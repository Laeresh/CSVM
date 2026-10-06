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

    /// <summary>The filtered sea coverage below which the ocean discards a fragment. The base sheet's
    /// hide (<c>csky_ocean.gdshaderinc</c>) steps aside only at or above it, so every texel has one
    /// owner. Both shaders spell it as a literal.</summary>
    public const float SeaThreshold = 0.02f;

    /// <summary>The zone byte of a texel on a seam between two zone groups, which no grid draws.</summary>
    public const byte SeamZone = OceanMaskRaster.AnyZone - 1;

    // The globals csky_ocean.gdshaderinc declares: the live mask, its zone groups and its world rect.
    private static readonly StringName MaskParam = "csky_ocean_mask";
    private static readonly StringName ZoneParam = "csky_ocean_zone";
    private static readonly StringName RectParam = "csky_ocean_rect";

    // One bake per built world, held only as long as its builder lives.
    private static readonly ConditionalWeakTable<SceneBuilder, Memo> Baked = new();

    // What the globals hold with no ocean live: no sea, and a zone every grid draws. The hide then
    // steps aside nowhere, whatever csky_ocean_on reads.
    private static ImageTexture? _noSea;
    private static ImageTexture? _anyZone;

    private OceanMask()
    {
    }

    /// <summary>The mask the base sheet's hide reads now, null while the defaults stand. Read by the
    /// suites, since the server's getter for a global errors outside the editor.</summary>
    public static OceanMask? Live { get; private set; }

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

    /// <summary>The lowest draw priority any sea-level base-sheet surface carries. The ocean draws a
    /// level below it, under every coplanar layer drawn on or beside any part of the sheet.</summary>
    public int BaseLevel { get; private set; }

    public int EdgeTriangles { get; private set; }

    public int SolidTriangles { get; private set; }

    /// <summary>The visual layer sets the base sheets are drawn on, one per zone group. The zone
    /// gate culls a whole group per camera, so the ocean over it draws on the same layers.</summary>
    public List<uint> ZoneLayers { get; } = new();

    /// <summary>Which zone group each texel shows (R8), or null when the sheet is one group.</summary>
    public ImageTexture? ZoneTexture { get; private set; }

    /// <summary>The wake sheets' meshes found in the world, whose ships the ocean calms around.</summary>
    public List<Node3D> Wakes { get; } = new();

    /// <summary>The movers the bake found to be hulls on the sea (<see cref="OceanMovers"/>). They stay
    /// out of the mask, so a hull that sails off leaves no calm patch.</summary>
    public List<Node3D> MovingHulls { get; } = new();

    /// <summary>The movers hidden at the bake, which the ocean judges once they show.</summary>
    public List<Node3D> UnjudgedMovers { get; } = new();

    /// <summary>The bake's triangles under moving hulls, kept out of the mask.</summary>
    public int MovingHullTriangles { get; private set; }

    /// <summary>The bake's milliseconds per phase, for the build log line.</summary>
    public string Timing { get; private set; } = "";

    /// <summary>Declares the mask globals with their defaults. Must run before the first shader that
    /// reads them is built, as <see cref="Ocean.RegisterGlobal"/> does.</summary>
    public static void RegisterGlobals()
    {
        _noSea ??= TextureUpload.Create(1, 1, Image.Format.Rg8, new byte[] { 0, 0 });
        _anyZone ??= TextureUpload.Create(1, 1, Image.Format.R8, new[] { OceanMaskRaster.AnyZone });
        RenderingServer.GlobalShaderParameterAdd(MaskParam, RenderingServer.GlobalShaderParameterType.Sampler2D, _noSea);
        RenderingServer.GlobalShaderParameterAdd(ZoneParam, RenderingServer.GlobalShaderParameterType.Sampler2D, _anyZone);
        RenderingServer.GlobalShaderParameterAdd(RectParam, RenderingServer.GlobalShaderParameterType.Vec4, new Vector4(0f, 0f, 1f, 1f));
    }

    /// <summary>Puts the defaults back, so the base sheet's hide steps aside nowhere.</summary>
    public static void Withdraw()
    {
        Live = null;
        if (_noSea == null || _anyZone == null)
            return;
        RenderingServer.GlobalShaderParameterSet(MaskParam, _noSea);
        RenderingServer.GlobalShaderParameterSet(ZoneParam, _anyZone);
        RenderingServer.GlobalShaderParameterSet(RectParam, new Vector4(0f, 0f, 1f, 1f));
    }


    /// <summary>Bakes the mask over the world under <paramref name="root"/>, or null when that world
    /// has no sea-level base sheet. It skips the subtrees in <paramref name="skip"/> and the
    /// <paramref name="movers"/> found to be hulls on the sea. The surfaces are read through
    /// <paramref name="scene"/>, which holds the arrays it committed.</summary>
    public static OceanMask? Bake(Node3D root, SceneBuilder scene, ISet<Node> skip, IReadOnlyCollection<Node3D> movers)
    {
        // The world is static. An ocean rebuilt over it, after a live switch or a Water Quality
        // change, reuses the first bake. The frame path never pays it twice.
        if (Baked.TryGetValue(scene, out var kept) && kept.Root == root && GodotObject.IsInstanceValid(root))
        {
            kept.Mask?.Wakes.RemoveAll(w => !GodotObject.IsInstanceValid(w));
            kept.Mask?.MovingHulls.RemoveAll(h => !GodotObject.IsInstanceValid(h));
            kept.Mask?.UnjudgedMovers.RemoveAll(h => !GodotObject.IsInstanceValid(h));
            if (kept.Mask != null)
                kept.Mask.Timing = "reused";
            return kept.Mask;
        }
        var mask = BakeNew(root, scene, skip, movers);
        // ⚠ Do not drop this collection. Reading MeshInstance3D.Mesh mints a managed wrapper for
        // every mesh whose wrapper was already collected, and each holds its mesh until collected.
        // A short run that quits first leaves hundreds alive, and Godot's exit check aborts.
        long gcStart = System.Diagnostics.Stopwatch.GetTimestamp();
        GC.Collect();
        if (mask != null)
            mask.Timing += string.Create(CultureInfo.InvariantCulture,
                $" gc={System.Diagnostics.Stopwatch.GetElapsedTime(gcStart).TotalMilliseconds:0}");
        Baked.AddOrUpdate(scene, new Memo(root, mask));
        return mask;
    }

    /// <summary>Hands this mask to the base sheet's hide, which then steps aside exactly where the
    /// ocean draws. A sheet shown after the bake lies off the sea coverage and keeps drawing.</summary>
    public void Publish()
    {
        if (_anyZone == null)
            return;
        Live = this;
        RenderingServer.GlobalShaderParameterSet(MaskParam, Texture);
        RenderingServer.GlobalShaderParameterSet(ZoneParam, ZoneTexture ?? _anyZone);
        RenderingServer.GlobalShaderParameterSet(RectParam, new Vector4(Origin.X, Origin.Y, 1f / Size.X, 1f / Size.Y));
    }

    private static OceanMask? BakeNew(Node3D root, SceneBuilder scene, ISet<Node> skip, IReadOnlyCollection<Node3D> movers)
    {
        long t0 = Stopwatch.GetTimestamp();
        var names = new Dictionary<Material, string>();
        foreach (var (mat, tex) in scene.TexturedMaterials)
            names[mat] = tex;
        var result = new OceanMask();
        var walker = new Walker(root, scene, names, skip, movers, result);
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
        if (result.ZoneLayers.Count > 1)
            result.ZoneTexture = TextureUpload.Create(raster.Width, raster.Height, Image.Format.R8, Seam(raster));
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

    // The zone groups with every texel on a seam between two groups taken from both. A seam texel's
    // centre can fall on either side, so either grid would overhang the other group's culled sea.
    // C5 lays an opaque fog gradient along each seam, which hides the gap this leaves.
    private static byte[] Seam(OceanMaskRaster.Result raster)
    {
        int w = raster.Width, h = raster.Height;
        var zones = raster.Zones;
        var mask = raster.Mask;
        var seamed = (byte[])zones.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                byte z = zones[i];
                if (mask[2 * i] == 0 || z >= OceanMaskRaster.AnyZone - 1)
                    continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int n = (ny * w) + nx;
                    if (mask[2 * n] != 0 && zones[n] < OceanMaskRaster.AnyZone - 1 && zones[n] != z)
                        seamed[i] = OceanMaskRaster.AnyZone - 1;
                }
            }
        }
        return seamed;
    }

    // The authored priority a world material was built at. Its depth bias adds under half a level
    // on top (rank, no_clutter), so the floor a quarter level up recovers it.
    private static int LevelOf(Material material) =>
        material is ShaderMaterial sm
            ? (int)Math.Floor((sm.GetShaderParameter("depth_bias").AsSingle() / SceneBuilder.DepthBiasPerLevel) + 0.25f)
            : 0;

    // A world's bake, or null when it had no sea-level base sheet.
    private sealed record Memo(Node3D Root, OceanMask? Mask);

    // One pass over the world tree finds the wake sheets and lists the surfaces to read, in tree
    // order. The triangles are read after it, once every hull subtree is known and dropped.
    private sealed class Walker
    {
        // A wake sheet lies on the water; the freighter's bow wave climbs its hull about 10 m.
        private const float WakeMaxHeight = 20f;

        private readonly Node3D _root;
        private readonly SceneBuilder _scene;
        private readonly Dictionary<Material, string> _names;
        private readonly ISet<Node> _skip;
        private readonly OceanMask _result;
        private readonly List<Surface> _surfaces = new();
        private readonly Dictionary<Node, int> _moverIndex = new();
        private readonly Mover[] _movers;
        private readonly List<int> _moverPath = new();
        private readonly List<int[]> _triMovers = new();
        private int[] _pathArray = Array.Empty<int>();

        public Walker(Node3D root, SceneBuilder scene, Dictionary<Material, string> names, ISet<Node> skip,
            IReadOnlyCollection<Node3D> movers, OceanMask result)
        {
            _root = root;
            _scene = scene;
            _names = names;
            _skip = skip;
            _result = result;
            _movers = new Mover[movers.Count];
            foreach (var m in movers)
            {
                if (m != root && GodotObject.IsInstanceValid(m) && !_moverIndex.ContainsKey(m))
                {
                    _movers[_moverIndex.Count] = new Mover { Node = m };
                    _moverIndex[m] = _moverIndex.Count;
                }
            }
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
            if (_moverIndex.Count > 0)
                DropMovingHulls();
        }

        private static bool IsWakeTexture(string tex) =>
            tex.Contains("wakefront", StringComparison.OrdinalIgnoreCase)
            || tex.Contains("wakeback", StringComparison.OrdinalIgnoreCase);

        // How many levels up the ship of a wake below sits: a sheet's mesh is 2, under its own node
        // (1), under the ship. Wakes are found everywhere, skipped subtrees included. The ship gives
        // up its surfaces, so a moving hull leaves no calm patch where it started. A wake node right
        // under the root gives up only its own. A subtree's surfaces are the tail it appended.
        private int Visit(Node node, Transform3D parent, bool collect)
        {
            // A hidden subtree draws nothing: C3's unplaced swtr sheets over its crater floor, and
            // every unplaced vehicle parked at the origin.
            collect = collect && !_skip.Contains(node) && node is not Node3D { Visible: false };
            int start = _surfaces.Count;
            var xf = parent;
            if (collect && node is Node3D n3 && node != _root)
                xf = parent * n3.Transform;
            bool mover = false;
            if (collect && _moverIndex.TryGetValue(node, out int moverAt))
            {
                mover = true;
                _movers[moverAt].Collected = true;
                _movers[moverAt].Origin = xf.Origin;
                _moverPath.Add(moverAt);
                _pathArray = _moverPath.ToArray();
            }
            bool wake = false;
            if (node is MeshInstance3D mi && mi.Mesh is { } mesh)
            {
                var arrayMesh = collect ? mesh as ArrayMesh : null;
                int count = mesh.GetSurfaceCount();
                // C1's rock zeppelin wears wakefront1 over 180 m of its underside; a sheet lies flat.
                bool flat = mesh.GetAabb().Size.Y < WakeMaxHeight;
                for (int s = 0; s < count; s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (mat == null || !_names.TryGetValue(mat, out var tex))
                        continue;
                    if (!wake && flat && IsWakeTexture(tex))
                    {
                        wake = true;
                        _result.Wakes.Add(mi);
                    }
                    if (arrayMesh != null)
                        _surfaces.Add(new Surface(arrayMesh, s, tex, xf, mat, mi.Layers, _pathArray));
                }
            }
            int below = 0;
            int children = node.GetChildCount();
            for (int i = 0; i < children; i++)
                below = Math.Max(below, Visit(node.GetChild(i), xf, collect));
            if (mover)
            {
                _moverPath.RemoveAt(_moverPath.Count - 1);
                _pathArray = _moverPath.ToArray();
            }
            if (below > 0 && node != _root)
                _surfaces.RemoveRange(start, _surfaces.Count - start);
            return wake ? 2 : Math.Max(below - 1, 0);
        }

        private void Read(Surface surface)
        {
            var (mesh, s, tex, xf, material, layers, movers) = surface;
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
                if (movers.Length > 0 && OceanMovers.ReachesWaterline(a, b, c))
                {
                    foreach (int m in movers)
                        _movers[m].Waterline = true;
                }
                Kind kind;
                if (isBase && seaLevel)
                {
                    kind = Kind.Base;
                    foreach (int m in movers)
                        _movers[m].HasBase = true;
                    int level = LevelOf(material);
                    if (_result.BaseTriangles++ == 0 || level < _result.BaseLevel)
                        _result.BaseLevel = level;
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
                else if (water && (seaLevel || (isBase && minY > -0.5f && minY < 0.5f)))
                {
                    // A base-sheet ramp rising off the sea hides only below 0.25 m, so the ocean
                    // must reach under its foot, calm.
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
                byte zone = kind == Kind.Base ? ZoneOf(layers) : (byte)0;
                Tris.Add(new Tri(a, b, c, kind, tinted ? Stored(col[i0]) : Colors.White, tinted ? Stored(col[i1]) : Colors.White, tinted ? Stored(col[i2]) : Colors.White, zone));
                _triMovers.Add(movers);
            }
        }

        // The group of base sheets sharing one visual layer set, numbered in first-seen order.
        private byte ZoneOf(uint layers)
        {
            int k = _result.ZoneLayers.IndexOf(layers);
            if (k < 0)
            {
                k = _result.ZoneLayers.Count;
                _result.ZoneLayers.Add(layers);
            }
            return (byte)Math.Min(k, OceanMaskRaster.AnyZone - 2);
        }

        // A mover is a hull when its origin sits on sea level over the world's own sea-level water
        // and its meshes reach the waterline. A mover carrying base-sheet water is sea, not a hull.
        // Its triangles and every nested mover's leave the mask together; triangle order is kept.
        private void DropMovingHulls()
        {
            var hull = new bool[_moverIndex.Count];
            for (int m = 0; m < hull.Length; m++)
            {
                ref var mover = ref _movers[m];
                if (!mover.Collected)
                {
                    _result.UnjudgedMovers.Add(mover.Node);
                    continue;
                }
                if (!mover.Waterline || mover.HasBase || Math.Abs(mover.Origin.Y) > OceanMovers.OriginBand)
                {
                    Log.Debug("world", $"ocean: mover '{mover.Node.Name}' at ({mover.Origin.X:0},{mover.Origin.Y:0.0},{mover.Origin.Z:0}) not a hull: waterline={mover.Waterline} base={mover.HasBase}");
                    continue;
                }
                var p = new Vector2(mover.Origin.X, mover.Origin.Z);
                for (int i = 0; i < Tris.Count; i++)
                {
                    var t = Tris[i];
                    if (t.Kind != Kind.Solid && _triMovers[i].Length == 0 && OceanMovers.Covers(t.A, t.B, t.C, p))
                    {
                        hull[m] = true;
                        _result.MovingHulls.Add(mover.Node);
                        break;
                    }
                }
                if (!hull[m])
                    Log.Debug("world", $"ocean: mover '{mover.Node.Name}' at ({mover.Origin.X:0},{mover.Origin.Y:0.0},{mover.Origin.Z:0}) not a hull: off the sea");
            }
            if (_result.MovingHulls.Count == 0)
                return;
            int kept = 0;
            for (int i = 0; i < Tris.Count; i++)
            {
                bool drop = false;
                foreach (int m in _triMovers[i])
                    drop |= hull[m];
                if (!drop)
                {
                    Tris[kept++] = Tris[i];
                    continue;
                }
                _result.MovingHullTriangles++;
                if (Tris[i].Kind == Kind.Solid)
                    _result.SolidTriangles--;
                else if (Tris[i].Kind == Kind.Edge)
                    _result.EdgeTriangles--;
            }
            Tris.RemoveRange(kept, Tris.Count - kept);
        }

        private readonly record struct Surface(ArrayMesh Mesh, int Index, string Texture, Transform3D Xf, Material Material, uint Layers, int[] Movers);

        // One mover as the walk found it: where it stood, and what its visible meshes reach.
        private struct Mover
        {
            public Node3D Node;
            public Vector3 Origin;
            public bool Collected;
            public bool Waterline;
            public bool HasBase;
        }
    }
}
