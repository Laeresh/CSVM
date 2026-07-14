using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Reader for mech3ax zrdr extractions (the game's "reader" config files → JSON).
/// Reader data is nested lists of strings and numbers. By convention most lists
/// alternate key/value pairs: [key, [values...], key, [values...], ...] — wrap those
/// in <see cref="ZrdrDict"/>. vehicle.json defs additionally inherit via 'kind_of'.
/// </summary>
public static class Zrdr
{
    /// <summary>Loads one reader file (e.g. "vehicle.json") from a zrdr ZIP or a directory of JSON files.</summary>
    public static List<object?> LoadFile(string zrdrPath, string fileName)
    {
        byte[] bytes;
        if (Directory.Exists(zrdrPath))
        {
            bytes = File.ReadAllBytes(Path.Combine(zrdrPath, fileName));
        }
        else
        {
            using var zip = ZipFile.OpenRead(zrdrPath);
            var entry = zip.GetEntry(fileName)
                ?? throw new FileNotFoundException($"'{fileName}' missing from '{zrdrPath}'");
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            bytes = ms.ToArray();
        }
        using var doc = JsonDocument.Parse(bytes);
        return Convert(doc.RootElement) as List<object?>
            ?? throw new InvalidDataException($"'{fileName}' is not a reader list");
    }

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
                i += 1; // stray value — tolerate
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
