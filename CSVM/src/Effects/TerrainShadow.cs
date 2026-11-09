using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The terrain's baked sun shadow under Enhanced, one texture per distinct zone sun. Per texel
/// (<see cref="Cell"/> metres) it holds the height of the sun ray grazing the shadowless ground
/// upstream along the sun's azimuth. Beside it lies the distance to that ground
/// (<see cref="TerrainShadowRaster"/>). The lit arms' <c>light()</c> scale the sun by it through
/// the <c>csky_terrain_shadow_*</c> globals (<see cref="SceneBuilder.TerrainShadowLight"/>).
/// Godot's shadow maps, which the ground stays out of, compose on top. In the tree it publishes
/// the live zone's texture and follows a Shadow Quality apply; leaving it puts the defaults back.
/// </summary>
internal sealed partial class TerrainShadow : Node
{
    // How long a bearing no load baked must stand before it is baked. A dragged lab slider then
    // bakes once where it stops.
    private const double OnDemandSettleSec = 0.25;

    // The globals csky_terrain_shadow.gdshaderinc declares: the texture, its world rect, the penumbra's
    // growth per metre of blocker distance and its floor, and the shade's strength.
    private static readonly StringName MapParam = "csky_terrain_shadow";
    private static readonly StringName RectParam = "csky_terrain_shadow_rect";
    private static readonly StringName SoftParam = "csky_terrain_shadow_soft";
    private static readonly StringName StrengthParam = "csky_terrain_shadow_strength";

    // What the globals hold with no bake live: one texel far below any world, so everything reads lit.
    private static ImageTexture? _none;
    private static float _strength = 1f;

    private readonly Dictionary<Vector3, Baked> _baked = new();
    private Vector3[] _tris = Array.Empty<Vector3>();
    private Vector3 _orientation;
    private int _revision;
    // The bearing waiting for its on-demand bake, how long it has stood, and the last such bake. The
    // next one drops it, so a lab session holds one extra texture at most.
    private Vector3? _pending;
    private double _pendingSec;
    private Vector3? _onDemand;

    private TerrainShadow()
    {
    }

    /// <summary>The texel edge in metres.</summary>
    public static float Cell => TerrainShadowRaster.Cell;

    /// <summary>The bake whose texture the globals carry, null while the defaults stand.</summary>
    public static TerrainShadow? Live { get; private set; }

    /// <summary>Gets or sets the share of the sun the bake takes under full shade, 1 by default
    /// (<c>WeatherRig.ShadowStrength</c>). It writes the strength global and keeps the
    /// value for <see cref="SunAt"/>, since the renderer reads no global back outside the editor.</summary>
    public static float Strength
    {
        get => _strength;
        set
        {
            _strength = value;
            RenderingServer.GlobalShaderParameterSet(StrengthParam, value);
        }
    }

    /// <summary>The world X/Z of the first texel's corner.</summary>
    public Vector2 Origin { get; private set; }

    /// <summary>The extent in metres along X and Z.</summary>
    public Vector2 Size { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The bake's milliseconds per orientation, for the build log line.</summary>
    public string Timing { get; private set; } = "";

    /// <summary>Declares the globals with their defaults. Must run before the first shader that
    /// reads them is built, beside <see cref="Ocean.RegisterGlobal"/>.</summary>
    public static void RegisterGlobals()
    {
        _none ??= TextureUpload.Create(1, 1, Image.Format.Rgh, Halves(new[] { TerrainShadowRaster.Empty }, new[] { 0f }));
        RenderingServer.GlobalShaderParameterAdd(MapParam, RenderingServer.GlobalShaderParameterType.Sampler2D, _none);
        RenderingServer.GlobalShaderParameterAdd(RectParam, RenderingServer.GlobalShaderParameterType.Vec4, new Vector4(0f, 0f, 1f, 1f));
        RenderingServer.GlobalShaderParameterAdd(SoftParam, RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(0f, 1f));
        RenderingServer.GlobalShaderParameterAdd(StrengthParam, RenderingServer.GlobalShaderParameterType.Float, _strength);
    }

    /// <summary>Puts the defaults back, so every lit fragment reads lit.</summary>
    public static void Withdraw()
    {
        Live = null;
        if (_none == null)
            return;
        RenderingServer.GlobalShaderParameterSet(MapParam, _none);
        RenderingServer.GlobalShaderParameterSet(RectParam, new Vector4(0f, 0f, 1f, 1f));
        RenderingServer.GlobalShaderParameterSet(SoftParam, new Vector2(0f, 1f));
    }

    /// <summary>Bakes the shadow of <paramref name="scene"/>'s shadowless ground meshes for every
    /// distinct orientation in <paramref name="orientations"/> (a sun's Euler rotation), or null when
    /// the world has none. A non-empty <paramref name="png"/> receives each bake as an 8-bit picture:
    /// R the ground over 2048 m, G the line, B the blocker distance.</summary>
    public static TerrainShadow? Bake(SceneBuilder scene, IEnumerable<Vector3> orientations, string png)
    {
        long t0 = Stopwatch.GetTimestamp();
        var tris = Triangles(scene);
        // ⚠ Do not drop this collection. Reading MeshInstance3D.Mesh mints a managed wrapper per mesh
        // (see OceanMask.Bake), and a short run that quits first aborts on Godot's exit check.
        GC.Collect();
        if (tris.Length == 0)
            return null;
        var result = new TerrainShadow { Name = "TerrainShadow", _tris = tris };
        var timing = new System.Text.StringBuilder(string.Create(CultureInfo.InvariantCulture, $"tris={tris.Length / 3} collect={OceanMask.Lap(ref t0):0}"));
        int k = 0;
        foreach (var orientation in orientations)
        {
            if (result._baked.ContainsKey(orientation))
                continue;
            var raster = result.BakeOne(orientation);
            if (png.Length > 0)
                Dump(raster, k == 0 ? png : System.IO.Path.ChangeExtension(png, $".{k}.png"));
            timing.Append(string.Create(CultureInfo.InvariantCulture,
                $" [{Mathf.RadToDeg(orientation.X):0}/{Mathf.RadToDeg(orientation.Y):0}: fill={raster.FillMs:0} sweep={raster.SweepMs:0} upload={OceanMask.Lap(ref t0):0}]"));
            k++;
        }
        result.Timing = timing.ToString();
        return result;
    }

    /// <summary>Hands the texture baked for <paramref name="orientation"/> to the globals, or the
    /// defaults when the sun casts no shadow at the resolved quality or no bake matches. The
    /// penumbra follows the level's angular distance, hard under <c>--no-soft-shadows</c>.</summary>
    public void Publish(Vector3 orientation)
    {
        _orientation = orientation;
        _revision = ShadowQualitySetting.Revision;
        if (_none == null)
            return;
        Live = this;
        if (!ShadowQualitySetting.Sun.Cast || !_baked.TryGetValue(orientation, out var baked))
        {
            RenderingServer.GlobalShaderParameterSet(MapParam, _none);
            // A bearing no load baked (a lab's sun offset, a re-aimed light) bakes once it settles.
            if (ShadowQualitySetting.Sun.Cast && _tris.Length > 0)
            {
                _pending = orientation;
                _pendingSec = 0.0;
            }
            return;
        }
        _pending = null;
        RenderingServer.GlobalShaderParameterSet(MapParam, baked.Texture);
        RenderingServer.GlobalShaderParameterSet(RectParam, new Vector4(Origin.X, Origin.Y, 1f / Size.X, 1f / Size.Y));
        RenderingServer.GlobalShaderParameterSet(SoftParam, Soft(baked));
    }

    /// <summary>The sun's share at a world point under the published orientation, 1 lit, 1 - <see cref="Strength"/>
    /// shaded, read as <c>csky_terrain_baked_sun</c> reads the texture. Off the rect, or with the defaults
    /// published for the orientation, 1.</summary>
    public float SunAt(Vector3 world)
    {
        if (!ShadowQualitySetting.Sun.Cast || !_baked.TryGetValue(_orientation, out var baked))
            return 1f;
        float u = (world.X - Origin.X) / Size.X, v = (world.Z - Origin.Y) / Size.Y;
        if (u < 0f || v < 0f || u > 1f || v > 1f)
            return 1f;
        float tx = (u * Width) - 0.5f, ty = (v * Height) - 0.5f;
        int ix = (int)Math.Floor(tx), iy = (int)Math.Floor(ty);
        float fx = tx - ix, fy = ty - iy;
        var image = baked.Texture.GetImage();
        var a = Texel(image, ix, iy);
        var b = Texel(image, ix + 1, iy);
        var c = Texel(image, ix, iy + 1);
        var d = Texel(image, ix + 1, iy + 1);
        var texel = a.Lerp(b, fx).Lerp(c.Lerp(d, fx), fy);
        var soft = Soft(baked);
        return TerrainShadowRaster.Lit(texel.X, texel.Y, world.Y, soft.X, soft.Y, Strength);
    }

    public override void _EnterTree() => Publish(_orientation);

    public override void _ExitTree()
    {
        if (Live == this)
            Withdraw();
    }

    // A Shadow Quality apply moves the revision. The level's angular distance and whether it casts
    // reach the globals from here, so Utils never names Effects. A withdrawn bake stays withdrawn.
    public override void _Process(double delta)
    {
        if (Live == this && _revision != ShadowQualitySetting.Revision)
            Publish(_orientation);
        if (Live != this || _pending is not { } pending || pending != _orientation)
            return;
        _pendingSec += delta;
        if (_pendingSec < OnDemandSettleSec)
            return;
        long t0 = Stopwatch.GetTimestamp();
        if (_onDemand is { } previous && _baked.Remove(previous, out var dropped))
            dropped.Texture.Dispose();
        BakeOne(pending);
        _onDemand = pending;
        Log.Info("world", $"terrain shadow: baked on demand for {Mathf.RadToDeg(pending.X):0.#}/{Mathf.RadToDeg(pending.Y):0.#} in {Stopwatch.GetElapsedTime(t0).TotalMilliseconds:0} ms");
        Publish(pending);
    }

    // The penumbra's growth in height per metre of blocker distance under the level's angular
    // distance, and the one-texel floor. The floor is the line's own drop over one texel. A disc
    // of half-angle a around a sun at elevation e spans tan(e + a) - tan(e - a) in slope. That is
    // 2 tan(a) (1 + tan(e)^2) to first order, the second factor turning the horizontal width into a height.
    private static Vector2 Soft(Baked baked)
    {
        float angular = ShadowQualitySetting.HardShadows ? 0f : ShadowQualitySetting.Sun.AngularDistance * ShadowQualitySetting.Softness;
        var sun = baked.Sun;
        float horizontal = MathF.Max(MathF.Sqrt((sun.X * sun.X) + (sun.Z * sun.Z)), 1e-4f);
        float tan = sun.Y / horizontal;
        return new Vector2(MathF.Tan(Mathf.DegToRad(angular)) * (1f + (tan * tan)), Cell * MathF.Max(tan, 1e-3f));
    }

    private static Vector2 Texel(Image image, int x, int y)
    {
        var c = image.GetPixel(Math.Clamp(x, 0, image.GetWidth() - 1), Math.Clamp(y, 0, image.GetHeight() - 1));
        return new Vector2(c.R, c.G);
    }

    // ponytail: a half holds the line to 0.5 m above 512 m and 1 m above 1024 m. Go to RGF if high
    // ground shows acne.

    // RGH texel bytes: the line in R, the distance in G.
    private static byte[] Halves(float[] line, float[] distance)
    {
        var bytes = new byte[line.Length * 4];
        var halves = MemoryMarshal.Cast<byte, Half>(bytes);
        for (int i = 0; i < line.Length; i++)
        {
            halves[2 * i] = (Half)line[i];
            halves[(2 * i) + 1] = (Half)distance[i];
        }
        return bytes;
    }

    private static void Dump(TerrainShadowRaster.Result raster, string path)
    {
        var bytes = new byte[raster.Width * raster.Height * 3];
        for (int i = 0; i < raster.Line.Length; i++)
        {
            bytes[3 * i] = (byte)Math.Clamp(raster.Ground[i] / 2048f * 255f, 0f, 255f);
            bytes[(3 * i) + 1] = (byte)Math.Clamp(raster.Line[i] / 2048f * 255f, 0f, 255f);
            bytes[(3 * i) + 2] = (byte)Math.Clamp(raster.Distance[i] / 2048f * 255f, 0f, 255f);
        }
        var err = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgb8, bytes).SavePng(path);
        if (err == Error.Ok)
            Log.Info("world", $"terrain shadow: written to {path}");
        else
            Log.Error("world", $"--dump-terrain-shadow: could not write {path} ({err})");
    }

    // The world-space corners of every surface of the meshes the builder set casting no shadow, but
    // the sea's. The flat sheet cast its line one texel's drop under sea level. A wave trough below that
    // read as shaded, a dark patch on open water.
    private static Vector3[] Triangles(SceneBuilder scene)
    {
        var sea = new HashSet<Material>();
        foreach (var (material, texture) in scene.TexturedMaterials)
        {
            if (IsSea(texture))
                sea.Add(material);
        }
        var tris = new List<Vector3>();
        foreach (var mi in scene.ShadowlessMeshes)
        {
            if (!GodotObject.IsInstanceValid(mi) || !mi.IsInsideTree() || !mi.IsVisibleInTree())
                continue;
            if (mi.Mesh is not ArrayMesh mesh)
                continue;
            var xf = mi.GlobalTransform;
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mesh.SurfaceGetMaterial(s) is { } material && sea.Contains(material))
                    continue;
                var arrays = scene.SurfaceArrays(mesh, s);
                var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                    ? Array.Empty<int>() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                int count = ix.Length > 0 ? ix.Length : v.Length;
                for (int i = 0; i < count; i++)
                    tris.Add(xf * v[ix.Length > 0 ? ix[i] : i]);
            }
        }
        return tris.ToArray();
    }

    // Water, and the painted shallows laid on it, which the ocean's own waves stand in for.
    private static bool IsSea(string texture) =>
        SceneBuilder.ClassifySurface(texture) == "water" || TextureArchive.IsPaintedShallows(texture);

    // One orientation's texture and the direction toward its sun.
    // Bakes one orientation into the cache and returns its raster.
    private TerrainShadowRaster.Result BakeOne(Vector3 orientation)
    {
        // The light shines down its -Z, so +Z points at the sun, as WeatherRig.SunDirectionFor reads it.
        var sun = Basis.FromEuler(orientation).Z.Normalized();
        var raster = TerrainShadowRaster.Run(_tris, sun, OceanMaskRaster.DefaultBands);
        var image = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgh, Halves(raster.Line, raster.Distance));
        _baked[orientation] = new Baked(TextureUpload.Create(image), sun);
        Width = raster.Width;
        Height = raster.Height;
        Origin = raster.Origin;
        Size = new Vector2(raster.Width * Cell, raster.Height * Cell);
        return raster;
    }

    private sealed record Baked(ImageTexture Texture, Vector3 Sun);
}
