using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Mech3;

/// <summary>
/// The game's localized string table (mech3ax extracts the message resource to a plain
/// JSON object: <c>{ "language_id": 1033, "entries": [{ "key", "id", "value" }, …] }</c>).
/// Reader files reference display text indirectly by <c>MSG_*</c> key, targets.json's
/// objective descriptions, briefing lines, UI labels, and this resolves them.
/// Unlike the zrdr archives this is a single top-level file (default
/// <c>extracted/messages.json</c>), so it is not a <see cref="Zrdr"/> list.
/// </summary>
public sealed class Messages
{
    private readonly Dictionary<string, string> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byKey.Count;

    /// <summary>Loads the message table from a messages.json file. Missing file → an empty
    /// table (every lookup then falls back to the raw key), never throws, display strings
    /// are cosmetic and a mission must still load without them.</summary>
    public static Messages Load(string path) =>
        File.Exists(path) ? Read(JsonDocument.Parse(File.ReadAllBytes(path))) : new Messages();

    /// <summary>Substitutes a message template's positional placeholders. <c>%1</c>…<c>%9</c> take
    /// <paramref name="args"/> in order (a missing arg renders empty); a bang-delimited type spec
    /// that follows a placeholder, e.g. the <c>!d!</c> in <c>%2!d!</c>, is consumed (the arg is
    /// already a formatted string); and <c>%%</c> is a literal percent. The template is raw text, so
    /// resolve it through <see cref="Get"/> first (keeps an unresolved key visible on load rather than
    /// per draw).</summary>
    public static string Fill(string template, params string?[] args)
    {
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }
        var sb = new StringBuilder(template.Length + 8);
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '%' && i + 1 < template.Length)
            {
                char n = template[i + 1];
                if (n == '%')
                {
                    sb.Append('%');
                    i++;
                    continue;
                }
                if (n >= '1' && n <= '9')
                {
                    int idx = n - '1';
                    sb.Append(idx < args.Length ? args[idx] ?? "" : "");
                    i++;
                    // consume an optional !spec! type marker (e.g. !d!) that trails the placeholder
                    if (i + 1 < template.Length && template[i + 1] == '!')
                    {
                        int close = template.IndexOf('!', i + 2);
                        i = close > 0 ? close : i + 1;
                    }
                    continue;
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>The display string for a <c>MSG_*</c> key, or the key itself if it is unknown
    /// (so an unresolved reference is visible in the HUD/log rather than blank). A null or
    /// empty key returns "".</summary>
    public string Get(string? key) =>
        string.IsNullOrEmpty(key) ? ""
        : _byKey.TryGetValue(key, out var v) ? v
        : key;

    /// <summary>Resolves <paramref name="key"/> and fills its placeholders in one call,
    /// <c>Fill(Get(key), args)</c>.</summary>
    public string Format(string? key, params string?[] args) => Fill(Get(key), args);

    /// <summary>The same table out of JSON already in hand, for a caller with no file to point at.
    /// Internal rather than public because every shipped caller holds a path, and only the unit
    /// project builds a table out of a literal.</summary>
    internal static Messages Parse(string json) => Read(JsonDocument.Parse(json));

    // The one reader both entry points share. Takes the document over so a malformed file leaks
    // nothing on the way back out.
    private static Messages Read(JsonDocument doc)
    {
        var msgs = new Messages();
        using (doc)
        {
            if (doc.RootElement.TryGetProperty("entries", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
                foreach (var e in entries.EnumerateArray())
                    if (e.TryGetProperty("key", out var k) && k.GetString() is { } key
                        && e.TryGetProperty("value", out var v) && v.GetString() is { } value)
                        msgs._byKey[key] = value;
        }
        return msgs;
    }
}
