using System.Text.Json.Nodes;
using MechWarrior5.Clans.SaveEditor.Core.Catalog;
using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>One test per bug found by the M5SE-T57 reverification, so none of them comes back.</summary>
[TestFixture]
[Category("Headless")]
public class RegressionTests
{
    private const string Equipment = "KelEquipmentItemDataAsset";
    private static readonly MechCatalog Mechs = MechCatalog.LoadBuiltIn();

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    /// <summary>M5SE-T60: Clans edits act on the player's Inventory.Player stacks, never the shop's.</summary>
    [Test]
    public void Clans_equipment_edits_change_the_players_stock_not_the_shops()
    {
        var (_, save) = SaveFixtures.ClansSaveOfKind("ManualSave");
        var doc = ClansDocument.Load(save);
        var inv = doc.Inventory;
        Assume.That(inv.AllStacks.Any(s => !s.IsPlayers), Is.True, "save has no shop stock");
        Assert.Multiple(() =>
        {
            Assert.That(inv.Stacks.All(s => s.IsPlayers), Is.True, "Stacks lists only the player's");
            Assert.That(inv.Mechs.All(m => m.Actor.IsPlayers), Is.True, "Mechs lists only the player's");
            Assert.That(ItemList.For(inv).Count, Is.GreaterThanOrEqualTo(inv.AllStacks.Select(s => s.AssetName).Distinct().Count()), "the add picker still knows shop-only items");
        });

        var shopped = inv.AllStacks.First(s => !s.IsPlayers && s.Category == StackCategory.Equipment).AssetName;
        static string Shop(Clans.SaveEditor.Core.Game.InventoryEditor i) => string.Join(";", i.AllStacks.Where(s => !s.IsPlayers).Select(s => $"{s.AssetName}={s.Quantity}"));
        var shopBefore = Shop(inv);
        var owned = inv.Total(shopped);
        inv.AddStack(Equipment, shopped, 5);
        inv.SetTotal(Equipment, shopped, owned + 7);

        var back = ClansDocument.Load(doc.Save("Regression Shop").Path).Inventory;
        Assert.Multiple(() =>
        {
            Assert.That(back.Total(shopped), Is.EqualTo(owned + 7), "the player's count");
            Assert.That(back.Stacks.Where(s => s.AssetName == shopped).All(s => s.InventoryTag is null or InventoryActor.PlayerTag), Is.True, "new stacks are the player's");
            Assert.That(Shop(back), Is.EqualTo(shopBefore), "the shop's stock is untouched");
        });
    }

    /// <summary>M5SE-T75: the Honor level follows Honor experience on the game's own curve, up and down.</summary>
    [Test]
    public void Honor_level_follows_honor_experience()
    {
        var pairs = SaveLibrary.List(SaveFixtures.ClansRoot)
            .Select(s => ClansDocument.Load(s.Path).Progress)
            .Where(p => p.HonorExperience is not null && p.HonorLevel is not null)
            .Select(p => (Exp: p.HonorExperience!.Value, Level: p.HonorLevel!.Value))
            .Distinct()
            .ToList();
        Assume.That(pairs, Is.Not.Empty, "no Clans saves with an Honor track");
        Assert.That(pairs.Where(p => ProgressEditor.HonorLevelFor(p.Exp) != p.Level), Is.Empty, "the curve matches every real save");

        var (_, save) = SaveFixtures.ClansSaveOfKind("ManualSave");
        var doc = ClansDocument.Load(save);
        Assume.That(doc.Progress.HonorExperience, Is.Not.Null);
        doc.Progress.HonorExperience = 5_300;
        var up = ClansDocument.Load(doc.Save("Regression Honor Up").Path);
        Assert.That(up.Progress.HonorLevel, Is.EqualTo(14), "5300 Honor is level 14");
        up.Progress.HonorExperience = 99;
        var down = ClansDocument.Load(up.Save("Regression Honor Down").Path);
        Assert.Multiple(() =>
        {
            Assert.That(down.Progress.HonorExperience, Is.EqualTo(99));
            Assert.That(down.Progress.HonorLevel, Is.EqualTo(1), "99 Honor is level 1");
        });
    }

    /// <summary>M5SE-T76: Clans Repair restores internal structure, not only armor and part health.</summary>
    [Test]
    public void Clans_repair_restores_internal_structure()
    {
        var (_, save) = SaveFixtures.ClansFolderCopy();
        var doc = ClansDocument.Load(save);
        var hurt = doc.Inventory.Mechs.FirstOrDefault(m => doc.Inventory.FullStructure(m) is { } full
            && m.Structure.Any(kv => kv.Value < full.GetValueOrDefault(kv.Key) - 0.01f));
        Assume.That(hurt, Is.Not.Null, "no mech with structure damage in this save");
        var full = doc.Inventory.FullStructure(hurt!)!;
        var id = hurt!.InventoryId;
        var result = doc.Inventory.RepairMechs([hurt]);
        var back = ClansDocument.Load(doc.Save("Regression Structure").Path);
        var mech = back.Inventory.Mechs.Single(m => m.InventoryId == id);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Has.Count.EqualTo(1));
            Assert.That(mech.IsDamaged, Is.False);
            foreach (var (part, value) in mech.Structure)
            {
                Assert.That(value, Is.GreaterThanOrEqualTo(full.GetValueOrDefault(part) - 0.01f), part);
            }
        });
    }

    /// <summary>M5SE-T81: an impossible calendar move is refused untouched; the same item twice in one add makes one stack.</summary>
    [Test]
    public void Mercs_calendar_and_inventory_edge_cases()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var doc = Mw5Document.Open(save, Mechs);
        Assume.That(doc.Company.CampaignDate, Is.Not.Null, "save has no calendar");
        var date = doc.Company.CampaignDate!.Value;
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Company.MoveCampaignDate(new DateTime(1, 6, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Company.MoveCampaignDate(new DateTime(9500, 1, 1)));
        Assert.That(doc.Company.CampaignDate, Is.EqualTo(date), "a refused move changes nothing");
        Assert.That(doc.Company.MoveCampaignDate(date.AddYears(-30)), Is.LessThan(0), "a long but sane move back works");

        Assume.That(doc.Inventory.CanAddWeapons, Is.True);
        var item = ItemCatalog.LoadBuiltIn().Items.First(i => i.IsWeapon && doc.Inventory.Items.All(x => x.Asset != i.Asset));
        doc.Inventory.Add([item, item], 4);
        var back = Mw5Document.Open(doc.Save(displayName: "Regression Edge").Path, Mechs);
        Assert.Multiple(() =>
        {
            Assert.That(back.Inventory.Items.Count(i => i.Asset == item.Asset), Is.EqualTo(1), "one stack");
            Assert.That(back.Inventory.Items.Single(i => i.Asset == item.Asset).Count, Is.EqualTo(4));
            Assert.That(back.Company.CampaignDate, Is.EqualTo(date.AddYears(-30)));
        });
    }

    /// <summary>M5SE-T82: an editor stamp whose source save name contains ';' reads back whole.</summary>
    [Test]
    public void Editor_stamp_source_may_contain_semicolons()
    {
        var root = new MechWarrior5.Shared.Unreal.PropertyList();
        MechWarrior5.Shared.Saves.EditorStamp.Apply(root, "Tests", "1.0", "my;odd=save name");
        var stamp = MechWarrior5.Shared.Saves.EditorStamp.Read(root);
        Assert.Multiple(() =>
        {
            Assert.That(stamp!.Source, Is.EqualTo("my;odd=save name"));
            Assert.That(stamp.Tool, Is.EqualTo("Tests"));
            Assert.That(stamp.Version, Is.EqualTo("1.0"));
            Assert.That(stamp.WrittenUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)));
        });
    }

    /// <summary>M5SE-T61: a pilot whose save has no PilotTraits array can still be given a trait.</summary>
    [Test]
    public void A_trait_can_be_added_to_a_pilot_with_no_trait_array()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var doc = Mw5Document.Open(save, Mechs);
        // The game omits an empty array, so a pilot with no traits is exactly the case that failed.
        var pilot = doc.Roster.Pilots.FirstOrDefault(p => p.Traits.Count == 0);
        Assume.That(pilot, Is.Not.Null, "campaign has no traitless pilot");
        var trait = PilotCatalog.LoadBuiltIn().PilotTraits[0].Asset;
        Assert.That(pilot!.AddTrait(trait), Is.True);
        var id = pilot.PersonaId;
        var back = Mw5Document.Open(doc.Save(displayName: "Regression Trait").Path, Mechs);
        Assert.That(back.Roster.Pilots.Single(p => p.PersonaId == id).Traits, Is.EqualTo(new[] { trait }));
    }

    /// <summary>M5SE-T62: a read-only load-menu index doesn't turn a written save into a failure (Mercs).</summary>
    [Test]
    public void Mercs_save_with_a_read_only_index_reports_the_file_it_wrote()
    {
        var (folder, save) = SaveFixtures.MercsSaveOfKind("Manual");
        var hash = SaveFixtures.Hash(save);
        var index = Path.Combine(folder, "Campaign.json");
        File.SetAttributes(index, FileAttributes.ReadOnly);
        try
        {
            var doc = Mw5Document.Open(save, Mechs);
            doc.Company.CBills = 123_456;
            var outcome = doc.Save(displayName: "Regression ReadOnly");
            Assert.Multiple(() =>
            {
                Assert.That(outcome.CreatedNewFile, Is.True);
                Assert.That(outcome.RegisteredInCampaign, Is.False, "not listed: the index is read-only");
                Assert.That(doc.FilePath, Is.EqualTo(outcome.Path), "the document follows the file it wrote");
                Assert.That(Mw5Document.Open(outcome.Path, Mechs).Company.CBills, Is.EqualTo(123_456));
                Assert.That(SaveFixtures.Hash(save), Is.EqualTo(hash));
            });
        }
        finally
        {
            File.SetAttributes(index, FileAttributes.Normal);
        }

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(index + ".tmp"), Is.False, "no temp file left behind");
            Assert.That(File.GetAttributes(index + ".bak").HasFlag(FileAttributes.ReadOnly), Is.False, "the backup isn't read-only");
        });

        // Once the index is writable again, saves are listed again.
        var again = Mw5Document.Open(save, Mechs).Save(displayName: "Regression Writable");
        Assert.That(again.RegisteredInCampaign, Is.True, "a read-only backup must not block later registrations");
    }

    /// <summary>M5SE-T62: a read-only load-menu index doesn't turn a written save into a failure (Clans).</summary>
    [Test]
    public void Clans_save_with_read_only_indexes_reports_the_file_it_wrote()
    {
        var (folder, save) = SaveFixtures.ClansSaveOfKind("ManualSave");
        var hash = SaveFixtures.Hash(save);
        var indexes = SaveIndex.Files(folder).ToList();
        indexes.ForEach(i => File.SetAttributes(i, FileAttributes.ReadOnly));
        try
        {
            var doc = ClansDocument.Load(save);
            var outcome = doc.Save("Regression ReadOnly");
            Assert.Multiple(() =>
            {
                Assert.That(outcome.CreatedNewFile, Is.True);
                Assert.That(outcome.RegisteredInLoadMenu, Is.False);
                Assert.That(doc.Path, Is.EqualTo(outcome.Path));
                Assert.That(File.Exists(outcome.Path), Is.True);
                Assert.That(SaveFixtures.Hash(save), Is.EqualTo(hash));
            });
        }
        finally
        {
            indexes.ForEach(i => File.SetAttributes(i, FileAttributes.Normal));
        }

        var again = ClansDocument.Load(save).Save("Regression Writable");
        Assert.That(again.RegisteredInLoadMenu, Is.True, "a read-only backup must not block later registrations");
    }

    /// <summary>M5SE-T70: stock loadouts read from the installed game carry full structure, so repairs aren't left damaged.</summary>
    [Test]
    public void Game_stock_loadouts_have_full_structure()
    {
        var game = GameInstall.Find();
        if (game is null)
        {
            Assert.Ignore("MW5 Mercenaries isn't installed.");
        }

        var builtIn = MechCatalog.LoadBuiltIn();
        var scanned = MechCatalog.LoadBuiltIn();
        scanned.Apply(GameDataScanner.Scan(game!, scanned));
        var short_ = scanned.Variants
            .Where(v => builtIn.FindStock(v.Asset) is not null && scanned.FindStock(v.Asset) is not null)
            .Where(v => builtIn.FindStock(v.Asset)!.Structure.Any(kv => scanned.FindStock(v.Asset)!.Structure.GetValueOrDefault(kv.Key) < kv.Value))
            .Select(v => v.Asset)
            .ToList();
        Assert.That(short_, Is.Empty, "variants whose game stock structure is below full");
    }

    /// <summary>M5SE-T71: company files missing a list are refused up front; older files without armor maxima still load.</summary>
    [Test]
    public void Partial_company_files_are_refused_and_older_files_load()
    {
        var (folder, save) = SaveFixtures.MercsCampaignCopy();
        var full = Path.Combine(folder, "full" + CompanyPackage.Extension);
        CompanyTransfer.Export(Mw5Document.Open(save, Mechs)).Save(full);

        string Variant(Action<JsonNode> change)
        {
            var node = JsonNode.Parse(File.ReadAllText(full))!;
            change(node);
            var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + CompanyPackage.Extension);
            File.WriteAllText(path, node.ToJsonString());
            return path;
        }

        var noPilots = Variant(n => n.AsObject().Remove("pilots"));
        var noWeapons = Variant(n => n["mechs"]![0]!["loadout"]!.AsObject().Remove("weapons"));
        var noMaxArmor = Variant(n =>
        {
            foreach (var m in n["mechs"]!.AsArray())
            {
                m!["loadout"]!.AsObject().Remove("maxArmor");
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(CompanyPackage.Load(full).Mechs, Is.Not.Empty);
            Assert.Throws<InvalidDataException>(() => CompanyPackage.Load(noPilots));
            Assert.Throws<InvalidDataException>(() => CompanyPackage.Load(noWeapons));
            Assert.That(CompanyPackage.Load(noMaxArmor).Mechs.All(m => m.Loadout.MaxArmor is not null), Is.True);
        });
    }
}
