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

/// <summary>The per-muzzle gun aim assist: slot state (above) plus the per-frame forget +
/// catch-up pass (docs/org/aim-assist.md "Per frame — <c>FUN_004b3e50</c>"). B3 adds the
/// intercept solver and B4 the candidate scorer to this same file, so the whole assist stays one
/// testable unit that <see cref="FlightController"/> calls into.</summary>
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
}
