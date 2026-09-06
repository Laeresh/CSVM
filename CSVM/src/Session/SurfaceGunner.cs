using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The gun a <c>mode ship</c> hull carries: the acquisition, the mount and the fire decision a
/// patrol boat and a turret truck run, decoded in docs/org/aiPilot.md ("What a <c>mode ship</c>
/// vehicle runs") and docs/org/aiPilot/aiWeapons.md. One per armed
/// <see cref="SurfaceVehicle"/>, stepped by it.
/// ⚠ A hull NEVER pursues and NEVER applies the quick draw, so this carries neither
/// <see cref="FlightController"/>'s Pursue-only gate nor a call to <see cref="AiGunner.Solve"/>,
/// whose first act is that gate. It shoots while flying its net. What it shares with the aircraft
/// path is the lead solver and the aim-quality threshold, not the gate order.
/// ⚠ The def's <c>attack_dwell</c> and <c>not_pursuit_dwell</c> are deliberately unread: both feed
/// the no-pursuit-before timestamp, which a class that never pursues never reaches.
/// </summary>
internal sealed class SurfaceGunner
{
    /// <summary>How long a picked target is held before the sweep may replace it, seconds — the
    /// hardcoded value at <c>FUN_004b0f20</c>. ⚠ This, not the def's dwell fields, is what paces a
    /// hull's target churn.</summary>
    public const float TargetHoldSeconds = 20f;

    /// <summary>The aim-error cone half-angle, degrees. A surface block authors <c>-1</c> in all
    /// nine skill slots (<c>dead_eye</c> included), so this is CSVM's standing unauthored-rating
    /// convention — the worst rating, the same default <see cref="AiGunner.DeadEyeAngleDeg"/>
    /// carries — and NOT a decoded boat-specific value. What the engine does with a <c>-1</c>
    /// rating is unread.</summary>
    public const float DeadEyeAngleDeg = 4f;

    private readonly SurfaceVehicle _vessel;
    private readonly ProjectilePool _pool;
    private readonly WeaponDef _weapon;
    private readonly AiWeaponSlot _slot;
    private readonly float _activationRange;
    private readonly Node3D _turretNode;
    private readonly Node3D _gunNode;
    private readonly Node3D _firepoint;
    private readonly Transform3D _turretRest;
    private readonly Transform3D _gunRest;
    private readonly RandomNumberGenerator _rng;
    private readonly AimCandidateSet _scan = new();
    private readonly List<RankedTargetCandidate> _ranked = new();
    private readonly List<AimCandidate> _sources = new();

    // The mount's aim in the HULL frame, which is the frame the engine slews and guards in; it
    // starts down the hull's nose, the mount's own authored rest direction (0, 0, -1).
    private Vector3 _aimLocal = Vector3.Forward;
    private object? _target;
    private float _holdLeft;
    private float _fireIn;
    private bool _firstShotLogged;

    private SurfaceGunner(SurfaceVehicle vessel, ProjectilePool pool, WeaponDef weapon,
        AiWeaponSlot slot, float activationRange, Node3D turretNode, Node3D gunNode,
        Node3D firepoint)
    {
        _vessel = vessel;
        _pool = pool;
        _weapon = weapon;
        _slot = slot;
        _activationRange = activationRange;
        _turretNode = turretNode;
        _gunNode = gunNode;
        _firepoint = firepoint;
        _turretRest = turretNode.Transform;
        _gunRest = gunNode.Transform;
        _rng = Rng.Stream(Rng.Weapons);
        Ammo = slot.Rounds;
    }

    /// <summary>Rounds left in the def's authored magazine (9000 on both surface defs, so
    /// effectively unlimited); a spent gun holds fire rather than reloading.</summary>
    public int Ammo { get; private set; }

    public int ShotsFired { get; private set; }

    /// <summary>What the acquisition last picked, or null while nothing ranks — the identity the
    /// suite reads rather than inferring the pick from where the barrel points.</summary>
    public object? Target => _target;

    /// <summary>The mount's aim in the hull's own frame, the value the guards and the slew act on
    /// and the aim-quality gate is measured from.</summary>
    public Vector3 AimLocal => _aimLocal;

    /// <summary>Where a round actually leaves: the model's own <c>firepoint</c> marker. Exposed so
    /// the suite can read the muzzle rather than infer it from where rounds appear, the same
    /// reason <see cref="TurretController.PlatformColliderRids"/> is reachable.</summary>
    public Vector3 MuzzlePosition => _firepoint.GlobalPosition;

    /// <summary>The two nodes the mount poses, for the suite's rotated-from-rest reading.</summary>
    public (Node3D Turret, Node3D Gun) MountNodes => (_turretNode, _gunNode);

    /// <summary>Builds the gunner for <paramref name="vessel"/>, or null when this hull is not an
    /// armed one. Every reason to decline is a missing input, never a policy: no pool or catalogue
    /// wired, no authored <c>weapons</c> block, an unknown weapon id, or no mount chain in the
    /// model. ⚠ The node lookup is recursive by name (<c>FUN_004761c0</c>): on both shipped defs
    /// the chain sits under <c>healthy</c>, so a direct-child lookup silences every boat.</summary>
    public static SurfaceGunner? Build(SurfaceVehicle vessel, ProjectilePool? pool,
        WeaponDefs? weapons, IReadOnlyList<AiWeaponSlot> fit, float activationRange)
    {
        if (pool == null || weapons == null || fit.Count == 0)
        {
            return null;
        }
        // The first slot: a hull's fit is one gun, and the engine's own fire decision considers
        // only the first cannon in the list in any case (aiWeapons.md, "The trigger routine").
        var slot = fit[0];
        if (weapons.Get(slot.WeaponId) is not { } weapon)
        {
            GD.Print($"surface: '{vessel.Name}' unarmed: no weapon def '{slot.WeaponId}'");
            return null;
        }
        var turret = FindDescendant(vessel.Body, "turret");
        var gun = FindDescendant(vessel.Body, "gun");
        var firepoint = gun != null ? FindDescendant(gun, "firepoint") : null;
        if (turret == null || gun == null || firepoint == null)
        {
            GD.Print($"surface: '{vessel.Name}' unarmed: model carries no turret/gun/firepoint chain");
            return null;
        }
        return new SurfaceGunner(vessel, pool, weapon, slot, activationRange, turret, gun, firepoint);
    }

    /// <summary>One sim step: age the clocks, hold or re-acquire the target, aim the mount, and
    /// fire when every gate is open. Called only for a woken, undestroyed hull.</summary>
    public void Step(float dt)
    {
        _fireIn -= dt;
        _holdLeft -= dt;
        if (!Acquire(out var targetPos, out var targetVel))
        {
            return; // nothing ranks: the mount holds where it is, the way an idle turret does
        }

        var basis = _vessel.Body.GlobalTransform.Basis.Orthonormalized();
        var muzzle = _firepoint.GlobalPosition;
        float roundSpeed = _weapon.Velocity ?? ProjectilePool.DefaultVelocity;
        // The lead is solved on the target's velocity RELATIVE to the hull, the CANNON row of the
        // decoded solver table (aiWeapons.md). No solution means the round cannot catch it, and
        // the engine points the mount back down its own axis rather than at a straight bearing.
        if (!AimAssist.TryIntercept(muzzle, roundSpeed, targetPos, targetVel - _vessel.Velocity,
                out var aimWorld, out _))
        {
            AimAt(basis, Vector3.Forward, dt);
            return;
        }

        var rawLocal = (basis.Transposed() * aimWorld).Normalized();
        AimAt(basis, rawLocal, dt);
        // The residual is measured against the RAW lead, so the angle the elevation guards gave
        // away is charged to the shot exactly as an aeroplane's traverse clamp is.
        if (SurfaceGunMount.AimQuality(_aimLocal, rawLocal) < AiGunner.AimQualityCos)
        {
            return;
        }
        // The engine gates on the separation itself against the slot's authored window, both ends
        // squared at parse time.
        float sep2 = muzzle.DistanceSquaredTo(targetPos);
        if (sep2 < _slot.MinRangeM * _slot.MinRangeM || sep2 > _slot.MaxRangeM * _slot.MaxRangeM)
        {
            return;
        }
        if (_fireIn > 0f || Ammo <= 0)
        {
            return;
        }

        Fire(basis);
    }

    // A recursive find-by-name under a subtree, the shape FUN_004761c0 takes: the whole subtree,
    // not the immediate children.
    private static Node3D? FindDescendant(Node3D root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Node3D node)
            {
                continue;
            }
            if (string.Equals(node.Name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
            if (FindDescendant(node, name) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static void CollectRids(Node node, Godot.Collections.Array<Rid> into)
    {
        if (node is CollisionObject3D body)
        {
            into.Add(body.GetRid());
        }
        foreach (var child in node.GetChildren())
        {
            CollectRids(child, into);
        }
    }

    // Guards the desired direction into the mount's elevation band, slews the mount toward it and
    // writes the pose onto the two nodes, so the barrel a player sees is the barrel that shoots.
    private void AimAt(Basis basis, Vector3 desiredLocal, float dt)
    {
        var guarded = SurfaceGunMount.Guard(desiredLocal);
        _aimLocal = SurfaceGunMount.Slew(_aimLocal, guarded, dt);
        var (yawDeg, pitchDeg) = TurretController.AnglesOfLocal(_aimLocal);
        _turretNode.Transform = new Transform3D(
            _turretRest.Basis * new Basis(Vector3.Up, Mathf.DegToRad(yawDeg)), _turretRest.Origin);
        _gunNode.Transform = new Transform3D(
            _gunRest.Basis * new Basis(Vector3.Right, Mathf.DegToRad(pitchDeg)), _gunRest.Origin);
    }

    // Keeps the standing target while the 20 s hold runs and it is still live, otherwise sweeps
    // the three candidate pools and ranks them with the non-jet scorer. The geometry comes back
    // out of the fresh scan rather than off the held object, so one candidate shape serves every
    // target class and nothing has to type-switch on what a hull is shooting at.
    private bool Acquire(out Vector3 targetPos, out Vector3 targetVel)
    {
        targetPos = Vector3.Zero;
        targetVel = Vector3.Zero;
        int ownTeam = _vessel.Team ?? AimAssist.NeutralTeam;
        if (ownTeam == AimAssist.NeutralTeam)
        {
            _target = null;
            return false; // an unowned hull is nobody's enemy, on either side of the gate
        }

        _scan.Clear();
        _pool.CollectVehicleList(_scan);
        _pool.CollectTurrets(_scan);
        _pool.CollectMissionStructures(_scan);
        _ranked.Clear();
        _sources.Clear();
        Collect(_scan.Vehicles, ownTeam, isTurret: false, aircraftOnly: true);
        Collect(_scan.Turrets, ownTeam, isTurret: true, aircraftOnly: false);
        Collect(_scan.Structures, ownTeam, isTurret: false, aircraftOnly: false);

        if (_target != null && _holdLeft > 0f)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                if (ReferenceEquals(_sources[i].Source, _target))
                {
                    targetPos = _sources[i].Position;
                    targetVel = _sources[i].Velocity;
                    return true;
                }
            }
        }

        int best = AiTargetRanking.SelectBest(_vessel.Position, -_vessel.Body.GlobalTransform.Basis.Z,
            _activationRange, AiScorer.Other, _ranked, out _);
        if (best < 0)
        {
            _target = null;
            return false;
        }

        _target = _sources[best].Source;
        _holdLeft = TargetHoldSeconds;
        targetPos = _sources[best].Position;
        targetVel = _sources[best].Velocity;
        return true;
    }

    // One pool's hostile, live members as rank candidates. The vehicle pool carries the session's
    // hulls beside its aircraft; aircraftOnly drops the hulls, matching the limit the aircraft
    // path already has, so a boat does not shoot another boat.
    private void Collect(List<AimCandidate> pool, int ownTeam, bool isTurret, bool aircraftOnly)
    {
        foreach (var c in pool)
        {
            if (!c.Live || ReferenceEquals(c.Source, _vessel))
            {
                continue;
            }
            if (c.Team == AimAssist.NeutralTeam || c.Team == ownTeam)
            {
                continue;
            }
            if (aircraftOnly && c.Source is not FlightController)
            {
                continue;
            }
            string name = c.Source switch
            {
                FlightController fc => fc.IsHumanPiloted ? AiTargetRanking.PlayerRole : fc.Name,
                DestructibleRegistry.Instance pool2 => pool2.Def.Name,
                _ => string.Empty,
            };
            _ranked.Add(new RankedTargetCandidate
            {
                Position = c.Position,
                Velocity = c.Velocity,
                IsPlayer = c.Source is FlightController { IsHumanPiloted: true },
                IsWingman = c.Source is FlightController { Pilot.Escort: not null },
                IsGasbag = c.Source is DestructibleRegistry.Instance { Gasbag: true },
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(name, null, isTurret),
                AlliedAttackers = 0,
            });
            _sources.Add(c);
        }
    }

    private void Fire(Basis basis)
    {
        var fp = _firepoint;
        // The round leaves along the MOUNT's aim, not the raw lead: the barrel is a real node here
        // and a shot down a different line than the one drawn would read as a miss aimed at
        // nothing. The dead-eye cone is the shot's own scatter, drawn once per round.
        var aimWorld = (basis * _aimLocal).Normalized();
        var dir = AimAssist.Scatter(aimWorld, Mathf.DegToRad(DeadEyeAngleDeg), _rng);
        // ⚠ Team explicitly and the hull's own colliders as the round's owner, the world
        // emplacement's case: a hull has no shooter id, so the default would read its rounds as
        // neutral, and its muzzle sits ON the thing it would otherwise strike.
        _pool.Spawn(_weapon, fp.GlobalTransform, _vessel.Velocity, ProjectilePool.NoShooter,
            fp, dir, team: _vessel.Team, ownerBodies: HullColliderRids());
        if (!_firstShotLogged)
        {
            _firstShotLogged = true; // verification breadcrumb: WHICH hulls actually engage
            GD.Print($"surface: '{_vessel.Name}' engaging (first shot, team " +
                     $"{_vessel.Team?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}, {_slot.WeaponId})");
        }
        Ammo--;
        ShotsFired++;
        _fireIn = _slot.RefireSeconds;
    }

    // The hull's own colliders, walked fresh rather than cached: a hull's subtree changes as the
    // injure ladder and the death sequence swap parts, where an emplacement's mounting section
    // does not.
    private Godot.Collections.Array<Rid> HullColliderRids()
    {
        var rids = new Godot.Collections.Array<Rid>();
        CollectRids(_vessel.Body, rids);
        return rids;
    }
}
