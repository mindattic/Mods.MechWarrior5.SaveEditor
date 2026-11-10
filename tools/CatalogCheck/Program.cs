using System.Text.Json;
using MechWarrior5.Shared.Assets;

// M5SE-T46: checks the built-in Mercenaries catalogs against the installed game with our own pak reader.
// Classifies every asset in the game pak (base game and all DLC) by its own file-named export's class, then
// diffs weapons, ammo, equipment and traits against src/MechWarrior5.Mercs.SaveEditor.Core/Data. Read-only.
// Usage: dotnet run --project tools/CatalogCheck -- "<MW5Mercs-WindowsNoEditor.pak>" "<repo root>"
// 2026-11-10 result: ammo 108/108, equipment 49/49, traits 241/241 identical; weapons: the game has 959
// weapon-class assets = the 928 built-in + 31 AI-only (turret, VTOL, vehicle, Elemental, LRM carrier), so nothing
// a player can own is missing or invented. ('Mechs are already read from the pak at runtime: GameDataScanner.)
// Pilots: personas are exactly the game's 591 MWPersonaAsset (none missing, none extra); every built-in portrait
// (385) and voice (342) name exists in the game. MWPilotPersonaAsset (141) are career hire presets, not personas.
var pakPath = args[0];
var repo = args[1];
using var pak = PakFile.Open(pakPath);
var byClass = new Dictionary<string, List<(string Asset, string File)>>();
foreach (var f in pak.Files.Where(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)))
{
    var name = Path.GetFileNameWithoutExtension(f);
    try
    {
        var main = CookedPackage.Parse(pak.Read(f), []).Exports.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (main is null) continue;
        if (!byClass.TryGetValue(main.ClassName, out var list)) byClass[main.ClassName] = list = [];
        list.Add((name, f));
    }
    catch { }
}

bool IsWeapon(string c) => c.EndsWith("WeaponDataAsset", StringComparison.Ordinal);
bool IsAmmo(string c) => c == "MWAmmoDataAsset";
bool IsTrait(string c) => c.Contains("Trait", StringComparison.Ordinal);
var items = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, @"src\MechWarrior5.Mercs.SaveEditor.Core\Data\items.json"))).RootElement;
var traits = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, @"src\MechWarrior5.Mercs.SaveEditor.Core\Data\traits.json"))).RootElement;
HashSet<string> Names(JsonElement arr) => arr.EnumerateArray().Select(e => e.GetProperty("asset").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
var builtWeapons = Names(items.GetProperty("weapons"));
var builtEquip = Names(items.GetProperty("equipment"));
var builtAmmo = Names(items.GetProperty("ammo"));
var builtTraits = Names(traits.GetProperty("pilot")).Union(Names(traits.GetProperty("mech")), StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

// Which classes do the built-in equipment entries have?
// Several file names exist twice (a weapon and its audio event), so prefer the data-asset class.
var classOf = byClass.SelectMany(kv => kv.Value.Select(v => (v.Asset, Class: kv.Key, v.File))).GroupBy(x => x.Asset, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Class.EndsWith("DataAsset", StringComparison.Ordinal)).First(), StringComparer.OrdinalIgnoreCase);
void Report(string label, HashSet<string> built, Func<string, bool> classRule)
{
    var found = classOf.Values.Where(x => classRule(x.Class)).ToList();
    var foundNames = found.Select(x => x.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missingInGame = built.Where(b => !classOf.ContainsKey(b)).ToList();
    var builtNotMatchingRule = built.Where(b => classOf.ContainsKey(b) && !classRule(classOf[b].Class)).Select(b => $"{b}:{classOf[b].Class}").ToList();
    var extra = found.Where(x => !built.Contains(x.Asset)).ToList();
    Console.WriteLine($"== {label}: built-in {built.Count}, class-rule finds {found.Count}");
    Console.WriteLine($"  built-in entries not in the game pak: {missingInGame.Count} {string.Join(", ", missingInGame.Take(8))}");
    Console.WriteLine($"  built-in entries with other classes: {builtNotMatchingRule.Count} {string.Join(", ", builtNotMatchingRule.Take(8))}");
    Console.WriteLine($"  in game, not built-in: {extra.Count}");
    foreach (var g in extra.GroupBy(x => string.Join('/', x.File.Split('/').SkipWhile(s => s != "Content").Skip(1).Take(3))).OrderByDescending(g => g.Count()).Take(14))
        Console.WriteLine($"    {g.Count(),4} {g.Key,-50} e.g. {string.Join(", ", g.Take(3).Select(x => x.Asset))}");
}
Console.WriteLine("built-in equipment classes: " + string.Join(", ", builtEquip.Where(classOf.ContainsKey).Select(b => classOf[b].Class).Distinct()));
Report("weapons", builtWeapons, IsWeapon);
Report("ammo", builtAmmo, IsAmmo);
var equipClasses = builtEquip.Where(classOf.ContainsKey).Select(b => classOf[b].Class).ToHashSet();
Report("equipment", builtEquip, c => equipClasses.Contains(c));
Report("traits", builtTraits, IsTrait);

// Pilots: every built-in persona, portrait and voice name should be a real asset; show which classes back them.
var pilots = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, @"src\MechWarrior5.Mercs.SaveEditor.Core\Data\pilots.json"))).RootElement;
void Backing(string label, IEnumerable<string> names)
{
    var list = names.ToList();
    var missing = list.Where(n => !classOf.ContainsKey(n)).ToList();
    Console.WriteLine($"== {label}: built-in {list.Count}, missing from the game {missing.Count} {string.Join(", ", missing.Take(10))}");
    foreach (var g in list.Where(classOf.ContainsKey).GroupBy(n => classOf[n].Class).OrderByDescending(g => g.Count()))
        Console.WriteLine($"    {g.Count(),4} {g.Key}");
}
Backing("personas", pilots.GetProperty("personas").EnumerateArray().Select(e => e.GetProperty("asset").GetString()!));
Backing("portraits", pilots.GetProperty("portraits").EnumerateArray().Select(e => e.GetString()!));
Backing("voices", pilots.GetProperty("voices").EnumerateArray().Select(e => e.GetString()!));
var builtPersonas = pilots.GetProperty("personas").EnumerateArray().Select(e => e.GetProperty("asset").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var cls in new[] { "MWPersonaAsset", "MWPilotPersonaAsset" })
{
    var inGame = byClass.GetValueOrDefault(cls)?.Select(x => x.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    var notBuilt = inGame.Where(n => !builtPersonas.Contains(n)).ToList();
    Console.WriteLine($"== {cls} in game: {inGame.Count}, covered by built-in personas {inGame.Count - notBuilt.Count}, not covered {notBuilt.Count} {string.Join(", ", notBuilt.Take(12))}");
}
