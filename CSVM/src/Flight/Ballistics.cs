using Godot;

namespace CSVM.Flight;

/// <summary>The VELOCITY/ACCELERATION/GRAVITY integration every round in this game steps with —
/// shared by the live rounds (<see cref="ProjectilePool.SimStep"/>) and the reticle's projected
/// impact point (<see cref="FlightController"/>), so the two cannot silently disagree. No Godot
/// <c>Node</c> dependency: both callers keep their own loop shape (the raycast/fuse test, the
/// range cap/iteration bound) and own their own <c>dt</c>.</summary>
public static class Ballistics
{
    /// <summary>One integration step: accelerate along the current heading, apply gravity, then
    /// advance position by the resulting velocity. Mutates both in place.</summary>
    public static void Step(ref Vector3 pos, ref Vector3 vel, float accel, float grav, float dt)
    {
        ApplyForces(ref vel, accel, grav, dt);
        pos += vel * dt;
    }

    /// <summary>Where a round of <paramref name="weapon"/> fired from <paramref name="origin"/>
    /// along <paramref name="forward"/> (carrying <paramref name="inheritVel"/>, the plane's
    /// velocity) sits after travelling <paramref name="maxDistance"/> m of path, integrated at
    /// <paramref name="dt"/> — the reticle's capped walk. Capped at the weapon's <c>RANGE</c> (a
    /// round never converges past where it expires) and a hard iteration bound.</summary>
    public static Vector3 March(WeaponDef weapon, Vector3 origin, Vector3 forward,
        Vector3 inheritVel, float maxDistance, float dt)
    {
        var vel = forward * (weapon.Velocity ?? 500f) + inheritVel;
        float accel = weapon.Acceleration ?? 0f;
        float grav = (weapon.Gravity ?? 0f) * ProjectilePool.WorldGravity;
        float cap = Mathf.Min(maxDistance, weapon.Range ?? maxDistance);
        var pos = origin;
        float travelled = 0f;
        for (int i = 0; i < 4096 && travelled < cap; i++)
        {
            ApplyForces(ref vel, accel, grav, dt);
            var stepv = vel * dt;
            float step = stepv.Length();
            if (step < 1e-6f)
                break; // a degenerate near-zero speed must never spin the loop
            if (travelled + step > cap)
            {
                pos += stepv * ((cap - travelled) / step); // don't overshoot the convergence range
                break;
            }
            pos += stepv;
            travelled += step;
        }
        return pos;
    }

    private static void ApplyForces(ref Vector3 vel, float accel, float grav, float dt)
    {
        if (accel != 0f)
            vel += vel.Normalized() * (accel * dt);
        if (grav != 0f)
            vel += Vector3.Down * (grav * dt);
    }
}
