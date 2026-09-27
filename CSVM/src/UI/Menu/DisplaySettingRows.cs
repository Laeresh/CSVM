using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu;

/// <summary>
/// How the six display settings read as rows, shared by the two Options screens so they cannot
/// disagree about a saved value. The labels are one per <see cref="CSVM.Utils.DisplayWords"/> entry
/// and in that order, since a row reads and writes the store word by index. The index rules restate
/// the resolvers' forgiving reads: a never-set row shows the setting's own default, an unoffered or
/// pinned size the screen's own. The screens and the sizes are enumerated per machine by
/// <see cref="CSVM.Utils.MonitorSetting"/> and <see cref="CSVM.Utils.ResolutionSetting"/>.
/// Engine-free, so the rules test without a screen.
/// </summary>
public static class DisplaySettingRows
{
    /// <summary>The display-mode labels. The two fullscreen modes read as one word each because the
    /// VIDEO page's authored box is 120 wide; which of them keeps the desktop alive beside the game
    /// is what a row's description says.</summary>
    public static readonly IReadOnlyList<string> DisplayModeLabels = new[] { "Windowed", "Borderless", "Fullscreen" };

    /// <summary>The V-Sync labels. A word that parses as a number is a cap in frames per second
    /// with V-Sync off, which is why the caps read as rates rather than as bare numbers.</summary>
    public static readonly IReadOnlyList<string> VSyncLabels = new[] { "On", "Off", "60 FPS", "120 FPS", "144 FPS" };

    /// <summary>The anti-aliasing labels, one per <see cref="CSVM.Utils.DisplayWords.AntiAliasingChoices"/>
    /// word and in that order.</summary>
    public static readonly IReadOnlyList<string> AntiAliasingLabels = new[] { "Off", "FXAA", "SMAA", "TAA", "FSR 2.2" };

    /// <summary>The render-scale labels for <paramref name="words"/>, which is the list
    /// <see cref="CSVM.Utils.RenderScaleSetting.ChoicesFor"/> offers under the standing method. Each
    /// store word is a percentage of the viewport's own size, so the label is that word with a sign
    /// on it.</summary>
    public static IReadOnlyList<string> RenderScaleLabels(IReadOnlyList<string> words)
    {
        var labels = new string[words.Count];
        for (int i = 0; i < words.Count; i++)
        {
            labels[i] = words[i] + "%";
        }

        return labels;
    }

    /// <summary>The anti-aliasing word a row stands on: the saved word, or else the mode's own
    /// default. <paramref name="graphicsWord"/> is the graphics word the same page would apply. The
    /// row therefore follows a flip of the Enhanced Graphics row beside it.</summary>
    public static string AntiAliasingWord(string? saved, string? graphicsWord)
    {
        string fallback = CSVM.Utils.AntiAliasingSetting.DefaultFor(graphicsWord == CSVM.Utils.GraphicsMode.EnhancedWord);
        return IndexOf(CSVM.Utils.DisplayWords.AntiAliasingChoices, saved) >= 0 ? saved! : fallback;
    }

    /// <summary>Where a saved word sits among a row's own values: a word the vocabulary does not
    /// know, or none saved at all, shows as <paramref name="fallback"/>, the setting's own default
    /// (the behaviour with no options file), which is not the first value of either vocabulary.</summary>
    public static int WordIndex(IReadOnlyList<string> words, string? word, string fallback)
    {
        int at = IndexOf(words, word);
        return at >= 0 ? at : Math.Max(0, IndexOf(words, fallback));
    }

    /// <summary>Where the size row stands among the sizes a screen offers. These words are the
    /// screen's own sizes rather than a vocabulary, so a size this screen does not offer, or none
    /// saved at all, shows as the list's own fallback, the screen's size; a
    /// <paramref name="displayModeWord"/> that pins the row reads the same way whatever is saved.
    /// That is <see cref="CSVM.Utils.ResolutionSetting.Resolve"/>'s own rule, and a row has to
    /// agree with it or it would name a size the window is not standing at.</summary>
    public static int ResolutionIndex(CSVM.Utils.SizeList sizes, string? saved, string? displayModeWord) =>
        WordIndex(sizes.Words, CSVM.Utils.ResolutionSetting.Pinned(displayModeWord) ? null : saved, sizes.Fallback);

    /// <summary>The value a sideways step lands on, wrapping in both directions, which is what every
    /// display row on either screen steps by. An empty list steps to 0, so a row with nothing to
    /// offer indexes nothing.</summary>
    public static int Step(int index, int direction, int count) =>
        count <= 0 ? 0 : (((index + direction) % count) + count) % count;

    private static int IndexOf(IReadOnlyList<string> words, string? word)
    {
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i] == word)
            {
                return i;
            }
        }

        return -1;
    }
}
