using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSVM.Effects;

/// <summary>
/// The shipped per-chapter cloud shadow tunes, <c>CSVM/data/cloud_shadows.json</c>: one object per
/// chapter holding <see cref="CloudShadowPattern.Tune"/> fields under <see cref="Keys"/>. A missing
/// chapter or field takes the constants in <see cref="CloudShadowPattern"/>. An unknown field or a
/// value that is not a number is reported, never fatal. Keys beginning with <c>_</c> are prose. An
/// entry's sparse <see cref="CloudsLayer"/> block applies over it while the volumetric clouds stand,
/// so tuning under them leaves the clouds-off look alone. The cloud lab writes one chapter's entry, or
/// its layer, through <see cref="WithEntry"/>, keeping every other key.
/// </summary>
public sealed class CloudShadowChapters
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/cloud_shadows.json";

    /// <summary>The block inside an entry that applies while Volumetric Clouds stands on.</summary>
    public const string CloudsLayer = "clouds";

    /// <summary>An entry's field names, in the order the lab writes them.</summary>
    public static readonly IReadOnlyList<string> Keys = CloudShadowPattern.Fields.Select(f => f.Key).ToArray();

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, CloudShadowPattern.Tune> _entries = new(StringComparer.OrdinalIgnoreCase);
    // Each entry with its clouds layer applied, for the chapters that carry one.
    private readonly Dictionary<string, CloudShadowPattern.Tune> _clouds = new(StringComparer.OrdinalIgnoreCase);
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
            var tune = chapters._entries[key] = ReadEntry(key, fields, CloudShadowPattern.Tune.Default, chapters._warnings);
            if (Find(fields, CloudsLayer) is { } layer)
            {
                if (layer is JsonObject over)
                    chapters._clouds[key] = ReadEntry($"{key}/{CloudsLayer}", over, tune, chapters._warnings);
                else
                    chapters._warnings.Add($"{key}/{CloudsLayer} is not an object, ignored");
            }
        }
        return chapters;
    }

    /// <summary>The file's text with <paramref name="chapter"/>'s entry replaced by every field of
    /// <paramref name="tune"/>, its <see cref="CloudsLayer"/> block kept. With <paramref name="clouds"/>
    /// only that block is replaced, by the fields where <paramref name="tune"/> differs from the entry,
    /// and dropped when none does. Every other key keeps its place and value; a new chapter is appended.
    /// Throws <see cref="InvalidDataException"/> when <paramref name="existing"/> is not a JSON object,
    /// so a Save never replaces a file it cannot read.</summary>
    public static string WithEntry(string? existing, string chapter, CloudShadowPattern.Tune tune, bool clouds = false)
    {
        var root = Mech3.VegetationSidecar.ObjectToRewrite(existing, "cloud shadows");

        string name = chapter.ToUpperInvariant();
        var old = Find(root, name) as JsonObject ?? new JsonObject();
        var held = tune.Clamped();
        var under = clouds ? ReadEntry(name, old, CloudShadowPattern.Tune.Default, new List<string>()) : (CloudShadowPattern.Tune?)null;
        var fields = new JsonObject();
        foreach (var f in CloudShadowPattern.Fields)
        {
            if (f.Get(held) is { } v && (under is not { } u || Rounded(f.Get(u)) != Rounded(v)))
                fields[f.Key] = JsonValue.Create(Math.Round((double)v, 3));
        }

        JsonObject entry;
        if (clouds)
        {
            entry = Replaced(old, CloudsLayer, fields.Count > 0 ? fields : null);
        }
        else
        {
            // The fields first, then the layer and any note in their order.
            entry = fields;
            foreach (var (key, node) in old)
            {
                if (!CloudShadowPattern.Fields.Any(f => f.Key == key))
                    entry[key] = node?.DeepClone();
            }
        }
        return Replaced(root, name, entry).ToJsonString(Written) + Environment.NewLine;
    }

    /// <summary>Writes <paramref name="chapter"/>'s entry, or with <paramref name="clouds"/> its layer
    /// (<see cref="WithEntry"/>), into the file at <paramref name="diskPath"/>, creating it when absent.</summary>
    public static void Save(string diskPath, string chapter, CloudShadowPattern.Tune tune, bool clouds = false)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        File.WriteAllText(diskPath, WithEntry(existing, chapter, tune, clouds), new UTF8Encoding(false));
    }

    /// <summary>The project's copy of the file while the game runs from the source tree. Null in an
    /// exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>The chapter's saved tunes, or the constants when the file has no entry for it. With
    /// <paramref name="clouds"/> its <see cref="CloudsLayer"/> block applies over them.</summary>
    public CloudShadowPattern.Tune For(string chapter, bool clouds = false) =>
        clouds && _clouds.TryGetValue(chapter, out var layered) ? layered
        : _entries.TryGetValue(chapter, out var tune) ? tune : CloudShadowPattern.Tune.Default;

    // A value as the file writes it, so a layer holds only what a reader would see differ.
    private static double? Rounded(float? value) => value is { } v ? Math.Round((double)v, 3) : null;

    // The member under `key` in any case, null when absent.
    private static JsonNode? Find(JsonObject parent, string key) =>
        parent.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    // `parent` rebuilt with the member under `key` (any case) replaced in its place, or appended, since
    // a JsonObject cannot replace a member in its place. A null `value` drops it.
    private static JsonObject Replaced(JsonObject parent, string key, JsonNode? value)
    {
        var rebuilt = new JsonObject();
        bool placed = false;
        foreach (var (k, node) in parent)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                if (!placed && value != null)
                    rebuilt[key] = value;
                placed = true;
                continue;
            }
            rebuilt[k] = node?.DeepClone();
        }
        if (!placed && value != null)
            rebuilt[key] = value;
        return rebuilt;
    }

    // The fields in `fields` over `start`. The clouds layer is read on its own (Parse).
    private static CloudShadowPattern.Tune ReadEntry(string chapter, JsonObject fields, CloudShadowPattern.Tune start,
        List<string> warnings)
    {
        var tune = start;
        foreach (var (key, node) in fields)
        {
            if (key.StartsWith('_') || key.Equals(CloudsLayer, StringComparison.OrdinalIgnoreCase))
                continue;
            if (node is not JsonValue value || !value.TryGetValue(out double number) || !double.IsFinite(number))
            {
                warnings.Add($"{chapter}: '{key}' is not a number, it takes the default");
                continue;
            }
            if (CloudShadowPattern.Fields.FirstOrDefault(f => f.Key == key) is not { } field)
            {
                warnings.Add($"{chapter}: '{key}' is not a cloud shadow field ({string.Join("/", Keys)}), ignored");
                continue;
            }
            tune = field.With(tune, (float)number);
        }
        var clamped = tune.Clamped();
        if (clamped != tune)
            warnings.Add($"{chapter}: a field out of range is held inside it");
        return clamped;
    }
}
