using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// How far the Enhanced sun casts, one of <see cref="Words"/>. Each word multiplies the zone's shadow
/// distance (the haze start) by its <see cref="Scale"/>. The three near cascades keep their size in
/// metres, so only the far one stretches. In <see cref="Lookup"/>, <c>--shadow-distance=</c> beats the
/// saved <c>shadowDistance</c> option, which beats the <see cref="Key"/> config key, then
/// <see cref="Default"/>. The VIDEO page's Shadow Distance row writes it, applied live
/// (<see cref="SunShadow.Stretch"/>). The faithful path casts no sun shadow.
/// </summary>
public static class ShadowDistanceSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.shadowDistance";

    /// <summary>Shadows end where the zone's haze begins.</summary>
    public const string Normal = "normal";

    /// <summary>Shadows reach half again as far.</summary>
    public const string Far = "far";

    /// <summary>Shadows reach twice as far.</summary>
    public const string Extreme = "extreme";

    /// <summary>The word every machine runs with nothing set, and under <c>--det</c>.</summary>
    public const string Default = Normal;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Normal, Far, Extreme };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Normal", "Far", "Extreme" };

    /// <summary>The setting's registry entry, beaten by <c>--shadow-distance=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    // TUNE. Multiples of the zone's shadow distance, index for index with Words. The far cascade's
    // texels grow with it: about 2 m at Extreme on an 8192 atlas, still sharp under a zeppelin.
    private static readonly float[] Scales = { 1f, 1.5f, 2f };

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>The resolved word's multiple of the zone's shadow distance.</summary>
    public static float Scale => ScaleFor(Word);

    /// <summary>The multiple <paramref name="word"/> names, or the default's for an unknown word.</summary>
    public static float ScaleFor(string? word)
    {
        int at = Lookup.IndexOf(word);
        return Scales[at >= 0 ? at : Lookup.IndexOf(Default)];
    }

    /// <summary>The multiple the session sun takes: the resolved one under Enhanced, 1 otherwise.</summary>
    public static float EnhancedScale() => GraphicsMode.Enhanced ? Scale : 1f;

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--shadow-distance")
    {
        SavedField = "shadowDistance",
        LogName = "shadow_distance",
        Labels = ShadowDistanceSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "How far the sun casts shadows, so a far zeppelin shades the ground. Farther costs frame rate. Applies at once."
            : "Enhanced Graphics only. The original world casts no sun shadows.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world =>
        {
            if (world.Sun is { } sun)
                SunShadow.Stretch(sun);
        },
    };
}
