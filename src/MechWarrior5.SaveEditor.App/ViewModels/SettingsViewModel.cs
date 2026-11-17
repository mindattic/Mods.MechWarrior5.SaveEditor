using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.SaveEditor.App.Localization;
using MechWarrior5.SaveEditor.App.Services;
using Microsoft.Win32;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly JsonSettings<AppSettings> _settings;

    [ObservableProperty]
    private string _gameFolder;

    public SettingsViewModel(MainViewModel main, JsonSettings<AppSettings> settings)
    {
        _main = main;
        _settings = settings;
        _gameFolder = settings.Current.GameFolder ?? string.Empty;
        // The startup scan can finish (or the language change) while this page is on screen.
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GameDataSummary))
            {
                OnPropertyChanged(nameof(MainViewSummary));
            }
        };
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
    }
}
