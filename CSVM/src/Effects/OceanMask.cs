using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;
using Kind = CSVM.Effects.OceanMaskRaster.Kind;
using Tri = CSVM.Effects.OceanMaskRaster.Tri;

namespace CSVM.Effects;

/// <summary>
/// The wave ocean's shore mask, baked once from a built world's mesh instances. Its red channel is
/// sea coverage. Its green channel is the wave height left after a fade from every shore, surf
/// texel and solid object near sea level. The tint texture carries the base sheet's baked vertex
/// colour, which darkens the water around the islands. The bake also finds the wake sheets and the
/// base texture's tile size. <see cref="OceanMaskRaster"/> turns the triangles into texels.
/// </summary>
internal sealed class OceanMask
{
    /// <summary>The texel edge in metres.</summary>
    public const float Cell = OceanMaskRaster.Cell;

    // One bake per built world, held only as long as its builder lives.
    private static readonly ConditionalWeakTable<SceneBuilder, Memo> Baked = new();

    private OceanMask()
    {
    }

    public ImageTexture Texture { get; private set; } = null!;

    public Image Image { get; private set; } = null!;

    public ImageTexture TintTexture { get; private set; } = null!;

    /// <summary>The tint texture's pixels, kept for <c>--dump-ocean-mask</c>.</summary>
    public Image TintImage { get; private set; } = null!;

    /// <summary>The world X/Z of the mask's first texel corner.</summary>
    public Vector2 Origin { get; private set; }

    /// <summary>The mask's extent in metres along X and Z.</summary>
    public Vector2 Size { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The texture most of the base sheet's triangles carry.</summary>
    public string BaseTexture { get; private set; } = "";

    /// <summary>The metres one repeat of the base texture spans, the median over the base sheet.</summary>
    public float TileMetres { get; private set; } = 32f;

    public int BaseTriangles { get; private set; }

    public int EdgeTriangles { get; private set; }

    public int SolidTriangles { get; private set; }

    /// <summary>The wake sheets found in the world, whose positions the ocean calms around.</summary>
    public List<Node3D> Wakes { get; } = new();

    /// <summary>The bake's milliseconds per phase, for the build log line.</summary>
    public string Timing { get; private set; } = "";

    /// <summary>Bakes the mask over the world under <paramref name="root"/>, skipping the subtrees in
    /// <paramref name="skip"/>; null when that world has no sea-level base sheet. The surfaces are
    /// read through <paramref name="scene"/>, which holds the arrays it committed.</summary>
    public static OceanMask? Bake(Node3D root, SceneBuilder scene, ISet<Node> skip)
    {
        // The world is static. An ocean rebuilt over it, after a live switch or a Water Quality
        // change, reuses the first bake. The frame path never pays it twice.
        if (Baked.TryGetValue(scene, out var kept) && kept.Root == root && GodotObject.IsInstanceValid(root))
        {
            kept.Mask?.Wakes.RemoveAll(w => !GodotObject.IsInstanceValid(w));
            if (kept.Mask != null)
                kept.Mask.Timing = "reused";
            return kept.Mask;
        }
        var mask = BakeNew(root, scene, skip);
        Baked.AddOrUpdate(scene, new Memo(root, mask));
        return mask;
    }

    private static OceanMask? BakeNew(Node3D root, SceneBuilder scene, ISet<Node> skip)
    {
        long t0 = Stopwatch.GetTimestamp();
        var names = new Dictionary<Material, string>();
        foreach (var (mat, tex) in scene.TexturedMaterials)
            names[mat] = tex;
        var result = new OceanMask();
        var walker = new Walker(root, scene, names, skip, result);
        walker.Walk();
        double walkMs = Lap(ref t0);
        if (result.BaseTriangles == 0)
            return null;
        int best = 0;
        foreach (var (name, count) in walker.BaseCounts)
        {
            if (count > best)
            {
                best = count;
                result.BaseTexture = name;
            }
        }
        var uvSamples = walker.UvSamples;
        if (uvSamples.Count > 0)
        {
            uvSamples.Sort();
            result.TileMetres = uvSamples[uvSamples.Count / 2];
        }
        var vertexColor = Colors.White;
        if (walker.ColorCount > 0)
        {
            int cn = walker.ColorCount;
            vertexColor = new Color((float)(walker.R / cn), (float)(walker.G / cn), (float)(walker.B / cn)).SrgbToLinear();
        }
        var raster = OceanMaskRaster.Run(walker.Tris.ToArray(), vertexColor.LinearToSrgb(), OceanMaskRaster.DefaultBands);
        Lap(ref t0);
        result.Width = raster.Width;
        result.Height = raster.Height;
        result.Origin = raster.Origin;
        result.Size = new Vector2(raster.Width * Cell, raster.Height * Cell);
        result.Image = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rg8, raster.Mask);
        result.TintImage = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgb8, raster.Tint);
        // Both kept: --dump-ocean-mask reads them after the upload.
        result.TintTexture = TextureUpload.Create(result.TintImage, callerKeeps: true);
        result.Texture = TextureUpload.Create(result.Image, callerKeeps: true);
        double uploadMs = Lap(ref t0);
        result.Timing = string.Create(CultureInfo.InvariantCulture,
            $"walk={walkMs:0.0} raster={raster.FillMs:0.0} distance={raster.EncodeMs:0.0} upload={uploadMs:0.0} bands={raster.Bands}");
        return result;
    }

    // Milliseconds since t0, which moves to now.
    private static double Lap(ref long t0)
    {
        long now = Stopwatch.GetTimestamp();
        double ms = (now - t0) * 1000.0 / Stopwatch.Frequency;
        t0 = now;
        return ms;
    }

    // Godot keeps a vertex colour as RGBA8, truncated, and a read-back returns those bytes over 255.
    // The builder's kept arrays hold the committed floats, so they take the same step here. The tint
    // then matches either source, and a stored value passes through unchanged.
    private static Color Stored(Color c) => new(
        (byte)Math.Clamp(c.R * 255f, 0f, 255f) / 255f,
        (byte)Math.Clamp(c.G * 255f, 0f, 255f) / 255f,
        (byte)Math.Clamp(c.B * 255f, 0f, 255f) / 255f,
        (byte)Math.Clamp(c.A * 255f, 0f, 255f) / 255f);

    // A world's bake, or null when it had no sea-level base sheet.
    private sealed record Memo(Node3D Root, OceanMask? Mask);

    // One pass over the world tree finds the wake sheets and lists the surfaces to read, in tree
    // order. The triangles are read after it, once every hull subtree is known and dropped.
    private sealed class Walker
    {
        private readonly Node3D _root;
        private readonly SceneBuilder _scene;
        private readonly Dictionary<Material, string> _names;
        private readonly ISet<Node> _skip;
        private readonly OceanMask _result;
        private readonly List<Surface> _surfaces = new();

        public Walker(Node3D root, SceneBuilder scene, Dictionary<Material, string> names, ISet<Node> skip, OceanMask result)
        {
            _root = root;
            _scene = scene;
            _names = names;
            _skip = skip;
            _result = result;
        }

        public List<Tri> Tris { get; } = new();

        public List<float> UvSamples { get; } = new();

        public Dictionary<string, int> BaseCounts { get; } = new();

        public double R { get; private set; }

        public double G { get; private set; }

        public double B { get; private set; }

        public int ColorCount { get; private set; }

        public void Walk()
        {
            Visit(_root, Transform3D.Identity, true);
            foreach (var s in _surfaces)
                Read(s);
        }

        // Whether the node is a wake sheet. Wakes are found everywhere, skipped subtrees included.
        // A ship's hull, the wake's parent, gives up its surfaces, so an animated ship leaves no calm
        // patch where it started. A subtree's surfaces are the tail it appended.
        private bool Visit(Node node, Transform3D parent, bool collect)
        {
            collect = collect && !_skip.Contains(node);
            int start = _surfaces.Count;
            var xf = parent;
            if (collect && node is Node3D n3 && node != _root)
                xf = parent * n3.Transform;
            bool wake = false;
            if (node is MeshInstance3D mi && mi.Mesh is { } mesh)
            {
                var arrayMesh = collect ? mesh as ArrayMesh : null;
                int count = mesh.GetSurfaceCount();
                for (int s = 0; s < count; s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (mat == null || !_names.TryGetValue(mat, out var tex))
                        continue;
                    if (!wake && tex.Contains("wakefront", StringComparison.OrdinalIgnoreCase))
                    {
                        wake = true;
                        _result.Wakes.Add(mi);
                    }
                    if (arrayMesh != null)
                        _surfaces.Add(new Surface(arrayMesh, s, tex, xf));
                }
            }
            bool hull = false;
            int children = node.GetChildCount();
            for (int i = 0; i < children; i++)
                hull |= Visit(node.GetChild(i), xf, collect);
            if (hull && node != _root)
                _surfaces.RemoveRange(start, _surfaces.Count - start);
            return wake;
        }

        private void Read(Surface surface)
        {
            var (mesh, s, tex, xf) = surface;
            bool water = SceneBuilder.ClassifySurface(tex) == "water";
            bool isBase = water && SceneBuilder.IsOceanBaseTexture(tex);
            // The builder's own copy: Godot's read-back copies the surface off the GPU and waits
            // for the render thread, about a millisecond a surface. UVs and colours serve the base.
            var arrays = _scene.SurfaceArrays(mesh, s);
            var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                ? Array.Empty<int>() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var uv = !isBase || arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil
                ? Array.Empty<Vector2>() : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var col = !isBase || arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil
                ? Array.Empty<Color>() : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            int triCount = ix.Length > 0 ? ix.Length / 3 : v.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = ix.Length > 0 ? ix[3 * t] : 3 * t;
                int i1 = ix.Length > 0 ? ix[(3 * t) + 1] : (3 * t) + 1;
                int i2 = ix.Length > 0 ? ix[(3 * t) + 2] : (3 * t) + 2;
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
                    _result.BaseTriangles++;
                    BaseCounts[tex] = BaseCounts.GetValueOrDefault(tex) + 1;
                    if (uv.Length > 0)
                    {
                        float dp = new Vector2(b.X - a.X, b.Z - a.Z).Length();
                        float du = (uv[i1] - uv[i0]).Length();
                        if (du > 1e-4f)
                            UvSamples.Add(dp / du);
                    }
                    if (col.Length > 0)
                    {
                        var c0 = Stored(col[i0]);
                        R += c0.R;
                        G += c0.G;
                        B += c0.B;
                        ColorCount++;
                    }
                }
                else if (water && seaLevel)
                {
                    kind = Kind.Edge;
                    _result.EdgeTriangles++;
                }
                else if (!water && minY < 20f && maxY - minY < 400f)
                {
                    kind = Kind.Solid;
                    _result.SolidTriangles++;
                }
                else
                {
                    continue;
                }
                bool tinted = kind == Kind.Base && col.Length > 0;
                Tris.Add(new Tri(a, b, c, kind, tinted ? Stored(col[i0]) : Colors.White, tinted ? Stored(col[i1]) : Colors.White, tinted ? Stored(col[i2]) : Colors.White));
            }
        }

        private readonly record struct Surface(ArrayMesh Mesh, int Index, string Texture, Transform3D Xf);
    }
}
