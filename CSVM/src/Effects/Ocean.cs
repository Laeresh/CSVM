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

    // TUNE. The waves stay flat this far past a ship's zone, more than the swell's sideways sway,
    // and reach full height over the fade beyond. Flat water keeps a wake sheet on the surface.
    private const float CalmMargin = 6f;
    private const float CalmFade = 50f;
    private const int MaxShips = 16;

    // TUNE. Metres from the nearest shore, surf or solid texel (the mask's G). The swell's height
    // fades in over the long ramp, so a displaced crest never lifts through a coplanar coast layer.
    // The look (slope, chop, foam, texture) fades in over the short one. It has only to match the
    // flat sheet where an opaque coast tile meets the ocean. LookCalm clears the texel beside the
    // coast one, which the linear filter blends into the boundary.
    private const float ShoreCalm = 24f;
    private const float ShoreFull = OceanMaskRaster.ShoreReach;
    private const float LookCalm = 12f;
    private const float LookFull = 64f;

    // A hull is on the water while its origin sits this close to sea level; a hoisted lifeboat is not.
    private const float HullWaterBand = OceanMovers.OriginBand;

    // The hull meshes that reach this close to the hull's own y = 0 make its waterline.
    private const float WaterlineBand = OceanMovers.WaterlineBand;

    // TUNE. The wind the swell runs with, degrees from +X toward +Z, and the crest sharpness.
    private const float WindDeg = 20f;
    private const float Choppiness = 0.55f;

    // TUNE. The fog amount by which the ocean shades wholly as the flat sheet. Low, so fog thick
    // enough to see covers no wave the sheet would not show.
    private const float FogSheetAt = 0.25f;

    // TUNE. The sea-state field that keeps the fixed waves from meeting on a lattice. Each wave reads
    // two noise fields along its own direction: one scales its height, one shifts its phase. The
    // shift is capped at PhaseMetres of travel, so a long wave's crests bend only slightly.
    // The field holds still in world space, so the csky_time wrap still lands on an identical frame.
    private const float GroupScale = 500f;
    private const float GroupGain = 0.8f;
    private const float PhaseScaleCoarse = 400f;
    private const float PhaseScaleFine = 130f;
    private const float PhaseMetres = 16f;
    private const float PhaseMaxRad = 3f;

    // TUNE. A finer field for the short waves, whose crests the coarse one leaves as a straight
    // grain. A wave feels it fully at FineFull metres and shorter, not at all from FineFrom up, so
    // the long swell keeps its shape.
    private const float FineGroupScale = 40f;
    private const float FinePhaseScale = 28f;
    private const float FineGain = 1.2f;
    private const float FineMetres = 5f;
    private const float FineFrom = 40f;
    private const float FineFull = 10f;

    // TUNE. The chop's own field, a few of its wavelengths across, so its crests break into short
    // irregular strokes. Fragment-only, as the chop is.
    private const float MicroScale = 9f;
    private const float MicroGain = 0.8f;
    private const float MicroMetres = 1f;

    // TUNE. Pixels per wavelength over which a wave fades into the fragment normals. A wave drawn
    // at a few pixels per wavelength reads as a straight grain, not as water.
    private const float FootprintFadeFrom = 4f;
    private const float FootprintFadeFull = 9f;

    // TUNE. Foam's crest reference in standard deviations of the swell height, so the share of
    // whitecaps does not move with the wave count.
    private const float CrestSigmas = 3.13f;

    private static readonly StringName ParamName = Param;
    private static readonly string[] Chapters = { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C5" };
    private static readonly float[] BandEnds = { 700f, 2800f, 11000f, 45000f };

    // TUNE. The swell the vertex stage displaces: wavelengths of tens of metres, a crest of about
    // 1-2 m where several meet. Angles are offsets from the wind. The lengths step by about 1.3
    // with no common ratio, so no two waves dominate and cross on a visible lattice.
    private static readonly Wave[] Swell =
    {
        new(0f, 152f, 0.04f), new(31f, 117f, 0.042f), new(-24f, 93f, 0.045f), new(55f, 71f, 0.047f),
        new(-47f, 57f, 0.05f), new(14f, 44f, 0.052f), new(-71f, 34f, 0.052f), new(38f, 26f, 0.06f),
        new(-9f, 19.5f, 0.06f), new(66f, 14.2f, 0.058f), new(-36f, 10.1f, 0.055f), new(21f, 7.3f, 0.05f),
    };

    // TUNE. The chop the fragment normals add on top: too small for any grid to carry.
    private static readonly Wave[] Chop =
    {
        new(-38f, 4.4f, 0.07f), new(71f, 2.9f, 0.07f), new(17f, 1.9f, 0.06f), new(-66f, 1.25f, 0.06f),
        new(42f, 0.82f, 0.05f), new(-8f, 0.54f, 0.05f),
    };

    private readonly Func<IEnumerable<Node3D>> _ships;
    private readonly List<Node3D> _wakes;
    private readonly Node3D _worldRoot;
    private readonly ShaderMaterial _material;
    private readonly Vector4[] _calmAxes = new Vector4[MaxShips];
    private readonly Vector4[] _calmSizes = new Vector4[MaxShips];
    private readonly OceanCalmZone[] _zones = new OceanCalmZone[MaxShips];
    private readonly Node3D?[] _zoneHulls = new Node3D?[MaxShips];
    private readonly Dictionary<Node3D, Aabb?> _waterlines = new();
    private readonly OceanMask _mask;
    private readonly List<Node3D> _candidates = new();
    private readonly List<Vector3> _candidateAt = new();
    private readonly List<bool> _candidateWake = new();
    private readonly List<int> _order = new();
    private readonly Dictionary<Node3D, Transform3D> _nestedRest = new();
    private readonly HashSet<Node3D> _zoned = new();

    private Ocean(ShaderMaterial material, Func<IEnumerable<Node3D>> ships, OceanMask mask, Node3D worldRoot)
    {
        _mask = mask;
        _wakes = mask.Wakes;
        Name = "Ocean";
        _material = material;
        _ships = ships;
        _worldRoot = worldRoot;
    }

    /// <summary>Declares the switch and the mask globals the base sheet's hide reads. Must run before
    /// the first shader that reads them is built.</summary>
    public static void RegisterGlobal()
    {
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Float, 0.0f);
        OceanMask.RegisterGlobals();
    }

    /// <summary>Whether a chapter's sea gets the ocean: every chapter with a sea at y = 0. C4 has
    /// only raised lakes, which keep the flat glossy water.</summary>
    public static bool Covers(string chapter) => Array.Exists(Chapters, c => string.Equals(c, chapter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds the ocean over the world under <paramref name="worldRoot"/>, or null when
    /// that world has no sea-level base sheet to replace. The <paramref name="movers"/> an animation
    /// carries (<see cref="OceanMovers"/>) are calmed around as hulls wherever they float. A
    /// non-empty <paramref name="maskPng"/> receives the baked mask (<c>--dump-ocean-mask=</c>).</summary>
    public static Ocean? Create(Node3D worldRoot, SceneBuilder scene, TextureArchive textures,
        Func<IEnumerable<Node3D>> ships, ISet<Node> skip, IReadOnlyCollection<Node3D> movers, string maskPng)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var mask = OceanMask.Bake(worldRoot, scene, skip, movers);
        if (mask == null)
        {
            Log.Info("world", $"ocean: no sea-level base sheet, not built");
            return null;
        }
        double bakeMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var baseTex = textures.Find(mask.BaseTexture);
        var meanTex = MeanColor(textures.FindImage(mask.BaseTexture));

        bool zoned = mask.ZoneTexture != null;
        var shader = new Shader { Code = ShaderCode(mask.BaseLevel - 1, zoned) };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mask_tex", mask.Texture);
        material.SetShaderParameter("mask_rect", new Vector4(mask.Origin.X, mask.Origin.Y, 1f / mask.Size.X, 1f / mask.Size.Y));
        if (baseTex != null)
            material.SetShaderParameter("albedo_tex", baseTex);
        material.SetShaderParameter("tile_m", mask.TileMetres);
        material.SetShaderParameter("base_color", meanTex);
        material.SetShaderParameter("tint_tex", mask.TintTexture);
        if (mask.ZoneTexture is { } zoneTex)
            material.SetShaderParameter("zone_tex", zoneTex);

        var ocean = new Ocean(material, ships, mask, worldRoot);
        ocean.PublishShips();
        var mesh = BuildGrid();
        mesh.SurfaceSetMaterial(0, material);
        // One grid per zone group, on that group's layers. The zone gate then culls the waves with
        // the sheet they replace; C5 splits its sea over zone_id 1 and 3.
        for (int z = 0; z < Math.Max(1, mask.ZoneLayers.Count); z++)
        {
            var instance = new MeshInstance3D
            {
                Name = z == 0 ? "OceanGrid" : $"OceanGrid{z}",
                Mesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // The grid follows every camera in its vertex stage, so it must never be culled.
                CustomAabb = new Aabb(new Vector3(-80000f, -50f, -80000f), new Vector3(160000f, 100f, 160000f)),
            };
            if (z < mask.ZoneLayers.Count)
                instance.Layers = mask.ZoneLayers[z];
            if (zoned)
                instance.SetInstanceShaderParameter("zone_index", (float)z);
            ocean.AddChild(instance);
        }
        double ms = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Log.Info("world", $"ocean: built grid verts={mesh.SurfaceGetArrayLen(0)} mask={mask.Width}x{mask.Height} cell={OceanMask.Cell}m origin=({mask.Origin.X:0},{mask.Origin.Y:0}) base={mask.BaseTexture} tile={mask.TileMetres:0.0}m base_tris={mask.BaseTriangles} base_level={mask.BaseLevel} zone_groups={mask.ZoneLayers.Count} edge_tris={mask.EdgeTriangles} solid_tris={mask.SolidTriangles} wakes={mask.Wakes.Count} movers={movers.Count} moving_hulls={mask.MovingHulls.Count} unjudged={mask.UnjudgedMovers.Count} hull_tris={mask.MovingHullTriangles} ms={ms:0} bake={bakeMs:0} ({mask.Timing})");
        foreach (var hull in mask.MovingHulls)
        {
            var p = hull.GlobalPosition;
            var line = ocean.Waterline(hull);
            string size = line is { } l ? string.Create(CultureInfo.InvariantCulture, $"{l.Size.X:0}x{l.Size.Z:0}m") : "none";
            Log.Info("world", $"ocean: moving hull '{hull.Name}' at ({p.X:0},{p.Y:0.0},{p.Z:0}) waterline {size}");
        }
        foreach (var mover in mask.UnjudgedMovers)
            Log.Debug("world", $"ocean: mover '{mover.Name}' hidden at the bake, judged when shown");
        foreach (var wake in mask.Wakes)
        {
            if (wake.IsInsideTree() && wake is MeshInstance3D { Mesh: { } wakeMesh })
            {
                var p = wake.GlobalPosition;
                var hull = ocean.HullOf(wake);
                var zone = new OceanCalmZone(new Vector2(p.X, p.Z), Heading(hull));
                zone.Add(wakeMesh.GetAabb(), wake.GlobalTransform);
                var half = zone.HalfExtents;
                Log.Info("world", $"ocean: wake '{wake.GetParent()?.Name}/{wake.Name}' of '{hull.Name}' at ({p.X:0},{p.Y:0.0},{p.Z:0}) sheet {2f * half.X:0}x{2f * half.Y:0}m visible={wake.IsVisibleInTree()}");
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

    // The sheet hides only where this ocean's mask says it draws, so the mask goes live with it.
    public override void _EnterTree()
    {
        _mask.Publish();
        RenderingServer.GlobalShaderParameterSet(ParamName, 1.0f);
    }

    public override void _ExitTree()
    {
        RenderingServer.GlobalShaderParameterSet(ParamName, 0.0f);
        OceanMask.Withdraw();
    }

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

    private static string Literal(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    // vec4(dir.x, dir.z, k, A) and vec2(Q, omega) per wave. Omega is rounded to a whole number of
    // cycles per csky_time wrap, so the hourly rollover lands on an identical frame.
    // Also vec4 _MOD per wave: the height field's weights, then the phase field's. Each wave's weights
    // turn a golden angle on from the last, so no two waves follow the field alike. The _FINE
    // weights do the same on the fine field, scaled by how short the wave is. The chop's _MICRO
    // weights are its height and phase on the micro field.
    private static void EmitWaves(StringBuilder sb, string name, Wave[] waves, int count, int first, bool micro)
    {
        var a = new List<string>();
        var b = new List<string>();
        var m = new List<string>();
        var fine = new List<string>();
        var mic = new List<string>();
        for (int i = 1; i < waves.Length; i++)
        {
            if (waves[i].Length >= waves[i - 1].Length)
                throw new InvalidOperationException($"ocean: the {name} table must run longest first; the fragment's fade ends its sum on the first faded wave");
        }
        foreach (var w in waves)
        {
            float u = Mathf.DegToRad(137.50776f * (first + m.Count));
            float rad = Mathf.Min(PhaseMaxRad, Mathf.Tau / w.Length * PhaseMetres);
            m.Add($"vec4({Literal(GroupGain * Mathf.Cos(u))}, {Literal(GroupGain * Mathf.Sin(u))}, {Literal(rad * Mathf.Sin(u + 1f))}, {Literal(rad * Mathf.Cos(u + 1f))})");
            float s = Mathf.Clamp((FineFrom - w.Length) / (FineFrom - FineFull), 0f, 1f);
            float fineRad = s * Mathf.Min(PhaseMaxRad, Mathf.Tau / w.Length * FineMetres);
            float v = u + 2f;
            fine.Add($"vec4({Literal(s * FineGain * Mathf.Cos(v))}, {Literal(s * FineGain * Mathf.Sin(v))}, {Literal(fineRad * Mathf.Sin(v + 1f))}, {Literal(fineRad * Mathf.Cos(v + 1f))})");
            float microRad = Mathf.Min(PhaseMaxRad, Mathf.Tau / w.Length * MicroMetres);
            mic.Add($"vec2({Literal(MicroGain * Mathf.Cos(v + 2f))}, {Literal(microRad * Mathf.Sin(v + 2f))})");
            float ang = Mathf.DegToRad(WindDeg + w.AngleDeg);
            float k = Mathf.Tau / w.Length;
            float amp = w.Steepness / k;
            float omega = Mathf.Sqrt(9.81f * k);
            omega = Mathf.Tau * Mathf.Round(omega * (float)ShaderTime.RolloverSecs / Mathf.Tau) / (float)ShaderTime.RolloverSecs;
            float q = Choppiness / (k * amp * count);
            a.Add($"vec4({Literal(Mathf.Cos(ang))}, {Literal(Mathf.Sin(ang))}, {Literal(k)}, {Literal(amp)})");
            b.Add($"vec2({Literal(q)}, {Literal(omega)})");
        }
        sb.AppendLine($"const vec4 {name}_DKA[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", a)});");
        sb.AppendLine($"const vec2 {name}_QW[{waves.Length}] = vec2[{waves.Length}]({string.Join(", ", b)});");
        sb.AppendLine($"const vec4 {name}_MOD[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", m)});");
        sb.AppendLine($"const vec4 {name}_FINE[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", fine)});");
        if (micro)
            sb.AppendLine($"const vec2 {name}_MICRO[{waves.Length}] = vec2[{waves.Length}]({string.Join(", ", mic)});");
    }

    // The view-space scale that draws the ocean at priority level, in the bias shader's own step
    // (SceneBuilder.DepthBiasPerLevel), spelled as a shader literal.
    private static string DepthScale(int level) =>
        (1.0 - (level * (double)SceneBuilder.DepthBiasPerLevel)).ToString("0.0######", CultureInfo.InvariantCulture);

    private static string ShaderCode(int level, bool zoned)
    {
        // The height foam measures a crest against.
        float variance = 0f;
        foreach (var w in Swell)
            variance += 0.5f * Mathf.Pow(w.Steepness * w.Length / Mathf.Tau, 2f);
        float crestRef = CrestSigmas * Mathf.Sqrt(variance);
        var sb = new StringBuilder();
        sb.AppendLine("shader_type spatial;");
        sb.AppendLine("render_mode skip_vertex_transform, cull_disabled;");
        sb.AppendLine(SceneBuilder.TimeInclude);
        sb.AppendLine(SceneBuilder.AtmosphereInclude);
        sb.AppendLine(SceneBuilder.MipBiasInclude);
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
        // Per ship zone: centre X/Z and heading axis, then the half length, half width, margin, fade.
        sb.AppendLine($"uniform vec4 calm_axes[{MaxShips}];");
        sb.AppendLine($"uniform vec4 calm_sizes[{MaxShips}];");
        // A sheet split over zone layers gets one grid per group, each keeping only its own texels.
        if (zoned)
        {
            sb.AppendLine("uniform sampler2D zone_tex : filter_nearest, repeat_disable;");
            sb.AppendLine("instance uniform float zone_index = 0.0;");
        }
        EmitWaves(sb, "SWELL", Swell, Swell.Length, 0, false);
        EmitWaves(sb, "CHOP", Chop, Chop.Length, Swell.Length, true);
        sb.AppendLine($"const float SWELL_CREST = {Literal(crestRef)};");
        sb.AppendLine("varying vec2 v_param;");
        sb.AppendLine(@"
vec2 ocean_mask(vec2 p) {
    return textureLod(mask_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).rg;
}

// The swell height and the look left at the mask's shore distance g.
float shore_swell(float g) {
    return smoothstep(" + Literal(ShoreCalm / OceanMaskRaster.ShoreReach) + @", " + Literal(ShoreFull / OceanMaskRaster.ShoreReach) + @", g);
}

float shore_look(float g) {
    return smoothstep(" + Literal(LookCalm / OceanMaskRaster.ShoreReach) + @", " + Literal(LookFull / OceanMaskRaster.ShoreReach) + @", g);
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

// Two hashes in [0, 1] per integer cell, without sin, so it costs little per wave sum.
vec2 sea_hash(vec2 c) {
    vec3 p3 = fract(c.xyx * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

// Two smooth value-noise channels in [-1, 1].
vec2 sea_noise(vec2 p) {
    vec2 c = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(sea_hash(c), sea_hash(c + vec2(1.0, 0.0)), f.x),
        mix(sea_hash(c + vec2(0.0, 1.0)), sea_hash(c + vec2(1.0, 1.0)), f.x), f.y) * 2.0 - 1.0;
}

// The sea-state field at p: xy the grouping channels, zw the phase channels. Each octave is turned
// off the grid's axes so the noise cells do not line up with each other.
vec4 sea_state(vec2 p) {
    vec2 g = sea_noise(mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * " + Literal(1f / GroupScale) + @");
    vec2 c = sea_noise(mat2(vec2(0.6, -0.8), vec2(0.8, 0.6)) * p * " + Literal(1f / PhaseScaleCoarse) + @" + vec2(17.0, 5.0));
    vec2 f = sea_noise(mat2(vec2(0.28, 0.96), vec2(-0.96, 0.28)) * p * " + Literal(1f / PhaseScaleFine) + @" + vec2(-9.0, 31.0));
    return vec4(g, c * 0.6 + f * 0.4);
}

// The fine field at p, laid out as sea_state's.
vec4 sea_fine(vec2 p) {
    vec2 g = sea_noise(mat2(vec2(0.96, -0.28), vec2(0.28, 0.96)) * p * " + Literal(1f / FineGroupScale) + @" + vec2(41.0, -23.0));
    vec2 f = sea_noise(mat2(vec2(-0.6, 0.8), vec2(-0.8, -0.6)) * p * " + Literal(1f / FinePhaseScale) + @" + vec2(-57.0, 13.0));
    return vec4(g, f);
}

// The chop's micro field at p: x height, y phase.
vec2 sea_micro(vec2 p) {
    return sea_noise(mat2(vec2(0.8, -0.6), vec2(0.6, 0.8)) * p * " + Literal(1f / MicroScale) + @" + vec2(7.0, 61.0));
}

// One wave's height factor and phase shift from its weights on both fields.
vec2 sea_mod(vec4 coarse_w, vec4 fine_w, vec4 sea, vec4 fine) {
    return vec2(clamp(1.0 + dot(coarse_w.xy, sea.xy) + dot(fine_w.xy, fine.xy), 0.0, 1.8),
        dot(coarse_w.zw, sea.zw) + dot(fine_w.zw, fine.zw));
}

// OceanCalmZone.Distance and Calm, per ship: flat within the margin of the box, full height a fade
// further out.
float ship_calm(vec2 p) {
    float a = 1.0;
    for (int i = 0; i < " + MaxShips.ToString(CultureInfo.InvariantCulture) + @"; i++) {
        vec2 q = p - calm_axes[i].xy;
        vec2 u = calm_axes[i].zw;
        vec2 e = max(abs(vec2(dot(q, u), dot(q, vec2(-u.y, u.x)))) - calm_sizes[i].xy, vec2(0.0));
        a = min(a, smoothstep(calm_sizes[i].z, calm_sizes[i].z + calm_sizes[i].w, length(e)));
    }
    return a;
}

void vertex() {
    // A spyglass disc shows the flat sea. Every vertex goes to one point behind the eye, so the
    // clipper drops each triangle before a fragment and no wave is summed.
    if (" + SceneBuilder.FlatSeaEye + @") {
        VERTEX = vec3(0.0, 0.0, 1.0);
        v_param = vec2(0.0);
    } else {
        vec2 p = VERTEX.xz + CAMERA_POSITION_WORLD.xz;
        float spacing = max(UV.x, 0.01);
        float amp = shore_swell(ocean_mask(p).g) * ship_calm(p) * wave_scale;
        vec3 disp = vec3(0.0);
        vec4 sea = sea_state(p);
        vec4 fine = sea_fine(p);
        for (int i = 0; i < SWELL_DKA.length(); i++) {
            vec4 w = SWELL_DKA[i];
            float len = 6.2831853 / w.z;
            vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], sea, fine);
            // Full height at eight grid steps per wavelength, none at four: a wave the grid cannot
            // carry would crawl as the grid follows the eye.
            float g = amp * clamp(len / spacing * 0.25 - 1.0, 0.0, 1.0) * sm.x;
            float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time + sm.y;
            disp.xz += w.xy * (SWELL_QW[i].x * w.w * g * cos(th));
            disp.y += w.w * g * sin(th);
        }
        v_param = p;
        vec3 world = vec3(p.x + disp.x, disp.y, p.y + disp.z);
        VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
        // A priority level below the lowest base sheet. Every coplanar layer over or beside it
        // stays on top, the surf ring and C5's fog-gradient passes included.
        VERTEX *= " + DepthScale(level) + @";
        NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
    }
}

void fragment() {
    vec2 p = v_param;
    vec2 m = ocean_mask(p);
    vec3 world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // One-sided like the sheet it replaces: an eye below the surface sees through it.
    if (m.r < " + Literal(OceanMask.SeaThreshold) + @" || CAMERA_POSITION_WORLD.y < world.y) {
        discard;
    }" + (zoned ? @"
    float zone = textureLod(zone_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).r * 255.0;
    if (zone < " + Literal(OceanMaskRaster.AnyZone - 0.5f) + @" && abs(zone - zone_index) > 0.5) {
        discard;
    }" : "") + @"
    // A ship's zone and the shore's swell ramp flatten only the geometry. The normals keep the
    // swell and the chop, so the water there shades like the open sea; only the short look ramp
    // below calms them. A calm patch would read as an artefact.
    float look = shore_look(m.g);
    float amp = wave_scale;
    vec2 fw = fwidth(p);
    float footprint = max(max(fw.x, fw.y), 0.001);
    vec3 n = vec3(0.0, 1.0, 0.0);
    // The full swell height here, independent of how much of it the grid displaced, so the
    // foam reads the same at every distance.
    float h = 0.0;
    // The sea-state field's own slope is left out of the normals: it bends a crest by a few
    // per cent at most.
    vec4 sea = sea_state(p);
    vec4 fine = sea_fine(p);
    // The tables run longest first (EmitWaves checks), so the first wave the footprint fades out
    // ends the sum: every wave after it is shorter still.
    for (int i = 0; i < SWELL_DKA.length(); i++) {
        vec4 w = SWELL_DKA[i];
        float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", 6.2831853 / w.z / footprint);
        if (fade <= 0.0) {
            break;
        }
        vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], sea, fine);
        float f = amp * fade * sm.x;
        float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time + sm.y;
        float ka = w.z * w.w * f;
        n.xz -= w.xy * (ka * cos(th));
        n.y -= SWELL_QW[i].x * ka * sin(th);
        h += w.w * f * sin(th);
    }
    if (6.2831853 / CHOP_DKA[0].z / footprint > " + Literal(FootprintFadeFrom) + @") {
        vec2 micro = sea_micro(p);
        for (int i = 0; i < CHOP_DKA.length(); i++) {
            vec4 w = CHOP_DKA[i];
            float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", 6.2831853 / w.z / footprint);
            if (fade <= 0.0) {
                break;
            }
            vec2 sm = sea_mod(CHOP_MOD[i], CHOP_FINE[i], sea, fine) + CHOP_MICRO[i] * micro;
            float f = amp * fade * clamp(sm.x, 0.0, 1.8);
            float th = w.z * dot(w.xy, p) - CHOP_QW[i].y * csky_time + sm.y;
            float ka = w.z * w.w * f;
            n.xz -= w.xy * (ka * cos(th));
            n.y -= CHOP_QW[i].x * ka * sin(th);
        }
    }
    float fog_amt = csky_fog_amount(world, CAMERA_POSITION_WORLD);
    // How fully this fragment shades as the flat sheet does. Wholly at the shore, so no edge shows
    // against a coplanar coast tile. Rising with the fog, so the far sea fogs as the sheet does
    // instead of showing darker, wavier water through it.
    float sheet = max(1.0 - look, smoothstep(0.0, " + Literal(FogSheetAt) + @", fog_amt));
    n = normalize(mix(normalize(n), vec3(0.0, 1.0, 0.0), sheet));
    NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
    float dist = distance(world.xz, CAMERA_POSITION_WORLD.xz);
    vec3 tint = texture(tint_tex, (p - mask_rect.xy) * mask_rect.zw).rgb;
    vec3 col = mix(base_color, csky_sample_albedo(albedo_tex, p / tile_m).rgb, mix(detail_mix, 1.0, sheet)) * tint;
    // Crest height alone spreads whitecaps too evenly. A drifting patch field decides where they
    // can form and moves each crest's threshold.
    vec2 drift = SWELL_DKA[0].xy * (csky_time * 1.5);
    float patches = foam_noise((p - drift) / 230.0) * 0.65 + foam_noise((p - drift) / 71.0) * 0.35;
    float breakup = foam_noise((p - drift * 2.0) / 9.0);
    float crest = smoothstep(0.5, 0.85, h / max(SWELL_CREST * wave_scale, 0.01) + (patches - 0.55) * 0.5);
    float foam = crest * smoothstep(0.55, 0.8, patches) * smoothstep(0.25, 0.85, breakup);
    col = mix(col, vec3(0.6, 0.65, 0.68), foam * foam_strength * (1.0 - sheet));
    ALBEDO = col;
    METALLIC = 0.0;
    SPECULAR = " + Literal(SceneBuilder.WaterSpecular) + @";
    // Lost slope detail turns into roughness, so the far sea keeps a glossy sheen, not a mirror.
    ROUGHNESS = mix(mix(rough_near, rough_far, smoothstep(150.0, 4000.0, dist)), " + Literal(SceneBuilder.WaterRoughness) + @", sheet);
    FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
}");
        return sb.ToString();
    }

    // A ship's horizontal heading, its local +Z on the water.
    private static Vector2 Heading(Node3D hull)
    {
        var z = hull.GlobalBasis.Z;
        return new Vector2(z.X, z.Z);
    }

    // Whether a hull shows with its origin on sea level.
    private static bool Afloat(Node3D hull) =>
        IsInstanceValid(hull) && hull.IsInsideTree() && hull.IsVisibleInTree()
        && Mathf.Abs(hull.GlobalPosition.Y) <= HullWaterBand;

    // A visible wake sheet's mesh while the sheet lies on the water, else null.
    private static Mesh? WakeOnWater(Node3D wake) =>
        IsInstanceValid(wake) && wake.IsInsideTree() && wake.IsVisibleInTree() && wake is MeshInstance3D { Mesh: { } mesh }
        && Mathf.Abs(wake.GlobalPosition.Y) <= HullWaterBand ? mesh : null;

    // The hull a wake sheet trails: the sheet's mesh sits under its own node, which hangs off the
    // ship. A wake node straight under the world root is its own hull.
    private Node3D HullOf(Node3D wake)
    {
        var node = wake.GetParent() as Node3D ?? wake;
        return node.GetParent() is Node3D ship && ship != _worldRoot ? ship : node;
    }

    // One zone per ship on the water, over its waterline and its visible wake sheets, each frame;
    // an unused slot sits far away. With more ships afloat than slots, the nearest to the eye win.
    private void PublishShips()
    {
        _candidates.Clear();
        _candidateAt.Clear();
        _candidateWake.Clear();
        foreach (var hull in _ships())
            Offer(hull, wake: false);
        foreach (var hull in _mask.MovingHulls)
            Offer(hull, wake: false);
        foreach (var mover in _mask.UnjudgedMovers)
        {
            if (Afloat(mover) && Waterline(mover) != null && OverSea(mover.GlobalPosition))
                Offer(mover, wake: false);
        }
        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            if (Rides(_candidates[i]))
            {
                _candidates.RemoveAt(i);
                _candidateAt.RemoveAt(i);
                _candidateWake.RemoveAt(i);
            }
        }
        foreach (var wake in _wakes)
        {
            if (WakeOnWater(wake) is { } && ZoneHullOf(wake, _candidates, _candidates.Count) < 0)
                Offer(HullOf(wake), wake: true);
        }
        int n = 0;
        var eye = IsInsideTree() ? GetViewport()?.GetCamera3D()?.GlobalPosition : null;
        // Without a camera the first listed keep the slots: roster, moving, then wake hulls.
        OceanMovers.Nearest(_candidateAt, eye ?? Vector3.Zero, eye != null ? MaxShips : int.MaxValue, _order);
        if (_order.Count > MaxShips)
            _order.RemoveRange(MaxShips, _order.Count - MaxShips);
        foreach (int i in _order)
        {
            var hull = _candidates[i];
            int added = AddHull(hull, n);
            if (added == n && _candidateWake[i])
            {
                // A wake's ship keeps its zone wherever its own origin stands.
                var o = hull.GlobalPosition;
                _zones[n] = new OceanCalmZone(new Vector2(o.X, o.Z), Heading(hull));
                _zoneHulls[n] = hull;
                added = n + 1;
            }
            if (added > n && _zoned.Add(hull))
                Log.Debug("world", $"ocean: zone over '{hull.Name}' at ({_candidateAt[i].X:0},{_candidateAt[i].Y:0.0},{_candidateAt[i].Z:0}) of {_candidates.Count} afloat");
            n = added;
        }
        foreach (var wake in _wakes)
        {
            if (WakeOnWater(wake) is { } mesh && ZoneHullOf(wake, _zoneHulls, n) is var z and >= 0)
                _zones[z].Add(mesh.GetAabb(), wake.GlobalTransform);
        }
        for (int i = 0; i < MaxShips; i++)
        {
            if (i < n)
            {
                var c = _zones[i].Center;
                var axis = _zones[i].Axis;
                var half = _zones[i].HalfExtents;
                _calmAxes[i] = new Vector4(c.X, c.Y, axis.X, axis.Y);
                _calmSizes[i] = new Vector4(half.X, half.Y, CalmMargin, CalmFade);
            }
            else
            {
                _calmAxes[i] = new Vector4(1e9f, 1e9f, 1f, 0f);
                _calmSizes[i] = new Vector4(0f, 0f, 0f, 1f);
            }
            _zoneHulls[i] = null;
        }
        _material.SetShaderParameter("calm_axes", _calmAxes);
        _material.SetShaderParameter("calm_sizes", _calmSizes);
    }

    // Opens a zone over a visible hull on the water and its waterline; returns the zones in use.
    private int AddHull(Node3D hull, int n)
    {
        if (n >= MaxShips || !IsInstanceValid(hull) || !hull.IsInsideTree() || !hull.IsVisibleInTree()
            || Array.IndexOf(_zoneHulls, hull, 0, n) >= 0)
            return n;
        var xf = hull.GlobalTransform;
        if (Mathf.Abs(xf.Origin.Y) > HullWaterBand)
            return n;
        _zones[n] = new OceanCalmZone(new Vector2(xf.Origin.X, xf.Origin.Z), Heading(hull));
        if (Waterline(hull) is { } line)
            _zones[n].Add(line, xf);
        else
            _zones[n].Add(new Vector2(xf.Origin.X, xf.Origin.Z));
        _zoneHulls[n] = hull;
        return n + 1;
    }

    // The union of the hull's meshes that reach its waterline, in the hull's own frame, measured
    // once. The wake sheets are left out; they move on their own and are added each frame.
    private Aabb? Waterline(Node3D hull)
    {
        if (_waterlines.TryGetValue(hull, out var kept))
            return kept;
        Aabb? line = null;
        var toHull = hull.GlobalTransform.AffineInverse();
        var stack = new Stack<Node>();
        stack.Push(hull);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var child in node.GetChildren())
                stack.Push(child);
            if (node is not MeshInstance3D { Mesh: { } mesh } mi || !mi.IsVisibleInTree() || _wakes.Contains(mi))
                continue;
            var box = toHull * mi.GlobalTransform * mesh.GetAabb();
            if (box.Position.Y > WaterlineBand || box.End.Y < -WaterlineBand)
                continue;
            line = line is { } l ? l.Merge(box) : box;
        }
        _waterlines[hull] = line;
        return line;
    }

    // Lists a ship for a zone this frame, once. A wake's ship needs no origin on the water.
    private void Offer(Node3D hull, bool wake)
    {
        if (!IsInstanceValid(hull) || !hull.IsInsideTree() || (!wake && !Afloat(hull)) || _candidates.Contains(hull))
            return;
        _candidates.Add(hull);
        _candidateAt.Add(hull.GlobalPosition);
        _candidateWake.Add(wake);
    }

    // The first of hulls[0..count) that is the wake's ship or holds the sheet, else -1.
    private int ZoneHullOf(Node3D wake, IReadOnlyList<Node3D?> hulls, int count)
    {
        var ship = HullOf(wake);
        for (int i = 0; i < count; i++)
        {
            if (hulls[i] is { } h && (h == ship || h.IsAncestorOf(wake)))
                return i;
        }
        return -1;
    }

    // Whether a hull rides inside another listed hull where it stood when first seen there, which
    // that hull's waterline already covers. A part that moves off on its own keeps a zone.
    private bool Rides(Node3D inner)
    {
        foreach (var outer in _candidates)
        {
            if (outer == inner || !outer.IsAncestorOf(inner))
                continue;
            var rel = outer.GlobalTransform.AffineInverse() * inner.GlobalTransform;
            if (!_nestedRest.TryGetValue(inner, out var rest))
            {
                _nestedRest[inner] = rel;
                return true;
            }
            return rel.Origin.DistanceTo(rest.Origin) < 0.5f && rel.Basis.Z.Normalized().Dot(rest.Basis.Z.Normalized()) > 0.999f;
        }
        return false;
    }

    // Whether the mask counts the texel under a world point as sea.
    private bool OverSea(Vector3 p)
    {
        int x = (int)Math.Floor((p.X - _mask.Origin.X) / OceanMask.Cell);
        int y = (int)Math.Floor((p.Z - _mask.Origin.Y) / OceanMask.Cell);
        return x >= 0 && y >= 0 && x < _mask.Width && y < _mask.Height
            && _mask.Image.GetPixel(x, y).R >= OceanMask.SeaThreshold;
    }

    // One Gerstner component: direction, wavelength in metres, steepness k*A.
    private readonly record struct Wave(float AngleDeg, float Length, float Steepness);
}
