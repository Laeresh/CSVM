using System;
using System.Diagnostics;

namespace CSVM.Utils;

/// <summary>
/// The wall time a frame spends OUTSIDE every scene-tree callback, between the ends of the
/// <see cref="PhysicsTickCost"/> and <see cref="ProcessPassCost"/> brackets. The gap after a
/// physics tick is the tree's end-of-tick flush (deferred calls, transform notifications) and the
/// physics server's step. The draw signals cut the gap after the process pass in three. They are
/// the same flush with every <c>_Draw</c>, the draw with its present, and the OS event pump. With
/// the two pass terms these sum to the whole frame (verification PERF-40).
/// </summary>
public static class EngineGapCost
{
    private static long _markAt;
    private static Gap _gap;
    private static long _physicsTicks;
    private static long _deferTicks;
    private static long _drawTicks;
    private static long _idleTicks;

    // Which bin the span since the last mark is banked in.
    private enum Gap
    {
        None,
        AfterPhysics,
        AfterProcess,
        InDraw,
        AfterDraw,
    }

    /// <summary>The timestamp source, in <see cref="Stopwatch"/> ticks. A test swaps in a clock it
    /// advances itself, so the bins are asserted by value (docs/verification.md PERF-30).</summary>
    internal static Func<long> Clock { get; set; } = Stopwatch.GetTimestamp;

    /// <summary>Stamps the end of a pass. Called by a tail bracket.</summary>
    public static void MarkTail(bool physics) => Mark(physics ? Gap.AfterPhysics : Gap.AfterProcess);

    /// <summary>Banks the gap since the last mark. Called by a head bracket.</summary>
    public static void MarkHead() => Mark(Gap.None);

    /// <summary>The rendering server's <c>frame_pre_draw</c>: ends the deferred flush.</summary>
    public static void MarkPreDraw() => Mark(Gap.InDraw);

    /// <summary>The rendering server's <c>frame_post_draw</c>: ends the draw.</summary>
    public static void MarkPostDraw() => Mark(Gap.AfterDraw);

    /// <summary>Drains the window in milliseconds: after physics ticks, the deferred flush, the draw,
    /// and the rest after the draw. Without the draw signals the whole post-process gap reads as
    /// the deferred flush.</summary>
    public static (double PhysicsEngineMs, double DeferMs, double DrawMs, double IdleMs) Take()
    {
        double toMs = 1000.0 / Stopwatch.Frequency;
        var taken = (_physicsTicks * toMs, _deferTicks * toMs, _drawTicks * toMs, _idleTicks * toMs);
        _physicsTicks = _deferTicks = _drawTicks = _idleTicks = 0;
        return taken;
    }

    /// <summary>Drops everything, including a standing mark: a session build or teardown, whose
    /// stall belongs to no frame.</summary>
    public static void Reset()
    {
        _markAt = 0;
        _gap = Gap.None;
        _physicsTicks = _deferTicks = _drawTicks = _idleTicks = 0;
    }

    private static void Mark(Gap next)
    {
        long now = Clock();
        if (_markAt != 0)
        {
            long span = now - _markAt;
            switch (_gap)
            {
                case Gap.AfterPhysics:
                    _physicsTicks += span;
                    break;
                case Gap.AfterProcess:
                    _deferTicks += span;
                    break;
                case Gap.InDraw:
                    _drawTicks += span;
                    break;
                case Gap.AfterDraw:
                    _idleTicks += span;
                    break;
            }
        }
        _gap = next;
        _markAt = next == Gap.None ? 0 : now;
    }
}
