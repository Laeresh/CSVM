using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced draws the drifting cloud shadows (<c>Effects.CloudShadow</c>), one of
/// <see cref="Words"/>. In <see cref="Lookup"/>, <c>--cloud-shadows=</c> beats the saved
/// <c>cloudShadows</c> option, which beats the <see cref="Key"/> config key, then
/// <see cref="Default"/>. Under <c>--det</c> only the flag survives, over <see cref="Off"/>, so a
/// golden shot draws none unless it asks. The VIDEO page's Cloud Shadows row writes it. The shadows
/// read it every frame, so it applies at once with no shader rebuilt. They ride the terrain shadow's
/// sun term, so <c>--terrain-shadows=off</c> leaves the word nothing to draw on, and the row says so.
/// </summary>
public static class CloudShadowSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.cloudShadows";

    /// <summary>No cloud shadows.</summary>
    public const string Off = "off";

    /// <summary>Cloud shadows over a cloudy day.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set, the Deck included until a frame-time
    /// measurement there says otherwise.</summary>
    public const string Default = On;

    /// <summary>The log's name for the fallback rule that chose <see cref="Off"/> under <c>--det</c>.</summary>
    public const string DetRule = "default_det";

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--cloud-shadows=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the resolved word draws the shadows. Original never does, whatever the word.</summary>
    public static bool Enabled => Word == On;

    /// <summary>The fallback for a run that is or is not <paramref name="det"/>.</summary>
    public static ResolvedWord FallbackFor(bool det) =>
        det ? new ResolvedWord(Off, SettingSource.Default, DetRule) : new ResolvedWord(Default, SettingSource.Default);

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--cloud-shadows")
    {
        SavedField = "cloudShadows",
        LogName = "cloud_shadows",
        Labels = CloudShadowSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => !enhanced
            ? "Enhanced Graphics only: the original world casts no cloud shadow."
            : ShadowQualitySetting.TerrainShadows
                ? "Soft shadows of passing clouds drift over the ground and the water by day. Applies at once."
                : "Terrain shadows only: this launch runs --terrain-shadows=off, so no cloud shadow draws.",
        Shown = static _ => Word,
        Fallback = static context => FallbackFor(context.Det),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
    };
}
