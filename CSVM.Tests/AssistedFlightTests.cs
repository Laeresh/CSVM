using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Assisted control law's outputs at fixed states, on the default airframe so no
/// install is needed, and a steep turn on two shipped ones. The flown behaviour (a held turn, a loop) is the <c>flight-assisted-law</c>
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

    /// <summary>A pull or push released holds the nose where it was left, not the lagging path, and
    /// the path then settles on that angle.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void AReleasedPull_HoldsTheNoseWhereItWasLeft(float pitch)
    {
        var plant = Level();
        var state = default(AssistedState);
        var stick = new FlightInput { Pitch = pitch, Throttle = 1f };
        for (int i = 0; i < 36; i++)
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);

        float sign = Mathf.Sign(pitch), noseAt = NoseDeg(plant), back = 0f;
        Assert.True(sign * (noseAt - PathDeg(plant)) > 10f, $"the path lags the nose at release ({noseAt} against {PathDeg(plant)})");
        stick.Pitch = 0f;
        plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
        // Captured on the last pull step, one step of pitch rate before noseAt.
        Assert.True(Mathf.Abs(noseAt - Mathf.RadToDeg(state.HeldClimb)) < 1.5f, $"it holds {Mathf.RadToDeg(state.HeldClimb)}, the nose's {noseAt}");
        for (int i = 0; i < 480; i++)
        {
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
            back = Mathf.Max(back, sign * (noseAt - NoseDeg(plant)));
        }

        Assert.True(back < 3f, $"the nose falls back {back} degrees from {noseAt}");
        Assert.True(Mathf.Abs(PathDeg(plant) - noseAt) < 2f, $"the path settles at {PathDeg(plant)} against the nose's {noseAt} at release");
    }

    /// <summary>Sideways stick turns the plane at every nose angle. A steep climb or dive is flown
    /// from the start, where a law that stands its turn down near vertical leaves the stick dead.</summary>
    [ExtractedDataTheory]
    [InlineData("player_bhawk", -85f)]
    [InlineData("player_bhawk", -70f)]
    [InlineData("player_bhawk", 70f)]
    [InlineData("player_bhawk", 85f)]
    [InlineData("player_autogyro", -85f)]
    [InlineData("player_autogyro", -70f)]
    [InlineData("player_autogyro", 70f)]
    [InlineData("player_autogyro", 85f)]
    public void FullSidewaysStick_TurnsOnASteepClimbOrDive(string plane, float noseDeg)
    {
        var stats = PlaneStats.Load(TestZrdr.Root, plane);
        var plant = new FlightModel(stats);
        var attitude = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(noseDeg)).Rotated(Vector3.Up, 0.7f);
        plant.Reset(new Vector3(0f, 800f, 0f), attitude, stats.FdSpeed * 0.8f, 1f);
        var state = default(AssistedState);
        var stick = new FlightInput { Roll = 1f, Throttle = 1f };
        float heading = HeadingDeg(plant), turned = 0f;
        for (int i = 0; i < 180; i++)
        {
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
            float now = HeadingDeg(plant);
            turned += Mathf.Wrap(now - heading, -180f, 180f);
            heading = now;
        }

        Assert.True(turned > 60f, $"[{plane}] nose at {noseDeg} degrees, full left stick turns {turned} degrees left in 3 s");
    }

    /// <summary>On its back with sideways stick held, the plane stays over and holds its height. It
    /// turns toward the stick on the level chase's upside-down screen, which is the other way in
    /// the world. Centring the stick is what rolls it upright.</summary>
    [ExtractedDataTheory]
    [InlineData("player_bhawk", 1f)]
    [InlineData("player_bhawk", -1f)]
    [InlineData("player_autogyro", 1f)]
    [InlineData("player_autogyro", -1f)]
    public void OnItsBack_AHeldSidewaysStickTurnsThatWayOnTheScreenAndStaysInverted(string plane, float side)
    {
        var stats = PlaneStats.Load(TestZrdr.Root, plane);
        var plant = new FlightModel(stats);
        var attitude = Basis.Identity.Rotated(Vector3.Back, Mathf.Pi).Rotated(Vector3.Up, 0.7f);
        plant.Reset(new Vector3(0f, 1200f, 0f), attitude, stats.FdSpeed * 0.8f, 1f);
        var state = default(AssistedState);
        var stick = new FlightInput { Roll = side, Throttle = 1f };
        float heading = HeadingDeg(plant), turned = 0f, highestUp = -1f, screenLeft = 0f;
        LevelChaseHold? hold = null;
        for (int i = 0; i < 240; i++)
        {
            var nose = -plant.Attitude.Z;
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
            float now = HeadingDeg(plant);
            turned += Mathf.Wrap(now - heading, -180f, 180f);
            heading = now;
            highestUp = Mathf.Max(highestUp, plant.Attitude.Y.Y);

            // The seat's own chase frame: how far the nose swung toward the screen's left, radians.
            var view = LevelChase.Step(plant.Attitude, hold, Dt, out var next);
            hold = next;
            screenLeft -= (-plant.Attitude.Z - nose).Dot(view.X);
        }

        Assert.True(side * turned < -40f, $"[{plane}] stick {side} turns {turned} degrees in 4 s, world left positive");
        Assert.True(side * screenLeft > 0.6f, $"[{plane}] stick {side} swings the nose {screenLeft} rad toward the screen's left");
        Assert.True(highestUp < 0f, $"[{plane}] it stays on its back (up axis height at most {highestUp})");
        Assert.True(Mathf.Abs(plant.Position.Y - 1200f) < 30f, $"[{plane}] it holds its height ({plant.Position.Y} m from 1200)");

        stick.Roll = 0f;
        for (int i = 0; i < 180; i++)
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
        Assert.True(plant.Attitude.Y.Y > 0.95f, $"[{plane}] centred, it is upright 3 s later (up axis height {plant.Attitude.Y.Y})");
    }

    /// <summary>A turn with the nose well off level drifts the bank toward knife edge. Held for 6 s
    /// it stays short of it, so neither the law nor the level chase changes sides mid-turn.</summary>
    [ExtractedDataTheory]
    [InlineData("player_bhawk", 60f)]
    [InlineData("player_bhawk", -60f)]
    [InlineData("player_autogyro", 60f)]
    [InlineData("player_autogyro", -60f)]
    public void ASteepHeldTurn_StaysShortOfKnifeEdge(string plane, float noseDeg)
    {
        var stats = PlaneStats.Load(TestZrdr.Root, plane);
        var plant = new FlightModel(stats);
        var attitude = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(noseDeg)).Rotated(Vector3.Up, 0.7f);
        plant.Reset(new Vector3(0f, 800f, 0f), attitude, stats.FdSpeed * 0.8f, 1f);
        var state = default(AssistedState);
        var stick = new FlightInput { Roll = 1f, Throttle = 1f };
        float steepest = 0f;
        for (int i = 0; i < 360; i++)
        {
            plant.Step(AssistedFlight.Steer(stick, plant, ref state, Dt), Dt);
            steepest = Mathf.Max(steepest, Mathf.Abs(Mathf.RadToDeg(Mathf.Atan2(plant.Attitude.X.Y, plant.Attitude.Y.Y))));
            Assert.False(state.Inverted, $"[{plane}] nose at {noseDeg} degrees, the turn changed sides after {(i + 1) * Dt} s");
        }

        Assert.True(steepest < 90f, $"[{plane}] nose at {noseDeg} degrees, the bank reaches {steepest} degrees");
    }

    /// <summary>A hard upright turn sits near 90 degrees of bank. Tipped just past it, the plane
    /// still counts as upright, so easing the stick rolls it back and not onto its back.</summary>
    [Fact]
    public void JustPastKnifeEdge_AnUprightTurnIsNotTakenForInverted()
    {
        var state = default(AssistedState);
        AssistedFlight.Steer(new FlightInput { Roll = 1f }, Banked(80f), ref state, Dt);
        var eased = AssistedFlight.Steer(new FlightInput { Roll = 0.3f }, Banked(100f), ref state, Dt);
        Assert.False(state.Inverted);
        Assert.True(eased.Roll < -0.5f, $"at 100 degrees of bank, part stick rolls back toward upright ({eased.Roll})");

        // The control: the same bank and stick, arrived at from inverted, rolls further over.
        var over = default(AssistedState);
        AssistedFlight.Steer(new FlightInput { Roll = 1f }, Banked(175f), ref over, Dt);
        Assert.True(AssistedFlight.Steer(new FlightInput { Roll = 0.3f }, Banked(100f), ref over, Dt).Roll > 0.5f);
    }

    [Fact]
    public void TheTurnCap_KeepsAFloorWhileThePathSinksBelowTheHeldClimb()
    {
        // Level flight against a held climb of 30 degrees: a sink no turn rate can close.
        var state = new AssistedState { HeldClimb = Mathf.DegToRad(30f), TurnCap = 0.5f, Primed = true };
        var stick = new FlightInput { Roll = 1f };
        var plant = Level();
        for (int i = 0; i < 600; i++)
            AssistedFlight.Steer(stick, plant, ref state, Dt);

        Assert.True(state.TurnCap > 0.1f, $"the cap stops at {state.TurnCap} rad/s");
        Assert.True(AssistedFlight.Steer(stick, plant, ref state, Dt).Roll > 0.5f, "and full stick still banks into the turn");
    }

    // Positive turning left, the plant's yaw sign.
    private static float HeadingDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Atan2(-m.VelocityDir.X, -m.VelocityDir.Z));

    private static float NoseDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-m.Attitude.Z.Y, -1f, 1f)));

    private static float PathDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(m.VelocityDir.Y, -1f, 1f)));

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
