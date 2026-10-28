using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// The Enhanced vegetation's quality, one of <see cref="Words"/>: the original's clutter as stamped,
/// or the remake's richer vegetation over the same stamps. In <see cref="Lookup"/>,
/// <c>--vegetation-quality=</c> beats the saved <c>vegetationQuality</c> option, which beats the
/// <see cref="Key"/> config key, then the fallback. That follows the GPU and the pane count
/// (<see cref="FallbackFor"/>), so a splitscreen session resolves it again
/// (<see cref="WordSettings.FollowPanes"/>). Under <c>--det</c> only the flag survives, over
/// <see cref="Default"/>. The clutter reads <see cref="RichOn"/>, never the word, so the faithful path
/// stays Original whatever the word. The VIDEO page's Vegetation row writes the option, applied live
/// through the session.
/// </summary>
public static class VegetationQualitySetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.vegetationQuality";

    /// <summary>The original's clutter, stamped and drawn as the faithful path draws it.</summary>
    public const string Original = "original";

    /// <summary>The remake's richer vegetation over the original's stamps.</summary>
    public const string Rich = "rich";

    /// <summary>The word a launch with nothing saved runs on a discrete GPU, on an integrated one with
    /// one pane, and under <c>--det</c> on every machine. On the Deck its worst one-pane view, the C1
    /// meadow, ran 15.36 ms against Original's 13.27.</summary>
    public const string Default = Rich;

    /// <summary>The word an integrated GPU runs when nothing is set and the session draws
    /// <see cref="SplitPanes"/> or more panes. On the Deck the two-pane C1 crop field ran 23.40 ms on
    /// Rich and 12.42 on Original.</summary>
    public const string IntegratedSplitDefault = Original;

    /// <summary>The fewest panes that take <see cref="IntegratedSplitDefault"/>.</summary>
    public const int SplitPanes = 2;

    /// <summary>The log's name for the fallback rule that chose <see cref="IntegratedSplitDefault"/>.</summary>
    public const string IntegratedSplitRule = "default_integrated_gpu_panes";

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Original, Rich };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Original", "Rich" };

    /// <summary>The setting's registry entry, beaten by <c>--vegetation-quality=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until <see cref="Resolve"/> runs.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the clutter draws the Rich vegetation: the word asks for it and the graphics
    /// mode is Enhanced. ⚠ Read this, never <see cref="Word"/>, from a build: the faithful path must
    /// draw the original's clutter whatever the saved word.</summary>
    public static bool RichOn => GraphicsMode.Enhanced && Word == Rich;

    /// <summary>The fallback on a GPU that is or is not <paramref name="integrated"/>, for a session
    /// drawing <paramref name="panes"/>. A discrete GPU runs <see cref="Default"/> on any OS and at any
    /// count. An integrated one runs it on one pane and <see cref="IntegratedSplitDefault"/> from
    /// <see cref="SplitPanes"/> panes up.</summary>
    public static string FallbackFor(bool integrated, int panes) =>
        integrated && panes >= SplitPanes ? IntegratedSplitDefault : Default;

    /// <summary>The word <see cref="Pick"/> resolves the sources to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var resolved = Pick(flagWord, savedWord, configWord, fallback);
        Word = resolved.Word;
        return resolved;
    }

    /// <summary>The word <see cref="Lookup"/> resolves the sources to over <paramref name="fallback"/>,
    /// leaving <see cref="Word"/> alone. An <see cref="IntegratedSplitDefault"/> fallback carries its
    /// own source, so the log line says the panes chose Original.</summary>
    public static ResolvedWord Pick(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default) =>
        Lookup.Resolve(flagWord, savedWord, configWord, fallback, FallbackRule(fallback));

    private static string? FallbackRule(string fallback) => fallback == IntegratedSplitDefault ? IntegratedSplitRule : null;

    // Built in a method rather than inline. Inside a static field initializer, the nullable analysis
    // reads the Shown hook's static Word as maybe null and warns (CS8603).
    private static WordSetting Register() => new(Key, Words, "--vegetation-quality")
    {
        SavedField = "vegetationQuality",
        LogName = "vegetation_quality",
        Labels = VegetationQualitySetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Original draws the original's trees and bushes; Rich adds detail to them. Applies at once."
            : "Enhanced Graphics only: the original world's vegetation is always as authored.",
        Shown = static _ => Word,
        // ⚠ Ask the renderer only off --det: a unit test reads FallbackFor, having no renderer.
        Fallback = static context =>
        {
            string fallback = context.Det ? Default : FallbackFor(ShadowQualitySetting.IntegratedGpu, context.Panes);
            return new ResolvedWord(fallback, SettingSource.Default, FallbackRule(fallback));
        },
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world => world.FollowSession(),
    };
}
