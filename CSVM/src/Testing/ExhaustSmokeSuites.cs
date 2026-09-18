using System;
using CSVM.Bindings;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The exhaust smoke as the pilot actually reaches it: the human lever, worked through
/// the keymap, slewing at its own rate while the smoke charges from the gap between the commanded
/// lever and the live one. The charge integrates that gap over the flight step, so it measures
/// what it should only on the clock the lever moves on.</summary>
internal static class ExhaustSmokeSuites
{
    private const float StepDt = 1f / 60f;

    // High enough over an empty world that nothing the climb steps through resolves a contact.
    private const float SpawnAltitudeM = 800f;

    // Below this opacity a near-black sprite over sky or sea is not seen. The one-eighth step's
    // decoded peak is 0.013 and the idle-to-full slam's is 0.356, so the bound separates them
    // with a wide margin either side and never decides a borderline case.
    private const float InvisibleOpacity = 0.02f;

    [Suite("exhaust-smoke",
        "the exhaust smoke on the human throttle path: a single one-eighth digit from idle peaks "
        + "far too faint to see, closing the lever charges nothing, and an idle-to-full digit slam "
        + "lights the trail at the decoded strength and puts it out again within four seconds")]
    internal static void ExhaustSmokeCharge(TestContext ctx)
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
            if (pilot.ExhaustSmoke is not { } smoke)
            {
                ctx.Check(false, $"{ctx.PlaneName}: the flown airframe carries the exhaust markers the smoke needs");
                return;
            }

            ctx.Check(pilot.Throttle <= 0.001f,
                $"{ctx.PlaneName} sits at idle before the first move, lever={pilot.Throttle:0.000}");

            // The control the footage bounds the effect with: one eighth, which shows no plume in
            // CAP-21. The trail may technically run for a few steps; what it draws must be unseeable.
            pilot.HoldActionForTest(InputAction.ThrottleSet1, true);
            var eighth = Run(pilot, smoke, 180);
            ctx.Check(eighth.Peak < InvisibleOpacity && !smoke.StreamingForTest,
                $"{ctx.PlaneName}: a single one-eighth step peaks at opacity {eighth.Peak:0.0000} (under {InvisibleOpacity}) and is out 3 s later");

            // Back to idle: a closing lever's gap is negative and charges nothing.
            pilot.HoldActionForTest(InputAction.ThrottleSet1, false);
            pilot.HoldActionForTest(InputAction.ThrottleSet0, true);
            var close = Run(pilot, smoke, 180);
            ctx.Check(!close.Charged && pilot.Throttle <= 0.001f,
                $"…closing it to idle charges nothing, lever={pilot.Throttle:0.000}");

            // The slam: the digit commands full at once and the gap closes over the 2 s slew,
            // which is what the decode charges on.
            pilot.HoldActionForTest(InputAction.ThrottleSet0, false);
            pilot.HoldActionForTest(InputAction.ThrottleSet8, true);
            var slam = Run(pilot, smoke, 360);
            ctx.Check(slam.EverStreamed && slam.Peak is > 0.33f and < 0.38f,
                $"{ctx.PlaneName}: the idle-to-full slam lights the trail at peak intensity {slam.Peak:0.000} (decoded 0.356), from step {slam.FirstOn}");
            ctx.Check(slam.LastOn >= 0 && slam.LastOn * StepDt < 4.1f && !smoke.StreamingForTest,
                $"…and puts it out {slam.LastOn * StepDt:0.00} sim-s after the slam (decoded 3.9)");
            ctx.Note($"{ctx.PlaneName}: one-eighth peak {eighth.Peak:0.0000}, slam peak {slam.Peak:0.000} lit steps {slam.FirstOn}..{slam.LastOn}");
        }
        finally
        {
            pilot?.Free();
            textures.Dispose();
        }
    }

    // Steps the rig and records what the smoke did. The rig is stepped and nothing else: a
    // rendered frame is exactly what the charge must not need.
    private static (float Peak, bool Charged, bool EverStreamed, int FirstOn, int LastOn) Run(
        FlightController pilot, ExhaustSmoke smoke, int steps)
    {
        float peak = 0f;
        bool charged = false;
        int first = -1, last = -1;
        for (int i = 0; i < steps; i++)
        {
            float before = smoke.IntensityForTest;
            pilot.SimStep(StepDt);
            charged |= smoke.IntensityForTest > before;
            peak = Math.Max(peak, smoke.IntensityForTest);
            if (smoke.StreamingForTest)
            {
                if (first < 0)
                    first = i;
                last = i;
            }
        }

        return (peak, charged, first >= 0, first, last);
    }

    // A human rig parked at idle with its exhaust smoke built, assembled the way the flight
    // adapter assembles the flown aircraft: the smoke resolves the airframe's own exhaust markers,
    // so it exists only once the plane model is under the controller.
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
            Name = "ExhaustSmokePilot",
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward,
            spawnThrottle: 0f);
        ctx.Host.AddChild(rig);
        rig.ExhaustSmoke = ExhaustSmoke.Build(model, textures, rig);
        return rig;
    }
}
