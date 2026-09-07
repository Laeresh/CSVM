using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>One resolved window size: the pixels, the word that names it, and the source that won,
/// named so a log line can say which layer the run is obeying.</summary>
public readonly record struct ResolutionPlan(int Width, int Height, string Word, string Source);

/// <summary>
/// The window's size. The words are sizes in <see cref="OptionsStore.FormatResolution"/>'s spelling
/// rather than a vocabulary, and the list a picker offers is built per screen by
/// <see cref="Sizes"/>, so the row cannot offer a size the monitor cannot hold. The sources layer
/// as <see cref="DisplayModeSetting"/>'s do: the saved size, then the size <c>project.godot</c>
/// ships. <see cref="Apply"/> is the only place <see cref="DisplayServer.WindowSetSize"/> is called.
/// ⚠ Godot exposes no video-mode list, only the screen's own size
/// (<see cref="DisplayServer.ScreenGetSize"/>), so "what the monitor offers" is that size and the
/// standard sizes that fit inside it, not a list the driver handed us.
/// </summary>
public static class ResolutionSetting
{
    /// <summary>The width a launch with no saved size runs at, <c>project.godot</c>'s own.</summary>
    public const int DefaultWidth = 1280;

    /// <summary>The height a launch with no saved size runs at, <c>project.godot</c>'s own.</summary>
    public const int DefaultHeight = 720;

    /// <summary>The size a launch with no usable saved one runs at, and the size an unsupported
    /// saved one falls back to. Every list <see cref="Sizes"/> builds holds it.</summary>
    public static readonly string Default = OptionsStore.FormatResolution(DefaultWidth, DefaultHeight);

    // The sizes a picker offers where the screen holds them, ascending, one entry per shape a
    // desktop monitor is actually sold in: 4:3, 16:9, 16:10, 21:9 and 32:9. A ceiling filter over
    // this is the whole of the per-monitor list, Godot having no mode enumeration to ask.
    private static readonly (int Width, int Height)[] Standard =
    {
        (1024, 768), (1280, 720), (1280, 800), (1600, 900), (1680, 1050), (1920, 1080), (1920, 1200),
        (2560, 1080), (2560, 1440), (3440, 1440), (3840, 2160), (5120, 1440),
    };

    private static readonly IReadOnlyList<string> UnfilteredWords = Format(Standard);

    /// <summary>Every candidate size unfiltered, what a caller with no screen to ask can offer.
    /// A shell composed without an engine (an engine-free test) is the only such caller; a run at
    /// the controls goes through <see cref="Sizes"/> and gets the screen's own list.</summary>
    public static IReadOnlyList<string> AllSizes => UnfilteredWords;

    /// <summary>The sizes <paramref name="screen"/> can hold, ascending: the standard ones that fit
    /// inside it, plus the screen's own size and <see cref="Default"/>, both always offerable. A
    /// screen the engine reports no size for falls back to the unfiltered list.</summary>
    public static IReadOnlyList<string> Sizes(int screen)
    {
        var size = DisplayServer.ScreenGetSize(screen);
        return SizesUnder(size.X, size.Y);
    }

    /// <summary>The sizes the window's own screen can hold, which is the screen a monitor setting
    /// has already moved it to.</summary>
    public static IReadOnlyList<string> ScreenSizes() => Sizes(DisplayServer.WindowGetCurrentScreen());

    /// <summary>The sizes a screen <paramref name="screenWidth"/> by <paramref name="screenHeight"/>
    /// can hold, ascending. The engine-free half of <see cref="Sizes"/>, so the filter and the
    /// always-offerable pair are testable without a screen.</summary>
    public static IReadOnlyList<string> SizesUnder(int screenWidth, int screenHeight)
    {
        if (screenWidth < 1 || screenHeight < 1)
        {
            return AllSizes;
        }

        var fits = new List<(int Width, int Height)>();
        foreach (var candidate in Standard)
        {
            if (candidate.Width <= screenWidth && candidate.Height <= screenHeight)
            {
                fits.Add(candidate);
            }
        }

        Include(fits, (screenWidth, screenHeight));
        Include(fits, (DefaultWidth, DefaultHeight));
        fits.Sort(static (a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
        return Format(fits);
    }

    /// <summary>The size the two sources resolve to: <paramref name="savedWord"/> where
    /// <paramref name="offered"/> holds it, else <see cref="Default"/>.
    /// ⚠ A saved size the screen does not offer falls back to the project default, never to the
    /// nearest offered one. Every other option here falls through to its own default when the saved
    /// value is one the reader does not know, and a nearest match needs a distance over sizes with
    /// no right answer. docs/architecture/Utils.md has the rest of the reasoning.</summary>
    public static ResolutionPlan Resolve(string? savedWord, IReadOnlyList<string> offered)
    {
        if (savedWord != null && Offers(offered, savedWord)
            && OptionsStore.TryParseResolution(savedWord, out int width, out int height))
        {
            return new ResolutionPlan(width, height, savedWord, "options.json");
        }

        return new ResolutionPlan(DefaultWidth, DefaultHeight, Default, "default");
    }

    /// <summary>The saved size a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting: options.json is one machine's state
    /// and a golden shot is a <c>--det</c> run against the player's own options directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().Resolution;

    /// <summary>Applies <paramref name="plan"/> to the window and logs the source that won. The
    /// engine call is skipped when the window already stands at that size and while it is not
    /// windowed, a fullscreen window's size being the screen's; the line is logged either way, since
    /// "obeying the saved size" and "the mode owns the size" are both facts a run's log owes its
    /// reader.</summary>
    public static void Apply(ResolutionPlan plan)
    {
        var mode = DisplayServer.WindowGetMode();
        var current = DisplayServer.WindowGetSize();
        bool changed = mode == DisplayServer.WindowMode.Windowed
            && (current.X != plan.Width || current.Y != plan.Height);
        if (changed)
        {
            DisplayServer.WindowSetSize(new Vector2I(plan.Width, plan.Height));
            Centre();
        }

        Log.Info("core", $"resolution={plan.Word} source={plan.Source} mode={mode} changed={changed}");
    }

    // A resize grows from the window's top-left corner, so a size the player picked larger than the
    // one standing would push the bottom-right off the screen and take the plaques they just pressed
    // with it. Centred on the screen the window already stands on, which is the screen a monitor
    // setting moved it to before this ran.
    private static void Centre()
    {
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        DisplayServer.WindowSetPosition(usable.Position + ((usable.Size - DisplayServer.WindowGetSize()) / 2));
    }

    private static void Include(List<(int Width, int Height)> sizes, (int Width, int Height) size)
    {
        if (!sizes.Contains(size))
        {
            sizes.Add(size);
        }
    }

    private static bool Offers(IReadOnlyList<string> offered, string word)
    {
        for (int i = 0; i < offered.Count; i++)
        {
            if (offered[i] == word)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> Format(IReadOnlyList<(int Width, int Height)> sizes)
    {
        var words = new string[sizes.Count];
        for (int i = 0; i < sizes.Count; i++)
        {
            words[i] = OptionsStore.FormatResolution(sizes[i].Width, sizes[i].Height);
        }

        return words;
    }
}
