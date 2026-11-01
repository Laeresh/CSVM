using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The strict-order course run: only the next sequence entry registers, from its own side,
/// and the clock runs from the first entry to the last.</summary>
public class CourseRunTests
{
    // Heading 0 faces -Z, so a Forward pass travels from +Z to -Z. The pylon gate's centre is 15 m
    // up its 30 m pylons; the ring stands 200 m on at 50 m.
    private static readonly CourseGate A = new("a", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
    private static readonly CourseGate B = new("b", CourseGateKind.BalloonRing, new Vector3(0f, 50f, -200f), 0f, 0f, 0f, 20f);

    [Fact]
    public void AnInOrderPassAdvancesToTheEnd()
    {
        var run = Run(Fwd(A), Fwd(B));
        int finished = 0;
        run.RunCompleted += () => finished++;

        Pass(run, A);
        Assert.Equal(1, run.CompletedCount);
        Pass(run, B);
        Assert.True(run.AllComplete);
        Assert.Equal(1, finished);
        Assert.Equal(new[] { "Gate 1", "Gate 2" }, new[] { run.Zones[0].Description, run.Zones[1].Description });
    }

    [Fact]
    public void OutOfOrderAndWrongWayCrossingsDoNothing()
    {
        var run = Run(Fwd(A), Fwd(B));

        Pass(run, B);
        Pass(run, A, reverse: true);
        Assert.Equal(0, run.CompletedCount);

        Pass(run, A);
        Pass(run, A); // a gate already flown, not the next entry
        Assert.Equal(1, run.CompletedCount);
        Pass(run, B, reverse: true);
        Assert.Equal(1, run.CompletedCount);
    }

    [Fact]
    public void ARepeatedGateCountsOncePerCrossing()
    {
        var run = Run(Fwd(A), Fwd(A), Rev(A), Fwd(B));

        Pass(run, A);
        Assert.Equal(1, run.CompletedCount);
        Pass(run, A, reverse: true); // entry 2 wants Forward
        Assert.Equal(1, run.CompletedCount);
        Pass(run, A);
        Assert.Equal(2, run.CompletedCount);
        Pass(run, A, reverse: true);
        Assert.Equal(3, run.CompletedCount);
        Pass(run, B);
        Assert.True(run.AllComplete);
    }

    [Fact]
    public void TheClockRunsFromTheFirstEntryToTheLast()
    {
        var run = Run(Fwd(A), Fwd(B));
        int started = 0;
        run.RunStarted += () => started++;

        run.Tick(2f); // flying in to the first gate
        Assert.Equal(0f, run.Elapsed);
        Assert.Equal(0, started);

        Pass(run, A);
        Assert.Equal(1, started);
        Assert.Equal(0f, run.Zones[0].CompletedAt);
        run.Tick(0.5f);
        run.Tick(1f);
        Pass(run, B);
        run.Tick(4f); // after the finish
        Assert.Equal(1.5f, run.Elapsed);
        Assert.Equal(1.5f, run.Zones[1].CompletedAt);
    }

    [Fact]
    public void AResetStartsAgainAtTheFirstEntry()
    {
        var run = Run(Fwd(A), Fwd(B));
        Pass(run, A);
        run.Tick(1f);
        run.AddPenalty(3f);

        run.Reset();
        Assert.Equal((0, 0f, 0), (run.CompletedCount, run.Elapsed, run.PenaltyCount));
        Assert.Null(run.ReturnPose());
        run.Tick(1f);
        Pass(run, B);
        Assert.Equal((0, 0f), (run.CompletedCount, run.Elapsed));
        Pass(run, A);
        Assert.Equal(1, run.CompletedCount);
    }

    [Fact]
    public void AnEmptySequenceIsRefused() =>
        Assert.Null(StuntMission.ForCourse(new Course("c1", "wip", new SpawnPoint(Vector3.Zero, 0f), new[] { A }, new SequenceEntry[0])));

    [Fact]
    public void TheGateMotionAtTheSimTimeMovesTheAperture()
    {
        var run = Run(Fwd(A));
        double asked = -1;
        run.SimSeconds = () => 12.5;
        run.GateMotion = (gate, t) =>
        {
            asked = t;
            return new Transform3D(Basis.Identity, new Vector3(100f, 0f, 0f));
        };

        Pass(run, A); // through where the gate stands at rest
        Assert.Equal(0, run.CompletedCount);
        Assert.Equal(12.5, asked);

        run.Relocated();
        run.Update(new Vector3(100f, 15f, 5f));
        run.Update(new Vector3(100f, 15f, -5f));
        Assert.Equal(1, run.CompletedCount);
    }

    [Fact]
    public void OnlyTheNextEntryIsOfferedAsATarget()
    {
        var run = Run(Fwd(A), Fwd(B));
        var into = new List<AimCandidate>();
        run.CollectTargets(into);
        Assert.Same(run.Zones[0], Assert.Single(into).Source);

        Pass(run, A);
        into.Clear();
        run.CollectTargets(into);
        Assert.Same(run.Zones[1], Assert.Single(into).Source);

        Pass(run, B);
        into.Clear();
        run.CollectTargets(into);
        Assert.Empty(into);
    }

    [Fact]
    public void ATapReturnsThroughTheLastGateFlownTheWayItWasFlown()
    {
        var run = Run(Fwd(A), Rev(A));
        Assert.Null(run.ReturnPose());

        Pass(run, A);
        Pass(run, A, reverse: true);
        var (pos, heading) = run.ReturnPose()!.Value;
        Assert.True(pos.IsEqualApprox(new Vector3(0f, 15f, 0f)), $"{pos}");
        Assert.True(heading.IsEqualApprox(Vector3.Back), $"{heading}");
    }

    [Fact]
    public void APenaltyAddsToARunningClockOnly()
    {
        var run = Run(Fwd(A), Fwd(B));
        run.AddPenalty(3f);
        Assert.Equal((0f, 0), (run.Elapsed, run.PenaltyCount));

        Pass(run, A);
        run.AddPenalty(3f);
        Assert.Equal((3f, 1), (run.Elapsed, run.PenaltyCount));
        Pass(run, B);
        run.AddPenalty(3f);
        Assert.Equal((3f, 1), (run.Elapsed, run.PenaltyCount));
    }

    [Fact]
    public void AnotherPlayersCopyFliesItsOwnCourseRun()
    {
        var run = Run(Fwd(A), Fwd(B));
        var other = run.ForAnotherPlayer();

        Pass(run, A);
        Assert.Equal((1, 0), (run.CompletedCount, other.CompletedCount));
        Assert.Same(run.Course, other.Course);
        Pass(other, B);
        Assert.Equal(0, other.CompletedCount);
    }

    private static StuntMission Run(params SequenceEntry[] sequence) =>
        StuntMission.ForCourse(new Course("c1", "test", new SpawnPoint(new Vector3(0f, 15f, 300f), 0f), new[] { A, B }, sequence))!;

    private static SequenceEntry Fwd(CourseGate g) => new(g.Id, GateDirection.Forward);

    private static SequenceEntry Rev(CourseGate g) => new(g.Id, GateDirection.Reverse);

    // One step straight through the gate's centre, along its forward or against it.
    private static void Pass(StuntMission run, CourseGate gate, bool reverse = false)
    {
        var centre = gate.Position + (gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, gate.Height / 2f, 0f) : Vector3.Zero);
        var along = gate.Forward * (reverse ? -5f : 5f);
        run.Relocated();
        run.Update(centre - along);
        run.Update(centre + along);
    }
}
