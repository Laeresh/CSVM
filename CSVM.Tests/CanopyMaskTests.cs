using System;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The canopy mask (<see cref="CanopyMask"/>): where forest mass is drawn, baked from Rich card stamps or
/// from ground painted as canopy. A stamp the brush dropped shades nothing, and a painted cell is as dense
/// as the share of it the canopy texture covers.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CanopyMaskTests
{
    private const float C = CanopyMask.CellM;

    /// <summary>The grid covers the area from its lowest corner, whichever way the corners come.
    /// A point off it has no cell and no density.</summary>
    [Fact]
    public void TheGridCoversTheAreaFromItsLowestCorner()
    {
        var mask = CanopyMask.FromStamps(Array.Empty<(Transform3D, float, float)>(), 0f, 0f, -12288f, -12288f);
        Assert.Equal(384, mask.Width);
        Assert.Equal(384, mask.Height);
        Assert.Equal(-12288f, mask.MinX);
        Assert.Equal(0, mask.CellAt(-12288f, -12288f));
        Assert.Equal((383 * 384) + 383, mask.CellAt(-1f, -1f));
        Assert.Equal(-1, mask.CellAt(1f, -10f));
        Assert.Equal(0f, mask.DensityAt(1f, -10f));
    }

    /// <summary>A crown shades its cell as a share of a full forest cell, held at 1, and ground and top are
    /// crown-weighted means. A stamp with a zero basis (dropped by the brush mask) or off the area counts nothing.</summary>
    [Fact]
    public void AStampShadesItsCellByItsCrown()
    {
        var stamps = new[]
        {
            (Stamp(16f, 5f, 16f), 10f, 18f),
            (Stamp(16f, 7f, 16f), 10f, 18f),
            (Stamp(48f, 0f, 16f), 4f, 3f),
            (new Transform3D(Basis.Identity.Scaled(Vector3.Zero), new Vector3(80f, 0f, 16f)), 10f, 18f),
            (Stamp(-5f, 0f, 16f), 10f, 18f),
        };
        var mask = CanopyMask.FromStamps(stamps, 0f, 0f, 3 * C, C);
        Assert.Equal(1f, mask.Density[0]);
        Assert.Equal(6f, mask.Ground[0], 4);
        Assert.Equal(24f, mask.Top[0], 4);
        Assert.Equal(MathF.PI * 4f / (CanopyMask.FullCrownShare * C * C), mask.Density[1], 5);
        Assert.Equal(3f, mask.Top[1], 4);
        Assert.Equal(0f, mask.Density[2]);
        Assert.Equal(0f, mask.Top[2]);
    }

    /// <summary>A fir on the line between two cells shades each by half. C1's spacing of one fir per cell is
    /// full forest whatever the lattice's phase to the grid.</summary>
    [Fact]
    public void AStampSplitsBetweenTheCellsAroundIt()
    {
        float fir = MathF.PI * 100f / 4f;
        var edge = CanopyMask.FromStamps(new[] { (Stamp(C, 0f, 0.5f * C), 10f, 18f) }, 0f, 0f, 3 * C, C);
        Assert.Equal(edge.Density[0], edge.Density[1]);
        Assert.Equal(0.5f * fir / (CanopyMask.FullCrownShare * C * C), edge.Density[0], 5);

        var lattice = Enumerable.Range(0, 64).Select(n => (Stamp(((n % 8) * C) + 11f, 0f, ((n / 8) * C) + 23f), 10f, 18f));
        var forest = CanopyMask.FromStamps(lattice, 0f, 0f, 8 * C, 8 * C);
        Assert.Equal(1f, forest.Density[(4 * 8) + 4]);
    }

    /// <summary>A painted cell is as dense as the share of its sample points under a canopy triangle.
    /// The higher of two layers sets its height, and a painted cell's top is its ground.</summary>
    [Fact]
    public void APaintedCellIsAsDenseAsTheCanopyCoversIt()
    {
        var tris = Quad(0f, 0f, C, C, 2f)
            .Concat(Quad(0f, 0f, C, C, 2.05f))
            .Concat(Quad(C, 0f, 1.5f * C, C, 10f))
            .ToArray();
        var mask = CanopyMask.FromPainted(tris, 0f, 0f, 3 * C, C);
        Assert.Equal(1f, mask.Density[0]);
        Assert.Equal(2.05f, mask.Ground[0], 4);
        Assert.Equal(mask.Ground[0], mask.Top[0]);
        Assert.Equal(0.5f, mask.Density[1]);
        Assert.Equal(10f, mask.Ground[1], 4);
        Assert.Equal(0f, mask.Density[2]);
        Assert.Equal(0.75f, mask.DensityAt(C, 0.5f * C), 4);
    }

    /// <summary>A painted bake keeps its surface. A point on it reads the slope's own height between the samples,
    /// and whether its nearest sample is painted. A stamp bake has no surface, so it reads the cell's ground.</summary>
    [Fact]
    public void APaintedBakeReadsItsSurfaceBetweenItsSamples()
    {
        // A slope rising half a metre a metre along X over the first cell and a half; the rest bare.
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(1.5f * C, 0.75f * C, 0f);
        var c = new Vector3(1.5f * C, 0.75f * C, C);
        var d = new Vector3(0f, 0f, C);
        var mask = CanopyMask.FromPainted(new[] { a, b, c, a, c, d }, 0f, 0f, 3 * C, C);
        foreach (float x in new[] { 5f, 13.3f, 30f, 40f })
        {
            Assert.True(mask.IsPainted(x, 17f));
            Assert.Equal(x / 2f, mask.SurfaceAt(x, 17f), 3);
        }
        Assert.False(mask.IsPainted(2.5f * C, 17f));
        Assert.Equal(1f, mask.PaintedShare(0));
        Assert.Equal(0.5f, mask.PaintedShare(1));
        Assert.Equal(0f, mask.PaintedShare(2));

        var stamps = CanopyMask.FromStamps(new[] { (Stamp(16f, 5f, 16f), 10f, 18f) }, 0f, 0f, C, C);
        Assert.False(stamps.IsPainted(16f, 16f));
        Assert.Equal(0f, stamps.PaintedShare(0));
        Assert.Equal(stamps.Ground[0], stamps.SurfaceAt(16f, 16f));
    }

    /// <summary>The brush mask thins painted canopy and clears it, but a thickened stroke never makes a cell
    /// denser than its cover.</summary>
    [Fact]
    public void TheBrushThinsPaintedCanopyAndNeverThickensIt()
    {
        var tris = Quad(0f, 0f, 3 * C, C, 0f);
        var brush = VegetationMask.Neutral(0f, 0f, 3 * C, C, side: 3);
        brush.Stroke(0.5f * C, 0.5f * C, 1f, 1f, VegetationBrush.Clear);
        brush.Stroke(2.5f * C, 0.5f * C, 1f, 1f, VegetationBrush.Thicken);
        var mask = CanopyMask.FromPainted(tris, 0f, 0f, 3 * C, C, brush);
        Assert.Equal(0f, mask.Density[0]);
        Assert.Equal(1f, mask.Density[1]);
        Assert.Equal(1f, mask.Density[2]);
    }

    /// <summary>C3's painted jungle is picked as canopy in the shipped file and its palm transitions are not.
    /// The canopy set stays empty, so no card is stamped on the painted treetops.</summary>
    [Fact]
    public void TheShippedFilePicksC3sPaintedJungleAsCanopy()
    {
        var sidecar = VegetationSidecar.Parse(File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "data", "vegetation.json")));
        var c3 = sidecar.For("C3");
        Assert.All(new[] { "forest1.tif", "forest1-128.tif", "cliff1.tif" }, t => Assert.True(CanopyMask.IsCanopy(c3, t), t));
        Assert.All(new[] { "cliff1_sandtrans.tif", "cliff1_toptrans.tif" }, t => Assert.False(CanopyMask.IsCanopy(c3, t), t));
        Assert.Empty(c3.SetFor("forest1.tif")!.Models);
        Assert.All(new[] { "C1", "C1B", "C2", "C4" }, c => Assert.DoesNotContain(sidecar.For(c).Picks, p => CanopyMask.IsCanopy(sidecar.For(c), p.Texture)));
    }

    private static Transform3D Stamp(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));

    // Two triangles over the XZ rectangle at height y.
    private static Vector3[] Quad(float x0, float z0, float x1, float z1, float y) => new[]
    {
        new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1),
        new Vector3(x0, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1),
    };
}
