using System.IO;
using System.Windows;
using System.Windows.Threading;
using Mech5.Shared.Wpf.Behaviors;
using Mech5.Shared.Wpf.Theme;
using Mech5.Mercs.SaveEditor.App.Services;
using Mech5.Mercs.SaveEditor.App.ViewModels;
using Mech5.Mercs.SaveEditor.Core.Catalog;

namespace Mech5.Mercs.SaveEditor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mech5SaveEditor", "MechImages");
        var settings = new SettingsService();
        ThemeManager.Apply(ThemeManager.TryParse(settings.Current.Theme, out var theme) ? theme : Mech5Theme.Mercs);
        var images = new MechImageService(cache) { Enabled = settings.Current.DownloadMechImages };
        MechArt.Service = images;
        Exit += (_, _) => images.Dispose();

        var main = new MainViewModel(MechCatalog.LoadBuiltIn(), ItemCatalog.LoadBuiltIn(), PilotCatalog.LoadBuiltIn(), new DialogService(), settings, new EditorWindows());
        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        window.Show();

        if (e.Args.FirstOrDefault(File.Exists) is { } path)
        {
            main.OpenPath(path);
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
