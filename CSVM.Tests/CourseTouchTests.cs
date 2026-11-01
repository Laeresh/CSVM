using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Testing;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The course touch penalty. Nothing collides, and a touch of a course pylon, ring rim or
/// balloon adds 3 s. Each element counts once until the plane leaves its volume.</summary>
public class CourseTouchTests
{
    // Heading 0 faces -Z. The pylon gate's course pylons stand at x = ±20, 30 m tall; the 20 m ring is
    // centred 50 m up, 200 m on.
    private static readonly CourseGate A = new("a", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
    private static readonly CourseGate B = new("b", CourseGateKind.BalloonRing, new Vector3(0f, 50f, -200f), 0f, 0f, 0f, 20f);

    [Fact]
    public void APassThroughEachGateCentreTakesNoPenalty()
    {
        var (run, touches) = Run();
        Pass(run, A, 6f);
        Pass(run, B, 6f);
        Assert.True(run.AllComplete);
        Assert.Equal(0, run.PenaltyCount);
        Assert.Empty(touches);
    }

    [Fact]
    public void BrushingACoursePylonIsOnePenaltyHoweverLongItLasts()
    {
        var (run, touches) = Started();
        for (int i = 0; i < 30; i++)
            run.Update(new Vector3(21f, 10f + (i * 0.1f), 1f - (i * 0.05f)));
        Assert.Equal((1, 3f), (run.PenaltyCount, run.Elapsed));
        Assert.Equal(CourseElement.RightCoursePylon, Assert.Single(touches).Element);
    }

    [Fact]
    public void LeavingAndReenteringTakesASecondPenalty()
    {
        var (run, touches) = Started();
        run.Update(new Vector3(21f, 10f, 0f));
        run.Update(new Vector3(30f, 10f, 0f));
        run.Update(new Vector3(21f, 10f, 0f));
        Assert.Equal((2, 6f), (run.PenaltyCount, run.Elapsed));
        Assert.Equal(2, touches.Count);
    }

    [Fact]
    public void TwoElementsTouchedInOneStepTakeTwo()
    {
        var (run, touches) = Started();
        run.Update(new Vector3(0f, 15f, 0f), 19f); // about 18.3 m to either course pylon's skin
        Assert.Equal(2, run.PenaltyCount);
        Assert.Equal(
            new[] { CourseElement.LeftCoursePylon, CourseElement.RightCoursePylon },
            new[] { touches[0].Element, touches[1].Element });
    }

    [Fact]
    public void ARimAndABalloonEachTouch()
    {
        var (run, touches) = Started();
        run.Update(B.Position + new Vector3(CourseGateShape.TubeCentreline(B), 0f, 0.3f));
        run.Update(B.Position + CourseGateShape.BalloonCentre(B, 1));
        Assert.Equal(2, run.PenaltyCount);
        Assert.Equal((1, CourseElement.RingRim, 0), (touches[0].Gate, touches[0].Element, touches[0].Balloon));
        Assert.Equal((1, CourseElement.Balloon, 1), (touches[1].Gate, touches[1].Element, touches[1].Balloon));
        Assert.Equal(0f, touches[1].PylonHeight);
    }

    [Fact]
    public void AMovingRingsVolumesMoveWithItsMotion()
    {
        var (run, touches) = Started();
        double asked = -1;
        run.SimSeconds = () => 7.25;
        run.GateMotion = (g, t) =>
        {
            asked = t;
            return g == 1 ? new Transform3D(Basis.Identity, new Vector3(100f, 0f, 0f)) : Transform3D.Identity;
        };

        var rim = new Vector3(CourseGateShape.TubeCentreline(B), 0f, 0f);
        run.Update(B.Position + rim); // where the rim stands at rest
        Assert.Empty(touches);
        var moved = B.Position + rim + new Vector3(100f, 0f, 0f);
        run.Update(moved);
        Assert.Equal(CourseElement.RingRim, Assert.Single(touches).Element);
        Assert.Equal(7.25, asked);
        Assert.True(touches[0].Point.IsEqualApprox(moved), $"{touches[0].Point}");
    }

    [Fact]
    public void ACoursePylonTouchNamesItsSidePointAndHeight()
    {
        var (run, touches) = Started();
        // The right course pylon tapers from 2.7 m to 0.75 m over its 30 m; this point is 2.08 m off its flank.
        run.Update(new Vector3(24f, 12f, 0f), 2.5f);
        var touch = Assert.Single(touches);
        Assert.Equal((0, CourseElement.RightCoursePylon, true), (touch.Gate, touch.Element, touch.TimeAdded));
        Assert.True(touch.Point.DistanceTo(new Vector3(21.9287f, 11.8656f, 0f)) < 1e-3f, $"{touch.Point}");
        Assert.Equal(touch.Point.Y, touch.PylonHeight);

        // Turned to heading 90 the gate flies toward -X, so its left course pylon stands at +Z.
        var turned = A with { Heading = 90f };
        var (run2, touches2) = Run(turned);
        run2.Update(new Vector3(0f, 5f, 20f));
        run2.Update(new Vector3(0f, 5f, 20f));
        Assert.Equal(CourseElement.LeftCoursePylon, Assert.Single(touches2).Element);
    }

    [Fact]
    public void ATouchBeforeTheFirstEntryOrOnTheFinishAddsNoTimeButStillFires()
    {
        var (run, touches) = Run();
        run.Update(new Vector3(21f, 10f, 30f)); // the first step only seeds the segment
        run.Update(new Vector3(21f, 10f, 0f));
        Assert.Equal((0, 0f), (run.PenaltyCount, run.Elapsed));
        Assert.False(Assert.Single(touches).TimeAdded);

        Pass(run, A, 0f);
        Pass(run, B, 11.5f); // 11.1 m from 5 m off the centre to the rim's skin
        Assert.True(run.AllComplete);
        Assert.Equal(0, run.PenaltyCount);
        Assert.Equal(CourseElement.RingRim, touches[1].Element);
        Assert.False(touches[1].TimeAdded);

        run.Update(new Vector3(21f, 10f, 0f)); // a finished run tests nothing more
        Assert.Equal(2, touches.Count);
    }

    [ExtractedDataFact]
    public void ADangerZoneRunTestsNoTouchVolumes()
    {
        string gamez = SessionPaths.ChapterGamez(TestData.ExtractedRoot!, "C4");
        var run = StuntMission.Load(GameZ.Load(gamez), TestZrdr.Mission("C4", "IA1"),
            Messages.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "messages.json")))!;
        int touched = 0;
        run.ElementTouched += _ => touched++;
        foreach (var z in run.Zones)
        {
            run.Update(z.Position, 1e5f);
            run.Update(z.Position + Vector3.Up, 1e5f);
        }

        Assert.Equal((0, 0), (touched, run.PenaltyCount));
    }

    private static (StuntMission Run, List<CourseTouch> Touches) Run(CourseGate? first = null)
    {
        var a = first ?? A;
        var sequence = new[] { new SequenceEntry(a.Id, GateDirection.Forward), new SequenceEntry(B.Id, GateDirection.Forward) };
        var run = StuntMission.ForCourse(new Course("c1", "touch", new SpawnPoint(new Vector3(0f, 15f, 300f), 0f), new[] { a, B }, sequence))!;
        var touches = new List<CourseTouch>();
        run.ElementTouched += touches.Add;
        return (run, touches);
    }

    // A run whose clock is running, its first gate flown, the plane last seen just beyond it.
    private static (StuntMission Run, List<CourseTouch> Touches) Started()
    {
        var (run, touches) = Run();
        Pass(run, A, 0f);
        Assert.Equal(1, run.CompletedCount);
        return (run, touches);
    }

    private static void Pass(StuntMission run, CourseGate gate, float radius)
    {
        var centre = gate.Position + (gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, gate.Height / 2f, 0f) : Vector3.Zero);
        run.Relocated();
        run.Update(centre - (gate.Forward * 5f), radius);
        run.Update(centre + (gate.Forward * 5f), radius);
    }
}
