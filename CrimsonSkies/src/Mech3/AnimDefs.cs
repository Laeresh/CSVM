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
        // The compiled archives always set anim_name (verified: never null in this install,
        // even where it just repeats NAME — see the waterfall01 dump in
        // docs/formats/anim-definitions.md). Mirroring that here is what lets a reader-only
        // def COLLIDE with its compiled counterpart in AnimProgram's (Name, AnimName) dedupe
        // key instead of instantiating a second, independently-running copy alongside it —
        // the bug that silently killed the C1 waterfall's splash puffers every frame (a
        // reader duplicate whose PUFFER_STATE events parsed as OFF — see below — kept
        // tearing down the compiled instance's puffers moments after they spawned).
        def.AnimName ??= def.Name;
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
            case "PufferState":
                AddPufferState(data, fields);
                break;
            case "LightState":
                AddLightState(data, fields);
                break;
            case "LightAnimation":
                if (RangeObj(fields, "RANGE") is { } lightDelta) data["range"] = lightDelta;
                if (ColorObj(fields, "COLOR") is { } colorDelta) data["color"] = colorDelta;
                if (Num(fields, "RUN_TIME") is { } lightRun) data["run_time"] = lightRun;
                break;
            case "If":
            case "Elseif":
                if (ReaderCondition(fields) is { } condition)
                    data["condition"] = condition;
                break;
        }
        // Everything a normalizer didn't claim stays reachable verbatim, so adding a handler
        // later never needs this front-end changed.
        data["raw"] = body;
        return new AnimEvent { Kind = kind, Data = new AnimData(data) };
    }

    /// <summary>
    /// Normalizes a reader PUFFER_STATE body into the same field shape
    /// <see cref="Effects.PufferState.FromAnimEvent"/> reads from the compiled archives —
    /// the two forms agree field-for-field except ACTIVE_STATE's token spelling and
    /// AT_NODE's optional trailing offset. Without this, a reader-only puffer event carried
    /// none of its own fields (no case existed here at all): <c>active_state</c> came back
    /// null, which <c>HandlePufferState</c>'s <c>?? 0f</c> default reads as "stop" — the C1
    /// waterfall bug, where a reader-scope duplicate of the (correctly compiled) waterfall
    /// def re-asserted its puffers as OFF every frame.
    /// </summary>
    private static void AddPufferState(Dictionary<string, object?> data, Dictionary<string, List<object?>?> fields)
    {
        data["active_state"] = string.Equals(First(fields, "ACTIVE_STATE") as string, "ACTIVE",
            StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
        // AT_NODE is [nodeName, dx?, dy?, dz?] — the trailing offset is what the compiled
        // shape carries separately as "translate" (verified against splashpuffer2/3, whose
        // reader AT_NODE ["waterfall01", 11, 8, -8] matches the compiled translate exactly).
        if (fields.TryGetValue("AT_NODE", out var atNode) && atNode is { Count: > 0 } && atNode[0] is string atName)
        {
            data["at_node"] = atName;
            if (atNode.Count >= 4 && AnimData.AsNum(atNode[1]) is { } ox
                && AnimData.AsNum(atNode[2]) is { } oy && AnimData.AsNum(atNode[3]) is { } oz)
                data["translate"] = Obj3(ox, oy, oz);
        }
        if (Num(fields, "TIME_INTERVAL") is { } ti)
            data["interval_garbage"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                { ["interval_value"] = ti };
        if (Vec(fields, "LOCAL_VELOCITY") is { } lv) data["local_velocity"] = lv;
        if (Vec(fields, "WORLD_VELOCITY") is { } wv) data["world_velocity"] = wv;
        if (Vec(fields, "MIN_RANDOM_VELOCITY") is { } minv) data["min_random_velocity"] = minv;
        if (Vec(fields, "MAX_RANDOM_VELOCITY") is { } maxv) data["max_random_velocity"] = maxv;
        if (Vec(fields, "WORLD_ACCELERATION") is { } wa) data["world_acceleration"] = wa;
        if (Num(fields, "FRICTION") is { } fr) data["friction"] = fr;
        if (Num(fields, "DEVIATION_DISTANCE") is { } dd) data["deviation_distance"] = dd;
        if (Num(fields, "NUMBER") is { } num) data["number"] = num;
        if (RangeObj(fields, "SIZE_RANGE") is { } sr) data["size_range"] = sr;
        if (RangeObj(fields, "LIFETIME_RANGE") is { } lr) data["lifetime_range"] = lr;
        // GROWTH_FACTOR is one scalar in the reader; FromAnimEvent reads a one-entry
        // growth_factors list the same way it reads the compiled shape's rare single-entry case.
        if (Num(fields, "GROWTH_FACTOR") is { } gf)
            data["growth_factors"] = new List<object?>
                { new Dictionary<string, object?>(StringComparer.Ordinal) { ["min"] = 0f, ["max"] = gf } };

        if (fields.TryGetValue("TEXTURES", out var texs) && texs != null)
        {
            var list = new List<object?>();
            foreach (var t in texs)
                if (t is string texName)
                    list.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = texName });
            data["textures"] = list;
        }
        if (fields.TryGetValue("TEXTURE_SEQUENCE", out var seqRaw) && seqRaw != null)
        {
            // Entries are [time, texName] pairs (PufferState.Parse's reader-form reading).
            var seq = data.TryGetValue("textures", out var existing) && existing is List<object?> l ? l : new List<object?>();
            foreach (var item in seqRaw)
                if (item is List<object?> { Count: >= 2 } pair && AnimData.AsNum(pair[0]) is { } t && pair[1] is string tex)
                    seq.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = tex, ["run_time"] = t });
            data["textures"] = seq;
        }
        if (fields.TryGetValue("COLORS", out var cols) && cols != null)
        {
            var list = new List<object?>();
            foreach (var item in cols)
                if (item is List<object?> { Count: >= 5 } c && AnimData.AsNum(c[0]) is { } frac
                    && AnimData.AsNum(c[1]) is { } r && AnimData.AsNum(c[2]) is { } g
                    && AnimData.AsNum(c[3]) is { } b && AnimData.AsNum(c[4]) is { } a)
                    list.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["unk00"] = frac,
                        ["color"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["r"] = r, ["g"] = g, ["b"] = b },
                        ["unk16"] = a,
                    });
            data["colors"] = list;
        }
    }

    /// <summary>
    /// Normalizes a reader LIGHT_STATE body into the compiled shape
    /// <see cref="AnimRuntime"/>'s handler reads. 66 of this install's reader files carry
    /// LIGHT_STATE, so skipping this front-end would repeat the PUFFER_STATE bug in a subtler
    /// form: a reader-only fire would define a light with no range or colour.
    ///
    /// The one semantic that must survive the trip is **partiality** — a flicker event is
    /// <c>["NAME", […], "RANGE", […]]</c> and nothing else, and it must not reset the light's
    /// position, colour or active state. So each field is written only when the reader body
    /// actually carries it, and ACTIVE_STATE's key is left absent rather than defaulted (the
    /// handler tests <c>Has</c>, not the value).
    /// </summary>
    private static void AddLightState(Dictionary<string, object?> data, Dictionary<string, List<object?>?> fields)
    {
        if (First(fields, "ACTIVE_STATE") is string active)
            data["active_state"] = string.Equals(active, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        // AT_NODE is [nodeName, dx?, dy?, dz?] as it is for a puffer, but LIGHT_STATE's compiled
        // shape nests it as translate:{AtNode:{name,pos}} rather than a flat at_node + translate.
        if (fields.TryGetValue("AT_NODE", out var atNode) && atNode is { Count: > 0 } && atNode[0] is string atName)
        {
            var pos = atNode.Count >= 4 && AnimData.AsNum(atNode[1]) is { } ox
                && AnimData.AsNum(atNode[2]) is { } oy && AnimData.AsNum(atNode[3]) is { } oz
                ? Obj3(ox, oy, oz)
                : Obj3(0f, 0f, 0f);
            data["translate"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["AtNode"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    { ["name"] = atName, ["pos"] = pos },
            };
        }
        if (RangeObj(fields, "RANGE") is { } range) data["range"] = range;
        if (ColorObj(fields, "COLOR") is { } color) data["color"] = color;
    }

    // {r,g,b} from a 3-element reader COLOR, matching the compiled shape.
    private static Dictionary<string, object?>? ColorObj(Dictionary<string, List<object?>?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: >= 3 })
            return null;
        if (AnimData.AsNum(v[0]) is not { } r || AnimData.AsNum(v[1]) is not { } g
            || AnimData.AsNum(v[2]) is not { } b)
            return null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["r"] = r, ["g"] = g, ["b"] = b };
    }

    /// <summary>
    /// A reader IF/ELSEIF body → the compiled <c>condition</c> payload
    /// <see cref="AnimRuntime"/> evaluates: a one-key union, e.g.
    /// <c>{"RandomWeight": 0.15}</c>. The reader spells the same ten conditions with its own
    /// vocabulary, and two of them change units on the way through the compiler — this is
    /// where that is undone so the runtime has exactly one convention:
    /// <c>PLAYER_RANGE</c> is metres in the reader and metres SQUARED compiled (reader 270 ↔
    /// compiled 72900, measured across the install), and <c>ANIMATION_LOD</c> is the token
    /// <c>HIGH</c> in the reader and the number 2 compiled. <c>NODE_NEAR_GROUND</c> is the
    /// reader's name for the condition upstream calls <c>NodeUndercover</c>.
    /// </summary>
    private static Dictionary<string, object?>? ReaderCondition(Dictionary<string, List<object?>?> fields)
    {
        Dictionary<string, object?> Union(string tag, object? value) =>
            new(StringComparer.Ordinal) { [tag] = value };

        if (fields.ContainsKey("RANDOM_WEIGHT"))
            return Union("RandomWeight", Num(fields, "RANDOM_WEIGHT") ?? 0f);
        if (fields.ContainsKey("ANIM_HEALTH"))
            return Union("AnimHealth", Num(fields, "ANIM_HEALTH") ?? 0f);
        if (fields.ContainsKey("PLAYER_RANGE"))
        {
            float r = Num(fields, "PLAYER_RANGE") ?? 0f;
            return Union("PlayerRange", r * r);
        }
        if (fields.ContainsKey("ANIMATION_LOD"))
            return Union("AnimationLod", (float)LodLevel(First(fields, "ANIMATION_LOD") as string));
        if (fields.ContainsKey("PLAYER_1ST_PERSON"))
            return Union("PlayerFirstPerson", false);
        if (fields.ContainsKey("HW_RENDER"))
            return Union("HwRender", false);
        if (fields.ContainsKey("NODE_ACTIVE"))
            return Union("NodeActive", First(fields, "NODE_ACTIVE"));
        if (fields.TryGetValue("NODE_NEAR_GROUND", out var nng) && nng is { Count: >= 2 })
            return Union("NodeUndercover", new Dictionary<string, object?>(StringComparer.Ordinal)
                { ["node"] = nng[0], ["distance"] = AnimData.AsNum(nng[1]) ?? 0f });
        if (fields.TryGetValue("NODE_BELOW_ALT", out var nba) && nba is { Count: >= 2 })
            return Union("NodeBelowAlt", new Dictionary<string, object?>(StringComparer.Ordinal)
                { ["node"] = nba[0], ["altitude"] = AnimData.AsNum(nba[1]) ?? 0f });
        return null;
    }

    // The reader's LOD tokens. Only HIGH ships in this install; an unrecognised token is
    // treated as HIGH so a decode gap never silently disables content.
    private static int LodLevel(string? token) => token?.ToUpperInvariant() switch
    {
        "LOW" => 0,
        "MED" or "MEDIUM" => 1,
        _ => AnimRuntime.HighLod,
    };

    private static Dictionary<string, object?> Obj3(float x, float y, float z) =>
        new(StringComparer.Ordinal) { ["x"] = x, ["y"] = y, ["z"] = z };

    // {min,max} from a 2-element reader field (SIZE_RANGE, LIFETIME_RANGE), matching the
    // compiled shape's {"min":…, "max":…} object.
    private static Dictionary<string, object?>? RangeObj(Dictionary<string, List<object?>?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: >= 2 })
            return null;
        if (AnimData.AsNum(v[0]) is not { } min || AnimData.AsNum(v[1]) is not { } max)
            return null;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["min"] = min, ["max"] = max };
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
