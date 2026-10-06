using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The full-view colour layers of a stereo session, drawn as quads locked to the head. They are
/// the cloud whiteout, the burst and blend wash, the sun wash and the mission-end fade. A stereo
/// viewport draws no canvas layer, so each quad copies its flat layer's colour every frame. The shader places each quad in view space, so it rides the head
/// with no lag and covers both eyes. The render priorities below are the stereo twin of
/// <see cref="HudLayers"/>. Module entry: docs/architecture/UI.md.
/// </summary>
public sealed partial class HeadLockedQuads : Node3D
{
    /// <summary>The 3D draw order of the world flare, its four elements on this and the next three.
    /// It draws after every other transparent in the world, as the flat sprites do, and under every
    /// quad below.</summary>
    public const int WorldFlarePriority = 100;

    /// <summary>Reserved for the cockpit-locked HUD panel: over the whiteout and the flash, under the
    /// sun wash and the fade, the order <see cref="HudLayers"/> gives the flat HUD.</summary>
    public const int HudPanelPriority = 115;

    /// <summary>How far ahead of the eye the cockpit interior is taken to end, metres along the view
    /// axis. Nearer geometry is the seat: the whiteout and the flare rings stay off it, as the flat
    /// cockpit pass keeps them off the interior. TUNE. The interior's own wings reach past it, which
    /// the whiteout's stencil test covers and the rings' depth test does not.</summary>
    public const float SeatClearanceM = 1.4f;

    // The quads with no depth test sit here, inside the seat and well clear of the near plane.
    private const float NearM = 0.25f;

    // After every writer of the copied colours (the session, the weather, the flash, the fade).
    // Also after the XR origin, so a quad shows this frame's colour.
    private const int AfterXrOrigin = 2100;

    // How far past the view axis a quad reaches, as a tangent: 8 is 83 degrees off-axis, past the
    // outer edge of any headset's eye.
    private const float Spread = 8f;

    private static readonly Color Clear = new(0f, 0f, 0f, 0f);

    private readonly Dictionary<Tier, Quad> _quads = new();

    /// <summary>The layers a stereo session draws over its world, bottom first.</summary>
    public enum Tier
    {
        /// <summary>The cloud whiteout. Depth-tested at <see cref="SeatClearanceM"/> and kept off the
        /// interior's stencil mark, so it covers the world and leaves the whole interior clear.</summary>
        Whiteout,

        /// <summary>The burst ramp and the blend wash (<see cref="ScreenFlash"/>), over the seat.</summary>
        Flash,

        /// <summary>The sun wash, over the HUD panel as the flat wash is over the HUD.</summary>
        SunWash,

        /// <summary>The mission-end black-out, over everything.</summary>
        MissionEndFade,
    }

    /// <summary>Builds one hidden quad per tier, each following nothing until <see cref="Follow"/>
    /// names its colour. The caller adds the node to the session's world.</summary>
    public static HeadLockedQuads Build()
    {
        var host = new HeadLockedQuads { Name = "head_locked_quads", ProcessPriority = AfterXrOrigin };
        var tested = new Shader { Code = QuadShader(depthTest: true) };
        var untested = new Shader { Code = QuadShader(depthTest: false) };
        foreach (Tier tier in Enum.GetValues<Tier>())
        {
            bool seat = tier == Tier.Whiteout;
            var material = new ShaderMaterial
            {
                Shader = seat ? tested : untested,
                RenderPriority = PriorityOf(tier),
            };
            material.SetShaderParameter("distance_m", seat ? SeatClearanceM : NearM);
            var mesh = new MeshInstance3D
            {
                Name = $"quad_{tier.ToString().ToLowerInvariant()}",
                Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // The shader moves the quad onto the eye, so the node's own bounds mean nothing.
                // They are made big enough that no camera anywhere culls it.
                CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
                Visible = false,
            };
            host.AddChild(mesh);
            host._quads[tier] = new Quad(mesh, material);
        }

        return host;
    }

    /// <summary>The draw order of one tier, among the world's transparents. The stereo interior's
    /// glass draws between the whiteout and the flash (<c>Mech3.SceneBuilder.XrInteriorBlendPriority</c>),
    /// as the flat cockpit pass sits between their canvas layers.</summary>
    public static int PriorityOf(Tier tier) => tier switch
    {
        Tier.Whiteout => 109,
        Tier.Flash => 111,
        Tier.SunWash => 120,
        _ => 121,
    };

    /// <summary>A canvas colour rectangle's colour as it is drawn. It is clear when the rectangle is
    /// not built, or it or its canvas layer is hidden (a cutscene lowers the world overlays).</summary>
    public static Color CanvasColour(ColorRect? rect)
    {
        if (rect == null || !GodotObject.IsInstanceValid(rect) || !rect.Visible)
        {
            return Clear;
        }

        return rect.GetParent() is CanvasLayer { Visible: false } ? Clear : rect.Color;
    }

    /// <summary>Has <paramref name="tier"/>'s quad copy <paramref name="colour"/> every frame, a
    /// colour in the flat layer's own terms (sRGB and alpha).</summary>
    public void Follow(Tier tier, Func<Color> colour) => _quads[tier].Source = colour;

    /// <summary>What <paramref name="tier"/>'s quad drew with at the last <see cref="Refresh"/>, clear
    /// while it is hidden. Read by the suite.</summary>
    public Color ColourOf(Tier tier) => _quads[tier].Mesh.Visible ? _quads[tier].Shown : Clear;

    /// <summary>The quad of one tier, for a suite that reads its material.</summary>
    public MeshInstance3D QuadOf(Tier tier) => _quads[tier].Mesh;

    public override void _Process(double delta) => Refresh();

    /// <summary>Copies every followed colour onto its quad and hides a clear one. A session that never
    /// whites out or fades then draws no quad at all. <see cref="_Process"/>'s body.</summary>
    public void Refresh()
    {
        foreach (var quad in _quads.Values)
        {
            Color c = quad.Source?.Invoke() ?? Clear;
            bool shows = c.A > 0f;
            quad.Mesh.Visible = shows;
            if (shows && c != quad.Shown)
            {
                // A Vector4, not the Color: Godot linearises a Color on a 3D material before the
                // shader's own sRGB conversion would run a second time (SHOT-47).
                quad.Material.SetShaderParameter("tint", new Vector4(c.R, c.G, c.B, c.A));
            }

            quad.Shown = c;
        }
    }

    // A colour quad in view space at distance_m, composited in gamma space like its canvas layer.
    // The alpha remap is the puffer mix's own (docs/org/puffer.md).
    // The depth-tested quad also skips the stereo interior's stencil mark, which covers its wings.
    private static string QuadShader(bool depthTest) => $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled{{(depthTest ? $"; stencil_mode read, compare_not_equal, {Mech3.SceneBuilder.XrInteriorStencil}" : ", depth_test_disabled")}};
        #include "res://shaders/csky_srgb.gdshaderinc"

        uniform vec4 tint = vec4(0.0);
        uniform float distance_m = 0.25;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;

        void vertex() {
            POSITION = PROJECTION_MATRIX * vec4(VERTEX.xy * (distance_m * {{Spread.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}}), -distance_m, 1.0);
        }

        void fragment() {
            ALBEDO = csky_srgb_to_linear(tint.rgb);
            ALPHA = tint.a;
        {{Effects.MultiMeshEmitterRenderer.GammaMixComposite}}
        }
        """;

    private sealed class Quad
    {
        public Quad(MeshInstance3D mesh, ShaderMaterial material)
        {
            Mesh = mesh;
            Material = material;
        }

        public MeshInstance3D Mesh { get; }

        public ShaderMaterial Material { get; }

        public Func<Color>? Source { get; set; }

        public Color Shown { get; set; } = new(0f, 0f, 0f, 0f);
    }
}
