using MechWarrior5.Clans.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>M5SE-T122: the Honor ladder is read from the game's unversioned HonorLevelingAsset by its own shape.</summary>
[TestFixture]
public class HonorLadderTests
{
    [Test]
    public void The_ladder_is_found_among_other_integers_and_checks_itself()
    {
        var bytes = new List<byte>();
        void Int(int v) => bytes.AddRange(BitConverter.GetBytes(v));
        void Pad(params byte[] b) => bytes.AddRange(b);

        // Decoys first: a rising run of flags and offsets that isn't a ladder (increments don't match).
        foreach (var v in new[] { 2, 4, 6, 8, 10, 12, 256, 768 })
        {
            Int(v);
        }

        Int(14);
        var previous = 0;
        var level = 2;
        foreach (var cumulative in ProgressEditor.BuiltInHonorThresholds)
        {
            Pad(0x80, 0x0d, 0x08); // the unversioned-property header bytes between entries
            Int(level++);
            Int(cumulative);
            Int(cumulative - previous);
            previous = cumulative;
        }

        Assert.That(HonorLadder.Parse([.. bytes]), Is.EqualTo(ProgressEditor.BuiltInHonorThresholds));
    }

    [Test]
    public void No_ladder_gives_null()
    {
        Assert.That(HonorLadder.Parse([.. Enumerable.Range(0, 400).Select(i => (byte)i)]), Is.Null);
    }
}
