using System.Collections.Generic;
using CSVM.Flight.Camera;
using CSVM.Spec;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The headset's views, engine-free. Where each <see cref="XrAnchor"/> puts the origin, the VR
/// chase's offset and its two frames (<see cref="XrMount.Chase"/>), and a static camera's held
/// heading. Also the opening view and the <c>--xr-chase</c> flag (<see cref="SessionSpec"/>).
/// </summary>
public class XrViewsTests
{
    private const float Tol = 1e-4f;

    // A spread of attitudes: level, banked, climbing, inverted, past vertical and tumbling.
    private static readonly Basis[] Attitudes =
    {
        Basis.Identity,
        new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Back, 1.1f),
        new Basis(Vector3.Up, -2.3f) * new Basis(Vector3.Right, 0.6f) * new Basis(Vector3.Back, 0.3f),
        new Basis(Vector3.Up, 1.9f) * new Basis(Vector3.Back, Mathf.Pi),
        new Basis(Vector3.Up, 0.4f) * new Basis(Vector3.Right, 1.9f),
        Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f)),
    };

    [Fact]
    public void TheVrChaseSitsAtTheFlatChasesCruiseDistanceBelowTheLevelGaze()
    {
        var offset = CameraController.VrChaseOffset(new CamParams());
        // The authored rig at dist_min (15.7 m): 16.1 m out, its height 2.1 m.
        Assert.InRange(offset.Length(), 15.9f, 16.3f);
        Assert.Equal(0f, offset.X, 4);
        Assert.True(offset.Z > 0f, "behind the aircraft");
        float belowDeg = Mathf.RadToDeg(Mathf.Atan2(offset.Y, offset.Z));
        Assert.InRange(belowDeg, 7.4f, 7.7f);
    }

    [Fact]
    public void TheVrChaseTakesTheDistanceTheBoundsHold()
    {
        var rest = new CamParams();
        var far = new CamParams { Dist = 30f };
        Assert.Equal(far.DistMax / rest.DistMin,
            CameraController.VrChaseOffset(far).Length() / CameraController.VrChaseOffset(rest).Length(), 4);
        var near = new CamParams { Dist = 13f, DistFactor = 1f };
        Assert.Equal(CameraController.VrChaseOffset(new CamParams()), CameraController.VrChaseOffset(near));
    }

    [Fact]
    public void TheRigidChaseHoldsTheAircraftInOnePlaceWhateverItsAttitude()
    {
        var offset = CameraController.VrChaseOffset(new CamParams());
        var plane = new Vector3(-6350f, 300f, -4144f);
        foreach (var attitude in Attitudes)
        {
            var mount = XrMount.Chase(plane, attitude, offset, XrChase.Rigid);
            Assert.True(mount.Basis.IsEqualApprox(attitude.Orthonormalized()));
            // The aircraft in the origin's frame: the same point for every attitude.
            var seen = mount.Basis.Inverse() * (plane - mount.Origin);
            Assert.True(seen.DistanceTo(-offset) < 2e-3f, $"aircraft at {seen}, against {-offset}");
        }
    }

    [Fact]
    public void TheLevelChaseKeepsTheNoseAndDropsTheRoll()
    {
        var offset = CameraController.VrChaseOffset(new CamParams());
        var plane = new Vector3(100f, 500f, -40f);
        var straightUp = new Basis(Vector3.Up, 0.4f) * new Basis(Vector3.Right, Mathf.Pi / 2f) * new Basis(Vector3.Back, 0.8f);
        foreach (var attitude in new List<Basis>(Attitudes) { straightUp })
        {
            var mount = XrMount.Chase(plane, attitude, offset, XrChase.Level);
            Assert.True(mount.Basis.IsEqualApprox(mount.Basis.Orthonormalized()), "orthonormal");
            Assert.True(mount.Basis.Determinant() > 0.99f, "right-handed");
            Assert.True((-mount.Basis.Z).IsEqualApprox(-attitude.Orthonormalized().Z), "the aircraft's own forward");
            Assert.Equal(0f, mount.Basis.X.Y, 4);
            // The aircraft in the origin's frame: the same point for every attitude, so the pipper
            // on the nose axis keeps the rig's angle above it.
            var seen = mount.Basis.Inverse() * (plane - mount.Origin);
            Assert.True(seen.DistanceTo(-offset) < 2e-3f, $"aircraft at {seen}, against {-offset}");
        }

        // Upright, a sky-side up; inverted level flight shows the world upright too.
        Assert.True(XrMount.NoRoll(Attitudes[3]).Y.Y > 0.99f);
    }

    [Fact]
    public void TheLevelChaseRollsHalfACircleThroughVerticalFlight()
    {
        // A loop entered heading north: just short of vertical and just past it.
        var rising = new Basis(Vector3.Right, Mathf.DegToRad(89f));
        var over = new Basis(Vector3.Right, Mathf.DegToRad(91f));
        var a = XrMount.Chase(Vector3.Zero, rising, Vector3.Back, XrChase.Level).Basis;
        var b = XrMount.Chase(Vector3.Zero, over, Vector3.Back, XrChase.Level).Basis;
        // The gaze moves the aircraft's two degrees; the view's up turns over.
        Assert.InRange(Mathf.RadToDeg((-a.Z).AngleTo(-b.Z)), 1.99f, 2.01f);
        Assert.True(a.Y.Dot(b.Y) < -0.99f, $"up {a.Y} then {b.Y}");
        // The rigid chase moves two degrees with the aircraft.
        var ra = -XrMount.Chase(Vector3.Zero, rising, Vector3.Back, XrChase.Rigid).Basis.Z;
        var rb = -XrMount.Chase(Vector3.Zero, over, Vector3.Back, XrChase.Rigid).Basis.Z;
        Assert.InRange(Mathf.RadToDeg(ra.AngleTo(rb)), 1.99f, 2.01f);
    }

    [Fact]
    public void AStaticCameraFacesTheTrackSoAPassCrossesTheView()
    {
        // The aircraft flies along -Z; the spot is 6 m to its right and 200 m ahead.
        var aircraft = new Vector3(100f, 300f, 50f);
        var eye = aircraft + new Vector3(6f, 1f, -200f);
        var held = XrMount.Watch(eye, aircraft, Vector3.Forward);
        Assert.True((-held.Z).IsEqualApprox(Vector3.Left), $"facing {-held.Z}");
        Assert.True(held.Y.IsEqualApprox(Vector3.Up));
        // Behind the aircraft's track there is nothing ahead to face, so it faces the aircraft.
        var behind = aircraft + new Vector3(0f, 0f, 30f);
        Assert.True((-XrMount.Watch(behind, aircraft, Vector3.Forward).Z).IsEqualApprox(Vector3.Forward));
        // A zero track faces the aircraft too: the crash cut's still wreck.
        var above = aircraft + new Vector3(-30f, 45f, 0f);
        Assert.True((-XrMount.Watch(above, aircraft, Vector3.Zero).Z).IsEqualApprox(Vector3.Right));
        // Straight over the track, seen from above it has no side to face from.
        var over = aircraft + new Vector3(0f, 6f, -200f);
        Assert.True((-XrMount.Watch(over, aircraft, Vector3.Forward).Z).IsEqualApprox(Vector3.Back));
    }

    [Fact]
    public void EachAnchorTakesTheFlatCameraItsOwnWay()
    {
        var attitude = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.2f) * new Basis(Vector3.Back, 0.3f);
        var eye = new Transform3D(attitude * new Basis(Vector3.Right, -0.08203f), new Vector3(100f, 50f, -20f));
        var offset = new Vector3(0f, 0f, 0.06f);

        var seat = XrMount.For(eye, XrAnchor.Seat, attitude, offset);
        Assert.True(seat.IsEqualApprox(XrMount.For(eye, attitude, offset)));
        Assert.True(seat.Origin.DistanceTo(eye.Origin + (attitude * offset)) < Tol);

        var camera = XrMount.For(eye, XrAnchor.Camera, attitude, offset);
        Assert.True(camera.IsEqualApprox(eye));

        var level = XrMount.For(eye, XrAnchor.Level, attitude, offset);
        Assert.True(level.IsEqualApprox(XrMount.For(eye, null)));
        Assert.True(level.Basis.Y.IsEqualApprox(Vector3.Up));
    }

    [Fact]
    public void AHeadsetFlightOpensInTheCockpitWhateverTheSavedView()
    {
        var spec = SessionSpec.Parse(new[] { "--fly" }).WithSavedDefaultView("chase");
        Assert.Equal(PilotViewMode.Chase, spec.ViewMode);
        Assert.Equal(PilotViewMode.Cockpit, spec.WithHeadsetView(true).ViewMode);
        Assert.Equal(PilotViewMode.Chase, spec.WithHeadsetView(false).ViewMode);
        var scripted = SessionSpec.Parse(new[] { "--fly", "--det" });
        Assert.Equal(PilotViewMode.Cockpit, scripted.WithHeadsetView(true).ViewMode);
    }

    [Fact]
    public void ANamedViewBeatsTheHeadsetOpeningView()
    {
        Assert.Equal(PilotViewMode.Chase,
            SessionSpec.Parse(new[] { "--fly", "--view=chase" }).WithHeadsetView(true).ViewMode);
        Assert.Equal(PilotViewMode.Nose,
            SessionSpec.Parse(new[] { "--fly", "--view=nose" }).WithHeadsetView(true).ViewMode);
        Assert.Equal(PilotViewMode.Chase,
            SessionSpec.Parse(new[] { "--fly", "--view=flyby" }).WithHeadsetView(true).ViewMode);
        Assert.Equal(PilotViewMode.Chase,
            SessionSpec.Parse(new[] { "--viewer" }).WithHeadsetView(true).ViewMode);
    }

    [Fact]
    public void TheChaseFrameFlagParsesAndAnUnknownWordIsIgnoredWithANote()
    {
        Assert.Equal(XrChase.Rigid, SessionSpec.Parse(new[] { "--fly" }).XrChase);
        Assert.Equal(XrChase.Level, SessionSpec.Parse(new[] { "--fly", "--xr-chase=level" }).XrChase);
        Assert.Equal(XrChase.Level, SessionSpec.Parse(new[] { "--fly", "--xr-chase=LEVEL" }).XrChase);
        foreach (string word in new[] { "sideways", "1", "" })
        {
            var spec = SessionSpec.Parse(new[] { "--fly", "--xr-chase=level", $"--xr-chase={word}" });
            Assert.Equal(XrChase.Level, spec.XrChase);
            Assert.Contains(spec.Warnings, n => n.Message.Contains("--xr-chase=", System.StringComparison.Ordinal));
        }
    }
}
