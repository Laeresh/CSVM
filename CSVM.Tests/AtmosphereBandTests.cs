using System;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The original's two-band atmosphere step function and the band its threshold selects.
/// The function compares the aircraft's altitude in feet against a single threshold and takes the
/// dense band at or below it, the thin band above. The threshold is not a shipped initialiser: it
/// lives in the uninitialised tail of <c>.data</c> and a reset routine stores 6561.68 ft into it,
/// so the whole flyable envelope below 2000 m runs on the dense band and the thin band is the
/// regime above that ceiling. Reproduced here in isolation so the band choice, its boundary and
/// the stall arithmetic that discriminates the two are asserted apart from the live plant.
/// Decode and live evidence: docs/org/flightModel.md's Atmosphere section.
/// </summary>
public class AtmosphereBandTests
{
    // The threshold the process holds in flight, in feet: exactly 2000 m converted at 3.28084.
    private const float ThresholdFt = 6561.6796875f;

    private const float SeaLevelDensity = 0.002377f;
    private const float SoundBase = 558f;
    private const float DenseDensityFactor = 0.9544815f;
    private const float DenseSoundFactor = 0.98842078f;
    private const float ThinDensityFactor = 0.057048105f;
    private const float ThinSoundFactor = 0.7348f;

    // The parser's fallback airframe, the case that separates the two bands: no stock aircraft is
    // authored far from this wing loading, so a band that cannot fly it cannot fly any of them.
    private const float FallbackWeightLb = 3500f;
    private const float FallbackRefAreaFt2 = 335f;
    private const float ClMaxStatic = 0.75f;
    private const float ClMaxMach = 0.15f;
    private const float FpsPerMph = 1.4666667f;

    [Fact]
    public void TheThresholdIsTwoThousandMetresExpressedInFeet()
    {
        Assert.Equal(2000f * 3.28084f, ThresholdFt, 3);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1000.0)]
    [InlineData(2952.756)]
    [InlineData(6561.6796875)]
    public void EveryAltitudeAtOrBelowTheThresholdTakesTheDenseBand(double altFt)
    {
        var atm = Atmosphere(altFt, ThresholdFt);
        Assert.Equal(2.2688e-3, atm.Rho, 7);
        Assert.Equal(1109.5, atm.SoundFps, 1);
    }

    [Theory]
    [InlineData(6561.68)]
    [InlineData(10000.0)]
    public void EveryAltitudeAboveTheThresholdTakesTheThinBand(double altFt)
    {
        var atm = Atmosphere(altFt, ThresholdFt);
        Assert.Equal(1.3560e-4, atm.Rho, 8);
        Assert.Equal(968.0, atm.SoundFps, 1);
    }

    [Fact]
    public void TheDenseBandIsTheOneTheFlightModelCarries()
    {
        var atm = Atmosphere(0.0, ThresholdFt);
        Assert.Equal(2.2688e-3, atm.Rho, 7);
        Assert.Equal(1109.5, atm.SoundFps, 1);
    }

    [Fact]
    public void OnlyTheDenseBandFliesTheFallbackAirframe()
    {
        var dense = Atmosphere(0.0, ThresholdFt);
        var thin = Atmosphere(ThresholdFt + 1.0, ThresholdFt);
        double denseMph = StallFps(dense.Rho, dense.SoundFps) / FpsPerMph;
        double thinMph = StallFps(thin.Rho, thin.SoundFps) / FpsPerMph;

        Assert.InRange(denseMph, 72.0, 80.0);
        Assert.True(thinMph > 4.0 * denseMph, $"thin={thinMph:0.0} mph dense={denseMph:0.0} mph");
    }

    // A zero threshold is the reading a byte-level look at the uninitialised slot produces. It has
    // to change the answer at every altitude a stock aircraft reaches, or the live read of the
    // threshold would not be the thing that settles the band.
    [Fact]
    public void AZeroThresholdWouldPutEveryAirborneAircraftOnTheThinBand()
    {
        Assert.Equal(2.2688e-3, Atmosphere(0.0, 0.0).Rho, 7);
        Assert.Equal(1.3560e-4, Atmosphere(1.0, 0.0).Rho, 8);
        Assert.Equal(1.3560e-4, Atmosphere(3000.0, 0.0).Rho, 8);
    }

    /// <summary>Density in slug/ft³ and speed of sound in ft/s at an altitude in feet.</summary>
    private static (double Rho, double SoundFps) Atmosphere(double altFt, double thresholdFt)
    {
        bool dense = altFt <= thresholdFt;
        double densityFactor = dense ? DenseDensityFactor : ThinDensityFactor;
        double soundFactor = dense ? DenseSoundFactor : ThinSoundFactor;
        return (densityFactor * SeaLevelDensity, (soundFactor + 1.0) * SoundBase);
    }

    /// <summary>The speed in ft/s at which the aerodynamic ceiling stops delivering 1 G.</summary>
    private static double StallFps(double rho, double soundFps)
    {
        double lo = 1.0;
        double hi = 4000.0;
        for (int i = 0; i < 200; i++)
        {
            double v = 0.5 * (lo + hi);
            double clMax = Math.Max(0.0, ClMaxStatic - (ClMaxMach * (v / soundFps)));
            double lift = clMax * 0.5 * rho * v * v * FallbackRefAreaFt2;
            if (lift < FallbackWeightLb)
            {
                lo = v;
            }
            else
            {
                hi = v;
            }
        }

        return 0.5 * (lo + hi);
    }
}
