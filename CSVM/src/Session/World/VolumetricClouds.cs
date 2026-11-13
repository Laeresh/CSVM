using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// Volumetric clouds in place of the cloud deck, the fogvol field and the placed cloud sprites,
/// under Enhanced alone (<see cref="VolumetricCloudsSetting"/>). The slab is one full-screen raymarch through
/// the mission's cloud band, over the cloud shadows' own pattern. It marches at a fraction of the view's
/// resolution (<see cref="MarchDivisor"/>). It draws cumulus heads over an
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

    // PROTOTYPE (clouds-look): the tuned looks. A clear day lights the heads hard from the sun; a
    // storm dims the sun, greys the ambient and lets fewer, taller heads stand.
    private const string Common = "shape_m=1500;detail_tile_m=250;phase_g=0.45;powder_k=0.6;fast_cover=1;amb_occ=0.6;base_rag=0.08;deck_scale=0.6;deck_erode=0.5;cover_stride=1";
    private const string ClearPreset = Common + ";head_soft=0.5;amb_gain=0.6;amb_floor=0.3;light_k=1.5;sun_gain=2.4";
    // An overcast deck under the heads: a lighter base, and the heads broken up over it.
    private const string DeckPreset = ClearPreset + ";amb_floor=0.45;cover_max=0.7;amb_gain=0.7";
    private const string StormPreset = Common + ";head_soft=1.0;heads_gain=0.7;cover_max=0.6;sun_gain=0.5;light_k=2.5;ms_b=0.75;amb_floor=0.6;amb_fog_mix=0.2;amb_top_rgb=0.42,0.45,0.5;amb_gain=1.3";
    // A storm's heads tower this far over the layer floor.
    private const float StormDepthM = 2450f;

    // The slab's draw order: past the flare's four quads, then the puffers over it, then the rain.
    private const int SlabPriority = Precipitation.OverSlabPriority - 2;

    // The shafts' target divisor at full resolution: the haze is soft, the cloud edges are not.
    private const int ShaftDivisor = 2;

    private static readonly StringName MarchOnParam = "march_on";
    private static readonly StringName MarchShaftsOnlyParam = "march_shafts_only";
    private static readonly StringName ShaftsOnlyParam = "shafts_only";
    private static readonly StringName MarchEyeParam = "march_eye";
    private static readonly StringName MarchBackParam = "march_back";
    private static readonly StringName MarchLensParam = "march_lens";
    private static readonly StringName ViewCameraParam = "view_camera";
    private static readonly StringName ViewInvProjectionParam = "view_inv_projection";

    // PROTOTYPE (clouds-look): CSVM_CLOUD_LOOK=1 starts the baked-noise look; Ctrl+Shift+V swaps it live.
    private static bool _prototype = System.Environment.GetEnvironmentVariable("CSVM_CLOUD_LOOK") == "1";

    private readonly Node3D _worldRoot;
    private readonly WeatherRig _weather;
    private readonly FogVolumeClutter? _field;
    private readonly SceneBuilder? _scene;
    private readonly List<(MeshInstance3D Node, Aabb World)> _placedSprites = new();
    private readonly List<MeshInstance3D> _hidden = new();
    // The prototype's float uniforms as its preset and CSVM_CLOUD_SET left them (PROTOTYPE, clouds-look).
    private readonly Dictionary<string, float> _protoValues = new();
    private readonly bool _hasDeck;
    private ShaderMaterial? _material;
    // The reduced march (MarchShader), its target, the target's render time and its size, zero while off.
    private ShaderMaterial? _march;
    private SubViewport? _marchView;
    private MeasuredRenderTime? _marchTime;
    private Vector2I _marchSize;
    private bool _marchShaftsOnly;
    private float? _bottom;
    private float? _top;
    // The layer was fitted to the placed clouds, so the upper zone is no air above it to split the fog by.
    private bool _fitted;
    private float _appliedTaper = -1f;
    private float _appliedShaft = -1f;
    private Node3D? _built;
    private bool _hiding;
    // The layer's top before a storm raises it (PROTOTYPE, clouds-look).
    private float? _baseTop;

    private VolumetricClouds(Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, bool hasDeck)
    {
        Name = "VolumetricClouds";
        _hasDeck = hasDeck;
        _worldRoot = worldRoot;
        _weather = weather;
        _field = field;
        _scene = scene;
        (_bottom, _top) = LayerOf(weather.Mission, volumes);
    }

    /// <summary>Gets the <c>csky_pc_ground</c> global as last written, zero with no clouds drawing. A suite
    /// reads it here, since reading a global back is an engine error outside the editor.</summary>
    public static Vector4 PublishedGround { get; private set; }

    /// <summary>Gets the march's resolution divisor, the Volumetric Clouds word's, read each frame so a
    /// live move re-marches. At 1 the quad marches the layer per pixel, the shafts at half size. At 2
    /// or 4 a half- or quarter-size target marches both.</summary>
    public static int MarchDivisor => VolumetricCloudsSetting.MarchDivisor;

    /// <summary>Gets the reduced march's render time while it runs, else null. Its target is a viewport
    /// of its own, which <c>gpu_ms</c> leaves out (verification PERF-39).</summary>
    public static MeasuredRenderTime? MarchTime { get; private set; }

    // The chapter's cloud tune, the cloud lab's live copy: the heads' taper and the deck's shares.
    private static bool PrototypeOn => _prototype;

    private static CloudShadowPattern.Tune Tune => CloudShadow.Live?.Tune.Clamped() ?? CloudShadowPattern.Tune.Default;

    // The heads' shape the slab and its ground shadow share: taper, edge band, billow, lean.
    private static Vector4 HeadShape => new(Tune.HeadTaper, HeadSoft, WarpM, ShearM);

    // The shafts' haze: a mission authoring rain or snow scatters the sun hard, a dry zone faintly.
    // The zone tune's god rays scale it.
    private float ShaftGain => (_weather.Mission?.Precip != null ? RainShaftGain : HazeShaftGain) * _weather.GodRays;

    private float CoverBias => _hasDeck ? -0.3f : 0.15f;

    private bool Storm => _weather.Mission?.Precip != null;

    /// <summary>The clouds over <paramref name="worldRoot"/>'s world. They hide the deck through
    /// <paramref name="weather"/>, the cloud <paramref name="field"/> and the sprites <paramref name="scene"/>
    /// placed. They draw the slab over the band or <paramref name="volumes"/>, and
    /// <paramref name="hasDeck"/> draws the overcast deck under the heads.</summary>
    public static VolumetricClouds Create(Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, bool hasDeck) =>
        new(worldRoot, weather, field, scene, volumes, hasDeck);

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

    public override void _EnterTree()
    {
        CollectPlacedSprites();
        FitLayerToSprites();
        Follow(GraphicsMode.Enhanced);
    }

    public override void _ExitTree() => Follow(false);

    // PROTOTYPE (clouds-look): Ctrl+Shift+V rebuilds the slab in the other look, for A/B at the controls.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_built == null || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.V, CtrlPressed: true, ShiftPressed: true })
            return;
        _prototype = !_prototype;
        Rebuild();
        Log.Info("world", $"volumetric clouds: look {(_prototype ? "prototype" : "today")}");
        GetViewport().SetInputAsHandled();
    }

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
            vec4 m = vec4(pc_colour(trace.xy, dir, eye), trace.z);
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
            if (l0 <= 1.0) {
                return 0.0;
            }
            return pc_shaft_at(texture(march_texture, vec2(0.5 + x, uv.y)), min(scene, l0) / l0);
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
            float dither = fract(csky_pc_dither(FRAGCOORD.xy) + floor(TIME * 60.0) * 0.6180339) * dither_k;
            if (reduced && !march_shafts_only && !inside) {
                trace = vec4(texture(march_texture, vec2(x, SCREEN_UV.y)).xyz, 0.0);
                // In front of the layer the scene hides it, so the edge stays at this pixel's own depth.
                if (span.y < 0.0 || scene <= span.x) {
                    trace.xyz = vec3(0.0, 0.0, 1.0);
                }
                trace.w = pc_reduced_shafts(SCREEN_UV, x, span, scene);
            } else if (reduced && march_shafts_only) {
                // The shafts are soft haze, so the target serves them; the cloud edges stay per pixel.
                trace = vec4(pc_layer(eye, dir, span, scene, dither), pc_reduced_shafts(SCREEN_UV, x, span, scene));
            } else {
                trace = pc_trace(eye, dir, span, scene, dither);
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

        void fragment() {
            vec3 eye = view_camera[3].xyz;
            // The left half traces the layer and the right half the shafts, each over the whole view.
            // The engine's projection has its y up and the canvas its rows down.
            vec2 uv = vec2(fract(UV.x * 2.0), 1.0 - UV.y);
            vec3 dir = csky_pc_ray_dir(uv, view_inv_projection, view_camera);
            vec2 span = csky_pc_slab_span(eye, dir, layer_bottom, layer_top);
            float dither = fract(csky_pc_dither(FRAGCOORD.xy) + floor(TIME * 60.0) * 0.6180339) * dither_k;
            if (UV.x < 0.5) {
                COLOR = shafts_only ? vec4(0.0, 0.0, 1.0, 0.0) : vec4(pc_layer(eye, dir, span, 1e9, dither), 0.0);
            } else {
                COLOR = pc_shafts(eye, dir, span, 1e9, dither);
            }
        }
        """;

    // The march both passes share: the includes, the layer's uniforms and the march's functions.
    // Its pc_trace returns what a pixel needs before the scene and the fog.
    private static string MarchCode() => """
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_volumetric_clouds.gdshaderinc"
        #include "res://shaders/csky_terrain_shadow.gdshaderinc"

        uniform float layer_bottom = 970.0;
        uniform float layer_top = 1130.0;
        uniform float cover_bias = 0.15;
        uniform float detail_m = 180.0;
        uniform float erosion = 0.55;
        uniform float density_gain = 0.025;
        uniform int steps = 40;
        // The step near the eye: this many metres plus this share of the distance (pc_step). The
        // budget caps a ray's steps.
        uniform float step_near_m = 30.0;
        uniform float step_growth = 0.05;
        uniform int step_budget = 128;
        uniform vec3 sun_rgb = vec3(0.8, 0.77, 0.72);
        uniform float ambient_gain = 0.5;
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
        uniform float shaft_far_m = 6000.0;
        uniform float shaft_height_m = 1500.0;
        // The heads' shape (csky_pc_cover_shaped). `taper` rounds each cloud's crown toward its own
        // top. The sample point billows by `warp_m` and leans `shear_m` downwind at the top.
        uniform float taper = 0.3;
        uniform float head_soft = 0.16;
        uniform float warp_m = 320.0;
        uniform float shear_m = 260.0;

        // PROTOTYPE (clouds-look): look 1 reads the baked Perlin-Worley noise (CloudNoise) and lights
        // the body with a short-stepped sun march and multiple-scattering octaves.
        uniform int look = 0;
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
        uniform float clear_stride = 2.0;
        uniform float cover_stride = 2.0;
        uniform float span_cap_m = 1e9;
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

        // The heads' coverage at `p`, the function the ground shadow reads too.
        float pc_cover_shaped(vec3 p) {
            return csky_pc_cover_shaped(p, layer_bottom, layer_top, detail_m, cover_bias, vec4(taper, head_soft, warp_m, shear_m));
        }

        uniform float fast_cover = 0.0;
        uniform sampler3D warp_lat : filter_linear, repeat_enable;
        uniform sampler2D tops_lat : filter_linear, repeat_enable;

        // csky_pc_noise3 off a lattice texture: the filter between two corners, weighted by the
        // smoothed fraction, is the noise's own interpolation.
        vec2 pc_lat3(vec3 p) {
            vec3 i = floor(p);
            vec3 f = p - i;
            return texture(warp_lat, (i + 0.5 + f * f * (3.0 - 2.0 * f)) / 256.0).rg;
        }

        vec2 pc_lat2(vec2 p) {
            vec2 i = floor(p);
            vec2 f = p - i;
            return texture(tops_lat, (i + 0.5 + f * f * (3.0 - 2.0 * f)) / 256.0).rg;
        }

        // csky_pc_cover_shaped exactly, its warp and each cloud's own top read off the lattices
        // (CloudNoise.WarpLattice, TopsLattice) instead of hashed.
        float pc_cover_fast(vec3 p) {
            float h = clamp((p.y - layer_bottom) / max(layer_top - layer_bottom, 1.0), 0.0, 1.0);
            vec3 w = p / (detail_m * 4.0);
            vec2 warp = vec2(pc_lat3(w).r, pc_lat3(w + 17.0).g) - 0.5;
            vec3 q = p + vec3(warp.x * warp_m * (0.4 + h) + shear_m * h, 0.0, warp.y * warp_m * (0.4 + h));
            // csky_pc_cloud_top's fine noise reads y = 5 m over a 720 m cell: the y = 0 plane is within 0.02 %.
            float broad = pc_lat2(q.xz / (detail_m * 11.0)).r;
            float fine = pc_lat2(q.xz / (detail_m * 4.0)).g;
            float top = mix(0.3, 1.0, smoothstep(0.25, 0.85, broad * 0.75 + fine * 0.25));
            float r = h / top;
            float rc = clamp(r, 0.0, 1.0);
            float rise = taper * pow(rc, 2.5) - taper * 0.25 * sin(3.14159265 * rc) + smoothstep(0.85, 1.0, r) * 0.6;
            float lo = mix(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, 0.5) - cover_bias * 0.25 + rise;
            return smoothstep(lo - head_soft * 0.5, lo + head_soft * 0.5, csky_cloud_noise(q));
        }

        // The heads over the baked noise. The coverage thresholds the Perlin-Worley base, so an edge
        // erodes into billows. The detail noise then frays the base and pocks the crown.
        float pc_heads_pwc(vec3 p, float bottom, float top, bool detail, out float cov) {
            float c = clamp((fast_cover > 0.5 ? pc_cover_fast(p) : pc_cover_shaped(p)) * heads_gain, 0.0, 1.0) * cover_max;
            cov = c;
            if (c <= 0.0) {
                return 0.0;
            }
            float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
            vec3 q = p + vec3(csky_cloud_shadow.x, 0.0, csky_cloud_shadow.y) + vec3(TIME * wind_ms, 0.0, 0.0);
            vec4 s = texture(shape_tex, q / shape_m);
            float wfbm = s.g * 0.625 + s.b * 0.25 + s.a * 0.125;
            float base = clamp(pc_remap(s.r, (wfbm - 1.0) * base_erode, 1.0, 0.0, 1.0), 0.0, 1.0);
            float d = clamp(pc_remap(base * smoothstep(0.0, 0.04 + base_rag * (1.0 - s.g), h), 1.0 - c, 1.0, 0.0, 1.0), 0.0, 1.0);
            if (detail && d > 0.0) {
                vec3 n = texture(detail_tex, q / detail_tile_m).rgb;
                float hf = n.r * 0.625 + n.g * 0.25 + n.b * 0.125;
                float m = mix(hf, 1.0 - hf, clamp(h * 6.0, 0.0, 1.0));
                d = clamp(pc_remap(d, m * detail_erode, 1.0, 0.0, 1.0), 0.0, 1.0);
            }
            return d;
        }

        // The deck over the baked noise: csky_pc_deck_density's sheet, its body the Perlin-Worley
        // shape, so its underside lumps like stratocumulus.
        float pc_deck_pw(vec3 p, float bottom) {
            float deck_top = bottom + deck_depth_m;
            if (deck_cover <= 0.0 || p.y < bottom || p.y > deck_top) {
                return 0.0;
            }
            float h = (p.y - bottom) / max(deck_depth_m, 1.0);
            float prof = smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.55, 1.0, h));
            vec3 q = p + vec3(csky_cloud_shadow.x, 0.0, csky_cloud_shadow.y);
            vec4 s = texture(shape_tex, q / (shape_m * deck_scale));
            float n = s.r * 0.6 + (s.g * 0.625 + s.b * 0.25 + s.a * 0.125) * 0.4;
            float body = clamp((n - (1.0 - deck_cover)) / max(deck_cover * 0.5, 0.05), 0.0, 1.0);
            if (body > 0.0) {
                vec3 dn = texture(detail_tex, q / detail_tile_m).rgb;
                float hf = dn.r * 0.625 + dn.g * 0.25 + dn.b * 0.125;
                body = clamp(pc_remap(body, hf * deck_erode, 1.0, 0.0, 1.0), 0.0, 1.0);
            }
            return body * prof * (pw_density / max(density_gain, 1e-6)) * deck_k;
        }

        float pc_heads_pw(vec3 p, float bottom, float top, bool detail) {
            float cov;
            return pc_heads_pwc(p, bottom, top, detail, cov);
        }

        // The cumulus heads at `p` over the pattern's coverage.
        float pc_heads(vec3 p, float bottom, float top) {
            if (look == 1) {
                return pc_heads_pw(p, bottom, top, true) * (pw_density / max(density_gain, 1e-6));
            }
            float c = pc_cover_shaped(p) * heads_gain;
            return csky_pc_density(p, bottom, top, c, detail_m, erosion);
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
                od += pc_heads_pw(q, bottom, top, i < 2) * pw_density * light_k * dt;
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
        float pc_deck(vec3 p, float bottom) {
            if (look == 1) {
                return pc_deck_pw(p, bottom) * csky_pc_deck_gaps(p, deck_gaps, gap_m);
            }
            float deck = csky_pc_deck_density(p, bottom, bottom + deck_depth_m, deck_cover, detail_m);
            return deck * csky_pc_deck_gaps(p, deck_gaps, gap_m);
        }

        // The whole field at `p`: the heads over the deck.
        float pc_density(vec3 p, float bottom, float top) {
            return max(pc_heads(p, bottom, top), pc_deck(p, bottom));
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
                od += pc_deck(p + csky_sun_dir * (span.x + dt * (float(i) + 0.5)), bottom) * density_gain * dt;
            }
            return od;
        }

        // The sun's share reaching `p`: three taps up the sun ray through the heads, and the deck's span.
        float pc_sun(vec3 p, float step_m, float bottom, float top) {
            float od = pc_deck_od(p, bottom);
            vec3 q = p;
            for (int i = 0; i < 3; i++) {
                q += csky_sun_dir * step_m;
                od += pc_heads(q, bottom, top) * density_gain * step_m;
            }
            return csky_pc_beer_powder(od);
        }

        // The sky's and the sun's weights on the cloud body at `p`, under `sun_t` of the sun. Their
        // colours are the same along a ray, so pc_colour applies them once per pixel.
        vec2 pc_light(vec3 p, float sun_t) {
            // The sky lights the crown and little reaches the base, so the underside reads darker.
            float h = clamp((p.y - layer_bottom) / max(layer_top - layer_bottom, 1.0), 0.0, 1.0);
            // The floor stands in for the multiple scattering a thick cloud's shaded side still gets.
            // It is lower at the base, where a deck's underside and a cumulus base read dark.
            return vec2(mix(0.5, 1.0, h), mix(mix(0.12, 0.25, h), 1.0, sun_t));
        }

        // The lit cloud body from the march's two weights, before fog: the sun through its own shadow
        // plus the sky's ambient. The mission's SUNLIGHT dims it and the zone tune tints it.
        vec3 pc_colour(vec2 w, vec3 dir, vec3 cam) {
            if (look == 1) {
                // The sun's phase is in w.y already (pc_sun_ms); the sky lights the rest bluish.
                vec3 amb = mix(amb_top_rgb, csky_fog_color_at(cam), amb_fog_mix) * amb_gain;
                vec3 sun1 = sun_rgb * max(csky_sun_light.y, 0.35) * sun_gain;
                return (amb * w.x + sun1 * w.y) * csky_world_light_at(cam) * csky_tune_cloud_tint;
            }
            float cos_theta = dot(dir, csky_sun_dir);
            float phase = mix(csky_pc_phase(cos_theta, 0.0), csky_pc_phase(cos_theta, 0.6), 0.5) * 4.0 * 3.14159265;
            vec3 ambient = mix(csky_fog_color_at(cam), vec3(1.0), 0.35) * ambient_gain;
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * mix(0.6, 1.4, phase / (1.0 + phase));
            return (ambient * w.x + sun * w.y) * csky_world_light_at(cam) * csky_tune_cloud_tint;
        }

        // The sun's share reaching a point under the layer. Along the sun ray: the deck's span, then
        // three taps through the heads above it. One where the ray misses the layer.
        float pc_sun_through(vec3 p, float bottom, float top) {
            float od = pc_deck_od(p, bottom);
            vec2 heads = csky_pc_slab_span(p, csky_sun_dir, bottom + deck_depth_m, top);
            if (heads.y > 0.0) {
                float dt = min(heads.y - heads.x, (top - bottom) * 3.0) / 3.0;
                for (int i = 0; i < 3; i++) {
                    od += pc_heads(p + csky_sun_dir * (heads.x + dt * (float(i) + 0.5)), bottom, top) * density_gain * dt;
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
            return min(span_step, step_near_m + t * step_growth);
        }

        // Marches `dir` from `t0` to `t1`, returning the light's weights (pc_light) premultiplied and
        // the transmittance.
        vec3 pc_march(vec3 eye, vec3 dir, float t0, float t1, float dither) {
            float bottom = layer_bottom;
            float top = layer_top;
            float len = t1 - t0;
            int n = max(int(float(steps) * clamp(len / max(top - bottom, 1.0) * 0.5, 0.5, 1.5)), 8);
            float span_step = min(len / float(n), span_cap_m);
            float t = t0 + pc_step(t0, span_step) * dither;
            // The clear air before the first cloud, density alone, in double steps. The search is most
            // of a ray's cost; the bisection's fourth pass restores the surface's precision.
            float clear = t0;
            int i = 0;
            while (i < step_budget && t < t1) {
                vec3 sp = eye + dir * t;
                float cov = 0.0;
                if ((look == 1 ? max(pc_heads_pwc(sp, bottom, top, false, cov), pc_deck(sp, bottom)) : pc_density(sp, bottom, top)) > 0.001) {
                    break;
                }
                clear = t;
                t += pc_step(t, span_step) * (cov > 0.0 ? cover_stride : clear_stride);
                i++;
            }
            if (t >= t1) {
                return vec3(0.0, 0.0, 1.0);
            }
            // Its surface, bisected since the last clear sample, and the first sample half a step past
            // it. The dither then no longer sets how deep the light is read. Outside the loop, so a
            // GPU wave bisects once.
            float hi = t;
            for (int k = 0; k < 4; k++) {
                float mid = (clear + hi) * 0.5;
                if (pc_density(eye + dir * mid, bottom, top) > 0.001) {
                    hi = mid;
                } else {
                    clear = mid;
                }
            }
            t = min((clear + hi) * 0.5 + pc_step(hi, span_step) * 0.5, t1);
            vec2 w = vec2(0.0);
            float trans = 1.0;
            float sun_step = max((top - bottom) * 0.3, 20.0);
            float cos_theta = dot(dir, csky_sun_dir);
            while (i < step_budget && trans > 0.01 && t < t1) {
                float dt = pc_step(t, span_step);
                vec3 p = eye + dir * t;
                float d = pc_density(p, bottom, top);
                if (d > 0.001) {
                    float ext = d * density_gain * dt;
                    float a = 1.0 - exp(-ext);
                    if (look == 1) {
                        float od = pc_sun_od_pw(p, bottom, top) + pc_deck_od(p, bottom);
                        float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
                        float powder = mix(1.0, 1.0 - exp(-d * density_gain * 60.0), powder_k);
                        float amb = mix(amb_floor, 1.0, h) * mix(1.0, exp(-od * amb_od_k), amb_occ);
                        w += trans * a * vec2(amb, pc_sun_ms(od, cos_theta) * powder);
                    } else {
                        float sun_t = pc_sun(p, sun_step, bottom, top);
                        w += trans * a * pc_light(p, sun_t);
                    }
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
        vec3 pc_layer(vec3 eye, vec3 dir, vec2 span, float scene, float dither) {
            if (span.y >= 0.0) {
                float t1 = pc_march_end(span, scene);
                if (t1 > span.x) {
                    return pc_march(eye, dir, span.x, t1, dither);
                }
            }
            return vec3(0.0, 0.0, 1.0);
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
        vec4 pc_trace(vec3 eye, vec3 dir, vec2 span, float scene, float dither) {
            return vec4(pc_layer(eye, dir, span, scene, dither), pc_shafts(eye, dir, span, scene, dither).w);
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
        material.SetShaderParameter("heads_gain", _hasDeck ? 1.15f : 1f);
        // A deck tears open in rifts and the sun falls through them in shafts. The shafts are strong
        // where the mission authors rain and faint in a dry haze.
        material.SetShaderParameter("deck_gaps", _hasDeck ? DeckGaps : 0f);
        material.SetShaderParameter("gap_m", GapM);
        material.SetShaderParameter("shaft_gain", ShaftGain);
        ApplyPrototype(material);
        return material;
    }

    // PROTOTYPE (clouds-look): CSVM_CLOUD_LOOK=1 turns the baked-noise look on, and CSVM_CLOUD_SET
    // ("name=v;name=a,b,c") overrides any float or vec3 uniform, for tuning without a rebuild.
    private void ApplyPrototype(ShaderMaterial material)
    {
        if (!PrototypeOn)
            return;
        material.SetShaderParameter("look", 1);
        material.SetShaderParameter("shape_tex", CloudNoise.Shape);
        material.SetShaderParameter("detail_tex", CloudNoise.Detail);
        material.SetShaderParameter("warp_lat", CloudNoise.WarpLattice);
        material.SetShaderParameter("tops_lat", CloudNoise.TopsLattice);
        string set = (Storm ? StormPreset : _hasDeck ? DeckPreset : ClearPreset) + ";" + (System.Environment.GetEnvironmentVariable("CSVM_CLOUD_SET") ?? string.Empty);
        foreach (var pair in set.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=');
            var parts = kv[1].Split(',');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (parts.Length == 3)
            {
                material.SetShaderParameter(kv[0].Trim(), new Vector3(float.Parse(parts[0], inv), float.Parse(parts[1], inv), float.Parse(parts[2], inv)));
            }
            else
            {
                material.SetShaderParameter(kv[0].Trim(), float.Parse(parts[0], inv));
                _protoValues[kv[0].Trim()] = float.Parse(parts[0], inv);
            }
        }
        RenderingServer.GlobalShaderParameterSet("csky_pc_shape_tex", CloudNoise.Shape);
        RenderingServer.GlobalShaderParameterSet("csky_pc_warp_lat", CloudNoise.WarpLattice);
        RenderingServer.GlobalShaderParameterSet("csky_pc_tops_lat", CloudNoise.TopsLattice);
        Log.Info("world", $"volumetric clouds: prototype look 1, set [{set}]");
    }

    // A prototype uniform as the slab stands, else the shader's default.
    private float Proto(string name, float fallback) => _protoValues.TryGetValue(name, out float v) ? v : fallback;

    private void Rebuild()
    {
        FreeBuilt();
        _baseTop ??= _top;
        if (_bottom is { } floor && _baseTop is { } roof)
            _top = PrototypeOn && Storm ? Mathf.Max(roof, floor + StormDepthM) : roof;
        var root = new Node3D { Name = "volumetric_slab" };
        string detail = BuildSlab(root);
        _built = root;
        AddChild(root);
        string layer = _bottom is { } b && _top is { } t ? $"layer {b:0}-{t:0} m" : "no cloud band";
        Log.Info("world", $"volumetric clouds: {layer}, deck cover {(_hasDeck ? DeckCover : 0f):0.00} gaps {(_hasDeck ? DeckGaps : 0f):0.00}, shafts {ShaftGain:0.0}, {detail}, hidden: deck={(_weather.HideDeck ? "yes" : "no")} field={(_field is { } f && !f.Visible ? "yes" : "no")} sprites={_hidden.Count}");
    }

    private string BuildSlab(Node3D root)
    {
        if (_bottom is not { } bottom || _top is not { } top)
            return "nothing drawn";
        _material = MaterialOf(SlabShader(), bottom, top);
        // Over the world's transparents and the flare, under the rain in front of the layer and the stereo HUD tiers.
        _material.RenderPriority = SlabPriority;
        // The puffers and weapon sprites draw after it, each dimmed by the slab's transmittance to it (csky_pc_veil_at). One in
        // front of a cloud covers it, and one behind shows only through its gaps. The rain draws last.
        MultiMeshEmitterRenderer.DrawPriority = SlabPriority + 1;
        Precipitation.DrawPriority = Precipitation.OverSlabPriority;
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
        var viewport = GetViewport();
        var camera = viewport.GetCamera3D();
        bool shaftsOnly = MarchDivisor == 1;
        int divisor = shaftsOnly ? ShaftDivisor : MarchDivisor;
        bool on = camera != null && !viewport.UseXR && (!shaftsOnly || ShaftGain > 0f);
        var full = (Vector2I)viewport.GetVisibleRect().Size;
        // Two halves side by side: the layer's trace, then the shafts' (MarchShader).
        var size = on ? new Vector2I(Math.Max((full.X + divisor - 1) / divisor, 1) * 2, Math.Max((full.Y + divisor - 1) / divisor, 1)) : Vector2I.Zero;
        if (size != _marchSize || shaftsOnly != _marchShaftsOnly)
        {
            _marchSize = size;
            _marchShaftsOnly = shaftsOnly;
            _marchView.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            if (on)
                _marchView.Size = size;
            _material.SetShaderParameter(MarchOnParam, on);
            _material.SetShaderParameter(MarchShaftsOnlyParam, shaftsOnly);
            _march.SetShaderParameter(ShaftsOnlyParam, shaftsOnly);
            MarchTime = on ? _marchTime : null;
            string what = !on ? "full resolution" : shaftsOnly ? $"full resolution, shafts at 1/{divisor}, {size.X / 2}x{size.Y}" : $"1/{divisor} resolution, {size.X / 2}x{size.Y}";
            Log.Info("world", $"volumetric clouds: march at {what}");
        }
        if (!on)
            return;
        var view = camera!.GetCameraTransform();
        var inverse = camera.GetCameraProjection().Inverse();
        _march.SetShaderParameter(ViewCameraParam, new Projection(view));
        _march.SetShaderParameter(ViewInvProjectionParam, inverse);
        _material.SetShaderParameter(MarchEyeParam, view.Origin);
        _material.SetShaderParameter(MarchBackParam, view.Basis.Z);
        _material.SetShaderParameter(MarchLensParam, new Vector2(Mathf.Abs(inverse.X.X), Mathf.Abs(inverse.Y.Y)));
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
            Precipitation.DrawPriority = 0;
        }
        _material = null;
        _march = null;
        _marchView = null;
        _marchTime = null;
        _marchSize = Vector2I.Zero;
        _marchShaftsOnly = false;
        MarchTime = null;
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
            RenderingServer.GlobalShaderParameterSet("csky_pc_ground", Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet("csky_pc_ground_sun", new Vector4(0f, 1f, 0f, 0f));
            RenderingServer.GlobalShaderParameterSet("csky_pc_fog", Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet("csky_pc_veil", Vector4.Zero);
            RenderingServer.GlobalShaderParameterSet("csky_pc_pw", Vector4.Zero);
            return;
        }
        // The layer the effects drawn after the slab march their transmittance through (csky_pc_veil_at).
        RenderingServer.GlobalShaderParameterSet("csky_pc_veil", new Vector4(bottom, top, _hasDeck ? DeckCover : 0f, DeckDepthM));
        // The fog splits at the layer: the held lower zone below it, the upper zone's range above it.
        if (_weather.Mission is { } mission && !_fitted)
        {
            var upper = WeatherRig.FogRangeFor(mission.Zone(mission.ZoneForState(2)));
            RenderingServer.GlobalShaderParameterSet("csky_pc_fog", new Vector4(bottom, upper.X, upper.Y, 0f));
        }
        else
        {
            RenderingServer.GlobalShaderParameterSet("csky_pc_fog", Vector4.Zero);
        }
        var sun = _weather.SunDirection;
        var tune = Tune;
        float deckY = bottom + DeckDepthM * 0.4f;
        PublishedGround = new Vector4(deckY, _hasDeck ? tune.DeckSunTaken : 0f, DeckGaps, GapM);
        RenderingServer.GlobalShaderParameterSet("csky_pc_ground", PublishedGround);
        RenderingServer.GlobalShaderParameterSet("csky_pc_ground_sun",
            new Vector4(sun.X, sun.Y, sun.Z, bottom + (top - bottom) * 0.3f));
        bool proto = PrototypeOn && _material != null;
        var shape = HeadShape;
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_shape", proto ? shape with { Y = Proto("head_soft", HeadSoft) } : shape);
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_layer", new Vector4(bottom, top, DetailM, CoverBias));
        // The slab's heads_gain and its erosion and density_gain defaults, so the ground marches its
        // density, and the ambient share the deck takes.
        float headsGain = _hasDeck ? 1.15f : 1f;
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_density",
            new Vector4(proto ? Proto("heads_gain", headsGain) : headsGain, 0.55f, 0.025f, _hasDeck ? tune.DeckAmbientTaken : 0f));
        RenderingServer.GlobalShaderParameterSet("csky_pc_pw", proto
            ? new Vector4(Proto("shape_m", 3000f), Proto("cover_max", 1f), Proto("base_rag", 0f), Proto("pw_density", 0.04f))
            : Vector4.Zero);
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
