using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The cloud deck's two altitude regimes (<see cref="WeatherRig.DeckRegime"/>, A7).
///
/// <para>The original's deck is engine trickery: below the <c>CLOUD_COVER</c> band's centre it
/// is a ceiling carried with the camera (which is why its texture looks identical at every
/// altitude on the way up) and the sheet is the overcast's dimmed underside; at or above the
/// centre it is a world-fixed floor at the tiles' OWN AUTHORED altitude (B13, superseding A7's
/// band-centre pin — C1's authored 960 vs its former 1047 pin) and the sheet is its undimmed top.
/// Two things have to hold for that to be invisible rather than a hard pop, and both are asserted
/// here: the flip is PER CAMERA (splitscreen panes on opposite sides of the band must disagree),
/// and it happens inside the fully-opaque whiteout core, which is
/// <see cref="WeatherState.WhiteoutAmount"/>'s business, not this rule's.</para>
///
/// <para>⚠ The rule's third member — whether the two ambient cloud populations RENDER — is gone
/// as of B12 (<c>PLAN-weather-decompile-match</c>): it was A7's altitude-keyed special case of the
/// original's own <c>zone_id</c> gate, which now owns it per camera and reads the zone per chapter
/// from the data. Those assertions moved to <c>ZoneGateTests</c>, which is also where the case
/// that killed the altitude rule lives (C2B's <c>zone_id −1</c> fog volumes, which must keep
/// rendering below its deck).</para>
/// </summary>
public class DeckRegimeTests
{
    // C1/IA1's authored CLOUD_COVER: band 970-1124, opaque core 30 m deep centred on 1047.
    private const float C1Bottom = 970f;
    private const float C1Top = 1124f;
    private const float C1Thickness = 30f;
    private const float C1Centre = 1047f;
    // C1/C1C/C2B's deck tiles' own authored altitude (WorldBuilder.CloudDeckAltitude,
    // docs/formats/fogvol.md's A6 table) — what B13 places the above-band floor at, instead of
    // C1Centre (A7's pin, which was C4's own coincidence: C4's authored 1050 equals its own
    // centre, so a C4 fixture would not tell the two apart — C1's 87 m gap is why this suite
    // pins C1).
    private const float C1AuthoredDeckY = 960f;

    [Fact]
    public void BelowTheBandTheDeckIsACeilingCarriedWithTheCamera()
    {
        // The item's whole point: the ceiling's height above the camera — and therefore its
        // apparent texture scale — is the same at every altitude below the band.
        (float low, bool lowDimmed) = WeatherRig.DeckRegime(300f, C1Centre, C1AuthoredDeckY);
        (float high, bool highDimmed) = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        Assert.Equal(600f, high - low, 3);          // exactly the 600 m the camera climbed
        // ...and it is the overcast's UNDERSIDE all the way up, so it keeps the mission's
        // SUNLIGHT dimming at every altitude below the band (C22 measured that face at 168.9
        // against the original's 167.7).
        Assert.True(lowDimmed);
        Assert.True(highDimmed);
    }

    [Fact]
    public void AtAndAboveTheBandCentreTheDeckIsAWorldFixedFloorAtTheAuthoredAltitude()
    {
        // B13, 2026-08-09: the above-band floor sits at the tiles' OWN authored altitude — never
        // re-pinned to the band centre (A7's pin, superseded). C1's 960 is 87 m below its former
        // 1047 pin; an implementation that still returned bandCentre here would fail this.
        Assert.Equal(C1AuthoredDeckY, WeatherRig.DeckRegime(C1Centre, C1Centre, C1AuthoredDeckY).DeckY, 3);
        Assert.Equal(C1AuthoredDeckY, WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY).DeckY, 3);
    }

    [Fact]
    public void C4sAuthoredAltitudeEqualsItsBandCentreSoItIsUnmovedByTheAuthoredFloor()
    {
        // C4's authored deck altitude (1050) happens to equal its own CLOUD_COVER centre — the
        // coincidence A7's band-centre pin rode without anyone noticing it was a coincidence
        // (docs/formats/fogvol.md's A6 table). Pinned here so a C4 golden staying byte-identical
        // across this item is expected, not a missed regression.
        const float c4Centre = 1050f;
        const float c4AuthoredDeckY = 1050f;
        Assert.Equal(c4AuthoredDeckY, WeatherRig.DeckRegime(c4Centre, c4Centre, c4AuthoredDeckY).DeckY, 3);
        Assert.Equal(c4AuthoredDeckY, WeatherRig.DeckRegime(1400f, c4Centre, c4AuthoredDeckY).DeckY, 3);
    }

    [Fact]
    public void TheDeckKeepsSunlightBelowTheBandAndDropsItAbove()
    {
        // C23's fork, resolved M-a: the same crossing that flips ceiling→floor flips the deck's
        // own SUNLIGHT dimming off, because the two regimes show two different faces of the
        // overcast. Measured both ways — the original's underside is 167.7 (ours 168.9, dimmed),
        // and no pixel in any original ABOVE-band frame falls below FOG_COLOR 175, which a
        // dimmed 168.9 sheet cannot satisfy at any fog setting.
        Assert.True(WeatherRig.DeckRegime(C1Centre - 0.5f, C1Centre, C1AuthoredDeckY).DeckDimmed);
        Assert.False(WeatherRig.DeckRegime(C1Centre + 0.5f, C1Centre, C1AuthoredDeckY).DeckDimmed);
        // It is the SAME predicate as the floor/ceiling choice — one crossing, two consequences,
        // so nothing can flip one of them a metre early.
        var below = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        var above = WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY);
        Assert.True(below.DeckDimmed);
        Assert.True(below.DeckY > 900f);            // a ceiling
        Assert.False(above.DeckDimmed);
        Assert.True(above.DeckY < 1192f);           // a floor
    }

    [Fact]
    public void TwoCamerasOnOppositeSidesOfTheBandGetOppositeRegimes()
    {
        // The splitscreen requirement, asserted on the rule itself: the answer must be a function
        // of one camera's own altitude and nothing else, because each pane holds its own deck
        // copy. P1 below, P2 above.
        var p1 = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        var p2 = WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY);
        Assert.NotEqual(p1.DeckY, p2.DeckY);
        // Including the sheet's brightness: P1 sees the dimmed underside while P2 sees the
        // undimmed top, at the same instant, of one world's deck — which is why the swap is a
        // per-instance mesh assignment on each rig's own copy and never a shared material.
        Assert.True(p1.DeckDimmed);
        Assert.False(p2.DeckDimmed);
        // P1's ceiling is above P1, P2's floor is below P2 — the same instant, one world.
        Assert.True(p1.DeckY > 900f);
        Assert.True(p2.DeckY < 1192f);
    }

    [ExtractedDataTheory]
    [InlineData("C1", 960f, 970.00f)]
    [InlineData("C1C", 960f, 970.73f)]
    [InlineData("C2B", 960f, 970.00f)]
    [InlineData("C4", 1050f, 1060.00f)]
    public void TheDeckMeshStaysAboutTenMetresBelowTheFvolSlabFloor(
        string chapter, float expectedDeckAltitude, float expectedSlabFloor)
    {
        // B13's own trap: the fvol sprite field does NOT move with this item — only the mesh's
        // WORLD placement above the band did (DeckRegime's new authoredY branch). The AUTHORED
        // relationship between the two (docs/formats/fogvol.md's A6 table: "the deck mesh is the
        // slab's floor... every deck chapter puts its CloudDeck tiles ~10 m BELOW its fvol1-fvol9
        // slab floor") is a fact about the extraction, untouched by where WeatherRig.Tick renders
        // the mesh — read straight off the data here rather than re-typing the table's numbers as
        // a constant, so a change to either reader would fail this.
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));

        float? deckAltitude = WorldBuilder.CloudDeckAltitudeOf(gamez);
        Assert.Equal(expectedDeckAltitude, deckAltitude);

        // The map-spanning slab's own floor: every top-anchored box volume (the slab pieces, not
        // C1C's build-up frusta) shares one bottom Y — asserted, not assumed.
        float? slabFloor = null;
        foreach (var volume in FogVolumeSpec.VolumesOf(gamez))
        {
            if (!volume.IsAxisAlignedBox())
                continue;
            float y = volume.Box.Position.Y;
            if (slabFloor is { } got)
                Assert.Equal(got, y, 2);
            else
                slabFloor = y;
        }
        Assert.NotNull(slabFloor);
        Assert.Equal(expectedSlabFloor, slabFloor!.Value, 2);
        Assert.True(slabFloor > deckAltitude, "the mesh must sit below the slab, not inside it");
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
