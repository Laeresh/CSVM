using System;
using System.Collections.Generic;

namespace CSVM.Session.Campaign;

/// <summary>
/// Which of the seated profile's aircraft each human on a co-op campaign sortie flies. The pool is
/// the host's hangar, and one seat at a time holds each unique plane, the way the wingman's is held.
/// Seat 0 is the host on its selected plane, and the wingman's plane is held before any guest
/// claims. Each further seat claims, in seat order, the first unheld plane in hangar order, else a
/// stock Devastator, which is not exclusive. Splitscreen seats and network guests draw from this one
/// pool, the local seats first and then each network guest in slot order. Engine-free, so the host
/// decides and the wire carries only the answer.
/// </summary>
public static class CoopPlanePool
{
    /// <summary>What a seat with no unique plane left flies: the stock Devastator, the campaign's
    /// own starter airframe (<see cref="CampaignProfileDef.NewProfile"/>).</summary>
    public const int StockAirframe = 5;

    /// <summary>The value <see cref="Allocate"/> gives a seat that flies <see cref="StockAirframe"/>
    /// at rest rather than a plane of the hangar.</summary>
    public const int Stock = -1;

    /// <summary>One entry per seat of the sortie, 0 the host: an index into the profile's
    /// <see cref="CampaignProfileDef.Planes"/>, or <see cref="Stock"/>. A seat's answer depends only
    /// on the seats before it. So the first entries of the widest sortie's answer are a smaller
    /// sortie's.</summary>
    public static int[] Allocate(CampaignProfileDef? profile, int seats)
    {
        var planes = new int[Math.Max(seats, 0)];
        Array.Fill(planes, Stock);
        if (profile is not { Planes.Count: > 0 } || planes.Length == 0)
        {
            return planes;
        }

        // Held by name, the key the flight check's own clash rule compares. Two records sharing a
        // name can never put two seats on what the hangar shows as one aircraft.
        var held = new HashSet<string>(StringComparer.Ordinal);
        planes[0] = Math.Clamp(profile.SelectedPlane, 0, profile.Planes.Count - 1);
        held.Add(profile.Planes[planes[0]].Name);
        if (profile.WingmanPlane >= 0 && profile.WingmanPlane < profile.Planes.Count)
        {
            held.Add(profile.Planes[profile.WingmanPlane].Name);
        }

        for (int seat = 1; seat < planes.Length; seat++)
        {
            for (int at = 0; at < profile.Planes.Count; at++)
            {
                if (held.Add(profile.Planes[at].Name))
                {
                    planes[seat] = at;
                    break;
                }
            }
        }

        return planes;
    }
}
