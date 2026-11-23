using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVM.Session.Career;

/// <summary>
/// The port's ammunition shelf. Slug rounds are always for sale; specialist rounds and rockets
/// come from a stock rolled each payday, so a market can run out. Gun rounds sell in lots of a
/// hundred. Phase 1 sells ammunition only; planes and parts are the full market's, a later phase.
/// </summary>
public sealed class PortShelf
{
    /// <summary>Rounds in one lot, the unit the shelf sells and prices.</summary>
    public const int RoundsPerLot = 100;

    private static readonly AmmoKind[] SpecialistKinds = { AmmoKind.DumDum, AmmoKind.ArmourPiercing, AmmoKind.Magnesium };

    private readonly Dictionary<AmmoLine, int> _rounds = new();
    private readonly Dictionary<string, int> _rockets = new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<AmmoLine, int>> SpecialistStock =>
        _rounds.OrderBy(p => p.Key.Calibre).ThenBy(p => p.Key.Kind);

    public IEnumerable<KeyValuePair<string, int>> RocketStock => _rockets.OrderBy(p => p.Key, StringComparer.Ordinal);

    /// <summary>The price of one lot of <paramref name="line"/>.</summary>
    public static int LotPrice(AmmoLine line, CareerTunes tunes) =>
        tunes.RoundPricePer100[Math.Clamp(line.Calibre, 0, tunes.RoundPricePer100.Count - 1)] * tunes.KindPriceFactor[line.Kind];

    /// <summary>Lots of <paramref name="line"/> on the shelf; slug answers <see cref="int.MaxValue"/>.</summary>
    public int LotsOf(AmmoLine line) =>
        line.Kind == AmmoKind.Slug ? int.MaxValue : _rounds.TryGetValue(line, out int n) ? n / RoundsPerLot : 0;

    public int RocketsOf(string weaponId) => _rockets.TryGetValue(weaponId, out int n) ? n : 0;

    /// <summary>Rolls the week's stock, replacing what was left. Pure in the seed and the week.</summary>
    public void Restock(ulong careerSeed, int week, CareerTunes tunes)
    {
        _rounds.Clear();
        _rockets.Clear();
        var dice = CareerDice.For(careerSeed, "shelf", week);
        int draw = 0;
        int maxLots = tunes.MaxSpecialistRounds / RoundsPerLot;
        for (int calibre = 0; calibre < tunes.RoundPricePer100.Count; calibre++)
        {
            foreach (var kind in SpecialistKinds)
            {
                int lots = dice.Range(draw++, 0, maxLots);
                if (lots > 0)
                {
                    _rounds[new AmmoLine(calibre, kind)] = lots * RoundsPerLot;
                }
            }
        }

        foreach (string id in tunes.RocketPrices.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            int count = dice.Range(draw++, 0, tunes.MaxRocketsPerId);
            if (count > 0)
            {
                _rockets[id] = count;
            }
        }
    }

    /// <summary>Buys <paramref name="lots"/> lots into the hold. Refused, changing nothing, when the
    /// shelf lacks them or cash cannot cover them.</summary>
    public bool BuyRounds(AmmoLine line, int lots, int day, Hold hold, Ledger ledger, CareerTunes tunes)
    {
        if (lots <= 0 || lots > LotsOf(line))
        {
            return false;
        }

        int price = lots * LotPrice(line, tunes);
        if (price > 0 && !ledger.TrySpend(day, LedgerCategory.Ammunition, price, Describe(line, lots)))
        {
            return false;
        }

        if (line.Kind != AmmoKind.Slug)
        {
            _rounds[line] -= lots * RoundsPerLot;
        }

        hold.AddRounds(line, lots * RoundsPerLot);
        return true;
    }

    /// <summary>Buys <paramref name="count"/> rockets into the hold, refused as <see cref="BuyRounds"/> is.</summary>
    public bool BuyRockets(string weaponId, int count, int day, Hold hold, Ledger ledger, CareerTunes tunes)
    {
        if (count <= 0 || count > RocketsOf(weaponId))
        {
            return false;
        }

        if (!ledger.TrySpend(day, LedgerCategory.Ammunition, count * tunes.RocketPrice(weaponId), FormattableString.Invariant($"{count} x {weaponId}")))
        {
            return false;
        }

        _rockets[weaponId] -= count;
        hold.AddRockets(weaponId, count);
        return true;
    }

    /// <summary>Restores a saved shelf exactly.</summary>
    internal void Restore(IEnumerable<KeyValuePair<AmmoLine, int>> rounds, IEnumerable<KeyValuePair<string, int>> rockets)
    {
        _rounds.Clear();
        _rockets.Clear();
        foreach (var (line, n) in rounds)
        {
            _rounds[line] = n;
        }

        foreach (var (id, n) in rockets)
        {
            _rockets[id] = n;
        }
    }

    private static string Describe(AmmoLine line, int lots) =>
        FormattableString.Invariant($"{lots * RoundsPerLot} x .{(line.Calibre + 3) * 10} {line.Kind}");
}
