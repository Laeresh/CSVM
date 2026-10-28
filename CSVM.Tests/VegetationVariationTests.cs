using System;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Rich vegetation's per-stamp draw (<see cref="VegetationVariation"/>). A jitter never leaves
/// its source triangle and never drops a stamp. Every stamp takes the same draw count whatever its
/// values, and a zero entry is the Original stamp. The mirror and tint keep to their ranges.
/// </summary>
public class VegetationVariationTests
{
    private static readonly ClutterBuilder.UvTriangle Tri = ClutterBuilder.UvTriangle.Build(
        new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 100f),
        new Vector2(0f, 0f), new Vector2(1.3f, 0.2f), new Vector2(0.1f, 1.1f), out _)!;

    [Fact]
    public void AWideJitterStaysInTheSourceTriangleAndIsClampedRatherThanDropped()
    {
        var kind = new VegetationKind("firtree1.flt", JitterUv: 0.5f);
        var rng = new Random(7);
        var points = new Random(11);
        int clamped = 0, kept = 0;
        for (int n = 0; n < 20000; n++)
        {
            double u = points.NextDouble() * 1.3, v = points.NextDouble() * 1.1;
            if (!Tri.Contains(u, v))
                continue;
            var stamp = VegetationVariation.Draw(Tri, u, v, kind, rng);
            Assert.True(Tri.Contains(stamp.U, stamp.V), $"({u}, {v}) jittered to ({stamp.U}, {stamp.V}) outside the triangle");
            Assert.True(Math.Abs(stamp.U - u) <= 0.5 && Math.Abs(stamp.V - v) <= 0.5);
            kept++;
            clamped += stamp.Clamped ? 1 : 0;
        }
        Assert.True(kept > 5000);
        Assert.True(clamped > kept / 10, $"the clamp acted on {clamped} of {kept}");
    }

    [Fact]
    public void EveryStampTakesTheSameDrawCountWhateverItsValues()
    {
        var plain = new VegetationKind("bush1.flt");
        var busy = new VegetationKind("bush1.flt", 0.5f, 180f, 0.5f, 0.5f);
        var a = new Random(3);
        var b = new Random(3);
        var reference = new Random(3);
        for (int n = 0; n < 100; n++)
        {
            VegetationVariation.Draw(Tri, 0.3, 0.3, plain, a);
            VegetationVariation.Draw(Tri, 0.3, 0.3, busy, b);
            for (int d = 0; d < VegetationVariation.DrawsPerStamp; d++)
                reference.NextDouble();
        }
        double next = reference.NextDouble();
        Assert.Equal(next, a.NextDouble());
        Assert.Equal(next, b.NextDouble());
    }

    [Fact]
    public void AZeroEntryIsTheOriginalStamp()
    {
        var stamp = VegetationVariation.Draw(Tri, 0.3, 0.4, new VegetationKind("bush1.flt"), new Random(5));
        Assert.Equal(0.3, stamp.U);
        Assert.Equal(0.4, stamp.V);
        Assert.False(stamp.Mirror);
        Assert.False(stamp.Clamped);
        Assert.Equal(Colors.White, stamp.Tint);
    }

    [Theory]
    [InlineData(0f, 0.0)]
    [InlineData(45f, 0.25)]
    [InlineData(90f, 0.5)]
    [InlineData(180f, 1.0)]
    public void ATurnMirrorsWithProbabilityYawOver180(float yaw, double expected)
    {
        var kind = new VegetationKind("palmtree1.flt", YawDeg: yaw);
        var rng = new Random(9);
        int mirrored = 0;
        const int Count = 20000;
        for (int n = 0; n < Count; n++)
            mirrored += VegetationVariation.Draw(Tri, 0.3, 0.3, kind, rng).Mirror ? 1 : 0;
        Assert.InRange(mirrored / (double)Count, expected - 0.015, expected + 0.015);
    }

    [Fact]
    public void TintAndBrightnessKeepToTheirSpreads()
    {
        var kind = new VegetationKind("firtree1.flt", Tint: 0.05f, Brightness: 0.1f);
        var rng = new Random(13);
        float lo = float.MaxValue, hi = float.MinValue;
        for (int n = 0; n < 5000; n++)
        {
            var c = VegetationVariation.Draw(Tri, 0.3, 0.3, kind, rng).Tint;
            Assert.Equal(1f, c.A);
            foreach (float ch in new[] { c.R, c.G, c.B })
            {
                lo = Math.Min(lo, ch);
                hi = Math.Max(hi, ch);
            }
        }
        Assert.True(lo >= (0.9f * 0.95f) - 1e-5f && hi <= (1.1f * 1.05f) + 1e-5f, $"{lo}..{hi}");
        Assert.True(hi - lo > 0.2f, $"the spread is used {lo}..{hi}");
    }

    [Theory]
    [InlineData(1f, 1f, 1.0)]
    [InlineData(0.6f, 1f, 0.6)]
    [InlineData(0.6f, 0.5f, 0.3)]
    [InlineData(0f, 1f, 0.0)]
    public void AKindsKeepScalesTheMaskAndKeepsThatShareOfItsStamps(float keep, float mask, double expected)
    {
        var kind = new VegetationKind("brush2.flt", Keep: keep);
        var rng = new Random(17);
        int kept = 0;
        const int Count = 20000;
        for (int n = 0; n < Count; n++)
            kept += VegetationVariation.Kept(VegetationVariation.Draw(Tri, 0.3, 0.3, kind, rng), kind, mask) ? 1 : 0;
        Assert.InRange(kept / (double)Count, expected - 0.015, expected + 0.015);
    }

    [Fact]
    public void AKeepEditMovesNoStampAndOnlyDropsOnesItKeptBefore()
    {
        var full = new VegetationKind("brush2.flt", JitterUv: 0.2f, Tint: 0.1f);
        var thinned = full with { Keep = 0.6f };
        var a = new Random(19);
        var b = new Random(19);
        for (int n = 0; n < 1000; n++)
        {
            var before = VegetationVariation.Draw(Tri, 0.3, 0.3, full, a);
            var after = VegetationVariation.Draw(Tri, 0.3, 0.3, thinned, b);
            Assert.Equal(before, after);
            Assert.True(VegetationVariation.Kept(before, full, 1f) || !VegetationVariation.Kept(after, thinned, 1f));
        }
    }

    [Fact]
    public void TheRichStreamSeedIsFixedAndApartFromThePlacementSeed()
    {
        int placement = unchecked((int)0x8EA91836);
        Assert.Equal(VegetationVariation.Seed(placement, 0), VegetationVariation.Seed(placement, 0));
        Assert.NotEqual(placement, VegetationVariation.Seed(placement, 0));
        Assert.NotEqual(VegetationVariation.Seed(placement, 0), VegetationVariation.Seed(placement, 1));
    }
}
