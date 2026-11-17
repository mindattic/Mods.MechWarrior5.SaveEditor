using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;
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
            else if (e.PropertyName is nameof(MainViewModel.Addons) or nameof(MainViewModel.Compatibility))
            {
                OnPropertyChanged(nameof(Addons));
                OnPropertyChanged(nameof(HasAddons));
                OnPropertyChanged(nameof(LimitLines));
                OnPropertyChanged(nameof(Collisions));
                OnPropertyChanged(nameof(Compatibility));
                OnPropertyChanged(nameof(CompatibilitySummary));
                OnPropertyChanged(nameof(HasCompatibility));
            }
        };
    }

    /// <summary>One row per add-on pack file, with its status and on/off switch.</summary>
    public IReadOnlyList<AddonRowViewModel> Addons =>
        [.. _main.Addons.Entries.Select(e => new AddonRowViewModel(e, SetAddonOn))];

    public bool HasAddons => _main.Addons.Entries.Count > 0;

    /// <summary>Forces vanilla: no pack applies while this is on.</summary>
    public bool AddonsOff
    {
        get => _settings.Current.AddonsOff;
        set
        {
            _settings.Current.AddonsOff = value;
            _settings.Save();
            _main.ApplyAddons();
            OnPropertyChanged();
        }
    }

    /// <summary>Each limit in force, marked with the mod or pack that widened it past vanilla.</summary>
    public IReadOnlyList<string> LimitLines => [.. GameLimits.Keys.Select(key =>
    {
        var value = _main.Addons.Limits[key].ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        return _main.Addons.LimitSources.TryGetValue(key, out var source)
            ? Loc.Text("addon_limit_from", "{0}: {1}  (from {2})", LimitName(key), value, source)
            : Loc.Text("addon_limit_vanilla", "{0}: {1}  (vanilla)", LimitName(key), value);
    })];

    /// <summary>A limit's translated name; one literal key per <see cref="GameLimits.Keys"/> entry, so the key test checks each.</summary>
    private static string LimitName(string key) => key switch
    {
        "maxSkillCap" => Loc.Text("limit_skill_cap", "Pilot skill cap"),
        "maxExperience" => Loc.Text("limit_experience", "Pilot experience"),
        "maxSalary" => Loc.Text("limit_salary", "Pilot salary"),
        "maxCBills" => Loc.Text("limit_cbills", "C-Bills"),
        "minStanding" => Loc.Text("limit_min_standing", "Lowest faction standing"),
        "maxStanding" => Loc.Text("limit_max_standing", "Highest faction standing"),
        "maxCount" => Loc.Text("limit_stack", "Items per stack"),
        _ => key,
    };

    public IReadOnlyList<string> Collisions => _main.Addons.Collisions;

    public IReadOnlyList<CompatibilityRowViewModel> Compatibility => [.. _main.Compatibility.Select(l => new CompatibilityRowViewModel(l))];

    public bool HasCompatibility => _main.Compatibility.Count > 0;

    public string CompatibilitySummary => _main.Document is null
        ? Loc.Text("compat_no_save", "Open a save to check it against the installed game and mods.")
        : _main.Compatibility.Count == 0
            ? Loc.Text("compat_vanilla", "This save matches the vanilla game: nothing in it needs a mod.")
            : Loc.Text("compat_summary", "{0} problem(s): content or values that need a mod that is missing, disabled or the wrong version. {1} modded value(s) covered by active mods.",
                _main.Compatibility.Count(l => l.Severity == CompatibilitySeverity.Problem),
                _main.Compatibility.Count(l => l.Severity == CompatibilitySeverity.Modded));

    private void SetAddonOn(string pack, bool on)
    {
        var off = (_settings.Current.DisabledAddons ?? []).Where(n => !string.Equals(n, pack, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!on)
        {
            off.Add(pack);
        }

        _settings.Current.DisabledAddons = off.Count == 0 ? null : off;
        _settings.Save();
        _main.ApplyAddons();
    }

    [RelayCommand]
    private static void OpenAddonsFolder()
    {
        Directory.CreateDirectory(MainViewModel.AddonsFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{MainViewModel.AddonsFolder}\"") { UseShellExecute = false });
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
