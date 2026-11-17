using System.Text.RegularExpressions;

namespace MechWarrior5.Clans.SaveEditor.Core.Catalog;

public sealed record Chassis(string Code, string Name, int Tons, string WeightClass);

/// <summary>
/// Display data for MW5 Clans assets: chassis by mech code (from <c>VPR_MIA</c>, <c>HBK-IIC-A_MIA</c>, ...)
/// and readable names for item asset ids. Codes not listed fall back to the code itself.
/// </summary>
public static partial class ClansCatalog
{
    // Every Clan chassis the game sells (30; tools/clans/classify_mia.py). Tonnage and weight class are checked
    // against the game's own data by tools/clans/read_tonnage.py (30/30 match, 2026-11-10).
    private static readonly Chassis[] KnownChassis =
    [
        new("ACH", "Arctic Cheetah", 30, "Light"),
        new("ADR", "Adder", 35, "Light"),
        new("BANE", "Bane", 100, "Assault"),
        new("DWF", "Dire Wolf", 100, "Assault"),
        new("EBJ", "Ebon Jaguar", 65, "Heavy"),
        new("EXE", "Executioner", 95, "Assault"),
        new("FMT", "Fire Moth", 20, "Light"),
        new("GAR", "Gargoyle", 80, "Assault"),
        new("HBK-IIC", "Hunchback IIC", 50, "Medium"),
        new("HBR", "Hellbringer", 65, "Heavy"),
        new("HMN", "Huntsman", 50, "Medium"),
        new("IFR", "Ice Ferret", 45, "Medium"),
        new("INC", "Incubus", 30, "Light"),
        new("JR7IIC", "Jenner IIC", 35, "Light"),
        new("KDK", "Kodiak", 100, "Assault"),
        new("KFX", "Kit Fox", 30, "Light"),
        new("LBK", "Linebacker", 65, "Heavy"),
        new("MDD", "Mad Dog", 60, "Heavy"),
        new("MLX", "Mist Lynx", 25, "Light"),
        new("NGA", "Naga", 80, "Assault"),
        new("NTG", "Night Gyr", 75, "Heavy"),
        new("NVA", "Nova", 50, "Medium"),
        new("RFLIIC", "Rifleman IIC", 65, "Heavy"),
        new("SCR", "Stormcrow", 55, "Medium"),
        new("SHC", "Shadow Cat", 45, "Medium"),
        new("SHD-IIC", "Shadow Hawk IIC", 45, "Medium"),
        new("SMN", "Summoner", 70, "Heavy"),
        new("TBR", "Timber Wolf", 75, "Heavy"),
        new("VPR", "Viper", 40, "Medium"),
        new("WHK", "Warhawk", 85, "Assault"),
    ];

    private static readonly Dictionary<string, string> Words = new(StringComparer.Ordinal)
    {
        ["ER"] = "ER", ["PPC"] = "PPC", ["LRM"] = "LRM", ["SRM"] = "SRM", ["ECM"] = "ECM", ["BAP"] = "BAP",
        ["TAG"] = "TAG", ["AMS"] = "AMS", ["LBX"] = "LB-X", ["UAC"] = "UAC", ["NARC"] = "NARC", ["STD"] = "Standard", ["XL"] = "XL",
    };

    /// <summary>The chassis of a mech item asset; unknown codes get a placeholder entry.</summary>
    public static Chassis ChassisOf(string mechAsset)
    {
        var variant = VariantCode(mechAsset);
        return KnownChassis
            .Where(c => variant.Equals(c.Code, StringComparison.Ordinal)
                || variant.StartsWith(c.Code + "-", StringComparison.Ordinal)
                || (variant.StartsWith(c.Code, StringComparison.Ordinal) && c.Code.Length >= 4))
            .OrderByDescending(c => c.Code.Length)
            .FirstOrDefault()
            ?? new Chassis(variant, variant, 0, "Unknown");
    }

    /// <summary><c>HBK-IIC-A_MIA</c> becomes <c>HBK-IIC-A</c>.</summary>
    public static string VariantCode(string mechAsset) =>
        mechAsset.EndsWith("_MIA", StringComparison.Ordinal) ? mechAsset[..^4] : mechAsset;

    /// <summary>The chassis for an <c>MXP_&lt;Chassis&gt;</c> mech-experience currency, e.g. MXP_MadDog.</summary>
    public static string MechXpLabel(string asset) =>
        asset.StartsWith("MXP_", StringComparison.Ordinal) ? SplitCamel(asset[4..]).Replace("Mech", string.Empty, StringComparison.Ordinal).Trim() : asset;

    /// <summary>A readable label for an item asset: <c>Clan_ERMediumLaser</c> becomes "ER Medium Laser".</summary>
    public static string ItemLabel(string asset)
    {
        var text = asset;
        foreach (var prefix in new[] { "Clan_", "IS_" })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
            }
        }

        if (EngineRegex().Match(text) is { Success: true } engine)
        {
            return $"{Words[engine.Groups[1].Value]} Engine {engine.Groups[2].Value}";
        }

        text = text.Replace("_EquipmentItem", string.Empty, StringComparison.Ordinal)
            .Replace("_Equipment", string.Empty, StringComparison.Ordinal)
            .Replace("_CurrencyItem", string.Empty, StringComparison.Ordinal)
            .Replace("_CrewItem", string.Empty, StringComparison.Ordinal);
        var half = text.StartsWith("Half_", StringComparison.Ordinal) || text.EndsWith("HalfAmmo", StringComparison.Ordinal);
        text = text.Replace("Half_", string.Empty, StringComparison.Ordinal).Replace("HalfAmmo", "Ammo", StringComparison.Ordinal);
        var parts = text.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Words.TryGetValue(p, out var word) ? word : SplitCamel(p));
        var label = string.Join(" ", parts).Replace("Autocannon", "AC", StringComparison.Ordinal);
        return half ? label + " (half ton)" : label;
    }

    private static string SplitCamel(string text)
    {
        var spaced = CamelRegex().Replace(text, " ");
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => Words.TryGetValue(w, out var word) ? word : w));
    }

    [GeneratedRegex(@"^(STD|XL)Engine_(\d+)")]
    private static partial Regex EngineRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=\d)")]
    private static partial Regex CamelRegex();
}
