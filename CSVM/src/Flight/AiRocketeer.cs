using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>One pylon as <see cref="AiRocketeer"/> sees it: enough to run the gates without the
/// engine. <see cref="Index"/> is the <see cref="Loadout"/> hardpoint index the decision names to
/// <see cref="FireControl.SelectPylon"/>; <see cref="MountPos"/> is where the round leaves from,
/// so the range window and the lead are measured from the pylon, not the fuselage.</summary>
public readonly struct RocketPylonView
{
    public int Index { get; init; }

    /// <summary>The weapon's <c>DAMAGES_ZEPPELIN</c> bit, which decides what it may be aimed at.</summary>
    public bool DamagesZeppelin { get; init; }

    /// <summary>Ammo remains on this pylon (or the run is on infinite ammo).</summary>
    public bool Armed { get; init; }

    public Vector3 MountPos { get; init; }

    /// <summary>Muzzle speed of the round, for the lead solve.</summary>
    public float RoundSpeed { get; init; }
}

/// <summary>The AI's ordnance employment: when a non-human pilot pulls the rocket trigger, decoded
/// in docs/org/aiPilot/aiWeapons.md. The gun half is <see cref="AiGunner"/>, which this deliberately
/// does not arbitrate with: the original couples the two because they share one mount and one aim
/// vector, and ours do not. Per sim tick the host <see cref="FlightController"/> ticks the lockout
/// and hands over the fire geometry (<see cref="Solve"/>); the answer is the trigger
/// (<see cref="WantsFire"/>), the pylon it chose (<see cref="SelectedPylon"/>) and the direction the
/// round leaves along (<see cref="LaunchDirWorld"/>).
/// ⚠ It holds no target of its own. The original validates ONE target and then walks every weapon
/// slot against it, so the host passes <see cref="AiGunner.Target"/>'s geometry rather than letting
/// this acquire, which is the only way the guns and the rockets cannot chase different aircraft.</summary>
public sealed class AiRocketeer
{
    /// <summary>The ordnance aim-quality gate: the clamped mount aim must sit within
    /// <c>acos(0.9962)</c>, 5°, of the lead. The original's own literal, and tighter than the gun's
    /// <see cref="AiGunner.AimQualityCos"/> by design.</summary>
    public const float AimQualityCos = 0.9962f;

    /// <summary>The shot-acceptance cone off the target's nose/tail axis, degrees. The original
    /// applies this at the TOP of the fire decision, before the weapon walk, so it gates the
    /// ordnance and the guns alike (<see cref="AiGunner.QuickDrawAngleDeg"/> carries the same
    /// value for the gun path).</summary>
    public float QuickDrawAngleDeg = 50f;

    /// <summary>Per-launch ordnance dice: <c>quick_draw_chance</c> at the pilot's rating (0.05 at
    /// 1, 0.44 at 9). The parameter's only consumer, and not a gun term at all.
    /// ⚠ The roll is drawn AFTER the lockout is stamped, so failing it costs the whole refire
    /// interval rather than retrying next tick (see <see cref="Solve"/>).</summary>
    public float QuickDrawChance = 0.05f;

    /// <summary>Traverse yaw half-limit, degrees (<c>gun_yaw</c>) — the ordnance fires from a gun
    /// mount and clamps into the same band.</summary>
    public float PylonYawLimitDeg = AiGunner.GunConeHalfAngleDeg;

    /// <summary>Traverse pitch half-limit, degrees (<c>gun_pitch</c>).</summary>
    public float PylonPitchLimitDeg = AiGunner.GunConeHalfAngleDeg;

    /// <summary>The engagement window's near end, metres: 200 m on every militia variant the game
    /// ships (30 m on the five base defs that author fields 3 and 4 transposed). A per-vehicle
    /// window arrives with the <c>weapons</c> tuple itself (<c>BL-394</c>).
    /// ⚠ The window is a BAND. A max-range-only gate lets an AI launch from the merge.</summary>
    public float MinRangeM = 200f;

    /// <summary>The engagement window's far end, metres (see <see cref="MinRangeM"/>).</summary>
    public float MaxRangeM = 800f;

    /// <summary>The vehicle-wide ordnance lockout, seconds — 30 on the shipped militia variants,
    /// 5 for the Black Hat torpedo. Vehicle-wide, not per pylon, so a plane carrying two rocket
    /// types cannot alternate them. Our loadout is pylons rather than the original's weapon-type
    /// slots, so this doubles as the per-slot timer while one ordnance type is carried; a mixed
    /// loadout wants one timer per weapon, and arrives with <c>BL-394</c>.</summary>
    public float RefireSeconds = 30f;

    private readonly Func<float> _roll;
    private float _lockout;

    /// <summary>The dice source is a delegate, not the engine's rng object: the roll is a decoded
    /// gate and has to be assertable without a Godot runtime, which a
    /// <see cref="RandomNumberGenerator"/> is not. The session hands it a seeded stream's
    /// <c>Randf</c>; a test hands it a scripted sequence.</summary>
    public AiRocketeer(Func<float> roll)
    {
        _roll = roll;
    }

    /// <summary>True when this tick's geometry passed every gate and the dice came up — the AI's
    /// rocket trigger, replacing the human one for a non-human pilot.</summary>
    public bool WantsFire { get; private set; }

    /// <summary>The hardpoint the walk settled on, or -1. Set whenever a pylon clears the
    /// deterministic gates, whether or not the roll then passed: the original takes the weapon
    /// selection during the trigger pass and rolls afterwards, in the shot routine.</summary>
    public int SelectedPylon { get; private set; } = -1;

    /// <summary>World-space direction the round leaves along while <see cref="WantsFire"/>: the
    /// lead clamped into the traverse band, which is what the original's mount fires along.
    /// ⚠ Our pylon does not rotate to match, so the mounted body and the round it becomes point
    /// up to the traverse limit apart at the launch instant (<c>BL-404</c>).</summary>
    public Vector3 LaunchDirWorld { get; private set; }

    /// <summary>Seconds until ordnance may fire again — the lockout, for tests and breadcrumbs.</summary>
    public float LockoutRemaining => _lockout;

    /// <summary>Clears the trigger and ages the lockout. Called every tick, including the ticks
    /// where no fire decision runs at all, so the lockout is a vehicle timer rather than one that
    /// stops whenever the AI loses its target or leaves Pursue.</summary>
    public void Tick(float dt)
    {
        WantsFire = false;
        if (_lockout > 0f)
        {
            _lockout -= dt;
        }
    }

    /// <summary>One tick's ordnance decision, in the original's gate order: the quick-draw cone
    /// aborts the whole pass, then each pylon must be armed, match the target's gasbag class, sit
    /// inside the range band and bring the clamped aim within <see cref="AimQualityCos"/>. The
    /// first pylon clearing all of it takes the selection.
    /// ⚠ The lockout is stamped BEFORE the dice, so a failed roll has already spent the full
    /// <see cref="RefireSeconds"/>. Stamp it after and the attempt becomes per-tick.</summary>
    public void Solve(Vector3 ownPos, Vector3 ownVelocity, Basis ownBasis,
        Vector3 targetPos, Vector3 targetVelocity, Vector3 targetForward, bool targetIsGasbag,
        IReadOnlyList<RocketPylonView> pylons)
    {
        WantsFire = false;
        SelectedPylon = -1;
        if (_lockout > 0f)
            return;
        if (!AiGunner.QuickDrawAccepts(ownPos, targetPos, targetForward, QuickDrawAngleDeg))
            return; // too oblique an attack for this pilot's quick draw
        var basis = ownBasis.Orthonormalized();
        var toLocal = basis.Transposed();
        foreach (var p in pylons)
        {
            if (!p.Armed)
                continue;
            // The DAMAGES_ZEPPELIN match, both ways: a torpedo is offered ONLY against a gasbag,
            // and a plain rocket only against something else. Getting this wrong is how the Black
            // Hat Warhawk's eight torpedoes end up aimed at an aircraft.
            if (p.DamagesZeppelin != targetIsGasbag)
                continue;
            float sep2 = p.MountPos.DistanceSquaredTo(targetPos);
            if (sep2 < MinRangeM * MinRangeM || sep2 > MaxRangeM * MaxRangeM)
                continue;
            if (!AimAssist.TryIntercept(p.MountPos, p.RoundSpeed, targetPos,
                    targetVelocity - ownVelocity, out var aim, out _))
                continue; // a target outrunning the round is simply not shot at
            var local = (toLocal * aim).Normalized();
            var (yawDeg, pitchDeg) = TurretController.AnglesOfLocal(local);
            var clamped = TurretController.LocalDir(
                Mathf.Clamp(yawDeg, -PylonYawLimitDeg, PylonYawLimitDeg),
                Mathf.Clamp(pitchDeg, -PylonPitchLimitDeg, PylonPitchLimitDeg));
            if (clamped.Dot(local) < AimQualityCos)
                continue; // the mount cannot be brought close enough: keep maneuvering
            SelectedPylon = p.Index;
            LaunchDirWorld = (basis * clamped).Normalized();
            _lockout = RefireSeconds;
            // The dice, last and only here. Inclusive, as the original's compare is.
            WantsFire = _roll() <= QuickDrawChance;
            return;
        }
    }
}
