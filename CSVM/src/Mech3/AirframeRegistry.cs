using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVM.Mech3;

/// <summary>One airframe a player can fly: its model node, the name a table shows for it, and the
/// stock node whose data it flies on. That is its own node for a stock airframe, its parent's for a
/// mod.</summary>
public sealed record AirframeEntry(string Node, string DisplayName, string StockNode)
{
    /// <summary>Whether this is one of the eleven stock airframes rather than a mod.</summary>
    public bool IsStock => string.Equals(Node, StockNode, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Every player airframe: the eleven stock ones of planes.zbd in airframe-id order, then the mod
/// airframes registered at run time. A mod has its own id and a stock parent. A table that
/// enumerates the player airframes reads <see cref="Stock"/> or <see cref="All"/> here instead of
/// keeping its own copy. A reader that needs per-airframe data a mod does not supply goes through
/// <see cref="StockNodeOf"/>. The stock order is <c>Flight.Hangar.StockAirframes</c>' id order.
/// </summary>
public sealed class AirframeRegistry
{
    // Airframe id 0-10 to the planes.zbd node, in the stat table's row order. The paint page's skin
    // prefixes and Instant Action's langui 3700 rows follow the same order. The display names are
    // camparam.json's block keys (docs/formats/camparam.md), so they are data, not labels. The
    // Hoplite is player_autogyro and the Hellhound player_avenger: the shipped data names them so.
    private static readonly AirframeEntry[] StockTable =
    {
        StockEntry("player_autogyro", "Hoplite"), StockEntry("player_avenger", "Hellhound"),
        StockEntry("player_balmoral", "Balmoral"), StockEntry("player_bhawk", "Bloodhawk"),
        StockEntry("player_brigand", "Brigand"), StockEntry("player_pfighter", "Devastator"),
        StockEntry("player_fbrand", "Firebrand"), StockEntry("player_fury", "Fury"),
        StockEntry("player_kestrel", "Kestrel"), StockEntry("player_peacemaker", "Peacemaker"),
        StockEntry("player_warhawk", "Warhawk"),
    };

    private readonly object _gate = new();

    // Replaced whole on each registration, so a reader holds a consistent list without the lock.
    private AirframeEntry[] _all = StockTable;

    /// <summary>The registry the game registers its mods into.</summary>
    public static AirframeRegistry Shared { get; } = new();

    /// <summary>The eleven stock airframes in airframe-id order, the order saves index and a network
    /// session's first eleven airframe rows.</summary>
    public static IReadOnlyList<AirframeEntry> Stock => StockTable;

    /// <summary>The stock airframes, then every registered mod in registration order.</summary>
    public IReadOnlyList<AirframeEntry> All => _all;

    /// <summary>The airframe flying as <paramref name="node"/>, ignoring case, or null.</summary>
    public AirframeEntry? Find(string node) =>
        Array.Find(_all, a => string.Equals(a.Node, node, StringComparison.OrdinalIgnoreCase));

    /// <summary>The stock node whose data <paramref name="node"/> flies on: a mod's parent, or a
    /// stock node itself. A node the registry does not know, an AI vehicle's, comes back unchanged.</summary>
    public string StockNodeOf(string node) => Find(node)?.StockNode ?? node;

    /// <summary>Adds a mod airframe. Throws <see cref="ArgumentException"/> for a blank argument, an
    /// <paramref name="id"/> already registered (stock or mod), or a non-stock
    /// <paramref name="parentNode"/>. The message names the reason.</summary>
    public AirframeEntry Register(string id, string displayName, string parentNode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentNode);
        var parent = Array.Find(StockTable, a => string.Equals(a.Node, parentNode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"airframe '{id}': parent '{parentNode}' is no stock airframe node", nameof(parentNode));
        lock (_gate)
        {
            if (Find(id) is { } taken)
            {
                string what = taken.IsStock ? "a stock airframe node" : "an airframe already registered";
                throw new ArgumentException($"airframe '{id}' collides with {what}", nameof(id));
            }

            var entry = new AirframeEntry(id, displayName, parent.Node);
            _all = _all.Append(entry).ToArray();
            return entry;
        }
    }

    private static AirframeEntry StockEntry(string node, string display) => new(node, display, node);
}
