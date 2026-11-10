using System.IO;
using System.Windows;
using System.Windows.Threading;
using Mw5SaveEditor.App.Behaviors;
using Mw5SaveEditor.App.Services;
using Mw5SaveEditor.App.ViewModels;
using Mw5SaveEditor.Core.Catalog;

namespace Mw5SaveEditor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MW5SaveEditor");
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MW5SaveEditor", "MechImages");
        var settings = new SettingsService(appData);
        var images = new MechImageService(cache) { Enabled = settings.Current.DownloadMechImages };
        MechArt.Service = images;
        Exit += (_, _) => images.Dispose();

        var main = new MainViewModel(MechCatalog.LoadBuiltIn(), ItemCatalog.LoadBuiltIn(), new DialogService(), settings);
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
