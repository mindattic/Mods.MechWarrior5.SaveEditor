using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Seeded random scenarios: each clones one original Mercenaries save (save kinds in rotation) under a new
/// load-menu name, applies a random mix of edits with random values, saves, reopens from disk and verifies every
/// edit, that values it didn't touch are unchanged, the load-menu name, the editor stamp and the untouched original.
/// Every fourth scenario edits the new save again in place. The seed is in the test name, so a failure replays.
/// </summary>
[TestFixture]
[Category("Headless")]
[Category("Scenarios")]
public class MercsScenarioTests
{
    public const int Count = 520;

    private static readonly MechCatalog Mechs = MechCatalog.LoadBuiltIn();
    private static readonly ItemCatalog Items = ItemCatalog.LoadBuiltIn();
    private static readonly PilotCatalog PilotData = PilotCatalog.LoadBuiltIn();

    private static readonly string[] Names =
        ["Scenario Lancers", "Red Dragon Company", "赤い竜 ⚔", "Ölberg Söldner", "  Padded Name  ", "Kell Hounds II", "Ω-Squad"];

    /// <summary>One original save of each kind the game writes (first found per kind), with its Campaign.json.</summary>
    private static readonly Lazy<IReadOnlyList<(string Kind, string Save, string Index)>> Sources = new(() =>
        [.. CampaignLibrary.Scan(SaveFixtures.MercsRoot)
            .SelectMany(c => c.Saves.Select(s => (Campaign: c, Save: s)))
            .Where(x => !EditorStamp.FileHasStamp(x.Save.Path))
            .Select(x => (Kind: x.Save.IsAutosave ? x.Save.Name : "Manual", x.Save, x.Campaign))
            .Where(x => MercsPermutationTests.SaveKinds.Contains(x.Kind))
            .OrderByDescending(x => x.Save.LastWrite)
            .DistinctBy(x => x.Kind)
            .Select(x => (x.Kind, x.Save.Path, Path.Combine(x.Campaign.Folder, "Campaign.json")))]);

    public static IEnumerable<TestCaseData> Seeds() =>
        Enumerable.Range(1, Count).Select(i => new TestCaseData(i).SetName($"Mercs scenario {i:0000}"));

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [TestCaseSource(nameof(Seeds))]
    public void Clone_edit_save_and_verify(int seed)
    {
        if (Sources.Value.Count == 0)
        {
            Assert.Ignore($"No original MW5 Mercenaries saves under {SaveFixtures.MercsRoot}.");
        }

        var source = Sources.Value[seed % Sources.Value.Count];
        var folder = SaveFixtures.Sandbox("mercs");
        var save = Path.Combine(folder, Path.GetFileName(source.Save));
        File.Copy(source.Save, save);
        File.Copy(source.Index, Path.Combine(folder, "Campaign.json"));
        var originalHash = SaveFixtures.Hash(save);

        var rng = new Random(seed);
        var doc = Mw5Document.Open(save, Mechs);
        var before = Snapshot.Of(doc);
        var touched = new HashSet<string>();
        var checks = new List<(string Op, Action<Mw5Document> Verify)>();
        var ops = Operations.OrderBy(_ => rng.Next()).Take(rng.Next(2, 9)).ToList();
        foreach (var (name, apply) in ops)
        {
            if (apply(doc, rng, touched) is { } verify)
            {
                checks.Add((name, verify));
            }
        }

        var displayName = $"Scenario M-{seed:0000}";
        var outcome = doc.Save(displayName: displayName);
        var back = Mw5Document.Open(outcome.Path, Mechs);
        TestContext.Out.WriteLine($"{source.Kind}: {string.Join(", ", ops.Select(o => o.Name))}");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.CreatedNewFile, Is.True, "an original is cloned to a new file");
            Assert.That(outcome.Path, Is.Not.EqualTo(save), "never written over the original");
            Assert.That(outcome.RegisteredInCampaign, Is.True, "listed in the load menu");
            Assert.That(CampaignIndex.GetName(folder, Path.GetFileNameWithoutExtension(outcome.Path)), Is.EqualTo(displayName));
            Assert.That(Mw5SaveFile.Load(outcome.Path).Stamp, Is.Not.Null, "stamped as an editor save");
            Assert.That(SaveFixtures.Hash(save), Is.EqualTo(originalHash), "the original save must never change");
            foreach (var (op, verify) in checks)
            {
                Assert.DoesNotThrow(() => verify(back), op);
            }

            before.AssertUnchanged(back, touched);
        });

        if (seed % 4 == 0)
        {
            back.Company.CBills = 1_000 + seed;
            var second = back.Save(displayName: displayName + " (2)");
            var again = Mw5Document.Open(second.Path, Mechs);
            Assert.Multiple(() =>
            {
                Assert.That(second.CreatedNewFile, Is.False, "an editor save is updated in place");
                Assert.That(second.Path, Is.EqualTo(outcome.Path));
                Assert.That(File.Exists(outcome.Path + ".bak"), Is.True, "previous version kept as .bak");
                Assert.That(again.Company.CBills, Is.EqualTo(1_000 + seed));
                Assert.That(CampaignIndex.GetName(folder, Path.GetFileNameWithoutExtension(second.Path)), Is.EqualTo(displayName + " (2)"));
                Assert.That(SaveFixtures.Hash(save), Is.EqualTo(originalHash), "the original save must never change");
            });
        }
    }

    private delegate Action<Mw5Document>? Op(Mw5Document doc, Random rng, HashSet<string> touched);

    private static readonly (string Name, Op Apply)[] Operations =
    [
        ("CBills", (doc, rng, touched) =>
        {
            var value = rng.Next(4) == 0 ? (rng.Next(2) == 0 ? 0 : CompanyEditor.MaxCBills) : rng.NextInt64(1, CompanyEditor.MaxCBills);
            doc.Company.CBills = value;
            touched.Add("CBills");
            return back => Assert.That(back.Company.CBills, Is.EqualTo(value), "C-Bills");
        }),
        ("Reputation", (doc, rng, touched) =>
        {
            var value = rng.Next(0, 250_000);
            doc.Company.Reputation = value;
            touched.Add("Reputation");
            return back => Assert.That(back.Company.Reputation, Is.EqualTo(value), "reputation");
        }),
        ("CompanyName", (doc, rng, touched) =>
        {
            var value = Names[rng.Next(Names.Length)];
            doc.Company.CompanyName = value;
            touched.Add("CompanyName");
            return back => Assert.That(back.Company.CompanyName, Is.EqualTo(value.Trim()), "company name");
        }),
        ("CampaignDate", (doc, rng, touched) =>
        {
            if (doc.Company.CampaignDate is not { } date)
            {
                return null;
            }

            var target = date.AddDays(rng.Next(-60, 366));
            doc.Company.MoveCampaignDate(target);
            touched.Add("CampaignDate");
            return back => Assert.That(back.Company.CampaignDate, Is.EqualTo(target), "campaign date");
        }),
        ("AddMechs", (doc, rng, touched) =>
        {
            if (!doc.Hangar.CanAdd)
            {
                return null;
            }

            var pool = Mechs.Variants.Where(v => doc.Dlc.Owns(v.Dlc) && v.ModName is null && Mechs.FindStock(v.Asset) is not null).ToList();
            var picks = pool.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)).ToList();
            var copies = rng.Next(1, 3);
            var cold = ColdCount(doc);
            var result = doc.Hangar.AddToColdStorage(picks, copies);
            var added = result.Succeeded.Count * copies;
            touched.Add("Mechs");
            return back =>
            {
                Assert.That(result.Succeeded, Has.Count.EqualTo(picks.Count), "every owned stock variant is added");
                var removed = touched.Count(t => t.StartsWith("removed:", StringComparison.Ordinal));
                Assert.That(ColdCount(back), Is.GreaterThanOrEqualTo(cold + added - removed), "cold storage grew");
                foreach (var v in result.Succeeded)
                {
                    var mech = back.Hangar.Mechs.Last(m => m.Asset == v.Asset);
                    Assert.That(Weapons(HangarEditor.ReadLoadout(mech)), Is.EquivalentTo(Weapons(Mechs.FindStock(v.Asset)!)), v.Code);
                }
            };
        }),
        ("RemoveColdMech", (doc, rng, touched) =>
        {
            var cold = doc.Hangar.Mechs.Where(m => m.Location == MechLocation.ColdStorage && !m.IsNew && !touched.Contains($"mech:{m.ItemId}")).ToList();
            if (cold.Count == 0)
            {
                return null;
            }

            var pick = cold[rng.Next(cold.Count)];
            touched.Add($"mech:{pick.ItemId}");
            var result = doc.Hangar.RemoveFromColdStorage([pick]);
            touched.Add("Mechs");
            var gone = result.Succeeded.Count == 1;
            if (gone)
            {
                touched.Add($"removed:{pick.ItemId}");
            }

            return back => Assert.That(back.Hangar.Mechs.Any(m => m.ItemId == pick.ItemId), Is.EqualTo(!gone), "removed mech");
        }),
        ("RepairAll", (doc, rng, touched) =>
        {
            var ids = doc.Hangar.Mechs.Select(m => m.ItemId).ToList();
            doc.Hangar.Repair(doc.Hangar.Mechs);
            return back => Assert.That(back.Hangar.Mechs.Select(m => m.ItemId),
                Is.SupersetOf(ids.Where(id => !touched.Contains($"removed:{id}"))), "repair keeps every mech");
        }),
        ("StripWeapon", (doc, rng, touched) =>
        {
            var mech = doc.Hangar.Mechs.Where(m => m.IsEditable && m.Location == MechLocation.ColdStorage && !m.IsNew && !touched.Contains($"mech:{m.ItemId}"))
                .FirstOrDefault(m => HangarEditor.ReadLoadout(m).Weapons.Count(w => w.IsInstalledWeapon) >= 2);
            if (mech is null)
            {
                return null;
            }

            touched.Add($"mech:{mech.ItemId}");
            var loadout = HangarEditor.ReadLoadout(mech);
            var strip = loadout.Weapons.Where(w => w.IsInstalledWeapon).ElementAt(rng.Next(loadout.Weapons.Count(w => w.IsInstalledWeapon)));
            doc.Hangar.ApplyLoadout(mech, loadout with { Weapons = [.. loadout.Weapons.Select(w => w == strip ? w with { Name = "None", Type = "None" } : w)] });
            var expected = Weapons(loadout).Count - 1;
            touched.Add("Mechs");
            return back => Assert.That(Weapons(HangarEditor.ReadLoadout(back.Hangar.Mechs.Single(m => m.ItemId == mech.ItemId))), Has.Count.EqualTo(expected), "weapon stripped");
        }),
        ("AddItems", (doc, rng, touched) =>
        {
            if (!doc.Inventory.CanAddWeapons || !doc.Inventory.CanAddEquipment)
            {
                return null;
            }

            var picks = Items.Items.Where(i => !touched.Contains($"item:{i.Asset}")).OrderBy(_ => rng.Next()).Take(rng.Next(1, 6)).DistinctBy(i => i.Asset).ToList();
            picks.ForEach(i => touched.Add($"item:{i.Asset}"));
            var count = rng.Next(1, 40);
            var before = picks.ToDictionary(i => i.Asset, i => ItemCount(doc, i.Asset));
            var result = doc.Inventory.Add(picks, count);
            touched.Add("Inventory");
            return back =>
            {
                foreach (var item in result.Succeeded)
                {
                    Assert.That(ItemCount(back, item.Asset), Is.EqualTo(before[item.Asset] + count), item.Asset);
                }
            };
        }),
        ("RecountItem", (doc, rng, touched) =>
        {
            var items = doc.Inventory.Items.ToList();
            var free = items.Where(i => !touched.Contains($"item:{i.Asset}")).ToList();
            if (free.Count == 0)
            {
                return null;
            }

            var pick = free[rng.Next(free.Count)];
            touched.Add($"item:{pick.Asset}");
            var value = rng.Next(1, 500);
            var stacks = items.Where(i => i.Asset == pick.Asset && i.Health == pick.Health).ToList();
            doc.Inventory.SetCount(stacks, value);
            touched.Add("Inventory");
            return back => Assert.That(back.Inventory.Items.Where(i => i.Asset == pick.Asset && i.Health == pick.Health).Select(i => i.Count), Is.All.EqualTo(value), pick.Asset);
        }),
        ("RemoveItem", (doc, rng, touched) =>
        {
            var assets = doc.Inventory.Items.Where(i => !i.IsAmmo && !touched.Contains($"item:{i.Asset}")).Select(i => i.Asset).Distinct().ToList();
            if (assets.Count == 0)
            {
                return null;
            }

            var asset = assets[rng.Next(assets.Count)];
            touched.Add($"item:{asset}");
            doc.Inventory.Remove(doc.Inventory.Items.Where(i => i.Asset == asset));
            touched.Add("Inventory");
            return back => Assert.That(back.Inventory.Items.Any(i => i.Asset == asset), Is.False, asset);
        }),
        ("HirePilot", (doc, rng, touched) =>
        {
            if (!doc.Roster.CanHire)
            {
                return null;
            }

            var name = $"Recruit {rng.Next(10_000)}";
            var callsign = $"CS{rng.Next(1_000)}";
            var salary = rng.Next(0, 20_000);
            var hired = doc.Roster.Hire(doc.Roster.Pilots[rng.Next(doc.Roster.Pilots.Count)], name, callsign);
            hired.Salary = salary;
            var trait = PilotData.PilotTraits[rng.Next(PilotData.PilotTraits.Count)].Asset;
            hired.AddTrait(trait);
            touched.Add("Pilots");
            return back =>
            {
                var p = back.Roster.Pilots.Single(x => x.PersonaId == hired.PersonaId);
                Assert.That(p.FullName, Is.EqualTo(name));
                Assert.That(p.Callsign, Is.EqualTo(callsign));
                Assert.That(p.Salary, Is.EqualTo(salary));
                Assert.That(p.Traits, Does.Contain(trait));
            };
        }),
        ("PilotTrait", (doc, rng, touched) =>
        {
            if (doc.Roster.Pilots.Count == 0)
            {
                return null;
            }

            var pilot = doc.Roster.Pilots[rng.Next(doc.Roster.Pilots.Count)];
            var trait = PilotData.PilotTraits.Select(t => t.Asset).Where(t => !pilot.Traits.Contains(t)).OrderBy(_ => rng.Next()).FirstOrDefault();
            if (trait is null)
            {
                return null;
            }

            var id = pilot.PersonaId;
            Assert.That(pilot.AddTrait(trait), Is.True, "trait added");
            touched.Add("Pilots");
            return back => Assert.That(back.Roster.Pilots.First(p => p.PersonaId == id).Traits, Does.Contain(trait), "pilot trait");
        }),
        ("PilotSkills", (doc, rng, touched) =>
        {
            if (doc.Roster.Pilots.Count == 0)
            {
                return null;
            }

            var pilot = doc.Roster.Pilots[rng.Next(doc.Roster.Pilots.Count)];
            pilot.MaxCaps();
            pilot.Heal();
            var id = pilot.PersonaId;
            touched.Add("Pilots");
            return back =>
            {
                var p = back.Roster.Pilots.First(x => x.PersonaId == id);
                Assert.That(p.Skills.Where(s => s.HasCap).Select(s => s.Cap), Is.All.EqualTo(p.MaxSkillCap), "skill caps");
                Assert.That(p.IsHealthy, Is.True, "healed");
            };
        }),
        ("Factions", (doc, rng, touched) =>
        {
            if (doc.Factions.Standings.Count == 0)
            {
                return null;
            }

            touched.Add("Factions");
            if (rng.Next(2) == 0)
            {
                var all = rng.Next(FactionEditor.MinStanding, FactionEditor.MaxStanding + 1);
                doc.Factions.SetAll(all);
                return back => Assert.That(back.Factions.Standings.Select(f => f.Standing), Is.All.EqualTo(all), "all standings");
            }

            var faction = doc.Factions.Standings[rng.Next(doc.Factions.Standings.Count)];
            var value = rng.Next(FactionEditor.MinStanding, FactionEditor.MaxStanding + 1);
            faction.Standing = value;
            return back => Assert.That(back.Factions.Standings.Single(f => f.Faction == faction.Faction).Standing, Is.EqualTo(value), faction.Faction);
        }),
        ("MechTraitUnlock", (doc, rng, touched) =>
        {
            if (!doc.Traits.HasMechTraits)
            {
                return null;
            }

            var locked = PilotData.MechTraits.Select(t => t.Asset).Where(t => !doc.Traits.UnlockedMechTraits.Contains(t)).ToList();
            if (locked.Count == 0)
            {
                return null;
            }

            var trait = locked[rng.Next(locked.Count)];
            doc.Traits.UnlockMechTrait(trait);
            return back => Assert.That(back.Traits.UnlockedMechTraits, Does.Contain(trait), "mech trait unlocked");
        }),
    ];

    private static int ColdCount(Mw5Document doc) => doc.Hangar.Mechs.Count(m => m.Location == MechLocation.ColdStorage);

    private static int ItemCount(Mw5Document doc, string asset) => doc.Inventory.Items.Where(i => i.Asset == asset).Sum(i => i.Count);

    private static List<string> Weapons(StockLoadout loadout) =>
        [.. loadout.Weapons.Where(w => w.IsInstalledWeapon).Select(w => $"{w.Slot}:{w.Name}")];

    /// <summary>Values a scenario must leave alone unless one of its edits changed them.</summary>
    private sealed record Snapshot(long CBills, int Reputation, string Name, DateTime? Date, int Mechs, int Pilots, int Items, int[] Standings)
    {
        public static Snapshot Of(Mw5Document d) => new(
            d.Company.CBills, d.Company.Reputation, d.Company.CompanyName, d.Company.CampaignDate,
            d.Hangar.Mechs.Count, d.Roster.Pilots.Count, d.Inventory.Items.Sum(i => i.Count), [.. d.Factions.Standings.Select(f => f.Standing)]);

        public void AssertUnchanged(Mw5Document back, HashSet<string> touched)
        {
            var now = Of(back);
            if (!touched.Contains("CBills")) Assert.That(now.CBills, Is.EqualTo(CBills), "untouched C-Bills");
            if (!touched.Contains("Reputation")) Assert.That(now.Reputation, Is.EqualTo(Reputation), "untouched reputation");
            if (!touched.Contains("CompanyName")) Assert.That(now.Name, Is.EqualTo(Name), "untouched name");
            if (!touched.Contains("CampaignDate")) Assert.That(now.Date, Is.EqualTo(Date), "untouched date");
            if (!touched.Contains("Mechs")) Assert.That(now.Mechs, Is.EqualTo(Mechs), "untouched mech count");
            if (!touched.Contains("Pilots")) Assert.That(now.Pilots, Is.EqualTo(Pilots), "untouched pilot count");
            if (!touched.Contains("Inventory")) Assert.That(now.Items, Is.EqualTo(Items), "untouched inventory");
            if (!touched.Contains("Factions")) Assert.That(now.Standings, Is.EqualTo(Standings), "untouched standings");
        }
    }
}
