using System;
using CSVM.Utils;

namespace CSVM.Flight;

/// <summary>
/// The plane-wobble oscillators (<c>shakes.json</c> via <see cref="ShakeDefs"/>): gunfire buzz,
/// overspeed rattle, being-hit rocks and the nitro engage, summed into <see cref="Roll"/> — radians the flight
/// rig applies to a pivot node between the <see cref="FlightController"/> and its plane model.
/// Decode: docs/org/shakes.md, which carries the random-walk law the FIRE source runs and what
/// the other sources still do. Engine-free on purpose: the pivot write is the controller's one
/// line, the law lives here where the unit tests reach.
/// ⚠ Everything visual rides the pivot; physics, aim and the chase camera read the controller's
/// own transform and must never read the pivot's.
/// ⚠ Every random step must come from the injected <see cref="Random"/>, never a fresh one, or
/// a <c>--det</c> burst stops replaying to the same wobble trace.</summary>
public sealed class PlaneShake
{
    /// <summary>Per-shot gun-buzz step scale. The decoded law is ±<c>7.54·(factor×caliber)</c>;
    /// scale 1 reproduces the original's faithful step (= ±2.11e-2 rad for wep40), lower tames it.
    /// The one tune knob for the fidelity judgment — the mechanism (random walk) is what BL-266(a)
    /// is evaluating, this is how loud the wobble reads.</summary>
    public const float GunBuzzKickScale = 1f;

    // What each impact source's magnitude_factor multiplies is authored for fire_bullet only
    // (caliber, measured). For being hit: an incoming gun round reuses the caliber law; a rocket
    // has no caliber, so its armor damage stands in, doubled by he_factor when HIGH_EXPLOSIVE.
    // The stand-ins are declared TUNE pending being-hit footage.
    private readonly Osc _fire = new();
    private readonly Osc _bulletHit = new();
    private readonly Osc _missileHit = new();
    private readonly Osc _explosion = new();
    private readonly Osc _speed = new();
    private readonly Osc _nitro = new();

    // Block 5 (camera+0xf4) is the one oscillator no data authors: shakes.zrd names no `turbulence`
    // source, so the constructor's law stands and the collision path is the block's only kicker
    // (docs/org/shakes.md). ⚠ Do not give it a def — these three constants ARE its law.
    private readonly Osc _contact = new()
    {
        Src = new ShakeSource { Id = "contact", Frequency = 2f, Damp = 4.5f, Sawtooth = false },
    };

    // The fire source's per-shot steps. Injected for off-engine determinism (BL-266(a) branch:
    // see the class summary); the session resolves Rng.NewSystemRandom(Rng.Shake).
    private readonly Random _fireRng;

    /// <param name="rng">The stream the fire source draws its per-shot steps from. Defaults to
    /// <see cref="Rng.NewSystemRandom(Rng.Shake)"/> (deterministic under <c>--det</c>); tests pass
    /// a fixed-seed <see cref="Random"/> so the wobble trace is pinned without the engine.
    /// Accepting an engineer-supplied <see cref="Random"/> (never the shared Godot stream directly)
    /// is the repo's off-engine-testing pattern (cf. BandFlicker, AiModeMachine).</param>
    public PlaneShake(ShakeDefs defs, Random? rng = null)
    {
        _fire.Src = defs.FireBullet;
        _bulletHit.Src = defs.BulletImpact;
        _missileHit.Src = defs.MissileImpact;
        _explosion.Src = defs.Explosion;
        _speed.Src = defs.HighSpeed;
        _nitro.Src = defs.Nitro;
        _fireRng = rng ?? Rng.NewSystemRandom(Rng.Shake);
    }

    /// <summary>The summed wobble after the last <see cref="Advance"/>, radians of roll.</summary>
    public float Roll { get; private set; }

    /// <summary>One gun round left this plane: add a random step to the firing random-walk
    /// accumulator (the original's gun buzz, decoded from crimson.exe FUN_0042be10). Each shot's
    /// step is uniform in ±<c>7.54·(factor×caliber)</c> (× <see cref="GunBuzzKickScale"/>); the walk
    /// decays with the source's authored <c>damp</c> in <see cref="Advance"/>, so a steady 8/s burst
    /// reads as a bounded rattle, not a monotonic roll-off.</summary>
    public void FireBullet(float caliber)
    {
        if (_fire.Src is { MagnitudeFactor: { } f })
        {
            _fire.RandomWalkKick(f * caliber * GunBuzzKickScale, _fireRng);
        }
    }

    /// <summary>A gun round struck this plane.</summary>
    public void BulletHit(float caliber)
    {
        if (_bulletHit.Src is { MagnitudeFactor: { } f })
        {
            _bulletHit.Kick(f * caliber);
        }
    }

    /// <summary>A rocket/ordnance round struck this plane; <paramref name="damage"/> stands in
    /// for the unauthored per-event quantity, doubled by <c>he_factor</c> on HE rounds.</summary>
    public void MissileHit(float damage, bool highExplosive)
    {
        if (_missileHit.Src is { MagnitudeFactor: { } f })
        {
            float he = highExplosive ? _missileHit.Src.HeFactor ?? 1f : 1f;
            _missileHit.Kick(f * damage * he);
        }
    }

    /// <summary>A nearby detonation (not a direct hit) rocked this plane.</summary>
    public void ExplosionAt(float damage)
    {
        if (_explosion.Src is { MagnitudeFactor: { } f })
        {
            _explosion.Kick(f * damage);
        }
    }

    /// <summary>The nitro engaged on this plane: one kick of the source's absolute authored
    /// <c>magnitude</c> (block 6 at <c>0x4b21ce</c>, docs/org/shakes.md), decaying at its damp.
    /// The caller gates it on a human pilot, as the original gates it on the player.</summary>
    public void NitroEngaged()
    {
        if (_nitro.Src is { Magnitude: { } m })
        {
            _nitro.Kick(m);
        }
    }

    /// <summary>One resolved contact rocked this plane: block 5's kick at <c>0x48d409</c>, whose
    /// <paramref name="magnitude"/> is <see cref="CollisionDamage.ContactShake"/>'s radians.
    /// The caller gates it on a human pilot, as the original gates it on the player, and a graze
    /// reaches it like any other contact: the only gate the original puts on the call is a positive
    /// severity cosine.</summary>
    public void ContactHit(float magnitude)
    {
        if (magnitude > 0f)
        {
            _contact.Kick(magnitude);
        }
    }

    /// <summary>Per-tick overspeed drive: <paramref name="speedRatio"/> is speed over the
    /// plane's rated max, so the authored <c>min_speed</c> 1.0 gate reads "beyond rated max" —
    /// the dive rattle. Magnitude is the EXCESS over the gate <c>(speedRatio − gate)/quotient</c>,
    /// not the whole ratio: zero at rated max, gentle ramp with overspeed, same order as the gun
    /// buzz in a dive (a whole-ratio reading snapped on at <c>1.0/70</c> = 5× the buzz). Quiet
    /// cruise matches the footage's motionless idle floor.</summary>
    public void SetSpeedRatio(float speedRatio)
    {
        _speed.Target = _speed.Src is { MinSpeed: { } gate, MagnitudeQuotient: > 0f } src
                        && speedRatio >= gate
            ? (speedRatio - gate) / src.MagnitudeQuotient!.Value
            : 0f;
    }

    /// <summary>Advances every oscillator by one sim tick and re-sums <see cref="Roll"/>.
    /// Pure function of sim dt and the events since the last tick — deterministic under
    /// <c>--det</c>'s fixed clock.</summary>
    public void Advance(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }
        Roll = _fire.Advance(dt) + _bulletHit.Advance(dt) + _missileHit.Advance(dt)
               + _explosion.Advance(dt) + _speed.Advance(dt) + _nitro.Advance(dt)
               + _contact.Advance(dt);
    }

    private sealed class Osc
    {
        public ShakeSource? Src;
        public float Amp;      // current envelope, radians
        public float Target;   // continuous drive (high_speed); impulses leave it 0
        public float Phase;    // waveform cycles, wraps at 1
        public float Walk;     // random-walk accumulator (fire source, BL-266(a) branch)

        public float Advance(float dt)
        {
            if (Src == null)
            {
                return 0f;
            }
            if (Walk != 0f)
            {
                // Random-walk accumulator (fire source): no waveform — the accumulated roll is the
                // wobble, decaying toward 0 between shots at the authored damp.
                Walk *= MathF.Exp(-Src.Damp * dt);
                if (MathF.Abs(Walk) < 1e-5f)
                {
                    Walk = 0f;
                }
                return Walk;
            }
            // exponential decay toward Target (0 for impulse sources, the drive for high_speed)
            float k = MathF.Exp(-Src.Damp * dt);
            Amp = Target + (Amp - Target) * k;
            if (Amp <= 1e-6f && Target <= 0f)
            {
                Amp = 0f;
                return 0f;
            }
            Phase += Src.Frequency * dt;
            Phase -= MathF.Floor(Phase);
            float wave = Src.Sawtooth
                ? 2f * Phase - 1f
                : MathF.Sin(2f * MathF.PI * Phase);
            return Amp * wave;
        }

        public void Kick(float magnitude)
        {
            Amp = Math.Max(Amp, magnitude);
        }

        /// <summary>Add a uniform random step to the random-walk accumulator. <paramref name="magnitude"/>
        /// is <c>factor×caliber</c>; the ×2.0×6.2832×1.2 gain (see class summary) and the injected
        /// rng give the step its direction, so over many shots the walk drifts across the roll axis
        /// like the original's camera accumulator, bounded by the <see cref="Advance"/> decay.</summary>
        public void RandomWalkKick(float magnitude, Random rng)
        {
            // u ∈ [0,1); (2u−1) ∈ (−1,1). 2.0×6.2832×1.2 = 15.08; × the (rand−0.5) half-range 0.5
            // collapses to the 7.54 the class docs quote.
            float u = 2f * (float)rng.NextDouble() - 1f;
            Walk += u * magnitude * 7.54f;
        }
    }
}
