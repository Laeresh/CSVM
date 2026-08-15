using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The patrol-net walk (M4 B5), engine-free on hand-authored nets: traversal follows the EDGE
/// list (never node order), starts at the nearest node, refuses to backtrack while an onward
/// edge exists, turns back at a dead end, resolves branches reproducibly from its seed, and the
/// id/name lookups cover both ways the data references a net.
/// </summary>
public class AiNetFollowerTests
{
    [Fact]
    public void StartsAtTheNearestNode()
    {
        var f = new AiNetFollower(Loop(), new Random(1));
        Assert.Equal(-1, f.CurrentIndex);
        Assert.True(f.Update(new Vector3(900f, 400f, 1100f))); // nearest is node 2 (1000,1000)
        Assert.Equal(2, f.CurrentIndex);
        Assert.Equal(new Vector3(1000f, 400f, 1000f), f.CurrentTarget);
    }

    [Fact]
    public void LapsALoopAlongItsEdgesWithoutBacktracking()
    {
        // From 0 the seed picks 1 or 3; every later step has exactly one onward neighbour, so
        // the lap direction is fixed after the first hop and the walk must keep circling.
        var visited = Walk(new AiNetFollower(Loop(), new Random(1)), 8);
        Assert.Equal(0, visited[0]);
        for (int i = 1; i < visited.Count; i++)
        {
            Assert.NotEqual(visited[i], visited[i - 1]);
            if (i >= 2)
                Assert.NotEqual(visited[i], visited[i - 2]); // never the node just left
        }
        // Two full laps: position 5 revisits position 1's node, and so on around.
        for (int i = 5; i < visited.Count; i++)
            Assert.Equal(visited[i - 4], visited[i]);
    }

    [Fact]
    public void TurnsBackAtADeadEnd()
    {
        var visited = Walk(new AiNetFollower(Path(), new Random(1)), 6);
        // 0 → 1 → 2 (dead end) → 1 → 0 (dead end) → 1 → 2: the ping-pong patrol of an open path.
        Assert.Equal(new List<int> { 0, 1, 2, 1, 0, 1, 2 }, visited);
    }

    [Fact]
    public void BranchChoiceIsSeedDeterministicAndAlwaysAnEdge()
    {
        var net = Star();
        var a = Walk(new AiNetFollower(net, new Random(42)), 20);
        var b = Walk(new AiNetFollower(net, new Random(42)), 20);
        Assert.Equal(a, b); // the same seed flies the same route
        for (int i = 1; i < a.Count; i++)
        {
            bool isEdge = net.Edges.Contains((a[i - 1], a[i])) || net.Edges.Contains((a[i], a[i - 1]));
            Assert.True(isEdge, $"hop {a[i - 1]}→{a[i]} is not an edge of the net");
        }
    }

    [Fact]
    public void ArrivalIsHorizontalWithinTheCaptureRadius()
    {
        var f = new AiNetFollower(Path(), new Random(1));
        f.Update(Vector3.Zero);
        Assert.Equal(0, f.CurrentIndex);
        // 250 m short horizontally: not arrived, whatever the altitude.
        Assert.False(f.Update(new Vector3(250f, 400f, 0f)));
        Assert.Equal(0, f.CurrentIndex);
        // Within the (invented) 200 m capture radius in XZ, 300 m of altitude error is ignored:
        // neither the deleted placeholder law nor the ported one converges on altitude quickly
        // through a level patrol turn, so arrival is horizontal by design.
        Assert.True(f.Update(new Vector3(150f, 700f, 0f)));
        Assert.Equal(1, f.CurrentIndex);
        Assert.Equal(1, f.Advances);
    }

    [Fact]
    public void AnIsolatedNodeIsHeldNotEscaped()
    {
        var net = new AiNet
        {
            Id = 10,
            Name = "TestIsolated",
            Nodes = new[] { Node(0f, 0f) },
            Edges = Array.Empty<(int, int)>(),
        };
        var f = new AiNetFollower(net, new Random(1));
        f.Update(Vector3.Zero);
        Assert.False(f.Update(f.CurrentTarget));
        Assert.Equal(0, f.CurrentIndex);
        Assert.Equal(0, f.Advances);
    }

    [Fact]
    public void TrailerAndTagsRideAlongUntouched()
    {
        var net = new AiNet
        {
            Id = 11,
            Name = "TestTagged",
            Nodes = new[]
            {
                new AiNetNode(new Vector3(0f, 400f, 0f), new[] { 3f, 1f }),
                Node(1000f, 0f),
            },
            Edges = new[] { (0, 1) },
            Trailer = new AiNetTrailer(1, "piratezep"),
        };
        var f = new AiNetFollower(net, new Random(1));
        // Exposed raw for later waves; the follower acts on neither (stop-point vs segment id
        // is still open, F17 owns resolving it, and the trailer target is not flown to).
        Assert.Equal(new[] { 3f, 1f }, f.Net.Nodes[0].Tags);
        Assert.Equal(new AiNetTrailer(1, "piratezep"), f.Net.Trailer);
    }

    [Fact]
    public void LookupWorksBothWaysTheDataReferencesNets()
    {
        var nets = new List<AiNet> { Loop(), Path(), Star() };
        Assert.Same(nets[1], AiNets.ById(nets, 8));
        Assert.Null(AiNets.ById(nets, 99));
        Assert.Same(nets[2], AiNets.ByName(nets, "TestStar"));
        Assert.Same(nets[2], AiNets.ByName(nets, "teststar")); // the join key, case-insensitive
        Assert.Null(AiNets.ByName(nets, "NoSuchNet"));
        Assert.Same(nets[0], AiNets.Resolve(nets, "7"));       // all digits reads as an id
        Assert.Same(nets[0], AiNets.Resolve(nets, "TestLoop"));
        Assert.Null(AiNets.Resolve(nets, "12"));
    }

    private static AiNetNode Node(float x, float z) => new(new Vector3(x, 400f, z), Array.Empty<float>());

    /// <summary>A 4-node square loop, 0-1-2-3-0. Edge (0,3) closes it, so a directed reading
    /// would dead-end; the undirected walk is what carries the lap.</summary>
    private static AiNet Loop() => new()
    {
        Id = 7,
        Name = "TestLoop",
        Nodes = new[] { Node(0f, 0f), Node(1000f, 0f), Node(1000f, 1000f), Node(0f, 1000f) },
        Edges = new[] { (0, 1), (1, 2), (2, 3), (0, 3) },
    };

    /// <summary>An open 3-node path 0-1-2, with no loop to hide a backtracking bug in.</summary>
    private static AiNet Path() => new()
    {
        Id = 8,
        Name = "TestPath",
        Nodes = new[] { Node(0f, 0f), Node(1000f, 0f), Node(2000f, 0f) },
        Edges = new[] { (0, 1), (1, 2) },
    };

    /// <summary>A hub with three spokes: node 0 connects to 1, 2 and 3 (the branch case).</summary>
    private static AiNet Star() => new()
    {
        Id = 9,
        Name = "TestStar",
        Nodes = new[] { Node(0f, 0f), Node(1000f, 0f), Node(0f, 1000f), Node(-1000f, 0f) },
        Edges = new[] { (0, 1), (0, 2), (0, 3) },
    };

    // Walks by teleporting to each target in turn. Arrival is by construction, so what is
    // under test is purely which node the follower picks next.
    private static List<int> Walk(AiNetFollower f, int hops)
    {
        var visited = new List<int>();
        f.Update(Vector3.Zero); // first call: nearest node
        visited.Add(f.CurrentIndex);
        for (int i = 0; i < hops; i++)
        {
            Assert.True(f.Update(f.CurrentTarget), $"hop {i}: standing at the target did not advance");
            visited.Add(f.CurrentIndex);
        }
        return visited;
    }
}
