using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// <c>--incoming[=metres[,wep_id]]</c> — the near-miss test rig: a phantom shooter sitting on each
/// player's six, walking a burst past the canopy at a chosen pass distance. Reaches the
/// incoming-fire cue deterministically, without an AI gunner (docs/cli.md's <c>--ai-attack</c>
/// aims to hit) or a second pilot in splitscreen.
/// Fires the target's own gun (or the named weapon) into the shared pool under a shooter identity
/// no player holds, so the rounds are real. The muzzle sits <see cref="Standoff"/> behind the
/// aircraft, aimed along the target's own nose, so the round overtakes on a parallel track.
/// ⚠ Never aim this rig at the plane: an aircraft IS a projectile target (<see
/// cref="AircraftBody"/>). Hit feedback comes from a real shooter — another pilot, or an AI
/// gunner (<c>BL-226</c>).
/// </summary>
public sealed partial class IncomingFire : Node
{
    /// <summary>The shooter identity these rounds carry — outside every player index, so no
    /// aircraft ever excludes them as its own.</summary>
    public const int ShooterId = 10_000;

    /// <summary>Default pass distance, metres — inside <see cref="WarningShotCue.PassRadius"/> so a
    /// bare <c>--incoming</c> sounds the cue rather than testing the threshold's far side.</summary>
    public const float DefaultPass = 8f;

    // How far behind the target the phantom muzzle sits. Originally kept short because CANNON_SPREAD
    // was (wrongly) read as a dispersion cone that grows with range and, past ~200 m, could throw a
    // round outside the trigger radius (A1 removed that scatter — a round now leaves dead straight,
    // so the achieved pass distance no longer depends on Standoff at all). No data-driven reason
    // remains for this exact figure; left at 120 m since nothing needs it changed.
    private const float Standoff = 120f;   // m
    private const float FireInterval = 2f; // s between rounds — one pass, heard, then the next

    private readonly ProjectilePool _pool;
    private readonly WeaponDefs _weapons;
    private readonly List<FlightController> _targets = new();
    private readonly float _pass;
    private readonly string? _weaponId;

    private float _accum;
    private int _shot;

    public IncomingFire(ProjectilePool pool, WeaponDefs weapons, float pass, string? weaponId)
    {
        _pool = pool;
        _weapons = weapons;
        _pass = pass;
        _weaponId = weaponId;
        Name = "incoming_fire";
    }

    public void AddTarget(FlightController controller) => _targets.Add(controller);

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One step of the burst clock: fires at each target in turn. Public for the same
    /// reason the pool's is — a fixed or halted clock has the session drive it.</summary>
    public void SimStep(float dt)
    {
        if (_targets.Count == 0)
            return;
        _accum += dt;
        while (_accum >= FireInterval)
        {
            _accum -= FireInterval;
            FireAt(_targets[_shot % _targets.Count], _shot % 2 == 0 ? 1f : -1f);
            _shot++;
        }
    }

    private void FireAt(FlightController target, float side)
    {
        var weapon = ResolveWeapon(target);
        if (weapon == null)
            return;
        var xf = target.GlobalTransform;
        var forward = -xf.Basis.Z.Normalized();
        var right = xf.Basis.X.Normalized();
        var origin = xf.Origin - forward * Standoff + right * (_pass * side);
        var muzzle = new Transform3D(Basis.LookingAt(forward, Vector3.Up), origin);
        _pool.Spawn(weapon, muzzle, Vector3.Zero, ShooterId);
        Log.Info("weapons", $"incoming fire at P{target.PlayerIndex + 1}: {weapon.Id} {(side > 0f ? "right" : "left")} {_pass:0.0} m, {Standoff:0} m astern");
    }

    private WeaponDef? ResolveWeapon(FlightController target)
    {
        if (_weaponId != null)
            return _weapons.Get(_weaponId);
        if (target.Loadout != null)
            foreach (var g in target.Loadout.FirableGuns)
                return g.Weapon;
        return null;
    }
}
