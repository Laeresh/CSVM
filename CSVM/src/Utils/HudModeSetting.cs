using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>How much flight HUD an outside view draws. The cockpit view ignores it.</summary>
public enum HudMode
{
    /// <summary>The original's HUD, unchanged.</summary>
    Full,

    /// <summary>The dial cluster, compass tape and throttle plate replaced by the flight text block.</summary>
    Minimal,

    /// <summary>Only the pipper, the lead marker, messages, prompts and scores.</summary>
    None,
}

/// <summary>
/// The player's HUD mode as a word, <c>full</c>, <c>minimal</c> or <c>none</c>, saved as
/// <c>hudMode</c> in the options file. A remake-only setting: the saved word beats Full, and no
/// deterministic run reads the file. A launch reads the saved word into <see cref="Current"/> (none
/// under <c>--det</c>), and an options apply re-reads it except under <c>--det</c>. The flight's
/// cycle key steps and saves it. Each pilot pane follows it every frame
/// (<c>Flight.Hud.FlightHud.SetHudMode</c>). docs/architecture/Utils.md.
/// </summary>
public static class HudModeSetting
{
    /// <summary>The word a launch with nothing saved runs, the original's HUD.</summary>
    public const string Default = "full";

    /// <summary>The words in cycle order, which is also the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "full", "minimal", "none" };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read and no flag beats it.</summary>
    public static readonly WordSetting Lookup = new("hud.mode", Words);

    private static readonly string[] LabelWords = { "Full", "Minimal", "None" };

    /// <summary>The mode the running process draws its outside views in, Full until a launch, an
    /// apply or the cycle key stores another.</summary>
    public static HudMode Current { get; set; } = HudMode.Full;

    /// <summary>The mode <paramref name="word"/> spells, Full for a null word or one outside
    /// <see cref="Words"/>.</summary>
    public static HudMode ModeOf(string? word)
    {
        int at = Lookup.IndexOf(word);
        return at >= 0 ? (HudMode)at : HudMode.Full;
    }

    /// <summary>The saved word for <paramref name="mode"/>.</summary>
    public static string WordOf(HudMode mode) => Words[(int)mode];

    /// <summary>The mode after <paramref name="mode"/>: Full, Minimal, None, then Full again.</summary>
    public static HudMode Next(HudMode mode) => (HudMode)(((int)mode + 1) % Words.Count);

    /// <summary>Whether the markers draw the lead marker alone: None, outside the cockpit view. The
    /// one rule the flat targeting HUD and a stereo session's world markers both follow.</summary>
    public static bool LeadOnly(HudMode mode, bool cockpitView) => mode == HudMode.None && !cockpitView;

    /// <summary>The message-stack line one press of the cycle key posts, e.g. <c>HUD: Minimal</c>.</summary>
    public static string Line(HudMode mode) => "HUD: " + LabelWords[(int)mode];

    /// <summary>The word the sources resolve to: <paramref name="savedWord"/>, then
    /// <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? savedWord) => Lookup.Resolve(null, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (the <c>--det</c> rule <see cref="WordSettings.ReadSaved"/> keeps). It reads
    /// <paramref name="saved"/>, the options file read here when null.</summary>
    public static string? SavedWord(bool det, OptionsDef? saved) =>
        det ? null : (saved ?? OptionsStore.UserOptions().Load()).HudMode;

    /// <summary>The saved word read from the file. ⚠ Keep this one-argument shape: display-det-guard
    /// finds every SavedWord(bool) reader by reflection.</summary>
    public static string? SavedWord(bool det) => SavedWord(det, null);

    /// <summary>The launch's read: stores the saved word's mode as <see cref="Current"/> through
    /// <see cref="SavedWord"/>, so <paramref name="det"/> reads none, and logs it.</summary>
    public static void ApplySaved(bool det, OptionsDef? saved = null) => Apply(SavedWord(det, saved));

    /// <summary>An options apply's read: the saved word, as <see cref="ApplySaved"/>, after the file is
    /// written. Under <paramref name="det"/> it changes nothing, so an unrelated accept never
    /// overwrites the cycle key's steps. Returns whether it applied.</summary>
    public static bool ApplyAfterOptions(bool det, OptionsDef? saved = null)
    {
        if (det)
        {
            return false;
        }

        ApplySaved(det: false, saved);
        return true;
    }

    /// <summary>Stores <paramref name="word"/>'s mode as <see cref="Current"/> and logs it.</summary>
    public static void Apply(string? word)
    {
        var resolved = Resolve(word);
        Current = ModeOf(resolved.Word);
        Log.Info("flight", $"hud mode={resolved.Word} source={Lookup.SourceName(resolved.Source)}");
    }

    /// <summary>Steps <see cref="Current"/> to the next mode and writes its word to
    /// <paramref name="store"/>, the one value the Options row also sets. A null store (a
    /// <c>--det</c> run) changes the running mode and writes nothing. Returns the new mode.</summary>
    public static HudMode Cycle(OptionsStore? store)
    {
        Current = Next(Current);
        if (store != null)
        {
            var options = store.Load();
            options.HudMode = WordOf(Current);
            store.Save(options);
        }

        return Current;
    }

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels() => LabelWords;
}
