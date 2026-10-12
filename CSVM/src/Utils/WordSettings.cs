using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVM.Utils;

/// <summary>
/// The registry of word settings, each one <see cref="WordSetting"/> held by its own file. The options
/// store, the spec parser, both Options screens, the launch, the apply and the session walk
/// <see cref="All"/> rather than naming a setting. A new setting is its own file and one entry here,
/// plus an authored row where the Original VIDEO page shows it. One whose word changes what the session
/// builds also adds that build to <c>EnhancedLook.FollowWordSettings</c>, the code its effect lives in.
/// </summary>
public static class WordSettings
{
    /// <summary>Every word setting, in the order the Built-in Options screen shows its rows within each
    /// row group (<see cref="WordSetting.UnderGraphicsRow"/>).</summary>
    public static IReadOnlyList<WordSetting> All { get; } = new[]
    {
        RenderScaleSetting.Lookup,
        AntiAliasingSetting.Lookup,
        ShadowQualitySetting.Lookup,
        WaterQualitySetting.Lookup,
        ViewDistance.Lookup,
    };

    /// <summary>The panes the running session draws, which a fallback may read. 1 until
    /// <see cref="FollowPanes"/> names a splitscreen session's count.</summary>
    public static int Panes { get; private set; } = 1;

    /// <summary>The words the options file saved, or none under <paramref name="det"/>. ⚠ Read no saved
    /// word under <c>--det</c>. The file is one machine's state, and a golden shot is a <c>--det</c>
    /// run against the player's own directory.</summary>
    public static WordValues ReadSaved(bool det) => det ? new WordValues() : OptionsStore.UserOptions().Load().Words;

    /// <summary>The launch's resolve: every setting from <paramref name="flags"/> and the saved words
    /// <see cref="ReadSaved"/> gives under <paramref name="det"/>.</summary>
    public static void ResolveLaunch(WordValues flags, bool det) => ResolveAll(flags, ReadSaved(det), det);

    /// <summary>Every setting resolved from <paramref name="flags"/>, <paramref name="saved"/>, its
    /// config key and its fallback under the standing graphics mode and pane count.</summary>
    public static void ResolveAll(WordValues flags, WordValues saved, bool det)
    {
        var context = new SettingContext(det, GraphicsMode.Enhanced, Panes);
        foreach (var setting in All)
        {
            setting.ResolveFrom(flags[setting], saved[setting], context);
        }
    }

    /// <summary>The settings whose fallback the standing graphics mode moves, resolved again. A live
    /// mode switch runs it.</summary>
    public static void FollowMode()
    {
        foreach (var setting in All)
        {
            setting.Refresh(GraphicsMode.Enhanced, Panes);
        }
    }

    /// <summary>The settings whose fallback a session drawing <paramref name="panes"/> moves, resolved
    /// again. Returns those whose word changed, for the caller to apply.</summary>
    public static IReadOnlyList<WordSetting> FollowPanes(int panes)
    {
        Panes = Math.Max(1, panes);
        return All.Where(setting => setting.Refresh(GraphicsMode.Enhanced, Panes)).ToList();
    }

    /// <summary>Every setting's stored word on the running <paramref name="world"/>.</summary>
    public static void ApplyAll(ILiveWorld world)
    {
        foreach (var setting in All)
        {
            setting.Apply?.Invoke(world);
        }
    }

    /// <summary>Every setting's word and source, one log line's worth.</summary>
    public static string Describe() => string.Join(" ", All.Select(setting => setting.Describe()));
}

/// <summary>One word per registered setting, null where never set. The options file, a screen's staged
/// choices, an apply and a command line each carry one. Two holding the same words are equal, so a
/// record carrying one compares by value.</summary>
public sealed class WordValues : IEquatable<WordValues>
{
    private readonly Dictionary<WordSetting, string> _words = new();

    /// <summary>The word stored for <paramref name="setting"/>, null where never set. Setting null
    /// clears it.</summary>
    public string? this[WordSetting setting]
    {
        get => _words.TryGetValue(setting, out string? word) ? word : null;
        set
        {
            if (value == null)
            {
                _words.Remove(setting);
            }
            else
            {
                _words[setting] = value;
            }
        }
    }

    /// <summary>A copy that later writes to either leave the other alone.</summary>
    public WordValues Copy()
    {
        var copy = new WordValues();
        foreach (var (setting, word) in _words)
        {
            copy._words[setting] = word;
        }

        return copy;
    }

    /// <inheritdoc/>
    public bool Equals(WordValues? other) =>
        other != null && other._words.Count == _words.Count
        && _words.All(pair => other._words.TryGetValue(pair.Key, out string? word) && word == pair.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WordValues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var (setting, word) in _words)
        {
            hash ^= HashCode.Combine(setting, word);
        }

        return hash;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Join(" ", _words.Select(pair => $"{pair.Key.SavedField}={pair.Value}"));
}
