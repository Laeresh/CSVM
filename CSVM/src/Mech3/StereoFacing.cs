namespace CSVM.Mech3;

/// <summary>
/// How a camera-facing sprite takes its basis when the process draws through the stereo path.
/// Flat, a sprite may copy the camera's basis, as the original's screen-rectangle puffer sprites and
/// the flat moon's billboard mode do. Under a headset that basis is the head's, so the quad lies in
/// the head's view plane. It pivots and is seen edge-on as the head turns away, and it rolls with
/// every tilt. A stereo sprite takes the world-up facade look-at toward the eye instead
/// (<c>shaders/csky_facade.gdshaderinc</c>), the original's rule for a SphericalY facade. Entry:
/// docs/architecture/Mech3.md.
/// </summary>
public static class StereoFacing
{
    /// <summary>The include every stereo pose below needs.</summary>
    public const string Include = SceneBuilder.FacadeInclude;

    /// <summary>Gets a value indicating whether this process draws through the stereo path. Set
    /// once by the session boot, before any world is built, so every sprite shader chooses its
    /// text once.</summary>
    public static bool Live { get; private set; }

    /// <summary>The <c>vertex()</c> lines that pose a quad centred on its instance origin in the
    /// facade basis toward the eye. Each axis keeps the instance's own scale (Godot's
    /// <c>billboard_keep_scale</c>). Declares <c>face</c> for any line after it. Indented by
    /// <paramref name="indent"/>, no trailing newline.</summary>
    public static string PoseLines(string indent) =>
        $"{indent}mat3 face = csky_facade_spherical(MODEL_MATRIX[3].xyz, CAMERA_POSITION_WORLD);\n"
        + $"{indent}MODELVIEW_MATRIX = VIEW_MATRIX * mat4(\n"
        + $"{indent}    vec4(face[0], 0.0), vec4(face[1], 0.0), vec4(face[2], 0.0), MODEL_MATRIX[3]);\n"
        + $"{indent}MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz);\n"
        + $"{indent}MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz);\n"
        + $"{indent}MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);";

    /// <summary>Marks the process as drawing through the stereo path.</summary>
    public static void Declare() => Live = true;

    /// <summary>An unshaded textured sprite in the stereo pose. It stands in for an unshaded alpha
    /// <c>StandardMaterial3D</c> in Godot's billboard mode with keep-scale. It takes
    /// <c>albedo_tex</c> times the vertex colour.</summary>
    public static string SpriteShaderCode() => $$"""
        shader_type spatial;
        render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
        {{Include}}

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;

        void vertex() {
        {{PoseLines("    ")}}
        }

        void fragment() {
            vec4 tex = texture(albedo_tex, UV) * COLOR;
            ALBEDO = tex.rgb;
            ALPHA = tex.a;
        }
        """;
}
