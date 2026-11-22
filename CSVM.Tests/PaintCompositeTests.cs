using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The `.BM` skin composite, which reproduces the original loader's integer arithmetic
/// (<c>docs/formats/paint.md</c>). The case that matters is an unmasked texel: a weighted sum of
/// the three slots paints it black, the original leaves the shading map.
/// </summary>
public class PaintCompositeTests
{
    [Fact]
    public void AnUnmaskedTexelKeepsTheShadingMap()
    {
        for (int v = 0; v < 256; v++)
            Assert.Equal(v, PlanePainter.Tint(v, 0, 17));
    }

    [Fact]
    public void AFullMaskMultipliesByTheTint()
    {
        Assert.Equal(100, PlanePainter.Tint(200, 255, 128)); // 200 * 128 / 255 = 100.4
        Assert.Equal(200, PlanePainter.Tint(200, 255, 255));
        Assert.Equal(0, PlanePainter.Tint(200, 255, 0));
    }

    [Fact]
    public void SlotsCompoundRatherThanAdd()
    {
        // Two full masks on one texel: the second tints the first's result.
        int once = PlanePainter.Tint(200, 255, 128);
        Assert.Equal(50, PlanePainter.Tint(once, 255, 128));
    }

    [Fact]
    public void TheOverlayBlendsByItsFourthByte()
    {
        Assert.Equal(90, PlanePainter.Over(90, 250, 0));
        Assert.Equal(250, PlanePainter.Over(90, 250, 255));
        Assert.Equal(170, PlanePainter.Over(90, 250, 128)); // (128*250 + 127*90) / 255 = 170.3
    }

    // The loader's (257x + 0x8000) >> 16 falls one short of rounded division near 255^3.
    // The composite keeps the loader's value.
    [Fact]
    public void Div255KeepsTheLoadersRounding()
    {
        Assert.Equal(65024, PlanePainter.Div255(255 * 255 * 255));
        Assert.Equal(1, PlanePainter.Div255(128));
    }
}
