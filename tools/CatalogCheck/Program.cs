using System.Text.Json;
using MechWarrior5.Shared.Assets;

// M5SE-T46: checks the built-in Mercenaries catalogs against the installed game with our own pak reader.
// Classifies every asset in the game pak (base game and all DLC) by its own file-named export's class, then
// diffs weapons, ammo, equipment and traits against src/MechWarrior5.Mercs.SaveEditor.Core/Data. Read-only.
// Usage: dotnet run --project tools/CatalogCheck -- "<MW5Mercs-WindowsNoEditor.pak>" "<repo root>"
// 2026-11-10 result: ammo 108/108, equipment 49/49, traits 241/241 identical; weapons: the game has 959
// weapon-class assets = the 928 built-in + 31 AI-only (turret, VTOL, vehicle, Elemental, LRM carrier), so nothing
// a player can own is missing or invented. ('Mechs are already read from the pak at runtime: GameDataScanner.)
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
