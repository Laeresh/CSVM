using System;

namespace CSVM.Utils;

/// <summary>
/// The enhanced mode's view distance, one of four saved words. It sets how far the clutter draws
/// before its authored far fade: the buildings, poles, trees and signs a chapter scatters. The fog never moves with it, since on the early chapters the haze is part of the
/// scenery. C5's city blocks fade at 700-900 m authored, well inside its fog. The faithful path
/// ignores it and keeps the decoded fade. Its home is <see cref="OptionsStore"/>'s
/// <c>viewDistance</c> field, which the Built-in Options screen writes and an apply switches live.
/// </summary>
public static class ViewDistance
{
    /// <summary>The shipped word: the clutter fade enhanced mode's fog push gives on its own.</summary>
    public const string Default = "normal";

    /// <summary>The words the option carries, nearest first.</summary>
    public static readonly string[] Words = { "normal", "far", "veryfar", "unlimited" };

    /// <summary>What a screen shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly string[] Labels = { "Normal", "Far", "Very Far", "Unlimited" };

    // TUNE. Multiples of the fade distance enhanced mode already draws clutter to. The last is no
    // fade at all, so every piece of clutter draws out to the fog, which still hides it past there.
    private static readonly float[] Reaches = { 1f, 2f, 4f, float.PositiveInfinity };

    private static float _reach = Reaches[0];

    /// <summary>The position of <paramref name="word"/> in <see cref="Words"/>, or the default's
    /// for a null or unknown word.</summary>
    public static int Index(string? word)
    {
        int i = word == null ? -1 : Array.IndexOf(Words, word);
        return i < 0 ? Array.IndexOf(Words, Default) : i;
    }

    /// <summary>The label a screen shows for <paramref name="word"/>.</summary>
    public static string Label(string? word) => Labels[Index(word)];

    /// <summary>How much further clutter draws under <paramref name="word"/> than its enhanced
    /// fade, infinite for no fade at all.</summary>
    public static float Reach(string? word) => Reaches[Index(word)];

    /// <summary>Resolve the reach from a saved word, at launch and on every apply. The caller
    /// writes the clutter fade global again after a change.</summary>
    public static void Set(string? word) => _reach = Reach(word);

    /// <summary>How much further clutter draws than its authored fade. Enhanced mode takes the
    /// saved reach; original mode takes 1, keeping the decoded fade.</summary>
    public static float ClutterReach() => GraphicsMode.Enhanced ? _reach : 1f;
}
