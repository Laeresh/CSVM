using System;
using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>Which source a word setting's resolved word came from, highest first. A log line spells
/// it through <see cref="WordSetting.SourceName"/>.</summary>
public enum SettingSource
{
    /// <summary>The setting's command-line flag.</summary>
    Flag,

    /// <summary>The word saved in the options file.</summary>
    Saved,

    /// <summary>The setting's config key.</summary>
    Config,

    /// <summary>The setting's own fallback.</summary>
    Default,

    /// <summary>Shadow quality's fallback on an integrated GPU.</summary>
    IntegratedGpuDefault,

    /// <summary>Shadow quality's fallback on an integrated GPU drawing three or more panes.</summary>
    IntegratedGpuPanesDefault,

    /// <summary>Water quality's fallback on Linux or on an integrated GPU, the Steam Deck being both.</summary>
    LowPowerDefault,
}

/// <summary>One resolved word and the source that won.</summary>
public readonly record struct ResolvedWord(string Word, SettingSource Source);

/// <summary>
/// The lookup a word-valued graphics setting shares. Highest first, it reads the setting's flag, the
/// saved word, the config key, then a fallback the setting chooses. A word outside
/// <see cref="Words"/> reads as never set and falls through. Each setting keeps its own rules beside
/// this, such as the mode-dependent fallback in <see cref="AntiAliasingSetting"/> and the FSR 2.2 clamp
/// in <see cref="RenderScaleSetting"/>. Others are the GPU fallback in <see cref="ShadowQualitySetting"/>,
/// the platform fallback in <see cref="WaterQualitySetting"/> and the reach in <see cref="ViewDistance"/>.
/// Engine-free, so the order tests without a renderer.
/// </summary>
public sealed class WordSetting
{
    /// <summary>A lookup over <paramref name="words"/> under the config <paramref name="key"/>, beaten
    /// by <paramref name="flag"/> where the setting has one.</summary>
    public WordSetting(string key, IReadOnlyList<string> words, string? flag = null)
    {
        Key = key;
        Words = words;
        Flag = flag;
    }

    /// <summary>The config key, read after the saved word.</summary>
    public string Key { get; }

    /// <summary>Every word the setting accepts, in the order a row offers them.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>The command-line flag that beats the saved word, spelt without its <c>=</c>, or null
    /// for a setting that has none.</summary>
    public string? Flag { get; }

    /// <summary>The saved word <paramref name="field"/> picks out of the options file, or null under
    /// <paramref name="det"/>. ⚠ Do not read a saved display word under <c>--det</c>. The file is one
    /// machine's state, and a golden shot is a <c>--det</c> run against the player's own directory.</summary>
    public static string? ReadSaved(bool det, Func<OptionsDef, string?> field) =>
        det ? null : field(OptionsStore.UserOptions().Load());

    /// <summary>Whether <paramref name="word"/> is one of <see cref="Words"/>.</summary>
    public bool IsWord(string? word) => IndexOf(word) >= 0;

    /// <summary>The position of <paramref name="word"/> in <see cref="Words"/>, or -1 for a null or
    /// unknown word.</summary>
    public int IndexOf(string? word)
    {
        for (int i = 0; i < Words.Count; i++)
        {
            if (Words[i] == word)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The word the sources resolve to, highest first: <paramref name="flagWord"/>,
    /// <paramref name="savedWord"/>, <paramref name="configWord"/>, then <paramref name="fallback"/>
    /// under <paramref name="fallbackSource"/>. A config key spelling the fallback reads as the
    /// fallback, since the absent key means the same; the caller reads the key with that fallback.
    /// An unknown config word warns when the lookup reaches it.</summary>
    public ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord, string fallback,
        SettingSource fallbackSource = SettingSource.Default)
    {
        if (IsWord(flagWord))
        {
            return new ResolvedWord(flagWord!, SettingSource.Flag);
        }

        if (IsWord(savedWord))
        {
            return new ResolvedWord(savedWord!, SettingSource.Saved);
        }

        if (configWord != fallback && IsWord(configWord))
        {
            return new ResolvedWord(configWord!, SettingSource.Config);
        }

        if (configWord != null && !IsWord(configWord))
        {
            Log.Warn("world", $"config {Key}={configWord} is not one of {string.Join("/", Words)}; using {fallback}");
        }

        return new ResolvedWord(fallback, fallbackSource);
    }

    /// <summary>How a log line names <paramref name="source"/>: the flag, the options file, the config
    /// key, or the fallback rule that chose the word.</summary>
    public string SourceName(SettingSource source) => source switch
    {
        SettingSource.Flag => Flag ?? "flag",
        SettingSource.Saved => OptionsStore.FileName,
        SettingSource.Config => Key,
        SettingSource.IntegratedGpuDefault => "default_integrated_gpu",
        SettingSource.IntegratedGpuPanesDefault => "default_integrated_gpu_panes",
        SettingSource.LowPowerDefault => "default_linux_or_integrated_gpu",
        _ => "default",
    };
}
