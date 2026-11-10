using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [RelayCommand]
    private void SetAll(string value)
    {
        _doc.Factions.SetAll(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        foreach (var faction in Factions)
        {
            faction.Refresh();
        }
    }
}
