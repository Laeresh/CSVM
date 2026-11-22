using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSVM.Utils;

/// <summary>
/// The player's spyglass size as a percentage word: 100, 125, 150 or 200. It multiplies the flat
/// views' disc, its picture's side and everything placed from its radius
/// (<c>Flight.Hud.HudMetrics.SpyglassScale</c> is the one reader). In <see cref="Lookup"/> the saved
/// <c>spyglassScale</c> option beats the <see cref="Key"/> config key, then <see cref="Default"/>; no
/// flag beats it and no <c>--det</c> run reads the file. The resolve stores its factor as
/// <see cref="Current"/> at launch and on every options apply, and the HUD reads it every frame.
/// </summary>
public static class SpyglassScaleSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "hud.spyglassScale";

    /// <summary>The word a launch with nothing saved runs, the disc at its decoded size.</summary>
    public const string Default = "100";

    /// <summary>The factor <see cref="Default"/> spells, which draws exactly the unscaled disc.</summary>
    public const float Native = 1f;

    /// <summary>The words, smallest first, which is the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "100", "125", "150", "200" };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = Words.Select(static w => w + "%").ToArray();

    /// <summary>The setting's registry entry. No flag beats it.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The factor the running process draws the disc at, <see cref="Native"/> until a resolve
    /// stores another.</summary>
    public static float Current { get; set; } = Native;

    /// <summary>The factor <paramref name="word"/> spells, <see cref="Native"/> for a null word or one
    /// outside <see cref="Words"/>.</summary>
    public static float FactorOf(string? word) =>
        Lookup.IsWord(word) && int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int percent)
            ? percent / 100f
            : Native;

    private static WordSetting Register() => new(Key, Words)
    {
        SavedField = "spyglassScale",
        LogName = "spyglass_scale",
        Labels = SpyglassScaleSetting.Labels,
        Detail = static _ => "Draw the round picture of an off-screen target larger.",
        Shown = static _ => Default,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default, null),
        Take = static resolved =>
        {
            Current = FactorOf(resolved.Word);
            return resolved;
        },
    };
}
