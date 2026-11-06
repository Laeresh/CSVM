using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced lays its colour grade over the lit world (<c>Launch.EnhancedLook.ApplyGrade</c>),
/// one of <see cref="Words"/>. In <see cref="Lookup"/>, <c>--post-processing=</c> beats the saved
/// <c>postProcessing</c> option, which beats the <see cref="Key"/> config key, then
/// <see cref="Default"/>. Under <c>--det</c> only the flag survives, over <see cref="Off"/>, so a
/// golden shot keeps the ungraded frame unless it asks. The VIDEO page's Post-processing row writes
/// it, and it applies at once to the session's Environments.
/// </summary>
public static class PostProcessingSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.postProcessing";

    /// <summary>The AgX tonemap alone.</summary>
    public const string Off = "off";

    /// <summary>The grade over the tonemap.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set.</summary>
    public const string Default = On;

    /// <summary>The log's name for the fallback rule that chose <see cref="Off"/> under <c>--det</c>.</summary>
    public const string DetRule = "default_det";

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--post-processing=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the resolved word grades. Original never does, whatever the word.</summary>
    public static bool Enabled => Word == On;

    /// <summary>The fallback for a run that is or is not <paramref name="det"/>.</summary>
    public static ResolvedWord FallbackFor(bool det) =>
        det ? new ResolvedWord(Off, SettingSource.Default, DetRule) : new ResolvedWord(Default, SettingSource.Default);

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--post-processing")
    {
        SavedField = "postProcessing",
        LogName = "post_processing",
        Labels = PostProcessingSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Raise the contrast and colour of the lit world. Applies at once."
            : "Enhanced Graphics only: the original world is not graded.",
        Shown = static _ => Word,
        Fallback = static context => FallbackFor(context.Det),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world => world.FollowSession(),
    };
}
