using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

/// <summary>
/// One Cantina career path: its points in this save and the level they reach. Levels and rewards come from the game's
/// (or a mod's) career path asset; the game offers each reached level's reward to claim in the Cantina.
/// </summary>
public sealed partial class CareerRowViewModel(CareerPathDefinition path, TraitsEditor traits, PilotCatalog catalog) : ObservableObject
{
    public string Name => path.Name;

    public string? Source => path.Source;

    public int MaxPoints => path.MaxPoints;

    /// <summary>A path the save hasn't started can only be added once the game has written its career list.</summary>
    public bool CanEdit => traits.CanStartCareers || traits.CareerPoints.ContainsKey(path.Asset);

    public int Points
    {
        get => traits.CareerPoints.GetValueOrDefault(path.Asset);
        set
        {
            traits.SetCareerPoints(path.Asset, Math.Clamp(value, 0, MaxPoints));
            OnPropertyChanged(string.Empty);
        }
    }

    public string LevelText
    {
        get
        {
            var level = path.LevelFor(Points);
            var next = path.Levels.FirstOrDefault(l => Points < l.Points);
            return next is null
                ? Loc.Text("career_level_max", "Level {0} of {1} (complete)", level, path.Levels.Count)
                : Loc.Text("career_level", "Level {0} of {1}  -  next at {2} points: {3}", level, path.Levels.Count, next.Points,
                    next.TraitReward is { } t ? catalog.TraitLabel(t) : "-");
        }
    }

    [RelayCommand]
    private void NextLevel()
    {
        if (path.Levels.FirstOrDefault(l => Points < l.Points) is { } next)
        {
            Points = next.Points;
        }
    }

    [RelayCommand]
    private void Max() => Points = MaxPoints;
}
