using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.ViewModels;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Retakes every screenshot (1920x1080) from the real app, off-screen, on sandbox copies of real saves: the 20 in
/// assets/ and the NexusMods page's six in assets/nexus/ (same names, overwritten in place). Explicit: run it on
/// purpose with --filter Category=Screenshots.
/// </summary>
public partial class UiHeadlessTests
{
    private const int ShotWidth = 1920;
    private const int ShotHeight = 1080;

    [Test]
    [Explicit("Writes assets/*.png; run on purpose to refresh the screenshots.")]
    [Category("Screenshots")]
    public void Retake_the_readme_screenshots()
    {
        var assets = Path.Combine(RepoRoot(), "assets");

        // Mercenaries
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        LoadoutEditorViewModel? lab = null;
        var (shell, window, _) = Launch(clansFolder: null, new FakeEditorWindows(editor => lab = editor), downloads: true);
        Size(window);
        Shot(window, assets, "mercs-01-campaigns");
        shell.OpenPath(mercsSave);
        WaitForGameData(shell.Mercs);

        shell.Mercs.NavigateCommand.Execute(MercsPage.Company);
        Shot(window, assets, "mercs-02-company");

        shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
        var mechs = (App.ViewModels.MechsViewModel)shell.Mercs.CurrentView!;
        foreach (var row in mechs.CatalogView.Cast<CatalogMechViewModel>().Where(r => r.IsDlcOwned).Take(4))
        {
            row.IsChecked = true;
        }

        Shot(window, assets, "mercs-03-mechs-requisition");
        mechs.ClearSelectionCommand.Execute(null);
        mechs.HeroesOnly = true;
        Shot(window, assets, "mercs-04-mechs-heroes");
        mechs.HeroesOnly = false;
        mechs.Search = "Atlas";
        Shot(window, assets, "mercs-05-mechs-search");
        mechs.Search = string.Empty;

        var refit = mechs.Hangar.FirstOrDefault(h => h.Record.IsEditable && h.IsColdStorage) ?? mechs.Hangar.First(h => h.Record.IsEditable);
        mechs.EditLoadoutCommand.Execute(refit);
        Assert.That(lab, Is.Not.Null, "the Mech Lab opened");
        var labWindow = new App.Views.LoadoutEditorWindow(lab!) { Left = -4000, Top = 0, ShowActivated = false };
        labWindow.Show();
        Size(labWindow);
        lab!.Tab = LoadoutTab.Weapons;
        Shot(labWindow, assets, "mercs-06-mechlab-weapons");
        lab.Tab = LoadoutTab.Armor;
        Shot(labWindow, assets, "mercs-07-mechlab-armor");
        labWindow.Close();

        shell.Mercs.NavigateCommand.Execute(MercsPage.Inventory);
        Shot(window, assets, "mercs-08-inventory");
        shell.Mercs.NavigateCommand.Execute(MercsPage.Pilots);
        Shot(window, assets, "mercs-09-pilots");
        shell.Mercs.NavigateCommand.Execute(MercsPage.Factions);
        Shot(window, assets, "mercs-10-factions");
        window.Close();

        // Clans
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        (shell, window, _) = Launch(clansFolder, downloads: true);
        Size(window);
        shell.IsClansGame = true;
        Shot(window, assets, "clans-01-campaigns");
        shell.OpenPath(clansSave);
        shell.Clans.SaveName = "Smoke Jaguar Reinforced";

        shell.Clans.NavigateCommand.Execute(ClansPage.Resources);
        Shot(window, assets, "clans-02-resources");

        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        var clansMechs = (App.Clans.ViewModels.MechsViewModel)shell.Clans.CurrentView!;
        Shot(window, assets, "clans-03-mechs");
        TwoThemes(window, Path.Combine(assets, "nexus", "06-two-themes-mercs-amber-clans-ice"));
        clansMechs.SelectDamagedCommand.Execute(null);
        if (clansMechs.CheckedCount == 0)
        {
            foreach (var row in clansMechs.Rows.Take(3))
            {
                row.IsChecked = true;
            }
        }

        Shot(window, assets, "clans-04-mechs-damaged-selected");
        clansMechs.SelectNoneCommand.Execute(null);
        clansMechs.Rows[0].IsChecked = true;
        clansMechs.DuplicateCommand.Execute(null);
        Shot(window, assets, "clans-05-mechs-duplicate");

        shell.Clans.NavigateCommand.Execute(ClansPage.Inventory);
        Shot(window, assets, "clans-06-inventory");
        shell.Clans.NavigateCommand.Execute(ClansPage.Progress);
        Shot(window, assets, "clans-07-progress");
        shell.Clans.NavigateCommand.Execute(ClansPage.Settings);
        Shot(window, assets, "clans-08-settings");

        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        clansMechs = (App.Clans.ViewModels.MechsViewModel)shell.Clans.CurrentView!;
        clansMechs.Filter = clansMechs.Rows[0].Mech.Chassis;
        Shot(window, assets, "clans-09-mechs-filtered");

        shell.Clans.NavigateCommand.Execute(ClansPage.Inventory);
        var inventory = (App.Clans.ViewModels.InventoryViewModel)shell.Clans.CurrentView!;
        foreach (var row in inventory.Rows.Take(3))
        {
            row.IsChecked = true;
        }

        Shot(window, assets, "clans-10-inventory-ticked");
        window.Close();

        // The NexusMods page's six images (assets/nexus, same names so the page's media can be swapped one for one).
        var nexus = Path.Combine(assets, "nexus");
        foreach (var (from, to) in new[]
        {
            ("mercs-03-mechs-requisition", "01-requisition-tick-shift-click-add"),
            ("mercs-06-mechlab-weapons", "02-mech-lab-hardpoint-aware-refits"),
            ("clans-02-resources", "03-clans-resources-kerenskies-honor-salvage-crew-mxp"),
            ("clans-04-mechs-damaged-selected", "04-clans-mechs-repair-duplicate-rename"),
            ("mercs-01-campaigns", "05-pick-saves-by-name-not-guid"),
        })
        {
            File.Copy(Path.Combine(assets, from + ".png"), Path.Combine(nexus, to + ".png"), overwrite: true);
        }
    }

    /// <summary>The same page in both themes, split down the middle: Mercs amber on the left, Clans ice on the right.</summary>
    private static void TwoThemes(Window window, string path)
    {
        var before = MechWarrior5.Shared.Wpf.Theme.ThemeManager.Current;
        MechWarrior5.Shared.Wpf.Theme.ThemeManager.Apply(MechWarrior5.Shared.Wpf.Theme.MechWarrior5Theme.Mercs);
        var mercs = Render(window);
        MechWarrior5.Shared.Wpf.Theme.ThemeManager.Apply(MechWarrior5.Shared.Wpf.Theme.MechWarrior5Theme.Clans);
        var clans = Render(window);
        MechWarrior5.Shared.Wpf.Theme.ThemeManager.Apply(before);

        var half = mercs.PixelWidth / 2;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(new CroppedBitmap(mercs, new Int32Rect(0, 0, half, mercs.PixelHeight)), new Rect(0, 0, half, mercs.PixelHeight));
            dc.DrawImage(new CroppedBitmap(clans, new Int32Rect(half, 0, clans.PixelWidth - half, clans.PixelHeight)),
                new Rect(half, 0, clans.PixelWidth - half, clans.PixelHeight));
            dc.DrawRectangle(Brushes.White, null, new Rect(half - 2, 0, 4, mercs.PixelHeight));
        }

        var bitmap = new RenderTargetBitmap(mercs.PixelWidth, mercs.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Save(bitmap, path);
    }

    private static RenderTargetBitmap Render(Window window)
    {
        // Mech pictures load asynchronously (cache or Sarna); give them time like a player would.
        var until = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < until)
        {
            Settle();
            Thread.Sleep(50);
        }

        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }

    private static void Save(BitmapSource bitmap, string pathWithoutExtension)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(pathWithoutExtension + ".png");
        encoder.Save(file);
        TestContext.Out.WriteLine($"{Path.GetFileName(pathWithoutExtension)}.png {bitmap.PixelWidth}x{bitmap.PixelHeight}");
    }

    private static void Size(Window window)
    {
        window.Width = ShotWidth;
        window.Height = ShotHeight;
        Settle();
    }

    private static void Settle()
    {
        for (var i = 0; i < 30; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    private static void Shot(Window window, string folder, string name) => Save(Render(window), Path.Combine(folder, name));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MechWarrior5.SaveEditor.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
