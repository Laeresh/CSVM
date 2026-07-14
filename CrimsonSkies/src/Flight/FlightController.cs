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
/// </summary>
public partial class FlightController : Node3D
{
    /// <summary>When set, replaces keyboard input — used by automated screenshot runs.</summary>
    public FlightInput? HoldInput;

    /// <summary>Own-plane sound, if the sound archive was found (add as a child too).</summary>
    public FlightAudio? Audio;

    private FlightModel _model = null!;
    private Camera3D _camera = null!;
    private Label _hud = null!;
    private Vector3 _spawnPos;
    private Basis _spawnAttitude;
    private float _throttle;
    private double _sinceTelemetry;

    private const float ThrottleRate = 0.5f;    // full sweep in 2 s
    private const float CamBack = 16f, CamUp = 4.5f, CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s
    private const float UnderMapY = 60f;        // C1 terrain sits at y≈100+; below this we're lost

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
        SnapCamera();
    }

    private void Respawn()
    {
        _throttle = 0.8f;
        _model.Reset(_spawnPos, _spawnAttitude, 0.8f * _model.Stats.FdSpeed, _throttle);
        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);
        if (_camera != null && IsInsideTree())
            SnapCamera();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var input = HoldInput ?? ReadKeyboard(dt);
        _model.Step(input, dt);
        GlobalTransform = new Transform3D(_model.Attitude, _model.Position);

        if (_model.Position.Y < UnderMapY)
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

        Audio?.Update(_model.Throttle, _model.Speed / _model.Stats.FdSpeed);
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
