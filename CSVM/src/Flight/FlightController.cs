using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>Which crash variant the original would play for the surface just hit.
/// The engine chooses natively from the impact surface — the three <c>player_crash_*</c> defs are
/// never <c>CallAnimation</c>-referenced by name — so the choice is ours to reconstruct in
/// <see cref="FlightController.ClassifySurface"/>, which reads the struck body's surface tag.
/// <see cref="Water"/> is a sea dive, <see cref="Ground"/> everything else it can hit;
/// <see cref="Air"/> stays unreachable — it is the no-impact destruct, and nothing shoots the
/// player down yet, so there is no trigger for it.</summary>
public enum CrashSurface { Air, Ground, Water }

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

    /// <summary>Spins the plane's propeller/rotor blur discs; advanced each frame,
    /// throttle-scaled. Null if the model has no propeller nodes.</summary>
    public PropAnimator? Props;

    /// <summary>Flashes the plane's wingtip flares on the original's 1.5 s cycle; advanced
    /// each frame. Null if the model has no wing-flare nodes.</summary>
    public WingLightBlinker? WingLights;

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

    /// <summary>The selected-weapon text readout (E36): the gun group + rocket type and their live
    /// ammo, drawn in the game's HUD font from the <c>MSG_HUD_GUNGAUGE</c>/<c>MSG_HUD_MISSLES</c>
    /// templates. Added to the HUD canvas and fed each frame; null (no font / no loadout) hides it.</summary>
    public WeaponReadout? WeaponReadout;

    /// <summary>The gun aiming reticle (E37): the game's pipper drawn at the SELECTED gun group's
    /// ballistic impact point at the convergence distance — trailing the nose in a hard turn, on the
    /// rounds in steady flight. Added to the HUD canvas and fed the world impact point each frame;
    /// null when the plane carries no firable gun (or the reticle texture was absent).</summary>
    public ImpactReticle? Reticle;

    /// <summary>The airframe collision boxes (fuselage/wings/tail), swept along each
    /// physics frame's motion so wingtips and tail collide with obstacles. Null falls
    /// back to the old center-ray-only test.</summary>
    public PlaneCollider? Collider;

    /// <summary>Per-part hit points from the vehicle def's destroyable_parts.
    /// When set, collisions below the crash threshold damage the struck
    /// part and the plane flies on; null keeps the old any-hit-crashes behavior.</summary>
    public PlaneDamage? Damage;

    /// <summary>C27: applies a plane collision to the struck world node, returning true iff it was a
    /// <c>WeaponOrCollideHit</c> destructible (the 44 facades/windows/agyrobus) — in which case the
    /// object breaks and the plane flies THROUGH it. Wired to <c>AnimRuntime.CollideDamageAt</c>; null
    /// (a viewer/static build with no world runtime) makes every collision solid, as before.</summary>
    public System.Func<Node?, float, bool>? CollideDamageSink;

    /// <summary>Plays a named effect def at a world point through the session's world-effects
    /// runtime — the survivable graze's authored <c>touchdown_*</c> reaction (B3). Same sink shape
    /// as <c>ProjectilePool.EffectSink</c>; null (no world, or a build with no effects runtime)
    /// leaves the scrape's sound without its sparks/dust/splash.</summary>
    public System.Action<string, Vector3>? GrazeEffectSink;

    /// <summary>Visible damage: torn-skin pdpanel flips + the low-HP
    /// smoke/fire trail, driven from the data's injure_anims thresholds. Optional.</summary>
    public DamageVisuals? Visuals;

    /// <summary>This plane's stock loadout bound to its model — the gun groups (with independent
    /// ammo counters) + hardpoints the firing code draws from. Null disables weapons.</summary>
    public Loadout? Loadout;

    /// <summary>The shared world's projectile/effect pool guns and hardpoints fire into. Null
    /// disables weapons.</summary>
    public ProjectilePool? Projectiles;

    /// <summary>D44: the FLYOUT-model rockets mounted under the wings, one per loaded pylon, hidden as
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
    /// <c>player_crash_dirt</c> the wreck breaks apart with the <c>pieceN</c> ballistics, sparks,
    /// fireball cluster, black smokeball, dirt burst and burning-debris arcs all firing from the
    /// extracted data; on <c>player_crash_water</c> the splash, ripple and steam spray do instead.
    /// It advances itself (its
    /// own <c>_Process</c>). The standard crash path (built by <c>GameSession</c> for every flown
    /// plane); null only when the crash program/scene were unavailable, and the plane then just
    /// hides on a crash.</summary>
    public AnimRuntime? CrashRuntime;

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

    /// <summary>The splitscreen stunt race this plane is one seat of, or null when
    /// flying solo. Set, clearing every zone parks this player at the finish while the others fly
    /// on, and R only becomes a rematch once the whole field is in — a rematch restarts every
    /// player, so it goes through <see cref="RestartRace"/> rather than this plane alone.</summary>
    public StuntRace? Race;

    /// <summary>Restarts the whole race (the session owns every player's plane, so it does the
    /// work). Invoked when a player presses R on the shared results board.</summary>
    public Action? RestartRace;

    /// <summary>0-based player index — this plane's seat in the race and its pane, and the
    /// identity a round it fired carries (<c>ProjectilePool.Spawn</c>'s shooter id).</summary>
    public int PlayerIndex;

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

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float SpawnThrottle = 0.5f;   // the original always spawns at half throttle (confirmed in-game, all planes)
    private const float SpawnSpeed = 53.6f;     // m/s ≈ 120 mph. PLACEHOLDER: the original's spawn speed is
                                                // plane-dependent (TODO — kept fixed for now per user); the
                                                // plane accelerates from here toward its cruise
    private const float GunConvergenceDist = 250f; // m — the range the gun reticle projects the
                                                   // ballistic solution to (the sight's zero range).
                                                   // NOT in the data (weapons.json carries no
                                                   // convergence field; guns have RANGE 1000) — a
                                                   // TUNE pending an original-game playtest (E37).
    private const float UnderMapY = 0f;        // C1 terrain sits at y≈100+; below this we're lost
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)
    private const float AutoRespawnDelay = 1.5f; // s a HoldInput run stays crashed before auto-respawn
    private const float DebugFinishStagger = 1.5f; // s between players' forced finishes (--debug-scoreboard in a race)

    // Collision severity (all TUNE): impact speed along the contact
    // normal decides between a survivable graze and a crash. A graze damages the
    // struck part (quadratic in severity), slides the velocity along the surface
    // with some tangential loss, and kicks the attitude.
    private const float CrashSpeed = 25f;        // m/s along the normal ⇒ outright crash
    private const float CollideDamagePerVn = 8f;  // C27: HEALTH_DAMAGE a collision deals to a WeaponOrCollideHit
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

    private FlightModel _model = null!;
    private CameraController _cam = null!;
    private Label _hud = null!;
    private float _hudPaneFactor = 1f;            // last applied splitscreen shrink (1 = single player)
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;
    private bool _crashed;                       // frozen at the impact point, waiting for respawn
    private WarningShotCue? _warningShots;       // the near-miss cue's shipped accumulator (BL-087)
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
    private GunState[]? _gunStates;              // per firable gun group: fire clock, muzzle rotation, empty-warned
    private GunGroup[] _firableGuns = Array.Empty<GunGroup>(); // the firable gun groups in _gunSel/_gunStates order (live ammo)
    private bool _firePrev;                      // previous frame's fire button (immediate first shot on press)
    private bool _gunLoopOn;                     // the firing loop sound is currently playing
    private float _rocketCooldown;               // s until the next rocket may launch (FIRE_RATE gate, one at a time)
    private int _selectedPylon;                  // the hardpoint H selects and rockets fire from; auto-advances to the next armed pylon as each empties
    private bool _rocketFirePrev;                // previous frame's rocket button (one rocket per discrete pull)
    private bool _rocketDryWarned;               // the all-pylons-empty cue has already sounded
    private int _rocketsLaunched;                // verification breadcrumb: the first few launches log their pylon
    private int _gunSel;                         // gun selector: 0-based firable group that fires (only ONE at a time)
    private bool _gunSelPrev;                    // edge detection for the gun-selector button
    private bool _rocketSelPrev;                 // edge detection for the hardpoint-selector (H) button
    private bool _held;                          // Held's backing field — the airframe is pinned (weapon lab)
    private bool _cameraOwned;                   // CameraOwned's backing field — the lab's free camera has the view
    private bool _orbitPrev;                     // edge detection for entering the orbit (halt or hold)
    private bool _reseedOrbit;                   // the free camera handed the view back; re-seed from where it left it
    private bool _heldPinned;                    // the pinned pose below is valid (captured on the first held step)
    private Vector3 _heldPos;                    // the pinned position, re-applied through the model every held step
    private Basis _heldAttitude;                 // the pinned attitude, ditto

    // The gungauge / missilegauge HUD state (E35), pushed to GaugeCluster each frame. Persistent
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

    /// <summary>The weapon lab (A2): the airframe holds the pose it had when this was set — it does
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

    /// <summary>The weapon lab's free camera (D8): while set, this controller writes NOTHING to the
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

    public void Setup(FlightModel model, Camera3D camera, CamParams camParams,
        Vector3 spawnPos, Vector3 spawnLookAt)
    {
        _model = model;
        _cam = new CameraController(camera, camParams, KeyDown, PinnedView);
        _spawnPos = spawnPos;
        _spawnAttitude = Basis.LookingAt((spawnLookAt - spawnPos).Normalized(), Vector3.Up);
        _warningShots = new WarningShotCue(model.Stats.WarningShotMax,
            model.Stats.WarningShotDissipation, model.Stats.WarningShotInterval);
        Respawn();
    }

    /// <summary>Registers this aircraft with the shared pool as a near-miss cue target (BL-087):
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
        var canvas = new CanvasLayer();
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
        if (Scoreboard != null)
            canvas.AddChild(Scoreboard); // end-of-run results, drawn over everything
        if (FontTest != null)
            canvas.AddChild(FontTest); // --hud-font-test: the E34 bitmap-font proof overlay
        // Splitscreen parents the HUD into this player's SubViewport so it draws in that pane
        // only (and scales off the pane's height); single player keeps it on this node.
        (HudParent ?? this).AddChild(canvas);
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
        SnapCamera();

        // One firing-state slot per firable gun group (turrets excluded — inert in M3).
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
            // (--ammo=N) wins over config.json so the knob survives --det (DET-8 drops config.json).
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
            _gunStates = new GunState[n];
            for (int i = 0; i < n; i++)
            {
                _gunStates[i] = new GunState();
            }
            // Apply the --gun-select testing override (0-based group index, clamped into range).
            _gunSel = n > 0 ? Mathf.Clamp(InitialGunSelect, 0, n - 1) : 0;

            // Bind the two weapon gauges (E35) — only for a system this plane actually carries.
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
        if (PlaneModel != null)
            PlaneModel.Visible = true;
        _throttle = SpawnThrottle;
        _model.Reset(_spawnPos, _spawnAttitude, SpawnSpeed, _throttle);
        _simPrev = _simCurr = _renderPose = new Transform3D(_model.Attitude, _model.Position);
        GlobalTransform = _simCurr;
        if (_cam != null && IsInsideTree())
            SnapCamera();
    }

    /// <summary>Weapon lab (A2): pin the held airframe at <paramref name="pos"/> with its nose on
    /// <paramref name="lookAt"/>, at zero speed — the lab's re-park (click-to-place, C6, and the
    /// scripted <c>--weapon-target=</c> twin, C7). Goes in through the same
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

    /// <summary>Weapon lab (B5): point the gun selector at a firable gun group (0-based, clamped) —
    /// the programmatic twin of G / D-pad Left, which only cycles. Interactively that cycle still
    /// wins the next time it is pressed; <see cref="InitialGunSelect"/> is the _Ready-time
    /// equivalent and cannot be re-applied once the rig is built.</summary>
    public void SelectGunGroup(int index)
    {
        int n = _firableGuns.Length;
        _gunSel = n > 0 ? Mathf.Clamp(index, 0, n - 1) : 0;
    }

    /// <summary>Weapon lab (B5): point the hardpoint selector at a pylon (0-based, clamped) — the
    /// programmatic twin of H. Unlike H this lands on an EMPTY pylon too (the lab picks a mount to
    /// look at, not a mount to fire); the firing path's own
    /// <see cref="NextArmedHardpoint"/> scan still advances off it when the trigger is pulled.</summary>
    public void SelectPylon(int index)
    {
        int n = Loadout?.Hardpoints.Count ?? 0;
        _selectedPylon = n > 0 ? Mathf.Clamp(index, 0, n - 1) : 0;
    }

    /// <summary>--crash[=frame]: forces this player's crash outside any live collision — the only
    /// headless trigger for the per-player crash rig. <c>hitName</c>/<c>part</c> are nominal and
    /// there is no struck body, so <see cref="ClassifySurface"/> resolves
    /// <see cref="CrashSurface.Ground"/> however the plane is posed — the water variant needs a real
    /// dive into a <c>water</c>-tagged collider. A no-op once already crashed.</summary>
    public void DebugForceCrash()
    {
        if (!_crashed)
            Crash(_model.Position, "debug-crash", "test", null);
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

        // Run complete: the flight sim freezes in place (the chase camera in _Process still holds
        // on the plane). Solo, the scoreboard is up and R (gamepad Y/A) starts a fresh run — the
        // deliberate opposite of a mid-run respawn, clearing the clock + every completed zone.
        // In a race this player is simply parked at the finish while the rest
        // of the field flies on, and R only means "rematch" — restarting everybody — once the
        // last pilot is in. Checked before the crash branch so completing the final zone always
        // restarts cleanly.
        if (Stunt is { AllComplete: true })
        {
            _simPrev = _simCurr;   // hold the finish pose — no stale pair left to interpolate
            // Unattended scripted runs rematch on a timer, the same rule as the crash branch's
            // auto-respawn below — so a --hold race soak-test keeps racing instead of parking on
            // the board forever. Player 1 alone runs the timer; the rematch restarts everybody.
            bool autoRematch = HoldSegments != null && Race is { AllFinished: true } && PlayerIndex == 0
                && (_autoRestartIn -= dt) <= 0f;
            if (RespawnPressed() || autoRematch)
            {
                if (Race == null)
                    RestartStuntRun();
                else if (Race.AllFinished)
                    RestartRace?.Invoke();
            }
            return;
        }
        _autoRestartIn = AutoRespawnDelay; // re-armed while the run is live

        if (_crashed)
        {
            // The wreck + effects run on the crash AnimRuntime, which advances itself in its own
            // _Process through this crash freeze (motions, the played def, every puffer) — but not
            // through a clock halt, which stops that runtime with everything else, so P during a
            // crash catches the wreck mid-break-up. The airframe stays frozen at the impact point
            // until the pilot respawns (R / gamepad Y or A); unattended HoldSegments runs respawn
            // on a timer instead
            if (RespawnPressed() || (HoldSegments != null && (_autoRespawnIn -= dt) <= 0f))
                Respawn();
            return;
        }

        // Weapon lab (A2): a HELD airframe skips input, the flight model and the whole collision
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
            var input = HoldSegments != null ? NextHoldInput(dt) : ReadKeyboard(dt);
            _lastInput = input;
            _damageCooldown -= dt;
            _grazeReactionCooldown -= dt;
            _model.Step(input, dt);

            // Crash when the frame's flight path runs into solid world geometry (terrain,
            // buildings, trees). The airframe boxes (fuselage/wings/tail) are swept along
            // the frame's motion so a wingtip or tail fin collides, not just the center
            // line; the center ray stays as an anti-tunnelling backstop. Only the shapeless
            // fallback keeps the old nose margin on the ray — with real boxes it would fire
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
            // C27: a collision with a WeaponOrCollideHit destructible (the 44 facades/windows/agyrobus)
            // breaks IT and the plane flies through — apply severity-scaled damage and clear the hit.
            // Every other object (WeaponHit towers/gates, plain geometry) stays solid and falls through
            // to the crash/graze below (decision 6: the 0.01 health marks these as fly-through set dressing).
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

        // Weapons: cycle the two selectors (edge-detected), then advance the fire clocks and spawn
        // into the shared projectile pool.
        CycleWeaponSelectors();
        UpdateGuns(dt);
        UpdateRockets(dt);
        Ordnance?.Update();   // hide a pylon's mounted rocket the moment it fired its last (D44)

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
            // wv = wing verticality |up·Y| (1 level/inverted, 0 knife-edge) — the lift factor
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
        // The orbit camera serves both the P freeze and the weapon lab's HELD airframe (D8): in
        // both the plane is standing still and the point is to fly the view around it. Seeding on
        // the edge starts it where the chase camera left off, so neither entry jumps — and so does
        // the hand-back from the lab's free camera, which leaves the eye somewhere else entirely.
        bool orbiting = halted || Held;
        if ((orbiting && !_orbitPrev) || _reseedOrbit)
        {
            _reseedOrbit = false;
            _cam.SeedOrbit(_model.Position);
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
        if (CameraOwned)
        {
            // The lab's free camera has the view (D8) — every camera write here would fight it.
        }
        else if (orbiting)
        {
            // The orbit camera runs on wall time through a halt on purpose: the point of the
            // freeze is to fly the camera around a stopped world. A held airframe is the same
            // situation with the world still running.
            var (yawIn, pitchIn, zoomIn) = OrbitInput();
            _cam.Orbit((float)delta, _model.Position, yawIn, pitchIn, zoomIn);
        }
        else
        {
            int view = _cam.ActiveView();
            if (view >= 0)
            {
                _cam.FixedView(view, _renderPose);
            }
            else
            {
                // The chase camera trails the plane by exponential smoothing, so its pose is a
                // function of the dt it is fed. On wall time that makes a scripted flight capture
                // frame-rate dependent even when the simulation underneath it is pinned — the pose
                // has to come off the same clock as the plane it follows.
                _cam.Chase(simDt, _model.Position, _model.Attitude);
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
        if (Gauges != null)
        {
            Gauges.SpeedMph = mph;
            Gauges.AltitudeFt = ft;
            // A held plane sits at 0 m/s, which is below every stall speed — but it is pinned, not
            // stalling, so the gauge (and the HUD line below) stay quiet in the lab.
            // The lamp is the WARNING (0.30 fd), which leads the nose-drop the model flies at 0.25;
            // the fraction beside it is what ramps its blink rate.
            Gauges.StallWarning = !_crashed && !halted && !_held && _model.IsStallWarned();
            Gauges.StallFrac = _model.StallFraction;
        }
        // Feeds the E35 gauges (if built) and the E36 readout (if built) — both draw from the live
        // loadout, so this runs whenever there is one, independent of the dial cluster.
        UpdateWeaponGauges();
        // Points the E37 gun reticle at the selected group's ballistic impact point (if built).
        UpdateReticle();
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
        // line still ran into the top-centre compass tape in a 4P quarter pane — break the
        // throttle onto its own line there. Full screen keeps the original one-liner.
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
        // The weapon ammo readout is now the E35 gauges + the E36 WeaponReadout (drawn in the game's
        // own HUD font from MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES), not this text block.
        // Stunt run status now lives in the marker HUD; keep the compact text line only
        // as a fallback if the marker somehow wasn't built.
        if (Stunt != null && Marker == null)
            _hud.Text += $"\n{Stunt.StatusLine()}";
        if (halted)
        {
            _hud.Text += "\n⏸ PAUSED — orbit: WASD/arrows · zoom: Shift/Ctrl · P (gamepad Start) resume · . step one frame";
        }
        else if (_crashed)
        {
            _hud.Text += "\n⚠ CRASHED — PRESS R (GAMEPAD Y/A) TO RESPAWN";
        }
        else
        {
            Audio?.Update(simDt, _model.Throttle, _model.Speed / _model.Stats.FdSpeed,
                1f - (Damage?.WorstFraction ?? 1f));
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
            Visuals?.Update(_model.Position, _model.Attitude); // smoke/fire trail emission
        }
    }

    /// <summary>The rocket name the E36 readout shows: the resolved <c>MSG_WEAP_*</c> display name
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

    /// <summary>Which crash variant the original would play for the surface just hit, from the
    /// struck body's <see cref="SceneBuilder.SurfaceMeta"/> tag through
    /// <see cref="ProjectilePool.ClassifySurface"/> — the one classifier the collision-consequence
    /// paths share, so a crash, a graze and a round never disagree about what they hit. A
    /// <c>water</c>-tagged body is the sea dive (<c>player_crash_water</c>); terrain and buildings
    /// are alike the <c>_dirt</c> variant. <see cref="CrashSurface.Air"/> is not reachable from
    /// here at all: it is the no-impact destruct, which needs a mid-air destruct trigger rather
    /// than a struck body. A null body (the headless <c>--crash</c> force) reads Ground.</summary>
    private static CrashSurface ClassifySurface(Node? hitBody) =>
        ProjectilePool.ClassifySurface(hitBody) == SurfaceClass.Water
            ? CrashSurface.Water
            : CrashSurface.Ground;

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
    private bool RocketFirePressed() => AutoFireRockets || KeyDown(Key.F) || PadPressed(JoyButton.A);

    /// <summary>G / gamepad D-pad Left — cycles the gun selector through the firable groups (1 → 2 →
    /// … → 1). Only ONE group fires at a time; the gun trigger fires the selected one. Caller edge-detects.</summary>
    private bool GunSelectPressed() => KeyDown(Key.G) || PadPressed(JoyButton.DpadLeft);

    /// <summary>H / gamepad D-pad Right — moves the hardpoint selector to the next pylon that still
    /// carries ordnance (each pylon is its own selectable slot, whatever it loads — even a plane with
    /// one uniform ordnance type). The rocket trigger then launches from the selected pylon. Caller
    /// edge-detects.</summary>
    private bool RocketSelectPressed() => KeyDown(Key.H) || PadPressed(JoyButton.DpadRight);

    /// <summary>Advances each weapon selector on the rising edge of its button to the next armed slot
    /// (one active at a time, skipping empties): the gun selector across the firable groups, the
    /// hardpoint selector across the pylons. Both cursors also auto-advance on their own when the
    /// selected slot empties (in `UpdateGuns`/`UpdateRockets`). The gun pick persists across a respawn;
    /// the pylon cursor doubles as the firing cursor, so a refill resets it to pylon 0 with the
    /// ammo.</summary>
    private void CycleWeaponSelectors()
    {
        bool gunSel = GunSelectPressed();
        if (gunSel && !_gunSelPrev && _gunStates is { Length: > 1 })
        {
            _gunSel = WeaponCursor.NextSelectable(
                _firableGuns.Length, i => _firableGuns[i].Ammo, _gunSel, InfiniteAmmo);
        }
        _gunSelPrev = gunSel;

        bool rocketSel = RocketSelectPressed();
        if (rocketSel && !_rocketSelPrev && Loadout is { Hardpoints.Count: > 1 } l)
        {
            _selectedPylon = WeaponCursor.NextSelectable(
                l.Hardpoints.Count, i => l.Hardpoints[i].Ammo, _selectedPylon, InfiniteAmmo);
        }
        _rocketSelPrev = rocketSel;
    }

    /// <summary>Feeds the two cockpit weapon gauges (E35) from the same live ammo the firing code
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

        // Guns: the SELECTED firable group. The gauge takes the caliber+ammo short NAME and the belt
        // fractions; the readout (E36) takes the group's mount name and its per-group rounds.
        GunGroup? selectedGun = null;
        int firable = 0;
        _gunGaugeSlots.Clear();
        foreach (var g in Loadout.FirableGuns)
        {
            _gunGaugeSlots.Add(g.Capacity > 0 ? (float)g.Ammo / g.Capacity : 0f);
            if (firable == _gunSel)
            {
                selectedGun = g;
            }
            firable++;
        }
        if (_gunGaugeState != null)
        {
            _gunGaugeState.Selected = firable > 0 ? Mathf.Clamp(_gunSel, 0, firable - 1) : 0;
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
            int sel = Mathf.Clamp(_selectedPylon, 0, hps.Count - 1);
            _missileGaugeSlots.Clear();
            for (int i = 0; i < hps.Count; i++)
            {
                var h = hps[i];
                _missileGaugeSlots.Add(h.Capacity > 0 ? (float)h.Ammo / h.Capacity : 0f);
            }
            WeaponDef typeWeapon = hps[sel].Weapon;
            int perPylon = hps[sel].Ammo;
            if (_missileGaugeState != null)
            {
                _missileGaugeState.Selected = sel;
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

    /// <summary>Points the gun reticle (E37) at the SELECTED gun group's ballistic impact point at
    /// the convergence distance. It integrates the round exactly as <see cref="ProjectilePool"/>
    /// fires it — muzzle-forward × <c>VELOCITY</c> plus the plane's inherited velocity, stepped
    /// through any <c>ACCELERATION</c>/<c>GRAVITY</c> — so the pipper and the rounds agree; it drops
    /// only the random <c>CANNON_SPREAD</c> (the reticle marks the cone centre). Hidden while crashed
    /// or when the plane has no firable gun / no muzzle to fire from. No-op without a reticle.</summary>
    private void UpdateReticle()
    {
        if (Reticle == null)
        {
            return;
        }
        // Hidden while crashed (the airframe is gone) — a stale pipper must not hang in the sky —
        // and when there is nothing to aim.
        if (_crashed || Loadout == null || _gunStates == null)
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
            if (gi == _gunSel)
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
        var origin = Vector3.Zero;
        var forward = Vector3.Zero;
        foreach (var m in sel.Muzzles)
        {
            var xf = m.GlobalTransform;
            origin += xf.Origin;
            forward += -xf.Basis.Z.Normalized(); // each muzzle's own aim (as ProjectilePool.Spawn)
        }
        origin /= sel.Muzzles.Count;
        if (forward.LengthSquared() < 1e-6f)
        {
            Reticle.Active = false;
            return;
        }
        forward = forward.Normalized();
        var inheritVel = _model.VelocityDir * _model.Speed;
        Reticle.ImpactPoint = BallisticImpactPoint(sel.Weapon, origin, forward, inheritVel,
            GunConvergenceDist);
        Reticle.Active = true;
    }

    /// <summary>Advances every gun group's fire clock: while the trigger is held, the selected group
    /// spawns rounds at its <c>FIRE_RATE</c> (alternating muzzles so the group's total rate equals it),
    /// drawing from its own ammo counter. When the selected group runs dry the selection auto-advances
    /// to the next group with ammo (the moment it empties); the empty-clip cue sounds only once every
    /// group is spent. Also drives the firing loop sound. No-op without a loadout / pool.</summary>
    private void UpdateGuns(float dt)
    {
        if (Loadout == null || Projectiles == null || _gunStates == null)
        {
            return;
        }
        bool fire = FirePressed();
        bool wantLoop = false;
        var inheritVel = _model.VelocityDir * _model.Speed;
        string? loopSound = null;
        int gi = 0;
        foreach (var g in Loadout.FirableGuns)
        {
            int groupIndex = gi;
            var st = _gunStates[gi++];
            // Only the selected gun group fires — one at a time (the original's behaviour).
            bool selected = groupIndex == _gunSel;
            if (!fire || !selected || g.Weapon.FireRate <= 0f || g.Muzzles.Count == 0)
            {
                st.Accum = 0f;
                continue;
            }
            float interval = 1f / g.Weapon.FireRate;
            if (!_firePrev)
            {
                st.Accum = interval; // the first shot leaves the barrel the instant the trigger goes down
            }
            st.Accum += dt;
            bool hasAmmo = g.Ammo > 0 || InfiniteAmmo;
            if (hasAmmo)
            {
                wantLoop = true;
                loopSound ??= g.Weapon.LoopedSoundName;
            }
            while (st.Accum >= interval)
            {
                st.Accum -= interval;
                if (g.Ammo > 0 || InfiniteAmmo)
                {
                    var muzzle = g.Muzzles[st.NextMuzzle % g.Muzzles.Count];
                    st.NextMuzzle++;
                    Projectiles.Spawn(g.Weapon, muzzle.GlobalTransform, inheritVel, PlayerIndex);
                    if (!InfiniteAmmo)
                    {
                        g.Ammo--;
                    }
                    st.Warned = false; // it fired a real round — re-arm the dry warning
                    if (!st.LoggedFirst)
                    {
                        st.LoggedFirst = true;   // verification breadcrumb: which groups actually fire
                        GD.Print($"gun group {groupIndex + 1} ({g.Mount}, {g.Weapon.Caliber ?? 0}-cal " +
                                 $"{g.Weapon.Id}) firing");
                    }
                }
                else
                {
                    // The selected group just ran dry — switch to the next group that still has ammo
                    // the moment it empties, not on the next trigger pull. Only when no group has ammo
                    // left does the dry cue sound.
                    int next = WeaponCursor.NextArmed(
                        _firableGuns.Length, i => _firableGuns[i].Ammo, _gunSel, InfiniteAmmo);
                    if (next >= 0)
                    {
                        _gunSel = next;
                    }
                    else if (!st.Warned)
                    {
                        st.Warned = true;
                        Audio?.PlayEmptyClip();
                    }
                    st.Accum = 0f;
                    break;
                }
            }
        }
        if (wantLoop)
        {
            // Called every frame the trigger is held, not just on the silence-to-firing edge:
            // switching gun groups mid-burst changes loopSound while wantLoop stays true, and
            // StartGunLoop only rebuilds the player when the name it's given actually changes.
            _gunLoopOn = true;
            Audio?.StartGunLoop(loopSound);
        }
        else if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        _firePrev = fire;
    }

    /// <summary>Launches rockets from the hardpoints: one per discrete trigger pull, drawn from the
    /// selected pylon (H picks it; it drains fully, then the cursor auto-advances to the next armed
    /// pylon the instant it empties), gated by the weapon's <c>FIRE_RATE</c> — 1.0/s for every rocket,
    /// i.e. one launch per second. Depletes that pylon's own counter; a pull with every pylon empty
    /// sounds the dry cue once. No-op without hardpoints / a pool.</summary>
    private void UpdateRockets(float dt)
    {
        if (Loadout == null || Projectiles == null || Loadout.Hardpoints.Count == 0)
        {
            return;
        }
        if (_rocketCooldown > 0f)
        {
            _rocketCooldown -= dt;
        }
        bool fire = RocketFirePressed();
        // A human pull fires one rocket; holding does not auto-repeat. Only --fire-rockets (soak
        // runs) auto-repeats — and either way the FIRE_RATE cooldown caps the launch rate.
        bool pull = AutoFireRockets ? fire : (fire && !_rocketFirePrev);
        _rocketFirePrev = fire;
        if (!pull)
        {
            return;
        }
        var hp = NextArmedHardpoint();
        if (hp == null)
        {
            if (!_rocketDryWarned)
            {
                _rocketDryWarned = true;
                Audio?.PlayEmptyClip();
                GD.Print("rocket: dry pull, all pylons empty — empty-clip cue");
            }
            return;
        }
        if (_rocketCooldown > 0f)
        {
            return;
        }
        _rocketDryWarned = false;
        var inheritVel = _model.VelocityDir * _model.Speed;
        Projectiles.Spawn(hp.Weapon, hp.Pylon.GlobalTransform, inheritVel, PlayerIndex);
        if (!InfiniteAmmo)
        {
            hp.Ammo--;
            // Advance the moment the selected pylon empties — not on the next trigger pull — so the
            // gauge arrow leaves the spent pylon straight away. NextArmed keeps the cursor while the
            // pylon still has rounds and steps to the next armed pylon once it is dry; -1 (all empty)
            // leaves it put so the next pull sounds the dry cue.
            var hps = Loadout.Hardpoints;
            int next = WeaponCursor.NextArmed(hps.Count, i => hps[i].Ammo, _selectedPylon, false);
            if (next >= 0)
            {
                _selectedPylon = next;
            }
        }
        _rocketCooldown = hp.Weapon.FireRate > 0f ? 1f / hp.Weapon.FireRate : 1f;
        if (_rocketsLaunched < 12)
        {
            _rocketsLaunched++;
            GD.Print($"rocket: {hp.Weapon.Id} ({hp.Weapon.Name}) from pylon{hp.Index}, " +
                     $"{(InfiniteAmmo ? "∞" : hp.Ammo.ToString())} left on that pylon");
        }
    }

    /// <summary>The hardpoint the next rocket fires from: the selected pylon while it still has
    /// ordnance, else the next armed pylon scanning from it and wrapping. Updates
    /// <see cref="_selectedPylon"/> to the pylon it returns. The cursor normally already sits on an
    /// armed pylon (the firing path advances it the instant one empties, and H lands only on armed
    /// pylons), so this is a confirming scan; it still self-heals if the cursor is somehow left on a
    /// spent pylon. Null when every pylon is empty. With <c>--infinite-ammo</c> the selected pylon
    /// always qualifies.</summary>
    private Hardpoint? NextArmedHardpoint()
    {
        var hps = Loadout!.Hardpoints;
        int idx = WeaponCursor.NextArmed(hps.Count, i => hps[i].Ammo, _selectedPylon, InfiniteAmmo);
        if (idx < 0)
        {
            return null;
        }
        _selectedPylon = idx;
        return hps[idx];
    }

    /// <summary>Refills every gun group to its full load and re-arms the dry warnings (respawn).</summary>
    private void RefillWeapons()
    {
        if (Loadout == null)
        {
            return;
        }
        foreach (var g in Loadout.Guns)
        {
            g.Ammo = g.Capacity;
        }
        foreach (var h in Loadout.Hardpoints)
        {
            h.Ammo = h.Capacity;
        }
        if (_gunStates != null)
        {
            foreach (var st in _gunStates)
            {
                st.Accum = 0f;
                st.Warned = false;
                st.NextMuzzle = 0;
            }
        }
        _rocketCooldown = 0f;
        _selectedPylon = 0;
        _rocketFirePrev = false;
        _rocketDryWarned = false;
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

    /// <summary>True if the segment crosses any static world collider; on a hit,
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
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
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

    private void Crash(Vector3 impact, string hitName, string part, Node? hitBody)
    {
        _crashed = true;
        _autoRespawnIn = AutoRespawnDelay;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        // Crash freezes the airframe before UpdateGuns runs again this frame (the early _crashed
        // return above it), so a held trigger's loop would otherwise keep playing under the wreck
        // until respawn — nothing else ever calls StopGunLoop while _crashed is true.
        if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        var surface = ClassifySurface(hitBody);
        bool water = surface == CrashSurface.Water;
        Audio?.OnCrash();
        // The boom under the plane explosion, per variant: the dirt def Sounds snd_exp_ground_a
        // itself, and the water def's snd_exp_water_a sits one level down, in the plane_big_splash
        // it calls. Both runtimes treat SOUND as handled-elsewhere, so both come from here.
        if (water)
            Audio?.OnWaterExplosion();
        else
            Audio?.OnGroundExplosion();
        if (CrashRuntime != null)
        {
            // Data-driven crash: PLAY the compiled def on this plane's scoped crash
            // runtime. The def hides healthy/dontmove/markers, shows the destroyed wreck, launches
            // the pieceN ballistics, and fires every authored effect (sparks, the fireball cluster,
            // the black smokeball, the dirt burst, the burning-debris arcs). Audio stays the same
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
            CrashRuntime.Play(water ? "player_crash_water" : "player_crash_dirt", CrashAnchor, applyReset: false);
        }
        GD.Print($"CRASH into {hitName} ({part}) surface={surface} impact=({impact.X:0},{impact.Y:0},{impact.Z:0}) " +
                 $"pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) " +
                 $"spd={_model.Speed:0} m/s — waiting for respawn");
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
    /// crash (severe impact, a critical part destroyed, or no damage data), true =
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
            if (state != null)
            {
                Visuals?.OnPartDamage(dataPart, state.Fraction);
                Gauges?.OnPartDamage(dataPart); // damage dial: hit zone blinks 5 s
                if (state.Hp <= 0f && state.Def.Critical)
                {
                    GD.Print($"part destroyed: {dataPart} (critical) — " +
                             $"vn={vn:0.0} m/s into {hitName}");
                    return false; // the data's meaning: a dead critical part downs the plane
                }
                _damageFlashText = $"⚠ IMPACT {dataPart.ToUpperInvariant()} {state.Fraction * 100f:0}%";
                _damageFlash = DamageFlashTime;
                GD.Print($"graze ({part}→{dataPart}): {hitName} " +
                         $"vn={vn:0.0} m/s dmg={dmg:0.0} " +
                         $"armor={state.Armor:0.0}/{state.Def.MaxArmor:0} hp={state.Hp:0.0}/{state.Def.MaxHp:0}");
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
                    };
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

    /// <summary>The survivable scrape's authored per-surface reaction (B3): the struck collider's
    /// <see cref="SceneBuilder.SurfaceMeta"/> class picks one of touchdown.zrd's three defs —
    /// <c>touchdown_default</c> sparks off a hard building surface, <c>touchdown_dirt</c> raises
    /// dust off terrain, <c>touchdown_water</c> splashes — which the world-effects runtime stages at
    /// the contact point, alongside the sequence's own SOUND. Classification is
    /// <see cref="ProjectilePool.ClassifySurface"/>, the same read a round's impact makes.
    ///
    /// <para>One reaction per <see cref="GrazeReactionInterval"/> rather than per frame: a scrape
    /// confirms a hit every physics frame, and each call restarts the def and re-fires its sound.
    /// The interval is the authored puffer window, so a long slide reads as a repeating reaction
    /// instead of a stutter.</para>
    ///
    /// <para><b>Where the def is staged is an open A/B</b> (<c>graze.siteAtContact</c>, default the
    /// CONTACT POINT — the user's judgement at the controls, 2026-08-01). The data argues for the
    /// aircraft: every offset the def carries is authored against <c>MAIN_ROOT_NODE</c>, the node
    /// the engine invokes it on, and its SOUND is <c>AT_NODE MAIN_ROOT_NODE</c> too — staging at the
    /// contact point puts the puffer's own −0.5 Y half a metre UNDER the struck surface. But the
    /// puffs are scaled up in practice (<c>puffer.*SizeScale</c>), which lifts them clear of the
    /// burial anyway, and smoke visibly leaving the SURFACE reads better than smoke leaving the
    /// plane. Set the flag false to stage on the aircraft instead.</para>
    ///
    /// <para>⚠ Effects (BL-061) keep ONE live instance per def across the session — two players
    /// scraping at once collapse onto the later site, as every PlayEffectAt caller does.</para></summary>
    private void GrazeReaction(Vector3 impact, string hitName, Node? hitBody)
    {
        if (_grazeReactionCooldown > 0f)
            return;
        _grazeReactionCooldown = GrazeReactionInterval;
        var surface = ProjectilePool.ClassifySurface(hitBody);
        // Buildings are the hard surface the spark variant is for; water splashes; everything else
        // (terrain, the unclassified majority) is dirt.
        string effect = EffectCatalogue.TouchdownFor(surface);
        var site = Config.GetBool("graze.siteAtContact", true) ? impact : _model.Position;
        GrazeEffectSink?.Invoke(effect, site);
        Audio?.OnGraze(surface == SurfaceClass.Water);
        Log.Info("flight", $"graze reaction effect={effect} surface={surface} into={hitName} contact=({impact.X:0},{impact.Y:0},{impact.Z:0}) site=({site.X:0},{site.Y:0},{site.Z:0}) rendered={(GrazeEffectSink != null ? 1 : 0)}");
    }

    /// <summary>Sweeps each airframe box along this frame's motion against the static
    /// world colliders. On a hit, reports the earliest one: contact point + surface
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
            };
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
    // Silent no-op while the lab's free camera owns the view (D8): a respawn or a lab re-park must
    // not yank the eye back onto the plane the tester just flew away from.
    private void SnapCamera()
    {
        if (!CameraOwned)
        {
            _cam.Snap(_model.Position, _model.Attitude, _renderPose);
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
                KeyAxis(Key.Ctrl, Key.Shift) + padZoom);      // Ctrl/RT out, Shift/LT in
    }

    /// <summary>Per gun group's live firing state: the fire-rate accumulator, which muzzle fires
    /// next (rounds alternate left/right so the group's total rate equals FIRE_RATE), and whether
    /// the empty-clip warning has already sounded since it last had ammo.</summary>
    private sealed class GunState
    {
        public float Accum;
        public int NextMuzzle;
        public bool Warned;
        public bool LoggedFirst;   // verification breadcrumb: the group logs its first live round once
    }

}
