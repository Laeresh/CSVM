using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// PROTOTYPE, throwaway: an Enhanced-only wave ocean that replaces the flat sea-level sheet. A
/// camera-centred polar grid carries a Gerstner wave sum in its vertex stage. Finer waves live in
/// the fragment normals. A mask baked from the world's water polygons calms the waves under the
/// surf ring, along every shore and around the ships. The base sheet's colliders stay flat at
/// y = 0; only its triangles step aside (<c>csky_ocean.gdshaderinc</c>). Driven by csky_time, so a
/// network peer and a fixed-step capture see the same waves.
/// </summary>
public sealed partial class OceanPrototype : Node3D
{
    /// <summary>The global switch's name, declared in <c>res://shaders/csky_ocean.gdshaderinc</c>.</summary>
    public const string Param = "csky_ocean_on";

    // The mask's texel edge in metres. Fine enough that the calm band hugs the surf ring.
    private const float MaskCell = 8f;

    // Waves are fully calm this close to a shore or a surf texel, and reach full height here.
    private const float ShoreCalm = 24f;
    private const float ShoreFull = 160f;

    // The grid's angular resolution near the eye; it halves at each of BandEnds.
    private const int InnerSegments = 384;

    // Waves fade out within this radius band around a ship, so its wake sheet stays on the water.
    private const float ShipCalm = 140f;
    private const float ShipFull = 320f;
    private const int MaxShips = 16;

    // TUNE. The wind the swell runs with, degrees from +X toward +Z, and the crest sharpness.
    private const float WindDeg = 20f;
    private const float Choppiness = 0.55f;

    private static readonly StringName ParamName = Param;
    private static readonly float[] BandEnds = { 700f, 2800f, 11000f, 45000f };

    // TUNE. The swell the vertex stage displaces: wavelengths of tens of metres, a crest of about
    // 1-2 m where several meet. Angles are offsets from the wind.
    private static readonly Wave[] Swell =
    {
        new(0f, 140f, 0.05f), new(24f, 91f, 0.06f), new(-31f, 59f, 0.07f), new(47f, 38f, 0.07f),
        new(-14f, 25f, 0.07f), new(63f, 16f, 0.06f), new(-52f, 10.5f, 0.06f), new(9f, 6.8f, 0.05f),
    };

    // TUNE. The chop the fragment normals add on top: too small for any grid to carry.
    private static readonly Wave[] Chop =
    {
        new(-38f, 4.4f, 0.07f), new(71f, 2.9f, 0.07f), new(17f, 1.9f, 0.06f), new(-66f, 1.25f, 0.06f),
        new(42f, 0.82f, 0.05f), new(-8f, 0.54f, 0.05f),
    };

    private readonly Func<IEnumerable<Vector3>> _ships;
    private readonly List<Node3D> _wakes;
    private readonly ShaderMaterial _material;
    private readonly Vector4[] _shipData = new Vector4[MaxShips];

    private OceanPrototype(ShaderMaterial material, Func<IEnumerable<Vector3>> ships, List<Node3D> wakes)
    {
        _wakes = wakes;
        Name = "OceanPrototype";
        _material = material;
        _ships = ships;
    }

    /// <summary>Declares the global. Must run before the first shader that reads it is built.</summary>
    public static void RegisterGlobal() =>
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Float, 0.0f);

    /// <summary>Whether this session gets the ocean: C1B only, and <c>CSVM_OCEAN=0</c> turns it
    /// off for a flat-sea comparison shot.</summary>
    public static bool Wanted(string chapter) =>
        string.Equals(chapter, "C1B", StringComparison.OrdinalIgnoreCase)
        && System.Environment.GetEnvironmentVariable("CSVM_OCEAN") != "0";

    /// <summary>Builds the ocean over the world under <paramref name="worldRoot"/>, or null when
    /// that world has no sea-level base sheet to replace.</summary>
    public static OceanPrototype? Create(Node3D worldRoot, SceneBuilder scene, TextureArchive textures,
        Func<IEnumerable<Vector3>> ships, ISet<Node> skip)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var names = new Dictionary<Material, string>();
        foreach (var (mat, tex) in scene.TexturedMaterials)
            names[mat] = tex;
        var mask = MaskBuilder.Build(worldRoot, names, skip);
        if (mask == null)
        {
            Log.Info("world", $"ocean prototype: no sea-level base sheet, not built");
            return null;
        }
        var baseTex = textures.Find(mask.BaseTexture);
        var meanTex = MeanColor(textures.FindImage(mask.BaseTexture));

        var shader = new Shader { Code = ShaderCode() };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mask_tex", mask.Texture);
        material.SetShaderParameter("mask_rect", new Vector4(mask.Origin.X, mask.Origin.Y, 1f / mask.Size.X, 1f / mask.Size.Y));
        if (baseTex != null)
            material.SetShaderParameter("albedo_tex", baseTex);
        material.SetShaderParameter("tile_m", mask.TileMetres);
        material.SetShaderParameter("base_color", meanTex);
        material.SetShaderParameter("tint_tex", mask.TintTexture);
        ApplyOverrides(material);

        var ocean = new OceanPrototype(material, ships, mask.Wakes);
        ocean.PublishShips();
        var mesh = BuildGrid();
        mesh.SurfaceSetMaterial(0, material);
        var instance = new MeshInstance3D
        {
            Name = "OceanGrid",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The grid follows every camera in its vertex stage, so it must never be culled.
            CustomAabb = new Aabb(new Vector3(-80000f, -50f, -80000f), new Vector3(160000f, 100f, 160000f)),
        };
        ocean.AddChild(instance);
        Log.Info("world", $"ocean prototype: built grid verts={mesh.SurfaceGetArrayLen(0)} mask={mask.Width}x{mask.Height} cell={MaskCell}m origin=({mask.Origin.X:0},{mask.Origin.Y:0}) base={mask.BaseTexture} tile={mask.TileMetres:0.0}m base_tris={mask.BaseTriangles} edge_tris={mask.EdgeTriangles} solid_tris={mask.SolidTriangles} wakes={mask.Wakes.Count} ms={System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds:0}");
        foreach (var wake in mask.Wakes)
        {
            if (wake.IsInsideTree())
                Log.Info("world", $"ocean prototype: wake '{wake.GetParent()?.Name}/{wake.Name}' at ({wake.GlobalPosition.X:0},{wake.GlobalPosition.Y:0.0},{wake.GlobalPosition.Z:0}) visible={wake.IsVisibleInTree()}");
        }
        if (System.Environment.GetEnvironmentVariable("CSVM_OCEAN_MASK_PNG") is { Length: > 0 } png)
            mask.Image.SavePng(png);
        return ocean;
    }

    public override void _EnterTree() => RenderingServer.GlobalShaderParameterSet(ParamName, 1.0f);

    public override void _ExitTree() => RenderingServer.GlobalShaderParameterSet(ParamName, 0.0f);

    public override void _Process(double delta)
    {
        PublishShips();
        if (System.Environment.GetEnvironmentVariable("CSVM_OCEAN_TRACE") == "1" && Engine.GetProcessFrames() % 300 == 0)
        {
            foreach (var wake in _wakes)
            {
                if (IsInstanceValid(wake) && wake.IsInsideTree())
                    Log.Info("world", $"ocean prototype: frame {Engine.GetProcessFrames()} wake '{wake.GetParent()?.Name}' at ({wake.GlobalPosition.X:0},{wake.GlobalPosition.Y:0.0},{wake.GlobalPosition.Z:0}) visible={wake.IsVisibleInTree()}");
            }
        }
    }

    // CSVM_OCEAN_PARAMS="wave_scale=0.5;rough_near=0.2" sets float uniforms for a tuning run.
    private static void ApplyOverrides(ShaderMaterial material)
    {
        if (System.Environment.GetEnvironmentVariable("CSVM_OCEAN_PARAMS") is not { Length: > 0 } spec)
            return;
        foreach (var pair in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                material.SetShaderParameter(kv[0].Trim(), v);
        }
        Log.Info("world", $"ocean prototype: overrides {spec}");
    }

    private static Color MeanColor(Image? img)
    {
        if (img == null)
            return new Color(0.1f, 0.25f, 0.35f);
        img.Convert(Image.Format.Rgba8);
        double r = 0, g = 0, b = 0;
        int w = img.GetWidth(), h = img.GetHeight();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y).SrgbToLinear();
                r += c.R;
                g += c.G;
                b += c.B;
            }
        }
        double n = w * h;
        return new Color((float)(r / n), (float)(g / n), (float)(b / n));
    }

    // Concentric rings around the origin whose spacing grows with radius, so a triangle covers
    // about the same screen area at every distance. UV.x carries the local spacing for the
    // vertex stage's per-wave fade.
    private static ArrayMesh BuildGrid()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var idx = new List<int>();
        verts.Add(Vector3.Zero);
        uvs.Add(new Vector2(0.5f, 0f));
        int n = InnerSegments;
        float r = 4f;
        int prevStart = AddRing(verts, uvs, r, n);
        int prevN = n;
        for (int i = 0; i < n; i++)
            idx.AddRange(new[] { 0, prevStart + i, prevStart + ((i + 1) % n) });
        for (int band = 0; band < BandEnds.Length; band++)
        {
            while (r < BandEnds[band])
            {
                r *= 1f + (Mathf.Tau / n);
                int start = AddRing(verts, uvs, r, n);
                Stitch(idx, prevStart, prevN, start, n);
                prevStart = start;
                prevN = n;
            }
            n = Math.Max(n / 2, 24);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static int AddRing(List<Vector3> verts, List<Vector2> uvs, float r, int n)
    {
        int start = verts.Count;
        float spacing = r * Mathf.Tau / n;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            verts.Add(new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)));
            uvs.Add(new Vector2(spacing, 0f));
        }
        return start;
    }

    // Joins an inner ring to the next one out, which has the same count or half of it.
    private static void Stitch(List<int> idx, int a, int na, int b, int nb)
    {
        if (na == nb)
        {
            for (int i = 0; i < na; i++)
            {
                int i1 = (i + 1) % na;
                idx.AddRange(new[] { a + i, b + i, b + i1 });
                idx.AddRange(new[] { a + i, b + i1, a + i1 });
            }
            return;
        }
        for (int k = 0; k < nb; k++)
        {
            int k1 = (k + 1) % nb;
            int i0 = 2 * k, i1 = 2 * k + 1, i2 = (2 * k + 2) % na;
            idx.AddRange(new[] { a + i0, b + k, a + i1 });
            idx.AddRange(new[] { a + i1, b + k, b + k1 });
            idx.AddRange(new[] { a + i1, b + k1, a + i2 });
        }
    }

    private static string F(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    // vec4(dir.x, dir.z, k, A) and vec2(Q, omega) per wave. Omega is rounded to a whole number of
    // cycles per csky_time wrap, so the hourly rollover lands on an identical frame.
    private static void EmitWaves(StringBuilder sb, string name, Wave[] waves, int count)
    {
        var a = new List<string>();
        var b = new List<string>();
        foreach (var w in waves)
        {
            float ang = Mathf.DegToRad(WindDeg + w.AngleDeg);
            float k = Mathf.Tau / w.Length;
            float amp = w.Steepness / k;
            float omega = Mathf.Sqrt(9.81f * k);
            omega = Mathf.Tau * Mathf.Round(omega * (float)ShaderTime.RolloverSecs / Mathf.Tau) / (float)ShaderTime.RolloverSecs;
            float q = Choppiness / (k * amp * count);
            a.Add($"vec4({F(Mathf.Cos(ang))}, {F(Mathf.Sin(ang))}, {F(k)}, {F(amp)})");
            b.Add($"vec2({F(q)}, {F(omega)})");
        }
        sb.AppendLine($"const vec4 {name}_DKA[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", a)});");
        sb.AppendLine($"const vec2 {name}_QW[{waves.Length}] = vec2[{waves.Length}]({string.Join(", ", b)});");
    }

    private static string ShaderCode()
    {
        float swellSum = 0f;
        foreach (var w in Swell)
            swellSum += w.Steepness * w.Length / Mathf.Tau;
        var sb = new StringBuilder();
        sb.AppendLine("shader_type spatial;");
        sb.AppendLine("render_mode skip_vertex_transform, cull_disabled;");
        sb.AppendLine(SceneBuilder.TimeInclude);
        sb.AppendLine(SceneBuilder.AtmosphereInclude);
        sb.AppendLine("uniform sampler2D mask_tex : filter_linear, repeat_disable;");
        sb.AppendLine("uniform vec4 mask_rect;");
        sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;");
        sb.AppendLine("uniform float tile_m = 32.0;");
        sb.AppendLine("uniform vec3 base_color : source_color = vec3(0.1, 0.25, 0.35);");
        sb.AppendLine("uniform sampler2D tint_tex : source_color, filter_linear, repeat_disable;");
        sb.AppendLine("uniform float rough_near = 0.2;");
        sb.AppendLine("uniform float rough_far = 0.3;");
        // TUNE: how much of the original's painted texture shows through the mean colour.
        sb.AppendLine("uniform float detail_mix = 0.6;");
        sb.AppendLine("uniform float wave_scale = 1.0;");
        sb.AppendLine("uniform float foam_strength = 0.12;");
        sb.AppendLine($"uniform vec4 ships[{MaxShips}];");
        EmitWaves(sb, "SWELL", Swell, Swell.Length);
        EmitWaves(sb, "CHOP", Chop, Chop.Length);
        sb.AppendLine($"const float SWELL_SUM = {F(swellSum)};");
        sb.AppendLine("varying vec2 v_param;");
        sb.AppendLine(@"
vec2 ocean_mask(vec2 p) {
    return textureLod(mask_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).rg;
}

float ship_calm(vec2 p) {
    float a = 1.0;
    for (int i = 0; i < " + MaxShips.ToString(CultureInfo.InvariantCulture) + @"; i++) {
        a = min(a, smoothstep(ships[i].z, ships[i].w, distance(p, ships[i].xy)));
    }
    return a;
}

void vertex() {
    vec2 p = VERTEX.xz + CAMERA_POSITION_WORLD.xz;
    float spacing = max(UV.x, 0.01);
    float amp = ocean_mask(p).g * ship_calm(p) * wave_scale;
    vec3 disp = vec3(0.0);
    for (int i = 0; i < SWELL_DKA.length(); i++) {
        vec4 w = SWELL_DKA[i];
        float len = 6.2831853 / w.z;
        // Full height at eight grid steps per wavelength, none at four: a wave the grid cannot
        // carry would crawl as the grid follows the eye.
        float g = amp * clamp(len / spacing * 0.25 - 1.0, 0.0, 1.0);
        float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time;
        disp.xz += w.xy * (SWELL_QW[i].x * w.w * g * cos(th));
        disp.y += w.w * g * sin(th);
    }
    v_param = p;
    vec3 world = vec3(p.x + disp.x, disp.y, p.y + disp.z);
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    // One priority level away from the eye, so sea-level ground and the surf ring draw over it.
    VERTEX *= 1.0002;
    NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
}

void fragment() {
    vec2 p = v_param;
    vec2 m = ocean_mask(p);
    vec3 world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // One-sided like the sheet it replaces: an eye below the surface sees through it.
    if (m.r < 0.02 || CAMERA_POSITION_WORLD.y < world.y) {
        discard;
    }
    float amp = m.g * ship_calm(p) * wave_scale;
    vec2 fw = fwidth(p);
    float footprint = max(max(fw.x, fw.y), 0.001);
    vec3 n = vec3(0.0, 1.0, 0.0);
    // The full swell height here, independent of how much of it the grid displaced, so the
    // foam reads the same at every distance.
    float h = 0.0;
    for (int i = 0; i < SWELL_DKA.length(); i++) {
        vec4 w = SWELL_DKA[i];
        float f = amp * smoothstep(1.5, 4.0, 6.2831853 / w.z / footprint);
        float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time;
        float ka = w.z * w.w * f;
        n.xz -= w.xy * (ka * cos(th));
        n.y -= SWELL_QW[i].x * ka * sin(th);
        h += w.w * f * sin(th);
    }
    for (int i = 0; i < CHOP_DKA.length(); i++) {
        vec4 w = CHOP_DKA[i];
        float f = amp * smoothstep(1.5, 4.0, 6.2831853 / w.z / footprint);
        float th = w.z * dot(w.xy, p) - CHOP_QW[i].y * csky_time;
        float ka = w.z * w.w * f;
        n.xz -= w.xy * (ka * cos(th));
        n.y -= CHOP_QW[i].x * ka * sin(th);
    }
    n = normalize(n);
    NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
    float dist = distance(world.xz, CAMERA_POSITION_WORLD.xz);
    vec3 tint = texture(tint_tex, (p - mask_rect.xy) * mask_rect.zw).rgb;
    vec3 col = mix(base_color, texture(albedo_tex, p / tile_m).rgb, detail_mix) * tint;
    float crest = smoothstep(0.6, 0.9, h / max(SWELL_SUM * wave_scale, 0.01));
    col = mix(col, vec3(0.6, 0.65, 0.68), crest * foam_strength);
    ALBEDO = col;
    METALLIC = 0.0;
    SPECULAR = 0.5;
    // Lost slope detail turns into roughness, so the far sea keeps a glossy sheen, not a mirror.
    ROUGHNESS = mix(rough_near, rough_far, smoothstep(150.0, 4000.0, dist));
    FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), csky_fog_amount(world, CAMERA_POSITION_WORLD));
}");
        return sb.ToString();
    }

    // Every live hull's position for the shader's calm discs; an unused slot sits far away.
    private void PublishShips()
    {
        int n = 0;
        foreach (var p in _ships())
        {
            if (n >= MaxShips)
                break;
            _shipData[n++] = new Vector4(p.X, p.Z, ShipCalm, ShipFull);
        }
        foreach (var wake in _wakes)
        {
            if (n >= MaxShips)
                break;
            if (!IsInstanceValid(wake) || !wake.IsInsideTree() || !wake.IsVisibleInTree())
                continue;
            var p = wake.GlobalPosition;
            _shipData[n++] = new Vector4(p.X, p.Z, ShipCalm, ShipFull);
        }
        for (int i = n; i < MaxShips; i++)
            _shipData[i] = new Vector4(1e9f, 1e9f, 0f, 1f);
        _material.SetShaderParameter("ships", _shipData);
    }

    // One Gerstner component: direction, wavelength in metres, steepness k*A.
    private readonly record struct Wave(float AngleDeg, float Length, float Steepness);

    // The baked shore and surf mask: R is sea coverage, G the wave height that survives there.
    private sealed class MaskBuilder
    {
        public ImageTexture Texture = null!;
        public Image Image = null!;
        public ImageTexture TintTexture = null!;
        public Vector2 Origin;
        public Vector2 Size;
        public int Width;
        public int Height;
        public string BaseTexture = "";
        public float TileMetres = 32f;
        public Color VertexColor = Colors.White;
        public int BaseTriangles;
        public int EdgeTriangles;
        public int SolidTriangles;
        public List<Node3D> Wakes = new();

        private enum Kind
        {
            Base,
            Edge,
            Solid,
        }

        public static MaskBuilder? Build(Node3D root, Dictionary<Material, string> names, ISet<Node> skip)
        {
            var tris = new List<Tri>();
            var result = new MaskBuilder();
            // An animated ship carries its wake sheet. Its hull must not leave a calm patch where it
            // started, so the wake's parent subtree is left out of the bake.
            FindWakes(root, names, result.Wakes);
            skip = new HashSet<Node>(skip);
            foreach (var wake in result.Wakes)
            {
                if (wake.GetParent() is { } hull && hull != root)
                    skip.Add(hull);
            }
            var uvSamples = new List<float>();
            double cr = 0, cg = 0, cb = 0;
            int cn = 0;
            var baseCounts = new Dictionary<string, int>();
            Walk(root, Transform3D.Identity, root, names, skip, tris, result, uvSamples, ref cr, ref cg, ref cb, ref cn, baseCounts);
            if (result.BaseTriangles == 0)
                return null;
            int best = 0;
            foreach (var (name, count) in baseCounts)
            {
                if (count > best)
                {
                    best = count;
                    result.BaseTexture = name;
                }
            }
            if (uvSamples.Count > 0)
            {
                uvSamples.Sort();
                result.TileMetres = uvSamples[uvSamples.Count / 2];
            }
            if (cn > 0)
                result.VertexColor = new Color((float)(cr / cn), (float)(cg / cn), (float)(cb / cn)).SrgbToLinear();

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var t in tris)
            {
                if (t.Kind == Kind.Solid)
                    continue;
                foreach (var v in new[] { t.A, t.B, t.C })
                {
                    minX = Math.Min(minX, v.X);
                    maxX = Math.Max(maxX, v.X);
                    minZ = Math.Min(minZ, v.Z);
                    maxZ = Math.Max(maxZ, v.Z);
                }
            }
            minX = Math.Max(minX, -30000f);
            minZ = Math.Max(minZ, -30000f);
            maxX = Math.Min(maxX, 30000f);
            maxZ = Math.Min(maxZ, 30000f);
            // No spare texel past the last polygon: clamp-to-edge carries the border sea outward.
            int w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / MaskCell));
            int h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / MaskCell));
            result.Width = w;
            result.Height = h;
            result.Origin = new Vector2(minX, minZ);
            result.Size = new Vector2(w * MaskCell, h * MaskCell);
            var sea = new byte[w * h];   // 1 base, 2 edge
            var solid = new bool[w * h];
            var tint = new Color[w * h];
            Array.Fill(tint, new Color(-1f, 0f, 0f));
            foreach (var t in tris)
                Raster(t, minX, minZ, w, h, sea, solid, tint);

            // Distance in metres to the nearest texel that is not open base sea (two-pass chamfer).
            var dist = new float[w * h];
            const float big = 1e6f;
            for (int i = 0; i < dist.Length; i++)
                dist[i] = sea[i] == 1 && !solid[i] ? big : 0f;
            float d1 = MaskCell, d2 = MaskCell * 1.4142f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float d = dist[i];
                    if (d == 0f)
                        continue;
                    if (x > 0) d = Math.Min(d, dist[i - 1] + d1);
                    if (y > 0) d = Math.Min(d, dist[i - w] + d1);
                    if (x > 0 && y > 0) d = Math.Min(d, dist[i - w - 1] + d2);
                    if (x < w - 1 && y > 0) d = Math.Min(d, dist[i - w + 1] + d2);
                    dist[i] = d;
                }
            }
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    float d = dist[i];
                    if (d == 0f)
                        continue;
                    if (x < w - 1) d = Math.Min(d, dist[i + 1] + d1);
                    if (y < h - 1) d = Math.Min(d, dist[i + w] + d1);
                    if (x < w - 1 && y < h - 1) d = Math.Min(d, dist[i + w + 1] + d2);
                    if (x > 0 && y < h - 1) d = Math.Min(d, dist[i + w - 1] + d2);
                    dist[i] = d;
                }
            }
            var bytes = new byte[w * h * 2];
            for (int i = 0; i < w * h; i++)
            {
                bytes[2 * i] = sea[i] != 0 ? (byte)255 : (byte)0;
                float a = Mathf.SmoothStep(ShoreCalm, ShoreFull, dist[i]);
                bytes[2 * i + 1] = (byte)Math.Round(a * 255f);
            }
            result.Image = Image.CreateFromData(w, h, false, Image.Format.Rg8, bytes);
            // The base sheet's baked vertex colour per texel, sRGB as authored; open texels take the mean.
            var tintBytes = new byte[w * h * 3];
            var mean = result.VertexColor.LinearToSrgb();
            for (int i = 0; i < w * h; i++)
            {
                var c = tint[i].R < 0f ? mean : tint[i];
                tintBytes[3 * i] = (byte)Math.Clamp(Math.Round(c.R * 255f), 0, 255);
                tintBytes[3 * i + 1] = (byte)Math.Clamp(Math.Round(c.G * 255f), 0, 255);
                tintBytes[3 * i + 2] = (byte)Math.Clamp(Math.Round(c.B * 255f), 0, 255);
            }
            result.TintTexture = ImageTexture.CreateFromImage(Image.CreateFromData(w, h, false, Image.Format.Rgb8, tintBytes));
            result.Texture = ImageTexture.CreateFromImage(result.Image);
            return result;
        }

        private static void FindWakes(Node node, Dictionary<Material, string> names, List<Node3D> wakes)
        {
            if (node is MeshInstance3D mi && mi.Mesh is { } mesh)
            {
                for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (mat != null && names.TryGetValue(mat, out var tex)
                        && tex.Contains("wakefront", StringComparison.OrdinalIgnoreCase))
                    {
                        wakes.Add(mi);
                        break;
                    }
                }
            }
            foreach (var child in node.GetChildren())
                FindWakes(child, names, wakes);
        }

        private static void Walk(Node node, Transform3D parent, Node3D root, Dictionary<Material, string> names,
            ISet<Node> skip, List<Tri> tris, MaskBuilder result, List<float> uvSamples,
            ref double cr, ref double cg, ref double cb, ref int cn, Dictionary<string, int> baseCounts)
        {
            if (skip.Contains(node))
                return;
            var xf = parent;
            if (node is Node3D n3 && node != root)
                xf = parent * n3.Transform;
            if (node is MeshInstance3D mi && mi.Mesh is ArrayMesh mesh)
            {
                for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (mat == null || !names.TryGetValue(mat, out var tex))
                        continue;
                    var arrays = mesh.SurfaceGetArrays(s);
                    var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                        ? Array.Empty<int>() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                    var uv = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil
                        ? Array.Empty<Vector2>() : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                    var col = arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil
                        ? Array.Empty<Color>() : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
                    bool water = SceneBuilder.ClassifySurface(tex) == "water";
                    bool isBase = water && SceneBuilder.IsOceanBaseTexture(tex);
                    int triCount = ix.Length > 0 ? ix.Length / 3 : v.Length / 3;
                    for (int t = 0; t < triCount; t++)
                    {
                        int i0 = ix.Length > 0 ? ix[3 * t] : 3 * t;
                        int i1 = ix.Length > 0 ? ix[3 * t + 1] : 3 * t + 1;
                        int i2 = ix.Length > 0 ? ix[3 * t + 2] : 3 * t + 2;
                        var a = xf * v[i0];
                        var b = xf * v[i1];
                        var c = xf * v[i2];
                        float minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
                        float maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
                        bool seaLevel = minY > -0.5f && maxY < 0.5f;
                        Kind kind;
                        if (isBase && seaLevel)
                        {
                            kind = Kind.Base;
                            result.BaseTriangles++;
                            baseCounts[tex] = baseCounts.GetValueOrDefault(tex) + 1;
                            if (uv.Length > 0)
                            {
                                float dp = new Vector2(b.X - a.X, b.Z - a.Z).Length();
                                float du = (uv[i1] - uv[i0]).Length();
                                if (du > 1e-4f)
                                    uvSamples.Add(dp / du);
                            }
                            if (col.Length > 0)
                            {
                                cr += col[i0].R;
                                cg += col[i0].G;
                                cb += col[i0].B;
                                cn++;
                            }
                        }
                        else if (water && seaLevel)
                        {
                            kind = Kind.Edge;
                            result.EdgeTriangles++;
                        }
                        else if (!water && minY < 20f && maxY - minY < 400f)
                        {
                            kind = Kind.Solid;
                            result.SolidTriangles++;
                        }
                        else
                        {
                            continue;
                        }
                        bool tinted = kind == Kind.Base && col.Length > 0;
                        tris.Add(new Tri(a, b, c, kind, tinted ? col[i0] : Colors.White, tinted ? col[i1] : Colors.White, tinted ? col[i2] : Colors.White));
                    }
                }
            }
            foreach (var child in node.GetChildren())
                Walk(child, xf, root, names, skip, tris, result, uvSamples, ref cr, ref cg, ref cb, ref cn, baseCounts);
        }

        private static void Raster(Tri t, float minX, float minZ, int w, int h, byte[] sea, bool[] solid, Color[] tint)
        {
            var a = new Vector2(t.A.X, t.A.Z);
            var b = new Vector2(t.B.X, t.B.Z);
            var c = new Vector2(t.C.X, t.C.Z);
            float area = Cross(b - a, c - a);
            int x0 = Math.Max(0, (int)Math.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - minX) / MaskCell));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(a.X, Math.Max(b.X, c.X)) - minX) / MaskCell));
            int z0 = Math.Max(0, (int)Math.Floor((Math.Min(a.Y, Math.Min(b.Y, c.Y)) - minZ) / MaskCell));
            int z1 = Math.Min(h - 1, (int)Math.Ceiling((Math.Max(a.Y, Math.Max(b.Y, c.Y)) - minZ) / MaskCell));
            // A triangle smaller than a texel still marks the texels its corners land in.
            if (t.Kind == Kind.Solid)
            {
                foreach (var p in new[] { a, b, c })
                    Mark((int)((p.X - minX) / MaskCell), (int)((p.Y - minZ) / MaskCell));
            }
            if (Math.Abs(area) < 1e-6f)
                return;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(minX + (x + 0.5f) * MaskCell, minZ + (z + 0.5f) * MaskCell);
                    float w0 = Cross(b - a, p - a) / area;
                    float w1 = Cross(c - b, p - b) / area;
                    float w2 = Cross(a - c, p - c) / area;
                    if (w0 >= -1e-4f && w1 >= -1e-4f && w2 >= -1e-4f)
                    {
                        Mark(x, z);
                        if (t.Kind == Kind.Base)
                            tint[z * w + x] = (t.CA * w1) + (t.CB * w2) + (t.CC * w0);
                    }
                }
            }

            void Mark(int x, int z)
            {
                if (x < 0 || z < 0 || x >= w || z >= h)
                    return;
                int i = z * w + x;
                switch (t.Kind)
                {
                    case Kind.Base:
                        if (sea[i] == 0)
                            sea[i] = 1;
                        break;
                    case Kind.Edge:
                        sea[i] = 2;
                        break;
                    default:
                        solid[i] = true;
                        break;
                }
            }
        }

        private static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

        private readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, Kind Kind, Color CA, Color CB, Color CC);
    }
}
