using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Main.tscn's root: the once-per-process bootstrap, and everything that persists across
/// in-process relaunches. <c>_Ready</c> parses the command line into a <see cref="SessionSpec"/>,
/// settles paths, applies process-wide side effects a pure value cannot, registers the global
/// shader parameters once, runs the early-quit probes, and builds the persistent camera / orbit
/// rig / sun / WorldEnvironment.
/// Each launch instantiates a <see cref="GameSession"/> with the launch's spec and a
/// <see cref="LauncherContext"/>; Esc from a menu-launched flight frees it
/// (<see cref="ReturnToMenu"/>) and shows the launchscreen again. Full arg reference:
/// docs/cli.md. Module notes: this file's docs/architecture.md entry.
/// </summary>
public partial class Launcher : Node3D
{
    // Rendered frames per `--perf` report. A frame count rather than a wall second
    // because under the fixed clock one rendered frame is exactly one sim step, so a window is a
    // fixed amount of simulation and two runs of the same scenario yield the same number
    // of samples — which is what makes a paired A/B comparable. At the vsync cap it is also
    // still one report a second, so an interactive run reads as it always did.
    private const int PerfWindowFrames = 60;

    // Nearest-rank p95 index into the sorted window (0-based): `ceil(0.95 x
    // PerfWindowFrames) - 1`. At 60 samples this is index 56, leaving 3 samples above it. p99
    // is deliberately not reported — nearest-rank p99 over 60 samples resolves to index 59, the
    // same slot as max, so it would just be max under another name (see ReportPerf).
    private const int Perf95Index = (PerfWindowFrames * 95 + 99) / 100 - 1;

    // Master-bus index. This project ships no bus layout, so Master is the only bus
    // and everything (both audio paths) is on it by default.
    private const int MasterBus = 0;

    // Master output gain, linear, when neither `--volume=` nor the
    // `audio.volume` config key says otherwise. 0: launches are silent
    // unless someone asks for sound (the Run scripts pass `--volume=1.0`), so a scripted
    // or agent run never sounds by accident — see ApplyMasterVolume.
    private const float MasterVolumeDefault = 0f;

    // The bus's own resting gain (0 dB). A launch resolving to this leaves the bus
    // untouched, keeping it byte-identical in output and console log to a launch that never
    // had a volume path at all.
    private const float MasterVolumeUnattenuated = 1f;

    // Gain floor for the dB conversion, since `LinearToDb(0)` is negative infinity.
    // -80 dB is inaudible, which is the whole point of `--volume=0`.
    private const float MasterVolumeFloor = 0.0001f;

    // What F11's placement print receives at the launchscreen, where no session (and no rigs)
    // exists — the same empty list the pre-split root held after a teardown.
    private static readonly List<PlayerRig> NoRigs = new();

    // This window's unaveraged per-frame wall cost, for the max/p95 that ReportPerf reports
    // alongside its means — fully overwritten every window, so it needs no reset. _perfFrameMsSorted
    // is scratch for the sort at window close, kept off the frame path so no window allocates.
    private readonly double[] _perfFrameMs = new double[PerfWindowFrames];
    private readonly double[] _perfFrameMsSorted = new double[PerfWindowFrames];

    // Everything the command line settled, parsed and resolved once (see SessionSpec). _cli is what
    // the user typed; _spec is what the LIVE session was built from — the launchscreen's pick
    // patches it — so every consumer below reads _spec and nothing re-derives a launch setting.
    private SessionSpec _cli = null!;
    private SessionSpec _spec = null!;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private int[][]? _menuPads;
    // The device-less menu players --debug-join= asks for, held until the launchscreen exists and
    // consumed there: a later return to the menu keeps whoever really joined.
    private int _pendingJoin;
    // --debug-waves=/--debug-wingmen=, the same one-shot hold as _pendingJoin above but for the
    // Instant Action wizard's own screenshot aids.
    private int _pendingWaves, _pendingWingmen;
    // The --screenshot=/--shots=/--frames= state machine and F11/F12's placement print and
    // ad-hoc save — see src/Testing/CaptureDirector.cs's entry. Process-scoped: constructed once
    // from the launch spec, never re-armed by a menu relaunch.
    private Testing.CaptureDirector _captureDirector = null!;

    // The --export-gltf= one-shot and F10's ad-hoc glTF save — see src\Testing\GltfExporter.cs's
    // entry. Constructed once from the launch spec alongside the capture director.
    private Testing.GltfExporter _gltfExporter = null!;
    // The master seed every subsystem generator derives from (see Utils.Rng). Pinned runs take the
    // spec's value; everything else draws from the clock, which is why it is resolved here and not
    // in the spec.
    private ulong _masterSeed;
    // The once-per-process draw _masterSeed starts at, kept so an unpinned relaunch can step to the
    // next sortie's master from it rather than from whatever the last session used.
    private ulong _processSeed;
    // How many times the launchscreen has started a flight this process — the step count applied to
    // _processSeed for an unpinned run, and the label the seed is logged under.
    private int _sortie;

    // The clock the --run-tests suites hand back (RunTestSuites' out param), ticked below so a
    // suite's teardown frame behaves as it always did. Every other clock is the session node's.
    private GameClock? _clock;

    private Camera3D _camera = null!;
    // Built once in _Ready and kept across sessions (like the camera). The mesh lab steers
    // both — sun direction/energy and the ambient — so held here rather than local to
    // SetupLighting.
    private DirectionalLight3D _sun = null!;
    private Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); created in
    // _Ready and kept across sessions, like _camera. --yaw=/--pitch= seed its initial angles.
    private OrbitCamera _orbit = null!;

    // Session lifecycle (the launchscreen's in-process world rebuild): each launch instantiates a
    // GameSession node, freed again by ReturnToMenu. The camera, lights and global shader
    // params live on `this` and persist across sessions.
    private GameSession? _session; // the current session node (null at the launchscreen)
    private LaunchMenu? _menu;     // the in-game launchscreen (shown on a no-content-arg launch)
    private bool _menuDriven;      // launched into the menu → Esc from flight returns here, not quit

    private double _perfClock;
    private int _perfFrames;
    private double _perfProcess, _perfGpu, _perfCpuRender, _perfPhysics;
    private double _perfDraws, _perfPrims, _perfNodes, _perfMem;

    // The always-on frame-hitch instrument and the viewport whose render times feed it. Both are
    // settled in _Ready: the monitor's constructor is what registers its hitchMonitor.* config keys,
    // and measured render time is opt-in per viewport, so both have to happen before the first
    // frame rather than lazily on one.
    private HitchMonitor _hitchMonitor = null!;
    // The F14 / --debug-fps frame-cost readout, ticked every frame like
    // the instrument above it, but drawing (if switched on) is its own concern, not this class's.
    private UI.PerfHud _perfHud = null!;
    private Rid _viewportRid;
    // The previous frame's QPC stamp, so the monitor is fed a raw wall cost rather than Godot's
    // post-processed `delta`. 0 on the first frame, which reports 0 ms and trips nothing.
    private long _lastFrameStamp;
    // B6's write path for the monitor above: queues a tripped record and drains it a few seconds
    // later, never inline on the hitching frame. Built after Log.Open (its path derives from
    // Log.SinkPath), so it lives a step later in _Ready than _hitchMonitor does.
    private HitchSidecar _hitchSidecar = null!;

    // Base (chapter-independent) paths + parse state, set once in _Ready; each session node
    // receives them via LauncherContext and recomputes the chapter-dependent gamez/texture/mission
    // paths from its spec. In the editor (and editor-run builds) _repoRoot is the repo checkout
    // root (res://'s parent on disk); in an exported build it is the exe's own directory, since
    // GlobalizePath("res://") only maps to a real directory inside the editor.
    private string _repoRoot = "";
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game — /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private string _dataRoot = "";
    private string _planesGamezPath = "";  // extracted/planes.zip — the aircraft models (always this)
    private string _zrdrPath = "";
    private string _soundsPath = "";
    private string _interpPath = "";
    private string _messagesPath = "";
    private string _rofPath = "";          // the extracted UI archive (paint patterns)
    // Constructed once the base paths above are settled; every --dump-*/--run-tests/--*-test/
    // --destroy= probe wrapper delegates to it (see src/Testing/ProbeRunner.cs).
    private Testing.ProbeRunner _probeRunner = null!;

    // Alt-tabbing away silences the game; alt-tabbing back restores it, via an AudioServer
    // master-bus mute rather than a factor threaded through the audio code. See this file's
    // docs/architecture.md entry for why (FlightAudio vs WorldSounds gain plumbing).
    // ⚠ `--mute` cannot be reused for this: it is load-time and never constructs the audio
    // players, so there is nothing here to toggle.
    private bool _focusMuted;

    // The session clock as this frame sees it: the suites' own under
    // `--run-tests`, the live session's (published as GameClock.Current by its
    // build, nulled by its teardown) otherwise, null at the launchscreen.
    private GameClock? ClockNow => _clock ?? GameClock.Current;

    public override void _Ready()
    {
        // One notch behind the session node (ProcessPriority -1000), so the shader clock and
        // capture pipeline run at the same point in the frame they did on one root. Godot runs
        // the lowest priority first.
        ProcessPriority = -999;

        // Load the optional tuning-override file first, before any module reads a Config value.
        // Missing/malformed file → in-code defaults (never throws); see src/Config.cs.
        Config.Load();

        if (OS.HasFeature("editor"))
        {
            // res:// is the CSVM/ project dir on disk only while the editor (or an editor-run
            // build) hosts the game; the repo root is its parent.
            var projectDir = ProjectSettings.GlobalizePath("res://");
            _repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));
        }
        else
        {
            // Exported build: res:// lives inside the pck, so the root is the exe's own folder —
            // extracted/ ships beside the exe, and .scratch/ output lands there too.
            _repoRoot = Path.GetFullPath(Path.GetDirectoryName(OS.GetExecutablePath())!);
        }

        // Everything the command line settles is parsed and resolved in one place; see
        // SessionSpec. What's left here is what a pure value cannot do: process-wide side
        // effects, and the warnings below, emitted before the log so they read in launch order.
        _cli = SessionSpec.Parse(OS.GetCmdlineUserArgs());
        _spec = _cli;
        foreach (var note in _spec.Warnings)
        {
            if (note.Category.Length == 0)
            {
                GD.Print(note.Message);
            }
            else
            {
                Log.Warn(note.Category, $"{note.Message}");
            }
        }

        // Every base path derives from the data root, so it is settled first.
        // Precedence: --data-root= beats CSVM_DATA_ROOT beats the repo root.
        _dataRoot = _repoRoot;
        var dataRootEnv = OS.GetEnvironment("CSVM_DATA_ROOT");
        if (!string.IsNullOrEmpty(dataRootEnv)) _dataRoot = Path.GetFullPath(dataRootEnv);
        if (_spec.DataRoot is { } dataRootArg) _dataRoot = Path.GetFullPath(dataRootArg);
        if (_dataRoot != _repoRoot)
            GD.Print($"data root: {_dataRoot} (repo root {_repoRoot})");

        // An override is used verbatim; everything else derives from the data root.
        var planesGamezPath = Path.Combine(_dataRoot, "extracted", "planes.zip");
        _zrdrPath = _spec.Zrdr ?? Path.Combine(_dataRoot, "extracted", "zrdr.zip");
        _soundsPath = _spec.Sounds ?? Path.Combine(_dataRoot, "extracted", "soundsh.zip");
        _interpPath = _spec.Interp ?? Path.Combine(_dataRoot, "extracted", "interp.json");
        _messagesPath = _spec.Messages ?? Path.Combine(_dataRoot, "extracted", "messages.json");
        _rofPath = _spec.Rof ?? Path.Combine(_dataRoot, "extracted", "rof");

        // The extraction tree's provenance check — at most one warning line, never a block.
        ExtractionStamp.Check(_dataRoot);

        // The drop-in writes statics every material built afterwards reads, so it is applied here
        // rather than carried as a session value.
        foreach (string name in _spec.TexOverrides)
        {
            TextureDropIn.SetScratchDir(_repoRoot);
            TextureDropIn.AddOverride(name);
        }
        if (_spec.TexCensus)
        {
            TextureDropIn.SetScratchDir(_repoRoot);
            TextureDropIn.EnableCensus(_spec.TexCensusFilter);
        }
        // Read by every archive built afterwards, for the same reason the drop-in's state is a
        // static: it settles once per launch and no consumer chooses it.
        TextureArchive.Mips = _spec.Mips;
        // A connected pad with stick drift steers the free camera and nudges the flight model,
        // which quietly makes a "deterministic" scripted run not one. SDL's hints don't help
        // (Godot 4.7 enumerates the pad regardless), so the switch is ours.
        if (_spec.PadsDisabled)
        {
            Pads.Disabled = true;
        }
        _captureDirector = new Testing.CaptureDirector(_spec);
        _gltfExporter = new Testing.GltfExporter(_spec);
        _pendingJoin = _spec.DebugJoin;
        _pendingWaves = _spec.DebugWaves;
        _pendingWingmen = _spec.DebugWingmen;
        // Built alongside the other process-scoped services, ahead of every probe's early quit
        // and of --dump-config: its constructor is what registers the five hitchMonitor.* keys.
        // See this file's docs/architecture.md entry.
        _hitchMonitor = new HitchMonitor();
        // Measured render time is opt-in per viewport and reads 0 until it is, so it is enabled once
        // here rather than per frame from ReportPerf (which used to own the call): the hitch record
        // needs the CPU/GPU split on every frame, not only on a --perf run.
        _viewportRid = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(_viewportRid, true);


        // The window is created without focus (no_focus in project.godot); an interactive
        // session asks for it explicitly instead, since setting the flag at runtime measured
        // not to hand focus back. See docs/verification.md's SHELL-13.
        if (!_spec.IsScripted)
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, false);
            DisplayServer.WindowMoveToForeground();
            Log.Debug("core", $"window: focus requested (interactive session)");
        }
        else
        {
            ScriptedWindow.Hide();
        }

        // --no-vsync uncaps the frame loop so frame/fps/script report work done rather than a
        // refresh cap, safe with the fixed clock since it steps one sim frame per rendered one.
        // The config read stays unconditional so it self-registers — see Utils/Config.cs's entry.
        bool vsyncOnByConfig = Config.GetBool("display.vsync", true);
        bool vsyncOff = _spec.NoVsync || !vsyncOnByConfig;
        if (vsyncOff)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
            string source = _spec.NoVsync ? "--no-vsync" : "display.vsync";
            Log.Info("perf", $"vsync off source={source} max_fps=0 — frame/fps/script report work done, not a refresh cap");
        }
        else
        {
            Log.Info("perf", $"vsync on — frame/fps/script are floored at the refresh interval");
        }

        // --debug-anim opens the call-site gates of the anim and sound families, so it is also the
        // legacy spelling of their console filter; an explicit --log= is applied after it and can
        // still narrow either one.
        if (_spec.DebugAnim)
        {
            Log.Configure("anim:debug,sound:debug");
        }
        foreach (string logSpec in _spec.LogSpecs)
        {
            Log.Configure(logSpec);
        }
        // Opened under the session shape's name (it names the file) and before anything else can
        // log. The sink always takes every category at every level; --log= only widens what the
        // console additionally shows.
        Log.Open(_repoRoot, _spec.ModeName);
        foreach (var (old, replacement) in _spec.Deprecated)
        {
            Log.Warn("core", $"deprecated flag={old} use={replacement}");
        }
        // Ahead of --dump-config, same reason as _hitchMonitor: registers the two
        // hitchSidecar.* keys. The fallback path only matters if Log.Open itself failed.
        string hitchLogPath = Log.SinkPath
            ?? Path.Combine(_repoRoot, ".scratch", "logs", $"{_spec.ModeName}-nolog.hitches.jsonl");
        _hitchSidecar = new HitchSidecar(hitchLogPath, _hitchMonitor.Last.Ring.Length);

        // --headless + --screenshot can never produce a frame: the dummy renderer's GetImage()
        // never returns, so the capture loop never counts down. Reject the combo here, before
        // any session builds, rather than let it hang as an orphan holding the log handle.
        if (_spec.ScreenshotPath != null && DisplayServer.GetName() == "headless")
        {
            Log.Error("core", $"--screenshot needs a real GPU context; --headless never renders a capturable frame — drop one of the two flags");
            GetTree().Quit(1);
            return;
        }
        // What a pure value cannot do: draw an unpinned seed from the clock. A deterministic run
        // (and the anim debugger) pins it; applied here too so the dump tools, which quit before
        // any session builds, draw the resolved master rather than a zero one.
        _processSeed = _spec.PinnedSeed ?? Rng.TimeSeed();
        _masterSeed = _processSeed;
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        LogMasterSeed();
        // Announce the whole resolved bundle on one line, so any capture or log carries the exact
        // conditions it was taken under instead of relying on the reader remembering what --det
        // implies. Every constituent is named with its value, including the ones a flag overrode.
        if (_spec.Det)
        {
            // The git-ignored dev tuning file would otherwise make a deterministic capture a
            // function of one machine's uncommitted state — see docs/verification.md's DET-8.
            // Pass --no-det to capture with your overrides applied.
            int dropped = Config.OverrideCount;
            Config.ClearOverrides();
            ulong liverySeed = _spec.PaintSeedExplicit ? _spec.PaintSeed : Rng.SeedFor(Rng.Paint);
            float dtMs = GameClock.FixedDt * 1000f;
            Log.Info("core", $"det clock=fixed dt_ms={dtMs:0.###} seed={_masterSeed} spawn={_spec.SpawnIndex} livery_seed={liverySeed} pads=off jitter={_spec.JitterDeg:0.###} config=defaults dropped_overrides={dropped} via={_spec.DetVia}");
        }
        else if (_spec.NoDet && (_spec.DetExplicit || _spec.ScriptedBy.Length > 0))
        {
            string wouldBe = _spec.DetExplicit ? "--det" : _spec.ScriptedBy;
            Log.Info("core", $"no-det: {wouldBe} would run deterministically — wall-clock sim clock, unpinned randomness seed={_masterSeed}");
        }
        // After the --det block, so a deterministic run reads the flag but not the tuning file that
        // ClearOverrides just dropped; before the early-quit probes below, so --run-tests and the
        // --dump-* wrappers are covered by the same gain an interactive launch gets.
        ApplyMasterVolume();
        // Prefer the unpacked sibling folder from ExtractAssets.ps1 -Unzip when it exists (loose
        // JSON/PNG/WAV: no zip decompression at load). Base (chapter-independent) paths resolve now;
        // the chapter-dependent gamez/texture/mission paths resolve per-session in StartSession.
        _planesGamezPath = SessionPaths.PreferUnzipped(planesGamezPath);
        if (_spec.Zrdr == null) { _zrdrPath = SessionPaths.PreferUnzipped(_zrdrPath); }
        if (_spec.Sounds == null) { _soundsPath = SessionPaths.PreferUnzipped(_soundsPath); }
        _probeRunner = new Testing.ProbeRunner(_repoRoot, _dataRoot, _zrdrPath, _soundsPath,
            _interpPath, _messagesPath, _planesGamezPath);

        // Registers the distance-fog params SceneBuilder's shaders reference; defaults are a
        // no-op until --fly's weather.json overrides them.
        // ⚠ Runs once here — GlobalShaderParameterAdd errors on a second call; a rebuild must Set.
        RenderingServer.GlobalShaderParameterAdd("csky_fog_color",
            RenderingServer.GlobalShaderParameterType.Vec3, new Vector3(0.69f, 0.69f, 0.69f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_range",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_alt",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        // The fullbright world's per-mission brightness from the weather's SUNLIGHT:
        // 1.0 = fullbright (no darkening) for static views / missions without weather; --fly
        // overrides it from WeatherState.WorldLight below.
        RenderingServer.GlobalShaderParameterAdd("csky_world_light",
            RenderingServer.GlobalShaderParameterType.Float, 1.0f);
        // The animated world's LIGHT_STATE point lights. Defaults to an empty set, so a session
        // with no lit animations renders exactly as it did before they existed.
        WorldLights.RegisterGlobals();
        // Registered before the --dump-* branches below, which build materials of their own:
        // registering after them left every dump run emitting a missing-global error.
        ShaderTime.RegisterGlobal();

        // --dump-markers: a pure-data report — print the marker rig tables and quit. Runs
        // whether or not a content arg was given; --headless makes it windowless. Each dump
        // quits with its own verdict, like --run-tests, never a false-clean exit code.
        if (_spec.DumpMarkers)
        {
            GetTree().Quit(_probeRunner.DumpMarkers(_spec) ? 0 : 1);
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader —
        // dump every def and assert no key went unmapped.
        if (_spec.DumpWeapons)
        {
            GetTree().Quit(_probeRunner.DumpWeapons(_spec) ? 0 : 1);
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints — a missing marker is a loud error here.
        if (_spec.DumpLoadout)
        {
            GetTree().Quit(_probeRunner.DumpLoadout(_spec) ? 0 : 1);
            return;
        }
        // --dump-flight: pure data again — no world and no model, just the zrdr stats stepped
        // through the manoeuvres the original was measured flying.
        if (_spec.DumpFlight)
        {
            GetTree().Quit(_probeRunner.DumpFlight(_spec) ? 0 : 1);
            return;
        }
        // --dump-mips: what the texture archive's mip chains actually hold, level by level, beside
        // the authored levels they should be — the before/after instrument for --mips=.
        if (_spec.DumpMips)
        {
            GetTree().Quit(_probeRunner.DumpMips(_spec) ? 0 : 1);
            return;
        }
        // --dump-ai: the five AI data families read straight off the extraction — no world, no
        // scene, no readers built for the families that don't have one yet (aiv/ai.zrd/zeppelins/
        // egen). See Probes.Ai.
        if (_spec.DumpAi)
        {
            GetTree().Quit(_probeRunner.DumpAi(_spec) ? 0 : 1);
            return;
        }

        // Exercises the wired modules once so Config's tuning registry is complete, then flags
        // any config.json key no tunable matched — data-free, so a typo is caught before flight.
        Config.WarmTuningRegistry();
        Config.ReportOrphans();
        // --dump-config: write a fully-populated tuning template (every registered key + its default,
        // nested by block) to the scratch folder and quit — the copy-and-edit source for config.json.
        if (_spec.DumpConfig)
        {
            string dumpPath = Path.Combine(_repoRoot, ".scratch", "config.dump.json");
            Config.DumpConfig(dumpPath);
            GD.Print($"config: wrote {Config.RegisteredCount}-key tuning template to ./.scratch/config.dump.json");
            GetTree().Quit();
            return;
        }

        // Gamepad hotplug: every input read polls Pads.Connected() fresh, so a pad plugged in
        // mid-game works the moment the engine reports it. Log the roster at launch and every
        // connect/disconnect so a silent pad is diagnosable from the console.
        if (Pads.Disabled)
            GD.Print("gamepad: off (--no-pads, or the --det bundle), ignoring every device (keyboard/scripted input only)");
        else
            Input.Singleton.JoyConnectionChanged += (device, connected) =>
                GD.Print(connected
                    ? $"gamepad connected: device {device} \"{Input.GetJoyName((int)device)}\" guid={Input.GetJoyGuid((int)device)}"
                    : $"gamepad disconnected: device {device}");
        var padsAtLaunch = Pads.Connected();
        if (Pads.Disabled)
        {
            // nothing more to report — the roster is deliberately empty
        }
        else if (padsAtLaunch.Count == 0)
            GD.Print("gamepad: none at launch (hotplug live — connect any time)");
        else
            foreach (int p in padsAtLaunch)
                GD.Print($"gamepad: device {p} \"{Input.GetJoyName(p)}\" guid={Input.GetJoyGuid(p)} info={Input.GetJoyInfo(p)}");

        SetupLighting();
        _camera = new Camera3D { Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? 62 : 50, Far = 40000f };
        AddChild(_camera);
        _orbit = new OrbitCamera(_camera);
        if (_spec.Yaw is { } argYaw) _orbit.Yaw = argYaw;
        if (_spec.Pitch is { } argPitch) _orbit.Pitch = argPitch;

        // --run-tests: the in-engine assertion suites. Dispatched here, after the camera exists (a
        // suite building a world resolves PLAYER_RANGE from it) and before any session is built —
        // the suites build exactly the world/plane each of them needs and nothing else.
        if (_spec.RunTests)
        {
            int code = _probeRunner.RunTestSuites(_spec, this, _camera, out _clock);
            GetTree().Quit(code);
            return;
        }

        // The F14 / --debug-fps readout, process-wide like the camera so it works at the
        // launchscreen too. Built after the --run-tests/--dump-* early exits, which render no
        // frame it would have anything to show.
        _perfHud = new UI.PerfHud
        {
            InitialMode = _spec.DebugFps == null ? UI.PerfHud.Mode.Off : UI.PerfHud.ParseMode(_spec.DebugFps),
            Monitor = _hitchMonitor,
        };
        AddChild(_perfHud);

        // No content-selecting arg (or explicit --menu): show the launchscreen. Its selection
        // derives the session spec and calls LaunchSession, so there is one downstream build
        // path; Esc from a menu-launched flight returns here (ReturnToMenu).
        if (_spec.ShowsMenu)
        {
            _menuDriven = true;
            ShowLaunchMenu();
            return;
        }
        LaunchSession();
    }

    public override void _Notification(int what)
    {
        // The APPLICATION_* pair, not WM_WINDOW_*: that's what a real focus change delivers
        // here. See docs/verification.md's SHELL-14 for the alt-tab-vs-minimise gotcha this was
        // measured against. Godot 4's constants are longs; _Notification hands us an int.
        if (what == (int)NotificationApplicationFocusOut)
        {
            SetFocusMuted(true);
        }
        else if (what == (int)NotificationApplicationFocusIn)
        {
            SetFocusMuted(false);
        }
    }

    // The root node's own teardown, reached on an ordinary quit
    // (GetTree().Quit() or the window's close button) — never on a kill/crash, which is what the
    // sidecar's flush-interval loss bound (HitchSidecar's own doc) covers instead.
    public override void _ExitTree()
    {
        _hitchSidecar.Flush();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            // While the launchscreen is up it owns Esc (back / quit from the Mode screen).
            if (_menu is { Visible: true })
                return;
            // Esc out of a menu-launched flight tears the world down and returns to the
            // launchscreen; a CLI-launched run just quits, as before.
            if (_menuDriven && _session is { InSession: true })
            {
                ReturnToMenu();
                return;
            }
            GetTree().Quit();
            return;
        }
        // F12 anywhere (orbit view or free flight): grab the current frame to a file.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            Testing.CaptureDirector.SaveScreenshot(GetViewport());
            return;
        }
        // F11 anywhere: print the mode's subject placement as ready-to-paste --pos=/--direction=
        // args, so a hand-framed orbit (or a spot found while flying) can be reproduced for a
        // deterministic --screenshot run.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            _captureDirector.PrintPlacement(_spec, _session?.Rigs ?? NoRigs, _camera, _orbit);
            return;
        }
        // F10 in the viewer: export the plane on screen — current livery and damage state baked
        // in — to a timestamped .glb under the repo's git-ignored Exports/ folder.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F10 })
        {
            var projectDir = ProjectSettings.GlobalizePath("res://");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, "..", "Exports"));
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir,
                $"crimsonskies_{_spec.PlaneName}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.glb");
            Testing.GltfExporter.Export(_session?.Plane, path);
            return;
        }
    }

    public override void _Process(double delta)
    {
        // The --run-tests clock is the only one this node owns; the session node advances its own
        // at the very top of the frame (ProcessPriority -1000, one notch ahead of this).
        if (_clock is { } clock)
        {
            clock.BeginFrame(delta);
        }
        // Publishes the frame's instant to the shaders so animated surfaces and the CPU sim
        // never disagree. Written unconditionally: with no session clock it runs on wall time,
        // so nothing stalls behind the menu.
        ShaderTime.Advance(ClockNow, delta);
        // Both instruments stay on wall time: an instrument that freezes with the thing it measures
        // reports nothing. One counter read per frame feeds both: the hitch monitor wants them
        // unaveraged and the --perf window wants them summed, but they are the same eight numbers.
        var counters = ReadFrameCounters();
        // Fires one QPC read before the stamp below, so the stall inflates THIS frame's wall
        // cost. Matched against HitchMonitor's own frame counter, never the sim frame: the
        // injector has to work with no session built at all.
        if (_spec.HitchInjectMs is float injectMs
            && _hitchMonitor.FrameCount + 1 == _spec.HitchInjectFrame)
        {
            InjectHitch(injectMs, _spec.HitchInjectAlloc);
        }
        // Detection is unconditional; logging is not — a hitch nobody watched for is what this
        // catches. Fed our own QPC pair, never Godot's post-processed `delta`, which measures
        // as a quantised constant.
        long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        double frameMs = _lastFrameStamp == 0
            ? 0
            : (stamp - _lastFrameStamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        _lastFrameStamp = stamp;
        // Closes the attribution window at the same instant the wall cost is stamped, so the
        // scopes that ran and the frame_ms they ran inside describe the same span. The session
        // node processes one notch ahead (-1000), so its declared work is already in.
        PerfSample.EndFrame();
        // B6: a trip queues its record (a cheap, preallocated copy) rather than writing anything
        // here — the sidecar's own Tick below drains the queue a few quiet frames later.
        if (_hitchMonitor.Tick(frameMs, counters))
        {
            _hitchSidecar.Enqueue(_hitchMonitor.Last);
        }
        _hitchSidecar.Tick(frameMs);
        // Same raw frameMs and the same counters read HitchMonitor
        // just judged, fed to the readout regardless of whether it is currently drawn — see
        // PerfHud.Tick's own doc comment.
        _perfHud.Tick(frameMs, counters);
        if (_spec.Perf)
            ReportPerf(delta, counters);

        _captureDirector.Tick(GetViewport(), GetTree(), _spec, ClockNow, _orbit, _camera,
            _session?.Plane, _menu is { Visible: true });
        _gltfExporter.Tick(_session?.Plane, GetTree(), _spec);
    }

    // Instantiates this launch's session node from the current _spec and
    // the persistent references, and runs its build. Returns the build's verdict; on false the
    // partial session node is left for the caller (the menu flow returns to the launchscreen, a
    // CLI launch leaves the log to tell the story, exactly as the single-root class did).
    private bool LaunchSession()
    {
        _session = new GameSession(_spec, new LauncherContext
        {
            RepoRoot = _repoRoot,
            DataRoot = _dataRoot,
            PlanesGamezPath = _planesGamezPath,
            ZrdrPath = _zrdrPath,
            SoundsPath = _soundsPath,
            InterpPath = _interpPath,
            MessagesPath = _messagesPath,
            RofPath = _rofPath,
            ProbeRunner = _probeRunner,
            CaptureDirector = _captureDirector,
            MasterSeed = _masterSeed,
            Camera = _camera,
            Orbit = _orbit,
            Sun = _sun,
            Env = _env,
            MenuDriven = _menuDriven,
            MenuPads = _menuPads,
        });
        AddChild(_session);
        bool built = _session.StartSession();
        // A build stalls the frame loop, and the frames either side of it are not neighbours, so
        // the hitch monitor drops its baseline here rather than reporting the build as a hitch.
        // Flushed first so nothing queued from before the build is held through it.
        _hitchSidecar.Flush();
        _hitchMonitor.Rearm();
        // C8: the build's own scopes (loads, material creation) belong to no frame, and the frame
        // that closes over the build would otherwise report them all at once.
        PerfSample.Reset();
        // D10: same reasoning as HitchMonitor.Rearm above — the build's own stall must never read
        // as the readout's worst recent frame.
        _perfHud.Rearm();
        return built;
    }

    private void SetupLighting()
    {
        _sun = new DirectionalLight3D
        {
            // The default bearing only, hand-picked so the plane model reads in the viewer, the
            // menu, and any mission with no weather.json. A flight with weather overwrites this
            // per zone-apply (WeatherRig.ApplyZone).
            RotationDegrees = new Vector3(-45, 150, 0),
            LightEnergy = 1.6f,
            // Off: the world is built fullbright and unshaded, so the only thing a shadow pass
            // reaches is one aircraft shadowing another — a non-original effect the original's
            // own projected-blob shadow does not have either.
            ShadowEnabled = false,
        };
        AddChild(_sun);

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
        };
        AddChild(new WorldEnvironment { Environment = _env });
    }

    // Shows the launchscreen (building it on first use) and wiring its Launch/Quit
    // callbacks. Re-shown by ReturnToMenu after Esc-from-flight.
    private void ShowLaunchMenu()
    {
        if (_menu == null)
        {
            _menu = LaunchMenu.Build(_zrdrPath, _dataRoot);
            _menu.Launch = StartSessionFromMenu;
            _menu.Quit = () => GetTree().Quit();
            AddChild(_menu);
        }
        _menu.ShowMenu(_spec.MenuStartScreen);
        // --debug-join=N: synthesize N extra device-less players so the splitscreen aircraft
        // select can be screenshot on a one-controller machine (they can never act, so the
        // shot is deterministic; the last one starts locked to show both panel states).
        if (_pendingJoin > 0)
        {
            _menu.DebugJoin(_pendingJoin);
            _pendingJoin = 0; // one-shot: a return to the menu keeps whoever really joined
        }
        // --debug-waves=/--debug-wingmen=: the same screenshot aid for the Instant Action wizard's
        // own screens.
        if (_pendingWaves > 0)
        {
            _menu.DebugWaves(_pendingWaves);
            _pendingWaves = 0;
        }
        if (_pendingWingmen > 0)
        {
            _menu.DebugWingmen(_pendingWingmen);
            _pendingWingmen = 0;
        }
    }

    // The launchscreen's players locked their picks: derive this session's spec, bind pads,
    // start the session. A build failure returns to the menu with a note instead of a blank
    // screen.
    // ⚠ Derive the spec from _cli, never the outgoing _spec, so nothing the last session
    // settled leaks into this one. Pads are the deliberate exception: they come from the join
    // flow, not args, so they stay session state rather than a spec field.
    private void StartSessionFromMenu(string chapter, IReadOnlyList<LaunchMenu.PlayerChoice> players,
        MenuMode mode, InstantActionDef? iaDef)
    {
        var planes = new List<string>(players.Count);
        foreach (var p in players)
        {
            planes.Add(p.PlaneNode);
        }
        _spec = SessionSpec.FromMenu(_cli, chapter, planes, mode, iaDef);
        // Step the master so flying again is a new mission rather than a replay of the last one:
        // without this every launchscreen relaunch re-derives the same spawn, opposition and
        // liveries from the one process draw. A pinned run (--seed=, --det, --scripted-by, the anim
        // lab) holds still, which is what keeps the goldens and the perf harnesses reproducible.
        if (!_spec.SeedPinned)
        {
            _sortie++;
            _masterSeed = Rng.SortieSeed(_processSeed, _sortie);
        }
        LogMasterSeed();
        // Honour the join flow's device binding rather than re-deriving it from the roster: the
        // pad that joined as P2 in the menu must be the pad that flies P2. Single player keeps the
        // any-pad policy (null), so every connected pad flies the one plane, as before.
        _menuPads = null;
        if (_spec.Players > 1)
        {
            _menuPads = new int[_spec.Players][];
            for (int i = 0; i < _spec.Players; i++)
            {
                _menuPads[i] = players[i].Pads;
            }
        }
        _menu!.HideMenu();
        if (!LaunchSession())
        {
            ReturnToMenu();
            _menu.ShowError($"Could not load {chapter} / {string.Join(", ", _spec.PlaneNames)} — see the log.");
        }
    }

    // Prints the master the next session will draw from. Per session rather than per process
    // because an unpinned run advances it: the seed a mission actually flew on is the one worth
    // having in the log, so an interesting one can be pinned with `--seed=`.
    private void LogMasterSeed()
    {
        string how = _spec.SeedPinned ? "pinned" : $"sortie {_sortie}, --seed=N to pin";
        GD.Print($"rng: master seed {_masterSeed} ({how})");
    }

    // Frees the current session node and shows the launchscreen again — the in-process
    // rebuild path for Esc-from-flight and failed builds. The whole session subtree hangs under the
    // node, so `QueueFree` tears it down; the non-child duties (the published clock, the world
    // lights, the session texture archive, the main-camera restore) run in the node's
    // `_Notification` on `NotificationExitTree`. The camera / lights / shader globals
    // persist on `this`.
    private void ReturnToMenu()
    {
        if (_session != null)
        {
            _session.QueueFree();
            _session = null;
        }
        // Same reason as the build in LaunchSession: a teardown legitimately stalls the loop.
        _hitchSidecar.Flush();
        _hitchMonitor.Rearm();
        PerfSample.Reset();
        _perfHud.Rearm();
        ShowLaunchMenu();
    }

    // Settles the master output gain: --volume= if given, else audio.volume, else silent.
    // Deliberately not --mute: at volume 0 both audio paths still load, play, count and log, so
    // the run is silent but not blind. A bus write for the same reason SetFocusMuted is one —
    // see this file's docs/architecture.md entry, which also covers why the config read stays
    // unconditional so it self-registers for --dump-config.
    private void ApplyMasterVolume()
    {
        float volume = Config.GetFloat("audio.volume", MasterVolumeDefault);
        string source = "config";
        if (_spec.Volume is { } asked)
        {
            volume = asked;
            source = "--volume";
        }
        // Full volume is the bus's own resting state, so leaving it alone keeps a full-volume
        // launch byte-identical in both output and console log.
        if (Mathf.IsEqualApprox(volume, MasterVolumeUnattenuated))
        {
            return;
        }
        AudioServer.SetBusVolumeDb(MasterBus, Mathf.LinearToDb(Mathf.Max(volume, MasterVolumeFloor)));
        string note = volume <= 0f ? " — sounds still load, play, count and log" : "";
        Log.Info("sound", $"master volume={volume:0.###} via={source}{note}");
    }

    // Mutes/unmutes the master bus and gates pad reads, on window focus. Idempotent —
    // the notification can arrive more than once — and it only ever clears a mute it set itself,
    // so it cannot stomp on a mute from anywhere else.
    private void SetFocusMuted(bool muted)
    {
        if (_focusMuted == muted)
        {
            return;
        }
        _focusMuted = muted;
        AudioServer.SetBusMute(MasterBus, muted);
        // Pad reads follow focus (Pads.For), so a stick drifting while alt-tabbed cannot fly
        // the plane. The roster deliberately does not — see Pads.cs's docs/architecture.md
        // entry. Keyboard needs no gate: Godot releases held keys on focus loss.
        Pads.Focused = !muted;
        GD.Print(muted
            ? "focus: lost — audio muted, pad reads gated"
            : "focus: regained — audio restored, pad reads live");
    }

    // Samples the engine's eight per-frame counters once, for both instruments. The two
    // `TIME_*` monitors are seconds and are converted here, so everything downstream of this
    // is in milliseconds. Read at priority -999, so (like `delta` itself) these describe the
    // frame that just ended rather than the one being built; the two agree with each other, which
    // is what a hitch record needs.
    private FrameCounters ReadFrameCounters() => new(
        ScriptMs: 1000 * Performance.GetMonitor(Performance.Monitor.TimeProcess),
        RenderCpuMs: RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewportRid),
        GpuMs: RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewportRid),
        PhysicsMs: 1000 * Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess),
        Draws: (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
        Prims: (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
        Nodes: (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
        MemBytes: (long)Performance.GetMonitor(Performance.Monitor.MemoryStatic));

    // --hitch-inject=: burns wall time synchronously for about `ms`, so a stall of known
    // magnitude exists to verify against. The busy-wait form proves the timing path; `alloc`
    // burns the same time allocating 4 KB buffers instead, the only way to move the GC columns
    // on demand.
    // ⚠ Never wrap this in a PerfSample scope. An injected fault must show as unattributed
    // time, not a breadcrumb mistaken for the thing under test.
    private void InjectHitch(float ms, bool alloc)
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

    // --perf: the headless stand-in for the editor's profiler, meaned over the window so a
    // single hitch doesn't read as a regression — A/B two builds by comparing the same line.
    // `physics` moves independently of `frame`/`fps`, which sit pinned at the vsync cap; trust
    // it as an A/B ratio, not an absolute, same caveat as `script` (docs/verification.md's
    // PERF-1). `max_ms`/`p95_ms` answer "how bad did it get", not "how bad on average"; no
    // `p99_ms` since a 60-sample window's nearest-rank p99 is just `max_ms` (Perf95Index).
    private void ReportPerf(double delta, in FrameCounters counters)
    {
        _perfFrames++;
        _perfFrameMs[_perfFrames - 1] = delta * 1000;
        _perfClock += delta;
        _perfProcess += counters.ScriptMs;
        _perfPhysics += counters.PhysicsMs;
        _perfCpuRender += counters.RenderCpuMs;
        _perfGpu += counters.GpuMs;
        // Counts, averaged like every ms term, but with no timing noise in them: a scene that
        // starts drawing more says so exactly, where an ms term has to clear a noise band first.
        _perfDraws += counters.Draws;
        _perfPrims += counters.Prims;
        _perfNodes += counters.Nodes;
        _perfMem += counters.MemBytes;
        if (_perfFrames < PerfWindowFrames)
        {
            return;
        }
        // Locals, not one very long expression: Log takes a single interpolated string (two
        // concatenated ones are a plain string, which would already have formatted its floats in
        // the current culture and so does not compile against it).
        double n = _perfFrames;
        long simFrame = ClockNow?.Frame ?? 0;
        double wallMs = 1000 * _perfClock;
        double fps = n / _perfClock;
        double frameMs = wallMs / n;
        // Every ms term is already in milliseconds here: ReadFrameCounters does the seconds-to-ms
        // conversion on Godot's two TIME_* monitors once, at the read.
        double scriptMs = _perfProcess / n;
        double renderCpuMs = _perfCpuRender / n;
        double gpuMs = _perfGpu / n;
        double physicsMs = _perfPhysics / n;
        double draws = _perfDraws / n;
        double prims = _perfPrims / n;
        double nodes = _perfNodes / n;
        double memMb = _perfMem / n / (1024 * 1024);
        System.Array.Copy(_perfFrameMs, _perfFrameMsSorted, PerfWindowFrames);
        System.Array.Sort(_perfFrameMsSorted);
        double maxMs = _perfFrameMsSorted[PerfWindowFrames - 1];
        double p95Ms = _perfFrameMsSorted[Perf95Index];
        Log.Info("perf", $"window sim_frame={simFrame} frames={_perfFrames} wall_ms={wallMs:0.00} fps={fps:0.0} frame_ms={frameMs:0.00} script_ms={scriptMs:0.00} render_cpu_ms={renderCpuMs:0.00} gpu_ms={gpuMs:0.00} physics_ms={physicsMs:0.00} draws={draws:0.0} prims={prims:0.0} nodes={nodes:0.0} mem_mb={memMb:0.00} max_ms={maxMs:0.00} p95_ms={p95Ms:0.00}");
        _perfClock = 0; _perfFrames = 0; _perfProcess = _perfGpu = _perfCpuRender = _perfPhysics = 0;
        _perfDraws = _perfPrims = _perfNodes = _perfMem = 0;
    }
}

/// <summary>What a session node needs from the launcher: the settled base paths, the persistent
/// rendering nodes it configures but does not own, the process-scoped services, and the join
/// flow's session state. A snapshot per launch — <see cref="MenuPads"/> is the field that varies.
/// ⚠ Not a second home for args: a new flag is a <see cref="SessionSpec"/> change, never a
/// context field.</summary>
public sealed class LauncherContext
{
    public required string RepoRoot { get; init; }
    public required string DataRoot { get; init; }
    public required string PlanesGamezPath { get; init; }
    public required string ZrdrPath { get; init; }
    public required string SoundsPath { get; init; }
    public required string InterpPath { get; init; }
    public required string MessagesPath { get; init; }
    public required string RofPath { get; init; }
    public required Testing.ProbeRunner ProbeRunner { get; init; }
    public required Testing.CaptureDirector CaptureDirector { get; init; }
    public required ulong MasterSeed { get; init; }
    public required Camera3D Camera { get; init; }
    public required OrbitCamera Orbit { get; init; }
    public required DirectionalLight3D Sun { get; init; }
    public required Godot.Environment? Env { get; init; }
    /// <summary>Whether this process launched into the menu — Esc from flight returns there
    /// instead of quitting (the flight HUD's exit hint reads it too).</summary>
    public required bool MenuDriven { get; init; }
    /// <summary>Per-player pad binding from the launchscreen's join flow (null = derive from the
    /// connected roster, which is what every CLI launch does).</summary>
    public required int[][]? MenuPads { get; init; }
}
