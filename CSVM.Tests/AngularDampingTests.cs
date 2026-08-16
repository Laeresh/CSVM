using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The original's angular damping: an EXPONENTIAL decay of <c>dt · ang_momentum_damp</c>, applied
/// to the WHOLE body rate AFTER this tick's torque is accumulated onto it. The stick, the bank
/// coupling and the weathervane all land in the same accumulator before the decay runs, so the decay
/// carries every one of them, not just whatever rate was carried over from the previous frame.
///
/// <para>That ordering is the point. The alternative this discriminates against — an explicit-Euler
/// linear subtraction, <c>(cmd − BodyRates·damp)·dt</c> — only ever damps the OLD rate, so each
/// tick's own torque contribution goes out undamped until the FOLLOWING tick. <see
/// cref="ThisTicksOwnTorqueIsDampedTooNotJustTheCarriedOverRate"/> is the test a decay-then-add
/// implementation (decay the old state, then add this tick's undamped torque — a different, wrong
/// reading of the same two lines) fails.</para>
///
/// <para>The two forms agree to first order in dt (<c>exp(-x) = 1 - x + O(x^2)</c>), so a small
/// timestep cannot tell them apart — <see
/// cref="ADeliberatelyLargeTimestepStaysBoundedWhereExplicitEulerWouldFlipSignAndGrow"/> is the
/// case that can: past <c>dt·damp = 2</c> the linear factor <c>(1 − dt·damp)</c> goes below −1 and
/// BodyRates flips sign and grows every tick, where <c>exp(−dt·damp)</c> stays in (0, 1) for any
/// dt ≥ 0 and only ever decays.</para>
/// </summary>
public class AngularDampingTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void DecaysAPreExistingRateByExpOfDtTimesDamp()
    {
        // Roll, stick centred, wings level, velocity down the nose: cmd is identically zero on every
        // axis (no stick, no bank coupling, no weathervane misalignment — see BankCouplingTests and
        // WeathervaneTests for each vanishing on its own), so this is a pure decay of whatever rate
        // was already there.
        var stats = Stats();
        var m = new FlightModel(stats);
        m.Reset(Vector3.Zero, Basis.Identity, 120f, 1f);
        m.BodyRates = new Vector3(0f, 0f, 1f);
        m.Step(default, Dt);

        float expected = Mathf.Exp(-Dt * stats.AngMomentumDamp);
        Assert.True(Mathf.IsEqualApprox(m.BodyRates.Z, expected, 1e-6f),
            $"roll decayed to {m.BodyRates.Z:0.000000}, expected exp(-dt*damp) {expected:0.000000} "
            + $"(the linear form would give {1f - stats.AngMomentumDamp * Dt:0.000000})");
    }

    [Fact]
    public void ThisTicksOwnTorqueIsDampedTooNotJustTheCarriedOverRate()
    {
        // Full roll stick from rest. The linear form this discriminates against gives
        // BodyRates = cmd·dt EXACTLY on this first tick (BodyRates starts at 0, so the subtracted
        // damping term is zero) — this tick's own torque goes out completely undamped. The original
        // damps the accumulated total, torque included, so the right answer is cmd·dt·exp(-dt·damp),
        // strictly smaller in magnitude. A build that decays only the carried-over rate before adding
        // this tick's torque (rather than adding then decaying the sum) matches the linear form's
        // reading below and fails this one — that is the ordering mistake the class doc warns about.
        var stats = Stats();
        var m = new FlightModel(stats);
        m.Reset(Vector3.Zero, Basis.Identity, 120f, 1f);
        m.Step(new FlightInput { Roll = 1f }, Dt);

        const float rollTune = 2.12f; // mirrors FlightModel.RollTune
        float cmdZ = stats.RollTorque * stats.RecInertia.Z * rollTune;
        float undamped = cmdZ * Dt;
        float expected = undamped * Mathf.Exp(-Dt * stats.AngMomentumDamp);
        Assert.True(Mathf.IsEqualApprox(m.BodyRates.Z, expected, 1e-6f),
            $"roll rate {m.BodyRates.Z:0.000000} vs cmd·dt·exp(-dt·damp) {expected:0.000000} "
            + $"(undamped cmd·dt alone — the decay-then-add ordering bug — would give {undamped:0.000000})");
    }

    [Fact]
    public void ADeliberatelyLargeTimestepStaysBoundedWhereExplicitEulerWouldFlipSignAndGrow()
    {
        // dt·damp = 5·1 = 5, past the linear form's stability edge at dt·damp = 2. Roll, stick
        // centred, wings level, on-path: cmd is zero, so this is pure decay of a pre-existing rate —
        // the case that actually separates the two forms.
        var stats = Stats();
        var m = new FlightModel(stats);
        m.Reset(Vector3.Zero, Basis.Identity, 120f, 1f);
        m.BodyRates = new Vector3(0f, 0f, 1f);
        const float largeDt = 1f;
        m.Step(default, largeDt);

        float linearFactor = 1f - stats.AngMomentumDamp * largeDt; // -4
        Assert.True(linearFactor < -1f, "the case must actually sit past the linear form's stability edge");

        float expDecay = Mathf.Exp(-stats.AngMomentumDamp * largeDt);
        Assert.True(Mathf.IsEqualApprox(m.BodyRates.Z, expDecay, 1e-6f),
            $"roll rate {m.BodyRates.Z:0.000000} vs exp(-damp·dt) {expDecay:0.000000}");
        // The point: bounded, positive, strictly decayed at any dt — never the sign flip
        // and 4x growth the linear form would have produced here (linearFactor · 1 = -4).
        Assert.True(m.BodyRates.Z is > 0f and < 1f,
            $"roll rate {m.BodyRates.Z:0.000000} must stay in (0, 1) — positive and decayed, unlike "
            + $"the linear form's {linearFactor:0.000000}");
    }

    // The Bloodhawk's real dynamics — see WeathervaneTests/BankCouplingTests for why the
    // placeholder `PlaneStats()` defaults would hide a wrong axis behind near-equal
    // components.
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
}
