using CSVM.Flight;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stall cues are two thresholds on one margin, and the STALL lamp's blink is a rate ramp.
/// Decode and measurements: docs/org/flightModel.md's "The two stall cues" section.
/// <see cref="GaugeCluster.StallLamp"/> is a plain struct; <see cref="Log.Debug"/> falls through
/// the process-wide no-op <c>TestHostLogSink</c>, avoiding the <c>StuntRaceTests</c> crash.
/// ⚠ Everything here is in sim seconds; the capture's wall figures are 1/1.390 of these.
/// </summary>
public class StallWarningTests
{
    // One game frame of the original, in sim ms (33.37 ms wall × 1.390) — the resolution the lamp
    // was measured at, and so the tolerance every period assertion gets.
    private const float GameFrameSimMs = 46.4f;
    private const float SimDt = 1f / 60f;

    [Fact]
    public void TheLampLeadsTheNoseDropOverARealFdBand()
    {
        // Flies the Bloodhawk's real dynamics, not the fallback PlaneStats() defaults, whose own
        // stall speed sits almost at the warn threshold and would invert the split.
        // Only ordering and mechanism are asserted; see docs/org/flightModel.md's stall-cues section.
        var model = new FlightModel(new PlaneStats { VehWeight = 1900f, RefArea = 330f, FdSpeed = 135f });
        float fd = model.Stats.FdSpeed;
        float stallFrac = model.StallSpeed / fd;
        Assert.True(stallFrac < 0.30f,
            $"the lamp must still lead the break: computed stall frac {stallFrac:0.000} vs the fixed warn 0.300");

        void At(float frac, bool warned, bool stalled, string what)
        {
            model.Reset(Vector3.Zero, Basis.Identity, frac * fd, 0f);
            bool w = model.IsStallWarned(), s = model.isStalled();
            Assert.True(w == warned && s == stalled,
                $"{what}: at {frac:0.000} fd (stall frac {stallFrac:0.000}) warned={w} stalled={s}, expected warned={warned} stalled={stalled}");
        }

        At(0.310f, false, false, "above both thresholds neither cue shows");
        At(0.299f, true, false, "the lamp lights at 0.30 fd");
        At((stallFrac + 0.300f) / 2f, true, false, "mid-lead: lamp lit, nose still held");
        At(stallFrac - 0.01f, true, true, "the nose drops at its own computed stall speed");
    }

    [Fact]
    public void TheBlinkLawMatchesCap06sTwoAnchors()
    {
        float half030 = GaugeCluster.StallBlinkHalfPeriodS(0.30f) * 1000f;
        float half015 = GaugeCluster.StallBlinkHalfPeriodS(0.15f) * 1000f;
        Assert.True(Mathf.Abs(half030 - 643f) < GameFrameSimMs,
            $"half-period at the 0.30 fd threshold {half030:0} ms sim vs CAP-06's 643 ms");
        Assert.True(Mathf.Abs(half015 - 296f) < GameFrameSimMs,
            $"half-period at 0.15 fd {half015:0} ms sim vs CAP-06's 296 ms");
        Assert.True(half030 > GaugeCluster.StallBlinkHalfPeriodS(0.25f) * 1000f
                  && GaugeCluster.StallBlinkHalfPeriodS(0.25f) > GaugeCluster.StallBlinkHalfPeriodS(0.20f),
            "the blink shortens monotonically with stall depth");
        Assert.True(Mathf.IsEqualApprox(GaugeCluster.StallBlinkHalfPeriodS(0.02f), GaugeCluster.StallBlinkHalfPeriodS(0.15f)),
            "below the deepest measured speed the law holds instead of extrapolating to a strobe");
    }

    [Fact]
    public void TheIntegratorCarriesTheRampOnItsOwnSimClock()
    {
        var lamp = new GaugeCluster.StallLamp();
        lamp.Advance(warning: true, frac: 0.30f, mph: 0f, simDt: 0f);
        Assert.True(lamp.Lit, "the lamp is lit on the tick the warning arrives");

        // --- 0.30 fd dwell.
        float dwell030 = DwellAt(ref lamp, 0.30f);
        Assert.True(Mathf.Abs(dwell030 - 643f) < GameFrameSimMs + SimDt * 1000f,
            $"a dwell at the threshold spans {dwell030:0} ms sim (CAP-06: 643)");

        // --- 0.15 fd dwell, deep in the stall.
        float dwell015 = DwellAt(ref lamp, 0.15f);
        Assert.True(Mathf.Abs(dwell015 - 296f) < GameFrameSimMs + SimDt * 1000f,
            $"a dwell deep in the stall spans {dwell015:0} ms sim (CAP-06: 296) — the RATE ramped, not the brightness");

        // A fixed-period blink would read the phase off a clock; this one integrates, so a
        // speed change part-way through a dwell shortens the REMAINDER rather than jumping the
        // lamp.
        bool before = lamp.Lit;
        lamp.Advance(true, 0.30f, 0f, SimDt);
        lamp.Advance(true, 0.15f, 0f, SimDt);
        Assert.True(lamp.Lit == before,
            "a mid-dwell speed change does not toggle the lamp, only its remaining time");

        // No hysteresis: the cue follows speed both ways, and clearing it re-arms the lamp lit.
        lamp.Advance(false, 0.15f, 0f, SimDt);
        Assert.False(lamp.Lit, "the lamp is dark once the warning clears");
        lamp.Advance(true, 0.30f, 0f, SimDt);
        Assert.True(lamp.Lit, "re-entering the warning lights the lamp again at once");
    }

    private static float DwellAt(ref GaugeCluster.StallLamp lamp, float frac)
    {
        bool lit = lamp.Lit;
        int steps = 0;
        while (lamp.Lit == lit && steps < 1000)
        {
            lamp.Advance(true, frac, 0f, SimDt);
            steps++;
        }
        return steps * SimDt * 1000f;
    }
}
