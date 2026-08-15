using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Turns a patrol net's TRAILER name into a live position supplier, which is the half of `BL-377`
/// that belongs to the session rather than to <see cref="Flight.AiNetFollower"/>: the follower
/// does the offset arithmetic and knows nothing about players or world nodes.
///
/// <para>The original resolves the name ONCE, when the net is built (<c>FUN_004314e0</c> calls
/// <c>FUN_004d0280(7, name)</c> into net <c>+0x1c</c>), and every later node read reuses that
/// object. Here the resolve is lazy-once instead: nets are read and AI armed before the zeppelin
/// runtime has placed its hosts, so a resolve at construction would miss targets that exist a few
/// hundred lines later. The result (hit or miss) is then cached per name, so a target that never
/// resolves costs one search, not one per frame.</para>
///
/// <para><b>⚠ <c>player</c> is one object in the binary and 2–4 rigs here.</b> The original has no
/// splitscreen, so there is no behaviour to copy; the decision (2026-08-15) is that split play
/// matches single player, which is the FIRST rig for every anchored net, and no rule (nearest
/// player, host player, per-plane pick) is invented. A session with no player rig at all resolves
/// nothing and the net is flown at its authored coordinates.</para>
/// </summary>
public sealed class NetTrailerTargets
{
    /// <summary>The trailer name the original resolves to the player's own vehicle. 11 of the 222
    /// nets carry it, 4 of them named <c>*Ace*</c>; it is team-blind, so it puts enemy aces and
    /// reinforcement flights on the player exactly as it keeps wingmen with them
    /// (docs/formats/ai-nets.md).</summary>
    public const string PlayerName = "player";

    private readonly Func<Vector3?>? _player;
    private readonly Func<string, Node3D?>? _worldNode;
    private readonly Dictionary<string, Node3D?> _resolved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);

    public NetTrailerTargets(Func<Vector3?>? player, Func<string, Node3D?>? worldNode)
    {
        _player = player;
        _worldNode = worldNode;
    }

    /// <summary>The supplier to hand <see cref="Flight.AiNetFollower"/> for this net, or null when
    /// the net is not anchored (no trailer, no attach node, or a bare index with no name — the
    /// three shipped shapes that carry no target). Null means "fly the authored coordinates",
    /// which is also what the supplier returning null on a given frame means.</summary>
    public Func<Vector3?>? For(AiNet net)
    {
        if (net.Trailer is not { NodeIndex: >= 0, Name: { Length: > 0 } name })
        {
            return null;
        }
        if (name.Equals(PlayerName, StringComparison.OrdinalIgnoreCase))
        {
            return _player;
        }
        return _worldNode == null ? null : () => Position(name);
    }

    /// <summary>Where this net's nodes sit right now, relative to their authored coordinates: the
    /// overlay's read of the same offset the followers fly (`BL-377`). Zero for an unanchored net
    /// or an unresolved target.</summary>
    public Vector3 OffsetOf(AiNet net) =>
        Flight.AiNetFollower.TrailerOffset(net, For(net)?.Invoke());

    private Vector3? Position(string name)
    {
        if (!_resolved.TryGetValue(name, out var node))
        {
            node = _worldNode!(name);
            _resolved[name] = node;
            if (node == null && _reported.Add(name))
            {
                GD.Print($"ainet: trailer target '{name}' is not in this world — the nets " +
                         "anchored to it fly at their authored coordinates");
            }
        }
        // A destroyed target (a killed zeppelin's node is freed) drops back to the authored
        // coordinates rather than throwing on a stale handle.
        return node is { } n && GodotObject.IsInstanceValid(n) && n.IsInsideTree()
            ? n.GlobalPosition
            : null;
    }
}
