using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Utils;

/// <summary>One process-wide option: today, only the requested menu presentation. A missing field
/// means "never set"; the caller, not this def, decides what that falls back to.</summary>
public sealed class OptionsDef
{
    public string? MenuPresentation { get; set; }
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
    /// does not know is treated as malformed rather than half-read.</summary>
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

    /// <summary>The production store, <c>user://</c> resolved to its OS path.</summary>
    public static OptionsStore UserOptions() => new(Godot.ProjectSettings.GlobalizePath("user://"));

    /// <summary>The canonical JSON text for <paramref name="def"/>.</summary>
    public static string Serialize(OptionsDef def)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            if (def.MenuPresentation is { } presentation)
            {
                w.WriteString("menuPresentation", presentation);
            }
            else
            {
                w.WriteNull("menuPresentation");
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The def a JSON text describes, or null when the text is not valid JSON, not an
    /// object, or not <see cref="Version"/>. An unknown <c>menuPresentation</c> value is dropped
    /// like a missing one rather than invalidating the whole file — only the version gate does
    /// that.</summary>
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

            string? presentation = root.TryGetProperty("menuPresentation", out var p)
                && p.ValueKind == JsonValueKind.String
                && ValidPresentations.Contains(p.GetString() ?? string.Empty)
                    ? p.GetString()
                    : null;

            return new OptionsDef { MenuPresentation = presentation };
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
}
