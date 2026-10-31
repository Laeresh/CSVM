using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The canopy clumps' placement and tile layout (<see cref="CanopyClumps"/>). Clumps stand only in covered
/// canopy cells, carry the cards' trees per area, and are sized by their cell's rise. Every draw is a hash of
/// the seed and the cell, so the layout is fixed and a local edit stays local.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CanopyClumpsTests
{
    private const float C = CanopyMask.CellM;
    private const float CardHeight = 18f;
    private const float TileHeight = 27f;
    private const float PerDensity = 1.5f;

    /// <summary>The same mask and seed give the same clumps; another seed gives others.</summary>
    [Fact]
    public void PlacementIsAFunctionOfTheMaskAndTheSeed()
    {
        var mask = Forest(skip: -1);
        var a = CanopyClumps.Place(mask, Even(mask), CardHeight, TileHeight);
        Assert.NotEmpty(a);
        Assert.Equal(a, CanopyClumps.Place(mask, Even(mask), CardHeight, TileHeight));
        Assert.NotEqual(a, CanopyClumps.Place(mask, Even(mask), CardHeight, TileHeight, seed: CanopyClumps.Seed + 1));
    }

    /// <summary>No clump stands over a clearing, and a full cell holds about its share of clumps.</summary>
    [Fact]
    public void ClumpsStandOnlyInCoveredCells()
    {
        var mask = Forest(skip: -1);
        var clumps = CanopyClumps.Place(mask, Even(mask), CardHeight, TileHeight);
        Assert.All(clumps, c => Assert.True(mask.Density[mask.CellAt(c.Base.X, c.Base.Z)] > 0f));
        Assert.All(clumps, c => Assert.True(c.Base.X < 5 * C, $"a clump at x {c.Base.X} stands over the clearing"));
        Assert.InRange(clumps.Count / 50f, PerDensity - 0.3f, PerDensity + 0.3f);
    }

    /// <summary>Build's rate is the stamps over the density sum over a tile's trees. At it, the clumps carry
    /// as many trees as the cards they take over from.</summary>
    [Fact]
    public void ClumpsCarryTheCardsTreesPerArea()
    {
        var stamps = new List<(Transform3D, float, float)>();
        for (int j = 0; j < 40; j++)
        {
            for (int i = 0; i < 40; i++)
                stamps.Add((new Transform3D(Basis.Identity, new Vector3((i + 0.5f) * C, 5f, (j + 0.5f) * C)), 10f, 18f));
        }
        var mask = CanopyMask.FromStamps(stamps, 0f, 0f, 40 * C, 40 * C);
        var weights = new[] { 1f };
        var sizes = new[] { new Vector2(10f, 18f) };
        float trees = CanopyClumps.MeanTrees(weights, sizes);
        var rate = CanopyClumps.Rate(mask, stamps.Select(s => s.Item1.Origin), trees);
        var clumps = CanopyClumps.Place(mask, rate, CardHeight, TileHeight);
        Assert.InRange(clumps.Count * trees, stamps.Count * 0.9f, stamps.Count * 1.1f);
        Assert.All(clumps, c => Assert.True(rate[mask.CellAt(c.Base.X, c.Base.Z)] > 0f));
    }

    /// <summary>A clump is its tile scaled by its cell's top less its ground over the card height. Its base is
    /// sunk below the ground, and its colour spreads as the Rich cards' does.</summary>
    [Fact]
    public void AClumpIsSizedByItsCellsRise()
    {
        var clumps = CanopyClumps.Place(Forest(skip: -1), Even(Forest(skip: -1)), CardHeight, TileHeight, brightness: 0.2f, tint: 0.1f);
        float s = 18f / CardHeight;
        float sink = (5f - clumps[0].Base.Y) / clumps[0].Height;
        Assert.InRange(sink, 0.01f, 0.5f);
        Assert.All(clumps, c =>
        {
            Assert.Equal(TileHeight * s, c.Height, 4);
            Assert.Equal(CanopyClumps.TileWidthM * s, c.Width, 4);
            Assert.Equal(sink, (5f - c.Base.Y) / c.Height, 4);
            Assert.InRange(c.Variant, 0, CanopyClumps.Variants - 1);
            Assert.All(new[] { c.Colour.R, c.Colour.G, c.Colour.B }, v => Assert.InRange(v, 0.8f * 0.9f, 1.2f * 1.1f));
        });
        Assert.InRange(clumps.Average(c => (c.Colour.R + c.Colour.G + c.Colour.B) / 3f), 0.95f, 1.05f);
        Assert.All(CanopyClumps.Place(Forest(skip: -1), Even(Forest(skip: -1)), CardHeight, TileHeight), c => Assert.Equal(Colors.White, c.Colour));
    }

    /// <summary>Clearing one cell takes its clumps and moves no other cell's.</summary>
    [Fact]
    public void AnEditMovesOnlyItsOwnCellsClumps()
    {
        var whole = CanopyClumps.Place(Forest(skip: -1), Even(Forest(skip: -1)), CardHeight, TileHeight);
        var mask = Forest(skip: 12);
        var cleared = CanopyClumps.Place(mask, Even(mask), CardHeight, TileHeight);
        // The ground interpolates over the covered neighbours, so the cleared cell's own weight leaves a float ulp.
        var kept = whole.Where(c => mask.CellAt(c.Base.X, c.Base.Z) != 12).ToList();
        Assert.Equal(kept.Select(c => c with { Base = new Vector3(c.Base.X, 0f, c.Base.Z) }),
            cleared.Select(c => c with { Base = new Vector3(c.Base.X, 0f, c.Base.Z) }));
        Assert.All(kept.Zip(cleared), p => Assert.Equal(p.First.Base.Y, p.Second.Base.Y, 3));
        Assert.Contains(whole, c => mask.CellAt(c.Base.X, c.Base.Z) == 12);
    }

    /// <summary>A tile's trees are drawn back row first and fit inside the tile. A tile is the same on every
    /// call, and the tiles differ from each other.</summary>
    [Fact]
    public void ATileIsLaidOutBackFirstInsideItsWidth()
    {
        var weights = new[] { 9f, 1f, 3f };
        var sizes = new[] { new Vector2(10f, 17.8f), new Vector2(10f, 17.8f), new Vector2(2.4f, 2.1f) };
        var tiles = new List<List<CanopyClumps.Tree>>();
        for (int v = 0; v < CanopyClumps.Variants; v++)
        {
            var trees = CanopyClumps.Layout(v, weights, sizes);
            Assert.Equal(trees, CanopyClumps.Layout(v, weights, sizes));
            Assert.InRange(trees.Count, 2, 4);
            Assert.Equal(trees.OrderByDescending(t => t.Base), trees);
            Assert.All(trees, t =>
            {
                float half = sizes[t.Card].X * t.Scale / 2f;
                Assert.InRange(t.X - half, -1e-3f, CanopyClumps.TileWidthM + 1e-3f - (2f * half));
            });
            tiles.Add(trees);
        }
        Assert.Equal(tiles.Count, tiles.Select(t => string.Join(";", t)).Distinct().Count());
    }

    /// <summary>With no tile every slot composes, as before tiles existed. A tiled species takes its stamp share of
    /// the slots after the composed ones, and one too rare for a slot stays composed.</summary>
    [Fact]
    public void ATiledSpeciesTakesItsShareOfTheSlots()
    {
        var weights = new[] { 6f, 3f, 2f, 1f };
        Assert.Equal(Enumerable.Repeat(-1, CanopyClumps.Variants), CanopyClumps.Slots(weights, new[] { false, false, false, false }));
        Assert.Equal(new[] { -1, -1, -1, 0, 0, 0 }, CanopyClumps.Slots(weights, new[] { true, false, false, false }));
        Assert.Equal(new[] { -1, -1, 0, 0, 0, 1 }, CanopyClumps.Slots(weights, new[] { true, true, false, false }));
        Assert.Equal(new[] { 0, 0, 0, 1, 1, 2 }, CanopyClumps.Slots(weights, new[] { true, true, true, true }));
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, CanopyClumps.Slots(new[] { 1f }, new[] { true }));
        Assert.DoesNotContain(1, CanopyClumps.Slots(new[] { 20f, 1f }, new[] { false, true }));
    }

    /// <summary>A tiled slot carries its own tile's tree count, cycling through the species' tiles. A tile of
    /// other counts then moves the rate and not the trees per area.</summary>
    [Fact]
    public void ARateTakesATilesOwnTreeCount()
    {
        var weights = new[] { 1f };
        var sizes = new[] { new Vector2(10f, 18f) };
        var all = new[] { 0, 0, 0, 0, 0, 0 };
        Assert.Equal(4f, CanopyClumps.MeanTrees(all, new IReadOnlyList<int>?[] { new[] { 4 } }, weights, sizes));
        Assert.Equal(3f, CanopyClumps.MeanTrees(all, new IReadOnlyList<int>?[] { new[] { 2, 4 } }, weights, sizes));
        Assert.Equal(CanopyClumps.MeanTrees(weights, sizes),
            CanopyClumps.MeanTrees(Enumerable.Repeat(-1, 6).ToArray(), new IReadOnlyList<int>?[] { null }, weights, sizes));
        var half = new[] { -1, -1, -1, 0, 0, 0 };
        float mixed = CanopyClumps.MeanTrees(half, new IReadOnlyList<int>?[] { new[] { 2 } }, weights, sizes);
        Assert.Equal((Enumerable.Range(0, 3).Sum(v => CanopyClumps.Layout(v, weights, sizes).Count) + 6f) / 6f, mixed, 5);

        var stamps = new List<(Transform3D, float, float)>();
        for (int j = 0; j < 40; j++)
        {
            for (int i = 0; i < 40; i++)
                stamps.Add((new Transform3D(Basis.Identity, new Vector3((i + 0.5f) * C, 5f, (j + 0.5f) * C)), 10f, 18f));
        }
        var mask = CanopyMask.FromStamps(stamps, 0f, 0f, 40 * C, 40 * C);
        foreach (int count in new[] { 2, 4 })
        {
            float trees = CanopyClumps.MeanTrees(all, new IReadOnlyList<int>?[] { new[] { count } }, weights, sizes);
            var clumps = CanopyClumps.Place(mask, CanopyClumps.Rate(mask, stamps.Select(s => s.Item1.Origin), trees), CardHeight, TileHeight);
            Assert.InRange(clumps.Count * count, stamps.Count * 0.9f, stamps.Count * 1.1f);
        }
    }

    /// <summary>C1 and C3 draw clumps by default; the other chapters do so only when asked.</summary>
    [Fact]
    public void OnlyC1AndC3DrawClumpsByDefault()
    {
        Assert.True(CanopyClumps.DefaultOn("C1"));
        Assert.True(CanopyClumps.DefaultOn("c1"));
        Assert.True(CanopyClumps.DefaultOn("C3"));
        Assert.All(new[] { "C1B", "C2", "C4", "C5", null }, ch => Assert.False(CanopyClumps.DefaultOn(ch)));
    }

    /// <summary>Over painted canopy the rate is the covered share times the crowns a full cell carries. The
    /// clumps then carry that many trees per covered area, and none stands outside the painted cells.</summary>
    [Fact]
    public void PaintedCanopyIsRatedFromItsCoveredShare()
    {
        var mask = Painted();
        const float trees = 3f;
        var rate = CanopyClumps.Rate(mask, CanopyClumps.CrownsPerCell, trees);
        Assert.All(Enumerable.Range(0, rate.Length), c => Assert.Equal(mask.Density[c] * CanopyClumps.CrownsPerCell / trees, rate[c], 5));
        var clumps = CanopyClumps.Place(mask, rate, CanopyClumps.PaintedRiseM, TileHeight, rise: CanopyClumps.PaintedRiseM);
        float expected = mask.Density.Sum() * CanopyClumps.CrownsPerCell;
        Assert.InRange(clumps.Count * trees, expected * 0.9f, expected * 1.1f);
        Assert.All(clumps, c => Assert.True(mask.Density[mask.CellAt(c.Base.X, c.Base.Z)] > 0f, $"a clump at x {c.Base.X} stands off the painted canopy"));
        Assert.Contains(clumps, c => mask.Density[mask.CellAt(c.Base.X, c.Base.Z)] < 1f);
    }

    /// <summary>Painted canopy's top is its ground, so without a rise it stands no clump. With one, every clump
    /// takes that rise over the card height, at its cell's painted height, the same on every call.</summary>
    [Fact]
    public void PaintedClumpsTakeTheGivenRise()
    {
        var mask = Painted();
        var rate = CanopyClumps.Rate(mask, CanopyClumps.CrownsPerCell, 3f);
        Assert.Empty(CanopyClumps.Place(mask, rate, CardHeight, TileHeight));
        var clumps = CanopyClumps.Place(mask, rate, CardHeight, TileHeight, rise: CanopyClumps.PaintedRiseM);
        Assert.NotEmpty(clumps);
        Assert.Equal(clumps, CanopyClumps.Place(mask, rate, CardHeight, TileHeight, rise: CanopyClumps.PaintedRiseM));
        Assert.All(clumps, c =>
        {
            Assert.Equal(TileHeight * CanopyClumps.PaintedRiseM / CardHeight, c.Height, 4);
            Assert.InRange(c.Base.Y, 7f - 1f, 7f);
        });
    }

    /// <summary>Over painted canopy no card hands over. The clumps take a band of their own that starts past
    /// the near view, so none stands up close.</summary>
    [Fact]
    public void PaintedClumpsFadeInPastTheNearView()
    {
        var band = CanopyClumps.Band(painted: true);
        Assert.Equal(new Vector2(CanopyClumps.PaintedNearM, CanopyClumps.PaintedFarM), band);
        Assert.InRange(band.X, 500f, band.Y - 1f);
        Assert.Equal(new Vector2(CanopyClumps.HandoverNearM, CanopyClumps.HandoverFarM), CanopyClumps.Band(painted: false));
    }

    /// <summary>The near crown models stand on painted points alone, at the painted surface. They carry about the
    /// crowns a full cell does per covered area, the half-covered column too. Fillers stand under emergents at
    /// about the emergent share, and each crown is one of the shapes asked for. The tint stays between
    /// yellow-green and blue-green. The seed alone moves them.</summary>
    [Fact]
    public void CrownModelsStandOnThePaintedCanopyAtItsCrownRate()
    {
        var mask = Painted();
        var crowns = CanopyClumps.CrownModels(mask, CanopyClumps.CrownsPerCell, CanopyClumps.CrownBrightness, CanopyClumps.CrownTint, shapes: 3);
        Assert.Equal(crowns, CanopyClumps.CrownModels(mask, CanopyClumps.CrownsPerCell, CanopyClumps.CrownBrightness, CanopyClumps.CrownTint, shapes: 3));
        Assert.NotEqual(crowns, CanopyClumps.CrownModels(mask, CanopyClumps.CrownsPerCell, CanopyClumps.CrownBrightness, CanopyClumps.CrownTint, CanopyClumps.Seed + 1, 3));
        Assert.All(crowns, c =>
        {
            Assert.True(mask.IsPainted(c.At.X, c.At.Z), $"a crown at x {c.At.X} stands off the painted canopy");
            Assert.Equal(7f, c.At.Y, 4);
            Assert.True(c.Scale is (>= 0.45f and <= 0.8f) or (>= 1f and <= 1.5f), $"a crown at scale {c.Scale} is neither a filler nor an emergent");
            Assert.InRange(c.Shape, 0, 2);
            // Red and blue move against each other about the brightness, so a crown is never brown or grey.
            float b = (c.Colour.R + c.Colour.B) / 2f;
            Assert.InRange(c.Colour.G / b, 1f - (0.3f * CanopyClumps.CrownTint), 1f + (0.3f * CanopyClumps.CrownTint));
        });
        float full = 40 * CanopyClumps.CrownsPerCell, half = 10 * 0.5f * CanopyClumps.CrownsPerCell;
        Assert.InRange(crowns.Count(c => c.At.X < 4 * C), full * 0.85f, full * 1.15f);
        Assert.InRange(crowns.Count(c => c.At.X >= 4 * C), half * 0.6f, half * 1.4f);
        Assert.InRange((float)crowns.Count(c => c.Scale >= 1f) / crowns.Count, CanopyClumps.EmergentShare * 0.8f, CanopyClumps.EmergentShare * 1.2f);
        Assert.Equal(new[] { 0, 1, 2 }, crowns.Select(c => c.Shape).Distinct().OrderBy(s => s));
        Assert.All(CanopyClumps.CrownModels(mask, CanopyClumps.CrownsPerCell), c => Assert.Equal(0, c.Shape));
        Assert.Contains(crowns, c => c.Mirror);
        Assert.Contains(crowns, c => !c.Mirror);
    }

    /// <summary>A painted cliff's edge falls between two sample points, as at the mouths of C3's Tiki Tunnel (#264).
    /// Each crown stands on the side of the edge under it, never on a height between the two.</summary>
    [Fact]
    public void CrownModelsStandOnTheGroundUnderThemAtACliff()
    {
        // Samples sit 8 m apart from x = 4, so the edge at 45 m lies between the ones at 44 and 52.
        const float edge = C + 13f;
        var mask = CanopyMask.FromPainted(Strip(0f, edge, 50f).Concat(Strip(edge, 4 * C, 0f)).ToArray(), 0f, 0f, 4 * C, 4 * C);
        var crowns = CanopyClumps.CrownModels(mask, CanopyClumps.CrownsPerCell);
        Assert.Contains(crowns, c => c.At.X > edge && c.At.X < edge + 7f);
        Assert.All(crowns, c => Assert.Equal(c.At.X < edge ? 50f : 0f, c.At.Y, 3));
    }

    /// <summary>The crown models alone fill the near view past the clumps' own near edge. They hand over to the
    /// clumps over a thin band inside the clumps' band. A model's top stands inside the clumps' rise.</summary>
    [Fact]
    public void CrownModelsHandOverInsideTheClumpsRise()
    {
        var band = CanopyClumps.ModelBand;
        Assert.Equal(new Vector2(CanopyClumps.ModelNearM, CanopyClumps.ModelFarM), band);
        Assert.InRange(band.X, CanopyClumps.PaintedNearM, band.Y - 1f);
        Assert.InRange(band.Y - band.X, 1f, 100f);
        Assert.InRange(band.Y, band.X + 1f, CanopyClumps.PaintedFarM - 1f);
        Assert.InRange(CanopyClumps.ModelRiseM, 1f, CanopyClumps.PaintedRiseM);
    }

    // A 10 x 10 cell area at 7 m. Its left four columns are painted canopy, the fifth half covered, the rest bare.
    private static CanopyMask Painted()
    {
        var tris = new List<Vector3>();
        foreach (var (x0, x1) in new[] { (0f, 4 * C), (4 * C, 4.5f * C) })
        {
            tris.AddRange(new[]
            {
                new Vector3(x0, 7f, 0f), new Vector3(x1, 7f, 0f), new Vector3(x1, 7f, 10 * C),
                new Vector3(x0, 7f, 0f), new Vector3(x1, 7f, 10 * C), new Vector3(x0, 7f, 10 * C),
            });
        }
        return CanopyMask.FromPainted(tris, 0f, 0f, 10 * C, 10 * C);
    }

    // Two triangles over x0 to x1 and the first four cells of Z, at height y.
    private static Vector3[] Strip(float x0, float x1, float y) => new[]
    {
        new Vector3(x0, y, 0f), new Vector3(x1, y, 0f), new Vector3(x1, y, 4 * C),
        new Vector3(x0, y, 0f), new Vector3(x1, y, 4 * C), new Vector3(x0, y, 4 * C),
    };

    // PerDensity clumps a cell for every unit of the mask's density.
    private static float[] Even(CanopyMask mask) => mask.Density.Select(d => d * PerDensity).ToArray();

    // A 10 x 10 cell area. Its left five columns hold one fir at each cell centre, 5 m up and 18 m tall.
    // The right five are a clearing, and the cell numbered skip holds none.
    private static CanopyMask Forest(int skip)
    {
        var stamps = new List<(Transform3D, float, float)>();
        for (int j = 0; j < 10; j++)
        {
            for (int i = 0; i < 5; i++)
            {
                if ((j * 10) + i != skip)
                    stamps.Add((new Transform3D(Basis.Identity, new Vector3((i + 0.5f) * C, 5f, (j + 0.5f) * C)), 10f, 18f));
            }
        }
        return CanopyMask.FromStamps(stamps, 0f, 0f, 10 * C, 10 * C);
    }
}
