using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;

using Mech5.SaveEditor.App.Localization;

namespace Mech5.SaveEditor.App.ViewModels;

/// <summary>A pick-list entry; <see cref="Item"/> is null for "empty".</summary>
public sealed record ItemOption(ItemDefinition? Item, string Label)
{
    public static readonly ItemOption Empty = new(null, "- empty -");

    public static ItemOption Of(ItemDefinition item) =>
        new(item, Names.Humanize(item.Asset) + (Names.Tier(item.Asset) is { } tier ? $"  [{tier}]" : string.Empty));

    public override string ToString() => Label;
}

public sealed partial class HardpointRowViewModel : ObservableObject
{
    [ObservableProperty]
    private ItemOption _selected;

    public HardpointRowViewModel(string slot, IReadOnlyList<ItemOption> options, ItemOption selected, bool[] groups)
    {
        Slot = slot;
        Options = options;
        _selected = selected;
        Groups = groups;
        Class = Hardpoints.ClassOf(slot);
    }

    public string Slot { get; }

    public HardpointClass Class { get; }

    public string Location => Hardpoints.Location(Slot);

    public string ClassLabel => Class switch
    {
        HardpointClass.Energy => "ENERGY",
        HardpointClass.Ballistic => "BALLISTIC",
        HardpointClass.Missile => "MISSILE",
        HardpointClass.Ams => "AMS",
        HardpointClass.Omni => "OMNI",
        HardpointClass.Melee => "MELEE",
        _ => "HARDPOINT",
    };

    public IReadOnlyList<ItemOption> Options { get; }

    public bool[] Groups { get; }
}

public sealed partial class EquipmentRowViewModel : ObservableObject
{
    [ObservableProperty]
    private ItemOption _selected;

    public EquipmentRowViewModel(string part, int slotId, string slotType, IReadOnlyList<ItemOption> options, ItemOption selected)
    {
        Part = part;
        SlotId = slotId;
        SlotType = slotType;
        Options = options;
        _selected = selected;
    }

    public string Part { get; }

    public string PartLabel => Names.Humanize(Part.Replace("EMechParts::", string.Empty, StringComparison.Ordinal));

    public int SlotId { get; }

    public string SlotType { get; }

    public string SlotLabel => EquipmentSlots.Label(SlotType);

    public IReadOnlyList<ItemOption> Options { get; }
}

public sealed partial class ArmorRowViewModel : ObservableObject
{
    [ObservableProperty]
    private int _front;

    [ObservableProperty]
    private int _rear;

    public ArmorRowViewModel(string part, string? rearPart, int max, int front, int rear)
    {
        Part = part;
        RearPart = rearPart;
        Max = max;
        _front = front;
        _rear = rear;
    }

    public string Part { get; }

    public string Label => Names.Humanize(Part);

    public string? RearPart { get; }

    public bool HasRear => RearPart is not null;

    /// <summary>Front and rear share one maximum on torso locations.</summary>
    public int Max { get; }

    public bool IsOverMax => Max > 0 && Front + Rear > Max;

    partial void OnFrontChanged(int value) => OnPropertyChanged(nameof(IsOverMax));

    partial void OnRearChanged(int value) => OnPropertyChanged(nameof(IsOverMax));
}

public enum LoadoutTab
{
    Weapons,
    Equipment,
    Armor,
    Traits,
}

public sealed partial class LoadoutEditorViewModel : ObservableObject
{
    private static readonly Dictionary<string, string> RearOf = new(StringComparer.Ordinal)
    {
        ["CenterTorso"] = "CenterTorsoRear",
        ["LeftTorso"] = "LeftTorsoRear",
        ["RightTorso"] = "RightTorsoRear",
    };

    private readonly HangarEditor _hangar;
    private readonly ItemCatalog _items;
    private readonly IDialogService _dialogs;
    private readonly StockLoadout? _stock;
    private readonly PilotCatalog _traits;
    private Dictionary<string, float> _structure = [];

    [ObservableProperty]
    private LoadoutTab _tab = LoadoutTab.Weapons;

    [ObservableProperty]
    private TraitDefinition? _traitToAdd;

    public LoadoutEditorViewModel(MechRecord mech, HangarEditor hangar, ItemCatalog items, PilotCatalog traits, IDialogService dialogs)
    {
        Mech = mech;
        _traits = traits;
        foreach (var trait in HangarEditor.MechTraits(mech))
        {
            Traits.Add(new TraitRow(trait, traits.TraitLabel(trait)));
        }

        _hangar = hangar;
        _items = items;
        _dialogs = dialogs;
        _stock = hangar.StockFor(mech);
        Load(HangarEditor.ReadLoadout(mech));
    }

    public event EventHandler<bool>? CloseRequested;

    public MechRecord Mech { get; }

    public string Title => $"{Mech.Code}  //  {Mech.Variant?.HeroName ?? Mech.Variant?.ChassisName ?? Mech.Asset}";

    public string? SarnaTitle => Mech.Variant?.SarnaTitle;

    public string Initials => Mech.Code.Length >= 2 ? Mech.Code[..2] : Mech.Code;

    public ObservableCollection<HardpointRowViewModel> Weapons { get; } = [];

    public ObservableCollection<EquipmentRowViewModel> Equipment { get; } = [];

    public ObservableCollection<ArmorRowViewModel> Armor { get; } = [];

    public bool HasStock => _stock is not null;

    public ObservableCollection<TraitRow> Traits { get; } = [];

    public IReadOnlyList<TraitDefinition> TraitChoices => _traits.MechTraits;

    [RelayCommand]
    private void AddTrait()
    {
        if (TraitToAdd is { } trait && !Traits.Any(t => string.Equals(t.Asset, trait.Asset, StringComparison.OrdinalIgnoreCase)))
        {
            Traits.Add(new TraitRow(trait.Asset, trait.Label));
        }
    }

    [RelayCommand]
    private void RemoveTrait(TraitRow? trait)
    {
        if (trait is not null)
        {
            Traits.Remove(trait);
        }
    }

    [RelayCommand]
    private void ShowTab(LoadoutTab tab) => Tab = tab;

    [RelayCommand]
    private void MaxArmor()
    {
        foreach (var row in Armor.Where(r => r.Max > 0))
        {
            row.Front = row.Max - row.Rear;
        }
    }

    [RelayCommand]
    private void ResetToStock()
    {
        if (_stock is not null)
        {
            Load(_stock);
        }
    }

    [RelayCommand]
    private void Export()
    {
        var name = $"{Mech.Code}{LoadoutFile.Extension}";
        if (_dialogs.PickFileToSave("Export loadout", name, $"MW5 loadout (*{LoadoutFile.Extension})|*{LoadoutFile.Extension}") is { } path)
        {
            LoadoutFile.Create(Mech.Asset, Build()).Save(path);
        }
    }

    [RelayCommand]
    private void Import()
    {
        if (_dialogs.PickFileToOpen("Import loadout", $"MW5 loadout (*{LoadoutFile.Extension})|*{LoadoutFile.Extension}") is not { } path)
        {
            return;
        }

        LoadoutFile file;
        try
        {
            file = LoadoutFile.Load(path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _dialogs.Show(Loc.Text("cant_import_loadout", "Can't import loadout"), e.Message, DialogTone.Error);
            return;
        }

        var slots = Weapons.Select(w => w.Slot).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dropped = file.Loadout.Weapons
            .Where(w => w.IsInstalledWeapon && !slots.Contains(w.Slot))
            .Select(w => $"{Names.Humanize(w.Name)} ({Hardpoints.Location(w.Slot)}): this mech has no such hardpoint")
            .ToList();
        Load(file.Loadout with
        {
            Weapons = [.. file.Loadout.Weapons.Where(w => slots.Contains(w.Slot))],
            Structure = _structure,
        });

        if (dropped.Count > 0 || !string.Equals(file.Variant, Mech.Asset, StringComparison.OrdinalIgnoreCase))
        {
            _dialogs.Show(
                Loc.Text("loadout_imported", "Loadout imported"),
                Loc.Text("loadout_imported_text", "Built for {0}. Review it, then Apply.", file.Variant.Replace("_MDA", string.Empty, StringComparison.Ordinal)),
                DialogTone.Warning,
                dropped.Count > 0 ? [new DialogSection(Loc.Text("not_fitted", "NOT FITTED ({0})", dropped.Count), DialogTone.Warning, dropped)] : []);
        }
    }

    [RelayCommand]
    private void Apply()
    {
        var over = Armor.Where(a => a.IsOverMax).Select(a => $"{a.Label}: {a.Front + a.Rear} / {a.Max}").ToList();
        if (over.Count > 0)
        {
            _dialogs.Show(Loc.Text("armor_over", "Armor over the limit"), Loc.Text("armor_over_text", "Lower these locations before applying."), DialogTone.Error,
                new DialogSection(Loc.Text("over_maximum", "OVER MAXIMUM"), DialogTone.Error, over));
            return;
        }

        _hangar.ApplyLoadout(Mech, Build());
        _hangar.SetMechTraits(Mech, Traits.Select(t => t.Asset));
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private void Load(StockLoadout loadout)
    {
        _structure = loadout.Structure.Count > 0 ? new Dictionary<string, float>(loadout.Structure) : _structure;

        Weapons.Clear();
        var installed = loadout.Weapons.Where(w => w.IsInstalledWeapon)
            .ToDictionary(w => w.Slot, StringComparer.OrdinalIgnoreCase);
        var groups = loadout.Groups.ToDictionary(g => g.Slot, g => g.G, StringComparer.OrdinalIgnoreCase);
        foreach (var slot in _hangar.HardpointsFor(Mech).Union(installed.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var options = WeaponOptions(Hardpoints.ClassOf(slot));
            var current = installed.TryGetValue(slot, out var w) ? Find(options, w.Name, w.Type, ItemKind.Weapon) : ItemOption.Empty;
            var g = groups.TryGetValue(slot, out var flags) && flags.Length == 6 ? (bool[])flags.Clone() : new bool[6];
            Weapons.Add(new HardpointRowViewModel(slot, options, current, g));
        }

        Equipment.Clear();
        foreach (var part in loadout.Equipment)
        {
            foreach (var item in part.Items.OrderBy(i => i.SlotId))
            {
                var options = EquipmentOptions(item.SlotType);
                Equipment.Add(new EquipmentRowViewModel(part.Part, item.SlotId, item.SlotType, options, Find(options, item.Name, item.Type, ItemKind.Equipment)));
            }
        }

        Armor.Clear();
        foreach (var part in MechLoadoutBuilder.BodyParts)
        {
            var rear = RearOf.GetValueOrDefault(part);
            Armor.Add(new ArmorRowViewModel(
                part,
                rear,
                (int)(_stock?.MaxArmor.GetValueOrDefault(part) ?? 0),
                (int)loadout.Armor.GetValueOrDefault(part),
                rear is null ? 0 : (int)loadout.RearArmor.GetValueOrDefault(rear)));
        }
    }

    private StockLoadout Build()
    {
        var weapons = Weapons
            .Where(w => w.Selected.Item is not null)
            .Select(w => new StockWeapon(w.Slot, w.Selected.Item!.AssetType, w.Selected.Item.Asset))
            .ToList();

        // Weapon groups cover fitted weapons plus melee mounts; a newly fitted weapon fires on group 1.
        var groups = Weapons
            .Where(w => w.Selected.Item is not null || w.Class == HardpointClass.Melee)
            .Select(w => new StockWeaponGroup(w.Slot, w.Groups.Any(g => g) ? w.Groups : [true, false, false, false, false, false]))
            .ToList();

        var equipment = Equipment
            .Where(e => e.Selected.Item is not null)
            .GroupBy(e => e.Part)
            .Select(g => new StockPartEquipment(g.Key, [.. g.Select(e =>
                new StockEquipment(e.Selected.Item!.AssetType, e.Selected.Item.Asset, e.SlotId, e.SlotType))]))
            .ToList();

        return new StockLoadout(
            Armor.ToDictionary(a => a.Part, a => (float)a.Front, StringComparer.Ordinal),
            [],
            Armor.Where(a => a.RearPart is not null).ToDictionary(a => a.RearPart!, a => (float)a.Rear, StringComparer.Ordinal),
            _structure,
            weapons,
            groups,
            equipment);
    }

    private List<ItemOption> WeaponOptions(HardpointClass slot) =>
        [ItemOption.Empty, .. _items.WeaponsFor(slot).OrderBy(i => Names.Humanize(i.Asset), StringComparer.OrdinalIgnoreCase).Select(ItemOption.Of)];

    private List<ItemOption> EquipmentOptions(string slotType) =>
        [ItemOption.Empty, .. _items.Items.Where(i => EquipmentSlots.Accepts(slotType, i))
            .OrderBy(i => Names.Humanize(i.Asset), StringComparer.OrdinalIgnoreCase).Select(ItemOption.Of)];

    /// <summary>
    /// Finds the fitted item among the options. Items the catalog doesn't list (modded or new) get an
    /// option of their own, so applying an unrelated change never strips them.
    /// </summary>
    private static ItemOption Find(List<ItemOption> options, string asset, string assetType, ItemKind kind)
    {
        var match = options.FirstOrDefault(o => string.Equals(o.Item?.Asset, asset, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        var unlisted = new ItemOption(new ItemDefinition(asset, assetType, kind), Names.Humanize(asset) + "  (unlisted)");
        options.Insert(1, unlisted);
        return unlisted;
    }
}
