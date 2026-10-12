using System;
using System.Collections.Generic;
using Godot;

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

    /// <summary>The setting's own fallback, named by the fallback rule that chose it where it has one.</summary>
    Default,
}

/// <summary>What a word setting's live-apply hook reaches on the running world, the same surface for
/// every setting. <c>Launch</c> builds it over the session and the sun, so this namespace names
/// neither.</summary>
public interface ILiveWorld
{
    /// <summary>The session sun, or null where none stands.</summary>
    DirectionalLight3D? Sun { get; }

    /// <summary>Has the running session follow every word it builds from, once however many settings
    /// ask. A session ignores a word that moves nothing it built.</summary>
    void FollowSession();
}

/// <summary>One resolved word and the source that won. <paramref name="Rule"/> names the fallback
/// rule that chose a <see cref="SettingSource.Default"/> word, null for the setting's plain default.</summary>
public readonly record struct ResolvedWord(string Word, SettingSource Source, string? Rule = null);

/// <summary>What a word setting's fallback reads: whether the run is deterministic, the graphics mode,
/// and the panes the running session draws.</summary>
public readonly record struct SettingContext(bool Det, bool Enhanced, int Panes);

/// <summary>
/// One word-valued setting, registered once in <see cref="WordSettings.All"/>: its words, config key,
/// flag, options-file field, fallback, Built-in row and live-apply hook. Highest first, the lookup reads
/// the flag, the saved word, the config key, then the fallback. A word outside <see cref="Words"/> reads
/// as never set and falls through. The options store, the spec parser, both Options screens, the launch
/// and the apply walk the registry. Each setting keeps its own rules in its own file, such as the
/// FSR 2.2 clamp in <see cref="RenderScaleSetting"/>.
/// </summary>
public sealed class WordSetting
{
    private string? _lastFlag;
    private string? _lastSaved;
    private bool _lastDet;
    private ResolvedWord? _lastFallback;
    private ResolvedWord? _lastResolved;

    /// <summary>A lookup over <paramref name="words"/> under the config <paramref name="key"/>, beaten
    /// by <paramref name="flag"/> where the setting has one.</summary>
    public WordSetting(string key, IReadOnlyList<string> words, string? flag = null)
    {
        Key = key;
        Words = words;
        Flag = flag;
        Labels = words;
    }

    /// <summary>The config key, read after the saved word.</summary>
    public string Key { get; }

    /// <summary>Every word the setting accepts, in the order a row offers them.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>The command-line flag that beats the saved word, spelt without its <c>=</c>, or null
    /// for a setting that has none.</summary>
    public string? Flag { get; }

    /// <summary>The field the options file carries the saved word under. ⚠ Never rename one: a file
    /// saved by an earlier build would lose the player's word.</summary>
    public string SavedField { get; init; } = string.Empty;

    /// <summary>The name the resolution log lines give the word, its source beside it.</summary>
    public string LogName { get; init; } = string.Empty;

    /// <summary>The Built-in Options row's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What a row shows for each of <see cref="Words"/>, index for index.</summary>
    public IReadOnlyList<string> Labels { get; init; }

    /// <summary>Whether a row steps only under Enhanced, keeping its word under Original.</summary>
    public bool EnhancedOnly { get; init; }

    /// <summary>Whether a row stops at its ends rather than wrapping.</summary>
    public bool Clamps { get; init; }

    /// <summary>Whether the Built-in row stands under the graphics mode's row, which it belongs to,
    /// rather than after the display rows.</summary>
    public bool UnderGraphicsRow { get; init; }

    /// <summary>What a row says it does, under Enhanced or not.</summary>
    public Func<bool, string> Detail { get; init; } = static _ => string.Empty;

    /// <summary>The word a never-set row stands on, under Enhanced or not.</summary>
    public Func<bool, string> Shown { get; init; } = static _ => string.Empty;

    /// <summary>The words a row offers under the other staged words and the mode, or null for all of
    /// <see cref="Words"/>.</summary>
    public Func<WordValues, bool, IReadOnlyList<string>>? Offered { get; init; }

    /// <summary>What a pick does to the other staged words, or null for nothing.</summary>
    public Action<WordValues, string>? Picked { get; init; }

    /// <summary>The fallback word and the rule that chose it, for the run's context.</summary>
    public Func<SettingContext, ResolvedWord> Fallback { get; init; } = static _ => default;

    /// <summary>Stores a resolved word where the setting's readers look, and returns what stands.</summary>
    public Func<ResolvedWord, ResolvedWord> Take { get; init; } = static resolved => resolved;

    /// <summary>Puts the stored word on the running world, or null where nothing there follows it.</summary>
    public Action<ILiveWorld>? Apply { get; init; }

    /// <summary>A note the log line adds after the source, such as a clamp, or null.</summary>
    public Func<string?>? LogNote { get; init; }

    /// <summary>What the last <see cref="ResolveFrom"/> stored.</summary>
    public ResolvedWord Current { get; private set; }

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

    /// <summary>The label for <paramref name="word"/>, or the word itself where it is not one of
    /// <see cref="Words"/>.</summary>
    public string Label(string word)
    {
        int at = IndexOf(word);
        return at >= 0 ? Labels[at] : word;
    }

    /// <summary>The word a row stands on: the staged word, or <see cref="Shown"/> where none is.</summary>
    public string Standing(WordValues staged, bool enhanced) =>
        IsWord(staged[this]) ? staged[this]! : Shown(enhanced);

    /// <summary>The word the sources resolve to, highest first: <paramref name="flagWord"/>,
    /// <paramref name="savedWord"/>, <paramref name="configWord"/>, then <paramref name="fallback"/>
    /// named by <paramref name="fallbackRule"/>. A config key spelling the fallback reads as the
    /// fallback, since the absent key means the same; the caller reads the key with that fallback.
    /// An unknown config word warns when the lookup reaches it.</summary>
    public ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord, string fallback,
        string? fallbackRule = null)
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

        return new ResolvedWord(fallback, SettingSource.Default, fallbackRule);
    }

    /// <summary>The run's word from <paramref name="flagWord"/>, <paramref name="savedWord"/>, the
    /// config key and <see cref="Fallback"/> under <paramref name="context"/>, stored through
    /// <see cref="Take"/>. The inputs are kept for <see cref="Refresh"/>.</summary>
    public ResolvedWord ResolveFrom(string? flagWord, string? savedWord, SettingContext context)
    {
        _lastFlag = flagWord;
        _lastSaved = savedWord;
        _lastDet = context.Det;
        var fallback = Fallback(context);
        _lastFallback = fallback;
        var resolved = Resolve(flagWord, savedWord, Config.GetString(Key, fallback.Word), fallback.Word, fallback.Rule);
        _lastResolved = resolved;
        Current = Take(resolved);
        return Current;
    }

    /// <summary>Runs <see cref="Take"/> again on the last resolved word, for a setting whose stored word
    /// reads another setting's, as the render scale reads the anti-aliasing method.</summary>
    public void Retake()
    {
        if (_lastResolved is { } resolved)
        {
            Current = Take(resolved);
        }
    }

    /// <summary>The last <see cref="ResolveFrom"/> again under a mode and a pane count that may have
    /// moved, when they move the fallback. Returns whether the stored word changed. ⚠ Leave a setting
    /// never resolved, or whose fallback stands: a suite may have stored a word of its own.</summary>
    public bool Refresh(bool enhanced, int panes)
    {
        var context = new SettingContext(_lastDet, enhanced, panes);
        if (_lastFallback is not { } last || Fallback(context) == last)
        {
            return false;
        }

        string before = Current.Word;
        return ResolveFrom(_lastFlag, _lastSaved, context).Word != before;
    }

    /// <summary>The word and its source as a log line spells them.</summary>
    public string Describe() =>
        $"{LogName}={Current.Word} {LogName}_source={SourceName(Current.Source, Current.Rule)}{LogNote?.Invoke()}";

    /// <summary>How a log line names <paramref name="source"/>: the flag, the options file, the config
    /// key, or the fallback <paramref name="rule"/> that chose the word, <c>default</c> where none did.</summary>
    public string SourceName(SettingSource source, string? rule = null) => source switch
    {
        SettingSource.Flag => Flag ?? "flag",
        SettingSource.Saved => OptionsStore.FileName,
        SettingSource.Config => Key,
        _ => rule ?? "default",
    };
}
