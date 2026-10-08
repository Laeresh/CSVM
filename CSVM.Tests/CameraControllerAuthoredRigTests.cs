using CSVM.Flight.Camera;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The authored chase rig, <see cref="CameraController.AuthoredRig"/>: the original's chase
/// direction and aim off camparam's <c>thirdp_height</c> and <c>thirdp_pitch</c>, swung by the
/// head. Pinned against the figures the decode works out on the shipped blocks
/// (docs/org/cameraViews.md, "The chase rig"). Pure, so none of it needs a live camera.
/// </summary>
public class CameraControllerAuthoredRigTests
{
    private const float DegTol = 0.01f;

    // The shipped default block and Balmoral's override, the only two rig shapes in the install.
    private static readonly CamParams Default = new();
    private static readonly CamParams Balmoral = new() { ThirdpHeight = 0.2f, ThirdpPitch = 0.2f };

    // With the head settled the camera sits atan(height) above the tail, less the pitch. That is
    // 7.57° on the default block and 11.11° on Balmoral's, never the bare 0.29° pitch.
    [Fact]
    public void ASettledHeadSitsAtTheAuthoredElevationAboveTheTail()
    {
        Assert.Equal(7.567f, ElevationDeg(Rig(Default, 0f, 0f).Offset), DegTol);
        Assert.Equal(11.110f, ElevationDeg(Rig(Balmoral, 0f, 0f).Offset), DegTol);
        Assert.Equal(0f, Rig(Default, 0f, 0f).Offset.X, 1e-6f);
    }

    // The aim looks along the swung nose, so the aircraft sits atan(height) below the image
    // centre whatever the pitch. The pitch tilts the view up by itself.
    [Fact]
    public void TheAimLooksAlongTheTiltedNose()
    {
        var (offset, aim) = Rig(Default, 0f, 0f);
        var forward = aim * Vector3.Forward;
        Assert.Equal(0.29f, ElevationDeg(forward), DegTol);
        float belowCentre = Mathf.RadToDeg(forward.AngleTo(-offset));
        Assert.Equal(Mathf.RadToDeg(Mathf.Atan(Default.ThirdpHeight)), belowCentre, DegTol);
    }

    // The offset carries the decoded 1.0145 scale and the rise, so the settled camera sits about
    // 2.4% beyond the distance it is handed.
    [Fact]
    public void TheSettledOffsetIsSlightlyLongerThanTheRadius()
    {
        float expected = 1.0145f * Mathf.Sqrt(1f + (0.138f * 0.138f));
        Assert.Equal(expected, Rig(Default, 0f, 0f).Offset.Length(), 1e-3f);
    }

    // The rise fades with the swing's squared quaternion scalar: half on a flank, none nose-on.
    // Those are where the three level numpad keys put the camera.
    [Fact]
    public void TheLevelKeysLoseTheRiseAsTheHeadSwingsRound()
    {
        var flank = Rig(Default, 0f, Mathf.Pi / 2f).Offset;
        Assert.True(flank.X > 0.9f, $"a positive azimuth carries the camera to starboard, at {flank}");
        Assert.Equal(3.66f, ElevationDeg(flank), 0.02f);

        var noseOn = Rig(Default, 0f, Mathf.Pi).Offset;
        Assert.True(noseOn.Z < -0.9f, $"nose-on is ahead of the aircraft, at {noseOn}");
        Assert.Equal(-0.29f, ElevationDeg(noseOn), DegTol);
    }

    // The swing is the original's direction builder FUN_0053f550 term for term. The rig turns by
    // the quaternion the decode names, not by a lookalike.
    [Fact]
    public void TheSwingIsTheDecodedQuaternion()
    {
        const float e = 0.4f, a = 0.7f;
        var decoded = new Quaternion(
            Mathf.Sin(e / 2f) * Mathf.Cos(a / 2f),
            Mathf.Cos(e / 2f) * Mathf.Sin(a / 2f),
            -Mathf.Sin(e / 2f) * Mathf.Sin(a / 2f),
            Mathf.Cos(e / 2f) * Mathf.Cos(a / 2f));
        var (_, aim) = CameraController.AuthoredRig(e, a, 0f, 0f);
        Assert.True(aim.GetRotationQuaternion().IsEqualApprox(decoded), $"{aim.GetRotationQuaternion()} against {decoded}");
    }

    private static (Vector3 Offset, Basis Aim) Rig(CamParams cam, float elevation, float azimuth) =>
        CameraController.AuthoredRig(elevation, azimuth, cam.ThirdpHeight, cam.ThirdpPitchRad);

    // Height above the plane's horizontal, in degrees, of a plane-frame direction.
    private static float ElevationDeg(Vector3 v) =>
        Mathf.RadToDeg(Mathf.Atan2(v.Y, Mathf.Sqrt((v.X * v.X) + (v.Z * v.Z))));
}
