using System;
using System.Threading.Tasks;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The compute half of the ocean's shore mask: world-space sea-level triangles in, the mask's and
/// the tint's texel bytes out. It touches no engine object, so it runs on the thread pool in row
/// bands. The bytes are identical for any band count. <see cref="OceanMask"/> collects the
/// triangles from the built world and uploads the bytes.
/// </summary>
internal static class OceanMaskRaster
{
    /// <summary>The texel edge in metres. Fine enough that the calm band hugs the surf ring.</summary>
    public const float Cell = 8f;

    /// <summary>The zone byte of a texel every zone group's ocean draws: water the gate never splits.</summary>
    public const byte AnyZone = 255;

    // Waves are fully calm this close to a shore or a surf texel, and reach full height here.
    private const float ShoreCalm = 24f;
    private const float ShoreFull = 160f;

    // Rows a band reads past its own edges in the distance pass. Every distance below ShoreFull is
    // a chain of at most ShoreFull / Cell steps, so a band sees every chain its own rows can take.
    private const int Halo = (int)(ShoreFull / Cell) + 4;

    // Below this many rows a band's halo costs more than its thread saves.
    private const int MinBandRows = 64;

    /// <summary>What a triangle marks: open sea, the surf ring's water, or a solid near sea level.</summary>
    public enum Kind
    {
        Base,
        Edge,
        Solid,
    }

    /// <summary>The band count the bake uses on this machine.</summary>
    public static int DefaultBands => Math.Clamp(System.Environment.ProcessorCount, 1, 16);

    /// <summary>Rasterises <paramref name="tris"/> into a mask over the base and edge triangles'
    /// extent. <paramref name="mean"/> is the sRGB tint every texel off the base sheet takes.</summary>
    public static Result Run(Tri[] tris, Color mean, int bands)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var t in tris)
        {
            if (t.Kind == Kind.Solid)
                continue;
            minX = Math.Min(minX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)));
            maxX = Math.Max(maxX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)));
            minZ = Math.Min(minZ, Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)));
            maxZ = Math.Max(maxZ, Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)));
        }
        minX = Math.Max(minX, -30000f);
        minZ = Math.Max(minZ, -30000f);
        maxX = Math.Min(maxX, 30000f);
        maxZ = Math.Min(maxZ, 30000f);
        // No spare texel past the last polygon: clamp-to-edge carries the border sea outward.
        int w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / Cell));
        int h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / Cell));
        var grid = new Grid(w, h, minX, minZ);
        var mask = new byte[w * h * 2];
        var tint = new byte[w * h * 3];
        int count = Math.Clamp(Math.Min(bands, h / MinBandRows), 1, h);
        int rows = (h + count - 1) / count;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        // Each triangle's rows, once, so a band passes over the triangles it cannot touch cheaply.
        var spans = new (int Lo, int Hi)[tris.Length];
        for (int i = 0; i < tris.Length; i++)
        {
            var t = tris[i];
            spans[i] = ((int)Math.Floor((Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)) - minZ) / Cell),
                (int)Math.Ceiling((Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)) - minZ) / Cell));
        }
        Parallel.For(0, count, b => Fill(tris, spans, grid, b * rows, Math.Min(h, (b + 1) * rows) - 1, mean, tint));
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        Parallel.For(0, count, b => Encode(grid, b * rows, Math.Min(h, (b + 1) * rows) - 1, mask));
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return new Result(w, h, new Vector2(minX, minZ), mask, tint, (t1 - t0) * tick, (t2 - t1) * tick, count, grid.Zone);
    }

    /// <summary>(byte)Math.Clamp(Math.Round(v * 255f), 0, 255), banker's rounding included, in float
    /// alone. Below 256 the fraction s - r is exact, so every comparison sees the true value.</summary>
    internal static byte Channel(float v)
    {
        float s = v * 255f;
        if (!(s > 0f))
            return 0;
        if (s >= 255f)
            return 255;
        int r = (int)s;
        float f = s - r;
        if (f > 0.5f || (f == 0.5f && (r & 1) == 1))
            r++;
        return (byte)r;
    }

    // Every triangle's texels within rows r0..r1, in list order. A later base triangle's tint wins
    // wherever two overlap, as it does in one sequential pass.
    private static void Fill(Tri[] tris, (int Lo, int Hi)[] spans, Grid g, int r0, int r1, Color mean, byte[] tint)
    {
        int w = g.W;
        var tintF = new Color[w * (r1 - r0 + 1)];
        Array.Fill(tintF, new Color(-1f, 0f, 0f));
        for (int i = 0; i < tris.Length; i++)
        {
            if (spans[i].Hi >= r0 && spans[i].Lo <= r1)
                Raster(tris[i], g, r0, r1, tintF);
        }
        // Open texels take the mean; the rounding is the one every texel had before the bands.
        byte mr = Channel(mean.R), mg = Channel(mean.G), mb = Channel(mean.B);
        for (int k = 0; k < tintF.Length; k++)
        {
            var c = tintF[k];
            int o = 3 * ((r0 * w) + k);
            bool open = c.R < 0f;
            tint[o] = open ? mr : Channel(c.R);
            tint[o + 1] = open ? mg : Channel(c.G);
            tint[o + 2] = open ? mb : Channel(c.B);
        }
    }

    // The barycentric test and the tint blend are spelled out in scalars. They keep the operation
    // order of the Vector2 and Color operators they stand for, so every texel rounds the same.
    private static void Raster(in Tri t, Grid g, int r0, int r1, Color[] tint)
    {
        float minX = g.MinX, minZ = g.MinZ;
        int w = g.W;
        float ax = t.A.X, ay = t.A.Z, bx = t.B.X, by = t.B.Z, cx = t.C.X, cy = t.C.Z;
        int z0 = Math.Max(r0, (int)Math.Floor((Math.Min(ay, Math.Min(by, cy)) - minZ) / Cell));
        int z1 = Math.Min(r1, (int)Math.Ceiling((Math.Max(ay, Math.Max(by, cy)) - minZ) / Cell));
        // A triangle smaller than a texel still marks the texels its corners land in.
        if (t.Kind == Kind.Solid)
        {
            Corner(g, (int)((ax - minX) / Cell), (int)((ay - minZ) / Cell), r0, r1);
            Corner(g, (int)((bx - minX) / Cell), (int)((by - minZ) / Cell), r0, r1);
            Corner(g, (int)((cx - minX) / Cell), (int)((cy - minZ) / Cell), r0, r1);
        }
        if (z0 > z1)
            return;
        float abx = bx - ax, aby = by - ay;
        float bcx = cx - bx, bcy = cy - by;
        float cax = ax - cx, cay = ay - cy;
        float area = (abx * (cy - ay)) - (aby * (cx - ax));
        if (Math.Abs(area) < 1e-6f)
            return;
        int x0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, Math.Min(bx, cx)) - minX) / Cell));
        int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(ax, Math.Max(bx, cx)) - minX) / Cell));
        if (x0 > x1)
            return;
        double reach = (x1 - x0 + 2) * Cell;
        for (int z = z0; z <= z1; z++)
        {
            float pz = minZ + ((z + 0.5f) * Cell);
            float paz = pz - ay, pbz = pz - by, pcz = pz - cy;
            double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
            Clip(abx, aby, paz, ax, area, reach, ref lo, ref hi);
            Clip(bcx, bcy, pbz, bx, area, reach, ref lo, ref hi);
            Clip(cax, cay, pcz, cx, area, reach, ref lo, ref hi);
            int xs = (int)Math.Clamp(Math.Floor(((lo - minX) / Cell) - 0.5) - 1, x0, x1 + 1);
            int xe = (int)Math.Clamp(Math.Ceiling(((hi - minX) / Cell) - 0.5) + 1, x0 - 1, x1);
            for (int x = xs; x <= xe; x++)
            {
                float px = minX + ((x + 0.5f) * Cell);
                float w0 = ((abx * paz) - (aby * (px - ax))) / area;
                if (w0 < -1e-4f)
                    continue;
                float w1 = ((bcx * pbz) - (bcy * (px - bx))) / area;
                if (w1 < -1e-4f)
                    continue;
                float w2 = ((cax * pcz) - (cay * (px - cx))) / area;
                if (w2 < -1e-4f)
                    continue;
                int i = (z * w) + x;
                switch (t.Kind)
                {
                    case Kind.Base:
                        if (g.Sea[i] == 0)
                            g.Sea[i] = 1;
                        if (g.Zone[i] != AnyZone)
                            g.Zone[i] = t.Zone;
                        tint[((z - r0) * w) + x] = new Color(
                            (t.CA.R * w1) + (t.CB.R * w2) + (t.CC.R * w0),
                            (t.CA.G * w1) + (t.CB.G * w2) + (t.CC.G * w0),
                            (t.CA.B * w1) + (t.CB.B * w2) + (t.CC.B * w0));
                        break;
                    case Kind.Edge:
                        g.Sea[i] = 2;
                        g.Zone[i] = AnyZone;
                        break;
                    default:
                        g.Solid[i] = true;
                        break;
                }
            }
        }
    }

    // Narrows lo..hi to the px where one edge's weight can pass the test. The bound is worked in
    // doubles and widened by two texels, far more than float rounding moves it. No texel the float
    // test takes is cut. The weight times area is ex * pRel - ey * (px - vx).
    private static void Clip(float ex, float ey, float pRel, float vx, float area, double reach, ref double lo, ref double hi)
    {
        double s = area > 0f ? 1.0 : -1.0;
        double q = s * ey;
        if (q == 0.0)
            return;
        double tol = 1e-4 * Math.Abs((double)area);
        double bound = (tol + (s * ex * pRel)) / q;
        double slack = (2.0 * Cell) + (1e-5 * (Math.Abs((double)ex * pRel) + (Math.Abs((double)ey) * reach) + tol) / Math.Abs(q));
        if (q > 0.0)
            hi = Math.Min(hi, vx + bound + slack);
        else
            lo = Math.Max(lo, vx + bound - slack);
    }

    private static void Corner(Grid g, int x, int z, int r0, int r1)
    {
        if (x >= 0 && x < g.W && z >= r0 && z <= r1)
            g.Solid[(z * g.W) + x] = true;
    }

    // Rows r0..r1 of both channels. The distance runs over the band plus its halo, then keeps its
    // own rows. A chain reaching past the halo is longer than ShoreFull and encodes as full height.
    private static void Encode(Grid g, int r0, int r1, byte[] mask)
    {
        int w = g.W;
        int e0 = Math.Max(0, r0 - Halo), e1 = Math.Min(g.H - 1, r1 + Halo);
        var dist = ShoreDistance(g, e0, e1);
        for (int y = r0; y <= r1; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                mask[2 * i] = g.Sea[i] != 0 ? (byte)255 : (byte)0;
                // SmoothStep is exactly 0 and 1 past its ends, so only the band between calls it.
                float d = dist[((y - e0) * w) + x];
                mask[(2 * i) + 1] = d <= ShoreCalm ? (byte)0 : d >= ShoreFull ? (byte)255
                    : (byte)Math.Round(Mathf.SmoothStep(ShoreCalm, ShoreFull, d) * 255f);
            }
        }
    }

    // Distance in metres to the nearest texel that is not open base sea, by a two-pass chamfer over
    // rows e0..e1, indexed from row e0.
    private static float[] ShoreDistance(Grid g, int e0, int e1)
    {
        int w = g.W, h = e1 - e0 + 1;
        var dist = new float[w * h];
        const float big = 1e6f;
        int off = e0 * w;
        for (int i = 0; i < dist.Length; i++)
            dist[i] = g.Sea[off + i] == 1 && !g.Solid[off + i] ? big : 0f;
        const float d1 = Cell, d2 = Cell * 1.4142f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                float d = dist[i];
                if (d == 0f)
                    continue;
                float e;
                if (x > 0 && (e = dist[i - 1] + d1) < d) d = e;
                if (y > 0 && (e = dist[i - w] + d1) < d) d = e;
                if (x > 0 && y > 0 && (e = dist[i - w - 1] + d2) < d) d = e;
                if (x < w - 1 && y > 0 && (e = dist[i - w + 1] + d2) < d) d = e;
                dist[i] = d;
            }
        }
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                int i = (y * w) + x;
                float d = dist[i];
                if (d == 0f)
                    continue;
                float e;
                if (x < w - 1 && (e = dist[i + 1] + d1) < d) d = e;
                if (y < h - 1 && (e = dist[i + w] + d1) < d) d = e;
                if (x < w - 1 && y < h - 1 && (e = dist[i + w + 1] + d2) < d) d = e;
                if (x > 0 && y < h - 1 && (e = dist[i + w - 1] + d2) < d) d = e;
                dist[i] = d;
            }
        }
        return dist;
    }

    /// <summary>One world-space triangle and, on the base sheet, its corners' stored vertex colours
    /// and the zone group its mesh instance is drawn in.</summary>
    public readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, Kind Kind, Color CA, Color CB, Color CC, byte Zone = 0);

    /// <summary>The texel bytes of the mask (RG8: sea, wave height), the tint (RGB8, sRGB) and the
    /// zone group (R8, <see cref="AnyZone"/> on surf-ring water). Also the milliseconds each pass
    /// took over how many bands.</summary>
    public sealed record Result(int Width, int Height, Vector2 Origin, byte[] Mask, byte[] Tint,
        double FillMs, double EncodeMs, int Bands, byte[] Zones);

    // The coverage both passes share. Each band writes only its own rows.
    private sealed class Grid
    {
        public Grid(int w, int h, float minX, float minZ)
        {
            W = w;
            H = h;
            MinX = minX;
            MinZ = minZ;
            Sea = new byte[w * h];
            Solid = new bool[w * h];
            Zone = new byte[w * h];
        }

        public int W { get; }

        public int H { get; }

        public float MinX { get; }

        public float MinZ { get; }

        // 1 open base sea, 2 the surf ring's water.
        public byte[] Sea { get; }

        public bool[] Solid { get; }

        // The zone group of the base sheet a texel shows, AnyZone on surf-ring water.
        public byte[] Zone { get; }
    }
}
