using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

public class HudMetricsTests
{
    [Fact]
    public void TextScalesDefaultToOneAndStayWithinTheAccessibleRange()
    {
        Assert.Equal(1f, HudMetrics.StatusTextScale, 3);
        Assert.Equal(1f, HudMetrics.MarkerTextScale, 3);
        Assert.Equal(0.5f, HudMetrics.ClampTextScale(0f), 3);
        Assert.Equal(0.5f, HudMetrics.ClampTextScale(0.49f), 3);
        Assert.Equal(1.25f, HudMetrics.ClampTextScale(1.25f), 3);
        Assert.Equal(2f, HudMetrics.ClampTextScale(2.01f), 3);
    }
}
