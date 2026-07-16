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
    /// Such runs are unattended, so a crash auto-respawns after a short pause.</summary>
    public FlightInput? HoldInput;

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

    /// <summary>Draw the collision probe — the swept ray the crash test casts each
    /// physics frame — as a debug line (green; red on the impact frame).</summary>
    public bool DebugCollision;

    private FlightModel _model = null!;
    private Camera3D _camera = null!;
    private Label _hud = null!;
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;
    private bool _crashed;                       // frozen at the impact point, waiting for respawn
    private float _autoRespawnIn;                // s until auto-respawn (HoldInput runs only)
    private bool _paused;                        // debug screenshot freeze (P): whole sim halts in place
    private bool _pausePrev;                     // previous frame's pause-key state (edge detection)
    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private ImmediateMesh? _probe;               // debug collision-probe line

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float SpawnThrottle = 0.5f;   // the original always spawns at half throttle (confirmed in-game, all planes)
    private const float SpawnSpeed = 53.6f;     // m/s ≈ 120 mph. PLACEHOLDER: the original's spawn speed is
                                                // plane-dependent (TODO — kept fixed for now per user); the
                                                // plane accelerates from here toward its cruise
    private const float CamBack = 16f, CamUp = 4.5f, CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s
    private const float UnderMapY = 60f;        // C1 terrain sits at y≈100+; below this we're lost
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)
    private const float AutoRespawnDelay = 1.5f; // s a HoldInput run stays crashed before auto-respawn
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
        CrashEffect?.Clear();
        WingLights?.Reset(); // flares off; the cycle restarts from this spawn
        if (PlaneModel != null)
            PlaneModel.Visible = true;
        _throttle = SpawnThrottle;
        _model.Reset(_spawnPos, _spawnAttitude, SpawnSpeed, _throttle);
        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);
        if (_camera != null && IsInsideTree())
            SnapCamera();
    }

    /// <summary>True if the segment crosses any static world collider; on a hit,
    /// <paramref name="point"/> is the impact position (else the segment end).</summary>
    private bool HitWorld(Vector3 from, Vector3 to, out Vector3 point)
    {
        point = to;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        if (hit.Count == 0)
            return false;
        point = (Vector3)hit["position"];
        return true;
    }

    private void Crash(Vector3 impact)
    {
        _crashed = true;
        _autoRespawnIn = AutoRespawnDelay;
        if (PlaneModel != null)
            PlaneModel.Visible = false; // the airframe is gone; HUD prompts for respawn
        Audio?.OnCrash();
        CrashEffect?.Burst(impact); // the game's large_fireball at the impact point
        GD.Print($"CRASH at ({_model.Position.X:0},{_model.Position.Y:0},{_model.Position.Z:0}) " +
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
            // frozen at the impact point until the pilot respawns (R / gamepad Y or A);
            // unattended HoldInput runs respawn on a timer instead
            if (RespawnPressed() || (HoldInput != null && (_autoRespawnIn -= dt) <= 0f))
                Respawn();
            return;
        }

        var prev = _model.Position;          // committed position from last frame
        var input = HoldInput ?? ReadKeyboard(dt);
        _model.Step(input, dt);

        // Crash when the frame's flight path runs into solid world geometry (terrain,
        // buildings). Sweeping prev→next avoids tunnelling through terrain in a fast
        // dive; the extra margin keeps the nose (not the plane's center) off the wall.
        var to = _model.Position;
        var step = to - prev;
        float len = step.Length();
        var probeEnd = len > 1e-4f ? to + step / len * CollisionMargin : to;
        bool hit = HitWorld(prev, probeEnd, out var impact);
        if (_probe != null)
            DrawProbe(prev, probeEnd, hit);
        if (hit)
        {
            Crash(impact);
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
            GD.Print($"flight: pos=({p.X:0},{p.Y:0},{p.Z:0}) spd={_model.Speed:0.0} m/s " +
                     $"thr={_model.Throttle:0.00} rates=({_model.BodyRates.X:0.00},{_model.BodyRates.Y:0.00},{_model.BodyRates.Z:0.00})");
        }
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

    /// <summary>Debug view of the swept collision ray: a line along this frame's flight
    /// path (plus the nose margin) with a cross at the probe tip. Freezes red at the
    /// impact point while crashed.</summary>
    private void DrawProbe(Vector3 from, Vector3 end, bool hit)
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
        _probe.SurfaceEnd();
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
            float t = 1f - Mathf.Exp(-CamSmooth * (float)delta);
            _camera.Position = _camera.Position.Lerp(DesiredCamPos(out var camUp), t);
            _camera.LookAt(_model.Position - _model.Attitude.Z * CamLookAhead, camUp);
        }

        float mph = _model.Speed * 2.23694f;
        float ft = _model.Position.Y * 3.28084f;
        _hud.Text = $"SPD {mph,4:0} MPH   ALT {ft,5:0} FT   THR {_model.Throttle * 100,3:0}%";
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

        // Blink the wingtip flares on the data's 1.5 s cycle. Frozen while paused (so a
        // screenshot catches a fixed state) and while crashed (the airframe is hidden anyway).
        if (!_crashed && !_paused)
            WingLights?.Advance(delta);
    }

    private Vector3 DesiredCamPos(out Vector3 camUp)
    {
        // chase from behind the nose, banking partway with the plane
        var nose = -_model.Attitude.Z;
        camUp = Vector3.Up.Lerp(_model.Attitude.Y, 0.45f).Normalized();
        return _model.Position - nose * CamBack + camUp * CamUp;
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
