using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Mech3;

/// <summary>
/// The original's UI string table, read by id from <c>extracted/rof/ui_strings.json</c>. Only the
/// <c>langui</c> rows are kept: ids are not unique across the file's two merged tables (9-35 exist
/// in both) and every menu range this remake reads, docs/formats/strings.md.
/// Placeholders are Win32 <c>FormatMessage</c> positional specifiers (<c>%1!d!</c>), converted to
/// composite format by <see cref="ToCompositeFormat"/> rather than handed to printf. A leading
/// <c>[FONTID]</c> tag the extractor left in a multi-line row is stripped, being a renderer
/// directive and not text. Engine-free: <see cref="Parse"/> takes the JSON itself, so the table
/// and its formatting test off engine.
/// </summary>
public sealed class UiStrings
{
    /// <summary>The one `dll` value whose rows are kept.</summary>
    public const string Table = "langui";

    private readonly Dictionary<int, string> _byId;

    private UiStrings(Dictionary<int, string> byId) => _byId = byId;

    /// <summary>A table with no strings in it. Every lookup falls back, so a menu built on a
    /// missing extraction still draws.</summary>
    public static UiStrings Empty { get; } = new(new Dictionary<int, string>());

    /// <summary>Reads the table under <paramref name="dataRoot"/>, or null when the file is
    /// absent or unreadable. The caller decides what a missing table means; the menus fall back to
    /// <see cref="Empty"/> rather than refusing to open.</summary>
    public static UiStrings? TryLoad(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "extracted", "rof", "ui_strings.json");
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The langui rows of an extracted table. The first row for an id wins, so a
    /// duplicate later in the file cannot silently replace an earlier reading.</summary>
    public static UiStrings Parse(string json)
    {
        var byId = new Dictionary<int, string>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object
                || !row.TryGetProperty("dll", out var dll)
                || dll.ValueKind != JsonValueKind.String
                || dll.GetString() != Table
                || !row.TryGetProperty("id", out var id)
                || !id.TryGetInt32(out int key)
                || !row.TryGetProperty("text", out var text)
                || text.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!byId.ContainsKey(key))
            {
                byId[key] = StripFontTag(text.GetString() ?? string.Empty);
            }
        }

        return new UiStrings(byId);
    }

    /// <summary>One <c>FormatMessage</c> string as a .NET composite format: <c>%1!d!</c> becomes
    /// <c>{0}</c>, <c>%1!02d!</c> becomes <c>{0:00}</c>, <c>%%</c> becomes a literal percent, and
    /// braces already in the text are escaped so they survive as themselves.</summary>
    public static string ToCompositeFormat(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{' || c == '}')
            {
                sb.Append(c).Append(c);
                continue;
            }

            if (c != '%')
            {
                sb.Append(c);
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '%')
            {
                sb.Append('%');
                i++;
                continue;
            }

            if (TryReadSpecifier(text, i, out string composite, out int next))
            {
                sb.Append(composite);
                i = next - 1;
                continue;
            }

            sb.Append('%');
        }

        return sb.ToString();
    }

    /// <summary>Whether the table carries this id at all.</summary>
    public bool Has(int id) => _byId.ContainsKey(id);

    /// <summary>The raw text of a string id, or <paramref name="fallback"/> when the table has no
    /// such row. Placeholders are left as the original wrote them; use <see cref="Format"/> to
    /// fill them.</summary>
    public string Text(int id, string fallback = "") =>
        _byId.TryGetValue(id, out var text) ? text : fallback;

    /// <summary>A string id with its positional placeholders filled. An id the table lacks, or a
    /// row whose specifiers do not match the arguments given, yields the empty string rather than
    /// throwing: a menu label is never worth a crash.</summary>
    public string Format(int id, params object[] args)
    {
        if (!_byId.TryGetValue(id, out var text))
        {
            return string.Empty;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, ToCompositeFormat(text), args);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    // One `%<n>!<spec>!` run starting at `start`, as a composite-format item. Anything that is not
    // that exact shape is left to the caller to emit literally.
    private static bool TryReadSpecifier(string text, int start, out string composite, out int next)
    {
        composite = string.Empty;
        next = start;
        int i = start + 1;
        int digits = i;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == digits || i >= text.Length || text[i] != '!'
            || !int.TryParse(text[digits..i], NumberStyles.None, CultureInfo.InvariantCulture, out int position)
            || position < 1)
        {
            return false;
        }

        int spec = i + 1;
        int end = text.IndexOf('!', spec);
        if (end < 0)
        {
            return false;
        }

        composite = "{" + (position - 1) + PaddingOf(text[spec..end]) + "}";
        next = end + 1;
        return true;
    }

    // The zero-padded integer specifiers (`02d`) carry a width the remake must keep; every other
    // spec (`d`, `s`, `u`, `x`) renders the argument as it stands.
    private static string PaddingOf(string spec) =>
        spec.Length > 2 && spec[0] == '0' && spec[^1] == 'd'
        && int.TryParse(spec[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
        && width > 0
            ? ":" + new string('0', spec.Length - 1)
            : string.Empty;

    // A row the extractor could not lift the `[FONTID]` off (a multi-line one) still leads with the
    // tag; it selects a font and is not text, so it never reaches a label.
    private static string StripFontTag(string text)
    {
        if (text.Length < 3 || text[0] != '[')
        {
            return text;
        }

        int close = text.IndexOf(']');
        if (close < 2)
        {
            return text;
        }

        foreach (char c in text[1..close])
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return text;
            }
        }

        return text[(close + 1)..];
    }
}
