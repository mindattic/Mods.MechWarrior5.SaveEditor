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
                // Still notify, so a rejected entry (below 0, over the cap) is replaced by the real value on screen.
                OnPropertyChanged();
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

/// <summary>The Star's pilots: unspent pilot XP and skill ranks.</summary>
public sealed class PilotsViewModel(ClansDocument doc, ClansMainViewModel main)
{
    public IReadOnlyList<PilotCard> Pilots { get; } = [.. doc.Inventory.StarPilots.Select(p => new PilotCard(p, main))];

    public bool HasPilots => Pilots.Count > 0;
}

public sealed partial class PilotCard : ObservableObject
{
    private readonly ClansPilot _pilot;
    private readonly ClansMainViewModel _main;

    public PilotCard(ClansPilot pilot, ClansMainViewModel main)
    {
        _pilot = pilot;
        _main = main;
        Rebuild();
    }

    public string Name => _pilot.Name.ToUpperInvariant();

    public int Experience
    {
        get => _pilot.Experience;
        set
        {
            var clamped = Math.Clamp(value, 0, InventoryEditor.MaxQuantity);
            if (clamped != _pilot.Experience)
            {
                _pilot.Experience = clamped;
                _main.MarkDirty();
            }

            OnPropertyChanged();
        }
    }

    public ObservableCollection<SkillRow> Skills { get; } = [];

    public ObservableCollection<SkillChoice> SkillChoices { get; } = [];

    [ObservableProperty]
    private SkillChoice? _skillToAdd;

    public bool CanAddSkills => _pilot.CanAddSkills;

    [RelayCommand]
    private void AddXp(string? step)
    {
        if (int.TryParse(step, out var delta))
        {
            Experience = (int)Math.Min(InventoryEditor.MaxQuantity, (long)Experience + delta);
        }
    }

    [RelayCommand]
    private void AddSkill()
    {
        if (SkillToAdd is { } choice)
        {
            Change(() => _pilot.SetSkill(choice.Asset, 1));
        }
    }

    [RelayCommand]
    private void MaxSkills() => Change(() =>
    {
        foreach (var skill in _pilot.Skills)
        {
            _pilot.SetSkill(skill.Asset, ClansPilot.MaxRank);
        }
    });

    internal void SetRank(string asset, int rank)
    {
        _pilot.SetSkill(asset, rank);
        _main.MarkDirty();
    }

    internal void Remove(string asset) => Change(() => _pilot.RemoveSkill(asset));

    private void Change(Action edit)
    {
        try
        {
            edit();
        }
        catch (InvalidOperationException e)
        {
            _main.ShowError(e.Message);
            return;
        }

        _main.MarkDirty();
        Rebuild();
    }

    private void Rebuild()
    {
        Skills.Clear();
        foreach (var skill in _pilot.Skills)
        {
            Skills.Add(new SkillRow(this, skill.Asset, skill.Rank));
        }

        SkillChoices.Clear();
        foreach (var asset in PilotSkills.All.Where(a => Skills.All(s => s.Asset != a)).OrderBy(PilotSkills.Label, StringComparer.CurrentCulture))
        {
            SkillChoices.Add(new SkillChoice(asset, PilotSkills.Label(asset)));
        }

        OnPropertyChanged(nameof(CanAddSkills));

        SkillToAdd = null;
    }
}

public sealed record SkillChoice(string Asset, string Label);

public sealed partial class SkillRow(PilotCard pilot, string asset, int rank) : ObservableObject
{
    public string Asset { get; } = asset;

    public string Label { get; } = PilotSkills.Label(asset);

    public int Rank
    {
        get => rank;
        set
        {
            var clamped = Math.Clamp(value, 1, ClansPilot.MaxRank);
            if (clamped != rank)
            {
                rank = clamped;
                pilot.SetRank(Asset, clamped);
            }

            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void Remove() => pilot.Remove(Asset);
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

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ClansMainViewModel main;
    private readonly EventHandler? artChanged;

    public SettingsViewModel(ClansMainViewModel main)
    {
        this.main = main;
        if (Art is { } art)
        {
            artChanged = (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(ArtStatus)));
            art.ClansChanged += artChanged;
        }

        // The compatibility check finishes in the background (after the game's files are read), maybe after this page opened.
        main.PropertyChanged += OnMainChanged;
    }

    private void OnMainChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClansMainViewModel.Compatibility) or nameof(ClansMainViewModel.CompatibilityChecked)
            or nameof(ClansMainViewModel.HasDocument) or nameof(ClansMainViewModel.InstalledMods))
        {
            OnPropertyChanged(nameof(Compatibility));
            OnPropertyChanged(nameof(HasCompatibility));
            OnPropertyChanged(nameof(CompatibilitySummary));
            OnPropertyChanged(nameof(Mods));
            OnPropertyChanged(nameof(HasMods));
        }
    }

    /// <summary>Rewords the page's code-built text after a language change.</summary>
    public void Reword() => OnPropertyChanged(string.Empty);

    /// <summary>
    /// Navigate() replaces this instance with a new one on every visit; without this, each visit would leave
    /// another closure subscribed to the app-wide art service, firing (and growing) forever.
    /// </summary>
    public void Dispose()
    {
        if (Art is { } art && artChanged is not null)
        {
            art.ClansChanged -= artChanged;
        }

        main.PropertyChanged -= OnMainChanged;
    }

    /// <summary>The open save checked against the installed Clans game and mods.</summary>
    public IReadOnlyList<ClansCompatibilityLine> Compatibility => main.Compatibility;

    public bool HasCompatibility => main.Compatibility.Count > 0;

    public string CompatibilitySummary => !main.HasDocument
        ? Loc.Text("compat_no_save", "Open a save to check it against the installed game and mods.")
        : main.CompatibilityChecked is null
            ? Loc.Text("compat_checking", "Checking this save against the installed game and mods...")
        : main.CompatibilityChecked == false
            ? Loc.Text("compat_unavailable", "MechWarrior 5: Clans or its mods couldn't be read, so this save wasn't checked.")
        : main.Compatibility.Count == 0
            ? Loc.Text("compat_vanilla", "This save matches the vanilla game: nothing in it needs a mod.")
            : Loc.Text("compat_summary", "{0} problem(s): content or values that need a mod that is missing, disabled or the wrong version. {1} modded value(s) covered by active mods.",
                main.Compatibility.Count(l => l.Problem), main.Compatibility.Count(l => !l.Problem));

    /// <summary>Installed Clans mods, each with whether the game has it on.</summary>
    public IReadOnlyList<string> Mods => [.. main.InstalledMods.Select(m => m.Enabled
        ? Loc.Text("clans_mod_on", "{0}  (on, {1} assets)", m.Label, m.Assets.Count)
        : Loc.Text("clans_mod_off", "{0}  (off)", m.Label))];

    public bool HasMods => main.InstalledMods.Count > 0;

    private static MechWarrior5.SaveEditor.App.Services.GameArtSource? Art =>
        MechWarrior5.Shared.Wpf.Behaviors.MechArt.Service as MechWarrior5.SaveEditor.App.Services.GameArtSource;

    /// <summary>Where Clans 'Mech pictures come from right now.</summary>
    public string ArtStatus => Art?.ClansStatus switch
    {
        MechWarrior5.SaveEditor.App.Services.ClansArtStatus.Ready => Loc.Text("clans_art_ready", "Pictures come from your MechWarrior 5: Clans files, decompressed with the Oodle DLL at {0}.", Art.OodlePath ?? main.Settings.OodlePath ?? "?"),
        MechWarrior5.SaveEditor.App.Services.ClansArtStatus.Searching => Loc.Text("clans_art_searching", "Looking for MechWarrior 5: Clans and an Oodle DLL on this PC..."),
        MechWarrior5.SaveEditor.App.Services.ClansArtStatus.ClansNotFound => Loc.Text("clans_art_no_game", "MechWarrior 5: Clans wasn't found, so 'Mechs show the Mercenaries picture of the same chassis when Mercenaries is installed."),
        MechWarrior5.SaveEditor.App.Services.ClansArtStatus.OodleNotFound => Loc.Text("clans_art_no_oodle", "Clans keeps its pictures Oodle-compressed, and no Oodle DLL (oo2core*.dll) was found on this PC. 'Mechs show the Mercenaries picture of the same chassis instead. If an Unreal Engine install or another game has an oo2core*.dll, choose it below."),
        _ => string.Empty,
    };

    /// <summary>Pick an oo2core*.dll already on this PC; it is checked before use and never copied.</summary>
    [RelayCommand]
    private void ChooseOodle()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Text("clans_art_pick", "Choose an Oodle DLL (oo2core*.dll)"),
            Filter = "Oodle (oo2core*.dll)|oo2core*.dll",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (MechWarrior5.Shared.Assets.Oodle.TryLoad(dialog.FileName, out var error) is null)
        {
            main.ShowError(Loc.Text("clans_art_bad_oodle", "That file can't be used: {0}", error ?? "?"));
            return;
        }

        main.Settings.OodlePath = dialog.FileName;
        main.SaveSettings();
        SearchAgain();
    }

    [RelayCommand]
    private void SearchAgain()
    {
        if (Art is { } art)
        {
            _ = art.StartClans(main.Settings.OodlePath);
            OnPropertyChanged(nameof(ArtStatus));
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
