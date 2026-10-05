using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced-only wave ocean that replaces the flat sea-level sheet. A camera-centred polar grid
/// carries a Gerstner wave sum in its vertex stage. Finer waves live in the fragment normals. A mask
/// baked from the world's water polygons (<see cref="OceanMask"/>) calms the waves under the surf
/// ring, along every shore and around the ships. The base sheet's colliders stay flat at y = 0; only
/// its triangles step aside (<c>csky_ocean.gdshaderinc</c>). Driven by csky_time, so a network peer
/// and a fixed-step capture see the same waves.
/// </summary>
public sealed partial class Ocean : Node3D
{
    /// <summary>The global switch's name, declared in <c>res://shaders/csky_ocean.gdshaderinc</c>.</summary>
    public const string Param = "csky_ocean_on";

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

    private Ocean(ShaderMaterial material, Func<IEnumerable<Vector3>> ships, List<Node3D> wakes)
    {
        _wakes = wakes;
        Name = "Ocean";
        _material = material;
        _ships = ships;
    }

    /// <summary>Declares the global. Must run before the first shader that reads it is built.</summary>
    public static void RegisterGlobal() =>
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Float, 0.0f);

    /// <summary>Whether a chapter's sea gets the ocean. C1B alone: the other sea-level chapters'
    /// coast layers and base sheets are not yet checked against the mask.</summary>
    public static bool Covers(string chapter) =>
        string.Equals(chapter, "C1B", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the ocean over the world under <paramref name="worldRoot"/>, or null when
    /// that world has no sea-level base sheet to replace. A non-empty <paramref name="maskPng"/>
    /// receives the baked mask (<c>--dump-ocean-mask=</c>).</summary>
    public static Ocean? Create(Node3D worldRoot, SceneBuilder scene, TextureArchive textures,
        Func<IEnumerable<Vector3>> ships, ISet<Node> skip, string maskPng)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var mask = OceanMask.Bake(worldRoot, scene, skip);
        if (mask == null)
        {
            Log.Info("world", $"ocean: no sea-level base sheet, not built");
            return null;
        }
        double bakeMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
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

        var ocean = new Ocean(material, ships, mask.Wakes);
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
        double ms = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Log.Info("world", $"ocean: built grid verts={mesh.SurfaceGetArrayLen(0)} mask={mask.Width}x{mask.Height} cell={OceanMask.Cell}m origin=({mask.Origin.X:0},{mask.Origin.Y:0}) base={mask.BaseTexture} tile={mask.TileMetres:0.0}m base_tris={mask.BaseTriangles} edge_tris={mask.EdgeTriangles} solid_tris={mask.SolidTriangles} wakes={mask.Wakes.Count} ms={ms:0} bake={bakeMs:0} ({mask.Timing})");
        foreach (var wake in mask.Wakes)
        {
            if (wake.IsInsideTree())
            {
                var p = wake.GlobalPosition;
                Log.Info("world", $"ocean: wake '{wake.GetParent()?.Name}/{wake.Name}' at ({p.X:0},{p.Y:0.0},{p.Z:0}) visible={wake.IsVisibleInTree()}");
            }
        }
        if (maskPng.Length > 0)
        {
            var err = mask.Image.SavePng(maskPng);
            if (err == Error.Ok)
                err = mask.TintImage.SavePng(System.IO.Path.ChangeExtension(maskPng, ".tint.png"));
            if (err == Error.Ok)
                Log.Info("world", $"ocean: mask written to {maskPng}");
            else
                Log.Error("world", $"--dump-ocean-mask: could not write {maskPng} ({err})");
        }
        return ocean;
    }

    public override void _EnterTree() => RenderingServer.GlobalShaderParameterSet(ParamName, 1.0f);

    public override void _ExitTree() => RenderingServer.GlobalShaderParameterSet(ParamName, 0.0f);

    public override void _Process(double delta) => PublishShips();

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

float foam_hash(vec2 c) {
    return fract(sin(dot(c, vec2(127.1, 311.7))) * 43758.5453);
}

// Smooth value noise in [0, 1]; the cell is integer, so it stays exact far from the origin.
float foam_noise(vec2 p) {
    vec2 c = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(foam_hash(c), foam_hash(c + vec2(1.0, 0.0)), f.x),
        mix(foam_hash(c + vec2(0.0, 1.0)), foam_hash(c + vec2(1.0, 1.0)), f.x), f.y);
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
    // The swell's crest coincidences repeat on a lattice, so foam alone would print a pattern.
    // A drifting patch field decides where whitecaps can form and moves each crest's threshold.
    vec2 drift = SWELL_DKA[0].xy * (csky_time * 1.5);
    float patches = foam_noise((p - drift) / 230.0) * 0.65 + foam_noise((p - drift) / 71.0) * 0.35;
    float breakup = foam_noise((p - drift * 2.0) / 9.0);
    float crest = smoothstep(0.5, 0.85, h / max(SWELL_SUM * wave_scale, 0.01) + (patches - 0.55) * 0.5);
    float foam = crest * smoothstep(0.55, 0.8, patches) * smoothstep(0.25, 0.85, breakup);
    col = mix(col, vec3(0.6, 0.65, 0.68), foam * foam_strength);
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
}
