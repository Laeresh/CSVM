using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ground cover shells (<see cref="GroundCoverShells"/>): the layers, and the cut of a ground
/// triangle on the mask's grid. Also the crop rows' direction field.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class GroundCoverShellsTests
{

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

    /// <summary>A rail laid over the ground cuts its strip, grown by the half-metre reach, out of a piece, and the rest
    /// keeps its area. A rail 10 m up, or a wall with no ground shadow, cuts nothing.</summary>
    [Fact]
    public void AnOverlayCutsItsGrownStripOutOfAPiece()
    {
        Vector3 a = new(-100f, 0f, -100f), b = new(200f, 0f, -100f), c = new(-100f, 0f, 200f);
        var piece = Assert.Single(GroundCoverShells.Clip(new(a.X, a.Z), new(b.X, b.Z), new(c.X, c.Z), Vector2.Zero, 12f, (i, j) => i == 0 && j == 0));
        static Vector3[] Rail(float y) => new Vector3[]
        {
            new(5f, y, -20f), new(7f, y, -20f), new(7f, y, 32f),
            new(5f, y, -20f), new(7f, y, 32f), new(5f, y, 32f),
        };
        List<Vector2> Flat(Vector3[] w) => w.Select(v => { var p = (a * v.X) + (b * v.Y) + (c * v.Z); return new Vector2(p.X, p.Z); }).ToList();

        var parts = GroundCoverShells.KeepOff(piece.Weights, a, b, c, Rail(0.05f));
        Assert.Equal(144f - (3f * 12f), parts.Sum(p => Area(Flat(p))), 2);
        Assert.All(parts.SelectMany(Flat), p => Assert.True(p.X <= 4.5f + 1e-3f || p.X >= 7.5f - 1e-3f, $"{p}"));

        Assert.Equal(144f, GroundCoverShells.KeepOff(piece.Weights, a, b, c, Rail(10f)).Sum(p => Area(Flat(p))), 2);
        var wall = new Vector3[] { new(6f, 0f, 0f), new(6f, 5f, 0f), new(6f, 0f, 12f) };
        Assert.Equal(144f, Area(Flat(Assert.Single(GroundCoverShells.KeepOff(piece.Weights, a, b, c, wall)))), 2);
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
