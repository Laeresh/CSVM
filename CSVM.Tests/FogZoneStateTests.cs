using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// B11: the camera weather state (A2) driving which zone's fog the flight wears —
/// <see cref="WeatherState.ZoneForState"/> (state n → <c>ZONE&lt;n&gt;</c>, through
/// <see cref="WeatherState.ResolveZone(string)"/>'s file fallback) and
/// <see cref="WeatherRig.FogStateTrigger"/> (apply on the EDGE, never per frame).
///
/// <para>The two halves are tested apart because they fail apart: a wrong mapping renders the
/// wrong zone's fog, a wrong trigger renders the right zone's fog over and over — and the second
/// is invisible in a screenshot while being exactly what would make the C26 rim annulus shimmer
/// at the boundary (the item's ⚠ trap).</para>
/// </summary>
public class FogZoneStateTests
{
    [Fact]
    public void AStateWithNoAuthoredZoneFallsBackToTheFilesFirstZone()
    {
        var weather = OneZoneFixture();
        Assert.Equal(new[] { "zone1" }, weather.ZoneNames);
        Assert.Equal("zone1", weather.ZoneForState(1));
        // Without the fallback this would answer "zone2", whose Fog() lookup misses and returns
        // NoFog — no fog at all and world light 1.0 (fullbright), the failure the fallback exists
        // to stop. Same for the C5-shaped state 3.
        Assert.Equal("zone1", weather.ZoneForState(2));
        Assert.Equal("zone1", weather.ZoneForState(3));
    }

    [ExtractedDataFact]
    public void ADeckChaptersStatesMapToItsTwoAuthoredZones()
    {
        // C1/IA1 is the item's subject: below the deck (state 1) it must wear ZONE1's 1000-1750 m
        // fog, above it (state 2) ZONE2's 1000-4000 m — the 2.3x below-deck error B11 closes.
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "IA1"));
        Assert.NotNull(weather);
        Assert.Equal("zone1", weather!.ZoneForState(1));
        Assert.Equal("zone2", weather.ZoneForState(2));
        Assert.Equal(1750f, weather.Zone(weather.ZoneForState(1)).FogFar, 1);
        Assert.Equal(4000f, weather.Zone(weather.ZoneForState(2)).FogFar, 1);
        // ...and the state-2 fog is what the flight rendered EVERYWHERE before this item, which is
        // what makes the below-deck half a change and the above-deck half an invariant.
        Assert.Equal(970f, weather.Zone(weather.ZoneForState(1)).FogLow, 1);
        Assert.Equal(4000f, weather.Zone(weather.ZoneForState(2)).FogLow, 1);
    }

    [ExtractedDataFact]
    public void C5sStatesResolveToItsZone1AndZone3()
    {
        // C5 ships ZONE1 + ZONE3 (no ZONE2 anywhere in the chapter), so its states exercise both
        // the direct hit (3 → zone3, C22's business) and the fallback (2 → zone1).
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C5", "IA1"));
        Assert.NotNull(weather);
        Assert.Equal("zone1", weather!.ZoneForState(1));
        Assert.Equal("zone1", weather.ZoneForState(2));
        Assert.Equal("zone3", weather.ZoneForState(3));
    }

    // C22's Verify: "ZONE3 present in all 8 C5 missions — assert from the files, not one." IA1
    // above only pins the mission every golden flies; this walks the other 7 so a mission whose
    // author dropped ZONE3 (or renamed it) cannot hide behind IA1 passing.
    [ExtractedDataTheory]
    [InlineData("IA1")]
    [InlineData("M01")]
    [InlineData("M02")]
    [InlineData("M03")]
    [InlineData("M04")]
    [InlineData("MP1")]
    [InlineData("MP2")]
    [InlineData("MP3")]
    public void EveryC5MissionAuthorsZone1AndZone3(string mission)
    {
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C5", mission));
        Assert.NotNull(weather);
        Assert.Contains("zone1", weather!.ZoneNames);
        Assert.Contains("zone3", weather.ZoneNames);
        Assert.Equal("zone3", weather.ZoneForState(3));
    }

    // C22's core claim: state 3 wears ZONE3's fog (50-250 m, FOG_COLOR [16,16,16]) unchanged from
    // B11's per-state machinery — no clip-range plumbing (the ⚠ trap: ZONE3's CLIP_RANGES far of
    // 300 is NOT applied, per B11's kept divergence that the remake fogs instead of clipping), no
    // extra smoothing (C21's whiteout curtain hides the hard switch). Leaving the volume restores
    // ZONE1 exactly, proving the trigger is symmetric rather than a one-way latch.
    [ExtractedDataFact]
    public void StateThreeFlipAppliesZone3FogAndExitRestoresZone1()
    {
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C5", "IA1"));
        Assert.NotNull(weather);
        // C5's own default resolution (ResolveZone falling back off "zone2") already lands on
        // zone1 — mirroring what WeatherRig.Build hands the trigger as its starting buildZone.
        var trigger = new WeatherRig.FogStateTrigger(stateDriven: true, buildZone: "zone1");

        var into = trigger.Next(3, weather!);
        Assert.NotNull(into);
        Assert.True(into!.Value.Applied);
        Assert.False(into.Value.FellBack);
        Assert.Equal("zone3", into.Value.Zone);
        var zone3Fog = weather!.Zone(into.Value.Zone);
        Assert.Equal(50f, zone3Fog.FogNear, 1);
        Assert.Equal(250f, zone3Fog.FogFar, 1);
        Assert.Equal(16f / 255f, zone3Fog.FogColor.R, 3);
        Assert.Equal(16f / 255f, zone3Fog.FogColor.G, 3);
        Assert.Equal(16f / 255f, zone3Fog.FogColor.B, 3);
        // ZONE3's own SUNLIGHT block (diffuse 1.5 / ambient 0.5) rode along with the fog — the
        // same ApplyZone call writes csky_world_light from this same ZoneWeather record.
        Assert.Equal(1f, zone3Fog.WorldLight, 3);

        var outOf = trigger.Next(1, weather!);
        Assert.NotNull(outOf);
        Assert.True(outOf!.Value.Applied);
        Assert.Equal("zone1", outOf.Value.Zone);
        var zone1Fog = weather!.Zone(outOf.Value.Zone);
        Assert.Equal(1500f, zone1Fog.FogNear, 1);
        Assert.Equal(2250f, zone1Fog.FogFar, 1);
        Assert.Equal(2, trigger.Applications);
    }

    // The ⚠ trap restated as a layering pin: A2's state machine already keeps state 3 from
    // arming outside an armed fog_zone chapter (CameraWeatherState only tests volumes when
    // fogZoneArmed), so no shipped non-C5 mission can ever hand the trigger a literal 3. This
    // test bypasses that gate on purpose — feeding the trigger state 3 directly against a
    // fog_zone-0 chapter's weather (C1, no ZONE3 at all) — to show the SECOND layer holds too:
    // ZoneForState's file fallback lands state 3 back on the chapter's first zone, never on a
    // zone3 that does not exist, so even a hypothetical bypass could not paint ZONE3's fog here.
    [ExtractedDataFact]
    public void AFogZoneZeroChapterNeverAppliesZone3EvenIfStateThreeWereRequested()
    {
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "IA1"));
        Assert.NotNull(weather);
        Assert.DoesNotContain("zone3", weather!.ZoneNames);
        var trigger = new WeatherRig.FogStateTrigger(stateDriven: true, buildZone: "zone1");

        var change = trigger.Next(3, weather);
        Assert.NotNull(change);
        Assert.Equal("zone1", change!.Value.Zone);   // falls back, never "zone3"
        Assert.True(change.Value.FellBack);
        Assert.False(change.Value.Applied);           // already live on zone1 — nothing written
        Assert.Equal(0, trigger.Applications);
    }

    [Fact]
    public void TheTriggerAppliesOnTheEdgeAndNeverAgainWhileTheStateHolds()
    {
        // The ⚠ trap: FUN_00472ea0 runs on a state CHANGE. Re-writing the fog globals every frame
        // would render identically at a static pose and shimmer the C26 rim annulus in motion, so
        // the count is the only thing that can catch it.
        var weather = OneZoneFixture();
        var trigger = new WeatherRig.FogStateTrigger(stateDriven: true, buildZone: "zone2");

        var first = trigger.Next(1, weather);
        Assert.NotNull(first);
        Assert.True(first!.Value.Applied);
        Assert.Equal("zone1", first.Value.Zone);
        Assert.Equal(1, trigger.Applications);

        // Sixty more frames at the same altitude: nothing to say, nothing written.
        for (int frame = 0; frame < 60; frame++)
            Assert.Null(trigger.Next(1, weather));
        Assert.Equal(1, trigger.Applications);
        Assert.Equal("zone1", trigger.Zone);
    }

    [Fact]
    public void AStateChangeThatResolvesToTheLiveZoneWritesNothing()
    {
        // The one-zone fixture climbing through its band: state 1 → 2 is a real edge, but both
        // states resolve to zone1, so the globals must not be touched — the non-deck chapters'
        // "costs nothing, changes nothing" guarantee, asserted on the rule rather than inferred
        // from a golden. The fallback is still REPORTED, once, so a silent no-op is explained.
        var weather = OneZoneFixture();
        var trigger = new WeatherRig.FogStateTrigger(stateDriven: true, buildZone: "zone1");

        var up = trigger.Next(2, weather);
        Assert.NotNull(up);
        Assert.False(up!.Value.Applied);
        Assert.True(up.Value.FellBack);
        Assert.Equal("zone1", up.Value.Zone);
        Assert.Equal(0, trigger.Applications);

        // Down and up again: the fallback line is not repeated.
        Assert.False(trigger.Next(1, weather)!.Value.FellBack);
        Assert.False(trigger.Next(2, weather)!.Value.FellBack);
        Assert.Equal(0, trigger.Applications);
    }

    [Fact]
    public void AnExplicitSkyZoneKeepsTheFogStatic()
    {
        // Decision 5: --sky-zone is a state OVERRIDE. An inspection pose that silently swapped
        // zone with altitude would not be reproducible, and analysis/ repro poses depend on it.
        var weather = OneZoneFixture();
        var trigger = new WeatherRig.FogStateTrigger(stateDriven: false, buildZone: "zone2");

        Assert.Null(trigger.Next(1, weather));
        Assert.Null(trigger.Next(2, weather));
        Assert.Null(trigger.Next(3, weather));
        Assert.Equal(0, trigger.Applications);
        Assert.Equal("zone2", trigger.Zone);
    }

    [ExtractedDataFact]
    public void TheDeckChaptersSwitchBothFogAndWorldLightWhileTheOtherChaptersCannot()
    {
        // The SUNLIGHT_* survey's finding, pinned: the pair is NOT uniformly identical across the
        // deck chapters (6 of 24 deck-chapter missions differ), so WorldLight rides the state too
        // — but C1/IA1, the mission every golden and every A/B pose flies, is one of the identical
        // ones, which is why the item's below-deck change is fog-only in practice.
        var ia1 = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "IA1"));
        Assert.NotNull(ia1);
        Assert.Equal(
            ia1!.Zone(ia1.ZoneForState(1)).WorldLight,
            ia1.Zone(ia1.ZoneForState(2)).WorldLight,
            3);

        var m02 = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "M02"));
        Assert.NotNull(m02);
        // ZONE1 ambient 0.20 / diffuse 1.5 vs ZONE2 0.25 / 1.2 — a real per-state brightness the
        // static resolution could never render.
        Assert.NotEqual(
            m02!.Zone(m02.ZoneForState(1)).WorldLight,
            m02.Zone(m02.ZoneForState(2)).WorldLight,
            3);
    }

    // The zrdr fixture authors ZONE1 only (fixtures/zrdr/weather.json), which is precisely the
    // shape the fallback exists for — a state-2 flip there has no ZONE2 to land on.
    private static WeatherState OneZoneFixture()
    {
        var weather = WeatherState.Load(TestData.Fixture("zrdr"));
        Assert.NotNull(weather);
        return weather!;
    }
}
