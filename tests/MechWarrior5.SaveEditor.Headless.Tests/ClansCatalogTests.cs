using MechWarrior5.Clans.SaveEditor.Core.Catalog;

namespace MechWarrior5.SaveEditor.Headless.Tests;

/// <summary>
/// Every Clan 'Mech variant the installed game sells (ItemCollection.Origin.Clan with a market fragment, read from
/// its IoStore containers by tools/clans_utoc_files.py and the M5SE-T46 classifier on 2026-11-10: 30 chassis, 54
/// variants including DLC01/DLC02) resolves to a known chassis, never the "Unknown" placeholder.
/// </summary>
[TestFixture]
public class ClansCatalogTests
{
    [TestCase("ADR_MIA")]
    [TestCase("ACH_MIA")]
    [TestCase("BANE-1_MIA")]
    [TestCase("BANE-2_MIA")]
    [TestCase("BANE-3_MIA")]
    [TestCase("BANE-L_MIA")]
    [TestCase("DWF_MIA")]
    [TestCase("EBJ_MIA")]
    [TestCase("EXE_MIA")]
    [TestCase("FMT_MIA")]
    [TestCase("GAR_MIA")]
    [TestCase("HBK-IIC_MIA")]
    [TestCase("HBK-IIC-A_MIA")]
    [TestCase("HBK-IIC-B_MIA")]
    [TestCase("HBK-IIC-C_MIA")]
    [TestCase("HBK-IIC-DW_MIA")]
    [TestCase("HBR_MIA")]
    [TestCase("HMN_MIA")]
    [TestCase("IFR_MIA")]
    [TestCase("INC-1_MIA")]
    [TestCase("INC-2_MIA")]
    [TestCase("INC-3_MIA")]
    [TestCase("INC-7_MIA")]
    [TestCase("JR7IIC_MIA")]
    [TestCase("JR7IIC2_MIA")]
    [TestCase("JR7IIC3_MIA")]
    [TestCase("JR7IICA_MIA")]
    [TestCase("JR7IICFY_MIA")]
    [TestCase("KFX_MIA")]
    [TestCase("KDK-1_MIA")]
    [TestCase("KDK-5_MIA")]
    [TestCase("KDK-A_MIA")]
    [TestCase("KDK-SB_MIA")]
    [TestCase("LBK_MIA")]
    [TestCase("MDD_MIA")]
    [TestCase("MLX_MIA")]
    [TestCase("NGA_MIA")]
    [TestCase("NTG_MIA")]
    [TestCase("NVA_MIA")]
    [TestCase("RFLIIC_MIA")]
    [TestCase("RFLIIC2_MIA")]
    [TestCase("RFLIICA_MIA")]
    [TestCase("RFLIICB_MIA")]
    [TestCase("SHC_MIA")]
    [TestCase("SHD-IIC_MIA")]
    [TestCase("SHD-IIC-2_MIA")]
    [TestCase("SHD-IIC-A_MIA")]
    [TestCase("SHD-IIC-B_MIA")]
    [TestCase("SHD-IIC-C_MIA")]
    [TestCase("SCR_MIA")]
    [TestCase("SMN_MIA")]
    [TestCase("TBR_MIA")]
    [TestCase("VPR_MIA")]
    [TestCase("WHK_MIA")]
    public void Every_clan_variant_the_game_sells_has_a_known_chassis(string asset)
    {
        var chassis = ClansCatalog.ChassisOf(asset);
        Assert.Multiple(() =>
        {
            Assert.That(chassis.WeightClass, Is.Not.EqualTo("Unknown"), asset);
            Assert.That(chassis.Tons, Is.InRange(20, 100), asset);
            Assert.That(chassis.Name, Is.Not.EqualTo(ClansCatalog.VariantCode(asset)), asset);
        });
    }
}
