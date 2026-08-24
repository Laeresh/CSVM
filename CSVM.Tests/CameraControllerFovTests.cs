using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The decoded horizontal→vertical FOV conversion law (docs/PLAN-cockpit-view.md, A3;
/// docs/org/cameraViews.md, "FOV constants and aspect correction"):
/// <c>vertical = atan(tan(H/2) · assumedAspect/liveAspect)</c>, doubled for the full angle
/// Godot's <see cref="Godot.Camera3D.Fov"/> expects. <see cref="CameraController.HorizontalToVerticalFovDeg"/>
/// is engine-free, so these tests need no live <see cref="Godot.Camera3D"/> or
/// <see cref="Godot.Viewport"/>.
/// </summary>
public class CameraControllerFovTests
{
    private const float Aspect16By9 = 16f / 9f;

    [Fact]
    public void CockpitsSixtyHorizontalPortsToFortySixPointEightVerticalAt16By9()
    {
        float vertical = CameraController.HorizontalToVerticalFovDeg(60f, Aspect16By9);
        Assert.InRange(vertical, 46.7f, 46.9f);
    }

    [Fact]
    public void CockpitsEightyHorizontalPortsToSixtyFourPointFourVerticalAt16By9()
    {
        float vertical = CameraController.HorizontalToVerticalFovDeg(80f, Aspect16By9);
        Assert.InRange(vertical, 64.3f, 64.5f);
    }

    [Fact]
    public void ASecondAspectRatioMovesTheDerivedVerticalAwayFromThe16By9Reading()
    {
        // 4:3 is the engine's own assumed reference aspect, so at 4:3 the formula's aspect
        // factor collapses to 1 and the derived vertical equals the stored horizontal angle
        // exactly — a second, independently checkable point on the curve.
        float verticalAt4By3 = CameraController.HorizontalToVerticalFovDeg(60f, 4f / 3f);
        Assert.InRange(verticalAt4By3, 59.9f, 60.1f);

        float verticalAt16By9 = CameraController.HorizontalToVerticalFovDeg(60f, Aspect16By9);
        Assert.True(verticalAt16By9 < verticalAt4By3);
    }

    [Fact]
    public void ANarrowerLiveAspectThan16By9WidensTheDerivedVerticalFov()
    {
        // A 4:3 splitscreen pane is narrower than 16:9 — the same horizontal picture needs a
        // taller vertical angle to keep it, so the derived vertical must grow as liveAspect
        // shrinks toward the engine's own 4:3 reference.
        float verticalAt16By9 = CameraController.HorizontalToVerticalFovDeg(80f, Aspect16By9);
        float verticalAt4By3 = CameraController.HorizontalToVerticalFovDeg(80f, 4f / 3f);
        Assert.True(verticalAt4By3 > verticalAt16By9);
    }
}
