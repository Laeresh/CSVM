using System.IO;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>A plane mod's livery tint: the per-texel composite of base texture, mask, detail and the
/// scheme's three colours.</summary>
public class ModTintTests
{
    private static readonly Color Red = new(1f, 0f, 0f);
    private static readonly Color Green = new(0f, 1f, 0f);
    private static readonly Color Blue = new(0f, 0f, 1f);

    [Fact]
    public void EachChannelTakesItsSlotColourAndAnUnmaskedTexelKeepsTheBase()
    {
        // Four texels: R, G, B and no channel; detail 128 is neutral.
        var tint = new ModTint(new[] { "Body" }, 4, 1, new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0 }, 1, 1, new byte[] { 128 });
        var rgba = Texels(4, 10, 20, 30, 77);

        tint.Composite(rgba, 4, 1, Red, Green, Blue);

        Assert.Equal(new byte[] { 255, 0, 0, 77 }, rgba[0..4]);
        Assert.Equal(new byte[] { 0, 255, 0, 77 }, rgba[4..8]);
        Assert.Equal(new byte[] { 0, 0, 255, 77 }, rgba[8..12]);
        Assert.Equal(new byte[] { 10, 20, 30, 77 }, rgba[12..16]);
        Assert.True(tint.Covers("body"));
        Assert.False(tint.Covers("glass"));
    }

    [Fact]
    public void APartialChannelBlendsAndTheDetailShadesTheColour()
    {
        // Mask R half on, detail 64 halves the colour: 100 * 0.5 + 255 * 0.5 * 0.5 = 113.75.
        var tint = new ModTint(new[] { "body" }, 1, 1, new byte[] { 128, 0, 0 }, 1, 1, new byte[] { 64 });
        var rgba = Texels(1, 100, 100, 100, 255);

        tint.Composite(rgba, 1, 1, Red, Green, Blue);

        Assert.InRange(rgba[0], 113, 115);
        Assert.InRange(rgba[1], 49, 51);
    }

    [Fact]
    public void ChannelsSummingPastOneShareTheTexelAndHideTheBase()
    {
        var tint = new ModTint(new[] { "body" }, 1, 1, new byte[] { 255, 255, 0 }, 1, 1, new byte[] { 128 });
        var rgba = Texels(1, 9, 9, 9, 255);

        tint.Composite(rgba, 1, 1, Red, Green, Blue);

        Assert.Equal(new byte[] { 128, 128, 0 }, rgba[0..3]);
    }

    [Fact]
    public void ASmallerMaskIsSampledNearestAtTheBasesSize()
    {
        // A 1x1 all-R mask over a 2x2 base tints every texel.
        var tint = new ModTint(new[] { "body" }, 1, 1, new byte[] { 255, 0, 0 }, 1, 1, new byte[] { 128 });
        var rgba = Texels(4, 0, 0, 0, 255);

        tint.Composite(rgba, 2, 2, Blue, Red, Red);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(new byte[] { 0, 0, 255 }, rgba[(i * 4)..((i * 4) + 3)]);
        }
    }

    [Fact]
    public void ImagesShorterThanTheirSizeAreRefused() =>
        Assert.Throws<InvalidDataException>(() => new ModTint(new[] { "body" }, 2, 2, new byte[3], 1, 1, new byte[1]));

    private static byte[] Texels(int count, byte r, byte g, byte b, byte a)
    {
        var rgba = new byte[count * 4];
        for (int i = 0; i < count; i++)
        {
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) = (r, g, b, a);
        }

        return rgba;
    }
}
