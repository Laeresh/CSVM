using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced lights the world with Godot's real-time global illumination
/// (<c>Effects.GlobalIllumination</c>, SDFGI plus SSIL), one of <see cref="Words"/>. In
/// <see cref="Lookup"/>, <c>--global-illumination=</c> beats the saved <c>globalIllumination</c>
/// option, which beats the <see cref="Key"/> config key, then the GPU's fallback (<see cref="FallbackFor"/>).
/// Under <c>--det</c> only the flag survives, over <see cref="Off"/>, so a golden shot lights without GI
/// unless it asks. A headset's VR quality profile stands it off. The VIDEO page's row writes it, and the
/// session's Environments follow live.
/// </summary>
public static class GlobalIlluminationSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.globalIllumination";

    /// <summary>The zone's flat ambient alone.</summary>
    public const string Off = "off";

    /// <summary>SDFGI plus SSIL over the zone's light.</summary>
    public const string On = "on";

    /// <summary>The word a discrete GPU runs with nothing set. SDFGI costs 0.6 to 1.0 ms of an RTX 5080's
    /// 1080p frame (D34 of the volumetric-clouds plan).</summary>
    public const string Default = On;

    /// <summary>The word an integrated GPU (the Steam Deck's APU) runs with nothing set, until a
    /// measurement there says SDFGI fits.</summary>
    public const string IntegratedDefault = Off;

    /// <summary>The log's name for the fallback rule that chose <see cref="IntegratedDefault"/>.</summary>
    public const string IntegratedRule = "default_integrated_gpu";

    /// <summary>The log's name for the fallback rule that chose <see cref="Off"/> under <c>--det</c>.</summary>
    public const string DetRule = "default_det";

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--global-illumination=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run stands on, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the standing word lights with GI. Original never does, whatever the word.</summary>
    public static bool Enabled => Word == On;

    /// <summary>The fallback for a run that is or is not <paramref name="det"/>, on a GPU that is or is
    /// not <paramref name="integrated"/>. Under <c>--det</c> every machine falls back to <see cref="Off"/>.</summary>
    public static ResolvedWord FallbackFor(bool det, bool integrated) =>
        det ? new ResolvedWord(Off, SettingSource.Default, DetRule)
        : integrated ? new ResolvedWord(IntegratedDefault, SettingSource.Default, IntegratedRule)
        : new ResolvedWord(Default, SettingSource.Default);

    /// <summary>Stands the run on <paramref name="on"/> without saving it, as a lab toggle does. The
    /// session puts the resolved word back when it ends (<see cref="WordSetting.Retake"/>).</summary>
    public static void TakeUnsaved(bool on) => Word = on ? On : Off;

    // Built in a method rather than inline, as CloudShadowSetting's is.
    private static WordSetting Register() => new(Key, Words, "--global-illumination")
    {
        SavedField = "globalIllumination",
        LogName = "global_illumination",
        Labels = GlobalIlluminationSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Sunlight bounces off the ground into shadow, and streets and eaves block the sky. Applies at once."
            : "Enhanced Graphics only: the original world is lit flat.",
        Shown = static _ => Word,
        // ⚠ Ask the renderer only off --det: a unit test reads FallbackFor, having no renderer.
        Fallback = static context => FallbackFor(context.Det, !context.Det && ShadowQualitySetting.IntegratedGpu),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world => world.FollowSession(),
    };
}
