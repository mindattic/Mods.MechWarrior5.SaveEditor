using MechWarrior5.SaveEditor.App.Localization;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Clans.SaveEditor.Core.Game;
using MechWarrior5.Clans.SaveEditor.Core.Saves;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Wpf.Theme;

namespace MechWarrior5.SaveEditor.App.Clans.ViewModels;

public enum Page
{
    Start,
    Resources,
    Mechs,
    Inventory,
    Progress,
    Settings,
}

public sealed partial class ClansMainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly JsonSettings<MechWarrior5.SaveEditor.App.Services.AppSettings> _settings;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(FileName), nameof(SaveTitle), nameof(IsOriginal), nameof(WindowTitle), nameof(SaveLabel))]
    private ClansDocument? _document;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private Page _page = Page.Start;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    private string _saveName = string.Empty;

    [ObservableProperty]
    private string _toast = "Open a campaign save to begin.";

    [ObservableProperty]
    private string _kerenskiesText = string.Empty;

    public ClansMainViewModel(IDialogService dialogs, JsonSettings<MechWarrior5.SaveEditor.App.Services.AppSettings> settings)
    {
        _dialogs = dialogs;
        _settings = settings;
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast = string.Empty;
        };
        ThemeManager.ThemeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsClansTheme));
            OnPropertyChanged(nameof(IsMercsTheme));
        };
        Start = new StartViewModel(this, SaveFolder);
        Navigate(Page.Start);
    }

    public StartViewModel Start { get; }

    public string SaveFolder => string.IsNullOrWhiteSpace(_settings.Current.ClansSaveFolder) ? SaveLibrary.DefaultFolder : _settings.Current.ClansSaveFolder;

    public MechWarrior5.SaveEditor.App.Services.AppSettings Settings => _settings.Current;

    public bool HasDocument => Document is not null;

    public bool IsOriginal => Document?.IsOriginalGameSave == true;

    public string? FileName => Document is null ? null : Path.GetFileName(Document.Path);

    /// <summary>The open save's friendly load-menu name (from UserSaveMetaData), else its file name.</summary>
    public string? SaveTitle => Document is null ? null : Document.DisplayName() ?? Path.GetFileNameWithoutExtension(Document.Path);

    public string WindowTitle => Document is null
        ? "MW5 Clans Save Editor"
        : $"{(IsDirty ? "* " : string.Empty)}{SaveName} - MW5 Clans Save Editor";

    public string SaveLabel => IsOriginal ? "SAVE AS NEW" : "SAVE";

    public bool IsClansTheme
    {
        get => ThemeManager.Current == MechWarrior5Theme.Clans;
        set
        {
            if (value)
            {
                SetTheme(MechWarrior5Theme.Clans);
            }
        }
    }

    public bool IsMercsTheme
    {
        get => ThemeManager.Current == MechWarrior5Theme.Mercs;
        set
        {
            if (value)
            {
                SetTheme(MechWarrior5Theme.Mercs);
            }
        }
    }

    public void SetTheme(MechWarrior5Theme theme)
    {
        ThemeManager.Apply(theme);
        _settings.Current.Theme = theme.ToString();
        _settings.Save();
    }

    public void SaveSettings() => _settings.Save();

    public void ShowToast(string message)
    {
        Toast = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    /// <summary>Called by pages after any edit.</summary>
    public void MarkDirty()
    {
        IsDirty = true;
        RefreshSummary();
    }

    public void OpenPath(string path)
    {
        if (!ConfirmLeaveDocument())
        {
            return;
        }

        ClansDocument doc;
        try
        {
            doc = ClansDocument.Load(path);
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.Show(Loc.Text("cant_open_save", "Can't open save"), e.Message, DialogTone.Error);
            return;
        }

        Document = doc;
        SaveName = doc.DisplayName() is { Length: > 0 } name
            ? (doc.IsOriginalGameSave ? name + " [Edited]" : name)
            : "Edited Save";
        IsDirty = false;
        _settings.Current.ClansLastSavePath = path;
        _settings.Save();
        RefreshSummary();
        Navigate(Page.Resources);
        ShowToast($"Loaded {SaveTitle}: {doc.Inventory.Mechs.Count()} mechs, {doc.Currency("Kerenskies_CurrencyItem"):N0} Kerenskies");
    }

    /// <summary>Asks about unsaved edits; false means stay.</summary>
    public bool ConfirmLeaveDocument()
    {
        if (!IsDirty || Document is null)
        {
            return true;
        }

        switch (_dialogs.AskSaveChanges(SaveName))
        {
            case true:
                return SaveCore();
            case false:
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private void Navigate(Page page)
    {
        if (page is not (Page.Start or Page.Settings) && Document is null)
        {
            page = Page.Start;
        }

        Page = page;
        CurrentView = page switch
        {
            Page.Resources => new ResourcesViewModel(Document!, this),
            Page.Mechs => new MechsViewModel(Document!, this, _dialogs),
            Page.Inventory => new InventoryViewModel(Document!, this, _dialogs),
            Page.Progress => new ProgressViewModel(Document!),
            Page.Settings => new SettingsViewModel(this),
            _ => Start.Refresh(),
        };
    }

    [RelayCommand]
    private void Browse()
    {
        if (_dialogs.PickSaveToOpen(SaveFolder) is { } path)
        {
            OpenPath(path);
        }
    }

    [RelayCommand]
    private void Save() => SaveCore();

    [RelayCommand]
    private void Close()
    {
        if (ConfirmLeaveDocument())
        {
            Document = null;
            IsDirty = false;
            Navigate(Page.Start);
        }
    }

    private bool SaveCore()
    {
        if (Document is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SaveName))
        {
            _dialogs.Show("Name the save", "Give the save a name; it is how the game's load menu will list it.", DialogTone.Warning);
            return false;
        }

        try
        {
            var outcome = Document.Save(SaveName.Trim());
            IsDirty = false;
            OnPropertyChanged(nameof(IsOriginal));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(SaveTitle));
            OnPropertyChanged(nameof(SaveLabel));
            ShowToast(outcome switch
            {
                { CreatedNewFile: true, RegisteredInLoadMenu: true } => $"Saved as new save \"{SaveName}\" in the game's load menu. The original is untouched.",
                { CreatedNewFile: true } => $"Saved as {Path.GetFileName(outcome.Path)} (not found in the load-menu index). The original is untouched.",
                _ => $"Saved \"{SaveName}\" (previous version kept as .bak).",
            });
            return true;
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.Show("Save failed", $"Nothing was overwritten. {e.Message}", DialogTone.Error);
            return false;
        }
    }

    private void RefreshSummary() =>
        KerenskiesText = Document is null ? string.Empty : $"{Document.Currency("Kerenskies_CurrencyItem"):N0}";

    partial void OnSaveNameChanged(string value) => OnPropertyChanged(nameof(WindowTitle));
}
