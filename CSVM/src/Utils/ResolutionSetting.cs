using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>One resolved window size: the pixels, the word that names it, and the source that won,
/// named so a log line can say which layer the run is obeying.</summary>
public readonly record struct ResolutionPlan(int Width, int Height, string Word, string Source);

/// <summary>The sizes a resolution picker can offer for one screen, ascending, and the word a
/// saved size the list does not hold falls back to: the screen's own size, which the list always
/// holds.</summary>
public readonly record struct SizeList(IReadOnlyList<string> Words, string Fallback);

/// <summary>
/// The window's size. The words are sizes in <see cref="OptionsStore.FormatResolution"/>'s spelling
/// rather than a vocabulary, and the list a picker offers is built per screen by
/// <see cref="Sizes"/>, so the row cannot offer a size the monitor cannot hold. The sources layer
/// as <see cref="MonitorSetting"/>'s do: the saved size, then the screen's own size, which is what
/// the borderless default fills anyway. <see cref="Apply"/> is the only place
/// <see cref="DisplayServer.WindowSetSize"/> is called.
/// ⚠ Godot exposes no video-mode list, only the screen's own size
/// (<see cref="DisplayServer.ScreenGetSize"/>), so "what the monitor offers" is that size and the
/// standard sizes that fit inside it, not a list the driver handed us.
/// </summary>
public static class ResolutionSetting
{
    /// <summary>The width <c>project.godot</c> ships, the window a run that applies no size keeps.</summary>
    public const int ProjectWidth = 1280;

    /// <summary>The height <c>project.godot</c> ships, the window a run that applies no size keeps.</summary>
    public const int ProjectHeight = 720;

    /// <summary>The size <c>project.godot</c> ships as a word, and the fallback of a list built with
    /// no screen to ask (<see cref="Unknown"/>).</summary>
    public static readonly string ProjectSize = OptionsStore.FormatResolution(ProjectWidth, ProjectHeight);

    // The sizes a picker offers where the screen holds them, ascending, one entry per shape a
    // desktop monitor is actually sold in: 4:3, 16:9, 16:10, 21:9 and 32:9. A ceiling filter over
    // this is the whole of the per-monitor list, Godot having no mode enumeration to ask.
    private static readonly (int Width, int Height)[] Standard =
    {
        (1024, 768), (1280, 720), (1280, 800), (1600, 900), (1680, 1050), (1920, 1080), (1920, 1200),
        (2560, 1080), (2560, 1440), (3440, 1440), (3840, 2160), (5120, 1440),
    };

    // Initialised after Standard, which it is built from; static fields initialise in textual order.
    private static readonly SizeList UnfilteredList = new(Format(Standard), ProjectSize);

    /// <summary>Every candidate size unfiltered with the project size as the fallback, what a caller
    /// with no screen to ask can offer. A shell composed without an engine (an engine-free test) is
    /// the only such caller; a run at the controls goes through <see cref="Sizes"/> and gets the
    /// screen's own list.</summary>
    public static SizeList Unknown => UnfilteredList;

    /// <summary>The sizes <paramref name="screen"/> can hold, ascending: the standard ones that fit
    /// inside it plus the screen's own size, which is the fallback. A screen the engine reports no
    /// size for falls back to <see cref="Unknown"/>.</summary>
    public static SizeList Sizes(int screen)
    {
        var size = DisplayServer.ScreenGetSize(screen);
        return SizesUnder(size.X, size.Y);
    }

    /// <summary>The sizes the window's own screen can hold, which is the screen a monitor setting
    /// has already moved it to.</summary>
    public static SizeList ScreenSizes() => Sizes(DisplayServer.WindowGetCurrentScreen());

    /// <summary>The sizes a screen <paramref name="screenWidth"/> by <paramref name="screenHeight"/>
    /// can hold, ascending, with that screen's own size as the fallback. The engine-free half of
    /// <see cref="Sizes"/>, so the filter and the fallback are testable without a screen.</summary>
    public static SizeList SizesUnder(int screenWidth, int screenHeight)
    {
        if (screenWidth < 1 || screenHeight < 1)
        {
            return Unknown;
        }

        var fits = new List<(int Width, int Height)>();
        foreach (var candidate in Standard)
        {
            if (candidate.Width <= screenWidth && candidate.Height <= screenHeight)
            {
                fits.Add(candidate);
            }
        }

        if (!fits.Contains((screenWidth, screenHeight)))
        {
            fits.Add((screenWidth, screenHeight));
        }

        fits.Sort(static (a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
        return new SizeList(Format(fits), OptionsStore.FormatResolution(screenWidth, screenHeight));
    }

    /// <summary>The size the two sources resolve to: <paramref name="savedWord"/> where
    /// <paramref name="offered"/> holds it, else that list's fallback, the screen's own size.
    /// ⚠ A saved size the screen does not offer falls back to the screen's size, never to the
    /// nearest offered one. Every other option here falls through to its own default when the saved
    /// value is one the reader does not know, and a nearest match needs a distance over sizes with
    /// no right answer. docs/architecture/Utils.md has the rest of the reasoning.</summary>
    public static ResolutionPlan Resolve(string? savedWord, SizeList offered)
    {
        if (savedWord != null && Offers(offered.Words, savedWord)
            && OptionsStore.TryParseResolution(savedWord, out int width, out int height))
        {
            return new ResolutionPlan(width, height, savedWord, "options.json");
        }

        return OptionsStore.TryParseResolution(offered.Fallback, out int fallbackWidth, out int fallbackHeight)
            ? new ResolutionPlan(fallbackWidth, fallbackHeight, offered.Fallback, "default")
            : new ResolutionPlan(ProjectWidth, ProjectHeight, ProjectSize, "default");
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
