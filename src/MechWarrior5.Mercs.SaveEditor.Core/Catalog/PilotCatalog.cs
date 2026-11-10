using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

public sealed record PersonaPreset(string Asset, string? FullName, string? Callsign, string? Portrait, string? Voice)
{
    public string DisplayName => FullName ?? Callsign ?? Asset;
}

public sealed record TraitDefinition(string Asset, string Label)
{
    /// <summary>The game's long description (runtime placeholders shown as "X"), when the asset has one.</summary>
    public string? Description { get; init; }

    /// <summary>Set for traits an enabled mod adds.</summary>
    public string? ModName { get; init; }

    /// <summary>The label in pickers: a mod's trait names its mod.</summary>
    public string PickerLabel => ModName is null ? Label : $"{Label}  ({ModName})";
}

/// <summary>Portraits, dialogue voices, persona presets and pilot/mech traits (see THIRD-PARTY-NOTICES.md).</summary>
public sealed class PilotCatalog
{
    public const string PortraitType = "MWPortraitAsset";
    public const string VoiceType = "MWDialogueVoiceAsset";
    public const string PilotTraitType = "MWPilotTraitDataAsset";
    public const string MechTraitType = "MWMechTraitDataAsset";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyList<TraitDefinition> _builtInPilot;
    private readonly IReadOnlyList<TraitDefinition> _builtInMech;
    private Dictionary<string, TraitDefinition> _byAsset;

    private PilotCatalog(PilotsFile pilots, TraitsFile traits)
    {
        Personas = pilots.Personas;
        Portraits = pilots.Portraits;
        Voices = pilots.Voices;
        _builtInPilot = [.. traits.Pilot.Select(Cleaned)];
        _builtInMech = [.. traits.Mech.Select(Cleaned)];
        PilotTraits = _builtInPilot;
        MechTraits = _builtInMech;
        _byAsset = Index();
    }

    public IReadOnlyList<PersonaPreset> Personas { get; }

    public IReadOnlyList<string> Portraits { get; }

    public IReadOnlyList<string> Voices { get; }

    public IReadOnlyList<TraitDefinition> PilotTraits { get; private set; }

    public IReadOnlyList<TraitDefinition> MechTraits { get; private set; }

    public static PilotCatalog LoadBuiltIn()
    {
        using var pilots = MechCatalog.OpenResource("pilots.json");
        using var traits = MechCatalog.OpenResource("traits.json");
        return new PilotCatalog(
            JsonSerializer.Deserialize<PilotsFile>(pilots, Json) ?? throw new InvalidDataException("pilots.json is empty"),
            JsonSerializer.Deserialize<TraitsFile>(traits, Json) ?? throw new InvalidDataException("traits.json is empty"));
    }

    /// <summary>
    /// Adds the pilot and mech traits the scanned game's enabled mods define. A rescan replaces the last one, so a
    /// mod disabled since then no longer offers its traits; built-in traits always stay.
    /// </summary>
    public void Apply(GameData game)
    {
        PilotTraits = Merge(_builtInPilot, game.PilotTraits);
        MechTraits = Merge(_builtInMech, game.MechTraits);
        _byAsset = Index();
    }

    /// <summary>Back to the built-in traits alone.</summary>
    public void Reset()
    {
        PilotTraits = _builtInPilot;
        MechTraits = _builtInMech;
        _byAsset = Index();
    }

    public TraitDefinition? FindTrait(string asset) => _byAsset.GetValueOrDefault(asset);

    public string TraitLabel(string asset) => FindTrait(asset)?.Label ?? asset;

    private static TraitDefinition Cleaned(TraitDefinition trait) =>
        trait.Description is null ? trait : trait with { Description = AssetLabel.CleanText(trait.Description) };

    private static List<TraitDefinition> Merge(IReadOnlyList<TraitDefinition> builtIn, IEnumerable<TraitDefinition> scanned)
    {
        var known = builtIn.Select(t => t.Asset).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. builtIn, .. scanned.Where(t => !known.Contains(t.Asset)).OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)];
    }

    private Dictionary<string, TraitDefinition> Index()
    {
        var index = new Dictionary<string, TraitDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var trait in PilotTraits.Concat(MechTraits))
        {
            index.TryAdd(trait.Asset, trait);
        }

        return index;
    }

    private sealed record PilotsFile(List<PersonaPreset> Personas, List<string> Portraits, List<string> Voices);

    private sealed record TraitsFile(List<TraitDefinition> Pilot, List<TraitDefinition> Mech);
}
