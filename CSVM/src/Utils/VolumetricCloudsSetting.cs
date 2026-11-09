using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced draws the volumetric clouds (<c>Session.World.VolumetricClouds</c>) in place of the
/// deck, the cloud field and the placed cloud sprites, one of <see cref="Words"/>. In
/// <see cref="Lookup"/>, <c>--volumetric-clouds=</c> beats the saved <c>volumetricClouds</c> option,
/// which beats the <see cref="Key"/> config key, then <see cref="Default"/>. Under <c>--det</c> only the
/// flag survives. The VIDEO page's row writes it, and the session builds or frees the clouds live.
/// </summary>
public static class VolumetricCloudsSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.volumetricClouds";

    /// <summary>The original's cloud deck, field and sprites.</summary>
    public const string Off = "off";

    /// <summary>The volumetric clouds in their place.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set, until a frame-time measurement says the
    /// slab's cost suits a default.</summary>
    public const string Default = Off;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--volumetric-clouds=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run stands on, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the standing word draws the clouds. Original never does, whatever the word.</summary>
    public static bool Enabled => Word == On;

    /// <summary>Stands the run on <paramref name="on"/> without saving it, as a lab toggle does. The
    /// session puts the resolved word back when it ends (<see cref="WordSetting.Retake"/>).</summary>
    public static void TakeUnsaved(bool on) => Word = on ? On : Off;

    // Built in a method rather than inline, as CloudShadowSetting's is.
    private static WordSetting Register() => new(Key, Words, "--volumetric-clouds")
    {
        SavedField = "volumetricClouds",
        LogName = "volumetric_clouds",
        Labels = VolumetricCloudsSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Raymarched clouds with light shafts in place of the cloud deck. Holds the weather zone. Applies at once."
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
