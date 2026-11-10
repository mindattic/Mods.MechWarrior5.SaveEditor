using System.Globalization;
using System.Text.RegularExpressions;
using Mech5.Mercs.SaveEditor.App.Localization;
using Mech5.Mercs.SaveEditor.Core.Catalog;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

/// <summary>Turns asset ids like <c>LBXAutocannon10_Lvl2</c> into readable labels.</summary>
public static partial class Names
{
    public static string Humanize(string asset)
    {
        var name = TierSuffix().Replace(asset, string.Empty);
        name = name.Replace("_AmmoEquipment", " Ammo", StringComparison.Ordinal)
            .Replace("_Equipment", string.Empty, StringComparison.Ordinal)
            .Replace('_', ' ');
        name = WordBreak().Replace(name, " ");
        return MultiSpace().Replace(name, " ").Trim();
    }

    /// <summary>Localized weight class in title case, e.g. "Leicht".</summary>
    public static string WeightClass(WeightClass weight) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Loc.Instance[weight.ToString().ToLowerInvariant()].ToLower(CultureInfo.CurrentCulture));

    public static string? Tier(string asset)
    {
        var match = TierSuffix().Match(asset);
        return match.Success ? $"T{match.Groups[1].Value}" : null;
    }

    public static string AssetTypeLabel(string assetType) => assetType switch
    {
        "MWAmmoDataAsset" => "Ammo",
        "MWHeatSinkDataAsset" => "Heat Sink",
        "MWJumpJetDataAsset" => "Jump Jet",
        "MWAMSWeaponDataAsset" => "AMS",
        "MWECMDataAsset" => "ECM",
        "MWBAPDataAsset" => "Probe",
        "MWMissileWeaponDataAsset" => "Missile",
        "MWTraceWeaponDataAsset" => "Energy",
        "MWBeamWeaponDataAsset" => "Energy",
        "MWProjectileWeaponDataAsset" => "Ballistic",
        _ => Humanize(assetType.Replace("MW", string.Empty, StringComparison.Ordinal).Replace("DataAsset", string.Empty, StringComparison.Ordinal)),
    };

    [GeneratedRegex(@"_Lvl(\d+)$")]
    private static partial Regex TierSuffix();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z][a-z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBreak();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
