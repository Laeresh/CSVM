using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The GPU half of a <see cref="Puffer"/>: live particles in, draw calls out. The seam exists so
/// the emitter's three modes — burst, distance trail and sustain — are reachable by a test, which
/// through a real <see cref="MultiMesh"/> they are not.
///
/// <para>The atlas is built <b>above</b> this seam (`Puffer.Create` calls `BuildAtlas` and hands the
/// result to the renderer's constructor), which is what makes a <c>Puffer</c> constructible with no
/// <c>TextureArchive</c> anywhere in the path. A renderer below the atlas would have delivered
/// nothing testable.</para>
///
/// <para>The contract per frame is: <see cref="Write"/> for indices <c>0 … liveCount-1</c>, packed
/// and ascending, then one <see cref="Show"/> publishing how many of them are live. Slots past
/// <c>liveCount</c> keep whatever they last held and are simply not drawn, exactly as the
/// <c>MultiMesh</c> does.</para>
/// </summary>
public interface IEmitterRenderer
{
    /// <summary>Sizes the draw pool at <paramref name="capacity"/> particles and attaches whatever
    /// draws under <paramref name="owner"/>. Called once, from the emitter's own init.
    /// <paramref name="cullMargin"/> pads the frustum test for billboarding, which moves vertices
    /// off the pool's computed AABB.</summary>
    void Attach(Node3D owner, int capacity, float cullMargin);

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
///
/// <para>⚠ The blend is already resolved by the time it gets here — `Puffer.Create` derives it from
/// the COLORS ramp and the dying sprite's luminance (effects.md). This type takes the verdict, not
/// the question: there is no <c>Auto</c> to re-derive, because it holds no state to derive it
/// from.</para>
/// </summary>
public sealed class MultiMeshEmitterRenderer : IEmitterRenderer
{
    private const string ShaderCode = """
        shader_type spatial;
        render_mode BLEND_MODE, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

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
            // soft particles: a billboard tilted by a high chase camera dips into the
            // terrain and the depth test cuts it with a hard straight line — fade
            // alpha out over the last ~1.5 m before the scene depth instead. Disabled
            // (SOFT_EXPR → 1.0) for the crash smokeball: it sits just above the ground,
            // so the fade zeroes the alpha of every fresh puff against the terrain right
            // behind it — a bright additive fire still leaks through, but MIX black smoke
            // faded to zero is simply invisible until it grows tall.
            float scene_raw = texture(depth_texture, SCREEN_UV).r;
            vec4 unproj = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene_raw, 1.0);
            float scene_z = unproj.z / unproj.w;
            float soft = SOFT_EXPR;
            ALBEDO = t.rgb * v_color.rgb;
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
        // PLAN-perf-hitches C9: the ShaderMaterial/Shader built here is the runtime one — a new
        // Puffer's first draw, never a load-time material. Usually reached from
        // EmitterDirector's EffectPoolMiss scope, which suppresses this one (flat leaves); it
        // fires on its own only when a Puffer is built with nothing else already open.
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
