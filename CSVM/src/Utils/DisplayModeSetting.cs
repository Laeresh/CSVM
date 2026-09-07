using Godot;

namespace CSVM.Utils;

/// <summary>One resolved window mode: the engine mode, the word that names it, and the source that
/// won, named so a log line can say which layer the run is obeying.</summary>
public readonly record struct DisplayModePlan(DisplayServer.WindowMode Mode, string Word, string Source);

/// <summary>
/// The window's display mode: a bordered window, a borderless one filling the screen, or exclusive
/// fullscreen. The words are <see cref="DisplayWords.DisplayModes"/> and the sources layer the way
/// <see cref="VSyncSetting"/>'s do with one layer fewer, there being no config key: the saved word,
/// then windowed, which is what <c>project.godot</c> ships. <see cref="Apply"/> is the only place
/// <see cref="DisplayServer.WindowSetMode"/> is called.
/// ⚠ Nothing here touches focus. The startup path owns it (an interactive session asks for it once,
/// a scripted one hides the window), and a mode change that cleared <c>no_focus</c> again or asked
/// for the foreground would hand a scripted run's focus back. See docs/verification.md's SHELL-13.
/// </summary>
public static class DisplayModeSetting
{
    /// <summary>The mode a launch with no saved word runs in, the one <c>project.godot</c>'s
    /// windowed 1280x720 already gives it.</summary>
    public const string Default = DisplayWords.Windowed;

    /// <summary>The mode the two sources resolve to: <paramref name="savedWord"/> when the
    /// vocabulary knows it, else windowed. A word this vocabulary does not know reads as never set
    /// and falls through, the same contract <see cref="OptionsStore"/> validates the field
    /// under.</summary>
    public static DisplayModePlan Resolve(string? savedWord) =>
        TryParseWord(savedWord, out var mode)
            ? new DisplayModePlan(mode, savedWord!, "options.json")
            : new DisplayModePlan(DisplayServer.WindowMode.Windowed, Default, "default");

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting: options.json is one machine's state
    /// and a golden shot is a <c>--det</c> run against the player's own options directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().DisplayMode;

    /// <summary>Whether <paramref name="word"/> is one of <see cref="DisplayWords.DisplayModes"/>,
    /// and the engine mode it names. False leaves <paramref name="mode"/> meaningless, which is how
    /// a caller tells "not a word I know" from "windowed".
    /// ⚠ Godot's names invert the reading: <c>Fullscreen</c> is the borderless window filling the
    /// screen and <c>ExclusiveFullscreen</c> is the exclusive mode.</summary>
    public static bool TryParseWord(string? word, out DisplayServer.WindowMode mode)
    {
        switch (word)
        {
            case DisplayWords.Borderless:
                mode = DisplayServer.WindowMode.Fullscreen;
                return true;
            case DisplayWords.Fullscreen:
                mode = DisplayServer.WindowMode.ExclusiveFullscreen;
                return true;
            case DisplayWords.Windowed:
                mode = DisplayServer.WindowMode.Windowed;
                return true;
            default:
                mode = DisplayServer.WindowMode.Windowed;
                return false;
        }
    }

    /// <summary>Applies <paramref name="plan"/> to the window and logs the source that won. The
    /// engine call is skipped when the window already stands in that mode, so a launch that changes
    /// nothing leaves the window untouched; the line is logged either way, since "obeying the saved
    /// word" and "already there" are both facts a run's log owes its reader.</summary>
    public static void Apply(DisplayModePlan plan)
    {
        bool changed = DisplayServer.WindowGetMode() != plan.Mode;
        if (changed)
        {
            DisplayServer.WindowSetMode(plan.Mode);
        }

        Log.Info("core", $"display mode={plan.Word} source={plan.Source} changed={changed}");
    }
}
