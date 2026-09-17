using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// <see cref="HitchMonitor"/>'s write path: every tripped record gets one human-readable line in
/// the <c>perf</c> log category and one JSON line in a sidecar sharing the main log's stem, never
/// inline on the hitching frame, since a string interpolation and a file write are avoidable
/// allocation-heavy work at the worst possible moment. A record is copied (no allocation; every
/// queue slot is preallocated at construction) into a small ring, drained a few seconds later.
/// Line grammar and the JSON shape: docs/org/hitch.md.
/// ⚠ Crash durability is bounded by the flush interval, not by the trip. A record survives a
/// crash only once flushed; <c>Launcher</c> flushes before every <see cref="HitchMonitor.Rearm"/>
/// so an ordinary relaunch never waits out the interval.
/// </summary>
public sealed class HitchSidecar
{
    /// <summary>How many tripped records can queue before the oldest is dropped. TUNE: a hitch
    /// storm faster than this is itself worth knowing about (reported as a dropped count), not
    /// something to buffer around indefinitely. 16 covers a compound storm at the BL-355/356 scale
    /// (31 trips in ~7 s at the controls, frames 3373-4243) with ~2x margin while keeping the
    /// dropped-count backstop honest.</summary>
    public const int QueueDepthDefault = 16;

    /// <summary>How long a queued record waits before it is written, in seconds. TUNE: long enough
    /// that the frames right after a hitch, which may still be elevated, are not asked to do the
    /// write work too. 2 s (down from 3) shrinks the crash/relaunch loss bound and drains a storm
    /// sooner; still well past the write-off-the-hitching-frame intent.</summary>
    public const float FlushSecondsDefault = 2f;

    private readonly HitchRecord[] _queue;
    private readonly int _queueDepth;
    private readonly double _flushMs;
    private readonly StreamWriter? _writer;

    private int _queueCount;
    private int _queueHead;
    private double _sinceFlushMs;
    private int _droppedCount;

    /// <summary>Builds a sidecar over the <c>hitchSidecar.*</c> config keys. Must construct after
    /// <see cref="Log.Open"/>, since the sidecar's path derives from <see cref="Log.SinkPath"/>.</summary>
    /// <param name="logPath">The open log file's path; the sidecar replaces its <c>.log</c> suffix
    /// with <c>.hitches.jsonl</c>.</param>
    /// <param name="ringFrames">Ring depth per queued record, matched to <see cref="HitchMonitor"/>.</param>
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
            Log.Error("perf", $"hitch sidecar unavailable path={jsonPath} error={e.GetType().Name}: {e.Message}");
            _writer = null;
        }
    }

    /// <summary>The sidecar's own path, whether or not it opened.</summary>
    public string JsonPath { get; }

    /// <summary>Whether the file opened. False means every <see cref="Enqueue"/>/<see cref="Tick"/>
    /// is a no-op, a write failure degrades the instrument, it does not crash the session.</summary>
    public bool IsOpen => _writer != null;

    /// <summary>Queues a tripped record for the next flush. Copies every scalar field and the ring
    /// buffer (<see cref="Array.Copy"/> into a preallocated slot, so nothing here allocates) rather
    /// than holding a reference to <see cref="HitchMonitor.Last"/>, which the monitor overwrites on
    /// the very next trip. Drops the OLDEST unflushed record once the queue is full, the same
    /// drop-oldest policy <see cref="HitchMonitor"/>'s own ring uses, and counts it, so a hitch
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

    /// <summary>Feeds one frame's wall cost, the same value <see cref="HitchMonitor.Tick"/> was
    /// just fed, and flushes the queue once <c>hitchSidecar.flushSeconds</c> has passed since the
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
            Log.Warn("perf", $"hitch sidecar queue overflowed dropped={_droppedCount} depth={_queueDepth}, raise hitchSidecar.queueDepth or lower hitchSidecar.flushSeconds");
            _droppedCount = 0;
        }
        _queueCount = 0;
        _queueHead = 0;
        _sinceFlushMs = 0;
    }

    // Field-by-field, not a record `with` copy: HitchRecord is a mutable sealed class (one instance
    // per HitchMonitor, refilled in place) precisely so a hitching frame never allocates one, and
    // that reasoning applies here too, dst is one of this sidecar's own preallocated queue slots.
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
        dst.Samples.CopyFrom(src.Samples);
    }

    // Mirrors ReportPerf's grammar (flat key=value, ms terms already in ms, bytes shown as MB) so
    // the two report families read the same way side by side in the perf category.
    private static void WriteLogLine(HitchRecord r)
    {
        var s = r.Samples;
        Log.Info("perf", $"hitch frame={r.Frame} frame_ms={r.FrameMs:0.00} baseline_ms={r.BaselineMs:0.00} threshold_ms={r.ThresholdMs:0.00} script_ms={r.ScriptMs:0.00} render_cpu_ms={r.RenderCpuMs:0.00} gpu_ms={r.GpuMs:0.00} physics_ms={r.PhysicsMs:0.00} draws={r.Draws} prims={r.Prims} nodes={r.Nodes} mem_mb={r.MemBytes / (1024.0 * 1024.0):0.00} draws_delta={r.DrawsDelta} prims_delta={r.PrimsDelta} nodes_delta={r.NodesDelta} mem_delta_mb={r.MemBytesDelta / (1024.0 * 1024.0):0.00} gc0_delta={r.Gc0Delta} gc1_delta={r.Gc1Delta} gc2_delta={r.Gc2Delta} alloc_delta_mb={r.AllocatedBytesDelta / (1024.0 * 1024.0):0.00} samples={FormatSamples(s)} attributed_ms={s.AttributedMs:0.00} unattributed_ms={s.UnattributedMs:0.00} sample_violations={s.Violations}");
    }

    // The frame's named work as one space-free value, so the flat key=value grammar survives:
    // `site:callsxms`, comma-separated, in PerfSite order. A site with no calls is ABSENT rather
    // than printed as zero, same rule as StartupProfile's phases, and it keeps the line short on
    // the ordinary hitch where two things ran out of eight.
    private static string FormatSamples(PerfSampleFrame s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Calls.Length; i++)
        {
            if (s.Calls[i] == 0)
            {
                continue;
            }
            if (sb.Length > 0)
            {
                sb.Append(',');
            }
            sb.Append(PerfSample.NameOf((PerfSite)i)).Append(':').Append(s.Calls[i]).Append('x')
                .Append(s.Ms[i].ToString("0.00", CultureInfo.InvariantCulture));
        }
        return sb.Length == 0 ? "none" : sb.ToString();
    }

    // Hand-written, not a library: a HitchRecord is all-numeric (Frame/counts/ms terms, and the
    // Ring's FrameSamples are the same), so there is no string field that would ever need escaping.
    // The one string is a C8 site name, which is a compile-time constant from a closed enum spelled
    // [a-z_], a name, never data, so that stays true.
    // string.Create(IFormatProvider, …), not plain string interpolation, is what keeps every float
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
        var s = r.Samples;
        var sampleParts = new List<string>();
        for (int i = 0; i < s.Calls.Length; i++)
        {
            if (s.Calls[i] == 0)
            {
                continue;
            }
            sampleParts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{{\"site\":\"{PerfSample.NameOf((PerfSite)i)}\",\"ms\":{s.Ms[i]:0.000},\"calls\":{s.Calls[i]}}}"));
        }
        string json = string.Create(CultureInfo.InvariantCulture,
            $"{{\"frame\":{r.Frame},\"frame_ms\":{r.FrameMs:0.000},\"baseline_ms\":{r.BaselineMs:0.000},\"threshold_ms\":{r.ThresholdMs:0.000},\"script_ms\":{r.ScriptMs:0.000},\"render_cpu_ms\":{r.RenderCpuMs:0.000},\"gpu_ms\":{r.GpuMs:0.000},\"physics_ms\":{r.PhysicsMs:0.000},\"draws\":{r.Draws},\"prims\":{r.Prims},\"nodes\":{r.Nodes},\"mem_bytes\":{r.MemBytes},\"draws_delta\":{r.DrawsDelta},\"prims_delta\":{r.PrimsDelta},\"nodes_delta\":{r.NodesDelta},\"mem_bytes_delta\":{r.MemBytesDelta},\"gc0_delta\":{r.Gc0Delta},\"gc1_delta\":{r.Gc1Delta},\"gc2_delta\":{r.Gc2Delta},\"allocated_bytes_delta\":{r.AllocatedBytesDelta},\"samples\":[{string.Join(",", sampleParts)}],\"attributed_ms\":{s.AttributedMs:0.000},\"unattributed_ms\":{s.UnattributedMs:0.000},\"sample_violations\":{s.Violations},\"ring\":[{string.Join(",", ringParts)}]}}");
        _writer!.WriteLine(json);
    }
}
