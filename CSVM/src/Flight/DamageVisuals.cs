using System;
using System.Collections.Generic;
using CSVM.Effects;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Visible damage on the flying aircraft, driven by the data's
/// thresholds: as a part's HP fraction crosses an entry of its 'injure_anims'
/// (see <see cref="DestroyablePart"/>), the named pdpanelN anim flips the
/// torn-skin panel — pdpN shown, and the healthy skin covering the same spot
/// (a pdpN_h node, a real airframe section like the Bloodhawk wingtip) hidden.
///
/// The healthy twin is found BY POSITION, not by name: on player_bhawk,
/// player_fbrand and player_brigand the _h numbering is crossed in the model
/// files (e.g. the firebrand's pdp3_h is the RIGHT wingtip skin while pdp3 is a
/// LEFT-wing torn panel — its true healthy twin is named pdp2_h), so name
/// pairing amputates the opposite wing. Each _h node instead pairs with the
/// nearest torn panel's mesh footprint (real twins sit ≤ 0.72 m apart, crossed
/// or unrelated skins ≥ 1.36 m — see MaxPairDistance), rejecting mirrored
/// left/right positions (the autogyro's pdp2/pdp2_h sit ±0.28 m across the
/// centerline). Unpaired _h skins (the brigand's tip strips, the balmoral's fin
/// panel) are never hidden. The original's pdpanelN anims only ACTIVE pdpN; its
/// player_destruct_reset anim re-ACTIVEs the _h nodes, so the original engine
/// does hide them at damage time by some rule of its own — if that rule is the
/// name convention, the original shows the same wrong-wing glitch on these
/// three planes (unverified). The *_damage_green/yellow/red entries are the
/// cockpit indicator's texture cycle and stay unwired until a cockpit exists.
///
/// The def-level injure_anims add whole-plane effects: player_smoketrail at
/// 0.10 starts the dense_firetrail pair — a black-smoke trail (COLORS ramp,
/// born orange) plus a fire trail (fire_f01–06 flipbook) emitted per meter of
/// motion at the nose (see the trail support in <see cref="Puffer"/>).
/// player_fuelleak (0.85) is not wired yet. FlightController notifies
/// <see cref="OnPartDamage"/> after each hit, drives <see cref="Update"/> per
/// frame, and calls <see cref="Reset"/> on respawn.
/// </summary>
public sealed class DamageVisuals
{
    private readonly Dictionary<string, Node3D> _panels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Node3D>> _pairedHealthy = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DestroyablePart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(float Frac, string Anim)> _vehicleInjure;
    private readonly Puffer? _smokeTrail, _fireTrail;
    private readonly List<Puffer> _panelTrailPool;
    private readonly List<(Node3D Panel, Puffer Trail)> _panelTrails = new();
    private bool _smoking;

    private const float NoseOffset = 3.5f; // m ahead of center — the data emits the trail
                                           // at prop1 (the nose engine); TUNE per plane

    private const float StaticBurnSpeed = 15f; // m/s of virtual motion the damage lab's parked
                                               // plane spends on its in-place trails; TUNE

    // Healthy-twin pairing (see the class comment): across all 11 player planes, a
    // torn panel and its true healthy skin overlap (mesh-AABB centers ≤ 0.72 m apart),
    // while crossed-name or unrelated skins are ≥ 1.36 m away — 1.0 m splits them.
    private const float MaxPairDistance = 1.0f;
    // The autogyro's pdp2/pdp2_h sit ±0.28 m across the centerline (mirrored, NOT the
    // same spot): opposite X signs beyond this dead band reject a candidate pair.
    private const float MirrorMinX = 0.15f;

    /// <param name="panels">PlaneBuilder.DamagePanels — pdpN (hidden) + pdpN_h nodes.</param>
    /// <param name="planeRoot">the built model's root — pairing measures panel mesh
    /// positions in this frame (the _h nodes bake their placement into mesh space).</param>
    /// <param name="smokeTrail">dense_firetrail's smokepuffer, trail-capable; optional.</param>
    /// <param name="fireTrail">dense_firetrail's firepuffer; optional.</param>
    /// <param name="panelTrails">a pool of firepuffer trail emitters, one assigned per
    /// flipped panel — the original streams a discrete-puff fire trail from every
    /// damaged panel (its pdpanelN anims call short_firetrail WITH_NODE pdpN; clearly
    /// visible in OriginalScreenshots/Videos/C1 IA1 Crash.mp4).</param>
    public DamageVisuals(IEnumerable<Node3D> panels, Node3D planeRoot, PlaneStats stats,
        Puffer? smokeTrail, Puffer? fireTrail, List<Puffer>? panelTrails = null)
    {
        foreach (var p in panels)
            _panels[p.Name] = p;
        PairHealthySkins(planeRoot);
        foreach (var part in stats.DestroyableParts)
            _parts[part.Name] = part;
        _vehicleInjure = stats.VehicleInjureAnims;
        _smokeTrail = smokeTrail;
        _fireTrail = fireTrail;
        _panelTrailPool = panelTrails ?? new List<Puffer>();
    }

    /// <summary>Pairs every healthy pdpN_h skin with the torn panel occupying the same
    /// spot on the airframe (nearest mesh-AABB center within <see cref="MaxPairDistance"/>,
    /// same side of the centerline). Name-based pairing is wrong on three planes — see
    /// the class comment.</summary>
    private void PairHealthySkins(Node3D planeRoot)
    {
        var torn = new List<(string Name, Vector3 Center)>();
        var healthy = new List<(string Name, Node3D Node, Vector3 Center)>();
        foreach (var (name, node) in _panels)
        {
            if (!TryMeshCenter(node, planeRoot, out var c))
                continue;
            if (name.EndsWith("_h", StringComparison.OrdinalIgnoreCase))
                healthy.Add((name, node, c));
            else
                torn.Add((name, c));
        }
        foreach (var (name, node, c) in healthy)
        {
            string? bestName = null;
            Vector3 bestCenter = default;
            float bestDist = float.MaxValue;
            foreach (var (tornName, tornCenter) in torn)
            {
                float d = c.DistanceTo(tornCenter);
                if (d < bestDist)
                    (bestDist, bestName, bestCenter) = (d, tornName, tornCenter);
            }
            bool mirrored = bestName != null && c.X * bestCenter.X < 0f
                && Mathf.Abs(c.X) > MirrorMinX && Mathf.Abs(bestCenter.X) > MirrorMinX;
            if (bestName == null || bestDist > MaxPairDistance || mirrored)
            {
                GD.Print($"damage panels: {name} has no co-located torn panel " +
                         $"(nearest {bestName ?? "none"} {bestDist:0.0} m{(mirrored ? ", mirrored" : "")}) — never hidden");
                continue;
            }
            if (!_pairedHealthy.TryGetValue(bestName, out var list))
                _pairedHealthy[bestName] = list = new List<Node3D>();
            list.Add(node);
            if (!name.Equals(bestName + "_h", StringComparison.OrdinalIgnoreCase))
                GD.Print($"damage panels: {name} is the healthy skin of {bestName} " +
                         $"(names crossed in the model) — paired by position");
        }
    }

    /// <summary>Merged mesh-AABB center of the panel's subtree, in the plane root's
    /// frame. The pdpN nodes carry transforms but the _h twins sit at the origin with
    /// their placement baked into the mesh — mesh AABBs locate both.</summary>
    private static bool TryMeshCenter(Node3D panel, Node3D root, out Vector3 center)
    {
        var merged = default(Aabb);
        bool any = false;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var box = RelativeTo(mi, root) * mi.GetAabb();
                merged = any ? merged.Merge(box) : box;
                any = true;
            }
            foreach (var child in node.GetChildren())
                Walk(child);
        }
        Walk(panel);
        center = merged.GetCenter();
        return any;
    }

    /// <summary>Transform of <paramref name="node"/> relative to <paramref name="root"/>
    /// by walking parents — works before the subtree enters the scene tree.</summary>
    private static Transform3D RelativeTo(Node3D node, Node3D root)
    {
        var xf = Transform3D.Identity;
        for (Node3D? n = node; n != null && n != root; n = n.GetParent() as Node3D)
            xf = n.Transform * xf;
        return xf;
    }

    public int PanelCount => _panels.Count;

    /// <summary>Applies every visual whose threshold the part's new HP fraction has
    /// crossed (fraction ≤ entry). Idempotent per anim name.</summary>
    public void OnPartDamage(string partName, float fraction)
    {
        if (_parts.TryGetValue(partName, out var def))
            foreach (var (frac, anim) in def.InjureAnims)
            {
                if (fraction > frac || !_applied.Add(anim))
                    continue;
                if (anim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase))
                {
                    string n = anim["pdpanel".Length..];
                    if (_panels.TryGetValue("pdp" + n, out var torn))
                    {
                        torn.Visible = true;
                        GD.Print($"damage panel: {anim} on ({partName} {fraction * 100f:0}%)");
                        // the panel burns: a discrete-puff fire trail streams from it
                        if (_panelTrailPool.Count > _panelTrails.Count)
                            _panelTrails.Add((torn, _panelTrailPool[_panelTrails.Count]));
                    }
                    // hide the healthy skin at the torn panel's own spot (paired by
                    // position — the _h names are crossed on three planes)
                    if (_pairedHealthy.TryGetValue("pdp" + n, out var healthySkins))
                        foreach (var healthy in healthySkins)
                            healthy.Visible = false;
                }
                // else: *_damage_green/yellow/red cockpit indicator, *_damage_effects,
                // got-hit sparks — unwired (no cockpit / effect-call playback yet)
            }

        // whole-plane thresholds (any part qualifies — see PlaneStats.VehicleInjureAnims)
        foreach (var (frac, anim) in _vehicleInjure)
        {
            if (fraction > frac || !_applied.Add(anim))
                continue;
            if (anim.Equals("player_smoketrail", StringComparison.OrdinalIgnoreCase))
            {
                _smoking = true;
                GD.Print($"smoke trail: on ({partName} {fraction * 100f:0}%)");
            }
        }
    }

    /// <summary>Feeds the trail emitters their emit points; call each frame while
    /// flying (not crashed/paused).</summary>
    public void Update(Vector3 position, Basis attitude)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.TrailAdvance(panel.GlobalPosition);
        if (!_smoking)
            return;
        var nose = position + attitude * new Vector3(0f, 0f, -NoseOffset);
        _smokeTrail?.TrailAdvance(nose);
        _fireTrail?.TrailAdvance(nose);
    }

    /// <summary>Damage-lab drive (static viewer): the parked plane never moves, so the
    /// distance-interval trails would emit nothing — burn them in place instead, at
    /// <see cref="StaticBurnSpeed"/> of virtual motion per second (panel fires and,
    /// when smoking, the nose smoke/fire pair rise from the standing plane).</summary>
    public void UpdateStatic(float dt, Vector3 position, Basis attitude)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.TrailBurnAt(panel.GlobalPosition, dt, StaticBurnSpeed);
        if (!_smoking)
            return;
        var nose = position + attitude * new Vector3(0f, 0f, -NoseOffset);
        _smokeTrail?.TrailBurnAt(nose, dt, StaticBurnSpeed);
        _fireTrail?.TrailBurnAt(nose, dt, StaticBurnSpeed);
    }

    /// <summary>Back to pristine: torn panels hidden, healthy twins shown, trails
    /// cleared. Called on respawn.</summary>
    public void Reset()
    {
        foreach (var (name, node) in _panels)
            node.Visible = name.EndsWith("_h", StringComparison.OrdinalIgnoreCase);
        _applied.Clear();
        _smoking = false;
        _smokeTrail?.Clear();
        _fireTrail?.Clear();
        foreach (var (_, trail) in _panelTrails)
            trail.Clear();
        _panelTrails.Clear();
    }
}
