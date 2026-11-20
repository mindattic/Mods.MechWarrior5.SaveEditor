namespace MechWarrior5.Mercs.SaveEditor.Core.Addons;

/// <summary>
/// Every bound the editor holds an edit to. <see cref="Vanilla"/> is the base game's; active add-on packs may widen
/// them. The editors read <see cref="GameRules.Limits"/> at the moment of each edit, so nothing else keeps a copy.
/// </summary>
public sealed record GameLimits(int MaxSkillCap, int MaxExperience, int MaxSalary, long MaxCBills, int MinStanding, int MaxStanding, int MaxCount)
{
    public static GameLimits Vanilla { get; } = new(
        MaxSkillCap: 10,
        MaxExperience: 1_000_000,
        MaxSalary: 10_000_000,
        MaxCBills: 2_000_000_000,
        MinStanding: -100,
        MaxStanding: 100,
        MaxCount: 9999);

    /// <summary>The pack keys, as written in an add-on's <c>limits</c> object.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        ["maxSkillCap", "maxExperience", "maxSalary", "maxCBills", "minStanding", "maxStanding", "maxCount"];

    public long this[string key] => key switch
    {
        "maxSkillCap" => MaxSkillCap,
        "maxExperience" => MaxExperience,
        "maxSalary" => MaxSalary,
        "maxCBills" => MaxCBills,
        "minStanding" => MinStanding,
        "maxStanding" => MaxStanding,
        "maxCount" => MaxCount,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    /// <summary>Minimums widen downwards, everything else upwards.</summary>
    public static bool IsMinimum(string key) => key.StartsWith("min", StringComparison.Ordinal);

    /// <summary>These limits with <paramref name="key"/> widened to <paramref name="value"/> (never narrowed).</summary>
    public GameLimits Widen(string key, long value)
    {
        var current = this[key];
        if (IsMinimum(key) ? value >= current : value <= current)
        {
            return this;
        }

        GameLimits wider = key switch
        {
            "maxSkillCap" => this with { MaxSkillCap = (int)Math.Min(value, byte.MaxValue) },
            "maxExperience" => this with { MaxExperience = (int)Math.Min(value, int.MaxValue) },
            "maxSalary" => this with { MaxSalary = (int)Math.Min(value, int.MaxValue) },
            "maxCBills" => this with { MaxCBills = value },
            "minStanding" => this with { MinStanding = (int)Math.Max(value, int.MinValue) },
            "maxStanding" => this with { MaxStanding = (int)Math.Min(value, int.MaxValue) },
            "maxCount" => this with { MaxCount = (int)Math.Min(value, int.MaxValue) },
            _ => this,
        };

        // A value capped back to the current limit widened nothing: return this, so callers don't credit the source.
        return wider == this ? this : wider;
    }
}

/// <summary>The rules in force: vanilla until the app applies an <see cref="AddonState"/>.</summary>
public static class GameRules
{
    public static GameLimits Limits { get; set; } = GameLimits.Vanilla;
}

/// <summary>
/// Clamping that never forces an existing value: a value already outside the active limits (from a save made with a
/// mod that allowed more) may stay or move back inside, but an edit can't push it further out.
/// </summary>
public static class Bounds
{
    public static int Clamp(int value, int min, int max, int current) =>
        Math.Clamp(value, Math.Min(min, current), Math.Max(max, current));

    public static long Clamp(long value, long min, long max, long current) =>
        Math.Clamp(value, Math.Min(min, current), Math.Max(max, current));
}
