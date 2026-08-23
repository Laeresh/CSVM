using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The hangar shell's own engine-free surface: the screen order, back/next navigation, the
/// scratch plane's lifetime, and the commit that ends a flow. `LaunchMenu` is engine-bound and
/// untestable directly, so these are the facts standing behind the screens it draws, the same
/// role LaunchMenuWizardTests plays for the Instant Action wizard.
/// </summary>
public class HangarFlowTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarFlowTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-hangar-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>The original's screen order (docs/org/hangar.md), plane selection first and the
    /// purchase review last.</summary>
    [Fact]
    public void ScreenOrderIsTheOriginals() =>
        Assert.Equal(
            new[]
            {
                HangarScreen.PlaneSelection, HangarScreen.Airframe, HangarScreen.Engine,
                HangarScreen.Armour, HangarScreen.Guns, HangarScreen.Hardpoints,
                HangarScreen.Paint, HangarScreen.Name, HangarScreen.Purchase,
            },
            HangarFlow.Order);

    [Fact]
    public void AFlowOpensOnPlaneSelection()
    {
        var flow = Open();
        Assert.Equal(HangarScreen.PlaneSelection, flow.Screen);
        Assert.Equal(HangarExit.None, flow.Exit);
    }

    /// <summary>Every screen's heading comes from its own langui id, not a literal.</summary>
    [Fact]
    public void EveryScreenTitlesItselfFromLangui()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":1004,\"text\":\"Airframe\",\"dll\":\"langui\"}," +
            "{\"id\":1010,\"text\":\"Purchase\",\"dll\":\"langui\"}]");
        var flow = new HangarFlow(_store, strings);
        flow.Accept(); // New Plane, on to Airframe
        Assert.Equal("Airframe", flow.Page.Title);
        Walk(flow, HangarScreen.Purchase);
        Assert.Equal("Purchase", flow.Page.Title);
    }

    /// <summary>Confirm walks forward one screen at a time and stops on the last one, which is
    /// the commit's own screen rather than a tenth.</summary>
    [Fact]
    public void ConfirmWalksTheOrderAndStopsOnPurchase()
    {
        var flow = Open();
        foreach (var screen in HangarFlow.Order)
        {
            Assert.Equal(screen, flow.Screen);
            if (screen != HangarScreen.Purchase)
            {
                flow.Accept();
            }
        }

        flow.Accept(); // the purchase page handles the press itself, so nothing advances past it
        Assert.Equal(HangarScreen.Purchase, flow.Screen);
    }

    [Fact]
    public void BackWalksTheOrderInReverse()
    {
        var flow = Open();
        Walk(flow, HangarScreen.Guns);
        flow.Back();
        Assert.Equal(HangarScreen.Armour, flow.Screen);
        flow.Back();
        Assert.Equal(HangarScreen.Engine, flow.Screen);
    }

    /// <summary>Backing out of the first screen cancels the flow.</summary>
    [Fact]
    public void BackOffTheFirstScreenCancels()
    {
        var flow = Open();
        flow.Back();
        Assert.Equal(HangarExit.Cancelled, flow.Exit);
    }

    /// <summary>Cancelling from a screen deep in the flow leaves no residue: the scratch plane is
    /// only ever in memory until the commit, so the store is untouched.</summary>
    [Fact]
    public void CancellingMidFlowWritesNothing()
    {
        var flow = Open();
        Walk(flow, HangarScreen.Name);
        flow.Scratch.Airframe = 3;
        flow.Step(1); // give it a name, so only the missing commit stops a save
        while (flow.Exit == HangarExit.None)
        {
            flow.Back();
        }

        Assert.Equal(HangarExit.Cancelled, flow.Exit);
        Assert.Null(flow.BuiltPlaneName);
        Assert.Empty(_store.List());
    }

    /// <summary>A completed flow saves the scratch plane and hands its name back, which is what
    /// D31's auto-select will key off.</summary>
    [Fact]
    public void CompletingTheFlowSavesTheScratchPlaneAndNamesIt()
    {
        var flow = Open();
        flow.Accept(); // New Plane
        flow.Scratch.Airframe = 0;
        flow.Scratch.Engine = 1;
        flow.Scratch.Name = "Test Hoplite";
        Walk(flow, HangarScreen.Purchase);
        Assert.True(flow.Commit());

        Assert.Equal(HangarExit.Built, flow.Exit);
        Assert.Equal("Test Hoplite", flow.BuiltPlaneName);
        var saved = Assert.Single(_store.List());
        Assert.Equal("Test Hoplite", saved.Name);
        Assert.Equal(1, saved.Engine);
    }

    /// <summary>The gate is HangarEconomy's verdict, in the original's own words, and it blocks
    /// the commit rather than saving a plane that cannot fly.</summary>
    [Fact]
    public void AnEnginelessBuildIsRefusedWithTheOriginalsWords()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Name = "Engineless";
        Walk(flow, HangarScreen.Purchase);

        Assert.False(flow.Commit());
        Assert.Contains("No Engine Selected", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_store.List());
    }

    /// <summary>An overweight build is refused —— a Balmoral loaded past its 15760 lb
    /// capacity (the figures HangarEconomyTests pins).</summary>
    [Fact]
    public void AnOverweightBuildIsRefused()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Name = "Overweight";
        flow.Scratch.Airframe = 2;
        flow.Scratch.Engine = 5;
        flow.Scratch.ArmourNose = 12;
        flow.Scratch.ArmourTail = 12;
        flow.Scratch.ArmourLeftWing = 12;
        flow.Scratch.ArmourRightWing = 12;
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            flow.Scratch.Guns[slot] = new GunChoice(4, true);
        }

        flow.Scratch.LeftHardpoints = 4;
        flow.Scratch.RightHardpoints = 4;
        Walk(flow, HangarScreen.Purchase);

        Assert.False(flow.Commit());
        Assert.Contains("OVERWEIGHT", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_store.List());
    }

    /// <summary>A nameless build is refused before the economy is consulted at all: the name is
    /// the store's identity, and the original has its own string for exactly this.</summary>
    [Fact]
    public void ANamelessBuildIsRefused()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "   ";
        Walk(flow, HangarScreen.Purchase);

        Assert.False(flow.Commit());
        Assert.Contains("name", flow.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_store.List());
    }

    /// <summary>Editing a saved plane starts from a copy: abandoning the edit must leave what is
    /// on disk exactly as it was.</summary>
    [Fact]
    public void EditingASavedPlaneWorksOnACopy()
    {
        _store.Save(new CustomPlaneDef { Name = "Saved One", Airframe = 4, Engine = 2 });
        var flow = Open();

        Assert.Equal(2, flow.Page.RowCount); // New Plane + the one saved plane
        Assert.Equal("Saved One", flow.Page.RowText(1));
        flow.Move(1);
        flow.Accept();

        Assert.Equal(4, flow.Scratch.Airframe);
        flow.Scratch.Airframe = 9;
        Assert.Equal(4, _store.Load("Saved One")!.Airframe);
    }

    /// <summary>The plane-selection screen's New Plane row seats a fresh scratch plane, so a
    /// second pass through the flow never inherits the first one's picks.</summary>
    [Fact]
    public void NewPlaneSeatsAFreshScratch()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Airframe = 7;
        while (flow.Screen != HangarScreen.PlaneSelection)
        {
            flow.Back();
        }

        flow.Accept();
        Assert.Equal(0, flow.Scratch.Airframe);
        Assert.Equal(CustomPlaneDef.EngineNone, flow.Scratch.Engine);
    }

    /// <summary>The screens C22-C26 have not landed yet edit nothing: a flow walked straight
    /// through them produces the plane the caller set up and no other.</summary>
    [Fact]
    public void ThePlaceholderScreensEditNothing()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Airframe = 5;
        flow.Scratch.Engine = 3;
        flow.Scratch.LeftHardpoints = 2;
        Walk(flow, HangarScreen.Name);

        Assert.Equal(5, flow.Scratch.Airframe);
        Assert.Equal(3, flow.Scratch.Engine);
        Assert.Equal(2, flow.Scratch.LeftHardpoints);
    }

    /// <summary>The persistent totals line (Decision 10) is the build's price and weight against
    /// the airframe's capacity, recomputed from the economy on demand.</summary>
    [Fact]
    public void TheTotalsLineCarriesPriceWeightAndCapacity()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Engine = 1;

        Assert.Equal("$7650   2400 / 4160 lbs.", flow.TotalsLine);
        Assert.False(flow.TotalsOverweight);
    }

    /// <summary>Over capacity, the totals line is visibly flagged with the original's word and
    /// the shell's colour flag reads true.</summary>
    [Fact]
    public void TheTotalsLineFlagsOverweight()
    {
        var flow = Open();
        flow.Accept();
        flow.Scratch.Airframe = 0;
        flow.Scratch.Engine = 5;
        flow.Scratch.LeftHardpoints = 4;
        flow.Scratch.RightHardpoints = 4;

        // Hoplite 1400 + engine 2000 + 8 hardpoints x 480 = 7240 lbs against 4160.
        Assert.Contains("7240 / 4160 lbs.", flow.TotalsLine, StringComparison.Ordinal);
        Assert.Contains("OVERWEIGHT", flow.TotalsLine, StringComparison.Ordinal);
        Assert.True(flow.TotalsOverweight);
    }

    // Confirms forward until the flow is on `target`, so a test names the screen it cares about
    // rather than counting presses.
    private static void Walk(HangarFlow flow, HangarScreen target)
    {
        for (int guard = 0; flow.Screen != target && guard < HangarFlow.Order.Length; guard++)
        {
            flow.Accept();
        }

        Assert.Equal(target, flow.Screen);
    }

    private HangarFlow Open() => new(_store, UiStrings.Empty);
}
