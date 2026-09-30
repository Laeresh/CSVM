using System;

namespace CSVM.Utils;

/// <summary>One resolved view distance: the word, and the source that won, named so a log line can
/// say which layer the run is obeying.</summary>
public readonly record struct ViewDistancePlan(string Word, string Source);

/// <summary>
/// The enhanced mode's view distance, one of four words. It sets how far the clutter draws before
/// its authored far fade: the buildings, poles, trees and signs a chapter scatters. The fog never
/// moves with it, since the early chapters' haze is part of their scenery. The faithful path
/// ignores it and keeps the decoded fade. The sources layer as <see cref="GraphicsMode"/>'s do:
/// <c>--view-distance=</c>, the saved <c>viewDistance</c> option, the <see cref="Key"/> config
/// key, then <see cref="Default"/>. The Built-in Options screen writes the option, applied live.
/// </summary>
public static class ViewDistance
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.viewDistance";

    /// <summary>The word a launch with nothing saved runs. On C5, Far costs one pane nothing and four
    /// panes about what Normal does. Very Far and Unlimited cost four panes measurably more.</summary>
    public const string Default = "far";

    /// <summary>The words the option carries, nearest first.</summary>
    public static readonly string[] Words = { "normal", "far", "veryfar", "unlimited" };

    /// <summary>What a screen shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly string[] Labels = { "Normal", "Far", "Very Far", "Unlimited" };

    // TUNE. Multiples of the fade distance enhanced mode already draws clutter to. The last is no
    // fade at all, so every piece of clutter draws out to the fog, which still hides it past there.
    private static readonly float[] Reaches = { 1f, 2f, 4f, float.PositiveInfinity };

    private static float _reach = Reaches[Array.IndexOf(Words, Default)];

    /// <summary>Whether <paramref name="word"/> is one of <see cref="Words"/>.</summary>
    public static bool IsWord(string? word) => word != null && Array.IndexOf(Words, word) >= 0;

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

    /// <summary>Resolves and sets the reach, highest first: <paramref name="flagWord"/>, then
    /// <paramref name="savedWord"/>, then <paramref name="configWord"/>, then <see cref="Default"/>.
    /// A word outside <see cref="Words"/> reads as never set; an unknown config word also warns.
    /// ⚠ The caller passes no saved word under <c>--det</c>: the options file is one machine's state.
    /// </summary>
    public static ViewDistancePlan Resolve(string? flagWord, string? savedWord, string? configWord)
    {
        var plan = IsWord(flagWord) ? new ViewDistancePlan(flagWord!, "--view-distance")
            : IsWord(savedWord) ? new ViewDistancePlan(savedWord!, "options.json")
            : IsWord(configWord) && configWord != Default ? new ViewDistancePlan(configWord!, Key)
            : new ViewDistancePlan(Default, "default");
        if (configWord != null && !IsWord(configWord))
            Log.Warn("world", $"config {Key}={configWord} is not one of {string.Join("/", Words)}; using {plan.Word}");
        Set(plan.Word);
        return plan;
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved setting: the options file is one machine's state.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().ViewDistance;

    /// <summary>Sets the reach from one word. The caller writes the clutter fade global again after
    /// a change.</summary>
    public static void Set(string? word) => _reach = Reach(word);

    /// <summary>How much further clutter draws than its authored fade. Enhanced mode takes the
    /// resolved reach; original mode takes 1, keeping the decoded fade.</summary>
    public static float ClutterReach() => GraphicsMode.Enhanced ? _reach : 1f;
}
