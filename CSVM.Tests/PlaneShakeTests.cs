using System;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The wobble-oscillator law (<c>docs/formats/shakes.md</c>): impulse kick + exponential decay,
/// the measured <c>magnitude_factor × caliber</c> amplitude, the <c>he_factor</c> doubling, the
/// overspeed gate, and determinism of the whole sum. Input is <c>fixtures/zrdr/shakes.json</c>
/// (probe values under the real source ids; <c>bullet_impact</c>/<c>explosion</c> deliberately
/// absent so missing sources prove to no-op).
/// </summary>
public class PlaneShakeTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void AFiredRoundKicksTheEnvelopeToFactorTimesCaliberAndDampDecaysIt()
    {
        var shake = NewShake();
        shake.FireBullet(40f);
        float peak = MaxAbsRollOver(shake, seconds: 0.1f);
        // fixture factor 2e-4 × caliber 40 = 8e-3 rad envelope. The envelope decays (τ = 80 ms)
        // while the sawtooth still sweeps toward ±1, so the observed peak sits well under the
        // kick but the same order — the bound pins the magnitude law, not the waveform phase.
        Assert.InRange(peak, 8e-3f * 0.25f, 8e-3f * 1.001f);

        // several damp time-constants later (damp 12.5 → τ = 80 ms) the buzz is gone
        MaxAbsRollOver(shake, seconds: 0.5f);
        float end = MaxAbsRollOver(shake, seconds: 0.2f);
        Assert.True(end < peak * 0.05f, $"envelope did not decay: peak {peak}, end {end}");
    }

    [Fact]
    public void ARepeatKickRaisesTheEnvelopeBackInsteadOfStacking()
    {
        var shake = NewShake();
        shake.FireBullet(40f);
        shake.FireBullet(40f); // same tick: max, not sum
        float peak = MaxAbsRollOver(shake, seconds: 0.1f);
        Assert.True(peak <= 8e-3f * 1.001f, $"kicks stacked: {peak}");
    }

    [Fact]
    public void AHighExplosiveHitDoublesTheMissileMagnitude()
    {
        var he = NewShake();
        he.MissileHit(10f, highExplosive: true);
        var plain = NewShake();
        plain.MissileHit(10f, highExplosive: false);
        float heEnv = MaxAbsRollOver(he, seconds: 0.6f);
        float plainEnv = MaxAbsRollOver(plain, seconds: 0.6f);
        Assert.InRange(heEnv / plainEnv, 1.8f, 2.2f);
    }

    [Fact]
    public void TheOverspeedGateHoldsCruiseSilentAndOpensBeyondRatedMax()
    {
        var shake = NewShake();
        shake.SetSpeedRatio(0.95f); // fast cruise, below the min_speed 1.0 gate
        Assert.Equal(0f, MaxAbsRollOver(shake, seconds: 1f));

        shake.SetSpeedRatio(1.0f);  // exactly at the gate: EXCESS over it, so still zero
        Assert.Equal(0f, MaxAbsRollOver(shake, seconds: 1f));

        shake.SetSpeedRatio(1.2f);  // overspeed dive
        float diving = MaxAbsRollOver(shake, seconds: 1f);
        // envelope settles at EXCESS over the gate / quotient = (1.2-1.0)/70 rad
        float settle = (1.2f - 1.0f) / 70f;
        Assert.InRange(diving, settle * 0.5f, settle * 1.001f);

        shake.SetSpeedRatio(0.5f);  // pull out: the same damp rate bleeds it off, no pop
        Assert.True(MaxAbsRollOver(shake, seconds: 1f) < diving);
        Assert.Equal(0f, MaxAbsRollOver(shake, seconds: 1f));
    }

    [Fact]
    public void AMissingSourceIsANoOpNotACrash()
    {
        var shake = NewShake(); // fixture has no bullet_impact / explosion
        shake.BulletHit(40f);
        shake.ExplosionAt(25f);
        Assert.Equal(0f, MaxAbsRollOver(shake, seconds: 0.5f));
    }

    [Fact]
    public void TheSameEventScriptReplaysToTheSameRollTrace()
    {
        float[] Run()
        {
            var shake = NewShake();
            var trace = new float[120];
            for (int i = 0; i < trace.Length; i++)
            {
                if (i == 10 || i == 40)
                {
                    shake.FireBullet(40f);
                }
                if (i == 60)
                {
                    shake.MissileHit(12f, highExplosive: true);
                }
                shake.SetSpeedRatio(i > 80 ? 1.1f : 0.7f);
                shake.Advance(Dt);
                trace[i] = shake.Roll;
            }
            return trace;
        }

        Assert.Equal(Run(), Run());
    }

    private static PlaneShake NewShake() =>
        new(ShakeDefs.Load(TestData.Fixture("zrdr")));

    private static float MaxAbsRollOver(PlaneShake shake, float seconds)
    {
        float max = 0f;
        for (float t = 0f; t < seconds; t += Dt)
        {
            shake.Advance(Dt);
            max = Math.Max(max, Math.Abs(shake.Roll));
        }
        return max;
    }
}
