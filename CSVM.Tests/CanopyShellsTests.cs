using System;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The canopy shells' placement over a canopy mask (<see cref="CanopyShells"/>). The shells stand between the
/// canopy's ground and its top over the covered cells and a one-cell halo. They fade in over the cards' own band.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CanopyShellsTests
{
    private const float C = CanopyMask.CellM;

    /// <summary>The lowest shell clears the ground by the base share, the highest is the canopy's top, and each
    /// stands above the last.</summary>
    [Fact]
    public void TheShellsRiseFromTheBaseShareToTheTop()
    {
        Assert.Equal(CanopyShells.BaseShare, CanopyShells.LayerShare(0), 6);
        Assert.Equal(1f, CanopyShells.LayerShare(CanopyShells.Layers - 1), 6);
        for (int l = 1; l < CanopyShells.Layers; l++)
            Assert.True(CanopyShells.LayerShare(l) > CanopyShells.LayerShare(l - 1));
    }

    /// <summary>The band is the mean of the fading stamps' near² and far². A stamp that never fades is left out,
    /// and none fading gives the never-fades band.</summary>
    [Fact]
    public void TheFadeBandIsTheFadingStampsMean()
    {
        var band = CanopyShells.FadeBand(new[]
        {
            new Color(100f, 400f, 0f, 0f),
            new Color(300f, 800f, 0f, 0f),
            new Color(0f, 0f, 0f, 0f),
        });
        Assert.Equal(200f, band.X, 4);
        Assert.Equal(600f, band.Y, 4);
        Assert.Equal(1f / 400f, band.Z, 6);
        Assert.Equal(Vector4.Zero, CanopyShells.FadeBand(new[] { new Color(0f, 0f, 0f, 0f) }));
    }

    /// <summary>One covered cell draws itself and its eight neighbours at its own ground, as tall as its canopy.
    /// The box holds the top shell. An empty mask draws nothing.</summary>
    [Fact]
    public void ACoveredCellDrawsItselfAndAHaloAtItsGround()
    {
        var mask = CanopyMask.FromStamps(new[] { (Stamp(1.5f * C, 40f, 1.5f * C), 10f, 18f) }, 0f, 0f, 3 * C, 3 * C);
        var chunk = Assert.Single(CanopyShells.Grid(mask));
        Assert.Equal(16, chunk.Corners.Length);
        Assert.Equal(9 * 6, chunk.Indices.Length);
        Assert.All(chunk.Corners, p => Assert.Equal(40f, p.Y, 4));
        Assert.All(chunk.Heights, height => Assert.Equal(18f, height, 4));
        Assert.Equal(58f, chunk.Box.End.Y, 4);

        Assert.Empty(CanopyShells.Grid(CanopyMask.FromStamps(Array.Empty<(Transform3D, float, float)>(), 0f, 0f, 3 * C, 3 * C)));
    }

    /// <summary>A corner between two covered cells takes their density-weighted ground and canopy height, so the
    /// shells follow the ground across a slope without a step.</summary>
    [Fact]
    public void ASharedCornerWeighsTheCellsByDensity()
    {
        var stamps = new[]
        {
            (Stamp(0.5f * C, 0f, 0.5f * C), 10f, 18f),
            (Stamp(1.5f * C, 10f, 0.5f * C), 5f, 8f),
        };
        var mask = CanopyMask.FromStamps(stamps, 0f, 0f, 2 * C, C);
        float d0 = mask.Density[0], d1 = mask.Density[1];
        Assert.True(d1 < d0);
        var chunk = Assert.Single(CanopyShells.Grid(mask));
        int shared = Array.FindIndex(chunk.Corners, p => p.X == C && p.Z == 0f);
        Assert.Equal(d1 * 10f / (d0 + d1), chunk.Corners[shared].Y, 4);
        Assert.Equal(((d0 * 18f) + (d1 * 8f)) / (d0 + d1), chunk.Heights[shared], 4);
    }

    /// <summary>Forest in two chunks' cells draws two chunks, and a chunk with no forest near it draws none.</summary>
    [Fact]
    public void EachChunkWithForestDrawsOnce()
    {
        float span = 3 * CanopyShells.ChunkCells * C;
        var stamps = new[]
        {
            (Stamp(10.5f * C, 0f, 10.5f * C), 10f, 18f),
            (Stamp((CanopyShells.ChunkCells + 10.5f) * C, 0f, 10.5f * C), 10f, 18f),
        };
        var chunks = CanopyShells.Grid(CanopyMask.FromStamps(stamps, 0f, 0f, span, span));
        Assert.Equal(2, chunks.Count);
        Assert.True(chunks.All(c => c.Indices.Length == 9 * 6));
    }

    private static Transform3D Stamp(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
}
