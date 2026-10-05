using System;
using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// The Enhanced sea's water quality, one of <see cref="Words"/>: the flat sea-level sheet, or the
/// wave ocean (<c>Effects.Ocean</c>) in its place. In <see cref="Lookup"/>, <c>--water-quality=</c>
/// beats the saved <c>waterQuality</c> option, which beats the <see cref="Key"/> config key, then the
/// fallback. That is <see cref="Waves"/> on a desktop and <see cref="Flat"/> on Linux or an
/// integrated GPU (<see cref="FallbackFor"/>), so the Steam Deck runs flat. Under <c>--det</c> only
/// the flag survives, over <see cref="Default"/>. The faithful path draws the flat sheet whatever the
/// word. The Built-in Options screen writes the option, applied live through the session.
/// </summary>
public static class WaterQualitySetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.waterQuality";

    /// <summary>The flat sea-level sheet, as the faithful path draws it.</summary>
    public const string Flat = "flat";

    /// <summary>The wave ocean over the sea-level sheet's place.</summary>
    public const string Waves = "waves";

    /// <summary>The word a desktop with nothing saved runs, and under <c>--det</c> every machine.</summary>
    public const string Default = Waves;

    /// <summary>The word Linux and an integrated GPU run when nothing is set. The Deck already runs
    /// Enhanced split-screen below 60 fps, and the ocean's reflection costs more than the flat sea's.</summary>
    public const string LowPowerDefault = Flat;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Flat, Waves };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Flat", "Waves" };

    /// <summary>The source order over <see cref="Words"/>, beaten by <c>--water-quality=</c>.</summary>
    public static readonly WordSetting Lookup = new(Key, Words, "--water-quality");

    /// <summary>The word the run resolved, <see cref="Default"/> until <see cref="Resolve"/> runs. The
    /// Options row shows it when nothing is saved, and the session's ocean follows it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the resolved word asks for the wave ocean.</summary>
    public static bool DrawsWaves => Word == Waves;

    /// <summary>The fallback word for this machine. ⚠ Asks the renderer for its GPU class, so a unit
    /// test reads <see cref="FallbackFor"/> instead.</summary>
    public static string MachineDefault => FallbackFor(OperatingSystem.IsLinux(), ShadowQualitySetting.IntegratedGpu);

    /// <summary>The fallback a launch resolves against. ⚠ <see cref="Default"/> under
    /// <paramref name="det"/>, so a scripted capture does not depend on the machine it runs on.</summary>
    public static string DefaultFor(bool det) => det ? Default : MachineDefault;

    /// <summary>The fallback on a machine that is or is not running <paramref name="linux"/>, on a GPU
    /// that is or is not <paramref name="integrated"/>. Either one takes <see cref="LowPowerDefault"/>.
    /// Linux stands for the Deck, whose SteamOS reports Linux whatever its GPU reads as.</summary>
    public static string FallbackFor(bool linux, bool integrated) => linux || integrated ? LowPowerDefault : Default;

    /// <summary>The word <see cref="Pick"/> resolves the sources to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var resolved = Pick(flagWord, savedWord, configWord, fallback);
        Word = resolved.Word;
        return resolved;
    }

    /// <summary>The word <see cref="Lookup"/> resolves the sources to over <paramref name="fallback"/>,
    /// leaving <see cref="Word"/> alone. A <see cref="LowPowerDefault"/> fallback carries its own
    /// source, so the log line says the machine chose flat.</summary>
    public static ResolvedWord Pick(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default) =>
        Lookup.Resolve(flagWord, savedWord, configWord, fallback,
            fallback == LowPowerDefault ? SettingSource.LowPowerDefault : SettingSource.Default);

    /// <summary>The launch's ladder: <paramref name="flagWord"/>, then the saved word and the config
    /// key, which <c>--det</c> both drop, over <see cref="DefaultFor"/>. An Options apply passes the
    /// word it applied as <paramref name="savedWord"/>.</summary>
    public static ResolvedWord ResolveForLaunch(string? flagWord, string? savedWord, bool det)
    {
        string fallback = DefaultFor(det);
        return Resolve(flagWord, savedWord, Config.GetString(Key, fallback), fallback);
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (<see cref="WordSetting.ReadSaved"/>).</summary>
    public static string? SavedWord(bool det) => WordSetting.ReadSaved(det, static o => o.WaterQuality);

    /// <summary>The label the Options row shows for <paramref name="word"/>, or for <see cref="Word"/>
    /// where the word is null or unknown.</summary>
    public static string Label(string? word)
    {
        int at = Lookup.IndexOf(word);
        return Labels[at >= 0 ? at : Math.Max(0, Lookup.IndexOf(Word))];
    }
}
