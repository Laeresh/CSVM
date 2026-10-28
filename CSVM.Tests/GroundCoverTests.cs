using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;
using Cover = CSVM.Mech3.GroundCover.Cover;

namespace CSVM.Tests;

/// <summary>
/// The ground cover cards' placement (<see cref="GroundCover"/>). A pick's saturation gate keeps grey texels bare, a bare
/// mask texel grows nothing and a hedge line grows grass. A card stands on the triangle's texture lattice inside its
/// cell, coloured by the ground under it. A crop row runs along the rows its patch paints. The art is the same on every run.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class GroundCoverTests
{
    /// <summary>A pick's saturation gate: grey texels grow nothing, coloured ones grow, and a pick with no gate grows on
    /// grey ground too.</summary>
    [Fact]
    public void ASaturationGateKeepsTheCoverOffGreyTexels()
    {
        // The left half of the texture is grey rock, the right half dry grass; U runs along world X, 200 m a repeat.
        var rgba = Flat(8, 128);
        for (int i = 0; i < 64; i++)
        {
            if (i % 8 >= 4)
                (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2]) = (108, 98, 76);
        }
        Assert.Equal(0f, GroundCover.Surface.Saturation(new Vector3(128f, 128f, 128f)));
        Assert.Equal(32f / 108f, GroundCover.Surface.Saturation(new Vector3(108f, 98f, 76f)), 4);
        var t = new CoverTriangle("x.tif", Vector3.Zero, new Vector3(400f, 0f, 0f), new Vector3(0f, 0f, 400f),
            Vector2.Zero, new Vector2(2f, 0f), new Vector2(0f, 2f), Colors.White, Colors.White, Colors.White);
        // The cell at X 0 to 64 m lies on the grey half, the one at 128 to 192 m on the grass half.
        var gated = GroundCover.Ground.Prepare(t, Cover.Grass, new GroundCover.Surface(rgba, 8, 8, 0.2f))!;
        Assert.Empty(Place(gated, 0f, 0f, out int bare)[(int)Cover.Grass]);
        Assert.Equal(0, bare);
        Assert.NotEmpty(Place(gated, 128f, 0f, out _)[(int)Cover.Grass]);
        var open = GroundCover.Ground.Prepare(t, Cover.Grass, new GroundCover.Surface(rgba, 8, 8))!;
        Assert.NotEmpty(Place(open, 0f, 0f, out _)[(int)Cover.Grass]);
    }

    /// <summary>A pick's value ceiling: a pale path's texels grow nothing however saturated they are, and the darker
    /// grass grows. A pick with no ceiling grows on the path too.</summary>
    [Fact]
    public void AValueCeilingKeepsTheCoverOffPalePaths()
    {
        // The left half is a pale, saturated path (value 0.9, saturation 0.3), the right half dry grass (value 0.42).
        var rgba = Flat(8, 128);
        for (int i = 0; i < 64; i++)
        {
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2]) = i % 8 >= 4 ? ((byte)108, (byte)98, (byte)76) : ((byte)230, (byte)200, (byte)160);
        }
        Assert.Equal(230f / 255f, GroundCover.Surface.Value(new Vector3(230f, 200f, 160f)), 4);
        var t = new CoverTriangle("x.tif", Vector3.Zero, new Vector3(400f, 0f, 0f), new Vector3(0f, 0f, 400f),
            Vector2.Zero, new Vector2(2f, 0f), new Vector2(0f, 2f), Colors.White, Colors.White, Colors.White);
        var gated = GroundCover.Ground.Prepare(t, Cover.Grass, new GroundCover.Surface(rgba, 8, 8, 0.2f, 0.55f))!;
        Assert.Empty(Place(gated, 0f, 0f, out _)[(int)Cover.Grass]);
        Assert.NotEmpty(Place(gated, 128f, 0f, out _)[(int)Cover.Grass]);
        var open = GroundCover.Ground.Prepare(t, Cover.Grass, new GroundCover.Surface(rgba, 8, 8, 0.2f))!;
        Assert.NotEmpty(Place(open, 0f, 0f, out _)[(int)Cover.Grass]);
    }

    /// <summary>A texture pack draws the ground at four times the size in another colour. The gate still reads the
    /// authored texels; the colour reads the drawn image, averaged over the same ground.</summary>
    [Fact]
    public void AnHdReplacementColoursTheCoverButNeverMovesTheGate()
    {
        var rgba = Flat(8, 128);
        for (int i = 0; i < 64; i++)
        {
            if (i % 8 >= 4)
                (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2]) = (108, 98, 76);
        }
        var hd = Flat(32, 0);
        for (int i = 0; i < 32 * 32; i++)
            (hd[i * 4], hd[(i * 4) + 1], hd[(i * 4) + 2]) = (i % 32 >= 16 ? (byte)200 : (byte)20, 60, 60);
        var surface = new GroundCover.Surface(rgba, 8, 8, 0.2f, 1f, (hd, 32, 32));
        Assert.False(surface.Grows(0.1, 0.5));
        Assert.True(surface.Grows(0.9, 0.5));
        Assert.Equal(new Vector3(Lin(200), Lin(60), Lin(60)).X, surface.At(0.8, 0.5).X, 4);
        Assert.Equal(new Vector3(Lin(20), Lin(60), Lin(60)).X, surface.At(0.2, 0.5).X, 4);

        static float Lin(byte c) => MathF.Pow(((c / 255f) + 0.055f) / 1.055f, 2.4f);
    }

    [Fact]
    public void ABareTexelGrowsNothingAndAHedgeLineGrass()
    {
        Assert.Null(GroundCover.Under(Cover.Crop, 0));
        Assert.Null(GroundCover.Under(Cover.Grass, 0));
        Assert.Equal(Cover.Crop, GroundCover.Under(Cover.Crop, 2));
        Assert.Equal(Cover.Crop, GroundCover.Under(Cover.Crop, VegetationMask.NeutralByte));
        Assert.Equal(Cover.Grass, GroundCover.Under(Cover.Crop, 255));
        Assert.Equal(Cover.Grass, GroundCover.Under(Cover.Grass, 255));
    }

    [Fact]
    public void TheMaskTexelIsReadUnsampled()
    {
        var mask = VegetationMask.FromBytes(new byte[] { 0, 128, 255, 2 }, 0f, 0f, 24f, 24f);
        Assert.Equal(0, mask.TexelAt(11.9f, 1f));
        Assert.Equal(128, mask.TexelAt(12.1f, 1f));
        Assert.Equal(255, mask.TexelAt(1f, 12.1f));
        Assert.Equal(2, mask.TexelAt(23f, 23f));
        Assert.Equal(VegetationMask.NeutralByte, mask.TexelAt(-1f, 5f));
    }

    /// <summary>Stripes down the image (brightness changing across X) are rows along V, and stripes across it are not.</summary>
    [Fact]
    public void TheRowMapReadsThePaintedStripes()
    {
        Assert.All(GroundCover.RowsAlongV(Striped(64, alongV: true), 64, 64), Assert.True);
        Assert.All(GroundCover.RowsAlongV(Striped(64, alongV: false), 64, 64), Assert.False);
        Assert.Equal(GroundCover.Patches * GroundCover.Patches, GroundCover.RowsAlongV(Flat(8, 100), 8, 8).Length);
    }

    /// <summary>Grass fills the cell's share of the triangle at its spacing, every tuft on the ground inside the cell.
    /// Each takes the ground's linear colour, and a second walk places the same tufts.</summary>
    [Fact]
    public void GrassStandsOnTheTriangleInsideItsCell()
    {
        var g = Ground(Cover.Grass, Flat(8, 128), Vector3.Zero);
        var cards = Place(g, 0f, 0f, out int bare);
        Assert.Equal(0, bare);
        Assert.Empty(cards[(int)Cover.Crop]);
        var grass = cards[(int)Cover.Grass];
        float expected = GroundCover.CellM * GroundCover.CellM / (GroundCover.GrassSpacingM * GroundCover.GrassSpacingM);
        Assert.InRange(grass.Count, expected * 0.95f, expected * 1.05f);
        Assert.All(grass, c =>
        {
            Assert.InRange(c.Xf.Origin.X, 0f, GroundCover.CellM);
            Assert.InRange(c.Xf.Origin.Z, 0f, GroundCover.CellM);
            Assert.Equal(0f, c.Xf.Origin.Y, 4);
            Assert.InRange(c.Albedo.G, 0.2f * 0.92f, 0.22f * 1.08f);
            Assert.Equal(Vector3.Up, c.Normal);
        });
        Assert.Equal(grass, Place(g, 0f, 0f, out _)[(int)Cover.Grass]);
    }

    /// <summary>A crop card lies along the rows its patch paints, but the rows are loose. Their spacing and the cards'
    /// places along them vary, so no lattice shows.</summary>
    [Fact]
    public void CropRowsFollowThePaintedRowsLoosely()
    {
        foreach (bool alongV in new[] { true, false })
        {
            var crops = Place(Ground(Cover.Crop, Striped(64, alongV), Vector3.Zero), 0f, 0f, out _)[(int)Cover.Crop];
            // The triangle's U runs along world X and its V along world Z, 200 m a repeat.
            var row = alongV ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);
            float steps = GroundCover.CellM * GroundCover.CellM / (GroundCover.RowSpacingM * GroundCover.CropStepM);
            Assert.InRange(crops.Count, steps * 0.6f, steps);
            Assert.All(crops, c => Assert.Equal(1f, Math.Abs(c.Xf.Basis.X.Normalized().Dot(row)), 4));
            var offRow = crops.Select(c => ((alongV ? c.Xf.Origin.X : c.Xf.Origin.Z) / GroundCover.RowSpacingM) - 0.5)
                .Select(k => Math.Abs(k - Math.Round(k))).ToList();
            Assert.True(offRow.Average() > 0.05, $"rows sit on a lattice ({offRow.Average():0.###})");
        }
        foreach (int r in Enumerable.Range(-50, 100))
        {
            var (across, along) = GroundCover.Loose(r, 1, GroundCover.Hash(r, 7, 9));
            Assert.InRange(across, -0.28, 0.28);
            Assert.InRange(along, -0.33, 1.33);
        }
        Assert.NotEqual(GroundCover.Loose(3, 1, 0u).Along, GroundCover.Loose(4, 1, 0u).Along);
    }

    /// <summary>A bare texel drops the cards over it and counts them; a hedge line turns a crop's cards to grass.</summary>
    [Fact]
    public void TheMaskClearsTheStripAndGrassesTheHedge()
    {
        var g = Ground(Cover.Crop, Flat(8, 128), Vector3.Zero);
        var cards = new[] { new List<GroundCover.Card>(), new List<GroundCover.Card>() };
        int bare = GroundCover.Place(g, 0f, 0f, GroundCover.CellM, (x, _) => x < 20f ? (byte)0 : x < 40f ? (byte)255 : (byte)2, cards);
        Assert.True(bare > 0);
        Assert.All(cards.SelectMany(c => c), c => Assert.True(c.Xf.Origin.X >= 20f));
        Assert.All(cards[(int)Cover.Grass], c => Assert.InRange(c.Xf.Origin.X, 20f, 40f));
        Assert.All(cards[(int)Cover.Crop], c => Assert.True(c.Xf.Origin.X >= 40f));
        Assert.NotEmpty(cards[(int)Cover.Grass]);
        Assert.NotEmpty(cards[(int)Cover.Crop]);
    }

    /// <summary>A card's foot lies along a sloping ground's plane, while it stands straight up.</summary>
    [Fact]
    public void ACardsFootFollowsTheSlope()
    {
        var g = Ground(Cover.Grass, Flat(8, 128), Vector3.Zero, rise: 60f);
        var grass = Place(g, 0f, 0f, out _)[(int)Cover.Grass];
        Assert.NotEmpty(grass);
        Assert.All(grass, c =>
        {
            Assert.Equal(0f, c.Xf.Basis.X.Dot(c.Normal), 3);
            Assert.Equal(0f, c.Xf.Basis.Z.Dot(c.Normal), 3);
            Assert.Equal(0f, c.Xf.Basis.Y.X, 5);
            Assert.Equal(0f, c.Xf.Basis.Y.Z, 5);
            Assert.Equal(c.Xf.Origin.Z * 60f / 200f, c.Xf.Origin.Y, 2);
        });
    }

    /// <summary>A wall grows nothing, and a cell off the triangle takes no card from it.</summary>
    [Fact]
    public void AWallAndAFarCellGrowNothing()
    {
        var wall = new CoverTriangle("terpat03.tif", Vector3.Zero, new Vector3(10f, 0f, 0f), new Vector3(0f, 10f, 0f),
            Vector2.Zero, Vector2.Right, Vector2.Down, Colors.White, Colors.White, Colors.White);
        Assert.Null(GroundCover.Ground.Prepare(wall, Cover.Grass, new GroundCover.Surface(Flat(8, 128), 8, 8)));
        Assert.Empty(Place(Ground(Cover.Grass, Flat(8, 128), Vector3.Zero), 640f, 640f, out _).SelectMany(c => c));
    }

    [Fact]
    public void TheArtIsTheSameOnEveryRunAndAveragesAGainOfOne()
    {
        foreach (var (art, width) in new[] { (GroundCover.GrassArt(), GroundCover.ArtSide), (GroundCover.CropArt(), GroundCover.ArtSide * 2) })
        {
            Assert.Equal(width * GroundCover.ArtSide * 4, art.Length);
            var solid = Enumerable.Range(0, art.Length / 4).Where(i => art[(i * 4) + 3] == 255).ToList();
            double share = (double)solid.Count / (art.Length / 4);
            Assert.InRange(share, 0.1, 0.7);
            Assert.InRange(solid.Average(i => art[i * 4]), 125.0, 130.0);
            Assert.All(Enumerable.Range(0, art.Length / 4).Where(i => art[(i * 4) + 3] == 0), i => Assert.InRange(art[i * 4], (byte)127, (byte)128));
        }
        Assert.Equal(GroundCover.GrassArt(), GroundCover.GrassArt());
        Assert.Equal(GroundCover.CropArt(), GroundCover.CropArt());
    }

    // A right triangle 400 m on its legs from `at`, its corners white. U runs along world X and V along world Z at
    // 200 m a repeat, and it rises `rise` metres per 200 m of Z.
    private static GroundCover.Ground Ground(Cover pick, byte[] rgba, Vector3 at, float rise = 0f)
    {
        int side = (int)Math.Sqrt(rgba.Length / 4);
        var t = new CoverTriangle("x.tif", at, at + new Vector3(400f, 0f, 0f), at + new Vector3(0f, 2f * rise, 400f),
            Vector2.Zero, new Vector2(2f, 0f), new Vector2(0f, 2f), Colors.White, Colors.White, Colors.White);
        return GroundCover.Ground.Prepare(t, pick, new GroundCover.Surface(rgba, side, side))!;
    }

    private static List<GroundCover.Card>[] Place(GroundCover.Ground g, float x0, float z0, out int bare)
    {
        var cards = new[] { new List<GroundCover.Card>(), new List<GroundCover.Card>() };
        bare = GroundCover.Place(g, x0, z0, GroundCover.CellM, (_, _) => VegetationMask.NeutralByte, cards);
        return cards;
    }

    private static byte[] Flat(int side, byte grey)
    {
        var rgba = new byte[side * side * 4];
        for (int i = 0; i < side * side; i++)
        {
            rgba[i * 4] = rgba[(i * 4) + 1] = rgba[(i * 4) + 2] = grey;
            rgba[(i * 4) + 3] = 255;
        }
        return rgba;
    }

    // Two-texel stripes, running down the image when `alongV`, else across it.
    private static byte[] Striped(int side, bool alongV)
    {
        var rgba = Flat(side, 0);
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                byte v = (((alongV ? x : y) / 2) % 2) == 0 ? (byte)60 : (byte)200;
                rgba[((y * side) + x) * 4] = rgba[(((y * side) + x) * 4) + 1] = rgba[(((y * side) + x) * 4) + 2] = v;
            }
        }
        return rgba;
    }
}
