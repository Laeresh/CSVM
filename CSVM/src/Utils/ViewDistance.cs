using System;

namespace CSVM.Utils;

/// <summary>
/// The enhanced mode's view distance: how far the clutter (the buildings, poles, trees and signs a
/// chapter scatters over its ground) draws before its authored far fade takes it, as one of four
/// saved words. The fog never moves with it, since on the early chapters the haze is part of the
/// scenery. C5's city blocks fade at 700-900 m authored, well inside its fog, which is the gap
/// this closes. The faithful path ignores it and keeps the decoded fade. Its home is
/// <see cref="OptionsStore"/>'s <c>viewDistance</c> field, which both Options screens write and an
/// apply switches live, like <see cref="GraphicsMode"/>.
/// </summary>
public static class ViewDistance
{
    /// <summary>The shipped word: the fade enhanced mode always had.</summary>
    public const string Default = "normal";

    /// <summary>The words the option carries, nearest first.</summary>
    public static readonly string[] Words = { "normal", "far", "farther", "farthest" };

    // TUNE. Multiples of the fade distance enhanced mode already draws clutter to. The last is no
    // fade at all, so every piece of clutter draws out to the fog, which still hides it past there.
    private static readonly float[] Reaches = { 1f, 2f, 4f, float.PositiveInfinity };

    private static float _reach = Reaches[0];

    /// <summary>The position of <paramref name="word"/> in <see cref="Words"/>, or the default's
    /// for a null or unknown word.</summary>
    public static int Index(string? word)
    {
        int i = word == null ? -1 : Array.IndexOf(Words, word);
        return i < 0 ? 0 : i;
    }

    /// <summary>Resolve the reach from a saved word, at launch and on every apply. The caller
    /// writes the clutter fade global again after a change.</summary>
    public static void Set(string? word) => _reach = Reaches[Index(word)];

    /// <summary>How much further clutter draws than its authored fade: the saved reach in enhanced
    /// mode, infinite for no fade at all, and 1 in original mode, whose fade stays the decoded one.</summary>
    public static float ClutterReach() => GraphicsMode.Enhanced ? _reach : 1f;
}
