namespace MechWarrior5.Mercs.SaveEditor.Core.Catalog;

/// <summary>One level of a Cantina career path: the points it needs and the 'Mech trait it rewards.</summary>
public sealed record CareerLevel(int Points, string? TraitReward);

/// <summary>A Cantina career path (<c>MWCareerPathAsset</c>), read from the game or a mod.</summary>
public sealed record CareerPathDefinition(string Asset, string Name, IReadOnlyList<CareerLevel> Levels)
{
    /// <summary>The mod that adds or replaces this path; null for the base game.</summary>
    public string? Source { get; init; }

    public int MaxPoints => Levels.Count == 0 ? 0 : Levels.Max(l => l.Points);

    /// <summary>How many levels <paramref name="points"/> has reached (0 when none).</summary>
    public int LevelFor(int points) => Levels.Count(l => points >= l.Points);
}
