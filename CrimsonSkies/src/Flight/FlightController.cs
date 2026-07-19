using System;
using CrimsonSkies.Effects;
using Godot;

namespace CrimsonSkies.Flight;

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

    /// <summary>The crash fireball, if its data/textures loaded (add as a child too).
    /// Fired at the impact point on a crash; cleared at respawn.</summary>
    public Puffer? CrashEffect;

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

    /// <summary>The airframe collision boxes (fuselage/wings/tail), swept along each
    /// physics frame's motion so wingtips and tail collide with obstacles. Null falls
    /// back to the old center-ray-only test.</summary>
    public PlaneCollider? Collider;

    /// <summary>Per-part hit points from the vehicle def's destroyable_parts (Run-2
    /// item 10b). When set, collisions below the crash threshold damage the struck
    /// part and the plane flies on; null keeps the old any-hit-crashes behavior.</summary>
    public PlaneDamage? Damage;

    /// <summary>Visible damage (Run-2 item 10c): torn-skin pdpanel flips + the low-HP
    /// smoke/fire trail, driven from the data's injure_anims thresholds. Optional.</summary>
    public DamageVisuals? Visuals;

    /// <summary>Crash breakup (Run-2 item 10d): the plane's 'destroyed' wreck pieces
    /// scatter at the impact and the wreck burns until respawn. Optional.</summary>
    public CrashBreakup? Breakup;

    /// <summary>Draw the collision probe — the swept ray plus the airframe boxes the
    /// crash test sweeps each physics frame — in green (red on the impact frame).</summary>
    public bool DebugCollision;

    private FlightModel _model = null!;
    private Camera3D _camera = null!;
    private Label _hud = null!;
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;
    private bool _crashed;                       // frozen at the impact point, waiting for respawn
    private FlightInput _lastInput;              // this physics frame's stick input (drives the surfaces)
    private float _autoRespawnIn;                // s until auto-respawn (HoldSegments runs only)
    private float _holdElapsed;                  // sim time into the HoldSegments sequence
    private bool _paused;                        // debug screenshot freeze (P): whole sim halts in place
    private bool _pausePrev;                     // previous frame's pause-key state (edge detection)
    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private ImmediateMesh? _probe;               // debug collision-probe line
    private float _damageCooldown;               // s left before the next HP subtraction
    private float _damageFlash;                  // s left on the HUD impact line
    private string _damageFlashText = "";

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

    // Collision severity (Run-2 item 10b, all TUNE): impact speed along the contact
    // normal decides between a survivable graze and a crash. A graze damages the
    // struck part (quadratic in severity), slides the velocity along the surface
    // with some tangential loss, and kicks the attitude; trees are soft obstacles —
    // the plane plows through with fixed damage and speed loss, never a direct
    // crash (the data still kills it once a critical part's HP drains).
    private const float CrashSpeed = 25f;        // m/s along the normal ⇒ outright crash
    private const float GrazeMaxDamage = 18f;    // HP at a just-under-crash graze (parts have 20–25)
    private const float GrazeFriction = 0.35f;   // tangential speed kill at full severity
    private const float GrazeKick = 1.2f;        // rad/s attitude kick at full severity
    private const float GrazePushOut = 0.15f;    // m off the surface after a graze (no sticky slide)
    private const float TreeDamage = 2.5f;       // HP per tree strike
    private const float TreeSpeedFactor = 0.92f; // speed retained per tree strike
    private const float DamageCooldown = 0.3f;   // s between HP subtractions (multi-frame scrapes)
    private const float DamageFlashTime = 2.5f;  // s the HUD shows the impact line
    private const float GrazeStopSpeed = 12f;    // m/s — grinding to (near) standstill on the
                                                 // ground explodes the plane (user-reported:
                                                 // a stopped plane sat there collecting 0-dmg kisses)
    private const float EmbedPushOut = 0.3f;     // m per un-embed attempt after a graze
    private const int EmbedTries = 3;            // attempts before giving up ⇒ explode, never tunnel
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
        _hud = new Label { Position = new Vector2(16, 10) };
        _hud.AddThemeFontSizeOverride("font_size", 22);
        _hud.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        _hud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        _hud.AddThemeConstantOverride("shadow_offset_y", 2);
        canvas.AddChild(_hud);
        if (Compass != null)
            canvas.AddChild(Compass);
        AddChild(canvas);
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
    }

    private void Respawn()
    {
        _crashed = false;
        _holdElapsed = 0f; // scripted hold sequences restart from the spawn
        _lastInput = default;
        CrashEffect?.Clear();
        WingLights?.Reset(); // flares off; the cycle restarts from this spawn
        Surfaces?.Reset();   // control surfaces back to neutral
        Damage?.Reset();     // every part back to full HP
        Visuals?.Reset();    // torn panels off, healthy twins back, smoke trail cleared
        Breakup?.Reset();    // wreck pieces hidden, burn out
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

    /// <summary>True if the segment crosses any static world collider; on a hit,
    /// <paramref name="point"/> is the impact position (else the segment end) and
    /// <paramref name="hitName"/> names the collider (parent/body — e.g. a terrain
    /// tile's "g27889/col", or the tree field's "world1/clutter_col").</summary>
    private bool HitWorld(Vector3 from, Vector3 to, out Vector3 point, out string hitName)
    {
        point = to;
        hitName = "";
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        if (hit.Count == 0)
            return false;
        point = (Vector3)hit["position"];
        if (hit["collider"].Obj is Node body)
            hitName = $"{body.GetParent()?.Name}/{body.Name}";
        return true;
    }

    private void Crash(Vector3 impact, string hitName, string part)
    {
        _crashed = true;
        _autoRespawnIn = AutoRespawnDelay;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        Audio?.OnCrash();
        CrashEffect?.Burst(impact); // the game's large_fireball at the impact point
        // the wreck: destroyed-subtree pieces scatter with the impact velocity and
        // the fire/smoke burn at the impact point (item 10d)
        Breakup?.Begin(PlaneModel?.GlobalTransform ?? GlobalTransform, impact,
            _model.VelocityDir * _model.Speed);
        GD.Print($"CRASH into {hitName} ({part}) impact=({impact.X:0},{impact.Y:0},{impact.Z:0}) " +
                 $"pos=({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) " +
                 $"spd={_model.Speed:0} m/s — waiting for respawn");
    }

    private static bool RespawnPressed()
    {
        if (Input.IsKeyPressed(Key.R))
            return true;
        var pads = Input.GetConnectedJoypads();
        return pads.Count > 0 && (Input.IsJoyButtonPressed(pads[0], JoyButton.Y)
                                  || Input.IsJoyButtonPressed(pads[0], JoyButton.A));
    }

    /// <summary>P (or gamepad Start), edge-detected so one press toggles once.</summary>
    private static bool PauseTogglePressed()
    {
        if (Input.IsKeyPressed(Key.P))
            return true;
        var pads = Input.GetConnectedJoypads();
        return pads.Count > 0 && Input.IsJoyButtonPressed(pads[0], JoyButton.Start);
    }

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

        if (_crashed)
        {
            // the wreck pieces keep tumbling/resting while the sim is frozen
            Breakup?.Advance(dt, GetWorld3D()?.DirectSpaceState);
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
            out var normal, out float stopFrac);
        if (!hit && HitWorld(prev, probeEnd, out impact, out hitName))
        {
            hit = true;
            part = "center";
            normal = len > 1e-4f ? -step / len : Vector3.Up;
            stopFrac = 1f;
        }
        if (_probe != null)
            DrawProbe(prev, probeEnd, prev + step * stopFrac, hit);
        if (hit && !SurviveHit(prev, step, stopFrac, impact, hitName, part, normal))
        {
            Crash(impact, hitName, part);
            return;
        }

        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);

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
                     $"wv={Mathf.Abs(_model.Attitude.Y.Dot(Vector3.Up)):0.00}");
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
        static float Axis(Key positive, Key negative) =>
            (Input.IsKeyPressed(positive) ? 1f : 0f) - (Input.IsKeyPressed(negative) ? 1f : 0f);

        // first connected gamepad, if any
        int pad = -1;
        var pads = Input.GetConnectedJoypads();
        if (pads.Count > 0)
            pad = pads[0];
        static float Btn(int device, JoyButton positive, JoyButton negative) =>
            (Input.IsJoyButtonPressed(device, positive) ? 1f : 0f)
            - (Input.IsJoyButtonPressed(device, negative) ? 1f : 0f);

        float padPitch = 0f, padRoll = 0f, padYaw = 0f, padThrottle = 0f;
        if (pad >= 0)
        {
            // arcade-flight standard: stick back (+Y) = nose up, stick right = bank right
            padPitch = StickCurve(Input.GetJoyAxis(pad, JoyAxis.LeftY));
            padRoll = -StickCurve(Input.GetJoyAxis(pad, JoyAxis.LeftX));
            padYaw = Btn(pad, JoyButton.LeftShoulder, JoyButton.RightShoulder);
            padThrottle = Input.GetJoyAxis(pad, JoyAxis.TriggerRight)
                        - Input.GetJoyAxis(pad, JoyAxis.TriggerLeft);
            if (Input.IsJoyButtonPressed(pad, JoyButton.Y))
                Respawn();
        }

        if (Input.IsKeyPressed(Key.R))
            Respawn();

        _throttle = Mathf.Clamp(
            _throttle + (Axis(Key.Shift, Key.Ctrl) + padThrottle) * ThrottleRate * dt, 0f, 1f);

        return new FlightInput
        {
            // pull = S/Down, push = W/Up; bank/yaw left = A/Left/Q
            Pitch = Mathf.Clamp(Axis(Key.S, Key.W) + Axis(Key.Down, Key.Up) + padPitch, -1f, 1f),
            Roll = Mathf.Clamp(Axis(Key.A, Key.D) + Axis(Key.Left, Key.Right) + padRoll, -1f, 1f),
            Yaw = Mathf.Clamp(Axis(Key.Q, Key.E) + padYaw, -1f, 1f),
            Throttle = _throttle,
        };
    }

    /// <summary>Decides a confirmed collision's outcome (Run-2 item 10b): false =
    /// crash (severe impact, a critical part destroyed, or no damage data), true =
    /// survivable graze — the struck part takes severity-scaled damage, the plane is
    /// placed at the swept safe pose, its velocity deflects along the surface with
    /// some tangential loss, and the attitude takes a lever-arm kick. Trees
    /// (clutter_col) are soft: fixed damage + speed loss, fly straight through.</summary>
    private bool SurviveHit(Vector3 prev, Vector3 step, float stopFrac, Vector3 impact,
        string hitName, string part, Vector3 normal)
    {
        if (Damage == null)
            return false; // no destroyable_parts data — every hit crashes (old behavior)
        bool tree = hitName.EndsWith("clutter_col");
        var vel = _model.VelocityDir * _model.Speed;
        float vn = tree ? 0f : Mathf.Abs(vel.Dot(normal));
        if (!tree && vn >= CrashSpeed)
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
            float dmg = tree ? TreeDamage
                : GrazeMaxDamage * (vn / CrashSpeed) * (vn / CrashSpeed);
            var state = Damage.Apply(dataPart, dmg);
            if (state != null)
            {
                Visuals?.OnPartDamage(dataPart, state.Fraction);
                if (state.Hp <= 0f && state.Def.Critical)
                {
                    GD.Print($"part destroyed: {dataPart} (critical) — " +
                             $"{(tree ? "tree strike" : $"vn={vn:0.0} m/s")} into {hitName}");
                    return false; // the data's meaning: a dead critical part downs the plane
                }
                _damageFlashText = $"⚠ IMPACT {dataPart.ToUpperInvariant()} {state.Fraction * 100f:0}%";
                _damageFlash = DamageFlashTime;
                GD.Print($"graze ({part}→{dataPart}): {hitName} " +
                         $"{(tree ? "tree" : $"vn={vn:0.0} m/s")} dmg={dmg:0.0} " +
                         $"hp={state.Hp:0.0}/{state.Def.MaxHp:0}");
            }
        }

        if (tree)
        {
            _model.Speed = speedBefore * TreeSpeedFactor; // plow through, shedding speed
            return true;
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
                    // trees (clutter_col) are soft — never "embedded" in a forest
                    foreach (var hitInfo in space2.IntersectShape(q, 4))
                        if (hitInfo["collider"].Obj is not Node b || !b.Name.ToString().EndsWith("clutter_col"))
                        {
                            overlapping = true;
                            break;
                        }
                    if (overlapping)
                        break;
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
        out string hitName, out string part, out Vector3 normal, out float stopFrac)
    {
        impact = _model.Position;
        hitName = "";
        part = "";
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
                hitName = GodotObject.InstanceFromId((ulong)rest["collider_id"]) is Node body
                    ? $"{body.GetParent()?.Name}/{body.Name}"
                    : "world";
            }
            else
            {
                impact = (baseXf * p.Local).Origin + motion * cast[1];
                normal = mLen > 1e-6f ? -motion / mLen : Vector3.Up;
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
        if (Compass != null)
        {
            // heading of the nose: 0 = north (−Z), 90 = east (+X)
            var nose = -_model.Attitude.Z;
            Compass.HeadingDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(nose.X, -nose.Z)), 360f);
        }
        _hud.Text = $"SPD {mph,4:0} MPH   ALT {ft,5:0} FT   THR {_model.Throttle * 100,3:0}%";
        if(_model.isStalled())
            _hud.Text += "\n⚠ STALLED - SPEED UP";
        if (!_paused && !_crashed && _damageFlash > 0f)
        {
            _damageFlash -= (float)delta;
            _hud.Text += $"\n{_damageFlashText}";
        }
        if (Damage?.Summary() is { Length: > 0 } dmgSummary)
            _hud.Text += $"\nDMG {dmgSummary}";
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
        static float Axis(Key positive, Key negative) =>
            (Input.IsKeyPressed(positive) ? 1f : 0f) - (Input.IsKeyPressed(negative) ? 1f : 0f);

        float padYaw = 0f, padPitch = 0f, padZoom = 0f;
        var pads = Input.GetConnectedJoypads();
        if (pads.Count > 0)
        {
            int pad = pads[0];
            padYaw = StickCurve(Input.GetJoyAxis(pad, JoyAxis.LeftX));
            padPitch = -StickCurve(Input.GetJoyAxis(pad, JoyAxis.LeftY)); // stick up = camera up
            padZoom = Input.GetJoyAxis(pad, JoyAxis.TriggerRight)
                    - Input.GetJoyAxis(pad, JoyAxis.TriggerLeft);         // RT out, LT in
        }

        float yawIn = Axis(Key.D, Key.A) + Axis(Key.Right, Key.Left) + padYaw;
        float pitchIn = Axis(Key.W, Key.S) + Axis(Key.Up, Key.Down) + padPitch;
        float zoomIn = Axis(Key.Ctrl, Key.Shift) + padZoom; // Ctrl/RT out, Shift/LT in

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
