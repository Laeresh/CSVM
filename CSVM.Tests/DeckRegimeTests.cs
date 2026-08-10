using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The cloud deck's two altitude regimes (<see cref="WeatherRig.DeckRegime"/>).
///
/// <para>The deck is a world-fixed sheet at the tiles' OWN AUTHORED altitude at every camera
/// altitude — the alternative this discriminates against pins it to the <c>CLOUD_COVER</c> band
/// centre, which for C1 is 1047 against its authored 960. Below the band's centre the sheet wears
/// the overcast's dimmed underside, at or above it its undimmed top. Two things have to hold for
/// that flip to be invisible rather than a hard pop, and both are asserted here: it is PER CAMERA
/// (splitscreen panes on opposite sides of the band must disagree), and it happens inside the
/// fully-opaque whiteout core, which is <see cref="WeatherState.WhiteoutAmount"/>'s business, not
/// this rule's.</para>
///
/// <para>⚠ There is no second, below-band member carrying a ceiling at
/// <c>camera.y + DeckCeilingHeight</c>: the tiles are <c>zone_id 2</c> world meshes the original
/// culls below the deck, and the ceiling the player sees there is <c>horizon/zone1</c>'s own
/// camera-anchored, UV-scrolled dome (<c>HorizonDomeTests</c>). There is no
/// <c>DeckCeilingHeight</c> TUNE either — both of its fits (400 m and 135 m) measured a surface the
/// original does not fog.</para>
///
/// <para>⚠ Whether the two ambient cloud populations RENDER is not this rule's business: it is the
/// original's own <c>zone_id</c> gate, which owns it per camera and reads the zone per chapter from
/// the data, rather than an altitude-keyed special case. Those assertions live in
/// <c>ZoneGateTests</c>, which is also where the case that rules out an altitude rule lives (C2B's
/// <c>zone_id −1</c> fog volumes, which must keep rendering below its deck).</para>
/// </summary>
public class DeckRegimeTests
{
    // C1/IA1's authored CLOUD_COVER: band 970-1124, opaque core 30 m deep centred on 1047.
    private const float C1Bottom = 970f;
    private const float C1Top = 1124f;
    private const float C1Thickness = 30f;
    private const float C1Centre = 1047f;
    // C1/C1C/C2B's deck tiles' own authored altitude (WorldBuilder.CloudDeckAltitude; see
    // docs/formats/fogvol.md) — where the above-band floor sits, rather than at C1Centre. C4's
    // authored 1050 equals its own band centre, so a C4 fixture cannot tell the two rules apart;
    // C1's 87 m gap is why this suite pins C1.
    private const float C1AuthoredDeckY = 960f;

    [Fact]
    public void BelowTheBandTheDeckNoLongerMovesWithTheCamera()
    {
        // The alternative this discriminates against hangs the sheet below the band at
        // `camera.y + DeckCeilingHeight`, as the overcast CEILING — the wrong object. The tiles
        // carry `zone_id 2` and the original culls them outright down here; the ceiling is
        // `horizon/zone1`'s own camera-anchored dome (HorizonDomeTests). So the deck does not move:
        // two cameras 600 m apart below the band get the SAME deck Y, which a camera-relative
        // ceiling makes impossible.
        (float low, bool lowDimmed) = WeatherRig.DeckRegime(300f, C1Centre, C1AuthoredDeckY);
        (float high, bool highDimmed) = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        Assert.Equal(C1AuthoredDeckY, low, 3);
        Assert.Equal(C1AuthoredDeckY, high, 3);
        // ...and it is still the overcast's UNDERSIDE all the way up, so it keeps the mission's
        // SUNLIGHT dimming at every altitude below the band (that face measures 168.9 against the
        // original's 167.7) — the lit variant is the only thing the crossing flips.
        Assert.True(lowDimmed);
        Assert.True(highDimmed);
    }

    [Fact]
    public void AtAndAboveTheBandCentreTheDeckIsAWorldFixedFloorAtTheAuthoredAltitude()
    {
        // The above-band floor sits at the tiles' OWN authored altitude — never re-pinned to the
        // band centre. C1's 960 is 87 m below its band centre of 1047, so an implementation that
        // returned bandCentre here fails this.
        Assert.Equal(C1AuthoredDeckY, WeatherRig.DeckRegime(C1Centre, C1Centre, C1AuthoredDeckY).DeckY, 3);
        Assert.Equal(C1AuthoredDeckY, WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY).DeckY, 3);
    }

    [Fact]
    public void C4sAuthoredAltitudeEqualsItsBandCentreSoItIsUnmovedByTheAuthoredFloor()
    {
        // C4's authored deck altitude (1050) happens to equal its own CLOUD_COVER centre — the
        // coincidence a band-centre pin rides on undetected (see docs/formats/fogvol.md). Pinned
        // here so a C4 golden staying byte-identical under either rule is expected, not a missed
        // regression.
        const float c4Centre = 1050f;
        const float c4AuthoredDeckY = 1050f;
        Assert.Equal(c4AuthoredDeckY, WeatherRig.DeckRegime(c4Centre, c4Centre, c4AuthoredDeckY).DeckY, 3);
        Assert.Equal(c4AuthoredDeckY, WeatherRig.DeckRegime(1400f, c4Centre, c4AuthoredDeckY).DeckY, 3);
    }

    [Fact]
    public void TheDeckKeepsSunlightBelowTheBandAndDropsItAbove()
    {
        // The band crossing flips the deck's own SUNLIGHT dimming off, because the two regimes
        // show two different faces of the overcast. Measured both ways — the original's underside
        // is 167.7 (ours 168.9, dimmed),
        // and no pixel in any original ABOVE-band frame falls below FOG_COLOR 175, which a
        // dimmed 168.9 sheet cannot satisfy at any fog setting.
        Assert.True(WeatherRig.DeckRegime(C1Centre - 0.5f, C1Centre, C1AuthoredDeckY).DeckDimmed);
        Assert.False(WeatherRig.DeckRegime(C1Centre + 0.5f, C1Centre, C1AuthoredDeckY).DeckDimmed);
        // It is the ONLY thing the crossing decides — the sheet stays world-fixed at its
        // authored altitude on both sides, so a camera 292 m under the band and one 145 m over it
        // differ in lit variant and in nothing else.
        var below = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        var above = WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY);
        Assert.True(below.DeckDimmed);
        Assert.False(above.DeckDimmed);
        Assert.Equal(below.DeckY, above.DeckY, 3);
    }

    [Fact]
    public void TwoCamerasOnOppositeSidesOfTheBandGetOppositeRegimes()
    {
        // The splitscreen requirement, asserted on the rule itself: the answer must be a function
        // of one camera's own altitude and nothing else, because each pane holds its own deck
        // copy. P1 below, P2 above.
        var p1 = WeatherRig.DeckRegime(900f, C1Centre, C1AuthoredDeckY);
        var p2 = WeatherRig.DeckRegime(1192f, C1Centre, C1AuthoredDeckY);
        // The sheet's brightness: P1 sees the dimmed underside while P2 sees the undimmed top, at
        // the same instant, of one world's deck — which is why the swap is a per-instance mesh
        // assignment on each rig's own copy and never a shared material.
        Assert.True(p1.DeckDimmed);
        Assert.False(p2.DeckDimmed);
        // Both panes' decks sit at the one authored altitude — P1 under it, P2 over it.
        Assert.Equal(C1AuthoredDeckY, p1.DeckY, 3);
        Assert.Equal(C1AuthoredDeckY, p2.DeckY, 3);
    }

    [ExtractedDataTheory]
    [InlineData("C1", 960f, 970.00f)]
    [InlineData("C1C", 960f, 970.73f)]
    [InlineData("C2B", 960f, 970.00f)]
    [InlineData("C4", 1050f, 1060.00f)]
    public void TheDeckMeshStaysAboutTenMetresBelowTheFvolSlabFloor(
        string chapter, float expectedDeckAltitude, float expectedSlabFloor)
    {
        // The trap: the fvol sprite field is independent of the mesh's WORLD placement above the
        // band (DeckRegime's authoredY branch). The AUTHORED relationship between the two — see
        // docs/formats/fogvol.md: "the deck mesh is the slab's floor... every deck chapter puts its
        // CloudDeck tiles ~10 m BELOW its fvol1-fvol9 slab floor" — is a fact about the extraction,
        // untouched by where WeatherRig.Tick renders the mesh. Read straight off the data here
        // rather than re-typing the table's numbers as a constant, so a change to either reader
        // fails this.
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
