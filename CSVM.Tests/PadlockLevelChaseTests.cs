using CSVM.Flight.Camera;
using Godot;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// The head under Assisted turns in the level frame. A padlock bearing and autohead's lead
/// therefore have to be taken in that frame too.
/// </summary>
public class PadlockLevelChaseTests
{
    private readonly ITestOutputHelper _out;

    public PadlockLevelChaseTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData(0f, 30f, 10f)]
    [InlineData(60f, 30f, 10f)]
    [InlineData(-60f, -40f, 20f)]
    [InlineData(80f, 0f, 45f)]
    [InlineData(150f, 20f, -10f)]
    public void TheLevelChaseLooksAtThePadlockedTarget(float bankDeg, float bearingDeg, float elevationDeg)
    {
        var attitude = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.2f)
            * new Basis(Vector3.Forward, Mathf.DegToRad(bankDeg));
        // The target's world direction: bearing left of the nose's heading, elevation above level.
        var toTarget = new Basis(Vector3.Up, 0.7f + Mathf.DegToRad(bearingDeg))
            * new Basis(Vector3.Right, Mathf.DegToRad(elevationDeg)) * Vector3.Forward;

        // A held frame, as a turn already flown would leave it: the level right nearest the wing.
        LevelChase.Step(attitude, null, 0f, out var held);
        var view = LevelChase.Step(attitude, held, 1f / 60f, out _);

        var offset = LevelChase.Frame(attitude, held).Inverse() * toTarget;
        var (el, az) = HeadLook.PadlockTargets(offset);
        var (_, aim) = CameraController.AuthoredRig(el, az, 0f, 0f);
        var look = -(view * aim).Z;

        float missDeg = Mathf.RadToDeg(look.AngleTo(toTarget));
        _out.WriteLine($"bank {bankDeg} miss {missDeg:0.00} deg");
        Assert.True(missDeg < 0.5f, $"the camera looks {missDeg:0.0} deg off the target");
    }

    [Fact]
    public void AutoheadLeadsALevelTurnAlongTheHorizonInTheLevelFrame()
    {
        // A level right turn at 60 deg of bank: the world turns about its up axis only.
        var attitude = new Basis(Vector3.Up, 0.4f) * new Basis(Vector3.Forward, Mathf.DegToRad(60f));
        Assert.True(attitude.X.Y < 0f, "right wing low");
        var bodyRates = attitude.Inverse() * new Vector3(0f, -0.3f, 0f);
        var plane = HeadLook.AutoheadTarget(bodyRates, 1f, 0.5f, -0.5f);

        var level = LevelChase.Frame(attitude, null);
        var (el, az) = HeadLook.Reframe(plane, attitude, level);
        _out.WriteLine($"plane frame el {Mathf.RadToDeg(plane.Elevation):0.0} az {Mathf.RadToDeg(plane.Azimuth):0.0}; level frame el {Mathf.RadToDeg(el):0.0} az {Mathf.RadToDeg(az):0.0}");

        // In the plane's frame a banked turn's lead reads mostly up through the canopy. In the level
        // frame the same direction lies along the horizon, to the right.
        Assert.True(plane.Elevation > Mathf.Abs(plane.Azimuth), "the plane frame's lead is mostly up");
        Assert.True(Mathf.Abs(el) < Mathf.DegToRad(0.5f), $"level-frame elevation {Mathf.RadToDeg(el):0.0} deg");
        Assert.True(az < -Mathf.DegToRad(10f), $"level-frame azimuth {Mathf.RadToDeg(az):0.0} deg looks right");
    }

    [Fact]
    public void ReframeIntoTheSameFrameKeepsTheAim()
    {
        var frame = new Basis(Vector3.Up, 0.4f) * new Basis(Vector3.Forward, 1.1f);
        var (el, az) = HeadLook.Reframe((0.3f, -0.8f), frame, frame);
        Assert.Equal(0.3f, el, 4);
        Assert.Equal(-0.8f, az, 4);
    }
}
