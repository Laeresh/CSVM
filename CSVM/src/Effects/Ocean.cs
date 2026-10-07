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
    // two noise fields along its own direction: one scales its height, one warps its phase. A long
    // wave's crests shift up to PhaseMetres, a short wave's at most PhaseMaxRad, so crossing crests
    // bend apart. The warp's slope is part of each wave's local wavelength, which the fades and the
    // normals read. The field holds still in world space, so the csky_time wrap lands on an
    // identical frame.
    private const float GroupScale = 500f;
    private const float GroupGain = 0.8f;
    private const float PhaseScaleCoarse = 360f;
    private const float PhaseScaleFine = 90f;
    private const float PhaseFineShare = 0.5f;
    private const float PhaseMetres = 60f;
    private const float PhaseMaxRad = 14f;

    // The longest a warped wave counts as, in its own lengths, when the fades read it. The bound
    // keeps the tables' longest-first order valid for every later wave's local length.
    private const float WarpStretchMax = 1.5f;

    // TUNE. A finer field for the short waves, whose crests the coarse one leaves as a straight
    // grain. A wave feels it fully at FineFull metres and shorter, not at all from FineFrom up, so
    // the long swell keeps its shape.
    private const float FineGroupScale = 40f;
    private const float FinePhaseScale = 28f;
    private const float FineGain = 1.2f;
    private const float FineMetres = 5f;
    private const float FineFrom = 40f;
    private const float FineFull = 10f;

    // TUNE. The fine detail's slope per layer, and the cells a layer's wavelength is taken as for the
    // footprint fade. Gradient noise puts most of its energy near two cells.
    private const float DetailSlope = 0.08f;
    private const float DetailWaveCells = 2f;

    // The cells along a detail layer's drift axis after which its noise repeats, so the drift
    // wraps with csky_time. At the shortest cell it is still hundreds of metres.
    private const int DetailPeriod = 1024;

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
    };

    // TUNE. The fine detail the fragment normals add on top of the swell: gradient-noise layers, each
    // drifting along its own direction. Noise has no fixed crests, so crossing layers print no weave.
    // Cell in metres, longest first; speed in m/s, rounded so the csky_time wrap lands on a whole
    // DetailPeriod of cells.
    private static readonly Detail[] DetailLayers =
    {
        new(-38f, 6f, 2.2f), new(57f, 3.4f, 1.6f), new(-71f, 1.9f, 1.2f), new(14f, 1.05f, 0.9f),
        new(-20f, 0.58f, 0.6f),
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

        bool zoned = mask.ZoneTexture != null;
        var shader = new Shader { Code = ShaderCode(mask.BaseLevel - 1, zoned) };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mask_tex", mask.Texture);
        material.SetShaderParameter("mask_rect", new Vector4(mask.Origin.X, mask.Origin.Y, 1f / mask.Size.X, 1f / mask.Size.Y));
        if (baseTex != null)
            material.SetShaderParameter("albedo_tex", baseTex);
        material.SetShaderParameter("tile_m", mask.TileMetres);
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

    // vec4(drift dir.x, dir.z, 1 / cell, drift in cells per second) per detail layer, and vec2
    // grouping weights, a golden angle on per layer. The drift is a whole number of DetailPeriods
    // per csky_time wrap, so the hourly rollover lands on an identical frame.
    private static void EmitDetail(StringBuilder sb)
    {
        var l = new List<string>();
        var g = new List<string>();
        for (int i = 0; i < DetailLayers.Length; i++)
        {
            var d = DetailLayers[i];
            if (i > 0 && d.Cell >= DetailLayers[i - 1].Cell)
                throw new InvalidOperationException("ocean: the detail table must run longest first; the fragment's fade ends its sum on the first faded layer");
            float ang = Mathf.DegToRad(WindDeg + d.AngleDeg);
            double turns = Math.Max(1.0, Math.Round(d.Speed * ShaderTime.RolloverSecs / (DetailPeriod * d.Cell)));
            float cellsPerSec = (float)(turns * DetailPeriod / ShaderTime.RolloverSecs);
            l.Add($"vec4({Literal(Mathf.Cos(ang))}, {Literal(Mathf.Sin(ang))}, {Literal(1f / d.Cell)}, {Literal(cellsPerSec)})");
            float u = Mathf.DegToRad(137.50776f * (i + Swell.Length));
            g.Add($"vec2({Literal(GroupGain * Mathf.Cos(u))}, {Literal(GroupGain * Mathf.Sin(u))})");
        }
        sb.AppendLine($"const vec4 DETAIL_L[{l.Count}] = vec4[{l.Count}]({string.Join(", ", l)});");
        sb.AppendLine($"const vec2 DETAIL_G[{g.Count}] = vec2[{g.Count}]({string.Join(", ", g)});");
    }

    // vec4(dir.x, dir.z, k, A) and vec2(Q, omega) per wave. Omega is rounded to a whole number of
    // cycles per csky_time wrap, so the hourly rollover lands on an identical frame.
    // Also vec4 _MOD per wave: the height field's weights, then the phase field's. Each wave's weights
    // turn a golden angle on from the last, so no two waves follow the field alike. The _FINE
    // weights do the same on the fine field, scaled by how short the wave is.
    private static void EmitWaves(StringBuilder sb, string name, Wave[] waves)
    {
        var a = new List<string>();
        var b = new List<string>();
        var m = new List<string>();
        var fine = new List<string>();
        for (int i = 1; i < waves.Length; i++)
        {
            if (waves[i].Length >= waves[i - 1].Length)
                throw new InvalidOperationException($"ocean: the {name} table must run longest first; the fragment's fade ends its sum on the first faded wave");
        }
        foreach (var w in waves)
        {
            float u = Mathf.DegToRad(137.50776f * m.Count);
            float rad = Mathf.Min(PhaseMaxRad, Mathf.Tau / w.Length * PhaseMetres);
            m.Add($"vec4({Literal(GroupGain * Mathf.Cos(u))}, {Literal(GroupGain * Mathf.Sin(u))}, {Literal(rad * Mathf.Sin(u + 1f))}, {Literal(rad * Mathf.Cos(u + 1f))})");
            float s = Mathf.Clamp((FineFrom - w.Length) / (FineFrom - FineFull), 0f, 1f);
            float fineRad = s * Mathf.Min(PhaseMaxRad, Mathf.Tau / w.Length * FineMetres);
            float v = u + 2f;
            fine.Add($"vec4({Literal(s * FineGain * Mathf.Cos(v))}, {Literal(s * FineGain * Mathf.Sin(v))}, {Literal(fineRad * Mathf.Sin(v + 1f))}, {Literal(fineRad * Mathf.Cos(v + 1f))})");
            float ang = Mathf.DegToRad(WindDeg + w.AngleDeg);
            float k = Mathf.Tau / w.Length;
            float amp = w.Steepness / k;
            float omega = Mathf.Sqrt(9.81f * k);
            omega = Mathf.Tau * Mathf.Round(omega * (float)ShaderTime.RolloverSecs / Mathf.Tau) / (float)ShaderTime.RolloverSecs;
            float q = Choppiness / (k * amp * waves.Length);
            a.Add($"vec4({Literal(Mathf.Cos(ang))}, {Literal(Mathf.Sin(ang))}, {Literal(k)}, {Literal(amp)})");
            b.Add($"vec2({Literal(q)}, {Literal(omega)})");
        }
        sb.AppendLine($"const vec4 {name}_DKA[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", a)});");
        sb.AppendLine($"const vec2 {name}_QW[{waves.Length}] = vec2[{waves.Length}]({string.Join(", ", b)});");
        sb.AppendLine($"const vec4 {name}_MOD[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", m)});");
        sb.AppendLine($"const vec4 {name}_FINE[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", fine)});");
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
        sb.AppendLine("uniform sampler2D tint_tex : source_color, filter_linear, repeat_disable;");
        sb.AppendLine("uniform float rough_near = 0.2;");
        sb.AppendLine("uniform float rough_far = 0.3;");
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
        EmitWaves(sb, "SWELL", Swell);
        EmitDetail(sb);
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

// sea_noise with its slope: d's columns are the two channels' change along x and along y.
vec2 sea_noise_d(vec2 p, out mat2 d) {
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 du = 6.0 * f * (1.0 - f);
    vec2 a = sea_hash(c);
    vec2 k1 = sea_hash(c + vec2(1.0, 0.0)) - a;
    vec2 k2 = sea_hash(c + vec2(0.0, 1.0)) - a;
    vec2 k3 = sea_hash(c + vec2(1.0, 1.0)) - a - k1 - k2;
    d = mat2(2.0 * du.x * (k1 + k3 * u.y), 2.0 * du.y * (k2 + k3 * u.x));
    return (a + k1 * u.x + k2 * u.y + k3 * (u.x * u.y)) * 2.0 - 1.0;
}

// The grouping channels at p. Each field is turned off the grid's axes so the noise cells do not
// line up with each other.
vec2 sea_group(vec2 p) {
    return sea_noise(mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * " + Literal(1f / GroupScale) + @");
}

// The phase (warp) channels at p in xy, and its fine octave alone in zw. The out matrix is the
// slope of xy per metre. A wave's local wavenumber is its own plus its phase weights times it.
vec4 sea_warp(vec2 p, out mat2 d) {
    const mat2 rc = mat2(vec2(0.6, -0.8), vec2(0.8, 0.6));
    const mat2 rf = mat2(vec2(0.28, 0.96), vec2(-0.96, 0.28));
    mat2 dc;
    mat2 df;
    vec2 c = sea_noise_d(rc * p * " + Literal(1f / PhaseScaleCoarse) + @" + vec2(17.0, 5.0), dc);
    vec2 f = sea_noise_d(rf * p * " + Literal(1f / PhaseScaleFine) + @" + vec2(-9.0, 31.0), df);
    d = dc * rc * " + Literal((1f - PhaseFineShare) / PhaseScaleCoarse) + @" + df * rf * " + Literal(PhaseFineShare / PhaseScaleFine) + @";
    return vec4(c * " + Literal(1f - PhaseFineShare) + @" + f * " + Literal(PhaseFineShare) + @", f);
}

// A wave's local wavenumber vector: its own plus the warp's slope along its phase weights. Its
// length is capped at WarpStretchMax times the wave's own, so the fades keep their order.
vec2 sea_wavevector(vec4 w, vec4 coarse_w, mat2 d) {
    vec2 k = w.z * w.xy + coarse_w.zw * d;
    return k * max(1.0, w.z / (" + Literal(WarpStretchMax) + @" * max(length(k), 1e-4)));
}

// The fine field at p: xy its grouping channels, zw its phase channels.
vec4 sea_fine(vec2 p) {
    vec2 g = sea_noise(mat2(vec2(0.96, -0.28), vec2(0.28, 0.96)) * p * " + Literal(1f / FineGroupScale) + @" + vec2(41.0, -23.0));
    vec2 f = sea_noise(mat2(vec2(-0.6, 0.8), vec2(-0.8, -0.6)) * p * " + Literal(1f / FinePhaseScale) + @" + vec2(-57.0, 13.0));
    return vec4(g, f);
}

// Two gradients in [-1, 1] per integer cell. The cell's x is taken modulo DetailPeriod, so a
// layer's noise repeats along its drift axis and its drift wraps with csky_time.
vec2 detail_hash(vec2 c) {
    return sea_hash(vec2(mod(c.x, " + DetailPeriod.ToString(CultureInfo.InvariantCulture) + @".0), c.y)) * 2.0 - 1.0;
}

// Gradient noise at q: x the value, yz its slope along q's axes.
vec3 detail_noise(vec2 q) {
    vec2 i = floor(q);
    vec2 f = fract(q);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
    vec2 ga = detail_hash(i);
    vec2 gb = detail_hash(i + vec2(1.0, 0.0));
    vec2 gc = detail_hash(i + vec2(0.0, 1.0));
    vec2 gd = detail_hash(i + vec2(1.0, 1.0));
    float va = dot(ga, f);
    float vb = dot(gb, f - vec2(1.0, 0.0));
    float vc = dot(gc, f - vec2(0.0, 1.0));
    float vd = dot(gd, f - vec2(1.0, 1.0));
    float k = va - vb - vc + vd;
    return vec3(va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * k,
        ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd) + du * (u.yx * k + vec2(vb, vc) - va));
}

// One wave's height factor and phase shift from its weights on both fields.
vec2 sea_mod(vec4 coarse_w, vec4 fine_w, vec2 group, vec2 warp, vec4 fine) {
    return vec2(clamp(1.0 + dot(coarse_w.xy, group) + dot(fine_w.xy, fine.xy), 0.0, 1.8),
        dot(coarse_w.zw, warp) + dot(fine_w.zw, fine.zw));
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
        vec2 group = sea_group(p);
        mat2 dwarp;
        vec4 warp = sea_warp(p, dwarp);
        vec4 fine = sea_fine(p);
        for (int i = 0; i < SWELL_DKA.length(); i++) {
            vec4 w = SWELL_DKA[i];
            float len = 6.2831853 / length(sea_wavevector(w, SWELL_MOD[i], dwarp));
            vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], group, warp.xy, fine);
            // Full height at eight grid steps per local wavelength, none at four. A wave the grid
            // cannot carry would crawl as the grid follows the eye.
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
    // swell and the fine detail, so the water there shades like the open sea. Only the short look
    // ramp below calms them. A calm patch would read as an artefact.
    float look = shore_look(m.g);
    float amp = wave_scale;
    vec2 fw = fwidth(p);
    float footprint = max(max(fw.x, fw.y), 0.001);
    vec3 n = vec3(0.0, 1.0, 0.0);
    // The full swell height here, independent of how much of it the grid displaced, so the
    // foam reads the same at every distance.
    float h = 0.0;
    // The warp's slope is in each wave's normal, as the vertex stage's fade reads it. The grouping
    // and fine fields' slopes are left out: they bend a crest by a few per cent at most.
    vec2 group = sea_group(p);
    mat2 dwarp;
    vec4 warp = sea_warp(p, dwarp);
    vec4 fine = sea_fine(p);
    // The tables run longest first (EmitWaves checks), and no local length passes WarpStretchMax
    // times the wave's own. Once that bound fades out, every later wave fades too.
    for (int i = 0; i < SWELL_DKA.length(); i++) {
        vec4 w = SWELL_DKA[i];
        float len = 6.2831853 / w.z;
        if (len * " + Literal(WarpStretchMax) + @" <= footprint * " + Literal(FootprintFadeFrom) + @") {
            break;
        }
        vec2 kv = sea_wavevector(w, SWELL_MOD[i], dwarp);
        float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", 6.2831853 / length(kv) / footprint);
        vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], group, warp.xy, fine);
        float f = amp * fade * sm.x;
        float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time + sm.y;
        float ka = w.z * w.w * f;
        n.xz -= kv * (w.w * f * cos(th));
        n.y -= SWELL_QW[i].x * ka * sin(th);
        h += w.w * f * sin(th);
    }
    // The detail layers run longest first (EmitDetail checks), so the first faded layer ends the sum.
    // A layer's height is DetailSlope times its cell times the noise, the same slope at every cell
    // size. The sea-state grouping varies it by region.
    for (int i = 0; i < DETAIL_L.length(); i++) {
        vec4 l = DETAIL_L[i];
        float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", " + Literal(DetailWaveCells) + @" / l.z / footprint);
        if (fade <= 0.0) {
            break;
        }
        vec2 side = vec2(-l.y, l.x);
        vec2 q = vec2(dot(p, l.xy), dot(p, side)) * l.z - vec2(l.w * csky_time, 0.0);
        vec3 nd = detail_noise(q);
        float f = amp * fade * " + Literal(DetailSlope) + @" * clamp(1.0 + dot(DETAIL_G[i], group), 0.2, 1.8);
        n.xz -= (l.xy * nd.y + side * nd.z) * f;
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
    // The open sea takes the texture's mean and the light alone varies it: a tiled texture reads as
    // a lattice from the air. The last mip is the mean the GPU samples. At sheet = 1 the texture is
    // the sheet's own mapping, so C25's seam holds.
    vec3 tex_mean = textureLod(albedo_tex, vec2(0.5), 16.0).rgb;
    vec3 col = mix(tex_mean, csky_sample_albedo(albedo_tex, p / tile_m).rgb, sheet) * tint;
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

    // One fine-detail noise layer: drift direction off the wind, cell size in metres, drift in m/s.
    private readonly record struct Detail(float AngleDeg, float Cell, float Speed);
}
