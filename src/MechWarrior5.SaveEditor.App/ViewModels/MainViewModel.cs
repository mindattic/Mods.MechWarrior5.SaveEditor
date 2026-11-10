using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;
using MechWarrior5.SaveEditor.App.Localization;
using MechWarrior5.Shared.Wpf.Theme;
using MechWarrior5.Shared.Unreal;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public enum Page
{
    Start,
    Company,
    Mechs,
    Inventory,
    Pilots,
    Factions,
    Settings,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly MechCatalog _mechs;
    private readonly ItemCatalog _items;
    private readonly PilotCatalog _pilots;
    private readonly IDialogService _dialogs;
    private readonly JsonSettings<AppSettings> _settings;
    private readonly IEditorWindows _windows;
    private IReadOnlyList<MissingAsset> _baselineMissing = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(FileName), nameof(SaveTitle), nameof(WindowTitle), nameof(DlcSummary))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand), nameof(CloseCommand))]
    private Mw5Document? _document;

    [ObservableProperty]
    private Page _page = Page.Start;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    private string? _toast;

    /// <summary>The name the save gets in the game's load menu.</summary>
    [ObservableProperty]
    private string _saveName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveLabel))]
    private bool _isOriginal;

    public string SaveLabel => IsOriginal ? Loc.Text("save_as_new", "SAVE AS NEW") : Loc.Text("save", "SAVE");

    public MainViewModel(MechCatalog mechs, ItemCatalog items, PilotCatalog pilots, IDialogService dialogs, JsonSettings<AppSettings> settings, IEditorWindows windows, string imageCache)
    {
        SettingsPage = new SettingsViewModel(this, settings, imageCache);
        _pilots = pilots;
        _windows = windows;
        _mechs = mechs;
        _items = items;
        _dialogs = dialogs;
        _settings = settings;
        Start = new StartViewModel(this);
        CurrentView = Start;
        _ = ReloadGameDataAsync();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            RebuildPages();
            GameDataSummary = _summary();
            OnPropertyChanged(nameof(SaveLabel));
        };
    }

    /// <summary>Summary of what the game scan found, for the status bar.</summary>
    [ObservableProperty]
    private string _gameDataSummary = Loc.Text("game_data_reading", "Reading game data...");

    /// <summary>
    /// Reads the installed game's and enabled mods' 'Mech data in the background, then folds it into the
    /// catalog (new and modded variants, DLC membership, stock loadouts) and refreshes the open pages.
    /// </summary>
    public async Task ReloadGameDataAsync()
    {
        // A rescan started while an earlier one runs supersedes it: only the latest result is applied.
        var generation = ++_scanGeneration;
        SetSummary(() => Loc.Text("game_data_reading", "Reading game data..."));
        var preferred = _settings.Current.GameFolder;
        GameData? data;
        try
        {
            data = await Task.Run(() => GameInstall.Find(preferred) is { } folder ? GameDataScanner.Scan(folder, _mechs) : null);
        }
        catch (Exception e)
        {
            // Started fire-and-forget at launch, so nothing may escape: report it and keep the built-in data.
            if (generation == _scanGeneration)
            {
                SetSummary(() => Loc.Text("game_data_failed", "Couldn't read game data: using built-in data ({0})", e.Message));
            }

            return;
        }

        if (generation != _scanGeneration)
        {
            return;
        }

        if (data is null)
        {
            // The status says built-in data, so stop using an earlier scan's mods and loadouts too.
            _mechs.Reset();
            _baselineMissing = [];
            SetSummary(() => Loc.Text("game_data_missing", "Game not found: using built-in data"));
            RebuildPages();
            return;
        }

        _mechs.Apply(data);
        if (Document is { } open)
        {
            _baselineMissing = SaveValidator.FindMissing(open.File, data.AssetNames);
        }

        var mods = data.Mechs.Count(m => m.Variant.ModName is not null);
        SetSummary(() => mods > 0
            ? Loc.Text("game_data_mods", "Game data: {0} mechs ({1} from mods)", data.Mechs.Count, mods)
            : Loc.Text("game_data", "Game data: {0} mechs", data.Mechs.Count));
        RebuildPages();
    }

    private int _scanGeneration;
    private Func<string> _summary = () => Loc.Text("game_data_reading", "Reading game data...");

    /// <summary>Keeps how the summary is worded, so a language change can re-word it.</summary>
    private void SetSummary(Func<string> summary)
    {
        _summary = summary;
        GameDataSummary = summary();
    }

    public StartViewModel Start { get; }

    public SettingsViewModel SettingsPage { get; }

    public CompanyViewModel? Company { get; private set; }

    public MechsViewModel? Mechs { get; private set; }

    public InventoryViewModel? Inventory { get; private set; }

    public PilotsViewModel? Pilots { get; private set; }

    public FactionsViewModel? Factions { get; private set; }

    public bool HasDocument => Document is not null;

    public string? FileName => Document is null ? null : Path.GetFileName(Document.FilePath);

    /// <summary>The open save's friendly load-menu name (from Campaign.json), else its file name.</summary>
    public string? SaveTitle => Document is null ? null : Document.DisplayName ?? Path.GetFileNameWithoutExtension(Document.FilePath);

    public string WindowTitle =>
        Document is null ? "MechWarrior 5 // Save Editor" : $"{(IsDirty ? "* " : string.Empty)}{SaveTitle} - MechWarrior 5 // Save Editor";

    public string DlcSummary => Document?.Dlc.Source switch
    {
        null => string.Empty,
        DlcSource.Unknown => Loc.Text("dlc_unknown_summary", "DLC: unknown (no Campaign.json)"),
        _ => $"DLC {Document.Dlc.Tags.Count}/{_mechs.DlcPacks.Count}",
    };

    public AppSettings Settings => _settings.Current;

    public void SaveSettings() => _settings.Save();

    public bool IsClansTheme
    {
        get => ThemeManager.Current == MechWarrior5Theme.Clans;
        set => SetTheme(value ? MechWarrior5Theme.Clans : MechWarrior5Theme.Mercs);
    }

    public bool IsMercsTheme
    {
        get => ThemeManager.Current == MechWarrior5Theme.Mercs;
        set => SetTheme(value ? MechWarrior5Theme.Mercs : MechWarrior5Theme.Clans);
    }

    private void SetTheme(MechWarrior5Theme theme)
    {
        if (ThemeManager.Current != theme)
        {
            ThemeManager.Apply(theme);
            _settings.Current.Theme = theme.ToString();
            _settings.Save();
        }

        OnPropertyChanged(nameof(IsClansTheme));
        OnPropertyChanged(nameof(IsMercsTheme));
    }

    public string? DefaultFolder =>
        Path.GetDirectoryName(_settings.Current.LastSavePath) is { } last && Directory.Exists(last)
            ? last
            : CampaignLibrary.DefaultSaveRoot;

    /// <summary>Opens a save; returns false if the user cancelled or the file was rejected.</summary>
    public bool OpenPath(string path)
    {
        if (!ConfirmLeaveDocument())
        {
            return false;
        }

        Mw5Document doc;
        try
        {
            doc = Mw5Document.Open(path, _mechs);
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            _dialogs.Show(Loc.Text("cant_open_save", "Can't open save"), e.Message, DialogTone.Error);
            return false;
        }

        AttachDocument(doc);
        _baselineMissing = _mechs.Game is { } game ? SaveValidator.FindMissing(doc.File, game.AssetNames) : [];
        _settings.Current.LastSavePath = path;
        _settings.Save();
        Navigate(Page.Mechs);
        ShowToast(Loc.Text("loaded_toast", "Loaded {0} - {1} mechs, {2} pilots", Company!.CompanyName, Mechs!.HangarCount, doc.Roster.Pilots.Count));
        return true;
    }

    /// <summary>Called by the main window before closing.</summary>
    public bool ConfirmLeaveDocument()
    {
        if (Document is null || !IsDirty)
        {
            return true;
        }

        return _dialogs.AskSaveChanges(SaveTitle!) switch
        {
            true => TrySave(null),
            false => true,
            null => false,
        };
    }

    public void ShowToast(string message) => Toast = message;

    [RelayCommand]
    private void Browse()
    {
        if (_dialogs.PickSaveToOpen(DefaultFolder) is { } path)
        {
            OpenPath(path);
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void Save() => TrySave(null);

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void SaveAs()
    {
        if (_dialogs.PickSaveAs(Document!.FilePath) is { } path)
        {
            TrySave(path);
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void Close()
    {
        if (!ConfirmLeaveDocument())
        {
            return;
        }

        if (Document is not null)
        {
            Document.DirtyChanged -= OnDirtyChanged;
        }

        Document = null;
        Company = null;
        Mechs = null;
        Inventory = null;
        Pilots = null;
        Factions = null;
        IsDirty = false;
        OnPropertyChanged(string.Empty);
        Start.Refresh();
        Navigate(Page.Start);
    }

    [RelayCommand]
    private void Navigate(Page page)
    {
        if (page is not (Page.Start or Page.Settings) && Document is null)
        {
            return;
        }

        Page = page;
        CurrentView = page switch
        {
            Page.Company => Company,
            Page.Mechs => Mechs,
            Page.Inventory => Inventory,
            Page.Pilots => Pilots,
            Page.Factions => Factions,
            Page.Settings => SettingsPage,
            _ => Start,
        };
    }

    /// <summary>Saves to <paramref name="path"/>, or by default to a new save (originals) or in place (editor saves).</summary>
    private bool TrySave(string? path)
    {
        if (!ConfirmAssetsResolve())
        {
            return false;
        }

        SaveOutcome outcome;
        try
        {
            outcome = Document!.Save(path, SaveName);
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            _dialogs.Show(Loc.Text("save_failed", "Save failed"), Loc.Text("save_failed_text", "Nothing was written. {0}", e.Message), DialogTone.Error);
            return false;
        }

        // The save is on disk from here on: nothing below may report it as failed.
        _settings.Current.LastSavePath = outcome.Path;
        try
        {
            _settings.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        IsOriginal = Document.IsOriginalGameSave;
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(SaveTitle));
        OnPropertyChanged(nameof(WindowTitle));
        ShowToast(outcome switch
        {
            { CreatedNewFile: true, RegisteredInCampaign: true } => Loc.Text("saved_new", "Saved as new save \"{0}\" in the game's load menu. The original is untouched.", SaveName),
            { CreatedNewFile: true } => Loc.Text("saved_unlisted", "Saved as {0} (not listed in the campaign index). The original is untouched.", Path.GetFileName(outcome.Path)),
            _ => Loc.Text("saved_in_place", "Saved \"{0}\" (previous version kept as .bak).", SaveName),
        });
        Start.Refresh();
        return true;
    }

    /// <summary>
    /// Warns before saving when the edits added references the installed game and enabled mods don't
    /// have (references that were already unresolved, e.g. from disabled mods, don't count).
    /// </summary>
    private bool ConfirmAssetsResolve()
    {
        if (_mechs.Game is not { } game || Document is not { } doc)
        {
            return true;
        }

        doc.Hangar.Commit();
        var introduced = SaveValidator.Introduced(SaveValidator.FindMissing(doc.File, game.AssetNames), _baselineMissing);
        return introduced.Count == 0 || _dialogs.Confirm(
            Loc.Text("unknown_assets", "Unknown game assets"),
            Loc.Text("unknown_assets_text", "Your edits reference {0} asset(s) that aren't in the installed game or its enabled mods. The game may not load them. Save anyway?", introduced.Count)
            + "\n\n"
            + string.Join("\n", introduced.Take(12).Select(m => $"  {m.AssetName}  ({m.AssetType})")),
            Loc.Text("save_anyway", "Save anyway"),
            DialogTone.Error);
    }

    private void AttachDocument(Mw5Document doc)
    {
        if (Document is not null)
        {
            Document.DirtyChanged -= OnDirtyChanged;
        }

        doc.DirtyChanged += OnDirtyChanged;
        Company = new CompanyViewModel(doc, _mechs, _pilots, _dialogs, RebuildPages);
        Mechs = new MechsViewModel(this, doc, _mechs, _items, _pilots, _dialogs, _windows);
        Inventory = new InventoryViewModel(doc, _items, _dialogs);
        Pilots = new PilotsViewModel(doc, _pilots, _dialogs);
        Factions = new FactionsViewModel(doc);
        Document = doc;
        IsDirty = false;
        IsOriginal = doc.IsOriginalGameSave;
        var listed = doc.DisplayName ?? Path.GetFileNameWithoutExtension(doc.FilePath);
        SaveName = IsOriginal ? CampaignIndex.DefaultCopyName(listed) : listed;
        OnPropertyChanged(nameof(Company));
        OnPropertyChanged(nameof(Mechs));
        OnPropertyChanged(nameof(Inventory));
        OnPropertyChanged(nameof(Pilots));
        OnPropertyChanged(nameof(Factions));
    }

    /// <summary>Recreates the data pages after a bulk change (e.g. an import) so they show the new contents.</summary>
    private void RebuildPages()
    {
        if (Document is not { } doc)
        {
            return;
        }

        // Same save, new language or game data: keep each page's searches, filters, ticks and selection.
        var (oldMechs, oldInventory, oldPilots) = (Mechs, Inventory, Pilots);
        Mechs = new MechsViewModel(this, doc, _mechs, _items, _pilots, _dialogs, _windows);
        Inventory = new InventoryViewModel(doc, _items, _dialogs);
        Pilots = new PilotsViewModel(doc, _pilots, _dialogs);
        if (oldMechs is not null)
        {
            Mechs.CarryOver(oldMechs);
        }

        if (oldInventory is not null)
        {
            Inventory.CarryOver(oldInventory);
        }

        if (oldPilots is not null)
        {
            Pilots.CarryOver(oldPilots);
        }

        Company?.Refresh();
        Factions = new FactionsViewModel(doc); // mood labels are worded in the current language
        OnPropertyChanged(nameof(Mechs));
        OnPropertyChanged(nameof(Inventory));
        OnPropertyChanged(nameof(Pilots));
        OnPropertyChanged(nameof(Factions));

        // The page on screen still shows the old view model; point it at the rebuilt one.
        if (Page is Page.Mechs or Page.Inventory or Page.Pilots or Page.Factions)
        {
            Navigate(Page);
        }
    }

    private void OnDirtyChanged(object? sender, EventArgs e)
    {
        IsDirty = Document?.IsDirty ?? false;
        Company?.Refresh();
    }
}
