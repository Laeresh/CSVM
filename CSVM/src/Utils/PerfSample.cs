using System;
using System.Diagnostics;

namespace CSVM.Utils;

/// <summary>The work a scope can declare itself as. A fixed enum, never a string built per call:
/// the whole point is that opening a scope costs two QPC reads and an array index, with nothing
/// allocated and nothing to look up.
/// <para>The vocabulary is deliberately COARSE — a debris burst, not one chunk; a spawn, not one
/// node. See <see cref="PerfSample"/> for why a fine-grained site is worse than no site at all.
/// A site nothing calls simply never appears in a record; adding one is this enum plus its name in
/// <c>PerfSample.Names</c>, in the same order.</para></summary>
public enum PerfSite
{
    /// <summary>A debris burst coming into existence — the damage lab's reproducible case.</summary>
    DebrisSpawn,

    /// <summary>A part detaching from an aircraft, the other half of that same case.</summary>
    PartDetach,

    /// <summary>An AI aircraft being built and added to the tree.</summary>
    AiSpawn,

    /// <summary>Taking an effect out of its pool.</summary>
    EffectCheckout,

    /// <summary>A pool that had nothing to hand out, so something had to be made.</summary>
    EffectPoolMiss,

    /// <summary>A <c>ShaderMaterial</c> (or any material) built at runtime rather than at load.</summary>
    MaterialCreate,

    /// <summary>A synchronous resource load on the frame path.</summary>
    ResourceLoad,

    /// <summary>A sound being loaded or decoded on the frame path.</summary>
    AudioLoad,
}

/// <summary>The handle a <c>using</c> holds for the length of one leaf scope. A <c>ref struct</c> so
/// the compiler refuses to box it onto the heap or park it in a field: a scope that allocated would
/// change the thing it measures. Disposing it is what records the time.</summary>
public readonly ref struct PerfScope
{
    private readonly long _start;
    private readonly int _site;

    internal PerfScope(int site, long start)
    {
        _site = site;
        _start = start;
    }

    /// <summary>Closes the scope, adding its wall time to the site's total for the frame in
    /// progress. A suppressed scope (one opened inside another) closes without recording, which is
    /// what keeps the per-frame sum from double-counting.</summary>
    public void Dispose() => PerfSample.Close(_site, _start);
}

/// <summary>
/// ambient timed leaf scopes. Any code path can declare that it ran and how
/// long it took — <c>using (PerfSample.Scope(PerfSite.DebrisSpawn)) { … }</c> — without knowing
/// anything about the hitch monitor, the readout, or whether anything is listening. The totals
/// accumulate per site into a preallocated array, are frozen once per frame by
/// <see cref="EndFrame"/>, and are copied into a <see cref="HitchRecord"/> when one fires, so a
/// record says what the frame was DOING rather than only what it cost.
///
/// <para><b>Flat leaves only, and every record carries the remainder.</b> A scope opened inside
/// another is counted as a violation and recorded as nothing, so
/// <c>Σ(sites) + unattributed = frame_ms</c> holds on every frame. This repo has paid for the
/// alternative once already: <see cref="StartupProfile"/>'s standing warning is that a phase must
/// stay a LEAF or the sum silently double-counts. The per-frame version of that failure is worse,
/// because a partially instrumented TREE attributes un-instrumented time to whatever parent
/// encloses it, and it is never fully instrumented — the next feature to land will not add its
/// scope. A flat set of leaves plus a visible remainder cannot lie that way: work with no scope on
/// it shows up as remainder, which is a question, rather than as somebody else's cost, which is an
/// answer that happens to be wrong.</para>
///
/// <para><b>Scopes are coarse-grained only.</b> A debris burst, not one chunk; a spawn, not one
/// node; a pool checkout, not one particle. A scope inside a per-projectile or per-particle loop
/// turns tens of nanoseconds into a real cost and produces an instrument that changes what it
/// measures.</para>
///
/// <para><b>Cost.</b> Two <see cref="Stopwatch.GetTimestamp"/> reads, an array add and a branch:
/// <b>about 60 ns</b> per open+close, measured on the dev machine at 58.8/59.3/62.7 ns over three
/// runs of <c>PerfSampleTests.AScopeCostsFarLessThanTheFrameItMeasures</c> (200 000 iterations
/// each), allocating exactly zero bytes. Six coarse scopes a frame is a third of a microsecond, on
/// a frame that is 16 700 of them. That is what makes it safe to leave on rather than flag-gate,
/// the same reasoning <see cref="StartupProfile"/> runs unconditionally on — and it is also the
/// budget a per-particle scope would blow, since the same 60 ns times a thousand particles is
/// 60 µs of instrument on a frame it was meant to explain.</para>
///
/// <para><b>Ambient statics, main thread only.</b> There is one set of counters for the process,
/// like <see cref="StartupProfile.Current"/>, because the shared code that records has no way to be
/// handed a per-frame accumulator down a call chain that does not know about it. The counters are
/// not synchronised: a scope opened off the main thread races the frame's totals. Nothing here does
/// that today (the loads C9 seeds are synchronous by definition — that is why they are worth
/// timing), and a lock on the frame path would cost more than the measurement.</para>
/// </summary>
public static class PerfSample
{
    // Indexed by PerfSite, so this array's ORDER is the enum's order. Lower-underscore spelling
    // because these names are written into the JSON sidecar as-is; they are compile-time constants
    // from a closed enum, so the sidecar's hand-written JSON still has nothing to escape.
    private static readonly string[] Names =
    {
        "debris_spawn", "part_detach", "ai_spawn", "effect_checkout", "effect_pool_miss",
        "material_create", "resource_load", "audio_load",
    };

    // The frame in progress, and the last frame EndFrame closed. Two buffers rather than one so a
    // record fired at the top of frame N reports frame N-1's work — the frame its own wall cost
    // measures — instead of the handful of scopes that have run since.
    private static readonly double[] CurrentMs = new double[Names.Length];
    private static readonly int[] CurrentCalls = new int[Names.Length];
    private static readonly double[] LastMs = new double[Names.Length];
    private static readonly int[] LastCalls = new int[Names.Length];

    private static int _open;
    private static int _currentViolations;
    private static int _lastViolations;

    /// <summary>How many sites the enum defines; the length of a
    /// <see cref="PerfSampleFrame"/>'s arrays.</summary>
    public static int SiteCount => Names.Length;

    /// <summary>The wire name of a site — what the sidecar and the log line call it.</summary>
    public static string NameOf(PerfSite site) =>
        (uint)site < (uint)Names.Length ? Names[(int)site] : "unknown";

    /// <summary>Opens a leaf scope on <paramref name="site"/>. Hand the result to a <c>using</c>;
    /// the time is recorded when it is disposed.
    /// <para>A scope opened while another is open is SUPPRESSED — it measures nothing and is
    /// counted as a violation on the frame's record — because its time is already inside the
    /// enclosing scope's, and adding both would break the frame's sum. Nesting is a defect to be
    /// found and removed, not a shape this supports.</para></summary>
    public static PerfScope Scope(PerfSite site)
    {
        int i = (int)site;
        if (_open != 0 || (uint)i >= (uint)Names.Length)
        {
            _currentViolations++;
            return new PerfScope(-1, 0);
        }
        _open = 1;
        return new PerfScope(i, Stopwatch.GetTimestamp());
    }

    /// <summary>Closes the frame in progress: its totals become the ones a record snapshots, and
    /// the accumulators start again from zero. Called from <c>Launcher._Process</c> at the same
    /// instant the frame's wall cost is stamped, so the two describe the same window.
    /// <para>A scope still open here has escaped its frame, which is counted as a violation; the
    /// open flag is cleared regardless, so a leak costs one frame's attribution rather than every
    /// frame after it.</para></summary>
    public static void EndFrame()
    {
        Array.Copy(CurrentMs, LastMs, Names.Length);
        Array.Copy(CurrentCalls, LastCalls, Names.Length);
        Array.Clear(CurrentMs);
        Array.Clear(CurrentCalls);
        _lastViolations = _currentViolations + _open;
        _currentViolations = 0;
        _open = 0;
    }

    /// <summary>Drops both frames' totals. Called wherever the frames on either side are not each
    /// other's neighbours — a session build, a teardown — for the same reason
    /// <see cref="HitchMonitor.Rearm"/> drops its baseline there.</summary>
    public static void Reset()
    {
        Array.Clear(CurrentMs);
        Array.Clear(CurrentCalls);
        Array.Clear(LastMs);
        Array.Clear(LastCalls);
        _currentViolations = 0;
        _lastViolations = 0;
        _open = 0;
    }

    /// <summary>Copies the last closed frame's totals into <paramref name="dst"/> and works out
    /// what <paramref name="frameMs"/> did NOT account for.
    /// <para>The remainder is not clamped. Negative means a scope spanned the
    /// <see cref="EndFrame"/> boundary and its time landed on a frame that did not spend it, which
    /// is a defect worth seeing rather than a number worth hiding.</para></summary>
    public static void SnapshotInto(PerfSampleFrame dst, double frameMs)
    {
        Array.Copy(LastMs, dst.Ms, Names.Length);
        Array.Copy(LastCalls, dst.Calls, Names.Length);
        double attributed = 0;
        for (int i = 0; i < Names.Length; i++)
        {
            attributed += LastMs[i];
        }
        dst.AttributedMs = attributed;
        dst.UnattributedMs = frameMs - attributed;
        dst.Violations = _lastViolations;
    }

    // Called by PerfScope.Dispose only. A suppressed scope carries site -1 and records nothing;
    // note it does not clear _open either, since the scope that suppressed it still owns that.
    internal static void Close(int site, long start)
    {
        if (site < 0)
        {
            return;
        }
        CurrentMs[site] += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        CurrentCalls[site]++;
        _open = 0;
    }
}

/// <summary>One frame's attribution: how long each site took, how many times it ran, and the
/// remainder that no site claimed.
/// <para>Mutable fields over an immutable value for the same reason <see cref="HitchRecord"/> has
/// them — one instance per record, refilled in place, so a hitching frame allocates nothing.</para>
/// </summary>
public sealed class PerfSampleFrame
{
    /// <summary>Arrays are <see cref="PerfSample.SiteCount"/> long and indexed by
    /// <see cref="PerfSite"/>.</summary>
    public PerfSampleFrame()
    {
        Ms = new double[PerfSample.SiteCount];
        Calls = new int[PerfSample.SiteCount];
    }

    /// <summary>Milliseconds spent inside each site's scopes, indexed by <see cref="PerfSite"/>.</summary>
    public double[] Ms { get; }

    /// <summary>How many scopes of each site closed in the frame.</summary>
    public int[] Calls { get; }

    /// <summary>The sum of <see cref="Ms"/> — what the frame could name.</summary>
    public double AttributedMs { get; set; }

    /// <summary>The frame's wall cost minus <see cref="AttributedMs"/>: real work with no scope on
    /// it, never an error term.</summary>
    public double UnattributedMs { get; set; }

    /// <summary>Scopes that were suppressed rather than measured — a nested scope, an unknown site,
    /// or one still open when the frame ended. Nonzero means the attribution below it is
    /// incomplete, which is why it travels with the record.</summary>
    public int Violations { get; set; }

    /// <summary>Zeroes everything, so a record that never took a snapshot cannot show the previous
    /// one's work.</summary>
    public void Clear()
    {
        Array.Clear(Ms);
        Array.Clear(Calls);
        AttributedMs = 0;
        UnattributedMs = 0;
        Violations = 0;
    }

    /// <summary>Copies <paramref name="src"/> field by field into this frame — no allocation, so
    /// the sidecar can queue a record's attribution alongside the rest of it.</summary>
    public void CopyFrom(PerfSampleFrame src)
    {
        Array.Copy(src.Ms, Ms, Ms.Length);
        Array.Copy(src.Calls, Calls, Calls.Length);
        AttributedMs = src.AttributedMs;
        UnattributedMs = src.UnattributedMs;
        Violations = src.Violations;
    }
}
