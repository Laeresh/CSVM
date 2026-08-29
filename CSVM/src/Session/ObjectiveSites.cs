using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>One live objective site as the targeting path sees it: the world node the mission
/// flagged, the two label lines its marker prints, and where the site is this frame. ONE instance
/// per site for as long as the mission flags it, because the selection is held by source identity
/// (<see cref="TargetRef.IsSameTarget"/>) and a fresh instance per rebuild would drop the pilot's
/// selection every frame.</summary>
public sealed class ObjectiveSite
{
    /// <summary>The flagged target's <see cref="ObjectiveTarget.Key"/> — this site's identity
    /// string, what <c>--target=</c> matches: a bare node name, or <c>parent/child</c> for a site
    /// the mission authored as a path.</summary>
    public string Node { get; init; } = "";

    /// <summary>The target itself: <see cref="ObjectiveTarget.Node"/> is the name of the world
    /// node the site stands on, the fallback a label is looked up by.</summary>
    public ObjectiveTarget Target { get; init; }

    /// <summary>The site's own resolved name, the marker's second line.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>The label half of the marker's line 1 (<c>category_label</c>), or null.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>The category half of line 1 (the live <c>help_label</c>), or null.</summary>
    public string? Category { get; set; }

    /// <summary>Where the site is now, re-read from its source on every rebuild.</summary>
    public Vector3 Position { get; set; }
}

/// <summary>
/// The flown campaign mission's objective sites, offered to each player's target pool as
/// objective-flagged candidates: the original carries an objective as a companion flag on the
/// Enemy cycle, so the ordinary selection draws one site at a time and d-pad up steps between
/// them. The set is <c>targets.zrd</c>'s own <c>objective</c> entries, edited by
/// <c>objectives.zrd</c>'s <c>ADD_/REMOVE_OBJECTIVE_TARGET</c> as objectives complete, with the
/// labels resolved through <see cref="Messages"/> and <c>SET_HELP_LABEL</c>. Bound to the roster
/// by <c>GameSession</c>; <see cref="Collect"/> runs once per pane per frame and re-reads each
/// site's position, which is what keeps a site on a moving node marked where it actually is.
/// </summary>
public sealed class ObjectiveSites
{
    private readonly CampaignDirector _director;
    private readonly Messages _messages;
    private readonly MissionTargets _targets;
    private readonly AnimRuntime? _runtime;
    private readonly Dictionary<string, ObjectiveSite> _sites =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Node3D> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _live = new();

    /// <summary>Reads a campaign director, the mission's target table and the world's node index.
    /// The director's graph may not exist yet; <see cref="Collect"/> polls for it and offers
    /// nothing until it does.</summary>
    public ObjectiveSites(CampaignDirector director, Messages messages, MissionTargets targets,
        AnimRuntime? runtime)
    {
        _director = director;
        _messages = messages;
        _targets = targets;
        _runtime = runtime;
    }

    /// <summary>The target keys carrying the objective-target flag right now: every
    /// <c>targets.zrd</c> entry the mission flags <c>objective</c> whose flag no completed
    /// objective has removed, plus everything <c>ADD_OBJECTIVE_TARGET</c> has since added.
    /// ⚠ Do not build this from <see cref="ObjectiveGraph.ObjectiveTargets"/> alone. That store
    /// starts empty, and a mission whose sites are only ever REMOVED offers nothing at all.</summary>
    public static void CollectTargets(ObjectiveScript script, ObjectiveGraph graph,
        MissionTargets targets, List<string> into)
    {
        foreach (var entry in targets.ByNode)
        {
            if (entry.Value.Objective && !RemovedByCompletion(script, graph, entry.Key))
            {
                into.Add(entry.Key);
            }
        }

        foreach (var key in graph.ObjectiveTargets)
        {
            if (!Listed(into, key))
            {
                into.Add(key);
            }
        }
    }

    /// <summary>Where a target sits: the bare <c>TRAVELERS</c> point of the objective that edits
    /// that target, where the mission gives one, and null to fall back to the world node.
    /// ⚠ Prefer the point over the node. C3/M01's village target names a node standing at the world
    /// origin, 7.9 km from the point its own objective tests, so the node is not the site.</summary>
    public static Vector3? PointFor(ObjectiveScript script, string key)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Travelers is { WherePoint: { } p }
                && (Holds(def.RemoveObjectiveTarget, key) || Holds(def.AddObjectiveTarget, key)))
            {
                return new Vector3(p[0], p[1], p[2]);
            }
        }

        return null;
    }

    /// <summary>The one world node a target stands on: a bare name is the first global match,
    /// and a path is walked one name at a time, each found inside the node before it, so
    /// <c>piratezep/rock_zeppelin</c> is the hull's own child and never a ground node of the
    /// same name. Null when any step resolves to nothing.</summary>
    public static Node3D? ResolveTarget(AnimRuntime runtime, ObjectiveTarget target)
    {
        Node3D? node = null;
        foreach (var name in target.Path)
        {
            var found = runtime.FindNodes(name, node);
            if (found.Count == 0)
            {
                return null;
            }

            node = found[0];
        }

        return node;
    }

    /// <summary>Where a resolved site node's marker stands: the node's own position for a placed
    /// node, and for a group node standing at the world origin that draws nothing itself the
    /// centre of its parts, read off the built meshes. ⚠ Do not read such a node's position.
    /// C2's seaplane hangar (<c>sghangar</c>) is a group at the world origin whose door leaves and
    /// body carry the world coordinates, 8 km from it. A <c>door</c>-named leaf pair wins over the
    /// whole, since a fly-through site means the aperture (the stunt mode's own rule).</summary>
    public static Vector3 SiteAnchor(Node3D node)
    {
        if (node is MeshInstance3D || HasOwnMesh(node) || !node.GlobalPosition.IsZeroApprox())
        {
            return node.GlobalPosition;
        }

        var boxes = new List<(string Name, Aabb Box)>();
        CollectMeshBoxes(node, node.Name, boxes);
        if (boxes.Count == 0)
        {
            return node.GlobalPosition;
        }

        int doors = 0;
        foreach (var box in boxes)
        {
            if (IsDoorLeaf(box.Name))
            {
                doors++;
            }
        }

        Aabb? merged = null;
        foreach (var box in boxes)
        {
            if (doors >= 2 && !IsDoorLeaf(box.Name))
            {
                continue;
            }

            merged = merged?.Merge(box.Box) ?? box.Box;
        }

        return merged!.Value.GetCenter();
    }

    /// <summary>Appends this frame's live sites, each as an objective-flagged candidate the pool
    /// files on the Enemy cycle. Every site is rebuilt from its live source, so a site under a
    /// moving node moves with it and a completed site is simply not offered again.</summary>
    public void Collect(List<AimCandidate> into)
    {
        if (_director.Graph is not { } graph)
        {
            return;
        }

        _live.Clear();
        CollectTargets(_director.Script, graph, _targets, _live);
        foreach (var node in _live)
        {
            if (Where(node) is not { } at)
            {
                continue;
            }

            into.Add(new AimCandidate
            {
                Position = at,
                Team = AimAssist.NeutralTeam,
                Live = true,
                ConeOverride = AimAssist.NoConeOverride,
                Source = SiteFor(node, graph, at),
            });
        }
    }

    private static bool RemovedByCompletion(ObjectiveScript script, ObjectiveGraph graph, string key)
    {
        foreach (var def in script.Objectives)
        {
            if (graph.CompletedOf(def.Number) && Holds(def.RemoveObjectiveTarget, key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Listed(List<string> keys, string key)
    {
        foreach (var listed in keys)
        {
            if (string.Equals(listed, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Holds(IReadOnlyList<ObjectiveTarget> targets, string key)
    {
        foreach (var target in targets)
        {
            if (target.Is(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOwnMesh(Node3D node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D { Mesh: not null })
            {
                return true;
            }
        }

        return false;
    }

    // One world-frame box per drawing node under `node`, named for the gamez node that owns the
    // mesh (the mesh instance itself is always called "mesh").
    private static void CollectMeshBoxes(Node3D node, string owner, List<(string Name, Aabb Box)> into)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D { Mesh: not null } mi)
            {
                into.Add((owner, mi.GlobalTransform * mi.GetAabb()));
            }
            else if (child is Node3D c3d)
            {
                CollectMeshBoxes(c3d, c3d.Name, into);
            }
        }
    }

    private static bool IsDoorLeaf(string name) =>
        name.Contains("door", StringComparison.OrdinalIgnoreCase);

    // A message key resolves to itself when unknown, which is right for a readout and wrong for a
    // marker; a whitespace-only value is how a mission clears a label, so both read as absent.
    private string Text(string? key) =>
        string.IsNullOrWhiteSpace(key) ? "" : _messages.Get(key).Trim();

    private Vector3? Where(string key) =>
        PointFor(_director.Script, key) ?? (Resolve(key) is { } node ? SiteAnchor(node) : null);

    // Cached: FindNodes walks the world index by name and this runs once per pane per frame.
    // ⚠ Re-resolve an invalid or detached node rather than caching the miss. `piratezep` is
    // switched off and moved while the world is built, and a dock point sits under it.
    private Node3D? Resolve(string key)
    {
        if (_nodes.TryGetValue(key, out var cached)
            && GodotObject.IsInstanceValid(cached) && cached.IsInsideTree())
        {
            return cached;
        }

        if (_runtime == null)
        {
            return null;
        }

        var found = ResolveTarget(_runtime, ObjectiveTarget.Parse(key));
        if (found == null || !found.IsInsideTree())
        {
            return null;
        }

        _nodes[key] = found;
        return found;
    }

    // The strings are re-read every frame because SET_HELP_LABEL rewrites a live site's category;
    // the instance itself survives that, since it is the identity the selection is held by.
    private ObjectiveSite SiteFor(string key, ObjectiveGraph graph, Vector3 at)
    {
        if (!_sites.TryGetValue(key, out var site))
        {
            site = new ObjectiveSite { Node = key, Target = ObjectiveTarget.Parse(key) };
            _sites[key] = site;
        }

        // targets.zrd keys a path-authored entry by the same parent/child key the script uses,
        // and a bare one by the node; a path added by the script over a bare entry reads the node.
        var info = _targets.For(key);
        if (info == default)
        {
            info = _targets.For(site.Target.Node);
        }

        string name = Text(info.Description);
        string typeLabel = Text(info.CategoryLabel);
        string category = Text(graph.HelpLabels.TryGetValue(key, out var written)
            ? written
            : info.HelpLabel);
        site.DisplayName = name.Length > 0 ? name : site.Target.Node;
        site.TypeLabel = typeLabel.Length > 0 ? typeLabel : null;
        site.Category = category.Length > 0 ? category : null;
        site.Position = at;
        return site;
    }
}
