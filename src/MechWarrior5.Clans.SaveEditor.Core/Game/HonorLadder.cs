using System.Buffers.Binary;

namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>
/// Reads the Honor experience ladder from the game's <c>HonorLevelingAsset</c> package. Clans cooks unversioned
/// properties (no field names), so the ladder is found by its own shape: one entry per level from 2 up, each three
/// int32s (level, cumulative experience, experience since the previous level), where every entry's increment is exactly
/// the step from the previous cumulative value. That self-check keeps flags and offsets from being mistaken for it.
/// </summary>
public static class HonorLadder
{
    /// <summary>The cumulative thresholds for levels 2, 3, ...; null when the package holds no such ladder.</summary>
    public static IReadOnlyList<int>? Parse(byte[] package)
    {
        List<int>? best = null;
        for (var at = 0; at + 12 <= package.Length; at++)
        {
            if (Int(package, at) != 2 || Int(package, at + 4) is var first && (first <= 0 || Int(package, at + 8) != first))
            {
                continue;
            }

            var ladder = new List<int> { first };
            var position = at + 12;
            while (Next(package, position, ladder.Count + 2, ladder[^1]) is { } found)
            {
                ladder.Add(found.Cumulative);
                position = found.End;
            }

            if (ladder.Count >= 5 && (best is null || ladder.Count > best.Count))
            {
                best = ladder;
            }
        }

        return best;
    }

    /// <summary>The next entry (level, cumulative, increment) within a short gap after <paramref name="from"/>.</summary>
    private static (int Cumulative, int End)? Next(byte[] d, int from, int level, int previous)
    {
        for (var at = from; at + 12 <= d.Length && at <= from + 48; at++)
        {
            var cumulative = Int(d, at + 4);
            if (Int(d, at) == level && cumulative > previous && Int(d, at + 8) == cumulative - previous)
            {
                return (cumulative, at + 12);
            }
        }

        return null;
    }

    private static int Int(byte[] d, int at) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at, 4));
}
