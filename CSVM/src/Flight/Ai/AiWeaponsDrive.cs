using System;
using System.Collections.Generic;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>What the AI weapons drive reads off its own aircraft for one tick. That is the shooter
/// id its log lines name, the sim pose and velocity, and the fire control it selects a pylon on. It
/// also carries the selected gun group and the ordnance fit. A null fire control holds both weapon
/// classes, as the host passes for a rig with no pool to fire into.</summary>
public readonly record struct AiWeaponsShooter(
    int ShooterId,
    Vector3 Position,
    Vector3 Velocity,
    Basis Attitude,
    FireControl? Fire,
    GunGroup? SelectedGun,
    Loadout? Loadout,
    bool InfiniteAmmo);

/// <summary>One AI aircraft's weapons tick, run on the sim step before the fire step reads the
/// triggers. The gunner keeps or re-acquires its standing target through
/// <see cref="GunnerAcquisition"/> and solves the selected gun group's lead off the sim pose. The
/// rocketeer then walks the pylons against that same target and names the pylon it chose. Only
/// Pursue shoots, for both classes. It owns the pylon walk's list and the per-shooter breadcrumbs,
/// and holds no node: the host supplies one <see cref="AiWeaponsShooter"/> per tick.</summary>
public sealed class AiWeaponsDrive
{
    private const int VerdictLogCap = 12;          // verdict changes logged per shooter

    private readonly GunnerAcquisition _acquisition;
    private readonly List<RocketPylonView> _pylonViews = new();          // the AI rocketeer's pylon walk
    private bool _gunnerLoggedFire;              // verification breadcrumb: the AI gunner's first open fire
    private bool _rocketeerLoggedFire;           // verification breadcrumb: the AI's first ordnance launch
    private string _rocketeerLastVerdict = "";   // the last rocketeer verdict KEY logged, so a repeat is silent
    private int _rocketeerVerdictsLogged;        // capped per shooter: a flight of twelve must not flood the log

    /// <summary>Builds the drive over the aircraft's own target acquisition.</summary>
    public AiWeaponsDrive(GunnerAcquisition acquisition)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        _acquisition = acquisition;
    }

    /// <summary>One tick of <paramref name="pilot"/>'s gunner, then its rocketeer if it has one.
    /// <see cref="AiGunner.WantsFire"/> and <see cref="AiRocketeer.WantsFire"/> are current when it
    /// returns.</summary>
    public void Step(AiPilot pilot, AiGunner gunner, in AiWeaponsShooter shooter, float dt)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentNullException.ThrowIfNull(gunner);
        DriveGunner(pilot, gunner, shooter);
        // The ordnance half runs off the gunner's target, never its own acquisition. The two weapon
        // classes then cannot chase different aircraft, as the original's never do.
        if (pilot.Rocketeer is { } rocketeer)
            DriveRocketeer(pilot, rocketeer, gunner, shooter, dt);
    }

    // The gunner's one standing target of any class, which AiPilot reads as its pursuit quarry.
    private static object? StandingTarget(AiGunner gunner) => gunner.Target;

    // One AI-gunner tick: the acquisition keeps or re-acquires the standing target. The gunner then
    // gets the SELECTED gun group's weapon and muzzle midpoint, the sim pose (never the render
    // pose) and the target's state. AiGunner.WantsFire is then current when the fire step reads it.
    private void DriveGunner(AiPilot pilot, AiGunner gunner, in AiWeaponsShooter shooter)
    {
        gunner.HoldFire();
        if (shooter.Fire == null)
            return;
        if (!_acquisition.Step(gunner, out var targetPos, out var targetVel, out var targetFwd))
            return;
        // ⚠ Only Pursue shoots. Lay off holds fire deliberately (the rubber-band assist) even
        // though the target stays acquired in every mode.
        if (pilot.Machine is { } modes && modes.Mode != AiMode.Pursue)
            return;
        if (shooter.SelectedGun is not { } group)
            return;
        // The same convergence point the reticle and the original's own barrel averaging use.
        var muzzlePos = group.MuzzleMidpoint();
        // The engagement window is a property of the weapon slot, not of the pilot. A group whose AI
        // def authored one flies that one; a stock fit authors none and keeps the gunner's own.
        if (group.MinRangeM > 0f && group.MaxRangeM > 0f)
        {
            gunner.MinRangeM = group.MinRangeM;
            gunner.MaxRangeM = group.MaxRangeM;
        }
        gunner.Solve(muzzlePos, shooter.Velocity, shooter.Attitude,
            targetPos, targetVel, targetFwd,
            group.Weapon.Velocity ?? ProjectilePool.DefaultVelocity);
        if (gunner.WantsFire && !_gunnerLoggedFire)
        {
            _gunnerLoggedFire = true; // verification breadcrumb: the gates first opened
            Log.Info("flight",
                $"ai gunner: shooter {shooter.ShooterId} opens fire on {TargetPool.TargetLabel(StandingTarget(gunner))} at {shooter.Position.DistanceTo(targetPos):0} m ({group.Weapon.Id})");
        }
    }

    // One AI-rocketeer tick. The lockout ages unconditionally: it is a vehicle timer, not one that
    // stops when the AI loses its target. The pylons are then walked against the GUNNER's target and
    // the decision names the pylon it chose. A DAMAGES_ZEPPELIN round then cannot be launched by a
    // cursor that disagrees with the gates that cleared it.
    private void DriveRocketeer(AiPilot pilot, AiRocketeer rocketeer, AiGunner gunner,
        in AiWeaponsShooter shooter, float dt)
    {
        rocketeer.Tick(dt);
        if (shooter.Fire is not { } fire || shooter.Loadout is not { Hardpoints.Count: > 0 } fit)
            return;
        if (!TargetPool.TryTargetGeometry(StandingTarget(gunner), out var targetPos, out var targetVel,
                out var targetFwd, out bool targetLive) || !targetLive)
            return;
        // ⚠ Only Pursue shoots, the same deliberate hold the guns take. The original restricts
        // neither, so revisiting this is one change for both classes, not two.
        if (pilot.Machine is { } modes && modes.Mode != AiMode.Pursue)
            return;
        _pylonViews.Clear();
        for (int i = 0; i < fit.Hardpoints.Count; i++)
        {
            var hp = fit.Hardpoints[i];
            _pylonViews.Add(new RocketPylonView
            {
                Index = i,   // the list position FireControl selects by, not the 1-based pylon number
                DamagesZeppelin = hp.Weapon.DamagesZeppelin,
                Armed = hp.Armed(shooter.InfiniteAmmo),
                MountPos = hp.Pylon.GlobalPosition,
                RoundSpeed = hp.Weapon.Velocity ?? ProjectilePool.DefaultVelocity,
                RoundAccel = hp.Weapon.Acceleration ?? 0f,
                MinRangeM = hp.MinRangeM,
                MaxRangeM = hp.MaxRangeM,
                RefireSeconds = hp.RefireSeconds,
            });
        }
        rocketeer.Solve(shooter.Position, shooter.Velocity, shooter.Attitude,
            targetPos, targetVel, targetFwd,
            targetIsGasbag: gunner.Target is DestructibleRegistry.Instance { Gasbag: true },
            _pylonViews);
        if (rocketeer.SelectedPylon >= 0)
            fire.SelectPylon(rocketeer.SelectedPylon);
        // Each CHANGE of verdict, a few per shooter. When a gasbag's gate reads open and nothing
        // launches, the log has to say which gate held it.
        if (_rocketeerVerdictsLogged < VerdictLogCap && rocketeer.LastVerdictKey.Length > 0
            && rocketeer.LastVerdictKey != _rocketeerLastVerdict)
        {
            _rocketeerLastVerdict = rocketeer.LastVerdictKey;
            _rocketeerVerdictsLogged++;
            Log.Info("flight",
                $"ai rocketeer: shooter {shooter.ShooterId} on {TargetPool.TargetLabel(StandingTarget(gunner))} at {shooter.Position.DistanceTo(targetPos):0} m: {rocketeer.LastVerdict}");
        }
        if (rocketeer.WantsFire && !_rocketeerLoggedFire)
        {
            _rocketeerLoggedFire = true; // verification breadcrumb: the ordnance gates first opened
            // pylon{Index}, never the list position the selection runs on. The fire step's launch line
            // names the hardpoint's OWN number. Two numbers for one pylon in adjacent lines read as the
            // wrong pylon firing.
            var hp = fit.Hardpoints[rocketeer.SelectedPylon];
            Log.Info("flight",
                $"ai rocketeer: shooter {shooter.ShooterId} launches at {TargetPool.TargetLabel(StandingTarget(gunner))} at {shooter.Position.DistanceTo(targetPos):0} m ({hp.Weapon.Id}, pylon{hp.Index})");
        }
    }
}
