using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Every Clans editor operation against every kind of save the game writes (manual, autosave, mission
/// autosave, reload checkpoint). Each case copies one original save and the load-menu indexes into a
/// throwaway folder, applies the operation, writes the save, reopens it from disk, verifies it, and
/// deletes the folder.
/// </summary>
[TestFixture]
[Category("Headless")]
[Category("Permutations")]
public class ClansPermutationTests
{
    public static readonly string[] SaveKinds = ["ManualSave", "AutoSave", "AutoSave (mission)", "ReloadSave"];

    public static readonly string[] Operations =
    [
        "Currencies", "SalvageAndCrew", "MechExperience", "AddNewItem", "RecountAndRemoveItems",
        "DuplicateRenameRepair", "RemoveMechs", "SaveInPlace", "Everything",
    ];

    private const string Currency = "KelCurrencyItemDataAsset";
    private const string Crew = "KelCrewItemDataAsset";
    private const string Equipment = "KelEquipmentItemDataAsset";

    private string _folder = null!;
    private string _save = null!;
    private string _originalHash = null!;

    /// <summary>
    /// Every save kind gets the combined check (<c>Everything</c>: all operations in one save, each verified after
    /// reopening) and <c>SaveInPlace</c> (the kind-specific write-back); each single operation also runs once on
    /// its own on the manual save, so a failure names the feature. (The full kind x operation product was judged
    /// excessive: M5SE-T53.) Set MW5_FULL_MATRIX=1 to run the full kind x operation product instead.
    /// </summary>
    public static IEnumerable<TestCaseData> Matrix() =>
        Environment.GetEnvironmentVariable("MW5_FULL_MATRIX") == "1"
            // Opt-in: the full save-kind x operation product (every operation on every kind).
            ? from kind in SaveKinds
              from op in Operations
              select new TestCaseData(kind, op).SetName($"Clans [{kind}] {op}")
            : (from kind in SaveKinds
         from op in new[] { "Everything", "SaveInPlace" }
         select new TestCaseData(kind, op).SetName($"Clans [{kind}] {op}"))
        .Concat(
            from op in Operations.Except(["Everything", "SaveInPlace"])
            select new TestCaseData("ManualSave", op).SetName($"Clans [ManualSave] {op}"));

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [TestCaseSource(nameof(Matrix))]
    public void Operation_round_trips_on_save_kind(string kind, string op)
    {
        (_folder, _save) = SaveFixtures.ClansSaveOfKind(kind);
        _originalHash = SaveFixtures.Hash(_save);

        switch (op)
        {
            case "Currencies": Currencies(); break;
            case "SalvageAndCrew": SalvageAndCrew(); break;
            case "MechExperience": MechExperience(); break;
            case "AddNewItem": AddNewItem(); break;
            case "RecountAndRemoveItems": RecountAndRemoveItems(); break;
            case "DuplicateRenameRepair": DuplicateRenameRepair(); break;
            case "RemoveMechs": RemoveMechs(); break;
            case "SaveInPlace": SaveInPlace(); break;
            case "Everything": Everything(); break;
            default: Assert.Fail(op); break;
        }

        Assert.That(SaveFixtures.Hash(_save), Is.EqualTo(_originalHash), "the original save must never change");
    }

    private void Currencies()
    {
        var doc = ClansDocument.Load(_save);
        doc.Inventory.SetTotal(Currency, "Kerenskies_CurrencyItem", 31_415_926);
        doc.Inventory.SetTotal(Currency, "Merits_CurrencyItem", 2_718);
        doc.Inventory.SetTotal(Currency, "Honor_CurrencyItem", 1_618);
        doc.Progress.HonorExperience = 1_618;
        var back = SaveAndReopen(doc, "Perm Currencies");
        Assert.Multiple(() =>
        {
            Assert.That(back.Currency("Kerenskies_CurrencyItem"), Is.EqualTo(31_415_926));
            Assert.That(back.Currency("Merits_CurrencyItem"), Is.EqualTo(2_718));
            Assert.That(back.Currency("Honor_CurrencyItem"), Is.EqualTo(1_618));
            Assert.That(back.Progress.HonorExperience, Is.EqualTo(1_618));
            var listed = SaveLibrary.List(_folder).Single(s => s.Path == back.Path);
            Assert.That(listed.Kerenskies, Is.EqualTo(31_415_926), "the load menu shows the new Kerenskies");
        });
    }

    private void SalvageAndCrew()
    {
        var doc = ClansDocument.Load(_save);
        string[] salvage = ["Salvage_MechComponents", "Salvage_WeaponComponents", "Salvage_EquipmentComponents"];
        string[] crew = ["Technician_CrewItem", "Scientist_CrewItem", "TechnicianUpgrade_CrewItem", "ScientistUpgrade_CrewItem", "SalvageUpgrade_CrewItem"];
        foreach (var (asset, i) in salvage.Select((a, i) => (a, i)))
        {
            doc.Inventory.SetTotal(Currency, asset, 100 + i);
        }

        foreach (var (asset, i) in crew.Select((a, i) => (a, i)))
        {
            doc.Inventory.SetTotal(Crew, asset, 20 + i);
        }

        doc.Progress.TechniciansAvailable = 20;
        var back = SaveAndReopen(doc, "Perm Salvage Crew");
        Assert.Multiple(() =>
        {
            foreach (var (asset, i) in salvage.Select((a, i) => (a, i)))
            {
                Assert.That(back.Inventory.Total(asset), Is.EqualTo(100 + i), asset);
            }

            foreach (var (asset, i) in crew.Select((a, i) => (a, i)))
            {
                Assert.That(back.Inventory.Total(asset), Is.EqualTo(20 + i), asset);
            }

            if (back.Progress.TechniciansAvailable is not null)
            {
                Assert.That(back.Progress.TechniciansAvailable, Is.EqualTo(20));
            }
        });
    }

    private void MechExperience()
    {
        var doc = ClansDocument.Load(_save);
        var mxp = doc.Inventory.Stacks.Where(s => s.Category == StackCategory.MechXp).Select(s => s.AssetName).Distinct().ToList();
        Assume.That(mxp, Is.Not.Empty, "save has no mech experience yet");
        foreach (var asset in mxp)
        {
            doc.Inventory.SetTotal(Currency, asset, 5_000);
        }

        var back = SaveAndReopen(doc, "Perm MXP");
        Assert.That(mxp.Select(back.Inventory.Total), Is.All.EqualTo(5_000));
    }

    private void AddNewItem()
    {
        // Campaigns often already hold a stack of every item, so make room first: remove an item's stacks,
        // save, then add it back from nothing, which creates a brand-new inventory actor.
        var doc = ClansDocument.Load(_save);
        var asset = doc.Inventory.Stacks.First(s => s.Category == StackCategory.Equipment).AssetName;
        doc.Inventory.RemoveStacks(doc.Inventory.Stacks.Where(s => s.AssetName == asset));
        var empty = SaveAndReopen(doc, "Perm Add Item 1");
        Assert.That(empty.Inventory.Stacks.Any(s => s.AssetName == asset), Is.False, "item removed first");
        var actors = empty.Inventory.Actors.Count;

        empty.Inventory.AddStack(Equipment, asset, 7);
        var back = ClansDocument.Load(empty.Save("Perm Add Item 2").Path);
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Total(asset), Is.EqualTo(7), asset);
            Assert.That(back.Inventory.Actors, Has.Count.EqualTo(actors + 1), "a new inventory actor was created");
            var ids = back.Inventory.Actors.Select(a => a.InventoryId).Where(g => g is not null).ToList();
            Assert.That(ids, Is.Unique, "the new stack gets its own inventory id");
        });
    }

    private void RecountAndRemoveItems()
    {
        var doc = ClansDocument.Load(_save);
        var equipment = doc.Inventory.Stacks.Where(s => s.Category == StackCategory.Equipment).Select(s => s.AssetName).Distinct().ToList();
        Assume.That(equipment, Has.Count.GreaterThanOrEqualTo(2), "save has too little equipment");
        doc.Inventory.SetTotal(Equipment, equipment[0], 13);
        doc.Inventory.RemoveStacks(doc.Inventory.Stacks.Where(s => s.AssetName == equipment[1]));
        var back = SaveAndReopen(doc, "Perm Items");
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Total(equipment[0]), Is.EqualTo(13), "recounted");
            Assert.That(back.Inventory.Stacks.Any(s => s.AssetName == equipment[1]), Is.False, "removed");
        });
    }

    private void DuplicateRenameRepair()
    {
        var doc = ClansDocument.Load(_save);
        var count = doc.Inventory.Mechs.Count();
        Assume.That(count, Is.GreaterThan(0), "save has no mechs");
        var source = doc.Inventory.Mechs.First();
        doc.Inventory.DuplicateMechs([source], copies: 2);
        var copies = doc.Inventory.Mechs.Skip(count).ToList();
        copies[0].CustomName = "Perm Copy";
        InventoryEditor.RepairMechs(doc.Inventory.Mechs);
        var ids = copies.Select(m => m.InventoryId).ToHashSet();
        var back = SaveAndReopen(doc, "Perm Mechs");
        var reloaded = back.Inventory.Mechs.Where(m => ids.Contains(m.InventoryId)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(count + 2));
            Assert.That(reloaded.Select(m => m.Variant), Is.All.EqualTo(source.Variant));
            Assert.That(reloaded.All(m => m.Equipment.SequenceEqual(source.Equipment)), Is.True, "copies keep the loadout");
            Assert.That(reloaded.Any(m => m.CustomName == "Perm Copy"), Is.True, "rename");
            Assert.That(back.Inventory.Mechs.Any(m => m.IsDamaged), Is.False, "all repaired");
        });
    }

    private void RemoveMechs()
    {
        var doc = ClansDocument.Load(_save);
        var count = doc.Inventory.Mechs.Count();
        Assume.That(count, Is.GreaterThan(0), "save has no mechs");
        doc.Inventory.DuplicateMechs([doc.Inventory.Mechs.First()], copies: 2);
        var ids = doc.Inventory.Mechs.Skip(count).Select(m => m.InventoryId).ToHashSet();
        var mid = SaveAndReopen(doc, "Perm Remove 1");
        var removed = mid.Inventory.RemoveMechs(mid.Inventory.Mechs.Where(m => ids.Contains(m.InventoryId)));
        var kept = mid.Inventory.RemoveMechs(mid.Inventory.Mechs.ToList());
        var back = ClansDocument.Load(mid.Save("Perm Remove 2").Path);
        Assert.Multiple(() =>
        {
            Assert.That(removed.Succeeded, Has.Count.EqualTo(2), "unreferenced copies are removed");
            Assert.That(back.Inventory.Mechs.Any(m => ids.Contains(m.InventoryId)), Is.False);
            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(count - kept.Succeeded.Count), "only unreferenced mechs go");
        });
    }

    private void SaveInPlace()
    {
        var doc = ClansDocument.Load(_save);
        var first = doc.Save("Perm In Place");
        var again = ClansDocument.Load(first.Path);
        again.Inventory.SetTotal(Currency, "Merits_CurrencyItem", 99);
        var second = again.Save("Perm In Place 2");
        Assert.Multiple(() =>
        {
            Assert.That(first.CreatedNewFile, Is.True);
            Assert.That(second.CreatedNewFile, Is.False, "an editor save is updated in place");
            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(File.Exists(first.Path + ".bak"), Is.True);
            var back = ClansDocument.Load(second.Path);
            Assert.That(back.Currency("Merits_CurrencyItem"), Is.EqualTo(99));
            Assert.That(back.DisplayName(), Is.EqualTo("Perm In Place 2"));
        });
    }

    private void Everything()
    {
        var doc = ClansDocument.Load(_save);
        var count = doc.Inventory.Mechs.Count();
        doc.Inventory.SetTotal(Currency, "Kerenskies_CurrencyItem", 88_000_000);
        doc.Inventory.SetTotal(Currency, "Salvage_WeaponComponents", 444);
        doc.Inventory.AddStack(Equipment, "Clan_ERLargeLaser", 2);
        var lasers = doc.Inventory.Total("Clan_ERLargeLaser");
        if (count > 0)
        {
            doc.Inventory.DuplicateMechs([doc.Inventory.Mechs.First()]);
        }

        InventoryEditor.RepairMechs(doc.Inventory.Mechs);
        var back = SaveAndReopen(doc, "Perm Everything");
        Assert.Multiple(() =>
        {
            Assert.That(back.Currency("Kerenskies_CurrencyItem"), Is.EqualTo(88_000_000));
            Assert.That(back.Inventory.Total("Salvage_WeaponComponents"), Is.EqualTo(444));
            Assert.That(back.Inventory.Total("Clan_ERLargeLaser"), Is.EqualTo(lasers));
            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(count > 0 ? count + 1 : 0));
            Assert.That(back.Inventory.Mechs.Any(m => m.IsDamaged), Is.False);
        });
    }

    /// <summary>Saves an original as a new listed save and checks the safety guarantees every time.</summary>
    private ClansDocument SaveAndReopen(ClansDocument doc, string name)
    {
        var outcome = doc.Save(name);
        var newId = Path.GetFileNameWithoutExtension(outcome.Path).Split('_')[1];
        Assert.Multiple(() =>
        {
            Assert.That(outcome.CreatedNewFile, Is.True);
            Assert.That(outcome.Path, Is.Not.EqualTo(_save), "never written over the original");
            Assert.That(outcome.RegisteredInLoadMenu, Is.True, "listed in the load menu");
            Assert.That(EditorStamp.Read(Ue5SaveFile.Load(outcome.Path).Properties), Is.Not.Null, "stamped");
            foreach (var index in SaveIndex.Files(_folder))
            {
                Assert.That(Ue5SaveFile.Load(index).ToPayload().AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(newId)), Is.GreaterThanOrEqualTo(0),
                    $"{Path.GetFileName(index)} lists the new save");
            }
        });
        var back = ClansDocument.Load(outcome.Path);
        Assert.That(back.DisplayName(), Is.EqualTo(name));
        return back;
    }
}
