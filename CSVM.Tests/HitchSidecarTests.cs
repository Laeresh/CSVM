using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The always-on detector's write path: the queue's copy-not-reference semantics, the
/// flush-interval gate, drop-oldest overflow, the JSON line's shape and culture-invariance, and
/// C8's per-site attribution riding along inside the record. Every test opens a
/// real file under <see cref="TestData.TempDir"/>, the sidecar's whole job is the write, so a fake
/// sink would not verify it, and reads it back with <see cref="JsonDocument"/> rather than string
/// matching, so a field reorder cannot make an assertion pass by accident.
/// </summary>
public class HitchSidecarTests
{
    [Fact]
    public void TheSidecarPathReplacesTheLogExtension()
    {
        string dir = TestData.TempDir();
        string logPath = Path.Combine(dir, "fly-20260101-000000.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 4, queueDepth: 4, flushSeconds: 0);

        Assert.True(sidecar.IsOpen);
        Assert.Equal(Path.Combine(dir, "fly-20260101-000000.hitches.jsonl"), sidecar.JsonPath);
    }

    [Fact]
    public void EnqueueThenFlushWritesOneJsonLineMatchingTheRecord()
    {
        string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 4, queueDepth: 4, flushSeconds: 0);
        var record = NewRecord(frame: 300, frameMs: 59.38, gc0Delta: 18, allocDelta: 859_822_536, ringCount: 3);

        sidecar.Enqueue(record);
        sidecar.Flush();

        string[] lines = ReadLines(sidecar.JsonPath);
        var line = Assert.Single(lines);
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal(300, root.GetProperty("frame").GetInt64());
        Assert.Equal(59.38, root.GetProperty("frame_ms").GetDouble(), 3);
        Assert.Equal(18, root.GetProperty("gc0_delta").GetInt32());
        Assert.Equal(859_822_536, root.GetProperty("allocated_bytes_delta").GetInt64());
        var ring = root.GetProperty("ring");
        Assert.Equal(3, ring.GetArrayLength());
        // Ring[2] is the hitching frame itself in HitchMonitor's own convention (oldest first).
        Assert.Equal(record.Ring[2].FrameMs, ring[2].GetProperty("frame_ms").GetDouble(), 3);
    }

    [Fact]
    public void ARecordsRingIsCopiedNotReferenced()
    {
        string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 4, queueDepth: 4, flushSeconds: 0);
        var record = NewRecord(frame: 1, frameMs: 50, gc0Delta: 0, allocDelta: 0, ringCount: 2);

        sidecar.Enqueue(record);
        // Mutating the source AFTER Enqueue must not reach the queued copy, the whole point of
        // copying rather than holding a reference to HitchMonitor.Last, which the monitor
        // overwrites on the very next trip.
        record.FrameMs = 999;
        record.Ring[0] = new FrameSample(FrameMs: 999, ScriptMs: 0, RenderCpuMs: 0, GpuMs: 0, PhysicsMs: 0);
        sidecar.Flush();

        using var doc = JsonDocument.Parse(Assert.Single(ReadLines(sidecar.JsonPath)));
        Assert.Equal(50, doc.RootElement.GetProperty("frame_ms").GetDouble(), 3);
        Assert.NotEqual(999, doc.RootElement.GetProperty("ring")[0].GetProperty("frame_ms").GetDouble());
    }

    [Fact]
    public void TickDoesNotFlushBeforeTheIntervalElapses()
    {
        string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 2, queueDepth: 4, flushSeconds: 1f);
        sidecar.Enqueue(NewRecord(frame: 1, frameMs: 50, gc0Delta: 0, allocDelta: 0, ringCount: 1));

        sidecar.Tick(500); // 0.5s of 1s, not yet
        Assert.Empty(ReadLines(sidecar.JsonPath));

        sidecar.Tick(600); // 1.1s total, past the interval
        Assert.Single(ReadLines(sidecar.JsonPath));
    }

    [Fact]
    public void QueueOverflowDropsTheOldestRecordsFirst()
    {
        string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 1, queueDepth: 2, flushSeconds: 0);

        for (int i = 1; i <= 4; i++)
        {
            sidecar.Enqueue(NewRecord(frame: i, frameMs: 50, gc0Delta: 0, allocDelta: 0, ringCount: 1));
        }
        sidecar.Flush();

        string[] lines = ReadLines(sidecar.JsonPath);
        Assert.Equal(2, lines.Length);
        // Depth 2 over 4 enqueues: frames 1 and 2 are the dropped-oldest, 3 and 4 survive, oldest
        // surviving first, the same order HitchMonitor's own ring reads in.
        Assert.Equal(3, JsonDocument.Parse(lines[0]).RootElement.GetProperty("frame").GetInt64());
        Assert.Equal(4, JsonDocument.Parse(lines[1]).RootElement.GetProperty("frame").GetInt64());
    }

    [Fact]
    public void AttributionRidesWithTheRecordAndIsCopiedNotReferenced()
    {
        string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
        var sidecar = new HitchSidecar(logPath, ringFrames: 1, queueDepth: 2, flushSeconds: 0);
        var record = NewRecord(frame: 300, frameMs: 62.5, gc0Delta: 0, allocDelta: 0, ringCount: 1);
        record.Samples.Ms[(int)PerfSite.DebrisSpawn] = 12.5;
        record.Samples.Calls[(int)PerfSite.DebrisSpawn] = 2;
        record.Samples.AttributedMs = 12.5;
        record.Samples.UnattributedMs = 50;

        sidecar.Enqueue(record);
        record.Samples.Ms[(int)PerfSite.DebrisSpawn] = 999;
        record.Samples.UnattributedMs = 999;
        sidecar.Flush();

        using var doc = JsonDocument.Parse(Assert.Single(ReadLines(sidecar.JsonPath)));
        var root = doc.RootElement;
        // Only the sites that actually ran are written, so a record names two things rather than
        // eight, seven of which are zero (C8's own rule, StartupProfile's phase rule before it).
        var samples = root.GetProperty("samples");
        Assert.Equal(1, samples.GetArrayLength());
        Assert.Equal("debris_spawn", samples[0].GetProperty("site").GetString());
        Assert.Equal(12.5, samples[0].GetProperty("ms").GetDouble(), 3);
        Assert.Equal(2, samples[0].GetProperty("calls").GetInt32());
        Assert.Equal(12.5, root.GetProperty("attributed_ms").GetDouble(), 3);
        Assert.Equal(50, root.GetProperty("unattributed_ms").GetDouble(), 3);
        Assert.Equal(0, root.GetProperty("sample_violations").GetInt32());
    }

    [Fact]
    public void JsonFloatsStayInvariantUnderAnyCulture()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            string logPath = Path.Combine(TestData.TempDir(), "fly-stamp.log");
            var sidecar = new HitchSidecar(logPath, ringFrames: 1, queueDepth: 1, flushSeconds: 0);
            sidecar.Enqueue(NewRecord(frame: 1, frameMs: 16.667, gc0Delta: 0, allocDelta: 0, ringCount: 1));

            sidecar.Flush();

            string line = Assert.Single(ReadLines(sidecar.JsonPath));
            Assert.Contains("16.667", line);
            Assert.DoesNotContain("16,667", line);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    // File.ReadAllLines' own share request collides with the sidecar's still-open writer (it stays
    // open for the process's whole life, by design, see HitchSidecar's own doc comment); a wider
    // share on the read side is a test concern only, never something production code needs.
    private static string[] ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new System.Collections.Generic.List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        return lines.ToArray();
    }

    private static HitchRecord NewRecord(long frame, double frameMs, int gc0Delta, long allocDelta, int ringCount)
    {
        var r = new HitchRecord(ringFrames: 4)
        {
            Frame = frame,
            FrameMs = frameMs,
            BaselineMs = frameMs / 4,
            ThresholdMs = 40,
            ScriptMs = 1,
            RenderCpuMs = 2,
            GpuMs = 3,
            PhysicsMs = 0.5,
            Draws = 100,
            Prims = 5000,
            Nodes = 400,
            MemBytes = 1024,
            DrawsDelta = 1,
            PrimsDelta = 2,
            NodesDelta = 3,
            MemBytesDelta = 4,
            Gc0Delta = gc0Delta,
            Gc1Delta = 0,
            Gc2Delta = 0,
            AllocatedBytesDelta = allocDelta,
            RingCount = ringCount,
        };
        for (int i = 0; i < ringCount; i++)
        {
            r.Ring[i] = new FrameSample(FrameMs: 8.3 + i, ScriptMs: 1, RenderCpuMs: 2, GpuMs: 3, PhysicsMs: 0.5);
        }
        return r;
    }
}
