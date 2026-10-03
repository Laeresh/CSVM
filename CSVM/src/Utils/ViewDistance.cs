namespace CSVM.Utils;

/// <summary>
/// The enhanced mode's view distance, one of four words. It sets how far the clutter draws before
/// its authored far fade: the buildings, poles, trees and signs a chapter scatters. The fog never
/// moves with it, since the early chapters' haze is part of their scenery. The faithful path
/// ignores it and keeps the decoded fade. In <see cref="Lookup"/>, <c>--view-distance=</c> beats
/// the saved <c>viewDistance</c> option, then the <see cref="Key"/> config key, then
/// <see cref="Default"/>. The Built-in Options screen writes the option, applied live.
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

    /// <summary>The source order over <see cref="Words"/>, beaten by <c>--view-distance=</c>.</summary>
    public static readonly WordSetting Lookup = new(Key, Words, "--view-distance");

    // TUNE. Multiples of the fade distance enhanced mode already draws clutter to. The last is no
    // fade at all, so every piece of clutter draws out to the fog, which still hides it past there.
    private static readonly float[] Reaches = { 1f, 2f, 4f, float.PositiveInfinity };

    private static float _reach = Reaches[Lookup.IndexOf(Default)];

    /// <summary>The position of <paramref name="word"/> in <see cref="Words"/>, or the default's
    /// for a null or unknown word.</summary>
    public static int Index(string? word)
    {
        int i = Lookup.IndexOf(word);
        return i < 0 ? Lookup.IndexOf(Default) : i;
    }

    /// <summary>The label a screen shows for <paramref name="word"/>.</summary>
    public static string Label(string? word) => Labels[Index(word)];

    /// <summary>How much further clutter draws under <paramref name="word"/> than its enhanced
    /// fade, infinite for no fade at all.</summary>
    public static float Reach(string? word) => Reaches[Index(word)];

    /// <summary>The word <see cref="Lookup"/> resolves the sources to over <see cref="Default"/>,
    /// whose reach is then set. ⚠ Pass no saved word under <c>--det</c>: the options file is one
    /// machine's state.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord)
    {
        var resolved = Lookup.Resolve(flagWord, savedWord, configWord, Default);
        Set(resolved.Word);
        return resolved;
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (<see cref="WordSetting.ReadSaved"/>).</summary>
    public static string? SavedWord(bool det) => WordSetting.ReadSaved(det, static o => o.ViewDistance);

    /// <summary>Sets the reach from one word. The caller writes the clutter fade global again after
    /// a change.</summary>
    public static void Set(string? word) => _reach = Reach(word);

    /// <summary>How much further clutter draws than its authored fade. Enhanced mode takes the
    /// resolved reach; original mode takes 1, keeping the decoded fade.</summary>
    public static float ClutterReach() => GraphicsMode.Enhanced ? _reach : 1f;
}
