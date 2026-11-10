using Mw5SaveEditor.Core.Saves;
using Mw5SaveEditor.Core.Unreal;
using Mw5SaveEditor.Core.Unreal.Properties;

namespace Mw5SaveEditor.Core.Game;

public sealed class RosterEditor
{
    internal RosterEditor(Mw5SaveFile file, Action changed)
    {
        var roster = file.GetModel("RosterModel").Find<ArrayProperty>("PilotRoster")?.Storage as StructArrayStorage;
        Pilots = roster?.PropertyElements.Select(p => new Pilot(p, changed)).ToList() ?? [];
    }

    public IReadOnlyList<Pilot> Pilots { get; }
}

public sealed class Pilot
{
    /// <summary>Gunnery and Piloting are parent skills; their six children carry the caps.</summary>
    public static readonly IReadOnlyList<string> SkillNames =
        ["Gunnery", "Ballistics", "Energy", "Missile", "Piloting", "Evasiveness", "Shielding", "HeatManagement"];

    public const int MaxSkillCap = 10;
    public const int MaxSalary = 10_000_000;

    private readonly PropertyList _element;
    private readonly Action _changed;

    internal Pilot(PropertyList element, Action changed)
    {
        _element = element;
        _changed = changed;
        var persona = element.Find<StructProperty>("PersonaData")?.Value as PropertyList;
        FullName = persona?.Find<TextProperty>("FullName")?.DisplayString ?? string.Empty;
        Callsign = persona?.Find<TextProperty>("Callsign")?.DisplayString ?? string.Empty;
        Portrait = Ue.AssetName(persona?.Find<StructProperty>("PortraitAssetId")?.Value as PropertyList);
        var slot = element.Find<EnumProperty>("LockedPlayerSlot");
        IsPlayer = slot is not null && Ue.EnumValue(slot.Value) != "None";

        var skills = element.Find<StructProperty>("Skills")?.Value as PropertyList;
        Skills = SkillNames
            .Select(n => skills?.Find<StructProperty>(n)?.Value is PropertyList s ? new PilotSkill(n, s, changed) : null)
            .OfType<PilotSkill>()
            .ToList();
    }

    public string FullName { get; }

    public string Callsign { get; }

    public string DisplayName => (string.IsNullOrWhiteSpace(FullName), string.IsNullOrWhiteSpace(Callsign)) switch
    {
        (false, false) => $"{FullName} \"{Callsign}\"",
        (true, false) => Callsign,
        (false, true) => FullName,
        _ => "Unnamed pilot",
    };

    public string? Portrait { get; }

    public bool IsPlayer { get; }

    public IReadOnlyList<PilotSkill> Skills { get; }

    public string Health
    {
        get
        {
            var health = _element.Find<EnumProperty>("HealthState");
            return health is null ? "Healthy" : Ue.EnumValue(health.Value);
        }
    }

    public bool IsHealthy => Health == "Healthy";

    public int Salary
    {
        get => _element.Find<IntProperty>("SalaryCBills")?.Value ?? 0;
        set => SetInt("SalaryCBills", Math.Clamp(value, 0, MaxSalary));
    }

    public void Heal()
    {
        if (_element.Find<EnumProperty>("HealthState") is { } health && !IsHealthy)
        {
            health.Value = "EPilotHealthState::Healthy";
            _changed();
        }
    }

    public void MaxCaps()
    {
        foreach (var skill in Skills.Where(s => s.HasCap))
        {
            skill.Cap = MaxSkillCap;
        }
    }

    private void SetInt(string name, int value)
    {
        var property = _element.Find<IntProperty>(name);
        if (property?.Value == value)
        {
            return;
        }

        if (property is null)
        {
            _element.Add(new IntProperty(name, value));
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
    public const int MaxExperience = 1_000_000;

    private readonly PropertyList _skill;
    private readonly Action _changed;

    internal PilotSkill(string name, PropertyList skill, Action changed)
    {
        Name = name;
        _skill = skill;
        _changed = changed;
    }

    public string Name { get; }

    /// <summary>Parent skills (Gunnery, Piloting) have no cap of their own.</summary>
    public bool HasCap => Name is not ("Gunnery" or "Piloting");

    public int Cap
    {
        get => _skill.Find<ByteProperty>("SkillCap")?.RawValue ?? 0;
        set
        {
            var clamped = (byte)Math.Clamp(value, 0, Pilot.MaxSkillCap);
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
            var clamped = Math.Clamp(value, 0, MaxExperience);
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
