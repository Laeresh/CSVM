using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Ground cover from new-art cards under Rich, a prototype behind <c>--ground-cover-cards</c>. Grass tufts grow on
/// a chapter's picked meadow textures and crop rows on its field textures, near the camera only. A card stands on a
/// lattice in the ground's own texture space, so a crop row runs along its field's painted rows (<see cref="RowsAlongV"/>).
/// Its colour is the ground's under it (the texture blurred over <see cref="BlurTexels"/> times the baked corner
/// colour), lit by the ground's normal. The art is a brightness map with no hue, drawn here
/// (<see cref="GrassArt"/>, <see cref="CropArt"/>); the brush mask gates it (<see cref="Under"/>). The cells around
/// the cameras are built by <see cref="Field"/>, with no collider and no shadow cast or received.
/// </summary>
public static class GroundCover
{
    /// <summary>The edge of the square cells the cover is placed and drawn in.</summary>
    public const float CellM = 64f;

    /// <summary>Grass: one tuft a lattice point this far apart, jittered, and the tuft's size at scale one.</summary>
    public const float GrassSpacingM = 1.1f, GrassWidthM = 0.9f, GrassHeightM = 0.5f;

    /// <summary>Crops: rows this far apart on average, a card every <see cref="CropStepM"/> along a row on average, and
    /// the crop's height. A card is 1 to 1.5 steps long, so neighbours overlap or leave a gap.</summary>
    public const float RowSpacingM = 0.75f, CropStepM = 1.3f, CropHeightM = 1.0f;

    /// <summary>The row map's patches along each side of a texture.</summary>
    public const int Patches = 16;

    /// <summary>The texels a side the ground colour is averaged over, so a card takes its patch's colour, not one texel's.</summary>
    public const int BlurTexels = 2;

    /// <summary>How far across from a card's foot, and how far above the ground, another polygon keeps it off
    /// (<see cref="Overlaid"/>).</summary>
    public const float OverlayReachM = 0.5f, OverlayRiseM = 1.5f;

    /// <summary>The cells one frame builds once some stand, nearest first.</summary>
    public const int MaxCellsPerFrame = 2;

    /// <summary>The side of the grass card's square texture and the crop card's height; the crop card is twice as wide.</summary>
    public const int ArtSide = 128;

    // Steeper than this (normal Y) is a bank or a wall, which grows no card.
    private const float MinUp = 0.5f;


    // A triangle whose lattice over one cell passes this is a stretched UV map, skipped rather than walked.
    private const long MaxPoints = 1_000_000;

    // The card's foot and four points half its width around it, each tested against an overlay.
    private static readonly (float X, float Z)[] Probes =
        { (0f, 0f), (OverlayReachM, 0f), (-OverlayReachM, 0f), (0f, OverlayReachM), (0f, -OverlayReachM) };

    private static Shader? _shader;

    /// <summary>What a card grows.</summary>
    public enum Cover
    {
        /// <summary>A row of a field crop.</summary>
        Crop,

        /// <summary>A meadow grass tuft.</summary>
        Grass,
    }

    /// <summary>What a chapter's ground texture grows, null for nothing. C1 alone: its crop fields and its meadows.</summary>
    public static Cover? PickOf(string chapter, string texture) =>
        !string.Equals(chapter, "C1", StringComparison.OrdinalIgnoreCase) ? null
        : Path.GetFileNameWithoutExtension(texture).ToLowerInvariant() switch
        {
            "crop04" or "crop05" => Cover.Crop,
            "terpat03" or "terpat03b" or "grass1" => Cover.Grass,
            _ => null,
        };

    /// <summary>Each cover's band, the one place to change it: inside x every card draws, past y none, dithered
    /// between. Camera distance in three dimensions, so a camera y over the ground draws none.</summary>
    public static Vector2 Band(Cover cover) => cover == Cover.Grass ? new Vector2(160f, 240f) : new Vector2(80f, 120f);

    /// <summary>What a picked texel grows under its brush-mask byte. A bare one (the strip clearance) grows nothing,
    /// and a crop texel over neutral (a hedge line) grows grass. Any other grows the pick.</summary>
    public static Cover? Under(Cover pick, byte mask) =>
        mask == 0 ? null : pick == Cover.Crop && mask > VegetationMask.NeutralByte ? Cover.Grass : pick;

    /// <summary>Per square patch of an RGBA8 texture, <see cref="Patches"/> a side, row 0 at the top: whether its
    /// painted rows run along V (down the image). A patch whose brightness changes more across X than down Y is
    /// striped down Y; a tie reads as along V. Neighbours wrap, as the texture repeats.</summary>
    public static bool[] RowsAlongV(byte[] rgba, int width, int height)
    {
        var across = new double[Patches * Patches];
        var down = new double[Patches * Patches];
        float Luma(int x, int y)
        {
            int at = ((y * width) + x) * 4;
            return (0.299f * rgba[at]) + (0.587f * rgba[at + 1]) + (0.114f * rgba[at + 2]);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float l = Luma(x, y), dx = Luma((x + 1) % width, y) - l, dy = Luma(x, (y + 1) % height) - l;
                int p = (y * Patches / height * Patches) + (x * Patches / width);
                across[p] += dx * dx;
                down[p] += dy * dy;
            }
        }

        var along = new bool[Patches * Patches];
        for (int p = 0; p < along.Length; p++)
            along[p] = across[p] >= down[p];
        return along;
    }

    /// <summary>The grass tuft card, RGBA8, <see cref="ArtSide"/> square: blades fanning from the foot, dark low and
    /// light high. A texel's grey is half its gain on the ground's colour, and the blades average a gain of one.</summary>
    public static byte[] GrassArt()
    {
        const int w = ArtSide, h = ArtSide;
        var gain = new float[w * h];
        var rng = new Random(0x67A55);
        float Next() => (float)rng.NextDouble();
        for (int b = 0; b < 52; b++)
        {
            float x0 = w * (0.5f + (0.3f * (Next() + Next() - 1f)));
            float tall = h * (0.4f + (0.58f * Next()));
            float tipX = x0 + ((x0 - (w * 0.5f)) * 0.8f) + (w * 0.2f * (Next() - 0.5f));
            Blade(gain, w, h, false, x0, h - 1f, tipX, h - 1f - tall, w * 0.1f * (Next() - 0.5f), 3.5f + (2.5f * Next()), 0.85f + (0.3f * Next()));
        }

        return Encode(gain, w, h);
    }

    /// <summary>The crop row card, RGBA8, twice <see cref="ArtSide"/> wide and <see cref="ArtSide"/> tall. Upright
    /// stalks carry leaves arching off them, dark low and light high. It wraps across X, so the cards along a row
    /// join. The grey is a gain on the ground's colour as on <see cref="GrassArt"/>.</summary>
    public static byte[] CropArt()
    {
        const int w = ArtSide * 2, h = ArtSide;
        var gain = new float[w * h];
        var rng = new Random(0xC209);
        float Next() => (float)rng.NextDouble();
        for (int s = 0; s < w / 6; s++)
        {
            float x0 = (s * 6f) + (3f * Next()), tall = h * (0.78f + (0.2f * Next())), shade = 0.85f + (0.3f * Next());
            Blade(gain, w, h, true, x0, h - 1f, x0 + (4f * (Next() - 0.5f)), h - 1f - tall, 2f * (Next() - 0.5f), 2.4f, shade);
            for (int l = 0; l < 3; l++)
            {
                float y0 = h - 1f - (tall * (0.25f + (0.55f * Next()))), side = Next() < 0.5f ? -1f : 1f, reach = 9f + (14f * Next());
                Blade(gain, w, h, true, x0, y0, x0 + (side * reach), y0 - (reach * (0.2f + (0.5f * Next()))), side * -5f * Next(), 3f, shade);
            }
        }

        return Encode(gain, w, h);
    }

    /// <summary>Adds the cards <paramref name="g"/> grows inside the XZ square [x0, x0 + size) by [z0, z0 + size) to
    /// <paramref name="into"/>, by <see cref="Cover"/>. Each takes the brush-mask byte <paramref name="mask"/>
    /// answers at its point, and none stands where <paramref name="covered"/> holds. With <paramref name="only"/> the
    /// other cover is skipped. Returns the lattice points on the triangle that the mask grew nothing on.</summary>
    public static int Place(Ground g, float x0, float z0, float size, Func<float, float, byte> mask, List<Card>[] into,
        Func<Vector3, bool>? covered = null, Cover? only = null)
    {
        double uMin = double.MaxValue, uMax = double.MinValue, vMin = double.MaxValue, vMax = double.MinValue;
        foreach (var (x, z) in new[] { (x0, z0), (x0 + size, z0), (x0, z0 + size), (x0 + size, z0 + size) })
        {
            var (u, v) = g.UvAt(x, z);
            (uMin, uMax, vMin, vMax) = (Math.Min(uMin, u), Math.Max(uMax, u), Math.Min(vMin, v), Math.Max(vMax, v));
        }
        uMin = Math.Max(uMin, g.UvMin.X);
        uMax = Math.Min(uMax, g.UvMax.X);
        vMin = Math.Max(vMin, g.UvMin.Y);
        vMax = Math.Min(vMax, g.UvMax.Y);
        if (uMin > uMax || vMin > vMax)
            return 0;
        double metresU = g.AxisU.Length(), metresV = g.AxisV.Length();
        if (g.Pick == Cover.Grass)
            return Lattice(GrassSpacingM / metresU, GrassSpacingM / metresV, 0, null);
        // Two lattices, one per row direction; a point stands only on the one its patch's rows follow.
        return Lattice(RowSpacingM / metresU, CropStepM / metresV, 1, true)
            + Lattice(CropStepM / metresU, RowSpacingM / metresV, 2, false);

        int Lattice(double du, double dv, int pass, bool? alongV)
        {
            // One spare index each side, since a jittered row or card may cross into the box from outside it.
            long i0 = (long)Math.Floor(uMin / du) - 1, i1 = (long)Math.Floor(uMax / du) + 1;
            long j0 = (long)Math.Floor(vMin / dv) - 1, j1 = (long)Math.Floor(vMax / dv) + 1;
            if ((i1 - i0 + 1) * (j1 - j0 + 1) > MaxPoints)
                return 0;
            int bare = 0;
            for (long i = i0; i <= i1; i++)
            {
                for (long j = j0; j <= j1; j++)
                {
                    uint h = Hash((int)i, (int)j, pass);
                    var (ju, jv) = alongV switch
                    {
                        null => ((Unit(h) - 0.5) * 0.8, (Unit(h >> 8) - 0.5) * 0.8),
                        true => Loose((int)i, pass, h),
                        false => Swap(Loose((int)j, pass, h)),
                    };
                    double u = (i + 0.5 + ju) * du, v = (j + 0.5 + jv) * dv;
                    if (g.At(u, v) is not { } at || at.X < x0 || at.X >= x0 + size || at.Z < z0 || at.Z >= z0 + size)
                        continue;
                    if (alongV != null && (g.Surface.AlongV(u, v) != alongV.Value || Unit(h >> 12) > PatchKeep(u, v)))
                        continue;
                    var under = Under(g.Pick, mask(at.X, at.Z));
                    if (under == null && (only == null || only == g.Pick))
                        bare++;
                    if (under is not { } cover || (only != null && cover != only) || covered?.Invoke(at) == true)
                        continue;
                    into[(int)cover].Add(Make(g, cover, at, u, v, alongV ?? true, Hash((int)i, (int)j, pass + 7)));
                }
            }
            return bare;
        }

        static (double, double) Swap((double A, double B) p) => (p.B, p.A);
    }

    /// <summary>A crop card's offset in lattice steps, across its row and along it, in row <paramref name="row"/> of
    /// lattice <paramref name="pass"/>, <paramref name="h"/> the card's hash. Each row shifts across by up to a fifth
    /// of its spacing and takes its own phase along, so card ends never line up. Each card wobbles across and slides
    /// along a third of a step.</summary>
    public static (double Across, double Along) Loose(int row, int pass, uint h)
    {
        uint r = Hash(row, pass, 0x5EED);
        return (((Unit(r) - 0.5) * 0.4) + ((Unit(h) - 0.5) * 0.16), Unit(r >> 8) + ((Unit(h >> 4) - 0.5) * 0.66));
    }

    /// <summary>The share of crop cards a texture patch keeps, 0.6 to 1 by a hash of the patch. The field is thicker
    /// in places and thinner in others rather than one even comb.</summary>
    public static double PatchKeep(double u, double v) =>
        0.6 + (0.4 * Unit(Hash((int)Math.Floor(u * Patches), (int)Math.Floor(v * Patches), 0x7A7C)));

    /// <summary>Whether any of <paramref name="triangles"/> (corner triples) lies over the ground at <paramref name="at"/>.
    /// It must reach within <see cref="OverlayReachM"/> of it across and lie under <see cref="OverlayRiseM"/> above it.
    /// That is a rail, a road or an apron drawn as its own polygon over a picked texture, which no card pokes through.</summary>
    public static bool Overlaid(IReadOnlyList<Vector3> triangles, Vector3 at)
    {
        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            Vector3 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            float d = ((b.Z - c.Z) * (a.X - c.X)) + ((c.X - b.X) * (a.Z - c.Z));
            if (Math.Abs(d) < 1e-6f || at.X + OverlayReachM < MathF.Min(a.X, MathF.Min(b.X, c.X)) || at.X - OverlayReachM > MathF.Max(a.X, MathF.Max(b.X, c.X))
                || at.Z + OverlayReachM < MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) || at.Z - OverlayReachM > MathF.Max(a.Z, MathF.Max(b.Z, c.Z)))
                continue;
            foreach (var (dx, dz) in Probes)
            {
                float px = at.X + dx, pz = at.Z + dz;
                float w0 = (((b.Z - c.Z) * (px - c.X)) + ((c.X - b.X) * (pz - c.Z))) / d;
                float w1 = (((c.Z - a.Z) * (px - c.X)) + ((a.X - c.X) * (pz - c.Z))) / d;
                float w2 = 1f - w0 - w1;
                float y = (w0 * a.Y) + (w1 * b.Y) + (w2 * c.Y);
                if (w0 >= 0f && w1 >= 0f && w2 >= 0f && y > at.Y - 0.25f && y < at.Y + OverlayRiseM)
                    return true;
            }
        }
        return false;
    }

    /// <summary>A pseudo-random integer of three integers, the same on every machine and run.</summary>
    public static uint Hash(int a, int b, int c)
    {
        uint h = unchecked(((uint)a * 0x9E3779B1u) ^ ((uint)b * 0x85EBCA77u) ^ ((uint)c * 0xC2B2AE3Du));
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        return h ^ (h >> 16);
    }

    /// <summary>The cover over a chapter's picked ground, null where it picks none. The cells build as cameras come
    /// near (<see cref="Field.Update"/>). <paramref name="mask"/> null grows everywhere picked.</summary>
    public static Field? Build(GameZ gamez, TextureArchive textures, VegetationMask? mask, string chapter)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var surfaces = new Dictionary<string, Surface?>(StringComparer.OrdinalIgnoreCase);
        var index = new Dictionary<(int, int), List<Ground>>();
        int kept = 0;
        foreach (var t in VegetationGround.Covered(gamez, tex => PickOf(chapter, tex) != null))
        {
            if (!surfaces.TryGetValue(t.Texture, out var surface))
            {
                var image = textures.FindImage(t.Texture);
                image?.Convert(Image.Format.Rgba8);
                surfaces[t.Texture] = surface = image == null ? null : new Surface(image.GetData(), image.GetWidth(), image.GetHeight());
            }
            if (surface == null || Ground.Prepare(t, PickOf(chapter, t.Texture)!.Value, surface) is not { } g)
                continue;
            kept++;
            for (int cx = Cell(g.Min.X); cx <= Cell(g.Max.X); cx++)
            {
                for (int cz = Cell(g.Min.Z); cz <= Cell(g.Max.Z); cz++)
                {
                    if (!index.TryGetValue((cx, cz), out var list))
                        index[(cx, cz)] = list = new List<Ground>();
                    list.Add(g);
                }
            }
        }
        if (kept == 0)
            return null;
        // Every other textured polygon over a picked cell, the overlays a card is kept off.
        var overlays = new Dictionary<(int, int), List<Vector3>>();
        var others = VegetationGround.Painted(gamez, tex => PickOf(chapter, tex) == null);
        for (int i = 0; i + 2 < others.Length; i += 3)
        {
            Vector3 lo = others[i].Min(others[i + 1]).Min(others[i + 2]), hi = others[i].Max(others[i + 1]).Max(others[i + 2]);
            for (int cx = Cell(lo.X - OverlayReachM); cx <= Cell(hi.X + OverlayReachM); cx++)
            {
                for (int cz = Cell(lo.Z - OverlayReachM); cz <= Cell(hi.Z + OverlayReachM); cz++)
                {
                    if (!index.ContainsKey((cx, cz)))
                        continue;
                    if (!overlays.TryGetValue((cx, cz), out var list))
                        overlays[(cx, cz)] = list = new List<Vector3>();
                    list.AddRange(new[] { others[i], others[i + 1], others[i + 2] });
                }
            }
        }
        Log.Info("world", $"ground cover: {chapter} {kept} picked triangles over {index.Count} cells of {CellM:0} m from {string.Join(", ", surfaces.Where(s => s.Value != null).Select(s => s.Key))}, {overlays.Values.Sum(o => o.Count / 3)} overlay triangles over them, crop band {Band(Cover.Crop).X:0}-{Band(Cover.Crop).Y:0} m, grass band {Band(Cover.Grass).X:0}-{Band(Cover.Grass).Y:0} m, indexed in {watch.ElapsedMilliseconds} ms");
        return new Field(index, overlays, mask);
    }

    private static int Cell(float v) => (int)MathF.Floor(v / CellM);

    private static double Unit(uint h) => (h & 0xFFFFFF) / 16777216.0;

    private static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    // One card at a lattice point. Grass turns by a hash and scales 0.75 to 1.25. A crop card lies along its row, 1 to
    // 1.5 steps long, either way round. Its height is its patch's 0.8 to 1.15 times its own 0.85 to 1.15. It spreads 0.6
    // to 1.4 across and leans up to a tenth of its height either way. Its foot follows the slope; brightness varies 8 %.
    private static Card Make(Ground g, Cover cover, Vector3 at, double u, double v, bool alongV, uint h)
    {
        var n = g.Normal;
        Basis basis;
        if (cover == Cover.Grass)
        {
            float yaw = (float)(Unit(h) * Math.Tau), s = 0.75f + (0.5f * (float)Unit(h >> 4));
            var x = InPlane(new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw)), n);
            var z = InPlane(new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw)), n);
            basis = new Basis(x * (GrassWidthM * s), Vector3.Up * (GrassHeightM * s), z * (GrassWidthM * s));
        }
        else
        {
            var row = (alongV ? g.AxisV : g.AxisU).Normalized();
            var across = InPlane(new Vector3(-row.Z, 0f, row.X).Normalized(), n);
            uint a = Hash((int)h, 1, 2), b = Hash((int)h, 3, 4);
            float patch = 0.8f + (0.35f * (float)Unit(Hash((int)Math.Floor(u * Patches), (int)Math.Floor(v * Patches), 0x4E16)));
            float tall = CropHeightM * patch * (0.85f + (0.3f * (float)Unit(a)));
            float length = CropStepM * (1f + (0.5f * (float)Unit(a >> 8))) * ((b & 1) == 0 ? 1f : -1f);
            var lean = ((row * ((float)Unit(b >> 4) - 0.5f)) + (across * ((float)Unit(b >> 12) - 0.5f))) * (0.2f * tall);
            basis = new Basis(row * length, (Vector3.Up * tall) + lean, across * (0.6f + (0.8f * (float)Unit(a >> 16))));
        }
        float shade = 0.92f + (0.16f * (float)Unit(h >> 12));
        var c = g.ColourAt(at, u, v) * shade;
        return new Card(new Transform3D(basis, at), new Color(c.X, c.Y, c.Z), n);
    }

    // The vector over `dir`'s XZ lying in the plane of normal `n`, so a card's foot follows the slope.
    private static Vector3 InPlane(Vector3 dir, Vector3 n) => new(dir.X, -((n.X * dir.X) + (n.Z * dir.Z)) / n.Y, dir.Z);

    // A blade from (x0, y0) to (x1, y1), bowed sideways by `bend` at its middle. It is `width` texels across at its
    // foot and tapers to under one at its tip. Its gain rises from 0.45 at the card's foot to 1.35 at its top, times `shade`.
    private static void Blade(float[] gain, int w, int h, bool wrap, float x0, float y0, float x1, float y1, float bend, float width, float shade)
    {
        float cx = ((x0 + x1) * 0.5f) + bend, cy = (y0 + y1) * 0.5f;
        int steps = (int)MathF.Ceiling(2f * MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0))) + 1;
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps, a = (1f - t) * (1f - t), b = 2f * t * (1f - t), c = t * t;
            float px = (a * x0) + (b * cx) + (c * x1), py = (a * y0) + (b * cy) + (c * y1);
            float r = MathF.Max(((width * (1f - t)) + (0.6f * t)) * 0.5f, 0.3f);
            for (int y = (int)MathF.Floor(py - r); y <= (int)MathF.Ceiling(py + r); y++)
            {
                for (int x = (int)MathF.Floor(px - r); x <= (int)MathF.Ceiling(px + r); x++)
                {
                    int xx = wrap ? ((x % w) + w) % w : x;
                    if (y < 0 || y >= h || xx < 0 || xx >= w || MathF.Abs(x + 0.5f - px) > r + 0.5f || MathF.Abs(y + 0.5f - py) > r + 0.5f)
                        continue;
                    gain[(y * w) + xx] = shade * (0.45f + (0.9f * (1f - ((float)y / h))));
                }
            }
        }
    }

    // A gain map as RGBA8: the blades scaled to a mean gain of one and stored at half, a clear texel holding that mean.
    private static byte[] Encode(float[] gain, int w, int h)
    {
        double sum = 0;
        int n = 0;
        foreach (float g in gain)
        {
            if (g > 0f)
            {
                sum += g;
                n++;
            }
        }
        float k = n > 0 ? (float)(n / sum) : 1f;
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < gain.Length; i++)
        {
            byte grey = (byte)Math.Clamp((int)MathF.Round((gain[i] > 0f ? gain[i] * k : 1f) * 127.5f), 0, 255);
            rgba[i * 4] = rgba[(i * 4) + 1] = rgba[(i * 4) + 2] = grey;
            rgba[(i * 4) + 3] = gain[i] > 0f ? (byte)255 : (byte)0;
        }
        return rgba;
    }

    // The cards' mesh, a metre square a quad, foot at the origin. The tuft is three quads crossed at 60 degrees.
    // The crop row is two quads along X leaning apart, so a row seen end on still shows its width.
    private static ArrayMesh CardMesh(Cover cover)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            foreach (var (p, uv) in new[] { (a, new Vector2(0, 1)), (b, new Vector2(1, 1)), (c, new Vector2(1, 0)), (a, new Vector2(0, 1)), (c, new Vector2(1, 0)), (d, new Vector2(0, 0)) })
            {
                st.SetNormal(Vector3.Up);
                st.SetColor(Colors.White);
                st.SetUV(uv);
                st.AddVertex(p);
            }
        }

        if (cover == Cover.Grass)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * MathF.PI / 3f;
                var half = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * 0.5f;
                Quad(-half, half, half + Vector3.Up, -half + Vector3.Up);
            }
        }
        else
        {
            foreach (float lean in new[] { 0.18f, -0.18f })
                Quad(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, lean), new Vector3(-0.5f, 1f, lean));
        }
        return st.Commit();
    }

    private static ShaderMaterial Material(Cover cover)
    {
        _shader ??= new Shader { Code = ShaderCode() };
        var image = cover == Cover.Grass
            ? Image.CreateFromData(ArtSide, ArtSide, false, Image.Format.Rgba8, GrassArt())
            : Image.CreateFromData(ArtSide * 2, ArtSide, false, Image.Format.Rgba8, CropArt());
        image.GenerateMipmaps();
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("card_tex", TextureUpload.Create(image));
        material.SetShaderParameter("cover_band", Band(cover));
        material.SetShaderParameter("sway", cover == Cover.Grass ? 3f : 1.5f);
        return material;
    }

    // Lit as the Enhanced ground is, by the ground's normal in the custom data: a card shades as the ground beside
    // it. ⚠ Keep shadows_disabled: sampling the sun's shadow on the cards' overdraw cost 3 to 6 ms
    // GPU at 5120x1440, and nothing else did. The card's grey is a gain on the ground's linear colour in COLOR. Fog as
    // the lit ground's, through FOG. ⚠ Never give it an instance uniform.
    private static string ShaderCode() => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, cull_disabled, shadows_disabled{{SceneBuilder.CoverageMode}};

        uniform sampler2D card_tex : filter_linear_mipmap, repeat_enable;
        uniform vec2 cover_band;
        uniform float sway = 1.0;

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_time.gdshaderinc"

        varying flat float v_alpha;
        varying flat vec3 v_normal;

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            v_alpha = clamp((cover_band.y - distance(origin, CAMERA_POSITION_WORLD)) / max(cover_band.y - cover_band.x, 1.0), 0.0, 1.0);
        {{ClutterBuilder.LeanLines}}    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            world += vec3(breeze.x, 0.0, breeze.y) * (lean * sway * (world.y - origin.y));
            world = mix(origin, world, step(0.004, v_alpha));
            VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
            v_normal = normalize((VIEW_MATRIX * vec4(INSTANCE_CUSTOM.xyz, 0.0)).xyz);
            NORMAL = v_normal;
        }

        void fragment() {
            if (!csky_clutter_dither_keep(FRAGCOORD.xy, v_alpha)) {
                discard;
            }
            vec4 card = texture(card_tex, UV);
            ALBEDO = COLOR.rgb * (card.r * 2.0);
            NORMAL = v_normal;
            ROUGHNESS = 1.0;
            METALLIC = 0.0;
            SPECULAR = 0.0;
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), csky_fog_amount(fog_world, CAMERA_POSITION_WORLD));
            ALPHA = card.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
            ALPHA_ANTIALIASING_EDGE = 0.0;
            ALPHA_TEXTURE_COORDINATE = UV * vec2(textureSize(card_tex, 0));
        }
        """;

    /// <summary>One card: its placement (foot on the ground, X along its width or row, Y up by its height). It
    /// carries the ground's linear colour under it and the ground's normal there.</summary>
    public readonly record struct Card(Transform3D Xf, Color Albedo, Vector3 Normal);

    /// <summary>A picked texture's colour, linear and averaged over <see cref="BlurTexels"/> a side, and its row map.</summary>
    public sealed class Surface
    {
        private readonly Vector3[] _linear;
        private readonly int _w, _h;
        private readonly bool[] _rows;

        public Surface(byte[] rgba, int width, int height)
        {
            _w = Math.Max(1, width / BlurTexels);
            _h = Math.Max(1, height / BlurTexels);
            _linear = new Vector3[_w * _h];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int at = ((y * width) + x) * 4;
                    _linear[(Math.Min(y / BlurTexels, _h - 1) * _w) + Math.Min(x / BlurTexels, _w - 1)] +=
                        new Vector3(Linear(rgba[at] / 255f), Linear(rgba[at + 1] / 255f), Linear(rgba[at + 2] / 255f));
                }
            }
            float per = (float)(_w * _h) / (width * height);
            for (int i = 0; i < _linear.Length; i++)
                _linear[i] *= per;
            _rows = RowsAlongV(rgba, width, height);
        }

        /// <summary>The colour at a texture coordinate, bilinear between the averaged texels, wrapping.</summary>
        public Vector3 At(double u, double v)
        {
            double fx = (Wrap(u) * _w) - 0.5, fy = (Wrap(v) * _h) - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = (float)(fx - x0), ty = (float)(fy - y0);
            Vector3 Texel(int x, int y) => _linear[(((y % _h) + _h) % _h * _w) + (((x % _w) + _w) % _w)];
            return Texel(x0, y0).Lerp(Texel(x0 + 1, y0), tx).Lerp(Texel(x0, y0 + 1).Lerp(Texel(x0 + 1, y0 + 1), tx), ty);
        }

        /// <summary>Whether the rows at a texture coordinate run along V (<see cref="RowsAlongV"/>).</summary>
        public bool AlongV(double u, double v) =>
            _rows[(Math.Min((int)(Wrap(v) * Patches), Patches - 1) * Patches) + Math.Min((int)(Wrap(u) * Patches), Patches - 1)];

        private static double Wrap(double v) => v - Math.Floor(v);
    }

    /// <summary>One picked triangle ready to place on: its maps between world XZ, texture space and its corners.</summary>
    public sealed class Ground
    {
        private readonly CoverTriangle _t;

        // Corners A's and B's barycentric weights, each an affine map k0 + k1 p + k2 q of (u, v) or of (x, z).
        private readonly double[] _uvA, _uvB, _xzA, _xzB;

        private Ground(CoverTriangle t, Cover pick, Surface surface, Vector3 normal, double[] uvA, double[] uvB, double[] xzA, double[] xzB)
        {
            _t = t;
            Pick = pick;
            Surface = surface;
            Normal = normal;
            (_uvA, _uvB, _xzA, _xzB) = (uvA, uvB, xzA, xzB);
            AxisU = ((t.A - t.C) * (float)uvA[1]) + ((t.B - t.C) * (float)uvB[1]);
            AxisV = ((t.A - t.C) * (float)uvA[2]) + ((t.B - t.C) * (float)uvB[2]);
            Min = t.A.Min(t.B).Min(t.C);
            Max = t.A.Max(t.B).Max(t.C);
            UvMin = t.UvA.Min(t.UvB).Min(t.UvC);
            UvMax = t.UvA.Max(t.UvB).Max(t.UvC);
        }

        public Cover Pick { get; }

        public Surface Surface { get; }

        /// <summary>The up-facing unit normal.</summary>
        public Vector3 Normal { get; }

        /// <summary>World displacement per +1 of U and of V.</summary>
        public Vector3 AxisU { get; }

        /// <inheritdoc cref="AxisU"/>
        public Vector3 AxisV { get; }

        /// <summary>The world bounding box.</summary>
        public Vector3 Min { get; }

        /// <inheritdoc cref="Min"/>
        public Vector3 Max { get; }

        /// <summary>The texture-space bounding box.</summary>
        public Vector2 UvMin { get; }

        /// <inheritdoc cref="UvMin"/>
        public Vector2 UvMax { get; }

        /// <summary>The triangle ready to place on, null when it faces steeper than a bank or is degenerate in
        /// either space.</summary>
        public static Ground? Prepare(CoverTriangle t, Cover pick, Surface surface)
        {
            var n = (t.B - t.A).Cross(t.C - t.A);
            if (n.LengthSquared() < 1e-8f)
                return null;
            n = n.Normalized();
            n = n.Y < 0f ? -n : n;
            if (n.Y < MinUp || Solve(t.UvA, t.UvB, t.UvC) is not { } uv || Solve(new(t.A.X, t.A.Z), new(t.B.X, t.B.Z), new(t.C.X, t.C.Z)) is not { } xz)
                return null;
            return new Ground(t, pick, surface, n, uv.A, uv.B, xz.A, xz.B);
        }

        /// <summary>The texture coordinate at a world XZ on the triangle's plane, extrapolated off it.</summary>
        public (double U, double V) UvAt(float x, float z)
        {
            double a = Eval(_xzA, x, z), b = Eval(_xzB, x, z), c = 1 - a - b;
            return ((a * _t.UvA.X) + (b * _t.UvB.X) + (c * _t.UvC.X), (a * _t.UvA.Y) + (b * _t.UvB.Y) + (c * _t.UvC.Y));
        }

        /// <summary>The world point at a texture coordinate, null off the triangle.</summary>
        public Vector3? At(double u, double v)
        {
            double a = Eval(_uvA, u, v), b = Eval(_uvB, u, v), c = 1 - a - b;
            if (a < 0 || b < 0 || c < 0)
                return null;
            return (_t.A * (float)a) + (_t.B * (float)b) + (_t.C * (float)c);
        }

        /// <summary>The ground's linear colour at a point: the baked corner colour there, linearised as the world
        /// shader does, times the texture's.</summary>
        public Vector3 ColourAt(Vector3 at, double u, double v)
        {
            double a = Eval(_uvA, u, v), b = Eval(_uvB, u, v), c = 1 - a - b;
            var baked = (_t.ColA * (float)a) + (_t.ColB * (float)b) + (_t.ColC * (float)c);
            return new Vector3(Linear(baked.R), Linear(baked.G), Linear(baked.B)) * Surface.At(u, v);
        }

        private static double Eval(double[] k, double p, double q) => k[0] + (k[1] * p) + (k[2] * q);

        // The weights of corners a and b as affine maps of a point in the 2D frame the three corners span.
        private static (double[] A, double[] B)? Solve(Vector2 a, Vector2 b, Vector2 c)
        {
            double det = ((a.X - (double)c.X) * (b.Y - (double)c.Y)) - ((b.X - (double)c.X) * (a.Y - (double)c.Y));
            if (Math.Abs(det) < 1e-12)
                return null;
            double[] wa = { 0, (b.Y - (double)c.Y) / det, -(b.X - (double)c.X) / det };
            double[] wb = { 0, -(a.Y - (double)c.Y) / det, (a.X - (double)c.X) / det };
            wa[0] = -((wa[1] * c.X) + (wa[2] * c.Y));
            wb[0] = -((wb[1] * c.X) + (wb[2] * c.Y));
            return (wa, wb);
        }
    }

    /// <summary>The cover's cells around the cameras. The picked triangles are indexed by cell at load. One cover's
    /// cards in a cell are placed once a camera comes within its band (<see cref="Band"/>). They draw as one MultiMesh
    /// under <see cref="Root"/> and are freed past the band plus a cell. Placement is a function of the data alone, so a cell freed
    /// and built again draws the same cards.</summary>
    public sealed class Field
    {
        private readonly Dictionary<(int X, int Z), List<Ground>> _index;
        private readonly Dictionary<(int X, int Z), List<Vector3>> _overlays;
        private readonly VegetationMask? _mask;
        private readonly ArrayMesh[] _meshes = { CardMesh(Cover.Crop), CardMesh(Cover.Grass) };
        private readonly ShaderMaterial[] _materials = { Material(Cover.Crop), Material(Cover.Grass) };
        private readonly Dictionary<((int X, int Z) Cell, Cover Cover), MultiMeshInstance3D?> _live = new();
        private bool _settled;

        internal Field(Dictionary<(int X, int Z), List<Ground>> index, Dictionary<(int X, int Z), List<Vector3>> overlays, VegetationMask? mask)
        {
            _index = index;
            _overlays = overlays;
            _mask = mask;
        }

        /// <summary>The node every cell's MultiMeshes stand under.</summary>
        public Node3D Root { get; } = new() { Name = "ground_cover" };

        /// <summary>The cells built now, each counted once whatever covers it holds.</summary>
        public int Cells => _live.Keys.Select(k => k.Cell).Distinct().Count();

        /// <summary>The MultiMeshes drawing now, each one draw.</summary>
        public IEnumerable<MultiMeshInstance3D> Draws => _live.Values.OfType<MultiMeshInstance3D>();

        /// <summary>Lattice points on picked ground in the built cells that the brush mask grew nothing on.</summary>
        public int Bare { get; private set; }

        /// <summary>The cards the built cells hold, by <see cref="Cover"/>.</summary>
        public int Instances(Cover cover) => Draws.Where(m => (int)m.GetMeta("cover") == (int)cover).Sum(m => m.Multimesh.InstanceCount);

        /// <summary>Builds each cover's cells within its band's far edge of a focus, nearest first, and frees those past
        /// it by a cell. Once some stand it builds at most <paramref name="budget"/> a call; while none do, every one.
        /// Call once a frame.</summary>
        public void Update(IReadOnlyList<Vector3> focuses, int budget = MaxCellsPerFrame)
        {
            var wanted = new List<(float D, ((int X, int Z) Cell, Cover Cover) Key)>();
            var keep = new HashSet<((int, int), Cover)>();
            foreach (var cover in new[] { Cover.Crop, Cover.Grass })
            {
                float far = Band(cover).Y;
                foreach (var (d, cell) in Near(focuses, far + CellM))
                {
                    keep.Add((cell, cover));
                    if (d <= far && !_live.ContainsKey((cell, cover)))
                        wanted.Add((d, (cell, cover)));
                }
            }
            foreach (var gone in _live.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                if (_live[gone] is { } m)
                {
                    Root.RemoveChild(m);
                    m.QueueFree();
                }
                _live.Remove(gone);
            }
            wanted.Sort((a, b) => a.D.CompareTo(b.D));
            int take = _live.Count == 0 ? wanted.Count : Math.Min(budget, wanted.Count);
            for (int i = 0; i < take; i++)
                _live[wanted[i].Key] = BuildCell(wanted[i].Key.Cell, wanted[i].Key.Cover);
            if (wanted.Count > take)
                _settled = false;
            else if (!_settled && wanted.Count > 0)
            {
                _settled = true;
                Log.Info("world", $"ground cover: {Cells} cells built, {Draws.Count()} draws, crop {Instances(Cover.Crop)} and grass {Instances(Cover.Grass)} cards, {Bare} lattice points bare under the mask");
            }
        }

        // The indexed cells within `reach` of a focus, with their distance from the nearest one. The distance is XZ
        // to the cell's square and height to the span its triangles cover.
        private IEnumerable<(float D, (int X, int Z) Cell)> Near(IReadOnlyList<Vector3> focuses, float reach)
        {
            var best = new Dictionary<(int, int), float>();
            int rings = (int)MathF.Ceiling(reach / CellM);
            foreach (var f in focuses)
            {
                int fx = Cell(f.X), fz = Cell(f.Z);
                for (int x = fx - rings; x <= fx + rings; x++)
                {
                    for (int z = fz - rings; z <= fz + rings; z++)
                    {
                        if (!_index.TryGetValue((x, z), out var tris))
                            continue;
                        float lo = tris.Min(g => g.Min.Y), hi = tris.Max(g => g.Max.Y);
                        float dx = MathF.Max(MathF.Max((x * CellM) - f.X, f.X - ((x + 1) * CellM)), 0f);
                        float dz = MathF.Max(MathF.Max((z * CellM) - f.Z, f.Z - ((z + 1) * CellM)), 0f);
                        float dy = MathF.Max(MathF.Max(lo - f.Y, f.Y - hi), 0f);
                        float d = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                        if (d <= reach && (!best.TryGetValue((x, z), out float had) || d < had))
                            best[(x, z)] = d;
                    }
                }
            }
            return best.Select(b => (b.Value, b.Key));
        }

        // One cover's cards in one cell, or null where it grows none. A crop field's hedge lines grow grass, so the
        // grass build walks the crop triangles too.
        private MultiMeshInstance3D? BuildCell((int X, int Z) cell, Cover cover)
        {
            var cards = new[] { new List<Card>(), new List<Card>() };
            float x0 = cell.X * CellM, z0 = cell.Z * CellM;
            var tris = _index[cell];
            var over = _overlays.GetValueOrDefault(cell);
            foreach (var g in tris)
            {
                if (cover == Cover.Crop && g.Pick != Cover.Crop)
                    continue;
                Bare += Place(g, x0, z0, CellM, (x, z) => _mask?.TexelAt(x, z) ?? VegetationMask.NeutralByte, cards,
                    over == null ? null : p => Overlaid(over, p), cover);
            }
            var list = cards[(int)cover];
            if (list.Count == 0)
                return null;
            var at = new Vector3(x0 + (CellM / 2f), tris.Average(g => (g.Min.Y + g.Max.Y) / 2f), z0 + (CellM / 2f));
            const int stride = 20;
            var buffer = new float[list.Count * stride];
            for (int i = 0; i < list.Count; i++)
            {
                var (xf, albedo, n) = list[i];
                var b = xf.Basis;
                var o = xf.Origin - at;
                float[] row = { b.X.X, b.Y.X, b.Z.X, o.X, b.X.Y, b.Y.Y, b.Z.Y, o.Y, b.X.Z, b.Y.Z, b.Z.Z, o.Z, albedo.R, albedo.G, albedo.B, 1f, n.X, n.Y, n.Z, 0f };
                Array.Copy(row, 0, buffer, i * stride, stride);
            }
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = _meshes[(int)cover],
                InstanceCount = list.Count,
            };
            mm.Buffer = buffer;
            var mmi = new MultiMeshInstance3D
            {
                Name = $"{cover}_{cell.X}_{cell.Z}".ToLowerInvariant(),
                Multimesh = mm,
                MaterialOverride = _materials[(int)cover],
                Position = at,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = Band(cover).Y + CellM,
            };
            mmi.SetMeta("cover", (int)cover);
            Root.AddChild(mmi);
            return mmi;
        }
    }
}
