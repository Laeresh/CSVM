using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace CSVM.Mech3;

/// <summary>
/// Reader for unzbd's zrdr output (the game's "reader" config files as JSON), the modding path.
/// The engine reads a scope as a <see cref="ZrdrSet"/>; <see cref="ReadScope"/> builds one from a
/// zip or folder. Only the fork's entry names are read (<c>vehicle.zrd.json</c>).
/// Reader data is nested lists of strings and numbers. By convention most lists
/// alternate key/value pairs: [key, [values...], key, [values...], ...], wrap those
/// in <see cref="ZrdrDict"/>. vehicle.json defs additionally inherit via 'kind_of'.
/// </summary>
public static class Zrdr
{
    private const string EntrySuffix = ".zrd.json";

    /// <summary>Every reader of a zrdr zip or folder as a <see cref="ZrdrFile"/>, in entry order,
    /// an empty reader as an empty root; null when neither exists. Throws when an entry does not
    /// parse.</summary>
    public static IReadOnlyList<ZrdrFile>? ReadScope(string zrdrPath)
    {
        if (!Directory.Exists(zrdrPath) && !File.Exists(zrdrPath))
            return null;
        var files = new List<ZrdrFile>();
        foreach (var (name, bytes) in Entries(zrdrPath))
        {
            using var doc = JsonDocument.Parse(bytes);
            files.Add(new ZrdrFile(name[..^".json".Length], null, Convert(doc.RootElement) as List<object?> ?? new List<object?>()));
        }
        return files;
    }

    /// <summary>Loads one reader file (e.g. "vehicle.json") from a zrdr ZIP or a directory of JSON files.</summary>
    public static List<object?> LoadFile(string zrdrPath, string fileName)
    {
        using var doc = JsonDocument.Parse(ReadEntry(zrdrPath, fileName));
        return Convert(doc.RootElement) as List<object?>
            ?? throw new InvalidDataException($"'{fileName}' is not a reader list");
    }

    /// <summary>Loads one reader file, treating an EMPTY reader as an empty list rather than as an
    /// error. mech3ax writes a reader with no entries as the four bytes <c>null</c>, which
    /// <see cref="LoadFile"/> rejects as "not a reader list", indistinguishable there from a
    /// corrupt file. Two shipped `templates.zrd` are in that state (C1C, C2B), and for a caller
    /// that must tell "this chapter authors nothing" apart from "this file is broken", that
    /// difference is the whole point. Still throws when the entry is absent.</summary>
    public static List<object?> LoadFileOrEmpty(string zrdrPath, string fileName)
    {
        using var doc = JsonDocument.Parse(ReadEntry(zrdrPath, fileName));
        if (doc.RootElement.ValueKind == JsonValueKind.Null)
            return new List<object?>();
        return Convert(doc.RootElement) as List<object?>
            ?? throw new InvalidDataException($"'{fileName}' is not a reader list");
    }

    /// <summary>
    /// Enumerates every reader file in a zrdr ZIP or directory whose raw JSON contains
    /// <paramref name="contentFilter"/> (a cheap pre-parse sniff, reader archives hold
    /// hundreds of files and most callers want one family, e.g. "ANIMATION_DEFINITIONS"),
    /// yielding (fileName, parsed root list). Unparseable files are skipped.
    /// </summary>
    public static IEnumerable<(string Name, List<object?> Root)> LoadMatchingFiles(
        string zrdrPath, string contentFilter)
    {
        foreach (var (name, bytes) in Entries(zrdrPath))
        {
            // UTF-8 substring sniff: the filter keys are plain ASCII JSON strings.
            if (System.Text.Encoding.UTF8.GetString(bytes).IndexOf(contentFilter, StringComparison.Ordinal) >= 0
                && TryParse(bytes) is { } root)
                yield return (name, root);
        }
    }

    /// <summary>Enumerates reader files by NAME (dir or zip), the counterpart of
    /// <see cref="LoadMatchingFiles"/> for families keyed by filename rather than content
    /// (the ne0NNNNN patrol nets are pure number arrays with nothing to sniff for). The
    /// predicate sees the stored file name; unparseable matches are skipped.</summary>
    public static IEnumerable<(string Name, List<object?> Root)> LoadFilesNamed(
        string zrdrPath, Func<string, bool> namePredicate)
    {
        foreach (var (name, bytes) in Entries(zrdrPath))
        {
            if (namePredicate(name) && TryParse(bytes) is { } root)
                yield return (name, root);
        }
    }

    /// <summary>Whether a zrdr ZIP or directory carries <paramref name="fileName"/> under the name
    /// <see cref="LoadFile"/> reads. It lets a caller tell an absent entry from a broken one
    /// before reading; false when the archive itself is absent.</summary>
    public static bool HasFile(string zrdrPath, string fileName)
    {
        string entry = EntryName(fileName);
        if (Directory.Exists(zrdrPath))
            return File.Exists(Path.Combine(zrdrPath, entry));
        if (!File.Exists(zrdrPath))
            return false;
        using var zip = ZipFile.OpenRead(zrdrPath);
        return zip.GetEntry(entry) != null;
    }

    // The fork's entries, a reader file per *.zrd.json in a folder or a zip, in stored order.
    private static IEnumerable<(string Name, byte[] Bytes)> Entries(string zrdrPath)
    {
        if (Directory.Exists(zrdrPath))
        {
            foreach (var file in Directory.EnumerateFiles(zrdrPath, "*" + EntrySuffix))
                yield return (Path.GetFileName(file), File.ReadAllBytes(file));
        }
        else if (File.Exists(zrdrPath))
        {
            using var zip = ZipFile.OpenRead(zrdrPath);
            foreach (var entry in zip.Entries)
            {
                if (!entry.Name.EndsWith(EntrySuffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                using var s = entry.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                yield return (entry.Name, ms.ToArray());
            }
        }
    }

    private static List<object?>? TryParse(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            return Convert(doc.RootElement) as List<object?>;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] ReadEntry(string zrdrPath, string fileName)
    {
        string entryName = EntryName(fileName);
        if (Directory.Exists(zrdrPath))
        {
            var p = Path.Combine(zrdrPath, entryName);
            if (File.Exists(p))
                return File.ReadAllBytes(p);
        }
        else
        {
            using var zip = ZipFile.OpenRead(zrdrPath);
            if (zip.GetEntry(entryName) is { } entry)
            {
                using var s = entry.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
        throw new FileNotFoundException($"'{fileName}' missing from '{zrdrPath}'");
    }

    // A request names the reader as the engine spells it (vehicle.json, vehicle.zrd.json); the
    // fork stores it as vehicle.zrd.json.
    private static string EntryName(string fileName) =>
        fileName.EndsWith(EntrySuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : Path.GetFileNameWithoutExtension(fileName) + EntrySuffix;

    private static object? Convert(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => ConvertList(e),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetSingle(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static List<object?> ConvertList(JsonElement e)
    {
        var list = new List<object?>(e.GetArrayLength());
        foreach (var item in e.EnumerateArray())
            list.Add(Convert(item));
        return list;
    }
}

/// <summary>
/// Key→value view over an alternating reader list. A key is a string followed by a
/// list value; a string followed by another string (or the list end) is a bare flag.
/// </summary>
public sealed class ZrdrDict
{
    private readonly Dictionary<string, List<object?>> _props = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every key present, in no particular order (duplicates already collapsed). Lets a
    /// typed reader assert it consumed every key its source file carries, see
    /// <c>Flight.Weapons.WeaponDefs</c>'s unhandled-key check.</summary>
    public IReadOnlyCollection<string> Keys => _props.Keys;

    public static ZrdrDict FromAlternating(List<object?> list)
    {
        var d = new ZrdrDict();
        int i = 0;
        while (i < list.Count)
        {
            if (list[i] is string key)
            {
                if (i + 1 < list.Count && list[i + 1] is List<object?> value)
                {
                    d._props[key] = value;
                    i += 2;
                }
                else
                {
                    d._props[key] = new List<object?>(); // bare flag, e.g. 'is_autogyro'
                    i += 1;
                }
            }
            else
            {
                i += 1; // stray value, tolerate
            }
        }
        return d;
    }

    public bool Has(string key) => _props.ContainsKey(key);

    public List<object?>? List(string key) => _props.TryGetValue(key, out var v) ? v : null;

    public bool TryFloat(string key, out float value, int index = 0)
    {
        value = 0f;
        if (_props.TryGetValue(key, out var v) && index < v.Count && v[index] is float f)
        {
            value = f;
            return true;
        }
        return false;
    }

    public float Float(string key, float fallback = 0f, int index = 0) =>
        TryFloat(key, out var f, index) ? f : fallback;

    public string? Str(string key, int index = 0) =>
        _props.TryGetValue(key, out var v) && index < v.Count ? v[index] as string : null;

    /// <summary>Interprets the value list of <paramref name="key"/> as a nested alternating dict.</summary>
    public ZrdrDict? Dict(string key) =>
        _props.TryGetValue(key, out var v) ? FromAlternating(v) : null;
}
