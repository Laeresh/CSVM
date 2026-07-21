using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// The zrdr front-end of the animation system: reads ANIMATION_DEFINITIONS reader files
/// (zepstate.json, hangar3.json, small_building.json, …) and normalizes them into the SAME
/// <see cref="AnimDefinition"/> model the compiled <see cref="AnimArchive"/> produces, so
/// <see cref="AnimRuntime"/> has exactly one thing to execute.
///
/// Both sources are needed and neither subsumes the other (measured 2026-07-21): the
/// compiled cam_anim/mis_anim archives are richer (typed events, resolved node refs, the SI
/// scripts — which exist nowhere else), but a mission's <c>mis_anim.zbd</c> compiles only
/// the defs its <c>mis_anim.json</c> lists. <c>zepstate</c> (the per-mission roster that
/// hides the zeppelins/trains a mission doesn't use) and <c>startanims</c> are never
/// compiled into any archive — they stay reader-only. See docs/formats/anim-definitions.md.
///
/// The normalization is mechanical: a reader op key converted SNAKE_CASE → PascalCase is
/// exactly the compiled event tag (<c>OBJECT_ACTIVE_STATE</c> → <c>ObjectActiveState</c>,
/// <c>OBJECT_MOTION_SI_SCRIPT</c> → <c>ObjectMotionSiScript</c>), verified across the whole
/// event vocabulary. Only the payload *fields* need per-kind attention, and only for the
/// kinds a handler actually reads; everything else keeps its raw body so no data is lost.
/// </summary>
public static class AnimDefs
{
    /// <summary>Loads every ANIMATION_DEFINITION from all reader files in a zrdr zip/dir.
    /// Missing archive → empty list (mission folders without anim readers are normal).</summary>
    public static List<AnimDefinition> LoadArchive(string zrdrPath)
    {
        var defs = new List<AnimDefinition>();
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

    private static AnimDefinition ParseDef(List<object?> list)
    {
        var def = new AnimDefinition();
        foreach (var (key, value) in Pairs(list))
        {
            switch (key.ToUpperInvariant())
            {
                case "NAME": def.Name = FirstString(value) ?? def.Name; break;
                case "ANIMATION_NAME": def.AnimName = FirstString(value); break;
                case "ANIMATION_ROOT_NAME": def.RootName = FirstString(value); break;
                // Reader activation values are ON_STARTUP / ON_CALL; the compiled archives
                // spell the same thing OnStartup / OnCall. Normalize to the compiled form.
                case "ACTIVATION": def.Activation = PascalCase(FirstString(value) ?? "ON_CALL"); break;
                case "LOCAL_NODES_ONLY": def.LocalNodesOnly = true; break;
                case "HEALTH": def.Health = FirstNumber(value) ?? 0f; break;
                case "RESET_STATE":
                    if (value != null)
                    {
                        def.ResetState ??= new AnimSequence();
                        def.ResetState.Events.AddRange(ParseEvents(value));
                    }
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
        var events = new List<AnimEvent>();
        foreach (var (key, value) in Pairs(list))
        {
            if (key.Equals("NAME", StringComparison.OrdinalIgnoreCase))
                seq.Name = FirstString(value) ?? "";
            else if (key.Equals("ACTIVATION", StringComparison.OrdinalIgnoreCase))
                seq.OnCallOnly = string.Equals(FirstString(value), "ON_CALL", StringComparison.OrdinalIgnoreCase);
            else if (ToEvent(key, value) is { } ev)
                events.Add(ev);
        }
        seq.Events.AddRange(events);
        return seq;
    }

    private static IEnumerable<AnimEvent> ParseEvents(List<object?> list)
    {
        foreach (var (key, value) in Pairs(list))
            if (ToEvent(key, value) is { } ev)
                yield return ev;
    }

    /// <summary>One reader op → one normalized event. The kind is the mechanical
    /// SNAKE_CASE→PascalCase conversion; the payload is normalized per kind for the fields
    /// handlers read, and always keeps the raw body under "raw".</summary>
    private static AnimEvent? ToEvent(string key, List<object?>? body)
    {
        var kind = PascalCase(key);
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        var fields = body == null ? new Dictionary<string, List<object?>?>() : FieldMap(body);

        // NAME is a node path in the reader (a multi-entry NAME is parent→child). The
        // compiled events name their target "node" or "name" depending on the type, so set
        // both, plus the full path for the multi-entry case.
        if (fields.TryGetValue("NAME", out var nameList) && nameList != null)
        {
            var path = new List<object?>();
            foreach (var v in nameList)
                if (v is string s)
                    path.Add(s);
            if (path.Count > 0)
            {
                data["node"] = path[0];
                data["name"] = path[0];
                if (path.Count > 1)
                    data["node_path"] = path;
            }
        }

        switch (kind)
        {
            case "ObjectActiveState":
                data["state"] = string.Equals(First(fields, "STATE") as string, "ACTIVE",
                    StringComparison.OrdinalIgnoreCase);
                break;
            case "ObjectTranslateState":
            case "ObjectRotateState":
            case "ObjectScaleState":
                if (Vec(fields, "STATE") is { } pose)
                    data["state"] = pose;
                break;
            case "ObjectMotionFromTo":
                if (Num(fields, "RUN_TIME") is { } rt)
                    data["run_time"] = rt;
                AddFromTo(data, fields, "translate", "TRANSLATE_FROM", "TRANSLATE_TO");
                AddFromTo(data, fields, "rotate", "ROTATE_FROM", "ROTATE_TO");
                AddFromTo(data, fields, "scale", "SCALE_FROM", "SCALE_TO");
                break;
        }
        // Everything a normalizer didn't claim stays reachable verbatim, so adding a handler
        // later never needs this front-end changed.
        data["raw"] = body;
        return new AnimEvent { Kind = kind, Data = new AnimData(data) };
    }

    private static void AddFromTo(Dictionary<string, object?> data,
        Dictionary<string, List<object?>?> fields, string channel, string fromKey, string toKey)
    {
        var from = Vec(fields, fromKey);
        var to = Vec(fields, toKey);
        if (from == null && to == null)
            return;
        var ch = new Dictionary<string, object?>(StringComparer.Ordinal);
        // A missing FROM means "from wherever the object currently is" — left absent so the
        // handler reads the live pose rather than assuming the authored rest pose.
        if (from != null) ch["from"] = from;
        if (to != null) ch["to"] = to;
        data[channel] = ch;
    }

    // Reader op body → key → value list (duplicate keys keep the first, which is how the
    // ops are shaped: one NAME, one STATE, one RUN_TIME).
    private static Dictionary<string, List<object?>?> FieldMap(List<object?> body)
    {
        var map = new Dictionary<string, List<object?>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in Pairs(body))
            if (!map.ContainsKey(k))
                map[k] = v;
        return map;
    }

    private static object? First(Dictionary<string, List<object?>?> fields, string key) =>
        fields.TryGetValue(key, out var v) && v is { Count: > 0 } ? v[0] : null;

    private static float? Num(Dictionary<string, List<object?>?> fields, string key) =>
        AnimData.AsNum(First(fields, key));

    // A vec3 in the compiled payload shape, so both front-ends hand handlers the same thing.
    private static Dictionary<string, object?>? Vec(Dictionary<string, List<object?>?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: >= 3 })
            return null;
        if (AnimData.AsNum(v[0]) is not { } x || AnimData.AsNum(v[1]) is not { } y
            || AnimData.AsNum(v[2]) is not { } z)
            return null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["x"] = x, ["y"] = y, ["z"] = z };
    }

    /// <summary>SNAKE_CASE → PascalCase, the reader↔compiled vocabulary bridge.</summary>
    public static string PascalCase(string snake)
    {
        var sb = new StringBuilder(snake.Length);
        bool upper = true;
        foreach (char c in snake)
        {
            if (c == '_')
            {
                upper = true;
                continue;
            }
            sb.Append(upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            upper = false;
        }
        return sb.ToString();
    }

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

    private static float? FirstNumber(List<object?>? value) =>
        value is { Count: > 0 } ? AnimData.AsNum(value[0]) : null;
}
