using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Effects;

namespace CSVM.Session.World;

/// <summary>
/// The shipped Enhanced lighting tune, <c>CSVM/data/lighting.json</c>: per chapter, an optional
/// mission, then a <see cref="ZoneLighting"/> block per weather zone (<c>ZONE1</c> to <c>ZONE3</c>).
/// A block holds only the fields that differ from what it sits over. A chapter block sits over the
/// defaults, and a mission block over its chapter's, so a later chapter edit still reaches the mission.
/// A zone block's <see cref="Layers"/> are sparse blocks applied over the resolved zone while their
/// option stands on, each chapter then mission. A mission's <c>night</c> flag marks a moonlit
/// mission (CONTEXT.md), <c>_</c> keys are prose, and a malformed entry is a warning, never fatal.
/// The lighting lab writes through <see cref="WithZone"/>, keeping every other key and its order.
/// </summary>
public sealed class LightingTable
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/lighting.json";

    /// <summary>The layer that applies while Volumetric Clouds stands on.</summary>
    public const string CloudsLayer = ZoneLighting.CloudsLayer;

    /// <summary>The layer that applies while Global Illumination stands on.</summary>
    public const string GiLayer = ZoneLighting.GiLayer;

    /// <summary>The table with nothing in it, what a missing or unreadable file gives.</summary>
    public static readonly LightingTable None = new();

    /// <summary>Every layer in the order they apply: <c>gi</c> after <c>clouds</c>, so GI's tune has the last word.</summary>
    public static readonly IReadOnlyList<string> Layers = new[] { CloudsLayer, GiLayer };

    private const string NightKey = "night";

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly IReadOnlyDictionary<string, float> NoFields = new Dictionary<string, float>();

    private readonly Dictionary<string, Entry> _chapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    /// <summary>What the file got wrong, one line each.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Every mission the file flags as night, spelled <c>&lt;chapter&gt;/&lt;mission&gt;</c>.</summary>
    public IEnumerable<string> NightMissions
    {
        get
        {
            foreach (var (chapter, entry) in _chapters)
            {
                foreach (var (mission, m) in entry.Missions)
                {
                    if (m.Night)
                        yield return $"{chapter}/{mission}";
                }
            }
        }
    }

    /// <summary>Whether a key names a weather zone (<c>zone1</c> to <c>zone9</c>, any case).</summary>
    public static bool IsZoneKey(string key) =>
        key.Length == 5 && key.StartsWith("zone", StringComparison.OrdinalIgnoreCase) && key[4] is >= '1' and <= '9';

    /// <summary>The layers that apply with the clouds and GI on or off, in their order.</summary>
    public static IReadOnlyList<string> ActiveLayers(bool clouds, bool gi) =>
        clouds && gi ? Layers : clouds ? new[] { CloudsLayer } : gi ? new[] { GiLayer } : Array.Empty<string>();

    /// <summary>Reads the shipped table, logging each warning. A missing file tunes nothing.</summary>
    public static LightingTable Load()
    {
        if (!Godot.FileAccess.FileExists(DefaultPath))
        {
            Utils.Log.Warn("world", $"lighting: {DefaultPath} not found, every zone lights untuned");
            return None;
        }

        var table = Parse(Encoding.UTF8.GetString(Godot.FileAccess.GetFileAsBytes(DefaultPath)));
        foreach (var w in table.Warnings)
            Utils.Log.Warn("world", $"lighting: {w}");
        return table;
    }

    /// <summary>Parses the file's text with no file IO and no engine call, so the rules unit-test.</summary>
    public static LightingTable Parse(string json)
    {
        var table = new LightingTable();
        JsonNode? root;
        try
        {
            root = Mech3.VegetationSidecar.ParseWhole(json);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            table._warnings.Add($"not readable JSON ({ex.Message}), every zone lights untuned");
            return table;
        }

        if (root is not JsonObject chapters)
        {
            table._warnings.Add("the file is not a JSON object, every zone lights untuned");
            return table;
        }

        foreach (var (chapter, node) in chapters)
        {
            if (chapter.StartsWith('_'))
                continue;
            if (node is not JsonObject body)
            {
                table._warnings.Add($"{chapter} is not an object, ignored");
                continue;
            }
            var entry = new Entry();
            foreach (var (key, child) in body)
            {
                if (key.StartsWith('_'))
                    continue;
                if (IsZoneKey(key))
                    ReadZone($"{chapter}/{key}", key, child, entry.Zones, table._warnings);
                else if (child is JsonObject mission)
                    entry.Missions[key] = ReadMission($"{chapter}/{key}", mission, table._warnings);
                else
                    table._warnings.Add($"{chapter}: '{key}' is neither a zone nor a mission object, ignored");
            }
            table._chapters[chapter] = entry;
        }

        return table;
    }

    /// <summary>The file's text with one zone's chapter and mission blocks, or their <paramref name="layer"/>,
    /// written. The chapter block holds <paramref name="chapterLighting"/>'s differences from <paramref name="under"/>
    /// (the defaults when null), the mission block the <paramref name="missionFields"/> that differ from it.
    /// A field of another layer (<see cref="Belongs"/>) is never written. An empty block is dropped, and so is a
    /// mission left with no zone and no night flag. A non-null <paramref name="night"/> sets the mission's flag
    /// (false removes it). Throws <see cref="InvalidDataException"/> when <paramref name="existing"/> is not a JSON object.</summary>
    public static string WithZone(string? existing, string chapter, string? mission, string zone,
        ZoneLighting chapterLighting, IReadOnlyDictionary<string, float> missionFields, bool? night = null,
        string? layer = null, ZoneLighting? under = null)
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
                    ?? throw new InvalidDataException("the lighting file is not a JSON object, so Save leaves it as it is");
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                throw new InvalidDataException($"the lighting file is not readable JSON ({ex.Message}), so Save leaves it as it is", ex);
            }
        }

        string zoneKey = zone.ToUpperInvariant();
        var chapterNode = Child(root, chapter);
        PutFields(chapterNode, zoneKey, layer, chapterLighting.Differences(under ?? ZoneLighting.Default));
        if (mission != null)
        {
            var missionNode = Child(chapterNode, mission);
            if (night == true)
                missionNode[Key(missionNode, NightKey)] = true;
            else if (night == false)
                Remove(missionNode, NightKey);
            var over = chapterLighting.Over(missionFields).Differences(chapterLighting);
            PutFields(missionNode, zoneKey, layer, over);
            if (missionNode.Count == 0)
                Remove(chapterNode, mission);
        }
        if (chapterNode.Count == 0)
            Remove(root, chapter);
        return root.ToJsonString(Written) + Environment.NewLine;
    }

    /// <summary>Whether a block of <paramref name="layer"/> (null for the base) may hold <paramref name="field"/>.
    /// A field that draws under one option alone belongs in that option's layer only.</summary>
    public static bool Belongs(ZoneLighting.Field field, string? layer) => field.Layer == null || field.Layer == layer;

    /// <summary>Writes one zone's blocks into the file at <paramref name="diskPath"/>
    /// (<see cref="WithZone"/>), creating it when absent.</summary>
    public static void Save(string diskPath, string chapter, string? mission, string zone,
        ZoneLighting chapterLighting, IReadOnlyDictionary<string, float> missionFields, bool? night = null,
        string? layer = null, ZoneLighting? under = null)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        File.WriteAllText(diskPath, WithZone(existing, chapter, mission, zone, chapterLighting, missionFields, night, layer, under),
            new UTF8Encoding(false));
    }

    /// <summary>The project's copy of the file on disk while the game runs from the source tree. Null in
    /// an exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>Whether the file flags <paramref name="chapter"/>'s <paramref name="mission"/> as night.</summary>
    public bool Night(string chapter, string mission) =>
        _chapters.TryGetValue(chapter, out var c) && c.Missions.TryGetValue(mission, out var m) && m.Night;

    /// <summary>A chapter zone block's fields, or its <paramref name="layer"/>'s, as the file holds them.</summary>
    public IReadOnlyDictionary<string, float> ChapterFields(string chapter, string zone, string? layer = null) =>
        _chapters.TryGetValue(chapter, out var c) && c.Zones.TryGetValue(LayerKey(zone, layer), out var z) ? z : NoFields;

    /// <summary>A mission zone block's fields, or its <paramref name="layer"/>'s, as the file holds them.</summary>
    public IReadOnlyDictionary<string, float> MissionFields(string chapter, string mission, string zone, string? layer = null) =>
        _chapters.TryGetValue(chapter, out var c) && c.Missions.TryGetValue(mission, out var m)
        && m.Zones.TryGetValue(LayerKey(zone, layer), out var z) ? z : NoFields;

    /// <summary>The chapter's tune for one zone, over the defaults.</summary>
    public ZoneLighting ChapterLighting(string chapter, string zone) => ZoneLighting.Default.Over(ChapterFields(chapter, zone));

    /// <summary>The tune one mission's zone lights with: its block over its chapter's over the defaults.
    /// Then each of <paramref name="layers"/> in turn, its chapter block and then its mission block.</summary>
    public ZoneLighting For(string chapter, string mission, string zone, IEnumerable<string>? layers = null)
    {
        var tune = ChapterLighting(chapter, zone).Over(MissionFields(chapter, mission, zone));
        foreach (string layer in layers ?? Array.Empty<string>())
            tune = tune.Over(ChapterFields(chapter, zone, layer)).Over(MissionFields(chapter, mission, zone, layer));
        return tune;
    }

    // A zone's own key, or the key its layer is held under.
    private static string LayerKey(string zone, string? layer) => layer == null ? zone : $"{zone}/{layer}";

    // A zone block and each layer inside it, the layers held under their own keys.
    private static void ReadZone(string where, string zone, JsonNode? node,
        Dictionary<string, Dictionary<string, float>> zones, List<string> warnings) =>
        zones[zone] = ReadBlock(where, node, warnings,
            (layer, child) => zones[LayerKey(zone, layer)] = ReadBlock($"{where}/{layer}", child, warnings, null));

    // One block's fields. A layer key is handed to `layer` where one is allowed, else it reads as an unknown field.
    private static Dictionary<string, float> ReadBlock(string where, JsonNode? node, List<string> warnings,
        Action<string, JsonNode?>? layer)
    {
        var fields = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonObject block)
        {
            warnings.Add($"{where} is not an object, ignored");
            return fields;
        }
        foreach (var (key, value) in block)
        {
            if (key.StartsWith('_'))
                continue;
            if (layer != null && Layers.FirstOrDefault(l => l.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } named)
            {
                layer(named, value);
                continue;
            }
            if (ZoneLighting.Find(key) is not { } field)
            {
                warnings.Add($"{where}: '{key}' is not a lighting field, ignored");
                continue;
            }
            if (value is not JsonValue v || !v.TryGetValue(out double number) || !double.IsFinite(number))
            {
                warnings.Add($"{where}: '{key}' is not a number, ignored");
                continue;
            }
            float clamped = Math.Clamp((float)number, field.Min, field.Max);
            if (clamped != (float)number)
                warnings.Add($"{where}: '{key}' {field.Format((float)number)} is held at {field.Format(clamped)}");
            fields[field.Key] = clamped;
        }

        return fields;
    }

    private static Mission ReadMission(string where, JsonObject body, List<string> warnings)
    {
        var mission = new Mission();
        foreach (var (key, child) in body)
        {
            if (key.StartsWith('_'))
                continue;
            if (key.Equals(NightKey, StringComparison.OrdinalIgnoreCase))
            {
                if (child is JsonValue v && v.TryGetValue(out bool night))
                    mission.Night = night;
                else
                    warnings.Add($"{where}: '{NightKey}' is not true or false, ignored");
            }
            else if (IsZoneKey(key))
            {
                ReadZone($"{where}/{key}", key, child, mission.Zones, warnings);
            }
            else
            {
                warnings.Add($"{where}: '{key}' is neither '{NightKey}' nor a zone, ignored");
            }
        }

        return mission;
    }

    // The object under the key matched in any case, created at the end when absent.
    private static JsonObject Child(JsonObject parent, string key)
    {
        foreach (var (k, node) in parent)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase) && node is JsonObject found)
                return found;
        }
        var made = new JsonObject();
        parent[Key(parent, key)] = made;
        return made;
    }

    // One zone's fields written into its block, or into the block's layer. A block left empty is dropped.
    private static void PutFields(JsonObject parent, string zoneKey, string? layer, Dictionary<string, float> fields)
    {
        var zone = Child(parent, zoneKey);
        var block = layer == null ? zone : Child(zone, layer);
        var others = block.Where(kv => ZoneLighting.Find(kv.Key) == null).ToList();
        // The fields go first in their own order, then every other key (a layer, a note) in its order.
        block.Clear();
        foreach (var f in ZoneLighting.Fields)
        {
            if (Belongs(f, layer) && fields.TryGetValue(f.Key, out float v))
                block[f.Key] = JsonValue.Create(ZoneLighting.Field.Rounded(v));
        }
        foreach (var (key, node) in others)
            block[key] = node;
        if (block.Count == 0 && layer != null)
            Remove(zone, layer);
        if (zone.Count == 0)
            Remove(parent, zoneKey);
    }

    // The key as the object spells it in any case, else as given.
    private static string Key(JsonObject parent, string key)
    {
        foreach (var (k, _) in parent)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                return k;
        }
        return key;
    }

    private static void Remove(JsonObject parent, string key)
    {
        foreach (var (k, _) in parent)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                parent.Remove(k);
                return;
            }
        }
    }

    private sealed class Entry
    {
        public Dictionary<string, Dictionary<string, float>> Zones { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Mission> Missions { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Mission
    {
        public bool Night { get; set; }

        public Dictionary<string, Dictionary<string, float>> Zones { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
