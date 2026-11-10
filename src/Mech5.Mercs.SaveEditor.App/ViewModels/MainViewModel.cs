using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.App.Services;
using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;
using Mech5.Mercs.SaveEditor.Core.Saves;
using Mech5.Shared.Wpf.Theme;
using Mech5.Shared.Unreal;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public enum Page
{
    Start,
    Company,
    Mechs,
    Inventory,
    Pilots,
    Factions,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly MechCatalog _mechs;
    private readonly ItemCatalog _items;
    private readonly PilotCatalog _pilots;
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settings;
    private readonly IEditorWindows _windows;
    private IReadOnlyList<MissingAsset> _baselineMissing = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(FileName), nameof(WindowTitle), nameof(DlcSummary))]
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

    public string SaveLabel => IsOriginal ? "SAVE AS NEW" : "SAVE";

    public MainViewModel(MechCatalog mechs, ItemCatalog items, PilotCatalog pilots, IDialogService dialogs, SettingsService settings, IEditorWindows windows)
    {
        _pilots = pilots;
        _windows = windows;
        _mechs = mechs;
        _items = items;
        _dialogs = dialogs;
        _settings = settings;
        Start = new StartViewModel(this);
        CurrentView = Start;
        _ = LoadGameDataAsync();
    }

    /// <summary>Summary of what the game scan found, for the status bar.</summary>
    [ObservableProperty]
    private string _gameDataSummary = "Reading game data...";

    /// <summary>
    /// Reads the installed game's and enabled mods' 'Mech data in the background, then folds it into the
    /// catalog (new and modded variants, DLC membership, stock loadouts) and refreshes the open pages.
    /// </summary>
    private async Task LoadGameDataAsync()
    {
        var preferred = _settings.Current.GameFolder;
        var data = await Task.Run(() => GameInstall.Find(preferred) is { } folder ? GameDataScanner.Scan(folder, _mechs) : null);
        if (data is null)
        {
            GameDataSummary = "Game not found: using built-in data";
            return;
        }

        _mechs.Apply(data);
        if (Document is { } open)
        {
            _baselineMissing = SaveValidator.FindMissing(open.File, data.AssetNames);
        }

        var mods = data.Mechs.Count(m => m.Variant.ModName is not null);
        GameDataSummary = mods > 0
            ? $"Game data: {data.Mechs.Count} mechs ({mods} from mods)"
            : $"Game data: {data.Mechs.Count} mechs";
        RebuildPages();
    }

    public StartViewModel Start { get; }

    public CompanyViewModel? Company { get; private set; }

    public MechsViewModel? Mechs { get; private set; }

    public InventoryViewModel? Inventory { get; private set; }

    public PilotsViewModel? Pilots { get; private set; }

    public FactionsViewModel? Factions { get; private set; }

    public bool HasDocument => Document is not null;

    public string? FileName => Document is null ? null : Path.GetFileName(Document.FilePath);

    public string WindowTitle =>
        Document is null ? "MW5 Mercs Save Editor" : $"{(IsDirty ? "* " : string.Empty)}{FileName} - MW5 Mercs Save Editor";

    public string DlcSummary => Document?.Dlc.Source switch
    {
        null => string.Empty,
        DlcSource.Unknown => "DLC: unknown (no Campaign.json)",
        _ => $"DLC {Document.Dlc.Tags.Count}/{_mechs.DlcPacks.Count}",
    };

    public AppSettings Settings => _settings.Current;

    public bool IsClansTheme
    {
        get => ThemeManager.Current == Mech5Theme.Clans;
        set => SetTheme(value ? Mech5Theme.Clans : Mech5Theme.Mercs);
    }

    public bool IsMercsTheme
    {
        get => ThemeManager.Current == Mech5Theme.Mercs;
        set => SetTheme(value ? Mech5Theme.Mercs : Mech5Theme.Clans);
    }

    private void SetTheme(Mech5Theme theme)
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
            _dialogs.Show("Can't open save", e.Message, DialogTone.Error);
            return false;
        }

        AttachDocument(doc);
        _baselineMissing = _mechs.Game is { } game ? SaveValidator.FindMissing(doc.File, game.AssetNames) : [];
        _settings.Current.LastSavePath = path;
        _settings.Save();
        Navigate(Page.Mechs);
        ShowToast($"Loaded {Company!.CompanyName} - {Mechs!.HangarCount} mechs, {doc.Roster.Pilots.Count} pilots");
        return true;
    }

    /// <summary>Called by the main window before closing.</summary>
    public bool ConfirmLeaveDocument()
    {
        if (Document is null || !IsDirty)
        {
            return true;
        }

        return _dialogs.AskSaveChanges(FileName!) switch
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
        if (page != Page.Start && Document is null)
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

        try
        {
            var outcome = Document!.Save(path, SaveName);
            _settings.Current.LastSavePath = outcome.Path;
            _settings.Save();
            IsOriginal = Document.IsOriginalGameSave;
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(WindowTitle));
            ShowToast(outcome switch
            {
                { CreatedNewFile: true, RegisteredInCampaign: true } => $"Saved as new save \"{SaveName}\" in the game's load menu. The original is untouched.",
                { CreatedNewFile: true } => $"Saved as {Path.GetFileName(outcome.Path)} (not listed in the campaign index). The original is untouched.",
                _ => $"Saved \"{SaveName}\" (previous version kept as .bak).",
            });
            Start.Refresh();
            return true;
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            _dialogs.Show("Save failed", $"Nothing was written. {e.Message}", DialogTone.Error);
            return false;
        }
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
            "Unknown game assets",
            $"Your edits reference {introduced.Count} asset(s) that aren't in the installed game or its enabled mods. "
            + "The game may not load them. Save anyway?\n\n"
            + string.Join("\n", introduced.Take(12).Select(m => $"  {m.AssetName}  ({m.AssetType})")),
            "Save anyway",
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

        Mechs = new MechsViewModel(this, doc, _mechs, _items, _pilots, _dialogs, _windows);
        Inventory = new InventoryViewModel(doc, _items, _dialogs);
        Pilots = new PilotsViewModel(doc, _pilots, _dialogs);
        OnPropertyChanged(nameof(Mechs));
        OnPropertyChanged(nameof(Inventory));
        OnPropertyChanged(nameof(Pilots));
    }

    private void OnDirtyChanged(object? sender, EventArgs e)
    {
        IsDirty = Document?.IsDirty ?? false;
        Company?.Refresh();
    }
}
