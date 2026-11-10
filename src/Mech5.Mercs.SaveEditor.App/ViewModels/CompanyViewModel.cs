using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public sealed record DlcRow(string Code, string Name, bool Owned);

public sealed partial class CompanyViewModel : ObservableObject
{
    private readonly Mw5Document _doc;
    private readonly MechCatalog _catalog;
    private readonly IDialogService _dialogs;
    private readonly Action _imported;
    private readonly PilotCatalog _traits;

    [ObservableProperty]
    private string _dateText;

    [ObservableProperty]
    private TraitDefinition? _unlockToAdd;

    public CompanyViewModel(Mw5Document doc, MechCatalog catalog, PilotCatalog traits, IDialogService dialogs, Action imported)
    {
        _traits = traits;
        _doc = doc;
        _catalog = catalog;
        _dialogs = dialogs;
        _imported = imported;
        _dateText = doc.Company.CampaignDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        Dlcs = catalog.DlcPacks.Select(p => new DlcRow(p.ShortName, p.Name, doc.Dlc.Tags.Contains(p.Tag))).ToList();
        DlcNote = doc.Dlc.Source switch
        {
            DlcSource.ThisSave => "Read from this save's campaign index.",
            DlcSource.CampaignLatestSave => "This save isn't in its campaign index; showing the campaign's latest save.",
            _ => "No Campaign.json beside this save, so DLC ownership can't be checked.",
        };
    }

    public IReadOnlyList<DlcRow> Dlcs { get; }

    public string DlcNote { get; }

    public long CBills
    {
        get => _doc.Company.CBills;
        set
        {
            _doc.Company.CBills = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CBillsText));
        }
    }

    public string CBillsText => CBills.ToString("N0", CultureInfo.CurrentCulture);

    public int Reputation
    {
        get => _doc.Company.Reputation;
        set
        {
            _doc.Company.Reputation = value;
            OnPropertyChanged();
        }
    }

    public string CompanyName
    {
        get => _doc.Company.CompanyName;
        set
        {
            _doc.Company.CompanyName = value;
            OnPropertyChanged();
        }
    }

    public string CampaignDate => _doc.Company.CampaignDate?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) ?? "Unknown";

    public string? GameVersion => _doc.File.ProjectVersion;

    public int MechBayCount => _doc.Hangar.Mechs.Count(m => m.Location == MechLocation.MechBay);

    public int ColdStorageCount => _doc.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage);

    public int PilotCount => _doc.Roster.Pilots.Count;

    public int ItemCount => _doc.Inventory.Items.Sum(i => i.Count);

    public void Refresh() => OnPropertyChanged(string.Empty);

    public bool HasMechTraits => _doc.Traits.HasMechTraits;

    public IReadOnlyList<TraitRow> UnlockedTraits =>
        [.. _doc.Traits.UnlockedMechTraits.Select(t => new TraitRow(t, _traits.TraitLabel(t)))];

    public IReadOnlyList<TraitDefinition> TraitChoices => _traits.MechTraits;

    public bool HasCantinas => _doc.Traits.HasCantinas;

    public bool CantinasUnlocked
    {
        get => !_doc.Traits.CantinasLocked;
        set
        {
            _doc.Traits.CantinasLocked = !value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void UnlockTrait()
    {
        if (UnlockToAdd is { } trait && _doc.Traits.UnlockMechTrait(trait.Asset))
        {
            OnPropertyChanged(nameof(UnlockedTraits));
        }
    }

    [RelayCommand]
    private void LockTrait(TraitRow? trait)
    {
        if (trait is not null && _doc.Traits.LockMechTrait(trait.Asset))
        {
            OnPropertyChanged(nameof(UnlockedTraits));
        }
    }

    [RelayCommand]
    private void AddCBills(string amount) =>
        CBills += long.Parse(amount, CultureInfo.InvariantCulture);

    [RelayCommand]
    private void MaxCBills() => CBills = CompanyEditor.MaxCBills;

    [RelayCommand]
    private void MoveDate()
    {
        if (!DateTime.TryParseExact(DateText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _dialogs.Show("Invalid date", "Use the form YYYY-MM-DD, e.g. 3049-05-01.", DialogTone.Error);
            return;
        }

        if (!_dialogs.Confirm("Move the calendar",
                $"Move the campaign to {date:d MMMM yyyy}? Payroll, injuries, contracts and every other dated event move with it, so their timing is kept.",
                "Move date"))
        {
            return;
        }

        var days = _doc.Company.MoveCampaignDate(date);
        OnPropertyChanged(nameof(CampaignDate));
        _dialogs.Show("Calendar moved", days == 0 ? "The campaign is already on that date." : $"Moved {Math.Abs(days):N0} days {(days > 0 ? "forward" : "back")}. Save to keep it.", DialogTone.Success);
    }

    [RelayCommand]
    private void Export()
    {
        var name = string.Concat(CompanyName.Split(Path.GetInvalidFileNameChars())) + CompanyPackage.Extension;
        if (_dialogs.PickFileToSave("Export company", name, $"MW5 company (*{CompanyPackage.Extension})|*{CompanyPackage.Extension}") is not { } path)
        {
            return;
        }

        var package = CompanyTransfer.Export(_doc);
        package.Save(path);
        _dialogs.Show("Company exported",
            $"{package.Mechs.Count} mechs, {package.Pilots.Count} pilots and {package.Inventory.Count} inventory stacks written to {Path.GetFileName(path)}.",
            DialogTone.Success);
    }

    [RelayCommand]
    private void Import()
    {
        if (_dialogs.PickFileToOpen("Import company", $"MW5 company (*{CompanyPackage.Extension})|*{CompanyPackage.Extension}") is not { } path)
        {
            return;
        }

        CompanyPackage package;
        try
        {
            package = CompanyPackage.Load(path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _dialogs.Show("Can't import", e.Message, DialogTone.Error);
            return;
        }

        if (!_dialogs.Confirm("Import company",
                $"Add {package.Company}'s {package.Mechs.Count} mechs (to cold storage), {package.Pilots.Count} pilots and {package.Inventory.Count} inventory stacks to this campaign?",
                "Import"))
        {
            return;
        }

        var report = CompanyTransfer.Import(_doc, package, _catalog, new TransferOptions());
        var sections = new List<DialogSection>();
        Add("MECHS", report.Mechs);
        Add("PILOTS", report.Pilots);
        Add("INVENTORY", report.Inventory);
        _imported();
        Refresh();
        _dialogs.Show("Company imported", "Save to keep it.",
            sections.Any(s => s.Tone == DialogTone.Warning) ? DialogTone.Warning : DialogTone.Success, [.. sections]);

        void Add(string what, BatchResult<string> result)
        {
            if (result.Succeeded.Count > 0)
            {
                sections.Add(new DialogSection($"{what} ADDED ({result.Succeeded.Count})", DialogTone.Success, [.. result.Succeeded]));
            }

            if (result.Skipped.Count > 0)
            {
                sections.Add(new DialogSection($"{what} SKIPPED ({result.Skipped.Count})", DialogTone.Warning,
                    [.. result.Skipped.Select(s => $"{s.Item}: {s.Reason}")]));
            }
        }
    }
}
