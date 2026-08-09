using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The original's weathervane: <c>return_rate</c> as a restoring torque that swings the nose onto
/// the velocity vector (<c>crimson.exe</c> <c>FUN_00490f70</c>, <c>0x4916fe</c>–<c>0x4917f0</c>),
/// in place of the extra per-axis damping the remake used to fold it into.
///
/// <para>Everything here reads <see cref="FlightModel.WeathervaneTorque"/> directly, or the body
/// rates after ONE step from rest where the model's arithmetic is a closed form
/// (<c>BodyRates = cmd · dt</c>) — so a sign flip, a missing halving or a leak into roll fails
/// exactly, rather than being absorbed by the integrator a hundred frames later.</para>
///
/// <para>The two properties that carry the item: it must VANISH when the nose is on the flight
/// path (that is what leaves level cruise untouched by construction rather than by scale), and its
/// SIGN must close the misalignment rather than open it — a weathervane with the sign reversed is
/// divergent and would still look plausible in a single frame.</para>
/// </summary>
public class WeathervaneTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void VanishesWhenTheNoseIsOnTheFlightPath()
    {
        var m = Model();
        m.Reset(Vector3.Zero, Basis.Identity, 120f, 1f);

        // Reset puts the velocity down the nose, so the misalignment is identically zero and the
        // torque must be exactly zero — not small. This is the property that keeps every
        // wings-level, zero-α scenario in the pinned envelope untouched.
        Assert.Equal(Vector3.Zero, m.WeathervaneTorque());
    }

    [Fact]
    public void PullsTheNoseBackDownWhenItLeadsThePathUpward()
    {
        // Nose pitched 20° above a level flight path: the torque must be nose-DOWN (negative pitch,
        // since BodyRates.X is + nose-up), and nothing else. A reversed sign here is the failure
        // that would make the model diverge instead of centre.
        var m = Model();
        m.Reset(Vector3.Zero, Pitched(20f), 120f, 1f);
        m.VelocityDir = Vector3.Forward;
        var torque = m.WeathervaneTorque();

        Assert.True(torque.X < 0f, $"nose above the path must pitch DOWN, got {torque.X:0.0000}");
        Assert.True(Mathf.Abs(torque.Y) < 1e-6f, $"a pure pitch offset must not yaw ({torque.Y:0.0000})");
    }

    [Fact]
    public void PullsTheNoseBackTowardThePathWhenItLeadsInYaw()
    {
        // Mirror case on the other axis: nose 20° left of the path yaws RIGHT (negative), and the
        // magnitude matches the pitch case exactly — the torque is isotropic about the nose.
        var m = Model();
        m.Reset(Vector3.Zero, Yawed(20f), 120f, 1f);
        m.VelocityDir = Vector3.Forward;
        var torque = m.WeathervaneTorque();

        Assert.True(torque.Y < 0f, $"nose left of the path must yaw RIGHT, got {torque.Y:0.0000}");
        Assert.True(Mathf.Abs(torque.X) < 1e-6f, $"a pure yaw offset must not pitch ({torque.X:0.0000})");

        var pitchCase = Model();
        pitchCase.Reset(Vector3.Zero, Pitched(20f), 120f, 1f);
        pitchCase.VelocityDir = Vector3.Forward;
        Assert.True(Mathf.IsEqualApprox(torque.Length(), pitchCase.WeathervaneTorque().Length(), 1e-6f),
            "the same misalignment must give the same torque on either axis");
    }

    [Fact]
    public void IsReturnRateTimesHalfTheMisalignmentAngle()
    {
        // The magnitude is the binary's: return_rate × HALF the angle. The original builds the
        // shortest-arc quaternion and converts it through a quaternion-log helper that returns
        // atan2(|q.v|, q.w) — the half angle — and never doubles it back. Reading it as the full
        // misalignment doubles the spring rate, which is exactly the mistake this pins.
        var m = Model();
        m.Reset(Vector3.Zero, Pitched(20f), 120f, 1f);
        m.VelocityDir = Vector3.Forward;

        float expected = Stats().ReturnRate * 0.5f * Mathf.DegToRad(20f);
        Assert.True(Mathf.IsEqualApprox(m.WeathervaneTorque().Length(), expected, 1e-5f),
            $"|torque| {m.WeathervaneTorque().Length():0.000000} vs return_rate·(α/2) {expected:0.000000} "
            + $"(the full angle would give {expected * 2f:0.000000})");
    }

    [Fact]
    public void NeverReachesTheRollAxis()
    {
        // The torque's axis is nose × v̂, which is perpendicular to the nose at every attitude, so
        // its roll component is identically zero — a weathervane cannot bank an aeroplane. This is
        // why roll-360 is untouched by this item on a measurement as well as on a test.
        foreach (float bank in new[] { 0f, 30f, 90f, 150f, 180f })
        {
            var m = Model();
            m.Reset(Vector3.Zero, BankedLeft(bank) * Pitched(15f), 120f, 1f);
            m.VelocityDir = Vector3.Forward;
            Assert.True(Mathf.Abs(m.WeathervaneTorque().Z) < 1e-6f,
                $"bank {bank:0}°: roll component {m.WeathervaneTorque().Z:0.000000} must be zero");
        }
    }

    [Fact]
    public void EntersTheSameAccumulatorAsTheStickAndCarriesRecInertia()
    {
        // One step from rest with the stick centred: BodyRates = cmd·dt·exp(-dt·damp) — the torque
        // scaled by that axis' RecInertia (the only scaling the original applies downstream), THEN
        // this tick's own exponential decay (C24 — the original damps the accumulated total, not
        // just whatever rate was already there, and on the first tick from rest that total IS this
        // tick's torque).
        var stats = Stats();
        var m = Model();
        m.Reset(Vector3.Zero, Pitched(20f), 120f, 1f);
        m.VelocityDir = Vector3.Forward;
        var torque = m.WeathervaneTorque();
        m.Step(default, Dt);

        float decay = Mathf.Exp(-Dt * stats.AngMomentumDamp);
        float expected = torque.X * stats.RecInertia.X * Dt * decay;
        Assert.True(Mathf.IsEqualApprox(m.BodyRates.X, expected, 1e-6f),
            $"pitch rate {m.BodyRates.X:0.000000} vs torque·recInertia.x·dt·exp(-dt·damp) "
            + $"{expected:0.000000} (undamped would give {torque.X * stats.RecInertia.X * Dt:0.000000})");
    }

    [Fact]
    public void DampingNoLongerDependsOnWhetherAStickIsHeld()
    {
        // return_rate is out of the damping coefficient: a spinning aircraft must decay at
        // ang_momentum_damp alone, whether or not the stick is centred. The old model damped a
        // released axis at damp + return_rate, which is the first-order lag this item replaces.
        var stats = Stats();
        var m = Model();
        m.Reset(Vector3.Zero, Basis.Identity, 120f, 1f);
        m.BodyRates = new Vector3(0f, 0f, 1f);
        m.Step(default, Dt);

        // Roll is the axis the weathervane provably cannot reach, so the decay there is the damping
        // term alone and the check is not contaminated by the torque under test. EXPONENTIAL decay
        // (C24) — the linear (1 - damp·dt) form this replaces would give a visibly different number
        // at this damp·dt, printed alongside for contrast.
        float expected = Mathf.Exp(-stats.AngMomentumDamp * Dt);
        Assert.True(Mathf.IsEqualApprox(m.BodyRates.Z, expected, 1e-6f),
            $"roll decayed to {m.BodyRates.Z:0.000000}, expected exp(-ang_momentum_damp·dt) "
            + $"{expected:0.000000} (the linear form would give {1f - (stats.AngMomentumDamp * Dt):0.000000}, "
            + $"damp + return_rate folded in would give {1f - ((stats.AngMomentumDamp + stats.ReturnRate) * Dt):0.000000})");
    }

    /// <summary>The Bloodhawk's real dynamics — the torque is scaled by <c>rec_moments_inertia</c>,
    /// so the placeholder defaults would hide a wrong axis behind near-equal components.</summary>
    private static PlaneStats Stats() => new()
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

    private static FlightModel Model() => new(Stats());

    /// <summary>Nose up by <paramref name="deg"/> — a rotation about the body starboard axis.</summary>
    private static Basis Pitched(float deg) =>
        Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(deg));

    /// <summary>Nose left by <paramref name="deg"/>.</summary>
    private static Basis Yawed(float deg) =>
        Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(deg));

    private static Basis BankedLeft(float deg) =>
        Basis.Identity.Rotated(Vector3.Back, Mathf.DegToRad(deg));
}
