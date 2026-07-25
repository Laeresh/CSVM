using Godot;

namespace CSVM.Flight;

/// <summary>
/// Free-flying observation camera (`--freecam`). Drives the
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

    /// <summary>The camera this drives — exposed so the anim lab can project mouse-pick rays
    /// through it (the lab reuses this camera as its freecam).</summary>
    public Camera3D Camera => _camera;

    // ---- follow / orbit (anim lab): the camera locks onto a node and orbits it -----------------

    /// <summary>The node the camera is locked onto, or null. While set, the camera <b>orbits</b>
    /// it (RMB drags the orbit, the wheel zooms, and the node stays centred as it moves), exactly
    /// like the static viewer's orbit camera but around a live target. Set via
    /// <see cref="FollowNode"/>; cleared the moment the user translates (WASD/QE / pad), so flying
    /// off ends the lock while the mouse keeps it. Read for the lab's status line.</summary>
    public Node3D? Follow { get; private set; }
    // Spherical offset of the eye from the target: distance, azimuth, elevation.
    private float _orbitYaw, _orbitPitch, _orbitDist;
    private const float OrbitMinDist = 3f, OrbitMaxDist = 8000f;
    // Kept short of straight-above so the look-at (Basis.LookingAt) never gets parallel to world
    // up, which is degenerate.
    private const float OrbitPitchLimit = 1.396f; // ~80°

    /// <summary>Lock onto a node and orbit it, seeding the orbit from the camera's current offset
    /// (so <see cref="Frame"/> having just placed the eye means no jump). Null releases the lock.</summary>
    public void FollowNode(Node3D? node)
    {
        Follow = node;
        if (node != null && IsInstanceValid(node))
        {
            var off = _camera.Position - node.GlobalPosition;
            _orbitDist = Mathf.Max(off.Length(), OrbitMinDist);
            _orbitPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(off.Y / _orbitDist, -1f, 1f)), -OrbitPitchLimit, OrbitPitchLimit);
            _orbitYaw = Mathf.Atan2(off.X, off.Z);
        }
    }

    /// <summary>Places the eye to frame an AABB from a fixed front-and-above angle, at the
    /// distance the current FOV needs to fit it — the anim lab's "focus this object". Leaves the
    /// follow state alone (the lab sets it alongside).</summary>
    public void Frame(Aabb aabb)
    {
        var center = aabb.GetCenter();
        float radius = Mathf.Max(aabb.Size.Length() * 0.5f, 1f);
        float dist = radius / Mathf.Tan(Mathf.DegToRad(_camera.Fov * 0.5f)) * 1.3f;
        var pos = center + new Vector3(0.4f, 0.45f, 1f).Normalized() * dist;
        _camera.Position = pos;
        var to = center - pos;
        _yaw = Mathf.Atan2(-to.X, -to.Z);
        _pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(to.Normalized().Y, -1f, 1f)), -PitchLimit, PitchLimit);
        ApplyOrientation();
    }

    public SpectatorCamera(Camera3D camera, Vector3 position, Vector3 lookAt)
    {
        _camera = camera;
        _camera.Position = position;
        var to = lookAt - position;
        // Derive the starting yaw/pitch from the requested look direction so --pos/--direction
        // (and the mission spawn default) frame exactly what they asked for. Only the DIRECTION
        // survives — the distance to the point is discarded, which is why the host may hand this
        // camera either a --lookat point or a --direction projected one unit ahead.
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
                // While locked the wheel zooms the orbit; otherwise it sets the fly speed.
                if (Follow != null)
                {
                    _orbitDist = Mathf.Max(_orbitDist / 1.15f, OrbitMinDist);
                }
                else
                {
                    _speed = Mathf.Min(_speed * 1.25f, MaxSpeed);
                }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                if (Follow != null)
                {
                    _orbitDist = Mathf.Min(_orbitDist * 1.15f, OrbitMaxDist);
                }
                else
                {
                    _speed = Mathf.Max(_speed / 1.25f, MinSpeed);
                }
                break;
            case InputEventMouseMotion motion when _looking:
                if (Follow != null)
                {
                    // Drag the orbit around the locked target (the OrbitUpdate re-aims each frame).
                    _orbitYaw -= motion.Relative.X * MouseLookRate;
                    _orbitPitch = Mathf.Clamp(_orbitPitch - motion.Relative.Y * MouseLookRate, -OrbitPitchLimit, OrbitPitchLimit);
                }
                else
                {
                    _yaw -= motion.Relative.X * MouseLookRate;
                    _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseLookRate, -PitchLimit, PitchLimit);
                    ApplyOrientation();
                }
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // Locked onto a target: orbit it, unless the user asks to translate (WASD/QE / pad) —
        // that releases the lock and this frame flies freely instead.
        if (Follow != null)
        {
            if (!IsInstanceValid(Follow))
            {
                Follow = null;
            }
            else if (TranslationRequested())
            {
                ExitFollow();
            }
            else
            {
                OrbitUpdate();
                UpdateReadout();
                return;
            }
        }
        Look(dt);
        Move(dt);
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        if (_readout == null)
        {
            return;
        }
        var p = _camera.Position;
        _readout.Text = $"freecam  x {p.X:0} y {p.Y:0} z {p.Z:0}   speed {_speed:0} m/s" +
                        "   [RMB look · WASD/QE move · Shift fast · wheel speed]";
    }

    // Places the eye on its orbit around the locked target and aims at it — the whole "orbit like
    // the static viewer, but around a live object" behaviour.
    private void OrbitUpdate()
    {
        var target = Follow!.GlobalPosition;
        float horiz = _orbitDist * Mathf.Cos(_orbitPitch);
        _camera.Position = target + new Vector3(
            horiz * Mathf.Sin(_orbitYaw), _orbitDist * Mathf.Sin(_orbitPitch), horiz * Mathf.Cos(_orbitYaw));
        var fwd = target - _camera.Position;
        if (fwd.LengthSquared() > 1e-6f)
        {
            _camera.Basis = Basis.LookingAt(fwd, Vector3.Up);
        }
    }

    // Release the lock, carrying the current orientation into the free-look yaw/pitch so the view
    // does not snap when you fly off.
    private void ExitFollow()
    {
        var fwd = -_camera.Basis.Z;
        _yaw = Mathf.Atan2(-fwd.X, -fwd.Z);
        _pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(fwd.Y, -1f, 1f)), -PitchLimit, PitchLimit);
        Follow = null;
    }

    // Any translation input this frame (used to release a lock). Keyboard is ignored while a text
    // field has focus, so typing a filter in the picker never moves the camera or breaks a lock.
    private bool TranslationRequested()
    {
        if (!KeyboardCaptured &&
            (Axis(Key.S, Key.W) != 0f || Axis(Key.A, Key.D) != 0f || Axis(Key.Q, Key.E) != 0f
             || Axis(Key.Down, Key.Up) != 0f || Axis(Key.Left, Key.Right) != 0f || Axis(Key.C, Key.Space) != 0f))
        {
            return true;
        }
        return Mathf.Abs(PadAxis(JoyAxis.LeftX)) > 0f || Mathf.Abs(PadAxis(JoyAxis.LeftY)) > 0f
               || PadButtonAxis() != 0f;
    }

    // True while a text input owns keyboard focus (the picker's filter): the camera polls raw key
    // state, which bypasses GUI focus, so without this typing WASD would fly the camera.
    private bool KeyboardCaptured => GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;

    private void Look(float dt)
    {
        // Keyboard fallback (IJKL) and the right stick both feed the same yaw/pitch as the
        // mouse, so any of the three can drive a session — including a pad-only machine.
        // They keep their own rates (a stick deflects proportionally, a key is on or off).
        float kb = KeyboardCaptured ? 0f : 1f;
        float yaw = kb * Axis(Key.J, Key.L) * KeyLookRate + PadAxis(JoyAxis.RightX) * PadLookRate;
        float pitch = kb * Axis(Key.K, Key.I) * KeyLookRate + PadAxis(JoyAxis.RightY) * PadLookRate;
        if (yaw == 0f && pitch == 0f)
            return;
        _yaw -= yaw * dt;
        _pitch = Mathf.Clamp(_pitch - pitch * dt, -PitchLimit, PitchLimit);
        ApplyOrientation();
    }

    private void Move(float dt)
    {
        // Keyboard is silenced while typing a filter (KeyboardCaptured); the pad is not.
        float kb = KeyboardCaptured ? 0f : 1f;
        var basis = _camera.Basis;
        // Forward is the camera's -Z (the project's convention everywhere); strafe its +X.
        var move = basis.Z * -(kb * (Axis(Key.S, Key.W) + Axis(Key.Down, Key.Up)) - PadAxis(JoyAxis.LeftY))
                 + basis.X * (kb * (Axis(Key.A, Key.D) + Axis(Key.Left, Key.Right)) + PadAxis(JoyAxis.LeftX));
        // Vertical stays WORLD up regardless of where the camera looks — climbing while
        // pitched down is what you want when repositioning over a target.
        move += Vector3.Up * (kb * (Axis(Key.Q, Key.E) + Axis(Key.C, Key.Space)) + PadButtonAxis());
        if (move.LengthSquared() < 1e-8f)
            return;

        float scale = 1f;
        if ((kb > 0f && Input.IsKeyPressed(Key.Shift)) || PadTrigger(JoyAxis.TriggerRight) > 0.5f)
        {
            scale *= BoostFactor;
        }
        if ((kb > 0f && Input.IsKeyPressed(Key.Ctrl)) || PadTrigger(JoyAxis.TriggerLeft) > 0.5f)
        {
            scale /= SlowFactor;
        }
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
    // Through Pads.For(null) rather than Pads.Connected(): these are input *reads*, so they are
    // gated on window focus as well as on --no-pads.
    private static float PadAxis(JoyAxis axis)
    {
        float best = 0f;
        foreach (int device in Pads.For(null))
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
        foreach (int device in Pads.For(null))
            best = Mathf.Max(best, Input.GetJoyAxis(device, axis));
        return best;
    }

    private static float PadButtonAxis()
    {
        bool up = false, down = false;
        foreach (int device in Pads.For(null))
        {
            up |= Input.IsJoyButtonPressed(device, JoyButton.RightShoulder);
            down |= Input.IsJoyButtonPressed(device, JoyButton.LeftShoulder);
        }
        return (up ? 1f : 0f) - (down ? 1f : 0f);
    }
}
