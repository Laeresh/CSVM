using Godot;

namespace CSVM.Flight.Camera;

/// <summary>What the flat level chase carries from one frame to the next: the camera's right axis
/// and the level right it is rolling to. Both are world vectors at right angles to the forward.
/// </summary>
public readonly record struct LevelChaseHold(Vector3 Right, Vector3 Target);

/// <summary>
/// The flat chase's level frame (<see cref="Bindings.ChaseCamera.Level"/>): the aircraft's forward
/// with its bank taken out, so a turn never tilts the horizon. Unlike <see cref="XrMount.NoRoll"/>
/// the up is not kept on the sky's side: the frame follows the aircraft over the top of a loop.
/// Its right axis rests on one of the two level rights. Once the bank passes 90 degrees plus
/// <see cref="FlipPastRad"/> from it, it rolls to the other through the aircraft's side, at
/// <see cref="RollRateRad"/>. Within <see cref="XrMount.VerticalConeCos"/> of vertical the level is
/// undefined and the held right is carried. Engine-free; the rule is in docs/architecture/Flight.md.
/// </summary>
public static class LevelChase
{
    /// <summary>20 deg: the bank beyond 90 degrees from the held level at which the frame rolls to
    /// the other. A knife-edge held within it keeps the level. TUNE.</summary>
    public const float FlipPastRad = 0.3491f;

    /// <summary>240 deg/s: the fastest the frame rolls. Above the 180 deg/s of a hard aileron roll,
    /// so the frame never falls half a turn behind the aircraft. TUNE.</summary>
    public const float RollRateRad = 4.1888f;

    private const float Epsilon = 1e-4f;

    /// <summary>The level frame for <paramref name="attitude"/> after <paramref name="dt"/> seconds,
    /// from last frame's <paramref name="held"/> (null on a fresh shot, which seeds the level nearest
    /// the aircraft). Its forward (−Z) is the aircraft's; <paramref name="next"/> is this frame's hold.
    /// </summary>
    public static Basis Step(Basis attitude, LevelChaseHold? held, float dt, out LevelChaseHold next)
    {
        var axes = attitude.Orthonormalized();
        var forward = -axes.Z;
        var wing = axes.X;
        var level = forward.Cross(Vector3.Up);
        bool vertical = Mathf.Abs(forward.Y) > XrMount.VerticalConeCos || level.Length() < Epsilon;
        level = level.Normalized();

        Vector3 right, target;
        if (held is { } last && Carry(last.Right, forward) is { } carried && Carry(last.Target, forward) is { } aim)
        {
            right = carried;
            target = aim;
            if (!vertical)
            {
                target = target.Dot(level) >= 0f ? level : -level;
                if (wing.Dot(target) < -Mathf.Sin(FlipPastRad))
                {
                    target = -target;
                }
            }

            // Measured through the wing, so a half-turn roll goes the aircraft's way.
            float turn = Signed(right, wing, forward) + Signed(wing, target, forward);
            float cap = RollRateRad * dt;
            right = Mathf.Abs(turn) <= cap ? target : right.Rotated(forward, Mathf.Sign(turn) * cap).Normalized();
        }
        else
        {
            target = vertical ? wing : (wing.Dot(level) >= 0f ? level : -level);
            right = target;
        }

        next = new LevelChaseHold(right, target);
        return new Basis(right, right.Cross(forward), -forward);
    }

    // The unit part of v at right angles to the forward, or null when v lies along it.
    private static Vector3? Carry(Vector3 v, Vector3 forward)
    {
        var flat = v - (forward * v.Dot(forward));
        return flat.Length() < Epsilon ? null : flat.Normalized();
    }

    // The signed angle from a to b about the axis, both at right angles to it.
    private static float Signed(Vector3 a, Vector3 b, Vector3 axis) => Mathf.Atan2(a.Cross(b).Dot(axis), a.Dot(b));
}
