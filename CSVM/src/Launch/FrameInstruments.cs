using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The process's frame instruments, on wall time and outliving every session. Always on: the
/// hitch monitor and its sidecar, the <c>[perf] rate</c> line and the F14 / --debug-fps readout.
/// Opt in: the --perf window, the --debug-mem census and the --hitch-inject stall. The launcher
/// builds it in three steps, since each part has its own place in startup. It calls
/// <see cref="BeginFrame"/> and <see cref="EndFrame"/> once a frame, and <see cref="Rearm"/>
/// across a stall that is not a frame. Module notes: docs/architecture/Launch.md.
/// </summary>
internal sealed class FrameInstruments
{
    // Rendered frames per `--perf` report. A frame count rather than a wall second: under the
    // fixed clock one rendered frame is exactly one sim step. A window is then a fixed amount of
    // simulation, and a paired A/B of one scenario compares equal sample counts. At the vsync cap
    // it is also still one report a second, so an interactive run reads as it always did.
    private const int PerfWindowFrames = 60;

    // Nearest-rank p95 index into the sorted window (0-based): `ceil(0.95 x
    // PerfWindowFrames) - 1`. At 60 samples this is index 56, leaving 3 samples above it. p99
    // is deliberately not reported. Nearest-rank p99 over 60 samples resolves to index 59, the
    // slot max takes, so it would be max under another name (see ReportPerf).
    private const int Perf95Index = (PerfWindowFrames * 95 + 99) / 100 - 1;

    // Wall seconds per always-on `[perf] rate` line. Wall time and NOT a frame count, unlike
    // PerfWindowFrames above: a frame-count window stretches exactly as the rate falls. At 1 fps a
    // 60-frame window would report once a minute and describe the collapse least where it matters.
    // Ten seconds is quiet enough to leave always on and short enough to place a drop. TUNE.
    private const double RateWindowSeconds = 10;

    private readonly SessionSpec _spec;

    // This window's unaveraged per-frame wall cost, for ReportPerf's max and p95. Every window
    // overwrites it fully, so it needs no reset. _perfFrameMsSorted is the sort's scratch, kept off
    // the frame path so no window allocates.
    private readonly double[] _perfFrameMs = new double[PerfWindowFrames];
    private readonly double[] _perfFrameMsSorted = new double[PerfWindowFrames];

    // The always-on frame-hitch instrument and the viewport render times that feed it, both
    // settled in the constructor. The monitor's constructor registers its hitchMonitor.* config
    // keys, and measured render time is opt-in per viewport.
    private readonly HitchMonitor _hitchMonitor;
    private readonly MeasuredRenderTime _renderTime;

    // The write path for the monitor above: queues a tripped record and drains it a few seconds
    // later, never inline on the hitching frame. Opened after Log.Open, since its path derives
    // from Log.SinkPath.
    private HitchSidecar _hitchSidecar = null!;

    // The F14 / --debug-fps frame-cost readout, ticked every frame like the monitor. Null until
    // BuildHud, which an early-quit probe never reaches.
    private UI.Overlays.PerfHud? _hud;

    private double _perfClock;
    private int _perfFrames;
    private double _perfProcess, _perfGpu, _perfCpuRender, _perfPhysics, _perfSetup;
    private double _perfDraws, _perfPrims, _perfNodes, _perfMem;

    // The spyglass discs' own counts and GPU time over the window (SpyglassView.Census), split out
    // of draws, prims and the root viewport's gpu_ms.
    private double _perfDiscs, _perfDiscDraws, _perfDiscShadowDraws, _perfDiscPrims, _perfDiscGpu;

    // The --perf GC readout. Built with the first --perf frame, so a run without the flag
    // subscribes to no runtime events at all.
    private GcTrace? _gcTrace;

    // Whether --perf has hooked the rendering server's draw signals into EngineGapCost.
    private bool _drawMarksHooked;

    // The always-on rate window (ReportRate). Separate accumulators from the --perf ones above
    // rather than shared: those are opt-in and reset on a frame count, these run every session.
    private double _rateWallMs;
    private double _rateWorstMs;
    private int _rateFrames;

    // The previous frame's QPC stamp, so the monitor is fed a raw wall cost, not Godot's
    // post-processed `delta`. It is 0 on the first frame, which reports 0 ms and trips nothing.
    private long _lastFrameStamp;

    // The QPC stamp of the last --debug-mem line.
    private long _memCensusStamp;

    // The counters BeginFrame read, which EndFrame's --perf window sums.
    private FrameCounters _counters;

    /// <summary>Builds the hitch monitor, which registers its config keys, and turns on measured
    /// render time for <paramref name="viewport"/>. Ahead of every probe's early quit and of
    /// --dump-config, so the registry those read is complete.</summary>
    public FrameInstruments(SessionSpec spec, Rid viewport)
    {
        _spec = spec;
        _hitchMonitor = new HitchMonitor();
        // Measured render time reads 0 until it is enabled. The hitch record needs the CPU/GPU
        // split on every frame, not only on a --perf run.
        RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
        _renderTime = new MeasuredRenderTime(viewport);
    }

    /// <summary>Opens the hitch sidecar beside the log, or under the log directory when
    /// <c>Log.Open</c> failed. Ahead of --dump-config, since it registers the hitchSidecar.* keys.
    /// </summary>
    public void OpenSidecar(string repoRoot, bool exported)
    {
        string path = Log.SinkPath
            ?? System.IO.Path.Combine(Log.DirectoryFor(repoRoot, exported), $"{_spec.ModeName}-nolog.hitches.jsonl");
        _hitchSidecar = new HitchSidecar(path, _hitchMonitor.Last.Ring.Length);
    }

    /// <summary>The F14 / --debug-fps readout over the monitor, for the caller to parent. Built
    /// after the early quits, which render no frame it would have anything to show.</summary>
    public UI.Overlays.PerfHud BuildHud()
    {
        _hud = new UI.Overlays.PerfHud
        {
            InitialMode = _spec.DebugFps == null ? UI.Overlays.PerfHud.Mode.Off : UI.Overlays.PerfHud.ParseMode(_spec.DebugFps),
            Monitor = _hitchMonitor,
        };
        return _hud;
    }

    /// <summary>The frame's wall cost in milliseconds, fed to the hitch monitor, the rate line and
    /// the readout. Called once a frame, after the shader clock and before anything else reads it.
    /// </summary>
    public double BeginFrame()
    {
        // One counter read per frame feeds every instrument: the hitch monitor wants them
        // unaveraged and the --perf window wants them summed.
        _counters = ReadFrameCounters();
        // Fires one QPC read before the stamp below, so the stall inflates THIS frame's wall
        // cost. Matched against HitchMonitor's own frame counter, never the sim frame: the
        // injector has to work with no session built at all.
        if (_spec.HitchInjectMs is float injectMs
            && _hitchMonitor.FrameCount + 1 == _spec.HitchInjectFrame)
        {
            InjectHitch(injectMs, _spec.HitchInjectAlloc);
        }

        // Fed our own QPC pair, never Godot's post-processed `delta`, which measures as a
        // quantised constant.
        long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        double frameMs = _lastFrameStamp == 0
            ? 0
            : (stamp - _lastFrameStamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _lastFrameStamp = stamp;
        // Closes the attribution window at the instant the wall cost is stamped. The scopes that
        // ran and the frame_ms they ran inside then describe the same span.
        PerfSample.EndFrame();
        // A trip queues its record (a cheap, preallocated copy) rather than writing anything here.
        // The sidecar's own Tick drains the queue a few quiet frames later.
        if (_hitchMonitor.Tick(frameMs, _counters))
        {
            _hitchSidecar.Enqueue(_hitchMonitor.Last);
        }

        _hitchSidecar.Tick(frameMs);
        // Unconditional where --perf is opt-in: the rate a player actually saw is the one thing a
        // report from someone else's machine cannot be reconstructed without.
        ReportRate(frameMs);
        // Early-quit probes do not construct the readout, but Godot may process one shutdown frame.
        _hud?.Tick(frameMs, _counters);
        return frameMs;
    }

    /// <summary>The opt-in half of the frame: the --perf window over the counters
    /// <see cref="BeginFrame"/> read, and the once-a-wall-second --debug-mem line.</summary>
    public void EndFrame(double delta, GameClock? clock)
    {
        if (_spec.Perf)
        {
            if (!_drawMarksHooked)
            {
                // Only under --perf: an ordinary run pays for no signal into managed code.
                RenderingServer.FramePreDraw += EngineGapCost.MarkPreDraw;
                RenderingServer.FramePostDraw += EngineGapCost.MarkPostDraw;
                _drawMarksHooked = true;
            }

            (_gcTrace ??= GcTrace.Create(_spec.GcTypes)).Tick();
            ReportPerf(delta, _counters, clock);
        }

        // The frame's own stamp, the one BeginFrame took.
        if (_spec.DebugMem && _lastFrameStamp - _memCensusStamp >= System.Diagnostics.Stopwatch.Frequency)
        {
            _memCensusStamp = _lastFrameStamp;
            Log.Info("perf", $"mem frame={Engine.GetProcessFrames()} {MemoryCensus.Line(Mech3.ShaderTwins.Made)}");
        }
    }

    /// <summary>Drops every instrument's baseline across a stall that is not a frame: a session
    /// build or teardown. Nothing queued before it is held through it, and no window spans it.
    /// </summary>
    public void Rearm()
    {
        _hitchSidecar.Flush();
        _hitchMonitor.Rearm();
        RearmRate();
        // The build's own scopes (loads, material creation) belong to no frame. The frame that
        // closes over the build would otherwise report them all at once.
        PerfSample.Reset();
        // Every bracket takes the same boundary. A build that spans the tail leaves a half-open
        // tick, pass, AI walk or phase. Its next close would charge the whole build to one step.
        PhysicsTickCost.Reset();
        ProcessPassCost.Reset();
        EngineGapCost.Reset();
        AiStepCost.Reset();
        SimPhaseCost.Reset();
        ProcessSiteCost.Reset();
        _hud?.Rearm();
    }

    /// <summary>The quit's half: the --debug-mem exit line, then the sidecar's last flush.</summary>
    public void Exit()
    {
        if (_spec.DebugMem)
        {
            // Its peak_priv_mb is the whole run's, which a short probe's once-a-second line can miss.
            Log.Info("perf", $"mem exit frame={Engine.GetProcessFrames()} {MemoryCensus.Line(Mech3.ShaderTwins.Made)}");
        }

        _hitchSidecar.Flush();
    }

    // --hitch-inject=: burns wall time synchronously for about `ms`, so a stall of known
    // magnitude exists to verify against. The busy-wait form proves the timing path; `alloc`
    // burns the same time allocating 4 KB buffers instead, the only way to move the GC columns.
    // ⚠ Never wrap this in a PerfSample scope. An injected fault must show as unattributed
    // time, not a breadcrumb mistaken for the thing under test.
    private static void InjectHitch(float ms, bool alloc)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        double freq = System.Diagnostics.Stopwatch.Frequency;
        long sink = 0;
        while ((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / freq < ms)
        {
            if (alloc)
            {
                sink += new byte[4096].Length;
            }
        }

        Log.Info("perf", $"hitch-inject fired ms={ms:0.0} alloc={alloc} bytes={sink}");
    }

    // Samples the engine's eight per-frame counters once, for every instrument. The two `TIME_*`
    // monitors are seconds and are converted here, so everything downstream is in milliseconds.
    // Like `delta` they describe the frame that just ended, which is what a hitch record needs.
    // The render pair is one draw older under the separate render thread.
    private FrameCounters ReadFrameCounters()
    {
        var (renderCpuMs, gpuMs) = _renderTime.Read();
        return new(
            ScriptMs: 1000 * Performance.GetMonitor(Performance.Monitor.TimeProcess),
            RenderCpuMs: renderCpuMs,
            GpuMs: gpuMs,
            PhysicsMs: 1000 * Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess),
            Draws: (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            Prims: (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            Nodes: (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
            MemBytes: (long)Performance.GetMonitor(Performance.Monitor.MemoryStatic));
    }

    // The always-on frame-rate trace, one `[perf] rate` line per RateWindowSeconds with the rate
    // the window achieved. HitchMonitor cannot answer this: a sustained collapse drags its rolling
    // median up and trips nothing. Mean and worst frame sit side by side, so a rate drop and a
    // single stall read differently.
    private void ReportRate(double frameMs)
    {
        _rateFrames++;
        _rateWallMs += frameMs;
        _rateWorstMs = System.Math.Max(_rateWorstMs, frameMs);
        if (_rateWallMs < RateWindowSeconds * 1000)
        {
            return;
        }

        double seconds = _rateWallMs / 1000;
        double fps = _rateFrames / seconds;
        double meanMs = _rateWallMs / _rateFrames;
        double worstMs = _rateWorstMs;
        Log.Info("perf", $"rate fps={fps:0.0} frames={_rateFrames} wall_s={seconds:0.0} frame_ms={meanMs:0.00} worst_ms={worstMs:0.00}");
        RearmRate();
    }

    // Drops the open rate window, for the reason HitchMonitor.Rearm drops its baseline. A window
    // spanning a session build would report a rate the run never ran at.
    private void RearmRate()
    {
        _rateWallMs = 0;
        _rateWorstMs = 0;
        _rateFrames = 0;
    }

    // --perf: the headless stand-in for the editor's profiler, meaned over the window so a
    // single hitch doesn't read as a regression. Two builds are A/B'd on the same line.
    // `script_ms`/`physics_ms` are Godot's worst-of-the-last-second monitors, kept for old records.
    // The measured terms are `proc_ms`, `phys_tick_ms` and `ai_ms` (verification PERF-1).
    // No `p99_ms`: a 60-sample window's nearest-rank p99 is just `max_ms` (Perf95Index).
    private void ReportPerf(double delta, in FrameCounters counters, GameClock? clock)
    {
        _perfFrames++;
        _perfFrameMs[_perfFrames - 1] = delta * 1000;
        _perfClock += delta;
        _perfProcess += counters.ScriptMs;
        _perfPhysics += counters.PhysicsMs;
        _perfCpuRender += counters.RenderCpuMs;
        // The rendering server's instance update, run before any viewport draws and left out of
        // render_cpu_ms. It is the part of draw_ms that grows with what moved this frame.
        _perfSetup += RenderingServer.GetFrameSetupTimeCpu();
        _perfGpu += counters.GpuMs;
        // Counts, averaged like every ms term, but with no timing noise in them. A scene that
        // starts drawing more says so exactly, where an ms term must clear a noise band first.
        _perfDraws += counters.Draws;
        _perfPrims += counters.Prims;
        _perfNodes += counters.Nodes;
        _perfMem += counters.MemBytes;
        var (discs, discDraws, discShadowDraws, discPrims, discGpuMs) = Flight.Camera.SpyglassView.Census();
        _perfDiscs += discs;
        _perfDiscDraws += discDraws;
        _perfDiscShadowDraws += discShadowDraws;
        _perfDiscPrims += discPrims;
        _perfDiscGpu += discGpuMs;
        if (_perfFrames < PerfWindowFrames)
        {
            return;
        }

        // Locals, not one very long expression: Log takes a single interpolated string. Two
        // concatenated ones would already have formatted their floats in the current culture.
        double n = _perfFrames;
        long simFrame = clock?.Frame ?? 0;
        double wallMs = 1000 * _perfClock;
        double fps = n / _perfClock;
        double frameMs = wallMs / n;
        // Every ms term is already in milliseconds here: ReadFrameCounters converts Godot's two
        // TIME_* monitors once, at the read.
        double scriptMs = _perfProcess / n;
        double renderCpuMs = _perfCpuRender / n;
        double setupMs = _perfSetup / n;
        double gpuMs = _perfGpu / n;
        double physicsMs = _perfPhysics / n;
        // ⚠ These are the physics terms to read, not physics_ms above (verification PERF-1). One
        // tick is one 1/60 sim step on a realtime clock, so phys_hz is sim seconds per wall second.
        // A step over its 16.7 ms budget shows here as a rate under 60.
        var (physTickMs, physTickMaxMs, physTicks) = PhysicsTickCost.Take();
        // ⚠ The script term to read, not script_ms above (verification PERF-1). Meaned over the
        // passes that CLOSED, one fewer than the window's frames: this runs inside the pass, whose
        // tail lands in the next window.
        var (procTotalMs, procMaxMs, procPasses) = ProcessPassCost.Take();
        double procMs = procPasses > 0 ? procTotalMs / procPasses : 0;
        // ⚠ The only term that attributes frame cost to the AI. Meaned over the window's FRAMES,
        // not its walks: a parent-driven clock runs several walks per rendered frame. The question
        // is what the AI cost that frame. Zero with no AI spawned.
        var (aiTotalMs, aiSteps, aiPlaneSum) = AiStepCost.Take();
        double aiMs = aiTotalMs / n;
        double aiPlanes = aiSteps > 0 ? (double)aiPlaneSum / aiSteps : 0;
        double physHz = _perfClock > 0 ? physTicks / _perfClock : 0;
        double physTick = physTicks > 0 ? physTickMs / physTicks : 0;
        // Per FRAME, like proc_ms: the engine's step after each tick, then the flush, draw and idle
        // after the pass. With the two pass terms they sum to frame_ms (verification PERF-40).
        var (physEngineTotalMs, deferTotalMs, drawTotalMs, idleTotalMs) = EngineGapCost.Take();
        double physEngineMs = physEngineTotalMs / n;
        double deferMs = deferTotalMs / n;
        double drawMs = drawTotalMs / n;
        double idleMs = idleTotalMs / n;
        // These split the two whole-pass terms above by what ran. The sim step goes per TICK beside
        // phys_tick_ms, the named _Process consumers per FRAME beside proc_ms (src/Utils/PhaseCost.cs).
        string simRow = SimPhaseCost.TakeRow(physTicks);
        string simAllocRow = SimPhaseCost.AllocRow();
        string procSites = ProcessSiteCost.TakeRow(n);
        double draws = _perfDraws / n;
        double prims = _perfPrims / n;
        double nodes = _perfNodes / n;
        double memMb = _perfMem / n / (1024 * 1024);
        System.Array.Copy(_perfFrameMs, _perfFrameMsSorted, PerfWindowFrames);
        System.Array.Sort(_perfFrameMsSorted);
        double maxMs = _perfFrameMsSorted[PerfWindowFrames - 1];
        double p95Ms = _perfFrameMsSorted[Perf95Index];
        Log.Info("perf", $"window sim_frame={simFrame} frames={_perfFrames} wall_ms={wallMs:0.00} fps={fps:0.0} frame_ms={frameMs:0.00} script_ms={scriptMs:0.00} proc_ms={procMs:0.000} proc_max_ms={procMaxMs:0.000} proc_passes={procPasses} ai_ms={aiMs:0.000} ai_planes={aiPlanes:0.0} render_cpu_ms={renderCpuMs:0.00} setup_ms={setupMs:0.00} gpu_ms={gpuMs:0.00} physics_ms={physicsMs:0.00} phys_tick_ms={physTick:0.000} phys_tick_max_ms={physTickMaxMs:0.000} phys_hz={physHz:0.0} phys_engine_ms={physEngineMs:0.000} defer_ms={deferMs:0.000} draw_ms={drawMs:0.000} idle_ms={idleMs:0.000} draws={draws:0.0} prims={prims:0.0} nodes={nodes:0.0} mem_mb={memMb:0.00} max_ms={maxMs:0.00} p95_ms={p95Ms:0.00} sim_ms={simRow} proc_sites_ms={procSites}");
        // Its own line, not another term on the window above. The BYTE figure answers a different
        // question from the millisecond one: which phase feeds the collector, rather than which
        // phase the pause landed in (PERF-34). The two are read side by side.
        Log.Info("perf", $"alloc sim_frame={simFrame} sim_alloc_b={simAllocRow}");
        // The discs' own line, said only while one rendered in the window. Each is a viewport of its
        // own, which gpu_ms leaves out and draws folds into the frame's total (verification PERF-47).
        if (_perfDiscs > 0)
        {
            Log.Info("perf", $"spyglass sim_frame={simFrame} discs={_perfDiscs / n:0.00} disc_draws={_perfDiscDraws / n:0.0} disc_shadow_draws={_perfDiscShadowDraws / n:0.0} disc_prims={_perfDiscPrims / n:0} disc_gpu_ms={_perfDiscGpu / n:0.000}");
        }

        _perfClock = 0;
        _perfFrames = 0;
        _perfProcess = _perfGpu = _perfCpuRender = _perfPhysics = _perfSetup = 0;
        _perfDraws = _perfPrims = _perfNodes = _perfMem = 0;
        _perfDiscs = _perfDiscDraws = _perfDiscShadowDraws = _perfDiscPrims = _perfDiscGpu = 0;
    }
}
