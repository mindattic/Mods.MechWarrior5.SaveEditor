using System.Text.Json;

namespace Mech5.Mercs.SaveEditor.Core.Catalog;

public enum ItemKind
{
    Weapon,
    Equipment,
    Ammo,
}

/// <summary>An inventory item asset, e.g. <c>MediumLaser_Lvl2</c> of type <c>MWTraceWeaponDataAsset</c>.</summary>
public sealed record ItemDefinition(string Asset, string AssetType, ItemKind Kind, HardpointClass Hardpoint = HardpointClass.Unknown)
{
    /// <summary>Weapons go to <c>WeaponInventory</c>; equipment and ammo to <c>EquipmentInventory</c>.</summary>
    public bool IsWeapon => Kind == ItemKind.Weapon;

    public int Tier
    {
        get
        {
            var at = Asset.LastIndexOf("_Lvl", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(Asset.AsSpan(at + 4), out var tier) ? tier : 0;
        }
    }
}

public sealed class ItemCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ItemCatalog(IReadOnlyList<ItemDefinition> items)
    {
        Items = items;
    }

    public IReadOnlyList<ItemDefinition> Items { get; }

    public IEnumerable<ItemDefinition> WeaponsFor(HardpointClass slot) =>
        Items.Where(i => i.Kind == ItemKind.Weapon && Hardpoints.Accepts(slot, i.Hardpoint));

    public ItemDefinition? Find(string asset) =>
        Items.FirstOrDefault(i => string.Equals(i.Asset, asset, StringComparison.OrdinalIgnoreCase));

    public static ItemCatalog LoadBuiltIn()
    {
        using var stream = MechCatalog.OpenResource("items.json");
        var file = JsonSerializer.Deserialize<Dictionary<string, List<ItemDto>>>(stream, JsonOptions)
            ?? throw new InvalidDataException("items.json is empty");
        var items = new List<ItemDefinition>();
        Add("weapons", ItemKind.Weapon);
        Add("equipment", ItemKind.Equipment);
        Add("ammo", ItemKind.Ammo);
        return new ItemCatalog(items);

        void Add(string group, ItemKind kind) =>
            items.AddRange(file[group].Select(i => new ItemDefinition(i.Asset, i.Type, kind, Hardpoints.Parse(i.Hardpoint))));
    }

    private sealed record ItemDto(string Asset, string Type, string? Hardpoint);
}
