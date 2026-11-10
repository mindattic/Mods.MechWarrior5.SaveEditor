using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

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
            DlcSource.ThisSave => Loc.Text("dlc_note_this", "Read from this save's campaign index."),
            DlcSource.CampaignLatestSave => Loc.Text("dlc_note_latest", "This save isn't in its campaign index; showing the campaign's latest save."),
            _ => Loc.Text("dlc_note_unknown", "No Campaign.json beside this save, so DLC ownership can't be checked."),
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

    public string CampaignDate => _doc.Company.CampaignDate?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) ?? Loc.Text("unknown", "Unknown");

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
            _dialogs.Show(Loc.Text("invalid_date", "Invalid date"), Loc.Text("invalid_date_text", "Use the form YYYY-MM-DD, e.g. 3049-05-01."), DialogTone.Error);
            return;
        }

        if (!_dialogs.Confirm(Loc.Text("move_calendar_title", "Move the calendar"),
                Loc.Text("move_calendar_text", "Move the campaign to {0:d MMMM yyyy}? Payroll, injuries, contracts and every other dated event move with it, so their timing is kept.", date),
                Loc.Text("move_date", "Move date")))
        {
            return;
        }

        int days;
        try
        {
            days = _doc.Company.MoveCampaignDate(date);
        }
        catch (ArgumentOutOfRangeException e)
        {
            _dialogs.Show(Loc.Text("invalid_date", "Invalid date"), e.Message.Split(" (Parameter")[0], DialogTone.Error);
            return;
        }

        OnPropertyChanged(nameof(CampaignDate));
        _dialogs.Show(Loc.Text("calendar_moved", "Calendar moved"), days == 0
            ? Loc.Text("calendar_same", "The campaign is already on that date.")
            : days > 0
                ? Loc.Text("calendar_forward", "Moved {0:N0} days forward. Save to keep it.", days)
                : Loc.Text("calendar_back", "Moved {0:N0} days back. Save to keep it.", -days), DialogTone.Success);
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
        try
        {
            package.Save(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _dialogs.Show(Loc.Text("cant_export", "Can't export"), e.Message, DialogTone.Error);
            return;
        }

        _dialogs.Show(Loc.Text("company_exported", "Company exported"),
            Loc.Text("company_exported_text", "{0} mechs, {1} pilots and {2} inventory stacks written to {3}.", package.Mechs.Count, package.Pilots.Count, package.Inventory.Count, Path.GetFileName(path)),
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            _dialogs.Show(Loc.Text("cant_import", "Can't import"), e.Message, DialogTone.Error);
            return;
        }

        if (!_dialogs.Confirm(Loc.Text("import_company_title", "Import company"),
                Loc.Text("import_company_text", "Add {0}'s {1} mechs (to cold storage), {2} pilots and {3} inventory stacks to this campaign?", package.Company, package.Mechs.Count, package.Pilots.Count, package.Inventory.Count),
                Loc.Text("import_button", "Import")))
        {
            return;
        }

        var report = CompanyTransfer.Import(_doc, package, _catalog, new TransferOptions());
        var sections = new List<DialogSection>();
        Add(Loc.Instance["mechs"], report.Mechs);
        Add(Loc.Instance["pilots"], report.Pilots);
        Add(Loc.Instance["inventory"], report.Inventory);
        _imported();
        Refresh();
        _dialogs.Show(Loc.Text("company_imported", "Company imported"), Loc.Text("save_to_keep", "Save to keep it."),
            sections.Any(s => s.Tone == DialogTone.Warning) ? DialogTone.Warning : DialogTone.Success, [.. sections]);

        void Add(string what, BatchResult<string> result)
        {
            if (result.Succeeded.Count > 0)
            {
                sections.Add(new DialogSection(Loc.Text("section_added", "{0} ADDED ({1})", what, result.Succeeded.Count), DialogTone.Success, [.. result.Succeeded]));
            }

            if (result.Skipped.Count > 0)
            {
                sections.Add(new DialogSection(Loc.Text("section_skipped_what", "{0} SKIPPED ({1})", what, result.Skipped.Count), DialogTone.Warning,
                    [.. result.Skipped.Select(s => $"{s.Item}: {s.Reason}")]));
            }
        }
    }
}
