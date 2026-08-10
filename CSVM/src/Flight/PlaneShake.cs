using System;

namespace CSVM.Flight;

/// <summary>
/// The plane-wobble oscillators (<c>shakes.json</c> via <see cref="ShakeDefs"/>): gunfire buzz,
/// overspeed rattle, and being-hit rocks, summed into <see cref="Roll"/> — radians the flight
/// rig applies to a pivot node between the <see cref="FlightController"/> and its plane model.
/// Everything visual — mesh, muzzle transforms, puffer anchors, mounted ordnance — rides that
/// pivot; physics, aim and the chase camera read the controller's own transform and never see
/// it (in the original the plane visibly rocks against the world in external views — the
/// camera does not follow the wobble). Engine-free on purpose: the pivot write is the
/// controller's one line, the law lives here where the unit tests reach.
///
/// <para>Amplitudes are radians of roll: measured for <c>fire_bullet</c>
/// (<c>analysis/gun-wobble-shake/</c> — magnitude_factor × caliber, dead-astern roll
/// decomposition). Each impulse source keeps one envelope that an event re-kicks to its
/// magnitude and <c>damp</c> decays exponentially; <c>high_speed</c> is driven continuously,
/// its envelope chasing the target at the same <c>damp</c> rate so the <c>min_speed</c> gate
/// never pops. The <c>nitro</c> source and the ON_CALL <c>damage_shakes</c> defs stay unwired
/// (no nitro system; unknown caller — docs/formats/shakes.md).</para>
/// </summary>
public sealed class PlaneShake
{
    // What each impact source's magnitude_factor multiplies is authored for fire_bullet only
    // (caliber, measured). For being hit: an incoming gun round reuses the caliber law; a rocket
    // has no caliber, so its armor damage stands in, doubled by he_factor when HIGH_EXPLOSIVE.
    // The stand-ins are declared TUNE pending being-hit footage.
    private readonly Osc _fire = new();
    private readonly Osc _bulletHit = new();
    private readonly Osc _missileHit = new();
    private readonly Osc _explosion = new();
    private readonly Osc _speed = new();

    public PlaneShake(ShakeDefs defs)
    {
        _fire.Src = defs.FireBullet;
        _bulletHit.Src = defs.BulletImpact;
        _missileHit.Src = defs.MissileImpact;
        _explosion.Src = defs.Explosion;
        _speed.Src = defs.HighSpeed;
    }

    /// <summary>The summed wobble after the last <see cref="Advance"/>, radians of roll.</summary>
    public float Roll { get; private set; }

    /// <summary>One gun round left this plane: re-kick the firing buzz to
    /// <c>magnitude_factor × caliber</c> (the measured law).</summary>
    public void FireBullet(float caliber)
    {
        if (_fire.Src is { MagnitudeFactor: { } f })
        {
            _fire.Kick(f * caliber);
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

    /// <summary>Per-tick overspeed drive: <paramref name="speedRatio"/> is speed over the
    /// plane's rated max, so the authored <c>min_speed</c> 1.0 gate reads "beyond rated max" —
    /// the dive rattle. Quiet cruise matches the footage's motionless idle floor.</summary>
    public void SetSpeedRatio(float speedRatio)
    {
        _speed.Target = _speed.Src is { MinSpeed: { } gate, MagnitudeQuotient: > 0f } src
                        && speedRatio >= gate
            ? speedRatio / src.MagnitudeQuotient!.Value
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
               + _explosion.Advance(dt) + _speed.Advance(dt);
    }

    private sealed class Osc
    {
        public ShakeSource? Src;
        public float Amp;      // current envelope, radians
        public float Target;   // continuous drive (high_speed); impulses leave it 0
        public float Phase;    // waveform cycles, wraps at 1

        public float Advance(float dt)
        {
            if (Src == null)
            {
                return 0f;
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
    }
}
