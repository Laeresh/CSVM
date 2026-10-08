using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSVM.Effects;

/// <summary>
/// The shipped per-chapter seas, <c>CSVM/data/ocean_seas.json</c>: one object per sea chapter
/// holding only the <see cref="SeaState"/> fields that differ from the defaults. A missing entry or
/// field takes the default; an unknown chapter or field is reported, never fatal. Keys beginning
/// with <c>_</c> are prose. The ocean lab writes one chapter's entry through
/// <see cref="WithEntry"/>, keeping every other key and its order. Shipped data, so a network peer
/// and a <c>--det</c> capture agree as long as both load the same file.
/// </summary>
public sealed class OceanSeas
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/ocean_seas.json";

    /// <summary>Every chapter with a sea at y = 0, each with an entry in the file. C4 has only raised
    /// lakes, which keep the flat glossy water.</summary>
    public static readonly IReadOnlyList<string> Chapters = new[] { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C5" };

    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, SeaState> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    /// <summary>What the file got wrong, one line each. Unreadable JSON, an unknown chapter or field, a
    /// value that is not a number and a value clamped into range each count.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Whether a chapter names a sea in the file's list.</summary>
    public static bool IsSeaChapter(string chapter) =>
        ((IList<string>)Chapters).IndexOf(Canonical(chapter) ?? "") >= 0;

    /// <summary>Reads the seas, logging each warning. A missing or unreadable file draws every
    /// chapter at the defaults, which is the ocean the constants always drew.</summary>
    public static OceanSeas Load(string? path = null)
    {
        path ??= DefaultPath;
        bool viaGodot = path.StartsWith("res://", StringComparison.Ordinal);
        OceanSeas seas;
        if (viaGodot ? !Godot.FileAccess.FileExists(path) : !File.Exists(path))
        {
            seas = new OceanSeas();
            seas._warnings.Add($"{path} not found, every chapter takes the default sea");
        }
        else
        {
            byte[] bytes = viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path);
            seas = Parse(Encoding.UTF8.GetString(bytes));
        }

        foreach (var w in seas.Warnings)
            Utils.Log.Warn("world", $"ocean seas: {w}");
        return seas;
    }

    /// <summary>Parses the file's text with no file IO and no engine call, so the rules unit-test.</summary>
    public static OceanSeas Parse(string json)
    {
        var seas = new OceanSeas();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            seas._warnings.Add($"not readable JSON ({ex.Message}), every chapter takes the default sea");
            return seas;
        }

        if (root is not JsonObject chapters)
        {
            seas._warnings.Add("the file is not a JSON object, every chapter takes the default sea");
            return seas;
        }

        foreach (var (key, node) in chapters)
        {
            if (key.StartsWith('_'))
                continue;
            if (Canonical(key) is not { } chapter)
            {
                seas._warnings.Add($"'{key}' is not a sea chapter ({string.Join("/", Chapters)}), ignored");
                continue;
            }
            if (node is not JsonObject fields)
            {
                seas._warnings.Add($"{chapter} is not an object, it takes the default sea");
                continue;
            }
            seas._entries[chapter] = ReadEntry(chapter, fields, seas._warnings);
        }

        return seas;
    }

    /// <summary>The file's text with <paramref name="chapter"/>'s entry replaced by the fields of
    /// <paramref name="sea"/> that differ from the defaults, in <see cref="SeaState.Fields"/> order.
    /// Every other key keeps its place and value; a chapter the file lacks is appended.</summary>
    public static string WithEntry(string? existing, string chapter, SeaState sea)
    {
        string name = Canonical(chapter) ?? throw new ArgumentException($"'{chapter}' is not a sea chapter", nameof(chapter));
        JsonObject root;
        try
        {
            root = existing is { Length: > 0 } text && JsonNode.Parse(text) is JsonObject parsed ? parsed : new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        var entry = new JsonObject();
        var clamped = sea.Clamped();
        foreach (var f in SeaState.Fields)
        {
            if (clamped.Differs(f))
                entry[f.Key] = JsonValue.Create(SeaState.Field.Rounded(f.Get(clamped)));
        }

        // Rebuilt key by key, since a JsonObject cannot replace a member in its place.
        var rebuilt = new JsonObject();
        bool placed = false;
        foreach (var (key, node) in root)
        {
            if (Canonical(key) == name)
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
    /// creating it when absent, and returns how many fields the entry holds.</summary>
    public static int Save(string diskPath, string chapter, SeaState sea)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        File.WriteAllText(diskPath, WithEntry(existing, chapter, sea), new UTF8Encoding(false));
        int count = 0;
        var clamped = sea.Clamped();
        foreach (var f in SeaState.Fields)
            count += clamped.Differs(f) ? 1 : 0;
        return count;
    }

    /// <summary>The project's copy of the file on disk while the game runs from the source tree. Null in
    /// an exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>The chapter's saved sea, or the defaults when the file has no entry for it.</summary>
    public SeaState For(string chapter) =>
        Canonical(chapter) is { } name && _entries.TryGetValue(name, out var sea) ? sea : SeaState.Default;

    // The file's spelling of a sea chapter, or null for any other key.
    private static string? Canonical(string chapter)
    {
        foreach (var c in Chapters)
        {
            if (string.Equals(c, chapter, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static SeaState ReadEntry(string chapter, JsonObject fields, List<string> warnings)
    {
        var sea = SeaState.Default;
        foreach (var (key, node) in fields)
        {
            if (key.StartsWith('_'))
                continue;
            if (SeaState.Find(key) is not { } field)
            {
                warnings.Add($"{chapter}: '{key}' is not a sea field, ignored");
                continue;
            }
            if (node is not JsonValue value || !value.TryGetValue(out double number) || !double.IsFinite(number))
            {
                warnings.Add($"{chapter}: '{key}' is not a number, it takes the default");
                continue;
            }
            sea = field.With(sea, (float)number);
        }

        var clamped = sea.Clamped();
        foreach (var f in SeaState.Fields)
        {
            if (f.Get(clamped) != f.Get(sea))
                warnings.Add($"{chapter}: '{f.Key}' {f.Format(f.Get(sea))} is held at {f.Format(f.Get(clamped))}");
        }

        return clamped;
    }
}
