using MechWarrior5.SaveEditor.App.Localization;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Clans.SaveEditor.Core.Catalog;
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
    Pilots,
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

    // Navigate() always builds a fresh page view model (never reuses one), so the page it replaces
    // must be released here - otherwise a disposable one (e.g. SettingsViewModel) leaks its subscriptions.
    partial void OnCurrentViewChanging(object? oldValue, object? newValue) => (oldValue as IDisposable)?.Dispose();

    [ObservableProperty]
    private Page _page = Page.Start;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    private string _saveName = string.Empty;

    [ObservableProperty]
    private string _toast = Loc.Text("clans_open_to_begin", "Open a campaign save to begin.");

    [ObservableProperty]
    private string _kerenskiesText = string.Empty;

    /// <summary>The installed Clans game's and mods' asset names, read in the background at startup (no Oodle needed).</summary>
    private readonly Task<ClansGameIndex?> _index = Task.Run(() =>
    {
        try
        {
            return MechWarrior5.Mercs.SaveEditor.Core.Catalog.GameInstall.FindClans() is { } folder ? ClansGameIndex.Read(folder) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    });

    /// <summary>Assets the open save uses that the base game doesn't have, and the installed mod behind each.</summary>
    [ObservableProperty]
    private IReadOnlyList<ClansCompatibilityLine> _compatibility = [];

    /// <summary>Whether <see cref="Compatibility"/> is a finished check: null while it runs, false when the game couldn't be read.</summary>
    [ObservableProperty]
    private bool? _compatibilityChecked;

    /// <summary>The installed Clans mods (Mods folder), once the background read has finished.</summary>
    public IReadOnlyList<ClansMod> InstalledMods => _index.IsCompletedSuccessfully && _index.Result is { } index ? index.Mods : [];

    public ClansMainViewModel(IDialogService dialogs, JsonSettings<MechWarrior5.SaveEditor.App.Services.AppSettings> settings)
    {
        _dialogs = dialogs;
        _settings = settings;
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SaveLabel));
            // Page text built in code (labels, summaries, research entries) is worded once, so the open page is rebuilt -
            // but not the list pages, whose ticks, filter and typed values a rebuild would lose: the Mechs page rewords its
            // summary in place, and the Inventory and Pilots pages word nothing in code.
            switch (CurrentView)
            {
                case MechsViewModel mechs:
                    mechs.Reword();
                    break;
                case SettingsViewModel settings:
                    settings.Reword();
                    break;
                case InventoryViewModel or PilotsViewModel:
                    break;
                default:
                    if (Page is not (Page.Start or Page.Settings))
                    {
                        Navigate(Page);
                    }

                    break;
            }
        };
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
        _ = NotifyWhenIndexedAsync();
    }

    /// <summary>The installed-mods list fills in once the background read finishes, even on a page already open.</summary>
    private async Task NotifyWhenIndexedAsync()
    {
        await _index;
        OnPropertyChanged(nameof(InstalledMods));
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
        ? "MechWarrior 5 // Save Editor"
        : $"{(IsDirty ? "* " : string.Empty)}{SaveName} - MechWarrior 5 // Save Editor";

    public string SaveLabel => IsOriginal ? Loc.Text("save_as_new", "SAVE AS NEW") : Loc.Text("save", "SAVE");

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

    public void ShowError(string message) => _dialogs.Show(Loc.Text("error", "Error"), message, DialogTone.Error);

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
            : Loc.Text("clans_edited_save", "Edited Save");
        IsDirty = false;
        _settings.Current.ClansLastSavePath = path;
        _settings.Save();
        RefreshSummary();
        Navigate(Page.Resources);
        ShowToast(Loc.Text("clans_loaded", "Loaded {0}: {1} mechs, {2:N0} Kerenskies", SaveTitle!, doc.Inventory.Mechs.Count(), doc.Currency("Kerenskies_CurrencyItem")));
        _ = CheckCompatibilityAsync(doc);
    }

    private async Task CheckCompatibilityAsync(ClansDocument doc)
    {
        Compatibility = [];
        CompatibilityChecked = null;
        var index = await _index;
        if (!ReferenceEquals(Document, doc))
        {
            return; // another save was opened meanwhile; its own check reports
        }

        if (index is null)
        {
            // The game or its index couldn't be read: an empty list here must not read as "matches the vanilla game".
            CompatibilityChecked = false;
            return;
        }

        Compatibility = ClansCompatibility.Build(doc, index);
        CompatibilityChecked = true;
        var problems = Compatibility.Count(l => l.Problem);
        if (problems > 0)
        {
            ShowToast(Loc.Text("compat_toast", "{0} thing(s) need a mod that isn't active: see Settings, Compatibility.", problems));
        }
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
            Page.Pilots => new PilotsViewModel(Document!, this),
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
            Compatibility = [];
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
            _dialogs.Show(Loc.Text("clans_name_the_save", "Name the save"), Loc.Text("clans_name_the_save_text", "Give the save a name; it is how the game's load menu will list it."), DialogTone.Warning);
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
                { CreatedNewFile: true, RegisteredInLoadMenu: true } => Loc.Text("saved_new", "Saved as new save \"{0}\" in the game's load menu. The original is untouched.", SaveName),
                { CreatedNewFile: true } => Loc.Text("clans_saved_unlisted", "Saved as {0} (not found in the load-menu index). The original is untouched.", Path.GetFileName(outcome.Path)),
                _ => Loc.Text("saved_in_place", "Saved \"{0}\" (previous version kept as .bak).", SaveName),
            });
            return true;
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.Show(Loc.Text("save_failed", "Save failed"), Loc.Text("clans_save_failed_text", "Nothing was overwritten. {0}", e.Message), DialogTone.Error);
            return false;
        }
    }

    private void RefreshSummary() =>
        KerenskiesText = Document is null ? string.Empty : $"{Document.Currency("Kerenskies_CurrencyItem"):N0}";

    partial void OnSaveNameChanged(string value)
    {
        OnPropertyChanged(nameof(WindowTitle));
        // A rename is an edit too (OpenPath sets the loaded name, then clears the flag).
        if (Document is not null)
        {
            IsDirty = true;
        }
    }
}
