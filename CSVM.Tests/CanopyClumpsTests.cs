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

    /// <summary>C1 draws clumps by default; the other chapters do so only when asked.</summary>
    [Fact]
    public void OnlyC1DrawsClumpsByDefault()
    {
        Assert.True(CanopyClumps.DefaultOn("C1"));
        Assert.True(CanopyClumps.DefaultOn("c1"));
        Assert.All(new[] { "C1B", "C2", "C3", "C4", "C5", null }, ch => Assert.False(CanopyClumps.DefaultOn(ch)));
    }

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
