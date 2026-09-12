using System.Collections.Generic;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The three class cycles as the pilot's three class keys reach them: each key steps its
/// own cycle and leaves the other two alone, a class change lands on the head of the cycle it
/// changes to, a step walks that cycle and wraps, and two pilots over one pool hold two picks.
/// Every source here is a plain object, so the whole order is asserted with no world and no node.
/// </summary>
public class TargetCycleTests
{
    private const int Hostile = AimAssist.PlayerTeam + 1;

    // Four poses whose decoded sector keys are 0, 0, 1 and 2 (ahead twice, behind, left) against
    // the identity basis, so a cycle built from them is ordered by sector and then by distance
    // inside the shared sector, never by the order they were offered in.
    private static readonly Vector3 Ahead = new(0f, 0f, -400f);
    private static readonly Vector3 NearAhead = new(0f, 0f, -150f);
    private static readonly Vector3 Behind = new(0f, 0f, 300f);
    private static readonly Vector3 Left = new(-250f, 0f, 0f);

    /// <summary>Each class key steps its own cycle and nothing else: the ally key moves the ally
    /// pick while the enemy cycle's own order and the non-aircraft cycle's stand unchanged.</summary>
    [Fact]
    public void EachClassKeyStepsItsOwnCycleAlone()
    {
        var world = new Fixture();
        var sel = world.Selection();

        Assert.Equal(TargetClass.Enemy, sel.ActiveClass);
        var firstEnemy = sel.Current!.Value.Source;

        world.Press(sel, TargetClass.Ally);
        Assert.Equal(TargetClass.Ally, sel.ActiveClass);
        Assert.Contains(sel.Current!.Value.Source!, world.AllySources);

        world.Press(sel, TargetClass.NonAircraft);
        Assert.Equal(TargetClass.NonAircraft, sel.ActiveClass);
        Assert.Contains(sel.Current!.Value.Source!, world.NonAircraftSources);

        // Back to the enemy cycle: the entries and their order are the ones it started with, so
        // walking another class never disturbed this one.
        world.PressEnemy(sel);
        Assert.Equal(TargetClass.Enemy, sel.ActiveClass);
        Assert.Equal(world.EnemyOrder(), Sources(sel.Ordered));
        Assert.Same(firstEnemy, sel.Current!.Value.Source);
    }

    /// <summary>A class change lands on the HEAD of the cycle it changes to, whatever index the
    /// pilot had reached in the cycle they left. That is the decoded rule: the handler clears the
    /// selection on a real class change, so the step that follows finds nothing and takes the
    /// first entry.</summary>
    [Fact]
    public void AClassChangeLandsOnTheHeadOfTheNewCycle()
    {
        var world = new Fixture();
        var sel = world.Selection();

        world.PressEnemy(sel);
        world.PressEnemy(sel);
        Assert.Same(world.EnemyOrder()[2], sel.Current!.Value.Source);

        world.Press(sel, TargetClass.NonAircraft);
        Assert.Same(world.NonAircraftOrder()[0], sel.Current!.Value.Source);

        world.Press(sel, TargetClass.Ally);
        Assert.Same(world.AllyOrder()[0], sel.Current!.Value.Source);
    }

    /// <summary>One key reaches every entry of its class: repeated presses walk the whole cycle in
    /// the decoded order and wrap to the head, which is what makes a single Next action enough for
    /// a class with no Previous key behind it.</summary>
    [Fact]
    public void OneKeyWalksItsWholeCycleAndWraps()
    {
        var world = new Fixture();
        var sel = world.Selection();
        var expected = world.EnemyOrder();

        var walked = new List<object> { sel.Current!.Value.Source! };
        for (int i = 1; i < expected.Count; i++)
        {
            world.PressEnemy(sel);
            walked.Add(sel.Current!.Value.Source!);
        }

        Assert.Equal(expected, walked);
        world.PressEnemy(sel);
        Assert.Same(expected[0], sel.Current!.Value.Source);
    }

    /// <summary>Two pilots over the same candidates hold two picks: one pane's key moves that
    /// pane's marker and leaves the other's where it was. The selection is per instance, which is
    /// what a splitscreen pane owns one of.</summary>
    [Fact]
    public void TwoPanesOverOnePoolHoldTheirOwnPick()
    {
        var world = new Fixture();
        var p1 = world.Selection();
        var p2 = world.Selection();
        var p2Was = p2.Current!.Value.Source;

        world.PressEnemy(p1);
        Assert.NotSame(p2Was, p1.Current!.Value.Source);
        Assert.Same(p2Was, p2.Current!.Value.Source);

        world.Press(p2, TargetClass.NonAircraft);
        Assert.Equal(TargetClass.NonAircraft, p2.ActiveClass);
        Assert.Equal(TargetClass.Enemy, p1.ActiveClass);
    }

    /// <summary>Target Nothing empties the cycles and STAYS empty through a rebuild, and each of
    /// the three class keys is a way back in, each to its own class.</summary>
    [Theory]
    [InlineData(TargetClass.Enemy)]
    [InlineData(TargetClass.Ally)]
    [InlineData(TargetClass.NonAircraft)]
    public void AClearedSelectionComesBackOnlyThroughAClassKey(TargetClass cls)
    {
        var world = new Fixture();
        var sel = world.Selection();

        sel.Clear();
        world.Rebuild(sel);
        Assert.Null(sel.ActiveClass);
        Assert.Null(sel.Current);
        Assert.Empty(sel.Pool.Enemy);

        world.Press(sel, cls);
        world.Rebuild(sel);
        Assert.Equal(cls, sel.ActiveClass);
        Assert.NotNull(sel.Current);
    }

    /// <summary>A hostile surface hull is on the ENEMY cycle, not the non-aircraft one: the
    /// decoded class takes what a mission flagged, and a vehicle carrying no flag splits by team
    /// whether it flies or floats (<c>FUN_004b5cd0</c>, docs/org/targeting.md).</summary>
    [Fact]
    public void AnUnflaggedHullRidesTheEnemyCycle()
    {
        Assert.Equal(TargetClass.Enemy,
            TargetRef.Classify(AimTargetKind.Vehicle, live: true, Hostile, AimAssist.PlayerTeam));
        Assert.Equal(TargetClass.NonAircraft,
            TargetRef.Classify(AimTargetKind.Vehicle, live: true, Hostile, AimAssist.PlayerTeam,
                otherTarget: true));
    }

    /// <summary>A turret or a structure is selectable only through the mission's own flags, so the
    /// non-aircraft key reaches a flagged emplacement and never an unflagged one.</summary>
    [Fact]
    public void OnlyAFlaggedTurretOrStructureReachesTheNonAircraftCycle()
    {
        Assert.Null(TargetRef.Classify(AimTargetKind.Turret, live: true, AimAssist.WorldTeam,
            AimAssist.PlayerTeam));
        Assert.Null(TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
            AimAssist.PlayerTeam));
        Assert.Equal(TargetClass.NonAircraft,
            TargetRef.Classify(AimTargetKind.Turret, live: true, AimAssist.WorldTeam,
                AimAssist.PlayerTeam, otherTarget: true));
    }

    private static List<object> Sources(IReadOnlyList<TargetRef> cycle)
    {
        var into = new List<object>(cycle.Count);
        foreach (var target in cycle)
        {
            into.Add(target.Source!);
        }

        return into;
    }

    // One pilot's candidates: three hostiles, two allies and three flagged non-aircraft, each at a
    // pose whose sector key is known, offered through the real pool so the cycles are split the
    // way a session splits them.
    private sealed class Fixture
    {
        private readonly AimCandidateSet _scan = new();
        private readonly List<AimCandidate> _parts = new();

        public Fixture()
        {
            AddVehicle(EnemyNear, NearAhead, Hostile);
            AddVehicle(EnemyFar, Ahead, Hostile);
            AddVehicle(EnemyBehind, Behind, Hostile);
            AddVehicle(Wingman, Ahead, AimAssist.PlayerTeam);
            AddVehicle(Escort, Left, AimAssist.PlayerTeam);
            AddPart(Gasbag, NearAhead);
            AddPart(Engine, Behind);
            AddPart(Emplacement, Left);
        }

        public object EnemyNear { get; } = new();

        public object EnemyFar { get; } = new();

        public object EnemyBehind { get; } = new();

        public object Wingman { get; } = new();

        public object Escort { get; } = new();

        public object Gasbag { get; } = new();

        public object Engine { get; } = new();

        public object Emplacement { get; } = new();

        public IReadOnlyList<object> AllySources => new[] { Wingman, Escort };

        public IReadOnlyList<object> NonAircraftSources => new[] { Gasbag, Engine, Emplacement };

        // The decoded order: sector before distance, so ahead comes first (nearer of the two
        // first), then behind, then left.
        public IReadOnlyList<object> EnemyOrder() => new[] { EnemyNear, EnemyFar, EnemyBehind };

        public IReadOnlyList<object> AllyOrder() => new[] { Wingman, Escort };

        public IReadOnlyList<object> NonAircraftOrder() => new[] { Gasbag, Engine, Emplacement };

        public TargetSelection Selection()
        {
            var selection = new TargetSelection();
            Rebuild(selection);
            return selection;
        }

        public void Rebuild(TargetSelection selection) =>
            selection.Rebuild(_scan, _parts, AimAssist.PlayerTeam, null, Vector3.Zero,
                Basis.Identity);

        // One press of a class key, then the per-frame pass that publishes it. Two calls because
        // the original's handler steps the list the last frame built and the next frame's rebuild
        // is what shows the result.
        public void Press(TargetSelection selection, TargetClass cls)
        {
            selection.Next(cls);
            Rebuild(selection);
        }

        public void PressEnemy(TargetSelection selection)
        {
            selection.NextEnemy();
            Rebuild(selection);
        }

        private void AddVehicle(object source, Vector3 at, int team) =>
            _scan.AddVehicle(at, Vector3.Zero, team, live: true, source);

        private void AddPart(object source, Vector3 at) =>
            _parts.Add(new AimCandidate
            {
                Position = at,
                Velocity = Vector3.Zero,
                Team = AimAssist.WorldTeam,
                Live = true,
                Source = source,
            });
    }
}
