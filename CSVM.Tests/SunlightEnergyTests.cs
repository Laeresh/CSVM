using CSVM.Flight;
using CSVM.Session;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The enhanced-mode mapping from a zone's authored SUNLIGHT to Godot energies
/// (<see cref="WeatherRig.EnhancedEnergies"/>), and the parse that carries the two authored
/// scalars through uncollapsed beside the faithful path's <c>WorldLight</c> scalar.
///
/// The mapping is pinned here rather than at the controls because its failure mode is silent: a
/// factor that drifts still renders, just at the wrong level in every mission at once.
/// </summary>
public class SunlightEnergyTests
{
    // The anchor the two factors were chosen on: the install's modal day zone must land on the
    // energies the faithful path hardcodes (Launcher.SetupLighting: 1.6 sun, 0.9 ambient), so
    // enhanced mode leaves a day mission at the level it already had.
    [Fact]
    public void TheModalDayZoneLandsOnTheHardcodedEnergies()
    {
        (float sun, float ambient) = WeatherRig.EnhancedEnergies(Zone(diffuse: 1.5f, ambient: 0.5f));
        Assert.Equal(1.6f, sun, 1);
        Assert.Equal(0.9f, ambient, 2);
    }

    // The point of the item: a night zone's authored values must resolve dimmer than a day one's
    // from the data alone, with no per-chapter special case.
    [Fact]
    public void ANightZoneResolvesDimmerThanADayZone()
    {
        (float nightSun, float nightAmbient) = WeatherRig.EnhancedEnergies(Zone(0.6f, 0.15f));
        (float daySun, float dayAmbient) = WeatherRig.EnhancedEnergies(Zone(1.5f, 0.5f));
        Assert.True(nightSun < daySun * 0.5f);
        Assert.True(nightAmbient < dayAmbient * 0.5f);
        Assert.Equal(0.64f, nightSun, 2);
        Assert.Equal(0.27f, nightAmbient, 2);
    }

    // C5 is the case the night cap exists for: a black FOG_COLOR over the install's modal DAY
    // SUNLIGHT pair, which without the cap lights a night city at noon level.
    [Fact]
    public void ADayLevelPairUnderABlackSkyCapsToTheNightPair()
    {
        var night = Zone(diffuse: 1.5f, ambient: 0.5f, fog: Colors.Black);
        Assert.True(WeatherRig.IsNightZone(night));
        (float sun, float ambient) = WeatherRig.EnhancedEnergies(night);
        Assert.Equal(0.64f, sun, 2);
        Assert.Equal(0.27f, ambient, 2);
    }

    // The cap is a ceiling, not a replacement: a zone already authored dimmer than the night pair
    // keeps its own values, so C1B's 0.6 / 0.15 renders exactly as before.
    [Fact]
    public void AZoneAuthoredDimmerThanTheCapKeepsItsOwnValues()
    {
        (float sun, float ambient) = WeatherRig.EnhancedEnergies(Zone(0.6f, 0.15f, Colors.Black));
        Assert.Equal(0.64f, sun, 2);
        Assert.Equal(0.27f, ambient, 2);
    }

    // The separator has to hold on the install's own two populations, not on invented colours:
    // C5's night zone authors a black fog and C4's day zone a 0.75 grey one.
    [ExtractedDataFact]
    public void TheInstallsNightAndDayFogColoursFallOnOppositeSidesOfTheRule()
    {
        var c5 = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C5", "IA1"));
        var c4 = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C4", "IA1"));
        Assert.NotNull(c5);
        Assert.NotNull(c4);
        Assert.True(WeatherRig.IsNightZone(c5!.Zone("zone1")));
        Assert.True(WeatherRig.IsNightZone(c5!.Zone("zone3")));
        Assert.False(WeatherRig.IsNightZone(c4!.Zone("zone1")));
        // And the cap is what C5 gets out of it, from the same day-level pair C4 keeps.
        Assert.Equal(0.64f, WeatherRig.EnhancedEnergies(c5!.Zone("zone1")).Sun, 2);
        Assert.Equal(1.6f, WeatherRig.EnhancedEnergies(c4!.Zone("zone1")).Sun, 1);
    }

    // The plumbing: SUNLIGHT_DIFFUSE and SUNLIGHT_AMBIENT reach the record uncollapsed. Before
    // this they existed only inside WorldLightFactor's clamp, which is why the launcher's
    // hardcoded energies could not be replaced by data.
    [ExtractedDataFact]
    public void C1BsNightZoneCarriesItsAuthoredDiffuseAndAmbient()
    {
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1B", "IA1"));
        Assert.NotNull(weather);
        var zone = weather!.Zone("zone1");
        Assert.Equal(0.6f, zone.SunDiffuse, 2);
        Assert.Equal(0.15f, zone.SunAmbient, 2);
        // The faithful scalar is untouched by the new fields: ambient + diffuse * 0.46, clamped.
        Assert.Equal(0.426f, zone.WorldLight, 3);
    }

    // C4 is the one chapter whose SUNLIGHT colours are authored away from white (warm sun, cold
    // ambient), so the colour half of the block has a case that can actually fail.
    [ExtractedDataFact]
    public void C4sZoneCarriesItsAuthoredSunlightColours()
    {
        var weather = WeatherState.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C4", "IA1"));
        Assert.NotNull(weather);
        var zone = weather!.Zone("zone1");
        Assert.Equal(1.0f, zone.SunColorDiffuse.R, 2);
        Assert.Equal(0.8f, zone.SunColorDiffuse.G, 2);
        Assert.Equal(0.7f, zone.SunColorDiffuse.B, 2);
        Assert.Equal(0.7f, zone.SunColorAmbient.R, 2);
        Assert.Equal(0.9f, zone.SunColorAmbient.G, 2);
        Assert.Equal(1.0f, zone.SunColorAmbient.B, 2);
    }

    // The default fog is the install's modal DAY colour, so a case that says nothing about the
    // sky gets the day arm of the night rule.
    private static WeatherState.ZoneWeather Zone(float diffuse, float ambient, Color? fog = null)
        => new(fog ?? Colors.Gray, 1000f, 2000f, 0f, 1f, 2050f, 1f, Vector3.Zero,
            diffuse, ambient, Colors.White, Colors.White);
}
