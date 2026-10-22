using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The flat interior mount and a body cockpit's pose arithmetic. An interior-frame transform is carried
/// into the plane frame (<see cref="PlaneBuilder.InteriorToPlane"/>) and back. A rig pose is laid over
/// it (<see cref="CockpitTransplant.Posed"/>), each absent field keeping the original's apparent value.
/// Inside <c>cockpit1</c> the same pose is read in interior units (<see cref="CockpitTransplant.PosedUnder"/>).
/// </summary>
public class CockpitTransplantTests
{
    private const float Tolerance = 1e-5f;

    // The Bloodhawk's marker and its speedometer as the data places it inside cockpit1.
    private static readonly Vector3 Marker = new(0f, 0.75f, -0.9f);
    private static readonly Transform3D Speedometer =
        new(Basis.FromEuler(new Vector3(-0.352f, 0f, 0f), EulerOrder.Yxz), new Vector3(5.02f, -4.11f, -10.59f));

    [Fact]
    public void TheFlatMountIsTheMarkerTheTiltAndTheInteriorScale()
    {
        var mount = PlaneBuilder.FlatMount(Marker);
        Assert.True(mount.Origin.IsEqualApprox(Marker));
        Assert.True(mount.Basis.IsEqualApprox(new Basis(Vector3.Right, PlaneBuilder.HeadPitchOffsetRad).Scaled(Vector3.One * PlaneBuilder.InteriorScale)));
    }

    [Fact]
    public void AnInteriorPoseLandsWhereTheFlatViewShowsIt()
    {
        var plane = PlaneBuilder.InteriorToPlane(Speedometer, Marker);
        var expected = Marker + (new Basis(Vector3.Right, PlaneBuilder.HeadPitchOffsetRad) * (Speedometer.Origin * PlaneBuilder.InteriorScale));
        Assert.True(plane.Origin.DistanceTo(expected) < Tolerance, $"{plane.Origin} vs {expected}");
        Assert.True(Mathf.Abs(plane.Basis.Scale.X - PlaneBuilder.InteriorScale) < Tolerance);
        // The dial's own -20 degree tilt and the head-pitch tilt add about the same axis.
        float pitch = plane.Basis.Orthonormalized().GetEuler(EulerOrder.Yxz).X;
        Assert.True(Mathf.Abs(pitch - (-0.352f + PlaneBuilder.HeadPitchOffsetRad)) < 1e-4f, $"pitch {pitch}");
    }

    [Fact]
    public void ThePlaneToInteriorReadIsTheInverse()
    {
        var plane = PlaneBuilder.InteriorToPlane(Speedometer, Marker);
        Assert.True(PlaneBuilder.InteriorFromPlane(plane, Marker).IsEqualApprox(Speedometer));
    }

    [Fact]
    public void APositionOnlyPoseKeepsTheOriginalSizeAndFacing()
    {
        var original = PlaneBuilder.InteriorToPlane(Speedometer, Marker);
        var moved = new Vector3(0.3f, 0.5f, -0.8f);
        var posed = CockpitTransplant.Posed(original, new RigGaugePose(moved, null, null));
        Assert.Equal(moved, posed.Origin);
        Assert.True(posed.Basis.IsEqualApprox(original.Basis));
    }

    [Fact]
    public void ARotationReplacesTheFacingAndKeepsTheSize()
    {
        var original = PlaneBuilder.InteriorToPlane(Speedometer, Marker);
        var degrees = new Vector3(-30f, 15f, 5f);
        var posed = CockpitTransplant.Posed(original, new RigGaugePose(Vector3.Zero, degrees, null));
        var rotation = Basis.FromEuler(degrees * (Mathf.Pi / 180f), EulerOrder.Yxz);
        Assert.True(posed.Basis.IsEqualApprox(rotation.Scaled(Vector3.One * PlaneBuilder.InteriorScale)));
        // YXZ is Node3D.RotationDegrees' order: basis Ry * Rx * Rz.
        var composed = new Basis(Vector3.Up, Mathf.DegToRad(15f)) * new Basis(Vector3.Right, Mathf.DegToRad(-30f)) * new Basis(Vector3.Back, Mathf.DegToRad(5f));
        Assert.True(rotation.IsEqualApprox(composed));
    }

    [Fact]
    public void AScaleReplacesTheSizeAndKeepsTheFacing()
    {
        var original = PlaneBuilder.InteriorToPlane(Speedometer, Marker);
        var posed = CockpitTransplant.Posed(original, new RigGaugePose(Vector3.Zero, null, 0.1f));
        Assert.True(posed.Basis.Scale.IsEqualApprox(Vector3.One * 0.1f));
        Assert.True(posed.Basis.Orthonormalized().IsEqualApprox(original.Basis.Orthonormalized()));
    }

    // Inside cockpit1 a pose is in interior units. The assembly lands there under a parent that is
    // itself moved, turned and scaled, and an absent field keeps the assembly's interior-frame value.
    [Fact]
    public void AnInteriorPoseLandsInCockpit1sFrameUnderAMovedParent()
    {
        var parent = new Transform3D(Basis.FromEuler(new Vector3(0.2f, -0.4f, 0.1f), EulerOrder.Yxz).Scaled(Vector3.One * 1.5f), new Vector3(2f, -1f, 3f));
        var original = parent * new Transform3D(Basis.FromEuler(new Vector3(-0.352f, 0f, 0f), EulerOrder.Yxz), new Vector3(1f, 2f, -4f));
        var at = new Vector3(6f, -3f, -12f);

        var moved = parent * CockpitTransplant.PosedUnder(parent, original, new RigGaugePose(at, null, null));
        Assert.True(moved.Origin.DistanceTo(at) < 1e-4f, $"{moved.Origin} vs {at}");
        Assert.True(moved.Basis.IsEqualApprox(original.Basis), "position only keeps the facing and size in cockpit1");

        var turned = parent * CockpitTransplant.PosedUnder(parent, original, new RigGaugePose(at, new Vector3(0f, 90f, 0f), 2f));
        var want = Basis.FromEuler(new Vector3(0f, Mathf.Pi / 2f, 0f), EulerOrder.Yxz).Scaled(Vector3.One * 2f);
        Assert.True(turned.Basis.IsEqualApprox(want), $"{turned.Basis} vs {want}");
    }

    [Fact]
    public void TheGlassFrameIsTheCentreOfItsCornersUnrotated()
    {
        var corners = new List<Vector3> { new(-1f, 1f, -10f), new(1f, 1f, -10f), new(1f, 3f, -10f), new(-1f, 3f, -10f) };
        var uvs = new List<Vector2> { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down };
        var frame = CockpitTransplant.GlassFrame(new[] { new GunsightReticle.Glass(corners, uvs, 3, "cockpit6") });
        Assert.NotNull(frame);
        Assert.True(frame!.Value.Origin.IsEqualApprox(new Vector3(0f, 2f, -10f)));
        Assert.Equal(Basis.Identity, frame.Value.Basis);
        Assert.Null(CockpitTransplant.GlassFrame(new List<GunsightReticle.Glass>()));
    }
}
