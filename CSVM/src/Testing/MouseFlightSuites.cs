using System;
using CSVM.Bindings;
using CSVM.Flight;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The mouse flight-control scheme through the seat's own stick reader: the decoded deadzone, the
/// autogyro exchange over the shipped defs' own flag, the free-look flag that hands the mouse back
/// to head-look, and the keyboard scheme underneath, which the mouse must leave exactly as it is.
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
        + "cursor near the middle flying nothing, the free-look control toggles a flag that takes "
        + "the stick off the mouse and a second press gives it back, the keys sum into the mouse's "
        + "own deflections rather than being replaced by them, and a seat on the keyboard scheme "
        + "reads no stick from the mouse and leaves the flag down for head-look")]
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

            FreeLookFlag(ctx, plane);
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

    // The flag the free-look control toggles: while the mouse flies, one press takes the stick off
    // it and the next gives it back. A toggle rather than a hold, since a player looking around
    // would otherwise have to hold a button through the whole look.
    private static void FreeLookFlag(TestContext ctx, FlightController plane)
    {
        plane.MouseStickForTest = new Vector2(0.8f, 0f);
        plane.HoldActionForTest(InputAction.FreeLook, true);
        var looking = plane.ReadKeyboard(Dt);
        ctx.Check(plane.FreeLookActiveForTest() && looking.Roll == 0f,
            $"a press on free-look takes the stick off the mouse (flag {plane.FreeLookActiveForTest()}, roll {looking.Roll:0.###})");

        plane.HoldActionForTest(InputAction.FreeLook, false);
        var released = plane.ReadKeyboard(Dt);
        ctx.Check(plane.FreeLookActiveForTest() && released.Roll == 0f,
            $"the release leaves it there, the flag being a toggle and not a hold (flag {plane.FreeLookActiveForTest()})");

        plane.HoldActionForTest(InputAction.FreeLook, true);
        var back = plane.ReadKeyboard(Dt);
        ctx.Check(!plane.FreeLookActiveForTest() && back.Roll < -0.7f,
            $"and the next press hands the mouse back to the stick (flag {plane.FreeLookActiveForTest()}, roll {back.Roll:0.###})");
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
    // no stick from the mouse, and its free-look control never raises the flag, so head-look keeps
    // reading the control itself exactly as it did.
    private static void KeyboardScheme(TestContext ctx, FlightController plane)
    {
        plane.MouseFlying = false;
        plane.MouseStickForTest = new Vector2(0.8f, 0.8f);
        plane.HoldActionForTest(InputAction.FreeLook, true);
        var off = plane.ReadKeyboard(Dt);
        ctx.Check(off.Roll == 0f && off.Pitch == 0f && off.Yaw == 0f,
            $"the keyboard scheme takes no stick from the mouse (roll {off.Roll:0.###}, pitch {off.Pitch:0.###}, yaw {off.Yaw:0.###})");
        ctx.Check(!plane.FreeLookActiveForTest(),
            $"and its free-look control leaves the flag down, head-look reading the control itself ({plane.FreeLookActiveForTest()})");
        plane.HoldActionForTest(InputAction.FreeLook, false);
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
