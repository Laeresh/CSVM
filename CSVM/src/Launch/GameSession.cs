using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.InstantAction;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.UI.Boards;
using CSVM.UI.Overlays;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The per-launch session node: one aircraft from the player's own extracted game data under orbit
/// controls, or with <c>--fly</c> free flight over the chapter world.
///
/// <see cref="Launcher"/> (Main.tscn's root) owns the bootstrap, the launchscreen and the
/// persistent camera/lighting, and instantiates one of these per launch from the settled
/// <see cref="SessionSpec"/> plus a <see cref="LauncherContext"/>. Menu and CLI share StartSession.
///
/// ⚠ Do not add a Teardown(); return-to-menu is a bare QueueFree and the session subtree frees
/// atomically under <c>_worldRoot</c>. ⚠ Do not parse an arg here: a new flag is a SessionSpec
/// change. Module entry: docs/architecture.md on src/Launch/GameSession.cs.
/// </summary>
public partial class GameSession : Node3D
{
    /// <summary>The cutscene host's node and definition names, in the world build's shape. A
    /// session and the test harness both pass this, so they stand up the same cutscene roots.</summary>
    internal static readonly WorldSession.CutsceneNames CutsceneWorldNames = new(
        CutsceneController.CameraNode, CutsceneController.BarsNode, CutsceneController.IntroAnims);

    // How many near-miss names a failed --node= lookup offers: a usable hint, not a census.
    private const int NodeSuggestCap = 20;

    // Smallest orbit radius a synthesized pivot may sit at, so an aim ray passing behind the
    // subject still leaves something to orbit rather than spinning about the eye.
    private const float MinOrbitRadius = 1f;

    // How long a race's opening count stands on READY before its 3, 2, 1. TUNE at the controls.
    private const float RaceReadySeconds = 2f;

    // Everything this launch settled, parsed and resolved once (see SessionSpec), the command
    // line verbatim, or the launchscreen's pick (SessionSpec.FromMenu). Every consumer below reads
    // it and nothing re-derives a launch setting; the pristine command line stays on the Launcher.
    private readonly SessionSpec _spec;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private readonly int[][]? _menuPads;
    // The panes handed to a spectator when their pilot ran out of lives, so a rerun can take them
    // back. Freed with this node otherwise.
    private readonly List<SpectatorCamera> _spectatorCameras = new();
    // The --screenshot=/--shots=/--frames= state machine, see
    // src/Tooling/CaptureDirector.cs's entry. Process-scoped and owned by the Launcher (which
    // Ticks it); held here for the Pending reads that gate display choices during the build.
    private readonly Tooling.CaptureDirector _captureDirector;
    // One rig per rendered view: its camera plus the camera-anchored copies only it
    // sees (skydome / cloud deck / whiteout). Exactly one entry in single player,
    // wrapping the main-viewport _camera below, so the 1P render path is unchanged.
    private readonly List<PlayerRig> _rigs = new();
    // One rig per SEAT: the panes above first, then one pane-less rig per network guest, in seat
    // order. This is what the seat-indexed systems size themselves by (roster, spawn walk, versus
    // board), while everything that dereferences a camera keeps reading _rigs. Identical to _rigs
    // outside a network match.
    private readonly List<PlayerRig> _seatRigs = new();
    // This session's end of the wire and everything wired onto it (src/Launch/SessionNet.cs).
    // Its link is null outside a network match. It is stepped before each simulation step, so a
    // payload is applied on the step after it arrived.
    private readonly SessionNet _wire;
    // Every pane's camera, bound once right after BuildRigs, the session-owned "what do the
    // cameras see" registry draw rules read instead of `_rigs[0]`/`GetViewport().GetCamera3D()`.
    // ProjectilePool.Viewers takes this same instance; B11/B13 are its next consumers.
    private readonly ViewerSet _viewers = new();

    // Every AI aircraft spawned into this session, stepped by SessionSimulation after the
    // player rigs, freed with the world subtree.
    // Scratch for LockCandidateAircraft, reused so a target-key press allocates nothing.
    private readonly List<Node3D> _lockCandidates = new();
    // scratch: the rigs' controllers plus AiPlanes, rebuilt on every AllAircraft() call
    private readonly List<FlightController> _aircraftScan = new();
    // scratch: the rigs' controllers alone, rebuilt on every HumanAircraft() call
    private readonly List<FlightController> _humanScan = new();
    // scratch: the aircraft plus the placed zeppelins, rebuilt on every GatedObjects() call
    private readonly List<Node3D> _gatedScan = new();
    // scratch: rig camera positions for the edge extender
    private readonly List<Vector3> _focusPoints = new();
    // The persistent rendering nodes, owned by the Launcher and kept across sessions; this node
    // only configures them. The mesh lab steers the sun and ambient, which is why both ride the
    // context rather than staying local to the Launcher's lighting setup.
    private readonly Camera3D _camera;
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); the
    // Launcher creates it once and seeds --yaw=/--pitch= into its initial angles.
    private readonly OrbitCamera _orbit;
    // launched into the menu → the boards' Exit item returns there, not quit
    private readonly bool _menuDriven;
    // The presentation this session's boards take, resolved by the Launcher. The only board that
    // reads it is the pause screen, which the Original presentation composes from escape.zrd.
    private readonly UI.Menu.PresentationId _presentation;
    // The boards' Exit item, routed by the Launcher (the screen this flight was launched from, or
    // quit when nothing launched it).
    private readonly Action _exitSession;
    // The boards' Restart item on an Instant Action or campaign mission: the Launcher frees this
    // session and builds a fresh one. Nothing here can put a mission's opposition back on its own.
    private readonly Action _restartSession;
    // The graphics-mode action every local seat's controller fires; the switch is the Launcher's.
    private readonly Action? _toggleGraphicsMode;
    // A campaign mission's end: the Launcher frees this session and reopens the launchscreen on
    // the named profile's debrief, carrying the result so a page can be opened on it. Null
    // outside a menu-driven process (a --campaign= run from the command line has no cabin to
    // return to and simply stays in the flown world).
    private readonly Action<string, CampaignMissionResult>? _campaignMissionEnded;
    // Where an ended Instant Action mission's final numbers go on a presentation with a wrap-up
    // page of its own, routed by the Launcher. Null leaves the ending to the in-flight board.
    private readonly Action<IaWrapupSnapshot>? _instantActionWrapup;
    // The process's music channel, owned by the Launcher so one channel outlives every session.
    // Handed to CampaignDirector, which is what routes the mission's own music cues into it.
    private readonly MusicPlayer? _music;
    // Base (chapter-independent) paths, settled by the Launcher once per process and handed in via
    // the context; StartSession reads them each build and recomputes the chapter-dependent
    // gamez/texture/mission paths from _spec.Chapter.
    private readonly string _repoRoot;
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game, /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private readonly string _dataRoot;
    private readonly string _planesGamezPath;
    private readonly string _zrdrPath;
    private readonly string _soundsPath;
    private readonly string _interpPath;
    private readonly string _messagesPath;
    private readonly DecodeCache? _decode;
    // the extracted UI archive (paint patterns)
    private readonly string _rofPath;
    // Process-scoped, owned by the Launcher; the --damage-test/--effects-test/--weapon-test/
    // --destroy= probe wrappers below delegate to it (see src/Tooling/ProbeRunner.cs).
    private readonly Tooling.ProbeRunner _probeRunner;
    // Tap-vs-hold timing for the "." step key: a tap steps once (handled directly in
    // _UnhandledInput), and holding past the grace period steps every rendered frame, polled
    // here rather than through key-repeat events, since the grace period is measured on wall
    // time regardless of the sim being halted.
    private readonly HoldToRepeat _stepHold = new(initialDelay: 0.3f, repeatInterval: 0f);

    // The two position snapshots refill these, because every aircraft asks on every sim step.
    // ⚠ A caller reads the answer on the spot and never keeps it, since the next call overwrites
    // it. Arrays, not lists, so a nested call during a caller's foreach cannot throw.
    private Vector3[] _playerPositions = Array.Empty<Vector3>();
    private Vector3[] _fieldPositions = Array.Empty<Vector3>();

    // The master seed every subsystem generator derives from (see Utils.Rng), resolved by the
    // Launcher once per process and re-applied here at each session build. A pinned run takes
    // the spec's value, everything else draws from the clock. Not readonly: a guest replaces it
    // with the host's before the build, which is the whole point of the handshake.
    private ulong _masterSeed;
    // Resolves each player's livery and spawn point against _spec;
    // see src/Session/Roster/LiveryResolver.cs and src/Session/Roster/SpawnPicker.cs.
    private LiveryResolver _liveryResolver = null!;
    private SpawnPicker _spawnPicker = null!;
    // --crash[=frame]: fires once, the frame the sim clock first reaches _spec.CrashFrame.
    private bool _crashFired;
    // --debug-pause[=frame]: fires once, the frame the sim clock first reaches it.
    private bool _debugPauseFired;
    // --debug-wash=N: how many of its two scripted washes have fired, see DriveParentSimulation.
    private int _debugWashesFired;
    // The inspection labs, the parked-plane view, the freecam and the debug overlays
    // (src/Launch/InspectionLabs.cs). One per build.
    private InspectionLabs? _labs;
    // The effect/crash stage factory, builds the world-effects runtime
    // (lazily, on demand for a plane-less session: --destroy, the damage lab's first kill) and
    // each player's crash runtime. Constructed once per session, same lifetime as _liveryResolver.
    private WorldEffectsFactory _worldEffectsFactory = null!;
    // The sky step's output: the weather rig, the cloud field and banks, the decks and the flare
    // (src/Launch/SkyStage.cs). Constructed once per session; its rigs tick in _Process.
    private SkyStage? _sky;
    // The scripted probes and build-time forces (src/Launch/SessionProbes.cs), one per build.
    private SessionProbes? _probes;
    // The world state every puffer in this session reads but none of them owns, the wind and the
    // camera position (see Effects/WorldWind.cs). It is built here, not on the weather rig: the
    // emitter factories need it at StartSession, long before a weathered build exists to write it.
    // WeatherRig.Tick is what fills it in.
    private Effects.EffectAmbience _ambience = new();
    // The FBFX_COLOR_FROM_TO wash, one ramp per rendered view, painted into the pane(s) the burst
    // was near.
    private UI.Boards.ScreenFlash? _screenFlash;
    // The active smoke screens (D18): laid by the SMOKE_SCREEN fire path, walked over every rig
    // and AI plane each sim step, washing humans through _screenFlash and stunning AI pilots.
    private SmokeScreens? _smokeScreens;
    // The beeper tags (B8/B9): the projectile pool tags on a BEEPER hit and asks for a seeker's
    // target; the list itself counts down here, after every aircraft, in both step paths.
    private BeeperTags<FlightController>? _beeperTags;
    private Node3D? _plane;
    // The session's simulation clock (see GameClock). Also published as GameClock.Current for
    // authored animation and the few consumers that read session time; dropped by ReturnToMenu.
    private GameClock? _clock;
    // The sole owner of haltable session advancement. Both clock adapters request its Step.
    private SessionSimulation? _simulation;
    // Combat owners retained by this orchestrator and advanced through SessionSimulation.
    private ProjectilePool? _projectiles;
    private IncomingFire? _incomingFire;   // --incoming: the incoming-fire test rig
    // The flight roster builds the human field and introduces AI aircraft later. Every AI it
    // returns is stepped by SessionSimulation after the player rigs and freed with the world.
    private FlightRoster? _flightRoster;
    // The mission radio, the E16 combat voice and the skills table they read
    // (src/Launch/SessionVoices.cs). Built with the rigs; both runtimes step in SessionSimulation.
    private SessionVoices? _voices;
    // The egen enemy generators (--generators): loaded with the rigs, stepped in
    // SessionSimulation before the AI planes it spawns into AiPlanes, freed with the world subtree.
    private AiGeneratorRuntime? _generators;
    private ZeppelinRuntime? _zeppelins;
    // The mission's surface vehicles (a mode ship roster block, a boat generator's launch):
    // built with the roster on a chapter world, stepped after the generators that launch them.
    private SurfaceVehicleRuntime? _surfaceVehicles;
    // The active Instant Action mission's director: the mission runtime, the wave state and (as
    // the deepening proceeds) the sequencing (see InstantActionDirector). Built at the top of
    // StartSession, null outside a mission, which is what keeps every other session mode
    // (free flight, Dogfight) untouched by its existence.
    private InstantActionDirector? _iaDirector;
    // The active campaign mission's director: the objectives graph, its world seam and the mission
    // end/return flow (see CampaignDirector). Built at the top of StartSession alongside the
    // Instant Action one, null outside a --campaign= launch.
    private CampaignDirector? _campaign;
    // The name the HUD kill line prints when the player is the one shot down. It is the flying
    // campaign profile's, and outside a campaign the profile last used. That is where the
    // original's startup takes its PlayerName from (docs/org/vehicleDamage.md "The kill message").
    // ⚠ The second arm is refused under --det. A record of who played last is machine state, and a
    // pinned run reads none of it. No profile on disk leaves the line its unnamed fall-through.
    private string? _pilotName;
    // The cutscene host: built for any flown chapter session, since a story mission's intro
    // definition starts itself out of startanims and needs its CALLBACK codes hosted from the
    // bootstrap on. Null everywhere else, which leaves the world build's node census untouched.
    private CutsceneController? _cutscene;
    // Seat 1's stick skip beside the cutscene host, since a stick raises no input event for
    // _UnhandledInput to hear. Polled every frame so a press is an edge, not a held trigger.
    private Sticks.StickSkip? _stickSkip;
    // The landings.zrd approach trigger, built and bound alongside the cutscene host it feeds.
    private LandingApproachRuntime? _landings;
    // The rope-ladder switch, bound with the landings trigger off the same pickup sensors.
    private LadderSwitchRuntime? _ladder;
    // The world AA emplacements: built with the rigs whenever a chapter world and the
    // shared pool exist, stepped by SessionSimulation after the zeppelins (slung mounts read the
    // moved pose). Shipped ACTIVATED honoured; --wake-turrets is the WAKEUP_TURRETS stand-in.
    private TurretEmplacementRuntime? _turretEmplacements;
    // The Dogfight's match, rotation, lives and team modes (src/Session/World/VersusDirector.cs),
    // built ahead of the roster. Null outside --vs, so the Downed events have no scorer.
    private VersusDirector? _dogfight;
    // The stunt race (--stunt with several pilots), for the same reason: a rerun resets it rather
    // than each pilot's own run. Null outside a race.
    private StuntRace? _race;
    // Who is holding the sim clock and why, shared by every rig and by every board that halts.
    // Null before the rigs exist.
    private PauseState? _pauseState;
    // The whole-window boards, their menu readers, the pause options leaf and photo mode
    // (src/Launch/SessionBoards.cs). Null before the rigs exist and in a mode that flies none.
    private SessionBoards? _boards;
    // The options leaf the pause board stands under PREFERENCES. The leaf is the Launcher's to
    // build (only it holds the decoded layout and the options writer); null leaves no door.
    private Func<UI.Screens.PausePreferences?>? _pauseOptionsFactory;
    // The trailer-target resolver every net follower this session builds shares. Built
    // with the rigs (it needs the player rig), so the F13 overlay, built earlier, reads it through
    // this field rather than holding a reference it could not have had yet.
    private NetTrailerTargets? _netTrailers;
    // rolling mirrored-tile window past the map edge
    private Mech3.MapEdgeExtender? _edgeExtender;
    // the static world's merged draws under Enhanced (null outside flight)
    private Mech3.WorldMerge? _worldMerge;
    // the splitscreen pane rig (null in single player)
    private UI.Boards.SplitScreen? _split;

    // Session lifecycle (the launchscreen's in-process world rebuild): everything a
    // session builds hangs under _worldRoot, so Esc-to-menu can free it and a new session node
    // build again. The camera, lights and global shader params live on the Launcher and persist.
    private Node3D? _worldRoot;
    // the session's LIGHT_STATE point lights (see WorldLights)
    private WorldLights? _worldLights;
    // The faithful path's projected aircraft shadow, null in enhanced mode, which casts shadow maps
    // instead. Held so a live graphics-mode switch can build it or free it.
    private GroundShadowPass? _groundShadows;
    // The spyglass discs' shadowless sun, Enhanced flight only; held so a live switch can build or
    // free it (FollowSpyglassSun).
    private Flight.Camera.SpyglassSun? _spyglassSun;
    // The enhanced-only world layers and the mode-dependent builds, held so a live graphics-mode
    // switch can build, free or rewrite each (ApplyGraphicsMode). Null where the build made none.
    private Effects.ScorchField? _scorches;
    private Effects.Ocean? _ocean;
    private bool _oceanEligible;
    // The chapter's saved sea, and the one the ocean draws: the saved one under any --debug-ocean
    // overrides and the ocean lab's edits. An ocean rebuilt by a live switch takes the drawn one.
    private Effects.SeaState _seaSaved = Effects.SeaState.Default;
    private Effects.SeaState _sea = Effects.SeaState.Default;
    // The world runtime whose motions say which hulls the ocean follows; set in every mode it builds in.
    private AnimRuntime? _oceanRuntime;
    private ClutterBuilder? _clutter;
    private SceneBuilder? _worldScene;
    // The session-owned texture archive, kept open past the build scope so the data-driven crash can
    // bake its effect puffers lazily at crash time (the same reason --anim-lab keeps it open, but that
    // path hands it to the AnimLab node instead). Disposed by ReturnToMenu on teardown so a map reload
    // drops the previous archive instead of leaking it. Null in the anim-lab (lab-owned) case.
    private TextureArchive? _sessionTextures;
    // The world whose origin-parked entities are still being watched, and the poll accumulator.
    // See WorldBuilder.HideUnplacedEntities / RestorePlacedEntities.
    private WorldBuilder? _unplacedWatch;
    private double _unplacedRecheck;
    // This session's startup timing, the always-on [perf] startup line. One per StartSession,
    // published as StartupProfile.Current so the shared build code can record into it, and cleared
    // when the line is emitted.
    private StartupProfile? _startup;
    // The chapter world's animation runtime, kept past the build for the F17 kill key and a guest's
    // objective catch-up. Null on a stage without one.
    private AnimRuntime? _worldRuntime;

    /// <summary>Constructs the session node for one launch. <paramref name="spec"/> is what this
    /// session is built from (the command line verbatim, or the launchscreen's pick);
    /// <paramref name="ctx"/> carries the Launcher's settled paths, the persistent rendering
    /// nodes, the process-scoped services and the join flow's pad binding. The caller adds the
    /// node to the tree and then runs <see cref="StartSession"/>.</summary>
    public GameSession(SessionSpec spec, LauncherContext ctx)
    {
        // The session clock is advanced at the top of this node's _Process, and every sim consumer
        // reads it during the same frame, so this node has to tick first. Godot runs the lowest
        // priority first (the Launcher sits one notch behind at -999).
        ProcessPriority = -1000;
        Name = "GameSession";
        // ⚠ Resolved here, before any consumer reads Chapter/Mission: a --campaign= launch names a
        // story position, not a chapter, and every path below is derived from those two fields.
        _spec = CampaignDirector.ResolveSeatedPlane(
            ResolveCampaignZeppelins(CampaignDirector.ResolveSpec(spec, ctx.ZrdrPath), ctx.DataRoot));
        _repoRoot = ctx.RepoRoot;
        _dataRoot = ctx.DataRoot;
        _planesGamezPath = ctx.PlanesGamezPath;
        _zrdrPath = ctx.ZrdrPath;
        _soundsPath = ctx.SoundsPath;
        _interpPath = ctx.InterpPath;
        _messagesPath = ctx.MessagesPath;
        _decode = ctx.Decode;
        _rofPath = ctx.RofPath;
        _probeRunner = ctx.ProbeRunner;
        _captureDirector = ctx.CaptureDirector;
        // A guest's seed is the host's, taken here so it is in place before Build calls Rng.Reset.
        // Both peers then draw the same liveries, the same spawn walk and the same dice.
        _masterSeed = ctx.NetHandshake?.Seed ?? ctx.MasterSeed;
        // The wire, opened here rather than at the build. A host must be able to answer a join
        // before its own world stands, and a guest has nothing to build from until it has.
        _wire = new SessionNet(ctx, _masterSeed, _rigs, _seatRigs, () => _clock?.Time ?? 0.0)
        {
            TraceSteps = _spec.DebugNetTrace,
        };
        _camera = ctx.Camera;
        _orbit = ctx.Orbit;
        _sun = ctx.Sun;
        _env = ctx.Env;
        _menuDriven = ctx.MenuDriven;
        _presentation = ctx.Presentation;
        _menuPads = ctx.MenuPads;
        _exitSession = ctx.ExitSession;
        _restartSession = ctx.RestartSession;
        _toggleGraphicsMode = ctx.ToggleGraphicsMode;
        _pauseOptionsFactory = ctx.PauseOptions;
        _campaignMissionEnded = ctx.CampaignMissionEnded;
        _instantActionWrapup = ctx.InstantActionWrapup;
        _music = ctx.Music;
    }

    /// <summary>Whether the build completed, the Launcher's Esc routing reads it (return to the
    /// launchscreen only once a world is actually up).</summary>
    public bool InSession { get; private set; }

    /// <summary>Whether the wave ocean stands in this session's world.</summary>
    public bool OceanBuilt => _ocean != null;

    /// <summary>The static world's scene builder, null outside a built world. Read by the suites.</summary>
    internal SceneBuilder? WorldScene => _worldScene;

    /// <summary>Has the session drawn the first frame the player is meant to see. That frame is
    /// an intro's camera posed onto the rigs, or the flown aeroplane on its spawn under its own
    /// HUD. It latches at the end of the frame that reaches it, and it is what the start cover
    /// holds for. No frame between the load screen and the mission then shows the world still
    /// assembling.</summary>
    internal bool FirstFrameReady { get; private set; }

    /// <summary>The session's per-player rigs, the Launcher's F11 placement print reads them.</summary>
    internal List<PlayerRig> Rigs => _rigs;

    /// <summary>The map-edge continuation, null where the world has none. Read by the suites.</summary>
    internal Mech3.MapEdgeExtender? EdgeExtender => _edgeExtender;

    /// <summary>The static world's merged draws, null outside flight. Read by the suites.</summary>
    internal Mech3.WorldMerge? WorldMerge => _worldMerge;

    /// <summary>The world's clutter, null where it has none. Read by the suites.</summary>
    internal ClutterBuilder? Clutter => _clutter;

    /// <summary>The launch as this session resolved it. A campaign launch settles its chapter and
    /// mission here, out of the story position, so the Launcher's own copy never names them.</summary>
    internal SessionSpec Spec => _spec;

    /// <summary>One rig per seat: the panes, then one pane-less rig per remote pilot, in seat
    /// order. Identical to <see cref="Rigs"/> outside a network match.</summary>
    internal IReadOnlyList<PlayerRig> SeatRigs => _seatRigs;

    /// <summary>The human field a campaign director reads, in seat order: the same list its
    /// <c>Humans</c> input returns. Refilled per read, so a caller must not hold it.</summary>
    internal IReadOnlyList<FlightController> HumanField => HumanAircraft();

    /// <summary>This session's end of the wire and its links (<see cref="SessionNet"/>). A suite
    /// reads the link's counters, the chat, the clock and the start gate through it.</summary>
    internal SessionNet Wire => _wire;

    /// <summary>The master every stream in this session was derived from, which on a guest is the
    /// host's.</summary>
    internal ulong MasterSeed => _masterSeed;

    /// <summary>The session's texture archive, for an instrument reading its textures.</summary>
    internal TextureArchive? SessionTextures => _sessionTextures;

    /// <summary>The spyglass discs' shadowless copy of the sun, null outside an Enhanced flight.
    /// Read by the suite.</summary>
    internal Flight.Camera.SpyglassSun? SpyglassSun => _spyglassSun;

    /// <summary>The whole match's roster in seat order: a network match's, or a local match's with
    /// bots. Empty in any other session.</summary>
    internal IReadOnlyList<Net.NetSeat> NetSeats => _wire.Seats;

    /// <summary>The Dogfight's director, null outside <c>--vs</c>: the match, its team modes and
    /// the spawns granted over the wire.</summary>
    internal VersusDirector? Dogfight => _dogfight;

    /// <summary>The stunt race, null outside one. The launcher lands an ended network race's table
    /// in the lobby from it.</summary>
    internal StuntRace? Race => _race;

    /// <summary>The mission's zeppelins, null in a flight that runs none.</summary>
    internal ZeppelinRuntime? ZeppelinHulls => _zeppelins;

    /// <summary>Each seat's opening entry in a team match's whole spawn table, null outside one.
    /// </summary>
    internal IReadOnlyList<int>? TeamOpenings => _spawnPicker?.SeatEntries;

    /// <summary>Whether the flight is built but waits for every machine to load. The launcher keeps
    /// its load screen up for as long as this holds.</summary>
    internal bool StartHeld => _clock is { StartHeld: true };

    /// <summary>What is holding this session's world, null before the build. A results board's
    /// wake raises <see cref="PauseState.Ended"/> here, so this is where a suite reads whether the
    /// wrap-up board is holding a machine.</summary>
    internal PauseState? Pause => _pauseState;

    /// <summary>Whether the pause sheet offers Restart. A network guest's sheet does not: the
    /// flight is its host's, which restarts it for every machine.</summary>
    internal bool RestartOffered => _wire.Link is null or { IsHost: true };

    /// <summary>The whole-window boards over this flight, null in a session that flies none. A
    /// suite reads the dogfight board and the pause board's Restart through them.</summary>
    internal SessionBoards? Boards => _boards;

    /// <summary>This session's own sim clock. Two sessions in one process share one
    /// <see cref="GameClock.Current"/>, so a suite driving both reads each end's here.</summary>
    internal GameClock? SimClock => _clock;

    /// <summary>The campaign mission's director, null outside a <c>--campaign=</c> launch. The
    /// session layer reads its <see cref="CampaignDirector.ReturnToCabin"/> to know the mission is
    /// over and the player belongs back in the cabin.</summary>
    internal CampaignDirector? Campaign => _campaign;

    /// <summary>The AI combat voice, null when the session built no sound defs. The harness suites
    /// raise and count its call-outs.</summary>
    internal AiVoiceRuntime? AiVoice => _voices?.Voice;

    /// <summary>The cutscene host, null outside a flown world. A suite drives its airframe swap
    /// seam the way a replayed definition's code does.</summary>
    internal CutsceneController? Cutscene => _cutscene;

    /// <summary>A suite's control: an airframe swap leaves the replacement off the wire, which is
    /// how a swapped pilot's reports stop crossing.</summary>
    internal bool SkipSwapRewire { get; set; }

    /// <summary>The mission's enemy generators, null on a stage without them. The harness suites
    /// credit them and read their launch counters.</summary>
    internal AiGeneratorRuntime? Generators => _generators;

    /// <summary>The Instant Action mission's runtime, null outside one. A suite reads that a lobby
    /// Stunt Race flies the course with no mission behind it.</summary>
    internal InstantActionRuntime? InstantAction => _iaDirector?.Runtime;

    /// <summary>How many AI aircraft the flight roster holds now.</summary>
    internal int AiAircraftCount => AiPlanes.Count;

    /// <summary>The session's subject plane (null until the build lands one), the Launcher's
    /// capture tick reads it, because CaptureDirector only shoots once a plane exists.</summary>
    internal Node3D? Plane => _plane;

    private IReadOnlyList<FlightController> AiPlanes =>
        _flightRoster?.AiAircraft ?? Array.Empty<FlightController>();

    // ⚠ Do not spell "does this session build colliders" any other way. The labs and the F20
    // overlay read this one definition, so they cannot disagree with what WorldSession built.
    private bool BuildsCollision => _spec.BuildsCollision;

    /// <summary>Builds one flight/view session from the spec (mode, chapter, plane, spawn, …)
    /// into a fresh <see cref="_worldRoot"/> so Esc-to-menu can tear it all down and a new session
    /// node build again, the launchscreen's in-process world rebuild. The camera, lights and
    /// global shader params live on the Launcher and persist across sessions. Called by the
    /// Launcher once this node is in the tree. Returns true on success; false (leaving the partial
    /// _worldRoot for the caller to free) when the build threw.</summary>
    public bool StartSession()
    {
        // Phase two of a guest's start, before anything below draws or sizes itself by the field.
        // A guest's seed is the host's, in place before Rng.Reset below.
        if (!_wire.AwaitJoin())
        {
            return false;
        }

        _masterSeed = _wire.JoinedSeed ?? _masterSeed;
        _wire.WireClock();
        _wire.WireStartGate();
        // Published as the ambient Current so WorldSession, which the test harness also drives with
        // no session around it, can record its phases blind.
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
            _ambience, PlayerPositionsSnapshot, AnyPilotFirstPerson);
        _probes = new SessionProbes(_spec, _probeRunner, _worldRoot, code => GetTree().Quit(code));
        _sky = new SkyStage(_spec, _worldRoot, _camera, _sun, _env, _ambience, _viewers, _rigs, GatedObjects);
        _labs = new InspectionLabs(new InspectionLabs.Inputs
        {
            Spec = _spec,
            WorldRoot = _worldRoot,
            Camera = _camera,
            Sun = _sun,
            Env = _env,
            Ambience = _ambience,
            CapturePending = () => _captureDirector.Pending,
            LockCandidates = LockCandidateAircraft,
            BuildsCollision = BuildsCollision,
        });
        // Re-derive every subsystem RNG from the master before anything draws, so this session's
        // content is a function of its master alone rather than of how long the previous one ran.
        // The launcher decides that master: held for a pinned run, stepped per sortie otherwise.
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        // One simulation clock per session. --det pins a fixed step, the animation lab is fixed-dt
        // by nature, everything else runs at the wall delta.
        _clock = new GameClock
        {
            Mode = _spec.Det || (_spec.AnimLab && _captureDirector.Pending) ? GameClock.RunMode.FixedStep
                : _spec.AnimLab ? GameClock.RunMode.FixedAccum
                : GameClock.RunMode.Realtime,
        };
        GameClock.Current = _clock;
        LoadProgress.Report(LoadStep.Scaffold);
        // World cameras outside the cockpit draw at the decoded base, and the viewer's 50 frames
        // a model. A free camera's own angle option replays a cockpit view an F11 line printed.
        _camera.Fov = _spec.Fov ?? (_spec.Fly || _spec.Freecam || _spec.AnimLab
            ? CameraController.ExternalFovDeg : 50f);
        // One rig per rendered view, before anything camera-anchored is built (the skydome and
        // weather visuals below are per-rig). Single player reuses the main-viewport camera.
        BuildRigs(_spec.Fly ? _spec.Players : 1);
        _wire.BuildSeatRigs(_worldRoot);
        // ⚠ Do not let a draw-rule consumer re-derive its camera set from _rigs; every one shares
        // this single registration so they cannot disagree about what the cameras see.
        _viewers.Bind(_rigs.Count > 0 ? _rigs.Select(r => r.Camera)
            : _camera != null ? new[] { _camera } : System.Array.Empty<Camera3D>());
        // The FBFX_COLOR_FROM_TO wash: one ramp per rendered view, built as soon as the rigs exist
        // so every runtime below takes the same sink. The viewer set goes with it because the
        // routing rule is per pane, and one rig list builds both index-aligned.
        _screenFlash = UI.Boards.ScreenFlash.Build(_rigs.Select(r => r.HudParent), _viewers);
        _worldRoot!.AddChild(_screenFlash);
        _worldEffectsFactory.ScreenFlash = _screenFlash.Play;
        // The smoke screens ride the same wash and read the roster through a closure, since the
        // rigs and the AI planes are both built later than this. player.json missing falls back
        // to the static image's tunables with a warning rather than aborting the launch.
        SmokeScreenTunables smokeTunables;
        try
        {
            smokeTunables = SmokeScreenTunables.Load(_zrdrPath);
        }
        catch (Exception e)
        {
            Log.Warn("flight", $"smoke screen: player.json unavailable ({e.Message}), flying on the image defaults");
            smokeTunables = SmokeScreenTunables.Image;
        }
        var screenFlashSink = _screenFlash;
        _smokeScreens = new SmokeScreens(smokeTunables, AllAircraft, (playerIndex, colour, weight, duration) =>
        {
            // A guest's seat index is not its pane index, so the wash finds the pane by its pilot.
            for (int i = 0; i < _rigs.Count; i++)
            {
                if (_rigs[i].Controller?.PlayerIndex == playerIndex)
                    screenFlashSink.PlayBlend(i, colour, weight, duration);
            }
        });
        // A fresh list per build: a tag never outlives the session that painted it.
        _beeperTags = new BeeperTags<FlightController>();
        LoadProgress.Report(LoadStep.Rigs);

        // The chapter-dependent paths are recomputed here so a new launchscreen chapter selection
        // takes effect on rebuild, and ride BuildState so no phase method re-derives them.
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
        // --zep= grafts one chapter node onto the empty stage, so it needs that chapter's gamez
        // where the bare stage needs only planes.zbd. Chapter is already the flag's own by here.
        state.GamezPath = _spec.Gamez
            ?? (_spec.WorldMode || _spec.Zep != null
                ? SessionPaths.ChapterGamez(_dataRoot, _spec.Chapter)
                : _planesGamezPath);
        state.MissionZrdrPath = SessionPaths.MissionZrdr(_dataRoot, _spec.Chapter, _spec.Mission);
        LoadProgress.Report(LoadStep.Paths);

        // A fresh director per build, or null: construction (and its one-InstantActionRuntime ⚠)
        // lives on InstantActionDirector.TryCreate.
        _iaDirector = InstantActionDirector.TryCreate(_spec);
        // The campaign's sibling, on the same "a load failure flies without a mission" contract.
        _campaign = CampaignDirector.TryCreate(_spec, _zrdrPath, state.MissionZrdrPath, _wire.CoopWingman);
        if (_campaign is { } campaign)
        {
            // The mission's own WAKEUP_SOUND_GROUP is what cues every campaign track, so the
            // channel is handed over rather than driven from here; the end event is this
            // session's cue to hand the player back to the cabin.
            campaign.Music = _music;
            campaign.MissionEnded += OnCampaignMissionEnded;
            // Losing the aircraft loses the mission; --no-crash-loss keeps the debugging
            // convenience of flying on past a crash.
            campaign.EndsOnPlayerDeath = !_spec.NoCrashLoss;
        }

        // Resolved once here rather than per death: a kill must not reach the disk.
        _pilotName = _campaign?.PilotName
            ?? (_spec.Det ? null : CampaignProfileStore.ForSession(_spec.ProfilesDir).LastPlayedPilotName);

        // The cutscene host, before the world build hands it to the animation runtime. Its world
        // hold stops the objectives update as well as the per-step world update (callback 20).
        _cutscene = _spec.Fly && _spec.WorldMode ? new CutsceneController() : null;
        if (_cutscene != null)
        {
            // The clock carries the authoritative hold into SessionSimulation; authored animation
            // reads the same fact but remains outside the held session step.
            _cutscene.WorldHeld = held =>
            {
                if (_clock != null)
                {
                    _clock.SimHeld = held;
                }

                _campaign?.HoldForCutscene(held);
                // The handoff (held -> false) is also when a --pos= this cutscene made
                // BuildFlightRigs withhold gets applied, so the intro's own progression tested the
                // player's pose against the authored spawn it expected all along.
                if (!held)
                {
                    ApplyDeferredSpawnOverride();
                }
            };
            // Read live, not captured: the pane rig is built later in this same build, and an
            // intro's first code lands in the animation bootstrap before it exists (BindRigs
            // re-raises this for that episode).
            _cutscene.FillsWindow = fills => _split?.Fill(fills);
            AddChild(_cutscene);
            // Primed now, so a trigger still held from the menu press that launched the mission
            // cannot skip its intro.
            _stickSkip = Sticks.StickSkip.Live();
            _stickSkip.Prime();
            // The mid-mission cutscene trigger, hosted by the same controller. ⚠ Story missions
            // only: C3/IA1 carries hooked_to_klondike with its approach armed, so an Instant
            // Action sortie would take a docking cutscene (WorldSession.Options.LandingTriggers).
            if (_campaign != null)
            {
                _landings = new LandingApproachRuntime();
                AddChild(_landings);
                _ladder = new LadderSwitchRuntime();
                AddChild(_ladder);
            }
        }

        LoadProgress.Report(LoadStep.Directors);

        Stopwatch sw;
        try
        {
            sw = Stopwatch.StartNew();
            ShaderTwins.ReleaseUnused();
            LoadArchives(state);
            LoadProgress.Report(LoadStep.Archives);
            // The SOUND archive is scoped to this build everywhere but the lab, whose node owns its
            // disposal. ⚠ Do not scope the TEXTURE archive here: the world runtime bakes puffers
            // all session, so LoadArchives hands it to the session instead.
            using var soundsScope = _spec.AnimLab ? null : state.Sounds;
            if (!ResolveNodeSubtree(state))
                return false;
            if (_spec.EmptyStage && _spec.Zep != null)
            {
                // The graft goes through the chapter-world path with NodeSubtree set: one subtree,
                // no MissionSetup and no clutter, but a real AnimRuntime, so the damage, targeting
                // and sub-part feeds bind to the airship as they do to a mission's own.
                if (!BuildWorldStage(state))
                    return false;
                AttachEmptyStageGrid(state);
            }
            else if (_spec.EmptyStage)
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
                _plane = _labs!.BuildParkedPlane(state, _liveryResolver);
            }
            if (!AttachPlaneAndLabs(state))
                return false;
            _sky?.AttachDeck(state);
            _labs!.BuildFreecam(state, _spawnPicker);
            LoadProgress.Report(LoadStep.WorldStage);
            if (_spec.Fly)
            {
                BuildFlightRigs(state);
                ApplyDebugSpectate();
                // AFTER the rigs, like the spectate override. A cutscene that started during the
                // world build has nothing to hide until they exist. On a network guest the scripted
                // player is the host's seat 0, not this pane.
                _cutscene?.BindRigs(_rigs, () => AiPlanes,
                    _wire.Seats.Count > 0 && _seatRigs.Count > 0 ? _seatRigs[0] : null);
                if (_cutscene != null)
                {
                    // The original parks the AI at the start of a mission of every type, before
                    // its start list runs. The park comes from the bootstrap definition. Missions
                    // only: free flight has no counterpart, so nothing outside a mission is held.
                    if (_iaDirector != null || _campaign != null)
                    {
                        _cutscene.ParkAtMissionStart();
                    }

                    _cutscene.SwapAirframe = SwapPlayerAirframe;
                    // The docking's own ending. Instant Action has no objectives graph to complete,
                    // and its rows never raise the code, so an unbound seam is the right answer
                    // there rather than a guarded one here.
                    _cutscene.MissionComplete = () => _campaign?.Graph?.NotifyDockingComplete();
                    // The result is banked the frame the ending lands, which is the frame the
                    // leaving hold and its fade start on, so this reads true for every ending that
                    // landed under a film whatever raised it.
                    _cutscene.EndingLanded = () => _campaign?.Result != null;
                    // Read through the field rather than captured: the pool is built by the world
                    // phase, which a lab or bench session skips entirely.
                    _cutscene.ClearOrdnance = () => _projectiles?.Clear();
                }
            }
            LoadProgress.Report(LoadStep.PlayerRigs);
            _probes!.Destroy(state, _worldEffectsFactory, _labs?.Spectator);
            _probes.ForceObjective(state, _campaign);
            LogBuildSummary(state, sw);
        }
        catch (Exception e)
        {
            Log.Error("core", $"failed to load session: {e}");
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
        _oceanEligible = state.NodeSubtree == null && (_spec.Fly || _spec.Freecam);
        _oceanRuntime = state.WorldRuntime;
        ResolveSea();
        FollowOcean();
        if (_oceanEligible && Effects.Ocean.Covers(_spec.Chapter))
            _labs!.BuildOceanLab(_seaSaved, _sea, () => _ocean, ApplySea);
        BuildWorldMerge(state);
        // By default only once this process has switched. The warm-up costs what one switch does,
        // and a player who never switches would pay it at every load.
        if (_spec.ShaderWarmup == "load" || (_spec.ShaderWarmup == "auto" && EnhancedLook.HasSwitched))
            WarmShadersNow();
        _simulation = new SessionSimulation(new SessionSimulationRuntime(this));
        _startup?.EndBuild();
        InSession = true;
        _wire.HoldStart(_clock);
        LoadProgress.Report(LoadStep.Finished);
        return true;
    }

    /// <summary>Follow a live graphics-mode switch the launcher has already applied to the shaders,
    /// the sun, the Environment and the clutter fade. A layer only one mode builds is built or
    /// freed, and every build-time choice is made again. The zone is lit last, under the other arm.
    /// docs/architecture/Spec.md lists what follows and what waits for the next load.</summary>
    public void ApplyGraphicsMode()
    {
        bool enhanced = GraphicsMode.Enhanced;
        // First: every step below that builds anything bakes from the archive's textures.
        EnhancedLook.FollowAlphaDepth(_sessionTextures);
        SwitchProfile.Mark("alpha");
        _worldScene?.FollowGraphicsMode();
        SwitchProfile.Mark("world");
        // After the world's own materials, whose blend verdicts decide what merges.
        _worldMerge?.Follow(enhanced);
        SwitchProfile.Mark("merge");
        FollowOcean();
        _clutter?.Recut();
        _edgeExtender?.FollowClutterFade();
        SwitchProfile.Mark("clutter");
        _sky?.CloudField?.FollowGraphicsMode();
        SwitchProfile.Mark("cloudfield");
        _worldLights?.FollowGraphicsMode();
        SwitchProfile.Mark("lights");
        _worldEffectsFactory?.FollowGraphicsMode();
        SwitchProfile.Mark("effects");
        foreach (var rig in _rigs)
        {
            if (rig.Controller?.Dressing.Pass?.Env is { } env)
                EnhancedLook.ApplyEnvironment(env, enhanced, _spec.SkippedPasses);
        }
        SwitchProfile.Mark("cockpit");
        FollowWindStreaks();
        SwitchProfile.Mark("streaks");
        FollowSun();
        SwitchProfile.Mark("zone");
        if (enhanced)
        {
            Drop(_groundShadows);
            _groundShadows = null;
        }
        else if (_groundShadows == null && _worldRoot != null && _projectiles != null)
        {
            BuildGroundShadows();
        }
        FollowSpyglassSun();
        if (_plane != null && BuildsCollision)
        {
            if (enhanced && _scorches == null && Effects.ScorchField.Create() is { } scorches)
            {
                _scorches = scorches;
                _plane.AddChild(scorches);
            }
            else if (!enhanced && _scorches != null)
            {
                Drop(_scorches);
                _scorches = null;
            }
        }
        SwitchProfile.Mark("shadows");
    }

    /// <summary>A live View Distance change under Enhanced cuts the clutter cells again. Their size
    /// and visibility range come from the fade scale the launcher just wrote, as do the map edge's
    /// copies' ranges.</summary>
    public void FollowClutterFade()
    {
        _clutter?.Recut();
        _edgeExtender?.FollowClutterFade();
    }

    /// <summary>A live Water Quality change builds or drops the wave ocean
    /// (<see cref="EnhancedLook.ApplyWaterQuality"/>). The graphics-mode switch follows it too.</summary>
    public void FollowWaterQuality() => FollowOcean();

    /// <summary>After the launcher re-dressed the session sun: each cockpit pass re-takes it, then
    /// the zone is written again over them all. ⚠ Keep the zone last. The Environment and sun
    /// writes put back defaults the zone's sky colour, energies and shadow distance overwrite.
    /// </summary>
    public void FollowSun()
    {
        foreach (var rig in _rigs)
            rig.Controller?.Dressing.Pass?.FollowSun();
        _sky?.Weather?.ReapplyZone();
    }

    public override void _Notification(int what)
    {
        // ⚠ Add only NON-CHILD teardown duties here. The whole session subtree hangs under
        // _worldRoot and frees atomically with this node, so a manual null-out is dead code.
        if (what == (int)NotificationExitTree)
        {
            _flightRoster?.ClearMembership();
            // ⚠ Release the lobby's carrier here. A relaunch binds the next session on it, and a
            // carrier still held throws there and leaves the load screen up.
            _wire.Link?.Release();
            // A run that quits inside the session build (the headless probes) never renders a
            // frame, so this is the only place its startup breakdown can still be reported.
            // Idempotent: a session that did render has already emitted and this does nothing.
            _startup?.Emit();
            // A static pointer, not a child: null it so a node outliving this teardown falls back
            // to its raw frame delta. The menu relaunch is a frame later, so the two never race.
            GameClock.Current = null;
            // Beside the clock, and for the same reason: a static book of this world's nodes would
            // otherwise be the next session's starting membership.
            RenderPoses.Clear();
            // Clears csky_light_count so the next world does not inherit this one's lights;
            // idempotent, and null-guarded (a failed build never set it).
            _worldLights?.Dispose();
            _worldLights = null;
            // The same for the per-view fog table, whose eyes are this session's cameras.
            FogViewTable.Clear();
            // Not a node, so QueueFree cannot reach it. Null-guarded: a failed build already
            // disposed and nulled it, so there is no double free.
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            // Restore the persistent (Launcher-owned) main camera: splitscreen stood it down while
            // the panes rendered, and the launchscreen and the next session expect it current.
            _camera.Current = true;
            // The sun is Launcher-owned too, so a splitscreen session's pane default leaves with it.
            FollowPaneShadows(1);
            // A line still open when the session ends leaves no field for the keyboard to type into.
            ScreenKeyboard.Hide(SessionNet.ChatKeyboardOwner);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Ahead of every other handler, so a letter typed into a chat line reaches no debug key,
        // overlay or skip. Only the keys; the pointer stays with whoever reads it.
        if (_wire.Chat is { } chat && @event is InputEventKey key
            && chat.TakeKey(key.Keycode, key.Pressed, (char)key.Unicode))
        {
            GetViewport()?.SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A cutscene skips on any input, as the original's state core does; a stick's is polled in
        // PollStickSkip. ⚠ Escape is exempt: it is the way out of the session. ⚠ Pads count only
        // where this session reads pads at all, since a pad reports button 0 pressed on arrival.
        if (_cutscene is { Playing: true } && !StartHeld
            && (@event is InputEventKey { Pressed: true, Echo: false, Keycode: not Key.Escape }
                || (!_spec.PadsDisabled && @event is InputEventJoypadButton { Pressed: true })))
        {
            int skipper = SkipperIndex(@event);
            if (_cutscene.Skip(skipper))
            {
                // Named on screen, because with a field of humans the picture ending is one
                // player's decision the others did not make.
                _split?.NoteSkip(skipper);
                return;
            }

            // The skip was declined, so this scene is one the original plays out in full: the same
            // input held fast-forwards it instead (docs/formats/anim-definitions/cutscenes.md).
            _cutscene.NoteHeld(@event);
        }
        // P halts the sim and . steps it one frame, in freecam and the static viewer. ⚠ Do not
        // handle either for flight or the animation lab. Both own their own transport, and in
        // flight both keys belong to the rebindable keymap (. is Yaw Right by default).
        if (!_spec.AnimLab && !_spec.Fly && @event is InputEventKey { Pressed: true, Echo: false } clockKey
            && _clock != null)
        {
            if (clockKey.Keycode == Key.P)
            {
                _clock.Halted = !_clock.Halted;
                Log.Info("core", $"{(_clock.Halted ? "clock: halted (P resumes, . steps one frame, hold . to run)" : "clock: running")}");
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
        if (!_spec.AnimLab && !_spec.Fly && @event is InputEventKey { Pressed: false } clockKeyUp
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
        using var _ = ProcessSiteCost.Enter(ProcessSite.Session);
        // First thing in the frame (ProcessPriority): decide how much sim time this rendered frame
        // is worth, then, when the clock is not realtime, step the physics-driven consumers
        // ourselves, in the tree order Godot's physics tick would have used.
        if (_clock is { } clock)
        {
            if (_stepHold.Tick((float)delta))
            {
                clock.StepOnce();
            }
            clock.BeginFrame(delta);
            // A guest's offset onto host time is walked, not written, so nothing reading a
            // replicated timestamp sees the correction land on one frame. Wall time, so a chat
            // line keeps its ten seconds whatever the sim clock is doing.
            _wire.Advance(delta);
            if (clock.ParentDriven)
            {
                DriveParentSimulation(clock);
            }
            // --debug-wash=N: two scripted blend washes to viewer N, red on the first sim frame
            // and white two seconds in, overlapping so the blend and not just the routing is on
            // screen. Sim time, every clock mode; a viewer no pane answers to paints nothing.
            if (_spec.DebugWash is int washViewer && _debugWashesFired < 2 && _screenFlash != null
                && clock.Time >= _debugWashesFired * 2.0)
            {
                _debugWashesFired++;
                var colour = _debugWashesFired == 1 ? new Color(1f, 0f, 0f) : new Color(1f, 1f, 1f);
                _screenFlash.PlayBlend(washViewer - 1, colour, _debugWashesFired == 1 ? 1f : 0.5f, 5f);
                Log.Info("flight", $"--debug-wash: wash {_debugWashesFired} addressed to viewer {washViewer} of {_screenFlash.PaneCount}");
            }
        }
        PollStickSkip();
        // The startup line goes out on the frame that proves the first one was drawn.
        _startup?.Frame();
        // One step of one deferred crash rig. A mid-flight AI introduction leaves its rig unbuilt
        // so the launch frame carries only what puts the aeroplane in the world; this is where the
        // rest of it lands, on the frames after.
        _flightRoster?.PumpDeferredCrashRigs();
        // Entities switched off as unplaced but since moved off the world origin are put back: a
        // motion starting is the proof a definition owns them. ⚠ Do not defer this once instead of
        // polling, and keep it on wall time: an OnCall definition can start its motion at any time.
        if (_unplacedWatch != null)
        {
            _unplacedRecheck += delta;
            if (_unplacedRecheck >= 1.0)
            {
                _unplacedRecheck = 0.0;
                if (_unplacedWatch.RestorePlacedEntities() is { Count: > 0 } restored)
                {
                    Log.Info("world", $"world: {restored.Count} entit(y/ies) moved off the origin after all, restored: {string.Join(", ", restored)}");
                }
            }
        }
        // Everything below is anchored to *a* camera, so it runs once per rig, one in single
        // player, one per pane in splitscreen (each on that player's own visual layer). See
        // src/Session/World/WeatherRig.cs's Tick.
        _sky?.Weather?.Tick(_rigs);
        // After the weather tick: that is where each rig's dome is re-centred on its camera, and
        // the flare reads the sun node inside it.
        _sky?.Flare?.Tick(delta);

        // One mirrored-tile window serves every pane (the union of the rings around each player),
        // so two players at opposite edges both get continued terrain. A no-op until one of them
        // crosses a cell boundary.
        if (_edgeExtender != null)
        {
            _focusPoints.Clear();
            foreach (var rig in _rigs)
                _focusPoints.Add(rig.Camera.Position);
            _edgeExtender.Update(_focusPoints);
        }

        // ⚠ Last in the frame, and the ONLY call anywhere: a suite that steps the simulation
        // itself must never reach this, so that it always reads the simulation pose
        // (docs/architecture.md, src/Utils/RenderPoses.cs).
        RenderPoses.Draw();
        // After the pose draw, because that is the state this frame renders. The cover the launcher
        // holds over the start comes off on what the player is meant to see, not on the build
        // finishing.
        FirstFrameReady = FirstFrameReady || ShowsFirstRealFrame();
    }

    /// <summary>The realtime clock adapter: request one complete session-simulation step from
    /// Godot's physics tick. Parent-driven modes request their substeps from <see cref="_Process"/>.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0.0 || _clock is not { } clock)
            return;
        // Ahead of the mode check: a held start steps the wire from here in every clock mode. The
        // parent-driven loop runs no steps while held.
        bool wireStepped = clock.StartHeld;
        if (wireStepped && !_wire.StepStartHold(clock, delta))
            return;
        if (clock.ParentDriven)
            return;
        // Before the step, never after: everything below reads world poses, and a follower or a
        // held pose seeded from a drawn one would feed the interpolation back into the simulation.
        RenderPoses.Restore();
        // Before the step, so everything that arrived is already applied when the phases run.
        if (!wireStepped)
        {
            _wire.Step(delta);
        }
        _simulation?.Step((float)delta);
    }

    // BL-451: a --campaign= launch never carries --zeppelins/--generators (FromCampaign resolves
    // before the mission is known), so this peeks both loaders once ResolveSpec has settled
    // Chapter/Mission. Internal, not private, so the campaign-zeppelins suite exercises the exact
    // code the constructor runs rather than a copy of it. See WithCampaignZeppelins for the why.
    internal static SessionSpec ResolveCampaignZeppelins(SessionSpec spec, string dataRoot)
    {
        if (spec.CampaignProfile == null)
        {
            return spec;
        }
        var missionZrdr = SessionPaths.MissionZrdr(dataRoot, spec.Chapter, spec.Mission);
        return spec.WithCampaignZeppelins(
            HasMissionRecords(() => Zeppelins.Load(missionZrdr)),
            HasMissionRecords(() => EnemyGenerators.Load(missionZrdr)));
    }

    /// <summary>Carries out one step of the build the load screen still owes and answers
    /// whether more is left. The wave aeroplanes are that work. They belong to the load, where
    /// there is no frame budget, rather than to the launch frame that needs one.</summary>
    internal bool StepOwedLoad() => _flightRoster?.BuildOrderedAirframe() ?? false;

    /// <summary>Every board's and the pause sheet's way out. A network race says so first. A guest's
    /// word marks its seats left on the host; a host leaving an ended race takes every guest to the
    /// lobby.</summary>
    internal void LeaveFlight()
    {
        _wire.Race?.Leave(toLobby: _menuDriven);
        _exitSession();
    }

    // The --campaign= zeppelin/generator peek's plumbing: true when the loader's list is
    // non-empty, false on an empty (authored [null]) or altogether missing mission file. Both
    // loaders already tolerate [null]; only the "no file at all" case needs the catch.
    private static bool HasMissionRecords<T>(Func<List<T>> load)
    {
        try
        {
            return load().Count > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // How long a seat's first count runs before GO, the window's opening; none opens it at once.
    private static float OpeningSeconds(IReadOnlyList<StartCountPhase>? phases) =>
        phases?.Sum(phase => phase.Seconds) ?? 0f;

    // A race run that is its pilot's best so far records under the solo run's key, the same store
    // and the same never-worsen rule. A pilot with no key records nothing.
    private static void RecordRaceBest(ScoreStore store, Racer racer)
    {
        if (racer.ScoreKey.Length > 0 && racer.BestTime is { } best && store.RecordIfBest(racer.ScoreKey, best))
            Log.Info("flight", $"stunt race: {racer.Tag} set a new best {StuntMission.FormatTime(best)} for '{racer.ScoreKey}'");
    }

    // A campaign mission has ended and its result is banked (the director records the attempt and
    // saves the profile before raising this). The world stays up for the rest of the frame; the
    // Launcher frees this session and shows the menu at the debrief, which re-reads the profile
    // this director just wrote.
    private void OnCampaignMissionEnded(CampaignMissionResult result)
    {
        if (_spec.CampaignProfile is not { } profile || _campaignMissionEnded == null)
        {
            return;
        }

        Log.Info("core", $"campaign: {result.Outcome}, handing '{profile}' back to the menu's debrief");
        _campaignMissionEnded(profile, result);
    }

    // What the start cover waits for. An intro owns the eye, so its camera having posed the rigs is
    // the frame. Otherwise it is the flown aeroplane placed under a live rig. A mode with no rig at
    // all (viewer, freecam) has nothing to wait for beyond the built world's first processed frame.
    private bool ShowsFirstRealFrame()
    {
        if (!InSession)
        {
            return false;
        }

        if (_cutscene is { Playing: true })
        {
            return _cutscene.CameraPosed;
        }

        return _rigs.Count == 0 || _rigs[0].Controller != null;
    }

    // Places every rig at the --pos= placement BuildFlightRigs withheld while the intro owned
    // the session, the moment the intro's own handoff (WorldHeld -> false) says it is done
    // testing the player against the authored spawn. A no-op when nothing was withheld: most
    // WorldHeld(false) calls are an ordinary mid-mission cutscene ending, not this one.
    private void ApplyDeferredSpawnOverride()
    {
        bool applied = false;
        for (int i = 0; i < _rigs.Count; i++)
        {
            string tag = _rigs.Count > 1 ? $"P{i + 1} " : "";
            if (_spawnPicker.TakeDeferredOverride(i, tag) is not { } placement
                || _rigs[i].Controller is not { } pilot)
            {
                continue;
            }

            pilot.Activate(placement.pos, placement.lookAt);
            applied = true;
        }

        if (applied)
        {
            _spawnPicker.ClearDeferredOverride();
            Log.Info("core", $"campaign: --pos= applied at the intro's handoff");
        }
    }

    /// <summary>Every aircraft in the session, the rigs' controllers first and then the AI
    /// planes, in one reused list read fresh on each call: waves activate and generators spawn
    /// long after the consumers holding this delegate are built. Not to be held across a step.</summary>
    private IReadOnlyList<FlightController> AllAircraft()
    {
        _aircraftScan.Clear();
        foreach (var rig in _rigs)
        {
            if (rig.Controller is { } c)
            {
                _aircraftScan.Add(c);
            }
        }
        _aircraftScan.AddRange(AiPlanes);
        return _aircraftScan;
    }

    // Every joined player's aircraft and no AI, in the same reused-list shape as AllAircraft.
    // Read fresh because an airframe swap rebuilds a rig's controller; do not hold across a step.
    // Outside splitscreen the sole entry is the scripted player. ⚠ Over the SEATS, not the panes.
    // A guest flown elsewhere is a human of this mission, read at its interpolated pose. Its
    // wreck is the one its own death report plays here.
    private IReadOnlyList<FlightController> HumanAircraft()
    {
        _humanScan.Clear();
        for (int seat = 0; seat < _seatRigs.Count; seat++)
        {
            // A guest that left is no longer a human of this mission, so nothing waits on it.
            if (_seatRigs[seat].Controller is { } c && !_wire.HasLeft(seat))
            {
                _humanScan.Add(c);
            }
        }

        return _humanScan;
    }

    // Every drawn thing the cloud band's per-object zone gate judges by its own altitude, meaning
    // the session's aircraft and its placed zeppelins. Same reused-list shape as AllAircraft, read
    // fresh each frame because both rosters change all flight long.
    private IReadOnlyList<Node3D> GatedObjects()
    {
        _gatedScan.Clear();
        _gatedScan.AddRange(AllAircraft());
        _zeppelins?.CollectHosts(_gatedScan);
        return _gatedScan;
    }

    // The whole warm-up at once, while the load screen is still up (--shader-warmup=load).
    private void WarmShadersNow()
    {
        long start = Stopwatch.GetTimestamp();
        int twins = ShaderTwins.WarmOtherMode();
        int cards = _sky?.CloudField?.WarmOtherMode() ?? 0;
        // The other mode's twins first, so the advanced variants the hidden frame builds cover them.
        bool advanced = EnhancedLook.WarmAdvancedVariants(this);
        Log.Info("world", $"shader warm-up: other mode's twins={twins} cloud_cards={cards} advanced_variants={(advanced ? "hidden frame" : "not owed")} at load ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:0.0}");
    }

    // Loads the session's core archives (gamez, textures, sounds, sound defs/groups) and routes the
    // texture/sound archives to whichever owner outlives this build scope.
    private void LoadArchives(BuildState state)
    {
        // ⚠ Do not defer this to the flight audio below: the animation bootstrap builds the world's
        // ambient SOUND_NODE emitters and needs the archive while it runs.
        var archives = SessionArchives.OpenFor(
            _spec.AnimLab ? ArchiveIntent.Lab : ArchiveIntent.Session,
            state.GamezPath, state.TexturesPath, state.SoundsPath, state.ZrdrPath, state.Mute, _decode);
        state.Gamez = archives.Gamez;
        state.Textures = archives.Textures;
        state.Sounds = archives.Sounds;
        state.SoundDefs = archives.SoundDefs;
        state.SoundGroups = archives.SoundGroups;
        state.TexturesOutliveBuild = archives.TexturesOutliveBuild;
        state.SoundsOutliveBuild = archives.SoundsOutliveBuild;
        // ⚠ Keep the texture archive open past this build scope; the data-driven crash bakes its
        // effect puffers lazily. The lab owns its copy, every other mode hands it to the session.
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

    // --node=<cs_name>: resolve the request against the chapter gamez BEFORE anything is built, so
    // a miss reports its candidates and quits instead of half-building a world. Returns false if
    // the session must abort. ⚠ Match on the source name, never the Godot node name, which is
    // sanitized and auto-renamed; duplicates are normal, so the first match builds.
    private bool ResolveNodeSubtree(BuildState state)
    {
        // --zep= is the same subtree build under another name: the record's hull instead of a node
        // the user named, so the graft borrows this resolution rather than repeating it.
        if (_spec.Zep is { } zep)
        {
            var zepMatches = WorldBuilder.MatchNodes(state.Gamez, zep.Record);
            if (zepMatches.Count == 0)
            {
                var zepNear = WorldBuilder.SuggestNodes(state.Gamez, zep.Record, NodeSuggestCap);
                Log.Warn("world", $"--zep: '{zep.Record}' matches no node in {zep.Chapter}'s gamez ({state.Gamez.Nodes.Count} nodes)");
                if (zepNear.Count > 0)
                {
                    Log.Warn("world", $"--zep= candidates containing '{zep.Record}': {string.Join(", ", zepNear)}");
                }
                _sessionTextures?.Dispose();
                _sessionTextures = null;
                GetTree().Quit();
                return false;
            }
            state.NodeSubtree = zepMatches[0];
            Log.Info("world", $"--zep='{zep.Chapter}/{zep.Mission}:{zep.Record}' grafting '{zepMatches[0].Name}'#{zepMatches[0].Index} onto the empty stage");
            return true;
        }
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
                Log.Warn("world", $"--node= no name in {_spec.Chapter} contains '{_spec.NodeName}' either, run the full world with --debug-names to read names off the objects");
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
            Log.Warn("world", $"--node='{_spec.NodeName}' is ambiguous, building the first ({state.NodeSubtree.Name}#{state.NodeSubtree.Index}); name a unique node or pick by eye from the list above");
        }
        return true;
    }

    // --stage=empty: no gamez, no mission, no animation program, just a flat collidable ground
    // plane under a grid drawn in code. Flight, weapons and colliders work; nothing else is built.
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

    // --zep=: the graft's ground. The same grid and collidable plane the bare stage builds, added
    // beside the subtree rather than as the subject, since on this path _plane is the airship and
    // AttachPlaneAndLabs joins that one.
    private void AttachEmptyStageGrid(BuildState state)
    {
        long mark = StartupProfile.Mark();
        var stage = EmptyStage.Build(collision: _spec.Fly || _spec.ForceCollision);
        StartupProfile.Record("world", mark);
        _worldRoot!.AddChild(stage.Root);
        state.MeshInstances += stage.MeshInstanceCount;
        state.Colliders += stage.ColliderCount;
        state.What += " on the empty stage";
    }

    // Builds the chapter world through WorldSession and binds its animation program, then the
    // per-view steps that read it back: node-name framing, the damage/effects-test probes, the
    // shared selection + node/damage labs, unplaced-entity hiding, the map-edge extender, per-rig
    // weather and the --anim-lab stage. Returns false if the session must abort.
    // ⚠ WorldSession does not add its Root to the tree; the AddChild(_plane) below owns that.
    private bool BuildWorldStage(BuildState state)
    {
        // AI voice lines are first reached at runtime, after the sound archive closes, so their
        // clips must join the prewarm set now. ⚠ Prewarm the mission roster's own accents, not the
        // whole voice bank; a mission without a roster prewarms none.
        IReadOnlyCollection<string>? voiceClips = null;
        if (_spec.Fly && state.NodeSubtree == null
            && state.SoundDefs is { } voiceDefs && state.SoundGroups is { } voiceGroups)
        {
            // Accents assigned outside the roster (--ai=…:accent=N, the Instant Action actors) join
            // the roster set. Their clips are first reached at runtime too, so an unprewarmed
            // accent would be a silent pilot.
            var extraAccents = new List<int>();
            if (_spec.AiPlanes is { } aiEntries)
            {
                foreach (var entry in aiEntries)
                {
                    if (entry.Accent is { } accent)
                    {
                        extraAccents.Add(accent);
                    }
                }
            }
            if (_iaDirector is { } iaVoice)
            {
                extraAccents.AddRange(InstantActionRuntime.VoiceAccentIds(iaVoice.Runtime.Def));
            }
            // Each network seat's chosen pilot speaks its lines at runtime too.
            var seatPilots = _wire.Seats.Select(s => UI.Menu.PilotVoices.SpeakerFor(s.Voice))
                .OfType<int>().ToList();
            voiceClips = CombatVoice.SessionPrewarmNames(
                state.ZrdrPath, state.MissionZrdrPath, voiceDefs, voiceGroups, extraAccents, seatPilots);
        }
        var session = WorldSession.Build(
            new WorldSession.Options
            {
                DataRoot = state.DataRoot,
                Chapter = _spec.Chapter,
                Mission = _spec.Mission,
                ZrdrPath = state.ZrdrPath,
                InterpPath = state.InterpPath,
                MissionZrdrPath = state.MissionZrdrPath,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(state.DataRoot, _spec.Chapter),
                Decode = _decode,
                EffectsParent = _worldRoot!,
                // The one instance the weather rig publishes to and the player's own effects read.
                // Without it the world's emitters hold the camera-less still-air null object, which
                // runs neither the authored distance fade nor either of its culls.
                ArchiveEmitterFactory = (textures, parent) =>
                    new Effects.PufferEmitterFactory(textures, parent, _ambience),
                // The PLAYER_RANGE fallback for a runtime with no PlayerPositions wired: player 1's
                // camera, resolved per call because none of those cameras exist yet here.
                PlayerPosition = () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam
                    ? cam.GlobalPosition
                    : Vector3.Zero,
                // Every pane camera is a 3D audio listener, so the world is heard from the nearest
                // of them (UI.Boards.SplitScreen). Same set as the rigs, for the debug log's column only.
                ListenerPositions = () =>
                {
                    if (_rigs.Count == 0)
                        return _camera is { } cam ? new[] { cam.GlobalPosition } : System.Array.Empty<Vector3>();
                    var positions = new Vector3[_rigs.Count];
                    for (int i = 0; i < _rigs.Count; i++)
                        positions[i] = _rigs[i].Camera.GlobalPosition;
                    return positions;
                },
                // ⚠ Measure EXECUTION_BY_RANGE and PLAYER_RANGE from the aircraft, not the camera.
                // The chase camera trails far enough behind to eat most of a 50 m radius.
                PlayerPositions = FieldPositionsSnapshot,
                // PLAYER_1ST_PERSON: any pilot in Cockpit or Nose. There is one world runtime for
                // every pane, so in splitscreen the condition is "someone is in a cockpit", the
                // per-pane reading needs per-pane runtimes and is E41's filed splitscreen item.
                FirstPersonView = AnyPilotFirstPerson,
                // The world lights' own nearest-viewer budget, the draw-rule seam A3
                // promoted, so a light beside player 4's pane stays lit even while player 1 is
                // far from it. Single player: one entry, same as every other _viewers consumer.
                LightViewerPositions = () => _viewers.Positions(),
                // Resolved on the spec: the damage-test census, --collision's overlay and
                // --debug-damage's collider count all need real bodies in a non-fly harness.
                Collision = BuildsCollision,
                DebugAnim = _spec.DebugAnim,
                AnimLod = _spec.AnimLod,
                DebugDzPaths = _spec.DebugDzPaths,
                NoClutter = _spec.NoClutter,
                DebugClutterFlag = _spec.DebugClutterFlag,
                HiddenAlpha = _spec.HiddenAlpha,
                ClutterTemplates = _spec.ClutterTemplates,
                // ⚠ Do not set these by hand; they come from LoadArchives's ArchiveIntent. The
                // textures belong to the session so the runtime keeps a live PufferFactory; the
                // sounds do not, being a `using` of this build outside the lab.
                TexturesOutliveBuild = state.TexturesOutliveBuild,
                SoundsOutliveBuild = state.SoundsOutliveBuild,
                VoiceClipNames = voiceClips,
                // The mission's own WAKEUP_SOUND_GROUP/COMPLETED_SOUND_GROUP names, never
                // referenced by the anim program, so nothing else would prewarm them (D33).
                ExtraPrewarmNames = _campaign?.Script.SoundGroupNames(),
                // The lab's quiet stage: ambient playback deferred to its A toggle, staged templates
                // relocated onto the call site. Safe this early, since a quiet-stage bootstrap
                // dispatches only RESET_STATEs and never consults it.
                AutoStart = !_spec.AnimLab,
                PlacesCalledTemplates = _spec.AnimLab,
                // The world's dice, RANDOM_WEIGHT verdicts, SOUND_GROUPS picks, crash-debris
                // scatter, in every mode, not just the lab.
                RuntimeSeed = Rng.IntSeedFor(Rng.Anim),
                // --node=: one subtree instead of the whole world (null = the full build).
                NodeSubtree = state.NodeSubtree,
                // The cutscene seam, wired before the bootstrap because an intro definition raises
                // its codes the instant startanims starts it, long before a rig exists.
                Cutscenes = _cutscene != null ? CutsceneWorldNames : null,
                LandingTriggers = _landings != null,
                PlanesGamezPath = state.PlanesGamezPath,
                CallbackHost = _cutscene != null ? _cutscene.Host : null,
                // No owner named: the opening cutscene has no triggering human, so the episode is
                // the scripted player's, which is what the intro has always meant.
                TriggerOwner = _cutscene is { } host ? anim => host.Own(anim) : null,
                // The weather rig is built after the world, and the intro's fog fires inside the
                // bootstrap, so the event is held until the rig has applied its zone.
                FogStateSink = fog => _sky?.TakeFogState(fog),
            },
            state.Gamez, state.Textures, state.Sounds, state.SoundDefs, state.SoundGroups);
        _plane = session.Root;
        var builder = session.Builder;
        state.CloudDeck = session.CloudDeck;
        // The deck's other lit variant, built beside it, the weather rig swaps it in above the
        // cloud band (WorldBuilder.CloudDeckUndimmedMeshes).
        state.DeckUndimmedMeshes = builder.CloudDeckUndimmedMeshes;
        // Owned by the session so a teardown drops the previous world's lights.
        _worldLights = session.Lights;
        // Under Enhanced every burning emitter lights its surroundings; on the faithful path no
        // emitter registers, so the source submits nothing.
        _worldLights?.AddSource(_ambience.SubmitFires);
        state.CrashProgram = session.Program;
        state.WorldScene = session.Builder.Scene;
        _worldScene = session.Builder.Scene;
        _clutter = session.Clutter;
        state.WorldRuntime = session.Runtime;
        // The mission's craters, which need world colliders both to find the terrain a round struck
        // and to cut that terrain's own trimesh. Owned by the session, so they last exactly as long
        // as the mission does and a new launch starts on uncratered ground.
        state.Craters = BuildsCollision ? new CraterField(session.Root) : null;
        // The enhanced scorch marks over those carves, a remake-only layer: null on the faithful
        // path, where the field, its decal pool and its texture are never built at all.
        _scorches = BuildsCollision ? Effects.ScorchField.Create() : null;
        state.Scorches = _scorches;
        if (_scorches != null)
            session.Root.AddChild(_scorches);
        // After the bootstrap: an intro definition has already raised its codes, and this is where
        // the host picks up the two nodes it drives.
        _cutscene?.BindWorld(session.Runtime, session.Aircraft);
        state.Aircraft = session.Aircraft;
        state.StagedAircraftMeshes = session.Aircraft?.MeshInstances ?? 0;
        state.Landings = session.Landings;
        state.Pickups = session.Pickups;
        if (_cutscene != null && _landings != null)
        {
            _cutscene.HostDefinitions(session.LandingCutsceneAnims);
            _landings.Bind(session.Runtime, session.Landings, _cutscene, () => _seatRigs);
            _ladder?.Bind(session.Runtime, _cutscene, () => _seatRigs, session.Pickups);
        }
        // The screen wash. Set here rather than inside WorldSession for the same reason the
        // contact mask below is: the overlay is a session-owned surface and WorldSession builds
        // runtimes for the test harness too, where there is no session to own one.
        session.Runtime.ScreenFlash = _screenFlash != null ? _screenFlash.Play : null;
        // Ground contact for the world's own do_intersections bodies. Set here, not inside
        // WorldSession, because the mask is a flight-layer constant and the runtime is the
        // animation layer. Left at 0 in a collider-less build, which is the whole fallback.
        if (BuildsCollision)
        {
            session.Runtime.ContactMask = CollisionLayers.World;
            // Bound to the ONE surface-id read, so a landing piece, a round's impact and a
            // wingtip graze cannot disagree about what they hit.
            session.Runtime.SurfaceIsWater = body => ProjectilePool.SurfaceIsWater(body as Node);
        }

        // F13 / --debug-ainets, the patrol nets and their live leashes (src/Launch/InspectionLabs.cs).
        // Chapter-scoped data, so the --node= partial stage skips it with the rest of the dressing.
        if (state.NodeSubtree == null)
        {
            _labs!.BuildAiNetsOverlay(state, _plane, () => AiPlanes, () => _netTrailers);
        }

        // --node=: the built subtree's WORLD-frame box. ⚠ Do not read it from GlobalTransform (the
        // subtree is not in the scene tree yet) or from the gamez child_bbox, which is stored in
        // the node's own frame.
        if (state.NodeSubtree != null && WorldBuilder.DetachedWorldAabb(session.Root) is { } box)
        {
            state.NodeAabb = box;
            Log.Info("world", $"node stage: '{state.NodeSubtree.Name}'#{state.NodeSubtree.Index} built, {builder.MeshInstanceCount} mesh instance(s), centre=({box.GetCenter().X:0},{box.GetCenter().Y:0},{box.GetCenter().Z:0}) size=({box.Size.X:0.#},{box.Size.Y:0.#},{box.Size.Z:0.#})");
        }
        else if (state.NodeSubtree != null)
        {
            Log.Warn("world", $"node stage: '{state.NodeSubtree.Name}'#{state.NodeSubtree.Index} built no geometry at all, it is a group node; the camera framing has nothing to aim at");
        }

        // The probes that end the session over the world just built (src/Launch/SessionProbes.cs).
        if (_probes!.RunWorldProbes(state, session, _worldEffectsFactory, _camera))
            return false;

        // The shared selection and its node and world damage labs, built now so the anim lab below
        // can bind its camera follow (src/Launch/InspectionLabs.cs).
        _labs!.BuildWorldLabs(state, session, _worldEffectsFactory);

        // ⚠ Do not move this earlier: every mechanism that places or hides a world entity must have
        // run, so that anything still on the world origin is content this mission never placed.
        // Switched off at once; _Process polls RestorePlacedEntities for what a motion moves.
        _unplacedWatch = builder;
        if (builder.HideUnplacedEntities() is { Count: > 0 } unplaced)
        {
            Log.Info("world", $"world: {unplaced.Count} unplaced entit(y/ies) left at the origin, switched off: {string.Join(", ", unplaced)}");
        }

        // Map-edge continuation: a rolling window of mirrored terrain tiles that follows the plane
        // past the map boundary. On in --fly and in static weathered views; ⚠ leave it off for
        // plain orbit viewing, which is an honest view of the data.
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
                Log.Info("world", $"map edge: rolling tile window active, block {_edgeExtender.BlockCells} cell(s), {(_edgeExtender.RepeatInsteadOfMirror ? "repeat" : "mirror (NOT what the original does)")}");
            }

            if (_probes!.DumpTileGrid(_edgeExtender))
                return false;
        }
        // The sky step: the weather rig, its domes and decks' zone, the cloud field and banks, then
        // the lens flare over them (src/Launch/SkyStage.cs).
        _sky!.Build(state, builder);
        state.MeshInstances = builder.MeshInstanceCount + state.StagedAircraftMeshes;
        state.Colliders = builder.ColliderCount;
        // Read after the domes, since C1's daytime sky layer is a horizon child.
        if (builder.ScrollingModelCount > 0)
            Log.Info("world", $"texture scroll: {builder.ScrollingModelCount} model(s) animating UVs");
        // The evidence that the authored render flags reached the materials, a night chapter
        // reporting 0 self-lit models means they did not.
        Log.Info("world", $"model flags: {builder.UnlitModelCount} self-lit (lighting: false), {builder.UnfoggedModelCount} unfogged (fog: false)");
        // The per-polygon second material pass. A declined count above zero means a
        // sprite/facade mesh carried one and it was dropped, never observed in this install.
        if (builder.OverlayPassSurfaceCount > 0 || builder.OverlayPassDeclinedCount > 0)
            Log.Info("world", $"overlay passes: {builder.OverlayPassSurfaceCount} surface(s) built, {builder.OverlayPassDeclinedCount} polygon(s) declined");
        // The evidence that SHOW_BACKFACE reached the colliders: a build with collision on that
        // reports no one-sided faces is back to the old blanket two-sided flag.
        if (builder.CollisionSidedness.Count > 0)
        {
            var classes = builder.CollisionSidedness
                .Select(kv => $"{(kv.Key.Length == 0 ? "default" : kv.Key)} {kv.Value.OneSided}/{kv.Value.OneSided + kv.Value.TwoSided}");
            Log.Info("world", $"collision sidedness: one-sided faces per class {string.Join(", ", classes)}; {builder.CollisionBackToBackPairs} back-to-back pair(s)");
        }
        // C3's skydome cloud cards, and nothing else in the install: the tripwire if the
        // absent-and-undrawn rule ever reaches a chapter that ships the texture.
        if (builder.UndrawnPolygonCount > 0)
            Log.Info("world", $"undrawn polygons: {builder.UndrawnPolygonCount} dropped (texture absent from game data)");
        state.What = $"chapter {_spec.Chapter} world";

        // The animation debugger (--anim-lab): the lab node owns the clock and the
        // transport; the world above is its quiet stage (AutoStart=false, reset states
        // and mission setup applied, nothing playing until A or --play-anim).
        if (_spec.AnimLab)
        {
            _labs!.BuildAnimLabStage(state, session, _spawnPicker, _liveryResolver, _masterSeed);
        }
        return true;
    }

    // Joins the built subject to the tree, then the labs shared by every mode that observes it: the
    // world selection/node/damage labs, the viewer's mesh lab, the marker overlay and the weapon
    // lab. Returns false if the session must abort.
    private bool AttachPlaneAndLabs(BuildState state)
    {
        _worldRoot!.AddChild(_plane);
        // The labs shared by every mode that observes the subject join after it does.
        _labs!.AttachLabs(state, _plane);
        // --weapon-test, the parked bench, ends the session here (src/Launch/SessionProbes.cs).
        return !_probes!.RunWeaponBench(state, _plane, _camera);
    }

    // --fly (and --stunt): builds every rendered rig's aircraft (model, loadout, HUD, audio,
    // stunt/crash hookup) over the session-wide flight data loaded once above the per-player loop.
    // The per-player body lives in its own HumanFlightAdapter.
    /// <summary>Decodes every sound the weapons catalogue binds, at the <c>LOOPED</c> flag its play
    /// site asks for (<see cref="WeaponDefs.SoundCues"/>). A name may be a <c>SOUND_GROUPS</c>
    /// group, whose members are picked at random per shot, so every member decodes.
    /// ⚠ Silent on a miss: a per-chapter archive legitimately lacks weapons another chapter uses,
    /// and the report that matters is the one at the point of use.</summary>
    private void PrewarmWeaponSounds(BuildState state, WeaponDefs weaponDefs)
    {
        if (state.Sounds is not { } archive || state.SoundDefs is not { } soundDefs)
        {
            return;
        }
        long mark = StartupProfile.Mark();
        int decoded = 0;
        foreach (var (name, looped) in weaponDefs.SoundCues())
        {
            foreach (string member in ExpandSoundGroup(name, state.SoundGroups))
            {
                if (soundDefs.TryGetValue(member, out var def)
                    && archive.Find(def.WavName, looped, warn: false) != null)
                {
                    decoded++;
                }
            }
        }
        StartupProfile.Record("prewarm", mark);
        Log.Info("sound", $"weapons prewarm: {decoded} stream(s) decoded before the archive closed");
    }

    // A cue name is either a SOUND_GROUPS group or a plain definition. Mirrors WorldSounds.Prewarm's
    // expansion, including the dialogue chains a group can carry.
    private IEnumerable<string> ExpandSoundGroup(string name, Dictionary<string, SoundGroup>? groups)
    {
        if (groups == null || !groups.TryGetValue(name, out var group))
        {
            yield return name;
            yield break;
        }
        foreach (var (member, _) in group.Members)
        {
            yield return member;
        }
        foreach (var chain in group.Chains)
        {
            foreach (string line in chain)
            {
                yield return line;
            }
        }
    }

    private void BuildFlightRigs(BuildState state)
    {
        var voices = _voices = new SessionVoices(_spec, _zrdrPath, _worldRoot!, _rigs, _seatRigs, _wire.Seats,
            onWire: _wire.Link != null);
        long mark = StartupProfile.Mark();
        // On the empty stage the session gamez IS planes.zbd (there is no chapter world), so there
        // is nothing to load a second time. ⚠ Unless --zep= grafted a chapter node on: the session
        // gamez is then that chapter's, and planes.zbd has to be loaded here as everywhere else.
        var planesGamez = _spec.EmptyStage && _spec.Zep == null
            ? state.Gamez
            : _decode?.Gamez(state.PlanesGamezPath) ?? GameZ.Load(state.PlanesGamezPath);
        StartupProfile.Record("gamez", mark);
        // Stats are per plane, not per player (splitscreen players can pick
        // different aircraft), load each distinct one once, logging it as it appears.
        var statsCache = new Dictionary<string, PlaneStats>();
        PlaneStats StatsFor(string plane)
        {
            if (statsCache.TryGetValue(plane, out var cached))
                return cached;
            var loaded = PlaneStats.Load(state.ZrdrPath, plane);
            statsCache[plane] = loaded;
            Log.Info("flight", $"flight stats [{loaded.DefName}]: fd_speed={loaded.FdSpeed} m/s weight={loaded.VehWeight} engine={loaded.EnginePower:0.00} torques=({loaded.PitchTorque},{loaded.RollTorque},{loaded.RudderTorque})");
            return loaded;
        }
        // The AI flavour of the same airframe: a different object off the same key, so it needs its
        // own dictionary. Loaded lazily, so a session with no AI spawns parses nothing twice.
        var aiStatsCache = new Dictionary<string, PlaneStats>();
        PlaneStats AiStatsFor(string plane, string? aiDef)
        {
            // Keyed by both: one airframe flown by two militias is two different sets of weapons,
            // paint and skills off the same node name.
            string key = aiDef == null ? plane : $"{plane}/{aiDef}";
            if (aiStatsCache.TryGetValue(key, out var cached))
                return cached;
            var loaded = PlaneStats.LoadForAi(state.ZrdrPath, plane, aiDef);
            aiStatsCache[key] = loaded;
            Log.Info("flight", $"ai flight stats [{loaded.DefName} damage:{loaded.AiDefName}]: armor={loaded.VehicleArmor:0.#} health={loaded.VehicleHealth:0.#} zones={loaded.DestroyableParts.Count} injure_anims={loaded.VehicleInjureAnims.Count}");
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
            Log.Info("flight", $"camera [{loaded.DisplayName ?? plane}]: dist={loaded.Dist:0.##} m{(loaded.FromData ? loaded.DisplayName == null ? " (camparam default, no block of its own)" : "" : " (no camparam.json, built-in defaults)")}");
            return loaded;
        }
        // Splitscreen: several own-ship engine stacks in one mix, equal-power scale them.
        float mixGain = 1f / Mathf.Sqrt(_rigs.Count);
        // The launchscreen's join flow binds the pads; a CLI launch derives them
        // from the connected roster instead.
        var padAssignment = _menuPads ?? Pads.AssignPads(_rigs.Count);
        // Ahead of the rigs, because the assembler hands both to the per-pane stunt board.
        _pauseState = new PauseState { Overlay = _wire.Link != null };
        _boards = new SessionBoards(new SessionBoards.Inputs
        {
            Spec = _spec,
            Presentation = _presentation,
            MenuDriven = _menuDriven,
            Exit = LeaveFlight,
            Restart = _restartSession,
            WorldRoot = _worldRoot!,
            Rigs = _rigs,
            NetSeats = _wire.Seats,
            PauseState = _pauseState,
            PadAssignment = padAssignment,
            PauseOptions = _pauseOptionsFactory,
            LockCandidates = LockCandidateAircraft,
            ZrdrPath = _zrdrPath,
            MessagesPath = _messagesPath,
            DataRoot = _dataRoot,
        });
        var boards = _boards;
        if (_menuPads != null)
            Pads.LogPads(_menuPads);
        // One livery RNG for the session, so P1..P4 draw distinct colours from one
        // stream and --paint-seed reproduces the whole field.
        var paintRng = _liveryResolver.NewPaintRng();
        // Instant Action: the mission's own scenario key, and the aircraft it forces on every human
        // (src/Session/InstantAction/InstantActionDirector.cs), null for any other launch.
        var iaRt = _iaDirector?.Runtime;
        string iaScenario = _iaDirector?.Scenario ?? _spec.Scenario;
        string? iaOverride = _iaDirector?.PlayerPlaneOverride();
        // One spawn list for the session; each player takes the next index (wrapping).
        _spawnPicker.ScenarioOverride = iaRt != null ? iaScenario : null;
        // The world build (above) has already run the intro's own animation bootstrap, so
        // _cutscene.Playing is settled before the player's spawn is chosen. Only a campaign intro
        // withholds --pos=; every other --pos= flight keeps landing on it immediately.
        _spawnPicker.WithholdOverrideForCutscene = _campaign != null && _cutscene is { Playing: true };
        // A team Dogfight walks its teams' blocks of the whole table instead of the free-for-all's.
        _spawnPicker.SeatTeams = VersusDirector.SpawnTeams(_spec, _wire.Seats);
        var spawnList = _spawnPicker.LoadSpawnList(state.MissionZrdrPath, iaScenario);
        int spawnBase = _spawnPicker.ChooseSpawnBase(spawnList);
        _spawnPicker.PlanTeams(spawnList, spawnBase);

        // The weapons catalogue and stock loadouts, loaded once, and ONE shared projectile pool
        // every player's guns fire into, since projectiles live in the shared world. The pool
        // reuses the session archives and the world gamez, so rockets get their flyout bodies.
        mark = StartupProfile.Mark();
        var weaponMessages = Messages.Load(state.MessagesPath);
        var weaponDefs = WeaponDefs.Load(state.ZrdrPath, weaponMessages);
        // Decode the catalogue's own sounds while the archive is open. Nothing else covers them:
        // weapons.json names them, not the anim program, and every one is first reached in flight
        // from a trigger pull or an impact, long after the build scope closes.
        PrewarmWeaponSounds(state, weaponDefs);
        var stockLoadouts = StockLoadouts.Load();
        var shakeDefs = ShakeDefs.Load(state.ZrdrPath);
        // The ai.zrd turret table. A missing/broken file costs the gunners, not the session.
        TurretDefs? turretDefs = null;
        try
        {
            turretDefs = TurretDefs.Load(state.ZrdrPath);
        }
        catch (Exception e)
        {
            Log.Warn("weapons", $"turrets: ai.zrd unavailable, no turret gunners: {e.Message}");
        }
        StartupProfile.Record("zrdr", mark);
        // The one shared projectile pool and its sinks (src/Launch/ProjectileStage.cs).
        var projectiles = _projectiles = ProjectileStage.Build(state, new ProjectileStage.Inputs
        {
            WorldRoot = _worldRoot!,
            Ambience = _ambience,
            WeaponDefs = weaponDefs,
            Viewers = _viewers,
            Lights = _worldLights,
            Scorches = () => _scorches,
            MixGain = mixGain,
            PlayerPositions = PlayerPositionsSnapshot,
            FirstPersonView = AnyPilotFirstPerson,
            CockpitViewOfPilot = PilotInCockpitView,
            BeeperTags = _beeperTags,
            ScreenFlash = _screenFlash,
        });

        // The original's per-frame ground shadow, one quad under every aircraft. Roster and rigs
        // are read fresh, so waves are covered and each pane's own pilot takes the player's shape
        // there. Enhanced graphics mode builds nothing here and casts real shadow maps instead.
        BuildGroundShadows();

        // The smoke screens' own smoke, wired here rather than at their construction because the
        // chapter's textures and anim program are only resolved this far into the build. Same
        // program as the rockets' flyout trails: missile_puffers carries generate_smokescreen too.
        if (_smokeScreens != null)
        {
            var smokeEmitters = new SmokeScreenEmitters(state.CrashProgram?.Defs, state.Textures,
                _worldRoot!, _ambience);
            _smokeScreens.Emitters = smokeEmitters.Create;
        }

        // The world-effects runtime: one per session, rendering the impact and destruction puffers
        // the world runtime cannot. ⚠ Go through EnsureWorldEffects, never construct it directly,
        // or a later --destroy= or damage-lab demand on the same session builds a second one.
        AnimRuntime? worldEffects = null;
        if (state.WorldScene != null && state.WorldRuntime != null)
        {
            worldEffects = _worldEffectsFactory.EnsureWorldEffects(state.Gamez, state.WorldScene,
                state.Textures, state.CrashProgram!, state.WorldRuntime, projectiles); // the rigs' graze reaction plays through the same runtime
        }

        // Stunt run: the mission's danger-zone objectives, positions resolved against this chapter
        // world's gamez. Parsed once for the session; each player then races an independent copy.
        StuntMission? stuntZones = null;
        StuntRace? race = null;
        // An Instant Action stunt_flying mission IS a stunt run: the mission type asks for the
        // zones, so --stunt is not the tester's flag to remember on an --ia= launch.
        bool iaStunt = _iaDirector?.IsStuntRun ?? false;
        bool wantStunt = _spec.Stunt || iaStunt;
        if (wantStunt && _spec.EmptyStage)
        {
            Log.Info("flight", $"--stunt has no danger zones on the empty stage (no mission, no world), flying free");
        }
        else if (wantStunt)
        {
            stuntZones = StuntMission.Load(state.Gamez, state.MissionZrdrPath, Messages.Load(state.MessagesPath));
            if (stuntZones == null)
                // Expected for the chapters whose IA1 has no dzones (C1C, C2B), a data
                // fact, not a fault, so a plain line (log hygiene: no stack traces).
                Log.Info("flight", $"--stunt: no danger zones for {_spec.Chapter}/{_spec.Mission}, flying free");
            else if (IsRace())
                race = new StuntRace(RaceWindowSeconds(), stuntZones.TotalCount);
            if (stuntZones != null && iaStunt && !_spec.Stunt)
                Log.Info("flight", $"ia: stunt_flying, {stuntZones.TotalCount} danger zone(s) from {_spec.Chapter}/{_spec.Mission}, the mission type's own objective");
        }

        // Dogfight (--vs): built here, before the rigs, same reason Race is (HumanFlightAdapter
        // binds every pane's VersusHud to this one instance below); the score/respawn plumbing
        // that feeds it Downed reports only runs once every rig exists, further down.
        _dogfight = VersusDirector.TryCreate(_spec, new VersusDirector.Field
        {
            Net = _wire.Link,
            NetSeats = _wire.Seats,
            SeatRigs = _seatRigs,
            Panes = _rigs,
            Strings = weaponMessages,
            ClockTime = () => _clock?.Time ?? 0.0,
            NetClock = _wire.Clock,
        }, state.ZrdrPath, _wire.TeamNames);
        VersusMatch? versus = _dogfight?.Match;
        // The original's HUD bitmap font, loaded once and shared across panes. Null when the rimage
        // atlas is absent, and its consumers are then simply not built.
        HudFont? hudFont = HudFont.Load(Path.Combine(_dataRoot, "extracted", "rimage"));

        // The gun aiming reticle's pipper: the game's own impact_point.png, loaded once
        // and shared across panes (it carries its own alpha, no colour-keying). Null (no file)
        // simply omits the reticle.
        Texture2D? reticleTex = ImpactReticle.LoadTexture(
            Path.Combine(_dataRoot, "extracted", "rimage"), "impact_point.png");

        // ⚠ No --det bypass on this arm, unlike the race arm below: a story mission has one
        // PLAYER_INIT, so the plain walk stacks the whole field on it
        // (docs/architecture.md, ## src/Session/Roster/StartGrid.cs).
        bool coopCampaign = _spec.CampaignProfile != null && _seatRigs.Count > 1;
        // ⚠ Choose the spawn placement ONCE, by picking an implementation here, never by a runtime
        // flag inside one. A --det race stays byte-identical because the shared start is not built.
        // It cannot move up beside new SpawnPicker, which runs before `race` is settled.
        IFlightStarts flightStarts = coopCampaign ? new StartGrid(_spawnPicker, GroundSampler())
            : race != null && !_spec.Det ? new SharedSpawnStarts(_spawnPicker)
            : _spawnPicker;
        // ⚠ No count under --det, the shared start's rule: a scripted run and every pinned golden
        // stay byte-identical because no count is ever begun. A race opens on its own count.
        var rerunCount = stuntZones != null && !_spec.Det ? StartCount.Rerun : null;
        var firstCount = rerunCount == null ? null
            : race != null ? StartCount.Opening(RaceReadySeconds) : rerunCount;
        // A network race before the roster, which feeds each local seat's run through it. A guest's
        // opening catches up to the host's on every local seat's count at once.
        var raceLink = race != null ? _wire.WireRace(race) : null;
        if (raceLink != null)
        {
            raceLink.CatchUp = seconds =>
            {
                foreach (var rig in _rigs)
                    rig.Controller?.CatchUpStartCount(seconds);
            };
        }
        var aircraftResources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = StatsFor,
            AiStatsFor = AiStatsFor,
            CamParamsFor = CamParamsFor,
            PaintRng = paintRng,
            WeaponDefs = weaponDefs,
            WeaponMessages = weaponMessages,
            StockLoadouts = stockLoadouts,
            TurretDefs = turretDefs,
            Shakes = shakeDefs,
            HudFont = hudFont,
            ReticleTex = reticleTex,
            Textures = state.Textures,
            ZrdrPath = state.ZrdrPath,
        };
        // Ensured here, before the roster reads it, rather than left to the later mode-ship/
        // generator spawn sites: those cache-check the same field, so this only moves WHEN the
        // runtime is first built, not whether it is built twice.
        var surfaceVehicleRuntime = EnsureSurfaceVehicles(state);
        // The pool holds the session's live rosters (aircraft, world emplacements), and the hulls
        // are the rest of the engine's VehicleList. A turret gunner sees this pool and nothing
        // else, so without it a gun's candidate list is the aircraft half of the pool alone.
        projectiles.SurfaceVehicles = surfaceVehicleRuntime;
        // The same reasoning for the third pool: a gun standing beside a hostile mission structure
        // has nothing else to see it through.
        projectiles.Structures = state.WorldRuntime?.Destructibles;
        if (surfaceVehicleRuntime != null)
        {
            // The other direction: a hull's own gun scans through the pool and fires through it,
            // and resolves the weapon id its def authors against the same catalogue everything
            // else uses. Without both, hulls build unarmed (docs/org/aiPilot.md).
            surfaceVehicleRuntime.Projectiles = projectiles;
            surfaceVehicleRuntime.Weapons = weaponDefs;
            // A hull's gun voice hangs on the world's own sound node, the same home a world
            // emplacement's takes and for the same reason: the animation runtime memoizes the
            // lookups inside the subtree a hull is built from.
            surfaceVehicleRuntime.Voices = state.WorldRuntime?.Sounds is { } hullVoiceHome
                ? new GunVoiceHome(hullVoiceHome, state.Sounds, state.SoundDefs,
                    PlayerPositionsSnapshot)
                : null;
        }
        var worldBindings = new FlightWorldBindings
        {
            Ambience = _ambience,
            Projectiles = projectiles,
            HumanPositions = PlayerPositionsSnapshot,
            ChapterZrdrPath = SessionPaths.ChapterZrdr(_dataRoot, _spec.Chapter),
            MissionZrdrPath = state.MissionZrdrPath,
            Gamez = state.Gamez,
            WorldScene = state.WorldScene,
            WorldRuntime = state.WorldRuntime,
            WorldEffects = worldEffects,
            SurfaceVehicles = surfaceVehicleRuntime,
            TouchdownDefs = _worldEffectsFactory.TouchdownDefs,
            CrashProgram = state.CrashProgram,
            Sounds = state.Sounds,
            SoundDefs = state.SoundDefs,
            SoundGroups = state.SoundGroups,
            // Read through the field: the radio is built after these bindings, and a session
            // with none never ducks.
            VoiceDuck = new EngineVoiceDuck(() => _voices?.Radio?.OnAir != null, AudioMix.EffectsGain),
            DebugCollision = state.DebugCollision,
            // Read through the field rather than captured by value: the rig is built after these
            // bindings, and a zone apply rewrites the band while the mission runs.
            FogRange = rigIndex => _sky?.Weather?.FogOf(rigIndex).Range ?? Vector2.Zero,
            // The session's race flag, keyed off the race itself and not off the pane count. A
            // network race that builds a StuntRace takes the same presence rules.
            Racing = race != null,
        };
        var humanBindings = new HumanRosterBindings
        {
            RigCount = _seatRigs.Count,
            NetSeats = _wire.Seats,
            SeatFit = _wire.SeatFit,
            SeatBuild = _wire.SeatBuild,
            MixGain = mixGain,
            PadAssignment = padAssignment,
            PauseState = _pauseState!,
            StuntBoard = boards.BuildSoloStuntBoard,
            ToggleGraphicsMode = _toggleGraphicsMode,
            SpawnList = spawnList,
            SpawnBase = spawnBase,
            StuntZones = stuntZones,
            RerunCount = rerunCount,
            FirstStartCount = firstCount,
            MenuSounds = rerunCount != null ? MenuSounds() : null,
            Race = race,
            RaceFeed = raceLink != null ? raceLink.Feed : null,
            VersusMatch = versus,
            Rigs = _seatRigs,
            InstantActionPlayerPlaneNode = iaOverride,
            InstantActionActive = iaRt != null,
            Coop = _spec.Coop,
            SmokeScreens = _smokeScreens,
        };
        var flightRoster = new FlightRoster(FlightRosterPolicy.From(_spec), _liveryResolver,
            _worldEffectsFactory, _worldRoot!, aircraftResources, worldBindings, humanBindings,
            flightStarts);
        var rosterBuild = flightRoster.BuildPlayers(_seatRigs);
        state.MeshInstances += rosterBuild.MeshInstances;
        state.What += rosterBuild.SummarySuffix;
        // One shared PauseState on every rig: any human pauses everybody, and only the pauser may
        // resume. The whole-window board covers every pane; single player uses the same path.
        boards.BuildPause(new SessionBoards.PauseSheetInputs
        {
            Campaign = _campaign,
            InstantAction = _iaDirector?.Runtime,
            Teamed = VersusDirector.SeatTeams(_spec, _wire.Seats) != null,
            World = state.WorldRuntime,
        }, RestartOffered ? Rerun : null);
        BuildCockpitPasses();
        FollowSpyglassSun();

        // Damage lab in flight (F19), bound to P1's real damage (src/Launch/InspectionLabs.cs).
        _labs!.BuildFlightDamageLab(state, _rigs, () => StatsFor(iaOverride ?? HumanFieldPlanes.PlaneFor(_spec, 0)));

        // --canopy-holes= gives struck glass from the spawn frame on. A scripted cockpit shot need
        // not wait on an AI burst and a 0.3 draw. Every human pane, a splitscreen canopy being per
        // pilot, and through the cue's own ledger, so a later round picks up where this left off.
        if (_spec.CanopyHoles is { } holes)
        {
            foreach (var rig in _rigs)
            {
                rig.Controller?.PresetCanopyHoles(holes);
            }
        }

        // The weapon lab in flight, bound to player 1's held aircraft.
        _labs!.BuildWeaponLab(state, _rigs, weaponDefs);

        // The board's Restart is Rerun's: a new window in place, or a rebuilt Instant Action
        // mission. The seats' opening counts began in this same build, so the window opens on GO.
        // A network guest's board waits for the host's window instead, which opens here on its call.
        if (race != null)
        {
            _race = race;
            var zoneNames = stuntZones!.Zones.Select(z => z.Description).ToList();
            if (raceLink is { IsHost: false })
                raceLink.Rerun = () => RerunRace(race);
            // --debug-race-end poses a split screen race's board as a network one's, P2 left.
            string? posed = raceLink == null ? _spec.DebugRaceEnd : null;
            if (posed != null)
                race.BestImproved += racer => _ = racer.Index == 1 && race.MarkLeft(1);
            bool guest = raceLink is { IsHost: false } || posed == "guest";
            boards.BuildRaceBoard(race, zoneNames,
                $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}", Rerun,
                raceLink != null || posed != null ? StuntRaceBoard.NetworkExitLabel(_menuDriven || posed != null, !guest) : null,
                guest ? StuntRaceBoard.WaitingForHost : null);
            foreach (var rig in _rigs)
                if (rig.Controller != null)
                    rig.Controller.RerunRace = Rerun;
            var bests = ScoreStore.ForSession(_spec.ScoresPath, _spec.ScoresThrowaway);
            race.BestImproved += racer => RecordRaceBest(bests, racer);
            race.BeginOpening(OpeningSeconds(firstCount));
            Log.Info("flight", $"stunt race: {_seatRigs.Count} pilots over {stuntZones.TotalCount} danger zones, a {StuntRace.FormatClock(race.WindowSeconds)} time attack, best run ranks{(race.Replicated ? ", the host's board and window replicated here" : "")}");
        }

        // Dogfight (--vs): the match's scoring, respawn rotation and lives, fed by every rig's
        // Downed report once every rig exists (src/Session/World/VersusDirector.cs).
        if (_dogfight is { } dogfight)
        {
            dogfight.Wire(new VersusDirector.WireInputs
            {
                Spawns = _spawnPicker,
                SpawnList = spawnList,
                SpawnBase = spawnBase,
                SpawnListName = _spawnPicker.ScenarioOverride ?? _spec.Scenario,
                ReportDeath = _wire.ReportDeath,
                ToLobby = _menuDriven ? _exitSession : null,
            });

            // The match's shared board, its R the director's rematch. A guest's board says why it
            // offers no Restart, except in Zeppelin vs Zeppelin, where each machine's own Restart
            // takes it to the lobby.
            boards.BuildDogfightBoard(dogfight.Match, $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
                dogfight.Restart,
                dogfight.RematchIsTheHosts && _spec.MissionType != DogfightMissionType.ZeppelinVsZeppelin ? VersusBoard.HostCallsTheRematch : null);
        }

        // Fire, hit, damage and death over the wire. It runs after the match so a death report
        // has a scorer to reach, and after the rigs so every seat carries its router.
        _wire.WireCombat(weaponDefs, projectiles, _dogfight);

        // And where a downed seat comes back, which runs after the match for the same reason: the
        // rotation it is granted from is built there.
        _dogfight?.WireSpawns();

        // The match clock, its limits and its ending, last of the three. It hands a guest's match
        // over to the host, so the match has to stand first.
        _dogfight?.WireMatchState();
        // The in-flight chat, once every local seat has its aeroplane to take the keys from.
        _wire.WireChat(weaponMessages);
        // No Dogfight seat and no network seat but a stunt racer's takes the respawn control in
        // flight. After WireCombat, which hands the wire the match; every seat stands now.
        _wire.WireLiveRespawn();
        AttachScores(race, weaponMessages);

        // --incoming: the incoming-fire test rig, a phantom shooter on every pilot's six, so both
        // cues and the shield are reachable with one player, no AI gunner needed.
        if (_spec.IncomingPass is float incomingPass)
        {
            var incoming = new IncomingFire(projectiles, weaponDefs, incomingPass, _spec.IncomingWeapon);
            foreach (var rig in _rigs)
                if (rig.Controller != null)
                    incoming.AddTarget(rig.Controller);
            _worldRoot!.AddChild(incoming);
            _incomingFire = incoming;
            Log.Info("weapons", $"--incoming: rounds {incomingPass:0.0} m off every player's track{(_spec.IncomingWeapon != null ? $" ({_spec.IncomingWeapon})" : " (their own gun)")}");
        }

        // The roster shares the session data the human field was built from, so later AI spawns
        // works from here on, at build or at any later sim step.
        _flightRoster = flightRoster;

        // The kill and crash lines in every pane's stack, the roster's AI included
        // (src/Launch/KillLines.cs).
        KillLines.Wire(_rigs, flightRoster, weaponMessages, _dogfight, _pilotName);

        // The radio and the combat voice (src/Launch/SessionVoices.cs), before any AI spawn so each
        // can register. The players register as event sources, a network seat as its pilot.
        voices.Build(state, projectiles, flightRoster);

        // Capture the Flag, once the match, the wire and the radio stand.
        _dogfight?.WireFlags(new VersusDirector.FlagInputs
        {
            World = state.WorldRuntime,
            Gamez = state.Gamez,
            Scene = state.WorldScene,
            WorldRoot = _worldRoot,
            Lights = _worldLights,
            Radio = voices.Radio,
            GroundAt = GroundSampler(),
            Chat = _wire.Chat,
        });
        // An anchored net rides its trailer target, so every follower built below takes a supplier
        // for the object its net names. The player is rig 0, anything else is a world node, and a
        // name that resolves to nothing leaves the net at its authored coordinates.
        var netTrailers = _netTrailers = new NetTrailerTargets(
            () => _rigs.Count > 0 && _rigs[0].Controller is { } trailedRig
                ? trailedRig.WorldPosition
                : null,
            name => worldBindings.WorldRuntime?.FindNodes(name) is { Count: > 0 } trailerHits
                ? trailerHits[0]
                : null);
        // Instant Action's actor build (director phase), at this exact point: the ace's spawn
        // draw follows the player's ChooseSpawnBase draw in the same stream, and the wingman
        // fan reads P1's built pose.
        if (_iaDirector is { } iaDirActors)
        {
            state.What += iaDirActors.BuildActors(new InstantActionDirector.ActorBuildInputs
            {
                Rigs = _rigs,
                ChapterZrdrPath = worldBindings.ChapterZrdrPath,
                MissionZrdrPath = state.MissionZrdrPath,
                ZrdrPath = state.ZrdrPath,
                MessagesPath = state.MessagesPath,
                SpawnList = spawnList,
                SpawnBase = spawnBase,
                LiveryResolver = _liveryResolver,
                NetTrailers = netTrailers,
                Spawn = flightRoster.SpawnAi,
                RegisterVoice = voices.RegisterAi,
            });
        }
        // The campaign's roster build, at the same point and for the same reason: every block is
        // placed against the built human field, and the leader pass names the player rig.
        if (_campaign is { } campaignRoster)
        {
            int grafted = 0;
            var surfaceVehicles = EnsureSurfaceVehicles(state);
            state.What += campaignRoster.BuildRoster(new CampaignDirector.RosterInputs
            {
                SpawnSurface = surfaceVehicles != null
                    ? (plan, pos, forward) => surfaceVehicles.Spawn(plan, pos, forward)
                    : null,
                ChapterZrdrPath = worldBindings.ChapterZrdrPath,
                MissionZrdrPath = state.MissionZrdrPath,
                ZrdrPath = state.ZrdrPath,
                MinAiActiveDist = voices.MinAiActiveDist(),
                Player = () => _rigs.Count > 0 ? _rigs[0].Controller : null,
                PlayerAirframe = flightRoster.FlyingAirframeOf(0),
                NetTrailers = netTrailers,
                FindNodes = worldBindings.WorldRuntime is { } rosterWorld
                    ? name => rosterWorld.FindNodes(name)
                    : null,
                Spawn = (plan, pos, look, pilot) => flightRoster.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                AttachMarkers = state.WorldRuntime is { } markerWorld
                    && state.WorldScene is { } markerScene && state.Gamez is { } markerGamez
                    ? (block, rig) => grafted +=
                        RosterMarkers.Attach(markerGamez, markerScene, markerWorld, block, rig)
                    : null,
                RegisterVoice = voices.RegisterAi,
                Rng = Rng.NewSystemRandom(Rng.Ai),
            });
            // ⚠ The first bind ran before any roster rig existed, so a row whose approach node the
            // graft above has just created was dropped there. Re-bound here, and only when
            // something was grafted, so a mission that adds nothing keeps one bind and one log line.
            if (grafted > 0 && _landings != null && _cutscene != null
                && state.WorldRuntime is { } landingWorld && state.Landings is { } landingRows)
            {
                _landings.Bind(landingWorld, landingRows, _cutscene, () => _seatRigs);
                _ladder?.Bind(landingWorld, _cutscene, () => _seatRigs, state.Pickups);
            }
        }
        // After every roster build, because a staged prop's livery is the one its own aeroplane's
        // rig resolved and that rig does not exist earlier.
        PaintStagedAircraft(state);
        // The opposition outside the directors' rosters (src/Launch/OppositionStage.cs), each step
        // at the build point it always took, the directors' phases between them.
        var opposition = new OppositionStage(new OppositionStage.Inputs
        {
            Spec = _spec,
            WorldRoot = _worldRoot!,
            Roster = flightRoster,
            NetTrailers = netTrailers,
            ChapterZrdrPath = worldBindings.ChapterZrdrPath,
            World = worldBindings.WorldRuntime,
            Campaign = _campaign,
            MinAiActiveDist = voices.MinAiActiveDist,
            AiStatsFor = AiStatsFor,
            RegisterVoice = (ai, accent) => voices.RegisterAi(ai, accent),
            EnsureSurfaceVehicles = () => EnsureSurfaceVehicles(state),
            PlayerPositions = PlayerPositionsSnapshot,
        });
        opposition.BuildSquadrons(state, _rigs);

        // --zeppelins, before --generators below so a zeppelin generator's min_altitude gate reads
        // the flown host's live Y from the first step.
        bool iaZeppelinRun = iaRt?.IsZeppelinRun ?? false;
        _zeppelins = opposition.BuildZeppelins(state, iaZeppelinRun, _projectiles, weaponDefs);

        // Instant Action's own zeppelin switch (the director's phase, between the --zeppelins and
        // --generators blocks so a held hull never feeds a generator's altitude gate). The switched
        // nodes feed the turret arm inside the emplacement block below.
        var iaZepTurretSwitch = _iaDirector != null && worldBindings.WorldRuntime is { } iaZepWorld
            ? _iaDirector.SwitchZeppelins(iaZepWorld, _zeppelins, _spec.Chapter)
            : new List<(Node3D Node, bool Objective, string Name)>();

        // --generators: the mission's egen enemy generators, spawning through the roster. An
        // Instant Action zeppelin run asks for them itself.
        _generators = opposition.BuildGenerators(state, iaZeppelinRun, _zeppelins);

        // The zeppelin run's wave arm (the director's phase): the objective zeppelin's generator
        // releases the waves built inert above. ⚠ It can only run here, after the generator block:
        // starting wave 1 on that mode means crediting a generator that did not exist earlier.
        _iaDirector?.ArmZeppelinRun(_generators);

        // Instant Action's end conditions, lives, spectate and wrap-up board (the director's
        // phase): every signal already exists above, so it only routes them into the mission
        // runtime, and its boards parent under _worldRoot like every board this method builds.
        _iaDirector?.WireEndConditions(new InstantActionDirector.EndConditionInputs
        {
            StuntZones = stuntZones,
            Race = race,
            Zeppelins = _zeppelins,
            Projectiles = _projectiles,
            WorldRoot = _worldRoot!,
            SpectatorCameras = _spectatorCameras,
            LockCandidates = LockCandidateAircraft,
            RespawnDelay = VersusDirector.RespawnDelay,
            BuildWrapupBoard = boards.BuildIaWrapupBoard,
            // Only the Original presentation has a wrap-up page to go to. Everywhere else, and on a
            // command-line launch with no menu behind it, the in-flight board takes the ending.
            WrapupToMenu = _menuDriven && _presentation == UI.Menu.PresentationId.Original
                ? _instantActionWrapup : null,
        });

        // World AA emplacements, after the zeppelins and the Instant Action turret arm above.
        _turretEmplacements = opposition.PlaceEmplacements(state, turretDefs, weaponDefs, projectiles,
            _zeppelins, iaZepTurretSwitch);
        // A race's ground stays inert: the pilots carry nothing to answer a gun with.
        if (worldBindings.Racing)
            _turretEmplacements?.SleepAll("the stunt race");

        // The campaign objectives graph (D31): armed once every runtime a directive can touch is
        // up, which is why it sits after the emplacement block rather than with the other
        // directors. It builds no node of its own.
        _worldRuntime = state.WorldRuntime;
        // Loaded before the graph is armed rather than with the readouts below: a SET_HELP_LABEL
        // write reaches a marker-carrying aircraft through the director, which needs the table.
        var objectiveMessages = _campaign != null ? Messages.Load(state.MessagesPath) : null;
        _campaign?.Attach(new CampaignDirector.WorldInputs
        {
            Runtime = state.WorldRuntime,
            Turrets = _turretEmplacements,
            Generators = _generators,
            Zeppelins = _zeppelins,
            SurfaceVehicles = _surfaceVehicles,
            // The campaign's danger-zone gates are chapter-world geometry, so the
            // tracker needs the built gamez to resolve its dzpathN subtrees.
            Gamez = state.Gamez,
            Strings = objectiveMessages,
            Sounds = state.WorldRuntime?.Sounds,
            Projectiles = _projectiles,
            ListenerPosition = () => _rigs.Count > 0 && _rigs[0].Controller is { } pilot
                ? pilot.WorldPosition
                : Vector3.Zero,
            PlayerAircraft = () => _rigs.Count > 0 ? _rigs[0].Controller : null,
            // P1's Danger Zone photograph, taken the way the stunt camera takes its own. It goes
            // through the pilot's posed eye, or, with no frames drawn, off the seat's pane.
            PlayerPane = landed => _rigs.Count != 0
                && (_rigs[0].Controller?.Photograph is { } eye && DangerZonePhotograph.Drawable
                    ? eye.Request(landed)
                    : PaneReadback.Request(_rigs[0].Viewport ?? (IsInsideTree() ? GetViewport() : null), landed)),
            Humans = HumanAircraft,
            Aircraft = AllAircraft,
            // The flight's Danger Zone praise (id 15), elected on P1's team. The original raises
            // it in the zone's completion routine, for the local player's vehicle alone.
            DangerZoneSpoken = () =>
            {
                if (voices.Voice is { } voice && _rigs.Count > 0 && _rigs[0].Controller is { } flown)
                {
                    voice.DangerZoneCompleted(flown);
                }
            },
            BeginSpectate = BeginCampaignSpectate,
            Rng = Rng.NewSystemRandom(Rng.Ai),
        });

        // The launch hook's CALLBACK codes, taken after the generator runtime's own bind so the
        // director sits ahead of it and the rest of the chain still answers everything else.
        if (state.WorldRuntime is { } callbackRuntime)
        {
            _campaign?.BindCallbackHost(callbackRuntime);
        }

        _wire.WireDirector(_campaign, _worldRuntime);
        _wire.WireWorld(state.WorldRuntime, new SessionNet.WorldLinkInputs
        {
            Zeppelins = _zeppelins,
            SurfaceVehicles = _surfaceVehicles,
            Campaign = _campaign,
            Generators = _generators,
            AiPlanes = () => AiPlanes,
            Voice = voices.Voice,
        });
        // Zeppelin vs Zeppelin, once the hulls, their pools and the world's wire stand.
        _dogfight?.WireZeppelinVersus(new VersusDirector.ZeppelinVersusWireInputs
        {
            ZrdrPath = state.ZrdrPath,
            Zeppelins = _zeppelins,
            SeatOfShooter = _wire.SeatOfShooter,
            Radio = voices.Radio,
            GroundAt = GroundSampler(),
        });
        _dogfight?.WireRearmBases(state.WorldRuntime, state.ZrdrPath, _zeppelins);

        _wire.WirePositionalStarts(_landings, _ladder, state.WorldRuntime);
        _wire.WireCutscenes(_cutscene);

        // F15, F16 and F17 over the live field (src/Launch/InspectionLabs.cs), read through
        // suppliers because waves, spawns and deaths change it long after this line runs.
        _labs!.BuildFlightDebugOverlays(_rigs,
            () => _turretEmplacements?.Emplacements ?? Array.Empty<TurretController>(),
            AllAircraft, () => _worldRuntime);

        // The objectives readouts and the site feed on the target cycles, once the graph is armed
        // (src/Launch/ObjectiveReadouts.cs).
        ObjectiveReadouts.Build(state, new ObjectiveReadouts.Inputs
        {
            Spec = _spec,
            Rigs = _rigs,
            Roster = flightRoster,
            Campaign = _campaign,
            CampaignStrings = objectiveMessages,
            PauseState = _pauseState!,
            SheetCarriesObjectives = boards.SheetCarriesObjectives,
            Stunting = stuntZones != null,
            Dogfight = _dogfight,
        });

        if (_rigs.Count > 1)
        {
            var flown = new List<string>(_rigs.Count);
            for (int pi = 0; pi < _rigs.Count; pi++)
                flown.Add($"P{pi + 1} '{iaOverride ?? HumanFieldPlanes.PlaneFor(_spec, pi)}'");
            state.What += $" + splitscreen {string.Join(", ", flown)}";
        }
        else
        {
            state.What += $" + '{iaOverride ?? _spec.PlaneName}' flying";
        }
    }

    // Dresses each staged cutscene prop in the livery of the aeroplane it stands in for: the
    // scheme its own roster block's rig resolved, else the one its vehicle def authors
    // (Mech3/AircraftStage.cs, StandIns). ⚠ Nothing here draws a default pattern of its own, and
    // nothing touches the paint RNG, which --det pins for every rig in the session.
    private void PaintStagedAircraft(BuildState state)
    {
        if (state.Aircraft is not { } stage || stage.PaintableNodes.Count == 0)
        {
            return;
        }

        var painted = new List<string>();
        foreach (var (node, block, def) in AircraftStage.StandIns)
        {
            PaintScheme? scheme = null;
            // The rig's own resolved livery first: it is the aeroplane the shot is about, and a rig
            // wearing the shipped skins (an enemy militia) must leave the prop wearing them too.
            if (block != null && _campaign is { } director
                && director.Roster.TryGetValue(block, out var rig))
            {
                scheme = rig.Scheme;
            }
            else if (def != null)
            {
                scheme = _liveryResolver.DefScheme(state.ZrdrPath, def);
            }

            if (stage.Paint(node, scheme, _liveryResolver.Patterns) is { } painter)
            {
                painted.Add($"'{node}' {painter.Scheme.Label} ({painter.PaintedSkins} skin(s))");
            }
        }

        string what = painted.Count > 0
            ? string.Join(", ", painted)
            : "no stand-in resolved a livery";
        Log.Info("world", $"aircraft stage paint: {what}");
    }

    // The "loaded ..." summary line and the per-pane/texture-census follow-ups, printed once the
    // whole build has finished.
    private void LogBuildSummary(BuildState state, Stopwatch sw)
    {
        Log.Info("world", $"loaded {state.What}: {state.Gamez.Nodes.Count} gamez nodes, {state.MeshInstances} mesh instances, {state.Colliders} colliders, {Mech3.SceneBuilder.ClampedSurfaceTotal} uv-clamped + {Mech3.SceneBuilder.EdgeClampedSurfaceTotal} edge-clamped surfaces, {sw.ElapsedMilliseconds} ms");
        if (_rigs.Count > 1)
            foreach (var rig in _rigs)
                Log.Info("flight", $"view P{rig.Index + 1}: layer {Mathf.Log(rig.VisualLayer) / Mathf.Log(2) + 1:0} cull 0x{rig.Camera.CullMask:X5}, sky={(rig.Horizon != null ? "own" : "none")} deck={(rig.Deck != null ? "own" : "none")} whiteout={(rig.Whiteout != null ? "own" : "none")}");
        // The authored-mip coverage, said out loud per chapter: a chapter never loads its whole
        // archive, so "installed N" alone cannot show whether a level was missed or simply unused.
        var tex = state.Textures;
        if (tex.AuthoredMipsAvailable > 0)
        {
            string refused = tex.AuthoredMipsRefused > 0 ? $", {tex.AuthoredMipsRefused} REFUSED" : "";
            Log.Info("world", $"[textures] authored mip levels: {(Mech3.TextureArchive.Mips == Mech3.TextureArchive.MipSource.Authored ? $"{tex.AuthoredMipsInstalled} installed on {tex.AuthoredMipTextures} texture(s), of {tex.AuthoredMipsAvailable} this archive ships{refused}" : $"off (--mips=generated); this archive ships {tex.AuthoredMipsAvailable}")}");
        }
        if (state.Textures.MissingTextures.Count > 0)
            Log.Info("world", $"[textures] {state.Textures.MissingTextures.Count} referenced texture(s) absent from this install: {string.Join(", ", state.Textures.MissingTextures)}");
    }

    // The Enhanced wave ocean, built on the first Enhanced frame that asks for waves. A switch back
    // to Original or to flat water drops it, and the sea sheet draws again because the shared
    // switch resets.
    private void FollowOcean()
    {
        if (!GraphicsMode.Enhanced || !WaterQualitySetting.DrawsWaves)
        {
            Drop(_ocean);
            _ocean = null;
            return;
        }
        if (_ocean != null || !_oceanEligible || _plane == null || _worldScene == null || _sessionTextures == null
            || !Effects.Ocean.Covers(_spec.Chapter))
            return;
        if ((_spec.SkippedPasses & EnhancedPasses.Ocean) != 0)
        {
            Log.Info("world", $"ocean: skipped (--no-ocean)");
            return;
        }
        var hulls = new HashSet<Node>();
        if (_surfaceVehicles != null)
        {
            foreach (var v in _surfaceVehicles.Vessels)
                hulls.Add(v.Body);
        }
        _ocean = Effects.Ocean.Create(_plane, _worldScene, _sessionTextures, OceanHulls, hulls, AnimatedMovers(hulls), _sea, _spec.OceanMaskPath);
        if (_ocean != null)
            _plane.AddChild(_ocean);
    }

    // The chapter's sea from the shipped file, read once per session that can stand an ocean, under
    // any --debug-ocean overrides. A chapter with no entry draws the defaults.
    private void ResolveSea()
    {
        if (!_oceanEligible || !Effects.Ocean.Covers(_spec.Chapter))
            return;
        _seaSaved = Effects.OceanSeas.Load().For(_spec.Chapter);
        _sea = _seaSaved.WithOverrides(_spec.DebugOcean);
        string debug = _spec.DebugOcean is { Length: > 0 } spec ? $" (--debug-ocean={spec})" : "";
        Log.Info("world", $"ocean: sea {_spec.Chapter} saved={_seaSaved.Describe()} drawn={_sea.Describe()}{debug}");
    }

    // The ocean lab's edit: the standing ocean takes it at once, and a rebuilt one takes it too.
    private void ApplySea(Effects.SeaState sea)
    {
        _sea = sea.Clamped();
        _ocean?.Apply(_sea);
    }

    // The world nodes the bound program's played motions carry, as the runtime resolves them. The
    // roster hulls are left out; they have their own source.
    private List<Node3D> AnimatedMovers(HashSet<Node> roster)
    {
        var movers = new List<Node3D>();
        if (_oceanRuntime is not { } runtime || _plane == null)
            return movers;
        var seen = new HashSet<Node3D>();
        foreach (var def in runtime.ProgramDefs)
        {
            var moving = Effects.OceanMovers.MovingEvents(def);
            if (moving.Count == 0)
                continue;
            foreach (var node in runtime.TargetsOf(def, moving))
            {
                if (node != _plane && GodotObject.IsInstanceValid(node) && !InRoster(node) && seen.Add(node))
                    movers.Add(node);
            }
        }
        var names = movers.Select(m => m.Name.ToString()).OrderBy(n => n, StringComparer.Ordinal);
        Log.Debug("world", $"ocean: movers resolved [{string.Join(", ", names)}]");
        return movers;

        bool InRoster(Node node)
        {
            for (Node? n = node; n != null; n = n.GetParent())
            {
                if (roster.Contains(n))
                    return true;
            }
            return false;
        }
    }

    // The roster hulls the ocean calms around, each over its own waterline.
    private IEnumerable<Node3D> OceanHulls()
    {
        if (_surfaceVehicles == null)
            yield break;
        foreach (var v in _surfaceVehicles.Vessels)
        {
            if (GodotObject.IsInstanceValid(v.Body) && v.Body.IsVisibleInTree())
                yield return v.Body;
        }
    }

    // Flight only: the inspection modes pick and edit single nodes, which a merged draw would not show.
    // ⚠ Keep it last in the build, after every placement and hide, so what stands visible is what
    // the mission shows.
    private void BuildWorldMerge(BuildState state)
    {
        if (!_spec.Fly || state.NodeSubtree != null || _plane == null || _worldScene == null)
            return;
        var parked = _unplacedWatch;
        _worldMerge = new Mech3.WorldMerge(_plane, _worldScene, state.WorldRuntime,
            () => ParkedAndDeck(parked));
        _plane.AddChild(_worldMerge);
        _worldMerge.Follow(GraphicsMode.Enhanced);
    }

    // What the merge leaves alone besides the runtime's claims: the parked vehicles and the cloud deck.
    // The deck follows the camera, and the weather rig sets its visibility per pane.
    private IEnumerable<Node3D> ParkedAndDeck(WorldBuilder? builder)
    {
        if (builder == null)
            yield break;
        foreach (var parked in builder.ParkedEntities)
            yield return parked;
        if (builder.CloudDeck is { } deck)
            yield return deck;
    }

    // The post-build framing pass: subject framing for the static views, then the inspection
    // overlays every observing mode carries (src/Launch/InspectionLabs.cs).
    private void FinishFraming(BuildState state)
    {
        // Only the static views frame their subject; flight and the spectator camera (both
        // --freecam and --anim-lab) place their own eye (FrameCamera would yank the freecam back
        // to the world's AABB orbit).
        if (!_spec.Fly && !_spec.Freecam && !_spec.AnimLab)
            FrameCamera(state.NodeAabb);

        _labs!.BuildOverlays(state, _plane, _rigs, _edgeExtender);
    }

    // The rof tree's menu sound folder, the shipped UI sounds a start count beeps with, or null where
    // this install's extraction carries none.
    private SoundArchive? MenuSounds()
    {
        string dir = RofTree.Member(_rofPath, "ASSETS/SOUNDS");
        return Directory.Exists(dir) ? new SoundArchive(dir) : null;
    }

    // The production ground sampler StartGrid probes its slots with: the world height under a point,
    // or null when the physics space answered nothing.
    // ⚠ Report null, never a fabricated height: the anchor is an authored, flyable point, and a
    // made-up correction would move a race field for no reason. This is the only place that knows
    // what an empty probe means. The warning below is a tripwire for a changed build order, not a
    // description of what happens today.
    private Func<Vector3, float?> GroundSampler()
    {
        // The probe column starts above the slot, so one fanned into a hillside finds the surface
        // rather than the terrain it is buried in. Generous on purpose: this runs a few times at
        // build, not per frame.
        const float ProbeAbove = 2000f;
        const float ProbeBelow = 20000f;

        bool warned = false;
        return at =>
        {
            if (GetWorld3D()?.DirectSpaceState is { } space)
            {
                // ⚠ World, NOT WorldAndAircraft: a placement pick stays blind to planes, or a later
                // caller probing while the field is airborne would stack a slot on an aircraft.
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    at + (Vector3.Up * ProbeAbove), at - (Vector3.Up * ProbeBelow),
                    CollisionLayers.World));
                if (hit.Count > 0)
                    return ((Vector3)hit["position"]).Y;
            }

            if (!warned)
            {
                warned = true;
                Log.Warn("flight", $"grid ground probe found nothing under a slot (probe {ProbeAbove:0}m up / {ProbeBelow:0}m down, mask=World). Measured, this does not happen at session build, so suspect the probe ran before the world stage was added rather than an empty map; the field keeps the spawn data's own altitude. Reported once per session.");
            }
            return null;
        };
    }

    // Every player's own position: the flown aircraft where a rig has a bound FlightController,
    // that rig's camera otherwise. ⚠ One shared snapshot behind both PlayerPositions consumers, so
    // EXECUTION_BY_RANGE and PLAYER_RANGE cannot answer "who is nearest" differently.
    private IReadOnlyList<Vector3> PlayerPositionsSnapshot()
    {
        if (_rigs.Count == 0 && _camera == null)
            return Array.Empty<Vector3>();
        int count = Math.Max(_rigs.Count, 1);
        if (_playerPositions.Length != count)
            _playerPositions = new Vector3[count];
        if (_rigs.Count == 0)
            _playerPositions[0] = _camera!.GlobalPosition;
        for (int i = 0; i < _rigs.Count; i++)
            _playerPositions[i] = _rigs[i].Controller is { } fc
                ? fc.GlobalPosition
                : _rigs[i].Camera.GlobalPosition;
        return _playerPositions;
    }

    // The anim runtime's range reads: every seat's aeroplane, a guest's copy and a bot included. A
    // range gate that starts a swap then counts a guest on every end. Without a seat roster it is
    // the panes' snapshot, which the per-viewer consumers keep in every session.
    private IReadOnlyList<Vector3> FieldPositionsSnapshot()
    {
        if (_wire.Seats.Count == 0)
            return PlayerPositionsSnapshot();
        int count = 0;
        foreach (var rig in _seatRigs)
        {
            if (rig.Controller != null || rig.Camera != null)
                count++;
        }
        if (_fieldPositions.Length != count)
            _fieldPositions = new Vector3[count];
        int i = 0;
        foreach (var rig in _seatRigs)
        {
            if (rig.Controller is { } fc)
                _fieldPositions[i++] = fc.GlobalPosition;
            else if (rig.Camera is { } cam)
                _fieldPositions[i++] = cam.GlobalPosition;
        }
        return _fieldPositions;
    }

    // The stick's half of _UnhandledInput's cutscene skip, for seat 1, who owns every stick. Ahead
    // of the controller's own tick, which is where a declined press's held state is re-read.
    private void PollStickSkip()
    {
        // No skip while the start is held: the film has not begun on the machines still loading.
        if (_stickSkip is not { } stick || _cutscene == null || StartHeld)
        {
            return;
        }

        stick.Poll();
        // The sticks are this machine's first player's, whose seat a key skip names too. On a
        // network guest that seat stands behind the host's, so it is not the owning index itself.
        int skipper = _rigs.Count > 0 ? _rigs[0].Index : Sticks.StickDeviceState.OwningSeat;
        if (stick.Pressed && _cutscene.Playing && _cutscene.TakeStickPress(skipper, () => stick.Held))
        {
            _split?.NoteSkip(skipper);
        }
    }

    // Which human that key or button belongs to. A pad is bound to exactly one seat by
    // Pads.AssignPads, and the keyboard is P1's alone (HumanFlightAdapter); an unmatched device is
    // the scripted player's, so a skip always has a skipper to name rather than a hole.
    private int SkipperIndex(InputEvent @event)
    {
        foreach (var rig in _rigs)
        {
            if (rig.Controller is not { IsHumanPiloted: true } pilot)
            {
                continue;
            }

            if (@event is InputEventJoypadButton pad
                ? pilot.PadDevices != null && System.Array.IndexOf(pilot.PadDevices, pad.Device) >= 0
                : pilot.UseKeyboard)
            {
                return rig.Index;
            }
        }

        return 0;
    }

    // Whether any human pilot is flying one of the two first-person views, the answer the anim
    // data's PLAYER_1ST_PERSON condition gets. Read per call, since the cycle key changes it
    // mid-session; false with no rigs bound (every non-flight mode, and the bootstrap passes).
    private bool AnyPilotFirstPerson()
    {
        for (int i = 0; i < _rigs.Count; i++)
            if (_rigs[i].Controller is { FirstPersonView: true })
                return true;
        return false;
    }

    // Whether the pilot this shooter id names is flying the full Cockpit view, which draws no
    // muzzle flash on that pilot's own guns. Keyed on the controller's own PlayerIndex, the id
    // every round is stamped with, so another aeroplane's flash is untouched.
    private bool PilotInCockpitView(int shooterId)
    {
        for (int i = 0; i < _rigs.Count; i++)
            if (_rigs[i].Controller is { CockpitView: true } c && c.PlayerIndex == shooterId)
                return true;
        return false;
    }

    // Creates this session's PlayerRigs, one per rendered view. One player keeps the main-viewport
    // camera and the default visual layers, so that render path is unchanged. Two or more build the
    // SplitScreen pane rig, each pane culling every other player's private sky/deck/puff layer.
    private void BuildRigs(int count)
    {
        // Photo mode belongs to the rigs and boards about to be replaced: leaving it engaged would
        // point a camera at a freed aircraft, and the old boards would linger in the suspend list.
        _boards?.ExitPhotoMode();
        _boards = null;
        _rigs.Clear();
        _split = null;
        FollowPaneShadows(count);
        if (count <= 1)
        {
            _camera.Current = true;
            // ⚠ Reopen the whole zone band before this session's first frame. The main camera is
            // the Launcher's and outlives the session, so it arrives carrying the last flight's
            // gate, and WeatherRig.Tick only ever NARROWS the band.
            _camera.CullMask = SplitScreen.PaneCullMask(Mech3.ZoneGate.OpenCullMask(_camera.CullMask));
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
                Fov = CameraController.ExternalFovDeg,
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
        Log.Info("flight", $"splitscreen: {count} panes sharing one world ({SplitScreen.LayoutName(count, GetViewport().GetVisibleRect().Size)})");
    }

    // The shadow level's fallback follows the pane count (ShadowQualitySetting.ResolveForPanes). An
    // integrated GPU's three- and four-pane sessions drop the sun's shadow pass unless a level was
    // chosen. The sun is the Launcher's and outlives the session, so the exit puts one pane back.
    // A one-pane session after another leaves the launch's resolution standing.
    private void FollowPaneShadows(int panes)
    {
        if (panes <= 1 && ShadowQualitySetting.Panes <= 1)
            return;
        string before = ShadowQualitySetting.Word;
        var plan = ShadowQualitySetting.ResolveForPanes(panes, _spec.ShadowQuality, _spec.Det);
        Log.Info("world", $"shadow quality at {panes} pane(s): shadow_quality={plan.Word} shadow_source={ShadowQualitySetting.Lookup.SourceName(plan.Source)}");
        if (plan.Word != before && IsInstanceValid(_sun))
            EnhancedLook.ApplyShadowQuality(_sun, GraphicsMode.Enhanced, _spec.SkippedPasses);
    }

    // A layer a switch drops leaves the tree now and is freed at the frame's end. ⚠ Do not QueueFree
    // alone: the node would draw, and count, for the rest of the frame.
    private void Drop(Node? node)
    {
        if (node == null)
            return;
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }

    // Each seat's wind streak field leaves the tree on the faithful path and comes back under
    // Enhanced; a seat with none gets one built. The seat keeps its field stepped, so a round trip
    // draws the drift and seeds a fresh one would. The roster's teardown frees it in or out of the
    // tree (FlightController.DetachRosterBindings).
    private void FollowWindStreaks()
    {
        foreach (var rig in _seatRigs)
        {
            if (rig.Controller is not { } controller)
                continue;
            var streaks = controller.WindStreaks;
            if (!GraphicsMode.Enhanced)
            {
                streaks?.GetParent()?.RemoveChild(streaks);
                continue;
            }
            if (streaks == null && Effects.WindStreaks.Create() is { } created)
            {
                if (rig.VisualLayer != 0)
                    SplitScreen.SetVisualLayer(created, rig.VisualLayer);
                controller.WindStreaks = streaks = created;
            }
            if (streaks != null && !streaks.IsInsideTree())
                _worldRoot!.AddChild(streaks);
        }
    }

    // Built at the flight build's projectile-pool step and again on a switch to original mode.
    // A no-op in enhanced mode (GroundShadowPass.Build).
    // The spyglass discs' shadowless sun, built under Enhanced and freed on the faithful path. A
    // flight session only, the one that builds the projectiles and the discs.
    private void FollowSpyglassSun()
    {
        if (GraphicsMode.Enhanced && _spyglassSun == null && _worldRoot != null && _projectiles != null)
        {
            _spyglassSun = Flight.Camera.SpyglassSun.Build(_sun);
            _worldRoot.AddChild(_spyglassSun);
        }
        else if (!GraphicsMode.Enhanced && _spyglassSun != null)
        {
            Drop(_spyglassSun);
            _spyglassSun = null;
        }
    }

    private void BuildGroundShadows() =>
        _groundShadows = GroundShadowPass.Build(_worldRoot!, AllAircraft, PlayerPositionsSnapshot, () => _rigs,
            () => _sky?.Weather?.SunlightRgb ?? WeatherRig.DefaultSunlightRgb);

    // One interior render pass per rig, on that player's own HUD parent. Splitscreen therefore gets
    // a pass per pane rather than one for the window (--no-cockpit-pass opts out). Built after the
    // rigs, since the interior it moves is the plane build's and the sun and environment it copies
    // are the session's. ⚠ An airframe swap rebuilds the interior and leaves this pass holding the
    // old node. The prototype hides itself rather than drawing a freed one (CockpitOverlay.Sync).
    private void BuildCockpitPasses()
    {
        if (!_spec.CockpitPass)
        {
            return;
        }
        foreach (var rig in _rigs)
        {
            if (rig.Controller is not { Dressing.Interior: { } interior } controller)
                continue;
            var overlay = Flight.Hud.CockpitOverlay.Build(rig.HudParent, interior, _sun, _env);
            controller.Dressing.Pass = overlay;
            // The overlay's cloned sun/env carry the zone live at its build; registering them
            // keeps a later mid-flight zone change reaching the interior pass too. Both modes,
            // since the faithful path's aircraft light moves per zone as well.
            if (overlay?.Sun != null)
                _sky?.Weather?.RegisterExtraLighting(overlay.Sun, overlay.Env);
        }
        Log.Info("flight", $"cockpit: interior drawn in its own pass at the origin for {_rigs.Count} rig(s)");
    }

    // A board menu's Restart item. An Instant Action or campaign mission is REBUILT by the
    // Launcher, because its opposition and objective state live in the world and nothing here can
    // put them back; every other mode reruns in place, the race and the match through their own
    // bookkeeping and anything else per-plane.
    private void Rerun()
    {
        if (_iaDirector != null || _campaign != null)
        {
            _restartSession();
            return;
        }
        if (_race is { Replicated: true })
        {
            // ⚠ Never on a guest: a window opened here would be one nobody else flies. The host's
            // rerun call opens the guest's (RerunRace through the race link).
            Log.Info("flight", $"stunt race: a new window is the host's to call");
            return;
        }
        if (_race is { } race)
        {
            RerunRace(race);
            return;
        }
        if (_dogfight is { } dogfight)
        {
            dogfight.Restart();
            return;
        }
        foreach (var rig in _rigs)
            rig.Controller?.Rerun();
    }

    // A new window from the shared race board (R), or on a network guest from the host's call.
    // Every pilot's runs are cleared, every plane here goes back to the start, and every seat opens
    // on the count again in one step. A host's call goes out first, ahead of the window's lines.
    private void RerunRace(StuntRace race)
    {
        Log.Info("flight", $"stunt race: a new window, every pilot's runs cleared");
        race.Rerun();
        _wire.Race?.CallRerun();
        IReadOnlyList<StartCountPhase>? opening = null;
        foreach (var rig in _rigs)
        {
            if (rig.Controller is not { } seat)
                continue;
            seat.Stunt?.Reset();
            seat.StuntShots?.Reset();
            seat.Respawn();
            // ⚠ Only a seat that carries a rerun count opens on one: under --det none does.
            if (seat.RerunCount != null)
            {
                opening = StartCount.Opening(RaceReadySeconds);
                seat.BeginStartCount(opening);
            }
        }
        race.BeginOpening(OpeningSeconds(opening));
    }

    // A stunt run races once it has a second seat, a network guest's included. A lobby Stunt Race
    // races even a host left alone.
    private bool IsRace() => _seatRigs.Count > 1 || _spec.MissionType == DogfightMissionType.StuntRace;

    // The race window the launch asked for. A --debug-scoreboard race closes it at once: its
    // staggered forced finishes then run as the final run, and the last wakes the board.
    private float RaceWindowSeconds() => _spec.DebugScoreboard ? 0f : _spec.StuntRaceMinutes * 60f;

    // Display Scores in every local pane, wherever a race or a Dogfight keeps scores. A seat is
    // named by its network callsign, else its splitscreen tag.
    private void AttachScores(StuntRace? race, Messages strings)
    {
        var source = new ScoresSource
        {
            Race = race,
            Match = _dogfight?.Match,
            Name = seat => _wire.Seats.FirstOrDefault(s => s.SeatIndex == seat) is { Callsign.Length: > 0 } net
                ? net.Callsign : SplitScreen.PlayerTag(seat),
            Carried = seat => _dogfight?.Flags?.Flags.Carried(seat) ?? 0,
            Words = OriginalScoresWords.From(strings),
        };
        if (!source.HasScores)
            return;
        bool original = _presentation == UI.Menu.PresentationId.Original;
        foreach (var pane in _rigs)
        {
            if (pane.Controller != null)
                pane.Controller.DebugHoldScores = _spec.DebugScores;
            ScoresOverlay.Attach(pane, source, original);
        }

        Log.Info("flight", $"display scores: {_rigs.Count} pane(s), {(original ? "the original's HUD text" : "the chrome table")}{(_spec.DebugScores ? ", held by --debug-scores" : "")}");
    }

    // Frames the parked plane in the orbit view. ⚠ --lookat is a POINT and is used verbatim;
    // --direction is only an aim, so a pivot is synthesized on the ray. Either way the orbit still
    // orbits the subject.
    private void FrameCamera(Aabb? subject = null)
    {
        // ⚠ Use the --node= stage's own box, measured before the labs joined the subtree: MeshLab
        // parks empty overlay meshes at the session origin, and a merge over the live tree would
        // stretch a distant subtree's box back to them.
        var aabb = subject ?? OrbitCamera.MergedAabb(_plane!);
        var pivot = _spec.LookAt;
        if (pivot == null && _spec.CamDir is { } dir)
        {
            if (_spec.CamPos is { } eye)
            {
                float ahead = Mathf.Max((aabb.GetCenter() - eye).Dot(dir), MinOrbitRadius);
                pivot = eye + dir * ahead;
                Log.Info("core", $"orbit pivot from --direction: --lookat={Tooling.CaptureDirector.Vec3Arg(pivot.Value)} radius={ahead:0.###}");
            }
            else
            {
                // No eye: keep the AABB pivot and the framing distance Frame derives, placing the
                // eye along the aim.
                _orbit.Pitch = Mathf.Asin(Mathf.Clamp(-dir.Y, -1f, 1f));
                _orbit.Yaw = Mathf.Atan2(-dir.X, -dir.Z);
                Log.Info("core", $"orbit pivot from --direction: subject centre, eye swung to the aim");
            }
        }
        _orbit.Frame(aabb, _spec.CamPos, pivot);
    }

    // The mission-script host's airframe swap (callback codes 965 to 967). The episode owner's rig,
    // which is the human whose trigger started the episode: the original has one player vehicle and
    // the codes name it, and with a field the aeroplane it names is the one that earned the swap.
    // An episode nobody claimed is the scripted player's, so a 1P mission swaps exactly what it
    // swapped before. A failed swap is reported rather than thrown: the player keeps the aircraft
    // the exception left them without, and the mission goes on.
    private AirframeSwapResult SwapPlayerAirframe(AirframeSwapOrder order)
    {
        if (_flightRoster == null || _rigs.Count == 0)
        {
            return default;
        }

        try
        {
            var owner = order.Owner ?? _rigs[0];
            var result = _flightRoster.RunSwap(owner, order,
                AirframeHandover.Resolves(_spec.Chapter, _spec.Mission));
            _wire.RewireSeat(owner, SkipSwapRewire);

            return result;
        }
        catch (Exception e)
        {
            Log.Warn("flight", $"airframe swap to '{order.Airframe.PlaneNode}' failed: {e.Message}");
            return default;
        }
    }

    // --debug-spectate: build the whole session as it would be flown, then take every human out of
    // it, so the AI can be watched with nobody provoking it. Each human aircraft goes Held and
    // Inert, and its pane takes a SpectatorCamera following the first AI aircraft.
    // ⚠ Run this AFTER BuildFlightRigs: the wingman fan, the ace's spawn draw and wave 1's
    // placement all read the player's position, so removing the player earlier moves what is
    // being watched. The mission's own end conditions are untouched.
    private void ApplyDebugSpectate()
    {
        if (!_spec.DebugSpectate)
        {
            return;
        }
        FlightController? follow = null;
        foreach (var ai in AiPlanes)
        {
            if (ai is { InPlay: true })
            {
                follow = ai;
                break;
            }
        }
        foreach (var rig in _rigs)
        {
            if (rig.Controller is not { } pilot)
            {
                continue;
            }
            var eye = pilot.WorldPosition + Vector3.Up * 30f;
            pilot.Held = true;
            pilot.Inert = true;
            pilot.CameraOwned = true;   // The controller writes this pane's camera no more.
            // No hand-back leg: the mode holds the pane for the rest of the session.
            pilot.SetViewedFromOutside(true);
            // The cockpit instruments belong to an aircraft nobody is flying; the marker HUD is a
            // sibling on the same canvas and stays, which is the whole point of the mode.
            pilot.PilotHud.SetInstrumentsVisible(false);
            var spectator = new SpectatorCamera(rig.Camera, eye,
                follow != null ? follow.WorldPosition : eye - rig.Camera.Basis.Z)
            {
                ShowReadout = _rigs.Count == 1,   // one pane, so the freecam readout has room
                LockCandidates = LockCandidateAircraft,
            };
            _worldRoot!.AddChild(spectator);
            if (follow != null)
            {
                spectator.FollowNode(follow);
            }
        }
        Log.Info("flight", $"--debug-spectate: {_rigs.Count} human(s) pinned, inert and untargetable; {(follow != null ? $"camera following {follow.Name}" : "camera free at the spawn")} ({AiPlanes.Count} AI aircraft flying)");
    }

    // A co-op campaign human whose aircraft is lost while the others fly on uses the same
    // hand-off Instant Action's last life runs, with the campaign's own loss rule deciding when.
    private void BeginCampaignSpectate(FlightController pilot)
    {
        foreach (var rig in _rigs)
        {
            if (!ReferenceEquals(rig.Controller, pilot))
            {
                continue;
            }

            // The seats, so a pane whose pilot is down can follow a guest flown elsewhere.
            SpectateHandoff.Begin(rig, _seatRigs, _worldRoot!, LockCandidateAircraft,
                _spectatorCameras, out var follow);
            Log.Info("core", $"campaign: P{rig.Index + 1}'s pane is spectating{(follow != null ? $", following P{follow.PlayerIndex + 1}" : " from the crash camera")}");
            return;
        }
    }

    /// <summary>What a <see cref="SpectatorCamera"/>'s target key may lock onto: every aircraft
    /// still in play, AI and human alike. Rebuilt per press, so a plane that has since been shot
    /// down or stood down (<see cref="FlightController.InPlay"/> covers crashed and inert both)
    /// drops out without anyone pruning a list. Empty on a stage with no aircraft, which leaves
    /// the key inert rather than special-cased.</summary>
    private IReadOnlyList<Node3D> LockCandidateAircraft()
    {
        _lockCandidates.Clear();
        foreach (var ai in AiPlanes)
            if (ai is { InPlay: true })
                _lockCandidates.Add(ai);
        foreach (var rig in _seatRigs)
            if (rig.Controller is { InPlay: true } pilot)
                _lockCandidates.Add(pilot);
        return _lockCandidates;
    }

    // Parent-driven clock adapter. Debug forces stay outside the session-simulation order as
    // outer-frame input injection; each clock substep below enters the same module as realtime.
    private void DriveParentSimulation(GameClock clock)
    {
        // The mission-ending hold rejects outer-frame input before every request enters the same
        // SessionSimulation admission path as realtime.
        if (_campaign?.Leaving != true)
        {
            // --crash[=frame]: force every player's crash rig at a fixed sim frame, the only
            // headless trigger for a crash a live collision otherwise gates. Spawned AI planes
            // crash too, while an inert one declines, since DebugForceCrash is gated on InPlay.
            if (_spec.CrashFrame is int crashFrame && !_crashFired && clock.Frame >= crashFrame)
            {
                _crashFired = true;
                foreach (var rig in _rigs)
                    rig.Controller?.DebugForceCrash();
                foreach (var plane in AiPlanes)
                    plane.DebugForceCrash();
            }
            // --debug-pause[=frame]: the scripted Start press, so a --screenshot catches the pause
            // screen. Player 0 owns it, as a solo press would. Same single-fire shape as --crash.
            if (_spec.DebugPauseFrame is int pauseFrame && !_debugPauseFired && clock.Frame >= pauseFrame)
            {
                _debugPauseFired = true;
                _pauseState?.TryToggle(0);
            }
            // --debug-scoreboard: each mode director's single-fire force, the Dogfight's a scripted
            // kill and Instant Action's attributed to P1, so a results board reads non-zero.
            // Which modes have a force at all: docs/architecture.md on GameSession.cs.
            if (_spec.DebugScoreboard)
            {
                _dogfight?.ForceDebugScoreboard();
                _iaDirector?.ForceDebugScoreboard();
            }
        }
        for (int i = 0; i < clock.Steps; i++)
        {
            // Per substep and before it, the same order the realtime adapter takes.
            _wire.Step(clock.Dt);
            _simulation?.Step(clock.Dt);
        }

        _campaign?.TraceObjectives(_rigs.Count > 0 ? _rigs[0].Controller : null);
    }

    // The mission-end hold presents one unchanged flown frame: the session and authored-animation
    // clocks both stay quiet while CampaignDirector alone counts down the hand-off.
    private void HoldEndingFrame()
    {
        if (_clock == null)
        {
            return;
        }
        _clock.SimHeld = true;
        _clock.AuthoredAnimationHeld = true;
    }

    // The surface-vehicle runtime, built once on the first roster or generator that can need one
    // and only where a chapter world exists to copy a hull out of. Null on a bare stage.
    private SurfaceVehicleRuntime? EnsureSurfaceVehicles(BuildState state)
    {
        if (_surfaceVehicles != null)
        {
            return _surfaceVehicles;
        }
        if (state.Gamez is not { } gamez || state.WorldScene is not { } scene
            || state.WorldRuntime is not { } runtime || _worldRoot == null)
        {
            return null;
        }
        _surfaceVehicles = new SurfaceVehicleRuntime(gamez, scene, runtime,
            VehicleDefs.Load(state.ZrdrPath), runtime.WorldRoot ?? _worldRoot)
        {
            Strings = Messages.Load(state.MessagesPath),
        };
        _worldRoot.AddChild(_surfaceVehicles);
        return _surfaceVehicles;
    }

    // Maps the simulation's named phases onto this session's concrete owners.
    private sealed class SessionSimulationRuntime(GameSession session) : ISessionSimulationRuntime
    {
        private readonly List<FlightController> _eligibleAiAircraft = new();

        public bool SimHeld => session._clock?.SimHeld ?? false;
        public bool EndingHold => session._campaign?.Leaving ?? false;

        public void StepEndingHold(float dt)
        {
            session.HoldEndingFrame();
            session._campaign?.Step(dt);
        }

        public void CaptureAiAircraft()
        {
            _eligibleAiAircraft.Clear();
            _eligibleAiAircraft.AddRange(session.AiPlanes);
            session._wire.World?.Admit(session.AiPlanes);
        }

        public void StepIncomingFire(float dt) => session._incomingFire?.SimStep(dt);
        public void StepProjectiles(float dt) => session._projectiles?.SimStep(dt);

        // Every SEAT, not every pane: a seat flown elsewhere steps here too, which is where its
        // received history is advanced and read (FlightController.RemoteOwned). Outside a network
        // match the two lists hold the same rigs.
        public void StepHumanAircraft(float dt)
        {
            foreach (var rig in session._seatRigs)
                rig.Controller?.SimStep(dt);
            // After every seat: a run started this step was started inside the window it saw.
            session._race?.Advance(dt);
            session._wire.Race?.Step();
            session._wire.BroadcastAircraftState();
            session._wire.TraceStep();
        }

        public void StepZeppelins(float dt) => session._zeppelins?.SimStep(dt);
        public void StepTurretEmplacements(float dt) => session._turretEmplacements?.SimStep(dt);
        public void StepGenerators(float dt) => session._generators?.SimStep(dt);
        public void StepSurfaceVehicles(float dt) => session._surfaceVehicles?.SimStep(dt);

        // The one walk --perf's ai_ms measures (src/Utils/AiStepCost.cs): every AI aircraft's whole
        // sim step, bracketed here rather than per aircraft so the count it divides by is the
        // membership captured at step entry.
        public void StepCapturedAiAircraft(float dt)
        {
            AiStepCost.Open();
            foreach (var aircraft in _eligibleAiAircraft)
                aircraft.SimStep(dt);
            AiStepCost.Close(_eligibleAiAircraft.Count);
            session._wire.World?.StepSends();
        }

        public void StepLandingApproaches()
        {
            session._landings?.Tick();
            session._wire.Starts?.Step();
            if (session._landings is not { } landings)
            {
                return;
            }

            // Per pane: only the human inside the sphere is shown the prompt, and only their own
            // button starts the row.
            foreach (var rig in session._rigs)
            {
                if (rig.Controller is { } flown)
                {
                    flown.AutoLandOffered = landings.OffersAutoLandTo(rig.Index);
                }
            }
        }

        public void StepInstantAction(float dt) => session._iaDirector?.Step(dt);
        public void StepCampaign(float dt)
        {
            session._campaign?.Step(dt);
            if (EndingHold)
            {
                session.HoldEndingFrame();
            }
        }
        public void StepRadio(float dt) => session._voices?.Radio?.Tick(dt);
        public void StepSmokeScreens(float dt) => session._smokeScreens?.SimStep(dt);
        public void StepBeeperTags(float dt) => session._beeperTags?.SimStep(dt);
        public void StepAiVoice(float dt) => session._voices?.Voice?.Step(dt);
        public void StepVersus(float dt)
        {
            if (session._dogfight is not { } dogfight)
                return;
            // Ahead of the match clock, so a flag that ends the match is sent out on this step.
            dogfight.Flags?.Step(dt);
            dogfight.RearmPlay?.Step();
            dogfight.StepMatch(dt);
        }
    }
}
