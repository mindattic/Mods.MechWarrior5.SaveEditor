using System.IO;
using System.Windows;
using System.Windows.Threading;
using MechWarrior5.Shared.Wpf.Behaviors;
using MechWarrior5.Shared.Wpf.Theme;
using MechWarrior5.SaveEditor.App.Localization;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.SaveEditor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MechWarrior5SaveEditor", "MechImages");
        var settings = new JsonSettings<AppSettings>();
        Loc.Instance.Language = string.IsNullOrEmpty(settings.Current.Language) ? Loc.DefaultLanguage() : settings.Current.Language;
        ThemeManager.Apply(ThemeManager.TryParse(settings.Current.Theme, out var theme) ? theme : MechWarrior5Theme.Mercs);
        var images = new MechImageService(cache) { Enabled = settings.Current.DownloadMechImages };
        MechArt.Service = images;
        Exit += (_, _) => images.Dispose();

        var main = new MainViewModel(MechCatalog.LoadBuiltIn(), ItemCatalog.LoadBuiltIn(), PilotCatalog.LoadBuiltIn(), new DialogService(), settings, new EditorWindows(), cache);
        var shell = new ShellViewModel(main, new Clans.ViewModels.ClansMainViewModel(new DialogService(), settings), settings);
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Show();

        if (e.Args.FirstOrDefault(File.Exists) is { } path)
        {
            shell.OpenPath(path);
        }
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        new DialogService().Show(
            "Unexpected error",
            $"{e.Exception.Message}\n\nYour save on disk was not modified. Use Save As to keep any edits in a separate file.",
            DialogTone.Error);
        e.Handled = true;
    }
}
