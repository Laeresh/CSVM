using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced draws the volumetric clouds (<c>Session.World.VolumetricClouds</c>) in place of the
/// deck, the cloud field and the placed cloud sprites. Each of <see cref="Words"/> but off names a march
/// resolution. In <see cref="Lookup"/>, <c>--volumetric-clouds=</c> beats the saved
/// <c>volumetricClouds</c> option, which beats the <see cref="Key"/> config key, then
/// <see cref="Default"/>. Under <c>--det</c> only the flag survives. The VIDEO page's row writes it, and
/// the session builds or frees the clouds live; a move between resolutions only re-marches.
/// </summary>
public static class VolumetricCloudsSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.volumetricClouds";

    /// <summary>The original's cloud deck, field and sprites.</summary>
    public const string Off = "off";

    /// <summary>The volumetric clouds, marched at a quarter of the view's resolution.</summary>
    public const string Quarter = "quarter";

    /// <summary>The volumetric clouds, marched at half the view's resolution.</summary>
    public const string Half = "half";

    /// <summary>The volumetric clouds, marched at every pixel.</summary>
    public const string Full = "full";

    /// <summary>The word every machine runs with nothing set, until a frame-time measurement says the
    /// slab's cost suits a default.</summary>
    public const string Default = Off;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Quarter, Half, Full };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "Quarter", "Half", "Full" };

    /// <summary>The setting's registry entry, beaten by <c>--volumetric-clouds=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run stands on, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the standing word draws the clouds. Original never does, whatever the word.</summary>
    public static bool Enabled => Word != Off;

    /// <summary>The march's resolution divisor the standing word names: 1 at full, 4 at quarter, else 2.
    /// A headset, a splitscreen pane and the spyglass march every pixel whatever it says.</summary>
    public static int MarchDivisor => Word switch
    {
        Full => 1,
        Quarter => 4,
        _ => 2,
    };

    /// <summary>Stands the run on <paramref name="word"/> without saving it, as a lab control does. The
    /// session puts the resolved word back when it ends (<see cref="WordSetting.Retake"/>).</summary>
    public static void TakeUnsaved(string word) => Word = Lookup.Known(word) ?? Default;

    // Built in a method rather than inline, as CloudShadowSetting's is.
    private static WordSetting Register() => new(Key, Words, "--volumetric-clouds")
    {
        SavedField = "volumetricClouds",
        LogName = "volumetric_clouds",
        Labels = VolumetricCloudsSetting.Labels,
        // The on/off option's on marched at half resolution (PLAN-volumetric-clouds-gi D37).
        Legacy = new Dictionary<string, string> { ["on"] = Half },
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Raymarched clouds with light shafts in place of the cloud deck, marched at a quarter, half or the full resolution. Holds the weather zone. Applies at once."
            : "Enhanced Graphics only: the original world draws its own clouds.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world => world.FollowSession(),
    };
}
