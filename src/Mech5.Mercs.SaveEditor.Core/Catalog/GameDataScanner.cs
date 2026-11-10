using System.Text.Json;
using System.Text.RegularExpressions;
using Mech5.Mercs.SaveEditor.Core.Game;
using Mech5.Shared.Assets;

namespace Mech5.Mercs.SaveEditor.Core.Catalog;

/// <summary>A 'Mech variant found in the installed game or an enabled mod.</summary>
public sealed record ScannedMech(MechVariant Variant, StockLoadout? Loadout);

public sealed record GameData(string GameFolder, IReadOnlyList<ScannedMech> Mechs, IReadOnlyList<string> Mods, IReadOnlyList<string> Problems);

/// <summary>
/// Reads 'Mech data assets (<c>*_MDA</c>) and stock loadouts (<c>*_Loadout</c>) from the game pak and
/// from every enabled mod's paks, so the catalog reflects the installed game, DLC and mods.
/// </summary>
public static partial class GameDataScanner
{
    public static GameData Scan(string gameFolder, MechCatalog catalog)
    {
        var mechs = new Dictionary<string, ScannedMech>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        ScanPak(Path.Combine(gameFolder, GameInstall.PakRelative), modName: null, catalog, mechs, problems);

        var mods = new List<string>();
        foreach (var (folder, name) in EnabledMods(Path.Combine(gameFolder, GameInstall.ModsRelative)))
        {
            var paks = Directory.Exists(Path.Combine(folder, "Paks"))
                ? Directory.EnumerateFiles(Path.Combine(folder, "Paks"), "*.pak", SearchOption.AllDirectories).ToList()
                : [];
            if (paks.Count == 0)
            {
                continue;
            }

            mods.Add(name);
            foreach (var pak in paks)
            {
                ScanPak(pak, name, catalog, mechs, problems);
            }
        }

        return new GameData(gameFolder, [.. mechs.Values], mods, problems);
    }

    private static void ScanPak(string path, string? modName, MechCatalog catalog, Dictionary<string, ScannedMech> mechs, List<string> problems)
    {
        try
        {
            using var pak = PakFile.Open(path);
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
                    continue;
                }

                var known = catalog.FindByAsset(asset);
                if (known is null && modName is null && !IsPlayable(asset, pak))
                {
                    continue;
                }

                var variant = known ?? Describe(pak, file, asset, catalog, modName);
                if (variant is null)
                {
                    continue;
                }

                if (known is not null && modName is null && DlcOf(file) is { } tag && known.Dlc?.Tag != tag)
                {
                    variant = known with { Dlc = catalog.DlcPacks.FirstOrDefault(p => p.Tag == tag) };
                }

                mechs[asset] = new ScannedMech(variant, loadouts.GetValueOrDefault(asset));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or InvalidDataException)
        {
            problems.Add($"{Path.GetFileName(path)}: {e.Message}");
        }
    }

    /// <summary>Builds a catalog entry for a variant the built-in catalog doesn't know (new DLC data or a mod).</summary>
    private static MechVariant? Describe(PakFile pak, string file, string asset, MechCatalog catalog, string? modName)
    {
        if (Read(pak, file, "MWMechDataAsset") is not { } props || props.Find<StructProperty>("MechData")?.Value is not PropertyList data)
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
            known?.SarnaTitle ?? name,
            tons,
            tons switch { <= 35 => WeightClass.Light, <= 55 => WeightClass.Medium, <= 75 => WeightClass.Heavy, _ => WeightClass.Assault },
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

    /// <summary>Enabled mods per the game's <c>modlist.json</c>; every mod folder counts when there is no list.</summary>
    private static IEnumerable<(string Folder, string Name)> EnabledMods(string modsFolder)
    {
        if (!Directory.Exists(modsFolder))
        {
            yield break;
        }

        HashSet<string>? enabled = null;
        var list = Path.Combine(modsFolder, "modlist.json");
        if (File.Exists(list))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(list));
                if (doc.RootElement.TryGetProperty("modStatus", out var status))
                {
                    enabled = status.EnumerateObject()
                        .Where(m => m.Value.TryGetProperty("bEnabled", out var e) && e.ValueKind == JsonValueKind.True)
                        .Select(m => m.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
            }
        }

        foreach (var folder in Directory.EnumerateDirectories(modsFolder))
        {
            var id = Path.GetFileName(folder);
            if (enabled is null || enabled.Contains(id))
            {
                yield return (folder, ModDisplayName(folder) ?? id);
            }
        }
    }

    private static string? ModDisplayName(string folder)
    {
        var json = Path.Combine(folder, "mod.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            return doc.RootElement.TryGetProperty("displayName", out var n) ? n.GetString() : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^A1M4_|(_Boss|_Cinematic|TUTORIAL|_DLC\d+)_MDA$", RegexOptions.IgnoreCase)]
    private static partial Regex NonPlayable();

    [GeneratedRegex(@"/Content/DLC(\d+)/", RegexOptions.IgnoreCase)]
    private static partial Regex DlcFolder();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex Spaced();
}
