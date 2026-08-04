using System;
using CSVM.Flight;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// Rain and snow, driven by the mission weather.json's precipitation block (see
/// <see cref="WeatherState.PrecipData"/>): C4 Rocky-Mountains SNOW, C1C/C2B RAIN. The data
/// gives the type, tint (COLOR), fall rate (GRAVITY), drift (WIND_DIR/WIND_VEL), density
/// (PARTICLES, rain only), and peak opacity (ALPHA_GRADIENT); every mapping from those data
/// units to a metres/second look is a marked <c>TUNE</c> constant, validated by the user's
/// A/B against the original.
///
/// <para><b>Model — a camera-following field the plane flies through.</b> One
/// <see cref="MultiMeshInstance3D"/> (one draw call) of N small quads. Each instance carries a
/// fixed random seed; a spatial shader turns that seed into a world position that (a) falls +
/// drifts over the <c>csky_time</c> global (see <see cref="CSVM.Utils.ShaderTime"/>) and (b)
/// wraps into a box <b>centred on the camera</b> — so the field is world-anchored (the plane's
/// own motion carries the particles past, correct parallax) yet infinite and cheap. The whole
/// animation runs from that uniform and the <c>CAMERA_POSITION_WORLD</c> built-in, so there is
/// <b>zero per-frame CPU</b>: build it once and it plays itself — and because the uniform is the
/// only handle, halting or fixed-stepping the sim clock is what stops or pins the fall. A
/// generous custom AABB keeps it from being frustum-culled when the camera is far from this
/// node's origin (the shader positions everything around the camera, so the computed instance
/// transforms stay near the origin).</para>
///
/// <para><b>SNOW</b> = small camera-facing flakes with a gentle per-instance horizontal
/// flutter (so they don't fall in lockstep). <b>RAIN</b> = thin streak quads billboarded
/// <i>along the fall direction</i> (the data's GRAVITY+WIND velocity), length scaled by fall
/// speed. (Streaking along the plane-relative velocity — rain coming at the camera when you
/// fly fast — is a nicer look but not what the data specifies; it's a documented TUNE
/// follow-up pending the user's in-game A/B.) The sprites are <b>procedural</b> (a soft dot /
/// a soft streak, generated here): the original rendered untextured coloured line/point
/// primitives, which no texture archive carries, so a tiny generated sprite is the faithful,
/// asset-free stand-in.</para>
///
/// <para>No distance fog: unlike the cloud sprites (which reach kilometres out to the fog
/// wall), the precipitation box is a tight ~40 m shell around the camera, entirely inside the
/// fog's clear near-range (FOG_RANGES.x / 2 ≈ 250–500 m), so a fog term would be a provable
/// no-op here.</para>
/// </summary>
public sealed partial class Precipitation : Node3D
{
    // --- TUNE: the feel, pending the user's A/B against the original ---
    private const float RainBoxHalf = 30f;   // camera-centred wrap-box half-extent (m), rain …
    private const float SnowBoxHalf = 30f;   // … and snow
    private const int PoolPerParticle = 40;  // MultiMesh instances per data PARTICLE (density)
    private const int DefaultParticles = 100; // SNOW carries no PARTICLES → use this (= rain's)
    private const int MinCount = 800, MaxCount = 5000;
    private const float FallScale = 5f;      // fall speed (m/s) = GRAVITY × this (rain 3→15, snow 1→5)
    private const float WindScale = 1f;      // horizontal drift (m/s) = WIND_VEL × this
    private const float FlakeSize = 0.32f;   // snow quad side (m; ×0.7–1.3 per-instance in-shader)
    private const float StreakWidth = 0.05f; // rain streak width (m)
    private const float StreakSeconds = 0.09f; // rain streak length = fall speed × this …
    private const float MinStreak = 0.8f;    // … but at least this (m)
    private const float OpacityTune = 1f;    // extra master opacity on top of ALPHA_GRADIENT.x
    private const float NearFade = 3f;       // fade particles within this many m of the camera …
    private const float FadeStart = 0.6f;    // … and out beyond this fraction of the box radius
    private const float SnowSwayAmp = 0.6f;  // snow horizontal flutter amplitude (m)
    private const float SnowSwayFreq = 1.3f; // … and frequency (rad/s)

    // Self-animating: fall/wind drift over csky_time, wrapped into a box centred on the camera.
    // The final world position ignores the instance transform entirely (POSITION is set
    // directly), so the instances can stay at the node origin — only the custom AABB below keeps
    // them visible. INSTANCE_CUSTOM.xyz = the fixed base fraction [0,1)³; .w = a flutter phase.
    //
    // ⚠ csky_time (the sim clock's shader-side twin), never Godot's TIME: this module has no
    // per-frame C# hook at all, so the uniform is the ONLY handle on the animation — with TIME
    // the rain kept falling through a halted clock and through a fixed-step capture.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        #include "res://shaders/csky_time.gdshaderinc"

        uniform sampler2D sprite : filter_linear, repeat_disable;
        uniform vec3 tint = vec3(0.5);      // already sRGB→linear (set on the CPU, like the fog colour)
        uniform vec3 box_half = vec3(40.0);
        uniform vec3 fall_vel = vec3(0.0, -10.0, 0.0);  // world fall + wind (m/s)
        uniform float peak_alpha = 0.5;
        uniform float near_fade = 3.0;      // fade particles within this many m of the camera
        uniform float fade_start = 0.6;
        uniform vec2 cloud_band = vec2(1e8, 1e9); // [base, top] altitude: precip only BELOW the cloud cover
        uniform bool is_rain = false;
        uniform float flake_size = 0.4;
        uniform vec2 streak_size = vec2(0.05, 1.0);     // (width, length) m
        uniform float sway_amp = 0.6;
        uniform float sway_freq = 1.3;

        varying float v_alpha;

        void vertex() {
            vec3 seed = INSTANCE_CUSTOM.xyz;   // fixed per-instance, [0,1)³
            float phase = INSTANCE_CUSTOM.w;
            vec3 cell = box_half * 2.0;
            vec3 world = seed * cell + fall_vel * csky_time;
            vec3 rel = world - CAMERA_POSITION_WORLD;
            rel = mod(rel + box_half, cell) - box_half;   // wrap into [-box_half, box_half]
            if (!is_rain) {                                // snow flutter (break the lockstep fall)
                rel.x += sin(csky_time * sway_freq + phase) * sway_amp;
                rel.z += cos(csky_time * sway_freq * 0.9 + phase) * sway_amp;
            }
            vec3 center = CAMERA_POSITION_WORLD + rel;

            // Alpha: fade in over the first near_fade metres (so a particle sitting on the
            // camera doesn't blow up into a blob — this is a chase cam, not a cockpit), and
            // fade out past fade_start of the box radius so the box edge has no hard boundary.
            float md = length(rel);              // metres from the camera
            float dn = length(rel / box_half);   // normalized (1 ≈ box face)
            v_alpha = peak_alpha
                * smoothstep(0.0, near_fade, md)
                * (1.0 - smoothstep(fade_start, 1.0, dn))
                // Precip falls FROM the cloud base: full below it, gone above the overcast.
                * (1.0 - smoothstep(cloud_band.x, cloud_band.y, center.y));

            vec3 offset;
            if (is_rain) {
                // Axial billboard: long axis = fall direction, thin axis faces the camera.
                vec3 up = normalize(fall_vel);
                vec3 to_cam = normalize(CAMERA_POSITION_WORLD - center);
                vec3 right = cross(up, to_cam);
                float rl = length(right);
                right = rl > 1e-4 ? right / rl : INV_VIEW_MATRIX[0].xyz;
                offset = right * VERTEX.x * streak_size.x + up * VERTEX.y * streak_size.y;
            } else {
                // Camera billboard flake, per-instance size variety from the seed.
                float sz = flake_size * (0.7 + 0.6 * seed.z);
                offset = (INV_VIEW_MATRIX[0].xyz * VERTEX.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y) * sz;
            }
            POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(center + offset, 1.0));
        }

        void fragment() {
            ALBEDO = tint;
            ALPHA = texture(sprite, UV).a * v_alpha;
        }
        """;

    private MultiMesh _mm = null!;

    /// <summary>Builds the field for a mission's precipitation, or null if there is none.
    /// <paramref name="cloudBottom"/>/<paramref name="cloudTop"/> are the <c>CLOUD_COVER</c>
    /// band (metres): the precipitation only shows *below* it (it falls from the cloud base —
    /// none above the overcast). Pass an empty band (top ≤ bottom) to disable that gating.
    /// Self-animating once added to the tree — no per-frame driving needed.</summary>
    public static Precipitation? Create(WeatherState.PrecipData? precip, float cloudBottom, float cloudTop)
    {
        if (precip is not { } p)
            return null;
        var field = new Precipitation();
        field.Init(p, cloudBottom, cloudTop);
        return field;
    }

    // A soft round dot (white RGB, radial-falloff alpha): the snow flake.
    private static ImageTexture MakeFlakeTexture()
    {
        const int n = 16;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 centre → 1 at the edge
                float a = 1f - Mathf.SmoothStep(0.15f, 0.95f, r);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        return ImageTexture.CreateFromImage(img);
    }

    // A soft vertical streak (thin bright line, fading at both ends): the rain drop. Symmetric
    // top/bottom so the axial billboard's orientation doesn't matter.
    private static ImageTexture MakeStreakTexture()
    {
        const int w = 8, h = 32;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = ((x + 0.5f) / w - 0.5f) / 0.22f;      // thin gaussian across the width
                float across = Mathf.Exp(-dx * dx);
                float along = Mathf.Sin(Mathf.Pi * (y + 0.5f) / h); // fade to 0 at both ends
                img.SetPixel(x, y, new Color(1f, 1f, 1f, across * along));
            }
        return ImageTexture.CreateFromImage(img);
    }

    private void Init(WeatherState.PrecipData p, float cloudBottom, float cloudTop)
    {
        Name = "precipitation";
        bool rain = p.Kind == WeatherState.PrecipKind.Rain;
        float boxHalf = rain ? RainBoxHalf : SnowBoxHalf;

        int particles = p.Particles > 0 ? p.Particles : DefaultParticles;
        int count = Mathf.Clamp(particles * PoolPerParticle, MinCount, MaxCount);

        float fallSpeed = p.Gravity * FallScale;
        float windRad = Mathf.DegToRad(p.WindDir);
        var fallVel = new Vector3(
            Mathf.Sin(windRad) * p.WindVel * WindScale,
            -fallSpeed,
            Mathf.Cos(windRad) * p.WindVel * WindScale);
        float streakLen = Mathf.Max(fallSpeed * StreakSeconds, MinStreak);
        // COLOR is a DX7 sRGB framebuffer value (like FOG_COLOR); convert to linear so the
        // unshaded ALBEDO renders back at the data's byte value in the sRGB framebuffer.
        var tint = p.Color.SrgbToLinear();
        float peakAlpha = p.AlphaGradient.X * OpacityTune;

        var mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        mat.SetShaderParameter("sprite", rain ? MakeStreakTexture() : MakeFlakeTexture());
        mat.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        mat.SetShaderParameter("box_half", new Vector3(boxHalf, boxHalf, boxHalf));
        mat.SetShaderParameter("fall_vel", fallVel);
        mat.SetShaderParameter("peak_alpha", peakAlpha);
        mat.SetShaderParameter("near_fade", NearFade);
        mat.SetShaderParameter("fade_start", FadeStart);
        // Only rain/snow below the cloud cover; fade through the band (its interior is
        // whited-out anyway), gone above the overcast. No band → a huge sentinel = no gating.
        var cloudBand = cloudTop > cloudBottom ? new Vector2(cloudBottom, cloudTop) : new Vector2(1e8f, 1e9f);
        mat.SetShaderParameter("cloud_band", cloudBand);
        mat.SetShaderParameter("is_rain", rain);
        mat.SetShaderParameter("flake_size", FlakeSize);
        mat.SetShaderParameter("streak_size", new Vector2(StreakWidth, streakLen));
        mat.SetShaderParameter("sway_amp", SnowSwayAmp);
        mat.SetShaderParameter("sway_freq", SnowSwayFreq);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = count,
        };
        // The per-instance seeds baked into the MultiMesh custom data: the whole field's layout is
        // this one draw sequence, so pinning it is what makes a rain/snow shot reproducible.
        var rng = Rng.NewSystemRandom(Rng.Precip);
        for (int i = 0; i < count; i++)
        {
            _mm.SetInstanceTransform(i, Transform3D.Identity); // position comes from the shader
            _mm.SetInstanceCustomData(i, new Color(
                (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(),
                (float)(rng.NextDouble() * Math.Tau)));
        }

        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The shader repositions every instance around the (moving) camera, far from this
            // node's origin where the actual instance transforms sit — so give it a world-sized
            // custom AABB to never be frustum-culled. One draw call, always drawn: correct here.
            CustomAabb = new Aabb(new Vector3(-40000f, -40000f, -40000f), new Vector3(80000f, 80000f, 80000f)),
        });
        GD.Print($"precipitation: {p.Kind.ToString().ToLowerInvariant()} — {count} particles, " +
                 $"fall {fallSpeed:0.0} m/s, tint {p.Color.R * 255:0}/{p.Color.G * 255:0}/{p.Color.B * 255:0}, " +
                 $"alpha {peakAlpha:0.00}");
    }
}
