using System;
using CSVM.Bindings;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>What the flight side does with a button at the two moments a session hands input over:
/// the frame a cutscene skip gives flight back, and the frame a respawn is asked for. Both are
/// level reads on a fixed tick, so a button that was already down when flight resumed reads as a
/// fresh pull unless something swallows it, and a respawn button read while the aeroplane is still
/// flying is a free repair wherever the mission counts.</summary>
internal static class FlightInputHandoffSuites
{
    private const float StepDt = 1f / 60f;

    // Where the respawn rig flies: high enough over an empty world that nothing it steps through
    // resolves a ground contact.
    private const float SpawnAltitudeM = 800f;

    [Suite("flight-input-handback",
        "the button that ended a mission intro does not also act on the flight it hands back: the "
        + "intro's out-of-flight code takes the player out of flight, its hold code arms the skip, "
        + "and the press that takes that skip reads the rocket trigger as consumed for as long as "
        + "it stays down, through its release, and reads the next real pull through -- while a "
        + "hand-back with nothing down costs the pull after it nothing")]
    internal static void FlightInputHandback(TestContext ctx)
    {
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        var pilot = new FlightController
        {
            PlayerIndex = 0,
            IsHumanPiloted = true,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Name = "InputHandbackPilot",
        };
        try
        {
            // The control: nothing has handed this aeroplane its input back, so every pull fires.
            ctx.Check(pilot.RocketTriggerReadsForTest(true),
                $"ABLE-TO-FAIL CONTROL: an aeroplane that never left flight reads a pull through");
            ctx.Check(!pilot.RocketTriggerReadsForTest(false) && pilot.RocketTriggerReadsForTest(true),
                $"…and reads the pull after a release through as well");

            var rig = new PlayerRig { Index = 0, Controller = pilot };
            cutscene.BindRigs(new[] { rig }, () => Array.Empty<FlightController>());
            // The shared mission intro, by name, which is what makes this host answer for its codes.
            string intro = CutsceneController.IntroAnims[0];
            Skipped(ctx, cutscene, pilot, intro, "the intro");
            ctx.Check(!pilot.RocketTriggerReadsForTest(true),
                $"the button that took the skip launches nothing on the first frame of flight");
            ctx.Check(!pilot.RocketTriggerReadsForTest(true),
                $"…nor on the frames after it, for as long as it is still down");
            ctx.Check(!pilot.RocketTriggerReadsForTest(false),
                $"…and its release launches nothing either");
            ctx.Check(pilot.RocketTriggerReadsForTest(true),
                $"…while the pull after that fires, so the skip costs the player one press and no more");

            // The other control: a hand-back nobody was holding a button through must not swallow
            // the pull that follows it, which is what makes the assertions above a real gate.
            Skipped(ctx, cutscene, pilot, intro, "a second episode");
            ctx.Check(!pilot.RocketTriggerReadsForTest(false),
                $"a hand-back with the trigger up reads nothing on the frame it happens");
            ctx.Check(pilot.RocketTriggerReadsForTest(true),
                $"…and the pull straight after it fires");
        }
        finally
        {
            pilot.Free();
            cutscene.Free();
        }

        ctx.Note($"the press that skips a mission intro is consumed through its release in flight");
    }

    [Suite("flight-live-respawn-gate",
        "the respawn button on a living aeroplane: pinned the way a campaign mission and Instant "
        + "Action pin it, a held respawn leaves a half-empty tank half empty rather than topping "
        + "it up, the same hold on the same rig unpinned (free flight, the stunt runs, the "
        + "dogfight) respawns and fills it, and the pin leaves the crashed read alone -- a crashed "
        + "pilot holding the button flies again with a full tank")]
    internal static void FlightLiveRespawnGate(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");

        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        FlightController? pilot = null;
        try
        {
            pilot = HumanRig(ctx, GameZ.Load(ctx.PlanesGamezPath), textures, pool);
            float tank = pilot.Fuel.Capacity;
            if (tank <= 0f)
            {
                throw new SuiteSkippedException($"{ctx.PlaneName} authors no fuel tank to read a top-up off");
            }

            ctx.Check(!pilot.Crashed && Mathf.Abs(pilot.Fuel.Remaining - tank) < 0.01f,
                $"{ctx.PlaneName} spawns flying on a full tank ({tank:0.#} units), which a respawn tops up");

            // Pinned, the way a campaign mission and Instant Action pin it.
            pilot.AllowLiveRespawn = false;
            BurnHalf(pilot);
            float burned = pilot.Fuel.Remaining;
            pilot.HoldActionForTest(InputAction.Respawn, true);
            pilot.SimStep(StepDt);
            ctx.Check(pilot.Fuel.Remaining <= burned && !pilot.Crashed,
                $"held on a living aeroplane the pin refuses it: still flying on {pilot.Fuel.Remaining:0.#} of {tank:0.#} units, no top-up off the {burned:0.#} it had");

            // The control: the same hold on the same rig, unpinned.
            pilot.AllowLiveRespawn = true;
            pilot.HoldActionForTest(InputAction.Respawn, true);
            pilot.SimStep(StepDt);
            ctx.Check(pilot.Fuel.Remaining > tank * 0.9f,
                $"ABLE-TO-FAIL CONTROL: unpinned, the same hold respawns and fills the tank ({pilot.Fuel.Remaining:0.#} of {tank:0.#} units)");

            // And the crashed read, which the pin does not touch: it is the crash camera's skip.
            pilot.AllowLiveRespawn = false;
            BurnHalf(pilot);
            pilot.HoldActionForTest(InputAction.Respawn, false);
            pilot.DebugForceCrash();
            ctx.Check(pilot.Crashed, $"the rig is down and waiting for a respawn");
            pilot.HoldActionForTest(InputAction.Respawn, true);
            pilot.SimStep(StepDt);
            ctx.Check(!pilot.Crashed && pilot.Fuel.Remaining > tank * 0.9f,
                $"…and the pinned rig still takes the button from a crash, flying again on {pilot.Fuel.Remaining:0.#} of {tank:0.#} units");
        }
        finally
        {
            pilot?.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"the live respawn is refused where the mission counts and kept from a crash");
    }

    // One skipped episode on a bound rig: out of flight, the skip armed, then the press taken. The
    // hand-back is what arms the trigger's consumed-press latch, so each leg drives the real codes.
    private static void Skipped(TestContext ctx, CutsceneController cutscene, FlightController pilot,
        string intro, string what)
    {
        ctx.Check(cutscene.Host(CutsceneController.CodeOutOfFlight, intro) && pilot.Inert && pilot.Held,
            $"{what}'s code {CutsceneController.CodeOutOfFlight} takes the player out of flight");
        ctx.Check(cutscene.Host(CutsceneController.CodeHoldsWorld, intro) && cutscene.Skippable,
            $"…and its code {CutsceneController.CodeHoldsWorld} arms the skip a press can take");
        ctx.Check(cutscene.Skip() && !cutscene.Playing && !pilot.Inert && !pilot.Held,
            $"…and the press ends {what} and hands flight back on that same frame");
    }

    // Half the tank, taken off the tank itself: the burn is dt x lever x BurnRate, and half a tank
    // is a reading a respawn's own top-up cannot be mistaken for.
    private static void BurnHalf(FlightController pilot) =>
        pilot.Fuel.Step(pilot.Fuel.Capacity * 0.5f / FuelTank.BurnRate, 1f);

    // The human rig, built the way the campaign suites build theirs: a real aircraft node with real
    // damage state, so DebugForceCrash goes through the production death path.
    private static FlightController HumanRig(TestContext ctx, GameZ planesGamez,
        TextureArchive textures, ProjectilePool live)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var pos = new Vector3(0f, SpawnAltitudeM, 0f);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                ? PlaneDamage.For(stats) : null,
            PlayerIndex = FlightRoster.ShooterIdBase - 1,
            IsHumanPiloted = true,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = AimAssist.PlayerTeam,
            Name = "LiveRespawnPilot",
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward);
        ctx.Host.AddChild(rig);
        return rig;
    }
}
