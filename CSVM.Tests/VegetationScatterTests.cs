using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The remake's added stamps on a picked texture (<see cref="VegetationScatter"/>). Placement is a function of
/// the triangle's UVs and the set's seed, one scatter per texture repeat. The strict edge test and the
/// quarter-metre dedup hold, and the brush mask clears, keeps half or keeps nearly all.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VegetationScatterTests
{
    private static readonly int[] Keys = { 1, 2, 3 };

    [Fact]
    public void PlacementIsAFunctionOfTheTrianglesUvsAndTheSeed()
    {
        var tri = Tri(Vector3.Zero);
        double perRepeat = VegetationScatter.PerRepeat(VegetationSet.DefaultDensity, tri);
        Assert.Equal(2.0 * 7.85, perRepeat, 3);
        var here = Place(tri, 5, perRepeat);
        var moved = Place(Tri(new Vector3(-3000f, 40f, 1234f)), 5, perRepeat);
        Assert.NotEmpty(here);
        Assert.Equal(here, moved);
        Assert.Equal(here, Place(tri, 5, perRepeat));
        Assert.NotEqual(here, Place(tri, 6, perRepeat));
    }

    /// <summary>Every repeat wholly inside the triangle holds the same points, the scatter's first candidates.
    /// The last one is weighted by the fraction the density leaves.</summary>
    [Fact]
    public void EachTextureRepeatTakesTheSameScatter()
    {
        var tri = Tri(Vector3.Zero);
        const double perRepeat = 15.7;
        var cell = new VegetationScatter.Scatter(9).Take(16).Take(16).ToList();
        var placed = Place(tri, 9, perRepeat);
        foreach (var (u0, v0) in new[] { (0, 0), (1, 0), (0, 1), (1, 1), (2, 0), (0, 2) })
        {
            var inRepeat = placed.Where(c => (int)Math.Floor(c.U) == u0 && (int)Math.Floor(c.V) == v0).ToList();
            Assert.Equal(16, inRepeat.Count);
            for (int j = 0; j < 16; j++)
            {
                Assert.Equal(cell[j].U, inRepeat[j].U - u0, 9);
                Assert.Equal(cell[j].V, inRepeat[j].V - v0, 9);
                Assert.Equal(cell[j].Member(Keys.Length), inRepeat[j].Member);
                Assert.Equal(j < 15 ? 1f : 0.7f, inRepeat[j].Weight, 4);
            }
        }
    }

    /// <summary>A scatter's prefix never depends on how far it grew, and its points spread apart.</summary>
    [Fact]
    public void TheScatterIsPrefixStableAndSpread()
    {
        var short10 = new VegetationScatter.Scatter(3).Take(10).Take(10).ToList();
        var long50 = new VegetationScatter.Scatter(3).Take(50).ToList();
        Assert.Equal(short10, long50.Take(10));
        double nearest = double.MaxValue;
        for (int i = 0; i < long50.Count; i++)
        {
            for (int j = i + 1; j < long50.Count; j++)
            {
                double du = Math.Abs(long50[i].U - long50[j].U), dv = Math.Abs(long50[i].V - long50[j].V);
                du = Math.Min(du, 1 - du);
                dv = Math.Min(dv, 1 - dv);
                nearest = Math.Min(nearest, Math.Sqrt((du * du) + (dv * dv)));
            }
        }
        Assert.True(nearest > 0.25 / Math.Sqrt(50), $"two of 50 points stand {nearest:0.###} apart");
    }

    /// <summary>A point on the diagonal two triangles share is claimed by neither, and every other by exactly one.
    /// A coplanar copy of a triangle adds nothing past the dedup.</summary>
    [Fact]
    public void TheStrictEdgeTestAndTheDedupHold()
    {
        var a = ClutterBuilder.UvTriangle.Build(new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 100f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), out _)!;
        var b = ClutterBuilder.UvTriangle.Build(new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 100f),
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), out _)!;
        var points = new[] { new VegetationScatter.Point(0.5, 0.5, 0.1), new VegetationScatter.Point(0.7, 0.2, 0.5), new VegetationScatter.Point(0.2, 0.7, 0.9) };
        var seen = new HashSet<(int, int, int)>();
        int deduped = 0;
        var inA = VegetationScatter.OnTriangle(a, points, 3, Keys, seen, ref deduped);
        var inB = VegetationScatter.OnTriangle(b, points, 3, Keys, seen, ref deduped);
        Assert.Equal((0.7, 0.2), (Assert.Single(inA).U, inA[0].V));
        Assert.Equal((0.2, 0.7), (Assert.Single(inB).U, inB[0].V));
        Assert.Equal(0, deduped);
        Assert.Empty(VegetationScatter.OnTriangle(a, points, 3, Keys, seen, ref deduped));
        Assert.Equal(1, deduped);
    }

    /// <summary>The brush mask scales the added stamps: 0 places none, neutral keeps about the set's density
    /// and twice neutral nearly every candidate.</summary>
    [Fact]
    public void TheMaskClearsKeepsHalfOrThickens()
    {
        var placed = Place(Tri(Vector3.Zero), 4, 40.0);
        double weight = placed.Sum(c => c.Weight);
        int Kept(float mask)
        {
            var draws = new Random(21);
            return placed.Count(c => VegetationScatter.Keeps(c, mask, draws.NextDouble()));
        }
        Assert.Equal(0, Kept(0f));
        Assert.InRange(Kept(1f), 0.45 * weight, 0.55 * weight);
        Assert.InRange(Kept(255f / VegetationMask.NeutralByte), 0.95 * weight, weight);
    }

    /// <summary>Only a pick to a set with members and density stamps.
    /// The reserved canopy set never does, even with a member, since painted forest takes no card.</summary>
    [Fact]
    public void OnlyAPickedSetStampsAndTheCanopySetNever()
    {
        var entry = new VegetationEntry
        {
            Kinds = new[] { new VegetationKind("palmtree1.flt") },
            Sets = new[]
            {
                new VegetationSet(CanopyMask.CanopySet, new[] { "palmtree1.flt" }),
                new VegetationSet("palms", new[] { "palmtree1.flt" }),
                new VegetationSet("bare", new[] { "palmtree1.flt" }, Density: 0f),
            },
            Picks = new[] { new TexturePick("forest1.tif", CanopyMask.CanopySet), new TexturePick("cliff1_toptrans.tif", "palms"), new TexturePick("sand.tif", "bare") },
        };
        Assert.Null(VegetationScatter.Stamped(entry, "forest1.tif"));
        Assert.Equal("palms", VegetationScatter.Stamped(entry, "CLIFF1_TOPTRANS.tif")!.Name);
        Assert.Null(VegetationScatter.Stamped(entry, "sand.tif"));
        Assert.Null(VegetationScatter.Stamped(entry, "terpat03.tif"));
    }

    /// <summary>The scatter's seed is fixed arithmetic of the chapter's and the set's name, never a per-process hash.</summary>
    [Fact]
    public void TheScatterSeedIsFixedAndPerSet()
    {
        Assert.Equal(VegetationScatter.ScatterSeed(11, "palms"), VegetationScatter.ScatterSeed(11, "PALMS"));
        Assert.NotEqual(VegetationScatter.ScatterSeed(11, "palms"), VegetationScatter.ScatterSeed(11, "hedge"));
        Assert.NotEqual(VegetationScatter.ScatterSeed(11, "palms"), VegetationScatter.ScatterSeed(12, "palms"));
        Assert.Equal(-52039509, VegetationScatter.ScatterSeed(0, "palms"));
    }

    // A 100 m square repeat (one hectare) over UVs 0..4 on each axis, half of it as one triangle.
    private static ClutterBuilder.UvTriangle Tri(Vector3 at) => ClutterBuilder.UvTriangle.Build(
        at, at + new Vector3(400f, 0f, 0f), at + new Vector3(0f, 0f, 400f),
        new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 4f), out _)!;

    private static List<VegetationScatter.Candidate> Place(ClutterBuilder.UvTriangle tri, int seed, double perRepeat)
    {
        int deduped = 0;
        var points = new VegetationScatter.Scatter(seed).Take((int)Math.Ceiling(perRepeat));
        return VegetationScatter.OnTriangle(tri, points, perRepeat, Keys, new HashSet<(int, int, int)>(), ref deduped);
    }
}
