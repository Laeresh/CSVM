using System;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The airframe registry: the eleven stock airframes keep the id order that saves and the network
/// roster index. A mod registers under its own id with a stock parent it resolves to. Each test
/// builds its own registry, so no mod leaks into <see cref="AirframeRegistry.Shared"/>.
/// </summary>
public class AirframeRegistryTests
{
    [Fact]
    public void StockAirframesKeepTheirIdOrder()
    {
        string[] expected =
        {
            "player_autogyro", "player_avenger", "player_balmoral", "player_bhawk", "player_brigand",
            "player_pfighter", "player_fbrand", "player_fury", "player_kestrel", "player_peacemaker",
            "player_warhawk",
        };
        Assert.Equal(expected, AirframeRegistry.Stock.Select(a => a.Node));
        Assert.Equal(expected, StockAirframes.Nodes);
        Assert.Equal(expected, new AirframeRegistry().All.Select(a => a.Node));
        Assert.All(AirframeRegistry.Stock, a => Assert.True(a.IsStock));
        Assert.Equal(expected.Length - 1, CustomPlaneDef.MaxAirframe);
    }

    [Fact]
    public void AModRegistersAfterTheStockAirframesAndResolvesToItsParent()
    {
        var registry = new AirframeRegistry();
        var mod = registry.Register("e1c_avenger", "Avenger", "PLAYER_BRIGAND");

        Assert.Equal(new AirframeEntry("e1c_avenger", "Avenger", "player_brigand"), mod);
        Assert.False(mod.IsStock);
        Assert.Same(mod, registry.All[^1]);
        Assert.Equal(AirframeRegistry.Stock.Count + 1, registry.All.Count);
        Assert.Same(mod, registry.Find("E1C_Avenger"));
        Assert.Equal("player_brigand", registry.StockNodeOf("e1c_avenger"));
        Assert.Null(StockAirframes.IdOf("e1c_avenger"));
    }

    [Fact]
    public void StockNodeOfPassesStockAndUnknownNodesThrough()
    {
        var registry = new AirframeRegistry();
        Assert.Equal("player_fury", registry.StockNodeOf("player_fury"));
        Assert.Equal("fury", registry.StockNodeOf("fury"));
        Assert.Null(registry.Find("fury"));
    }

    [Theory]
    [InlineData("player_avenger")]
    [InlineData("Player_Fury")]
    public void AnIdThatIsAStockNodeIsRejected(string id)
    {
        var registry = new AirframeRegistry();
        var e = Assert.Throws<ArgumentException>(() => registry.Register(id, "Clash", "player_brigand"));
        Assert.Contains("stock airframe node", e.Message, StringComparison.Ordinal);
        Assert.Equal(AirframeRegistry.Stock.Count, registry.All.Count);
    }

    [Fact]
    public void ASecondModWithTheSameIdIsRejected()
    {
        var registry = new AirframeRegistry();
        registry.Register("e1c_avenger", "Avenger", "player_brigand");
        var e = Assert.Throws<ArgumentException>(() => registry.Register("E1C_AVENGER", "Again", "player_fury"));
        Assert.Contains("already registered", e.Message, StringComparison.Ordinal);
        Assert.Equal("player_brigand", registry.StockNodeOf("e1c_avenger"));
    }

    [Theory]
    [InlineData("fury")]
    [InlineData("e1c_avenger")]
    public void AParentThatIsNoStockNodeIsRejected(string parent)
    {
        var registry = new AirframeRegistry();
        registry.Register("e1c_avenger", "Avenger", "player_brigand");
        Assert.Throws<ArgumentException>(() => registry.Register("second_mod", "Second", parent));
        Assert.Null(registry.Find("second_mod"));
    }

    [Fact]
    public void ABlankArgumentIsRejected()
    {
        var registry = new AirframeRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register(" ", "Avenger", "player_brigand"));
        Assert.Throws<ArgumentException>(() => registry.Register("e1c_avenger", "", "player_brigand"));
        Assert.Throws<ArgumentException>(() => registry.Register("e1c_avenger", "Avenger", " "));
    }
}
