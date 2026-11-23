using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Session.Career;

namespace CSVM.Tests;

/// <summary>One week of a simulated career: the day, cash, debt and contracts flown so far.</summary>
public readonly record struct SimulatedWeek(int Week, int Cash, int Debt, int Flown, int Won, int Wrecks);

/// <summary>
/// A scripted gang playing a career for many weeks, with a sortie outcome model in place of
/// flying. The economy tunes can then be judged as a curve. The model and the gang's policy are
/// tunes of the test, not of the game. The gang repairs what it can afford, keeps slug for one
/// sortie, and takes the best-paying contract its flight can handle.
/// </summary>
public static class CareerSimulation
{
    /// <summary>Rounds one plane fires in a sortie, before the threat's share.</summary>
    private const int BaseRounds = 300;

    public static List<SimulatedWeek> Run(ulong seed, int weeks, CareerTunes? tunes = null)
    {
        tunes ??= CareerTunes.Default;
        var state = CareerState.New("Sim", seed, tunes.StartingAirframes.Select(Starter), tunes);
        var history = new List<SimulatedWeek>();
        int flown = 0;
        int won = 0;
        int wrecks = 0;
        int sortie = 0;
        int lastWeek = -1;
        while (state.Week < weeks)
        {
            // A repair can pass more than a week; each week it skipped reads the state after it.
            while (lastWeek < state.Week)
            {
                lastWeek++;
                history.Add(new SimulatedWeek(lastWeek, state.Ledger.Cash, state.Ledger.Debt, flown, won, wrecks));
            }

            for (int i = 0; i < state.Planes.Count; i++)
            {
                if (state.Planes[i].NeedsRepair)
                {
                    state.Repair(i);
                }
            }

            var fit = Enumerable.Range(0, state.Planes.Count).Where(i => Fit(state.Planes[i])).ToList();
            var pick = state.Board.Offers
                .Where(c => c.Threat <= fit.Count + 1)
                .OrderByDescending(c => c.Fee)
                .FirstOrDefault();
            if (fit.Count == 0 || pick == null || !Stock(state, fit, pick.Threat))
            {
                state.AdvanceDays(1);
                continue;
            }

            state.Accept(pick.Id);
            var result = Fly(state, fit, pick, CareerDice.For(seed, "sim", sortie++));
            state.CompleteSortie(result);
            flown++;
            won += result.PrimaryMet ? 1 : 0;
            wrecks += result.Planes.Count(p => p.Crashed);
        }

        while (lastWeek < weeks - 1)
        {
            lastWeek++;
            history.Add(new SimulatedWeek(lastWeek, state.Ledger.Cash, state.Ledger.Debt, flown, won, wrecks));
        }

        return history;
    }

    private static bool Fit(PlaneInstance plane) => plane.CanFly;

    // Buys the slug the flight will fire; refuses the sortie when cash cannot cover it.
    private static bool Stock(CareerState state, List<int> fit, int threat)
    {
        foreach (var group in fit.GroupBy(i => Slug(state.Planes[i])))
        {
            int need = group.Count() * Rounds(threat);
            int lots = (need - state.Hold.RoundsOf(group.Key) + PortShelf.RoundsPerLot - 1) / PortShelf.RoundsPerLot;
            if (lots > 0 && !state.Shelf.BuyRounds(group.Key, lots, state.Day, state.Hold, state.Ledger, state.Tunes))
            {
                return false;
            }
        }

        return true;
    }

    // The outcome model. Odds fall as threat outruns the flight, and damage, part hits and crashes
    // rise with threat. A lost sortie doubles the crash odds.
    private static SortieResult Fly(CareerState state, List<int> fit, Contract contract, CareerDice dice)
    {
        int draw = 0;
        double win = Math.Clamp(0.9 - (0.15 * (contract.Threat - fit.Count)), 0.2, 0.95);
        bool primary = dice.Unit(draw++) < win;
        int secondaries = Enumerable.Range(0, 2).Count(_ => dice.Unit(draw++) < win * 0.5);
        var reports = new List<PlaneReport>();
        foreach (int i in fit)
        {
            var damage = new int[4];
            for (int z = 0; z < 4; z++)
            {
                damage[z] = dice.Range(draw++, 0, 5 * contract.Threat);
            }

            bool crashed = dice.Unit(draw++) < 0.03 * contract.Threat * (primary ? 1 : 2);
            var parts = dice.Unit(draw++) < 0.1 * contract.Threat ? PlaneInstance.GunPart(0) : PlaneParts.None;
            reports.Add(new PlaneReport
            {
                Plane = i,
                ZoneDamage = damage,
                PartsHit = parts,
                Crashed = crashed,
                Kills = primary ? 1 : 0,
                RoundsFired = new Dictionary<AmmoLine, int> { [Slug(state.Planes[i])] = Rounds(contract.Threat) },
            });
        }

        return new SortieResult { PrimaryMet = primary, SecondariesMet = primary ? secondaries : 0, Planes = reports };
    }

    private static int Rounds(int threat) => BaseRounds + (100 * threat);

    private static AmmoLine Slug(PlaneInstance plane) =>
        new(plane.Build.Guns.FirstOrDefault(g => !g.IsEmpty).Calibre ?? 0, AmmoKind.Slug);

    // A starting plane: the airframe's stock armour spread over its zones, a middle engine, two
    // .50 guns. The real starters take the stock loadouts, which this test does not read.
    private static CustomPlaneDef Starter(int airframe)
    {
        int presses = HangarEconomy.StockArmourUnits[airframe] / HangarEconomy.ArmourUnitsPerStep / 4;
        var def = new CustomPlaneDef
        {
            Name = $"Starter {airframe}",
            Airframe = airframe,
            Engine = 1,
            ArmourNose = presses,
            ArmourTail = presses,
            ArmourLeftWing = presses,
            ArmourRightWing = presses,
        };
        def.Guns[0] = new GunChoice(2, false);
        def.Guns[1] = new GunChoice(2, false);
        return def;
    }
}
