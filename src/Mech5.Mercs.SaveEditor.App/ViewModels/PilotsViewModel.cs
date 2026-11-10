using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mech5.Mercs.SaveEditor.Core.Game;

namespace Mech5.Mercs.SaveEditor.App.ViewModels;

public sealed partial class SkillViewModel(PilotSkill skill) : ObservableObject
{
    public string Name { get; } = Names.Humanize(skill.Name);

    public bool HasCap => skill.HasCap;

    public bool IsParent => !skill.HasCap;

    public int Cap
    {
        get => skill.Cap;
        set
        {
            skill.Cap = value;
            OnPropertyChanged();
        }
    }

    public int Experience
    {
        get => skill.Experience;
        set
        {
            skill.Experience = value;
            OnPropertyChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(string.Empty);
}

public sealed partial class PilotViewModel : ObservableObject
{
    public PilotViewModel(Pilot pilot)
    {
        Pilot = pilot;
        Skills = pilot.Skills.Select(s => new SkillViewModel(s)).ToList();
    }

    public Pilot Pilot { get; }

    public IReadOnlyList<SkillViewModel> Skills { get; }

    public string Name => Pilot.DisplayName;

    public string Initials => string.Concat(Name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.FirstOrDefault(char.IsLetterOrDigit))
        .Where(c => c != default)
        .Take(2)
        .Select(char.ToUpperInvariant));

    public bool IsPlayer => Pilot.IsPlayer;

    public string Health => Pilot.Health;

    public bool IsHealthy => Pilot.IsHealthy;

    public int Salary
    {
        get => Pilot.Salary;
        set
        {
            Pilot.Salary = value;
            OnPropertyChanged();
        }
    }

    public int TotalExperience => Skills.Where(s => s.HasCap).Sum(s => s.Experience);

    [RelayCommand]
    private void Heal()
    {
        Pilot.Heal();
        OnPropertyChanged(nameof(Health));
        OnPropertyChanged(nameof(IsHealthy));
    }

    [RelayCommand]
    private void MaxCaps()
    {
        Pilot.MaxCaps();
        foreach (var skill in Skills)
        {
            skill.Refresh();
        }
    }
}

public sealed partial class PilotsViewModel : ObservableObject
{
    [ObservableProperty]
    private PilotViewModel? _selected;

    public PilotsViewModel(Mw5Document doc)
    {
        Pilots = doc.Roster.Pilots.Select(p => new PilotViewModel(p)).ToList();
        _selected = Pilots.Count > 0 ? Pilots[0] : null;
    }

    public IReadOnlyList<PilotViewModel> Pilots { get; }

    [RelayCommand]
    private void HealAll()
    {
        foreach (var pilot in Pilots)
        {
            pilot.HealCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void MaxAllCaps()
    {
        foreach (var pilot in Pilots)
        {
            pilot.MaxCapsCommand.Execute(null);
        }
    }
}
