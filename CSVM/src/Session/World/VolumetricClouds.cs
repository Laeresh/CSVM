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
/// the mission's cloud band, over the cloud shadows' own pattern. It draws cumulus heads over an
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
    // The share of a deck torn open and the tears' size, and the shafts' haze under rain or a dry zone.
    private const float DeckGaps = 0.35f;
    private const float GapM = 3200f;
    // The deck's depth (the shader's deck_depth_m) and the sun share it takes from the ground under it.
    private const float DeckDepthM = 150f;
    // The heads' edge band, billow and lean (the shader's head_soft, warp_m, shear_m), and erosion cell.
    private const float HeadSoft = 0.16f;
    private const float WarpM = 320f;
    private const float ShearM = 260f;
    private const float DetailM = 180f;
    private const float RainShaftGain = 1f;
    private const float HazeShaftGain = 0.3f;
    // How far above the highest placed cloud an authored band may sit and still count as the layer.
    private const float SpriteBandMarginM = 1000f;

    private readonly Node3D _worldRoot;
    private readonly WeatherRig _weather;
    private readonly FogVolumeClutter? _field;
    private readonly SceneBuilder? _scene;
    private readonly List<(MeshInstance3D Node, Aabb World)> _placedSprites = new();
    private readonly List<MeshInstance3D> _hidden = new();
    private readonly bool _hasDeck;
    private ShaderMaterial? _material;
    private float? _bottom;
    private float? _top;
    // The layer was fitted to the placed clouds, so the upper zone is no air above it to split the fog by.
    private bool _fitted;
    private float _appliedTaper = -1f;
    private float _appliedShaft = -1f;
    private Node3D? _built;
    private bool _hiding;

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

    // The chapter's cloud tune, the cloud lab's live copy: the heads' taper and the deck's shares.
    private static CloudShadowPattern.Tune Tune => CloudShadow.Live?.Tune.Clamped() ?? CloudShadowPattern.Tune.Default;

    // The heads' shape the slab and its ground shadow share: taper, edge band, billow, lean.
    private static Vector4 HeadShape => new(Tune.HeadTaper, HeadSoft, WarpM, ShearM);

    // The shafts' haze: a mission authoring rain or snow scatters the sun hard, a dry zone faintly.
    // The zone tune's god rays scale it.
    private float ShaftGain => (_weather.Mission?.Precip != null ? RainShaftGain : HazeShaftGain) * _weather.GodRays;

    private float CoverBias => _hasDeck ? -0.3f : 0.15f;

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

    // One full-screen quad, the cloud shadows' pattern as the layer's coverage.
    private static string SlabShader() => """
        shader_type spatial;
        render_mode blend_premul_alpha, unshaded, depth_draw_never, shadows_disabled, fog_disabled, cull_disabled, depth_test_disabled;
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_volumetric_clouds.gdshaderinc"
        #include "res://shaders/csky_terrain_shadow.gdshaderinc"

        uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
        uniform float layer_bottom = 970.0;
        uniform float layer_top = 1130.0;
        uniform float cover_bias = 0.15;
        uniform float detail_m = 180.0;
        uniform float erosion = 0.55;
        uniform float density_gain = 0.025;
        uniform int steps = 40;
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
        // How far from the zone fog colour toward white the sky above the layer stands, seen
        // through the rifts; 0 leaves the Environment's panorama.
        uniform float rift_sky_lift = 0.0;
        // The heads' shape (csky_pc_cover_shaped). `taper` rounds each cloud's crown toward its own
        // top. The sample point billows by `warp_m` and leans `shear_m` downwind at the top.
        uniform float taper = 0.3;
        uniform float head_soft = 0.16;
        uniform float warp_m = 320.0;
        uniform float shear_m = 260.0;

        // The heads' coverage at `p`, the function the ground shadow reads too.
        float pc_cover_shaped(vec3 p) {
            return csky_pc_cover_shaped(p, layer_bottom, layer_top, detail_m, cover_bias, vec4(taper, head_soft, warp_m, shear_m));
        }

        // The cumulus heads at `p` over the pattern's coverage.
        float pc_heads(vec3 p, float bottom, float top, float cover_scale) {
            float c = pc_cover_shaped(p) * heads_gain * cover_scale;
            return csky_pc_density(p, bottom, top, c, detail_m, erosion);
        }

        // The deck at `p`, torn by the rifts.
        float pc_deck(vec3 p, float bottom) {
            float deck = csky_pc_deck_density(p, bottom, bottom + deck_depth_m, deck_cover, detail_m);
            return deck * csky_pc_deck_gaps(p, deck_gaps, gap_m);
        }

        // The whole field at `p`: the heads over the deck.
        float pc_density(vec3 p, float bottom, float top, float cover_scale) {
            return max(pc_heads(p, bottom, top, cover_scale), pc_deck(p, bottom));
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
        float pc_sun(vec3 p, float step_m, float bottom, float top, float cover_scale) {
            float od = pc_deck_od(p, bottom);
            vec3 q = p;
            for (int i = 0; i < 3; i++) {
                q += csky_sun_dir * step_m;
                od += pc_heads(q, bottom, top, cover_scale) * density_gain * step_m;
            }
            return csky_pc_beer_powder(od);
        }

        // The lit cloud body at `p`, before fog: the sun through its own shadow plus the sky's
        // ambient. The mission's SUNLIGHT dims it and the zone tune tints it.
        vec3 pc_light(vec3 p, vec3 dir, float sun_t, vec3 cam) {
            float cos_theta = dot(dir, csky_sun_dir);
            float phase = mix(csky_pc_phase(cos_theta, 0.0), csky_pc_phase(cos_theta, 0.6), 0.5) * 4.0 * 3.14159265;
            // The sky lights the crown and little reaches the base, so the underside reads darker.
            float h = clamp((p.y - layer_bottom) / max(layer_top - layer_bottom, 1.0), 0.0, 1.0);
            vec3 ambient = mix(csky_fog_color_at(cam), vec3(1.0), 0.35) * ambient_gain * mix(0.5, 1.0, h);
            // The floor stands in for the multiple scattering a thick cloud's shaded side still gets.
            // It is lower at the base, where a deck's underside and a cumulus base read dark.
            float lit = mix(mix(0.12, 0.25, h), 1.0, sun_t);
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * lit * mix(0.6, 1.4, phase / (1.0 + phase));
            return (ambient + sun) * csky_world_light_at(cam) * csky_tune_cloud_tint;
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

        // The light shafts under the layer: the sun in-scattered toward the eye by the haze below the
        // deck. The march runs from the eye to `t_end`, each sample shadowed by the layer along the
        // sun ray. The haze thins with height and the zone fog swallows the far samples.
        vec3 pc_shafts(vec3 eye, vec3 dir, float t_end, float dither) {
            float len = min(t_end, shaft_far_m);
            if (len <= 1.0) {
                return vec3(0.0);
            }
            float dt = len / float(shaft_steps);
            float phase = csky_pc_phase(dot(dir, csky_sun_dir), 0.5) * 4.0 * 3.14159265;
            float light = 0.0;
            float t = dt * dither;
            for (int i = 0; i < shaft_steps; i++) {
                vec3 q = eye + dir * t;
                float haze = exp(-max(q.y, 0.0) / shaft_height_m);
                float reach = 1.0 - csky_fog_amount(q, eye);
                // The terrain's baked sun shadow holds in the air too, so a hill's lee carries no shaft.
                float sun_t = pc_sun_through(q, layer_bottom, layer_top) * csky_terrain_baked_sun(q);
                light += sun_t * haze * reach * dt;
                t += dt;
            }
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * csky_world_light_at(eye);
            return sun * light * shaft_gain * 0.00006 * phase;
        }

        // Marches `dir` from `t0` to `t1`, returning the lit colour premultiplied and the transmittance.
        vec4 pc_march(vec3 eye, vec3 dir, float t0, float t1, float bottom, float top, float cover_scale, float dither) {
            float len = t1 - t0;
            int n = max(int(float(steps) * clamp(len / max(top - bottom, 1.0) * 0.5, 0.5, 1.5)), 8);
            float dt = len / float(n);
            float t = t0 + dt * dither;
            vec3 col = vec3(0.0);
            float trans = 1.0;
            float sun_step = max((top - bottom) * 0.3, 20.0);
            for (int i = 0; i < n && trans > 0.01; i++) {
                vec3 p = eye + dir * t;
                float d = pc_density(p, bottom, top, cover_scale);
                if (d > 0.001) {
                    float ext = d * density_gain * dt;
                    float a = 1.0 - exp(-ext);
                    float sun_t = pc_sun(p, sun_step, bottom, top, cover_scale);
                    col += trans * a * pc_light(p, dir, sun_t, eye);
                    trans *= 1.0 - a;
                }
                t += dt;
            }
            return vec4(col, trans);
        }

        // Fogs the march's result at the entry point and writes it premultiplied. The light shafts in
        // front of it add on top, and `behind`, premultiplied over its alpha, shows under it.
        void pc_write(vec4 marched, vec3 entry, vec3 cam, vec3 shafts, vec4 behind, inout vec3 albedo, inout float alpha) {
            float a = 1.0 - marched.a;
            alpha = a + (1.0 - a) * behind.a;
            if (alpha <= 0.002 && max(shafts.r, max(shafts.g, shafts.b)) <= 0.0005) {
                discard;
            }
            float fog = max(csky_fog_amount(entry, cam), smoothstep(haze_near_m, far_m, distance(entry, cam)));
            albedo = mix(marched.rgb, csky_fog_color_at(cam) * a, fog) + shafts + (1.0 - a) * behind.rgb;
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
            // The march's start offset moves by the golden ratio every frame. TAA then averages the
            // pattern out, where a fixed one leaves a hatch wherever the steps are coarse.
            float dither = fract(csky_pc_dither(FRAGCOORD.xy) + floor(TIME * 60.0) * 0.6180339);
            vec4 m = vec4(0.0, 0.0, 0.0, 1.0);
            float t0 = 0.0;
            if (span.y >= 0.0) {
                t0 = span.x;
                float t1 = min(min(span.y, scene), t0 + far_m);
                if (t1 > t0) {
                    m = pc_march(eye, dir, t0, t1, layer_bottom, layer_top, 1.0, dither);
                }
                // A march cut off inside the layer leaves the rest of it to the fog, not the dome, by
                // the layer left unmarched. A ray leaving just past the cap takes little.
                float unmarched = min(span.y, scene) - t1;
                if (unmarched > 1.0) {
                    float rest = max(csky_fog_amount(eye + dir * t1, eye), smoothstep(haze_near_m, far_m, t1))
                        * (1.0 - exp(-unmarched / 5000.0));
                    m.rgb += m.a * rest * csky_fog_color_at(eye);
                    m.a *= 1.0 - rest;
                }
            }
            // Under the layer the ray crosses the haze first, up to the layer, the scene or the cap.
            vec3 shafts = vec3(0.0);
            if (shaft_gain > 0.0 && eye.y < layer_bottom) {
                shafts = pc_shafts(eye, dir, min(scene, span.y >= 0.0 ? span.x : shaft_far_m), dither);
            }
            // A sky pixel whose ray leaves through the layer's top shows the lifted sky above it.
            // The exit's fog closes the far rifts into the horizon.
            vec4 behind = vec4(0.0);
            // At or below the horizon, a sky pixel past the clipped sea shows the fog, not the dome's lower band.
            if (scene >= 1e8 && span.y < 0.0 && dir.y <= 0.02) {
                behind = vec4(csky_fog_color_at(eye), 1.0);
            }
            if (rift_sky_lift > 0.0 && scene >= 1e8 && span.y >= 0.0 && dir.y > 0.0 && eye.y < layer_top) {
                vec3 fog_rgb = csky_fog_color_at(eye);
                float exit_fog = csky_fog_amount(eye + dir * span.y, eye);
                behind = vec4(mix(mix(fog_rgb, vec3(1.0), rift_sky_lift), fog_rgb, exit_fog), 1.0);
            }
            vec3 albedo = vec3(0.0);
            float alpha = 0.0;
            pc_write(m, eye + dir * t0, eye, shafts, behind, albedo, alpha);
            ALBEDO = albedo;
            ALPHA = alpha;
        }
        """;

    private ShaderMaterial MaterialOf(float bottom, float top)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = SlabShader() }, RenderPriority = 120 };
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
        // Off: the held upper zone's dome is the sky a rift shows (WeatherRig.HoldZone).
        material.SetShaderParameter("rift_sky_lift", 0f);
        return material;
    }

    private void Rebuild()
    {
        FreeBuilt();
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
        _material = MaterialOf(bottom, top);
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
        return "one full-screen march";
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
        _material = null;
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
            return;
        }
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
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_shape", HeadShape);
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_layer", new Vector4(bottom, top, DetailM, CoverBias));
        // The slab's heads_gain and its erosion and density_gain defaults, so the ground marches its
        // density, and the ambient share the deck takes.
        RenderingServer.GlobalShaderParameterSet("csky_pc_heads_density",
            new Vector4(_hasDeck ? 1.15f : 1f, 0.55f, 0.025f, _hasDeck ? tune.DeckAmbientTaken : 0f));
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
        _material.SetShaderParameter("taper", taper);
        _material.SetShaderParameter("shaft_gain", shaft);
    }
}
