using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Danger Zone camera's engine-free rules (<see cref="DangerZonePhotograph"/>). The eye stands
/// two and a half chase distances ahead along the nose's level heading. It is scattered on the
/// world axes by at most 0.15, 0.25 and 0.15 of that distance. It turns with no roll to look back
/// at the aircraft, even with the nose straight up. Its cull mask draws the airframe a
/// first-person pilot's own pane leaves out.
/// Decode: docs/formats/campaign-screens.md, "The danger-zone slot".
/// </summary>
public class DangerZonePhotographTests
{
    private const float Dist = 18.5f;

    [Fact]
    public void TheEyeStandsTwoAndAHalfChaseDistancesAheadOfTheNose()
    {
        var plane = new Transform3D(Basis.Identity, new Vector3(100f, 50f, -20f));
        var eye = DangerZonePhotograph.Pose(plane, Dist, () => 0f);

        AssertNear(plane.Origin + new Vector3(0f, 0f, -2.5f * Dist), eye.Origin);
        AssertLooksAt(eye, plane.Origin);
    }

    [Fact]
    public void TheHeadingIsTheNosesLevelOneWhateverThePitch()
    {
        // Yawed a quarter turn left (nose on -X), then pitched 60 degrees up: the eye stays level
        // with the aircraft and the full 2.5 dist out, not shortened by the climb.
        var basis = new Basis(Vector3.Up, Mathf.Pi / 2f) * new Basis(Vector3.Right, Mathf.DegToRad(60f));
        var plane = new Transform3D(basis, new Vector3(0f, 300f, 0f));
        var eye = DangerZonePhotograph.Pose(plane, Dist, () => 0f);

        AssertNear(plane.Origin + new Vector3(-2.5f * Dist, 0f, 0f), eye.Origin);
        AssertLooksAt(eye, plane.Origin);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TheScatterReachesAFractionOfTheChaseDistanceOnEachWorldAxis(float draw)
    {
        var plane = Transform3D.Identity;
        var eye = DangerZonePhotograph.Pose(plane, Dist, () => draw);

        var ahead = new Vector3(0f, 0f, -2.5f * Dist);
        var scatter = new Vector3(0.15f * Dist, 0.25f * Dist, 0.15f * Dist) * draw;
        AssertNear(ahead + scatter, eye.Origin);
        AssertLooksAt(eye, plane.Origin);
    }

    [Fact]
    public void AStraightUpNoseLeavesTheEyeOnTheScatterAlone()
    {
        // Built from exact axes: a rotation by pi/2 leaves a float-rounding level heading behind.
        var up = new Basis(Vector3.Right, new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f));
        var plane = new Transform3D(up, new Vector3(5f, 80f, 5f));
        var eye = DangerZonePhotograph.Pose(plane, Dist, () => 1f);

        AssertNear(plane.Origin + new Vector3(0.15f * Dist, 0.25f * Dist, 0.15f * Dist), eye.Origin);
        AssertLooksAt(eye, plane.Origin);
    }

    [Fact]
    public void TheEyeNeverRolls()
    {
        var eye = DangerZonePhotograph.Pose(new Transform3D(new Basis(Vector3.Back, 1f), Vector3.Zero), Dist, () => 0.5f);

        // No roll: the eye's right axis lies in the horizontal plane.
        Assert.Equal(0f, eye.Basis.X.Y, 4);
    }

    [Fact]
    public void ThePhotographDrawsTheAirframeItsPilotsPaneLeavesOut()
    {
        for (int pane = 0; pane < SplitScreen.MaxPlayers; pane++)
        {
            uint seated = SplitScreen.OwnViewCullMask(SplitScreen.PlayerCullMask(pane), pane);
            uint photo = SplitScreen.OutsideCullMask(seated);
            Assert.Equal(0u, seated & SplitScreen.FirstPersonLayer(pane));
            Assert.NotEqual(0u, photo & SplitScreen.FirstPersonLayer(pane));

            // Nothing else moves: the photograph draws what the pane draws, its own pilot's body added.
            Assert.Equal(SplitScreen.FirstPersonLayer(pane), photo ^ seated);
        }

        // Layer 1, where the world is built, stays in every pane.
        Assert.Equal(1u, SplitScreen.PaneCullMask(0xFFFFFu) & 1u);
    }

    private static void AssertLooksAt(Transform3D eye, Vector3 target)
    {
        var forward = -eye.Basis.Z;
        var toward = (target - eye.Origin).Normalized();
        Assert.True(forward.Dot(toward) > 0.9999f, $"eye forward {forward} does not face {toward}");
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(expected.DistanceTo(actual) < 1e-3f, $"expected {expected}, got {actual}");
    }
}
