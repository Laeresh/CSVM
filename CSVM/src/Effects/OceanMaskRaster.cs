using System;
using System.Collections.Generic;
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

    /// <summary>The zone byte of a texel on a seam between two zone groups, which no grid draws.</summary>
    public const byte SeamZone = AnyZone - 1;

    /// <summary>The mask's red byte on a texel a ramp's open sea covers, where the open sea's is 255.
    /// The base sheet's hide clears a ramp up to <see cref="OceanMask.RampTop"/> where any texel its
    /// filter reads holds it (<c>csky_ocean.gdshaderinc</c> spells it as a literal). Coverage reads it
    /// as sea, as it reads 255.</summary>
    public const byte RampSea = 254;

    /// <summary>The shore distance in metres at which the mask's G saturates. G is the shore distance
    /// over this. It is the nearer of the exact coastline distance and the texel distance to a coast
    /// tile, solid off the water or uncovered texel.</summary>
    public const float ShoreReach = 160f;

    /// <summary>The lowest corner at or above which a solid cannot touch the water: the top of a ramp
    /// and the hide's band over it. A bridge deck or a roof over the sea is no shore, and marks no texel.</summary>
    public const float SolidTop = OceanMask.RampTop + OceanMask.HideHeight;

    /// <summary>How far either side of y = 0 every corner of a sea-level triangle lies. A solid within it
    /// is coplanar land, which a painted coast's water side clears.</summary>
    public const float SeaLevelBand = 0.5f;

    // Rows a band reads past its own edges in the distance pass. Every distance below ShoreReach is
    // a chain of at most ShoreReach / Cell steps, so a band sees every chain its own rows can take.
    private const int Halo = (int)(ShoreReach / Cell) + 4;

    // A solid's corner within this of y = 0 lies on the coastline. A face reaching past it on both
    // sides crosses the water.
    private const float CoastBand = 0.25f;

    // How far past a ramp its plane is lifted. It reaches the nearest texel centre beyond the rim,
    // so the linear filter reads the plane on both sides of it.
    private const double LiftReach = Cell * 1.5;

    // Below this many rows a band's halo costs more than its thread saves.
    private const int MinBandRows = 64;

    // A texel's painted-coast mark. The higher wins, so land wins where two coasts overlap.
    private const byte CoastWater = 1;
    private const byte CoastLand = 2;

    /// <summary>What a triangle marks: open sea, other water at sea level, the surf ring, a painted
    /// coast, or a solid near sea level. The surf ring is open sea with its authored coverage. A
    /// painted coast is sea with a surf coverage seaward of its painted waterline, solid landward of
    /// it. It decides its texels over every other kind.</summary>
    public enum Kind
    {
        Base,
        Edge,
        Surf,
        Coast,
        Solid,
    }

    /// <summary>The band count the bake uses on this machine.</summary>
    public static int DefaultBands => Math.Clamp(System.Environment.ProcessorCount, 1, 16);

    /// <summary>Rasterises <paramref name="tris"/> into a mask over the water triangles'
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
        var lift = new byte[w * h];
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
        Parallel.For(0, count, b => Dilate(grid, b * rows, Math.Min(h, (b + 1) * rows) - 1, tint));
        Parallel.For(0, count, b => Seam(grid, b * rows, Math.Min(h, (b + 1) * rows) - 1));
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        var coast = Coastline(tris);
        Parallel.For(0, count, b => Encode(grid, coast, b * rows, Math.Min(h, (b + 1) * rows) - 1, mask, lift));
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return new Result(w, h, new Vector2(minX, minZ), mask, tint, (t1 - t0) * tick, (t2 - t1) * tick, count, grid.DrawZone, lift, grid.Surf);
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

    // The distance from (px, pz) to the segment a..b. The tests' oracle measures the coastline by it too.
    internal static double Segment(double px, double pz, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az;
        double len = (dx * dx) + (dz * dz);
        double s = len > 0.0 ? Math.Clamp((((px - ax) * dx) + ((pz - az) * dz)) / len, 0.0, 1.0) : 0.0;
        double ex = ax + (s * dx) - px, ez = az + (s * dz) - pz;
        return Math.Sqrt((ex * ex) + (ez * ez));
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
            if (tris[i].Ramp && spans[i].Hi + 2 >= r0 && spans[i].Lo - 2 <= r1)
                Lift(tris[i], g, r0, r1);
        }
        PaintedCoasts(g, r0, r1);
        // Open texels take the mean, rounded through Channel as a tinted texel is. The bytes do not
        // depend on how the rows split into bands.
        byte mr = Channel(mean.R), mg = Channel(mean.G), mb = Channel(mean.B);
        for (int k = 0; k < tintF.Length; k++)
        {
            var c = tintF[k];
            int o = 3 * ((r0 * w) + k);
            bool open = c.R < 0f;
            g.Tinted[(r0 * w) + k] = !open;
            tint[o] = open ? mr : Channel(c.R);
            tint[o + 1] = open ? mg : Channel(c.G);
            tint[o + 2] = open ? mb : Channel(c.B);
        }
    }

    // A painted coast decides its texel once every triangle has marked it, so no triangle order
    // moves it. Its land side is solid over the tile or sheet beneath. Its water side is open sea
    // over the coplanar land under a strip, while a solid rising off the water keeps its marks. Where
    // two coasts overlap, land wins.
    private static void PaintedCoasts(Grid g, int r0, int r1)
    {
        for (int i = r0 * g.W; i < (r1 + 1) * g.W; i++)
        {
            if (g.Coast[i] == CoastLand)
            {
                g.Sea[i] = 0;
                g.Solid[i] = true;
                g.Seed[i] = true;
            }
            else if (g.Coast[i] == CoastWater)
            {
                g.Sea[i] = 1;
                g.Solid[i] = g.StandSolid[i];
                g.Seed[i] = g.StandSeed[i];
                g.Zone[i] = AnyZone;
                if (g.CoastSurf[i] > g.Surf[i])
                    g.Surf[i] = g.CoastSurf[i];
            }
        }
    }

    // An open texel beside the base sheet takes its tinted neighbours' mean. The shader filters the
    // tint linearly. Without this the sheet's last half texel blends toward the mean colour, a line
    // where an opaque coast tile meets it. Reads only tinted texels, which no band writes here, so
    // the bytes do not depend on the band count.
    private static void Dilate(Grid g, int r0, int r1, byte[] tint)
    {
        int w = g.W;
        for (int z = r0; z <= r1; z++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (z * w) + x;
                if (g.Tinted[i])
                    continue;
                int n = 0, sr = 0, sg = 0, sb = 0;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= g.H || !g.Tinted[(nz * w) + nx])
                            continue;
                        int o = 3 * ((nz * w) + nx);
                        sr += tint[o];
                        sg += tint[o + 1];
                        sb += tint[o + 2];
                        n++;
                    }
                }
                if (n == 0)
                    continue;
                tint[3 * i] = (byte)((sr + (n / 2)) / n);
                tint[(3 * i) + 1] = (byte)((sg + (n / 2)) / n);
                tint[(3 * i) + 2] = (byte)((sb + (n / 2)) / n);
            }
        }
    }

    // Rows r0..r1 of the zone groups, with every texel on a seam between two groups taken from both.
    // A seam texel's centre can fall on either side, so either grid would overhang the other group's
    // culled sea. C5 lays an opaque fog gradient along each seam, which hides the gap this leaves.
    // Reads only the filled zones, so the bytes do not depend on the band count.
    private static void Seam(Grid g, int r0, int r1)
    {
        int w = g.W;
        Array.Copy(g.Zone, r0 * w, g.DrawZone, r0 * w, (r1 - r0 + 1) * w);
        for (int y = r0; y <= r1; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                byte z = g.Zone[i];
                if (g.Sea[i] == 0)
                {
                    g.DrawZone[i] = Shore(g, x, y);
                    continue;
                }
                if (z >= SeamZone)
                    continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= g.H)
                        continue;
                    int n = (ny * w) + nx;
                    if (g.Sea[n] != 0 && g.Zone[n] < SeamZone && g.Zone[n] != z)
                        g.DrawZone[i] = SeamZone;
                }
            }
        }
    }

    // The zone of a land texel the filtered coverage reaches is the group of the sea beside it. The
    // coast's last metres then draw and cull with that sea. Beside two groups it is a seam, beside
    // water off the sheet alone every grid's. ⚠ Do not leave it 0: group 0's grid would draw a sliver of
    // shore around every other group's culled sea. The diagonals count: the filter reads them.
    private static byte Shore(Grid g, int x, int y)
    {
        int group = -1;
        bool surf = false;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= g.W || ny >= g.H)
                    continue;
                int n = (ny * g.W) + nx;
                if (g.Sea[n] == 0)
                    continue;
                byte z = g.Zone[n];
                if (z >= SeamZone)
                    surf = true;
                else if (group < 0)
                    group = z;
                else if (group != z)
                    return SeamZone;
            }
        }
        return group >= 0 ? (byte)group : surf ? AnyZone : g.Zone[(y * g.W) + x];
    }

    // The barycentric test and the tint blend are spelled out in scalars. They keep the operation
    // order of the Vector2 and Color operators they stand for, so every texel rounds the same.
    private static void Raster(in Tri t, Grid g, int r0, int r1, Color[] tint)
    {
        float minX = g.MinX, minZ = g.MinZ;
        int w = g.W;
        if (t.Kind == Kind.Solid && Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) >= SolidTop)
            return;
        float ax = t.A.X, ay = t.A.Z, bx = t.B.X, by = t.B.Z, cx = t.C.X, cy = t.C.Z;
        int z0 = Math.Max(r0, (int)Math.Floor((Math.Min(ay, Math.Min(by, cy)) - minZ) / Cell));
        int z1 = Math.Min(r1, (int)Math.Ceiling((Math.Max(ay, Math.Max(by, cy)) - minZ) / Cell));
        bool hull = t.Kind == Kind.Solid && !IsCoast(t);
        bool standing = t.Kind == Kind.Solid && !IsFlat(t);
        // A triangle smaller than a texel still marks the texels its corners land in.
        if (t.Kind == Kind.Solid)
        {
            Corner(g, (int)((ax - minX) / Cell), (int)((ay - minZ) / Cell), r0, r1, hull, standing);
            Corner(g, (int)((bx - minX) / Cell), (int)((by - minZ) / Cell), r0, r1, hull, standing);
            Corner(g, (int)((cx - minX) / Cell), (int)((cy - minZ) / Cell), r0, r1, hull, standing);
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
                        if (t.Ramp)
                            g.Ramp[i] = true;
                        tint[((z - r0) * w) + x] = new Color(
                            (t.CA.R * w1) + (t.CB.R * w2) + (t.CC.R * w0),
                            (t.CA.G * w1) + (t.CB.G * w2) + (t.CC.G * w0),
                            (t.CA.B * w1) + (t.CB.B * w2) + (t.CC.B * w0));
                        break;
                    case Kind.Edge:
                        g.Sea[i] = 2;
                        g.Zone[i] = AnyZone;
                        break;
                    case Kind.Surf:
                        if (g.Sea[i] == 0)
                            g.Sea[i] = 1;
                        g.Zone[i] = AnyZone;
                        byte surf = Channel((t.Surf.X * w1) + (t.Surf.Y * w2) + (t.Surf.Z * w0));
                        if (surf > g.Surf[i])
                            g.Surf[i] = surf;
                        break;
                    case Kind.Coast:
                        // How far seaward of the painted waterline, in fade widths.
                        float sea = (t.Surf.X * w1) + (t.Surf.Y * w2) + (t.Surf.Z * w0);
                        if (sea > 0f)
                        {
                            if (g.Coast[i] == 0)
                                g.Coast[i] = CoastWater;
                            byte cover = Channel(1f - sea);
                            if (cover > g.CoastSurf[i])
                                g.CoastSurf[i] = cover;
                        }
                        else
                        {
                            g.Coast[i] = CoastLand;
                        }
                        break;
                    default:
                        g.Solid[i] = true;
                        g.Seed[i] = true;
                        g.StandSolid[i] |= standing;
                        g.StandSeed[i] |= standing;
                        break;
                }
            }
        }
    }

    // A ramp's plane height at every texel centre within LiftReach of it, held to the ramp's own
    // heights; the highest plane wins. Sampled linearly, the ocean then meets the plane at the
    // ramp's rim and reaches the wall the ramp rises to, with no gap under it.
    private static void Lift(in Tri t, Grid g, int r0, int r1)
    {
        double ax = t.A.X, az = t.A.Z, bx = t.B.X, bz = t.B.Z, cx = t.C.X, cz = t.C.Z;
        double area = ((bx - ax) * (cz - az)) - ((bz - az) * (cx - ax));
        if (Math.Abs(area) < 1e-6)
            return;
        double top = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
        int x0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, Math.Min(bx, cx)) - LiftReach - g.MinX) / Cell));
        int x1 = Math.Min(g.W - 1, (int)Math.Ceiling((Math.Max(ax, Math.Max(bx, cx)) + LiftReach - g.MinX) / Cell));
        int z0 = Math.Max(r0, (int)Math.Floor((Math.Min(az, Math.Min(bz, cz)) - LiftReach - g.MinZ) / Cell));
        int z1 = Math.Min(r1, (int)Math.Ceiling((Math.Max(az, Math.Max(bz, cz)) + LiftReach - g.MinZ) / Cell));
        for (int z = z0; z <= z1; z++)
        {
            double pz = g.MinZ + ((z + 0.5) * Cell);
            for (int x = x0; x <= x1; x++)
            {
                double px = g.MinX + ((x + 0.5) * Cell);
                double wa = (((bx - px) * (cz - pz)) - ((bz - pz) * (cx - px))) / area;
                double wb = (((cx - px) * (az - pz)) - ((cz - pz) * (ax - px))) / area;
                double wc = 1.0 - wa - wb;
                bool inside = wa >= 0.0 && wb >= 0.0 && wc >= 0.0;
                if (!inside && Math.Min(Segment(px, pz, ax, az, bx, bz), Math.Min(Segment(px, pz, bx, bz, cx, cz), Segment(px, pz, cx, cz, ax, az))) > LiftReach)
                    continue;
                float y = (float)Math.Clamp((wa * t.A.Y) + (wb * t.B.Y) + (wc * t.C.Y), 0.0, top);
                int i = (z * g.W) + x;
                if (y > g.Lift[i])
                    g.Lift[i] = y;
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

    private static void Corner(Grid g, int x, int z, int r0, int r1, bool hull, bool standing)
    {
        if (x >= 0 && x < g.W && z >= r0 && z <= r1)
        {
            int i = (z * g.W) + x;
            g.Solid[i] = true;
            g.Seed[i] |= hull;
            g.StandSolid[i] |= standing;
            g.StandSeed[i] |= standing && hull;
        }
    }

    // A triangle within the sea-level band at every corner, the walk's own test for coplanar land.
    private static bool IsFlat(in Tri t) =>
        Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > -SeaLevelBand && Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) < SeaLevelBand;

    // A solid touching sea level, or with a coast edge, meets the water; its corner texels leave the
    // sea beside them to the coastline. Only a solid off the water's reach seeds its corners.
    private static bool IsCoast(in Tri t)
    {
        float lo = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)), hi = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
        return (lo <= CoastBand && hi >= -CoastBand) || CoastEdge(t.A, t.B) || CoastEdge(t.B, t.C) || CoastEdge(t.C, t.A);
    }

    // An edge the water can meet: both corners between sea level and the top of a ramp, which lifts
    // the water to a quay's foot. A foot need not be level: C2's rise along their ramps.
    private static bool CoastEdge(Vector3 p, Vector3 q) =>
        Math.Min(p.Y, q.Y) >= -CoastBand && Math.Max(p.Y, q.Y) < OceanMask.RampTop;

    // Rows r0..r1 of both channels. The distance runs over the band plus its halo, then keeps its
    // own rows. A chain reaching past the halo is longer than ShoreReach and saturates.
    private static void Encode(Grid g, float[] coast, int r0, int r1, byte[] mask, byte[] lift)
    {
        int w = g.W;
        int e0 = Math.Max(0, r0 - Halo), e1 = Math.Min(g.H - 1, r1 + Halo);
        var dist = ShoreDistance(g, e0, e1);
        // The nearer wins. A solid off the water beside the coast keeps its calm. Land behind a
        // coastline is never nearer than the coastline, so the coast reads the exact distance.
        Exact(g, coast, dist, e0, r0, r1);
        for (int y = r0; y <= r1; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                mask[2 * i] = g.Sea[i] == 0 ? (byte)0 : g.Ramp[i] ? RampSea : (byte)255;
                // The distance, not a height. The shader fades the swell and the look over ramps
                // of their own, from the distance the linear filter interpolates between texels.
                float d = g.Sea[i] == 1 && !g.Solid[i] ? dist[((y - e0) * w) + x] : 0f;
                mask[(2 * i) + 1] = d >= ShoreReach ? (byte)255 : (byte)Math.Round(d / ShoreReach * 255f);
                lift[i] = Channel(g.Lift[i] / OceanMask.RampTop);
            }
        }
    }

    // The coastline in world XZ, four floats a segment: every coast edge of a solid. A face crossing the
    // water's band adds its cut through y = 0, a pier's or a hull's waterline.
    private static float[] Coastline(Tri[] tris)
    {
        var seg = new List<float>();
        foreach (var t in tris)
        {
            if (t.Kind != Kind.Solid)
                continue;
            if (CoastEdge(t.A, t.B))
                seg.AddRange(new[] { t.A.X, t.A.Z, t.B.X, t.B.Z });
            if (CoastEdge(t.B, t.C))
                seg.AddRange(new[] { t.B.X, t.B.Z, t.C.X, t.C.Z });
            if (CoastEdge(t.C, t.A))
                seg.AddRange(new[] { t.C.X, t.C.Z, t.A.X, t.A.Z });
            if (Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) < -CoastBand && Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) > CoastBand)
            {
                Cut(seg, t.A, t.B);
                Cut(seg, t.B, t.C);
                Cut(seg, t.C, t.A);
            }
        }
        return seg.ToArray();
    }

    // The point where p..q crosses y = 0, if it does. Exactly two edges of a crossing face add one.
    private static void Cut(List<float> seg, Vector3 p, Vector3 q)
    {
        if ((p.Y < 0f) == (q.Y < 0f))
            return;
        float s = p.Y / (p.Y - q.Y);
        seg.Add(p.X + ((q.X - p.X) * s));
        seg.Add(p.Z + ((q.Z - p.Z) * s));
    }

    // Lowers each open-sea texel of rows r0..r1 in dist (indexed from row e0) to its exact distance from
    // every segment within ShoreReach. Each segment writes the box it reaches; past ShoreReach G saturates.
    private static void Exact(Grid g, float[] coast, float[] dist, int e0, int r0, int r1)
    {
        int w = g.W;
        float minX = g.MinX, minZ = g.MinZ;
        byte[] sea = g.Sea;
        bool[] solid = g.Solid;
        for (int s = 0; s < coast.Length; s += 4)
        {
            float ax = coast[s], az = coast[s + 1], bx = coast[s + 2], bz = coast[s + 3];
            int z0 = Math.Max(r0, (int)Math.Floor((Math.Min(az, bz) - ShoreReach - minZ) / Cell));
            int z1 = Math.Min(r1, (int)Math.Ceiling((Math.Max(az, bz) + ShoreReach - minZ) / Cell));
            int x0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, bx) - ShoreReach - minX) / Cell));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(ax, bx) + ShoreReach - minX) / Cell));
            for (int z = z0; z <= z1; z++)
            {
                float pz = minZ + ((z + 0.5f) * Cell);
                for (int x = x0; x <= x1; x++)
                {
                    int k = (z * w) + x;
                    if (sea[k] != 1 || solid[k])
                        continue;
                    int i = ((z - e0) * w) + x;
                    float e = (float)Segment(minX + ((x + 0.5f) * Cell), pz, ax, az, bx, bz);
                    if (e < dist[i])
                        dist[i] = e;
                }
            }
        }
    }

    // Distance in metres to the nearest seed texel, by a two-pass chamfer over rows e0..e1, indexed
    // from row e0. A seed is a coast tile, a texel nothing covers, a centre a solid covers, or the
    // corner texel of a solid off the water. A coast solid's does not seed: its centre can lie in the sea.
    private static float[] ShoreDistance(Grid g, int e0, int e1)
    {
        int w = g.W, h = e1 - e0 + 1;
        var dist = new float[w * h];
        const float big = 1e6f;
        int off = e0 * w;
        for (int i = 0; i < dist.Length; i++)
        {
            int k = off + i;
            dist[i] = g.Seed[k] || g.Sea[k] == 2 || (g.Sea[k] == 0 && !g.Solid[k]) ? 0f : big;
        }
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

    /// <summary>One world-space triangle. On the base sheet it carries its corners' stored vertex
    /// colours and its mesh instance's zone group. A ramp is a base triangle rising off sea level:
    /// its texels take <see cref="RampSea"/> and its plane lifts the ocean. A surf triangle carries
    /// its corners' surf coverage (1 - u) in <see cref="Surf"/>'s X, Y and Z. A painted coast carries
    /// there how far seaward of its painted waterline each corner lies, in widths of its fade. At 0
    /// or below a texel is land; above, its coverage is 1 less that.</summary>
    public readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, Kind Kind, Color CA, Color CB, Color CC, byte Zone = 0, bool Ramp = false, Vector3 Surf = default);

    /// <summary>The texel bytes of the mask, tint, zone group, lift and surf, and each pass's
    /// milliseconds over how many bands. The mask is RG8 (sea or <see cref="RampSea"/>, shore
    /// distance), the tint RGB8 sRGB. The zone is R8, <see cref="AnyZone"/> on water off the sheet
    /// and <see cref="SeamZone"/> on a seam between two groups. The lift is R8, the ocean's height
    /// over a ramp over <see cref="OceanMask.RampTop"/>. The surf is R8, the coverage of the surf
    /// ring and the painted coasts' water sides, 0 off them.</summary>
    public sealed record Result(int Width, int Height, Vector2 Origin, byte[] Mask, byte[] Tint,
        double FillMs, double EncodeMs, int Bands, byte[] Zones, byte[] Lift, byte[] Surf);

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
            Seed = new bool[w * h];
            Zone = new byte[w * h];
            DrawZone = new byte[w * h];
            Tinted = new bool[w * h];
            Ramp = new bool[w * h];
            Lift = new float[w * h];
            Surf = new byte[w * h];
            Coast = new byte[w * h];
            CoastSurf = new byte[w * h];
            StandSolid = new bool[w * h];
            StandSeed = new bool[w * h];
        }

        public int W { get; }

        public int H { get; }

        public float MinX { get; }

        public float MinZ { get; }

        // 1 open sea (the base sheet, the surf ring, a painted coast's water side), 2 other water at sea level.
        public byte[] Sea { get; }

        public bool[] Solid { get; }

        // Solid texels the chamfer runs from: every centre a solid covers, a solid off the water's corners.
        public bool[] Seed { get; }

        // The zone group of the base sheet a texel shows, AnyZone on water off the sheet.
        public byte[] Zone { get; }

        // The zone byte the grids draw each texel by. A seam texel holds SeamZone, a coast texel the
        // group of the sea beside it.
        public byte[] DrawZone { get; }

        // Texels a base triangle gave a tint; the rest start as the mean.
        public bool[] Tinted { get; }

        // Texels a ramp's base triangle covers.
        public bool[] Ramp { get; }

        // The height in metres the ocean rises to over and around a ramp.
        public float[] Lift { get; }

        // The surf ring's coverage, the highest of the surf triangles over a texel.
        public byte[] Surf { get; }

        // The painted coasts' mark (CoastWater, CoastLand) and their water sides' highest coverage.
        public byte[] Coast { get; }

        public byte[] CoastSurf { get; }

        // The Solid and Seed marks of solids rising off the water, which a coast's water side keeps.
        public bool[] StandSolid { get; }

        public bool[] StandSeed { get; }
    }
}
