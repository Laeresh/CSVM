using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The aircraft weapon-marker rig (<c>docs/formats/markers.md</c>): which node names count as
/// markers, and how their plane-frame positions accumulate. Input is
/// <c>fixtures/gamez-plane/</c>.
/// </summary>
public class MarkerRigTests
{
    [Theory]
    [InlineData("firepoint1", MarkerRig.MarkerKind.Firepoint, 1)]
    [InlineData("FIREPOINT8", MarkerRig.MarkerKind.Firepoint, 8)]
    [InlineData("pylon3", MarkerRig.MarkerKind.Pylon, 3)]
    [InlineData("target", MarkerRig.MarkerKind.Target, 0)]
    public void NumberedMarkersAndTheTargetAreClassified(string name, MarkerRig.MarkerKind kind, int ordinal)
    {
        Assert.True(MarkerRig.Classify(name, out var got, out int gotOrdinal));
        Assert.Equal(kind, got);
        Assert.Equal(ordinal, gotOrdinal);
    }

    [Theory]
    [InlineData("firepoint")]   // the AI airframes' bare, unnumbered marker
    [InlineData("pylon")]
    [InlineData("firepoint0")]  // ordinals are 1-based
    [InlineData("lpylon1")]     // the AI naming, not the player rig
    [InlineData("cockpit_camera")]
    [InlineData("ground_level")]
    public void EverythingElseInTheMarkersGroupIsRejected(string name)
    {
        Assert.False(MarkerRig.Classify(name, out _, out _));
    }

    [Fact]
    public void CoLocationGroupsOnlyPointsWithinTolerance()
    {
        var positions = new List<Vector3>
        {
            new(1f, 0f, 0f),
            new(1f, 0f, 0.001f),  // same mount
            new(-1f, 0f, 0f),
            new(1f, 0f, 0f),      // same mount
        };
        var groups = MarkerRig.GroupCoLocated(positions, MarkerRig.CoLocateTolerance);
        Assert.Single(groups);
        Assert.Equal(new[] { 0, 1, 3 }, groups[0]);
    }

    [Fact]
    public void DistinctMountsProduceNoGroups()
    {
        var positions = new List<Vector3> { new(1f, 0f, 0f), new(-1f, 0f, 0f) };
        Assert.Empty(MarkerRig.GroupCoLocated(positions, MarkerRig.CoLocateTolerance));
    }

    [Fact]
    public void PositionsAreAccumulatedInThePlaneFrameNotTheScene()
    {
        // The plane root's own transform places the airframe in a world; markers.md quotes
        // positions relative to the airframe origin, so the walk starts BELOW the root.
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        Assert.Equal("probe_plane", rig.PlaneRoot);
        Assert.Equal(4, rig.Markers.Count); // cockpit_camera is not a marker

        var fp1 = Find(rig, "firepoint1");
        Assert.Equal(new Vector3(-2f, 1f, -3f), fp1.Local); // markers group's +1 y, root's +100 x dropped
        Assert.Equal(new Vector3(2f, 1f, -3f), Find(rig, "firepoint2").Local);
        Assert.Equal(new Vector3(0f, 1f, 0f), Find(rig, "target").Local);
    }

    [Fact]
    public void MarkersAreSortedByKindThenOrdinal()
    {
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        Assert.Equal(
            new[] { "firepoint1", "firepoint2", "pylon1", "target" },
            Names(rig));
    }

    [Fact]
    public void TwoMarkersOnOneMountAreReportedAsShared()
    {
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        var group = Assert.Single(rig.CoLocated);
        var shared = new List<string>();
        foreach (int i in group)
        {
            shared.Add(rig.Markers[i].Name);
        }
        shared.Sort();
        Assert.Equal(new[] { "firepoint1", "pylon1" }, shared);
    }

    [Fact]
    public void AnAbsentPlaneRootYieldsNullRatherThanAnEmptyRig()
    {
        Assert.Null(MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "player_not_here"));
    }

    [Fact]
    public void FindNamedMarkerAccumulatesFromBelowTheRootAndSkipsAlternateStateSubtrees()
    {
        // "cockpit1" nests its own decoy cockpit_camera (9,9,9) as the FIRST child under
        // "markers", so a naive first-match walk would find it before the authored one at
        // markers' +1 y plus cockpit_camera's local +0.5 y.
        var offset = MarkerRig.FindNamedMarker(
            GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane", "cockpit_camera");
        Assert.Equal(new Vector3(0f, 1.5f, 0f), offset);
    }

    [Fact]
    public void FindNamedMarkerFallsBackWhenTheNodeIsAbsent()
    {
        var fallback = new Vector3(1f, 2f, 3f);
        var gamez = GameZ.Load(TestData.Fixture("gamez-plane"));
        Assert.Equal(fallback, MarkerRig.FindNamedMarker(gamez, "probe_plane", "no_such_node", fallback));
        Assert.Equal(fallback, MarkerRig.FindNamedMarker(gamez, "player_not_here", "cockpit_camera", fallback));
        Assert.Equal(Vector3.Zero, MarkerRig.FindNamedMarker(gamez, "player_not_here", "cockpit_camera"));
    }

    private static MarkerRig.Marker Find(MarkerRig rig, string name)
    {
        foreach (var m in rig.Markers)
        {
            if (m.Name == name)
            {
                return m;
            }
        }
        throw new Xunit.Sdk.XunitException($"marker '{name}' not in the rig");
    }

    private static string[] Names(MarkerRig rig)
    {
        var names = new string[rig.Markers.Count];
        for (int i = 0; i < rig.Markers.Count; i++)
        {
            names[i] = rig.Markers[i].Name;
        }
        return names;
    }
}
