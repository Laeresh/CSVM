using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using Godot;

namespace CSVM.Session.Career;

/// <summary>
/// Turns a contract and a chapter's site catalogue into a <see cref="MissionSpec"/>. Every draw
/// comes from the contract's seed, so one contract always yields one mission. The threat rating
/// becomes an opposition budget in hangar dollars, and enemies are bought against it at their
/// hangar price times a skill factor. A third of the air budget is held back as one reserve wave.
/// Answers null when the catalogue lacks the sites the job needs.
/// </summary>
public static class MissionGenerator
{
    public const string FlakPrefab = "flak_battery";

    /// <summary>The most wingman seats a sortie offers.</summary>
    public const int MaxWingmen = 3;

    /// <summary>The prefab ids a strike places. The placer (B13) defines what each one is built from.</summary>
    public static readonly string[] TargetPrefabs = { "fuel_depot", "supply_dump" };

    /// <summary>
    /// One enemy plane's value before skill: its airframe, the middle engine and the stock armour,
    /// at the decoded hangar prices. Guns are left out, because the stock fit is data the session
    /// reads (<c>CSVM/data/stock_loadouts.json</c>); every airframe's guns cost a few hundred dollars.
    /// </summary>
    public static int PlaneValue(int airframe) =>
        HangarEconomy.Airframes[airframe].Cost
        + HangarEconomy.EngineLine(airframe, 1).Cost
        + (HangarEconomy.StockArmourUnits[airframe] * HangarEconomy.ArmourUnitCost);

    /// <summary>The yaw in degrees that noses an aircraft at <paramref name="from"/> toward
    /// <paramref name="to"/>, in the spawn convention: yaw 0 faces -Z.</summary>
    public static float HeadingTo(Vector3 from, Vector3 to) =>
        Mathf.RadToDeg(MathF.Atan2(from.X - to.X, from.Z - to.Z));

    /// <summary>The spec for <paramref name="contract"/>, or null when the catalogue cannot host it.</summary>
    public static MissionSpec? Generate(Contract contract, SiteCatalogue catalogue, int wingmen, CareerTunes? tunes = null)
    {
        tunes ??= CareerTunes.Default;
        var draw = new Draws(CareerDice.For(contract.Seed, "mission"));
        return contract.Job switch
        {
            JobType.Patrol => Patrol(contract, catalogue, wingmen, tunes, draw),
            _ => Strike(contract, catalogue, wingmen, tunes, draw),
        };
    }

    private static MissionSpec? Patrol(Contract contract, SiteCatalogue catalogue, int wingmen, CareerTunes tunes, Draws draw)
    {
        if (catalogue.Routes.Count == 0)
        {
            return null;
        }

        var route = catalogue.Routes[draw.Range(0, catalogue.Routes.Count - 1)];
        var centre = route.Centre;
        var (start, heading, bearing) = Approach(centre, tunes, draw);
        int budget = contract.Threat * tunes.BudgetPerSkull;
        var enemies = BuyAir(budget, contract.Threat, tunes, draw);
        var placed = new List<EnemyFlight>();
        int onRoute = 0;
        foreach (var (airframe, skill, value, wave) in enemies)
        {
            if (wave == EnemyWave.Initial)
            {
                var p = route.Points[onRoute++ % route.Points.Count];
                var at = new Vector3(p.X, Mathf.Max(p.Y, tunes.MinStartAltitude), p.Z);
                placed.Add(new EnemyFlight(airframe, skill, at, HeadingTo(at, centre), route.Net, wave, value));
            }
            else
            {
                placed.Add(Reserve(airframe, skill, value, centre, bearing, tunes));
            }
        }

        var objectives = new List<MissionObjective> { new(ObjectiveKind.DestroyWave, true, EnemyWave.Initial) };
        if (placed.Any(e => e.Wave == EnemyWave.Reserve))
        {
            objectives.Add(new MissionObjective(ObjectiveKind.DestroyWave, false, EnemyWave.Reserve));
        }

        return Spec(contract, centre, start, heading, wingmen, route.Net, placed, new List<TargetPlacement>(), objectives, ReserveTrigger.TimeElapsed, budget, tunes);
    }

    private static MissionSpec? Strike(Contract contract, SiteCatalogue catalogue, int wingmen, CareerTunes tunes, Draws draw)
    {
        if (catalogue.GroundCandidates.Count == 0)
        {
            return null;
        }

        var anchor = catalogue.GroundCandidates[draw.Range(0, catalogue.GroundCandidates.Count - 1)];
        var cluster = catalogue.CandidatesNear(new Vector3(anchor.X, 0, anchor.Z), tunes.StrikeRadius)
            .OrderBy(c => ((c.X - anchor.X) * (c.X - anchor.X)) + ((c.Z - anchor.Z) * (c.Z - anchor.Z)))
            .Take(draw.Range(tunes.MinStrikeTargets, tunes.MaxStrikeTargets))
            .ToList();
        var centre = new Vector3(cluster.Average(c => c.X), cluster.Average(c => c.Altitude), cluster.Average(c => c.Z));
        var (start, heading, bearing) = Approach(centre, tunes, draw);
        int budget = contract.Threat * tunes.BudgetPerSkull;

        var targets = cluster
            .Select(c => new TargetPlacement(TargetPrefabs[draw.Range(0, TargetPrefabs.Length - 1)], c.X, c.Z, 0))
            .ToList();
        int flak = Math.Min(tunes.MaxFlakBatteries, (int)(budget * tunes.FlakShare) / tunes.FlakBatteryValue);
        for (int i = 0; i < flak; i++)
        {
            // Each battery stands 200 m off a target, on a side drawn per battery.
            var t = cluster[i % cluster.Count];
            float side = (float)(draw.Unit() * Math.Tau);
            targets.Add(new TargetPlacement(FlakPrefab, t.X + (200f * MathF.Sin(side)), t.Z + (200f * MathF.Cos(side)), tunes.FlakBatteryValue));
        }

        int air = budget - (flak * tunes.FlakBatteryValue);
        var placed = new List<EnemyFlight>();
        var bought = BuyAir(air, contract.Threat, tunes, draw);
        int initialCount = bought.Count(b => b.Wave == EnemyWave.Initial);
        int ring = 0;
        foreach (var (airframe, skill, value, wave) in bought)
        {
            if (wave == EnemyWave.Initial)
            {
                float angle = (float)(Math.Tau * ring++ / Math.Max(1, initialCount));
                var at = centre + new Vector3(tunes.DefenderRing * MathF.Sin(angle), 0, tunes.DefenderRing * MathF.Cos(angle));
                at.Y = Mathf.Max(centre.Y, tunes.MinStartAltitude);
                placed.Add(new EnemyFlight(airframe, skill, at, Mathf.RadToDeg(angle) + 90f, null, wave, value));
            }
            else
            {
                placed.Add(Reserve(airframe, skill, value, centre, bearing, tunes));
            }
        }

        var objectives = new List<MissionObjective>
        {
            new(ObjectiveKind.DestroyTargets, true),
            new(ObjectiveKind.DestroyWave, false, EnemyWave.Initial),
        };
        return Spec(contract, centre, start, heading, wingmen, null, placed, targets, objectives, ReserveTrigger.FirstTargetDestroyed, budget, tunes);
    }

    // Buys aircraft until the budget cannot pay for another, the first of them always, so a sortie
    // never has an empty first wave. The reserve takes the tunes' share of the budget.
    private static List<(int Airframe, SkillTier Skill, int Value, EnemyWave Wave)> BuyAir(int budget, int threat, CareerTunes tunes, Draws draw)
    {
        var bought = new List<(int, SkillTier, int, EnemyWave)>();
        int reserve = (int)(budget * tunes.ReserveShare);
        Buy(budget - reserve, EnemyWave.Initial, alwaysOne: true);
        Buy(reserve, EnemyWave.Reserve, alwaysOne: false);
        return bought;

        void Buy(int money, EnemyWave wave, bool alwaysOne)
        {
            while (true)
            {
                int airframe = WeightedAirframe(draw);
                var skill = (SkillTier)Math.Clamp(((threat - 1) * 3 / 4) + draw.Range(-1, 1), 0, 3);
                int value = (int)Math.Round(PlaneValue(airframe) * tunes.SkillFactors[(int)skill]);
                bool first = alwaysOne && bought.Count(b => b.Item4 == wave) == 0;
                if (value > money && !first)
                {
                    return;
                }

                bought.Add((airframe, skill, value, wave));
                money -= value;
            }
        }
    }

    // An airframe drawn in proportion to the decoded availability column.
    private static int WeightedAirframe(Draws draw)
    {
        int total = HangarEconomy.Airframes.Sum(a => Math.Max(1, a.Availability));
        int pick = draw.Range(0, total - 1);
        for (int id = 0; id < HangarEconomy.Airframes.Length; id++)
        {
            pick -= Math.Max(1, HangarEconomy.Airframes[id].Availability);
            if (pick < 0)
            {
                return id;
            }
        }

        return HangarEconomy.Airframes.Length - 1;
    }

    // The flight starts StartDistance out on a drawn bearing, nosed at the centre.
    private static (Vector3 Start, float Heading, float Bearing) Approach(Vector3 centre, CareerTunes tunes, Draws draw)
    {
        float bearing = (float)(draw.Unit() * Math.Tau);
        var start = centre + new Vector3(tunes.StartDistance * MathF.Sin(bearing), 0, tunes.StartDistance * MathF.Cos(bearing));
        start.Y = Mathf.Max(centre.Y, tunes.MinStartAltitude);
        return (start, HeadingTo(start, centre), bearing);
    }

    // The reserve comes in from the side opposite the flight's start.
    private static EnemyFlight Reserve(int airframe, SkillTier skill, int value, Vector3 centre, float bearing, CareerTunes tunes)
    {
        float far = tunes.StartDistance * 1.2f;
        var at = centre - new Vector3(far * MathF.Sin(bearing), 0, far * MathF.Cos(bearing));
        at.Y = Mathf.Max(centre.Y, tunes.MinStartAltitude);
        return new EnemyFlight(airframe, skill, at, HeadingTo(at, centre), null, EnemyWave.Reserve, value);
    }

    private static MissionSpec Spec(
        Contract contract,
        Vector3 centre,
        Vector3 start,
        float heading,
        int wingmen,
        string? route,
        List<EnemyFlight> enemies,
        List<TargetPlacement> targets,
        List<MissionObjective> objectives,
        ReserveTrigger trigger,
        int budget,
        CareerTunes tunes) => new()
        {
            Job = contract.Job,
            Threat = contract.Threat,
            Seed = contract.Seed,
            Centre = centre,
            PlayerStart = start,
            PlayerHeadingDeg = heading,
            WingmanSeats = Math.Clamp(wingmen, 0, MaxWingmen),
            Route = route,
            Enemies = enemies,
            Targets = targets,
            Objectives = objectives,
            ReserveTrigger = trigger,
            ReserveDelaySeconds = tunes.ReserveDelaySeconds,
            Budget = budget,
            Spent = enemies.Sum(e => e.Value) + targets.Sum(t => t.Value),
        };

    // A running draw index over one set of dice, so each call takes the next draw.
    private sealed class Draws
    {
        private readonly CareerDice _dice;
        private int _index;

        public Draws(CareerDice dice)
        {
            _dice = dice;
        }

        public int Range(int min, int max) => _dice.Range(_index++, min, max);

        public double Unit() => _dice.Unit(_index++);
    }
}
