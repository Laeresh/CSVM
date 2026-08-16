using System;
using System.Diagnostics;
using CSVM.Utils;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// The accumulate / freeze / snapshot cycle behind <see cref="PerfSample"/>:
/// what a scope adds, what <see cref="PerfSample.EndFrame"/> hands to the next record, the
/// remainder arithmetic, and the two properties the instrument's honesty rests on — a nested scope
/// cannot double-count, and a scope allocates nothing.
///
/// <para>⚠ This is the ONLY class that writes <see cref="PerfSample"/>'s ambient statics. xUnit runs
/// test classes in parallel but the tests within one class serially, so that is what keeps these
/// deterministic; a second class exercising scopes would have to join this one.</para>
/// </summary>
public class PerfSampleTests
{
    private readonly ITestOutputHelper _out;

    public PerfSampleTests(ITestOutputHelper output)
    {
        _out = output;
        PerfSample.Reset();
    }

    // ---- what a scope accumulates ----

    [Fact]
    public void ScopesAccumulatePerSiteAndCountTheirCalls()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            Spin();
        }
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            Spin();
        }
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            Spin();
        }

        var frame = Snapshot(frameMs: 100);
        Assert.Equal(2, frame.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(1, frame.Calls[(int)PerfSite.AiSpawn]);
        Assert.True(frame.Ms[(int)PerfSite.DebrisSpawn] > 0);
        Assert.Equal(0, frame.Calls[(int)PerfSite.ResourceLoad]);
        Assert.Equal(0.0, frame.Ms[(int)PerfSite.ResourceLoad]);
    }

    [Fact]
    public void AFrameOnlyReportsItsOwnScopes()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            Spin();
        }
        Snapshot(frameMs: 100);

        // Nothing ran in the frame after it, so the next record must not repeat the one before.
        var second = Snapshot(frameMs: 100);
        Assert.Equal(0, second.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(0.0, second.AttributedMs);
        Assert.Equal(100.0, second.UnattributedMs, 9);
    }

    [Fact]
    public void SnapshotReportsTheClosedFrameNotTheOneInProgress()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            Spin();
        }
        PerfSample.EndFrame();

        // Work in the frame now in progress belongs to the NEXT record: a hitch record is filled at
        // the top of the frame after the one it describes, so a snapshot that read the live
        // accumulators would credit the hitching frame with work that has not happened yet.
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            Spin();
        }
        var frame = new PerfSampleFrame();
        PerfSample.SnapshotInto(frame, 100);
        Assert.Equal(1, frame.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(0, frame.Calls[(int)PerfSite.AiSpawn]);
    }

    // ---- the remainder is whatever the frame cost that no scope claimed ----

    [Fact]
    public void AttributedPlusUnattributedIsExactlyTheFrameCost()
    {
        using (PerfSample.Scope(PerfSite.MaterialCreate))
        {
            Spin();
        }

        var frame = Snapshot(frameMs: 62.5);
        Assert.Equal(62.5, frame.AttributedMs + frame.UnattributedMs, 9);
        Assert.True(frame.AttributedMs > 0);
        Assert.True(frame.UnattributedMs < 62.5);
    }

    [Fact]
    public void AFrameWithNoScopesIsEntirelyUnattributed()
    {
        var frame = Snapshot(frameMs: 47.0);
        Assert.Equal(0.0, frame.AttributedMs);
        Assert.Equal(47.0, frame.UnattributedMs, 9);
        Assert.Equal(0, frame.Violations);
    }

    [Fact]
    public void TheRemainderIsNotClamped()
    {
        using (PerfSample.Scope(PerfSite.ResourceLoad))
        {
            Spin();
        }

        // A frame cheaper than the work attributed to it means a scope spanned the frame boundary.
        // Clamping to zero would hide exactly that, so it reads negative instead.
        var frame = Snapshot(frameMs: 0);
        Assert.True(frame.UnattributedMs < 0);
    }

    // ---- flat leaves only: a nested scope is suppressed, never double-counted ----

    [Fact]
    public void ANestedScopeIsCountedAsAViolationAndMeasuresNothing()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            using (PerfSample.Scope(PerfSite.EffectCheckout))
            {
                Spin();
            }
            Spin();
        }

        var frame = Snapshot(frameMs: 100);
        Assert.Equal(1, frame.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(0, frame.Calls[(int)PerfSite.EffectCheckout]);
        Assert.Equal(0.0, frame.Ms[(int)PerfSite.EffectCheckout]);
        Assert.Equal(1, frame.Violations);
        // The identity survives the violation, which is the point of suppressing rather than
        // adding: the inner scope's time is inside the outer's and is reported once.
        Assert.Equal(100.0, frame.AttributedMs + frame.UnattributedMs, 9);
    }

    [Fact]
    public void TheOuterScopeStillRecordsAfterASuppressedInnerOne()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            using (PerfSample.Scope(PerfSite.EffectCheckout))
            {
                Spin();
            }
        }
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            Spin();
        }

        // The suppressed scope must not leave the gate shut behind it: everything after it would
        // silently stop being measured, which is the failure mode this instrument cannot have.
        var frame = Snapshot(frameMs: 100);
        Assert.Equal(1, frame.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(1, frame.Calls[(int)PerfSite.AiSpawn]);
        Assert.Equal(1, frame.Violations);
    }

    [Fact]
    public void AnUnknownSiteIsAViolationRatherThanAnIndexOutOfRange()
    {
        using (PerfSample.Scope((PerfSite)99))
        {
            Spin();
        }

        var frame = Snapshot(frameMs: 100);
        Assert.Equal(1, frame.Violations);
        Assert.Equal(0.0, frame.AttributedMs);
    }

    [Fact]
    public void AScopeLeftOpenAcrossAFrameBoundaryIsAViolationAndDoesNotJamTheNextFrame()
    {
        var scope = PerfSample.Scope(PerfSite.AudioLoad);
        PerfSample.EndFrame();
        var straddled = new PerfSampleFrame();
        PerfSample.SnapshotInto(straddled, 100);
        Assert.Equal(1, straddled.Violations);

        scope.Dispose();
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
            Spin();
        }
        var next = Snapshot(frameMs: 100);
        Assert.Equal(1, next.Calls[(int)PerfSite.DebrisSpawn]);
        Assert.Equal(0, next.Violations);
    }

    // ---- Reset drops both frames, for a build or a teardown ----

    [Fact]
    public void ResetDropsTheFrameInProgressAndTheOneBeforeIt()
    {
        using (PerfSample.Scope(PerfSite.ResourceLoad))
        {
            Spin();
        }
        PerfSample.EndFrame();
        using (PerfSample.Scope(PerfSite.ResourceLoad))
        {
            Spin();
        }

        PerfSample.Reset();
        var frame = Snapshot(frameMs: 100);
        Assert.Equal(0.0, frame.AttributedMs);
        Assert.Equal(0, frame.Calls[(int)PerfSite.ResourceLoad]);
    }

    // ---- the site vocabulary ----

    [Fact]
    public void EverySiteHasItsOwnWireName()
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (PerfSite site in Enum.GetValues<PerfSite>())
        {
            string name = PerfSample.NameOf(site);
            Assert.NotEqual("unknown", name);
            // Written into the sidecar's hand-built JSON as-is, so nothing here may need escaping.
            Assert.Matches("^[a-z][a-z_]*$", name);
            Assert.True(seen.Add(name), $"duplicate site name {name}");
        }
        Assert.Equal(Enum.GetValues<PerfSite>().Length, PerfSample.SiteCount);
    }

    // ---- a record carries the frame's attribution, filled by the monitor itself ----

    [Fact]
    public void AHitchRecordCarriesTheAttributionOfTheFrameItDescribes()
    {
        var idle = new FrameCounters(1, 2, 3, 0.5, 100, 5000, 400, 1024);
        var mon = new HitchMonitor(4, 40, 8, 8, 0);
        using (PerfSample.Scope(PerfSite.PartDetach))
        {
            Spin();
        }
        PerfSample.EndFrame();

        Assert.True(mon.Tick(500, idle));
        Assert.Equal(1, mon.Last.Samples.Calls[(int)PerfSite.PartDetach]);
        Assert.Equal(500.0, mon.Last.Samples.AttributedMs + mon.Last.Samples.UnattributedMs, 9);
    }

    // ---- the two properties that decide whether this can be left on ----

    [Fact]
    public void AScopeAllocatesNothing()
    {
        using (PerfSample.Scope(PerfSite.DebrisSpawn))
        {
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            using (PerfSample.Scope(PerfSite.DebrisSpawn))
            {
            }
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        // Exactly zero, not "a little": a ref-struct handle over a fixed enum has nothing to box,
        // and an instrument that allocates on the frame path manufactures the collections it is
        // there to catch.
        Assert.Equal(0L, after - before);
    }

    [Fact]
    public void AScopeCostsFarLessThanTheFrameItMeasures()
    {
        const int Iterations = 200_000;
        for (int i = 0; i < 1000; i++)
        {
            using (PerfSample.Scope(PerfSite.DebrisSpawn))
            {
            }
        }

        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < Iterations; i++)
        {
            using (PerfSample.Scope(PerfSite.DebrisSpawn))
            {
            }
        }
        double ns = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / Iterations;
        _out.WriteLine($"PerfSample.Scope: {ns:0.0} ns per open+close");

        // A ceiling two orders of magnitude above the measured cost, not a benchmark: this fails
        // when somebody puts a lock, an allocation or a dictionary lookup on the scope path, and
        // stays quiet on a loaded machine. The measured figure is the printed line above, and is
        // what the architecture entry quotes.
        Assert.True(ns < 2000, $"a scope cost {ns:0.0} ns — something expensive is on the path");
        PerfSample.Reset();
    }

    // Enough work that a scope's span is non-zero on a coarse timer, without a Thread.Sleep's
    // scheduler quantum.
    private static void Spin()
    {
        long start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetTimestamp() - start < Stopwatch.Frequency / 10_000)
        {
        }
    }

    private static PerfSampleFrame Snapshot(double frameMs)
    {
        PerfSample.EndFrame();
        var frame = new PerfSampleFrame();
        PerfSample.SnapshotInto(frame, frameMs);
        return frame;
    }
}
