using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace CSVM.Session;

/// <summary>The parsed <c>CSVM/data/effect_pools.json</c> — how many copies of each world-effects
/// template the stage builds, per effect template ROOT, scaled by the session's player count
/// (`BL-225`; the number itself is `BL-231` in the TUNE list). Hand-authored engine config, not
/// extracted data: the original instances a fresh copy per CALL_ANIMATION, so every value here is a
/// finite approximation of "unbounded" and belongs in a file the user can edit rather than in a
/// <c>const</c>.
///
/// <para>Pure data — it answers "how many copies of this root" and nothing else.
/// <see cref="WorldEffectsFactory"/> builds the slots and <see cref="Mech3.AnimRuntime"/> hands one
/// out per call.</para>
///
/// <para>A missing or unreadable file is a warning, not a session failure: the built-in
/// <see cref="Fallback"/> (the same values the shipped file carries) applies, exactly as
/// <c>Config</c>'s <c>const</c> defaults do. A root named here that is not a stage root is reported
/// — a typo would otherwise silently size nothing.</para></summary>
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
        },
    };

    private readonly Dictionary<string, Entry> _roots = new(StringComparer.OrdinalIgnoreCase);

    private Entry _default = new(4, 1);

    /// <summary>The committed config's default location (res://), independent of
    /// <c>--data-root</c>: it is engine config, not extracted game data.</summary>
    public static string DefaultPath => ProjectSettings.GlobalizePath("res://data/effect_pools.json");

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
        if (!File.Exists(path))
        {
            GD.PushWarning($"effect pools: file not found, using built-in defaults: {path}");
            return Fallback;
        }
        try
        {
            return Parse(File.ReadAllBytes(path));
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
