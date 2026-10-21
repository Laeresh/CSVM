using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The vegetation mask (<see cref="VegetationMask"/>): its world mapping, its sampling, a brush stroke
/// and the file round trip. Also the density rule that keeps or drops a listed stamp.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VegetationMaskTests
{
    /// <summary>A neutral mask reads 1 everywhere, and outside the area too.</summary>
    [Fact]
    public void ANeutralMaskReadsOneInsideAndOutside()
    {
        var mask = C1Shaped();
        Assert.True(mask.IsNeutral());
        Assert.Equal(1f, mask.At(-6000f, -6000f));
        Assert.Equal(1f, mask.At(-12288f, 0f));
        Assert.Equal(1f, mask.At(500f, -6000f));
        Assert.Equal(1f, mask.At(-6000f, -20000f));
    }

    /// <summary>Row 0 runs along the lowest Z and column 0 along the lowest X. The first byte is therefore
    /// the area's lowest corner and the last its highest.</summary>
    [Fact]
    public void TheFirstByteIsTheLowestCornerAndTheLastTheHighest()
    {
        var mask = C1Shaped();
        float texel = 12288f / 64;
        mask.Stroke(-12288f + (texel / 2), -12288f + (texel / 2), texel / 4, 1f, VegetationBrush.Clear);
        mask.Stroke(-texel / 2, -texel / 2, texel / 4, 1f, VegetationBrush.Thicken);
        byte[] bytes = mask.ToBytes();
        Assert.Equal(0, bytes[0]);
        Assert.Equal(255, bytes[^1]);
        Assert.Equal(VegetationMask.NeutralByte, bytes[1]);
        Assert.Equal(VegetationMask.NeutralByte, bytes[64]);
        Assert.Equal(0f, mask.At(-12288f + (texel / 2), -12288f + (texel / 2)));
    }

    /// <summary>A Clear stroke bares its radius and leaves the rest neutral; Neutral undoes it.</summary>
    [Fact]
    public void ClearBaresTheRadiusAndNeutralUndoesIt()
    {
        var mask = C1Shaped(256);
        Assert.True(mask.Stroke(-6000f, -6000f, 300f, 1f, VegetationBrush.Clear));
        Assert.Equal(0f, mask.At(-6000f, -6000f));
        Assert.Equal(1f, mask.At(-6000f, -5000f));
        Assert.True(mask.Stroke(-6000f, -6000f, 300f, 1f, VegetationBrush.Neutral));
        Assert.True(mask.IsNeutral());
    }

    /// <summary>Thicken raises and Thin lowers, most at the centre and none at the radius; a dab with no
    /// texel inside it changes nothing.</summary>
    [Fact]
    public void ThickenAndThinFallOffToTheRadius()
    {
        var mask = C1Shaped(256);
        mask.Stroke(-6000f, -6000f, 500f, 0.5f, VegetationBrush.Thicken);
        float centre = mask.At(-6000f, -6000f);
        float edge = mask.At(-6000f, -5600f);
        Assert.True(centre > edge && edge > 1f, $"centre {centre}, edge {edge}");
        Assert.Equal(1f, mask.At(-6000f, -5400f));

        var thin = C1Shaped(256);
        thin.Stroke(-6000f, -6000f, 500f, 0.5f, VegetationBrush.Thin);
        Assert.InRange(thin.At(-6000f, -6000f), 0.45f, 0.55f);
        Assert.False(thin.Stroke(5000f, 5000f, 10f, 1f, VegetationBrush.Clear));
    }

    /// <summary>A saved mask reads back byte for byte through an atomic write, and a file that is not a
    /// square of bytes is refused.</summary>
    [Fact]
    public void ASavedMaskReadsBackAndANonSquareFileIsRefused()
    {
        var mask = C1Shaped();
        mask.Stroke(-3000f, -9000f, 800f, 0.7f, VegetationBrush.Thin);
        string path = Path.Combine(TestData.TempDir(), "vegetation", "C1.bin");
        mask.Save(path);
        Assert.False(File.Exists(path + ".tmp"));
        var back = VegetationMask.FromBytes(File.ReadAllBytes(path), -12288f, -12288f, 0f, 0f);
        Assert.Equal(mask.ToBytes(), back.ToBytes());
        Assert.Equal(mask.At(-3000f, -9000f), back.At(-3000f, -9000f));
        Assert.Throws<InvalidDataException>(() => VegetationMask.FromBytes(new byte[10], 0f, 0f, 1f, 1f));
        Assert.Equal("vegetation/C1.bin", VegetationMask.PathFor("C1"));
    }

    /// <summary>The density rule: an original stamp is kept whole at neutral or above and thins below it.
    /// An added stamp shows half its candidates at neutral and all at the densest.</summary>
    [Fact]
    public void TheDensityRuleThinsOriginalsAndScalesAddedStamps()
    {
        Assert.True(VegetationMask.Keeps(1f, 0.999f, added: false));
        Assert.True(VegetationMask.Keeps(1.99f, 0.999f, added: false));
        Assert.False(VegetationMask.Keeps(0f, 0f, added: false));
        Assert.True(VegetationMask.Keeps(0.5f, 0.49f, added: false));
        Assert.False(VegetationMask.Keeps(0.5f, 0.5f, added: false));
        Assert.True(VegetationMask.Keeps(1f, 0.49f, added: true));
        Assert.False(VegetationMask.Keeps(1f, 0.5f, added: true));
        Assert.True(VegetationMask.Keeps(255f / 128f, 0.99f, added: true));
    }

    // C1's world node area: left and top -12288, right and bottom 0.
    private static VegetationMask C1Shaped(int side = 64) => VegetationMask.Neutral(-12288f, -12288f, 0f, 0f, side);
}
