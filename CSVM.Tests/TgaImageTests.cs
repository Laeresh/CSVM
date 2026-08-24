using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The TGA decoder behind the hangar's art (C22): synthetic files pin the byte-level rules
/// (BGR order, the bottom-up default and the flip bit, both packet kinds of RLE, malformed input
/// reading as null), and the extracted-data facts prove the real shipped blueprints and icons
/// decode at their catalogued shape (PLAN-hangar A2: 358x335, 24-bit RLE and 32-bit RLE).
/// </summary>
public class TgaImageTests
{
    private const int BlueprintWidth = 358;
    private const int BlueprintHeight = 335;

    /// <summary>An uncompressed 24-bit file with no flip bit is bottom-up: the first stored row
    /// is the image's bottom, so decoding must flip it to top-down.</summary>
    [Fact]
    public void Type2_24Bit_DefaultOrientation_FlipsToTopDown()
    {
        // 1x2: stored bottom row red, stored top row blue (BGR on disk).
        var tga = Bytes(Header(2, 1, 2, 24, 0), 0, 0, 255, 255, 0, 0);
        var image = TgaImage.Decode(tga);

        Assert.NotNull(image);
        Assert.Equal(1, image!.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, image.Rgba);
    }

    /// <summary>Descriptor bit 5 marks the file top-down; rows then keep their stored order.</summary>
    [Fact]
    public void Type2_TopDownFlipBit_KeepsRowOrder()
    {
        var tga = Bytes(Header(2, 1, 2, 24, 0x20), 0, 0, 255, 255, 0, 0);
        var image = TgaImage.Decode(tga);

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, image!.Rgba);
    }

    /// <summary>RLE 32-bit: a run packet repeats one pixel, a raw packet carries each of its
    /// pixels, and the source alpha is kept.</summary>
    [Fact]
    public void Type10_32Bit_DecodesRunAndRawPackets()
    {
        // 2x2 top-down: a run of 3 of BGRA (1,2,3,4), then one raw pixel BGRA (5,6,7,8).
        var tga = Bytes(Header(10, 2, 2, 32, 0x20), 0x82, 1, 2, 3, 4, 0x00, 5, 6, 7, 8);
        var image = TgaImage.Decode(tga);

        Assert.NotNull(image);
        Assert.Equal(
            new byte[] { 3, 2, 1, 4, 3, 2, 1, 4, 3, 2, 1, 4, 7, 6, 5, 8 },
            image!.Rgba);
    }

    /// <summary>A 24-bit source decodes with an opaque alpha in every pixel.</summary>
    [Fact]
    public void Type10_24Bit_GetsOpaqueAlpha()
    {
        var tga = Bytes(Header(10, 2, 1, 24, 0x20), 0x81, 9, 8, 7);
        var image = TgaImage.Decode(tga);

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 7, 8, 9, 255, 7, 8, 9, 255 }, image!.Rgba);
    }

    /// <summary>Anything outside the decoder's coverage or cut short reads as null, never a
    /// throw: colour-mapped and greyscale types, a truncated pixel stream, a header alone, and an
    /// RLE packet claiming more pixels than the image holds.</summary>
    [Fact]
    public void MalformedOrUnsupportedInput_IsNull()
    {
        Assert.Null(TgaImage.Decode(Array.Empty<byte>()));
        Assert.Null(TgaImage.Decode(Header(2, 1, 1, 24, 0)));
        Assert.Null(TgaImage.Decode(Bytes(Header(1, 1, 1, 24, 0), 0, 0, 0)));
        Assert.Null(TgaImage.Decode(Bytes(Header(3, 1, 1, 8, 0), 0)));
        Assert.Null(TgaImage.Decode(Bytes(Header(2, 2, 2, 24, 0), 1, 2, 3)));
        Assert.Null(TgaImage.Decode(Bytes(Header(10, 2, 1, 24, 0x20), 0x84, 1, 2, 3)));
    }

    /// <summary>A path with no file behind it loads as null.</summary>
    [Fact]
    public void TryLoad_MissingFile_IsNull() =>
        Assert.Null(TgaImage.TryLoad(Path.Combine(Path.GetTempPath(), "csvm-no-such-file.tga")));

    /// <summary>A real shipped blueprint (24-bit RLE, type 10) decodes at the catalogued
    /// 358x335, fully opaque.</summary>
    [ExtractedDataFact]
    public void RealBlueprint_DecodesAtItsCataloguedShape()
    {
        var image = TgaImage.TryLoad(Graphics("PX_0_BLUEPRINT.TGA"));

        Assert.NotNull(image);
        Assert.Equal(BlueprintWidth, image!.Width);
        Assert.Equal(BlueprintHeight, image.Height);
        Assert.Equal(BlueprintWidth * BlueprintHeight * 4, image.Rgba.Length);
        for (int p = 3; p < image.Rgba.Length; p += 4)
        {
            Assert.Equal(255, image.Rgba[p]);
        }
    }

    /// <summary>A real shipped icon (32-bit RLE) decodes on the same 358x335 canvas as the
    /// blueprints, which is what lets the two overlay.</summary>
    [ExtractedDataFact]
    public void RealIcon_DecodesOnTheBlueprintCanvas()
    {
        var image = TgaImage.TryLoad(Graphics("PX_ICON_0_4_0.TGA"));

        Assert.NotNull(image);
        Assert.Equal(BlueprintWidth, image!.Width);
        Assert.Equal(BlueprintHeight, image.Height);
    }

    private static string Graphics(string file) =>
        Path.Combine(TestData.DataRoot!, "extracted", "rof", "ASSETS", "GRAPHICS", file);

    private static byte[] Header(byte type, int width, int height, byte bpp, byte descriptor) =>
        new byte[]
        {
            0, 0, type, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)(width & 0xff), (byte)(width >> 8),
            (byte)(height & 0xff), (byte)(height >> 8),
            bpp, descriptor,
        };

    private static byte[] Bytes(byte[] header, params int[] rest)
    {
        var all = new List<byte>(header);
        foreach (int b in rest)
        {
            all.Add((byte)b);
        }

        return all.ToArray();
    }
}
