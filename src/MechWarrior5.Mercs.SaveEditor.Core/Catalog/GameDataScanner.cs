using System.Text.RegularExpressions;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Shared.Assets;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>A 'Mech variant found in the installed game or an enabled mod.</summary>
public sealed record ScannedMech(MechVariant Variant, StockLoadout? Loadout);

/// <summary>A texture package inside a pak: <paramref name="Entry"/> is its <c>.uasset</c> path.</summary>
public sealed record GameTexture(string PakPath, string Entry);

public sealed record GameData(
    string GameFolder,
    IReadOnlyList<ScannedMech> Mechs,
    IReadOnlyList<string> Mods,
    IReadOnlyList<string> Problems,
    IReadOnlySet<string> AssetNames)
{
    /// <summary>Each variant's picture (its data asset's <c>MechTexture</c>), by variant asset.</summary>
    public IReadOnlyDictionary<string, GameTexture> MechArt { get; init; } = new Dictionary<string, GameTexture>();

    /// <summary>Every installed mod, enabled or not, in load order.</summary>
    public IReadOnlyList<InstalledMod> InstalledMods { get; init; } = [];

    /// <summary>Asset names in the base game and its DLC alone (vanilla).</summary>
    public IReadOnlySet<string> BaseAssetNames { get; init; } = new HashSet<string>();

    /// <summary>Which installed mod (enabled or not) ships each asset the base game doesn't; the last in load order wins.</summary>
    public IReadOnlyDictionary<string, InstalledMod> ModAssets { get; init; } = new Dictionary<string, InstalledMod>();

    /// <summary>Weapons, equipment and ammo the enabled mods add (assets the base game doesn't have).</summary>
    public IReadOnlyList<ItemDefinition> Items { get; init; } = [];

    /// <summary>Pilot traits the enabled mods add.</summary>
    public IReadOnlyList<TraitDefinition> PilotTraits { get; init; } = [];

    /// <summary>'Mech traits the enabled mods add.</summary>
    public IReadOnlyList<TraitDefinition> MechTraits { get; init; } = [];

    /// <summary>Each weapon's and ammo's ammo tag (base game and enabled mods): matching tags go together.</summary>
    public IReadOnlyDictionary<string, string> AmmoTypes { get; init; } = new Dictionary<string, string>();

    /// <summary>Cantina career paths (base game and enabled mods) with their levels and rewards.</summary>
    public IReadOnlyList<CareerPathDefinition> CareerPaths { get; init; } = [];

    /// <summary>The top of the base game's pilot skill level curve, when it could be read.</summary>
    public int? BaseSkillCap { get; init; }

    /// <summary>The top of an enabled mod's pilot skill level curve (e.g. Pilot Overhaul's), and the mod's name.</summary>
    public (int Level, string Mod)? ModSkillCap { get; init; }
}

/// <summary>
/// Reads 'Mech data assets (<c>*_MDA</c>) and stock loadouts (<c>*_Loadout</c>) from the game pak and
/// from every enabled mod's paks (Mods folder and Steam Workshop, in the game's load order), so the catalog
/// reflects the installed game, DLC and mods. Disabled mods are only listed, so a save can be told which
/// of them its content comes from.
/// </summary>
public static partial class GameDataScanner
{
    public static GameData Scan(string gameFolder, MechCatalog catalog) =>
        Scan(gameFolder, Path.Combine(gameFolder, GameInstall.PakRelative), InstalledMods.Read(gameFolder), catalog);

    /// <summary>The base game pak plus the given mods (in their order); used directly to read one mod on its own.</summary>
    public static GameData Scan(string gameFolder, string basePak, IReadOnlyList<InstalledMod> installed, MechCatalog catalog)
    {
        var mechs = new Dictionary<string, ScannedMech>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var art = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var opened = new List<PakFile>();
        var content = new ModContent();
        var baseAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            ScanPak(basePak, modName: null, catalog, mechs, problems, assets, art, opened, baseAssets, content);
            baseAssets.UnionWith(assets);

            var modAssets = new Dictionary<string, InstalledMod>(StringComparer.OrdinalIgnoreCase);
            var mods = new List<string>();
            foreach (var mod in installed)
            {
                if (mod.Enabled)
                {
                    mods.Add(mod.Name);
                }

                foreach (var pak in mod.Paks)
                {
                    var names = mod.Enabled
                        ? ScanPak(pak, mod.Name, catalog, mechs, problems, assets, art, opened, baseAssets, content)
                        : ListAssets(pak, problems);
                    foreach (var name in names.Where(n => !baseAssets.Contains(n)))
                    {
                        modAssets[name] = mod;
                    }
                }
            }

            return new GameData(gameFolder, [.. mechs.Values], mods, problems, assets)
            {
                MechArt = ResolveArt(art, opened),
                InstalledMods = installed,
                BaseAssetNames = baseAssets,
                ModAssets = modAssets,
                Items = [.. content.Items.Values],
                PilotTraits = [.. content.PilotTraits.Values],
                MechTraits = [.. content.MechTraits.Values],
                BaseSkillCap = content.BaseSkillCap,
                AmmoTypes = content.AmmoTypes,
                CareerPaths = [.. content.CareerPaths.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)],
                ModSkillCap = content.ModSkillCap,
            };
        }
        finally
        {
            foreach (var pak in opened)
            {
                pak.Dispose();
            }
        }
    }

    /// <summary>
    /// Finds each picture's package in the paks, the last-loaded pak first (a mod's texture wins over the game's, as in game).
    /// <c>/Game/X</c> is the game's <c>MW5Mercs/Content/X</c>; <c>/Plugin/X</c> is a mod's <c>Plugin/Content/X</c>.
    /// </summary>
    private static Dictionary<string, GameTexture> ResolveArt(Dictionary<string, string> art, List<PakFile> paks)
    {
        var found = new Dictionary<string, GameTexture>(StringComparer.OrdinalIgnoreCase);
        var indexes = new Dictionary<PakFile, Dictionary<string, string>>();
        foreach (var (asset, softPath) in art)
        {
            var package = softPath.LastIndexOf('.') is var dot && dot > softPath.LastIndexOf('/') ? softPath[..dot] : softPath;
            for (var i = paks.Count - 1; i >= 0; i--)
            {
                if (!indexes.TryGetValue(paks[i], out var index))
                {
                    indexes[paks[i]] = index = PackageIndex(paks[i]);
                }

                if (index.TryGetValue(package, out var entry))
                {
                    found[asset] = new GameTexture(paks[i].Path, entry);
                    break;
                }
            }
        }

        return found;
    }

    private static Dictionary<string, string> PackageIndex(PakFile pak)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in pak.Files)
        {
            if (!file.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = file.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
            if (content <= 0)
            {
                continue;
            }

            var root = file[..content];
            root = root[(root.LastIndexOf('/') + 1)..];
            var mount = string.Equals(root, "MW5Mercs", StringComparison.OrdinalIgnoreCase) ? "/Game" : "/" + root;
            index.TryAdd(mount + file[(content + "/Content".Length)..^".uasset".Length], file);
        }

        return index;
    }

    private static List<string> ListAssets(string path, List<string> problems)
    {
        try
        {
            using var pak = PakFile.Open(path);
            return [.. pak.Files.Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileNameWithoutExtension).OfType<string>()];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or InvalidDataException
                                       or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            problems.Add($"{Path.GetFileName(path)}: {e.Message}");
            return [];
        }
    }

    /// <summary>Reads one pak's 'Mechs, loadouts and pictures; returns its asset names.</summary>
    private static List<string> ScanPak(
        string path, string? modName, MechCatalog catalog, Dictionary<string, ScannedMech> mechs, List<string> problems, HashSet<string> assets,
        Dictionary<string, string> art, List<PakFile> opened, IReadOnlySet<string> baseAssets, ModContent content)
    {
        var names = new List<string>();
        try
        {
            var pak = PakFile.Open(path);
            opened.Add(pak);
            foreach (var file in pak.Files.Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                assets.Add(name);
                names.Add(name);
            }

            var loadouts = new Dictionary<string, StockLoadout>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in pak.Files.Where(f => f.EndsWith("_Loadout.uasset", StringComparison.OrdinalIgnoreCase)
                                                      && !f.Contains("/CareerMode/", StringComparison.OrdinalIgnoreCase)))
            {
                if (Read(pak, file, "MWMechLoadoutAsset") is { } props && props.Find<StructProperty>("MechLoadout")?.Value is PropertyList loadout
                    && Ue.AssetName(loadout.Find<StructProperty>("MechDataAssetId")?.Value as PropertyList) is { } asset)
                {
                    loadouts.TryAdd(asset, MechLoadoutReader.Read(loadout));
                }
            }

            foreach (var file in pak.Files.Where(f => f.EndsWith("_MDA.uasset", StringComparison.OrdinalIgnoreCase)))
            {
                var asset = Path.GetFileNameWithoutExtension(file);
                if (mechs.ContainsKey(asset))
                {
                    continue; // already known; a mod's stock loadout for it is applied below
                }

                var known = catalog.FindByAsset(asset);
                if (known is null && modName is null && !IsPlayable(asset, pak))
                {
                    continue;
                }

                var data = Read(pak, file, "MWMechDataAsset")?.Find<StructProperty>("MechData")?.Value as PropertyList;
                var variant = known ?? Describe(data, file, asset, catalog, modName);
                if (variant is null)
                {
                    continue;
                }

                if (data?.Find<NameProperty>("MechTexture")?.Value.Value is { Length: > 0 } texture && texture != "None")
                {
                    art[asset] = texture;
                }

                if (known is not null && modName is null && DlcOf(file) is { } tag && known.Dlc?.Tag != tag)
                {
                    variant = known with { Dlc = catalog.DlcPacks.FirstOrDefault(p => p.Tag == tag) };
                }

                mechs[asset] = new ScannedMech(variant, loadouts.GetValueOrDefault(asset));
            }

            ScanModContent(pak, modName, baseAssets, content);

            // Like the game, a mod's pak wins over what came before: its loadout replaces the stock one for a
            // mech already known (e.g. a rebalance mod shipping only AS7-D_Loadout).
            if (modName is not null)
            {
                foreach (var (asset, loadout) in loadouts)
                {
                    if (mechs.TryGetValue(asset, out var existing))
                    {
                        mechs[asset] = existing with { Loadout = loadout };
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or InvalidDataException
                                       or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // One pak the reader can't follow (e.g. an index entry stored outside the encoded block) skips only that pak.
            problems.Add($"{Path.GetFileName(path)}: {e.Message}");
        }

        return names;
    }

    /// <summary>Builds a catalog entry for a variant the built-in catalog doesn't know (new DLC data or a mod).</summary>
    private static MechVariant? Describe(PropertyList? data, string file, string asset, MechCatalog catalog, string? modName)
    {
        if (data is null)
        {
            return null;
        }

        var tag = ((data.Find<StructProperty>("MechType")?.Value as PropertyList)?.Find<NameProperty>("TagName"))?.Value.Value ?? string.Empty;
        var parts = tag.Split('.');
        var chassis = parts.Length >= 3 ? parts[2] : asset.Split('-')[0];
        var tons = ((((data.Find<StructProperty>("MechDataStats")?.Value as PropertyList)
            ?.Find<StructProperty>("BaseStats")?.Value as PropertyList)?.Find<IntProperty>("Tons"))?.Value) ?? 0;
        var code = asset.EndsWith("_MDA", StringComparison.OrdinalIgnoreCase) ? asset[..^4] : asset;
        var known = catalog.Variants.FirstOrDefault(v => string.Equals(v.Chassis.Replace(" ", string.Empty, StringComparison.Ordinal), chassis, StringComparison.OrdinalIgnoreCase));
        var name = known?.ChassisName ?? Spaced().Replace(chassis, " ");
        var dlc = modName is null && DlcOf(file) is { } dlcTag ? catalog.DlcPacks.FirstOrDefault(p => p.Tag == dlcTag) : null;
        return new MechVariant(
            code,
            asset,
            known?.Chassis ?? chassis,
            name,
            tons,
            MechCatalog.ClassOf(tons),
            null,
            dlc) { ModName = modName };
    }

    private static PropertyList? Read(PakFile pak, string uasset, string exportClass)
    {
        var uexp = uasset[..^".uasset".Length] + ".uexp";
        if (!pak.Contains(uexp))
        {
            return null;
        }

        try
        {
            var package = CookedPackage.Parse(pak.Read(uasset), pak.Read(uexp));
            return package.FindExport(exportClass) is { } export ? package.ReadProperties(export) : null;
        }
        catch (SaveFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The base game also ships scripted, boss, tutorial and cinematic variants that players can't own;
    /// only playable ones are offered (a "_PLAYABLE" twin replaces its non-playable original).
    /// </summary>
    private static bool IsPlayable(string asset, PakFile pak)
    {
        if (NonPlayable().IsMatch(asset))
        {
            return false;
        }

        var code = asset[..^"_MDA".Length];
        return !pak.Files.Any(f => f.EndsWith($"/{code}_PLAYABLE_MDA.uasset", StringComparison.OrdinalIgnoreCase));
    }

    private static string? DlcOf(string file) =>
        DlcFolder().Match(file) is { Success: true } m ? $"DLC.DLC{m.Groups[1].Value}" : null;

    [GeneratedRegex(@"^A1M4_|(_Boss|_Cinematic|TUTORIAL|_DLC\d+)_MDA$", RegexOptions.IgnoreCase)]
    private static partial Regex NonPlayable();

    [GeneratedRegex(@"/Content/DLC(\d+)/", RegexOptions.IgnoreCase)]
    private static partial Regex DlcFolder();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex Spaced();
}
