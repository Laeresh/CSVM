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
/// <see cref="LauncherContext"/>; the boards' Exit frees it (<see cref="ReturnToMenu"/>) and their
/// Restart on a mission frees and rebuilds it (<see cref="RestartSession"/>). Both
/// interactive paths in draw the load screen and build a frame later. Full arg reference:
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

    // Wall seconds per always-on `[perf] rate` line. Wall time and NOT a frame count, unlike
    // PerfWindowFrames above: a frame-count window stretches exactly as the rate falls, so at
    // 1 fps a 60-frame window would report once a minute and describe the collapse least where
    // it matters most. Ten seconds is quiet enough to leave always on beside the rest of the
    // log and short enough that a drop is placed within the sortie. TUNE.
    private const double RateWindowSeconds = 10;

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

    // TUNE, and the FALLBACK only: a flown mission overwrites this per zone from its own pushed-out
    // fog far (WeatherRig.ApplyEnhancedLighting), so shadows end where that zone's haze does. This
    // value is what a session with no weather.json gets, and it sits in the middle of the pushed
    // range the shipped zones resolve to.
    private const float EnhancedShadowMaxDistance = 6000f;

    // TUNE, judged at the controls, and the pair trades against each other: lower values put
    // dithered acne over every terrain triangle at C1's 25° sun, higher ones dissolve a hangar's
    // shadow along with it. These keep the building and aircraft silhouettes with no acne left.
    private const float EnhancedShadowBias = 0.05f;
    private const float EnhancedShadowNormalBias = 1.25f;

    // TUNE. Fractions of the distance above, tighter than Godot's 0.1/0.2/0.5 because the shadows
    // a player reads are the aircraft's own and the buildings it passes, all inside the first few
    // hundred metres; the outer cascades only have to carry a skyline into the haze.
    private const float EnhancedShadowSplit1 = 0.06f;
    private const float EnhancedShadowSplit2 = 0.17f;
    private const float EnhancedShadowSplit3 = 0.42f;

    // TUNE. Screen-space reflection on the glossy water arm: the step count buys reflection length
    // along the ray, the fades hide where a ray runs off the screen or past the depth buffer.
    private const int EnhancedSsrMaxSteps = 64;
    private const float EnhancedSsrFadeIn = 0.15f;
    private const float EnhancedSsrFadeOut = 2.0f;
    private const float EnhancedSsrDepthTolerance = 0.2f;

    // TUNE, judged at the controls on C2/C5. Godot's own default (1.0 m) reads a building's own
    // trim but misses the wider contact shading a street canyon wants at this world's scale
    // (buildings tens of metres tall, streets a similar width); this radius picks up a block's
    // base and a hangar's corner without darkening open tarmac.
    private const float EnhancedSsaoRadius = 2.5f;

    // TUNE, judged at the controls: Godot's defaults (intensity 2.0, power 1.5) already read as
    // grounded contact shading rather than a grey wash at this radius, so both are kept.
    private const float EnhancedSsaoIntensity = 2.0f;
    private const float EnhancedSsaoPower = 1.5f;

    // TUNE, Godot defaults: detail keeps small-scale creases (window mullions, girders) from
    // being swallowed by the coarse term above; horizon and sharpness are the denoise pair that
    // keeps the depth-buffer edges from shimmering worse than the effect is worth.
    private const float EnhancedSsaoDetail = 0.5f;
    private const float EnhancedSsaoHorizon = 0.06f;
    private const float EnhancedSsaoSharpness = 0.98f;

    // TUNE, judged at the controls against C21's contract (only the glow-arm sprites exceed 1.0
    // in the HDR buffer). A threshold of 1.0 blooms exactly them; bloom stays 0 so nothing below
    // threshold glows, and screen blend keeps a flare's halo additive without blowing its own
    // core out further.
    private const float EnhancedGlowHdrThreshold = 1.0f;
    private const float EnhancedGlowBloom = 0.0f;
    private const float EnhancedGlowIntensity = 0.9f;
    private const float EnhancedGlowStrength = 1.1f;
    private const Godot.Environment.GlowBlendModeEnum EnhancedGlowBlendMode =
        Godot.Environment.GlowBlendModeEnum.Screen;

    // TUNE. Scale and cap on the values the glow pass reads before it thresholds them; wide enough
    // that a saturated flare core (255 before the tonemap) still separates from its own falloff.
    private const float EnhancedGlowHdrScale = 2.0f;
    private const float EnhancedGlowHdrLuminanceCap = 8.0f;

    // TUNE, judged at the controls against a C4 horizon, a C1 horizon and C5 at night: AgX rolls
    // off the far-ridge washout the authored sun energy produces (Wave B) while keeping the night
    // city's contrast, where Filmic read flatter. Exposure stays neutral; the AgX-specific white
    // point is what recovers the horizon rather than the general TonemapWhite, which AgX ignores.
    private const Godot.Environment.ToneMapper EnhancedTonemapMode = Godot.Environment.ToneMapper.Agx;
    private const float EnhancedTonemapExposure = 1.0f;
    private const float EnhancedTonemapAgxWhite = 6.0f;
    private const float EnhancedTonemapAgxContrast = 1.0f;

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
    // --debug-preset=, same one-shot hold. −1 rather than 0 because preset 0 is a real request.
    private int _pendingPreset = -1;
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
    // GameSession node, freed again by ReturnToMenu or RestartSession. The camera, lights and
    // global shader params live on `this` and persist across sessions.
    private GameSession? _session; // the current session node (null at the launchscreen)
    private LaunchMenu? _menu;     // the in-game launchscreen (shown on a no-content-arg launch)
    private bool _menuDriven;      // launched into the menu → Esc from flight returns here, not quit
    // The score, and the archive it streams from. Both are process-lifetime, unlike the
    // build-scoped SessionArchives.Sounds: one channel has to survive a mission launch, or the
    // cabin track would restart every time the player left a board. See docs/org/music.md.
    private MusicPlayer? _music;
    private SoundArchive? _musicArchive;
    private System.Random _musicRng = new();
    // The profile a just-ended campaign mission belongs to, acted on at the top of the next frame:
    // the mission ends inside the session's own physics step, which is no place to free it.
    private (string Profile, CampaignMissionResult Result)? _pendingDebrief;

    // The load screen and the deferred build behind it (BeginLaunch → _Process). A build is one
    // synchronous block, so the screen has to be DRAWN before it starts: _launchFramesWaited counts
    // the frames since the request and the build runs on the first one that proves a frame rendered.
    // ⚠ Null/-1 means no launch is owed; a CLI launch does not come through here at all.
    private CanvasLayer? _loadLayer;
    private int _launchFramesWaited = -1;

    private double _perfClock;
    private int _perfFrames;
    private double _perfProcess, _perfGpu, _perfCpuRender, _perfPhysics;
    private double _perfDraws, _perfPrims, _perfNodes, _perfMem;

    // The always-on rate window (ReportRate). Separate accumulators from the --perf ones above
    // rather than shared: those are opt-in and reset on a frame count, these run every session.
    private double _rateWallMs;
    private double _rateWorstMs;
    private int _rateFrames;

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

        // Both ends of the physics-tick bracket (see PhysicsTickCost). At the launcher rather than
        // the session, because the pair must survive a session rebuild for the tick rate either
        // side of one to be comparable.
        AddChild(PhysicsTickBracket.Make(false));
        AddChild(PhysicsTickBracket.Make(true));

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
        _pendingPreset = _spec.DebugPreset;
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

        // ⚠ The driver and method IN USE, never the project setting: a machine that fell back off
        // Forward+/Vulkan gets none of the export's baked pipelines, and nothing else in the log
        // would say so. Ahead of the vsync line, whose meaning rests on the refresh rate here.
        Log.Info("perf", $"gpu={Testing.GoldenShot.Adapter()} driver={RenderingServer.GetCurrentRenderingDriverName()} method={RenderingServer.GetCurrentRenderingMethod()} refresh_hz={DisplayServer.ScreenGetRefreshRate():0.#}");

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
        // Named while the run is live, because a crash never reaches the mirror in _ExitTree and
        // this is then the only pointer to the traces our own sink cannot see.
        Log.Info("core", $"engine log={Path.Combine(OS.GetUserDataDir(), "logs", "godot.log")} (mirrored beside this one on quit)");
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
        // Before the first PreferUnzipped call and process-wide, so every later resolution (the
        // chapter paths in StartSession, the menu pages' own lookups) takes the same asset shape.
        SessionPaths.ForceZipped = _spec.ZipAssets;
        if (_spec.ZipAssets)
        {
            Log.Info("core", $"assets: --zip-assets — reading .zip archives, ignoring unpacked folders");
        }
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
        // The graphics EffectsLevel's one global: the clutter fade's squared distance scale, 0
        // when the fade is switched off (never fades, clutter draws out to the fog).
        float clutterFadeScaleSq = Utils.EffectsLevel.ResolveClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterAdd(Utils.EffectsLevel.ShaderParam,
            RenderingServer.GlobalShaderParameterType.Float, clutterFadeScaleSq);
        string clutterFarFade = Utils.EffectsLevel.ClutterFarFadeEnabled() ? "true" : "false";
        Log.Info("world", $"clutter fade: {Utils.EffectsLevel.FadeKey}={clutterFarFade} {Utils.EffectsLevel.Key}={Config.GetString(Utils.EffectsLevel.Key, Utils.EffectsLevel.Default)} scale_sq={clutterFadeScaleSq}");
        // Resolved after the --det block above, so a user config's graphics.mode is dropped by
        // ClearOverrides the same way EffectsLevel's is; --graphics= bypasses Config outright and
        // survives it. One resolution point read by every later scene builder.
        bool graphicsEnhanced = Utils.GraphicsMode.Resolve(_spec.GraphicsMode);
        Log.Info("world", $"graphics mode: {Utils.GraphicsMode.Key}={(graphicsEnhanced ? "enhanced" : "original")}");
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

        // The music channel, once per process and after every early-quit probe: one player that
        // outlives every session, over a sound archive of its own for the same reason (D37's
        // wiring contract, step 1).
        BuildMusic();

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
        _musicArchive?.Dispose();
        _musicArchive = null;
        MirrorEngineLog();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            // While the launchscreen is up it owns Esc (back / quit from the Mode screen).
            if (_menu is { Visible: true })
                return;
            // ⚠ Esc no longer leaves a live flight; it opens the pause board, whose Exit item does.
            // FlightController polls it as a pause toggle, so nothing is done here.
            if (_session is { InSession: true })
                return;
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
        // Fed the same wall cost the monitor above gets, and unconditional where --perf is opt-in:
        // the rate a player actually saw is the one thing a report from someone else's machine
        // cannot be reconstructed without.
        ReportRate(frameMs);
        // Early-quit probes do not construct the readout, but Godot may process one shutdown frame.
        _perfHud?.Tick(frameMs, counters);
        if (_spec.Perf)
            ReportPerf(delta, counters);

        // Wall time, like the instruments above: the score is not part of the simulation, and a
        // paused or stepped session must not stall a fade halfway.
        _music?.Tick((float)delta, _musicRng);

        _captureDirector.Tick(GetViewport(), GetTree(), _spec, ClockNow, _orbit, _camera,
            _session?.Plane, _menu is { Visible: true });
        _gltfExporter.Tick(_session?.Plane, GetTree(), _spec);

        // A campaign mission that ended during the session's own step: free it and reopen the
        // launchscreen on the debrief, here where a QueueFree is safe.
        if (_pendingDebrief is { } debrief)
        {
            _pendingDebrief = null;
            OpenDebrief(debrief.Profile, debrief.Result);
        }

        // Last in the frame, where the build used to happen anyway: the launchscreen and the boards
        // are children, so they process AFTER this node, and a build they asked for landed here.
        RunOwedLaunch();
    }

    // The process's one music channel and the archive it streams from. Everything here is
    // optional: an install without soundsh or without a readable sounds.json leaves the game
    // silent, which is what a missing extraction has always meant.
    /// <summary>Copies Godot's own log next to ours in <c>.scratch/logs/</c> on an ordinary quit,
    /// so one folder is the whole bug report. The file sink takes <see cref="Log"/> calls only, so
    /// a managed exception escaping a callback reaches the engine's log and NOT ours.
    /// ⚠ Best effort throughout, and never on a crash: the engine holds the file open, and nothing
    /// here may take the quit with it.</summary>
    private void MirrorEngineLog()
    {
        if (Log.SinkPath is not { } sinkPath)
        {
            return;
        }
        string source = Path.Combine(OS.GetUserDataDir(), "logs", "godot.log");
        string target = Path.ChangeExtension(sinkPath, ".godot.log");
        try
        {
            if (!File.Exists(source))
            {
                return;
            }
            // Share ReadWrite: the engine's own writer is still open on it, and a plain
            // File.Copy would fail on Windows against that handle.
            using var src = new FileStream(source, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            using var dst = new FileStream(target, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read);
            src.CopyTo(dst);
            Log.Info("core", $"engine log mirrored from={source} to={target} bytes={dst.Length}");
        }
        catch (System.Exception e) when (e is IOException or System.UnauthorizedAccessException
                                             or System.NotSupportedException)
        {
            Log.Warn("core", $"engine log mirror failed from={source} error={e.GetType().Name}: {e.Message}");
        }
    }

    private void BuildMusic()
    {
        try
        {
            _musicArchive = new SoundArchive(_soundsPath);
            _music = new MusicPlayer(SoundDefs.Load(_zrdrPath), SoundDefs.LoadGroups(_zrdrPath))
            {
                Loader = (def, looped) => _musicArchive.Find(def.WavName, looped, warn: false),
            };
            AddChild(_music);
            _musicRng = new System.Random((int)(_masterSeed & 0x7fffffff));
        }
        catch (System.Exception e)
        {
            _music = null;
            _musicArchive = null;
            Log.Warn("sound", $"music: no channel this process ({e.GetType().Name}: {e.Message})");
        }
    }

    // The deferred half of BeginLaunch: builds once the load screen has had a frame to render.
    // A QueueFree'd session is gone by now too, so the outgoing world's exit-tree duties (the
    // published clock, the world lights, the camera restore) cannot land on top of the new one.
    private void RunOwedLaunch()
    {
        if (_launchFramesWaited < 0 || _launchFramesWaited++ < 1)
        {
            return;
        }
        _launchFramesWaited = -1;
        bool built = LaunchSession();
        // In the same tick the build returned, before anything renders: a load screen left up for
        // a frame would draw over the first frame of the world, and over a --screenshot capture.
        HideLoadScreen();
        if (built || !_menuDriven)
        {
            return; // a CLI launch leaves the log to tell the story, as it always did
        }
        ReturnToMenu();
        _menu!.ShowError($"Could not load {_spec.Chapter} / {string.Join(", ", _spec.PlaneNames)} — see the log.");
    }

    // Shows the load screen and owes a build from the next frame. Every interactive path in (the
    // launchscreen's Fly, the boards' Restart) comes through here; the CLI launch in _Ready does
    // not, so no scripted or golden run gains a frame it did not have before.
    private void BeginLaunch()
    {
        // The one place the session leaves the boards for a mission, so the one place the menu
        // score stops. What plays next is the mission's own business: a campaign mission cues
        // prebattle from its objectives graph, and Instant Action ships silent (docs/org/music.md).
        _music?.Stop();
        ShowLoadScreen(_spec.CampaignProfile != null);
        _launchFramesWaited = 0;
    }

    // The load screen over the whole window, on the board layer. Campaign launches take the
    // original's chart sheet and everything else its blackboard (docs/org/loading-screen.md).
    private void ShowLoadScreen(bool campaign)
    {
        _loadLayer = new CanvasLayer { Name = "load_board", Layer = UI.HudLayers.Board };
        _loadLayer.AddChild(UI.LoadBoard.Build(
            _dataRoot, campaign, $"{_spec.Chapter}   ·   {LaunchSubject()}"));
        AddChild(_loadLayer);
    }

    // What the load screen calls this flight: an Instant Action mission by the wizard's own name
    // for it ("Attacking a Zeppelin"), anything else by its mode. ⚠ Not ModeName — that is the log
    // file's and the startup line's internal tag ("fly", "stunt"), which is not a player's word.
    private string LaunchSubject()
    {
        if (_spec.IaDef is { } def)
        {
            return Mech3.InstantAction.MissionTypeLabel(def.MissionType);
        }
        return _spec.Versus ? "Dogfight" : "Free Flight";
    }

    private void HideLoadScreen()
    {
        if (_loadLayer == null)
        {
            return;
        }
        RemoveChild(_loadLayer);
        _loadLayer.QueueFree();
        _loadLayer = null;
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
            ExitSession = ExitSession,
            RestartSession = RestartSession,
            ReturnToCabin = _menuDriven
                ? (profile, result) => _pendingDebrief = (profile, result)
                : null,
            Music = _music,
        });
        AddChild(_session);
        bool built = _session.StartSession();
        // A build stalls the frame loop, and the frames either side of it are not neighbours, so
        // the hitch monitor drops its baseline here rather than reporting the build as a hitch.
        // Flushed first so nothing queued from before the build is held through it.
        _hitchSidecar.Flush();
        _hitchMonitor.Rearm();
        RearmRate();
        // C8: the build's own scopes (loads, material creation) belong to no frame, and the frame
        // that closes over the build would otherwise report them all at once.
        PerfSample.Reset();
        // Same boundary for the tick bracket: a build that spans the tail leaves a half-open tick
        // whose next close would charge the whole build to one step.
        PhysicsTickCost.Reset();
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
            // Off in the faithful path: the world is built fullbright and unshaded, so the only
            // thing a shadow pass reaches is one aircraft shadowing another, and the original's own
            // projected-blob shadow is not a shadow map either. Enhanced mode turns it on below.
            ShadowEnabled = false,
        };
        // Enhanced mode alone: a lit world has surfaces a shadow pass can land on.
        if (GraphicsMode.Enhanced)
            EnableSunShadows(_sun);
        AddChild(_sun);

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
        };
        // Enhanced mode alone: SSAO reads ambient light, which the faithful path never has, so
        // it has nothing to modulate there. The cockpit pass duplicates this Environment at build
        // time (CockpitOverlay.NewOverlay), so its 100 m interior inherits the same settings.
        if (GraphicsMode.Enhanced)
        {
            _env.SsaoEnabled = true;
            _env.SsaoRadius = EnhancedSsaoRadius;
            _env.SsaoIntensity = EnhancedSsaoIntensity;
            _env.SsaoPower = EnhancedSsaoPower;
            _env.SsaoDetail = EnhancedSsaoDetail;
            _env.SsaoHorizon = EnhancedSsaoHorizon;
            _env.SsaoSharpness = EnhancedSsaoSharpness;
            EnableWaterReflections(_env);
            EnableGlowAndTonemap(_env);
        }
        AddChild(new WorldEnvironment { Environment = _env });
    }

    // Every setting here is TUNE: nothing in the original authors a shadow map, so there is no
    // decoded magnitude to match. Four splits because the useful range spans an aircraft's own
    // shadow a few metres below it and a skyline several kilometres out.
    private void EnableSunShadows(DirectionalLight3D sun)
    {
        sun.ShadowEnabled = true;
        sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        sun.DirectionalShadowMaxDistance = EnhancedShadowMaxDistance;
        sun.DirectionalShadowSplit1 = EnhancedShadowSplit1;
        sun.DirectionalShadowSplit2 = EnhancedShadowSplit2;
        sun.DirectionalShadowSplit3 = EnhancedShadowSplit3;
        sun.DirectionalShadowBlendSplits = true;
        sun.ShadowBias = EnhancedShadowBias;
        sun.ShadowNormalBias = EnhancedShadowNormalBias;
    }

    // Screen-space reflection, for the one glossy population in the world: the water surfaces
    // SceneBuilder.ClassifySurface names. Every other enhanced surface is matte, so nothing else
    // can reflect. ⚠ SSR reflects only what the camera already draws; content off-screen or behind
    // the near plane has no reflection at all.
    private void EnableWaterReflections(Godot.Environment env)
    {
        env.SsrEnabled = true;
        env.SsrMaxSteps = EnhancedSsrMaxSteps;
        env.SsrFadeIn = EnhancedSsrFadeIn;
        env.SsrFadeOut = EnhancedSsrFadeOut;
        env.SsrDepthTolerance = EnhancedSsrDepthTolerance;
    }

    // Enhanced mode alone: with a lit world, sun, shadows and real light energy feeding the HDR
    // colour buffer, values can exceed 1.0 and clip instead of rolling off, and C21's glow-arm
    // sprites are the only surfaces meant to bloom. The cockpit pass duplicates this Environment
    // at build time (CockpitOverlay.NewOverlay), so its own tonemap matches the world pass exactly.
    private void EnableGlowAndTonemap(Godot.Environment env)
    {
        env.GlowEnabled = true;
        env.GlowHdrThreshold = EnhancedGlowHdrThreshold;
        env.GlowBloom = EnhancedGlowBloom;
        env.GlowIntensity = EnhancedGlowIntensity;
        env.GlowStrength = EnhancedGlowStrength;
        env.GlowBlendMode = EnhancedGlowBlendMode;
        env.GlowHdrScale = EnhancedGlowHdrScale;
        env.GlowHdrLuminanceCap = EnhancedGlowHdrLuminanceCap;
        env.TonemapMode = EnhancedTonemapMode;
        env.TonemapExposure = EnhancedTonemapExposure;
        env.TonemapAgxWhite = EnhancedTonemapAgxWhite;
        env.TonemapAgxContrast = EnhancedTonemapAgxContrast;
    }

    // Shows the launchscreen (building it on first use) and wiring its Launch/Quit
    // callbacks. Re-shown by ReturnToMenu after Esc-from-flight.
    private void ShowLaunchMenu()
    {
        if (_menu == null)
        {
            _menu = LaunchMenu.Build(_zrdrPath, _dataRoot);
            _menu.Launch = StartSessionFromMenu;
            _menu.LaunchCampaign = StartCampaignFromMenu;
            _menu.Quit = () => GetTree().Quit();
            // The board's own two audio needs, both over the process-lifetime channel and archive:
            // the score it enters on every screen, and the briefing narration it plays itself.
            _menu.Music = _music;
            _menu.MusicRng = _musicRng;
            _menu.Sounds = (wav, looped) => _musicArchive?.Find(wav, looped, warn: false);
            AddChild(_menu);
        }
        _menu.ShowMenu(_spec.MenuStartScreen);
        // The load screen is up for two frames during a build and torn down before anything
        // renders, so a shot of it needs a door of its own that leaves it standing.
        if (_spec.MenuStartScreen is "loadboard" or "loadboard-campaign")
        {
            ShowLoadScreen(_spec.MenuStartScreen == "loadboard-campaign");
        }

        // Safe on every entry: a cue for the track already playing is a no-op, which is exactly
        // what the original's own mail(11004) does (docs/org/music.md).
        _music?.Enter(MusicState.Menu, _musicRng);
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
        // Last, so it overwrites the two above rather than being half-overwritten by them: a
        // preset fills the wave and wingman fields itself and asking for both is a contradiction.
        if (_pendingPreset >= 0)
        {
            _menu.DebugPreset(_pendingPreset);
            _pendingPreset = -1;
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
        var pads = new List<int[]>(players.Count);
        // The fits ride alongside the planes rather than inside them: FromMenu writes each
        // menu-settable field explicitly, so a chosen loadout has to be handed over here or it
        // would be dropped exactly like any other field left out of that factory.
        var fits = new List<Flight.LoadoutChoice?>(players.Count);
        // A custom pick reaches the session as its def, loaded here: the menu carries only the
        // store name (PlanePickerRoster), and PlaneNode is the airframe's stock node, so without
        // this read a custom plane would fly as the stock aircraft it is built on.
        var customs = new List<Flight.CustomPlaneDef?>(players.Count);
        Flight.CustomPlaneStore? store = null;
        Flight.StockLoadouts? stock = null;
        foreach (var p in players)
        {
            planes.Add(p.PlaneNode);
            pads.Add(p.Pads);
            Flight.CustomPlaneDef? custom = null;
            if (p.CustomPlane is { } customName)
            {
                store ??= Flight.CustomPlaneStore.UserPlanes();
                custom = store.Load(customName);
                if (custom == null)
                {
                    // The file went away (or turned unreadable) between the picker's listing and
                    // the launch. Flying the stock airframe is the honest fallback: PlaneNode is
                    // already that aircraft, so the session builds rather than refusing.
                    GD.PushWarning($"custom plane '{customName}' could not be loaded, " +
                                   $"flying the stock {p.PlaneNode}");
                }
            }

            customs.Add(custom);
            // A plane the campaign exported flies with the ammunition and ordnance EXPORT wrote
            // into it. A fit set on the loadout screen is this sortie's own explicit pick and
            // stands instead of the stored one.
            fits.Add(p.Fit ?? (custom is { HasLoadout: true }
                ? CampaignLoadout.For(custom, stock ??= Flight.StockLoadouts.Load())
                : null));
        }
        _spec = SessionSpec.FromMenu(_cli, chapter, planes, mode, iaDef, fits, customs);
        // Step the master so flying again is a new mission rather than a replay: without this every
        // relaunch re-derives the same spawn, opposition and liveries. ⚠ A pinned run must hold
        // still, which is what keeps the goldens and the perf harnesses reproducible.
        if (!_spec.SeedPinned)
        {
            _sortie++;
            _masterSeed = Rng.SortieSeed(_processSeed, _sortie);
        }
        LogMasterSeed();
        BindMenuPads(pads);
        _menu!.HideMenu();
        BeginLaunch();
    }

    // Honour the join flow's device binding rather than re-deriving it from the connected roster:
    // the pad that joined as P2 in the menu must be the pad that flies P2. Single player keeps the
    // any-pad policy (null), so every connected pad flies the one plane, as before.
    // ⚠ EVERY menu launch path has to come through here. Pads.AssignPads, the fallback a path that
    // skips it lands on, gives P1 every pad no later seat claimed — so a two-seat launch with one
    // pad and the keyboard flew both seats off both devices, and P2's plane sat still.
    private void BindMenuPads(IReadOnlyList<int[]> pads)
    {
        _menuPads = null;
        if (_spec.Players <= 1)
        {
            return;
        }

        _menuPads = new int[_spec.Players][];
        for (int i = 0; i < _spec.Players; i++)
        {
            _menuPads[i] = pads[i];
        }
    }

    // The campaign cabin's FLY MISSION: the same derive-spec-then-build path as the launchscreen's
    // own launch, over the profile and story position the flow settled. The chapter and mission are
    // NOT named here — CampaignDirector.ResolveSpec reads them out of cm_sequence in the session's
    // constructor, so one place resolves a story position whether it came from a cabin or a
    // --campaign= command line.
    private void StartCampaignFromMenu(LaunchMenu.CampaignLaunch launch)
    {
        _spec = SessionSpec.FromCampaign(_cli, launch.Profile, launch.Seq, launch.PlaneNodes,
            launch.Pads.Count, launch.Fits, launch.Customs);
        if (!_spec.SeedPinned)
        {
            _sortie++;
            _masterSeed = Rng.SortieSeed(_processSeed, _sortie);
        }
        LogMasterSeed();
        BindMenuPads(launch.Pads);
        _menu!.HideMenu();
        BeginLaunch();
    }

    // A flown campaign mission is over: free the world and open the scrapbook on the mission just
    // flown, cabin on its far side, on a flow seated on the profile the director just wrote.
    // Reached from the session's MissionEnded by way of _pendingDebrief, one frame later, carrying
    // the result the mission ended with.
    private void OpenDebrief(string profile, CampaignMissionResult result)
    {
        GD.Print($"campaign: {result.Outcome} — arrived at the debrief with '{profile}'");
        ReturnToMenu();
        _menu!.OpenCampaignScrapbook(profile, result.Attempt.Seq);
    }

    // The mission boards' Restart (Instant Action and campaign): free this session and build a
    // fresh one from the same spec, behind the load screen. A rerun in place cannot put the
    // mission's opposition or objectives back (waves, the ace, a killed zeppelin and the campaign
    // graph all live in the world), so the world is rebuilt instead.
    // ⚠ Steps the seed exactly as flying again from the menu does, so an unpinned restart is a new
    // mission and a pinned one (--seed=/--det) still repeats.
    private void RestartSession()
    {
        if (_session != null)
        {
            // Freed at the end of THIS frame, so the build owed for the next one finds it gone.
            _session.QueueFree();
            _session = null;
        }
        if (!_spec.SeedPinned)
        {
            _sortie++;
            _masterSeed = Rng.SortieSeed(_processSeed, _sortie);
        }
        LogMasterSeed();
        GD.Print($"restart: rebuilding {_spec.Chapter} / {_spec.ModeName} from the same settings");
        BeginLaunch();
    }

    // Prints the master the next session will draw from. Per session rather than per process
    // because an unpinned run advances it: the seed a mission actually flew on is the one worth
    // having in the log, so an interesting one can be pinned with `--seed=`.
    private void LogMasterSeed()
    {
        string how = _spec.SeedPinned ? "pinned" : $"sortie {_sortie}, --seed=N to pin";
        GD.Print($"rng: master seed {_masterSeed} ({how})");
    }

    // The boards' Exit item: back to the launchscreen when this process launched into it, out of
    // the game otherwise. The routing Esc used to do, now reachable from a pad.
    private void ExitSession()
    {
        if (_menuDriven && _session is { InSession: true })
        {
            ReturnToMenu();
            return;
        }
        GetTree().Quit();
    }

    // Frees the current session node and shows the launchscreen again — the in-process rebuild
    // path for the boards' Exit item and for failed builds. The whole session subtree hangs under
    // the node, so `QueueFree` tears it down; the non-child duties (the published clock, the world
    // lights, the session texture archive, the main-camera restore) run in the node's
    // `_Notification` on `NotificationExitTree`. The camera, lights and shader globals persist
    // on `this`.
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
        RearmRate();
        PerfSample.Reset();
        PhysicsTickCost.Reset();
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

    /// <summary>The always-on frame-rate trace: one <c>[perf] rate</c> line per
    /// <see cref="RateWindowSeconds"/> carrying the rate the window ACHIEVED. This is the question
    /// <see cref="HitchMonitor"/> cannot answer and never could: its trigger is a multiple of a
    /// rolling median, so a sustained collapse drags the median up with it and trips nothing, and a
    /// whole sortie spent at a fraction of the refresh rate leaves a clean log. Mean and worst frame
    /// sit side by side so a rate drop and a single stall read differently.</summary>
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

    // Drops the open rate window. Called wherever the frames on either side are not each other's
    // neighbours, for the same reason HitchMonitor.Rearm is: a window spanning a session build
    // would report a rate no part of the run ever ran at.
    private void RearmRate()
    {
        _rateWallMs = 0;
        _rateWorstMs = 0;
        _rateFrames = 0;
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
        // ⚠ These are the physics terms to read, not physics_ms above (verification PERF-21). One
        // tick is one 1/60 sim step on a realtime clock, so phys_hz is sim seconds per wall second
        // and a step over its 16.7 ms budget shows here as a rate under 60.
        var (physTickMs, physTickMaxMs, physTicks) = PhysicsTickCost.Take();
        double physHz = _perfClock > 0 ? physTicks / _perfClock : 0;
        double physTick = physTicks > 0 ? physTickMs / physTicks : 0;
        double draws = _perfDraws / n;
        double prims = _perfPrims / n;
        double nodes = _perfNodes / n;
        double memMb = _perfMem / n / (1024 * 1024);
        System.Array.Copy(_perfFrameMs, _perfFrameMsSorted, PerfWindowFrames);
        System.Array.Sort(_perfFrameMsSorted);
        double maxMs = _perfFrameMsSorted[PerfWindowFrames - 1];
        double p95Ms = _perfFrameMsSorted[Perf95Index];
        Log.Info("perf", $"window sim_frame={simFrame} frames={_perfFrames} wall_ms={wallMs:0.00} fps={fps:0.0} frame_ms={frameMs:0.00} script_ms={scriptMs:0.00} render_cpu_ms={renderCpuMs:0.00} gpu_ms={gpuMs:0.00} physics_ms={physicsMs:0.00} phys_tick_ms={physTick:0.000} phys_tick_max_ms={physTickMaxMs:0.000} phys_hz={physHz:0.0} draws={draws:0.0} prims={prims:0.0} nodes={nodes:0.0} mem_mb={memMb:0.00} max_ms={maxMs:0.00} p95_ms={p95Ms:0.00}");
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

    /// <summary>Leaves the session the way <see cref="MenuDriven"/> says: back to the launchscreen,
    /// or out of the game. The boards' Exit item calls it, so the one routing rule lives on the
    /// Launcher rather than being restated per board.</summary>
    public required System.Action ExitSession { get; init; }

    /// <summary>Frees this session and builds a fresh one from the same settings, behind the load
    /// screen — the mission boards' Restart item (Instant Action and campaign). The mission's
    /// opposition lives in the world, so putting it back means rebuilding the world, which only
    /// the Launcher can do.</summary>
    public required System.Action RestartSession { get; init; }

    /// <summary>Frees this session and reopens the launchscreen on the named profile's campaign
    /// cabin, carrying the mission's result intact from <c>MissionEnded</c>. Null when this process
    /// was not launched into the menu, where there is no cabin to return to.</summary>
    public System.Action<string, CampaignMissionResult>? ReturnToCabin { get; init; }

    /// <summary>The process's music channel, so a mission's own cues reach the one player that
    /// outlives every session. Null when the sound archive or the sound definitions would not
    /// load, which leaves the game silent rather than refusing to launch.</summary>
    public MusicPlayer? Music { get; init; }
}
