using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The plane wobble as the original renders it (<c>docs/org/shakes.md</c>). Seven component
/// blocks each take a random velocity kick (pitch and yaw ×1.2, roll ×2.5) and integrate it at a
/// 1/150 s substep. Their sum turns the node as a rotation vector at twice its length, in YXZ.
/// Input is <c>fixtures/zrdr/shakes.json</c>, probe values under the real source ids. Expected
/// kicks replay the seeded <see cref="Random"/> the shake draws from, so each pin is hand-computed.
/// </summary>
public class PlaneShakeTests
{
    private const float Dt = 1f / 60f;

    // fixture fire_bullet: magnitude_factor 2e-4, frequency 15, sawtooth; a 40-calibre round.
    private const float FireStep = 2e-4f * 40f * 15f * 4f;

    [Fact]
    public void TheNodeRotationIsTheSummedPositionAtTwiceItsLength()
    {
        // One axis at a time the chain is exact: a rotation of 2|s| about that axis.
        AssertVec(new Vector3(0.2f, 0f, 0f), PlaneShake.NodeRotation(new Vector3(0.1f, 0f, 0f)));
        AssertVec(new Vector3(0f, 0.2f, 0f), PlaneShake.NodeRotation(new Vector3(0f, 0.1f, 0f)));
        AssertVec(new Vector3(0f, 0f, 0.2f), PlaneShake.NodeRotation(new Vector3(0f, 0f, 0.1f)));
        AssertVec(Vector3.Zero, PlaneShake.NodeRotation(Vector3.Zero));

        // Mixed axes, against the decode's chain worked by hand in double precision. That chain is
        // the quaternion (cos|s|, sin|s|·ŝ), its matrix, then asin(−m7), atan2(m6, m8), atan2(m1, m4).
        AssertVec(new Vector3(0.0998327f, 0.1003340f, 0.0050167f),
            PlaneShake.NodeRotation(new Vector3(0.05f, 0.05f, 0f)));
        AssertVec(new Vector3(0.0456927f, -0.0556508f, 0.1988146f),
            PlaneShake.NodeRotation(new Vector3(0.02f, -0.03f, 0.1f)));
        AssertVec(new Vector3(0.6888687f, 0.1421312f, -0.7842240f),
            PlaneShake.NodeRotation(new Vector3(0.3f, 0.2f, -0.4f)));
    }

    [Fact]
    public void TheNodeRotationIsTheEngineNodeOrderAPivotTakes()
    {
        // The independent route: Godot's own axis-angle basis read back in YXZ. That is the order a
        // Node3D's Rotation is built in, so writing NodeRotation to the pivot turns it by 2|s|.
        foreach (var s in new[] { new Vector3(0.05f, 0.05f, 0f), new Vector3(0.02f, -0.03f, 0.1f), new Vector3(0.3f, 0.2f, -0.4f) })
        {
            var reference = new Basis(s.Normalized(), 2f * s.Length()).GetEuler(EulerOrder.Yxz);
            AssertVec(reference, PlaneShake.NodeRotation(s));
        }
    }

    [Fact]
    public void AFiredRoundKicksBlockZeroWithTheDecodedVelocityTriple()
    {
        // From rest one 60 Hz tick is two full substeps and a half one. The reversal needs |x| past
        // |v|/60, which takes longer, so the block's position is exactly the kick times dt.
        var shake = NewShake();
        shake.FireBullet(40f);
        shake.Advance(Dt);
        var kick = ExpectedKick(seed: 1, FireStep);
        AssertVec(kick * Dt, shake.Sum, 1e-6f);
        AssertVec(PlaneShake.NodeRotation(shake.Sum), shake.Rotation, 1e-7f);

        // The weights: pitch and yaw take ±0.6·step, roll ±1.25·step, so the roll is the ×2.5
        // component and the nose moves on every kick.
        Assert.InRange(Math.Abs(shake.Sum.X), 1e-7f, 0.6f * FireStep * Dt);
        Assert.InRange(Math.Abs(shake.Sum.Y), 1e-7f, 0.6f * FireStep * Dt);
        Assert.InRange(Math.Abs(shake.Sum.Z), 1e-7f, 1.25f * FireStep * Dt);
    }

    [Fact]
    public void ASawtoothBlockCoastsThenReversesAtItsAuthoredLaw()
    {
        // Nitro: frequency 4, damp 3. The block coasts until |x| passes |v|/16, tested before each
        // substep of 1/150, 1/150 and 1/300 s. The first test past it is at 3/60 + 2/150 s.
        var shake = NewShake();
        shake.NitroEngaged();
        var kick = ExpectedKick(seed: 1, 0.05f * 4f * 4f);
        float reversedAt = (3f / 60f) + (2f / 150f);
        // The reversal sets the velocity to −16·e^(−3/8)·x, and the tick's last 1/300 s carries x
        // back to the peak a tick samples.
        float samplePeak = reversedAt * (1f - (16f * MathF.Exp(-3f / 8f) / 300f));
        float peak = 0f;
        for (int i = 0; i < 30; i++)
        {
            shake.Advance(Dt);
            peak = Math.Max(peak, shake.Sum.Length());
        }
        Assert.Equal(kick.Length() * samplePeak, peak, 5);

        // Each later swing is the last one times the per-reversal factor, about 0.687 on ticks. A
        // swing comes every 8 to 11 ticks rather than at the authored 4 Hz.
        var decay = NewShake();
        decay.NitroEngaged();
        var swings = Swings(decay, seconds: 1f);
        Assert.True(swings.Count >= 4, $"the engage swings several times: {swings.Count}");
        for (int i = 2; i < swings.Count; i++)
        {
            Assert.InRange(Math.Abs(swings[i].Roll) / Math.Abs(swings[i - 1].Roll), 0.55f, 0.85f);
            Assert.InRange(swings[i].Tick - swings[i - 1].Tick, 8, 11);
        }
        MaxTurnOver(decay, seconds: 6f); // let the ring-down finish
        Assert.Equal(0f, MaxTurnOver(decay, seconds: 1f));
    }

    [Fact]
    public void ANitroEngageRollsAboutFourTimesThePreviousPortAndMovesTheNose()
    {
        // The first swing's rendered roll is about twice the ×2.5 component's position. The port
        // before rolled by the ×1.2 component's position alone. On the same draw the roll grows by
        // about 2·2.5/1.2, and the nose moves too.
        var shake = NewShake();
        shake.NitroEngaged();
        var kick = ExpectedKick(seed: 1, 0.05f * 4f * 4f);
        float samplePeak = ((3f / 60f) + (2f / 150f)) * (1f - (16f * MathF.Exp(-3f / 8f) / 300f));
        float rollPeak = 0f, nosePeak = 0f;
        for (int i = 0; i < 30; i++)
        {
            shake.Advance(Dt);
            rollPeak = Math.Max(rollPeak, Math.Abs(shake.Rotation.Z));
            nosePeak = Math.Max(nosePeak, Math.Max(Math.Abs(shake.Rotation.X), Math.Abs(shake.Rotation.Y)));
        }
        Assert.Equal(Math.Abs(PlaneShake.NodeRotation(kick * samplePeak).Z), rollPeak, 5);
        float previousPort = Math.Abs(kick.Z) * (1.2f / 2.5f) * samplePeak;
        // 2·2.5/1.2 = 4.17 on the roll component. The Euler readback couples in the nose's pitch
        // and yaw, which moves the rendered roll by a few percent either way.
        Assert.InRange(rollPeak / previousPort, 3.5f, 4.5f);
        Assert.True(nosePeak > 0f, "the engage pitches or yaws the nose");
    }

    [Fact]
    public void ACannonHitIsSizedByCaliberAndHeFactor()
    {
        // The kick is linear in its magnitude, and the same seed draws the same direction. So the
        // block's first-tick position scales exactly with caliber and with he_factor.
        float Tick(Action<PlaneShake> hit)
        {
            var shake = NewShake();
            hit(shake);
            shake.Advance(Dt);
            return shake.Sum.Length();
        }

        float plain = Tick(s => s.BulletHit(40f, highExplosive: false));
        Assert.True(plain > 0f, "a cannon round taken rocks the plane");
        Assert.Equal(2f, Tick(s => s.BulletHit(80f, highExplosive: false)) / plain, 4);
        Assert.Equal(2f, Tick(s => s.BulletHit(40f, highExplosive: true)) / plain, 4);
    }

    [Fact]
    public void AMissileHitIsSizedByTheLargerFigureOfTheDamagePair()
    {
        // The original takes max(armour, health) of the delivered pair. The HE rocket's 40/60
        // pair kicks as 60, swapping the pair changes nothing, and he_factor doubles it.
        float Tick(float armor, float health, bool he)
        {
            var shake = NewShake();
            shake.MissileHit(armor, health, he);
            shake.Advance(Dt);
            return shake.Sum.Length();
        }

        Assert.Equal(Tick(60f, 60f, false), Tick(40f, 60f, false), 6);
        Assert.Equal(Tick(40f, 60f, false), Tick(60f, 40f, false), 6);
        Assert.Equal(1.5f, Tick(60f, 60f, false) / Tick(40f, 40f, false), 4);
        Assert.Equal(2f, Tick(10f, 10f, true) / Tick(10f, 10f, false), 4);
    }

    [Fact]
    public void AnImpactRingsDownOnItsSpringLaw()
    {
        // missile_impact is a smooth source: a damped spring at 2π·2.2 rad/s with damp 6.5. A kick
        // swings out about 0.7 of v/ω (damping ratio 0.235) and rings back to rest.
        var shake = NewShake();
        shake.MissileHit(60f, 60f, highExplosive: true);
        var kick = ExpectedKick(seed: 1, 60f * 1e-3f * 2f * 2.2f * MathF.Tau);
        float omega = 2.2f * MathF.Tau;
        float peak = 0f;
        for (int i = 0; i < 60; i++)
        {
            shake.Advance(Dt);
            peak = Math.Max(peak, shake.Sum.Length());
        }
        Assert.InRange(peak / (kick.Length() / omega), 0.6f, 0.8f);
        MaxTurnOver(shake, seconds: 6f);
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));
    }

    [Fact]
    public void TheExplosionSourceKicksNothingBecauseNoMaxMagnitudeIsAuthored()
    {
        // The fixture authors explosion exactly as the shipped file does, magnitude_factor and no
        // max_magnitude, so a burst at its centre rocks nothing.
        var shake = NewShake();
        shake.ExplosionAt(1f);
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 0.5f));
    }

    [Fact]
    public void TheOverspeedGateHoldsCruiseSilentAndOpensBeyondRatedMax()
    {
        var shake = NewShake();
        shake.SetSpeedRatio(0.95f); // fast cruise, below the min_speed 1.0 gate
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));

        shake.SetSpeedRatio(1.0f);  // exactly at the gate: EXCESS over it, so still zero
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));

        shake.SetSpeedRatio(1.2f);  // overspeed dive
        float diving = MaxTurnOver(shake, seconds: 1f);
        Assert.True(diving > 0f, "the open gate rattles");

        shake.SetSpeedRatio(0.5f);  // pull out: the ramp law's own decay bleeds it off, no pop
        Assert.True(MaxTurnOver(shake, seconds: 1f) < diving);
        MaxTurnOver(shake, seconds: 2f); // let the ring-down reach the block's rest threshold
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));
    }

    [Fact]
    public void TheDiveRattleKicksTheDecodedVelocityTripleOncePerTick()
    {
        // The excess over the gate, (ratio − 1)/70, kicks block 4 (frequency 15, sawtooth). The
        // kick lands before the tick integrates, so one tick's position is that kick times dt.
        float Step(float ratio) => (ratio - 1f) / 70f * 15f * 4f;

        var shake = NewShake();
        shake.SetSpeedRatio(1.2f);
        shake.Advance(Dt);
        AssertVec(ExpectedKick(seed: 1, Step(1.2f)) * Dt, shake.Sum, 1e-7f);

        // Twice the excess over the gate is twice the step, the law being linear in the drive.
        var faster = NewShake();
        faster.SetSpeedRatio(1.4f);
        faster.Advance(Dt);
        Assert.Equal(2f, faster.Sum.Length() / shake.Sum.Length(), 3);

        // It is a walk: the same dive on another seed steps somewhere else.
        var other = NewShake(seed: 7);
        other.SetSpeedRatio(1.2f);
        other.Advance(Dt);
        Assert.NotEqual(shake.Sum, other.Sum);
    }

    [Fact]
    public void ASustainedDiveWandersOnEveryAxis()
    {
        // A deterministic sawtooth under a constant drive settles to one repeated amplitude. The
        // decoded block re-kicks its velocity at random every tick, so the swings wander. The nose
        // pitches and yaws while the wings roll.
        var shake = NewShake();
        shake.SetSpeedRatio(1.3f);
        var trace = new List<Vector3>();
        for (int i = 0; i < 240; i++)
        {
            shake.Advance(Dt);
            trace.Add(shake.Rotation);
        }
        var swings = Swings(trace.Select(r => r.Z).ToList()).Select(s => s.Roll).ToList();
        Assert.True(swings.Count > 20, $"a 4 s dive swings repeatedly: {swings.Count}");
        Assert.Contains(swings, s => s > 0f);
        Assert.Contains(swings, s => s < 0f);
        double mean = swings.Average(s => Math.Abs(s));
        double spread = Math.Sqrt(swings.Average(s => (Math.Abs(s) - mean) * (Math.Abs(s) - mean)));
        Assert.True(spread / mean > 0.15,
            $"the swing amplitude wanders rather than repeating: mean {mean}, spread {spread}");
        Assert.True(trace.Max(r => Math.Abs(r.X)) > 0f && trace.Max(r => Math.Abs(r.Y)) > 0f,
            "the dive moves the nose as well as the wings");
    }

    [Fact]
    public void AContactKicksBlockFiveAtTheDecodedMagnitude()
    {
        // 120 m/s into a 0.6 cosine is far over the ceiling, where any contact at flight speed
        // lands. The decoded law is min(speed x severity x 0.03, 0.15).
        float magnitude = CollisionDamage.ContactShake(speed: 120f, severity: 0.6f);
        Assert.Equal(CollisionDamage.ContactShakeCap, magnitude, 5);

        // Block 5 keeps the constructor's law (2 Hz, damp 4.5, smooth) because no def authors it.
        // The kick's step is 0.15·2·2π, and the spring swings out about 0.79 of v/ω.
        var shake = NewShake();
        shake.ContactHit(magnitude);
        var kick = ExpectedKick(seed: 1, magnitude * 2f * MathF.Tau);
        float peak = 0f;
        for (int i = 0; i < 30; i++)
        {
            shake.Advance(Dt);
            peak = Math.Max(peak, shake.Sum.Length());
        }
        Assert.InRange(peak / (kick.Length() / (2f * MathF.Tau)), 0.7f, 0.9f);
        MaxTurnOver(shake, seconds: 8f);
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));
    }

    [Fact]
    public void AGrazeKicksTooAndScalesWithSpeedAndTheRawCosine()
    {
        // The original gates the call on a positive severity cosine and nothing else, so a graze
        // kicks like any other contact. Under the ceiling the law is linear in both terms.
        Assert.Equal(0f, CollisionDamage.ContactShake(120f, 0f));
        Assert.Equal(0.06f, CollisionDamage.ContactShake(20f, 0.1f), 5);
        Assert.Equal(0.12f, CollisionDamage.ContactShake(40f, 0.1f), 5);

        float Tick(float magnitude)
        {
            var shake = NewShake();
            shake.ContactHit(magnitude);
            shake.Advance(Dt);
            return shake.Sum.Length();
        }

        Assert.True(Tick(CollisionDamage.ContactShake(20f, 0.1f)) > 0f, "a graze rocks the plane");
        Assert.Equal(2f, Tick(0.12f) / Tick(0.06f), 4);
    }

    [Fact]
    public void AResetBringsEveryBlockBackToRest()
    {
        // A crash leaves the contact block ringing for seconds. A respawn hands the next airframe
        // a level pivot instead, and it stays level with nothing new kicked.
        var shake = NewShake();
        shake.ContactHit(CollisionDamage.ContactShakeCap);
        shake.NitroEngaged();
        shake.SetSpeedRatio(1.3f);
        Assert.True(MaxTurnOver(shake, seconds: 0.2f) > 0f, "the kicks rock the plane before the reset");

        shake.Reset();
        Assert.Equal(Vector3.Zero, shake.Sum);
        Assert.Equal(Vector3.Zero, shake.Rotation);
        Assert.Equal(0f, MaxTurnOver(shake, seconds: 1f));
    }

    [Fact]
    public void AMissingSourceIsANoOpNotACrash()
    {
        // An install whose file authors nothing at all: no source has a law or a magnitude to run.
        var bare = new PlaneShake(new ShakeDefs(), new Random(1));
        bare.FireBullet(40f);
        bare.BulletHit(40f, highExplosive: true);
        bare.MissileHit(40f, 60f, highExplosive: true);
        bare.ExplosionAt(1f);
        bare.NitroEngaged();
        bare.SetSpeedRatio(1.5f);
        Assert.Equal(0f, MaxTurnOver(bare, seconds: 0.5f));
    }

    [Fact]
    public void TheSameEventScriptReplaysToTheSameRotationTrace()
    {
        Vector3[] Run(int seed)
        {
            var shake = NewShake(seed);
            var trace = new Vector3[120];
            for (int i = 0; i < trace.Length; i++)
            {
                if (i == 10 || i == 40)
                {
                    shake.FireBullet(40f);
                }
                if (i == 60)
                {
                    shake.MissileHit(12f, 12f, highExplosive: true);
                }
                shake.SetSpeedRatio(i > 80 ? 1.1f : 0.7f);
                shake.Advance(Dt);
                trace[i] = shake.Rotation;
            }
            return trace;
        }

        Assert.Equal(Run(1), Run(1));       // same seed (fixed) → identical wobble
        Assert.NotEqual(Run(1), Run(2));    // seeded → different mission, different buzz
    }

    private static PlaneShake NewShake() => NewShake(seed: 1);

    private static PlaneShake NewShake(int seed) =>
        new(ShakeDefs.Load(TestData.Fixture("zrdr")), new Random(seed));

    // The velocity triple one kick of `step` adds, from the stream the shake was seeded with.
    // Three uniform draws: pitch and yaw weighted 1.2 and roll 2.5, in that order.
    private static Vector3 ExpectedKick(int seed, float step)
    {
        var rng = new Random(seed);
        float pitch = ((float)rng.NextDouble() - 0.5f) * step * 1.2f;
        float yaw = ((float)rng.NextDouble() - 0.5f) * step * 1.2f;
        float roll = ((float)rng.NextDouble() - 0.5f) * step * 2.5f;
        return new Vector3(pitch, yaw, roll);
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, float tolerance = 2e-6f)
    {
        Assert.True((expected - actual).Length() <= tolerance,
            $"expected {expected}, got {actual} (off by {(expected - actual).Length():E2})");
    }

    // Every turning point of the roll trace over the window, with the tick it turned on: the
    // swing amplitude and the swing rate.
    private static List<(float Roll, int Tick)> Swings(PlaneShake shake, float seconds)
    {
        var rolls = new List<float> { shake.Rotation.Z };
        for (int tick = 0; tick < (int)(seconds / Dt); tick++)
        {
            shake.Advance(Dt);
            rolls.Add(shake.Rotation.Z);
        }
        return Swings(rolls);
    }

    private static List<(float Roll, int Tick)> Swings(List<float> rolls)
    {
        var swings = new List<(float Roll, int Tick)>();
        float last = 0f;
        for (int i = 1; i < rolls.Count; i++)
        {
            float step = rolls[i] - rolls[i - 1];
            if (step == 0f)
            {
                continue;
            }
            if (last != 0f && Math.Sign(step) != Math.Sign(last))
            {
                swings.Add((rolls[i - 1], i));
            }
            last = step;
        }
        return swings;
    }

    // The largest turn the node took over the window, radians of its rotation vector.
    private static float MaxTurnOver(PlaneShake shake, float seconds)
    {
        float max = 0f;
        for (float t = 0f; t < seconds; t += Dt)
        {
            shake.Advance(Dt);
            max = Math.Max(max, shake.Rotation.Length());
        }
        return max;
    }
}
