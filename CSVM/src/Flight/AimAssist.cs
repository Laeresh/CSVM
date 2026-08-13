using Godot;

namespace CSVM.Flight;

/// <summary>One gun barrel's aim-assist state, held in plane-local space
/// (docs/org/aim-assist.md "Per-muzzle state" — the original's eight <c>0x24</c>-byte slots at
/// plane <c>+0x3a4</c>). Indexed by (gun group, muzzle) exactly as <see cref="FireControl"/>
/// already picks a muzzle — no re-derivation of the original's <c>weaponGroup·2+barrelToggle</c>
/// index is needed.</summary>
public struct GunAimSlot
{
    /// <summary>False until this barrel exists (mirrors the original's muzzle-handle field, 0 =
    /// unused); an inactive slot is skipped by <see cref="AimAssist.Tick"/> entirely.</summary>
    public bool Active;

    /// <summary>Plane-local unit vector: what the round is actually fired along. Lags
    /// <see cref="Target"/> by the catch-up slerp.</summary>
    public Vector3 Smoothed;

    /// <summary>Plane-local unit vector: what <see cref="Smoothed"/> is chasing — the candidate
    /// scan's winner once B4/B5 land, or <see cref="AimAssist.LocalForward"/> when the forget
    /// timer has unwound it.</summary>
    public Vector3 Target;

    /// <summary>Game-time seconds (<see cref="CSVM.Utils.GameClock.Time"/>) this slot was last
    /// touched — stamped by the forget reset here, and, once B5 lands, by every round that goes
    /// out (the original restamps it on every shot, which is why the forget timer measures time
    /// since the barrel last fired, not time since a lock was lost).</summary>
    public double LastUpdate;
}

/// <summary>The per-muzzle gun aim assist: slot state (above), the per-frame forget + catch-up
/// pass (B2), and the constant-velocity intercept solver (B3, docs/org/aim-assist.md "The lead
/// solver — <c>FUN_00460e30</c>"). B4 adds the candidate scorer to this same file, so the whole
/// assist stays one testable unit that <see cref="FlightController"/> calls into.</summary>
public static class AimAssist
{
    /// <summary>Local forward — the "no target found" answer a slot's target unwinds to.</summary>
    public static readonly Vector3 LocalForward = new(0f, 0f, -1f);

    /// <summary>Two directions this close to parallel (or its negation) make Godot's
    /// <see cref="Vector3.Slerp"/> throw "Argument is not normalized": its rotation axis comes
    /// from a cross product that degenerates at 0° and 180° separation. <see cref="FlightModel"/>
    /// guards its own VelocityDir slerp the same way, for the same crash — a plane holding
    /// straight and level, or a gun line that has already caught up to its target, hits this
    /// every frame.</summary>
    private const float ParallelDot = 0.999f;

    /// <summary>The per-frame forget + catch-up pass, run once per active slot. Forget: past
    /// <paramref name="forgetInterval"/> seconds since the slot was last touched, the target
    /// unwinds to local forward. Catch-up: <see cref="GunAimSlot.Smoothed"/> slerps toward
    /// <see cref="GunAimSlot.Target"/> at <paramref name="catchupRate"/> per second, snapping
    /// outright once that covers the whole turn in one frame — any frame at or past
    /// <c>1 / catchupRate</c> seconds fully snaps the gun line, a real hitch-behaviour difference
    /// and not a rounding detail to smooth away.
    ///
    /// <para>Call this immediately BEFORE performing this tick's fire outcome — the original
    /// restamps <c>lastUpdate</c> on every round that goes out, so the pass must see the
    /// pre-shot state.</para></summary>
    public static void Tick(GunAimSlot[] slots, double now, float dt, float forgetInterval, float catchupRate)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].Active)
            {
                continue;
            }
            ref var slot = ref slots[i];
            if (slot.LastUpdate + forgetInterval < now)
            {
                slot.Target = LocalForward;
                slot.LastUpdate = now + forgetInterval;
            }
            float t = catchupRate * dt;
            if (t >= 1f)
            {
                slot.Smoothed = slot.Target;
                continue;
            }
            float dot = slot.Smoothed.Dot(slot.Target);
            slot.Smoothed = dot > ParallelDot
                ? (slot.Smoothed + (slot.Target - slot.Smoothed) * t).Normalized()
                : dot < -ParallelDot
                    ? slot.Target // degenerate opposite case: snap rather than crash on Slerp
                    : slot.Smoothed.Slerp(slot.Target, t).Normalized();
        }
    }

    /// <summary>The constant-velocity intercept solver (<c>FUN_00460e30</c>): given a muzzle
    /// position, the round's speed, a target position and the target's velocity RELATIVE to the
    /// shooter (target velocity minus the shooter's — the caller subtracts before calling), finds
    /// the direction to fire and the time of flight so a straight-line round launched now reaches
    /// where the target will be. Frame-agnostic — every input in the same space, world or
    /// plane-local, and the answer comes out in that space.
    ///
    /// <para>The round meets the target when <c>speed·t = |displacement + relVel·t|</c>, a
    /// quadratic in <c>t</c> whose leading coefficient is <c>|relVel|² − speed²</c> — which sits
    /// near zero whenever the target's closing speed is close to the round's, the common case.
    /// Substituting <c>u = 1/t</c> flips the roles of leading and constant coefficient, so the
    /// leading coefficient becomes <c>|displacement|²</c> instead, essentially never near zero for
    /// a real separation. That substitution, not the quadratic formula's own cancellation
    /// avoidance, is what "numerically stable" refers to here — see the trap on
    /// <c>docs/PLAN-sticky-bullets.md</c>'s B3 before "fixing" it.</para></summary>
    public static bool TryIntercept(Vector3 muzzlePos, float speed, Vector3 targetPos, Vector3 relVel,
        out Vector3 aimDir, out float t)
    {
        aimDir = Vector3.Zero;
        t = 0f;

        Vector3 displacement = targetPos - muzzlePos;
        float a = displacement.LengthSquared(); // u's leading coefficient — see the doc comment
        if (a < 1e-6f)
        {
            return false; // zero separation: nothing to aim at
        }
        float b = 2f * relVel.Dot(displacement);
        float c = relVel.LengthSquared() - speed * speed;
        float disc = b * b - 4f * a * c;
        if (disc < 0f)
        {
            return false; // no real root: the target outruns the round on every heading
        }

        // The stable (Citardauq) quadratic form: avoids subtracting near-equal quantities when b
        // and sqrt(disc) are close in magnitude, which the textbook (-b±sqrt(disc))/2a form does
        // not — do not replace this with the textbook formula (docs/PLAN-sticky-bullets.md B3's trap).
        float sq = Mathf.Sqrt(disc);
        float q = -0.5f * (b + (b >= 0f ? sq : -sq));
        float u1 = q / a;
        float u2 = Mathf.IsZeroApprox(q) ? float.NaN : c / q;

        // u = 1/t, so only a positive u is a forward-time solution; the larger positive root is
        // the smaller t — the earliest intercept.
        float u = u1 > 0f && u2 > 0f ? Mathf.Max(u1, u2) : u1 > 0f ? u1 : u2 > 0f ? u2 : float.NaN;
        if (float.IsNaN(u) || u <= 0f)
        {
            return false; // both roots non-positive: the target outran the round in this geometry
        }

        t = 1f / u;
        Vector3 dir = relVel + displacement / t;
        if (dir.LengthSquared() < 1e-12f)
        {
            t = 0f;
            return false;
        }
        aimDir = dir.Normalized();
        return true;
    }
}
