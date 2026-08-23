using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ENGINE screen (PLAN-hangar C23): seven rows (the six per-airframe engines from langui
/// 3100+af*6+id plus the explicit no-engine row, langui 1171), the airframe page's
/// pick-and-tick stepper writing the scratch plane's engine and nothing else, and the detail
/// line carrying the decoded engine cost and weight through HangarEconomy.EngineLine.
/// </summary>
public class HangarEnginePageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarEnginePageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-engine-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>Seven rows: engine ids 0-5 named from langui 3100+af*6+id, and id 6 the
    /// no-engine row from langui 1171.</summary>
    [Fact]
    public void OffersSevenRows_NamedFromLangui()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":3100,\"text\":\"Fairfield 650\",\"dll\":\"langui\"}," +
            "{\"id\":3105,\"text\":\"Fairfield 1000 Nitro\",\"dll\":\"langui\"}," +
            "{\"id\":1171,\"text\":\"No Engine Selected\",\"dll\":\"langui\"}]");
        var flow = OpenOnEngine(strings);

        Assert.Equal(7, flow.Page.RowCount);
        Assert.StartsWith("Fairfield 650", flow.Page.RowText(0), StringComparison.Ordinal);
        Assert.StartsWith("Fairfield 1000 Nitro", flow.Page.RowText(5), StringComparison.Ordinal);
        Assert.StartsWith("No Engine Selected", flow.Page.RowText(6), StringComparison.Ordinal);
    }

    /// <summary>The names follow the scratch plane's airframe: airframe 2 reads from the
    /// 3112-3117 band (3100 + 2*6 + id), not airframe 0's.</summary>
    [Fact]
    public void EngineNamesFollowTheAirframe()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":3112,\"text\":\"Whirlwind 800\",\"dll\":\"langui\"}]");
        var flow = OpenOnEngine(strings);
        flow.Scratch.Airframe = 2;

        Assert.StartsWith("Whirlwind 800", flow.Page.RowText(0), StringComparison.Ordinal);
    }

    /// <summary>A fresh build has no engine, so the no-engine row opens ticked.</summary>
    [Fact]
    public void TheDefaultPickIsNoEngine()
    {
        var flow = OpenOnEngine(UiStrings.Empty);

        Assert.EndsWith("✓", flow.Page.RowText(6), StringComparison.Ordinal);
        Assert.False(flow.Page.RowText(0).EndsWith("✓", StringComparison.Ordinal));
    }

    /// <summary>The ←→ stepper makes the focused row the pick and ticks it; stepping a row that
    /// already is the pick changes nothing.</summary>
    [Fact]
    public void SteppingSelectsTheFocusedEngine()
    {
        var flow = OpenOnEngine(UiStrings.Empty);
        flow.Move(1);
        flow.Move(1);

        Assert.True(flow.Step(1));
        Assert.Equal(2, flow.Scratch.Engine);
        Assert.EndsWith("✓", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.False(flow.Page.RowText(6).EndsWith("✓", StringComparison.Ordinal));
        Assert.False(flow.Step(1));
    }

    /// <summary>Confirm advances to the Armour screen without touching the pick.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnEngine(UiStrings.Empty);
        flow.Scratch.Engine = 4;
        flow.Accept();

        Assert.Equal(HangarScreen.Armour, flow.Screen);
        Assert.Equal(4, flow.Scratch.Engine);
    }

    /// <summary>Each row's detail is the decoded engine line: the airframe's base plus the
    /// per-id offsets (docs/org/hangar.md), zero for the no-engine row.</summary>
    [Fact]
    public void DetailShowsTheDecodedCostAndWeight()
    {
        var flow = OpenOnEngine(UiStrings.Empty);

        // Airframe 0: base 850 / 1000 lb; id 0 offsets -425 / -500, id 5 offsets +855 / +1000.
        Assert.Contains("$425", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("500 lbs.", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("$1705", flow.Page.Detail(5), StringComparison.Ordinal);
        Assert.Contains("2000 lbs.", flow.Page.Detail(5), StringComparison.Ordinal);
        Assert.Contains("$0", flow.Page.Detail(6), StringComparison.Ordinal);
        Assert.Contains("0 lbs.", flow.Page.Detail(6), StringComparison.Ordinal);

        // Airframe 2: base 2550 / 3000 lb; id 2 offsets +425 / +500.
        flow.Scratch.Airframe = 2;
        Assert.Contains("$2975", flow.Page.Detail(2), StringComparison.Ordinal);
        Assert.Contains("3500 lbs.", flow.Page.Detail(2), StringComparison.Ordinal);
    }

    /// <summary>The power stat is the base rating times the decoded per-id factor at
    /// 0x00619e38 (0.9/1.0/1.1, then x1.33 for nitrous), truncated; none shows no power.</summary>
    [Fact]
    public void DetailShowsThePowerStat()
    {
        var flow = OpenOnEngine(UiStrings.Empty);

        // Airframe 0 rating 200: id 0 = 180, id 2 = 220, id 5 = 200 * 1.463 = 292.6 -> 292.
        Assert.Contains("Power 180", flow.Page.Detail(0), StringComparison.Ordinal);
        Assert.Contains("Power 220", flow.Page.Detail(2), StringComparison.Ordinal);
        Assert.Contains("Power 292", flow.Page.Detail(5), StringComparison.Ordinal);
        Assert.DoesNotContain("Power", flow.Page.Detail(6), StringComparison.Ordinal);
    }

    /// <summary>The detail never edits the scratch plane.</summary>
    [Fact]
    public void DetailLeavesTheScratchUntouched()
    {
        var flow = OpenOnEngine(UiStrings.Empty);
        flow.Scratch.Engine = 3;
        flow.Page.Detail(5);

        Assert.Equal(3, flow.Scratch.Engine);
    }

    /// <summary>The plan's verify line: with the real extraction, every airframe x engine cell
    /// of the 3100 name band resolves to a langui row.</summary>
    [ExtractedDataFact]
    public void EngineNamesResolveForAllAirframes()
    {
        var strings = UiStrings.TryLoad(TestData.DataRoot!);
        Assert.NotNull(strings);

        for (int airframe = 0; airframe <= CustomPlaneDef.MaxAirframe; airframe++)
        {
            for (int engine = 0; engine < CustomPlaneDef.EngineNone; engine++)
            {
                Assert.True(
                    strings!.Has(3100 + (airframe * 6) + engine),
                    $"missing langui {3100 + (airframe * 6) + engine} (airframe {airframe}, engine {engine})");
            }
        }

        Assert.True(strings!.Has(1171));
    }

    // A flow standing on the ENGINE screen with a fresh scratch plane.
    private HangarFlow OpenOnEngine(UiStrings strings)
    {
        var flow = new HangarFlow(_store, strings);
        flow.Accept(); // New Plane, on to Airframe
        flow.Accept(); // on to Engine
        Assert.Equal(HangarScreen.Engine, flow.Screen);
        return flow;
    }
}
