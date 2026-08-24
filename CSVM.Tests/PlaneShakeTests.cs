using System;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The wobble-oscillator law (<c>docs/formats/shakes.md</c>): <b>fire is a random-walk accumulator</b>
/// (BL-266(a) branch — per-shot uniform step ±7.54·(factor×caliber), explosion, pulled from an
/// injected <see cref="Random"/> so the trace pins without the engine), the being-hit he_factor
/// doubling, the overspeed gate, and determinism of the whole sum. Input is
/// <c>fixtures/zrdr/shakes.json</c> (probe values under the real source ids; <c>bullet_impact</c>/
/// <c>explosion</c> deliberately absent so missing sources prove to no-op).
/// </summary>
public class PlaneShakeTests
{
    private const float Dt = 1f / 60f;

    // fixture fire_bullet.magnitude_factor = 2e-4; × caliber 40 = 8e-3; × the decoded 7.54 gain
    // (2.0×6.2832×1.2 over the rand half-range) = max single-shot step 6.03e-2 rad.
    private const float MaxSingleStep = 2e-4f * 40f * 7.54f;

    [Fact]
    public void AFiredRoundStepsTheAccumulatorByABoundedRandomWalkStep()
    {
        // One shot from a fixed seed: |step| must be in (0, 7.54·(factor×caliber)] — the decoded
        // uniform ±6.03e-2 law, not the old ±8e-3 sawtooth envelope.
        var shake = NewShake();
        shake.FireBullet(40f);
        float roll = PeakAfter(shake, seconds: 0.02f); // ~1 tick later, before decay eats the step
        Assert.True(roll > 0f && Math.Abs(roll) <= MaxSingleStep * 1.001f,
            $"single kick step out of the decoded bound: {roll} (max {MaxSingleStep})");
    }

    [Fact]
    public void TheKickStepScalesWithMagnitudeAndIsRandomAcrossSeeds()
    {
        // Same event, different seed → the step direction/size differs (it is a random walk, not a
        // fixed sawtooth)
        float a = SingleKickStep(NewShake(seed: 1));
        float b = SingleKickStep(NewShake(seed: 2));
        Assert.NotEqual(a, b);

        // A bigger caliber (or factor) gives a proportionally bigger step: factor×caliber doubles
        // → the step's absolute upper bound doubles.
        var big = NewShake();
        big.FireBullet(80f); // twice the caliber of the wep40 shot
        float bigRoll = PeakAfter(big, seconds: 0.02f);
        Assert.True(Math.Abs(bigRoll) <= MaxSingleStep * 2f * 1.001f,
            $"step did not scale with caliber: {bigRoll}");
    }

    [Fact]
    public void RepeatedShotsAccumulateThenDampDecaysTheWalkBackToZero()
    {
        // A burst injects many steps; letting fire stop lets the authored damp (12.5, τ=80 ms)
        // pull the walk back to rest — the mechanism is bounded, it does not drift monotonic.
        var shake = NewShake();
        float peak = 0f;
        for (int i = 0; i < 30; i++) // ~0.5 s at 60 Hz
        {
            if (i % 8 == 0)
            {
                shake.FireBullet(40f); // ~8 shots/s
            }
            shake.Advance(Dt);
            peak = Math.Max(peak, Math.Abs(shake.Roll));
        }
        Assert.True(peak > MaxSingleStep * 0.5f, $"burst never reached a sizeable walk: {peak}");

        // several damp time-constants after the last shot the walk is essentially at rest
        for (int i = 0; i < 30; i++) // 0.5 s ≈ 6 damp time-constants (τ=80 ms)
        {
            shake.Advance(Dt);
        }
        Assert.True(Math.Abs(shake.Roll) < peak * 0.05f,
            $"walk did not decay to rest: peak {peak}, final {shake.Roll}");
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
    public void ANitroEngageKicksTheAuthoredAbsoluteMagnitudeOnce()
    {
        // fixture nitro.magnitude = 0.05, absolute (no caliber or damage multiplies it), damp 3.
        var shake = NewShake();
        shake.NitroEngaged();
        // One tick in: envelope 0.05·e^(−3/60) on a sawtooth at phase 4/60, |wave| = 0.87.
        float peak = PeakAfter(shake, seconds: 0.02f);
        Assert.InRange(peak, 0.05f * 0.6f, 0.05f * 1.001f);
        // The envelope decays at the authored damp and is gone well before a burn ends.
        Assert.True(MaxAbsRollOver(shake, seconds: 4f) < 0.05f);
        Assert.Equal(0f, MaxAbsRollOver(shake, seconds: 4f));
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
        float[] Run(int seed)
        {
            var shake = NewShake(seed);
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

        Assert.Equal(Run(1), Run(1));       // same seed (fixed) → identical wobble
        Assert.NotEqual(Run(1), Run(2));    // the fire walk is seeded → different mission, different buzz
    }

    private static PlaneShake NewShake() => NewShake(seed: 1);

    private static PlaneShake NewShake(int seed) =>
        new(ShakeDefs.Load(TestData.Fixture("zrdr")), new Random(seed));

    private static float SingleKickStep(PlaneShake shake)
    {
        shake.FireBullet(40f);
        return PeakAfter(shake, seconds: 0.02f);
    }

    private static float PeakAfter(PlaneShake shake, float seconds)
    {
        float max = 0f;
        for (float t = 0f; t < seconds; t += Dt)
        {
            shake.Advance(Dt);
            max = Math.Max(max, Math.Abs(shake.Roll));
        }
        return max;
    }

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
