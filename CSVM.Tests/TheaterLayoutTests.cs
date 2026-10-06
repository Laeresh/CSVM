using CSVM.UI.Boards;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine-free half of the theater screen (<see cref="TheaterLayout"/>). It covers which canvas
/// layers the screen shows, and how big and how finely it is drawn for a given 2D layout. It also
/// covers where a pointer ray meets it. The layouts are a stand-in eye, a headset eye and a 16:9 window.
/// </summary>
[Trait("Tier", "Quick")]
public class TheaterLayoutTests
{
    private static readonly Vector2 SimEye = new(640f, 720f);
    private static readonly Vector2 HeadsetEye = new(3072f, 3264f);
    private static readonly Vector2 Window = new(1280f, 720f);

    [Fact]
    public void TheBoardsAndCinemasMoveAndTheFlightChromeStays()
    {
        Assert.True(TheaterLayout.Hosts(HudLayers.Board));
        Assert.True(TheaterLayout.Hosts(HudLayers.Cinema));
        Assert.True(TheaterLayout.Hosts(HudLayers.PerfReadout));
        Assert.True(TheaterLayout.Hosts(HudLayers.KeyboardEcho));
        Assert.False(TheaterLayout.Hosts(HudLayers.Hud));
        Assert.False(TheaterLayout.Hosts(HudLayers.MissionEndFade));
        Assert.False(TheaterLayout.Hosts(HudLayers.Lab));
        Assert.False(TheaterLayout.Hosts(HudLayers.CockpitPass));
    }

    [Fact]
    public void OnlyABoardOrACinemaBringsTheScreenUpInFlight()
    {
        Assert.True(TheaterLayout.Raises(HudLayers.Board));
        Assert.True(TheaterLayout.Raises(HudLayers.Cinema));
        Assert.False(TheaterLayout.Raises(HudLayers.PerfReadout));
        Assert.False(TheaterLayout.Raises(HudLayers.BuildStamp));
        Assert.False(TheaterLayout.Raises(HudLayers.KeyboardEcho));
    }

    // An authored board pixel is two texels whatever the layout, so 600 rows become 1200.
    [Theory]
    [InlineData(640f, 720f)]
    [InlineData(3072f, 3264f)]
    [InlineData(1280f, 720f)]
    [InlineData(1920f, 1200f)]
    public void TheSurfaceDrawsEveryAuthoredPixelAsTwoTexels(float w, float h)
    {
        var layout = new Vector2(w, h);
        var fit = BoardFit.For(w, h);
        float boardRows = BoardFit.AuthoredHeight * fit.Scale * TheaterLayout.SurfaceScale(layout);
        Assert.Equal(1200f, boardRows, 3);
        var size = TheaterLayout.SurfaceSize(layout);
        Assert.Equal(w / h, size.X / (float)size.Y, 2);
    }

    [Fact]
    public void AHeadsetEyeIsDrawnSmallerThanItsLayoutAndAStandInEyeLarger()
    {
        Assert.Equal(new Vector2I(1600, 1700), TheaterLayout.SurfaceSize(HeadsetEye));
        Assert.Equal(new Vector2I(1600, 1800), TheaterLayout.SurfaceSize(SimEye));
        Assert.Equal(new Vector2I(2133, 1200), TheaterLayout.SurfaceSize(Window));
    }

    // The default seat puts the board's 1.53 m at 2.5 m, 34 degrees tall, 35 surface rows a degree.
    [Fact]
    public void TheDefaultBoardSpansThirtyFourDegrees()
    {
        var seat = TheaterLayout.Default;
        Assert.Equal(34.0f, seat.VerticalDeg, 1);
        Assert.InRange(seat.RowsPerDegree(), 35f, 35.5f);
        Assert.Equal(new Vector3(0f, -0.1f, -2.5f), seat.Pose.Origin);
    }

    // A narrow layout letterboxes the board top and bottom, a wide one at the sides. Either way the
    // board inside is the seat's height, and the quad keeps the layout's aspect.
    [Fact]
    public void TheScreenIsSizedSoItsInscribedBoardIsTheSeatsHeight()
    {
        var seat = TheaterLayout.Default;
        foreach (var layout in new[] { SimEye, HeadsetEye, Window })
        {
            var size = seat.SizeM(layout);
            var fit = BoardFit.For(layout.X, layout.Y);
            float boardM = size.Y * BoardFit.AuthoredHeight * fit.Scale / layout.Y;
            Assert.Equal(seat.HeightM, boardM, 4);
            Assert.Equal(layout.X / layout.Y, size.X / size.Y, 4);
        }

        Assert.Equal(seat.HeightM, seat.SizeM(Window).Y, 4);
        Assert.Equal(seat.HeightM * 4f / 3f, seat.SizeM(SimEye).X, 4);
    }

    [Fact]
    public void ARayFromTheEyeThroughTheCentreHitsTheMiddle()
    {
        var seat = TheaterLayout.Default;
        var hit = seat.Hit(Vector3.Zero, seat.Pose.Origin.Normalized(), Window);
        Assert.NotNull(hit);
        Assert.Equal(0.5f, hit!.Value.X, 4);
        Assert.Equal(0.5f, hit.Value.Y, 4);
    }

    // From a hand off the eye, a ray to a point on the screen comes back as that point's layout pixel.
    [Fact]
    public void AHandRayLandsOnThePixelItPointsAt()
    {
        var seat = TheaterLayout.Default;
        var hand = new Vector3(0.18f, -0.32f, -0.12f);
        var size = seat.SizeM(HeadsetEye);
        var target = new Vector2(0.2f, 0.85f);
        var onScreen = seat.Pose.Origin + new Vector3((target.X - 0.5f) * size.X, (0.5f - target.Y) * size.Y, 0f);
        var hit = seat.Hit(hand, (onScreen - hand).Normalized(), HeadsetEye);
        Assert.NotNull(hit);
        Assert.Equal(target.X, hit!.Value.X, 4);
        Assert.Equal(target.Y, hit.Value.Y, 4);
        var pixel = TheaterLayout.LayoutPoint(hit.Value, HeadsetEye);
        Assert.Equal(614.4f, pixel.X, 1);
        Assert.Equal(2774.4f, pixel.Y, 1);
    }

    // PointAt is Hit's inverse: the screen point it names is where a ray from a hand lands, which is
    // where the pointer's beam ends.
    [Fact]
    public void PointAtIsWhereAHandRayLands()
    {
        var seat = TheaterLayout.Default;
        var hand = new Vector3(-0.2f, -0.3f, -0.1f);
        var target = new Vector2(0.73f, 0.31f);
        var onScreen = seat.PointAt(target, HeadsetEye);
        Assert.Equal(seat.Pose.Origin.Z, onScreen.Z, 5);
        var hit = seat.Hit(hand, (onScreen - hand).Normalized(), HeadsetEye);
        Assert.NotNull(hit);
        Assert.Equal(target.X, hit!.Value.X, 4);
        Assert.Equal(target.Y, hit.Value.Y, 4);
    }

    // A cutscene plays where the 800x600 board stands, the seat's height and 4:3 on every layout.
    // Each eye's render draws it at the boards' two texels per authored pixel.
    [Fact]
    public void ACutscenePlaysOnTheBoardsOwnRect()
    {
        var seat = TheaterLayout.Default;
        foreach (var layout in new[] { SimEye, HeadsetEye, Window })
        {
            var rect = TheaterLayout.CutsceneRect(layout);
            var size = seat.SizeM(layout);
            Assert.Equal(seat.HeightM, rect.Size.Y * size.Y, 4);
            Assert.Equal(4f / 3f, rect.Size.X * size.X / (rect.Size.Y * size.Y), 4);
            Assert.Equal(0.5f, rect.Position.X + (rect.Size.X * 0.5f), 4);
            Assert.Equal(0.5f, rect.Position.Y + (rect.Size.Y * 0.5f), 4);
        }

        Assert.Equal(new Vector2I(1600, 1200), TheaterLayout.CutsceneSize);
        Assert.Equal(new Rect2(0f, 0f, 1f, 1f), TheaterLayout.CutsceneRect(Vector2.Zero));
    }

    [Fact]
    public void ARayAwayFromOrBesideTheScreenLandsNowhere()
    {
        var seat = TheaterLayout.Default;
        Assert.Null(seat.Hit(Vector3.Zero, new Vector3(0f, 0f, 1f), Window));
        Assert.Null(seat.Hit(Vector3.Zero, new Vector3(1f, 0f, 0f), Window));
        Assert.Null(seat.Hit(Vector3.Zero, new Vector3(0.9f, 0f, -0.1f).Normalized(), Window));
        Assert.Null(seat.Hit(new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, -1f), Window));
    }
}
