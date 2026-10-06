using CSVM.Flight.Hud;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine-free half of the stereo cockpit interior (<see cref="XrCockpit"/>). It covers where
/// the interior stands relative to the XR origin, and the arithmetic its shader places a vertex
/// with. The second is checked against Godot's own float32 composition at chapter scale, so it
/// shows the rounding the placement exists to avoid.
/// </summary>
[Trait("Tier", "Quick")]
public class XrCockpitTests
{
    // The Bloodhawk's cockpit_camera marker and the interior's mount: the head-pitch tilt at 0.04.
    private static readonly Vector3 Eye = new(0f, 0.75f, -0.2f);
    private static readonly Basis Mount = new Basis(Vector3.Right, -0.08203f).Scaled(Vector3.One * 0.04f);

    [Fact]
    public void AtRestTheInteriorSitsOnTheOriginWithItsMount()
    {
        var rel = XrCockpit.InOrigin(new Transform3D(Mount, Eye), Eye);
        Assert.Equal(Vector3.Zero, rel.Origin);
        Assert.True(rel.Basis.IsEqualApprox(Mount));
    }

    [Fact]
    public void TheWobbleTurnsTheInteriorAboutTheAirframeNotTheEye()
    {
        var shake = Basis.FromEuler(new Vector3(0.01f, -0.02f, 0.015f));
        var rel = XrCockpit.InOrigin(new Transform3D(shake, Vector3.Zero) * new Transform3D(Mount, Eye), Eye);
        Assert.True(rel.Origin.IsEqualApprox((shake * Eye) - Eye), $"origin {rel.Origin}");
        Assert.True(rel.Basis.IsEqualApprox(shake * Mount));
    }

    // A dial face 0.46 m ahead of an eye whose origin is 20 km out on a rotated heading. Godot's
    // own path rounds the eye and the combined camera at that magnitude. The interior's path
    // composes only small numbers, and recovers the eye-frame position to a few microns.
    [Fact]
    public void TheInteriorsPlacementSurvivesChapterScaleWhereTheWorldsDoesNot()
    {
        var heading = new Basis(Vector3.Up, 0.8389f);
        var origin = new Transform3D(heading, new Vector3(20000.37f, 300.21f, -13.77f));
        var head = new Transform3D(new Basis(Vector3.Up, 0.3f) * new Basis(Vector3.Right, -0.2f), new Vector3(0.02f, 0.01f, 0.05f));
        var eyeInHead = new Vector3(-0.0315f, 0f, 0f);
        var combinedInHead = new Vector3(0f, 0f, 0.0315f);
        var dialInOrigin = new Vector3(0.05f, -0.2f, -0.46f);

        // Exact: the dial seen from the left eye, all small numbers.
        var eyeInOrigin = head * new Transform3D(Basis.Identity, eyeInHead);
        var truth = eyeInOrigin.AffineInverse() * dialInOrigin;

        // Godot's frames at world scale, as the renderer builds them.
        var eyeWorld = origin * eyeInOrigin;
        var main = origin * head * new Transform3D(Basis.Identity, combinedInHead);
        var viewOffset = main.AffineInverse() * eyeWorld;

        // The world's way: the model's world transform through the combined view.
        var naive = viewOffset.AffineInverse() * (main.AffineInverse() * (origin * dialInOrigin));

        // The interior's way: the offset from the eye in world axes, through the combined rotation.
        var eyeOffsetWorld = origin.Basis * eyeInOrigin.Origin;
        var vertex = (main.Basis.Transposed() * ((origin.Basis * dialInOrigin) - eyeOffsetWorld)) + viewOffset.Origin;
        var precise = viewOffset.AffineInverse() * vertex;

        Assert.True(precise.DistanceTo(truth) < 2e-5f, $"precise error {precise.DistanceTo(truth)} m");
        Assert.True(naive.DistanceTo(truth) > 2e-4f, $"naive error {naive.DistanceTo(truth)} m, so the check can fail");
    }

    // An eye 7.6 km from the chapter origin, facing away from it. The lifted interior's box, through
    // the mount's scale, must still hold that eye, or the frustum test drops the whole interior.
    [Fact]
    public void TheLiftedInteriorsBoxReachesEveryEyeOnTheMapThroughTheMountScale()
    {
        var box = XrCockpit.Everywhere(Mount.Scale);
        var world = new Transform3D(new Basis(Vector3.Up, -2.3f) * Mount, Vector3.Zero) * box;
        var eye = new Vector3(-6350f, 300f, -4144f);
        Assert.True(world.HasPoint(eye), $"world box {world.Position} size {world.Size}");
        Assert.True(world.HasPoint(new Vector3(-16384f, 3000f, -16384f)), "the far corner of C3 and C5");
    }
}
