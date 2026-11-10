using System.Windows;
using System.Windows.Media;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Shared.Wpf.Behaviors;
using MechWarrior5.Shared.Wpf.Controls;
using ClansDocument = MechWarrior5.Clans.SaveEditor.Core.Game.ClansDocument;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Mercenaries and Clans live in one app: these check that neither game's systems leak into the other
/// (files, documents, dirty state, saving, game data, settings) and that shared features work in both.
/// </summary>
public partial class UiHeadlessTests
{
    [Test]
    public void Each_game_refuses_the_others_save_and_the_shell_routes_by_format()
    {
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (folder, clansSave) = SaveFixtures.ClansFolderCopy();

        Assert.Multiple(() =>
        {
            Assert.That(() => Mw5Document.Open(clansSave, MechCatalog.LoadBuiltIn()), Throws.Exception, "the Mercenaries core refuses a Clans save");
            Assert.That(() => ClansDocument.Load(mercsSave), Throws.Exception, "the Clans core refuses a Mercenaries save");
        });

        var (shell, _, dialogs) = Launch(clansFolder: folder);
        // Opened from the "wrong" tab, each file still lands in its own game's editor.
        shell.IsMercsGame = true;
        shell.OpenPath(clansSave);
        Assert.That(shell.Game, Is.EqualTo(Game.Clans));
        shell.IsClansGame = true;
        shell.OpenPath(mercsSave);

        Assert.Multiple(() =>
        {
            Assert.That(shell.Game, Is.EqualTo(Game.Mercs));
            Assert.That(shell.Mercs.Document!.FilePath, Is.EqualTo(mercsSave));
            Assert.That(shell.Clans.Document!.Path, Is.EqualTo(clansSave), "opening a Mercenaries save left the Clans document alone");
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Edits_dirty_state_and_saving_stay_inside_their_own_game()
    {
        var (mercsFolder, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (shell, _, dialogs) = Launch(clansFolder: clansFolder);
        shell.OpenPath(mercsSave);
        shell.OpenPath(clansSave);
        var clansBefore = HashFolder(clansFolder);
        var mercsBefore = HashFolder(mercsFolder);

        // A Mercenaries edit marks only Mercenaries dirty.
        shell.Mercs.Company!.CBills = 123_456_789;
        Assert.Multiple(() =>
        {
            Assert.That(shell.Mercs.IsDirty, Is.True);
            Assert.That(shell.Clans.IsDirty, Is.False, "a Mercenaries edit dirtied Clans");
        });

        // A Clans edit marks only Clans dirty (Mercenaries stays dirty from its own edit).
        shell.Clans.NavigateCommand.Execute(ClansPage.Resources);
        var resources = (ResourcesViewModel)shell.Clans.CurrentView!;
        var kerenskies = resources.Currencies[0].Amount;
        resources.Currencies[0].Amount = kerenskies + 1_111;
        Assert.That(shell.Clans.IsDirty, Is.True);

        // Saving Mercenaries writes only into the Mercenaries campaign folder.
        shell.Mercs.SaveName = "Cross Game Mercs";
        shell.Mercs.SaveCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(shell.Mercs.IsDirty, Is.False);
            Assert.That(shell.Clans.IsDirty, Is.True, "saving Mercenaries cleared the Clans edits");
            Assert.That(HashFolder(clansFolder), Is.EqualTo(clansBefore), "saving Mercenaries touched the Clans save folder");
            Assert.That(resources.Currencies[0].Amount, Is.EqualTo(kerenskies + 1_111), "saving Mercenaries changed a Clans value");
        });
        var mercsAfterSave = HashFolder(mercsFolder);
        Assert.That(mercsAfterSave[Path.GetFileName(mercsSave)], Is.EqualTo(mercsBefore[Path.GetFileName(mercsSave)]), "the original Mercenaries save was overwritten");

        // Saving Clans writes only into the Clans folder.
        shell.Clans.SaveName = "Cross Game Clans";
        shell.Clans.SaveCommand.Execute(null);
        var mercsWritten = shell.Mercs.Document!.FilePath;
        var clansWritten = shell.Clans.Document!.Path;

        Assert.Multiple(() =>
        {
            Assert.That(shell.Clans.IsDirty, Is.False);
            Assert.That(HashFolder(mercsFolder), Is.EqualTo(mercsAfterSave), "saving Clans touched the Mercenaries campaign folder");
            Assert.That(Path.GetDirectoryName(mercsWritten), Is.EqualTo(mercsFolder));
            Assert.That(Path.GetDirectoryName(clansWritten), Is.EqualTo(clansFolder));
            Assert.That(HashFolder(clansFolder)[Path.GetFileName(clansSave)], Is.EqualTo(clansBefore[Path.GetFileName(clansSave)]), "the original Clans save was overwritten");

            // Each written file is its own game's format: readable by its core, refused by the other.
            Assert.That(Mw5Document.Open(mercsWritten, MechCatalog.LoadBuiltIn()).Company.CBills, Is.EqualTo(123_456_789));
            Assert.That(ClansDocument.Load(clansWritten).Currency("Kerenskies_CurrencyItem"), Is.EqualTo(kerenskies + 1_111));
            Assert.That(() => ClansDocument.Load(mercsWritten), Throws.Exception);
            Assert.That(() => Mw5Document.Open(clansWritten, MechCatalog.LoadBuiltIn()), Throws.Exception);
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void The_mercenaries_game_data_scan_leaves_clans_views_and_settings_alone()
    {
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (shell, window, _) = Launch(clansFolder: clansFolder);

        shell.OpenPath(clansSave);
        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        AssertRenders(window, "Clans mechs before");
        var rowsBefore = Rows(shell);

        // The Mercenaries catalog is rebuilt from the installed game in the background.
        shell.OpenPath(mercsSave);
        WaitForGameData(shell.Mercs);
        shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
        AssertRenders(window, "Mercenaries mechs");

        shell.IsClansGame = true;
        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        AssertRenders(window, "Clans mechs after");

        Assert.Multiple(() =>
        {
            Assert.That(Rows(shell), Is.EqualTo(rowsBefore), "the Mercenaries scan changed the Clans mech list");
            Assert.That(shell.Clans.IsDirty, Is.False);
            Assert.That(new MechWarrior5.Shared.Wpf.Services.JsonSettings<App.Services.AppSettings>(_settingsDir).Current.ClansSaveFolder, Is.EqualTo(clansFolder),
                "Mercenaries work changed the Clans save folder setting");
        });
    }

    [Test]
    public void The_mech_picture_lightbox_works_in_both_games_and_the_mech_lab()
    {
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (shell, window, _) = Launch(clansFolder: clansFolder);

        shell.OpenPath(mercsSave);
        WaitForGameData(shell.Mercs);
        shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
        AssertRenders(window, "Mercenaries mechs");
        ClickFirstPicture(window, out var mercsTitle);
        var lightbox = Descendants(window).OfType<MechArtLightbox>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.True, "a Mercenaries picture opened the lightbox");
            Assert.That(lightbox.ShownTitle, Is.EqualTo(mercsTitle));
        });
        AssertRenders(window, "Mercenaries lightbox");
        lightbox.Close();
        Assert.That(lightbox.IsOpen, Is.False);

        shell.OpenPath(clansSave);
        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        AssertRenders(window, "Clans mechs");
        ClickFirstPicture(window, out var clansTitle);
        Assert.Multiple(() =>
        {
            Assert.That(lightbox.IsOpen, Is.True, "a Clans picture opened the same lightbox");
            Assert.That(lightbox.ShownTitle, Is.EqualTo(clansTitle));
            Assert.That(shell.Clans.IsDirty, Is.False, "looking at a picture is not an edit");
        });
        AssertRenders(window, "Clans lightbox");
        lightbox.Close();

        // The Mech Lab has its own lightbox for its header picture.
        var hangarMech = shell.Mercs.Mechs!.Hangar.First(h => h.Record.IsEditable);
        var editor = new LoadoutEditorViewModel(hangarMech.Record, shell.Mercs.Document!.Hangar, ItemCatalog.LoadBuiltIn(), PilotCatalog.LoadBuiltIn(), new FakeDialogs());
        var lab = new App.Views.LoadoutEditorWindow(editor) { Left = -4000, Top = 0, Width = 1280, Height = 860, ShowActivated = false };
        lab.Show();
        AssertRenders(lab, "Mech Lab");
        ClickFirstPicture(lab, out var labTitle);
        var labBox = Descendants(lab).OfType<MechArtLightbox>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(labBox.IsOpen, Is.True, "the Mech Lab picture opened its lightbox");
            Assert.That(labBox.ShownTitle, Is.EqualTo(labTitle));
            Assert.That(lightbox.IsOpen, Is.False, "the main window's lightbox stayed closed");
            Assert.That(shell.Mercs.IsDirty, Is.False, "looking at a picture is not an edit");
        });
        lab.Close();
    }

    private static List<string> Rows(ShellViewModel shell) =>
        [.. ((MechWarrior5.SaveEditor.App.Clans.ViewModels.MechsViewModel)shell.Clans.CurrentView!).Rows.Select(r => $"{r.Title}|{r.Subtitle}")];

    private static Dictionary<string, string> HashFolder(string folder) =>
        Directory.EnumerateFiles(folder).ToDictionary(f => Path.GetFileName(f), SaveFixtures.Hash);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    /// <summary>Clicks the first visible mech picture the way a player would (its button's click).</summary>
    private static void ClickFirstPicture(Window window, out string title)
    {
        var button = Descendants(window).OfType<MindAttic.Components.Wpf.Controls.MaButton>()
            .First(b => b.Command == MechArt.ShowCommand && b.IsVisible && b.CommandParameter is string { Length: > 0 });
        Assert.That(button.IsEnabled, Is.True, "the picture button is clickable");
        title = (string)button.CommandParameter;
        // OnClick is what a mouse click ends in: it raises Click and runs the button's command.
        typeof(System.Windows.Controls.Primitives.ButtonBase)
            .GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(button, null);
    }
}
