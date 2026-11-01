using System;
using CSVM.UI.Labs;
using Godot;
using Xunit;
using Handle = CSVM.UI.Labs.CourseGizmo.Handle;

namespace CSVM.Tests;

/// <summary>The course gizmo's math, engine-free: a ray onto an axis, the heading swept round a gate, the
/// screen size, and which handle a press picks.</summary>
public class CourseGizmoTests
{
    [Fact]
    public void AlongAxisIsTheAxisPointNearestTheRay()
    {
        // A ray straight down through x = 7 meets the X axis line through (1, 0, 0) six units along.
        Assert.Equal(6f, CourseGizmo.AlongAxis(new Vector3(1f, 0f, 0f), Vector3.Right, new Vector3(7f, 50f, 0f), Vector3.Down)!.Value, 4);

        // A slanted ray through the Y axis line at height 12 is nearest it there.
        var from = new Vector3(30f, 0f, 40f);
        var dir = (new Vector3(0f, 12f, 0f) - from).Normalized();
        Assert.Equal(12f, CourseGizmo.AlongAxis(Vector3.Zero, Vector3.Up, from, dir)!.Value, 3);

        // A ray running along the axis has no nearest point.
        Assert.Null(CourseGizmo.AlongAxis(Vector3.Zero, Vector3.Back, new Vector3(0f, 5f, -20f), Vector3.Back));
    }

    [Theory]
    [InlineData(0f, -1f, 0f)]
    [InlineData(-1f, 0f, 90f)]
    [InlineData(1f, 0f, -90f)]
    [InlineData(0f, 1f, 180f)]
    public void AngleAroundIsTheHeadingTowardsTheRaysHit(float x, float z, float heading)
    {
        // The ray drops onto the centre's plane at the offset; its heading is the spawn convention's.
        var centre = new Vector3(100f, 40f, -50f);
        var at = centre + (new Vector3(x, 0f, z) * 10f);
        float angle = CourseGizmo.AngleAround(centre, at + (Vector3.Up * 30f), Vector3.Down)!.Value;
        Assert.Equal(0f, CourseGizmo.WrapDeg(angle - heading), 3);
        var forward = new Flight.Modes.SpawnPoint(Vector3.Zero, angle).Forward;
        Assert.True(forward.IsEqualApprox(new Vector3(x, 0f, z)), $"{forward} for heading {angle}");
    }

    [Fact]
    public void AngleAroundIsNullOffThePlaneOrAtTheCentre()
    {
        Assert.Null(CourseGizmo.AngleAround(Vector3.Zero, new Vector3(0f, 10f, 5f), Vector3.Up));
        Assert.Null(CourseGizmo.AngleAround(Vector3.Zero, new Vector3(0f, 10f, 5f), Vector3.Back));
        Assert.Null(CourseGizmo.AngleAround(Vector3.Zero, new Vector3(0f, 10f, 0f), Vector3.Down));
    }

    [Theory]
    [InlineData(90f, -45f, -5f, false, 130f)]
    [InlineData(170f, 0f, 30f, false, -160f)]   // past 180, it wraps
    [InlineData(0f, 170f, -170f, false, 20f)]   // the sweep across the seam is the short way, +20
    [InlineData(0f, -170f, 170f, false, -20f)]
    [InlineData(90f, -45f, 2f, true, 135f)]     // 137 snaps to 135
    [InlineData(-170f, 0f, -12f, true, 180f)]   // -182 snaps to -180, kept as -180
    public void TurnedSweepsTheShortWayAndWraps(float heading, float from, float to, bool snap, float expected)
    {
        Assert.Equal(0f, CourseGizmo.WrapDeg(CourseGizmo.Turned(heading, from, to, snap) - expected), 3);
        float turned = CourseGizmo.Turned(heading, from, to, snap);
        Assert.InRange(turned, -180f, 179.999f);
    }

    [Fact]
    public void ScreenScaleKeepsOnePixelSize()
    {
        // At 60 degrees over 720 rows, a 110 px arrow at 400 m depth spans 400 * 2 tan 30 * 110 / 720 metres.
        float metres = CourseGizmo.ScreenScale(400f, 60f, 720f, 110f);
        Assert.Equal(400f * 2f * MathF.Tan(MathF.PI / 6f) * 110f / 720f, metres, 3);
        Assert.Equal(2f * metres, CourseGizmo.ScreenScale(800f, 60f, 720f, 110f), 3);
        Assert.Equal(0.5f * metres, CourseGizmo.ScreenScale(400f, 60f, 1440f, 110f), 3);

        // Projected back by a pinhole of the same field, the arrow is 110 px tall whatever the depth.
        foreach (float depth in new[] { 50f, 400f, 6000f })
        {
            float focalPx = 360f / MathF.Tan(MathF.PI / 6f);
            Assert.Equal(110f, CourseGizmo.ScreenScale(depth, 60f, 720f, 110f) * focalPx / depth, 2);
        }
    }

    [Fact]
    public void PickTakesTheNearestHandleWithinTolerance()
    {
        var lines = new[]
        {
            new[] { new Vector2(100f, 100f), new Vector2(200f, 100f) },  // X
            Array.Empty<Vector2>(),                                     // Y, hidden
            new[] { new Vector2(100f, 100f), new Vector2(100f, 200f) },  // Z
            new[] { new Vector2(150f, 90f), new Vector2(160f, 108f) },   // the ring, a short piece
        };
        Assert.Equal(Handle.X, CourseGizmo.Pick(new Vector2(180f, 106f), lines, 10f));
        Assert.Equal(Handle.Z, CourseGizmo.Pick(new Vector2(94f, 180f), lines, 10f));
        Assert.Equal(Handle.None, CourseGizmo.Pick(new Vector2(130f, 130f), lines, 10f));
        Assert.Equal(Handle.None, CourseGizmo.Pick(new Vector2(211f, 100f), lines, 10f));

        // Nearer wins: (155, 99) lies on the ring piece and 1 px off X.
        Assert.Equal(Handle.Yaw, CourseGizmo.Pick(new Vector2(155f, 99f), lines, 10f));

        // An exact tie, where X and Z meet, goes to the lower handle; a hidden one is never picked.
        Assert.Equal(Handle.X, CourseGizmo.Pick(new Vector2(100f, 100f), lines, 10f));
        Assert.Equal(Handle.None, CourseGizmo.Pick(new Vector2(300f, 300f), new[] { Array.Empty<Vector2>() }, 1000f));
    }
}
