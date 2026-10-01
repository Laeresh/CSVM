using System;
using System.Diagnostics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The bin each gap between marks lands in, behind <see cref="EngineGapCost"/>. A physics tail opens
/// the engine's step and a process tail the deferred flush. The draw signals open the draw and then
/// the idle rest. A head mark closes whichever gap is open
/// and opens none, so the time inside a pass is never charged to a gap. The clock is one the tests
/// advance, so every figure is asserted by value (docs/verification.md PERF-30).
/// ⚠ This is the ONLY class that writes <see cref="EngineGapCost"/>'s statics; xUnit runs one
/// class's tests serially, which is what keeps them deterministic.
/// </summary>
public sealed class EngineGapCostTests : IDisposable
{
    private long _now = 1;

    public EngineGapCostTests()
    {
        EngineGapCost.Clock = () => _now;
        EngineGapCost.Reset();
    }

    public void Dispose()
    {
        EngineGapCost.Clock = Stopwatch.GetTimestamp;
        EngineGapCost.Reset();
    }

    [Fact]
    public void AEachGapLandsInTheBinOfTheMarkThatOpenedIt()
    {
        EngineGapCost.MarkTail(physics: true);
        Advance(6);
        EngineGapCost.MarkHead();
        Advance(100);
        EngineGapCost.MarkTail(physics: false);
        Advance(8);
        EngineGapCost.MarkPreDraw();
        Advance(10);
        EngineGapCost.MarkPostDraw();
        Advance(4);
        EngineGapCost.MarkHead();

        var (phys, defer, draw, idle) = EngineGapCost.Take();
        Assert.Equal(6, phys, 6);
        Assert.Equal(8, defer, 6);
        Assert.Equal(10, draw, 6);
        Assert.Equal(4, idle, 6);
    }

    [Fact]
    public void BTimeInsideAPassIsChargedToNoGap()
    {
        EngineGapCost.MarkHead();
        Advance(10);
        EngineGapCost.MarkTail(physics: true);
        Advance(2);
        EngineGapCost.MarkHead();
        Advance(10);

        var (phys, defer, draw, idle) = EngineGapCost.Take();
        Assert.Equal(2, phys, 6);
        Assert.Equal(0, defer);
        Assert.Equal(0, draw);
        Assert.Equal(0, idle);
    }

    [Fact]
    public void CWithoutTheDrawSignalsThePostProcessGapIsAllDeferredFlush()
    {
        EngineGapCost.MarkTail(physics: false);
        Advance(8);
        EngineGapCost.MarkHead();

        var (_, defer, draw, idle) = EngineGapCost.Take();
        Assert.Equal(8, defer, 6);
        Assert.Equal(0, draw);
        Assert.Equal(0, idle);
    }

    [Fact]
    public void DADrainEmptiesEveryBinAndAResetDropsAStandingMark()
    {
        EngineGapCost.MarkTail(physics: true);
        Advance(5);
        EngineGapCost.MarkHead();
        EngineGapCost.Take();
        var (phys, _, _, _) = EngineGapCost.Take();
        Assert.Equal(0, phys);

        EngineGapCost.MarkTail(physics: true);
        Advance(5);
        EngineGapCost.Reset();
        EngineGapCost.MarkHead();
        (phys, _, _, _) = EngineGapCost.Take();
        Assert.Equal(0, phys);
    }

    private void Advance(double ms) => _now += (long)(ms * Stopwatch.Frequency / 1000.0);
}
