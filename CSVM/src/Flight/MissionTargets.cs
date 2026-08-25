using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;

namespace CSVM.Flight;

/// <summary>The display keys a mission's <c>targets.json</c> attaches to one world node: a
/// <c>description</c> (the target's own name, e.g. <c>MSG_OBJ_TRAINTUNNEL_M</c>), a
/// <c>category_label</c> (its type, e.g. <c>MSG_OBJ_DZ</c> = "Danger Zone"), and a
/// <c>help_label</c> (the action, e.g. <c>MSG_OBJ_FLYTHROUGH</c> = "Fly Through"). Any may be
/// absent (a plain reference point has only a description + help). Resolve the keys through
/// <see cref="Messages"/>. <c>Objective</c> and <c>OtherTarget</c> are the file's own valueless
/// <c>objective</c> / <c>other_target</c> keys: the marker flags a mission STARTS with, which
/// <c>objectives.zrd</c>'s <c>ADD_/REMOVE_OBJECTIVE_TARGET</c> then edits.
/// ⚠ Read <c>Objective</c> for the starting set. C3/M01 only ever REMOVES its three sites, so a
/// set built from the script's adds alone is empty for the whole mission.</summary>
public readonly record struct MissionTarget(string? Description, string? CategoryLabel,
    string? HelpLabel, bool Objective = false, bool OtherTarget = false);

/// <summary>
/// Loads a mission's <c>targets.json</c> — the map from a world node NAME to its objective
/// display strings. The file is a list of target entries, each a list of <c>[key, value]</c>
/// pairs (NOT the flat-alternating reader form): <c>description</c>, a <c>nodes</c> list of
/// the node name(s) the entry labels, an optional <c>category_label</c>, and a
/// <c>help_label</c>. Generic across mission types — the stunt mode reads the <c>dzN</c>
/// entries, but dogfight/zeppelin targets parse identically.
/// </summary>
public sealed class MissionTargets
{
    private readonly Dictionary<string, MissionTarget> _byNode = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byNode.Count;

    /// <summary>Every entry, node name to its display keys — what a marker HUD walks to find the
    /// nodes the mission flags <c>objective</c> before its script has edited anything.</summary>
    public IReadOnlyDictionary<string, MissionTarget> ByNode => _byNode;

    /// <summary>Loads targets.json from a mission's zrdr (zip or unpacked dir). Missing file
    /// → an empty set (a mission may have none); malformed entries are skipped.</summary>
    public static MissionTargets Load(string missionZrdrPath)
    {
        var targets = new MissionTargets();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "targets.json");
        }
        catch (IOException)
        {
            return targets; // no targets.json for this mission
        }

        foreach (var entryObj in root)
        {
            if (entryObj is not List<object?> entry)
                continue;
            string? description = null, category = null, help = null;
            bool objective = false, other = false;
            List<object?>? nodes = null;
            foreach (var pairObj in entry)
            {
                if (pairObj is not List<object?> { Count: > 0 } pair || pair[0] is not string key)
                    continue;
                switch (key.ToLowerInvariant())
                {
                    case "description": description = Value(pair); break;
                    case "category_label": category = Value(pair); break;
                    case "help_label": help = Value(pair); break;
                    case "objective": objective = true; break;
                    case "other_target": other = true; break;
                    case "nodes": nodes = pair.Count > 1 ? pair[1] as List<object?> : null; break;
                }
            }
            if (nodes == null)
                continue;
            var info = new MissionTarget(description, category, help, objective, other);
            foreach (var n in nodes)
                if (n is string nodeName)
                    targets._byNode[nodeName] = info;
        }
        return targets;
    }

    /// <summary>The display keys for a world node, or an all-null <see cref="MissionTarget"/>
    /// if the node has no targets.json entry.</summary>
    public MissionTarget For(string nodeName) =>
        _byNode.TryGetValue(nodeName, out var t) ? t : default;

    private static string? Value(List<object?> pair) => pair.Count > 1 ? pair[1] as string : null;
}
