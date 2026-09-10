using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace CSVM.Utils;

/// <summary>The GC readout behind <c>--perf</c>: one <c>[perf] gc</c> line per window carrying the
/// pause the process actually spent and how many FINALIZABLE objects died to earn it. Pause per
/// wall second is what a change is judged on, since cutting allocation batches the pause into
/// rarer, longer collections without shrinking the total, and the pause tracks the
/// finalization-promoted count rather than promoted bytes (docs/verification.md PERF-20). Every
/// line carries process uptime, so the world-build settling regime is excluded by dropping the
/// windows below it instead of guessing at a warm-up (PERF-19).</summary>
public sealed class GcTrace : EventListener
{
    /// <summary>Seconds of wall time one reported window covers. Long enough that a settled
    /// collection every 13 to 35 s lands in most windows, short enough to see a regime change.</summary>
    public const double WindowSeconds = 10;

    // Microsoft-Windows-DotNETRuntime's GC keyword, at Informational. Never Verbose: verbose adds
    // GCAllocationTick once per ~100 KB allocated, which costs more than what is being measured.
    private const int GcKeyword = 1;
    private const string RuntimeSourceName = "Microsoft-Windows-DotNETRuntime";
    private const string HeapStatsPrefix = "GCHeapStats";
    private const string FinalizationCountField = "FinalizationPromotedCount";

    private readonly Stopwatch _since = Stopwatch.StartNew();

    // How much of the process this instance missed: it is built on the first --perf frame, which is
    // already past the world build, and the reported uptime is what PERF-19's cutoff is stated in.
    private readonly double _bornAt = UptimeSeconds();

    // Written from the GC's own thread when a heap-stats event lands, read from the frame path.
    private long _finalizable;

    private double _markUpSeconds = UptimeSeconds();
    private long _markFinalizable;
    private long _markPauseTicks;
    private long _markAllocated;
    private int _markGen0;
    private int _markGen1;
    private int _markGen2;

    /// <summary>Emits a window line once the window has elapsed, else returns having read one
    /// stopwatch. Called from the frame path, so it must stay free of allocation until it
    /// reports.</summary>
    public void Tick()
    {
        double up = _bornAt + _since.Elapsed.TotalSeconds;
        double windowSeconds = up - _markUpSeconds;
        if (windowSeconds < WindowSeconds)
        {
            return;
        }
        long finalizable = Interlocked.Read(ref _finalizable);
        long pauseTicks = GC.GetTotalPauseDuration().Ticks;
        long allocated = GC.GetTotalAllocatedBytes(false);
        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);
        double pauseMs = (pauseTicks - _markPauseTicks) / (double)TimeSpan.TicksPerMillisecond;
        double finWindow = finalizable - _markFinalizable;
        double allocMb = (allocated - _markAllocated) / (1024.0 * 1024.0);
        Log.Info("perf", $"gc up_s={up:0.0} win_s={windowSeconds:0.00} pause_ms={pauseMs:0.00} pause_per_s_ms={pauseMs / windowSeconds:0.000} gc0={gen0 - _markGen0} gc1={gen1 - _markGen1} gc2={gen2 - _markGen2} fin={finWindow:0} fin_per_s={finWindow / windowSeconds:0.0} alloc_mb_s={allocMb / windowSeconds:0.000} heap_mb={GC.GetTotalMemory(false) / (1024.0 * 1024.0):0.0}");
        _markUpSeconds = up;
        _markFinalizable = finalizable;
        _markPauseTicks = pauseTicks;
        _markAllocated = allocated;
        _markGen0 = gen0;
        _markGen1 = gen1;
        _markGen2 = gen2;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // ⚠ Touch no field of this class here: the base constructor raises this for every source
        // that already exists, before the derived field initialisers have run.
        if (eventSource.Name == RuntimeSourceName)
        {
            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)GcKeyword);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName is not { } name || !name.StartsWith(HeapStatsPrefix, StringComparison.Ordinal))
        {
            return;
        }
        // Read by payload NAME, not index: the event is versioned and its field order has moved.
        var names = eventData.PayloadNames;
        if (names == null || eventData.Payload == null)
        {
            return;
        }
        for (int i = 0; i < names.Count; i++)
        {
            if (names[i] == FinalizationCountField && eventData.Payload[i] is { } value)
            {
                Interlocked.Add(ref _finalizable, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
        }
    }

    // Read once per instance: a Process handle is far too heavy for the frame path, and the
    // stopwatch above carries the rest.
    private static double UptimeSeconds()
    {
        using var self = Process.GetCurrentProcess();
        return (DateTime.Now - self.StartTime).TotalSeconds;
    }
}
