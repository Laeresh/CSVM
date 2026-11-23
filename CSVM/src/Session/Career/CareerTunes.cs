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

    /// <summary>Opposition budget per skull of threat, in the same dollars as the hangar's prices.</summary>
    public int BudgetPerSkull { get; init; } = 10000;

    /// <summary>The share of the air budget held back for the one reserve wave.</summary>
    public double ReserveShare { get; init; } = 0.33;

    /// <summary>A plane's value multiplier per pilot skill tier, rookie to ace.</summary>
    public IReadOnlyList<double> SkillFactors { get; init; } = new[] { 0.8, 1.0, 1.3, 1.8 };

    /// <summary>The share of a strike's budget spent on flak batteries, and one battery's value.</summary>
    public double FlakShare { get; init; } = 0.3;

    public int FlakBatteryValue { get; init; } = 1500;

    public int MaxFlakBatteries { get; init; } = 4;

    /// <summary>Fewest and most ground targets a strike places.</summary>
    public int MinStrikeTargets { get; init; } = 2;

    public int MaxStrikeTargets { get; init; } = 5;

    /// <summary>How far, in metres, a strike's targets may lie from its first target.</summary>
    public float StrikeRadius { get; init; } = 2500f;

    /// <summary>How far, in metres, the flight starts from the area's centre.</summary>
    public float StartDistance { get; init; } = 3000f;

    /// <summary>The lowest altitude, in metres, the flight or an enemy starts at.</summary>
    public float MinStartAltitude { get; init; } = 300f;

    /// <summary>The radius, in metres, of the ring a strike's defenders start on.</summary>
    public float DefenderRing { get; init; } = 800f;

    /// <summary>Seconds after the start a patrol's reserve wave arrives.</summary>
    public int ReserveDelaySeconds { get; init; } = 120;

    /// <summary>The steepest ground, in degrees, a target's footprint may stand on.</summary>
    public float MaxPlacementSlopeDeg { get; init; } = 8f;

    /// <summary>How far, in metres, the placer may move a target from its requested point.</summary>
    public float MaxPlacementShift { get; init; } = 300f;

    /// <summary>The spacing, in metres, of the placer's search rings.</summary>
    public float PlacementSearchStep { get; init; } = 50f;

    /// <summary>Each prefab's footprint radius in metres; a prefab not listed takes the default.</summary>
    public IReadOnlyDictionary<string, float> PrefabFootprints { get; init; } = new Dictionary<string, float>(StringComparer.Ordinal)
    {
        ["fuel_depot"] = 40f,
        ["supply_dump"] = 30f,
        ["flak_battery"] = 15f,
    };

    public float DefaultFootprint { get; init; } = 30f;

    /// <summary>The share of the fee each secondary objective met adds.</summary>
    public double SecondaryBonusShare { get; init; } = 0.25;

    /// <summary>Days a sortie takes, travel to the area and back included.</summary>
    public int SortieDays { get; init; } = 1;

    /// <summary>The fee band for <paramref name="threat"/>, clamped to one to five skulls.</summary>
    public (int Min, int Max) FeeBand(int threat) => FeeBands[Math.Clamp(threat, 1, FeeBands.Count) - 1];

    /// <summary>The price of one rocket of <paramref name="weaponId"/>.</summary>
    public int RocketPrice(string weaponId) =>
        RocketPrices.TryGetValue(weaponId, out int price) ? price : DefaultRocketPrice;
}
