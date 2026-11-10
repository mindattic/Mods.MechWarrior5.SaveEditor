namespace Mech5.Mercs.SaveEditor.Core.Catalog;

/// <summary>Which equipment fits which slot type (<c>General_SlotType</c>, <c>JumpJetClass4_SlotType</c>, ...).</summary>
public static class EquipmentSlots
{
    private static readonly HashSet<string> GeneralTypes = new(StringComparer.Ordinal)
    {
        "MWHeatSinkDataAsset",
        "MWAmmoDataAsset",
        "MWBAPDataAsset",
        "MWTargetingComputerDataAsset",
    };

    public static bool Accepts(string slotType, ItemDefinition item)
    {
        if (item.Kind == ItemKind.Weapon)
        {
            return false;
        }

        if (slotType.Equals("General_SlotType", StringComparison.OrdinalIgnoreCase))
        {
            return GeneralTypes.Contains(item.AssetType);
        }

        if (slotType.Equals("EngineHeatsink_SlotType", StringComparison.OrdinalIgnoreCase))
        {
            return item.AssetType == "MWHeatSinkDataAsset";
        }

        if (slotType.StartsWith("JumpJetClass", StringComparison.OrdinalIgnoreCase))
        {
            var cls = slotType["JumpJetClass".Length..].Split('_')[0];
            return item.AssetType == "MWJumpJetDataAsset"
                && item.Asset.Contains($"Class{cls}_", StringComparison.OrdinalIgnoreCase);
        }

        if (slotType.StartsWith("MASC_", StringComparison.OrdinalIgnoreCase))
        {
            var mark = slotType.Split('_')[1];
            return item.AssetType == "MWMASCDataAsset" && item.Asset.EndsWith("_" + mark, StringComparison.OrdinalIgnoreCase);
        }

        return slotType switch
        {
            "ECM_SlotType" => item.AssetType == "MWECMDataAsset",
            "BAP_SlotType" => item.AssetType == "MWBAPDataAsset",
            _ => false,
        };
    }

    /// <summary>A readable slot label, e.g. <c>JumpJetClass4_SlotType</c> → <c>Jump Jet IV</c>.</summary>
    public static string Label(string slotType)
    {
        if (slotType.StartsWith("JumpJetClass", StringComparison.OrdinalIgnoreCase))
        {
            return "Jump Jet " + slotType["JumpJetClass".Length..].Split('_')[0];
        }

        return slotType.Replace("_SlotType", string.Empty, StringComparison.Ordinal) switch
        {
            "General" => "General",
            "EngineHeatsink" => "Engine Heat Sink",
            var other => other.Replace('_', ' '),
        };
    }
}
