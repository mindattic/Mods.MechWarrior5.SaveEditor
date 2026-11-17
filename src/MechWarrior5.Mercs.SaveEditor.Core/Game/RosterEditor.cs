using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Saves;

namespace MechWarrior5.Mercs.SaveEditor.Core.Game;

/// <summary>The company's MechWarriors (<c>RosterModel.PilotRoster</c>): hire, release, reorder, edit.</summary>
public sealed class RosterEditor
{
    private readonly Mw5SaveFile _file;
    private readonly StructArrayStorage? _roster;
    private readonly Action _changed;
    private readonly List<Pilot> _pilots = [];

    internal RosterEditor(Mw5SaveFile file, Action changed)
    {
        _file = file;
        _changed = changed;
        _roster = file.GetModel("RosterModel").Find<ArrayProperty>("PilotRoster")?.Storage as StructArrayStorage;
        Reload();
    }

    public IReadOnlyList<Pilot> Pilots => _pilots;

    public bool CanHire => _roster is not null && _pilots.Count > 0;

    /// <summary>
    /// Hires a new MechWarrior modelled on <paramref name="template"/> (skills, salary, portrait, voice),
    /// with a fresh identity, full health, no traits and no lance assignment.
    /// </summary>
    public Pilot Hire(Pilot template, string fullName, string callsign)
    {
        if (_roster is null)
        {
            throw new InvalidOperationException("This save has no pilot roster.");
        }

        var element = template.Element.DeepClone();
        var persona = element.GetStruct("PersonaData");
        persona.Set(Ue.ItemId("PersonaId", Guid.NewGuid()));
        persona.Set(new TextProperty("FullName", FText.Invariant(fullName)));
        persona.Set(new TextProperty("Callsign", FText.Invariant(callsign)));
        foreach (var bio in new[] { "Bio", "Bio1", "Bio2" })
        {
            if (persona.Find<TextProperty>(bio) is not null)
            {
                persona.Set(new TextProperty(bio, FText.Invariant(null)));
            }
        }

        element.Remove("LockedPlayerSlot");
        element.Set(Ue.Enum("HealthState", "EPilotHealthState", "Healthy"));
        if (element.Find<IntProperty>("HiringCostCBills") is { } cost)
        {
            cost.Value = 0;
        }

        if (element.Find<ArrayProperty>("PilotTraits")?.Storage is StructArrayStorage traits)
        {
            traits.Elements.Clear();
        }

        _roster.Elements.Add(element);
        Reload();
        _changed();
        return _pilots[^1];
    }

    /// <summary>
    /// Hires as <see cref="Hire(Pilot, string, string)"/>, then shapes the recruit's skills: a rookie starts with no
    /// experience; a random recruit gets each skill's experience and cap at random within what the current roster has
    /// (caps up to the active limit).
    /// </summary>
    public Pilot Hire(Pilot template, string fullName, string callsign, HireStyle style, Random? random = null)
    {
        var ranges = Pilot.SkillNames.ToDictionary(
            n => n,
            n => _pilots.Select(p => p.Skills.FirstOrDefault(s => s.Name == n)).OfType<PilotSkill>().ToList());
        var hired = Hire(template, fullName, callsign);
        random ??= Random.Shared;
        foreach (var skill in hired.Skills)
        {
            switch (style)
            {
                case HireStyle.Rookie:
                    skill.Experience = 0;
                    break;
                case HireStyle.Random:
                    var others = ranges[skill.Name];
                    skill.Experience = random.Next(0, Math.Max(1, others.Count == 0 ? 1 : others.Max(s => s.Experience)) + 1);
                    if (skill.HasCap)
                    {
                        var low = others.Count == 0 ? 1 : Math.Max(1, others.Min(s => s.Cap));
                        skill.Cap = random.Next(Math.Min(low, GameRules.Limits.MaxSkillCap), GameRules.Limits.MaxSkillCap + 1);
                    }

                    break;
            }
        }

        if (style == HireStyle.Random)
        {
            // A parent skill's experience is its children's total in game saves (Gunnery = Ballistics + Energy + Missile).
            SetParent("Gunnery", "Ballistics", "Energy", "Missile");
            SetParent("Piloting", "Evasiveness", "Shielding", "HeatManagement");
        }

        return hired;

        void SetParent(string parent, params string[] children)
        {
            if (hired.Skills.FirstOrDefault(s => s.Name == parent) is { } p)
            {
                p.Experience = hired.Skills.Where(s => children.Contains(s.Name)).Sum(s => s.Experience);
            }
        }
    }

    /// <summary>Adds a roster entry from another campaign under a fresh identity; never as the commander.</summary>
    public Pilot Import(PropertyList element)
    {
        if (_roster is null)
        {
            throw new InvalidOperationException("This save has no pilot roster.");
        }

        element.GetStruct("PersonaData").Set(Ue.ItemId("PersonaId", Guid.NewGuid()));
        element.Remove("LockedPlayerSlot");
        // An injury's recovery is a timeline event in the source campaign, which doesn't come along:
        // an imported pilot arrives healthy, as a hired one does, instead of injured with nothing to heal them.
        if (element.Find<EnumProperty>("HealthState") is not null)
        {
            element.Set(Ue.Enum("HealthState", "EPilotHealthState", "Healthy"));
        }

        _roster.Elements.Add(element);
        Reload();
        _changed();
        return _pilots[^1];
    }

    /// <summary>
    /// Releases MechWarriors. The commander and anyone a lance or a pending timeline event (such as an
    /// injury recovery) still refers to are kept: removing them would leave the game a dangling reference.
    /// </summary>
    public BatchResult<Pilot> Release(IEnumerable<Pilot> pilots)
    {
        var result = new BatchResult<Pilot>();
        var lances = Serialized("OperationsModel");
        var timeline = Serialized("TimelineModel");
        foreach (var pilot in pilots.ToList())
        {
            var id = pilot.PersonaId.ToByteArray();
            // An empty id is 16 zero bytes, which the serialized models are full of: it identifies no reference.
            var traceable = pilot.PersonaId != Guid.Empty;
            if (pilot.IsPlayer)
            {
                result.Skip(pilot, "the commander can't be released");
            }
            else if (traceable && lances.AsSpan().IndexOf(id) >= 0)
            {
                result.Skip(pilot, "still assigned to a lance; unassign them in game first");
            }
            else if (traceable && timeline.AsSpan().IndexOf(id) >= 0)
            {
                result.Skip(pilot, "has a pending timeline event (e.g. recovering from injury)");
            }
            else if (_roster?.Elements.Remove(pilot.Element) == true)
            {
                result.Succeed(pilot);
            }
        }

        if (result.Succeeded.Count > 0)
        {
            Reload();
            _changed();
        }

        return result;
    }

    /// <summary>Moves a pilot up (-1) or down (+1) among the non-commander pilots.</summary>
    public bool Move(Pilot pilot, int delta)
    {
        if (_roster is null || pilot.IsPlayer)
        {
            return false;
        }

        var index = _roster.Elements.IndexOf(pilot.Element);
        var target = index + Math.Sign(delta);
        while (target >= 0 && target < _roster.Elements.Count && _pilots[target].IsPlayer)
        {
            target += Math.Sign(delta);
        }

        if (index < 0 || target < 0 || target >= _roster.Elements.Count)
        {
            return false;
        }

        (_roster.Elements[index], _roster.Elements[target]) = (_roster.Elements[target], _roster.Elements[index]);
        Reload();
        _changed();
        return true;
    }

    private byte[] Serialized(string model) =>
        _file.TryGetModel(model) is { } list ? PropertySerializer.WriteList(list) : [];

    private void Reload()
    {
        _pilots.Clear();
        if (_roster is not null)
        {
            _pilots.AddRange(_roster.PropertyElements.Select(p => new Pilot(p, _changed)));
        }
    }
}

/// <summary>How a hired recruit's skills start: a copy of the template pilot's, none, or random within the roster's range.</summary>
public enum HireStyle
{
    CopySelected,
    Rookie,
    Random,
}

public sealed class Pilot
{
    /// <summary>Gunnery and Piloting are parent skills; their six children carry the caps.</summary>
    public static readonly IReadOnlyList<string> SkillNames =
        ["Gunnery", "Ballistics", "Energy", "Missile", "Piloting", "Evasiveness", "Shielding", "HeatManagement"];

    private readonly Action _changed;

    internal Pilot(PropertyList element, Action changed)
    {
        Element = element;
        _changed = changed;
        var slot = element.Find<EnumProperty>("LockedPlayerSlot");
        IsPlayer = slot is not null && Ue.EnumValue(slot.Value) != "None";

        var skills = element.Find<StructProperty>("Skills")?.Value as PropertyList;
        Skills = SkillNames
            .Select(n => skills?.Find<StructProperty>(n)?.Value is PropertyList s ? new PilotSkill(n, s, changed) : null)
            .OfType<PilotSkill>()
            .ToList();
    }

    public Guid PersonaId => Ue.ItemGuid(Persona?.Find<StructProperty>("PersonaId")?.Value as PropertyList) ?? Guid.Empty;

    public string FullName
    {
        get => Text("FullName");
        set => SetText("FullName", value);
    }

    public string Callsign
    {
        get => Text("Callsign");
        set => SetText("Callsign", value);
    }

    public string Bio
    {
        get => Text("Bio");
        set => SetText("Bio", value);
    }

    public string DisplayName => (string.IsNullOrWhiteSpace(FullName), string.IsNullOrWhiteSpace(Callsign)) switch
    {
        (false, false) => $"{FullName} \"{Callsign}\"",
        (true, false) => Callsign,
        (false, true) => FullName,
        _ => "Unnamed pilot",
    };

    public string? Portrait
    {
        get => Asset("PortraitAssetId");
        set => SetAsset("PortraitAssetId", "PortraitAssetId", PilotCatalog.PortraitType, value);
    }

    public string? Voice
    {
        get => Asset("DialogueVoiceAssetId");
        set => SetAsset("DialogueVoiceAssetId", "DialogueVoiceAssetId", PilotCatalog.VoiceType, value);
    }

    public bool IsPlayer { get; }

    public IReadOnlyList<PilotSkill> Skills { get; }

    public IReadOnlyList<string> Traits =>
        TraitStorage?.PropertyElements.Select(e => Ue.AssetName(e)).OfType<string>().ToList() ?? [];

    public string Health
    {
        get
        {
            var health = Element.Find<EnumProperty>("HealthState");
            return health is null ? "Healthy" : Ue.EnumValue(health.Value);
        }
    }

    public bool IsHealthy => Health == "Healthy";

    public int Salary
    {
        get => Element.Find<IntProperty>("SalaryCBills")?.Value ?? 0;
        set => SetInt("SalaryCBills", Bounds.Clamp(value, 0, GameRules.Limits.MaxSalary, Salary));
    }

    internal PropertyList Element { get; }

    private PropertyList? Persona => Element.Find<StructProperty>("PersonaData")?.Value as PropertyList;

    private StructArrayStorage? TraitStorage => Element.Find<ArrayProperty>("PilotTraits")?.Storage as StructArrayStorage;

    public void Heal()
    {
        if (Element.Find<EnumProperty>("HealthState") is { } health && !IsHealthy)
        {
            health.Value = "EPilotHealthState::Healthy";
            _changed();
        }
    }

    /// <summary>
    /// Raises every cap below the active limit to it. Never lowers one: a cap already above the limit (from a
    /// playthrough with a mod that allowed more) stays exactly as it is.
    /// </summary>
    public void MaxCaps()
    {
        var limit = GameRules.Limits.MaxSkillCap;
        foreach (var skill in Skills.Where(s => s.HasCap && s.Cap < limit))
        {
            skill.Cap = limit;
        }
    }

    /// <summary>Applies a persona preset: name, callsign, portrait and voice (blank preset fields are kept).</summary>
    public void ApplyPersona(PersonaPreset preset)
    {
        if (preset.FullName is { Length: > 0 } name)
        {
            FullName = name;
        }

        if (preset.Callsign is { Length: > 0 } callsign)
        {
            Callsign = callsign;
        }

        if (preset.Portrait is { Length: > 0 } portrait)
        {
            Portrait = portrait;
        }

        if (preset.Voice is { Length: > 0 } voice)
        {
            Voice = voice;
        }
    }

    public bool AddTrait(string traitAsset)
    {
        if (Traits.Contains(traitAsset, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Unreal omits an empty PilotTraits array, so a traitless pilot gets one created where the game writes it.
        TraitIds.Add(Element, "PilotTraits", "PilotTraitDataAssetId", traitAsset, insertAfter: "HealthState", assetType: PilotCatalog.PilotTraitType);
        _changed();
        return true;
    }

    public bool RemoveTrait(string traitAsset)
    {
        var element = TraitStorage?.PropertyElements.FirstOrDefault(e =>
            string.Equals(Ue.AssetName(e), traitAsset, StringComparison.OrdinalIgnoreCase));
        if (element is null || TraitStorage!.Elements.Remove(element) is false)
        {
            return false;
        }

        _changed();
        return true;
    }

    private string Text(string field) => Persona?.Find<TextProperty>(field)?.DisplayString ?? string.Empty;

    private void SetText(string field, string value)
    {
        if (Persona is not { } persona || Text(field) == value)
        {
            return;
        }

        if (persona.Find<TextProperty>(field) is { } text)
        {
            text.Data = FText.WithDisplayString(text.Data, value);
        }
        else
        {
            persona.Add(new TextProperty(field, FText.Invariant(value)));
        }

        _changed();
    }

    private string? Asset(string field) =>
        Ue.AssetName(Persona?.Find<StructProperty>(field)?.Value as PropertyList) is { } name && name != "None" ? name : null;

    private void SetAsset(string field, string structType, string assetType, string? value)
    {
        if (Persona is not { } persona || string.IsNullOrEmpty(value) || Asset(field) == value)
        {
            return;
        }

        persona.Set(Ue.AssetId(field, structType, assetType, value));
        _changed();
    }

    private void SetInt(string name, int value)
    {
        var property = Element.Find<IntProperty>(name);
        if (property?.Value == value)
        {
            return;
        }

        if (property is null)
        {
            Element.Add(new IntProperty(name, value));
        }
        else
        {
            property.Value = value;
        }

        _changed();
    }
}

public sealed class PilotSkill
{
    private readonly PropertyList _skill;
    private readonly Action _changed;

    internal PilotSkill(string name, PropertyList skill, Action changed)
    {
        Name = name;
        _skill = skill;
        _changed = changed;
    }

    public string Name { get; }

    /// <summary>
    /// The highest value this cap can be set to: the active limit, or the current value when that is already higher
    /// (kept as it is, from a mod playthrough). A slider uses it as its maximum so showing a cap never changes it.
    /// </summary>
    public int CapCeiling => Math.Min(Math.Max(GameRules.Limits.MaxSkillCap, Cap), byte.MaxValue);

    /// <summary>Parent skills (Gunnery, Piloting) have no cap of their own.</summary>
    public bool HasCap => Name is not ("Gunnery" or "Piloting");

    public int Cap
    {
        get => _skill.Find<ByteProperty>("SkillCap")?.RawValue ?? 0;
        set
        {
            var clamped = (byte)Bounds.Clamp(value, 0, CapCeiling, Cap);
            var property = _skill.Find<ByteProperty>("SkillCap");
            if (property?.RawValue == clamped)
            {
                return;
            }

            if (property is null)
            {
                _skill.Insert(0, new ByteProperty("SkillCap") { RawValue = clamped });
            }
            else
            {
                property.RawValue = clamped;
            }

            _changed();
        }
    }

    public int Experience
    {
        get => _skill.Find<IntProperty>("TotalExp")?.Value ?? 0;
        set
        {
            var clamped = Bounds.Clamp(value, 0, GameRules.Limits.MaxExperience, Experience);
            var property = _skill.Find<IntProperty>("TotalExp");
            if (property?.Value == clamped)
            {
                return;
            }

            if (property is null)
            {
                _skill.Add(new IntProperty("TotalExp", clamped));
            }
            else
            {
                property.Value = clamped;
            }

            _changed();
        }
    }
}
