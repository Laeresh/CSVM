using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVM.Session.Career;

/// <summary>The job a contract asks for. Phase 1 has two; the design lists the rest.</summary>
public enum JobType
{
    Patrol,
    Strike,
}

/// <summary>Who offers a contract. A kind only: named employers arrive with standing.</summary>
public enum EmployerKind
{
    Nation,
    Corporation,
    Militia,
    Broker,
}

/// <summary>One offer on the board. The mission generator builds the sortie from
/// <see cref="Seed"/>, so a contract always flies the same mission. <see cref="ExpiresOn"/> is
/// the last day it can still be accepted.</summary>
public sealed record Contract(
    int Id,
    JobType Job,
    EmployerKind Employer,
    int Threat,
    int Fee,
    int OfferedOn,
    int ExpiresOn,
    ulong Seed);

/// <summary>
/// The port's contract board. Each day expired offers drop and empty slots refill, up to the
/// board size, with offers rolled from the career's seed and the day. The threat floor is the
/// lowest rating the region offers, and lower ratings are offered more often than high ones.
/// </summary>
public sealed class ContractBoard
{
    private readonly List<Contract> _offers = new();

    public IReadOnlyList<Contract> Offers => _offers;

    /// <summary>The next id a new offer takes. Saved, so ids never repeat within a career.</summary>
    public int NextId { get; private set; } = 1;

    /// <summary>Drops offers expired before <paramref name="day"/> and refills empty slots.</summary>
    public void Refresh(ulong careerSeed, int day, CareerTunes tunes)
    {
        _offers.RemoveAll(c => c.ExpiresOn < day);
        while (_offers.Count < tunes.BoardSize)
        {
            _offers.Add(Roll(careerSeed, day, tunes));
        }
    }

    /// <summary>Takes the offer <paramref name="id"/> off the board, or null when it is not there.</summary>
    public Contract? Accept(int id)
    {
        int index = _offers.FindIndex(c => c.Id == id);
        if (index < 0)
        {
            return null;
        }

        var contract = _offers[index];
        _offers.RemoveAt(index);
        return contract;
    }

    /// <summary>Restores a saved board exactly.</summary>
    internal void Restore(int nextId, IEnumerable<Contract> offers)
    {
        NextId = nextId;
        _offers.Clear();
        _offers.AddRange(offers);
    }

    // Keyed by the offer's id, so a second refill on one day never rolls a duplicate. Threat is
    // weighted toward the floor: the lower of two draws over [floor, 5].
    private Contract Roll(ulong careerSeed, int day, CareerTunes tunes)
    {
        var dice = CareerDice.For(careerSeed, "board", NextId);
        int floor = Math.Clamp(tunes.ThreatFloor, 1, 5);
        int threat = Math.Min(dice.Range(0, floor, 5), dice.Range(1, floor, 5));
        var (min, max) = tunes.FeeBand(threat);
        int fee = dice.Range(2, min / 100, max / 100) * 100;
        var job = (JobType)dice.Range(3, 0, Enum.GetValues<JobType>().Length - 1);
        var employer = (EmployerKind)dice.Range(4, 0, Enum.GetValues<EmployerKind>().Length - 1);
        int expires = day + dice.Range(5, tunes.MinExpiryDays, tunes.MaxExpiryDays);
        return new Contract(NextId++, job, employer, threat, fee, day, expires, dice.Next(6));
    }
}
