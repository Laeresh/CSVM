using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>One resolved window size: the pixels, the word that names it, and the source that won,
/// named so a log line can say which layer the run is obeying.</summary>
public readonly record struct ResolutionPlan(int Width, int Height, string Word, string Source);

/// <summary>The sizes a resolution picker can offer for one screen, ascending, the word a saved
/// size the list does not hold falls back to (the screen's own size, which the list always holds),
/// and that screen's own pixels, the ceiling <see cref="Including"/> keeps a size under. A list
/// built with no screen to ask carries a zero ceiling, which filters nothing.</summary>
public readonly record struct SizeList(IReadOnlyList<string> Words, string Fallback, int ScreenWidth, int ScreenHeight)
{
    /// <summary>This list with <paramref name="word"/> standing as an entry of its own where it
    /// sorts, which is what lets a size written into <c>options.json</c> by hand be shown, picked
    /// and saved back rather than replaced by a listed one. A word the list already holds, one that
    /// is not a size, and one the screen cannot hold each leave the list as it is, so what the
    /// picker offers is still what the screen can hold.</summary>
    public SizeList Including(string? word)
    {
        if (word == null || !OptionsStore.TryParseResolution(word, out int width, out int height)
            || (ScreenWidth > 0 && (width > ScreenWidth || height > ScreenHeight)))
        {
            return this;
        }

        var widened = new List<string>(Words.Count + 1);
        bool placed = false;
        for (int i = 0; i < Words.Count; i++)
        {
            if (Words[i] == word)
            {
                return this;
            }

            if (!placed && OptionsStore.TryParseResolution(Words[i], out int listed, out int listedHeight)
                && (width < listed || (width == listed && height < listedHeight)))
            {
                widened.Add(word);
                placed = true;
            }

            widened.Add(Words[i]);
        }

        if (!placed)
        {
            widened.Add(word);
        }

        return this with { Words = widened };
    }
}

/// <summary>
/// The window's size, and which display modes honour it. The words are sizes in
/// <see cref="OptionsStore.FormatResolution"/>'s spelling rather than a vocabulary; a picker's list
/// is built per screen by <see cref="Sizes"/> and widened by <see cref="SizeList.Including"/> with
/// the size the options file names. The sources layer as <see cref="MonitorSetting"/>'s do, the
/// saved size then the screen's own, with the display mode over both: borderless pins the size to
/// the screen (<see cref="Pinned"/>) and exclusive fullscreen takes it as a render size, Godot's
/// window there owning its own. <see cref="Apply"/> is the only place
/// <see cref="DisplayServer.WindowSetSize"/> is called, and it runs after the mode. ⚠ Godot exposes
/// no video-mode list, only the screen's own size (<see cref="DisplayServer.ScreenGetSize"/>), so
/// "what the monitor offers" is that size and the standard sizes inside it, not the driver's list.
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

    // The sizes a picker offers where the screen holds them, ascending, over the shapes a desktop
    // monitor is sold in: 4:3, 16:9, 16:10, 21:9 and 32:9. The 4:3 ladder carries four rungs rather
    // than one because the original game's own frame is 4:3, so that shape is the one a player is
    // most likely to want a choice within. A ceiling filter over this is the whole of the
    // per-monitor list, Godot having no mode enumeration to ask, and a size the table lacks reaches
    // the picker through the options file (SizeList.Including).
    private static readonly (int Width, int Height)[] Standard =
    {
        (800, 600), (1024, 768), (1280, 720), (1280, 800), (1280, 960), (1600, 900), (1600, 1200),
        (1680, 1050), (1920, 1080), (1920, 1200), (2560, 1080), (2560, 1440), (3440, 1440),
        (3840, 2160), (5120, 1440),
    };

    // Initialised after Standard, which it is built from; static fields initialise in textual order.
    private static readonly SizeList UnfilteredList = new(Format(Standard), ProjectSize, 0, 0);

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
        return new SizeList(
            Format(fits), OptionsStore.FormatResolution(screenWidth, screenHeight), screenWidth, screenHeight);
    }

    /// <summary>Whether <paramref name="displayModeWord"/> pins the size to the screen's own,
    /// which borderless does: that window fills the screen, so a size chosen under it would be
    /// ignored. A word the vocabulary does not know reads as never set and falls through to the
    /// borderless default, so a file naming no mode pins the size too.</summary>
    public static bool Pinned(string? displayModeWord) =>
        DisplayModeSetting.Resolve(displayModeWord).Word == DisplayWords.Borderless;

    /// <summary>The size the three sources resolve to: the screen's own where
    /// <paramref name="displayModeWord"/> pins it, else <paramref name="savedWord"/> where
    /// <paramref name="offered"/> holds it or the screen can hold it
    /// (<see cref="SizeList.Including"/>), else that list's fallback, the screen's own size.
    /// ⚠ A saved size the screen cannot hold falls back to the screen's size, never to the nearest
    /// offered one, a distance over sizes having no right answer. docs/architecture/Utils.md.</summary>
    public static ResolutionPlan Resolve(string? savedWord, SizeList offered, string? displayModeWord)
    {
        if (Pinned(displayModeWord))
        {
            return OptionsStore.TryParseResolution(offered.Fallback, out int screenWidth, out int screenHeight)
                ? new ResolutionPlan(screenWidth, screenHeight, offered.Fallback, DisplayWords.Borderless)
                : new ResolutionPlan(ProjectWidth, ProjectHeight, ProjectSize, DisplayWords.Borderless);
        }

        if (savedWord != null && Offers(offered.Including(savedWord).Words, savedWord)
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

    /// <summary>Applies <paramref name="plan"/> to <paramref name="window"/>, whose mode has already
    /// been applied, and logs what the window reports back rather than what was asked of it. A
    /// windowed window takes the plan's size and is re-centred; an exclusive-fullscreen one renders
    /// at it inside the screen-sized window Godot gives it (<see cref="Render"/>); a borderless one
    /// is left alone at the screen's own size, which is what its mode gave it.</summary>
    public static void Apply(ResolutionPlan plan, Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var mode = DisplayServer.WindowGetMode();
        var current = DisplayServer.WindowGetSize();
        bool changed = mode == DisplayServer.WindowMode.Windowed
            && (current.X != plan.Width || current.Y != plan.Height);
        if (changed)
        {
            DisplayServer.WindowSetSize(new Vector2I(plan.Width, plan.Height));
            Centre();
        }

        Render(window, mode == DisplayServer.WindowMode.ExclusiveFullscreen
            ? new Vector2I(plan.Width, plan.Height) : Vector2I.Zero);
        var got = DisplayServer.WindowGetSize();
        var drawn = window.GetVisibleRect().Size;
        Log.Info("core", $"resolution={plan.Word} source={plan.Source} mode={DisplayServer.WindowGetMode()} window={got.X}x{got.Y} render={(int)drawn.X}x{(int)drawn.Y} changed={changed}");
    }

    // What the game renders at inside a window whose size it does not own. Godot's exclusive
    // fullscreen on Windows neither switches the display mode nor takes a WindowSetSize, the window
    // staying at the screen's size through both, so a chosen size can only be the render target,
    // drawn at that size and scaled up with its shape kept. Zero, and a size the window already
    // stands at, put back the project's own one-to-one presentation.
    private static void Render(Window window, Vector2I size)
    {
        bool scaled = size.X > 0 && size.Y > 0 && (size.X != window.Size.X || size.Y != window.Size.Y);
        window.ContentScaleMode = scaled ? Window.ContentScaleModeEnum.Viewport : Window.ContentScaleModeEnum.Disabled;
        window.ContentScaleAspect = scaled ? Window.ContentScaleAspectEnum.Keep : Window.ContentScaleAspectEnum.Ignore;
        window.ContentScaleSize = scaled ? size : Vector2I.Zero;
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
