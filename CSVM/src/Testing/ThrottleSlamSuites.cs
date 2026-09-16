using System;
using CSVM.Bindings;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The throttle-slam exhaust smoke as the pilot actually reaches it: the human lever,
/// worked through the keymap, slewing at its own rate into the gate that watches it. The gate
/// compares the throttle between the frames it is driven on, so it can only see a slam when it runs
/// on the clock the lever moves on, the sim step. Driven from a rendered frame instead, half of
/// which carry no sim step at 120 Hz over a 60 Hz tick, it reads the lever flat every other frame
/// and no throttle movement of any size can ever cross the threshold.</summary>
internal static class ThrottleSlamSuites
{
    private const float StepDt = 1f / 60f;

    // High enough over an empty world that nothing the climb steps through resolves a contact.
    private const float SpawnAltitudeM = 800f;

    [Suite("throttle-slam-smoke",
        "the slam smoke on the human throttle path: an idle-to-full slam, commanded through the "
        + "throttle-up control and slewed by the flight step alone, fires the exhaust plume exactly "
        + "once as the climb crosses the threshold, closing the lever again fires nothing, and a "
        + "single one-eighth step (the jump the original's footage shows no plume for) fires "
        + "nothing either")]
    internal static void ThrottleSlamSmokeGate(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");

        var textures = new TextureArchive(texturesPath);
        FlightController? pilot = null;
        try
        {
            pilot = IdlingRig(ctx, GameZ.Load(ctx.PlanesGamezPath), textures);
            if (pilot.ThrottleSmoke is not { } smoke)
            {
                ctx.Check(false, $"{ctx.PlaneName}: the flown airframe carries the exhaust markers and the nitropuff puffers the plume needs");
                return;
            }

            ctx.Check(pilot.Throttle <= 0.001f,
                $"{ctx.PlaneName} sits at idle before the slam, lever={pilot.Throttle:0.000}");

            // Idle to full, as the pilot commands it: the control held down, the lever slewing at
            // its own rate inside the flight step. 150 steps is 2.5 s, past the 2 s full sweep.
            pilot.HoldActionForTest(InputAction.ThrottleUp, true);
            int firstSlam = StepUntilSlam(pilot, smoke, 150);
            ctx.Check(smoke.SlamCountForTest == 1,
                $"{ctx.PlaneName}: the idle-to-full slam fires the plume once, at step {firstSlam} of 150, lever={pilot.Throttle:0.000} (fires={smoke.SlamCountForTest})");

            // Closing the lever again: a fall is not a slam, and the fresh climb it leaves behind
            // must start from where the lever ends up rather than carrying the slam's magnitude.
            pilot.HoldActionForTest(InputAction.ThrottleUp, false);
            pilot.HoldActionForTest(InputAction.ThrottleSet0, true);
            StepUntilSlam(pilot, smoke, 150);
            ctx.Check(smoke.SlamCountForTest == 1 && pilot.Throttle <= 0.001f,
                $"…and closing it back to idle fires nothing more, lever={pilot.Throttle:0.000} (fires={smoke.SlamCountForTest})");

            // The control the footage bounds the gate with: one eighth, the step that shows no
            // plume in CAP-21, commanded from the same idle the slam started at.
            pilot.HoldActionForTest(InputAction.ThrottleSet0, false);
            pilot.HoldActionForTest(InputAction.ThrottleSet1, true);
            StepUntilSlam(pilot, smoke, 60);
            ctx.Check(smoke.SlamCountForTest == 1 && pilot.Throttle > 0.1f,
                $"…while a single one-eighth step fires nothing at all, lever={pilot.Throttle:0.000} (fires={smoke.SlamCountForTest})");

            ctx.Note($"{ctx.PlaneName}: slam at step {firstSlam} ({firstSlam * StepDt:0.00} sim-s into the climb), one eighth on its own at lever {pilot.Throttle:0.000}");
        }
        finally
        {
            pilot?.Free();
            textures.Dispose();
        }
    }

    // Steps the rig and reports the step the plume first fired on, or -1. The rig is stepped and
    // nothing else: a rendered frame is exactly what the gate must not need.
    private static int StepUntilSlam(FlightController pilot, ThrottleSlamSmoke smoke, int steps)
    {
        int before = smoke.SlamCountForTest;
        int at = -1;
        for (int i = 0; i < steps; i++)
        {
            pilot.SimStep(StepDt);
            if (at < 0 && smoke.SlamCountForTest > before)
            {
                at = i;
            }
        }

        return at;
    }

    // A human rig parked at idle with its slam smoke built, assembled the way the flight adapter
    // assembles the flown aircraft: the smoke resolves the airframe's own exhaust markers, so it
    // exists only once the plane model is under the controller and Setup has placed the lever.
    private static FlightController IdlingRig(TestContext ctx, GameZ planesGamez, TextureArchive textures)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var pos = new Vector3(0f, SpawnAltitudeM, 0f);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            PlayerIndex = 0,
            IsHumanPiloted = true,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Name = "ThrottleSlamPilot",
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward,
            spawnThrottle: 0f);
        ctx.Host.AddChild(rig);
        rig.ThrottleSmoke = ThrottleSlamSmoke.Build(model, ctx.ZrdrPath, textures, rig, rig.Throttle);
        return rig;
    }
}
