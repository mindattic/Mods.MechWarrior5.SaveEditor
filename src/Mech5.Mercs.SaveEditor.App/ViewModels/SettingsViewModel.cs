using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.App.Localization;
using Mech5.Mercs.SaveEditor.App.Services;
using Mech5.Shared.Wpf.Behaviors;
using Microsoft.Win32;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly SettingsService _settings;
    private readonly string _imageCache;

    [ObservableProperty]
    private string _gameFolder;

    [ObservableProperty]
    private string? _cacheStatus;

    public SettingsViewModel(MainViewModel main, SettingsService settings, string imageCache)
    {
        _main = main;
        _settings = settings;
        _imageCache = imageCache;
        _gameFolder = settings.Current.GameFolder ?? string.Empty;
    }

    public IReadOnlyList<LanguageOption> Languages { get; } = Loc.Languages;

    public LanguageOption Language
    {
        get => Loc.Languages.First(l => l.Code == Loc.Instance.Language);
        set
        {
            Loc.Instance.Language = value.Code;
            _settings.Current.Language = value.Code;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool DownloadImages
    {
        get => _settings.Current.DownloadMechImages;
        set
        {
            _settings.Current.DownloadMechImages = value;
            if (MechArt.Service is { } images)
            {
                images.Enabled = value;
            }

            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string MainViewSummary => _main.GameDataSummary;

    public string Version { get; } =
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "2";

    public string SettingsFile => _settings.FilePath;

    [RelayCommand]
    private void BrowseGameFolder()
    {
        var dialog = new OpenFolderDialog { Title = Loc.Text("pick_game_folder", "Pick the MechWarrior 5 Mercenaries folder") };
        if (dialog.ShowDialog() == true)
        {
            GameFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task RescanGame()
    {
        _settings.Current.GameFolder = string.IsNullOrWhiteSpace(GameFolder) ? null : GameFolder.Trim();
        _settings.Save();
        await _main.ReloadGameDataAsync();
        OnPropertyChanged(nameof(MainViewSummary));
    }

    [RelayCommand]
    private void ClearImageCache()
    {
        var removed = 0;
        if (Directory.Exists(_imageCache))
        {
            foreach (var file in Directory.EnumerateFiles(_imageCache, "*.img"))
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (IOException)
                {
                }
            }
        }

        CacheStatus = Loc.Text("cache_cleared", "Removed {0} cached pictures. They download again as needed.", removed);
    }

    [RelayCommand]
    private static void OpenProjectPage() =>
        Process.Start(new ProcessStartInfo("https://github.com/mindattic/Mods.Mech5.Mercs.SaveEditor") { UseShellExecute = true });
}
