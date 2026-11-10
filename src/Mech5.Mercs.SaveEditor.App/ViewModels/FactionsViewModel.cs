using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

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
        >= 75 => "ALLIED",
        >= 25 => "FRIENDLY",
        > -25 => "NEUTRAL",
        > -75 => "UNFRIENDLY",
        _ => "HOSTILE",
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
