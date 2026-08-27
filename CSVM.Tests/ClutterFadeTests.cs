using CSVM.Mech3;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The templates-clutter distance fade's two pure halves: the per-stamp thresholds the stamper's
/// step 11 stores (<see cref="ClutterKindProps.FadeThresholds(float)"/>) and the graphics
/// EffectsLevel's squared scale (<see cref="EffectsLevel"/>). Decode: docs/formats/templates.md.
/// </summary>
public class ClutterFadeTests
{
    // C1's firtree2: [[300, 600], [1000, 2000]], read by BOUND (min pair, max pair).
    private static readonly Vector2 Min = new(300f, 600f);
    private static readonly Vector2 Max = new(1000f, 2000f);

    [Fact]
    public void OneDrawLerpsBothDistancesAndStoresTheirSquares()
    {
        var atMin = ClutterKindProps.FadeThresholds(Min, Max, 0f);
        Assert.Equal(300f * 300f, atMin.X);
        Assert.Equal(600f * 600f, atMin.Y);
        Assert.Equal(1f / ((600f * 600f) - (300f * 300f)), atMin.Z, 6);

        var atMax = ClutterKindProps.FadeThresholds(Min, Max, 1f);
        Assert.Equal(1000f * 1000f, atMax.X);
        Assert.Equal(2000f * 2000f, atMax.Y);

        // The same t moves near and far together: the two are correlated, never rolled apart.
        var mid = ClutterKindProps.FadeThresholds(Min, Max, 0.5f);
        Assert.Equal(650f * 650f, mid.X);
        Assert.Equal(1300f * 1300f, mid.Y);
    }

    [Fact]
    public void AZeroFarMaxIsTheNeverFadesSentinel()
    {
        Assert.Equal(Vector3.Zero, ClutterKindProps.FadeThresholds(Min, new Vector2(1000f, 0f), 0.7f));
        Assert.Equal(Vector3.Zero, new ClutterKindProps().FadeThresholds(0.7f));
    }

    [Fact]
    public void CoincidentDistancesGiveAZeroReciprocalNotADivision()
    {
        var flat = ClutterKindProps.FadeThresholds(new Vector2(500f, 500f), new Vector2(500f, 500f), 0.3f);
        Assert.Equal(250_000f, flat.X);
        Assert.Equal(250_000f, flat.Y);
        Assert.Equal(0f, flat.Z);
    }

    [Theory]
    [InlineData("high", 1f)]
    [InlineData("HIGH", 1f)]
    [InlineData(" Medium ", 4f)]
    [InlineData("low", 9f)]
    public void TheLevelWordsMapToTheEnginesSquaredScales(string level, float expected)
    {
        Assert.True(EffectsLevel.TryClutterFadeScaleSq(level, out float scaleSq));
        Assert.Equal(expected, scaleSq);
    }

    [Fact]
    public void AnUnknownLevelWordFallsBackToHigh()
    {
        Assert.False(EffectsLevel.TryClutterFadeScaleSq("ultra", out float scaleSq));
        Assert.Equal(1f, scaleSq);
    }

    [Theory]
    [InlineData("high")]
    [InlineData("medium")]
    [InlineData("low")]
    public void TheFadeSwitchOffIsScaleZeroAtEveryLevel(string level)
    {
        // 0 is the never-fades scale: scale × d² never reaches any near², so the shader's
        // "inside near" arm always wins, whatever the level word says.
        Assert.Equal(0f, EffectsLevel.ClutterFadeScaleSq(false, level));
        Assert.NotEqual(0f, EffectsLevel.ClutterFadeScaleSq(true, level));
    }
}
