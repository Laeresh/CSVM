using CSVM.Flight.Hud;
using CSVM.Mech3;
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

    // The Kestrel's marker, off the centreline on the pilot's boom. The stereo mount stands the
    // interior's point at the marker's numbers on the marker, untilted, and a rescale keeps it there.
    [Fact]
    public void TheStereoMountSeatsTheAuthoredEyeOnTheMarkerAndARescaleKeepsIt()
    {
        var marker = new Vector3(1.9785f, 0.7479f, -1.21f);
        var mount = PlaneBuilder.StereoMount(marker, 0.04f);
        Assert.True((mount * marker).IsEqualApprox(marker), $"the authored eye lands at {mount * marker}");
        Assert.True(mount.Basis.IsEqualApprox(Basis.Identity.Scaled(Vector3.One * 0.04f)));

        var rescaled = XrCockpit.Rescaled(mount, marker, 0.05f);
        Assert.True((rescaled * marker).IsEqualApprox(marker), $"rescaled, the eye moves to {rescaled * marker}");
        Assert.Equal(0.05f, rescaled.Basis.Scale.X, 6);
        Assert.True(XrCockpit.Rescaled(new Transform3D(Mount, Eye), Vector3.Zero, 0.05f).Origin.IsEqualApprox(Eye),
            "an interior whose eye is its origin rescales about its origin");
    }

    // A pylon store's stereo copy (Flight/Weapons/PylonOrdnance.cs): mounted as built it lands on the
    // world pylon; lifted, it rides the interior. The launch point is the pylon, which nothing moves.
    [Fact]
    public void AStoresCopyLandsOnItsPylonAndThenRidesTheInterior()
    {
        var mount = new Transform3D(Mount, Eye);
        var pylon = new Transform3D(new Basis(Vector3.Up, 0.05f), new Vector3(-2.31f, -0.62f, 0.4f));
        var copy = XrCockpit.InInterior(mount, pylon);
        var landed = mount * copy;
        Assert.True(landed.Origin.IsEqualApprox(pylon.Origin) && landed.Basis.IsEqualApprox(pylon.Basis), $"lands at {landed}");

        var attitude = Basis.FromEuler(new Vector3(0.2f, -1.1f, 0.05f));
        var lifted = new Transform3D(attitude, Vector3.Zero) * XrCockpit.InOrigin(mount, Eye);
        var rides = lifted * copy;
        var wing = lifted * XrCockpit.InInterior(mount, new Transform3D(Basis.Identity, pylon.Origin + new Vector3(0f, 0.1f, 0f)));
        Assert.True((wing.Origin - rides.Origin).IsEqualApprox(attitude * new Vector3(0f, 0.1f, 0f)),
            "the copy keeps its offset from the interior's wing above it");
    }

    // The per-plane store placement. At the defaults it is the true pylon pose, bit for bit. Else
    // the lateral distance is spread, then offset, and the store's own attitude is kept.
    [Fact]
    public void AStoreIsPlacedBySpreadingItsLateralDistanceThenOffsetting()
    {
        var pylon = new Transform3D(new Basis(Vector3.Up, 0.05f), new Vector3(-2.31f, -0.62f, 0.4f));

        Assert.Equal(pylon, XrCockpit.Placed(pylon, Vector3.Zero, 1f));

        var placed = XrCockpit.Placed(pylon, new Vector3(0f, 0.1f, -0.2f), 1.5f);
        Assert.True(placed.Origin.IsEqualApprox(new Vector3(-3.465f, -0.52f, 0.2f)), $"placed at {placed.Origin}");
        Assert.Equal(pylon.Basis, placed.Basis);
        var mirror = XrCockpit.Placed(new Transform3D(pylon.Basis, pylon.Origin * new Vector3(-1f, 1f, 1f)), new Vector3(0f, 0.1f, -0.2f), 1.5f);
        Assert.Equal(-placed.Origin.X, mirror.Origin.X);
        Assert.Equal(placed.Origin.Y, mirror.Origin.Y);
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
