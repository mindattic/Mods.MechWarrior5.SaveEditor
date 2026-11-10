using System.Globalization;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.ViewModels;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Every command a player can press, driven through the real view models (in the same fixture as the
/// other UI tests, since WPF allows one application per process), then saved through the UI and verified
/// from the file on disk.
/// </summary>
public partial class UiHeadlessTests
{
    [Test]
    public void Every_mercenaries_command_works_through_the_ui()
    {
        var (_, save) = SaveFixtures.MercsSaveOfKind("Manual");
        ItemOption? fitted = null;
        var lab = new FakeEditorWindows(editor =>
        {
            var row = editor.Weapons.First(w => w.Options.Count(o => o.Item is not null) > 1);
            fitted = row.Options.First(o => o.Item is not null && o != row.Selected);
            row.Selected = fitted;
            editor.MaxArmorCommand.Execute(null);
        });
        var (shell, window, dialogs) = Launch(clansFolder: null, lab);
        shell.OpenPath(save);
        WaitForGameData(shell.Mercs);
        var m = shell.Mercs;
        Assert.That(m.HasDocument, Is.True, string.Join(" | ", dialogs.Shown));
        var doc = m.Document!;

        // Company.
        var company = m.Company!;
        var date = doc.Company.CampaignDate;
        company.AddCBillsCommand.Execute("1000000");
        company.MaxCBillsCommand.Execute(null);
        company.Reputation = 500;
        company.CompanyName = "UI Lancers";
        if (date is { } d)
        {
            company.DateText = d.AddDays(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            company.MoveDateCommand.Execute(null);
        }

        var trait = doc.Traits.HasMechTraits
            ? company.TraitChoices.FirstOrDefault(t => !doc.Traits.UnlockedMechTraits.Contains(t.Asset))
            : null;
        if (trait is not null)
        {
            company.UnlockToAdd = trait;
            company.UnlockTraitCommand.Execute(null);
        }

        // Mechs: requisition two, reset and repair them, refit one in the Mech Lab, remove the other.
        m.NavigateCommand.Execute(MercsPage.Mechs);
        var mechs = m.Mechs!;
        var hangarBefore = mechs.Hangar.Count;
        var mechsInSave = doc.Hangar.Mechs.Count; // includes market/unassigned mechs the hangar list leaves out
        foreach (var row in mechs.CatalogView.Cast<CatalogMechViewModel>().Where(r => r.Variant.ModName is null).Take(2))
        {
            row.IsChecked = true;
        }

        mechs.AddSelectedCommand.Execute(null);
        Assert.That(mechs.Hangar, Has.Count.EqualTo(hangarBefore + 2), "requisition added two mechs");
        var added = mechs.Hangar.Where(h => h.Record.IsNew).ToList();
        added.ForEach(h => h.IsChecked = true);
        mechs.ResetSelectedCommand.Execute(null);
        mechs.RepairAllCommand.Execute(null);
        mechs.ClearHangarSelectionCommand.Execute(null);
        var refitId = added[0].Record.ItemId;
        mechs.EditLoadoutCommand.Execute(mechs.Hangar.First(h => h.Record.ItemId == refitId));
        Assert.That(lab.Opened, Is.EqualTo(1), "the Mech Lab opened");
        var removedId = added[1].Record.ItemId;
        mechs.Hangar.First(h => h.Record.ItemId == removedId).IsChecked = true;
        mechs.RemoveSelectedCommand.Execute(null);

        // Inventory: add two items, recount one, remove the other.
        var inv = m.Inventory!;
        var picks = inv.CatalogView.Cast<ItemPickViewModel>().Take(2).ToList();
        picks.ForEach(p => p.IsChecked = true);
        inv.AddCount = 4;
        inv.AddSelectedCommand.Execute(null);
        var first = picks[0].Definition.Asset;
        var second = picks[1].Definition.Asset;
        foreach (var row in inv.Items.Where(r => r.Asset == first))
        {
            row.IsChecked = true;
        }

        inv.SetCount = 9;
        inv.SetSelectedCountCommand.Execute(null);
        inv.ClearSelectionCommand.Execute(null);
        foreach (var row in inv.Items.Where(r => r.Asset == second))
        {
            row.IsChecked = true;
        }

        inv.RemoveSelectedCommand.Execute(null);

        // Pilots: hire two, rename one, release the other, heal and max everyone.
        var pilots = m.Pilots!;
        var crew = pilots.Pilots.Count;
        pilots.HireCommand.Execute(null);
        pilots.Selected!.FullName = "UI Pilot";
        pilots.Selected.Callsign = "Clicker";
        var keptId = pilots.Selected.Pilot.PersonaId;
        pilots.HireCommand.Execute(null);
        pilots.ReleaseCommand.Execute(null);
        pilots.HealAllCommand.Execute(null);
        pilots.MaxAllCapsCommand.Execute(null);

        // Factions.
        m.Factions!.SetAllCommand.Execute("100");

        // Save through the UI and verify everything from the file on disk.
        m.SaveName = "UI Every Command";
        m.SaveCommand.Execute(null);
        var back = Mw5Document.Open(doc.FilePath, MechCatalog.LoadBuiltIn());
        var refitted = back.Hangar.Mechs.Single(h => h.ItemId == refitId);
        Assert.Multiple(() =>
        {
            Assert.That(doc.FilePath, Is.Not.EqualTo(save), "saved as a new file");
            Assert.That(m.IsDirty, Is.False);
            Assert.That(dialogs.Errors, Is.Empty);
            Assert.That(back.Company.CBills, Is.EqualTo(CompanyEditor.MaxCBills));
            Assert.That(back.Company.Reputation, Is.EqualTo(500));
            Assert.That(back.Company.CompanyName, Is.EqualTo("UI Lancers"));
            if (date is { } d2)
            {
                Assert.That(back.Company.CampaignDate, Is.EqualTo(d2.AddDays(10)));
            }

            if (trait is not null)
            {
                Assert.That(back.Traits.UnlockedMechTraits, Does.Contain(trait.Asset));
            }

            Assert.That(back.Hangar.Mechs, Has.Count.EqualTo(mechsInSave + 1), "two added, one removed");
            Assert.That(back.Hangar.Mechs.Any(h => h.ItemId == removedId), Is.False);
            Assert.That(HangarEditor.ReadLoadout(refitted).Weapons.Select(w => w.Name), Does.Contain(fitted!.Item!.Asset), "Mech Lab refit saved");
            Assert.That(back.Inventory.Items.Where(i => i.Asset == first).Select(i => i.Count), Is.All.EqualTo(9));
            Assert.That(back.Inventory.Items.Any(i => i.Asset == second), Is.False);
            Assert.That(back.Roster.Pilots, Has.Count.EqualTo(crew + 1), "hired two, released one");
            Assert.That(back.Roster.Pilots.Any(p => p.PersonaId == keptId && p.FullName == "UI Pilot" && p.Callsign == "Clicker"), Is.True);
            Assert.That(back.Roster.Pilots.All(p => p.IsHealthy), Is.True);
            Assert.That(back.Factions.Standings.Select(f => f.Standing), Is.All.EqualTo(100));
        });
        window.Close();
    }

    [Test]
    public void Every_clans_command_works_through_the_ui()
    {
        var (folder, save) = SaveFixtures.ClansSaveOfKind("ManualSave");
        var (shell, window, dialogs) = Launch(clansFolder: folder);
        shell.IsClansGame = true;
        shell.OpenPath(save);
        var c = shell.Clans;
        Assert.That(c.HasDocument, Is.True, string.Join(" | ", dialogs.Shown));

        c.NavigateCommand.Execute(ClansPage.Resources);
        var res = (ResourcesViewModel)c.CurrentView!;
        var kerenskies = res.Currencies[0].Amount;
        res.Currencies[0].AddCommand.Execute("1000");
        res.Currencies[1].Amount = 321;
        res.Currencies[2].Amount = 654;
        foreach (var row in res.Salvage)
        {
            row.Amount = 77;
        }

        res.CrewRows[0].Amount = 25;
        foreach (var row in res.MechXp)
        {
            row.AddCommand.Execute("100");
        }

        var mxp = res.MechXp.Select(r => (r.Asset, r.Amount)).ToList();

        c.NavigateCommand.Execute(ClansPage.Mechs);
        var mechs = (MechWarrior5.SaveEditor.App.Clans.ViewModels.MechsViewModel)c.CurrentView!;
        var count = mechs.Rows.Count;
        mechs.SelectDamagedCommand.Execute(null);
        mechs.RepairCommand.Execute(null);
        mechs.SelectNoneCommand.Execute(null);
        var source = mechs.Rows[0].Mech;
        mechs.Rows[0].IsChecked = true;
        mechs.Copies = 2;
        mechs.DuplicateCommand.Execute(null);
        mechs.SelectNoneCommand.Execute(null);
        var copies = mechs.Rows.Where(r => r.Mech.Variant == source.Variant && r.Mech.InventoryId != source.InventoryId && r.Mech.InventoryId is not null).ToList();
        copies.ForEach(r => r.IsChecked = true);
        mechs.NewName = "UI Copy";
        mechs.RenameCommand.Execute(null);
        var renamed = mechs.Rows.Where(r => r.Mech.CustomName == "UI Copy").Select(r => r.Mech.InventoryId).ToList();
        mechs.SelectNoneCommand.Execute(null);
        mechs.Rows.First(r => r.Mech.InventoryId == renamed[0]).IsChecked = true;
        mechs.RemoveCommand.Execute(null);

        c.NavigateCommand.Execute(ClansPage.Inventory);
        var inv = (MechWarrior5.SaveEditor.App.Clans.ViewModels.InventoryViewModel)c.CurrentView!;
        const string Laser = "Clan_ERMediumLaser";
        var lasers = inv.Rows.FirstOrDefault(r => r.Asset == Laser)?.Quantity ?? 0;
        inv.ItemToAdd = inv.Catalog.First(i => i.Asset == Laser);
        inv.AddCount = 3;
        inv.AddItemCommand.Execute(null);
        var bump = inv.Rows.First(r => r.Asset != Laser);
        var bumpBefore = bump.Quantity;
        bump.IsChecked = true;
        inv.AddToTickedCommand.Execute("5");
        inv.SelectNoneCommand.Execute(null);
        var drop = inv.Rows.Last(r => r.Asset != bump.Asset && r.Asset != Laser);
        drop.IsChecked = true;
        inv.RemoveTickedCommand.Execute(null);

        c.NavigateCommand.Execute(ClansPage.Progress);
        AssertRenders(window, "Clans progress after edits");
        c.SaveName = "UI Every Clans Command";
        c.SaveCommand.Execute(null);
        var back = Clans.SaveEditor.Core.Game.ClansDocument.Load(c.Document!.Path);
        Assert.Multiple(() =>
        {
            Assert.That(c.Document.Path, Is.Not.EqualTo(save));
            Assert.That(c.IsDirty, Is.False);
            Assert.That(dialogs.Errors, Is.Empty);
            Assert.That(back.DisplayName(), Is.EqualTo("UI Every Clans Command"));
            Assert.That(back.Currency("Kerenskies_CurrencyItem"), Is.EqualTo(kerenskies + 1000));
            Assert.That(back.Currency("Merits_CurrencyItem"), Is.EqualTo(321));
            Assert.That(back.Currency("Honor_CurrencyItem"), Is.EqualTo(654));
            Assert.That(back.Progress.HonorExperience, Is.EqualTo(654), "Honor track follows");
            Assert.That(back.Inventory.Total("Salvage_MechComponents"), Is.EqualTo(77));
            Assert.That(back.Inventory.Total("Technician_CrewItem"), Is.EqualTo(25));
            Assert.That(back.Progress.TechniciansAvailable, Is.EqualTo(25), "technician pool follows");
            foreach (var (asset, amount) in mxp)
            {
                Assert.That(back.Inventory.Total(asset), Is.EqualTo(amount), asset);
            }

            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(count + 1), "two copies, one removed");
            Assert.That(back.Inventory.Mechs.Count(x => x.CustomName == "UI Copy"), Is.EqualTo(1));
            Assert.That(back.Inventory.Mechs.Any(x => x.IsDamaged), Is.False, "repaired");
            Assert.That(back.Inventory.Total(Laser), Is.EqualTo(lasers + 3));
            Assert.That(back.Inventory.Total(bump.Asset), Is.EqualTo(bumpBefore + 5));
            Assert.That(back.Inventory.Stacks.Any(s => s.AssetName == drop.Asset), Is.False);
        });
        window.Close();
    }
}
