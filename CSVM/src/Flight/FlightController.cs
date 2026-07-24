using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Which crash variant the original would play for the surface just hit.
/// The engine chooses natively from the impact surface — the three <c>player_crash_*</c> defs are
/// never <c>CallAnimation</c>-referenced by name — so the choice is ours to reconstruct in
/// <see cref="FlightController.ClassifySurface"/>. Every reachable crash today is a hard non-water
/// surface (<see cref="Ground"/>); <see cref="Air"/> waits on a mid-air destruct trigger (M3) and
/// <see cref="Water"/> on a sea-surface signal the collision system does not yet expose.</summary>
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
    /// compiled <c>player_crash_dirt</c> definition on a crash — the airframe hides, the wreck
    /// breaks apart and the <c>pieceN</c> ballistics, sparks, fireball cluster, black smokeball,
    /// dirt burst and burning-debris arcs all fire from the extracted data. It advances itself (its
    /// own <c>_Process</c>). The standard crash path (built by <c>PlaneViewer</c> for every flown
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

    /// <summary>0-based player index — this plane's seat in the race and its pane.</summary>
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

    private FlightModel _model = null!;
    private Camera3D _camera = null!;
    private Label _hud = null!;
    private float _hudPaneFactor = 1f;            // last applied splitscreen shrink (1 = single player)
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;
    private bool _crashed;                       // frozen at the impact point, waiting for respawn
    private FlightInput _lastInput;              // this physics frame's stick input (drives the surfaces)
    private float _autoRespawnIn;                // s until auto-respawn (HoldSegments runs only)
    private float _autoRestartIn = AutoRespawnDelay; // s until auto-rematch on a finished race (HoldSegments runs only)
    private float _holdElapsed;                  // sim time into the HoldSegments sequence
    private bool _paused;                        // debug screenshot freeze (P): whole sim halts in place
    private bool _pausePrev;                     // previous frame's pause-key state (edge detection)
    private bool _cyclePrev;                     // previous frame's stunt cycle-target key state (edge detection)
    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private ImmediateMesh? _probe;               // debug collision-probe line
    private float _damageCooldown;               // s left before the next HP subtraction
    private float _damageFlash;                  // s left on the HUD impact line
    private string _damageFlashText = "";
    private GunState[]? _gunStates;              // per firable gun group: fire clock, muzzle rotation, empty-warned
    private bool _firePrev;                      // previous frame's fire button (immediate first shot on press)
    private bool _gunLoopOn;                     // the firing loop sound is currently playing
    private float _rocketCooldown;               // s until the next rocket may launch (FIRE_RATE gate, one at a time)
    private int _nextPylon;                      // which hardpoint sources the next rocket (cycles across pylons)
    private bool _rocketFirePrev;                // previous frame's rocket button (one rocket per discrete pull)
    private bool _rocketDryWarned;               // the all-pylons-empty cue has already sounded
    private int _rocketsLaunched;                // verification breadcrumb: the first few launches log their pylon
    private int _gunSel;                         // gun selector: 0-based firable group that fires (only ONE at a time)
    private bool _gunSelPrev;                    // edge detection for the gun-selector button
    private int _rocketSel;                      // hardpoint selector: index into _ordnanceTypes (which ordnance fires)
    private bool _rocketSelPrev;                 // edge detection for the hardpoint-selector button
    private string[] _ordnanceTypes = Array.Empty<string>(); // distinct hardpoint weapon ids, in pylon order

    // The gungauge / missilegauge HUD state (E35), pushed to GaugeCluster each frame. Persistent
    // objects mutated in place (the belt-fraction lists too) so the HUD readout costs no per-frame
    // allocation. Null until _Ready binds them, and only for a system the plane actually carries.
    private GaugeCluster.WeaponGauge? _gunGaugeState;
    private GaugeCluster.WeaponGauge? _missileGaugeState;
    private readonly List<float> _gunGaugeSlots = new();
    private readonly List<float> _missileGaugeSlots = new();

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

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float SpawnThrottle = 0.5f;   // the original always spawns at half throttle (confirmed in-game, all planes)
    private const float SpawnSpeed = 53.6f;     // m/s ≈ 120 mph. PLACEHOLDER: the original's spawn speed is
                                                // plane-dependent (TODO — kept fixed for now per user); the
                                                // plane accelerates from here toward its cruise
    private const float CamBack = 16f, CamUp = 4.5f, CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s — position catch-up
    private const float CamRotSmooth = 7f;      // 1/s — orientation (basis) catch-up; a touch of
                                                // lag on fast rolls so they read dynamic (TUNE)
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
    private const float DamageFlashTime = 2.5f;  // s the HUD shows the impact line
    private const float GrazeStopSpeed = 12f;    // m/s — grinding to (near) standstill on the
                                                 // ground explodes the plane (user-reported:
                                                 // a stopped plane sat there collecting 0-dmg kisses)
    private const float EmbedPushOut = 0.3f;     // m per un-embed attempt after a graze
    private const int EmbedTries = 3;            // attempts before giving up ⇒ explode, never tunnel
    private const int HudFontSize = 22;         // text HUD, full-screen (shrunk per splitscreen pane)
    private static readonly Vector2 HudMargin = new(16, 10);
    private const float PropIdleSpin = 0.4f;    // blur discs still turn at zero throttle (windmilling)
    private const float OrbitRateDeg = 70f;     // paused orbit-camera slew (deg/s)
    private const float OrbitZoomRate = 1.6f;   // paused orbit-camera dolly (1/s, exponential)
    private const float OrbitMinDist = 4f, OrbitMaxDist = 150f;

    public void Setup(FlightModel model, Camera3D camera, Vector3 spawnPos, Vector3 spawnLookAt)
    {
        _model = model;
        _camera = camera;
        _spawnPos = spawnPos;
        _spawnAttitude = Basis.LookingAt((spawnLookAt - spawnPos).Normalized(), Vector3.Up);
        Respawn();
    }

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
            int n = 0;
            foreach (var _ in Loadout.FirableGuns)
            {
                n++;
            }
            _gunStates = new GunState[n];
            for (int i = 0; i < n; i++)
            {
                _gunStates[i] = new GunState();
            }
            // The distinct ordnance types across the hardpoints, in pylon order — what the
            // hardpoint selector cycles. Stock loadouts carry one type (all HE), so this is
            // usually a single entry; it grows straight away once mixed loadouts land.
            var types = new List<string>();
            foreach (var h in Loadout.Hardpoints)
            {
                if (!types.Contains(h.Weapon.Id))
                {
                    types.Add(h.Weapon.Id);
                }
            }
            _ordnanceTypes = types.ToArray();
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

    /// <summary>H / gamepad D-pad Right — cycles the hardpoint selector across the loaded ordnance
    /// types. The rocket trigger then launches only the selected type. Caller edge-detects. (Stock
    /// loadouts carry a single ordnance type, so this is a no-op until mixed loadouts land.)</summary>
    private bool RocketSelectPressed() => KeyDown(Key.H) || PadPressed(JoyButton.DpadRight);

    /// <summary>Advances each weapon selector on the rising edge of its button: the gun selector
    /// through the firable groups (one active at a time), the hardpoint selector through the distinct
    /// loaded ordnance types. Both are pure UI state — they survive a respawn (a player's pick is
    /// not ammo).</summary>
    private void CycleWeaponSelectors()
    {
        bool gunSel = GunSelectPressed();
        if (gunSel && !_gunSelPrev && _gunStates is { Length: > 1 })
        {
            _gunSel = (_gunSel + 1) % _gunStates.Length;
        }
        _gunSelPrev = gunSel;

        bool rocketSel = RocketSelectPressed();
        if (rocketSel && !_rocketSelPrev && _ordnanceTypes.Length > 1)
        {
            _rocketSel = (_rocketSel + 1) % _ordnanceTypes.Length;
        }
        _rocketSelPrev = rocketSel;
    }

    /// <summary>Feeds the two cockpit weapon gauges (E35) from the same live ammo the firing code
    /// draws down. The gun gauge shows the SELECTED group (its rounds, its short NAME, and one belt
    /// light per firable group by remaining fraction, the arrow on the selected one); the missile
    /// gauge shows the SELECTED ordnance type's total, its NAME, one belt light per pylon, and points
    /// the arrow at the next pylon that will fire. With <c>--infinite-ammo</c> the counters sit at
    /// capacity (the counters never deplete), so the gauges read full and never step.</summary>
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

        // Rockets: the SELECTED ordnance type / next-to-fire pylon. The count reads that pylon (the
        // arrow's) — per-pylon rounds (a full HE pylon = 3), NOT the sum across pylons; the original's
        // gauge is per-pylon (its Warhawk shows BOOM 3, not 24). The readout takes the rocket's display
        // name (MSG_WEAP_* through Messages, e.g. "High-explosive rocket"), the gauge its short NAME.
        var hps = Loadout.Hardpoints;
        if (hps.Count > 0)
        {
            string? type = _ordnanceTypes.Length > 0
                ? _ordnanceTypes[Mathf.Clamp(_rocketSel, 0, _ordnanceTypes.Length - 1)]
                : null;
            WeaponDef? typeWeapon = null;
            _missileGaugeSlots.Clear();
            for (int i = 0; i < hps.Count; i++)
            {
                var h = hps[i];
                _missileGaugeSlots.Add(h.Capacity > 0 ? (float)h.Ammo / h.Capacity : 0f);
                if ((type == null || h.Weapon.Id == type) && typeWeapon == null)
                {
                    typeWeapon = h.Weapon;
                }
            }
            int next = NextArmedPylon(type);
            int perPylon = next < hps.Count ? hps[next].Ammo : 0;
            if (_missileGaugeState != null)
            {
                _missileGaugeState.Selected = next;
                _missileGaugeState.Count = perPylon;
                _missileGaugeState.Type = typeWeapon?.Name ?? "";
            }
            if (WeaponReadout != null)
            {
                WeaponReadout.MissileName = typeWeapon != null ? RocketReadoutName(typeWeapon) : null;
                WeaponReadout.MissileAmmo = perPylon;
            }
        }
        else if (WeaponReadout != null)
        {
            WeaponReadout.MissileName = null;
        }
    }

    /// <summary>The rocket name the E36 readout shows: the resolved <c>MSG_WEAP_*</c> display name
    /// (e.g. "High-explosive rocket") when it resolved, else the short internal handle ("BOOM") — a
    /// raw, unresolved <c>MSG_*</c> key falls back to the handle rather than being shown verbatim.</summary>
    private static string RocketReadoutName(WeaponDef w) =>
        !string.IsNullOrEmpty(w.DisplayName) && !w.DisplayName.StartsWith("MSG_", StringComparison.Ordinal)
            ? w.DisplayName
            : w.Name;

    /// <summary>The pylon the next rocket would launch from (the arrow target on the missile gauge):
    /// the first armed pylon of <paramref name="type"/> scanning from <see cref="_nextPylon"/> and
    /// wrapping — a read-only mirror of <see cref="NextArmedHardpoint"/> that does NOT advance the
    /// cursor. Falls back to the cursor position when every matching pylon is empty.</summary>
    private int NextArmedPylon(string? type)
    {
        var hps = Loadout!.Hardpoints;
        if (hps.Count == 0)
        {
            return 0;
        }
        for (int k = 0; k < hps.Count; k++)
        {
            int idx = (_nextPylon + k) % hps.Count;
            if (type != null && hps[idx].Weapon.Id != type)
            {
                continue;
            }
            if (hps[idx].Ammo > 0 || InfiniteAmmo)
            {
                return idx;
            }
        }
        return _nextPylon % hps.Count;
    }

    /// <summary>Advances every gun group's fire clock: while the trigger is held, each group spawns
    /// rounds at its <c>FIRE_RATE</c> (alternating muzzles so the group's total rate equals it),
    /// drawing from its own ammo counter; a dry group sounds the empty-clip cue once. Also drives
    /// the firing loop sound. No-op without a loadout / pool.</summary>
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
                    Projectiles.Spawn(g.Weapon, muzzle.GlobalTransform, inheritVel);
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
                    if (!st.Warned)
                    {
                        st.Warned = true;
                        Audio?.PlayEmptyClip();
                    }
                    st.Accum = 0f;
                    break;
                }
            }
        }
        if (wantLoop && !_gunLoopOn)
        {
            _gunLoopOn = true;
            Audio?.StartGunLoop(loopSound);
        }
        else if (!wantLoop && _gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        _firePrev = fire;
    }

    /// <summary>Launches rockets from the hardpoints: one per discrete trigger pull, drawn from the
    /// next pylon that still has ordnance (cycling across them), gated by the weapon's <c>FIRE_RATE</c>
    /// — 1.0/s for every rocket, i.e. one launch per second. Depletes that pylon's own counter; a pull
    /// with every pylon empty sounds the dry cue once. No-op without hardpoints / a pool.</summary>
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
        if (!pull || _rocketCooldown > 0f)
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
            }
            return;
        }
        _rocketDryWarned = false;
        var inheritVel = _model.VelocityDir * _model.Speed;
        Projectiles.Spawn(hp.Weapon, hp.Pylon.GlobalTransform, inheritVel);
        if (!InfiniteAmmo)
        {
            hp.Ammo--;
        }
        _rocketCooldown = hp.Weapon.FireRate > 0f ? 1f / hp.Weapon.FireRate : 1f;
        if (_rocketsLaunched < 12)
        {
            _rocketsLaunched++;
            GD.Print($"rocket: {hp.Weapon.Id} ({hp.Weapon.Name}) from pylon{hp.Index}, " +
                     $"{(InfiniteAmmo ? "∞" : hp.Ammo.ToString())} left on that pylon");
        }
    }

    /// <summary>The next hardpoint with ordnance, scanning from <see cref="_nextPylon"/> and wrapping,
    /// then advancing the cursor so consecutive pulls spread across the pylons. Null when every pylon
    /// is empty. With <c>--infinite-ammo</c> the first-scanned pylon always qualifies.</summary>
    private Hardpoint? NextArmedHardpoint()
    {
        var hps = Loadout!.Hardpoints;
        // The hardpoint selector restricts firing to one ordnance type (stock = the sole type).
        string? type = _ordnanceTypes.Length > 0
            ? _ordnanceTypes[Mathf.Clamp(_rocketSel, 0, _ordnanceTypes.Length - 1)]
            : null;
        for (int k = 0; k < hps.Count; k++)
        {
            int idx = (_nextPylon + k) % hps.Count;
            if (type != null && hps[idx].Weapon.Id != type)
            {
                continue;
            }
            if (hps[idx].Ammo > 0 || InfiniteAmmo)
            {
                _nextPylon = (idx + 1) % hps.Count;
                return hps[idx];
            }
        }
        return null;
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
        _nextPylon = 0;
        _rocketFirePrev = false;
        _rocketDryWarned = false;
        if (_gunLoopOn)
        {
            _gunLoopOn = false;
            Audio?.StopGunLoop();
        }
        Projectiles?.Clear();
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
        _damageFlash = 0f;
        if (PlaneModel != null)
            PlaneModel.Visible = true;
        _throttle = SpawnThrottle;
        _model.Reset(_spawnPos, _spawnAttitude, SpawnSpeed, _throttle);
        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);
        if (_camera != null && IsInsideTree())
            SnapCamera();
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

    private void Crash(Vector3 impact, string hitName, string part)
    {
        _crashed = true;
        _autoRespawnIn = AutoRespawnDelay;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        var surface = ClassifySurface(hitName);
        Audio?.OnCrash();
        if (surface == CrashSurface.Ground)
            Audio?.OnGroundExplosion(); // snd_exp_ground_a, layered over the plane explosion
        if (CrashRuntime != null)
        {
            // Data-driven crash: PLAY the compiled def on this plane's scoped crash
            // runtime. The def hides healthy/dontmove/markers, shows the destroyed wreck, launches
            // the pieceN ballistics, and fires every authored effect (sparks, the fireball cluster,
            // the black smokeball, the dirt burst, the burning-debris arcs). Audio stays the same
            // path (the def's SOUND events are not runtime-driven, so nothing double-plays).
            // The wreck pieces inherit a fraction of the plane's impact velocity so they scatter
            // along its travel rather than just popping up (the authored launch is a small relative
            // pop); TUNE the fraction against the original.
            CrashRuntime.InheritedWorldVelocity = _model.VelocityDir * _model.Speed * WreckMomentum;
            CrashRuntime.Play("player_crash_dirt", CrashAnchor, applyReset: false);
        }
        GD.Print($"CRASH into {hitName} ({part}) impact=({impact.X:0},{impact.Y:0},{impact.Z:0}) " +
                 $"pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) " +
                 $"spd={_model.Speed:0} m/s — waiting for respawn");
    }

    /// <summary>Which crash variant the original would play for the surface just hit. Every
    /// reachable crash today is a collision with terrain or a city block — a hard non-water
    /// surface, which is the <c>_dirt</c> (ground) variant; buildings are dirt too, since the
    /// only alternative to a hard-surface impact is the <c>_default</c> (air) variant, and
    /// that fires when the plane is destroyed with NO impact at all (shot down mid-flight),
    /// which has no trigger until weapons (M3). Water needs a sea-surface signal the collision
    /// system does not yet expose. So this is Ground for now — the seam is real, the other two
    /// arms wait on their triggers.</summary>
    private static CrashSurface ClassifySurface(string hitName) =>
        CrashSurface.Ground;

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

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Debug screenshot freeze: toggle with P / gamepad Start, then hold the whole
        // simulation in place (physics, input, collision, audio, props) so successive
        // screenshots frame the plane from the same spot. Checked even while crashed.
        bool pausePressed = PauseTogglePressed();
        if (pausePressed && !_pausePrev)
        {
            _paused = !_paused;
            if (_paused)
                SeedOrbit(); // start the orbit where the chase camera left off (no jump)
        }
        _pausePrev = pausePressed;
        if (_paused)
            return;

        // Advance the stunt clock every physics frame — including through the crash freeze so the
        // clock never stops (a deliberate rule); it stops only at AllComplete (inside Tick). Frozen
        // while paused (returned above — a debug screenshot freeze must not run the timer).
        Stunt?.Tick(dt);

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
            // _Process even through this sim freeze (motions, the played def, every puffer).
            // frozen at the impact point until the pilot respawns (R / gamepad Y or A);
            // unattended HoldSegments runs respawn on a timer instead
            if (RespawnPressed() || (HoldSegments != null && (_autoRespawnIn -= dt) <= 0f))
                Respawn();
            return;
        }

        var prev = _model.Position;          // committed position from last frame
        var input = HoldSegments != null ? NextHoldInput(dt) : ReadKeyboard(dt);
        _lastInput = input;
        _damageCooldown -= dt;
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
        if (hit && !SurviveHit(prev, step, stopFrac, impact, hitName, part, normal))
        {
            Crash(impact, hitName, part);
            return;
        }

        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);

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

        if (_model.Position.Y < UnderMapY)   // backstop if the swept ray ever misses
            Respawn();

        _sinceTelemetry += delta;
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
        string hitName, string part, Vector3 normal)
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
                         $"hp={state.Hp:0.0}/{state.Def.MaxHp:0}");
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

    public override void _Process(double delta)
    {
        if (_paused)
        {
            // free orbit around the frozen plane for framing screenshots
            UpdateOrbitCamera((float)delta);
        }
        else
        {
            UpdateChaseCamera((float)delta);
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
            Gauges.Stalled = !_crashed && !_paused && _model.isStalled();
        }
        // Feeds the E35 gauges (if built) and the E36 readout (if built) — both draw from the live
        // loadout, so this runs whenever there is one, independent of the dial cluster.
        UpdateWeaponGauges();
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
        if(_model.isStalled())
            _hud.Text += "\n⚠ STALLED - SPEED UP";
        if (!_paused && !_crashed && _damageFlash > 0f)
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
        if (_paused)
            _hud.Text += "\n⏸ PAUSED — orbit: WASD/arrows · zoom: Shift/Ctrl · P (gamepad Start) resume";
        else if (_crashed)
            _hud.Text += "\n⚠ CRASHED — PRESS R (GAMEPAD Y/A) TO RESPAWN";
        else
            Audio?.Update((float)delta, _model.Throttle, _model.Speed / _model.Stats.FdSpeed);

        // Spin the propeller/rotor blur discs: they keep turning even at idle (windmilling)
        // and speed up with throttle. Frozen while crashed or paused (a still disc reads
        // the same at any angle, and freezing it keeps screenshots deterministic).
        Props?.Advance(delta, _crashed || _paused ? 0f : PropIdleSpin + (1f - PropIdleSpin) * _model.Throttle);

        // Blink the wingtip flares on the data's 1.5 s cycle, and track the stick with
        // the control surfaces. Frozen while paused (so a screenshot catches a fixed
        // state — the paused orbit camera can inspect the held deflection) and while
        // crashed (the airframe is hidden anyway).
        if (!_crashed && !_paused)
        {
            WingLights?.Advance(delta);
            Surfaces?.Advance(delta, _lastInput);
            Visuals?.Update(_model.Position, _model.Attitude); // smoke/fire trail emission
        }
    }

    private Vector3 DesiredCamPos(out Vector3 camUp)
    {
        // chase from behind and above the nose in the plane's own frame, so the offset (and
        // the camera) roll fully with the plane — inverted flight shows the world upside down
        var nose = -_model.Attitude.Z;
        camUp = _model.Attitude.Y;
        return _model.Position - nose * CamBack + camUp * CamUp;
    }

    /// <summary>Chase camera: smooth the position toward the rigid behind-and-above offset
    /// (expressed in the plane's frame, so it banks with the plane) and slerp the orientation
    /// toward a look-at of the point ahead of the nose with the plane's own up. Smoothing the
    /// basis — rather than re-deriving a hard LookAt each frame from a near-world up — lets the
    /// horizon roll fully through inverted flight, while the rotational lag keeps fast rolls
    /// reading dynamic instead of glued.</summary>
    private void UpdateChaseCamera(float dt)
    {
        float tPos = 1f - Mathf.Exp(-CamSmooth * dt);
        _camera.Position = _camera.Position.Lerp(DesiredCamPos(out var camUp), tPos);

        var toTarget = _model.Position - _model.Attitude.Z * CamLookAhead - _camera.Position;
        if (toTarget.LengthSquared() < 1e-6f)
            return; // camera sitting on the look target (degenerate) — keep last orientation
        // Basis.LookingAt needs the up not parallel to the view direction; the plane's up is ⟂
        // to its nose so this practically never trips, but guard against extreme catch-up poses.
        var up = Mathf.Abs(toTarget.Normalized().Dot(camUp)) > 0.999f ? Vector3.Up : camUp;
        var desired = Basis.LookingAt(toTarget, up);
        float tRot = 1f - Mathf.Exp(-CamRotSmooth * dt);
        // Slerp via GetRotationQuaternion (which re-orthonormalizes each side) rather than
        // Basis.Slerp: the latter feeds the raw basis straight into Quaternion(), and the tiny
        // orthonormality drift that accumulates when the result is fed back frame after frame
        // eventually trips its "not normalized" assert. Re-orthonormalizing here can't compound.
        var current = _camera.Basis.GetRotationQuaternion();
        _camera.Basis = new Basis(current.Slerp(desired.GetRotationQuaternion(), tRot));
    }

    /// <summary>On entering the paused screenshot freeze, initialise the orbit angles
    /// and distance from the current camera position so it starts where the chase
    /// camera left off (no jump).</summary>
    private void SeedOrbit()
    {
        var v = _camera.Position - _model.Position;
        _orbitDist = Mathf.Clamp(v.Length(), OrbitMinDist, OrbitMaxDist);
        _orbitYaw = Mathf.Atan2(v.X, v.Z);
        _orbitPitch = _orbitDist > 1e-3f ? Mathf.Asin(Mathf.Clamp(v.Y / _orbitDist, -1f, 1f)) : 0f;
    }

    /// <summary>Free orbit camera used only while paused: WASD/arrows (or the gamepad
    /// left stick) swing the camera around the frozen plane, Shift/Ctrl (or the
    /// triggers) dolly in/out. The plane stays put, so every angle frames the same
    /// pose for side-by-side screenshots.</summary>
    private void UpdateOrbitCamera(float dt)
    {
        float padYaw = StickCurve(PadAxis(JoyAxis.LeftX));
        float padPitch = -StickCurve(PadAxis(JoyAxis.LeftY)); // stick up = camera up
        float padZoom = PadAxis(JoyAxis.TriggerRight)
                      - PadAxis(JoyAxis.TriggerLeft);         // RT out, LT in

        float yawIn = KeyAxis(Key.D, Key.A) + KeyAxis(Key.Right, Key.Left) + padYaw;
        float pitchIn = KeyAxis(Key.W, Key.S) + KeyAxis(Key.Up, Key.Down) + padPitch;
        float zoomIn = KeyAxis(Key.Ctrl, Key.Shift) + padZoom; // Ctrl/RT out, Shift/LT in

        float rate = Mathf.DegToRad(OrbitRateDeg);
        _orbitYaw += rate * yawIn * dt;
        _orbitPitch = Mathf.Clamp(_orbitPitch + rate * pitchIn * dt,
                                  Mathf.DegToRad(-85f), Mathf.DegToRad(85f));
        _orbitDist = Mathf.Clamp(_orbitDist * Mathf.Exp(OrbitZoomRate * zoomIn * dt),
                                 OrbitMinDist, OrbitMaxDist);

        var focus = _model.Position;
        float cp = Mathf.Cos(_orbitPitch);
        var dir = new Vector3(cp * Mathf.Sin(_orbitYaw), Mathf.Sin(_orbitPitch), cp * Mathf.Cos(_orbitYaw));
        _camera.Position = focus + dir * _orbitDist;
        _camera.LookAt(focus, Vector3.Up);
    }

    private void SnapCamera()
    {
        _camera.Position = DesiredCamPos(out var camUp);
        _camera.LookAt(_model.Position - _model.Attitude.Z * CamLookAhead, camUp);
    }
}
