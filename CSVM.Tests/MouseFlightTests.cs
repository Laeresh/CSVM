using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The mouse stick's own arithmetic, decoded from the mouse arm of <c>FUN_00487460</c>: the
/// per-axis deadzone and its rescale, the sign each source reaches the stick with, and the
/// <c>is_autogyro</c> exchange that makes an autogyro yaw where an aeroplane banks.</summary>
public class MouseFlightTests
{
    [Fact]
    public void Gate_IsDeadInsideTheDeadzoneAndRescalesOutsideIt()
    {
        Assert.Equal(0f, MouseFlight.Gate(0.1f, MouseFlight.AttitudeDeadzone));
        Assert.Equal(0f, MouseFlight.Gate(-0.1f, MouseFlight.AttitudeDeadzone));
        // (0.8 - 0.1) / 0.9, the original's subtract-and-multiply-by-1/0.9.
        Assert.Equal(0.7777778f, MouseFlight.Gate(0.8f, MouseFlight.AttitudeDeadzone), 5);
        Assert.Equal(-0.7777778f, MouseFlight.Gate(-0.8f, MouseFlight.AttitudeDeadzone), 5);
        // The full travel reaches exactly full deflection on either deadzone.
        Assert.Equal(1f, MouseFlight.Gate(1f, MouseFlight.AttitudeDeadzone), 5);
        Assert.Equal(1f, MouseFlight.Gate(1f, MouseFlight.YawDeadzone), 5);
    }

    /// <summary>The third axis's deadzone is three times the other two, so a source that already
    /// banks an aeroplane hard is still inside that axis's dead band.</summary>
    [Fact]
    public void Gate_TakesAWiderDeadzoneOnTheThirdAxis()
    {
        Assert.NotEqual(0f, MouseFlight.Gate(0.25f, MouseFlight.AttitudeDeadzone));
        Assert.Equal(0f, MouseFlight.Gate(0.25f, MouseFlight.YawDeadzone));
    }

    /// <summary>An aeroplane banks with sideways motion and takes no yaw from the mouse at all, this
    /// port having no third mouse axis for the yaw source to come off.</summary>
    [Fact]
    public void Read_BanksAnAeroplaneWithSidewaysMotion()
    {
        var right = MouseFlight.Read(0.8f, 0f, 0f, isAutogyro: false);

        Assert.True(right.Roll < 0f, $"a cursor right of the middle banks right, got {right.Roll}");
        Assert.Equal(-0.7777778f, right.Roll, 5);
        Assert.Equal(0f, right.Yaw);
        Assert.Equal(0f, right.Pitch);
    }

    /// <summary>0x4876f4: the autogyro's roll source is the third axis and its yaw source the
    /// sideways travel, each negated, so the same cursor yaws it and banks it nowhere.</summary>
    [Fact]
    public void Read_YawsAnAutogyroWithTheSameMotion()
    {
        var right = MouseFlight.Read(0.8f, 0f, 0f, isAutogyro: true);

        Assert.True(right.Yaw < 0f, $"a cursor right of the middle yaws right, got {right.Yaw}");
        // (0.8 - 0.1) / 0.9: the yaw slot carries the sideways travel here, so it carries that
        // travel's own deadzone rather than the absent third axis's.
        Assert.Equal(-0.7777778f, right.Yaw, 5);
        Assert.Equal(0f, right.Roll);
    }

    /// <summary>The autogyro's whole mouse mapping, on the two axes this port has: an offset that
    /// banks an aeroplane yaws the autogyro by the same amount, the pitch source is the same on
    /// both, and the mouse never touches the autogyro's roll, which stays the keys'. The offset is
    /// deliberately inside the third axis's 0.3 dead band, where an autogyro used to read exactly
    /// nothing sideways while the same cursor already banked an aeroplane.</summary>
    [Fact]
    public void Read_PutsTheAutogyrosLateralAxisWhereTheAeroplanesBankIs()
    {
        var plane = MouseFlight.Read(0.25f, 0.25f, 0f, isAutogyro: false);
        var gyro = MouseFlight.Read(0.25f, 0.25f, 0f, isAutogyro: true);

        // (0.25 - 0.1) / 0.9, the sideways travel's own gate, on the bank slot and the yaw slot.
        Assert.Equal(-0.1666667f, plane.Roll, 5);
        Assert.Equal(-0.1666667f, gyro.Yaw, 5);
        Assert.Equal(0f, gyro.Roll);
        Assert.Equal(plane.Pitch, gyro.Pitch);
        Assert.True(gyro.Pitch > 0f, $"a cursor below the middle pulls the nose up, got {gyro.Pitch}");
        // The able-to-fail half: the aeroplane's yaw is the one that still sits on the absent third
        // axis, so it reads zero at an offset that yaws the autogyro.
        Assert.Equal(0f, plane.Yaw);
    }

    /// <summary>Both airframes pitch off the same source: the exchange is between the other two, and
    /// the pitch write happens before it.</summary>
    [Fact]
    public void Read_PitchesBothAirframesTheSameWay()
    {
        var plane = MouseFlight.Read(0f, 0.6f, 0f, isAutogyro: false);
        var gyro = MouseFlight.Read(0f, 0.6f, 0f, isAutogyro: true);

        Assert.Equal(0.5555556f, plane.Pitch, 5);
        Assert.Equal(plane.Pitch, gyro.Pitch);
    }

    /// <summary>A cursor near the middle of the pane flies nothing, on either airframe, which is
    /// what lets a player let go of the mouse without holding a deflection.</summary>
    [Fact]
    public void Read_FliesNothingInsideTheDeadzone()
    {
        foreach (bool autogyro in new[] { false, true })
        {
            var idle = MouseFlight.Read(0.09f, 0.09f, 0f, autogyro);

            Assert.Equal(0f, idle.Roll);
            Assert.Equal(0f, idle.Pitch);
            Assert.Equal(0f, idle.Yaw);
        }
    }

    /// <summary>The pane's own geometry: the middle reads a centred stick, either edge reads exactly
    /// full travel, and past the edge clamps rather than running away.</summary>
    [Fact]
    public void Offset_MapsThePaneOntoTheStick()
    {
        var middle = new Vector2(400f, 300f);
        var half = new Vector2(400f, 300f);

        Assert.Equal(Vector2.Zero, MouseFlight.Offset(middle, middle, half));
        Assert.Equal(new Vector2(1f, 1f), MouseFlight.Offset(new Vector2(800f, 600f), middle, half));
        Assert.Equal(new Vector2(-1f, -1f), MouseFlight.Offset(Vector2.Zero, middle, half));
        Assert.Equal(new Vector2(1f, -1f), MouseFlight.Offset(new Vector2(9000f, -9000f), middle, half));
    }

    /// <summary>A pane with no extent reads centred rather than dividing by nothing, which is what a
    /// seat asked for its stick before its viewport has a size would otherwise do.</summary>
    [Fact]
    public void Offset_ReadsCentredWithNoPaneToMeasure()
    {
        Assert.Equal(Vector2.Zero, MouseFlight.Offset(new Vector2(10f, 10f), Vector2.Zero, Vector2.Zero));
    }
}
