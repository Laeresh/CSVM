using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// A store of decoded world inputs, keyed by the absolute paths they were decoded from, so a
/// process that builds the same chapter many times pays each decode once. Opt-in and instance
/// scoped: a caller holding no instance loads exactly as before, which is why a game session
/// never retains a chapter it has left.
/// ⚠ Every object handed back here is SHARED, and read-only by contract. <see cref="GameZ"/> and
/// <see cref="AnimProgram"/> are graphs of public mutable fields with nothing to enforce that, so
/// one write reaches every later build of the same chapter. Who may hold one, and what the
/// contract binds: this module's entry in docs/architecture.md.
/// </summary>
public sealed class DecodeCache
{
    // Illegal in a Windows path, so no five real paths can spell another combination's key. A
    // space would not hold, since the paths here routinely contain one.
    private const char KeySeparator = '|';

    private readonly Dictionary<string, GameZ> _gamez = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimProgram> _anim = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many lookups were served from the store, and how many decoded. Reported so a
    /// run can show the cache actually hit rather than that it merely exists.</summary>
    public int Hits { get; private set; }

    public int Misses { get; private set; }

    /// <summary>The chapter (or shared aircraft) gamez at <paramref name="path"/>. The path is the
    /// whole key: it already carries the data root and the chapter, and no build option changes
    /// what is decoded from it.</summary>
    public GameZ Gamez(string path) => Get(_gamez, path, GameZ.Load);

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
