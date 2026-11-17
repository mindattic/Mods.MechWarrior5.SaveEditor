namespace MechWarrior5.Clans.SaveEditor.Core.Catalog;

/// <summary>The base game's pilot skills (<c>Campaign/_common/Pilots/Skills/*_Skill</c>, read from the game's file index 2026-11-17).</summary>
public static class PilotSkills
{
    public static readonly IReadOnlyList<string> All =
    [
        "ActiveProbeAndECMEffects_Skill", "ActiveProbeAndECMRange_Skill", "ArtillerySpecialist_Skill", "Backstabber_Skill",
        "BallisticCooldown_Skill", "BallisticRangeAndVelocity_Skill", "Berserker_Skill", "Bullwark_Skill", "DuckAndDodge_Skill",
        "EagleEyes_Skill", "ElectronicWarfare_Skill", "EnergyCooldown_Skill", "EnergyRange_Skill", "Evasion_Skill", "Flanker_Skill",
        "Handling_Skill", "HeatManagement_Skill", "HeavyArmsExpert_Skill", "JumpJetRecharge_Skill", "KnifeFighter_Skill",
        "LaserDuration_Skill", "LockOnTime_Skill", "MechXPGain_Skill", "Melee_Skill", "MissileCooldown_Skill", "MissileRange_Skill",
        "MissileSpread_Skill", "PilotXPGain_Skill", "ReverseSpeed_Skill", "RunAndGun_Skill", "Scrapper_Skill", "SensorRange_Skill",
        "Sentry_Skill", "Sniper_Skill", "Sprinter_Skill", "Stonewall_Skill", "ThrottleControl_Skill",
    ];

    /// <summary><c>HeatManagement_Skill</c> becomes "Heat Management".</summary>
    public static string Label(string asset)
    {
        var name = asset.EndsWith("_Skill", StringComparison.Ordinal) ? asset[..^"_Skill".Length] : asset;
        return name switch
        {
            "Bullwark" => "Bulwark",
            "ActiveProbeAndECMEffects" => "Active Probe & ECM Effects",
            "ActiveProbeAndECMRange" => "Active Probe & ECM Range",
            "BallisticRangeAndVelocity" => "Ballistic Range & Velocity",
            "DuckAndDodge" => "Duck & Dodge",
            "RunAndGun" => "Run & Gun",
            _ => ClansCatalog.ItemLabel(name),
        };
    }
}
