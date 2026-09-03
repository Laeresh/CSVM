using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The numpad +/- zoom axis (BL-433, PLAN-cockpit-view "What the data actually
/// ships"): the target moves at 2/s on the two keys, clamped [0, 1], and the shown trim chases
/// it at 1.5/s through <see cref="HeadLook.Approach"/>. <see cref="CameraController.ZoomTarget"/>
/// is engine-free, so none of this needs a live camera.
/// </summary>
public class CameraControllerZoomTests
{
    private const float Tol = 1e-4f;
    private const float SmoothRate = 1.5f; // the decoded shown-trim catch-up rate

    [Fact]
    public void HoldingZoomInMovesTheTargetTowardOneAtTwoPerSecond()
    {
        float target = CameraController.ZoomTarget(0f, zoomIn: true, zoomOut: false, dt: 0.1f);
        Assert.Equal(0.2f, target, Tol);
    }

    [Fact]
    public void HoldingZoomOutMovesTheTargetTowardZeroAtTwoPerSecond()
    {
        float target = CameraController.ZoomTarget(0.5f, zoomIn: false, zoomOut: true, dt: 0.1f);
        Assert.Equal(0.3f, target, Tol);
    }

    [Fact]
    public void NeitherKeyLeavesTheTargetWhereItWas()
    {
        float target = CameraController.ZoomTarget(0.4f, zoomIn: false, zoomOut: false, dt: 0.25f);
        Assert.Equal(0.4f, target, Tol);
    }

    [Fact]
    public void HoldingBothKeysCancelsLikeAPlainAxis()
    {
        float target = CameraController.ZoomTarget(0.4f, zoomIn: true, zoomOut: true, dt: 0.25f);
        Assert.Equal(0.4f, target, Tol);
    }

    [Fact]
    public void TheTargetClampsAtOneRatherThanOvershooting()
    {
        // A full second at 2/s would reach 2 unclamped; the decoded axis stops at 1.
        float target = CameraController.ZoomTarget(0f, zoomIn: true, zoomOut: false, dt: 1f);
        Assert.Equal(1f, target, Tol);
    }

    [Fact]
    public void TheTargetClampsAtZeroRatherThanGoingNegative()
    {
        float target = CameraController.ZoomTarget(1f, zoomIn: false, zoomOut: true, dt: 1f);
        Assert.Equal(0f, target, Tol);
    }

    [Fact]
    public void TheShownTrimChasesTheClampedTargetAtOnePointFivePerSecond()
    {
        // Isolate the smoothing law from the target axis's own ramp: a huge dt saturates
        // ZoomTarget at its clamp in one call, so the target is already settled at 1.
        float target = CameraController.ZoomTarget(0f, zoomIn: true, zoomOut: false, dt: 10f);
        Assert.Equal(1f, target, Tol);

        // HeadLook.Approach is exact for any dt (it is the closed-form exponential, not an
        // Euler step), so one second-long call reads the same as sixty 1/60 s frames would.
        // 1 - e^(-1.5) ≈ 0.7769.
        float shown = HeadLook.Approach(0f, target, SmoothRate, 1f);
        Assert.InRange(shown, 0.7755f, 0.7785f);
    }

    [Fact]
    public void TheShownTrimNeverOvershootsAFrozenTarget()
    {
        float shown = 0f;
        for (int i = 0; i < 5; i++)
        {
            float next = HeadLook.Approach(shown, 1f, SmoothRate, 0.1f);
            Assert.True(next > shown && next <= 1f); // strictly closer, never past the target
            shown = next;
        }
    }
}
