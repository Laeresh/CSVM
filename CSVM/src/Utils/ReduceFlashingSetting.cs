using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether the lights that flash at a gun's rate stay dark, one of <see cref="Words"/>, an aid for
/// photosensitive players. On, no muzzle and no round in flight carries a light
/// (<c>Flight.Weapons.ProjectilePool</c>). No gun hit flashes its authored light either
/// (<c>Mech3.Anim.LightChannel</c>), so none of them glows in a cloud. In <see cref="Lookup"/>,
/// <c>--reduce-flashing=</c> beats the saved <c>reduceFlashing</c> option, which beats the
/// <see cref="Key"/> config key, then <see cref="Default"/>. Under <c>--det</c> only the flag survives.
/// The GAME OPTIONS page's Reduce Flashing row writes it, and each light reads it when it is lit.
/// </summary>
public static class ReduceFlashingSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "accessibility.reduceFlashing";

    /// <summary>Every gun light flashes as it is drawn.</summary>
    public const string Off = "off";

    /// <summary>No gun light flashes.</summary>
    public const string On = "on";

    /// <summary>The word every machine runs with nothing set.</summary>
    public const string Default = Off;

    /// <summary>Every word, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--reduce-flashing=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the gun lights stay dark now.</summary>
    public static bool Active => Word == On;

    private static WordSetting Register() => new(Key, Words, "--reduce-flashing")
    {
        SavedField = "reduceFlashing",
        LogName = "reduce_flashing",
        Labels = ReduceFlashingSetting.Labels,
        Detail = static _ => "Turn off the lights that flash with gunfire: the muzzles, the rounds in flight and the gun hits.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default, null),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
    };
}
