using System;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>
/// The plane wobble, the original's seven camera component blocks over <see cref="ShakeDefs"/>,
/// each a randomly kicked velocity and the position it integrates into. The
/// <see cref="Rotation"/> property renders their sum as a rotation vector at twice its length,
/// in YXZ Euler angles for the pivot above the plane model (docs/org/shakes.md).
/// ⚠ Everything visual rides the pivot; physics, aim and the chase camera read the controller's
/// own transform and must never read the pivot's.
/// ⚠ Every random step must come from the injected <see cref="Random"/>, never a fresh one, or
/// a <c>--det</c> burst stops replaying to the same wobble trace.</summary>
public sealed class PlaneShake
{
    /// <summary>Per-shot gun-buzz kick scale, the fire source's one tune knob. Scale 1 is the
    /// original's own velocity kick into block 0, so dial this and never <c>magnitude_factor</c>.</summary>
    public const float GunBuzzKickScale = 1f;

    /// <summary>Per-tick overspeed-rattle kick scale, the dive rattle's one tune knob. Scale 1 is
    /// the original's own velocity kick into block 4, so dial this and never
    /// <c>magnitude_quotient</c>.</summary>
    public const float DiveRattleKickScale = 1f;

    /// <summary>Per-engage nitro-wobble kick scale, the same knob for block 6. Scale 1 is the
    /// original's own velocity kick, so dial this and never the authored <c>magnitude</c>.</summary>
    public const float NitroWobbleKickScale = 1f;

    // ⚠ Do not give block 5 a def; these three constants ARE its law (docs/org/shakes.md).
    // The data authors no `turbulence` source, so the camera constructor's law stands.
    private static readonly ShakeSource ContactLaw =
        new() { Id = "contact", Frequency = 2f, Damp = 4.5f, Sawtooth = false };

    // The seven blocks in the original's own index order. A source the file does not author has
    // no block and kicks nothing; the impact sizing is "What a round taken kicks".
    private readonly Block? _fire;
    private readonly Block? _bulletHit;
    private readonly Block? _missileHit;
    private readonly Block? _explosion;
    private readonly Block? _speed;
    private readonly Block _contact = new(ContactLaw);
    private readonly Block? _nitro;

    // Every block that exists, summed in one walk as the original's consumer sums all seven.
    private readonly Block[] _all;

    // The random-walk steps of every block. Injected for off-engine determinism (see the class
    // summary); the session resolves Rng.NewSystemRandom(Rng.Shake).
    private readonly Random _rng;

    // This tick's high_speed magnitude, the excess over the authored gate; zero below it.
    private float _speedDrive;

    /// <param name="rng">The stream the blocks draw their kicks from. Defaults to
    /// <see cref="Rng.NewSystemRandom(Rng.Shake)"/> (deterministic under <c>--det</c>); tests pass
    /// a fixed-seed <see cref="Random"/> so the wobble trace is pinned without the engine.</param>
    public PlaneShake(ShakeDefs defs, Random? rng = null)
    {
        _fire = BlockFor(defs.FireBullet);
        _bulletHit = BlockFor(defs.BulletImpact);
        _missileHit = BlockFor(defs.MissileImpact);
        _explosion = BlockFor(defs.Explosion);
        _speed = BlockFor(defs.HighSpeed);
        _nitro = BlockFor(defs.Nitro);
        _all = new[] { _fire, _bulletHit, _missileHit, _explosion, _speed, _contact, _nitro }
            .OfType<Block>().ToArray();
        _rng = rng ?? Rng.NewSystemRandom(Rng.Shake);
    }

    /// <summary>The seven blocks' summed position after the last <see cref="Advance"/>: pitch,
    /// yaw and roll components, radians, before the doubling the render applies.</summary>
    public Vector3 Sum { get; private set; }

    /// <summary>The node rotation <see cref="Sum"/> renders as, YXZ Euler radians (X pitch, Y yaw,
    /// Z roll), what the pivot's own <c>Rotation</c> takes. Zero when every block is at rest.</summary>
    public Vector3 Rotation { get; private set; }

    /// <summary>The node rotation the original writes for a summed block position
    /// <paramref name="sum"/>. The quaternion <c>(cos|s|, sin|s|·ŝ)</c> has no halving, so it
    /// turns by <c>2|s|</c> about <c>ŝ</c>. Its matrix is read back as YXZ Euler angles:
    /// <c>x = asin(−m[7])</c>, <c>y = atan2(m[6], m[8])</c>, <c>z = atan2(m[1], m[4])</c>.</summary>
    public static Vector3 NodeRotation(Vector3 sum)
    {
        float len = sum.Length();
        if (len == 0f)
        {
            return Vector3.Zero;
        }
        float w = MathF.Cos(len);
        float k = MathF.Sin(len) / len;
        float x = sum.X * k, y = sum.Y * k, z = sum.Z * k;
        float m1 = 2f * ((x * y) + (w * z));
        float m4 = 1f - (2f * ((z * z) + (x * x)));
        float m6 = 2f * ((y * w) + (x * z));
        float m7 = 2f * ((z * y) - (x * w));
        float m8 = 1f - (2f * ((x * x) + (y * y)));
        return new Vector3(MathF.Asin(Math.Clamp(-m7, -1f, 1f)), MathF.Atan2(m6, m8), MathF.Atan2(m1, m4));
    }

    /// <summary>One gun round left this plane: block 0 kicked by <c>magnitude_factor × CALIBER</c>
    /// (× <see cref="GunBuzzKickScale"/>), the per-shot law at <c>0x004b6e20</c>.</summary>
    public void FireBullet(float caliber)
    {
        if (_fire?.Src is { MagnitudeFactor: { } f })
        {
            _fire.Kick(f * caliber * GunBuzzKickScale, _rng);
        }
    }

    /// <summary>A <c>CANNON</c> round struck this plane: block 1 kicked by its <c>CALIBER</c> times
    /// the source's <c>magnitude_factor</c>, times missile_impact's <c>he_factor</c> on a
    /// <c>HIGH_EXPLOSIVE</c> round.</summary>
    public void BulletHit(float caliber, bool highExplosive)
    {
        if (_bulletHit?.Src is { MagnitudeFactor: { } f })
        {
            _bulletHit.Kick(f * caliber * HeScale(highExplosive), _rng);
        }
    }

    /// <summary>Any other round struck this plane, directly or by its blast. Block 2 is kicked by
    /// the larger figure of the delivered damage pair times <c>magnitude_factor</c>, and by
    /// <c>he_factor</c> on a <c>HIGH_EXPLOSIVE</c> round.</summary>
    public void MissileHit(float armorDamage, float healthDamage, bool highExplosive)
    {
        if (_missileHit?.Src is { MagnitudeFactor: { } f })
        {
            _missileHit.Kick(f * MathF.Max(armorDamage, healthDamage) * HeScale(highExplosive), _rng);
        }
    }

    /// <summary>A <c>SHAKES_CAMERA</c> round's burst reached this plane, at
    /// <paramref name="falloff"/> of the way in from its edge: block 3 kicked by that fraction of
    /// <c>max_magnitude</c>. ⚠ Do not read <c>magnitude_factor</c> here; the original's parser never
    /// does, and shakes.zrd authors no max_magnitude, so this kicks nothing, as the original's does.</summary>
    public void ExplosionAt(float falloff)
    {
        if (_explosion?.Src is { MaxMagnitude: { } m })
        {
            _explosion.Kick(falloff * m, _rng);
        }
    }

    /// <summary>The nitro engaged on this plane: block 6 kicked once by the source's absolute
    /// authored <c>magnitude</c> (<c>0x4b21ce</c>). The caller gates it on a human pilot, as the
    /// original gates it on the player.</summary>
    public void NitroEngaged()
    {
        if (_nitro?.Src is { Magnitude: { } m })
        {
            _nitro.Kick(m * NitroWobbleKickScale, _rng);
        }
    }

    /// <summary>One resolved contact rocked this plane: block 5 kicked at <c>0x48d409</c> by
    /// <see cref="Airframe.CollisionDamage.ContactShake"/>'s magnitude. The caller gates it on a
    /// human pilot, as the original gates it on the player. The original's only other gate is a
    /// positive severity cosine, so a graze reaches it like any other contact.</summary>
    public void ContactHit(float magnitude)
    {
        if (magnitude > 0f)
        {
            _contact.Kick(magnitude, _rng);
        }
    }

    /// <summary>Per-tick overspeed drive: <paramref name="speedRatio"/> is speed over the
    /// plane's rated max, so the authored <c>min_speed</c> 1.0 gate reads "beyond rated max",
    /// the dive rattle. Magnitude is the EXCESS over the gate <c>(speedRatio − gate)/quotient</c>,
    /// not the whole ratio: zero at rated max, a gentle ramp with overspeed. What it feeds is the
    /// next <see cref="Advance"/>'s velocity kick into block 4.</summary>
    public void SetSpeedRatio(float speedRatio)
    {
        _speedDrive = _speed?.Src is { MinSpeed: { } gate, MagnitudeQuotient: > 0f } src
                      && speedRatio > gate
            ? (speedRatio - gate) / src.MagnitudeQuotient!.Value
            : 0f;
    }

    /// <summary>Advances every block by one sim tick, then re-sums <see cref="Sum"/> and renders
    /// <see cref="Rotation"/> from it. Pure function of sim dt and the events since the last
    /// tick, deterministic under <c>--det</c>'s fixed clock.</summary>
    public void Advance(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }
        // Re-kicked every tick the gate is open, as the original's per-frame player updater does,
        // so the dive rattle is sustained accumulation.
        if (_speedDrive > 0f)
        {
            _speed?.Kick(_speedDrive * DiveRattleKickScale, _rng);
        }
        var sum = Vector3.Zero;
        foreach (var block in _all)
        {
            block.Advance(dt);
            sum += block.Position;
        }
        Sum = sum;
        Rotation = NodeRotation(sum);
    }

    private static Block? BlockFor(ShakeSource? src) => src == null ? null : new Block(src);

    // The original applies missile_impact's he_factor to a cannon round as well (0x004b9caf).
    private float HeScale(bool highExplosive) =>
        highExplosive ? _missileHit?.Src.HeFactor ?? 1f : 1f;

    // One of the original's seven camera component blocks. A velocity triple the kickers walk at
    // random integrates into a position triple, components pitch, yaw and roll. The authored
    // sawtooth picks both laws, the kick's waveform factor and the integrator's.
    // ⚠ The kick is a velocity, not an angle. Decode: docs/org/shakes.md.
    private sealed class Block
    {
        // The original integrates at this fixed substep inside whatever its frame was, and runs
        // the remainder short rather than long; a 60 Hz sim tick is two and a half of them.
        private const float SubStep = 1f / 150f;

        // The position below which the block is at rest, paired with the velocity that could
        // still swing it that far. Neither law reaches zero on its own, and a ringing denormal
        // would keep writing the pivot forever.
        private const float RestPos = 1e-6f;

        private Vector3 _vel;   // rad/s, what a kick adds to

        public Block(ShakeSource src) => Src = src;

        public ShakeSource Src { get; }

        public Vector3 Position { get; private set; }

        /// <summary>One kicker's random velocity step, <c>FUN_0042be10</c>. The step is
        /// <c>magnitude·frequency·W</c>, W 4 on a <c>sawtooth</c> source and 2π otherwise. Pitch and
        /// yaw take <c>(u−0.5)·step·1.2</c> and roll <c>(u−0.5)·step·2.5</c>, drawn in that order.</summary>
        public void Kick(float magnitude, Random rng)
        {
            float step = magnitude * Src.Frequency * (Src.Sawtooth ? 4f : MathF.Tau);
            float pitch = ((float)rng.NextDouble() - 0.5f) * step * 1.2f;
            float yaw = ((float)rng.NextDouble() - 0.5f) * step * 1.2f;
            float roll = ((float)rng.NextDouble() - 0.5f) * step * 2.5f;
            _vel += new Vector3(pitch, yaw, roll);
        }

        /// <summary>Integrates the block over one sim tick, <c>FUN_0042bec0</c>. A smooth source is
        /// a damped spring. On a <c>sawtooth</c> source, a triple moving outward slower than
        /// <c>4·frequency·|pos|</c> takes the velocity <c>−4·frequency·e^(−damp/2·frequency)·pos</c>,
        /// which is where the decay lives.</summary>
        public void Advance(float dt)
        {
            if (Src.Frequency <= 0f)
            {
                return;
            }
            var pos = Position;
            for (float left = dt; left > 1e-7f;)
            {
                float h = MathF.Min(SubStep, left);
                if (!Src.Sawtooth)
                {
                    float w = Src.Frequency * MathF.Tau;
                    _vel -= ((Src.Damp * _vel) + (w * w * pos)) * h;
                }
                else if (pos.Dot(_vel) > 0f && _vel.Length() < pos.Length() * Src.Frequency * 4f)
                {
                    _vel = -4f * Src.Frequency * MathF.Exp(-Src.Damp * 0.5f / Src.Frequency) * pos;
                }
                pos += _vel * h;
                left -= h;
            }
            float restVel = RestPos * Src.Frequency * (Src.Sawtooth ? 4f : MathF.Tau);
            if (pos.Length() < RestPos && _vel.Length() < restVel)
            {
                pos = Vector3.Zero;
                _vel = Vector3.Zero;
            }
            Position = pos;
        }
    }
}
