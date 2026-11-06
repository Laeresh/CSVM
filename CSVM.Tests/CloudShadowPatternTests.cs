using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Enhanced cloud shadows' drift. The offset runs against the drift inside one period. The drift
/// comes from the ceiling, then the sideways wind, then the fixed drift. The word is on with nothing
/// set and off under <c>--det</c>.
/// </summary>
public class CloudShadowPatternTests
{
    [Fact]
    public void TheOffsetRunsAgainstTheDriftWrappedIntoOnePeriod()
    {
        // A point reads the pattern at its X/Z plus the offset, so the offset runs against the drift.
        var offset = CloudShadowPattern.Offset(new Vector2(6f, -2f), 10.0);
        Assert.Equal(CloudShadowPattern.Period - 60f, offset.X, 1);
        Assert.Equal(20f, offset.Y, 3);
    }

    [Fact]
    public void TheOffsetStaysInsideOnePeriodOverHours()
    {
        var offset = CloudShadowPattern.Offset(new Vector2(20f, -20f), 3600.0 * 50);
        Assert.InRange(offset.X, 0f, CloudShadowPattern.Period);
        Assert.InRange(offset.Y, 0f, CloudShadowPattern.Period);
    }

    [Fact]
    public void ADeckMissionDriftsWithItsCeiling()
    {
        var ceiling = new Vector2(4f, 1f);
        var (velocity, source) = CloudShadowPattern.ChooseDrift(ceiling, new Vector3(0f, 2f, 0f));
        Assert.Equal(CloudShadowPattern.DriftSource.Ceiling, source);
        Assert.Equal(ceiling, velocity);
        // A ceiling estimate past the cap is held to it.
        var (fast, _) = CloudShadowPattern.ChooseDrift(new Vector2(300f, 0f), Vector3.Zero);
        Assert.Equal(CloudShadowPattern.MaxCeilingDrift, fast.Length(), 3);
    }

    [Fact]
    public void AMissionWithSidewaysWindDriftsWithIt()
    {
        var (velocity, source) = CloudShadowPattern.ChooseDrift(null, new Vector3(3f, 2f, -1.5f));
        Assert.Equal(CloudShadowPattern.DriftSource.Wind, source);
        Assert.Equal(new Vector2(3f, -1.5f), velocity);
    }

    [Fact]
    public void AMissionWithNeitherDriftsSlowlyOnTheFixedDrift()
    {
        // Every shipped mission authors STATIC_VELOCITY (0, 2, 0): an updraft, no sideways part.
        var (velocity, source) = CloudShadowPattern.ChooseDrift(null, new Vector3(0f, 2f, 0f));
        Assert.Equal(CloudShadowPattern.DriftSource.Fixed, source);
        Assert.Equal(CloudShadowPattern.FixedDrift, velocity);
        Assert.InRange(velocity.Length(), 1f, 8f);
    }

    [Fact]
    public void APlanarScrollingCeilingDriftsAgainstItsScrollScaledToTheBand()
    {
        // A flat ceiling 2000 m above the dome's origin, one UV unit across 1000 m of X and of Z.
        var corners = new List<Vector3>
        {
            new(0f, 2000f, 0f), new(1000f, 2000f, 0f), new(0f, 2000f, 1000f),
            new(1000f, 2000f, 0f), new(1000f, 2000f, 1000f), new(0f, 2000f, 1000f),
        };
        var uvs = new List<Vector2>
        {
            new(0f, 0f), new(1f, 0f), new(0f, 1f),
            new(1f, 0f), new(1f, 1f), new(0f, 1f),
        };
        var drift = CloudShadowPattern.CeilingDrift(corners, uvs, new Vector2(0.01f, 0f), 1000f);
        Assert.NotNull(drift);
        // The texture moves against the scroll: 10 m/s on the dome, halved to the band's altitude.
        Assert.Equal(-5f, drift!.Value.X, 3);
        Assert.Equal(0f, drift.Value.Y, 3);
    }

    [Fact]
    public void AWallOnlyCeilingCarriesNoDrift()
    {
        var corners = new List<Vector3> { new(0f, 0f, 0f), new(100f, 0f, 0f), new(0f, 100f, 0f) };
        var uvs = new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f) };
        Assert.Null(CloudShadowPattern.CeilingDrift(corners, uvs, new Vector2(0.07f, 0f), 1000f));
    }

    [Fact]
    public void TheWordIsOnWithNothingSetAndOffUnderDet()
    {
        Assert.Equal(CloudShadowSetting.On, CloudShadowSetting.FallbackFor(det: false).Word);
        Assert.Equal(CloudShadowSetting.Off, CloudShadowSetting.FallbackFor(det: true).Word);
        Assert.Equal(CloudShadowSetting.DetRule, CloudShadowSetting.FallbackFor(det: true).Rule);
        // The flag beats the --det fallback, so a probe can ask for the shadows.
        var resolved = CloudShadowSetting.Lookup.Resolve(CloudShadowSetting.On, null, null, CloudShadowSetting.Off);
        Assert.Equal(SettingSource.Flag, resolved.Source);
        Assert.Equal(CloudShadowSetting.On, resolved.Word);
        Assert.Contains(CloudShadowSetting.Lookup, WordSettings.All);
    }
}
