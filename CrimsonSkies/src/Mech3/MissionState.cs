using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Mission start-state applier (anim-state engine part 1, Run-2 item 8). At world build it
/// replays the *state* consequences of the mission's animation set so the world matches
/// what the original shows at mission start:
///
///  1. every anchored ANIMATION_DEFINITION's RESET_STATE (base states — hides `destroyed`
///     building/vehicle variants that the gamez stores overlaid on their healthy twins),
///  2. ACTIVATION ON_STARTUP definitions' sequences (zepstate.json — deactivates the
///     zeppelins/trains this mission doesn't use),
///  3. the mission's startanims.json NEW_GAME_START animations' end states (hangar doors
///     end open; the train exists — its path *motion* is part 2),
///  4. a safety net hiding any still-visible `destroyed`-named subtree no definition
///     covered (logged — each is a data-coverage gap to chase).
///
/// "Inactive" = hidden AND non-collidable (crashing into an invisible zeppelin would be
/// worse than the visual bug). Definitions resolve to built nodes by their ORIGINAL gamez
/// names (SceneBuilder stores them in the `cs_name` meta — Godot mangles duplicate
/// sibling names, so Node.Name is unreliable). Wildcard NAMEs (`ftank0*`, `s_build**`)
/// anchor per matching instance; LOCAL_NODES_ONLY scopes op targets to each anchor's
/// subtree. Playback ops (SI scripts, sounds, cameras, puffers) are ignored in part 1.
/// </summary>
public sealed class MissionState
{
    /// <summary>Original-name metadata key SceneBuilder stamps on every built Node3D.</summary>
    public const string NameMeta = "cs_name";

    private readonly List<(Node3D Node, string SrcName)> _index = new();
    private readonly Node3D _root;
    private readonly Dictionary<Node3D, Transform3D> _rest = new(); // pre-anim pose per touched node

    private int _opsApplied;
    private int _opsUnresolved;

    private MissionState(Node3D worldRoot)
    {
        _root = worldRoot;
        void Walk(Node3D n)
        {
            var srcName = n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();
            _index.Add((n, srcName));
            foreach (var child in n.GetChildren())
                if (child is Node3D c)
                    Walk(c);
        }
        Walk(worldRoot);
    }

    /// <summary>
    /// Loads the animation definitions visible to a mission (shared + chapter + mission
    /// zrdr, in that order — the original compiles the same sources into mis_anim.zbd)
    /// and applies the start states to the built world. Call right after the world build.
    /// </summary>
    public static void Apply(Node3D worldRoot, string sharedZrdr, string chapterZrdr, string missionZrdr)
    {
        var state = new MissionState(worldRoot);

        var defs = new List<AnimDef>();
        int shared = Load(defs, sharedZrdr), chapter = Load(defs, chapterZrdr), mission = Load(defs, missionZrdr);

        // Pass 1+2: base states. Anchored defs only — a def whose NAME matches nothing in
        // this world (player-plane anims, cutscene rigs) must not stomp globally-resolved
        // bare names like 'destroyed'.
        int anchored = 0;
        foreach (var def in defs)
        {
            var anchors = state.Anchors(def);
            if (anchors.Count == 0)
                continue;
            anchored++;
            foreach (var anchor in anchors)
                state.ApplyOps(def.ResetState, anchor, def.LocalNodesOnly);
        }
        foreach (var def in defs.Where(d => d.OnStartup))
            foreach (var anchor in state.Anchors(def))
                foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
                    state.ApplyOps(seq.Ops, anchor, def.LocalNodesOnly);

        // Pass 3: the mission's start animations — applied by ANIMATION_NAME, end states
        // only. A start anim with no world anchor still applies globally (its def may
        // target world nodes by unique name, e.g. hangar doors).
        var ranAnims = new List<string>();
        var missingAnims = new List<string>();
        foreach (var animName in LoadStartAnims(missionZrdr))
        {
            var matches = defs.Where(d =>
                string.Equals(d.AnimationName, animName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
            {
                missingAnims.Add(animName);
                continue;
            }
            ranAnims.Add(animName);
            foreach (var def in matches)
            {
                var anchors = state.Anchors(def);
                if (anchors.Count == 0)
                    anchors.Add(null); // global resolution
                foreach (var anchor in anchors)
                {
                    state.ApplyOps(def.ResetState, anchor, def.LocalNodesOnly);
                    foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
                        state.ApplyOps(seq.Ops, anchor, def.LocalNodesOnly);
                }
            }
        }

        // Pass 4: safety net for destroyed-variant subtrees no definition covered.
        var netHidden = state.HideUncoveredDestroyed();

        GD.Print($"mission state: {defs.Count} anim defs ({shared} shared, {chapter} chapter, {mission} mission), " +
                 $"{anchored} anchored; {state._opsApplied} state ops applied, {state._opsUnresolved} unresolved");
        if (ranAnims.Count > 0 || missingAnims.Count > 0)
            GD.Print($"mission state: start anims [{string.Join(", ", ranAnims)}]" +
                     (missingAnims.Count > 0 ? $", undefined here: [{string.Join(", ", missingAnims)}]" : ""));
        if (netHidden.Count > 0)
            GD.Print($"mission state: safety net hid {netHidden.Count} uncovered destroyed subtree(s): " +
                     string.Join(", ", netHidden.Take(10)) + (netHidden.Count > 10 ? ", …" : ""));
    }

    private static int Load(List<AnimDef> into, string zrdrPath)
    {
        var defs = AnimDefs.LoadArchive(zrdrPath);
        into.AddRange(defs);
        return defs.Count;
    }

    // startanims.json: [["NEW_GAME_START", [[name], [name], …], "LOAD_GAME_START", …]]
    private static List<string> LoadStartAnims(string missionZrdr)
    {
        var names = new List<string>();
        try
        {
            var root = Zrdr.LoadFile(missionZrdr, "startanims.json");
            if (root.Count > 0 && root[0] is List<object?> outer)
                for (int i = 0; i + 1 < outer.Count; i++)
                    if (outer[i] is string k && k.Equals("NEW_GAME_START", StringComparison.OrdinalIgnoreCase)
                        && outer[i + 1] is List<object?> list)
                        foreach (var entry in list)
                            if (entry is List<object?> { Count: > 0 } e && e[0] is string name)
                                names.Add(name);
        }
        catch (Exception)
        {
            // startanims.json is optional (not every mission folder has one)
        }
        return names;
    }

    // ---- resolution ----

    // ANIMATION_ROOT_NAME matches above this count are generic per-object roots ('healthy'
    // appears 217× in C1) — those defs are wired to game objects (planes, zeppelin parts),
    // not anchorable by world-node name; part 2's object wiring owns them. The genuine
    // building templates lift ≤ 9 instances (lifeballoon).
    private const int MaxRootLift = 16;

    /// <summary>World nodes a definition anchors to: NAME matches (wildcards = one per
    /// building/vehicle instance); else ANIMATION_ROOT_NAME matches lifted to their parent
    /// (the instance root, so sibling healthy/destroyed both resolve locally — needed where
    /// instance roots have free names: `m_build**` instances are `apbuild01.flt`…).</summary>
    private List<Node3D?> Anchors(AnimDef def)
    {
        // Multi-target NAME1 definitions (zeppelin nacelles/turrets) parse with an empty
        // NAME; they animate per-object sub-parts and are part-2 scope — never anchor them
        // (their generic ROOT names would anchor them onto every building in the world).
        if (string.IsNullOrEmpty(def.Name))
            return new List<Node3D?>();
        var anchors = FindAll(def.Name, null).Cast<Node3D?>().ToList();
        if (anchors.Count == 0 && def.RootName != null)
        {
            var roots = FindAll(def.RootName, null);
            if (roots.Count > 0 && roots.Count <= MaxRootLift)
                anchors = roots
                    .Select(n => n.GetParent() as Node3D)
                    .Where(p => p != null)
                    .Distinct()
                    .ToList();
        }
        return anchors;
    }

    private void ApplyOps(List<AnimStateOp> ops, Node3D? scope, bool localOnly)
    {
        foreach (var op in ops)
        {
            var targets = ResolvePath(op.TargetPath, scope, localOnly);
            if (targets.Count == 0)
            {
                _opsUnresolved++;
                continue;
            }
            foreach (var t in targets)
                ApplyOp(op, t);
        }
    }

    private void ApplyOp(AnimStateOp op, Node3D target)
    {
        if (op.Kind == "OBJECT_ACTIVE_STATE")
        {
            SetSubtreeActive(target, op.Active);
            _opsApplied++;
            return;
        }
        // Pose states/motions: offsets from the node's authored rest pose. Rotation
        // composes the game's Euler order (Yxz, degrees) onto the rest basis.
        if (!_rest.TryGetValue(target, out var rest))
            _rest[target] = rest = target.Transform;
        if (op.Translate is { } tr)
            target.Position = rest.Origin + tr;
        if (op.Rotate is { } rot)
            target.Basis = rest.Basis * Basis.FromEuler(
                new Vector3(Mathf.DegToRad(rot.X), Mathf.DegToRad(rot.Y), Mathf.DegToRad(rot.Z)),
                EulerOrder.Yxz);
        if (op.Translate != null || op.Rotate != null)
            _opsApplied++;
    }

    // NAME paths: resolve the first element in scope (falling back to global for
    // non-local defs), then each further element inside the previous matches.
    private List<Node3D> ResolvePath(List<string> path, Node3D? scope, bool localOnly)
    {
        if (path.Count == 0)
            return new List<Node3D>();
        var candidates = FindAll(path[0], scope);
        if (candidates.Count == 0 && scope != null && !localOnly)
            candidates = FindAll(path[0], null);
        for (int i = 1; i < path.Count && candidates.Count > 0; i++)
        {
            var next = new List<Node3D>();
            foreach (var c in candidates)
                next.AddRange(FindAll(path[i], c).Where(n => n != c));
            candidates = next;
        }
        return candidates;
    }

    private List<Node3D> FindAll(string pattern, Node3D? scope)
    {
        var match = Matcher(pattern);
        var result = new List<Node3D>();
        foreach (var (node, srcName) in _index)
        {
            // Defs may name a node without its model-file suffix ('ap_radiotwr' for the
            // gamez node 'ap_radiotwr.flt') — try both.
            var matches = match(srcName)
                || (srcName.EndsWith(".flt", StringComparison.OrdinalIgnoreCase) && match(srcName[..^4]));
            if (matches && (scope == null || node == scope || scope.IsAncestorOf(node)))
                result.Add(node);
        }
        return result;
    }

    private readonly Dictionary<string, Func<string, bool>> _matcherCache = new(StringComparer.OrdinalIgnoreCase);

    // Wildcard NAME → predicate: '*' (and the '**' template form) match any run of
    // characters, '#' a run of digits ('air_gen#' covers 'air_gen'). Plain names compare
    // exactly (case-insensitive, like every reader name lookup).
    private Func<string, bool> Matcher(string pattern)
    {
        if (_matcherCache.TryGetValue(pattern, out var cached))
            return cached;
        Func<string, bool> match;
        if (pattern.Contains('*') || pattern.Contains('#'))
        {
            var re = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\#", "[0-9]*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            match = re.IsMatch;
        }
        else
        {
            match = s => s.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
        return _matcherCache[pattern] = match;
    }

    // ---- state application ----

    // INACTIVE = invisible and non-collidable, the whole subtree; ACTIVE re-enables both.
    private static void SetSubtreeActive(Node3D node, bool active)
    {
        node.Visible = active;
        SetCollidersEnabled(node, active);
    }

    private static void SetCollidersEnabled(Node node, bool enabled)
    {
        if (node is CollisionShape3D shape)
            shape.Disabled = !enabled;
        foreach (var child in node.GetChildren())
            SetCollidersEnabled(child, enabled);
    }

    // Any still-visible node named like a destroyed variant that no definition touched:
    // hide it and report — each name is a data-coverage gap (a def we failed to anchor).
    // Match 'destroyed' only: a '_dest' suffix rule proved WRONG — C1's `ref_tank_dest` is
    // the parent GROUP of the five healthy harbor refuel tanks ("destructible", not
    // "destroyed"), and hiding it wiped the visible tanks (user-reported).
    private List<string> HideUncoveredDestroyed()
    {
        var hidden = new List<string>();
        foreach (var (node, srcName) in _index)
        {
            if (!srcName.Contains("destroyed", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!node.Visible || HasHiddenAncestor(node))
                continue;
            SetSubtreeActive(node, false);
            hidden.Add(srcName);
        }
        return hidden;
    }

    private bool HasHiddenAncestor(Node3D node)
    {
        for (var p = node.GetParent() as Node3D; p != null && p != _root; p = p.GetParent() as Node3D)
            if (!p.Visible)
                return true;
        return false;
    }
}
