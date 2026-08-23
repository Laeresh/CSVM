using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ARMOR screen (PLAN-hangar C23): the four zones named through their own langui formats
/// (1191-1194, which carry the number themselves), the stepper walking each zone's units 0-12
/// with wraparound into the scratch def, and the detail line keeping the three factors distinct:
/// units bought, cost and weight at x4, the original's x5 lb display figure.
/// </summary>
public class HangarArmourPageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarArmourPageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-armour-" + Guid.NewGuid().ToString("N"));
        _store = new CustomPlaneStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Four rows, each its zone's own langui format with the units filled in.</summary>
    [Fact]
    public void OffersTheFourZones_NamedFromLangui()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":1191,\"text\":\"Nose: %1!d! units\",\"dll\":\"langui\"}," +
            "{\"id\":1194,\"text\":\"Right Wing: %1!d! units\",\"dll\":\"langui\"}]");
        var flow = OpenOnArmour(strings);
        flow.Scratch.ArmourNose = 3;

        Assert.Equal(4, flow.Page.RowCount);
        Assert.Equal("Nose: 3 units", flow.Page.RowText(0));
        Assert.Equal("Right Wing: 0 units", flow.Page.RowText(3));
    }

    /// <summary>A missing string table falls back to the same shape in plain text.</summary>
    [Fact]
    public void RowTextFallsBackWithoutStrings()
    {
        var flow = OpenOnArmour(UiStrings.Empty);
        flow.Scratch.ArmourTail = 7;

        Assert.Equal("Tail: 7 units", flow.Page.RowText(1));
        Assert.Equal("Left Wing: 0 units", flow.Page.RowText(2));
    }

    /// <summary>Stepping a row edits that zone alone, in the 1191-1194 order the record stores
    /// them: nose, tail, left wing, right wing.</summary>
    [Fact]
    public void EachRowEditsItsOwnZone()
    {
        var flow = OpenOnArmour(UiStrings.Empty);
        for (int row = 0; row < 4; row++)
        {
            Assert.True(flow.Page.Step(row, 1));
        }

        Assert.Equal(1, flow.Scratch.ArmourNose);
        Assert.Equal(1, flow.Scratch.ArmourTail);
        Assert.Equal(1, flow.Scratch.ArmourLeftWing);
        Assert.Equal(1, flow.Scratch.ArmourRightWing);
    }

    /// <summary>The stepper walks the 13-unit roster as a cycle: below zero lands on 12, past
    /// 12 lands on zero.</summary>
    [Fact]
    public void SteppingWrapsAtBothEnds()
    {
        var flow = OpenOnArmour(UiStrings.Empty);

        Assert.True(flow.Step(-1));
        Assert.Equal(CustomPlaneDef.MaxArmourUnits, flow.Scratch.ArmourNose);
        Assert.True(flow.Step(1));
        Assert.Equal(0, flow.Scratch.ArmourNose);
    }

    /// <summary>The detail keeps the three factors distinct for 3 units: the units themselves,
    /// $12 cost and 12 lbs. weight (x4, the priced pair), and the 15 lb display figure (x5,
    /// never priced).</summary>
    [Fact]
    public void DetailSeparatesTheThreeFactors()
    {
        var flow = OpenOnArmour(UiStrings.Empty);
        flow.Scratch.ArmourNose = 3;

        string detail = flow.Page.Detail(0);
        Assert.Contains("3 units", detail, StringComparison.Ordinal);
        Assert.Contains("$12", detail, StringComparison.Ordinal);
        Assert.Contains("12 lbs.", detail, StringComparison.Ordinal);
        Assert.Contains("15 lb shown", detail, StringComparison.Ordinal);
    }

    /// <summary>The units figure in the detail renders through format 1170 when present, the
    /// string the original's dropdown labels used.</summary>
    [Fact]
    public void DetailUnitsUseFormat1170()
    {
        var strings = UiStrings.Parse("[{\"id\":1170,\"text\":\"%1!d! units\",\"dll\":\"langui\"}]");
        var flow = OpenOnArmour(strings);
        flow.Scratch.ArmourRightWing = 12;

        string detail = flow.Page.Detail(3);
        Assert.StartsWith("12 units", detail, StringComparison.Ordinal);
        Assert.Contains("$48", detail, StringComparison.Ordinal);
        Assert.Contains("48 lbs.", detail, StringComparison.Ordinal);
        Assert.Contains("60 lb shown", detail, StringComparison.Ordinal);
    }

    /// <summary>Confirm advances to the Guns screen without touching the zones.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnArmour(UiStrings.Empty);
        flow.Scratch.ArmourNose = 5;
        flow.Scratch.ArmourRightWing = 9;
        flow.Accept();

        Assert.Equal(HangarScreen.Guns, flow.Screen);
        Assert.Equal(5, flow.Scratch.ArmourNose);
        Assert.Equal(9, flow.Scratch.ArmourRightWing);
    }

    // A flow standing on the ARMOR screen with a fresh scratch plane.
    private HangarFlow OpenOnArmour(UiStrings strings)
    {
        var flow = new HangarFlow(_store, strings);
        flow.Accept(); // New Plane, on to Airframe
        flow.AnswerDefaultsAsk(false); // decline the airframe-defaults ask (E41)
        flow.Accept(); // on to Engine
        flow.Accept(); // on to Armour
        Assert.Equal(HangarScreen.Armour, flow.Screen);
        return flow;
    }
}
