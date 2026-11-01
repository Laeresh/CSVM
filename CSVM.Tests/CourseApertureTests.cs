using CSVM.Flight.Modes;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class CourseApertureTests
{
    // Heading 0 faces -Z, so a Forward crossing travels from +Z to -Z.
    private static readonly CourseAperture Pylons =
        new(new CourseGate("p", CourseGateKind.PylonGate, new Vector3(100f, 10f, 0f), 0f, 40f, 30f, 0f));

    private static readonly CourseAperture Ring =
        new(new CourseGate("r", CourseGateKind.BalloonRing, new Vector3(0f, 80f, 0f), 0f, 0f, 0f, 20f));

    private static readonly Transform3D Still = Transform3D.Identity;

    [Fact]
    public void AForwardPassThroughCounts()
    {
        Assert.True(Pylons.CrossedBy(new(100f, 25f, 5f), new(100f, 25f, -5f), GateDirection.Forward, Still, out float t));
        Assert.Equal(0.5f, t, 4);
        Assert.True(Ring.CrossedBy(new(2f, 82f, 1f), new(2f, 82f, -3f), GateDirection.Forward, Still, out t));
        Assert.Equal(0.25f, t, 4);
    }

    [Fact]
    public void ACrossingAgainstTheEntrysDirectionIsRefused()
    {
        Vector3 front = new(100f, 25f, -5f), back = new(100f, 25f, 5f);

        Assert.False(Pylons.CrossedBy(front, back, GateDirection.Forward, Still, out _));
        Assert.True(Pylons.CrossedBy(front, back, GateDirection.Reverse, Still, out _));
        Assert.False(Pylons.CrossedBy(back, front, GateDirection.Reverse, Still, out _));
        Assert.False(Ring.CrossedBy(new(0f, 80f, -1f), new(0f, 80f, 1f), GateDirection.Forward, Still, out _));
        Assert.True(Ring.CrossedBy(new(0f, 80f, -1f), new(0f, 80f, 1f), GateDirection.Reverse, Still, out _));
    }

    [Theory]
    [InlineData(125f, 25f)] // beside a course pylon, outside the 40 m width
    [InlineData(100f, 41f)] // above the 30 m course pylon tops
    [InlineData(100f, 9f)] // below the ground at the gate's base
    public void APylonGateMissIsNotACrossing(float x, float y) =>
        Assert.False(Pylons.CrossedBy(new(x, y, 5f), new(x, y, -5f), GateDirection.Forward, Still, out _));

    [Fact]
    public void ARingMissOutsideTheDiameterIsNotACrossing()
    {
        Assert.False(Ring.CrossedBy(new(10.5f, 80f, 5f), new(10.5f, 80f, -5f), GateDirection.Forward, Still, out _));
        Assert.False(Ring.CrossedBy(new(7.2f, 87.2f, 5f), new(7.2f, 87.2f, -5f), GateDirection.Forward, Still, out _));
        Assert.True(Ring.CrossedBy(new(9.9f, 80.5f, 5f), new(9.9f, 80.5f, -5f), GateDirection.Forward, Still, out _));
    }

    [Fact]
    public void AFastStepCrossesInOneSegment()
    {
        Assert.True(Pylons.CrossedBy(new(90f, 20f, 400f), new(110f, 30f, -100f), GateDirection.Forward, Still, out float t));
        Assert.Equal(0.8f, t, 4);
    }

    [Fact]
    public void TheHeadingTurnsTheAperture()
    {
        var east = new CourseAperture(new CourseGate("e", CourseGateKind.PylonGate, Vector3.Zero, -90f, 40f, 30f, 0f));

        Assert.True(east.Gate.Forward.IsEqualApprox(Vector3.Right));
        Assert.True(east.CrossedBy(new(-5f, 10f, 15f), new(5f, 10f, 15f), GateDirection.Forward, Still, out _));
        Assert.False(east.CrossedBy(new(-5f, 10f, 25f), new(5f, 10f, 25f), GateDirection.Forward, Still, out _));
    }

    [Fact]
    public void AMovedPoseShiftsTheAperture()
    {
        var bob = new Transform3D(Basis.Identity, new Vector3(0f, 5f, 0f));
        Vector3 high = new(0f, 94f, 5f), highThrough = new(0f, 94f, -5f);

        Assert.False(Ring.CrossedBy(high, highThrough, GateDirection.Forward, Still, out _));
        Assert.True(Ring.CrossedBy(high, highThrough, GateDirection.Forward, bob, out _));
        Assert.False(Ring.CrossedBy(new(0f, 71f, 5f), new(0f, 71f, -5f), GateDirection.Forward, bob, out _));

        // A sway about the ring's own forward keeps the centre and the facing.
        var sway = new Transform3D(new Basis(Vector3.Forward, 0.3f), Vector3.Zero);
        Assert.True(Ring.CrossedBy(new(0f, 80f, 5f), new(0f, 80f, -5f), GateDirection.Forward, sway, out _));
    }

    [Fact]
    public void ADangerZoneGateStillCountsEitherSide()
    {
        var gate = new StuntGate
        {
            Vertices = new[] { new Vector3(-5f, -5f, 0f), new Vector3(5f, -5f, 0f), new Vector3(5f, 5f, 0f), new Vector3(-5f, 5f, 0f) },
            Center = Vector3.Zero,
            Normal = Vector3.Back,
        };

        Assert.True(gate.CrossedBy(new(0f, 0f, -1f), new(0f, 0f, 3f), out float t));
        Assert.Equal(0.25f, t, 4);
        Assert.True(gate.CrossedBy(new(0f, 0f, 3f), new(0f, 0f, -1f), out t));
        Assert.Equal(0.75f, t, 4);
        Assert.False(gate.CrossedBy(new(6f, 0f, -1f), new(6f, 0f, 1f), out _));
    }
}
