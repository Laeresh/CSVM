using System;
using System.Collections.Generic;

namespace CSVM.Session.Career;

/// <summary>
/// Every economy number the career uses, in one place. The career is remake-only, so each value
/// is a tune for playtesting, not a decode; the design's starting figures are in
/// <c>docs/career-mode.md</c>, "Economy tunes". Prices of planes, guns, engines and armour are not
/// here: they are the decoded hangar economy's (<see cref="Flight.Hangar.HangarEconomy"/>).
/// </summary>
public sealed class CareerTunes
{
    /// <summary>Days between paydays.</summary>
    public const int DaysPerWeek = 7;

    /// <summary>The defaults the design starts from.</summary>
    public static CareerTunes Default { get; } = new();

    /// <summary>Cash a new career starts with, in dollars.</summary>
    public int StartingCash { get; init; } = 6000;

    /// <summary>The moneylender's loan a new career starts owing.</summary>
    public int StartingDebt { get; init; } = 25000;

    /// <summary>The weekly instalment paid off the debt, capped at what is still owed.</summary>
    public int DebtInstalment { get; init; } = 1000;

    /// <summary>Interest added to the debt each payday, in percent of what is owed.</summary>
    public double WeeklyInterestPercent { get; init; } = 2.0;

    /// <summary>The weekly wage bill. Phase 1 has no crew roster, so the starting crew's wages are
    /// one figure: two pilots and a mechanic.</summary>
    public int WeeklyWages { get; init; } = 900;

    /// <summary>The zeppelin's weekly upkeep. Phase 1 has no modules, so this is the base alone.</summary>
    public int WeeklyUpkeep { get; init; } = 500;

    /// <summary>Airframe ids of the planes a new career starts with: a Warhawk and a Brigand.</summary>
    public IReadOnlyList<int> StartingAirframes { get; init; } = new[] { 10, 4 };

    /// <summary>The share of each armour zone a starting plane has lost, so the gang starts worn.</summary>
    public double StartingWear { get; init; } = 0.25;

    /// <summary>Dollars per armour unit restored. The hangar sells new armour at four dollars a unit.</summary>
    public int RepairCostPerUnit { get; init; } = 5;

    /// <summary>Armour units the machine shop restores in one day.</summary>
    public int RepairUnitsPerDay { get; init; } = 40;

    /// <summary>A damaged part's repair as a share of its hangar price.</summary>
    public double PartRepairShare { get; init; } = 0.5;

    /// <summary>Days one damaged part takes to repair.</summary>
    public int PartRepairDays { get; init; } = 1;

    /// <summary>Contracts the port's board holds at most.</summary>
    public int BoardSize { get; init; } = 5;

    /// <summary>The lowest threat rating the phase 1 region offers.</summary>
    public int ThreatFloor { get; init; } = 1;

    /// <summary>Shortest and longest offer, in days, before a contract expires.</summary>
    public int MinExpiryDays { get; init; } = 3;

    public int MaxExpiryDays { get; init; } = 7;

    /// <summary>Fee band per threat rating, index 0 for one skull: the lowest and highest fee.</summary>
    public IReadOnlyList<(int Min, int Max)> FeeBands { get; init; } = new[]
    {
        (1500, 2500), (3000, 5000), (5000, 8000), (7500, 12000), (10000, 16000),
    };

    /// <summary>Price of a hundred slug rounds per calibre, index 0 for .30 through 4 for .70.</summary>
    public IReadOnlyList<int> SlugPricePer100 { get; init; } = new[] { 8, 10, 12, 14, 16 };

    /// <summary>Price multiplier of each ammunition kind over slug, by kind.</summary>
    public IReadOnlyDictionary<AmmoKind, int> KindPriceFactor { get; init; } = new Dictionary<AmmoKind, int>
    {
        [AmmoKind.Slug] = 1,
        [AmmoKind.DumDum] = 2,
        [AmmoKind.ArmourPiercing] = 3,
        [AmmoKind.Magnesium] = 3,
    };

    /// <summary>Most rounds of one specialist kind and calibre a shelf stocks in a week.</summary>
    public int MaxSpecialistRounds { get; init; } = 600;

    /// <summary>Price of one rocket by <c>wep_*</c> id; an id not listed costs <see cref="DefaultRocketPrice"/>.</summary>
    public IReadOnlyDictionary<string, int> RocketPrices { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["wep_04"] = 80,
        ["wep_05"] = 90,
        ["wep_06"] = 60,
        ["wep_07"] = 70,
        ["wep_08"] = 120,
        ["wep_09"] = 100,
        ["wep_10"] = 150,
        ["wep_11"] = 200,
        ["wep_12"] = 140,
        ["wep_13"] = 40,
        ["wep_14"] = 250,
        ["wep_15"] = 50,
    };

    public int DefaultRocketPrice { get; init; } = 100;

    /// <summary>Most rockets of one id a shelf stocks in a week.</summary>
    public int MaxRocketsPerId { get; init; } = 8;

    /// <summary>The fee band for <paramref name="threat"/>, clamped to one to five skulls.</summary>
    public (int Min, int Max) FeeBand(int threat) => FeeBands[Math.Clamp(threat, 1, FeeBands.Count) - 1];

    /// <summary>The price of one rocket of <paramref name="weaponId"/>.</summary>
    public int RocketPrice(string weaponId) =>
        RocketPrices.TryGetValue(weaponId, out int price) ? price : DefaultRocketPrice;
}
