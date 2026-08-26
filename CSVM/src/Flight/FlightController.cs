using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The flying aircraft: polls keyboard + gamepad into a <see cref="FlightModel"/>, applies the
/// result to this node's transform, and drives the chase camera plus a minimal text HUD.
/// Controls: docs/controls.md. Collaborators and other constraints: this module's entry in
/// docs/architecture.md.
/// Hitting terrain or a building crashes the plane: explosion, airframe hidden, frozen at the
/// impact point until R (or gamepad Y/A) respawns.
/// ⚠ P (or gamepad Start) halts the WHOLE simulation, not just this plane; see
/// <see cref="PauseTogglePressed"/>.
/// </summary>
public partial class FlightController : Node3D
{
    /// <summary><c>weapons.gunAmmoCap</c> (config.json) default: 0 = off, full stock gun capacity.
    /// A positive value caps every firable gun group's load to that many rounds — a testing knob for
    /// the low-ammo cases (chiefly the on-empty group hand-off) that thousands of stock rounds make
    /// tedious to reach. Capped at each group's real capacity; the gauge reads full at the cap.</summary>
    public const int GunAmmoCapDefault = 0;

    /// <summary><c>weapons.ordnanceCap</c> (config.json) default: 0 = off, full stock pylon load —
    /// the same knob as <see cref="GunAmmoCapDefault"/>, for hardpoints instead of gun groups.</summary>
    public const int OrdnanceCapDefault = 0;

    /// <summary>Own-plane sound, if the sound archive was found (add as a child too).</summary>
    public FlightAudio? Audio;

    /// <summary>The positional twin of <see cref="Audio"/>, carried by an AI-flown aircraft instead
    /// of it: engine loops on 3D emitters, culled by distance. Never both — own-ship audio is
    /// non-positional by design, and a rig with a person in it takes <see cref="Audio"/>.</summary>
    public AiEngineAudio? EngineAudio;

    /// <summary>The visible aircraft model (a child of this node); hidden while crashed.</summary>
    public Node3D? PlaneModel;

    /// <summary>The per-mode hiding of this pilot's OWN aircraft while a first-person view is on
    /// the screen (interior in, body out; Nose also drops markers/dontmove). Null on every rig that
    /// was not built an interior — AI planes and the labs — which then never hides anything.</summary>
    public CockpitVisibility? Cockpit;

    /// <summary>The wobble oscillators and the pivot they roll — the node the assembler hung
    /// <see cref="PlaneModel"/> under. Null when no rig assembly ran (parked lab planes).</summary>
    public PlaneShake? Shake;
    public Node3D? ShakePivot;

    /// <summary>Spins the plane's propeller/rotor blur discs; advanced each frame,
    /// throttle-scaled. Null if the model has no propeller nodes.</summary>
    public PropAnimator? Props;

    /// <summary>Flashes the plane's wingtip flares on the original's 1.5 s cycle; advanced
    /// each frame. Null if the model has no wing-flare nodes.</summary>
    public WingLightBlinker? WingLights;

    /// <summary>The throttle-slam exhaust smoke; advanced each frame. Null if the model has no
    /// exhaust nodes.</summary>
    public ThrottleSlamSmoke? ThrottleSmoke;

    /// <summary>The chapter-authored pale speed wisps spawned ahead of this aircraft; one private
    /// instance per rendered player view.</summary>
    public SpeedCue? SpeedCue;

    /// <summary>Deflects the plane's ailerons/elevators/rudders with stick input;
    /// advanced each frame. Null if the model has no control-surface nodes.</summary>
    public ControlSurfaceAnimator? Surfaces;

    /// <summary>The airframe collision boxes (fuselage/wings/tail), swept along each
    /// physics frame's motion so wingtips and tail collide with obstacles. Null falls
    /// back to the center-ray-only test.</summary>
    public PlaneCollider? Collider;

    /// <summary>The airframe's physics body on the aircraft collision layer, built in
    /// <c>_Ready</c> from <see cref="Collider"/>'s own boxes: what a projectile ray or another
    /// plane's sweep strikes. This plane's own queries exclude it (<see cref="AircraftBody.ExcludeSelf"/>).
    /// Null when no collider boxes could be derived — the plane is then unhittable.</summary>
    public AircraftBody? Body;

    /// <summary>Per-part hit points from the vehicle def's destroyable_parts.
    /// When set, collisions below the crash threshold damage the struck
    /// part and the plane flies on; null means any hit crashes.</summary>
    public PlaneDamage? Damage;

    /// <summary>Applies a plane collision's health damage to the struck world node, returning true
    /// iff it was a <c>WeaponOrCollideHit</c> destructible (the 44 facades/windows/agyrobus), in
    /// which case the object breaks and the plane flies THROUGH it. EVERY destructible takes the
    /// damage; the return value is the plane's fate alone. Wired to
    /// <c>AnimRuntime.CollideDamageAt</c>; null (a viewer/static build with no world runtime)
    /// makes every collision solid and harmless.</summary>
    public System.Func<Node?, float, bool>? CollideDamageSink;


    /// <summary>Plays a named effect def at a world point through the session's world-effects
    /// runtime — the survivable graze's authored <c>touchdown_*</c> reaction. Same sink shape
    /// as <c>ProjectilePool.EffectSink</c>; null (no world, or a build with no effects runtime)
    /// leaves the scrape's sound without its sparks/dust/splash.</summary>
    public System.Action<string, Vector3>? GrazeEffectSink;

    /// <summary>The graze family's def vector — the session's ONE <c>touchdown_*</c> table
    /// (<c>WorldEffectsFactory.TouchdownDefs</c>), indexed by the scraped material's surface id by
    /// the same cascade <see cref="CrashDefs"/> uses. Null on a build with no world-effects runtime,
    /// which leaves a scrape with neither its def nor its bark, exactly as the original leaves it
    /// when the vector cannot answer, because the sound is authored inside the def.</summary>
    public SurfaceDefTable? TouchdownDefs;

    /// <summary>Visible damage: torn-skin pdpanel flips + the authored damage-stage anims
    /// (panel burns, fuel leak, heavy prop1 trail), driven from the data's injure_anims
    /// thresholds through the rig runtime. Optional.</summary>
    public DamageVisuals? Visuals;

    /// <summary>This plane's stock loadout bound to its model — the gun groups (with independent
    /// ammo counters) + hardpoints the firing code draws from. Null disables weapons.</summary>
    public Loadout? Loadout;

    /// <summary>The shared world's projectile/effect pool guns and hardpoints fire into. Null
    /// disables weapons.</summary>
    public ProjectilePool? Projectiles;

    /// <summary>The FLYOUT-model rockets mounted under the wings, one per loaded pylon, hidden as
    /// each pylon's ammo depletes. Rides the plane; null when nothing could be mounted (viewer, or a
    /// chapter gamez lacking the prototype roots).</summary>
    public PylonOrdnance? Ordnance;

    /// <summary>The world's smoke screens, which a <c>SMOKE_SCREEN</c> pylon lays into instead of
    /// spawning a round. Null in a session that runs no screens (the weapon lab, a suite): the
    /// weapon then still spends its ammo and lays nothing.</summary>
    public SmokeScreens? SmokeScreens;

    /// <summary>--infinite-ammo: guns/hardpoints fire without depleting (frictionless testing).</summary>
    public bool InfiniteAmmo;

    /// <summary>--ammo=N: overrides both <c>weapons.gunAmmoCap</c> and <c>weapons.ordnanceCap</c> at
    /// rig build, so the low-ammo start survives <c>--det</c> (which drops config.json entirely).
    /// Null when the flag was absent — falls through to the config.json knobs.</summary>
    public int? AmmoCapOverride;

    /// <summary>--fire: hold the gun trigger down (scripted screenshot / soak runs), as a scripted
    /// hold profile does for flight input.</summary>
    public bool AutoFire;

    /// <summary>--fire-rockets: hold the rocket trigger down (scripted screenshot / soak runs).
    /// Unlike a human pull (one rocket per press), this auto-repeats at the launch cooldown.</summary>
    public bool AutoFireRockets;

    /// <summary>--gun-select=N: the gun selector's initial firable group (0-based; 0 = the first
    /// group, the default). Only one gun group fires at a time. A headless testing hook so a scripted
    /// run can fire one group in isolation; interactively the selector cycles with G / gamepad D-pad Left.</summary>
    public int InitialGunSelect;

    /// <summary>This airframe's DESTROY def — the anim slot the original starts the instant health
    /// reaches zero (<c>fury-fury</c>, <c>player-player</c>), where <see cref="CrashDefs"/> is the
    /// GROUND-IMPACT slot. Set alongside <see cref="CrashRuntime"/>; null leaves a kill with no
    /// wreck at all, which is what hiding the airframe on the death frame used to look like.</summary>
    public string? DestroyDef;

    /// <summary>Whether <see cref="DestroyDef"/> takes the hull over itself (an <c>ObjectMotion</c>
    /// on <c>MAIN_ROOT_NODE</c>, with its own bounce landing) — true on all eleven airframe defs,
    /// false on <c>player</c>. It says who LANDS the wreck, not who flies it: every dead hull flies
    /// itself until the def's <c>Callback 15</c>. Derived by <c>EffectCatalogue.FliesOwnHull</c>.</summary>
    public bool DestroyDefFliesWreck;

    /// <summary>The stunt run, when flying --stunt: danger-zone sphere
    /// detection, tested against the plane each physics frame. Deliberately NOT reset on
    /// respawn — a mid-run crash keeps completed zones (the clock keeps running).
    /// Null in free flight.</summary>
    public StuntMission? Stunt;

    /// <summary>The end-of-run results overlay: splits + total + best-time on
    /// AllComplete. Added to the HUD canvas last (drawn over the marker/dials); wakes itself on
    /// the run's RunCompleted. Null in free flight.</summary>
    public StuntScoreboard? Scoreboard;

    /// <summary>The Dogfight per-pane HUD, <c>--vs</c> only: the match timer/K-D/leader line, the
    /// kill banner, and the opponent markers. Added to the HUD canvas; fed nothing per frame (it
    /// pulls VersusMatch's own live state) beyond the kill facts GameSession pushes through its
    /// OnKill.</summary>
    public VersusHud? VersusHud;

    /// <summary>The splitscreen stunt race this plane is one seat of, or null when
    /// flying solo. Set, clearing every zone parks this player at the finish while the others fly
    /// on, and R only becomes a rematch once the whole field is in — a rematch restarts every
    /// player, so it goes through <see cref="RestartRace"/> rather than this plane alone.</summary>
    public StuntRace? Race;

    /// <summary>Restarts the whole race (the session owns every player's plane, so it does the
    /// work). Invoked when a player presses R on the shared results board.</summary>
    public Action? RestartRace;

    /// <summary>The dogfight this plane is one seat of, or null outside <c>--vs</c>. Set, once
    /// <see cref="VersusMatch.Completed"/> the results board is up and any player's R there means
    /// "rematch" instead of "respawn me" — checked before the crash branch, exactly the same
    /// R-ownership rule <see cref="Race"/>/<see cref="RestartRace"/> already follow (the board
    /// owns R only while it is visible, which mirrors <c>Completed</c> exactly).</summary>
    public VersusMatch? Match;

    /// <summary>Restarts the whole match (the session owns every player's plane, so it does the
    /// work): scores and clock reset, every plane respawns. Invoked when a player presses R on the
    /// dogfight results board.</summary>
    public Action? RestartMatch;

    /// <summary>Splitscreen pause bookkeeping — the SAME instance on every rig
    /// (assigned by <c>GameSession</c>, the same way <see cref="Match"/> is), so any player's
    /// Start/P here can pause everyone but only <see cref="PauseState.OwnerPlayerIndex"/> can
    /// resume. Null only where no session builds one (the suites' bare rigs), in which case
    /// <see cref="AllowPause"/> is always false there too, so <see cref="PauseTogglePressed"/>
    /// never fires and the null is never read.</summary>
    public PauseState? PauseState;

    /// <summary>0-based player index — this plane's seat in the race and its pane, and the
    /// identity a round it fired carries (<c>ProjectilePool.Spawn</c>'s shooter id).</summary>
    public int PlayerIndex;

    /// <summary>This pilot's target selection, or null for a seat that
    /// does no targeting (every AI rig, and the suites' bare rigs). Set by
    /// <c>HumanFlightAdapter</c> on each human pane; <see cref="StepTargeting"/> feeds it every
    /// frame. Read <c>Targeting.Current</c> for the selected target — that is the property the
    /// marker, Track Target's camera and any later AI-order consumer are meant to read.</summary>
    public TargetSelection? Targeting;

    /// <summary>Appends the mission's selectable structures (the zeppelin sub-parts) to the
    /// targeting pool each frame — <c>ZeppelinRuntime.CollectTargetParts</c>, bound by
    /// <c>GameSession</c> once the zeppelins exist. Null in a session with none. A delegate because
    /// the zeppelins are BUILT AFTER the rigs, so there is no runtime to hand the assembler; the
    /// same sink shape <see cref="CollideDamageSink"/> already uses.</summary>
    public System.Action<List<AimCandidate>>? TargetSubParts;

    /// <summary>Appends the campaign mission's live objective sites to the targeting pool each
    /// frame (<c>ObjectiveSites.Collect</c>, bound by <c>GameSession</c>). Null outside a campaign
    /// session. A channel of its own rather than <see cref="TargetSubParts"/>: a site carries the
    /// mission's <c>objectiveTarget</c> flag and rides the Enemy cycle, while a sub-part carries
    /// <c>otherTarget</c> and rides the Non-Aircraft one.</summary>
    public System.Action<List<AimCandidate>>? TargetObjectives;

    /// <summary><c>--target=</c>'s spec, or null for an unscripted session. Applied ONCE, on
    /// the first frame <see cref="Targeting"/>'s pool has anything in it, and never consulted again —
    /// it sets the initial selection, it does not hold it, so an interactive session started with the
    /// flag still cycles normally.</summary>
    public string? InitialTarget;

    /// <summary>Whether a person is flying this plane. Gates the gun aim assist
    /// (B6): true runs <see cref="AimAssist"/> as normal, false takes the muzzle axis
    /// unassisted, the same fallback a barrel with no slot already uses. Defaults true; the AI
    /// spawner sets it false. It is the original's human-versus-AI split (`FUN_004b6530`'s
    /// else-branch), not "pane 1 only".</summary>
    public bool IsHumanPiloted = true;

    /// <summary>This plane's carried turret gunners: built by the rig assembler from the
    /// vehicle def's <c>turrets</c> block against <c>ai.zrd</c>, ticked from <see cref="SimStep"/>
    /// (so a crash silences them), and collected into every shooter's aim-assist candidate set
    /// through <see cref="ProjectilePool.CollectTurrets"/>. Empty on the six turretless airframes.</summary>
    public TurretController[] Turrets = Array.Empty<TurretController>();

    /// <summary>The non-player input source: set (with <see cref="IsHumanPiloted"/> false), it
    /// replaces the keyboard/pad read each sim step, the way a scripted hold profile does for
    /// scripted runs — collision, weapons, damage and crash downstream are byte-for-byte the
    /// player's path. ⚠ The FLIGHT MODEL is the one exception: construction selects the AI force
    /// path off this same split (<see cref="FlightModel.UsesAiForcePath"/>).</summary>
    public AiPilot? Pilot;

    /// <summary>The nitro boost lifecycle. <see cref="NitroSystem.Installed"/> is the build's
    /// (the hangar's nitrous engine pick, or the AI spawn's roster flag); the command arm, the
    /// AI maneuver arm, the tank and the animation edges run from <see cref="SimStep"/>.</summary>
    public NitroSystem Nitro = new();

    /// <summary>Where the human pilots are, as one snapshot per call — the seam the flight model's
    /// far-field plant is selected on (<see cref="FlightModel.FarFieldPlant"/>). The session binds
    /// the same snapshot every other "who is nearest" consumer reads. Null (every rig built without
    /// a session) leaves the distance at zero, which keeps that rig on near-field aerodynamics.
    /// ⚠ Set on AI rigs. A human rig's nearest human is itself, so the distance is always 0.</summary>
    public Func<IReadOnlyList<Vector3>>? HumanPositions;

    /// <summary>The world's destructibles, when this session has a world runtime — the aim assist's
    /// third candidate list (an approximation of the original's `targets.zrd`
    /// `MStructList`). Null in every build with no world (the weapon lab, the suites), which costs
    /// the scan nothing: that pass simply iterates an empty list.</summary>
    public DestructibleRegistry? Destructibles;

    /// <summary>Draw the collision probe — the swept ray plus the airframe boxes the
    /// crash test sweeps each physics frame — in green (red on the impact frame).</summary>
    public bool DebugCollision;

    /// <summary>The gamepad devices that fly THIS plane (splitscreen). Null — the
    /// single-player default — means every connected pad flies it (see <see cref="PadPressed"/>).
    /// In splitscreen each player is bound to its own device so P2's stick never moves P1.</summary>
    public int[]? PadDevices;

    /// <summary>Whether the keyboard flies this plane. Single player and splitscreen P1: true;
    /// P2–P4 are pad-only (there is one keyboard).</summary>
    public bool UseKeyboard = true;

    /// <summary>Where the HUD <see cref="CanvasLayer"/> is parented. Null (single player) keeps it
    /// a child of this node, i.e. the main viewport; splitscreen sets the player's SubViewport so
    /// the dials/compass draw in that player's pane only.</summary>
    public Node? HudParent;

    /// <summary>Whether P / gamepad-Start reads on this rig at all. True for every human rig,
    /// splitscreen included: the freeze halts the shared simulation for everyone
    /// regardless of who pressed it, and <see cref="PauseState"/> is what keeps a second player
    /// from stealing the resume. False for AI rigs and the suites' bare test rigs, which have no
    /// pause key to read.</summary>
    public bool AllowPause = true;

    /// <summary>Debug/testing (--debug-scoreboard): force-complete the stunt run on the first
    /// physics frame so the results scoreboard renders deterministically for a screenshot. No
    /// effect without a stunt run.</summary>
    public bool DebugCompleteStunt;

    /// <summary>The numpad camera view (1–9, 5 unbound) held for the whole run — the scripted twin
    /// of holding the key, so a capture can frame the belly or a flank of a flying plane. 0 (the
    /// default) is the chase camera, i.e. exactly today's behaviour. A key held at the controls
    /// wins over this while it is down.</summary>
    public int PinnedView;

    /// <summary>The view mode this pilot starts in (<c>--view=cockpit</c>/<c>=nose</c>); Chase, the
    /// default, is exactly today's behaviour. The live value is the camera's
    /// (<see cref="CameraController.ViewMode"/>) once <see cref="Setup"/> has run, because the
    /// cycle key changes it; this field only seeds it.</summary>
    public PilotViewMode PinnedViewMode = PilotViewMode.Chase;

    /// <summary>Out of lives: this pilot stays crashed for the rest of
    /// the mission — neither R nor <see cref="AutoRespawnAfter"/>'s timer brings it back — while
    /// the session hands its pane to a <see cref="SpectatorCamera"/> and the others fly on. Set by
    /// the session's own lives ledger (<c>InstantActionRuntime.NotifyPilotDown</c>), never from
    /// here; this node holds no mission state and decides no rule.</summary>
    public bool Spectating;

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    // Spawn throttle/speed come from the mission's PLAYER_INIT via Setup (docs/formats/spawns.md).
    // ⚠ These two are only the no-mission fallback (labs, tests, AI rigs), and they are the OLD
    // placeholder, deliberately: the original gives an AI aircraft min(plane_speed_max, fd_speed),
    // which is decoded but not landed, so moving them to the player's 18 m/s would be a third
    // invented answer rather than that rule (docs/org/flightModel.md, "What this changes" #13).
    private const float FallbackSpawnThrottle = 0.5f;
    private const float FallbackSpawnSpeed = 53.6f;
    private const float CarrierDropThrottle = 0.1f;
    private const float UnderMapY = 0f;        // C1 terrain sits at y≈100+; below this we're lost
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)
    // The nitro_decay def's authored opacity fade (RUN_TIME 1.0), which is how long a re-engage
    // stays refused for; the runtime has no completion callback to read it off.
    private const float NitroDecayAnimSeconds = 1f;
    private const float DebugFinishStagger = 1.5f; // s between players' forced finishes (--debug-scoreboard in a race)

    private const float GrazeReactionInterval = 1.5f; // s between graze reactions — NOT a tuned value:
                                                      // the touchdown defs stop their own puffer at
                                                      // ANIMATION_OFFSET 1.5, so this is one whole authored
                                                      // reaction per scrape rather than a restart per frame
    private const int InitialTargetGrace = 300;    // frames --target= waits for the pool to fill
    private const float TargetHoldSeconds = 0.25f; // decision 7: D-pad Up past this is a HOLD,
                                                   // not a tap. ⚠ TUNE — ours, not the original's,
                                                   // which needs no threshold because it has a key
                                                   // per action
    private const float PropIdleSpin = 0.4f;    // blur discs still turn at zero throttle (windmilling)

    // The head-look recenter key: the original's Kp5 "Look Forward", the middle of the numpad
    // cluster its eight direction slots surround.
    private const Key SnapCenterKey = Key.Kp5;

    // Everything this pane draws for its pilot. Always present, so no site has to ask whether
    // there is a HUD: an aircraft with no readouts built simply has a module that draws nothing.
    private readonly FlightHud _pilotHud = new();
    // Which state this aircraft is in and what moves it between them, including the spawn timers.
    // Every transition below reports what it did and this node performs it (Decision 7).
    private readonly AircraftLifecycle _lifecycle = new();
    private readonly SweepCadence _sweep = new();    // the original's alternate-step sweep and its carried motion
    private readonly AimCandidateSet _aimCandidates = new(); // rebuilt once per fire call (B4/B5)
    private readonly AimCandidateSet _gunnerScan = new();    // the AI gunner's acquisition scan (D14)
    private readonly List<RocketPylonView> _pylonViews = new();          // the AI rocketeer's pylon walk
    private readonly List<RankedTargetCandidate> _rankCandidates = new(); // the D12/D36 ranking snapshots
    // …and their sources, by index: a FlightController, a TurretController or a
    // DestructibleRegistry.Instance (the D36 widening, BL-363's decoded turret/structure pools).
    private readonly List<object?> _rankSources = new();
    private readonly RandomNumberGenerator _aimRng = Rng.Stream(Rng.Weapons); // the assist's 1° launch scatter
    private readonly AimCandidateSet _targetScan = new();   // the targeting pass's own scan, rebuilt per frame
    private readonly List<AimCandidate> _targetParts = new(); // this frame's selectable sub-parts
    private readonly List<AimCandidate> _targetSites = new(); // this frame's objective sites
    private readonly bool[] _targetKeyPrev = new bool[5];   // T/Y/U/I/O edge detection
    private readonly bool[] _viewModeKeyPrev = new bool[4]; // F8/F6 + pad view-selection edges
    private readonly TapHoldButton _targetHold = new(TargetHoldSeconds); // D-pad Up tap vs hold

    private bool _initialTargetDone;             // --target= has had its one chance
    private int _initialTargetWaits;             // …frames it has waited for a non-empty pool
    private FlightModel _model = null!;
    private CameraController? _cam;              // null on an AI rig — no view rides this plane
    private Camera3D? _viewCamera;
    private CanvasLayer? _hudCanvas;             // the whole HUD layer; hidden while crashed (the
                                                 // original's crash camera shows no HUD — footage);
                                                 // never built on an AI rig
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _spawnThrottle = FallbackSpawnThrottle;
    private float _spawnSpeed = FallbackSpawnSpeed;
    private float _throttle;
    private float _keyPitch;                     // the three keyboard axes' own deflection, ramped
    private float _keyRoll;                      // by StickRamp; a gamepad's analogue axis adds on
    private float _keyYaw;                       // top and is never ramped
    private double _sinceTelemetry;
    private WarningShotCue? _warningShots;       // the near-miss cue's shipped accumulator
    private FlightInput _lastInput;              // this physics frame's stick input (drives the surfaces)
    // s until auto-rematch on a finished race (scripted hold runs only). Not the lifecycle's
    // respawn timer: this one belongs to a results board, which no aircraft state reaches.
    private float _autoRestartIn = AircraftLifecycle.AutoRespawnDelay;

    /// <summary>When set (from <see cref="FlightControllerBuild.HoldSegments"/>), replaces keyboard
    /// input for automated screenshot/demo runs: each segment holds its input for its duration
    /// (seconds of sim time), the last holds forever, and a respawn restarts the sequence
    /// (<see cref="ScriptedInputSource.Reset"/>). Such runs are unattended, so a crash
    /// auto-respawns after a short pause.</summary>
    private (FlightInput Input, float Duration)[]? _holdSegments;
    private bool _pausePrev;                     // previous frame's pause-key state (edge detection)
    private bool _haltPrev;                      // previous frame's clock-halt state (orbit seeding)
    private bool _cyclePrev;                     // previous frame's stunt cycle-target key state (edge detection)
    private ImmediateMesh? _probe;               // debug collision-probe line
    // The rest pose of every node the crash def flings (the destroyed wreck's pieceN meshes) and
    // the BUILT visibility of every plane-model node, both captured before the first crash so
    // Respawn can undo what the def did: a RESET_STATE re-poses only the nodes it names and
    // restores only dontmove, so neither the flung pieces nor the built-hidden torn panels and
    // wingtip flares come back without these two snapshots. Bound with the rest of the crash rig.
    private IReadOnlyList<(Node3D Node, Transform3D RestPose)>? _crashRestPoses;
    private IReadOnlyList<(Node3D Node, bool Visible)>? _crashPlaneVisibility;
    private IWorldQuery? _worldQuery;             // the sweep/ray seam; bound in Bind, lazy for bare test rigs
    private AircraftContactResolver? _contacts;   // the contact rules; lazy, over the same seam
    private IFlightInputSource? _inputSource;     // which stick flies this aircraft; bound in Bind, lazy for bare test rigs
    private float _grazeReactionCooldown;        // s left before the next touchdown_* reaction
    private int _projectileHitsLogged;           // verification breadcrumb: the first few hits log
    private FireControl? _fire;                  // the fire-control state machine; built in _Ready with the loadout
    private GunGroup[] _firableGuns = Array.Empty<GunGroup>(); // the firable gun groups in _fire's slot order (muzzle nodes, live ammo)
    private GunAimSlot[][] _aimSlots = Array.Empty<GunAimSlot[]>(); // per _firableGuns group, one slot per muzzle — B2's assist state
    private bool _aimLoggedFirst;                // verification breadcrumb: the assist's first snap logs once
    private bool _aimListsLogged;                // verification breadcrumb: the candidate list sizes log once
    private bool _groundBlowLoggedFirst;         // verification breadcrumb: ground blow's first repelling hit
    private bool _gunnerLoggedTarget;            // verification breadcrumb: the AI gunner's first acquisition
    private bool _gunnerLoggedFire;              // verification breadcrumb: the AI gunner's first open fire
    private bool _rocketeerLoggedFire;           // verification breadcrumb: the AI's first ordnance launch
    private bool _gunLoopOn;                     // the firing loop sound is currently playing
    private bool _aiNitroArmed;                  // the current AI nitro maneuver already engaged
    private float _nitroDecayLeftS;              // s the nitro_decay def has left to play
    private bool[] _gunLoggedFirst = Array.Empty<bool>(); // verification breadcrumb: each group logs its first live round once
    private int _rocketsLaunched;                // verification breadcrumb: the first few launches log their pylon
    private int? _team;                          // Team's backing field — null until overridden (B7)
    private bool _held;                          // Held's backing field — the airframe is pinned (weapon lab)
    private bool _cameraOwned;                   // CameraOwned's backing field — the lab's free camera has the view
    private bool _orbitPrev;                     // edge detection for entering the orbit (halt or hold)
    private bool _reseedOrbit;                   // the free camera handed the view back; re-seed from where it left it
    private bool _heldPinned;                    // the pinned pose below is valid (captured on the first held step)
    private Vector3 _heldPos;                    // the pinned position, re-applied through the model every held step
    private Basis _heldAttitude;                 // the pinned attitude, ditto
    private Vector2 _mouseLookPrev;              // last frame's screen mouse position (head-look motion)

    // The sim advances on the 60 Hz physics tick while rendering runs at the display rate, so
    // drawing the raw sim pose stutters the plane against the smoothly-moving chase camera at
    // any render rate above 60 fps, in proportion to speed. SimStep records the last two sim
    // poses; _Process draws between them at the physics interpolation fraction. Realtime clock
    // only — a parent-driven (fixed-dt) clock draws the exact sim pose, keeping scripted
    // captures byte-identical.
    private Transform3D _simPrev = Transform3D.Identity;
    private Transform3D _simCurr = Transform3D.Identity;
    private Transform3D _renderPose = Transform3D.Identity; // the pose actually drawn this frame

    /// <summary>Raised once per crash, at <see cref="Crash"/>: (victim <see cref="PlayerIndex"/>,
    /// killer shooter id). Null for terrain, mid-air, an unowned round or any other crash cause. A
    /// fact report, not a score — this node knows no match rules; the session subscribes and scores
    /// when a match exists. Respawn emits nothing.</summary>
    public event Action<int, int?>? Downed;

    /// <summary>Raised whenever <see cref="Inert"/> flips, with this aircraft — the seam a
    /// session-level roster (the E16 voice dispatch's speaker list) mirrors the state into, since
    /// nothing outside this node polls it.</summary>
    public event Action<FlightController>? InertChanged;

    /// <summary>Raised on every projectile hit that moved this plane's damage state without
    /// destroying it (the destroying hit reports through <see cref="Downed"/> instead) — the
    /// E16 voice runtime reads the whole-vehicle summary off it for the DI distress tiers and
    /// the player's WA-HighDmg crossing. Terrain grazes do not raise it; the decoded distress
    /// sites are the combat hit path's.</summary>
    public event Action<FlightController>? DamageApplied;

    /// <summary>This aircraft's team — everywhere "is this hostile" is asked reads this instead of
    /// deriving a team from <see cref="PlayerIndex"/> (shooter ids are not team ids). Unset, it
    /// falls back to <see cref="AimAssist.TeamOfPilot"/>, so free flight and <c>--vs</c> keep every
    /// pane on its own team; a mission builder sets it to a fixed team explicitly.</summary>
    public int Team
    {
        get => _team ?? AimAssist.TeamOfPilot(PlayerIndex);
        set => _team = value;
    }

    /// <summary>The weapon lab's hold: the airframe holds its pose while everything else in the
    /// session keeps running (props, guns, rounds, world sim). ⚠ NOT the P halt
    /// (<see cref="GameClock.Halted"/>), which stops the whole clock. Clearing it un-pins the
    /// airframe at zero speed; <see cref="PlaceHeld"/> moves the pin.</summary>
    public bool Held
    {
        get => _held;
        set
        {
            _held = value;
            if (!value)
            {
                _heldPinned = false;   // a later re-hold pins wherever the plane is then
            }
        }
    }

    /// <summary>Current throttle (0-1), the live flight model's own value — exposed so the rig
    /// assembler can seed <see cref="ThrottleSmoke"/> at build time, after <see cref="Setup"/> has
    /// already placed the plane at its spawn throttle.</summary>
    public float Throttle => _model.Throttle;

    /// <summary>Seconds left on this aircraft's engine-dead timer, zero when the engine runs. The
    /// choker's one observable, since nothing else on that path changes (see
    /// <see cref="TryChokeEngine"/>).</summary>
    public float EngineDeadRemainingS => _model.EngineDeadRemainingS;

    /// <summary>The view this pilot has selected, live. Falls back to <see cref="PinnedViewMode"/>
    /// before <see cref="Setup"/> has built a camera, and reads Chase on an AI rig, which has
    /// none.</summary>
    public PilotViewMode ViewMode => _cam?.ViewMode ?? PinnedViewMode;

    /// <summary>Whether this pilot is in one of the two first-person views — the session's feed for
    /// the anim data's <c>PLAYER_1ST_PERSON</c> condition (id 120).</summary>
    public bool FirstPersonView => PilotView.IsFirstPerson(ViewMode);

    /// <summary>This airframe's stats, the flight model's own copy (jittered for an AI spawn, so it
    /// is the plane's data and not the cached def's). Read for the airframe's DISPLAY NAME
    /// (<c>PlaneRoster.PlaneDisplayName</c> → <c>Fury</c>) by the targeting pool's label pass. Null
    /// before <see cref="Setup"/> has bound a flight model, which is a bare suite rig; a caller
    /// that wants a name falls back to the node's.</summary>
    public PlaneStats? Stats => _model?.Stats;

    /// <summary>The data-driven crash: a per-player <see cref="AnimRuntime"/> bound to this plane's
    /// scoped crash subtree, advancing itself in its own <c>_Process</c>. Plays the compiled crash
    /// def the struck surface selects; see this module's entry in docs/architecture.md. Bound by
    /// <c>WorldEffectsFactory</c> through <see cref="BindCrashRig"/> for every flown plane; null
    /// only when the crash program/scene were unavailable, and the plane then just hides.</summary>
    public AnimRuntime? CrashRuntime { get; private set; }

    /// <summary>The node the crash definition anchors to (its <c>player</c> anim-root) — passed to
    /// <see cref="AnimRuntime.Play"/> on a crash. Bound with <see cref="CrashRuntime"/>.</summary>
    public Node3D? CrashAnchor { get; private set; }

    /// <summary>The crash-def vector the struck surface id indexes — the original's own selection
    /// mechanism (see <see cref="SurfaceDefTable"/>). Built from the bound crash program, so it
    /// knows which slots name a def this install actually ships. The selection is
    /// <see cref="AircraftLifecycle"/>'s, which holds the table; null when no crash rig was
    /// built, and the plane then just hides on a crash.</summary>
    public SurfaceDefTable? CrashDefs => _lifecycle.CrashDefs;

    /// <summary>The def the last <see cref="Crash"/> selected off <see cref="CrashDefs"/> —
    /// <c>player_crash_*</c> on a human rig, <c>ai_crash_*</c> on an AI plane, null before any
    /// crash or when no crash rig was built. The prefix names the family, so a suite (or a log
    /// reader — the CRASH line prints the same value as <c>def=</c>) can pin which family
    /// fired.</summary>
    public string? LastCrashDef => _lifecycle.LastCrashDef;

    /// <summary>Seconds a crash sits on the crash cam before this plane auto-respawns, or null —
    /// the default — for manual R only. The session arms it (Versus: 3 s, every rig) so a downed
    /// player rejoins the fight without touching a key; R still respawns early, and the timer is
    /// armed at <see cref="Crash"/>. Scripted HoldSegments runs auto-respawn regardless, on
    /// <see cref="AircraftLifecycle.AutoRespawnDelay"/> unless this says otherwise.</summary>
    public float? AutoRespawnAfter
    {
        get => _lifecycle.AutoRespawnAfter;
        set => _lifecycle.AutoRespawnAfter = value;
    }

    /// <summary>Whether this plane is crashed — frozen at the impact, airframe hidden, waiting
    /// for respawn. The fact the session (and the in-engine suites) read; only Respawn clears it.</summary>
    public bool Crashed => _lifecycle.Crashed;

    /// <summary>Whether this aircraft's hull is spent and its destroy def is playing — true from
    /// the kill, through the fall, and on past the ground impact until respawn. <see cref="Crashed"/>
    /// covers a live aircraft flown into terrain as well; this is the shot-down half alone.</summary>
    public bool Destroyed => _lifecycle.Destroyed;

    /// <summary>Whether the wreck is still falling under the flight model. True from the kill until
    /// the destroy def's <c>Callback 15</c> hands the hull to the anim, or until it lands and runs
    /// its ground-impact def, whichever the airframe authors.</summary>
    public bool WreckFalling => _lifecycle.WreckFalling;

    /// <summary>An aircraft that has been BUILT but held completely out of the session — not
    /// stepped, drawn, collidable, hittable, or a targeting candidate. The original's wave
    /// sequencer builds waves 2-4 this way (docs/formats/instant-action.md "The ace and the
    /// waves"); <see cref="Activate"/> is CSVM's inverse.
    /// ⚠ Do not park an inert aircraft far away instead of flagging it. It would still tick,
    /// collide and cost a frame; every consumer reads <see cref="InPlay"/> instead.</summary>
    public bool Inert
    {
        get => _lifecycle.Inert;
        set
        {
            if (!_lifecycle.SetInert(value))
                return;
            ApplyPresence();
            InertChanged?.Invoke(this);
        }
    }

    /// <summary>Whether this aircraft is present in the session as a real object: neither crashed
    /// nor <see cref="Inert"/>. The single "is it there" test every roster reads — the aim assist's
    /// vehicle/turret candidate lists, the projectile pool's proximity fuse and blast pass, the D12
    /// ranking's standing-target check, the HUD hostile tracker and the E11 wave-clear walk. A
    /// consumer that tests <see cref="Crashed"/> alone silently sees inert aircraft.</summary>
    public bool InPlay => _lifecycle.InPlay;

    /// <summary>This pane is in photo mode: the session owns its camera and its HUD is hidden,
    /// and this node's pause key is silent so Escape means "leave photo mode" and nothing else.
    /// Driven by <c>GameSession</c> through <see cref="BeginPhotoMode"/>/<see cref="EndPhotoMode"/>;
    /// this node decides nothing about the mode itself.</summary>
    public bool InPhotoMode { get; private set; }

    /// <summary>The flight model's world position — the plane as a SIM value, not a node transform
    /// (the node lags it by the render interpolation). What another plane's aim assist aims at.</summary>
    public Vector3 WorldPosition => _model.Position;

    /// <summary>The flight model's world velocity, m/s — the assist's intercept solve needs it, and
    /// so does the shooter's own subtraction to relative velocity.</summary>
    public Vector3 WorldVelocity => _model.VelocityDir * _model.Speed;

    /// <summary>The nose axis off the SIM attitude (the render half may hold an interpolated
    /// frame) — what the AI gunner's quick-draw cones project fore and aft from.</summary>
    public Vector3 NoseDirection => -_model.Attitude.Z;

    /// <summary>The whole SIM attitude, body→world, nose −Z and up +Y. Roll is part of the answer:
    /// the landings trigger measures the aircraft against an approach node's own orientation, and
    /// a rolled aircraft is off that approach by the roll as much as by the heading.</summary>
    public Basis Attitude => _model.Attitude;

    /// <summary>The weapon lab's free camera: while set, this controller writes NOTHING to the
    /// camera — no chase, no fixed view, no orbit, and <see cref="SnapCamera"/> is a no-op — because
    /// the lab has handed the same <see cref="Camera3D"/> to a <see cref="SpectatorCamera"/> so the
    /// tester can fly out and watch an impact from a metre away. Clearing it re-seeds the orbit from
    /// wherever the free camera left the eye, so the hand-back does not jump.</summary>
    public bool CameraOwned
    {
        get => _cameraOwned;
        set
        {
            if (_cameraOwned && !value)
            {
                _reseedOrbit = true;
            }
            _cameraOwned = value;
        }
    }

    /// <summary>This pane's pilot HUD, for the one assembler that builds its readouts and the one
    /// debug mode that hides its instruments. Internal, not public: nothing outside this assembly
    /// draws on a live aircraft, and the per-frame feed is this node's alone.</summary>
    internal FlightHud PilotHud => _pilotHud;

    // The sweep/ray seam: set in Bind, and lazy here too so a bare test rig that never binds
    // still gets one (GetWorld3D() only needs tree membership, which Bind does not gate).
    private IWorldQuery World => _worldQuery ??= new GodotWorldQuery(this);

    // The decoded contact rules, over the same seam: what a contact costs, whether this plane
    // survives it, and how far out of the surface it has to be pushed.
    private AircraftContactResolver Contacts => _contacts ??= new AircraftContactResolver(World);

    // Which stick flies this aircraft: set in Bind, and lazy here too so a bare test rig that
    // never binds still gets one, off whichever of _holdSegments/Pilot it already set (Decision 8,
    // docs/plans/PLAN-flightcontroller-deepening.md); no suite mutates either after stepping starts.
#pragma warning disable SA1202 // kept beside World, its seam counterpart, ahead of the public method below
    private IFlightInputSource InputSource => _inputSource ??= ResolveInputSource();

    /// <summary>The raw lever/surface command last written into <see cref="_lastInput"/>, the value
    /// <see cref="StepWreckFall"/> replays unchanged for a dead hull: internal so a suite can
    /// assert it is bit-identical across the death handover rather than inferring the freeze from
    /// the wreck's retained speed alone.</summary>
    internal FlightInput LastCommand => _lastInput;

    private IFlightInputSource ResolveInputSource() =>
        _holdSegments != null ? new ScriptedInputSource(_holdSegments)
        : Pilot != null ? new PilotInputSource(this)
        : new KeyboardInputSource(this);

    /// <summary>The direction an ordnance round leaves along, which the player and the AI decide
    /// differently in the original (docs/org/ordnanceTypes.md, "Who aims ordnance, and who does
    /// not"): a human's comes from the aircraft's own basis axis, negated, or taken as-is for a
    /// <c>REAR</c> weapon, with the mount giving the spawn position alone; an AI's is the mount's
    /// clamped aim in world space. Null keeps the mount's own axis, which is what an AI with no
    /// rocketeer to clamp an aim has. No ordnance round of either shooter is aim-assisted.</summary>
    public static Vector3? OrdnanceLaunchDir(bool humanPiloted, Basis planeBasis, bool rear,
        Vector3? mountAimWorld) =>
        humanPiloted ? (rear ? planeBasis.Z : -planeBasis.Z) : mountAimWorld;
#pragma warning restore SA1202

    /// <summary>Wires the flight model and (for a piloted view) the chase camera, then spawns.
    /// <paramref name="camera"/> is null on an AI rig: no camera write below runs.
    /// <paramref name="cockpitCameraOffset"/> is this rig's authored <c>cockpit_camera</c> marker
    /// (<see cref="Mech3.PlaneBuilder.CockpitCameraOffset"/>), origin for callers with no first
    /// person view. <paramref name="spawnThrottle"/>/<paramref name="spawnSpeed"/> are the
    /// mission's PLAYER_INIT values, defaulted for callers with none, and persist across respawns.</summary>
    public void Setup(FlightModel model, Camera3D? camera, CamParams camParams,
        Vector3 spawnPos, Vector3 spawnLookAt,
        float spawnThrottle = FallbackSpawnThrottle, float spawnSpeed = FallbackSpawnSpeed,
        Vector3 cockpitCameraOffset = default)
    {
        _model = model;
        _viewCamera = camera;
        _cam = camera != null
            ? new CameraController(camera, camParams, KeyDown, PinnedView, PinnedViewMode, cockpitCameraOffset)
            : null;
        // C22: the idle branch of the shared head-look law — set once here, since Head lives for
        // the controller's whole life and _model (captured by the closure) is reassigned by every
        // respawn, not replaced.
        if (_cam != null)
        {
            _cam.Head.IdleAim = AutoheadTarget;
        }
        _spawnPos = spawnPos;
        _spawnAttitude = Basis.LookingAt((spawnLookAt - spawnPos).Normalized(), Vector3.Up);
        _spawnThrottle = spawnThrottle;
        _spawnSpeed = spawnSpeed;
        _warningShots = new WarningShotCue(model.Stats.WarningShotMax,
            model.Stats.WarningShotDissipation, model.Stats.WarningShotInterval);
        Respawn();
    }

    /// <summary>Registers this aircraft with the shared pool as a near-miss cue target:
    /// any round not fired by this pilot that passes inside <see cref="WarningShotCue.PassRadius"/>
    /// sounds <c>bullet_warning_sg</c> here, rate-limited by the shipped accumulator. Call after
    /// <see cref="PlayerIndex"/> is set — the index IS the self-exclusion identity.</summary>
    public void AttachWarningShotCue(ProjectilePool pool) =>
        pool.NearMissTargets.Add(new ProjectilePool.NearMissTarget
        {
            ShooterId = PlayerIndex,
            Position = () => _model.Position,
            OnPass = OnNearMiss,
        });

    public override void _Ready()
    {
        // No HUD on an AI rig: a CanvasLayer draws over the whole window wherever its Node3D
        // parent sits, so an AI plane building one would paint its telemetry over the player's view.
        if (IsHumanPiloted)
        {
            // Explicit rather than Godot's implicit default of 1: the sun wash draws just above this
            // (UI.HudLayers.SunWash), so the HUD's own layer is load-bearing, not incidental.
            var canvas = new CanvasLayer { Layer = UI.HudLayers.Hud };
            _hudCanvas = canvas;
            canvas.Name = "hud";
            // The two board-adjacent readouts this node still owns take their z-order slots inside
            // the pilot HUD's own order, so they are handed to it rather than added around it.
            _pilotHud.Attach(canvas, VersusHud, Scoreboard);
            // Splitscreen parents the HUD into this player's SubViewport so it draws in that pane
            // only (and scales off the pane's height); single player keeps it on this node.
            (HudParent ?? this).AddChild(canvas);
        }
        if (DebugCollision)
        {
            _probe = new ImmediateMesh();
            AddChild(new MeshInstance3D
            {
                Mesh = _probe,
                TopLevel = true, // vertices are in world space
                Name = "collision_probe",
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                    NoDepthTest = true, // stays visible against/through terrain
                },
            });
        }
        // The airframe's physics body, riding this node's transform; own queries pass
        // Body.ExcludeSelf so it never collides with itself.
        if (Collider != null)
        {
            Body = new AircraftBody(this, Collider);
            AddChild(Body);
            // ⚠ Register even an INERT plane: ApplyPresence keeps it off the aircraft layer, not
            // exclusion from this roster (the pool's fuse/blast passes walk it and read InPlay).
            Projectiles?.RegisterAircraft(Body);
        }
        // Setup's Respawn ran before this node was in the tree, so Body did not exist to switch
        // off then: re-assert an inert airframe's presence now that it does.
        ApplyPresence();
        SnapCamera();

        // One firing-state slot per firable gun group (turrets excluded — built inert).
        if (Loadout != null)
        {
            var firable = new List<GunGroup>();
            foreach (var g in Loadout.FirableGuns)
            {
                firable.Add(g);
            }
            _firableGuns = firable.ToArray();
            int n = _firableGuns.Length;
            // --ammo=N wins over weapons.gunAmmoCap so the knob survives --det, which drops config.json.
            int gunCap = AmmoCapOverride ?? Config.GetInt("weapons.gunAmmoCap", GunAmmoCapDefault);
            if (gunCap > 0)
            {
                foreach (var g in _firableGuns)
                {
                    g.Capacity = Mathf.Min(g.Capacity, gunCap);
                    g.Ammo = g.Capacity;
                }
            }
            // weapons.ordnanceCap / --ammo=N: the pylon equivalent of the gun cap above, same pattern.
            int ordnanceCap = AmmoCapOverride ?? Config.GetInt("weapons.ordnanceCap", OrdnanceCapDefault);
            if (ordnanceCap > 0)
            {
                foreach (var h in Loadout.Hardpoints)
                {
                    h.Capacity = Mathf.Min(h.Capacity, ordnanceCap);
                    h.Ammo = h.Capacity;
                }
            }
            // The state machine sees the loadout through its node-free slot views; --gun-select
            // (0-based group index, clamped) seeds its cursor.
            _fire = new FireControl(_firableGuns, Loadout.Hardpoints,
                AutoFireRockets, InfiniteAmmo, InitialGunSelect);
            _gunLoggedFirst = new bool[n];

            // One aim-assist slot per muzzle, seeded to "no assist" (both directions local
            // forward) — the original's slots start unused and the forget pass alone would reach
            // the same state, this just skips the wait.
            double aimNow = GameClock.Current?.Time ?? 0.0;
            _aimSlots = new GunAimSlot[n][];
            for (int gi = 0; gi < n; gi++)
            {
                var slots = new GunAimSlot[_firableGuns[gi].MuzzleCount];
                for (int mi = 0; mi < slots.Length; mi++)
                {
                    slots[mi] = new GunAimSlot
                    {
                        Active = true,
                        Smoothed = AimAssist.LocalForward,
                        Target = AimAssist.LocalForward,
                        LastUpdate = aimNow,
                    };
                }
                _aimSlots[gi] = slots;
            }

            _pilotHud.BindWeaponGauges(n, Loadout.Hardpoints.Count);
        }
    }

    /// <summary>Rerun this plane's own run: fresh clock and every zone incomplete, then the
    /// respawn below. A plane with no stunt run is simply respawned, which is all a free flight's
    /// rerun amounts to.</summary>
    public void Rerun()
    {
        Stunt?.Reset();
        Respawn();
    }

    /// <summary>Back to the spawn pose at half throttle with a healthy, repaired airframe: the
    /// crash respawn (R), and the session's per-plane reset for a race rematch. Leaves the
    /// stunt run alone — a mid-run crash deliberately keeps its zones and clock.</summary>
    public void Respawn()
    {
        _lifecycle.Respawn();
        (_inputSource as ScriptedInputSource)?.Reset(); // scripted hold sequences restart from the spawn
        _lastInput = default;
        Pilot?.ClearStun();  // a fresh airframe never wakes up with its pilot's hands still off
        _model.ClearChoke(); // nor with the last airframe's engine still choked
        WingLights?.Reset(); // flares off; the cycle restarts from this spawn
        Surfaces?.Reset();   // control surfaces back to neutral
        Damage?.Reset();     // every part back to full HP
        _pilotHud.Reset();   // damage-dial blink timers cleared, no impact line pending
        Visuals?.Reset();    // torn panels off, healthy twins back, smoke trail cleared
        RefillWeapons();     // full ammo, dry warnings re-armed, any live tracers cleared
        if (CrashRuntime != null)
        {
            // Hard-stop the played def, re-hide the wreck (its RESET_STATE), and re-home the flung
            // pieces below (no reset event re-poses them; without this respawn leaves just the prop).
            CrashRuntime.ResetToBaseState();
            if (_crashRestPoses != null)
                foreach (var (node, rest) in _crashRestPoses)
                {
                    node.Transform = rest;
                }
            if (_crashPlaneVisibility != null)
                foreach (var (node, vis) in _crashPlaneVisibility)
                {
                    node.Visible = vis;
                }
        }
        _sweep.Reset();
        _grazeReactionCooldown = 0f;
        if (_hudCanvas != null)
            _hudCanvas.Visible = true;  // the crash camera hid it (footage); flying again
        // ⚠ An INERT airframe must stay off-screen and off the aircraft layer through a respawn too.
        // Setup() calls Respawn before _Ready builds the body, so this is also that first assertion.
        ApplyPresence();
        _throttle = _spawnThrottle;
        _keyPitch = _keyRoll = _keyYaw = 0f;  // a fresh airframe spawns with the stick centred
        // First setup precedes adapter construction, so the adapter replays startprops after attachment.
        CrashRuntime?.Play("startprops", PlaneModel, applyReset: false);
        // A fresh engine has no in-flight plume, and the spawn throttle jump (0 → the spawn
        // throttle) must never itself read as a slam.
        ThrottleSmoke?.Reset(_throttle);
        Nitro.Reset();
        _aiNitroArmed = false;
        _nitroDecayLeftS = 0f;
        SpeedCue?.Reset();
        _model.Reset(_spawnPos, _spawnAttitude, _spawnSpeed, _throttle);
        _simPrev = _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;
        if (_cam != null && IsInsideTree())
            SnapCamera();
    }

    /// <summary>Opens this spawn's collision-free window, and with
    /// <paramref name="carrierDrop"/> the longer ground-blow damping a dropped aircraft needs so it
    /// does not fight the fall it was launched into. The rules are
    /// <see cref="AircraftLifecycle.ArmSpawnTimers"/>'s.</summary>
    public void ArmSpawnTimers(bool carrierDrop = false) => _lifecycle.ArmSpawnTimers(carrierDrop);

    /// <summary>Binds this plane's crash rig: the runtime the crash and destroy defs play on, the
    /// def vector the struck surface indexes, the anchor they play against, and the two snapshots a
    /// respawn restores. One call rather than six assignments, so a rig cannot be half-bound.</summary>
    public void BindCrashRig(AnimRuntime runtime, SurfaceDefTable? defs, Node3D? anchor,
        IReadOnlyList<(Node3D Node, Transform3D RestPose)>? restPoses,
        IReadOnlyList<(Node3D Node, bool Visible)>? planeVisibility)
    {
        CrashRuntime = runtime;
        CrashAnchor = anchor;
        _lifecycle.CrashDefs = defs;
        _crashRestPoses = restPoses;
        _crashPlaneVisibility = planeVisibility;
    }

    /// <summary>The inverse of building inert: re-home this aircraft at <paramref name="pos"/>
    /// with its nose on <paramref name="lookAt"/>, put it back in play and respawn it there — the
    /// original's teleport-then-reactivate, in one call. <see cref="Respawn"/> does the rest of the
    /// work it always does (spawn speed and throttle, a healthy repaired airframe, full ammo, the
    /// start choreography), so a wave arrives flying rather than parked. Calling this on an
    /// aircraft already in play is simply that teleport-and-reset.</summary>
    public void Activate(Vector3 pos, Vector3 lookAt, Vector3? launchVelocity = null,
        bool carrierDrop = false)
    {
        var dir = lookAt - pos;
        _spawnPos = pos;
        // A zero-length aim keeps the attitude it has, the same guard PlaceHeld makes.
        if (dir.LengthSquared() > 1e-6f)
            _spawnAttitude = Basis.LookingAt(dir.Normalized(), Vector3.Up);
        Inert = false;
        ArmSpawnTimers(carrierDrop);
        // The original snaps an activated vehicle to its net's nearest node (FUN_004b0f40 →
        // FUN_00432010), which is what makes a teleported wave patrol where it ARRIVED rather
        // than fly back to wherever it was parked.
        Pilot?.Patrol?.Reseat();
        Respawn();
        if (launchVelocity is { } velocity)
            _model.SetVelocity(velocity);
        if (carrierDrop)
        {
            _throttle = CarrierDropThrottle;
            _model.Throttle = CarrierDropThrottle;
            if (Pilot != null)
                Pilot.Throttle = CarrierDropThrottle;
            ThrottleSmoke?.Reset(_throttle);
        }
    }

    /// <summary>Weapon lab: pin the held airframe at <paramref name="pos"/> with its nose on
    /// <paramref name="lookAt"/>, at zero speed — click-to-place and <c>--weapon-target=</c>. Goes
    /// through the same <see cref="FlightModel.Reset"/> + <see cref="SnapCamera"/> pair
    /// <see cref="Respawn"/> uses. Sets the pin whether or not <see cref="Held"/> is on.</summary>
    public void PlaceHeld(Vector3 pos, Vector3 lookAt)
    {
        var dir = lookAt - pos;
        // A zero-length aim (clicked the plane's own position) would make LookingAt throw — keep
        // the attitude the plane already has rather than fail the placement.
        _heldAttitude = dir.LengthSquared() > 1e-6f
            ? Basis.LookingAt(dir.Normalized(), Vector3.Up)
            : _model.Attitude;
        _heldPos = pos;
        _heldPinned = true;
        _model.Reset(_heldPos, _heldAttitude, 0f, 0f);
        _throttle = 0f;
        _simPrev = _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;
        if (_cam != null && IsInsideTree())
            SnapCamera();
    }

    /// <summary>Weapon lab: point the gun selector at a firable gun group (0-based, clamped) —
    /// the programmatic twin of G / D-pad Left, which only cycles. Interactively that cycle still
    /// wins the next time it is pressed; <see cref="InitialGunSelect"/> is the _Ready-time
    /// equivalent and cannot be re-applied once the rig is built.</summary>
    public void SelectGunGroup(int index) => _fire?.SelectGunGroup(index);

    /// <summary>Weapon lab: point the hardpoint selector at a pylon (0-based, clamped) — the
    /// programmatic twin of H. Unlike H this lands on an EMPTY pylon too (the lab picks a mount to
    /// look at, not a mount to fire); the firing path's own armed scan still advances off it when
    /// the trigger is pulled.</summary>
    public void SelectPylon(int index) => _fire?.SelectPylon(index);

    /// <summary>Enter photo mode: the pause key goes silent for the duration.</summary>
    public void BeginPhotoMode() => InPhotoMode = true;

    /// <summary>Leave photo mode, seeding the pause key's edge flag from the CURRENT device state.
    /// ⚠ Clearing the flag alone is a bug, and was one: <see cref="PauseTogglePressed"/> answered
    /// false for the whole mode, so <c>_pausePrev</c> is false, and the Escape still under the
    /// player's finger the instant the gate lifts then reads as a fresh press and unpauses the
    /// session that photo mode was opened from. Same hazard as priming a menu reader, one edge
    /// flag further down.</summary>
    public void EndPhotoMode()
    {
        InPhotoMode = false;
        // The raw combination, not PauseTogglePressed: that still reads the gates, and the point
        // is to record what the hands are doing regardless of them.
        _pausePrev = KeyDown(Key.P) || KeyDown(Key.Escape) || PadPressed(JoyButton.Start);
    }

    /// <summary>Photo mode's forward onto <see cref="FlightHud.SetVisible"/>: the session drives it
    /// per rig and holds no HUD of its own.</summary>
    public void SetPilotHudVisible(bool visible) => _pilotHud.SetVisible(visible);

    /// <summary>The decoded bracket gate for the targeting marker (<c>FUN_004574d0</c>): whether the
    /// SELECTED gun group could reach an intercept inside the weapon's authored <c>RANGE</c>. That,
    /// not a HUD distance constant, is the original's threshold, so the marker is weapon-dependent.
    /// With no gun group resolvable the original never rejects, and neither does this.
    /// <paramref name="margin"/> is the hysteresis: zero to turn the brackets on,
    /// <see cref="TargetHud.BracketHysteresis"/> to keep them on.</summary>
    public bool GunReachesTarget(Vector3 targetPos, Vector3 targetVel, float margin = 0f)
    {
        if (SelectedGun() is not { } sel)
        {
            return true;
        }

        float speed = sel.Weapon.Velocity ?? ProjectilePool.DefaultVelocity;
        float range = sel.Weapon.Range ?? 0f;
        return TargetHud.GunReaches(MuzzleMidpoint(sel), _model.VelocityDir * _model.Speed, speed,
            range + margin, targetPos, targetVel);
    }

    /// <summary>--crash[=frame]: forces this player's crash outside any live collision. No struck
    /// body by default, so the selection cascade takes its null-material arm and resolves slot 0
    /// (<c>player_crash_default</c>) however the plane is posed. <paramref name="killer"/> lets
    /// <c>--debug-scoreboard --vs</c> show a real, attributed kill. <paramref name="struckBody"/>
    /// lets a suite pass a real <see cref="SceneBuilder.SurfaceIdMeta"/> stamp instead.</summary>
    public void DebugForceCrash(int? killer = null, Node? struckBody = null)
    {
        // An INERT wave-parked plane is not in the fight; --crash sweeps every spawned AI plane.
        if (InPlay)
            Crash(_model.Position, "debug-crash", "test", struckBody, killer);
    }

    /// <summary>One projectile hit on this plane: maps it to the data part and spends the weapon's
    /// ARMOR_DAMAGE/HEALTH_DAMAGE through <see cref="PlaneDamage.Apply"/> — the decoded flow, see
    /// docs/org/vehicleDamage.md. Exhausted whole-vehicle health downs the plane through
    /// <see cref="Crash"/>, as a fatal contact does. No cooldown: every round counts.
    /// <paramref name="shooter"/> carries into <see cref="Downed"/> as the killer.
    /// <paramref name="damageScale"/>: 1 for a direct round, the blast falloff share otherwise.</summary>
    public void TakeProjectileHit(WeaponDef weapon, Vector3 impact, string colliderPart, int shooter,
        float damageScale = 1f)
    {
        if (!InPlay)
            return;
        // The attacker queue (FUN_004b9770): whoever just shot this pilot goes to the END of the
        // queue `Next Enemy/Objective` walks backwards. The gate is a shooter on a DIFFERENT,
        // non-zero team, so a friendly-fire round records nothing.
        if (Targeting != null && Projectiles?.RigOfShooter(shooter) is { } attacker
            && attacker.Team != Team && attacker.Team != AimAssist.NeutralTeam
            && Team != AimAssist.NeutralTeam)
        {
            Targeting.RecordAttacker(attacker);
        }
        // Runs even with no damage data, so a plane nothing tracks HP for still visibly takes fire.
        if (weapon.Caliber is { } shakeCal)
            Shake?.BulletHit(shakeCal);
        else if (damageScale < 1f)
            Shake?.ExplosionAt((weapon.ArmorDamage ?? 0f) * damageScale);
        else
            Shake?.MissileHit(weapon.ArmorDamage ?? 0f, weapon.HighExplosive);
        if (Damage == null)
            return;
        // The sim pose, not GlobalTransform: the render half may hold an interpolated frame.
        var pose = new Transform3D(_model.Attitude, _model.Position);
        var localImpact = pose.AffineInverse() * impact;
        string dataPart = PlaneDamage.MapStruckPart(colliderPart, localImpact);
        // Apply's answer is the struck zone, not the geometric guess: it may redirect a dead-zone
        // hit to a survivor (docs/org/vehicleDamage.md).
        var state = Damage.Apply(dataPart,
            (weapon.HealthDamage ?? 0f) * damageScale, (weapon.ArmorDamage ?? 0f) * damageScale);
        string struckPart = state?.Def.Name ?? dataPart;
        if (state != null)
        {
            Visuals?.OnPartDamage(struckPart, state.HealthFraction);
            _pilotHud.OnPartDamage(struckPart); // damage dial: hit zone blinks 5 s
        }

        // the def-level stages run off the hull pool even when the round went zone-less
        Visuals?.OnHullDamage(Damage.SummaryHealthFraction);

        if (_projectileHitsLogged < 6)
        {
            _projectileHitsLogged++;
            string zone = state != null
                ? Log.Format($"armor={state.Armor:0.0}/{state.Def.MaxArmor:0} hp={state.Hp:0.0}/{state.Def.MaxHp:0} ")
                : string.Empty;
            Log.Info("weapons", $"shot hit P{PlayerIndex + 1} ({colliderPart}→{struckPart}): {weapon.Id} {zone}hull={Damage.WholeHealth:0.0}/{Damage.WholeHealthMax:0}");
        }

        // The decoded kill rule: whole-vehicle health current at or below zero (never a part
        // flag) — reachable through the zone-less overflow, so it is tested on every hit.
        if (Damage.IsDestroyed)
        {
            Log.Info("weapons", $"vehicle health exhausted ({struckPart} last) — shot down by {weapon.Id}");
            Destroy(impact, $"gunfire ({weapon.Id})", colliderPart,
                killer: shooter != ProjectilePool.NoShooter ? shooter : null);
            return;
        }
        DamageApplied?.Invoke(this);
        // The round carries no shooter position, so the impact offset stands in as the threat
        // bearing for the AI's evade turn-away.
        if (!IsHumanPiloted && Pilot?.Machine is { } machine)
        {
            machine.NotifyDamage(
                ((weapon.HealthDamage ?? 0f) + (weapon.ArmorDamage ?? 0f)) * damageScale,
                impact - _model.Position);
        }
        _pilotHud.Flash(state != null
            ? $"⚠ HIT {struckPart.ToUpperInvariant()} {state.Fraction * 100f:0}%"
            : $"⚠ HIT HULL {Damage.SummaryHealthFraction * 100f:0}%");
    }

    /// <summary>The AI stun's entry for a struck aircraft (decoded: <c>FUN_004200d0</c>; a
    /// <c>SONIC</c>/<c>FLASH</c> burst passes <see cref="DisablingIntensity"/>'s stun seconds, the
    /// smoke screen <c>smokescreen_stun_interval</c>). The original's victim guards live here: never
    /// a human (their half is the screen wash, no control is touched), never a dead or inert
    /// airframe, never an aircraft with no AI pilot; the <c>+0xf8</c> byte is not modelled on our
    /// side. Returns whether the pilot was stunned. Re-entrant: see <see cref="AiPilot.Stun"/>.</summary>
    public bool TryStunPilot(float seconds)
    {
        if (IsHumanPiloted || !InPlay || Pilot is not { } pilot || seconds <= 0f)
            return false;
        pilot.Stun(seconds);
        return true;
    }

    /// <summary>The choker's entry for a struck aircraft (decoded: <c>FUN_004b1690</c> under
    /// <c>FUN_004b9bc0</c>'s <c>TANGLER</c> branch; the seconds come from
    /// <see cref="TanglerChoke.Duration"/>). Unlike the stun this hits a human exactly as it hits an
    /// AI, because the original's choker branch has no player guard: the engine is a mechanical
    /// system and nobody is told about it, least of all the AI. Returns whether the engine is now
    /// dead. Re-entrant, and extend-only: see <see cref="FlightModel.ChokeEngine"/>.</summary>
    public bool TryChokeEngine(float seconds)
    {
        if (!InPlay || seconds <= 0f)
            return false;
        bool wasDead = _model.EngineDead;
        _model.ChokeEngine(seconds);
        if (!wasDead && _model.EngineDead)
            Log.Info("weapons", $"engine choked on P{PlayerIndex + 1} for {seconds:0.0} s");
        return _model.EngineDead;
    }

    /// <summary>The receiving half of a plane-versus-plane ram: the striker's decoded
    /// pair spent through this plane's own ledger, the flow a projectile hit uses.
    /// ⚠ Do not suppress the struck plane hitting back. Each aircraft sweeps itself, so both
    /// resolve the contact; that is the original's behaviour, not a double-count.</summary>
    public void TakeCollisionHit(float armorDamage, float healthDamage, Vector3 impact, int striker)
    {
        if (!InPlay || Damage == null)
            return;
        var pose = new Transform3D(_model.Attitude, _model.Position);
        string dataPart = PlaneDamage.MapStruckPart("center", pose.AffineInverse() * impact);
        var state = Damage.Apply(dataPart, healthDamage, armorDamage);
        string struckPart = state?.Def.Name ?? dataPart;
        if (state != null)
        {
            Visuals?.OnPartDamage(struckPart, state.HealthFraction);
            _pilotHud.OnPartDamage(struckPart);
        }

        Visuals?.OnHullDamage(Damage.SummaryHealthFraction);
        Log.Info("flight",
            $"rammed P{PlayerIndex + 1} by P{striker + 1} ({struckPart}): a={armorDamage:0.0} h={healthDamage:0.0} hull={Damage.WholeHealth:0.0}/{Damage.WholeHealthMax:0}");
        if (Damage.IsDestroyed)
        {
            Log.Info("flight", $"vehicle health exhausted ({struckPart} last) — rammed by P{striker + 1}");
            Destroy(impact, "collision", "center", null);
        }
    }

    /// <summary>Arms this plane's own collision-free window, the other half of a ram's grace pair:
    /// the caller arms its own side and calls this on the struck rig so neither re-resolves the
    /// overlap they are still in. Kept off <see cref="TakeCollisionHit"/> itself because that
    /// method's test callers exercise the receiving half alone, with no ram and no grace to arm.</summary>
    public void ArmCollisionGrace() => _lifecycle.ArmCollisionGrace();

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One flight step: input, the flight model, collision, weapons and the stunt clock.
    /// Public because a non-realtime clock has the session call this instead of Godot's physics
    /// tick — the halt (P / gamepad Start) simply stops the calls.</summary>
    public void SimStep(float dt)
    {
        // An inert airframe takes no step at all — no stunt clock, no input, no flight
        // model, no collision sweep, no weapons. Guarded here rather than in the session's loop so
        // every caller (the loop, this node's own _PhysicsProcess, a suite) honours it in one place.
        if (Inert)
            return;

        // Advance the stunt clock every physics frame — including through the crash freeze so the
        // clock never stops (a deliberate rule); it stops only at AllComplete (inside Tick). A
        // halted GameClock stops the calls entirely, so the timer freezes with the rest of the sim.
        Stunt?.Tick(dt);

        // The near-miss accumulator drains on the sim clock like everything else here; the pool
        // registers passes into it earlier in the same step (GameSession.DriveSimSteps order).
        _warningShots?.Tick(dt);

        // Race players finish STAGGERED by index, exercising the real one-finishes-while-others-fly
        // path instead of four identical totals landing on frame one.
        if (DebugCompleteStunt && Stunt is { AllComplete: false }
            && (Race == null || Stunt.Elapsed >= PlayerIndex * DebugFinishStagger))
            Stunt.DebugCompleteAll(Race != null ? PlayerIndex * 2f : 0f);

        // ⚠ The rematch shortcuts are NOT read here; a results board halts the clock, so this step
        // never runs while one is up. PollResultsShortcuts reads them off the rendered frame.
        if (Match is { Completed: true })
            return;

        if (Race is { AllFinished: true } && Stunt is { AllComplete: true })
            return;

        if (Stunt is { AllComplete: true } && Race == null)
        {
            _simPrev = _simCurr;   // hold the finish pose — no stale pair left to interpolate
            return;
        }
        _autoRestartIn = AircraftLifecycle.AutoRespawnDelay; // re-armed while the run is live

        if (Crashed)
        {
            // ⚠ Out of lives, neither R nor AutoRespawnAfter's timer may bring the pilot back;
            // check this ahead of both rather than by clearing AutoRespawnAfter, which R overrides.
            if (Spectating)
            {
                StepWreckFall(dt);
                return;
            }
            if (RespawnPressed() || _lifecycle.TickAutoRespawn(dt, _holdSegments != null))
            {
                Respawn();
                return;
            }
            // The one thing a dead aircraft still does: fall. No input, no weapons, no stunt
            // clock — only the hull, on its way to its ground-impact def.
            StepWreckFall(dt);
            return;
        }

        // Weapon lab: a HELD airframe skips input/model/collision and re-asserts its pinned pose
        // instead; weapons/gauges/telemetry below run exactly as in flight, through _model.Reset.
        if (_held)
        {
            if (!_heldPinned)
            {
                _heldPos = _model.Position;
                _heldAttitude = _model.Attitude;
                _heldPinned = true;
            }
            _model.Reset(_heldPos, _heldAttitude, 0f, 0f);
            _throttle = 0f;      // the throttle ramp is input-driven and no input is read while held
            _lastInput = default; // control surfaces sit neutral
        }
        else
        {
            var input = InputSource.Read(dt);
            // Read AFTER the input: R respawns inside it, and a sweep from the pose before that
            // respawn would run the whole way to the spawn point and strike whatever lies between.
            var entered = _model.Position;       // the position this step enters with
            // The response is absent for 1.5 s, then AI terms run at 15% for 1 s.
            // Keep the probe off too, so the log records response rather than an inert hit.
            bool groundBlowReady = IsHumanPiloted || !_lifecycle.CollisionGraceActive;
            if (groundBlowReady)
                ProbeGroundBlow(ref input);  // reads the pose this step ENTERED with, as the original does
            input.AiGroundBlowScale = !groundBlowReady ? -1f
                : _lifecycle.PostDropGroundBlowActive ? 0.15f : 1f;
            input.NearestHumanDistSqM = NearestHumanDistSqM();
            AdvanceNitro(dt);
            input.Boost = Nitro.Boosting;
            _lastInput = input;
            _grazeReactionCooldown -= dt;
            // The original sweeps on every other step and carries the skipped step's motion into
            // the next sweep, so the sweep from `prev` covers two steps of motion after a skipped
            // one. Every contact it resolves spends the pair; nothing else gates the spend.
            bool onSweepStep = _sweep.Advance(entered, out var prev);
            _lifecycle.TickTimers(dt);
            _model.Step(input, dt);

            // The airframe boxes sweep along the carried motion; the center ray stays as an
            // anti-tunnelling backstop. Only the shapeless fallback keeps a nose margin on it.
            var to = _model.Position;
            var step = to - prev;
            float len = step.Length();
            float margin = Collider == null ? CollisionMargin : 0f;
            var probeEnd = len > 1e-4f ? to + step / len * margin : to;
            // ⚠ The grace window suppresses the SWEEP, not just the damage: while it is live this
            // plane has no collision at all (obj+0xAC, docs/org/flightModel.md).
            bool sweeping = onSweepStep && !_lifecycle.CollisionGraceActive;
            ContactReport contact = default;
            Node? hitBody = null;
            bool hit = sweeping && SweepAirframe(prev, step, out contact, out hitBody);
            if (!hit && sweeping)
                hit = CenterRayContact(prev, probeEnd, step, len, out contact, out hitBody);
            // No contact means the boxes cleared the whole motion, which is where they are drawn.
            if (_probe != null)
                DrawProbe(prev, probeEnd, prev + step * (hit ? contact.StopFraction : 1f), hit);
            // The decoded contact: the resolver decides it whole (both parties' damage off one
            // severity cosine, then this plane's fate), and what comes back is performed here.
            if (hit)
            {
                var outcome = Contacts.Resolve(in contact, Striking(),
                    new ContactEffects(this, hitBody, in contact, prev, step));
                PerformContact(in outcome, in contact, hitBody);
                if (outcome.Fate == ContactFate.Crash)
                {
                    Crash(contact.Impact, contact.ColliderName, contact.Part, hitBody);
                    return;
                }
            }
        }

        _simPrev = _simCurr;
        _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;

        // The dynamic chase radius advances on the sim step, not the render frame: the
        // acceleration derivative needs the fixed dt, and the transient's relaxation is a
        // SIM-time rate. A crash or halt stops the calls, freezing the radius too.
        _cam?.UpdateDynamics(dt, _model.Speed);

        // The AI gunner: acquire/hold the target and decide this tick's trigger and lead
        // BEFORE the fire step reads them. Runs for AI pilots only; a gunner-less AI keeps the
        // released trigger it always had.
        if (!IsHumanPiloted && Pilot?.Gunner is { } aiGunner)
        {
            DriveAiGunner(aiGunner);
            // The ordnance half runs off the gunner's target, never its own acquisition, so the
            // two weapon classes cannot chase different aircraft the way the original never does.
            if (Pilot?.Rocketeer is { } aiRocketeer)
                DriveAiRocketeer(aiRocketeer, aiGunner, dt);
        }

        // Weapons: poll the raw held controls, let FireControl decide (selector edges, fire
        // clocks, ammo draw-down, cues), then perform its outcome against the pool and audio.
        if (_fire != null)
        {
            // The weapon lab flips these on the public fields at runtime (bank switch, toggles;
            // GameSession also sets InfiniteAmmo post-_Ready) — mirror them into the machine.
            _fire.AutoFireRockets = AutoFireRockets;
            _fire.InfiniteAmmo = InfiniteAmmo;
            var fireInputs = new FireInputs
            {
                // A null pool could spawn nothing — feed the triggers as released so no ammo is
                // decided away on rounds that could never fire. An AI pilot's gunner IS its
                // trigger; without one the raw controls (--fire's AutoFire included) decide.
                FireHeld = Projectiles != null
                    && (!IsHumanPiloted && Pilot?.Gunner is { } g ? g.WantsFire : FirePressed()),
                RocketHeld = Projectiles != null
                    && (!IsHumanPiloted && Pilot?.Rocketeer is { } r ? r.WantsFire : RocketFirePressed()),
                GunSelectHeld = GunSelectPressed(),
                RocketSelectHeld = RocketSelectPressed(),
            };
            // The assist's forget + catch-up pass (docs/org/aim-assist.md "Per frame"), ticked on
            // the PRE-shot state; must run before a round out this frame restamps last-update.
            if (IsHumanPiloted)
            {
                double aimNow = GameClock.Current?.Time ?? 0.0;
                for (int gi = 0; gi < _aimSlots.Length; gi++)
                {
                    AimAssist.Tick(_aimSlots[gi], aimNow, dt,
                        _model.Stats.StickyBulletForgetInterval, _model.Stats.StickyBulletCatchupRate);
                }
            }
            ApplyFireOutcome(_fire.Step(dt, fireInputs));
        }
        Ordnance?.Update(InfiniteAmmo);   // hide a pylon's mounted rocket the moment it fired its last

        // The carried turret gunners: each tracks and fires on its own, into the same
        // shared pool, under this pilot's shooter id. The crash branch above already returned,
        // so a downed host's gunners take no further ticks.
        foreach (var turret in Turrets)
        {
            turret.SimStep(dt);
        }

        // The plane wobble: overspeed drive plus this tick's fire/hit kicks, written as
        // visual-only roll to the pivot the model hangs under. Physics, aim and the camera
        // read this node's transform, which the pivot sits below — never the wobble.
        if (Shake != null)
        {
            Shake.SetSpeedRatio(_model.Speed / Mathf.Max(1f, _model.Stats.FdSpeed));
            Shake.Advance(dt);
            if (ShakePivot != null)
                ShakePivot.Rotation = new Vector3(0f, 0f, Shake.Roll);
        }

        // Stunt run: flew-through-a-danger-zone test against this frame's committed position.
        Stunt?.Update(_model.Position);

        // height over ground for the altimeter's LOW ALT warning: one ray straight
        // down per physics frame (world + map-edge extension colliders)
        _pilotHud.StepAgl(World, _model.Position, Body?.ExcludeSelf);

        // Backstop if the swept ray ever misses. A HELD plane is exempt: it is exactly where the lab
        // parked it (below the map is a legal place to hold), and a respawn would fling it away from
        // the target it was aimed at.
        if (!_held && _model.Position.Y < UnderMapY)
            Respawn();

        _sinceTelemetry += dt;
        if (_sinceTelemetry >= 1.0)
        {
            _sinceTelemetry = 0;
            var p = _model.Position;
            // path = climb/dive angle of the flight path; nose = the attitude's pitch;
            // wv = wing verticality |up·Y| (1 level/inverted, 0 knife-edge) — the nose-chase factor
            GD.Print($"flight: pos=({p.X:0},{p.Y:0},{p.Z:0}) spd={_model.Speed:0.0} m/s " +
                     $"thr={_model.Throttle:0.00} rates=({_model.PhysicalBodyRates.X:0.00},{_model.PhysicalBodyRates.Y:0.00},{_model.PhysicalBodyRates.Z:0.00}) " +
                     $"path={Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(_model.VelocityDir.Y, -1f, 1f))):0}° " +
                     $"nose={Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-_model.Attitude.Z.Y, -1f, 1f))):0}° " +
                     $"wv={Mathf.Abs(_model.Attitude.Y.Dot(Vector3.Up)):0.00}" +
                     (_pilotHud.AglMeters < float.MaxValue
                         ? $" agl={_pilotHud.AglMeters:0}" : ""));
        }
    }

    public override void _Process(double delta)
    {
        var clock = GameClock.Current;
        if (!Inert)
            PollResultsShortcuts((float)delta);
        // ⚠ Ahead of the inert return, for a rig that HAS a pause key. --debug-spectate flags
        // every human rig inert (GameSession.ApplyDebugSpectate), so swallowing the key here
        // leaves a spectated session with no way to freeze the picture at all.
        bool halted = false;
        if (AllowPause || !Inert)
            halted = PollPauseAndHalt(clock);
        // Nothing left to draw, animate, interpolate or point a camera at while inert.
        if (Inert)
            return;

        // ⚠ A halt does NOT orbit: a board's cursor keys ARE the orbit keys, so the free look
        // lives behind its Photo Mode row instead (BL-429, docs/architecture.md). The weapon lab's
        // hold keeps its own orbit, seeded on the edge so entering it never jumps.
        bool orbiting = Held;
        if ((orbiting && !_orbitPrev) || _reseedOrbit)
        {
            _reseedOrbit = false;
            _cam?.SeedOrbit(_model.Position);
        }
        _orbitPrev = orbiting;
        // Plane state — animators, audio ramps — is sim time, so it freezes and scales with it.
        float simDt = clock?.FrameDt ?? (float)delta;
        if (!orbiting)
        {
            // Draw the plane between its last two sim poses (see the _simPrev/_simCurr fields).
            // Skipped while crashed (the sim pair is stale; the wreck owns the visuals) and on a
            // parent-driven clock, which steps the sim once per rendered frame anyway.
            if ((clock == null || !clock.ParentDriven) && !Crashed)
            {
                _renderPose = _simPrev.InterpolateWith(_simCurr, (float)Engine.GetPhysicsInterpolationFraction());
                GlobalTransform = _renderPose;
            }
        }
        if (CameraOwned || _cam == null)
        {
            // The lab's free camera has the view — every camera write here would fight it —
            // and an AI rig has no camera at all.
        }
        else if (orbiting)
        {
            // The orbit camera runs on wall time on purpose: a held airframe is a stopped subject
            // with the world still running, and the point is to look around it.
            var (yawIn, pitchIn, zoomIn) = OrbitInput();
            _cam.Orbit((float)delta, _model.Position, yawIn, pitchIn, zoomIn);
        }
        else if (halted)
        {
            // A board is up. Nothing is written, so the camera holds the pose it had when the
            // board appeared and the menu sits over a still frame (BL-429).
        }
        else if (Crashed)
        {
            // The authored crash camera holds the pose Crash() cut to — the original's camera
            // does not move after the cut (footage), so nothing is written here until respawn.
        }
        else
        {
            PollViewModeKeys();
            // Default to the external FOV global; the FirstPerson arm below overrides it, so a
            // look-behind while SELECTED Cockpit/Nose gets the first-person FOV back on release.
            _cam.RestoreExternalFov();
            bool firstPersonPose = false;
            int view = _cam.ActiveView();
            if (view >= 0)
            {
                _cam.FixedView(view, _renderPose);
            }
            else if (_cam.FirstPerson)
            {
                // Rigid at cockpit_camera (wobble inherited), the mode's own FOV, aimed by the
                // head. Look-back stays IN the cockpit — head to dead astern while held, as the
                // original does — which is why this arm sits above the look-behind branch below.
                _cam.Head.Step(simDt, _cam.BackActive(PadPressed(JoyButton.RightStick))
                    ? new HeadLookInput(0f, -1f, 0f, 0f, false)
                    : HeadLookRead());
                _cam.FirstPersonView(_renderPose);
                _cam.ApplyFirstPersonFov();
                firstPersonPose = true;
                view = _cam.ViewMode == PilotViewMode.Nose
                    ? CameraController.NoseViewLog
                    : CameraController.CockpitViewLog;
            }
            // E42: this player's right-stick click looks back, the pad twin of holding
            // numpad 0 — read here, not in CameraController, same "no pad devices in the camera"
            // rule OrbitInput/PadLookInput follow.
            else if (_cam.BackActive(PadPressed(JoyButton.RightStick)))
            {
                _cam.BackView(_renderPose);
                view = CameraController.BackViewLog;
            }
            else
            {
                var (lookX, lookY) = PadLookInput();
                if (lookX != 0f || lookY != 0f)
                {
                    // E42: the right stick swings the view around the plane instead of
                    // the usual chase pose — see CameraController.PadLook.
                    _cam.PadLook(_renderPose, lookX, lookY);
                    view = CameraController.PadLookLog;
                }
                else
                {
                    // Fed simDt, not wall time, so a scripted flight capture stays frame-rate
                    // independent; fed the DRAWN pose, same rule as the rigid views above.
                    _cam.Chase(simDt, _renderPose.Origin, _renderPose.Basis);
                }
            }
            // Keyed to the pose this frame actually took, not to the selection — a look-behind
            // puts the camera outside the aircraft and must bring its body back while held.
            Cockpit?.Apply(_cam.ViewMode, firstPersonPose);
            _cam.LogView(view, _model.Position, _model.Attitude);
        }

        // heading of the nose: 0 = north (−Z), 90 = east (+X) — shared by the compass and the marker
        var nose = -_model.Attitude.Z;
        float headingDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(nose.X, -nose.Z)), 360f);
        // Stunt objective marker: cycle the displayed target (Tab / gamepad X, edge-detected) before
        // the HUD feed, so the marker and the status line show this frame's choice, not last one's.
        if (Stunt != null)
        {
            bool cycle = CycleTargetPressed();
            if (cycle && !_cyclePrev)
                Stunt.CycleTarget();
            _cyclePrev = cycle;
        }
        // Player target selection: rebuild-then-input, the original's own order — the
        // per-frame candidate pass runs first and a handler then steps the list it just built.
        if (Targeting != null && IsHumanPiloted)
            StepTargeting(simDt);
        // Dogfight opponent / AI hostile markers: this pane's own pose, so each HUD can compute
        // its own target's clock bearing off it (the same feed the pilot HUD's markers get).
        if (VersusHud != null)
        {
            VersusHud.PlanePos = _model.Position;
            VersusHud.HeadingDeg = headingDeg;
        }
        _pilotHud.Draw(BuildHudState((float)delta, simDt, halted, headingDeg));
        if (!halted && !Crashed)
        {
            float speedFrac = _model.Speed / _model.Stats.FdSpeed;
            // Zones where the airframe resolves them, the hull pair where it does not: the two
            // arms the decoded damage-state test itself has, so nothing here picks between them.
            float healthFrac = Damage?.WorstHealthFraction ?? 1f;
            // One drive for both paths: the original runs ONE per-frame routine for the player and
            // every AI vehicle, so the two must never read the airframe differently.
            var engineDrive = EngineAudioCurves.DriveFrom(_model, _model.Boosting);
            // Keyed to the SELECTED view (D31), not the per-frame pose the camera actually took —
            // the original's swap is a camera-mode gate, and a held numpad key or look-behind is a
            // pose, not a mode change (⚠ table row 2 traces the analogous head-look case).
            Audio?.Update(simDt, engineDrive, speedFrac, healthFrac, _model.EngineDead,
                ViewMode == PilotViewMode.Cockpit);
            EngineAudio?.Update(simDt, engineDrive, speedFrac, healthFrac, _model.EngineDead);
            // The throttle-slam gate needs the live value every frame, not just while its plume
            // is active, so it can tell a fresh climb from one already in progress.
            ThrottleSmoke?.Update(simDt, _model.Throttle);
            if (SpeedCue != null && _viewCamera != null)
            {
                var cameraPos = _viewCamera.GlobalPosition;
                SpeedCue.Update(simDt, _model.Position, _model.Attitude, cameraPos.Y,
                    HeightAboveWorldGround(cameraPos));
            }
        }

        // Spin the propeller/rotor blur discs: they keep turning even at idle (windmilling)
        // and speed up with throttle. Frozen while crashed or paused (a still disc reads
        // the same at any angle, and freezing it keeps screenshots deterministic).
        Props?.Advance(simDt, Crashed || halted ? 0f : PropIdleSpin + (1f - PropIdleSpin) * _model.Throttle);

        // Frozen while paused (so a screenshot catches a fixed state) and while crashed.
        if (!Crashed && !halted)
        {
            WingLights?.Advance(simDt);
            Surfaces?.Advance(simDt, _lastInput, IsHumanPiloted,
                _model.ReverseAuthorityAt(_model.Speed));
            // damage-stage trails need no per-frame feed: the rig runtime's emitters follow
            // their pdpN/prop1 host nodes themselves
        }
    }

    internal void DetachRosterBindings(ProjectilePool pool)
    {
        pool.NearMissTargets.RemoveAll(target => target.ShooterId == PlayerIndex);
        if (Body != null)
            pool.UnregisterAircraft(Body);
        if (_hudCanvas != null && GodotObject.IsInstanceValid(_hudCanvas))
        {
            _hudCanvas.GetParent()?.RemoveChild(_hudCanvas);
            _hudCanvas.QueueFree();
            _hudCanvas = null;
        }
        SpeedCue?.Dispose();
        SpeedCue = null;
        Race?.Remove(PlayerIndex);
        Race = null;
        SmokeScreens = null;
        PauseState = null;
        TargetSubParts = null;
        TargetObjectives = null;
    }

    /// <summary>Whether static world geometry blocks the segment — the turret gunners' cached
    /// line-of-sight test. World layer only: another aircraft in the way is not cover, which is
    /// also why <see cref="HitWorld"/> (world + aircraft) is not reused here.</summary>
    internal bool WorldBlocksLine(Vector3 from, Vector3 to) =>
        World.Ray(from, to, CollisionLayers.World, null, out _);

    /// <summary>What blocks the AI's avoid-crash lookahead along the segment — the static world or
    /// another aircraft, never this plane's own body — as the struck body's name, or null for a
    /// clear path. Not <see cref="WorldBlocksLine"/>; decode: docs/org/aiPilot.md, "What the ray
    /// can hit".
    /// ⚠ Do not widen this to a swept sphere. Tried and reverted 2026-08-16: it detects far more
    /// but changes no outcome, and detection was never the bottleneck (same doc).</summary>
    internal string? AvoidCrashBlocksLine(Vector3 from, Vector3 to)
    {
        if (!World.Ray(from, to, CollisionLayers.WorldAndAircraft, Body?.ExcludeSelf, out var report))
            return null;
        // The name is the diagnostic: a block on "a5/col" is terrain, one on
        // "ai6_player_bhawk/airframe" is the aircraft case the decode says this ray also covers.
        return report.Collider is { } body
            ? $"{body.GetParent()?.Name}/{body.Name}"
            : "unnamed";
    }

    // Degrees between two vectors, 180 when either is degenerate (an unmeasurable
    // aspect reads as the worst case rather than as zero).
    private static float AngleBetweenDeg(Vector3 a, Vector3 b) =>
        a.LengthSquared() < 1e-6f || b.LengthSquared() < 1e-6f
            ? 180f
            : Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(a.Normalized().Dot(b.Normalized()), -1f, 1f)));

    // The struck body's numeric surface id (SceneBuilder.SurfaceIdMeta,
    // stamped on every collider), or null when there is no struck body — the headless
    // `--crash` force, which is the original's null-material arm and so resolves slot 0.
    // The weapon IMPACT table is read at this same id (ProjectilePool.SurfaceIdOf),
    // which answers `0` where this answers null, because that path has no null to carry.
    private static int? SurfaceIdOf(Node? hitBody) =>
        hitBody != null && hitBody.HasMeta(SceneBuilder.SurfaceIdMeta)
            ? hitBody.GetMeta(SceneBuilder.SurfaceIdMeta).AsInt32()
            : null;

    // Deadzone + squared response for fine control around center.
    private static float StickCurve(float v)
    {
        const float deadzone = 0.15f;
        float a = Mathf.Abs(v);
        if (a < deadzone)
            return 0f;
        float t = Mathf.Min(1f, (a - deadzone) / (1f - deadzone));
        return Mathf.Sign(v) * t * t;
    }

    // N / gamepad X — the nitro command (the original's MSG_CMD_NITROUS "Use Nitro-Booster").
    // A level read: the command arm engages on pressed-or-held and ignores it otherwise.
    private bool NitroPressed() => KeyDown(Key.N) || PadPressed(JoyButton.X);

    // The nitro lifecycle for this step, in the original's order: the command arm (human key,
    // or the AI's nitro-flagged maneuver), then the tank, then the edges the flag produced.
    // Every human pilot takes the player's arms (shake, loop, animation): plan Decision 3.
    private void AdvanceNitro(float dt)
    {
        bool engineOut = _model.EngineDead;
        if (IsHumanPiloted)
            Nitro.HumanCommand(NitroPressed(), engineOut, dt);
        else if (Pilot?.Machine?.Executor is { Maneuver.Nitro: true })
        {
            // One engage per maneuver, the way the maneuver starter fires SetNitro(1) once.
            if (!_aiNitroArmed)
                Nitro.AiSet(true, engineOut, dt);
            _aiNitroArmed = true;
        }
        else
        {
            _aiNitroArmed = false;
            Nitro.AiSet(false, engineOut, dt);
        }
        Nitro.Advance(dt, engineOut);

        if (Nitro.EngagedThisTick)
        {
            if (IsHumanPiloted)
                Shake?.NitroEngaged();
            if (PlaneModel != null)
                CrashRuntime?.PlayWithin(PlaneModel, "nitro_boost", applyReset: false);
            Log.Debug("flight", $"nitro engaged charge={Nitro.Charge:0.0}");
        }
        if (Nitro.ReleasedThisTick && PlaneModel != null && CrashRuntime != null)
        {
            CrashRuntime.StopWithin(PlaneModel, "nitro_boost");
            CrashRuntime.PlayWithin(PlaneModel, "nitro_decay", applyReset: false);
            _nitroDecayLeftS = NitroDecayAnimSeconds;
            Log.Debug("flight", $"nitro released charge={Nitro.Charge:0.0}");
        }
        // The decay def has no completion callback here; its opacity fade is authored 1.0 s long.
        if (_nitroDecayLeftS > 0f)
        {
            _nitroDecayLeftS -= dt;
            if (_nitroDecayLeftS <= 0f)
                Nitro.DecayFinished();
        }
        else if (Nitro.DecayAnimPlaying)
            Nitro.DecayFinished();

        if (IsHumanPiloted && Audio != null)
        {
            if (Nitro.BoostAnimAlive)
                Audio.StartNitroLoop();
            else
                Audio.StopNitroLoop();
        }
    }

    // Space / gamepad B — the gun trigger (caller drives the fire-rate clock);
    // `--fire` holds it down for unattended runs.
    private bool FirePressed() => AutoFire || KeyDown(Key.Space) || PadPressed(JoyButton.B);

    // F / gamepad A — the rocket trigger. One discrete pull launches one rocket (holding
    // does NOT auto-repeat; only the 1.0 s cooldown gates it), and `--fire-rockets` auto-repeats
    // for unattended runs. ⚠ Gamepad A must not also respawn: PadPressed is a level read, so a
    // button still held when the plane goes live fires a rocket on the spawn frame.
    private bool RocketFirePressed() => KeyDown(Key.F) || PadPressed(JoyButton.A);

    // G / gamepad D-pad Left — cycles the gun selector through the firable groups (1 → 2 →
    // … → 1). Only ONE group fires at a time; the gun trigger fires the selected one. Caller edge-detects.
    private bool GunSelectPressed() => KeyDown(Key.G) || PadPressed(JoyButton.DpadLeft);

    // H / gamepad D-pad Right — moves the hardpoint selector to the next pylon that still
    // carries ordnance (each pylon is its own selectable slot, whatever it loads — even a plane with
    // one uniform ordnance type). The rocket trigger then launches from the selected pylon. Caller
    // edge-detects.
    private bool RocketSelectPressed() => KeyDown(Key.H) || PadPressed(JoyButton.DpadRight);

    // This frame's pilot-HUD feed. The pipper's inputs are resolved HERE and only where there is a
    // reticle to draw: a muzzle midpoint reads one world transform per barrel, which every aircraft
    // drawing no reticle would otherwise pay every rendered frame.
    private FlightHudState BuildHudState(float wallDt, float simDt, bool halted, float headingDeg)
    {
        GunGroup? reticleGun = null;
        Vector3 reticleOrigin = default;
        Vector3 reticleNose = default;
        Vector3 inheritedVelocity = default;
        if (_pilotHud.DrawsReticle && !Crashed && Loadout != null && _fire != null
            && SelectedGun() is { } sel)
        {
            reticleGun = sel;
            reticleOrigin = MuzzleMidpoint(sel);
            reticleNose = -GlobalTransform.Basis.Orthonormalized().Z;
            inheritedVelocity = _model.VelocityDir * _model.Speed;
        }
        return new FlightHudState
        {
            Position = _model.Position,
            HeadingDeg = headingDeg,
            SpeedMps = _model.Speed,
            Throttle = _model.Throttle,
            Crashed = Crashed,
            Held = _held,
            Halted = halted,
            StallWarned = _model.IsStallWarned(),
            StallFraction = _model.StallFraction,
            Stalled = _model.isStalled(),
            WallDt = wallDt,
            SimDt = simDt,
            DamageSummary = _pilotHud.DrawsTextBlock ? Damage?.Summary() : null,
            StuntStatusLine = Stunt != null && _pilotHud.NeedsStuntStatusLine ? Stunt.StatusLine() : null,
            Loadout = Loadout,
            GunSelect = _fire?.GunSel ?? 0,
            PylonSelect = _fire?.SelectedPylon ?? 0,
            NitroInstalled = Nitro.Installed,
            NitroBoosting = Nitro.Boosting,
            NitroChargeFrac = Nitro.ChargeFraction,
            ReticleGun = reticleGun,
            ReticleOrigin = reticleOrigin,
            ReticleNose = reticleNose,
            InheritedVelocity = inheritedVelocity,
        };
    }

    // Pushes InPlay into the two facts the engine can only hold as state:
    // whether the airframe is drawn, and whether its body sits on the aircraft collision layer
    // (so a ray, a sweep or a hit test can find it). Everything else consults the flag. Called by
    // Respawn and by _Ready, the two points where the pieces that
    // carry the state come (back) into existence.
    private void ApplyPresence()
    {
        if (PlaneModel != null)
            PlaneModel.Visible = InPlay;
        Body?.SetHittable(InPlay);
    }

    /// <summary>The selected firable gun group — the one the trigger fires — or null when there is
    /// no loadout, no fire control, no group at the selected index, or the group has no muzzle to
    /// fire from. Shared by the pipper and the targeting marker's bracket gate so both read the
    /// same "which gun is selected" answer.</summary>
    private GunGroup? SelectedGun()
    {
        if (Loadout == null || _fire == null)
        {
            return null;
        }
        int gi = 0;
        foreach (var g in Loadout.FirableGuns)
        {
            if (gi == _fire.GunSel)
            {
                return g.Muzzles.Count > 0 ? g : null;
            }
            gi++;
        }
        return null;
    }

    /// <summary>A gun group's muzzle MIDPOINT: the original averages that group's live barrel
    /// attachments, which is where that group's fire converges.</summary>
    private Vector3 MuzzleMidpoint(GunGroup group)
    {
        var origin = Vector3.Zero;
        foreach (var m in group.Muzzles)
        {
            origin += m.GlobalPosition;
        }
        return origin / group.Muzzles.Count;
    }

    // One gun round's launch direction: the per-muzzle aim assist through
    // AimAssist.FireDirection, which restamps this barrel's slot so the forget timer
    // runs from the last SHOT. ⚠ Gated on IsHumanPiloted, not pane 1 — every CSVM
    // pane is a human pilot, so every pane is assisted. An AI plane's round leaves along the
    // gunner's lead instead; a gunner-less AI and a barrel with no slot fall back to the muzzle
    // axis.
    private Vector3 AssistedGunDirection(WeaponDef weapon, int gi, int mi, Node3D muzzle,
        Basis planeBasis, Vector3 inheritVel, double now)
    {
        var muzzleXf = muzzle.GlobalTransform;
        if (!IsHumanPiloted)
        {
            return Pilot?.Gunner is { WantsFire: true } gunner
                ? gunner.ShotDirection(muzzleXf.Origin)
                : -muzzleXf.Basis.Z.Normalized();
        }
        var slots = gi >= 0 && gi < _aimSlots.Length ? _aimSlots[gi] : null;
        if (slots == null || mi < 0 || mi >= slots.Length)
        {
            return -muzzleXf.Basis.Z.Normalized();
        }
        float range = weapon.Range ?? 0f;
        var scan = new AimScan
        {
            MuzzlePosition = muzzleXf.Origin,
            ShooterVelocity = inheritVel,
            // Alignment is measured against the PLANE's nose, not this muzzle's axis — the engine
            // scores every candidate against the airframe's own forward row.
            Forward = -planeBasis.Z,
            Team = Team,
            Speed = weapon.Velocity ?? ProjectilePool.DefaultVelocity,
            RangeSquared = range * range,
            ConeCos = AimAssist.WeaponConeCos(weapon),
            DistFactor = _model.Stats.StickyBulletDistFactor,
            Self = this,
        };
        var dir = AimAssist.FireDirection(ref slots[mi], scan, _aimCandidates, planeBasis, now,
            _model.Stats.StickyBulletInaccuracy, _aimRng, out var found);
        if (found.Found && !_aimLoggedFirst)
        {
            _aimLoggedFirst = true; // verification breadcrumb: the assist found something, once
            Log.Info("weapons", $"gun aim assist: P{PlayerIndex + 1} snapped onto {found.Kind} (score {found.Score:0.000}, {found.TimeOfFlight * (weapon.Velocity ?? ProjectilePool.DefaultVelocity):0} m out)");
        }
        return dir;
    }

    // Performs one FireControl.Step's decisions against the engine: spawns
    // the commanded rounds from their live muzzle nodes (with the plane's inherited velocity),
    // launches the commanded rocket, mirrors the loop-sound state onto FlightAudio
    // (started every wanted frame — StartGunLoop only rebuilds when the name changes, which is how
    // a mid-burst group switch swaps the loop), and sounds the dry cues. The first-round-per-group
    // and first-launches breadcrumbs log here — print throttles, not fire-control state.
    private void ApplyFireOutcome(FireOutcome outcome)
    {
        var inheritVel = _model.VelocityDir * _model.Speed;
        // Built ONCE for this tick's rounds, not per barrel: the same four lists feed every muzzle.
        if (outcome.GunShots.Count > 0 && Projectiles != null)
        {
            _aimCandidates.Clear();
            Projectiles.CollectAircraft(_aimCandidates);
            Projectiles.CollectTurrets(_aimCandidates);
            Projectiles.CollectFusedOrdnance(_aimCandidates);
            if (Destructibles != null)
            {
                _aimCandidates.AddStructures(Destructibles);
            }
            if (!_aimListsLogged)
            {
                _aimListsLogged = true; // verification breadcrumb: WHICH lists this build actually feeds
                // The nearest structure's range comes with the count on purpose: a registry whose
                // anchors carried no world transform would report its full count and sit at the
                // world origin, i.e. be silently untargetable, and the count alone would not show it.
                float nearest = float.MaxValue;
                foreach (var s in _aimCandidates.Structures)
                {
                    nearest = Mathf.Min(nearest, s.Position.DistanceTo(_model.Position));
                }
                string nearestPart = _aimCandidates.Structures.Count > 0
                    ? Log.Format($", nearest structure {nearest:0} m")
                    : string.Empty;
                Log.Info("weapons", $"gun aim assist: candidates vehicles={_aimCandidates.Vehicles.Count} turrets={_aimCandidates.Turrets.Count} structures={_aimCandidates.Structures.Count} ordnance={_aimCandidates.Ordnance.Count}{nearestPart}");
            }
        }
        var planeBasis = GlobalTransform.Basis.Orthonormalized();
        double aimNow = GameClock.Current?.Time ?? 0.0;
        foreach ((int gi, int mi) in outcome.GunShots)
        {
            var g = _firableGuns[gi];
            var muzzle = g.Muzzles[mi];
            var aimDir = AssistedGunDirection(g.Weapon, gi, mi, muzzle, planeBasis, inheritVel, aimNow);
            Projectiles!.Spawn(g.Weapon, muzzle.GlobalTransform, inheritVel, PlayerIndex, muzzle, aimDir, Team);
            Shake?.FireBullet(g.Weapon.Caliber ?? 0f); // the firing buzz: factor × caliber (measured)
            if (!_gunLoggedFirst[gi])
            {
                _gunLoggedFirst[gi] = true;   // verification breadcrumb: which groups actually fire
                Log.Info("weapons", $"gun group {gi + 1} ({g.Mount}, {g.Weapon.Caliber ?? 0}-cal {g.Weapon.Id}) firing on {Name}");
            }
        }
        if (outcome.RocketPylon >= 0)
        {
            var hp = Loadout!.Hardpoints[outcome.RocketPylon];
            var rocketAim = OrdnanceLaunchDir(IsHumanPiloted, planeBasis, hp.Weapon.Rear,
                !IsHumanPiloted && Pilot?.Rocketeer is { } launcher ? launcher.LaunchDirWorld : null);
            // The round's target, the second half of `ProjectilePool.SteeringStepRuns`'s gate: a
            // human's own selection, an AI's gunner quarry. A LOCK_ON round holding none never
            // sheds its inherited launch velocity.
            object? launchTarget = IsHumanPiloted ? Targeting?.Current?.Source : Pilot?.Gunner?.Target;
            // A SMOKE_SCREEN weapon spawns no round: the original's launch branch builds a world
            // object carrying the weapon's TIME instead, and skips the spawn that would have
            // played the FIRE row's sound and animation. Ammo and the fire clock are already spent.
            if (hp.Weapon.SmokeScreenTime is { } screenTime)
            {
                SmokeScreens?.Lay(this, screenTime);
            }
            else
            {
                Projectiles!.Spawn(hp.Weapon, hp.Pylon.GlobalTransform, inheritVel, PlayerIndex, hp.Pylon,
                    rocketAim, Team, launchTarget);
            }
            if (_rocketsLaunched < 12)
            {
                _rocketsLaunched++;
                Log.Info("weapons", $"rocket: {Name} launched {hp.Weapon.Id} ({hp.Weapon.Name}) from pylon{hp.Index}, {(InfiniteAmmo ? "∞" : hp.Ammo.ToString(CultureInfo.InvariantCulture))} left on that pylon");
            }
        }
        if (outcome.GunLoopWanted)
        {
            _gunLoopOn = true;
            Audio?.StartGunLoop(outcome.GunLoopSound);
        }
        else if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        if (outcome.GunDryCue)
        {
            Audio?.PlayEmptyClip();
        }
        if (outcome.RocketDryCue)
        {
            Audio?.PlayEmptyClip();
            Log.Info("weapons", $"rocket: {Name} dry pull, all pylons empty — empty-clip cue");
        }
    }

    // Refills every gun group to its full load and re-arms the dry warnings (respawn).
    private void RefillWeapons()
    {
        if (Loadout == null)
        {
            return;
        }
        // The loadout-wide top-up covers the inert turret groups too, and runs even before _Ready
        // has built the state machine (Setup's pre-tree Respawn). FireControl.Refill re-tops its
        // own slots with the same values and resets the machine (clocks, cursors, dry warnings).
        foreach (var g in Loadout.Guns)
        {
            g.Ammo = g.Capacity;
        }
        foreach (var h in Loadout.Hardpoints)
        {
            h.Ammo = h.Capacity;
        }
        _fire?.Refill();
        if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        Projectiles?.Clear();
    }

    // Full stunt restart from the results scoreboard (R): fresh clock + every
    // zone incomplete, then the normal respawn (spawn pose / throttle / cleared damage). The
    // scoreboard hides itself once AllComplete clears; the marker HUD replays its intro line.

    // True if the segment crosses any solid collider — the static world, or another
    // aircraft's body (never this plane's own, excluded by RID); on a hit,
    // `point` is the impact position (else the segment end) and
    // `hitName` names the collider (parent/body — e.g. a terrain
    // tile's "g27889/col", or a clutter city block's "world1/clutter_bld_3_7").
    private bool HitWorld(Vector3 from, Vector3 to, out Vector3 point, out string hitName, out Node? hitBody)
    {
        point = to;
        hitName = "";
        hitBody = null;
        if (!World.Ray(from, to, CollisionLayers.WorldAndAircraft, Body?.ExcludeSelf, out var report))
            return false;
        point = report.Position;
        if (report.Collider is { } body)
        {
            hitBody = body;
            hitName = $"{body.GetParent()?.Name}/{body.Name}";
        }
        return true;
    }

    // Ground blow's probe (docs/org/flightModel.md "Ground blow"): a ray along the nose
    // whose nearest hit FlightModel turns into a control-response bias.
    // ⚠ The mask is CollisionLayers.World, the decoded emitter rule — do not add an
    // aircraft mask. ⚠ Skipped while the AI pilot is stunned; do not lift either gate.
    private void ProbeGroundBlow(ref FlightInput input)
    {
        float elev = _model.Stats.GroundBlowElev;
        if (elev <= 0f)
            return;
        if (!IsHumanPiloted && (!_model.UsesAiForcePath || Pilot?.IsStunned == true))
            return;
        var from = _model.Position;
        if (!World.Ray(from, from - _model.Attitude.Z * elev, CollisionLayers.World, null, out var report))
            return;
        input.GroundBlowNormal = report.Normal;
        input.GroundBlowDistM = from.DistanceTo(report.Position);
        if (!_groundBlowLoggedFirst && _model.Attitude.Z.Dot(input.GroundBlowNormal) > 0f)
        {
            _groundBlowLoggedFirst = true;   // verification breadcrumb: the probe reaches real geometry
            // The facing test comes with it: a hit that fails it is inert and must not read as "working".
            string what = report.Collider is { } body
                ? $"{body.GetParent()?.Name}/{body.Name}" : "?";
            GD.Print($"ground blow: first repelling hit on {what} at {input.GroundBlowDistM:0} m "
                     + $"of {elev:0} (facing {_model.Attitude.Z.Dot(input.GroundBlowNormal):0.00})");
        }
    }

    // Squared HORIZONTAL range to the nearest human pilot, the quantity the flight model's
    // far-field branch is selected on. The original measures Δx² + Δz² against its single player;
    // this reads every human, which is the flight-parity plan's Decision 3 (docs/plans/PLAN-flight-model-parity.md).
    // ⚠ No seam bound means no human is known, and 0 keeps the aircraft near-field.
    private float NearestHumanDistSqM()
    {
        if (HumanPositions?.Invoke() is not { Count: > 0 } humans)
            return 0f;
        var here = _model.Position;
        float best = float.MaxValue;
        for (int i = 0; i < humans.Count; i++)
        {
            float dx = humans[i].X - here.X;
            float dz = humans[i].Z - here.Z;
            float d = (dx * dx) + (dz * dz);
            if (d < best)
                best = d;
        }

        return best;
    }

    // Vertical clearance over static world collision only. Unlike HitWorld,
    // another aircraft below the camera is not ground for speed_cue's NODE_NEAR_GROUND gate.
    private float HeightAboveWorldGround(Vector3 from)
    {
        var to = from + Vector3.Down * 1000f;
        return World.Ray(from, to, CollisionLayers.World, null, out var report)
            ? from.Y - report.Position.Y
            : float.MaxValue;
    }

    // The explosion boom the chosen crash def authors, which FlightAudio
    // plays because the crash runtime renders effects and never sound: `player_crash_dirt`
    // Sounds `snd_exp_ground_a` itself, `player_crash_water`'s `snd_exp_water_a`
    // sits one level down in the `plane_big_splash` it calls, and the fallback
    // `player_crash_default` Sounds only `plane_destroy_sg` — already played by
    // FlightAudio.OnCrash — so it layers no surface boom at all.
    private void PlayCrashBoom(string? crashDef)
    {
        switch (crashDef)
        {
            // The AI family authors the same surface booms (ai_crash_dirt Sounds snd_exp_ground_a
            // itself; ai_crash_water's snd_exp_water_a rides the plane_big_splash it calls), so
            // both prefixes take the same arm.
            case EffectCatalogue.CrashDefPrefix + "water":
            case EffectCatalogue.AiCrashDefPrefix + "water":
                Audio?.OnWaterExplosion();
                break;
            case EffectCatalogue.CrashDefPrefix + "dirt":
            case EffectCatalogue.AiCrashDefPrefix + "dirt":
                Audio?.OnGroundExplosion();
                break;
        }
    }

    /// <summary>The DEATH event: whole-vehicle health has reached zero. Starts the airframe's own
    /// <see cref="DestroyDef"/> and leaves the wreck VISIBLE — the surface-indexed
    /// <see cref="CrashDefs"/> family belongs to the wreck's ground contact, and playing it here is
    /// what deleted the fall, the burning wreck and the parachute (org/vehicleDamage.md).</summary>
    private void Destroy(Vector3 at, string hitName, string part, int? killer)
    {
        // The transition decides; everything below performs what it reported.
        var death = _lifecycle.Destroy(CrashRuntime != null ? DestroyDef : null, killer);
        if (!death.Occurred)
            return;
        Body?.SetHittable(false);       // a dead plane soaks no rounds and blocks no sweep
        if (death.EndFlightSystems)
            EndFlightSystems();
        string? destroyDef = death.DestroyDef;
        if (destroyDef != null)
        {
            // The dead hull flies itself from here; the def's own Callback 15 stops it, at 3.0 s on
            // the ten AI airframes and at once on `player`. ⚠ Wire all three seams before Play, or
            // the untimed player def raises them into nothing (docs/org/vehicleDamage.md).
            CrashRuntime!.WreckVelocity = () => _model.VelocityDir * _model.Speed;
            CrashRuntime.StopWreckFlying = () => _lifecycle.StopWreckFall();
            // Code 15's other half: the stages the hull was wearing end with it. Both AI stage anims
            // are LOOP -1 with NO authored exit, so nothing else can reach them and a survivor emits
            // forever at the node the wreck left behind.
            CrashRuntime.StopDamageStages = () => Visuals?.DamageEffectStop?.Invoke();
            // The airburst, the pilot's chute and (on the eleven airframe defs) the wreck's own
            // launch: this plane's parts detaching, not a world destructible's.
            using (PerfSample.Scope(PerfSite.PartDetach))
            {
                CrashRuntime.Play(destroyDef, CrashAnchor, applyReset: false);
                CrashRuntime.Play("stopprops", PlaneModel, applyReset: false);
            }
        }
        else if (PlaneModel != null)
        {
            // No destroy def bound: nothing authored can end this airframe, so hide it outright
            // rather than leave a pristine hull hanging in the air.
            PlaneModel.Visible = false;
        }
        if (death.CutCamera)
            CutToCrashView(at);
        Log.Info("flight",
            $"DESTROYED by {hitName} ({part}) def={destroyDef ?? "-"} wreck={(death.WreckFalling ? "falling (flight model)" : "handed over on the kill frame")} lands={(DestroyDefFliesWreck ? "anim (bounce sequence)" : "ground-impact def")} pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) spd={_model.Speed:0} m/s");
        if (death.Downed)
            Downed?.Invoke(PlayerIndex, death.Killer);
    }

    // Everything an aircraft stops doing the moment it is out of the fight, whether it died in the
    // air or struck the world. Runs exactly once per death: the gun loop would otherwise keep
    // playing under the wreck, since nothing else calls StopGunLoop while the plane is crashed.
    private void EndFlightSystems()
    {
        if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        Audio?.StopNitroLoop();
        Audio?.OnCrash();
        // An AI aircraft's loops end here and stay ended: the animation's own authored sound
        // events are what is audible from now on, and no wreck respawns to restart them.
        EngineAudio?.Stop();
        // The engine wind-down cue layers over the explosion, replacing the loops' abrupt cut with
        // snd_propstop.
        Audio?.OnEngineStop();
        // No plume survives a dead engine.
        ThrottleSmoke?.Reset(_throttle);
        SpeedCue?.Reset();
    }

    // The authored crash camera: hard-cut to the static elevated vantage and hide the HUD — both
    // straight off the original's crash footage. Written once, and _Process writes nothing to the
    // camera while crashed, so the pose holds until respawn.
    private void CutToCrashView(Vector3 at)
    {
        if (!CameraOwned)
            _cam?.CrashView(at, _model.VelocityDir);
        if (_hudCanvas != null)
            _hudCanvas.Visible = false;
    }

    // The dead hull flying itself, on the same model it flew alive: the original gates nothing in
    // the integrator on death (docs/org/flightModel.md, "A destroyed hull flies the same model").
    // ⚠ The commands FREEZE rather than neutralise: death stops the AI think, so nothing writes the
    // command vector and the integrator reads its last value. A default input here would be ours.
    // Runs until Callback 15 takes the hull over, so a wreck reaching the ground first still gets
    // its crash def, as the original's does.
    private void StepWreckFall(float dt)
    {
        if (!WreckFalling)
            return;
        var prev = _model.Position;
        // The stick freezes, the range does not: the original re-tests the far-field boundary every
        // step regardless of what is flying the aircraft, so a wreck drifting past it switches too.
        var falling = _lastInput;
        falling.NearestHumanDistSqM = NearestHumanDistSqM();
        _model.Step(falling, dt);
        if (_model.Position.Y < UnderMapY)
        {
            _lifecycle.StopWreckFall();   // lost under the map; nothing left to strike
            return;
        }
        if (HitWorld(prev, _model.Position, out var impact, out var hitName, out var hitBody))
        {
            Crash(impact, hitName, "center", hitBody);
            return;
        }
        _simPrev = _simCurr;
        _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;
    }

    /// <summary>The GROUND-IMPACT event: a live aircraft flown into the world, or a
    /// <see cref="Destroy"/>ed wreck reaching it. Plays the surface-indexed
    /// <see cref="CrashDefs"/> slot, which is where that family belongs.</summary>
    private void Crash(Vector3 impact, string hitName, string part, Node? hitBody, int? killer = null)
    {
        // The original's selection: index by the struck material's surface id, falling back to
        // slot 0 (player_crash_default) for most of the ground (id 0 is ~98% of every chapter).
        int? surfaceId = SurfaceIdOf(hitBody);
        // The transition decides, including the guard and which of the four effects below are due;
        // everything after this line performs what it reported.
        var landing = _lifecycle.Crash(surfaceId, killer);
        if (!landing.Occurred)
            return;
        string? crashDef = landing.CrashDef;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        Body?.SetHittable(false);       // a crashed plane soaks no rounds and blocks no sweep
        if (landing.EndFlightSystems)
            EndFlightSystems();
        PlayCrashBoom(crashDef);
        if (CrashRuntime != null && crashDef != null)
        {
            // Plays the compiled def; see this module's entry in docs/architecture.md.
            // ⚠ Never seed an inherited velocity here: the `player_crash_*` defs this path plays
            // inherit none in the original, and only a `Callback 16` can arm one anyway.
            using (PerfSample.Scope(PerfSite.PartDetach))
            {
                CrashRuntime.Play(crashDef, CrashAnchor, applyReset: false);
                // The prop wind-down (staticpropN fades back in as prop1..3 fade out) — inert the
                // instant PlaneModel above hides, but keeps the def's own state consistent for
                // whatever plays next, and matters once a shutdown can leave the airframe visible.
                CrashRuntime.Play("stopprops", PlaneModel, applyReset: false);
            }
        }
        // ⚠ Not on a wreck landing: the cut and the report both belong to the kill, seconds
        // earlier, and re-cutting here would swing the camera off the fall it was framing.
        if (landing.CutCamera)
            CutToCrashView(impact);
        string surface = surfaceId is { } sid
            ? $"{sid}/{SurfaceRegistry.NameForId(sid) ?? "?"}"
            : "none";
        // A mid-air's ASPECT (diagnostic): which AI rule should have prevented it depends entirely
        // on whether the two met head-on, overtaking or side-on, and no crash line carries that.
        if (hitBody is AircraftBody struckAir)
        {
            var mine = _model.VelocityDir;
            var theirs = struckAir.Rig.WorldVelocity;
            var los = struckAir.Rig.WorldPosition - _model.Position;
            Log.Info("flight",
                $"midair aspect: into {hitName} — tracks {AngleBetweenDeg(mine, theirs):0}° apart (0 = same heading, 180 = head-on), line of sight {AngleBetweenDeg(mine, los):0}° off own track, spd mine={_model.Speed:0} theirs={theirs.Length():0} m/s");
        }
        Log.Info("flight",
            $"CRASH into {hitName} ({part}) surface={surface} def={crashDef ?? "-"} wreck={landing.WreckLanding} impact=({impact.X:0},{impact.Y:0},{impact.Z:0}) pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) spd={_model.Speed:0} m/s — waiting for respawn");
        if (landing.Downed)
            Downed?.Invoke(PlayerIndex, landing.Killer);
    }

    // True when the button is down on one of THIS player's gamepads. With
    // PadDevices null (single player) that is every connected pad — never `pads[0]`:
    // phantom joypad devices (wireless dongles enumerating with the pad asleep, non-pad HID like
    // Razer boards) can occupy the early slots, which made a pad connected after launch (= a later
    // slot) dead. Splitscreen binds each player to its own device list instead.
    private bool PadPressed(JoyButton button)
    {
        foreach (int pad in Pads.For(PadDevices))
            if (Input.IsJoyButtonPressed(pad, button))
                return true;
        return false;
    }

    // The largest-magnitude value of the axis across this player's gamepads (0 when
    // none) — idle phantom devices read ~0 and never mask the real stick.
    private float PadAxis(JoyAxis axis)
    {
        float v = 0f;
        foreach (int pad in Pads.For(PadDevices))
        {
            float a = Input.GetJoyAxis(pad, axis);
            if (Mathf.Abs(a) > Mathf.Abs(v))
                v = a;
        }
        return v;
    }

    // A key, but only for a player the keyboard flies (splitscreen P2–P4 are pad-only).
    private bool KeyDown(Key key) => UseKeyboard && Input.IsKeyPressed(key);

    // A +/- key pair as an axis, honoring UseKeyboard.
    private float KeyAxis(Key positive, Key negative) =>
        (KeyDown(positive) ? 1f : 0f) - (KeyDown(negative) ? 1f : 0f);

    // R and pad Y while a results board is up: the direct route to the board's Restart item, and
    // the hold harness's automatic one. Read off the rendered frame on wall time, since the board
    // halts the clock and a sim-dt timer under a halt would never fire.
    private void PollResultsShortcuts(float delta)
    {
        if (Match is { Completed: true })
        {
            if (RespawnPressed())
                RestartMatch?.Invoke();
            return;
        }
        if (Race is { AllFinished: true } && Stunt is { AllComplete: true })
        {
            bool autoRematch = _holdSegments != null && PlayerIndex == 0
                && (_autoRestartIn -= delta) <= 0f;
            if (RespawnPressed() || autoRematch)
                RestartRace?.Invoke();
            return;
        }
        if (Stunt is { AllComplete: true } && Race == null && RespawnPressed())
            Rerun();   // the solo board takes R as a fresh run, distinct from a mid-run respawn
    }

    private bool RespawnPressed() =>
        KeyDown(Key.R) || PadPressed(JoyButton.Y);

    // P, Esc or gamepad Start, edge-detected so one press toggles once, gated on AllowPause
    // (false for AI rigs and the suites' bare test rigs). Esc opens the pause board rather than
    // leaving the flight; the board's Exit item is what leaves, and a pad can reach it.
    // ⚠ Silent in photo mode: Escape is what LEAVES that mode, and this reads Escape too, so one
    // press would both close the mode and unpause the session behind it (BL-429).
    private bool PauseTogglePressed() =>
        AllowPause && !InPhotoMode
        && (KeyDown(Key.P) || KeyDown(Key.Escape) || PadPressed(JoyButton.Start));

    // One frame of the pause key, and the halt it mirrors into the shared clock. Polled from
    // _Process, not the sim step: a halted sim takes no steps and could never resume itself.
    // With a shared PauseState only the player who paused may resume it.
    private bool PollPauseAndHalt(GameClock? clock)
    {
        bool pausePressed = PauseTogglePressed();
        if (pausePressed && !_pausePrev)
        {
            if (PauseState != null)
                PauseState.TryToggle(PlayerIndex);
            else if (clock != null)
                clock.Halted = !clock.Halted;
        }
        _pausePrev = pausePressed;
        bool halted = PauseState?.Halted ?? (clock?.Halted ?? false);
        if (clock != null)
            clock.Halted = halted;
        if (halted != _haltPrev)
        {
            _haltPrev = halted;
            // The engine/whine/rattle loops hold their sample position through the freeze; the
            // one-shots already in flight are left to play out.
            Audio?.SetPaused(halted);
        }
        return halted;
    }

    // Tab / gamepad X — cycles the stunt marker's displayed target (caller edge-detects).
    // Gamepad Y would clash with the respawn button, so X (a free face button) instead.
    private bool CycleTargetPressed() =>
        KeyDown(Key.Tab) || PadPressed(JoyButton.X);

    /// <summary>One frame of player targeting: rebuild the pool and re-resolve, prune the
    /// attacker queue, then dispatch this frame's input. That order is the original's — its
    /// per-frame candidate pass runs in the sim step and a handler steps the list it just built,
    /// which is why a class change reads one frame late and self-heals.</summary>
    private void StepTargeting(float dt)
    {
        var sel = Targeting!;
        if (Projectiles != null && sel.ActiveClass != null)
        {
            // Skipped entirely with the selection cleared: `Target Nothing` zeroes the class flags
            // and the original then skips its whole collection pass, which is the mechanism that
            // keeps the clear cleared rather than an optimisation.
            _targetScan.Clear();
            Projectiles.CollectAircraft(_targetScan);
            Projectiles.CollectTurrets(_targetScan);
            // The fourth pool (E19): a TARGETABLE round in flight is selectable, which is why a
            // torpedo can be locked and shot at. The pool itself reads the admission byte.
            Projectiles.CollectFusedOrdnance(_targetScan);
            _targetParts.Clear();
            TargetSubParts?.Invoke(_targetParts);
            // The mission's objective sites, rebuilt from their live sources every frame, so a
            // site under a moving node is marked where it now is rather than where it was.
            _targetSites.Clear();
            TargetObjectives?.Invoke(_targetSites);
        }
        // The team is read off the FIELD. Deriving it from PlayerIndex is right for P1 by
        // coincidence and wrong for every other pane the moment a mission sets teams — the
        // wingman-in-the-marker bug (see TargetHud.OwnTeam).
        sel.Rebuild(_targetScan, _targetParts, Team, this, _model.Position, _model.Attitude,
            _targetSites);

        // The death prune (FUN_004a64e0). There is no session-wide Downed broadcast outside --vs,
        // so this pane prunes its own queue: a shot-down attacker must not be offered again.
        for (int i = sel.Attackers.Count - 1; i >= 0; i--)
        {
            if (sel.Attackers[i] is FlightController { InPlay: false } dead)
                sel.ForgetTarget(dead);
        }

        // --target=, before the input dispatch and before the InPlay gate: a --det run pins its
        // selection without a pilot who can press anything, and a real keypress on the same frame
        // should win over the scripted one rather than be overwritten by it.
        if (InitialTarget != null && !_initialTargetDone)
        {
            ApplyInitialTarget(sel);
        }

        // ⚠ Gate the INPUT on InPlay, not the rebuild. The freecam a downed pilot watches from binds
        // `U` among its own keys, so reading targeting from a spectator both re-targets a plane that
        // is not there and fights the camera. The selection keeps re-resolving regardless.
        if (!InPlay)
        {
            _targetHold.Step(false, dt);   // let a button held through the crash resolve as nothing
            return;
        }

        // D-pad Up, the pad's one targeting button (decision 6/7): tap steps the enemy cycle, hold
        // selects the target nearest the crosshair. TapHoldButton owns the timing and the
        // resolve-on-release rule; this reads the pad and acts on the verdict.
        switch (_targetHold.Step(PadPressed(JoyButton.DpadUp), dt))
        {
            case TapHold.Hold:
                sel.NearestCrosshairs(_model.Position, _model.Attitude);
                break;
            case TapHold.Tap:
                sel.NextEnemy();
                break;
        }

        // The curated keyboard set (decision 14): every one of the original's eleven targeting keys
        // collides with our flight scheme, so these five free keys carry one action per class plus
        // the two class-less ones. They are defaults, not a scheme — the rebind layer is its own item.
        DispatchTargetKey(0, Key.T, () => sel.NextEnemy());
        DispatchTargetKey(1, Key.Y, () => sel.Next(TargetClass.Ally));
        DispatchTargetKey(2, Key.U, () => sel.Next(TargetClass.NonAircraft));
        DispatchTargetKey(3, Key.I, () => sel.NearestCrosshairs(_model.Position, _model.Attitude));
        DispatchTargetKey(4, Key.O, () => sel.Clear());
    }

    /// <summary>Spends <c>--target=</c>'s one application. Waits for a non-empty pool first:
    /// the things it can name (AI spawns, the zeppelins, a generator's first drop) are all built
    /// after the rigs are, so applying on frame one would match nothing in every session.
    /// <c>none</c> needs no pool and does not wait.</summary>
    private void ApplyInitialTarget(TargetSelection sel)
    {
        bool needsPool = !string.Equals(InitialTarget, "none", System.StringComparison.OrdinalIgnoreCase);
        if (needsPool && sel.Pool.Count == 0 && ++_initialTargetWaits < InitialTargetGrace)
        {
            return;
        }

        _initialTargetDone = true;      // spent whether or not it matched: one chance, then hands off
        if (sel.ApplyInitial(InitialTarget!, _model.Position, _model.Attitude))
        {
            string picked = sel.Current is { } t && t.Name.Length > 0 ? t.Name : "nothing";
            GD.Print($"--target={InitialTarget}: {picked} (class={sel.ActiveClass?.ToString() ?? "cleared"})");
            return;
        }

        // Naming what IS selectable is the whole diagnosis for a mistyped node name, and it is why
        // the flag needs no separate listing mode.
        var names = new List<string>();
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var t in sel.Pool.Of(cls))
            {
                if (t.Name.Length > 0 && !names.Contains(t.Name))
                {
                    names.Add(t.Name);
                }
            }
        }

        names.Sort(System.StringComparer.OrdinalIgnoreCase);
        const int MaxNamed = 24;    // a C1 session offers ~100; enough to recognise a typo, not a wall
        int extra = names.Count - MaxNamed;
        if (extra > 0)
        {
            names.RemoveRange(MaxNamed, extra);
        }

        string listed = names.Count == 0 ? "(nothing)"
            : string.Join(", ", names) + (extra > 0 ? $", +{extra} more" : "");
        Log.Warn("core", $"--target={InitialTarget}: no match — selectable now: {listed}");
    }

    /// <summary>The view-selection inputs, edge-detected: F8 or D-pad Down cycles the first-person
    /// pair (Cockpit ↔ Nose, entering Cockpit from Chase — the original's "Cycle Cockpit Views",
    /// which its binding menu also puts on a joystick button), F6 or the pad's Back/Select selects
    /// the chase view. The original binds a selector per view rather than one three-stop cycle, so
    /// the way out of first person is its own input; F6 and Back are this port's choices for it.
    /// The pad half makes the views reachable for a pad-only pilot (P2–P4), who has no keyboard.</summary>
    private void PollViewModeKeys()
    {
        DispatchViewModeKey(0, KeyDown(Key.F8), () => _cam!.CycleCockpitViews());
        DispatchViewModeKey(1, KeyDown(Key.F6), () => _cam!.SelectChase());
        DispatchViewModeKey(2, PadPressed(JoyButton.DpadDown), () => _cam!.CycleCockpitViews());
        DispatchViewModeKey(3, PadPressed(JoyButton.Back), () => _cam!.SelectChase());
    }

    // Same one-action-per-press rule as DispatchTargetKey, against its own slots.
    private void DispatchViewModeKey(int slot, bool down, System.Action act)
    {
        if (down && !_viewModeKeyPrev[slot])
            act();
        _viewModeKeyPrev[slot] = down;
    }

    /// <summary>Edge-detects one targeting key against its own slot and runs its action once per
    /// press. Splitscreen-safe by construction: <see cref="KeyDown"/> is gated on
    /// <see cref="UseKeyboard"/>, so P2–P4 (pad-only) never see these.</summary>
    private void DispatchTargetKey(int slot, Key key, System.Action act)
    {
        bool down = KeyDown(key);
        if (down && !_targetKeyPrev[slot])
            act();
        _targetKeyPrev[slot] = down;
    }

    // Internal rather than private: IFlightInputSource.cs's PilotInputSource calls this to keep
    // the body where it always lived, rather than hoisting it for SA1202's sake. The scripted hold
    // sequence's own body lives on ScriptedInputSource now, which needs no such wrapper.
#pragma warning disable SA1202
    // The AI pilot's throttle is the desired lever (+0x124); like keyboard input, its live
    // flight-model lever (+0x128) must traverse at 0.5/s. Carrier launch seeds both at 0.1,
    // then the AI may immediately request full power without erasing the visible settling ramp.
    internal FlightInput NextPilotInput(float dt)
    {
        // The mode machine's obstacle probe (D11 avoid crash) is this node's world-and-aircraft
        // ray; wired lazily so a machine assigned after spawn still gets it, and never
        // overwriting a probe a test injected.
        if (Pilot!.Machine is { ProbeBlocked: null } machine && IsInsideTree())
            machine.ProbeBlocked = AvoidCrashBlocksLine;
        var input = Pilot!.Next(_model, dt);
        _throttle = Mathf.MoveToward(_throttle, input.Throttle, ThrottleRate * dt);
        input.Throttle = _throttle;
        return input;
    }
#pragma warning restore SA1202

    // Four small static helpers for the D36 gunner-target widening (BL-363), kept beside the
    // instance methods that are their only real callers rather than hoisted for SA1204's sake —
    // the same trade the SA1202 blocks elsewhere in this file already make.
#pragma warning disable SA1204
    // A standing gunner target's current geometry and liveness, read off whichever of the three
    // D36 source types (BL-363) it actually is — a FlightController, a TurretController or a
    // DestructibleRegistry.Instance. Neither non-aircraft source carries a facing axis (forward
    // comes back zero). Internal: TargetingOverlay's F15 debug line reuses this same lookup.
    internal static bool TryTargetGeometry(object? target, out Vector3 position, out Vector3 velocity,
        out Vector3 forward, out bool live)
    {
        switch (target)
        {
            case FlightController fc:
                position = fc.WorldPosition;
                velocity = fc.WorldVelocity;
                forward = fc.NoseDirection;
                live = fc.InPlay && fc.IsInsideTree();
                return true;
            case TurretController t:
                position = t.WorldPosition;
                velocity = t.PlatformVelocity;
                forward = Vector3.Zero;
                live = t.Alive;
                return true;
            case DestructibleRegistry.Instance inst:
                live = inst.Status != DestructibleRegistry.State.Destroyed
                    && GodotObject.IsInstanceValid(inst.Anchor) && inst.Anchor.IsInsideTree();
                position = live ? inst.Anchor.GlobalPosition : Vector3.Zero;
                velocity = Vector3.Zero;
                forward = Vector3.Zero;
                return true;
            default:
                position = velocity = forward = Vector3.Zero;
                live = false;
                return false;
        }
    }

    // Where a target source is DRAWN this frame, as opposed to where the gun solves to. An
    // aircraft's WorldPosition is the last physics pose; its node sits on _renderPose, the pose
    // interpolated between sim steps that the chase camera follows too. A marker projected from
    // the physics pose through that camera stalls between ticks and jumps on each, a shake that
    // grows with angular rate (BL-519). Turret and structure positions are node positions already.
    // False on a freed, out-of-tree or unknown source: the caller keeps its physics snapshot.
    internal static bool TryRenderPosition(object? source, out Vector3 position)
    {
        switch (source)
        {
            case FlightController fc when GodotObject.IsInstanceValid(fc) && fc.IsInsideTree():
                position = fc.GlobalPosition;
                return true;
            case TurretController t:
                position = t.WorldPosition;
                return true;
            case DestructibleRegistry.Instance inst when GodotObject.IsInstanceValid(inst.Anchor)
                && inst.Anchor.IsInsideTree():
                position = inst.Anchor.GlobalPosition;
                return true;
            default:
                position = Vector3.Zero;
                return false;
        }
    }

    // The breadcrumb label for a standing target: "P{n}" for a human-readable aircraft slot, the
    // node/label name (TargetPool.NameOf) for a turret or structure.
    private static string TargetLabel(object? source) =>
        source is FlightController fc ? $"P{fc.PlayerIndex + 1}" : TargetPool.NameOf(source);

    // The gunner's one standing target, whichever of its two fields holds it — AiGunner.Target
    // (aircraft, AiPilot's own pursuit quarry) or AiGunner.GroundTarget (D36's non-aircraft half).
    // The two are mutually exclusive by construction (see AssignAcquired below).
    private static object? StandingTarget(AiGunner gunner) =>
        (object?)gunner.Target ?? gunner.GroundTarget;

    // Routes one D12/D36 acquisition winner into the field AiPilot's flight law expects: an
    // aircraft into Target (so it becomes the pursuit quarry), anything else into GroundTarget
    // (so the flight law sees nothing and keeps flying its assigned course while the gunner alone
    // aims and fires at it) — the mechanism AiGunner.GroundTarget's own doc names.
    private static void AssignAcquired(AiGunner gunner, object? acquired)
    {
        if (acquired is FlightController fc)
        {
            gunner.Target = fc;
            gunner.GroundTarget = null;
        }
        else
        {
            gunner.Target = null;
            gunner.GroundTarget = acquired;
        }
    }
#pragma warning restore SA1204

    // One AI-gunner tick: keep the standing target while it is in play (re-acquiring
    // through the D12/D36 ranking when it is gone and AiGunner.AutoTarget allows), then
    // hand the gunner this tick's fire geometry — the SELECTED gun group's weapon and muzzle
    // midpoint, the sim pose (never the render pose), and the target's state — so
    // AiGunner.WantsFire is current when the fire step reads it.
    private void DriveAiGunner(AiGunner gunner)
    {
        gunner.HoldFire();
        if (_fire == null || Projectiles == null)
            return;
        if (!TryTargetGeometry(StandingTarget(gunner), out var targetPos, out var targetVel,
                out var targetFwd, out bool targetLive) || !targetLive)
        {
            TargetScore score = default;
            string how = "ranked";
            AssignAcquired(gunner, gunner.AutoTarget
                ? SelectRankedTarget(gunner, out score, out how)
                : null);
            if (!TryTargetGeometry(StandingTarget(gunner), out targetPos, out targetVel, out targetFwd,
                    out targetLive) || !targetLive)
                return;
            if (!_gunnerLoggedTarget)
            {
                _gunnerLoggedTarget = true; // verification breadcrumb: who the gunner went after
                Log.Info("flight",
                    $"ai gunner: shooter {PlayerIndex} targets {TargetLabel(StandingTarget(gunner))} at {score.Distance:0} m ({how}: weight {score.Weight:0.0#} bias {score.Bias:0} rank {score.Rank:0})");
            }
        }
        // ⚠ Only Pursue shoots. Lay off holds fire deliberately (the rubber-band assist) even
        // though the target stays acquired in every mode.
        if (Pilot?.Machine is { } modes && modes.Mode != AiMode.Pursue)
            return;
        GunGroup? group = _fire.GunSel >= 0 && _fire.GunSel < _firableGuns.Length
            ? _firableGuns[_fire.GunSel]
            : null;
        if (group == null || group.Muzzles.Count == 0)
            return;
        // The muzzle midpoint of the selected group — the same convergence point the reticle
        // and the original's own barrel averaging use.
        var muzzlePos = Vector3.Zero;
        foreach (var m in group.Muzzles)
            muzzlePos += m.GlobalPosition;
        muzzlePos /= group.Muzzles.Count;
        // The engagement window is a property of the weapon slot, not of the pilot, so a group whose
        // AI def authored one flies that one; a stock fit authors none and keeps the gunner's own.
        if (group.MinRangeM > 0f && group.MaxRangeM > 0f)
        {
            gunner.MinRangeM = group.MinRangeM;
            gunner.MaxRangeM = group.MaxRangeM;
        }
        gunner.Solve(muzzlePos, WorldVelocity, _model.Attitude,
            targetPos, targetVel, targetFwd,
            group.Weapon.Velocity ?? ProjectilePool.DefaultVelocity);
        if (gunner.WantsFire && !_gunnerLoggedFire)
        {
            _gunnerLoggedFire = true; // verification breadcrumb: the gates first opened
            Log.Info("flight",
                $"ai gunner: shooter {PlayerIndex} opens fire on {TargetLabel(StandingTarget(gunner))} at {WorldPosition.DistanceTo(targetPos):0} m ({group.Weapon.Id})");
        }
    }

    // One AI-rocketeer tick: age the lockout unconditionally (it is a vehicle timer, not one that
    // stops when the AI loses its target), then walk the pylons against the GUNNER's target and let
    // the decision name the pylon it chose, so a DAMAGES_ZEPPELIN round cannot be launched by a
    // cursor that disagrees with the gates that cleared it.
    private void DriveAiRocketeer(AiRocketeer rocketeer, AiGunner gunner, float dt)
    {
        rocketeer.Tick(dt);
        if (_fire == null || Projectiles == null || Loadout is not { Hardpoints.Count: > 0 })
            return;
        if (!TryTargetGeometry(StandingTarget(gunner), out var targetPos, out var targetVel,
                out var targetFwd, out bool targetLive) || !targetLive)
            return;
        // ⚠ Only Pursue shoots, the same deliberate hold the guns take. The original restricts
        // neither, so revisiting this is one change for both classes, not two.
        if (Pilot?.Machine is { } modes && modes.Mode != AiMode.Pursue)
            return;
        _pylonViews.Clear();
        for (int i = 0; i < Loadout.Hardpoints.Count; i++)
        {
            var hp = Loadout.Hardpoints[i];
            _pylonViews.Add(new RocketPylonView
            {
                Index = i,   // the list position FireControl selects by, not the 1-based pylon number
                DamagesZeppelin = hp.Weapon.DamagesZeppelin,
                Armed = hp.Armed(InfiniteAmmo),
                MountPos = hp.Pylon.GlobalPosition,
                RoundSpeed = hp.Weapon.Velocity ?? ProjectilePool.DefaultVelocity,
                RoundAccel = hp.Weapon.Acceleration ?? 0f,
                MinRangeM = hp.MinRangeM,
                MaxRangeM = hp.MaxRangeM,
                RefireSeconds = hp.RefireSeconds,
            });
        }
        // targetIsGasbag stays false: gasbag identity is unreachable from here
        // (docs/org/aiPilot.md "What CSVM ports of this"), an unmodelled gap rather than a guess.
        rocketeer.Solve(WorldPosition, WorldVelocity, _model.Attitude,
            targetPos, targetVel, targetFwd,
            targetIsGasbag: false, _pylonViews);
        if (rocketeer.SelectedPylon >= 0)
            _fire.SelectPylon(rocketeer.SelectedPylon);
        if (rocketeer.WantsFire && !_rocketeerLoggedFire)
        {
            _rocketeerLoggedFire = true; // verification breadcrumb: the ordnance gates first opened
            // pylon{Index}, never the list position the selection runs on: the launch line the
            // fire step logs names the hardpoint's OWN number, and two numbers for one pylon in
            // adjacent lines is how a reader concludes the wrong pylon fired.
            var hp = Loadout.Hardpoints[rocketeer.SelectedPylon];
            Log.Info("flight",
                $"ai rocketeer: shooter {PlayerIndex} launches at {TargetLabel(StandingTarget(gunner))} at {WorldPosition.DistanceTo(targetPos):0} m ({hp.Weapon.Id}, pylon{hp.Index})");
        }
    }

    // The D12/D36 acquisition: the decoded ranking formula over aircraft, turrets and structures
    // (BL-363's TargetVehicle/TargetTurret/TargetStruct), swept for one global minimum, same roster
    // and team gate as the aim assist. A live PrimaryTargetName is picked outright; its "player"
    // token resolves to the nearest human (C22) — both stay aircraft-only. Decode: docs/org/aiPilot.md.
    // ⚠ Deconfliction (AiTargetRanking) stays zero outside a mission.
    private object? SelectRankedTarget(AiGunner gunner, out TargetScore score, out string how)
    {
        score = default;
        how = "ranked";
        if (Projectiles == null)
            return null;
        _gunnerScan.Clear();
        Projectiles.CollectAircraft(_gunnerScan);
        Projectiles.CollectTurrets(_gunnerScan);
        if (Destructibles != null)
        {
            _gunnerScan.AddStructures(Destructibles);
        }
        int ownTeam = Team;
        float activation = Pilot?.Machine?.ActivationRange ?? 2000f; // min_ai_active_dist fallback
        var ownPos = WorldPosition;
        var ownFwd = NoseDirection;
        _rankCandidates.Clear();
        _rankSources.Clear();
        FlightController? primary = null;
        FlightController? nearestHuman = null;
        float nearestHumanDistSq = float.MaxValue;
        foreach (var c in _gunnerScan.Vehicles)
        {
            if (!c.Live || ReferenceEquals(c.Source, this))
                continue;
            if (c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam || c.Team == ownTeam)
                continue;
            if (c.Source is not FlightController fc)
                continue;
            if (gunner.PrimaryTargetName is { Length: > 0 } wanted
                && ownPos.DistanceSquaredTo(c.Position) <= activation * activation)
            {
                if (primary == null
                    && string.Equals(fc.Name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    primary = fc; // a by-NAME assignment names one aircraft: first match is it
                }
                else if (fc.IsHumanPiloted
                    && wanted.Equals(AiTargetRanking.PlayerRole, StringComparison.OrdinalIgnoreCase))
                {
                    // "player" is a role, not a name (C22); resolved ONCE per acquisition.
                    float d = ownPos.DistanceSquaredTo(c.Position);
                    if (d < nearestHumanDistSq)
                    {
                        nearestHumanDistSq = d;
                        nearestHuman = fc;
                    }
                }
            }

            // Allied gunners already on this candidate (the deconfliction input).
            int attackers = 0;
            foreach (var a in _gunnerScan.Vehicles)
            {
                if (a.Team == ownTeam && a.Source is FlightController ally
                    && !ReferenceEquals(ally, this)
                    && ReferenceEquals(ally.Pilot?.Gunner?.Target, fc))
                    attackers++;
            }

            _rankCandidates.Add(new RankedTargetCandidate
            {
                Position = c.Position,
                Forward = fc.NoseDirection,
                IsPlayer = fc.IsHumanPiloted,
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(
                    fc.IsHumanPiloted ? AiTargetRanking.PlayerRole : fc.Name, gunner.RatingBiases),
                AlliedAttackers = attackers,
            });
            _rankSources.Add(fc);
        }

        // Turrets and structures: BL-363's other two pools, neither with a primary_target/facing
        // term (docs/org/aiPilot.md). A structure reaches the gate on its pool's authored team, so
        // an unauthored one is neutral and never ranked at all.
        AddRankedNonAircraft(_gunnerScan.Turrets, isTurret: true, ownTeam, gunner);
        AddRankedNonAircraft(_gunnerScan.Structures, isTurret: false, ownTeam, gunner);

        bool byRole = primary == null && nearestHuman != null;
        primary ??= nearestHuman;
        if (primary != null)
        {
            // Log the assigned pick with its own rank inputs (informational — rank not consulted).
            int idx = _rankSources.IndexOf(primary);
            if (idx >= 0)
                score = AiTargetRanking.Score(ownPos, ownFwd, activation, _rankCandidates[idx]);
            how = byRole ? "primary target: nearest human" : "primary target";
            return primary;
        }

        int best = AiTargetRanking.SelectBest(ownPos, ownFwd, activation, _rankCandidates, out score);
        return best >= 0 ? _rankSources[best] : null;
    }

    // Files one turret or structure candidate into the shared rank pool, mirroring the vehicle
    // loop's team gate and allied-attacker count above (BL-363's TargetTurret/TargetStruct).
    private void AddRankedNonAircraft(List<AimCandidate> pool, bool isTurret, int ownTeam,
        AiGunner gunner)
    {
        foreach (var c in pool)
        {
            if (!c.Live || c.Source == null || ReferenceEquals(c.Source, this))
                continue;
            if (c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam
                || c.Team == ownTeam)
                continue;

            int attackers = 0;
            foreach (var a in _gunnerScan.Vehicles)
            {
                if (a.Team == ownTeam && a.Source is FlightController ally
                    && !ReferenceEquals(ally, this)
                    && ReferenceEquals(ally.Pilot?.Gunner?.GroundTarget, c.Source))
                    attackers++;
            }

            _rankCandidates.Add(new RankedTargetCandidate
            {
                Position = c.Position,
                Forward = Vector3.Zero,
                IsPlayer = false,
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(
                    TargetPool.NameOf(c.Source), TargetPool.OwnerOf(c.Source),
                    gunner.RatingBiases, isTurret),
                AlliedAttackers = attackers,
            });
            _rankSources.Add(c.Source);
        }
    }

    // Internal rather than private: PilotInputSource/KeyboardInputSource (IFlightInputSource.cs)
    // call this and its sibling above to keep each body exactly where it always lived among the
    // other sim-step helpers, rather than hoisting it for SA1202's sake.
#pragma warning disable SA1202
    internal FlightInput ReadKeyboard(float dt)
    {
        // this player's gamepad(s) fly the plane (see PadPressed/PadAxis);
        // arcade-flight standard: stick back (+Y) = nose up, stick right = bank right
        float padPitch = StickCurve(PadAxis(JoyAxis.LeftY));
        float padRoll = -StickCurve(PadAxis(JoyAxis.LeftX));
        float padYaw = (PadPressed(JoyButton.LeftShoulder) ? 1f : 0f)
                     - (PadPressed(JoyButton.RightShoulder) ? 1f : 0f);
        float padThrottle = PadAxis(JoyAxis.TriggerRight)
                          - PadAxis(JoyAxis.TriggerLeft);
        if (PadPressed(JoyButton.Y))
            Respawn();

        if (KeyDown(Key.R))
            Respawn();

        _throttle = Mathf.Clamp(
            _throttle + (KeyAxis(Key.Shift, Key.Ctrl) + padThrottle) * ThrottleRate * dt, 0f, 1f);

        // pull = S/Down, push = W/Up; bank/yaw left = A/Left/Q
        _keyPitch = StickRamp.Step(
            _keyPitch, Mathf.Sign(KeyAxis(Key.S, Key.W) + KeyAxis(Key.Down, Key.Up)), dt);
        _keyRoll = StickRamp.Step(
            _keyRoll, Mathf.Sign(KeyAxis(Key.A, Key.D) + KeyAxis(Key.Left, Key.Right)), dt);
        _keyYaw = StickRamp.Step(_keyYaw, KeyAxis(Key.Q, Key.E), dt);

        return new FlightInput
        {
            Pitch = Mathf.Clamp(_keyPitch + padPitch, -1f, 1f),
            Roll = Mathf.Clamp(_keyRoll + padRoll, -1f, 1f),
            Yaw = Mathf.Clamp(_keyYaw + padYaw, -1f, 1f),
            Throttle = _throttle,
        };
    }
#pragma warning restore SA1202

    // This plane's state entering a contact, as the resolver's per-call half.
    private ContactConditions Striking() => new()
    {
        IsHumanPiloted = IsHumanPiloted,
        VelocityDir = _model.VelocityDir,
        Speed = _model.Speed,
        Pose = GlobalTransform,
        Stats = Stats,
        Ledger = Damage,
        Parts = Collider?.Parts,
        ExcludeSelf = Body?.ExcludeSelf,
    };

    // The outcome's instructions, which only this side can perform: the struck aeroplane's share
    // of the pair, the pilot's impact line, and the push out of whatever the graze embedded this
    // airframe in. One value, so forgetting it is forgetting one call rather than four.
    private void PerformContact(in ContactOutcome outcome, in ContactReport contact, Node? hitBody)
    {
        // The node the report deliberately does not carry, read here for the applying alone.
        if (outcome.DamageStruckAircraft && (hitBody as AircraftBody)?.Rig is { } struckRig)
        {
            struckRig.TakeCollisionHit(outcome.ArmorDamage, outcome.HealthDamage, contact.Impact, PlayerIndex);
            // Both parties go collision-free, so neither re-resolves the overlap they are still in.
            _lifecycle.ArmCollisionGrace();
            struckRig.ArmCollisionGrace();
        }

        // The block-5 kick. Crashed rides the same gate as the impulse: 0x48d3aa jumps the whole
        // shake-and-decal branch when the player's crashed flag (obj+0x384) is set.
        if (outcome.ShakeMagnitude > 0f && !Crashed)
            Shake?.ContactHit(outcome.ShakeMagnitude);
        if (outcome.DamageFlashText is { } flash)
            _pilotHud.Flash(flash);
        _model.Position += outcome.PushOut;
    }

    // One round passed close. The accumulator decides whether it is heard: intensity
    // accrues here and the cue re-triggers no faster than the shipped interval, so a burst walking
    // past the canopy is one warning, not thirty.
    private void OnNearMiss(float distance)
    {
        if (Crashed || _warningShots == null || !_warningShots.Register())
            return;
        string? variant = Audio?.OnWarningShot();
        // The breadcrumb the cue otherwise leaves only in the speakers: which pilot, how close, and
        // which of the three pass samples drew — an at-the-controls report is judgeable from it.
        Log.Info("weapons", $"near miss P{PlayerIndex + 1} at {distance:0.0} m intensity={_warningShots.Intensity:0.00} snd={variant ?? "none"}");
    }

    // The survivable scrape's authored per-surface reaction, selected exactly as
    // Crash selects its own, indexing TouchdownDefs by surface id. A
    // null def plays nothing. One reaction per GrazeReactionInterval, not per frame.
    // `graze.siteAtContact` (default true) picks contact point over aircraft.
    // ⚠ Effects keep ONE live instance per def session-wide, as every PlayEffectAt caller does.
    private void GrazeReaction(Vector3 impact, string hitName, Node? hitBody)
    {
        if (_grazeReactionCooldown > 0f)
            return;
        _grazeReactionCooldown = GrazeReactionInterval;
        int? surfaceId = SurfaceIdOf(hitBody);
        string? effect = TouchdownDefs?.DefForSurfaceId(surfaceId);
        var site = Config.GetBool("graze.siteAtContact", true) ? impact : _model.Position;
        if (effect != null)
        {
            GrazeEffectSink?.Invoke(effect, site);
            // The bark belongs to the def, exactly as the crash boom does: touchdown_water Sounds
            // snd_exp_water_b, the other two snd_exp_ground_b. No def, no sound.
            Audio?.OnGraze(effect == EffectCatalogue.TouchdownDefPrefix + "water");
        }
        string surface = surfaceId is { } sid
            ? $"{sid}/{SurfaceRegistry.NameForId(sid) ?? "?"}"
            : "none";
        Log.Info("flight", $"graze reaction effect={effect ?? "-"} surface={surface} into={hitName} contact=({impact.X:0},{impact.Y:0},{impact.Z:0}) site=({site.X:0},{site.Y:0},{site.Z:0}) rendered={(GrazeEffectSink != null ? 1 : 0)}");
    }

    // Sweeps each airframe box along this frame's motion against every solid collider —
    // world plus other aircraft's bodies (a mid-air resolves through the contact rules like any
    // other hit), own body excluded by RID. Fills the earliest hit's report. False when
    // uncollidable or nothing is in the way. `hitBody` is the caller's, not the report's: the
    // struck node stays out of the value the decision side reads.
    private bool SweepAirframe(Vector3 from, Vector3 motion, out ContactReport contact,
        out Node? hitBody)
    {
        contact = default;
        hitBody = null;
        if (Collider == null)
            return false;
        var baseXf = new Transform3D(_model.Attitude, from);
        if (!World.Sweep(Collider.Parts, baseXf, motion, CollisionLayers.WorldAndAircraft,
            Body?.ExcludeSelf, out var report))
            return false;
        hitBody = report.Collider;
        contact = new ContactReport
        {
            Impact = report.Contact,
            Normal = report.Normal,
            Part = report.Part,
            ColliderName = report.ColliderName,
            StopFraction = report.StopFraction,
            StruckIsAircraft = (report.Collider as AircraftBody)?.Rig != null,
        };
        return true;
    }

    // The anti-tunnelling backstop, filling the same report off the centre ray alone: no box
    // reached the obstacle but the swept centre did. It has no struck box and no surface normal
    // of its own, so the part reads `center` and the normal is the reversed motion, which makes
    // the contact head-on; the stop fraction stays 1 because a ray reports where it hit, not
    // where the airframe would have come to rest.
    private bool CenterRayContact(Vector3 from, Vector3 to, Vector3 motion, float motionLen,
        out ContactReport contact, out Node? hitBody)
    {
        contact = default;
        if (!HitWorld(from, to, out var point, out var hitName, out hitBody))
            return false;
        contact = new ContactReport
        {
            Impact = point,
            Normal = motionLen > 1e-4f ? -motion / motionLen : Vector3.Up,
            Part = "center",
            ColliderName = hitName,
            StopFraction = 1f,
            StruckIsAircraft = (hitBody as AircraftBody)?.Rig != null,
        };
        return true;
    }

    // Debug view of the collision test: the swept center ray with a cross at
    // its tip, plus the airframe boxes drawn at where this frame's sweep stopped.
    // Freezes red at the impact pose while crashed.
    private void DrawProbe(Vector3 from, Vector3 end, Vector3 shapePos, bool hit)
    {
        var color = hit ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f);
        _probe!.ClearSurfaces();
        _probe.SurfaceBegin(Mesh.PrimitiveType.Lines);
        _probe.SurfaceSetColor(color);
        _probe.SurfaceAddVertex(from);
        _probe.SurfaceAddVertex(end);
        const float s = 1.5f;
        foreach (var axis in stackalloc[] { Vector3.Right, Vector3.Up, Vector3.Back })
        {
            _probe.SurfaceAddVertex(end - axis * s);
            _probe.SurfaceAddVertex(end + axis * s);
        }
        if (Collider != null)
        {
            var baseXf = new Transform3D(_model.Attitude, shapePos);
            foreach (var p in Collider.Parts)
                AddBoxEdges(baseXf * p.Local, p.Shape.Size * 0.5f);
        }
        _probe.SurfaceEnd();
    }

    // Adds the 12 wireframe edges of a box (half-extents h) to the probe mesh.
    private void AddBoxEdges(Transform3D xf, Vector3 h)
    {
        Span<Vector3> c = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            c[i] = xf * new Vector3((i & 1) == 0 ? -h.X : h.X,
                                    (i & 2) == 0 ? -h.Y : h.Y,
                                    (i & 4) == 0 ? -h.Z : h.Z);
        ReadOnlySpan<int> edges = stackalloc int[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, // along X
            0, 2, 1, 3, 4, 6, 5, 7, // along Y
            0, 4, 1, 5, 2, 6, 3, 7, // along Z
        };
        for (int i = 0; i < edges.Length; i += 2)
        {
            _probe!.SurfaceAddVertex(c[edges[i]]);
            _probe.SurfaceAddVertex(c[edges[i + 1]]);
        }
    }

    /// <summary>Places the camera at its settled pose immediately (spawn, respawn, the weapon
    /// lab's re-park) — there is nothing to interpolate from at those moments.</summary>
    // Silent no-op while the lab's free camera owns the view: a respawn or a lab re-park must
    // not yank the eye back onto the plane the tester just flew away from.
    private void SnapCamera()
    {
        if (!CameraOwned)
        {
            _cam?.Snap(_model.Position, _model.Attitude, _model.Speed, _renderPose);
        }
    }

    // The paused orbit camera's three axes, mixed from this player's keyboard and pads.
    // Read here rather than in CameraController so the camera never learns about
    // pad devices, window focus or the stick response curve.
    private (float Yaw, float Pitch, float Zoom) OrbitInput()
    {
        float padYaw = StickCurve(PadAxis(JoyAxis.LeftX));
        float padPitch = -StickCurve(PadAxis(JoyAxis.LeftY)); // stick up = camera up
        float padZoom = PadAxis(JoyAxis.TriggerRight)
                      - PadAxis(JoyAxis.TriggerLeft);         // RT out, LT in
        return (KeyAxis(Key.D, Key.A) + KeyAxis(Key.Right, Key.Left) + padYaw,
                KeyAxis(Key.W, Key.S) + KeyAxis(Key.Up, Key.Down) + padPitch,
                KeyAxis(Key.KpSubtract, Key.KpAdd) + padZoom); // Kp- out, Kp+ in, RT out, LT in
    }

    // E42's pad look-around stick: this player's right stick, curved the same
    // way OrbitInput's is. Read here, not in CameraController, for the
    // same reason `OrbitInput` is — the camera never learns about pad devices or the stick
    // response curve. Both components read exactly 0 inside the deadzone, which is what tells the
    // caller the look-around is inactive.
    private (float X, float Y) PadLookInput() =>
        (StickCurve(PadAxis(JoyAxis.RightX)), StickCurve(PadAxis(JoyAxis.RightY)));

    // One frame of head-look input, in HeadLook's own conventions. Read here for the same reason
    // the look-around stick is: the camera never learns about pads, mice or key layouts.
    private HeadLookInput HeadLookRead()
    {
        var (snapX, snapY) = SnapLookInput();
        var (freeRight, freeUp) = FreeLookRead();
        return new HeadLookInput(snapX, snapY, freeRight, freeUp, KeyDown(SnapCenterKey));
    }

    // C22's IdleAim delegate: HeadLook.Step calls this only on a frame with no look input at all.
    // Gated on ViewMode (Cockpit only — the original's option byte AND mode ≠ 7) and the options
    // toggle here, mirroring the original's engine option byte; the magnitude/negligible-velocity
    // gate lives in HeadLook.AutoheadTarget itself.
    private (float Elevation, float Azimuth)? AutoheadTarget()
    {
        if (_cam == null || _cam.ViewMode != PilotViewMode.Cockpit
            || !Config.GetBool("headLook.autohead", false))
        {
            return null;
        }
        Vector3 localVelocity = _model.Attitude.Inverse() * (_model.VelocityDir * _model.Speed);
        return HeadLook.AutoheadTarget(localVelocity, _model.Stats.AutoheadTurnTime,
            _model.Stats.AutoheadTurnMax, _model.Stats.AutoheadTurnMinPitch);
    }

    // The snap cluster as a composed direction, the original's own numpad bindings: Kp8 Look Up,
    // Kp4/Kp6 the flanks, Kp2 Look Back, the corners the four diagonals. Only read in a
    // first-person mode, where the numpad drives no fixed view (PilotView.HoldsFixedViews).
    private (float X, float Y) SnapLookInput()
    {
        float x = (KeyDown(Key.Kp9) || KeyDown(Key.Kp6) || KeyDown(Key.Kp3) ? 1f : 0f)
                - (KeyDown(Key.Kp7) || KeyDown(Key.Kp4) || KeyDown(Key.Kp1) ? 1f : 0f);
        float y = (KeyDown(Key.Kp7) || KeyDown(Key.Kp8) || KeyDown(Key.Kp9) ? 1f : 0f)
                - (KeyDown(Key.Kp1) || KeyDown(Key.Kp2) || KeyDown(Key.Kp3) ? 1f : 0f);
        return (x, y);
    }

    // Free-look direction: this player's right stick, or the mouse while its right button is held
    // (the RMB-to-look posture the freecam already uses). Only the DIRECTION is read, so mouse
    // pixels and a curved stick axis mix freely and neither needs its own sensitivity.
    private (float Right, float Up) FreeLookRead()
    {
        var mouse = MouseLookDelta();
        float right = StickCurve(PadAxis(JoyAxis.RightX));
        float up = -StickCurve(PadAxis(JoyAxis.RightY));   // stick up = look up
        if (right != 0f || up != 0f)
        {
            return (right, up);
        }
        return (mouse.X, -mouse.Y);                        // screen Y grows downward
    }

    // How far the mouse moved since the last read, or zero unless this player's right button is
    // held. Polled rather than event-driven, like every other control here; the previous position
    // is refreshed on every call, so an idle mouse reads exactly zero.
    private Vector2 MouseLookDelta()
    {
        var pos = (Vector2)DisplayServer.MouseGetPosition();
        var delta = pos - _mouseLookPrev;
        _mouseLookPrev = pos;
        bool looking = UseKeyboard && Input.IsMouseButtonPressed(MouseButton.Right);
        return looking && delta.LengthSquared() > 1f ? delta : Vector2.Zero;
    }

    // This plane's half of a contact the resolver is deciding: the engine effects it has to
    // interleave with, plus the struck Node the report deliberately does not carry. One instance
    // per contact, so nothing about a contact outlives the call that decided it.
    private sealed class ContactEffects : IContactEffects
    {
        private readonly FlightController _rig;
        private readonly Node? _struck;
        private readonly ContactReport _contact;
        private readonly Vector3 _from;
        private readonly Vector3 _motion;

        public ContactEffects(FlightController rig, Node? struck, in ContactReport contact,
            Vector3 from, Vector3 motion)
        {
            _rig = rig;
            _struck = struck;
            _contact = contact;
            _from = from;
            _motion = motion;
        }

        public bool ShatterStruck(float healthDamage) =>
            _rig.CollideDamageSink != null && _rig.CollideDamageSink(_struck, healthDamage);

        public void PlayGrazeReaction() =>
            _rig.GrazeReaction(_contact.Impact, _contact.ColliderName, _struck);

        public PlaneDamage.PartState? SpendDamage(string zone, float healthDamage, float armorDamage)
        {
            var state = _rig.Damage!.Apply(zone, healthDamage, armorDamage);
            string struckPart = state?.Def.Name ?? zone;
            if (state != null)
            {
                _rig.Visuals?.OnPartDamage(struckPart, state.HealthFraction);
                _rig._pilotHud.OnPartDamage(struckPart); // damage dial: hit zone blinks 5 s
            }

            // the def-level stages run off the hull pool even when the graze went zone-less
            _rig.Visuals?.OnHullDamage(_rig.Damage.SummaryHealthFraction);
            return state;
        }

        // Placement and the decoded impulse, on the plant whose fields they write. An airframe
        // already crashed is off the player path, so its gate rides the impulse's argument.
        public ContactResponse ApplyResponse()
        {
            _rig._model.Collide(_from, _motion, _contact.StopFraction, _contact.Impact, _contact.Normal,
                _rig.IsHumanPiloted && !_rig.Crashed);
            return new ContactResponse(new Transform3D(_rig._model.Attitude, _rig._model.Position));
        }
    }
}
