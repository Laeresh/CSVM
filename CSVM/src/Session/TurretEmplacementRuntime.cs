using System;
using System.Collections.Generic;
using CSVM.Flight;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The world's AA emplacements: the standalone <c>ai.zrd</c> entries resolved against the built
/// chapter world, each driven by the same <see cref="TurretController"/> loop as the carried
/// gunners. Registered with the shared pool so every player's aim assist sees them. Detail on
/// the wake mechanisms (<see cref="WakeAll"/>, <see cref="SetActivatedUnder"/>) and the tree
/// order this depends on: this module's docs/architecture.md entry.
/// ⚠ Must step from both <see cref="_PhysicsProcess"/> and <c>GameSession.DriveSimSteps</c>, and
/// must be added to the tree after the zeppelin runtime. A plain class stepped from
/// <c>DriveSimSteps</c> alone left every emplacement inert in ordinary play.
/// ⚠ Shipped `ACTIVATED` is the default; <see cref="WakeAll"/> and <see cref="SetActivatedUnder"/>
/// are the only two wake paths, both logged. Never wake a turret silently.
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
