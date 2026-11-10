using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Clans.SaveEditor.Core.Catalog;
using MechWarrior5.Clans.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.App.Clans.ViewModels;

public sealed partial class MechRow : SelectableItem
{
    public MechRow(ClansMech mech)
    {
        Mech = mech;
        Chassis = ClansCatalog.ChassisOf(mech.Variant);
    }

    public ClansMech Mech { get; }

    public Chassis Chassis { get; }

    public string Title => string.IsNullOrEmpty(Mech.CustomName) ? Chassis.Name : $"{Mech.CustomName}";

    public string Variant => ClansCatalog.VariantCode(Mech.Variant);

    public string Subtitle => $"{Chassis.Name} {Variant}  -  {Chassis.WeightClass} {Chassis.Tons}T";

    public string Initials => Chassis.Code.Length > 3 ? Chassis.Code[..3] : Chassis.Code;

    public string SarnaTitle => Chassis.SarnaTitle;

    public bool IsDamaged => Mech.IsDamaged;

    public string Gear => string.Join(", ", Mech.Equipment.Take(6).Select(ClansCatalog.ItemLabel))
        + (Mech.Equipment.Count > 6 ? $" +{Mech.Equipment.Count - 6}" : string.Empty);

    public void Refresh() => OnPropertyChanged(string.Empty);
}

public sealed partial class MechsViewModel : ObservableObject
{
    private readonly ClansDocument _doc;
    private readonly ClansMainViewModel _main;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private int _checkedCount;

    public MechsViewModel(ClansDocument doc, ClansMainViewModel main, IDialogService dialogs)
    {
        _doc = doc;
        _main = main;
        _dialogs = dialogs;
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = o => o is MechRow row && Matches(row);
        Rebuild();
    }

    public ObservableCollection<MechRow> Rows { get; } = [];

    public ICollectionView View { get; }

    public string Summary => $"{Rows.Count} MECHS  -  {Rows.Count(r => r.IsDamaged)} DAMAGED";

    partial void OnFilterChanged(string value) => View.Refresh();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in View.Cast<MechRow>())
        {
            row.IsChecked = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var row in Rows)
        {
            row.IsChecked = false;
        }
    }

    [RelayCommand]
    private void SelectDamaged()
    {
        foreach (var row in Rows)
        {
            row.IsChecked = row.IsDamaged;
        }
    }

    [RelayCommand]
    private void Repair() => Run("Repaired", picks => InventoryEditor.RepairMechs(picks));

    [RelayCommand]
    private void Duplicate() => Run($"Duplicated x{Copies}", picks => _doc.Inventory.DuplicateMechs(picks, Math.Clamp(Copies, 1, 20)));

    [RelayCommand]
    private void Remove()
    {
        var picks = Picked();
        if (picks.Count == 0
            || !_dialogs.Confirm("Remove mechs", $"Remove {picks.Count} mech(s) from the inventory? Mechs that are deployed or in the mech bay's work queue are kept.", "Remove", DialogTone.Error))
        {
            return;
        }

        Run("Removed", _ => _doc.Inventory.RemoveMechs(picks));
    }

    [RelayCommand]
    private void Rename()
    {
        var picks = Picked();
        if (picks.Count == 0)
        {
            Nothing();
            return;
        }

        foreach (var mech in picks)
        {
            mech.CustomName = NewName;
        }

        _main.MarkDirty();
        Rebuild();
        _main.ShowToast(string.IsNullOrWhiteSpace(NewName)
            ? $"Cleared the name of {picks.Count} mech(s)."
            : $"Named {picks.Count} mech(s) \"{NewName.Trim()}\".");
    }

    private void Run(string title, Func<IReadOnlyList<ClansMech>, BatchResult> action)
    {
        var picks = Picked();
        if (picks.Count == 0)
        {
            Nothing();
            return;
        }

        var result = action(picks);
        if (result.Succeeded.Count > 0)
        {
            _main.MarkDirty();
        }

        Rebuild();
        if (result.Skipped.Count == 0)
        {
            _main.ShowToast($"{title}: {result.Succeeded.Count} mech(s). Save to keep the change.");
            return;
        }

        var sections = new List<DialogSection>();
        if (result.Succeeded.Count > 0)
        {
            sections.Add(new DialogSection($"DONE ({result.Succeeded.Count})", DialogTone.Success, result.Succeeded));
        }

        sections.Add(new DialogSection($"SKIPPED ({result.Skipped.Count})", DialogTone.Warning,
            [.. result.Skipped.Select(s => $"{s.Item}: {s.Reason}")]));
        _dialogs.Show(title, "Save to keep the changes.", result.Succeeded.Count > 0 ? DialogTone.Success : DialogTone.Warning, [.. sections]);
    }

    private void Nothing() =>
        _dialogs.Show("Nothing selected", "Tick the mechs to change (Shift+click ticks a range, Ctrl+A ticks everything shown).");

    private List<ClansMech> Picked() => [.. Rows.Where(r => r.IsChecked).Select(r => r.Mech)];

    private bool Matches(MechRow row) =>
        Filter.Length == 0
        || row.Title.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || row.Subtitle.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || row.Gear.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private void Rebuild()
    {
        var keep = Rows.Where(r => r.IsChecked).Select(r => r.Mech.InventoryId).ToHashSet();
        foreach (var row in Rows)
        {
            row.CheckedChanged -= OnRowChecked;
        }

        Rows.Clear();
        foreach (var mech in _doc.Inventory.Mechs
            .OrderByDescending(m => ClansCatalog.ChassisOf(m.Variant).Tons)
            .ThenBy(m => m.Variant, StringComparer.Ordinal))
        {
            var row = new MechRow(mech) { IsChecked = keep.Contains(mech.InventoryId) };
            row.CheckedChanged += OnRowChecked;
            Rows.Add(row);
        }

        OnRowChecked(null, EventArgs.Empty);
        OnPropertyChanged(nameof(Summary));
    }

    private void OnRowChecked(object? sender, EventArgs e) => CheckedCount = Rows.Count(r => r.IsChecked);
}

public sealed partial class StackRow : SelectableItem
{
    private readonly ClansDocument _doc;
    private readonly ClansMainViewModel _main;

    public StackRow(ClansDocument doc, ClansMainViewModel main, string type, string asset)
    {
        _doc = doc;
        _main = main;
        Type = type;
        Asset = asset;
    }

    public string Type { get; }

    public string Asset { get; }

    public string Label => ClansCatalog.ItemLabel(Asset);

    public int Quantity
    {
        get => _doc.Inventory.Total(Asset);
        set
        {
            var clamped = Math.Clamp(value, 0, InventoryEditor.MaxQuantity);
            if (clamped != Quantity)
            {
                _doc.Inventory.SetTotal(Type, Asset, clamped);
                OnPropertyChanged();
                _main.MarkDirty();
            }
        }
    }
}

public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly ClansDocument _doc;
    private readonly ClansMainViewModel _main;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private KnownItem? _itemToAdd;

    [ObservableProperty]
    private int _addCount = 1;

    public InventoryViewModel(ClansDocument doc, ClansMainViewModel main, IDialogService dialogs)
    {
        _doc = doc;
        _main = main;
        _dialogs = dialogs;
        Catalog = [.. ItemList.For(doc.Inventory).Where(i => i.Type == "KelEquipmentItemDataAsset")];
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = o => o is StackRow row && (Filter.Length == 0 || row.Label.Contains(Filter, StringComparison.OrdinalIgnoreCase));
        Rebuild();
    }

    public ObservableCollection<StackRow> Rows { get; } = [];

    public ICollectionView View { get; }

    public IReadOnlyList<KnownItem> Catalog { get; }

    partial void OnFilterChanged(string value) => View.Refresh();

    [RelayCommand]
    private void AddItem()
    {
        if (ItemToAdd is not { } item)
        {
            _dialogs.Show("Pick an item", "Choose a weapon or piece of equipment to add.");
            return;
        }

        _doc.Inventory.AddStack(item.Type, item.Asset, Math.Clamp(AddCount, 1, 10_000));
        _main.MarkDirty();
        Rebuild();
        _main.ShowToast($"Added {Math.Clamp(AddCount, 1, 10_000)} x {item.Label}. Save to keep it.");
    }

    [RelayCommand]
    private void AddToTicked(string? amount)
    {
        if (!int.TryParse(amount, out var delta))
        {
            return;
        }

        var picks = Rows.Where(r => r.IsChecked).ToList();
        if (picks.Count == 0)
        {
            _dialogs.Show("Nothing selected", "Tick the items to change (Shift+click ticks a range, Ctrl+A ticks everything shown).");
            return;
        }

        foreach (var row in picks)
        {
            row.Quantity = (int)Math.Min(InventoryEditor.MaxQuantity, (long)row.Quantity + delta);
        }

        _main.ShowToast($"Added {delta} to {picks.Count} item(s). Save to keep it.");
    }

    [RelayCommand]
    private void RemoveTicked()
    {
        var picks = Rows.Where(r => r.IsChecked).Select(r => r.Asset).ToHashSet(StringComparer.Ordinal);
        if (picks.Count == 0
            || !_dialogs.Confirm("Remove items", $"Remove {picks.Count} item type(s) from the inventory? Installed gear on mechs is not affected.", "Remove", DialogTone.Error))
        {
            return;
        }

        var result = _doc.Inventory.RemoveStacks(_doc.Inventory.Stacks.Where(s => picks.Contains(s.AssetName)));
        _main.MarkDirty();
        Rebuild();
        _main.ShowToast($"Removed {result.Succeeded.Count} stack(s). Save to keep it.");
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in View.Cast<StackRow>())
        {
            row.IsChecked = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var row in Rows)
        {
            row.IsChecked = false;
        }
    }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var asset in _doc.Inventory.Stacks
            .Where(s => s.Category == StackCategory.Equipment)
            .Select(s => (s.AssetType, s.AssetName))
            .Distinct()
            .OrderBy(s => ClansCatalog.ItemLabel(s.AssetName), StringComparer.CurrentCulture))
        {
            Rows.Add(new StackRow(_doc, _main, asset.AssetType, asset.AssetName));
        }
    }
}
