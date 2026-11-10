using MechWarrior5.Mercs.SaveEditor.Core.Catalog;
using MechWarrior5.Mercs.SaveEditor.Core.Game;
using MechWarrior5.Shared.Unreal;
using MechWarrior5.Shared.Unreal.Properties;

namespace MechWarrior5.Mercs.SaveEditor.Core.Tests.Game;

/// <summary>Items and traits read from enabled mods' paks (M5SE-T104).</summary>
[TestFixture]
public class ModCatalogTests
{
    [TestCase("Weapon.Energy.Laser.Pulse.Medium", HardpointClass.Energy)]
    [TestCase("Weapon.Ballistic.AutoCannon.Standard.AC10", HardpointClass.Ballistic)]
    [TestCase("Weapon.Missile.SRM.SRM6", HardpointClass.Missile)]
    [TestCase("Weapon.ArtemisMissile.LRM.LRM10", HardpointClass.Missile)]
    [TestCase("Weapon.Artillery.ArrowIV", HardpointClass.Missile)]
    [TestCase("Weapon.AMS", HardpointClass.Ams)]
    [TestCase("Weapon.Melee.Hatchet", HardpointClass.Melee)]
    [TestCase("Weapon.Plasma.Rifle", HardpointClass.Unknown)]
    public void Weapon_hardpoint_comes_from_its_weapon_type_tag(string tag, HardpointClass expected)
    {
        Assert.That(GameDataScanner.HardpointOf(WeaponProps(tag, equippable: true)), Is.EqualTo(expected));
    }

    [Test]
    public void Mod_weapon_is_named_by_its_asset_and_marked_with_its_mod()
    {
        var item = GameDataScanner.DescribeItem("HeavyPulseLaser_Lvl3", "MWTraceWeaponDataAsset", WeaponProps("Weapon.Energy.Laser.Pulse", equippable: true, name: "Heavy Pulse Laser"), "YAW")!;

        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo(ItemKind.Weapon));
            Assert.That(item.Hardpoint, Is.EqualTo(HardpointClass.Energy));
            Assert.That(item.Label, Is.EqualTo("Heavy Pulse Laser"));
            Assert.That(item.ModName, Is.EqualTo("YAW"));
            Assert.That(item.Tier, Is.EqualTo(3));
        });
    }

    [Test]
    public void Ai_only_mod_weapon_is_not_offered()
    {
        Assert.That(GameDataScanner.DescribeItem("Turret_Laser", "MWTraceWeaponDataAsset", WeaponProps("Weapon.Energy.Laser", equippable: false), "Mod"), Is.Null);
    }

    [Test]
    public void Mod_weapon_with_an_unreadable_hardpoint_stays_in_the_inventory_but_out_of_the_mech_lab()
    {
        var catalog = ItemCatalog.LoadBuiltIn();
        var odd = new ItemDefinition("PlasmaRifle", "MWProjectileWeaponDataAsset", ItemKind.Weapon) { ModName = "Mod" };
        catalog.Apply(Game(items: [odd]));

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Find("PlasmaRifle"), Is.SameAs(odd));
            Assert.That(Enum.GetValues<HardpointClass>().SelectMany(catalog.WeaponsFor), Does.Not.Contain(odd));
        });
    }

    [Test]
    public void Mod_equipment_fits_the_slots_its_asset_lists()
    {
        var props = new PropertyList
        {
            new TextProperty("Name", FText.Invariant("Improved Heat Sink")),
            new ArrayProperty("SupportedSlotTypesAssetId", "StructProperty", SlotList("General_SlotType", "EngineHeatsink_SlotType")),
        };

        var item = GameDataScanner.DescribeItem("ImprovedHeatSink", "MWHeatSinkDataAsset", props, "YAEC")!;

        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo(ItemKind.Equipment));
            Assert.That(EquipmentSlots.Accepts("General_SlotType", item), Is.True);
            Assert.That(EquipmentSlots.Accepts("EngineHeatsink_SlotType", item), Is.True);
            Assert.That(EquipmentSlots.Accepts("ECM_SlotType", item), Is.False);
        });
    }

    [Test]
    public void Equipment_that_lists_no_slots_stays_out_of_every_mech_lab_slot()
    {
        var item = GameDataScanner.DescribeItem("OddArmor", "MWArmorDataAsset", [], "Mod")!;

        Assert.That(new[] { "General_SlotType", "ECM_SlotType", "BAP_SlotType" }.Any(s => EquipmentSlots.Accepts(s, item)), Is.False);
    }

    [Test]
    public void Mod_trait_takes_its_name_and_description_from_the_asset()
    {
        var props = new PropertyList
        {
            new TextProperty("PilotTraitName", FText.Invariant("Iron Will")),
            new TextProperty("PilotTraitLongDescription", FText.Invariant("Recovers {InjuryRecoveryAdjPct}% faster.")),
        };

        var trait = GameDataScanner.DescribeTrait("IronWill_PilotTrait", props, "PilotTrait", "Expanded Pilots");

        Assert.Multiple(() =>
        {
            Assert.That(trait.Label, Is.EqualTo("Iron Will"));
            Assert.That(trait.Description, Is.EqualTo("Recovers X% faster."));
            Assert.That(trait.PickerLabel, Is.EqualTo("Iron Will  (Expanded Pilots)"));
        });
    }

    [Test]
    public void Unnamed_or_templated_mod_trait_is_labelled_from_its_asset_id()
    {
        var props = new PropertyList { new TextProperty("PilotTraitName", FText.Invariant("{MechType} Mastery")) };

        Assert.That(GameDataScanner.DescribeTrait("SharpShooter_PilotTrait", props, "PilotTrait", "Mod").Label, Is.EqualTo("Sharp Shooter"));
    }

    [Test]
    public void A_rescan_that_drops_a_mod_drops_its_items_and_traits()
    {
        var items = ItemCatalog.LoadBuiltIn();
        var pilots = PilotCatalog.LoadBuiltIn();
        var (builtInItems, builtInPilot) = (items.Items.Count, pilots.PilotTraits.Count);
        var modItem = new ItemDefinition("HeavyPulseLaser", "MWTraceWeaponDataAsset", ItemKind.Weapon, HardpointClass.Energy) { ModName = "YAW" };
        var modTrait = new TraitDefinition("IronWill_PilotTrait", "Iron Will") { ModName = "Expanded Pilots" };

        items.Apply(Game(items: [modItem]));
        pilots.Apply(Game(pilotTraits: [modTrait]));
        Assert.Multiple(() =>
        {
            Assert.That(items.WeaponsFor(HardpointClass.Energy), Does.Contain(modItem));
            Assert.That(pilots.PilotTraits, Does.Contain(modTrait));
            Assert.That(pilots.TraitLabel("IronWill_PilotTrait"), Is.EqualTo("Iron Will"));
        });

        items.Apply(Game());
        pilots.Apply(Game());
        Assert.Multiple(() =>
        {
            Assert.That(items.Items, Has.Count.EqualTo(builtInItems));
            Assert.That(items.Find("HeavyPulseLaser"), Is.Null);
            Assert.That(pilots.PilotTraits, Has.Count.EqualTo(builtInPilot));
        });
    }

    [Test]
    public void A_mod_copy_of_a_built_in_item_keeps_the_built_in_entry()
    {
        var items = ItemCatalog.LoadBuiltIn();
        var builtIn = items.Find("MediumLaser_Lvl2")!;

        items.Apply(Game(items: [builtIn with { Hardpoint = HardpointClass.Missile, ModName = "Rebalance" }]));

        Assert.That(items.Find("MediumLaser_Lvl2"), Is.SameAs(builtIn));
    }

    [Test]
    public void Skill_cap_maximum_follows_the_scanned_game_and_falls_back_to_ten()
    {
        var mechs = MechCatalog.LoadBuiltIn();
        Assert.That(mechs.MaxSkillCap, Is.EqualTo(Pilot.DefaultMaxSkillCap));

        mechs.Apply(new GameData("game", [], [], [], new HashSet<string>()) { MaxSkillCap = 20 });
        Assert.That(mechs.MaxSkillCap, Is.EqualTo(20));

        mechs.Apply(new GameData("game", [], [], [], new HashSet<string>()));
        Assert.That(mechs.MaxSkillCap, Is.EqualTo(Pilot.DefaultMaxSkillCap));
    }

    private static PropertyList WeaponProps(string tag, bool equippable, string? name = null)
    {
        var props = new PropertyList
        {
            new StructProperty("WeaponType", "GameplayTag", new PropertyList { new NameProperty("TagName", tag) }),
        };
        if (equippable)
        {
            props.Add(new BoolProperty("bMechEquippable", true));
        }

        if (name is not null)
        {
            props.Add(new TextProperty("Name", FText.Invariant(name)));
        }

        return props;
    }

    private static StructArrayStorage SlotList(params string[] slots)
    {
        var storage = new StructArrayStorage("SupportedSlotTypesAssetId", "SlotTypeDataAssetId");
        foreach (var slot in slots)
        {
            storage.Elements.Add(new PropertyList
            {
                new StructProperty("Id", "PrimaryAssetId", new PropertyList { new NameProperty("PrimaryAssetName", slot) }),
            });
        }

        return storage;
    }

    private static GameData Game(
        IReadOnlyList<ItemDefinition>? items = null, IReadOnlyList<TraitDefinition>? pilotTraits = null) =>
        new("game", [], [], [], new HashSet<string>())
        {
            Items = items ?? [],
            PilotTraits = pilotTraits ?? [],
        };
}
