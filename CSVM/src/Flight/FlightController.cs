using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The flying aircraft: polls keyboard + gamepad into a <see cref="FlightModel"/>,
/// applies the result to this node's transform (the plane model is a child), and
/// drives the chase camera plus a minimal text HUD.
///
/// Keyboard: W/S or Up/Down pitch (W = push), A/D or Left/Right roll, Q/E rudder,
/// Shift/Ctrl throttle, R respawn, P pause.
/// Gamepad: left stick pitch/roll (back = nose up), LB/RB rudder, RT/LT throttle
/// up/down, Y respawn, Start pause.
///
/// Hitting terrain or a building crashes the plane: explosion sound, airframe
/// hidden, frozen at the impact point until R (or gamepad Y/A) respawns.
///
/// P (gamepad Start) is a debug freeze: the whole simulation halts in place —
/// physics, input, audio and props all hold — so screenshots can be taken from a
/// fixed position across frames. While paused the chase camera is replaced by a
/// free orbit around the frozen plane (WASD/arrows orbit, Shift/Ctrl zoom, or the
/// gamepad left stick + triggers) so you can circle it and shoot any angle.
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

    /// <summary>When set, replaces keyboard input — used by automated screenshot runs.
    /// Each segment holds its input for its duration (seconds of sim time); the last
    /// segment holds forever, and a respawn restarts the sequence (deterministic runs).
    /// Such runs are unattended, so a crash auto-respawns after a short pause.</summary>
    public (FlightInput Input, float Duration)[]? HoldSegments;

    /// <summary>Own-plane sound, if the sound archive was found (add as a child too).</summary>
    public FlightAudio? Audio;

    /// <summary>The visible aircraft model (a child of this node); hidden while crashed.</summary>
    public Node3D? PlaneModel;

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

    /// <summary>The original's heading tape at the top of the screen (added to the
    /// HUD canvas, fed the heading each frame). Null if the chapter's texture
    /// archive lacks the compass textures.</summary>
    public CompassTape? Compass;

    /// <summary>The original's cockpit dials (altimeter / speedometer / damage
    /// display) rebuilt from the plane's own gauges subtree; added to the HUD canvas
    /// and fed altitude/AGL/speed/stall + part-damage events. Optional.</summary>
    public GaugeCluster? Gauges;

    /// <summary>The <c>--hud-font-test</c> bitmap-font verification overlay: added to the HUD
    /// canvas so it scales with the pane. Null unless the flag is set.</summary>
    public HudFontTest? FontTest;

    /// <summary>The selected-weapon text readout: the gun group + rocket type and their live
    /// ammo, drawn in the game's HUD font from the <c>MSG_HUD_GUNGAUGE</c>/<c>MSG_HUD_MISSLES</c>
    /// templates. Added to the HUD canvas and fed each frame; null (no font / no loadout) hides it.</summary>
    public WeaponReadout? WeaponReadout;

    /// <summary>The gun aiming reticle: the game's pipper drawn at the SELECTED gun group's
    /// ballistic impact point at the convergence distance — trailing the nose in a hard turn, on the
    /// rounds in steady flight. Added to the HUD canvas and fed the world impact point each frame;
    /// null when the plane carries no firable gun (or the reticle texture was absent).</summary>
    public ImpactReticle? Reticle;

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

    /// <summary>Applies a plane collision to the struck world node, returning true iff it was a
    /// <c>WeaponOrCollideHit</c> destructible (the 44 facades/windows/agyrobus) — in which case the
    /// object breaks and the plane flies THROUGH it. Wired to <c>AnimRuntime.CollideDamageAt</c>; null
    /// (a viewer/static build with no world runtime) makes every collision solid.</summary>
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

    /// <summary>--infinite-ammo: guns/hardpoints fire without depleting (frictionless testing).</summary>
    public bool InfiniteAmmo;

    /// <summary>--ammo=N: overrides both <c>weapons.gunAmmoCap</c> and <c>weapons.ordnanceCap</c> at
    /// rig build, so the low-ammo start survives <c>--det</c> (which drops config.json entirely).
    /// Null when the flag was absent — falls through to the config.json knobs.</summary>
    public int? AmmoCapOverride;

    /// <summary>--fire: hold the gun trigger down (scripted screenshot / soak runs), as
    /// <see cref="HoldSegments"/> does for flight input.</summary>
    public bool AutoFire;

    /// <summary>--fire-rockets: hold the rocket trigger down (scripted screenshot / soak runs).
    /// Unlike a human pull (one rocket per press), this auto-repeats at the launch cooldown.</summary>
    public bool AutoFireRockets;

    /// <summary>--gun-select=N: the gun selector's initial firable group (0-based; 0 = the first
    /// group, the default). Only one gun group fires at a time. A headless testing hook so a scripted
    /// run can fire one group in isolation; interactively the selector cycles with G / gamepad D-pad Left.</summary>
    public int InitialGunSelect;

    /// <summary>The data-driven crash: a per-player
    /// <see cref="AnimRuntime"/> bound to this plane's scoped crash subtree (the plane model's
    /// <c>healthy</c>, the built <c>destroyed</c> wreck, and the effect templates) that PLAYS the
    /// compiled crash definition the struck surface selects — the airframe hides, and on
    /// <c>player_crash_default</c>/<c>_dirt</c> the wreck breaks apart with the <c>pieceN</c>
    /// ballistics, sparks, fireball cluster and burning-debris arcs all firing from the extracted
    /// data (<c>_dirt</c> adding the dirt burst, black smokeball and 10 s fire the fallback def
    /// does not author); on <c>player_crash_water</c> the splash, ripple and steam spray do instead.
    /// It advances itself (its
    /// own <c>_Process</c>). The standard crash path (built by <c>GameSession</c> for every flown
    /// plane); null only when the crash program/scene were unavailable, and the plane then just
    /// hides on a crash.</summary>
    public AnimRuntime? CrashRuntime;

    /// <summary>The crash-def vector the struck surface id indexes — the original's own selection
    /// mechanism (see <see cref="SurfaceDefTable"/>). Built from the bound crash program, so it
    /// knows which slots name a def this install actually ships. Set alongside
    /// <see cref="CrashRuntime"/>; null when no crash rig was built, and the plane then just hides
    /// on a crash.</summary>
    public SurfaceDefTable? CrashDefs;

    /// <summary>The def the last <see cref="Crash"/> selected off <see cref="CrashDefs"/> —
    /// <c>player_crash_*</c> on a human rig, <c>ai_crash_*</c> on an AI plane, null before any
    /// crash or when no crash rig was built. The prefix names the family, so a suite (or a log
    /// reader — the CRASH line prints the same value as <c>def=</c>) can pin which family
    /// fired. Written only by <see cref="Crash"/>.</summary>
    public string? LastCrashDef;

    /// <summary>The node the crash definition anchors to (its <c>player</c> anim-root) — passed to
    /// <see cref="AnimRuntime.Play"/> on a crash. Set alongside <see cref="CrashRuntime"/>.</summary>
    public Node3D? CrashAnchor;

    /// <summary>The rest pose of every node the crash def flings (the <c>destroyed</c> wreck's
    /// pieceN meshes), captured before the first crash so <see cref="Respawn"/> can re-home them —
    /// a RESET_STATE re-poses only nodes it names, and the pieces have no reset event. Set
    /// alongside <see cref="CrashRuntime"/>.</summary>
    public IReadOnlyList<(Node3D Node, Transform3D RestPose)>? CrashRestPoses;

    /// <summary>The BUILT visibility of every node in the plane model, captured before the first
    /// crash so <see cref="Respawn"/> can undo what the crash def hid. The def deactivates
    /// <c>healthy</c>/<c>dontmove</c>/<c>markers</c> (each node's own <c>Visible</c>), and its
    /// RESET_STATE only restores <c>dontmove</c> — so <c>PlaneModel.Visible=true</c> alone re-shows
    /// the root while the airframe stays hidden (the original respawns a fresh plane; we reuse this
    /// one). Restoring this snapshot puts every part back to its built state (and keeps the
    /// built-hidden torn panels / wingtip flares hidden). Set alongside <see cref="CrashRuntime"/>.</summary>
    public IReadOnlyList<(Node3D Node, bool Visible)>? CrashPlaneVisibility;

    /// <summary>The stunt run, when flying --stunt: danger-zone sphere
    /// detection, tested against the plane each physics frame. Deliberately NOT reset on
    /// respawn — a mid-run crash keeps completed zones (the clock keeps running).
    /// Null in free flight.</summary>
    public StuntMission? Stunt;

    /// <summary>The stunt objective marker HUD: the active zone's projected marker /
    /// screen-edge arrow + clock bearing, the run-status line, intro/complete banners. Added to
    /// the HUD canvas, fed the plane pose each frame; the camera + mission are bound at Build.
    /// Null in free flight (and when --stunt found no danger zones).</summary>
    public MarkerHud? Marker;

    /// <summary>The end-of-run results overlay: splits + total + best-time on
    /// AllComplete. Added to the HUD canvas last (drawn over the marker/dials); wakes itself on
    /// the run's RunCompleted. Null in free flight.</summary>
    public StuntScoreboard? Scoreboard;

    /// <summary>The Dogfight per-pane HUD: the match timer/K-D/leader line and
    /// the kill banner. Added to the HUD canvas; fed nothing per frame (it pulls VersusMatch's own
    /// live state) beyond the kill facts GameSession pushes through its OnKill. Null outside
    /// <c>--vs</c>.</summary>
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

    /// <summary>0-based player index — this plane's seat in the race and its pane, and the
    /// identity a round it fired carries (<c>ProjectilePool.Spawn</c>'s shooter id).</summary>
    public int PlayerIndex;

    /// <summary>Whether a person is flying this plane. Gates the gun aim assist
    /// (<c>BL-342</c>/B6): true runs <see cref="AimAssist"/> as normal, false takes the muzzle axis
    /// unassisted, the same fallback a barrel with no slot already uses. Defaults true; the AI
    /// spawner sets it false. It is the original's human-versus-AI split (`FUN_004b6530`'s
    /// else-branch), not "pane 1 only" — see Decision 7 in
    /// `docs/plans/PLAN-sticky-bullets.md`.</summary>
    public bool IsHumanPiloted = true;

    /// <summary>This plane's carried turret gunners (C9a): built by the rig assembler from the
    /// vehicle def's <c>turrets</c> block against <c>ai.zrd</c>, ticked from <see cref="SimStep"/>
    /// (so a crash silences them), and collected into every shooter's aim-assist candidate set
    /// through <see cref="ProjectilePool.CollectTurrets"/>. Empty on the six turretless airframes.</summary>
    public TurretController[] Turrets = Array.Empty<TurretController>();

    /// <summary>The non-player input source: set (with <see cref="IsHumanPiloted"/> false), it
    /// replaces the keyboard/pad read each sim step, the way <see cref="HoldSegments"/> does for
    /// scripted runs — everything downstream of the input (flight model, collision, weapons,
    /// damage, crash) is byte-for-byte the player's path. Its orders are mutable between steps;
    /// see <see cref="AiPilot"/>.</summary>
    public AiPilot? Pilot;

    /// <summary>The world's destructibles, when this session has a world runtime — the aim assist's
    /// third candidate list (`BL-342`, an approximation of the original's `targets.zrd`
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

    /// <summary>Whether P / gamepad-Start toggles the debug screenshot freeze. Off in splitscreen:
    /// the freeze halts the shared simulation, so it is not one player's to press.</summary>
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

    /// <summary>Seconds a crash sits on the crash cam before this plane auto-respawns, or null —
    /// the default — for manual R only. The session arms it (Versus: 3 s, every rig) so a downed
    /// player rejoins the fight without touching a key; R still respawns early, and the timer is
    /// armed at <see cref="Crash"/>. Scripted HoldSegments runs auto-respawn regardless, on
    /// <see cref="AutoRespawnDelay"/> unless this says otherwise.</summary>
    public float? AutoRespawnAfter;

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float SpawnThrottle = 0.5f;   // the original always spawns at half throttle (confirmed in-game, all planes)
    private const float SpawnSpeed = 53.6f;     // m/s ≈ 120 mph. PLACEHOLDER: the original's spawn speed is
                                                // plane-dependent (TODO — kept fixed for now per user); the
                                                // plane accelerates from here toward its cruise
                                                // The gun pipper's own rule, decoded out of crimson.exe for B5 (docs/org/aim-assist.md "What
                                                // the pipper follows", FUN_00426570 / FUN_004267f0) — no TUNE left in it. The sprite marks
                                                // where a round fired NOW would be after ReticleFlightTime seconds, so its range is the
                                                // weapon's own VELOCITY halved (430 m for the 860 m/s default), and its distance from the
                                                // muzzle is rate-smoothed rather than snapping when the selected group changes.
    private const float ReticleFlightTime = 0.5f;      // s of flight the pipper marks
    private const float ReticleDefaultSpeed = 860f;    // m/s used when no weapon def resolves
    private const float ReticleAccel = 894.07996f;     // m/s² the smoother's rate builds at
    private const float ReticleRatePerGap = 1.9848576f; // rate ceiling per metre of remaining gap
    private const float ReticleFarGap = 900f;          // m past which the ceiling is flat
    private const float ReticleFarRate = 1788.1599f;   // m/s that flat ceiling
    private const float UnderMapY = 0f;        // C1 terrain sits at y≈100+; below this we're lost
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)
    private const float AutoRespawnDelay = 1.5f; // s a HoldInput run stays crashed before auto-respawn
    private const float DebugFinishStagger = 1.5f; // s between players' forced finishes (--debug-scoreboard in a race)

    // Collision severity (all TUNE): impact speed along the contact
    // normal decides between a survivable graze and a crash. A graze damages the
    // struck part (quadratic in severity), slides the velocity along the surface
    // with some tangential loss, and kicks the attitude.
    private const float CrashSpeed = 25f;        // m/s along the normal ⇒ outright crash
    private const float CollideDamagePerVn = 8f;  // HEALTH_DAMAGE a collision deals to a WeaponOrCollideHit
                                                  // object, per m/s of impact severity — a real flight-speed
                                                  // hit (vn≥~9) breaks even agyrobus (health 70); the 43
                                                  // 0.01-health facades/windows shatter at any motion.
    private const float WreckMomentum = 0.4f;    // TUNE: fraction of impact velocity the crash wreck pieces inherit
    private const float GrazeMaxDamage = 18f;    // HP at a just-under-crash graze (parts have 20–25)
    private const float GrazeFriction = 0.35f;   // tangential speed kill at full severity
    private const float GrazeKick = 1.2f;        // rad/s attitude kick at full severity
    private const float GrazePushOut = 0.15f;    // m off the surface after a graze (no sticky slide)
    private const float DamageCooldown = 0.3f;   // s between HP subtractions (multi-frame scrapes)
    private const float GrazeReactionInterval = 1.5f; // s between graze reactions — NOT a tuned value:
                                                      // the touchdown defs stop their own puffer at
                                                      // ANIMATION_OFFSET 1.5, so this is one whole authored
                                                      // reaction per scrape rather than a restart per frame
    private const float DamageFlashTime = 2.5f;  // s the HUD shows the impact line
    private const float GrazeStopSpeed = 12f;    // m/s — grinding to (near) standstill on the
                                                 // ground explodes the plane (user-reported:
                                                 // a stopped plane sat there collecting 0-dmg kisses)
    private const float EmbedPushOut = 0.3f;     // m per un-embed attempt after a graze
    private const int EmbedTries = 3;            // attempts before giving up ⇒ explode, never tunnel
    private const int HudFontSize = 22;         // text HUD, full-screen (shrunk per splitscreen pane)

    private const float PropIdleSpin = 0.4f;    // blur discs still turn at zero throttle (windmilling)

    private static readonly Vector2 HudMargin = new(16, 10);

    private readonly List<float> _gunGaugeSlots = new();
    private readonly List<float> _missileGaugeSlots = new();
    private readonly AimCandidateSet _aimCandidates = new(); // rebuilt once per fire call (B4/B5)
    private readonly AimCandidateSet _gunnerScan = new();    // the AI gunner's acquisition scan (D14)
    private readonly List<RankedTargetCandidate> _rankCandidates = new(); // the D12 ranking snapshots
    private readonly List<FlightController> _rankSources = new();         // …and their controllers, by index
    private readonly RandomNumberGenerator _aimRng = Rng.Stream(Rng.Weapons); // the assist's 1° launch scatter

    private FlightModel _model = null!;
    private CameraController? _cam;              // null on an AI rig — no view rides this plane
    private Camera3D? _viewCamera;
    private CanvasLayer? _hudCanvas;             // the whole HUD layer; hidden while crashed (the
                                                 // original's crash camera shows no HUD — footage);
                                                 // never built on an AI rig
    private Label? _hud;
    private float _hudPaneFactor = 1f;            // last applied splitscreen shrink (1 = single player)
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;
    private bool _crashed;                       // frozen at the impact point, waiting for respawn
    private WarningShotCue? _warningShots;       // the near-miss cue's shipped accumulator
    private FlightInput _lastInput;              // this physics frame's stick input (drives the surfaces)
    private float _autoRespawnIn;                // s until auto-respawn (HoldSegments runs only)
    private float _autoRestartIn = AutoRespawnDelay; // s until auto-rematch on a finished race (HoldSegments runs only)
    private float _holdElapsed;                  // sim time into the HoldSegments sequence
    private bool _pausePrev;                     // previous frame's pause-key state (edge detection)
    private bool _haltPrev;                      // previous frame's clock-halt state (orbit seeding)
    private bool _cyclePrev;                     // previous frame's stunt cycle-target key state (edge detection)
    private ImmediateMesh? _probe;               // debug collision-probe line
    private float _damageCooldown;               // s left before the next HP subtraction
    private float _grazeReactionCooldown;        // s left before the next touchdown_* reaction
    private float _damageFlash;                  // s left on the HUD impact line
    private string _damageFlashText = "";
    private int _projectileHitsLogged;           // verification breadcrumb: the first few hits log
    private FireControl? _fire;                  // the fire-control state machine; built in _Ready with the loadout
    private GunGroup[] _firableGuns = Array.Empty<GunGroup>(); // the firable gun groups in _fire's slot order (muzzle nodes, live ammo)
    private GunAimSlot[][] _aimSlots = Array.Empty<GunAimSlot[]>(); // per _firableGuns group, one slot per muzzle — B2's assist state
    private float _reticleDist = ReticleDefaultSpeed * ReticleFlightTime; // m — the pipper's smoothed range
    private float _reticleRate;                  // m/s the pipper's range is currently closing at
    private bool _aimLoggedFirst;                // verification breadcrumb: the assist's first snap logs once
    private bool _aimListsLogged;                // verification breadcrumb: the candidate list sizes log once
    private bool _gunnerLoggedTarget;            // verification breadcrumb: the AI gunner's first acquisition
    private bool _gunnerLoggedFire;              // verification breadcrumb: the AI gunner's first open fire
    private bool _gunLoopOn;                     // the firing loop sound is currently playing
    private bool[] _gunLoggedFirst = Array.Empty<bool>(); // verification breadcrumb: each group logs its first live round once
    private int _rocketsLaunched;                // verification breadcrumb: the first few launches log their pylon
    private bool _held;                          // Held's backing field — the airframe is pinned (weapon lab)
    private bool _cameraOwned;                   // CameraOwned's backing field — the lab's free camera has the view
    private bool _orbitPrev;                     // edge detection for entering the orbit (halt or hold)
    private bool _reseedOrbit;                   // the free camera handed the view back; re-seed from where it left it
    private bool _heldPinned;                    // the pinned pose below is valid (captured on the first held step)
    private Vector3 _heldPos;                    // the pinned position, re-applied through the model every held step
    private Basis _heldAttitude;                 // the pinned attitude, ditto

    // The gungauge / missilegauge HUD state, pushed to GaugeCluster each frame. Persistent
    // objects mutated in place (the belt-fraction lists too) so the HUD readout costs no per-frame
    // allocation. Null until _Ready binds them, and only for a system the plane actually carries.
    private GaugeCluster.WeaponGauge? _gunGaugeState;
    private GaugeCluster.WeaponGauge? _missileGaugeState;

    // The sim advances on the 60 Hz physics tick while rendering runs at the display rate, so
    // drawing the raw sim pose stutters the plane against the smoothly-moving chase camera at
    // any render rate above 60 fps, in proportion to speed. SimStep records the last two sim
    // poses; _Process draws between them at the physics interpolation fraction. Realtime clock
    // only — a parent-driven (fixed-dt) clock draws the exact sim pose, keeping scripted
    // captures byte-identical.
    private Transform3D _simPrev = Transform3D.Identity;
    private Transform3D _simCurr = Transform3D.Identity;
    private Transform3D _renderPose = Transform3D.Identity; // the pose actually drawn this frame

    /// <summary>Raised exactly once per crash, at the moment of <see cref="Crash"/>: (victim
    /// <see cref="PlayerIndex"/>, killer shooter id) — the killer is the identity of the round
    /// whose critical-part kill downed this plane, null for terrain, mid-air, an unowned round
    /// (<see cref="ProjectilePool.NoShooter"/>) and every other crash cause. A fact report, not a
    /// score: this node knows no match rules — the session subscribes and scores when a match
    /// exists, guarding the killer against its own roster (a non-player shooter id like
    /// <see cref="IncomingFire.ShooterId"/> is a plain death there). Respawn emits nothing; the
    /// death was reported here.</summary>
    public event Action<int, int?>? Downed;

    /// <summary>Raised on every projectile hit that moved this plane's damage state without
    /// destroying it (the destroying hit reports through <see cref="Downed"/> instead) — the
    /// E16 voice runtime reads the whole-vehicle summary off it for the DI distress tiers and
    /// the player's WA-HighDmg crossing. Terrain grazes do not raise it; the decoded distress
    /// sites are the combat hit path's.</summary>
    public event Action<FlightController>? DamageApplied;

    /// <summary>The weapon lab's hold: the airframe holds the pose it had when this was set — it does
    /// not fly, stall, fall or collide — while everything else in the session keeps running. The
    /// props still spin, the guns still fire through the normal trigger, the rounds still fly and
    /// the world sim is untouched. Deliberately NOT the P halt (<see cref="GameClock.Halted"/>),
    /// which stops the whole clock: the point here is that the world keeps going while only this
    /// plane is pinned. Clearing it un-pins the airframe at zero speed (it will drop) and re-pins
    /// the CURRENT pose when set again. <see cref="PlaceHeld"/> moves the pin.</summary>
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

    /// <summary>Whether this plane is crashed — frozen at the impact, airframe hidden, waiting
    /// for respawn. The fact the session (and the in-engine suites) read; only Respawn clears it.</summary>
    public bool Crashed => _crashed;

    /// <summary>The flight model's world position — the plane as a SIM value, not a node transform
    /// (the node lags it by the render interpolation). What another plane's aim assist aims at.</summary>
    public Vector3 WorldPosition => _model.Position;

    /// <summary>The flight model's world velocity, m/s — the assist's intercept solve needs it, and
    /// so does the shooter's own subtraction to relative velocity.</summary>
    public Vector3 WorldVelocity => _model.VelocityDir * _model.Speed;

    /// <summary>The nose axis off the SIM attitude (the render half may hold an interpolated
    /// frame) — what the AI gunner's quick-draw cones project fore and aft from.</summary>
    public Vector3 NoseDirection => -_model.Attitude.Z;

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

    /// <summary>Wires the flight model and (for a piloted view) the chase camera, then spawns.
    /// <paramref name="camera"/> is null on an AI rig: no camera rides the plane and every camera
    /// write below is skipped — the flight half is identical either way.</summary>
    public void Setup(FlightModel model, Camera3D? camera, CamParams camParams,
        Vector3 spawnPos, Vector3 spawnLookAt)
    {
        _model = model;
        _viewCamera = camera;
        _cam = camera != null ? new CameraController(camera, camParams, KeyDown, PinnedView) : null;
        _spawnPos = spawnPos;
        _spawnAttitude = Basis.LookingAt((spawnLookAt - spawnPos).Normalized(), Vector3.Up);
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
            _hud = new Label { Position = HudMargin };
            _hud.AddThemeFontSizeOverride("font_size", HudFontSize);
            _hud.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
            _hud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
            _hud.AddThemeConstantOverride("shadow_offset_y", 2);
            canvas.Name = "hud";
            canvas.AddChild(_hud);
            if (Compass != null)
                canvas.AddChild(Compass);
            if (Gauges != null)
                canvas.AddChild(Gauges);
            if (Reticle != null)
                canvas.AddChild(Reticle); // gun aiming pipper, over the dials, under the text/marker
            if (WeaponReadout != null)
                canvas.AddChild(WeaponReadout); // selected-weapon text readout, over the dials
            if (Marker != null)
                canvas.AddChild(Marker); // stunt objective marker, drawn on top of the dials
            if (VersusHud != null)
                canvas.AddChild(VersusHud); // dogfight timer/K-D/leader line + kill banner
            if (Scoreboard != null)
                canvas.AddChild(Scoreboard); // end-of-run results, drawn over everything
            if (FontTest != null)
                canvas.AddChild(FontTest); // --hud-font-test: the bitmap-font proof overlay
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
        // The airframe's physics body: the PlaneCollider boxes as real shapes on the aircraft
        // layer, riding this node's transform as a child. Every OTHER plane's sweep and every
        // hit ray sees it; this plane's own queries pass Body.ExcludeSelf so it never collides
        // with itself. Nothing here lets the physics engine move the plane.
        if (Collider != null)
        {
            Body = new AircraftBody(this, Collider);
            AddChild(Body);
            // Strikeable by every other identity's rounds; this pilot's own rounds exclude it.
            Projectiles?.RegisterAircraft(Body);
        }
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
            // weapons.gunAmmoCap (config.json, testing) / --ammo=N: cap each firable group's load so
            // the low-ammo cases — chiefly the on-empty group hand-off — are reachable without draining
            // thousands of stock rounds. 0 = off. Capping Capacity too makes the gauge read full at
            // the cap and drain from there; RefillWeapons refills to it on every respawn. AmmoCapOverride
            // (--ammo=N) wins over config.json so the knob survives --det, which drops config.json.
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

            // Bind the two weapon gauges — only for a system this plane actually carries.
            if (Gauges != null)
            {
                if (n > 0)
                {
                    _gunGaugeState = new GaugeCluster.WeaponGauge { Slots = _gunGaugeSlots };
                    Gauges.GunGauge = _gunGaugeState;
                }
                if (Loadout.Hardpoints.Count > 0)
                {
                    _missileGaugeState = new GaugeCluster.WeaponGauge { Slots = _missileGaugeSlots };
                    Gauges.MissileGauge = _missileGaugeState;
                }
            }
        }
    }

    /// <summary>Back to the spawn pose at half throttle with a healthy, repaired airframe: the
    /// crash respawn (R), and the session's per-plane reset for a race rematch. Leaves the
    /// stunt run alone — a mid-run crash deliberately keeps its zones and clock.</summary>
    public void Respawn()
    {
        _crashed = false;
        _holdElapsed = 0f; // scripted hold sequences restart from the spawn
        _lastInput = default;
        WingLights?.Reset(); // flares off; the cycle restarts from this spawn
        Surfaces?.Reset();   // control surfaces back to neutral
        Damage?.Reset();     // every part back to full HP
        Gauges?.Reset();     // damage-dial blink timers cleared
        Visuals?.Reset();    // torn panels off, healthy twins back, smoke trail cleared
        RefillWeapons();     // full ammo, dry warnings re-armed, any live tracers cleared
        if (CrashRuntime != null)
        {
            // data-driven crash: hard-stop the played def (instances, motions, the fire +
            // every other puffer), re-hide the wreck + effect templates (their RESET_STATE), re-home
            // the flung pieces (no reset event re-poses them), and restore the plane model's built
            // visibility — the def hid healthy/markers and only the RESET_STATE's dontmove comes back,
            // so without this respawn leaves just the propeller.
            CrashRuntime.ResetToBaseState();
            if (CrashRestPoses != null)
                foreach (var (node, rest) in CrashRestPoses)
                {
                    node.Transform = rest;
                }
            if (CrashPlaneVisibility != null)
                foreach (var (node, vis) in CrashPlaneVisibility)
                {
                    node.Visible = vis;
                }
        }
        _damageCooldown = 0f;
        _grazeReactionCooldown = 0f;
        _damageFlash = 0f;
        if (_hudCanvas != null)
            _hudCanvas.Visible = true;  // the crash camera hid it (footage); flying again
        if (PlaneModel != null)
            PlaneModel.Visible = true;
        Body?.SetHittable(true);
        _throttle = SpawnThrottle;
        // The start choreography (snd_propstart already re-fires from FlightAudio's own
        // loop-restart hook): the static blade prop cross-fades to its spinning blur disc with
        // the startup smokepuffN burst. CrashRuntime does not exist yet for the very first spawn
        // (Setup calls this before FlightRigAssembler builds it) — FlightRigAssembler plays it
        // once more there for that one case.
        CrashRuntime?.Play("startprops", PlaneModel, applyReset: false);
        // A fresh engine has no in-flight plume, and the spawn throttle jump (0 → SpawnThrottle)
        // must never itself read as a slam.
        ThrottleSmoke?.Reset(_throttle);
        SpeedCue?.Reset();
        _model.Reset(_spawnPos, _spawnAttitude, SpawnSpeed, _throttle);
        _simPrev = _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;
        if (_cam != null && IsInsideTree())
            SnapCamera();
    }

    /// <summary>Weapon lab: pin the held airframe at <paramref name="pos"/> with its nose on
    /// <paramref name="lookAt"/>, at zero speed — the lab's re-park (click-to-place, and the
    /// scripted <c>--weapon-target=</c> twin). Goes in through the same
    /// <see cref="FlightModel.Reset"/> + <see cref="SnapCamera"/> pair <see cref="Respawn"/> uses,
    /// so the sim pose, the drawn pose and the chase camera all land together with nothing left to
    /// interpolate from. Sets the pin whether or not <see cref="Held"/> is on; on a free-flying
    /// plane it is simply a teleport to a standstill.</summary>
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

    /// <summary>--crash[=frame]: forces this player's crash outside any live collision — the only
    /// headless trigger for the per-player crash rig. <c>hitName</c>/<c>part</c> are nominal and
    /// there is no struck body, so the selection cascade takes its null-material arm and resolves
    /// slot 0 (<c>player_crash_default</c>) however the plane is posed — the other variants need a
    /// real impact on material carrying their surface id. A no-op once already crashed.
    /// <paramref name="killer"/> is null (a plain death) by default — <c>--crash</c> itself never
    /// passes one; <c>--debug-scoreboard --vs</c> passes a shooter id so a Dogfight screenshot has
    /// a real, attributed kill to show without scripting an actual shot.
    /// <paramref name="struckBody"/> lets a suite hand the cascade a body carrying a real
    /// <see cref="SceneBuilder.SurfaceIdMeta"/> stamp; the default null keeps the null-material
    /// arm above.</summary>
    public void DebugForceCrash(int? killer = null, Node? struckBody = null)
    {
        if (!_crashed)
            Crash(_model.Position, "debug-crash", "test", struckBody, killer);
    }

    /// <summary>One projectile hit on this plane (the pool resolved the struck box already):
    /// maps the box + plane-local impact to the data part, spends the weapon's ARMOR_DAMAGE /
    /// HEALTH_DAMAGE through <see cref="PlaneDamage.Apply"/> — the decoded flow: the zone
    /// armor-first (a dead zone redirects to a survivor), then the unabsorbed leftover against
    /// the whole-vehicle pair — and drives the same feedback a terrain graze does — part
    /// visuals, damage-dial blink, HUD flash line. Exhausted whole-vehicle health
    /// (<see cref="PlaneDamage.IsDestroyed"/>, the decoded kill rule) downs the plane through
    /// the existing <see cref="Crash"/> path, exactly as <see cref="SurviveHit"/> does. No
    /// cooldown: weapon fire is discrete, every round counts.
    /// Ignored while crashed (the body is unhittable then anyway — belt and braces) and without
    /// damage data (no destroyable_parts: nothing to track, the round just sparks).
    /// <paramref name="shooter"/> is the round's owner (<see cref="PlayerIndex"/> of who fired,
    /// <see cref="ProjectilePool.NoShooter"/> for an unowned round) — carried into
    /// <see cref="Downed"/> as the killer when the hit downs the plane.
    /// <paramref name="damageScale"/> scales both magnitudes: 1 for a direct round, the linear
    /// blast falloff share for a splash hit (the pool's aircraft blast pass).</summary>
    public void TakeProjectileHit(WeaponDef weapon, Vector3 impact, string colliderPart, int shooter,
        float damageScale = 1f)
    {
        if (_crashed)
            return;
        // The being-hit rock: a gun round reuses the measured caliber law; a rocket's armor
        // damage stands in for the unauthored quantity (he_factor doubles HE); a splash hit
        // (damageScale < 1 — the pool's blast pass) plays the explosion source instead. Runs
        // even with no damage data, so a plane nothing tracks HP for still visibly takes fire.
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
        // The decoded flow (D14 corrected 2026-08-14, docs/org/vehicleDamage.md): the resolver
        // may REDIRECT a hit on a dead zone to a surviving one, and the leftover the zone could
        // not absorb drains the whole-vehicle pair — so the struck zone is Apply's answer, not
        // the geometric guess, and the kill test runs even when the hit went zone-less.
        var state = Damage.Apply(dataPart,
            (weapon.HealthDamage ?? 0f) * damageScale, (weapon.ArmorDamage ?? 0f) * damageScale);
        string struckPart = state?.Def.Name ?? dataPart;
        if (state != null)
        {
            Visuals?.OnPartDamage(struckPart, state.Fraction);
            Gauges?.OnPartDamage(struckPart); // damage dial: hit zone blinks 5 s
        }

        if (_projectileHitsLogged < 6)
        {
            _projectileHitsLogged++;
            GD.Print($"shot hit P{PlayerIndex + 1} ({colliderPart}→{struckPart}): {weapon.Id} "
                + (state != null
                    ? $"armor={state.Armor:0.0}/{state.Def.MaxArmor:0} hp={state.Hp:0.0}/{state.Def.MaxHp:0} "
                    : string.Empty)
                + $"hull={Damage.WholeHealth:0.0}/{Damage.WholeHealthMax:0}");
        }

        // The decoded kill rule: whole-vehicle health current at or below zero (never a part
        // flag) — reachable through the zone-less overflow, so it is tested on every hit.
        if (Damage.IsDestroyed)
        {
            GD.Print($"vehicle health exhausted ({struckPart} last) — shot down by {weapon.Id}");
            Crash(impact, $"gunfire ({weapon.Id})", colliderPart, null,
                killer: shooter != ProjectilePool.NoShooter ? shooter : null);
            return;
        }
        DamageApplied?.Invoke(this);
        // The damage-reaction roll (D11): an AI pilot rolls the steady-hand test on the
        // absorbed damage; a FAILED test breaks off (the decoded vocabulary). The round does
        // not carry its shooter's position, so the impact offset stands in as the threat
        // bearing for evade's turn-away.
        if (!IsHumanPiloted && Pilot?.Machine is { } machine)
        {
            machine.NotifyDamage(
                ((weapon.HealthDamage ?? 0f) + (weapon.ArmorDamage ?? 0f)) * damageScale,
                impact - _model.Position);
        }
        _damageFlashText = state != null
            ? $"⚠ HIT {struckPart.ToUpperInvariant()} {state.Fraction * 100f:0}%"
            : $"⚠ HIT HULL {Damage.SummaryHealthFraction * 100f:0}%";
        _damageFlash = DamageFlashTime;
    }

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
        // Advance the stunt clock every physics frame — including through the crash freeze so the
        // clock never stops (a deliberate rule); it stops only at AllComplete (inside Tick). A
        // halted GameClock stops the calls entirely, so the timer freezes with the rest of the sim.
        Stunt?.Tick(dt);

        // The near-miss accumulator drains on the sim clock like everything else here; the pool
        // registers passes into it earlier in the same step (GameSession.DriveSimSteps order).
        _warningShots?.Tick(dt);

        // Debug: force-complete the run so the results board renders for a deterministic
        // screenshot. In a race the players finish STAGGERED by index (and their totals padded by
        // it), so the shot exercises the real one-pilot-finishes-while-the-others-fly path — the
        // placings, the waiting banner, then the shared board — instead of four identical totals
        // landing on frame one.
        if (DebugCompleteStunt && Stunt is { AllComplete: false }
            && (Race == null || Stunt.Elapsed >= PlayerIndex * DebugFinishStagger))
            Stunt.DebugCompleteAll(Race != null ? PlayerIndex * 2f : 0f);

        // Dogfight: once the match is decided the results board is up — Visible mirrors
        // Match.Completed exactly, same as the race board mirrors Race.AllFinished — and any
        // player's R there is a rematch, not a respawn. Checked before the crash branch (a still-
        // crashed loser's R means "rematch", not "respawn me alone"; RestartMatch already respawns
        // every rig, this one included) but does NOT freeze the sim like the stunt/race branch
        // below — flying continues under the board on every frame the button is not pressed.
        if (Match is { Completed: true } && RespawnPressed())
        {
            RestartMatch?.Invoke();
            return;
        }

        // A solo run freezes at the finish; R starts a fresh run.
        // In a race, a finished pilot keeps flying while their mission state holds the time,
        // placing and objective progress. R or an unattended restart rematches only after the
        // last pilot finishes, while the shared board is displayed over the live simulation.
        // This lets the finished pilot clear the remaining gates instead of parking in one.
        // The branch remains ahead of crash handling so a finish transition is always clean.
        if (Race is { AllFinished: true } && Stunt is { AllComplete: true })
        {
            bool autoRematch = HoldSegments != null && PlayerIndex == 0
                && (_autoRestartIn -= dt) <= 0f;
            if (RespawnPressed() || autoRematch)
            {
                RestartRace?.Invoke();
                return;
            }
        }

        if (Stunt is { AllComplete: true } && Race == null)
        {
            _simPrev = _simCurr;   // hold the finish pose — no stale pair left to interpolate
            // The solo scoreboard accepts R as a fresh run, distinct from a mid-run respawn.
            if (RespawnPressed())
                RestartStuntRun();
            return;
        }
        _autoRestartIn = AutoRespawnDelay; // re-armed while the run is live

        if (_crashed)
        {
            // The wreck + effects run on the crash AnimRuntime, which advances itself in its own
            // _Process through this crash freeze (motions, the played def, every puffer) — but not
            // through a clock halt, which stops that runtime with everything else, so P during a
            // crash catches the wreck mid-break-up. The airframe stays frozen at the impact point
            // until the pilot respawns (R / gamepad Y or A); unattended HoldSegments runs and
            // AutoRespawnAfter sessions (Versus) respawn on the timer armed at Crash instead
            if (RespawnPressed()
                || ((HoldSegments != null || AutoRespawnAfter != null) && (_autoRespawnIn -= dt) <= 0f))
                Respawn();
            return;
        }

        // Weapon lab: a HELD airframe skips input, the flight model and the whole collision
        // sweep, and re-asserts its pinned pose instead — but only those. Everything from the pose
        // commit down (weapons, gauges, telemetry) runs exactly as it does in flight, which is the
        // whole point: the lab fires through the same code path free flight does. The pose goes
        // back in through _model.Reset, never by writing GlobalTransform behind the model's back,
        // so every reader of _model (the stall gauge, the telemetry line, the AGL ray, the reticle
        // march) sees one consistent stationary state.
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
            var prev = _model.Position;          // committed position from last frame
            var input = HoldSegments != null ? NextHoldInput(dt)
                : Pilot != null ? NextPilotInput(dt)
                : ReadKeyboard(dt);
            _lastInput = input;
            _damageCooldown -= dt;
            _grazeReactionCooldown -= dt;
            _model.Step(input, dt);

            // Crash when the frame's flight path runs into solid world geometry (terrain,
            // buildings, trees). The airframe boxes (fuselage/wings/tail) are swept along
            // the frame's motion so a wingtip or tail fin collides, not just the center
            // line; the center ray stays as an anti-tunnelling backstop. Only the shapeless
            // fallback keeps a nose margin on the ray — with real boxes it would fire
            // ~6 m before the fuselage box reaches the wall.
            var to = _model.Position;
            var step = to - prev;
            float len = step.Length();
            float margin = Collider == null ? CollisionMargin : 0f;
            var probeEnd = len > 1e-4f ? to + step / len * margin : to;
            bool hit = SweepAirframe(prev, step, out var impact, out var hitName, out var part,
                out var normal, out float stopFrac, out var hitBody);
            if (!hit && HitWorld(prev, probeEnd, out impact, out hitName, out hitBody))
            {
                hit = true;
                part = "center";
                normal = len > 1e-4f ? -step / len : Vector3.Up;
                stopFrac = 1f;
            }
            if (_probe != null)
                DrawProbe(prev, probeEnd, prev + step * stopFrac, hit);
            // A collision with a WeaponOrCollideHit destructible (the 44 facades/windows/agyrobus)
            // breaks IT and the plane flies through — apply severity-scaled damage and clear the hit.
            // Every other object (WeaponHit towers/gates, plain geometry) stays solid and falls through
            // to the crash/graze below (the 0.01 health marks these as fly-through set dressing).
            if (hit && CollideDamageSink != null)
            {
                var cv = _model.VelocityDir * _model.Speed;
                float cvn = Mathf.Abs(cv.Dot(normal));
                if (CollideDamageSink(hitBody, cvn * CollideDamagePerVn))
                {
                    hit = false;   // set-dressing shattered; the plane keeps its full-motion pose
                }
            }
            if (hit && !SurviveHit(prev, step, stopFrac, impact, hitName, part, normal, hitBody))
            {
                Crash(impact, hitName, part, hitBody);
                return;
            }
        }

        _simPrev = _simCurr;
        _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;

        // The dynamic chase radius advances on the sim step, not the render frame: the
        // acceleration derivative needs the fixed dt, and the transient's relaxation is a
        // SIM-time rate. A crash or halt stops the calls, freezing the radius too.
        _cam?.UpdateDynamics(dt, _model.Speed);

        // The AI gunner (D14): acquire/hold the target and decide this tick's trigger and lead
        // BEFORE the fire step reads them. Runs for AI pilots only; a gunner-less AI keeps the
        // released trigger it always had.
        if (!IsHumanPiloted && Pilot?.Gunner is { } aiGunner)
            DriveAiGunner(aiGunner);

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
                RocketHeld = Projectiles != null && RocketFirePressed(),
                GunSelectHeld = GunSelectPressed(),
                RocketSelectHeld = RocketSelectPressed(),
            };
            // The assist's forget + catch-up pass (docs/org/aim-assist.md "Per frame") — ticked
            // on the PRE-shot state, since the original restamps a slot's last-update on every
            // round that goes out (B5's job) and this pass must run before that happens this
            // frame. Gated on IsHumanPiloted (B6): the original runs this only for the local
            // player, and an AI plane's dead-eye path has no slots to tick at all.
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
        Ordnance?.Update();   // hide a pylon's mounted rocket the moment it fired its last

        // The carried turret gunners (C9a): each tracks and fires on its own, into the same
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
        if (Gauges != null)
            Gauges.AglMeters = HitWorld(_model.Position,
                _model.Position + Vector3.Down * 1000f, out var ground, out _, out _)
                ? _model.Position.Y - ground.Y
                : float.MaxValue;

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
                     $"thr={_model.Throttle:0.00} rates=({_model.BodyRates.X:0.00},{_model.BodyRates.Y:0.00},{_model.BodyRates.Z:0.00}) " +
                     $"path={Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(_model.VelocityDir.Y, -1f, 1f))):0}° " +
                     $"nose={Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-_model.Attitude.Z.Y, -1f, 1f))):0}° " +
                     $"wv={Mathf.Abs(_model.Attitude.Y.Dot(Vector3.Up)):0.00}" +
                     (Gauges != null && Gauges.AglMeters < float.MaxValue
                         ? $" agl={Gauges.AglMeters:0}" : ""));
        }
    }

    public override void _Process(double delta)
    {
        var clock = GameClock.Current;
        // Debug screenshot freeze: toggle with P / gamepad Start, then hold the whole simulation
        // in place (physics, input, collision, audio, props) so successive screenshots frame the
        // plane from the same spot. Polled here rather than in the sim step because a halted sim
        // takes no steps and could never resume itself. Checked even while crashed.
        bool pausePressed = PauseTogglePressed();
        if (pausePressed && !_pausePrev && clock != null)
        {
            clock.Halted = !clock.Halted;
        }
        _pausePrev = pausePressed;
        bool halted = clock?.Halted ?? false;
        if (halted != _haltPrev)
        {
            _haltPrev = halted;
            // The engine/whine/rattle loops hold their sample position through the freeze; the
            // one-shots already in flight are left to play out.
            Audio?.SetPaused(halted);
        }
        // The orbit camera serves both the P freeze and the weapon lab's HELD airframe: in
        // both the plane is standing still and the point is to fly the view around it. Seeding on
        // the edge starts it where the chase camera left off, so neither entry jumps — and so does
        // the hand-back from the lab's free camera, which leaves the eye somewhere else entirely.
        bool orbiting = halted || Held;
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
            if ((clock == null || !clock.ParentDriven) && !_crashed)
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
            // The orbit camera runs on wall time through a halt on purpose: the point of the
            // freeze is to fly the camera around a stopped world. A held airframe is the same
            // situation with the world still running.
            var (yawIn, pitchIn, zoomIn) = OrbitInput();
            _cam.Orbit((float)delta, _model.Position, yawIn, pitchIn, zoomIn);
        }
        else if (_crashed)
        {
            // The authored crash camera holds the pose Crash() cut to — the original's camera
            // does not move after the cut (footage), so nothing is written here until respawn.
        }
        else
        {
            int view = _cam.ActiveView();
            if (view >= 0)
            {
                _cam.FixedView(view, _renderPose);
            }
            else if (_cam.BackActive())
            {
                _cam.BackView(_renderPose);
                view = CameraController.BackViewLog;
            }
            else
            {
                // The chase camera trails the plane by exponential smoothing, so its pose is a
                // function of the dt it is fed. On wall time that makes a scripted flight capture
                // frame-rate dependent even when the simulation underneath it is pinned — the pose
                // has to come off the same clock as the plane it follows. The POSE it composes
                // from is the DRAWN one, same rule as the rigid views above: the camera rides the
                // plane exactly (offset smoothing only), so basing it on the raw sim pose while
                // the plane renders interpolated makes the plane jump back and forth in frame by
                // one sim step's travel — invisible parked, a blur at speed.
                _cam.Chase(simDt, _renderPose.Origin, _renderPose.Basis);
            }
            _cam.LogView(view, _model.Position, _model.Attitude);
        }

        float mph = _model.Speed * 2.23694f;
        float ft = _model.Position.Y * 3.28084f;
        // heading of the nose: 0 = north (−Z), 90 = east (+X) — shared by the compass and the marker
        var nose = -_model.Attitude.Z;
        float headingDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(nose.X, -nose.Z)), 360f);
        if (Compass != null)
            Compass.HeadingDeg = headingDeg;
        // Stunt objective marker: cycle the displayed target (Tab / gamepad X, edge-detected) and
        // feed it this frame's pose so it can project the zone and compute the clock bearing.
        if (Stunt != null)
        {
            bool cycle = CycleTargetPressed();
            if (cycle && !_cyclePrev)
                Stunt.CycleTarget();
            _cyclePrev = cycle;
        }
        if (Marker != null)
        {
            Marker.PlanePos = _model.Position;
            Marker.HeadingDeg = headingDeg;
        }
        // Dogfight opponent markers: this pane's own pose, so the HUD can compute each
        // opponent's clock bearing off it — same feed Marker gets, for the same reason.
        if (VersusHud != null)
        {
            VersusHud.PlanePos = _model.Position;
            VersusHud.HeadingDeg = headingDeg;
        }
        if (Gauges != null)
        {
            Gauges.SpeedMph = mph;
            Gauges.AltitudeFt = ft;
            // A held plane sits at 0 m/s, which is below every stall speed — but it is pinned, not
            // stalling, so the gauge (and the HUD line below) stay quiet in the lab.
            // The lamp is the WARNING (fixed 0.30 fd), which leads the nose-drop the model now flies
            // at the airframe's own computed StallSpeed (see FlightModel.isStalled); the fraction
            // beside it is what ramps the lamp's blink rate.
            Gauges.StallWarning = !_crashed && !halted && !_held && _model.IsStallWarned();
            Gauges.StallFrac = _model.StallFraction;
        }
        // Feeds the weapon gauges (if built) and the text readout (if built) — both draw from the live
        // loadout, so this runs whenever there is one, independent of the dial cluster.
        UpdateWeaponGauges();
        // Points the gun pipper at 0.5 s of the selected group's flight, on the nose axis (if built).
        UpdateReticle(simDt);
        if (_hud != null)
        {
            // Splitscreen: the text block shrinks with the pane, like every other HUD element
            // (HudMetrics). PaneFactor is exactly 1 in single player, so the original 22 px at
            // (16,10) is untouched there; re-applied only when the factor actually changes.
            float paneFactor = HudMetrics.PaneFactor(_hud);
            if (!Mathf.IsEqualApprox(paneFactor, _hudPaneFactor))
            {
                _hudPaneFactor = paneFactor;
                _hud.AddThemeFontSizeOverride("font_size", Mathf.Max(8, Mathf.RoundToInt(HudFontSize * paneFactor)));
                _hud.Position = new Vector2(HudMargin.X * paneFactor, HudMargin.Y * paneFactor);
            }
            // A splitscreen pane is proportionally WIDER than it is tall, so a height-scaled single
            // line would run into the top-centre compass tape in a 4P quarter pane — break the
            // throttle onto its own line there. Full screen keeps the one-liner.
            string speedAlt = $"SPD {mph,4:0} MPH   ALT {ft,5:0} FT";
            string throttle = $"THR {_model.Throttle * 100,3:0}%";
            _hud.Text = paneFactor < 1f ? $"{speedAlt}\n{throttle}" : $"{speedAlt}   {throttle}";
            if (!_held && _model.isStalled())
                _hud.Text += "\n⚠ STALLED - SPEED UP";
            if (!halted && !_crashed && _damageFlash > 0f)
            {
                _damageFlash -= (float)delta;
                _hud.Text += $"\n{_damageFlashText}";
            }
            if (Damage?.Summary() is { Length: > 0 } dmgSummary)
                _hud.Text += $"\nDMG {dmgSummary}";
            // The weapon ammo readout is the weapon gauges + the WeaponReadout (drawn in the game's
            // own HUD font from MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES), not this text block.
            // Stunt run status lives in the marker HUD; keep the compact text line only
            // as a fallback if the marker somehow wasn't built.
            if (Stunt != null && Marker == null)
                _hud.Text += $"\n{Stunt.StatusLine()}";
            if (halted)
                _hud.Text += "\n⏸ PAUSED — orbit: WASD/arrows · zoom: Shift/Ctrl · P (gamepad Start) resume · . step one frame";
            else if (_crashed)
                _hud.Text += "\n⚠ CRASHED — PRESS R (GAMEPAD Y/A) TO RESPAWN";
        }
        if (!halted && !_crashed)
        {
            Audio?.Update(simDt, _model.Throttle, _model.Speed / _model.Stats.FdSpeed,
                1f - (Damage?.WorstFraction ?? 1f));
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
        Props?.Advance(simDt, _crashed || halted ? 0f : PropIdleSpin + (1f - PropIdleSpin) * _model.Throttle);

        // Blink the wingtip flares on the data's 1.5 s cycle, and track the stick with
        // the control surfaces. Frozen while paused (so a screenshot catches a fixed
        // state — the paused orbit camera can inspect the held deflection) and while
        // crashed (the airframe is hidden anyway).
        if (!_crashed && !halted)
        {
            WingLights?.Advance(simDt);
            Surfaces?.Advance(simDt, _lastInput);
            // damage-stage trails need no per-frame feed: the rig runtime's emitters follow
            // their pdpN/prop1 host nodes themselves
        }
    }

    /// <summary>Whether static world geometry blocks the segment — the turret gunners' cached
    /// line-of-sight test. World layer only: another aircraft in the way is not cover, which is
    /// also why <see cref="HitWorld"/> (world + aircraft) is not reused here.</summary>
    internal bool WorldBlocksLine(Vector3 from, Vector3 to)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        return space.IntersectRay(
            PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.World)).Count > 0;
    }

    /// <summary>The rocket name the text readout shows: the resolved <c>MSG_WEAP_*</c> display name
    /// (e.g. "High-explosive rocket") when it resolved, else the short internal handle ("BOOM") — a
    /// raw, unresolved <c>MSG_*</c> key falls back to the handle rather than being shown verbatim.</summary>
    private static string RocketReadoutName(WeaponDef w) =>
        !string.IsNullOrEmpty(w.DisplayName) && !w.DisplayName.StartsWith("MSG_", StringComparison.Ordinal)
            ? w.DisplayName
            : w.Name;

    /// <summary>Where a round of <paramref name="weapon"/> fired from <paramref name="origin"/> along
    /// <paramref name="forward"/> (carrying <paramref name="inheritVel"/>, the plane's velocity) sits
    /// after travelling <paramref name="distance"/> m of path — <see cref="Ballistics.March"/>, the
    /// SAME integration <see cref="ProjectilePool"/> steps each round with, so the reticle and the
    /// rounds agree.</summary>
    private static Vector3 BallisticImpactPoint(WeaponDef weapon, Vector3 origin, Vector3 forward,
        Vector3 inheritVel, float distance)
    {
        // A fixed integration step rather than the sim's: no weapon a gun group can resolve carries
        // ACCELERATION or GRAVITY (the four accelerating defs of the 48 are rockets and a glide
        // bomb), so every marched round is a straight line, on which the step size cannot move the
        // endpoint — and a fixed step keeps the reticle from twitching with the frame rate.
        const float dt = 1f / 120f;
        return Ballistics.March(weapon, origin, forward, inheritVel, distance, dt);
    }

    /// <summary>The struck body's numeric surface id (<see cref="SceneBuilder.SurfaceIdMeta"/>,
    /// stamped on every collider), or null when there is no struck body — the headless
    /// <c>--crash</c> force, which is the original's null-material arm and so resolves slot 0.
    /// The weapon IMPACT table is read at this same id (<see cref="ProjectilePool.SurfaceIdOf"/>),
    /// which answers <c>0</c> where this answers null, because that path has no null to carry.</summary>
    private static int? SurfaceIdOf(Node? hitBody) =>
        hitBody != null && hitBody.HasMeta(SceneBuilder.SurfaceIdMeta)
            ? hitBody.GetMeta(SceneBuilder.SurfaceIdMeta).AsInt32()
            : null;

    /// <summary>Deadzone + squared response for fine control around center.</summary>
    private static float StickCurve(float v)
    {
        const float deadzone = 0.15f;
        float a = Mathf.Abs(v);
        if (a < deadzone)
            return 0f;
        float t = Mathf.Min(1f, (a - deadzone) / (1f - deadzone));
        return Mathf.Sign(v) * t * t;
    }

    /// <summary>Space / gamepad B — the gun trigger (caller drives the fire-rate clock);
    /// <c>--fire</c> holds it down for unattended runs.</summary>
    private bool FirePressed() => AutoFire || KeyDown(Key.Space) || PadPressed(JoyButton.B);

    /// <summary>F / gamepad A — the rocket trigger. One discrete pull launches one rocket (holding
    /// does NOT auto-repeat; only the 1.0 s cooldown gates it), and <c>--fire-rockets</c> auto-repeats
    /// for unattended runs. Gamepad A also respawns, but only from the crashed / run-complete screens
    /// — states this live-flight firing path never shares — so the two never collide.</summary>
    private bool RocketFirePressed() => KeyDown(Key.F) || PadPressed(JoyButton.A);

    /// <summary>G / gamepad D-pad Left — cycles the gun selector through the firable groups (1 → 2 →
    /// … → 1). Only ONE group fires at a time; the gun trigger fires the selected one. Caller edge-detects.</summary>
    private bool GunSelectPressed() => KeyDown(Key.G) || PadPressed(JoyButton.DpadLeft);

    /// <summary>H / gamepad D-pad Right — moves the hardpoint selector to the next pylon that still
    /// carries ordnance (each pylon is its own selectable slot, whatever it loads — even a plane with
    /// one uniform ordnance type). The rocket trigger then launches from the selected pylon. Caller
    /// edge-detects.</summary>
    private bool RocketSelectPressed() => KeyDown(Key.H) || PadPressed(JoyButton.DpadRight);

    /// <summary>Feeds the two cockpit weapon gauges from the same live ammo the firing code
    /// draws down. The gun gauge shows the SELECTED group (its rounds, its short NAME, and one belt
    /// light per firable group by remaining fraction, the arrow on the selected one); the missile
    /// gauge shows the SELECTED pylon's rounds, its NAME, one belt light per pylon, and points the
    /// arrow at that pylon. With <c>--infinite-ammo</c> the counters sit at capacity (the counters
    /// never deplete), so the gauges read full and never step.</summary>
    private void UpdateWeaponGauges()
    {
        if (Loadout == null)
        {
            return;
        }
        int gunSel = _fire?.GunSel ?? 0;

        // Guns: the SELECTED firable group. The gauge takes the caliber+ammo short NAME and the belt
        // fractions; the text readout takes the group's mount name and its per-group rounds.
        GunGroup? selectedGun = null;
        int firable = 0;
        _gunGaugeSlots.Clear();
        foreach (var g in Loadout.FirableGuns)
        {
            _gunGaugeSlots.Add(g.Capacity > 0 ? (float)g.Ammo / g.Capacity : 0f);
            if (firable == gunSel)
            {
                selectedGun = g;
            }
            firable++;
        }
        if (_gunGaugeState != null)
        {
            _gunGaugeState.Selected = firable > 0 ? Mathf.Clamp(gunSel, 0, firable - 1) : 0;
            _gunGaugeState.Count = selectedGun?.Ammo ?? 0;
            _gunGaugeState.Type = selectedGun?.Weapon.Name ?? "";
        }
        if (WeaponReadout != null)
        {
            WeaponReadout.GunGroupName = firable > 0 ? selectedGun?.Mount : null;
            WeaponReadout.GunAmmo = selectedGun?.Ammo ?? 0;
        }

        // Rockets: the SELECTED pylon (the one H points at and the trigger fires). The count reads
        // that pylon's own rounds — per-pylon (a full HE pylon = 3), NOT the sum across pylons; the
        // original's gauge is per-pylon (its Warhawk shows BOOM 3, not 24). The arrow points at the
        // selected pylon; the readout takes the rocket's display name (MSG_WEAP_* through Messages,
        // e.g. "High-explosive rocket"), the gauge its short NAME.
        var hps = Loadout.Hardpoints;
        if (hps.Count > 0)
        {
            int sel = Mathf.Clamp(_fire?.SelectedPylon ?? 0, 0, hps.Count - 1);
            var selectedHp = hps[sel];
            // The belt lights index by PYLON NUMBER (Hardpoint.Index), not by position in this
            // compacted list — a partial stock fit must leave gaps at the unfitted physical
            // positions rather than piling its lit slots at the ring's start. The ring is
            // always the full 8; unfitted positions default to 0f, which already reads red like a
            // spent one (GaugeCluster.HardpointIndicatorColor).
            _missileGaugeSlots.Clear();
            for (int i = 0; i < GaugeCluster.HardpointRingSize; i++)
            {
                _missileGaugeSlots.Add(0f);
            }
            foreach (var h in hps)
            {
                _missileGaugeSlots[h.Index - 1] = h.Capacity > 0 ? (float)h.Ammo / h.Capacity : 0f;
            }
            WeaponDef typeWeapon = selectedHp.Weapon;
            int perPylon = selectedHp.Ammo;
            if (_missileGaugeState != null)
            {
                _missileGaugeState.Selected = selectedHp.Index - 1;
                _missileGaugeState.Count = perPylon;
                _missileGaugeState.Type = typeWeapon.Name;
            }
            if (WeaponReadout != null)
            {
                WeaponReadout.MissileName = RocketReadoutName(typeWeapon);
                WeaponReadout.MissileAmmo = perPylon;
            }
        }
        else if (WeaponReadout != null)
        {
            WeaponReadout.MissileName = null;
        }
    }

    /// <summary>Points the gun pipper where the original points it (decoded for `BL-342`/B5 —
    /// <c>FUN_00426570</c> places it, <c>FUN_004267f0</c> smooths it; docs/org/aim-assist.md
    /// "What the pipper follows"): at the selected gun group's muzzle MIDPOINT, offset by
    /// <see cref="ReticleFlightTime"/> seconds of the round's flight — the weapon's <c>VELOCITY</c>
    /// along the plane's NOSE plus the plane's own velocity. So its range is the weapon's own
    /// (430 m for the 860 m/s fallback), and its distance from the muzzle is rate-smoothed, which
    /// is what keeps it from snapping when the selected group changes.
    ///
    /// <para>⚠ It marks the plane's nose axis, NOT the aim assist's line and not the muzzle axes.
    /// The original computes it from the airframe's forward row and never reads the assist slots'
    /// directions (it reads those slots only for the muzzle attachment positions), so an assisted
    /// round deliberately leaves along a line the pipper does not show. That is the answer to B5's
    /// HUD question: the assist stays invisible, and "make the pipper follow the assisted line"
    /// would be the wrong port.</para>
    ///
    /// <para>Hidden while crashed or when the plane has no firable gun / no muzzle to fire from.
    /// No-op without a reticle.</para></summary>
    private void UpdateReticle(float dt)
    {
        if (Reticle == null)
        {
            return;
        }
        // Hidden while crashed (the airframe is gone) — a stale pipper must not hang in the sky —
        // and when there is nothing to aim.
        if (_crashed || Loadout == null || _fire == null)
        {
            Reticle.Active = false;
            return;
        }
        // The selected firable gun group — the one the trigger fires. Its muzzles' averaged world
        // pose is where THAT group's fire converges.
        GunGroup? sel = null;
        int gi = 0;
        foreach (var g in Loadout.FirableGuns)
        {
            if (gi == _fire.GunSel)
            {
                sel = g;
                break;
            }
            gi++;
        }
        if (sel == null || sel.Muzzles.Count == 0)
        {
            Reticle.Active = false;
            return;
        }
        // The muzzle MIDPOINT of the selected group — the original averages that group's live
        // barrel attachments (and falls back to the plane's own position when it has none).
        var origin = Vector3.Zero;
        foreach (var m in sel.Muzzles)
        {
            origin += m.GlobalPosition;
        }
        origin /= sel.Muzzles.Count;

        // Where a round fired now would be after ReticleFlightTime: nose × VELOCITY + the plane's
        // own velocity. No ballistic march — no weapon a gun group can resolve carries ACCELERATION
        // or GRAVITY, so the straight line IS the round's path (see Ballistics.cs), and this is the
        // original's own expression rather than a second derivation of it.
        var inheritVel = _model.VelocityDir * _model.Speed;
        var nose = -GlobalTransform.Basis.Orthonormalized().Z;
        float speed = sel.Weapon.Velocity ?? ReticleDefaultSpeed;
        var offset = (nose * speed + inheritVel) * ReticleFlightTime;
        float target = offset.Length();
        if (target < 1e-3f)
        {
            _reticleRate = 0f;
            Reticle.Active = false;
            return;
        }
        // Marched through the shared integration rather than added on directly: for every weapon a
        // gun group can resolve (no ACCELERATION, no GRAVITY) the march IS this straight line, so
        // the two agree exactly — and the reticle keeps sharing one integration with the rounds
        // instead of growing a second copy of it.
        var marched = BallisticImpactPoint(sel.Weapon, origin, nose, inheritVel, target);
        // The range smoother: the closing rate builds at ReticleAccel, capped by the gap that is
        // left (so it eases in rather than overshooting), then the pipper sits at the smoothed
        // range along the same direction.
        var toward = marched - origin;
        float reach = toward.Length();
        if (reach < 1e-3f)
        {
            _reticleRate = 0f;
            Reticle.Active = false;
            return;
        }
        float gap = Mathf.Abs(_reticleDist - reach);
        float cap = gap >= ReticleFarGap ? ReticleFarRate : gap * ReticleRatePerGap;
        _reticleRate = Mathf.Min(_reticleRate + dt * ReticleAccel, cap);
        _reticleDist = Mathf.MoveToward(_reticleDist, reach, _reticleRate * dt);
        Reticle.ImpactPoint = origin + toward / reach * _reticleDist;
        Reticle.Active = true;
    }

    /// <summary>One gun round's launch direction: the per-muzzle aim assist (`BL-342`/B5,
    /// <c>FUN_004b6530</c>) — scan, lead, plane-local smoothing, 1° scatter — through
    /// <see cref="AimAssist.FireDirection"/>, which also restamps this barrel's slot so the forget
    /// timer runs from the last SHOT. A barrel with no slot (a group built before the slot array,
    /// which cannot happen in a bound loadout) falls back to its own muzzle axis, the same fallback
    /// an AI-piloted plane takes (below).
    ///
    /// <para>Gated on <see cref="IsHumanPiloted"/> (B6): the original's "local player" test is
    /// human-versus-AI, not pane 1 — every CSVM pane is a human pilot, so every pane is assisted
    /// (Decision 7). An AI plane never reaches the assist: its round leaves along the gunner's
    /// lead perturbed inside the dead-eye cone (D14), one scatter draw per shot; a gunner-less
    /// AI falls back to its muzzle axis.</para></summary>
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
            Team = AimAssist.TeamOfPilot(PlayerIndex),
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
            GD.Print($"gun aim assist: P{PlayerIndex + 1} snapped onto {found.Kind} " +
                     $"(score {found.Score:0.000}, {found.TimeOfFlight * (weapon.Velocity ?? ProjectilePool.DefaultVelocity):0} m out)");
        }
        return dir;
    }

    /// <summary>Performs one <see cref="FireControl.Step"/>'s decisions against the engine: spawns
    /// the commanded rounds from their live muzzle nodes (with the plane's inherited velocity),
    /// launches the commanded rocket, mirrors the loop-sound state onto <see cref="FlightAudio"/>
    /// (started every wanted frame — StartGunLoop only rebuilds when the name changes, which is how
    /// a mid-burst group switch swaps the loop), and sounds the dry cues. The first-round-per-group
    /// and first-launches breadcrumbs log here — print throttles, not fire-control state.</summary>
    private void ApplyFireOutcome(FireOutcome outcome)
    {
        var inheritVel = _model.VelocityDir * _model.Speed;
        // The aim assist's candidate set, built ONCE for this tick's rounds rather than per barrel:
        // the four lists are the same for every muzzle firing this frame. Two of the four have real
        // contents today (aircraft, live proximity-fused ordnance); structures need a world runtime,
        // and turrets arrive with M4 (docs/PLAN-sticky-bullets.md B4).
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
                GD.Print($"gun aim assist: candidates vehicles={_aimCandidates.Vehicles.Count} " +
                         $"turrets={_aimCandidates.Turrets.Count} " +
                         $"structures={_aimCandidates.Structures.Count} ordnance={_aimCandidates.Ordnance.Count}" +
                         (_aimCandidates.Structures.Count > 0 ? $", nearest structure {nearest:0} m" : ""));
            }
        }
        var planeBasis = GlobalTransform.Basis.Orthonormalized();
        double aimNow = GameClock.Current?.Time ?? 0.0;
        foreach ((int gi, int mi) in outcome.GunShots)
        {
            var g = _firableGuns[gi];
            var muzzle = g.Muzzles[mi];
            var aimDir = AssistedGunDirection(g.Weapon, gi, mi, muzzle, planeBasis, inheritVel, aimNow);
            Projectiles!.Spawn(g.Weapon, muzzle.GlobalTransform, inheritVel, PlayerIndex, muzzle, aimDir);
            Shake?.FireBullet(g.Weapon.Caliber ?? 0f); // the firing buzz: factor × caliber (measured)
            if (!_gunLoggedFirst[gi])
            {
                _gunLoggedFirst[gi] = true;   // verification breadcrumb: which groups actually fire
                GD.Print($"gun group {gi + 1} ({g.Mount}, {g.Weapon.Caliber ?? 0}-cal " +
                         $"{g.Weapon.Id}) firing");
            }
        }
        if (outcome.RocketPylon >= 0)
        {
            var hp = Loadout!.Hardpoints[outcome.RocketPylon];
            // No aim assist on a rocket: the original's assist is the GUN fire path's
            // (`FUN_004b6530` is reached from the gun branch alone). It leaves along the pylon axis.
            Projectiles!.Spawn(hp.Weapon, hp.Pylon.GlobalTransform, inheritVel, PlayerIndex, hp.Pylon);
            if (_rocketsLaunched < 12)
            {
                _rocketsLaunched++;
                GD.Print($"rocket: {hp.Weapon.Id} ({hp.Weapon.Name}) from pylon{hp.Index}, " +
                         $"{(InfiniteAmmo ? "∞" : hp.Ammo.ToString())} left on that pylon");
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
            GD.Print("rocket: dry pull, all pylons empty — empty-clip cue");
        }
    }

    /// <summary>Refills every gun group to its full load and re-arms the dry warnings (respawn).</summary>
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

    /// <summary>Full stunt restart from the results scoreboard (R): fresh clock + every
    /// zone incomplete, then the normal respawn (spawn pose / throttle / cleared damage). The
    /// scoreboard hides itself once AllComplete clears; the marker HUD replays its intro line.</summary>
    private void RestartStuntRun()
    {
        Stunt?.Reset();
        Respawn();
    }

    /// <summary>True if the segment crosses any solid collider — the static world, or another
    /// aircraft's body (never this plane's own, excluded by RID); on a hit,
    /// <paramref name="point"/> is the impact position (else the segment end) and
    /// <paramref name="hitName"/> names the collider (parent/body — e.g. a terrain
    /// tile's "g27889/col", or a clutter city block's "world1/clutter_bld_3_7").</summary>
    private bool HitWorld(Vector3 from, Vector3 to, out Vector3 point, out string hitName, out Node? hitBody)
    {
        point = to;
        hitName = "";
        hitBody = null;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to,
            CollisionLayers.WorldAndAircraft, Body?.ExcludeSelf));
        if (hit.Count == 0)
            return false;
        point = (Vector3)hit["position"];
        if (hit["collider"].Obj is Node body)
        {
            hitBody = body;
            hitName = $"{body.GetParent()?.Name}/{body.Name}";
        }
        return true;
    }

    /// <summary>Vertical clearance over static world collision only. Unlike <see cref="HitWorld"/>,
    /// another aircraft below the camera is not ground for speed_cue's NODE_NEAR_GROUND gate.</summary>
    private float HeightAboveWorldGround(Vector3 from)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return float.MaxValue;
        var to = from + Vector3.Down * 1000f;
        var hit = space.IntersectRay(
            PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.World));
        return hit.Count > 0 ? from.Y - ((Vector3)hit["position"]).Y : float.MaxValue;
    }

    /// <summary>The explosion boom the chosen crash def authors, which <see cref="FlightAudio"/>
    /// plays because the crash runtime renders effects and never sound: <c>player_crash_dirt</c>
    /// Sounds <c>snd_exp_ground_a</c> itself, <c>player_crash_water</c>'s <c>snd_exp_water_a</c>
    /// sits one level down in the <c>plane_big_splash</c> it calls, and the fallback
    /// <c>player_crash_default</c> Sounds only <c>plane_destroy_sg</c> — already played by
    /// <see cref="FlightAudio.OnCrash"/> — so it layers no surface boom at all.</summary>
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

    private void Crash(Vector3 impact, string hitName, string part, Node? hitBody, int? killer = null)
    {
        if (_crashed)
            return; // one crash, one Downed report — nothing may double-fire the death
        _crashed = true;
        _autoRespawnIn = AutoRespawnAfter ?? AutoRespawnDelay;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        Body?.SetHittable(false);       // a crashed plane soaks no rounds and blocks no sweep
        // Crash freezes the airframe before the fire step runs again this frame (the early
        // _crashed return above it), so a held trigger's loop would otherwise keep playing under
        // the wreck until respawn — nothing else ever calls StopGunLoop while _crashed is true.
        if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        // The original's selection: index the crash-def vector with the struck material's surface
        // id, falling back to slot 0 (player_crash_default) for a null material, an out-of-range id
        // or a slot naming a def this install does not ship — which is most of the ground, since
        // ids fire/airstrip/buildings/dzone have no def of their own and id 0 is ~98 % of every
        // chapter's materials.
        int? surfaceId = SurfaceIdOf(hitBody);
        string? crashDef = CrashDefs?.DefForSurfaceId(surfaceId);
        LastCrashDef = crashDef;
        Audio?.OnCrash();
        PlayCrashBoom(crashDef);
        // The engine wind-down cue layers over the explosion, replacing the loops' abrupt cut with
        // snd_propstop.
        Audio?.OnEngineStop();
        // No plume survives a dead engine.
        ThrottleSmoke?.Reset(_throttle);
        SpeedCue?.Reset();
        if (CrashRuntime != null && crashDef != null)
        {
            // Data-driven crash: PLAY the compiled def on this plane's scoped crash
            // runtime. The def hides healthy/dontmove/markers, shows the destroyed wreck, launches
            // the pieceN ballistics, and fires every authored effect (sparks, the fireball cluster,
            // the burning-debris arcs, and on the dirt variant the black smokeball and dirt burst).
            // Audio stays the same
            // path (the crash runtime treats SOUND as handled-elsewhere, so nothing double-plays).
            // The wreck pieces inherit a fraction of the plane's impact velocity so they scatter
            // along its travel rather than just popping up (the authored launch is a small relative
            // pop); TUNE the fraction against the original.
            //
            // The water variant is a different sequence, not a re-skin: `destroy_crash` leaves the
            // `destroyed` wreck INACTIVE and flings no pieces (the plane went under), and plays the
            // splash, its ripple and the steam spray over the crash trails instead of the dirt
            // burst and the fireball cluster.
            CrashRuntime.InheritedWorldVelocity = _model.VelocityDir * _model.Speed * WreckMomentum;
            CrashRuntime.Play(crashDef, CrashAnchor, applyReset: false);
            // The prop wind-down (staticpropN fades back in as prop1..3 fade out) — inert the
            // instant PlaneModel above hides, but keeps the def's own state consistent for
            // whatever plays next, and matters once a shutdown can leave the airframe visible.
            CrashRuntime.Play("stopprops", PlaneModel, applyReset: false);
        }
        // The authored crash camera: hard-cut to the static elevated vantage and hide
        // the HUD — both straight off the original's crash footage. The pose is set once here
        // and _Process writes nothing to the camera while crashed, so it holds until respawn.
        if (!CameraOwned)
            _cam?.CrashView(impact, _model.VelocityDir);
        if (_hudCanvas != null)
            _hudCanvas.Visible = false;
        string surface = surfaceId is { } sid
            ? $"{sid}/{SurfaceRegistry.NameForId(sid) ?? "?"}"
            : "none";
        GD.Print($"CRASH into {hitName} ({part}) surface={surface} def={crashDef ?? "-"} " +
                 $"impact=({impact.X:0},{impact.Y:0},{impact.Z:0}) " +
                 $"pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) " +
                 $"spd={_model.Speed:0} m/s — waiting for respawn");
        Downed?.Invoke(PlayerIndex, killer);
    }

    /// <summary>True when the button is down on one of THIS player's gamepads. With
    /// <see cref="PadDevices"/> null (single player) that is every connected pad — never `pads[0]`:
    /// phantom joypad devices (wireless dongles enumerating with the pad asleep, non-pad HID like
    /// Razer boards) can occupy the early slots, which made a pad connected after launch (= a later
    /// slot) dead. Splitscreen binds each player to its own device list instead.</summary>
    private bool PadPressed(JoyButton button)
    {
        foreach (int pad in Pads.For(PadDevices))
            if (Input.IsJoyButtonPressed(pad, button))
                return true;
        return false;
    }

    /// <summary>The largest-magnitude value of the axis across this player's gamepads (0 when
    /// none) — idle phantom devices read ~0 and never mask the real stick.</summary>
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

    /// <summary>A key, but only for a player the keyboard flies (splitscreen P2–P4 are pad-only).</summary>
    private bool KeyDown(Key key) => UseKeyboard && Input.IsKeyPressed(key);

    /// <summary>A +/- key pair as an axis, honoring <see cref="UseKeyboard"/>.</summary>
    private float KeyAxis(Key positive, Key negative) =>
        (KeyDown(positive) ? 1f : 0f) - (KeyDown(negative) ? 1f : 0f);

    private bool RespawnPressed() =>
        KeyDown(Key.R) || PadPressed(JoyButton.Y) || PadPressed(JoyButton.A);

    /// <summary>P (or gamepad Start), edge-detected so one press toggles once. Splitscreen
    /// disables it (<see cref="AllowPause"/>): the freeze halts the shared world.</summary>
    private bool PauseTogglePressed() =>
        AllowPause && (KeyDown(Key.P) || PadPressed(JoyButton.Start));

    /// <summary>Tab / gamepad X — cycles the stunt marker's displayed target (caller edge-detects).
    /// Gamepad Y would clash with the respawn button, so X (a free face button) instead.</summary>
    private bool CycleTargetPressed() =>
        KeyDown(Key.Tab) || PadPressed(JoyButton.X);

    /// <summary>Advance the scripted hold sequence by this frame and return the active
    /// segment's input. Segments run for their duration in order; the last one (or a
    /// duration ≤ 0) holds until respawn.</summary>
    private FlightInput NextHoldInput(float dt)
    {
        var segments = HoldSegments!;
        _holdElapsed += dt;
        float t = _holdElapsed;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Duration <= 0f || t < segments[i].Duration)
                return segments[i].Input;
            t -= segments[i].Duration;
        }
        return segments[^1].Input;
    }

    /// <summary>The AI pilot's step: ask <see cref="Pilot"/> for this frame's input and mirror its
    /// throttle into the controller's own lever, so the readers of <c>_throttle</c> (spawn smoke,
    /// telemetry) see the flown value exactly as the keyboard ramp path leaves it.</summary>
    private FlightInput NextPilotInput(float dt)
    {
        // The mode machine's terrain probe (D11 avoid crash) is this node's world-only LOS ray;
        // wired lazily so a machine assigned after spawn still gets it, and never overwriting a
        // probe a test injected.
        if (Pilot!.Machine is { ProbeBlocked: null } machine && IsInsideTree())
            machine.ProbeBlocked = WorldBlocksLine;
        var input = Pilot!.Next(_model, dt);
        _throttle = input.Throttle;
        return input;
    }

    /// <summary>One AI-gunner tick (D14): keep the standing target while it lives (re-acquiring
    /// through the D12 ranking when it is gone and <see cref="AiGunner.AutoTarget"/> allows), then
    /// hand the gunner this tick's fire geometry — the SELECTED gun group's weapon and muzzle
    /// midpoint, the sim pose (never the render pose), and the target's state — so
    /// <see cref="AiGunner.WantsFire"/> is current when the fire step reads it.</summary>
    private void DriveAiGunner(AiGunner gunner)
    {
        gunner.HoldFire();
        if (_fire == null || Projectiles == null)
            return;
        if (gunner.Target is not { } target || target.Crashed || !target.IsInsideTree())
        {
            TargetScore score = default;
            string how = "ranked";
            gunner.Target = gunner.AutoTarget
                ? SelectRankedTarget(gunner, out score, out how)
                : null;
            if (gunner.Target is not { } acquired)
                return;
            target = acquired;
            if (!_gunnerLoggedTarget)
            {
                _gunnerLoggedTarget = true; // verification breadcrumb: who the gunner went after
                Log.Info("flight",
                    $"ai gunner: shooter {PlayerIndex} targets P{target.PlayerIndex + 1} at {score.Distance:0} m ({how}: weight {score.Weight:0.0#} bias {score.Bias:0} rank {score.Rank:0})");
            }
        }
        // The mode machine gates the trigger (D11/D15): the target stays acquired in every
        // mode — patrol reads its position for the activation test — but only pursue shoots.
        // Lay off holds fire: the mode exists to let the player catch up and recover (the
        // design's rubber-band assist), and shooting the pursuer it is favouring defeats it.
        // Acquisition itself is bounded by the activation radius (D12's 1e21 cutoff), which is
        // the same 2000 m the machine activates at, so patrol still sees the approach.
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
        gunner.Solve(muzzlePos, WorldVelocity, _model.Attitude,
            target.WorldPosition, target.WorldVelocity, target.NoseDirection,
            group.Weapon.Velocity ?? ProjectilePool.DefaultVelocity, group.Weapon.Range ?? 0f);
        if (gunner.WantsFire && !_gunnerLoggedFire)
        {
            _gunnerLoggedFire = true; // verification breadcrumb: the gates first opened
            Log.Info("flight",
                $"ai gunner: shooter {PlayerIndex} opens fire on P{target.PlayerIndex + 1} at {WorldPosition.DistanceTo(target.WorldPosition):0} m ({group.Weapon.Id})");
        }
    }

    /// <summary>The D12 acquisition: the decoded ranking formula over the pool's registered
    /// aircraft, same roster and team gate as the aim assist and the turret gunners
    /// (<see cref="AimAssist.TeamOfPilot"/>). An assigned <see cref="AiGunner.PrimaryTargetName"/>
    /// that resolves to a live hostile inside the activation radius is picked outright —
    /// the assumed reading of the decoded "Primary target: %s" semantics: the assignment holds
    /// while valid, ranking takes over when it dies or leaves. The activation radius is the
    /// machine's (<c>min_ai_active_dist</c>, 2000 m shipped) — a candidate beyond it never
    /// ranks, but a STANDING target is kept regardless (disengagement is the mode machine's
    /// return-range rule, not acquisition's).
    ///
    /// <para>Deconfliction counts allied gunners already holding each candidate (invented
    /// minimum, see <see cref="AiTargetRanking"/>). ⚠ Under <see cref="AimAssist.TeamOfPilot"/>
    /// every pilot is its own team, so the count is zero in every current session — it becomes
    /// live the moment a team model puts two AI on one side.</para></summary>
    private FlightController? SelectRankedTarget(AiGunner gunner, out TargetScore score,
        out string how)
    {
        score = default;
        how = "ranked";
        if (Projectiles == null)
            return null;
        _gunnerScan.Clear();
        Projectiles.CollectAircraft(_gunnerScan);
        int ownTeam = AimAssist.TeamOfPilot(PlayerIndex);
        float activation = Pilot?.Machine?.ActivationRange ?? 2000f; // min_ai_active_dist fallback
        var ownPos = WorldPosition;
        var ownFwd = NoseDirection;
        _rankCandidates.Clear();
        _rankSources.Clear();
        FlightController? primary = null;
        foreach (var c in _gunnerScan.Vehicles)
        {
            if (!c.Live || ReferenceEquals(c.Source, this))
                continue;
            if (c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam || c.Team == ownTeam)
                continue;
            if (c.Source is not FlightController fc)
                continue;
            if (primary == null && gunner.PrimaryTargetName is { Length: > 0 } wanted
                && ownPos.DistanceTo(c.Position) <= activation
                && (string.Equals(fc.Name, wanted, StringComparison.OrdinalIgnoreCase)
                    || (fc.IsHumanPiloted
                        && wanted.Equals("player", StringComparison.OrdinalIgnoreCase))))
            {
                primary = fc;
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
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(fc.Name, gunner.RatingBiases),
                AlliedAttackers = attackers,
            });
            _rankSources.Add(fc);
        }

        if (primary != null)
        {
            // Log the assigned pick with its own rank inputs (informational — rank not consulted).
            int idx = _rankSources.IndexOf(primary);
            if (idx >= 0)
                score = AiTargetRanking.Score(ownPos, ownFwd, activation, _rankCandidates[idx]);
            how = "primary target";
            return primary;
        }

        int best = AiTargetRanking.SelectBest(ownPos, ownFwd, activation, _rankCandidates, out score);
        return best >= 0 ? _rankSources[best] : null;
    }

    private FlightInput ReadKeyboard(float dt)
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

        return new FlightInput
        {
            // pull = S/Down, push = W/Up; bank/yaw left = A/Left/Q
            Pitch = Mathf.Clamp(KeyAxis(Key.S, Key.W) + KeyAxis(Key.Down, Key.Up) + padPitch, -1f, 1f),
            Roll = Mathf.Clamp(KeyAxis(Key.A, Key.D) + KeyAxis(Key.Left, Key.Right) + padRoll, -1f, 1f),
            Yaw = Mathf.Clamp(KeyAxis(Key.Q, Key.E) + padYaw, -1f, 1f),
            Throttle = _throttle,
        };
    }

    /// <summary>Decides a confirmed collision's outcome: false =
    /// crash (severe impact, whole-vehicle health exhausted, or no damage data), true =
    /// survivable graze — the struck part takes severity-scaled damage, the plane is
    /// placed at the swept safe pose, its velocity deflects along the surface with
    /// some tangential loss, and the attitude takes a lever-arm kick.</summary>
    private bool SurviveHit(Vector3 prev, Vector3 step, float stopFrac, Vector3 impact,
        string hitName, string part, Vector3 normal, Node? hitBody)
    {
        if (Damage == null)
            return false; // no destroyable_parts data — every hit crashes (old behavior)
        var vel = _model.VelocityDir * _model.Speed;
        float vn = Mathf.Abs(vel.Dot(normal));
        if (vn >= CrashSpeed)
        {
            GD.Print($"impact severity: vn={vn:0.0} m/s (spd {_model.Speed:0.0}, " +
                     $"n=({normal.X:0.00},{normal.Y:0.00},{normal.Z:0.00})) ≥ {CrashSpeed} — crash");
            return false;
        }

        float speedBefore = _model.Speed;
        var localImpact = GlobalTransform.AffineInverse() * impact;
        string dataPart = PlaneDamage.MapStruckPart(part, localImpact);
        GrazeReaction(impact, hitName, hitBody);
        if (_damageCooldown <= 0f)
        {
            _damageCooldown = DamageCooldown;
            float dmg = GrazeMaxDamage * (vn / CrashSpeed) * (vn / CrashSpeed);
            var state = Damage.Apply(dataPart, dmg);
            string struckPart = state?.Def.Name ?? dataPart; // the resolver may redirect
            if (state != null)
            {
                Visuals?.OnPartDamage(struckPart, state.Fraction);
                Gauges?.OnPartDamage(struckPart); // damage dial: hit zone blinks 5 s
            }

            if (Damage.IsDestroyed)
            {
                GD.Print($"vehicle health exhausted ({struckPart} last) — " +
                         $"vn={vn:0.0} m/s into {hitName}");
                return false; // the decoded kill rule: whole-vehicle health at zero (A4/D14)
            }

            if (state != null)
            {
                _damageFlashText = $"⚠ IMPACT {struckPart.ToUpperInvariant()} {state.Fraction * 100f:0}%";
                _damageFlash = DamageFlashTime;
                GD.Print($"graze ({part}→{struckPart}): {hitName} " +
                         $"vn={vn:0.0} m/s dmg={dmg:0.0} " +
                         $"armor={state.Armor:0.0}/{state.Def.MaxArmor:0} hp={state.Hp:0.0}/{state.Def.MaxHp:0} " +
                         $"hull={Damage.WholeHealth:0.0}/{Damage.WholeHealthMax:0}");
            }
        }

        // Slide: place at the safe pose just off the surface, keep the tangential
        // velocity (with a severity-scaled loss), and kick the attitude about the
        // lever arm — impulse direction is the surface normal at the impact point.
        _model.Position = prev + step * stopFrac + normal * GrazePushOut;
        var slide = vel - normal * vel.Dot(normal);
        float slideLen = slide.Length();
        _model.Speed = slideLen * (1f - GrazeFriction * vn / CrashSpeed);
        if (slideLen > 1e-4f)
            _model.VelocityDir = slide / slideLen;
        var inv = _model.Attitude.Inverse();
        var lever = (inv * (impact - _model.Position)).Normalized();
        var kick = lever.Cross((inv * normal).Normalized());
        _model.BodyRates += kick * (GrazeKick * vn / CrashSpeed);

        // A plane ground to (near) standstill is a wreck, not a parked aircraft
        // (user-reported: it sat there collecting zero-damage kisses forever).
        if (_model.Speed < GrazeStopSpeed)
        {
            GD.Print($"ground stop: slid to {_model.Speed:0.0} m/s — destroyed");
            return false;
        }

        // Un-embed check: if any airframe box still overlaps world geometry at the
        // new pose (V-ditches, berm backsides — the reported terrain glitch-through),
        // push out along the contact normal; if it can't get free, explode rather
        // than tunnel.
        if (Collider != null && GetWorld3D()?.DirectSpaceState is { } space2)
        {
            for (int attempt = 0; ; attempt++)
            {
                var pose = new Transform3D(_model.Attitude, _model.Position);
                bool overlapping = false;
                foreach (var p in Collider.Parts)
                {
                    var q = new PhysicsShapeQueryParameters3D
                    {
                        Shape = p.Shape,
                        Transform = pose * p.Local,
                        CollisionMask = CollisionLayers.WorldAndAircraft,
                    };
                    if (Body != null)
                        q.Exclude = Body.ExcludeSelf; // own boxes always overlap the own body
                    // One hit is enough — this only asks whether the box is free.
                    // (Was a 4-result scan skipping bodies named "clutter_col". Those
                    // bodies were real until `a795548` confined clutter collision to
                    // kind.Solid; after it, the filter had nothing left to skip.)
                    if (space2.IntersectShape(q, 1).Count > 0)
                    {
                        overlapping = true;
                        break;
                    }
                }
                if (!overlapping)
                    break;
                if (attempt >= EmbedTries)
                {
                    GD.Print("embedded in terrain after a graze — destroyed");
                    return false;
                }
                _model.Position += normal * EmbedPushOut;
            }
        }
        return true;
    }

    /// <summary>One round passed close. The accumulator decides whether it is heard: intensity
    /// accrues here and the cue re-triggers no faster than the shipped interval, so a burst walking
    /// past the canopy is one warning, not thirty.</summary>
    private void OnNearMiss(float distance)
    {
        if (_crashed || _warningShots == null || !_warningShots.Register())
            return;
        string? variant = Audio?.OnWarningShot();
        // The breadcrumb the cue otherwise leaves only in the speakers: which pilot, how close, and
        // which of the three pass samples drew — an at-the-controls report is judgeable from it.
        Log.Info("weapons", $"near miss P{PlayerIndex + 1} at {distance:0.0} m intensity={_warningShots.Intensity:0.00} snd={variant ?? "none"}");
    }

    /// <summary>The survivable scrape's authored per-surface reaction, selected exactly as
    /// <see cref="Crash"/> selects its own: index <see cref="TouchdownDefs"/> with the struck
    /// material's numeric surface id (<see cref="SceneBuilder.SurfaceIdMeta"/>), falling back to
    /// slot 0 for a null material, an out-of-range id, or a slot naming a def this install does not
    /// ship. touchdown.zrd ships three (<c>touchdown_default</c> sparks, <c>touchdown_dirt</c>
    /// raises dust, <c>touchdown_water</c> splashes), so the other eleven ids fall back. That makes
    /// <b>ordinary terrain scrapes spark</b> off <c>_default</c> and reserves <c>_dirt</c> for
    /// <c>dirt</c>(13)-tagged material, the same correction B11 made to the crash.
    ///
    /// <para>The world-effects runtime stages the def at the contact point, alongside the
    /// sequence's own SOUND. Where the crash cascade ends at a bare anim name, this one ends at
    /// "play nothing", so a null def is silent rather than defaulted.</para>
    ///
    /// <para>One reaction per <see cref="GrazeReactionInterval"/> rather than per frame: a scrape
    /// confirms a hit every physics frame, and each call restarts the def and re-fires its sound.
    /// The interval is the authored puffer window, so a long slide reads as a repeating reaction
    /// instead of a stutter.</para>
    ///
    /// <para><b>Where the def is staged is an open A/B</b> (<c>graze.siteAtContact</c>, default the
    /// CONTACT POINT — the user's judgement at the controls). The data argues for the
    /// aircraft: every offset the def carries is authored against <c>MAIN_ROOT_NODE</c>, the node
    /// the engine invokes it on, and its SOUND is <c>AT_NODE MAIN_ROOT_NODE</c> too — staging at the
    /// contact point puts the puffer's own −0.5 Y half a metre UNDER the struck surface. But the
    /// puffs are scaled up in practice (<c>puffer.*SizeScale</c>), which lifts them clear of the
    /// burial anyway, and smoke visibly leaving the SURFACE reads better than smoke leaving the
    /// plane. Set the flag false to stage on the aircraft instead.</para>
    ///
    /// <para>⚠ Effects keep ONE live instance per def across the session — two players
    /// scraping at once collapse onto the later site, as every PlayEffectAt caller does.</para></summary>
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

    /// <summary>Sweeps each airframe box along this frame's motion against every solid
    /// collider — the static world plus other aircraft's bodies (a mid-air is a collision
    /// like any other, resolved by SurviveHit/Crash), this plane's own body excluded by
    /// RID. On a hit, reports the earliest one: contact point + surface
    /// normal (from rest info at the just-touching pose), collider name, which part
    /// struck, and the motion fraction where it stopped (for the debug draw). False
    /// when no collider was built or nothing is in the way.</summary>
    private bool SweepAirframe(Vector3 from, Vector3 motion, out Vector3 impact,
        out string hitName, out string part, out Vector3 normal, out float stopFrac, out Node? hitBody)
    {
        impact = _model.Position;
        hitName = "";
        part = "";
        hitBody = null;
        float mLen = motion.Length();
        normal = mLen > 1e-6f ? -motion / mLen : Vector3.Up;
        stopFrac = 1f;
        if (Collider == null)
            return false;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        var baseXf = new Transform3D(_model.Attitude, from);
        bool hit = false;
        foreach (var p in Collider.Parts)
        {
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = p.Shape,
                Transform = baseXf * p.Local,
                Motion = motion,
                CollisionMask = CollisionLayers.WorldAndAircraft,
            };
            if (Body != null)
                query.Exclude = Body.ExcludeSelf; // never sweep into this plane's own body
            var cast = space.CastMotion(query); // [safe, unsafe] fractions; [1,1] = clear
            if (cast[0] >= 1f || cast[0] >= stopFrac)
                continue;
            hit = true;
            stopFrac = cast[0];
            part = p.Name;
            // Contact details slightly PAST the first-overlap pose — at exactly
            // cast[1] the box may only just touch and GetRestInfo comes back empty,
            // which would leave the head-on fallback normal (vn = full speed) on
            // what was really a shallow graze. The box's swept center is the last
            // resort if even the deepened query finds nothing.
            query.Transform = query.Transform.Translated(
                motion * cast[1] + (mLen > 1e-6f ? motion / mLen * 0.05f : Vector3.Zero));
            query.Motion = Vector3.Zero;
            var rest = space.GetRestInfo(query);
            if (rest.Count > 0)
            {
                impact = (Vector3)rest["point"];
                normal = (Vector3)rest["normal"];
                hitBody = GodotObject.InstanceFromId((ulong)rest["collider_id"]) as Node;
                hitName = hitBody != null ? $"{hitBody.GetParent()?.Name}/{hitBody.Name}" : "world";
            }
            else
            {
                impact = (baseXf * p.Local).Origin + motion * cast[1];
                normal = mLen > 1e-6f ? -motion / mLen : Vector3.Up;
                hitBody = null;
                hitName = "world";
            }
        }
        return hit;
    }

    /// <summary>Debug view of the collision test: the swept center ray with a cross at
    /// its tip, plus the airframe boxes drawn at where this frame's sweep stopped.
    /// Freezes red at the impact pose while crashed.</summary>
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

    /// <summary>Adds the 12 wireframe edges of a box (half-extents h) to the probe mesh.</summary>
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

    /// <summary>The paused orbit camera's three axes, mixed from this player's keyboard and pads.
    /// Read here rather than in <see cref="CameraController"/> so the camera never learns about
    /// pad devices, window focus or the stick response curve.</summary>
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

}
