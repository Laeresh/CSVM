using System;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using Godot;

namespace CSVM.Testing;

/// <summary>The Assisted control law flown through a real seat's sim step on the shipped airframes.
/// Each step the law's output is the controller's stick, and the result is read off the plant. The
/// seat suite then flies the scheme through the seat's own device reader.</summary>
internal static class AssistedFlightSuites
{
    private const float Dt = 1f / 60f;

    // High over an empty world, so a loop's bottom stays far above the under-map backstop (INSTR-22).
    private const float SpawnAltitudeM = 1500f;
    private const float AltitudeFloorM = 500f;

    private const float TurnSeconds = 12f;
    private const float TurnHeightLossM = 10f;
    private const float ControlHeightLossM = 40f;
    private const float SteadyTurnSpread = 0.1f;
    private const float LevelBankDeg = 5f;
    private const float LevelWithinS = 2f;
    private const float UprightWithinS = 4f;
    private const float LoopWithinS = 20f;

    [Suite("flight-assisted-law",
        "the Assisted control law flown into a real seat's plant on the Bloodhawk and the Hoplite, "
        + "heading off-axis: a held full-left stick settles into a steady left turn at full pull "
        + "that loses under 10 m in 12 s where the same stick flown raw falls "
        + "past 40 m, centring the stick levels the wings inside 2 s and keeps them there, a held "
        + "pull flies a whole loop back to level on the entry heading, and a pull released over the "
        + "top rolls upright on the reversed heading inside 4 s")]
    internal static void FlightAssistedLaw(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequirePlane("player_bhawk", "player_autogyro");
        foreach (string plane in new[] { "player_bhawk", "player_autogyro" })
        {
            var stats = PlaneStats.Load(ctx.Zrdr, plane);
            HeldTurnAndRelease(ctx, stats, plane);
            Loop(ctx, stats, plane);
            Immelmann(ctx, stats, plane);
        }

        ctx.Note($"the law's deflections fly the decoded plant; the feel of them is the user's to judge (B14)");
    }

    [Suite("flight-assisted-seat",
        "the Assisted scheme wired into a seat's own stick reader: a held roll-left key reads "
        + "straight through under Original and through the law under Assisted, the switch and a "
        + "respawn hand the law a fresh state while an accept that keeps the scheme does not, an AI "
        + "seat on Assisted reads the raw stick and never primes the law, and the Hoplite under "
        + "Assisted turns left on the left key and on a cursor left of centre, banking into the "
        + "turn because the cursor reaches the law before the autogyro exchange")]
    internal static void FlightAssistedSeat(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequirePlane("player_bhawk", "player_autogyro");
        var plane = PlaneStats.Load(ctx.Zrdr, "player_bhawk");
        var gyro = PlaneStats.Load(ctx.Zrdr, "player_autogyro");
        SchemesDiffer(ctx, plane);
        AiSeatStaysRaw(ctx, plane);
        AutogyroTurnsLeft(ctx, gyro, mouse: false);
        AutogyroTurnsLeft(ctx, gyro, mouse: true);
        ctx.Note($"one seam, ReadKeyboard's return, carries every device through the law on a person's Assisted seat");
    }

    private static void HeldTurnAndRelease(TestContext ctx, PlaneStats stats, string plane)
    {
        // The control: the same full-left stick flown raw, as the Original scheme reads it.
        var raw = FlyTurn(ctx, stats, raw: true);
        raw.Rig.Seat.Free();
        var turn = FlyTurn(ctx, stats, raw: false);
        var rig = turn.Rig;
        try
        {
            ctx.Check(raw.HeightLost > ControlHeightLossM,
                $"ABLE-TO-FAIL CONTROL: [{plane}] the same full-left stick flown raw loses {raw.HeightLost:0.0} m in {TurnSeconds:0} s");
            ctx.Check(turn.HeightLost < TurnHeightLossM,
                $"[{plane}] through the assist, the held full-left turn loses {turn.HeightLost:0.0} m in {TurnSeconds:0} s");
            float spread = Mathf.Abs(turn.LateRate - turn.EarlyRate) / Mathf.Max(Mathf.Abs(turn.LateRate), 1e-3f);
            ctx.Check(turn.LateRate > 0f && spread < SteadyTurnSpread,
                $"[{plane}] it settles into a steady left turn: {turn.LateRate:0.0} deg/s over the last 4 s against {turn.EarlyRate:0.0} before");
            ctx.Check(turn.LatePull > 0.95f,
                $"[{plane}] at full pull, the hardest turn it holds: mean elevator {turn.LatePull:0.000} over the last 4 s");
            ctx.Check(turn.Lowest > AltitudeFloorM, $"[{plane}] the leg stays above {AltitudeFloorM:0} m ({turn.Lowest:0} m)");

            // Centred: the wings come level and stay there.
            rig.Stick.Stick = new FlightInput { Throttle = 1f };
            float levelAt = -1f;
            float worstAfter = 0f;
            for (int i = 0; i < 300; i++)
            {
                rig.Seat.SimStep(Dt);
                float bank = Mathf.Abs(BankDeg(rig.Plant));
                if (levelAt < 0f && bank < LevelBankDeg)
                    levelAt = (i + 1) * Dt;
                else if (levelAt >= 0f)
                    worstAfter = Mathf.Max(worstAfter, bank);
            }

            ctx.Check(levelAt >= 0f && levelAt < LevelWithinS && worstAfter < LevelBankDeg,
                $"[{plane}] centring the stick levels the wings in {levelAt:0.00} s and holds them within {worstAfter:0.0} degrees after");
            ctx.Note($"[{plane}] full-left turn: {turn.HeightLost:0.0} m lost in {TurnSeconds:0} s (raw stick {raw.HeightLost:0.0} m), {turn.LateRate:0.0} deg/s late against {turn.EarlyRate:0.0}, elevator {turn.LatePull:0.000}, bank {turn.Bank:0.0} degrees, {turn.Speed:0.0} m/s; level {levelAt:0.00} s after release, worst {worstAfter:0.0} degrees after");
        }
        finally
        {
            rig.Seat.Free();
        }
    }

    // Twelve seconds of full left stick, read over its last eight seconds in two halves.
    private static TurnLeg FlyTurn(TestContext ctx, PlaneStats stats, bool raw)
    {
        var rig = Build(ctx, stats);
        rig.Stick.Stick = new FlightInput { Roll = 1f, Throttle = 1f };
        rig.Stick.Raw = raw;
        float start = rig.Plant.Position.Y;
        int steps = Mathf.RoundToInt(TurnSeconds / Dt);
        const int Window = 240;
        float turnedEarly = 0f, turnedLate = 0f, pullLate = 0f, lowest = start;
        var heading = HeadingDeg(rig.Plant);
        for (int i = 0; i < steps; i++)
        {
            rig.Seat.SimStep(Dt);
            float now = HeadingDeg(rig.Plant);
            float turned = Mathf.Wrap(now - heading, -180f, 180f);
            heading = now;
            lowest = Mathf.Min(lowest, rig.Plant.Position.Y);
            if (i >= steps - Window)
            {
                turnedLate += turned;
                pullLate += rig.Seat.LastCommand.Pitch / Window;
            }
            else if (i >= steps - (2 * Window))
            {
                turnedEarly += turned;
            }
        }

        float seconds = Window * Dt;
        return new TurnLeg(rig, start - lowest, lowest, turnedEarly / seconds, turnedLate / seconds, pullLate,
            BankDeg(rig.Plant), rig.Plant.Speed);
    }

    // A held pull flies the whole loop: past vertical, over the top inverted, and back to level
    // upright on the heading it entered on.
    private static void Loop(TestContext ctx, PlaneStats stats, string plane)
    {
        var rig = Build(ctx, stats);
        try
        {
            rig.Stick.Stick = new FlightInput { Pitch = 1f, Throttle = 1f };
            float entry = HeadingDeg(rig.Plant);
            bool vertical = false, inverted = false;
            float doneAt = -1f, lowest = rig.Plant.Position.Y;
            for (int i = 0; i < Mathf.RoundToInt(LoopWithinS / Dt) && doneAt < 0f; i++)
            {
                rig.Seat.SimStep(Dt);
                lowest = Mathf.Min(lowest, rig.Plant.Position.Y);
                vertical |= ClimbDeg(rig.Plant) > 80f;
                inverted |= vertical && rig.Plant.Attitude.Y.Y < -0.9f;
                if (inverted && rig.Plant.Attitude.Y.Y > 0.9f && ClimbDeg(rig.Plant) > -10f)
                    doneAt = (i + 1) * Dt;
            }

            float off = Mathf.Abs(Mathf.Wrap(HeadingDeg(rig.Plant) - entry, -180f, 180f));
            ctx.Check(doneAt > 0f && off < 20f,
                $"[{plane}] a held pull loops back to level upright in {doneAt:0.0} s, {off:0.0} degrees off the entry heading (vertical {vertical}, inverted {inverted})");
            ctx.Check(lowest > AltitudeFloorM, $"[{plane}] the loop stays above {AltitudeFloorM:0} m ({lowest:0} m)");
            ctx.Note($"[{plane}] held pull: loop closed in {doneAt:0.0} s, {off:0.0} degrees off the entry heading, lowest {lowest:0} m");
        }
        finally
        {
            rig.Seat.Free();
        }
    }

    // Half a loop, released over the top: the plane rolls upright on the reversed heading.
    private static void Immelmann(TestContext ctx, PlaneStats stats, string plane)
    {
        var rig = Build(ctx, stats);
        try
        {
            rig.Stick.Stick = new FlightInput { Pitch = 1f, Throttle = 1f };
            float entry = HeadingDeg(rig.Plant);
            bool vertical = false;
            for (int i = 0; i < Mathf.RoundToInt(LoopWithinS / Dt); i++)
            {
                rig.Seat.SimStep(Dt);
                vertical |= ClimbDeg(rig.Plant) > 80f;
                if (vertical && rig.Plant.Attitude.Y.Y < -0.95f && Mathf.Abs(ClimbDeg(rig.Plant)) < 15f)
                    break;
            }

            bool overTop = rig.Plant.Attitude.Y.Y < -0.95f;
            rig.Stick.Stick = new FlightInput { Throttle = 1f };
            float uprightAt = -1f;
            for (int i = 0; i < Mathf.RoundToInt(UprightWithinS / Dt) && uprightAt < 0f; i++)
            {
                rig.Seat.SimStep(Dt);
                if (rig.Plant.Attitude.Y.Y > 0.95f && Mathf.Abs(BankDeg(rig.Plant)) < LevelBankDeg)
                    uprightAt = (i + 1) * Dt;
            }

            float reversal = Mathf.Abs(Mathf.Wrap(HeadingDeg(rig.Plant) - entry, -180f, 180f));
            ctx.Check(overTop && uprightAt > 0f && reversal > 160f,
                $"[{plane}] a pull released inverted over the top rolls upright in {uprightAt:0.00} s, {reversal:0.0} degrees round from the entry (over the top {overTop})");
            ctx.Note($"[{plane}] released over the top: upright in {uprightAt:0.00} s, {reversal:0.0} degrees round from the entry");
        }
        finally
        {
            rig.Seat.Free();
        }
    }

    private static void SchemesDiffer(TestContext ctx, PlaneStats stats)
    {
        var seat = Place(ctx, new FlightModel(stats), null, human: true);
        try
        {
            var raw = HoldLeft(seat, 30);
            ctx.Check(raw.Roll == 1f && raw.Yaw == 0f && raw.Pitch == 0f,
                $"ABLE-TO-FAIL CONTROL: under Original a held roll-left key reads straight through (roll {raw.Roll:0.###}, yaw {raw.Yaw:0.###}, pitch {raw.Pitch:0.###})");

            seat.ControlScheme = ControlScheme.Assisted;
            ctx.Check(!seat.AssistedStateForTest.Primed, $"the switch to Assisted hands the law a fresh state");
            var assisted = HoldLeft(seat, 1);
            ctx.Check(assisted.Yaw > 0.1f && seat.AssistedStateForTest.Primed,
                $"under Assisted the same key reads through the law, which adds the turn's rudder (yaw {assisted.Yaw:0.###}, roll {assisted.Roll:0.###})");

            seat.ControlScheme = ControlScheme.Original;
            var back = HoldLeft(seat, 1);
            ctx.Check(!seat.AssistedStateForTest.Primed && back.Yaw == 0f && back.Roll == 1f,
                $"switching back resets the state and reads the key raw again (yaw {back.Yaw:0.###})");

            // The pause CONTROLS page reaches a seat through ApplyProfile.
            var profile = BindingProfile.Defaults(default, true);
            profile.ControlScheme = ControlScheme.Assisted;
            seat.ApplyProfile(profile);
            HoldLeft(seat, 1);
            ctx.Check(seat.ControlScheme == ControlScheme.Assisted && seat.AssistedStateForTest.Primed,
                $"an accepted profile puts the seat on Assisted ({seat.ControlScheme})");
            seat.ApplyProfile(profile);
            ctx.Check(seat.AssistedStateForTest.Primed, $"an accept that keeps the scheme keeps the law's state");
            seat.Respawn();
            ctx.Check(!seat.AssistedStateForTest.Primed, $"a respawn hands the law a fresh state");
        }
        finally
        {
            seat.HoldActionForTest(InputAction.RollLeft, false);
            seat.Free();
        }
    }

    private static void AiSeatStaysRaw(TestContext ctx, PlaneStats stats)
    {
        var seat = Place(ctx, new FlightModel(stats), null, human: false);
        try
        {
            seat.ControlScheme = ControlScheme.Assisted;
            var read = HoldLeft(seat, 30);
            ctx.Check(read.Roll == 1f && read.Yaw == 0f && !seat.AssistedStateForTest.Primed,
                $"an AI seat on Assisted reads the raw stick and never primes the law (roll {read.Roll:0.###}, yaw {read.Yaw:0.###})");
        }
        finally
        {
            seat.HoldActionForTest(InputAction.RollLeft, false);
            seat.Free();
        }
    }

    // Three seconds of left stick, the key or a cursor left of centre, flown through the seat.
    private static void AutogyroTurnsLeft(TestContext ctx, PlaneStats stats, bool mouse)
    {
        var plant = new FlightModel(stats);
        var seat = Place(ctx, plant, null, human: true);
        string device = mouse ? "cursor" : "key";
        try
        {
            seat.ControlScheme = ControlScheme.Assisted;
            if (mouse)
            {
                seat.MouseFlying = true;
                seat.Mouse.StickForTest = new Vector2(-0.8f, 0f);
                var cursor = seat.MouseFlightRead();
                seat.ControlScheme = ControlScheme.Original;
                var exchanged = seat.MouseFlightRead();
                seat.ControlScheme = ControlScheme.Assisted;
                ctx.Check(exchanged.Yaw > 0.7f && exchanged.Roll == 0f,
                    $"ABLE-TO-FAIL CONTROL: under Original the cursor yaws the Hoplite, the decoded exchange (yaw {exchanged.Yaw:0.###}, roll {exchanged.Roll:0.###})");
                ctx.Check(cursor.Roll > 0.7f && cursor.Yaw == 0f,
                    $"under Assisted the law reads the cursor as stick, un-exchanged (roll {cursor.Roll:0.###}, yaw {cursor.Yaw:0.###})");
            }

            float entry = HeadingDeg(plant);
            float bank = 0f;
            for (int i = 0; i < Mathf.RoundToInt(3f / Dt); i++)
            {
                if (!mouse)
                    seat.HoldActionForTest(InputAction.RollLeft, true);
                seat.SimStep(Dt);
                bank = Mathf.Max(bank, BankDeg(plant));
            }

            float turned = Mathf.Wrap(HeadingDeg(plant) - entry, -180f, 180f);
            ctx.Check(turned > 20f && bank > 10f,
                $"[player_autogyro] left {device} under Assisted turns {turned:0.0} degrees left in 3 s, banked up to {bank:0.0} degrees left");
            ctx.Note($"[player_autogyro] left {device}: {turned:0.0} degrees left in 3 s, bank up to {bank:0.0} degrees");
        }
        finally
        {
            seat.HoldActionForTest(InputAction.RollLeft, false);
            seat.Mouse.StickForTest = null;
            seat.Free();
        }
    }

    // The seat's own read after `steps` ticks of the roll-left key, the ramp reaching full at 24.
    private static FlightInput HoldLeft(FlightController seat, int steps)
    {
        var read = default(FlightInput);
        for (int i = 0; i < steps; i++)
        {
            seat.HoldActionForTest(InputAction.RollLeft, true);
            read = seat.ReadKeyboard(Dt);
        }

        return read;
    }

    private static Rig Build(TestContext ctx, PlaneStats stats)
    {
        var plant = new FlightModel(stats);
        var stick = new AssistedStick(plant);
        return new Rig(Place(ctx, plant, stick, human: true), plant, stick);
    }

    // A seat over `plant`, flown by `source` or, with none, by its own devices.
    private static FlightController Place(TestContext ctx, FlightModel plant, IFlightInputSource? source, bool human)
    {
        var seat = new FlightController();
        seat.Bind(new FlightControllerBuild
        {
            PlaneModel = new Node3D { Name = "AssistedLawModel" },
            PlayerIndex = 0,
            IsHumanPiloted = human,
            InputSource = source,
            UseKeyboard = source == null,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
        });

        // Off the identity heading (DIAG-19), level, at four fifths of the airframe's top speed.
        var spawn = new Vector3(0f, SpawnAltitudeM, 0f);
        seat.Setup(plant, null, new CamParams(), spawn, spawn + new Vector3(1f, 0f, -1f), 1f, plant.Stats.FdSpeed * 0.8f);
        ctx.Host.AddChild(seat);
        return seat;
    }

    private static float BankDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Atan2(m.Attitude.X.Y, m.Attitude.Y.Y));

    private static float ClimbDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(m.VelocityDir.Y, -1f, 1f)));

    // Positive turning left, the plant's yaw sign.
    private static float HeadingDeg(FlightModel m) => Mathf.RadToDeg(Mathf.Atan2(-m.VelocityDir.X, -m.VelocityDir.Z));

    private sealed record Rig(FlightController Seat, FlightModel Plant, AssistedStick Stick);

    private sealed record TurnLeg(Rig Rig, float HeightLost, float Lowest, float EarlyRate, float LateRate, float LatePull,
        float Bank, float Speed);

    // The seat's stick: the pilot's input through the law, read against the seat's own plant.
    private sealed class AssistedStick : IFlightInputSource
    {
        private readonly FlightModel _plant;
        private AssistedState _state;

        public AssistedStick(FlightModel plant) => _plant = plant;

        public FlightInput Stick { get; set; }

        public bool Raw { get; set; }

        public FlightInput Read(float dt)
        {
            return Raw ? Stick : AssistedFlight.Steer(Stick, _plant, ref _state, dt);
        }
    }
}
