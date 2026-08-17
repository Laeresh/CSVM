using System.IO;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stock-loadout config (<c>docs/formats/loadouts.md</c>) — committed engine data under
/// <c>CSVM/data/</c>, so these run without an extraction — plus the caliber+ammo weapon-id rule.
/// Binding a loadout to a plane needs live <c>Node3D</c>s and stays with the in-engine suites.
/// </summary>
public class LoadoutTests
{
    private static string ConfigPath =>
        Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json");

    [Theory]
    [InlineData(30, "slug", "wep_30")]
    [InlineData(30, "dumdum", "wep_31")]
    [InlineData(30, "ap", "wep_32")]
    [InlineData(30, "magnesium", "wep_33")]
    [InlineData(70, "slug", "wep_70")]
    public void AGunsWeaponIdIsItsCaliberPlusTheAmmoOffset(int caliber, string ammo, string expected)
    {
        // Each caliber's four ammo types sit at four consecutive wep ids.
        Assert.Equal(expected, StockLoadouts.GunWeaponId(caliber, ammo));
    }

    [Fact]
    public void AnUnknownAmmoNameFallsBackToTheStockSlugRatherThanShiftingCaliber()
    {
        Assert.Equal("wep_50", StockLoadouts.GunWeaponId(50, "not_an_ammo_type"));
        Assert.Equal("wep_50", StockLoadouts.GunWeaponId(50, "SLUG"));
    }

    [Fact]
    public void TheCommittedConfigCarriesTheElevenPlayerAirframes()
    {
        var loadouts = Load();
        Assert.Equal(11, loadouts.All.Count);
        Assert.NotNull(loadouts.For("pbloodhawk"));
        Assert.Null(loadouts.For("not_a_plane"));
    }

    [Fact]
    public void EveryLoadoutNamesAModelAndAtLeastOneGunGroup()
    {
        foreach (var (def, loadout) in Load().All)
        {
            Assert.Equal(def, loadout.Def);
            Assert.NotEqual("", loadout.Model);
            Assert.NotEqual("", loadout.Display);
            Assert.NotEmpty(loadout.Guns);
        }
    }

    [Fact]
    public void EveryGunSlotHasACaliberAndTheMarkersItFiresFrom()
    {
        foreach (var (def, loadout) in Load().All)
        {
            foreach (var gun in loadout.Guns)
            {
                Assert.InRange(gun.Slot, 1, 4);           // the W1-W4 mount slots
                Assert.NotEqual("", gun.Mount);
                Assert.True(gun.Caliber > 0, $"{def} slot {gun.Slot}: no caliber");
                Assert.NotEmpty(gun.Markers);
            }
        }
    }

    [Fact]
    public void PylonsAreDeclaredWithTheirStockOrdnance()
    {
        int withPylons = 0;
        foreach (var (_, loadout) in Load().All)
        {
            if (loadout.Hardpoints is not { } hp)
            {
                continue;
            }
            withPylons++;
            Assert.InRange(hp.Count, 1, 8);              // pylon1..pylonN, max 8 on the rig
            Assert.Equal(hp.Count, hp.Stock.Length);
            Assert.All(hp.Stock, stock => Assert.StartsWith("wep_", stock));
        }
        Assert.Equal(11, withPylons);
    }

    [Fact]
    public void MarkerNamesAreOnesTheRigCanClassify()
    {
        // A loadout naming a marker the model does not carry throws at bind time; the names
        // must at least be shaped like firepoints.
        foreach (var (def, loadout) in Load().All)
        {
            foreach (var gun in loadout.Guns)
            {
                foreach (var marker in gun.Markers)
                {
                    Assert.True(CSVM.Mech3.MarkerRig.Classify(marker, out var kind, out _),
                        $"{def} slot {gun.Slot}: '{marker}' is not a marker name");
                    Assert.Equal(CSVM.Mech3.MarkerRig.MarkerKind.Firepoint, kind);
                }
            }
        }
    }

    [Fact]
    public void PylonFillOrderAlternatesWings()
    {
        // The original fills hardpoints 1,5,2,6,3,7,4,8 — both wings alternately,
        // not sequential 1..N. A partial stock fit (hp.Count < 8) takes this sequence's prefix.
        Assert.Equal(new[] { 1, 5, 2, 6, 3, 7, 4, 8 }, Loadout.PylonFillOrder);
    }

    private static StockLoadouts Load() => StockLoadouts.Load(ConfigPath);
}
