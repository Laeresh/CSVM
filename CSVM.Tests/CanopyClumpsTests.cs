using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The impostor-clump prototype's placement and tile layout (<see cref="CanopyClumps"/>). Clumps stand only
/// in covered canopy cells, a full cell holds its share, and a clump is sized by its cell's rise. Every draw
/// is a hash of the seed and the cell, so the layout is fixed and a local edit stays local.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CanopyClumpsTests
{
    private const float C = CanopyMask.CellM;
    private const float CardHeight = 18f;
    private const float TileHeight = 27f;

    /// <summary>The same mask and seed give the same clumps; another seed gives others.</summary>
    [Fact]
    public void PlacementIsAFunctionOfTheMaskAndTheSeed()
    {
        var mask = Forest(skip: -1);
        var a = CanopyClumps.Place(mask, CardHeight, TileHeight);
        Assert.NotEmpty(a);
        Assert.Equal(a, CanopyClumps.Place(mask, CardHeight, TileHeight));
        Assert.NotEqual(a, CanopyClumps.Place(mask, CardHeight, TileHeight, CanopyClumps.Seed + 1));
    }

    /// <summary>No clump stands over a clearing, and a full cell holds about its share of clumps.</summary>
    [Fact]
    public void ClumpsStandOnlyInCoveredCells()
    {
        var mask = Forest(skip: -1);
        var clumps = CanopyClumps.Place(mask, CardHeight, TileHeight);
        Assert.All(clumps, c => Assert.True(mask.Density[mask.CellAt(c.Base.X, c.Base.Z)] > 0f));
        Assert.All(clumps, c => Assert.True(c.Base.X < 5 * C, $"a clump at x {c.Base.X} stands over the clearing"));
        float perCell = clumps.Count / 50f;
        Assert.InRange(perCell, CanopyClumps.ClumpsPerFullCell - 0.3f, CanopyClumps.ClumpsPerFullCell + 0.3f);
    }

    /// <summary>A clump is its tile scaled by its cell's top less its ground over the card height. Its base is
    /// sunk by one share of its height below the ground, and it fades in over its own band.</summary>
    [Fact]
    public void AClumpIsSizedByItsCellsRise()
    {
        var clumps = CanopyClumps.Place(Forest(skip: -1), CardHeight, TileHeight);
        float s = 18f / CardHeight;
        float sink = (5f - clumps[0].Base.Y) / clumps[0].Height;
        Assert.InRange(sink, 0.01f, 0.5f);
        Assert.All(clumps, c =>
        {
            Assert.Equal(TileHeight * s, c.Height, 4);
            Assert.Equal(CanopyClumps.TileWidthM * s, c.Width, 4);
            Assert.Equal(sink, (5f - c.Base.Y) / c.Height, 4);
            Assert.InRange(c.FadeNear, CanopyClumps.FadeNearMin, CanopyClumps.FadeNearMax);
            Assert.Equal(c.FadeNear + CanopyClumps.FadeSpanM, c.FadeFar, 3);
            Assert.InRange(c.Variant, 0, CanopyClumps.Variants - 1);
        });
    }

    /// <summary>Clearing one cell takes its clumps and moves no other cell's.</summary>
    [Fact]
    public void AnEditMovesOnlyItsOwnCellsClumps()
    {
        var whole = CanopyClumps.Place(Forest(skip: -1), CardHeight, TileHeight);
        var mask = Forest(skip: 12);
        var cleared = CanopyClumps.Place(mask, CardHeight, TileHeight);
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
            Assert.InRange(trees.Count, 4, 6);
            Assert.Equal(trees.OrderByDescending(t => t.Base), trees);
            Assert.All(trees, t =>
            {
                float half = sizes[t.Card].X * t.Scale / 2f;
                Assert.InRange(t.X - half, -1e-3f, CanopyClumps.TileWidthM + 1e-3f - (2f * half));
                Assert.InRange(t.Shade, 0.5f, 1f);
            });
            tiles.Add(trees);
        }
        Assert.Equal(tiles.Count, tiles.Select(t => string.Join(";", t)).Distinct().Count());
    }

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
