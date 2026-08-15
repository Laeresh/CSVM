using System;
using System.Collections.Generic;
using CSVM.Flight;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The world's AA emplacements (M4 C9b): the 26 standalone <c>ai.zrd</c> entries resolved
/// against the built chapter world (one entry instantiates as many turrets as its
/// <c>NODES</c> patterns match — the count is a property of the world model, not of the file),
/// each driven by the same <see cref="TurretController"/> loop as the carried gunners. Built
/// with the flight rigs whenever a chapter world and the shared projectile pool exist, and
/// registered with the pool so every player's aim assist sees the emplacements
/// (<c>ProjectilePool.CollectTurrets</c>).
///
/// <para><b>A Node, and it has to be.</b> It ticks itself from <see cref="_PhysicsProcess"/> on a
/// realtime clock and lets <c>GameSession.DriveSimSteps</c> drive <see cref="SimStep"/> on a
/// fixed or halted one — the <see cref="ZeppelinRuntime"/> contract, and for the same reason:
/// <c>DriveSimSteps</c> runs ONLY when the clock is parent-driven. ⚠ As a plain class stepped
/// from there alone, every emplacement in the install was inert in ordinary play and awake only
/// under <c>--det</c>, which is every suite and every golden — so nothing caught it. Ordering
/// against the zeppelins (a slung mount must read its ride's moved pose) is the tree order here,
/// exactly as it is in <c>DriveSimSteps</c>: this node is added after the zeppelin runtime.</para>
///
/// <para>Shipped <c>ACTIVATED</c> values are honoured by default: 22 of the 26 entries are
/// dormant and stay dormant, because the real wake mechanism is the mission script's
/// <c>WAKEUP_TURRETS</c> and objectives scripting is out of M4's scope. <see cref="WakeAll"/>
/// is the documented stand-in behind <c>--wake-turrets</c> — explicit, logged per emplacement,
/// never a silent default.</para>
///
/// <para>The one non-script activation the binary itself performs is
/// <see cref="SetActivatedUnder"/>: the Instant Action mission builder walks the subtree of each
/// <c>*_zeppelin</c> node and writes <c>ACTIVATED</c> on every turret standing in it: 0 for the
/// zeppelins it switches off, 1 for the <c>zeppelin_run</c> objective. That is what puts guns on
/// the Instant Action zeppelin, whose four <c>ai.zrd</c> entries all ship dormant
/// (docs/formats/turrets.md "Waking a whole subtree").</para>
/// </summary>
public sealed partial class TurretEmplacementRuntime : Node
{
    private readonly TurretController[] _turrets;

    /// <param name="worldRoot">The built world's root, so each emplacement knows which top-level
    /// world object it stands on. Omitted, an emplacement treats only its own site subtree as its
    /// platform, and a gun on a modelled hull is blocked by that hull.</param>
    public TurretEmplacementRuntime(TurretDefs defs, WeaponDefs weapons,
        Func<string, Node3D?, IReadOnlyList<Node3D>> findNodes, ProjectilePool pool,
        Node3D? worldRoot = null)
    {
        _turrets = TurretController.BuildEmplacements(defs, weapons, findNodes, pool, worldRoot);
        pool.RegisterWorldTurrets(_turrets);
        Name = "turret_emplacements";
    }

    public IReadOnlyList<TurretController> Emplacements => _turrets;

    public int Count => _turrets.Length;

    public int AwakeCount
    {
        get
        {
            int n = 0;
            foreach (var t in _turrets)
            {
                if (t.Activated)
                {
                    n++;
                }
            }
            return n;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = Utils.GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>The <c>WAKEUP_TURRETS</c> stand-in: wakes every dormant emplacement, one
    /// breadcrumb per turret so a log shows exactly what the flag armed.</summary>
    public int WakeAll()
    {
        int woken = 0;
        foreach (var t in _turrets)
        {
            if (!t.Activated)
            {
                t.Wake();
                woken++;
                GD.Print($"turret {t.Label}: woken (--wake-turrets stand-in for WAKEUP_TURRETS)");
            }
        }
        return woken;
    }

    /// <summary>Writes <c>ACTIVATED</c> on every emplacement standing on
    /// <paramref name="root"/> or anywhere under it, and reports how many changed state. The
    /// engine's own primitive: a recursive walk of the node's children that looks each node up in
    /// the turret list and stores the flag, reached both by the Instant Action builder's zeppelin
    /// arm and by the objectives script's zeppelin-turret wake.</summary>
    public int SetActivatedUnder(Node3D root, bool activated)
    {
        int changed = 0;
        foreach (var t in _turrets)
        {
            if (t.Site is not { } site || !GodotObject.IsInstanceValid(site))
            {
                continue;
            }
            if (site != root && !root.IsAncestorOf(site))
            {
                continue;
            }
            if (t.Activated == activated)
            {
                continue;
            }
            t.SetActivated(activated);
            changed++;
        }
        return changed;
    }

    public void SimStep(float dt)
    {
        foreach (var t in _turrets)
        {
            t.SimStep(dt);
        }
    }
}
