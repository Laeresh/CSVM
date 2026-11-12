using CSVM.Flight.Airframe;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Assisted control law's outputs at fixed states, on the default airframe so no
/// install is needed. The flown behaviour (a held turn, a loop) is the <c>flight-assisted-law</c>
/// suite's; these pin the deflections <c>docs/assisted-flight.md</c> describes.</summary>
public class AssistedFlightTests
{
    private const float Dt = 1f / 60f;
    private const float Speed = 100f;

    [Fact]
    public void InsideTheRudderBand_TheRudderAloneTurns()
    {
        var left = Steer(Level(), new FlightInput { Roll = 0.15f });
        var right = Steer(Level(), new FlightInput { Roll = -0.15f });
        float flat = 0.15f / AssistedFlight.RudderBand * AssistedFlight.FlatRudder;
        Assert.Equal(0f, left.Roll, 4);
        Assert.Equal(0f, left.Pitch, 4);
        Assert.Equal(flat, left.Yaw, 4);
        Assert.Equal(-flat, right.Yaw, 4);

        // The control: past the band the same airframe banks.
        Assert.True(Steer(Level(), new FlightInput { Roll = 0.6f }).Roll > 0.5f);
    }

    [Fact]
    public void ACentredStick_RollsTheWingsLevel()
    {
        Assert.True(Steer(Banked(40f), default).Roll < -0.5f, "banked left, it rolls right");
        Assert.True(Steer(Banked(-40f), default).Roll > 0.5f, "banked right, it rolls left");
        Assert.Equal(0f, Steer(Level(), default).Roll, 4);
    }

    [Fact]
    public void FullSidewaysStick_PullsFullElevator()
    {
        // Banked as steeply as full stick asks, holding level: the hardest turn is full pull.
        Assert.Equal(1f, Steer(Banked(80f), new FlightInput { Roll = 1f }).Pitch);
        Assert.Equal(1f, Steer(Banked(-80f), new FlightInput { Roll = -1f }).Pitch);

        // The control: half stick in the same bank asks for less than full pull.
        float half = Steer(Banked(80f), new FlightInput { Roll = 0.5f }).Pitch;
        Assert.True(half > 0f && half < 0.9f, $"half stick pulls {half}");
    }

    [Fact]
    public void ACentredStick_HoldsTheClimbAngleItWasLeftAt()
    {
        var state = default(AssistedState);
        var climbing = Plant(Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(10f)));
        float onTarget = AssistedFlight.Steer(default, climbing, ref state, Dt).Pitch;
        Assert.Equal(10f, Mathf.RadToDeg(state.HeldClimb), 3);
        Assert.Equal(0f, onTarget, 3);

        // Sagged to 5 degrees with the stick still centred, it pulls back up toward 10.
        var sagged = Plant(Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(5f)));
        Assert.True(AssistedFlight.Steer(default, sagged, ref state, Dt).Pitch > 0.1f);
        Assert.Equal(10f, Mathf.RadToDeg(state.HeldClimb), 3);

        // A stick pushed at 5 degrees moves the held angle there.
        AssistedFlight.Steer(new FlightInput { Pitch = -0.2f }, sagged, ref state, Dt);
        Assert.Equal(5f, Mathf.RadToDeg(state.HeldClimb), 3);
    }

    /// <summary>A plane-axis elevator and a horizon-relative climb agree wings level and part once
    /// banked. This pull is taken at 60 degrees, where some of the climb goes to the rudder.</summary>
    [Fact]
    public void APullInABank_ClimbsAgainstTheHorizon()
    {
        var pull = new FlightInput { Pitch = 0.3f };
        var level = Steer(Level(), pull);
        var banked = Steer(Banked(60f), pull);
        Assert.Equal(0f, level.Yaw, 4);
        Assert.True(level.Pitch > 0.5f);
        Assert.True(banked.Yaw < -0.5f, $"left wing low, the nose climbs by yawing right ({banked.Yaw})");
        Assert.True(banked.Pitch > 0f && banked.Pitch < level.Pitch, $"and pitches less ({banked.Pitch} against {level.Pitch})");
    }

    [Fact]
    public void TheYawInput_PassesStraightThroughOntoTheRudder()
    {
        var plain = Steer(Banked(30f), new FlightInput { Roll = 0.5f, Throttle = 0.7f });
        var yawed = Steer(Banked(30f), new FlightInput { Roll = 0.5f, Yaw = -0.3f, Throttle = 0.7f, Boost = true });
        Assert.Equal(plain.Yaw - 0.3f, yawed.Yaw, 5);
        Assert.Equal(plain.Roll, yawed.Roll);
        Assert.Equal(plain.Pitch, yawed.Pitch);
        Assert.Equal(0.7f, yawed.Throttle);
        Assert.True(yawed.Boost);
    }

    [Fact]
    public void AHeldPullFliesOverTheTop_AndAReleaseInvertedRollsUpright()
    {
        var overTop = Steer(Banked(175f), new FlightInput { Pitch = 1f });
        Assert.Equal(1f, overTop.Pitch);
        Assert.True(Mathf.Abs(overTop.Roll) < 0.5f, $"a held pull is not rolled out of halfway round ({overTop.Roll})");

        var released = Steer(Banked(175f), default);
        Assert.True(Mathf.Abs(released.Roll) > 0.9f, $"released inverted, it rolls upright ({released.Roll})");
        Assert.Equal(0f, released.Pitch, 3);
    }

    private static FlightInput Steer(FlightModel plant, FlightInput stick)
    {
        var state = default(AssistedState);
        return AssistedFlight.Steer(stick, plant, ref state, Dt);
    }

    private static FlightModel Level() => Plant(Basis.Identity);

    // Positive degrees put the left wing low, the plant's own sign.
    private static FlightModel Banked(float degrees) => Plant(Basis.Identity.Rotated(Vector3.Back, Mathf.DegToRad(degrees)));

    private static FlightModel Plant(Basis attitude)
    {
        var plant = new FlightModel(new PlaneStats());
        plant.Reset(new Vector3(0f, 1000f, 0f), attitude, Speed, 1f);
        return plant;
    }
}
