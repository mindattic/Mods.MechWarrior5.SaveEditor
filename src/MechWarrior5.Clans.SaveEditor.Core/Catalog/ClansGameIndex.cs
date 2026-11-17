using System.Text.Json;
using MechWarrior5.Shared.Assets;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.Clans.SaveEditor.Core.Catalog;

/// <summary>An installed MechWarrior 5: Clans mod: a folder in <c>MW5Clans\Mods</c> with paks, and the assets it ships.</summary>
public sealed record ClansMod(string Folder, string Name, string? Version, string? Url, bool Enabled, IReadOnlySet<string> Assets)
{
    public string Label => string.IsNullOrEmpty(Version) ? Name : $"{Name} {Version}";
}

/// <summary>
/// Every asset name in the installed Clans game (base and DLC) and in each installed Clans mod, read from the IoStore
/// directory indexes and legacy pak indexes. Those indexes aren't compressed, so this needs no Oodle.
/// </summary>
public sealed class ClansGameIndex
{
    public const string PaksRelative = @"MW5Clans\Content\Paks";
    public const string ModsRelative = @"MW5Clans\Mods";

    private ClansGameIndex(IReadOnlySet<string> baseAssets, IReadOnlyList<ClansMod> mods)
    {
        BaseAssets = baseAssets;
        Mods = mods;
    }

    public IReadOnlySet<string> BaseAssets { get; }

    public IReadOnlyList<ClansMod> Mods { get; }

    public static ClansGameIndex Read(string clansFolder)
    {
        var baseAssets = AssetsIn(Path.Combine(clansFolder, PaksRelative), SearchOption.TopDirectoryOnly);
        var modsFolder = Path.Combine(clansFolder, ModsRelative);
        var disabled = DisabledInModList(Path.Combine(modsFolder, "modlist.ini"));
        var mods = new List<ClansMod>();
        if (Directory.Exists(modsFolder))
        {
            foreach (var folder in Directory.EnumerateDirectories(modsFolder))
            {
                var assets = AssetsIn(folder, SearchOption.AllDirectories);
                assets.ExceptWith(baseAssets);
                if (assets.Count == 0 && !Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).Any(f => f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var id = Path.GetFileName(folder);
                var (name, version, url) = Describe(folder);
                mods.Add(new ClansMod(folder, name ?? id, version, url, !disabled.Contains(id) && (name is null || !disabled.Contains(name)), assets));
            }
        }

        return new ClansGameIndex(baseAssets, mods);
    }

    /// <summary>
    /// Mods turned off in <c>modlist.ini</c>. Its layout isn't documented and no Clans mod was available to check it, so
    /// only an explicit off value (<c>Name=False</c>, <c>=0</c>, <c>=Disabled</c>) counts; anything else is treated as on.
    /// </summary>
    private static HashSet<string> DisabledInModList(string path)
    {
        var off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    var at = line.IndexOf('=', StringComparison.Ordinal);
                    if (at > 0 && line[(at + 1)..].Trim().ToLowerInvariant() is "false" or "0" or "disabled")
                    {
                        off.Add(line[..at].Trim());
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return off;
    }

    private static (string? Name, string? Version, string? Url) Describe(string folder)
    {
        try
        {
            var json = Path.Combine(folder, "mod.json");
            if (File.Exists(json))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                string? Text(string key) => doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
                var url = Text("authorURL") is { } link && link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? link
                    : doc.RootElement.TryGetProperty("steamPublishedFileId", out var steam) && steam.TryGetInt64(out var id) && id > 0
                        ? "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id
                        : null;
                return (Text("displayName"), Text("version"), url);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return (null, null, null);
    }

    private static HashSet<string> AssetsIn(string folder, SearchOption search)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder))
        {
            return names;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.*", search))
        {
            try
            {
                IEnumerable<string> entries = file.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase) ? IoStoreContainer.Open(file).Files.Keys
                    : file.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ? PakEntries(file)
                    : [];
                foreach (var entry in entries.Where(e => e.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)))
                {
                    names.Add(Path.GetFileNameWithoutExtension(entry));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or ArgumentException or InvalidDataException)
            {
                // A pak or table the readers can't follow is skipped; the rest of the index stands.
            }
        }

        return names;
    }

    private static List<string> PakEntries(string path)
    {
        using var pak = PakFile.Open(path);
        return [.. pak.Files];
    }
}
