using System;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>What the flight side does with a control when a session hands input over: a cutscene
/// skip, a pause-sheet dismiss, a respawn. Each is a level read on a fixed tick, so a control
/// down when flight resumes reads as a fresh command unless something swallows it. One
/// control serves both sides, since gamepad B is the menu's Back and the gun trigger. A respawn
/// read while the aeroplane still flies is a free repair wherever the mission counts. In a stunt
/// run the same button splits by hold length into a return and a restart.</summary>
internal static class FlightInputHandoffSuites
{
    private const float StepDt = 1f / 60f;

    // Where the respawn rig flies: high enough over an empty world that nothing it steps through
    // resolves a ground contact.
    private const float SpawnAltitudeM = 800f;

    // A spawn speed no fallback carries, so a return at it can only have come from the rig's start.
    private const float StuntSpawnSpeed = 71.5f;

    // Presses either side of the split's threshold, two frames clear of it at 60 Hz.
    private static readonly int TapFrames = Mathf.RoundToInt(TapHoldButton.PadHoldSeconds / StepDt) - 2;
    private static readonly int HoldFrames = Mathf.RoundToInt(TapHoldButton.PadHoldSeconds / StepDt) + 2;

    [Suite("flight-input-handback",
        "the control that ended a mission intro or dismissed the pause sheet does not also act on "
        + "the flight it hands back: the intro's out-of-flight code takes the player out of flight, "
        + "its hold code arms the skip, and the press that takes that skip reads both triggers as "
        + "consumed for as long as it stays down, through its release, and reads the next real "
        + "press through -- gamepad B held across a pause resume fires no burst either, while a "
        + "hand-back with nothing down costs the press after it nothing and --fire never latches")]
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
            // The control: nothing has handed this aeroplane its input back, so every press fires.
            ctx.Check(pilot.RocketTriggerReadsForTest(true),
                $"ABLE-TO-FAIL CONTROL: an aeroplane that never left flight reads a pull through");
            ctx.Check(!pilot.RocketTriggerReadsForTest(false) && pilot.RocketTriggerReadsForTest(true),
                $"…and reads the pull after a release through as well");
            ctx.Check(pilot.GunTriggerReadsForTest(true),
                $"ABLE-TO-FAIL CONTROL: the same aeroplane reads a gun press through");

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

            // The gun trigger over the same boundary: gamepad B takes a skip as any button does,
            // and B is the gun trigger too, so the burst is the one the player never asked for.
            Skipped(ctx, cutscene, pilot, intro, "an episode skipped with B");
            ctx.Check(!pilot.GunTriggerReadsForTest(true),
                $"the B that took the skip fires no burst on the first frame of flight");
            ctx.Check(!pilot.GunTriggerReadsForTest(true) && !pilot.GunTriggerReadsForTest(false),
                $"…nor while it stays down, nor on its release");
            ctx.Check(pilot.GunTriggerReadsForTest(true),
                $"…while the press after that fires, so the skip costs the player one press and no more");

            // The other control: a hand-back nobody was holding a button through must not swallow
            // the press that follows it, which is what makes the assertions above a real gate.
            Skipped(ctx, cutscene, pilot, intro, "a second episode");
            ctx.Check(!pilot.RocketTriggerReadsForTest(false) && !pilot.GunTriggerReadsForTest(false),
                $"a hand-back with both triggers up reads nothing on the frame it happens");
            ctx.Check(pilot.RocketTriggerReadsForTest(true) && pilot.GunTriggerReadsForTest(true),
                $"…and the press straight after it fires on both");

            PauseResumed(ctx, pilot);
        }
        finally
        {
            pilot.Free();
            cutscene.Free();
        }

        ctx.Note($"the press that skips an intro or dismisses the pause sheet is consumed in flight");
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

    [Suite("stunt-respawn-tap-hold",
        "a stunt run's respawn button split by hold length over C1/IA1's real zones: a tap from a "
        + "crash with no zone cleared returns to the start with the clock running on, a tap after a "
        + "zone returns abeam that zone's exit heading the way it was flown at the start's own spawn "
        + "speed with zones and clock kept, a tap in flight does the same for the zone cleared last, "
        + "a hold in flight or from a crash restarts the run at the start with zones and clock "
        + "cleared and its release taps nothing, and a rig pinned the way Instant Action pins it "
        + "takes neither in flight while a crash still returns it")]
    internal static void StuntRespawnTapHold(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, "C1");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(gamezPath, $"C1 gamez");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        var run = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath));
        if (run is not { TotalCount: >= 3 })
        {
            throw new SuiteSkippedException($"C1/IA1 loads fewer than three danger zones");
        }

        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        FlightController? pilot = null;
        try
        {
            pilot = HumanRig(ctx, GameZ.Load(ctx.PlanesGamezPath), textures, pool, StuntSpawnSpeed);
            pilot.Stunt = run;
            var start = pilot.WorldPosition;
            Fly(pilot, 30);

            // No zone cleared: the tap is the plain return to the start, and the clock runs on.
            float clock = run.Elapsed;
            pilot.DebugForceCrash();
            int respawns = pilot.RespawnCount;
            Press(pilot, TapFrames);
            ctx.Check(!pilot.Crashed && pilot.RespawnCount == respawns + 1
                    && pilot.WorldPosition.DistanceTo(start) < 1e-3f && run.Elapsed > clock,
                $"a tap from a crash with no zone cleared flies again at the start, clock {StuntMission.FormatTime(clock)} running on to {StuntMission.FormatTime(run.Elapsed)}: moved {pilot.WorldPosition.DistanceTo(start):0.000} m from it");

            // One zone, then a crash: the tap returns abeam that zone's exit.
            ClearZone(run, run.Zones[2]);
            var exit = run.ReturnPose()!.Value;
            var zone = run.Zones[2];
            var exitGate = zone.ExitGate!;
            var entryGate = exitGate == zone.RedGate ? zone.GreenGate : zone.RedGate;
            clock = run.Elapsed;
            pilot.DebugForceCrash();
            Press(pilot, TapFrames);
            var at = pilot.WorldPosition;
            ctx.Check(at.DistanceTo(exit.Position) < 1e-2f && at.DistanceTo(exitGate.Center) < at.DistanceTo(entryGate.Center),
                $"a tap from a crash after {zone.PathName} returns abeam its exit gate: {at.DistanceTo(exitGate.Center):0.0} m from the exit, {at.DistanceTo(entryGate.Center):0.0} m from the entry");
            ctx.Check(pilot.NoseDirection.Dot(exit.Heading) > 0.999f && exit.Heading.Dot(zone.ExitTravel) > 0f,
                $"…heading on the way the zone was flown: nose·route={pilot.NoseDirection.Dot(exit.Heading):0.0000}, route·travel={exit.Heading.Dot(zone.ExitTravel):0.00}");
            ctx.Check(Mathf.Abs(pilot.WorldVelocity.Length() - StuntSpawnSpeed) < 0.01f,
                $"…at the start's own spawn speed, {pilot.WorldVelocity.Length():0.00} of {StuntSpawnSpeed:0.0} m/s");
            ctx.Check(run.CompletedCount == 1 && zone.Completed && run.Elapsed > clock,
                $"…with the zone kept and the clock running on from {StuntMission.FormatTime(clock)} to {StuntMission.FormatTime(run.Elapsed)}");

            // A second zone, then the tap in flight: the zone cleared last is the one returned to.
            ClearZone(run, run.Zones[0]);
            exit = run.ReturnPose()!.Value;
            clock = run.Elapsed;
            respawns = pilot.RespawnCount;
            Press(pilot, TapFrames);
            float drift = pilot.WorldPosition.DistanceTo(exit.Position);
            ctx.Check(pilot.RespawnCount == respawns + 1 && drift < StuntSpawnSpeed * StepDt * 2f
                    && run.CompletedCount == 2 && run.Elapsed > clock,
                $"a tap in flight returns once, abeam {run.Zones[0].PathName}'s exit ({drift:0.00} m off after one step), zones 2 and the clock {StuntMission.FormatTime(run.Elapsed)} kept");

            // The hold in flight restarts the run: the start, zones and clock cleared.
            respawns = pilot.RespawnCount;
            Hold(pilot, HoldFrames);
            ctx.Check(pilot.RespawnCount == respawns + 1 && run.CompletedCount == 0
                    && run.Elapsed < StepDt * 4f && pilot.WorldPosition.DistanceTo(start) < StuntSpawnSpeed * StepDt * 4f,
                $"a hold in flight restarts once at the start: zones {run.CompletedCount}, clock {StuntMission.FormatTime(run.Elapsed)}, {pilot.WorldPosition.DistanceTo(start):0.0} m from the start");
            Release(pilot);
            ctx.Check(pilot.RespawnCount == respawns + 1,
                $"…and letting go of that hold taps nothing ({pilot.RespawnCount - respawns} respawn(s))");

            // The hold from a crash restarts as well.
            Fly(pilot, 20);
            ClearZone(run, run.Zones[1]);
            pilot.DebugForceCrash();
            respawns = pilot.RespawnCount;
            Hold(pilot, HoldFrames);
            Release(pilot);
            ctx.Check(!pilot.Crashed && pilot.RespawnCount == respawns + 1 && run.CompletedCount == 0
                    && run.Elapsed < StepDt * 5f && pilot.WorldPosition.DistanceTo(start) < StuntSpawnSpeed * StepDt * 5f,
                $"a hold from a crash restarts once at the start: zones {run.CompletedCount}, clock {StuntMission.FormatTime(run.Elapsed)}, {pilot.WorldPosition.DistanceTo(start):0.0} m from the start");

            // Pinned as Instant Action pins it: neither press is taken in flight.
            pilot.AllowLiveRespawn = false;
            ClearZone(run, run.Zones[1]);
            exit = run.ReturnPose()!.Value;
            respawns = pilot.RespawnCount;
            Press(pilot, TapFrames);
            Hold(pilot, HoldFrames);
            Release(pilot);
            ctx.Check(pilot.RespawnCount == respawns && run.CompletedCount == 1,
                $"pinned, neither a tap nor a hold in flight is taken: {pilot.RespawnCount - respawns} respawn(s), zones {run.CompletedCount}");
            pilot.DebugForceCrash();
            Press(pilot, TapFrames);
            ctx.Check(pilot.RespawnCount == respawns + 1 && pilot.WorldPosition.DistanceTo(exit.Position) < 1e-2f,
                $"ABLE-TO-FAIL CONTROL: the pinned rig still takes the tap from a crash, abeam {run.Zones[1].PathName}'s exit ({pilot.WorldPosition.DistanceTo(exit.Position):0.000} m off)");
        }
        finally
        {
            pilot?.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"the stunt respawn taps back to the last cleared zone and holds into a restart");
    }

    [Suite("stunt-start-count",
        "a solo stunt run's start count over C1/IA1's real zones on a 71.5 m/s spawn: a tap from a "
        + "crash returns with no count and the clock running on, a hold in flight restarts the run "
        + "behind the count with the aircraft walking in along its nose from the spawn speed times "
        + "the time left, stick, throttle and respawn held through the count move nothing but the "
        + "walk and leave the clock at 0, the GO step lands on the spawn state a respawn leaves bit "
        + "for bit with the clock still 0, the step after it the held controls act and the clock "
        + "reads one step, the respawn held over GO taps nothing on release, and a hold from a "
        + "crash runs the count as well")]
    internal static void StuntStartCount(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, "C1");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(gamezPath, $"C1 gamez");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        var run = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath));
        if (run is not { TotalCount: >= 3 })
        {
            throw new SuiteSkippedException($"C1/IA1 loads fewer than three danger zones");
        }

        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        FlightController? pilot = null;
        try
        {
            pilot = HumanRig(ctx, GameZ.Load(ctx.PlanesGamezPath), textures, pool, StuntSpawnSpeed);
            pilot.Stunt = run;
            pilot.RerunCount = StartCount.Rerun;
            // The spawn state a plain respawn leaves, which GO must reproduce bit for bit.
            var spawnPos = pilot.WorldPosition;
            var spawnNose = pilot.NoseDirection;
            var spawnVelocity = pilot.WorldVelocity;
            float spawnLever = pilot.Throttle;
            Fly(pilot, 30);

            // A tap never runs the count: back after a zone with the clock running on.
            ClearZone(run, run.Zones[2]);
            pilot.DebugForceCrash();
            float clock = run.Elapsed;
            Press(pilot, TapFrames);
            ctx.Check(!pilot.Crashed && !pilot.StartCount.Running && run.CompletedCount == 1 && run.Elapsed > clock,
                $"a tap from a crash returns with no count: count running={pilot.StartCount.Running}, clock {StuntMission.FormatTime(clock)} on to {StuntMission.FormatTime(run.Elapsed)}");

            // A hold in flight restarts the run behind the count.
            Fly(pilot, 20);
            int respawns = pilot.RespawnCount;
            Hold(pilot, HoldFrames);
            var walkStart = pilot.WorldPosition;
            float expectedBack = StuntSpawnSpeed * pilot.StartCount.Remaining;
            ctx.Check(pilot.RespawnCount == respawns + 1 && pilot.StartCount.Running && run.CompletedCount == 0 && run.Elapsed == 0f,
                $"a hold in flight restarts the run behind the count: respawns {pilot.RespawnCount - respawns}, count running={pilot.StartCount.Running}, zones {run.CompletedCount}, clock {run.Elapsed}");
            ctx.Check(Mathf.Abs(walkStart.DistanceTo(spawnPos) - expectedBack) < 0.05f
                    && (spawnPos - walkStart).Normalized().Dot(spawnNose) > 0.9999f,
                $"…walking in along the nose from {walkStart.DistanceTo(spawnPos):0.00} m behind the start, the spawn speed times the {pilot.StartCount.Remaining:0.000} s left ({expectedBack:0.00} m)");

            // Through the count, stick, throttle and respawn all held hard.
            pilot.HoldActionForTest(InputAction.PitchUp, true);
            pilot.HoldActionForTest(InputAction.RollLeft, true);
            pilot.HoldActionForTest(InputAction.ThrottleUp, true);
            pilot.HoldActionForTest(InputAction.Respawn, true);
            int steps = 0;
            int disturbed = 0;
            float offLine = 0f;
            while (pilot.StartCount.Running && steps < 600)
            {
                pilot.SimStep(StepDt);
                steps++;
                if (pilot.StartCount.Running
                    && (run.Elapsed != 0f || pilot.NoseDirection != spawnNose || pilot.Throttle != spawnLever
                        || pilot.LastCommand.Pitch != 0f || pilot.LastCommand.Roll != 0f))
                {
                    disturbed++;
                }
                var along = pilot.WorldPosition - spawnPos;
                offLine = Mathf.Max(offLine, (along - (spawnNose * along.Dot(spawnNose))).Length());
            }
            ctx.Check(!pilot.StartCount.Running && disturbed == 0 && offLine < 0.01f && pilot.RespawnCount == respawns + 1,
                $"held controls move nothing but the walk for the {steps} count steps: {disturbed} disturbed, {offLine:0.000} m off the line, respawns {pilot.RespawnCount - respawns}");
            ctx.Check(pilot.WorldPosition == spawnPos && pilot.NoseDirection == spawnNose
                    && pilot.WorldVelocity == spawnVelocity && pilot.Throttle == spawnLever && run.Elapsed == 0f,
                $"the GO step is the spawn state a respawn leaves, bit for bit, and the clock reads 0: {pilot.WorldPosition.DistanceTo(spawnPos):0.000000} m off, velocity {pilot.WorldVelocity.DistanceTo(spawnVelocity):0.000000} m/s off, lever {pilot.Throttle:0.000} of {spawnLever:0.000}, clock {run.Elapsed}");

            pilot.SimStep(StepDt);
            ctx.Check(run.Elapsed == StepDt && (pilot.LastCommand.Pitch != 0f || pilot.LastCommand.Roll != 0f),
                $"ABLE-TO-FAIL CONTROL: the step after GO the held stick acts (pitch {pilot.LastCommand.Pitch:0.000}, roll {pilot.LastCommand.Roll:0.000}) and the clock reads one step ({run.Elapsed:0.0000} s)");
            pilot.HoldActionForTest(InputAction.PitchUp, false);
            pilot.HoldActionForTest(InputAction.RollLeft, false);
            pilot.HoldActionForTest(InputAction.ThrottleUp, false);
            Release(pilot);
            ctx.Check(pilot.RespawnCount == respawns + 1 && !pilot.StartCount.Running,
                $"…and the respawn held over GO taps nothing on release ({pilot.RespawnCount - respawns} respawn(s))");

            // A hold from a crash runs the count as well.
            Fly(pilot, 20);
            pilot.DebugForceCrash();
            Hold(pilot, HoldFrames);
            ctx.Check(!pilot.Crashed && pilot.StartCount.Running && run.Elapsed == 0f,
                $"a hold from a crash restarts behind the count too: count running={pilot.StartCount.Running}, clock {run.Elapsed}");
            Release(pilot);
        }
        finally
        {
            pilot?.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"a held restart runs the start count and hands over on the spawn state at GO");
    }

    // One skipped episode on a bound rig: out of flight, the skip armed, then the press taken. The
    // hand-back is what arms flight's consumed-input latch, so each leg drives the real codes.
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

    // The other re-entry point: the pause sheet, dismissed with gamepad B, which is the gun trigger
    // as well. The board's own Dismissed action is the resume, and the halt-clearing poll after it
    // is where flight takes the frame back with B still down.
    private static void PauseResumed(TestContext ctx, FlightController pilot)
    {
        var state = new PauseState();
        pilot.PauseState = state;
        ctx.Check(!pilot.PollPauseForTest(null), $"the flight is running before the sheet goes up");
        ctx.Check(state.TryToggle(0) && pilot.PollPauseForTest(null),
            $"the pause sheet halts the flight for the player who opened it");

        state.ForceResume();
        ctx.Check(!pilot.PollPauseForTest(null),
            $"…and the B that dismisses it hands the flight straight back");
        ctx.Check(!pilot.GunTriggerReadsForTest(true),
            $"the B that dismissed the sheet fires no burst on the frame flight comes back");
        ctx.Check(!pilot.GunTriggerReadsForTest(true) && !pilot.GunTriggerReadsForTest(false),
            $"…nor while it stays down, nor on its release");
        ctx.Check(pilot.GunTriggerReadsForTest(true),
            $"…while the press after that fires, so the resume costs the player one press and no more");

        // --fire is a switch a scripted run sets, not a control anybody held through the resume, so
        // the latch never sees it: an unattended soak keeps firing across a hand-back.
        state.TryToggle(0);
        pilot.PollPauseForTest(null);
        state.ForceResume();
        pilot.PollPauseForTest(null);
        pilot.AutoFire = true;
        ctx.Check(pilot.GunTriggerReadsForTest(false),
            $"ABLE-TO-FAIL CONTROL: --fire fires through a resume with no button down at all");
        pilot.AutoFire = false;
        pilot.PauseState = null;
    }

    // Half the tank, taken off the tank itself: the burn is dt x lever x BurnRate, and half a tank
    // is a reading a respawn's own top-up cannot be mistaken for.
    private static void BurnHalf(FlightController pilot) =>
        pilot.Fuel.Step(pilot.Fuel.Capacity * 0.5f / FuelTank.BurnRate, 1f);

    // The human rig, built the way the campaign suites build theirs: a real aircraft node with real
    // damage state, so DebugForceCrash goes through the production death path.
    private static FlightController HumanRig(TestContext ctx, GameZ planesGamez,
        TextureArchive textures, ProjectilePool live, float? spawnSpeed = null)
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
        if (spawnSpeed is { } speed)
            rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward, spawnSpeed: speed);
        else
            rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward);
        ctx.Host.AddChild(rig);
        return rig;
    }

    // Steps flying with the respawn button up.
    private static void Fly(FlightController pilot, int frames)
    {
        pilot.HoldActionForTest(InputAction.Respawn, false);
        for (int i = 0; i < frames; i++)
            pilot.SimStep(StepDt);
    }

    // The respawn button down for this many steps, left down.
    private static void Hold(FlightController pilot, int frames)
    {
        pilot.HoldActionForTest(InputAction.Respawn, true);
        for (int i = 0; i < frames; i++)
            pilot.SimStep(StepDt);
    }

    private static void Release(FlightController pilot) => Fly(pilot, 1);

    // Down for this many steps, then the one step that lets it go.
    private static void Press(FlightController pilot, int frames)
    {
        Hold(pilot, frames);
        Release(pilot);
    }

    // Both gates of a zone crossed green to red on the run itself, each along its own axis. A fresh
    // segment follows, so the aircraft's next step is not tested from the last gate.
    private static void ClearZone(StuntMission run, StuntZone zone)
    {
        var travel = (zone.RedGate.Center - zone.GreenGate.Center).Normalized();
        foreach (var gate in new[] { zone.GreenGate, zone.RedGate })
        {
            var along = gate.Normal * (gate.Normal.Dot(travel) < 0f ? -20f : 20f);
            run.Relocated();
            run.Update(gate.Center - along);
            run.Update(gate.Center + along);
        }
        run.Relocated();
    }
}
