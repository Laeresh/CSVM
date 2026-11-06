using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// What each Shadow Quality level writes on the sun, engine-free (<see cref="ShadowQualitySetting"/>).
/// </summary>
public class ShadowQualityPlanTests
{
    // #297: wider, an aircraft's shadow on its own wings breaks into dots and holes. From about half a
    // degree it vanishes.
    private const float WidestSelfShadowingSun = 0.2f;

    [Theory]
    [InlineData(ShadowQualitySetting.Low)]
    [InlineData(ShadowQualitySetting.Medium)]
    [InlineData(ShadowQualitySetting.High)]
    [InlineData(ShadowQualitySetting.Ultra)]
    public void NoCastingLevelIsWiderThanAnAircraftsSelfShadowHolds(string word)
    {
        var plan = ShadowQualitySetting.PlanFor(word);
        Assert.True(plan.Cast);
        Assert.InRange(plan.AngularDistance, 0f, WidestSelfShadowingSun);
    }
}
