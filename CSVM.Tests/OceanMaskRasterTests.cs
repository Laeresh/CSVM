using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using Godot;
using Xunit;
using Kind = CSVM.Effects.OceanMaskRaster.Kind;
using Tri = CSVM.Effects.OceanMaskRaster.Tri;

namespace CSVM.Tests;

/// <summary>
/// The ocean mask's texels must not depend on how the bake splits its work. Each case runs
/// <see cref="OceanMaskRaster.Run"/> at several band counts and wants identical bytes. The oracle
/// is a plain whole-image pass over every texel of every triangle's box and of every coastline
/// segment's reach. It uses the Vector2 and Color operators the scalar code stands for.
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
        var (w, h, mask, tint, surf, inland) = Reference(tris, Mean);
        foreach (int bands in new[] { 1, 5, 12 })
        {
            var r = OceanMaskRaster.Run(tris.ToArray(), Mean, bands);
            Assert.Equal(w, r.Width);
            Assert.Equal(h, r.Height);
            Assert.True(mask.AsSpan().SequenceEqual(r.Mask), $"mask differs at {bands} bands");
            Assert.True(tint.AsSpan().SequenceEqual(r.Tint), $"tint differs at {bands} bands");
            Assert.True(surf.AsSpan().SequenceEqual(r.Surf), $"surf differs at {bands} bands");
            Assert.True(inland.AsSpan().SequenceEqual(r.Inland), $"inland differs at {bands} bands");
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
    // height: the shader fades the swell and the look over ramps of their own. A solid standing off
    // the water with no level edge has no coastline, so G runs from its texels.
    [Fact]
    public void ASolidOffTheWaterTakesTheTexelDistance()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -2048f, -2048f, 4096f, Kind.Base, Colors.White);
        // A sliver smaller than a texel still marks the texel its corner lands in.
        tris.Add(Sliver(new Vector3(4.1f, 0.5f, 4.1f)));
        var p = tris[^1].A;
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 4);
        int cx = (int)((p.X - r.Origin.X) / Cell), cz = (int)((p.Z - r.Origin.Y) / Cell);
        Assert.Equal(255, r.Mask[2 * ((cz * r.Width) + cx)]);
        Assert.Equal(0, r.Mask[(2 * ((cz * r.Width) + cx)) + 1]);
        Assert.Equal(128, r.Mask[(2 * ((cz * r.Width) + cx + (int)(OceanMaskRaster.ShoreReach / Cell / 2f))) + 1]);
        Assert.Equal(255, r.Mask[(2 * ((cz * r.Width) + cx + 40)) + 1]);
    }

    // Off a coast at any angle G is the distance to the land's sea-level edge, to within the byte's
    // rounding. Texels equally far off a 45 degree coast read the same byte, so no staircase.
    [Theory]
    [InlineData(10000f, 5773.5f, false)]
    [InlineData(8000f, 8000f, true)]
    public void ADiagonalCoastReadsTheExactDistance(float dx, float dz, bool diagonal)
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        // The coastline runs through (-1.5, 1.5), off the texel lattice. The third corner stands inland, off the water.
        var a = new Vector3(-1.5f - (dx / 2f), 0f, 1.5f - (dz / 2f));
        var inland = new Vector2(-dz, dx).Normalized();
        var top = new Vector3(-1.5f + (inland.X * 6000f), 50f, 1.5f + (inland.Y * 6000f));
        tris.Add(new Tri(a, a + new Vector3(dx, 0f, dz), top, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 5);
        byte G(int x, int z) => r.Mask[(2 * ((z * r.Width) + x)) + 1];
        // Metres seaward of the coastline. The barycentric tolerance takes the texels within 0.6 m of it as land.
        float Off(int x, int z) => -((r.Origin.X + ((x + 0.5f) * Cell) + 1.5f) * inland.X) - ((r.Origin.Y + ((z + 0.5f) * Cell) - 1.5f) * inland.Y);
        int sea = 0;
        for (int z = 0; z < r.Height; z++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                float d = Off(x, z);
                if (d < -1f)
                {
                    Assert.Equal(0, G(x, z));
                }
                else if (d > 1f)
                {
                    float want = Math.Min(d, OceanMaskRaster.ShoreReach);
                    Assert.InRange(G(x, z) * OceanMaskRaster.ShoreReach / 255f, want - 0.5f, want + 0.5f);
                    if (d < OceanMaskRaster.ShoreReach)
                        sea++;
                    if (diagonal && x + 1 < r.Width && z + 1 < r.Height)
                        Assert.Equal(G(x, z), G(x + 1, z + 1));
                }
            }
        }
        Assert.True(sea > 4000, $"only {sea} sea texels within reach");
    }

    // Beside a coast a solid off the water keeps its calm: G is the nearer of the coastline and its texels.
    [Fact]
    public void BesideACoastTheNearerShoreWins()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 0f, -1024f, 1024f, Kind.Base, Colors.White);
        // Land west of x = 0 with every corner at sea level: its east edge is the coastline.
        AddQuad(tris, -1024f, -1024f, 1024f, Kind.Solid, Colors.White);
        tris.Add(Sliver(new Vector3(100.1f, 0.5f, -499.9f)));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((-500f - r.Origin.Y) / Cell);
        for (float x = 4f; x < 300f; x += Cell)
        {
            // The sliver's texel centre is at x = 100.
            float d = Math.Min(x, Math.Abs(x - 100f));
            byte want = d >= OceanMaskRaster.ShoreReach ? (byte)255 : (byte)Math.Round(d / OceanMaskRaster.ShoreReach * 255f);
            Assert.Equal(want, r.Mask[(2 * ((row * r.Width) + (int)((x - r.Origin.X) / Cell))) + 1]);
        }
    }

    // A land strip whose west edge lies at sea level. Its east edge stands 5 to 6 m up, above any
    // ramp, so the water never reaches it. The west sea reads the exact distance to the coastline.
    // The east sea has no coastline beside it and reads the texel distance from the land's covered
    // centres, not the distance across the land.
    [Fact]
    public void ALandEdgeAboveTheWaterTakesTheTexelDistance()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var a = new Vector3(-101.5f, 0f, -1024f);
        var b = new Vector3(1.5f, 5f, -1024f);
        var d = new Vector3(-101.5f, 0f, 1024f);
        var e = new Vector3(1.5f, 6f, 1024f);
        tris.Add(new Tri(a, b, e, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(a, e, d, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        byte G(float x) => r.Mask[(2 * ((row * r.Width) + (int)((x - r.Origin.X) / Cell))) + 1];
        byte Byte(float m) => (byte)Math.Round(m / OceanMaskRaster.ShoreReach * 255f);
        // West: the texel centre at x = -108 is 6.5 m off the coastline, nearer than the land's centre at -100.
        Assert.Equal(Byte(6.5f), G(-108f));
        Assert.Equal(Byte(38.5f), G(-140f));
        // East: the nearest covered centre is at x = -4.
        Assert.Equal(Byte(8f), G(4f));
        Assert.Equal(Byte(48f), G(44f));
        Assert.Equal(0, G(-52f));
    }

    // A quay as C2's harbour builds it: land 8 m up west of x = 1.5 with no sea sheet under it. Its
    // wall's foot rises along a ramp from sea level to 1.03 m, or stands level at 1.03 m. The sea
    // beside it reads the exact distance to the foot, not the texel distance from the land at x = -4.
    [Theory]
    [InlineData(0f)]
    [InlineData(1.03f)]
    public void AQuayWallReadsTheExactDistanceToItsFoot(float low)
    {
        var tris = new List<Tri>();
        AddQuad(tris, 0f, -1024f, 2048f, Kind.Base, Colors.White);
        // A sliver far west stretches the mask over the land.
        var far = new Vector3(-512f, 0f, -512f);
        tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
        var a = new Vector3(-1024f, 8f, -1024f);
        var b = new Vector3(1.5f, 8f, -1024f);
        var d = new Vector3(-1024f, 8f, 1024f);
        var e = new Vector3(1.5f, 8f, 1024f);
        tris.Add(new Tri(a, b, e, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(a, e, d, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var bFoot = b with { Y = low };
        var eFoot = e with { Y = 1.03f };
        tris.Add(new Tri(bFoot, eFoot, e, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(bFoot, e, b, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        byte G(float x) => r.Mask[(2 * ((row * r.Width) + (int)((x - r.Origin.X) / Cell))) + 1];
        byte Byte(float m) => (byte)Math.Round(m / OceanMaskRaster.ShoreReach * 255f);
        Assert.Equal(0, G(-4f));
        Assert.Equal(Byte(2.5f), G(4f));
        Assert.Equal(Byte(10.5f), G(12f));
        Assert.Equal(Byte(42.5f), G(44f));
    }

    // A pier's face crosses the water with no corner or level edge at sea level. The sea beside it
    // reads the exact distance to its waterline, the line where the face cuts y = 0.
    [Fact]
    public void APierReadsTheExactDistanceToItsWaterline()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var a = new Vector3(101.5f, -5f, -20f);
        var b = new Vector3(101.5f, -5f, 20f);
        var up = new Vector3(0f, 35f, 0f);
        tris.Add(new Tri(a, b, b + up, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(a, b + up, a + up, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        byte G(float x) => r.Mask[(2 * ((row * r.Width) + (int)((x - r.Origin.X) / Cell))) + 1];
        byte Byte(float m) => (byte)Math.Round(m / OceanMaskRaster.ShoreReach * 255f);
        // The corners stand at z = -20 and 20, two texels off this row, so they mark none of it.
        Assert.Equal(Byte(1.5f), G(100f));
        Assert.Equal(Byte(6.5f), G(108f));
        Assert.Equal(Byte(9.5f), G(92f));
        Assert.Equal(Byte(38.5f), G(140f));
    }

    // A deck hanging 14 m over open sea, as C2's bridge does, cannot touch the water. The mask is the
    // open sea's to the byte. Lowered to 2 m it is shore, its covered texels calm.
    [Fact]
    public void ASolidOverTheWaterIsNoShore()
    {
        var sea = new List<Tri>();
        AddQuad(sea, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var open = OceanMaskRaster.Run(sea.ToArray(), Mean, 3);
        List<Tri> Deck(float y)
        {
            var tris = new List<Tri>(sea);
            AddQuad(tris, -60.5f, -60.5f, 120f, Kind.Solid, Colors.White);
            for (int i = sea.Count; i < tris.Count; i++)
                tris[i] = tris[i] with { A = tris[i].A with { Y = y }, B = tris[i].B with { Y = y }, C = tris[i].C with { Y = y } };
            return tris;
        }
        var high = OceanMaskRaster.Run(Deck(14f).ToArray(), Mean, 3);
        Assert.True(open.Mask.AsSpan().SequenceEqual(high.Mask), "a deck at 14 m moves the mask");
        var low = OceanMaskRaster.Run(Deck(2f).ToArray(), Mean, 3);
        int At(float x, float z) => (2 * (((int)((z - low.Origin.Y) / Cell) * low.Width) + (int)((x - low.Origin.X) / Cell))) + 1;
        Assert.Equal(0, low.Mask[At(4f, 4f)]);
        // The deck's east edge at x = 59.5 is a coast edge: the texel centre at x = 68 reads 8.5 m.
        Assert.Equal((byte)Math.Round(8.5f / OceanMaskRaster.ShoreReach * 255f), low.Mask[At(68f, 4f)]);
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

    // A sheet split over two zone layers, with coast water on the seam. Each texel names the
    // group of the sheet it shows, the coast water every group, and no band count moves either.
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

    // A land texel the coverage filter reaches takes the group of the sea beside it, and a seam where
    // two groups meet. Group 0's grid then draws no shore of another group's sea.
    [Fact]
    public void ALandTexelTakesTheGroupOfTheSeaBesideIt()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 1024f, Kind.Base, Colors.White);
        int east = tris.Count;
        AddQuad(tris, 0f, -1024f, 1024f, Kind.Base, Colors.White);
        for (int i = east; i < tris.Count; i++)
            tris[i] = tris[i] with { Zone = 1 };
        // A sliver far south stretches the mask over the land south of z = 0.
        var far = new Vector3(-1024f, 0f, 1024f);
        tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
        var one = OceanMaskRaster.Run(tris.ToArray(), Mean, 1);
        int At(float x, float z) => ((int)((z - one.Origin.Y) / Cell) * one.Width) + (int)((x - one.Origin.X) / Cell);
        Assert.Equal(0, one.Mask[2 * At(500f, 4f)]);
        Assert.Equal(1, one.Zones[At(500f, 4f)]);
        Assert.Equal(0, one.Zones[At(-500f, 4f)]);
        Assert.Equal(OceanMaskRaster.SeamZone, one.Zones[At(-4f, 4f)]);
        Assert.Equal(OceanMaskRaster.SeamZone, one.Zones[At(4f, -4f)]);
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

    // The surf ring between the land and the sheet is open sea, which every grid draws. Its shore
    // distance runs from the land. Its surf byte runs from 1 at the land edge to 0 at the seaward edge.
    [Fact]
    public void TheSurfRingIsSeaMeasuredFromTheLand()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 64f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -1024f, -512f, 1024f, Kind.Solid, Colors.White);
        var a = new Vector3(0f, 0f, -512f);
        var b = new Vector3(64f, 0f, -512f);
        var d = new Vector3(0f, 0f, 512f);
        var e = new Vector3(64f, 0f, 512f);
        tris.Add(new Tri(a, b, e, Kind.Surf, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1f, 0f, 0f)));
        tris.Add(new Tri(a, e, d, Kind.Surf, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1f, 0f, 1f)));
        // A sliver far west stretches the mask over the land.
        var far = new Vector3(-512f, 0f, -512f);
        tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        for (float x = 4f; x < 64f; x += Cell)
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            // The coastline is the land's east edge, at x = 0.
            Assert.Equal((byte)Math.Round(x / OceanMaskRaster.ShoreReach * 255f), r.Mask[(2 * At(x)) + 1]);
            Assert.Equal(OceanMaskRaster.Channel(1f - (x / 64f)), r.Surf[At(x)]);
            Assert.Equal(OceanMaskRaster.AnyZone, r.Zones[At(x)]);
        }
        Assert.Equal(239, r.Surf[At(4f)]);
        Assert.Equal(16, r.Surf[At(60f)]);
        Assert.Equal(0, r.Surf[At(68f)]);
        Assert.Equal(0, r.Surf[At(-4f)]);
        Assert.Equal(0, r.Mask[(2 * At(-4f)) + 1]);
        Assert.True(r.Surf.AsSpan().SequenceEqual(OceanMaskRaster.Run(tris.ToArray(), Mean, 7).Surf));
    }

    // A painted strip over the sheet from land at x = 0 to x = 128. Its waterline is at x = 64, its
    // fade 32 m wide. Seaward of the waterline it is open sea, its surf byte 1 at
    // the waterline falling to 0 a fade seaward. Landward it is solid over the sheet.
    [Fact]
    public void APaintedCoastIsSeaToItsWaterline()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 0f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -1024f, -512f, 1024f, Kind.Solid, Colors.White);
        AddStrip(tris, 0f, 128f, x => (x - 64f) / 32f);
        // A sliver far west stretches the mask over the land.
        var far = new Vector3(-512f, 0f, -512f);
        tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        for (float x = 4f; x < 64f; x += Cell)
        {
            Assert.Equal(0, r.Mask[2 * At(x)]);
            Assert.Equal(0, r.Mask[(2 * At(x)) + 1]);
            Assert.Equal(0, r.Surf[At(x)]);
        }
        for (float x = 68f; x < 128f; x += Cell)
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            // The waterline is coastline: the exact distance to x = 64, nearer than the land side's centres.
            Assert.Equal((byte)Math.Round((x - 64f) / OceanMaskRaster.ShoreReach * 255f), r.Mask[(2 * At(x)) + 1]);
            Assert.InRange((int)r.Surf[At(x)], OceanMaskRaster.Channel(1f - ((x - 64f) / 32f)) - 1, OceanMaskRaster.Channel(1f - ((x - 64f) / 32f)) + 1);
            Assert.Equal(OceanMaskRaster.AnyZone, r.Zones[At(x)]);
        }
        Assert.Equal(0, r.Surf[At(100f)]);
        Assert.Equal(255, r.Mask[2 * At(132f)]);
        Assert.Equal(0, r.Surf[At(132f)]);
        AssertOrderFree(tris);
    }

    // Flat land at sea level lies under a strip's sand and part of its painted water, as in C3. The
    // strip's water side is open sea over that land, so the waves run to the painted waterline.
    [Fact]
    public void AStripsWaterSideIsSeaOverTheLandBeneathIt()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 128f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -928f, -512f, 1024f, Kind.Solid, Colors.White);
        AddStrip(tris, 0f, 128f, x => (x - 64f) / 32f);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        Assert.Equal(0, r.Mask[2 * At(60f)]);
        for (float x = 68f; x < 96f; x += Cell)
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            Assert.NotEqual(0, r.Mask[(2 * At(x)) + 1]);
            Assert.NotEqual(0, r.Surf[At(x)]);
        }
        AssertOrderFree(tris);
    }

    // A rock rising 6 m off a strip's painted water keeps its texels and its seeds: the water side
    // clears only coplanar land. The sea beyond it reads the texel distance from the rock.
    [Fact]
    public void ARisingSolidOnAStripsWaterSideKeepsItsTexels()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 0f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -1024f, -512f, 1024f, Kind.Solid, Colors.White);
        AddStrip(tris, 0f, 128f, x => (x - 64f) / 32f);
        var a = new Vector3(72f, 0f, -24f);
        var b = new Vector3(96f, 6f, -24f);
        var d = new Vector3(72f, 0f, 24f);
        var e = new Vector3(96f, 6f, 24f);
        tris.Add(new Tri(a, b, e, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(a, e, d, Kind.Solid, Colors.White, Colors.White, Colors.White));
        var far = new Vector3(-512f, 0f, -512f);
        tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
        byte Byte(float m) => (byte)Math.Round(m / OceanMaskRaster.ShoreReach * 255f);
        foreach (bool reversed in new[] { false, true })
        {
            var list = new List<Tri>(tris);
            if (reversed)
                list.Reverse();
            var r = OceanMaskRaster.Run(list.ToArray(), Mean, 3);
            int row = (int)((4f - r.Origin.Y) / Cell);
            int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
            foreach (float x in new[] { 76f, 84f, 92f })
                Assert.Equal(0, r.Mask[(2 * At(x)) + 1]);
            // The rock's nearest covered centre is at x = 92; the land side's at 60 lies farther.
            Assert.Equal(Byte(16f), r.Mask[(2 * At(108f)) + 1]);
            // The rock's foot at x = 72 lies at sea level: an exact coastline.
            Assert.Equal(Byte(4f), r.Mask[(2 * At(68f)) + 1]);
        }
        AssertOrderFree(tris);
    }

    // C1's tile is sea throughout, with no surf of its own. The rock overlay over its landward strip,
    // waterline at x = 20 and fade 10 m, is solid on its land side over the tile.
    [Fact]
    public void AnOverlaysLandSideIsSolidOverItsSeaTile()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 128f, -512f, 1024f, Kind.Base, Colors.White);
        AddStrip(tris, 0f, 128f, _ => 2f);
        AddStrip(tris, 0f, 32f, x => (x - 20f) / 10f);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        foreach (float x in new[] { 4f, 12f, 20f })
        {
            Assert.Equal(0, r.Mask[2 * At(x)]);
            Assert.Equal(0, r.Mask[(2 * At(x)) + 1]);
        }
        Assert.Equal(255, r.Mask[2 * At(28f)]);
        Assert.InRange((int)r.Surf[At(28f)], 50, 52);
        for (float x = 36f; x < 128f; x += Cell)
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            Assert.Equal(0, r.Surf[At(x)]);
        }
        AssertOrderFree(tris);
    }

    // A painted strip at any angle: its waterline is coastline. The sea in front of it reads the exact
    // distance to the painted waterline, not a texel distance from the strip's land side.
    [Theory]
    [InlineData(10000f, 5773.5f, false)]
    [InlineData(8000f, 8000f, true)]
    public void ADiagonalStripReadsTheExactDistanceToItsWaterline(float dx, float dz, bool diagonal)
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        // The waterline runs through (-1.5, 1.5), off the texel lattice, with 40 m of strip either
        // side of it at a 32 m fade. Land rises inland from the strip's landward edge.
        var o = new Vector3(-1.5f, 0f, 1.5f);
        var along = new Vector3(dx, 0f, dz).Normalized() * 1600f;
        var inland2 = new Vector2(-dz, dx).Normalized();
        var inland = new Vector3(inland2.X, 0f, inland2.Y);
        Vector3 sea0 = o - (inland * 40f) - along, sea1 = o - (inland * 40f) + along;
        Vector3 land0 = o + (inland * 40f) - along, land1 = o + (inland * 40f) + along;
        tris.Add(new Tri(sea0, sea1, land1, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1.25f, 1.25f, -1.25f)));
        tris.Add(new Tri(sea0, land1, land0, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1.25f, -1.25f, -1.25f)));
        tris.Add(new Tri(land0, land1, o + (inland * 6000f) + new Vector3(0f, 50f, 0f), Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 5);
        byte G(int x, int z) => r.Mask[(2 * ((z * r.Width) + x)) + 1];
        Vector2 Centre(int x, int z) => new(r.Origin.X + ((x + 0.5f) * Cell), r.Origin.Y + ((z + 0.5f) * Cell));
        // Metres seaward of the waterline. Only texels well inside the sheet: past its edge the mask is uncovered.
        float Off(int x, int z) => -((Centre(x, z).X - o.X) * inland2.X) - ((Centre(x, z).Y - o.Z) * inland2.Y);
        bool Inner(int x, int z) => Math.Abs(Centre(x, z).X) < 800f && Math.Abs(Centre(x, z).Y) < 800f;
        int sea = 0;
        for (int z = 0; z < r.Height; z++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                float d = Off(x, z);
                if (!Inner(x, z))
                    continue;
                if (d < -1f)
                {
                    Assert.Equal(0, G(x, z));
                }
                else if (d > 1f)
                {
                    float want = Math.Min(d, OceanMaskRaster.ShoreReach);
                    Assert.InRange(G(x, z) * OceanMaskRaster.ShoreReach / 255f, want - 0.5f, want + 0.5f);
                    if (d < OceanMaskRaster.ShoreReach)
                        sea++;
                    if (diagonal && Inner(x + 1, z + 1))
                        Assert.Equal(G(x, z), G(x + 1, z + 1));
                }
            }
        }
        Assert.True(sea > 3000, $"only {sea} sea texels within reach");
        AssertOrderFree(tris);
    }

    // The spray pick. A cliff on a painted coast 20 m behind its waterline takes sites along its foot. Each
    // faces the sea and stands at least 9 m from the next. A low wall, a gentle slope and a cliff 20 m past the
    // coast's landward edge take none. A quay wall off every painted coast takes sites marked bare, and so
    // does a breakwater on the painted coast, being no cliff rock. No triangle order moves a site.
    [Fact]
    public void TheSpraySitesStandAtTheSteepFeetBehindTheSea()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        AddStrip(tris, 0f, 80f, x => (40f - x) / 32f);
        Wall(tris, 60f, -400f, 400f, 40f);
        Wall(tris, 20f, 420f, 500f, 8f, cliff: false);
        Wall(tris, 70f, -400f, 400f, OceanMaskRaster.SprayRise - 2f);
        tris.Add(new Tri(new Vector3(50f, 0f, -300f), new Vector3(50f, 0f, 300f), new Vector3(90f, 20f, 300f), Kind.Solid, Colors.White, Colors.White, Colors.White));
        Wall(tris, -300f, -100f, 100f, 10f);
        Vector3 l0 = new(80f, 0f, -512f), l1 = new(200f, 0f, -512f), l2 = new(200f, 0f, 512f), l3 = new(80f, 0f, 512f);
        tris.Add(new Tri(l0, l1, l2, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(l0, l2, l3, Kind.Solid, Colors.White, Colors.White, Colors.White));
        Wall(tris, 100f, -400f, 400f, 40f);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 5);
        var cliff = r.Spray.Where(s => !s.Bare).ToList();
        var bare = r.Spray.Where(s => s.Bare).ToList();
        Assert.InRange(cliff.Count, 60, 70);
        Assert.All(cliff, s =>
        {
            Assert.Equal(60f, s.At.X, 2);
            Assert.InRange(s.Seaward.X, -1.001f, -0.999f);
            Assert.InRange(s.Behind, 19.5f, 20.5f);
        });
        Assert.InRange(bare.Count(s => s.At.X == -300f), 15, 20);
        Assert.InRange(bare.Count(s => s.At.X == 20f), 6, 8);
        Assert.All(bare, s =>
        {
            Assert.True(s.At.X == -300f || s.At.X == 20f, $"a bare site at x {s.At.X}");
            Assert.InRange(Math.Abs(s.Seaward.X), 0.999f, 1.001f);
        });
        for (int i = 0; i < r.Spray.Length; i++)
        {
            for (int j = i + 1; j < r.Spray.Length; j++)
                Assert.True(r.Spray[i].At.DistanceTo(r.Spray[j].At) >= OceanMaskRaster.SpraySpacing * 0.75f, $"sites {i} and {j} stand closer than the spacing allows");
        }
        var reversed = new List<Tri>(tris);
        reversed.Reverse();
        Assert.Equal(r.Spray, OceanMaskRaster.Run(reversed.ToArray(), Mean, 1).Spray);
    }

    // The inland image at the same strip. A land-side texel reads its exact distance up from the painted
    // waterline. A water-side texel reads it negated, saturating at InlandReach.
    // The solid's coast edge 40 m inland is coastline too, so only the nearer half of the sand is read.
    [Theory]
    [InlineData(10000f, 5773.5f)]
    [InlineData(8000f, 8000f)]
    public void ADiagonalStripsSandReadsItsExactInlandDistance(float dx, float dz)
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        var o = new Vector3(-1.5f, 0f, 1.5f);
        var along = new Vector3(dx, 0f, dz).Normalized() * 1600f;
        var inland2 = new Vector2(-dz, dx).Normalized();
        var inland = new Vector3(inland2.X, 0f, inland2.Y);
        Vector3 sea0 = o - (inland * 40f) - along, sea1 = o - (inland * 40f) + along;
        Vector3 land0 = o + (inland * 40f) - along, land1 = o + (inland * 40f) + along;
        tris.Add(new Tri(sea0, sea1, land1, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1.25f, 1.25f, -1.25f)));
        tris.Add(new Tri(sea0, land1, land0, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(1.25f, -1.25f, -1.25f)));
        tris.Add(new Tri(land0, land1, o + (inland * 6000f) + new Vector3(0f, 50f, 0f), Kind.Solid, Colors.White, Colors.White, Colors.White));
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 5);
        float Metres(int x, int z) => ((r.Inland[(z * r.Width) + x] / 255f * 2f) - 1f) * OceanMaskRaster.InlandReach;
        int land = 0, sea = 0;
        for (int z = 0; z < r.Height; z++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                var c = new Vector2(r.Origin.X + ((x + 0.5f) * Cell), r.Origin.Y + ((z + 0.5f) * Cell));
                if (Math.Abs(c.X) >= 800f || Math.Abs(c.Y) >= 800f)
                    continue;
                // Metres landward of the waterline.
                float d = ((c.X - o.X) * inland2.X) + ((c.Y - o.Z) * inland2.Y);
                if (d > 1f && d < 18f)
                {
                    Assert.InRange(Metres(x, z), d - 0.5f, d + 0.5f);
                    land++;
                }
                else if (d < -1f)
                {
                    float want = Math.Min(-d, OceanMaskRaster.InlandReach);
                    Assert.InRange(Metres(x, z), -want - 0.5f, -want + 0.5f);
                    sea += d > -OceanMaskRaster.InlandReach ? 1 : 0;
                }
            }
        }
        Assert.True(land > 300 && sea > 600, $"only {land} land and {sea} sea texels within reach");
        Assert.Equal(128, OceanMaskRaster.InlandByte(true, 0f));
        Assert.Equal(255, OceanMaskRaster.InlandByte(true, 1000f));
        Assert.Equal(0, OceanMaskRaster.InlandByte(false, 1000f));
        AssertOrderFree(tris);
    }

    // A strip whose sand turns opaque 16 m landward of its painted waterline at x = 40. The inland image
    // measures from that opaque line at x = 56, where the sea the mask draws ends. Its translucent sand
    // reads seaward, so the swash climbs the opaque sand. The shore distance still runs from x = 40.
    [Fact]
    public void AStripsInlandDistanceRunsFromItsOpaqueLine()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        AddStrip(tris, 0f, 80f, x => (40f - x) / 32f, opaque: 0.5f);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int At(float x) => ((int)((0f - r.Origin.Y) / Cell) * r.Width) + (int)((x - r.Origin.X) / Cell);
        float Metres(float x) => ((r.Inland[At(x)] / 255f * 2f) - 1f) * OceanMaskRaster.InlandReach;
        Assert.InRange(Metres(60f), 3.5f, 4.5f);
        Assert.InRange(Metres(68f), 11.5f, 12.5f);
        Assert.InRange(Metres(52f), -4.5f, -3.5f);
        Assert.InRange(Metres(36f), -20.5f, -19.5f);
        Assert.InRange(r.Mask[(2 * At(28f)) + 1] / 255f * OceanMaskRaster.ShoreReach, 11f, 13f);
        AssertOrderFree(tris);
    }

    // A sea tile over one corner of a sheet triangle, that corner baked dark. The corner takes the mean,
    // so no dark wedge spreads from it. A corner on a tile's edge counts; one off the tiles keeps its own.
    [Fact]
    public void ASheetCornerUnderASeaTileTakesTheMeanTint()
    {
        var dark = new Color(0f, 0.5f, 0.6f);
        var own = new Color(0.9f, 1f, 1f);
        var tris = new[]
        {
            new Tri(new Vector3(0f, 0f, 0f), new Vector3(400f, 0f, 0f), new Vector3(0f, 0f, 400f), Kind.Base, dark, own, own),
            new Tri(new Vector3(-50f, 0f, -50f), new Vector3(100f, 0f, -50f), new Vector3(-50f, 0f, 100f), Kind.Coast, Colors.White, Colors.White, Colors.White),
            new Tri(new Vector3(400f, 0f, -50f), new Vector3(450f, 0f, 0f), new Vector3(400f, 0f, 50f), Kind.Coast, Colors.White, Colors.White, Colors.White),
        };
        OceanMaskRaster.TintUnder(tris, new[] { 1, 2 }, Mean);
        Assert.Equal(Mean, tris[0].CA);
        Assert.Equal(Mean, tris[0].CB);
        Assert.Equal(own, tris[0].CC);
        Assert.Equal(Colors.White, tris[1].CA);
    }

    // A flat island at sea level from x = 0 to 1024 on the sheet. Its quad's inland diagonal joins two
    // corners on the sea. Only its ends beside the sea are coastline to the swash. The
    // land halfway along it reads no distance; the shore at x = 0 does.
    [Fact]
    public void AnInlandEdgeTouchingTheSeaAtItsEndsIsNoCoastlineAlongItsLength()
    {
        var tris = new List<Tri>();
        AddQuad(tris, -1024f, -1024f, 2048f, Kind.Base, Colors.White);
        AddQuad(tris, 0f, -512f, 1024f, Kind.Solid, Colors.White);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int At(float x, float z) => ((int)((z - r.Origin.Y) / Cell) * r.Width) + (int)((x - r.Origin.X) / Cell);
        Assert.Equal(OceanMaskRaster.InlandByte(true, OceanMaskRaster.InlandReach), r.Inland[At(512f, 0f)]);
        Assert.Equal(OceanMaskRaster.InlandByte(true, 4f), r.Inland[At(4f, 0f)]);
        AssertOrderFree(tris);
    }

    // The land's sea-level edge at x = 96 lies under the strip's painted water, which the mask makes
    // sea. It is no coastline: the sea there and beyond reads its distance to the waterline at x = 64.
    // A rock rising off that water keeps its foot at x = 104 as coastline.
    [Fact]
    public void CoplanarLandUnderAStripsWaterAddsNoCoastline()
    {
        var tris = new List<Tri>();
        AddQuad(tris, 128f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -928f, -512f, 1024f, Kind.Solid, Colors.White);
        AddStrip(tris, 0f, 128f, x => (x - 64f) / 32f);
        var a = new Vector3(104f, 0f, -24f);
        var b = new Vector3(120f, 6f, -24f);
        var d = new Vector3(104f, 0f, 24f);
        var e = new Vector3(120f, 6f, 24f);
        tris.Add(new Tri(a, b, e, Kind.Solid, Colors.White, Colors.White, Colors.White));
        tris.Add(new Tri(a, e, d, Kind.Solid, Colors.White, Colors.White, Colors.White));
        byte Byte(float m) => (byte)Math.Round(m / OceanMaskRaster.ShoreReach * 255f);
        foreach (bool reversed in new[] { false, true })
        {
            var list = new List<Tri>(tris);
            if (reversed)
                list.Reverse();
            var r = OceanMaskRaster.Run(list.ToArray(), Mean, 3);
            int At(float x, float z) => ((int)((z - r.Origin.Y) / Cell) * r.Width) + (int)((x - r.Origin.X) / Cell);
            for (float x = 68f; x < 200f; x += Cell)
                Assert.Equal(Byte(x - 64f), r.Mask[(2 * At(x, 300f)) + 1]);
            Assert.Equal(Byte(4f), r.Mask[(2 * At(100f, 4f)) + 1]);
        }
        AssertOrderFree(tris);
    }

    // An alpha strip's waterline is at x = 64; it turns opaque half a 32 m fade landward, at x = 48.
    // The sand between is sea with no distance and full surf, seaward of the swash's opaque line, and land
    // lies past x = 48. Seaward of the waterline the bytes are a strip's with no such sand.
    [Fact]
    public void AStripsTranslucentSandIsSeaWithNoDistance()
    {
        List<Tri> Scene(float opaque)
        {
            var tris = new List<Tri>();
            AddQuad(tris, 0f, -512f, 1024f, Kind.Base, Colors.White);
            AddQuad(tris, -1024f, -512f, 1024f, Kind.Solid, Colors.White);
            AddStrip(tris, 0f, 128f, x => (x - 64f) / 32f, opaque);
            var far = new Vector3(-512f, 0f, -512f);
            tris.Add(new Tri(far, far + new Vector3(1f, 0f, 0f), far + new Vector3(0f, 0f, 1f), Kind.Base, Colors.White, Colors.White, Colors.White));
            return tris;
        }

        var under = Scene(0.5f);
        var r = OceanMaskRaster.Run(under.ToArray(), Mean, 3);
        var bare = OceanMaskRaster.Run(Scene(0f).ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        foreach (float x in new[] { 4f, 28f, 44f })
        {
            Assert.Equal(0, r.Mask[2 * At(x)]);
            Assert.Equal(0, r.Surf[At(x)]);
        }
        foreach (float x in new[] { 52f, 60f })
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            Assert.Equal(0, r.Mask[(2 * At(x)) + 1]);
            Assert.Equal(255, r.Surf[At(x)]);
            Assert.Equal(0, bare.Mask[2 * At(x)]);
            Assert.Equal(0, bare.Surf[At(x)]);
            // Sea in the mask, so seaward of the opaque line the swash runs from; without the sand, land.
            Assert.Equal(OceanMaskRaster.InlandByte(false, x - 48f), r.Inland[At(x)]);
            Assert.Equal(OceanMaskRaster.InlandByte(true, 64f - x), bare.Inland[At(x)]);
        }
        for (float x = 68f; x < 400f; x += Cell)
        {
            Assert.Equal(bare.Mask[2 * At(x)], r.Mask[2 * At(x)]);
            Assert.Equal(bare.Mask[(2 * At(x)) + 1], r.Mask[(2 * At(x)) + 1]);
            Assert.Equal(bare.Surf[At(x)], r.Surf[At(x)]);
        }
        Assert.Equal((byte)Math.Round(36f / OceanMaskRaster.ShoreReach * 255f), r.Mask[(2 * At(100f)) + 1]);
        AssertOrderFree(under);
    }

    // C3's painted shallows: a flat blended decal on the sea marks nothing. The sheet under it is open
    // sea, reading its distance to the land at x = 0. A shallows triangle rising off the sea stays solid.
    [Fact]
    public void PaintedShallowsLyingFlatAreSeaAndRisingOnesSolid()
    {
        Assert.False(OceanMask.MarksSolid("shore2.tif", false, -0.17f, 0f));
        Assert.True(OceanMask.MarksSolid("shore2.tif", false, 0f, 7.64f));
        Assert.True(OceanMask.MarksSolid("sand128.tif", false, 0f, 0f));
        Assert.False(OceanMask.MarksSolid("wtr00000.tif", true, 0f, 0f));
        var tris = new List<Tri>();
        AddQuad(tris, 0f, -512f, 1024f, Kind.Base, Colors.White);
        AddQuad(tris, -1024f, -512f, 1024f, Kind.Solid, Colors.White);
        void AddIfSolid(Vector3 a, Vector3 b, Vector3 c)
        {
            if (OceanMask.MarksSolid("shore2.tif", false, Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Max(a.Y, Math.Max(b.Y, c.Y))))
                tris.Add(new Tri(a, b, c, Kind.Solid, Colors.White, Colors.White, Colors.White));
        }

        // Flat shallows from x = 40 to 100, and a rising piece from x = 300 to 340.
        AddIfSolid(new Vector3(40f, -0.17f, -64f), new Vector3(100f, 0f, -64f), new Vector3(100f, 0f, 64f));
        AddIfSolid(new Vector3(40f, -0.17f, -64f), new Vector3(100f, 0f, 64f), new Vector3(40f, 0f, 64f));
        AddIfSolid(new Vector3(300f, 0f, -64f), new Vector3(340f, 7.6f, -64f), new Vector3(340f, 7.6f, 64f));
        AddIfSolid(new Vector3(300f, 0f, -64f), new Vector3(340f, 7.6f, 64f), new Vector3(300f, 0f, 64f));
        Assert.Equal(6, tris.Count);
        var r = OceanMaskRaster.Run(tris.ToArray(), Mean, 3);
        int row = (int)((4f - r.Origin.Y) / Cell);
        int At(float x) => (row * r.Width) + (int)((x - r.Origin.X) / Cell);
        for (float x = 44f; x < 100f; x += Cell)
        {
            Assert.Equal(255, r.Mask[2 * At(x)]);
            Assert.Equal((byte)Math.Round(x / OceanMaskRaster.ShoreReach * 255f), r.Mask[(2 * At(x)) + 1]);
        }
        Assert.Equal(255, r.Mask[2 * At(316f)]);
        Assert.Equal(0, r.Mask[(2 * At(316f)) + 1]);
    }

    // C1B-like: a sheet of 512 m base quads with baked colours, scattered edge water and surf ring
    // pieces. The solids run from slivers to large faces, some reaching past the sheet.
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
        // Surf pieces, overlapping each other, the sheet, the edge water and the solids.
        for (int i = 0; i < 200; i++)
            tris.Add(Scatter(rng, Kind.Surf, rng.NextSingle() * 80f) with { Surf = new Vector3(rng.NextSingle(), rng.NextSingle(), rng.NextSingle()) });
        // Painted coasts, their waterline crossing most of them, overlapping each other and every
        // other kind; a fifth are sea tiles, water throughout. A third are alpha strips, sea under
        // their translucent sand.
        float Seaward() => (rng.NextSingle() * 3f) - 1.5f;
        for (int i = 0; i < 300; i++)
        {
            var t = Scatter(rng, Kind.Coast, rng.NextSingle() * 200f);
            tris.Add(t with
            {
                Surf = i % 5 == 0 ? new Vector3(2f, 2f, 2f) : new Vector3(Seaward(), Seaward(), Seaward()),
                Opaque = i % 3 == 0 ? rng.NextSingle() * 1.2f : 0f,
            });
        }
        // Faces crossing the water, cut at y = 0: half with no corner at sea level, half with one.
        for (int i = 0; i < 400; i++)
        {
            var t = Scatter(rng, Kind.Solid, rng.Next(4) == 0 ? 0.5f : rng.NextSingle() * 100f);
            float a = i % 2 == 0 ? -2f : 0.1f;
            tris.Add(t with { A = t.A + new Vector3(0f, a, 0f), B = t.B + new Vector3(0f, 3f, 0f), C = t.C + new Vector3(0f, -0.3f, 0f) });
        }
        // Quay walls on a foot the water reaches, level or rising, and solids off the water. Then
        // vertical pier faces with no corner at sea level.
        for (int i = 0; i < 300; i++)
        {
            var t = Scatter(rng, Kind.Solid, rng.Next(4) == 0 ? 0.5f : rng.NextSingle() * 100f);
            float foot = 0.3f + (rng.NextSingle() * 3.5f);
            tris.Add((i % 3) switch
            {
                0 => t with { A = t.A + new Vector3(0f, foot, 0f), B = t.B + new Vector3(0f, foot * rng.NextSingle(), 0f), C = t.C + new Vector3(0f, 6f, 0f) },
                1 => t with { A = t.A + new Vector3(0f, 0.5f, 0f), B = t.B + new Vector3(0f, 5f, 0f), C = t.C + new Vector3(0f, 4.5f, 0f) },
                _ => t with { A = t.A + new Vector3(0f, -2f, 0f), B = t.A + new Vector3(0f, 30f, 0f), C = t.C + new Vector3(0f, -4f, 0f) },
            });
        }
        // Decks and roofs hanging over the water, which mark nothing.
        for (int i = 0; i < 60; i++)
        {
            var t = Scatter(rng, Kind.Solid, rng.NextSingle() * 200f);
            var up = new Vector3(0f, OceanMaskRaster.SolidTop + (rng.NextSingle() * 15f), 0f);
            tris.Add(t with { A = t.A + up, B = t.B + up, C = t.C + up + new Vector3(0f, 2f, 0f) });
        }
        return tris;
    }

    // A solid smaller than a texel standing off the water. One corner is under RampTop and two above
    // it, so no edge lies where the water reaches.
    private static Tri Sliver(Vector3 p) =>
        new(p, p + new Vector3(0.1f, 5f, 0f), p + new Vector3(0f, 7f, 0.1f), Kind.Solid, Colors.White, Colors.White, Colors.White);

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

    // A painted strip from x0 to x1 along z -512..512, each corner carrying seaward(x) fade widths.
    // Its sand turns opaque `opaque` fade widths landward of its waterline.
    private static void AddStrip(List<Tri> tris, float x0, float x1, Func<float, float> seaward, float opaque = 0f)
    {
        var a = new Vector3(x0, 0f, -512f);
        var b = new Vector3(x1, 0f, -512f);
        var d = new Vector3(x0, 0f, 512f);
        var e = new Vector3(x1, 0f, 512f);
        float s0 = seaward(x0), s1 = seaward(x1);
        tris.Add(new Tri(a, b, e, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(s0, s1, s1), Opaque: opaque));
        tris.Add(new Tri(a, e, d, Kind.Coast, Colors.White, Colors.White, Colors.White, Surf: new Vector3(s0, s1, s0), Opaque: opaque));
    }

    // An upright wall at x from z0 to z1, rising h from sea level, as two solid triangles. They are cliff
    // rock unless cliff is false, as a breakwater's stone is.
    private static void Wall(List<Tri> tris, float x, float z0, float z1, float h, bool cliff = true)
    {
        Vector3 a = new(x, 0f, z0), b = new(x, 0f, z1), c = new(x, h, z1), d = new(x, h, z0);
        tris.Add(new Tri(a, b, c, Kind.Solid, Colors.White, Colors.White, Colors.White, Cliff: cliff));
        tris.Add(new Tri(a, c, d, Kind.Solid, Colors.White, Colors.White, Colors.White, Cliff: cliff));
    }

    // The painted coasts' texels hold for the triangles in reverse order and at another band count.
    private static void AssertOrderFree(List<Tri> tris)
    {
        var one = OceanMaskRaster.Run(tris.ToArray(), Mean, 1);
        var reversed = new List<Tri>(tris);
        reversed.Reverse();
        var back = OceanMaskRaster.Run(reversed.ToArray(), Mean, 7);
        Assert.True(one.Mask.AsSpan().SequenceEqual(back.Mask), "the mask moves with triangle order");
        Assert.True(one.Surf.AsSpan().SequenceEqual(back.Surf), "the surf moves with triangle order");
        Assert.True(one.Inland.AsSpan().SequenceEqual(back.Inland), "the inland moves with triangle order");
    }

    // The whole-image pass: every texel of every triangle's box, one thread, one distance pass.
    private static (int W, int H, byte[] Mask, byte[] Tint, byte[] Surf, byte[] Inland) Reference(List<Tri> tris, Color mean)
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
        var seed = new bool[w * h];
        var tint = new Color[w * h];
        var surf = new byte[w * h];
        var coast = new byte[w * h];
        var coastSurf = new byte[w * h];
        var standSolid = new bool[w * h];
        var standSeed = new bool[w * h];
        Array.Fill(tint, new Color(-1f, 0f, 0f));
        foreach (var t in tris)
            RasterReference(t, minX, minZ, w, h, sea, solid, seed, tint, surf, coast, coastSurf, standSolid, standSeed);
        // A painted coast's land (3) is solid over anything, its water (1) open sea over all but a rising
        // solid. A strip's translucent sand (2) is sea too, seeding the distance as land.
        for (int i = 0; i < w * h; i++)
        {
            if (coast[i] == 3)
            {
                sea[i] = 0;
                solid[i] = true;
                seed[i] = true;
            }
            else if (coast[i] != 0)
            {
                sea[i] = 1;
                solid[i] = standSolid[i];
                seed[i] = coast[i] == 2 || standSeed[i];
                surf[i] = Math.Max(surf[i], coastSurf[i]);
            }
        }
        var dist = DistanceReference(tris, minX, minZ, sea, solid, seed, w, h, out _, out var segments);
        // Land or a solid lies landward of the swash's coastline, a strip's translucent sand seaward. The inland
        // image measures from the runs of half-texel pieces with an open-sea texel centre within one texel
        // edge, over boxes wider than either reach.
        bool Landward(int i) => sea[i] == 0 || solid[i];
        var near = new double[w * h];
        Array.Fill(near, double.PositiveInfinity);
        static float Along(float a, float b, double s) => s == 0.0 ? a : (float)(a + (s * ((double)b - a)));
        Vector2 At(Vector2 p, Vector2 q, int k, int n) => new(Along(p.X, q.X, (double)k / n), Along(p.Y, q.Y, (double)k / n));
        foreach (var (p, q) in segments)
        {
            int n = Math.Max(1, (int)Math.Ceiling((q - p).Length() / (0.5f * Cell)));
            int from = -1;
            for (int k = 0; k <= n; k++)
            {
                bool faces = false;
                if (k < n)
                    Each(At(p, q, k, n), At(p, q, k + 1, n), 3f * Cell, (i, e) => faces |= sea[i] == 1 && !Landward(i) && e <= Cell);
                if (faces && from < 0)
                {
                    from = k;
                }
                else if (!faces && from >= 0)
                {
                    Each(At(p, q, from, n), At(p, q, k, n), OceanMaskRaster.ShoreReach, (i, e) => near[i] = Math.Min(near[i], e));
                    from = -1;
                }
            }
        }

        void Each(Vector2 p, Vector2 q, float reach, Action<int, double> visit)
        {
            int x0 = Math.Max(0, (int)Math.Floor((Math.Min(p.X, q.X) - reach - minX) / Cell));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(p.X, q.X) + reach - minX) / Cell));
            int z0 = Math.Max(0, (int)Math.Floor((Math.Min(p.Y, q.Y) - reach - minZ) / Cell));
            int z1 = Math.Min(h - 1, (int)Math.Ceiling((Math.Max(p.Y, q.Y) + reach - minZ) / Cell));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                    visit((z * w) + x, OceanMaskRaster.Segment(minX + ((x + 0.5f) * Cell), minZ + ((z + 0.5f) * Cell), p.X, p.Y, q.X, q.Y));
            }
        }
        var mask = new byte[w * h * 2];
        var tintBytes = new byte[w * h * 3];
        var inland = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            inland[i] = OceanMaskRaster.InlandByte(Landward(i), (float)Math.Min(near[i], OceanMaskRaster.InlandReach));
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
        return (w, h, mask, dilated, surf, inland);
    }

    private static void RasterReference(Tri t, float minX, float minZ, int w, int h, byte[] sea, bool[] solid, bool[] seed, Color[] tint, byte[] surf, byte[] coast, byte[] coastSurf, bool[] standSolid, bool[] standSeed)
    {
        if (t.Kind == Kind.Solid && Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) >= OceanMaskRaster.SolidTop)
            return;
        bool isHull = !OnCoast(t.A) && !OnCoast(t.B) && !OnCoast(t.C) && Coastline(t).Count == 0;
        bool rising = t.Kind == Kind.Solid && !(Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > -0.5f && Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) < 0.5f);
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
                Mark((int)((p.X - minX) / Cell), (int)((p.Y - minZ) / Cell), isHull);
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
                    Mark(x, z, true);
                    if (t.Kind == Kind.Base)
                        tint[(z * w) + x] = (t.CA * w1) + (t.CB * w2) + (t.CC * w0);
                    if (t.Kind == Kind.Surf)
                    {
                        float s = (t.Surf.X * w1) + (t.Surf.Y * w2) + (t.Surf.Z * w0);
                        surf[(z * w) + x] = Math.Max(surf[(z * w) + x], (byte)Math.Clamp(Math.Round(s * 255f), 0, 255));
                    }
                    if (t.Kind == Kind.Coast)
                    {
                        int i = (z * w) + x;
                        float s = (t.Surf.X * w1) + (t.Surf.Y * w2) + (t.Surf.Z * w0);
                        byte mark = s > 0f ? (byte)1 : s > -t.Opaque ? (byte)2 : (byte)3;
                        coast[i] = Math.Max(coast[i], mark);
                        if (mark != 3)
                            coastSurf[i] = Math.Max(coastSurf[i], (byte)Math.Clamp(Math.Round((1f - s) * 255f), 0, 255));
                    }
                }
            }
        }

        void Mark(int x, int z, bool seeds)
        {
            if (x < 0 || z < 0 || x >= w || z >= h)
                return;
            int i = (z * w) + x;
            switch (t.Kind)
            {
                case Kind.Base:
                case Kind.Surf:
                    if (sea[i] == 0)
                        sea[i] = 1;
                    break;
                case Kind.Edge:
                    sea[i] = 2;
                    break;
                case Kind.Coast:
                    break;
                default:
                    solid[i] = true;
                    seed[i] |= seeds;
                    standSolid[i] |= rising;
                    standSeed[i] |= rising && seeds;
                    break;
            }
        }
    }

    private static float Cross(Vector2 u, Vector2 v) => (u.X * v.Y) - (u.Y * v.X);

    private static bool OnCoast(Vector3 v) => Math.Abs(v.Y) <= 0.25f;

    // A solid's coastline: each edge with both corners from 0.25 m below sea level to under RampTop. A face
    // reaching past 0.25 m on both sides adds its cut through y = 0.
    private static List<(Vector2 P, Vector2 Q)> Coastline(Tri t)
    {
        var edges = new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) };
        var seg = new List<(Vector2, Vector2)>();
        foreach (var (p, q) in edges)
        {
            if (p.Y >= -0.25f && q.Y >= -0.25f && p.Y < OceanMask.RampTop && q.Y < OceanMask.RampTop)
                seg.Add((new Vector2(p.X, p.Z), new Vector2(q.X, q.Z)));
        }
        if (Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) < -0.25f && Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) > 0.25f)
        {
            var cut = new List<Vector2>();
            foreach (var (p, q) in edges)
            {
                if ((p.Y < 0f) == (q.Y < 0f))
                    continue;
                float s = p.Y / (p.Y - q.Y);
                cut.Add(new Vector2(p.X + ((q.X - p.X) * s), p.Z + ((q.Z - p.Z) * s)));
            }
            seg.Add((cut[0], cut[1]));
        }
        return seg;
    }

    // A painted coast's waterline: where its seaward value crosses 0, on a triangle the raster draws. With
    // its Opaque as shift, the line where it turns opaque.
    private static List<(Vector2 P, Vector2 Q)> Waterline(Tri t, float shift = 0f)
    {
        var seg = new List<(Vector2, Vector2)>();
        if (Math.Abs(Cross(new Vector2(t.B.X - t.A.X, t.B.Z - t.A.Z), new Vector2(t.C.X - t.A.X, t.C.Z - t.A.Z))) < 1e-6f)
            return seg;
        var cut = new List<Vector2>();
        foreach (var (p, q, vp, vq) in new[] { (t.A, t.B, t.Surf.X + shift, t.Surf.Y + shift), (t.B, t.C, t.Surf.Y + shift, t.Surf.Z + shift), (t.C, t.A, t.Surf.Z + shift, t.Surf.X + shift) })
        {
            if ((vp > 0f) == (vq > 0f))
                continue;
            float s = vp / (vp - vq);
            cut.Add(new Vector2(p.X + ((q.X - p.X) * s), p.Z + ((q.Z - p.Z) * s)));
        }
        if (cut.Count == 2)
            seg.Add((cut[0], cut[1]));
        return seg;
    }

    // A coplanar solid's segments, less their parts under any painted coast's water side, every coast tested.
    private static List<(Vector2 P, Vector2 Q)> Uncovered(List<(Vector2 P, Vector2 Q)> segs, List<Tri> coasts)
    {
        var parts = new List<float>();
        foreach (var (p, q) in segs)
            OceanMaskRaster.Uncovered(parts, p.X, p.Y, q.X, q.Y, coasts);
        var kept = new List<(Vector2, Vector2)>();
        for (int i = 0; i < parts.Count; i += 4)
            kept.Add((new Vector2(parts[i], parts[i + 1]), new Vector2(parts[i + 2], parts[i + 3])));
        return kept;
    }

    // An open-sea texel's G is the nearer of two distances. One is the chamfer from the seeds: coast
    // tiles, texels nothing covers, centres a solid covers, a solid off the water's corners. The
    // other is exact, to every solid's coastline.
    private static float[] DistanceReference(List<Tri> tris, float minX, float minZ, byte[] sea, bool[] solid, bool[] seed, int w, int h) =>
        DistanceReference(tris, minX, minZ, sea, solid, seed, w, h, out _, out _);

    // The same, with every coastline segment it measured, and the swash's: the painted coasts' opaque lines
    // in place of their waterlines.
    private static float[] DistanceReference(List<Tri> tris, float minX, float minZ, byte[] sea, bool[] solid, bool[] seed, int w, int h, out List<(Vector2 P, Vector2 Q)> all, out List<(Vector2 P, Vector2 Q)> wash)
    {
        all = new List<(Vector2 P, Vector2 Q)>();
        wash = new List<(Vector2 P, Vector2 Q)>();
        var exact = new double[w * h];
        Array.Fill(exact, double.PositiveInfinity);
        var coasts = tris.FindAll(t => t.Kind == Kind.Coast);
        foreach (var t in tris)
        {
            List<(Vector2 P, Vector2 Q)> segs;
            if (t.Kind == Kind.Coast)
                segs = Waterline(t);
            else if (t.Kind != Kind.Solid)
                continue;
            else if (Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > -0.5f && Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) < 0.5f)
                segs = Uncovered(Coastline(t), coasts);
            else
                segs = Coastline(t);
            all.AddRange(segs);
            wash.AddRange(t.Kind == Kind.Coast ? Waterline(t, t.Opaque) : segs);
            foreach (var (p, q) in segs)
            {
                // Every texel within reach of the segment, and some past it, which saturate either way.
                int x0 = Math.Max(0, (int)Math.Floor((Math.Min(p.X, q.X) - OceanMaskRaster.ShoreReach - minX) / Cell));
                int x1 = Math.Min(w - 1, (int)Math.Ceiling((Math.Max(p.X, q.X) + OceanMaskRaster.ShoreReach - minX) / Cell));
                int z0 = Math.Max(0, (int)Math.Floor((Math.Min(p.Y, q.Y) - OceanMaskRaster.ShoreReach - minZ) / Cell));
                int z1 = Math.Min(h - 1, (int)Math.Ceiling((Math.Max(p.Y, q.Y) + OceanMaskRaster.ShoreReach - minZ) / Cell));
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        double e = OceanMaskRaster.Segment(minX + ((x + 0.5f) * Cell), minZ + ((z + 0.5f) * Cell), p.X, p.Y, q.X, q.Y);
                        exact[(z * w) + x] = Math.Min(exact[(z * w) + x], e);
                    }
                }
            }
        }
        var dist = new float[w * h];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = seed[i] || sea[i] == 2 || (sea[i] == 0 && !solid[i]) ? 0f : 1e6f;
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
        for (int i = 0; i < dist.Length; i++)
            dist[i] = sea[i] == 1 && !solid[i] ? Math.Min(dist[i], (float)exact[i]) : 0f;
        return dist;
    }
}
