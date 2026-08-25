using System.IO;
using CSVM.Flight;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>The profile's stored picks turned into a flying fit: the ammunition index's four
/// names and its no-gun marker, the ordnance cell's table index and its left/right wing split,
/// and what an unset or table-less pick leaves alone.</summary>
public class CampaignLoadoutTests
{
    private static readonly StockLoadouts Stock =
        StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));

    [Fact]
    public void EachAmmoIndexBindsItsOwnAmmunitionName()
    {
        var plane = new OwnedPlane { Name = "Gypsy Magic", Ammo = new[] { 0, 1, 2, 3 } };

        var fit = CampaignLoadout.For(plane, Stock);

        Assert.Equal("slug", fit.GunAmmoFor(1));
        Assert.Equal("dumdum", fit.GunAmmoFor(2));
        Assert.Equal("ap", fit.GunAmmoFor(3));
        Assert.Equal("magnesium", fit.GunAmmoFor(4));
    }

    [Fact]
    public void TheNoGunMarkerBindsAnEmptyMountRatherThanAnAmmunition()
    {
        var plane = new OwnedPlane { Name = "Gypsy Magic", Ammo = new[] { 4, 0, 0, 0 } };

        var fit = CampaignLoadout.For(plane, Stock);

        Assert.Equal(LoadoutChoice.None, fit.GunAmmoFor(1));
        Assert.Equal("slug", fit.GunAmmoFor(2));
    }

    [Fact]
    public void AnOrdnanceCellBindsItsTableRowsWeaponOnItsOwnWingsPylon()
    {
        // Cell 0 is the left wing's first pylon and cell 4 the right wing's first; the stored
        // value is the table index plus one, so 3 is the table's row 2 and 11 its row 10.
        var ordnance = new int[8];
        ordnance[0] = 3;
        ordnance[4] = 11;
        var plane = new OwnedPlane { Name = "Gypsy Magic", Ordnance = ordnance };

        var fit = CampaignLoadout.For(plane, Stock);

        Assert.Equal(Stock.Options.PylonOrdnance[2].Id, fit.PylonFor(1));
        Assert.Equal(Stock.Options.PylonOrdnance[10].Id, fit.PylonFor(5));
    }

    [Fact]
    public void AnUnsetCellLeavesThePylonAtItsBaseFit()
    {
        var plane = new OwnedPlane { Name = "Gypsy Magic" };

        var fit = CampaignLoadout.For(plane, Stock);

        for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
        {
            Assert.Null(fit.PylonFor(pylon));
        }
    }

    [Fact]
    public void WithNoStockTableThePylonsAreLeftAloneAndTheGunsAreNot()
    {
        var ordnance = new int[8];
        ordnance[0] = 3;
        var plane = new OwnedPlane { Name = "Gypsy Magic", Ammo = new[] { 2, 2, 2, 2 }, Ordnance = ordnance };

        var fit = CampaignLoadout.For(plane, null);

        Assert.Null(fit.PylonFor(1));
        Assert.Equal("ap", fit.GunAmmoFor(1));
    }

    [Fact]
    public void AFreshProfilesStarterBindsTheStockFitUnchanged()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");

        var fit = CampaignLoadout.For(profile.Planes[profile.SelectedPlane], Stock);

        // Every gun slot reads slug, the stock ammunition, and no pylon is picked at all, so
        // laying this over an airframe's own fit changes nothing about it.
        Assert.Equal("slug", fit.GunAmmoFor(1));
        Assert.Null(fit.PylonFor(1));
    }

    [Fact]
    public void AFreshProfileFliesTheWingmanInTheSecondStarter()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");

        Assert.Equal(0, profile.SelectedPlane);
        Assert.Equal(1, profile.WingmanPlane);
        Assert.Equal("Gypsy Magic", profile.Planes[profile.SelectedPlane].Name);
        Assert.Equal("The Knave", profile.Planes[profile.WingmanPlane].Name);
    }
}
