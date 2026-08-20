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
/// The per-launch session node: one aircraft from the player's own extracted game data under orbit
/// controls, or with <c>--fly</c> free flight over the chapter world.
///
/// <see cref="Launcher"/> (Main.tscn's root) owns the bootstrap, the launchscreen and the
/// persistent camera/lighting, and instantiates one of these per launch from the settled
/// <see cref="SessionSpec"/> plus a <see cref="LauncherContext"/>. Menu and CLI share StartSession.
///
/// ⚠ Do not add a Teardown(); return-to-menu is a bare QueueFree and the session subtree frees
/// atomically under <c>_worldRoot</c>. ⚠ Do not parse an arg here: a new flag is a SessionSpec
/// change. Module entry: docs/architecture.md on src/Session/GameSession.cs.
/// </summary>
public partial class GameSession : Node3D
{
    private const float HorizonScale = 2.5f;

    // The fraction of the camera's far plane the scaled skydome may reach; past it the dome clips
    // and the clear colour shows through. 0.9 leaves room for the one-frame anchor lag.
    // See HorizonScaleFor.
    private const float HorizonFarFraction = 0.9f;

    // How many near-miss names a failed --node= lookup offers: a usable hint, not a census.
    private const int NodeSuggestCap = 20;

    // Smallest orbit radius a synthesized pivot may sit at, so an aim ray passing behind the
    // subject still leaves something to orbit rather than spinning about the eye.
    private const float MinOrbitRadius = 1f;

    // Seconds a downed Versus player watches the crash cam before auto-respawning (R skips early).
    // Respawn is at the player's own spawn point, full HP/ammo, no invulnerability window.
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
    // The panes handed to a spectator when their pilot ran out of lives, so a rerun can take them
    // back. Freed with this node otherwise.
    private readonly List<SpectatorCamera> _spectatorCameras = new();
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
    // Every pane's camera, bound once right after BuildRigs — the session-owned "what do the
    // cameras see" registry draw rules read instead of `_rigs[0]`/`GetViewport().GetCamera3D()`.
    // ProjectilePool.Viewers takes this same instance; B11/B13 are its next consumers.
    private readonly ViewerSet _viewers = new();

    // Every AI aircraft spawned into this session — stepped in DriveSimSteps after the
    // player rigs, freed with the world subtree.
    private readonly List<FlightController> _aiPlanes = new();
    // scratch: the rigs' controllers plus _aiPlanes, rebuilt on every AllAircraft() call
    private readonly List<FlightController> _aircraftScan = new();
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
    // launched into the menu → the boards' Exit item returns there, not quit
    private readonly bool _menuDriven;
    // The boards' Exit item, routed by the Launcher (launchscreen or quit).
    private readonly Action _exitSession;
    // The boards' Restart item on an Instant Action mission: the Launcher frees this session and
    // builds a fresh one. Nothing here can put a mission's opposition back on its own.
    private readonly Action _restartSession;
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
    // --debug-wash=N: how many of its two scripted washes have fired — see DriveSimSteps.
    private int _debugWashesFired;
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
    // The FBFX_COLOR_FROM_TO wash — one ramp per rendered view, painted into the pane(s) the burst
    // was near.
    private UI.ScreenFlash? _screenFlash;
    // The active smoke screens (D18): laid by the SMOKE_SCREEN fire path, walked over every rig
    // and AI plane each sim step, washing humans through _screenFlash and stunning AI pilots.
    private SmokeScreens? _smokeScreens;
    // The beeper tags (B8/B9): the projectile pool tags on a BEEPER hit and asks for a seeker's
    // target; the list itself counts down here, after every aircraft, in both step paths.
    private BeeperTags<FlightController>? _beeperTags;
    private Node3D? _plane;
    // The session's simulation clock (see GameClock). Also published as GameClock.Current, which
    // is how the sim consumers scattered through the tree reach it; dropped by ReturnToMenu.
    private GameClock? _clock;
    // The physics-stepped consumers this node drives itself when the clock is not realtime, in
    // the tree order Godot's physics tick would have used. Dropped by ReturnToMenu.
    private ProjectilePool? _projectiles;
    private IncomingFire? _incomingFire;   // --incoming: the near-miss test rig
    // The flight roster builds the human field and introduces AI aircraft later. Every AI it
    // returns is stepped in DriveSimSteps after the player rigs and freed with the world.
    private FlightRoster? _flightRoster;
    private AiSkills? _aiSkills; // ai_skill_parameters, loaded once on the first AI spawn
    // The roster's own AI stats reader, so a spawn can consult the def it is about to fly (pilot
    // skills, accent) before the roster builds it. Same cache: the read here is not a second parse.
    private Func<string, string?, PlaneStats>? _aiStatsFor;
    private Dictionary<string, string>? _militiaPatterns; // militia -> its paint pattern
    private List<Maneuver>? _aiManeuvers; // the D13 library, loaded once for the D11 machines
    private bool _noAssistLogged; // the one-per-session --no-assist breadcrumb
    // The E16 voice dispatch (built with the rigs when the world has sounds; its mission clock
    // steps in DriveSimSteps). Null in a soundless/world-less session — chatter simply off.
    private AiVoiceRuntime? _aiVoice;
    // The egen enemy generators (--generators): loaded with the rigs, stepped in
    // DriveSimSteps before the AI planes it spawns into _aiPlanes, freed with the world subtree.
    private AiGeneratorRuntime? _generators;
    private ZeppelinRuntime? _zeppelins;
    // The active Instant Action mission, loaded once at the top of
    // StartSession from --ia=<path> — null outside one, which is what keeps every other session
    // mode (free flight, Dogfight) untouched by its existence.
    private InstantActionRuntime? _instantAction;
    // E11's wave sequencer: null on dogfight_ace (every wave count is forced to 0) or outside an
    // Instant Action mission. _iaWaveRosters[w] is wave w+1's built (inert until activated)
    // members, indexed the same way; DriveSimSteps polls the current wave's alive count and
    // activates whatever InstantActionWaves.Step hands back — through the teleport arm, or (F12,
    // zeppelin_run) through the zeppelin generator's own wave credit.
    private InstantActionWaves? _iaWaves;
    private List<FlightController>[]? _iaWaveRosters;
    private List<SpawnPoint>? _iaWaveSpawnList;
    // F12: the wave whose parked members the objective zeppelin's generator is releasing — the
    // decoded group stamp (FUN_0045b9d0 writes the new counter to the generator's +0x64). 0
    // outside a zeppelin run, or before the first wave becomes current.
    private int _iaLaunchWave;
    // G14: the authored ace, kept as a field (not just BuildFlightRigs' own local) so
    // --debug-scoreboard can force it down from DriveSimSteps, well after every Downed
    // subscription the end-condition block wires is in place. Null outside dogfight_ace.
    private FlightController? _iaAce;
    // --debug-scoreboard (IA): single-fire, same shape as _crashFired/_versusDebugKillFired.
    private bool _iaDebugForceFired;
    // The world AA emplacements: built with the rigs whenever a chapter world and the
    // shared pool exist, stepped in DriveSimSteps after the zeppelins (slung mounts read the
    // moved pose). Shipped ACTIVATED honoured; --wake-turrets is the WAKEUP_TURRETS stand-in.
    private TurretEmplacementRuntime? _turretEmplacements;
    // The dogfight scorekeeping (--vs): built with the rigs, fed their Downed reports, its clock
    // advanced on the sim dt (never wall time). Null outside Versus — the Downed events then
    // simply have no subscriber. Freed with this node; flight holds no match state.
    private VersusMatch? _versus;
    // The stunt race (--stunt with several pilots), for the same reason: a rerun resets it rather
    // than each pilot's own run. Null outside a race.
    private StuntRace? _race;
    // One menu reader per player, built with that player's own pad binding, so a board menu can be
    // driven by its owner alone. Null before the rigs exist.
    private UI.MenuInput[]? _menuInputs;
    // Who is holding the sim clock and why — shared by every rig and by every board that halts.
    // Null before the rigs exist.
    private PauseState? _pauseState;
    // The trailer-target resolver every net follower this session builds shares. Built
    // with the rigs (it needs the player rig), so the F13 overlay, built earlier, reads it through
    // this field rather than holding a reference it could not have had yet.
    private NetTrailerTargets? _netTrailers;
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
        _exitSession = ctx.ExitSession;
        _restartSession = ctx.RestartSession;
    }

    /// <summary>Whether the build completed — the Launcher's Esc routing reads it (return to the
    /// launchscreen only once a world is actually up).</summary>
    public bool InSession { get; private set; }

    /// <summary>The session's per-player rigs — the Launcher's F11 placement print reads them.</summary>
    internal List<PlayerRig> Rigs => _rigs;

    /// <summary>The session's subject plane (null until the build lands one) — the Launcher's
    /// capture tick reads it, because CaptureDirector only shoots once a plane exists.</summary>
    internal Node3D? Plane => _plane;

    // ⚠ Do not spell "does this session build colliders" any other way; this is the one definition
    // the labs and the C overlay read, so they cannot disagree with what WorldSession built.
    private bool BuildsCollision => _spec.BuildsCollision;

    // Whether P / . may halt this session. Splitscreen flight says no: the freeze halts the shared
    // world, so it is not one player's to press.
    private bool HaltAllowed => !_spec.Fly || _rigs.Count == 1;

    /// <summary>Spawns an AI-piloted aircraft into this session at runtime, any time after the
    /// flight build. Null when this session built no flight rigs. This is the <c>--ai=</c>
    /// overload: no authored identity beyond <paramref name="aiDef"/>, whose militia scheme the
    /// aircraft then wears in place of the Fortune Hunters default.
    /// ⚠ Route an authored enemy through the other overload instead; it carries the livery, team,
    /// rating and <c>shippedSkins</c> a mission's actor needs.</summary>
    public FlightController? SpawnAiAircraft(string planeName, Vector3 pos, Vector3 lookAt,
        AiPilot pilot, string? aiDef = null) =>
        SpawnAiAircraft(planeName, pos, lookAt, pilot, scheme: null, team: null, attackRating: null,
            aiDef: aiDef);

    /// <summary>As the four-parameter overload, plus an authored actor's identity.
    /// <paramref name="scheme"/>/<paramref name="team"/> are worn as-is, with no RNG draw;
    /// <paramref name="attackRating"/> arms the gunner and mode machine at that rating regardless
    /// of <c>--ai-attack=</c>; <paramref name="inert"/> builds the aircraft fully wired but taking
    /// no step until <see cref="FlightController.Activate"/>; <paramref name="shippedSkins"/> keeps
    /// its own textures, for an actor flying for a militia the mission data never names.</summary>
    public FlightController? SpawnAiAircraft(string planeName, Vector3 pos, Vector3 lookAt,
        AiPilot pilot, PaintScheme? scheme, int? team, int? attackRating, bool inert = false,
        bool shippedSkins = false, string? aiDef = null, Flight.LoadoutChoice? fit = null)
    {
        if (_flightRoster == null)
        {
            GD.PushWarning($"ai: no spawner in this session mode — '{planeName}' not spawned");
            return null;
        }
        // The vehicle def's own nine-slot pilot vector, when this spawn resolves one. A slot it
        // authors is the rating that slot's consumer flies at, unless --ai-attack=N pinned one.
        var defStats = AiStatsForSpawn(planeName, aiDef);
        var defSkills = defStats?.AiPilotSkills ?? default;
        int SkillFor(int? authored, int fallback) =>
            _spec.AiAttackSkillExplicit || attackRating != null ? fallback : authored ?? fallback;

        // --ai-attack arms every spawned pilot with a D14 gunner at the ordered skill rating:
        // interpolated dead-eye/quick-draw cones, nearest-hostile auto-targeting, its own
        // seeded scatter stream. A pilot armed by its caller keeps what it was given.
        if ((attackRating ?? _spec.AiAttackSkill) is { } skill && pilot.Gunner == null)
        {
            try
            {
                _aiSkills ??= AiSkills.Load(_zrdrPath);
                var rng = new RandomNumberGenerator { Seed = (ulong)(uint)Rng.NewIntSeed(Rng.Ai) };
                pilot.Gunner = new AiGunner(rng)
                {
                    DeadEyeAngleDeg = _aiSkills.DeadEyeAngleDeg(SkillFor(defSkills.DeadEye, skill)),
                    QuickDrawAngleDeg = _aiSkills.QuickDrawAngleDeg(SkillFor(defSkills.QuickDraw, skill)),
                };
                // The ordnance half rides the quick-draw slot, on its own draw off the ai stream so
                // the launch dice and the dead-eye scatter cannot walk each other's sequence.
                var ordRng = new RandomNumberGenerator { Seed = (ulong)(uint)Rng.NewIntSeed(Rng.Ai) };
                int quickDraw = SkillFor(defSkills.QuickDraw, skill);
                pilot.Rocketeer = new AiRocketeer(ordRng.Randf)
                {
                    QuickDrawAngleDeg = _aiSkills.QuickDrawAngleDeg(quickDraw),
                    QuickDrawChance = _aiSkills.QuickDrawChance(quickDraw),
                };
                // Names the ratings actually flown, not the session default: with a def's own slots
                // in play the two differ, and a reader comparing cones needs the numbers behind them.
                GD.Print($"ai: gunner armed at dead-eye {SkillFor(defSkills.DeadEye, skill)} / " +
                         $"quick-draw {quickDraw} (dead-eye {pilot.Gunner.DeadEyeAngleDeg:0.00}°, " +
                         $"quick-draw {pilot.Gunner.QuickDrawAngleDeg:0}°, " +
                         $"ordnance roll {pilot.Rocketeer.QuickDrawChance:0.00} per {pilot.Rocketeer.RefireSeconds:0} s)");
            }
            catch (Exception e)
            {
                GD.PushWarning($"--ai-attack: cannot load ai_skill_parameters: {e.Message}");
            }
        }
        // The mode machine on every spawned pilot, unless its caller already gave it one. The
        // spawner adds the vehicle def's attack/return ranges; the controller wires the terrain
        // probe.
        if (pilot.Machine == null)
        {
            try
            {
                _aiSkills ??= AiSkills.Load(_zrdrPath);
                _aiManeuvers ??= Maneuvers.Load(_zrdrPath);
                int rating = attackRating ?? _spec.AiAttackSkill ?? 5;
                int sixthSense = SkillFor(defSkills.SixthSense, rating);
                pilot.Machine = new AiModeMachine(Rng.NewSystemRandom(Rng.Ai))
                {
                    ActivationRange = _aiSkills.MinAiActiveDist,
                    SteadyHandChance = _aiSkills.At("steady_hand_chance", SkillFor(defSkills.SteadyHand, rating)),
                    SixthSenseChance = _aiSkills.At("sixth_sense_chance", sixthSense),
                    SixthSenseFactor = _aiSkills.At("sixth_sense_factor", sixthSense),
                    StunRecoveryIntervalS = _aiSkills.At("stun_recovery_interval", SkillFor(defSkills.StunRecovery, rating)),
                    NaturalTouch = SkillFor(defSkills.NaturalTouch, rating),
                    Library = _aiManeuvers,
                    AssistEnabled = !_spec.NoAssist,
                };
                if (_spec.NoAssist && !_noAssistLogged)
                {
                    _noAssistLogged = true;
                    GD.Print("ai assist: off (--no-assist): lay off disabled, pursue only");
                }
            }
            catch (Exception e)
            {
                GD.PushWarning($"ai: no mode machine — cannot load skills/maneuvers: {e.Message}");
            }
        }
        var ai = _flightRoster.SpawnAi(new AiSpawn(planeName, pos, lookAt, pilot, scheme, team,
            inert, shippedSkins, aiDef, fit));
        _aiPlanes.Add(ai);
        ai.SmokeScreens = _smokeScreens;   // a shipped AI smoker lays through the same fire path
        // Mode transitions and reaction rolls, in the engine's own vocabulary — the D11
        // observability lines. Through Log (not GD.Print) so a play session's file sink
        // (.scratch/logs/<mode>-<stamp>.log) carries them for post-flight reading.
        if (pilot.Machine is { } modes)
        {
            string tag = ai.Name;
            modes.ModeChanged += (from, to, why) => Log.Info("flight",
                $"ai mode: {tag}: {AiModeMachine.NameOf(from)} -> {AiModeMachine.NameOf(to)} ({why})");
            modes.RollLogged += line => Log.Info("flight", $"ai roll: {tag}: {line}");
        }
        ai.Downed += (victim, killer) => Log.Info("flight",
            $"ai: {ai.Name} downed (shooter id {victim}, killer {killer?.ToString() ?? "none"})");
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
            _ambience, PlayerPositionsSnapshot);
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
        _camera.Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? 62 : 50;
        // One rig per rendered view, before anything camera-anchored is built (the skydome and
        // weather visuals below are per-rig). Single player reuses the main-viewport camera.
        BuildRigs(_spec.Fly ? _spec.Players : 1);
        // ⚠ Do not let a draw-rule consumer re-derive its camera set from _rigs; every one shares
        // this single registration so they cannot disagree about what the cameras see.
        _viewers.Bind(_rigs.Count > 0 ? _rigs.Select(r => r.Camera)
            : _camera != null ? new[] { _camera } : System.Array.Empty<Camera3D>());
        // The FBFX_COLOR_FROM_TO wash: one ramp per rendered view, built as soon as the rigs exist
        // so every runtime below takes the same sink. The viewer set goes with it because the
        // routing rule is per pane, and one rig list builds both index-aligned.
        _screenFlash = UI.ScreenFlash.Build(_rigs.Select(r => r.HudParent), _viewers);
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
            GD.PushWarning($"smoke screen: player.json unavailable ({e.Message}) — flying on the image defaults");
            smokeTunables = SmokeScreenTunables.Image;
        }
        var screenFlashSink = _screenFlash;
        _smokeScreens = new SmokeScreens(smokeTunables, AllAircraft,
            (playerIndex, colour, weight, duration) => screenFlashSink.PlayBlend(playerIndex, colour, weight, duration));
        // A fresh list per build: a tag never outlives the session that painted it.
        _beeperTags = new BeeperTags<FlightController>();

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
        state.GamezPath = _spec.Gamez
            ?? (_spec.WorldMode ? SessionPaths.ChapterGamez(_dataRoot, _spec.Chapter) : _planesGamezPath);
        state.MissionZrdrPath = SessionPaths.MissionZrdr(_dataRoot, _spec.Chapter, _spec.Mission);

        // ⚠ Keep both producers (the wizard's SessionSpec.IaDef and --ia=<path>) converging on the
        // one InstantActionRuntime construction; two similar calls is the failure this avoids.
        // A failed --ia= load warns and flies without a mission rather than aborting the launch.
        _instantAction = null;
        _iaWaves = null;
        _iaWaveRosters = null;
        _iaWaveSpawnList = null;
        _iaLaunchWave = 0;
        _iaAce = null;
        _iaDebugForceFired = false;
        if (_spec.IaDef is { } wizardDef)
        {
            _instantAction = new InstantActionRuntime(wizardDef);
            GD.Print($"ia: wizard mission_type={wizardDef.MissionType} " +
                     $"player='{wizardDef.PlayerPlane}' ace='{wizardDef.AceName}' ({wizardDef.AcePlane})");
        }
        else if (_spec.IaPath != null)
        {
            try
            {
                var def = InstantAction.LoadFromJson(_spec.IaPath);
                _instantAction = new InstantActionRuntime(def);
                GD.Print($"ia: '{_spec.IaPath}' mission_type={def.MissionType} " +
                         $"player='{def.PlayerPlane}' ace='{def.AceName}' ({def.AcePlane})");
            }
            catch (Exception e)
            {
                GD.PushWarning($"--ia={_spec.IaPath}: cannot load ({e.Message}) — flying without a mission");
            }
        }

        Stopwatch sw;
        try
        {
            sw = Stopwatch.StartNew();
            LoadArchives(state);
            // The SOUND archive is scoped to this build everywhere but the lab, whose node owns its
            // disposal. ⚠ Do not scope the TEXTURE archive here: the world runtime bakes puffers
            // all session, so LoadArchives hands it to the session instead.
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
                ApplyDebugSpectate();
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
        // ⚠ Add only NON-CHILD teardown duties here. The whole session subtree hangs under
        // _worldRoot and frees atomically with this node, so a manual null-out is dead code.
        if (what == (int)NotificationExitTree)
        {
            // A run that quits inside the session build (the headless probes) never renders a
            // frame, so this is the only place its startup breakdown can still be reported.
            // Idempotent: a session that did render has already emitted and this does nothing.
            _startup?.Emit();
            // A static pointer, not a child: null it so a node outliving this teardown falls back
            // to its raw frame delta. The menu relaunch is a frame later, so the two never race.
            GameClock.Current = null;
            // Clears csky_light_count so the next world does not inherit this one's light spill;
            // idempotent, and null-guarded (a failed build never set it).
            _worldLights?.Dispose();
            _worldLights = null;
            // Not a node, so QueueFree cannot reach it. Null-guarded: a failed build already
            // disposed and nulled it, so there is no double free.
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            // Restore the persistent (Launcher-owned) main camera: splitscreen stood it down while
            // the panes rendered, and the launchscreen and the next session expect it current.
            _camera.Current = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // P halts the sim and . steps it one frame, in freecam and the static viewer. ⚠ Do not
        // handle either for flight or the animation lab; both own their own transport.
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
            // --debug-wash=N: two scripted blend washes to viewer N, red on the first sim frame
            // and white two seconds in, overlapping so the blend and not just the routing is on
            // screen. Sim time, every clock mode; a viewer no pane answers to paints nothing.
            if (_spec.DebugWash is int washViewer && _debugWashesFired < 2 && _screenFlash != null
                && clock.Time >= _debugWashesFired * 2.0)
            {
                _debugWashesFired++;
                var colour = _debugWashesFired == 1 ? new Color(1f, 0f, 0f) : new Color(1f, 1f, 1f);
                _screenFlash.PlayBlend(washViewer - 1, colour, _debugWashesFired == 1 ? 1f : 0.5f, 5f);
                GD.Print($"--debug-wash: wash {_debugWashesFired} addressed to viewer {washViewer} of {_screenFlash.PaneCount}");
            }
        }
        // The startup line goes out on the frame that proves the first one was drawn.
        _startup?.Frame();
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
    }

    /// <summary>The match clock and the Instant Action mission's own step on a realtime session:
    /// like every physics-stepped consumer, advance on Godot's tick unless the clock is
    /// parent-driven — then <see cref="DriveSimSteps"/> advances both itself, on the same dt as
    /// the rigs.</summary>
    public override void _PhysicsProcess(double delta)
    {
        float dt = _clock?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
            return;
        _versus?.Advance(dt);
        StepInstantAction(dt);
        // On a realtime clock the walk reads whatever pose each aircraft holds at this node's
        // tick; a step's stale pose is at most one 60 Hz frame of a 600 m cone.
        _smokeScreens?.SimStep(dt);
        _beeperTags?.SimStep(dt);
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
        _aircraftScan.AddRange(_aiPlanes);
        return _aircraftScan;
    }

    // Loads the session's core archives (gamez, textures, sounds, sound defs/groups) and routes the
    // texture/sound archives to whichever owner outlives this build scope.
    private void LoadArchives(BuildState state)
    {
        // ⚠ Do not defer this to the flight audio below: the animation bootstrap builds the world's
        // ambient SOUND_NODE emitters and needs the archive while it runs.
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
            // CLI-assigned accents (--ai=…:accent=N) join the roster set: their clips are first
            // reached at runtime too, so an unprewarmed accent would be a silent pilot.
            List<int>? cliAccents = null;
            if (_spec.AiPlanes is { } aiEntries)
            {
                foreach (var entry in aiEntries)
                {
                    if (entry.Accent is { } accent)
                    {
                        (cliAccents ??= new List<int>()).Add(accent);
                    }
                }
            }
            voiceClips = CombatVoice.SessionPrewarmNames(
                state.ZrdrPath, state.MissionZrdrPath, voiceDefs, voiceGroups, cliAccents);
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
                EffectsParent = _worldRoot!,
                // The PLAYER_RANGE fallback for a runtime with no PlayerPositions wired: player 1's
                // camera, resolved per call because none of those cameras exist yet here.
                PlayerPosition = () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam
                    ? cam.GlobalPosition
                    : Vector3.Zero,
                // Every pane camera is a 3D audio listener, so the world is heard from the nearest
                // of them (UI.SplitScreen). Same set as the rigs, for the debug log's column only.
                ListenerPositions = () =>
                {
                    if (_rigs.Count == 0)
                        return _camera is { } cam ? new[] { cam.GlobalPosition } : System.Array.Empty<Vector3>();
                    var positions = new Vector3[_rigs.Count];
                    for (int i = 0; i < _rigs.Count; i++)
                        positions[i] = _rigs[i].Camera.GlobalPosition;
                    return positions;
                },
                // ⚠ Measure EXECUTION_BY_RANGE and PLAYER_RANGE from the aircraft, not the camera:
                // the chase camera trails far enough behind to eat most of a 50 m radius. The same
                // closure goes to the world-effects runtime, so the two agree on who is nearest.
                PlayerPositions = PlayerPositionsSnapshot,
                // The world lights' own nearest-viewer budget — the draw-rule seam A3
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
                ClutterTemplates = _spec.ClutterTemplates,
                // ⚠ Do not set these by hand; they come from LoadArchives's ArchiveIntent. The
                // textures belong to the session so the runtime keeps a live PufferFactory; the
                // sounds do not, being a `using` of this build outside the lab.
                TexturesOutliveBuild = state.TexturesOutliveBuild,
                SoundsOutliveBuild = state.SoundsOutliveBuild,
                VoiceClipNames = voiceClips,
                // The lab's quiet stage: ambient playback deferred to its A toggle, staged templates
                // relocated onto the call site. Safe this early, since a quiet-stage bootstrap
                // dispatches only RESET_STATEs and never consults it.
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
                // The live leashes: each patrolling AI's own follower state, read per frame off
                // the list this session keeps (the overlay is built before any AI exists, so it
                // takes a supplier rather than a snapshot).
                CollectLeashes = into =>
                {
                    foreach (var ai in _aiPlanes)
                    {
                        if (ai is { InPlay: true } && ai.Pilot is { Patrol: { CurrentIndex: >= 0 } patrol } pilot)
                        {
                            into.Add(new UI.AiNetLeash(ai.WorldPosition, patrol.CurrentTarget,
                                patrol.Net.Id, pilot.SteeringPatrol));
                        }
                    }
                },
                // An anchored net is drawn where it actually is, not where the file says.
                // Zero until the rigs are built, which is before anything flies it.
                TrailerOffsetOf = net => _netTrailers?.OffsetOf(net) ?? Vector3.Zero,
            });
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
            Log.Warn("world", $"node stage: '{state.NodeSubtree.Name}'#{state.NodeSubtree.Index} built no geometry at all — it is a group node; the camera framing has nothing to aim at");
        }

        // --damage-test: drive one destructible's HP through its DAMAGE_SEQUENCE stages and quit.
        // ⚠ The world subtree must be in the tree first (with ManualAdvance, so _Process does not
        // double-drive): ticking a death sequence reads global transforms.
        if (_spec.DamageTest)
        {
            _worldRoot!.AddChild(_plane);
            session.Runtime.ManualAdvance = true;
            _probeRunner.RunDamageTest(_spec, session.Runtime);
            GetTree().Quit();
            return false;
        }

        // --effects-test: play every impact/destruction effect and report which resolve and which
        // actually build a puffer, then quit. ⚠ Both subtrees must be in the tree first, or the
        // census counts nothing and the templates' global transforms are identity.
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

        // The shared world selection: click-pick plus the cs_name ancestor ladder, in the two modes
        // that observe a live world with a cursor. Created here so the anim lab below can bind its
        // camera-follow to it; it builds no HUD until something is picked.
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

        // ⚠ Do not move this earlier: every mechanism that places or hides a world entity must have
        // run, so that anything still on the world origin is content this mission never placed.
        // Switched off at once; _Process polls RestorePlacedEntities for what a motion moves.
        _unplacedWatch = builder;
        if (builder.HideUnplacedEntities() is { Count: > 0 } unplaced)
        {
            GD.Print($"world: {unplaced.Count} unplaced entit(y/ies) left at the origin, "
                     + "switched off: " + string.Join(", ", unplaced));
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
            // The ambient cloud field: fogvol.zrd clutter scattered through the fvol* boxes this
            // gamez authors. World-anchored, so it is built once beside the world rather than per
            // rig, and needs no per-frame driving unlike the dome/deck/whiteout below.
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
            // ⚠ Read the fvol zone from the data, never assume it: a chapter authoring -1 keeps the
            // default layer and renders below its deck, which is authored and not a bug. One
            // MultiMesh spans every volume, so it cannot carry a per-volume layer.
            int fvolZone = Mech3.WorldBuilder.FogVolumeZoneIdOf(state.Gamez);
            if (cloudField != null && Mech3.ZoneGate.LayerFor(fvolZone) is var fvolLayer and not 0)
            {
                UI.SplitScreen.SetVisualLayer(cloudField, fvolLayer);
            }

            // The sun goes in with the weather: its bearing is the zone's own SUNLIGHT_ORIENTATION,
            // applied by the same zone-apply that writes the fog. The ambience is the wind seam and
            // the viewer set carries each pane's camera pose for the puffer distance fade.
            _weatherRig = new WeatherRig(_spec, _worldRoot!, _sun, _ambience, _viewers);
            // The deck's own zone_id, the one gated population that cannot ride a visual layer (it
            // is a per-rig camera-anchored copy — see WeatherRig.SetDeckZoneId).
            _weatherRig.SetDeckZoneId(builder.CloudDeckZoneId);
            // ⚠ Read the deck's altitude off the built data; do not hardcode it or pin the deck to
            // the CLOUD_COVER band centre.
            _weatherRig.SetDeckAltitude(builder.CloudDeckAltitude);
            // The same census and parsed fogvol.zrd the cloud field was built from, handed to a
            // second consumer rather than re-loaded. Tick resolves each camera's weather state from
            // it, and its in-volume whiteout where fog_zone is armed.
            _weatherRig.SetFogVolumes(fogVolumes, fogVolumeSpec);
            // The horizon's zone children go in with the mission's weather: the zone the fog and
            // the dome share is picked from both (three chapters ship an empty zone2).
            _weatherRig.Build(state.MissionZrdrPath, _rigs, builder.HorizonZones(),
                activeZone =>
            {
                // One dome per horizon zone the gate can tell apart, not just the flown one: below
                // the cloud deck the camera is in state 1 and zone1 is the sky. The per-rig
                // container is what Tick anchors, so each dome keeps its own scale and gate.
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
                        // ⚠ Scale per dome, never once for the container: a chapter's two zone domes
                        // differ in size, and the flown one's scale must not move because a second
                        // was added beside it. See HorizonScaleFor.
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

                // The evidence that the swap exists at all, since a broken gate and a one-dome
                // chapter render identically at the state they share (docs/verification.md).
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

        // ⚠ Build the flare after the weather: the sun node it anchors to is a child of each rig's
        // horizon subtree, and finding it is one of the two gates. Safe unconditionally, since the
        // chapter data decides whether anything is built.
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

    // The anchor scale for one built skydome: HorizonScale, reduced where that would push the
    // dome's far wall past the camera's far plane and let the clear colour show through.
    // ⚠ HorizonScale is a MAXIMUM, not a constant, and the fit is measured from the built dome's
    // own AABB, never a per-chapter table. Scaling only Y is refuted; the domes keep one uniform
    // fitted scale.
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

    // --anim-lab: the animation debugger's quiet stage — the effect/crash anchor stage, the mission
    // spawn point, the freecam-style SpectatorCamera, an optional parked --plane= prop, and the
    // AnimLab node itself.
    private void BuildAnimLabStage(BuildState state, WorldSession session)
    {
        // ⚠ Use ManualAdvance, never SetProcess(false): Godot re-enables processing at READY for a
        // node overriding _Process, and the runtime enters the tree after this line, so the world
        // would run at double speed on fixed steps plus wall dt.
        session.Runtime.ManualAdvance = true;

        // The lab's effect/crash stage under ONE staging node it moves in front of the camera.
        // ⚠ Keep it indexed as its own subtree, so a played def resolves its puffer hosts and its
        // healthy/destroyed anchors here instead of onto a generic world node of the same name.
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
        // point. No FlightController — in the Fortune Hunters livery like every other
        // aircraft (--paint= picks another, --paint=none the bare shipped skins).
        if (_spec.PlaneNames.Count > 0)
        {
            long mark = StartupProfile.Mark();
            var planesGamez = GameZ.Load(state.PlanesGamezPath);
            StartupProfile.Record("gamez", mark);
            mark = StartupProfile.Mark();
            var parkedBuilder = new PlaneBuilder(planesGamez, state.Textures,
                scheme: _liveryResolver.SchemeFor(0, state.ZrdrPath, _liveryResolver.NewPaintRng(),
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
            // The parked prop hangs beside the world content, outside the selection/node-lab walk,
            // so register it as an extra pick root or neither a click nor the lab tree reaches it.
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

    // The parked-plane static view (--viewer or a bare --plane=): builds the model unpainted
    // (--viewer) or pre-painted, then the damage and livery labs that only make sense parked.
    private void BuildStaticStage(BuildState state)
    {
        // ⚠ Resolve the scheme once here, so the livery lab below opens on exactly what the plane
        // wears rather than a second roll of --paint=random.
        long mark = StartupProfile.Mark();
        var staticPatterns = _liveryResolver.PatternsForPlane(state.Gamez, _spec.PlaneName);
        var staticScheme = _liveryResolver.SchemeFor(0, state.ZrdrPath, _liveryResolver.NewPaintRng(), staticPatterns);
        // In --viewer the LIVERY LAB owns the livery and applies it itself, so the
        // model is built bare and there is one write path for paint (its Repaint).
        // Everywhere else the builder paints at construction as usual.
        var builder = new PlaneBuilder(state.Gamez, state.Textures, damagePanels: _spec.Viewer,
            scheme: _spec.Viewer ? null : staticScheme, patterns: _liveryResolver.Patterns);
        _plane = builder.Build(_spec.PlaneName);
        StartupProfile.Record("plane", mark);
        state.MeshInstances = builder.MeshInstanceCount;
        state.What = $"'{_spec.PlaneName}'";

        // Damage lab: per-part HP sliders driving the same DamageVisuals/puffer pipeline as flight.
        // Present in every --viewer session, opened at launch only by --damage.
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
                // Stand-in puffers: the parked plane travels no distance, so the authored
                // distance-interval trail defs the flight lab plays would emit nothing here.
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

        // Livery lab (--viewer, L): pattern, RGB sliders and decal slots repainting the parked plane
        // through PlaneBuilder.Repaint. Hidden and unpainted unless --paint named a scheme, so an
        // unadorned --viewer screenshot is unchanged.
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

    // Joins the built subject to the tree, then the labs shared by every mode that observes it: the
    // world selection/node/damage labs, the viewer's mesh lab, the marker overlay and the weapon
    // lab. Returns false if the session must abort.
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
        // Mesh lab (--viewer, M): normals, wireframe, zone boxes, lighting and the cull/normal
        // overrides. ⚠ Build it after the plane joins the tree; it reads geometry back through
        // GlobalTransform, which on a detached node returns identity and logs per call.
        if (_spec.Viewer && _plane != null)
            _worldRoot!.AddChild(new UI.MeshLab(_plane, PlaneCollider.Build(_plane),
                _sun, _env, _camera)
            { DebugSpec = _spec.DebugMesh });
        // Marker overlay (--viewer --plane, K): the firepoint, pylon and target gizmos. Only on the
        // parked plane, since a chapter world has no marker rig, and after it joins the tree,
        // since the overlay reads each marker's GlobalPosition.
        if (_spec.Viewer && !_spec.WorldMode && _plane != null)
        {
            _worldRoot!.AddChild(new UI.MarkerOverlay(_plane) { StartHidden = !_spec.MarkersOverlay });
            state.What += _spec.MarkersOverlay ? " + marker overlay" : " + marker overlay (K)";
        }
        // --weapon-test: the whole-catalogue pass check on a PARKED plane. ⚠ Keep it on this cheap
        // no-world path: it asks only whether every weapon mounts and spawns without throwing, and
        // the interactive lab lives in flight because it needs a real world, pool and trigger.
        if (_spec.WeaponTest && _spec.Viewer && !_spec.WorldMode && _plane != null)
        {
            long mark = StartupProfile.Mark();
            var labWeapons = WeaponDefs.Load(state.ZrdrPath, Messages.Load(state.MessagesPath));
            StartupProfile.Record("zrdr", mark);
            // The bench fires the airframe's WHOLE rig, seeded from the plane's stock entry where
            // it has one, so a weapon stock never mounts still gets a mount of its own class.
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
            // The bench's own scene-less pool: rockets fly streak-only, impacts show stand-ins, and
            // there is no DamageSink. ⚠ Do not build a WeaponLab here; the pass check is the bench's.
            var benchPool = new ProjectilePool(state.Textures, null, null);
            _worldRoot!.AddChild(benchPool);
            if (_camera != null)
                benchPool.Viewers.Bind(new[] { _camera });
            // Fire every weapon once per mount and report any that throw, then quit. The report is
            // synchronous, so no world tick is required.
            string report = WeaponBench.Run(_plane, benchLoadout, labWeapons, benchPool).Report;
            GD.Print(report);
            _probeRunner.WriteScratch("weapon_test.txt", report);
            GetTree().Quit();
            return false;
        }
        return true;
    }

    // The deck is now in the tree at its original position; remember its centre so _Process can
    // re-anchor it under each player every frame, and give every rig past the first its own copy.
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

    // Spectator mode (--freecam): the live world with no aircraft, observed from a free-flying
    // camera that starts where the mission would have spawned the player, or wherever --pos put it.
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

    // --fly (and --stunt): builds every rendered rig's aircraft (model, loadout, HUD, audio,
    // stunt/crash hookup) over the session-wide flight data loaded once above the per-player loop.
    // The per-player body lives in its own HumanFlightAdapter.
    private void BuildFlightRigs(BuildState state)
    {
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
            GD.Print($"ai flight stats [{loaded.DefName} damage:{loaded.AiDefName}]: " +
                     $"armor={loaded.VehicleArmor:0.#} health={loaded.VehicleHealth:0.#} " +
                     $"zones={loaded.DestroyableParts.Count} injure_anims={loaded.VehicleInjureAnims.Count}");
            return loaded;
        }
        _aiStatsFor = AiStatsFor;
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
        // Ahead of the rigs, because the assembler hands both to the per-pane stunt board.
        _menuInputs = BuildMenuInputs(padAssignment);
        _pauseState = new PauseState();
        if (_menuPads != null)
            Pads.LogPads(_menuPads);
        // One livery RNG for the session, so P1..P4 draw distinct colours from one
        // stream and --paint-seed reproduces the whole field.
        var paintRng = _liveryResolver.NewPaintRng();
        // Instant Action: the mission's own mission_type IS the scenario key, and its player_plane
        // overrides whichever --plane= was given, so an --ia= launch needs neither flag.
        string iaScenario = _instantAction?.Def.MissionType ?? _spec.Scenario;
        string? iaPlayerNode = _instantAction != null
            ? InstantAction.PlaneNodeFor(_instantAction.Def.PlayerPlane) : null;
        if (_instantAction != null && iaPlayerNode == null)
        {
            GD.PushWarning($"ia: player plane '{_instantAction.Def.PlayerPlane}' is not one of " +
                            $"the eleven airframes — flying '{_spec.PlaneName}' instead");
        }
        // One spawn list for the session; each player takes the next index (wrapping).
        // The empty stage has no mission, so nothing to read: ChooseSpawn takes the
        // --pos/default override placed over the grid origin.
        _spawnPicker.ScenarioOverride = _instantAction != null ? iaScenario : null;
        var spawnList = _spec.EmptyStage ? null : SpawnPoints.LoadIa(state.MissionZrdrPath, iaScenario);
        int spawnBase = _spawnPicker.ChooseSpawnBase(spawnList);

        // The weapons catalogue and stock loadouts, loaded once, and ONE shared projectile pool
        // every player's guns fire into, since projectiles live in the shared world. The pool
        // reuses the session archives and the world gamez, so rockets get their flyout bodies.
        mark = StartupProfile.Mark();
        var weaponMessages = Messages.Load(state.MessagesPath);
        var weaponDefs = WeaponDefs.Load(state.ZrdrPath, weaponMessages);
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
            // The same equal-power splitscreen factor FlightAudio's own-ship loops take, plus the
            // nearest-human snapshot shared with WorldSession and the world-effects runtime.
            MixGain = mixGain,
            PlayerPositions = PlayerPositionsSnapshot,
            BeeperTags = _beeperTags,
            WashSink = _screenFlash != null ? _screenFlash.PlayBlend : null,
            EngineDeadBounds = TanglerChoke.EngineDeadBounds(weaponDefs),
        };
        // ⚠ Bind EVERY pane's camera, never player 1's alone. The tracer pixel floor is a
        // screen-space rule over one shared world mesh, so a single viewer sizes every round
        // against that pane and draws the same geometry oversized in all the others.
        projectiles.Viewers = _viewers;
        _worldRoot!.AddChild(projectiles);
        _projectiles = projectiles;

        // The smoke screens' own smoke, wired here rather than at their construction because the
        // chapter's textures and anim program are only resolved this far into the build. Same
        // program as the rockets' flyout trails: missile_puffers carries generate_smokescreen too.
        if (_smokeScreens != null)
        {
            var smokeEmitters = new SmokeScreenEmitters(state.CrashProgram?.Defs, state.Textures,
                _worldRoot, _ambience);
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
        bool iaStunt = _instantAction is { } iaStuntMission
            && string.Equals(iaStuntMission.Def.MissionType, "stunt_flying", StringComparison.OrdinalIgnoreCase);
        bool wantStunt = _spec.Stunt || iaStunt;
        if (wantStunt && _spec.EmptyStage)
        {
            GD.Print("--stunt has no danger zones on the empty stage (no mission, no world) — flying free");
        }
        else if (wantStunt)
        {
            stuntZones = StuntMission.Load(state.Gamez, state.MissionZrdrPath, Messages.Load(state.MessagesPath));
            if (stuntZones == null)
                // Expected for the chapters whose IA1 has no dzones (C1C, C2B) — a data
                // fact, not a fault, so a plain line (log hygiene: no stack traces).
                GD.Print($"--stunt: no danger zones for {_spec.Chapter}/{_spec.Mission} — flying free");
            else if (_rigs.Count > 1)
                race = new StuntRace(); // splitscreen: a race, ranked on the shared board
            if (stuntZones != null && iaStunt && !_spec.Stunt)
                GD.Print($"ia: stunt_flying — {stuntZones.TotalCount} danger zone(s) from " +
                          $"{_spec.Chapter}/{_spec.Mission}, the mission type's own objective");
        }

        // Dogfight (--vs): built here, before the rigs — same reason Race is (HumanFlightAdapter
        // binds every pane's VersusHud to this one instance below); the score/respawn plumbing
        // that feeds it Downed reports only runs once every rig exists, further down.
        VersusMatch? versus = _spec.Versus
            ? new VersusMatch(_rigs.Count, _spec.VsKills, _spec.VsTimeMinutes * 60f)
            : null;

        // The original's HUD bitmap font, loaded once and shared across panes. Null when the rimage
        // atlas is absent, and its consumers are then simply not built.
        HudFont? hudFont = HudFont.Load(Path.Combine(_dataRoot, "extracted", "rimage"));

        // The gun aiming reticle's pipper: the game's own impact_point.png, loaded once
        // and shared across panes (it carries its own alpha — no colour-keying). Null (no file)
        // simply omits the reticle.
        Texture2D? reticleTex = ImpactReticle.LoadTexture(
            Path.Combine(_dataRoot, "extracted", "rimage"), "impact_point.png");

        // ⚠ Choose the spawn placement ONCE, by picking an implementation here, never by a runtime
        // flag inside one: --det stays byte-identical because RaceGrid is then not constructed at
        // all. It cannot move up beside new SpawnPicker, which runs before `race` is settled.
        IFlightStarts flightStarts = race != null && !_spec.Det
            ? new RaceGrid(_spawnPicker, GroundSampler())
            : _spawnPicker;
        var rigInputs = new HumanFlightAdapter.Inputs
        {
            Ambience = _ambience,
            PlanesGamez = planesGamez,
            StatsFor = StatsFor,
            AiStatsFor = AiStatsFor,
            CamParamsFor = CamParamsFor,
            RigCount = _rigs.Count,
            MixGain = mixGain,
            PadAssignment = padAssignment,
            PauseState = _pauseState!,
            MenuInputFor = MenuInputFor,
            ExitsToMenu = _menuDriven,
            ExitSession = _exitSession,
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
            InstantActionPlayerPlaneNode = iaPlayerNode,
            InstantActionActive = _instantAction != null,
            Coop = _spec.Coop,
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
        var flightRoster = new FlightRoster(_spec, _liveryResolver, _worldEffectsFactory, _worldRoot!,
            rigInputs, flightStarts);
        var rosterBuild = flightRoster.BuildPlayers(_rigs);
        state.MeshInstances += rosterBuild.MeshInstances;
        state.What += rosterBuild.SummarySuffix;
        // The smoke-screen registry is built before the rigs are, so the fire path is bound here
        // rather than through the roster's inputs.
        foreach (var rig in _rigs)
            if (rig.Controller is { } layer)
                layer.SmokeScreens = _smokeScreens;

        // One shared PauseState on every rig: any human pauses everybody, and only the pauser may
        // resume. ⚠ Wire single player the same way, so there is one pause path and not two. The
        // board covers the whole window on its own CanvasLayer, since a pause stops every pane.
        var pauseState = _pauseState!;
        foreach (var rig in _rigs)
            if (rig.Controller is { } pausable)
                pausable.PauseState = pauseState;
        var pauseBoard = PauseBoard.Build(pauseState, exitsToMenu: _menuDriven, MenuInputFor);
        pauseBoard.Restart = Rerun;
        pauseBoard.Exit = _exitSession;
        var pauseLayer = new CanvasLayer { Name = "pause_board", Layer = UI.HudLayers.Board };
        pauseLayer.AddChild(pauseBoard);
        _worldRoot!.AddChild(pauseLayer);

        // Damage lab in flight (F5): the panel --viewer hosts, bound to P1's real PlaneDamage
        // rather than visuals alone, so a dialled-in state drives the HUD and can then be flown.
        // Splitscreen binds P1 only: the panel is one overlay, not one per pane.
        if (_rigs.Count > 0 && _rigs[0].Controller is { Damage: not null } p1)
        {
            var p1Stats = StatsFor(iaPlayerNode ?? PlaneRoster.PlaneFor(_spec, 0));
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

        // The weapon lab in flight, bound to player 1's held aircraft inside a real chapter world.
        // ⚠ Keep it firing through the session's own fully-wired ProjectilePool, never a scene-less
        // pool of its own; --weapon-test is the parked-plane probe and never reaches here.
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

        // The race's shared results board: one ranked row per player, on its own CanvasLayer over
        // the whole window, since the race ends for everybody at once. R rematches every plane,
        // so it routes back through the session.
        if (race != null)
        {
            _race = race;
            var board = StuntRaceBoard.Build(race, $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
                exitsToMenu: _menuDriven, _pauseState!, MenuInputFor);
            board.Restart = () => RestartRace(race);
            board.Exit = _exitSession;
            var boardLayer = new CanvasLayer { Name = "race_board", Layer = UI.HudLayers.Board };
            boardLayer.AddChild(board);
            _worldRoot!.AddChild(boardLayer);
            foreach (var rig in _rigs)
                if (rig.Controller != null)
                    rig.Controller.RestartRace = () => RestartRace(race);
            GD.Print($"stunt race: {_rigs.Count} pilots over {stuntZones!.TotalCount} danger zones, " +
                     "own progress + clock each, shared ranked board");
        }

        // Dogfight (--vs): the match bookkeeping, fed by every rig's Downed report. A killer inside
        // the roster scores a kill and anything else is a plain death. ⚠ Do not guard against
        // post-completion events here; the match ignores them, and the rigs only report facts.
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
                    // ⚠ Keep the kill banner a SEPARATE subscription from the scoring one: every
                    // pane's HUD hears every report, so the whole field sees who went down.
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
                exitsToMenu: _menuDriven, _pauseState!, MenuInputFor);
            board.Restart = () => RestartMatch(match);
            board.Exit = _exitSession;
            var boardLayer = new CanvasLayer { Name = "dogfight_board", Layer = UI.HudLayers.Board };
            boardLayer.AddChild(board);
            _worldRoot!.AddChild(boardLayer);
        }

        // --incoming: the near-miss test rig — a phantom shooter on every pilot's six, so the
        // incoming-fire cue is reachable deterministically with one player, no AI gunner needed.
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

        // The roster shares the session data the human field was built from, so SpawnAiAircraft
        // works from here on, at build or at any later sim step.
        _flightRoster = flightRoster;
        // The E16 voice dispatch, over B8's seam: needs the world's WorldSounds (prewarmed
        // above) and the sound defs. Built before the --ai loop so spawns can register; the
        // players register as damage sources only (WA-HighDmg's broadcast trigger).
        if (state.WorldRuntime?.Sounds is { } worldSounds
            && state.SoundDefs is { } vDefs && state.SoundGroups is { } vGroups)
        {
            var combatVoice = new CombatVoice(vDefs, vGroups, CombatVoice.LoadAccents(state.ZrdrPath));
            _aiVoice = new AiVoiceRuntime(combatVoice, worldSounds, Rng.NewSystemRandom(Rng.Ai));
            _worldRoot!.AddChild(_aiVoice); // its realtime tick; freed with the world subtree
            foreach (var rig in _rigs)
            {
                if (rig.Controller is { } human)
                {
                    _aiVoice.RegisterPlayer(human);
                }
            }
        }
        // An anchored net rides its trailer target, so every follower built below takes a supplier
        // for the object its net names. The player is rig 0, anything else is a world node, and a
        // name that resolves to nothing leaves the net at its authored coordinates.
        var netTrailers = _netTrailers = new NetTrailerTargets(
            () => _rigs.Count > 0 && _rigs[0].Controller is { } trailedRig
                ? trailedRig.WorldPosition
                : null,
            name => rigInputs.WorldRuntime?.FindNodes(name) is { Count: > 0 } trailerHits
                ? trailerHits[0]
                : null);
        // Instant Action's authored ace, dogfight_ace only. Kept as a local so the end-condition
        // block at the bottom of this method can hang the mode's win signal on it.
        FlightController? iaAce = null;
        int iaWaveEnemies = 0;
        // ⚠ Hand every Instant Action actor the chapter's FIRST patrol net, ace, wingmen and wave
        // members alike, and read "first" as neindex FILE order, never the lowest id
        // (docs/formats/instant-action.md).
        AiNet? iaPatrolNet = null;
        if (_instantAction != null)
        {
            try
            {
                iaPatrolNet = AiNets.ChapterFirst(AiNets.Load(rigInputs.ChapterZrdrPath),
                    rigInputs.ChapterZrdrPath);
            }
            catch (Exception e)
            {
                GD.PushWarning($"ia: cannot read {_spec.Chapter}'s patrol nets: {e.Message}");
            }
            if (iaPatrolNet is { Nodes.Count: 0 })
            {
                iaPatrolNet = null;
            }
            GD.Print(iaPatrolNet != null
                ? $"ia: actors patrol '{iaPatrolNet.Name}' (net {iaPatrolNet.Id}), the chapter's first"
                  + (iaPatrolNet.Trailer is { NodeIndex: >= 0, Name: { } anchorName }
                      ? $", anchored to '{anchorName}' at node {iaPatrolNet.Trailer.Value.NodeIndex}"
                      : "")
                : $"ia: {_spec.Chapter} has no first patrol net, actors fly their spawn course");
        }
        // The net IS the standing order. ⚠ Do not let it bring its own volumes: the original copies
        // the roster block's volumes over the net's afterwards, so ApplyActorVolumes has the last
        // word (docs/formats/instant-action.md).
        Action<AiPilot> armIaPatrol = pilot =>
        {
            if (iaPatrolNet == null)
                return;
            pilot.Patrol = new AiNetFollower(iaPatrolNet, Rng.NewSystemRandom(Rng.Ai),
                trailerTarget: netTrailers.For(iaPatrolNet));
        };
        if (_instantAction is { } ia
            && string.Equals(ia.Def.MissionType, "dogfight_ace", StringComparison.OrdinalIgnoreCase))
        {
            string? aceNode = InstantAction.PlaneNodeFor(ia.Def.AcePlane);
            if (aceNode == null)
            {
                GD.PushWarning($"ia: ace plane '{ia.Def.AcePlane}' is not one of the eleven " +
                                "airframes — no ace spawned");
            }
            else if (SpawnPoints.LoadIa(state.MissionZrdrPath, ia.Def.MissionType) is not { Count: > 0 } aceSpawns)
            {
                GD.PushWarning($"ia: no '{ia.Def.MissionType}' spawn points for " +
                                $"{_spec.Chapter}/{_spec.Mission} — no ace spawned");
            }
            else
            {
                int playerSpawnIndex = spawnBase % aceSpawns.Count;
                uint draw = Rng.Stream(Rng.Spawn).Randi();
                var (spIndex, sp) = InstantActionRuntime.ChooseAceSpawn(aceSpawns, playerSpawnIndex, draw);
                var fwd = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
                var pilot = AiPilot.HoldingCourse(sp.Position, sp.Position + fwd);
                armIaPatrol(pilot);
                int rating = InstantActionRuntime.RepresentativeRating(ia.Def.AceStats);
                // shippedSkins for the same reason as a wave member below: the ace flies for an
                // enemy militia, so an ia.json without ace_pattern (hand-authored only) falls back
                // to its own textures, never the player militia's Fortune Hunters default.
                var ace = SpawnAiAircraft(aceNode, sp.Position, sp.Position + fwd, pilot,
                    scheme: ia.Def.AceLivery, team: InstantActionRuntime.EnemyTeam,
                    attackRating: rating, shippedSkins: true);
                RegisterAiVoice(ace, ia.Def.AceAccentId, rating);
                iaAce = ace;
                _iaAce = ace;
                InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                if (ace != null)
                {
                    GD.Print($"ia: ace '{ia.Def.AceName}' ({aceNode}) rating={rating} " +
                              $"team={InstantActionRuntime.EnemyTeam} spawn #{spIndex} of {aceSpawns.Count}");
                    state.What += " + IA ace";
                }
            }
        }
        // Instant Action's wingmen. NumWingmen is forced to 0 on dogfight_ace at parse time, so
        // this and the ace block above are exclusive without an extra mission-type test.
        if (_instantAction is { } iaWingmen && iaWingmen.Def.NumWingmen > 0
            && _rigs.Count > 0 && _rigs[0].Controller is { } leadForWingmen)
        {
            string? wingmanNode = InstantAction.PlaneNodeFor(iaWingmen.Def.WingmanPlane);
            if (wingmanNode == null)
            {
                GD.PushWarning($"ia: wingman plane '{iaWingmen.Def.WingmanPlane}' is not one of " +
                                "the eleven airframes — no wingmen spawned");
            }
            else
            {
                int humans = _rigs.Count;
                int flown = InstantActionRuntime.FlownWingmen(iaWingmen.Def.NumWingmen, humans);
                if (flown < iaWingmen.Def.NumWingmen)
                {
                    GD.Print($"ia: wingmen clamped to {flown} of {iaWingmen.Def.NumWingmen} " +
                              $"configured ({humans} human(s), flight cap 6 — decision 8a)");
                }
                // player_fortune: the wingmen's shared livery. The colour and decal values ride the
                // setup screen in the original, not ia.json, so the catalog entry stands in
                // (docs/formats/paint.md).
                var wingmanScheme = _liveryResolver.PaintCatalog(_zrdrPath)
                    .Find(s => string.Equals(s.Pattern, LiveryResolver.DefaultPattern, StringComparison.OrdinalIgnoreCase));
                if (wingmanScheme == null)
                {
                    GD.PushWarning("ia: no 'player_fortune' entry in the paint catalog — wingmen " +
                                    "fly unpainted/random");
                }
                var wmBasis = leadForWingmen.GlobalTransform.Basis;
                var wmFwd = -wmBasis.Z;
                var wmLeadPos = leadForWingmen.WorldPosition;
                var wingmen = new FlightController?[flown];
                for (int i = 0; i < flown; i++)
                {
                    var slot = InstantActionRuntime.WingmanSlotFor(i);
                    var offsetDir = wmFwd.Rotated(Vector3.Up, Mathf.DegToRad(slot.OffsetDeg));
                    var pos = wmLeadPos + offsetDir * slot.MetresOut;
                    var pilot = AiPilot.HoldingCourse(pos, pos + wmFwd);
                    // The wingman takes the same net the ace and the waves do, which in the original
                    // demotes it out of wingman mode, so the escort chain below is a target
                    // assignment and not a flown formation (docs/org/aiPilot.md).
                    armIaPatrol(pilot);
                    // ⚠ Pass an explicit rating, never null: a wingman's Gunner and Machine are only
                    // built when one resolves, and null would arm them solely on a launch that
                    // happened to carry --ai-attack= (docs/formats/instant-action.md).

                    // The wizard's one wingman fit, covering the whole flight as the original's
                    // Player/Wingman radio does. Passed per spawn, never as a blanket default: the
                    // stock-table branch it lands in also catches enemies on player airframes.
                    var wingman = SpawnAiAircraft(wingmanNode, pos, pos + wmFwd, pilot,
                        scheme: wingmanScheme, team: AimAssist.PlayerTeam, attackRating: 5,
                        fit: iaWingmen.Def.WingmanLoadout);
                    wingmen[i] = wingman;
                    if (wingman == null)
                        continue;
                    // primary_target: 0, 1 and 3 escort the player; 2 and 4 escort
                    // wingmen 1 and 3 — FlightController.SelectRankedTarget's own by-name/"player"
                    // match, the same seam the D12 ranking already reads.
                    if (pilot.Gunner != null)
                    {
                        pilot.Gunner.PrimaryTargetName = slot.PrimaryTargetIsWingman is { } escortIdx
                            ? wingmen[escortIdx]?.Name.ToString()
                            : "player";
                    }
                    // An Instant Action actor's volumes are all authored far wider than the airframe
                    // defaults SpawnAiAircraft arms (docs/formats/instant-action.md).
                    InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                    RegisterAiVoice(wingman, slot.AccentId);
                }
                int wmSpawned = wingmen.Count(w => w != null);
                if (wmSpawned > 0)
                {
                    GD.Print($"ia: {wmSpawned} wingman(s) ({wingmanNode}) team={AimAssist.PlayerTeam}");
                    state.What += $" + {wmSpawned} IA wingmen";
                }
            }
        }
        // Instant Action's wave sequencer. ⚠ Build EVERY wave inert here, wave 1 included, so one
        // build-then-activate path serves them all; the mission type decides a wave's ARRIVAL, not
        // whether it is built (docs/formats/instant-action.md).
        if (_instantAction is { } iaWaves)
        {
            var waveSizes = iaWaves.Def.Waves.Select(w => w.NumEnemies).ToArray();
            var rosters = new List<FlightController>[4];
            for (int w = 0; w < 4; w++)
            {
                var roster = new List<FlightController>();
                var wave = iaWaves.Def.Waves[w];
                if (wave.NumEnemies > 0)
                {
                    string? waveNode = InstantAction.PlaneNodeFor(wave.EnemyPlane);
                    if (waveNode == null)
                    {
                        GD.PushWarning($"ia: wave {w + 1} plane '{wave.EnemyPlane}' is not one " +
                                        "of the eleven airframes — no wave enemies spawned");
                    }
                    else
                    {
                        for (int m = 0; m < wave.NumEnemies; m++)
                        {
                            // Built at the origin, inert — position is irrelevant until
                            // ActivateInstantActionWave teleports it in, same as the original's
                            // own "deactivated at the world origin".
                            var pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
                            // Armed at build, but the follower seats itself at its first update and
                            // Activate re-seats it, so a member patrols from where it arrives
                            // rather than from this parking pose.
                            armIaPatrol(pilot);
                            // ⚠ The militia paints it and nothing more: the original spawns a wave
                            // member from the PLAIN AI def of its aircraft. An unnamed militia keeps
                            // its own skins, never the Fortune Hunters default.
                            var waveScheme = WaveMilitiaScheme(state, wave.EnemyName);
                            int rating = InstantActionRuntime.RepresentativeRating(
                                InstantActionRuntime.RandomPilotStats(Rng.Stream(Rng.Ai).Randi()));
                            var enemy = SpawnAiAircraft(waveNode, Vector3.Zero, Vector3.Forward,
                                pilot, scheme: waveScheme, team: InstantActionRuntime.EnemyTeam,
                                attackRating: rating, inert: true,
                                shippedSkins: waveScheme == null);
                            if (enemy == null)
                            {
                                continue;
                            }
                            if (pilot.Gunner != null)
                            {
                                pilot.Gunner.PrimaryTargetName = "player";
                            }
                            // The same authored actor volumes as the ace and wingmen
                            // (docs/formats/instant-action.md).
                            InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                            int accentId = InstantActionRuntime.ResolveWaveAccentId(
                                wave.EnemyAccentId, Rng.Stream(Rng.Ai).Randi());
                            RegisterAiVoice(enemy, accentId, rating);
                            roster.Add(enemy);
                        }
                    }
                }
                rosters[w] = roster;
            }
            _iaWaveRosters = rosters;
            _iaWaveSpawnList = spawnList;
            _iaWaves = new InstantActionWaves(waveSizes);
            // On zeppelin_run the first wave is started after the generator block below, since
            // "activating" it there means crediting the zeppelin's generator, which does not
            // exist yet at this point in the build.
            if (!iaWaves.IsZeppelinRun)
            {
                int firstWave = _iaWaves.Start();
                if (firstWave != 0)
                {
                    ActivateInstantActionWave(firstWave);
                }
            }
            iaWaveEnemies = rosters.Sum(r => r.Count);
            if (iaWaveEnemies > 0)
            {
                GD.Print($"ia: {iaWaveEnemies} wave enemies across " +
                          $"{rosters.Count(r => r.Count > 0)} wave(s), built inert, " +
                          $"team={InstantActionRuntime.EnemyTeam}");
                state.What += $" + {iaWaveEnemies} IA wave enemies";
            }
        }
        if (_spec.AiPlanes is { Count: > 0 } aiPlanes && _rigs.Count > 0
            && _rigs[0].Controller is { } lead)
        {
            // Without a net: ahead of P1 on its own spawn heading, fanned right/left, holding
            // that course. With one: on the net's first node, patrolling the graph.
            var basis = lead.GlobalTransform.Basis;
            var fwd = -basis.Z;
            var right = basis.X;
            List<AiNet>? nets = null;
            bool netsTried = false;
            for (int i = 0; i < aiPlanes.Count; i++)
            {
                var (planeName, netRef, accentId, aiDef) = aiPlanes[i];
                float lateral = 60f * ((i + 1) / 2) * (i % 2 == 0 ? 1f : -1f);
                AiNet? net = null;
                if (netRef != null)
                {
                    if (!netsTried)
                    {
                        netsTried = true;
                        try
                        {
                            nets = AiNets.Load(SessionPaths.ChapterZrdr(_dataRoot, _spec.Chapter));
                        }
                        catch (Exception e)
                        {
                            GD.PushWarning($"--ai: cannot read {_spec.Chapter}'s patrol nets: {e.Message}");
                        }
                    }
                    net = nets != null ? AiNets.Resolve(nets, netRef) : null;
                    if (net == null)
                        GD.PushWarning($"--ai: net '{netRef}' not in {_spec.Chapter}'s neindex; " +
                                       $"'{planeName}' spawns without a patrol");
                }
                if (net != null)
                {
                    // Spawned on the net itself, so a scripted run sees it patrolling within
                    // seconds. ⚠ Take node positions off the follower, not the record, or an
                    // anchored net puts the plane where the ring is not.
                    var follower = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai),
                        trailerTarget: netTrailers.For(net));
                    var pos = follower.NodePosition(0) + right * lateral;
                    var look = net.Nodes.Count > 1 ? follower.NodePosition(1) : pos + fwd;
                    var pilot = AiPilot.HoldingCourse(pos, look);
                    pilot.Patrol = follower;
                    var spawnedOnNet = SpawnAiAircraft(planeName, pos, look, pilot, aiDef: aiDef);
                    RegisterAiVoice(spawnedOnNet, accentId ?? AiStatsForSpawn(planeName, aiDef)?.AiAccentId);
                    ApplyAiHullPreset(spawnedOnNet);
                }
                else
                {
                    var pos = lead.WorldPosition + fwd * 250f + right * lateral;
                    var spawnedAhead = SpawnAiAircraft(planeName, pos, pos + fwd,
                        AiPilot.HoldingCourse(pos, pos + fwd), aiDef: aiDef);
                    RegisterAiVoice(spawnedAhead, accentId ?? AiStatsForSpawn(planeName, aiDef)?.AiAccentId);
                    ApplyAiHullPreset(spawnedAhead);
                }
            }
            state.What += $" + {aiPlanes.Count} AI";
        }

        // --zeppelins: the mission's zeppelin instances, placed at their authored pose and flown
        // along their nets as kinematic world nodes. ⚠ Build them before --generators below, so a
        // zeppelin generator's min_altitude gate reads the flown host's live Y from the first step.
        bool iaZeppelinRun = _instantAction?.IsZeppelinRun ?? false;
        if (_spec.Zeppelins || iaZeppelinRun)
        {
            List<ZeppelinDef> zepDefs;
            try
            {
                zepDefs = Zeppelins.Load(state.MissionZrdrPath);
            }
            catch (IOException e)
            {
                GD.Print($"zep: no zeppelins file for {_spec.Chapter}/{_spec.Mission}: {e.Message}");
                zepDefs = new List<ZeppelinDef>();
            }
            var zepNets = AiNets.Load(rigInputs.ChapterZrdrPath);
            _zeppelins = new ZeppelinRuntime(zepDefs,
                name => rigInputs.WorldRuntime?.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null,
                zepNets, netTrailers.For);
            _worldRoot!.AddChild(_zeppelins);
            // F18: the multi-zone damage half — per-part pools over the world registry, the
            // survivor-count kill, and the DAMAGES_ZEPPELIN gate on the shared pool.
            if (rigInputs.WorldRuntime is { } zepRuntime)
            {
                _zeppelins.WireDamage(zepRuntime);
                if (_projectiles != null)
                {
                    _projectiles.WorldDamageGate = _zeppelins.GateWeaponDamage;
                }
            }
            // F19: the broadside cannons — real wep_28 rounds through the shared pool, the
            // authored deploy/retract anims, targets from the record. After WireDamage so
            // F18's cannon pools exist (a destroyed cannon thins the volley).
            if (_projectiles != null)
            {
                _zeppelins.WireCannons(_projectiles, weaponDefs);
            }
            // B14: the zeppelin sub-parts are the one thing that makes a structure selectable, and
            // the zeppelins are built AFTER the rigs — so the feed is bound here rather than in the
            // assembler. Every pane shares the one runtime; each fills its own list from it.
            var zepTargets = _zeppelins;
            foreach (var zepRig in _rigs)
            {
                if (zepRig.Controller is { } zepPlane)
                {
                    zepPlane.TargetSubParts = into => zepTargets.CollectTargetParts(into);
                }
            }
            GD.Print($"zep: {_zeppelins.LiveCount} of {zepDefs.Count} zeppelin(s) placed for " +
                     $"{_spec.Chapter}/{_spec.Mission}");
            state.What += $" + {_zeppelins.LiveCount} zeppelin(s)";
        }

        // Instant Action's own zeppelin switch. ⚠ Visible is the WHOLE write, since world colliders
        // derive Disabled from it, and each switched node must reach the turret arm below or the
        // objective zeppelin flies unarmed.
        var iaZepTurretSwitch = new List<(Node3D Node, bool Objective, string Name)>();
        if (_instantAction is { } iaZeppelins && rigInputs.WorldRuntime is { } iaZepWorld)
        {
            string selectedZep = InstantActionRuntime.SelectedZeppelinNode(iaZeppelins.Def);
            var switchedZeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string zepName in InstantActionRuntime.ZeppelinNodes(iaZeppelins.Def))
            {
                if (!switchedZeps.Add(zepName))
                {
                    continue;   // all 8 shipped chapters name the same node three times
                }
                bool objective = iaZeppelinRun
                    && zepName.Equals(selectedZep, StringComparison.OrdinalIgnoreCase);
                if (iaZepWorld.FindNodes(zepName) is not { Count: > 0 } zepNodes)
                {
                    GD.Print($"ia: zeppelin '{zepName}' is not in {_spec.Chapter}'s world — " +
                              "nothing to switch");
                    continue;
                }
                foreach (var zepNode in zepNodes)
                {
                    zepNode.Visible = objective;   // colliders derive from this (WorldCollision)
                    iaZepTurretSwitch.Add((zepNode, objective, zepName));
                }
                if (!objective)
                {
                    _zeppelins?.Hold(zepName);
                }
                GD.Print($"ia: zeppelin '{zepName}' " + (objective
                    ? "ACTIVATED as this mission's objective (zeppelin_run)"
                    : "deactivated by the Instant Action builder"));
            }
        }

        // --generators: the mission's egen enemy generators, spawning through the seam above and
        // loaded here because the drop rules need the built world. An Instant Action zeppelin run
        // asks for them itself: its generator is the only way an enemy reaches the air.
        if (_spec.Generators || iaZeppelinRun)
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
            var chapterNets = AiNets.Load(rigInputs.ChapterZrdrPath);
            var wr = rigInputs.WorldRuntime;
            _generators = new AiGeneratorRuntime(egenDefs,
                wr == null ? null
                    : (name, scope) => wr.FindNodes(name, scope) is { Count: > 0 } hits ? hits[0] : null,
                // ⚠ A lambda, not the SpawnAiAircraft method group: shippedSkins rides the authoring
                // overload, and a generated aircraft is the mission's enemy, so it keeps its own
                // textures rather than the player militia's default.
                chapterNets, _spec.GeneratorsPlane,
                (plane, pos, look, pilot) => SpawnAiAircraft(plane, pos, look, pilot,
                    scheme: null, team: null, attackRating: null, shippedSkins: true),
                wr == null ? null : (name, host) => wr.PlayWithin(host, name, applyReset: false).Count,
                wr == null ? null : (name, host) => wr.StopWithin(host, name),
                netTrailers.For);
            _worldRoot!.AddChild(_generators);
            // A dead zeppelin permanently disables its generator (the decoded rule; F18
            // supplies the death the B6 stub waited on).
            if (_zeppelins != null)
            {
                var generators = _generators;
                _zeppelins.ZeppelinKilled += node => generators.NotifyHostDied(node);
            }
            GD.Print($"egen: {_generators.LiveCount} of {egenDefs.Count} generator(s) live for " +
                     $"{_spec.Chapter}/{_spec.Mission}, spawning '{_spec.GeneratorsPlane}'");
            state.What += $" + {_generators.LiveCount} generator(s)";
        }

        // The zeppelin run's wave arm: the objective zeppelin's generator releases the waves built
        // inert above on a budget the sequencer credits wave by wave. ⚠ The first wave can only
        // start here, since activating it means crediting a generator that did not exist earlier.
        if (iaZeppelinRun && _instantAction is { } iaZepRun && _iaWaves != null)
        {
            string objectiveZep = InstantActionRuntime.SelectedZeppelinNode(iaZepRun.Def);
            int claimed = _generators?.UseInstantActionLaunches(
                objectiveZep, ReleaseInstantActionWaveMember) ?? 0;
            if (claimed == 0)
            {
                // ⚠ Do not invent a fallback spawn path: the original burns through every wave the
                // same way, its counter advancing whether or not the top-up lands
                // (docs/formats/instant-action.md).
                GD.PushWarning($"ia: zeppelin '{objectiveZep}' carries no egen generator — no " +
                                "wave will ever launch on this zeppelin run");
            }
            else
            {
                GD.Print($"ia: zeppelin run: '{objectiveZep}' launches every wave " +
                          $"({claimed} generator(s) on the wave-credit budget). BL-350 is open " +
                          "and in this mission's way: a drop is not gated on the doors opening.");
            }
            int firstZepWave = _iaWaves.Start();
            if (firstZepWave != 0)
            {
                ActivateInstantActionWave(firstZepWave);
            }
        }

        // Instant Action's end conditions, lives and spectating: every signal already exists above,
        // so this only routes them into the runtime that decides the outcome. ⚠ Each source reports
        // the objective it satisfied and the runtime drops what this mission does not run on.
        if (_instantAction is { } iaEnd)
        {
            // "Enemies Shot Down": every EnemyTeam actor downed by an attributed shooter, on every
            // mission type. ⚠ Keep the killer != null filter: a bare terrain or mid-air crash never
            // reaches the take-hit body the original counts in (docs/formats/instant-action.md).
            int enemiesShotDown = 0;
            if (iaAce != null)
            {
                iaAce.Downed += (_, killer) => { if (killer != null) enemiesShotDown++; };
            }
            if (_iaWaveRosters != null)
            {
                foreach (var roster in _iaWaveRosters)
                {
                    foreach (var member in roster)
                    {
                        member.Downed += (_, killer) => { if (killer != null) enemiesShotDown++; };
                    }
                }
            }

            var objective = iaEnd.Objective;
            if (objective == InstantActionObjective.AceDown && iaAce != null)
            {
                iaAce.Downed += (_, _) => iaEnd.ReportObjective(InstantActionObjective.AceDown);
            }
            else if (objective == InstantActionObjective.WavesCleared && iaWaveEnemies > 0)
            {
                // Reported by StepInstantAction off InstantActionWaves.Finished — the sequencer
                // owns "every configured wave is cleared" and nothing here re-derives it.
            }
            else if (objective == InstantActionObjective.ZonesFlown && stuntZones != null)
            {
                // ⚠ All-finished, never first past the post, and evaluated only over pilots who can
                // still fly, so one out of lives cannot deadlock a mission the survivors finished.
                // Checked on the two events that can make it true, never polled.
                foreach (var rig in _rigs)
                {
                    if (rig.Controller?.Stunt is { } run)
                    {
                        run.RunCompleted += CheckInstantActionZoneSets;
                    }
                }
            }
            else if (objective == InstantActionObjective.ZeppelinDisabled && _zeppelins != null)
            {
                string endZep = InstantActionRuntime.SelectedZeppelinNode(iaEnd.Def);
                Action<string> reportZeppelin = node =>
                {
                    // The OBJECTIVE's own signal only: a mission world may fly other zeppelins,
                    // and disabling or shooting down one of those is not this mission's win.
                    if (string.Equals(node, endZep, StringComparison.OrdinalIgnoreCase))
                    {
                        iaEnd.ReportObjective(InstantActionObjective.ZeppelinDisabled);
                    }
                };
                // Both decoded paths report the ONE objective, engines first: the mode is for
                // disabling them, and the hull dying on the gasbag threshold wins it too.
                _zeppelins.ZeppelinEnginesDisabled += reportZeppelin;
                _zeppelins.ZeppelinKilled += reportZeppelin;
            }
            else
            {
                // A mission that cannot be won says so at build. Deliberate, in the shape
                // VersusMatch's disabled kill target/time limit already has — the mission still
                // flies and can still be lost.
                iaEnd.DisableObjective();
                GD.PushWarning("ia: this mission has NO win condition — " + (objective switch
                {
                    null => $"mission type '{iaEnd.Def.MissionType}' has none in this build",
                    InstantActionObjective.AceDown => "no ace was spawned",
                    InstantActionObjective.WavesCleared => "no wave enemy is configured",
                    InstantActionObjective.ZonesFlown => "this mission ships no danger zones",
                    _ => "no zeppelin runtime was built",
                }) + " (it can still be lost)");
            }
            // Lives are a remake-only rule; no ia.json key carries one. Every human seat joins the
            // ledger, and NotifyPilotDown decides whether the crash cam ends in a respawn.
            foreach (var rig in _rigs)
            {
                if (rig.Controller is not { } pilot)
                {
                    continue;
                }
                iaEnd.RegisterPilot(pilot.PlayerIndex);
                // G14's "Shot %": the decode's "the local player" filter, generalised to every
                // human seat for splitscreen (ProjectilePool.ScoredShooters).
                _projectiles?.ScoredShooters.Add(pilot.PlayerIndex);
                pilot.AutoRespawnAfter = VersusRespawnDelay; // crash cam, then back in — R skips
                pilot.Downed += (victim, _) =>
                {
                    if (iaEnd.NotifyPilotDown(victim))
                    {
                        GD.Print($"ia: P{victim + 1} down — " + (iaEnd.Def.Lives == 0
                            ? "unlimited lives" : $"{iaEnd.LivesLeft(victim)} life/lives left") +
                            $", respawning in {VersusRespawnDelay:0.#} s");
                        return;
                    }
                    BeginInstantActionSpectate(rig);
                };
            }
            iaEnd.MissionEnded += outcome => GD.Print(
                $"ia: mission {(outcome == InstantActionOutcome.Won ? "COMPLETE" : "FAILED")} — " +
                $"{iaEnd.Def.MissionType} after {iaEnd.Elapsed:0.0} s");
            GD.Print($"ia: {iaEnd.Def.MissionType} — win: " +
                     (iaEnd.ObjectiveEnabled ? objective!.Value.ToString() : "none") +
                     $", loss: every human out of lives ({(iaEnd.Def.Lives == 0 ? "unlimited" : iaEnd.Def.Lives.ToString())} " +
                     $"per pilot), {iaEnd.PilotCount} human seat(s)");

            // The wrap-up board, shared over the WHOLE window like the race and dogfight boards,
            // never per pane: the mission ends for every human at once. ⚠ Danger Zones Completed
            // and Shot % are summed across every human seat, never picked from one pane.
            var wrapupBoard = IaWrapupBoard.Build(
                $"{_spec.Chapter}   ·   {InstantAction.MissionTypeLabel(iaEnd.Def.MissionType)}",
                exitsToMenu: _menuDriven,
                _pauseState!, MenuInputFor);
            wrapupBoard.Restart = _restartSession;
            wrapupBoard.Exit = _exitSession;
            var wrapupLayer = new CanvasLayer { Name = "ia_wrapup_board", Layer = UI.HudLayers.Board };
            wrapupLayer.AddChild(wrapupBoard);
            _worldRoot!.AddChild(wrapupLayer);
            iaEnd.MissionEnded += outcome =>
            {
                int zonesCompleted = _rigs.Sum(r => r.Controller?.Stunt?.CompletedCount ?? 0);
                int shotPercent = InstantActionRuntime.ShotPercent(
                    _projectiles?.CannonHits ?? 0, _projectiles?.CannonRoundsFired ?? 0);
                wrapupBoard.Present(outcome == InstantActionOutcome.Won, iaEnd.Elapsed,
                    enemiesShotDown, zonesCompleted, shotPercent, StuntSummaryFor(iaEnd));
            };
        }

        // World AA emplacements: the standalone ai.zrd family, placed against this chapter's built
        // world unconditionally, like the original's own placement pass. Shipped ACTIVATED decides
        // which are awake; --wake-turrets stands in for the mission script's WAKEUP_TURRETS.
        if (state.WorldRuntime is { } worldRt && turretDefs != null)
        {
            var placedRt = worldRt;
            _turretEmplacements = new TurretEmplacementRuntime(turretDefs, weaponDefs,
                (pattern, scope) => placedRt.FindNodes(pattern, scope), projectiles,
                placedRt.WorldRoot);
            // ⚠ Into the tree AFTER the zeppelin runtime: the physics tick follows tree order, so
            // this is what lets a slung mount read its ride's moved pose on a realtime clock.
            _worldRoot!.AddChild(_turretEmplacements);
            int awakeByData = _turretEmplacements.AwakeCount;
            // The zeppelin turret arm recorded above. ⚠ Run it BEFORE --wake-turrets, which stands
            // in for a mission script and therefore wins, the same order the original has.
            foreach (var (zepNode, objective, zepName) in iaZepTurretSwitch)
            {
                int touched = _turretEmplacements.SetActivatedUnder(zepNode, objective);
                if (touched > 0)
                {
                    GD.Print($"ia: zeppelin '{zepName}' turrets: {touched} emplacement(s) " +
                             (objective ? "ACTIVATED with the objective" : "stowed with the hull"));
                }
            }
            int woken = _spec.WakeTurrets ? _turretEmplacements.WakeAll() : 0;
            GD.Print($"turrets: {_turretEmplacements.Count} world emplacement(s) placed for " +
                     $"{_spec.Chapter} ({awakeByData} awake by data, " +
                     $"{_turretEmplacements.Count - awakeByData} dormant" +
                     (woken > 0 ? $", {woken} woken by --wake-turrets" : "") + ")");
            if (_turretEmplacements.Count > 0)
            {
                state.What += $" + {_turretEmplacements.Count} emplacement(s)";
            }
        }

        // F15 / --debug-targets: who is aiming at whom. Reads the live gunners through closures
        // rather than a snapshot — waves activate, AI planes spawn and emplacements die long
        // after this line runs. The roster list is reused, not rebuilt per frame.
        _worldRoot!.AddChild(new UI.TargetingOverlay(
            () => _turretEmplacements?.Emplacements ?? Array.Empty<TurretController>(),
            AllAircraft)
        {
            DebugShow = _spec.DebugTargets,
        });

        if (_rigs.Count > 1)
        {
            var flown = new List<string>(_rigs.Count);
            for (int pi = 0; pi < _rigs.Count; pi++)
                flown.Add($"P{pi + 1} '{iaPlayerNode ?? PlaneRoster.PlaneFor(_spec, pi)}'");
            state.What += $" + splitscreen {string.Join(", ", flown)}";
        }
        else
        {
            state.What += $" + '{iaPlayerNode ?? _spec.PlaneName}' flying";
        }
    }

    // --destroy=<name>: kill a named destructible at session build so a --screenshot captures its
    // destruction with nobody at the controls. Reuses the weapon-damage path, so DamageAt runs the
    // full death. A plane-less --freecam asks for a world-effects runtime here, gated on the flag
    // so a plain --freecam builds nothing extra.
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

    // The "loaded ..." summary line and the per-pane/texture-census follow-ups, printed once the
    // whole build has finished.
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

    // The post-build framing pass: subject framing for the static views, the freecam/anim-lab mesh
    // lab, the collider wireframe overlay and the node-name label layer.
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
                // ⚠ Collider.Parts.Local is in the plane MODEL's parent frame, not the model's own:
                // the model root carries its own GameZ local transform, so drawing the boxes as its
                // children would apply that transform twice.
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
                // ids have a def of their own is whatever the bound program defines.
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

        // Map-edge tile grid (--debug-tilegrid, flag-only). Gated on the extender rather than a
        // mode list, because "there is a continuation to colour" is exactly the precondition.
        if (_edgeExtender != null && _worldRoot != null && _plane != null)
        {
            _worldRoot.AddChild(new UI.TileGridOverlay(_plane, _edgeExtender)
            {
                DebugShow = _spec.ShowTileGrid,
            });
            Log.Info("world", $"tile-grid overlay ready (--debug-tilegrid)");
        }
        else if (_spec.ShowTileGrid)
        {
            Log.Warn("world", $"--debug-tilegrid: this mode builds no map-edge continuation, so there is no tile grid to colour (it exists in --fly, --freecam, and a --sky-zone viewer)");
        }

        // Node-name labels (T), in both the viewer and flight, over the whole session subtree, so
        // the world and the aircraft are labelled alike. Off until pressed, and it builds nothing
        // until then. In splitscreen the selection follows P1 but the labels render in every pane.
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

    // The production ground sampler RaceGrid probes its slots with: the world height under a point,
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
                Log.Warn("flight", $"grid ground probe found nothing under a slot (probe {ProbeAbove:0}m up / {ProbeBelow:0}m down, mask=World) — measured, this does not happen at session build, so suspect the probe ran before the world stage was added rather than an empty map; the field keeps the spawn data's own altitude. Reported once per session.");
            }
            return null;
        };
    }

    // --ai-damage=: spends this AI plane's hull down to the ordered fraction at build, so a scripted
    // shot catches its injure_anims stages already up. Armour first and health second, in two exact
    // spends, because that is the order the take-hit flow spends them in; an AI airframe resolves no
    // zones, so both land in the whole pair the ladder reads. ⚠ Never drives the pool to zero — a
    // preset that kills would leave a wreck where the point is a flying, burning aircraft.
    private void ApplyAiHullPreset(FlightController? controller)
    {
        if (_spec.AiHullDamage is not { } fraction || controller?.Damage is not { } damage)
            return;
        if (damage.WholeArmor > 0f)
            damage.Apply("hull", 0f, damage.WholeArmor);
        float spend = damage.WholeHealth - (Mathf.Max(fraction, 0.01f) * damage.WholeHealthMax);
        if (spend > 0f)
            damage.Apply("hull", spend, 0f);
        controller.Visuals?.OnHullDamage(damage.SummaryHealthFraction);
        GD.Print($"ai damage preset: {controller.Name} hull at " +
                 $"{damage.SummaryHealthFraction * 100f:0}% ({damage.WholeHealth:0.0}/{damage.WholeHealthMax:0})");
    }

    // Every player's own position: the flown aircraft where a rig has a bound FlightController,
    // that rig's camera otherwise. ⚠ One shared snapshot behind both PlayerPositions consumers, so
    // EXECUTION_BY_RANGE and PLAYER_RANGE cannot answer "who is nearest" differently.
    private IReadOnlyList<Vector3> PlayerPositionsSnapshot()
    {
        if (_rigs.Count == 0)
            return _camera is { } cam ? new[] { cam.GlobalPosition } : Array.Empty<Vector3>();
        var positions = new Vector3[_rigs.Count];
        for (int i = 0; i < _rigs.Count; i++)
            positions[i] = _rigs[i].Controller is { } fc
                ? fc.GlobalPosition
                : _rigs[i].Camera.GlobalPosition;
        return positions;
    }

    // Creates this session's PlayerRigs, one per rendered view. One player keeps the main-viewport
    // camera and the default visual layers, so that render path is unchanged. Two or more build the
    // SplitScreen pane rig, each pane culling every other player's private sky/deck/puff layer.
    private void BuildRigs(int count)
    {
        _rigs.Clear();
        _split = null;
        if (count <= 1)
        {
            _camera.Current = true;
            // ⚠ Reopen the whole zone band before this session's first frame. The main camera is
            // the Launcher's and outlives the session, so it arrives carrying the last flight's
            // gate, and WeatherRig.Tick only ever NARROWS the band.
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

    // Gives every rig a cloudlayer deck to anchor under its own camera: rig 0 takes the world's
    // deck, the rest get copies on their player's visual layer. ⚠ Re-apply the instance uniforms
    // from the source; they are RenderingServer state and Duplicate drops them.
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

    // One menu reader per player, bound the way that player's plane is bound: player 1 also has the
    // keyboard, and a session with no per-player split reads every connected pad (null).
    private UI.MenuInput[] BuildMenuInputs(int[][]? padAssignment)
    {
        var inputs = new UI.MenuInput[Math.Max(1, _rigs.Count)];
        for (int i = 0; i < inputs.Length; i++)
            inputs[i] = new UI.MenuInput { Keyboard = i == 0, Pads = padAssignment?[i] };
        return inputs;
    }

    // The reader a board menu drives its cursor from. An owner outside the roster (a board that
    // named no player) falls back to player 1, who always exists.
    private UI.MenuInput MenuInputFor(int playerIndex)
    {
        var inputs = _menuInputs ??= BuildMenuInputs(null);
        return playerIndex >= 0 && playerIndex < inputs.Length ? inputs[playerIndex] : inputs[0];
    }

    // A board menu's Restart item. An Instant Action mission is REBUILT by the Launcher, because
    // its opposition lives in the world and nothing here can put it back; every other mode reruns
    // in place, the race and the match through their own bookkeeping and anything else per-plane.
    private void Rerun()
    {
        if (_instantAction != null)
        {
            _restartSession();
            return;
        }
        if (_race is { } race)
        {
            RestartRace(race);
            return;
        }
        if (_versus is { } match)
        {
            RestartMatch(match);
            return;
        }
        foreach (var rig in _rigs)
            rig.Controller?.Rerun();
    }

    // Player 1's stunt run for the wrap-up board's split section, on a stunt mission alone. The
    // best time is recorded here rather than on the board, under the same chapter/mission/plane key
    // the solo scoreboard uses — a different mission id, so Instant Action bests stay their own.
    private StuntSummary? StuntSummaryFor(InstantActionRuntime runtime)
    {
        if (runtime.Objective != InstantActionObjective.ZonesFlown)
            return null;
        if (_rigs.Count == 0 || _rigs[0].Controller?.Stunt is not { } run)
            return null;
        var store = ScoreStore.Load();
        string key = $"{_spec.Chapter}/{_spec.Mission}/{PlaneRoster.PlaneFor(_spec, 0)}";
        float? prevBest = store.GetBest(key);
        bool newBest = store.RecordIfBest(key, run.Elapsed);
        return new StuntSummary(run, run.Elapsed, prevBest, newBest);
    }

    // Rematch from the shared race board (R): every player's zones, clock and placing cleared, then
    // every plane back to its own spawn. The session owns the planes, so the restart lands here
    // rather than in the FlightController that read the button.
    private void RestartRace(StuntRace race)
    {
        GD.Print("stunt race: rematch — fresh clocks and zones for every pilot");
        race.Restart();
        foreach (var rig in _rigs)
            rig.Controller?.Respawn();
    }

    // Rematch from the dogfight results board (R): every score and the clock reset, then every
    // plane back to its own spawn. Mirrors RestartRace exactly.
    private void RestartMatch(VersusMatch match)
    {
        GD.Print("dogfight: rematch — scores and clock reset for every pilot");
        match.Restart();
        foreach (var rig in _rigs)
            rig.Controller?.Respawn();
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
                Log.Info("core", $"orbit pivot from --direction: --lookat={Testing.CaptureDirector.Vec3Arg(pivot.Value)} radius={ahead:0.###}");
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

    // Teleports and activates waveNumber's built (inert) roster: a spawn drawn against every live
    // human's CURRENT position, then the fan pattern off that point's heading. A missing spawn list
    // leaves the wave parked inert with a warning rather than guessing a position.
    // ⚠ On zeppelin_run the generator arm REPLACES all of that, never adds to it: the wave is not
    // moved and no spawn is drawn, the objective zeppelin's generator is credited instead.
    private void ActivateInstantActionWave(int waveNumber)
    {
        var roster = _iaWaveRosters![waveNumber - 1];
        if (roster.Count == 0)
        {
            return; // InstantActionWaves.Start/Step never hand back an empty wave; stay defensive
        }
        if (_instantAction is { IsZeppelinRun: true } iaZepRun)
        {
            _iaLaunchWave = waveNumber;   // the decoded group stamp (the generator's +0x64)
            string objectiveZep = InstantActionRuntime.SelectedZeppelinNode(iaZepRun.Def);
            int fed = _generators?.GrantWaveCapacity(objectiveZep, roster.Count) ?? 0;
            GD.Print($"ia: wave {waveNumber} ({roster.Count} aircraft) credited to '" +
                      $"{objectiveZep}' ({fed} generator(s)) — they launch from the bay, " +
                      "not teleported");
            return;
        }
        if (_iaWaveSpawnList is not { Count: > 0 } spawns)
        {
            GD.PushWarning($"ia: no spawn points for wave {waveNumber} — {roster.Count} " +
                            "aircraft stay parked inert");
            return;
        }
        var humanPositions = new List<Vector3>();
        foreach (var rig in _rigs)
        {
            if (rig.Controller is { } human)
            {
                humanPositions.Add(human.WorldPosition);
            }
        }
        uint draw = Rng.Stream(Rng.Spawn).Randi();
        var (spIndex, sp) = InstantActionWaves.ChooseWaveSpawn(spawns, humanPositions, draw);
        var fwd = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
        for (int m = 0; m < roster.Count; m++)
        {
            var (metres, offsetDeg) = InstantActionWaves.FanOffset(m);
            var dir = fwd.Rotated(Vector3.Up, Mathf.DegToRad(offsetDeg));
            var pos = sp.Position + dir * metres;
            roster[m].Activate(pos, pos + fwd);
        }
        GD.Print($"ia: wave {waveNumber} ({roster.Count} aircraft) activated at spawn #{spIndex} " +
                  $"of {spawns.Count}");
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
        foreach (var ai in _aiPlanes)
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
            pilot.CameraOwned = true;   // D8's seam: the controller writes this pane's camera no more
            // The cockpit instruments belong to an aircraft nobody is flying; the marker HUD is a
            // sibling on the same canvas and stays, which is the whole point of the mode.
            if (pilot.Gauges != null)
                pilot.Gauges.Visible = false;
            if (pilot.Reticle != null)
                pilot.Reticle.Visible = false;
            if (pilot.WeaponReadout != null)
                pilot.WeaponReadout.Visible = false;
            var spectator = new SpectatorCamera(rig.Camera, eye,
                follow != null ? follow.WorldPosition : eye - rig.Camera.Basis.Z)
            {
                ShowReadout = _rigs.Count == 1,   // one pane, so the freecam readout has room
            };
            _worldRoot!.AddChild(spectator);
            if (follow != null)
            {
                spectator.FollowNode(follow);
            }
        }
        GD.Print($"--debug-spectate: {_rigs.Count} human(s) pinned, inert and untargetable; " +
                 (follow != null ? $"camera following {follow.Name}" : "camera free at the spawn") +
                 $" ({_aiPlanes.Count} AI aircraft flying)");
    }

    // A pilot has spent its last life. The Spectating flag pins the wreck, so neither R nor the
    // armed respawn timer flies it again, and this pane's camera goes to a SpectatorCamera locked
    // onto a still-flying human where there is one. Any translation input releases the lock.
    // ⚠ Give the spectator this pilot's own device filter, so in splitscreen two downed pilots
    // watching at once move independently rather than in lockstep.
    private void BeginInstantActionSpectate(PlayerRig rig)
    {
        if (rig.Controller is not { Spectating: false } pilot)
        {
            return;
        }
        pilot.Spectating = true;
        pilot.CameraOwned = true;   // D8's seam: this node writes nothing to the camera from here
        FlightController? follow = null;
        foreach (var other in _rigs)
        {
            if (other.Controller is { InPlay: true } live && live != pilot)
            {
                follow = live;
                break;
            }
        }
        var eye = rig.Camera.Position;   // where Crash's own cut left it (CameraController.CrashView)
        var spectator = new SpectatorCamera(rig.Camera, eye,
            follow != null ? follow.WorldPosition : eye - rig.Camera.Basis.Z,
            pilot.PadDevices, pilot.UseKeyboard)
        {
            ShowReadout = false,   // the freecam's own label would sit over a splitscreen pane
        };
        _worldRoot!.AddChild(spectator);
        _spectatorCameras.Add(spectator);   // tracked so a rerun can hand the panes back
        if (follow != null)
        {
            spectator.FollowNode(follow);
        }
        GD.Print($"ia: P{pilot.PlayerIndex + 1} is out of lives — spectating" +
                 (follow != null ? $", following P{follow.PlayerIndex + 1}" : " from the crash camera"));
        // One of the two events that can complete a stunt mission's zone sets: this pilot has
        // stopped being one the mission waits for.
        CheckInstantActionZoneSets();
    }

    // The stunt-flying end test: the zone sets are flown once every pilot who can still fly has
    // finished. ⚠ InstantActionRuntime.ZoneSetsFlown owns the rule, so the suites test the same
    // predicate the session runs. A no-op on every other mission type.
    private void CheckInstantActionZoneSets()
    {
        if (_instantAction is not { } ia)
        {
            return;
        }
        var pilots = new List<(bool OutOfLives, bool Finished)>(_rigs.Count);
        foreach (var rig in _rigs)
        {
            if (rig.Controller is { } pilot)
            {
                pilots.Add((pilot.Spectating, pilot.Stunt is { AllComplete: true }));
            }
        }
        if (InstantActionRuntime.ZoneSetsFlown(pilots))
        {
            ia.ReportObjective(InstantActionObjective.ZonesFlown);
        }
    }

    // One sim step of the Instant Action mission: the wave sequencer's tick, the mission clock and
    // the wave-cleared win signal. ⚠ Call this from BOTH drive paths, like the match clock: a
    // realtime session never enters DriveSimSteps, so a sequencer stepped only there advances no
    // wave at the controls.
    private void StepInstantAction(float dt)
    {
        if (_instantAction is not { } ia)
        {
            return;
        }
        ia.Advance(dt);
        if (_iaWaves is not { Finished: false, CurrentWave: >= 1 } waves)
        {
            return;
        }
        var waveRoster = _iaWaveRosters![waves.CurrentWave - 1];
        // ⚠ A wave member still waiting in the zeppelin's bay COUNTS as present, as the decoded walk
        // counts a still-deactivated enemy, or a credited wave reads as cleared in the frames
        // before its first launch (docs/formats/instant-action.md).
        int alive = ia.IsZeppelinRun
            ? waveRoster.Count(fc => !fc.Crashed)
            : waveRoster.Count(fc => fc.InPlay);
        int next = waves.Step(alive);
        if (next != 0)
        {
            ActivateInstantActionWave(next);
        }
        else if (waves.Finished)
        {
            // Every configured wave cleared — the squadron mode's win. Reported on every mode;
            // the runtime drops it on the ones that do not run on it (a zeppelin run's waves all
            // clear too, and the zeppelin is what decides that mission).
            ia.ReportObjective(InstantActionObjective.WavesCleared);
        }
    }

    // The launch hook handed to the objective zeppelin's generator: releases the next still-parked
    // member of the CURRENT wave at the generator's own drop point and attitude. ⚠ Do not re-derive
    // that point here. Null once the wave has nothing parked left, which the generator accounts as
    // a failed spawn.
    private FlightController? ReleaseInstantActionWaveMember(Vector3 pos, Vector3 lookAt,
        Vector3 launchVelocity)
    {
        if (_iaWaveRosters == null || _iaLaunchWave is < 1 or > 4)
        {
            return null;
        }
        foreach (var member in _iaWaveRosters[_iaLaunchWave - 1])
        {
            if (!member.Inert)
            {
                continue;
            }
            member.Activate(pos, lookAt, launchVelocity, carrierDrop: true);
            return member;
        }
        return null;
    }

    // Steps the consumers whose sim normally rides Godot's physics tick; they return early from
    // _PhysicsProcess whenever the clock is not realtime, since a fixed or halted sim cannot be
    // paced by a tick it does not own. ⚠ Keep the tree order those callbacks had, so a round fired
    // this frame behaves exactly as it did.
    private void DriveSimSteps(GameClock clock)
    {
        // --crash[=frame]: force every player's crash rig at a fixed sim frame, the only headless
        // trigger for a crash a live collision otherwise gates. Spawned AI planes crash too, while
        // an inert one declines, since DebugForceCrash is gated on InPlay.
        if (_spec.CrashFrame is int crashFrame && !_crashFired && clock.Frame >= crashFrame)
        {
            _crashFired = true;
            foreach (var rig in _rigs)
                rig.Controller?.DebugForceCrash();
            foreach (var plane in _aiPlanes)
                plane.DebugForceCrash();
        }
        // --debug-scoreboard --vs: one scripted, ATTRIBUTED kill on the first sim step, through the
        // same Downed path a real kill takes, so a screenshot has a real K/D and kill banner
        // without scripting a shot. Same single-fire shape as --crash above.
        if (_spec.Versus && _spec.DebugScoreboard && !_versusDebugKillFired && _rigs.Count > 1)
        {
            _versusDebugKillFired = true;
            _rigs[1].Controller?.DebugForceCrash(_rigs[0].Controller?.PlayerIndex);
        }
        // --debug-scoreboard (IA): force this mission's own win signal on the first sim step, the
        // same single-fire shape as the two blocks above, attributed to P1 so the wrap-up board
        // reads non-zero. Which modes have a force at all: docs/architecture.md on GameSession.cs.
        if (_instantAction is { } iaDebug && _spec.DebugScoreboard && !_iaDebugForceFired)
        {
            _iaDebugForceFired = true;
            int? attributedTo = _rigs.Count > 0 ? _rigs[0].Controller?.PlayerIndex : null;
            if (iaDebug.Objective == InstantActionObjective.AceDown)
            {
                _iaAce?.DebugForceCrash(attributedTo);
            }
            else if (iaDebug.Objective == InstantActionObjective.WavesCleared && _iaWaveRosters != null)
            {
                // DebugForceCrash self-gates on InPlay, so this reaches only whatever wave
                // is currently active — the rest are still parked inert awaiting their own turn.
                foreach (var roster in _iaWaveRosters)
                    foreach (var member in roster)
                        member.DebugForceCrash(attributedTo);
            }
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
            // Zeppelins move before the generators read their host altitude this step.
            _zeppelins?.SimStep(dt);
            // Emplacements after the zeppelins: a slung mount reads its ride's moved pose.
            _turretEmplacements?.SimStep(dt);
            // Generators step before the AI-plane loop below: a spawn appends to _aiPlanes, which
            // must not happen while that list is being enumerated (the new plane ticks next step).
            _generators?.SimStep(dt);
            // AI aircraft step after the player rigs — the tree order their _PhysicsProcess
            // callbacks take on a realtime clock, since they spawn after every rig is built.
            foreach (var ai in _aiPlanes)
            {
                ai.SimStep(dt);
            }
            // E11/G13: one sequencer tick and one mission-clock step per sim step, after the AI
            // planes above have taken this step's crashes — the alive count
            // InstantActionWaves.Step reads must reflect them.
            StepInstantAction(dt);
            // The smoke screens after every aircraft has moved this step: the walk reads the
            // layer's and the victims' poses as they stand now, as the original's does.
            _smokeScreens?.SimStep(dt);
            // The tags after the pool has hit and the aircraft have died this step: a tag on a
            // crashed aircraft collapses on the same step's tick, and the per-step tag gate re-arms
            // only once the pool's hits are in.
            _beeperTags?.SimStep(dt);
            // The voice dispatch's mission clock: the 2 s mute window and every 15 s
            // slot cooldown run on sim time, so a halted clock halts the chatter too.
            _aiVoice?.Step(dt);
            // The weapon lab has no sim step of its own: it is hosted by player 1's
            // FlightController, which owns the fire clock, and fires into _projectiles above.
            _versus?.Advance(dt);
        }
    }

    // The livery a wave flies in: its militia's pattern, in that pattern's shipped colours. The
    // original paints a wave member from the setup screen rather than from a vehicle def, so this
    // holds for a pair the install ships no def for (Sacred Trust's Warhawk) as much as for one it
    // does. Null when the militia is not named or names no pattern, and the member keeps its skins.
    private PaintScheme? WaveMilitiaScheme(BuildState state, string enemyName)
    {
        if (MilitiaPaint.PatternForWave(MilitiaPatterns(state), enemyName) is not { } pattern)
            return null;
        foreach (var scheme in _liveryResolver.PaintCatalog(state.ZrdrPath))
            if (string.Equals(scheme.Pattern, pattern, StringComparison.OrdinalIgnoreCase))
                return scheme;
        return null;
    }

    private IReadOnlyDictionary<string, string> MilitiaPatterns(BuildState state)
    {
        if (_militiaPatterns != null)
            return _militiaPatterns;
        try
        {
            _militiaPatterns = MilitiaPaint.PatternByMilitia(state.ZrdrPath, Messages.Load(state.MessagesPath));
        }
        catch (Exception e)
        {
            GD.PushWarning($"ia: cannot read the militia paint patterns: {e.Message}");
            _militiaPatterns = new Dictionary<string, string>();
        }
        return _militiaPatterns;
    }

    // The AI flavour of one airframe as the roster would load it, or null before the rigs are
    // built. Read for the def's own pilot facts at spawn; the roster reads the same cached object.
    private PlaneStats? AiStatsForSpawn(string planeName, string? aiDef)
    {
        if (_aiStatsFor == null)
            return null;
        try
        {
            return _aiStatsFor(planeName, aiDef);
        }
        catch (Exception e)
        {
            GD.PushWarning($"ai: cannot resolve '{aiDef ?? planeName}': {e.Message}");
            return null;
        }
    }

    // Gives a spawned AI aircraft its voice: the talker and constitution chances come from
    // ai_skill_parameters at ratingOverride when given, else the session's skill rating. A missing
    // accent, voice runtime or skills table means a silent pilot, never an error.
    private void RegisterAiVoice(FlightController? ai, int? accentId, int? ratingOverride = null)
    {
        if (ai == null || accentId is not { } accent || _aiVoice == null || _aiSkills == null)
        {
            if (accentId != null && (_aiVoice == null || _aiSkills == null))
            {
                GD.Print($"ai voice: accent {accentId} ignored — no voice runtime in this session");
            }
            return;
        }
        int rating = ratingOverride ?? _spec.AiAttackSkill ?? 5;
        _aiVoice.RegisterAi(ai, accent,
            _aiSkills.At("talker_chance", rating), _aiSkills.At("constitution_chance", rating));
    }

    // Per-build state threaded through StartSession's phase methods: the archives, world-build
    // outputs and running counts. ⚠ Nothing here may be cached across a rebuild.
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
