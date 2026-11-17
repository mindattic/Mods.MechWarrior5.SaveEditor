using MechWarrior5.Clans.SaveEditor.Core.Catalog;
using MechWarrior5.Clans.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>Clans pilot XP and skills, edited on a copy of a real save, written and read back from disk.</summary>
[TestFixture]
[Category("Headless")]
public class ClansPilotTests
{
    private string _save = null!;

    [SetUp]
    public void SetUp() => (_, _save) = SaveFixtures.ClansFolderCopy();

    [TearDown]
    public void TearDown() => SaveFixtures.CleanUp();

    private static ClansPilot Pilot(ClansDocument doc, string name) => doc.Inventory.StarPilots.Single(p => p.Name == name);

    [Test]
    public void Xp_and_skill_ranks_persist()
    {
        var doc = ClansDocument.Load(_save);
        var pilots = doc.Inventory.StarPilots;
        Assume.That(pilots.Any(p => p.Skills.Count > 0), "No pilot in this save owns a skill.");
        var pilot = pilots.First(p => p.Skills.Count > 0);
        var owned = pilot.Skills[0].Asset;
        var added = PilotSkills.All.First(s => pilot.Skills.All(k => k.Asset != s));
        var others = pilots.Where(p => p != pilot).ToDictionary(p => p.Name, p => (p.Experience, Skills: p.Skills.ToList()));

        pilot.Experience = 123_456;
        pilot.SetSkill(owned, ClansPilot.MaxRank);
        pilot.SetSkill(added, 2);
        var expected = pilot.Skills.ToList();
        var path = doc.Save("Pilot skills").Path;

        var back = ClansDocument.Load(path);
        var reread = Pilot(back, pilot.Name);
        Assert.That(reread.Experience, Is.EqualTo(123_456));
        Assert.That(reread.Skills, Is.EqualTo(expected));
        Assert.That(reread.Skills.Single(s => s.Asset == owned).Rank, Is.EqualTo(ClansPilot.MaxRank));
        Assert.That(reread.Skills.Single(s => s.Asset == added).Rank, Is.EqualTo(2));
        foreach (var other in back.Inventory.StarPilots.Where(p => p.Name != pilot.Name))
        {
            Assert.That(other.Experience, Is.EqualTo(others[other.Name].Experience), other.Name);
            Assert.That(other.Skills, Is.EqualTo(others[other.Name].Skills), other.Name);
        }
    }

    [Test]
    public void Removing_every_skill_then_adding_one_rebuilds_the_list()
    {
        var doc = ClansDocument.Load(_save);
        Assume.That(doc.Inventory.StarPilots.Count(p => p.Skills.Count > 0), Is.GreaterThanOrEqualTo(2), "Needs two pilots with skills.");
        var pilot = doc.Inventory.StarPilots.First(p => p.Skills.Count > 0);
        foreach (var skill in pilot.Skills)
        {
            pilot.RemoveSkill(skill.Asset);
        }

        Assert.That(pilot.Skills, Is.Empty);
        var path = doc.Save("No skills").Path;

        var emptied = ClansDocument.Load(path);
        var bare = Pilot(emptied, pilot.Name);
        Assert.That(bare.Skills, Is.Empty);
        Assert.That(bare.CanAddSkills, Is.True);
        bare.SetSkill("Sniper_Skill", 3);
        var rebuilt = Pilot(ClansDocument.Load(emptied.Save("Sniper").Path), pilot.Name);
        Assert.That(rebuilt.Skills, Is.EqualTo(new[] { new ClansPilotSkill("Sniper_Skill", 3) }));
    }

    [Test]
    public void Every_skill_a_real_save_holds_is_in_the_catalog_and_within_the_rank_cap()
    {
        foreach (var path in SaveFixtures.AllClansFiles().Where(p => Path.GetFileName(p).Contains('_')))
        {
            ClansDocument doc;
            try
            {
                doc = ClansDocument.Load(path);
            }
            catch (Exception e) when (e is InvalidOperationException or SaveFormatException)
            {
                continue;
            }

            foreach (var skill in doc.Inventory.StarPilots.SelectMany(p => p.Skills))
            {
                Assert.That(PilotSkills.All, Does.Contain(skill.Asset), path);
                Assert.That(skill.Rank, Is.InRange(1, ClansPilot.MaxRank), path);
            }
        }
    }
}
