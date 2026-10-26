using System.Collections.Generic;
using System.IO;
using CSVM.Bindings;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session.Campaign;
using CSVM.Session.InstantAction;
using CSVM.Session.Objectives;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Overlays;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

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
    // What F11's placement print receives at the launchscreen, where no session (and no rigs)
    // exists, the same empty list the pre-split root held after a teardown.
    private static readonly List<PlayerRig> NoRigs = new();

    // The multiplayer door and the wire a flight carries, across every session this process builds.
    private readonly NetFlight _net;
    // The score, the cinemas over it and the master bus, across every session this process builds.
    private readonly LaunchAudio _audio;
    // The process's one menu. Its host is built on the first show of the menu (a no-content-arg
    // launch) and hands every typed exit to OnMenuExit.
    private readonly ProcessMenu _menu;

    // Everything the command line settled, parsed and resolved once (see SessionSpec). _cli is what
    // the user typed; _spec is what the LIVE session was built from, the launchscreen's pick
    // patches it, so every consumer below reads _spec and nothing re-derives a launch setting.
    private SessionSpec _cli = null!;
    private SessionSpec _spec = null!;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private int[][]? _menuPads;
    // The --screenshot=/--shots=/--frames= state machine and F11/F12's placement print and
    // ad-hoc save, see src/Tooling/CaptureDirector.cs's entry. Process-scoped: constructed once
    // from the launch spec, never re-armed by a menu relaunch.
    private Tooling.CaptureDirector _captureDirector = null!;

    // The --export-gltf= one-shot and F10's ad-hoc glTF save, see src\Tooling\GltfExporter.cs's
    // entry. Constructed once from the launch spec alongside the capture director.
    private Tooling.GltfExporter _gltfExporter = null!;
    // The master seed every subsystem generator derives from (see Utils.Rng). Pinned runs take the
    // spec's value; everything else draws from the clock, which is why it is resolved here and not
    // in the spec.
    private ulong _masterSeed;
    // The once-per-process draw _masterSeed starts at, kept so an unpinned relaunch can step to the
    // next sortie's master from it rather than from whatever the last session used.
    private ulong _processSeed;
    // How many times the launchscreen has started a flight this process, the step count applied to
    // _processSeed for an unpinned run, and the label the seed is logged under.
    private int _sortie;

    // The clock the --run-tests suites hand back (RunTestSuites' out param), ticked below so a
    // suite's teardown frame behaves as it always did. Every other clock is the session node's.
    private GameClock? _clock;

    private Camera3D _camera = null!;
    // Built once in _Ready and kept across sessions (like the camera). The mesh lab steers
    // both, sun direction/energy and the ambient, so held here rather than local to
    // SetupLighting.
    private DirectionalLight3D _sun = null!;
    private Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); created in
    // _Ready and kept across sessions, like _camera. --yaw=/--pitch= seed its initial angles.
    private OrbitCamera _orbit = null!;
    // The headset path, null on every flat launch. Built once in _Ready, like the camera it follows.
    private XrSession? _xr;
    // The headset's theater screen, where the boards and menus draw under the stereo path.
    private UI.Boards.TheaterScreen? _theater;
    // Whether this launch asked Godot for OpenXR (--xr-mode on), which compiles the stereo shader
    // variants whether or not a runtime then starts.
    private bool _xrRequested;
    // The shader cache watch such a launch keeps, swept as it ends or restarts; null otherwise.
    private XrShaderCache? _xrCache;
    // The VR mode a restart failed to start this process's child for, which the menu then tells
    // the player about; null when none failed.
    private bool? _xrStartFailed;

    // Session lifecycle (the launchscreen's in-process world rebuild): each launch instantiates a
    // GameSession node, freed again by ReturnToMenu or RestartSession. The camera, lights and
    // global shader params live on `this` and persist across sessions.
    private GameSession? _session; // the current session node (null at the launchscreen)
    // An Options apply, acted on at the top of the next frame: the exit arrives inside the active
    // presentation's own tick, which is no place to free it.
    private OptionsApplyExit? _pendingApply;
    // The Options settings on the window and the world, and the live graphics switch. Built beside
    // the sun it dresses.
    private LiveSettings _settings = null!;
    private bool _menuDriven;      // launched into the menu → Esc from flight returns here, not quit
    // Where a flight left early lands, settled by the launch that started it rather than by the
    // exit press: the menu's own launch paths write it (MenuReturnDestination.ForLaunch) and a
    // restart keeps it, since a restarted mission was launched from the same screen.
    private MenuReturnDestination _exitDestination = MenuReturnDestination.TopLevel;
    // The profile a just-ended campaign mission belongs to, acted on at the top of the next frame:
    // the mission ends inside the session's own physics step, which is no place to free it.
    private (string Profile, CampaignMissionResult Result)? _pendingDebrief;

    // The final numbers of an ended Instant Action mission, acted on at the top of the next frame.
    // The reason is the debrief's: the ending arrives inside the session's own step.
    private IaWrapupSnapshot? _pendingWrapup;

    // A finished lobby match's landing on Game Scores, acted on at the top of the next frame for
    // the debrief's reason.
    private bool _pendingLobbyLanding;

    // The load screen and the deferred build behind it (BeginLaunch → _Process). A build is one
    // synchronous block, so the screen has to be DRAWN before it starts: _launchFramesWaited counts
    // the frames since the request and the build runs on the first one that proves a frame rendered.
    // ⚠ Null/-1 means no launch is owed; a CLI launch does not come through here at all.
    private CanvasLayer? _loadLayer;
    private int _launchFramesWaited = -1;
    // This is the load screen's second half. Once the session is built, the work it ordered for the
    // load is carried out one step a frame with the screen still up. That is what makes the screen
    // a real yield of several frames. -1 means nothing is owed.
    private int _loadStepsRun = -1;

    // Frames the load screen stayed up after the owed steps, for a network start still held.
    private int _startHeldFrames;
    // The cover that bridges the load screen and the session's first real frame. It is raised with
    // the screen, or with the build on a CLI launch. It stays opaque until the session says that
    // frame is ready, then fades up from dark. Null under --det and once it has finished.
    private UI.Screens.SessionStartFade? _startFade;

    // The version stamp and its folder icons in the menu's corner. They show while the main menu or
    // the extraction screen is up. No presentation carries them, and no flight capture sees them.
    private UI.Screens.BuildStamp _buildStamp = null!;
    // The --debug-net readout, null without the flag, and the wall time since it last refreshed.
    private UI.Overlays.NetReadout? _netReadout;
    private double _sinceNetReadout;
    // The frame instruments: the hitch monitor and sidecar, the rate line, the F14 readout and the
    // opt-in --perf, --debug-mem and --hitch-inject. Built in three steps across _Ready.
    private FrameInstruments _instruments = null!;

    // The base paths, resolved once _Ready has settled both roots and again by the extraction
    // screen's hand-back. Each session node receives them through LauncherContext.
    private BasePaths _paths = null!;
    // Set beside the repo root, by the same editor check. The log directory is all that reads it
    // (Log.DirectoryFor): a recipient's log must not land in a hidden developer folder.
    private bool _exported;
    // Why --synthetic-data could not write its tree, or null. The run quits on it once the log is
    // open. Until then the data root names the unwritten tree, so nothing real is read.
    private string? _syntheticError;
    // Constructed once the base paths above are settled; every --dump-*/--run-tests/--*-test/
    // --destroy= probe wrapper delegates to it (see src/Tooling/ProbeRunner.cs).
    private Tooling.ProbeRunner _probeRunner = null!;
    // The screen a menu launch stops at over missing or stale data, null once it hands back.
    private UI.Screens.NoGameDataScreen? _extractionScreen;

    // The flight's two exits are this node's, so the network state is built over them here. The
    // sound is built with the node, since a focus change can arrive before _Ready settles anything.
    public Launcher()
    {
        _net = new NetFlight(ReturnToMenu, ExitSession);
        _audio = new LaunchAudio(this);
        _menu = new ProcessMenu(this, _audio, _net, () => _paths, OnMenuExit)
        {
            // Read on the menu's first show, after _Ready has booted the headset path or not.
            SeatLimit = () => _xr != null ? 1 : PlayerSetupFeature.MaxSeats,
            Notice = () => XrRelaunch.MenuNotice(_xrRequested, _xr is { PacesFrames: true }, _xrStartFailed),
        };
    }

    // The screen ExitSession takes a flight back to, as the last launch settled it. The setter is
    // the launch-return suite's way of putting the live node's own destination back afterwards.
    internal MenuReturnDestination ExitDestination
    {
        get => _exitDestination;
        set => _exitDestination = value;
    }

    // The session clock as this frame sees it: the suites' own under
    // `--run-tests`, the live session's (published as GameClock.Current by its
    // build, nulled by its teardown) otherwise, null at the launchscreen.
    private GameClock? ClockNow => _clock ?? GameClock.Current;

    // The boards stood with no flight in front of them, over the live spec and the current paths.
    // Built per use, since an extraction's hand-back re-resolves the paths.
    private LaunchBoards Boards => new(_spec, _paths);

    public override void _Ready()
    {
        // One notch behind the session node (ProcessPriority -1000), so the shader clock and
        // capture pipeline run at the same point in the frame they did on one root. Godot runs
        // the lowest priority first.
        ProcessPriority = -999;

        // Both ends of the physics-tick bracket (see PhysicsTickCost). At the launcher rather than
        // the session, because the pair must survive a session rebuild for the tick rate either
        // side of one to be comparable.
        AddChild(PhysicsTickCost.MakeBracket(false));
        AddChild(PhysicsTickCost.MakeBracket(true));

        // The same pair around the process pass (see ProcessPassCost), for the same reason.
        AddChild(ProcessPassCost.MakeBracket(false));
        AddChild(ProcessPassCost.MakeBracket(true));

        // Load the optional tuning-override file first, before any module reads a Config value.
        // Missing/malformed file → in-code defaults (never throws); see src/Config.cs.
        Config.Load();

        string repoRoot;
        if (OS.HasFeature("editor"))
        {
            // res:// is the CSVM/ project dir on disk only while the editor (or an editor-run
            // build) hosts the game; the repo root is its parent.
            var projectDir = ProjectSettings.GlobalizePath("res://");
            repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));
        }
        else
        {
            // Exported build: res:// lives inside the pck, so the root is the exe's own folder,
            // extracted/ ships beside the exe, and logs/ and .scratch/ output land there too.
            _exported = true;
            repoRoot = Path.GetFullPath(Path.GetDirectoryName(OS.GetExecutablePath())!);
        }

        // Everything the command line settles is parsed and resolved in one place; see
        // SessionSpec. What's left here is what a pure value cannot do: process-wide side
        // effects, and the warnings below, emitted before the log so they read in launch order.
        _cli = SessionSpec.Parse(OS.GetCmdlineUserArgs());
        _spec = _cli;
        Tooling.ShaderDiagnostics.Enabled = _spec.DebugShaders;
        MouseCapture.ScriptedLaunch = _cli.IsScripted || _cli.Det;
        foreach (var note in _spec.Warnings)
        {
            if (note.Category.Length == 0)
            {
                Log.Info("core", $"{note.Message}");
            }
            else
            {
                Log.Warn(note.Category, $"{note.Message}");
            }
        }

        // Every base path derives from the data root, so it is settled first. Precedence:
        // --data-root= beats CSVM_DATA_ROOT beats the repo root. The overrides exist because a git
        // worktree has no extracted/, which is git-ignored, and cannot otherwise build or verify.
        string dataRoot = repoRoot;
        var dataRootEnv = OS.GetEnvironment("CSVM_DATA_ROOT");
        if (!string.IsNullOrEmpty(dataRootEnv)) dataRoot = Path.GetFullPath(dataRootEnv);
        if (_spec.DataRoot is { } dataRootArg) dataRoot = Path.GetFullPath(dataRootArg);
        if (dataRoot != repoRoot)
            Log.Info("core", $"data root: {dataRoot} (repo root {repoRoot})");
        dataRoot = ResolveSyntheticData(repoRoot, dataRoot);

        // The extraction tree's provenance check, at most one warning line, never a block. An
        // --extract run is about to write that tree, and a missing one has no stamp to read. A
        // warning about either would only mislead.
        if (_spec.ExtractInstall == null && !UI.Screens.NoGameDataScreen.Missing(dataRoot))
        {
            ExtractionStamp.Check(dataRoot);
        }

        // The drop-in writes statics every material built afterwards reads, so it is applied here
        // rather than carried as a session value.
        foreach (string name in _spec.TexOverrides)
        {
            TextureDropIn.SetScratchDir(repoRoot);
            TextureDropIn.AddOverride(name);
        }
        if (_spec.TexCensus)
        {
            TextureDropIn.SetScratchDir(repoRoot);
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
        // Read by both AI pickers, for the same reason the two statics above are statics. It
        // settles once per launch, and no pilot or gunner chooses it for itself.
        Flight.Ai.AiTargetRanking.AircraftFirst = _spec.AircraftFirstTargeting;
        _captureDirector = new Tooling.CaptureDirector(_spec);
        _gltfExporter = new Tooling.GltfExporter(_spec);
        // Built alongside the other process-scoped services, ahead of every probe's early quit
        // and of --dump-config: its hitch monitor is what registers the five hitchMonitor.* keys.
        _instruments = new FrameInstruments(_spec, GetViewport().GetViewportRid());


        // An editor run's window is created without focus (no_focus in project.godot). An
        // interactive session asks for it here. See docs/verification.md's SHELL-13.
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
        Log.Info("perf", $"gpu={Tooling.GoldenShot.Adapter()} driver={RenderingServer.GetCurrentRenderingDriverName()} method={RenderingServer.GetCurrentRenderingMethod()} refresh_hz={DisplayServer.ScreenGetRefreshRate():0.#}");

        // --run-tests must never read or write the player's options file, and this must be set
        // before the first UserOptions() call, the vsync read below. One scratch directory per
        // process: docs/architecture.md's OptionsStore entry has the shard race that settled it.
        if (_spec.RunTests)
        {
            string scratchOptions = Path.Combine(Path.GetTempPath(), "CSVM", "run-tests-options",
                System.Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (Directory.Exists(scratchOptions))
            {
                Directory.Delete(scratchOptions, recursive: true);
            }

            Directory.CreateDirectory(scratchOptions);
            OptionsStore.DirectoryOverride = scratchOptions;
            CSVM.Bindings.BindingStore.DirectoryOverride = scratchOptions;
        }

        // After the options directory is settled, since both read the saved options.
        LiveSettings.ApplyWindowAtLaunch(_spec, GetWindow());

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
        Log.Open(repoRoot, _spec.ModeName, BuildVersion.Current, _exported);
        // Every persisted store resolves against this root (profiles, hangar planes, bindings,
        // options), and it is derived from the project name alone, so a worktree run shares it with
        // the main checkout. Printed so a "not found" against a file on disk is answered, not guessed.
        Log.Info("core", $"user={ProjectSettings.GlobalizePath("user://")}");
        // Named while the run is live, because a crash never reaches the mirror in _ExitTree and
        // this is then the only pointer to the traces our own sink cannot see.
        Log.Info("core", $"engine log={Path.Combine(OS.GetUserDataDir(), "logs", "godot.log")} (mirrored beside this one on quit)");
        foreach (var (old, replacement) in _spec.Deprecated)
        {
            Log.Warn("core", $"deprecated flag={old} use={replacement}");
        }
        // Before any door is built or suite runs: a door reads the game port as it is constructed.
        if (_spec.NetPortBase is { } netPortBase)
        {
            Net.NetPorts.Use(netPortBase);
            Log.Info("core", $"net ports: game {Net.NetPorts.Game} lan {Net.NetPorts.Lan} (--net-port-base)");
        }
        // Ahead of --dump-config, same reason as the instruments: registers the two
        // hitchSidecar.* keys. After Log.Open, since its path derives from the log's.
        _instruments.OpenSidecar(repoRoot, _exported);

        // Before anything heavy loads: a scripted launch below the memory floor ends here. A
        // release build has no ledger to join.
        if (!_exported && Tooling.MemoryAdmission.Refuses(OS.GetCmdlineUserArgs(), repoRoot))
        {
            GetTree().Quit(Tooling.MemoryAdmission.TripwireExitCode);
            return;
        }

        // A failed --synthetic-data tree ends the run. Reading the install instead would pass it on
        // data nobody asked for, and an empty tree would only skip every suite.
        if (_syntheticError != null)
        {
            Log.Error("core", $"--synthetic-data could not write its tree: {_syntheticError}");
            GetTree().Quit(1);
            return;
        }

        // VR mode at boot, before anything heavy runs: a restart into the headset leaves from here
        // in the state the --extract branch below runs in.
        if (StartXrMode())
        {
            return;
        }

        // --extract builds no world and no menu: it writes the data root's extracted/ and quits.
        if (_spec.ExtractInstall is { } extractInstall)
        {
            StartHeadlessExtraction(extractInstall, repoRoot, dataRoot);
            return;
        }

        // --headless + --screenshot can never produce a frame: the dummy renderer's GetImage()
        // never returns, so the capture loop never counts down. Reject the combo here, before
        // any session builds, rather than let it hang as an orphan holding the log handle.
        if (_spec.ScreenshotPath != null && DisplayServer.GetName() == "headless")
        {
            Log.Error("core", $"--screenshot needs a real GPU context; --headless never renders a capturable frame, drop one of the two flags");
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
            // function of one machine's uncommitted state, see docs/verification.md's DET-8.
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
            Log.Info("core", $"no-det: {wouldBe} would run deterministically, wall-clock sim clock, unpinned randomness seed={_masterSeed}");
        }
        // After the --det block, so a deterministic run reads the flag but not the tuning file that
        // ClearOverrides just dropped; before the early-quit probes below, so --run-tests and the
        // --dump-* wrappers are covered by the same gain an interactive launch gets.
        _audio.ApplyMasterVolume(_spec.Volume, _exported);
        // After it, so the two are read in the order they multiply: the developer gain on bus 0,
        // then the player's mix on the three buses under it. ⚠ --det reads no saved level, the same
        // rule the saved graphics word below follows; SavedLevels owns that drop.
        var savedMix = AudioMix.SavedLevels(_spec.Det);
        AudioMix.Apply(savedMix.Master, savedMix.Music, savedMix.Effects, savedMix.Voice);
        // Logged on every launch, not only when a flag moved it. The shipped factor is a remake-only
        // rule over the decoded radii, so a run's record has to say what reach it played at.
        Mech3.SoundFalloff.SetRangeScale(_spec.SoundRangeScale);
        string rangeVia = Mech3.SoundFalloff.RangeScale == Mech3.SoundFalloff.ShippedRangeScale
            ? "shipped" : "--sound-range-scale";
        Log.Info("sound", $"sound range scale=x{Mech3.SoundFalloff.RangeScale:0.###} via={rangeVia}, every positional RANGE pair and its cull multiplied");
        // The haptics toggle, read under the same rule and defaulting ON where nothing is saved.
        // ⚠ A deterministic run never rumbles. A golden sweep or a scripted probe must not reach
        // the hardware on the desk, and no screen is drawn from this.
        PadRumble.Enabled = !_spec.Det && OptionsStore.UserOptions().Load().Rumble != false;
        // The rocket carve, defaulting OFF: the original carves nothing in play. ⚠ No screen offers
        // it. The saved key and --craters are its only doors, read here and nowhere else. A --det
        // run drops the key, keeping every golden clear of a bowl; the flag survives it, for a probe.
        CraterGate.Enabled = _spec.Craters
            || (!_spec.Det && OptionsStore.UserOptions().Load().RocketCraters == true);
        // The flight text block, defaulting OFF: the original draws no such readout. ⚠ No screen
        // offers it. The saved key and --hud-text are its only doors, the same pair the carve has.
        Flight.Hud.FlightHud.TextBlockEnabled = _spec.HudText
            || (!_spec.Det && OptionsStore.UserOptions().Load().FlightTextBlock == true);
        // The cockpit loop's throttle pitch, a remake-only rule. ⚠ No screen offers it; the saved key
        // is its one door. A --det run drops the key, so a suite states the rule it tests.
        bool? savedCockpitPitch = _spec.Det ? null : OptionsStore.UserOptions().Load().CockpitEnginePitch;
        Flight.Audio.FlightAudio.CockpitLoopPitched = savedCockpitPitch ?? Flight.Audio.FlightAudio.CockpitLoopPitchedDefault;
        Log.Info("sound", $"cockpit engine pitch: pitched={Flight.Audio.FlightAudio.CockpitLoopPitched} via={(savedCockpitPitch.HasValue ? "options.json" : "default")}");
        // Before the first PreferUnzipped call and process-wide, so every later resolution (the
        // chapter paths in StartSession, the menu pages' own lookups) takes the same asset shape.
        SessionPaths.ForceZipped = _spec.ZipAssets;
        if (_spec.ZipAssets)
        {
            Log.Info("core", $"assets: --zip-assets, reading .zip archives, ignoring unpacked folders");
        }
        // Before any texture loads. A deterministic run reads no pack, so no golden depends on what
        // one machine dropped in; --hd= survives it, for a probe.
        if ((_spec.Hd ?? (_spec.Det ? null : Config.GetString("textures.hd", "HD"))) is { } hdFolder)
        {
            HdTextures.Current = HdTextures.Open(dataRoot, hdFolder);
        }
        // The model packs follow the same rule, and --no-hd-models beats every folder name.
        if (!_spec.NoHdModels && (_spec.HdModels ?? (_spec.Det ? null : Config.GetString("models.hd", "HDModels"))) is { } modelFolder)
        {
            HdModels.Current = HdModels.Open(dataRoot, modelFolder);
        }
        // After the asset shape, which the unpacked-folder preference reads. The chapter-dependent
        // gamez/texture/mission paths resolve per session in StartSession.
        _paths = BasePaths.Resolve(repoRoot, dataRoot, _spec);
        _probeRunner = new Tooling.ProbeRunner(_paths);

        // Registers the distance-fog params SceneBuilder's shaders reference; defaults are a
        // no-op until --fly's weather.json overrides them.
        // ⚠ Runs once here, GlobalShaderParameterAdd errors on a second call; a rebuild must Set.
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
        // The per-view table beside them, empty, so every shader reads the four above until a
        // splitscreen pane's zone differs from the first one's.
        FogViewTable.RegisterGlobals();
        // The fog volumes' planes and zone fog, for the Enhanced view of a volume from outside.
        WeatherRig.RegisterGlobals();
        // The same SUNLIGHT uncollapsed, for the cloud cards and the enhanced billboard grades
        // (docs/org/vertexLighting.md). The defaults, ambient 1 and diffuse 0, draw a card as
        // authored in a view without mission weather.
        RenderingServer.GlobalShaderParameterAdd("csky_sun_dir",
            RenderingServer.GlobalShaderParameterType.Vec3, new Vector3(0f, 1f, 0f));
        RenderingServer.GlobalShaderParameterAdd("csky_sun_light",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1f, 0f));
        // The same pair with its colours (WeatherRig.SunVertexLight), which the faithful aircraft
        // reads. SetupLighting replaces these registration defaults with the day pair.
        RenderingServer.GlobalShaderParameterAdd("csky_sun_ambient_rgb",
            RenderingServer.GlobalShaderParameterType.Vec3, Vector3.One);
        RenderingServer.GlobalShaderParameterAdd("csky_sun_diffuse_rgb",
            RenderingServer.GlobalShaderParameterType.Vec3, Vector3.Zero);
        // The Danger Zone photograph's ambient half for its own pilot (WeatherRig.PhotographFill);
        // ambient 1 fills to 1, so the default agrees with the ambient default above.
        RenderingServer.GlobalShaderParameterAdd("csky_sun_fill_rgb",
            RenderingServer.GlobalShaderParameterType.Vec3, Vector3.One);
        // The world sampler's mip LOD bias. 0 is the original's own device default, so a chapter
        // authoring no MipBias renders exactly as it did (docs/org/textures.md).
        RenderingServer.GlobalShaderParameterAdd("csky_mip_bias",
            RenderingServer.GlobalShaderParameterType.Float, 0.0f);
        // Which keymap a seat is built on, resolved before the first seat exists. Both flags close
        // the door for the same reason: a run whose result is compared against a committed golden
        // must not depend on the keymap saved at whoever's machine ran it (verification.md, DET-8).
        CSVM.Bindings.LaunchBindings.Configure(_spec.Det || _spec.RunTests);
        // A scrolling caption would put a capture's pixels on the frame count. The same runs hold
        // every marquee at its start unless the aid names a phase.
        CSVM.UI.Boards.BoardMarquee.PinnedSeconds = _spec.DebugMarquee ?? (_spec.Det || _spec.RunTests ? 0d : null);

        // After the --det block, so ClearOverrides has dropped a config graphics.mode; ahead of the
        // clutter fade, which needs the mode to follow the pushed fog.
        LiveSettings.ResolveGraphicsAtLaunch(_spec, GetViewport());
        // The clutter fade's squared distance scale, 0 when the fade is off. Enhanced mode pushes
        // the fade out with the fog and further by the View Distance (ClutterFadeScaleSq).
        float clutterFadeScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterAdd(Utils.EffectsLevel.ShaderParam,
            RenderingServer.GlobalShaderParameterType.Float, clutterFadeScaleSq);
        Utils.EffectsLevel.RegisteredScaleSq = clutterFadeScaleSq;
        string clutterFarFade = Utils.EffectsLevel.ClutterFarFadeEnabled() ? "true" : "false";
        Log.Info("world", $"clutter fade: {Utils.EffectsLevel.FadeKey}={clutterFarFade} {Utils.EffectsLevel.Key}={Config.GetString(Utils.EffectsLevel.Key, Utils.EffectsLevel.Default)} scale_sq={clutterFadeScaleSq}");
        // The animated world's LIGHT_STATE point lights. Defaults to an empty set, so a session
        // with no lit animations renders exactly as it did before they existed.
        WorldLights.RegisterGlobals();
        // Registered before the --dump-* branches below, which build materials of their own:
        // registering after them left every dump run emitting a missing-global error.
        ShaderTime.RegisterGlobal();
        Effects.Ocean.RegisterGlobal();

        // --dump-markers: a pure-data report, print the marker rig tables and quit. Runs
        // whether or not a content arg was given; --headless makes it windowless. Each dump
        // quits with its own verdict, like --run-tests, never a false-clean exit code.
        if (_spec.DumpMarkers)
        {
            GetTree().Quit(_probeRunner.DumpMarkers(_spec) ? 0 : 1);
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader,
        // dump every def and assert no key went unmapped.
        if (_spec.DumpWeapons)
        {
            GetTree().Quit(_probeRunner.DumpWeapons(_spec) ? 0 : 1);
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints, a missing marker is a loud error here.
        if (_spec.DumpLoadout)
        {
            GetTree().Quit(_probeRunner.DumpLoadout(_spec) ? 0 : 1);
            return;
        }
        // --dump-flight: pure data again, no world and no model, just the zrdr stats stepped
        // through the manoeuvres the original was measured flying.
        if (_spec.DumpFlight)
        {
            GetTree().Quit(_probeRunner.DumpFlight(_spec) ? 0 : 1);
            return;
        }
        // --dump-mips: what the texture archive's mip chains actually hold, level by level, beside
        // the authored levels they should be, the before/after instrument for --mips=.
        if (_spec.DumpMips)
        {
            GetTree().Quit(_probeRunner.DumpMips(_spec) ? 0 : 1);
            return;
        }
        // --dump-ai: the five AI data families read straight off the extraction, no world, no
        // scene, no readers built for the families that don't have one yet (aiv/ai.zrd/zeppelins/
        // egen). See Probes.Ai.
        if (_spec.DumpAi)
        {
            GetTree().Quit(_probeRunner.DumpAi(_spec) ? 0 : 1);
            return;
        }
        // --dump-sticks: the SDL2 stick roster with each stick's counts and resting reads; fails
        // only when no SDL2 loads, since zero sticks is a valid answer.
        string? sdlRepoRoot = _exported ? null : repoRoot;
        string? sdlDataRoot = string.IsNullOrEmpty(dataRootEnv) ? null : dataRootEnv;
        if (_spec.DumpSticks)
        {
            GetTree().Quit(Sticks.StickPump.Dump(sdlRepoRoot, sdlDataRoot, _spec.DumpSticksWatch) ? 0 : 1);
            return;
        }

        // Exercises the wired modules once so Config's tuning registry is complete, then flags
        // any config.json key no tunable matched, data-free, so a typo is caught before flight.
        TuningWarmup.Run();
        Config.ReportOrphans();
        // --dump-config: write a fully-populated tuning template (every registered key + its default,
        // nested by block) to the scratch folder and quit, the copy-and-edit source for config.json.
        if (_spec.DumpConfig)
        {
            string dumpPath = Path.Combine(repoRoot, ".scratch", "config.dump.json");
            Config.DumpConfig(dumpPath);
            Log.Info("core", $"config: wrote {Config.RegisteredCount}-key tuning template to ./.scratch/config.dump.json");
            GetTree().Quit();
            return;
        }

        // Gamepad hotplug: every input read polls Pads.Connected() fresh, so a pad plugged in
        // mid-game works the moment the engine reports it. Log the roster at launch and every
        // connect/disconnect so a silent pad is diagnosable from the console.
        if (Pads.Disabled)
            Log.Info("core", $"gamepad: off (--no-pads, or the --det bundle), ignoring every device (keyboard/scripted input only)");
        else
            Input.Singleton.JoyConnectionChanged += (device, connected) =>
            {
                if (connected)
                    Log.Info("core", $"gamepad connected: device {device} \"{Input.GetJoyName((int)device)}\" guid={Input.GetJoyGuid((int)device)}");
                else
                    Log.Info("core", $"gamepad disconnected: device {device}");
            };
        var padsAtLaunch = Pads.Connected();
        if (Pads.Disabled)
        {
            // nothing more to report, the roster is deliberately empty
        }
        else if (padsAtLaunch.Count == 0)
            Log.Info("core", $"gamepad: none at launch (hotplug live, connect any time)");
        else
            foreach (int p in padsAtLaunch)
                Log.Info("core", $"gamepad: device {p} \"{Input.GetJoyName(p)}\" guid={Input.GetJoyGuid(p)} info={Input.GetJoyInfo(p)}");
        // After the pad roster, which the stick roster subtracts. Once per process, like the pads;
        // a run under --no-pads (so every test and golden) never loads SDL2 at all.
        if (Sticks.StickPump.Start(sdlRepoRoot, sdlDataRoot) is { } stickPump)
        {
            AddChild(stickPump);
        }

        SetupLighting();
        _settings = new LiveSettings(this, _sun, _env, subject => Boards.SwitchScreen(subject));
        // GameSession re-applies these same two framings per launch. The decoded world base serves
        // every camera that draws the world, and the viewer's own 50 a model on a stage.
        _camera = new Camera3D
        {
            Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? CameraController.ExternalFovDeg : 50f,
            Far = 40000f,
        };
        AddChild(_camera);
        _orbit = new OrbitCamera(_camera);
        if (_spec.Yaw is { } argYaw) _orbit.Yaw = argYaw;
        if (_spec.Pitch is { } argPitch) _orbit.Pitch = argPitch;

        // --run-tests: the in-engine assertion suites. Dispatched here, after the camera exists (a
        // suite building a world resolves PLAYER_RANGE from it) and before any session is built,
        // the suites build exactly the world/plane each of them needs and nothing else.
        if (_spec.RunTests)
        {
            int code = _probeRunner.RunTestSuites(_spec, this, _camera, out _clock);
            GetTree().Quit(code);
            return;
        }

        // OpenXR starts with the engine or not at all, and the --xr-sim stand-in only with its feature
        // tag. Without either Boot builds nothing, so a flat launch draws and paces exactly as it did.
        _xr = XrSession.Boot(GetViewport(), _camera, _instruments.RenderTime, _spec.XrSim);
        if (_xr != null)
        {
            AddChild(_xr);
            // A stereo root draws no canvas layer, so every board, menu and cinema built from here
            // on draws on the screen instead. A flight is on view once its first frame is ready.
            var rig = _xr.Rig;
            System.Func<bool> flightLive = () => rig.Pane != null && _session is { InSession: true, FirstFrameReady: true };
            // A presenting cutscene plays on the screen as a stereo pair, converged on its letterbox.
            rig.CutscenePlays = () => _session?.Cutscene is { Playing: true, Presenting: true };
            _theater = UI.Boards.TheaterScreen.Build(
                GetViewport(), _camera, () => rig.Origin.GlobalTransform,
                flightLive,
                eyeFeed: true,
                cutscene: () => rig is { Screening: true, Pane: { } pane } && _session?.Cutscene is { } film
                    ? new UI.Boards.TheaterCutscene(pane.Camera, UI.Boards.StereoPair.ConvergenceM(Mathf.Abs(film.CardBox.GetCenter().Z)), film.FilmRowsShown)
                    : null,
                interocular: () => XrEyeFeed.InterocularM);
            AddChild(_theater);
            // The hand controllers' laser pointer on it, since the desktop mouse cannot reach the
            // eye-size layout a stereo root lays its 2D out in.
            AddChild(UI.Boards.TheaterPointer.Build(GetViewport(), _theater, () => rig.Origin.GlobalTransform, eyeFeed: true));
            // Every cut the headset sees lands on black, and the optional vignette closes in on turns.
            rig.Fade = new XrFade();
            var theater = _theater;
            var comfort = UI.Boards.ComfortQuads.Build(rig.Fade, () => rig.Origin.GlobalTransform, flightLive,
                () => theater.BoardShown, () => VrVignetteSetting.Current);
            rig.Cut += comfort.OnCut;
            AddChild(comfort);
            ApplyVrComfort(VrRenderScaleSetting.SavedWord(_spec.Det), VrVignetteSetting.SavedWord(_spec.Det));
        }

        // VR is one local seat in every mode, so a command line asking for splitscreen flies one.
        if (_xr != null && _spec.Players > 1)
        {
            Log.Warn("core", $"xr: --players={_spec.Players} refused, {PlayerSetupFeature.VrOneSeat} Flying one seat");
            _spec = _spec.WithOneSeat();
            _cli = _cli.WithOneSeat();
        }

        // The F14 / --debug-fps readout, process-wide like the camera so it works at the
        // launchscreen too. Built after the --run-tests/--dump-* early exits, which render no
        // frame it would have anything to show.
        AddChild(_instruments.BuildHud());

        // The version stamp on the menu, process-wide for the same reason and built beside it: the
        // build a capture came from is a fact about the binary, not about a presentation.
        _buildStamp = new UI.Screens.BuildStamp(repoRoot, _exported);
        AddChild(_buildStamp);
        // A Game Mode refusal stands in the Original menu's own warning box, the stamp's dialog elsewhere.
        _buildStamp.Messagebox = words => _menu.Host?.Active is UI.Menu.Original.OriginalPresentation original && original.RaiseWarning(words);
        // Exported only, so an editor run, a worktree, the suites and agents never ask GitHub. A
        // --det capture of an export does not either, or the stamp would depend on the network.
        if (_exported && !_spec.Det && Config.GetBool(ReleaseCheck.Key, ReleaseCheck.DefaultEnabled))
        {
            _buildStamp.Watch(ReleaseCheck.Start(BuildVersion.Current));
        }

        // The strip over Steam's on-screen keyboard, process-wide because the menus, the extraction
        // screen and the flight chat all raise it. It draws nothing on any other machine.
        AddChild(new UI.Screens.ScreenKeyboardEcho());
        if (ScreenKeyboard.Available)
        {
            Log.Info("core", $"screen keyboard: available (SteamOS in Game Mode)");
        }

        // --debug-net, process-wide like the two above: the session it reads comes and goes.
        if (_spec.DebugNet)
        {
            _netReadout = new UI.Overlays.NetReadout();
            AddChild(_netReadout);
        }

        // Only on the path a recipient takes. Every other entry carries a content arg, which is a
        // developer's launch, and the log is where that reader already looks. ⚠ Keep this ahead of
        // the music's build: a stale tree is rewritten by the screen, and an open sound archive would hold it.
        if (_spec.ShowsMenu && _spec.MovieName == null
            && UI.Screens.ExtractionFlow.ProblemAt(dataRoot) is var problem && problem != UI.Screens.DataProblem.None)
        {
            ShowExtractionScreen(problem);
            return;
        }

        // A current tree the Original menus cannot read stops at the same screen, naming the fault.
        if (_spec.ShowsMenu && _spec.MovieName == null
            && UI.Menu.Original.OriginalAvailability.Load(dataRoot, out string? unusable) == null)
        {
            ShowExtractionScreen(UI.Screens.DataProblem.Unusable, unusable);
            return;
        }

        // The music channel, once per process and after every early-quit probe: one player that
        // outlives every session, over a sound archive of its own for the same reason (D37's
        // wiring contract, step 1).
        _audio.Build(_paths, _masterSeed);

        // --movie=: one cinema, then quit. No world, no menu, and no session behind it, which is
        // what makes the audio sync judgeable at the controls before any flow plays a cinema.
        if (_spec.MovieName is { } cinemaName)
        {
            _audio.PlayCinema(cinemaName, () => GetTree().Quit());
            return;
        }

        // No content-selecting arg (or explicit --menu): show the launchscreen. Its selection
        // derives the session spec and calls LaunchSession, so there is one downstream build
        // path; Esc from a menu-launched flight returns here (ReturnToMenu).
        if (_spec.ShowsMenu)
        {
            EnterMenu();
            return;
        }

        // --debug-load stands the real screen over a CLI launch, deferred build and all. That is
        // the only way to watch the bar with nobody at the menu.
        if (_spec.DebugLoad != null)
        {
            BeginLaunch();
            return;
        }

        // Before the build, because the session reads its wire in its own constructor.
        _net.OpenCli(_spec, _paths.MessagesPath);
        _net.SeatLocalField(_spec, _paths.MessagesPath);
        // A CLI launch has no load screen, so the cover is the whole of what stands between the
        // build and the session's first real frame. Same rule as the interactive paths: a session
        // starts from dark, whatever opened it.
        RaiseStartCover();
        LaunchSession();
    }

    public override void _Notification(int what)
    {
        // The APPLICATION_* pair, not WM_WINDOW_*: that's what a real focus change delivers
        // here. See docs/verification.md's SHELL-14 for the alt-tab-vs-minimise gotcha this was
        // measured against. Godot 4's constants are longs; _Notification hands us an int.
        if (what == (int)NotificationApplicationFocusOut)
        {
            _audio.SetFocusMuted(true);
        }
        else if (what == (int)NotificationApplicationFocusIn)
        {
            _audio.SetFocusMuted(false);
        }
    }

    // The root node's own teardown, reached on an ordinary quit
    // (GetTree().Quit() or the window's close button), never on a kill/crash, which is what the
    // sidecar's flush-interval loss bound (HitchSidecar's own doc) covers instead.
    public override void _ExitTree()
    {
        SweepXrShaderCache();
        // ⚠ Do not move this below the teardown calls, nor out of here. The engine disposes the
        // peers a service thread may still poll, and a probe's quit closes no transport itself.
        try
        {
            int closed = Net.EnetTransport.CloseAll();
            if (closed > 0)
            {
                Log.Info("core", $"net: closed {closed} transport(s) still open at quit");
            }
        }
        catch (System.Exception e)
        {
            Log.Error("core", $"net: closing the open transports at quit failed: {e}");
        }

        DrainLowPriorityTasks();
        _instruments.Exit();
        _audio.Exit();
        SettleFinalizers();
        MirrorEngineLog();
    }

    public override void _Input(InputEvent @event)
    {
        // A typed character is the event's alone: the layout that produced it is not a held state
        // any poll can read. Taken in _Input, where no screen marks a key handled, so none can
        // hide a character from the menu seats.
        if (@event is InputEventKey key)
        {
            if (UI.Boards.MenuInput.IsPasteChord(key))
            {
                UI.Boards.TypedText.Live.FeedPaste();
            }
            else if (UI.Boards.MenuInput.IsCopyChord(key))
            {
                // A hosting door is the one thing on a menu with something to copy: its join code,
                // else its address.
                if (_menu.Host is { Shown: true })
                {
                    _net.CopyForGuests();
                }
            }
            else
            {
                UI.Boards.TypedText.Live.Feed(key.Pressed, key.Echo, key.Unicode);
            }
        }

        // The wheel is an event, never a held state, so it is counted here and handed to the
        // menu seat's poll; nothing else about it is read while the menu is up.
        if (_menu.Host is { Shown: true } && @event is InputEventMouseButton { Pressed: true } wheel)
        {
            switch (wheel.ButtonIndex)
            {
                case MouseButton.WheelDown:
                    _menu.TurnWheel(1);
                    break;
                case MouseButton.WheelUp:
                    _menu.TurnWheel(-1);
                    break;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            // While the menu is up it owns Esc (back / quit from the Mode screen).
            if (_menu.Host is { Shown: true })
                return;
            // ⚠ Esc no longer leaves a live flight; it opens the pause board, whose Exit item does.
            // FlightController polls it as a pause toggle, so nothing is done here.
            if (_session is { InSession: true })
                return;
            BlankAndQuit();
            return;
        }
        // F12 anywhere (orbit view or free flight): grab the current frame to a file.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            Tooling.CaptureDirector.SaveScreenshot(GetViewport());
            return;
        }
        // F11 anywhere: print args that reproduce each pane's camera and, in flight, each
        // aircraft's placement. A deterministic --screenshot run then replays a hand-framed view.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            _captureDirector.PrintPlacement(_session?.Spec ?? _spec, _session?.Rigs ?? NoRigs, _camera, _orbit);
            return;
        }
        // F10 in the viewer: export the plane on screen, current livery and damage state baked
        // in, to a timestamped .glb under the repo's git-ignored Exports/ folder.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F10 })
        {
            Tooling.GltfExporter.ExportToExports(_session?.Plane, _spec.PlaneName);
            return;
        }
    }

    public override void _Process(double delta)
    {
        UI.Boards.TypedText.Live.Stamp(Engine.GetProcessFrames());
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
        // The instruments stay on wall time: an instrument that freezes with the thing it measures
        // reports nothing. The session node processes one notch ahead (-1000), so its declared work
        // is already in the frame they close.
        double frameMs = _instruments.BeginFrame();
        // The main menu only: deeper screens have plaques along the bottom edge that the stamp
        // overlaps at a Steam Deck's aspect. The
        // extraction screen too, since a player whose extraction failed needs the logs icon most.
        _buildStamp?.Tick(_menu.Host is { OnMainMenu: true } || _extractionScreen != null);
        TickNetReadout(delta);
        _instruments.EndFrame(delta, ClockNow);
        // Wall time, like the instruments above: the score is not part of the simulation, and a
        // paused or stepped session must not stall a fade halfway.
        _audio.Tick((float)delta);

        _captureDirector.Tick(GetViewport(), GetTree(), _spec, ClockNow, _orbit, _camera,
            _session?.Plane, _menu.Host is { Shown: true } || _extractionScreen != null || _spec.MovieName != null);
        _gltfExporter.Tick(_session?.Plane, GetTree(), _spec);

        // A campaign mission that ended during the session's own step: free it and reopen the
        // launchscreen on the debrief, here where a QueueFree is safe.
        if (_pendingDebrief is { } debrief)
        {
            _pendingDebrief = null;
            OpenDebrief(debrief.Profile, debrief.Result);
        }

        // The same handover for an Instant Action mission whose presentation carries a wrap-up page.
        if (_pendingWrapup is { } wrapup)
        {
            _pendingWrapup = null;
            Log.Info("core", $"ia: {(wrapup.Won ? "COMPLETE" : "FAILED")}: arrived at the menu's wrap-up page");
            ReturnToMenu(new InstantActionWrapupReturn(wrapup));
        }

        // ⚠ Only while the session still flies: a pause-sheet exit in the same frame already left,
        // and ExitSession with no session quits the game.
        if (_pendingLobbyLanding)
        {
            _pendingLobbyLanding = false;
            if (_session is { InSession: true })
            {
                ExitSession();
            }
        }

        _net.TickCoop(delta, _session);
        _net.TickVersus(delta, _session);

        // An Options apply, one frame after the exit that asked for it.
        if (_pendingApply is { } applied)
        {
            _pendingApply = null;
            ApplyOptions(applied);
        }

        // Null only on the frame an early quit leaves, which built no sun.
        _settings?.Tick(frameMs, _spec, _session);

        if (_session is { InSession: true } && GameClock.Current is { } diagClock)
        {
            ShaderTwins.EnhancedDrawn |= GraphicsMode.Enhanced;
            Tooling.ShaderDiagnostics.Tick(GetTree().Root, diagClock.Frame);
        }

        // Dropped here rather than by the cover itself, so one node owns both screens a launch
        // raises. It ticks after this node, so what is read is the state it last painted.
        if (_startFade is { Finished: true })
        {
            DropStartCover();
        }

        // Last in the frame, where the build used to happen anyway: the launchscreen and the boards
        // are children, so they process AFTER this node, and a build they asked for landed here.
        RunOwedLaunch();
        // After everything above, which is where the launchscreen's own process callback ran when
        // it was a child ticking itself: the capture director reads the menu as it stood before
        // this frame's presses, as it always did.
        _menu.Host?.Tick(MenuStep(delta));
    }

    // Where a flight left early lands, taken from the launch that starts it. Every menu launch path
    // writes it here, ExitSession reads it back, and the rule stands in one place.
    // ⚠ Keep it internal rather than private. Nothing instantiates a Launcher headlessly, so the
    // launch-return suite pins this round trip on the live node or not at all.
    internal void LaunchedFrom(MenuExit exit) => ExitDestination = MenuReturnDestination.ForLaunch(exit);

    // --synthetic-data replaces the data root, whatever it held, with a tree written into scratch.
    // The switch alone opts in, and a synthetic tree is named in the log on every run that reads one.
    // Answers the data root this run reads. ⚠ Do not fall back to the resolved root when the tree
    // fails; see _syntheticError.
    private string ResolveSyntheticData(string repoRoot, string dataRoot)
    {
        if (!_spec.SyntheticData)
        {
            if (Tooling.SyntheticData.Marks(dataRoot))
            {
                Log.Warn("core", $"SYNTHETIC DATA: the data root {dataRoot} holds an invented test tree, not an extraction; every asset this run reads is invented");
            }
            return dataRoot;
        }
        string replaced = dataRoot;
        bool installThere = !UI.Screens.NoGameDataScreen.Missing(replaced);
        string synthetic = Tooling.SyntheticData.ScratchRoot(repoRoot);
        try
        {
            Tooling.SyntheticData.Build(Tooling.SyntheticData.FixturesUnder(repoRoot), synthetic, UI.Menu.Original.SyntheticShell.TreeFamilies);
        }
        catch (System.Exception e)
        {
            // Every failure alike, a malformed record included: each one leaves the tree unusable.
            _syntheticError = $"{e.GetType().Name}: {e.Message}";
            return synthetic;
        }
        Flight.Weapons.StockLoadouts.Supplement =
            Tooling.SyntheticPlane.LoadoutsUnder(Tooling.SyntheticData.FixturesUnder(repoRoot));
        SessionSpec.DefaultPlane = Tooling.SyntheticPlane.Plane;
        string families = string.Join(",", System.Linq.Enumerable.Select(UI.Menu.Original.SyntheticShell.TreeFamilies, f => f.Name));
        Log.Warn("core", $"SYNTHETIC DATA: --synthetic-data reads the invented tree {synthetic} (families {families}) in place of the data root {replaced}{(installThere ? ", whose extraction this run does not read" : "")}");
        return synthetic;
    }

    // Runs on a worker thread, since the extraction must not hold the main thread. The per-frame
    // callbacks are switched off until the quit, because _Ready returned before building what they read.
    private void StartHeadlessExtraction(string install, string repoRoot, string dataRoot)
    {
        SetProcess(false);
        SetPhysicsProcess(false);
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        string unzbd = _spec.UnzbdPath is { } named ? Path.GetFullPath(named) : Extraction.ExtractionRun.DefaultUnzbd(repoRoot, _exported);
        var request = new Extraction.ExtractionRequest(install, dataRoot, unzbd, _spec.ExtractForce, _spec.ExtractUnzip, _spec.ExtractAllowMissing);
        System.Threading.Tasks.Task.Run(() =>
        {
            int code;
            try
            {
                code = Extraction.ExtractionRun.RunToConsole(request, Log.Raw, line => Log.Error("core", $"{line}"));
            }
            catch (System.Exception e)
            {
                // Anything unforeseen still ends the process with a verdict, never a hang.
                Log.Error("core", $"extraction crashed", e);
                code = 1;
            }

            Callable.From(() => GetTree().Quit(code)).CallDeferred();
        });
    }

    /// <summary>Empties the worker pool's low-priority queue before the engine's exit asks every
    /// worker to report idle. A worker that finds a low-priority task still queued then sleeps
    /// uncounted, and nothing wakes it, so the process hangs after its last frame. Pipelines left
    /// compiling in the background after a shader change fill that queue. docs/verification.md
    /// SHELL-21 holds the stacks and the measurement.
    /// ⚠ The sentinel must be low priority: the queue is FIFO, so its completion is the proof.</summary>
    private void DrainLowPriorityTasks()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // The render thread's last frame may still be queueing compiles; settle it first.
        RenderingServer.ForceSync();
        long sentinel = WorkerThreadPool.AddTask(Callable.From(() => { }), highPriority: false, "ExitDrain");
        WorkerThreadPool.WaitForTaskCompletion(sentinel);
        Log.Info("core", $"exit drain: low-priority tasks settled in {watch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    /// <summary>Runs every pending finalizer while the engine is whole, then forbids any further
    /// collection. A collection during teardown, often a background one already running here, queues
    /// Godot wrappers for finalizing. Those finalizers race CSharpLanguage::finalize and crash the
    /// process after its work is done. docs/verification.md LOG-27 holds the mechanism and the rates.
    /// ⚠ Keep it last before the log mirror, and never end the region: the quit must not collect.</summary>
    private void SettleFinalizers()
    {
        // Room for the teardown's own allocations, the disposal tracker's wrapper snapshot among them.
        // Past it the runtime collects again and the race is back.
        const long noCollectionBytes = 256L << 20;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // A blocking collection also waits out a background one in flight.
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        string region;
        try
        {
            // Its own full collection finds what the first round's finalizers let go.
            region = System.GC.TryStartNoGCRegion(noCollectionBytes) ? "held" : "refused";
        }
        catch (System.Exception e) when (e is System.ArgumentOutOfRangeException or System.InvalidOperationException)
        {
            region = $"refused ({e.GetType().Name})";
        }
        System.GC.WaitForPendingFinalizers();
        Log.Info("core", $"exit finalizers: settled in {watch.Elapsed.TotalMilliseconds:0.0} ms, no-collection region {region}");
    }

    // The step the menus advance on. A deterministic run gives them the sim's own, for the reason
    // the sim takes it: what a capture shows must be a function of the frame count and nothing
    // else, and the screens that animate (the briefing's reveal, a board's background movie)
    // otherwise land wherever this machine's frame times put them.
    private float MenuStep(double delta) => _spec.Det ? GameClock.FixedDt : (float)delta;

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

    // The deferred half of BeginLaunch: builds once the load screen has had a frame to render.
    // A QueueFree'd session is gone by now too, so the outgoing world's exit-tree duties (the
    // published clock, the world lights, the camera restore) cannot land on top of the new one.
    private void RunOwedLaunch()
    {
        // This is the load screen's second half. The session exists, and what it ordered for the
        // load is carried out a step a frame behind the same screen. A step is one wave aeroplane,
        // so the launch frame that needs it later binds a finished one instead of building it.
        if (_loadStepsRun >= 0)
        {
            if (_session is { } loading && loading.StepOwedLoad())
            {
                _loadStepsRun++;
                return;
            }

            // A network flight waiting for its other machines keeps the screen up, since its world
            // stands still until they have all loaded.
            if (_session is { StartHeld: true })
            {
                _startHeldFrames++;
                return;
            }

            Log.Info("ui", $"load screen: {_loadStepsRun} owed build step(s) run behind the screen, {_startHeldFrames} frame(s) held for the other machines");
            _loadStepsRun = -1;
            HideLoadScreen();
            if (_startHeldFrames > 0)
            {
                // The cover's own hold is capped from when it went up, so a long wait would spend
                // it behind the screen. A fresh one covers the frame the world first runs on.
                RaiseStartCover();
            }

            _startHeldFrames = 0;
            return;
        }

        if (_launchFramesWaited < 0 || _launchFramesWaited++ < 1)
        {
            return;
        }
        _launchFramesWaited = -1;
        bool built = TryLaunchSession();
        // The screen stays up while the build's own owed steps run and a network start is held,
        // and comes down on the frame both end. A load screen left up past that would draw over the first frame of the
        // world, and over a --screenshot capture.
        if (built && _session is { } loaded && loaded.StepOwedLoad() is var owed && (owed || loaded.StartHeld))
        {
            _loadStepsRun = owed ? 1 : 0;
            _startHeldFrames = 0;
            return;
        }
        HideLoadScreen();
        if (!built)
        {
            // Nothing to uncover: the cover would otherwise hold over whatever the failure left on
            // screen for the whole of its cap.
            DropStartCover();
        }

        if (built || !_menuDriven)
        {
            return; // a CLI launch leaves the log to tell the story, as it always did
        }
        ReturnToMenu(MenuReturnDestination.TopLevel);
        // Original says so in its warning box. Without the box its top level shows bare, which
        // reads as a misclick.
        Log.Warn("ui", $"menu: the build failed, back at the top level of {_menu.Host?.Selected}");
        if (_menu.Host?.Active is UI.Menu.Original.OriginalPresentation original)
        {
            original.RaiseWarning(FailedBuildWarning());
        }
    }

    // The words of Original's failed-build box. An extraction with no chapter world fails every
    // flight the same way, so it names the fix; anything else is the log's to explain.
    private string FailedBuildWarning() => SessionPaths.HasAnyChapterWorld(_paths.DataRoot)
        ? $"The mission could not be loaded. The log says why: open it with the \"{UI.Screens.BuildStamp.LogsTooltip}\" icon beside the version number."
        : "The extracted game data holds no chapter worlds, so no mission can be flown. "
            + $"Extract again from a full install of Crimson Skies, {Extraction.InstallLocator.ExpectedFolder}.";

    // A build that throws counts as one that failed. The caller's own failure path then takes the
    // load screen down and returns a menu launch to the menu. An escaped exception would leave the
    // screen up for good, with a network guest dropped behind it.
    private bool TryLaunchSession()
    {
        try
        {
            return LaunchSession();
        }
        catch (System.Exception e)
        {
            Log.Error("core", $"launch: the session build threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // Shows the load screen and owes a build from the next frame. Every interactive path in (the
    // launchscreen's Fly, the boards' Restart) comes through here; the CLI launch in _Ready does
    // not, so no scripted or golden run gains a frame it did not have before.
    private void BeginLaunch()
    {
        // The one place the session leaves the boards for a mission, so the one place the menu
        // score stops. What plays next is the mission's own business: a campaign mission cues
        // prebattle from its objectives graph, and Instant Action ships silent (docs/org/music.md).
        _audio.StopMusic();
        RaiseStartCover();
        ShowLoadScreen(_spec.CampaignProfile != null, _spec.IaDef?.MissionType);
        _launchFramesWaited = 0;
    }

    // The cover goes up before the load screen that hides it. The frame the screen comes down on is
    // then already covered, and no frame between the two shows the world. Replacing a cover still
    // up (a relaunch straight out of a session) starts the hold again, which is what a fresh build
    // wants.
    private void RaiseStartCover()
    {
        DropStartCover();
        _startFade = UI.Screens.SessionStartFade.Build(
            _spec.Det, () => _session is { InSession: true, FirstFrameReady: true, StartHeld: false });
        if (_startFade != null)
        {
            AddChild(_startFade);
        }
    }

    private void DropStartCover()
    {
        if (_startFade == null)
        {
            return;
        }

        RemoveChild(_startFade);
        _startFade.QueueFree();
        _startFade = null;
    }

    // The load screen over the whole window, on the board layer; what it draws is LaunchBoards'.
    private void ShowLoadScreen(bool campaign, string? missionType, int? missionSeq = null, string? briefing = null)
    {
        var board = Boards.LoadScreen(campaign, missionType, missionSeq, _net.TeamedLobbyFlight, briefing);
        _loadLayer = new CanvasLayer { Name = "load_board", Layer = UI.Boards.HudLayers.Board };
        _loadLayer.AddChild(board);
        AddChild(_loadLayer);
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
        // Undoes the menu's black before anything reads the environment: the world's reflections
        // and the cockpit pass's copy of it both take the sky from here (Utils/WorldBackdrop.cs).
        WorldBackdrop.Sky(_env);
        // The saved gameplay options, read at every launch so an Options apply reaches the next
        // flight in the same process. The flag and --det rules are the spec's own.
        var saved = OptionsStore.UserOptions().Load();
        _spec = _spec.WithSavedDifficulty(saved.Difficulty).WithSavedNearestAfterKill(saved.NearestAfterKill)
            .WithSavedDefaultView(saved.DefaultView).WithSavedAutoHeadTurn(saved.AutoHeadTurn)
            .WithSavedLeadingAimAssist(saved.LeadingAimAssist).WithSavedLeadMarker(saved.LeadMarker)
            .WithHeadsetView(_xr != null);
        LoadProgress.Report(LoadStep.RenderState);
        // Set per launch, not once at startup: a relaunch can change chapter, and the original
        // re-sources the new chapter's adjust.gw at the same point.
        float mipBias = Mech3.TextureArchive.MipBias(_paths.InterpPath, _spec.Chapter);
        RenderingServer.GlobalShaderParameterSet("csky_mip_bias", mipBias);
        Log.Info("world", $"mip bias: {_spec.Chapter} adjust.gw MipBias={mipBias.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
        LoadProgress.Report(LoadStep.ChapterPaths);
        _session = new GameSession(_spec, _net.WithNet(new LauncherContext
        {
            Paths = _paths,
            ProbeRunner = _probeRunner,
            CaptureDirector = _captureDirector,
            MasterSeed = _masterSeed,
            Camera = _camera,
            Xr = _xr?.Rig,
            Orbit = _orbit,
            Sun = _sun,
            Env = _env,
            MenuDriven = _menuDriven,
            MenuPads = _menuPads,
            ExitSession = ExitSession,
            RestartSession = RestartSession,
            ToggleGraphicsMode = _settings.RequestToggle,
            PauseOptions = BuildPauseOptions,
            CampaignMissionEnded = _menuDriven
                ? (profile, result) => _pendingDebrief = (profile, result)
                : null,
            InstantActionWrapup = _menuDriven ? snapshot => _pendingWrapup = snapshot : null,

            Music = _audio.Music,
        }, _menuDriven ? () => _pendingLobbyLanding = true : null));
        AddChild(_session);
        bool built = _session.StartSession();
        // A CLI launch has no load screen to yield behind. What the build ordered for the load runs
        // here, inside the same block as the rest of the build. The menu path steps it one a frame
        // with the screen still up instead (RunOwedLaunch).
        if (built && _loadLayer == null)
        {
            int steps = 0;
            while (_session.StepOwedLoad())
            {
                steps++;
            }

            if (steps > 0)
            {
                Log.Info("world", $"load: {steps} owed build step(s) run inside the build (no load screen on this launch)");
            }
        }
        // A build stalls the frame loop, and the frames either side of it are not neighbours.
        // Every instrument drops its baseline here rather than reporting the build as a hitch.
        _instruments.Rearm();
        return built;
    }

    private void SetupLighting()
    {
        _sun = new DirectionalLight3D
        {
            // The defaults only, hand-picked so the plane model reads in the viewer, the menu, and
            // any mission with no weather.json. A flight with weather overwrites the bearing and
            // the energy per zone-apply (WeatherRig.ApplyZone), off the zone's authored SUNLIGHT.
            RotationDegrees = new Vector3(-45, 150, 0),
            LightEnergy = WeatherRig.DefaultEnergies.Sun,
            // Off in the faithful path: the world is built fullbright and unshaded, so the only
            // thing a shadow pass reaches is one aircraft shadowing another, and the original's own
            // projected-blob shadow is not a shadow map either. Enhanced mode turns it on below.
            ShadowEnabled = false,
        };
        // Enhanced mode alone: a lit world has surfaces a shadow pass can land on.
        if (GraphicsMode.Enhanced)
            EnhancedLook.ApplySun(_sun, true);
        AddChild(_sun);
        // The modal day pair under this bearing, after the AddChild so the bearing is the one the
        // light wears in the tree. A session with no weather.json never reaches ApplyZone, and
        // this write shades its planes like a day zone.
        (Vector3 dayDiffuse, Vector3 dayAmbient) = WeatherRig.DefaultSunlightRgb;
        WeatherRig.WriteSunDirection(_sun);
        RenderingServer.GlobalShaderParameterSet("csky_sun_ambient_rgb", dayAmbient);
        RenderingServer.GlobalShaderParameterSet("csky_sun_diffuse_rgb", dayDiffuse);
        RenderingServer.GlobalShaderParameterSet("csky_sun_fill_rgb",
            Vector3.One * WeatherRig.PhotographFillAmbient(WeatherState.DefaultAmbient));

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            // ⚠ Colour-sourced in BOTH modes, never AmbientSource.Sky. A sky ambient fills a night
            // chapter's aircraft off the same daylight gradient a day one gets, and ignores the
            // pair written here. Per-zone values: WeatherRig.ApplyZone (docs/architecture.md).
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = WeatherRig.DefaultEnergies.Ambient,
        };
        // Enhanced mode alone: the faithful path's world is fullbright, so none of these passes has
        // anything to work on. The cockpit pass duplicates this Environment
        // (CockpitOverlay.NewOverlay) and inherits the settings.
        if (GraphicsMode.Enhanced)
            EnhancedLook.ApplyEnvironment(_env, true, _spec.SkippedPasses);
        AddChild(new WorldEnvironment { Environment = _env });
    }

    // The launchscreen, after the boot sequence when this launch plays one. Both a boot with data
    // and the extraction screen's hand-back come through here, so they reach the same menu.
    private void EnterMenu()
    {
        _menuDriven = true;
        // A player's flight survives a bad rig.json, flying without it; every other launch keeps the throw.
        Mech3.PlaneRigPatch.PlayerNotice = message => _menu.Host?.Features.Get<MenuNoticeFeature>().Post(message);
        if (_spec.PlaysBootSequence)
        {
            _audio.PlayBoot(() => ShowMenu(MenuReturnDestination.TopLevel));
            return;
        }

        ShowMenu(MenuReturnDestination.TopLevel);
    }

    // The screen a menu launch stops at when the data root holds no extraction, or one stamped under
    // another schema. Nothing that reads the tree is built yet. Esc leaves through _UnhandledInput,
    // which quits with neither a menu nor a session up; the screen keeps Esc while a run is going.
    private void ShowExtractionScreen(UI.Screens.DataProblem problem, string? detail = null)
    {
        string extracted = Path.Combine(_paths.DataRoot, Extraction.ExtractionRun.ExtractedFolder);
        if (problem == UI.Screens.DataProblem.Missing)
        {
            Log.Error("core", $"no extracted game data path={extracted}, {UI.Screens.NoGameDataScreen.Instruction}");
        }
        else if (problem == UI.Screens.DataProblem.Unusable)
        {
            Log.Error("core", $"the menus cannot read the game data ({detail}) path={extracted}, asking to extract again");
        }
        else if (problem == UI.Screens.DataProblem.Incomplete)
        {
            Log.Warn("core", $"the last extraction did not finish path={extracted}, asking to extract again");
        }
        else
        {
            ExtractionStamp.Standing(_paths.DataRoot, out int? found);
            Log.Warn("core", $"extraction stamp schema={found} but this build reads schema={ExtractionStamp.Schema} path={extracted}, asking to re-extract ({problem})");
        }

        // The screen honours --unzbd= as --extract does, which is how a worktree names a tool.
        string unzbd = _spec.UnzbdPath is { } named ? Path.GetFullPath(named) : Extraction.ExtractionRun.DefaultUnzbd(_paths.RepoRoot, _exported);
        string? remembered = Extraction.RememberedInstall.Get();
        var candidates = Extraction.InstallLocator.Candidates(Extraction.InstallSearchRoots.ForThisMachine(), remembered);
        string preFill = UI.Screens.ExtractionFlow.PreFill(remembered, candidates);
        Log.Info("core", $"extraction screen: problem={problem} remembered={remembered ?? "none"} candidates={candidates.Count} prefill={(preFill.Length == 0 ? "none" : preFill)} unzbd={unzbd}");

        // ⚠ A run that drives itself never writes the player's options, the rule --run-tests keeps.
        System.Action<string> remember = _spec.IsScripted
            ? _ => { }
        : Extraction.RememberedInstall.Set;
        var flow = new UI.Screens.ExtractionFlow(problem, _paths.DataRoot, unzbd, preFill,
            (request, progress, cancel) => RunExtraction(request, progress, cancel), remember)
        {
            Detail = detail,
        };
        _extractionScreen = UI.Screens.NoGameDataScreen.Build(flow, LeaveExtractionScreen, BlankAndQuit);
        AddChild(_extractionScreen);
        Callable.From(() => ApplyExtractionAid(_cli.MenuStartScreen)).CallDeferred();
    }

    // The worker's side of the screen: the one pipeline, with its console lines in the log.
    private Extraction.ExtractionResult RunExtraction(Extraction.ExtractionRequest request,
        System.Action<Extraction.ExtractionProgress> progress, System.Threading.CancellationToken cancel)
    {
        foreach (string line in Extraction.ExtractionRun.Header(request))
        {
            Log.Raw(line);
        }

        var result = Extraction.ExtractionRun.Run(request, report =>
        {
            foreach (string line in report.Lines)
            {
                Log.Raw(line);
            }

            progress(report);
        }, cancel);
        foreach (string line in result.Summary(request.Unzip))
        {
            Log.Raw(line);
        }

        return result;
    }

    // The screen's hand-back, after a run or the stale screen's Play anyway. The base paths were
    // resolved against a tree that has since changed, so they and the music are resolved again.
    private void LeaveExtractionScreen()
    {
        if (_extractionScreen is { } screen)
        {
            foreach (string warning in screen.Flow.Warnings)
            {
                Log.Warn("core", $"extraction: {warning}");
            }

            RemoveChild(screen);
            screen.QueueFree();
            _extractionScreen = null;
        }

        _paths = BasePaths.Resolve(_paths.RepoRoot, _paths.DataRoot, _spec);
        _audio.Build(_paths, _masterSeed);
        EnterMenu();
    }

    // The screen's screenshot doors, through --menu= like the menu's own: extract-picker[:<folder>]
    // opens the picker, and extract-run:<install> fills the field and presses Extract.
    private void ApplyExtractionAid(string aid)
    {
        if (_extractionScreen is not { } screen)
        {
            return;
        }

        int colon = aid.IndexOf(':');
        string name = colon < 0 ? aid : aid[..colon];
        string? argument = colon < 0 ? null : aid[(colon + 1)..];
        switch (name)
        {
            case "extract-picker":
                screen.OpenPicker(argument);
                break;
            case "extract-run":
                if (argument != null)
                {
                    screen.Flow.InstallPath = argument;
                }

                screen.Extract();
                break;
        }
    }

    // Shows the menu at a semantic destination, building the host on first use. Re-shown by
    // ReturnToMenu after the boards' Exit and a failed build, and by OpenDebrief after a campaign
    // mission. The --menu= aid is the cold start's alone: the presentation created inside the
    // first Show reads it, and it is consumed here so a return shows the destination itself.
    private void ShowMenu(MenuReturnDestination destination)
    {
        // A presentation is hidden a frame before the switch that replaces it, and nothing opaque
        // stands over the environment in between, so the menu owns a black background from its
        // first show until a launch takes it back (Utils/WorldBackdrop.cs).
        WorldBackdrop.Black(_env);
        string aid = _menu.Show(_cli, _spec, _buildStamp, destination);
        // A build tears the load screen down before anything renders, so a shot needs a door that
        // leaves it standing. The arguments: a campaign mission, an Instant Action mission type,
        // or a Dogfight's chapter and mode letter.
        if (aid.StartsWith("loadboard-mp", System.StringComparison.Ordinal))
        {
            var (chapter, letter) = LaunchBoards.MultiplayerDoor(aid);
            ShowLoadScreen(false, null, briefing: UI.Screens.LoadScreens.MultiplayerKey(chapter, letter));
        }
        else if (aid.StartsWith("loadboard-campaign", System.StringComparison.Ordinal))
        {
            var parts = aid.Split(':');
            ShowLoadScreen(
                campaign: true,
                missionType: null,
                missionSeq: System.Math.Max(
                    1, parts.Length > 1 && int.TryParse(parts[1], out int cm) ? cm : 1) - 1);
        }
        else if (aid.StartsWith("loadboard", System.StringComparison.Ordinal))
        {
            int colon = aid.IndexOf(':');
            string missionType = colon < 0 ? string.Empty : aid[(colon + 1)..];
            ShowLoadScreen(
                false, missionType.Length > 0 ? missionType : _spec.IaDef?.MissionType);
        }

        Boards.ShowPauseAid(aid, this);

        // Safe on every entry: a cue for the track already playing is a no-op.
        _audio.CueMenu();
    }

    // The Options route's apply from the menu: persist and apply every choice, then end the active
    // presentation (discarding every feature's transient state) and show the same one again at its
    // top level. No re-select: the presentation is the command line's alone, settled once.
    private void ApplyOptions(OptionsApplyExit applied)
    {
        if (_menu.Host is not { } host)
        {
            return;
        }

        bool? vrBefore = OptionsStore.UserOptions().Load().VrMode;
        PersistOptions(applied);
        // A changed VR mode takes a new process, OpenXR starting only with the engine.
        string? flatOnly = XrFlatOnly();
        if (XrRelaunch.OnApply(vrBefore, applied.VrMode, _xrRequested, flatOnly) is { } vrOn
            && Relaunch(vrOn, "the Options apply changed VR mode", skipIntro: true))
        {
            return;
        }

        if ((applied.VrMode == true) != (vrBefore == true) && flatOnly != null)
        {
            Log.Info("core", $"xr: VR mode saved {(applied.VrMode == true ? "on" : "off")}; a {flatOnly} run does not restart for it");
        }

        host.Deactivate();
        ShowMenu(MenuReturnDestination.TopLevel);
    }

    // VR mode at boot. Every launch first sweeps what a headset launch that never ended left, and
    // one that compiled the stereo variants then opens its own watch (XrShaderCache.Boot). Then a
    // launch with VR mode saved on that did not ask for OpenXR restarts into it. True means this
    // process is quitting for that.
    private bool StartXrMode()
    {
        var args = XrRelaunch.WithoutProgram(System.Environment.GetCommandLineArgs());
        _xrRequested = XrLaunch.Requested(args, ProjectSettings.GetSetting(XrLaunch.EnabledSetting).AsBool());
        string userDir = ProjectSettings.GlobalizePath("user://");
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
        {
            _xrCache = XrShaderCache.Boot(userDir, _xrRequested, self.Id, self.StartTime.ToUniversalTime(),
                XrShaderCache.LiveStartUtc, out int? swept);
            if (swept is { } count)
            {
                Log.Info("core", $"xr: a headset launch ended without sweeping the shader cache; swept {count} files it wrote");
            }
        }

        string? flatOnly = XrFlatOnly();
        bool? saved = flatOnly == null ? OptionsStore.UserOptions().Load().VrMode : null;
        if (!XrRelaunch.AtBoot(saved, XrRelaunch.Mode(args), flatOnly))
        {
            return false;
        }

        return Relaunch(true, "VR mode is saved on", skipIntro: false);
    }

    // Why this process never restarts for VR mode, or null (XrRelaunch.FlatOnly).
    private string? XrFlatOnly() =>
        XrRelaunch.FlatOnly(_cli, DisplayServer.GetName() == "headless", OS.GetName() == "Windows");

    // Starts this executable again with VR mode on or off, then quits. A headset launch's cache is
    // swept first and watched again if the start fails (XrShaderCache.EndThenStart). False when the
    // start failed: this process carries on, and the menu's next show tells the player why.
    private bool Relaunch(bool on, string why, bool skipIntro)
    {
        var args = XrRelaunch.Arguments(XrRelaunch.WithoutProgram(System.Environment.GetCommandLineArgs()), on, skipIntro);
        string exe = OS.GetExecutablePath();
        string[] argv = new string[args.Count];
        for (int i = 0; i < argv.Length; i++)
        {
            argv[i] = args[i];
        }

        int swept = 0;
        int pid = _xrCache?.EndThenStart(() => OS.CreateProcess(exe, argv), out swept) ?? OS.CreateProcess(exe, argv);
        LogXrShaderCacheSwept(swept);
        if (pid < 0)
        {
            Log.Error("core", $"xr: could not restart {(on ? "into the headset" : "flat")} ({why}): {exe} {string.Join(' ', argv)}");
            _xrStartFailed = on;
            _menu.Host?.Features.Get<MenuNoticeFeature>().Post(XrRelaunch.StartFailedNotice(on));

            return false;
        }

        Log.Info("core", $"xr: restarting {(on ? "into the headset" : "flat")} ({why}) pid={pid}: {exe} {string.Join(' ', argv)}");
        if (_env != null)
        {
            BlankAndQuit();
        }
        else
        {
            GetTree().Quit();
        }

        return true;
    }

    // The headset's comfort settings, each flag beating the given word: the saved one at boot (none
    // under --det), the apply's after it. Both take effect at once, the render scale in the runtime.
    private void ApplyVrComfort(string? renderScaleWord, string? vignetteWord)
    {
        var scale = VrRenderScaleSetting.Resolve(_spec.XrRenderScale, renderScaleWord);
        var vignette = VrVignetteSetting.Resolve(_spec.XrVignette, vignetteWord);
        VrVignetteSetting.Current = VrVignetteSetting.LevelOf(vignette.Word);
        Log.Info("core", $"xr: comfort vignette={vignette.Word} source={VrVignetteSetting.Lookup.SourceName(vignette.Source)} render_scale={scale.Word} source={VrRenderScaleSetting.Lookup.SourceName(scale.Source)}");
        _xr?.ApplyRenderScale(VrRenderScaleSetting.ScaleOf(scale.Word));
    }

    // The headset launch's end: every shader cache file written since it started goes, once.
    private void SweepXrShaderCache() => LogXrShaderCacheSwept(_xrCache?.End() ?? 0);

    private void LogXrShaderCacheSwept(int swept)
    {
        if (swept > 0)
        {
            Log.Info("core", $"xr: swept {swept} shader cache files this headset launch wrote, so a flat launch reads none of them");
        }
    }

    // The Options apply for the menu above and the pause leaf: the settings (LiveSettings.Persist),
    // then a live headset's comfort settings. VR mode is saved and no more; the menu's ApplyOptions
    // restarts for it.
    private void PersistOptions(OptionsApplyExit applied)
    {
        _settings.Persist(applied, _spec, _session, headsetPaces: _xr is { PacesFrames: true });
        if (_xr != null)
        {
            ApplyVrComfort(applied.VrRenderScale, applied.VrVignette);
        }
    }

    // The in-flight Preferences leaf both pause boards open, composed by ProcessMenu. Its options
    // apply is PersistOptions, as the menu's is. ⚠ No ShowMenu: the flight returns to the sheet
    // over its own world, not tearing down what the pause stands on.
    private UI.Screens.PausePreferences? BuildPauseOptions() => _menu.PauseOptions(PersistOptions);

    // The host's exit sink: the one place a menu leaves through. The host has already hidden the
    // presentation, with its state kept so a failed build can show it again where it stood.
    private void OnMenuExit(MenuExit exit)
    {
        switch (exit)
        {
            case LaunchExit launch:
                StartSessionFromMenu(launch);
                break;
            case CampaignMissionExit mission:
                StartCampaignFromMenu(mission);
                break;
            case QuitExit:
                BlankAndQuit();
                break;
            case OptionsApplyExit applied:
                _pendingApply = applied;
                break;
        }
    }

    // A non-campaign launch: derive this session's spec, bind pads, start the session. A build
    // failure returns to the menu with a note instead of a blank screen.
    // ⚠ Derive the spec from _cli, never the outgoing _spec, so nothing the last session
    // settled leaks into this one. Pads are the deliberate exception: they come from the join
    // flow, not args, so they stay session state rather than a spec field.
    private void StartSessionFromMenu(LaunchExit launch)
    {
        var (planes, pads, fits, customs) = Unpack(launch.Seats);
        LaunchedFrom(launch);
        _net.TakeLaunch(launch.Net, planes, fits, customs, _paths.MessagesPath, _paths.ZrdrPath);
        _spec = SessionSpec.FromMenu(_cli, launch.Chapter, planes, launch.Mode, launch.InstantAction, fits, customs,
            launch.Match?.KillTarget, launch.Match?.TimeLimitMinutes, launch.Match?.Lives, launch.Match?.AutoRespawn,
            launch.WingmanLoadout, launch.Match?.MissionType ?? DogfightMissionType.Deathmatch,
            launch.Match?.FlagHomeToCapture == true, launch.Bots);
        // Step the master so flying again is a new mission rather than a replay: without this every
        // relaunch re-derives the same spawn, opposition and liveries. ⚠ A pinned run must hold
        // still, which is what keeps the goldens and the perf harnesses reproducible.
        StepSortieSeed();
        // A local Dogfight with join board bots flies a seat roster with no wire, as a command-line
        // one does. It follows the seed step, since its draws read the master.
        _net.SeatLocalField(_spec, _paths.MessagesPath);
        BindMenuPads(pads);
        BeginLaunch();
    }

    // The seat choices as the four parallel lists the spec factories take. The fits ride
    // alongside the planes rather than inside them: FromMenu writes each menu-settable field
    // explicitly, so a chosen loadout has to be handed over or it would be dropped. A plane the
    // campaign exported flies with the ammunition and ordnance EXPORT wrote into it; a fit set on
    // the loadout screen is this sortie's own explicit pick and stands instead of the stored one.
    private (List<string> Planes, List<int[]> Pads, List<Flight.Weapons.LoadoutChoice?> Fits,
        List<Flight.Hangar.CustomPlaneDef?> Customs) Unpack(IReadOnlyList<MenuSeatChoice> seats)
    {
        var planes = new List<string>(seats.Count);
        var pads = new List<int[]>(seats.Count);
        var fits = new List<Flight.Weapons.LoadoutChoice?>(seats.Count);
        var customs = new List<Flight.Hangar.CustomPlaneDef?>(seats.Count);
        Flight.Weapons.StockLoadouts? stock = null;
        foreach (var seat in seats)
        {
            planes.Add(seat.PlaneNode);
            pads.Add(seat.Pads as int[] ?? new List<int>(seat.Pads).ToArray());
            customs.Add(seat.Custom);
            fits.Add(seat.Fit ?? (seat.Custom is { HasLoadout: true } custom
                ? CampaignLoadout.For(custom, stock ??= Flight.Weapons.StockLoadouts.Load())
                : null));
        }

        return (planes, pads, fits, customs);
    }

    // Steps the master so flying again is a new mission rather than a replay. A pinned run holds
    // still, which is what keeps the goldens and the perf harnesses reproducible.
    private void StepSortieSeed()
    {
        if (!_spec.SeedPinned)
        {
            _sortie++;
            _masterSeed = Rng.SortieSeed(_processSeed, _sortie);
        }
        LogMasterSeed();
    }

    // Honour the join flow's device binding rather than re-deriving it from the connected roster:
    // the pad that joined as P2 in the menu must be the pad that flies P2. Single player keeps the
    // any-pad policy (null), so every connected pad flies the one plane, as before.
    // ⚠ EVERY menu launch path has to come through here. Pads.AssignPads, the fallback a path that
    // skips it lands on, gives P1 every pad no later seat claimed, so a two-seat launch with one
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
    // NOT named here, CampaignDirector.ResolveSpec reads them out of cm_sequence in the session's
    // constructor, so one place resolves a story position whether it came from a cabin or a
    // --campaign= command line.
    private void StartCampaignFromMenu(CampaignMissionExit mission)
    {
        var (planes, pads, fits, customs) = Unpack(mission.Seats);
        LaunchedFrom(mission);
        _net.TakeCoopLaunch(mission.Net, planes, fits, customs, mission.Profile, _cli.ProfilesDir, mission.Wingman);
        _spec = SessionSpec.FromCampaign(_cli, mission.Profile, mission.MissionSeq, planes,
            pads.Count, fits, customs, mission.Wingman);
        StepSortieSeed();
        BindMenuPads(pads);
        BeginLaunch();
    }

    // A flown campaign mission is over, won or lost. Free the world and reopen the menu on the
    // flown mission's debrief, which the presentation maps onto its own book. Reached
    // from the session's CampaignMissionEnded by way of _pendingDebrief, one frame later, carrying
    // the result the mission ended with.
    private void OpenDebrief(string profile, CampaignMissionResult result)
    {
        Log.Info("core", $"campaign: {result.Outcome}, arrived at the debrief with '{profile}'");
        // A co-op guest has no profile, and its debrief is the one its host shows.
        ReturnToMenu(profile.Length == 0
            ? new CoopGuestReturn(result.Attempt)
            : new DebriefReturn(profile, result.Attempt.Seq, result.Outcome == MissionOutcome.Won));
    }

    // The mission boards' Restart (Instant Action and campaign): free this session and build a
    // fresh one from the same spec, behind the load screen. A rerun in place cannot put the
    // mission's opposition or objectives back (waves, the ace, a killed zeppelin and the campaign
    // graph all live in the world), so the world is rebuilt instead.
    // ⚠ Steps the seed exactly as flying again from the menu does, so an unpinned restart is a new
    // mission and a pinned one (--seed=/--det) still repeats.
    private void RestartSession()
    {
        // ⚠ Never rebuild a network flight past its door; NetFlight says which ones it may.
        if (_net.RefusesRestart())
        {
            return;
        }

        if (_session != null)
        {
            // Freed at the end of THIS frame, so the build owed for the next one finds it gone.
            _settings.DropCover();
            _session.QueueFree();
            _session = null;
        }

        // A co-op host relaunches through its door here, or lands at the menu when it will not.
        if (!_net.Restart(_spec))
        {
            return;
        }

        StepSortieSeed();
        Log.Info("core", $"restart: rebuilding {_spec.Chapter} / {_spec.ModeName} from the same settings");
        BeginLaunch();
    }

    // Prints the master the next session will draw from. Per session rather than per process
    // because an unpinned run advances it: the seed a mission actually flew on is the one worth
    // having in the log, so an interesting one can be pinned with `--seed=`.
    private void LogMasterSeed()
    {
        string how = _spec.SeedPinned ? "pinned" : Log.Format($"sortie {_sortie}, --seed=N to pin");
        Log.Info("core", $"rng: master seed {_masterSeed} ({how})");
    }

    // The boards' Exit item, the pause sheet's among them: back to the screen this flight was
    // launched from when the process launched into the menu, out of the game otherwise, and
    // reachable from a pad as much as from Esc. Every way a session ends without a result
    // (the sheet, the wrap-up board, the scoreboard) arrives here, so one destination serves them.
    private void ExitSession()
    {
        if (_menuDriven && _session is { InSession: true })
        {
            ReturnToMenu(_net.Landing(_session) ?? _exitDestination);
            return;
        }
        BlankAndQuit();
    }

    // --debug-net: once a wall second, the session's desync counters to the log and the corner.
    // Wall time, so a paused or stalled session still reports what its wire is doing.
    private void TickNetReadout(double delta)
    {
        if (_netReadout is not { } readout)
        {
            return;
        }

        _sinceNetReadout += delta;
        if (_sinceNetReadout < 1.0)
        {
            return;
        }

        _sinceNetReadout = 0.0;
        if (_session?.Wire.Link is not { } net)
        {
            readout.Show(null);
            return;
        }

        var poses = default(Net.RemotePoseTally);
        foreach (var rig in _session.SeatRigs)
        {
            if (rig.Controller?.RemotePoses is { } buffer)
            {
                poses = poses.Plus(buffer.Tally);
            }
        }

        string line = Net.NetInstruments.Describe(net, poses, _session.Wire.Clock, _session.Wire.Ping);
        Log.Info("core", $"{line}");
        readout.Show(line);
    }

    /// <summary>The three quits reached from a frame that is still drawing: blacks the persistent
    /// <c>WorldEnvironment</c>'s background, then ends the frame. <c>Quit()</c> ends the frame
    /// rather than the process, so one more frame is drawn and that image is held on screen for
    /// the whole shutdown, and the menu host hides its opaque backdrop one call before the exit
    /// reaches here. The held frame would otherwise be the procedural sky.
    /// ⚠ Every probe exit keeps the bare <c>Quit()</c>: no headless run may pay for this.</summary>
    private void BlankAndQuit()
    {
        _net.Close();
        WorldBackdrop.Black(_env);
        GetTree().Quit();
    }

    // Frees the current session node and shows the menu again at a semantic destination, the
    // in-process rebuild path for the boards' Exit item, for failed builds and for the debrief.
    // The whole session subtree hangs under the node, so `QueueFree` tears it down; the non-child
    // duties (the published clock, the world lights, the session texture archive, the main-camera
    // restore) run in the node's `_Notification` on `NotificationExitTree`. The camera, lights and
    // shader globals persist on `this`.
    private void ReturnToMenu(MenuReturnDestination destination)
    {
        // A co-op guest leaving a flight its host still flies says so before the wire goes back.
        _net.LeaveCoopMission(_session);

        if (_session != null)
        {
            _settings.DropCover();
            _session.QueueFree();
            _session = null;
        }

        _net.Close();
        // The menu is not a session start. A cover left over from one (a mission exited inside its
        // own fade) has nothing left to uncover.
        DropStartCover();
        // Same reason as the build in LaunchSession: a teardown legitimately stalls the loop.
        _instruments.Rearm();
        ShowMenu(destination);
    }
}

/// <summary>What a session node needs from the launcher: the settled base paths, the persistent
/// rendering nodes, the process-scoped services, and the join flow's state. A snapshot per launch, <see cref="MenuPads"/> is the field that varies.
/// A record, so <c>NetFlight.WithNet</c> writes the network slice into a copy.
/// ⚠ Not a second home for args: a new flag is a <see cref="SessionSpec"/> change, never a
/// context field.</summary>
public sealed record LauncherContext
{
    public required BasePaths Paths { get; init; }
    public required Tooling.ProbeRunner ProbeRunner { get; init; }
    public required Tooling.CaptureDirector CaptureDirector { get; init; }
    public required ulong MasterSeed { get; init; }
    public required Camera3D Camera { get; init; }

    /// <summary>The VR seat's rig (<see cref="XrSession.Rig"/>), null in a flat launch. A session given
    /// one binds its pane to it and draws its lens flare and full-view colour layers in the world.</summary>
    public XrRig? Xr { get; init; }

    public required OrbitCamera Orbit { get; init; }
    public required DirectionalLight3D Sun { get; init; }
    public required Godot.Environment? Env { get; init; }
    /// <summary>Whether this process launched into the menu, Esc from flight returns there
    /// instead of quitting (the flight HUD's exit hint reads it too).</summary>
    public required bool MenuDriven { get; init; }
    /// <summary>Per-player pad binding from the launchscreen's join flow (null = derive from the
    /// connected roster, which is what every CLI launch does).</summary>
    public required int[][]? MenuPads { get; init; }

    /// <summary>The whole network match's seat roster, local panes and remote guests alike, or
    /// null outside a network match. A seat here without <see cref="Net.NetSeat.HasPane"/> gets an
    /// aircraft, a spawn slot, a score row and a marker colour, and no pane.</summary>
    public IReadOnlyList<Net.NetSeat>? NetSeats { get; init; }

    /// <summary>What the host handed this guest at join, or null on a host and outside a match.
    /// Its seed replaces this session's master before anything draws, so every peer's liveries,
    /// spawn walk and dice agree. A session given a <see cref="NetTransport"/> as a guest takes
    /// this off the wire instead, and this field then names nothing.</summary>
    public Net.NetHandshake? NetHandshake { get; init; }

    /// <summary>The carrier this session's <c>NetSession</c> talks to its peers over, or null
    /// outside a network match. The session binds it and steps it once per simulation step. The
    /// caller owns the object and never binds a listener of its own to it.</summary>
    public Net.INetTransport? NetTransport { get; init; }

    /// <summary>Whether this peer owns the match. A host sends its roster and seed to every peer
    /// that joins. A guest is built from what arrives, and waits for it before its world builds.
    /// Meaningless without a <see cref="NetTransport"/>.</summary>
    public bool NetHost { get; init; }

    /// <summary>The airframe order every peer reads a roster's airframe index against, since the
    /// roster carries an index and a seat flies a named node. Empty leaves a guest's seats without
    /// a pick, which falls back to this machine's own launch flags.</summary>
    public IReadOnlyList<string>? NetAirframes { get; init; }

    /// <summary>The fit a seat flown elsewhere carries, by seat index, or null for its stock fit.
    /// Read once the field is known, which on a guest is after the host's roster arrived. A seat
    /// flown here keeps its own menu pick and never asks this.</summary>
    public System.Func<int, Flight.Weapons.LoadoutChoice?>? NetSeatFit { get; init; }

    /// <summary>The custom plane a seat flown elsewhere carries, by seat index, or null for a stock
    /// airframe. Read when the field is known, as <see cref="NetSeatFit"/> is.</summary>
    public System.Func<int, Flight.Hangar.CustomPlaneDef?>? NetSeatBuild { get; init; }

    /// <summary>The campaign wingman's aeroplane as a co-op guest's host named it, null while the
    /// host named none. Set on a co-op guest only, whose director binds the wingman from it.</summary>
    public System.Func<Net.CoopWingmanMessage?>? NetCoopWingman { get; init; }

    /// <summary>The persist log as a co-op guest's host named it. Set on a co-op guest only, whose
    /// director opens its world on it.</summary>
    public System.Func<IReadOnlyCollection<Net.CoopPersistMessage>?>? NetCoopPersisted { get; init; }

    /// <summary>A team Dogfight's team names by lobby team number, as this machine's lobby held
    /// them at the launch. A host reads its own book and a guest the names its host sent. Null
    /// names each team by its number.</summary>
    public IReadOnlyDictionary<int, string>? NetTeamNames { get; init; }

    /// <summary>Leaves the session the way <see cref="MenuDriven"/> says: back to the launchscreen,
    /// or out of the game. The boards' Exit item calls it, so the one routing rule lives on the
    /// Launcher rather than being restated per board.</summary>
    public required System.Action ExitSession { get; init; }

    /// <summary>Frees this session and builds a fresh one from the same settings, behind the load
    /// screen, the mission boards' Restart item (Instant Action and campaign). The mission's
    /// opposition lives in the world, so putting it back means rebuilding the world, which only
    /// the Launcher can do.</summary>
    public required System.Action RestartSession { get; init; }

    /// <summary>The graphics-mode action a seat's controller fires: the Launcher switches the
    /// running world at the top of its next frame and saves the choice. Null leaves the action inert.
    /// </summary>
    public System.Action? ToggleGraphicsMode { get; init; }

    /// <summary>Builds the in-flight Preferences leaf either pause board opens over the held world,
    /// or answers null where the install carries no decoded menu layout for it to compose from. A
    /// factory rather than the node, since the session parents it and only the Launcher holds the
    /// layout, the shared rebinding feature, the menu's audio and the options file's writer.</summary>
    public System.Func<UI.Screens.PausePreferences?>? PauseOptions { get; init; }

    /// <summary>A campaign mission ended, won or lost: the Launcher frees this session a frame later
    /// and shows the menu at the debrief of the named profile's flown mission, carrying the result
    /// intact from <c>MissionEnded</c>. Null when this process was not launched into the menu,
    /// where there is no menu to return to.</summary>
    public System.Action<string, CampaignMissionResult>? CampaignMissionEnded { get; init; }

    /// <summary>Frees the session and shows the menu at the Instant Action wrap-up, carrying the
    /// final numbers the ending left. Null when this process was not launched into the menu, and
    /// unused by a presentation whose own board takes the ending inside the flight.</summary>
    public System.Action<IaWrapupSnapshot>? InstantActionWrapup { get; init; }

    /// <summary>A finished match's way to its lobby's Game Scores: the Launcher leaves the session a
    /// frame later, as <see cref="ExitSession"/> does. Null outside a lobby flight whose host lands
    /// there (<c>NetFlight.LandsOnScores</c>), where a results board takes the ending.</summary>
    public System.Action? VersusLobbyLanding { get; init; }

    /// <summary>The process's music channel, so a mission's own cues reach the one player that
    /// outlives every session. Null when the sound archive or the sound definitions would not
    /// load, which leaves the game silent rather than refusing to launch.</summary>
    public MusicPlayer? Music { get; init; }

    /// <summary>A decode store shared with other builds in this process, or null to decode afresh.
    /// The test harness hands its own in, so a suite's sessions decode a chapter once. A launch
    /// leaves it null, so a session retains nothing of a chapter it has left.</summary>
    public Mech3.DecodeCache? Decode { get; init; }
}
