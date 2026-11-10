using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.SaveEditor.App.Clans.ViewModels;
using MechWarrior5.SaveEditor.App.Services;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Wpf.Theme;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public enum Game
{
    Mercs,
    Clans,
}

/// <summary>
/// The window's top level: one editor per game, with the title bar's game tabs choosing which one
/// is shown. Each keeps its own open save, so switching games never loses edits.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly JsonSettings<AppSettings> _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current), nameof(IsMercsGame), nameof(IsClansGame))]
    private Game _game;

    public ShellViewModel(MainViewModel mercs, ClansMainViewModel clans, JsonSettings<AppSettings> settings)
    {
        Mercs = mercs;
        Clans = clans;
        _settings = settings;
        _game = Enum.TryParse<Game>(settings.Current.Game, ignoreCase: true, out var game) ? game : Game.Mercs;
        ThemeManager.ThemeChanged += (_, _) => OnPropertyChanged(nameof(IsClansTheme));
    }

    /// <summary>True while the Clans (ice) theme is on; the title-bar theme button shows the Clans star, else a skull.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Bound by the title bar; raises PropertyChanged when the theme changes.")]
    public bool IsClansTheme => ThemeManager.Current == MechWarrior5Theme.Clans;

    public MainViewModel Mercs { get; }

    public ClansMainViewModel Clans { get; }

    public object Current => Game == Game.Clans ? Clans : Mercs;

    public bool IsMercsGame
    {
        get => Game == Game.Mercs;
        set
        {
            if (value)
            {
                Game = Game.Mercs;
            }
        }
    }

    public bool IsClansGame
    {
        get => Game == Game.Clans;
        set
        {
            if (value)
            {
                Game = Game.Clans;
            }
        }
    }

    /// <summary>Opens a save in the editor for its game: Clans saves are <c>!GZIP</c> archives.</summary>
    public void OpenPath(string path)
    {
        var isClans = false;
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[5];
            isClans = stream.Read(head) == 5 && GzipChunkArchive.IsArchive(head);
        }
        catch (IOException)
        {
        }

        Game = isClans ? Game.Clans : Game.Mercs;
        if (isClans)
        {
            Clans.OpenPath(path);
        }
        else
        {
            Mercs.OpenPath(path);
        }
    }

    /// <summary>Switches between the Mercs (amber) and Clans (ice) themes; the choice is remembered.</summary>
    [RelayCommand]
    private void ToggleTheme() => Mercs.IsClansTheme = !IsClansTheme;

    /// <summary>Asks about unsaved edits in either game before the window closes.</summary>
    public bool ConfirmExit() => Mercs.ConfirmLeaveDocument() && Clans.ConfirmLeaveDocument();

    partial void OnGameChanged(Game value)
    {
        _settings.Current.Game = value.ToString();
        _settings.Save();
    }
}
