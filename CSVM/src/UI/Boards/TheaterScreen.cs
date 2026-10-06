using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The theater screen of a stereo session: the menus, boards, load screens and cinemas on a quad in
/// front of the seated player. A stereo root draws no canvas layer, so <see cref="CanvasRehost"/>
/// moves every layer <see cref="TheaterLayout.Hosts"/> accepts into the quad's <see cref="Surface"/>.
/// Their controls stay the root's, so the pad and the keyboard drive them exactly as in flat play.
/// With no flight live the screen stands in a dark surround, and the camera's plain environment keeps
/// any tonemap off the boards. In flight a board brings the screen up over a dimmed world. Module
/// entry: docs/architecture/UI.md.
/// </summary>
public sealed partial class TheaterScreen : Node3D
{
    /// <summary>The surround's draw order: after every other transparent, the mission-end fade's
    /// head-locked quad included, so a results board reads over a faded world.</summary>
    public const int SurroundPriority = 125;

    /// <summary>The screen's draw order, over its surround.</summary>
    public const int ScreenPriority = 126;

    /// <summary>The surround's opacity behind a board in flight: the halted world dimmed, which
    /// gives the board's translucent panels contrast against a bright sky. TUNE.</summary>
    public const float DimOpacity = 0.6f;

    // The quad's vertex stage under the eye feed. Each eye places it from its own offset to it, the
    // way the stereo cockpit interior is placed (Mech3/SceneBuilder.cs). The pointer's beam shares it.
    internal const string EyeFeedVertex = $"""
            vec3 csky_rel = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            vec3 csky_eye = VIEW_INDEX == 0 ? {SceneBuilder.XrEyeLeftGlobal} : {SceneBuilder.XrEyeRightGlobal};
            VERTEX = mat3(VIEW_MATRIX) * (csky_rel - csky_eye) + EYE_OFFSET;
        """;

    // After the XR origin is placed (XrSession, 2000) and after every board's own _Process.
    private const int AfterXrOrigin = 2100;

    // Far enough that leaning in the seat shows no parallax on it, inside any camera's far plane.
    private const float SurroundRadiusM = 20f;

    // The surround's floor and ceiling, sRGB: a dark room with a horizon to sit level with. TUNE.
    // A Vector3, not a Color: Godot hands a Color to a shader already made linear.
    private static readonly Vector3 Floor = new(0.075f, 0.075f, 0.08f);
    private static readonly Vector3 Ceiling = new(0.015f, 0.015f, 0.018f);

    private Viewport _root = null!;
    private Camera3D? _camera;
    private Func<Transform3D> _origin = null!;
    private Func<bool> _flightLive = null!;
    private bool _eyeFeed;
    private CanvasRehost _rehost = null!;
    private MeshInstance3D _screen = null!;
    private QuadMesh _quad = null!;
    private MeshInstance3D _surround = null!;
    private ShaderMaterial _surroundMaterial = null!;
    private Godot.Environment _plain = null!;
    private Godot.Environment? _cameraEnv;
    private bool _plainOn;
    private float _rootOversampling;
    private float _shownOpacity = -1f;
    private string _state = string.Empty;

    /// <summary>The 2D the screen shows: the hosted layers, drawn at
    /// <see cref="TheaterLayout.SurfaceSize"/>.</summary>
    public SubViewport Surface { get; private set; } = null!;

    /// <summary>Where the screen stands and how finely it is drawn.</summary>
    public TheaterLayout Layout { get; } = TheaterLayout.Default;

    /// <summary>The root's 2D layout size the screen shows, which the hosted controls lay out in.
    /// Under a stereo root it is one eye's render target, which Godot sizes from the runtime.</summary>
    public Vector2 LayoutSize { get; private set; }

    /// <summary>The surround's opacity at the last <see cref="Refresh"/>: 1 with no flight live,
    /// <see cref="DimOpacity"/> behind a board in flight, else 0.</summary>
    public float SurroundOpacity { get; private set; }

    /// <summary>Whether the screen showed at the last <see cref="Refresh"/>.</summary>
    public bool Shown => _screen.Visible;

    /// <summary>The quad the surface is drawn on, for a suite that reads its material.</summary>
    public MeshInstance3D ScreenQuad => _screen;

    /// <summary>The canvas layers drawing on the screen now.</summary>
    public IReadOnlyCollection<CanvasLayer> Hosted => _rehost.Layers;

    /// <summary>Builds the screen over the 2D of <paramref name="root"/>. The XR origin's pose comes
    /// from <paramref name="origin"/>, and whether a flight is on view from <paramref name="flightLive"/>.
    /// The <paramref name="camera"/> takes the plain environment while none is. With
    /// <paramref name="eyeFeed"/> the quad is placed from the eye feed's declared globals. The caller
    /// adds the node to the tree.</summary>
    public static TheaterScreen Build(Viewport root, Camera3D? camera, Func<Transform3D> origin, Func<bool> flightLive, bool eyeFeed)
    {
        var theater = new TheaterScreen
        {
            Name = "theater_screen",
            ProcessPriority = AfterXrOrigin,
            _root = root,
            _camera = camera,
            _origin = origin,
            _flightLive = flightLive,
            _eyeFeed = eyeFeed,
            _plain = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black },
        };
        theater.Surface = new SubViewport
        {
            Name = "theater_surface",
            TransparentBg = true,
            Disable3D = true,
            GuiDisableInput = true,
            Size2DOverrideStretch = true,
            CanvasItemDefaultTextureFilter = root.CanvasItemDefaultTextureFilter,
            CanvasItemDefaultTextureRepeat = root.CanvasItemDefaultTextureRepeat,
            Snap2DTransformsToPixel = root.Snap2DTransformsToPixel,
            Snap2DVerticesToPixel = root.Snap2DVerticesToPixel,
            Msaa2D = root.Msaa2D,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        theater.AddChild(theater.Surface);
        theater._rehost = new CanvasRehost(root, theater.Surface, TheaterLayout.Hosts);

        var screenMaterial = new ShaderMaterial
        {
            Shader = new Shader { Code = ScreenShader(eyeFeed) },
            RenderPriority = ScreenPriority,
        };
        screenMaterial.SetShaderParameter("screen_tex", theater.Surface.GetTexture());
        theater._quad = new QuadMesh { Size = Vector2.One };
        theater._screen = Geometry("screen_quad", theater._quad, screenMaterial);
        theater.AddChild(theater._screen);

        theater._surroundMaterial = new ShaderMaterial
        {
            Shader = new Shader { Code = SurroundShader() },
            RenderPriority = SurroundPriority,
        };
        theater._surroundMaterial.SetShaderParameter("radius_m", SurroundRadiusM);
        theater._surroundMaterial.SetShaderParameter("floor_rgb", Floor);
        theater._surroundMaterial.SetShaderParameter("ceiling_rgb", Ceiling);
        var sphere = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 };
        theater._surround = Geometry("surround", sphere, theater._surroundMaterial);
        theater.AddChild(theater._surround);
        return theater;
    }

    /// <summary>Where a pointer ray, given in the XR origin's frame, meets the screen. (0, 0) is the
    /// top left and (1, 1) the bottom right; null when it misses. See <see cref="TheaterLayout.Hit"/>.</summary>
    public Vector2? Hit(Vector3 fromInOrigin, Vector3 dirInOrigin) =>
        LayoutSize.Y > 0f ? Layout.Hit(fromInOrigin, dirInOrigin, LayoutSize) : null;

    /// <summary>The point of the root's 2D layout a screen point shows. A pointer's mouse event
    /// carries it into the root through <c>PushInput(event, true)</c>, as the hosted controls' own
    /// coordinates.</summary>
    public Vector2 LayoutPoint(Vector2 uv) => TheaterLayout.LayoutPoint(uv, LayoutSize);

    public override void _EnterTree() => _rehost.Attach();

    public override void _ExitTree()
    {
        _rehost.Detach();
        Plain(false);
        if (LayoutSize != Vector2.Zero)
        {
            _root.OversamplingOverride = _rootOversampling;
        }
    }

    public override void _Process(double delta) => Refresh();

    /// <summary>Follows the root's layout size, shows or hides the screen and its surround, swaps the
    /// camera's environment and places both for this frame. <see cref="_Process"/>'s body.</summary>
    public void Refresh()
    {
        Vector2 layout = _root.GetVisibleRect().Size;
        if (layout != LayoutSize && layout.X >= 1f && layout.Y >= 1f)
        {
            Resize(layout);
        }

        bool live = _flightLive();
        var raiser = live ? _rehost.Showing(TheaterLayout.Raises) : null;
        bool shown = !live || raiser != null;
        SurroundOpacity = !live ? 1f : raiser != null ? DimOpacity : 0f;
        if (_screen.Visible != shown)
        {
            _screen.Visible = shown;
            Surface.RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        }

        _surround.Visible = SurroundOpacity > 0f;
        if (SurroundOpacity != _shownOpacity)
        {
            _shownOpacity = SurroundOpacity;
            _surroundMaterial.SetShaderParameter("opacity", SurroundOpacity);
        }

        Plain(!live);
        Place(_origin());

        string state = !live ? "no flight" : raiser != null ? $"over flight, raised by {raiser.Name}" : "hidden in flight";
        if (state != _state)
        {
            _state = state;
            Log.Debug("ui", $"theater: {state} surround={SurroundOpacity.ToString("0.##", CultureInfo.InvariantCulture)} layers={_rehost.Layers.Count}");
        }
    }

    private static MeshInstance3D Geometry(string name, Mesh mesh, ShaderMaterial material) => new()
    {
        Name = name,
        Mesh = mesh,
        MaterialOverride = material,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        // The shaders place it from the eye, so its own bounds mean nothing. Big enough that no
        // camera anywhere culls it.
        CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
    };

    // The surface on the quad, composited as the canvas would be: its premultiplied sRGB taken to
    // linear. Four taps across each pixel's footprint stand in for the mipmaps a viewport texture
    // lacks.
    private static string ScreenShader(bool eyeFeed) => $$"""
        shader_type spatial;
        render_mode blend_premul_alpha, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled{{(eyeFeed ? ", skip_vertex_transform" : "")}};
        #include "res://shaders/csky_srgb.gdshaderinc"
        {{(eyeFeed ? SceneBuilder.XrInteriorGlobals : "")}}

        uniform sampler2D screen_tex : filter_linear, repeat_disable;

        void vertex() {
        {{(eyeFeed ? EyeFeedVertex : "")}}
        }

        void fragment() {
            vec2 dx = dFdx(UV) * 0.25;
            vec2 dy = dFdy(UV) * 0.25;
            vec4 c = 0.25 * (texture(screen_tex, UV - dx - dy) + texture(screen_tex, UV + dx - dy)
                + texture(screen_tex, UV - dx + dy) + texture(screen_tex, UV + dx + dy));
            vec3 straight = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);
            ALBEDO = csky_srgb_to_linear(straight) * c.a;
            ALPHA = c.a;
        }
        """;

    // A sphere around the eye in the origin's axes, so its horizon stays level with the seat.
    private static string SurroundShader() => """
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;
        #include "res://shaders/csky_srgb.gdshaderinc"

        uniform float opacity = 1.0;
        uniform float radius_m = 20.0;
        uniform vec3 floor_rgb;
        uniform vec3 ceiling_rgb;
        varying float up;

        void vertex() {
            up = VERTEX.y;
            vec3 dir = mat3(MODEL_MATRIX) * VERTEX;
            POSITION = PROJECTION_MATRIX * vec4(mat3(VIEW_MATRIX) * dir * radius_m, 1.0);
        }

        void fragment() {
            ALBEDO = csky_srgb_to_linear(mix(floor_rgb, ceiling_rgb, smoothstep(-0.06, 0.06, up)));
            ALPHA = opacity;
        }
        """;

    // The screen's quad as its shader receives it. Under the eye feed that is its offset from the
    // XR origin in the origin's axes, and without it the plain world pose.
    private void Place(in Transform3D origin)
    {
        var axes = new Transform3D(origin.Basis, Vector3.Zero);
        _screen.GlobalTransform = (_eyeFeed ? axes : origin) * Layout.Pose;
        _surround.GlobalTransform = axes;
    }

    private void Resize(Vector2 layout)
    {
        if (LayoutSize == Vector2.Zero)
        {
            _rootOversampling = _root.OversamplingOverride;
        }

        LayoutSize = layout;
        var size2d = new Vector2I(Mathf.RoundToInt(layout.X), Mathf.RoundToInt(layout.Y));
        Surface.Size = TheaterLayout.SurfaceSize(layout);
        Surface.Size2DOverride = size2d;
        // Text on a hosted layer is rasterised at the root's oversampling, not the surface's.
        _root.OversamplingOverride = TheaterLayout.SurfaceScale(layout);
        _quad.Size = Layout.SizeM(layout);
        var inv = CultureInfo.InvariantCulture;
        Log.Info("ui", $"theater: layout {size2d.X}x{size2d.Y} drawn at {Surface.Size.X}x{Surface.Size.Y}, screen {_quad.Size.X.ToString("0.###", inv)}x{_quad.Size.Y.ToString("0.###", inv)} m, board {Layout.VerticalDeg.ToString("0.#", inv)} deg at {Layout.RowsPerDegree().ToString("0.#", inv)} rows per degree");
    }

    // The plain environment while no flight is live: no tonemap, glow or adjustment on the boards.
    private void Plain(bool on)
    {
        if (on == _plainOn || _camera == null || !IsInstanceValid(_camera))
        {
            return;
        }

        _plainOn = on;
        if (on)
        {
            _cameraEnv = _camera.Environment;
            _camera.Environment = _plain;
        }
        else
        {
            _camera.Environment = _cameraEnv;
            _cameraEnv = null;
        }
    }
}
