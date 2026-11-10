using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Every Mercenaries editor operation against every kind of save the game writes. Each case copies one
/// original save (and its Campaign.json) into a throwaway folder, applies the operation, writes the save,
/// reopens the written file from disk, verifies it, and deletes the folder.
/// </summary>
[TestFixture]
[Category("Headless")]
[Category("Permutations")]
public class MercsPermutationTests
{
    public static readonly string[] SaveKinds =
    [
        "Manual",
        "AutoSave (Entered System)",
        "AutoSave (Mission)",
        "AutoSave (Finished Mission)",
        "AutoSave (Paid Bill)",
        "ReloadSave",
    ];

    public static readonly string[] Operations =
    [
        "Company", "AddMechs", "RemoveMechs", "LoadoutRepairReset", "Inventory",
        "Pilots", "Factions", "MechTraits", "SaveInPlace", "Everything",
    ];

    private static readonly MechCatalog Mechs = MechCatalog.LoadBuiltIn();
    private static readonly ItemCatalog Items = ItemCatalog.LoadBuiltIn();
    private static readonly PilotCatalog Pilots = PilotCatalog.LoadBuiltIn();

    private string _folder = null!;
    private string _save = null!;
    private string _originalHash = null!;

    /// <summary>
    /// Every save kind gets the combined check (<c>Everything</c>: all operations in one save, each verified after
    /// reopening) and <c>SaveInPlace</c> (the kind-specific write-back); each single operation also runs once on
    /// its own on the manual save, so a failure names the feature. (The full kind x operation product was judged
    /// excessive: M5SE-T53.)
    /// </summary>
    public static IEnumerable<TestCaseData> Matrix() =>
        (from kind in SaveKinds
         from op in new[] { "Everything", "SaveInPlace" }
         select new TestCaseData(kind, op).SetName($"Mercs [{kind}] {op}"))
        .Concat(
            from op in Operations.Except(["Everything", "SaveInPlace"])
            select new TestCaseData("Manual", op).SetName($"Mercs [Manual] {op}"));

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [TestCaseSource(nameof(Matrix))]
    public void Operation_round_trips_on_save_kind(string kind, string op)
    {
        (_folder, _save) = SaveFixtures.MercsSaveOfKind(kind);
        _originalHash = SaveFixtures.Hash(_save);

        switch (op)
        {
            case "Company": Company(); break;
            case "AddMechs": AddMechs(); break;
            case "RemoveMechs": RemoveMechs(); break;
            case "LoadoutRepairReset": LoadoutRepairReset(); break;
            case "Inventory": Inventory(); break;
            case "Pilots": PilotsOp(); break;
            case "Factions": Factions(); break;
            case "MechTraits": MechTraits(); break;
            case "SaveInPlace": SaveInPlace(); break;
            case "Everything": Everything(); break;
            default: Assert.Fail(op); break;
        }

        Assert.That(SaveFixtures.Hash(_save), Is.EqualTo(_originalHash), "the original save must never change");
    }

    private void Company()
    {
        var doc = Open();
        var date = doc.Company.CampaignDate;
        doc.Company.CBills = 777_777_777;
        doc.Company.Reputation = 1_234;
        doc.Company.CompanyName = "Permutation Lancers";
        if (date is { } d)
        {
            doc.Company.MoveCampaignDate(d.AddDays(45));
        }

        var back = SaveAndReopen(doc, "Perm Company");
        Assert.Multiple(() =>
        {
            Assert.That(back.Company.CBills, Is.EqualTo(777_777_777));
            Assert.That(back.Company.Reputation, Is.EqualTo(1_234));
            Assert.That(back.Company.CompanyName, Is.EqualTo("Permutation Lancers"));
            if (date is { } d2)
            {
                Assert.That(back.Company.CampaignDate, Is.EqualTo(d2.AddDays(45)));
            }
        });
    }

    private void AddMechs()
    {
        var doc = Open();
        var variants = StockVariants(doc, 3);
        var before = Cold(doc).Count;
        var result = doc.Hangar.AddToColdStorage(variants, copiesEach: 2);
        var back = SaveAndReopen(doc, "Perm Add Mechs");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Has.Count.EqualTo(variants.Count));
            Assert.That(Cold(back), Has.Count.EqualTo(before + variants.Count * 2));
            foreach (var v in variants)
            {
                var added = Cold(back).Where(m => m.Asset == v.Asset).ToList();
                Assert.That(added, Has.Count.GreaterThanOrEqualTo(2), v.Code);
                Assert.That(Weapons(HangarEditor.ReadLoadout(added[^1])), Is.EquivalentTo(Weapons(Mechs.FindStock(v.Asset)!)), v.Code);
            }
        });
    }

    private void RemoveMechs()
    {
        var doc = Open();
        var variant = StockVariants(doc, 1)[0];
        doc.Hangar.AddToColdStorage([variant], copiesEach: 3);
        var mid = SaveAndReopen(doc, "Perm Remove 1");
        var targets = Cold(mid).Where(m => m.Asset == variant.Asset).Take(3).ToList();
        var ids = targets.Select(m => m.ItemId).ToHashSet();
        var removed = mid.Hangar.RemoveFromColdStorage(targets);
        var back = Reopen(mid.Save(displayName: "Perm Remove 2"));

        Assert.Multiple(() =>
        {
            Assert.That(removed.Succeeded, Has.Count.EqualTo(3));
            Assert.That(back.Hangar.Mechs.Any(m => ids.Contains(m.ItemId)), Is.False);
        });
    }

    private void LoadoutRepairReset()
    {
        var doc = Open();
        var variant = StockVariants(doc, 1, minWeapons: 2)[0];
        doc.Hangar.AddToColdStorage([variant]);
        var mech = Cold(doc).Last(m => m.Asset == variant.Asset);
        var stock = HangarEditor.ReadLoadout(mech);
        var strip = stock.Weapons.First(w => w.IsInstalledWeapon);
        doc.Hangar.ApplyLoadout(mech, stock with { Weapons = [.. stock.Weapons.Select(w => w == strip ? w with { Name = "None", Type = "None" } : w)] });
        var edited = SaveAndReopen(doc, "Perm Loadout");
        var reloaded = edited.Hangar.Mechs.Single(m => m.ItemId == mech.ItemId);
        Assert.That(Weapons(HangarEditor.ReadLoadout(reloaded)), Has.Count.EqualTo(Weapons(stock).Count - 1), "weapon removed");

        edited.Hangar.Repair(edited.Hangar.Mechs);
        edited.Hangar.ResetToStock([reloaded]);
        var back = Reopen(edited.Save(displayName: "Perm Loadout Reset"));
        Assert.That(Weapons(HangarEditor.ReadLoadout(back.Hangar.Mechs.Single(m => m.ItemId == mech.ItemId))),
            Is.EquivalentTo(Weapons(Mechs.FindStock(variant.Asset)!)), "reset restores the factory weapons");
    }

    private void Inventory()
    {
        var doc = Open();
        Assume.That(doc.Inventory.CanAddWeapons && doc.Inventory.CanAddEquipment, Is.True, "save has no inventory lists");
        var weapon = Items.Items.First(i => i.Kind == ItemKind.Weapon);
        var equipment = Items.Items.First(i => i.Kind == ItemKind.Equipment);
        var ammo = Items.Items.First(i => i.Kind == ItemKind.Ammo);
        var before = Count(doc, weapon.Asset);
        doc.Inventory.Add([weapon, equipment, ammo], 3);
        var added = SaveAndReopen(doc, "Perm Inventory 1");
        Assert.Multiple(() =>
        {
            Assert.That(Count(added, weapon.Asset), Is.EqualTo(before + 3), "weapon");
            Assert.That(Count(added, equipment.Asset), Is.GreaterThanOrEqualTo(3), "equipment");
            Assert.That(Count(added, ammo.Asset), Is.GreaterThanOrEqualTo(3), "ammo");
        });

        added.Inventory.SetCount(added.Inventory.Items.Where(i => i.Asset == ammo.Asset), 11);
        added.Inventory.Remove(added.Inventory.Items.Where(i => i.Asset == equipment.Asset));
        var back = Reopen(added.Save(displayName: "Perm Inventory 2"));
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Items.Where(i => i.Asset == ammo.Asset).Select(i => i.Count), Is.All.EqualTo(11), "recounted");
            Assert.That(back.Inventory.Items.Any(i => i.Asset == equipment.Asset), Is.False, "removed");
        });
    }

    private void PilotsOp()
    {
        var doc = Open();
        Assume.That(doc.Roster.CanHire, Is.True, "save has no roster to hire into");
        var count = doc.Roster.Pilots.Count;
        var hired = doc.Roster.Hire(doc.Roster.Pilots[0], "Perm Recruit", "Matrix");
        hired.Salary = 4_321;
        hired.Bio = "Permutation pilot.";
        var trait = Pilots.PilotTraits.FirstOrDefault(t => !hired.Traits.Contains(t.Asset));
        if (trait is not null)
        {
            hired.AddTrait(trait.Asset);
        }

        foreach (var skill in hired.Skills.Where(s => s.HasCap))
        {
            skill.Cap = 6;
        }

        hired.MaxCaps();
        var back = SaveAndReopen(doc, "Perm Pilots 1");
        var pilot = back.Roster.Pilots.SingleOrDefault(p => p.PersonaId == hired.PersonaId);
        Assert.That(pilot, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(back.Roster.Pilots, Has.Count.EqualTo(count + 1));
            Assert.That(pilot!.FullName, Is.EqualTo("Perm Recruit"));
            Assert.That(pilot.Callsign, Is.EqualTo("Matrix"));
            Assert.That(pilot.Salary, Is.EqualTo(4_321));
            Assert.That(pilot.Bio, Is.EqualTo("Permutation pilot."));
            Assert.That(pilot.Skills.Where(s => s.HasCap).Select(s => s.Cap), Is.All.EqualTo(Pilot.MaxSkillCap));
            if (trait is not null)
            {
                Assert.That(pilot.Traits, Does.Contain(trait.Asset));
            }
        });

        back.Roster.Release([pilot!]);
        var after = Reopen(back.Save(displayName: "Perm Pilots 2"));
        Assert.That(after.Roster.Pilots.Any(p => p.PersonaId == hired.PersonaId), Is.False, "released");
    }

    private void Factions()
    {
        var doc = Open();
        Assume.That(doc.Factions.Standings, Is.Not.Empty, "save has no faction standings");
        doc.Factions.SetAll(-40);
        var back = SaveAndReopen(doc, "Perm Factions");
        Assert.That(back.Factions.Standings.Select(f => f.Standing), Is.All.EqualTo(-40));
    }

    private void MechTraits()
    {
        var doc = Open();
        Assume.That(doc.Traits.HasMechTraits, Is.True, "save has no mech-trait unlocks");
        var trait = Pilots.MechTraits.First(t => !doc.Traits.UnlockedMechTraits.Contains(t.Asset)).Asset;
        doc.Traits.UnlockMechTrait(trait);
        var cantinas = doc.Traits.HasCantinas ? (bool?)!doc.Traits.CantinasLocked : null;
        if (cantinas is { } locked)
        {
            doc.Traits.CantinasLocked = locked;
        }

        var back = SaveAndReopen(doc, "Perm Traits 1");
        Assert.That(back.Traits.UnlockedMechTraits, Does.Contain(trait));
        if (cantinas is { } c)
        {
            Assert.That(back.Traits.CantinasLocked, Is.EqualTo(c));
        }

        back.Traits.LockMechTrait(trait);
        var after = Reopen(back.Save(displayName: "Perm Traits 2"));
        Assert.That(after.Traits.UnlockedMechTraits, Does.Not.Contain(trait));
    }

    private void SaveInPlace()
    {
        var doc = Open();
        var first = doc.Save(displayName: "Perm In Place");
        var again = Reopen(first);
        again.Company.CBills = 42;
        var second = again.Save(displayName: "Perm In Place 2");
        Assert.Multiple(() =>
        {
            Assert.That(first.CreatedNewFile, Is.True);
            Assert.That(second.CreatedNewFile, Is.False, "an editor save is updated in place");
            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(File.Exists(first.Path + ".bak"), Is.True, "previous version kept");
            Assert.That(Reopen(second).Company.CBills, Is.EqualTo(42));
            Assert.That(CampaignIndex.GetName(_folder, Path.GetFileNameWithoutExtension(second.Path)), Is.EqualTo("Perm In Place 2"));
        });
    }

    private void Everything()
    {
        var doc = Open();
        doc.Company.CBills = 555_000_000;
        var variants = StockVariants(doc, 2);
        doc.Hangar.AddToColdStorage(variants);
        doc.Hangar.Repair(doc.Hangar.Mechs);
        var weapon = Items.Items.First(i => i.Kind == ItemKind.Weapon);
        var weapons = Count(doc, weapon.Asset);
        if (doc.Inventory.CanAddWeapons)
        {
            doc.Inventory.Add([weapon], 2);
        }

        Pilot? hired = doc.Roster.CanHire ? doc.Roster.Hire(doc.Roster.Pilots[0], "Everything Pilot", "All") : null;
        if (doc.Factions.Standings.Count > 0)
        {
            doc.Factions.SetAll(60);
        }

        var cold = Cold(doc).Count;
        var back = SaveAndReopen(doc, "Perm Everything");
        Assert.Multiple(() =>
        {
            Assert.That(back.Company.CBills, Is.EqualTo(555_000_000));
            Assert.That(Cold(back), Has.Count.EqualTo(cold));
            if (back.Inventory.CanAddWeapons)
            {
                Assert.That(Count(back, weapon.Asset), Is.EqualTo(weapons + 2));
            }

            if (hired is not null)
            {
                Assert.That(back.Roster.Pilots.Any(p => p.PersonaId == hired.PersonaId), Is.True);
            }

            if (back.Factions.Standings.Count > 0)
            {
                Assert.That(back.Factions.Standings.Select(f => f.Standing), Is.All.EqualTo(60));
            }
        });
    }

    private Mw5Document Open() => Mw5Document.Open(_save, Mechs);

    /// <summary>Saves an original as a new listed save and checks the safety guarantees every time.</summary>
    private Mw5Document SaveAndReopen(Mw5Document doc, string name)
    {
        var outcome = doc.Save(displayName: name);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Path, Is.Not.EqualTo(_save), "never written over the original");
            Assert.That(outcome.RegisteredInCampaign, Is.True, "listed in the load menu");
            Assert.That(CampaignIndex.GetName(_folder, Path.GetFileNameWithoutExtension(outcome.Path)), Is.EqualTo(name));
            Assert.That(Mw5SaveFile.Load(outcome.Path).Stamp, Is.Not.Null, "stamped as an editor save");
        });
        return Reopen(outcome);
    }

    private static Mw5Document Reopen(SaveOutcome outcome) => Mw5Document.Open(outcome.Path, Mechs);

    private static List<MechVariant> StockVariants(Mw5Document doc, int count, int minWeapons = 0) =>
        [.. Mechs.Variants
            .Where(v => doc.Dlc.Owns(v.Dlc) && v.ModName is null && Mechs.FindStock(v.Asset) is { } s && s.Weapons.Count(w => w.IsInstalledWeapon) >= minWeapons)
            .Take(count)];

    private static List<MechRecord> Cold(Mw5Document doc) => [.. doc.Hangar.Mechs.Where(m => m.Location == MechLocation.ColdStorage)];

    private static int Count(Mw5Document doc, string asset) => doc.Inventory.Items.Where(i => i.Asset == asset).Sum(i => i.Count);

    private static List<string> Weapons(StockLoadout loadout) =>
        [.. loadout.Weapons.Where(w => w.IsInstalledWeapon).Select(w => $"{w.Slot}:{w.Name}")];
}
