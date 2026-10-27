using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// D35's ground cover shells (<see cref="GroundCoverShells"/>): which textures grow what, and the cut of a ground
/// triangle on the mask's grid. Also the crop rows' direction field.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class GroundCoverShellsTests
{
    /// <summary>C1's crop fields grow crops and its meadows grass; nothing else and no other chapter grows any.</summary>
    [Fact]
    public void OnlyC1sFieldsAndMeadowsGrowCover()
    {
        Assert.Equal(GroundCoverShells.Cover.Crop, GroundCoverShells.CoverOf("C1", "crop04.tif"));
        Assert.Equal(GroundCoverShells.Cover.Crop, GroundCoverShells.CoverOf("c1", "CROP05.tif"));
        Assert.Equal(GroundCoverShells.Cover.Grass, GroundCoverShells.CoverOf("C1", "terpat03.tif"));
        Assert.Equal(GroundCoverShells.Cover.Grass, GroundCoverShells.CoverOf("C1", "terpat03b.tif"));
        Assert.Equal(GroundCoverShells.Cover.Grass, GroundCoverShells.CoverOf("C1", "grass1.tif"));
        Assert.Null(GroundCoverShells.CoverOf("C1", "terpat02.tif"));
        Assert.Null(GroundCoverShells.CoverOf("C1", "tarmac01.tif"));
        Assert.Null(GroundCoverShells.CoverOf("C1", "terpat03_trans1.tif"));
        Assert.Null(GroundCoverShells.CoverOf("C4", "crop04.tif"));
    }

    /// <summary>The layers stand evenly to the full height, the lowest off the ground.</summary>
    [Fact]
    public void TheLayersRiseEvenlyToTheFullHeight()
    {
        Assert.Equal(1f, GroundCoverShells.LayerShare(GroundCoverShells.Layers - 1));
        Assert.True(GroundCoverShells.LayerShare(0) > 0f);
        Assert.Equal(GroundCoverShells.LayerShare(1) - GroundCoverShells.LayerShare(0),
            GroundCoverShells.LayerShare(GroundCoverShells.Layers - 1) - GroundCoverShells.LayerShare(GroundCoverShells.Layers - 2), 5);
    }

    /// <summary>With every cell kept the pieces tile the triangle: their areas sum to its own, and each lies
    /// inside its cell.</summary>
    [Fact]
    public void KeptPiecesTileTheTriangleInsideTheirCells()
    {
        Vector2 a = new(-5f, -3f), b = new(37f, 4f), c = new(10f, 29f);
        var pieces = GroundCoverShells.Clip(a, b, c, Vector2.Zero, 12f, (_, _) => true);
        Assert.True(pieces.Count > 4);
        Assert.Equal(Area(a, b, c), pieces.Sum(p => Area(Points(p, a, b, c))), 2);
        foreach (var p in pieces)
        {
            foreach (var at in Points(p, a, b, c))
            {
                Assert.InRange(at.X, (p.I * 12f) - 1e-3f, ((p.I + 1) * 12f) + 1e-3f);
                Assert.InRange(at.Y, (p.J * 12f) - 1e-3f, ((p.J + 1) * 12f) + 1e-3f);
            }
            foreach (var w in p.Weights)
                Assert.Equal(1f, w.X + w.Y + w.Z, 4);
        }
    }

    /// <summary>A cell the keep test refuses (a cleared mask texel) takes no piece, and the rest lose exactly its
    /// share of the triangle.</summary>
    [Fact]
    public void ARefusedCellTakesNothing()
    {
        Vector2 a = new(0f, 0f), b = new(24f, 0f), c = new(0f, 24f);
        var all = GroundCoverShells.Clip(a, b, c, Vector2.Zero, 12f, (_, _) => true);
        var cut = GroundCoverShells.Clip(a, b, c, Vector2.Zero, 12f, (i, j) => !(i == 0 && j == 0));
        Assert.DoesNotContain(cut, p => p.I == 0 && p.J == 0);
        Assert.Equal(all.Sum(p => Area(Points(p, a, b, c))) - 144f, cut.Sum(p => Area(Points(p, a, b, c))), 2);
        Assert.Empty(GroundCoverShells.Clip(a, b, c, Vector2.Zero, 12f, (_, _) => false));
    }

    /// <summary>Stripes along v read as a gradient along u (field angle 0), and stripes along u the opposite.
    /// Diagonal stripes read a quarter turn, and a flat texture nothing.</summary>
    [Fact]
    public void TheRowFieldReadsTheStripesDirection()
    {
        static byte[] Image(Func<int, int, byte> lum)
        {
            var px = new byte[64 * 64 * 4];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    byte l = lum(x, y);
                    px[(((y * 64) + x) * 4) + 0] = l;
                    px[(((y * 64) + x) * 4) + 1] = l;
                    px[(((y * 64) + x) * 4) + 2] = l;
                    px[(((y * 64) + x) * 4) + 3] = 255;
                }
            }
            return px;
        }
        static byte Stripe(int s) => (byte)(s % 4 < 2 ? 40 : 200);
        var alongV = GroundCoverShells.RowField(Image((x, _) => Stripe(x)), 64, 64, 8);
        var alongU = GroundCoverShells.RowField(Image((_, y) => Stripe(y)), 64, 64, 8);
        var diagonal = GroundCoverShells.RowField(Image((x, y) => Stripe(x + y)), 64, 64, 8);
        var flat = GroundCoverShells.RowField(Image((_, _) => 120), 64, 64, 8);
        Assert.All(alongV, v => Assert.True(v.X > 0.95f && Math.Abs(v.Y) < 0.05f, $"{v}"));
        Assert.All(alongU, v => Assert.True(v.X < -0.95f && Math.Abs(v.Y) < 0.05f, $"{v}"));
        Assert.All(diagonal, v => Assert.True(Math.Abs(v.X) < 0.05f && v.Y > 0.95f, $"{v}"));
        Assert.All(flat, v => Assert.Equal(Vector2.Zero, v));
    }

    private static List<Vector2> Points(GroundCoverShells.Piece p, Vector2 a, Vector2 b, Vector2 c) =>
        p.Weights.Select(w => (a * w.X) + (b * w.Y) + (c * w.Z)).ToList();

    private static float Area(Vector2 a, Vector2 b, Vector2 c) => Math.Abs((b - a).Cross(c - a)) / 2f;

    private static float Area(List<Vector2> poly)
    {
        float sum = 0f;
        for (int k = 0; k < poly.Count; k++)
            sum += poly[k].Cross(poly[(k + 1) % poly.Count]);
        return Math.Abs(sum) / 2f;
    }
}
