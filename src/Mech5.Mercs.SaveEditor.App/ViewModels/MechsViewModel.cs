using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.App.Services;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public sealed record SourceFilter(string Label, string? DlcTag, bool BaseGameOnly = false);

public sealed partial class MechsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly Mw5Document _doc;
    private readonly IDialogService _dialogs;
    private readonly IEditorWindows _windows;
    private readonly ItemCatalog _items;
    private readonly List<CatalogMechViewModel> _catalog;
    private readonly HashSet<WeightClass> _classes = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private SourceFilter _source;

    [ObservableProperty]
    private bool _ownedDlcOnly;

    [ObservableProperty]
    private bool _heroesOnly;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private bool _showColdStorageOnly;

    public MechsViewModel(MainViewModel main, Mw5Document doc, MechCatalog catalog, ItemCatalog items, IDialogService dialogs, IEditorWindows windows)
    {
        _main = main;
        _doc = doc;
        _dialogs = dialogs;
        _windows = windows;
        _items = items;
        _ownedDlcOnly = main.Settings.OwnedDlcOnly && doc.Dlc.Source != DlcSource.Unknown;

        Sources =
        [
            new SourceFilter("All sources", null),
            new SourceFilter("Base game", null, BaseGameOnly: true),
            .. catalog.DlcPacks.Select(p => new SourceFilter($"{p.ShortName} - {p.Name}", p.Tag)),
        ];
        _source = Sources[0];

        _catalog = catalog.Variants
            .OrderBy(v => v.ChassisName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.Code, StringComparer.OrdinalIgnoreCase)
            .Select(v => new CatalogMechViewModel(v, doc.Dlc.Source == DlcSource.Unknown || doc.Dlc.Owns(v.Dlc)))
            .ToList();
        foreach (var row in _catalog)
        {
            row.CheckedChanged += (_, _) => OnCatalogSelectionChanged();
        }

        CatalogView = CollectionViewSource.GetDefaultView(_catalog);
        CatalogView.Filter = o => Matches((CatalogMechViewModel)o);

        HangarView = CollectionViewSource.GetDefaultView(Hangar);
        HangarView.Filter = o => !ShowColdStorageOnly || ((HangarMechViewModel)o).IsColdStorage;
        RebuildHangar();
    }

    public IReadOnlyList<SourceFilter> Sources { get; }

    public ICollectionView CatalogView { get; }

    public ObservableCollection<HangarMechViewModel> Hangar { get; } = [];

    public ICollectionView HangarView { get; }

    public int HangarCount => Hangar.Count;

    public int BayCount => Hangar.Count(m => m.Record.Location == MechLocation.MechBay);

    public int ColdCount => Hangar.Count(m => m.IsColdStorage);

    public int VisibleCount => CatalogView.Cast<object>().Count();

    public int SelectedCount => _catalog.Count(m => m.IsChecked);

    public int HangarSelectedCount => Hangar.Count(m => m.IsChecked);

    public bool HasDlcInfo => _doc.Dlc.Source != DlcSource.Unknown;

    /// <summary>Tri-state "select all visible" header box.</summary>
    public bool? AllVisibleChecked
    {
        get
        {
            var visible = CatalogView.Cast<CatalogMechViewModel>().ToList();
            if (visible.Count == 0 || visible.All(m => !m.IsChecked))
            {
                return false;
            }

            return visible.All(m => m.IsChecked) ? true : null;
        }

        set => SetVisibleChecked(value == true);
    }

    public bool LightChecked
    {
        get => _classes.Contains(WeightClass.Light);
        set => ToggleClass(WeightClass.Light, value);
    }

    public bool MediumChecked
    {
        get => _classes.Contains(WeightClass.Medium);
        set => ToggleClass(WeightClass.Medium, value);
    }

    public bool HeavyChecked
    {
        get => _classes.Contains(WeightClass.Heavy);
        set => ToggleClass(WeightClass.Heavy, value);
    }

    public bool AssaultChecked
    {
        get => _classes.Contains(WeightClass.Assault);
        set => ToggleClass(WeightClass.Assault, value);
    }

    partial void OnSearchChanged(string value) => RefreshCatalog();

    partial void OnSourceChanged(SourceFilter value) => RefreshCatalog();

    partial void OnOwnedDlcOnlyChanged(bool value)
    {
        _main.Settings.OwnedDlcOnly = value;
        RefreshCatalog();
    }

    partial void OnHeroesOnlyChanged(bool value) => RefreshCatalog();

    partial void OnShowColdStorageOnlyChanged(bool value) => HangarView.Refresh();

    [RelayCommand]
    private void SelectAllVisible() => SetVisibleChecked(true);

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in _catalog.Where(r => r.IsChecked))
        {
            row.IsChecked = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _classes.Clear();
        Search = string.Empty;
        Source = Sources[0];
        HeroesOnly = false;
        OnPropertyChanged(string.Empty);
        RefreshCatalog();
    }

    /// <summary>Adds every ticked variant in one pass and reports the outcome once.</summary>
    [RelayCommand]
    private void AddSelected()
    {
        var picks = _catalog.Where(r => r.IsChecked).Select(r => r.Variant).ToList();
        if (picks.Count == 0)
        {
            _dialogs.Show("Nothing selected", "Tick the mechs you want (Shift+click selects a range, Ctrl+A selects everything shown), then add them.");
            return;
        }

        var copies = Math.Clamp(Copies, 1, 50);
        var result = _doc.Hangar.AddToColdStorage(picks, copies);
        RebuildHangar();

        var added = result.Succeeded.Select(v => copies > 1 ? $"{v.DisplayName}  x{copies}" : v.DisplayName).ToList();
        var skipped = result.Skipped.Select(s => $"{s.Item.DisplayName}: {s.Reason}").ToList();
        foreach (var row in _catalog.Where(r => result.Succeeded.Contains(r.Variant)))
        {
            row.IsChecked = false;
        }

        var sections = new List<DialogSection>();
        if (added.Count > 0)
        {
            sections.Add(new DialogSection($"ADDED TO COLD STORAGE ({added.Count * copies})", DialogTone.Success, added));
        }

        if (skipped.Count > 0)
        {
            sections.Add(new DialogSection($"SKIPPED ({skipped.Count})", DialogTone.Warning, skipped));
        }

        var message = added.Count > 0
            ? "New mechs arrive with their factory loadouts. Save to write them to the campaign file."
            : "No mechs were added.";
        _dialogs.Show(
            added.Count > 0 ? "Requisition complete" : "Requisition failed",
            message,
            skipped.Count == 0 ? DialogTone.Success : added.Count > 0 ? DialogTone.Warning : DialogTone.Error,
            [.. sections]);
    }

    [RelayCommand]
    private void SelectAllHangar()
    {
        foreach (var row in HangarView.Cast<HangarMechViewModel>())
        {
            row.IsChecked = true;
        }
    }

    [RelayCommand]
    private void ClearHangarSelection()
    {
        foreach (var row in Hangar.Where(r => r.IsChecked))
        {
            row.IsChecked = false;
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var picks = Hangar.Where(r => r.IsChecked).ToList();
        if (picks.Count == 0)
        {
            return;
        }

        var cold = picks.Count(p => p.IsColdStorage);
        if (cold == 0)
        {
            _dialogs.Show("Nothing to remove", "Only cold-storage mechs can be removed. MechBay mechs stay because your lances and pilots refer to them.", DialogTone.Warning);
            return;
        }

        if (!_dialogs.Confirm("Remove mechs", $"Remove {cold} mech(s) from cold storage? This can't be undone after you save.", "Remove", DialogTone.Error))
        {
            return;
        }

        var result = _doc.Hangar.RemoveFromColdStorage(picks.Select(p => p.Record));
        RebuildHangar();
        Report("Removed from cold storage", result, r => $"{r.Code} {r.Variant?.ChassisName}");
    }

    [RelayCommand]
    private void ResetSelected()
    {
        var picks = Hangar.Where(r => r.IsChecked).Select(r => r.Record).ToList();
        if (picks.Count == 0
            || !_dialogs.Confirm("Reset to stock", $"Replace the loadout of {picks.Count} mech(s) with their factory configuration? Installed weapons and equipment are not returned to inventory.", "Reset"))
        {
            return;
        }

        Report("Reset to stock loadout", _doc.Hangar.ResetToStock(picks), r => $"{r.Code} {r.Variant?.ChassisName}");
    }

    /// <summary>Opens the Mech Lab for the given row, or for the single ticked hangar mech.</summary>
    [RelayCommand]
    public void EditLoadout(HangarMechViewModel? row)
    {
        var picks = Hangar.Where(r => r.IsChecked).ToList();
        row ??= picks.Count == 1 ? picks[0] : null;
        if (row is null)
        {
            _dialogs.Show("Pick one mech", "Tick exactly one hangar mech (or double-click it) to open it in the Mech Lab.");
            return;
        }

        if (!row.Record.IsEditable)
        {
            _dialogs.Show("Can't edit this mech", "Its data uses a layout this editor can't safely rewrite.", DialogTone.Warning);
            return;
        }

        if (_windows.EditLoadout(new LoadoutEditorViewModel(row.Record, _doc.Hangar, _items, _dialogs)))
        {
            _main.ShowToast($"Updated {row.Code} loadout. Save to keep it.");
        }
    }

    [RelayCommand]
    private void RepairSelected() =>
        Repair(Hangar.Where(r => r.IsChecked).Select(r => r.Record).ToList());

    [RelayCommand]
    private void RepairAll() => Repair(Hangar.Select(r => r.Record).ToList());

    private void Repair(List<MechRecord> picks)
    {
        if (picks.Count > 0)
        {
            Report("Repaired", _doc.Hangar.Repair(picks), r => $"{r.Code} {r.Variant?.ChassisName}");
        }
    }

    private void Report(string title, BatchResult<MechRecord> result, Func<MechRecord, string> label)
    {
        var sections = new List<DialogSection>();
        if (result.Succeeded.Count > 0)
        {
            sections.Add(new DialogSection($"DONE ({result.Succeeded.Count})", DialogTone.Success, [.. result.Succeeded.Select(label)]));
        }

        if (result.Skipped.Count > 0)
        {
            sections.Add(new DialogSection($"SKIPPED ({result.Skipped.Count})", DialogTone.Warning,
                [.. result.Skipped.Select(s => $"{label(s.Item)}: {s.Reason}")]));
        }

        if (result.Skipped.Count == 0)
        {
            _main.ShowToast($"{title}: {result.Succeeded.Count} mech(s). Save to keep the change.");
        }
        else
        {
            _dialogs.Show(title, "Save to keep the changes.", result.Succeeded.Count > 0 ? DialogTone.Warning : DialogTone.Error, [.. sections]);
        }

        ClearHangarSelection();
    }

    private void RebuildHangar()
    {
        Hangar.Clear();
        foreach (var record in _doc.Hangar.Mechs.Where(m => m.Location != MechLocation.Market)
                     .OrderBy(m => m.Location)
                     .ThenBy(m => m.Variant?.ChassisName ?? m.Asset, StringComparer.OrdinalIgnoreCase))
        {
            var row = new HangarMechViewModel(record);
            row.CheckedChanged += (_, _) => OnPropertyChanged(nameof(HangarSelectedCount));
            Hangar.Add(row);
        }

        var owned = _doc.Hangar.Mechs
            .Where(m => m.Location != MechLocation.Market)
            .GroupBy(m => m.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var row in _catalog)
        {
            row.SetOwnedCount(owned.GetValueOrDefault(row.Variant.Asset));
        }

        OnPropertyChanged(nameof(HangarCount));
        OnPropertyChanged(nameof(BayCount));
        OnPropertyChanged(nameof(ColdCount));
        OnPropertyChanged(nameof(HangarSelectedCount));
    }

    private bool Matches(CatalogMechViewModel row)
    {
        var v = row.Variant;
        if (_classes.Count > 0 && !_classes.Contains(v.WeightClass))
        {
            return false;
        }

        if (Source.BaseGameOnly && v.Dlc is not null)
        {
            return false;
        }

        if (Source.DlcTag is { } tag && v.Dlc?.Tag != tag)
        {
            return false;
        }

        if (OwnedDlcOnly && !row.IsDlcOwned)
        {
            return false;
        }

        if (HeroesOnly && !row.IsHero)
        {
            return false;
        }

        var terms = Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.All(t => row.SearchText.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private void ToggleClass(WeightClass weight, bool on)
    {
        if (on ? _classes.Add(weight) : _classes.Remove(weight))
        {
            RefreshCatalog();
        }
    }

    private void SetVisibleChecked(bool value)
    {
        foreach (var row in CatalogView.Cast<CatalogMechViewModel>())
        {
            row.IsChecked = value;
        }
    }

    private void RefreshCatalog()
    {
        CatalogView.Refresh();
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(AllVisibleChecked));
    }

    private void OnCatalogSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(AllVisibleChecked));
    }
}
