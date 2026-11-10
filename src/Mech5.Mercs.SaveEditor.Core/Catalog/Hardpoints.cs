using System.Text.RegularExpressions;

namespace Mech5.Mercs.SaveEditor.Core.Catalog;

public enum HardpointClass
{
    Unknown,
    Energy,
    Ballistic,
    Missile,
    Ams,
    Omni,
    Melee,
}

/// <summary>
/// Hardpoint slot ids encode their class, e.g. <c>Torso_Right_MH1_lrm10</c> is a missile hardpoint
/// and <c>Arm_Left_Battle_Fist_Actuator_Melee</c> a melee mount.
/// </summary>
public static partial class Hardpoints
{
    public static HardpointClass ClassOf(string slotId)
    {
        if (slotId.EndsWith("_Melee", StringComparison.OrdinalIgnoreCase))
        {
            return HardpointClass.Melee;
        }

        var match = SlotToken().Match(slotId + "_");
        return match.Success
            ? match.Groups[1].Value.ToUpperInvariant() switch
            {
                "E" => HardpointClass.Energy,
                "B" => HardpointClass.Ballistic,
                "M" => HardpointClass.Missile,
                "A" => HardpointClass.Ams,
                "O" => HardpointClass.Omni,
                _ => HardpointClass.Unknown,
            }
            : HardpointClass.Unknown;
    }

    public static HardpointClass Parse(string? code) => code switch
    {
        "E" => HardpointClass.Energy,
        "B" => HardpointClass.Ballistic,
        "M" => HardpointClass.Missile,
        "A" => HardpointClass.Ams,
        "Melee" => HardpointClass.Melee,
        _ => HardpointClass.Unknown,
    };

    /// <summary>Omni hardpoints take any energy, ballistic or missile weapon.</summary>
    public static bool Accepts(HardpointClass slot, HardpointClass weapon) => slot switch
    {
        HardpointClass.Omni => weapon is HardpointClass.Energy or HardpointClass.Ballistic or HardpointClass.Missile,
        HardpointClass.Unknown => weapon != HardpointClass.Melee,
        _ => slot == weapon,
    };

    /// <summary>A readable location, e.g. <c>Torso_Right_MH1_lrm10</c> → <c>Right Torso</c>.</summary>
    public static string Location(string slotId)
    {
        var parts = slotId.Split('_');
        if (parts.Length < 2)
        {
            return slotId;
        }

        var area = parts[0] switch
        {
            "Forearm" or "Arm" or "UpperArm" or "Hand" => "Arm",
            "Torso" => "Torso",
            "Head" => "Head",
            "Leg" => "Leg",
            _ => parts[0],
        };
        var side = parts[1] is "Left" or "Right" or "Center" ? parts[1] + " " : string.Empty;
        return side + area;
    }

    [GeneratedRegex(@"_([EBMAO])H\d*_", RegexOptions.IgnoreCase)]
    private static partial Regex SlotToken();
}
