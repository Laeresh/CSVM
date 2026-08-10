using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Testing;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The knife-edge, and the two things settled about it.
///
/// <para><b>1. The sag has no dedicated term, and must not grow one.</b> The tempting alternative
/// is a bounded nose-sag term in <see cref="FlightModel.Step"/> keyed on wing verticality, on the
/// reasoning that nothing else in the model drops the nose at 90° of bank. The decoded bank→yaw
/// coupling and the weathervane do it — at 90° bank the body yaw axis is horizontal, so a yaw rate
/// IS a nose sag — and they do it in the original's own shape, a drift with no equilibrium, which a
/// bounded term cannot. They also do it BETTER at the onset such a term would be fitted to: the
/// original's +3 s sample reads −4.9° and the decoded mechanism alone gives −4.94°, against −7.28°
/// with a bounded step stacked on top.</para>
///
/// <para><b>2. <c>wingVert</c> survives in the nose-chase, on a measurement.</b> The decode says
/// lift does not depend on bank, and lift does not read <c>wingVert</c> at all. Its one remaining
/// reader is the rate at which the flight path chases the nose, which the original has no
/// counterpart for — so the decode is silent there and the footage is not: the original holds its
/// nose 4.8° → 8.3° BELOW its flight path through a 36 s knife-edge, and that gap is what the chase
/// rate sets. <see cref="TheNoseStaysWellBelowTheFlightPath"/> is the able-to-fail form of that:
/// with <c>wingVert</c> retired (chase floor 1.0, the bank-independent reading) that gap collapses
/// and the test fails.</para>
///
/// <para>The recipe itself lives in <see cref="Probes.KnifeEdge"/> — code rather than prose, so it
/// cannot go missing.</para>
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

    /// <summary>Wings level, nose 45° up, flight path along the nose, stick centred: every torque in
    /// the model is identically zero there — the bank coupling keys off bank, the weathervane off
    /// nose-versus-path — so one step must leave the attitude EXACTLY where it was.
    ///
    /// <para>This is the dedicated sag term's own footprint, and the reason there is none. Keyed on
    /// <c>1 − |bodyUp·up|</c>, which is 0.29 at a 45° nose-up attitude with the wings dead level, it
    /// rotates the nose down here at up to 11.5 °/s — a nose-down bias in every pull, at any bank,
    /// that nothing in the original authorises. This pins that no such leak exists.</para></summary>
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

    /// <summary>The nose sits BELOW the flight path throughout, by a margin the chase rate sets. The
    /// original's own gap is 4.8° at +3 s GROWING to 8.3° at +36 s; ours is 2.2–2.9° over the same
    /// early span and shrinks after, so the bound below is deliberately well under the footage — it
    /// is here to catch the chase getting FASTER, which is what retiring <c>wingVert</c> does: the
    /// bank-independent chase reads 1.86°/1.67° (143 mph) and 1.53°/1.35° (300 mph) at these two
    /// samples — three of the four below the bound — while its 36 s altitude loss rises
    /// 1087 → 1334 m against a measured 540.</summary>
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

    /// <summary>α at the knife-edge, measured — against the claim this discriminates against, that
    /// "the Balmoral knife-edges at α = 5.1°, 0.1° inside the liftAOAs ramp", which no instrument
    /// reproduces. α here is an emergent alignment lag, and what matters about it is whether it crosses
    /// <c>liftAOAs[0]</c> — past that edge the airflow starts being faked toward the nose and the
    /// lift demand changes character. It does not, on any airframe: the peak runs 0.71–4.29°
    /// against the authored 5°, which `liftAOAs` sets globally in `player.json` and is therefore
    /// the same edge for all eleven. The Balmoral is not the tight one — it peaks at 1.77°, a
    /// margin of ≈3.2°, against the quoted 0.1°; the tightest of the eleven is the BLOODHAWK at
    /// 4.29°, ≈0.71° clear.</summary>
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

    /// <summary>The Bloodhawk's real dynamics — the placeholder <c>PlaneStats()</c> defaults are the
    /// executable's fallback aircraft and carry a different inertia and damping.</summary>
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
