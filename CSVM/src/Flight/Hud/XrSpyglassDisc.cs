using CSVM.UI.Boards;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The spyglass picture in a stereo session: the flat disc's (<see cref="Camera.SpyglassView"/>),
/// rendered once, on a quad on the head ring beside the selection's arrow. Its shader places it in
/// the head's frame as the ring cards are, so both eyes see one mono picture at the ring's depth.
/// It masks the picture to a circle with the flat rim and drop shadow in the marker's colour.
/// It mixes in gamma space as the canvas mixes the flat disc. <see cref="XrMarkers"/> places it.
/// Module entry: docs/architecture/Flight.md.
/// </summary>
public sealed partial class XrSpyglassDisc : MeshInstance3D
{
    // The flat rim's width and its drop shadow's offset, reference pixels (TargetHud's disc).
    private const float RimRef = 2f;
    private const float ShadowRef = 1.5f;

    // A margin past the rim, reference pixels, which holds the shadow and the edge's smoothing.
    private const float PadRef = 4f;

    // The picture in the circle, then the shadow ring and the rim, composed in sRGB as the canvas
    // draws the flat disc. It mixes over the frame in gamma space as a canvas layer does.
    // The picture is a 3D viewport's texture, already in sRGB. Four taps stand in for its mipmaps.
    private const string DiscShader = $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;
        #include "res://shaders/csky_srgb.gdshaderinc"

        uniform sampler2D picture : filter_linear, repeat_disable;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
        uniform vec3 view_centre = vec3(0.0, 0.0, -2.0);
        uniform float units = 0.001;
        uniform float radius_ref = 72.0;
        uniform float half_ref = 76.0;
        uniform float rim_ref = 2.0;
        uniform float shadow_ref = 1.5;
        uniform vec4 rim_colour = vec4(1.0);

        float band(float r, float centre, float half_width, float aa) {
            return clamp((half_width - abs(r - centre)) / aa + 0.5, 0.0, 1.0);
        }

        void vertex() {
            POSITION = PROJECTION_MATRIX * vec4(view_centre + vec3(VERTEX.xy * (2.0 * half_ref * units), 0.0), 1.0);
        }

        void fragment() {
            vec2 p = (UV - vec2(0.5)) * (2.0 * half_ref);
            float r = length(p);
            float aa = max(fwidth(r), 1e-4);
            vec2 uv = p / (2.0 * radius_ref) + vec2(0.5);
            vec2 dx = dFdx(uv) * 0.25;
            vec2 dy = dFdy(uv) * 0.25;
            vec3 pic = 0.25 * (texture(picture, uv - dx - dy).rgb + texture(picture, uv + dx - dy).rgb
                + texture(picture, uv - dx + dy).rgb + texture(picture, uv + dx + dy).rgb);
            float a = clamp((radius_ref - r) / aa + 0.5, 0.0, 1.0);
            vec3 pre = pic * a;
            float shade = band(length(p - vec2(shadow_ref)), radius_ref, rim_ref * 0.5, aa) * 0.75;
            pre *= 1.0 - shade;
            a = shade + (a * (1.0 - shade));
            float ring = band(r, radius_ref, rim_ref * 0.5, aa) * rim_colour.a;
            pre = (rim_colour.rgb * ring) + (pre * (1.0 - ring));
            a = ring + (a * (1.0 - ring));
            if (a <= 0.0) {
                discard;
            }
            ALBEDO = csky_srgb_to_linear(clamp(pre / a, 0.0, 1.0));
            ALPHA = a;
        {{Effects.MultiMeshEmitterRenderer.GammaMixComposite}}
        }
        """;

    private ShaderMaterial _material = null!;
    private Texture2D? _picture;
    private Color? _rim;

    /// <summary>The picture this disc last showed, or null while hidden. Read by the suite.</summary>
    public Texture2D? Showing => Visible ? _picture : null;

    /// <summary>Builds the hidden disc. The caller adds it to the session's world.</summary>
    public static XrSpyglassDisc Build()
    {
        var material = new ShaderMaterial
        {
            Shader = new Shader { Code = DiscShader },
            RenderPriority = HeadLockedQuads.SpyglassPriority,
        };
        material.SetShaderParameter("rim_ref", RimRef);
        material.SetShaderParameter("shadow_ref", ShadowRef);
        return new XrSpyglassDisc
        {
            Name = "xr_spyglass_disc",
            Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = material,
            CastShadow = ShadowCastingSetting.Off,
            // The shader places it on the head, so its own bounds mean nothing. Big enough that no
            // camera anywhere culls it.
            CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
            // ⚠ Keep it off layer 1: the picture's own camera draws that layer and would sample itself.
            Layers = SplitScreen.HeadOverlayLayer,
            Visible = false,
            _material = material,
        };
    }

    /// <summary>Shows <paramref name="picture"/> as a disc <paramref name="radiusRef"/> reference
    /// pixels in radius, centred at <paramref name="viewCentre"/> in the head's frame with
    /// <paramref name="metresPerRef"/> metres to a reference pixel, its rim in
    /// <paramref name="rim"/>.</summary>
    public void Show(Vector3 viewCentre, float metresPerRef, float radiusRef, Texture2D picture, Color rim)
    {
        if (!ReferenceEquals(picture, _picture))
        {
            _material.SetShaderParameter("picture", picture);
            _picture = picture;
        }

        if (_rim != rim)
        {
            // A Vector4, not the Color: Godot linearises a Color on a 3D material (SHOT-47).
            _material.SetShaderParameter("rim_colour", new Vector4(rim.R, rim.G, rim.B, rim.A));
            _rim = rim;
        }

        _material.SetShaderParameter("view_centre", viewCentre);
        _material.SetShaderParameter("units", metresPerRef);
        _material.SetShaderParameter("radius_ref", radiusRef);
        _material.SetShaderParameter("half_ref", radiusRef + PadRef);
        Visible = true;
    }
}
