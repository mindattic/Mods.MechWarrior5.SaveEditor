using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>Company stock of weapons (<c>WeaponInventory</c>) and equipment/ammo (<c>EquipmentInventory</c>).</summary>
public sealed class InventoryEditor
{
    public const int MaxCount = 9999;

    private readonly StructArrayStorage? _weapons;
    private readonly StructArrayStorage? _equipment;
    private readonly Action _changed;
    private readonly List<InventoryItem> _items = [];

    internal InventoryEditor(Mw5SaveFile file, Action changed)
    {
        _changed = changed;
        var model = file.GetModel("InventoryModel");
        _weapons = FindItems(model, "WeaponInventory");
        _equipment = FindItems(model, "EquipmentInventory");
        Reload();
    }

    public IReadOnlyList<InventoryItem> Items => _items;

    public bool CanAddWeapons => _weapons is not null;

    public bool CanAddEquipment => _equipment is not null;

    /// <summary>Adds <paramref name="count"/> of each item, stacking onto a healthy stack when one exists.</summary>
    public BatchResult<ItemDefinition> Add(IEnumerable<ItemDefinition> definitions, int count)
    {
        var result = new BatchResult<ItemDefinition>();
        count = Math.Clamp(count, 1, MaxCount);
        foreach (var definition in definitions)
        {
            var storage = definition.IsWeapon ? _weapons : _equipment;
            if (storage is null)
            {
                result.Skip(definition, "this save has no inventory list for that item type yet");
                continue;
            }

            var stack = _items.FirstOrDefault(i =>
                i.IsHealthy && i.IsWeapon == definition.IsWeapon
                && string.Equals(i.Asset, definition.Asset, StringComparison.OrdinalIgnoreCase));
            if (stack is not null)
            {
                stack.SetCountSilently(Math.Min(MaxCount, stack.Count + count));
            }
            else
            {
                storage.Elements.Add(NewStack(definition, count));
            }

            result.Succeed(definition);
        }

        if (result.Succeeded.Count > 0)
        {
            Reload();
            _changed();
        }

        return result;
    }

    public void SetCount(IEnumerable<InventoryItem> items, int count)
    {
        var clamped = Math.Clamp(count, 1, MaxCount);
        var any = false;
        foreach (var item in items)
        {
            any |= item.SetCountSilently(clamped);
        }

        if (any)
        {
            _changed();
        }
    }

    public int Remove(IEnumerable<InventoryItem> items)
    {
        var removed = 0;
        foreach (var item in items.ToList())
        {
            var storage = item.IsWeapon ? _weapons : _equipment;
            if (storage?.Elements.Remove(item.Element) == true)
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            Reload();
            _changed();
        }

        return removed;
    }

    private static PropertyList NewStack(ItemDefinition definition, int count)
    {
        var data = definition.IsWeapon
            ? Ue.Struct("ItemData", "WeaponData",
                Ue.AssetId("WeaponId", "WeaponDataAssetId", definition.AssetType, definition.Asset),
                Ue.Healthy())
            : Ue.Struct("ItemData", "EquipmentData",
                Ue.AssetId("EquipmentId", "EquipmentDataAssetId", definition.AssetType, definition.Asset),
                Ue.Healthy());
        return [Ue.ItemId("ItemId", Guid.NewGuid()), data, new IntProperty("Count", count)];
    }

    private static StructArrayStorage? FindItems(PropertyList model, string inventory) =>
        (model.Find<StructProperty>(inventory)?.Value as PropertyList)
            ?.Find<ArrayProperty>("Items")?.Storage as StructArrayStorage;

    private void Reload()
    {
        _items.Clear();
        if (_weapons is not null)
        {
            _items.AddRange(_weapons.PropertyElements.Select(e => new InventoryItem(e, isWeapon: true, _changed)));
        }

        if (_equipment is not null)
        {
            _items.AddRange(_equipment.PropertyElements.Select(e => new InventoryItem(e, isWeapon: false, _changed)));
        }
    }
}

public sealed class InventoryItem
{
    private readonly Action _changed;

    internal InventoryItem(PropertyList element, bool isWeapon, Action changed)
    {
        Element = element;
        IsWeapon = isWeapon;
        _changed = changed;
        var data = element.Find<StructProperty>("ItemData")?.Value as PropertyList;
        var id = data?.Find<StructProperty>(isWeapon ? "WeaponId" : "EquipmentId")?.Value as PropertyList;
        Asset = Ue.AssetName(id) ?? "Unknown";
        AssetType = Ue.AssetType(id) ?? string.Empty;
        var health = data?.Find<EnumProperty>("HealthState");
        Health = health is null ? "Healthy" : Ue.EnumValue(health.Value);
    }

    public bool IsWeapon { get; }

    public string Asset { get; }

    public string AssetType { get; }

    public string Health { get; }

    public bool IsHealthy => Health == "Healthy";

    public bool IsAmmo => AssetType == "MWAmmoDataAsset";

    public int Count
    {
        get => Element.Find<IntProperty>("Count")?.Value ?? 0;
        set
        {
            if (SetCountSilently(Math.Clamp(value, 1, InventoryEditor.MaxCount)))
            {
                _changed();
            }
        }
    }

    internal PropertyList Element { get; }

    internal bool SetCountSilently(int count)
    {
        var property = Element.Find<IntProperty>("Count");
        if (property?.Value == count)
        {
            return false;
        }

        if (property is null)
        {
            Element.Add(new IntProperty("Count", count));
        }
        else
        {
            property.Value = count;
        }

        return true;
    }
}
