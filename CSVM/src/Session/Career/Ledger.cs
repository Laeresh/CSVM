using System;
using System.Collections.Generic;

namespace CSVM.Session.Career;

/// <summary>What a ledger entry is for, so the ledger page can total spending by kind.</summary>
public enum LedgerCategory
{
    ContractFee,
    Ammunition,
    Repair,
    Wages,
    Upkeep,
    DebtPayment,
    Interest,
    Arrears,
}

/// <summary>One line of the ledger: the day, its kind, and how it moved cash and debt. A
/// positive <see cref="Cash"/> is money in; a positive <see cref="Debt"/> is more owed.</summary>
public readonly record struct LedgerEntry(int Day, LedgerCategory Category, int Cash, int Debt, string Note);

/// <summary>
/// The career's money in one place: cash, debt and every entry that moved them. Cash never goes
/// below zero. A payday the gang cannot cover adds the shortfall to the debt as arrears, so a
/// broke gang falls deeper into the moneylender's hands. Bankruptcy and seizure are a later phase.
/// </summary>
public sealed class Ledger
{
    private readonly List<LedgerEntry> _entries = new();

    public Ledger(int cash, int debt)
    {
        Cash = Math.Max(0, cash);
        Debt = Math.Max(0, debt);
    }

    public int Cash { get; private set; }

    public int Debt { get; private set; }

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    /// <summary>Whether <paramref name="amount"/> can be spent from cash now.</summary>
    public bool CanAfford(int amount) => amount <= Cash;

    /// <summary>Money in, such as a contract fee.</summary>
    public void Earn(int day, LedgerCategory category, int amount, string note = "")
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "earn a non-negative amount");
        }

        Cash += amount;
        _entries.Add(new LedgerEntry(day, category, amount, 0, note));
    }

    /// <summary>A purchase the player chose. Refused, with nothing written, when cash cannot
    /// cover it.</summary>
    public bool TrySpend(int day, LedgerCategory category, int amount, string note = "")
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "spend a non-negative amount");
        }

        if (amount > Cash)
        {
            return false;
        }

        Cash -= amount;
        _entries.Add(new LedgerEntry(day, category, -amount, 0, note));
        return true;
    }

    /// <summary>A bill the gang must pay, such as wages. Whatever cash cannot cover becomes debt.</summary>
    public void Charge(int day, LedgerCategory category, int amount, string note = "")
    {
        if (amount <= 0)
        {
            return;
        }

        int paid = Math.Min(amount, Cash);
        Cash -= paid;
        _entries.Add(new LedgerEntry(day, category, -paid, 0, note));
        if (amount > paid)
        {
            Debt += amount - paid;
            _entries.Add(new LedgerEntry(day, LedgerCategory.Arrears, 0, amount - paid, note));
        }
    }

    /// <summary>The weekly debt service: interest on what is owed, then the instalment.</summary>
    public void ServiceDebt(int day, CareerTunes tunes)
    {
        if (Debt <= 0)
        {
            return;
        }

        int interest = (int)Math.Round(Debt * tunes.WeeklyInterestPercent / 100.0, MidpointRounding.AwayFromZero);
        if (interest > 0)
        {
            Debt += interest;
            _entries.Add(new LedgerEntry(day, LedgerCategory.Interest, 0, interest, string.Empty));
        }

        int instalment = Math.Min(tunes.DebtInstalment, Debt);
        int paid = Math.Min(instalment, Cash);
        if (paid > 0)
        {
            Cash -= paid;
            Debt -= paid;
            _entries.Add(new LedgerEntry(day, LedgerCategory.DebtPayment, -paid, -paid, string.Empty));
        }
    }

    /// <summary>Restores a saved ledger exactly, entries included.</summary>
    internal static Ledger Restore(int cash, int debt, IEnumerable<LedgerEntry> entries)
    {
        var ledger = new Ledger(cash, debt);
        ledger._entries.AddRange(entries);
        return ledger;
    }
}
