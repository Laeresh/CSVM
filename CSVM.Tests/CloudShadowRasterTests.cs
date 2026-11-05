using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Enhanced cloud shadows' map over a synthetic cloud set. A card at a known place under a known sun
/// darkens the ground its sun rays reach and nothing else. A clear sky casts nothing. The shadow's
/// strength follows the card's authored opacity and mask. The word is on with nothing set and off
/// under <c>--det</c>.
/// </summary>
public class CloudShadowRasterTests
{
    private static readonly Vector3 Overhead = Vector3.Up;

    // A sun 30 degrees up, toward +X: its ray rises one metre per sqrt(3) metres it runs.
    private static readonly Vector3 LowEast = new(Mathf.Cos(Mathf.DegToRad(30f)), Mathf.Sin(Mathf.DegToRad(30f)), 0f);

    [Fact]
    public void ACardOverheadDarkensTheGroundStraightBelowIt()
    {
        var map = Cast(Overhead, Card(new Vector3(1000f, 1200f, -500f), 100f));
        var run = CloudShadowRaster.Run(Overhead);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(1000f, 0f, -500f), run), 3);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(1030f, 250f, -470f), run), 3);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(1200f, 0f, -500f), run), 3);
        Assert.Equal(1f - CloudShadowRaster.Darkness, CloudShadowRaster.Sun(0f), 5);
    }

    [Fact]
    public void ASlantedSunCastsTheCardAwayFromItAndOntoHighGroundSooner()
    {
        var centre = new Vector3(0f, 1000f, 0f);
        var map = Cast(LowEast, Card(centre, 100f));
        var run = CloudShadowRaster.Run(LowEast);
        float reach = 1000f * Mathf.Sqrt(3f);
        // At sea level the shadow lies 1732 m from below the card, away from the sun.
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(-reach, 0f, 0f), run), 3);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(0f, 0f, 0f), run), 3);
        // A hill 400 m high meets the same rays 400 * sqrt(3) m nearer the card.
        float hill = (1000f - 400f) * Mathf.Sqrt(3f);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(-hill, 400f, 0f), run), 3);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(-reach, 400f, 0f), run), 3);
    }

    [Fact]
    public void ASlantedSunStretchesTheShadowAlongItsBearingOnly()
    {
        // The card faces the sun, so at 30 degrees its height lies twice as long on the ground.
        var map = Cast(LowEast, Card(new Vector3(0f, 1000f, 0f), 100f));
        var run = CloudShadowRaster.Run(LowEast);
        float foot = -1000f * Mathf.Sqrt(3f);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(foot + 85f, 0f, 0f), run), 3);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(foot - 85f, 0f, 0f), run), 3);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(foot, 0f, 85f), run), 3);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(foot + 120f, 0f, 0f), run), 3);
    }

    [Fact]
    public void AClearSkyCastsNothing()
    {
        Assert.Null(CloudShadowRaster.Cast(new List<CloudShadowRaster.Caster>(), Overhead));
        Assert.Null(CloudShadowRaster.Cast(new[] { Card(Vector3.Up * 900f, 100f, opacity: 0f) }, Overhead));
        Assert.Equal(1f, CloudShadowRaster.Sun(1f));
    }

    [Fact]
    public void ASunNearTheHorizonCastsNothing()
    {
        var grazing = new Vector3(1f, 0.05f, 0f);
        Assert.Null(CloudShadowRaster.Cast(new[] { Card(Vector3.Up * 900f, 100f) }, grazing));
        Assert.Equal(Vector2.Zero, CloudShadowRaster.Run(grazing));
    }

    [Fact]
    public void TheShadowScalesWithTheCardsAuthoredOpacity()
    {
        var below = new Vector3(0f, 0f, 0f);
        var run = CloudShadowRaster.Run(Overhead);
        float faint = CloudShadowRaster.Through(Cast(Overhead, Card(Vector3.Up * 900f, 100f, opacity: 0.25f)), below, run);
        float half = CloudShadowRaster.Through(Cast(Overhead, Card(Vector3.Up * 900f, 100f, opacity: 0.5f)), below, run);
        Assert.Equal(0.75f, faint, 3);
        Assert.Equal(0.5f, half, 3);
        Assert.True(CloudShadowRaster.Sun(half) < CloudShadowRaster.Sun(faint));
    }

    [Fact]
    public void OverlappingCardsMultiplyWhatTheyLetThrough()
    {
        var map = Cast(Overhead, Card(Vector3.Up * 900f, 100f, opacity: 0.5f), Card(Vector3.Up * 1100f, 100f, opacity: 0.5f));
        Assert.Equal(0.25f, CloudShadowRaster.Through(map, Vector3.Zero, CloudShadowRaster.Run(Overhead)), 3);
    }

    [Fact]
    public void TheMaskShapesTheShadow()
    {
        // Clear on the card's left half, opaque on its right. Under an overhead sun the card's right is +X.
        var mask = new CloudShadowRaster.Mask(2, 1, new[] { 0f, 1f });
        var map = Cast(Overhead, new CloudShadowRaster.Caster(Vector3.Up * 900f, 100f, 1f, mask));
        var run = CloudShadowRaster.Run(Overhead);
        Assert.Equal(1f, CloudShadowRaster.Through(map, new Vector3(-40f, 0f, 0f), run), 2);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(40f, 0f, 0f), run), 2);
    }

    [Fact]
    public void AWideFieldTakesLargerTexelsWithinTheSideCap()
    {
        var map = Cast(Overhead, Card(new Vector3(-20000f, 1000f, 0f), 100f), Card(new Vector3(20000f, 1000f, 0f), 100f));
        Assert.InRange(map.Width, 1, CloudShadowRaster.MaxSide);
        Assert.True(map.Cell > CloudShadowRaster.MinCell);
        Assert.Equal(0f, CloudShadowRaster.Through(map, new Vector3(20000f, 0f, 0f), CloudShadowRaster.Run(Overhead)), 3);
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

    private static CloudShadowRaster.Caster Card(Vector3 centre, float size, float opacity = 1f) =>
        new(centre, size, opacity, CloudShadowRaster.Mask.Solid);

    private static CloudShadowRaster.Result Cast(Vector3 sun, params CloudShadowRaster.Caster[] casters)
    {
        var map = CloudShadowRaster.Cast(casters, sun);
        Assert.NotNull(map);
        return map!;
    }
}
