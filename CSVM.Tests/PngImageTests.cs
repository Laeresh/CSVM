using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The PNG half of the menu art path: the <c>rimage</c> extraction's own shape (8-bit,
/// non-interlaced, with and without alpha) and the filters its rows use. The bytes here are
/// hand-authored from the PNG layout.</summary>
public class PngImageTests
{
    [Fact]
    public void ItDecodesTruecolourRowsAndGivesThemAnOpaqueAlpha()
    {
        // Row 0 unfiltered, row 1 predicted from the row above, so both come out red then green.
        var rows = new byte[]
        {
            0, 255, 0, 0, 0, 255, 0,
            2, 0, 0, 0, 0, 0, 0,
        };

        var image = PngImage.Decode(Png(2, 2, colourType: 2, rows));

        Assert.NotNull(image);
        Assert.Equal(2, image!.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(image, 0, 0));
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, Pixel(image, 1, 0));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(image, 0, 1));
    }

    [Fact]
    public void ItKeepsTheAlphaChannelOfAnRgbaFile()
    {
        // One row, filter 1 (each byte predicted from the pixel before it in the row).
        var rows = new byte[] { 1, 10, 20, 30, 128, 5, 5, 5, 127 };

        var image = PngImage.Decode(Png(2, 1, colourType: 6, rows));

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 10, 20, 30, 128 }, Pixel(image!, 0, 0));
        Assert.Equal(new byte[] { 15, 25, 35, 255 }, Pixel(image!, 1, 0));
    }

    [Fact]
    public void ItNormalizesAnImagesGammaToTheUiGamma()
    {
        var image = PngImage.Decode(Png(3, 1, colourType: 2,
            rows: new byte[] { 0, 64, 128, 255, 64, 128, 255, 64, 128, 255 }, gamma: 22727));

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 128, 181, 255, 255 }, Pixel(image!, 0, 0));
        Assert.Equal(new byte[] { 128, 181, 255, 255 }, Pixel(image, 1, 0));
        Assert.Equal(new byte[] { 128, 181, 255, 255 }, Pixel(image, 2, 0));
    }

    [Fact]
    public void SomethingThatIsNotAPngDecodesAsNullRatherThanThrowing()
    {
        Assert.Null(PngImage.Decode(new byte[] { 1, 2, 3, 4 }));
        Assert.Null(PngImage.Decode(Array.Empty<byte>()));
        Assert.Null(PngImage.TryLoad(Path.Combine(TestData.TempDir(), "absent.png")));
    }

    /// <summary>A palette or an interlaced file is outside the coverage the rimage extraction
    /// needs; it decodes as null so a screen loses its art instead of falling over.</summary>
    [Fact]
    public void AColourTypeThisDecoderDoesNotCoverIsNullNotAThrow()
    {
        Assert.Null(PngImage.Decode(Png(2, 1, colourType: 3, new byte[] { 0, 0, 1 })));
    }

    /// <summary>The real art the briefing draws: a mission map and a flag pin, both straight out
    /// of the rimage extraction.</summary>
    [ExtractedDataFact]
    public void ItDecodesTheBriefingsOwnMapAndPinArt()
    {
        var map = PngImage.Decode(Art("ha-m1map.png"));
        var pin = PngImage.Decode(Art("pin6.png"));

        Assert.NotNull(map);
        Assert.Equal(800, map!.Width);
        Assert.Equal(600, map.Height);
        Assert.NotNull(pin);
        Assert.Equal(76, pin!.Width);
        Assert.Equal(80, pin.Height);
    }

    // One rimage file, from whichever shape the install has: the unpacked folder the engine reads
    // or the zip it came from.
    private static byte[] Art(string name)
    {
        var dir = Path.Combine(TestData.ExtractedRoot!, "rimage");
        var loose = Path.Combine(dir, name);
        if (File.Exists(loose))
        {
            return File.ReadAllBytes(loose);
        }

        using var zip = ZipFile.OpenRead(dir + ".zip");
        using var entry = zip.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        entry.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] Pixel(TgaImage image, int x, int y)
    {
        int at = (((y * image.Width) + x) * 4);
        return new[] { image.Rgba[at], image.Rgba[at + 1], image.Rgba[at + 2], image.Rgba[at + 3] };
    }

    // A minimal PNG: signature, IHDR, one IDAT holding the zlib-compressed filtered rows, IEND.
    // The chunk CRCs are written as zeros, which the decoder under test skips.
    private static byte[] Png(int width, int height, byte colourType, byte[] rows, int? gamma = null)
    {
        var png = new List<byte> { 137, 80, 78, 71, 13, 10, 26, 10 };
        var ihdr = new List<byte>();
        ihdr.AddRange(BigEndian(width));
        ihdr.AddRange(BigEndian(height));
        ihdr.AddRange(new byte[] { 8, colourType, 0, 0, 0 });
        png.AddRange(Chunk("IHDR", ihdr.ToArray()));
        if (gamma is { } value)
        {
            png.AddRange(Chunk("gAMA", BigEndian(value)));
        }
        png.AddRange(Chunk("IDAT", Deflate(rows)));
        png.AddRange(Chunk("IEND", Array.Empty<byte>()));
        return png.ToArray();
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(raw, 0, raw.Length);
        }

        return buffer.ToArray();
    }

    private static byte[] Chunk(string type, byte[] body)
    {
        var chunk = new List<byte>();
        chunk.AddRange(BigEndian(body.Length));
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        chunk.AddRange(body);
        chunk.AddRange(new byte[4]);
        return chunk.ToArray();
    }

    private static byte[] BigEndian(int value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value,
    };
}
