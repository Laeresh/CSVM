using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSVM.Effects;

/// <summary>
/// The shipped per-chapter cloud shadow tunes, <c>CSVM/data/cloud_shadows.json</c>: one object per
/// chapter holding <see cref="CloudShadowPattern.Tune"/> fields under <see cref="Keys"/>. A missing
/// chapter or field takes the constants in <see cref="CloudShadowPattern"/>. An unknown field or a
/// value that is not a number is reported, never fatal. Keys beginning with <c>_</c> are prose. The
/// cloud lab writes one chapter's entry through <see cref="WithEntry"/>, keeping every other key.
/// </summary>
public sealed class CloudShadowChapters
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/cloud_shadows.json";

    /// <summary>An entry's field names, in the order the lab writes them.</summary>
    public static readonly IReadOnlyList<string> Keys = new[] { "darkness", "ambient", "size", "coverage", "softness", "speed", "heading" };

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, CloudShadowPattern.Tune> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    /// <summary>What the file got wrong, one line each.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Reads the file, logging each warning. A missing file draws every chapter at the constants.</summary>
    public static CloudShadowChapters Load()
    {
        var chapters = Godot.FileAccess.FileExists(DefaultPath)
            ? Parse(Encoding.UTF8.GetString(Godot.FileAccess.GetFileAsBytes(DefaultPath)))
            : new CloudShadowChapters();
        foreach (var w in chapters.Warnings)
            Utils.Log.Warn("world", $"cloud shadows: {w}");
        return chapters;
    }

    /// <summary>Parses the file's text with no file IO and no engine call, so the rules unit-test.</summary>
    public static CloudShadowChapters Parse(string json)
    {
        var chapters = new CloudShadowChapters();
        JsonNode? root;
        try
        {
            root = Mech3.VegetationSidecar.ParseWhole(json);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            chapters._warnings.Add($"not readable JSON ({ex.Message}), every chapter takes the defaults");
            return chapters;
        }
        if (root is not JsonObject entries)
        {
            chapters._warnings.Add("the file is not a JSON object, every chapter takes the defaults");
            return chapters;
        }
        foreach (var (key, node) in entries)
        {
            if (key.StartsWith('_'))
                continue;
            if (node is not JsonObject fields)
            {
                chapters._warnings.Add($"{key} is not an object, it takes the defaults");
                continue;
            }
            chapters._entries[key] = ReadEntry(key, fields, chapters._warnings);
        }
        return chapters;
    }

    /// <summary>The file's text with <paramref name="chapter"/>'s entry replaced by every field of
    /// <paramref name="tune"/>. Every other key keeps its place and value; a new chapter is appended.
    /// Throws <see cref="InvalidDataException"/> when <paramref name="existing"/> is not a JSON object,
    /// so a Save never replaces a file it cannot read.</summary>
    public static string WithEntry(string? existing, string chapter, CloudShadowPattern.Tune tune)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existing))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = Mech3.VegetationSidecar.ParseWhole(existing) as JsonObject
                    ?? throw new InvalidDataException("the cloud shadows file is not a JSON object, so Save leaves it as it is");
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                throw new InvalidDataException($"the cloud shadows file is not readable JSON ({ex.Message}), so Save leaves it as it is", ex);
            }
        }

        string name = chapter.ToUpperInvariant();
        var entry = new JsonObject();
        foreach (var k in Keys)
        {
            if (Get(tune.Clamped(), k) is { } v)
                entry[k] = JsonValue.Create(Math.Round((double)v, 3));
        }

        // Rebuilt key by key, since a JsonObject cannot replace a member in its place.
        var rebuilt = new JsonObject();
        bool placed = false;
        foreach (var (key, node) in root)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                if (!placed)
                    rebuilt[name] = entry;
                placed = true;
                continue;
            }
            rebuilt[key] = node?.DeepClone();
        }
        if (!placed)
            rebuilt[name] = entry;
        return rebuilt.ToJsonString(Written) + Environment.NewLine;
    }

    /// <summary>Writes <paramref name="chapter"/>'s entry into the file at <paramref name="diskPath"/>,
    /// creating it when absent.</summary>
    public static void Save(string diskPath, string chapter, CloudShadowPattern.Tune tune)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        File.WriteAllText(diskPath, WithEntry(existing, chapter, tune), new UTF8Encoding(false));
    }

    /// <summary>The project's copy of the file while the game runs from the source tree. Null in an
    /// exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>The chapter's saved tunes, or the constants when the file has no entry for it.</summary>
    public CloudShadowPattern.Tune For(string chapter) =>
        _entries.TryGetValue(chapter, out var tune) ? tune : CloudShadowPattern.Tune.Default;

    private static float? Get(CloudShadowPattern.Tune t, string key) => key switch
    {
        "darkness" => t.Darkness,
        "ambient" => t.Ambient,
        "size" => t.PatternSize,
        "coverage" => t.Coverage,
        "speed" => t.DriftSpeed,
        "heading" => t.Heading,
        _ => t.Softness,
    };

    private static CloudShadowPattern.Tune? With(CloudShadowPattern.Tune t, string key, float v) => key switch
    {
        "darkness" => t with { Darkness = v },
        "ambient" => t with { Ambient = v },
        "size" => t with { PatternSize = v },
        "coverage" => t with { Coverage = v },
        "softness" => t with { Softness = v },
        "speed" => t with { DriftSpeed = v },
        "heading" => t with { Heading = v },
        _ => null,
    };

    private static CloudShadowPattern.Tune ReadEntry(string chapter, JsonObject fields, List<string> warnings)
    {
        var tune = CloudShadowPattern.Tune.Default;
        foreach (var (key, node) in fields)
        {
            if (key.StartsWith('_'))
                continue;
            if (node is not JsonValue value || !value.TryGetValue(out double number) || !double.IsFinite(number))
            {
                warnings.Add($"{chapter}: '{key}' is not a number, it takes the default");
                continue;
            }
            if (With(tune, key, (float)number) is not { } next)
            {
                warnings.Add($"{chapter}: '{key}' is not a cloud shadow field ({string.Join("/", Keys)}), ignored");
                continue;
            }
            tune = next;
        }
        var clamped = tune.Clamped();
        if (clamped != tune)
            warnings.Add($"{chapter}: a field out of range is held inside it");
        return clamped;
    }
}
