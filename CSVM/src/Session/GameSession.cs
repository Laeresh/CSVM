using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Testing;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The per-launch session node: loads one aircraft from the player's own
/// extracted game data and renders it with orbit controls — or, with --fly,
/// free flight over the chapter world with arcade controls.
///
/// <see cref="Launcher"/> (Main.tscn's root) owns the bootstrap, the launchscreen and the
/// persistent camera/lighting, and instantiates one of these per launch — the menu's selection
/// and the CLI feed the same StartSession build path — constructed with the launch's settled
/// <see cref="SessionSpec"/> and a <see cref="LauncherContext"/> carrying the persistent
/// references. Esc from a menu-launched flight frees it again (Launcher.ReturnToMenu QueueFrees
/// this node; disposal duties that outlive its child subtree run in <c>_Notification</c> on
/// <c>NotificationExitTree</c>).
/// </summary>
public partial class GameSession : Node3D
{
    private const float HorizonScale = 2.5f;

    /// The fraction of the camera's far plane the scaled skydome may reach. The dome is anchored
    /// on the camera, so its far wall sits at (its own radius x scale) from the eye in every
    /// direction; past the far plane it is CLIPPED and the engine's clear colour shows through the
    /// hole. 0.9 leaves room for the dome's one-frame anchor lag and for a diagonal vertex.
    private const float HorizonFarFraction = 0.9f;

    /// How many near-miss names a failed <c>--node=</c> lookup offers. A chapter holds
    /// thousands of nodes and a substring like "zep" hits dozens; the point is a usable hint, not
    /// a census.
    private const int NodeSuggestCap = 20;

    /// <summary>Smallest orbit radius a synthesized pivot may sit at, so an aim ray that passes
    /// behind the subject still leaves something to orbit rather than spinning about the eye.</summary>
    private const float MinOrbitRadius = 1f;

    /// <summary>Seconds a downed Versus player watches the crash cam before auto-respawning
    /// (R skips early). Respawn is at the player's own spawn point, with full HP/ammo and no
    /// invulnerability window.</summary>
    private const float VersusRespawnDelay = 3f;

    private static readonly string[] InstanceShaderParams =
        { "node_bias", "csky_fog_on", "csky_light_fade", Mech3.SceneBuilder.OpacityParam };

    // Everything this launch settled, parsed and resolved once (see SessionSpec) — the command
    // line verbatim, or the launchscreen's pick (SessionSpec.FromMenu). Every consumer below reads
    // it and nothing re-derives a launch setting; the pristine command line stays on the Launcher.
    private readonly SessionSpec _spec;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private readonly int[][]? _menuPads;
    // The --screenshot=/--shots=/--frames= state machine — see
    // src/Testing/CaptureDirector.cs's entry. Process-scoped and owned by the Launcher (which
    // Ticks it); held here for the Pending reads that gate display choices during the build.
    private readonly Testing.CaptureDirector _captureDirector;
    // The master seed every subsystem generator derives from (see Utils.Rng), resolved by the
    // Launcher once per process (pinned runs take the spec's value; everything else draws from
    // the clock) and re-applied here at each session build.
    private readonly ulong _masterSeed;
    // One rig per rendered view: its camera plus the camera-anchored copies only it
    // sees (skydome / cloud deck / whiteout). Exactly one entry in single player,
    // wrapping the main-viewport _camera below — so the 1P render path is unchanged.
    private readonly List<PlayerRig> _rigs = new();

    // Every AI aircraft spawned into this session (M4 A2) — stepped in DriveSimSteps after the
    // player rigs, freed with the world subtree.
    private readonly List<FlightController> _aiPlanes = new();
    // scratch: rig camera positions for the edge extender
    private readonly List<Vector3> _focusPoints = new();
    // Pickable subtrees that live beside the world content rather than under it — the anim lab's
    // parked --plane= prop. Shared by reference with _selection and _nodeLab, which walk it in
    // addition to the world root; populated during the world build after those tools are created.
    private readonly List<Node3D> _selectionExtraRoots = new();
    // The persistent rendering nodes, owned by the Launcher and kept across sessions; this node
    // only configures them. The mesh lab steers the sun and ambient, which is why both ride the
    // context rather than staying local to the Launcher's lighting setup.
    private readonly Camera3D _camera;
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); the
    // Launcher creates it once and seeds --yaw=/--pitch= into its initial angles.
    private readonly OrbitCamera _orbit;
    // launched into the menu → Esc from flight returns there, not quit
    private readonly bool _menuDriven;
    // Base (chapter-independent) paths, settled by the Launcher once per process and handed in via
    // the context; StartSession reads them each build and recomputes the chapter-dependent
    // gamez/texture/mission paths from _spec.Chapter.
    private readonly string _repoRoot;
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game — /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private readonly string _dataRoot;
    private readonly string _planesGamezPath;
    private readonly string _zrdrPath;
    private readonly string _soundsPath;
    private readonly string _interpPath;
    private readonly string _messagesPath;
    // the extracted UI archive (paint patterns)
    private readonly string _rofPath;
    // Process-scoped, owned by the Launcher; the --damage-test/--effects-test/--weapon-test/
    // --destroy= probe wrappers below delegate to it (see src/Testing/ProbeRunner.cs).
    private readonly Testing.ProbeRunner _probeRunner;
    // Tap-vs-hold timing for the "." step key: a tap steps once (handled directly in
    // _UnhandledInput), and holding past the grace period steps every rendered frame — polled
    // here rather than through key-repeat events, since the grace period is measured on wall
    // time regardless of the sim being halted.
    private readonly HoldToRepeat _stepHold = new(initialDelay: 0.3f, repeatInterval: 0f);

    // Resolves each player's livery and spawn point against _spec;
    // see src/Session/LiveryResolver.cs and src/Session/SpawnPicker.cs.
    private LiveryResolver _liveryResolver = null!;
    private SpawnPicker _spawnPicker = null!;
    // --crash[=frame]: fires once, the frame the sim clock first reaches _spec.CrashFrame.
    private bool _crashFired;
    // --debug-scoreboard --vs: fires once, on the first sim step — see DriveSimSteps.
    private bool _versusDebugKillFired;
    private SpectatorCamera? _spectator;
    // The session's shared world selection (--freecam/--anim-lab): the clicked leaf plus its
    // cs_name ancestor ladder, which every inspect tool reads instead of picking for itself.
    private UI.SelectionService? _selection;
    // The node lab (N, --freecam/--anim-lab): tree panel, search, per-node actions and the
    // dependency readout for whatever the selection holds.
    private UI.NodeLab? _nodeLab;
    // The world damage lab (F5, --freecam/--anim-lab): HP slider + kill/reset on the selection's
    // destructible pool — the interactive twin of --damage-test.
    private UI.WorldDamageLab? _worldDamageLab;
    // The aircraft damage lab (F5, --viewer/--fly/--stunt): per-part HP sliders on the parked
    // plane's visuals, or on P1's real PlaneDamage in flight.
    private DamageLab? _damageLab;
    // The effect/crash stage factory — builds the world-effects runtime
    // (lazily, on demand for a plane-less session: --destroy, the damage lab's first kill) and
    // each player's crash runtime. Constructed once per session, same lifetime as _liveryResolver.
    private WorldEffectsFactory _worldEffectsFactory = null!;
    // Loads/applies the flown mission's weather and drives its per-frame rig state
    // — see src/Session/WeatherRig.cs's entry. Constructed once per
    // session (same lifetime as _worldEffectsFactory); null before the first weathered build and
    // nulled by ReturnToMenu so _Process's null guard covers the frame before the deferred free.
    private WeatherRig? _weatherRig;
    // The world state every puffer in this session reads but none of them owns — the wind and the
    // camera position (see Effects/WorldWind.cs). Constructed here rather than on
    // _weatherRig because the emitter factories need it at StartSession, long before the first
    // weathered build exists to write it; WeatherRig.Tick is what fills it in.
    private Effects.EffectAmbience _ambience = new();
    private LensFlareRig? _lensFlareRig;
    // The FBFX_COLOR_FROM_TO full-screen wash — one ramp state painted into every rendered view.
    private UI.ScreenFlash? _screenFlash;
    private Node3D? _plane;
    // The session's simulation clock (see GameClock). Also published as GameClock.Current, which
    // is how the sim consumers scattered through the tree reach it; dropped by ReturnToMenu.
    private GameClock? _clock;
    // The physics-stepped consumers this node drives itself when the clock is not realtime, in
    // the tree order Godot's physics tick would have used. Dropped by ReturnToMenu.
    private ProjectilePool? _projectiles;
    private IncomingFire? _incomingFire;   // --incoming: the near-miss test rig
    // The AI actor seam (M4 A2): the spawner is built with the rigs; every AI aircraft it has
    // spawned is stepped in DriveSimSteps after the player rigs and freed with the world.
    private AiAircraftSpawner? _aiSpawner;
    // The egen enemy generators (M4 B6, --generators): loaded with the rigs, stepped in
    // DriveSimSteps before the AI planes it spawns into _aiPlanes, freed with the world subtree.
    private AiGeneratorRuntime? _generators;
    // The dogfight scorekeeping (--vs): built with the rigs, fed their Downed reports, its clock
    // advanced on the sim dt (never wall time). Null outside Versus — the Downed events then
    // simply have no subscriber. Freed with this node; flight holds no match state.
    private VersusMatch? _versus;
    // rolling mirrored-tile window past the map edge
    private Mech3.MapEdgeExtender? _edgeExtender;
    // the splitscreen pane rig (null in single player)
    private UI.SplitScreen? _split;

    // Session lifecycle (the launchscreen's in-process world rebuild): everything a
    // session builds hangs under _worldRoot, so Esc-to-menu can free it and a new session node
    // build again. The camera, lights and global shader params live on the Launcher and persist.
    private Node3D? _worldRoot;
    // the session's LIGHT_STATE point lights (see WorldLights)
    private WorldLights? _worldLights;
    // The session-owned texture archive, kept open past the build scope so the data-driven crash can
    // bake its effect puffers lazily at crash time (the same reason --anim-lab keeps it open, but that
    // path hands it to the AnimLab node instead). Disposed by ReturnToMenu on teardown so a map reload
    // drops the previous archive instead of leaking it. Null in the anim-lab (lab-owned) case.
    private TextureArchive? _sessionTextures;
    // The world whose origin-parked entities are still being watched, and the poll accumulator.
    // See WorldBuilder.HideUnplacedEntities / RestorePlacedEntities.
    private WorldBuilder? _unplacedWatch;
    private double _unplacedRecheck;
    // This session's startup timing — the always-on [perf] startup line. One per StartSession,
    // published as StartupProfile.Current so the shared build code can record into it, and cleared
    // when the line is emitted.
    private StartupProfile? _startup;

    /// <summary>Constructs the session node for one launch. <paramref name="spec"/> is what this
    /// session is built from (the command line verbatim, or the launchscreen's pick);
    /// <paramref name="ctx"/> carries the Launcher's settled paths, the persistent rendering
    /// nodes, the process-scoped services and the join flow's pad binding. The caller adds the
    /// node to the tree and then runs <see cref="StartSession"/>.</summary>
    public GameSession(SessionSpec spec, LauncherContext ctx)
    {
        // The session clock is advanced at the top of this node's _Process, and every sim consumer
        // reads it during the same frame — so this node has to tick first. Godot runs the lowest
        // priority first (the Launcher sits one notch behind at -999).
        ProcessPriority = -1000;
        Name = "GameSession";
        _spec = spec;
        _repoRoot = ctx.RepoRoot;
        _dataRoot = ctx.DataRoot;
        _planesGamezPath = ctx.PlanesGamezPath;
        _zrdrPath = ctx.ZrdrPath;
        _soundsPath = ctx.SoundsPath;
        _interpPath = ctx.InterpPath;
        _messagesPath = ctx.MessagesPath;
        _rofPath = ctx.RofPath;
        _probeRunner = ctx.ProbeRunner;
        _captureDirector = ctx.CaptureDirector;
        _masterSeed = ctx.MasterSeed;
        _camera = ctx.Camera;
        _orbit = ctx.Orbit;
        _sun = ctx.Sun;
        _env = ctx.Env;
        _menuDriven = ctx.MenuDriven;
        _menuPads = ctx.MenuPads;
    }

    /// <summary>Whether the build completed — the Launcher's Esc routing reads it (return to the
    /// launchscreen only once a world is actually up).</summary>
    public bool InSession { get; private set; }

    /// <summary>The session's per-player rigs — the Launcher's F11 placement print reads them.</summary>
    internal List<PlayerRig> Rigs => _rigs;

    /// <summary>The session's subject plane (null until the build lands one) — the Launcher's
    /// capture tick reads it, because CaptureDirector only shoots once a plane exists.</summary>
    internal Node3D? Plane => _plane;

    /// <summary>Whether this session builds the world's colliders — the one definition every
    /// consumer reads, resolved on the spec so the labs and the C overlay cannot spell it
    /// differently from what <see cref="Mech3.WorldSession"/> actually built.</summary>
    private bool BuildsCollision => _spec.BuildsCollision;

    /// <summary>Whether P / <c>.</c> may halt this session. Splitscreen flight says no: the freeze
    /// halts the shared world, so it is not one player's to press (the same rule
    /// <see cref="FlightController.AllowPause"/> applies to the in-flight binding).</summary>
    private bool HaltAllowed => !_spec.Fly || _rigs.Count == 1;

    /// <summary>Spawns an AI-piloted aircraft into this session at runtime — the M4 actor seam.
    /// Works any time after the flight build (the <c>--ai=</c> flag calls it there; generators
    /// and the mission script call it mid-session): the plane joins the shared world, ticks on
    /// the session clock like every rig, is hittable/damageable through the shared pool, and
    /// reports its death through <c>Downed</c>. Null when this session built no flight rigs
    /// (viewer/freecam/anim-lab have no spawner).</summary>
    public FlightController? SpawnAiAircraft(string planeName, Vector3 pos, Vector3 lookAt,
        AiPilot pilot)
    {
        if (_aiSpawner == null)
        {
            GD.PushWarning($"ai: no spawner in this session mode — '{planeName}' not spawned");
            return null;
        }
        var ai = _aiSpawner.Spawn(planeName, pos, lookAt, pilot);
        _aiPlanes.Add(ai);
        ai.Downed += (victim, killer) =>
            GD.Print($"ai: {ai.Name} downed (shooter id {victim}, killer {killer?.ToString() ?? "none"})");
        return ai;
    }

    /// <summary>Builds one flight/view session from the spec (mode, chapter, plane, spawn, …)
    /// into a fresh <see cref="_worldRoot"/> so Esc-to-menu can tear it all down and a new session
    /// node build again — the launchscreen's in-process world rebuild. The camera, lights and
    /// global shader params live on the Launcher and persist across sessions. Called by the
    /// Launcher once this node is in the tree. Returns true on success; false (leaving the partial
    /// _worldRoot for the caller to free) when the build threw.</summary>
    public bool StartSession()
    {
        // The startup timing line, opened before anything is built and closed when the session's
        // first frame is on screen. Published as the ambient Current so WorldSession — which the
        // test harness also drives, with no session around it — can record its phases blind.
        _startup = new StartupProfile(_spec.ModeName, Time.GetTicksMsec())
        {
            Subject = _spec.EmptyStage ? "stage=empty"
                : _spec.NodeName != null ? $"chapter={_spec.Chapter} node={_spec.NodeName}"
                : _spec.WorldMode ? $"chapter={_spec.Chapter}"
                : $"plane={_spec.PlaneName}",
        };
        StartupProfile.Current = _startup;
        _worldRoot = new Node3D { Name = "Session" };
        AddChild(_worldRoot);
        // Built fresh per session: a launchscreen relaunch replaces _spec wholesale (FromMenu),
        // so these must not be cached across a rebuild.
        _liveryResolver = new LiveryResolver(_spec, _rofPath);
        _spawnPicker = new SpawnPicker(_spec);
        _ambience = new Effects.EffectAmbience();
        _worldEffectsFactory = new WorldEffectsFactory(_spec, _worldRoot,
            () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam ? cam.GlobalPosition : Vector3.Zero,
            _ambience);
        // Re-derive every subsystem RNG from the master before anything in the session draws, so a
        // rebuild (Esc to the launchscreen and back) repeats the run rather than continuing it.
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        // One simulation clock per session. --det pins it to a fixed step in every mode; the
        // animation lab is fixed-dt by nature (an accumulator interactively, one step per rendered
        // frame when scripted); everything else runs at the wall delta, which is arithmetically
        // what each consumer used before the clock existed.
        _clock = new GameClock
        {
            Mode = _spec.Det || (_spec.AnimLab && _captureDirector.Pending) ? GameClock.RunMode.FixedStep
                : _spec.AnimLab ? GameClock.RunMode.FixedAccum
                : GameClock.RunMode.Realtime,
        };
        GameClock.Current = _clock;
        _camera.Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? 62 : 50;
        // One rig per rendered view, before anything camera-anchored is built (the skydome and
        // weather visuals below are per-rig). Single player reuses the main-viewport camera.
        BuildRigs(_spec.Fly ? _spec.Players : 1);
        // The FBFX_COLOR_FROM_TO wash: one ramp state, one overlay per rendered view, built
        // as soon as the rigs exist so every runtime below can be handed the same sink. Screen-space
        // and session-scoped on purpose — an AnimRuntime is world-scoped and is instanced per effect
        // pool and per crash rig.
        _screenFlash = UI.ScreenFlash.Build(_rigs.Select(r => r.HudParent));
        _worldRoot!.AddChild(_screenFlash);
        _worldEffectsFactory.ScreenFlash = _screenFlash.Play;

        // The build body reads the base paths as plain locals (unchanged from when this was inline
        // in _Ready); the chapter-dependent paths are recomputed here so a new launchscreen chapter
        // selection takes effect on rebuild. Carried on BuildState so the phase methods below share
        // them without re-deriving anything.
        var state = new BuildState
        {
            DataRoot = _dataRoot,
            ZrdrPath = _zrdrPath,
            SoundsPath = _soundsPath,
            InterpPath = _interpPath,
            MessagesPath = _messagesPath,
            PlanesGamezPath = _planesGamezPath,
            Mute = _spec.Mute,
            DebugCollision = _spec.DebugCollision,
        };
        state.TexturesPath = _spec.Textures ?? SessionPaths.ChapterTextures(_dataRoot, _spec.Chapter);
        state.GamezPath = _spec.Gamez
            ?? (_spec.WorldMode ? SessionPaths.ChapterGamez(_dataRoot, _spec.Chapter) : _planesGamezPath);
        state.MissionZrdrPath = SessionPaths.MissionZrdr(_dataRoot, _spec.Chapter, _spec.Mission);

        Stopwatch sw;
        try
        {
            sw = Stopwatch.StartNew();
            LoadArchives(state);
            // The SOUND archive is scoped to this build everywhere but the lab, where the AnimLab
            // node owns its disposal so effects can build at any playhead time; until that node
            // exists a failed lab build must close it from the catch below. The TEXTURE archive is
            // not here at all — LoadArchives gives it to the session in every mode, which is what
            // lets the world runtime keep baking puffers all session.
            using var soundsScope = _spec.AnimLab ? null : state.Sounds;
            if (!ResolveNodeSubtree(state))
                return false;
            if (_spec.EmptyStage)
            {
                BuildEmptyStage(state);
            }
            else if (_spec.WorldMode)
            {
                if (!BuildWorldStage(state))
                    return false;
            }
            else
            {
                BuildStaticStage(state);
            }
            if (!AttachPlaneAndLabs(state))
                return false;
            AssignCloudDeckIfBuilt(state);
            BuildFreecamSpectator(state);
            if (_spec.Fly)
            {
                BuildFlightRigs(state);
            }
            ApplyDestroyOverride(state);
            LogBuildSummary(state, sw);
        }
        catch (Exception e)
        {
            GD.PrintErr($"failed to load session: {e}");
            // A failed build: nothing yet owns the archives kept open past the build scope (the
            // AnimLab node in the lab case, ReturnToMenu's teardown otherwise), so dispose them here.
            if (state.AnimLabNode == null)
            {
                state.LabTextures?.Dispose();
                state.LabSounds?.Dispose();
            }
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            if (_captureDirector.Pending)
            {
                GetTree().Quit(1);
            }
            return false;
        }

        FinishFraming(state);
        _startup?.EndBuild();
        InSession = true;
        return true;
    }

    public override void _Notification(int what)
    {
        // The focus mute lives on the Launcher (it is process state, not session state). What is
        // left here is the session's teardown: every duty that QueueFree does NOT cover on its own.
        // Return-to-menu is now a bare QueueFree (Launcher.ReturnToMenu) — the whole session subtree
        // (world, plane, HUD, rigs, effects) hangs under _worldRoot, a child of this node, so it
        // frees atomically with us and needs no manual null-out. Only the non-child duties run here.
        if (what == (int)NotificationExitTree)
        {
            // A run that quits inside the session build (the headless probes) never renders a
            // frame, so this is the only place its startup breakdown can still be reported.
            // Idempotent: a session that did render has already emitted and this does nothing.
            _startup?.Emit();
            // The published clock is a static pointer, not a child: null it so any node that
            // outlives this teardown falls back to its raw frame delta (GameClock.Current == null).
            // A later session sets it again in StartSession; the menu-relaunch happens in a frame
            // after this node has exited, so the two never race.
            GameClock.Current = null;
            // Clears csky_light_count so the next world does not inherit this one's light spill;
            // idempotent, and null-guarded (a failed build never set it).
            _worldLights?.Dispose();
            _worldLights = null;
            // The session-owned texture archive, kept open past its build scope so the data-driven
            // crash could bake puffers lazily. Not a node, so QueueFree cannot reach it; dispose it
            // here. Null-guarded — a failed build disposed and nulled it already, so no double free.
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            // Restore the persistent (Launcher-owned) main camera: splitscreen stood it down while
            // the panes rendered, and the launchscreen and the next session expect it current.
            _camera.Current = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // P halts the sim, . steps it one frame (hold past a grace period to keep stepping every
        // rendered frame — see _stepHold and the poll in _Process) — in freecam, the static
        // viewer and the launchscreen. Flight polls P itself (FlightController, so gamepad Start
        // keeps working) and the animation lab owns its own transport, so neither is handled here.
        if (!_spec.AnimLab && @event is InputEventKey { Pressed: true, Echo: false } clockKey
            && _clock != null && HaltAllowed)
        {
            if (clockKey.Keycode == Key.P && !_spec.Fly)
            {
                _clock.Halted = !_clock.Halted;
                GD.Print(_clock.Halted ? "clock: halted (P resumes, . steps one frame, hold . to run)" : "clock: running");
                return;
            }
            if (clockKey.Keycode == Key.Period)
            {
                _clock.Halted = true;
                _clock.StepOnce();
                _stepHold.Press();
                return;
            }
        }
        if (!_spec.AnimLab && @event is InputEventKey { Pressed: false } clockKeyUp
            && clockKeyUp.Keycode == Key.Period)
        {
            _stepHold.Release();
        }
        // Esc (menu-or-quit routing) and the F11/F12 capture keys are the Launcher's: they are
        // meaningful at the launchscreen too, where no session node exists.
        if (_spec.Fly || _spec.Freecam || _spec.AnimLab)
            return; // the FlightController / SpectatorCamera owns the camera; no orbit controls
        _orbit.HandleInput(@event);
    }

    public override void _Process(double delta)
    {
        // First thing in the frame (ProcessPriority): decide how much sim time this rendered frame
        // is worth, then — when the clock is not realtime — step the physics-driven consumers
        // ourselves, in the tree order Godot's physics tick would have used.
        if (_clock is { } clock)
        {
            if (_stepHold.Tick((float)delta))
            {
                clock.StepOnce();
            }
            clock.BeginFrame(delta);
            if (clock.ParentDriven)
            {
                DriveSimSteps(clock);
            }
        }
        // The startup line goes out on the frame that proves the first one was drawn.
        _startup?.Frame();
        // The shader clock (ShaderTime.Advance), the --perf report and the capture pipeline tick
        // on the Launcher, one priority notch behind this node — the same point in the frame they
        // ran at when both lived on one root, and alive at the launchscreen where this node is not.
        //
        // The poll below stays on wall time: it is an instrument, and an instrument that freezes
        // with the thing it measures reports nothing.
        // Entities switched off as unplaced (see WorldBuilder.HideUnplacedEntities) that have
        // since been moved off the world origin are put back: a motion starting is the proof
        // that a definition owns them. Polled rather than deferred once, because an OnCall
        // definition can start its motion at any time. Goes quiet for good once the list drains.
        if (_unplacedWatch != null)
        {
            _unplacedRecheck += delta;
            if (_unplacedRecheck >= 1.0)
            {
                _unplacedRecheck = 0.0;
                if (_unplacedWatch.RestorePlacedEntities() is { Count: > 0 } restored)
                {
                    GD.Print($"world: {restored.Count} entit(y/ies) moved off the origin after all, "
                             + "restored: " + string.Join(", ", restored));
                }
            }
        }
        // Everything below is anchored to *a* camera, so it runs once per rig — one in single
        // player, one per pane in splitscreen (each on that player's own visual layer). See
        // src/Session/WeatherRig.cs's Tick.
        _weatherRig?.Tick(_rigs);
        // After the weather tick: that is where each rig's dome is re-centred on its camera, and
        // the flare reads the sun node inside it.
        _lensFlareRig?.Tick(delta);

        // Map-edge continuation: re-center the mirrored-tile window on the cameras. One window
        // serves every pane (the union of the rings around each player), so two players at
        // opposite edges both get continued terrain. Cheap no-op until one of them crosses a cell
        // boundary (1024 m), then ~a window row rebuilds.
        if (_edgeExtender != null)
        {
            _focusPoints.Clear();
            foreach (var rig in _rigs)
                _focusPoints.Add(rig.Camera.Position);
            _edgeExtender.Update(_focusPoints);
        }
    }

    /// <summary>The match clock on a realtime session: like every physics-stepped consumer,
    /// advance on Godot's tick unless the clock is parent-driven — then
    /// <see cref="DriveSimSteps"/> advances the match itself, on the same dt as the rigs.</summary>
    public override void _PhysicsProcess(double delta)
    {
        float dt = _clock?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
            return;
        _versus?.Advance(dt);
    }

    private static void CopyInstanceShaderParams(Node source, Node copy)
    {
        if (source is GeometryInstance3D from && copy is GeometryInstance3D to)
            foreach (var name in InstanceShaderParams)
            {
                var value = from.GetInstanceShaderParameter(name);
                if (value.VariantType != Variant.Type.Nil)
                    to.SetInstanceShaderParameter(name, value);
            }
        int n = Math.Min(source.GetChildCount(), copy.GetChildCount());
        for (int i = 0; i < n; i++)
            CopyInstanceShaderParams(source.GetChild(i), copy.GetChild(i));
    }

    /// <summary>Loads the session's core archives — gamez, textures, sounds, sound defs/groups —
    /// and routes the texture/sound archives to whichever owner outlives this build scope (the
    /// anim lab node, or the session for teardown).</summary>
    private void LoadArchives(BuildState state)
    {
        // Opened before the world build rather than with the flight audio below, because the
        // animation bootstrap builds the world's ambient SOUND_NODE emitters (the waterfall,
        // the train, the sirens) and needs the archive while it runs. Same lifetime rule as
        // the puffer factory: the decoded streams outlive this scope, the zip handle does not.
        var archives = SessionArchives.OpenFor(
            _spec.AnimLab ? ArchiveIntent.Lab : ArchiveIntent.Session,
            state.GamezPath, state.TexturesPath, state.SoundsPath, state.ZrdrPath, state.Mute);
        state.Gamez = archives.Gamez;
        state.Textures = archives.Textures;
        state.Sounds = archives.Sounds;
        state.SoundDefs = archives.SoundDefs;
        state.SoundGroups = archives.SoundGroups;
        state.TexturesOutliveBuild = archives.TexturesOutliveBuild;
        state.SoundsOutliveBuild = archives.SoundsOutliveBuild;
        // The texture archive stays open past this build scope: the data-driven crash bakes its
        // effect puffers lazily at crash time (the same reason --anim-lab keeps it open). The lab
        // owns its copy (LabTextures, freed with the lab node); every other mode hands it to the
        // session (_sessionTextures), which ReturnToMenu disposes on teardown so a map reload drops
        // the previous archive instead of leaking it. A failed build disposes it from the catch.
        if (_spec.AnimLab)
        {
            state.LabTextures = archives.Textures;
            state.LabSounds = archives.Sounds;
        }
        else
        {
            _sessionTextures = archives.Textures;
        }
    }

    /// <summary>--node=&lt;cs_name&gt;: resolve the request against the chapter gamez BEFORE
    /// anything is built, so a miss reports its candidates and quits instead of half-building a
    /// world. Matching is on the source name, never the Godot node name (which is sanitized and
    /// auto-renamed); duplicates are normal, so the whole match list is logged and the first is
    /// what builds. Returns false if the session must abort (the tree quit is already issued).</summary>
    private bool ResolveNodeSubtree(BuildState state)
    {
        if (_spec.NodeName == null || !_spec.WorldMode)
            return true;
        var matches = WorldBuilder.MatchNodes(state.Gamez, _spec.NodeName);
        if (matches.Count == 0)
        {
            var near = WorldBuilder.SuggestNodes(state.Gamez, _spec.NodeName, NodeSuggestCap);
            Log.Warn("world", $"--node='{_spec.NodeName}' matches no node in {_spec.Chapter}'s gamez ({state.Gamez.Nodes.Count} nodes)");
            if (near.Count > 0)
            {
                Log.Warn("world", $"--node= candidates containing '{_spec.NodeName}': {string.Join(", ", near)}");
            }
            else
            {
                Log.Warn("world", $"--node= no name in {_spec.Chapter} contains '{_spec.NodeName}' either — run the full world with --debug-names to read names off the objects");
            }
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            GetTree().Quit();
            return false;
        }
        state.NodeSubtree = matches[0];
        var labels = new List<string>();
        foreach (var m in matches)
        {
            labels.Add($"{m.Name}#{m.Index}");
        }
        Log.Info("world", $"--node='{_spec.NodeName}' matched {matches.Count} node(s): {string.Join(", ", labels)}");
        if (matches.Count > 1)
        {
            Log.Warn("world", $"--node='{_spec.NodeName}' is ambiguous — building the first ({state.NodeSubtree.Name}#{state.NodeSubtree.Index}); name a unique node or pick by eye from the list above");
        }
        return true;
    }

    /// <summary>--stage=empty: no gamez, no mission, no animation program — a flat collidable
    /// ground plane under a grid drawn in code. Flight, weapons and colliders work; nothing else
    /// is built.</summary>
    private void BuildEmptyStage(BuildState state)
    {
        long mark = StartupProfile.Mark();
        var stage = EmptyStage.Build(collision: _spec.Fly || _spec.ForceCollision);
        StartupProfile.Record("world", mark);
        _plane = stage.Root;
        state.MeshInstances = stage.MeshInstanceCount;
        state.Colliders = stage.ColliderCount;
        state.What = "empty stage";
    }

    /// <summary>Builds the chapter world (world1) and binds its animation program, then the
    /// per-view steps that read it back: node-name framing, the damage/effects-test probes, the
    /// shared world selection + node/damage labs, unplaced-entity hiding, the map-edge extender,
    /// per-rig weather, and (--anim-lab) the animation debugger stage. Returns false if the
    /// session must abort (a probe quit is already issued).</summary>
    private bool BuildWorldStage(BuildState state)
    {
        // Build the world (world1) and bind its animation program: WorldBuilder, clutter,
        // mission setup, AnimProgram + the AnimRuntime collaborator wiring, bind, sound
        // prewarm — extracted to WorldSession so --anim-lab builds the same
        // world+runtime. The per-view steps below (unplaced watch, edge extender, per-rig
        // horizon + weather) stay here and read the returned builder. WorldSession does NOT
        // add its Root to the tree — _worldRoot.AddChild(_plane) below still owns that.
        var session = WorldSession.Build(
            new WorldSession.Options
            {
                DataRoot = state.DataRoot,
                Chapter = _spec.Chapter,
                Mission = _spec.Mission,
                ZrdrPath = state.ZrdrPath,
                InterpPath = state.InterpPath,
                MissionZrdrPath = state.MissionZrdrPath,
                EffectsParent = _worldRoot!,
                // PLAYER_RANGE conditions + the sound listener measure from player 1's
                // camera — the honest answer in every mode (chase cam, free camera, orbit
                // eye); resolved per call because none of those cameras exist yet here.
                PlayerPosition = () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam
                    ? cam.GlobalPosition
                    : Vector3.Zero,
                // The EXECUTION_BY_RANGE gate measures from the aircraft themselves (every
                // player, nearest wins) — the chase camera trails far enough behind the plane
                // to eat most of a 50 m radius. Camera fallback for the plane-less modes.
                PlayerPositions = () =>
                {
                    if (_rigs.Count == 0)
                        return _camera is { } cam ? new[] { cam.GlobalPosition } : System.Array.Empty<Vector3>();
                    var positions = new Vector3[_rigs.Count];
                    for (int i = 0; i < _rigs.Count; i++)
                        positions[i] = _rigs[i].Controller is { } fc
                            ? fc.GlobalPosition
                            : _rigs[i].Camera.GlobalPosition;
                    return positions;
                },
                // The damage-test needs the collidable world (its census measures which
                // destructible geometry is solid and whether death removes it) even though it
                // runs in the freecam (non-fly) harness. Two interactive levers switch the
                // same build: --collision, because the collider overlay needs bodies to
                // draw, and --debug-damage, because a kill's collider count would otherwise
                // read zero and lie.
                Collision = BuildsCollision,
                DebugAnim = _spec.DebugAnim,
                AnimLod = _spec.AnimLod,
                DebugDzPaths = _spec.DebugDzPaths,
                NoClutter = _spec.NoClutter,
                DebugClutterFlag = _spec.DebugClutterFlag,
                ClutterTemplates = _spec.ClutterTemplates,
                // The texture archive belongs to the SESSION in every mode (LoadArchives hands it
                // to _sessionTextures, or to the lab node), not to this build scope — so the world
                // runtime keeps a live PufferFactory and a death's fire/trails, or a car's dust,
                // still bakes when it is first reached. The sound archive does NOT: it is
                // a `using` of this build below except in the lab, and the prewarm covers it. Both
                // flags come from LoadArchives's ArchiveIntent, not set here by hand.
                TexturesOutliveBuild = state.TexturesOutliveBuild,
                SoundsOutliveBuild = state.SoundsOutliveBuild,
                // The lab: quiet stage, ambient playback deferred to its A toggle, and its staged
                // templates relocated onto the (in-front-of-camera) call site. The second is stage
                // construction state — safe to set this early, since a quiet-stage bootstrap
                // dispatches only RESET_STATEs, which are instant and never consult it.
                AutoStart = !_spec.AnimLab,
                PlacesCalledTemplates = _spec.AnimLab,
                // The world's dice — RANDOM_WEIGHT verdicts, SOUND_GROUPS picks, crash-debris
                // scatter — in every mode, not just the lab.
                RuntimeSeed = Rng.IntSeedFor(Rng.Anim),
                // --node=: one subtree instead of the whole world (null = the full build).
                NodeSubtree = state.NodeSubtree,
            },
            state.Gamez, state.Textures, state.Sounds, state.SoundDefs, state.SoundGroups);
        _plane = session.Root;
        var builder = session.Builder;
        state.CloudDeck = session.CloudDeck;
        // The deck's other lit variant, built beside it — the weather rig swaps it in above the
        // cloud band (WorldBuilder.CloudDeckUndimmedMeshes).
        state.DeckUndimmedMeshes = builder.CloudDeckUndimmedMeshes;
        // Owned by the session so a teardown drops the previous world's lights.
        _worldLights = session.Lights;
        state.CrashProgram = session.Program;
        state.WorldScene = session.Builder.Scene;
        state.WorldRuntime = session.Runtime;
        // The screen wash. Set here rather than inside WorldSession for the same reason the
        // contact mask below is: the overlay is a session-owned surface and WorldSession builds
        // runtimes for the test harness too, where there is no session to own one.
        session.Runtime.ScreenFlash = _screenFlash != null ? _screenFlash.Play : null;
        // Ground contact for the world's own `do_intersections` bodies — `agyrobus`' root and
        // pieces, the C1B airframes. Set here rather than inside WorldSession because the mask is
        // a flight-layer constant and the runtime is the animation layer; this is where the two
        // legitimately meet. Left at 0 in a collider-less build, which is the whole fallback.
        if (BuildsCollision)
        {
            session.Runtime.ContactMask = CollisionLayers.World;
            // Bound to the ONE surface-id read, so a landing piece, a round's impact and a
            // wingtip graze cannot disagree about what they hit.
            session.Runtime.SurfaceIsWater = body => ProjectilePool.SurfaceIsWater(body as Node);
        }

        // F13 / --debug-ainets: the chapter's AI patrol nets (ne0NNNNN + neindex — AI route
        // data the original never renders; docs/formats/ai-nets.md). Chapter-scoped data, so
        // the --node= partial stage skips it with the rest of the mission dressing.
        if (state.NodeSubtree == null)
        {
            _plane.AddChild(new UI.AiNetsOverlay(
                SessionPaths.ChapterZrdr(state.DataRoot, _spec.Chapter), _spec.Chapter)
            {
                DebugShow = _spec.DebugAiNets != null,
                Filter = _spec.DebugAiNets ?? "",
            });
        }

        // --node=: the built subtree's WORLD-frame box. Computed from the built meshes and
        // the node transforms rather than from GlobalTransform, because the subtree has not
        // joined the scene tree yet — and never from the gamez child_bbox, which is stored
        // in the node's own frame.
        if (state.NodeSubtree != null && WorldBuilder.DetachedWorldAabb(session.Root) is { } box)
        {
            state.NodeAabb = box;
            Log.Info("world", $"node stage: '{state.NodeSubtree.Name}'#{state.NodeSubtree.Index} built, {builder.MeshInstanceCount} mesh instance(s), centre=({box.GetCenter().X:0},{box.GetCenter().Y:0},{box.GetCenter().Z:0}) size=({box.Size.X:0.#},{box.Size.Y:0.#},{box.Size.Z:0.#})");
        }
        else if (state.NodeSubtree != null)
        {
            Log.Warn("world", $"node stage: '{state.NodeSubtree.Name}'#{state.NodeSubtree.Index} built no geometry at all — it is a group node; the camera framing has nothing to aim at");
        }

        // --damage-test: with the world built and its AnimRuntime bound, drive one
        // destructible's HP through its DAMAGE_SEQUENCE stages and quit — the headless stand-in
        // for an interactive HP control. The world subtree is added to the tree (ManualAdvance so
        // _Process doesn't double-drive) because ticking the death sequences forward reads global
        // transforms, which are invalid on an out-of-tree node (`!is_inside_tree()` spam). It also
        // makes the collider positions real.
        if (_spec.DamageTest)
        {
            _worldRoot!.AddChild(_plane);
            session.Runtime.ManualAdvance = true;
            _probeRunner.RunDamageTest(_spec, session.Runtime);
            GetTree().Quit();
            return false;
        }

        // --effects-test: build the world-effects runtime and play every impact/destruction
        // effect through it, reporting which resolve and which actually build a puffer
        // (a started def that renders nothing vs one that does), then quit — the headless
        // verify. Added to the tree (self-ticking) so puffers spawn and the census
        // is real; the world plane subtree is added so the templates' global transforms hold.
        if (_spec.EffectsTest && state.WorldScene != null)
        {
            _worldRoot!.AddChild(_plane);
            if (_worldEffectsFactory.EnsureWorldEffects(state.Gamez, state.WorldScene, state.Textures,
                    session.Program, session.Runtime) is { } effects)
            {
                _probeRunner.RunEffectsTest(_spec, _camera, effects,
                    EffectCatalogue.WorldEffectAnimNames(session.Program),
                    _worldEffectsFactory.EffectStage);
            }
            GetTree().Quit();
            return false;
        }

        // The shared world selection: click-pick plus the cs_name ancestor ladder PgUp/PgDn
        // walks, in the two modes that observe a live world with a cursor. Created here so
        // the anim lab below can bind its camera-follow to it; it joins the tree with the
        // rest of the session, and builds no HUD and no highlight until something is
        // picked, so an unadorned capture is unchanged.
        if (_spec.Freecam || _spec.AnimLab)
        {
            _selection = new UI.SelectionService(_plane, _camera)
            {
                DebugPick = _spec.DebugSelect != null ? UI.SelectionService.ParseDebugPick(_spec.DebugSelect) : null,
                ExtraRoots = _selectionExtraRoots,
            };
            // The node lab reads that selection. Its camera is resolved through a
            // delegate: the freecam is created further down, after this point.
            _nodeLab = new UI.NodeLab(_plane, _selection, session.Runtime, session.Program,
                session.Builder.Scene, BuildsCollision)
            {
                ExtraRoots = _selectionExtraRoots,
                CameraSource = () => _spectator,
                DebugSpec = _spec.DebugNodeLab,
                // The anim lab's timeline strip and transport panel own the bottom of the
                // window; plain freecam has nothing there.
                BottomMargin = _spec.AnimLab ? 252 : 16,
            };
            // The world damage lab reads the same selection. Its effects runtime is built
            // on the first damage action, not now — an untouched session pays nothing.
            var damageScene = session.Builder.Scene;
            var damageProgram = session.Program;
            var damageRuntime = session.Runtime;
            _worldDamageLab = new UI.WorldDamageLab(_selection, damageRuntime, BuildsCollision)
            {
                SelectByName = name => _nodeLab?.SelectByName(name) ?? false,
                EffectsSource = () => _worldEffectsFactory.EnsureWorldEffects(state.Gamez, damageScene, state.Textures,
                    damageProgram, damageRuntime),
                DebugSpec = _spec.DebugDamage,
                BottomMargin = _spec.AnimLab ? 252 : 16,
            };
        }

        // Every mechanism that places or hides a world entity has now run (the mission's
        // interp setup script as bootstrap pass 0, then the ON_STARTUP definitions), so
        // anything still sitting on the world origin is content this mission never placed
        // — the chapter build script parks every vehicle there (see
        // WorldBuilder.HideUnplacedEntities). Retail data leaves a few switched on:
        // C5/IA1's piratezep is the reported "zeppelin buried in the ground".
        // Switched off immediately so none of it is ever seen; _Process then polls
        // RestorePlacedEntities, which puts back anything a time-based motion moves.
        _unplacedWatch = builder;
        if (builder.HideUnplacedEntities() is { Count: > 0 } unplaced)
        {
            GD.Print($"world: {unplaced.Count} unplaced entit(y/ies) left at the origin, "
                     + "switched off: " + string.Join(", ", unplaced));
        }

        // Map-edge continuation: a rolling window of mirrored terrain tiles (WITH the
        // chapter's clutter) that follows the plane past the map boundary, so the world
        // continues indefinitely under the fog like the original's tile-reload grid
        // (see MapEdgeExtender). On in --fly and in static weathered views (--sky-zone,
        // for edge-verification shots); off for plain orbit viewing (honest data view).
        if (_spec.Fly || _spec.Freecam || _spec.SkyZoneExplicit)
        {
            long edgeMark = StartupProfile.Mark();
            // Block depth is per chapter (A/B'd against the original); --map-edge-block= overrides
            // it, which is why the spec keeps it nullable rather than pre-defaulted.
            int block = _spec.MapEdgeBlock ?? Mech3.MapEdgeExtender.DefaultBlockCells(_spec.Chapter);
            _edgeExtender = builder.CreateEdgeExtender(session.Clutter, block, _spec.MapEdgeRepeat,
                census: _spec.DumpTileGrid);
            StartupProfile.Record("edge", edgeMark);
            if (_edgeExtender != null)
            {
                _plane.AddChild(_edgeExtender);
                GD.Print($"map edge: rolling tile window active — block {_edgeExtender.BlockCells} cell(s), "
                    + (_edgeExtender.RepeatInsteadOfMirror ? "repeat" : "mirror (NOT what the original does)"));
            }

            // --dump-tilegrid: the census is complete the moment the extender exists (it is built
            // in ScanTiles), so the report is written here and the session ends — no window is
            // ever needed, and nothing later in the build can change what was scanned.
            if (_spec.DumpTileGrid)
            {
                string? report = _edgeExtender?.WriteCensus(_spec.Chapter);
                if (report == null)
                {
                    GD.PrintErr($"--dump-tilegrid: {_spec.Chapter} builds no map-edge continuation "
                        + "(no area/partition grid, or no recognizable ground tiles at all).");
                    GetTree().Quit(1);
                    return false;
                }
                string name = _spec.DumpTileGridPath.Length > 0
                    ? _spec.DumpTileGridPath
                    : $"tilegrid_{_spec.Chapter}.json";
                _probeRunner.WriteScratch(name, report);
                // WriteScratch resolves a relative name under ./.scratch/ and passes an absolute
                // one straight through, so the flag takes either.
                GD.Print($"--dump-tilegrid: {_spec.Chapter} census → {name}");
                GetTree().Quit();
                return false;
            }
        }
        if (_spec.Fly || _spec.Freecam || _spec.SkyZoneExplicit)
        {
            long weatherMark = StartupProfile.Mark();
            // The ambient cloud field: the chapter's own fogvol.zrd clutter scattered through the
            // fvol* boxes its gamez authors. World-anchored authored geometry, so it is
            // built once beside the world rather than per rig, and needs no per-frame driving —
            // unlike the dome/deck/whiteout below, which follow a camera. Gated with them because
            // it is atmosphere: a plain --viewer inspection shows the data, not the sky, and it
            // shares their `weather` startup phase ("skydome + fog + cloud visuals").
            var fogVolumes = Mech3.FogVolumeSpec.VolumesOf(state.Gamez);
            var fogVolumeSpec = Mech3.FogVolumeSpec.Load(SessionPaths.ChapterZrdr(_dataRoot, _spec.Chapter));
            var cloudField = Effects.FogVolumeClutter.Create(state.Gamez, state.Textures,
                fogVolumeSpec, fogVolumes);
            if (cloudField != null)
            {
                _worldRoot!.AddChild(cloudField);
                GD.Print($"fogvol clouds: {cloudField.InstanceCount} sprites "
                         + $"({cloudField.BaseCount} base + {cloudField.ExtensionCount} "
                         + $"map-edge extension) over {fogVolumes.Count} volume(s) — "
                         + $"{cloudField.Summary}");
            }
            // The fvol sprite field onto the zone layer its own VOLUMES author: the field is
            // scattered through them, so it is gated with them. Done HERE rather than in
            // FogVolumeClutter because the field is one MultiMesh per sprite kind spanning every
            // volume, so it cannot carry a per-volume layer — one uniform zone per chapter is the
            // only thing this shape can express, and WorldBuilder.FogVolumeZoneIdOf refuses to
            // guess when the volumes disagree.
            //
            // ⚠ Read from the data, never assumed: C1/C1C/C4 author 2 and C5 authors 1, but
            // **C2B authors −1** — LayerFor returns 0 there, the field stays on the default layer,
            // and it renders below C2B's deck. That divergence is authored, not a bug
            // (docs/formats/weather.md).
            //
            // The world's own placed `cloudparent` clusters need nothing here at all any more:
            // they are ordinary world nodes and SceneBuilder already stamped them with their own
            // zone_id during the walk.
            int fvolZone = Mech3.WorldBuilder.FogVolumeZoneIdOf(state.Gamez);
            if (cloudField != null && Mech3.ZoneGate.LayerFor(fvolZone) is var fvolLayer and not 0)
            {
                UI.SplitScreen.SetVisualLayer(cloudField, fvolLayer);
            }

            // The sun goes in with the weather: its bearing is the zone's own
            // SUNLIGHT_ORIENTATION, applied by the same zone-apply that writes the fog. The
            // ambience rides along as the wind seam — same reason, different authored block.
            _weatherRig = new WeatherRig(_spec, _worldRoot!, _sun, _ambience);
            // The deck's own zone_id, the one gated population that cannot ride a visual layer (it
            // is a per-rig camera-anchored copy — see WeatherRig.SetDeckZoneId).
            _weatherRig.SetDeckZoneId(builder.CloudDeckZoneId);
            // The deck tiles' own authored altitude (C1/C1C/C2B 960, C4 1050), read off the built
            // data rather than hardcoded — Tick leaves the deck here rather than pinning it to the
            // CLOUD_COVER band centre.
            _weatherRig.SetDeckAltitude(builder.CloudDeckAltitude);
            // Hand the rig the chapter's own fog-volume census and its parsed fogvol.zrd —
            // the same pair the cloud field above was built from, handed to a second consumer
            // rather than re-loaded. Tick resolves each camera's weather state (1/2/3) from it and,
            // where fog_zone is armed (C5 alone), its in-volume whiteout too.
            _weatherRig.SetFogVolumes(fogVolumes, fogVolumeSpec);
            // The horizon's zone children go in with the mission's weather: the zone the fog and
            // the dome share is picked from both (three chapters ship an empty zone2).
            _weatherRig.Build(state.MissionZrdrPath, _rigs, builder.HorizonZones(),
                activeZone =>
            {
                // One dome per horizon zone the gate can tell apart, not just the flown one —
                // below the cloud deck the camera is in state 1 and `horizon/zone1` IS the sky and
                // the ceiling. The zones and their order are the data's (`DomeZonesToBuild`); the
                // per-rig CONTAINER is what `WeatherRig.Tick` anchors to the camera, so each dome
                // keeps its own scale and its own gate.
                var zones = builder.HorizonZones();
                var domeZones = Mech3.WorldBuilder.DomeZonesToBuild(zones, activeZone);
                foreach (var rig in _rigs)
                {
                    var anchor = new Node3D { Name = "horizon" };
                    foreach (string zoneName in domeZones)
                    {
                        var dome = builder.BuildHorizon(zoneName);
                        if (dome == null)
                            continue;
                        dome.Name = $"dome_{zoneName}";
                        // Per dome, never once for the container: a chapter's two zone domes are
                        // different sizes, and the flown one's scale must not move because a
                        // second one was added beside it (C1B is the clamp canary — see
                        // HorizonScaleFor).
                        dome.Scale = Vector3.One * HorizonScaleFor(dome);
                        anchor.AddChild(dome);
                        int zoneId = -1;
                        foreach (var z in zones)
                            if (z.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase))
                                zoneId = z.ZoneId;
                        rig.HorizonDomes.Add(new HorizonDome(dome, zoneId));
                    }
                    if (anchor.GetChildCount() == 0)
                    {
                        anchor.QueueFree();
                        break;
                    }
                    if (rig.VisualLayer != 0)
                        UI.SplitScreen.SetVisualLayer(anchor, rig.VisualLayer);
                    _worldRoot!.AddChild(anchor);
                    rig.Horizon = anchor;
                }

                // The one-line evidence that the swap exists at all: a gate that stopped building
                // the second dome and a chapter whose data supports only one render identically at
                // the state they share (docs/verification.md, "an unchanged number is not
                // evidence"). Names the zone AND the zone_id it will be gated on.
                if (_rigs.Count > 0)
                {
                    var built = _rigs[0].HorizonDomes;
                    var parts = new List<string>();
                    foreach (var d in built)
                        parts.Add($"{d.Node.Name} (zone_id {d.ZoneId})");
                    GD.Print($"horizon: {built.Count} dome(s) per rig — {string.Join(", ", parts)}"
                             + (built.Count > 1 ? "; shown by camera weather state" : ""));
                }
            });
            StartupProfile.Record("weather", weatherMark);
        }

        // The sun's lens flare. After the weather build, because it needs each rig's
        // dome copy to exist — the `sun` node it anchors to is a child of the horizon subtree,
        // and finding it is one of the two gates. Safe to call unconditionally: the chapter data
        // decides, and six of the eight chapters author nothing here.
        _lensFlareRig = new LensFlareRig(_spec);
        _lensFlareRig.Build(_rigs, state.Textures, _interpPath, _spec.Chapter);
        state.MeshInstances = builder.MeshInstanceCount;
        state.Colliders = builder.ColliderCount;
        // Read after the domes, since C1's daytime sky layer is a horizon child.
        if (builder.ScrollingModelCount > 0)
            GD.Print($"texture scroll: {builder.ScrollingModelCount} model(s) animating UVs");
        // The evidence that the authored render flags reached the materials — a night chapter
        // reporting 0 self-lit models means they did not.
        GD.Print($"model flags: {builder.UnlitModelCount} self-lit (lighting: false), "
                 + $"{builder.UnfoggedModelCount} unfogged (fog: false)");
        // The per-polygon second material pass. A declined count above zero means a
        // sprite/facade mesh carried one and it was dropped — never observed in this install.
        if (builder.OverlayPassSurfaceCount > 0 || builder.OverlayPassDeclinedCount > 0)
            GD.Print($"overlay passes: {builder.OverlayPassSurfaceCount} surface(s) built, "
                     + $"{builder.OverlayPassDeclinedCount} polygon(s) declined");
        state.What = $"chapter {_spec.Chapter} world";

        // The animation debugger (--anim-lab): the lab node owns the clock and the
        // transport; the world above is its quiet stage (AutoStart=false — reset states
        // and mission setup applied, nothing playing until A or --play-anim).
        if (_spec.AnimLab)
        {
            BuildAnimLabStage(state, session);
        }
        return true;
    }

    /// <summary>The anchor scale for one built skydome: <see cref="HorizonScale"/>, reduced where
    /// that would push the dome's far wall past the camera's far plane.
    ///
    /// <para>The 2.5x exists because the authored domes are ~8.8 km in radius on a 12.3 km map and
    /// would otherwise cut into the terrain. It is a MAXIMUM, not a constant: every chapter's dome
    /// is 6.4–12.0 km and clears the 40 km far plane at 2.5x — except <b>C1B's zone1 at 21.8 km</b>,
    /// which 2.5x puts at 54.5 km, so its far wall clipped and the engine's clear colour showed
    /// through the sky.
    /// Clamped, C1B lands at ~1.65x and every other chapter keeps 2.5x exactly, which is why no
    /// other chapter's sky moves.</para>
    ///
    /// <para>Measured from the built dome rather than a table, so a chapter is never assumed: the
    /// radius is the largest axis extent of its own geometry, which for a dome is its rim.</para>
    /// </summary>
    private float HorizonScaleFor(Node3D dome)
    {
        if (Mech3.WorldBuilder.DetachedWorldAabb(dome) is not { } aabb)
            return HorizonScale;
        var min = aabb.Position;
        var max = aabb.End;
        float radius = Mathf.Max(
            Mathf.Max(Mathf.Abs(min.X), Mathf.Abs(max.X)),
            Mathf.Max(
                Mathf.Max(Mathf.Abs(min.Y), Mathf.Abs(max.Y)),
                Mathf.Max(Mathf.Abs(min.Z), Mathf.Abs(max.Z))));
        if (radius <= 0f)
            return HorizonScale;
        float fitted = Mathf.Min(HorizonScale, _camera.Far * HorizonFarFraction / radius);
        if (fitted < HorizonScale)
            GD.Print($"horizon: dome radius {radius:0} m x {HorizonScale:0.##} would reach past the "
                     + $"{_camera.Far:0} m far plane — scaled {fitted:0.##}x instead");
        return fitted;
    }

    /// <summary>--anim-lab: the animation debugger's quiet stage — the effect/crash anchor
    /// stage, the mission spawn point, the freecam-style SpectatorCamera, an optional parked
    /// --plane= prop, and the AnimLab node itself (timeline + transport).</summary>
    private void BuildAnimLabStage(BuildState state, WorldSession session)
    {
        // The lab feeds Advance itself in fixed 1/60 s steps. ManualAdvance, not
        // SetProcess(false): Godot re-enables processing at READY for nodes that
        // override _Process, and the runtime enters the tree (with _plane, below)
        // after this line — a SetProcess here is silently undone and the world runs
        // at 2× (fixed steps + wall dt). See AnimRuntime.ManualAdvance.
        session.Runtime.ManualAdvance = true;

        // Build the lab's effect/crash stage under ONE staging node the lab moves in
        // front of the camera: the effect-template roots WorldBuilder skips (the
        // fireball/spark/smokeball/fire/trail/dirt roots the world never renders
        // ambiently), plus a meshless player crash-anchor set (healthy/destroyed/pieces
        // the crash def targets). Indexed so a played crash/effect def resolves its
        // puffer hosts AND its healthy/destroyed anchors HERE — scoped to this subtree —
        // instead of onto one of C1's 217 generic 'healthy' world nodes. With the runtime's
        // template stage built with Places on (WorldSession.Options.PlacesCalledTemplates,
        // sealed at construction above), a CALL_ANIMATION relocates the called template onto
        // the (in-front-of-camera) call site. IndexStage runs the reset states, hiding
        // the templates.
        var labStage = new Node3D { Name = "lab_stage_anchor" };
        // Which template roots: derived from the crash/damage defs themselves against this stage's
        // own scope — the crash-anchor set, built first and parented after, so the lab stages
        // exactly what BuildFlightCrashRuntime derives without changing this subtree's child order.
        var labAnchors = WorldEffectsFactory.BuildCrashAnchorSet();
        int effectRoots = WorldEffectsFactory.BuildEffectStage(state.Gamez, session.Builder.Scene,
            labStage, WorldEffectsFactory.CrashStageRootNames(session.Program, state.Gamez, labAnchors));
        labStage.AddChild(labAnchors);
        session.Root.AddChild(labStage);
        session.Runtime.IndexStage(labStage);
        GD.Print($"anim-lab: stage — {effectRoots} effect template(s) + player anchor set built + indexed");

        // The spawn the mission would place the player at — the camera starts here so
        // the interesting part of the map is in view, and (below) an optional parked
        // plane sits on it. Resolved once so the camera and plane agree.
        var labSpawns = SpawnPoints.LoadIa(state.MissionZrdrPath, _spec.Scenario);
        var (spawnPos, spawnLook) = _spawnPicker.ChooseSpawn(labSpawns, state.MissionZrdrPath,
            _spawnPicker.ChooseSpawnBase(labSpawns), 0, "");

        // Camera: the freecam SpectatorCamera (RMB look, WASD/QE move), like --freecam,
        // in place of the orbit view — the lab drives it (Frame/FollowNode) on
        // play/pick. Starts at the mission spawn; --pos/--direction override.
        var camPos = _spec.CamPos ?? spawnPos;
        var camLook = _spec.CamDir is { } labDir ? camPos + labDir : _spec.LookAt ?? spawnLook;
        var labCam = new SpectatorCamera(_camera, camPos, camLook) { ShowReadout = false };
        // --node=: the mission spawn is meaningless on a single-subtree stage — frame
        // the subject instead, unless the tester placed the eye themselves.
        if (state.NodeAabb is { } nodeBox && _spec.CamPos == null && _spec.LookAt == null && _spec.CamDir == null)
        {
            labCam.Frame(nodeBox);
        }
        _worldRoot!.AddChild(labCam);
        _spectator = labCam;

        // Optional stage prop: --plane= parks that aircraft at the mission spawn
        // point. No FlightController — unpainted by default like every static view
        // (--paint still applies one).
        if (_spec.PlaneNames.Count > 0)
        {
            long mark = StartupProfile.Mark();
            var planesGamez = GameZ.Load(state.PlanesGamezPath);
            StartupProfile.Record("gamez", mark);
            mark = StartupProfile.Mark();
            var parkedBuilder = new PlaneBuilder(planesGamez, state.Textures,
                scheme: _liveryResolver.SchemeFor(0, state.ZrdrPath, randomByDefault: false, _liveryResolver.NewPaintRng(),
                    _liveryResolver.PatternsForPlane(planesGamez, _spec.PlaneName)),
                patterns: _liveryResolver.Patterns);
            var parked = parkedBuilder.Build(_spec.PlaneName);
            StartupProfile.Record("plane", mark);
            state.MeshInstances += parkedBuilder.MeshInstanceCount;
            _worldRoot!.AddChild(parked);
            parked.Position = spawnPos;
            if ((spawnLook - spawnPos).LengthSquared() > 1e-6f)
            {
                parked.LookAtFromPosition(spawnPos, spawnLook, Vector3.Up);
            }
            state.What += $" + parked '{_spec.PlaneName}'";
            // The parked prop hangs beside the world content (not under it — it joins the tree
            // before the world root does, so its LookAtFromPosition needs an in-tree parent),
            // which puts it outside the selection/node-lab walk rooted at the world content.
            // Register it as an extra pick root so a click and the node lab's tree both reach it.
            _selectionExtraRoots.Add(parked);
        }

        var animLab = new UI.AnimLab(session.Runtime, session.Program, labCam,
            labStage, state.Textures, state.Sounds, _masterSeed, _spec.PlayAnim,
            // On a --node= stage the subject IS the stage and is already framed; letting
            // the lab re-aim on every Play swings the camera off the only object there
            // (measured: the tower left the frame entirely on its own destruction).
            autoFrame: _spec.CamPos == null && _spec.LookAt == null && _spec.CamDir == null
                       && state.NodeSubtree == null)
        {
            // Interactive shows the whole lab UI; a scripted --screenshot hides it so
            // the 3D shot stays byte-identical — unless --debug-anim-ui forces it on
            // to capture the timeline (the same convention as --debug-livery).
            ShowUi = !_captureDirector.Pending || _spec.DebugAnimUi,
            // The lab's camera follows whichever rung of the shared selection is current.
            Selection = _selection,
        };
        _worldRoot!.AddChild(animLab);
        state.AnimLabNode = animLab;
        GD.Print($"anim-lab: quiet stage, seed {_masterSeed}, fixed dt 1/60"
                 + (_spec.PlayAnim != null ? $", playing '{_spec.PlayAnim}'" : "")
                 + " — freecam (RMB look, WASD/QE move); transport on the button panel,"
                 + " P pause · . step · R restart · F picker · N node lab; click an object to follow");
        state.What += " + anim lab";
    }

    /// <summary>The parked-plane static view (--viewer or a bare --plane=): builds the model
    /// unpainted (--viewer) or pre-painted, then the damage / livery labs that only make sense
    /// on a parked plane.</summary>
    private void BuildStaticStage(BuildState state)
    {
        // The damage lab needs the pdpN torn-skin panels the plain viewer skips.
        // Every --viewer session gets one now, so H always has something
        // to toggle; --damage only decides whether it opens straight away. Built
        // hidden otherwise, and the panels are built hidden regardless, so a plain
        // --viewer still renders byte-identically.
        // Static views build unpainted unless --paint asks (randomByDefault: false),
        // so every existing orbit/damage screenshot renders exactly as before.
        // Resolved once: the livery lab below opens on exactly the scheme the plane
        // wears, not a second roll of --paint=random.
        long mark = StartupProfile.Mark();
        var staticPatterns = _liveryResolver.PatternsForPlane(state.Gamez, _spec.PlaneName);
        var staticScheme = _liveryResolver.SchemeFor(0, state.ZrdrPath, randomByDefault: false, _liveryResolver.NewPaintRng(), staticPatterns);
        // In --viewer the LIVERY LAB owns the livery and applies it itself, so the
        // model is built bare and there is one write path for paint (its Repaint).
        // Everywhere else the builder paints at construction as usual.
        var builder = new PlaneBuilder(state.Gamez, state.Textures, damagePanels: _spec.Viewer,
            scheme: _spec.Viewer ? null : staticScheme, patterns: _liveryResolver.Patterns);
        _plane = builder.Build(_spec.PlaneName);
        StartupProfile.Record("plane", mark);
        state.MeshInstances = builder.MeshInstanceCount;
        state.What = $"'{_spec.PlaneName}'";

        // Damage lab: per-part HP sliders driving the item-10c damage visuals on the
        // parked plane — the same DamageVisuals/puffer pipeline as flight, with the
        // distance-interval trails burning in place (DamageLab). Present in every
        // --viewer session (H), opened at launch only by --damage.
        if (_spec.Viewer)
        {
            mark = StartupProfile.Mark();
            var stats = PlaneStats.Load(state.ZrdrPath, _spec.PlaneName);
            StartupProfile.Record("zrdr", mark);
            if (stats.DestroyableParts.Count == 0)
            {
                GD.Print($"damage lab: '{_spec.PlaneName}' ({stats.DefName}) has no destroyable_parts");
            }
            else
            {
                // Stand-in puffers for the parked plane: it travels no distance, so the authored
                // distance-interval trail defs the FLIGHT lab plays would emit nothing here —
                // these burn in place instead (DamageVisuals.UpdateStatic), the heavy pair at the
                // authored prop1 anchor.
                var smoke = Effects.Puffer.MakePuffer(state.ZrdrPath, state.Textures, _worldRoot!, "pufftrails.json", "smokepuffer", ambience: _ambience);
                var fire = Effects.Puffer.MakePuffer(state.ZrdrPath, state.Textures, _worldRoot!, "pufftrails.json", "firepuffer", ambience: _ambience);
                var panelTrails = new List<Effects.Puffer>();
                for (int i = 0; i < 8; i++) // pool one per pdp panel — the lab can flip all of them
                    if (Effects.Puffer.MakePuffer(state.ZrdrPath, state.Textures, _worldRoot!, "pufftrails.json", "firepuffer", ambience: _ambience) is { } pt)
                        panelTrails.Add(pt);
                // The healthy↔torn candidate sets from the authored defs — the viewer
                // has no anim program, so the two reader files are loaded directly.
                var pairingDefs = new List<Mech3.AnimDefinition>();
                pairingDefs.AddRange(Mech3.AnimDefs.LoadFileDefs(state.ZrdrPath, "player_destruct_reset.json"));
                pairingDefs.AddRange(Mech3.AnimDefs.LoadFileDefs(state.ZrdrPath, "player-1.json"));
                var visuals = new DamageVisuals(builder.DamagePanels, _plane, stats, smoke, fire, panelTrails,
                    DamageVisuals.PanelPairingSets(pairingDefs));
                // the HUD gauge cluster as a lab toggle (user request): the damage
                // dial mirrors the sliders, blinks on decreases like a flight hit
                var labGauges = GaugeCluster.Build(state.Gamez, _spec.PlaneName, state.Textures, stats.DestroyableParts);
                _damageLab = new DamageLab(stats, new ViewerDamageTarget(visuals),
                    _spec.DamagePreset, labGauges)
                {
                    StartHidden = !_spec.DamageLab, // --damage opens it; plain --viewer waits for F5
                };
                _worldRoot!.AddChild(_damageLab);
                GD.Print($"damage lab: {stats.DestroyableParts.Count} part sliders, " +
                         $"{visuals.PanelCount} panels, {panelTrails.Count} panel fire trails"
                         + (_spec.DamageLab ? "" : " (hidden — F5)"));
                state.What += _spec.DamageLab ? " + damage lab" : " + damage lab (F5)";
            }
        }

        // Livery lab (--viewer): pattern / RGB colour sliders / decal slots, repainting
        // the parked plane live via PlaneBuilder.Repaint. Built hidden-by-default state
        // is "unpainted" unless --paint named a scheme, so an unadorned --viewer
        // screenshot is byte-identical to the pre-paint viewer. L toggles it.
        if (_spec.Viewer && builder.SkinPrefix != null)
        {
            var lab = new UI.LiveryLab(builder, _liveryResolver.PaintCatalog(state.ZrdrPath), state.Textures, staticScheme,
                _liveryResolver.Patterns.PatternsFor(builder.SkinPrefix))
            {
                DebugShow = _spec.DebugLivery.HasValue,
                DebugPatternSteps = _spec.DebugLivery ?? 0,
            };
            _worldRoot!.AddChild(lab);
            state.What += " + livery lab";
        }

        if (_spec.Viewer)
            state.What += " + mesh lab";
    }

    /// <summary>Joins the built subject to the tree, then the labs shared by every mode that
    /// observes it: the world selection/node/damage labs (if built), the viewer's mesh lab, the
    /// marker overlay and the weapon lab. Returns false if the session must abort (--weapon-test
    /// already issued its quit).</summary>
    private bool AttachPlaneAndLabs(BuildState state)
    {
        _worldRoot!.AddChild(_plane);
        // The shared selection joins after the world does: its pick walk and its highlight box
        // both read GlobalTransform, which on a detached subtree is identity + error spam.
        if (_selection != null)
        {
            _worldRoot!.AddChild(_selection);
        }
        // The node lab joins after the selection, so its first _Process (which carries the
        // scripted dump) runs once the selection's own scripted pick has settled.
        if (_nodeLab != null)
        {
            _worldRoot!.AddChild(_nodeLab);
        }
        // The damage lab joins after the node lab, so a scripted script can select through the
        // node lab's name index on the frame it runs.
        if (_worldDamageLab != null)
        {
            _worldRoot!.AddChild(_worldDamageLab);
        }
        // Mesh lab (--viewer): normals / wireframe+seams / zone boxes / lighting, plus live
        // cull-mode and normal-source overrides. Like the other two labs it is built in every
        // --viewer session and starts hidden (M), so an unadorned viewer screenshot is
        // unchanged; --debug-mesh opens it and presets modes. Built AFTER the plane joins the
        // tree: it reads geometry back through GlobalTransform, which on a detached node
        // returns identity and logs an error per call rather than walking the subtree.
        if (_spec.Viewer && _plane != null)
            _worldRoot!.AddChild(new UI.MeshLab(_plane, PlaneCollider.Build(_plane),
                _sun, _env, _camera)
            { DebugSpec = _spec.DebugMesh });
        // Marker overlay (--viewer --plane, key K): the firepoint / pylon / target gizmos on the
        // parked aircraft. Only on the parked plane — a chapter world has no marker rig
        // — and after the plane joins the tree, since it reads each marker's GlobalPosition. Built
        // hidden unless --markers opened it, so an unadorned viewer screenshot is unchanged.
        if (_spec.Viewer && !_spec.WorldMode && _plane != null)
        {
            _worldRoot!.AddChild(new UI.MarkerOverlay(_plane) { StartHidden = !_spec.MarkersOverlay });
            state.What += _spec.MarkersOverlay ? " + marker overlay" : " + marker overlay (K)";
        }
        // --weapon-test: the 48-weapon pass check on a PARKED plane — the one weapon-lab host left in
        // the viewer path. The interactive lab lives in flight because it needs a real world, the
        // session's pool and a real trigger; this probe deliberately keeps the cheap no-world path
        // instead, since it only asks whether every weapon mounts and spawns without throwing,
        // which needs no chapter, no colliders and no frame.
        if (_spec.WeaponTest && _spec.Viewer && !_spec.WorldMode && _plane != null)
        {
            long mark = StartupProfile.Mark();
            var labWeapons = WeaponDefs.Load(state.ZrdrPath, Messages.Load(state.MessagesPath));
            StartupProfile.Record("zrdr", mark);
            // The bench fires the airframe's WHOLE rig: ForRig's 4 gun groups + every pylon,
            // seeded from the plane's stock entry where it has one, so a weapon that stock never
            // mounts still gets a mount of its own class rather than a skip. A plane the table omits
            // passes a null stock def — ForRig synthesizes the rig from the markers regardless.
            LoadoutDef? stock = null;
            foreach (var ldef in StockLoadouts.Load().All.Values)
            {
                if (ldef.Model == _spec.PlaneName)
                {
                    stock = ldef;
                    break;
                }
            }
            var benchLoadout = Loadout.ForRig(_plane, labWeapons, stock);
            // The bench's own scene-less pool: rockets fly streak-only, gun impacts show the spark,
            // hardpoint impacts the pool's explosion stand-in, and there is no DamageSink. No
            // WeaponLab is built here at all — the lab is a flight-mode panel that fires nothing;
            // the pass check is WeaponBench's.
            var benchPool = new ProjectilePool(state.Textures, null, null);
            _worldRoot!.AddChild(benchPool);
            if (_camera != null)
                benchPool.Viewers.Add(_camera);
            // Mount and fire every one of the 48 weapons once per mount and report any that throw,
            // then quit (windowless under --headless). The report is synchronous (Spawn does the
            // muzzle math + pool insert without needing a frame), so no world tick is required.
            string report = WeaponBench.Run(_plane, benchLoadout, labWeapons, benchPool).Report;
            GD.Print(report);
            _probeRunner.WriteScratch("weapon_test.txt", report);
            GetTree().Quit();
            return false;
        }
        return true;
    }

    /// <summary>The deck is now in the tree at its original position; remember its centre so
    /// _Process can re-anchor it under each player every frame, and give every rig past the
    /// first its own copy (the deck follows *a* camera — see AssignCloudDecks).</summary>
    private void AssignCloudDeckIfBuilt(BuildState state)
    {
        if (state.CloudDeck != null)
        {
            _weatherRig?.SetDeckCenter(OrbitCamera.MergedAabb(state.CloudDeck).GetCenter());
            if (state.DeckUndimmedMeshes != null)
                _weatherRig?.SetDeckUndimmedMeshes(state.DeckUndimmedMeshes);
            AssignCloudDecks(state.CloudDeck);
        }
    }

    /// <summary>Spectator mode (--freecam): the live world with no aircraft at all, observed
    /// from a free-flying camera. It starts where the mission would have spawned the player (or
    /// wherever --pos put it), so the interesting part of the map is already in view rather than
    /// a corner of empty sea.</summary>
    private void BuildFreecamSpectator(BuildState state)
    {
        if (!_spec.Freecam)
            return;
        Vector3 camPos, camLookAt;
        if (_spec.EmptyStage)
        {
            // The empty stage has no mission and therefore no spawn list: look at the grid
            // origin, which is where a --stage=empty subject is put.
            camPos = EmptyStage.CameraPos;
            camLookAt = Vector3.Zero;
        }
        else
        {
            var freecamSpawns = SpawnPoints.LoadIa(state.MissionZrdrPath, _spec.Scenario);
            (camPos, camLookAt) = _spawnPicker.ChooseSpawn(freecamSpawns, state.MissionZrdrPath,
                _spawnPicker.ChooseSpawnBase(freecamSpawns), 0, "");
        }
        if (_spec.CamPos is { } cp) camPos = cp;
        // The aim: a direction from wherever the eye ended up, or the named point.
        if (_spec.CamDir is { } cd) camLookAt = camPos + cd;
        else if (_spec.LookAt is { } la) camLookAt = la;
        _spectator = new SpectatorCamera(_camera, camPos, camLookAt)
        {
            // A scripted --screenshot run wants the frame clean of the overlay.
            ShowReadout = !_captureDirector.Pending,
        };
        _worldRoot!.AddChild(_spectator);
        state.What += " + freecam";
        GD.Print($"freecam: spectator camera at ({camPos.X:0}, {camPos.Y:0}, {camPos.Z:0}) — " +
                 "hold RMB to look, WASD/QE to move, Shift boost, wheel sets speed; " +
                 "click an object to select it, PgUp/PgDn walk its ancestor ladder (Home/End jump), " +
                 "N opens the node lab, F5 the damage lab on whatever destructible is selected");
    }

    /// <summary>--fly (and --stunt): builds every rendered rig's aircraft — model, loadout,
    /// HUD, audio, stunt/crash hookup — sharing the session-wide flight data (planes gamez,
    /// per-plane stats, weapons, the projectile pool) loaded once above the per-player loop.
    /// The per-player body lives in its own FlightRigAssembler.</summary>
    private void BuildFlightRigs(BuildState state)
    {
        // Session-wide flight data, loaded once and shared by every player: the aircraft
        // models' gamez, the plane's stats, the sound defs/archive. Only the built nodes
        // and the per-plane state below are per player.
        long mark = StartupProfile.Mark();
        // On the empty stage the session gamez IS planes.zbd (there is no chapter world),
        // so there is nothing to load a second time.
        var planesGamez = _spec.EmptyStage ? state.Gamez : GameZ.Load(state.PlanesGamezPath);
        StartupProfile.Record("gamez", mark);
        // Stats are per plane, not per player (splitscreen players can pick
        // different aircraft) — load each distinct one once, logging it as it appears.
        var statsCache = new Dictionary<string, PlaneStats>();
        PlaneStats StatsFor(string plane)
        {
            if (statsCache.TryGetValue(plane, out var cached))
                return cached;
            var loaded = PlaneStats.Load(state.ZrdrPath, plane);
            statsCache[plane] = loaded;
            GD.Print($"flight stats [{loaded.DefName}]: fd_speed={loaded.FdSpeed} m/s " +
                     $"weight={loaded.VehWeight} engine={loaded.EnginePower:0.00} " +
                     $"torques=({loaded.PitchTorque},{loaded.RollTorque},{loaded.RudderTorque})");
            return loaded;
        }
        // The camera's per-plane tuning, cached the same way and for the same reason. Only the
        // chase distance is applied; the line names it so a capture's evidence is in its own log.
        var camCache = new Dictionary<string, CamParams>();
        CamParams CamParamsFor(string plane)
        {
            if (camCache.TryGetValue(plane, out var cached))
                return cached;
            var loaded = CamParams.Load(state.ZrdrPath, plane);
            camCache[plane] = loaded;
            GD.Print($"camera [{loaded.DisplayName ?? plane}]: dist={loaded.Dist:0.##} m" +
                     (loaded.FromData
                        ? loaded.DisplayName == null ? " (camparam default — no block of its own)" : ""
                        : " (no camparam.json — built-in defaults)"));
            return loaded;
        }
        // Splitscreen: several own-ship engine stacks in one mix — equal-power scale them.
        float mixGain = 1f / Mathf.Sqrt(_rigs.Count);
        // The launchscreen's join flow binds the pads; a CLI launch derives them
        // from the connected roster instead.
        var padAssignment = _menuPads ?? Pads.AssignPads(_rigs.Count);
        if (_menuPads != null)
            Pads.LogPads(_menuPads);
        // One livery RNG for the session, so P1..P4 draw distinct colours from one
        // stream and --paint-seed reproduces the whole field.
        var paintRng = _liveryResolver.NewPaintRng();
        // One spawn list for the session; each player takes the next index (wrapping).
        // The empty stage has no mission, so nothing to read: ChooseSpawn takes the
        // --pos/default override placed over the grid origin.
        var spawnList = _spec.EmptyStage ? null : SpawnPoints.LoadIa(state.MissionZrdrPath, _spec.Scenario);
        int spawnBase = _spawnPicker.ChooseSpawnBase(spawnList);

        // Weapons: the typed weapons.json catalogue + the stock loadouts, loaded
        // once, and ONE shared projectile/effect pool every player's guns fire into
        // (projectiles live in the shared world, so every splitscreen pane sees them). The
        // pool reuses the session texture/sound archives (tracer/muzzle textures, impact sounds)
        // and the world gamez + its SceneBuilder, so rockets instance their FLYOUT MODEL body
        // (`he_rocket` …) from the chapter's own prototype roots.
        mark = StartupProfile.Mark();
        var weaponMessages = Messages.Load(state.MessagesPath);
        var weaponDefs = WeaponDefs.Load(state.ZrdrPath, weaponMessages);
        var stockLoadouts = StockLoadouts.Load();
        var shakeDefs = ShakeDefs.Load(state.ZrdrPath);
        // The ai.zrd turret table (C9a). A missing/broken file costs the gunners, not the session.
        TurretDefs? turretDefs = null;
        try
        {
            turretDefs = TurretDefs.Load(state.ZrdrPath);
        }
        catch (Exception e)
        {
            GD.PushWarning($"turrets: ai.zrd unavailable — no turret gunners: {e.Message}");
        }
        StartupProfile.Record("zrdr", mark);
        // flyoutAnims: the world program also carries the rockets' FLYOUT MODEL_ANIMATION defs
        // (cam_anim / missile_puffers), from which the pool builds each type's smoke trail.
        // Null on the empty stage (no world program) — rockets there fly trail-less, like the body.
        var projectiles = new ProjectilePool(state.Textures, state.Sounds, state.SoundDefs,
            flyoutGamez: state.Gamez, flyoutScene: state.WorldScene, flyoutAnims: state.CrashProgram,
            soundGroups: state.SoundGroups, ambience: _ambience)
        {
            // Route weapon hits to the world's destructibles: the pool's raycast
            // reports the struck collider, the runtime resolves it to a destructible and
            // spends the weapon's HEALTH_DAMAGE. Null runtime ⇒ impacts stay cosmetic.
            DamageSink = state.WorldRuntime != null ? state.WorldRuntime.DamageAt : null,
        };
        // EVERY pane's camera, not just player 1's. The tracer pixel floor is a screen-space rule
        // over one shared world mesh, so binding P1 alone sized every round against P1's distance
        // and pane and then drew that geometry in all the other panes — the splitscreen bug where
        // P1's tracers looked right and everyone else's were far too big. The pool takes the
        // nearest viewer; the rigs are built before this point and are not rebuilt on respawn.
        if (_rigs.Count > 0)
        {
            foreach (var rig in _rigs)
            {
                projectiles.Viewers.Add(rig.Camera);
            }
        }
        else if (_camera != null)
        {
            projectiles.Viewers.Add(_camera);
        }
        _worldRoot!.AddChild(projectiles);
        _projectiles = projectiles;

        // The world-effects runtime: one per session, rendering the impact/destruction
        // puffer effects the world runtime cannot (its factory is gone after the build). A
        // rocket impact plays its named effect here; the world runtime routes a death's
        // CALL_ANIMATION of a curated effect here too. Needs the world's SceneBuilder to stage
        // the templates, so it is built only when the world was. Through EnsureWorldEffects (not
        // built directly) so a later --destroy=/damage-lab demand on the SAME session finds the
        // cache instead of building a second one — it also wires projectiles.EffectSink,
        // since this is the one call site that has a ProjectilePool to wire it to.
        AnimRuntime? worldEffects = null;
        if (state.WorldScene != null && state.WorldRuntime != null)
        {
            worldEffects = _worldEffectsFactory.EnsureWorldEffects(state.Gamez, state.WorldScene,
                state.Textures, state.CrashProgram!, state.WorldRuntime, projectiles); // the rigs' graze reaction plays through the same runtime
        }

        // Stunt run: the mission's danger-zone objectives from ia.json
        // dzones, positions resolved against this chapter world's gamez, display strings
        // from targets.json → messages.json. --stunt only. Parsed ONCE for the session —
        // every player then races an independent copy of the same zone list, so the
        // archives are read once no matter how many pilots are in.
        StuntMission? stuntZones = null;
        StuntRace? race = null;
        if (_spec.Stunt && _spec.EmptyStage)
        {
            GD.Print("--stunt has no danger zones on the empty stage (no mission, no world) — flying free");
        }
        else if (_spec.Stunt)
        {
            stuntZones = StuntMission.Load(state.Gamez, state.MissionZrdrPath, Messages.Load(state.MessagesPath));
            if (stuntZones == null)
                // Expected for the chapters whose IA1 has no dzones (C1C, C2B) — a data
                // fact, not a fault, so a plain line (log hygiene: no stack traces).
                GD.Print($"--stunt: no danger zones for {_spec.Chapter}/{_spec.Mission} — flying free");
            else if (_rigs.Count > 1)
                race = new StuntRace(); // splitscreen: a race, ranked on the shared board
        }

        // Dogfight (--vs): built here, before the rigs — same reason Race is (FlightRigAssembler
        // binds every pane's VersusHud to this one instance below); the score/respawn plumbing
        // that feeds it Downed reports only runs once every rig exists, further down.
        VersusMatch? versus = _spec.Versus
            ? new VersusMatch(_rigs.Count, _spec.VsKills, _spec.VsTimeMinutes * 60f)
            : null;

        // The game's own HUD bitmap font (extracted/rimage/5pointhud*.png), loaded once and
        // shared across panes — the weapon readout and the --hud-font-test proof overlay
        // both draw with it. Null (one log line) if the rimage atlas is absent; both are then
        // simply not built.
        HudFont? hudFont = HudFont.Load(Path.Combine(_dataRoot, "extracted", "rimage"));

        // The gun aiming reticle's pipper: the game's own impact_point.png, loaded once
        // and shared across panes (it carries its own alpha — no colour-keying). Null (no file)
        // simply omits the reticle.
        Texture2D? reticleTex = ImpactReticle.LoadTexture(
            Path.Combine(_dataRoot, "extracted", "rimage"), "impact_point.png");

        // Per-player rigs: one FlightRigAssembler over the session data above, run in
        // ascending player order — the paint rng and the spawn index wrap are shared
        // streams, so the draw order is load-bearing (see src/Session/FlightRigAssembler.cs).
        // Which spawn placement this session flies is chosen ONCE, here, by picking an
        // IFlightStarts implementation — never by a runtime flag inside one. A race gets the
        // abreast starting grid; solo flight, Dogfight, the zone-less chapters (no dzones, so no
        // race) and every --det run keep the plain per-player walk of the mission's spawn list and
        // stay byte-identical to what they emitted before the grid existed. --det is bypassed by
        // *not constructing* RaceGrid at all, which is what makes that guarantee structural rather
        // than a promise about a branch inside it.
        //
        // ⚠ This sits here and NOT beside `new SpawnPicker(...)` at the top of StartSession, where
        // it might look like it belongs: at that point the session does not yet know whether it
        // will be a race. That is settled a few lines above — StuntMission.Load must have returned
        // zones AND _rigs.Count must be > 1 — so this is the first site that has `race` in scope,
        // and it still runs before any rig is assembled, which is all the grid needs. _spawnPicker
        // itself stays exactly what it was: the weapon lab and the freecam spectator call
        // ChooseSpawn on it directly, and the grid delegates to it for the anchor.
        IFlightStarts flightStarts = race != null && !_spec.Det
            ? new RaceGrid(_spawnPicker, GroundSampler())
            : _spawnPicker;
        var rigInputs = new FlightRigAssembler.Inputs
        {
            Ambience = _ambience,
            PlanesGamez = planesGamez,
            StatsFor = StatsFor,
            CamParamsFor = CamParamsFor,
            RigCount = _rigs.Count,
            MixGain = mixGain,
            PadAssignment = padAssignment,
            PaintRng = paintRng,
            SpawnList = spawnList,
            SpawnBase = spawnBase,
            WeaponDefs = weaponDefs,
            WeaponMessages = weaponMessages,
            StockLoadouts = stockLoadouts,
            TurretDefs = turretDefs,
            Shakes = shakeDefs,
            Projectiles = projectiles,
            HudFont = hudFont,
            ReticleTex = reticleTex,
            StuntZones = stuntZones,
            Race = race,
            VersusMatch = versus,
            Rigs = _rigs,
            Textures = state.Textures,
            ZrdrPath = state.ZrdrPath,
            ChapterZrdrPath = SessionPaths.ChapterZrdr(_dataRoot, _spec.Chapter),
            MissionZrdrPath = state.MissionZrdrPath,
            Gamez = state.Gamez,
            WorldScene = state.WorldScene,
            WorldRuntime = state.WorldRuntime,
            WorldEffects = worldEffects,
            TouchdownDefs = _worldEffectsFactory.TouchdownDefs,
            CrashProgram = state.CrashProgram,
            Sounds = state.Sounds,
            SoundDefs = state.SoundDefs,
            SoundGroups = state.SoundGroups,
            DebugCollision = state.DebugCollision,
        };
        var assembler = new FlightRigAssembler(_spec, _liveryResolver, flightStarts,
            _worldEffectsFactory, _worldRoot!, rigInputs);
        for (int pi = 0; pi < _rigs.Count; pi++)
        {
            assembler.Assemble(pi, _rigs[pi]);
        }
        state.MeshInstances += assembler.MeshInstances;
        state.What += assembler.WhatSuffix;

        // Damage lab in flight (F5): the same panel --viewer hosts, bound to P1's real
        // PlaneDamage instead of visuals alone — so a dialled-in state drives the HUD DMG line,
        // the damaged-engine mix and the gauge dial, and can then be flown. The sim keeps
        // running under it, and its sliders follow the hits taken while it is up. Splitscreen
        // binds P1 only: the panel is one overlay, not one per pane.
        if (_rigs.Count > 0 && _rigs[0].Controller is { Damage: not null } p1)
        {
            var p1Stats = StatsFor(PlaneRoster.PlaneFor(_spec, 0));
            _damageLab = new DamageLab(p1Stats,
                new FlightDamageTarget(p1, _rigs.Count > 1 ? "P1" : null), _spec.DamagePreset)
            {
                StartHidden = !_spec.DamageLab, // --damage opens it; a plain flight waits for F5
                RightAligned = true,            // the top-left corner is the flight HUD's
            };
            _worldRoot!.AddChild(_damageLab);
            GD.Print($"damage lab: {p1Stats.DestroyableParts.Count} part sliders on the flown " +
                     "plane's armor+HP" + (_spec.DamageLab ? "" : " (hidden — F5)"));
            state.What += _spec.DamageLab ? " + damage lab" : " + damage lab (F5)";
        }
        else if (_spec.DamageLab)
        {
            GD.Print($"damage lab: '{_spec.PlaneName}' has no destroyable_parts");
        }

        // The weapon lab in flight: the panel bound to player 1's
        // HELD aircraft inside a real chapter world — so it fires through the session's own
        // fully-wired ProjectilePool (flyout gamez/scene/anims, the world-effects EffectSink and the
        // destructible DamageSink, all built above), never a scene-less pool of its own.
        // Nothing here is per-player: the lab is one overlay on one aircraft, like the
        // damage lab above. --weapon-test never reaches this path (it stays a parked-plane probe).
        if (_spec.WeaponLab && _rigs.Count > 0 && _rigs[0] is { Controller: { PlaneModel: not null } p1c } labRig)
        {
            // A soak run must never dry up: the lab exists to watch a weapon fire, not to manage
            // ammo. Explicit flags still win — --ammo=N caps the load on purpose.
            p1c.InfiniteAmmo = true;
            // --weapon-fire holds the real trigger, the one free flight pulls (decision 3) — which
            // one follows the panel's bank, so the lab sets it rather than this call site.
            var lab = new UI.WeaponLab(p1c.PlaneModel, weaponDefs, p1c.Loadout, _spec.PlaneName,
                host: p1c, camera: labRig.Camera)
            {
                DebugShow = true,   // the lab IS the session now — the panel is why you launched it
                InitialWeapon = _spec.WeaponSelect,
                InitialMount = _spec.WeaponMount,
                AutoFireAtStart = _spec.WeaponFire,
                CycleFrames = _spec.WeaponCycle,
                DebugClickRequested = _spec.WeaponClick,
                DebugClick = _spec.WeaponClickAt,
                DebugClickAimOnly = _spec.WeaponClickAimOnly,
                DebugTarget = _spec.WeaponTarget,
                DebugSurface = _spec.WeaponSurface,
                StandoffAtStart = _spec.WeaponStandoff,
                FreeCameraAtStart = _spec.WeaponFreeCamera,
                CameraToggleFrames = _spec.WeaponCameraToggle,
            };
            _worldRoot!.AddChild(lab);
            // The lab is one overlay on one aircraft (like the damage lab), and its camera hand-off
            // takes that rig's camera — so in splitscreen it binds P1 and says so rather than
            // silently leaving the other panes' pilots without a panel they can see.
            if (_rigs.Count > 1)
            {
                GD.Print($"weapon lab: {_rigs.Count} players — the lab binds P1's aircraft and P1's " +
                         "pane only; the other panes fly normally");
            }
            GD.Print($"weapon lab: '{_spec.PlaneName}' held " +
                     (_spec.EmptyStage ? "on the empty stage" : $"in {_spec.Chapter}") +
                     ", firing through the session pool" +
                     (_spec.WeaponFire ? " (--weapon-fire: trigger held)" : "") +
                     (_spec.WeaponCycle > 0 ? $" (--weapon-cycle: a weapon every {_spec.WeaponCycle} frames)" : ""));
            // The authored impact/destruction effects need the world-effects runtime, which is only
            // built when there IS a world program — say so rather than silently drawing stand-ins.
            if (state.WorldScene == null)
            {
                GD.Print("weapon lab: no world program on this stage — impacts fall back to the " +
                         "pool's stand-in burst and rockets fly without their FLYOUT body/trail");
            }
            state.What += " + weapon lab";
        }
        else if (_spec.WeaponLab)
        {
            GD.Print("weapon lab: no flight rig to host it (nothing was built to hold)");
        }

        // The race's shared results board: one ranked row per player, over the
        // WHOLE window rather than inside a pane — the race ends for everybody at once — so
        // it goes on its own CanvasLayer above the splitscreen panes. Any player's R there
        // is a rematch, which restarts every plane, so it routes back through the session.
        if (race != null)
        {
            var board = StuntRaceBoard.Build(race, $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
                exitsToMenu: _menuDriven);
            var boardLayer = new CanvasLayer { Name = "race_board", Layer = UI.HudLayers.Board };
            boardLayer.AddChild(board);
            _worldRoot!.AddChild(boardLayer);
            foreach (var rig in _rigs)
                if (rig.Controller != null)
                    rig.Controller.RestartRace = () => RestartRace(race);
            GD.Print($"stunt race: {_rigs.Count} pilots over {stuntZones!.TotalCount} danger zones, " +
                     "own progress + clock each, shared ranked board");
        }

        // Dogfight (--vs): the match bookkeeping, fed by every rig's Downed report. A killer
        // inside the roster scores a kill; anything else — terrain, mid-air, an unowned or
        // non-player round — is a plain death. The match ignores post-completion events itself,
        // so no guard is layered here. The rigs report facts; only this session applies rules.
        if (versus is { } match)
        {
            _versus = match;
            foreach (var rig in _rigs)
                if (rig.Controller is { } pilot)
                {
                    pilot.AutoRespawnAfter = VersusRespawnDelay; // crash cam, then back in — R skips
                    pilot.Match = match;                  // R-ownership gate: board-up ⇒ rematch
                    pilot.RestartMatch = () => RestartMatch(match);
                    pilot.Downed += (victim, killer) =>
                    {
                        if (killer is int k && k >= 0 && k < match.PlayerCount)
                            match.RegisterKill(k, victim);
                        else
                            match.RegisterDeath(victim);
                    };
                    // The kill banner: a SEPARATE subscription from the scoring one above —
                    // every pane's HUD hears every Downed report, not just the shooter's/victim's,
                    // so the whole field sees who went down.
                    pilot.Downed += (victim, killer) =>
                    {
                        foreach (var other in _rigs)
                            other.Controller?.VersusHud?.OnKill(killer, victim);
                    };
                }
            match.MatchCompleted += () => GD.Print("dogfight: match complete — " + string.Join(", ",
                match.Standings().Select(s => $"P{s.PlayerIndex + 1} {s.Kills}K/{s.Deaths}D (#{s.Rank})")));
            GD.Print($"dogfight: {_rigs.Count} pilots, " +
                     (match.KillTarget > 0 ? $"first to {match.KillTarget} kills" : "no kill target") + ", " +
                     (match.TimeLimit > 0f ? $"{match.TimeLimit / 60f:0.#} min limit" : "no time limit"));

            // The match's shared results board: same construction as the race board above —
            // one CanvasLayer over the whole window (the match ends for everybody at once), R
            // routed back through this session via RestartMatch.
            var board = VersusBoard.Build(match, $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
                exitsToMenu: _menuDriven);
            var boardLayer = new CanvasLayer { Name = "dogfight_board", Layer = UI.HudLayers.Board };
            boardLayer.AddChild(board);
            _worldRoot!.AddChild(boardLayer);
        }

        // --incoming: the near-miss test rig — a phantom shooter on every pilot's six, so the
        // incoming-fire cue is reachable with one player and nothing in the world that shoots back.
        if (_spec.IncomingPass is float incomingPass)
        {
            var incoming = new IncomingFire(projectiles, weaponDefs, incomingPass, _spec.IncomingWeapon);
            foreach (var rig in _rigs)
                if (rig.Controller != null)
                    incoming.AddTarget(rig.Controller);
            _worldRoot!.AddChild(incoming);
            _incomingFire = incoming;
            GD.Print($"--incoming: rounds passing {incomingPass:0.0} m from every player" +
                     (_spec.IncomingWeapon != null ? $" ({_spec.IncomingWeapon})" : " (their own gun)"));
        }

        // The AI actor seam (M4 A2): the spawner shares the session data the rigs were built
        // from, and SpawnAiAircraft works from here on — at build (--ai=), or at any later sim
        // step (generators, the mission script, the ai-actor suite's runtime-spawn case).
        _aiSpawner = new AiAircraftSpawner(_spec, _liveryResolver, _worldEffectsFactory,
            _worldRoot!, rigInputs);
        if (_spec.AiPlanes is { Count: > 0 } aiPlanes && _rigs.Count > 0
            && _rigs[0].Controller is { } lead)
        {
            // Ahead of P1 on its own spawn heading, fanned right/left, each holding that course.
            var basis = lead.GlobalTransform.Basis;
            var fwd = -basis.Z;
            var right = basis.X;
            for (int i = 0; i < aiPlanes.Count; i++)
            {
                float lateral = 60f * ((i + 1) / 2) * (i % 2 == 0 ? 1f : -1f);
                var pos = lead.WorldPosition + fwd * 250f + right * lateral;
                SpawnAiAircraft(aiPlanes[i], pos, pos + fwd, AiPilot.HoldingCourse(pos, pos + fwd));
            }
            state.What += $" + {aiPlanes.Count} AI";
        }

        // --generators: the mission's egen enemy generators (M4 B6), spawning through the seam
        // above. Loaded here because the drop rules need the built world (host-node resolution).
        if (_spec.Generators)
        {
            List<EnemyGeneratorDef> egenDefs;
            try
            {
                egenDefs = EnemyGenerators.Load(state.MissionZrdrPath);
            }
            catch (IOException e)
            {
                GD.Print($"egen: no generator file for {_spec.Chapter}/{_spec.Mission}: {e.Message}");
                egenDefs = new List<EnemyGeneratorDef>();
            }
            var netNames = new HashSet<string>(
                AiNets.LoadIndex(rigInputs.ChapterZrdrPath).Values,
                StringComparer.OrdinalIgnoreCase);
            _generators = new AiGeneratorRuntime(egenDefs, rigInputs.WorldRuntime, netNames,
                _spec.GeneratorsPlane, SpawnAiAircraft);
            _worldRoot!.AddChild(_generators);
            GD.Print($"egen: {_generators.LiveCount} of {egenDefs.Count} generator(s) live for " +
                     $"{_spec.Chapter}/{_spec.Mission}, spawning '{_spec.GeneratorsPlane}'");
            state.What += $" + {_generators.LiveCount} generator(s)";
        }

        if (_rigs.Count > 1)
        {
            var flown = new List<string>(_rigs.Count);
            for (int pi = 0; pi < _rigs.Count; pi++)
                flown.Add($"P{pi + 1} '{PlaneRoster.PlaneFor(_spec, pi)}'");
            state.What += $" + splitscreen {string.Join(", ", flown)}";
        }
        else
        {
            state.What += $" + '{_spec.PlaneName}' flying";
        }
    }

    /// <summary>--destroy=&lt;name&gt;: kill a named destructible at session build so a
    /// --screenshot captures its destruction with nobody at the controls. Reuses the
    /// weapon-damage path — DamageAt runs the full death (the healthy→destroyed swap fires
    /// synchronously here; the debris and effects play out as the runtime self-ticks through the
    /// screenshot warm-up). The world subtree is already in the tree (added above), so the
    /// death's global-transform reads and the effect stage are valid. Flight already built +
    /// wired the world-effects runtime (to the projectile pool too); a plane-less --freecam asks
    /// for one here so the destruction's fire/smoke still render — gated on --destroy, so a
    /// plain --freecam regression builds nothing extra.</summary>
    private void ApplyDestroyOverride(BuildState state)
    {
        if (_spec.DestroyName != null && state.WorldRuntime != null)
        {
            if (state.WorldScene != null)
            {
                _worldEffectsFactory.EnsureWorldEffects(state.Gamez, state.WorldScene, state.Textures, state.CrashProgram!, state.WorldRuntime);
            }
            int killed = Testing.ProbeRunner.TriggerDestroy(state.WorldRuntime, _spec.DestroyName, out var destroyBounds);
            state.What += killed > 0 ? $" + destroyed {killed}× '{_spec.DestroyName}'"
                               : $" + destroy '{_spec.DestroyName}' (no match)";
            // Auto-frame the plane-less freecam on what it killed, unless the tester placed the
            // camera themselves (--pos/--direction) — so a bare `--freecam --chapter=CX
            // --destroy=name --screenshot=x.png` is a complete, self-framing destruction shot.
            if (killed > 0 && _spectator != null && _spec.CamPos == null && _spec.LookAt == null
                && _spec.CamDir == null && destroyBounds.Size.LengthSquared() > 0f)
            {
                _spectator.Frame(destroyBounds);
            }
        }
        else if (_spec.DestroyName != null)
        {
            GD.Print($"--destroy='{_spec.DestroyName}' ignored: no chapter world " +
                     "(pair it with --freecam/--fly + --chapter=)");
        }
    }

    /// <summary>The "loaded ..." summary line + the per-pane/texture-census follow-ups, printed
    /// once the whole build (including any --fly rigs) has finished.</summary>
    private void LogBuildSummary(BuildState state, Stopwatch sw)
    {
        GD.Print($"loaded {state.What}: {state.Gamez.Nodes.Count} gamez nodes, " +
                 $"{state.MeshInstances} mesh instances, {state.Colliders} colliders, " +
                 $"{Mech3.SceneBuilder.ClampedSurfaceTotal} uv-clamped + " +
                 $"{Mech3.SceneBuilder.EdgeClampedSurfaceTotal} edge-clamped surfaces, {sw.ElapsedMilliseconds} ms");
        if (_rigs.Count > 1)
            foreach (var rig in _rigs)
                GD.Print($"view P{rig.Index + 1}: layer {Mathf.Log(rig.VisualLayer) / Mathf.Log(2) + 1:0} " +
                         $"cull 0x{rig.Camera.CullMask:X5}, sky={(rig.Horizon != null ? "own" : "none")} " +
                         $"deck={(rig.Deck != null ? "own" : "none")} " +
                         $"whiteout={(rig.Whiteout != null ? "own" : "none")}");
        // The authored-mip coverage, said out loud per chapter: a chapter never loads its whole
        // archive, so "installed N" alone cannot show whether a level was missed or simply unused.
        var tex = state.Textures;
        if (tex.AuthoredMipsAvailable > 0)
        {
            string refused = tex.AuthoredMipsRefused > 0 ? $", {tex.AuthoredMipsRefused} REFUSED" : "";
            GD.Print(Mech3.TextureArchive.Mips == Mech3.TextureArchive.MipSource.Authored
                ? $"[textures] authored mip levels: {tex.AuthoredMipsInstalled} installed on "
                  + $"{tex.AuthoredMipTextures} texture(s), of {tex.AuthoredMipsAvailable} this "
                  + $"archive ships{refused}"
                : $"[textures] authored mip levels: off (--mips=generated); this archive ships "
                  + $"{tex.AuthoredMipsAvailable}");
        }
        if (state.Textures.MissingTextures.Count > 0)
            GD.Print($"[textures] {state.Textures.MissingTextures.Count} referenced texture(s) absent from this install: " +
                     string.Join(", ", state.Textures.MissingTextures));
    }

    /// <summary>The post-build framing pass, run whether or not the try succeeded is already
    /// decided by the caller: subject framing for the static views, the freecam/anim-lab mesh
    /// lab, the collider wireframe overlay, and the node-name label layer.</summary>
    private void FinishFraming(BuildState state)
    {
        // Only the static views frame their subject; flight and the spectator camera (both
        // --freecam and --anim-lab) place their own eye (FrameCamera would yank the freecam back
        // to the world's AABB orbit).
        if (!_spec.Fly && !_spec.Freecam && !_spec.AnimLab)
            FrameCamera(state.NodeAabb);

        // Mesh lab on the selection (M) — the freecam/anim-lab twin of the viewer's lab above. It
        // owns no subtree until M attaches it to whatever the shared selection has, and restores
        // that subtree exactly when M lets go, so it builds and changes nothing until then.
        if (_selection != null)
        {
            _worldRoot!.AddChild(new UI.MeshLab(_selection, _sun, _env, _camera)
            {
                DebugSpec = _spec.DebugMesh,
                // A scripted capture is about the geometry, not the panel over it.
                ShowPanel = !_captureDirector.Pending,
            });
        }

        // Collider wireframes (C). Built in the modes that observe a live world: it draws what the
        // collision build produced, and says so loudly when the mode built none rather than
        // rendering an empty overlay that reads as "nothing here is solid".
        if ((_spec.Freecam || _spec.AnimLab || _spec.Fly) && _plane != null)
        {
            var planeColliders = new List<(Node3D, PlaneCollider)>();
            foreach (var rig in _rigs)
            {
                // Collider.Parts.Local is expressed in the plane MODEL's parent frame (the
                // FlightController — see PlaneCollider's class doc), not the model's own: the
                // model root carries its own GameZ local transform, so drawing the boxes as its
                // children would apply that transform a second time.
                if (rig.Controller is { Collider: { } airframe, PlaneModel: { } } controller)
                {
                    planeColliders.Add((controller, airframe));
                }
            }
            _worldRoot!.AddChild(new UI.ColliderOverlay(_plane, BuildsCollision)
            {
                DebugShow = _spec.ShowColliders,
                Planes = planeColliders,
                // What each surface id resolves to on contact, asked of this session's own program
                // rather than listed: the overlay colours by the id a touch will select, and which
                // ids have a def of their own is whatever the bound program defines (BL-345).
                ResolvedSurfaceIds = state.CrashProgram != null
                    ? EffectCatalogue.ResolvedSurfaceIds(state.CrashProgram)
                    : null,
            });
            Log.Info("world", $"collider overlay ready (C){(BuildsCollision ? "" : " — but this mode built NO collision; relaunch with --collision")}");

            // Colour-by-class overlay (X): same mode set as the collider overlay, since it reads
            // the same live world — a findable-targets view, not a collision one.
            _worldRoot!.AddChild(new UI.ClassOverlay(_plane, state.Gamez, state.WorldRuntime)
            {
                DebugShow = _spec.ShowClassOverlay,
            });
            Log.Info("world", $"class overlay ready (X)");
        }
        else if (_spec.ForceCollision && _spec.WorldMode)
        {
            // The static viewer builds the bodies but binds no overlay: C there cycles the mesh
            // lab's cull override, and silently rebinding a lab key would be worse than saying so.
            Log.Info("world", $"--collision built the world's colliders, but the C overlay is not bound in this mode (C is the mesh lab's cull cycler) — use --freecam to see them");
        }

        // Map-edge tile grid (F14, with F15/F16 stepping the fold). Gated on
        // the extender rather than on a mode list, because "there is a continuation to colour" is
        // exactly the precondition: the extender is built for --fly/--freecam and for a --sky-zone
        // viewer, and those are the sessions where the overlay has anything to say. Its keys are in
        // the reserved F13-F24 debug range, so binding it in the viewer too cannot collide with a
        // lab key the way a letter would.
        if (_edgeExtender != null && _worldRoot != null && _plane != null)
        {
            _worldRoot.AddChild(new UI.TileGridOverlay(_plane, _edgeExtender)
            {
                DebugShow = _spec.ShowTileGrid,
            });
            Log.Info("world", $"tile-grid overlay ready (F14; F15 block depth, F16 mirror/repeat)");
        }
        else if (_spec.ShowTileGrid)
        {
            Log.Warn("world", $"--debug-tilegrid: this mode builds no map-edge continuation, so there is no tile grid to colour (it exists in --fly, --freecam, and a --sky-zone viewer)");
        }

        // Node-name labels (T) — in BOTH the viewer and flight: reading a misplaced object's
        // name off it as you fly past is the fast way to identify it. Covers the whole session
        // subtree, so it labels the world and the aircraft alike. Off until pressed (and it
        // builds nothing until then), so no screenshot changes. In splitscreen the selection
        // follows P1's camera; the labels themselves render in every pane.
        var flownPlanes = new List<Node3D>();
        foreach (var rig in _rigs)
            if (rig.Controller?.PlaneModel is { } flown)
                flownPlanes.Add(flown);
        _worldRoot!.AddChild(new UI.NodeLabels(_worldRoot!, _rigs.Count > 0 ? _rigs[0].Camera : _camera)
        {
            InitialMode = _spec.DebugNames == null ? UI.NodeLabels.Mode.Off : UI.NodeLabels.ParseMode(_spec.DebugNames),
            // The flown aircraft sits metres from the camera while the world is hundreds of
            // metres away, so without this it wins every label slot. Empty in --viewer, where
            // the parked aircraft IS the subject.
            Deprioritise = flownPlanes,
        });
    }

    /// <summary>The production ground sampler <see cref="RaceGrid"/> probes its slots with: the
    /// world height under a point, or null when the physics space answered nothing there.
    ///
    /// <para>⚠ <b>The empty result is decided here, once, and this is the only place that knows
    /// what it means.</b> A ray finds nothing for two very different reasons — there is genuinely
    /// no collider in that column, or the space has not stepped since the world's bodies entered
    /// the tree. The second was the expected hazard at this call site and it does <b>not</b> bite:
    /// measured on a C1 four-player race, every slot answers and the warning below never fires
    /// (Godot registers a static body with the space on tree entry, so a ray does not need a step
    /// first). The warning is therefore a tripwire for a build order that changes, not a describing
    /// of what happens today — if it ever appears, suspect the probe ran before the world stage was
    /// added rather than an empty map.</para>
    ///
    /// <para>Either way the honest report to the grid is <c>null</c> — "no answer", never a
    /// fabricated height — because the anchor is an authored, flyable point and a made-up
    /// correction would move a race field for no reason. <see cref="RaceGrid"/> then leaves the
    /// field at the spawn data's own altitude and warns that it did. One warning is emitted here
    /// per session, naming the probe, so the log distinguishes "the sampler found nothing" from
    /// "the grid chose not to lift".</para></summary>
    private Func<Vector3, float?> GroundSampler()
    {
        // The probe column. Up first, so a slot fanned into a hillside still finds the surface
        // above it rather than reporting the terrain it is buried in; then far enough down to
        // clear any spawn's height above the deck. Deliberately generous — this is a placement
        // probe run four times at build, not a per-frame cost.
        const float ProbeAbove = 2000f;
        const float ProbeBelow = 20000f;

        bool warned = false;
        return at =>
        {
            if (GetWorld3D()?.DirectSpaceState is { } space)
            {
                // World, NOT WorldAndAircraft: this is a placement pick, and CollisionLayers'
                // own rule is that placement picks stay blind to planes on purpose. It makes no
                // difference today — no aircraft body exists when this runs — but it is what keeps
                // the rule true if a later caller (a respawn, the deferred rolling start) probes
                // while the field is already in the air, where reading planes as solid would stack
                // a grid slot on top of an aircraft.
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    at + (Vector3.Up * ProbeAbove), at - (Vector3.Up * ProbeBelow),
                    CollisionLayers.World));
                if (hit.Count > 0)
                    return ((Vector3)hit["position"]).Y;
            }

            if (!warned)
            {
                warned = true;
                Log.Warn("flight", $"grid ground probe found nothing under a slot (probe {ProbeAbove:0}m up / {ProbeBelow:0}m down, mask=World) — measured, this does not happen at session build, so suspect the probe ran before the world stage was added rather than an empty map; the field keeps the spawn data's own altitude. Reported once per session.");
            }
            return null;
        };
    }

    /// <summary>Creates this session's <see cref="PlayerRig"/>s — one per rendered view.
    /// One player keeps the session's own main-viewport camera and the default visual
    /// layers, so the single-player render path is byte-for-byte what it was. Two or more build
    /// the <see cref="SplitScreen"/> pane rig: the main camera stands down (the panes cover the
    /// screen) and each pane gets its own camera, culling every other player's private
    /// sky/deck/puff layer.</summary>
    private void BuildRigs(int count)
    {
        _rigs.Clear();
        _split = null;
        if (count <= 1)
        {
            _camera.Current = true;
            // ⚠ The main camera is the LAUNCHER's and outlives the session, so it can arrive
            // carrying the last flight's zone gate — two of the three zone bits cleared. Put the
            // whole band back before this session's first frame: WeatherRig.Tick only ever
            // NARROWS the band, and it does not run at all in a mode with no weather rig, so a
            // session that ended above C1's deck would otherwise cull every zone-1 node of the
            // next flight's world for its whole duration. The splitscreen cameras below are built
            // fresh each session and need no reset.
            _camera.CullMask = Mech3.ZoneGate.OpenCullMask(_camera.CullMask);
            _rigs.Add(new PlayerRig { Index = 0, Camera = _camera, HudParent = _worldRoot!, VisualLayer = 0 });
            return;
        }

        _camera.Current = false; // the panes render the world now; nothing draws the main viewport's 3D
        _split = SplitScreen.Build(count, GetViewport());
        _worldRoot!.AddChild(_split);
        for (int i = 0; i < count; i++)
        {
            var view = _split.Views[i];
            var camera = new Camera3D
            {
                Name = $"camera{i + 1}",
                Fov = 62,
                Far = _camera.Far,
                CullMask = SplitScreen.PlayerCullMask(i),
                Current = true,
            };
            view.AddChild(camera);
            _rigs.Add(new PlayerRig
            {
                Index = i,
                Camera = camera,
                Viewport = view,
                HudParent = view,
                VisualLayer = SplitScreen.PlayerVisualLayer(i),
            });
        }
        GD.Print($"splitscreen: {count} panes sharing one world " +
                 $"({(count == 2 ? "stacked top/bottom" : "2×2 grid")})");
    }

    /// <summary>Gives every rig a cloudlayer deck to anchor under its own camera: rig 0 takes the
    /// world's own deck, the rest get copies alongside it, each moved onto its player's visual
    /// layer. Duplicating drops the SceneBuilder instance uniforms (they are RenderingServer
    /// instance state, not plain properties), so they are re-applied from the source.</summary>
    private void AssignCloudDecks(Node3D deck)
    {
        _rigs[0].Deck = deck;
        if (_rigs[0].VisualLayer != 0)
            SplitScreen.SetVisualLayer(deck, _rigs[0].VisualLayer);
        var parent = deck.GetParent();
        for (int i = 1; i < _rigs.Count; i++)
        {
            var copy = (Node3D)deck.Duplicate();
            copy.Name = $"cloud_deck{i + 1}";
            CopyInstanceShaderParams(deck, copy);
            SplitScreen.SetVisualLayer(copy, _rigs[i].VisualLayer);
            parent.AddChild(copy);
            _rigs[i].Deck = copy;
        }
    }

    /// <summary>Rematch from the shared race board (R): every player's zones, clock and
    /// placing cleared, then every plane back to its own spawn — same chapter, aircraft and spawn
    /// points. The board retires itself once the placings are gone. The session owns the planes, so
    /// the restart lands here rather than in the FlightController that read the button.</summary>
    private void RestartRace(StuntRace race)
    {
        GD.Print("stunt race: rematch — fresh clocks and zones for every pilot");
        race.Restart();
        foreach (var rig in _rigs)
            rig.Controller?.Respawn();
    }

    /// <summary>Rematch from the dogfight results board (R): every score and the clock reset, then
    /// every plane back to its own spawn — mirrors <see cref="RestartRace"/> exactly. The board
    /// retires itself once <see cref="VersusMatch.Completed"/> clears.</summary>
    private void RestartMatch(VersusMatch match)
    {
        GD.Print("dogfight: rematch — scores and clock reset for every pilot");
        match.Restart();
        foreach (var rig in _rigs)
            rig.Controller?.Respawn();
    }

    /// <summary>Frames the parked plane in the orbit view. <c>--lookat</c> is a true pivot and is
    /// used verbatim; <c>--direction</c> names only an aim, so a pivot is synthesized on the aim
    /// ray — at the subject's nearest approach when an eye was given, otherwise the subject's own
    /// centre with the eye swung round to that direction. Either way the orbit still ORBITS the
    /// subject: dragging turns around it and the wheel dollies toward it.</summary>
    private void FrameCamera(Aabb? subject = null)
    {
        // The subject box. `subject` is the --node= stage's own, measured before the labs joined the
        // subtree: MeshLab parks three EMPTY overlay meshes at the session origin, which a merge
        // over the live tree folds in — harmless for a plane or a whole world (both already contain
        // the origin) and ruinous for one subtree 7 km out, whose box would stretch back to it.
        var aabb = subject ?? OrbitCamera.MergedAabb(_plane!);
        var pivot = _spec.LookAt;
        if (pivot == null && _spec.CamDir is { } dir)
        {
            if (_spec.CamPos is { } eye)
            {
                float ahead = Mathf.Max((aabb.GetCenter() - eye).Dot(dir), MinOrbitRadius);
                pivot = eye + dir * ahead;
                Log.Info("core", $"orbit pivot from --direction: --lookat={Testing.CaptureDirector.Vec3Arg(pivot.Value)} radius={ahead:0.###}");
            }
            else
            {
                // No eye: keep the AABB pivot and the framing distance Frame derives, and place the
                // eye along the aim — the inverse of OrbitCamera.Update's yaw/pitch to offset.
                _orbit.Pitch = Mathf.Asin(Mathf.Clamp(-dir.Y, -1f, 1f));
                _orbit.Yaw = Mathf.Atan2(-dir.X, -dir.Z);
                Log.Info("core", $"orbit pivot from --direction: subject centre, eye swung to the aim");
            }
        }
        _orbit.Frame(aabb, _spec.CamPos, pivot);
    }

    /// <summary>Steps the consumers whose sim normally rides Godot's physics tick. They return
    /// early from <c>_PhysicsProcess</c> whenever the clock is not realtime (GameClock.PhysicsDt
    /// hands them 0), because a fixed or halted sim cannot be paced by a tick it does not own.
    /// Order is the tree order those callbacks had — the shared projectile pool before the flight
    /// controllers, the weapon lab before the pool it owns — so a round fired this frame behaves
    /// exactly as it did.</summary>
    private void DriveSimSteps(GameClock clock)
    {
        // --crash[=frame]: force every player's crash rig at a fixed sim frame — the only
        // headless trigger for FlightController.Crash(), which a live collision otherwise gates.
        if (_spec.CrashFrame is int crashFrame && !_crashFired && clock.Frame >= crashFrame)
        {
            _crashFired = true;
            foreach (var rig in _rigs)
                rig.Controller?.DebugForceCrash();
        }
        // --debug-scoreboard --vs: one scripted, ATTRIBUTED kill (P1 downs P2) on the first sim
        // step, through the same Downed path a real kill takes — so a screenshot has a real K/D,
        // leader and kill banner to show without scripting an actual shot, and (with --vs-kills=1)
        // a deterministic completed match for the results board. Same single-fire shape as
        // --crash above.
        if (_spec.Versus && _spec.DebugScoreboard && !_versusDebugKillFired && _rigs.Count > 1)
        {
            _versusDebugKillFired = true;
            _rigs[1].Controller?.DebugForceCrash(_rigs[0].Controller?.PlayerIndex);
        }
        for (int i = 0; i < clock.Steps; i++)
        {
            float dt = clock.Dt;
            _incomingFire?.SimStep(dt);   // fires into the pool, so it steps before it
            _projectiles?.SimStep(dt);
            foreach (var rig in _rigs)
            {
                rig.Controller?.SimStep(dt);
            }
            // Generators step before the AI-plane loop below: a spawn appends to _aiPlanes, which
            // must not happen while that list is being enumerated (the new plane ticks next step).
            _generators?.SimStep(dt);
            // AI aircraft step after the player rigs — the tree order their _PhysicsProcess
            // callbacks take on a realtime clock, since they spawn after every rig is built.
            foreach (var ai in _aiPlanes)
            {
                ai.SimStep(dt);
            }
            // The weapon lab has no sim step of its own: it is hosted by player 1's
            // FlightController, which owns the fire clock, and fires into _projectiles above.
            _versus?.Advance(dt);
        }
    }

    /// <summary>Per-build state threaded through StartSession's phase methods — the archives,
    /// world-build outputs and running counts. Local to a single StartSession call; nothing here
    /// is cached across a rebuild.</summary>
    private sealed class BuildState
    {
        public string DataRoot = "", ZrdrPath = "", SoundsPath = "", InterpPath = "",
            MessagesPath = "", PlanesGamezPath = "";
        public bool Mute, DebugCollision;
        public string TexturesPath = "", GamezPath = "", MissionZrdrPath = "";

        public GameZ Gamez = null!;
        public TextureArchive Textures = null!;
        public SoundArchive? Sounds;
        public Dictionary<string, SoundDef>? SoundDefs;
        public Dictionary<string, SoundGroup>? SoundGroups;
        // Set by LoadArchives from the ArchiveIntent (Session/Lab) it opened the archives for —
        // BuildWorldStage's WorldSession.Options carries them through unchanged.
        public bool TexturesOutliveBuild;
        public bool SoundsOutliveBuild;
        // The archives that outlive this build scope in the anim lab (the lab node owns their
        // disposal); a failed build closes them from StartSession's catch instead.
        public TextureArchive? LabTextures;
        public SoundArchive? LabSounds;
        public UI.AnimLab? AnimLabNode;

        public GameZNode? NodeSubtree;
        // The --node= subtree's world-frame box, measured at build time and kept for the framing
        // step at the very end — see FrameCamera on why the live-tree merge is the wrong instrument.
        public Aabb? NodeAabb;

        public int MeshInstances;
        public int Colliders;
        public string What = "";

        public Node3D? CloudDeck;
        public IReadOnlyDictionary<Rid, ArrayMesh>? DeckUndimmedMeshes;
        public AnimProgram? CrashProgram;
        public SceneBuilder? WorldScene;
        public AnimRuntime? WorldRuntime;
    }
}
