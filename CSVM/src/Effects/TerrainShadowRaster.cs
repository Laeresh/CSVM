using System;
using System.Threading.Tasks;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The compute half of the baked terrain sun shadow, with no engine object touched. World-space
/// ground triangles go in. Out come three texel fields: the ground height, the line and the blocker
/// distance. The line is the height of the sun ray grazing the ground upstream along the sun's
/// azimuth, the distance how far away that ground lies. A fragment below the line is in terrain
/// shadow. <see cref="TerrainShadow"/> collects the
/// triangles, uploads the texels and publishes them.
/// </summary>
internal static class TerrainShadowRaster
{
    /// <summary>The texel edge in metres, the ocean mask's.</summary>
    public const float Cell = OceanMaskRaster.Cell;

    /// <summary>The height of a texel no triangle covers, and of a line nothing upstream casts. No
    /// fragment lies below it, so it reads lit.</summary>
    public const float Empty = -10000f;

    private const int MinBandRows = 64;

    /// <summary>Rasterises <paramref name="tris"/> (three corners per triangle) at the texel centres
    /// over their extent. Then it sweeps the grazing line along the azimuth of
    /// <paramref name="sunDir"/>, which points toward the sun. The floats are identical for any band
    /// count.</summary>
    public static Result Run(Vector3[] tris, Vector3 sunDir, int bands)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var v in tris)
        {
            minX = Math.Min(minX, v.X);
            maxX = Math.Max(maxX, v.X);
            minZ = Math.Min(minZ, v.Z);
            maxZ = Math.Max(maxZ, v.Z);
        }
        minX = Math.Max(minX, -30000f);
        minZ = Math.Max(minZ, -30000f);
        maxX = Math.Min(maxX, 30000f);
        maxZ = Math.Min(maxZ, 30000f);
        int w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / Cell));
        int h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / Cell));
        var ground = new float[w * h];
        Array.Fill(ground, Empty);
        int count = Math.Clamp(Math.Min(bands, h / MinBandRows), 1, h);
        int rows = (h + count - 1) / count;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int triCount = tris.Length / 3;
        var spans = new (int Lo, int Hi)[triCount];
        for (int i = 0; i < triCount; i++)
        {
            var (a, b, c) = (tris[3 * i], tris[(3 * i) + 1], tris[(3 * i) + 2]);
            spans[i] = ((int)Math.Floor((Math.Min(a.Z, Math.Min(b.Z, c.Z)) - minZ) / Cell),
                (int)Math.Ceiling((Math.Max(a.Z, Math.Max(b.Z, c.Z)) - minZ) / Cell));
        }
        Parallel.For(0, count, band => Fill(tris, spans, ground, w, minX, minZ, band * rows, Math.Min(h, (band + 1) * rows) - 1));
        long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        var line = new float[w * h];
        var distance = new float[w * h];
        Sweep(ground, w, h, sunDir, line, distance);
        long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return new Result(w, h, new Vector2(minX, minZ), ground, line, distance, (t1 - t0) * tick, (t2 - t1) * tick, count);
    }

    /// <summary>The sun's share reaching a fragment at height <paramref name="y"/> under a texel's
    /// <paramref name="line"/> and blocker <paramref name="distance"/>: 1 lit, 0 shaded. The ramp
    /// between spans <paramref name="grow"/> per metre of distance, at least <paramref name="least"/>.
    /// <c>csky_terrain_shadow.gdshaderinc</c> spells the same.</summary>
    public static float Lit(float line, float distance, float y, float grow, float least)
    {
        float soft = Math.Max(distance * grow, least);
        return 1f - Math.Clamp(((line - y) / soft) + 0.5f, 0f, 1f);
    }

    // Rows r0..r1 of the ground: every triangle's plane at the texel centres inside it, the highest
    // kept. Max is order-free, so the bands' rows add up to one sequential pass.
    private static void Fill(Vector3[] tris, (int Lo, int Hi)[] spans, float[] ground, int w, float minX, float minZ, int r0, int r1)
    {
        for (int t = 0; t < spans.Length; t++)
        {
            if (spans[t].Hi < r0 || spans[t].Lo > r1)
                continue;
            var (a, b, c) = (tris[3 * t], tris[(3 * t) + 1], tris[(3 * t) + 2]);
            double ax = a.X, az = a.Z, bx = b.X, bz = b.Z, cx = c.X, cz = c.Z;
            double area = ((bx - ax) * (cz - az)) - ((bz - az) * (cx - ax));
            if (Math.Abs(area) < 1e-6)
                continue;
            int x0 = Math.Max(0, (int)Math.Floor((Math.Min(ax, Math.Min(bx, cx)) - minX) / Cell));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(ax, Math.Max(bx, cx)) - minX) / Cell));
            int z0 = Math.Max(r0, spans[t].Lo), z1 = Math.Min(r1, spans[t].Hi);
            for (int z = z0; z <= z1; z++)
            {
                double pz = minZ + ((z + 0.5) * Cell);
                for (int x = x0; x <= x1; x++)
                {
                    double px = minX + ((x + 0.5) * Cell);
                    double wa = (((bx - px) * (cz - pz)) - ((bz - pz) * (cx - px))) / area;
                    double wb = (((cx - px) * (az - pz)) - ((cz - pz) * (ax - px))) / area;
                    double wc = 1.0 - wa - wb;
                    if (wa < -1e-6 || wb < -1e-6 || wc < -1e-6)
                        continue;
                    float y = (float)((wa * a.Y) + (wb * b.Y) + (wc * c.Y));
                    int i = (z * w) + x;
                    if (y > ground[i])
                        ground[i] = y;
                }
            }
        }
    }

    // One pass along the major axis of the shadow's direction. Each texel's ray came from the
    // previous column, between the two texels around its minor coordinate, read linearly. The
    // higher of a texel's ground and line carries on, dropping the sun's slope per step. A texel
    // holding no ground and no line lends its neighbour's value, so a hole casts no cliff.
    private static void Sweep(float[] ground, int w, int h, Vector3 sun, float[] line, float[] distance)
    {
        float ux = -sun.X, uz = -sun.Z;
        float horizontal = MathF.Sqrt((ux * ux) + (uz * uz));
        float tan = horizontal > 1e-6f ? sun.Y / horizontal : 0f;
        if (tan <= 0f)
        {
            // A sun straight overhead casts no terrain shadow; one below the horizon lights nothing
            // the shadow could take away.
            Array.Fill(line, Empty);
            return;
        }
        ux /= horizontal;
        uz /= horizontal;
        bool xMajor = MathF.Abs(ux) >= MathF.Abs(uz);
        int major = xMajor ? w : h, minor = xMajor ? h : w;
        float along = xMajor ? ux : uz;
        int step = along > 0f ? 1 : -1;
        float slope = (xMajor ? uz : ux) / MathF.Abs(along);
        float ds = Cell / MathF.Abs(along);
        float drop = ds * tan;
        var top = new float[minor];
        var topDistance = new float[minor];
        var next = new float[minor];
        var nextDistance = new float[minor];
        Array.Fill(top, Empty);
        for (int k = 0, c = step > 0 ? 0 : major - 1; k < major; k++, c += step)
        {
            for (int m = 0; m < minor; m++)
            {
                int i = xMajor ? (m * w) + c : (c * w) + m;
                float l = Empty, d = 0f;
                if (k > 0)
                {
                    float from = m - slope;
                    int m0 = (int)MathF.Floor(from);
                    float f = from - m0;
                    int ia = Math.Clamp(m0, 0, minor - 1), ib = Math.Clamp(m0 + 1, 0, minor - 1);
                    float ta = top[ia], tb = top[ib];
                    float height = ta <= Empty ? tb : tb <= Empty ? ta : ta + ((tb - ta) * f);
                    float dd = ta <= Empty ? topDistance[ib] : tb <= Empty ? topDistance[ia]
                        : topDistance[ia] + ((topDistance[ib] - topDistance[ia]) * f);
                    if (height > Empty)
                    {
                        l = MathF.Max(height - drop, Empty);
                        d = dd + ds;
                    }
                }
                line[i] = l;
                distance[i] = d;
                float g = ground[i];
                bool groundWins = g >= l;
                next[m] = groundWins ? g : l;
                nextDistance[m] = groundWins ? 0f : d;
            }
            (top, next) = (next, top);
            (topDistance, nextDistance) = (nextDistance, topDistance);
        }
    }

    /// <summary>The texels, row-major from the origin's corner along +X then +Z, and each pass's
    /// milliseconds. The line and the distance are what the texture carries.</summary>
    public sealed record Result(int Width, int Height, Vector2 Origin, float[] Ground, float[] Line, float[] Distance,
        double FillMs, double SweepMs, int Bands);
}
