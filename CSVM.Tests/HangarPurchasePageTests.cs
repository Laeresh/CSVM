using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The PURCHASE screen (PLAN-hangar C26): the itemised review with one row per priced thing the
/// scratch plane carries (airframe always, engine when chosen, armed gun slots named as the GUNS
/// screen names them, armoured zones via langui 1191-1194, wings with hardpoints via 1176/1177),
/// each detailed with its decoded cost and weight, then the totals row and the Purchase Now row.
/// The row set is dynamic like the original's, and the Purchase Now row mirrors the original's
/// button gate: flagged with the problems text visible whenever the verdict is not Ok, not only
/// refusing on press.
/// </summary>
public class HangarPurchasePageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarPurchasePageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-purchase-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>A bare build (no engine, no guns, no armour, no hardpoints) reviews as exactly
    /// the airframe, the totals and the Purchase Now row: absent components get no row, the
    /// original's dynamic count.</summary>
    [Fact]
    public void ABareBuildShowsOnlyAirframeTotalsAndBuild()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);

        Assert.Equal(3, flow.Page.RowCount);
        Assert.Equal("Airframe 0", flow.Page.RowText(0));
        Assert.Equal("$6800   1400 lbs.", flow.Page.Detail(0));
        Assert.Equal("Totals", flow.Page.RowText(1));
    }

    /// <summary>A chosen engine gets its own row, priced by the decoded base-plus-offset line.</summary>
    [Fact]
    public void AnEngineRowAppearsWhenChosen()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Engine = 1;

        Assert.Equal(4, flow.Page.RowCount);
        Assert.Equal("Engine 1", flow.Page.RowText(1));
        Assert.Equal("$850   1000 lbs.", flow.Page.Detail(1));
    }

    /// <summary>Each armed gun slot rows exactly as the GUNS screen names it (slot title, then
    /// the shared calibre naming with its twin prefix), priced by the airframe's wing or turret
    /// column, doubled for twin. Empty slots get no row.</summary>
    [Fact]
    public void ArmedGunSlotsRowAsTheGunsScreenNamesThem()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Airframe = 1; // Hellhound: turret mask 0x08, slot 3 is the turret
        flow.Scratch.Guns[0] = new GunChoice(2, Twin: true);
        flow.Scratch.Guns[3] = new GunChoice(0, Twin: false);

        Assert.Equal(5, flow.Page.RowCount); // airframe + two guns + totals + build
        Assert.Equal("Slot 1: (2) .50-cal.", flow.Page.RowText(1));
        Assert.Equal("$820   960 lbs.", flow.Page.Detail(1)); // wing column, twinned
        Assert.Equal("Slot 4: .30-cal.", flow.Page.RowText(2));
        Assert.Equal("$440   520 lbs.", flow.Page.Detail(2)); // the turret column
    }

    /// <summary>Armour zones with units row through their own langui formats (1191-1194), priced
    /// at the decoded units x4 for cost and weight; empty zones get no row.</summary>
    [Fact]
    public void ArmouredZonesRowThroughTheirLanguiFormats()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":1191,\"text\":\"Nose: %1!d! units\",\"dll\":\"langui\"}," +
            "{\"id\":1193,\"text\":\"Left Wing: %1!d! units\",\"dll\":\"langui\"}]");
        var flow = OpenOnPurchase(strings);
        flow.Scratch.ArmourNose = 5;
        flow.Scratch.ArmourLeftWing = 2;

        Assert.Equal(5, flow.Page.RowCount);
        Assert.Equal("Nose: 5 units", flow.Page.RowText(1));
        Assert.Equal("$20   20 lbs.", flow.Page.Detail(1));
        Assert.Equal("Left Wing: 2 units", flow.Page.RowText(2));
        Assert.Equal("$8   8 lbs.", flow.Page.Detail(2));
    }

    /// <summary>Each wing with hardpoints rows through 1176/1177 at the decoded $410 / 480 lb
    /// each; a bare wing gets no row.</summary>
    [Fact]
    public void WingsWithHardpointsGetTheirRows()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":1177,\"text\":\"Right Wing: %1!d!\",\"dll\":\"langui\"}]");
        var flow = OpenOnPurchase(strings);
        flow.Scratch.RightHardpoints = 2;

        Assert.Equal(4, flow.Page.RowCount);
        Assert.Equal("Right Wing: 2", flow.Page.RowText(1));
        Assert.Equal("$820   960 lbs.", flow.Page.Detail(1));
    }

    /// <summary>A missing string table falls back to the same shapes in plain text.</summary>
    [Fact]
    public void RowTextFallsBackWithoutStrings()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.ArmourTail = 3;
        flow.Scratch.LeftHardpoints = 1;

        Assert.Equal("Tail: 3 units", flow.Page.RowText(1));
        Assert.Equal("Left Wing: 1", flow.Page.RowText(2));
        Assert.Equal("Purchase Now", flow.Page.RowText(flow.Page.RowCount - 1)[^12..]);
    }

    /// <summary>The totals row carries the bill's grand total judged against the airframe's
    /// capacity, the same line the shell shows on every hangar screen.</summary>
    [Fact]
    public void TheTotalsRowJudgesWeightAgainstCapacity()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Engine = 1;

        int totals = flow.Page.RowCount - 2;
        Assert.Equal("Totals", flow.Page.RowText(totals));
        Assert.Equal("$7650   2400 / 4160 lbs.", flow.Page.Detail(totals));
    }

    /// <summary>The gate is the button, not only the commit: an engineless build flags the
    /// Purchase Now row and shows the problems text before any press, the original's greyed
    /// pur_b_purchase with pur_t_problems carrying the reason.</summary>
    [Fact]
    public void TheBuildRowIsFlaggedAndExplainsWhenBlocked()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        var page = (HangarPurchasePage)flow.Page;

        int build = page.RowCount - 1;
        Assert.False(page.BuildEnabled);
        Assert.StartsWith("✕", page.RowText(build), StringComparison.Ordinal);
        Assert.Equal("CAN'T PURCHASE: No Engine Selected", page.Detail(build));

        flow.Scratch.Engine = 1;
        Assert.True(page.BuildEnabled);
        Assert.Equal("Purchase Now", page.RowText(page.RowCount - 1));
        Assert.Equal(string.Empty, page.Detail(page.RowCount - 1));
    }

    /// <summary>An overweight build flags with the original's word on the Purchase Now row and on
    /// the totals line both.</summary>
    [Fact]
    public void AnOverweightBuildFlagsWithTheOriginalsWord()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        MakeOverweight(flow.Scratch);

        Assert.Contains("OVERWEIGHT", flow.Page.Detail(flow.Page.RowCount - 1), StringComparison.Ordinal);
        Assert.Contains("OVERWEIGHT", flow.Page.Detail(flow.Page.RowCount - 2), StringComparison.Ordinal);
    }

    /// <summary>The Purchase Now press commits: the existing gate semantics, now reached through
    /// the itemised page's own build row.</summary>
    [Fact]
    public void TheBuildRowCommits()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Reviewed";

        Assert.True(flow.Page.Accept(flow.Page.RowCount - 1));
        Assert.Equal(HangarExit.Built, flow.Exit);
        Assert.Equal("Reviewed", Assert.Single(_store.List()).Name);
    }

    /// <summary>A press on a review row is a no-op: it neither commits nor advances, since the
    /// purchase screen is the last one.</summary>
    [Fact]
    public void AReviewRowPressDoesNotCommit()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Not Yet";

        Assert.True(flow.Page.Accept(0));
        Assert.Equal(HangarExit.None, flow.Exit);
        Assert.Empty(_store.List());
    }

    /// <summary>A blocked build's press is still refused with the commit's own words; the flag on
    /// the row is a mirror of that rule, not a replacement for it.</summary>
    [Fact]
    public void ABlockedBuildPressIsStillRefused()
    {
        var flow = OpenOnPurchase(UiStrings.Empty);
        flow.Scratch.Name = "Engineless";

        Assert.True(flow.Page.Accept(flow.Page.RowCount - 1));
        Assert.Equal(HangarExit.None, flow.Exit);
        Assert.Contains("No Engine Selected", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_store.List());
    }

    // The overweight rig HangarFlowTests uses: a Balmoral loaded far past its 15760 lb capacity.
    private static void MakeOverweight(CustomPlaneDef def)
    {
        def.Airframe = 2;
        def.Engine = 5;
        def.ArmourNose = 12;
        def.ArmourTail = 12;
        def.ArmourLeftWing = 12;
        def.ArmourRightWing = 12;
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            def.Guns[slot] = new GunChoice(4, Twin: true);
        }

        def.LeftHardpoints = 4;
        def.RightHardpoints = 4;
    }

    // A flow standing on the purchase screen over a fresh scratch plane.
    private HangarFlow OpenOnPurchase(UiStrings strings)
    {
        var flow = new HangarFlow(_store, strings);
        for (int guard = 0; flow.Screen != HangarScreen.Purchase && guard < HangarFlow.Order.Length; guard++)
        {
            flow.Accept();
        }

        Assert.Equal(HangarScreen.Purchase, flow.Screen);
        return flow;
    }
}
