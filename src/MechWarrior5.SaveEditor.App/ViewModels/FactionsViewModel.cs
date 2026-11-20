using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

public sealed partial class FactionViewModel(FactionStanding standing) : ObservableObject
{
    public string Name { get; } = Names.Humanize(standing.Faction);

    public int Standing
    {
        get => standing.Standing;
        set
        {
            standing.Standing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Mood));
        }
    }

    /// <summary>The slider's range: the active limits, widened to a standing already beyond them so showing it never changes it.</summary>
    public int MinStanding => Math.Min(GameRules.Limits.MinStanding, Standing);

    public int MaxStanding => Math.Max(GameRules.Limits.MaxStanding, Standing);

    public string Mood => Standing switch
    {
        >= 75 => Loc.Text("mood_allied", "ALLIED"),
        >= 25 => Loc.Text("mood_friendly", "FRIENDLY"),
        > -25 => Loc.Text("mood_neutral", "NEUTRAL"),
        > -75 => Loc.Text("mood_unfriendly", "UNFRIENDLY"),
        _ => Loc.Text("mood_hostile", "HOSTILE"),
    };

    public void Refresh()
    {
        OnPropertyChanged(nameof(Standing));
        OnPropertyChanged(nameof(Mood));
    }
}

public sealed partial class FactionsViewModel : ObservableObject
{
    private readonly Mw5Document _doc;

    public FactionsViewModel(Mw5Document doc)
    {
        _doc = doc;
        Factions = doc.Factions.Standings.Select(s => new FactionViewModel(s)).ToList();
    }

    public IReadOnlyList<FactionViewModel> Factions { get; }

    // An instance property bound normally: x:Static read it once, and a rescan raising the limit reuses the same view.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Bound per view model, so a new one re-reads it")]
    public string AllAlliedLabel => Loc.Text("all_allied_max", "ALL ALLIED ({0})", GameRules.Limits.MaxStanding);

    /// <summary>"max" is the active limit; anything else is a number.</summary>
    [RelayCommand]
    private void SetAll(string value)
    {
        _doc.Factions.SetAll(value == "max" ? GameRules.Limits.MaxStanding : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        foreach (var faction in Factions)
        {
            faction.Refresh();
        }
    }
}
