using System.Collections.Generic;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>A course run's HUD words: the status line, the cleared flash, the "+3 s" flash and the
/// marker on the next sequence entry.</summary>
public class CourseHudTests
{
    // Heading 0 faces -Z. The pylon gate's course pylons stand at x = ±20; the ring is 200 m on.
    private static readonly CourseGate A = new("a", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
    private static readonly CourseGate B = new("b", CourseGateKind.BalloonRing, new Vector3(0f, 50f, -200f), 0f, 0f, 0f, 20f);

    [Fact]
    public void TheStatusLineCountsTheNextEntryAndThePenaltyTime()
    {
        Assert.Equal("0:00.0    GATE 1/5", StuntRunHud.CourseStatusLine(0f, 0, 5, 0));
        Assert.Equal("0:14.2    GATE 2/5    +6 s", StuntRunHud.CourseStatusLine(14.2f, 1, 5, 2));
        Assert.Equal("1:02.5    GATE 5/5    +3 s", StuntRunHud.CourseStatusLine(62.5f, 5, 5, 1));
        Assert.Equal("+7.5 s", StuntRunHud.PenaltyText(7.5f));
    }

    [Fact]
    public void ARunReadsInCourseWords()
    {
        var run = Run(Fwd(A), Fwd(B));
        string? flash = null;
        run.ZoneCompleted += z => flash = StuntRunHud.ClearedFlash(run, z);
        Assert.Equal("0:00.0    GATE 1/2", StuntRunHud.StatusLine(run));

        Pass(run, A);
        run.Tick(2.5f);
        Assert.Equal("GATE 1  0:00.0", flash);
        Brush(run);
        Assert.Equal("0:05.5    GATE 2/2    +3 s", StuntRunHud.StatusLine(run));
        Pass(run, B);
        Assert.Equal("GATE 2  0:05.5", flash);
    }

    [Fact]
    public void ThePenaltyFlashShowsOnlyTimeAddedAndAddsUpWhileShown()
    {
        var flash = default(StuntRunHud.PenaltyFlash);
        flash.Touched(Touch(timeAdded: false));
        Assert.False(flash.Shown);

        flash.Touched(Touch(timeAdded: true));
        Assert.True(flash.Shown);
        Assert.Equal("+3 s", flash.Text);
        flash.Step(1f);
        flash.Touched(Touch(timeAdded: true));
        Assert.Equal("+6 s", flash.Text);
        flash.Step(1.5f);
        Assert.True(flash.Shown);
        Assert.Equal(0.1f / 0.4f, flash.Alpha, 3);
        flash.Step(0.2f);
        Assert.False(flash.Shown);

        flash.Touched(Touch(timeAdded: true));
        Assert.Equal("+3 s", flash.Text);
    }

    [Fact]
    public void TheMarkerNamesTheNextEntryAndNoDangerZone()
    {
        var run = Run(Fwd(A), Fwd(B));
        Pass(run, A);
        var zones = new List<AimCandidate>();
        run.CollectTargets(zones);
        var selection = new TargetSelection();
        selection.Rebuild(new AimCandidateSet(), null, AimAssist.PlayerTeam, null, Vector3.Zero, Basis.Identity, zones);
        Assert.True(selection.Current.HasValue);

        var lines = new List<string>();
        TargetHud.LabelLines(selection.Current!.Value, "12 o'clock", lines, keepSlots: false);
        Assert.Equal(new[] { "Gate 2", "12 o'clock" }, lines);
        lines.Clear();
        TargetHud.LabelLines(selection.Current!.Value, null, lines);
        Assert.Equal(new[] { "", "Gate 2" }, lines);
    }

    private static CourseTouch Touch(bool timeAdded) =>
        new(0, CourseElement.LeftCoursePylon, 0, Vector3.Zero, 10f, timeAdded);

    private static StuntMission Run(params SequenceEntry[] sequence) =>
        StuntMission.ForCourse(new Course("c1", "test", new SpawnPoint(new Vector3(0f, 15f, 300f), 0f), new[] { A, B }, sequence))!;

    private static SequenceEntry Fwd(CourseGate g) => new(g.Id, GateDirection.Forward);

    // One step straight through the gate's centre along its forward.
    private static void Pass(StuntMission run, CourseGate gate)
    {
        var centre = gate.Position + (gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, gate.Height / 2f, 0f) : Vector3.Zero);
        run.Relocated();
        run.Update(centre - (gate.Forward * 5f));
        run.Update(centre + (gate.Forward * 5f));
    }

    // Into the right course pylon of gate A from clear of it, beside the gate's plane.
    private static void Brush(StuntMission run)
    {
        run.Relocated();
        run.Update(new Vector3(30f, 10f, 1f));
        run.Update(new Vector3(21f, 10f, 1f));
    }
}
