using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

using MechWarrior5.SaveEditor.App.Localization;

namespace MechWarrior5.SaveEditor.App.ViewModels;

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

public sealed record TraitRow(string Asset, string Label);

public sealed partial class PilotViewModel : ObservableObject
{
    private readonly PilotCatalog _catalog;

    [ObservableProperty]
    private TraitDefinition? _traitToAdd;

    [ObservableProperty]
    private PersonaPreset? _persona;

    public PilotViewModel(Pilot pilot, PilotCatalog catalog)
    {
        Pilot = pilot;
        _catalog = catalog;
        Skills = pilot.Skills.Select(s => new SkillViewModel(s)).ToList();
        RefreshTraits();
    }

    public Pilot Pilot { get; }

    public IReadOnlyList<SkillViewModel> Skills { get; }

    public ObservableCollection<TraitRow> Traits { get; } = [];

    public IReadOnlyList<string> Portraits => _catalog.Portraits;

    /// <summary>The known voices, plus this pilot's own if it isn't one of them (a mod's), so the box shows what the save has.</summary>
    public IReadOnlyList<string> Voices =>
        Pilot.Voice is { Length: > 0 } own && !_catalog.Voices.Contains(own, StringComparer.OrdinalIgnoreCase)
            ? [own, .. _catalog.Voices]
            : _catalog.Voices;

    public IReadOnlyList<PersonaPreset> Personas => _catalog.Personas;

    public IReadOnlyList<TraitDefinition> TraitChoices => _catalog.PilotTraits;

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

    public string FullName
    {
        get => Pilot.FullName;
        set
        {
            Pilot.FullName = value;
            OnNameChanged();
        }
    }

    public string Callsign
    {
        get => Pilot.Callsign;
        set
        {
            Pilot.Callsign = value;
            OnNameChanged();
        }
    }

    public string Bio
    {
        get => Pilot.Bio;
        set
        {
            Pilot.Bio = value;
            OnPropertyChanged();
        }
    }

    public string? Portrait
    {
        get => Pilot.Portrait;
        set
        {
            Pilot.Portrait = value;
            OnPropertyChanged();
        }
    }

    public string? Voice
    {
        get => Pilot.Voice;
        set
        {
            Pilot.Voice = value;
            OnPropertyChanged();
        }
    }

    public int Salary
    {
        get => Pilot.Salary;
        set
        {
            Pilot.Salary = value;
            OnPropertyChanged();
        }
    }

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

    [RelayCommand]
    private void ApplyPersona()
    {
        if (Persona is null)
        {
            return;
        }

        Pilot.ApplyPersona(Persona);
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void AddTrait()
    {
        if (TraitToAdd is not null && Pilot.AddTrait(TraitToAdd.Asset))
        {
            RefreshTraits();
        }
    }

    [RelayCommand]
    private void RemoveTrait(TraitRow? trait)
    {
        if (trait is not null && Pilot.RemoveTrait(trait.Asset))
        {
            RefreshTraits();
        }
    }

    private void OnNameChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(Callsign));
    }

    private void RefreshTraits()
    {
        Traits.Clear();
        foreach (var asset in Pilot.Traits)
        {
            Traits.Add(new TraitRow(asset, _catalog.TraitLabel(asset)));
        }
    }
}

public sealed partial class PilotsViewModel : ObservableObject
{
    private readonly Mw5Document _doc;
    private readonly PilotCatalog _catalog;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private PilotViewModel? _selected;

    public PilotsViewModel(Mw5Document doc, PilotCatalog catalog, IDialogService dialogs)
    {
        _doc = doc;
        _catalog = catalog;
        _dialogs = dialogs;
        Rebuild(null);
    }

    public ObservableCollection<PilotViewModel> Pilots { get; } = [];

    /// <summary>Keeps the previously selected pilot selected (the page is rebuilt on a language change or rescan).</summary>
    public void CarryOver(PilotsViewModel old) =>
        Selected = Pilots.FirstOrDefault(p => p.Pilot.PersonaId == old.Selected?.Pilot.PersonaId) ?? Selected;

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

    /// <summary>Hires a recruit modelled on the selected pilot; rename them in the panel.</summary>
    [RelayCommand]
    private void Hire()
    {
        var template = Selected?.Pilot ?? (_doc.Roster.Pilots.Count > 0 ? _doc.Roster.Pilots[0] : null);
        if (template is null || !_doc.Roster.CanHire)
        {
            _dialogs.Show(Loc.Text("cant_hire", "Can't hire"), Loc.Text("cant_hire_text", "This save has no pilot to model a recruit on."), DialogTone.Warning);
            return;
        }

        var hired = _doc.Roster.Hire(template, Loc.Text("new_recruit", "New Recruit"), Loc.Text("recruit", "Recruit"));
        Rebuild(hired);
    }

    [RelayCommand]
    private void Release()
    {
        if (Selected is not { } pilot
            || !_dialogs.Confirm(Loc.Text("release_pilot", "Release pilot"), Loc.Text("release_pilot_text", "Release {0} from the company?", pilot.Name), Loc.Instance["release"], DialogTone.Error))
        {
            return;
        }

        var result = _doc.Roster.Release([pilot.Pilot]);
        if (result.Skipped.Count > 0)
        {
            _dialogs.Show(Loc.Text("cant_release", "Can't release"), $"{pilot.Name}: {result.Skipped[0].Reason}.", DialogTone.Warning);
            return;
        }

        Rebuild(null);
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (Selected is { } pilot && _doc.Roster.Move(pilot.Pilot, delta))
        {
            Rebuild(pilot.Pilot);
        }
    }

    private void Rebuild(Pilot? select)
    {
        Pilots.Clear();
        foreach (var pilot in _doc.Roster.Pilots)
        {
            Pilots.Add(new PilotViewModel(pilot, _catalog));
        }

        Selected = Pilots.FirstOrDefault(p => select is not null && p.Pilot.PersonaId == select.PersonaId)
            ?? (Pilots.Count > 0 ? Pilots[0] : null);
    }
}
