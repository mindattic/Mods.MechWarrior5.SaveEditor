using System.Text.Json;
using System.Text.RegularExpressions;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;
using MechWarrior5.SaveEditor.App.Localization;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Shared.Wpf.Services;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// The features the M5SE-T57 audit found no test drove, each exercised through the real view models a player uses:
/// Save As, company and loadout export/import, mech-trait lock and the Cantinas toggle, pilot reorder/persona/trait
/// removal, the unsaved-changes prompt, both Settings pages, every language, and translation completeness.
/// </summary>
public partial class UiHeadlessTests
{
    private static readonly MechCatalog FeatureMechs = MechCatalog.LoadBuiltIn();

    [Test]
    public void Save_as_writes_a_new_editor_save_and_never_overwrites_an_original()
    {
        var (folder, save) = SaveFixtures.MercsSaveOfKind("Manual");
        var hash = SaveFixtures.Hash(save);
        var (shell, _, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(save);
        var m = shell.Mercs;

        m.Company!.CBills = 4_242_424;
        var target = Path.Combine(folder, "SaveAsCopy.sav");
        dialogs.SaveAsPick = target;
        m.SaveAsCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(target), Is.True, "Save As wrote the picked file");
            Assert.That(m.Document!.FilePath, Is.EqualTo(target), "the document follows it");
            Assert.That(m.IsDirty, Is.False);
            Assert.That(Mw5Document.Open(target, FeatureMechs).Company.CBills, Is.EqualTo(4_242_424));
            Assert.That(EditorStamp.FileHasStamp(target), Is.True, "stamped as an editor save");
            Assert.That(dialogs.Errors, Is.Empty);
        });

        // Save As over the original game save must be refused and change nothing.
        m.Company!.CBills = 1;
        dialogs.SaveAsPick = save;
        m.SaveAsCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(dialogs.Errors, Has.Count.EqualTo(1), "refused with an error");
            Assert.That(SaveFixtures.Hash(save), Is.EqualTo(hash), "the original is untouched");
            Assert.That(m.IsDirty, Is.True, "the edit is still unsaved");
            Assert.That(Directory.EnumerateFiles(folder, "*.tmp"), Is.Empty, "no temp file left behind");
        });
    }

    [Test]
    public void Unsaved_changes_prompt_cancel_discard_and_save()
    {
        var (_, first) = SaveFixtures.MercsCampaignCopy();
        var (_, second) = SaveFixtures.MercsSaveOfKind("Manual");
        var firstHash = SaveFixtures.Hash(first);
        var (shell, _, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(first);
        var m = shell.Mercs;

        m.Company!.CBills = 7_777;
        dialogs.SaveChangesAnswer = null;
        Assert.That(shell.Mercs.OpenPath(second), Is.False, "Cancel keeps the open save");
        Assert.Multiple(() =>
        {
            Assert.That(m.Document!.FilePath, Is.EqualTo(first));
            Assert.That(m.IsDirty, Is.True);
            Assert.That(m.Company.CBills, Is.EqualTo(7_777));
        });

        dialogs.SaveChangesAnswer = true;
        Assert.That(shell.Mercs.OpenPath(second), Is.True, "Save, then open the other save");
        var written = Directory.EnumerateFiles(Path.GetDirectoryName(first)!, "*.sav")
            .Where(f => f != first && EditorStamp.FileHasStamp(f)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(written, Has.Count.EqualTo(1), "the edits went to one new save");
            Assert.That(Mw5Document.Open(written[0], FeatureMechs).Company.CBills, Is.EqualTo(7_777));
            Assert.That(SaveFixtures.Hash(first), Is.EqualTo(firstHash), "the original is untouched");
            Assert.That(m.Document!.FilePath, Is.EqualTo(second));
        });

        m.Company!.CBills = 1;
        dialogs.SaveChangesAnswer = false;
        Assert.That(shell.Mercs.OpenPath(first), Is.True, "Discard opens the other save");
        Assert.That(m.Company!.CBills, Is.Not.EqualTo(1), "the discarded edit is gone");
        Assert.That(dialogs.SaveChangesAsked, Is.EqualTo(3));
    }

    [Test]
    public void Company_export_and_import_through_the_ui()
    {
        var (folder, save) = SaveFixtures.MercsCampaignCopy();
        var (_, other) = SaveFixtures.MercsSaveOfKind("Manual");
        var (shell, _, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(save);
        var package = Path.Combine(folder, "company" + CompanyPackage.Extension);
        dialogs.SaveFilePick = package;
        shell.Mercs.Company!.ExportCommand.Execute(null);
        Assert.That(File.Exists(package), Is.True, "exported");
        var exported = CompanyPackage.Load(package);

        // Export to a read-only file reports it instead of crashing.
        var locked = Path.Combine(folder, "locked" + CompanyPackage.Extension);
        File.WriteAllText(locked, "x");
        File.SetAttributes(locked, FileAttributes.ReadOnly);
        dialogs.SaveFilePick = locked;
        shell.Mercs.Company.ExportCommand.Execute(null);
        File.SetAttributes(locked, FileAttributes.Normal);
        Assert.That(dialogs.Errors, Has.Count.EqualTo(1), "read-only export reported");

        shell.OpenPath(other);
        var mechsBefore = shell.Mercs.Document!.Hangar.Mechs.Count;
        var pilotsBefore = shell.Mercs.Document.Roster.Pilots.Count;
        dialogs.OpenFilePick = package;
        shell.Mercs.Company!.ImportCommand.Execute(null);
        shell.Mercs.SaveCommand.Execute(null);
        var back = Mw5Document.Open(shell.Mercs.Document!.FilePath, FeatureMechs);
        Assert.Multiple(() =>
        {
            Assert.That(back.Hangar.Mechs.Count, Is.GreaterThan(mechsBefore), "mechs imported");
            Assert.That(back.Roster.Pilots.Count, Is.GreaterThanOrEqualTo(pilotsBefore), "pilots kept or imported");
            Assert.That(exported.Mechs, Is.Not.Empty);
            Assert.That(dialogs.Errors, Has.Count.EqualTo(1), "no new errors");
        });

        // A damaged company file is refused with a message, not a crash.
        var broken = Path.Combine(folder, "broken" + CompanyPackage.Extension);
        File.WriteAllText(broken, "{\"format\":\"mw5se-company\"}");
        dialogs.OpenFilePick = broken;
        shell.Mercs.Company!.ImportCommand.Execute(null);
        Assert.That(dialogs.Errors, Has.Count.EqualTo(2), "broken file refused");
    }

    [Test]
    public void Loadout_export_and_import_through_the_mech_lab()
    {
        var (folder, save) = SaveFixtures.MercsCampaignCopy();
        var file = Path.Combine(folder, "lab" + LoadoutFile.Extension);
        FakeDialogs dialogs = null!;
        var exportedWeapons = new List<string>();
        var importedWeapons = new List<string>();
        var windows = new FakeEditorWindows(editor =>
        {
            dialogs.SaveFilePick = file;
            editor.ExportCommand.Execute(null);
            exportedWeapons.AddRange(editor.Weapons.Select(w => $"{w.Slot}:{w.Selected.Item?.Asset}"));
            dialogs.OpenFilePick = file;
            editor.ImportCommand.Execute(null);
            importedWeapons.AddRange(editor.Weapons.Select(w => $"{w.Slot}:{w.Selected.Item?.Asset}"));
        });
        (var shell, _, dialogs) = Launch(clansFolder: null, windows);
        shell.OpenPath(save);
        shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
        var mechs = (App.ViewModels.MechsViewModel)shell.Mercs.CurrentView!;
        mechs.EditLoadoutCommand.Execute(mechs.Hangar.First(h => h.Record.IsEditable));

        Assert.Multiple(() =>
        {
            Assert.That(windows.Opened, Is.EqualTo(1));
            Assert.That(File.Exists(file), Is.True, "loadout exported");
            Assert.That(LoadoutFile.Load(file).Loadout.Weapons, Is.Not.Empty);
            Assert.That(importedWeapons, Is.EqualTo(exportedWeapons), "import restores the same weapons");
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Mech_trait_unlock_lock_and_cantinas_toggle_save()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var (shell, _, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(save);
        var company = shell.Mercs.Company!;
        Assume.That(company.HasMechTraits, Is.True, "save has no mech-trait unlocks");

        // Unlock two, then lock one of them again through the row's Lock command.
        var two = company.TraitChoices.Where(t => company.UnlockedTraits.All(u => u.Asset != t.Asset)).Take(2).ToList();
        foreach (var trait in two)
        {
            company.UnlockToAdd = trait;
            company.UnlockTraitCommand.Execute(null);
        }

        var locked = two[0];
        var drop = company.UnlockedTraits.First(t => t.Asset == two[1].Asset);
        company.LockTraitCommand.Execute(drop);
        bool? cantinas = company.HasCantinas ? !company.CantinasUnlocked : null;
        if (cantinas is { } c)
        {
            company.CantinasUnlocked = c;
        }

        shell.Mercs.SaveCommand.Execute(null);
        var back = Mw5Document.Open(shell.Mercs.Document!.FilePath, FeatureMechs);
        Assert.Multiple(() =>
        {
            Assert.That(back.Traits.UnlockedMechTraits, Does.Contain(locked.Asset), "unlocked");
            Assert.That(back.Traits.UnlockedMechTraits, Does.Not.Contain(drop.Asset), "locked again");
            if (cantinas is { } c2)
            {
                Assert.That(!back.Traits.CantinasLocked, Is.EqualTo(c2), "Cantinas toggle");
            }

            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Pilot_reorder_persona_and_trait_removal_save()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var (shell, _, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(save);
        shell.Mercs.NavigateCommand.Execute(MercsPage.Pilots);
        var pilots = shell.Mercs.Pilots!;
        var movable = pilots.Pilots.Where(p => !p.IsPlayer).ToList();
        Assume.That(movable, Has.Count.GreaterThanOrEqualTo(2), "needs two non-commander pilots");

        var last = movable[^1];
        var order = pilots.Pilots.Select(p => p.Pilot.PersonaId).ToList();
        pilots.Selected = last;
        pilots.MoveUpCommand.Execute(null);
        var moved = pilots.Pilots.Select(p => p.Pilot.PersonaId).ToList();
        Assert.That(moved, Is.Not.EqualTo(order), "Move up changed the order");

        var target = movable[0];
        var persona = target.Personas.First(p => p.FullName is { Length: > 0 } && p.FullName != target.FullName);
        target.Persona = persona;
        target.ApplyPersonaCommand.Execute(null);
        target.TraitToAdd = target.TraitChoices.First(t => target.Traits.All(r => r.Asset != t.Asset));
        target.AddTraitCommand.Execute(null);
        var removed = target.Traits.First();
        target.RemoveTraitCommand.Execute(removed);
        var kept = target.Traits.Select(t => t.Asset).ToList();

        shell.Mercs.SaveCommand.Execute(null);
        var back = Mw5Document.Open(shell.Mercs.Document!.FilePath, FeatureMechs);
        var pilot = back.Roster.Pilots.Single(p => p.PersonaId == target.Pilot.PersonaId);
        Assert.Multiple(() =>
        {
            Assert.That(back.Roster.Pilots.Select(p => p.PersonaId), Is.EqualTo(moved), "new order saved");
            Assert.That(pilot.FullName, Is.EqualTo(persona.FullName), "persona applied");
            Assert.That(pilot.Traits, Is.EquivalentTo(kept), "trait added and removed");
            Assert.That(pilot.Traits, Does.Not.Contain(removed.Asset));
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Settings_pages_persist_and_every_language_renders()
    {
        var (folder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (shell, window, dialogs) = Launch(clansFolder: folder);
        var settings = shell.Mercs.SettingsPage;
        try
        {
            // Downloads toggle, cache clear and Owned DLC only are remembered in settings.json.
            settings.DownloadImages = true;
            var cache = Path.Combine(_settingsDir, "img");
            Directory.CreateDirectory(cache);
            for (var i = 0; i < 3; i++)
            {
                File.WriteAllBytes(Path.Combine(cache, $"x{i}.img"), [1, 2, 3]);
            }

            settings.ClearImageCacheCommand.Execute(null);
            Assert.That(Directory.EnumerateFiles(cache, "*.img"), Is.Empty, "cache cleared");
            Assert.That(settings.CacheStatus, Does.Contain("3"));

            shell.OpenPath(mercsSave);
            shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
            var mechs = (App.ViewModels.MechsViewModel)shell.Mercs.CurrentView!;
            var dlcOnly = !mechs.OwnedDlcOnly;
            mechs.OwnedDlcOnly = dlcOnly;

            // Every language: switch, render every page of both games, no raw keys or format errors.
            shell.OpenPath(clansSave);
            foreach (var language in Loc.Languages)
            {
                settings.Language = language;
                shell.IsMercsGame = true;
                foreach (var page in Enum.GetValues<MercsPage>())
                {
                    shell.Mercs.NavigateCommand.Execute(page);
                    AssertRenders(window, $"{language.Code} Mercenaries {page}");
                }

                shell.IsClansGame = true;
                foreach (var page in Enum.GetValues<ClansPage>().Where(p => p != ClansPage.Start))
                {
                    shell.Clans.NavigateCommand.Execute(page);
                    AssertRenders(window, $"{language.Code} Clans {page}");
                }
            }

            settings.Language = Loc.Languages.First(l => l.Code == "de");

            // Clans settings page: downloads and save folder.
            shell.Clans.NavigateCommand.Execute(ClansPage.Settings);
            var clansSettings = (App.Clans.ViewModels.SettingsViewModel)shell.Clans.CurrentView!;
            clansSettings.DownloadMechImages = false;
            clansSettings.SaveFolder = folder;

            var reread = new JsonSettings<AppSettings>(_settingsDir).Current;
            Assert.Multiple(() =>
            {
                Assert.That(reread.Language, Is.EqualTo("de"), "language remembered");
                Assert.That(reread.DownloadMechImages, Is.False, "downloads toggle remembered");
                Assert.That(reread.OwnedDlcOnly, Is.EqualTo(dlcOnly), "Owned DLC only remembered");
                Assert.That(reread.ClansSaveFolder, Is.EqualTo(folder), "Clans save folder remembered");
                Assert.That(clansSettings.FolderExists, Is.True);
                Assert.That(dialogs.Errors, Is.Empty);
            });
        }
        finally
        {
            Loc.Instance.Language = "en";
        }
    }

    [Test]
    public void Changing_language_keeps_filters_ticks_and_selection()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var (shell, _, _) = Launch(clansFolder: null);
        shell.OpenPath(save);
        var m = shell.Mercs;
        try
        {
            m.NavigateCommand.Execute(MercsPage.Mechs);
            var mechs = m.Mechs!;
            mechs.Search = "Atlas";
            mechs.HeroesOnly = true;
            mechs.ShowColdStorageOnly = true;
            var ticked = mechs.CatalogView.Cast<CatalogMechViewModel>().First();
            ticked.IsChecked = true;
            m.Inventory!.Search = "laser";
            var pilot = m.Pilots!.Pilots[^1];
            m.Pilots.Selected = pilot;

            m.SettingsPage.Language = Loc.Languages.First(l => l.Code == "fr");

            Assert.Multiple(() =>
            {
                Assert.That(m.Mechs, Is.Not.SameAs(mechs), "the page was rebuilt");
                Assert.That(m.Mechs!.Search, Is.EqualTo("Atlas"));
                Assert.That(m.Mechs.HeroesOnly, Is.True);
                Assert.That(m.Mechs.ShowColdStorageOnly, Is.True);
                Assert.That(m.Mechs.CatalogView.Cast<CatalogMechViewModel>().Where(r => r.IsChecked).Select(r => r.Variant.Asset), Is.EqualTo(new[] { ticked.Variant.Asset }));
                Assert.That(m.Inventory!.Search, Is.EqualTo("laser"));
                Assert.That(m.Pilots!.Selected?.Pilot.PersonaId, Is.EqualTo(pilot.Pilot.PersonaId));
                Assert.That(m.Company!.DlcNote, Is.EqualTo(Loc.Text("dlc_note_this", "x")).Or.EqualTo(Loc.Text("dlc_note_latest", "x")).Or.EqualTo(Loc.Text("dlc_note_unknown", "x")), "DLC note in the new language");
            });
        }
        finally
        {
            Loc.Instance.Language = "en";
        }
    }

    [Test]
    public void Rows_hidden_by_a_filter_are_unticked_so_actions_match_the_screen()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var (shell, _, _) = Launch(clansFolder: null);
        shell.OpenPath(save);
        var mechs = shell.Mercs.Mechs!;
        var rows = mechs.CatalogView.Cast<CatalogMechViewModel>().ToList();
        var keep = rows.First(r => r.Variant.ChassisName.Length > 3);
        var other = rows.First(r => !r.Variant.ChassisName.Equals(keep.Variant.ChassisName, StringComparison.Ordinal)
            && !r.SearchText.Contains(keep.Variant.ChassisName, StringComparison.OrdinalIgnoreCase));
        keep.IsChecked = true;
        other.IsChecked = true;
        mechs.Search = keep.Variant.ChassisName;
        Assert.Multiple(() =>
        {
            Assert.That(keep.IsChecked, Is.True, "a visible ticked row stays ticked");
            Assert.That(other.IsChecked, Is.False, "a row the filter hides is unticked");
            Assert.That(mechs.SelectedCount, Is.EqualTo(mechs.CatalogView.Cast<CatalogMechViewModel>().Count(r => r.IsChecked)), "the count matches the screen");
        });

        var inventory = shell.Mercs.Inventory!;
        Assume.That(inventory.Items, Has.Count.GreaterThanOrEqualTo(2));
        var a = inventory.Items[0];
        var b = inventory.Items.First(r => !r.Name.Contains(a.Name, StringComparison.OrdinalIgnoreCase) && !r.Asset.Contains(a.Asset, StringComparison.OrdinalIgnoreCase));
        a.IsChecked = true;
        b.IsChecked = true;
        inventory.Search = a.Asset;
        Assert.That((a.IsChecked, b.IsChecked), Is.EqualTo((true, false)), "inventory: hidden tick dropped");
    }

    [Test]
    public void Every_ui_text_key_is_translated_in_every_language()
    {
        var root = RepoRoot();
        var app = Path.Combine(root, "src", "MechWarrior5.SaveEditor.App");
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "Loc\\.Text\\(\"([a-z0-9_]+)\""))
            {
                used.Add(m.Groups[1].Value);
            }
        }

        foreach (var file in Directory.EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\\{l:T ([a-z0-9_]+)"))
            {
                used.Add(m.Groups[1].Value);
            }
        }

        Dictionary<string, string> Read(string code) =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(app, "Localization", code + ".json")))!;

        // Symbols that read the same in every language live only in en.json.
        string[] symbols = ["100m", "10m", "1m", "c_bills", "clans", "ctrl_o", "ctrl_shift_s", "mercs", "mw5_mercs", "yyyy_mm_dd"];
        var en = Read("en");
        static string Holes(string text) => string.Join(",", Regex.Matches(text, "\\{(\\d+)").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));

        Assert.Multiple(() =>
        {
            Assert.That(used.Where(k => !en.ContainsKey(k)), Is.Empty, "keys used in code but missing from en.json");
            foreach (var language in Loc.Languages.Where(l => l.Code != "en"))
            {
                var other = Read(language.Code);
                Assert.That(en.Keys.Except(symbols).Where(k => !other.ContainsKey(k)), Is.Empty, $"missing from {language.Code}.json");
                Assert.That(other.Where(kv => en.TryGetValue(kv.Key, out var e) && Holes(e) != Holes(kv.Value)).Select(kv => kv.Key), Is.Empty,
                    $"placeholders differ in {language.Code}.json");
            }
        });
    }
}
