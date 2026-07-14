using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The flying aircraft: polls the keyboard into a <see cref="FlightModel"/>, applies
/// the result to this node's transform (the plane model is a child), and drives the
/// chase camera plus a minimal text HUD.
///
/// Controls: W/S or Up/Down pitch (W = push), A/D or Left/Right roll, Q/E rudder,
/// Shift/Ctrl throttle, R respawn.
/// </summary>
public partial class FlightController : Node3D
{
    /// <summary>When set, replaces keyboard input — used by automated screenshot runs.</summary>
    public FlightInput? HoldInput;

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

        if (Input.IsKeyPressed(Key.R))
            Respawn();

        _throttle = Mathf.Clamp(
            _throttle + Axis(Key.Shift, Key.Ctrl) * ThrottleRate * dt, 0f, 1f);

        return new FlightInput
        {
            // pull = S/Down, push = W/Up; bank/yaw left = A/Left/Q
            Pitch = Mathf.Clamp(Axis(Key.S, Key.W) + Axis(Key.Down, Key.Up), -1f, 1f),
            Roll = Mathf.Clamp(Axis(Key.A, Key.D) + Axis(Key.Left, Key.Right), -1f, 1f),
            Yaw = Axis(Key.Q, Key.E),
            Throttle = _throttle,
        };
    }

    public override void _Process(double delta)
    {
        float t = 1f - Mathf.Exp(-CamSmooth * (float)delta);
        _camera.Position = _camera.Position.Lerp(DesiredCamPos(out var camUp), t);
        _camera.LookAt(_model.Position - _model.Attitude.Z * CamLookAhead, camUp);

        float mph = _model.Speed * 2.23694f;
        float ft = _model.Position.Y * 3.28084f;
        _hud.Text = $"SPD {mph,4:0} MPH   ALT {ft,5:0} FT   THR {_model.Throttle * 100,3:0}%";
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
