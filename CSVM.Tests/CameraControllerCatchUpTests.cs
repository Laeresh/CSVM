using CSVM.Flight.Camera;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The chase camera's catch-up: the eased aircraft frames the authored rig turns by
/// (<see cref="CameraController.EaseFrame"/>) and the swing factor on both rates
/// (<see cref="CameraController.CatchUpScale"/>). Pinned against the figures the decode works out
/// on the shipped blocks (docs/org/cameraViews.md, "The chase rig"). Pure, so none of it needs a
/// live camera.
/// </summary>
public class CameraControllerCatchUpTests
{
    private static readonly CamParams Default = new();
    private static readonly CamParams Balmoral = new() { ThirdpHeight = 0.2f, ThirdpPitch = 0.2f };

    // The settled default rig sits 0.1349 per metre off the flight path, so both rates run 1.2667
    // times their authored figure. Balmoral's higher rig runs 1.3943 times.
    [Fact]
    public void ASettledHeadScalesTheRatesByTheRigsHeightOffTheFlightPath()
    {
        Assert.Equal(1.26674f, Scale(Default, 0f, 0f), 1e-4f);
        Assert.Equal(1.39430f, Scale(Balmoral, 0f, 0f), 1e-4f);
    }

    // A flank view or one straight up sits a whole radius off the flight path, easing about three
    // times as fast. Dead ahead sits on it and takes the authored rate almost bare.
    [Fact]
    public void TheFactorGrowsAsTheViewSwingsOffTheFlightPath()
    {
        Assert.Equal(3.01132f, Scale(Default, 0f, Mathf.Pi / 2f), 1e-4f);
        Assert.Equal(3.00722f, Scale(Default, Mathf.Pi / 2f, 0f), 1e-4f);
        Assert.Equal(1.01016f, Scale(Default, 0f, Mathf.Pi), 1e-4f);
    }

    // The original's quaternion ease turns rate·dt of the remaining rotation, not 1 − e^(−rate·dt).
    // So 3/s over 0.1 s takes 0.3 of a 1 rad roll, where the exponential would take 0.259.
    [Fact]
    public void TheFrameTurnsALinearFractionOfTheGap()
    {
        var live = new Quaternion(Vector3.Forward, 1f);
        var eased = CameraController.EaseFrame(Quaternion.Identity, live, 3f, 0.1f);
        Assert.Equal(0.3f, eased.GetAngle(), 1e-5f);
    }

    // Once rate·dt reaches 1 the frame lands on the attitude outright, and a zero step leaves it
    // exactly where it was.
    [Fact]
    public void AFullStepLandsAndAZeroStepHolds()
    {
        var frame = new Quaternion(Vector3.Up, 0.2f);
        var live = new Quaternion(Vector3.Right, 0.9f);
        Assert.Equal(live, CameraController.EaseFrame(frame, live, 2f, 0.5f));
        Assert.Equal(frame, CameraController.EaseFrame(frame, live, 2f, 0f));
    }

    // A frame 10 degrees behind across the 360 seam follows the short way round.
    [Fact]
    public void TheFrameTakesTheShorterArc()
    {
        var live = new Quaternion(Vector3.Forward, Mathf.DegToRad(350f));
        var eased = CameraController.EaseFrame(Quaternion.Identity, live, 5f, 0.1f);
        var turned = eased * Vector3.Right;
        Assert.Equal(-5f, Mathf.RadToDeg(Mathf.Atan2(-turned.Y, turned.X)), 1e-3f);
    }

    private static float Scale(CamParams cam, float elevation, float azimuth) =>
        CameraController.CatchUpScale(
            CameraController.AuthoredRig(elevation, azimuth, cam.ThirdpHeight, cam.ThirdpPitchRad).Offset);
}
