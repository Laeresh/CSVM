using System;
using CSVM.Flight.Hangar;
using CSVM.UI.Hangar;
using Xunit;

namespace CSVM.Tests;

/// <summary>The hangar's live plane picture off engine: which pylons two counts open, where an edit
/// swings the camera, and the camera's hold-then-resume timing.</summary>
public class HangarPreviewTests
{
    private static readonly HangarPreviewPose Plain = new(
        7, 0, 0, 4, new PaintColour(1, 2, 3), new PaintColour(4, 5, 6), new PaintColour(7, 8, 9), 10, 11, 12);

    [Theory]
    [InlineData(0, 0, new int[0])]
    [InlineData(2, 3, new[] { 1, 3, 2, 4, 6 })]
    [InlineData(4, 4, new[] { 1, 3, 5, 7, 2, 4, 6, 8 })]
    [InlineData(9, 1, new[] { 1, 3, 5, 7, 2 })]
    public void OpenPylons_TakesEachWingsFirstPylonsInOrder(int left, int right, int[] expected) =>
        Assert.Equal(expected, CustomPlaneBuild.OpenPylons(left, right));

    [Fact]
    public void FocusFor_NamesTheEditedPart()
    {
        Assert.Equal(HangarPreviewFocus.None, HangarPreviewPose.FocusFor(null, Plain));
        Assert.Equal(HangarPreviewFocus.LeftWing, HangarPreviewPose.FocusFor(Plain, Plain with { LeftHardpoints = 2 }));
        Assert.Equal(HangarPreviewFocus.RightWing, HangarPreviewPose.FocusFor(Plain, Plain with { RightHardpoints = 1 }));
        Assert.Equal(HangarPreviewFocus.Nose, HangarPreviewPose.FocusFor(Plain, Plain with { NoseDecal = 3 }));
        Assert.Equal(HangarPreviewFocus.Tail, HangarPreviewPose.FocusFor(Plain, Plain with { TailDecal = 3 }));
        Assert.Equal(HangarPreviewFocus.Wing, HangarPreviewPose.FocusFor(Plain, Plain with { WingDecal = 3 }));
    }

    [Fact]
    public void FocusFor_PaintAndAirframeEditsMoveNothing()
    {
        Assert.Equal(HangarPreviewFocus.None, HangarPreviewPose.FocusFor(Plain, Plain with { Pattern = 1 }));
        Assert.Equal(HangarPreviewFocus.None, HangarPreviewPose.FocusFor(Plain, Plain with { Colour2 = new PaintColour(0, 0, 0) }));
        Assert.Equal(HangarPreviewFocus.None, HangarPreviewPose.FocusFor(Plain, Plain with { Airframe = 2, LeftHardpoints = 4 }));
    }

    [Fact]
    public void SamePaint_IgnoresAirframeAndStores()
    {
        Assert.True(Plain.SamePaint(Plain with { Airframe = 1, LeftHardpoints = 3, RightHardpoints = 2 }));
        Assert.False(Plain.SamePaint(Plain with { WingDecal = 0 }));
        Assert.False(Plain.SamePaint(Plain with { Colour3 = new PaintColour(9, 9, 9) }));
    }

    [Fact]
    public void Camera_TurnsOnItsOwn()
    {
        var camera = new HangarPreviewCamera();
        float before = camera.Yaw;
        camera.Advance(1f);
        Assert.True(camera.Turning);
        Assert.Equal(before + HangarPreviewCamera.TurnRate, camera.Yaw, 3);
    }

    [Fact]
    public void Camera_FocusSwingsHoldsThenResumes()
    {
        var camera = new HangarPreviewCamera();
        camera.Focus(HangarPreviewFocus.LeftWing, portSide: -1f);
        Step(camera, HangarPreviewCamera.FocusHold - 0.1f);
        var aim = HangarPreviewCamera.Aim(HangarPreviewFocus.LeftWing, 0f, -1f)!.Value;
        Assert.False(camera.Turning);
        Assert.Equal(aim.Yaw, camera.Yaw, 2);
        Assert.Equal(aim.Pitch, camera.Pitch, 2);
        Assert.True(aim.Pitch < 0f, "a wing is seen from below");

        Step(camera, 0.2f);
        Assert.True(camera.Turning);
        float held = camera.Yaw;
        camera.Advance(0.05f);
        float early = camera.Yaw - held;
        Assert.InRange(early, 0f, HangarPreviewCamera.TurnRate * 0.05f * 0.5f);
    }

    [Fact]
    public void Camera_WingSidesFollowThePortSign()
    {
        var left = HangarPreviewCamera.Aim(HangarPreviewFocus.LeftWing, 0f, portSide: 1f)!.Value;
        var right = HangarPreviewCamera.Aim(HangarPreviewFocus.RightWing, 0f, portSide: 1f)!.Value;
        Assert.True(left.Yaw > 0f && right.Yaw < 0f);
        Assert.Null(HangarPreviewCamera.Aim(HangarPreviewFocus.None, 0f, 1f));
    }

    [Fact]
    public void Camera_DragHoldsTheTurnUntilIdle()
    {
        var camera = new HangarPreviewCamera();
        camera.Orbit(0.5f, 10f);
        Assert.Equal(HangarPreviewCamera.MaxPitch, camera.Pitch);
        float yaw = camera.Yaw;
        Step(camera, HangarPreviewCamera.DragIdle - 0.1f);
        Assert.Equal(yaw, camera.Yaw);
        Step(camera, 0.2f);
        Assert.True(camera.Turning);
    }

    private static void Step(HangarPreviewCamera camera, float seconds)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += 0.01f)
        {
            camera.Advance(MathF.Min(0.01f, seconds - t));
        }
    }
}
