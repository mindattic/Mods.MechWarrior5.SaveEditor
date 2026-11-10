using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// MechWarrior 5: Mercenaries, end to end: every feature is applied to a copy of a real campaign,
/// saved as the game would load it, then the written file is reopened from disk and each add, edit and
/// delete is verified there, not in memory.
/// </summary>
[TestFixture]
[Category("Headless")]
public class MercsEndToEndTests
{
    private static readonly MechCatalog Mechs = MechCatalog.LoadBuiltIn();
    private static readonly ItemCatalog Items = ItemCatalog.LoadBuiltIn();

    private string _folder = null!;
    private string _save = null!;
    private string _originalHash = null!;

    [SetUp]
    public void SetUp()
    {
        (_folder, _save) = SaveFixtures.MercsCampaignCopy();
        _originalHash = SaveFixtures.Hash(_save);
    }

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    [Test]
    public void Every_real_save_reads_back_byte_for_byte()
    {
        var failures = new List<string>();
        foreach (var path in SaveFixtures.AllMercsSaves())
        {
            try
            {
                var file = Mw5SaveFile.Load(path);
                Assert.That(file.Serialize(), Is.EqualTo(File.ReadAllBytes(path)), path);
            }
            catch (SaveFormatException e)
            {
                failures.Add($"{Path.GetFileName(path)}: {e.Message}");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void Company_edits_persist_and_the_original_is_untouched()
    {
        var doc = Open();
        var date = doc.Company.CampaignDate;
        doc.Company.CBills = 123_456_789;
        doc.Company.Reputation = 4321;
        doc.Company.CompanyName = "Headless Lancers";
        if (date is { } d)
        {
            doc.Company.MoveCampaignDate(d.AddDays(30));
        }

        var outcome = doc.Save(displayName: "Headless Company");
        var back = Reopen(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.CreatedNewFile, Is.True, "an original save must be saved as a new file");
            Assert.That(outcome.Path, Is.Not.EqualTo(_save));
            Assert.That(SaveFixtures.Hash(_save), Is.EqualTo(_originalHash), "original changed");
            Assert.That(back.Company.CBills, Is.EqualTo(123_456_789));
            Assert.That(back.Company.Reputation, Is.EqualTo(4321));
            Assert.That(back.Company.CompanyName, Is.EqualTo("Headless Lancers"));
            if (date is { } d2)
            {
                Assert.That(back.Company.CampaignDate, Is.EqualTo(d2.AddDays(30)));
            }

            Assert.That(outcome.RegisteredInCampaign, Is.True);
            Assert.That(CampaignIndex.GetName(_folder, Path.GetFileNameWithoutExtension(outcome.Path)), Is.EqualTo("Headless Company"));
            Assert.That(back.IsOriginalGameSave, Is.False, "the written save must carry the editor stamp");
        });
    }

    [Test]
    public void Added_mechs_are_in_cold_storage_with_factory_loadouts()
    {
        var doc = Open();
        var variants = Mechs.Variants
            .Where(v => v.Dlc is null && v.HeroName is null && Mechs.FindStock(v.Asset) is not null)
            .Take(3)
            .ToList();
        var before = ColdStorage(doc).Count;

        var result = doc.Hangar.AddToColdStorage(variants, copiesEach: 2);
        var back = Reopen(doc.Save(displayName: "Headless Mechs"));

        var cold = ColdStorage(back);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Has.Count.EqualTo(3));
            Assert.That(cold, Has.Count.EqualTo(before + 6));
            foreach (var variant in variants)
            {
                var added = cold.Where(m => m.Asset == variant.Asset).ToList();
                Assert.That(added, Has.Count.GreaterThanOrEqualTo(2), variant.Code);
                var stock = Mechs.FindStock(variant.Asset)!;
                var loadout = HangarEditor.ReadLoadout(added[^1]);
                Assert.That(Installed(loadout), Is.EquivalentTo(Installed(stock)), $"{variant.Code} weapons");
                Assert.That(loadout.Armor.Values.Sum(), Is.EqualTo(stock.Armor.Values.Sum()).Within(0.01), $"{variant.Code} armor");
            }
        });
    }

    [Test]
    public void Removed_mechs_are_gone_after_reload()
    {
        var doc = Open();
        var variant = Mechs.Variants.First(v => v.Dlc is null && Mechs.FindStock(v.Asset) is not null);
        doc.Hangar.AddToColdStorage([variant], copiesEach: 2);
        var withMechs = Reopen(doc.Save(displayName: "Headless Remove 1"));
        var targets = ColdStorage(withMechs).Where(m => m.Asset == variant.Asset).Take(2).ToList();
        var ids = targets.Select(m => m.ItemId).ToHashSet();

        var removed = withMechs.Hangar.RemoveFromColdStorage(targets);
        var back = Reopen(withMechs.Save(displayName: "Headless Remove 2"));

        Assert.Multiple(() =>
        {
            Assert.That(removed.Succeeded, Has.Count.EqualTo(2));
            Assert.That(back.Hangar.Mechs.Any(m => ids.Contains(m.ItemId)), Is.False);
            Assert.That(File.Exists(withMechs.FilePath + ".bak"), Is.True, "an in-place save keeps a .bak");
        });
    }

    [Test]
    public void Edited_loadout_persists_and_reset_to_stock_restores_it()
    {
        var doc = Open();
        var variant = Mechs.Variants.First(v => v.Dlc is null && Mechs.FindStock(v.Asset)?.Weapons.Count(w => w.IsInstalledWeapon) >= 2);
        doc.Hangar.AddToColdStorage([variant]);
        var mech = ColdStorage(doc).Last(m => m.Asset == variant.Asset);
        var stock = HangarEditor.ReadLoadout(mech);
        var stripped = stock.Weapons.First(w => w.IsInstalledWeapon);
        var edited = stock with
        {
            Weapons = [.. stock.Weapons.Select(w => w == stripped ? w with { Name = "None", Type = "None" } : w)],
        };

        doc.Hangar.ApplyLoadout(mech, edited);
        var back = Reopen(doc.Save(displayName: "Headless Loadout"));
        var reloaded = back.Hangar.Mechs.Single(m => m.ItemId == mech.ItemId);
        var afterEdit = Installed(HangarEditor.ReadLoadout(reloaded));

        back.Hangar.ResetToStock([reloaded]);
        var reset = Reopen(back.Save(displayName: "Headless Loadout Reset"));
        var afterReset = Installed(HangarEditor.ReadLoadout(reset.Hangar.Mechs.Single(m => m.ItemId == mech.ItemId)));

        Assert.Multiple(() =>
        {
            Assert.That(afterEdit, Has.Count.EqualTo(Installed(stock).Count - 1), "one weapon removed");
            Assert.That(afterReset, Is.EquivalentTo(Installed(Mechs.FindStock(variant.Asset)!)), "reset restores factory weapons");
        });
    }

    [Test]
    public void Loadout_files_round_trip()
    {
        var doc = Open();
        var mech = doc.Hangar.Mechs.First(m => m.IsEditable);
        var loadout = HangarEditor.ReadLoadout(mech);
        var path = Path.Combine(_folder, "test.mw5loadout");

        LoadoutFile.Create(mech.Asset, loadout).Save(path);
        var read = LoadoutFile.Load(path);

        Assert.That(Installed(read.Loadout), Is.EquivalentTo(Installed(loadout)));
    }

    [Test]
    public void Inventory_items_are_added_recounted_and_removed()
    {
        var doc = Open();
        var weapon = Items.Items.First(i => i.Kind == ItemKind.Weapon);
        var ammo = Items.Items.First(i => i.Kind == ItemKind.Ammo);
        var weaponsBefore = Count(doc, weapon.Asset);

        doc.Inventory.Add([weapon, ammo], 5);
        var added = Reopen(doc.Save(displayName: "Headless Inventory 1"));
        Assert.That(Count(added, weapon.Asset), Is.EqualTo(weaponsBefore + 5), "weapon added");
        Assert.That(Count(added, ammo.Asset), Is.GreaterThanOrEqualTo(5), "ammo added");

        added.Inventory.SetCount(added.Inventory.Items.Where(i => i.Asset == ammo.Asset), 9);
        var recounted = Reopen(added.Save(displayName: "Headless Inventory 2"));
        Assert.That(Count(recounted, ammo.Asset), Is.EqualTo(9 * recounted.Inventory.Items.Count(i => i.Asset == ammo.Asset)), "ammo recounted");

        recounted.Inventory.Remove(recounted.Inventory.Items.Where(i => i.Asset == ammo.Asset));
        var removed = Reopen(recounted.Save(displayName: "Headless Inventory 3"));
        Assert.That(removed.Inventory.Items.Any(i => i.Asset == ammo.Asset), Is.False, "ammo removed");
    }

    [Test]
    public void Pilots_are_hired_edited_and_released()
    {
        var doc = Open();
        Assume.That(doc.Roster.CanHire, Is.True, "save has no pilot to model a recruit on");
        var count = doc.Roster.Pilots.Count;
        var hired = doc.Roster.Hire(doc.Roster.Pilots[0], "Headless Recruit", "Ghost");
        hired.Salary = 12_345;
        hired.Bio = "Written by the headless suite.";
        var skill = hired.Skills.First(s => s.HasCap);
        skill.Cap = 7;
        skill.Experience = 4_200;

        var back = Reopen(doc.Save(displayName: "Headless Pilots 1"));
        var pilot = back.Roster.Pilots.SingleOrDefault(p => p.PersonaId == hired.PersonaId);
        Assert.That(pilot, Is.Not.Null, "hired pilot present after reload");
        Assert.Multiple(() =>
        {
            Assert.That(back.Roster.Pilots, Has.Count.EqualTo(count + 1));
            Assert.That(pilot!.FullName, Is.EqualTo("Headless Recruit"));
            Assert.That(pilot.Callsign, Is.EqualTo("Ghost"));
            Assert.That(pilot.Salary, Is.EqualTo(12_345));
            Assert.That(pilot.Bio, Is.EqualTo("Written by the headless suite."));
            Assert.That(pilot.Skills.Single(s => s.Name == skill.Name).Cap, Is.EqualTo(7));
            Assert.That(pilot.Skills.Single(s => s.Name == skill.Name).Experience, Is.EqualTo(4_200));
        });

        var released = back.Roster.Release([pilot!]);
        var after = Reopen(back.Save(displayName: "Headless Pilots 2"));
        Assert.Multiple(() =>
        {
            Assert.That(released.Succeeded, Has.Count.EqualTo(1));
            Assert.That(after.Roster.Pilots.Any(p => p.PersonaId == hired.PersonaId), Is.False, "released pilot gone");
            Assert.That(after.Roster.Pilots, Has.Count.EqualTo(count));
        });
    }

    [Test]
    public void Faction_standings_persist()
    {
        var doc = Open();
        doc.Factions.SetAll(75);

        var back = Reopen(doc.Save(displayName: "Headless Factions"));

        Assert.That(back.Factions.Standings.Select(f => f.Standing), Is.All.EqualTo(75));
    }

    [Test]
    public void Company_export_imports_into_another_campaign_copy()
    {
        var source = Open();
        var package = CompanyTransfer.Export(source);
        var path = Path.Combine(_folder, "company.mw5company");
        package.Save(path);

        var target = Open();
        var before = ColdStorage(target).Count;
        var report = CompanyTransfer.Import(target, CompanyPackage.Load(path), Mechs, new TransferOptions(Mechs: true, Pilots: false, Inventory: false));
        var back = Reopen(target.Save(displayName: "Headless Import"));

        Assert.That(ColdStorage(back), Has.Count.EqualTo(before + report.Mechs.Succeeded.Count));
    }

    [Test]
    public void An_original_save_is_never_overwritten()
    {
        var doc = Open();

        Assert.Multiple(() =>
        {
            Assert.That(() => doc.File.Save(_save, sourceSave: null), Throws.Exception, "writer must refuse an unstamped target");
            Assert.That(SaveFixtures.Hash(_save), Is.EqualTo(_originalHash));
        });
    }

    [Test]
    public void Edits_introduce_no_assets_the_game_cannot_load()
    {
        var install = GameInstall.Find();
        Assume.That(install, Is.Not.Null, "MechWarrior 5: Mercenaries install not found");
        var game = GameDataScanner.Scan(install!, Mechs);
        var doc = Open();
        var baseline = SaveValidator.FindMissing(doc.File, game.AssetNames);
        doc.Hangar.AddToColdStorage(Mechs.Variants.Where(v => v.Dlc is null && Mechs.FindStock(v.Asset) is not null).Take(10));
        doc.Inventory.Add(Items.Items.Take(40), 1);

        var back = Reopen(doc.Save(displayName: "Headless Assets"));

        Assert.That(SaveValidator.Introduced(SaveValidator.FindMissing(back.File, game.AssetNames), baseline), Is.Empty);
    }

    private Mw5Document Open() => Mw5Document.Open(_save, Mechs);

    private static Mw5Document Reopen(SaveOutcome outcome) => Mw5Document.Open(outcome.Path, Mechs);

    private static List<MechRecord> ColdStorage(Mw5Document doc) =>
        [.. doc.Hangar.Mechs.Where(m => m.Location == MechLocation.ColdStorage)];

    private static int Count(Mw5Document doc, string asset) =>
        doc.Inventory.Items.Where(i => i.Asset == asset).Sum(i => i.Count);

    private static List<string> Installed(StockLoadout loadout) =>
        [.. loadout.Weapons.Where(w => w.IsInstalledWeapon).Select(w => $"{w.Slot}:{w.Name}")];
}
