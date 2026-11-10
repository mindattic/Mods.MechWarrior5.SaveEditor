using System.Collections.ObjectModel;
using MechWarrior5.SaveEditor.App.Localization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Clans.SaveEditor.Core.Catalog;
using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;

namespace MechWarrior5.SaveEditor.App.Clans.ViewModels;

/// <summary>The campaign picker: every save in the folder, as the load menu names them.</summary>
public sealed partial class StartViewModel(ClansMainViewModel main, string folder) : ObservableObject
{
    public ObservableCollection<SaveEntry> Saves { get; } = [];

    public string Folder { get; private set; } = folder;

    public bool HasSaves => Saves.Count > 0;

    public StartViewModel Refresh()
    {
        Folder = main.SaveFolder;
        Saves.Clear();
        foreach (var save in SaveLibrary.List(Folder))
        {
            Saves.Add(save);
        }

        OnPropertyChanged(nameof(Folder));
        OnPropertyChanged(nameof(HasSaves));
        return this;
    }

    [RelayCommand]
    private void Open(SaveEntry? save)
    {
        if (save is not null)
        {
            main.OpenPath(save.Path);
        }
    }

    [RelayCommand]
    private void Reload() => Refresh();
}

/// <summary>An editable amount of a currency, salvage, crew or mech-XP stack.</summary>
public sealed partial class AmountRow : ObservableObject
{
    private readonly ClansDocument _doc;
    private readonly ClansMainViewModel _main;

    public AmountRow(ClansDocument doc, ClansMainViewModel main, string type, string asset, string label, string? hint = null)
    {
        _doc = doc;
        _main = main;
        Type = type;
        Asset = asset;
        Label = label;
        Hint = hint;
    }

    public string Type { get; }

    public string Asset { get; }

    public string Label { get; }

    public string? Hint { get; }

    public int Amount
    {
        get => _doc.Inventory.Total(Asset);
        set
        {
            var clamped = Math.Clamp(value, 0, InventoryEditor.MaxQuantity);
            if (clamped == Amount)
            {
                return;
            }

            _doc.Inventory.SetTotal(Type, Asset, clamped);
            switch (Asset)
            {
                // Honor's level track and the technician pool mirror these stacks.
                case "Honor_CurrencyItem":
                    _doc.Progress.HonorExperience = clamped;
                    break;
                case "Technician_CrewItem":
                    _doc.Progress.TechniciansAvailable = clamped;
                    break;
            }

            OnPropertyChanged();
            _main.MarkDirty();
        }
    }

    [RelayCommand]
    private void Add(string? step)
    {
        if (int.TryParse(step, out var delta))
        {
            Amount = (int)Math.Min(InventoryEditor.MaxQuantity, (long)Amount + delta);
        }
    }
}

/// <summary>Kerenskies, Merits, Honor, salvage, crew and per-chassis mech XP.</summary>
public sealed partial class ResourcesViewModel : ObservableObject
{
    private readonly ClansDocument _doc;

    public ResourcesViewModel(ClansDocument doc, ClansMainViewModel main)
    {
        _doc = doc;
        const string Currency = "KelCurrencyItemDataAsset";
        const string Crew = "KelCrewItemDataAsset";
        Currencies =
        [
            // Kerenskies, Merits and Honor are the game's own names for its currencies; they stay as they are.
            new(doc, main, Currency, "Kerenskies_CurrencyItem", "KERENSKIES", Loc.Text("clans_res_kerenskies_hint", "Clan currency for mechs, omnipods and equipment.")),
            new(doc, main, Currency, "Merits_CurrencyItem", "MERITS", Loc.Text("clans_res_merits_hint", "Spent on research and upgrades.")),
            new(doc, main, Currency, "Honor_CurrencyItem", "HONOR", Loc.Text("clans_res_honor_hint", "Also moves the Honor level track.")),
        ];
        Salvage =
        [
            new(doc, main, Currency, "Salvage_MechComponents", Loc.Text("clans_res_mech_components", "MECH COMPONENTS")),
            new(doc, main, Currency, "Salvage_WeaponComponents", Loc.Text("clans_res_weapon_components", "WEAPON COMPONENTS")),
            new(doc, main, Currency, "Salvage_EquipmentComponents", Loc.Text("clans_res_equipment_components", "EQUIPMENT COMPONENTS")),
        ];
        CrewRows =
        [
            new(doc, main, Crew, "Technician_CrewItem", Loc.Text("clans_res_technicians", "TECHNICIANS"), Loc.Text("clans_res_technicians_hint", "Also sets the mech bay's technician pool.")),
            new(doc, main, Crew, "Scientist_CrewItem", Loc.Text("clans_res_scientists", "SCIENTISTS")),
            new(doc, main, Crew, "TechnicianUpgrade_CrewItem", Loc.Text("clans_res_technician_upgrades", "TECHNICIAN UPGRADES")),
            new(doc, main, Crew, "ScientistUpgrade_CrewItem", Loc.Text("clans_res_scientist_upgrades", "SCIENTIST UPGRADES")),
            new(doc, main, Crew, "SalvageUpgrade_CrewItem", Loc.Text("clans_res_salvage_upgrades", "SALVAGE UPGRADES")),
        ];
        MechXp = [.. doc.Inventory.Stacks
            .Where(s => s.Category == StackCategory.MechXp)
            .Select(s => s.AssetName)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Select(asset => new AmountRow(doc, main, Currency, asset, ClansCatalog.MechXpLabel(asset).ToUpperInvariant()))];
        Currencies[2].PropertyChanged += (_, _) => OnPropertyChanged(nameof(HonorLevel));
    }

    public IReadOnlyList<AmountRow> Currencies { get; }

    public IReadOnlyList<AmountRow> Salvage { get; }

    public IReadOnlyList<AmountRow> CrewRows { get; }

    public IReadOnlyList<AmountRow> MechXp { get; }

    public int? HonorLevel => _doc.Progress.HonorLevel;
}

/// <summary>Research and campaign clock, read from the save.</summary>
public sealed class ProgressViewModel(ClansDocument doc)
{
    public int? HonorLevel => doc.Progress.HonorLevel;

    public int? HonorExperience => doc.Progress.HonorExperience;

    public int? Cycles => doc.Progress.CyclesElapsed;

    public int? Technicians => doc.Progress.TechniciansAvailable;

    public IReadOnlyList<string> Queue => [.. doc.Progress.ResearchQueue.Select(Describe)];

    public IReadOnlyList<string> Completed => [.. doc.Progress.CompletedResearch.Select(Describe)];

    private static string Describe(ResearchEntry r) =>
        Loc.Text("clans_research_entry", "{0}  -  level {1}", ClansCatalog.ItemLabel(r.Job.Replace("_ResearchJob", string.Empty, StringComparison.Ordinal)), r.Level + 1)
        + (r.PointsApplied > 0 ? Loc.Text("clans_research_points", "  ({0:N0} pts)", r.PointsApplied) : string.Empty);
}

public sealed partial class SettingsViewModel(ClansMainViewModel main) : ObservableObject
{
    public bool DownloadMechImages
    {
        get => main.Settings.DownloadMechImages;
        set
        {
            main.Settings.DownloadMechImages = value;
            if (MechWarrior5.Shared.Wpf.Behaviors.MechArt.Service is { } images)
            {
                images.Enabled = value;
            }

            main.SaveSettings();
            OnPropertyChanged();
        }
    }

    public string SaveFolder
    {
        get => main.SaveFolder;
        set
        {
            main.Settings.ClansSaveFolder = string.IsNullOrWhiteSpace(value) || value == SaveLibrary.DefaultFolder ? null : value.Trim();
            main.SaveSettings();
            OnPropertyChanged();
        }
    }

    public bool FolderExists => Directory.Exists(main.SaveFolder);

    [RelayCommand]
    private void ResetFolder() => SaveFolder = SaveLibrary.DefaultFolder;
}
