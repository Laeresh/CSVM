using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Utils;

/// <summary>The display settings' vocabularies: the words <see cref="OptionsDef.DisplayMode"/> and
/// <see cref="OptionsDef.VSync"/> accept, in the order a picker offers them. Kept as strings for
/// the reason the presentation and graphics words are: the resolved value is an argument to an
/// engine call, owned by whichever module makes the call, not a type this store carries. The other
/// two display settings have no word list, so <see cref="OptionsStore"/> validates their shape
/// instead.</summary>
public static class DisplayWords
{
    /// <summary>A bordered window, the mode the project ships in.</summary>
    public const string Windowed = "windowed";

    /// <summary>A borderless window filling the screen.</summary>
    public const string Borderless = "borderless";

    /// <summary>Exclusive fullscreen.</summary>
    public const string Fullscreen = "fullscreen";

    /// <summary>V-Sync on, which is the behaviour with no options file.</summary>
    public const string VSyncOn = "on";

    /// <summary>V-Sync off with no frame cap.</summary>
    public const string VSyncOff = "off";

    /// <summary>Every display-mode word.</summary>
    public static readonly IReadOnlyList<string> DisplayModes = new[] { Windowed, Borderless, Fullscreen };

    /// <summary>Every V-Sync word. A word that parses as an integer is a frame cap in frames per
    /// second and means V-Sync off at that cap; <see cref="VSyncOn"/> and <see cref="VSyncOff"/>
    /// are the two that do not parse, so one field carries both the choice and the cap.</summary>
    public static readonly IReadOnlyList<string> VSyncChoices = new[] { VSyncOn, VSyncOff, "60", "120", "144" };
}

/// <summary>The process-wide options: the requested menu presentation, the requested graphics
/// mode, the difficulty setting and the four display settings (the monitor, the window size, the
/// display mode and the V-Sync choice). A missing field means "never set"; the caller, not this
/// def, decides what that falls back to.
/// ⚠ A display field added here is read through a <c>SavedWord(bool det)</c> reader and nowhere
/// else, and that reader returns null under <c>--det</c>: a golden shot is a deterministic run
/// against the player's own options directory, so a saved size or mode that escaped the drop would
/// move every golden in the repo. The <c>display-det-guard</c> suite compares this def against the
/// readers by reflection and fails on a field that has none.</summary>
public sealed class OptionsDef
{
    public string? MenuPresentation { get; set; }

    public string? GraphicsMode { get; set; }

    /// <summary>The difficulty word (<see cref="Flight.Difficulty.Word"/>): the campaign
    /// selector's tier the launch reads when no flag names one.</summary>
    public string? Difficulty { get; set; }

    /// <summary>The screen the window opens on, its index rendered decimal (<c>"0"</c>,
    /// <c>"1"</c>). This is not a vocabulary word, so the store proves the shape alone: whether a
    /// screen with that index is plugged in can only be answered by a caller holding an engine,
    /// and that caller owns the fallback to the primary screen.</summary>
    public string? MonitorIndex { get; set; }

    /// <summary>The window size in the canonical <c>"1920x1080"</c> form
    /// (<see cref="OptionsStore.FormatResolution"/>). Shape-validated like
    /// <see cref="MonitorIndex"/>: whether the chosen screen offers the mode is the applying
    /// caller's question, not this def's.</summary>
    public string? Resolution { get; set; }

    /// <summary>The window's display mode, one of <see cref="DisplayWords.DisplayModes"/>.</summary>
    public string? DisplayMode { get; set; }

    /// <summary>The frame pacing, one of <see cref="DisplayWords.VSyncChoices"/>: V-Sync on, off,
    /// or off with the frame cap the word names.</summary>
    public string? VSync { get; set; }
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

    /// <summary>The largest width or height a saved resolution may name. A ceiling, not a mode
    /// list: it keeps a hand-edited file from asking for a window no screen could hold, while the
    /// modes a monitor actually offers stay the applying caller's question.</summary>
    public const int MaxDimension = 32767;

    /// <summary>The largest screen index a saved monitor may name, for the same reason
    /// <see cref="MaxDimension"/> exists. A shaped index can still name a screen that is not
    /// plugged in.</summary>
    public const int MaxMonitorIndex = 63;

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

    // The three campaign words alone, spelt as Flight.Difficulty.Word spells them. Parse's wider
    // vocabulary (Instant Action's names, bare digits) is a command line's convenience, not a
    // value this file should ever carry: a file reads back only what the options screens write.
    private static readonly HashSet<string> ValidDifficulties = new(StringComparer.Ordinal)
    {
        Flight.Difficulty.Word(Flight.Difficulty.Normal),
        Flight.Difficulty.Word(Flight.Difficulty.Hard),
        Flight.Difficulty.Word(Flight.Difficulty.Hardest),
    };

    // The two display vocabularies, DisplayWords' own lists as sets. Held here rather than there
    // for the same reason the three above are held here at all: the words a file may carry are
    // this reader's business, and a file reads back only what an options screen writes.
    private static readonly HashSet<string> ValidDisplayModes = new(DisplayWords.DisplayModes, StringComparer.Ordinal);

    private static readonly HashSet<string> ValidVSyncChoices = new(DisplayWords.VSyncChoices, StringComparer.Ordinal);

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
            Write(w, "difficulty", def.Difficulty);
            Write(w, "monitorIndex", def.MonitorIndex);
            Write(w, "resolution", def.Resolution);
            Write(w, "displayMode", def.DisplayMode);
            Write(w, "vsync", def.VSync);
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
                Difficulty = Read(root, "difficulty", ValidDifficulties),
                MonitorIndex = ReadShaped(root, "monitorIndex", static v => TryParseMonitorIndex(v, out _)),
                Resolution = ReadShaped(root, "resolution", static v => TryParseResolution(v, out _, out _)),
                DisplayMode = Read(root, "displayMode", ValidDisplayModes),
                VSync = Read(root, "vsync", ValidVSyncChoices),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The canonical text for a window size, the one spelling
    /// <see cref="OptionsDef.Resolution"/> carries and <see cref="TryParseResolution"/> reads
    /// back. Callers that build a resolution go through this rather than composing the string, so
    /// the writing side and the validating side cannot drift apart.</summary>
    public static string FormatResolution(int width, int height) =>
        width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);

    /// <summary>The width and height a resolution string names, false when it is not the canonical
    /// shape: two decimal numbers from 1 to <see cref="MaxDimension"/> around one <c>x</c>. False
    /// is what makes <see cref="Deserialize"/> drop the field, so a hand-edited size that is not a
    /// size reads as never set instead of reaching a window.</summary>
    public static bool TryParseResolution(string? value, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (value == null)
        {
            return false;
        }

        int at = value.IndexOf('x', StringComparison.Ordinal);
        return at > 0
            && TryParseCanonical(value.AsSpan(0, at), 1, MaxDimension, out width)
            && TryParseCanonical(value.AsSpan(at + 1), 1, MaxDimension, out height);
    }

    /// <summary>The screen index a monitor string names, false when it is not a plain decimal
    /// index from 0 to <see cref="MaxMonitorIndex"/>. Shape only: a well-shaped index can still
    /// name a screen that is not plugged in, and only a caller holding an engine can tell.</summary>
    public static bool TryParseMonitorIndex(string? value, out int index)
    {
        index = 0;
        return value != null && TryParseCanonical(value.AsSpan(), 0, MaxMonitorIndex, out index);
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

    // Digits alone in range, and no leading zero past a single digit: NumberStyles.None already
    // refuses a sign, whitespace and separators, and refusing "01" as well leaves a value with one
    // spelling, so two files that ask for the same screen or size read the same by eye.
    private static bool TryParseCanonical(ReadOnlySpan<char> text, int min, int max, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 5 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= min && value <= max;
    }

    private static string? Read(JsonElement root, string name, HashSet<string> valid) =>
        root.TryGetProperty(name, out var field)
        && field.ValueKind == JsonValueKind.String
        && valid.Contains(field.GetString() ?? string.Empty)
            ? field.GetString()
            : null;

    // The shape-validated read, the same drop-an-unknown-value contract as the vocabulary read
    // above with a predicate in place of the set: a resolution and a monitor index have no word
    // list to belong to, so what stands in for membership is the canonical form parsing back.
    private static string? ReadShaped(JsonElement root, string name, Func<string, bool> valid) =>
        root.TryGetProperty(name, out var field)
        && field.ValueKind == JsonValueKind.String
        && valid(field.GetString() ?? string.Empty)
            ? field.GetString()
            : null;
}
