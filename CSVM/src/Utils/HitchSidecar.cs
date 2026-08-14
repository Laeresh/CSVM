using System;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// PLAN-perf-hitches B6: the always-on detector's write path. Every tripped <see cref="HitchMonitor"/>
/// record gets ONE human-readable line in the <c>perf</c> log category and ONE JSON line in a
/// sidecar sharing the main log's <c>&lt;mode&gt;-&lt;stamp&gt;</c> stem — but never inline on the
/// hitching frame itself: both a string interpolation and a file write are avoidable allocation-heavy
/// work, and doing either right after a hitch is the worst possible moment. A tripped record is
/// instead copied (no allocation — every queue slot's own <see cref="HitchRecord"/> and its
/// <c>Ring</c> array are preallocated once, at construction) into a small ring, and the whole ring is
/// drained a few seconds later, once the frames that followed the hitch have had time to settle.
///
/// <para><b>Crash durability.</b> The sidecar's file is opened once, for the process's whole life,
/// with <see cref="Log"/>'s own recipe: UTF-8 WITH a BOM (PowerShell 5.1 reads a BOM-less file as
/// ANSI) and <c>AutoFlush</c>, so a line reaches disk the instant it is written. A record therefore
/// survives a crash from the moment it is FLUSHED, not from the moment it TRIPPED: the flush interval
/// is the loss bound on an abnormal exit (a <c>Stop-Process -Force</c> mid-flight loses at most its
/// queued-but-unflushed tail), and nothing more elaborate — a signal handler racing the crash it is
/// meant to survive — is worth building for that bound. <c>Launcher</c> flushes before
/// <c>HitchMonitor.Rearm</c> on both a session build and a teardown, so an ordinary relaunch never
/// waits out the interval.</para>
/// </summary>
public sealed class HitchSidecar
{
    /// <summary>How many tripped records can queue before the oldest is dropped. TUNE: a hitch
    /// storm faster than this is itself worth knowing about (reported as a dropped count), not
    /// something to buffer around indefinitely.</summary>
    public const int QueueDepthDefault = 8;

    /// <summary>How long a queued record waits before it is written, in seconds. TUNE: long enough
    /// that the frames right after a hitch — which may still be elevated — are not asked to do the
    /// write work too.</summary>
    public const float FlushSecondsDefault = 3f;

    private readonly HitchRecord[] _queue;
    private readonly int _queueDepth;
    private readonly double _flushMs;
    private readonly StreamWriter? _writer;

    private int _queueCount;
    private int _queueHead;
    private double _sinceFlushMs;
    private int _droppedCount;

    /// <summary>Builds a sidecar over the <c>hitchSidecar.*</c> config keys, each falling back to
    /// its <c>Default</c> const. Constructing it is what registers those keys, so this has to
    /// happen before <c>--dump-config</c> writes its template — and after <see cref="Log.Open"/>,
    /// since the sidecar's path is derived from <see cref="Log.SinkPath"/>.</summary>
    /// <param name="logPath">The open log file's path (<see cref="Log.SinkPath"/>); the sidecar
    /// replaces its <c>.log</c> suffix with <c>.hitches.jsonl</c>.</param>
    /// <param name="ringFrames">Ring depth per queued record — matched to the <see cref="HitchMonitor"/>
    /// this sidecar drains, so a queue slot's <c>Ring</c> array and a record's are the same length.</param>
    public HitchSidecar(string logPath, int ringFrames)
        : this(
            logPath,
            ringFrames,
            Config.GetInt("hitchSidecar.queueDepth", QueueDepthDefault),
            Config.GetFloat("hitchSidecar.flushSeconds", FlushSecondsDefault))
    {
    }

    /// <summary>Builds a sidecar over explicit constants, which is what the unit tests use.</summary>
    public HitchSidecar(string logPath, int ringFrames, int queueDepth, float flushSeconds)
    {
        _queueDepth = Math.Max(queueDepth, 1);
        _flushMs = Math.Max(flushSeconds, 0) * 1000.0;
        _queue = new HitchRecord[_queueDepth];
        for (int i = 0; i < _queueDepth; i++)
        {
            _queue[i] = new HitchRecord(ringFrames);
        }

        string jsonPath = Path.ChangeExtension(logPath, null) + ".hitches.jsonl";
        JsonPath = jsonPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            _writer = new StreamWriter(
                new FileStream(jsonPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read),
                new UTF8Encoding(true))
            {
                AutoFlush = true,
            };
        }
        catch (Exception e)
        {
            // Best-effort, same policy as Log.Open: a session must never fail to launch because
            // .scratch/logs turned out to be unwritable. A clean run with no sidecar reads as
            // "instrument unavailable", never as "no hitches" (LOG-1).
            GD.PrintErr($"ERROR [perf] hitch sidecar unavailable path={jsonPath} error={e.GetType().Name}: {e.Message}");
            _writer = null;
        }
    }

    /// <summary>The sidecar's own path, whether or not it opened.</summary>
    public string JsonPath { get; }

    /// <summary>Whether the file opened. False means every <see cref="Enqueue"/>/<see cref="Tick"/>
    /// is a no-op — a write failure degrades the instrument, it does not crash the session.</summary>
    public bool IsOpen => _writer != null;

    /// <summary>Queues a tripped record for the next flush. Copies every scalar field and the ring
    /// buffer (<see cref="Array.Copy"/> into a preallocated slot, so nothing here allocates) rather
    /// than holding a reference to <see cref="HitchMonitor.Last"/>, which the monitor overwrites on
    /// the very next trip. Drops the OLDEST unflushed record once the queue is full — the same
    /// drop-oldest policy <see cref="HitchMonitor"/>'s own ring uses — and counts it, so a hitch
    /// burst that outruns the flush interval reads as a reported gap, never a silent one.</summary>
    public void Enqueue(HitchRecord record)
    {
        if (_writer == null)
        {
            return;
        }
        if (_queueCount == 0)
        {
            // The wait clock starts with the first record landing in an otherwise-empty queue.
            _sinceFlushMs = 0;
        }
        int slot;
        if (_queueCount < _queueDepth)
        {
            slot = (_queueHead + _queueCount) % _queueDepth;
            _queueCount++;
        }
        else
        {
            slot = _queueHead;
            _queueHead = (_queueHead + 1) % _queueDepth;
            _droppedCount++;
        }
        CopyInto(_queue[slot], record);
    }

    /// <summary>Feeds one frame's wall cost — the same value <see cref="HitchMonitor.Tick"/> was
    /// just fed — and flushes the queue once <c>hitchSidecar.flushSeconds</c> has passed since the
    /// oldest queued record landed. A no-op on an empty queue, so an ordinary clean run costs one
    /// branch a frame.</summary>
    public void Tick(double frameMs)
    {
        if (_writer == null || _queueCount == 0)
        {
            return;
        }
        _sinceFlushMs += frameMs;
        if (_sinceFlushMs >= _flushMs)
        {
            Flush();
        }
    }

    /// <summary>Drains every queued record right now: one <c>[perf] hitch …</c> line plus one JSON
    /// line each, oldest first. Called from <see cref="Tick"/> on the flush interval, and from
    /// <c>Launcher</c> before a session build or teardown rearms <see cref="HitchMonitor"/>, so nothing
    /// queued at the moment of a legitimate stall waits out the interval.</summary>
    public void Flush()
    {
        if (_writer == null || _queueCount == 0)
        {
            return;
        }
        for (int i = 0; i < _queueCount; i++)
        {
            var r = _queue[(_queueHead + i) % _queueDepth];
            WriteLogLine(r);
            WriteJsonLine(r);
        }
        if (_droppedCount > 0)
        {
            Log.Warn("perf", $"hitch sidecar queue overflowed dropped={_droppedCount} depth={_queueDepth} — raise hitchSidecar.queueDepth or lower hitchSidecar.flushSeconds");
            _droppedCount = 0;
        }
        _queueCount = 0;
        _queueHead = 0;
        _sinceFlushMs = 0;
    }

    // Field-by-field, not a record `with` copy: HitchRecord is a mutable sealed class (one instance
    // per HitchMonitor, refilled in place) precisely so a hitching frame never allocates one, and
    // that reasoning applies here too — dst is one of this sidecar's own preallocated queue slots.
    private static void CopyInto(HitchRecord dst, HitchRecord src)
    {
        dst.Frame = src.Frame;
        dst.FrameMs = src.FrameMs;
        dst.BaselineMs = src.BaselineMs;
        dst.ThresholdMs = src.ThresholdMs;
        dst.ScriptMs = src.ScriptMs;
        dst.RenderCpuMs = src.RenderCpuMs;
        dst.GpuMs = src.GpuMs;
        dst.PhysicsMs = src.PhysicsMs;
        dst.Draws = src.Draws;
        dst.Prims = src.Prims;
        dst.Nodes = src.Nodes;
        dst.MemBytes = src.MemBytes;
        dst.DrawsDelta = src.DrawsDelta;
        dst.PrimsDelta = src.PrimsDelta;
        dst.NodesDelta = src.NodesDelta;
        dst.MemBytesDelta = src.MemBytesDelta;
        dst.Gc0Delta = src.Gc0Delta;
        dst.Gc1Delta = src.Gc1Delta;
        dst.Gc2Delta = src.Gc2Delta;
        dst.AllocatedBytesDelta = src.AllocatedBytesDelta;
        dst.RingCount = src.RingCount;
        Array.Copy(src.Ring, dst.Ring, src.RingCount);
    }

    // Mirrors ReportPerf's grammar (flat key=value, ms terms already in ms, bytes shown as MB) so
    // the two report families read the same way side by side in the perf category.
    private static void WriteLogLine(HitchRecord r)
    {
        Log.Info("perf", $"hitch frame={r.Frame} frame_ms={r.FrameMs:0.00} baseline_ms={r.BaselineMs:0.00} threshold_ms={r.ThresholdMs:0.00} script_ms={r.ScriptMs:0.00} render_cpu_ms={r.RenderCpuMs:0.00} gpu_ms={r.GpuMs:0.00} physics_ms={r.PhysicsMs:0.00} draws={r.Draws} prims={r.Prims} nodes={r.Nodes} mem_mb={r.MemBytes / (1024.0 * 1024.0):0.00} draws_delta={r.DrawsDelta} prims_delta={r.PrimsDelta} nodes_delta={r.NodesDelta} mem_delta_mb={r.MemBytesDelta / (1024.0 * 1024.0):0.00} gc0_delta={r.Gc0Delta} gc1_delta={r.Gc1Delta} gc2_delta={r.Gc2Delta} alloc_delta_mb={r.AllocatedBytesDelta / (1024.0 * 1024.0):0.00}");
    }

    // Hand-written, not a library: a HitchRecord is all-numeric (Frame/counts/ms terms, and the
    // Ring's FrameSamples are the same), so there is no string field that would ever need escaping.
    // string.Create(IFormatProvider, …) — not plain string interpolation — is what keeps every float
    // invariant; a raw $"..." would format under CurrentCulture instead (16,667 on a German machine).
    private void WriteJsonLine(HitchRecord r)
    {
        string[] ringParts = new string[r.RingCount];
        for (int i = 0; i < r.RingCount; i++)
        {
            var f = r.Ring[i];
            ringParts[i] = string.Create(CultureInfo.InvariantCulture,
                $"{{\"frame_ms\":{f.FrameMs:0.000},\"script_ms\":{f.ScriptMs:0.000},\"render_cpu_ms\":{f.RenderCpuMs:0.000},\"gpu_ms\":{f.GpuMs:0.000},\"physics_ms\":{f.PhysicsMs:0.000}}}");
        }
        string json = string.Create(CultureInfo.InvariantCulture,
            $"{{\"frame\":{r.Frame},\"frame_ms\":{r.FrameMs:0.000},\"baseline_ms\":{r.BaselineMs:0.000},\"threshold_ms\":{r.ThresholdMs:0.000},\"script_ms\":{r.ScriptMs:0.000},\"render_cpu_ms\":{r.RenderCpuMs:0.000},\"gpu_ms\":{r.GpuMs:0.000},\"physics_ms\":{r.PhysicsMs:0.000},\"draws\":{r.Draws},\"prims\":{r.Prims},\"nodes\":{r.Nodes},\"mem_bytes\":{r.MemBytes},\"draws_delta\":{r.DrawsDelta},\"prims_delta\":{r.PrimsDelta},\"nodes_delta\":{r.NodesDelta},\"mem_bytes_delta\":{r.MemBytesDelta},\"gc0_delta\":{r.Gc0Delta},\"gc1_delta\":{r.Gc1Delta},\"gc2_delta\":{r.Gc2Delta},\"allocated_bytes_delta\":{r.AllocatedBytesDelta},\"ring\":[{string.Join(",", ringParts)}]}}");
        _writer!.WriteLine(json);
    }
}
