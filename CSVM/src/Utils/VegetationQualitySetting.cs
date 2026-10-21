using System;
using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// The Enhanced vegetation's quality, one of <see cref="Words"/>: the original's clutter as stamped,
/// or the remake's richer vegetation over the same stamps. In <see cref="Lookup"/>,
/// <c>--vegetation-quality=</c> beats the saved <c>vegetationQuality</c> option, which beats the
/// <see cref="Key"/> config key, then the fallback. That is <see cref="Rich"/> on a desktop and
/// <see cref="Original"/> on Linux or an integrated GPU (<see cref="FallbackFor"/>), so the Steam Deck
/// runs Original. Under <c>--det</c> only the flag survives, over <see cref="Default"/>. The clutter
/// reads <see cref="RichOn"/>, never the word, so the faithful path stays Original whatever the word.
/// The VIDEO page's Vegetation row writes the option, applied live through the session.
/// </summary>
public static class VegetationQualitySetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.vegetationQuality";

    /// <summary>The original's clutter, stamped and drawn as the faithful path draws it.</summary>
    public const string Original = "original";

    /// <summary>The remake's richer vegetation over the original's stamps.</summary>
    public const string Rich = "rich";

    /// <summary>The word a desktop with nothing saved runs, and under <c>--det</c> every machine.</summary>
    public const string Default = Rich;

    /// <summary>The word Linux and an integrated GPU run when nothing is set, until a measurement on
    /// the Deck says Rich fits its frame.</summary>
    public const string LowPowerDefault = Original;

    /// <summary>The log's name for the fallback rule that chose <see cref="LowPowerDefault"/>.</summary>
    public const string LowPowerRule = "default_linux_or_integrated_gpu";

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

    /// <summary>The fallback word for this machine. ⚠ Asks the renderer for its GPU class, so a unit
    /// test reads <see cref="FallbackFor"/> instead.</summary>
    public static string MachineDefault => FallbackFor(OperatingSystem.IsLinux(), ShadowQualitySetting.IntegratedGpu);

    /// <summary>The fallback a launch resolves against. ⚠ <see cref="Default"/> under
    /// <paramref name="det"/>, so a scripted capture does not depend on the machine it runs on.</summary>
    public static string DefaultFor(bool det) => det ? Default : MachineDefault;

    /// <summary>The fallback on a machine that is or is not running <paramref name="linux"/>, on a GPU
    /// that is or is not <paramref name="integrated"/>. Either one takes <see cref="LowPowerDefault"/>.
    /// Linux stands for the Deck, whose SteamOS reports Linux whatever its GPU reads as.</summary>
    public static string FallbackFor(bool linux, bool integrated) => linux || integrated ? LowPowerDefault : Default;

    /// <summary>The word <see cref="Pick"/> resolves the sources to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var resolved = Pick(flagWord, savedWord, configWord, fallback);
        Word = resolved.Word;
        return resolved;
    }

    /// <summary>The word <see cref="Lookup"/> resolves the sources to over <paramref name="fallback"/>,
    /// leaving <see cref="Word"/> alone. A <see cref="LowPowerDefault"/> fallback carries its own
    /// source, so the log line says the machine chose Original.</summary>
    public static ResolvedWord Pick(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default) =>
        Lookup.Resolve(flagWord, savedWord, configWord, fallback, FallbackRule(fallback));

    private static string? FallbackRule(string fallback) => fallback == LowPowerDefault ? LowPowerRule : null;

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
        Fallback = static context =>
        {
            string fallback = DefaultFor(context.Det);
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
