using CSVM.Flight;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The cloud deck's two altitude regimes and the ambient-cloud gate that rides on them
/// (<see cref="WeatherRig.DeckRegime"/>, A7).
///
/// <para>The original's deck is engine trickery: below the <c>CLOUD_COVER</c> band's centre it
/// is a ceiling carried with the camera (which is why its texture looks identical at every
/// altitude on the way up) and both ambient cloud populations are hidden; at or above the centre
/// it is a world-fixed floor at that centre and the clouds render. Two things have to hold for
/// that to be invisible rather than a hard pop, and both are asserted here: the flip is
/// PER CAMERA (splitscreen panes on opposite sides of the band must disagree), and it happens
/// inside the fully-opaque whiteout core, which is <see cref="WeatherState.WhiteoutAmount"/>'s
/// business, not this rule's.</para>
/// </summary>
public class DeckRegimeTests
{
    // C1/IA1's authored CLOUD_COVER: band 970-1124, opaque core 30 m deep centred on 1047.
    private const float C1Bottom = 970f;
    private const float C1Top = 1124f;
    private const float C1Thickness = 30f;
    private const float C1Centre = 1047f;

    [Fact]
    public void BelowTheBandTheDeckIsACeilingCarriedWithTheCamera()
    {
        // The item's whole point: the ceiling's height above the camera — and therefore its
        // apparent texture scale — is the same at every altitude below the band.
        (float low, bool lowClouds) = WeatherRig.DeckRegime(300f, C1Centre);
        (float high, bool highClouds) = WeatherRig.DeckRegime(900f, C1Centre);
        Assert.Equal(600f, high - low, 3);          // exactly the 600 m the camera climbed
        Assert.False(lowClouds);
        Assert.False(highClouds);
    }

    [Fact]
    public void AtAndAboveTheBandCentreTheDeckIsAWorldFixedFloor()
    {
        Assert.Equal(C1Centre, WeatherRig.DeckRegime(C1Centre, C1Centre).DeckY, 3);
        Assert.Equal(C1Centre, WeatherRig.DeckRegime(1192f, C1Centre).DeckY, 3);
        Assert.True(WeatherRig.DeckRegime(C1Centre, C1Centre).CloudsVisible);
        Assert.True(WeatherRig.DeckRegime(1192f, C1Centre).CloudsVisible);
    }

    [Fact]
    public void TheCloudsAreHiddenBelowTheCentreAndShownAbove()
    {
        // "Not even the cloud groups" from underneath — the gate covers both populations, and
        // it is this predicate that decides it for every camera in the session.
        Assert.False(WeatherRig.DeckRegime(C1Centre - 0.5f, C1Centre).CloudsVisible);
        Assert.True(WeatherRig.DeckRegime(C1Centre + 0.5f, C1Centre).CloudsVisible);
    }

    [Fact]
    public void TwoCamerasOnOppositeSidesOfTheBandGetOppositeRegimes()
    {
        // The splitscreen requirement, asserted on the rule itself: a shared field hidden as a
        // NODE would vanish from both panes, so the answer must be a function of one camera's
        // own altitude and nothing else. P1 below, P2 above.
        var p1 = WeatherRig.DeckRegime(900f, C1Centre);
        var p2 = WeatherRig.DeckRegime(1192f, C1Centre);
        Assert.False(p1.CloudsVisible);
        Assert.True(p2.CloudsVisible);
        Assert.NotEqual(p1.DeckY, p2.DeckY);
        // P1's ceiling is above P1, P2's floor is below P2 — the same instant, one world.
        Assert.True(p1.DeckY > 900f);
        Assert.True(p2.DeckY < 1192f);
    }

    [ExtractedDataFact]
    public void TheRegimeFlipHappensInsideTheFullyOpaqueWhiteoutCore()
    {
        // ⚠ This is what makes the deck's altitude JUMP unobservable, and it is asserted against
        // the AUTHORED band rather than a hand-typed one: if C1's CLOUD_COVER or the core's
        // thickness ever reads differently, the flip stops being masked. Should that happen it
        // is a finding about the whiteout band, not a licence to move the flip.
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "IA1"));
        Assert.NotNull(weather);
        Assert.Equal(C1Bottom, weather!.CloudBottom, 3);
        Assert.Equal(C1Top, weather.CloudTop, 3);
        Assert.Equal(C1Thickness, weather.CloudThickness, 3);
        Assert.Equal(C1Centre, weather.CloudBandCentre, 3);
        // The deck flips exactly here, and here the pane is solid whiteout.
        Assert.Equal(1f, weather.WhiteoutAmount(weather.CloudBandCentre), 3);
        Assert.Equal(1f, weather.WhiteoutAmount(weather.CloudBandCentre - 1f), 3);
        Assert.Equal(1f, weather.WhiteoutAmount(weather.CloudBandCentre + 1f), 3);
        // ...and the core really does end, so the three assertions above can fail.
        Assert.True(weather.WhiteoutAmount(weather.CloudBandCentre + (C1Thickness * 0.5f) + 20f) < 1f);
    }
}
