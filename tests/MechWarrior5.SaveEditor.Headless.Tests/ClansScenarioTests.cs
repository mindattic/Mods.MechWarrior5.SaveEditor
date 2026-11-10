using MechWarrior5.Clans.SaveEditor.Core.Catalog;
using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Seeded random scenarios: each clones one original Clans save (save kinds in rotation) under a new load-menu
/// name, applies a random mix of edits with random values, saves, reopens from disk and verifies every edit, that
/// values it didn't touch are unchanged, that the shop's stock never moves, the load-menu name in every index, the
/// editor stamp and the untouched original. Every fourth scenario edits the new save again in place.
/// </summary>
[TestFixture]
[Category("Headless")]
[Category("Scenarios")]
public class ClansScenarioTests
{
    public const int Count = 520;

    private const string Currency = "KelCurrencyItemDataAsset";
    private const string Crew = "KelCrewItemDataAsset";
    private const string Equipment = "KelEquipmentItemDataAsset";

    private static readonly string[] Names = ["Star Adder Prime", "Ghost Bear's Pride", "Jade Falcon Keshik", "  Padded  ", "Ω-Trinary", "Šárka", "赤い竜"];

    /// <summary>One original save of each kind the game writes (newest first found per kind).</summary>
    private static readonly Lazy<IReadOnlyList<(string Kind, string Save)>> Sources = new(() =>
        Directory.Exists(SaveFixtures.ClansRoot)
            ? [.. SaveLibrary.List(SaveFixtures.ClansRoot)
                .Where(s => !ClansDocument.DiskFileIsEditorSave(s.Path))
                .Select(s => (Kind: s.SaveType == "AutoSave" && s.IsMissionSave ? "AutoSave (mission)" : s.SaveType, s.Path))
                .Where(x => ClansPermutationTests.SaveKinds.Contains(x.Kind))
                .DistinctBy(x => x.Kind)]
            : []);

    public static IEnumerable<TestCaseData> Seeds() =>
        Enumerable.Range(1, Count).Select(i => new TestCaseData(i).SetName($"Clans scenario {i:0000}"));

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [TestCaseSource(nameof(Seeds))]
    public void Clone_edit_save_and_verify(int seed)
    {
        if (Sources.Value.Count == 0)
        {
            Assert.Ignore($"No original MW5 Clans saves under {SaveFixtures.ClansRoot}.");
        }

        var source = Sources.Value[seed % Sources.Value.Count];
        var folder = SaveFixtures.Sandbox("clans");
        var save = Path.Combine(folder, Path.GetFileName(source.Save));
        File.Copy(source.Save, save);
        foreach (var index in Directory.EnumerateFiles(SaveFixtures.ClansRoot, "UserSaveMetaData*.sav"))
        {
            File.Copy(index, Path.Combine(folder, Path.GetFileName(index)));
        }

        var originalHash = SaveFixtures.Hash(save);
        var rng = new Random(seed);
        var doc = ClansDocument.Load(save);
        var before = Snapshot.Of(doc);
        var touched = new HashSet<string>();
        var checks = new List<(string Op, Action<ClansDocument> Verify)>();
        var ops = Operations.OrderBy(_ => rng.Next()).Take(rng.Next(2, 9)).ToList();
        foreach (var (name, apply) in ops)
        {
            if (apply(doc, rng, touched) is { } verify)
            {
                checks.Add((name, verify));
            }
        }

        var displayName = $"Scenario C-{seed:0000}";
        var outcome = doc.Save(displayName);
        var back = ClansDocument.Load(outcome.Path);
        var newId = Path.GetFileNameWithoutExtension(outcome.Path).Split('_')[1];
        TestContext.Out.WriteLine($"{source.Kind}: {string.Join(", ", ops.Select(o => o.Name))}");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.CreatedNewFile, Is.True, "an original is cloned to a new file");
            Assert.That(outcome.Path, Is.Not.EqualTo(save), "never written over the original");
            Assert.That(outcome.RegisteredInLoadMenu, Is.True, "listed in the load menu");
            Assert.That(back.DisplayName(), Is.EqualTo(displayName), "load-menu name");
            foreach (var index in SaveIndex.Files(folder))
            {
                Assert.That(Ue5SaveFile.Load(index).ToPayload().AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(newId)), Is.GreaterThanOrEqualTo(0),
                    $"{Path.GetFileName(index)} lists the new save");
            }

            Assert.That(EditorStamp.Read(Ue5SaveFile.Load(outcome.Path).Properties), Is.Not.Null, "stamped as an editor save");
            Assert.That(SaveFixtures.Hash(save), Is.EqualTo(originalHash), "the original save must never change");
            foreach (var (op, verify) in checks)
            {
                Assert.DoesNotThrow(() => verify(back), op);
            }

            before.AssertUnchanged(back, touched);
        });

        if (seed % 4 == 0)
        {
            back.Inventory.SetTotal(Currency, "Merits_CurrencyItem", 100 + seed);
            var second = back.Save(displayName + " (2)");
            var again = ClansDocument.Load(second.Path);
            Assert.Multiple(() =>
            {
                Assert.That(second.CreatedNewFile, Is.False, "an editor save is updated in place");
                Assert.That(second.Path, Is.EqualTo(outcome.Path));
                Assert.That(File.Exists(outcome.Path + ".bak"), Is.True, "previous version kept as .bak");
                Assert.That(again.Currency("Merits_CurrencyItem"), Is.EqualTo(100 + seed));
                Assert.That(again.DisplayName(), Is.EqualTo(displayName + " (2)"));
                Assert.That(SaveFixtures.Hash(save), Is.EqualTo(originalHash), "the original save must never change");
            });
        }
    }

    private delegate Action<ClansDocument>? Op(ClansDocument doc, Random rng, HashSet<string> touched);

    private static Op SetCurrency(string asset, int max) => (doc, rng, touched) =>
    {
        var value = rng.Next(4) == 0 ? 0 : rng.Next(1, max);
        doc.Inventory.SetTotal(Currency, asset, value);
        if (asset == "Honor_CurrencyItem" && doc.Progress.HonorExperience is not null)
        {
            doc.Progress.HonorExperience = value;
        }

        touched.Add(asset);
        return back =>
        {
            Assert.That(back.Inventory.Total(asset), Is.EqualTo(value), asset);
            if (asset == "Kerenskies_CurrencyItem")
            {
                Assert.That(SaveLibrary.List(Path.GetDirectoryName(back.Path)!).Single(s => s.Path == back.Path).Kerenskies, Is.EqualTo(value), "load menu shows the new Kerenskies");
            }
        };
    };

    private static readonly (string Name, Op Apply)[] Operations =
    [
        ("Kerenskies", SetCurrency("Kerenskies_CurrencyItem", InventoryEditor.MaxQuantity)),
        ("Merits", SetCurrency("Merits_CurrencyItem", 1_000_000)),
        ("Honor", SetCurrency("Honor_CurrencyItem", 100_000)),
        ("Salvage", (doc, rng, touched) =>
        {
            string[] salvage = ["Salvage_MechComponents", "Salvage_WeaponComponents", "Salvage_EquipmentComponents"];
            var asset = salvage[rng.Next(salvage.Length)];
            var value = rng.Next(0, 50_000);
            doc.Inventory.SetTotal(Currency, asset, value);
            touched.Add(asset);
            return back => Assert.That(back.Inventory.Total(asset), Is.EqualTo(value), asset);
        }),
        ("Crew", (doc, rng, touched) =>
        {
            string[] crew = ["Technician_CrewItem", "Scientist_CrewItem", "TechnicianUpgrade_CrewItem", "ScientistUpgrade_CrewItem", "SalvageUpgrade_CrewItem"];
            var asset = crew[rng.Next(crew.Length)];
            var value = rng.Next(0, 60);
            doc.Inventory.SetTotal(Crew, asset, value);
            touched.Add(asset);
            return back => Assert.That(back.Inventory.Total(asset), Is.EqualTo(value), asset);
        }),
        ("Technicians", (doc, rng, touched) =>
        {
            if (doc.Progress.TechniciansAvailable is null)
            {
                return null;
            }

            var value = rng.Next(0, 40);
            doc.Progress.TechniciansAvailable = value;
            return back => Assert.That(back.Progress.TechniciansAvailable, Is.EqualTo(value), "technicians");
        }),
        ("MechXp", (doc, rng, touched) =>
        {
            var mxp = doc.Inventory.Stacks.Where(s => s.Category == StackCategory.MechXp).Select(s => s.AssetName).Distinct().ToList();
            if (mxp.Count == 0)
            {
                return null;
            }

            var asset = mxp[rng.Next(mxp.Count)];
            var value = rng.Next(0, 100_000);
            doc.Inventory.SetTotal(Currency, asset, value);
            touched.Add(asset);
            return back => Assert.That(back.Inventory.Total(asset), Is.EqualTo(value), asset);
        }),
        ("AddEquipment", (doc, rng, touched) =>
        {
            var catalog = ItemList.For(doc.Inventory).Where(i => i.Type == Equipment && !touched.Contains($"item:{i.Asset}")).ToList();
            var item = catalog[rng.Next(catalog.Count)];
            touched.Add($"item:{item.Asset}");
            var quantity = rng.Next(1, 25);
            var owned = doc.Inventory.Total(item.Asset);
            doc.Inventory.AddStack(Equipment, item.Asset, quantity);
            touched.Add("Equipment");
            return back => Assert.That(back.Inventory.Total(item.Asset), Is.EqualTo(owned + quantity), item.Asset);
        }),
        ("RecountEquipment", (doc, rng, touched) =>
        {
            var owned = doc.Inventory.Stacks.Where(s => s.Category == StackCategory.Equipment && !touched.Contains($"item:{s.AssetName}")).Select(s => s.AssetName).Distinct().ToList();
            if (owned.Count == 0)
            {
                return null;
            }

            var asset = owned[rng.Next(owned.Count)];
            touched.Add($"item:{asset}");
            var value = rng.Next(0, 200);
            doc.Inventory.SetTotal(Equipment, asset, value);
            touched.Add("Equipment");
            return back => Assert.That(back.Inventory.Total(asset), Is.EqualTo(value), asset);
        }),
        ("RemoveEquipment", (doc, rng, touched) =>
        {
            var owned = doc.Inventory.Stacks.Where(s => s.Category == StackCategory.Equipment && !touched.Contains($"item:{s.AssetName}")).Select(s => s.AssetName).Distinct().ToList();
            if (owned.Count == 0)
            {
                return null;
            }

            var asset = owned[rng.Next(owned.Count)];
            touched.Add($"item:{asset}");
            doc.Inventory.RemoveStacks(doc.Inventory.Stacks.Where(s => s.AssetName == asset));
            touched.Add("Equipment");
            return back => Assert.That(back.Inventory.Stacks.Any(s => s.AssetName == asset), Is.False, asset);
        }),
        ("DuplicateMechs", (doc, rng, touched) =>
        {
            var mechs = doc.Inventory.Mechs.ToList();
            if (mechs.Count == 0)
            {
                return null;
            }

            var source = mechs[rng.Next(mechs.Count)];
            var copies = rng.Next(1, 4);
            var known = doc.Inventory.Actors.Select(a => a.InventoryId).ToHashSet();
            doc.Inventory.DuplicateMechs([source], copies);
            var added = doc.Inventory.Mechs.Where(m => !known.Contains(m.InventoryId)).Select(m => m.InventoryId).ToHashSet();
            touched.Add("Mechs");
            return back =>
            {
                var copiesBack = back.Inventory.Mechs.Where(m => added.Contains(m.InventoryId)).ToList();
                Assert.That(copiesBack, Has.Count.EqualTo(copies), "copies are the player's mechs");
                Assert.That(copiesBack.Select(m => m.Variant), Is.All.EqualTo(source.Variant));
                Assert.That(copiesBack.All(m => m.Equipment.SequenceEqual(source.Equipment)), Is.True, "copies keep the loadout");
                Assert.That(copiesBack.Any(m => m.IsDamaged), Is.False, "copies arrive repaired");
                Assert.That(back.Inventory.Actors.Select(a => a.InventoryId).Where(g => g is not null), Is.Unique, "inventory ids stay unique");
            };
        }),
        ("RenameMech", (doc, rng, touched) =>
        {
            var mechs = doc.Inventory.Mechs.ToList();
            if (mechs.Count == 0)
            {
                return null;
            }

            var mech = mechs[rng.Next(mechs.Count)];
            var name = rng.Next(5) == 0 ? "   " : Names[rng.Next(Names.Length)];
            mech.CustomName = name;
            var id = mech.InventoryId;
            return back => Assert.That(back.Inventory.Mechs.Single(m => m.InventoryId == id).CustomName, Is.EqualTo(name.Trim()), "mech name");
        }),
        ("RepairMechs", (doc, rng, touched) =>
        {
            InventoryEditor.RepairMechs(doc.Inventory.Mechs);
            return back => Assert.That(back.Inventory.Mechs.Any(m => m.IsDamaged), Is.False, "all repaired");
        }),
        ("RemoveDuplicate", (doc, rng, touched) =>
        {
            var mechs = doc.Inventory.Mechs.ToList();
            if (mechs.Count == 0)
            {
                return null;
            }

            var known = doc.Inventory.Actors.Select(a => a.InventoryId).ToHashSet();
            doc.Inventory.DuplicateMechs([mechs[rng.Next(mechs.Count)]], 1);
            var copy = doc.Inventory.Mechs.Single(m => !known.Contains(m.InventoryId));
            var result = doc.Inventory.RemoveMechs([copy]);
            var id = copy.InventoryId;
            return back =>
            {
                Assert.That(result.Succeeded, Has.Count.EqualTo(1), "an unreferenced copy can be removed");
                Assert.That(back.Inventory.Actors.Any(a => a.InventoryId == id), Is.False, "removed");
            };
        }),
    ];

    /// <summary>Values a scenario must leave alone unless one of its edits changed them, plus the shop's stock.</summary>
    private sealed record Snapshot(Dictionary<string, int> Currencies, int Mechs, string Equipment, string Shop, int ShopMechs)
    {
        private static readonly string[] Tracked =
        [
            "Kerenskies_CurrencyItem", "Merits_CurrencyItem", "Honor_CurrencyItem", "Salvage_MechComponents", "Salvage_WeaponComponents",
            "Salvage_EquipmentComponents", "Technician_CrewItem", "Scientist_CrewItem", "TechnicianUpgrade_CrewItem", "ScientistUpgrade_CrewItem",
            "SalvageUpgrade_CrewItem",
        ];

        public static Snapshot Of(ClansDocument d) => new(
            Tracked.Concat(d.Inventory.Stacks.Where(s => s.Category == StackCategory.MechXp).Select(s => s.AssetName)).Distinct()
                .ToDictionary(a => a, d.Inventory.Total),
            d.Inventory.Mechs.Count(),
            string.Join(";", d.Inventory.Stacks.Where(s => s.Category == StackCategory.Equipment).GroupBy(s => s.AssetName).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}={g.Sum(s => s.Quantity)}")),
            string.Join(";", d.Inventory.AllStacks.Where(s => !s.IsPlayers).Select(s => $"{s.AssetName}={s.Quantity}")),
            d.Inventory.Actors.Count(a => a.Kind == ActorKind.Mech && !a.IsPlayers));

        public void AssertUnchanged(ClansDocument back, HashSet<string> touched)
        {
            var now = Of(back);
            foreach (var (asset, value) in Currencies.Where(c => !touched.Contains(c.Key)))
            {
                Assert.That(now.Currencies.GetValueOrDefault(asset), Is.EqualTo(value), $"untouched {asset}");
            }

            if (!touched.Contains("Mechs")) Assert.That(now.Mechs, Is.EqualTo(Mechs), "untouched mech count");
            if (!touched.Contains("Equipment")) Assert.That(now.Equipment, Is.EqualTo(Equipment), "untouched equipment");
            Assert.That(now.Shop, Is.EqualTo(Shop), "the shop's stock never changes");
            Assert.That(now.ShopMechs, Is.EqualTo(ShopMechs), "shop, planned and saved-loadout mechs never change");
        }
    }
}
