using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// A store of decoded world inputs, keyed by chapter or by the paths they were decoded from. A
/// process that builds one chapter many times pays each decode once. Opt-in and instance scoped:
/// a caller holding no instance decodes afresh, so a game session retains no chapter it left.
/// ⚠ Every object handed back here is SHARED, and read-only by contract. <see cref="GameZ"/> and
/// <see cref="AnimProgram"/> are graphs of public mutable fields with nothing to enforce that.
/// One write reaches every later build of the same chapter. Who may hold one, and what the
/// contract binds: this module's entry in docs/architecture.md.
/// </summary>
public sealed class DecodeCache
{
    // Illegal in a Windows path, so no five real paths can spell another combination's key. A
    // space would not hold, since the paths here routinely contain one.
    private const char KeySeparator = '|';

    private readonly Dictionary<string, GameZ?> _gamez = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimProgram> _anim = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many lookups were served from the store, and how many decoded. Reported so a
    /// run can show the cache actually hit rather than that it merely exists.</summary>
    public int Hits { get; private set; }

    public int Misses { get; private set; }

    /// <summary>The world of <paramref name="chapter"/>, from <paramref name="load"/> on a miss, null
    /// where it has none. The chapter is the whole key: one store serves one launch's game data, and
    /// no build option changes what is decoded.</summary>
    public GameZ? Gamez(string chapter, Func<GameZ?> load) => Get(_gamez, chapter, _ => load());

    /// <summary>The merged animation program for one chapter+mission. All five paths are in the
    /// key because all five are inputs to the merge, and between them they carry the data root,
    /// the chapter and the mission.</summary>
    public AnimProgram Anim(string sharedZrdr, string chapterZrdr, string missionZrdr,
        string chapterAnim, string missionAnim)
    {
        string key = string.Join(KeySeparator, sharedZrdr, chapterZrdr, missionZrdr,
            chapterAnim, missionAnim);
        return Get(_anim, key, _ => AnimProgram.Load(sharedZrdr, chapterZrdr, missionZrdr,
            chapterAnim, missionAnim));
    }

    private T Get<T>(Dictionary<string, T> store, string key, Func<string, T> load)
    {
        if (store.TryGetValue(key, out var hit))
        {
            Hits++;
            return hit;
        }
        Misses++;
        var loaded = load(key);
        store[key] = loaded;
        return loaded;
    }
}
