using System;
using System.Globalization;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The cockpit-locked HUD panel of a stereo session, which draws no canvas layer. The pane's
/// <see cref="Camera.PlayerRig.ChromeParent"/> is its <see cref="Surface"/>, which holds the HUD,
/// messages, scores and chat. The HUD lays out there as on a 1440p screen, since
/// <see cref="HudMetrics"/> takes the surface for its window. A quad in the XR origin's frame
/// shows it, placed from the eye feed as the stereo cockpit is. It rides the seat or the chase pose,
/// never the head. Geometry: <see cref="XrHudPanelLayout"/>; module entry: docs/architecture/Flight.md.
/// </summary>
public sealed partial class XrHudPanel : Node3D
{
    // After the XR origin is placed (XrSession, 2000) and after every HUD feed.
    private const int AfterXrOrigin = 2100;

    private Func<Transform3D> _origin = null!;
    private bool _eyeFeed;
    private MeshInstance3D _quad = null!;

    /// <summary>The 2D the panel shows: the chrome layers parented here, drawn at
    /// <see cref="XrHudPanelLayout.SurfaceSize"/>.</summary>
    public PanelSurface Surface { get; private set; } = null!;

    /// <summary>Where the panel stands and how large it is.</summary>
    public XrHudPanelLayout Layout { get; } = XrHudPanelLayout.Default;

    /// <summary>The quad the surface is drawn on, for a suite that reads its pose and material.</summary>
    public MeshInstance3D Quad => _quad;

    /// <summary>Whether the panel showed at the last <see cref="Refresh"/>.</summary>
    public bool Shown => _quad.Visible;

    /// <summary>Builds the panel for an XR origin whose pose <paramref name="origin"/> reads. With
    /// <paramref name="eyeFeed"/> the quad is placed from the eye feed's declared globals. The
    /// surface copies <paramref name="filterSource"/>'s 2D filtering, the root's, which the HUD art
    /// was drawn for. The caller adds the node to the session's world.</summary>
    public static XrHudPanel Build(Func<Transform3D> origin, bool eyeFeed, Viewport filterSource)
    {
        var panel = new XrHudPanel
        {
            Name = "xr_hud_panel",
            ProcessPriority = AfterXrOrigin,
            _origin = origin,
            _eyeFeed = eyeFeed,
        };
        panel.Surface = new PanelSurface
        {
            Name = "xr_hud_surface",
            Size = XrHudPanelLayout.SurfaceSize,
            TransparentBg = true,
            Disable3D = true,
            GuiDisableInput = true,
            CanvasItemDefaultTextureFilter = filterSource.CanvasItemDefaultTextureFilter,
            CanvasItemDefaultTextureRepeat = filterSource.CanvasItemDefaultTextureRepeat,
            Snap2DTransformsToPixel = filterSource.Snap2DTransformsToPixel,
            Snap2DVerticesToPixel = filterSource.Snap2DVerticesToPixel,
            Msaa2D = filterSource.Msaa2D,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        panel.AddChild(panel.Surface);
        var material = new ShaderMaterial
        {
            Shader = new Shader { Code = PanelShader(eyeFeed) },
            RenderPriority = HeadLockedQuads.HudPanelPriority,
        };
        material.SetShaderParameter("panel", panel.Surface.GetTexture());
        panel._quad = new MeshInstance3D
        {
            Name = "xr_hud_quad",
            Mesh = new QuadMesh { Size = panel.Layout.SizeM },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The shader places it from the eye, so its own bounds mean nothing. Big enough that no
            // camera anywhere culls it.
            CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
            // The headset's own layer. Chat and scores can show over a presenting cutscene, and its
            // pair cameras drop this layer, so the panel stays out of the picture.
            Layers = SplitScreen.HeadsetLayer,
            Visible = false,
        };
        panel.AddChild(panel._quad);
        var inv = CultureInfo.InvariantCulture;
        var size = panel.Layout.SizeM;
        Log.Info("flight", $"stereo: HUD panel {XrHudPanelLayout.SurfaceSize.X}x{XrHudPanelLayout.SurfaceSize.Y} on a {size.X.ToString("0.###", inv)}x{size.Y.ToString("0.###", inv)} m quad {panel.Layout.DistanceM.ToString("0.##", inv)} m ahead, {panel.Layout.VerticalDeg.ToString("0.##", inv)} deg tall at {panel.Layout.RowsPerDegree.ToString("0.#", inv)} rows per degree, {(-panel.Layout.TiltDeg).ToString("0.##", inv)} deg under the seat's axis");
        return panel;
    }

    /// <summary>Whether any chrome layer under <paramref name="surface"/> draws: a visible layer
    /// holding at least one visible canvas item directly.</summary>
    public static bool Draws(Node surface)
    {
        foreach (var child in surface.GetChildren())
        {
            if (child is not CanvasLayer { Visible: true } layer)
            {
                continue;
            }

            foreach (var item in layer.GetChildren())
            {
                if (item is CanvasItem { Visible: true })
                {
                    return true;
                }
            }
        }

        return false;
    }

    public override void _Process(double delta) => Refresh();

    /// <summary>Shows the panel while a chrome layer on it draws, and places it for this frame's
    /// XR origin. A hidden panel stops its surface updating. <see cref="_Process"/>'s body.</summary>
    public void Refresh()
    {
        bool shown = Draws(Surface);
        if (_quad.Visible != shown)
        {
            _quad.Visible = shown;
            Surface.RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            Log.Debug("flight", $"xr hud panel: {(shown ? "shown" : "hidden, no chrome layer draws")}");
        }

        _quad.GlobalTransform = Layout.Placement(_origin(), _eyeFeed);
    }

    // The surface on the quad, mixed over the frame in gamma space as a canvas layer is, from its
    // premultiplied sRGB. Four taps across each pixel's footprint stand in for the mipmaps a viewport
    // texture lacks. A clear texel is discarded, so a transparent panel leaves the frame's bytes alone.
    private static string PanelShader(bool eyeFeed) => $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled{{(eyeFeed ? ", skip_vertex_transform" : "")}};
        #include "res://shaders/csky_srgb.gdshaderinc"
        {{(eyeFeed ? SceneBuilder.XrInteriorGlobals : "")}}

        uniform sampler2D panel : filter_linear, repeat_disable;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

        void vertex() {
        {{(eyeFeed ? TheaterScreen.EyeFeedVertex : "")}}
        }

        void fragment() {
            vec2 dx = dFdx(UV) * 0.25;
            vec2 dy = dFdy(UV) * 0.25;
            vec4 c = 0.25 * (texture(panel, UV - dx - dy) + texture(panel, UV + dx - dy)
                + texture(panel, UV - dx + dy) + texture(panel, UV + dx + dy));
            if (c.a <= 0.0) {
                discard;
            }
            vec3 straight = c.rgb / c.a;
            ALBEDO = csky_srgb_to_linear(clamp(straight, 0.0, 1.0));
            ALPHA = c.a;
        {{Effects.MultiMeshEmitterRenderer.GammaMixComposite}}
        }
        """;

    /// <summary>The panel's SubViewport. A distinct type, so <see cref="HudMetrics"/> can tell a HUD
    /// drawn on the panel from one drawn on a screen.</summary>
    public sealed partial class PanelSurface : SubViewport
    {
    }
}
