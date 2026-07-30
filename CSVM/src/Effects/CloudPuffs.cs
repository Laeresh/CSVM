using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The original's ambient cloud puffs: a field of very soft, translucent cloud sprites
/// (the cloud1/cloud2 blob textures) that the plane flies through at altitude — the wisps
/// drifting past as you approach and pass through the CLOUD_COVER band
/// (see <c>OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png</c>).
///
/// <para>Unlike the crash fireball (a data-defined <see cref="PufferState"/> from
/// flame_ball.json), no zrdr reader defines these — the mission weather.json carries only
/// the CLOUD_COVER band and the WIND. So this is a hand-tuned ambient field parameterized
/// by the data we do have: the cloud band altitude gates where the puffs live, and the
/// weather WIND drives their slow drift. Every feel constant is marked TUNE, validated by
/// user playtest against the reference screenshot.</para>
///
/// <para>Model: a fixed pool of world-anchored sprites kept in a cylindrical shell around
/// the camera. The layer's altitude is world-fixed to the cloud band (you fly up into it
/// and then out over it, like a real layer) while the field follows the plane in X/Z. Each
/// frame the puffs drift slowly with the wind, but the plane's own motion is what carries
/// them past (parallax against world-fixed sprites). A puff the plane flies past horizontally
/// is recycled to the leading edge, so the field is endless. Alpha fades to 0 at the shell
/// edge (recycled puffs fade in, they never pop) and by vertical distance from the camera,
/// so low-altitude flight is clear and climbing above the layer leaves wisps scattered below.</para>
///
/// <para>Drawn as ONE <see cref="MultiMeshInstance3D"/> (one draw call): a spatial shader
/// billboards each quad toward the camera, applies a per-instance roll for variety, picks
/// cloud1/cloud2 from per-instance custom data, and alpha-blends — soft translucent clouds,
/// not the additive glow the fire <see cref="Puffer"/> uses.</para>
/// </summary>
public sealed partial class CloudPuffs : Node3D
{
    // --- TUNE: the ambient field's feel, calibrated against the reference screenshot ---
    private const int Count = 12;              // sprites in the pool — sparse, so they read as separate wisps not a solid ring
    private const float Radius = 620f;         // horizontal shell radius (m); alpha 0 beyond it
    private const float FadeInFrac = 0.5f;     // outer fraction of the radius over which alpha ramps 0→1
    private const float SizeMin = 90f, SizeMax = 200f;             // sprite side (m)
    private const float BaseAlphaMin = 0.06f, BaseAlphaMax = 0.13f; // per-sprite opacity (× the fairly-opaque texture) — the original's wisps are very faint veils
    private const float DriftScale = 0.15f;    // fraction of WIND applied (the plane's motion dominates; wind is a subtle drift)
    private const float RandomDrift = 1.5f;    // ± per-sprite horizontal drift (m/s) so the field isn't rigid
    // The puff layer is anchored to the cloud band altitude (world-fixed Y), not the camera —
    // you fly up into it and then out over it, as with a real cloud layer. So the layer stays
    // put in altitude while the field follows the plane in X/Z. Margins relative to the band, m.
    private const float BandBelow = 120f;      // layer extends this far below BOTTOM …
    private const float BandAbove = 280f;      // … and this far above TOP (a scattered layer with depth)
    // Vertical alpha fade: a puff at the camera's altitude is full; it fades to nothing once
    // the camera is far above/below it, so low-altitude flight is clear and climbing above the
    // layer leaves scattered wisps below.
    private const float VertFull = 200f;       // full alpha within this |Δaltitude| of the camera …
    private const float VertFade = 560f;       // … fading to 0 by here

    private struct Puff
    {
        public Vector3 Pos;     // world position
        public Vector3 Vel;     // drift velocity, m/s (already includes the scaled wind)
        public float Size;      // side length, m
        public float BaseAlpha; // per-sprite opacity
        public float Frame;     // 0 = cloud1, 1 = cloud2
        public float Roll;      // billboard roll, rad
    }

    private Puff[] _puffs = Array.Empty<Puff>();
    private MultiMesh _mm = null!;
    private int _frameCount;
    private float _layerLo, _layerHi;  // the puff layer's world-Y bounds (band ± the margins)
    private Vector3 _wind;
    private bool _seeded;
    // Puff placement, size and frame choice. One stream per field (one per splitscreen rig), drawn
    // off the master seed's cloud stream.
    private readonly System.Random _rng = Rng.NewSystemRandom(Rng.Clouds);

    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        uniform sampler2D atlas : source_color, filter_linear;
        uniform float frame_count = 2.0;

        // Same global distance-fog params as SceneBuilder's world shader (registered by
        // GameSession; no-op ranges when not flying). Puffs near the fog shell fade into the
        // fog wall like the terrain below them, replacing the old fog immunity.
        global uniform vec3 csky_fog_color;
        global uniform vec2 csky_fog_range;
        global uniform vec2 csky_fog_alt;

        varying flat float v_frame;
        varying flat float v_alpha;

        void vertex() {
            // Per-instance roll (variety), applied in the quad's local plane before billboarding.
            float ca = cos(INSTANCE_CUSTOM.z);
            float sa = sin(INSTANCE_CUSTOM.z);
            VERTEX.xy = mat2(vec2(ca, sa), vec2(-sa, ca)) * VERTEX.xy;
            // Billboard the quad toward the camera, keeping its per-instance scale
            // (Godot's billboard_keep_scale, by hand — this is a MultiMesh).
            MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
                INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
            MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz);
            MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz);
            MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
            v_frame = INSTANCE_CUSTOM.x;
            v_alpha = INSTANCE_CUSTOM.y;
        }

        void fragment() {
            float col = floor(v_frame + 0.5);
            vec2 uv = vec2((UV.x + col) / frame_count, UV.y);
            vec4 t = texture(atlas, uv);
            // Cylindrical distance fog, identical to the world shader: a puff near the shell
            // edge fades into the fog wall like the terrain below it. VERTEX is view-space in
            // fragment; INV_VIEW_MATRIX lifts it to world for the distance + altitude fade.
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            float fog_amt = smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, CAMERA_POSITION_WORLD.xz))
                * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));
            ALBEDO = mix(t.rgb, csky_fog_color, fog_amt);
            ALPHA = t.a * v_alpha;
        }
        """;

    /// <summary>Builds the ambient field from the cloud1/cloud2 sprite textures. Null if
    /// neither texture is in the archive. <paramref name="wind"/> is the weather WIND static
    /// velocity; <paramref name="bandBottom"/>/<paramref name="bandTop"/> the CLOUD_COVER band
    /// (metres) that gates the field's altitude.</summary>
    public static CloudPuffs? Create(TextureArchive textures, Vector3 wind, float bandBottom, float bandTop)
    {
        var atlas = BuildAtlas(textures, out int frames);
        if (atlas == null)
            return null;
        var puffs = new CloudPuffs();
        puffs.Init(atlas, frames, wind, bandBottom, bandTop);
        return puffs;
    }

    private void Init(ImageTexture atlas, int frames, Vector3 wind, float bandBottom, float bandTop)
    {
        Name = "cloud_puffs";
        _frameCount = frames;
        _wind = wind * DriftScale;
        _layerLo = bandBottom - BandBelow;
        _layerHi = bandTop + BandAbove;
        _puffs = new Puff[Count];

        var mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        mat.SetShaderParameter("atlas", atlas);
        mat.SetShaderParameter("frame_count", (float)frames);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = Count,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Instances span the whole shell around the (moving) camera, far from this node's
            // origin — pad the culling box so the field is never wrongly frustum-culled.
            ExtraCullMargin = Radius,
        });
    }

    /// <summary>Advances the field one frame. <paramref name="cameraPos"/> is the flight
    /// camera position; <paramref name="cameraForward"/> its look direction (recycled puffs
    /// favour the leading hemisphere so the plane flies into fresh clouds). Safe to call every
    /// frame regardless of altitude — the layer self-gates via the vertical fade (clear well
    /// below/above it).</summary>
    public void Update(float dt, Vector3 cameraPos, Vector3 cameraForward)
    {
        if (!_seeded)
        {
            // Seed once the camera is within reach of the layer; below that there's nothing to show.
            float layerMid = (_layerLo + _layerHi) * 0.5f, layerHalf = (_layerHi - _layerLo) * 0.5f;
            if (Mathf.Abs(cameraPos.Y - layerMid) > layerHalf + VertFade)
            {
                _mm.VisibleInstanceCount = 0;
                return;
            }
            for (int i = 0; i < _puffs.Length; i++)
                _puffs[i] = SpawnPuff(cameraPos, cameraForward, initial: true);
            _seeded = true;
        }

        float fadeIn = Radius * FadeInFrac;
        for (int i = 0; i < _puffs.Length; i++)
        {
            ref var p = ref _puffs[i];
            p.Pos += p.Vel * dt;

            float dx = p.Pos.X - cameraPos.X, dz = p.Pos.Z - cameraPos.Z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            // Recycle a puff the plane has flown past horizontally (the layer's altitude is
            // world-fixed, so vertical distance only fades the puff, never recycles it — that
            // way climbing above the layer leaves it below rather than churning).
            if (dist > Radius)
            {
                p = SpawnPuff(cameraPos, cameraForward, initial: false);
                dx = p.Pos.X - cameraPos.X;
                dz = p.Pos.Z - cameraPos.Z;
                dist = Mathf.Sqrt(dx * dx + dz * dz);
            }

            // Horizontal fade: 0 at the shell edge → full inside the fade-in shell.
            float distFade = dist >= Radius ? 0f
                : dist <= Radius - fadeIn ? 1f
                : (Radius - dist) / fadeIn;
            // Vertical fade: full within VertFull of the camera altitude → 0 by VertFade.
            float vy = Mathf.Abs(p.Pos.Y - cameraPos.Y);
            float vertFade = vy <= VertFull ? 1f
                : vy >= VertFade ? 0f
                : 1f - (vy - VertFull) / (VertFade - VertFull);
            float alpha = p.BaseAlpha * distFade * vertFade;

            _mm.SetInstanceTransform(i,
                new Transform3D(Basis.Identity.Scaled(new Vector3(p.Size, p.Size, p.Size)), p.Pos));
            _mm.SetInstanceCustomData(i, new Color(p.Frame, alpha, p.Roll, 0f));
        }
        _mm.VisibleInstanceCount = _puffs.Length;
    }

    // A puff placed around the camera in X/Z, at a world-fixed altitude in the band layer.
    // Initial seeding fills the whole disc (uniform by area); recycled puffs spawn at the
    // fade-in edge (so they fade in, never pop) and favour the leading hemisphere ahead.
    private Puff SpawnPuff(Vector3 cameraPos, Vector3 cameraForward, bool initial)
    {
        float fwdAngle = Mathf.Atan2(cameraForward.X, cameraForward.Z);
        float az = initial ? Rand(-Mathf.Pi, Mathf.Pi) : fwdAngle + Rand(-2.1f, 2.1f); // ±120° of forward
        float dist = initial
            ? Mathf.Sqrt(Rand(0f, 1f)) * Radius             // uniform over the disc area
            : Rand(Radius * (1f - FadeInFrac), Radius);     // recycle at the fade-in edge
        return new Puff
        {
            Pos = new Vector3(
                cameraPos.X + Mathf.Sin(az) * dist,
                Rand(_layerLo, _layerHi),                   // world-fixed altitude within the layer
                cameraPos.Z + Mathf.Cos(az) * dist),
            Vel = new Vector3(Rand(-RandomDrift, RandomDrift), 0f, Rand(-RandomDrift, RandomDrift)) + _wind,
            Size = Rand(SizeMin, SizeMax),
            BaseAlpha = Rand(BaseAlphaMin, BaseAlphaMax),
            Frame = _rng.Next(_frameCount),
            Roll = Rand(0f, Mathf.Tau),
        };
    }

    private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    // A horizontal 2-frame atlas (cloud1 | cloud2), alpha kept (soft translucent blobs).
    private static ImageTexture? BuildAtlas(TextureArchive textures, out int frameCount)
    {
        var frames = new List<Image>();
        int fw = 0, fh = 0;
        foreach (var name in new[] { "cloud1", "cloud2" })
        {
            var tex = textures.Find(name);
            if (tex == null)
                continue;
            var img = tex.GetImage();
            img.Convert(Image.Format.Rgba8);
            frames.Add(img);
            fw = Mathf.Max(fw, img.GetWidth());
            fh = Mathf.Max(fh, img.GetHeight());
        }
        frameCount = frames.Count;
        if (frames.Count == 0)
            return null;
        var atlas = Image.CreateEmpty(fw * frames.Count, fh, false, Image.Format.Rgba8);
        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            if (f.GetWidth() != fw || f.GetHeight() != fh)
                f.Resize(fw, fh);
            atlas.BlitRect(f, new Rect2I(0, 0, fw, fh), new Vector2I(i * fw, 0));
        }
        return ImageTexture.CreateFromImage(atlas);
    }
}
