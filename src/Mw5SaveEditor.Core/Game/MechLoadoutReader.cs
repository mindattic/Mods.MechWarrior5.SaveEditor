using Mw5SaveEditor.Core.Catalog;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Game;

/// <summary>Reads a saved <c>MechLoadout</c> back into the same shape as a stock loadout.</summary>
public static class MechLoadoutReader
{
    public static StockLoadout Read(PropertyList loadout)
    {
        var weapons = Structs(loadout, "InstalledWeapons")
            .Select(w =>
            {
                var id = (w.Find<StructProperty>("WeaponData")?.Value as PropertyList)?.Find<StructProperty>("WeaponId")?.Value as PropertyList;
                return new StockWeapon(Name(w, "HardpointSlotID"), Ue.AssetType(id) ?? "None", Ue.AssetName(id) ?? "None");
            })
            .ToList();

        var groups = Structs(loadout.Find<StructProperty>("WeaponGroupInfo")?.Value as PropertyList, "WeaponGroups")
            .Select(g => new StockWeaponGroup(
                Name(g, "HardpointSlotID"),
                [.. Enumerable.Range(1, 6).Select(i => g.Find<BoolProperty>($"bWeaponGroup{i}")?.Value ?? false)]))
            .ToList();

        var equipment = Structs(loadout.Find<StructProperty>("Equipment")?.Value as PropertyList, "MechPartEquipment")
            .Select(p => new StockPartEquipment(
                p.Find<EnumProperty>("MechPart")?.Value.Value ?? "EMechParts::CenterTorso",
                [.. Structs(p, "SlottedEquipment").Select(s =>
                {
                    var data = s.Find<StructProperty>("EquipmentData")?.Value as PropertyList;
                    var id = data?.Find<StructProperty>("EquipmentId")?.Value as PropertyList;
                    return new StockEquipment(
                        Ue.AssetType(id) ?? "None",
                        Ue.AssetName(id) ?? "None",
                        s.Find<IntProperty>("SlotId")?.Value ?? 0,
                        Ue.AssetName(s.Find<StructProperty>("SlotTypeAssetId")?.Value as PropertyList) ?? "General_SlotType");
                })]))
            .ToList();

        return new StockLoadout(
            Parts(loadout, "InstalledArmor", MechLoadoutBuilder.BodyParts),
            [],
            Parts(loadout, "InstalledRearArmor", MechLoadoutBuilder.RearParts),
            Parts(loadout, "CurrentStructure", MechLoadoutBuilder.BodyParts),
            weapons,
            groups,
            equipment);
    }

    private static Dictionary<string, float> Parts(PropertyList loadout, string field, IReadOnlyList<string> names)
    {
        var parts = loadout.Find<StructProperty>(field)?.Value as PropertyList;
        return names.ToDictionary(n => n, n => parts?.Find<FloatProperty>(n)?.Value ?? 0f, StringComparer.Ordinal);
    }

    private static IEnumerable<PropertyList> Structs(PropertyList? owner, string array) =>
        (owner?.Find<ArrayProperty>(array)?.Storage as StructArrayStorage)?.PropertyElements ?? [];

    private static string Name(PropertyList element, string field) =>
        element.Find<NameProperty>(field)?.Value.Value ?? string.Empty;
}
