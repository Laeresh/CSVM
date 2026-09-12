using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The id→name table <c>SurfaceRegistry</c> exists to reproduce: six names compiled into
/// <c>crimson.exe</c> plus eight recovered from <c>ZBD/zrdr.zbd</c>'s soils list
/// (<c>analysis/surface-classification/soils_list.py</c>).
/// Asserted against the full 14-entry table so a future edit cannot quietly renumber a slot.
/// </summary>
public class SurfaceRegistryTests
{
    [Fact]
    public void ReproducesTheFullFourteenEntryTable()
    {
        Assert.Equal(14, SurfaceRegistry.Names.Count);
        Assert.Equal("default", SurfaceRegistry.Names[0]);
        Assert.Equal("water", SurfaceRegistry.Names[1]);
        Assert.Equal("seafloor", SurfaceRegistry.Names[2]);
        Assert.Equal("quicksand", SurfaceRegistry.Names[3]);
        Assert.Equal("lava", SurfaceRegistry.Names[4]);
        Assert.Equal("fire", SurfaceRegistry.Names[5]);
        Assert.Equal("player", SurfaceRegistry.Names[6]);
        Assert.Equal("enemy", SurfaceRegistry.Names[7]);
        Assert.Equal("airstrip", SurfaceRegistry.Names[8]);
        Assert.Equal("opensesame", SurfaceRegistry.Names[9]);
        Assert.Equal("death", SurfaceRegistry.Names[10]);
        Assert.Equal("buildings", SurfaceRegistry.Names[11]);
        Assert.Equal("dzone", SurfaceRegistry.Names[12]);
        Assert.Equal("dirt", SurfaceRegistry.Names[13]);
    }

    [Theory]
    [InlineData(0, "default")]
    [InlineData(1, "water")]
    [InlineData(5, "fire")]
    [InlineData(8, "airstrip")]
    [InlineData(11, "buildings")]
    [InlineData(12, "dzone")]
    [InlineData(13, "dirt")]
    public void EveryIdAShippedMaterialCarriesResolvesToAName(int id, string expected)
    {
        // soil_id_probe.py's census: the only ids any shipped material's soil field ever holds.
        Assert.Equal(expected, SurfaceRegistry.NameForId(id));
    }

    [Theory]
    [InlineData(2, "seafloor")]
    [InlineData(3, "quicksand")]
    [InlineData(4, "lava")]
    [InlineData(6, "player")]
    [InlineData(7, "enemy")]
    [InlineData(9, "opensesame")]
    [InlineData(10, "death")]
    public void UnusedSlotsStillResolveRatherThanBeingTidiedAway(int id, string expected)
    {
        // These ids carry no material in this install but still occupy a real registry slot;
        // dropping one would renumber everything below it.
        Assert.Equal(expected, SurfaceRegistry.NameForId(id));
    }

    [Fact]
    public void NoIdAboveThirteenIsEverProduced()
    {
        Assert.Null(SurfaceRegistry.NameForId(14));
        Assert.Null(SurfaceRegistry.NameForId(100));
    }

    [Fact]
    public void ANegativeIdResolvesToNull()
    {
        // FUN_0048b920's JL test at 0x0048bab5, the id is signed, and a negative id is one of
        // the cascade's fallback-to-slot-0 arms, not a valid lookup.
        Assert.Null(SurfaceRegistry.NameForId(-1));
    }
}
