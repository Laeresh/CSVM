using System;
using CSVM.Bindings;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The laser pointer on the theater screen: a ray from one hand controller's aim pose, clicking with
/// that hand's trigger. It draws as a thin beam with a dot where it meets the screen. Godot controls
/// and the event readers take its moves and clicks as mouse events pushed into the root at the hit's
/// layout point. The polled readers take the same point through <see cref="ScreenPointer"/>, which
/// it overrides while a controller is present. The right hand points first; the other hand's
/// trigger takes the pointer over without clicking. Module entry: docs/architecture/UI.md.
/// </summary>
public sealed partial class TheaterPointer : Node3D
{
    /// <summary>The beam's and the dot's draw order: over the screen (<see cref="TheaterScreen.ScreenPriority"/>),
    /// which ignores depth and would hide them.</summary>
    public const int BeamPriority = 127;

    // After the screen has placed itself and decided whether it shows (TheaterScreen, 2100).
    private const int AfterTheater = 2200;

    // The beam's thickness, the dot's diameter and the beam's length when it meets nothing. TUNE.
    private const float BeamWidthM = 0.003f;
    private const float DotM = 0.012f;
    private const float MissLengthM = 0.6f;

    // How far the dot stands in front of the screen, toward the seat, so it never sinks into it.
    private const float DotLiftM = 0.004f;

    // sRGB tints, as vectors: Godot hands a Color to a shader already made linear. TUNE.
    private static readonly Vector3 BeamTint = new(0.55f, 0.85f, 1f);
    private static readonly Vector3 DotTint = new(1f, 1f, 1f);

    private Viewport _root = null!;
    private TheaterScreen _theater = null!;
    private Func<Transform3D> _origin = null!;
    private bool _eyeFeed;
    private XrControllers _hands = null!;
    private Func<(Vector2 At, bool Pressed)?> _reading = null!;
    private MeshInstance3D _beam = null!;
    private MeshInstance3D _dot = null!;
    private Vector2 _lastAt;
    private bool _held;
    private bool _wasPressed;
    private bool _clicking;
    private bool _swallow;
    private bool _otherWasDown;
    private bool _live;

    /// <summary>The hand the pointer casts from.</summary>
    public XrHand Hand { get; private set; } = XrHand.Right;

    /// <summary>Where the ray meets the screen in the root's layout pixels, or null when it misses or
    /// the screen is down.</summary>
    public Vector2? At { get; private set; }

    /// <summary>Whether the beam drew at the last <see cref="Step"/>.</summary>
    public bool BeamShown => _beam.Visible;

    /// <summary>Whether the dot drew at the last <see cref="Step"/>, which is whether the ray hit.
    /// </summary>
    public bool DotShown => _dot.Visible;

    /// <summary>Builds the pointer over <paramref name="theater"/>, pushing its events into
    /// <paramref name="root"/>. The XR origin's pose comes from <paramref name="origin"/>; with
    /// <paramref name="eyeFeed"/> the beam is placed from the eye feed, as the screen is. The hands are
    /// <paramref name="hands"/>, else <see cref="XrControllers.Live"/>. The caller adds the node.</summary>
    public static TheaterPointer Build(
        Viewport root, TheaterScreen theater, Func<Transform3D> origin, bool eyeFeed, XrControllers? hands = null)
    {
        var pointer = new TheaterPointer
        {
            Name = "theater_pointer",
            ProcessPriority = AfterTheater,
            _root = root ?? throw new ArgumentNullException(nameof(root)),
            _theater = theater ?? throw new ArgumentNullException(nameof(theater)),
            _origin = origin ?? throw new ArgumentNullException(nameof(origin)),
            _eyeFeed = eyeFeed,
            _hands = hands ?? XrControllers.Live,
        };
        pointer._reading = () => pointer.At is { } at ? (at, pointer._held && !pointer._swallow) : null;
        pointer._beam = Geometry("pointer_beam", new BoxMesh { Size = Vector3.One }, BeamTint, 0.6f, eyeFeed);
        pointer._dot = Geometry("pointer_dot", new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 6 }, DotTint, 1f, eyeFeed);
        pointer.AddChild(pointer._beam);
        pointer.AddChild(pointer._dot);
        return pointer;
    }

    public override void _ExitTree()
    {
        LetGo();
        if (ScreenPointer.Override == _reading)
        {
            ScreenPointer.Override = null;
        }
    }

    public override void _Process(double delta) => Step();

    /// <summary>One frame: picks the hand, casts its ray, draws the beam and pushes what changed.
    /// <see cref="_Process"/>'s body, public for a suite.</summary>
    public void Step()
    {
        bool present = _hands.Present(XrHand.Left) || _hands.Present(XrHand.Right);
        Stand(present);
        if (!present)
        {
            HideBeam();
            Move(null, false);
            return;
        }

        PickHand();
        _held = _hands.IsButtonDown(DeviceId.Xr, (int)XrControls.TriggerOf(Hand));
        _swallow &= _held;
        Vector2? at = null;
        if (_theater.Shown && _hands.Aim(Hand) is { } aim && aim.IsFinite())
        {
            var from = aim.Origin;
            var dir = (-aim.Basis.Z).Normalized();
            if (_theater.Hit(from, dir) is { } uv)
            {
                at = _theater.LayoutPoint(uv);
                Draw(from, _theater.Layout.PointAt(uv, _theater.LayoutSize), true);
            }
            else
            {
                Draw(from, from + (dir * MissLengthM), false);
            }
        }
        else
        {
            HideBeam();
        }

        Move(at, _held && !_swallow);
    }

    private static MeshInstance3D Geometry(string name, Mesh mesh, Vector3 tint, float opacity, bool eyeFeed)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = BeamShader(eyeFeed) }, RenderPriority = BeamPriority };
        material.SetShaderParameter("tint_rgb", tint);
        material.SetShaderParameter("opacity", opacity);
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            // Placed by its shader from the eye, as the screen is, so its own bounds mean nothing.
            CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
        };
    }

    private static InputEventMouseButton Button(Vector2 at, bool pressed) => new()
    {
        ButtonIndex = MouseButton.Left,
        Pressed = pressed,
        Position = at,
        GlobalPosition = at,
        ButtonMask = pressed ? MouseButtonMask.Left : 0,
    };

    // Flat colour over everything but the screen's own draw, in the screen's placement.
    private static string BeamShader(bool eyeFeed) => $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled{{(eyeFeed ? ", skip_vertex_transform" : "")}};
        #include "res://shaders/csky_srgb.gdshaderinc"
        {{(eyeFeed ? SceneBuilder.XrInteriorGlobals : "")}}

        uniform vec3 tint_rgb;
        uniform float opacity = 1.0;

        void vertex() {
        {{(eyeFeed ? TheaterScreen.EyeFeedVertex : "")}}
        }

        void fragment() {
            ALBEDO = csky_srgb_to_linear(tint_rgb);
            ALPHA = opacity;
        }
        """;

    // The override stands only while a controller is there. A headset with no controllers keeps the
    // desktop mouse exactly as the theater screen alone left it.
    private void Stand(bool present)
    {
        if (present == _live)
        {
            return;
        }

        _live = present;
        if (present && ScreenPointer.Override == null)
        {
            ScreenPointer.Override = _reading;
        }
        else if (!present && ScreenPointer.Override == _reading)
        {
            ScreenPointer.Override = null;
        }

        Log.Debug("ui", $"theater pointer: {(present ? $"controllers present, pointing with the {Hand.ToString().ToLowerInvariant()} hand" : "no controllers, the desktop mouse points")}");
    }

    // The pointer goes to the other hand on a fresh pull of its trigger, or when its own hand leaves.
    // ⚠ Swallow the pull that moved it: it is a gesture, not a click on whatever the ray lands on.
    private void PickHand()
    {
        var other = XrControls.Other(Hand);
        bool otherDown = _hands.IsButtonDown(DeviceId.Xr, (int)XrControls.TriggerOf(other));
        bool pulled = otherDown && !_otherWasDown && _hands.Aim(other) != null;
        if (pulled || (!_hands.Present(Hand) && _hands.Present(other)))
        {
            LetGo();
            _otherWasDown = _hands.IsButtonDown(DeviceId.Xr, (int)XrControls.TriggerOf(Hand));
            Hand = other;
            _swallow = otherDown;
            Log.Debug("ui", $"theater pointer: the {Hand.ToString().ToLowerInvariant()} hand points");
            return;
        }

        _otherWasDown = otherDown;
    }

    // Pushes the move and the click edges. A click starts only on a fresh pull over the screen. A
    // trigger held while the ray slides onto a plaque presses nothing, as a held mouse button.
    private void Move(Vector2? at, bool pressed)
    {
        bool fresh = pressed && !_wasPressed;
        _wasPressed = pressed;
        if (at is { } point)
        {
            if (At != point)
            {
                Send(new InputEventMouseMotion
                {
                    Position = point,
                    GlobalPosition = point,
                    ButtonMask = _clicking ? MouseButtonMask.Left : 0,
                });
            }

            if (fresh && !_clicking)
            {
                Send(Button(point, true));
                _clicking = true;
            }

            _lastAt = point;
        }

        At = at;
        if (_clicking && !pressed)
        {
            LetGo();
        }
    }

    // Lets a pushed click go where it was last over the screen, so no control is left held.
    private void LetGo()
    {
        if (!_clicking)
        {
            return;
        }

        _clicking = false;
        Send(Button(_lastAt, false));
    }

    private void Send(InputEvent input) => _root.PushInput(input, true);

    private void HideBeam()
    {
        _beam.Visible = false;
        _dot.Visible = false;
    }

    // The beam from the hand to its end, and the dot on a hit, both given in the XR origin's frame.
    private void Draw(Vector3 from, Vector3 to, bool hit)
    {
        var along = to - from;
        float length = along.Length();
        if (length < 1e-4f)
        {
            HideBeam();
            return;
        }

        var dir = along / length;
        var up = Mathf.Abs(dir.Y) < 0.99f ? Vector3.Up : Vector3.Right;
        var side = up.Cross(dir).Normalized();
        var top = dir.Cross(side);
        var origin = _origin();
        var frame = _eyeFeed ? new Transform3D(origin.Basis, Vector3.Zero) : origin;
        _beam.GlobalTransform = frame * new Transform3D(new Basis(side * BeamWidthM, top * BeamWidthM, dir * length), from + (along * 0.5f));
        _beam.Visible = true;
        _dot.Visible = hit;
        if (hit)
        {
            _dot.GlobalTransform = frame * new Transform3D(Basis.Identity.Scaled(Vector3.One * DotM), to + new Vector3(0f, 0f, DotLiftM));
        }
    }
}
