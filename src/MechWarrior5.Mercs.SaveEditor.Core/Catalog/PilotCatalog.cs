using System.Text.Json;
using MechWarrior5.Mercs.SaveEditor.Core.Addons;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

public sealed record PersonaPreset(string Asset, string? FullName, string? Callsign, string? Portrait, string? Voice)
{
    public string DisplayName => FullName ?? Callsign ?? Asset;
}

/// <summary>A pilot or 'Mech trait; <paramref name="Description"/> is the game's own text, when it has one.</summary>
public sealed record TraitDefinition(string Asset, string Label, string? Description = null)
{
    /// <summary>The mod or add-on pack the trait comes from; null for the base game.</summary>
    public string? Source { get; init; }

    /// <summary>The label in pickers: a mod's trait names its mod.</summary>
    public string PickerLabel => Source is null ? Label : $"{Label}  ({Source})";
}

/// <summary>
/// Portraits, dialogue voices, persona presets and pilot/mech traits (see THIRD-PARTY-NOTICES.md). Traits are the
/// built-in ones, then the enabled mods', then the active add-on packs'; <see cref="Apply"/> rebuilds them each time.
/// </summary>
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
        _builtInPilot = traits.Pilot;
        _builtInMech = traits.Mech;
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

    public string TraitLabel(string asset) => FindTrait(asset)?.Label ?? asset;

    /// <summary>The pilot or 'Mech trait with this asset name; null when no source defines it.</summary>
    public TraitDefinition? FindTrait(string asset) => _byAsset.GetValueOrDefault(asset);

    public bool IsBuiltInTrait(string asset) =>
        _builtInPilot.Concat(_builtInMech).Any(t => string.Equals(t.Asset, asset, StringComparison.OrdinalIgnoreCase));

    /// <summary>Built-in traits, plus the enabled mods' traits, plus the active packs' additions and changes.</summary>
    public void Apply(GameData? game, AddonState addons)
    {
        PilotTraits = Merge(_builtInPilot, game?.PilotTraits ?? [], addons.Active.Select(p => (p.Name, p.PilotTraits)));
        MechTraits = Merge(_builtInMech, game?.MechTraits ?? [], addons.Active.Select(p => (p.Name, p.MechTraits)));
        _byAsset = Index();
    }

    /// <summary>Back to the built-in traits alone.</summary>
    public void Reset()
    {
        PilotTraits = _builtInPilot;
        MechTraits = _builtInMech;
        _byAsset = Index();
    }

    private static List<TraitDefinition> Merge(
        IReadOnlyList<TraitDefinition> builtIn, IEnumerable<TraitDefinition> scanned, IEnumerable<(string Pack, AddonList<AddonTrait> List)> packs)
    {
        var traits = builtIn.ToDictionary(t => t.Asset, StringComparer.OrdinalIgnoreCase);
        var added = new List<string>();
        foreach (var trait in scanned.Where(t => traits.TryAdd(t.Asset, t)))
        {
            added.Add(trait.Asset);
        }

        foreach (var (pack, list) in packs)
        {
            foreach (var trait in list.Add.Concat(list.Override))
            {
                if (traits.TryGetValue(trait.Asset, out var known))
                {
                    traits[trait.Asset] = known with { Label = trait.Label ?? known.Label, Description = trait.Description ?? known.Description, Source = known.Source ?? pack };
                }
                else if (list.Add.Contains(trait))
                {
                    traits[trait.Asset] = new TraitDefinition(trait.Asset, trait.Label ?? AssetLabel.HumanizeTrait(trait.Asset), trait.Description) { Source = pack };
                    added.Add(trait.Asset);
                }
            }
        }

        return [.. builtIn.Select(t => traits[t.Asset]), .. added.Select(a => traits[a]).OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)];
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
