using MechWarrior5.Mercs.SaveEditor.Core.Addons;
using MechWarrior5.Mercs.SaveEditor.Core.Catalog;

namespace MechWarrior5.Mercs.SaveEditor.Core.Tests.Addons;

/// <summary>M5SE-T116/T118: add-on packs are parsed leniently, apply only while their mod is active, and never stick.</summary>
[TestFixture]
public class AddonTests
{
    private const string Yaw = """
        {
          "schema": 1,
          "name": "YAW",
          "game": "mercenaries",
          "source": "https://www.nexusmods.com/mechwarrior5mercenaries/mods/1158",
          "unknownFieldIsIgnored": true,
          "requires": { "modName": "Yet Another Weapon", "modFolder": "YAW", "versions": ["3.0"] },
          "limits": { "maxSkillCap": 30, "minStanding": -150, "notALimit": 5 },
          "pilotTraits": { "add": [ { "asset": "YAW_Trait", "label": "Gunslinger" } ] },
          "items": {
            "add": [ { "asset": "YAW_Laser", "type": "MWBeamWeaponDataAsset", "kind": "weapon", "label": "Mod Laser", "hardpoint": "E" } ],
            "override": [ { "asset": "MediumLaser", "label": "Renamed Laser" } ]
          }
        }
        """;

    [Test]
    public void A_valid_pack_parses_and_unknown_fields_are_ignored()
    {
        var file = AddonPackReader.Parse(Yaw, "yaw.json");

        Assert.Multiple(() =>
        {
            Assert.That(file.Error, Is.Null);
            Assert.That(file.Pack!.Name, Is.EqualTo("YAW"));
            Assert.That(file.Pack.Limits, Is.EquivalentTo(new Dictionary<string, long> { ["maxSkillCap"] = 30, ["minStanding"] = -150 }));
            Assert.That(file.Pack.Requires.Accepts("3.0g"), Is.True, "versions are prefixes");
            Assert.That(file.Pack.Requires.Accepts("2.9"), Is.False);
            Assert.That(file.Pack.Items.Add.Single().Label, Is.EqualTo("Mod Laser"));
        });
    }

    [TestCase("not json", "not valid JSON")]
    [TestCase("""{ "name": "x", "requires": { "modName": "m" } }""", "schema")]
    [TestCase("""{ "schema": 1, "requires": { "modName": "m" } }""", "name")]
    [TestCase("""{ "schema": 1, "name": "x" }""", "requires")]
    [TestCase("""{ "schema": 99, "name": "x", "requires": { "modName": "m" } }""", "newer editor")]
    public void A_bad_pack_is_reported_not_thrown(string json, string reason)
    {
        var file = AddonPackReader.Parse(json, "bad.json");

        Assert.Multiple(() =>
        {
            Assert.That(file.Pack, Is.Null);
            Assert.That(file.Error, Does.Contain(reason));
        });
    }

    [Test]
    public void A_pack_applies_only_while_its_mod_is_installed_enabled_and_a_listed_version()
    {
        var pack = AddonPackReader.Parse(Yaw, "yaw.json");
        var other = AddonPackReader.Parse(Yaw.Replace("\"mercenaries\"", "\"clans\"", StringComparison.Ordinal), "clans.json");

        Assert.Multiple(() =>
        {
            Assert.That(Status(pack, []), Is.EqualTo(AddonStatus.ModNotInstalled));
            Assert.That(Status(pack, [Mod(enabled: false)]), Is.EqualTo(AddonStatus.ModDisabled));
            Assert.That(Status(pack, [Mod(version: "2.0")]), Is.EqualTo(AddonStatus.VersionNotSupported));
            Assert.That(Status(pack, [Mod()]), Is.EqualTo(AddonStatus.Active));
            Assert.That(Status(pack, [Mod()], turnedOff: ["YAW"]), Is.EqualTo(AddonStatus.TurnedOff));
            Assert.That(Status(pack, [Mod()], allOff: true), Is.EqualTo(AddonStatus.AllTurnedOff));
            Assert.That(Status(other, [Mod()]), Is.EqualTo(AddonStatus.NotForThisGame));
            Assert.That(Status(new AddonFile("x.json", null, "broken"), [Mod()]), Is.EqualTo(AddonStatus.Invalid));
        });
    }

    [Test]
    public void A_pack_that_needs_a_missing_pack_stays_inactive()
    {
        var needs = AddonPackReader.Parse(Yaw.Replace("\"name\": \"YAW\"", "\"name\": \"Addon\", \"requiresPacks\": [\"Other\"]", StringComparison.Ordinal), "a.json");
        var state = AddonState.Build([needs], Game([Mod()]), new HashSet<string>(), false);

        Assert.Multiple(() =>
        {
            Assert.That(state.Entries.Single().Status, Is.EqualTo(AddonStatus.NeedsPack));
            Assert.That(state.Active, Is.Empty);
        });
    }

    [Test]
    public void Limits_only_widen_and_name_their_source()
    {
        var state = AddonState.Build([AddonPackReader.Parse(Yaw, "yaw.json")], Game([Mod()]), new HashSet<string>(), false);
        var none = AddonState.Build([AddonPackReader.Parse(Yaw, "yaw.json")], Game([]), new HashSet<string>(), false);

        Assert.Multiple(() =>
        {
            Assert.That(state.Limits.MaxSkillCap, Is.EqualTo(30));
            Assert.That(state.Limits.MinStanding, Is.EqualTo(-150), "a minimum widens downwards");
            Assert.That(state.Limits.MaxStanding, Is.EqualTo(GameLimits.Vanilla.MaxStanding), "absent means vanilla");
            Assert.That(state.LimitSources["maxSkillCap"], Is.EqualTo("YAW"));
            Assert.That(none.Limits, Is.EqualTo(GameLimits.Vanilla), "an inactive pack changes nothing");
            Assert.That(GameLimits.Vanilla.Widen("maxSkillCap", 5), Is.EqualTo(GameLimits.Vanilla), "never narrowed");
        });
    }

    [Test]
    public void An_enabled_mods_skill_curve_raises_the_cap_without_any_pack()
    {
        var state = AddonState.Build([], Game([Mod()]) with { ModSkillCap = (30, "Pilot Overhaul") }, new HashSet<string>(), false);

        Assert.Multiple(() =>
        {
            Assert.That(state.Limits.MaxSkillCap, Is.EqualTo(30));
            Assert.That(state.LimitSources["maxSkillCap"], Is.EqualTo("Pilot Overhaul"));
        });
    }

    [Test]
    public void Pack_items_and_traits_layer_over_the_built_in_ones_and_turning_off_restores_them()
    {
        var items = ItemCatalog.LoadBuiltIn();
        var pilots = PilotCatalog.LoadBuiltIn();
        var before = items.Find("MediumLaser");
        var game = Game([Mod()]);
        var active = AddonState.Build([AddonPackReader.Parse(Yaw, "yaw.json")], game, new HashSet<string>(), false);
        items.Apply(game, active);
        pilots.Apply(game, active);
        var added = items.Find("YAW_Laser");
        var renamed = items.Find("MediumLaser");
        var trait = pilots.FindTrait("YAW_Trait");

        var off = AddonState.Build([AddonPackReader.Parse(Yaw, "yaw.json")], game, new HashSet<string> { "YAW" }, false);
        items.Apply(game, off);
        pilots.Apply(game, off);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.Not.Null);
            Assert.That(added!.Hardpoint, Is.EqualTo(HardpointClass.Energy));
            Assert.That(added.Source, Is.EqualTo("YAW"));
            Assert.That(renamed!.Label, Is.EqualTo("Renamed Laser"), "an override changes only the fields it gives");
            Assert.That(renamed.AssetType, Is.EqualTo(before!.AssetType));
            Assert.That(trait!.Label, Is.EqualTo("Gunslinger"));
            Assert.That(items.Find("YAW_Laser"), Is.Null, "turned off: the pack's item is gone");
            Assert.That(items.Find("MediumLaser"), Is.EqualTo(before), "turned off: the built-in entry is exactly restored");
            Assert.That(pilots.FindTrait("YAW_Trait"), Is.Null);
        });
    }

    [Test]
    public void Values_past_a_limit_are_kept_but_never_pushed_further()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Bounds.Clamp(25, 0, 10, current: 20), Is.EqualTo(20), "an existing 20 isn't raised further");
            Assert.That(Bounds.Clamp(15, 0, 10, current: 20), Is.EqualTo(15), "but may come back down");
            Assert.That(Bounds.Clamp(15, 0, 10, current: 5), Is.EqualTo(10), "a vanilla value is held to the limit");
        });
    }

    [Test]
    public void Every_shipped_pack_parses()
    {
        var folder = Path.Combine(RepoRoot(), "addons");
        var files = AddonPackReader.ReadFolder(folder);

        Assert.Multiple(() =>
        {
            Assert.That(files, Is.Not.Empty);
            Assert.That(files.Where(f => f.Error is not null).Select(f => $"{f.File}: {f.Error}"), Is.Empty);
            Assert.That(files.Select(f => f.Pack!.Source), Has.All.StartWith("https://"), "every pack links its mod's page");
        });
    }

    private static AddonStatus Status(AddonFile file, IReadOnlyList<InstalledMod> mods, string[]? turnedOff = null, bool allOff = false) =>
        AddonState.Build([file], Game(mods), (turnedOff ?? []).ToHashSet(), allOff).Entries.Single().Status;

    private static InstalledMod Mod(bool enabled = true, string version = "3.0g") =>
        new("YAW", @"C:\Mods\YAW", "Yet Another Weapon", version, enabled, 1, ["yaw.pak"]);

    private static GameData Game(IReadOnlyList<InstalledMod> mods) =>
        new(@"C:\Game", [], [], [], new HashSet<string>()) { InstalledMods = mods };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MechWarrior5.SaveEditor.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
