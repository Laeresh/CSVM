using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The rule that finds the boats an animation carries across the sea (<see cref="OceanMovers"/>).
/// These hold which played events move a node and when a triangle reaches the waterline. They also
/// hold the sea test under a hull's origin, and which hulls keep a zone when slots run out.
/// </summary>
public class OceanMoversTests
{
    [Fact]
    public void ATranslatingMotionMovesItsTarget()
    {
        var ev = Event("ObjectMotionFromTo", ("name", "yacht1"), ("translate", new Dictionary<string, object?>()));
        Assert.Equal(new[] { "yacht1" }, OceanMovers.MovedNames(ev));
    }

    [Fact]
    public void ARotationOnlyMotionLeavesItsTargetInPlace()
    {
        var ev = Event("ObjectMotionFromTo", ("name", "sinker"), ("translate", null), ("rotate", new Dictionary<string, object?>()));
        Assert.Empty(OceanMovers.MovedNames(ev));
    }

    [Fact]
    public void AnSiScriptAndAPlayedTranslateStateMoveTheirTargets()
    {
        Assert.Equal(new[] { "tugandbarge01" }, OceanMovers.MovedNames(Event("ObjectMotionSiScript", ("name", "tugandbarge01"), ("index", 0))));
        Assert.Equal(new[] { "barracuda" }, OceanMovers.MovedNames(Event("ObjectTranslateState", ("node", "barracuda"))));
    }

    [Fact]
    public void BallisticDebrisAndSpinsAreNotMovers()
    {
        var debris = Event("ObjectMotion", ("node", "part1"), ("translation_range", new Dictionary<string, object?>()));
        var spin = Event("ObjectMotion", ("node", "prop"), ("xyz_rotation", new Dictionary<string, object?>()));
        Assert.Empty(OceanMovers.MovedNames(debris));
        Assert.Empty(OceanMovers.MovedNames(spin));
    }

    [Fact]
    public void TheAllNamesFormMovesEachRecordAndAScopedNameGivesItsLeaf()
    {
        var motions = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "walker1" },
            new Dictionary<string, object?> { ["node_path"] = new List<object?> { "ship", "lifeboat" } },
        };
        var ev = Event(AnimDefinition.AllNamesKind, ("motions", motions));
        Assert.Equal(new[] { "walker1", "lifeboat" }, OceanMovers.MovedNames(ev));
    }

    [Fact]
    public void ADefinitionNamesEachMoverOnceAndIgnoresItsResetState()
    {
        var def = new AnimDefinition();
        var seq = new AnimSequence();
        seq.Events.Add(Event("ObjectMotionFromTo", ("name", "sailboat3"), ("translate", new Dictionary<string, object?>())));
        seq.Events.Add(Event("ObjectMotionFromTo", ("name", "SAILBOAT3"), ("translate", new Dictionary<string, object?>())));
        seq.Events.Add(Event("ObjectActiveState", ("node", "sail_emit1"), ("state", true)));
        def.Sequences.Add(seq);
        def.ResetState = new AnimSequence();
        def.ResetState.Events.Add(Event("ObjectTranslateState", ("node", "placed_once")));
        Assert.Equal(new[] { "sailboat3" }, OceanMovers.MovedNames(def));
    }

    [Theory]
    [InlineData(-6f, -2.5f, false)]
    [InlineData(-6f, -2f, true)]
    [InlineData(-1f, 30f, true)]
    [InlineData(2f, 30f, true)]
    [InlineData(2.5f, 30f, false)]
    public void ATriangleReachesTheWaterlineWithinTwoMetres(float low, float high, bool reaches) =>
        Assert.Equal(reaches, OceanMovers.ReachesWaterline(new Vector3(0f, low, 0f), new Vector3(10f, high, 0f), new Vector3(0f, high, 10f)));

    [Theory]
    [InlineData(2f, 2f, true)]
    [InlineData(5f, 5f, true)]
    [InlineData(0f, 0f, true)]
    [InlineData(6f, 6f, false)]
    [InlineData(-1f, 3f, false)]
    public void TheSeaTestCoversATrianglesShadowEitherWinding(float x, float z, bool covered)
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0.3f, 0f);
        var c = new Vector3(0f, -0.2f, 10f);
        var p = new Vector2(x, z);
        Assert.Equal(covered, OceanMovers.Covers(a, b, c, p));
        Assert.Equal(covered, OceanMovers.Covers(a, c, b, p));
    }

    [Fact]
    public void WithMoreHullsThanSlotsTheNearestKeepTheirListingOrder()
    {
        var hulls = new List<Vector3>
        {
            new(1000f, 0f, 0f), new(10f, 0f, 0f), new(500f, 0f, 0f), new(20f, 0f, 0f), new(30f, 0f, 0f),
        };
        Assert.Equal(new[] { 1, 3, 4 }, OceanMovers.Nearest(hulls, Vector3.Zero, 3));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, OceanMovers.Nearest(hulls, Vector3.Zero, 16));
    }

    [Fact]
    public void EquidistantHullsKeepTheEarlierListed()
    {
        var hulls = new List<Vector3> { new(0f, 0f, 50f), new(50f, 0f, 0f), new(0f, 0f, -50f) };
        Assert.Equal(new[] { 0, 1 }, OceanMovers.Nearest(hulls, Vector3.Zero, 2));
    }

    private static AnimEvent Event(string kind, params (string Key, object? Value)[] data) => new()
    {
        Kind = kind,
        Data = new AnimData(data.ToDictionary(d => d.Key, d => d.Value)),
    };
}
