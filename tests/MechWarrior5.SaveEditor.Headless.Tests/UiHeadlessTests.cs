using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Shared.Saves;
using MechWarrior5.Shared.Wpf.Services;
using MechWarrior5.Shared.Wpf.Theme;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// The real app, headless: the window is shown off-screen, every page of both games is navigated and
/// rendered, edits are made through the view models a player uses, and the saves they write are reopened
/// from disk. Dialogs are answered automatically.
/// </summary>
[TestFixture]
[Category("Headless")]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class UiHeadlessTests
{
    private static App.App? _app;

    private string _settingsDir = null!;

    [OneTimeSetUp]
    public void StartApp()
    {
        if (Application.Current is null)
        {
            _app = new App.App();
            _app.InitializeComponent();
        }

        // Closing a test's window must not shut the shared application down for the next test.
        Application.Current!.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    [SetUp]
    public void SetUp()
    {
        _settingsDir = Path.Combine(Path.GetTempPath(), "MechWarrior5.SaveEditor.Headless", TestContext.CurrentContext.Test.ID, "settings");
        Directory.CreateDirectory(_settingsDir);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Window window in Application.Current.Windows)
        {
            window.Close();
        }

        SaveFixtures.CleanUp();
    }

    [Test]
    public void Every_mercenaries_page_renders_and_a_ui_edit_saves()
    {
        var (_, save) = SaveFixtures.MercsCampaignCopy();
        var (shell, window, dialogs) = Launch(clansFolder: null);
        shell.OpenPath(save);
        Assert.That(shell.Mercs.HasDocument, Is.True, string.Join(" | ", dialogs.Shown));

        foreach (var page in Enum.GetValues<MercsPage>())
        {
            shell.Mercs.NavigateCommand.Execute(page);
            AssertRenders(window, $"Mercenaries {page}");
        }

        shell.Mercs.Company!.CBills = 987_654_321;
        shell.Mercs.SaveName = "Headless UI Save";
        shell.Mercs.SaveCommand.Execute(null);
        var written = shell.Mercs.Document!.FilePath;
        var back = Mercs.SaveEditor.Core.Game.Mw5Document.Open(written, MechCatalog.LoadBuiltIn());

        Assert.Multiple(() =>
        {
            Assert.That(written, Is.Not.EqualTo(save), "the original was saved as a new file");
            Assert.That(back.Company.CBills, Is.EqualTo(987_654_321));
            Assert.That(shell.Mercs.IsDirty, Is.False);
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Every_clans_page_renders_and_a_ui_edit_saves()
    {
        var (folder, save) = SaveFixtures.ClansFolderCopy();
        var (shell, window, dialogs) = Launch(clansFolder: folder);
        shell.IsClansGame = true;
        AssertRenders(window, "Clans start");
        Assert.That(shell.Clans.Start.Saves, Is.Not.Empty, "start page lists the saves");

        shell.OpenPath(save);
        Assert.That(shell.Game, Is.EqualTo(Game.Clans), "a Clans save opens in the Clans editor");
        foreach (var page in Enum.GetValues<ClansPage>().Where(p => p != ClansPage.Start))
        {
            shell.Clans.NavigateCommand.Execute(page);
            AssertRenders(window, $"Clans {page}");
        }

        shell.Clans.NavigateCommand.Execute(ClansPage.Resources);
        var resources = (ResourcesViewModel)shell.Clans.CurrentView!;
        resources.Currencies[0].Amount = 7_654_321;
        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        var mechs = (MechWarrior5.SaveEditor.App.Clans.ViewModels.MechsViewModel)shell.Clans.CurrentView!;
        var before = mechs.Rows.Count;
        mechs.Rows[0].IsChecked = true;
        mechs.DuplicateCommand.Execute(null);
        shell.Clans.SaveName = "Headless UI Clans";
        shell.Clans.SaveCommand.Execute(null);
        var back = Clans.SaveEditor.Core.Game.ClansDocument.Load(shell.Clans.Document!.Path);

        Assert.Multiple(() =>
        {
            Assert.That(shell.Clans.Document.Path, Is.Not.EqualTo(save));
            Assert.That(back.Currency("Kerenskies_CurrencyItem"), Is.EqualTo(7_654_321));
            Assert.That(back.Inventory.Mechs.Count(), Is.EqualTo(before + 1));
            Assert.That(back.DisplayName(), Is.EqualTo("Headless UI Clans"));
            Assert.That(EditorStamp.Read(Ue5SaveFile.Load(back.Path).Properties), Is.Not.Null);
            Assert.That(dialogs.Errors, Is.Empty);
        });
    }

    [Test]
    public void Game_tabs_keep_each_game_open_and_themes_swap()
    {
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        var (folder, clansSave) = SaveFixtures.ClansFolderCopy();
        var (shell, window, _) = Launch(clansFolder: folder);

        shell.OpenPath(mercsSave);
        shell.OpenPath(clansSave);
        shell.IsMercsGame = true;
        AssertRenders(window, "back to Mercenaries");
        shell.Mercs.IsClansTheme = true;
        AssertRenders(window, "Clans theme");
        shell.Mercs.IsMercsTheme = true;

        Assert.Multiple(() =>
        {
            Assert.That(shell.Mercs.HasDocument, Is.True);
            Assert.That(shell.Clans.HasDocument, Is.True);
            Assert.That(shell.Current, Is.SameAs(shell.Mercs));
            Assert.That(ThemeManager.Current, Is.EqualTo(MechWarrior5Theme.Mercs));
        });
    }

    private (ShellViewModel Shell, Window Window, FakeDialogs Dialogs) Launch(string? clansFolder)
    {
        var settings = new JsonSettings<AppSettings>(_settingsDir);
        settings.Current.ClansSaveFolder = clansFolder;
        settings.Current.DownloadMechImages = false;
        var dialogs = new FakeDialogs();
        var mercs = new MainViewModel(MechCatalog.LoadBuiltIn(), ItemCatalog.LoadBuiltIn(), PilotCatalog.LoadBuiltIn(), dialogs, settings, new EditorWindows(), Path.Combine(_settingsDir, "img"));
        var shell = new ShellViewModel(mercs, new ClansMainViewModel(dialogs, settings), settings);
        var window = new App.MainWindow { DataContext = shell, Left = -4000, Top = 0, Width = 1440, Height = 900, ShowActivated = false };
        window.Show();
        return (shell, window, dialogs);
    }

    /// <summary>Lets layout and bindings settle, renders the window, and checks it drew real content.</summary>
    private static void AssertRenders(Window window, string what)
    {
        for (var i = 0; i < 20; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }

        var root = (FrameworkElement)window.Content;
        Assert.That(root.ActualWidth, Is.GreaterThan(0), what);
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var pixels = new int[bitmap.PixelWidth * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        Assert.That(pixels.Distinct().Take(50).Count(), Is.GreaterThan(20), $"{what} rendered blank");
    }

    /// <summary>Answers every dialog without a person: confirms, never picks files, records what was shown.</summary>
    private sealed class FakeDialogs : IDialogService
    {
        public List<string> Shown { get; } = [];

        public List<string> Errors { get; } = [];

        public string? PickSaveToOpen(string? initialFolder) => null;

        public string? PickSaveAs(string currentPath) => null;

        public string? PickFileToOpen(string title, string filter) => null;

        public string? PickFileToSave(string title, string defaultName, string filter) => null;

        public void Show(string title, string message, DialogTone tone = DialogTone.Info, params DialogSection[] sections)
        {
            Shown.Add($"{title}: {message}");
            if (tone == DialogTone.Error)
            {
                Errors.Add($"{title}: {message}");
            }
        }

        public bool Confirm(string title, string message, string confirmText, DialogTone tone = DialogTone.Warning) => true;

        public bool? AskSaveChanges(string fileName) => false;
    }
}
