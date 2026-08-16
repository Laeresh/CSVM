using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Testing;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The knife-edge: no dedicated sag term (the decoded bank→yaw coupling and the weathervane
/// already produce the drift), and <c>wingVert</c> surviving in the nose-chase rate. Decode and
/// measurements: docs/org/flightModel.md's "Bank-independent lift vs the measured knife-edge sag".
/// ⚠ Do not reintroduce a nose-sag term; see that section for why it regresses the onset.
/// The probe recipe lives in <see cref="Probes.KnifeEdge"/>, not restated here.
/// </summary>
public class KnifeEdgeTests
{
    private const float Dt = 1f / 60f;

    private static readonly string[] AllPlanes =
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro",
        "player_avenger", "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel",
        "player_peacemaker",
    };

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>Wings level, nose 45° up, flight path along the nose, stick centred: every torque
    /// in the model is identically zero there, so one step must leave the attitude exactly where
    /// it was. Pins that a retired sag term keyed on wing verticality (docs/org/flightModel.md)
    /// cannot leak back in and rotate the nose here.</summary>
    [Fact]
    public void NothingRotatesTheAttitudeInAWingsLevelPullWithTheStickCentred()
    {
        var m = new FlightModel(Bhawk());
        var entry = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(45f));
        m.Reset(Vector3.Zero, entry, 135f, 1f);
        var before = m.Attitude;
        m.Step(new FlightInput { Throttle = 1f }, Dt);

        float moved = Mathf.RadToDeg((-m.Attitude.Z).AngleTo(-before.Z));
        Assert.True(moved < 1e-4f,
            $"a wings-level nose-up aircraft with its path on the nose must not rotate at all "
            + $"(nose moved {moved:0.0000}°) — a sag term keyed on wing verticality leaks here");
    }

    /// <summary>The discriminating signature: the original's knife-edge drifts for the whole 36 s and
    /// never finds an equilibrium. A bounded sag reaches its bound inside a second, which puts
    /// essentially none of the total sag in the last third of the hold; a genuine drift puts about a
    /// third of it there. Asserted on every airframe, not just the two that were filmed — the
    /// mechanism is the same decoded coupling on all eleven.</summary>
    [ExtractedDataFact]
    public void TheKnifeEdgeNeverSettlesOnAnyAirframe()
    {
        foreach (string plane in AllPlanes)
        {
            var r = Probes.KnifeEdge(ZrdrPath, plane);
            Assert.True(r.Error == null, $"{plane}: {r.Error ?? "-"}");
            foreach (var run in r.Runs)
            {
                Assert.True(run.DriftDegS > 0.2,
                    $"{plane} @{run.EntryMph:0} mph: nose drift {run.DriftDegS:0.00} °/s — the "
                    + "knife-edge must keep sagging (the original drifts 0.69–0.89 °/s)");
                Assert.True(run.SettledFrac > 0.15,
                    $"{plane} @{run.EntryMph:0} mph: only {run.SettledFrac:0.00} of the sag arrived "
                    + "in the last third — that is a bounded sag settling, not the original's drift");
            }
        }
    }

    /// <summary>The nose sits below the flight path throughout, by a margin the chase rate sets.
    /// The −1.8° bound is deliberately well under the original's own 4.8°–8.3° gap: it exists to
    /// catch the chase getting faster, which is what retiring <c>wingVert</c> does. Measurements:
    /// docs/org/flightModel.md's "Bank-independent lift vs the measured knife-edge sag".</summary>
    [ExtractedDataFact]
    public void TheNoseStaysWellBelowTheFlightPath()
    {
        var r = Probes.KnifeEdge(ZrdrPath, "player_bhawk");
        Assert.True(r.Error == null, $"{r.Error ?? "-"}");
        foreach (var run in r.Runs)
        {
            foreach (var x in run.Samples.Where(s => s.T >= 3.0 && s.T <= 12.0))
            {
                Assert.True(x.LagDeg < -1.8,
                    $"@{run.EntryMph:0} mph, +{x.T:0} s: nose is only {-x.LagDeg:0.00}° below the "
                    + "path (original 4.8–6.0°) — the flight path is chasing the nose too hard");
            }
        }
    }

    /// <summary>α at the knife-edge stays under <c>liftAOAs[0]</c> on every airframe, so the
    /// airflow blend never starts faking toward the nose there. Peak runs 0.71–4.29° against the
    /// authored 5° edge, tightest on the Bloodhawk. Refutes the claim (no instrument reproduces it)
    /// that the Balmoral knife-edges 0.1° inside the ramp; see docs/org/flightModel.md.</summary>
    [ExtractedDataFact]
    public void KnifeEdgeAlphaStaysUnderTheLiftAoaWindowOnEveryAirframe()
    {
        foreach (string plane in AllPlanes)
        {
            var stats = PlaneStats.Load(ZrdrPath, plane);
            double edgeDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(stats.LiftAoaCosLo, -1f, 1f)));
            var r = Probes.KnifeEdge(ZrdrPath, plane);
            Assert.True(r.Error == null, $"{plane}: {r.Error ?? "-"}");
            foreach (var run in r.Runs)
            {
                Assert.True(run.AlphaPeak < edgeDeg,
                    $"{plane} @{run.EntryMph:0} mph: α peaks at {run.AlphaPeak:0.00}° against a "
                    + $"liftAOAs low edge of {edgeDeg:0.00}° — the airflow blend engages in a "
                    + "knife-edge, which no measurement of the original supports");
            }
        }
    }

    // The Bloodhawk's real dynamics — the placeholder `PlaneStats()` defaults are the
    // executable's fallback aircraft and carry a different inertia and damping.
    private static PlaneStats Bhawk() => new()
    {
        PitchTorque = 3.3f,
        RollTorque = 7.5f,
        RudderTorque = 2f,
        ReturnRate = 3f,
        AngMomentumDamp = 5f,
        RecInertia = new Vector3(1.18f, 1f, 1.1f),
        FdSpeed = 135f,
        VehWeight = 1900f,
        RefArea = 330f,
        DragFactor = 0.37f,
    };
}
