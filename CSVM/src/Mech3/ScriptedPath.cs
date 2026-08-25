using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// One authored waypoint path a placed vehicle can be driven along instead of being flight
/// simulated. The path is world geometry, not roster data: the mission's <c>aiv.zrd</c> names it
/// (<c>pp1</c>), and the gamez carries it as the transform-only subtree <c>pp1_aipath</c> whose
/// <c>pp1_aipN</c> children are the waypoints in order. Ten vehicles across three missions carry
/// one. Decode: <c>docs/org/flightModel.md</c>, "Ground blow", and
/// <c>docs/formats/ai-rosters.md</c>'s <c>taxiPath</c> slot.
/// </summary>
public sealed class ScriptedPath
{
    /// <summary>The suffix a path name takes in the gamez: <c>pp1</c> is the subtree
    /// <c>pp1_aipath</c>. The roster authors the bare name.</summary>
    public const string RootSuffix = "_aipath";

    /// <summary>The suffix a waypoint takes under that root, followed by its ordinal:
    /// <c>pp1_aip0</c>, <c>pp1_aip1</c>. The ordinal is the leg order.</summary>
    public const string WaypointSuffix = "_aip";

    private ScriptedPath(string name, IReadOnlyList<Vector3> waypoints)
    {
        Name = name;
        Waypoints = waypoints;
    }

    /// <summary>The authored path name, as the roster spells it.</summary>
    public string Name { get; }

    /// <summary>The waypoints in leg order, in world space. Always at least two.</summary>
    public IReadOnlyList<Vector3> Waypoints { get; }

    /// <summary>Resolves an authored path name against the built world. <paramref name="findNodes"/>
    /// is the world's one name resolver (<c>AnimRuntime.FindNodes</c>). Null when the chapter
    /// carries no such subtree or it holds fewer than two waypoints, which is a path nothing can be
    /// driven along; the caller reports that rather than inventing a route.</summary>
    public static ScriptedPath? Resolve(string name, Func<string, IReadOnlyList<Node3D>> findNodes)
    {
        var roots = findNodes(name + RootSuffix);
        if (roots.Count == 0)
        {
            return null;
        }

        string prefix = name + WaypointSuffix;
        var ordered = new List<(int Ordinal, Vector3 Position)>();
        foreach (var child in roots[0].GetChildren())
        {
            if (child is not Node3D node)
            {
                continue;
            }

            string childName = node.HasMeta(AnimRuntime.NameMeta)
                ? node.GetMeta(AnimRuntime.NameMeta).AsString()
                : node.Name.ToString();
            if (childName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(childName[prefix.Length..], out int ordinal))
            {
                ordered.Add((ordinal, node.GlobalPosition));
            }
        }

        if (ordered.Count < 2)
        {
            return null;
        }

        ordered.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));
        var points = new List<Vector3>(ordered.Count);
        foreach (var (_, position) in ordered)
        {
            points.Add(position);
        }

        return new ScriptedPath(name, points);
    }
}
