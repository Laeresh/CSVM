using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Utils;

/// <summary>The process-wide options: the requested menu presentation and the requested graphics
/// mode. A missing field means "never set"; the caller, not this def, decides what that falls back
/// to.</summary>
public sealed class OptionsDef
{
    public string? MenuPresentation { get; set; }

    public string? GraphicsMode { get; set; }
}

/// <summary>
/// JSON persistence for <see cref="OptionsDef"/>: one file, <c>user://options.json</c>, independent
/// of any campaign profile (<see cref="Session.CampaignProfileStore"/> is per-profile; this store is
/// process-wide). Plain System.IO over an absolute directory, like the profile and plane stores, so
/// it unit-tests without an engine; a missing or malformed file reads as an empty
/// <see cref="OptionsDef"/> rather than throwing. Unlike those stores, <see cref="Save"/> writes
/// atomically (temp file, then rename), so a process killed mid-write keeps the previous save.
/// </summary>
public sealed class OptionsStore
{
    /// <summary>The schema version written into every file. A file claiming a version this reader
    /// does not know is treated as malformed rather than half-read.
    /// ⚠ A field added to <see cref="OptionsDef"/> does not bump this: a missing field already
    /// reads as "never set", so a file written before the field existed loads with every other
    /// field intact. Bump it only when an existing field changes meaning or shape, which is the
    /// one case a reader cannot recover from by reading what is there.</summary>
    public const int Version = 1;

    private const string FileName = "options.json";
    private const string TempFileName = "options.json.tmp";

    // The only presentation names this option currently accepts. Kept as strings, not an enum: the
    // presentation identity type belongs to whichever module owns the presentation contract.
    private static readonly HashSet<string> ValidPresentations = new(StringComparer.Ordinal)
    {
        PresentationResolution.BuiltIn,
        "original",
    };

    // The only graphics words this option accepts, the pair GraphicsMode.TryParse knows. Kept as
    // strings for the same reason the presentations are: the resolved value is a boolean owned by
    // the module that reads it, not a type this store carries.
    private static readonly HashSet<string> ValidGraphicsModes = new(StringComparer.Ordinal)
    {
        GraphicsMode.Default,
        GraphicsMode.EnhancedWord,
    };

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly string _dir;

    /// <summary>A store over <paramref name="directory"/>, which must be absolute (a relative path
    /// would resolve against whatever the process's working directory happens to be).</summary>
    public OptionsStore(string directory)
    {
        if (!Path.IsPathRooted(directory))
        {
            throw new ArgumentException($"options store needs an absolute directory, got '{directory}'");
        }

        _dir = directory;
    }

    /// <summary>Where <see cref="UserOptions"/> reads and writes instead of <c>user://</c> while
    /// set. The in-engine suites point it at an emptied scratch directory before any suite runs,
    /// so a suite that drives an Options screen opens it on the shipped defaults and its Apply
    /// lands in scratch: the player's own options.json is one machine's state, and a suite that
    /// read it would pass or fail with the last choice saved at the controls.</summary>
    public static string? DirectoryOverride { get; set; }

    /// <summary>The production store, <c>user://</c> resolved to its OS path, or the
    /// <see cref="DirectoryOverride"/> while one is set.</summary>
    public static OptionsStore UserOptions() => new(DirectoryOverride ?? Godot.ProjectSettings.GlobalizePath("user://"));

    /// <summary>The canonical JSON text for <paramref name="def"/>.</summary>
    public static string Serialize(OptionsDef def)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            Write(w, "menuPresentation", def.MenuPresentation);
            Write(w, "graphicsMode", def.GraphicsMode);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The def a JSON text describes, or null when the text is not valid JSON, not an
    /// object, or not <see cref="Version"/>. An unknown value in any field is dropped like a
    /// missing one rather than invalidating the whole file, and a field the file does not carry
    /// at all reads as never set, so a file written before a field existed still loads. Only the
    /// version gate rejects a whole file.</summary>
    public static OptionsDef? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int version = root.TryGetProperty("version", out var v)
                && v.ValueKind == JsonValueKind.Number
                && v.TryGetInt32(out int value)
                    ? value
                    : -1;
            if (root.ValueKind != JsonValueKind.Object || version != Version)
            {
                return null;
            }

            return new OptionsDef
            {
                MenuPresentation = Read(root, "menuPresentation", ValidPresentations),
                GraphicsMode = Read(root, "graphicsMode", ValidGraphicsModes),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The stored options, or an empty <see cref="OptionsDef"/> when the file is absent,
    /// unreadable or malformed. A read failure is never the caller's problem to handle.</summary>
    public OptionsDef Load()
    {
        try
        {
            var path = Path.Combine(_dir, FileName);
            return File.Exists(path) ? Deserialize(File.ReadAllText(path)) ?? new OptionsDef() : new OptionsDef();
        }
        catch (IOException)
        {
            return new OptionsDef();
        }
        catch (UnauthorizedAccessException)
        {
            return new OptionsDef();
        }
    }

    /// <summary>Writes <paramref name="def"/> atomically: the JSON lands in a sibling temp file
    /// first, then a rename replaces the real file in one filesystem operation, so a run that dies
    /// mid-write leaves the previous save (or, on a first save, no file at all) rather than a
    /// half-written one.</summary>
    public void Save(OptionsDef def)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, FileName);
        var temp = Path.Combine(_dir, TempFileName);
        File.WriteAllText(temp, Serialize(def), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    // A never-set field is written as an explicit null rather than left out, so the file names
    // every option the build knows and a reader can tell "not set" from "written by an older
    // build" by eye.
    private static void Write(Utf8JsonWriter w, string name, string? value)
    {
        if (value is { } set)
        {
            w.WriteString(name, set);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    private static string? Read(JsonElement root, string name, HashSet<string> valid) =>
        root.TryGetProperty(name, out var field)
        && field.ValueKind == JsonValueKind.String
        && valid.Contains(field.GetString() ?? string.Empty)
            ? field.GetString()
            : null;
}
