using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// PROTOTYPE, throwaway: volumetric clouds replacing the cloud deck, the fogvol field and the placed
/// cloud sprites under Enhanced alone (the flag <c>--proto-clouds</c>, Shift+F6 cycles).
/// Three structurally different variants answer which technique to build for real. The slab variant
/// is one full-screen raymarch through the mission's cloud band, over the cloud shadows' own pattern.
/// The boxes variant raymarches each fvol volume and each placed cloud in its own proxy box, over a
/// puff map of the sprites' positions. The shells variant is a stack of noise-eroded sheets through
/// the band, no march, the Steam Deck candidate. Every variant reads the depth buffer so terrain
/// and aircraft occlude it, nothing is tuned or golden-pinned, and the faithful path builds none.
/// </summary>
internal sealed partial class ProtoClouds : Node3D
{
    /// <summary>The default variant, the one a bare <c>--proto-clouds</c> draws.</summary>
    public const string DefaultWord = "slab";

    // The authored band is the whiteout's thin sheet. The cumulus layer stands this much taller
    // over the same floor, so the heads have room to tower.
    private const float LayerDepthGain = 3f;
    private const float LayerMinDepthM = 600f;
    private const int ShellCount = 24;
    private const float ShellSizeM = 36000f;
    private const float PuffMapSize = 128;
    private const float PuffRadiusM = 170f;
    private const float PlacedCloudDepthShare = 0.8f;
    private const float FallbackHalfExtentM = 20000f;

    private static readonly string Inv = CultureInfo.InvariantCulture.Name;

    private readonly Node3D _worldRoot;
    private readonly WeatherRig _weather;
    private readonly FogVolumeClutter? _field;
    private readonly SceneBuilder? _scene;
    private readonly IReadOnlyList<FogVolumeBox> _volumes;
    private readonly Func<Camera3D?> _camera;
    private readonly List<(MeshInstance3D Node, Aabb World)> _placedSprites = new();
    private readonly List<MeshInstance3D> _hidden = new();
    private readonly float? _bottom;
    private readonly float? _top;
    private Variant _variant;
    private Node3D? _built;
    private bool _hiding;

    private ProtoClouds(Variant variant, Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, Func<Camera3D?> camera)
    {
        Name = "ProtoClouds";
        _variant = variant;
        _worldRoot = worldRoot;
        _weather = weather;
        _field = field;
        _scene = scene;
        _volumes = volumes;
        _camera = camera;
        (_bottom, _top) = LayerOf(weather.Mission, volumes);
    }

    /// <summary>The three techniques under comparison.</summary>
    public enum Variant
    {
        Slab,
        Boxes,
        Shells,
    }

    /// <summary>The variant drawing now.</summary>
    public Variant Current => _variant;

    /// <summary>The flag's word as a variant, or null for a word that names none.</summary>
    public static Variant? Parse(string word) => word.Trim().ToLowerInvariant() switch
    {
        "" or "slab" => Variant.Slab,
        "boxes" => Variant.Boxes,
        "shells" => Variant.Shells,
        _ => null,
    };

    /// <summary>The prototype over <paramref name="worldRoot"/>'s world: it hides the deck through
    /// <paramref name="weather"/>, the cloud <paramref name="field"/> and the sprites <paramref name="scene"/>
    /// placed, and draws <paramref name="variant"/> over the band or <paramref name="volumes"/>. The shells
    /// follow <paramref name="camera"/>.</summary>
    public static ProtoClouds Create(Variant variant, Node3D worldRoot, WeatherRig weather, FogVolumeClutter? field,
        SceneBuilder? scene, IReadOnlyList<FogVolumeBox> volumes, Func<Camera3D?> camera) =>
        new(variant, worldRoot, weather, field, scene, volumes, camera);

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
        Follow(GraphicsMode.Enhanced);
    }

    public override void _ExitTree() => Follow(false);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F6, ShiftPressed: true })
            return;
        _variant = (Variant)(((int)_variant + 1) % 3);
        Rebuild();
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        // The shells ride the eye in X/Z, so the stack never shows an edge; Y is the layer's own.
        if (_built == null || _variant != Variant.Shells || _camera() is not { } cam)
            return;
        var eye = cam.GlobalPosition;
        _built.GlobalPosition = new Vector3(eye.X, 0f, eye.Z);
    }

    /// <summary>The graphics switch: Enhanced draws the variant and hides the originals, the faithful
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
        SetProcess(enhanced);
    }

    private static string F(float v) => v.ToString("0.0###", CultureInfo.InvariantCulture);

    private static string SharedHead(string extraRenderModes) => $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, depth_draw_never, shadows_disabled, fog_disabled{{extraRenderModes}};
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_proto_clouds.gdshaderinc"

        uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
        uniform float layer_bottom = 970.0;
        uniform float layer_top = 1130.0;
        uniform float cover_bias = 0.15;
        uniform float detail_m = 180.0;
        uniform float erosion = 0.55;
        uniform float density_gain = 0.025;
        uniform int steps = 40;
        uniform vec3 sun_rgb = vec3(1.0, 0.96, 0.9);
        uniform float ambient_gain = 0.6;
        uniform float far_m = 24000.0;
        """;

    // The march, its lighting and the write, shared by every variant after its own PC_COVER.
    private static string SharedFunctions() => """
        // The sun's share reaching `p`, three taps up the sun ray inside the layer.
        float pc_sun(vec3 p, float step_m, float bottom, float top, float cover_scale) {
            float od = 0.0;
            vec3 q = p;
            for (int i = 0; i < 3; i++) {
                q += csky_sun_dir * step_m;
                float c = PC_COVER(q) * cover_scale;
                od += csky_pc_density(q, bottom, top, c, detail_m, erosion) * density_gain * step_m;
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
            float lit = mix(0.25, 1.0, sun_t);
            vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * lit * mix(0.6, 1.4, phase / (1.0 + phase));
            return (ambient + sun) * csky_world_light_at(cam) * csky_tune_cloud_tint;
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
                float c = PC_COVER(p) * cover_scale;
                float d = csky_pc_density(p, bottom, top, c, detail_m, erosion);
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

        // Fogs the march's result at the entry point and writes it as a mixed surface.
        void pc_write(vec4 marched, vec3 entry, vec3 cam, inout vec3 albedo, inout float alpha) {
            float a = 1.0 - marched.a;
            if (a <= 0.002) {
                discard;
            }
            vec3 col = marched.rgb / a;
            float fog = csky_fog_amount(entry, cam);
            albedo = mix(col, csky_fog_color_at(cam), fog);
            alpha = a;
        }
        """;

    // Variant A: one full-screen quad, the cloud shadows' pattern as the layer's coverage.
    private static string SlabShader() => $$"""
        #define PC_COVER(p) (csky_cloud_cover(p) + cover_bias)
        {{SharedHead(", cull_disabled, depth_test_disabled")}}
        {{SharedFunctions()}}

        void vertex() {
            POSITION = vec4(VERTEX.xy, 1.0, 1.0);
        }

        void fragment() {
            vec3 eye = CAMERA_POSITION_WORLD;
            vec3 dir = csky_pc_ray_dir(SCREEN_UV, INV_PROJECTION_MATRIX, INV_VIEW_MATRIX);
            float scene = csky_pc_scene_distance(texture(depth_texture, SCREEN_UV).r, SCREEN_UV, INV_PROJECTION_MATRIX);
            vec2 span = csky_pc_slab_span(eye, dir, layer_bottom, layer_top);
            if (span.y < 0.0) {
                discard;
            }
            float t0 = span.x;
            float t1 = min(min(span.y, scene), t0 + far_m);
            if (t1 <= t0) {
                discard;
            }
            vec4 m = pc_march(eye, dir, t0, t1, layer_bottom, layer_top, 1.0, csky_pc_dither(FRAGCOORD.xy));
            vec3 albedo = vec3(0.0);
            float alpha = 0.0;
            pc_write(m, eye + dir * t0, eye, albedo, alpha);
            ALBEDO = albedo;
            ALPHA = alpha;
        }
        """;

    // Variant B: one proxy box per authored volume or placed cloud, its puff map as the coverage.
    private static string BoxesShader() => $$"""
        {{SharedHead(", cull_front, depth_test_disabled")}}
        uniform sampler2D puff_map : filter_linear, repeat_disable;
        uniform vec3 box_min = vec3(-1000.0, 970.0, -1000.0);
        uniform vec3 box_max = vec3(1000.0, 1130.0, 1000.0);
        uniform float puff_gain = 1.0;

        float pc_puff(vec3 p) {
            vec2 uv = (p.xz - box_min.xz) / max(box_max.xz - box_min.xz, vec2(1.0));
            return texture(puff_map, uv).r * puff_gain;
        }
        #define PC_COVER(p) pc_puff(p)
        {{SharedFunctions()}}

        varying vec3 v_world;

        void vertex() {
            v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        void fragment() {
            vec3 eye = CAMERA_POSITION_WORLD;
            vec3 dir = normalize(v_world - eye);
            float scene = csky_pc_scene_distance(texture(depth_texture, SCREEN_UV).r, SCREEN_UV, INV_PROJECTION_MATRIX);
            vec2 span = csky_pc_box_span(eye, dir, box_min, box_max);
            if (span.y < 0.0) {
                discard;
            }
            float t0 = span.x;
            float t1 = min(span.y, scene);
            if (t1 <= t0) {
                discard;
            }
            vec4 m = pc_march(eye, dir, t0, t1, box_min.y, box_max.y, 1.0, csky_pc_dither(FRAGCOORD.xy));
            vec3 albedo = vec3(0.0);
            float alpha = 0.0;
            pc_write(m, eye + dir * t0, eye, albedo, alpha);
            ALBEDO = albedo;
            ALPHA = alpha;
        }
        """;

    // Variant C: horizontal sheets through the band, each shaded at its own altitude, no march.
    private static string ShellsShader() => $$"""
        #define PC_COVER(p) (csky_cloud_cover(p) + cover_bias)
        {{SharedHead(", cull_disabled")}}
        {{SharedFunctions()}}
        uniform float shell_thickness_m = 12.0;
        uniform float near_fade_m = 60.0;

        varying vec3 v_world;

        void vertex() {
            v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        void fragment() {
            vec3 eye = CAMERA_POSITION_WORLD;
            vec3 p = v_world;
            float c = PC_COVER(p);
            float d = csky_pc_density(p, layer_bottom, layer_top, c, detail_m, erosion);
            float a = 1.0 - exp(-d * density_gain * shell_thickness_m);
            float dist = distance(p, eye);
            a *= smoothstep(0.0, near_fade_m, dist);
            if (a <= 0.002) {
                discard;
            }
            vec3 dir = normalize(p - eye);
            float sun_t = pc_sun(p, max((layer_top - layer_bottom) * 0.3, 20.0), layer_bottom, layer_top, 1.0);
            vec3 col = pc_light(p, dir, sun_t, eye);
            float fog = csky_fog_amount(p, eye);
            ALBEDO = mix(col, csky_fog_color_at(eye), fog);
            ALPHA = a;
        }
        """;

    private static ShaderMaterial MaterialOf(string code, float bottom, float top)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = code }, RenderPriority = 120 };
        material.SetShaderParameter("layer_bottom", bottom);
        material.SetShaderParameter("layer_top", top);
        return material;
    }

    private static Aabb Grow(Aabb box, float margin) => box.Grow(margin);

    // A top-down coverage map of `box`: a soft disc per sprite inside it, summed and clamped.
    private static ImageTexture PuffMap(Aabb box, IReadOnlyList<Vector3> sprites, float yMargin)
    {
        int size = (int)PuffMapSize;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rf);
        var grown = Grow(box, yMargin);
        float cellX = box.Size.X / size, cellZ = box.Size.Z / size;
        float radius = Mathf.Max(PuffRadiusM, Mathf.Min(box.Size.X, box.Size.Z) * 0.08f);
        var values = new float[size * size];
        foreach (var s in sprites)
        {
            if (!grown.HasPoint(new Vector3(s.X, Mathf.Clamp(s.Y, grown.Position.Y, grown.End.Y), s.Z)))
                continue;
            float fx = (s.X - box.Position.X) / Mathf.Max(cellX, 1e-3f);
            float fz = (s.Z - box.Position.Z) / Mathf.Max(cellZ, 1e-3f);
            int reachX = (int)Mathf.Ceil(radius / Mathf.Max(cellX, 1e-3f));
            int reachZ = (int)Mathf.Ceil(radius / Mathf.Max(cellZ, 1e-3f));
            int cx = (int)fx, cz = (int)fz;
            for (int z = Mathf.Max(cz - reachZ, 0); z <= Mathf.Min(cz + reachZ, size - 1); z++)
            {
                for (int x = Mathf.Max(cx - reachX, 0); x <= Mathf.Min(cx + reachX, size - 1); x++)
                {
                    float dx = (x + 0.5f - fx) * cellX, dz = (z + 0.5f - fz) * cellZ;
                    float r = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                    if (r < 1f)
                        values[z * size + x] += (1f - r * r) * (1f - r * r);
                }
            }
        }
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
                image.SetPixel(x, z, new Color(Mathf.Min(values[z * size + x], 1f), 0f, 0f, 1f));
        }
        return ImageTexture.CreateFromImage(image);
    }

    private void Rebuild()
    {
        FreeBuilt();
        var root = new Node3D { Name = $"proto_{_variant.ToString().ToLowerInvariant()}" };
        string detail;
        switch (_variant)
        {
            case Variant.Slab:
                detail = BuildSlab(root);
                break;
            case Variant.Boxes:
                detail = BuildBoxes(root);
                break;
            default:
                detail = BuildShells(root);
                break;
        }
        _built = root;
        AddChild(root);
        string layer = _bottom is { } b && _top is { } t ? $"layer {b:0}-{t:0} m" : "no cloud band";
        Log.Info("world", $"proto clouds: variant={_variant.ToString().ToLowerInvariant()} {layer}, {detail}, hidden: deck={(_weather.HideDeck ? "yes" : "no")} field={(_field is { } f && !f.Visible ? "yes" : "no")} sprites={_hidden.Count} (Shift+F6 cycles)");
    }

    private string BuildSlab(Node3D root)
    {
        if (_bottom is not { } bottom || _top is not { } top)
            return "nothing drawn";
        var quad = new MeshInstance3D
        {
            Name = "slab",
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = MaterialOf(SlabShader(), bottom, top),
            CustomAabb = new Aabb(new Vector3(-1e6f, -1e6f, -1e6f), new Vector3(2e6f, 2e6f, 2e6f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            Layers = uint.MaxValue,
        };
        root.AddChild(quad);
        return "one full-screen march";
    }

    private string BuildBoxes(Node3D root)
    {
        var sprites = FieldSpritePositions();
        int boxes = 0;
        foreach (var volume in _volumes)
        {
            // The authored slab is thin; the box grows upward so the heads can tower over it.
            var authored = volume.Box;
            var box = new Aabb(authored.Position, new Vector3(authored.Size.X,
                Thickened(authored.Position.Y, authored.End.Y) - authored.Position.Y, authored.Size.Z));
            var map = PuffMap(authored, sprites, authored.Size.Y * 0.5f);
            AddBox(root, box, map, 1.0f);
            boxes++;
        }
        if (_volumes.Count == 0 && _bottom is { } b && _top is { } t)
        {
            var box = new Aabb(new Vector3(-FallbackHalfExtentM, b, -FallbackHalfExtentM),
                new Vector3(2f * FallbackHalfExtentM, t - b, 2f * FallbackHalfExtentM));
            AddBox(root, box, PuffMap(box, sprites, (t - b) * 0.5f), 1.0f);
            boxes++;
        }
        foreach (var (_, world) in _placedSprites)
        {
            // A billboard's quad is thin, so the proxy takes the card's width as its depth. It
            // stands on the card's foot, so the cumulus keeps a flat base where the sprite's was.
            float depth = Mathf.Max(world.Size.X, world.Size.Y) * PlacedCloudDepthShare;
            var centre = world.GetCenter();
            var size = new Vector3(Mathf.Max(world.Size.X, depth), Mathf.Max(world.Size.Y, depth * 0.9f), depth);
            var box = new Aabb(new Vector3(centre.X - size.X * 0.5f, world.Position.Y, centre.Z - size.Z * 0.5f), size);
            AddBox(root, box, PuffMap(box, new[] { centre }, size.Y), 1.4f);
            boxes++;
        }
        return $"{boxes} proxy box(es) over {sprites.Count} field sprites and {_placedSprites.Count} placed clouds";
    }

    private void AddBox(Node3D root, Aabb box, ImageTexture map, float puffGain)
    {
        var material = MaterialOf(BoxesShader(), box.Position.Y, box.End.Y);
        material.SetShaderParameter("puff_map", map);
        material.SetShaderParameter("box_min", box.Position);
        material.SetShaderParameter("box_max", box.End);
        material.SetShaderParameter("puff_gain", puffGain);
        var mesh = new MeshInstance3D
        {
            Name = $"box{root.GetChildCount()}",
            Mesh = new BoxMesh { Size = box.Size },
            MaterialOverride = material,
            Position = box.GetCenter(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            Layers = uint.MaxValue,
            ExtraCullMargin = 1000f,
        };
        root.AddChild(mesh);
    }

    private string BuildShells(Node3D root)
    {
        if (_bottom is not { } bottom || _top is not { } top)
            return "nothing drawn";
        float thickness = (top - bottom) / ShellCount;
        for (int i = 0; i < ShellCount; i++)
        {
            var material = MaterialOf(ShellsShader(), bottom, top);
            material.SetShaderParameter("shell_thickness_m", thickness * 1.5f);
            var sheet = new MeshInstance3D
            {
                Name = $"shell{i}",
                Mesh = new PlaneMesh { Size = new Vector2(ShellSizeM, ShellSizeM) },
                MaterialOverride = material,
                Position = new Vector3(0f, bottom + (i + 0.5f) * thickness, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                IgnoreOcclusionCulling = true,
                Layers = uint.MaxValue,
                ExtraCullMargin = 1000f,
            };
            root.AddChild(sheet);
        }
        return $"{ShellCount} sheets {thickness:0} m apart";
    }

    // Every sprite's world position in the fogvol field, read off its MultiMeshes.
    private List<Vector3> FieldSpritePositions()
    {
        var positions = new List<Vector3>();
        if (_field == null)
            return positions;
        foreach (var child in _field.GetChildren())
        {
            if (child is not MultiMeshInstance3D { Multimesh: { } mm } instance)
                continue;
            var xform = instance.GlobalTransform;
            for (int i = 0; i < mm.VisibleInstanceCount; i++)
                positions.Add(xform * mm.GetInstanceTransform(i).Origin);
        }
        return positions;
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
        if (_field != null)
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
        if (_built == null)
            return;
        _built.QueueFree();
        _built = null;
    }
}
