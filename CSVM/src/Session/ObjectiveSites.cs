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
    /// <summary>The flagged world node's name — this site's identity string, what
    /// <c>--target=</c> matches.</summary>
    public string Node { get; init; } = "";

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

    /// <summary>The nodes carrying the objective-target flag right now: every
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

        foreach (var name in graph.ObjectiveTargets)
        {
            if (!Holds(into, name))
            {
                into.Add(name);
            }
        }
    }

    /// <summary>Where a target sits: the bare <c>TRAVELERS</c> point of the objective that edits
    /// that target, where the mission gives one, and null to fall back to the world node.
    /// ⚠ Prefer the point over the node. C3/M01's village target names a node standing at the world
    /// origin, 7.9 km from the point its own objective tests, so the node is not the site.</summary>
    public static Vector3? PointFor(ObjectiveScript script, string node)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Travelers is { WherePoint: { } p }
                && (Holds(def.RemoveObjectiveTarget, node) || Holds(def.AddObjectiveTarget, node)))
            {
                return new Vector3(p[0], p[1], p[2]);
            }
        }

        return null;
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

    private static bool RemovedByCompletion(ObjectiveScript script, ObjectiveGraph graph, string node)
    {
        foreach (var def in script.Objectives)
        {
            if (graph.CompletedOf(def.Number) && Holds(def.RemoveObjectiveTarget, node))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Holds(IReadOnlyList<string> names, string node)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, node, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // A message key resolves to itself when unknown, which is right for a readout and wrong for a
    // marker; a whitespace-only value is how a mission clears a label, so both read as absent.
    private string Text(string? key) =>
        string.IsNullOrWhiteSpace(key) ? "" : _messages.Get(key).Trim();

    private Vector3? Where(string node) =>
        PointFor(_director.Script, node) ?? Resolve(node)?.GlobalPosition;

    // Cached: FindNodes walks the world index by name and this runs once per pane per frame.
    // ⚠ Re-resolve an invalid or detached node rather than caching the miss. `piratezep` is
    // switched off and moved while the world is built, and a dock point sits under it.
    private Node3D? Resolve(string node)
    {
        if (_nodes.TryGetValue(node, out var cached)
            && GodotObject.IsInstanceValid(cached) && cached.IsInsideTree())
        {
            return cached;
        }

        if (_runtime == null)
        {
            return null;
        }

        var found = _runtime.FindNodes(node, null);
        if (found.Count == 0 || !found[0].IsInsideTree())
        {
            return null;
        }

        _nodes[node] = found[0];
        return found[0];
    }

    // The strings are re-read every frame because SET_HELP_LABEL rewrites a live site's category;
    // the instance itself survives that, since it is the identity the selection is held by.
    private ObjectiveSite SiteFor(string node, ObjectiveGraph graph, Vector3 at)
    {
        if (!_sites.TryGetValue(node, out var site))
        {
            site = new ObjectiveSite { Node = node };
            _sites[node] = site;
        }

        var info = _targets.For(node);
        string name = Text(info.Description);
        string typeLabel = Text(info.CategoryLabel);
        string category = Text(graph.HelpLabels.TryGetValue(node, out var written)
            ? written
            : info.HelpLabel);
        site.DisplayName = name.Length > 0 ? name : node;
        site.TypeLabel = typeLabel.Length > 0 ? typeLabel : null;
        site.Category = category.Length > 0 ? category : null;
        site.Position = at;
        return site;
    }
}
