using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>
/// A mod the game can load: a folder with paks in <c>MW5Mercs\Mods</c>, or a Steam Workshop mod listed in
/// <c>modlist.json</c>. <see cref="Id"/> is the key the game's <c>modlist.json</c> uses (the folder name).
/// </summary>
public sealed record InstalledMod(string Id, string Folder, string Name, string? Version, bool Enabled, int LoadOrder, IReadOnlyList<string> Paks)
{
    public string? Author { get; init; }

    /// <summary>The mod's own page: its <c>authorURL</c>, else its Steam Workshop page, else null.</summary>
    public string? Url { get; init; }

    public string Label => string.IsNullOrEmpty(Version) ? Name : $"{Name} {Version}";
}

/// <summary>Reads the installed mods the way the game does: <c>modlist.json</c> says which are enabled and in what order.</summary>
public static class InstalledMods
{
    private const string WorkshopPage = "https://steamcommunity.com/sharedfiles/filedetails/?id=";

    /// <summary>Every installed mod with paks, enabled or not, in load order (later wins in game).</summary>
    public static IReadOnlyList<InstalledMod> Read(string gameFolder)
    {
        var modsFolder = Path.Combine(gameFolder, GameInstall.ModsRelative);
        var status = ReadModList(Path.Combine(modsFolder, "modlist.json"));
        var mods = new List<InstalledMod>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(modsFolder))
        {
            foreach (var folder in SafeDirectories(modsFolder))
            {
                var id = Path.GetFileName(folder);
                var paks = Paks(Path.Combine(folder, "Paks"));
                if (paks.Count > 0 && Describe(id, folder, paks, status) is { } mod)
                {
                    mods.Add(mod);
                    seen.Add(id);
                }
            }
        }

        // Steam Workshop mods live outside the game folder; modlist.json records where their paks are.
        foreach (var (id, entry) in status ?? [])
        {
            if (seen.Contains(id))
            {
                continue;
            }

            var paks = entry.Paks.Where(p => Path.IsPathRooted(p) && File.Exists(p)).Select(Path.GetFullPath).ToList();
            if (paks.Count == 0)
            {
                continue;
            }

            var folder = ModFolderOf(paks[0]);
            if (Describe(id, folder, paks, status) is { } mod)
            {
                mods.Add(mod);
            }
        }

        return [.. mods.OrderBy(m => m.LoadOrder).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>One mod folder on its own (e.g. a downloaded or staged copy), treated as enabled.</summary>
    public static InstalledMod? ReadFolder(string folder)
    {
        var paks = Paks(Path.Combine(folder, "Paks"));
        return paks.Count == 0 ? null : Describe(Path.GetFileName(folder), folder, paks, status: null);
    }

    private static InstalledMod? Describe(string id, string folder, IReadOnlyList<string> paks, Dictionary<string, ModListEntry>? status)
    {
        string? name = null, version = null, author = null, url = null;
        int? loadOrder = null;
        try
        {
            var json = Path.Combine(folder, "mod.json");
            if (File.Exists(json))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                var root = doc.RootElement;
                name = Text(root, "displayName");
                version = Text(root, "version");
                author = Text(root, "author");
                url = Text(root, "authorURL") is { } link && Uri.TryCreate(link, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? link : null;
                if (url is null && root.TryGetProperty("steamPublishedFileId", out var steam) && steam.TryGetInt64(out var steamId) && steamId > 0)
                {
                    url = WorkshopPage + steamId;
                }

                if (root.TryGetProperty("defaultLoadOrder", out var order) && order.TryGetInt32(out var o))
                {
                    loadOrder = o;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable mod.json still leaves the mod itself: it is named after its folder.
        }

        var entry = status?.GetValueOrDefault(id);
        if (url is null && long.TryParse(id, out var workshopId))
        {
            url = WorkshopPage + workshopId;
        }

        return new InstalledMod(
            id,
            folder,
            string.IsNullOrWhiteSpace(name) ? id : name,
            string.IsNullOrWhiteSpace(version) ? null : version,
            // No modlist.json means the game hasn't run with mods yet; a mod missing from one isn't enabled.
            status is null || entry?.Enabled == true,
            entry?.LoadOrder ?? loadOrder ?? 0,
            paks) { Author = string.IsNullOrWhiteSpace(author) ? null : author, Url = url };
    }

    private static Dictionary<string, ModListEntry>? ReadModList(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = new Dictionary<string, ModListEntry>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("modStatus", out var status) && status.ValueKind == JsonValueKind.Object)
            {
                foreach (var mod in status.EnumerateObject())
                {
                    var enabled = mod.Value.TryGetProperty("bEnabled", out var e) && e.ValueKind == JsonValueKind.True;
                    int? order = mod.Value.TryGetProperty("defaultLoadOrder", out var o) && o.TryGetInt32(out var n) ? n : null;
                    var paks = mod.Value.TryGetProperty("cachedPakPaths", out var p) && p.ValueKind == JsonValueKind.Array
                        ? p.EnumerateArray().Select(x => x.GetString()).OfType<string>().ToList()
                        : [];
                    list[mod.Name] = new ModListEntry(enabled, order, paks);
                }
            }

            return list;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable: treat it as absent rather than as "every mod disabled".
            return null;
        }
    }

    /// <summary>
    /// The mod folder holding a pak: the parent of the nearest folder named <c>Paks</c> above it (paks often sit in
    /// <c>&lt;mod&gt;\Paks\WindowsNoEditor\</c>), else the pak's grandparent folder.
    /// </summary>
    private static string ModFolderOf(string pak)
    {
        for (var dir = Path.GetDirectoryName(pak); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (string.Equals(Path.GetFileName(dir), "Paks", StringComparison.OrdinalIgnoreCase)
                && Path.GetDirectoryName(dir) is { Length: > 0 } parent)
            {
                return parent;
            }
        }

        return Path.GetDirectoryName(Path.GetDirectoryName(pak)!)!;
    }

    private static List<string> Paks(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.pak", SearchOption.AllDirectories)] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeDirectories(string folder)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(folder)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record ModListEntry(bool Enabled, int? LoadOrder, IReadOnlyList<string> Paks);
}
