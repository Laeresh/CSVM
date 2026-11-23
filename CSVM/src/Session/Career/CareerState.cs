using System;
using System.Collections.Generic;
using CSVM.Flight.Hangar;

namespace CSVM.Session.Career;

/// <summary>
/// One career's whole state, engine-free: the calendar, the ledger, the gang's planes, the hold,
/// the port's shelf and board, and the contract taken. Every operation that passes time goes
/// through <see cref="AdvanceDays"/>, so paydays, restocks and board refreshes cannot be skipped.
/// Nothing in flight reads this; a sortie gets a mission spec and hands back a result.
/// </summary>
public sealed class CareerState
{
    private readonly List<PlaneInstance> _planes = new();

    private CareerState(string name, ulong seed, Ledger ledger)
    {
        Name = name;
        Seed = seed;
        Ledger = ledger;
    }

    public string Name { get; }

    /// <summary>The seed every career draw derives from, fixed for the career's life.</summary>
    public ulong Seed { get; }

    /// <summary>Days since the career began; day 0 is the first.</summary>
    public int Day { get; private set; }

    /// <summary>The economy numbers. Not saved: a career always runs on the current tunes.</summary>
    public CareerTunes Tunes { get; private set; } = CareerTunes.Default;

    public Ledger Ledger { get; }

    public IReadOnlyList<PlaneInstance> Planes => _planes;

    public Hold Hold { get; } = new();

    public PortShelf Shelf { get; } = new();

    public ContractBoard Board { get; } = new();

    /// <summary>The contract accepted and not yet flown, or null.</summary>
    public Contract? ActiveContract { get; private set; }

    /// <summary>The week number, which keys the shelf's stock.</summary>
    public int Week => Day / CareerTunes.DaysPerWeek;

    /// <summary>The next payday's day number.</summary>
    public int NextPayday => (Week + 1) * CareerTunes.DaysPerWeek;

    /// <summary>A new career. <paramref name="starters"/> are the starting planes' builds, which
    /// the caller makes from <see cref="CareerTunes.StartingAirframes"/> and the stock loadouts.
    /// Each starts with <see cref="CareerTunes.StartingWear"/> of every zone lost.</summary>
    public static CareerState New(string name, ulong seed, IEnumerable<CustomPlaneDef> starters, CareerTunes? tunes = null)
    {
        ArgumentNullException.ThrowIfNull(starters);
        tunes ??= CareerTunes.Default;
        var state = new CareerState(name, seed, new Ledger(tunes.StartingCash, tunes.StartingDebt)) { Tunes = tunes };
        foreach (var build in starters)
        {
            var plane = new PlaneInstance(build);
            foreach (var zone in Enum.GetValues<ArmourZone>())
            {
                plane.SetZoneDamage(zone, (int)Math.Round(plane.ZoneCapacity(zone) * tunes.StartingWear));
            }

            state._planes.Add(plane);
        }

        state.Shelf.Restock(seed, 0, tunes);
        state.Board.Refresh(seed, 0, tunes);
        return state;
    }

    /// <summary>Passes <paramref name="days"/> days. Each day refreshes the board; each payday
    /// charges wages, upkeep and the debt, then restocks the shelf.</summary>
    public void AdvanceDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            Day++;
            Board.Refresh(Seed, Day, Tunes);
            if (Day % CareerTunes.DaysPerWeek == 0)
            {
                Payday();
            }
        }
    }

    /// <summary>Repairs plane <paramref name="index"/> fully: pays the quote and passes its days.
    /// Refused, changing nothing, when cash cannot cover it or there is nothing to repair.</summary>
    public bool Repair(int index)
    {
        var plane = _planes[index];
        var quote = plane.QuoteRepair(Tunes);
        if (quote.IsNothing || !Ledger.TrySpend(Day, LedgerCategory.Repair, quote.Cost, plane.Build.Name))
        {
            return false;
        }

        plane.Repair();
        AdvanceDays(quote.Days);
        return true;
    }

    /// <summary>Takes offer <paramref name="id"/> off the board as the active contract. Refused
    /// while another contract is active.</summary>
    public Contract? Accept(int id)
    {
        if (ActiveContract != null)
        {
            return null;
        }

        ActiveContract = Board.Accept(id);
        return ActiveContract;
    }

    /// <summary>Drops the active contract, which pays nothing.</summary>
    public void AbandonContract() => ActiveContract = null;

    /// <summary>Restores a saved career exactly; the store's seam.</summary>
    internal static CareerState Restore(string name, ulong seed, int day, Ledger ledger, IEnumerable<PlaneInstance> planes, Contract? active)
    {
        var state = new CareerState(name, seed, ledger) { Day = day, ActiveContract = active };
        state._planes.AddRange(planes);
        return state;
    }

    private void Payday()
    {
        Ledger.Charge(Day, LedgerCategory.Wages, Tunes.WeeklyWages);
        Ledger.Charge(Day, LedgerCategory.Upkeep, Tunes.WeeklyUpkeep);
        Ledger.ServiceDebt(Day, Tunes);
        Shelf.Restock(Seed, Week, Tunes);
    }
}
