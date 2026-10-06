using System;
using System.Globalization;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The headset's comfort layers, two black quads locked to the head. One is the fade through black
/// that every cut lands on (<see cref="XrFade"/>), the other the optional tunnel vignette
/// (<see cref="XrVignette"/>). One stands per process beside the theater screen, since a fade spans
/// the step from a load screen to the first flight frame. Both draw on the headset layer alone, so a
/// cutscene's pair never draws them into its picture. The fade covers the whole headset view, theater
/// included, except under a board's surround while one shows (docs/architecture/UI.md).
/// </summary>
public sealed partial class ComfortQuads : Node3D
{
    /// <summary>The vignette's draw order: over every head-locked quad of the session and the HUD
    /// panel, under the theater's surround.</summary>
    public const int VignettePriority = 122;

    /// <summary>The fade's draw order while a board shows on the theater: over the world, under
    /// the board's surround, so the board stays readable.</summary>
    public const int FadeUnderBoardPriority = 123;

    /// <summary>The fade's draw order otherwise: the engine's ceiling, over the theater's surround
    /// and screen, so a cutscene's take and hand-back fade too.</summary>
    public const int FadePriority = 127;

    // After the theater (2100), whose board state this frame decides the fade's order.
    private const int AfterTheater = 2110;

    // The quads stand inside the seat and clear of the near plane, as the session's do.
    private const float NearM = 0.25f;

    // How far past the view axis a quad reaches, as a tangent: 83 degrees, past any eye's edge.
    private const float Spread = 8f;

    private XrFade _fade = null!;
    private Func<Transform3D> _origin = null!;
    private Func<bool> _flightLive = null!;
    private Func<bool> _boardShown = null!;
    private Func<VrVignette> _level = null!;
    private MeshInstance3D _fadeQuad = null!;
    private ShaderMaterial _fadeMaterial = null!;
    private MeshInstance3D _vignetteQuad = null!;
    private ShaderMaterial _vignetteMaterial = null!;
    private bool _wasLive;
    private bool _cutThisFrame;
    private float _shownFade = -1f;
    private float _shownHalfAngle = -1f;

    /// <summary>The fade both quads' black follows.</summary>
    public XrFade Fade => _fade;

    /// <summary>The vignette's state, for a suite and the log.</summary>
    public XrVignette Vignette { get; } = new();

    /// <summary>The fade quad, for a suite that reads its material.</summary>
    public MeshInstance3D FadeQuad => _fadeQuad;

    /// <summary>The vignette quad, for a suite that reads its material.</summary>
    public MeshInstance3D VignetteQuad => _vignetteQuad;

    /// <summary>The vignette's clear half-angle at the last <see cref="Refresh"/>, degrees.</summary>
    public float VignetteHalfAngleDeg { get; private set; } = XrVignette.OpenHalfAngleDeg;

    /// <summary>Builds both quads over <paramref name="fade"/>. Four functions give the XR origin's
    /// pose, the flight on view, a board on the theater and the vignette's level. The caller adds
    /// the node and hands it the rig's cuts
    /// (<see cref="OnCut"/>).</summary>
    public static ComfortQuads Build(XrFade fade, Func<Transform3D> origin, Func<bool> flightLive,
        Func<bool> boardShown, Func<VrVignette> level)
    {
        var quads = new ComfortQuads
        {
            Name = "comfort_quads",
            ProcessPriority = AfterTheater,
            _fade = fade,
            _origin = origin,
            _flightLive = flightLive,
            _boardShown = boardShown,
            _level = level,
        };
        var shader = new Shader { Code = QuadShader() };
        (quads._fadeQuad, quads._fadeMaterial) = Quad("comfort_fade", shader, FadePriority);
        (quads._vignetteQuad, quads._vignetteMaterial) = Quad("comfort_vignette", shader, VignettePriority);
        // The fade darkens the whole view; only the vignette's edge has a clear middle.
        quads._fadeMaterial.SetShaderParameter("inner_deg", -90f);
        quads._fadeMaterial.SetShaderParameter("feather_deg", 1f);
        quads._vignetteMaterial.SetShaderParameter("opacity", 1f);
        quads._vignetteMaterial.SetShaderParameter("feather_deg", XrVignette.FeatherDeg);
        quads.AddChild(quads._fadeQuad);
        quads.AddChild(quads._vignetteQuad);
        return quads;
    }

    /// <summary>The seconds one frame advances the comfort layers. Under a scripted run's fixed clock
    /// it is the fixed step, so a capture lands on the same black every run; else the wall delta.</summary>
    public static float FrameDt(double delta) =>
        GameClock.Current is { Mode: GameClock.RunMode.FixedStep } ? GameClock.FixedDt : (float)delta;

    /// <summary>The rig's cut: the view lands on black this frame. Subscribed to
    /// <c>XrRig.Cut</c> by the composition.</summary>
    public void OnCut(string why)
    {
        _fade.Cut();
        _cutThisFrame = true;
    }

    public override void _Process(double delta) => Refresh(FrameDt(delta));

    /// <summary>Steps the fade and the vignette by <paramref name="dt"/> and draws both for this
    /// frame. The first frame a flight is on view after a load lands on black.
    /// <see cref="_Process"/>'s body.</summary>
    public void Refresh(float dt)
    {
        bool live = _flightLive();
        if (live && !_wasLive)
        {
            _fade.Cut();
            _cutThisFrame = true;
            Log.Debug("ui", $"comfort: the first flight frame lands on black");
        }

        _wasLive = live;
        _fade.Step(dt);
        var level = live ? _level() : VrVignette.Off;
        Vignette.Step(level, _origin().Basis, dt, _cutThisFrame);
        _cutThisFrame = false;

        float fade = _fade.Alpha;
        _fadeQuad.Visible = fade > 0f;
        if (fade != _shownFade)
        {
            _shownFade = fade;
            _fadeMaterial.SetShaderParameter("opacity", fade);
        }

        int priority = _boardShown() ? FadeUnderBoardPriority : FadePriority;
        if (_fadeMaterial.RenderPriority != priority)
        {
            _fadeMaterial.RenderPriority = priority;
        }

        VignetteHalfAngleDeg = XrVignette.HalfAngleDeg(level, Vignette.Strength);
        // A clear cone wider than the outer edge's feather leaves nothing on any eye to draw.
        _vignetteQuad.Visible = level != VrVignette.Off && VignetteHalfAngleDeg < XrVignette.OpenHalfAngleDeg - 0.05f;
        if (_vignetteQuad.Visible && Mathf.Abs(VignetteHalfAngleDeg - _shownHalfAngle) > 0.01f)
        {
            _shownHalfAngle = VignetteHalfAngleDeg;
            _vignetteMaterial.SetShaderParameter("inner_deg", VignetteHalfAngleDeg);
        }
    }

    private static (MeshInstance3D Mesh, ShaderMaterial Material) Quad(string name, Shader shader, int priority)
    {
        var material = new ShaderMaterial { Shader = shader, RenderPriority = priority };
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // ⚠ Keep it off every picture camera's layers. A cutscene's pair would draw it into the
            // film, and a fade would then darken the screen's own picture twice.
            Layers = SplitScreen.HeadsetLayer,
            // The shader moves the quad onto the eye, so its own bounds mean nothing. Big enough
            // that no camera anywhere culls it.
            CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
            Visible = false,
        };
        return (mesh, material);
    }

    // Black in view space at NearM, each eye centred on its own axis. It darkens as a canvas fade
    // does, to (1 - opacity) of each sRGB value, which the alpha reaches through a linear blend.
    // The clear cone's half-angle is inner_deg, and past it the black eases in over feather_deg.
    private static string QuadShader()
    {
        var inv = CultureInfo.InvariantCulture;
        return $$"""
            shader_type spatial;
            render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;

            uniform float opacity = 0.0;
            uniform float inner_deg = -90.0;
            uniform float feather_deg = 1.0;
            varying vec2 tangent;

            void vertex() {
                tangent = VERTEX.xy * {{Spread.ToString("0.0", inv)}};
                POSITION = PROJECTION_MATRIX * vec4(tangent * {{NearM.ToString("0.0##", inv)}}, -{{NearM.ToString("0.0##", inv)}}, 1.0);
            }

            void fragment() {
                float off_axis = degrees(atan(length(tangent)));
                float dark = clamp(opacity, 0.0, 1.0) * smoothstep(inner_deg, inner_deg + feather_deg, off_axis);
                ALBEDO = vec3(0.0);
                ALPHA = 1.0 - pow(1.0 - dark, 2.2);
            }
            """;
    }
}
