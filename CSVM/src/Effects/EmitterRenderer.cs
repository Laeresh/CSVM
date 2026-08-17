using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>The GPU half of a <see cref="Puffer"/>: live particles in, draw calls out. The seam
/// exists so the emitter's three modes are reachable by a test, which a real <see cref="MultiMesh"/>
/// is not. The atlas is built above this seam, in <c>Puffer.Create</c>, and passed to the renderer's
/// constructor, so a <c>Puffer</c> needs no <c>TextureArchive</c> below it.
/// Per frame: <see cref="Write"/> for indices <c>0 … liveCount-1</c>, packed and ascending, then one
/// <see cref="Show"/>. Slots past <c>liveCount</c> keep their last value and simply aren't
/// drawn.</summary>
public interface IEmitterRenderer
{
    /// <summary>Sizes the draw pool at <paramref name="capacity"/> particles and attaches whatever
    /// draws under <paramref name="owner"/>. Called once, from the emitter's own init.
    /// <paramref name="cullMargin"/> pads the frustum test for billboarding, which moves vertices
    /// off the pool's computed AABB.</summary>
    void Attach(Node3D owner, int capacity, float cullMargin);

    /// <summary>Re-sizes the draw pool to <paramref name="capacity"/> slots, keeping nothing: the
    /// emitter rewrites every live slot before the next <see cref="Show"/>. A continuous emitter
    /// calls this when its authored emission outgrows the pool it was sized with.</summary>
    void Grow(int capacity);

    /// <summary>One live particle's state this frame: world <paramref name="position"/>, uniform
    /// <paramref name="size"/>, atlas column <paramref name="frame"/>, <paramref name="alpha"/>
    /// envelope and ramp <paramref name="color"/>.</summary>
    void Write(int index, Vector3 position, float size, float frame, float alpha, Color color);

    /// <summary>Publishes the frame: the first <paramref name="liveCount"/> written slots draw.</summary>
    void Show(int liveCount);
}

/// <summary>
/// The real renderer: ONE <see cref="MultiMesh"/> of billboarded quads (one draw call), whose
/// shared shader billboards each instance toward the camera, picks its flipbook column from
/// per-instance custom data, and composites additively or mixed.
/// ⚠ The blend is already resolved by the time it gets here — <c>Puffer.Create</c> derives it from
/// the COLORS ramp and the dying sprite's luminance (effects.md). This type takes the verdict, not
/// the question: there is no <c>Auto</c> to re-derive, because it holds no state to derive it from.
/// </summary>
public sealed class MultiMeshEmitterRenderer : IEmitterRenderer
{
    private const string ShaderCode = """
        shader_type spatial;
        render_mode BLEND_MODE, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
        #include "res://shaders/csky_srgb.gdshaderinc"

        uniform sampler2D atlas : source_color, filter_linear, repeat_disable;
        uniform float frame_count = 1.0;
        uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;

        varying flat float v_frame;
        varying flat float v_alpha;
        varying flat vec4 v_color;

        void vertex() {
            // Billboard the instance quad toward the camera, keeping its per-instance scale
            // (Godot's billboard_keep_scale, done by hand because this is a MultiMesh).
            MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
                INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
            MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz);
            MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz);
            MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
            v_frame = INSTANCE_CUSTOM.x;
            v_alpha = INSTANCE_CUSTOM.y;
            v_color = COLOR;
        }

        void fragment() {
            float col = floor(v_frame + 0.5);
            vec2 uv = vec2((UV.x + col) / frame_count, UV.y);
            vec4 t = texture(atlas, uv);
            // fade to nothing at the quad rim: some source frames leak bright pixels
            // to their border (fire_f01 edge maxes at 247), which otherwise paints
            // faint additive rectangles over dark ground on large grown quads
            vec2 rim = smoothstep(vec2(0.0), vec2(0.12), UV)
                     * smoothstep(vec2(0.0), vec2(0.12), vec2(1.0) - UV);
            // Soft particles: fade alpha over the last ~1.5 m before the scene depth, so a tilted
            // billboard doesn't hard-cut into the terrain. Disabled for the smokeball, which sits on
            // the ground and would otherwise fade every fresh puff to invisible.
            float scene_raw = texture(depth_texture, SCREEN_UV).r;
            vec4 unproj = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene_raw, 1.0);
            float scene_z = unproj.z / unproj.w;
            float soft = SOFT_EXPR;
            // The COLORS ramp is authored in DX7 framebuffer bytes, the same gamma-space
            // modulate every fullbright pass linearises (csky_srgb.gdshaderinc); multiplied in
            // raw it draws two shades too pale. White, the ramp-less case, is a fixed point.
            ALBEDO = t.rgb * csky_srgb_to_linear(v_color.rgb);
            ALPHA = t.a * v_alpha * v_color.a * rim.x * rim.y * soft;
        }
        """;

    private readonly ImageTexture _atlas;
    private readonly int _frameCount;
    private readonly bool _blendMix;
    private readonly bool _softParticles;

    private MultiMeshInstance3D _mmi = null!;
    private MultiMesh _mm = null!;

    /// <summary>Builds the renderer for an already-packed <paramref name="atlas"/> of
    /// <paramref name="frameCount"/> side-by-side frames. <paramref name="blendMix"/> alpha-blends
    /// instead of adding (a COLORS ramp or a near-black dying sprite), and
    /// <paramref name="softParticles"/> enables the depth fade.</summary>
    public MultiMeshEmitterRenderer(ImageTexture atlas, int frameCount, bool blendMix, bool softParticles)
    {
        _atlas = atlas;
        _frameCount = frameCount;
        _blendMix = blendMix;
        _softParticles = softParticles;
    }

    public void Attach(Node3D owner, int capacity, float cullMargin)
    {
        // Runtime shader/material build, not load-time — a new Puffer's first draw. Usually
        // absorbed into EmitterDirector's EffectPoolMiss scope; fires alone only when nothing else
        // is already open.
        using var _ = PerfSample.Scope(PerfSite.MaterialCreate);
        var code = ShaderCode
            .Replace("BLEND_MODE", _blendMix ? "blend_mix" : "blend_add")
            .Replace("SOFT_EXPR", _softParticles ? "clamp((VERTEX.z - scene_z) / 1.5, 0.0, 1.0)" : "1.0");
        var mat = new ShaderMaterial { Shader = new Shader { Code = code } };
        mat.SetShaderParameter("atlas", _atlas);
        mat.SetShaderParameter("frame_count", (float)_frameCount);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = capacity,
            VisibleInstanceCount = 0,
        };
        _mmi = new MultiMeshInstance3D
        {
            Multimesh = _mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // billboarding moves verts off the MultiMesh's computed AABB — pad culling; the
            // margin covers the largest tuned size any spawn path could produce
            ExtraCullMargin = cullMargin,
        };
        owner.AddChild(_mmi);
    }

    public void Grow(int capacity)
    {
        // Setting InstanceCount reallocates the buffer and clears every slot; the emitter's next
        // frame writes all its live ones back before Show, so nothing drawn is lost for longer.
        if (_mm != null && capacity > _mm.InstanceCount)
        {
            _mm.VisibleInstanceCount = 0;
            _mm.InstanceCount = capacity;
        }
    }

    public void Write(int index, Vector3 position, float size, float frame, float alpha, Color color)
    {
        _mm.SetInstanceTransform(index,
            new Transform3D(Basis.Identity.Scaled(new Vector3(size, size, size)), position));
        _mm.SetInstanceCustomData(index, new Color(frame, alpha, 0f, 0f));
        _mm.SetInstanceColor(index, color);
    }

    public void Show(int liveCount)
    {
        if (_mm != null)
            _mm.VisibleInstanceCount = liveCount;
    }
}
