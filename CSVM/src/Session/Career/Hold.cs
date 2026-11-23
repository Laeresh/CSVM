using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVM.Session.Career;

/// <summary>The four gun ammunition kinds, the vocabulary of <c>docs/formats/loadouts.md</c>.</summary>
public enum AmmoKind
{
    Slug,
    DumDum,
    ArmourPiercing,
    Magnesium,
}

/// <summary>One stock line of gun ammunition: a calibre id (0 for .30 through 4 for .70) and a kind.</summary>
public readonly record struct AmmoLine(int Calibre, AmmoKind Kind);

/// <summary>
/// The zeppelin's hold: gun rounds counted per calibre and kind, and rockets counted per
/// <c>wep_*</c> id. A flight check loads from it and a debrief returns what was not fired, so
/// ammunition is paid for once, when it is bought. Planes and parts join the hold in a later phase.
/// </summary>
public sealed class Hold
{
    private readonly Dictionary<AmmoLine, int> _rounds = new();
    private readonly Dictionary<string, int> _rockets = new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<AmmoLine, int>> Rounds =>
        _rounds.Where(p => p.Value > 0).OrderBy(p => p.Key.Calibre).ThenBy(p => p.Key.Kind);

    public IEnumerable<KeyValuePair<string, int>> Rockets =>
        _rockets.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal);

    public int RoundsOf(AmmoLine line) => _rounds.TryGetValue(line, out int n) ? n : 0;

    public int RocketsOf(string weaponId) => _rockets.TryGetValue(weaponId, out int n) ? n : 0;

    public void AddRounds(AmmoLine line, int count) => _rounds[line] = RoundsOf(line) + Positive(count);

    public void AddRockets(string weaponId, int count) => _rockets[weaponId] = RocketsOf(weaponId) + Positive(count);

    /// <summary>Takes up to <paramref name="count"/> rounds and answers how many it took.</summary>
    public int TakeRounds(AmmoLine line, int count)
    {
        int taken = Math.Min(RoundsOf(line), Positive(count));
        _rounds[line] = RoundsOf(line) - taken;
        return taken;
    }

    /// <summary>Takes up to <paramref name="count"/> rockets and answers how many it took.</summary>
    public int TakeRockets(string weaponId, int count)
    {
        int taken = Math.Min(RocketsOf(weaponId), Positive(count));
        _rockets[weaponId] = RocketsOf(weaponId) - taken;
        return taken;
    }

    private static int Positive(int count) =>
        count >= 0 ? count : throw new ArgumentOutOfRangeException(nameof(count), "a stock count is never negative");
}
