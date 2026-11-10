using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public enum ItemFilter
{
    All,
    Weapons,
    Equipment,
    Ammo,
}

public sealed partial class InventoryRowViewModel : SelectableItem
{
    public InventoryRowViewModel(InventoryItem item)
    {
        Item = item;
        Name = Names.Humanize(item.Asset);
        Tier = Names.Tier(item.Asset);
        TypeLabel = Names.AssetTypeLabel(item.AssetType);
    }

    public InventoryItem Item { get; }

    public string Name { get; }

    public string? Tier { get; }

    public string TypeLabel { get; }

    public string Asset => Item.Asset;

    public string Health => Item.Health;

    public bool IsDamaged => !Item.IsHealthy;

    public int Count
    {
        get => Item.Count;
        set
        {
            Item.Count = value;
            OnPropertyChanged();
        }
    }

    public ItemKind Kind => Item.IsWeapon ? ItemKind.Weapon : Item.IsAmmo ? ItemKind.Ammo : ItemKind.Equipment;
}

public sealed partial class ItemPickViewModel : SelectableItem
{
    public ItemPickViewModel(ItemDefinition definition)
    {
        Definition = definition;
        Name = Names.Humanize(definition.Asset);
        Tier = Names.Tier(definition.Asset);
        TypeLabel = Names.AssetTypeLabel(definition.AssetType);
        SearchText = $"{definition.Asset} {Name} {TypeLabel}";
    }

    public ItemDefinition Definition { get; }

    public string Name { get; }

    public string? Tier { get; }

    public string TypeLabel { get; }

    public string SearchText { get; }
}

public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly Mw5Document _doc;
    private readonly IDialogService _dialogs;
    private readonly List<ItemPickViewModel> _catalog;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private ItemFilter _filter = ItemFilter.All;

    [ObservableProperty]
    private string _catalogSearch = string.Empty;

    [ObservableProperty]
    private ItemFilter _catalogFilter = ItemFilter.Weapons;

    [ObservableProperty]
    private int _addCount = 1;

    [ObservableProperty]
    private int _setCount = 10;

    public InventoryViewModel(Mw5Document doc, ItemCatalog items, IDialogService dialogs)
    {
        _doc = doc;
        _dialogs = dialogs;
        _catalog = items.Items.OrderBy(i => Names.Humanize(i.Asset), StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Asset, StringComparer.Ordinal)
            .Select(i => new ItemPickViewModel(i))
            .ToList();
        foreach (var pick in _catalog)
        {
            pick.CheckedChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CatalogSelectedCount));
                OnPropertyChanged(nameof(HasCatalogSelection));
            };
        }

        CatalogView = CollectionViewSource.GetDefaultView(_catalog);
        CatalogView.Filter = o => Matches((ItemPickViewModel)o);
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = o => Matches((InventoryRowViewModel)o);
        Rebuild();
    }

    public IReadOnlyList<ItemFilter> Filters { get; } = Enum.GetValues<ItemFilter>();

    public ObservableCollection<InventoryRowViewModel> Items { get; } = [];

    public ICollectionView ItemsView { get; }

    public ICollectionView CatalogView { get; }

    public int SelectedCount => Items.Count(i => i.IsChecked);

    public int CatalogSelectedCount => _catalog.Count(i => i.IsChecked);

    /// <summary>Enables the stock batch buttons (set count, remove) only while a row is ticked.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>Enables the requisition add button only while an item is ticked.</summary>
    public bool HasCatalogSelection => CatalogSelectedCount > 0;

    /// <summary>Takes over a previous page's searches, filters, counts and ticks (rebuilt on a language change or rescan).</summary>
    public void CarryOver(InventoryViewModel old)
    {
        Search = old.Search;
        Filter = old.Filter;
        CatalogSearch = old.CatalogSearch;
        CatalogFilter = old.CatalogFilter;
        AddCount = old.AddCount;
        SetCount = old.SetCount;
        var ticked = old.Items.Where(r => r.IsChecked).Select(r => (r.Asset, r.Item.Health, r.Item.IsWeapon)).ToHashSet();
        foreach (var row in Items)
        {
            row.IsChecked = ticked.Contains((row.Asset, row.Item.Health, row.Item.IsWeapon));
        }

        var picks = old._catalog.Where(r => r.IsChecked).Select(r => r.Definition.Asset).ToHashSet(StringComparer.Ordinal);
        foreach (var pick in _catalog)
        {
            pick.IsChecked = picks.Contains(pick.Definition.Asset);
        }

        OnPropertyChanged(string.Empty);
    }

    public int TotalCount => Items.Sum(i => i.Count);

    partial void OnSearchChanged(string value) => RefreshItems();

    partial void OnFilterChanged(ItemFilter value) => RefreshItems();

    partial void OnCatalogSearchChanged(string value) => RefreshCatalogPicks();

    partial void OnCatalogFilterChanged(ItemFilter value) => RefreshCatalogPicks();

    // Batch actions act on ticked rows, so a row the filter hides is unticked: what you see is what changes.
    private void RefreshItems()
    {
        ItemsView.Refresh();
        foreach (var row in Items.Where(r => r.IsChecked && !Matches(r)))
        {
            row.IsChecked = false;
        }
    }

    private void RefreshCatalogPicks()
    {
        CatalogView.Refresh();
        foreach (var pick in _catalog.Where(p => p.IsChecked && !Matches(p)))
        {
            pick.IsChecked = false;
        }
    }

    [RelayCommand]
    private void AddSelected()
    {
        var picks = _catalog.Where(p => p.IsChecked).Select(p => p.Definition).ToList();
        if (picks.Count == 0)
        {
            return;
        }

        var result = _doc.Inventory.Add(picks, AddCount);
        foreach (var pick in _catalog.Where(p => p.IsChecked))
        {
            pick.IsChecked = false;
        }

        Rebuild();
        var sections = new List<DialogSection>
        {
            new(Loc.Text("inv_added", "ADDED x{0} ({1})", AddCount, result.Succeeded.Count), DialogTone.Success,
                [.. result.Succeeded.Select(d => Names.Humanize(d.Asset) + (Names.Tier(d.Asset) is { } t ? $" ({t})" : string.Empty))]),
        };
        if (result.Skipped.Count > 0)
        {
            sections.Add(new DialogSection(Loc.Text("section_skipped", "SKIPPED ({0})", result.Skipped.Count), DialogTone.Warning,
                [.. result.Skipped.Select(s => $"{Names.Humanize(s.Item.Asset)}: {s.Reason}")]));
        }

        _dialogs.Show(Loc.Text("inventory_updated", "Inventory updated"), Loc.Text("inventory_updated_text", "Save to write the new stock to the campaign file."),
            result.Skipped.Count == 0 ? DialogTone.Success : DialogTone.Warning, [.. sections]);
    }

    [RelayCommand]
    private void SetSelectedCount()
    {
        var picks = Items.Where(i => i.IsChecked).ToList();
        if (picks.Count == 0)
        {
            return;
        }

        _doc.Inventory.SetCount(picks.Select(p => p.Item), SetCount);
        foreach (var pick in picks)
        {
            pick.IsChecked = false;
        }

        Rebuild();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var picks = Items.Where(i => i.IsChecked).ToList();
        if (picks.Count == 0
            || !_dialogs.Confirm(Loc.Text("remove_items", "Remove items"), Loc.Text("remove_items_text", "Remove {0} inventory stack(s)?", picks.Count), Loc.Instance["remove"], DialogTone.Error))
        {
            return;
        }

        _doc.Inventory.Remove(picks.Select(p => p.Item));
        Rebuild();
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var row in ItemsView.Cast<InventoryRowViewModel>())
        {
            row.IsChecked = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in Items.Where(r => r.IsChecked))
        {
            row.IsChecked = false;
        }
    }

    private void Rebuild()
    {
        Items.Clear();
        foreach (var item in _doc.Inventory.Items
                     .OrderBy(i => i.IsWeapon ? 0 : i.IsAmmo ? 2 : 1)
                     .ThenBy(i => Names.Humanize(i.Asset), StringComparer.OrdinalIgnoreCase))
        {
            var row = new InventoryRowViewModel(item);
            row.CheckedChanged += (_, _) => OnSelectionChanged();
            // An inline count edit changes the "N units" header too.
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(InventoryRowViewModel.Count))
                {
                    OnPropertyChanged(nameof(TotalCount));
                }
            };
            Items.Add(row);
        }

        OnSelectionChanged();
        OnPropertyChanged(nameof(TotalCount));
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private bool Matches(InventoryRowViewModel row) =>
        Fits(row.Kind, Filter) && Contains($"{row.Asset} {row.Name} {row.TypeLabel}", Search);

    private bool Matches(ItemPickViewModel pick) =>
        Fits(pick.Definition.Kind, CatalogFilter) && Contains(pick.SearchText, CatalogSearch);

    private static bool Fits(ItemKind kind, ItemFilter filter) => filter switch
    {
        ItemFilter.Weapons => kind == ItemKind.Weapon,
        ItemFilter.Equipment => kind == ItemKind.Equipment,
        ItemFilter.Ammo => kind == ItemKind.Ammo,
        _ => true,
    };

    private static bool Contains(string text, string search) =>
        search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
