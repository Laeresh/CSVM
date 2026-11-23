using System.Collections.Generic;

namespace CSVM.Session.Career;

/// <summary>What one gang plane went through in a sortie. It carries the armour lost per zone,
/// the parts hit, a crash, its kills, and the rounds and rockets fired.</summary>
public sealed class PlaneReport
{
    /// <summary>The plane's index in <see cref="CareerState.Planes"/>.</summary>
    public required int Plane { get; init; }

    /// <summary>Armour units lost this sortie, per zone in <see cref="ArmourZone"/> order.</summary>
    public int[] ZoneDamage { get; init; } = new int[4];

    public PlaneParts PartsHit { get; init; }

    public bool Crashed { get; init; }

    public int Kills { get; init; }

    public Dictionary<AmmoLine, int> RoundsFired { get; init; } = new();

    public Dictionary<string, int> RocketsFired { get; init; } = new();
}

/// <summary>
/// What a sortie hands back to the career when it ends, as plain data the session fills in.
/// <see cref="PrimaryMet"/> decides whether the contract pays. Each secondary met adds a bonus.
/// A sortie that ends early, quit or the captain down, reports only what was met before it ended.
/// </summary>
public sealed class SortieResult
{
    public required bool PrimaryMet { get; init; }

    public int SecondariesMet { get; init; }

    public List<PlaneReport> Planes { get; init; } = new();
}

/// <summary>What the career did with a sortie result, for the debrief to show. It holds the fee
/// and bonus paid, the stock drawn from the hold, and the planes that came back as wrecks.</summary>
public sealed record SortieOutcome(int Fee, int Bonus, int RoundsUsed, int RocketsUsed, IReadOnlyList<int> Wrecked)
{
    public int Paid => Fee + Bonus;
}
