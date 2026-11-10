using Mw5SaveEditor.Core.Catalog;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Game;

/// <summary>
/// Builds <c>MechLoadout</c> structs from a stock loadout, field for field in the layout the game
/// itself writes for hangar and market 'Mechs.
/// </summary>
public static class MechLoadoutBuilder
{
    public static readonly IReadOnlyList<string> BodyParts =
        ["Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg"];

    public static readonly IReadOnlyList<string> RearParts = ["CenterTorsoRear", "LeftTorsoRear", "RightTorsoRear"];

    /// <summary>Loadout fields a stock reset replaces; identity fields (id, variant, skin, name) are kept.</summary>
    public static readonly IReadOnlyList<string> StockFields =
    [
        "CurrentStructure", "CurrentArmor", "CurrentRearArmor", "InstalledArmor", "InstalledRearArmor",
        "InstalledWeapons", "WeaponGroupInfo", "Equipment",
    ];

    /// <summary>The serialized blob of a cold-storage mech: loadout type plus the market item wrapper.</summary>
    public static PropertyList BuildColdStorageBlob(string mechAsset, string skinAsset, StockLoadout stock, Guid itemId) =>
    [
        Ue.Enum("MechLoadoutType", "EMechLoadoutType", "ColdStorageLoadout"),
        Ue.Struct("MarketItemMech", "MarketItemMech",
            Ue.Struct("Item", "ItemMech",
                Ue.ItemId("ItemId", itemId),
                new StructProperty("ItemData", "MechLoadout", BuildLoadout(mechAsset, skinAsset, stock)))),
    ];

    public static PropertyList BuildLoadout(string mechAsset, string skinAsset, StockLoadout stock)
    {
        PropertyList loadout =
        [
            Ue.AssetId("MechDataAssetId", "MechDataAssetId", "MWMechDataAsset", mechAsset),
            Ue.Struct("SkinCustomization", "UnitSkinCustomization",
                Ue.AssetId("MechSkinAssetId", "UnitSkinAssetId", "MWUnitSkinAsset", skinAsset),
                new NameProperty("CustomMechSkinId", skinAsset)),
        ];
        foreach (var field in BuildStockFields(stock))
        {
            loadout.Add(field);
        }

        return loadout;
    }

    public static IEnumerable<UProperty> BuildStockFields(StockLoadout stock)
    {
        yield return Parts("CurrentStructure", stock.Structure);
        yield return Parts("CurrentArmor", stock.Armor);
        yield return Rear("CurrentRearArmor", stock.RearArmor);
        yield return Parts("InstalledArmor", stock.Armor);
        yield return Rear("InstalledRearArmor", stock.RearArmor);
        yield return Ue.StructArray("InstalledWeapons", "MechLoadoutWeapons",
            stock.Weapons.Where(w => w.IsInstalledWeapon).Select(Weapon));
        yield return Ue.Struct("WeaponGroupInfo", "MechLoadoutWeaponGroupInfo",
            Ue.StructArray("WeaponGroups", "MechLoadoutWeaponGroups", stock.Groups.Select(Group)));
        yield return Ue.Struct("Equipment", "MechLoadoutEquipment",
            Ue.StructArray("MechPartEquipment", "MechLoadoutPartEquipment", stock.Equipment.Select(Part)));
    }

    public static StructProperty Parts(string name, IReadOnlyDictionary<string, float> values) =>
        Ue.Struct(name, "MechPartsHealth",
            [.. BodyParts.Select(p => new FloatProperty(p, values.GetValueOrDefault(p)))]);

    public static StructProperty Rear(string name, IReadOnlyDictionary<string, float> values) =>
        Ue.Struct(name, "MechRearArmorValues",
            [.. RearParts.Select(p => new FloatProperty(p, values.GetValueOrDefault(p)))]);

    private static PropertyList Weapon(StockWeapon weapon) =>
    [
        new NameProperty("HardpointSlotID", weapon.Slot),
        Ue.Struct("WeaponData", "WeaponData",
            Ue.AssetId("WeaponId", "WeaponDataAssetId", weapon.Type, weapon.Name),
            Ue.Healthy()),
    ];

    private static PropertyList Group(StockWeaponGroup group)
    {
        PropertyList element = [new NameProperty("HardpointSlotID", group.Slot)];
        for (var i = 0; i < 6; i++)
        {
            element.Add(new BoolProperty($"bWeaponGroup{i + 1}", i < group.G.Length && group.G[i]));
        }

        return element;
    }

    private static PropertyList Part(StockPartEquipment part) =>
    [
        new EnumProperty("MechPart", "EMechParts", part.Part),
        Ue.StructArray("SlottedEquipment", "MechLoadoutSlottedEquipment", part.Items.Select(Slot)),
    ];

    private static PropertyList Slot(StockEquipment item) =>
    [
        new IntProperty("SlotId", item.SlotId),
        Ue.AssetId("SlotTypeAssetId", "SlotTypeDataAssetId", "MWSlotTypeDataAsset", item.SlotType),
        Ue.Struct("EquipmentData", "EquipmentData",
            Ue.AssetId("EquipmentId", "EquipmentDataAssetId", item.Type, item.Name),
            Ue.Healthy()),
    ];
}
