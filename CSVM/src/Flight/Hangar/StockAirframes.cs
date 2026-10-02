using System;
using System.Collections.Generic;

namespace CSVM.Flight.Hangar;

/// <summary>
/// The eleven stock airframes by id: the <c>planes.zbd</c> node each airframe id 0-10 flies as, and
/// back. The id is <see cref="CustomPlaneDef.Airframe"/>'s, a campaign profile's and a network
/// roster's, so every picker, the campaign and the wire read this one order.
/// </summary>
public static class StockAirframes
{
    // Airframe id 0-10 to the planes.zbd node, in the stat table's row order: Hoplite, Hellhound,
    // Balmoral, Bloodhawk, Brigand, Devastator, Firebrand, Fury, Kestrel, Peacemaker, Warhawk.
    // The paint page's skin prefixes are keyed by the same order. The Hoplite is player_autogyro:
    // the shipped data names that aircraft both ways.
    private static readonly string[] NodeTable =
    {
        "player_autogyro", "player_avenger", "player_balmoral", "player_bhawk", "player_brigand",
        "player_pfighter", "player_fbrand", "player_fury", "player_kestrel", "player_peacemaker",
        "player_warhawk",
    };

    /// <summary>The eleven stock nodes in airframe-id order. A network roster carries an airframe
    /// as its index into this list, so both peers have to read the one order.</summary>
    public static IReadOnlyList<string> Nodes => NodeTable;

    /// <summary>The stock node an airframe id flies as, clamped like the def's own fields.</summary>
    public static string Node(int airframe) =>
        NodeTable[Math.Clamp(airframe, 0, NodeTable.Length - 1)];

    /// <summary>The inverse of <see cref="Node"/>: the airframe id a stock player node flies as.
    /// Null when the node names none of the eleven, as a surface hull's own library-root model
    /// does.</summary>
    public static int? IdOf(string node)
    {
        for (int i = 0; i < NodeTable.Length; i++)
        {
            if (string.Equals(NodeTable[i], node, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }
}
