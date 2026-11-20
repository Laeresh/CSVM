using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// Volumetric clouds in place of the cloud deck, the fogvol field and the placed cloud sprites,
/// under Enhanced alone (<see cref="VolumetricCloudsSetting"/>). The slab is one full-screen raymarch through
/// the mission's cloud band, over the cloud shadows' own pattern. It marches at a fraction of the view's
/// resolution (<see cref="VolumetricCloudsSetting.MarchDivisor"/>). It draws cumulus heads over an
/// overcast deck torn by rifts, with light shafts under them. It reads the depth buffer so terrain
/// and aircraft occlude it, publishes its ground shadow to the terrain, and the faithful path
/// builds none. It lives beside <see cref="WeatherRig"/>, whose zone, night and deck it holds.
/// </summary>
internal sealed partial class VolumetricClouds : Node3D
{
    // The authored band is the whiteout's thin sheet. The cumulus layer stands this much taller
    // over the same floor, so the heads have room to tower.
    private const float LayerDepthGain = 3f;
    private const float LayerMinDepthM = 900f;
    private const float DeckCover = 0.92f;
    // The share of a deck torn open and the tears' size.
    private const float DeckGaps = 0.35f;
    private const float GapM = 3200f;
    // The deck's depth over the layer floor, the shader's deck_depth_m.
    private const float DeckDepthM = 150f;
    // The heads' edge band, billow and lean (the shader's head_soft, warp_m, shear_m), and erosion cell.
    private const float HeadSoft = 0.16f;
    private const float WarpM = 320f;
    private const float ShearM = 260f;
    private const float DetailM = 180f;
    // The shafts' haze under rain or snow, and in a dry zone.
    private const float RainShaftGain = 1f;
    private const float HazeShaftGain = 0.3f;
    // How far above the highest placed cloud an authored band may sit and still count as the layer.
    private const float SpriteBandMarginM = 1000f;

    // The look's presets, each a set of the march's uniforms, the last six those the ground shadow's
    // globals read back (PublishGround). A clear day lights the heads hard from the sun.
    // A storm dims the sun, greys the ambient and lets fewer, taller heads stand, clear air between them.
    // Every look marches without dither. The search strides by the gap to the nearest surface.
    // The lit steps start at the bisected surface, capped at 8 m plus 1 % of the distance.
    private const string BasePreset = "shape_m=1500;detail_tile_m=250;phase_g=0.45;powder_k=0.6;amb_occ=0.6;base_rag=0.08;deck_scale=0.6;deck_erode=0.5;cover_stride=1;od_step=0.3;bisect_n=7;search_gap_m=400;retreat_k=1.2;dither_k=0;step_budget=1500;lit_max_m=8;lit_growth=0.01;search_pix_k=0.006;cover_gap_m=800;base_erode=1;cover_max=1;pw_density=0.04;deck_k=0.6;edge_cut=0;edge_k=1";
    // Soft heads take a 10 m least step and larger optical-depth steps.
    // The lit step's 8 m cap stays under 16 m, where the heads pulse.
    private const string ClearPreset = BasePreset + ";head_soft=0.5;amb_gain=0.6;amb_floor=0.3;light_k=1.5;sun_gain=2.4;cloud_min_m=10;pix_k=0.005;od_step=0.5;lit_growth=0.02";
    // An overcast deck under the heads: a lighter base, and the heads broken up over it.
    private const string DeckPreset = ClearPreset + ";amb_floor=0.45;cover_max=0.7;amb_gain=0.7";
    // The storm's clumps end in a surface, which pulses under the soft heads' coarser steps, so its lit steps stay fine.
    // Its dense clumps shade little with depth, so the sun march re-runs per 3 % of opacity.
    // Soft heads ring under that. Its thin sharp beams need 48 shaft steps, where soft shadows need 16.
    // Its steps grow slower with distance, since coarse ones stripe the tall faces seen edge-on.
    private const string StormPreset = BasePreset + ";head_soft=0.5;heads_gain=0.45;cover_max=0.95;pw_density=0.08;edge_cut=0.2;edge_k=5;sun_gain=0.5;light_k=2.5;ms_b=0.75;amb_floor=0.7;amb_fog_mix=0.2;amb_top_rgb=0.42,0.45,0.5;amb_gain=1.3;step_growth=0.035;light_every_a=0.03;shaft_steps=48";
    // A storm's heads tower this far over the layer floor.
    private const float StormDepthM = 2450f;

    // The shafts' target divisor at full resolution: the haze is soft, the cloud edges are not.
    private const int ShaftDivisor = 2;

    // The bubble's reach past the hull's bounding radius: room to fly an attack in clear air.
    private const float BubbleMarginM = 400f;

    private static readonly StringName PointLightsParam = "point_lights";
    private static readonly StringName MarchOnParam = "march_on";
    private static readonly StringName MarchShaftsOnlyParam = "march_shafts_only";
    private static readonly StringName ShaftsOnlyParam = "shafts_only";
    private static readonly StringName MarchEyeParam = "march_eye";
    private static readonly StringName MarchBackParam = "march_back";
    private static readonly StringName MarchLensParam = "march_lens";
    private static readonly StringName ViewCameraParam = "view_camera";
    private static readonly StringName ViewInvProjectionParam = "view_inv_projection";

    private static readonly string[] ResolveOptions = { "accum", "accum_spatial", "clip_sigma", "depth_tol" };

    // The temporal march's 2x2 order, diagonal first so two frames cover both diagonals.
    private static readonly Vector2I[] Phases = { new(0, 0), new(1, 1), new(1, 0), new(0, 1) };

    // The cloud lab's live edits, applied again after every rebuild.
    private static readonly Dictionary<string, float> _labValues = new();

    // Full and half trace a quarter of their pixels a frame, resolved over four frames (StepTemporal).
    // The control channel's temporal=0 marches every pixel, the reference its measurements score against.
    private static bool _temporalOn = true;

    // The slab in the tree, for the control channel's and the cloud lab's live uniform writes.
    private static VolumetricClouds? _live;

    private readonly Node3D _worldRoot;
    private readonly WeatherRig _weather;
    private readonly FogVolumeClutter? _field;
    private readonly SceneBuilder? _scene;
    private readonly List<(MeshInstance3D Node, Aabb World)> _placedSprites = new();
    private readonly List<MeshInstance3D> _hidden = new();
    // The look's float uniforms as the slab stands: the preset, the chapter's saved look, then live edits.
    private readonly Dictionary<string, float> _lookValues = new();
    // The same values before the cloud lab's edits: the preset under the chapter's saved look.
    private readonly Dictionary<string, float> _savedValues = new();
    // The preset's own values, before the chapter's saved look.
    private readonly Dictionary<string, float> _presetValues = new();
    private readonly ZeppelinRuntime? _zeppelins;
    // Each bubble's host, a zeppelin or a named world structure, its centre in the host's frame and its radius.
    private readonly List<(Node3D Host, Vector3 Centre, float Radius)> _hulls = new();
    private readonly bool _hasDeck;
    // The temporal resolve's two targets, each the other's history.
    private readonly ShaderMaterial?[] _resolve = new ShaderMaterial?[2];
    private readonly SubViewport?[] _resolveViews = new SubViewport?[2];
    private ShaderMaterial? _material;
    // The reduced march (MarchShader), its target, the target's render time and its size, zero while off.
    private ShaderMaterial? _march;
    private SubViewport? _marchView;
    private MeasuredRenderTime? _marchTime;
    private Vector2I _marchSize;
    private bool _marchShaftsOnly;
    private MeasuredRenderTime? _resolveTime;
    private bool _temporal;
    private bool _historyOk;
    private int _temporalFrame;
    private int _ditherFrame;
    private Projection _prevViewProjection;
    private Vector3 _prevEye;
    private float? _bottom;
    private float? _top;
    // The layer was fitted to the placed clouds, so the upper zone is no air above it to split the fog by.
    private bool _fitted;
    private float _appliedTaper = -1f;
    private float _appliedShaft = -1f;
    private Node3D? _built;
    private bool _hiding;
    // The layer's top before a storm raises it.
    private float? _baseTop;
    // The layer's top as the chapter's saved look leaves it, and as the preset alone does.
    private float? _savedTop;
    private float? _presetTop;

    private VolumetricClouds(Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, bool hasDeck, ZeppelinRuntime? zeppelins)
    {
        Name = "VolumetricClouds";
        _hasDeck = hasDeck;
        _zeppelins = zeppelins;
        _worldRoot = worldRoot;
        _weather = weather;
        _field = field;
        _scene = scene;
        (_bottom, _top) = LayerOf(weather.Mission, volumes);
    }

    /// <summary>How much of the look a value read through <see cref="LookValue"/> includes.</summary>
    public enum LookStage
    {
        /// <summary>As the slab stands, the cloud lab's live edits included.</summary>
        Live,

        /// <summary>The preset under the chapter's saved look.</summary>
        Saved,

        /// <summary>The weather's preset alone.</summary>
        Preset,
    }

    /// <summary>Gets the <c>csky_pc_ground</c> global as last written, zero with no clouds drawing. A suite
    /// reads it here, since reading a global back is an engine error outside the editor.</summary>
    public static Vector4 PublishedGround { get; private set; }


    /// <summary>Gets the reduced march's render time while it runs, else null. Its target is a viewport
    /// of its own, which <c>gpu_ms</c> leaves out (verification PERF-39).</summary>
    public static MeasuredRenderTime? MarchTime { get; private set; }

    /// <summary>Gets one temporal resolve target's render time while the temporal march runs, else null.
    /// The two targets alternate, so one's latest time is a frame's resolve.</summary>
    public static MeasuredRenderTime? ResolveTime { get; private set; }

    /// <summary>Gets the cloud lab's live edits as the control channel's <c>cloud</c> verb takes them.</summary>
    public static string LabSetText => string.Join(";", System.Linq.Enumerable.Select(_labValues, kv => Pair(kv.Key, kv.Value)));

    /// <summary>Gets or sets a value indicating whether the bubbles of clear air stand.
    /// The control channel's <c>bubbles</c> switch, for measuring the march without them.</summary>
    public static bool BubblesOn { get; set; } = true;

    /// <summary>Gets how many hosts the clouds took to leave a bubble round. A hidden one is skipped
    /// each frame, and past the four bubble globals (<see cref="CloudShadow.BubbleParams"/>) the rest are.</summary>
    public int HullCount => _hulls.Count;

    /// <summary>Gets the march and resolve shaders' float uniform defaults, read from the shader text so no
    /// C# copy drifts from it.</summary>
    internal static IReadOnlyDictionary<string, float> UniformDefaults { get; } = ParseDefaults(MarchCode() + ResolveShader());

    // The chapter's cloud tune, the cloud lab's live copy: the heads' taper and the deck's shares.
    private static CloudShadowPattern.Tune Tune => CloudShadow.Live?.Tune.Clamped() ?? CloudShadowPattern.Tune.Default;

    // The heads' shape the slab and its ground shadow share: taper, edge band, billow, lean.
    private static Vector4 HeadShape => new(Tune.HeadTaper, HeadSoft, WarpM, ShearM);

    // The shafts' haze: a mission authoring rain or snow scatters the sun hard, a dry zone faintly.
    // The zone tune's god rays scale it.
    private float ShaftGain => (_weather.Mission?.Precip != null ? RainShaftGain : HazeShaftGain) * _weather.GodRays;

    private float CoverBias => _hasDeck ? -0.3f : 0.15f;

    private bool Storm => _weather.Mission?.Precip != null;

    // The heads' gain under the look: a deck chapter's stand fuller over the overcast.
    private float BaseHeadsGain => _hasDeck ? 1.15f : 1f;

    /// <summary>The clouds over <paramref name="worldRoot"/>'s world. They hide the deck through
    /// <paramref name="weather"/>, the cloud <paramref name="field"/> and the sprites <paramref name="scene"/>
    /// placed. They draw the slab over the band or <paramref name="volumes"/>, and
    /// <paramref name="hasDeck"/> draws the overcast deck under the heads. The <paramref name="zeppelins"/>'
    /// hulls stay clear of cloud.</summary>
    public static VolumetricClouds Create(Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, bool hasDeck, ZeppelinRuntime? zeppelins) =>
        new(worldRoot, weather, field, scene, volumes, hasDeck, zeppelins);

    /// <summary>The layer's altitudes: the mission's cloud band where it authors one, else the fvol
    /// volumes' vertical union, else none.</summary>
    public static (float? Bottom, float? Top) LayerOf(WeatherState? mission, IReadOnlyList<FogVolumeBox> volumes)
    {
        if (mission is { HasCloudBand: true })
            return (mission.CloudBottom, Thickened(mission.CloudBottom, mission.CloudTop));
        if (volumes.Count == 0)
            return (null, null);
        float bottom = float.MaxValue, top = float.MinValue;
        foreach (var v in volumes)
        {
            bottom = Mathf.Min(bottom, v.Box.Position.Y);
            top = Mathf.Max(top, v.Box.End.Y);
        }
        return (bottom, Thickened(bottom, top));
    }

    /// <summary>The cumulus layer's top over the authored floor <paramref name="bottom"/>: the authored
    /// depth to <paramref name="top"/> times <see cref="LayerDepthGain"/>, at least <see cref="LayerMinDepthM"/>.</summary>
    public static float Thickened(float bottom, float top) =>
        bottom + Mathf.Max((top - bottom) * LayerDepthGain, LayerMinDepthM);

    /// <summary>The control channel's <c>cloud</c> verb. Writes <paramref name="set"/>
    /// ("name=v;name=a,b,c") onto the live slab's materials; <c>temporal=0|1</c> swaps the march.</summary>
    public static string ControlSet(string set)
    {
        if (_live?._material == null)
            return "err no slab";
        _live.ApplySet(set, _live._material, _live._march, _live._resolve[0], _live._resolve[1]);
        return "ok";
    }

    /// <summary>The look option <paramref name="name"/> at <paramref name="stage"/>. Null with no slab.
    /// <c>depth_m</c> is the layer's depth.</summary>
    public static float? LookValue(string name, LookStage stage = LookStage.Live)
    {
        if (_live?._material is not { } material || _live._bottom is not { } floor)
            return null;
        if (name == "depth_m")
            return (stage == LookStage.Live ? _live._top : stage == LookStage.Saved ? _live._savedTop : _live._presetTop) - floor;
        return (stage == LookStage.Live ? _live._lookValues : stage == LookStage.Saved ? _live._savedValues : _live._presetValues).TryGetValue(name, out float v)
            ? v
            : RenderingServer.ShaderGetParameterDefault(material.Shader.GetRid(), name).AsSingle();
    }

    /// <summary>The cloud lab sets one look option live. The edit outlasts a rebuild.</summary>
    public static void LabSet(string name, float value)
    {
        _labValues[name] = value;
        ControlSet(Pair(name, value));
    }

    /// <summary>Drops the cloud lab's edits, so the slab stands as the chapter's saved look builds it.</summary>
    public static void LabClear()
    {
        _labValues.Clear();
        if (_live?._built != null)
            _live.Rebuild();
    }

    public override void _EnterTree()
    {
        _live = this;
        CollectPlacedSprites();
        FitLayerToSprites();
        CollectHulls();
        Follow(GraphicsMode.Enhanced);
    }

    public override void _ExitTree() => Follow(false);

    public override void _Process(double delta)
    {
        PublishGround(true);
        FollowKnobs();
    }

    /// <summary>The graphics switch: Enhanced draws the slab and hides the originals, the faithful
    /// path puts every original back and draws nothing.</summary>
    public void Follow(bool enhanced)
    {
        if (enhanced)
        {
            HideOriginals();
            Rebuild();
        }
        else
        {
            FreeBuilt();
            Restore();
        }
        PublishGround(enhanced);
        SetProcess(enhanced);
    }

    private static string Pair(string name, float value) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name}={value:R}");

    // Every `uniform float name = value;` in `code`, hints and all.
    private static Dictionary<string, float> ParseDefaults(string code)
    {
        var defaults = new Dictionary<string, float>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(code, @"uniform float (\w+)\s*(?::[^=;]*)?=\s*(-?[0-9.]+)\s*;"))
            defaults[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        return defaults;
    }

    // One full-screen quad, the cloud shadows' pattern as the layer's coverage. Where the reduced
    // march runs for this view, the quad reads its target and marches only the pixels it cannot serve.
    private static string SlabShader() => """
        shader_type spatial;
        render_mode blend_premul_alpha, unshaded, depth_draw_never, shadows_disabled, fog_disabled, cull_disabled, depth_test_disabled;

        """ + MarchCode() + """

        uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
        // The reduced march's target (MarchShader) and the camera it was traced from. A view drawn
        // from any other camera, a pane, a spyglass or a second eye, marches every pixel itself.
        uniform sampler2D march_texture : filter_linear, repeat_disable;
        uniform bool march_on = false;
        // The target holds the shafts alone (a divisor of 1): the quad marches the layer per pixel.
        uniform bool march_shafts_only = false;
        uniform vec3 march_eye;
        uniform vec3 march_back;
        uniform vec2 march_lens;
        // At full with the temporal march, the layer's trace comes from the resolve (ResolveShader).
        uniform bool march_temporal = false;
        uniform sampler2D resolved_texture : filter_linear, repeat_disable;

        // The world's point lights glowing in the clouds (#368): bursts, fires, tracers and rockets. It
        // runs here per pixel, so the temporal resolve never holds a moving light in its history.
        #include "res://shaders/csky_lights.gdshaderinc"
        uniform float point_gain = 0.35;
        // A light reaches this many times its far range inside a cloud, which scatters it wide.
        uniform float point_reach = 3.0;
        // A burst's light, far range over `burst_far_m`, reaches this many times instead. Tripled, one
        // burst lit too much of the cloud. Rounds, fires and placed lights stay under 50 m.
        uniform float burst_reach = 2.0;
        uniform float burst_far_m = 60.0;
        // The head of the ranked light list that glows, 0 with Cloud Glow off. Each light costs a veil
        // and a chord march per pixel, so all sixteen tripled the GPU time while firing in cloud.
        uniform int point_lights = 4;

        // The point lights' glow along the ray from `t0` to `t1`, premultiplied. Each light's chord
        // takes four steps of the cloud, lit by its falloff and dimmed by the cloud before them.
        vec3 pc_point_glow(vec3 eye, vec3 dir, float t0, float t1) {
            vec3 sum = vec3(0.0);
            for (int i = 0; i < min(csky_light_count, point_lights); i++) {
                vec4 pr = texelFetch(csky_light_data, ivec2(0, i), 0);
                vec4 cr = texelFetch(csky_light_data, ivec2(1, i), 0);
                float r = pr.w * (pr.w > burst_far_m ? burst_reach : point_reach);
                vec3 oc = pr.xyz - eye;
                float tc = dot(oc, dir);
                float d2 = max(dot(oc, oc) - tc * tc, 0.0);
                if (d2 >= r * r) {
                    continue;
                }
                float half_chord = sqrt(r * r - d2);
                float a = max(tc - half_chord, t0);
                float b = min(tc + half_chord, t1);
                if (b <= a) {
                    continue;
                }
                // Two steps of the same density dim the light by the cloud before its chord. The
                // shared veil took fifteen per light, the larger share of the glow's cost.
                float trans = 1.0;
                float vt = (a - t0) * 0.5;
                for (int k = 0; k < 2 && vt > 0.0; k++) {
                    vec3 q = eye + dir * (t0 + vt * (float(k) + 0.5));
                    trans *= exp(-pc_density(q, layer_bottom, layer_top) * density_gain * vt);
                }
                float dt = (b - a) / 4.0;
                float acc = 0.0;
                for (int k = 0; k < 4; k++) {
                    vec3 q = eye + dir * (a + dt * (float(k) + 0.5));
                    float sigma = pc_density(q, layer_bottom, layer_top) * density_gain;
                    if (sigma <= 0.0) {
                        continue;
                    }
                    float w = clamp(1.0 - distance(q, pr.xyz) / r, 0.0, 1.0);
                    float s = 1.0 - exp(-sigma * dt);
                    acc += trans * s * w * w;
                    trans *= 1.0 - s;
                }
                sum += cr.rgb * acc;
            }
            return sum * point_gain;
        }

        // Whether this view is the camera the reduced march traced. TAA's jitter leaves the lens
        // diagonal alone, and the eye test fails for a headset's eyes and every other camera.
        bool pc_reduced(vec3 eye, mat4 inv_view, mat4 inv_projection) {
            if (!march_on || distance(eye, march_eye) > 0.01 || dot(inv_view[2].xyz, march_back) < 0.99999) {
                return false;
            }
            vec2 lens = abs(vec2(inv_projection[0][0], inv_projection[1][1]));
            return all(lessThan(abs(lens - march_lens), march_lens * 0.001));
        }

        // Fogs the march's result at the entry point and writes it premultiplied. The light shafts in
        // front of it add on top, and `behind`, premultiplied over its alpha, shows under it.
        void pc_write(vec4 marched, vec3 entry, vec3 cam, vec3 shafts, vec4 behind, inout vec3 albedo, inout float alpha) {
            float a = 1.0 - marched.a;
            alpha = a + (1.0 - a) * behind.a;
            if (alpha <= 0.002 && max(shafts.r, max(shafts.g, shafts.b)) <= 0.0005) {
                discard;
            }
            float fog = max(csky_fog_amount(entry, cam), smoothstep(haze_near_m, far_m, distance(entry, cam))) * cloud_fog;
            albedo = mix(marched.rgb, csky_fog_color_at(cam) * a, fog) + shafts + (1.0 - a) * behind.rgb;
        }

        // The shafts' light gathered over share `f` of their ray, between the quarters pc_shaft_light keeps.
        float pc_shaft_at(vec4 quarters, float f) {
            float u = clamp(f, 0.0, 1.0) * 4.0;
            int k = min(int(u), 3);
            vec4 before = vec4(0.0, quarters.xyz);
            return mix(before[k], quarters[k], u - float(k));
        }

        // Writes the pixel from its trace (pc_trace): the cloud lit and fogged, then the layer past
        // the cap. The shafts and the sky or fog behind follow.
        void pc_resolve(vec3 eye, vec3 dir, vec2 span, float scene, vec4 trace, inout vec3 albedo, inout float alpha) {
            vec4 m = vec4(pc_colour(trace.xy, eye), trace.z);
            if (point_lights > 0 && csky_light_count > 0 && span.y >= 0.0) {
                m.rgb += pc_point_glow(eye, dir, span.x, min(span.y, scene));
            }
            float t0 = 0.0;
            if (span.y >= 0.0) {
                t0 = span.x;
                // A march cut off inside the layer leaves the rest of it to the fog, not the dome, by
                // the layer left unmarched. A ray leaving just past the cap takes little.
                float t1 = pc_march_end(span, scene);
                float unmarched = min(span.y, scene) - t1;
                if (unmarched > 1.0) {
                    float rest = max(csky_fog_amount(eye + dir * t1, eye), smoothstep(haze_near_m, far_m, t1))
                        * (1.0 - exp(-unmarched / 5000.0));
                    m.rgb += m.a * rest * csky_fog_color_at(eye);
                    m.a *= 1.0 - rest;
                }
            }
            vec3 shafts = pc_shaft_colour(trace.w, eye, dir);
            vec4 behind = vec4(0.0);
            // At or below the horizon, a sky pixel past the clipped sea shows the fog, not the dome's lower band.
            if (scene >= 1e8 && span.y < 0.0 && dir.y <= 0.02) {
                behind = vec4(csky_fog_color_at(eye), 1.0);
            }
            pc_write(m, eye + dir * t0, eye, shafts, behind, albedo, alpha);
        }

        // The shafts' light at this pixel's depth from the target's right half, at column `x` of the
        // left half (the shafts' quarters, pc_shaft_at).
        float pc_reduced_shafts(vec2 uv, float x, vec2 span, float scene) {
            float l0 = min(span.y >= 0.0 ? span.x : shaft_far_m, shaft_far_m);
            if (l0 <= 1.0 || shaft_gain <= 0.0) {
                return 0.0;
            }
            // Neighbouring texels start their steps at different dithers. Four half-texel taps average
            // them, which TAA cannot: it rejects a thin beam's lone bright sample.
            vec2 px = 1.0 / vec2(textureSize(march_texture, 0));
            vec2 c = vec2(0.5, 0.0) + clamp(vec2(x, uv.y) * shaft_scale, px * 2.0, vec2(0.5, 1.0) * shaft_scale - px * 2.0);
            vec4 quarters = 0.25 * (texture(march_texture, c + px * vec2(0.5, 0.5)) + texture(march_texture, c + px * vec2(-0.5, 0.5))
                + texture(march_texture, c + px * vec2(0.5, -0.5)) + texture(march_texture, c + px * vec2(-0.5, -0.5)));
            return pc_shaft_at(quarters, min(scene, l0) / l0);
        }

        void vertex() {
            POSITION = vec4(VERTEX.xy, 1.0, 1.0);
        }

        void fragment() {
            vec3 eye = CAMERA_POSITION_WORLD;
            vec3 dir = csky_pc_ray_dir(SCREEN_UV, INV_PROJECTION_MATRIX, INV_VIEW_MATRIX);
            float scene = csky_pc_scene_distance(texture(depth_texture, SCREEN_UV).r, SCREEN_UV, INV_PROJECTION_MATRIX);
            // Past the full fog nothing is seen, so the dome, which writes depth, reads as open sky.
            if (scene > csky_fog_range.y) {
                scene = 1e9;
            }
            vec2 span = csky_pc_slab_span(eye, dir, layer_bottom, layer_top);
            vec4 trace;
            // The reduced march ignored the scene, so it serves every pixel whose scene stands outside
            // the marched span. Terrain or an aircraft inside the layer marches here at full resolution.
            bool inside = span.y >= 0.0 && scene > span.x && scene < min(span.y, span.x + far_m);
            bool reduced = pc_reduced(eye, INV_VIEW_MATRIX, INV_PROJECTION_MATRIX);
            // The target's left half holds the layer's trace and its right half the shafts'
            // quarters. The clamp keeps the filter off the seam between them.
            float edge = 1.0 / float(max(textureSize(march_texture, 0).x, 1));
            float x = clamp(SCREEN_UV.x, edge, 1.0 - edge) * 0.5;
            // The march's start offset moves by the golden ratio every frame. TAA then averages the
            // pattern out, where a fixed one leaves a hatch wherever the steps are coarse.
            float shaft_dither = fract(csky_pc_dither(FRAGCOORD.xy) + frame_offset);
            float dither = shaft_dither * dither_k;
            if (reduced && !march_shafts_only && !inside) {
                trace = vec4((march_temporal ? texture(resolved_texture, SCREEN_UV) : texture(march_texture, vec2(x, SCREEN_UV.y))).xyz, 0.0);
                // In front of the layer the scene hides it, so the edge stays at this pixel's own depth.
                if (span.y < 0.0 || scene <= span.x) {
                    trace.xyz = vec3(0.0, 0.0, 1.0);
                }
                trace.w = pc_reduced_shafts(SCREEN_UV, x, span, scene);
            } else if (reduced && march_shafts_only) {
                // The shafts are soft haze, so the target serves them; the cloud edges stay per pixel.
                trace = vec4(pc_layer(eye, dir, span, scene, dither), pc_reduced_shafts(SCREEN_UV, x, span, scene));
            } else {
                trace = pc_trace(eye, dir, span, scene, dither, shaft_dither);
            }
            vec3 albedo = vec3(0.0);
            float alpha = 0.0;
            pc_resolve(eye, dir, span, scene, trace, albedo, alpha);
            ALBEDO = albedo;
            ALPHA = alpha;
        }
        """;

    // The reduced march: a canvas over two targets side by side, each a half or a quarter of the view's
    // size. Each texel traces the main camera's ray with the scene ignored, and keeps the trace raw.
    private static string MarchShader() => """
        shader_type canvas_item;
        render_mode blend_disabled, unshaded;

        """ + MarchCode() + """

        // The main view's camera and inverse projection, written before each draw (FollowCamera).
        uniform mat4 view_camera;
        uniform mat4 view_inv_projection;
        // The quad marches the layer itself (a divisor of 1), so the left half stays clear.
        uniform bool shafts_only = false;
        // The temporal march's offset to this frame's pixel of each 2x2 block, in UV (ResolveShader).
        uniform vec2 jitter = vec2(0.0);
        // This frame's pixel of each 2x2 block, or negative without the temporal march. The dither
        // then varies per full-size pixel, as the quad's does, not in a 2x2 weave TAA cannot average.
        uniform vec2 dither_phase = vec2(-1.0);

        void fragment() {
            vec3 eye = view_camera[3].xyz;
            // The left half traces the layer and the right half the shafts, each over the whole view.
            // The engine's projection has its y up and the canvas its rows down.
            // The shafts' half stays put: the quad filters it at fixed texel centres.
            vec2 cuv = UV.x < 0.5 ? vec2(UV.x * 2.0, UV.y) + jitter : vec2(UV.x * 2.0 - 1.0, UV.y) / shaft_scale;
            vec2 uv = vec2(cuv.x, 1.0 - cuv.y);
            vec3 dir = csky_pc_ray_dir(uv, view_inv_projection, view_camera);
            vec2 span = csky_pc_slab_span(eye, dir, layer_bottom, layer_top);
            vec2 dither_px = dither_phase.x >= 0.0 ? floor(FRAGCOORD.xy) * 2.0 + dither_phase + 0.5 : FRAGCOORD.xy;
            float dither = fract(csky_pc_dither(dither_px) + frame_offset) * dither_k;
            // The shafts' half is not jittered, and the noise read at every second pixel is a slow ramp.
            float shaft_dither = fract(csky_pc_dither(FRAGCOORD.xy) + frame_offset);
            if (UV.x < 0.5) {
                // The alpha keeps the first cloud's distance, where the resolve reprojects from.
                float hit = 20000.0;
                vec3 layer = shafts_only ? vec3(0.0, 0.0, 1.0) : pc_layer_hit(eye, dir, span, 1e9, dither, hit);
                COLOR = vec4(layer, min(hit, 60000.0));
            } else if (max(cuv.x, cuv.y) > 1.0) {
                COLOR = vec4(0.0);
            } else {
                COLOR = pc_shafts(eye, dir, span, 1e9, shaft_dither);
            }
        }
        """;

    // The temporal march's resolve, a full-size canvas. The half-size march traced
    // one pixel of each 2x2 block this frame, which it keeps. The other three reproject the previous
    // resolve to this frame. A history at another distance than the fresh traces around the pixel is
    // dropped. The rest is clamped to those traces, so a disocclusion cannot ghost.
    private static string ResolveShader() => """
        shader_type canvas_item;
        render_mode blend_disabled, unshaded;

        #include "res://shaders/csky_volumetric_clouds.gdshaderinc"

        uniform sampler2D fresh : filter_nearest, repeat_disable;
        uniform sampler2D history : filter_linear, repeat_disable;
        uniform mat4 view_camera;
        uniform mat4 view_inv_projection;
        uniform mat4 prev_view_projection;
        uniform vec3 prev_eye;
        // The history's distance may leave the span of the traces around the pixel by this share.
        uniform float depth_tol = 0.25;
        uniform ivec2 phase;
        uniform bool history_ok = false;
        // The history's share in the pixel traced this frame, and in the three the neighbours'
        // traces only estimate. Every pixel takes some of this frame, so none drifts from the rest.
        uniform float accum = 0.75;
        uniform float accum_spatial = 0.9;
        // The history may stray this many standard deviations from the traces around the pixel.
        uniform float clip_sigma = 1.25;

        vec4 pc_fresh(ivec2 c, ivec2 cells) {
            return texelFetch(fresh, clamp(c, ivec2(0), cells - 1), 0);
        }

        // Catmull-Rom over five bilinear taps: a bilinear read would soften the history every frame.
        vec3 pc_history(vec2 uv) {
            vec2 size = vec2(textureSize(history, 0));
            vec2 pos = uv * size;
            vec2 tc1 = floor(pos - 0.5) + 0.5;
            vec2 f = pos - tc1;
            vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
            vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
            vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
            vec2 w3 = f * f * (-0.5 + 0.5 * f);
            vec2 w12 = w1 + w2;
            vec2 tc0 = (tc1 - 1.0) / size;
            vec2 tc3 = (tc1 + 2.0) / size;
            vec2 tc12 = (tc1 + w2 / w12) / size;
            float a = w12.x * w0.y;
            float b = w0.x * w12.y;
            float c = w12.x * w12.y;
            float d = w3.x * w12.y;
            float e = w12.x * w3.y;
            vec3 sum = texture(history, vec2(tc12.x, tc0.y)).rgb * a + texture(history, vec2(tc0.x, tc12.y)).rgb * b
                + texture(history, tc12).rgb * c + texture(history, vec2(tc3.x, tc12.y)).rgb * d
                + texture(history, vec2(tc12.x, tc3.y)).rgb * e;
            return max(sum / (a + b + c + d + e), vec3(0.0));
        }

        void fragment() {
            ivec2 px = ivec2(FRAGCOORD.xy);
            // The fresh target's left half holds the layer's trace, its alpha the first cloud's distance.
            ivec2 cells = ivec2(textureSize(fresh, 0).x / 2, textureSize(fresh, 0).y);
            vec4 f = pc_fresh(px / 2, cells);
            bool traced = all(equal(px - (px / 2) * 2, phase));
            // This frame's estimate: the trace itself, else the traces around it, bilinear in their
            // own grid, where each sits at 2 * cell + phase.
            vec2 q = (vec2(px) - vec2(phase)) * 0.5;
            ivec2 c0 = ivec2(floor(q));
            vec2 t = q - floor(q);
            vec4 s00 = pc_fresh(c0, cells);
            vec4 s10 = pc_fresh(c0 + ivec2(1, 0), cells);
            vec4 s01 = pc_fresh(c0 + ivec2(0, 1), cells);
            vec4 s11 = pc_fresh(c0 + ivec2(1, 1), cells);
            vec3 cur = traced ? f.rgb : mix(mix(s00.rgb, s10.rgb, t.x), mix(s01.rgb, s11.rgb, t.x), t.y);
            float hit_near = min(min(s00.a, s10.a), min(s01.a, s11.a));
            float hit_far = max(max(s00.a, s10.a), max(s01.a, s11.a));
            float hit = traced ? f.a : hit_near;
            COLOR = vec4(cur, hit);
            if (history_ok) {
                vec3 eye = view_camera[3].xyz;
                vec3 dir = csky_pc_ray_dir(vec2(UV.x, 1.0 - UV.y), view_inv_projection, view_camera);
                vec4 clip = prev_view_projection * vec4(eye + dir * hit, 1.0);
                vec2 prev = clip.xy / max(clip.w, 1e-6) * 0.5 + 0.5;
                prev.y = 1.0 - prev.y;
                // The history's own distance, against the nearest and farthest trace around the pixel as the
                // previous eye saw them. A pixel beside a silhouette holds either side's, so both pass.
                ivec2 held_size = textureSize(history, 0);
                float held = texelFetch(history, clamp(ivec2(prev * vec2(held_size)), ivec2(0), held_size - 1), 0).a;
                bool agrees = held >= distance(eye + dir * hit_near, prev_eye) * (1.0 - depth_tol)
                    && held <= distance(eye + dir * hit_far, prev_eye) * (1.0 + depth_tol);
                if (agrees && clip.w > 0.0 && all(greaterThanEqual(prev, vec2(0.0))) && all(lessThanEqual(prev, vec2(1.0)))) {
                    // The traces' spread around the pixel bounds its history, so a disocclusion cannot ghost.
                    vec3 m1 = vec3(0.0);
                    vec3 m2 = vec3(0.0);
                    vec3 lo = vec3(1e9);
                    vec3 hi = vec3(-1e9);
                    ivec2 centre = ivec2(round(q));
                    for (int y = -1; y <= 1; y++) {
                        for (int x = -1; x <= 1; x++) {
                            vec3 n = pc_fresh(centre + ivec2(x, y), cells).rgb;
                            m1 += n;
                            m2 += n * n;
                            lo = min(lo, n);
                            hi = max(hi, n);
                        }
                    }
                    m1 /= 9.0;
                    vec3 sigma = sqrt(max(m2 / 9.0 - m1 * m1, vec3(0.0)));
                    vec3 h = clamp(pc_history(prev), max(lo, m1 - sigma * clip_sigma), min(hi, m1 + sigma * clip_sigma));
                    COLOR = vec4(mix(cur, h, traced ? accum : accum_spatial), hit);
                }
            }
        }
        """;

    // The march both passes share: the includes, the layer's uniforms and the march's functions.
    // Its pc_trace returns what a pixel needs before the scene and the fog.
    private static string MarchCode() => """
        #define CSKY_PC_LAT_EXACT
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_volumetric_clouds.gdshaderinc"
        #include "res://shaders/csky_terrain_shadow.gdshaderinc"

        uniform float layer_bottom = 970.0;
        uniform float layer_top = 1130.0;
        uniform float cover_bias = 0.15;
        uniform float detail_m = 180.0;
        uniform float density_gain = 0.025;
        uniform int steps = 40;
        // The step near the eye: this many metres plus this share of the distance (pc_step). The
        // budget caps a ray's steps.
        uniform float step_near_m = 30.0;
        uniform float step_growth = 0.05;
        // A finer ramp at the eye, under the step above for the first 200 m or so. Coarse steps
        // there alias a near wall into streaks fanning from the view's centre.
        uniform float step_fine_m = 8.0;
        uniform float step_fine_growth = 0.15;
        // 1 jitters the lit march's start past the surface. Only the per-pixel march averages that
        // jitter; a reduced texel flickers and the temporal resolve pulses.
        uniform float hit_dither = 0.0;
        // Above 0 the lit march steps by optical depth from the surface, this much per step.
        // A step is no finer than cloud_min_m or pix_k of the distance, and no coarser than the clear-air step.
        // At 0 the plain march runs: fixed steps, every sample lit. The control channel's reference.
        uniform float od_step = 0.0;
        uniform float cloud_min_m = 6.0;
        uniform float pix_k = 0.003;
        uniform int bisect_n = 4;
        // The first sample's depth inside the eroded surface, as a share of the least step.
        uniform float first_k = 0.5;
        uniform float retreat_k = 2.0;
        uniform float lit_max_m = 1e9;
        // The heads' edge: density under edge_cut is cut away and the rest steepened by edge_k.
        // A cloud then ends in a surface, not a wide haze.
        uniform float edge_cut = 0.0;
        uniform float edge_k = 1.0;
        uniform float lit_growth = 0.0;
        // Above 0 the clear-air search inside a coverage steps this many metres per unit of gap
        // (pc_heads_kept). It shortens toward a surface, so it cannot step over a thin cloud.
        uniform float search_gap_m = 0.0;
        // Outside every coverage, the stride in metres per unit the pattern stands under its edge.
        uniform float cover_gap_m = 1e6;
        uniform float search_pix_k = 0.003;
        // The sun march re-runs once the samples since its last run hold this share of the pixel's opacity.
        uniform float light_every_a = 0.0;
        uniform int step_budget = 128;
        uniform vec3 sun_rgb = vec3(0.8, 0.77, 0.72);
        uniform float far_m = 24000.0;
        // Where a cloud starts hazing into the fog colour on its way to far_m, whatever the zone fog.
        uniform float haze_near_m = 8000.0;
        // The stratus deck's share of the sky, 0 in a chapter without a deck, and its depth over the
        // layer floor. The heads' coverage gain is lower over a deck.
        uniform float deck_cover = 0.0;
        uniform float deck_depth_m = 150.0;
        uniform float heads_gain = 1.0;
        // Rifts in the deck, the share opened and their size. Under it the light shafts: the sun
        // scattered by the haze or rain below the layer, where the rifts let it through.
        uniform float deck_gaps = 0.0;
        uniform float gap_m = 3200.0;
        // The haze's scattering per 10 km, about 1 under rain.
        uniform float shaft_gain = 0.0;
        uniform int shaft_steps = 16;
        // The share of their half of the march target the shafts fill, from its corner (FollowCamera).
        // Soft haze needs fewer texels than the clouds, and each texel pays every step.
        uniform float shaft_scale = 1.0;
        uniform float shaft_far_m = 6000.0;
        uniform float shaft_height_m = 1500.0;
        // The heads' shape (csky_pc_cover_lat_gap). `taper` rounds each cloud's crown toward its own
        // top. The sample point billows by `warp_m` and leans `shear_m` downwind at the top.
        uniform float taper = 0.3;
        uniform float head_soft = 0.16;
        uniform float warp_m = 320.0;
        uniform float shear_m = 260.0;

        // The body is the baked Perlin-Worley noise (CloudNoise) under that coverage, lit by a
        // short-stepped sun march and multiple-scattering octaves.
        uniform sampler3D shape_tex : filter_linear, repeat_enable;
        uniform sampler3D detail_tex : filter_linear, repeat_enable;
        uniform float shape_m = 3000.0;
        uniform float detail_tile_m = 500.0;
        uniform float detail_erode = 0.35;
        uniform float base_erode = 1.0;
        uniform float pw_density = 0.04;
        uniform float light_step_m = 25.0;
        uniform float light_k = 1.0;
        uniform float cover_max = 1.0;
        uniform float base_rag = 0.0;
        uniform float deck_scale = 1.6;
        uniform float deck_k = 0.6;
        uniform float deck_erode = 0.0;
        uniform float amb_occ = 0.0;
        uniform float amb_od_k = 0.25;
        uniform float dither_k = 1.0;
        // The dither's golden-ratio step, one per drawn frame (FollowCamera). TIME at 60 Hz
        // would hold it over several frames above 60 fps, which TAA reads as a pulse.
        uniform float frame_offset = 0.0;
        uniform float clear_stride = 2.0;
        uniform float cover_stride = 2.0;
        uniform float ms_a = 0.5;
        uniform float ms_b = 0.6;
        uniform float ms_c = 0.5;
        uniform float phase_g = 0.6;
        uniform float phase_back = 0.25;
        uniform float sun_gain = 2.2;
        uniform float amb_gain = 0.9;
        uniform float amb_floor = 0.35;
        uniform vec3 amb_top_rgb = vec3(0.55, 0.68, 0.92);
        uniform float amb_fog_mix = 0.35;
        uniform float powder_k = 0.0;
        uniform float cloud_fog = 1.0;
        uniform float wind_ms = 0.0;

        float pc_remap(float x, float a, float b, float c, float d) {
            return c + (x - a) / max(b - a, 1e-4) * (d - c);
        }

        // The baked noise's density in the deck's units, which density_gain scales to extinction.
        float pc_pw_gain() {
            return pw_density / max(density_gain, 1e-6);
        }

        // The heads' coverage at `p`, which the ground shadow reads too, with the stride measure `cgap`.
        // The lattices are read exactly here (CSKY_PC_LAT_EXACT).
        float pc_cover_gap(vec3 p, out float cgap) {
            return csky_pc_cover_lat_gap(p, layer_bottom, layer_top, detail_m, cover_bias, vec4(taper, head_soft, warp_m, shear_m), cgap);
        }

        // The heads over the baked noise. The coverage thresholds the Perlin-Worley base, so an edge
        // erodes into billows. The detail noise then frays the base and pocks the crown.
        // A march may stride the clear distance `reach` from `p` before a cloud can start.
        float pc_heads_kept(vec3 p, float bottom, float top, bool detail, float keep, out float cov, out float reach) {
            float cgap = 1e3;
            // The bubbles leave the share `keep` of the coverage, so a bubble's wall is a cloud's own face.
            float c = clamp(pc_cover_gap(p, cgap) * heads_gain, 0.0, 1.0) * cover_max * keep;
            cov = c;
            if (c <= 0.0) {
                reach = max(cgap, 0.0) * cover_gap_m;
                return 0.0;
            }
            float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
            vec3 q = csky_pc_drifted(p) + vec3(TIME * wind_ms, 0.0, 0.0);
            float v = csky_pc_heads_body(texture(shape_tex, q / shape_m), h, base_erode, base_rag);
            // Held under the coverage's own share, so the stride does not jump where the coverage begins.
            reach = min(max((1.0 - c) + edge_cut * c - v, 0.0) * search_gap_m, c * cover_gap_m);
            float d = clamp(pc_remap(v, 1.0 - c, 1.0, 0.0, 1.0), 0.0, 1.0);
            if (detail && d > 0.0) {
                float hf = csky_pc_octaves(texture(detail_tex, q / detail_tile_m).rgb);
                float m = mix(hf, 1.0 - hf, clamp(h * 6.0, 0.0, 1.0));
                d = clamp(pc_remap(d, m * detail_erode, 1.0, 0.0, 1.0), 0.0, 1.0);
            }
            return clamp((d - edge_cut) * edge_k, 0.0, 1.0);
        }

        // The deck over the baked noise: a sheet over the layer floor, its body the Perlin-Worley
        // shape, so its underside lumps like stratocumulus. The bubbles leave the share `keep` of its cover.
        float pc_deck_pw(vec3 p, float bottom, float keep) {
            float deck_top = bottom + deck_depth_m;
            if (deck_cover * keep <= 0.0 || p.y < bottom || p.y > deck_top) {
                return 0.0;
            }
            float body = csky_pc_deck_body_pw(p, deck_cover * keep, shape_m * deck_scale);
            if (body > 0.0) {
                float hf = csky_pc_octaves(texture(detail_tex, csky_pc_drifted(p) / detail_tile_m).rgb);
                body = clamp(pc_remap(body, hf * deck_erode, 1.0, 0.0, 1.0), 0.0, 1.0);
            }
            return body * csky_pc_deck_profile(p.y, bottom, deck_top) * pc_pw_gain() * deck_k;
        }

        // The heads as the sun sees them, whole: the bubbles cut nothing from a shadow.
        float pc_heads_whole(vec3 p, float bottom, float top, bool detail) {
            float cov;
            float reach;
            return pc_heads_kept(p, bottom, top, detail, 1.0, cov, reach);
        }

        // The cumulus heads at `p` over the pattern's coverage, in the deck's density units.
        float pc_heads(vec3 p, float bottom, float top, float keep) {
            float cov;
            float reach;
            return pc_heads_kept(p, bottom, top, true, keep, cov, reach) * pc_pw_gain();
        }

        // The sun's optical depth from `p`: six taps doubling from light_step_m. The first two read
        // the detail, so the billows shade each other.
        float pc_sun_od_pw(vec3 p, float bottom, float top) {
            float od = 0.0;
            float t = 0.0;
            float dt = light_step_m;
            for (int i = 0; i < 6; i++) {
                vec3 q = p + csky_sun_dir * (t + dt * 0.5);
                if (q.y > top) {
                    break;
                }
                od += pc_heads_whole(q, bottom, top, i < 2) * pw_density * light_k * dt;
                t += dt;
                dt *= 2.0;
            }
            return od;
        }

        float pc_hg(float cos_theta, float g) {
            return csky_pc_phase(cos_theta, g) * 4.0 * 3.14159265;
        }

        // The sun reaching the eye through optical depth `od`. Thinner, flatter octaves stand in
        // for the light scattered more than once.
        float pc_sun_ms(float od, float cos_theta) {
            float sun = 0.0;
            float a = 1.0;
            float b = 1.0;
            float c = 1.0;
            for (int o = 0; o < 4; o++) {
                float phase = mix(pc_hg(cos_theta, phase_g * c), pc_hg(cos_theta, -0.3 * c), phase_back);
                sun += b * exp(-od * a) * phase;
                a *= ms_a;
                b *= ms_b;
                c *= ms_c;
            }
            return sun;
        }

        // The deck at `p`, torn by the rifts.
        float pc_deck(vec3 p, float bottom, float keep) {
            return pc_deck_pw(p, bottom, keep) * csky_pc_deck_gaps(p, deck_gaps, gap_m);
        }

        // How far `p` stands inside one bubble's clear core, in metres: the stride a march may take there.
        float pc_bubble_core_of(vec4 b, vec3 p) {
            return b.w > 0.0 ? max(b.w * 0.7 - distance(p, b.xyz), 0.0) : 0.0;
        }

        float pc_bubble_core(vec3 p) {
            return max(max(pc_bubble_core_of(csky_pc_bubble0, p), pc_bubble_core_of(csky_pc_bubble1, p)),
                max(pc_bubble_core_of(csky_pc_bubble2, p), pc_bubble_core_of(csky_pc_bubble3, p)));
        }

        // The whole field at `p` for the lit march, less the bubbles, with the stride `gap` in metres for clear air.
        float pc_density_gap(vec3 p, float bottom, float top, out float gap) {
            float cov = 0.0;
            float keep = csky_pc_bubbles(p);
            float heads = pc_heads_kept(p, bottom, top, true, keep, cov, gap) * pc_pw_gain();
            // The deck stands under its top, so a ray over it strides no further than its height over that top.
            if (deck_cover > 0.0) {
                gap = min(gap, max(p.y - bottom - deck_depth_m, 0.0));
            }
            gap = max(gap, pc_bubble_core(p));
            return max(heads, pc_deck(p, bottom, keep));
        }

        // The whole field at `p`: the heads over the deck, less the zeppelins' bubbles. Only an eye
        // inside a bubble sees its hull clear; from outside, the cloud round it still hides it.
        float pc_density(vec3 p, float bottom, float top) {
            float keep = csky_pc_bubbles(p);
            return max(pc_heads(p, bottom, top, keep), pc_deck(p, bottom, keep));
        }

        // The deck's optical depth along the sun ray from `p`, two taps over the ray's span through
        // the deck's own depth. The heads' taps are spaced for the whole layer and step over it.
        float pc_deck_od(vec3 p, float bottom) {
            if (deck_cover <= 0.0) {
                return 0.0;
            }
            vec2 span = csky_pc_slab_span(p, csky_sun_dir, bottom, bottom + deck_depth_m);
            if (span.y <= 0.0) {
                return 0.0;
            }
            float dt = (span.y - span.x) * 0.5;
            float od = 0.0;
            for (int i = 0; i < 2; i++) {
                od += pc_deck(p + csky_sun_dir * (span.x + dt * (float(i) + 0.5)), bottom, 1.0) * density_gain * dt;
            }
            return od;
        }

        // The lit cloud body from the march's two weights, the sky's and the sun's, before fog. Their colours
        // are the same along a ray, so they apply once per pixel. The sun's phase is in w.y already (pc_sun_ms).
        // The mission's SUNLIGHT dims the body and the zone tune tints it.
        vec3 pc_colour(vec2 w, vec3 cam) {
            vec3 amb = mix(amb_top_rgb, csky_fog_color_at(cam), amb_fog_mix) * amb_gain;
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * sun_gain;
            return (amb * w.x + sun * w.y) * csky_world_light_at(cam) * csky_tune_cloud_tint;
        }

        // The sun's share reaching a point under the layer. Along the sun ray: the deck's span, then
        // three taps through the heads above it. One where the ray misses the layer.
        float pc_sun_through(vec3 p, float bottom, float top) {
            float od = pc_deck_od(p, bottom);
            vec2 heads = csky_pc_slab_span(p, csky_sun_dir, bottom + deck_depth_m, top);
            if (heads.y > 0.0) {
                float dt = min(heads.y - heads.x, (top - bottom) * 3.0) / 3.0;
                for (int i = 0; i < 3; i++) {
                    od += pc_heads(p + csky_sun_dir * (heads.x + dt * (float(i) + 0.5)), bottom, top, 1.0) * density_gain * dt;
                }
            }
            return exp(-od);
        }

        // The light shafts' gathered light: the sun in-scattered by the haze below the deck, from the
        // eye to `t_end`, each sample shadowed by the layer. The haze thins with height, and the fog swallows it.
        // Returns the light gathered by the end of each quarter of the ray, the last the whole.
        vec4 pc_shaft_light(vec3 eye, vec3 dir, float t_end, float dither) {
            float len = min(t_end, shaft_far_m);
            if (len <= 1.0) {
                return vec4(0.0);
            }
            float dt = len / float(shaft_steps);
            vec4 light = vec4(0.0);
            float t = dt * dither;
            for (int i = 0; i < shaft_steps; i++) {
                vec3 q = eye + dir * t;
                float haze = exp(-max(q.y, 0.0) / shaft_height_m);
                float reach = 1.0 - csky_fog_amount(q, eye);
                // The terrain's baked sun shadow holds in the air too, so a hill's lee carries no shaft.
                float sun_t = pc_sun_through(q, layer_bottom, layer_top) * csky_terrain_baked_sun(q);
                // The sample's quarter and every later one gather it.
                light += sun_t * haze * reach * dt * step(vec4(float(i * 4 / shaft_steps)), vec4(0.0, 1.0, 2.0, 3.0));
                t += dt;
            }
            return light;
        }

        // The shafts' colour from the light pc_shaft_light gathered along `dir`.
        vec3 pc_shaft_colour(float light, vec3 eye, vec3 dir) {
            float phase = csky_pc_phase(dot(dir, csky_sun_dir), 0.5) * 4.0 * 3.14159265;
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * csky_world_light_at(eye);
            return sun * light * shaft_gain * 0.00006 * phase;
        }

        // The march's step at distance `t`: the span's share, but never more than grows with distance
        // from the eye. A level ray's span runs to far_m, and its share alone stepped over a near head.
        float pc_step(float t, float span_step) {
            return min(span_step, min(step_near_m + t * step_growth, step_fine_m + t * step_fine_growth));
        }

        // Marches `dir` from `t0` to `t1`, returning the light's weights (pc_colour) premultiplied and
        // the transmittance. `hit` is the first cloud's distance, else t1.
        vec3 pc_march_hit(vec3 eye, vec3 dir, float t0, float t1, float dither, out float hit) {
            hit = t1;
            float bottom = layer_bottom;
            float top = layer_top;
            float len = t1 - t0;
            int n = max(int(float(steps) * clamp(len / max(top - bottom, 1.0) * 0.5, 0.5, 1.5)), 8);
            float span_step = len / float(n);
            float t = t0 + pc_step(t0, span_step) * dither;
            // The search crosses the clear air before the first cloud on density alone, most of a ray's cost.
            // It strides by the gap to the nearest surface, and the bisection after it finds that surface.
            float clear = t0;
            // The first sample inside the heads' un-eroded shape and the clear one before it, the first bracket.
            float shell = -1.0;
            float shell_clear = t0;
            bool found = false;
            int i = 0;
            while (i < step_budget && t < t1) {
                vec3 sp = eye + dir * t;
                float cov = 0.0;
                float gap = 1.0;
                // The heads are read with their detail erosion. Past the shape's first sample the search goes
                // on to the eroded surface, so the lit march can take a sample there.
                float keep = csky_pc_bubbles(sp);
                float dens = max(pc_heads_kept(sp, bottom, top, true, keep, cov, gap), pc_deck(sp, bottom, keep));
                found = dens > 0.001;
                if (shell < 0.0 && (found || (cov > 0.0 && gap <= 0.0))) {
                    shell = t;
                    shell_clear = clear;
                }
                if (found) {
                    break;
                }
                clear = t;
                float st = pc_step(t, span_step) * (cov > 0.0 ? cover_stride : clear_stride);
                if (search_gap_m > 0.0) {
                    st = clamp(max(gap, pc_bubble_core(sp)), min(max(cloud_min_m, t * search_pix_k), st), st);
                }
                t += st;
                i++;
            }
            if (shell < 0.0) {
                return vec3(0.0, 0.0, 1.0);
            }
            // Two surfaces, bisected outside the loop so a GPU wave bisects once. The first is any cloud in
            // the first bracket, which a long stride may have crossed. The second is the eroded surface.
            float lo = shell_clear;
            float hi = shell;
            float hi2 = t;
            for (int k = 0; k < bisect_n; k++) {
                float mid = (lo + hi) * 0.5;
                if (pc_density(eye + dir * mid, bottom, top) > 0.001) {
                    hi = mid;
                } else {
                    lo = mid;
                }
                float mid2 = (clear + hi2) * 0.5;
                if (pc_density(eye + dir * mid2, bottom, top) > 0.001) {
                    hi2 = mid2;
                } else {
                    clear = mid2;
                }
            }
            bool first = hi < shell || (found && t <= shell);
            float surf = (lo + hi) * 0.5;
            float surf2 = (clear + hi2) * 0.5;
            hit = first ? surf : (found ? surf2 : shell);
            t = min((first ? surf : shell) + pc_step(hi, span_step) * mix(0.5, dither, hit_dither), t1);
            vec2 w = vec2(0.0);
            float trans = 1.0;
            float cos_theta = dot(dir, csky_sun_dir);
            if (od_step > 0.0) {
                float prev_dt = max(cloud_min_m, hit * pix_k);
                // Every ray takes a sample at `entry`, a share of a step inside the eroded surface, so
                // neighbouring rays light the skin alike.
                float entry = found ? surf2 + prev_dt * first_k : t1;
                t = first ? surf + prev_dt * first_k : shell;
                float grow = 2.0;
                // The last sample waits for the next one to confirm its step, so a retreat shortens it uncounted.
                bool pend = false;
                float p_sigma = 0.0;
                vec2 p_light = vec2(0.0);
                bool lit = false;
                float l_ao = 1.0;
                float l_acc = 0.0;
                float l_sun = 0.0;
                while (i < step_budget && trans > 0.01 && t < t1) {
                    float far_dt = min(pc_step(t, span_step), lit_max_m + t * lit_growth);
                    float near_dt = min(max(cloud_min_m, t * pix_k), far_dt);
                    vec3 p = eye + dir * t;
                    float gap = 0.0;
                    float d = pc_density_gap(p, bottom, top, gap);
                    float sigma = d * density_gain;
                    i++;
                    // Clear air between clouds takes the search's stride, and beside a cloud the lit cap.
                    // The cap there is the step inside thin cloud, so the step does not jump at a cloud's edge.
                    if (d <= 0.001 && search_gap_m > 0.0) {
                        far_dt = max(far_dt, min(gap, pc_step(t, span_step)));
                    }
                    // A long step that landed in dense cloud retreats to the step its density allows, so the skin
                    // is stepped finely. The retreat is nothing at the threshold, so neighbouring rays do not jump.
                    // It runs once a step, so it cannot spend the budget creeping toward its threshold.
                    if (sigma * prev_dt > od_step * retreat_k && prev_dt > near_dt * 1.01 && grow > 1.5) {
                        float back = max(od_step * retreat_k / sigma, near_dt);
                        t -= prev_dt - back;
                        prev_dt = back;
                        grow = 1.0;
                        continue;
                    }
                    if (pend) {
                        float a = 1.0 - exp(-p_sigma * prev_dt);
                        w += trans * a * p_light;
                        trans *= 1.0 - a;
                    }
                    float dt = clamp(od_step / max(sigma, 1e-6), near_dt, max(near_dt, min(far_dt, prev_dt * grow)));
                    grow = 2.0;
                    pend = d > 0.001;
                    if (pend) {
                        float a_est = trans * (1.0 - exp(-sigma * dt));
                        l_acc += a_est;
                        if (!lit || l_acc >= light_every_a) {
                            l_acc = 0.0;
                            float od = pc_sun_od_pw(p, bottom, top) + pc_deck_od(p, bottom);
                            l_ao = mix(1.0, exp(-od * amb_od_k), amb_occ);
                            l_sun = pc_sun_ms(od, cos_theta);
                            lit = true;
                        }
                        float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
                        float powder = mix(1.0, 1.0 - exp(-d * density_gain * 60.0), powder_k);
                        p_sigma = sigma;
                        p_light = vec2(mix(amb_floor, 1.0, h) * l_ao, l_sun * powder);
                    }
                    if (t < entry && t + dt > entry) {
                        dt = entry - t;
                    }
                    t += dt;
                    prev_dt = dt;
                }
                if (pend) {
                    float a = 1.0 - exp(-p_sigma * prev_dt);
                    w += trans * a * p_light;
                    trans *= 1.0 - a;
                }
                return vec3(w, trans);
            }
            // The plain march (od_step 0): fixed steps, every sample lit.
            while (i < step_budget && trans > 0.01 && t < t1) {
                float dt = pc_step(t, span_step);
                vec3 p = eye + dir * t;
                float d = pc_density(p, bottom, top);
                if (d > 0.001) {
                    float a = 1.0 - exp(-d * density_gain * dt);
                    float od = pc_sun_od_pw(p, bottom, top) + pc_deck_od(p, bottom);
                    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
                    float powder = mix(1.0, 1.0 - exp(-d * density_gain * 60.0), powder_k);
                    float amb = mix(amb_floor, 1.0, h) * mix(1.0, exp(-od * amb_od_k), amb_occ);
                    w += trans * a * vec2(amb, pc_sun_ms(od, cos_theta) * powder);
                    trans *= 1.0 - a;
                }
                t += dt;
                i++;
            }
            return vec3(w, trans);
        }

        // Where the march along a ray through the layer's `span` ends: the exit, the scene or the cap.
        float pc_march_end(vec2 span, float scene) {
            return min(min(span.y, scene), span.x + far_m);
        }

        // A pixel's march through the layer before the fog: the light's weights and the transmittance.
        // A ray missing the layer reads its `hit` at 20 km.
        vec3 pc_layer_hit(vec3 eye, vec3 dir, vec2 span, float scene, float dither, out float hit) {
            hit = 20000.0;
            if (span.y >= 0.0) {
                float t1 = pc_march_end(span, scene);
                if (t1 > span.x) {
                    return pc_march_hit(eye, dir, span.x, t1, dither, hit);
                }
            }
            return vec3(0.0, 0.0, 1.0);
        }

        vec3 pc_layer(vec3 eye, vec3 dir, vec2 span, float scene, float dither) {
            float hit;
            return pc_layer_hit(eye, dir, span, scene, dither, hit);
        }

        // A pixel's shaft light by quarters (pc_shaft_light). Under the layer the ray crosses the haze
        // first, up to the layer, the scene or the cap.
        vec4 pc_shafts(vec3 eye, vec3 dir, vec2 span, float scene, float dither) {
            if (shaft_gain <= 0.0 || eye.y >= layer_bottom) {
                return vec4(0.0);
            }
            return pc_shaft_light(eye, dir, min(scene, span.y >= 0.0 ? span.x : shaft_far_m), dither);
        }

        // A pixel's whole trace: the layer's weights and transmittance, then the shafts' light.
        // The shafts take their own dither: the stable march runs with none, and they need all of it.
        vec4 pc_trace(vec3 eye, vec3 dir, vec2 span, float scene, float dither, float shaft_dither) {
            return vec4(pc_layer(eye, dir, span, scene, dither), pc_shafts(eye, dir, span, scene, shaft_dither).w);
        }
        """;

    // The quad's or the reduced march's material over the layer from `bottom` to `top`.
    private ShaderMaterial MaterialOf(string code, float bottom, float top)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = code } };
        material.SetShaderParameter("layer_bottom", bottom);
        material.SetShaderParameter("layer_top", top);
        // A deck chapter reads as a near-solid overcast with a few tall heads over it, formed only
        // where the pattern is strong. One without a deck draws fuller heads alone.
        material.SetShaderParameter("deck_cover", _hasDeck ? DeckCover : 0f);
        material.SetShaderParameter("cover_bias", CoverBias);
        material.SetShaderParameter("taper", Tune.HeadTaper);
        material.SetShaderParameter("heads_gain", BaseHeadsGain);
        // A deck tears open in rifts and the sun falls through them in shafts. The shafts are strong
        // where the mission authors rain and faint in a dry haze.
        material.SetShaderParameter("deck_gaps", _hasDeck ? DeckGaps : 0f);
        material.SetShaderParameter("gap_m", GapM);
        material.SetShaderParameter("shaft_gain", ShaftGain);
        ApplyLook(material);
        return material;
    }

    // The look in three stages, each snapshot kept for the cloud lab. The weather's preset comes first,
    // then the chapter's saved look (CloudShadowChapters.LookBlock), then the lab's live edits.
    private void ApplyLook(ShaderMaterial material)
    {
        _lookValues.Clear();
        material.SetShaderParameter("shape_tex", CloudNoise.Shape);
        material.SetShaderParameter("detail_tex", CloudNoise.Detail);
        string preset = Storm ? StormPreset : _hasDeck ? DeckPreset : ClearPreset;
        ApplySet(preset, material);
        _presetValues.Clear();
        foreach (var kv in _lookValues)
            _presetValues[kv.Key] = kv.Value;
        string saved = CloudShadow.Live is { } shadow ? string.Join(";", System.Linq.Enumerable.Select(shadow.SavedLook, kv => Pair(kv.Key, kv.Value))) : string.Empty;
        ApplySet(saved, material);
        RenderingServer.GlobalShaderParameterSet(CloudShadow.ShapeTexParam, CloudNoise.Shape);
        RenderingServer.GlobalShaderParameterSet(CloudShadow.WarpLatticeParam, CloudNoise.WarpLattice);
        RenderingServer.GlobalShaderParameterSet(CloudShadow.TopsLatticeParam, CloudNoise.TopsLattice);
        Log.Info("world", $"volumetric clouds: look {(Storm ? "storm" : _hasDeck ? "deck" : "clear")}, saved [{saved}]");
        _savedValues.Clear();
        foreach (var kv in _lookValues)
            _savedValues[kv.Key] = kv.Value;
        _savedTop = _top;
        ApplySet(LabSetText, material);
    }

    // Writes "name=v;name=a,b,c" onto `materials`, floats kept for LookOr. The int uniforms take ints.
    private void ApplySet(string set, params ShaderMaterial?[] materials)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var pair in set.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=');
            var parts = kv.Length == 2 ? kv[1].Split(',') : Array.Empty<string>();
            if (parts.Length is not (1 or 3) || !Array.TrueForAll(parts, p => float.TryParse(p, System.Globalization.NumberStyles.Float, inv, out _)))
            {
                Log.Warn("world", $"volumetric clouds: set skips '{pair}'");
                continue;
            }
            string name = kv[0].Trim();
            float first = float.Parse(parts[0], inv);
            if (name == "temporal")
            {
                _temporalOn = first != 0f;
                continue;
            }
            if (name == "bubbles")
            {
                BubblesOn = first != 0f;
                continue;
            }
            if (name == "divisor")
            {
                VolumetricCloudsSetting.TakeUnsaved(first >= 4f ? VolumetricCloudsSetting.Quarter : first >= 2f ? VolumetricCloudsSetting.Half : VolumetricCloudsSetting.Full);
                continue;
            }
            if (name == "rebuild")
            {
                Rebuild();
                return;
            }
            if (name is "depth_m" or "layer_top" && _bottom is { } floor)
            {
                // The layer's top moves with the uniform, so the ground shadow, the veil and the fog follow.
                first += name == "depth_m" ? floor : 0f;
                name = "layer_top";
                _top = first;
            }
            Variant value = parts.Length == 3 ? (Variant)new Vector3(first, float.Parse(parts[1], inv), float.Parse(parts[2], inv))
                : name is "steps" or "step_budget" or "shaft_steps" or "bisect_n" ? (Variant)(int)first : (Variant)first;
            foreach (var material in materials)
                material?.SetShaderParameter(name, value);
            if (parts.Length == 1)
                _lookValues[name] = first;
        }
    }

    // A look uniform as the slab stands, else `fallback`, what the slab takes with the look silent on it.
    private float LookOr(string name, float fallback) => _lookValues.TryGetValue(name, out float v) ? v : fallback;

    // A look uniform as the slab stands, else the shader's default.
    private float LookOr(string name) => LookOr(name, UniformDefaults[name]);

    // A look uniform every preset sets (BasePreset), as the slab stands.
    private float Look(string name) => _lookValues[name];

    private void Rebuild()
    {
        FreeBuilt();
        _baseTop ??= _top;
        if (_bottom is { } floor && _baseTop is { } roof)
            _top = Storm ? Mathf.Max(roof, floor + StormDepthM) : roof;
        _savedTop = _presetTop = _top;
        var root = new Node3D { Name = "volumetric_slab" };
        string detail = BuildSlab(root);
        _built = root;
        AddChild(root);
        string layer = _bottom is { } b && _top is { } t ? $"layer {b:0}-{t:0} m" : "no cloud band";
        Log.Info("world", $"volumetric clouds: {layer}, deck cover {(_hasDeck ? DeckCover : 0f):0.00} gaps {(_hasDeck ? DeckGaps : 0f):0.00}, shafts {ShaftGain:0.0}, {detail}, hidden: deck={(_weather.HideDeck ? "yes" : "no")} field={(_field is { } f && !f.Visible ? "yes" : "no")} sprites={_hidden.Count}, bubbles={_hulls.Count}");
    }

    private string BuildSlab(Node3D root)
    {
        if (_bottom is not { } bottom || _top is not { } top)
            return "nothing drawn";
        _material = MaterialOf(SlabShader(), bottom, top);
        // Over the world's transparents and the flare, under the rain in front of the layer and the stereo HUD tiers.
        _material.RenderPriority = HeadLockedQuads.CloudSlabPriority;
        // The puffers and weapon sprites draw after it, each dimmed by the slab's transmittance to it (csky_pc_veil_at). One in
        // front of a cloud covers it, and one behind shows only through its gaps. The rain draws last.
        MultiMeshEmitterRenderer.DrawPriority = HeadLockedQuads.OverSlabPriority;
        // The world's blended surfaces join them, each fragment dimmed the same way.
        SceneBuilder.OverSlabPriority = HeadLockedQuads.OverSlabPriority;
        Precipitation.DrawPriority = HeadLockedQuads.RainOverSlabPriority;
        var quad = new MeshInstance3D
        {
            Name = "slab",
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = _material,
            CustomAabb = new Aabb(new Vector3(-1e6f, -1e6f, -1e6f), new Vector3(2e6f, 2e6f, 2e6f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            Layers = uint.MaxValue,
        };
        root.AddChild(quad);

        // The reduced march renders before the view that reads it, as every SubViewport does. Its
        // target is linear half floats, so the trace's weights and shaft light arrive unclamped.
        _march = MaterialOf(MarchShader(), bottom, top);
        _marchView = new SubViewport
        {
            Name = "march",
            Disable3D = true,
            UseHdr2D = true,
            // Without it the target's alpha reads 1 whatever the canvas wrote.
            TransparentBg = true,
            Size = Vector2I.One,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var canvas = new ColorRect { Material = _march, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _marchView.AddChild(canvas);
        root.AddChild(_marchView);
        _material.SetShaderParameter("march_texture", _marchView.GetTexture());
        RenderingServer.ViewportSetMeasureRenderTime(_marchView.GetViewportRid(), true);
        _marchTime = new MeasuredRenderTime(_marchView.GetViewportRid());
        // Siblings draw in tree order, so the resolve targets follow the march they read.
        for (int i = 0; i < 2; i++)
        {
            _resolve[i] = new ShaderMaterial { Shader = new Shader { Code = ResolveShader() } };
            _resolveViews[i] = new SubViewport
            {
                Name = $"resolve{i}",
                Disable3D = true,
                UseHdr2D = true,
                TransparentBg = true,
                Size = Vector2I.One,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            };
            var rect = new ColorRect { Material = _resolve[i], MouseFilter = Control.MouseFilterEnum.Ignore };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _resolveViews[i]!.AddChild(rect);
            root.AddChild(_resolveViews[i]);
            _resolve[i]!.SetShaderParameter("fresh", _marchView.GetTexture());
            // A look that sets none of the resolve's own options leaves it at the shader's defaults.
            foreach (string name in ResolveOptions)
            {
                if (_lookValues.TryGetValue(name, out float value))
                    _resolve[i]!.SetShaderParameter(name, value);
            }
        }
        for (int i = 0; i < 2; i++)
            _resolve[i]!.SetShaderParameter("history", _resolveViews[1 - i]!.GetTexture());
        RenderingServer.ViewportSetMeasureRenderTime(_resolveViews[0]!.GetViewportRid(), true);
        _resolveTime = new MeasuredRenderTime(_resolveViews[0]!.GetViewportRid());
        RenderingServer.FramePreDraw += FollowCamera;
        return "one full-screen march";
    }

    // Before each draw, once every camera has moved: aims the reduced march at the view's camera. At a
    // divisor of 1 the target traces the shafts alone, at ShaftDivisor, while a shaft gain stands. No
    // camera, or a headset's two eyes, leave every pixel to the quad.
    private void FollowCamera()
    {
        if (_material == null || _march == null || _marchView == null || !IsInsideTree())
            return;
        FollowHulls();
        var viewport = GetViewport();
        var camera = viewport.GetCamera3D();
        int resolution = VolumetricCloudsSetting.MarchDivisor;
        bool temporal = resolution <= 2 && _temporalOn;
        bool shaftsOnly = resolution == 1 && !temporal;
        int divisor = shaftsOnly ? ShaftDivisor : temporal ? resolution * 2 : resolution;
        bool on = camera != null && !viewport.UseXR && (!shaftsOnly || ShaftGain > 0f);
        temporal &= on;
        var full = (Vector2I)viewport.GetVisibleRect().Size;
        // Two halves side by side: the layer's trace, then the shafts' (MarchShader).
        var size = on ? new Vector2I(Math.Max((full.X + divisor - 1) / divisor, 1) * 2, Math.Max((full.Y + divisor - 1) / divisor, 1)) : Vector2I.Zero;
        if (size != _marchSize || shaftsOnly != _marchShaftsOnly || temporal != _temporal)
        {
            _marchSize = size;
            _marchShaftsOnly = shaftsOnly;
            _temporal = temporal;
            _historyOk = false;
            _marchView.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            if (on)
                _marchView.Size = size;
            // The resolve spans the march's 2x2 grid, a pixel over the view on an odd side.
            foreach (var resolveView in _resolveViews)
            {
                resolveView!.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                if (temporal)
                    resolveView.Size = new Vector2I(size.X, size.Y * 2);
            }
            _material.SetShaderParameter(MarchOnParam, on);
            _material.SetShaderParameter(MarchShaftsOnlyParam, shaftsOnly);
            _material.SetShaderParameter("march_temporal", temporal);
            // The temporal resolve pulses brightness frame to frame under the jitter, so only the per-pixel march takes it.
            float hitDither = LookOr("hit_dither", resolution == 1 && !temporal ? 1f : 0f);
            _material.SetShaderParameter("hit_dither", hitDither);
            _march.SetShaderParameter("hit_dither", hitDither);
            _march.SetShaderParameter(ShaftsOnlyParam, shaftsOnly);
            // The shafts march at a quarter of the view at most, whatever the target's size.
            float shaftScale = Mathf.Min(divisor / 4f, 1f);
            _material.SetShaderParameter("shaft_scale", shaftScale);
            _march.SetShaderParameter("shaft_scale", shaftScale);
            _march.SetShaderParameter("jitter", Vector2.Zero);
            _march.SetShaderParameter("dither_phase", new Vector2(-1f, -1f));
            MarchTime = on ? _marchTime : null;
            ResolveTime = temporal ? _resolveTime : null;
            string what = !on ? "full resolution" : temporal ? $"1/{resolution} resolution, temporal over a 1/{divisor} march, {size.X / 2}x{size.Y}" : shaftsOnly ? $"full resolution, shafts at 1/{divisor}, {size.X / 2}x{size.Y}" : $"1/{divisor} resolution, {size.X / 2}x{size.Y}";
            Log.Info("world", $"volumetric clouds: march at {what}");
        }
        // One golden-ratio step of the dither per drawn frame, whatever the frame rate.
        float frameOffset = (float)(_ditherFrame++ * 0.6180339887 % 1.0);
        _material.SetShaderParameter("frame_offset", frameOffset);
        _march.SetShaderParameter("frame_offset", frameOffset);
        _material.SetShaderParameter(PointLightsParam, CloudGlowSetting.Drawn ? WorldLights.RankedHead : 0);
        if (!on)
            return;
        var view = camera!.GetCameraTransform();
        var inverse = camera.GetCameraProjection().Inverse();
        if (temporal)
            StepTemporal(camera.GetCameraProjection() * new Projection(view.AffineInverse()), new Projection(view), inverse, new Vector2I(size.X, size.Y * 2));
        _march.SetShaderParameter(ViewCameraParam, new Projection(view));
        _march.SetShaderParameter(ViewInvProjectionParam, inverse);
        _material.SetShaderParameter(MarchEyeParam, view.Origin);
        _material.SetShaderParameter(MarchBackParam, view.Basis.Z);
        _material.SetShaderParameter(MarchLensParam, new Vector2(Mathf.Abs(inverse.X.X), Mathf.Abs(inverse.Y.Y)));
    }

    // One temporal frame. The march traces this frame's pixel of each 2x2 block. The
    // resolve target due draws once over the other's history, and the quad reads it.
    private void StepTemporal(Projection viewProjection, Projection camera, Projection inverse, Vector2I grid)
    {
        var phase = Phases[_temporalFrame & 3];
        int due = _temporalFrame & 1;
        _temporalFrame++;
        _march!.SetShaderParameter("jitter", new Vector2((phase.X - 0.5f) / grid.X, (phase.Y - 0.5f) / grid.Y));
        _march.SetShaderParameter("dither_phase", new Vector2(phase.X, phase.Y));
        var resolve = _resolve[due]!;
        resolve.SetShaderParameter("phase", phase);
        resolve.SetShaderParameter("history_ok", _historyOk);
        resolve.SetShaderParameter("prev_view_projection", _prevViewProjection);
        resolve.SetShaderParameter("prev_eye", _prevEye);
        resolve.SetShaderParameter(ViewCameraParam, camera);
        resolve.SetShaderParameter(ViewInvProjectionParam, inverse);
        _resolveViews[due]!.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _material!.SetShaderParameter("resolved_texture", _resolveViews[due]!.GetTexture());
        _prevViewProjection = viewProjection;
        _prevEye = new Vector3(camera.W.X, camera.W.Y, camera.W.Z);
        _historyOk = true;
    }

    // The world structures the chapter's cloud entry names, first, so a zeppelin past the four slots loses its
    // bubble before one does. Then each zeppelin's host and its bubble: the radius about its origin holding
    // the whole hull, whichever way it turns, plus the margin.
    private void CollectHulls()
    {
        _hulls.Clear();
        foreach (string name in CloudShadow.Live?.SavedBubbles ?? Array.Empty<string>())
        {
            if (_worldRoot.FindChild(name, true, false) is not Node3D structure)
                continue;
            // World geometry's node stands at the world origin, so its bubble is centred on its bounds.
            var bounds = SubtreeBounds.WorldAabb(structure);
            float radius = bounds.Size.Length() * 0.5f + BubbleMarginM;
            _hulls.Add((structure, structure.GlobalTransform.AffineInverse() * bounds.GetCenter(), radius));
            var at = bounds.GetCenter();
            Log.Info("world", $"volumetric clouds: bubble round {name} at ({at.X:0},{at.Y:0},{at.Z:0}), radius {radius:0} m");
        }
        var hosts = new List<Node3D>();
        _zeppelins?.CollectHosts(hosts);
        if (_zeppelins == null)
        {
            // A freecam world runs no zeppelins, so its parked hulls stand in for the cloud lab.
            // ponytail: found by a name ending in "zep". Pass the mission's zeppelin records if a hull is missed.
            foreach (var node in _worldRoot.FindChildren("*zep", "Node3D", true, false))
            {
                // A hull's own part of that name takes no second bubble.
                if (!hosts.Exists(h => h.IsAncestorOf(node)))
                    hosts.Add((Node3D)node);
            }
        }
        foreach (var host in hosts)
        {
            var box = SubtreeBounds.WorldAabb(host);
            _hulls.Add((host, Vector3.Zero, box.GetCenter().DistanceTo(host.GlobalPosition) + box.Size.Length() * 0.5f + BubbleMarginM));
        }
        if (_hulls.Count > CloudShadow.BubbleParams.Length)
            Log.Warn("world", $"volumetric clouds: {_hulls.Count} bubble hosts, bubbles round the first {CloudShadow.BubbleParams.Length} standing alone");
    }

    // Publishes where each standing hull's bubble is now, for both marches and the effects' veil. An
    // empty slot, or every slot with the clouds off (PublishGround), has a zero radius.
    private void FollowHulls()
    {
        if (_hulls.Count == 0)
            return;
        int n = 0;
        foreach (var (host, centre, radius) in _hulls)
        {
            if (BubblesOn && n < CloudShadow.BubbleParams.Length && GodotObject.IsInstanceValid(host) && host.IsVisibleInTree())
            {
                var at = host.GlobalTransform * centre;
                RenderingServer.GlobalShaderParameterSet(CloudShadow.BubbleParams[n++], new Vector4(at.X, at.Y, at.Z, radius));
            }
        }
        for (; n < CloudShadow.BubbleParams.Length; n++)
            RenderingServer.GlobalShaderParameterSet(CloudShadow.BubbleParams[n], Vector4.Zero);
    }

    // The placed cloud sprites: every mesh instance under the world root wearing a pooled material.
    private void CollectPlacedSprites()
    {
        _placedSprites.Clear();
        if (_scene == null)
            return;
        Walk(_worldRoot);
        // ⚠ Reading MeshInstance3D.Mesh mints a managed wrapper per mesh; a short run that quits
        // first aborts on Godot's exit check without this (CloudShadow.CeilingDriftOf).
        GC.Collect();

        void Walk(Node node)
        {
            if (node is MeshInstance3D instance && instance != _built && IsPlacedCloud(instance))
                _placedSprites.Add((instance, instance.GlobalTransform * instance.GetAabb()));
            foreach (var child in node.GetChildren())
                Walk(child);
        }
    }

    private bool IsPlacedCloud(MeshInstance3D instance)
    {
        if (instance.Mesh is not { } mesh)
            return false;
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            var material = instance.GetSurfaceOverrideMaterial(i) ?? mesh.SurfaceGetMaterial(i);
            if (material != null && _scene!.IsPlacedCloudMaterial(material))
                return true;
        }
        return false;
    }

    private void HideOriginals()
    {
        if (_hiding)
            return;
        _hiding = true;
        _weather.HideDeck = true;
        _weather.HoldZone = true;
        WeatherRig.NightOff = true;
        _weather.ReapplyZone();
        var sun = _weather.SunOrientation;
        Log.Info("world", $"volumetric clouds: zone held, night off, sun at {Mathf.RadToDeg(sun.X):0.#}° pitch / {Mathf.RadToDeg(sun.Y):0.#}° yaw, cloud shadows {(CloudShadow.Live?.Draws == true ? "drawn" : "not drawn")}");
        if (_field != null && _bottom != null)
            _field.Visible = false;
        foreach (var (node, _) in _placedSprites)
        {
            if (GodotObject.IsInstanceValid(node) && node.Visible)
            {
                node.Visible = false;
                _hidden.Add(node);
            }
        }
    }

    private void Restore()
    {
        if (!_hiding)
            return;
        _hiding = false;
        _weather.HideDeck = false;
        _weather.HoldZone = false;
        WeatherRig.NightOff = false;
        _weather.ReapplyZone();
        if (_field != null)
            _field.Visible = true;
        foreach (var node in _hidden)
        {
            if (GodotObject.IsInstanceValid(node))
                node.Visible = true;
        }
        _hidden.Clear();
    }

    private void FreeBuilt()
    {
        if (_material != null)
        {
            RenderingServer.FramePreDraw -= FollowCamera;
            MultiMeshEmitterRenderer.DrawPriority = 0;
            SceneBuilder.OverSlabPriority = 0;
            Precipitation.DrawPriority = 0;
        }
        _material = null;
        _march = null;
        _marchView = null;
        _marchTime = null;
        _marchSize = Vector2I.Zero;
        _marchShaftsOnly = false;
        MarchTime = null;
        _resolve[0] = _resolve[1] = null;
        _resolveViews[0] = _resolveViews[1] = null;
        _resolveTime = null;
        _temporal = false;
        ResolveTime = null;
        if (_built == null)
            return;
        _built.QueueFree();
        _built = null;
    }

    // The ground shadow's globals (csky_cloud_shadow.gdshaderinc): the deck and its rifts, and the
    // heads' pattern read where the sun ray meets them. Every frame, since a zone apply turns the sun.
    private void PublishGround(bool on)
    {
        if (!on || _bottom is not { } bottom || _top is not { } top)
        {
            PublishedGround = Vector4.Zero;
            RenderingServer.GlobalShaderParameterSet(CloudShadow.GroundParam, Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet(CloudShadow.GroundSunParam, new Vector4(0f, 1f, 0f, 0f));
            RenderingServer.GlobalShaderParameterSet(CloudShadow.FogParam, Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet(CloudShadow.VeilParam, Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet(CloudShadow.PwParam, Vector4.Zero);
            foreach (var bubble in CloudShadow.BubbleParams)
                RenderingServer.GlobalShaderParameterSet(bubble, Vector4.Zero);
            return;
        }
        // The layer the effects drawn after the slab march their transmittance through (csky_pc_veil_at).
        RenderingServer.GlobalShaderParameterSet(CloudShadow.VeilParam, new Vector4(bottom, top, _hasDeck ? DeckCover : 0f, DeckDepthM));
        // The fog splits at the layer: the held lower zone below it, the upper zone's range above it.
        if (_weather.Mission is { } mission && !_fitted)
        {
            var upper = WeatherRig.FogRangeFor(mission.Zone(mission.ZoneForState(2)));
            RenderingServer.GlobalShaderParameterSet(CloudShadow.FogParam, new Vector4(bottom, upper.X, upper.Y, 0f));
        }
        else
        {
            RenderingServer.GlobalShaderParameterSet(CloudShadow.FogParam, Vector4.Zero);
        }
        var sun = _weather.SunDirection;
        var tune = Tune;
        float deckY = bottom + DeckDepthM * 0.4f;
        PublishedGround = new Vector4(deckY, _hasDeck ? tune.DeckSunTaken : 0f, DeckGaps, GapM);
        RenderingServer.GlobalShaderParameterSet(CloudShadow.GroundParam, PublishedGround);
        RenderingServer.GlobalShaderParameterSet(CloudShadow.GroundSunParam,
            new Vector4(sun.X, sun.Y, sun.Z, bottom + (top - bottom) * 0.3f));
        RenderingServer.GlobalShaderParameterSet(CloudShadow.HeadsShapeParam, HeadShape with { Y = LookOr("head_soft") });
        RenderingServer.GlobalShaderParameterSet(CloudShadow.HeadsLayerParam, new Vector4(bottom, top, DetailM, CoverBias));
        // The slab's heads_gain and its density_gain default, so the ground marches its density, and the
        // ambient share the deck takes. In y, the bases' erosion (csky_pc_heads_body).
        RenderingServer.GlobalShaderParameterSet(CloudShadow.HeadsDensityParam,
            new Vector4(LookOr("heads_gain", BaseHeadsGain), Look("base_erode"), UniformDefaults["density_gain"], _hasDeck ? tune.DeckAmbientTaken : 0f));
        RenderingServer.GlobalShaderParameterSet(CloudShadow.PwParam,
            new Vector4(Look("shape_m"), Look("cover_max"), Look("base_rag"), Look("pw_density")));
        // The deck's shape tile and density gain (the shader's shape_m * deck_scale and deck_k), for the veil.
        // Then the heads' cut edge, for the ground shadow and the veil.
        RenderingServer.GlobalShaderParameterSet(CloudShadow.PwDeckParam, new Vector4(Look("shape_m") * Look("deck_scale"), Look("deck_k"), Look("edge_cut"), Look("edge_k")));
    }

    // A band authored far above every placed cloud is the original's "no band" altitude (C1B, C3,
    // C5 at 10 km). The layer then spans the placed clouds' heights, or is absent with none placed.
    // C5's fvols are ground fog banks, not a layer, so its fog field stays drawn (HideOriginals).
    private void FitLayerToSprites()
    {
        if (_hasDeck)
            return;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var (_, world) in _placedSprites)
        {
            lo = Mathf.Min(lo, world.Position.Y);
            hi = Mathf.Max(hi, world.End.Y);
        }
        float? band = _bottom;
        if (band is { } b && b < hi + SpriteBandMarginM)
            return;
        string at = band is { } a ? $"at {a:0} m" : "absent";
        if (_placedSprites.Count == 0)
        {
            _bottom = _top = null;
            Log.Info("world", $"volumetric clouds: band {at} has no placed cloud under it, no layer drawn");
            return;
        }
        _bottom = lo;
        _top = Thickened(lo, hi);
        _fitted = true;
        Log.Info("world", $"volumetric clouds: band {at} stands clear of the {_placedSprites.Count} placed clouds, layer fitted to them at {lo:0}-{_top:0} m");
    }

    // Rewrites the cloud tune's taper and the zone tune's god rays onto the slab's material when a lab
    // or a zone apply moves one.
    private void FollowKnobs()
    {
        float taper = Tune.HeadTaper, shaft = ShaftGain;
        if (_material == null || (taper == _appliedTaper && shaft == _appliedShaft))
            return;
        _appliedTaper = taper;
        _appliedShaft = shaft;
        foreach (var material in new[] { _material, _march })
        {
            material?.SetShaderParameter("taper", taper);
            material?.SetShaderParameter("shaft_gain", shaft);
        }
    }
}
