using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Walks an <see cref="AiNet"/> patrol graph as a stream of waypoints: positions in, the
/// current target node out. Aircraft-agnostic on purpose, so <c>ZeppelinMotion</c> (F17) reuses it
/// unchanged; <see cref="AiPilot.Patrol"/> is the aircraft consumer. Traversal is decoded in this
/// module's docs/architecture.md entry; the anchored-trailer ride is docs/formats/ai-nets.md.
/// Arrival is the decoded along-leg test (<see cref="ArrivalRadius"/>). Per-node tags
/// (<see cref="AiNetNode.Tags"/>) are preserved raw and unacted on (docs/formats/ai-nets.md).
/// </summary>
public sealed class AiNetFollower
{
    /// <summary>The floor on a leg's arrival radius, metres: <c>CCENet+0x28</c>, whose constructor
    /// (<c>FUN_004303d0</c>) seats 10 and which every shipped net leaves at that default.</summary>
    public const float MinArrivalRadiusM = 10f;

    // The radius is a tenth of the leg's HORIZONTAL length, floored (FUN_00431a90, which squares
    // it into edge+0x1c at net load). Altitude change along a leg does not widen the capture.
    private const float ArrivalRadiusPerLeg = 0.1f;

    private readonly Random _rng;
    private readonly float _minArrivalRadius;
    private readonly int[][] _neighbors;
    private readonly Func<Vector3?>? _trailerTarget;
    private readonly int _anchorIndex = -1;
    private int _previousIndex = -1;
    private Vector3? _legOrigin;

    /// <param name="trailerTarget">Where the net's trailer target is right now, or null when it
    /// cannot be located this frame. Supplied only by a caller that WANTS the net to ride;
    /// omitted, or paired with a net whose trailer has no anchor node, the authored
    /// coordinates are flown. Called once per node read, so it must be cheap.</param>
    /// <param name="minArrivalRadius">The net's own floor on the per-leg radius. Raised only by a
    /// caller whose vehicle cannot turn inside the decoded one; never lowers it.</param>
    public AiNetFollower(AiNet net, Random rng, float minArrivalRadius = MinArrivalRadiusM,
        Func<Vector3?>? trailerTarget = null)
    {
        if (net.Nodes.Count == 0)
            throw new ArgumentException($"net '{net.Name}#{net.Id}' has no nodes", nameof(net));
        Net = net;
        _rng = rng;
        _minArrivalRadius = Mathf.Max(minArrivalRadius, MinArrivalRadiusM);
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

    /// <summary>True when this follower rides its net's trailer target: the net has an
    /// anchor node AND the caller supplied a target. False is the fixed-route case, which is every
    /// unanchored net and every caller that did not opt in.</summary>
    public bool Anchored => _anchorIndex >= 0;

    /// <summary>The position of the node currently flown toward, trailer offset included. Only
    /// valid once <see cref="Update"/> has run.</summary>
    public Vector3 CurrentTarget => NodePosition(CurrentIndex);

    /// <summary>Where the leg being flown STARTS, trailer offset included: the node just left, or
    /// on the first leg the point the walk was seated from. The original always has one
    /// (`obj+0x2e8` is seated at spawn and it aims at the far end of that edge); our first leg
    /// targets the nearest node instead of leaving it, so it starts where the vehicle was.
    /// Null only before the first <see cref="Update"/>. <see cref="AiPilot.PatrolAim"/> and the
    /// arrival test are the consumers.</summary>
    public Vector3? LegStart =>
        _previousIndex >= 0 ? NodePosition(_previousIndex)
        : _legOrigin is { } o ? o + LiveOffset()
        : null;

    /// <summary>The radius, metres, at which a leg from <paramref name="legStart"/> to
    /// <paramref name="node"/> counts as reached — a tenth of its horizontal length, floored at
    /// <see cref="MinArrivalRadiusM"/>. The engine keeps the square in <c>edge+0x1c</c>.</summary>
    public static float ArrivalRadius(Vector3 legStart, Vector3 node, float floorM = MinArrivalRadiusM)
    {
        var leg = node - legStart;
        return Mathf.Max(ArrivalRadiusPerLeg * Mathf.Sqrt((leg.X * leg.X) + (leg.Z * leg.Z)), floorM);
    }

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
        _legOrigin = null;
    }

    /// <summary>Advances the walk from <paramref name="position"/>: the first call targets the
    /// nearest node (the design's "fly first to the nearest node"); afterwards, drawing abeam the
    /// node steps to a connected neighbour. Returns true when the target changed.</summary>
    // ⚠ The test is ALONG the leg, not a distance to the node (FUN_0041d1f0, after the law call):
    // it fires however far off to the side the vehicle is, so one that cannot turn tightly enough
    // flows past its node instead of orbiting it forever.
    public bool Update(Vector3 position)
    {
        if (CurrentIndex < 0)
        {
            CurrentIndex = NearestNode(position);
            _legOrigin = position - LiveOffset();
            return true;
        }
        var node = CurrentTarget;
        var start = LegStart ?? position;
        var leg = node - start;
        if (leg.LengthSquared() > 1e-6f
            && (position - node).Dot(leg.Normalized()) <= -ArrivalRadius(start, node, _minArrivalRadius))
            return false;
        var candidates = _neighbors[CurrentIndex];
        if (candidates.Length == 0)
            return false; // an isolated node is held, not escaped by inventing an edge
        int next = candidates.Length == 1 ? candidates[0] : PickOnward(candidates);
        _previousIndex = CurrentIndex;
        _legOrigin = null;
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

    // The seat scan runs in authored space: a uniform offset moves every node equally.
    // ⚠ Edgeless nodes are skipped, the engine's own rule (docs/org/aiPilot.md); a net whose nodes
    // are all edgeless still gets a seat rather than nothing.
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
