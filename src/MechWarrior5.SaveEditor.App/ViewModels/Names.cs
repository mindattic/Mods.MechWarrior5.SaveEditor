using System.Globalization;
using System.Text.RegularExpressions;
using MechWarrior5.SaveEditor.App.Localization;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.SaveEditor.App.ViewModels;

/// <summary>Turns asset ids like <c>LBXAutocannon10_Lvl2</c> into readable labels.</summary>
public static partial class Names
{
    public static string Humanize(string asset) => AssetLabel.Humanize(asset);

    /// <summary>An item's name: the mod's own name for a mod item, else the readable asset id.</summary>
    public static string Item(ItemDefinition item) => item.Label ?? Humanize(item.Asset);

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
}
