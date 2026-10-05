using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The wave ocean's shore mask, baked once from a built world's mesh instances. Its red channel is
/// sea coverage. Its green channel is the wave height left after a fade from every shore, surf
/// texel and solid object near sea level. The tint texture carries the base sheet's baked vertex
/// colour, which darkens the water around the islands. The bake also finds the wake sheets and the
/// base texture's tile size.
/// </summary>
internal sealed class OceanMask
{
    /// <summary>The texel edge in metres. Fine enough that the calm band hugs the surf ring.</summary>
    public const float Cell = 8f;

    // Waves are fully calm this close to a shore or a surf texel, and reach full height here.
    private const float ShoreCalm = 24f;
    private const float ShoreFull = 160f;

    private OceanMask()
    {
    }

    private enum Kind
    {
        Base,
        Edge,
        Solid,
    }

    public ImageTexture Texture { get; private set; } = null!;

    public Image Image { get; private set; } = null!;

    public ImageTexture TintTexture { get; private set; } = null!;

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

    private Color VertexColor { get; set; } = Colors.White;

    /// <summary>Bakes the mask over the world under <paramref name="root"/>, skipping the subtrees in
    /// <paramref name="skip"/>; null when that world has no sea-level base sheet.</summary>
    public static OceanMask? Bake(Node3D root, Dictionary<Material, string> names, ISet<Node> skip)
    {
        var result = new OceanMask();
        // An animated ship carries its wake sheet. Its hull must not leave a calm patch where it
        // started, so the wake's parent subtree is left out of the bake.
        FindWakes(root, names, result.Wakes);
        var skipped = new HashSet<Node>(skip);
        foreach (var wake in result.Wakes)
        {
            if (wake.GetParent() is { } hull && hull != root)
                skipped.Add(hull);
        }
        var walker = new Walker(root, names, skipped, result);
        walker.Walk(root, Transform3D.Identity);
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
        if (walker.ColorCount > 0)
        {
            int cn = walker.ColorCount;
            result.VertexColor = new Color((float)(walker.R / cn), (float)(walker.G / cn), (float)(walker.B / cn)).SrgbToLinear();
        }
        result.Rasterise(walker.Tris);
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

    private static void Raster(Tri t, float minX, float minZ, int w, int h, byte[] sea, bool[] solid, Color[] tint)
    {
        var a = new Vector2(t.A.X, t.A.Z);
        var b = new Vector2(t.B.X, t.B.Z);
        var c = new Vector2(t.C.X, t.C.Z);
        float area = Cross(b - a, c - a);
        int x0 = Math.Max(0, (int)Math.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - minX) / Cell));
        int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(a.X, Math.Max(b.X, c.X)) - minX) / Cell));
        int z0 = Math.Max(0, (int)Math.Floor((Math.Min(a.Y, Math.Min(b.Y, c.Y)) - minZ) / Cell));
        int z1 = Math.Min(h - 1, (int)Math.Ceiling((Math.Max(a.Y, Math.Max(b.Y, c.Y)) - minZ) / Cell));
        // A triangle smaller than a texel still marks the texels its corners land in.
        if (t.Kind == Kind.Solid)
        {
            foreach (var p in new[] { a, b, c })
                Mark((int)((p.X - minX) / Cell), (int)((p.Y - minZ) / Cell));
        }
        if (Math.Abs(area) < 1e-6f)
            return;
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(minX + (x + 0.5f) * Cell, minZ + (z + 0.5f) * Cell);
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

    // Distance in metres to the nearest texel that is not open base sea, by a two-pass chamfer.
    private static float[] ShoreDistance(byte[] sea, bool[] solid, int w, int h)
    {
        var dist = new float[w * h];
        const float big = 1e6f;
        for (int i = 0; i < dist.Length; i++)
            dist[i] = sea[i] == 1 && !solid[i] ? big : 0f;
        float d1 = Cell, d2 = Cell * 1.4142f;
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
        return dist;
    }

    private void Rasterise(List<Tri> tris)
    {
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
        int w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / Cell));
        int h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / Cell));
        Width = w;
        Height = h;
        Origin = new Vector2(minX, minZ);
        Size = new Vector2(w * Cell, h * Cell);
        var sea = new byte[w * h];   // 1 base, 2 edge
        var solid = new bool[w * h];
        var tint = new Color[w * h];
        Array.Fill(tint, new Color(-1f, 0f, 0f));
        foreach (var t in tris)
            Raster(t, minX, minZ, w, h, sea, solid, tint);

        var dist = ShoreDistance(sea, solid, w, h);
        var bytes = new byte[w * h * 2];
        for (int i = 0; i < w * h; i++)
        {
            bytes[2 * i] = sea[i] != 0 ? (byte)255 : (byte)0;
            float a = Mathf.SmoothStep(ShoreCalm, ShoreFull, dist[i]);
            bytes[2 * i + 1] = (byte)Math.Round(a * 255f);
        }
        Image = Image.CreateFromData(w, h, false, Image.Format.Rg8, bytes);
        // The base sheet's baked vertex colour per texel, sRGB as authored; open texels take the mean.
        var tintBytes = new byte[w * h * 3];
        var mean = VertexColor.LinearToSrgb();
        for (int i = 0; i < w * h; i++)
        {
            var c = tint[i].R < 0f ? mean : tint[i];
            tintBytes[3 * i] = (byte)Math.Clamp(Math.Round(c.R * 255f), 0, 255);
            tintBytes[3 * i + 1] = (byte)Math.Clamp(Math.Round(c.G * 255f), 0, 255);
            tintBytes[3 * i + 2] = (byte)Math.Clamp(Math.Round(c.B * 255f), 0, 255);
        }
        TintTexture = TextureUpload.Create(w, h, Image.Format.Rgb8, tintBytes);
        // Kept for --dump-ocean-mask, which only reads it.
        Texture = TextureUpload.Create(Image, callerKeeps: true);
    }

    private readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, Kind Kind, Color CA, Color CB, Color CC);

    // One pass over the world tree collects every sea-level water triangle and every solid one near
    // sea level, in world space. It also counts the base sheet's textures, tile sizes and colour.
    private sealed class Walker
    {
        private readonly Node3D _root;
        private readonly Dictionary<Material, string> _names;
        private readonly ISet<Node> _skip;
        private readonly OceanMask _result;

        public Walker(Node3D root, Dictionary<Material, string> names, ISet<Node> skip, OceanMask result)
        {
            _root = root;
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

        public void Walk(Node node, Transform3D parent)
        {
            if (_skip.Contains(node))
                return;
            var xf = parent;
            if (node is Node3D n3 && node != _root)
                xf = parent * n3.Transform;
            if (node is MeshInstance3D mi && mi.Mesh is ArrayMesh mesh)
            {
                for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (mat != null && _names.TryGetValue(mat, out var tex))
                        WalkSurface(mesh, s, tex, xf);
                }
            }
            foreach (var child in node.GetChildren())
                Walk(child, xf);
        }

        private void WalkSurface(ArrayMesh mesh, int s, string tex, Transform3D xf)
        {
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
                        R += col[i0].R;
                        G += col[i0].G;
                        B += col[i0].B;
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
                Tris.Add(new Tri(a, b, c, kind, tinted ? col[i0] : Colors.White, tinted ? col[i1] : Colors.White, tinted ? col[i2] : Colors.White));
            }
        }
    }
}
