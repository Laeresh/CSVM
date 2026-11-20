using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether a Capture the Flag pane marks the flags and bases that matter beside its target
/// (<c>Flight.Modes.FlagMarkers.Passive</c>), one of <see cref="Words"/>. A remake-only aid: the
/// original marks a flag or base only as the selected target. In <see cref="Lookup"/>,
/// <c>--flag-marks=</c> beats the saved <c>flagMarks</c> option, which beats the <see cref="Key"/>
/// config key, then <see cref="Default"/>. Under <c>--det</c> only the flag survives. The GAME
/// OPTIONS page's Flag Markers row writes it, and the HUD reads it every frame.
/// </summary>
public static class FlagMarksSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "hud.flagMarks";

    /// <summary>Flags and bases marked only as the selected target, as the original does.</summary>
    public const string Off = "off";

    /// <summary>The flags and bases that matter marked as well.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set.</summary>
    public const string Default = On;

    /// <summary>Every word, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--flag-marks=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the passive marks draw this frame.</summary>
    public static bool Drawn => Word == On;

    private static WordSetting Register() => new(Key, Words, "--flag-marks")
    {
        SavedField = "flagMarks",
        LogName = "flag_marks",
        Labels = FlagMarksSetting.Labels,
        Detail = static _ => "In Capture the Flag, mark the enemy flag, your own flag while it is away, and your base while you carry a flag.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default, null),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
    };
}
