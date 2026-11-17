using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

// M5SE-T116/T118: writes add-on packs (docs/ADDONS.md) from a mod's own files, never by hand. Each mod.json folder under the
// input becomes one pack: its items, traits and 'Mechs (assets the base game doesn't have, with their in-game names), and
// its pilot skill curve's top level when it ships one. A developer tool: the editor itself never goes online.
//
//   dotnet run --project tools/AddonPack -- <mod or staging folder | nexus:<modId>> [--out addons] [--source URL] [--game <MW5 folder>]
//
// nexus:<modId> downloads the mod's main file through the NexusMods API (NEXUSMODS_API_KEY; download links need a Premium
// account) and extracts it (.zip only) to a temp folder first.
var input = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? throw new ArgumentException("usage: AddonPack <folder|nexus:id> [--out dir] [--source url] [--game dir]");
string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
var outDir = Option("--out") ?? "addons";
var source = Option("--source");
var game = Option("--game") ?? GameInstall.Find() ?? throw new InvalidOperationException("MechWarrior 5: Mercenaries not found; pass --game");

if (input.StartsWith("nexus:", StringComparison.OrdinalIgnoreCase))
{
    var id = input["nexus:".Length..];
    source ??= $"https://www.nexusmods.com/mechwarrior5mercenaries/mods/{id}";
    input = await Nexus.DownloadAsync(id);
}

var modFolders = Directory.Exists(Path.Combine(input, "Paks")) && File.Exists(Path.Combine(input, "mod.json"))
    ? [input]
    : Directory.EnumerateFiles(input, "mod.json", SearchOption.AllDirectories).Select(Path.GetDirectoryName).OfType<string>().ToList();
if (modFolders.Count == 0)
{
    Console.Error.WriteLine($"No mod (a folder with mod.json and Paks) under {input}");
    return 1;
}

Directory.CreateDirectory(outDir);
foreach (var folder in modFolders)
{
    if (InstalledMods.ReadFolder(folder) is not { } mod)
    {
        Console.Error.WriteLine($"skip {folder}: no paks");
        continue;
    }

    var catalog = MechCatalog.LoadBuiltIn();
    var data = GameDataScanner.Scan(game, Path.Combine(game, GameInstall.PakRelative), [mod], catalog);
    var file = Path.Combine(outDir, Slug(mod.Name) + ".json");
    File.WriteAllText(file, Write(mod, data, source ?? mod.Url), new UTF8Encoding(false));
    Console.WriteLine($"{file}: {mod.Label}: {data.Items.Count} items, {data.PilotTraits.Count} pilot traits, {data.MechTraits.Count} mech traits, "
        + $"{data.Mechs.Count(m => m.Variant.ModName is not null)} mechs, skill cap {data.ModSkillCap?.Level.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}"
        + (data.Problems.Count > 0 ? $"; problems: {string.Join("; ", data.Problems)}" : string.Empty));
}

return 0;

static string Write(InstalledMod mod, GameData data, string? source)
{
    using var stream = new MemoryStream();
    using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        w.WriteStartObject();
        w.WriteNumber("schema", AddonPack.Schema);
        w.WriteString("name", mod.Name);
        if (mod.Author is { } author)
        {
            w.WriteString("author", author);
        }

        w.WriteString("game", "mercenaries");
        if (source is { } url)
        {
            w.WriteString("source", url);
        }

        w.WriteString("generated", $"tools/AddonPack from {mod.Label}");
        w.WriteStartObject("requires");
        w.WriteString("modName", mod.Name);
        w.WriteString("modFolder", mod.Id);
        w.WriteStartArray("versions");
        if (mod.Version is { } version)
        {
            w.WriteStringValue(version);
        }

        w.WriteEndArray();
        w.WriteEndObject();

        if (data.ModSkillCap is { } cap)
        {
            w.WriteStartObject("limits");
            w.WriteNumber("maxSkillCap", cap.Level);
            w.WriteEndObject();
        }

        Traits("pilotTraits", data.PilotTraits);
        Traits("mechTraits", data.MechTraits);

        w.WriteStartObject("items");
        w.WriteStartArray("add");
        foreach (var item in data.Items.OrderBy(i => i.Asset, StringComparer.OrdinalIgnoreCase))
        {
            w.WriteStartObject();
            w.WriteString("asset", item.Asset);
            w.WriteString("type", item.AssetType);
            w.WriteString("kind", item.Kind.ToString().ToLowerInvariant());
            if (item.Label is { } label)
            {
                w.WriteString("label", label);
            }

            if (item.Kind == ItemKind.Weapon && item.Hardpoint != HardpointClass.Unknown)
            {
                w.WriteString("hardpoint", item.Hardpoint switch
                {
                    HardpointClass.Energy => "E",
                    HardpointClass.Ballistic => "B",
                    HardpointClass.Missile => "M",
                    HardpointClass.Ams => "A",
                    _ => item.Hardpoint.ToString(),
                });
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();

        w.WriteStartObject("mechs");
        w.WriteStartArray("add");
        foreach (var mech in data.Mechs.Select(m => m.Variant).Where(v => v.ModName is not null).OrderBy(v => v.Asset, StringComparer.OrdinalIgnoreCase))
        {
            w.WriteStartObject();
            w.WriteString("asset", mech.Asset);
            w.WriteString("code", mech.Code);
            w.WriteString("chassis", mech.Chassis);
            w.WriteString("name", mech.ChassisName);
            w.WriteNumber("tons", mech.Tons);
            if (mech.HeroName is { } hero)
            {
                w.WriteString("hero", hero);
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();

        // Every other asset of the mod's a save might reference, so a save made with the mod can name it when it's missing.
        var named = data.Items.Select(i => i.Asset).Concat(data.PilotTraits.Concat(data.MechTraits).Select(t => t.Asset))
            .Concat(data.Mechs.Where(m => m.Variant.ModName is not null).Select(m => m.Variant.Asset))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        w.WriteStartArray("assets");
        foreach (var asset in ReferenceableAssets(mod, data).Where(a => !named.Contains(a)).Order(StringComparer.OrdinalIgnoreCase))
        {
            w.WriteStringValue(asset);
        }

        w.WriteEndArray();
        w.WriteEndObject();

        void Traits(string name, IReadOnlyList<TraitDefinition> traits)
        {
            w.WriteStartObject(name);
            w.WriteStartArray("add");
            foreach (var trait in traits.OrderBy(t => t.Asset, StringComparer.OrdinalIgnoreCase))
            {
                w.WriteStartObject();
                w.WriteString("asset", trait.Asset);
                w.WriteString("label", trait.Label);
                if (trait.Description is { } description)
                {
                    w.WriteString("description", description);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }
    }

    return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
}

// Art, audio, meshes and animation are never named in a save; data assets (campaign actors, skins, decals) can be.
static IEnumerable<string> ReferenceableAssets(InstalledMod mod, GameData data)
{
    string[] skipFolders = ["/Textures/", "/Texture/", "/Materials/", "/Material/", "/Meshes/", "/Mesh/", "/Audio/", "/Sound/", "/Sounds/",
        "/Animations/", "/Animation/", "/Anims/", "/FX/", "/VFX/", "/Effects/", "/Particles/", "/Models/", "/Model/", "/Icons/", "/UI/"];
    string[] skipPrefixes = ["T_", "M_", "MI_", "MF_", "SM_", "SK_", "SKM_", "A_", "AM_", "ABP_", "BS_", "SFX_", "SW_", "SC_", "PS_", "NS_", "P_", "TX_", "Mat_", "Tex_", "WBP_", "W_", "Font_"];
    var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var path in mod.Paks)
    {
        using var pak = MechWarrior5.Shared.Assets.PakFile.Open(path);
        foreach (var file in pak.Files.Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!data.BaseAssetNames.Contains(name)
                && !skipFolders.Any(f => file.Contains(f, StringComparison.OrdinalIgnoreCase))
                && !skipPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(name);
            }
        }
    }

    return found;
}

static string Slug(string name) =>
    System.Text.RegularExpressions.Regex.Replace(string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')), "-+", "-").Trim('-');

internal static class Nexus
{
    private const string Api = "https://api.nexusmods.com/v1/games/mechwarrior5mercenaries/mods/";

    public static async Task<string> DownloadAsync(string modId)
    {
        var key = Environment.GetEnvironmentVariable("NEXUSMODS_API_KEY")
            ?? throw new InvalidOperationException("Set NEXUSMODS_API_KEY (a Premium account's key) to download from NexusMods.");
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("apikey", key);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MW5SaveEditor-AddonPack", "1"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var files = JsonDocument.Parse(await http.GetStringAsync($"{Api}{modId}/files.json?category=main"));
        var main = files.RootElement.GetProperty("files").EnumerateArray()
            .OrderByDescending(f => f.GetProperty("uploaded_timestamp").GetInt64())
            .FirstOrDefault();
        if (main.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"NexusMods mod {modId} has no main file.");
        }

        var fileName = main.GetProperty("file_name").GetString() ?? "mod.zip";
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{fileName} isn't a .zip; extract it by hand and pass the folder.");
        }

        using var links = JsonDocument.Parse(await http.GetStringAsync($"{Api}{modId}/files/{main.GetProperty("file_id").GetInt64()}/download_link.json"));
        var url = links.RootElement.EnumerateArray().First().GetProperty("URI").GetString()!;
        var folder = Path.Combine(Path.GetTempPath(), "MW5AddonPack", modId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        var zip = Path.Combine(folder, "download.zip");
        await using (var file = File.Create(zip))
        {
            await (await http.GetStreamAsync(url)).CopyToAsync(file);
        }

        ZipFile.ExtractToDirectory(zip, Path.Combine(folder, "mod"));
        Console.WriteLine($"downloaded {fileName} from NexusMods mod {modId}");
        return Path.Combine(folder, "mod");
    }
}
