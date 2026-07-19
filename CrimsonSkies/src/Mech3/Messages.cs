using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CrimsonSkies.Mech3;

/// <summary>
/// The game's localized string table (mech3ax extracts the message resource to a plain
/// JSON object: <c>{ "language_id": 1033, "entries": [{ "key", "id", "value" }, …] }</c>).
/// Reader files reference display text indirectly by <c>MSG_*</c> key — targets.json's
/// objective descriptions, briefing lines, UI labels — and this resolves them.
/// Unlike the zrdr archives this is a single top-level file (default
/// <c>extracted/messages.json</c>), so it is not a <see cref="Zrdr"/> list.
/// </summary>
public sealed class Messages
{
    private readonly Dictionary<string, string> _byKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads the message table from a messages.json file. Missing file → an empty
    /// table (every lookup then falls back to the raw key), never throws — display strings
    /// are cosmetic and a mission must still load without them.</summary>
    public static Messages Load(string path)
    {
        var msgs = new Messages();
        if (!File.Exists(path))
            return msgs;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        if (doc.RootElement.TryGetProperty("entries", out var entries)
            && entries.ValueKind == JsonValueKind.Array)
            foreach (var e in entries.EnumerateArray())
                if (e.TryGetProperty("key", out var k) && k.GetString() is { } key
                    && e.TryGetProperty("value", out var v) && v.GetString() is { } value)
                    msgs._byKey[key] = value;
        return msgs;
    }

    public int Count => _byKey.Count;

    /// <summary>The display string for a <c>MSG_*</c> key, or the key itself if it is unknown
    /// (so an unresolved reference is visible in the HUD/log rather than blank). A null or
    /// empty key returns "".</summary>
    public string Get(string? key) =>
        string.IsNullOrEmpty(key) ? ""
        : _byKey.TryGetValue(key, out var v) ? v
        : key;
}
