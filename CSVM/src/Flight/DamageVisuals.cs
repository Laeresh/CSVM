using System;
using System.Collections.Generic;
using CSVM.Effects;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Visible damage on the aircraft, driven by the data's thresholds: as a part's
/// combined armor+HP fraction crosses an entry of its 'injure_anims' (see
/// <see cref="DestroyablePart"/>), the entry's authored animation plays. The
/// pdpanelN entries flip the torn-skin panel (pdpN shown, the healthy skin covering
/// the same spot hidden) and, in flight, play the authored pdpanelN def through the
/// player's own rig runtime (<see cref="DamageEffectSink"/>) — gimmeflakes debris
/// plus the staged short_firetrail / loop_short_firetrail burn-down at the panel,
/// exactly as player-1.zrd.json choreographs it. The def-level entries play the same
/// way: player_fuelleak (0.85 — the fuel-vapor stream from a random pdp1–3) and, for
/// the 0.10 player_smoketrail entry, player_damage_trail — short_firetrail at prop1
/// plus the flickering fire_lt nose light (see <see cref="RigAnimFor"/> for why that
/// one name is mapped, `BL-259`).
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
/// Two hosts. In flight the sinks reach the rig runtime and the effects are the
/// authored defs; a world-less flight (--stage=empty) has no rig runtime, so only
/// the panel flips render there, logged once. The parked viewer's damage lab has no
/// runtime either — and a parked plane travels no distance, so the authored
/// distance-interval trails would emit nothing anyway; it keeps stand-in Puffer
/// trails burning in place (<see cref="UpdateStatic"/>) at the panels and at the
/// authored prop1 anchor. FlightController notifies <see cref="OnPartDamage"/>
/// after each hit and calls <see cref="Reset"/> on respawn.
/// </summary>
public sealed class DamageVisuals
{
    /// <summary>Plays a named anim def on this player's own plane-scoped runtime (the crash rig):
    /// the authored damage-stage menu (<c>pdpanelN</c>/<c>player_fuelleak</c>/
    /// <c>player_damage_trail</c>) and the `&lt;part&gt;_damage_effects` spark shims. Set after the
    /// rig's runtime exists, which is later than this object is built; null (world-less flight, the
    /// parked viewer) leaves the authored stages unplayed.</summary>
    public Action<string>? DamageEffectSink;

    /// <summary>Stops everything <see cref="DamageEffectSink"/>'s plays started — the whole
    /// damage-stage closure, not just the played roots, because a stopped pdpanelN cannot reach the
    /// short_firetrail instance it CALLed onto the panel, and player_damage_trail's trail sits on
    /// prop1, whose NODE_ACTIVE exit never fires (the prop stays visible). Invoked from
    /// <see cref="Reset"/>; null when there is no runtime to stop.</summary>
    public Action? DamageEffectStop;

    private const float StaticBurnSpeed = 15f; // m/s of virtual motion the damage lab's parked
                                               // plane spends on its in-place stand-in trails; TUNE

    // Healthy-twin pairing (see the class comment): across all 11 player planes, a
    // torn panel and its true healthy skin overlap (mesh-AABB centers ≤ 0.72 m apart),
    // while crossed-name or unrelated skins are ≥ 1.36 m away — 1.0 m splits them.
    private const float MaxPairDistance = 1.0f;
    // The autogyro's pdp2/pdp2_h sit ±0.28 m across the centerline (mirrored, NOT the
    // same spot): opposite X signs beyond this dead band reject a candidate pair.
    private const float MirrorMinX = 0.15f;

    private readonly Dictionary<string, Node3D> _panels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Node3D>> _pairedHealthy = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DestroyablePart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(float Frac, string Anim)> _vehicleInjure;
    private readonly Puffer? _standInTrailPuffer, _standInFirePuffer;
    private readonly List<Puffer> _panelTrailPool;
    private readonly List<(Node3D Panel, Puffer Trail)> _panelTrails = new();
    private readonly Node3D? _prop1; // the authored heavy-trail anchor, for the parked stand-in
    private bool _smoking;
    private bool _noRuntimeLogged;

    /// <param name="panels">PlaneBuilder.DamagePanels — pdpN (hidden) + pdpN_h nodes.</param>
    /// <param name="planeRoot">the built model's root — pairing measures panel mesh
    /// positions in this frame (the _h nodes bake their placement into mesh space).</param>
    /// <param name="standInTrail">parked-viewer stand-in for the heavy trail's smoke half,
    /// burned in place at prop1; flight passes none and plays the authored defs instead.</param>
    /// <param name="standInFire">its fire half.</param>
    /// <param name="panelTrails">parked-viewer stand-in pool, one firepuffer per flipped
    /// panel — standing in for the authored short_firetrail the flight path plays.</param>
    public DamageVisuals(IEnumerable<Node3D> panels, Node3D planeRoot, PlaneStats stats,
        Puffer? standInTrail = null, Puffer? standInFire = null, List<Puffer>? panelTrails = null)
    {
        foreach (var p in panels)
            _panels[p.Name] = p;
        PairHealthySkins(planeRoot);
        foreach (var part in stats.DestroyableParts)
            _parts[part.Name] = part;
        _vehicleInjure = stats.VehicleInjureAnims;
        _standInTrailPuffer = standInTrail;
        _standInFirePuffer = standInFire;
        _panelTrailPool = panelTrails ?? new List<Puffer>();
        _prop1 = FindByName(planeRoot, "prop1");
    }

    public int PanelCount => _panels.Count;

    /// <summary>The rig anim an injure_anims entry plays, or null for the entries that are not
    /// rig-runtime work (the cockpit gauge cycles, got_hit_anim). One deliberate mapping: the
    /// data's 0.10 entry names <c>player_smoketrail</c>, whose def calls the dense_firetrail pair,
    /// but `BL-259`/CAP-15 pin the heavy stage the player sees as <c>player_damage_trail</c> —
    /// short_firetrail at prop1 plus the fire_lt nose light — so that is what plays.</summary>
    public static string? RigAnimFor(string injureAnim)
    {
        if (injureAnim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase)
            || injureAnim.EndsWith("_damage_effects", StringComparison.OrdinalIgnoreCase)
            || injureAnim.Equals("player_fuelleak", StringComparison.OrdinalIgnoreCase))
        {
            return injureAnim;
        }
        return injureAnim.Equals("player_smoketrail", StringComparison.OrdinalIgnoreCase)
            ? "player_damage_trail"
            : null;
    }

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
                        // the parked stand-in: a firepuffer burning in place at the panel
                        // (the flight path plays the authored def through the sink below)
                        if (DamageEffectSink == null && _panelTrailPool.Count > _panelTrails.Count)
                            _panelTrails.Add((torn, _panelTrailPool[_panelTrails.Count]));
                    }
                    // hide the healthy skin at the torn panel's own spot (paired by
                    // position — the _h names are crossed on three planes)
                    if (_pairedHealthy.TryGetValue("pdp" + n, out var healthySkins))
                        foreach (var healthy in healthySkins)
                            healthy.Visible = false;
                    PlayStage(anim, partName, fraction);
                }
                else if (anim.EndsWith("_damage_effects", StringComparison.OrdinalIgnoreCase))
                {
                    // The per-impact spark burst. The authored chain discards the part: all four
                    // <part>_damage_effects defs are the same one-event shim calling
                    // random_gun_impact, which picks pdp1 (40%) or pdp2 (40%) and ALWAYS also
                    // sparks pdp4 — so a hit can light one panel or two, never none.
                    PlayStage(anim, partName, fraction);
                }
                // else: *_damage_green/yellow/red cockpit indicator and got_hit_anim's nosedamage
                // blink — unwired (no cockpit; GaugeCluster.OnPartDamage approximates the latter)
            }

        // whole-plane thresholds (any part qualifies — see PlaneStats.VehicleInjureAnims)
        foreach (var (frac, anim) in _vehicleInjure)
        {
            if (fraction > frac || !_applied.Add(anim))
                continue;
            if (RigAnimFor(anim) is not { } stage)
                continue;
            if (DamageEffectSink == null
                && anim.Equals("player_smoketrail", StringComparison.OrdinalIgnoreCase))
            {
                _smoking = true; // the parked stand-in pair burns at prop1 (UpdateStatic)
                GD.Print($"smoke trail: on ({partName} {fraction * 100f:0}%)");
            }
            PlayStage(stage, partName, fraction);
        }
    }

    /// <summary>Damage-lab drive (static viewer): the parked plane never moves, so the authored
    /// distance-interval trails would emit nothing — the stand-in puffers burn in place instead,
    /// at <see cref="StaticBurnSpeed"/> of virtual motion per second (panel fires and, when
    /// smoking, the prop1 smoke/fire pair rise from the standing plane).</summary>
    public void UpdateStatic(float dt)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.TrailBurnAt(panel.GlobalPosition, dt, StaticBurnSpeed);
        if (!_smoking)
            return;
        if (_prop1 is not { } nose)
            return;
        _standInTrailPuffer?.TrailBurnAt(nose.GlobalPosition, dt, StaticBurnSpeed);
        _standInFirePuffer?.TrailBurnAt(nose.GlobalPosition, dt, StaticBurnSpeed);
    }

    /// <summary>Back to pristine: the runtime's damage stages stopped, torn panels hidden,
    /// healthy twins shown, stand-in trails cleared. Called on respawn and by the damage lab's
    /// repair path. Stop runs FIRST: pdpanel1's authored LOOP −1 re-asserts pdpN ACTIVE every
    /// tick, so hiding the panel under a live instance would be undone next frame.</summary>
    public void Reset()
    {
        DamageEffectStop?.Invoke();
        foreach (var (name, node) in _panels)
            node.Visible = name.EndsWith("_h", StringComparison.OrdinalIgnoreCase);
        _applied.Clear();
        _smoking = false;
        foreach (var (_, trail) in _panelTrails)
            trail.Clear();
        _panelTrails.Clear();
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

    /// <summary>First node under <paramref name="root"/> whose Godot name or gamez
    /// <c>cs_name</c> meta matches — the same two names AnimRuntime resolution reads.</summary>
    private static Node3D? FindByName(Node root, string name)
    {
        if (root is Node3D n3
            && (root.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)
                || (root.HasMeta(Mech3.AnimRuntime.NameMeta)
                    && root.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()
                        .Equals(name, StringComparison.OrdinalIgnoreCase))))
        {
            return n3;
        }
        foreach (var child in root.GetChildren())
            if (FindByName(child, name) is { } hit)
                return hit;
        return null;
    }

    /// <summary>Routes one authored stage anim out through the rig runtime, or says (once) why it
    /// cannot: a world-less flight has no rig runtime and renders panel flips alone.</summary>
    private void PlayStage(string anim, string partName, float fraction)
    {
        if (DamageEffectSink != null)
        {
            DamageEffectSink(anim);
            GD.Print($"damage stage: {anim} on ({partName} {fraction * 100f:0}%)");
        }
        else if (_panelTrailPool.Count == 0 && !_noRuntimeLogged)
        {
            _noRuntimeLogged = true;
            GD.Print($"damage stage: no rig runtime — authored anim '{anim}' (and any later stage) " +
                     "not played; panel flips only");
        }
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
}
