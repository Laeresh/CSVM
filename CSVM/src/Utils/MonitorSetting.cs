using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace CSVM.Utils;

/// <summary>One resolved screen: the index the window stands on, the word that names it, and the
/// source that won, named so a log line can say which layer the run is obeying.</summary>
public readonly record struct MonitorPlan(int Screen, string Word, string Source);

/// <summary>The screens a monitor picker can offer: one label per screen in index order, so the
/// label's position is the screen it names, and the screen an index that names none falls back
/// to.</summary>
public readonly record struct ScreenList(IReadOnlyList<string> Labels, int Fallback);

/// <summary>
/// The screen the window sits on. The saved value is that screen's index rendered decimal
/// (<see cref="OptionsStore.TryParseMonitorIndex"/>) and the sources layer as
/// <see cref="DisplayModeSetting"/>'s do: the saved index, then the fallback screen.
/// <see cref="Apply"/> is the only place <see cref="DisplayServer.WindowSetCurrentScreen"/> is
/// called, and both of <c>Launcher</c>'s call sites make it before the mode and the size, a mode
/// applied first having filled the screen the window is leaving.
/// ⚠ This is the one display setting whose saved value can name something that is not there. The
/// store proves the shape and only a caller holding an engine can count the screens, so
/// <see cref="Resolve"/> dropping an index no screen answers to is the feature, not an error path.
/// </summary>
public static class MonitorSetting
{
    /// <summary>The screens a caller with no engine to ask can offer: one, unnamed by size. A shell
    /// composed without an engine (an engine-free test) is the only such caller; a run at the
    /// controls goes through <see cref="Screens"/> and gets the machine's own.</summary>
    public static readonly ScreenList Unknown = new(new[] { Label(0, 0, 0) }, 0);

    /// <summary>Every screen the engine reports, labelled by index and size, falling back to the
    /// screen the window already stands on, which on a launch that has moved nothing is the primary.
    /// Taking the standing screen rather than the primary outright means a saved index naming an
    /// unplugged monitor leaves the window where the player is looking instead of moving it.</summary>
    public static ScreenList Screens()
    {
        int count = Math.Max(1, DisplayServer.GetScreenCount());
        var labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            var size = DisplayServer.ScreenGetSize(i);
            labels[i] = Label(i, size.X, size.Y);
        }

        return new ScreenList(labels, DisplayServer.WindowGetCurrentScreen());
    }

    /// <summary>How one screen reads on a page: the index it is saved as and the size the engine
    /// reports for it, or the index alone where there is no size to name. The index is the engine's
    /// own and counts from zero, so the page, the options file and the log line all name a screen
    /// the same way.</summary>
    public static string Label(int index, int width, int height)
    {
        string at = "Screen " + Word(index);
        return width > 0 && height > 0 ? at + " (" + OptionsStore.FormatResolution(width, height) + ")" : at;
    }

    /// <summary>The word a screen index is saved as, the one spelling
    /// <see cref="OptionsStore.TryParseMonitorIndex"/> reads back.</summary>
    public static string Word(int screen) => screen.ToString(CultureInfo.InvariantCulture);

    /// <summary>The screen the two sources resolve to: <paramref name="savedWord"/> where
    /// <paramref name="screens"/> has one at that index, else that list's fallback. A shaped index
    /// past the last screen is dropped exactly as an unknown word is, since a file that named a
    /// monitor which is no longer plugged in is not a file to refuse.</summary>
    public static MonitorPlan Resolve(string? savedWord, ScreenList screens)
    {
        if (OptionsStore.TryParseMonitorIndex(savedWord, out int index) && index < screens.Labels.Count)
        {
            return new MonitorPlan(index, Word(index), "options.json");
        }

        int fallback = screens.Fallback > 0 && screens.Fallback < screens.Labels.Count ? screens.Fallback : 0;
        return new MonitorPlan(fallback, Word(fallback), "default");
    }

    /// <summary>The saved index a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting: options.json is one machine's state
    /// and a golden shot is a <c>--det</c> run against the player's own options directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().MonitorIndex;

    /// <summary>Applies <paramref name="plan"/> to the window and logs the source that won. The
    /// engine call is skipped when the window already stands on that screen; the line is logged
    /// either way, since "obeying the saved screen" and "already there" are both facts a run's log
    /// owes its reader. The mode and the size are applied after this, never before.</summary>
    public static void Apply(MonitorPlan plan)
    {
        bool changed = DisplayServer.WindowGetCurrentScreen() != plan.Screen;
        if (changed)
        {
            DisplayServer.WindowSetCurrentScreen(plan.Screen);
        }

        Log.Info("core", $"monitor={plan.Word} source={plan.Source} screens={DisplayServer.GetScreenCount()} changed={changed}");
    }
}
