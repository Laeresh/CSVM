using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Where the voice subtitles show (<c>Flight.Hud.SubtitleLine</c>): off, low near the foot of the
/// pane, or high under the compass. A briefing's caption keeps its place over the map at either
/// height. The saved word beats <see cref="Default"/>, and no deterministic run reads the file. The
/// launch sets <see cref="Word"/> and so does every options apply, and the caption reads it every frame.
/// </summary>
public static class SubtitlesSetting
{
    /// <summary>No subtitles, in flight or in a briefing.</summary>
    public const string Off = "off";

    /// <summary>The flight caption near the foot of the pane.</summary>
    public const string Low = "low";

    /// <summary>The flight caption at the top of the pane.</summary>
    public const string High = "high";

    /// <summary>The word a launch with nothing saved runs.</summary>
    public const string Default = Low;

    /// <summary>The words, in the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Low, High };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read and no flag beats it.</summary>
    public static readonly WordSetting Lookup = new("hud.subtitles", Words);

    private static readonly string[] LabelWords = { "Off", "Low", "High" };

    /// <summary>The word the running process shows subtitles by, <see cref="Default"/> until a launch
    /// or an apply stores another.</summary>
    public static string Word { get; set; } = Default;

    /// <summary>The word the sources resolve to: <paramref name="savedWord"/>, then
    /// <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? savedWord) => Lookup.Resolve(null, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>. It reads
    /// <paramref name="saved"/>, the options file read here when null.</summary>
    public static string? SavedWord(bool det, OptionsDef? saved) =>
        det ? null : (saved ?? OptionsStore.UserOptions().Load()).Subtitles;

    /// <summary>The saved word read from the file. ⚠ Keep this one-argument shape: display-det-guard
    /// finds every SavedWord(bool) reader by reflection.</summary>
    public static string? SavedWord(bool det) => SavedWord(det, null);

    /// <summary>Stores the saved word as <see cref="Word"/> through <see cref="SavedWord"/>, so
    /// <paramref name="det"/> reads none, and logs it. The launch and every options apply call it.</summary>
    public static void ApplySaved(bool det, OptionsDef? saved = null)
    {
        var resolved = Resolve(SavedWord(det, saved));
        Word = resolved.Word;
        Log.Info("sound", $"subtitles={resolved.Word} source={Lookup.SourceName(resolved.Source)}");
    }

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels() => LabelWords;
}
