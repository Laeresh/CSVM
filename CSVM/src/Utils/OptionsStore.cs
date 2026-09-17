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
/// mode, the difficulty setting, the nearest-after-a-kill targeting setting, the opening view, the
/// automatic head turn, the four display settings (the monitor, the window size, the display mode
/// and the V-Sync choice) and the four volume levels. A missing field means "never set"; the
/// caller, not this def, decides what that falls back to.
/// ⚠ A display field added here is read through a <c>SavedWord(bool det)</c> reader and nowhere
/// else, and that reader returns null under <c>--det</c>: a golden shot is a deterministic run
/// against the player's own options directory, so a saved size or mode that escaped the drop would
/// move every golden in the repo. The <c>display-det-guard</c> suite compares this def against the
/// readers by reflection and fails on a field that has none. The four levels take the same drop
/// through <see cref="AudioMix.SavedLevels"/>, which is their one reader.</summary>
public sealed class OptionsDef
{
    public string? MenuPresentation { get; set; }

    public string? GraphicsMode { get; set; }

    /// <summary>The difficulty word (<see cref="Flight.Difficulty.Word"/>): the campaign
    /// selector's tier the launch reads when no flag names one.</summary>
    public string? Difficulty { get; set; }

    /// <summary>Whether a target that dies is replaced by the nearest live member of the current
    /// cycle instead of by the cycle's head (<see cref="Flight.TargetSelection.NearestAfterKill"/>).
    /// ⚠ Nullable because null is "never set", which the consumer reads as off, the decoded rule.
    /// A plain <c>bool</c> would make a saved off indistinguishable from an absent field.</summary>
    public bool? NearestAfterKill { get; set; }

    /// <summary>Whether the gamepad rumbles for this pilot's flight events
    /// (<see cref="Bindings.PadRumble"/>).
    /// ⚠ Nullable for the same reason as the field above, but read the other way round: null is
    /// "never set", which the consumer reads as ON, since the original ships force feedback on.
    /// </summary>
    public bool? Rumble { get; set; }

    /// <summary>The view a flight opens in (<see cref="Flight.PilotView.Name"/>), the original's
    /// own Default View setting. Kept as a word rather than the enum for the reason the difficulty
    /// is: the file carries a spelling, and the mode it resolves to belongs to the flight.
    /// ⚠ A <c>--view=</c> on the command line outranks this, so a scripted capture that names a
    /// view is not silently overruled by whatever is saved at this machine's controls.</summary>
    public string? DefaultView { get; set; }

    /// <summary>Whether the pilot's head turns with the aircraft in the cockpit
    /// (<see cref="Flight.FlightController.AutoHeadTurn"/>), the original's Auto Head Turn box.
    /// ⚠ Nullable because null is "never set", which leaves the <c>headLook.autohead</c> config
    /// key deciding: the row exposes the existing key rather than replacing its default.</summary>
    public bool? AutoHeadTurn { get; set; }

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

    /// <summary>The Master level, the multiplier over the three category levels
    /// (<see cref="AudioMix"/>). No vocabulary and no spelling to parse, so the store proves the
    /// range alone.
    /// ⚠ Nullable because 0 is a player's mute and null is "never set", which takes the shipped
    /// default: a plain <c>int</c> would fold the two together and make a mute unsaveable.</summary>
    public int? AudioMaster { get; set; }

    /// <summary>The Music level, <see cref="AudioMix.MinLevel"/> to
    /// <see cref="AudioMix.MaxLevel"/>, null where never set.</summary>
    public int? AudioMusic { get; set; }

    /// <summary>The Effects level, on the same range as <see cref="AudioMusic"/>.</summary>
    public int? AudioEffects { get; set; }

    /// <summary>The Voice level, on the same range as <see cref="AudioMusic"/>.</summary>
    public int? AudioVoice { get; set; }
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

    // The three selectable views, spelt as Flight.PilotView.Name spells them and so as --view=
    // takes them. Parse's other arguments (a numpad digit, "back") name a momentary look rather
    // than a selection, so they are not words this file may carry.
    private static readonly HashSet<string> ValidDefaultViews = new(StringComparer.Ordinal)
    {
        Flight.PilotView.Name(Flight.PilotViewMode.Chase),
        Flight.PilotView.Name(Flight.PilotViewMode.Cockpit),
        Flight.PilotView.Name(Flight.PilotViewMode.Nose),
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
            WriteFlag(w, "nearestAfterKill", def.NearestAfterKill);
            WriteFlag(w, "rumble", def.Rumble);
            Write(w, "defaultView", def.DefaultView);
            WriteFlag(w, "autoHeadTurn", def.AutoHeadTurn);
            Write(w, "monitorIndex", def.MonitorIndex);
            Write(w, "resolution", def.Resolution);
            Write(w, "displayMode", def.DisplayMode);
            Write(w, "vsync", def.VSync);
            WriteLevel(w, "audioMaster", def.AudioMaster);
            WriteLevel(w, "audioMusic", def.AudioMusic);
            WriteLevel(w, "audioEffects", def.AudioEffects);
            WriteLevel(w, "audioVoice", def.AudioVoice);
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
                NearestAfterKill = ReadFlag(root, "nearestAfterKill"),
                Rumble = ReadFlag(root, "rumble"),
                DefaultView = Read(root, "defaultView", ValidDefaultViews),
                AutoHeadTurn = ReadFlag(root, "autoHeadTurn"),
                MonitorIndex = ReadShaped(root, "monitorIndex", static v => TryParseMonitorIndex(v, out _)),
                Resolution = ReadShaped(root, "resolution", static v => TryParseResolution(v, out _, out _)),
                DisplayMode = Read(root, "displayMode", ValidDisplayModes),
                VSync = Read(root, "vsync", ValidVSyncChoices),
                AudioMaster = ReadLevel(root, "audioMaster"),
                AudioMusic = ReadLevel(root, "audioMusic"),
                AudioEffects = ReadLevel(root, "audioEffects"),
                AudioVoice = ReadLevel(root, "audioVoice"),
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

    // The numeric sibling of Write above, and null for the same reason: a level the build knows but
    // the player has never set is named in the file rather than left out of it.
    private static void WriteLevel(Utf8JsonWriter w, string name, int? value)
    {
        if (value is { } set)
        {
            w.WriteNumber(name, set);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    // The boolean sibling of the two writers above, and null for the same reason: a switch the
    // build knows but the player has never touched is named in the file rather than left out of it.
    private static void WriteFlag(Utf8JsonWriter w, string name, bool? value)
    {
        if (value is { } set)
        {
            w.WriteBoolean(name, set);
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

    // The numeric read, the same drop-an-unknown-value contract as the reads either side of it with
    // a range in place of the set: a level has no word list and no spelling, so what stands in for
    // membership is the range the sliders take. A value out of range, of another JSON kind or not a
    // whole number reads as never set, which keeps a hand-edited level from taking the file down
    // with it.
    private static int? ReadLevel(JsonElement root, string name) =>
        root.TryGetProperty(name, out var field)
        && field.ValueKind == JsonValueKind.Number
        && field.TryGetInt32(out int level)
        && level >= AudioMix.MinLevel && level <= AudioMix.MaxLevel
            ? level
            : null;

    // The boolean read, the same drop-an-unknown-value contract as the reads either side of it: a
    // switch has neither a vocabulary nor a range, so what stands in for membership is the JSON kind
    // alone, and anything else (a quoted "true", a number) reads as never set.
    private static bool? ReadFlag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var field) && field.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? field.GetBoolean()
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
