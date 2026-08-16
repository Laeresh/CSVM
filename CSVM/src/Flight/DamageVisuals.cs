using System;
using System.Collections.Generic;
using CSVM.Effects;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Visible damage on the aircraft, driven purely by the data's injure_anims thresholds: a
/// part's own health fraction (<see cref="OnPartDamage"/>) and the whole vehicle's health
/// fraction (<see cref="OnHullDamage"/>) each drive their own list — see
/// <c>docs/org/vehicleDamage.md</c>'s "Damage staging" for the two pools and why armour is in
/// neither. See <c>docs/architecture.md</c>'s "src/Flight/DamageVisuals.cs" entry for what each
/// authored stage plays and the panel-pairing/gimmeflakes traps.
/// FlightController calls <see cref="OnPartDamage"/>/<see cref="OnHullDamage"/> after each hit
/// and <see cref="Reset"/> on respawn.
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

    /// <param name="panels">PlaneBuilder.DamagePanels — pdpN + pdpN_h nodes.</param>
    /// <param name="planeRoot">pairing measures panel mesh centers in this frame.</param>
    /// <param name="standInTrail">parked-viewer stand-in smoke half, burned at prop1; null in flight.</param>
    /// <param name="standInFire">its fire half.</param>
    /// <param name="panelTrails">parked-viewer stand-in pool, one firepuffer per flipped panel.</param>
    /// <param name="pairing">def-derived candidate sets; null falls back to unscoped pairing, loudly.</param>
    public DamageVisuals(IEnumerable<Node3D> panels, Node3D planeRoot, PlaneStats stats,
        Puffer? standInTrail = null, Puffer? standInFire = null, List<Puffer>? panelTrails = null,
        PanelPairing? defPairing = null)
    {
        foreach (var p in panels)
            _panels[p.Name] = p;
        PairHealthySkins(planeRoot, defPairing);
        foreach (var part in stats.DestroyableParts)
            _parts[part.Name] = part;
        _vehicleInjure = stats.VehicleInjureAnims;
        _standInTrailPuffer = standInTrail;
        _standInFirePuffer = standInFire;
        _panelTrailPool = panelTrails ?? new List<Puffer>();
        _prop1 = FindByName(planeRoot, "prop1");
    }

    public int PanelCount => _panels.Count;

    /// <summary>The panel candidate sets the authored data names: the healthy skins
    /// damage may hide are exactly the `*_h` nodes `plane_reset` re-ACTIVEs on reset (pdp2_h and
    /// pdp3_h, one shared def OPERAND_NODE-retargeted at every airframe), and the torn panels are
    /// the `pdpN` nodes the `pdpanelN` defs activate. What the defs do NOT encode is which torn
    /// panel hides which skin — no def ever deactivates an `_h` node — so the co-location match
    /// (<see cref="PairHealthySkins"/>) still assigns pairs, scoped to these sets.</summary>
    public static PanelPairing? PanelPairingSets(IEnumerable<Mech3.AnimDefinition> defs)
    {
        var hideable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var torn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in defs)
        {
            string animName = def.AnimName ?? def.Name;
            bool reset = animName.Equals("plane_reset", StringComparison.OrdinalIgnoreCase);
            bool panel = animName.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase);
            if (!reset && !panel)
                continue;
            foreach (var seq in def.Sequences)
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind != "ObjectActiveState" || !ev.Data.Bool("state"))
                        continue;
                    if ((ev.Data.Str("node") ?? ev.Data.Str("name")) is not { } name)
                        continue;
                    if (reset && name.EndsWith("_h", StringComparison.OrdinalIgnoreCase))
                        hideable.Add(name);
                    else if (panel && name.StartsWith("pdp", StringComparison.OrdinalIgnoreCase)
                             && !name.EndsWith("_h", StringComparison.OrdinalIgnoreCase))
                        torn.Add(name);
                }
        }
        return hideable.Count == 0 && torn.Count == 0 ? null : new PanelPairing(hideable, torn);
    }

    /// <summary>The rig anim an injure_anims entry plays, or null for the entries that are not
    /// rig-runtime work (the cockpit gauge cycles, got_hit_anim). One deliberate mapping: the
    /// data's 0.10 entry names <c>player_smoketrail</c>, whose def calls the dense_firetrail pair,
    /// but the original's damage footage pins the heavy stage the player sees as <c>player_damage_trail</c> —
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

    /// <summary>Applies every per-part visual whose threshold the part's health fraction has
    /// crossed (health-only, not combined armour+HP — see <c>docs/org/vehicleDamage.md</c>'s
    /// "Damage staging"). Idempotent per anim name. The whole-vehicle stages are
    /// <see cref="OnHullDamage"/>, off a different pool.</summary>
    public void OnPartDamage(string partName, float healthFraction)
    {
        if (_parts.TryGetValue(partName, out var def))
            foreach (var (frac, anim) in def.InjureAnims)
            {
                if (healthFraction > frac || !_applied.Add(anim))
                    continue;
                if (anim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase))
                {
                    string n = anim["pdpanel".Length..];
                    if (_panels.TryGetValue("pdp" + n, out var torn))
                    {
                        torn.Visible = true;
                        GD.Print($"damage panel: {anim} on ({partName} {healthFraction * 100f:0}%)");
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
                    PlayStage(anim, partName, healthFraction);
                }
                else if (anim.EndsWith("_damage_effects", StringComparison.OrdinalIgnoreCase))
                {
                    // The per-impact spark burst; see docs/formats/vehicle.md for
                    // random_gun_impact's panel pick. Health-gated like every other entry.
                    PlayStage(anim, partName, healthFraction);
                }
                // else: *_damage_green/yellow/red cockpit indicator and got_hit_anim's nosedamage
                // blink — unwired (no cockpit; GaugeCluster.OnPartDamage approximates the latter)
            }
    }

    /// <summary>Applies every def-level visual whose threshold the WHOLE VEHICLE's health fraction
    /// has crossed. FUN_004b3800 divides [inst+0x2d0] / [inst+0x2cc], hull health current over max,
    /// so player_smoketrail's 0.10 entry means "the hull is at 10%", not "some zone is at 10%";
    /// PlaneDamage.SummaryHealthFraction is that quotient. Takes no part name because the original's
    /// def-level driver has none. Runs on every spend, including a zone-less hit that only drains
    /// the hull pair.</summary>
    public void OnHullDamage(float healthFraction)
    {
        foreach (var (frac, anim) in _vehicleInjure)
        {
            if (healthFraction > frac || !_applied.Add(anim))
                continue;
            if (RigAnimFor(anim) is not { } stage)
                continue;
            if (DamageEffectSink == null
                && anim.Equals("player_smoketrail", StringComparison.OrdinalIgnoreCase))
            {
                _smoking = true; // the parked stand-in pair burns at prop1 (UpdateStatic)
                GD.Print($"smoke trail: on (hull {healthFraction * 100f:0}%)");
            }
            PlayStage(stage, "hull", healthFraction);
        }
    }

    /// <summary>The hull health fraction the parked damage lab has no ledger to read: FUN_004b3bf0's
    /// sum over parts, health current over health max, weighted by each part's authored MaxHp.
    /// Parts the caller omits count as untouched.</summary>
    public float HullHealthFractionFrom(IReadOnlyDictionary<string, float> partHealthFractions)
    {
        float health = 0f, max = 0f;
        foreach (var (name, def) in _parts)
        {
            float frac = partHealthFractions.TryGetValue(name, out var f) ? f : 1f;
            health += frac * def.MaxHp;
            max += def.MaxHp;
        }
        return max > 0f ? health / max : 1f;
    }

    /// <summary>Damage-lab drive (static viewer): the parked plane never moves, so the authored
    /// distance-interval trails would emit nothing — the stand-in puffers burn in place instead,
    /// at <see cref="StaticBurnSpeed"/> of virtual motion per second (panel fires and, when
    /// smoking, the prop1 smoke/fire pair rise from the standing plane).</summary>
    public void UpdateStatic(float dt)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.Emit(panel.GlobalPosition, panel.GlobalTransform.Basis, dt, StaticBurnSpeed);
        if (!_smoking)
            return;
        if (_prop1 is not { } nose)
            return;
        _standInTrailPuffer?.Emit(nose.GlobalPosition, nose.GlobalTransform.Basis, dt, StaticBurnSpeed);
        _standInFirePuffer?.Emit(nose.GlobalPosition, nose.GlobalTransform.Basis, dt, StaticBurnSpeed);
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

    // Merged mesh-AABB center of the panel's subtree, in the plane root's
    // frame. The pdpN nodes carry transforms but the _h twins sit at the origin with
    // their placement baked into the mesh — mesh AABBs locate both.
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

    // Transform of `node` relative to `root`
    // by walking parents — works before the subtree enters the scene tree.
    private static Transform3D RelativeTo(Node3D node, Node3D root)
    {
        var xf = Transform3D.Identity;
        for (Node3D? n = node; n != null && n != root; n = n.GetParent() as Node3D)
            xf = n.Transform * xf;
        return xf;
    }

    // First node under `root` whose Godot name or gamez
    // `cs_name` meta matches — the same two names AnimRuntime resolution reads.
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

    // Routes one authored stage anim out through the rig runtime, or says (once) why it
    // cannot: a world-less flight has no rig runtime and renders panel flips alone.
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

    // Pairs each healthy pdpN_h skin with the nearest torn panel (mesh-AABB centers,
    // same side of the centerline). CANDIDATE sets are def-derived when given — see
    // architecture.md's DamageVisuals entry. The ASSIGNMENT inside those sets is
    // always positional: no def says which panel hides which skin, and name-based
    // pairing is wrong on three planes.
    private void PairHealthySkins(Node3D planeRoot, PanelPairing? defPairing)
    {
        if (defPairing == null)
        {
            Utils.Log.Warn("flight", $"damage panels: no authored pairing data (plane_reset/pdpanelN defs unavailable) — geometric AABB pairing over every *_h skin engaged");
        }
        var torn = new List<(string Name, Vector3 Center)>();
        var healthy = new List<(string Name, Node3D Node, Vector3 Center)>();
        foreach (var (name, node) in _panels)
        {
            if (!TryMeshCenter(node, planeRoot, out var c))
                continue;
            if (name.EndsWith("_h", StringComparison.OrdinalIgnoreCase))
            {
                if (defPairing != null && !defPairing.HideableHealthy.Contains(name))
                {
                    GD.Print($"damage panels: {name} is not in the authored reset list — never hidden");
                    continue;
                }
                healthy.Add((name, node, c));
            }
            else
            {
                if (defPairing != null && !defPairing.TornTargets.Contains(name))
                {
                    GD.Print($"damage panels: {name} is not an authored pdpanelN target — not paired");
                    continue;
                }
                torn.Add((name, c));
            }
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

/// <summary>The authored panel candidate sets (<see cref="DamageVisuals.PanelPairingSets"/>):
/// which healthy `*_h` skins damage may hide (`plane_reset`'s re-ACTIVE list) and which torn
/// `pdpN` panels participate (the `pdpanelN` defs' targets). Case-insensitive sets.</summary>
public sealed record PanelPairing(IReadOnlySet<string> HideableHealthy, IReadOnlySet<string> TornTargets);
