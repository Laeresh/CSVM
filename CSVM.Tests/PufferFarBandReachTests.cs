using CSVM.Effects;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The far band's depth factor under the View Distance reach (see <c>docs/org/puffer.md</c>). The
/// drawn alpha over a live emitter is the <c>puffer-distance-fade</c> engine suite's job.
/// </summary>
public class PufferFarBandReachTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(1.37f)]
    public void ReachOneKeepsTheGlobalFadeFactorBitForBit(float global)
    {
        float factor = Puffer.FarBandFactor(global, reach: 1f);

        Assert.Equal(System.BitConverter.SingleToInt32Bits(global),
            System.BitConverter.SingleToInt32Bits(factor));
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(4f)]
    public void AReachMeasuresTheStretchedBandAtTheAuthoredOne(float reach)
    {
        // large_30sec_fire's fire_n_smoke authors FADE_RANGE [1500, 1700].
        float factor = Puffer.FarBandFactor(Puffer.GlobalFadeFactorDefault, reach);

        Assert.Equal(1500f, 1500f * reach * factor, 2);
        Assert.Equal(1700f, 1700f * reach * factor, 2);
    }

    [Fact]
    public void AnUnlimitedReachMeasuresEveryDepthAtZero()
    {
        float factor = Puffer.FarBandFactor(Puffer.GlobalFadeFactorDefault, float.PositiveInfinity);

        Assert.Equal(0f, factor);
        Assert.Equal(0f, 1e9f * factor);
    }

    [Fact]
    public void AReachComposesWithTheGlobalFadeFactor()
    {
        Assert.Equal(0.25f, Puffer.FarBandFactor(0.5f, 2f), 6);
    }
}
