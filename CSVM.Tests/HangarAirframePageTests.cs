using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AIRFRAME screen (PLAN-hangar C22): all 11 airframes offered (Decision 9), the stepper
/// writing the scratch plane's airframe and nothing else, the detail line carrying the stat
/// table's figures and the economy's own star ratings, and the focused airframe's blueprint
/// through the page-art seam.
/// </summary>
public class HangarAirframePageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarAirframePageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-airframe-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>Decision 9: every one of the 11 airframes is a row; the availability threshold
    /// gates nothing. Names come from langui 3000+id.</summary>
    [Fact]
    public void OffersAllElevenAirframes_NamedFromLangui()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":3000,\"text\":\"Ford Hoplite\",\"dll\":\"langui\"}," +
            "{\"id\":3002,\"text\":\"Blackflag Balmoral\",\"dll\":\"langui\"}]");
        var flow = OpenOnAirframe(strings);

        Assert.Equal(HangarEconomy.Airframes.Length, flow.Page.RowCount);
        Assert.Equal(11, flow.Page.RowCount);
        Assert.StartsWith("Ford Hoplite", flow.Page.RowText(0), StringComparison.Ordinal);
        Assert.StartsWith("Blackflag Balmoral", flow.Page.RowText(2), StringComparison.Ordinal);
    }

    /// <summary>The ←→ stepper makes the focused row the pick and ticks it; stepping a row that
    /// already is the pick changes nothing.</summary>
    [Fact]
    public void SteppingSelectsTheFocusedAirframe()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Move(1);
        flow.Move(1);

        Assert.True(flow.Step(1));
        Assert.Equal(2, flow.Scratch.Airframe);
        Assert.EndsWith("✓", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.False(flow.Page.RowText(0).EndsWith("✓", StringComparison.Ordinal));
        Assert.False(flow.Step(1));
    }

    /// <summary>Changing airframe preserves every other pick: guns and hardpoints are count-valid
    /// on every airframe (the wrong-claims disproof), so nothing re-clamps.</summary>
    [Fact]
    public void ChangingAirframe_PreservesTheOtherPicks()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Engine = 3;
        flow.Scratch.Guns[3] = new GunChoice(4, true);
        flow.Scratch.LeftHardpoints = 4;
        flow.Scratch.ArmourTail = 7;

        flow.Move(1);
        Assert.True(flow.Step(1));

        Assert.Equal(1, flow.Scratch.Airframe);
        Assert.Equal(3, flow.Scratch.Engine);
        Assert.Equal(new GunChoice(4, true), flow.Scratch.Guns[3]);
        Assert.Equal(4, flow.Scratch.LeftHardpoints);
        Assert.Equal(7, flow.Scratch.ArmourTail);
    }

    /// <summary>Confirm advances to the Engine screen without touching the pick, so walking the
    /// flow straight through keeps whatever airframe was chosen.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Airframe = 5;
        flow.Accept();

        Assert.Equal(HangarScreen.Engine, flow.Screen);
        Assert.Equal(5, flow.Scratch.Airframe);
    }

    /// <summary>Every row's detail line shows its own decoded cost, weight and capacity
    /// (docs/org/hangar.md's stat table, pinned by HangarEconomyTests).</summary>
    [Fact]
    public void DetailShowsTheDecodedFigures()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        string hoplite = flow.Page.Detail(0);
        Assert.Contains("$6800", hoplite, StringComparison.Ordinal);
        Assert.Contains("1400 lbs.", hoplite, StringComparison.Ordinal);
        Assert.Contains("Capacity 4160 lbs.", hoplite, StringComparison.Ordinal);

        string balmoral = flow.Page.Detail(2);
        Assert.Contains("$1870", balmoral, StringComparison.Ordinal);
        Assert.Contains("5460 lbs.", balmoral, StringComparison.Ordinal);
        Assert.Contains("Capacity 15760 lbs.", balmoral, StringComparison.Ordinal);
    }

    /// <summary>The stars are HangarEconomy's own: Hoplite tops agility at 4, the Balmoral sits
    /// at 0 (the plan's hand-checked pair); armour stars track the scratch plane's armour units,
    /// exactly as the decoded formula reads the record.</summary>
    [Fact]
    public void StarRatingsMatchTheEconomy()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);

        Assert.Contains("Agility ★★★★", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("Armor ☆☆☆☆", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("Agility ☆☆☆☆", flow.Page.Detail(2), StringComparison.Ordinal);
        Assert.Contains("Armor ★☆☆☆", flow.Page.Detail(2), StringComparison.Ordinal);

        flow.Scratch.ArmourNose = 12;
        flow.Scratch.ArmourTail = 12;
        flow.Scratch.ArmourLeftWing = 12;
        flow.Scratch.ArmourRightWing = 12;
        Assert.Contains("Armor ★★★★", flow.Page.Detail(0), StringComparison.Ordinal);
    }

    /// <summary>Reading a detail line never edits the scratch plane, even though the ratings are
    /// priced for the focused row's airframe rather than the chosen one.</summary>
    [Fact]
    public void DetailLeavesTheScratchUntouched()
    {
        var flow = OpenOnAirframe(UiStrings.Empty);
        flow.Scratch.Airframe = 7;
        flow.Page.Detail(2);

        Assert.Equal(7, flow.Scratch.Airframe);
    }

    /// <summary>A flow opened without a data root has no art to offer, and the screen still
    /// works: art is optional by design.</summary>
    [Fact]
    public void ArtIsNullWithoutADataRoot() =>
        Assert.Null(OpenOnAirframe(UiStrings.Empty).Page.Art);

    /// <summary>With the extraction present, the page's art is the focused airframe's blueprint
    /// TGA at its catalogued 358x335, captioned with that airframe's name, and it follows the
    /// cursor rather than the pick.</summary>
    [ExtractedDataFact]
    public void ArtShowsTheFocusedAirframesBlueprint()
    {
        var flow = new HangarFlow(_store, UiStrings.Empty, TestData.DataRoot);
        flow.Accept();

        var art = flow.Page.Art;
        Assert.NotNull(art);
        Assert.Equal(358, art!.Image.Width);
        Assert.Equal(335, art.Image.Height);
        Assert.Equal("Airframe 0", art.Caption);

        flow.Move(1);
        Assert.Equal("Airframe 1", flow.Page.Art!.Caption);
    }

    // A flow standing on the AIRFRAME screen with a fresh scratch plane.
    private HangarFlow OpenOnAirframe(UiStrings strings)
    {
        var flow = new HangarFlow(_store, strings);
        flow.Accept(); // New Plane, on to Airframe
        Assert.Equal(HangarScreen.Airframe, flow.Screen);
        return flow;
    }
}
