using Godot;

namespace CSVM.Flight;

/// <summary>The AI's forward-gun gunnery (M4 D14): the lead-sphere accuracy model and the
/// shot-angle cones, decoded in docs/formats/ai-rosters.md ("ai_skill_parameters") and
/// docs/formats/vehicle.md (gun_pitch/gun_yaw). Per sim tick the host
/// <see cref="FlightController"/> hands it the fire geometry (<see cref="Solve"/>); the gunner
/// answers with the trigger (<see cref="WantsFire"/>) and the lead direction, and every round
/// that goes out perturbs that lead inside the dead-eye cone (<see cref="ShotDirection"/>, one
/// draw per shot). The lead solve is <see cref="AimAssist.TryIntercept"/> — the same
/// constant-velocity solver the player assist and the turret gunners consume, never re-derived.
///
/// <para>The fire gates, in order: an intercept the round can reach inside the weapon's RANGE;
/// the lead direction inside the airframe's forward gun cone (<c>gun_pitch</c>/<c>gun_yaw</c>,
/// ±11° on every shipped AI aircraft — a hard gate, the AI does not fire off-boresight); and
/// the quick-draw cone — the shot is taken only within <see cref="QuickDrawAngleDeg"/> of the
/// TARGET's nose or tail axis (head-on and rear shots are the safe ones; beam shots need a
/// confident pilot: 50° at rating 1, 89° at 9).</para>
///
/// <para><see cref="Target"/> is a plain mutable field BY DESIGN — the original's mission
/// script retargets an AI at runtime (<c>ADD_OTHER_TARGET</c>…), so orders are never read-once.
/// The dead-eye rng is the gunner's own seeded stream, so a fixed-seed run scatters
/// identically.</para></summary>
public sealed class AiGunner
{
    /// <summary>The shipped forward-gun cone half-angle: <c>gun_pitch</c>/<c>gun_yaw</c> are
    /// <c>[-11, 11]</c> degrees on every AI aircraft def (docs/formats/vehicle.md).</summary>
    public const float GunConeHalfAngleDeg = 11f;

    /// <summary>The standing target — mutable at any time (the mission-script seam). Null with
    /// <see cref="AutoTarget"/> set lets the host re-acquire the nearest hostile aircraft.</summary>
    public FlightController? Target;

    /// <summary>Re-acquire the nearest hostile when <see cref="Target"/> is null or dead. Off,
    /// a cleared target simply holds fire — an explicitly ordered gunner.</summary>
    public bool AutoTarget = true;

    /// <summary>Dead-eye aim-error cone half-angle, degrees — <c>ai_skill_parameters</c>'s
    /// <c>dead_eye_angle</c> at the pilot's rating (4.0° at 1, 1.45° at 9; the default is the
    /// worst rating, matching the 89 shipped mook blocks whose only authored skill is
    /// <c>dead_eye 1</c>).</summary>
    public float DeadEyeAngleDeg = 4f;

    /// <summary>Quick-draw shot-acceptance cone half-angle off the target's nose/tail axis,
    /// degrees — <c>quick_draw_angle</c> at the pilot's rating (50° at 1, 89° at 9).</summary>
    public float QuickDrawAngleDeg = 50f;

    /// <summary>Forward-cone yaw half-limit, degrees (<c>gun_yaw</c>).</summary>
    public float GunYawLimitDeg = GunConeHalfAngleDeg;

    /// <summary>Forward-cone pitch half-limit, degrees (<c>gun_pitch</c>).</summary>
    public float GunPitchLimitDeg = GunConeHalfAngleDeg;

    private readonly RandomNumberGenerator _rng;

    public AiGunner(RandomNumberGenerator rng)
    {
        _rng = rng;
    }

    /// <summary>True when this tick's geometry passed every fire gate — the AI's trigger.</summary>
    public bool WantsFire { get; private set; }

    /// <summary>The unperturbed world-space lead direction of the last <see cref="Solve"/> with
    /// a solution. Valid while <see cref="WantsFire"/>; each shot leaves along
    /// <see cref="ShotDirection"/>, never along this exactly.</summary>
    public Vector3 AimDirWorld { get; private set; }

    /// <summary>The world-space intercept point of the last solution — where the lead says the
    /// round meets the target. Each barrel converges on this point (<see cref="ShotDirection"/>),
    /// so wing-mounted guns do not fire parallel lines that straddle the fuselage.</summary>
    public Vector3 InterceptPoint { get; private set; }

    /// <summary>Clears the trigger — no target, no fire step this tick.</summary>
    public void HoldFire() => WantsFire = false;

    /// <summary>One tick's fire decision. All world-space; <paramref name="ownBasis"/> is the
    /// firing airframe's attitude (the gun cone is measured against ITS nose, not the muzzle
    /// axis), <paramref name="targetForward"/> the target's nose axis (the quick-draw cones
    /// project fore and aft from the target).</summary>
    public void Solve(Vector3 muzzlePos, Vector3 ownVelocity, Basis ownBasis,
        Vector3 targetPos, Vector3 targetVelocity, Vector3 targetForward,
        float roundSpeed, float range)
    {
        WantsFire = false;
        if (!AimAssist.TryIntercept(muzzlePos, roundSpeed, targetPos,
                targetVelocity - ownVelocity, out var aim, out float t))
            return; // a target outrunning the round is simply not shot at
        float reach = roundSpeed * t;
        if (reach * reach > range * range)
            return; // the round cannot reach the intercept inside authored RANGE
        AimDirWorld = aim;
        InterceptPoint = targetPos + targetVelocity * t; // where the target will be at impact
        var local = (ownBasis.Orthonormalized().Transposed() * aim).Normalized();
        var (yawDeg, pitchDeg) = TurretController.AnglesOfLocal(local);
        if (Mathf.Abs(yawDeg) > GunYawLimitDeg || Mathf.Abs(pitchDeg) > GunPitchLimitDeg)
            return; // outside the forward gun cone: keep maneuvering, do not fire
        if (!QuickDrawAccepts(muzzlePos, targetPos, targetForward))
            return; // too oblique an attack for this pilot's quick draw
        WantsFire = true;
    }

    /// <summary>The quick-draw gate alone: true when the shooter sits inside the cone of
    /// <see cref="QuickDrawAngleDeg"/> about the target's nose axis or its tail axis. A
    /// degenerate zero separation passes (the geometry is meaningless there and the range gate
    /// owns that case).</summary>
    public bool QuickDrawAccepts(Vector3 ownPos, Vector3 targetPos, Vector3 targetForward)
    {
        var away = ownPos - targetPos;
        if (away.LengthSquared() < 1e-6f || targetForward.LengthSquared() < 1e-6f)
            return true;
        // |cos| covers both cones at once — the fore and aft cones are mirror images and the
        // shipped angles never exceed 89°.
        float cos = Mathf.Abs(away.Normalized().Dot(targetForward.Normalized()));
        return cos >= Mathf.Cos(Mathf.DegToRad(QuickDrawAngleDeg));
    }

    /// <summary>One round's launch direction from one barrel: the line from THIS muzzle to the
    /// solved intercept point (wing guns converge rather than firing parallel), perturbed
    /// inside the dead-eye cone — uniform in the polar angle, the engine's own scatter shape
    /// (<see cref="AimAssist.Scatter"/>), one rng draw pair per shot.</summary>
    public Vector3 ShotDirection(Vector3 muzzlePos)
    {
        var toIntercept = InterceptPoint - muzzlePos;
        var line = toIntercept.LengthSquared() > 1e-6f ? toIntercept.Normalized() : AimDirWorld;
        return AimAssist.Scatter(line, Mathf.DegToRad(DeadEyeAngleDeg), _rng);
    }
}
