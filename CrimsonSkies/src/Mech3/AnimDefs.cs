using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// One state-changing event inside an ANIMATION_DEFINITION (reset state or sequence).
/// Part 1 of the anim-state engine parses only the *state* op kinds — active/pose setters
/// and the end pose of timed motions; playback ops (SI scripts, sounds, camera, puffers)
/// are recorded by kind for logging and otherwise ignored. See docs/formats/anim-definitions.md.
/// </summary>
public sealed class AnimStateOp
{
    public required string Kind;              // OBJECT_ACTIVE_STATE / OBJECT_TRANSLATE_STATE / …
    public List<string> TargetPath = new();   // NAME values; >1 entry = a parent→child path
    public bool Active;                       // OBJECT_ACTIVE_STATE: ACTIVE (true) / INACTIVE
    public Vector3? Translate;                // end/base local translation offset (game units)
    public Vector3? Rotate;                   // end/base local rotation (degrees)
}

/// <summary>A SEQUENCE_DEFINITION: state ops that run when the animation runs — unless the
/// sequence itself is ACTIVATION ON_CALL (only triggered by CALL_SEQUENCE, which part 1
/// does not follow).</summary>
public sealed class AnimSequence
{
    public string? Name;
    public bool OnCallOnly;
    public List<AnimStateOp> Ops = new();
    public List<string> SkippedOps = new();   // op kinds present but not state-applicable
}

/// <summary>
/// One ANIMATION_DEFINITION from a zrdr reader. NAME anchors the definition to world
/// node(s) — it may be a wildcard template (`s_build**`, `ftank0*`) matching one node per
/// building/vehicle instance; with LOCAL_NODES_ONLY the ops' node names resolve inside
/// each anchor's subtree. ANIMATION_NAME is what startanims.json / CALL_ANIMATION refer to.
/// </summary>
public sealed class AnimDef
{
    public string Name = "";
    public string? AnimationName;
    public string? RootName;                  // ANIMATION_ROOT_NAME (attach node inside the instance)
    public bool LocalNodesOnly;
    public string? Activation;                // ON_STARTUP / ON_CALL (default)
    public List<AnimStateOp> ResetState = new();
    public List<AnimSequence> Sequences = new();
    public string SourceFile = "";

    public bool OnStartup => string.Equals(Activation, "ON_STARTUP", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Loader for ANIMATION_DEFINITIONS reader files (zepstate.json, hangar3.json,
/// small_building.json, …). The original compiles these into mis_anim.zbd (a format
/// mech3ax cannot read); scanning the zrdr JSON sources gives the same state data.
/// </summary>
public static class AnimDefs
{
    // Op kinds whose effect is a node state part 1 can apply at world build.
    private static readonly HashSet<string> StateOpKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "OBJECT_ACTIVE_STATE", "OBJECT_TRANSLATE_STATE", "OBJECT_ROTATE_STATE", "OBJECT_MOTION_FROM_TO",
    };

    /// <summary>Loads every ANIMATION_DEFINITION from all reader files in a zrdr zip/dir.
    /// Missing archive → empty list (mission folders without anim readers are normal).</summary>
    public static List<AnimDef> LoadArchive(string zrdrPath)
    {
        var defs = new List<AnimDef>();
        foreach (var (name, root) in Zrdr.LoadMatchingFiles(zrdrPath, "ANIMATION_DEFINITIONS"))
        {
            // Root shape: [["ANIMATION_DEFINITIONS", [ …, "ANIMATION_LIST", [
            //   "ANIMATION_DEFINITION", [def], … ] ] ]]
            foreach (var defList in Walk(root, "ANIMATION_DEFINITIONS", "ANIMATION_LIST", "ANIMATION_DEFINITION"))
            {
                var def = ParseDef(defList);
                def.SourceFile = name;
                defs.Add(def);
            }
        }
        return defs;
    }

    // Yields every value list reached by following the given alternating-key chain; the
    // last key may repeat (ANIMATION_DEFINITION does), so all its occurrences are yielded.
    private static IEnumerable<List<object?>> Walk(List<object?> list, params string[] keys)
    {
        var frontier = new List<List<object?>> { list };
        // The top-level file root nests one extra list around the alternating pairs.
        if (list.Count > 0 && list[0] is List<object?>)
            frontier = new List<List<object?>>(list.ConvertAll(o => o as List<object?>).FindAll(l => l != null)!);
        foreach (var key in keys)
        {
            var next = new List<List<object?>>();
            foreach (var l in frontier)
                for (int i = 0; i + 1 < l.Count; i++)
                    if (l[i] is string k && k.Equals(key, StringComparison.OrdinalIgnoreCase)
                        && l[i + 1] is List<object?> v)
                        next.Add(v);
            frontier = next;
        }
        return frontier;
    }

    private static AnimDef ParseDef(List<object?> list)
    {
        var def = new AnimDef();
        foreach (var (key, value) in Pairs(list))
        {
            switch (key.ToUpperInvariant())
            {
                case "NAME": def.Name = FirstString(value) ?? def.Name; break;
                case "ANIMATION_NAME": def.AnimationName = FirstString(value); break;
                case "ANIMATION_ROOT_NAME": def.RootName = FirstString(value); break;
                case "ACTIVATION": def.Activation = FirstString(value); break;
                case "LOCAL_NODES_ONLY": def.LocalNodesOnly = true; break;
                case "RESET_STATE":
                    if (value != null)
                        def.ResetState.AddRange(ParseOps(value, skipped: null));
                    break;
                case "SEQUENCE_DEFINITION":
                    if (value != null)
                        def.Sequences.Add(ParseSequence(value));
                    break;
            }
        }
        return def;
    }

    private static AnimSequence ParseSequence(List<object?> list)
    {
        var seq = new AnimSequence();
        foreach (var (key, value) in Pairs(list))
        {
            if (key.Equals("NAME", StringComparison.OrdinalIgnoreCase))
                seq.Name = FirstString(value);
            else if (key.Equals("ACTIVATION", StringComparison.OrdinalIgnoreCase))
                seq.OnCallOnly = string.Equals(FirstString(value), "ON_CALL", StringComparison.OrdinalIgnoreCase);
            else if (StateOpKinds.Contains(key))
            {
                if (value != null && ParseOp(key, value) is { } op)
                    seq.Ops.Add(op);
            }
            else
                seq.SkippedOps.Add(key);
        }
        return seq;
    }

    private static IEnumerable<AnimStateOp> ParseOps(List<object?> list, List<string>? skipped)
    {
        foreach (var (key, value) in Pairs(list))
        {
            if (StateOpKinds.Contains(key))
            {
                if (value != null && ParseOp(key, value) is { } op)
                    yield return op;
            }
            else
                skipped?.Add(key);
        }
    }

    private static AnimStateOp? ParseOp(string kind, List<object?> body)
    {
        var op = new AnimStateOp { Kind = kind.ToUpperInvariant() };
        foreach (var (key, value) in Pairs(body))
        {
            if (value == null)
                continue;
            switch (key.ToUpperInvariant())
            {
                case "NAME":
                    foreach (var v in value)
                        if (v is string s)
                            op.TargetPath.Add(s);
                    break;
                case "STATE":
                    // OBJECT_ACTIVE_STATE carries a string; the pose states carry a vec3.
                    if (FirstString(value) is { } state)
                        op.Active = state.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);
                    else if (ReadVec(value) is { } pose)
                    {
                        if (op.Kind == "OBJECT_ROTATE_STATE") op.Rotate = pose;
                        else op.Translate = pose;
                    }
                    break;
                case "TRANSLATE_TO":
                    op.Translate ??= ReadVec(value);
                    break;
                case "ROTATE_TO":
                    op.Rotate ??= ReadVec(value);
                    break;
            }
        }
        return op.TargetPath.Count > 0 ? op : null;
    }

    private static Vector3? ReadVec(List<object?> value) =>
        value.Count >= 3 && value[0] is float x && value[1] is float y && value[2] is float z
            ? new Vector3(x, y, z) : null;

    // Alternating key/value walk that PRESERVES duplicate keys (ZrdrDict collapses them,
    // which loses repeated SEQUENCE_DEFINITION / op entries). A string followed by a list
    // is a keyed value; followed by null, an explicit bare flag; otherwise a bare flag.
    private static IEnumerable<(string Key, List<object?>? Value)> Pairs(List<object?> list)
    {
        int i = 0;
        while (i < list.Count)
        {
            if (list[i] is string key)
            {
                if (i + 1 < list.Count && list[i + 1] is List<object?> value)
                {
                    yield return (key, value);
                    i += 2;
                }
                else
                {
                    yield return (key, null);
                    i += i + 1 < list.Count && list[i + 1] == null ? 2 : 1;
                }
            }
            else
            {
                i += 1; // stray value — tolerate
            }
        }
    }

    private static string? FirstString(List<object?>? value) =>
        value is { Count: > 0 } && value[0] is string s ? s : null;
}
