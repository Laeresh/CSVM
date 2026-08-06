using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Main.tscn's root: the once-per-process bootstrap and everything that persists across
/// in-process relaunches. <c>_Ready</c> parses the command line into a <see cref="SessionSpec"/>,
/// settles the data root and base paths, applies the process-wide side effects a pure value
/// cannot (log, master seed, window focus/hide, gamepad policy, texture drop-ins), registers the
/// global shader parameters exactly once, runs the early-quit probes (<c>--dump-*</c>,
/// <c>--run-tests</c>), and builds the persistent camera / orbit rig / sun / WorldEnvironment.
///
/// Each launch then instantiates a <see cref="GameSession"/> session node, constructed with the
/// launch's spec and a <see cref="LauncherContext"/> carrying the persistent references; Esc from
/// a menu-launched flight frees that node (<see cref="ReturnToMenu"/> QueueFrees it) and shows the
/// launchscreen again. The full user-arg reference lives on <see cref="GameSession"/> and in
/// docs/cli.md.
/// </summary>
public partial class Launcher : Node3D
{
    /// <summary>Rendered frames per <c>--perf</c> report. A frame count rather than a wall second
    /// because under the fixed clock one rendered frame is exactly one sim step, so a window is a
    /// fixed amount of <i>simulation</i> and two runs of the same scenario yield the same number
    /// of samples — which is what makes a paired A/B comparable. At the vsync cap it is also
    /// still one report a second, so an interactive run reads as it always did.</summary>
    private const int PerfWindowFrames = 60;

    /// <summary>Master-bus index. This project ships no bus layout, so Master is the only bus
    /// and everything (both audio paths) is on it by default.</summary>
    private const int MasterBus = 0;

    /// <summary>Master output gain, linear, when neither <c>--volume=</c> nor the
    /// <c>audio.volume</c> config key says otherwise. 0 since 2026-08-05: launches are silent
    /// unless someone asks for sound (the Run scripts pass <c>--volume=1.0</c>), so a scripted
    /// or agent run never sounds by accident — see <see cref="ApplyMasterVolume"/>.</summary>
    private const float MasterVolumeDefault = 0f;

    /// <summary>The bus's own resting gain (0 dB). A launch resolving to this leaves the bus
    /// untouched, keeping it byte-identical in output and console log to a launch that never
    /// had a volume path at all.</summary>
    private const float MasterVolumeUnattenuated = 1f;

    /// <summary>Gain floor for the dB conversion, since <c>LinearToDb(0)</c> is negative infinity.
    /// -80 dB is inaudible, which is the whole point of <c>--volume=0</c>.</summary>
    private const float MasterVolumeFloor = 0.0001f;

    // What F11's placement print receives at the launchscreen, where no session (and no rigs)
    // exists — the same empty list the pre-split root held after a teardown.
    private static readonly List<PlayerRig> NoRigs = new();

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

    // ---- Focus mute --------------------------------------------------------------------------
    //
    // Alt-tabbing away silences the game; alt-tabbing back restores it. The mute is an
    // AudioServer *master-bus* mute rather than a factor threaded through the audio code,
    // because there are two entirely independent audio paths and only one of them has any
    // gain plumbing at all:
    //   - Flight.FlightAudio  — own-plane engine/whine/rattle loops (has MixGain) plus the
    //     crash and prop one-shots, which deliberately bypass MixGain ("one-shots stay global").
    //   - Mech3.WorldSounds   — the ambient SOUND_NODE 3D emitters, whose VolumeDb comes
    //     straight from the sound def. No MixGain, no shared gate, nothing to multiply.
    // A `MixGain = 0` mute would therefore leave the whole animated world audible, and would
    // also have to be un-set to exactly the right per-player value on the way back.
    //
    // The bus mute has a second property we specifically want: it does not touch
    // FlightAudio's state at all. The engine loop keeps Playing, `_engineRamp` stays at 1, and
    // FlightAudio.Update keeps writing the throttle curve into VolumeDb every frame while
    // muted — so on focus-in the loop is already at its correct level and does NOT re-ramp
    // from the -60f a fresh AudioStreamPlayer is constructed with (FlightAudio.cs:79), which
    // is what stopping/restarting the players would have caused.
    //
    // `--mute` is unrelated and cannot be reused for this: it is a load-time switch that
    // simply never constructs FlightAudio/WorldSounds, so there is nothing to toggle.
    //
    // --volume= (ApplyMasterVolume) rides the same bus for the same reasons, but writes its
    // VOLUME rather than its mute flag. The two are separate bus properties, so neither has to
    // know about the other: alt-tabbing in and out of a --volume=0 run restores the mute flag
    // and leaves the gain where it was.
    private bool _focusMuted;

    /// <summary>The session clock as this frame sees it: the suites' own under
    /// <c>--run-tests</c>, the live session's (published as <see cref="GameClock.Current"/> by its
    /// build, nulled by its teardown) otherwise, null at the launchscreen.</summary>
    private GameClock? ClockNow => _clock ?? GameClock.Current;

    public override void _Ready()
    {
        // The session node ticks first (its ProcessPriority is -1000: it advances the sim clock
        // every consumer reads during the same frame); this node comes right behind it and ahead
        // of everything else, so the shader clock and the capture pipeline run at the same point
        // in the frame they did when both lived on one root. Godot runs the lowest priority first.
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

        // Everything the command line settles, parsed AND resolved in one place (see SessionSpec):
        // the mode arbitration, the --det bundle's membership and the placement routing are all
        // answered by the time this returns. What is left here is the part a pure value cannot do —
        // the globals it deliberately does not touch, and the complaints it holds instead of
        // logging, emitted before anything configures the log so they read in launch order.
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


        // The window is CREATED without focus (`display/window/size/no_focus` in project.godot), so
        // a scripted run never takes the desktop from whoever is using the machine — a full
        // RunTests.ps1 launches the engine about twenty times. Setting the flag here instead was
        // measured not to work: by the time any script runs the window exists and has already
        // activated, and clearing that after the fact does not hand focus back.
        //
        // So the default is inverted, and an INTERACTIVE session asks for focus explicitly. A
        // session is scripted when a flag will drive and end it by itself, or when --no-focus says
        // so outright; everything else is somebody sitting down to play or to look at something,
        // and wants the window it just launched.
        //
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

        // --no-vsync: let the loop run as fast as it can. A measurement flag, not a display one —
        // with the presentation wait gone, `frame`, `fps` and `script` stop being floors pinned at
        // the refresh rate and start reporting the work actually done. Safe to combine with the
        // fixed clock precisely because that clock advances one sim step per RENDERED frame: the
        // simulation is identical frame for frame, only the wall time it takes changes.
        if (_spec.NoVsync)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
            Log.Info("perf", $"vsync off max_fps=0 — frame/fps/script report work done, not a refresh cap");
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

        // --headless + --screenshot can never produce a frame: the dummy renderer's GetImage()
        // comes back null forever, so the capture loop never counts down and the process never
        // quits — an orphan that still holds the log handle (docs/verification.md SHOT-9).
        // Reject the combo here, before any session builds, rather than let it hang.
        if (_spec.ScreenshotPath != null && DisplayServer.GetName() == "headless")
        {
            Log.Error("core", $"--screenshot needs a real GPU context; --headless never renders a capturable frame — drop one of the two flags");
            GetTree().Quit(1);
            return;
        }
        // The --det bundle is settled on the spec; what is left here is the two things a pure value
        // cannot do — draw an unpinned seed from the clock, and announce the resolved set.
        //
        // The master seed. A deterministic run and the animation debugger (deterministic by nature
        // — its whole point is an identical replay) pin it. Everything else draws from the clock, so
        // the shipped game keeps its variety. Applied here as well as per session so the dump tools
        // — which quit before any session is built — still draw from the resolved master rather
        // than a zero one.
        _masterSeed = _spec.PinnedSeed ?? Rng.TimeSeed();
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        GD.Print($"rng: master seed {_masterSeed}" + (_spec.SeedPinned ? " (pinned)" : " (--seed=N to pin)"));
        // Announce the whole resolved bundle on one line, so any capture or log carries the exact
        // conditions it was taken under instead of relying on the reader remembering what --det
        // implies. Every constituent is named with its value, including the ones a flag overrode.
        if (_spec.Det)
        {
            // The dev tuning file is git-ignored, so honouring it would make a deterministic capture
            // a function of one machine's uncommitted state: the same command gives different pixels
            // in a checkout and in a worktree, and a golden hash quietly records whatever was being
            // tuned that day. Pass --no-det to capture with your overrides applied.
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

        // Register the distance-fog global shader parameters SceneBuilder's world/aircraft
        // shader references, before any material using it is built. Defaults are a no-op
        // (nothing fades) — only --fly overrides them from the mission's weather.json below.
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
        // The shader clock every animated shader reads instead of Godot's TIME. Written each
        // frame from _Process below; registered here because Godot refuses to compile a shader
        // that references an unregistered global.
        //
        // All of these are registered before the dump branches below, which build materials of
        // their own and then quit: registering after them left every --dump-* run emitting a
        // missing-global error that poisons an error census.
        ShaderTime.RegisterGlobal();

        // --dump-markers: a pure-data report (no world, no camera) — print the marker rig table(s)
        // and quit. Placed here, once planes.zbd's path is known, so it runs whether or not any
        // content arg was given; --headless makes it windowless.
        //
        // Each dump quits with its probe's verdict, like --run-tests: a report that could not be
        // produced must not look to a caller like one that came out clean.
        //
        if (_spec.DumpMarkers)
        {
            GetTree().Quit(_probeRunner.DumpMarkers(_spec) ? 0 : 1);
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader (B11) —
        // dump every def and assert no key went unmapped.
        if (_spec.DumpWeapons)
        {
            GetTree().Quit(_probeRunner.DumpWeapons(_spec) ? 0 : 1);
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints (B12) — a missing marker is a loud error here.
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

        // Populate Config's tuning registry by exercising the wired modules once (WarmTuningRegistry),
        // then flag any config.json key that matched no tunable. Both run on every launch, are
        // data-free, and print before flight — so a typo'd or misplaced override is caught loudly at
        // startup rather than silently doing nothing.
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

        // No content-selecting arg (or an explicit --menu): show the in-game launchscreen
        // (Mode → Chapter → Plane). Its selection derives the session's spec and calls
        // LaunchSession, so there is exactly one downstream build path. Esc from a menu-launched
        // flight returns here (see ReturnToMenu).
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
        // The APPLICATION_* pair, not the WM_WINDOW_* pair: the application-level notifications
        // are what a real focus change delivers here. Measured on Windows 11 / Godot 4.7 while
        // implementing this: another app taking the foreground sends 1005
        // (WM_WINDOW_FOCUS_OUT) then 2017 (APPLICATION_FOCUS_OUT), and coming back sends 2016
        // then 1004. Note that *minimising* the window from another process delivers neither —
        // only the mouse enter/exit pair — so a manual test must alt-tab, not minimise.
        // Godot 4's constants are longs; _Notification hands us an int.
        if (what == (int)NotificationApplicationFocusOut)
        {
            SetFocusMuted(true);
        }
        else if (what == (int)NotificationApplicationFocusIn)
        {
            SetFocusMuted(false);
        }
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
        // Publish the frame's instant to the shaders, so the animated surfaces (UV scroll,
        // precipitation) and the CPU sim never disagree within a frame. Written unconditionally:
        // with no session clock — the launchscreen, or the frame after a teardown — it keeps
        // running on wall time so nothing on screen stalls behind the menu.
        ShaderTime.Advance(ClockNow, delta);
        // The frame-budget report stays on wall time: it is an instrument, and an instrument that
        // freezes with the thing it measures reports nothing.
        if (_spec.Perf)
            ReportPerf(delta);

        _captureDirector.Tick(GetViewport(), GetTree(), _spec, ClockNow, _orbit, _camera,
            _session?.Plane, _menu is { Visible: true });
        _gltfExporter.Tick(_session?.Plane, GetTree(), _spec);
    }

    /// <summary>Instantiates this launch's session node from the current <see cref="_spec"/> and
    /// the persistent references, and runs its build. Returns the build's verdict; on false the
    /// partial session node is left for the caller (the menu flow returns to the launchscreen, a
    /// CLI launch leaves the log to tell the story, exactly as the single-root class did).</summary>
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
        return _session.StartSession();
    }

    private void SetupLighting()
    {
        _sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, 150, 0), // shine onto the -Z (nose) side
            LightEnergy = 1.6f,
            ShadowEnabled = true,
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

    /// <summary>Shows the launchscreen (building it on first use) and wiring its Launch/Quit
    /// callbacks. Re-shown by <see cref="ReturnToMenu"/> after Esc-from-flight.</summary>
    private void ShowLaunchMenu()
    {
        if (_menu == null)
        {
            _menu = LaunchMenu.Build(_zrdrPath);
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
    }

    /// <summary>The launchscreen's players locked their picks: derive this session's spec from the
    /// pristine command line, bind each player's pad, and start the session. On a build failure,
    /// return to the menu with a note rather than leave a blank screen.
    ///
    /// <para>The spec is derived from <see cref="_cli"/>, never from the outgoing
    /// <see cref="_spec"/>, so nothing the last session settled can leak into this one. The pads are
    /// the exception on purpose: they come from the join flow rather than from args, so they stay
    /// session state here instead of becoming a spec field.</para></summary>
    private void StartSessionFromMenu(string chapter, IReadOnlyList<LaunchMenu.PlayerChoice> players,
        MenuMode mode)
    {
        var planes = new List<string>(players.Count);
        foreach (var p in players)
        {
            planes.Add(p.PlaneNode);
        }
        _spec = SessionSpec.FromMenu(_cli, chapter, planes, mode);
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

    /// <summary>Frees the current session node and shows the launchscreen again — the in-process
    /// rebuild path for Esc-from-flight and failed builds. The whole session subtree hangs under the
    /// node, so <c>QueueFree</c> tears it down; the non-child duties (the published clock, the world
    /// lights, the session texture archive, the main-camera restore) run in the node's
    /// <c>_Notification</c> on <c>NotificationExitTree</c>. The camera / lights / shader globals
    /// persist on <c>this</c>.</summary>
    private void ReturnToMenu()
    {
        if (_session != null)
        {
            _session.QueueFree();
            _session = null;
        }
        ShowLaunchMenu();
    }

    /// <summary>Settles the master output gain for the launch: <c>--volume=</c> if it was given,
    /// else the <c>audio.volume</c> config key, else silent.
    ///
    /// <para>This is the knob for running the game next to something else, and it is deliberately
    /// NOT <c>--mute</c>: at volume 0 both audio paths still load and play, so every sound counter
    /// and every <c>sound</c> log line reads exactly as it does at full volume — the run is silent
    /// but not blind. It is a bus write for the reason the focus mute above is: only FlightAudio
    /// has a gain to scale, and WorldSounds would go on sounding through any factor threaded
    /// through the other path.</para>
    ///
    /// <para>The config read is unconditional even when the flag wins, because the read is what
    /// registers the key — skipping it would drop <c>audio.volume</c> from
    /// <c>--dump-config</c> on exactly the runs that set a volume.</para></summary>
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

    /// <summary>Mutes/unmutes the master bus and gates pad reads, on window focus. Idempotent —
    /// the notification can arrive more than once — and it only ever clears a mute it set itself,
    /// so it cannot stomp on a mute from anywhere else.</summary>
    private void SetFocusMuted(bool muted)
    {
        if (_focusMuted == muted)
        {
            return;
        }
        _focusMuted = muted;
        AudioServer.SetBusMute(MasterBus, muted);
        // Pad *reads* follow focus too (Pads.For / Pads.InputBlocked): a stick held or drifting
        // while the player is alt-tabbed must not fly the plane, steer the free camera or scroll
        // the launchscreen. The pad *roster* (Pads.Connected) deliberately does not follow focus
        // — an unfocused pad has not disconnected; see the Pads class remarks. The keyboard needs
        // no gate: Godot releases held keys on focus loss, while joypads are polled from SDL
        // regardless of focus, which is the whole reason this exists.
        Pads.Focused = !muted;
        GD.Print(muted
            ? "focus: lost — audio muted, pad reads gated"
            : "focus: regained — audio restored, pad reads live");
    }

    /// <summary>
    /// --perf: the headless stand-in for the editor's profiler. Godot's visual profiler needs
    /// the editor GUI, but the same numbers are available at runtime — and the one that settles
    /// most questions is the per-viewport measured GPU time, which separates "our shader got
    /// more expensive" from "our C# got more expensive". Every term is a mean over the window,
    /// so a single hitch doesn't read as a regression; A/B two builds by comparing the same line.
    ///
    /// <para><c>physics</c> is Godot's <c>TIME_PHYSICS_PROCESS</c> monitor —
    /// the physics tick, which is where broadphase and narrowphase cost lands. It exists because
    /// `frame`/`fps` sit pinned at the vsync cap in nearly every run here, so they are floors
    /// and cannot show a collision change getting cheaper or dearer; the physics term can move
    /// while the frame time does not. Same caveat as `script`: it is Godot's own monitor, so
    /// trust it as an A/B ratio rather than as an absolute.</para>
    ///
    /// <para>The line carries <c>sim_frame=</c> so a parser can pin each window to the run's
    /// simulation state instead of to a wall moment, and its grammar is flat
    /// <c>key=value</c> — <c>RunTests.ps1 -Perf</c> reads it.</para>
    /// </summary>
    private void ReportPerf(double delta)
    {
        var vp = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(vp, true);
        _perfFrames++;
        _perfClock += delta;
        _perfProcess += Performance.GetMonitor(Performance.Monitor.TimeProcess);
        _perfPhysics += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
        _perfCpuRender += RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp);
        _perfGpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp);
        // Counts, and averaged like every other term: a single frame's draw-call count is whatever
        // was in view at the instant the window closed, which moves under a flying camera. These
        // four are the sharp end of the report — a count has no timing noise in it, so a scene that
        // starts drawing (or holding) more says so exactly, while every ms term has to clear a
        // noise band first.
        _perfDraws += Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        _perfPrims += Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        _perfNodes += Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        _perfMem += Performance.GetMonitor(Performance.Monitor.MemoryStatic);
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
        double scriptMs = 1000 * _perfProcess / n;
        double renderCpuMs = _perfCpuRender / n;
        double gpuMs = _perfGpu / n;
        double physicsMs = 1000 * _perfPhysics / n;
        double draws = _perfDraws / n;
        double prims = _perfPrims / n;
        double nodes = _perfNodes / n;
        double memMb = _perfMem / n / (1024 * 1024);
        Log.Info("perf", $"window sim_frame={simFrame} frames={_perfFrames} wall_ms={wallMs:0.00} fps={fps:0.0} frame_ms={frameMs:0.00} script_ms={scriptMs:0.00} render_cpu_ms={renderCpuMs:0.00} gpu_ms={gpuMs:0.00} physics_ms={physicsMs:0.00} draws={draws:0.0} prims={prims:0.0} nodes={nodes:0.0} mem_mb={memMb:0.00}");
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
