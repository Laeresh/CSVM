using System;
using System.Text;

namespace CSVM.Utils;

/// <summary>The session simulation's phases, in the order <c>SessionSimulation.StepPhases</c> runs
/// them, then the animation runtime's own physics advance, the tick's other consumer. The enum's
/// names are the printed labels and the phase names the failure report carries.</summary>
public enum SimPhase
{
    CaptureAiAircraft,
    IncomingFire,
    Projectiles,
    HumanAircraft,
    Zeppelins,
    TurretEmplacements,
    Generators,
    SurfaceVehicles,
    CapturedAiAircraft,
    LandingApproaches,
    InstantAction,
    Campaign,
    Radio,
    SmokeScreens,
    BeeperTags,
    AiVoice,
    Versus,
    EndingHold,
    AnimAdvance,
}

/// <summary>The <c>_Process</c> consumers heavy enough to name in <c>--perf</c>'s <c>proc_sites_ms=</c>
/// row, each divided by the window's FRAMES so it reads beside <c>proc_ms</c>; the remainder of
/// <c>proc_ms</c> is every callback not listed here.</summary>
public enum ProcessSite
{
    /// <summary>The projectile pool's tracer, muzzle, impact and smoke sprite pass.</summary>
    Projectiles,

    /// <summary>The animation runtime's frame advance.</summary>
    Anim,

    /// <summary>Every aircraft's presentation: input, camera, gauges, interpolation.</summary>
    Flight,

    /// <summary>Every live particle emitter's spawn and advance.</summary>
    Puffers,

    /// <summary>The session's own frame: clock, weather, flare, edge tiles, pose draw.</summary>
    Session,
}

/// <summary>The <c>--perf</c> split of one physics tick by session-simulation phase, printed as
/// <c>sim_ms=</c> with each phase's banked total divided by the window's TICKS, so it reads beside
/// <c>phys_tick_ms</c> and the gap between the two is the physics engine's own step plus anything on
/// the tick outside the simulation. <c>CapturedAiAircraft</c> is the same span <c>ai_ms</c> banks,
/// on the other divisor.</summary>
public static class SimPhaseCost
{
    private static readonly string[] Labels = Enum.GetNames<SimPhase>();
    private static readonly PhaseCost Row = new(Labels);

    /// <summary>The label a phase prints and reports under.</summary>
    public static string Label(SimPhase phase) => Labels[(int)phase];

    /// <summary>Opens <paramref name="phase"/>'s slot, closing the phase before it.</summary>
    public static void Enter(SimPhase phase) => Row.Open((int)phase);

    /// <summary>Closes the phase in progress: the step's end, or its failure.</summary>
    public static void Leave() => Row.CloseOpen();

    /// <summary>Drains the window into the <c>sim_ms=</c> row, per tick.</summary>
    public static string TakeRow(double ticks) => Row.TakeRow(ticks);

    /// <summary>The same phases in allocated BYTES per tick, the <c>sim_alloc_b=</c> row. Read after
    /// <see cref="TakeRow"/>, which drains the window both rows report.</summary>
    public static string AllocRow() => Row.AllocRow();

    /// <summary>The per-tick figure the last drain printed for <paramref name="phase"/>.</summary>
    public static double LastMs(SimPhase phase) => Row.LastMs((int)phase);

    /// <summary>Drops everything, including a half-open phase.</summary>
    public static void Reset() => Row.Reset();
}

/// <summary>The <c>--perf</c> split of the process pass by its heaviest consumers, banked by a
/// <c>using</c> scope at each callback. Nested scopes are not supported: the one slot open is the
/// one that banks, so keep the scopes at callback level.</summary>
public static class ProcessSiteCost
{
    private static readonly PhaseCost Row = new(Enum.GetNames<ProcessSite>());

    /// <summary>Opens <paramref name="site"/>'s slot for the callback in progress; dispose closes it.
    /// </summary>
    public static Scope Enter(ProcessSite site)
    {
        Row.Open((int)site);
        return default;
    }

    /// <summary>Drains the window into the <c>proc_sites_ms=</c> row, per frame.</summary>
    public static string TakeRow(double frames) => Row.TakeRow(frames);

    /// <summary>The per-frame figure the last drain printed for <paramref name="site"/>.</summary>
    public static double LastMs(ProcessSite site) => Row.LastMs((int)site);

    /// <summary>Drops everything, including a half-open scope.</summary>
    public static void Reset() => Row.Reset();

    /// <summary>The disposable end of <see cref="Enter"/>.</summary>
    public readonly struct Scope : IDisposable
    {
        public void Dispose() => Row.CloseOpen();
    }
}

/// <summary>
/// A row of named <see cref="WallCostBank"/> slots, one per phase of a pass, so a whole-pass term
/// can be split by what the pass ran. <c>proc_ms</c> and <c>phys_tick_ms</c> bracket the whole
/// callback and say a fight costs more, not which consumer grew; this banks each phase into its own
/// slot and prints the row as one <c>name:ms</c> list. Two facades name the rows: the session
/// simulation's phases on the physics tick (<see cref="SimPhaseCost"/>) and the heaviest
/// <c>_Process</c> consumers (<see cref="ProcessSiteCost"/>). A slot open across several callbacks in
/// one frame (every aircraft's presentation, every live emitter) sums them, so the printed figure
/// is what that consumer cost the frame, divided by the frames or ticks the window held.
/// ⚠ Main thread only. Only one slot is open at a time: opening a slot closes whichever is open.
/// </summary>
public sealed class PhaseCost
{
    private readonly WallCostBank[] _slots;
    private readonly string[] _labels;
    private readonly double[] _drained;
    private readonly double[] _drainedMax;
    private readonly StringBuilder _row = new();

    // The same slots in allocated bytes. A phase's wall cost names where a GC pause LANDED, never
    // what earned it: the pause falls into whichever slot happens to be open (verification PERF-34),
    // so the allocation has to be banked per phase too or the reader has only the whole process's
    // rate to go on.
    private readonly long[] _bytes;
    private readonly long[] _bytesMax;
    private readonly double[] _drainedBytes;
    private readonly long[] _drainedBytesMax;
    private long _openedBytes;
    private int _open = -1;

    /// <summary>Builds one bank per label. <paramref name="now"/> is the test-only clock source
    /// <see cref="WallCostBank"/> takes, shared by every slot.</summary>
    public PhaseCost(string[] labels, Func<long>? now = null)
    {
        _labels = labels;
        _slots = new WallCostBank[labels.Length];
        _drained = new double[labels.Length];
        _drainedMax = new double[labels.Length];
        _bytes = new long[labels.Length];
        _bytesMax = new long[labels.Length];
        _drainedBytes = new double[labels.Length];
        _drainedBytesMax = new long[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            _slots[i] = new WallCostBank(labels[i], now);
        }
    }

    /// <summary>The slot open right now, or -1.</summary>
    public int OpenSlot => _open;

    /// <summary>Opens <paramref name="slot"/>, closing the one open before it, so a sequence of phases
    /// needs one call per phase and a final <see cref="CloseOpen"/>.</summary>
    public void Open(int slot)
    {
        CloseOpen();
        _slots[slot].Open();
        _openedBytes = GC.GetAllocatedBytesForCurrentThread();
        _open = slot;
    }

    /// <summary>Closes the open slot, banking its span. A no-op with nothing open.</summary>
    public void CloseOpen()
    {
        if (_open < 0)
        {
            return;
        }
        long grew = GC.GetAllocatedBytesForCurrentThread() - _openedBytes;
        _bytes[_open] += grew;
        if (grew > _bytesMax[_open])
        {
            _bytesMax[_open] = grew;
        }
        _slots[_open].Close();
        _open = -1;
    }

    /// <summary>Drains every slot and formats the row as <c>label:mean/max,label:mean/max</c>: each
    /// slot's banked total divided by <paramref name="per"/> (the window's frames or ticks), then its
    /// longest single span, which is what names the slot a stall landed in. Zero-cost slots print
    /// too, so a reader always finds every phase. The allocation figures are drained here as well,
    /// so <see cref="AllocRow"/> reads the same window this row does.</summary>
    public string TakeRow(double per)
    {
        double divisor = per > 0 ? per : 1;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        _row.Clear();
        for (int i = 0; i < _slots.Length; i++)
        {
            var (ms, maxMs, _, _) = _slots[i].Take();
            _drained[i] = ms / divisor;
            _drainedMax[i] = maxMs;
            _drainedBytes[i] = _bytes[i] / divisor;
            _drainedBytesMax[i] = _bytesMax[i];
            _bytes[i] = 0;
            _bytesMax[i] = 0;
            if (i > 0)
            {
                _row.Append(',');
            }
            _row.Append(_labels[i]).Append(':').Append(_drained[i].ToString("0.000", culture));
            _row.Append('/').Append(maxMs.ToString("0.0", culture));
        }
        return _row.ToString();
    }

    /// <summary>The same slots as <paramref name="TakeRow"/>'s last drain, in allocated BYTES:
    /// <c>label:mean/max</c>, the slot's bytes divided by the window's frames or ticks, then the
    /// worst single span. Call it after <see cref="TakeRow"/>, which is where the window drains.
    /// </summary>
    public string AllocRow()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        _row.Clear();
        for (int i = 0; i < _labels.Length; i++)
        {
            if (i > 0)
            {
                _row.Append(',');
            }
            _row.Append(_labels[i]).Append(':').Append(_drainedBytes[i].ToString("0", culture));
            _row.Append('/').Append(_drainedBytesMax[i].ToString(culture));
        }
        return _row.ToString();
    }

    /// <summary>The per-unit figure the last <see cref="TakeRow"/> printed for <paramref name="slot"/>,
    /// so a test asserts on numbers rather than parsing the row.</summary>
    public double LastMs(int slot) => _drained[slot];

    /// <summary>The longest single span the last <see cref="TakeRow"/> printed for <paramref name="slot"/>.</summary>
    public double LastMaxMs(int slot) => _drainedMax[slot];

    /// <summary>Drops everything in every slot, the open one included.</summary>
    public void Reset()
    {
        foreach (var slot in _slots)
        {
            slot.Reset();
        }
        Array.Clear(_bytes);
        Array.Clear(_bytesMax);
        _open = -1;
    }
}
