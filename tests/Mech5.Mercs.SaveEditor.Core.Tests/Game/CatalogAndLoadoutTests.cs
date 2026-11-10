using Mech5.Mercs.SaveEditor.Core.Catalog;
using Mech5.Mercs.SaveEditor.Core.Game;
using Mech5.Shared.Unreal;
using Mech5.Shared.Unreal.Properties;

namespace Mech5.Mercs.SaveEditor.Core.Tests.Game;

[TestFixture]
public class CatalogAndLoadoutTests
{
    private static readonly MechCatalog Catalog = MechCatalog.LoadBuiltIn();

    [Test]
    public void Catalog_covers_all_nine_dlc_packs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Catalog.DlcPacks.Select(p => p.Tag), Is.EqualTo(Enumerable.Range(1, 9).Select(i => $"DLC.DLC{i}")));
            Assert.That(Catalog.DlcPacks[^1].Name, Is.EqualTo("Ashes & Ascension"));
            Assert.That(Catalog.Variants.Count(v => v.Dlc?.Tag == "DLC.DLC9"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void Every_variant_has_a_stock_loadout()
    {
        var missing = Catalog.Variants.Where(v => Catalog.FindStock(v.Asset) is null).Select(v => v.Code).ToList();

        Assert.That(missing, Is.Empty);
    }

    [TestCase("BANE-1", "DLC.DLC9")]
    [TestCase("DWF-PRIME", "DLC.DLC7")]
    [TestCase("AS7-D", null)]
    public void Variants_know_their_dlc(string code, string? tag)
    {
        var variant = Catalog.Variants.Single(v => v.Code == code);

        Assert.That(variant.Dlc?.Tag, Is.EqualTo(tag));
    }

    [Test]
    public void Cold_storage_blob_has_the_game_layout()
    {
        var stock = Catalog.FindStock("GRF-1N_MDA")!;
        var id = Guid.NewGuid();

        var blob = MechLoadoutBuilder.BuildColdStorageBlob("GRF-1N_MDA", "Griffin_Default_SKN", stock, id);
        var reread = PropertySerializer.ReadList(PropertySerializer.WriteList(blob), out _);

        var item = reread.GetStruct("MarketItemMech", "Item");
        var data = item.GetStruct("ItemData");
        Assert.Multiple(() =>
        {
            Assert.That(Ue.EnumValue(reread.Get<EnumProperty>("MechLoadoutType").Value), Is.EqualTo("ColdStorageLoadout"));
            Assert.That(Ue.ItemGuid(item.GetStruct("ItemId")), Is.EqualTo(id));
            Assert.That(Ue.AssetName(data.GetStruct("MechDataAssetId")), Is.EqualTo("GRF-1N_MDA"));
            Assert.That(data.Select(p => p.Name), Is.EqualTo(new[]
            {
                "MechDataAssetId", "SkinCustomization", "CurrentStructure", "CurrentArmor", "CurrentRearArmor",
                "InstalledArmor", "InstalledRearArmor", "InstalledWeapons", "WeaponGroupInfo", "Equipment",
            }));
        });
    }

    [Test]
    public void Melee_actuators_are_grouped_but_not_installed_as_weapons()
    {
        var stock = Catalog.FindStock("GRF-1N_MDA")!;

        var data = MechLoadoutBuilder.BuildLoadout("GRF-1N_MDA", "Griffin_Default_SKN", stock);
        var weapons = data.Get<ArrayProperty>("InstalledWeapons").Structs.Elements;
        var groups = data.GetStruct("WeaponGroupInfo").Get<ArrayProperty>("WeaponGroups").Structs.Elements;

        Assert.Multiple(() =>
        {
            Assert.That(weapons, Has.Count.EqualTo(stock.Weapons.Count(w => w.IsInstalledWeapon)));
            Assert.That(groups, Has.Count.EqualTo(stock.Groups.Count));
        });
    }

    [Test]
    public void Item_catalog_separates_weapons_from_equipment()
    {
        var items = ItemCatalog.LoadBuiltIn();

        Assert.Multiple(() =>
        {
            Assert.That(items.Items.Where(i => i.Kind == ItemKind.Weapon), Is.Not.Empty);
            Assert.That(items.Items.Where(i => i.Kind == ItemKind.Ammo).All(i => !i.IsWeapon), Is.True);
            Assert.That(items.Items.Single(i => i.Asset == "AntiMissileSystem_Lvl2").Tier, Is.EqualTo(2));
        });
    }
}
