using System;
using System.Collections.Generic;

namespace CSVM.Session.Campaign;

/// <summary>
/// The rule that settles the co-op sortie's own picks of the seated profile's hangar. One seat at a
/// time holds each plane, compared by name as the flight check's own clash rule compares. Picks
/// settle in seat order, seat 0 the host, so on a same-moment clash the earlier seat wins. A seat
/// whose pick is lost, or that has none, takes the first free plane, else the shared stock
/// Devastator. The wingman comes after every human. Engine-free: the host settles it and the wire
/// carries only the answer.
/// </summary>
public static class CoopPlanePool
{
    /// <summary>What a seat with no free plane flies: the stock Devastator, the campaign's own
    /// starter airframe (<see cref="CampaignProfileDef.NewProfile"/>).</summary>
    public const int StockAirframe = 5;

    /// <summary>A seat's plane when it flies <see cref="StockAirframe"/> at rest rather than a plane
    /// of the hangar.</summary>
    public const int Stock = -1;

    /// <summary>A seat's pick when it has not picked yet.</summary>
    public const int Unpicked = -2;

    /// <summary>The plane each seat flies, by seat: an index into <paramref name="hangar"/>, or
    /// <see cref="Stock"/>. The hangar is its planes' names in order. <paramref name="picks"/> is
    /// each seat's own pick, an index, <see cref="Stock"/> or <see cref="Unpicked"/>. A seat's
    /// answer depends only on the seats before it.</summary>
    public static int[] Resolve(IReadOnlyList<string> hangar, IReadOnlyList<int> picks)
    {
        ArgumentNullException.ThrowIfNull(hangar);
        ArgumentNullException.ThrowIfNull(picks);
        var held = new HashSet<string>(StringComparer.Ordinal);
        var planes = new int[picks.Count];
        for (int seat = 0; seat < planes.Length; seat++)
        {
            int pick = picks[seat];
            if (pick == Stock)
            {
                planes[seat] = Stock;
                continue;
            }

            planes[seat] = pick >= 0 && pick < hangar.Count && !held.Contains(hangar[pick]) ? pick : FirstFree(hangar, held);
            if (planes[seat] >= 0)
            {
                held.Add(hangar[planes[seat]]);
            }
        }

        return planes;
    }

    /// <summary>The plane the wingman flies on a mission that flies one, after every human's
    /// <paramref name="seats"/> as <see cref="Resolve"/> answers them. That is its
    /// <paramref name="saved"/> plane while no human holds it, else the first free plane, else
    /// <see cref="Stock"/>.</summary>
    public static int Wingman(IReadOnlyList<string> hangar, IReadOnlyList<int> seats, int saved)
    {
        ArgumentNullException.ThrowIfNull(hangar);
        ArgumentNullException.ThrowIfNull(seats);
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (int plane in seats)
        {
            if (plane >= 0 && plane < hangar.Count)
            {
                held.Add(hangar[plane]);
            }
        }

        return saved >= 0 && saved < hangar.Count && !held.Contains(hangar[saved]) ? saved : FirstFree(hangar, held);
    }

    private static int FirstFree(IReadOnlyList<string> hangar, HashSet<string> held)
    {
        for (int at = 0; at < hangar.Count; at++)
        {
            if (!held.Contains(hangar[at]))
            {
                return at;
            }
        }

        return Stock;
    }
}
