using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// <c>--incoming[=metres[,wep_id]]</c> — the near-miss test rig: a phantom shooter sitting on each
/// player's six, walking a burst past the canopy at a chosen pass distance. It exists because
/// nothing in the world shoots back yet (M4 AI), so the incoming-fire cue (BL-087) would otherwise
/// be reachable only with a second pilot in splitscreen.
///
/// <para>It fires the target's OWN gun (or the named weapon) into the shared pool under a shooter
/// identity no player holds, so the rounds are real — same ballistics, tracers, muzzle flash and
/// impacts as a fired round — and the target's self-exclusion is not what makes them audible.
/// The muzzle is placed <see cref="Standoff"/> behind the aircraft, offset laterally by the pass
/// distance and aimed along the target's own nose, so the round overtakes it on a parallel track
/// and the geometry holds without lead maths. Sides alternate. <c>CANNON_SPREAD</c> still jitters
/// each round, so the achieved distance scatters a little around the requested one — which is what
/// makes a threshold sweep read honestly rather than as an on/off switch.</para>
///
/// <para><b>Near misses only.</b> A hit cannot be simulated this way: an aircraft exists to the
/// projectile raycast as nothing at all (its collision is the swept <see cref="PlaneCollider"/>
/// query boxes, not a body), so no round can strike one — the reason <c>bullet_hit_sg</c> stays
/// unbuildable in M3.</para>
/// </summary>
public sealed partial class IncomingFire : Node
{
    /// <summary>The shooter identity these rounds carry — outside every player index, so no
    /// aircraft ever excludes them as its own.</summary>
    public const int ShooterId = 10_000;

    /// <summary>Default pass distance, metres — inside <see cref="WarningShotCue.PassRadius"/> so a
    /// bare <c>--incoming</c> sounds the cue rather than testing the threshold's far side.</summary>
    public const float DefaultPass = 8f;

    // How far behind the target the phantom muzzle sits. Kept short on purpose: the weapon's own
    // CANNON_SPREAD cone (6° for the stock guns) grows with range, and past ~200 m it can throw a
    // round clean outside the trigger radius, so a test rig at a longer standoff would sometimes
    // be silent for reasons that have nothing to do with the cue.
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
