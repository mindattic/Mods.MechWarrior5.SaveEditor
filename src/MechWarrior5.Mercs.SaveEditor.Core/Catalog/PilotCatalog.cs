using System.Text.Json;

namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

public sealed record PersonaPreset(string Asset, string? FullName, string? Callsign, string? Portrait, string? Voice)
{
    public string DisplayName => FullName ?? Callsign ?? Asset;
}

public sealed record TraitDefinition(string Asset, string Label);

/// <summary>Portraits, dialogue voices, persona presets and pilot/mech traits (see THIRD-PARTY-NOTICES.md).</summary>
public sealed class PilotCatalog
{
    public const string PortraitType = "MWPortraitAsset";
    public const string VoiceType = "MWDialogueVoiceAsset";
    public const string PilotTraitType = "MWPilotTraitDataAsset";
    public const string MechTraitType = "MWMechTraitDataAsset";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private PilotCatalog(PilotsFile pilots, TraitsFile traits)
    {
        Personas = pilots.Personas;
        Portraits = pilots.Portraits;
        Voices = pilots.Voices;
        PilotTraits = traits.Pilot;
        MechTraits = traits.Mech;
    }

    public IReadOnlyList<PersonaPreset> Personas { get; }

    public IReadOnlyList<string> Portraits { get; }

    public IReadOnlyList<string> Voices { get; }

    public IReadOnlyList<TraitDefinition> PilotTraits { get; }

    public IReadOnlyList<TraitDefinition> MechTraits { get; }

    public static PilotCatalog LoadBuiltIn()
    {
        using var pilots = MechCatalog.OpenResource("pilots.json");
        using var traits = MechCatalog.OpenResource("traits.json");
        return new PilotCatalog(
            JsonSerializer.Deserialize<PilotsFile>(pilots, Json) ?? throw new InvalidDataException("pilots.json is empty"),
            JsonSerializer.Deserialize<TraitsFile>(traits, Json) ?? throw new InvalidDataException("traits.json is empty"));
    }

    public string TraitLabel(string asset) =>
        PilotTraits.Concat(MechTraits).FirstOrDefault(t => string.Equals(t.Asset, asset, StringComparison.OrdinalIgnoreCase))?.Label ?? asset;

    private sealed record PilotsFile(List<PersonaPreset> Personas, List<string> Portraits, List<string> Voices);

    private sealed record TraitsFile(List<TraitDefinition> Pilot, List<TraitDefinition> Mech);
}
