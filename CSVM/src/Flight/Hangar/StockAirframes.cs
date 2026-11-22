using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;

namespace CSVM.Flight.Hangar;

/// <summary>
/// The eleven stock airframes by int id: the <c>planes.zbd</c> node each airframe id 0-10 flies as,
/// and back, read off <see cref="AirframeRegistry.Stock"/>. The id is
/// <see cref="CustomPlaneDef.Airframe"/>'s and a campaign profile's, and the first eleven rows of a
/// network session's (<see cref="Net.NetModSet"/>). A mod
/// airframe has no int id: <see cref="IdOf"/> answers null for it and these readers stay stock-only.
/// </summary>
public static class StockAirframes
{
    private static readonly string[] NodeTable = AirframeRegistry.Stock.Select(a => a.Node).ToArray();

    /// <summary>The eleven stock nodes in airframe-id order.</summary>
    public static IReadOnlyList<string> Nodes => NodeTable;

    /// <summary>The stock node an airframe id flies as, clamped like the def's own fields.</summary>
    public static string Node(int airframe) =>
        NodeTable[Math.Clamp(airframe, 0, NodeTable.Length - 1)];

    /// <summary>The inverse of <see cref="Node"/>: the airframe id a stock player node flies as.
    /// Null when the node names none of the eleven, as a surface hull's own library-root model
    /// or a mod airframe does.</summary>
    public static int? IdOf(string node)
    {
        int id = Array.FindIndex(NodeTable, n => string.Equals(n, node, StringComparison.OrdinalIgnoreCase));
        return id < 0 ? null : id;
    }
}
