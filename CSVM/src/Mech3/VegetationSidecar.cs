using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Utils;

namespace CSVM.Mech3;

/// <summary>
/// One listed vegetation kind and its Rich variation, keyed by the decoration model name
/// (<c>firtree1.flt</c>), the key <c>templates.zrd</c> uses. Each value is a symmetric spread drawn
/// per stamp. The jitter is in texture repeats on each UV axis, the yaw in degrees about Y. The tint
/// scales each colour channel and the brightness all three, both as a fraction of 1. Zero is the
/// faithful stamp.
/// </summary>
public sealed record VegetationKind(string Model, float JitterUv = 0f, float YawDeg = 0f, float Tint = 0f, float Brightness = 0f)
{
    /// <summary>The numeric fields in file order, each with its JSON key and the lab's range, which a
    /// load clamps into. Every range starts at 0.</summary>
    public static readonly IReadOnlyList<Field> Fields = new Field[]
    {
        new("jitter_uv", 0.5f, k => k.JitterUv, (k, v) => k with { JitterUv = v }),
        new("yaw_deg", 180f, k => k.YawDeg, (k, v) => k with { YawDeg = v }),
        new("tint", 0.5f, k => k.Tint, (k, v) => k with { Tint = v }),
        new("brightness", 0.5f, k => k.Brightness, (k, v) => k with { Brightness = v }),
    };

    /// <summary>One numeric field: its JSON key, its upper bound, and how to read and set it.</summary>
    public sealed record Field(string Key, float Max, Func<VegetationKind, float> Get, Func<VegetationKind, float, VegetationKind> With);
}

/// <summary>A named list of listed kinds that a texture pick stamps on its ground texture.</summary>
public sealed record VegetationSet(string Name, IReadOnlyList<string> Models);

/// <summary>A ground texture given a vegetation set by the lab's paint tool.</summary>
public sealed record TexturePick(string Texture, string Set);

/// <summary>
/// One chapter's entry: the listed kinds, the seed salt of the Rich variation stream, the vegetation
/// sets and the texture picks. The mask is the brush mask's path relative to <c>CSVM/data</c>, empty
/// for none. A kind absent from <see cref="Kinds"/> is not vegetation and keeps the faithful stamp.
/// </summary>
public sealed class VegetationEntry
{
    /// <summary>A chapter with no entry: nothing listed, so every stamp is faithful.</summary>
    public static readonly VegetationEntry Empty = new();

    public int Seed { get; init; }

    public string Mask { get; init; } = "";

    public IReadOnlyList<VegetationKind> Kinds { get; init; } = Array.Empty<VegetationKind>();

    public IReadOnlyList<VegetationSet> Sets { get; init; } = Array.Empty<VegetationSet>();

    public IReadOnlyList<TexturePick> Picks { get; init; } = Array.Empty<TexturePick>();

    /// <summary>The listed kind for a decoration model name, or null when the model is not vegetation.</summary>
    public VegetationKind? Find(string model)
    {
        foreach (var k in Kinds)
        {
            if (string.Equals(k.Model, model, StringComparison.OrdinalIgnoreCase))
                return k;
        }

        return null;
    }

    /// <summary>A copy with <paramref name="model"/>'s <paramref name="field"/> at <paramref name="value"/>,
    /// clamped into the field's range, the lab's slider edit. Throws for a model the entry does not list,
    /// since a slider exists only for a listed kind.</summary>
    public VegetationEntry With(string model, VegetationKind.Field field, float value)
    {
        var kinds = new List<VegetationKind>(Kinds);
        int i = kinds.FindIndex(k => string.Equals(k.Model, model, StringComparison.OrdinalIgnoreCase));
        if (i < 0)
            throw new ArgumentException($"'{model}' is not a listed kind", nameof(model));
        kinds[i] = field.With(kinds[i], Math.Clamp(value, 0f, field.Max));
        return new VegetationEntry { Seed = Seed, Mask = Mask, Kinds = kinds, Sets = Sets, Picks = Picks };
    }

    /// <summary>A copy with the variation stream's seed salt at <paramref name="seed"/>.</summary>
    public VegetationEntry WithSeed(int seed) =>
        new() { Seed = seed, Mask = Mask, Kinds = Kinds, Sets = Sets, Picks = Picks };

    /// <summary>A copy whose brush mask is <paramref name="mask"/>, a path relative to <c>CSVM/data</c>.</summary>
    public VegetationEntry WithMask(string mask) =>
        new() { Seed = Seed, Mask = mask, Kinds = Kinds, Sets = Sets, Picks = Picks };

    /// <summary>A copy where <paramref name="texture"/> is picked for <paramref name="models"/>, the lab's pick.
    /// A set with exactly those members is reused, or one named by joining them with <c>+</c> is added.
    /// A model the entry does not list is dropped, and no listed model left removes the pick.</summary>
    public VegetationEntry WithPick(string texture, IEnumerable<string> models)
    {
        var members = new List<string>();
        foreach (var m in models)
        {
            if (Find(m) is { } kind && !members.Contains(kind.Model))
                members.Add(kind.Model);
        }

        var picks = new List<TexturePick>(Picks);
        picks.RemoveAll(p => string.Equals(p.Texture, texture, StringComparison.OrdinalIgnoreCase));
        var sets = new List<VegetationSet>(Sets);
        if (members.Count > 0)
        {
            var set = sets.Find(s => s.Models.Count == members.Count
                && members.TrueForAll(m => s.Models.Contains(m, StringComparer.OrdinalIgnoreCase)));
            if (set == null)
            {
                set = new VegetationSet(string.Join("+", members), members);
                sets.RemoveAll(s => string.Equals(s.Name, set.Name, StringComparison.OrdinalIgnoreCase));
                sets.Add(set);
            }
            picks.Add(new TexturePick(texture, set.Name));
        }

        return new VegetationEntry { Seed = Seed, Mask = Mask, Kinds = Kinds, Sets = sets, Picks = picks };
    }

    /// <summary>The vegetation set picked for a ground texture, or null when the texture has no pick.</summary>
    public VegetationSet? SetFor(string texture)
    {
        foreach (var p in Picks)
        {
            if (!string.Equals(p.Texture, texture, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var s in Sets)
            {
                if (string.Equals(s.Name, p.Set, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
        }

        return null;
    }
}

/// <summary>
/// The shipped per-chapter vegetation sidecar, <c>CSVM/data/vegetation.json</c>, read under the Rich
/// Vegetation Quality only. Shape and field meanings: the file's <c>_about</c> and its Mech3.md entry. A
/// missing chapter lists nothing. An unknown key, a bad or clamped value, and a set or pick naming
/// what the entry lacks are each a warning, never fatal. Keys beginning with
/// <c>_</c> are prose. The vegetation lab writes one chapter's entry through <see cref="WithEntry"/>,
/// keeping every other key and its order.
/// </summary>
public sealed class VegetationSidecar
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/vegetation.json";

    /// <summary>Every chapter with ground to carry vegetation. C1C and C2B have none and C5 is city only,
    /// so an entry for them is refused rather than letting a city block be listed.</summary>
    public static readonly IReadOnlyList<string> Chapters = new[] { "C1", "C1B", "C2", "C3", "C4" };

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Comments are skipped on every read, so a note added by hand breaks neither a load nor a Save.
    private static readonly JsonDocumentOptions Reading = new() { CommentHandling = JsonCommentHandling.Skip };

    private readonly Dictionary<string, VegetationEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    /// <summary>What the file got wrong, one line each.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Reads the sidecar, logging each warning. A missing or unreadable file lists nothing.</summary>
    public static VegetationSidecar Load()
    {
        VegetationSidecar sidecar;
        if (!Godot.FileAccess.FileExists(DefaultPath))
        {
            sidecar = new VegetationSidecar();
            sidecar._warnings.Add($"{DefaultPath} not found, no chapter lists vegetation");
        }
        else
        {
            sidecar = Parse(Encoding.UTF8.GetString(Godot.FileAccess.GetFileAsBytes(DefaultPath)));
        }

        foreach (var w in sidecar.Warnings)
            Log.Warn("world", $"vegetation sidecar: {w}");
        return sidecar;
    }

    /// <summary>Parses the file's text with no file IO and no engine call, so the rules unit-test.</summary>
    public static VegetationSidecar Parse(string json)
    {
        var sidecar = new VegetationSidecar();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: Reading);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            sidecar._warnings.Add($"not readable JSON ({ex.Message}), no chapter lists vegetation");
            return sidecar;
        }

        if (root is not JsonObject chapters)
        {
            sidecar._warnings.Add("the file is not a JSON object, no chapter lists vegetation");
            return sidecar;
        }

        foreach (var (key, node) in chapters)
        {
            if (key.StartsWith('_'))
                continue;
            if (Canonical(key) is not { } chapter)
            {
                sidecar._warnings.Add($"'{key}' is not a vegetation chapter ({string.Join("/", Chapters)}), ignored");
                continue;
            }
            if (node is not JsonObject fields)
            {
                sidecar._warnings.Add($"{chapter} is not an object, it lists nothing");
                continue;
            }
            sidecar._entries[chapter] = ReadEntry(chapter, fields, sidecar._warnings);
        }

        return sidecar;
    }

    /// <summary>The file's text with <paramref name="chapter"/>'s entry replaced by <paramref name="entry"/>,
    /// each kind writing only its nonzero fields. Every other key keeps its place and value; a chapter the
    /// file lacks is appended. Throws <see cref="InvalidDataException"/> when <paramref name="existing"/> is
    /// not a JSON object, so a Save never replaces a file it cannot read.</summary>
    public static string WithEntry(string? existing, string chapter, VegetationEntry entry)
    {
        string name = Canonical(chapter) ?? throw new ArgumentException($"'{chapter}' is not a vegetation chapter", nameof(chapter));
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existing))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(existing, documentOptions: Reading) as JsonObject
                    ?? throw new InvalidDataException("the vegetation file is not a JSON object, so Save leaves it as it is");
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                throw new InvalidDataException($"the vegetation file is not readable JSON ({ex.Message}), so Save leaves it as it is", ex);
            }
        }

        // Rebuilt key by key, since a JsonObject cannot replace a member in its place.
        var rebuilt = new JsonObject();
        bool placed = false;
        foreach (var (key, node) in root)
        {
            if (Canonical(key) == name)
            {
                if (!placed)
                    rebuilt[name] = EntryNode(entry);
                placed = true;
                continue;
            }
            rebuilt[key] = node?.DeepClone();
        }
        if (!placed)
            rebuilt[name] = EntryNode(entry);
        return rebuilt.ToJsonString(Written) + Environment.NewLine;
    }

    /// <summary>Writes <paramref name="chapter"/>'s entry into the file at <paramref name="diskPath"/> through
    /// <see cref="AtomicFile"/>, creating it when absent. A file that is not a JSON object is left untouched
    /// and <see cref="InvalidDataException"/> thrown.</summary>
    public static void Save(string diskPath, string chapter, VegetationEntry entry)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        AtomicFile.WriteAllText(diskPath, WithEntry(existing, chapter, entry));
    }

    /// <summary>The project's copy of the file on disk while the game runs from the source tree. Null in
    /// an exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>The chapter's entry, or <see cref="VegetationEntry.Empty"/> when the file has none.</summary>
    public VegetationEntry For(string chapter) =>
        Canonical(chapter) is { } name && _entries.TryGetValue(name, out var entry) ? entry : VegetationEntry.Empty;

    // The file's spelling of a vegetation chapter, or null for any other key.
    private static string? Canonical(string chapter)
    {
        foreach (var c in Chapters)
        {
            if (string.Equals(c, chapter, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static JsonObject EntryNode(VegetationEntry entry)
    {
        var node = new JsonObject();
        if (entry.Seed != 0)
            node["seed"] = entry.Seed;
        if (entry.Mask.Length > 0)
            node["mask"] = entry.Mask;
        var kinds = new JsonObject();
        foreach (var kind in entry.Kinds)
        {
            var values = new JsonObject();
            foreach (var f in VegetationKind.Fields)
            {
                double v = Math.Round(Math.Clamp(f.Get(kind), 0f, f.Max), 4);
                if (v != 0)
                    values[f.Key] = v;
            }
            kinds[kind.Model] = values;
        }
        node["kinds"] = kinds;
        if (entry.Sets.Count > 0)
        {
            var sets = new JsonObject();
            foreach (var s in entry.Sets)
            {
                var models = new JsonArray();
                foreach (var m in s.Models)
                    models.Add((JsonNode)m);
                sets[s.Name] = models;
            }
            node["sets"] = sets;
        }
        if (entry.Picks.Count > 0)
        {
            var picks = new JsonObject();
            foreach (var p in entry.Picks)
                picks[p.Texture] = p.Set;
            node["picks"] = picks;
        }
        return node;
    }

    private static VegetationEntry ReadEntry(string chapter, JsonObject fields, List<string> warnings)
    {
        int seed = 0;
        string mask = "";
        var kinds = new List<VegetationKind>();
        JsonObject? sets = null, picks = null;
        foreach (var (key, node) in fields)
        {
            if (key.StartsWith('_'))
                continue;
            switch (key)
            {
                case "seed":
                    if (node is JsonValue sv && sv.TryGetValue(out int s))
                        seed = s;
                    else
                        warnings.Add($"{chapter}: 'seed' is not an integer, it takes 0");
                    break;
                case "mask":
                    if (node is JsonValue mv && mv.TryGetValue(out string? m))
                        mask = m;
                    else
                        warnings.Add($"{chapter}: 'mask' is not a string, the chapter has no mask");
                    break;
                case "kinds":
                    if (node is JsonObject k)
                        ReadKinds(chapter, k, kinds, warnings);
                    else
                        warnings.Add($"{chapter}: 'kinds' is not an object, it lists nothing");
                    break;
                case "sets":
                    sets = node as JsonObject;
                    if (sets == null)
                        warnings.Add($"{chapter}: 'sets' is not an object, ignored");
                    break;
                case "picks":
                    picks = node as JsonObject;
                    if (picks == null)
                        warnings.Add($"{chapter}: 'picks' is not an object, ignored");
                    break;
                default:
                    warnings.Add($"{chapter}: '{key}' is not a vegetation field, ignored");
                    break;
            }
        }

        var readSets = sets == null ? new List<VegetationSet>() : ReadSets(chapter, sets, new VegetationEntry { Kinds = kinds }, warnings);
        var readPicks = picks == null ? new List<TexturePick>() : ReadPicks(chapter, picks, readSets, warnings);
        return new VegetationEntry { Seed = seed, Mask = mask, Kinds = kinds, Sets = readSets, Picks = readPicks };
    }

    private static void ReadKinds(string chapter, JsonObject node, List<VegetationKind> kinds, List<string> warnings)
    {
        var entry = new VegetationEntry { Kinds = kinds };
        foreach (var (model, values) in node)
        {
            if (entry.Find(model) != null)
            {
                warnings.Add($"{chapter}: '{model}' is listed twice, the first stands");
                continue;
            }
            if (values is not JsonObject fields)
            {
                warnings.Add($"{chapter}: '{model}' is not an object, it is listed at the faithful stamp");
                kinds.Add(new VegetationKind(model));
                continue;
            }
            kinds.Add(ReadKind(chapter, model, fields, warnings));
        }
    }

    private static VegetationKind ReadKind(string chapter, string model, JsonObject fields, List<string> warnings)
    {
        var kind = new VegetationKind(model);
        foreach (var (key, node) in fields)
        {
            if (key.StartsWith('_'))
                continue;
            var field = FindField(key);
            if (field == null)
            {
                warnings.Add($"{chapter}: {model}: '{key}' is not a kind field, ignored");
                continue;
            }
            if (node is not JsonValue value || !value.TryGetValue(out double number) || !double.IsFinite(number))
            {
                warnings.Add($"{chapter}: {model}: '{key}' is not a number, it takes 0");
                continue;
            }
            float held = Math.Clamp((float)number, 0f, field.Max);
            if (held != (float)number)
                warnings.Add($"{chapter}: {model}: '{key}' {number.ToString(System.Globalization.CultureInfo.InvariantCulture)} is held at {held.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            kind = field.With(kind, held);
        }

        return kind;
    }

    // A set may name only the entry's listed kinds, which keeps a pick from stamping a city block or a car.
    private static List<VegetationSet> ReadSets(string chapter, JsonObject node, VegetationEntry entry, List<string> warnings)
    {
        var sets = new List<VegetationSet>();
        foreach (var (name, members) in node)
        {
            if (members is not JsonArray array)
            {
                warnings.Add($"{chapter}: set '{name}' is not a list, ignored");
                continue;
            }
            var models = new List<string>();
            foreach (var member in array)
            {
                if (member is JsonValue v && v.TryGetValue(out string? model) && entry.Find(model) is { } kind)
                    models.Add(kind.Model);
                else
                    warnings.Add($"{chapter}: set '{name}' names '{member?.ToJsonString()}', which is not a listed kind, dropped");
            }
            sets.Add(new VegetationSet(name, models));
        }

        return sets;
    }

    private static List<TexturePick> ReadPicks(string chapter, JsonObject node, List<VegetationSet> sets, List<string> warnings)
    {
        var picks = new List<TexturePick>();
        foreach (var (texture, set) in node)
        {
            if (set is JsonValue v && v.TryGetValue(out string? name)
                && sets.Exists(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                picks.Add(new TexturePick(texture, name));
            }
            else
            {
                warnings.Add($"{chapter}: pick '{texture}' names '{set?.ToJsonString()}', which is not a set, dropped");
            }
        }

        return picks;
    }

    private static VegetationKind.Field? FindField(string key)
    {
        foreach (var f in VegetationKind.Fields)
        {
            if (f.Key == key)
                return f;
        }

        return null;
    }
}
