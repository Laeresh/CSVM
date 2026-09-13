using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu;

/// <summary>
/// How the four display settings read as rows, shared by the two Options screens so they cannot
/// disagree about a saved value. The labels are one per <see cref="CSVM.Utils.DisplayWords"/> entry
/// and in that order, since a row reads and writes the store word by index; the two index rules are
/// the forgiving reads the resolvers already make (a never-set row shows the setting's own default,
/// an unoffered size the screen's own, and a size row the display mode pins the screen's own
/// whatever is saved), restated where a row needs an index rather than a plan. The screens and the sizes are enumerated per machine by
/// <see cref="CSVM.Utils.MonitorSetting"/> and <see cref="CSVM.Utils.ResolutionSetting"/>, which is
/// why neither of those is a list here. Engine-free, so the rules test without a screen.
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
