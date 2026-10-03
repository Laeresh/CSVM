using CSVM.Flight.Camera;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The enhanced presentation's one chase cue, the external FOV's widening with the speed fraction
/// of the airframe's rated max. It is a pure law on <see cref="CameraController"/>. The decoded
/// camera has no counterpart for it (docs/org/cameraViews.md). These pin the shape and the ends of
/// the ramp; the degrees are TUNE.
/// </summary>
public class CameraControllerSpeedFovTests
{
    /// <summary>Cruise carries no widening at all. At and below the cruise fraction the external
    /// FOV is the decoded angle the camera was built with. Ordinary flight therefore reads as it
    /// does on the faithful path.</summary>
    [Fact]
    public void NothingWidensAtOrBelowCruise()
    {
        Assert.Equal(0f, CameraController.SpeedFovWiden(0f));
        Assert.Equal(0f, CameraController.SpeedFovWiden(0.3f));
        Assert.Equal(0f, CameraController.SpeedFovWiden(0.6f));
    }

    /// <summary>The shipped TUNE: six degrees of widening at the airframe's rated max. It is
    /// reached linearly from the cruise fraction, so the midpoint of the ramp carries half of
    /// it.</summary>
    [Fact]
    public void TheWideningReachesSixDegreesAtRatedMax()
    {
        Assert.Equal(6f, CameraController.SpeedFovWiden(1f), 1e-4f);
        Assert.Equal(3f, CameraController.SpeedFovWiden(0.8f), 1e-4f);
    }

    /// <summary>A dive past the rating holds the widening where rated max left it. The speed law
    /// admits well over rated max. An unclamped ramp would keep opening the view through the
    /// manoeuvre that most needs a stable frame.</summary>
    [Fact]
    public void ADivePastRatedMaxDoesNotKeepOpeningTheView()
    {
        float atMax = CameraController.SpeedFovWiden(1f);
        Assert.Equal(atMax, CameraController.SpeedFovWiden(1.4f), 1e-4f);
        Assert.Equal(atMax, CameraController.SpeedFovWiden(3f), 1e-4f);
    }
}
