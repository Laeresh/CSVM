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
/// any tonemap off the boards. In flight a board brings the screen up over a dimmed world, a
/// presenting cutscene in the full surround as a <see cref="StereoPair"/> (docs/architecture/UI.md).
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

    private readonly SubViewport[] _eyes = new SubViewport[2];
    private readonly Camera3D[] _eyeCameras = new Camera3D[2];
    private Viewport _root = null!;
    private Camera3D? _camera;
    private Func<Transform3D> _origin = null!;
    private Func<bool> _flightLive = null!;
    private Func<TheaterCutscene?> _cutscene = () => null;
    private Func<float> _interocular = () => StereoPair.DefaultInterocularM;
    private bool _eyeFeed;
    private CanvasRehost _rehost = null!;
    private ShaderMaterial _screenMaterial = null!;
    private MeshInstance3D _screen = null!;
    private QuadMesh _quad = null!;
    private MeshInstance3D _surround = null!;
    private ShaderMaterial _surroundMaterial = null!;
    private Godot.Environment _plain = null!;
    private Godot.Environment? _cameraEnv;
    private bool _plainOn;
    // The camera's own cull mask while a cutscene has the headset draw the theater alone, null
    // otherwise. Put back before every physics and process pass, so no writer sees the cut.
    private uint? _ownMask;
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

    /// <summary>Whether a cutscene played on the screen at the last <see cref="Refresh"/>.</summary>
    public bool PlaysCutscene { get; private set; }

    /// <summary>Whether a board, menu or cinema showed on the screen at the last
    /// <see cref="Refresh"/>: always with no flight live, in flight once one raised it.</summary>
    public bool BoardShown { get; private set; }

    /// <summary>The cutscene's two renders, left eye first, updating only while one plays.</summary>
    public IReadOnlyList<SubViewport> CutsceneEyes => _eyes;

    /// <summary>The cameras of <see cref="CutsceneEyes"/>, posed by <see cref="StereoPair"/>.</summary>
    public IReadOnlyList<Camera3D> CutsceneCameras => _eyeCameras;

    /// <summary>Builds the screen over the 2D of <paramref name="root"/>. The XR origin's pose comes
    /// from <paramref name="origin"/>, whether a flight is on view from <paramref name="flightLive"/>,
    /// and a presenting cutscene from <paramref name="cutscene"/>, its pair <paramref name="interocular"/>
    /// metres apart. The <paramref name="camera"/> takes the plain environment while no flight is on view
    /// or a cutscene plays. With <paramref name="eyeFeed"/> the quad is placed from the eye feed's
    /// globals. The caller adds the node to the tree.</summary>
    public static TheaterScreen Build(Viewport root, Camera3D? camera, Func<Transform3D> origin, Func<bool> flightLive, bool eyeFeed,
        Func<TheaterCutscene?>? cutscene = null, Func<float>? interocular = null)
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
        if (cutscene != null)
        {
            theater._cutscene = cutscene;
        }

        if (interocular != null)
        {
            theater._interocular = interocular;
        }

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
        theater._screenMaterial = screenMaterial;
        theater._quad = new QuadMesh { Size = Vector2.One };
        theater._screen = Geometry("screen_quad", theater._quad, screenMaterial);
        theater.AddChild(theater._screen);
        theater.BuildEyes(root);

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

    public override void _EnterTree()
    {
        _rehost.Attach();
        GetTree().PhysicsFrame += RestoreMask;
        GetTree().ProcessFrame += RestoreMask;
    }

    public override void _ExitTree()
    {
        _rehost.Detach();
        GetTree().PhysicsFrame -= RestoreMask;
        GetTree().ProcessFrame -= RestoreMask;
        RestoreMask();
        Plain(false);
        if (LayoutSize != Vector2.Zero)
        {
            _root.OversamplingOverride = _rootOversampling;
        }
    }

    public override void _Process(double delta) => Refresh();

    /// <summary>Follows the root's layout size and shows or hides the screen and its surround. Then
    /// it swaps the camera's environment, places both and poses a cutscene's pair for this frame.
    /// <see cref="_Process"/>'s body.</summary>
    public void Refresh()
    {
        RestoreMask();
        Vector2 layout = _root.GetVisibleRect().Size;
        if (layout != LayoutSize && layout.X >= 1f && layout.Y >= 1f)
        {
            Resize(layout);
        }

        bool live = _flightLive();
        var raiser = live ? _rehost.Showing(TheaterLayout.Raises) : null;
        var cutscene = live && _cutscene() is { } playing && IsInstanceValid(playing.Camera) ? playing : (TheaterCutscene?)null;
        BoardShown = !live || raiser != null;
        bool shown = BoardShown || cutscene != null;
        // A cutscene takes the full surround. Its camera flies through the world the seat stands in,
        // and that world showing round the window would move against the picture.
        SurroundOpacity = !live || cutscene != null ? 1f : raiser != null ? DimOpacity : 0f;
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

        // A cutscene's picture is already tonemapped by its own cameras, so the screen takes none.
        Plain(!live || cutscene != null);
        Place(_origin());
        Pose(cutscene);
        CutWorld(cutscene != null);

        string state = !live ? "no flight"
            : cutscene != null ? $"a cutscene in stereo{(raiser != null ? $" under {raiser.Name}" : "")}"
            : raiser != null ? $"over flight, raised by {raiser.Name}" : "hidden in flight";
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
        // ⚠ Keep it off every picture camera's layers. The cutscene pair would otherwise draw the
        // screen into its own picture, and the surround over all of it.
        Layers = SplitScreen.HeadsetLayer,
        // The shaders place it from the eye, so its own bounds mean nothing. Big enough that no
        // camera anywhere culls it.
        CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
    };

    // The surface on the quad, composited as the canvas would be: its premultiplied sRGB taken to
    // linear. Four taps across each pixel's footprint stand in for the mipmaps a viewport texture
    // lacks. A playing cutscene fills its rect under the boards, each eye its own picture.
    private static string ScreenShader(bool eyeFeed) => $$"""
        shader_type spatial;
        render_mode blend_premul_alpha, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled{{(eyeFeed ? ", skip_vertex_transform" : "")}};
        #include "res://shaders/csky_srgb.gdshaderinc"
        {{(eyeFeed ? SceneBuilder.XrInteriorGlobals : "")}}

        uniform sampler2D screen_tex : filter_linear, repeat_disable;
        uniform sampler2D cutscene_left : filter_linear, repeat_disable;
        uniform sampler2D cutscene_right : filter_linear, repeat_disable;
        uniform float cutscene_on = 0.0;
        uniform vec4 cutscene_rect = vec4(0.0, 0.0, 1.0, 1.0);

        vec4 four_taps(sampler2D tex, vec2 uv, vec2 dx, vec2 dy) {
            return 0.25 * (texture(tex, uv - dx - dy) + texture(tex, uv + dx - dy)
                + texture(tex, uv - dx + dy) + texture(tex, uv + dx + dy));
        }

        void vertex() {
        {{(eyeFeed ? EyeFeedVertex : "")}}
        }

        void fragment() {
            vec2 dx = dFdx(UV) * 0.25;
            vec2 dy = dFdy(UV) * 0.25;
            vec4 c = four_taps(screen_tex, UV, dx, dy);
            vec3 straight = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);
            vec3 board = csky_srgb_to_linear(straight) * c.a;
            vec2 at = (UV - cutscene_rect.xy) / cutscene_rect.zw;
            vec2 adx = dx / cutscene_rect.zw;
            vec2 ady = dy / cutscene_rect.zw;
            vec3 left = four_taps(cutscene_left, at, adx, ady).rgb;
            vec3 right = four_taps(cutscene_right, at, adx, ady).rgb;
            vec3 picture = csky_srgb_to_linear(VIEW_INDEX == 0 ? left : right);
            float inside = cutscene_on * step(0.0, at.x) * step(at.x, 1.0) * step(0.0, at.y) * step(at.y, 1.0);
            ALBEDO = board + (picture * inside * (1.0 - c.a));
            ALPHA = max(c.a, inside);
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

    // The cutscene's two renders, drawing the root's world. Each camera's frustum is shifted, so its
    // own framing comes from StereoPair rather than from a field of view.
    private void BuildEyes(Viewport root)
    {
        for (int view = 0; view < _eyes.Length; view++)
        {
            string side = view == 0 ? "left" : "right";
            var eye = new SubViewport
            {
                Name = $"cutscene_{side}",
                Size = TheaterLayout.CutsceneSize,
                GuiDisableInput = true,
                Msaa3D = root.Msaa3D,
                ScreenSpaceAA = root.ScreenSpaceAA,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            };
            var camera = new Camera3D
            {
                Name = $"cutscene_camera_{side}",
                Projection = Camera3D.ProjectionType.Frustum,
                KeepAspect = Camera3D.KeepAspectEnum.Height,
                Current = true,
            };
            eye.AddChild(camera);
            AddChild(eye);
            _eyes[view] = eye;
            _eyeCameras[view] = camera;
            _screenMaterial.SetShaderParameter($"cutscene_{side}", eye.GetTexture());
        }
    }

    // Stands the pair on the cutscene's camera for this frame, or stops both renders with none.
    // Each eye takes the camera's framing, planes, layers and look, less the headset's own layer.
    private void Pose(TheaterCutscene? cutscene)
    {
        bool on = cutscene != null;
        bool started = on && !PlaysCutscene;
        if (on != PlaysCutscene)
        {
            PlaysCutscene = on;
            foreach (var eye in _eyes)
            {
                eye.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            }

            _screenMaterial.SetShaderParameter("cutscene_on", on ? 1f : 0f);
        }

        if (cutscene is not { } shot)
        {
            return;
        }

        var source = shot.Camera;
        var pose = source.GlobalTransform.Orthonormalized();
        float ipd = _interocular();
        // The look the world gives the flat camera: the one it wore before the plain swap took it.
        var look = source == _camera && _plainOn ? _cameraEnv : source.Environment;
        for (int view = 0; view < _eyeCameras.Length; view++)
        {
            var camera = _eyeCameras[view];
            camera.Transform = StereoPair.Eye(pose, view, ipd);
            camera.Near = source.Near;
            camera.Far = source.Far;
            camera.Size = StereoPair.FrustumHeightM(source.Fov, source.Near);
            camera.FrustumOffset = new Vector2(StereoPair.FrustumOffsetM(view, ipd, source.Near, shot.ConvergenceM), 0f);
            camera.CullMask = source.CullMask & ~SplitScreen.HeadsetLayer;
            camera.Environment = look;
            camera.Attributes = source.Attributes;
        }

        if (started)
        {
            var inv = CultureInfo.InvariantCulture;
            Log.Info("ui", $"theater: cutscene pair {TheaterLayout.CutsceneSize.X}x{TheaterLayout.CutsceneSize.Y} per eye, {(ipd * 1000f).ToString("0.#", inv)} mm apart, converged at {shot.ConvergenceM.ToString("0.##", inv)} m, framed at {source.Fov.ToString("0.##", inv)} deg");
        }
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
        var rect = TheaterLayout.CutsceneRect(layout);
        _screenMaterial.SetShaderParameter("cutscene_rect", new Vector4(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y));
        var inv = CultureInfo.InvariantCulture;
        Log.Info("ui", $"theater: layout {size2d.X}x{size2d.Y} drawn at {Surface.Size.X}x{Surface.Size.Y}, screen {_quad.Size.X.ToString("0.###", inv)}x{_quad.Size.Y.ToString("0.###", inv)} m, board {Layout.VerticalDeg.ToString("0.#", inv)} deg at {Layout.RowsPerDegree().ToString("0.#", inv)} rows per degree");
    }

    // Under a cutscene the opaque surround hides the whole world. The headset then draws its own
    // layer alone for this frame, with no world, sky or sun shadow behind it. Called after Pose,
    // which reads the camera's own mask for the pair.
    private void CutWorld(bool on)
    {
        if (!on || _camera == null || !IsInstanceValid(_camera))
        {
            return;
        }

        _ownMask = _camera.CullMask;
        _camera.CullMask = SplitScreen.HeadsetLayer;
    }

    // Puts the camera's own mask back, so the zone gate and every other writer works on it.
    private void RestoreMask()
    {
        if (_ownMask is { } mask && _camera != null && IsInstanceValid(_camera))
        {
            _camera.CullMask = mask;
        }

        _ownMask = null;
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
