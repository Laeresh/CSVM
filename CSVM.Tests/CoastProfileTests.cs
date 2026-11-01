using System;
using CSVM.Mech3;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="CoastProfile.Of"/> over synthetic textures, whose crossings are known in closed form,
/// and over the painted coasts the install ships, against the figures of
/// analysis/painted-coasts/FINDINGS.md. A crossing holds to within one texel along its axis.
/// </summary>
public class CoastProfileTests
{
    private const int N = 64;

    private static readonly (float R, float G, float B) Sheet = (30f, 60f, 70f);

    private readonly ITestOutputHelper _output;

    public CoastProfileTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ATwoEdgedAlphaStripCrossesOnTheEdgeWhoseColourIsTheSheets()
    {
        // Alpha rises over texels 8 to 24 from either edge, so it passes 0.5 at texel 16 from each.
        byte Alpha(int k) => Byte(Math.Min(k - 8, N - 9 - k) / 16.0);
        var water = (30, 60, 70);
        var sand = (200, 180, 120);

        var p = CoastProfile.Of(Image(N, 4, k => k < N / 2 ? water : sand, Alpha), Sheet);
        Assert.NotNull(p);
        Assert.False(p.Value.AlongV);
        Assert.False(p.Value.WaterAtOne);
        Near(16.5 / N, p.Value.Crossing);
        Near(12.8 / N, p.Value.Fade);

        var mirrored = CoastProfile.Of(Image(N, 4, k => k < N / 2 ? sand : water, Alpha), Sheet);
        Assert.NotNull(mirrored);
        Assert.True(mirrored.Value.WaterAtOne);
        Near(1 - (16.5 / N), mirrored.Value.Crossing);
    }

    [Fact]
    public void AOneEdgedAlphaStripCrossesOnItsTransparentEdge()
    {
        // Along V, opaque at v = 0 and clear from row 47: alpha passes 0.5 at texel 24 from the water
        // end. The colour is the sand's throughout, so it cannot decide.
        var p = CoastProfile.Of(Image(4, N, _ => (200, 180, 120), r => Byte((47 - r) / 16.0), alongV: true), Sheet);
        Assert.NotNull(p);
        Assert.True(p.Value.AlongV);
        Assert.True(p.Value.WaterAtOne);
        Near(1 - (24.5 / N), p.Value.Crossing);
        Near(12.8 / N, p.Value.Fade);
    }

    [Fact]
    public void AnOpaqueRampCrossesWhereItsColourLeavesThePaintedWater()
    {
        // The painted water sits a quarter of the way from the sheet's colour to the land's. The
        // blend runs over texels 16 to 48 from the water end, so 10 % falls at 19.2 and 90 % at 44.8.
        var sheet = (80f, 120f, 120f);
        (int, int, int) Colour(int r)
        {
            double t = Math.Clamp((N - 1 - r - 16) / 32.0, 0, 1);
            return ((int)Math.Round(70 - (30 * t)), (int)Math.Round(110 - (30 * t)), (int)Math.Round(100 - (60 * t)));
        }

        var p = CoastProfile.Of(Image(4, N, Colour, null, alongV: true), sheet);
        Assert.NotNull(p);
        Assert.True(p.Value.AlongV);
        Assert.True(p.Value.WaterAtOne);
        Near(1 - (19.7 / N), p.Value.Crossing);
        Near(25.6 / N, p.Value.Fade);
    }

    [Fact]
    public void AnOpaqueTextureOfOneColourHasNoCrossing()
    {
        Assert.Null(CoastProfile.Of(Image(N, 4, _ => (40, 80, 40), null), Sheet));
    }

    // C1's overlay crossing is in its own V; FINDINGS' A2 fit maps that row to the tile's u 0.846.
    // NaN marks a fade FINDINGS does not record.
    [GameFilesTheory]
    [InlineData("C3", "wtr00000", "shore1", false, false, 0.188, double.NaN)]
    [InlineData("C3", "wtr00000", "shore_trans", false, false, 0.195, double.NaN)]
    [InlineData("C3", "wtr00000", "cliff1_watertrans", true, true, 0.777, 0.656)]
    [InlineData("C3", "wtr00000", "cliff1_watertrans2", true, true, 0.762, 0.609)]
    [InlineData("C2", "wtr00000", "beach1", true, false, 0.125, double.NaN)]
    [InlineData("C2", "wtr00000", "cliff01_trans2", false, false, 0.398, 0.422)]
    [InlineData("C1", "water1", "cliff01_trans2", true, true, 0.617, double.NaN)]
    public void ThePaintedCoastsCrossWhereTheirPixelsWereMeasuredTo(
        string chapter, string sheet, string texture, bool alongV, bool waterAtOne, double crossing, double fade)
    {
        using var archive = new TextureArchive(TextureSet.ForChapter(GameFilesData.Source, chapter));
        var image = archive.Lookup(texture);
        Assert.NotNull(image);
        var p = archive.CoastProfileOf(texture, sheet);
        Assert.NotNull(p);
        _output.WriteLine($"{chapter} {texture} {image.Width}x{image.Height}: {p}");
        double texel = 1.0 / (alongV ? image.Height : image.Width);
        Assert.Equal(alongV, p.Value.AlongV);
        Assert.Equal(waterAtOne, p.Value.WaterAtOne);
        Assert.InRange(p.Value.Crossing, crossing - texel, crossing + texel);
        if (!double.IsNaN(fade))
            Assert.InRange(p.Value.Fade, fade - texel, fade + texel);
    }

    [Fact]
    public void OnlyTheListedPaintedCoastsHaveAProfile()
    {
        using var archive = new TextureArchive(new TextureSet("probe", new[]
        {
            Image(N, 4, _ => (30, 60, 70), k => Byte((k - 8) / 16.0), name: "cliff1_watertrans3"),
            Image(1, 1, _ => (30, 60, 70), null, name: "wtr00000"),
        }));
        Assert.Null(archive.CoastProfileOf("cliff1_watertrans3", "wtr00000"));
        Assert.True(TextureArchive.IsPaintedSeaTile("water1_trans2.tif"));
        Assert.DoesNotContain("water1_trans1", TextureArchive.PaintedCoasts);
    }

    private static byte Byte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255);

    private static void Near(double expected, float actual) =>
        Assert.InRange(actual, expected - (1.0 / N), expected + (1.0 / N));

    // A texture that varies along one axis: texel k along it takes colour(k) and, given alpha(k), that alpha.
    private static TextureImage Image(int w, int h, Func<int, (int R, int G, int B)> colour, Func<int, byte>? alpha,
        bool alongV = false, string name = "probe")
    {
        int ch = alpha == null ? 3 : 4;
        var px = new byte[w * h * ch];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int k = alongV ? y : x, i = ((y * w) + x) * ch;
                var (r, g, b) = colour(k);
                px[i] = (byte)r;
                px[i + 1] = (byte)g;
                px[i + 2] = (byte)b;
                if (alpha != null)
                    px[i + 3] = alpha(k);
            }
        }
        return new TextureImage(name, null, w, h, TextureArchive.AlphaClass.None, 0, alpha != null, px);
    }
}
