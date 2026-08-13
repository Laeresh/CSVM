using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Walks an <see cref="AiNet"/> patrol graph as a stream of waypoints: positions in,
/// the current target node out (M4 B5). Deliberately aircraft-agnostic: nothing here knows
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
/// the follower is horizontally (XZ) within the radius. Horizontal, because the placeholder
/// control law converges on altitude slowly, and the pilot model (wave D) owns real
/// waypoint-arrival and turn behaviour. Replace, do not tune, when that lands.</para>
///
/// <para><b>The trailer and the per-node tags ride along untouched.</b> The net's trailer
/// (its attach/follow target, e.g. <c>[10, "player"]</c>) is recorded and exposed via
/// <see cref="AiNet.Trailer"/> but not acted on; target-relative motion is later-wave work.
/// Per-node tags are preserved raw on <see cref="AiNetNode.Tags"/>; both decoded readings
/// (stop-point id vs segment id) are still open (F17), so this class acts on neither.</para></summary>
public sealed class AiNetFollower
{
    /// <summary>The capture radius, metres: an invented value, not original behaviour, sized to
    /// the placeholder control law's tracking error on the tightest fighter rings (measured on
    /// C1's M4ReinfAce: the law orbits a node at ~200 m; wave D's real maneuvering shrinks
    /// this).</summary>
    public const float DefaultArrivalRadius = 200f;

    private readonly Random _rng;
    private readonly float _arrivalRadius;
    private readonly int[][] _neighbors;
    private int _previousIndex = -1;

    public AiNetFollower(AiNet net, Random rng, float arrivalRadius = DefaultArrivalRadius)
    {
        if (net.Nodes.Count == 0)
            throw new ArgumentException($"net '{net.Name}#{net.Id}' has no nodes", nameof(net));
        Net = net;
        _rng = rng;
        _arrivalRadius = arrivalRadius;

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

    /// <summary>The position of the node currently flown toward. Only valid once
    /// <see cref="Update"/> has run.</summary>
    public Vector3 CurrentTarget => Net.Nodes[CurrentIndex].Position;

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

    private int NearestNode(Vector3 position)
    {
        int best = 0;
        float bestSq = float.MaxValue;
        for (int i = 0; i < Net.Nodes.Count; i++)
        {
            float dSq = Net.Nodes[i].Position.DistanceSquaredTo(position);
            if (dSq < bestSq)
            {
                bestSq = dSq;
                best = i;
            }
        }
        return best;
    }
}
