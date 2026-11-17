using System.Text.Json;
using System.Text.Json.Nodes;

namespace MechWarrior5.Mercs.SaveEditor.Core.Addons;

/// <summary>Which mod a pack belongs to: matched on its <c>mod.json</c> display name, else its folder.</summary>
public sealed record AddonRequirement(string? ModName, string? ModFolder, IReadOnlyList<string> Versions)
{
    public string Label => ModName ?? ModFolder ?? "?";

    /// <summary>Versions are prefixes ("3.0" accepts "3.0g"); none listed accepts any version.</summary>
    public bool Accepts(string? version) =>
        Versions.Count == 0 || (version is not null && Versions.Any(v => version.StartsWith(v, StringComparison.OrdinalIgnoreCase)));
}

public sealed record SkillLevel(int Level, int Experience);

public sealed record AddonTrait(string Asset, string? Label, string? Description);

public sealed record AddonItem(string Asset, string? Type, string? Kind, string? Label, string? Hardpoint);

public sealed record AddonMech(string Asset, string? Code, string? Chassis, string? Name, int? Tons, string? Hero);

/// <summary>New entries (<see cref="Add"/>) and changes to existing ones, matched by asset (<see cref="Override"/>).</summary>
public sealed record AddonList<T>(IReadOnlyList<T> Add, IReadOnlyList<T> Override)
{
    public IEnumerable<T> All => Add.Concat(Override);
}

/// <summary>
/// An add-on pack: plain JSON data carrying one mod's values (limits, skill curve, trait, item and 'Mech additions and
/// changes). It applies only while that mod is installed, enabled and a version it lists. See docs/ADDONS.md.
/// </summary>
public sealed record AddonPack(
    string File,
    string Name,
    string Game,
    AddonRequirement Requires)
{
    public const int Schema = 1;

    public string? Author { get; init; }

    public string? Source { get; init; }

    public IReadOnlyList<string> RequiresPacks { get; init; } = [];

    public IReadOnlyDictionary<string, long> Limits { get; init; } = new Dictionary<string, long>();

    public IReadOnlyList<SkillLevel> SkillCurve { get; init; } = [];

    public AddonList<AddonTrait> PilotTraits { get; init; } = new([], []);

    public AddonList<AddonTrait> MechTraits { get; init; } = new([], []);

    public AddonList<AddonItem> Items { get; init; } = new([], []);

    public AddonList<AddonMech> Mechs { get; init; } = new([], []);

    /// <summary>
    /// The mod's other asset names a save can reference (campaign actors, skins, decals...): not offered anywhere, used
    /// only to tell a save which mod its content comes from.
    /// </summary>
    public IReadOnlyList<string> OtherAssets { get; init; } = [];

    /// <summary>Every asset the pack names, for telling a save which mod its content comes from.</summary>
    public IEnumerable<string> Assets =>
        PilotTraits.All.Select(t => t.Asset).Concat(MechTraits.All.Select(t => t.Asset)).Concat(Items.All.Select(i => i.Asset)).Concat(Mechs.All.Select(m => m.Asset)).Concat(OtherAssets);

    public IEnumerable<string> AddedAssets =>
        PilotTraits.Add.Select(t => t.Asset).Concat(MechTraits.Add.Select(t => t.Asset)).Concat(Items.Add.Select(i => i.Asset)).Concat(Mechs.Add.Select(m => m.Asset));
}

/// <summary>A pack file as read: the pack, or why it couldn't be used.</summary>
public sealed record AddonFile(string File, AddonPack? Pack, string? Error);

public static class AddonPackReader
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Every <c>*.json</c> in <paramref name="folder"/>; a bad file is reported, never thrown.</summary>
    public static IReadOnlyList<AddonFile> ReadFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.OrdinalIgnoreCase).Select(ReadFile)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [new AddonFile(folder, null, e.Message)];
        }
    }

    public static AddonFile ReadFile(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path), Path.GetFileName(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new AddonFile(Path.GetFileName(path), null, e.Message);
        }
    }

    public static AddonFile Parse(string json, string file)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: Options) as JsonObject ?? throw new FormatException("not a JSON object");
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            return new AddonFile(file, null, $"not valid JSON: {e.Message}");
        }

        try
        {
            var schema = Int(root, "schema");
            if (schema is null)
            {
                return new AddonFile(file, null, "missing \"schema\"");
            }

            if (schema > AddonPack.Schema)
            {
                return new AddonFile(file, null, $"schema {schema} needs a newer editor (this one reads {AddonPack.Schema})");
            }

            var name = Text(root, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return new AddonFile(file, null, "missing \"name\"");
            }

            if (root["requires"] is not JsonObject requires || (Text(requires, "modName") is null && Text(requires, "modFolder") is null))
            {
                return new AddonFile(file, null, "missing \"requires\" (a modName or modFolder)");
            }

            var limits = new Dictionary<string, long>(StringComparer.Ordinal);
            if (root["limits"] is JsonObject limitNode)
            {
                foreach (var key in GameLimits.Keys)
                {
                    if (Long(limitNode, key) is { } value)
                    {
                        limits[key] = value;
                    }
                }
            }

            var pack = new AddonPack(file, name.Trim(), Text(root, "game") ?? "mercenaries",
                new AddonRequirement(Text(requires, "modName"), Text(requires, "modFolder"), Strings(requires["versions"])))
            {
                Author = Text(root, "author"),
                Source = Text(root, "source") is { } source && Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? source : null,
                RequiresPacks = Strings(root["requiresPacks"]),
                Limits = limits,
                SkillCurve = [.. Objects(root["skillCurve"])
                    .Select(o => (Level: Int(o, "level"), Xp: Int(o, "xp")))
                    .Where(l => l.Level > 0 && l.Xp >= 0)
                    .Select(l => new SkillLevel(l.Level!.Value, l.Xp!.Value))
                    .OrderBy(l => l.Level)],
                PilotTraits = List(root["pilotTraits"], Trait),
                MechTraits = List(root["mechTraits"], Trait),
                Items = List(root["items"], o => Text(o, "asset") is { } a ? new AddonItem(a, Text(o, "type"), Text(o, "kind"), Text(o, "label"), Text(o, "hardpoint")) : null),
                OtherAssets = Strings(root["assets"]),
                Mechs = List(root["mechs"], o => Text(o, "asset") is { } a ? new AddonMech(a, Text(o, "code"), Text(o, "chassis"), Text(o, "name"), Int(o, "tons"), Text(o, "hero")) : null),
            };
            return new AddonFile(file, pack, null);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException)
        {
            return new AddonFile(file, null, e.Message);
        }

        static AddonTrait? Trait(JsonObject o) => Text(o, "asset") is { } a ? new AddonTrait(a, Text(o, "label"), Text(o, "description")) : null;
    }

    private static AddonList<T> List<T>(JsonNode? node, Func<JsonObject, T?> read)
        where T : class =>
        node is JsonObject o
            ? new AddonList<T>([.. Objects(o["add"]).Select(read).OfType<T>()], [.. Objects(o["override"]).Select(read).OfType<T>()])
            : new AddonList<T>([], []);

    private static IEnumerable<JsonObject> Objects(JsonNode? node) => node is JsonArray array ? array.OfType<JsonObject>() : [];

    private static List<string> Strings(JsonNode? node) =>
        node is JsonArray array ? [.. array.Select(v => v is JsonValue s && s.TryGetValue<string>(out var t) ? t : null).OfType<string>().Where(t => t.Length > 0)] : [];

    private static string? Text(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;

    private static int? Int(JsonObject o, string key) => Long(o, key) is { } l && l is >= int.MinValue and <= int.MaxValue ? (int)l : null;

    private static long? Long(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<long>(out var l) ? l
        : o[key] is JsonValue d && d.TryGetValue<double>(out var x) && x == Math.Floor(x) && Math.Abs(x) < 9e18 ? (long)x
        : null;
}
