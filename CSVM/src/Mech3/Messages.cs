using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>
/// The game's localized string table, read from <c>strings.dll</c>. Reader files reference display
/// text indirectly by <c>MSG_*</c> key, targets.json's objective descriptions, briefing lines, UI
/// labels, and this resolves them. A launch loads it once into <c>Spec/GameData.cs</c>, which every
/// consumer reads it from.
/// </summary>
public sealed class Messages
{
    /// <summary>The table's file at the install root.</summary>
    public const string GameFile = "strings.dll";

    // The key table follows 48 bytes of C runtime initializer slots at the head of .data.
    private const int KeyTableOffset = 48;

    private readonly Dictionary<string, string> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byKey.Count;

    /// <summary>Every key and its text, for a tool that lists the table.</summary>
    public IReadOnlyDictionary<string, string> Entries => _byKey;

    /// <summary>Loads the table from the install's <c>strings.dll</c>. No source or no file gives
    /// an empty table, whose lookups fall back to the raw key. Display strings are cosmetic, and a
    /// mission must still load without them. A malformed DLL throws
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

    /// <summary>A table out of key and value pairs, a later pair replacing an earlier one under the
    /// same key. What the synthetic game data and the test oracle build a table with.</summary>
    internal static Messages Of(IEnumerable<(string Key, string Value)> entries)
    {
        var msgs = new Messages();
        foreach (var (key, value) in entries)
        {
            msgs._byKey[key] = value;
        }

        return msgs;
    }

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
}
