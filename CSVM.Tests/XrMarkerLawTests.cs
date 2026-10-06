using CSVM.Flight.Hud;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stereo flight markers' geometry, engine-free (<see cref="XrMarkerLaw"/>). It pins a reference
/// pixel's angle against the flat pane's pixel and which marker stays in the world. It pins the
/// ring's bearing and how a world card faces the eyes.
/// </summary>
public class XrMarkerLawTests
{
    // A head turned, pitched and rolled, so no basis product is an exact 0 or 1.
    private static readonly Basis Head = Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f));

    [Theory]
    [InlineData(64.3698f, 720)]
    [InlineData(46.8264f, 720)]
    [InlineData(64.3698f, 1440)]
    public void ACardHoldsTheFlatPipperSizeAtTheViewCentre(float fovDeg, int paneHeight)
    {
        // A pinhole the flat pane's height tall over the view's angle sees a 40-pixel pipper card at
        // any distance. It spans the pixels the flat HUD draws it in, 40 x height / 1440.
        float f = paneHeight / (2f * Mathf.Tan(Mathf.DegToRad(fovDeg) * 0.5f));
        foreach (float distance in new[] { 5f, 430f, 9000f })
        {
            float metres = 40f * XrMarkerLaw.TangentPerReferencePixel(fovDeg) * distance;
            float pixels = f * metres / distance;
            Assert.Equal(40f * paneHeight / HudMetrics.ReferenceHeight, pixels, 3);
        }
    }

    [Fact]
    public void AMarkerStaysInTheWorldUpToTheRingAndLeavesPastIt()
    {
        foreach (float deg in new[] { 0f, 10f, XrMarkerLaw.RingDeg - 0.05f })
        {
            Assert.True(XrMarkerLaw.OnView(Off(deg, 30f)), $"{deg} degrees off the axis is on view");
        }

        foreach (float deg in new[] { XrMarkerLaw.RingDeg + 0.05f, 90f, 179f })
        {
            Assert.False(XrMarkerLaw.OnView(Off(deg, 30f)), $"{deg} degrees off the axis goes to the ring");
        }

        Assert.Equal(180f, XrMarkerLaw.OffAxisDeg(new Vector3(0f, 0f, 400f)), 3);
    }

    [Fact]
    public void TheRingPointsAlongTheBearingInTheHeadsViewPlane()
    {
        Assert.True(XrMarkerLaw.RingDirection(new Vector3(300f, 0f, -20f)).IsEqualApprox(new Vector2(1f, 0f)));
        Assert.True(XrMarkerLaw.RingDirection(new Vector3(0f, 50f, 10f)).IsEqualApprox(new Vector2(0f, 1f)));
        Assert.True(XrMarkerLaw.RingDirection(new Vector3(-150f, 0f, 200f)).IsEqualApprox(new Vector2(-1f, 0f)));
        var diagonal = XrMarkerLaw.RingDirection(new Vector3(3f, -4f, 1f));
        Assert.True(diagonal.IsEqualApprox(new Vector2(0.6f, -0.8f)), $"{diagonal}");
    }

    [Fact]
    public void DeadAsternPointsDownAsTheFlatEdgeMarkerDoes()
    {
        Assert.Equal(new Vector2(0f, -1f), XrMarkerLaw.RingDirection(new Vector3(0f, 0f, 400f)));
        Assert.Equal(new Vector2(0f, -1f), XrMarkerLaw.RingDirection(new Vector3(0.001f, -0.002f, 400f)));
        Assert.Equal(new Vector2(0f, -1f), XrMarkerLaw.RingDirection(Vector3.Zero));
    }

    [Fact]
    public void InHeadReadsAWorldOffsetInTheHeadsAxes()
    {
        var local = new Vector3(12f, -3f, -250f);
        var world = Head * local;
        Assert.True(XrMarkerLaw.InHead(Head, world).DistanceTo(local) < 1e-3f, $"{XrMarkerLaw.InHead(Head, world)}");
    }

    [Fact]
    public void AWorldCardFacesTheEyeWithTheSeatsUp()
    {
        var eye = new Vector3(20000f, 310f, -4144f);
        var seatUp = Head.Y;
        foreach (var local in new[] { new Vector3(0f, 0f, -450f), new Vector3(40f, 10f, -300f), new Vector3(-90f, -60f, -120f) })
        {
            var at = eye + (Head * local);
            var axes = XrMarkerLaw.Facing(at, eye, seatUp, Head.Y);
            Assert.Equal(1f, axes.Determinant(), 4);
            Assert.Equal(1f, axes.Z.Dot((eye - at).Normalized()), 5);
            Assert.Equal(0f, axes.X.Dot(seatUp), 5);
            Assert.True(axes.Y.Dot(seatUp) > 0f, "the card's up keeps the seat's side");
        }
    }

    [Fact]
    public void ACardStraightAboveTheSeatTakesTheFallbackUp()
    {
        var eye = Vector3.Zero;
        var axes = XrMarkerLaw.Facing(new Vector3(0f, 500f, 0f), eye, Vector3.Up, Vector3.Back);
        Assert.Equal(1f, axes.Determinant(), 4);
        Assert.Equal(-1f, axes.Z.Y, 5);
        Assert.Equal(0f, axes.X.Dot(Vector3.Back), 5);
    }

    [Fact]
    public void ALabelStepsInByItsReachAlongTheBearing()
    {
        var half = new Vector2(60f, 10f);
        Assert.Equal(60f, XrMarkerLaw.Reach(new Vector2(-1f, 0f), half), 4);
        Assert.Equal(10f, XrMarkerLaw.Reach(new Vector2(0f, 1f), half), 4);
        Assert.Equal((0.6f * 60f) + (0.8f * 10f), XrMarkerLaw.Reach(new Vector2(0.6f, -0.8f), half), 4);
    }

    // A point `deg` off the head's view axis at `distance`, in the head's frame.
    private static Vector3 Off(float deg, float distance)
    {
        float r = Mathf.DegToRad(deg);
        return new Vector3(Mathf.Sin(r) * distance * 0.6f, Mathf.Sin(r) * distance * 0.8f, -Mathf.Cos(r) * distance);
    }
}
