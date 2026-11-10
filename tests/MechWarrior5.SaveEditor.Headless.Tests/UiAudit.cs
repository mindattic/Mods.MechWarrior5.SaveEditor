using System.Windows;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Shared.Wpf.Dialogs;
using MechWarrior5.Shared.Wpf.Services;
using ClansPage = MechWarrior5.SaveEditor.App.Clans.ViewModels.Page;
using MercsPage = MechWarrior5.SaveEditor.App.ViewModels.Page;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Renders every screen and window of both games (each page, the Mech Lab's four tabs, Settings, the start pages
/// and each dialog shape) at 1920x1080 into MW5_AUDIT_DIR, for a visual review against the UI rules. Explicit; it
/// writes nothing into the repo.
/// </summary>
public partial class UiHeadlessTests
{
    [Test]
    [Explicit("Writes renders for a visual review into MW5_AUDIT_DIR.")]
    [Category("Audit")]
    public void Render_every_screen_for_review()
    {
        var folder = Environment.GetEnvironmentVariable("MW5_AUDIT_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Path.GetTempPath(), "MW5SaveEditorAudit");
        Directory.CreateDirectory(folder);

        // Mercenaries
        var (_, mercsSave) = SaveFixtures.MercsCampaignCopy();
        LoadoutEditorViewModel? lab = null;
        var (shell, window, _) = Launch(clansFolder: null, new FakeEditorWindows(editor => lab = editor), downloads: true);
        Size(window);
        Shot(window, folder, "m00-start");
        shell.OpenPath(mercsSave);
        WaitForGameData(shell.Mercs);
        foreach (var page in new[] { MercsPage.Company, MercsPage.Mechs, MercsPage.Inventory, MercsPage.Pilots, MercsPage.Factions, MercsPage.Settings })
        {
            shell.Mercs.NavigateCommand.Execute(page);
            Shot(window, folder, $"m{(int)page:00}-{page.ToString().ToLowerInvariant()}");
        }

        shell.Mercs.NavigateCommand.Execute(MercsPage.Mechs);
        var mechs = (App.ViewModels.MechsViewModel)shell.Mercs.CurrentView!;
        foreach (var row in mechs.CatalogView.Cast<CatalogMechViewModel>().Take(3))
        {
            row.IsChecked = true;
        }

        foreach (var row in mechs.Hangar.Take(2))
        {
            row.IsChecked = true;
        }

        Shot(window, folder, "m20-mechs-ticked");
        mechs.ShowColdStorageOnly = true;
        Shot(window, folder, "m21-mechs-cold-storage");
        mechs.ShowColdStorageOnly = false;

        var refit = mechs.Hangar.First(h => h.Record.IsEditable);
        mechs.EditLoadoutCommand.Execute(refit);
        var labWindow = new App.Views.LoadoutEditorWindow(lab!) { Left = -4000, Top = 0, ShowActivated = false };
        labWindow.Show();
        Size(labWindow);
        foreach (var tab in Enum.GetValues<LoadoutTab>())
        {
            lab!.Tab = tab;
            Shot(labWindow, folder, $"m30-mechlab-{tab.ToString().ToLowerInvariant()}");
        }

        labWindow.Close();
        window.Close();

        // Clans
        var (clansFolder, clansSave) = SaveFixtures.ClansFolderCopy();
        (shell, window, _) = Launch(clansFolder, downloads: true);
        Size(window);
        shell.IsClansGame = true;
        Shot(window, folder, "c00-start");
        shell.OpenPath(clansSave);
        foreach (var page in new[] { ClansPage.Resources, ClansPage.Mechs, ClansPage.Inventory, ClansPage.Progress, ClansPage.Settings })
        {
            shell.Clans.NavigateCommand.Execute(page);
            Shot(window, folder, $"c{(int)page:00}-{page.ToString().ToLowerInvariant()}");
        }

        shell.Clans.NavigateCommand.Execute(ClansPage.Mechs);
        ((App.Clans.ViewModels.MechsViewModel)shell.Clans.CurrentView!).SelectDamagedCommand.Execute(null);
        Shot(window, folder, "c20-mechs-damaged-ticked");

        // Dialogs, in the Clans theme and then the Mercs one.
        Dialog(folder, "d01-info", new MessageDialogModel("Nothing selected", "Tick the mechs to change (Shift+click ticks a range).", DialogTone.Info, [], "OK", null, null));
        Dialog(folder, "d02-batch", new MessageDialogModel("Repaired", "Save to keep the changes.", DialogTone.Success,
            [new DialogSection("DONE (3)", DialogTone.Success, ["Atlas AS7-D", "Kit Fox", "Nova Prime"]),
             new DialogSection("SKIPPED (1)", DialogTone.Warning, ["Mad Dog: already in full repair"])], "OK", null, null));
        Dialog(folder, "d03-confirm", new MessageDialogModel("Remove mechs", "Remove 2 mech(s) from the inventory?", DialogTone.Error, [], "Remove", "Cancel", null));
        Dialog(folder, "d04-unsaved", new MessageDialogModel("Unsaved changes", "Save the changes to \"Smoke Jaguar Reinforced\"?", DialogTone.Warning, [], "Save", "Cancel", "Discard"));
        window.Close();
        MechWarrior5.Shared.Wpf.Theme.ThemeManager.Apply(MechWarrior5.Shared.Wpf.Theme.MechWarrior5Theme.Mercs);
        Dialog(folder, "d05-mercs-unsaved", new MessageDialogModel("Unsaved changes", "Save the changes to \"Iron Reckoning\"?", DialogTone.Warning, [], "Save", "Cancel", "Discard"));
        TestContext.Out.WriteLine(folder);
    }

    private static void Dialog(string folder, string name, MessageDialogModel model)
    {
        var dialog = (Window)Activator.CreateInstance(typeof(MessageDialog), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, [model], null)!;
        dialog.Left = -4000;
        dialog.Top = 0;
        dialog.ShowActivated = false;
        dialog.Show();
        Settle();
        Shot(dialog, folder, name);
        dialog.Close();
    }
}
