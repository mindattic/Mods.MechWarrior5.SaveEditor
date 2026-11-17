using MechWarrior5.Shared.Assets;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>What the scan reads from enabled mods besides 'Mechs: the items and traits they add, and their skill curve.</summary>
internal sealed class ModContent
{
    public Dictionary<string, ItemDefinition> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, TraitDefinition> PilotTraits { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, TraitDefinition> MechTraits { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The highest level on the base game's pilot skill level curve.</summary>
    public int? BaseSkillCap { get; set; }

    /// <summary>The highest level on an enabled mod's pilot skill level curve, and which mod ships it.</summary>
    public (int Level, string Mod)? ModSkillCap { get; set; }
}

/// <summary>Reads the weapons, equipment, ammo, pilot/'Mech traits and skill curves a mod's pak defines.</summary>
public static partial class GameDataScanner
{
    private const string SkillLevelCurve = "PilotSkillLevelCurve";

    /// <summary>The base game's equipment classes (built-in <c>items.json</c> "equipment").</summary>
    private static readonly HashSet<string> EquipmentClasses = new(StringComparer.Ordinal)
    {
        "MWHeatSinkDataAsset",
        "MWJumpJetDataAsset",
        "MWMASCDataAsset",
        "MWBAPDataAsset",
        "MWECMDataAsset",
        "MWTargetingComputerDataAsset",
        "MWArmorDataAsset",
        "MWStructureDataAsset",
    };

    private static void ScanModContent(PakFile pak, string? modName, IReadOnlySet<string> baseAssets, ModContent content)
    {
        // Any pilot skill level curve counts (a mod may ship its own, e.g. PQPilotSkillLevelCurve, beside the game's).
        foreach (var curve in pak.Files.Where(f => f.EndsWith(SkillLevelCurve + ".uasset", StringComparison.OrdinalIgnoreCase)))
        {
            if (MaxLevel(pak, curve) is not { } top)
            {
                continue;
            }

            if (modName is null)
            {
                content.BaseSkillCap = Math.Max(content.BaseSkillCap ?? 0, top);
            }
            else if (content.ModSkillCap is not { } current || top > current.Level)
            {
                content.ModSkillCap = (top, modName);
            }
        }

        if (modName is null)
        {
            return;
        }

        foreach (var file in pak.Files.Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)))
        {
            var asset = Path.GetFileNameWithoutExtension(file);
            if (baseAssets.Contains(asset)
                || asset.EndsWith("_MDA", StringComparison.OrdinalIgnoreCase)
                || asset.EndsWith("_Loadout", StringComparison.OrdinalIgnoreCase))
            {
                continue; // a base asset the mod overrides (the built-in entry stays), or a 'Mech the scan reads elsewhere
            }

            try
            {
                if (ClassOf(pak, file, asset) is not { } cls || !IsModAssetClass(cls) || Read(pak, file, cls) is not { } props)
                {
                    continue;
                }

                switch (cls)
                {
                    case PilotCatalog.PilotTraitType:
                        content.PilotTraits[asset] = DescribeTrait(asset, props, "PilotTrait", modName);
                        break;
                    case PilotCatalog.MechTraitType:
                        content.MechTraits[asset] = DescribeTrait(asset, props, "MechTrait", modName);
                        break;
                    default:
                        if (DescribeItem(asset, cls, props, modName) is { } item)
                        {
                            content.Items[asset] = item; // a later mod's copy wins, as in the game
                        }

                        break;
                }
            }
            catch (Exception e) when (e is SaveFormatException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                // An asset the reader can't follow is skipped; the rest of the mod still counts.
            }
        }
    }

    /// <summary>The class of the export named like the file (its data asset), from the package header alone.</summary>
    private static string? ClassOf(PakFile pak, string uasset, string asset) =>
        CookedPackage.Parse(pak.Read(uasset), []).Exports
            .FirstOrDefault(e => string.Equals(e.Name, asset, StringComparison.OrdinalIgnoreCase))?.ClassName;

    private static bool IsModAssetClass(string cls) =>
        cls is PilotCatalog.PilotTraitType or PilotCatalog.MechTraitType or "MWAmmoDataAsset"
        || cls.EndsWith("WeaponDataAsset", StringComparison.Ordinal)
        || EquipmentClasses.Contains(cls)
        || (cls.StartsWith("MW", StringComparison.Ordinal) && cls.EndsWith("DataAsset", StringComparison.Ordinal) && cls != "MWMechDataAsset");

    /// <summary>
    /// An inventory item from a mod's data asset, or null when it isn't one a player can own (an AI-only weapon, or a
    /// data asset that lists no equipment slots). The name comes from the asset's own <c>Name</c>.
    /// </summary>
    internal static ItemDefinition? DescribeItem(string asset, string cls, PropertyList props, string modName)
    {
        var label = AssetLabel.GameName(props.Find<TextProperty>("Name")?.DisplayString);
        if (cls.EndsWith("WeaponDataAsset", StringComparison.Ordinal))
        {
            // Turret, vehicle and other AI weapons leave bMechEquippable unset.
            return props.Find<BoolProperty>("bMechEquippable")?.Value == true
                ? new ItemDefinition(asset, cls, ItemKind.Weapon, HardpointOf(props)) { Label = label, Source = modName }
                : null;
        }

        var slots = SlotTypesOf(props);
        if (cls == "MWAmmoDataAsset")
        {
            return new ItemDefinition(asset, cls, ItemKind.Ammo) { Label = label, Source = modName, SlotTypes = slots };
        }

        // Other data asset classes count as equipment only when they list the slots they fit.
        return EquipmentClasses.Contains(cls) || slots.Count > 0
            ? new ItemDefinition(asset, cls, ItemKind.Equipment) { Label = label, Source = modName, SlotTypes = slots }
            : null;
    }

    /// <summary>
    /// The hardpoint a weapon mounts on, from its <c>WeaponType</c> tag (<c>Weapon.Energy.Laser...</c>). Every base
    /// weapon's tag maps to its known hardpoint this way (928 of 928); an unknown root stays Unknown, which keeps the
    /// weapon out of the Mech Lab.
    /// </summary>
    internal static HardpointClass HardpointOf(PropertyList props) =>
        TagOf(props, "WeaponType")?.Split('.') is [_, var root, ..]
            ? root switch
            {
                "Energy" => HardpointClass.Energy,
                "Ballistic" => HardpointClass.Ballistic,
                "Missile" or "ArtemisMissile" or "Artillery" => HardpointClass.Missile,
                "AMS" => HardpointClass.Ams,
                "Melee" => HardpointClass.Melee,
                _ => HardpointClass.Unknown,
            }
            : HardpointClass.Unknown;

    internal static TraitDefinition DescribeTrait(string asset, PropertyList props, string prefix, string modName) =>
        new(asset,
            AssetLabel.GameName(props.Find<TextProperty>(prefix + "Name")?.DisplayString) ?? AssetLabel.HumanizeTrait(asset),
            // A long description kept in a string table reads as blank; the short one is plain text.
            AssetLabel.CleanText(props.Find<TextProperty>(prefix + "LongDescription")?.DisplayString)
            ?? AssetLabel.CleanText(props.Find<TextProperty>(prefix + "ShortDescription")?.DisplayString))
        {
            Source = modName,
        };

    private static string? TagOf(PropertyList props, string name) =>
        ((props.Find<StructProperty>(name)?.Value as PropertyList)?.Find<NameProperty>("TagName"))?.Value.Value;

    private static List<string> SlotTypesOf(PropertyList props) =>
        (props.Find<ArrayProperty>("SupportedSlotTypesAssetId")?.Storage as StructArrayStorage)?.PropertyElements
            .Select(e => ((e.Find<StructProperty>("Id")?.Value as PropertyList)?.Find<NameProperty>("PrimaryAssetName"))?.Value.Value)
            .OfType<string>()
            .ToList() ?? [];

    /// <summary>The highest level on a skill level curve (rounded), or null when unreadable or outside a save's 1..255 byte.</summary>
    private static int? MaxLevel(PakFile pak, string uasset)
    {
        var uexp = uasset[..^".uasset".Length] + ".uexp";
        if (!pak.Contains(uexp))
        {
            return null;
        }

        try
        {
            var package = CookedPackage.Parse(pak.Read(uasset), pak.Read(uexp));
            var keys = package.FindExport("CurveFloat") is { } export ? package.ReadCurveKeys(export) : [];
            if (keys.Count == 0)
            {
                return null;
            }

            var top = (int)Math.Round(keys.Max(k => k.Value));
            return top is >= 1 and <= byte.MaxValue ? top : null;
        }
        catch (SaveFormatException)
        {
            return null;
        }
    }
}
