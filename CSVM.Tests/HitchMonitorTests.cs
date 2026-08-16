using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The trigger math behind <see cref="HitchMonitor"/>: the floor and relative regimes and the
/// boundary between them, the grace window, the true-median baseline, the two wraparounds (the
/// rolling window and the ring buffer), and what a record carries. Every test builds the monitor
/// over explicit constants, because the <c>hitchMonitor.*</c> config keys are the shipped defaults
/// rather than what is asserted here.
/// </summary>
public class HitchMonitorTests
{
    // The counters are not what most of these tests are about; one shared value keeps every delta
    // at zero except where a test says otherwise.
    private static readonly FrameCounters Idle = new(
        ScriptMs: 1, RenderCpuMs: 2, GpuMs: 3, PhysicsMs: 0.5,
        Draws: 100, Prims: 5000, Nodes: 400, MemBytes: 1024);

    // ---- the absolute floor fires when the baseline is far below it ----

    [Fact]
    public void FloorFiresWhateverTheMedianSays()
    {
        // 4 x a 5 ms median is 20 ms, well under the 40 ms floor, so the floor is the threshold.
        var mon = Fill(NewMonitor(), 5, 8);

        Assert.False(mon.Tick(39.9, Idle));
        Assert.False(mon.Tick(40.0, Idle)); // strictly greater than, so the boundary itself is quiet
        Assert.True(mon.Tick(40.1, Idle));
        Assert.Equal(1, mon.HitchCount);
        Assert.Equal(40.0, mon.ThresholdMs, 6);
    }

    // ---- above the crossover the relative term takes over ----

    [Fact]
    public void RelativeTermDecidesOnceItExceedsTheFloor()
    {
        // A 20 ms median puts 4 x median at 80 ms, so the floor no longer decides anything.
        var mon = Fill(NewMonitor(), 20, 8);

        Assert.False(mon.Tick(79.9, Idle));
        Assert.False(mon.Tick(80.0, Idle));
        Assert.True(mon.Tick(80.1, Idle));
        Assert.Equal(80.0, mon.ThresholdMs, 6);
    }

    // ---- a frame is judged against its neighbours, not against itself ----

    [Fact]
    public void TheHitchingFrameIsNotInItsOwnBaseline()
    {
        var mon = Fill(NewMonitor(), 20, 8);

        Assert.True(mon.Tick(500, Idle));
        Assert.Equal(20.0, mon.Last.BaselineMs, 6);
        Assert.Equal(80.0, mon.Last.ThresholdMs, 6);
        Assert.Equal(500.0, mon.Last.FrameMs, 6);
    }

    // ---- the baseline is a median, so one hitch cannot raise it and hide the next ----

    [Fact]
    public void OneHitchDoesNotMoveTheMedian()
    {
        var mon = NewMonitor(baselineFrames: 9);
        Fill(mon, 20, 8);
        mon.Tick(2000, Idle); // window now full, with one enormous outlier in it

        // A mean of eight 20s and one 2000 would be 240, which would put the threshold at 960 and
        // swallow every hitch that followed.
        mon.Tick(1, Idle);
        Assert.Equal(20.0, mon.BaselineMs, 6);
    }

    [Fact]
    public void EvenSizedWindowAveragesTheTwoMiddleFrames()
    {
        var mon = NewMonitor(baselineFrames: 4);
        mon.Tick(10, Idle);
        mon.Tick(20, Idle);
        mon.Tick(30, Idle);
        mon.Tick(50, Idle);

        mon.Tick(1, Idle); // first tick with a full window: median of 10/20/30/50
        Assert.Equal(25.0, mon.BaselineMs, 6);
    }

    // ---- the rolling window forgets: old frames leave it as new ones arrive ----

    [Fact]
    public void OldFramesLeaveTheRollingWindow()
    {
        var mon = NewMonitor(baselineFrames: 8);
        Fill(mon, 100, 8);
        Fill(mon, 10, 8);

        mon.Tick(1, Idle);
        Assert.Equal(10.0, mon.BaselineMs, 6); // the eight 100s have all wrapped out
    }

    // ---- until the window is full there is no estimate, so only the floor governs ----

    [Fact]
    public void PartialWindowUsesTheFloorAlone()
    {
        var mon = NewMonitor(baselineFrames: 8);
        mon.Tick(30, Idle);
        mon.Tick(30, Idle); // 4 x 30 would be 120 if a two-sample median counted

        Assert.Equal(0.0, mon.BaselineMs, 6);
        Assert.Equal(40.0, mon.ThresholdMs, 6);
        Assert.True(mon.Tick(50, Idle));
    }

    // ---- the grace window keeps a session build out of the record ----

    [Fact]
    public void GraceWindowSuppressesTheFramesAfterARearm()
    {
        var mon = NewMonitor(graceMs: 100);

        Assert.False(mon.Tick(90, Idle)); // 0 ms into the grace window: 90 ms would trip otherwise
        Assert.False(mon.Tick(15, Idle)); // 90 ms in, still inside
        Assert.True(mon.Tick(500, Idle)); // 105 ms in: the window has elapsed
        Assert.Equal(1, mon.HitchCount);
    }

    [Fact]
    public void RearmRestartsGraceAndDropsTheBaseline()
    {
        var mon = Fill(NewMonitor(graceMs: 100), 20, 8);
        Assert.True(mon.Tick(500, Idle));

        mon.Rearm();
        Assert.Equal(0.0, mon.BaselineMs, 6);
        Assert.False(mon.Tick(500, Idle)); // grace is running again
        Assert.Equal(1, mon.HitchCount);   // and nothing new was counted
    }

    // ---- the ring buffer wraps, and a record reads oldest first ----

    [Fact]
    public void RingCarriesTheRunUpOldestFirstEndingWithTheHitch()
    {
        var mon = NewMonitor(baselineFrames: 4, ringFrames: 4);
        mon.Tick(10, Idle);
        mon.Tick(11, Idle);
        mon.Tick(12, Idle);
        mon.Tick(13, Idle); // ring full: 10, 11, 12, 13
        mon.Tick(14, Idle); // wrapped: 11, 12, 13, 14

        Assert.True(mon.Tick(500, Idle));
        Assert.Equal(4, mon.Last.RingCount);
        Assert.Equal(12.0, mon.Last.Ring[0].FrameMs, 6);
        Assert.Equal(13.0, mon.Last.Ring[1].FrameMs, 6);
        Assert.Equal(14.0, mon.Last.Ring[2].FrameMs, 6);
        Assert.Equal(500.0, mon.Last.Ring[3].FrameMs, 6); // the hitching frame is the last entry
    }

    [Fact]
    public void PartiallyFilledRingReportsOnlyWhatItHolds()
    {
        var mon = NewMonitor(ringFrames: 64);
        mon.Tick(10, Idle);

        Assert.True(mon.Tick(500, Idle));
        Assert.Equal(2, mon.Last.RingCount);
        Assert.Equal(10.0, mon.Last.Ring[0].FrameMs, 6);
        Assert.Equal(500.0, mon.Last.Ring[1].FrameMs, 6);
    }

    // ---- the record's counter terms are absolutes plus deltas against the frame before ----

    [Fact]
    public void CountsAreRecordedAbsoluteAndAsDeltas()
    {
        var mon = NewMonitor();
        mon.Tick(10, Idle);
        var burst = Idle with { Draws = 180, Prims = 5400, Nodes = 900, MemBytes = 4096 };

        Assert.True(mon.Tick(500, burst));
        Assert.Equal(900L, mon.Last.Nodes);
        Assert.Equal(500L, mon.Last.NodesDelta);
        Assert.Equal(80L, mon.Last.DrawsDelta);
        Assert.Equal(400L, mon.Last.PrimsDelta);
        Assert.Equal(3072L, mon.Last.MemBytesDelta);
    }

    [Fact]
    public void TheFirstFrameAfterARearmHasNoDeltasToReport()
    {
        var mon = NewMonitor();
        mon.Tick(10, Idle);
        mon.Rearm();

        // Nothing preceded this frame in this session, so subtracting the previous one would
        // invent a spike out of another session's counters.
        Assert.True(mon.Tick(500, Idle with { Nodes = 9000 }));
        Assert.Equal(0L, mon.Last.NodesDelta);
        Assert.Equal(9000L, mon.Last.Nodes);
    }

    // ---- the cost split is carried through unaveraged ----

    [Fact]
    public void RecordCarriesTheUnaveragedCostSplit()
    {
        var mon = NewMonitor();
        var spike = new FrameCounters(ScriptMs: 41, RenderCpuMs: 6, GpuMs: 7, PhysicsMs: 2,
            Draws: 1, Prims: 1, Nodes: 1, MemBytes: 1);

        Assert.True(mon.Tick(56, spike));
        Assert.Equal(1L, mon.Last.Frame);
        Assert.Equal(41.0, mon.Last.ScriptMs, 6);
        Assert.Equal(6.0, mon.Last.RenderCpuMs, 6);
        Assert.Equal(7.0, mon.Last.GpuMs, 6);
        Assert.Equal(2.0, mon.Last.PhysicsMs, 6);
    }

    // ---- FrameCount is exposed one call early, for --hitch-inject= to act on ----

    [Fact]
    public void FrameCountIsOneAheadOfTheNextTicksFrame()
    {
        var mon = NewMonitor();
        Assert.Equal(0L, mon.FrameCount);

        mon.Tick(1, Idle);
        mon.Tick(1, Idle);
        Assert.Equal(2L, mon.FrameCount);

        // A caller reading FrameCount before this Tick sees 2, and Tick's own record agrees the
        // frame it just stamped was FrameCount + 1 — the invariant --hitch-inject= relies on to
        // fire on the stated frame rather than the one before or after it.
        Assert.True(mon.Tick(500, Idle));
        Assert.Equal(3L, mon.FrameCount);
        Assert.Equal(mon.FrameCount, mon.Last.Frame);
    }

    // ---- a nonsense frame time cannot poison the sorted mirror for the rest of the run ----

    [Fact]
    public void NaNAndNegativeFrameTimesAreSquashedNotStored()
    {
        var mon = NewMonitor(baselineFrames: 3);
        Assert.False(mon.Tick(double.NaN, Idle));
        Assert.False(mon.Tick(-5, Idle));
        mon.Tick(0, Idle);

        mon.Tick(1, Idle);
        Assert.Equal(0.0, mon.BaselineMs, 6);
        Assert.True(mon.Tick(500, Idle)); // still detecting, so nothing was corrupted
    }

    private static HitchMonitor NewMonitor(double medianMultiple = 4, double floorMs = 40,
        int baselineFrames = 8, int ringFrames = 8, double graceMs = 0) =>
        new(medianMultiple, floorMs, baselineFrames, ringFrames, graceMs);

    // Feeds `count` frames of the same cost, so the next Tick is judged against a settled median.
    private static HitchMonitor Fill(HitchMonitor mon, double frameMs, int count)
    {
        for (int i = 0; i < count; i++)
        {
            mon.Tick(frameMs, Idle);
        }
        return mon;
    }
}
