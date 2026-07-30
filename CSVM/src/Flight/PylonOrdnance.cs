using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The ordnance hanging under a plane's wings (D44): one <c>FLYOUT</c> <c>MODEL</c> body instanced at
/// each loaded pylon and hidden the moment that pylon runs dry. The mounted body is the SAME gamez
/// prototype the round flies (<c>he_rocket</c>, <c>ap_rocket</c>, <c>sonic</c>, …), instanced through
/// <see cref="ProjectilePool.BuildFlyoutBody"/> and parented to the pylon marker with an identity
/// local transform — so it rides the airframe nose-forward, exactly where and how the round launches.
///
/// <para><b>One model per pylon</b>, regardless of the <c>CLUSTER_SIZE</c> rounds that pylon carries:
/// the original shows a single rocket per hardpoint, not a stack. <see cref="Update"/> tracks the
/// live <see cref="Hardpoint.Ammo"/> counter that <c>FlightController.UpdateRockets</c> depletes —
/// the model shows while the pylon holds ordnance and hides at zero, a respawn refill bringing it
/// back. Built once at session setup and rides the plane; freed with it.</para>
/// </summary>
public sealed class PylonOrdnance
{
    private readonly List<Mount> _mounts;
    private int _hidesLogged;

    private PylonOrdnance(List<Mount> mounts) => _mounts = mounts;

    /// <summary>The number of pylons currently showing a mounted model — for the setup breadcrumb.</summary>
    public int Count => _mounts.Count;

    /// <summary>Instances one ordnance body per loaded pylon and parents it to that pylon marker,
    /// nose-forward at the mount. Returns null when nothing could be mounted — no projectile pool, a
    /// view without the world scene, or a chapter whose gamez lacks the prototype roots (the round
    /// then flies its streak-only fallback and the wing simply shows no ordnance).</summary>
    public static PylonOrdnance? Build(Loadout loadout, ProjectilePool? pool)
    {
        if (pool == null)
        {
            return null;
        }
        var mounts = new List<Mount>();
        foreach (var hp in loadout.Hardpoints)
        {
            var model = pool.BuildFlyoutBody(hp.Weapon);
            if (model == null)
            {
                continue;   // chapter lacks the prototype (or the ordnance carries no FLYOUT MODEL)
            }
            model.Name = $"ordnance{hp.Index}";
            hp.Pylon.AddChild(model);
            // Identity in the pylon's frame: the body's nose (local -Z) rides the pylon's -Z (the
            // forward firing direction), tail at the mount — the same pose the round launches in, so
            // the mounted body and the fired round are seamless.
            model.Transform = Transform3D.Identity;
            bool shown = hp.Ammo > 0;
            model.Visible = shown;
            mounts.Add(new Mount { Hardpoint = hp, Model = model, Shown = shown });
        }
        return mounts.Count > 0 ? new PylonOrdnance(mounts) : null;
    }

    /// <summary>Syncs each mounted body's visibility to its pylon's live ammo — shown while the pylon
    /// holds ordnance, hidden at zero. Cheap: writes <see cref="Node3D.Visible"/> only on a change.
    /// Driven each frame after the rocket-firing update; a respawn refill shows on the next frame.</summary>
    public void Update()
    {
        foreach (var m in _mounts)
        {
            bool want = m.Hardpoint.Ammo > 0;
            if (want != m.Shown)
            {
                m.Model.Visible = want;
                m.Shown = want;
                // Verification breadcrumb (the first few hides): the mounted body vanishing as its
                // pylon empties is the depletion behaviour, unobservable headless otherwise; capped.
                if (!want && _hidesLogged < 12)
                {
                    _hidesLogged++;
                    GD.Print($"pylon ordnance: pylon{m.Hardpoint.Index} dry — mounted model hidden");
                }
            }
        }
    }

    private sealed class Mount
    {
        public Hardpoint Hardpoint = null!;
        public Node3D Model = null!;
        public bool Shown;
    }
}
