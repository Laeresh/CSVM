using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSVM.Utils;

/// <summary>
/// The player's spyglass size as a percentage word: 100, 125, 150 or 200. It multiplies the flat
/// views' disc, its picture's side and everything placed from its radius
/// (<c>Flight.Hud.HudMetrics.SpyglassScale</c> is the one reader). The saved word beats 100, and no
/// deterministic run reads the file. The launch sets <see cref="Current"/> and so does every options
/// apply, and the HUD reads it every frame. docs/architecture/Utils.md.
/// </summary>
public static class SpyglassScaleSetting
{
    /// <summary>The word a launch with nothing saved runs, the disc at its decoded size.</summary>
    public const string Default = "100";

    /// <summary>The factor <see cref="Default"/> spells, which draws exactly the unscaled disc.</summary>
    public const float Native = 1f;

    /// <summary>The words, smallest first, which is the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "100", "125", "150", "200" };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read and no flag beats it.</summary>
    public static readonly WordSetting Lookup = new("hud.spyglassScale", Words);

    private static readonly string[] LabelWords = Words.Select(static w => w + "%").ToArray();

    /// <summary>The factor the running process draws the disc at, <see cref="Native"/> until a launch
    /// or an apply stores another.</summary>
    public static float Current { get; set; } = Native;

    /// <summary>The factor <paramref name="word"/> spells, <see cref="Native"/> for a null word or one
    /// outside <see cref="Words"/>.</summary>
    public static float FactorOf(string? word) =>
        Lookup.IsWord(word) && int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int percent)
            ? percent / 100f
            : Native;

    /// <summary>The word the sources resolve to: <paramref name="savedWord"/>, then
    /// <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? savedWord) => Lookup.Resolve(null, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (the <c>--det</c> rule <see cref="WordSettings.ReadSaved"/> keeps).</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().SpyglassScale;

    /// <summary>Resolves <paramref name="savedWord"/> and stores its factor as <see cref="Current"/>.
    /// Returns the word and its source for the caller's log line.</summary>
    public static ResolvedWord Apply(string? savedWord)
    {
        var resolved = Resolve(savedWord);
        Current = FactorOf(resolved.Word);
        return resolved;
    }

    /// <summary>Stores the saved word's factor as <see cref="Current"/> through <see cref="SavedWord"/>,
    /// so <paramref name="det"/> reads none, and logs the word and its source. The launch and every
    /// options apply call it after the file is written.</summary>
    public static void ApplySaved(bool det)
    {
        var resolved = Apply(SavedWord(det));
        Log.Info("flight", $"spyglass scale={resolved.Word} source={Lookup.SourceName(resolved.Source)}");
    }

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels() => LabelWords;
}
