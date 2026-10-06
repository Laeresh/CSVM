using System;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine-free half of the cockpit-locked HUD panel (<see cref="XrHudPanelLayout"/>). It covers
/// the panel's pose in the XR origin's frame and its size. It also covers the flat first-person pane
/// it reproduces, and the eye-feed placement's arithmetic at chapter scale.
/// </summary>
[Trait("Tier", "Quick")]
public class XrHudPanelLayoutTests
{
    private static readonly XrHudPanelLayout Panel = XrHudPanelLayout.Default;

    [Fact]
    public void TheSurfaceIsTheHudsReferenceFrameSoItLaysOutAtScaleOne()
    {
        Assert.Equal(HudMetrics.ReferenceHeight, XrHudPanelLayout.SurfaceSize.Y);
        Assert.Equal(HudMetrics.ReferenceAspect, (float)XrHudPanelLayout.SurfaceSize.X / XrHudPanelLayout.SurfaceSize.Y, 3);
    }

    [Fact]
    public void ThePanelStandsOnTheOriginalsFirstPersonAxisFacingTheEye()
    {
        var centre = Panel.Pose.Origin;
        Assert.Equal(Panel.DistanceM, centre.Length(), 5);
        float under = Mathf.RadToDeg(MathF.Asin(-centre.Y / centre.Length()));
        Assert.Equal(-Mathf.RadToDeg(PlaneBuilder.HeadPitchOffsetRad), under, 3);
        // Its +Z, the quad's face, points back at the eye.
        Assert.Equal(1f, Panel.Pose.Basis.Z.Dot(-centre.Normalized()), 5);
    }

    // Every point of the 2560x1440 frame lands where the flat Nose view draws that pixel. That view
    // is a camera on the seat, pitched by the original's head tilt, 46.83 degrees tall, at 16:9.
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(0.1667f, 0.75f)]
    [InlineData(0.5f, 0.0375f)]
    [InlineData(0.83f, 0.6f)]
    public void EverySurfacePointLandsWhereTheFlatFirstPersonViewDrawsIt(float u, float v)
    {
        var camera = new Basis(Vector3.Right, PlaneBuilder.HeadPitchOffsetRad);
        var dir = camera.Transposed() * Panel.PointAt(new Vector2(u, v));
        float tanHalf = MathF.Tan(Mathf.DegToRad(CameraController.ExternalFovDeg) * 0.5f);
        float x = dir.X / -dir.Z / tanHalf * 9f / 16f;
        float y = dir.Y / -dir.Z / tanHalf;
        Assert.Equal((2f * u) - 1f, x, 4);
        Assert.Equal(1f - (2f * v), y, 4);
    }

    [Fact]
    public void ThePanelSpansTheFlatExternalViewsAngleAtThirtyRowsPerDegree()
    {
        var top = Panel.PointAt(new Vector2(0.5f, 0f)).Normalized();
        var bottom = Panel.PointAt(new Vector2(0.5f, 1f)).Normalized();
        Assert.Equal(CameraController.ExternalFovDeg, Mathf.RadToDeg(MathF.Acos(top.Dot(bottom))), 3);
        Assert.InRange(Panel.RowsPerDegree, 30f, 31f);
        Assert.Equal(16f / 9f, Panel.SizeM.X / Panel.SizeM.Y, 4);
    }

    // Under the eye feed the quad's transform carries only the panel's offset from the origin, so
    // nothing Godot composes for it reaches chapter magnitude. Without the feed it is the world pose.
    [Fact]
    public void TheEyeFeedPlacementCarriesNoWorldTranslation()
    {
        var origin = new Transform3D(Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f)), new Vector3(20000.37f, 300.21f, -13.77f));
        var fed = Panel.Placement(origin, eyeFeed: true);
        Assert.True(fed.Origin.Length() <= Panel.DistanceM + 1e-4f, $"fed origin {fed.Origin}");
        Assert.True(fed.Basis.IsEqualApprox(origin.Basis * Panel.Pose.Basis));
        var plain = Panel.Placement(origin, eyeFeed: false);
        Assert.True(plain.Origin.IsEqualApprox(origin * Panel.Pose.Origin), $"plain origin {plain.Origin}");
    }

    // A panel corner seen from the left eye of a turned head whose origin is 20 km out. Godot's
    // own path rounds the eye and the combined camera at that magnitude. The panel's eye-feed path
    // composes small numbers only, and recovers the eye-frame position to a few microns.
    [Fact]
    public void ThePanelsPlacementSurvivesChapterScaleWhereTheWorldsDoesNot()
    {
        var origin = new Transform3D(new Basis(Vector3.Up, 0.8389f) * new Basis(Vector3.Forward, 0.3f), new Vector3(20000.37f, 300.21f, -13.77f));
        var head = new Transform3D(new Basis(Vector3.Up, 0.5f) * new Basis(Vector3.Right, -0.2f), new Vector3(0.02f, 0.01f, 0.05f));
        var eyeInHead = new Vector3(-0.0315f, 0f, 0f);
        var combinedInHead = new Vector3(0f, 0f, 0.0315f);
        var cornerInOrigin = Panel.PointAt(new Vector2(0.1f, 0.85f));

        var eyeInOrigin = head * new Transform3D(Basis.Identity, eyeInHead);
        var truth = eyeInOrigin.AffineInverse() * cornerInOrigin;

        var eyeWorld = origin * eyeInOrigin;
        var main = origin * head * new Transform3D(Basis.Identity, combinedInHead);
        var viewOffset = main.AffineInverse() * eyeWorld;

        var naive = viewOffset.AffineInverse() * (main.AffineInverse() * (origin * cornerInOrigin));

        var fed = Panel.Placement(origin, eyeFeed: true);
        var rel = fed * (Panel.Pose.AffineInverse() * cornerInOrigin);
        var eyeOffsetWorld = origin.Basis * eyeInOrigin.Origin;
        var vertex = (main.Basis.Transposed() * (rel - eyeOffsetWorld)) + viewOffset.Origin;
        var precise = viewOffset.AffineInverse() * vertex;

        Assert.True(precise.DistanceTo(truth) < 2e-5f, $"precise error {precise.DistanceTo(truth)} m");
        Assert.True(naive.DistanceTo(truth) > 2e-4f, $"naive error {naive.DistanceTo(truth)} m, so the check can fail");
    }
}
