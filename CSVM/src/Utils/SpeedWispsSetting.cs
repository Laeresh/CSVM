using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced draws the authored speed cue wisps (<c>Flight.Hud.SpeedCue</c>), one of
/// <see cref="Words"/>. In <see cref="Lookup"/>, <c>--speed-wisps=</c> beats the saved
/// <c>speedWisps</c> option, which beats the <see cref="Key"/> config key, then <see cref="Default"/>.
/// Under <c>--det</c> only the flag survives. The VIDEO page's Speed Wisps row writes it. The cue
/// reads it every step, so it applies at once. Original Graphics draws the wisps whatever the word.
/// </summary>
public static class SpeedWispsSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.speedWisps";

    /// <summary>No wisps under Enhanced: the wind sound and the camera carry speed.</summary>
    public const string Off = "off";

    /// <summary>The original's wisps under Enhanced too.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set.</summary>
    public const string Default = Off;

    /// <summary>Every word, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--speed-wisps=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the wisps draw this step: always under Original, by the word under Enhanced.</summary>
    public static bool Drawn => !GraphicsMode.Enhanced || Word == On;

    /// <summary>The word the sources resolve to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord)
    {
        var resolved = Lookup.Resolve(flagWord, savedWord, configWord, Default, null);
        Word = resolved.Word;
        return resolved;
    }

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--speed-wisps")
    {
        SavedField = "speedWisps",
        LogName = "speed_wisps",
        Labels = SpeedWispsSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "The original's pale wisps that pass the aircraft at speed. Off leaves speed to the wind and the camera. Applies at once."
            : "Enhanced Graphics only: the original world always draws its speed wisps.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default, null),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
    };
}
