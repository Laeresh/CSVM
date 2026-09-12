using System;

namespace CSVM.Utils;

/// <summary>The engine's per-frame counters as one frame saw them, unaveraged: the four cost terms
/// in milliseconds and the four counts. Sampled by the caller (they are Godot reads) and handed to
/// <see cref="HitchMonitor.Tick"/> by value, so the monitor itself stays engine-free.</summary>
public readonly record struct FrameCounters(
    double ScriptMs,
    double RenderCpuMs,
    double GpuMs,
    double PhysicsMs,
    long Draws,
    long Prims,
    long Nodes,
    long MemBytes);

/// <summary>One frame in the ring buffer: its wall cost and the split behind it. Costs only, no
/// counts, because the ring exists to show a hitch's run-up and the counts are a record-level
/// term.</summary>
public readonly record struct FrameSample(
    double FrameMs,
    double ScriptMs,
    double RenderCpuMs,
    double GpuMs,
    double PhysicsMs);

/// <summary>
/// The always-on frame-hitch detector: every rendered frame is compared against the cost of its
/// recent neighbours, and a frame that costs far more than they did has a record assembled for it
/// describing what the frame was doing. Nothing is logged from here; the record is held for
/// whoever asks (the sidecar writer, the on-screen readout), so a clean run is silent. Trigger
/// formula and TUNE constants: docs/org/hitch.md; vsync interaction: docs/verification.md
/// PERF-12/PERF-13.
/// ⚠ Feed this a raw wall-clock timestamp, never Godot's <c>delta</c>, which is post-processed
/// and does not describe the same frame as the counters read here.
/// ⚠ Nothing allocates after construction, including on a hitching frame. <see cref="Last"/> is
/// overwritten by the next trigger, so a consumer that needs to keep one copies it.
/// </summary>
public sealed class HitchMonitor
{
    /// <summary>How many times the rolling median a frame must cost to trip the relative term.
    /// TUNE: named in conversation on 2026-08-14 and evidenced by nothing.</summary>
    public const float MedianMultipleDefault = 4f;

    /// <summary>The absolute floor a frame must clear whatever the median says, in milliseconds.
    /// At the 60 Hz cap that is about two and a half dropped frames, which is where a freeze starts
    /// being something you feel rather than something you measure. TUNE.</summary>
    public const float FloorMsDefault = 40f;

    /// <summary>How many recent frames the rolling median is taken over. Two seconds at the 60 Hz
    /// cap, so the baseline tracks the situation rather than the second. TUNE.</summary>
    public const int BaselineFramesDefault = 120;

    /// <summary>How many preceding frames a record carries, so a hitch's run-up is visible and not
    /// just its peak. TUNE.</summary>
    public const int RingFramesDefault = 120;

    /// <summary>How long after a session build the detector stays quiet, in milliseconds. Startup
    /// legitimately stalls the frame loop and <see cref="StartupProfile"/> already covers that
    /// ground; without this every run would open with a record nobody wants. TUNE.</summary>
    public const float GraceMsDefault = 2000f;

    private readonly double _medianMultiple;
    private readonly double _floorMs;
    private readonly double _graceMs;
    private readonly int _baselineFrames;
    private readonly int _ringFrames;

    // The rolling window in arrival order, plus the same values kept sorted for the median. Both are
    // written every frame; _sorted is maintained by insert/remove rather than re-sorted.
    private readonly double[] _window;
    private readonly double[] _sorted;
    private readonly FrameSample[] _ring;
    private readonly HitchRecord _last;

    private int _windowCount;
    private int _windowNext;
    private int _ringCount;
    private int _ringNext;

    // Last frame's absolute counters, for the deltas a record reports. _hasPrevious is false for the
    // first frame after a rearm, whose deltas would otherwise be measured against another session.
    private FrameCounters _previous;
    private int _prevGc0, _prevGc1, _prevGc2;
    private long _prevAllocated;
    private bool _hasPrevious;

    private double _sinceRearmMs;
    private long _frames;

    /// <summary>Builds a monitor over the <c>hitchMonitor.*</c> config keys, each falling back to
    /// its <c>Default</c> const. Constructing it is what registers those keys, so this has to
    /// happen before <c>--dump-config</c> writes its template.</summary>
    public HitchMonitor()
        : this(
            Config.GetFloat("hitchMonitor.medianMultiple", MedianMultipleDefault),
            Config.GetFloat("hitchMonitor.floorMs", FloorMsDefault),
            Config.GetInt("hitchMonitor.baselineFrames", BaselineFramesDefault),
            Config.GetInt("hitchMonitor.ringFrames", RingFramesDefault),
            Config.GetFloat("hitchMonitor.graceMs", GraceMsDefault))
    {
    }

    /// <summary>Builds a monitor over explicit constants, which is what the unit tests use.</summary>
    public HitchMonitor(double medianMultiple, double floorMs, int baselineFrames, int ringFrames,
        double graceMs)
    {
        // Clamped rather than rejected: these come from a hand-edited config.json, and a zero-length
        // window would divide by nothing while a negative floor would trip on every frame.
        _medianMultiple = Math.Max(medianMultiple, 0);
        _floorMs = Math.Max(floorMs, 0);
        _graceMs = Math.Max(graceMs, 0);
        _baselineFrames = Math.Max(baselineFrames, 1);
        _ringFrames = Math.Max(ringFrames, 1);
        _window = new double[_baselineFrames];
        _sorted = new double[_baselineFrames];
        _ring = new FrameSample[_ringFrames];
        _last = new HitchRecord(_ringFrames);
    }

    /// <summary>How many frames have tripped the trigger since the process started.</summary>
    public int HitchCount { get; private set; }

    /// <summary>How many frames <see cref="Tick"/> has been fed so far, the same counter
    /// <see cref="HitchRecord.Frame"/> reports, exposed one call early so a caller can act on
    /// "the next <see cref="Tick"/> will be frame N" (the <c>--hitch-inject=</c> synthetic
    /// stall). Never reset by <see cref="Rearm"/>: it counts from process start,
    /// same as <see cref="HitchRecord.Frame"/> itself.</summary>
    public long FrameCount => _frames;

    /// <summary>The rolling median in milliseconds, or 0 while the window is still filling.</summary>
    public double BaselineMs { get; private set; }

    /// <summary>What a frame had to beat to trip, in milliseconds, as of the last
    /// <see cref="Tick"/>. The readout draws this as a line on its frame-time strip.</summary>
    public double ThresholdMs { get; private set; }

    /// <summary>The most recent hitch, meaningful once <see cref="HitchCount"/> is nonzero.
    /// ⚠ One preallocated instance, overwritten by the next trigger, so copy what you need.</summary>
    public HitchRecord Last => _last;

    /// <summary>Ring capacity: how many entries <see cref="CopyRing"/> can return at most, the same
    /// depth <see cref="Fill"/> copies into a triggered record's <see cref="HitchRecord.Ring"/>.
    /// The frame-time strip sizes its own buffer off this once, at build.</summary>
    public int RingFrames => _ringFrames;

    /// <summary>Copies the live ring buffer into <paramref name="destination"/>, oldest first,
    /// unlike <see cref="Last"/>'s <c>Ring</c>, which only advances on a trigger, this reflects
    /// every <see cref="Tick"/>. A shorter <paramref name="destination"/> gets the most recent
    /// that many entries, not the oldest. Returns how many entries were written.</summary>
    public int CopyRing(Span<FrameSample> destination)
    {
        int count = Math.Min(_ringCount, destination.Length);
        int oldest = _ringCount == _ringFrames ? _ringNext : 0;
        int skip = _ringCount - count;
        for (int i = 0; i < count; i++)
        {
            destination[i] = _ring[(oldest + skip + i) % _ringFrames];
        }
        return count;
    }

    /// <summary>Restarts the grace window and drops the rolling baseline and the ring. Called after
    /// anything that legitimately stalls the frame loop and leaves the frames on either side
    /// incomparable: a session build, a teardown back to the launchscreen.</summary>
    public void Rearm()
    {
        _sinceRearmMs = 0;
        _windowCount = 0;
        _windowNext = 0;
        _ringCount = 0;
        _ringNext = 0;
        _hasPrevious = false;
        BaselineMs = 0;
        ThresholdMs = 0;
    }

    /// <summary>Feeds one rendered frame. Returns true on the frame a hitch was detected, with
    /// <see cref="Last"/> describing it.</summary>
    /// <param name="frameMs">The frame's wall cost. Wall time, never sim time: an instrument that
    /// freezes with the thing it measures reports nothing.</param>
    /// <param name="counters">The engine's per-frame counters as this frame saw them, unaveraged.</param>
    public bool Tick(double frameMs, in FrameCounters counters)
    {
        // A NaN would survive into the sorted mirror and silently break every later comparison, so
        // it is squashed at the door rather than guarded against everywhere below.
        if (double.IsNaN(frameMs) || frameMs < 0)
        {
            frameMs = 0;
        }
        _frames++;

        // The median is taken over the PRECEDING frames: this frame is judged against its
        // neighbours, then joins them. A half-full window is not yet an estimate of anything, so
        // until it fills the floor alone governs.
        BaselineMs = _windowCount == _baselineFrames ? Median() : 0;
        ThresholdMs = Math.Max(_medianMultiple * BaselineMs, _floorMs);
        bool tripped = _sinceRearmMs >= _graceMs && frameMs > ThresholdMs;

        _ring[_ringNext] = new FrameSample(frameMs, counters.ScriptMs, counters.RenderCpuMs,
            counters.GpuMs, counters.PhysicsMs);
        _ringNext = (_ringNext + 1) % _ringFrames;
        if (_ringCount < _ringFrames)
        {
            _ringCount++;
        }

        int gc0 = GC.CollectionCount(0);
        int gc1 = GC.CollectionCount(1);
        int gc2 = GC.CollectionCount(2);
        // The imprecise overload on purpose: it sums the per-thread allocation contexts without
        // stopping anything, which is what makes it callable every frame.
        long allocated = GC.GetTotalAllocatedBytes(false);
        if (tripped)
        {
            HitchCount++;
            Fill(frameMs, counters, gc0, gc1, gc2, allocated);
        }

        PushBaseline(frameMs);
        _previous = counters;
        _prevGc0 = gc0;
        _prevGc1 = gc1;
        _prevGc2 = gc2;
        _prevAllocated = allocated;
        _hasPrevious = true;
        _sinceRearmMs += frameMs;
        return tripped;
    }

    // Writes the record in place. Deltas read zero on the first frame after a rearm, where the
    // previous frame belongs to another session and subtracting it would invent a spike.
    private void Fill(double frameMs, in FrameCounters counters, int gc0, int gc1, int gc2,
        long allocated)
    {
        _last.Frame = _frames;
        _last.FrameMs = frameMs;
        _last.BaselineMs = BaselineMs;
        _last.ThresholdMs = ThresholdMs;
        _last.ScriptMs = counters.ScriptMs;
        _last.RenderCpuMs = counters.RenderCpuMs;
        _last.GpuMs = counters.GpuMs;
        _last.PhysicsMs = counters.PhysicsMs;
        _last.Draws = counters.Draws;
        _last.Prims = counters.Prims;
        _last.Nodes = counters.Nodes;
        _last.MemBytes = counters.MemBytes;
        _last.DrawsDelta = _hasPrevious ? counters.Draws - _previous.Draws : 0;
        _last.PrimsDelta = _hasPrevious ? counters.Prims - _previous.Prims : 0;
        _last.NodesDelta = _hasPrevious ? counters.Nodes - _previous.Nodes : 0;
        _last.MemBytesDelta = _hasPrevious ? counters.MemBytes - _previous.MemBytes : 0;
        _last.Gc0Delta = _hasPrevious ? gc0 - _prevGc0 : 0;
        _last.Gc1Delta = _hasPrevious ? gc1 - _prevGc1 : 0;
        _last.Gc2Delta = _hasPrevious ? gc2 - _prevGc2 : 0;
        _last.AllocatedBytesDelta = _hasPrevious ? allocated - _prevAllocated : 0;

        // Read ambiently, taken here rather than by the caller so a record can never carry a
        // stale frame's attribution.
        PerfSample.SnapshotInto(_last.Samples, frameMs);

        // Oldest first, ending with the hitching frame itself, so a consumer reads the run-up left
        // to right without knowing where the ring's write head is.
        int oldest = _ringCount == _ringFrames ? _ringNext : 0;
        for (int i = 0; i < _ringCount; i++)
        {
            _last.Ring[i] = _ring[(oldest + i) % _ringFrames];
        }
        _last.RingCount = _ringCount;
    }

    // A TRUE median, never a mean: a mean would be dragged up by the hitch it just saw and would
    // hide the next one. See verification.md PERF-13 for the vsync case this degenerates into.
    private double Median()
    {
        int mid = _windowCount / 2;
        return (_windowCount & 1) == 1 ? _sorted[mid] : 0.5 * (_sorted[mid - 1] + _sorted[mid]);
    }

    private void PushBaseline(double ms)
    {
        if (_windowCount == _baselineFrames)
        {
            RemoveSorted(_window[_windowNext]);
            _windowCount--;
        }
        _window[_windowNext] = ms;
        _windowNext = (_windowNext + 1) % _baselineFrames;
        InsertSorted(ms);
        _windowCount++;
    }

    private void InsertSorted(double ms)
    {
        int at = LowerBound(ms);
        Array.Copy(_sorted, at, _sorted, at + 1, _windowCount - at);
        _sorted[at] = ms;
    }

    private void RemoveSorted(double ms)
    {
        // The value is known to be in the window, and every slot holding it holds the same double,
        // so the first match is as good as any other.
        int at = LowerBound(ms);
        Array.Copy(_sorted, at + 1, _sorted, at, _windowCount - at - 1);
    }

    // Index of the first slot holding a value >= ms. Hand-rolled rather than Array.BinarySearch so
    // nothing on the frame path can pick up the boxing non-generic overload.
    private int LowerBound(double ms)
    {
        int lo = 0;
        int hi = _windowCount;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_sorted[mid] < ms)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }
}

/// <summary>What a hitching frame was doing: its unaveraged cost split, how far past the trigger it
/// was, what the counts and the GC did relative to the frame before it, and the frames leading up
/// to it. Mutable fields over an immutable value because there is exactly one of these per monitor
/// and it is refilled in place; allocating on a hitching frame would be an allocation at the worst
/// possible moment.</summary>
public sealed class HitchRecord
{
    /// <param name="ringFrames">Ring depth, preallocated once.</param>
    public HitchRecord(int ringFrames)
    {
        Ring = new FrameSample[ringFrames];
    }

    /// <summary>The preceding frames, oldest first, the last entry being the hitching frame.
    /// Only the first <see cref="RingCount"/> entries are meaningful.</summary>
    public FrameSample[] Ring { get; }

    /// <summary>What the frame could NAME: per-site times from the scopes that ran in it, plus the
    /// remainder no scope claimed. <see cref="PerfSampleFrame.AttributedMs"/>
    /// plus <see cref="PerfSampleFrame.UnattributedMs"/> is <see cref="FrameMs"/> by
    /// construction.</summary>
    public PerfSampleFrame Samples { get; } = new();

    /// <summary>The monitor's own rendered-frame counter, which counts from process start and is
    /// not the sim frame.</summary>
    public long Frame { get; set; }

    /// <summary>The frame's wall cost in milliseconds.</summary>
    public double FrameMs { get; set; }

    /// <summary>The rolling median it was judged against, 0 while the window was still filling.</summary>
    public double BaselineMs { get; set; }

    /// <summary>What it had to beat: <c>max(multiple x baseline, floor)</c>.</summary>
    public double ThresholdMs { get; set; }

    /// <summary>Godot's <c>TIME_PROCESS</c> for the frame, in milliseconds.</summary>
    public double ScriptMs { get; set; }

    /// <summary>The viewport's measured render CPU time for the frame, in milliseconds.</summary>
    public double RenderCpuMs { get; set; }

    /// <summary>The viewport's measured render GPU time for the frame, in milliseconds.</summary>
    public double GpuMs { get; set; }

    /// <summary>Godot's <c>TIME_PHYSICS_PROCESS</c> for the frame, in milliseconds.</summary>
    public double PhysicsMs { get; set; }

    /// <summary>Draw calls in the frame.</summary>
    public long Draws { get; set; }

    /// <summary>Primitives in the frame.</summary>
    public long Prims { get; set; }

    /// <summary>Live nodes at the frame.</summary>
    public long Nodes { get; set; }

    /// <summary>Static memory in use at the frame, in bytes.</summary>
    public long MemBytes { get; set; }

    /// <summary>Draw calls relative to the previous frame.</summary>
    public long DrawsDelta { get; set; }

    /// <summary>Primitives relative to the previous frame.</summary>
    public long PrimsDelta { get; set; }

    /// <summary>Live nodes relative to the previous frame, the count that says something came
    /// into existence.</summary>
    public long NodesDelta { get; set; }

    /// <summary>Static memory relative to the previous frame, in bytes.</summary>
    public long MemBytesDelta { get; set; }

    /// <summary>Gen-0 collections during the frame.</summary>
    public int Gc0Delta { get; set; }

    /// <summary>Gen-1 collections during the frame.</summary>
    public int Gc1Delta { get; set; }

    /// <summary>Gen-2 collections during the frame, the one worth landing on a hitching frame.</summary>
    public int Gc2Delta { get; set; }

    /// <summary>Bytes allocated during the frame, from the cheap imprecise counter.</summary>
    public long AllocatedBytesDelta { get; set; }

    /// <summary>How many <see cref="Ring"/> entries are meaningful.</summary>
    public int RingCount { get; set; }
}
