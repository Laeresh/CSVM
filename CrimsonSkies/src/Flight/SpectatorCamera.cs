using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Free-flying observation camera (`--freecam`, anim-playback plan item 1). Drives the
/// session camera directly with no aircraft in the world: the point is to park in front of
/// a moving train or a hangar door and watch it, without scripting a flight past it.
///
/// Controls — keyboard: WASD/arrows move, Q/E (or Space/C) down/up, hold Shift for ×6 and
/// Ctrl for ÷6, mouse wheel sets the base speed, hold the RIGHT mouse button to look
/// (the mouse is captured only while held, so the window stays usable). Arrow-free look
/// fallback: IJKL. Gamepad: left stick moves, right stick looks, LB/RB descend/climb,
/// left trigger slows, right trigger boosts.
///
/// Deliberately has NO collision — flying through terrain to get a vantage point is the
/// feature. Pitch is clamped just short of vertical and roll is never applied, so the
/// horizon stays level and the view cannot tumble into an unrecoverable attitude.
/// </summary>
public sealed partial class SpectatorCamera : Node
{
    /// <summary>Base movement speed in m/s, before the boost/slow modifiers. TUNE.</summary>
    public const float DefaultSpeed = 180f;
    private const float MinSpeed = 2f, MaxSpeed = 6000f;
    private const float BoostFactor = 6f, SlowFactor = 6f;
    private const float MouseLookRate = 0.0035f;   // radians per pixel of mouse motion. TUNE.
    private const float KeyLookRate = 1.6f;        // radians/s for the IJKL fallback. TUNE.
    private const float PadLookRate = 2.4f;        // radians/s at full right-stick deflection. TUNE.
    private const float PadDeadzone = 0.18f;
    private const float PitchLimit = 1.5533f;      // ~89°, so the view never gimbals over the top

    private readonly Camera3D _camera;
    private float _yaw, _pitch;
    private float _speed = DefaultSpeed;
    private bool _looking;                          // right mouse button held
    private Label? _readout;

    /// <summary>Shows the live position/speed readout (on by default; off for screenshots).</summary>
    public bool ShowReadout = true;

    public SpectatorCamera(Camera3D camera, Vector3 position, Vector3 lookAt)
    {
        _camera = camera;
        _camera.Position = position;
        var to = lookAt - position;
        // Derive the starting yaw/pitch from the requested look direction so --campos/--lookat
        // (and the mission spawn default) frame exactly what they asked for.
        if (to.LengthSquared() > 1e-6f)
        {
            _yaw = Mathf.Atan2(-to.X, -to.Z);
            _pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(to.Normalized().Y, -1f, 1f)), -PitchLimit, PitchLimit);
        }
        ApplyOrientation();
    }

    public override void _Ready()
    {
        if (!ShowReadout)
            return;
        var layer = new CanvasLayer();
        AddChild(layer);
        _readout = new Label
        {
            Position = new Vector2(12, 12),
            Modulate = new Color(0.75f, 0.9f, 1f),
        };
        _readout.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(_readout);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rmb:
                _looking = rmb.Pressed;
                Input.MouseMode = _looking ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _speed = Mathf.Min(_speed * 1.25f, MaxSpeed);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _speed = Mathf.Max(_speed / 1.25f, MinSpeed);
                break;
            case InputEventMouseMotion motion when _looking:
                _yaw -= motion.Relative.X * MouseLookRate;
                _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseLookRate, -PitchLimit, PitchLimit);
                ApplyOrientation();
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Look(dt);
        Move(dt);
        if (_readout != null)
        {
            var p = _camera.Position;
            _readout.Text = $"freecam  x {p.X:0} y {p.Y:0} z {p.Z:0}   speed {_speed:0} m/s" +
                            "   [RMB look · WASD/QE move · Shift fast · wheel speed]";
        }
    }

    private void Look(float dt)
    {
        // Keyboard fallback (IJKL) and the right stick both feed the same yaw/pitch as the
        // mouse, so any of the three can drive a session — including a pad-only machine.
        // They keep their own rates (a stick deflects proportionally, a key is on or off).
        float yaw = Axis(Key.J, Key.L) * KeyLookRate + PadAxis(JoyAxis.RightX) * PadLookRate;
        float pitch = Axis(Key.K, Key.I) * KeyLookRate + PadAxis(JoyAxis.RightY) * PadLookRate;
        if (yaw == 0f && pitch == 0f)
            return;
        _yaw -= yaw * dt;
        _pitch = Mathf.Clamp(_pitch - pitch * dt, -PitchLimit, PitchLimit);
        ApplyOrientation();
    }

    private void Move(float dt)
    {
        var basis = _camera.Basis;
        // Forward is the camera's -Z (the project's convention everywhere); strafe its +X.
        var move = basis.Z * -(Axis(Key.S, Key.W) + Axis(Key.Down, Key.Up) - PadAxis(JoyAxis.LeftY))
                 + basis.X * (Axis(Key.A, Key.D) + Axis(Key.Left, Key.Right) + PadAxis(JoyAxis.LeftX));
        // Vertical stays WORLD up regardless of where the camera looks — climbing while
        // pitched down is what you want when repositioning over a target.
        move += Vector3.Up * (Axis(Key.Q, Key.E) + Axis(Key.C, Key.Space) + PadButtonAxis());
        if (move.LengthSquared() < 1e-8f)
            return;

        float scale = 1f;
        if (Input.IsKeyPressed(Key.Shift) || PadTrigger(JoyAxis.TriggerRight) > 0.5f) scale *= BoostFactor;
        if (Input.IsKeyPressed(Key.Ctrl) || PadTrigger(JoyAxis.TriggerLeft) > 0.5f) scale /= SlowFactor;
        _camera.Position += move.Normalized() * (_speed * scale * dt);
    }

    private void ApplyOrientation()
    {
        // Yaw about world up, then pitch about the camera's own X: no roll term ever enters,
        // so the horizon stays level.
        _camera.Basis = new Basis(Vector3.Up, _yaw) * new Basis(Vector3.Right, _pitch);
    }

    // -1 when only `negative` is down, +1 when only `positive` is, 0 for neither or both.
    private static float Axis(Key negative, Key positive) =>
        (Input.IsKeyPressed(positive) ? 1f : 0f) - (Input.IsKeyPressed(negative) ? 1f : 0f);

    // Any-pad reads, matching the project's phantom-device policy (never pads[0]): take the
    // largest-magnitude value across every connected pad, so idle/phantom devices read ~0.
    private static float PadAxis(JoyAxis axis)
    {
        float best = 0f;
        foreach (int device in Input.GetConnectedJoypads())
        {
            float v = Input.GetJoyAxis(device, axis);
            if (Mathf.Abs(v) > Mathf.Abs(best))
                best = v;
        }
        return Mathf.Abs(best) < PadDeadzone ? 0f : best;
    }

    private static float PadTrigger(JoyAxis axis)
    {
        float best = 0f;
        foreach (int device in Input.GetConnectedJoypads())
            best = Mathf.Max(best, Input.GetJoyAxis(device, axis));
        return best;
    }

    private static float PadButtonAxis()
    {
        bool up = false, down = false;
        foreach (int device in Input.GetConnectedJoypads())
        {
            up |= Input.IsJoyButtonPressed(device, JoyButton.RightShoulder);
            down |= Input.IsJoyButtonPressed(device, JoyButton.LeftShoulder);
        }
        return (up ? 1f : 0f) - (down ? 1f : 0f);
    }
}
