using CrimsonSkies.Effects;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The flying aircraft: polls keyboard + gamepad into a <see cref="FlightModel"/>,
/// applies the result to this node's transform (the plane model is a child), and
/// drives the chase camera plus a minimal text HUD.
///
/// Keyboard: W/S or Up/Down pitch (W = push), A/D or Left/Right roll, Q/E rudder,
/// Shift/Ctrl throttle, R respawn.
/// Gamepad: left stick pitch/roll (back = nose up), LB/RB rudder, RT/LT throttle
/// up/down, Y respawn.
///
/// Hitting terrain or a building crashes the plane: explosion sound, airframe
/// hidden, frozen at the impact point until R (or gamepad Y/A) respawns.
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
    private ImmediateMesh? _probe;               // debug collision-probe line

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float SpawnThrottle = 0.5f;   // the original's spawn throttle (user-observed)
    private const float SpawnSpeed = 53.6f;     // m/s ≈ 120 mph, the original's spawn speed; the
                                                // plane accelerates from here toward its cruise
    private const float CamBack = 16f, CamUp = 4.5f, CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s
    private const float UnderMapY = 60f;        // C1 terrain sits at y≈100+; below this we're lost
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)
    private const float AutoRespawnDelay = 1.5f; // s a HoldInput run stays crashed before auto-respawn
    private const float PropIdleSpin = 0.4f;    // blur discs still turn at zero throttle (windmilling)

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

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
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
        float t = 1f - Mathf.Exp(-CamSmooth * (float)delta);
        _camera.Position = _camera.Position.Lerp(DesiredCamPos(out var camUp), t);
        _camera.LookAt(_model.Position - _model.Attitude.Z * CamLookAhead, camUp);

        float mph = _model.Speed * 2.23694f;
        float ft = _model.Position.Y * 3.28084f;
        _hud.Text = $"SPD {mph,4:0} MPH   ALT {ft,5:0} FT   THR {_model.Throttle * 100,3:0}%";
        if (_crashed)
            _hud.Text += "\n⚠ CRASHED — PRESS R (GAMEPAD Y/A) TO RESPAWN";
        else
            Audio?.Update((float)delta, _model.Throttle, _model.Speed / _model.Stats.FdSpeed);

        // Spin the propeller/rotor blur discs: they keep turning even at idle (windmilling)
        // and speed up with throttle. Frozen while crashed (the airframe is hidden anyway).
        Props?.Advance(delta, _crashed ? 0f : PropIdleSpin + (1f - PropIdleSpin) * _model.Throttle);
    }

    private Vector3 DesiredCamPos(out Vector3 camUp)
    {
        // chase from behind the nose, banking partway with the plane
        var nose = -_model.Attitude.Z;
        camUp = Vector3.Up.Lerp(_model.Attitude.Y, 0.45f).Normalized();
        return _model.Position - nose * CamBack + camUp * CamUp;
    }

    private void SnapCamera()
    {
        _camera.Position = DesiredCamPos(out var camUp);
        _camera.LookAt(_model.Position - _model.Attitude.Z * CamLookAhead, camUp);
    }
}
