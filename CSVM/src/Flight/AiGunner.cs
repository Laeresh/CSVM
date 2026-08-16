using Godot;

namespace CSVM.Flight;

/// <summary>The AI's forward-gun gunnery: the lead-sphere accuracy model and the fire gates,
/// decoded in docs/org/aiPilot/aiWeapons.md, with their authored values in
/// docs/formats/ai-rosters.md (<c>ai_skill_parameters</c>) and docs/formats/vehicle.md
/// (<c>gun_pitch</c>/<c>gun_yaw</c>). Per sim tick the host
/// <see cref="FlightController"/> hands it the fire geometry (<see cref="Solve"/>); the gunner
/// answers with the trigger (<see cref="WantsFire"/>) and the lead direction, perturbed inside
/// the dead-eye cone (<see cref="ShotDirection"/>, one draw per shot). The lead solve is
/// <see cref="AimAssist.TryIntercept"/>, the same constant-velocity solver the player assist and
/// the turret gunners consume.
/// ⚠ <see cref="Target"/> is a plain mutable field by design: the original's mission script
/// retargets an AI at runtime, so orders are never read-once.</summary>
public sealed class AiGunner
{
    /// <summary>The shipped forward-gun traverse limit: <c>gun_pitch</c>/<c>gun_yaw</c> are
    /// <c>[-11, 11]</c> degrees on every AI aircraft def (docs/formats/vehicle.md).
    /// ⚠ These CLAMP the aim, they do not veto the shot: what fires is the residual left over
    /// after clamping, against <see cref="AimQualityCos"/> (docs/org/aiPilot/aiWeapons.md).</summary>
    public const float GunConeHalfAngleDeg = 11f;

    /// <summary>The gun aim-quality gate: the clamped mount aim must sit within
    /// <c>acos(0.9848)</c>, 10°, of the lead direction. The original's own literal, per weapon
    /// class — ordnance takes the tighter <see cref="AiRocketeer.AimQualityCos"/>.</summary>
    public const float AimQualityCos = 0.9848f;

    /// <summary>The standing target — mutable at any time (the mission-script seam). Null with
    /// <see cref="AutoTarget"/> set lets the host re-acquire through the D12 target ranking
    /// (<see cref="AiTargetRanking"/>).</summary>
    public FlightController? Target;

    /// <summary>Re-acquire through the D12 ranking when <see cref="Target"/> is null or dead.
    /// Off, a cleared target simply holds fire — an explicitly ordered gunner.</summary>
    public bool AutoTarget = true;

    /// <summary>The roster's assigned target (slot 6, <c>primary_target</c>), by node name —
    /// mutable, the mission-script seam. While it resolves to a live hostile inside the
    /// activation radius it is picked outright; ranking takes over when it dies or leaves
    /// (the reading is assumed — see <c>FlightController.SelectRankedTarget</c>).
    /// <c>"player"</c> resolves to any human-piloted aircraft. Null/empty = none.</summary>
    public string? PrimaryTargetName;

    /// <summary>The roster's <c>rating_biases</c> (slot 33), mutable; null = none. Matched
    /// against candidate node names by <see cref="AiTargetRanking.ObjectiveBiasFor"/>. An
    /// <c>--ai</c>/egen spawn carries no roster block and so no biases — the documented gap
    /// until mission spawns attach roster identities.</summary>
    public System.Collections.Generic.IReadOnlyList<Mech3.AiRatingBias>? RatingBiases;

    /// <summary>Dead-eye aim-error cone half-angle, degrees — <c>ai_skill_parameters</c>'s
    /// <c>dead_eye_angle</c> at the pilot's rating (4.0° at 1, 1.45° at 9; the default is the
    /// worst rating, matching the 89 shipped mook blocks whose only authored skill is
    /// <c>dead_eye 1</c>).</summary>
    public float DeadEyeAngleDeg = 4f;

    /// <summary>Quick-draw shot-acceptance cone half-angle off the target's nose/tail axis,
    /// degrees — <c>quick_draw_angle</c> at the pilot's rating (50° at 1, 89° at 9). The
    /// same-named <c>quick_draw_chance</c> is not a gun term at all: it is the per-launch
    /// ordnance roll (docs/org/aiPilot/aiWeapons.md), so nothing here consumes it. The cone's
    /// aircraft-against-aircraft scope is on <see cref="QuickDrawAccepts"/>.</summary>
    public float QuickDrawAngleDeg = 50f;

    /// <summary>Traverse yaw half-limit, degrees (<c>gun_yaw</c>).</summary>
    public float GunYawLimitDeg = GunConeHalfAngleDeg;

    /// <summary>Traverse pitch half-limit, degrees (<c>gun_pitch</c>).</summary>
    public float GunPitchLimitDeg = GunConeHalfAngleDeg;

    /// <summary>The engagement window, metres: the separation the gun is willing to shoot
    /// across. Authored per weapon slot in the vehicle def's <c>weapons</c> tuple, and 1 to 900 m
    /// on every AI gun the game ships (docs/org/aiPilot/aiWeapons.md); a per-vehicle window
    /// arrives with the tuple itself (<c>BL-394</c>). The floor is authored, not a sentinel.</summary>
    public float MinRangeM = 1f;

    /// <summary>The engagement window's far end, metres (see <see cref="MinRangeM"/>).</summary>
    public float MaxRangeM = 900f;

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

    /// <summary>The quick-draw gate at an explicit cone, so <see cref="AiRocketeer"/> runs this
    /// implementation rather than a second copy: the original applies the quick draw once at the
    /// top of the fire decision, ahead of the weapon walk, so it governs both weapon classes.
    /// The instance overload below is the gun's own call, at this gunner's cone.</summary>
    public static bool QuickDrawAccepts(Vector3 ownPos, Vector3 targetPos, Vector3 targetForward,
        float angleDeg)
    {
        var away = ownPos - targetPos;
        if (away.LengthSquared() < 1e-6f || targetForward.LengthSquared() < 1e-6f)
            return true;
        // |cos| covers both cones at once — the fore and aft cones are mirror images and the
        // shipped angles never exceed 89°.
        float cos = Mathf.Abs(away.Normalized().Dot(targetForward.Normalized()));
        return cos >= Mathf.Cos(Mathf.DegToRad(angleDeg));
    }

    /// <summary>Clears the trigger — no target, no fire step this tick.</summary>
    public void HoldFire() => WantsFire = false;

    /// <summary>One tick's fire decision, in the original's gate order. All world-space;
    /// <paramref name="ownBasis"/> is the firing airframe's attitude (the traverse clamp is
    /// measured against ITS nose, not the muzzle axis), <paramref name="targetForward"/> the
    /// target's nose axis (the quick-draw cones project fore and aft from the target).</summary>
    public void Solve(Vector3 muzzlePos, Vector3 ownVelocity, Basis ownBasis,
        Vector3 targetPos, Vector3 targetVelocity, Vector3 targetForward,
        float roundSpeed)
    {
        WantsFire = false;
        if (!QuickDrawAccepts(muzzlePos, targetPos, targetForward))
            return; // too oblique an attack for this pilot's quick draw
        // The engine gates on the separation ITSELF against the slot's authored window, both
        // ends squared at parse time — not on whether the round reaches the intercept.
        float sep2 = muzzlePos.DistanceSquaredTo(targetPos);
        if (sep2 < MinRangeM * MinRangeM || sep2 > MaxRangeM * MaxRangeM)
            return;
        if (!AimAssist.TryIntercept(muzzlePos, roundSpeed, targetPos,
                targetVelocity - ownVelocity, out var aim, out float t))
            return; // a target outrunning the round is simply not shot at
        AimDirWorld = aim;
        InterceptPoint = targetPos + targetVelocity * t; // where the target will be at impact
        // Clamp the aim into the airframe's traverse limits, then gate on what the clamp had to
        // give away. A lead past the limits still fires while the residual stays inside the
        // gate, which is why the employable cone is the limit PLUS 10°, not the limit.
        var local = (ownBasis.Orthonormalized().Transposed() * aim).Normalized();
        var (yawDeg, pitchDeg) = TurretController.AnglesOfLocal(local);
        var clamped = TurretController.LocalDir(
            Mathf.Clamp(yawDeg, -GunYawLimitDeg, GunYawLimitDeg),
            Mathf.Clamp(pitchDeg, -GunPitchLimitDeg, GunPitchLimitDeg));
        if (clamped.Dot(local) < AimQualityCos)
            return; // the mount cannot be brought close enough: keep maneuvering
        WantsFire = true;
    }

    /// <summary>The quick-draw gate alone: true when the shooter sits inside the cone of
    /// <see cref="QuickDrawAngleDeg"/> about the target's nose axis or its tail axis. A
    /// degenerate zero separation passes (the geometry is meaningless there and the range gate
    /// owns that case). ⚠ The original applies this only aircraft-against-aircraft, a condition
    /// <see cref="Target"/>'s type satisfies rather than tests; it needs a real test once a
    /// gasbag or a ground target can be aimed at (<c>BL-395</c>).</summary>
    public bool QuickDrawAccepts(Vector3 ownPos, Vector3 targetPos, Vector3 targetForward) =>
        QuickDrawAccepts(ownPos, targetPos, targetForward, QuickDrawAngleDeg);

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
