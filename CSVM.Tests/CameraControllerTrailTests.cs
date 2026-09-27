using CSVM.Flight.Camera;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The enhanced presentation's two chase cues, both pure laws on
/// <see cref="CameraController"/> and neither present in the decoded camera
/// (docs/org/cameraViews.md): the lagged attitude the chase pose is built from, which eases toward
/// the live one on the exponential shape <c>dist_catch_up</c> uses, and the widening of the
/// external FOV with the speed fraction of the airframe's rated max. What these pin is the shape
/// and the ends of the ramp; the rate and the degrees are TUNE.
/// </summary>
public class CameraControllerTrailTests
{
    private const float Rate = 1f;      // 1/s, so one second of easing leaves exactly 1/e
    private const float LagDeg = 90f;

    /// <summary>One time constant of easing leaves 1/e of the angle the camera was behind by,
    /// which is what makes the rate a time constant rather than a per-frame fraction. The discrete
    /// slerp leaves a couple of percent more than the continuous law, so the reading is taken to
    /// 5% rather than to float precision.</summary>
    [Fact]
    public void ThePlainLagRelaxesToOneOverEInOneTimeConstant()
    {
        float left = RelaxFor(1f, 1f / 60f);
        Assert.Equal(LagDeg * 0.36788f, left, 0.05f * LagDeg);
    }

    /// <summary>The rate is per real second, so the same second of easing leaves the same angle at
    /// 60 and at 240 frames a second, within the degree the discrete step's own bias moves it. A
    /// per-step fraction would leave 1.6° against 33°, which is the failure this rules out.</summary>
    [Fact]
    public void TheRelaxationDoesNotDependOnTheFrameRate()
    {
        Assert.Equal(RelaxFor(1f, 1f / 60f), RelaxFor(1f, 1f / 240f), 1f);
    }

    /// <summary>A frame that advances no time leaves the lag exactly where it was, so a halted
    /// session cannot walk the camera off the pose it is holding.</summary>
    [Fact]
    public void AZeroStepLeavesTheLagWhereItWas()
    {
        Assert.Equal(LagDeg, RelaxFor(0f, 0f), 1e-3f);
    }

    /// <summary>Cruise carries no widening at all: at and below the cruise fraction the external
    /// FOV is the decoded angle the camera was built with, so ordinary flight reads as it does on
    /// the faithful path.</summary>
    [Fact]
    public void NothingWidensAtOrBelowCruise()
    {
        Assert.Equal(0f, CameraController.SpeedFovWiden(0f));
        Assert.Equal(0f, CameraController.SpeedFovWiden(0.3f));
        Assert.Equal(0f, CameraController.SpeedFovWiden(0.6f));
    }

    /// <summary>The shipped TUNE: six degrees of widening at the airframe's rated max, reached
    /// linearly from the cruise fraction, so the midpoint of the ramp carries half of it.</summary>
    [Fact]
    public void TheWideningReachesSixDegreesAtRatedMax()
    {
        Assert.Equal(6f, CameraController.SpeedFovWiden(1f), 1e-4f);
        Assert.Equal(3f, CameraController.SpeedFovWiden(0.8f), 1e-4f);
    }

    /// <summary>A dive past the rating holds the widening where rated max left it. The speed law
    /// admits well over rated max, and an unclamped ramp would keep opening the view through
    /// exactly the manoeuvre that most needs a stable frame.</summary>
    [Fact]
    public void ADivePastRatedMaxDoesNotKeepOpeningTheView()
    {
        float atMax = CameraController.SpeedFovWiden(1f);
        Assert.Equal(atMax, CameraController.SpeedFovWiden(1.4f), 1e-4f);
        Assert.Equal(atMax, CameraController.SpeedFovWiden(3f), 1e-4f);
    }

    // Ease a lag that starts LagDeg of roll behind a held attitude for the given seconds at the
    // given step, and hand back the degrees it is still behind by.
    private static float RelaxFor(float seconds, float dt)
    {
        var live = Basis.Identity;
        var lagged = new Basis(Vector3.Forward, Mathf.DegToRad(LagDeg));
        int steps = dt > 0f ? (int)(seconds / dt) : 1;
        for (int i = 0; i < steps; i++)
        {
            lagged = CameraController.TrailAttitude(lagged, live, Rate, dt);
        }
        return Mathf.RadToDeg((lagged * Vector3.Up).AngleTo(live * Vector3.Up));
    }
}
