using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Reader for the mech3ax fork's compiled-anim extraction (<c>unzbd cs anim</c> on a
/// <c>cam_anim.zbd</c>/<c>mis_anim.zbd</c> → a zip of per-def JSON + per-script
/// <c>*.zan.json</c> + <c>metadata.json</c>). Accepts a zip or an unpacked directory, like
/// every other loader in this project.
///
/// The fork's output is a *decoded AST*, not a key/value soup: each def carries typed
/// fields and typed events whose node references are already resolved back to names. So
/// this reader deliberately keeps event payloads as a generic property bag
/// (<see cref="AnimData"/>) instead of 35 hand-written DTOs — a new event type costs the
/// reader nothing, and <see cref="AnimRuntime"/> asks each payload only for the fields its
/// handler needs. See docs/formats/anim-definitions.md.
///
/// Scripts are loaded lazily: a chapter archive holds ~50 and a mission's defs reference a
/// handful, so parsing all of them (megabytes of frame JSON) at world build would be waste.
/// </summary>
/// <summary>A per-axis cubic <c>value + c1·t + c2·t² + c3·t³</c>, stored in the archive as a
/// 16-byte little-endian float block (base64 in the JSON layer).</summary>
public readonly struct SiCubic
{
    public readonly float Value, C1, C2, C3;

    public SiCubic(float value, float c1, float c2, float c3)
    {
        Value = value; C1 = c1; C2 = c2; C3 = c3;
    }

    public static SiCubic FromBase64(string? b64)
    {
        if (string.IsNullOrEmpty(b64))
            return default;
        Span<byte> buf = stackalloc byte[16];
        if (!Convert.TryFromBase64String(b64, buf, out int written) || written < 16)
            return default;
        return new SiCubic(
            BitConverter.ToSingle(buf[..4]), BitConverter.ToSingle(buf[4..8]),
            BitConverter.ToSingle(buf[8..12]), BitConverter.ToSingle(buf[12..16]));
    }

    /// <summary>Evaluates at <paramref name="t"/> seconds. Non-finite coefficients occur in
    /// the shipped data (uninitialised spline memory), so a bad result degrades to the
    /// constant term rather than propagating NaN into a transform.</summary>
    public float Eval(float t)
    {
        float v = Value + C1 * t + C2 * t * t + C3 * t * t * t;
        return float.IsFinite(v) ? v : (float.IsFinite(Value) ? Value : 0f);
    }
}

public sealed class AnimArchive
{
    /// <summary>Every definition in the archive, in the container's own def order.</summary>
    public readonly List<AnimDefinition> Defs = new();

    private readonly string _path;
    private readonly bool _isDir;
    private readonly List<string> _scriptNames = new();
    private readonly Dictionary<int, SiScript?> _scriptCache = new();

    private AnimArchive(string path, bool isDir)
    {
        _path = path;
        _isDir = isDir;
    }

    /// <summary>Archive-relative source label used in log lines (e.g. "C1/cam_anim").</summary>
    public string Label { get; private set; } = "";

    /// <summary>Number of SI scripts in this archive's pool (referenced by index from
    /// <see cref="AnimDefinition.SiScriptIds"/>).</summary>
    public int ScriptCount => _scriptNames.Count;

    /// <summary>
    /// Loads an anim extraction. Returns null when the path does not exist — a missing
    /// archive is normal (not every mission folder has a mis_anim, and a user who has not
    /// re-run ExtractAssets.ps1 simply gets no compiled animations), so callers degrade to
    /// the zrdr-only path rather than failing the world build.
    /// </summary>
    public static AnimArchive? Load(string path, string label)
    {
        bool isDir = Directory.Exists(path);
        if (!isDir && !File.Exists(path))
            return null;
        var archive = new AnimArchive(path, isDir) { Label = label };
        try
        {
            archive.ReadAll();
        }
        catch (Exception e)
        {
            // A corrupt/partial extraction must not take the world build down with it.
            GD.Print($"anim archive '{label}': unreadable ({e.GetType().Name}) — skipping");
            return null;
        }
        return archive;
    }

    /// <summary>The SI script at a pool index (from a def's <c>si_script_ids</c>), parsed on
    /// first use and cached. Out-of-range or unreadable → null.</summary>
    public SiScript? Script(int index)
    {
        if (_scriptCache.TryGetValue(index, out var cached))
            return cached;
        SiScript? script = null;
        if (index >= 0 && index < _scriptNames.Count && ReadEntry(_scriptNames[index] + ".json") is { } bytes)
        {
            try
            {
                script = SiScript.Parse(ParseObject(bytes), _scriptNames[index]);
            }
            catch (Exception e)
            {
                GD.Print($"anim archive '{Label}': script #{index} '{_scriptNames[index]}' unreadable ({e.GetType().Name})");
            }
        }
        return _scriptCache[index] = script;
    }

    private static AnimData ParseObject(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        return new AnimData(JsonConvert.ToDictionary(doc.RootElement));
    }

    private void ReadAll()
    {
        // metadata.json names every def and script in container order; si_script_ids index
        // into script_names, and each name is the extraction's file stem.
        var meta = ParseObject(ReadEntry("metadata.json")
            ?? throw new FileNotFoundException($"metadata.json missing from '{_path}'"));
        foreach (var n in meta.Strings("script_names"))
            _scriptNames.Add(n);
        foreach (var defName in meta.Strings("anim_def_names"))
        {
            if (ReadEntry(defName + ".json") is not { } bytes)
                continue; // the zero-def placeholder and any name the extraction skipped
            var def = AnimDefinition.Parse(ParseObject(bytes), defName);
            def.Archive = this;
            Defs.Add(def);
        }
    }

    private byte[]? ReadEntry(string name)
    {
        if (_isDir)
        {
            var p = Path.Combine(_path, name);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }
        // Zip: reopened per read. Only the (rare) zipped-tree case pays this — ExtractAssets
        // -Unzip produces the loose directories the viewer prefers, and defs are read once.
        using var zip = ZipFile.OpenRead(_path);
        if (zip.GetEntry(name) is not { } entry)
            return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>
/// One ANIMATION_DEFINITION — the UNIFIED model both front-ends produce (the compiled
/// archives via <see cref="AnimArchive"/>, the zrdr readers via <see cref="AnimDefs"/>).
/// Field meanings match the reader-source schema
/// documented in docs/formats/anim-definitions.md — this is the same definition the zrdr
/// readers carry, already compiled and resolved.
/// </summary>
public sealed class AnimDefinition
{
    public readonly List<AnimSequence> Sequences = new();

    /// <summary>
    /// This definition's symbol table: the node name each of its events refers to → that
    /// node's flat gamez list index. Built from the def's own <c>objects</c>/<c>nodes</c>
    /// support arrays, whose <c>ptr</c> field IS the gamez node index (verified exactly on
    /// 136,048 references across all 8 chapters — the only apparent exceptions are the
    /// fork's own reversible <c>~N</c> duplicate-name suffixes, which the event names carry
    /// too, so lookups still hit).
    ///
    /// This is what makes compiled definitions bind unambiguously: C1 has both a `caboose`
    /// and a `caboose.flt` in different parts of the world, and name matching drives both.
    /// Reader-sourced defs have no symbol table and keep the wildcard name matching.
    /// </summary>
    public readonly Dictionary<string, int> NodeRefs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>nodes</c> support array's names in container order. Separate from
    /// <see cref="NodeRefs"/> because IF/ELSEIF conditions reference a node by its **1-based
    /// position** in this array rather than by name (mech3ax resolves the index→name mapping
    /// for every other event kind, but leaves condition node indices raw) — see
    /// <c>AnimRuntime.ConditionNode</c>.</summary>
    public readonly List<string> NodeList = new();

    public string Name = "";              // the world node(s) this def anchors to
    public string? AnimName;              // ANIMATION_NAME — what startanims/CALL_ANIMATION use
    public string? RootName;              // ANIMATION_ROOT_NAME — attach node inside each instance
    public bool LocalNodesOnly;
    public string Activation = "OnCall";  // OnCall / OnStartup / WeaponHit / WeaponOrCollideHit
    public float Health;
    /// <summary>EXECUTION_BY_RANGE: the def executes only while the player is within this
    /// distance band of its anchor. Metres SQUARED, the compiled convention (reader 50 ↔
    /// compiled 2500) — same unit divergence as the PLAYER_RANGE condition. Absent → both 0
    /// and <see cref="ByRange"/> false.</summary>
    public float RangeMin, RangeMax;
    public bool ByRange;
    public int[] SiScriptIds = Array.Empty<int>();
    public AnimSequence? ResetState;
    public string SourceFile = "";
    /// <summary>The archive this def came from, and therefore the pool
    /// <see cref="SiScriptIds"/> index into. Null for zrdr-sourced defs (no scripts exist
    /// outside the compiled archives).</summary>
    public AnimArchive? Archive;

    public bool OnStartup => Activation.Equals("OnStartup", StringComparison.OrdinalIgnoreCase);

    /// <summary>Any def carrying HEALTH &gt; 0 is a destructible world object — this is the whole
    /// test (docs/formats/destructibles.md). <see cref="Activation"/> then says what may damage it
    /// (<c>WeaponHit</c> vs <c>WeaponOrCollideHit</c>).</summary>
    public bool Destructible => Health > 0f;

    public static AnimDefinition Parse(AnimData d, string sourceFile)
    {
        var def = new AnimDefinition
        {
            Name = d.Str("name") ?? "",
            AnimName = d.Str("anim_name"),
            RootName = d.Str("anim_root_name"),
            LocalNodesOnly = d.Bool("local_nodes_only"),
            Activation = d.Str("activation") ?? "OnCall",
            Health = d.Num("health") ?? 0f,
            SourceFile = sourceFile,
        };
        // execution is a one-key union: "None" (a bare string) or {ByRange: {min, max}}.
        if (d.Obj("execution")?.Obj("ByRange") is { } byRange)
        {
            def.ByRange = true;
            def.RangeMin = byRange.Num("min") ?? 0f;
            def.RangeMax = byRange.Num("max") ?? 0f;
        }
        if (d.List("si_script_ids") is { } ids)
        {
            var arr = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                arr[i] = (int)(AnimData.AsNum(ids[i]) ?? 0f);
            def.SiScriptIds = arr;
        }
        // The support arrays are the def's symbol table (see NodeRefs). ONLY `nodes` and
        // `objects` carry node indices: measured over C1/C2/C4/C5, their in-range `ptr`s
        // resolve to a matching node name 46,481/46,481 and 31,323/31,323 of the time, while
        // `lights`/`puffers`/`dynamic_sounds` are out of node range in **every** one of their
        // 2,616 entries — those are runtime pointers to engine objects, not node indices.
        // Admitting them here would silently bind (say) a PUFFER_STATE's own name to a bogus
        // index and resolve it to nothing instead of falling through to name matching.
        foreach (var key in new[] { "nodes", "objects" })
            foreach (var r in d.Objects(key))
                if (r.Str("name") is { } refName && r.Num("ptr") is { } ptr
                    && ptr >= 0 && ptr < 0xFFFFFFFu)
                    def.NodeRefs.TryAdd(refName, (int)ptr);
        foreach (var r in d.Objects("nodes"))
            def.NodeList.Add(r.Str("name") ?? "");
        if (d.Obj("reset_state") is { } reset)
            def.ResetState = AnimSequence.Parse(reset);
        foreach (var seq in d.Objects("sequences"))
            def.Sequences.Add(AnimSequence.Parse(seq));
        return def;
    }
}

/// <summary>One SEQUENCE_DEFINITION: a timeline of events. <c>seq_state</c> "OnCall" means
/// the sequence runs only when a CALL_SEQUENCE names it; "Initial" runs with the animation.</summary>
public sealed class AnimSequence
{
    public readonly List<AnimEvent> Events = new();

    public string Name = "";
    public bool OnCallOnly;

    public static AnimSequence Parse(AnimData d)
    {
        var seq = new AnimSequence
        {
            Name = d.Str("name") ?? "",
            OnCallOnly = string.Equals(d.Str("seq_state"), "OnCall", StringComparison.OrdinalIgnoreCase),
        };
        foreach (var e in d.Objects("events"))
            if (AnimEvent.Parse(e) is { } ev)
                seq.Events.Add(ev);
        return seq;
    }
}

/// <summary>
/// One event in a sequence: a kind (the JSON union tag — "ObjectActiveState",
/// "ObjectMotionSiScript", …), an optional schedule, and the raw payload. The payload stays
/// generic on purpose; see the class comment on <see cref="AnimArchive"/>.
/// </summary>
public sealed class AnimEvent
{
    public string Kind = "";
    /// <summary>Schedule origin: "Animation" (since the animation started), "Sequence"
    /// (since this sequence started), "Event" (since the previous event fired), or null =
    /// immediately after the previous event.</summary>
    public string? StartOffset;
    public float StartTime;
    public AnimData Data = AnimData.Empty;

    public static AnimEvent? Parse(AnimData d)
    {
        // data is a single-key union object: {"ObjectActiveState": {...}} — or, for the
        // unit-payload kinds, a bare string.
        if (d.Get("data") is not { } dataValue)
            return null;
        var ev = new AnimEvent();
        switch (dataValue)
        {
            case Dictionary<string, object?> { Count: 1 } union:
                foreach (var (k, v) in union)
                {
                    ev.Kind = k;
                    ev.Data = v is Dictionary<string, object?> payload ? new AnimData(payload)
                        // Some unions carry a scalar payload (Loop: {"Count": -1} nests, but
                        // e.g. an enum-only variant is a bare value) — keep it reachable.
                        : new AnimData(new Dictionary<string, object?> { ["value"] = v });
                }
                break;
            case string kindOnly:
                ev.Kind = kindOnly;
                break;
            default:
                return null;
        }
        if (d.Obj("start") is { } start)
        {
            ev.StartOffset = start.Str("offset");
            ev.StartTime = start.Num("time") ?? 0f;
        }
        return ev;
    }
}

/// <summary>
/// One SI script (a compiled <c>.zan</c>): a list of time-ranged frames, each carrying a
/// base pose plus per-axis cubics. Decoded per docs/formats/anim-definitions.md.
/// </summary>
public sealed class SiScript
{
    public readonly List<SiFrame> Frames = new();

    public string ScriptName = "";
    public string ObjectName = "";

    /// <summary>False on 15 of the install's 1,090 scripts (the C1 `hkzep` zeppelin family,
    /// C1/M04's intro pirate zeppelin + fighter, and three C4 hookup cameras). On those the
    /// spline blocks are UNINITIALISED MEMORY and must never be evaluated; the frame is
    /// `base + delta·dt` instead. Baked into the cubics at parse time — see
    /// <see cref="SiVectorChannel.Parse"/>.</summary>
    public bool SplineInterp = true;

    /// <summary>Total script duration = the last frame's end time (0 when empty).</summary>
    public float Duration => Frames.Count > 0 ? Frames[^1].EndTime : 0f;

    public static SiScript Parse(AnimData d, string sourceName)
    {
        var script = new SiScript
        {
            ScriptName = d.Str("script_name") ?? sourceName,
            ObjectName = d.Str("object_name") ?? "",
            SplineInterp = d.Bool("spline_interp", defaultValue: true),
        };
        foreach (var f in d.Objects("frames"))
            script.Frames.Add(SiFrame.Parse(f, script.SplineInterp));
        return script;
    }

    /// <summary>The frame covering <paramref name="t"/> seconds, or null when the script is
    /// empty. Times past the end clamp to the last frame (callers loop by wrapping t).</summary>
    public SiFrame? FrameAt(float t)
    {
        if (Frames.Count == 0)
            return null;
        // Frames are contiguous and ordered; a linear scan from a hint would be faster, but
        // a binary search keeps callers stateless and the counts are small (≤ ~200).
        int lo = 0, hi = Frames.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Frames[mid].StartTime <= t) lo = mid; else hi = mid - 1;
        }
        return Frames[lo];
    }
}

/// <summary>One script frame: a time span plus optional translate/rotate/scale channels.</summary>
public sealed class SiFrame
{
    public float StartTime, EndTime;
    public SiVectorChannel? Translate;
    public SiRotateChannel? Rotate;
    public SiVectorChannel? Scale;

    public static SiFrame Parse(AnimData d, bool spline)
    {
        var f = new SiFrame
        {
            StartTime = d.Num("start_time") ?? 0f,
            EndTime = d.Num("end_time") ?? 0f,
        };
        if (d.Obj("translate") is { } tr) f.Translate = SiVectorChannel.Parse(tr, spline);
        if (d.Obj("rotate") is { } rot) f.Rotate = SiRotateChannel.Parse(rot, spline);
        if (d.Obj("scale") is { } sc) f.Scale = SiVectorChannel.Parse(sc, spline);
        return f;
    }
}

/// <summary>Translate/scale channel: absolute per-axis cubics whose constant term is the
/// base component, so <c>Value(dt)</c> is the component itself. When the owning script is
/// NOT spline-interpolated the spline blocks are uninitialised memory and must not be read —
/// see <see cref="Parse"/>.</summary>
public sealed class SiVectorChannel
{
    public Vector3 Base;
    public Vector3 Delta;
    public SiCubic X, Y, Z;

    /// <summary>A non-spline frame is exactly a degenerate cubic — <c>base + delta·dt</c> —
    /// so the flag is resolved here and <see cref="At"/> stays branch-free.</summary>
    public static SiVectorChannel Parse(AnimData d, bool spline)
    {
        var b = d.Vec3("base");
        var v = d.Vec3("delta");
        return new SiVectorChannel
        {
            Base = b,
            Delta = v,
            X = spline ? SiCubic.FromBase64(d.Str("spline_x")) : new SiCubic(b.X, v.X, 0f, 0f),
            Y = spline ? SiCubic.FromBase64(d.Str("spline_y")) : new SiCubic(b.Y, v.Y, 0f, 0f),
            Z = spline ? SiCubic.FromBase64(d.Str("spline_z")) : new SiCubic(b.Z, v.Z, 0f, 0f),
        };
    }

    /// <summary>The vector at <paramref name="dt"/> seconds into the frame.</summary>
    public Vector3 At(float dt) => new(X.Eval(dt), Y.Eval(dt), Z.Eval(dt));
}

/// <summary>
/// Rotate channel. Two decode facts, both load-bearing (see docs/formats/anim-definitions.md):
/// the cubics are HALF-ANGLE radians RELATIVE to the frame's base (constant term 0) and
/// compose as <c>q(t) = exp(f(t)) ⊗ base</c>; and the JSON quaternion's field labels are
/// shifted, because mech3ax reads the file's (w,x,y,z) order into a repr(C) {x,y,z,w}
/// struct — so real w = json x, real x = json y, real y = json z, real z = json w.
/// <see cref="Parse"/> undoes that shift, so nothing downstream has to know.
/// </summary>
public sealed class SiRotateChannel
{
    public Quaternion Base;
    public Vector3 Delta;
    public SiCubic X, Y, Z;

    public static SiRotateChannel Parse(AnimData d, bool spline)
    {
        var raw = d.Obj("base");
        // The shift: json (x,y,z,w) holds file (w,x,y,z).
        var q = raw == null
            ? Quaternion.Identity
            : new Quaternion(raw.Num("y") ?? 0f, raw.Num("z") ?? 0f, raw.Num("w") ?? 0f, raw.Num("x") ?? 1f);
        // Uninitialised spline memory exists in the data (pfighter11.zan), and one frame in
        // the install carries NaN deltas; a non-finite or zero-length quaternion would
        // poison every transform downstream, so fall back to identity.
        if (!IsFinite(q) || q.LengthSquared() < 1e-6f)
            q = Quaternion.Identity;
        else
            q = q.Normalized();
        // Non-spline: the half-angle vector is simply `delta·dt` (constant term 0, like every
        // spline rotate cubic), so the same `exp(v) ⊗ base` composition serves both forms.
        var delta = d.Vec3("delta");
        return new SiRotateChannel
        {
            Base = q,
            Delta = delta,
            X = spline ? SiCubic.FromBase64(d.Str("spline_x")) : new SiCubic(0f, delta.X, 0f, 0f),
            Y = spline ? SiCubic.FromBase64(d.Str("spline_y")) : new SiCubic(0f, delta.Y, 0f, 0f),
            Z = spline ? SiCubic.FromBase64(d.Str("spline_z")) : new SiCubic(0f, delta.Z, 0f, 0f),
        };
    }

    /// <summary>The rotation at <paramref name="dt"/> seconds into the frame:
    /// <c>exp(halfAngleVector(dt)) ⊗ base</c>.</summary>
    public Quaternion At(float dt)
    {
        var v = new Vector3(X.Eval(dt), Y.Eval(dt), Z.Eval(dt));
        float len = v.Length();
        if (!float.IsFinite(len) || len < 1e-9f)
            return Base;
        // Quaternion exponential of a pure half-angle vector: (cos|v|, sin|v| · v̂).
        var axis = v / len;
        var exp = new Quaternion(axis.X * Mathf.Sin(len), axis.Y * Mathf.Sin(len), axis.Z * Mathf.Sin(len),
            Mathf.Cos(len));
        return (exp * Base).Normalized();
    }

    private static bool IsFinite(Quaternion q) =>
        float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);
}

/// <summary>
/// A parsed JSON object as a property bag with typed accessors. The compiled-anim payloads
/// are plain objects of scalars/objects/arrays, and the event vocabulary is wide (35 kinds)
/// but shallowly used, so this beats hand-written DTOs per kind.
/// </summary>
public sealed class AnimData
{
    public static readonly AnimData Empty = new(new Dictionary<string, object?>());

    private readonly Dictionary<string, object?> _props;

    public AnimData(Dictionary<string, object?> props) => _props = props;

    public static float? AsNum(object? v) => v switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        _ => null,
    };

    public object? Get(string key) => _props.TryGetValue(key, out var v) ? v : null;
    public bool Has(string key) => _props.ContainsKey(key) && _props[key] != null;

    public string? Str(string key) => Get(key) as string;
    public float? Num(string key) => AsNum(Get(key));
    public bool Bool(string key, bool defaultValue = false) => Get(key) as bool? ?? defaultValue;

    public AnimData? Obj(string key) =>
        Get(key) is Dictionary<string, object?> d ? new AnimData(d) : null;

    public List<object?>? List(string key) => Get(key) as List<object?>;

    /// <summary>Each element of an array-valued property that is itself an object.</summary>
    public IEnumerable<AnimData> Objects(string key)
    {
        if (Get(key) is not List<object?> list)
            yield break;
        foreach (var item in list)
            if (item is Dictionary<string, object?> d)
                yield return new AnimData(d);
    }

    /// <summary>Each string element of an array-valued property.</summary>
    public IEnumerable<string> Strings(string key)
    {
        if (Get(key) is not List<object?> list)
            yield break;
        foreach (var item in list)
            if (item is string s)
                yield return s;
    }

    /// <summary>An {x,y,z} sub-object as a vector; absent → zero.</summary>
    public Vector3 Vec3(string key)
    {
        var d = Obj(key);
        return d == null ? Vector3.Zero
            : new Vector3(d.Num("x") ?? 0f, d.Num("y") ?? 0f, d.Num("z") ?? 0f);
    }

    /// <summary>The single key of a one-key union object (e.g. Loop's {"Count": -1}), or null.</summary>
    public (string Tag, object? Value)? Union()
    {
        if (_props.Count != 1)
            return null;
        foreach (var (k, v) in _props)
            return (k, v);
        return null;
    }
}

/// <summary>JSON → plain object tree (strings / floats / bools / lists / dictionaries), so
/// parsed payloads outlive the JsonDocument they came from.</summary>
internal static class JsonConvert
{
    public static Dictionary<string, object?> ToDictionary(JsonElement e)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (e.ValueKind != JsonValueKind.Object)
            return d;
        foreach (var prop in e.EnumerateObject())
            d[prop.Name] = ToValue(prop.Value);
        return d;
    }

    private static object? ToValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => ToDictionary(e),
        JsonValueKind.Array => ToList(e),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetSingle(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static List<object?> ToList(JsonElement e)
    {
        var list = new List<object?>(e.GetArrayLength());
        foreach (var item in e.EnumerateArray())
            list.Add(ToValue(item));
        return list;
    }
}
