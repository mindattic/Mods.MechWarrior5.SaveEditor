using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mw5SaveEditor.App.Services;
using Mw5SaveEditor.Core.Catalog;
using Mw5SaveEditor.Core.Game;
using Mw5SaveEditor.Core.Unreal;

namespace Mw5SaveEditor.App.ViewModels;

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
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settings;

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

    public MainViewModel(MechCatalog mechs, ItemCatalog items, IDialogService dialogs, SettingsService settings)
    {
        _mechs = mechs;
        _items = items;
        _dialogs = dialogs;
        _settings = settings;
        Start = new StartViewModel(this);
        CurrentView = Start;
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
            true => TrySave(Document.FilePath),
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
    private void Save() => TrySave(Document!.FilePath);

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

    private bool TrySave(string path)
    {
        try
        {
            Document!.Save(path);
            _settings.Current.LastSavePath = path;
            _settings.Save();
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(WindowTitle));
            ShowToast($"Saved {Path.GetFileName(path)} (previous version kept as .bak)");
            return true;
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            _dialogs.Show("Save failed", $"Nothing was written. {e.Message}", DialogTone.Error);
            return false;
        }
    }

    private void AttachDocument(Mw5Document doc)
    {
        if (Document is not null)
        {
            Document.DirtyChanged -= OnDirtyChanged;
        }

        doc.DirtyChanged += OnDirtyChanged;
        Company = new CompanyViewModel(doc, _mechs);
        Mechs = new MechsViewModel(this, doc, _mechs, _dialogs);
        Inventory = new InventoryViewModel(doc, _items, _dialogs);
        Pilots = new PilotsViewModel(doc);
        Factions = new FactionsViewModel(doc);
        Document = doc;
        IsDirty = false;
        OnPropertyChanged(nameof(Company));
        OnPropertyChanged(nameof(Mechs));
        OnPropertyChanged(nameof(Inventory));
        OnPropertyChanged(nameof(Pilots));
        OnPropertyChanged(nameof(Factions));
    }

    private void OnDirtyChanged(object? sender, EventArgs e)
    {
        IsDirty = Document?.IsDirty ?? false;
        Company?.Refresh();
    }
}
