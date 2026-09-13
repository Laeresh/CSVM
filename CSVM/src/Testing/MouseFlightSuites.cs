using System;
using CSVM.Bindings;
using CSVM.Flight;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The mouse flight-control scheme through the seat's own stick reader: the decoded deadzone, the
/// autogyro exchange over the shipped defs' own flag, the held free-look control that hands the
/// mouse to head-look for as long as it is down, and the keyboard scheme underneath, which the
/// mouse must leave exactly as it is.
/// </summary>
internal static class MouseFlightSuites
{
    private const float Dt = 1f / 60f;

    // High over an empty world, so nothing the rig spawns into resolves a ground contact.
    private const float SpawnAltitudeM = 800f;

    [Suite("flight-mouse-scheme",
        "the mouse flight-control scheme over the shipped defs: the Hoplite authors is_autogyro and "
        + "the Bloodhawk does not, sideways mouse motion banks the aeroplane where it yaws the "
        + "autogyro, both off the same reader and both past the decoded deadzone that leaves a "
        + "cursor near the middle flying nothing, an ordinary quarter-pane offset yaws and pitches "
        + "the autogyro into the plant where it banks the aeroplane, holding the free-look control "
        + "takes the stick off "
        + "the mouse for exactly as long as it is held and releasing it hands the mouse straight "
        + "back, the keys sum into the mouse's own deflections rather than being replaced by them, "
        + "and a seat on the keyboard scheme reads no stick from the mouse at all")]
    internal static void FlightMouseScheme(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var gyroStats = PlaneStats.Load(ctx.ZrdrPath, "player_autogyro");
        var planeStats = PlaneStats.Load(ctx.ZrdrPath, "player_bhawk");
        ctx.Check(gyroStats.IsAutogyro && !planeStats.IsAutogyro,
            $"ABLE-TO-FAIL CONTROL: the shipped defs author is_autogyro on the Hoplite alone (gyro {gyroStats.IsAutogyro}, aeroplane {planeStats.IsAutogyro})");

        var gyro = Rig(ctx, gyroStats, "MouseFlightGyro");
        var plane = Rig(ctx, planeStats, "MouseFlightPlane");
        try
        {
            gyro.MouseFlying = true;
            plane.MouseFlying = true;

            // Inside the deadzone the cursor flies nothing at all, which is what lets a player let
            // go of the mouse without the aeroplane holding a deflection.
            plane.MouseStickForTest = new Vector2(0.09f, 0.09f);
            var idle = plane.ReadKeyboard(Dt);
            ctx.Check(idle.Roll == 0f && idle.Pitch == 0f && idle.Yaw == 0f,
                $"a cursor inside the deadzone flies nothing (roll {idle.Roll:0.###}, pitch {idle.Pitch:0.###}, yaw {idle.Yaw:0.###})");

            plane.MouseStickForTest = new Vector2(0.8f, 0f);
            gyro.MouseStickForTest = new Vector2(0.8f, 0f);
            var planeRight = plane.ReadKeyboard(Dt);
            var gyroRight = gyro.ReadKeyboard(Dt);
            ctx.Check(planeRight.Roll < -0.7f && planeRight.Yaw == 0f,
                $"sideways mouse motion banks the aeroplane and yaws it nowhere (roll {planeRight.Roll:0.###}, yaw {planeRight.Yaw:0.###})");
            ctx.Check(gyroRight.Yaw < -0.7f && gyroRight.Roll == 0f,
                $"and yaws the autogyro and banks it nowhere, the exchange the decode names (yaw {gyroRight.Yaw:0.###}, roll {gyroRight.Roll:0.###})");

            plane.MouseStickForTest = new Vector2(0f, 0.6f);
            var pull = plane.ReadKeyboard(Dt);
            ctx.Check(pull.Pitch > 0.5f && pull.Roll == 0f,
                $"a cursor below the middle pulls the nose up on either airframe (pitch {pull.Pitch:0.###})");

            FreeLookHold(ctx, plane);
            // Before KeysSum, which leaves the autogyro's roll keys ramped up for its own reading.
            AutogyroFliesAnOrdinaryOffset(ctx, gyro, gyroStats, plane, planeStats);
            KeysSum(ctx, gyro);
            KeyboardScheme(ctx, plane);
        }
        finally
        {
            gyro.QueueFree();
            plane.QueueFree();
        }

        ctx.Note($"the mouse flies the aeroplane through the seat's own stick reader, free-look and all");
    }

    // Hold-to-look, the posture under both mouse schemes: while the free-look control is down the
    // mouse is the head's and the stick reads nothing from it, and the frame it comes up the stick
    // has it back. A hold rather than a toggle, so one tap cannot strand the mouse on the head.
    private static void FreeLookHold(TestContext ctx, FlightController plane)
    {
        plane.MouseStickForTest = new Vector2(0.8f, 0f);
        plane.HoldActionForTest(InputAction.FreeLook, true);
        var looking = plane.ReadKeyboard(Dt);
        ctx.Check(plane.FreeLookActiveForTest() && looking.Roll == 0f,
            $"a held free-look control takes the stick off the mouse (held {plane.FreeLookActiveForTest()}, roll {looking.Roll:0.###})");

        plane.HoldActionForTest(InputAction.FreeLook, false);
        var released = plane.ReadKeyboard(Dt);
        ctx.Check(!plane.FreeLookActiveForTest() && released.Roll < -0.7f,
            $"the release hands the mouse straight back to the stick (held {plane.FreeLookActiveForTest()}, roll {released.Roll:0.###})");

        // The reading a toggle could not give: a second press takes the mouse off the stick AGAIN
        // rather than handing it back, which is what the toggle did on its own second press.
        plane.HoldActionForTest(InputAction.FreeLook, true);
        var again = plane.ReadKeyboard(Dt);
        ctx.Check(plane.FreeLookActiveForTest() && again.Roll == 0f,
            $"and a second hold takes it off once more, no toggle underneath (held {plane.FreeLookActiveForTest()}, roll {again.Roll:0.###})");
        plane.HoldActionForTest(InputAction.FreeLook, false);
    }

    // The keys are not replaced by the mouse, they sum with it, which is what the original's arm
    // does to the same slots: an autogyro pilot banks with the roll keys while the mouse yaws.
    private static void KeysSum(TestContext ctx, FlightController gyro)
    {
        gyro.MouseStickForTest = new Vector2(0.8f, 0f);
        var summed = default(FlightInput);
        for (int i = 0; i < 120; i++)
        {
            gyro.HoldActionForTest(InputAction.RollLeft, true);
            summed = gyro.ReadKeyboard(Dt);
        }

        ctx.Check(summed.Roll > 0.5f && summed.Yaw < -0.7f,
            $"the roll keys bank the autogyro while the mouse yaws it (roll {summed.Roll:0.###}, yaw {summed.Yaw:0.###})");
        gyro.HoldActionForTest(InputAction.RollLeft, false);
    }

    // The control the whole feature has to leave alone: a seat that did not choose the scheme reads
    // no stick from the mouse at all, whether the free-look control is down or up, and that control
    // is the same hold head-look has always read.
    private static void KeyboardScheme(TestContext ctx, FlightController plane)
    {
        plane.MouseFlying = false;
        plane.MouseStickForTest = new Vector2(0.8f, 0.8f);
        plane.HoldActionForTest(InputAction.FreeLook, true);
        var off = plane.ReadKeyboard(Dt);
        ctx.Check(off.Roll == 0f && off.Pitch == 0f && off.Yaw == 0f,
            $"the keyboard scheme takes no stick from the mouse (roll {off.Roll:0.###}, pitch {off.Pitch:0.###}, yaw {off.Yaw:0.###})");
        ctx.Check(plane.FreeLookActiveForTest(),
            $"and reads the same held free-look posture head-look has always read ({plane.FreeLookActiveForTest()})");

        plane.HoldActionForTest(InputAction.FreeLook, false);
        var up = plane.ReadKeyboard(Dt);
        ctx.Check(!plane.FreeLookActiveForTest() && up.Roll == 0f && up.Pitch == 0f,
            $"and releasing it leaves the stick on the keys, not on the cursor (roll {up.Roll:0.###}, pitch {up.Pitch:0.###})");
    }

    // The reading the complaint at the controls was about: an ordinary cursor offset, a quarter of
    // the way across the pane, flown into the real plant for three seconds. It sits inside the third
    // axis's 0.3 dead band, where the autogyro used to read roll 0, pitch 0, yaw 0 and hold heading
    // 0.0 while the same cursor banked the aeroplane 84.5 degrees over. The aeroplane is the control
    // on both halves: its own lateral axis still answers, and its yaw, which is still fed by the
    // absent third axis, still reads exactly zero.
    private static void AutogyroFliesAnOrdinaryOffset(
        TestContext ctx, FlightController gyro, PlaneStats gyroStats, FlightController plane, PlaneStats planeStats)
    {
        var cursor = new Vector2(0.25f, 0.25f);
        gyro.MouseStickForTest = cursor;
        plane.MouseStickForTest = cursor;
        var gyroStick = gyro.ReadKeyboard(Dt);
        var planeStick = plane.ReadKeyboard(Dt);
        ctx.Check(gyroStick.Yaw < -0.15f && gyroStick.Pitch > 0.15f && gyroStick.Roll == 0f,
            $"a quarter of the way across the pane yaws and pitches the autogyro and leaves its roll to the keys (yaw {gyroStick.Yaw:0.###}, pitch {gyroStick.Pitch:0.###}, roll {gyroStick.Roll:0.###})");
        ctx.Check(planeStick.Roll == gyroStick.Yaw && planeStick.Yaw == 0f,
            $"ABLE-TO-FAIL CONTROL: the same offset reaches the aeroplane's bank by the same amount and its yaw not at all (roll {planeStick.Roll:0.###}, yaw {planeStick.Yaw:0.###})");

        float gyroHeading = FlownHeadingDeg(gyro, gyroStats, out float gyroPitch);
        float planeHeading = FlownHeadingDeg(plane, planeStats, out _);
        ctx.Check(Mathf.Abs(gyroHeading) > 10f && gyroPitch > 10f,
            $"and the plant flies it: the autogyro's nose comes {gyroHeading:0.0} degrees round and {gyroPitch:0.0} degrees up in three seconds");
        ctx.Check(Mathf.Abs(planeHeading) > 1f,
            $"the aeroplane the same cursor already flew is unchanged by this ({planeHeading:0.0} degrees of heading)");

        gyro.MouseStickForTest = null;
        plane.MouseStickForTest = null;
    }

    // Three seconds of the seat's own stick into a throwaway plant on that airframe's shipped stats,
    // reported as the heading it came round to and the pitch it climbed to. It is a plant rather
    // than the rig's own model because what is under test is the deflection, not the seat's tick.
    private static float FlownHeadingDeg(FlightController rig, PlaneStats stats, out float pitchDeg)
    {
        var model = new FlightModel(stats);
        model.Reset(new Vector3(0f, SpawnAltitudeM, 0f), Basis.Identity, 60f, 0.85f);
        for (int i = 0; i < 180; i++)
        {
            var input = rig.ReadKeyboard(Dt);
            input.Throttle = 0.85f;
            model.Step(input, Dt);
        }

        var nose = -model.Attitude.Z;
        pitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(nose.Y, -1f, 1f)));
        return Mathf.RadToDeg(Mathf.Atan2(-nose.X, -nose.Z));
    }

    // One flying seat over a plant and nothing else: no model to draw, no camera and no HUD, since
    // every reading here comes out of the stick reader rather than off the screen.
    private static FlightController Rig(TestContext ctx, PlaneStats stats, string name)
    {
        var model = new Node3D { Name = name + "Model" };
        var rig = new FlightController
        {
            PlaneModel = model,
            PlayerIndex = 0,
            IsHumanPiloted = false,
            UseKeyboard = true,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Name = name,
        };
        rig.AddChild(model);
        var spawn = new Vector3(0f, SpawnAltitudeM, 0f);
        rig.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
        ctx.Host.AddChild(rig);
        return rig;
    }
}
