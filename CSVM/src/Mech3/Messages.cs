using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using CSVM.Extraction;

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
    /// <summary>The table's file at the install root.</summary>
    public const string GameFile = "strings.dll";

    // The key table follows 48 bytes of C runtime initializer slots at the head of .data.
    private const int KeyTableOffset = 48;

    private readonly Dictionary<string, string> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byKey.Count;

    /// <summary>Loads the message table from a messages.json file. Missing file → an empty
    /// table (every lookup then falls back to the raw key), never throws, display strings
    /// are cosmetic and a mission must still load without them.</summary>
    public static Messages Load(string path) =>
        File.Exists(path) ? Read(JsonDocument.Parse(File.ReadAllBytes(path))) : new Messages();

    /// <summary>Loads the table from the install's <c>strings.dll</c>. No source or no file gives
    /// an empty table, as a missing JSON file does; a malformed DLL throws
    /// <see cref="InvalidDataException"/>.</summary>
    public static Messages Load(IGameFileSource? game) =>
        game != null && game.Exists(GameFile) ? FromStringsDll(game.ReadAllBytes(GameFile)) : new Messages();

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

    /// <summary>The table out of a <c>strings.dll</c>'s bytes. The values are its
    /// <c>STRINGTABLE</c>; the keys are a table of name-pointer and id pairs in its <c>.data</c>
    /// section. Layout: docs/formats/missions.md#message-table.</summary>
    internal static Messages FromStringsDll(byte[] pe)
    {
        var text = PeStringTable.Read(pe);
        var (data, address) = PeStringTable.Section(pe, ".data")
            ?? throw new InvalidDataException("strings.dll has no .data section");
        var msgs = new Messages();

        // The pairs run up to the first name, which the last pair points at.
        int end = data.Length;
        int used = 0;
        for (int o = KeyTableOffset; o + 8 <= end; o += 8)
        {
            long name = (long)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o)) - address;
            int id = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(o + 4));
            int nul = name >= o + 8 && name < data.Length ? Array.IndexOf(data, (byte)0, (int)name) : -1;
            if (nul < 0)
            {
                throw new InvalidDataException("strings.dll key " + id + " points outside its .data section");
            }

            string key = Encoding.ASCII.GetString(data, (int)name, nul - (int)name);
            msgs._byKey[key] = text.TryGetValue(id, out var value) ? value
                : throw new InvalidDataException("strings.dll key " + key + " has no string " + id);
            end = Math.Min(end, (int)name);
            used++;
        }

        // Every string is named by a key, so a shortfall means the key table was misread.
        if (used != text.Count)
        {
            throw new InvalidDataException("strings.dll names " + used + " of its " + text.Count + " strings");
        }

        return msgs;
    }

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
