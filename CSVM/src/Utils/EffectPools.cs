using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace CSVM.Utils;

/// <summary>The parsed <c>CSVM/data/effect_pools.json</c> — how many copies of each world-effects
/// template the stage builds, per effect template root, scaled by the session's player count.
/// Hand-authored engine config, not extracted data: the original instances a fresh copy per
/// CALL_ANIMATION, so every value here is a finite approximation the user can edit without a
/// rebuild. Three sections and the crash-rig sizing: this module's entry in docs/architecture.md.
/// ⚠ A missing or unreadable file warns and falls back to <see cref="Fallback"/>, the same policy
/// <c>Config</c>'s defaults use. Keep the fallback values in step with the shipped file.
/// </summary>
public sealed class EffectPools
{
    /// <summary>The built-in defaults, applied when the file is absent or unreadable — the shipped
    /// file's own values, so a deleted file changes nothing but the log line.</summary>
    public static readonly EffectPools Fallback = new()
    {
        MaxSlots = 16,
        _default = new Entry(4, 1),
        _roots =
        {
            ["gunhit"] = new Entry(1, 0),
            ["dum_gunhit"] = new Entry(1, 0),
            ["mag_gunhit"] = new Entry(1, 0),
            ["partial_damage_obj"] = new Entry(8, 1),
            ["fire_here"] = new Entry(16, 0),
        },
        _localCallDefault = new Entry(1, 0),
        _localCallRoots =
        {
            ["facdsticks"] = new Entry(6, 0),
        },
        _crashDefault = new Entry(1, 0),
        _crashRoots =
        {
            ["planeflakes"] = new Entry(8, 0),
            ["planeflakes2"] = new Entry(2, 0),
            ["short_firetrail"] = new Entry(6, 0),
            ["large_firetrail"] = new Entry(6, 0),
            ["small_injure_fireball"] = new Entry(3, 0),
            ["flame_ball_02"] = new Entry(2, 0),
            ["yellow_spark_02"] = new Entry(4, 0),
        },
    };

    private readonly Dictionary<string, Entry> _roots = new(StringComparer.OrdinalIgnoreCase);

    // The separate, smaller pool for library-root call templates — see the file's own
    // "_localCallAbout" for why this is not just another entry in _roots (that map is validated
    // against WorldEffectsFactory.EffectStageRoots, and these names never are one).
    private readonly Dictionary<string, Entry> _localCallRoots = new(StringComparer.OrdinalIgnoreCase);

    // The crash rig's own pool — per-airframe damage/crash templates staged under the
    // rig's crash root, kept apart from _roots (validated against the WORLD stage's derived root
    // set) and from _localCallRoots (library-root clones). No per-player term: the whole rig is
    // already built once per player.
    private readonly Dictionary<string, Entry> _crashRoots = new(StringComparer.OrdinalIgnoreCase);

    private Entry _default = new(4, 1);

    private Entry _localCallDefault = new(1, 0);

    private Entry _crashDefault = new(1, 0);

    /// <summary>The committed config's default location (res://), independent of
    /// <c>--data-root</c>: it is engine config, not extracted game data. Kept as a res:// path and
    /// read through <see cref="Godot.FileAccess"/>, because in an exported build the file lives in
    /// the pck, where <c>GlobalizePath</c> + System.IO cannot reach it.</summary>
    public static string DefaultPath => "res://data/effect_pools.json";

    /// <summary>The hard ceiling on any root's slot count — the memory guard, since each slot is one
    /// more copy of that root's subtree. Raise it before raising a per-player term for a
    /// many-player session, not after.</summary>
    public int MaxSlots { get; private set; } = 16;

    /// <summary>The size used for any root the file does not name.</summary>
    public Entry Default => _default;

    /// <summary>The per-root overrides, as authored.</summary>
    public IReadOnlyDictionary<string, Entry> Roots => _roots;

    /// <summary>Loads the pool sizes (defaults to <see cref="DefaultPath"/>). Never throws: a
    /// missing or malformed file warns and yields <see cref="Fallback"/>, because a pool size
    /// getting a session no further than the launch screen would be a worse failure than a
    /// mis-sized pool.</summary>
    public static EffectPools Load(string? path = null)
    {
        path ??= DefaultPath;
        // res:// lives inside the pck in an exported build, where only Godot's own FileAccess
        // can read it; an explicit disk path (unit tests, tools) stays on System.IO, which the
        // xunit host can run without a Godot runtime.
        bool viaGodot = path.StartsWith("res://", StringComparison.Ordinal);
        if (viaGodot ? !Godot.FileAccess.FileExists(path) : !File.Exists(path))
        {
            GD.PushWarning($"effect pools: file not found, using built-in defaults: {path}");
            return Fallback;
        }
        try
        {
            return Parse(viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"effect pools: {path} could not be read ({ex.Message}) — using built-in defaults");
            return Fallback;
        }
    }

    /// <summary>Parses the file's bytes — the whole decision, with no file IO and no engine calls,
    /// so the sizes are testable without a session. Throws <see cref="JsonException"/> on bytes that
    /// are not JSON; a well-formed file missing a key keeps that key's built-in default, since a
    /// partial file is a legitimate way to override one root.</summary>
    public static EffectPools Parse(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var pools = new EffectPools();
        if (root.ValueKind != JsonValueKind.Object)
            return pools;
        if (root.TryGetProperty("maxSlots", out var max) && max.ValueKind == JsonValueKind.Number)
            pools.MaxSlots = Math.Max(1, max.GetInt32());
        if (root.TryGetProperty("default", out var def) && ReadEntry(def) is { } d)
            pools._default = d;
        if (root.TryGetProperty("roots", out var roots) && roots.ValueKind == JsonValueKind.Object)
        {
            foreach (var r in roots.EnumerateObject())
                if (ReadEntry(r.Value) is { } e)
                    pools._roots[r.Name] = e;
        }
        if (root.TryGetProperty("localCallDefault", out var lcDef) && ReadEntry(lcDef) is { } lcd)
            pools._localCallDefault = lcd;
        if (root.TryGetProperty("localCallRoots", out var lcRoots) && lcRoots.ValueKind == JsonValueKind.Object)
        {
            foreach (var r in lcRoots.EnumerateObject())
                if (ReadEntry(r.Value) is { } e)
                    pools._localCallRoots[r.Name] = e;
        }
        if (root.TryGetProperty("crashDefault", out var crDef) && ReadEntry(crDef) is { } crd)
            pools._crashDefault = crd;
        if (root.TryGetProperty("crashRoots", out var crRoots) && crRoots.ValueKind == JsonValueKind.Object)
        {
            foreach (var r in crRoots.EnumerateObject())
                if (ReadEntry(r.Value) is { } e)
                    pools._crashRoots[r.Name] = e;
        }
        return pools;
    }

    /// <summary>How many copies of <paramref name="rootName"/> to stage for a
    /// <paramref name="players"/>-player session: <c>base + perExtraPlayer × (players − 1)</c>,
    /// clamped to 1..<see cref="MaxSlots"/>. Splitscreen and multiplayer are what the per-player
    /// term is for — every extra aircraft is another gun and another rocket landing somewhere
    /// else, so a fixed size collapses back onto one copy as the session grows.</summary>
    public int SlotsFor(string rootName, int players)
    {
        var e = _roots.TryGetValue(rootName, out var hit) ? hit : _default;
        int extra = Math.Max(0, players - 1);
        return Math.Clamp(e.Base + (e.PerExtraPlayer * extra), 1, MaxSlots);
    }

    /// <summary>How many copies of a death-triggered library-root call template to keep — the
    /// same clamp as <see cref="SlotsFor"/> but against <c>localCallRoots</c>/<c>localCallDefault</c>,
    /// never <c>roots</c>/<c>default</c>.
    /// ⚠ Do not widen <c>genx12</c> here even though it has no entry: its own <c>Targets</c>
    /// rescue depends on staying unbuilt.</summary>
    public int LocalCallPoolSize(string rootName)
    {
        var e = _localCallRoots.TryGetValue(rootName, out var hit) ? hit : _localCallDefault;
        return Math.Clamp(e.Base, 1, MaxSlots);
    }

    /// <summary>How many copies of a crash-rig template root the per-player crash stage builds
    /// — against <c>crashRoots</c>/<c>crashDefault</c>, never <c>roots</c>/<c>default</c>
    /// (those are the WORLD stage's, validated against its derived root set). No player scaling:
    /// the whole crash rig is already one per player. A root with no entry stays single-copy —
    /// right for the crash choreography templates, which play once per crash; the per-panel
    /// damage-stage family is sized to its authored distinct call anchors.</summary>
    public int CrashSlotsFor(string rootName)
    {
        var e = _crashRoots.TryGetValue(rootName, out var hit) ? hit : _crashDefault;
        return Math.Clamp(e.Base, 1, MaxSlots);
    }

    /// <summary>The crash stage's pool depth — <see cref="DepthFor"/> against the crash
    /// sizes.</summary>
    public int CrashDepthFor(IEnumerable<string> stageRoots)
    {
        int depth = 1;
        foreach (var r in stageRoots)
            depth = Math.Max(depth, CrashSlotsFor(r));
        return depth;
    }

    /// <summary>Names any authored <c>crashRoots</c> entry that is not one of
    /// <paramref name="stageRoots"/> — <see cref="UnknownRoots"/> for the crash section.</summary>
    public List<string> UnknownCrashRoots(IEnumerable<string> stageRoots)
    {
        var known = new HashSet<string>(stageRoots, StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        foreach (var name in _crashRoots.Keys)
            if (!known.Contains(name))
                unknown.Add(name);
        return unknown;
    }

    /// <summary>The pool DEPTH for a set of stage roots — the largest per-root count, i.e. how many
    /// slot containers the stage needs. Roots sized below the depth simply have no copy in the
    /// deeper slots, and a call landing there falls back to one that exists.</summary>
    public int DepthFor(IEnumerable<string> stageRoots, int players)
    {
        int depth = 1;
        foreach (var r in stageRoots)
            depth = Math.Max(depth, SlotsFor(r, players));
        return depth;
    }

    /// <summary>Names any authored root that is not in <paramref name="stageRoots"/> — a typo sizes
    /// nothing and would otherwise be invisible. Returns an empty list when the file is clean.</summary>
    public List<string> UnknownRoots(IEnumerable<string> stageRoots)
    {
        var known = new HashSet<string>(stageRoots, StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        foreach (var name in _roots.Keys)
            if (!known.Contains(name))
                unknown.Add(name);
        return unknown;
    }

    private static Entry? ReadEntry(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("base", out var b)
            || b.ValueKind != JsonValueKind.Number)
        {
            return null;
        }
        int perPlayer = e.TryGetProperty("perExtraPlayer", out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : 0;
        return new Entry(b.GetInt32(), perPlayer);
    }

    /// <summary>One root's authored size: its single-player <paramref name="Base"/> and how much
    /// each additional player adds. (The file's <c>why</c> prose is documentation for the reader,
    /// not data — it is deliberately not parsed.)</summary>
    public readonly record struct Entry(int Base, int PerExtraPlayer);
}
