using System;
using System.Collections.Generic;
using CSVM.Effects;
using Godot;
using Xunit;
using Kind = CSVM.Effects.OceanMaskRaster.Kind;
using Tri = CSVM.Effects.OceanMaskRaster.Tri;

namespace CSVM.Tests;

/// <summary>
/// The ocean mask's texels must not depend on how the bake splits its work. Each case runs
/// <see cref="OceanMaskRaster.Run"/> at several band counts and wants identical bytes. The oracle
/// is a plain whole-image pass over every texel of every triangle's box. It uses the Vector2 and
/// Color operators the scalar code stands for.
/// </summary>
public class OceanMaskRasterTests
{
    private const float Cell = OceanMaskRaster.Cell;

    private static readonly Color Mean = new(0.2f, 0.35f, 0.4f);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryBandCountMatchesTheWholeImagePass(int seed)
    {
        var tris = Scene(new Random(seed));
        var (w, h, mask, tint) = Reference(tris, Mean);
        foreach (int bands in new[] { 1, 5, 12 })
        {
            var r = OceanMaskRaster.Run(tris.ToArray(), Mean, bands);
            Assert.Equal(w, r.Width);
            Assert.Equal(h, r.Height);
            Assert.True(mask.AsSpan().SequenceEqual(r.Mask), $"mask differs at {bands} bands");
            Assert.True(tint.AsSpan().SequenceEqual(r.Tint), $"tint differs at {bands} bands");
        }
    }

    // Enough rows for a dozen bands, so the distance halo and the band edges are both crossed.
    [Fact]
    public void TheSceneIsTallEnoughToSplit()
    {
        var r = OceanMaskRaster.Run(Scene(new Random(1)).ToArray(), Mean, 12);
        Assert.Equal(12, r.Bands);
    }

    // Every float a tint channel can hold, sampled, plus the ones whose product lands on a half.
    [Fact]
    public void TheChannelRoundsAsMathRoundDoes()
    {
        static byte Expected(float v) => (byte)Math.Clamp(Math.Round(v * 255f), 0, 255);
        for (float v = -0.01f; v < 1.01f; v = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(v) + (v < 0f ? -997 : 997)))
        {
            if (v < 0f && v > -1e-30f)
                v = 0f;
            Assert.Equal(Expected(v), OceanMaskRaster.Channel(v));
        }
        for (int k = 0; k < 256; k++)
        {
            int bits = BitConverter.SingleToInt32Bits((k + 0.5f) / 255f);
            for (int d = -8; d <= 8; d++)
            {
                float v = BitConverter.Int32BitsToSingle(bits + d);
                Assert.Equal(Expected(v), OceanMaskRaster.Channel(v));
            }
        }
    }

    // G is the distance from the shore over ShoreReach, saturating past it. It is not a wave
    // height: the shader fades the swell and the look over ramps of their own.
    [Fact]
    public void TheShoreChannelIsTheDistanceFromASolidCorner()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -2048f, -2048f, 4096f, Kind.Base, Colors.White);
        // A sliver smaller than a texel still marks the texel its corner lands in.
        var p = new Vector3(4.1f, 0f, 4.1f);
        tris.Add(new Tri(p, p + new Vector3(0.1f, 3f, 0f), p + new Vector3(0f, 3f, 0.1f), Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 4);
        int cx = (int)((p.X - r.Origin.X) / Cell), cz = (int)((p.Z - r.Origin.Y) / Cell);
        Assert.Equal(255, r.Mask[2 * ((cz * r.Width) + cx)]);
        Assert.Equal(0, r.Mask[(2 * ((cz * r.Width) + cx)) + 1]);
        Assert.Equal(128, r.Mask[(2 * ((cz * r.Width) + cx + (int)(OceanMaskRaster.ShoreReach / Cell / 2f))) + 1]);
        Assert.Equal(255, r.Mask[(2 * ((cz * r.Width) + cx + 40)) + 1]);
    }

    // Coast-tile water beside the sheet takes the sheet's own tint, not the mean. The shader's
    // linear filter then cannot lighten the sheet's edge against an opaque coast tile.
    [Fact]
    public void WaterBesideTheSheetTakesTheSheetsTint()
    {
        var tris = new List<Tri>();
        var dark = new Color(0.1f, 0.1f, 0.1f);
        AddQuad(tris, -1024f, -1024f, 1024f, Kind.Base, dark);
        AddQuad(tris, 0f, -1024f, 1024f, Kind.Edge, Colors.White);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int Tint(float x, float z) => r.Tint[3 * ((((int)((z - r.Origin.Y) / Cell)) * r.Width) + (int)((x - r.Origin.X) / Cell))];
        Assert.Equal(26, Tint(-4f, -500f));
        Assert.Equal(26, Tint(4f, -500f));
        Assert.Equal(51, Tint(500f, -500f));
    }

    // A sheet split over two zone layers, with surf-ring water on the seam. Each texel names the
    // group of the sheet it shows, the ring every group, and no band count moves either.
    [Fact]
    public void EachTexelNamesItsSheetsZoneGroup()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 1024f, Kind.Base, Colors.White);
        int east = tris.Count;
        AddQuad(tris, 0f, -1024f, 1024f, Kind.Base, Colors.White);
        for (int i = east; i < tris.Count; i++)
            tris[i] = tris[i] with { Zone = 1 };
        var p = new Vector3(-100f, 0f, -600f);
        tris.Add(new Tri(p, p + new Vector3(200f, 0f, 0f), p + new Vector3(0f, 0f, 200f), Kind.Edge, Colors.White, Colors.White, Colors.White));
        var one = OceanMaskRaster.Run(tris.ToArray(), Mean, 1);
        byte At(float x, float z) => one.Zones[((int)((z - one.Origin.Y) / Cell) * one.Width) + (int)((x - one.Origin.X) / Cell)];
        Assert.Equal(0, At(-500f, -100f));
        Assert.Equal(1, At(500f, -100f));
        Assert.Equal(OceanMaskRaster.AnyZone, At(-50f, -550f));
        Assert.True(one.Zones.AsSpan().SequenceEqual(OceanMaskRaster.Run(tris.ToArray(), Mean, 7).Zones));
    }

    // A ramp's base triangle marks its texels with the ramp byte, which the hide reads, and the rest
    // of the sheet stays 255. Both are sea, and no band count moves either.
    [Fact]
    public void ARampsTexelsCarryTheRampByte()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var p = new Vector3(-200f, 0f, -200f);
        tris.Add(new Tri(p, p + new Vector3(400f, 1f, 0f), p + new Vector3(0f, 0f, 400f), Kind.Base, Colors.White, Colors.White, Colors.White, Ramp: true));
        var one = OceanMaskRaster.Run(tris.ToArray(), Mean, 1);
        byte At(float x, float z) => one.Mask[2 * (((int)((z - one.Origin.Y) / Cell) * one.Width) + (int)((x - one.Origin.X) / Cell))];
        Assert.Equal(OceanMaskRaster.RampSea, At(-150f, -150f));
        Assert.Equal(255, At(500f, 500f));
        var seven = OceanMaskRaster.Run(tris.ToArray(), Mean, 7);
        Assert.True(one.Mask.AsSpan().SequenceEqual(seven.Mask));
        Assert.True(one.Lift.AsSpan().SequenceEqual(seven.Lift));
    }

    // The ocean rises with a ramp's plane, past its rim on the side it rises to, and stays at sea
    // level away from it.
    [Fact]
    public void TheLiftFollowsTheRampsPlane()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var p = new Vector3(-200f, 0f, -200f);
        tris.Add(new Tri(p, p + new Vector3(400f, 2f, 0f), p + new Vector3(0f, 0f, 400f), Kind.Base, Colors.White, Colors.White, Colors.White, Ramp: true));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        float Lift(int x, int z) => r.Lift[(z * r.Width) + x] / 255f * OceanMask.RampTop;
        float CentreX(int x) => r.Origin.X + ((x + 0.5f) * Cell);
        int zRow = (int)((-180f - r.Origin.Y) / Cell);
        for (int x = (int)((-150f - r.Origin.X) / Cell); x <= (int)((150f - r.Origin.X) / Cell); x++)
            Assert.InRange(Lift(x, zRow), ((CentreX(x) + 200f) / 200f) - 0.02f, ((CentreX(x) + 200f) / 200f) + 0.02f);
        Assert.Equal(0, r.Lift[((int)((500f - r.Origin.Y) / Cell) * r.Width) + (int)((500f - r.Origin.X) / Cell)]);
    }

    // C1B-like: a sheet of 512 m base quads with baked colours and scattered edge water. The solids
    // run from slivers to large faces, some reaching past the sheet.
    private static List<Tri> Scene(Random rng)
    {
        var tris = new List<Tri>();
        for (int qz = 0; qz < 12; qz++)
        {
            for (int qx = 0; qx < 12; qx++)
            {
                float x = -3072f + (qx * 512f), z = -3072f + (qz * 512f);
                var a = new Vector3(x, Jitter(rng), z);
                var b = new Vector3(x + 512f, Jitter(rng), z);
                var c = new Vector3(x + 512f, Jitter(rng), z + 512f);
                var d = new Vector3(x, Jitter(rng), z + 512f);
                tris.Add(new Tri(a, b, c, Kind.Base, Rgb(rng), Rgb(rng), Rgb(rng)));
                tris.Add(new Tri(a, c, d, Kind.Base, Rgb(rng), Rgb(rng), Rgb(rng)));
            }
        }
        for (int i = 0; i < 300; i++)
            tris.Add(Scatter(rng, Kind.Edge, rng.NextSingle() * 60f));
        for (int i = 0; i < 2000; i++)
            tris.Add(Scatter(rng, Kind.Solid, rng.Next(4) == 0 ? 0.5f : rng.NextSingle() * 300f));
        // Overlapping base faces: the later one's tint must win in every band.
        for (int i = 0; i < 40; i++)
            tris.Add(Scatter(rng, Kind.Base, rng.NextSingle() * 400f) with { CA = Rgb(rng), CB = Rgb(rng), CC = Rgb(rng) });
        // Needles and axis-aligned edges.
        for (int i = 0; i < 40; i++)
        {
            var p = Point(rng);
            var q = p + new Vector3((rng.NextSingle() * 2000f) - 1000f, 0f, rng.NextSingle() * 3f);
            tris.Add(new Tri(p, q, p + new Vector3(0f, 0f, 0.01f + rng.NextSingle()), Kind.Solid, Colors.White, Colors.White, Colors.White));
            tris.Add(new Tri(p, p + new Vector3(300f, 0f, 0f), p + new Vector3(0f, 0f, 300f), Kind.Edge, Colors.White, Colors.White, Colors.White));
        }
        return tris;
    }

    private static Tri Scatter(Random rng, Kind kind, float size)
    {
        var p = Point(rng);
        Vector3 Off() => new((rng.NextSingle() - 0.5f) * 2f * size, 0f, (rng.NextSingle() - 0.5f) * 2f * size);
        return new Tri(p, p + Off(), p + Off(), kind, Colors.White, Colors.White, Colors.White);
    }

    private static Vector3 Point(Random rng) => new((rng.NextSingle() * 7000f) - 3500f, 0f, (rng.NextSingle() * 7000f) - 3500f);

    private static float Jitter(Random rng) => (rng.NextSingle() - 0.5f) * 0.4f;

    private static Color Rgb(Random rng) => new(rng.Next(256) / 255f, rng.Next(256) / 255f, rng.Next(256) / 255f);

    private static void AddQuad(List<Tri> tris, float x, float z, float size, Kind kind, Color c)
    {
        var a = new Vector3(x, 0f, z);
        var b = new Vector3(x + size, 0f, z);
        var d = new Vector3(x, 0f, z + size);
        var e = new Vector3(x + size, 0f, z + size);
        tris.Add(new Tri(a, b, e, kind, c, c, c));
        tris.Add(new Tri(a, e, d, kind, c, c, c));
    }

    // The whole-image pass: every texel of every triangle's box, one thread, one distance pass.
    private static (int W, int H, byte[] Mask, byte[] Tint) Reference(List<Tri> tris, Color mean)
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
        int w = Math.Max(1, (int)Math.Ceiling((maxX - minX) / Cell));
        int h = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / Cell));
        var sea = new byte[w * h];
        var solid = new bool[w * h];
        var tint = new Color[w * h];
        Array.Fill(tint, new Color(-1f, 0f, 0f));
        foreach (var t in tris)
            RasterReference(t, minX, minZ, w, h, sea, solid, tint);
        var dist = DistanceReference(sea, solid, w, h);
        var mask = new byte[w * h * 2];
        var tintBytes = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
        {
            mask[2 * i] = sea[i] != 0 ? (byte)255 : (byte)0;
            mask[(2 * i) + 1] = (byte)Math.Round(Math.Min(dist[i], OceanMaskRaster.ShoreReach) / OceanMaskRaster.ShoreReach * 255f);
            var c = tint[i].R < 0f ? mean : tint[i];
            tintBytes[3 * i] = (byte)Math.Clamp(Math.Round(c.R * 255f), 0, 255);
            tintBytes[(3 * i) + 1] = (byte)Math.Clamp(Math.Round(c.G * 255f), 0, 255);
            tintBytes[(3 * i) + 2] = (byte)Math.Clamp(Math.Round(c.B * 255f), 0, 255);
        }
        // An open texel takes the rounded mean of its tinted neighbours, read from the undilated bytes.
        var dilated = (byte[])tintBytes.Clone();
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                if (tint[(z * w) + x].R >= 0f)
                    continue;
                var sum = new int[3];
                int n = 0;
                for (int nz = Math.Max(0, z - 1); nz <= Math.Min(h - 1, z + 1); nz++)
                {
                    for (int nx = Math.Max(0, x - 1); nx <= Math.Min(w - 1, x + 1); nx++)
                    {
                        if (tint[(nz * w) + nx].R < 0f)
                            continue;
                        for (int ch = 0; ch < 3; ch++)
                            sum[ch] += tintBytes[(3 * ((nz * w) + nx)) + ch];
                        n++;
                    }
                }
                for (int ch = 0; n > 0 && ch < 3; ch++)
                    dilated[(3 * ((z * w) + x)) + ch] = (byte)Math.Round(sum[ch] / (double)n, MidpointRounding.AwayFromZero);
            }
        }
        return (w, h, mask, dilated);
    }

    private static void RasterReference(Tri t, float minX, float minZ, int w, int h, byte[] sea, bool[] solid, Color[] tint)
    {
        var a = new Vector2(t.A.X, t.A.Z);
        var b = new Vector2(t.B.X, t.B.Z);
        var c = new Vector2(t.C.X, t.C.Z);
        float area = Cross(b - a, c - a);
        int x0 = Math.Max(0, (int)Math.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - minX) / Cell));
        int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(a.X, Math.Max(b.X, c.X)) - minX) / Cell));
        int z0 = Math.Max(0, (int)Math.Floor((Math.Min(a.Y, Math.Min(b.Y, c.Y)) - minZ) / Cell));
        int z1 = Math.Min(h - 1, (int)Math.Ceiling((Math.Max(a.Y, Math.Max(b.Y, c.Y)) - minZ) / Cell));
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
                var p = new Vector2(minX + ((x + 0.5f) * Cell), minZ + ((z + 0.5f) * Cell));
                float w0 = Cross(b - a, p - a) / area;
                float w1 = Cross(c - b, p - b) / area;
                float w2 = Cross(a - c, p - c) / area;
                if (w0 >= -1e-4f && w1 >= -1e-4f && w2 >= -1e-4f)
                {
                    Mark(x, z);
                    if (t.Kind == Kind.Base)
                        tint[(z * w) + x] = (t.CA * w1) + (t.CB * w2) + (t.CC * w0);
                }
            }
        }

        void Mark(int x, int z)
        {
            if (x < 0 || z < 0 || x >= w || z >= h)
                return;
            int i = (z * w) + x;
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

    private static float Cross(Vector2 u, Vector2 v) => (u.X * v.Y) - (u.Y * v.X);

    private static float[] DistanceReference(byte[] sea, bool[] solid, int w, int h)
    {
        var dist = new float[w * h];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = sea[i] == 1 && !solid[i] ? 1e6f : 0f;
        float d1 = Cell, d2 = Cell * 1.4142f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
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
                int i = (y * w) + x;
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
}
