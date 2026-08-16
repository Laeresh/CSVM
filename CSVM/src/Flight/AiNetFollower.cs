using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Walks an <see cref="AiNet"/> patrol graph as a stream of waypoints: positions in,
/// the current target node out. Deliberately aircraft-agnostic: nothing here knows
/// about flight models, controllers or speeds, so the zeppelin motion item (F17) reuses this
/// class unchanged and only the thing consuming <see cref="CurrentTarget"/> differs
/// (<see cref="AiPilot.Patrol"/> is the aircraft consumer).
///
/// <para><b>Traversal is over EDGES, treated as undirected, avoiding an immediate backtrack.</b>
/// The shipped traversal direction is undecoded; this reading is ours, chosen because the worked
/// C1 loop (`M4ReinfAce`, edges `[0,1]…[8,9]` closed by `[0,9]`) dead-ends at node 9 under a
/// directed reading. A branch (a node with several onward neighbours) is resolved by the
/// follower's own seeded <see cref="Random"/> (per plane through the repo's <c>Rng</c> streams
/// at spawn, never Godot's global rng), so a fixed-seed run repeats its route. A dead end (the
/// only neighbour is the node just left) turns back; an isolated node is held forever.</para>
///
/// <para><b>Arrival is a capture radius, and it is invented.</b>
/// <see cref="DefaultArrivalRadius"/> is not an original value: the node counts as reached when
/// the follower is horizontally (XZ) within the radius. Horizontal, because neither the deleted
/// placeholder law nor the ported <see cref="AiControlLaw"/> converges on altitude quickly
/// through a level patrol turn. E42 re-measured the radius against the ported law rather than
/// assume it would shrink: it does not — on C1's M4ReinfAce the real law's own turning circle
/// misses a stationary aim point on roughly this same scale, and halving the radius to 100 m
/// roughly quadruples the mean time between node captures (measured over a 600 s run: 17 s/leg at
/// 200 m vs 65 s/leg at 100 m). The value stands, re-justified rather than retired.</para>
///
/// <para><b>An anchored net RIDES its target (`BL-377`).</b> A trailer such as
/// <c>[10, "player"]</c> makes the whole graph a PATTERN carried by a moving object rather than a
/// fixed route: every node position comes back as <c>(node − anchor) + target</c> in X/Z with the
/// node's <b>authored Y</b> (docs/org/aiPilot.md, "The trailer"). 76 of the 222 nets are anchored,
/// 11 of them to the player, and six of the eight chapters' first nets. The offset runs through
/// <see cref="NodePosition"/>, so the nearest-node scan and every consumer move with it, as the
/// original's single <c>FUN_00432010</c> does. Whether a net actually rides is the CALLER's
/// choice: without a <c>trailerTarget</c> supplier the authored coordinates are flown, which is
/// also the engine's own unresolved-target branch.</para>
///
/// <para>Per-node tags are preserved raw on <see cref="AiNetNode.Tags"/>; both decoded readings
/// (stop-point id vs segment id) are still open (F17), so this class acts on neither.</para></summary>
public sealed class AiNetFollower
{
    /// <summary>The capture radius, metres: an invented value, not original behaviour. First
    /// sized to the (now deleted) placeholder law's tracking error; E42 re-measured it against
    /// the ported <see cref="AiControlLaw"/> on the same C1 M4ReinfAce loop and found no smaller
    /// value to prefer — shrinking it makes the patrol slower to advance, not more precise, so it
    /// stands unchanged (see the class remarks for the measurement).</summary>
    public const float DefaultArrivalRadius = 200f;

    private readonly Random _rng;
    private readonly float _arrivalRadius;
    private readonly int[][] _neighbors;
    private readonly Func<Vector3?>? _trailerTarget;
    private readonly int _anchorIndex = -1;
    private int _previousIndex = -1;

    /// <param name="trailerTarget">Where the net's trailer target is right now, or null when it
    /// cannot be located this frame. Supplied only by a caller that WANTS the net to ride
    /// (`BL-377`); omitted, or paired with a net whose trailer has no anchor node, the authored
    /// coordinates are flown. Called once per node read, so it must be cheap.</param>
    public AiNetFollower(AiNet net, Random rng, float arrivalRadius = DefaultArrivalRadius,
        Func<Vector3?>? trailerTarget = null)
    {
        if (net.Nodes.Count == 0)
            throw new ArgumentException($"net '{net.Name}#{net.Id}' has no nodes", nameof(net));
        Net = net;
        _rng = rng;
        _arrivalRadius = arrivalRadius;
        if (trailerTarget != null && net.Trailer is { NodeIndex: >= 0 } trailer
            && trailer.NodeIndex < net.Nodes.Count)
        {
            _anchorIndex = trailer.NodeIndex;
            _trailerTarget = trailerTarget;
        }

        // Undirected adjacency off the explicit edge list, never node order (the graph
        // branches; a list-order walk is the documented wrong reading).
        var sets = new HashSet<int>[net.Nodes.Count];
        for (int i = 0; i < sets.Length; i++)
            sets[i] = new HashSet<int>();
        foreach (var (a, b) in net.Edges)
        {
            if (a == b)
                continue;
            sets[a].Add(b);
            sets[b].Add(a);
        }
        _neighbors = new int[sets.Length][];
        for (int i = 0; i < sets.Length; i++)
        {
            _neighbors[i] = new int[sets[i].Count];
            sets[i].CopyTo(_neighbors[i]);
            Array.Sort(_neighbors[i]); // stable candidate order, so the rng draw is reproducible
        }
    }

    /// <summary>The net being flown: nodes, edges, raw tags and the (unacted-on) trailer.</summary>
    public AiNet Net { get; }

    /// <summary>The node index currently flown toward; −1 before the first
    /// <see cref="Update"/> picks the nearest node.</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>How many node captures have advanced the target so far.</summary>
    public int Advances { get; private set; }

    /// <summary>True when this follower rides its net's trailer target (`BL-377`): the net has an
    /// anchor node AND the caller supplied a target. False is the fixed-route case, which is every
    /// unanchored net and every caller that did not opt in.</summary>
    public bool Anchored => _anchorIndex >= 0;

    /// <summary>The position of the node currently flown toward, trailer offset included. Only
    /// valid once <see cref="Update"/> has run.</summary>
    public Vector3 CurrentTarget => NodePosition(CurrentIndex);

    /// <summary>How far an anchored net's nodes are carried from their authored coordinates right
    /// now: <c>target − anchorNode</c> in X and Z, <b>zero in Y</b>, because the pattern keeps its
    /// authored altitude, so a ring authored at 400 m stays at 400 m over a zeppelin at 200 m.
    /// <see cref="Vector3.Zero"/> when the net is unanchored or the target cannot be located,
    /// which is the engine's own no-trailer branch (`FUN_00432010`). Static because the overlay
    /// needs the same offset for a net nobody is flying.</summary>
    public static Vector3 TrailerOffset(AiNet net, Vector3? target)
    {
        if (target is not { } t || net.Trailer is not { NodeIndex: >= 0 } trailer
            || trailer.NodeIndex >= net.Nodes.Count)
            return Vector3.Zero;
        var anchor = net.Nodes[trailer.NodeIndex].Position;
        return new Vector3(t.X - anchor.X, 0f, t.Z - anchor.Z);
    }

    /// <summary>Where node <paramref name="index"/> is right now: its authored position plus the
    /// live trailer offset. EVERY node read goes through here, because the original's does: a
    /// follower that offset only its current target would seat itself on the wrong node.</summary>
    public Vector3 NodePosition(int index) => Net.Nodes[index].Position + LiveOffset();

    /// <summary>Drops the walk back to "nearest node next", so the next <see cref="Update"/>
    /// re-seats from wherever the follower now is. This is the original's own activation rule:
    /// <c>FUN_004b0f40</c> snaps a vehicle carrying a net to that net's nearest node
    /// (<c>FUN_00432010</c>) when it is activated. An Instant Action wave member ticks while it
    /// is inert (presence is not a sim gate), so without this its first update latches a node
    /// near the parking pose and it flies back there after the teleport.</summary>
    public void Reseat()
    {
        CurrentIndex = -1;
        _previousIndex = -1;
    }

    /// <summary>Advances the walk from <paramref name="position"/>: the first call targets the
    /// nearest node (the design's "fly first to the nearest node"); afterwards, reaching the
    /// capture radius steps to a connected neighbour. Returns true when the target changed.</summary>
    public bool Update(Vector3 position)
    {
        if (CurrentIndex < 0)
        {
            CurrentIndex = NearestNode(position);
            return true;
        }
        var to = CurrentTarget - position;
        if (new Vector2(to.X, to.Z).LengthSquared() > _arrivalRadius * _arrivalRadius)
            return false;
        var candidates = _neighbors[CurrentIndex];
        if (candidates.Length == 0)
            return false; // an isolated node is held, not escaped by inventing an edge
        int next = candidates.Length == 1 ? candidates[0] : PickOnward(candidates);
        _previousIndex = CurrentIndex;
        CurrentIndex = next;
        Advances++;
        return true;
    }

    // A seeded draw over the onward neighbours, excluding the node just left when anything
    // else is available (a path net still turns back at its dead ends).
    private int PickOnward(int[] candidates)
    {
        int onward = 0;
        foreach (int c in candidates)
        {
            if (c != _previousIndex)
                onward++;
        }
        if (onward == 0)
            return candidates[_rng.Next(candidates.Length)];
        int pick = _rng.Next(onward);
        foreach (int c in candidates)
        {
            if (c == _previousIndex)
                continue;
            if (pick-- == 0)
                return c;
        }
        return candidates[0]; // unreachable; keeps the compiler satisfied
    }

    private Vector3 LiveOffset() =>
        _anchorIndex < 0 ? Vector3.Zero : TrailerOffset(Net, _trailerTarget!());

    // The seat scan, in authored space: a uniform offset moves every node equally, so the target
    // moves back instead of all N nodes forward.
    //
    // ⚠ Edgeless nodes are SKIPPED, which is the engine's own rule (FUN_00431900 tests the degree
    // at node +0x18). It matters most on an anchored net, where the anchor is parked off the ring
    // in every shipped case: seated there, the walk has no neighbour to advance to and the plane
    // holds that node forever. A net whose nodes are ALL edgeless still gets a seat rather than
    // nothing.
    private int NearestNode(Vector3 position)
    {
        var local = position - LiveOffset();
        int best = -1;
        float bestSq = float.MaxValue;
        for (int i = 0; i < Net.Nodes.Count; i++)
        {
            if (_neighbors[i].Length == 0)
                continue;
            float dSq = Net.Nodes[i].Position.DistanceSquaredTo(local);
            if (dSq < bestSq)
            {
                bestSq = dSq;
                best = i;
            }
        }
        if (best >= 0)
            return best;
        for (int i = 0; i < Net.Nodes.Count; i++)
        {
            float dSq = Net.Nodes[i].Position.DistanceSquaredTo(local);
            if (dSq < bestSq)
            {
                bestSq = dSq;
                best = i;
            }
        }
        return Math.Max(best, 0);
    }
}
