using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The zrdr front-end of the animation system: reads reader ANIMATION_DEFINITIONS and normalizes
/// them into the same <see cref="AnimDefinition"/> model <see cref="AnimArchive"/> produces, so
/// <see cref="AnimRuntime"/> executes one shape. Decode and reader/compiled unit table: docs/
/// formats/anim-definitions.md.
/// ⚠ Both sources are needed; neither subsumes the other. A mission's compiled archive carries
/// only the defs it lists, and <c>zepstate</c>/<c>startanims</c> are never compiled at all.
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

    /// <summary>Loads one named reader file's ANIMATION_DEFINITIONs, for a consumer that wants a
    /// specific family without paying for the whole archive (the parked viewer's panel-pairing
    /// derivation reads two files where <see cref="LoadArchive"/> parses hundreds). Missing file
    /// → empty list, same policy as the archive load.</summary>
    public static List<AnimDefinition> LoadFileDefs(string zrdrPath, string fileName)
    {
        var defs = new List<AnimDefinition>();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(zrdrPath, fileName);
        }
        catch (System.IO.FileNotFoundException)
        {
            return defs;
        }
        foreach (var defList in Walk(root, "ANIMATION_DEFINITIONS", "ANIMATION_LIST", "ANIMATION_DEFINITION"))
        {
            var def = ParseDef(defList);
            def.SourceFile = fileName;
            defs.Add(def);
        }
        return defs;
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
                // The multi-target form: alternating (anim-name pattern, anchor node path)
                // pairs, the zeppelin sub-part wiring/destructible defs. Name stays empty;
                // NameResolver.Anchors resolves the recorded paths instead (M4 F18).
                case "NAME1":
                    if (value != null)
                        for (int i = 0; i + 1 < value.Count; i++)
                            if (value[i] is string pattern && value[i + 1] is List<object?> pathList)
                            {
                                var path = new List<string>();
                                foreach (var seg in pathList)
                                    if (seg is string s)
                                        path.Add(s);
                                if (path.Count > 0)
                                    def.MultiTargets.Add((pattern, path));
                            }
                    break;
                // Two forms on one key, parsed into the fields the compiled form fills: OPTIONS
                // [MINIMUM_TO_SATISFY n, ANIMATION_LIST names] (the hull-death gate) and REQUIRED
                // or OPTIONS carrying OBJECT_ACTIVE_LIST / OBJECT_INACTIVE_LIST node paths.
                case "ACTIVATION_PREREQUISITE":
                    if (value != null)
                        foreach (var (optKey, optValue) in Pairs(value))
                        {
                            bool required = optKey.Equals("REQUIRED", StringComparison.OrdinalIgnoreCase);
                            if (optValue == null
                                || (!required && !optKey.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)))
                                continue;
                            foreach (var (k, v) in Pairs(optValue))
                            {
                                if (k.Equals("MINIMUM_TO_SATISFY", StringComparison.OrdinalIgnoreCase))
                                    def.PrereqMinToSatisfy = (int)(FirstNumber(v) ?? 0f);
                                else if (k.Equals("ANIMATION_LIST", StringComparison.OrdinalIgnoreCase)
                                    && v != null)
                                {
                                    foreach (var entry in v)
                                        if (entry is string anim)
                                            def.PrereqAnims.Add(anim);
                                }
                                else if (v != null
                                    && (k.Equals("OBJECT_ACTIVE_LIST", StringComparison.OrdinalIgnoreCase)
                                        || k.Equals("OBJECT_INACTIVE_LIST", StringComparison.OrdinalIgnoreCase)))
                                {
                                    foreach (var listed in v)
                                        if (listed is List<object?> pathList)
                                        {
                                            var path = new List<string>();
                                            foreach (var seg in pathList)
                                                if (seg is string s)
                                                    path.Add(s);
                                            if (path.Count > 0)
                                                def.PrereqNodes.Add(new AnimNodePrereq(path,
                                                    k.Equals("OBJECT_ACTIVE_LIST", StringComparison.OrdinalIgnoreCase),
                                                    required));
                                        }
                                }
                            }
                        }
                    break;
                case "ANIMATION_NAME": def.AnimName = FirstString(value); break;
                case "ANIMATION_ROOT_NAME": def.RootName = FirstString(value); break;
                // Reader activation values are ON_STARTUP / ON_CALL; the compiled archives
                // spell the same thing OnStartup / OnCall. Normalize to the compiled form.
                case "ACTIVATION": def.Activation = PascalCase(FirstString(value) ?? "ON_CALL"); break;
                case "LOCAL_NODES_ONLY": def.LocalNodesOnly = true; break;
                // The cross-mission half of the state log (BL-243). SAVE_LOG is not carried:
                // the compiled form keeps it and nothing reads it, while PERSIST_LOG survives
                // only here (see AnimDefinition.PersistLog).
                case "PERSIST_LOG":
                    def.PersistLog = "ON".Equals(FirstString(value), StringComparison.OrdinalIgnoreCase);
                    break;
                case "HEALTH": def.Health = FirstNumber(value) ?? 0f; break;
                // One argument, metres; the compiled form stores metres SQUARED with min 0,
                // the same reader↔compiled unit divergence as the PLAYER_RANGE condition,
                // converted once here so the runtime has a single convention.
                case "EXECUTION_BY_RANGE":
                    if (FirstNumber(value) is { } range)
                    {
                        def.ByRange = true;
                        def.RangeMin = 0f;
                        def.RangeMax = range * range;
                    }
                    break;
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
                // ⚠ Mirror the compiled DAMAGE_SEQUENCE name so AnimRuntime.ApplyDamageStages finds
                // it; without this a reader-only destructible's damage stages are silently dropped.
                case "DAMAGE_SEQUENCE":
                    if (value != null)
                    {
                        var damage = new AnimSequence { Name = "DAMAGE_SEQUENCE" };
                        damage.Events.AddRange(ParseEvents(value));
                        def.Sequences.Add(damage);
                    }
                    break;
            }
        }
        // ⚠ Mirror anim_name so a reader-only def collides with its compiled twin in AnimProgram's
        // dedupe key instead of double-running; a NAME1 def keys on its first pattern instead,
        // since it has no NAME/ANIMATION_NAME of its own.
        if (def.AnimName == null && def.MultiTargets.Count > 0)
            def.AnimName = def.MultiTargets[0].AnimName;
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

    // One reader op → one normalized event. The kind is the mechanical
    // SNAKE_CASE→PascalCase conversion; the payload is normalized per kind for the fields
    // handlers read, and always keeps the raw body under "raw".
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
            case "ObjectScaleState":
                if (Vec(fields, "STATE") is { } pose)
                    data["state"] = pose;
                AddAtNode(data, fields, "AT_NODE", degToRad: false);
                break;
            case "ObjectRotateState":
                // Reader rotations are degrees; compiled is radians (docs/formats/
                // anim-definitions.md). Unconverted, C2's roadblock spun ~21 turns.
                if (Vec(fields, "STATE", degToRad: true) is { } rotPose)
                    data["state"] = rotPose;
                AddAtNode(data, fields, "AT_NODE_MATRIX", degToRad: true);
                break;
            case "ObjectMotionFromTo":
                if (Num(fields, "RUN_TIME") is { } rt)
                    data["run_time"] = rt;
                AddFromTo(data, fields, "translate", "TRANSLATE_FROM", "TRANSLATE_TO");
                // ROTATE_FROM/TO are degrees in the reader (survey: 1,388 of 1,428 nonzero
                // values exceed 2π, max 900), the C2 roadblock swerve spun cars ~9 turns
                // when 135° reached FromToMotion as 135 rad.
                AddFromTo(data, fields, "rotate", "ROTATE_FROM", "ROTATE_TO", degToRad: true);
                AddFromTo(data, fields, "scale", "SCALE_FROM", "SCALE_TO");
                break;
            case "ObjectMotion":
                // XYZ_ROTATION: degrees/s in the reader, radians/s compiled (docs/formats/
                // anim-definitions.md). Inert today but kept: a missing case here fails silently,
                // the same shape that kills C1's waterfall via PUFFER_STATE.
                if (Spin(fields, "XYZ_ROTATION") is { } spin)
                    data["xyz_rotation"] = spin;
                if (Num(fields, "RUN_TIME") is { } motionRun)
                    data["run_time"] = motionRun;
                break;
            case "ObjectOpacityState":
                // STATE is a token + value in either order; scan by type, default value to 1.
                // Load-bearing: C1's cloudparent cloud deck is reader-only (docs/formats/
                // anim-definitions.md).
                if (fields.TryGetValue("STATE", out var op) && op is { Count: > 0 })
                {
                    float value = 1f;
                    bool? on = null;
                    foreach (var item in op)
                    {
                        if (item is string tok)
                            on = tok.Equals("ON", StringComparison.OrdinalIgnoreCase);
                        else if (AnimData.AsNum(item) is { } f)
                            value = f;
                    }
                    if (on is { } state)
                    {
                        data["state"] = state;
                        data["opacity"] = value;
                    }
                }
                break;
            case "Callback":
                // The reader writes CALLBACK [VALUE [n]] where the compiled event carries `value`
                // directly. Without this a reader-authored destroy def raises a code the runtime
                // cannot read, and the wreck silently inherits nothing.
                if (Num(fields, "VALUE") is { } callbackCode)
                    data["value"] = callbackCode;
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
            case "CallAnimation":
                AddCallTarget(data, fields);
                break;
            case "CallSequence":
                // ⚠ Deliberately not honoured: CALL_SEQUENCE's bare WAIT_FOR_COMPLETION has no
                // compiled counterpart to decode from (docs/formats/anim-definitions.md).
                break;
            case "ObjectAddChild":
            case "ObjectDeleteChild":
                // The reader writes one PARENT_CHILD pair where the compiled event has separate
                // `parent`/`child` fields. Without this the sound-emitter attachment, the
                // reader's third event in the SOUND_NODE triple, carries neither name.
                if (fields.TryGetValue("PARENT_CHILD", out var pair) && pair is { Count: >= 2 })
                {
                    if (pair[0] is string parent) data["parent"] = parent;
                    if (pair[1] is string child) data["child"] = child;
                }
                break;
            case "SoundNode":
                // ⚠ No active_state default: SOUND_NODE only declares the emitter, ACTIVE follows
                // as the next event (docs/formats/anim-definitions.md's waterfall bug).
                break;
            case "Sound":
                // NAME is the sound (sounds.json or SOUND_GROUPS), not a gamez node; AT_NODE
                // positions it. Flattened to at_node+translate, the shape HandleSound reads
                // (docs/formats/anim-definitions.md).
                if (fields.TryGetValue("AT_NODE", out var soundAt) && soundAt is { Count: > 0 }
                    && soundAt[0] is string soundAtName)
                {
                    data["at_node"] = soundAtName;
                    if (soundAt.Count >= 4 && AnimData.AsNum(soundAt[1]) is { } sx
                        && AnimData.AsNum(soundAt[2]) is { } sy && AnimData.AsNum(soundAt[3]) is { } sz)
                    {
                        data["translate"] = Obj3(sx, sy, sz);
                    }
                }
                break;
        }
        // Everything a normalizer didn't claim stays reachable verbatim, so adding a handler
        // later never needs this front-end changed.
        data["raw"] = body;
        return new AnimEvent
        {
            Kind = kind,
            Data = new AnimData(data),
            // The reader's WAIT_FOR_COMPLETION is a bare token; presence is the whole state
            // (docs/formats/anim-definitions.md).
            WaitsForCompletion = kind == "CallAnimation" && fields.ContainsKey("WAIT_FOR_COMPLETION"),
        };
    }

    // Normalizes a reader PUFFER_STATE body into the compiled shape FromAnimEvent reads.
    // ⚠ Without this, a reader-only puffer event carries none of its own fields, which
    // HandlePufferState's default reads as "stop", the C1 waterfall bug (docs/formats/
    // anim-definitions.md).
    private static void AddPufferState(Dictionary<string, object?> data, Dictionary<string, List<object?>?> fields)
    {
        data["active_state"] = string.Equals(First(fields, "ACTIVE_STATE") as string, "ACTIVE",
            StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
        // AT_NODE is [nodeName, dx?, dy?, dz?], the trailing offset is what the compiled
        // shape carries separately as "translate" (verified against splashpuffer2/3, whose
        // reader AT_NODE ["waterfall01", 11, 8, -8] matches the compiled translate exactly).
        if (fields.TryGetValue("AT_NODE", out var atNode) && atNode is { Count: > 0 } && atNode[0] is string atName)
        {
            data["at_node"] = atName;
            if (atNode.Count >= 4 && AnimData.AsNum(atNode[1]) is { } ox
                && AnimData.AsNum(atNode[2]) is { } oy && AnimData.AsNum(atNode[3]) is { } oz)
                data["translate"] = Obj3(ox, oy, oz);
        }
        // The compiled shape carries ONE interval, tagged Time or Distance, so both reader
        // spellings have to arrive here. A trail puffer that loses its metres on the way reads as a
        // state authoring no interval at all, which is the constructor's 1 s and not a trail.
        if (Num(fields, "TIME_INTERVAL") is { } ti)
            data["interval_garbage"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["interval_value"] = ti };
        else if (Num(fields, "DISTANCE_INTERVAL") is { } di)
            data["interval_garbage"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["interval_value"] = di, ["interval_type"] = "Distance" };
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
        if (RangeObj(fields, "START_AGE_RANGE") is { } sa) data["start_age_range"] = sa;
        if (Num(fields, "WIND_FACTOR") is { } wf) data["wind_factor"] = wf;
        // ⚠ A key this normalizer misses is silently lost on a reader-scope puffer event's way to
        // FromAnimEvent. NEAR_FADE→unk_range, FADE_RANGE/FAR_FADE→fade_range (docs/org/puffer.md).
        if (RangeObj(fields, "NEAR_FADE") is { } nf) data["unk_range"] = nf;
        if ((RangeObj(fields, "FADE_RANGE") ?? RangeObj(fields, "FAR_FADE")) is { } ff)
            data["fade_range"] = ff;
        // GROWTH_FACTOR is the reader's two-stop spelling of compiled `growth_factors`
        // ((age_i, scale_i) pairs, docs/formats/anim-definitions.md): mirror (0,1),(1,G).
        if (Num(fields, "GROWTH_FACTOR") is { } gf)
            data["growth_factors"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["min"] = 0f, ["max"] = 1f },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["min"] = 1f, ["max"] = gf },
            };

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

    // Normalizes a reader LIGHT_STATE body into the compiled shape AnimRuntime reads.
    // ⚠ Must preserve partiality: a flicker event names only RANGE, so write each field only
    // when the body carries it, and leave ACTIVE_STATE absent rather than defaulted
    // (docs/formats/anim-definitions.md).
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

    // A reader IF/ELSEIF body -> the compiled `condition` payload's one-key union.
    // PLAYER_RANGE (m) and ANIMATION_LOD (HIGH) change units on the way through the compiler;
    // undone here so the runtime has one convention (docs/formats/anim-definitions.md).
    // NODE_NEAR_GROUND is the reader's name for NodeUndercover.
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

    // CALL_ANIMATION's optional target node, normalized to the compiled `parameters` union
    // (docs/formats/anim-definitions.md). One authored template can then serve many call sites.
    // ⚠ Must not touch data["node"]/["name"]: those already hold the callee's own name.
    private static void AddCallTarget(Dictionary<string, object?> data,
        Dictionary<string, List<object?>?> fields)
    {
        foreach (var (readerKey, tag) in new[] { ("WITH_NODE", "WithNode"), ("AT_NODE", "AtNode") })
        {
            if (First(fields, readerKey) is not string node)
                continue;
            data["parameters"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [tag] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["node"] = node },
            };
            return;
        }
        if (First(fields, "OPERAND_NODE") is string operand)
            data["operand_node"] = operand;
    }

    private static void AddFromTo(Dictionary<string, object?> data,
        Dictionary<string, List<object?>?> fields, string channel, string fromKey, string toKey,
        bool degToRad = false)
    {
        var from = Vec(fields, fromKey, degToRad);
        var to = Vec(fields, toKey, degToRad);
        if (from == null && to == null)
            return;
        var ch = new Dictionary<string, object?>(StringComparer.Ordinal);
        // A missing FROM means "from wherever the object currently is", left absent so the
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

    // AT_NODE / AT_NODE_MATRIX: the pose is taken from another node's frame, with STATE read as an
    // offset inside it rather than as an absolute pose. Emitted in the COMPILED shape (a flat
    // `at_node` for the translation, a `basis.AtNodeMatrix` for the rotation), so both front-ends
    // hand the handlers one thing. Trailing numbers are the offset, in the channel's own units.
    private static void AddAtNode(Dictionary<string, object?> data,
        Dictionary<string, List<object?>?> fields, string key, bool degToRad)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: > 0 } || v[0] is not string host)
            return;
        if (degToRad)
            data["basis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["AtNodeMatrix"] = host };
        else
            data["at_node"] = host;
        if (v.Count >= 4 && AnimData.AsNum(v[1]) is { } x && AnimData.AsNum(v[2]) is { } y
            && AnimData.AsNum(v[3]) is { } z)
            data["state"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x"] = degToRad ? Mathf.DegToRad(x) : x,
                ["y"] = degToRad ? Mathf.DegToRad(y) : y,
                ["z"] = degToRad ? Mathf.DegToRad(z) : z,
            };
    }

    // A vec3 in the compiled payload shape, so both front-ends hand handlers the same thing.
    private static Dictionary<string, object?>? Vec(Dictionary<string, List<object?>?> fields, string key,
        bool degToRad = false)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: >= 3 })
            return null;
        if (AnimData.AsNum(v[0]) is not { } x || AnimData.AsNum(v[1]) is not { } y
            || AnimData.AsNum(v[2]) is not { } z)
            return null;
        if (degToRad)
        {
            x = Mathf.DegToRad(x);
            y = Mathf.DegToRad(y);
            z = Mathf.DegToRad(z);
        }
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        { ["x"] = x, ["y"] = y, ["z"] = z };
    }

    // OBJECT_MOTION's XYZ_ROTATION: six numbers, [initial xyz, delta xyz], degrees/second in
    // the reader against radians/second compiled. Rebuilt into the compiled form's nested
    // {initial:{x,y,z}, delta:{x,y,z}} so AnimRuntime reads one shape from both front-ends.
    private static Dictionary<string, object?>? Spin(
        Dictionary<string, List<object?>?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v) || v is not { Count: >= 6 })
            return null;
        var n = new float[6];
        for (int i = 0; i < 6; i++)
        {
            if (AnimData.AsNum(v[i]) is not { } f)
                return null;
            n[i] = Mathf.DegToRad(f);
        }
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["initial"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["x"] = n[0], ["y"] = n[1], ["z"] = n[2] },
            ["delta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            { ["x"] = n[3], ["y"] = n[4], ["z"] = n[5] },
        };
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
                i += 1; // stray value, tolerate
            }
        }
    }

    private static string? FirstString(List<object?>? value) =>
        value is { Count: > 0 } && value[0] is string s ? s : null;

    private static float? FirstNumber(List<object?>? value) =>
        value is { Count: > 0 } ? AnimData.AsNum(value[0]) : null;
}
