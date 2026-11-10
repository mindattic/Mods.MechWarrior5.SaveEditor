using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// MechWarrior 5: Clans, end to end: every feature is applied to a copy of the real save folder, written
/// as the game loads it (compressed, listed in every UserSaveMetaData index), then reopened from disk
/// and verified there.
/// </summary>
[TestFixture]
[Category("Headless")]
public class ClansEndToEndTests
{
    private const string Currency = "KelCurrencyItemDataAsset";
    private const string Crew = "KelCrewItemDataAsset";
    private const string Equipment = "KelEquipmentItemDataAsset";

    private string _folder = null!;
    private string _save = null!;
    private string _originalHash = null!;
    private Dictionary<string, string> _indexHashes = null!;

    [SetUp]
    public void SetUp()
    {
        (_folder, _save) = SaveFixtures.ClansFolderCopy();
        _originalHash = SaveFixtures.Hash(_save);
        _indexHashes = SaveIndex.Files(_folder).ToDictionary(f => f, SaveFixtures.Hash);
    }

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [Test]
    public void Every_real_save_and_index_reads_back_byte_for_byte()
    {
        var failures = new List<string>();
        foreach (var path in SaveFixtures.AllClansFiles())
        {
            try
            {
                var file = Ue5SaveFile.Load(path); // verifies the payload round-trips
                Assert.That(GzipChunkArchive.Unpack(file.ToFileBytes()), Is.EqualTo(GzipChunkArchive.Unpack(File.ReadAllBytes(path))), path);
            }
            catch (SaveFormatException e)
            {
                failures.Add($"{Path.GetFileName(path)}: {e.Message}");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void Resource_edits_persist_and_mirrored_progress_follows()
    {
        var doc = ClansDocument.Load(_save);
        doc.Inventory.SetTotal(Currency, "Kerenskies_CurrencyItem", 50_000_000);
        doc.Inventory.SetTotal(Currency, "Merits_CurrencyItem", 4_321);
        doc.Inventory.SetTotal(Currency, "Honor_CurrencyItem", 2_500);
        doc.Progress.HonorExperience = 2_500;
        doc.Inventory.SetTotal(Currency, "Salvage_MechComponents", 777);
        doc.Inventory.SetTotal(Crew, "Technician_CrewItem", 30);
        doc.Progress.TechniciansAvailable = 30;
        var mxp = doc.Inventory.Stacks.FirstOrDefault(s => s.Category == StackCategory.MechXp)?.AssetName;
        if (mxp is not null)
        {
            doc.Inventory.SetTotal(Currency, mxp, 9_999);
        }

        var back = Reopen(doc.Save("Headless Resources"));

        Assert.Multiple(() =>
        {
            Assert.That(back.Currency("Kerenskies_CurrencyItem"), Is.EqualTo(50_000_000));
            Assert.That(back.Currency("Merits_CurrencyItem"), Is.EqualTo(4_321));
            Assert.That(back.Currency("Honor_CurrencyItem"), Is.EqualTo(2_500));
            Assert.That(back.Progress.HonorExperience, Is.EqualTo(2_500));
            Assert.That(back.Inventory.Total("Salvage_MechComponents"), Is.EqualTo(777));
            Assert.That(back.Inventory.Total("Technician_CrewItem"), Is.EqualTo(30));
            Assert.That(back.Progress.TechniciansAvailable, Is.EqualTo(30));
            if (mxp is not null)
            {
                Assert.That(back.Inventory.Total(mxp), Is.EqualTo(9_999));
            }
        });
    }

    [Test]
    public void Weapons_are_added_recounted_and_removed()
    {
        var doc = ClansDocument.Load(_save);
        const string NewItem = "Clan_GaussRifle";
        var before = doc.Inventory.Total(NewItem);
        var existing = doc.Inventory.Stacks.First(s => s.Category == StackCategory.Equipment && s.AssetName != NewItem).AssetName;

        doc.Inventory.AddStack(Equipment, NewItem, 4);
        doc.Inventory.SetTotal(Equipment, existing, 12);
        var added = Reopen(doc.Save("Headless Weapons 1"));
        Assert.Multiple(() =>
        {
            Assert.That(added.Inventory.Total(NewItem), Is.EqualTo(before + 4), "weapon added");
            Assert.That(added.Inventory.Total(existing), Is.EqualTo(12), "stack recounted");
        });

        added.Inventory.RemoveStacks(added.Inventory.Stacks.Where(s => s.AssetName == NewItem));
        var removed = Reopen(added.Save("Headless Weapons 2"));
        Assert.That(removed.Inventory.Stacks.Any(s => s.AssetName == NewItem), Is.False, "weapon removed");
    }

    [Test]
    public void Mechs_are_duplicated_renamed_repaired_and_removed()
    {
        var doc = ClansDocument.Load(_save);
        var count = doc.Inventory.Mechs.Count();
        var source = doc.Inventory.Mechs.First();

        doc.Inventory.DuplicateMechs([source], copies: 2);
        var copies = doc.Inventory.Mechs.Skip(count).ToList();
        copies[0].CustomName = "Headless Copy";
        var copyIds = copies.Select(m => m.InventoryId).ToHashSet();
        doc.Inventory.RepairMechs(doc.Inventory.Mechs);
        var back = Reopen(doc.Save("Headless Mechs 1"));

        var reloadedCopies = back.Inventory.Mechs.Where(m => copyIds.Contains(m.InventoryId)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(count + 2), "two copies added");
            Assert.That(reloadedCopies, Has.Count.EqualTo(2));
            Assert.That(reloadedCopies.All(m => m.Variant == source.Variant), Is.True, "copies keep the variant");
            Assert.That(reloadedCopies.All(m => m.Equipment.SequenceEqual(source.Equipment)), Is.True, "copies keep the loadout");
            Assert.That(reloadedCopies.Any(m => m.CustomName == "Headless Copy"), Is.True, "rename persisted");
            Assert.That(back.Inventory.Mechs.Any(m => m.IsDamaged), Is.False, "every mech repaired");
            var ids = back.Inventory.Actors.Select(a => a.InventoryId).Where(g => g is not null).ToList();
            Assert.That(ids, Is.Unique, "inventory ids stay unique");
        });

        var result = back.Inventory.RemoveMechs(back.Inventory.Mechs.Where(m => copyIds.Contains(m.InventoryId)));
        var after = Reopen(back.Save("Headless Mechs 2"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Has.Count.EqualTo(2));
            Assert.That(after.Inventory.Mechs.Any(m => copyIds.Contains(m.InventoryId)), Is.False, "copies removed");
            Assert.That(after.Inventory.Mechs.Count(), Is.EqualTo(count));
        });
    }

    [Test]
    public void Deployed_mechs_are_never_removed()
    {
        var doc = ClansDocument.Load(_save);
        var count = doc.Inventory.Mechs.Count();

        var result = doc.Inventory.RemoveMechs(doc.Inventory.Mechs.ToList());

        Assert.Multiple(() =>
        {
            Assert.That(result.Skipped, Is.Not.Empty, "the deployed star must be kept");
            Assert.That(doc.Inventory.Mechs.Count(), Is.EqualTo(count - result.Succeeded.Count));
        });
    }

    [Test]
    public void Saving_an_original_creates_a_listed_new_save_and_leaves_the_original_alone()
    {
        var doc = ClansDocument.Load(_save);
        Assume.That(doc.IsOriginalGameSave, Is.True);
        doc.Inventory.SetTotal(Currency, "Kerenskies_CurrencyItem", 1_234_567);

        var outcome = doc.Save("Headless New Save");
        var back = Reopen(outcome);
        var newId = Path.GetFileNameWithoutExtension(outcome.Path).Split('_')[1];

        Assert.Multiple(() =>
        {
            Assert.That(outcome.CreatedNewFile, Is.True);
            Assert.That(outcome.RegisteredInLoadMenu, Is.True);
            Assert.That(outcome.Path, Is.Not.EqualTo(_save));
            Assert.That(SaveFixtures.Hash(_save), Is.EqualTo(_originalHash), "original changed");
            Assert.That(back.IsOriginalGameSave, Is.False);
            Assert.That(EditorStamp.Read(Ue5SaveFile.Load(outcome.Path).Properties)?.Source, Is.EqualTo(doc_SourceId()));
            Assert.That(back.DisplayName(), Is.EqualTo("Headless New Save"));
            foreach (var index in SaveIndex.Files(_folder))
            {
                Assert.That(File.Exists(index + ".bak"), Is.True, $"{Path.GetFileName(index)} backed up");
                Assert.That(Ue5SaveFile.Load(index).ToPayload().AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(newId)), Is.GreaterThanOrEqualTo(0),
                    $"{Path.GetFileName(index)} lists the new save");
            }

            var listed = SaveLibrary.List(_folder).FirstOrDefault(s => s.SaveId == newId);
            Assert.That(listed?.Name, Is.EqualTo("Headless New Save"));
            Assert.That(listed?.Kerenskies, Is.EqualTo(1_234_567), "load menu shows the edited Kerenskies");
        });

        string doc_SourceId() => Path.GetFileNameWithoutExtension(_save).Split('_')[1];
    }

    [Test]
    public void An_editor_save_is_updated_in_place_with_a_backup()
    {
        var doc = ClansDocument.Load(_save);
        var first = doc.Save("Headless In Place");

        var again = ClansDocument.Load(first.Path);
        again.Inventory.SetTotal(Currency, "Merits_CurrencyItem", 42);
        var second = again.Save("Headless In Place 2");

        Assert.Multiple(() =>
        {
            Assert.That(second.CreatedNewFile, Is.False, "an editor save is updated, not copied");
            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(File.Exists(first.Path + ".bak"), Is.True);
            Assert.That(Reopen(second).Currency("Merits_CurrencyItem"), Is.EqualTo(42));
            Assert.That(Reopen(second).DisplayName(), Is.EqualTo("Headless In Place 2"));
        });
    }

    private static ClansDocument Reopen(SaveOutcome outcome) => ClansDocument.Load(outcome.Path);
}
