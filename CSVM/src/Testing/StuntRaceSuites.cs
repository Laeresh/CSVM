using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.InstantAction;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The time-attack stunt race flown on real seats built through the session's own roster
/// over C1/IA1's zones. It covers the shared opening, restarts behind each pilot's own count,
/// best-run ranking, the final run and the board.</summary>
internal static class StuntRaceSuites
{
    private const float Dt = 1f / 60f;

    // A short window, long enough for a run, a restart and a second run on each seat.
    private const float WindowSeconds = 20f;

    // The opening count's READY, the session's own.
    private const float ReadySeconds = 2f;

    private static readonly int HoldFrames = Mathf.RoundToInt(TapHoldButton.PadHoldSeconds / Dt) + 2;

    [Suite("stunt-race-time-attack",
        "two seats built through the session's roster over C1/IA1's zones race a 20 s window: both "
        + "open on READY then 3, 2, 1 in lockstep and the window clock starts at that GO with both "
        + "run clocks; P1 completes a run, holds respawn into a new run behind its own 3, 2, 1 "
        + "while P2 flies on, keeps its first time as its best until a faster run replaces it with "
        + "that run's splits; P2 restarts late, is in a run at time up and completes it as the "
        + "final run, faster than P1's best; P1's hold in the final run is refused; the race ends on "
        + "P2's finish and its board ranks P2 then P1 by best run, with each pilot's best-run splits")]
    internal static void StuntRaceTimeAttack(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, "C1");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(gamezPath, $"C1 gamez");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");
        var zones = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath));
        if (zones is not { TotalCount: >= 4 })
        {
            throw new SuiteSkippedException($"C1/IA1 loads fewer than four danger zones");
        }

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rigs = new[]
        {
            new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
            new PlayerRig { Index = 1, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
        };
        var race = new StuntRace(WindowSeconds, zones.TotalCount);
        var pause = new PauseState();
        var spec = SessionSpec.Parse(new[] { "--no-pads" });
        var roster = new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host,
            SuiteConstants.AircraftResources(ctx, planesGamez, textures, Messages.Load(ctx.MessagesPath), _ => new CamParams()),
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"),
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Length,
                Rigs = rigs,
                PauseState = pause,
                StuntZones = zones,
                Race = race,
                RestartCount = StartCount.Restart,
                FirstStartCount = StartCount.Opening(ReadySeconds),
            }, new ApartStarts());
        var zoneNames = zones.Zones.Select(z => z.Description).ToList();
        var board = StuntRaceBoard.Build(race, zoneNames, "C1   ·   test", exitsToMenu: true, pause, _ => new MenuInput());
        ctx.Host.AddChild(board);
        try
        {
            roster.BuildPlayers(rigs);
            if (rigs[0].Controller is not { Stunt: { } run1 } p1 || rigs[1].Controller is not { Stunt: { } run2 } p2)
            {
                ctx.Check(false, $"both seats were built with a stunt run");
                return;
            }

            foreach (var seat in new[] { p1, p2 })
            {
                seat.UseKeyboard = false;
                seat.PadDevices = Array.Empty<int>();
                seat.HoldActionForTest(InputAction.Respawn, false);
            }

            ctx.Check(p1.Race == race && p2.Race == race && race.Racers.Count == 2
                    && p1.StartCount.Figure == "READY" && p2.StartCount.Figure == "READY",
                $"the roster seats both pilots in the race, each opening on READY: {p1.StartCount.Figure}/{p2.StartCount.Figure}, {race.Racers.Count} racers");
            race.BeginOpening(ReadySeconds + 3f);

            void Frame()
            {
                if (pause.Halted)
                {
                    return;
                }

                p1.SimStep(Dt);
                p2.SimStep(Dt);
                race.Advance(Dt);
            }

            void Frames(int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Frame();
                }
            }

            void Restart(FlightController seat)
            {
                seat.HoldActionForTest(InputAction.Respawn, true);
                Frames(HoldFrames);
                seat.HoldActionForTest(InputAction.Respawn, false);
                Frame();
            }

            // The opening: lockstep counts, the window opening on their GO step.
            int steps = 0;
            int early = 0;
            while (p1.StartCount.Running && steps < 600)
            {
                Frame();
                steps++;
                if (p1.StartCount.Running != p2.StartCount.Running || (p1.StartCount.Running && race.Phase != StuntRacePhase.Opening))
                {
                    early++;
                }
            }
            ctx.Check(steps == 300 && early == 0 && race.Phase == StuntRacePhase.Open && race.WindowElapsed == 0f
                    && run1.Elapsed == 0f && run2.Elapsed == 0f,
                $"both counts hand over together after {steps} steps and the window opens on that GO step: {early} out of step, window {race.WindowElapsed} s, clocks {run1.Elapsed}/{run2.Elapsed}");
            Frame();
            ctx.Check(race.WindowElapsed == Dt && run1.Elapsed == Dt && run2.Elapsed == Dt
                    && race.Of(0)!.InRun && race.Of(1)!.InRun,
                $"the step after GO the window and both run clocks read one step: window {race.WindowElapsed:0.0000}, clocks {run1.Elapsed:0.0000}/{run2.Elapsed:0.0000}");

            // P1's first run, every zone half a second apart; P2 clears two meanwhile.
            for (int k = 0; k < run1.TotalCount; k++)
            {
                Frames(30);
                ClearZone(run1, run1.Zones[k]);
                if (k < 2)
                {
                    ClearZone(run2, run2.Zones[k]);
                }
            }
            float firstRun = run1.Elapsed;
            var r1 = race.Of(0)!;
            var r2 = race.Of(1)!;
            ctx.Check(run1.AllComplete && r1.BestTime == firstRun && r1.RunsFinished == 1 && !r1.InRun && r2.MostZones == 2,
                $"P1's first run counts as its best, {StuntMission.FormatTime(firstRun)}, and P2 has two zones: best {r1.BestTime}, P2 zones {r2.MostZones}");

            // P1 holds into a new run behind its own count while P2's clock runs on.
            float p2Before = run2.Elapsed;
            Restart(p1);
            ctx.Check(p1.StartCount.Running && run1.CompletedCount == 0 && run1.Elapsed == 0f
                    && r1.BestTime == firstRun && run2.Elapsed > p2Before,
                $"a held respawn restarts P1 behind its count with its best kept ({r1.BestTime}) while P2's clock runs on ({StuntMission.FormatTime(p2Before)} to {StuntMission.FormatTime(run2.Elapsed)})");
            int counting = 0;
            bool clockStill = true;
            while (p1.StartCount.Running && counting < 400)
            {
                Frame();
                counting++;
                clockStill &= run1.Elapsed == 0f;
            }
            Frame();
            ctx.Check(clockStill && run1.Elapsed == Dt && r1.InRun && r1.RunsStarted == 2,
                $"P1's run clock stands at 0 through its {counting}-step count and starts at its own GO: {run1.Elapsed:0.0000} s, runs started {r1.RunsStarted}");

            // P1's second run is faster and becomes its best, with its own splits.
            for (int k = 0; k < run1.TotalCount; k++)
            {
                Frames(20);
                ClearZone(run1, run1.Zones[k]);
            }
            float secondRun = run1.Elapsed;
            var p1Splits = run1.Zones.Select(z => (float?)z.CompletedAt).ToList();
            ctx.Check(secondRun < firstRun && r1.BestTime == secondRun && r1.Splits.SequenceEqual(p1Splits),
                $"P1's faster second run replaces its best, {StuntMission.FormatTime(firstRun)} to {StuntMission.FormatTime(secondRun)}, splits and all");

            // P2 restarts late enough that its run is still in progress at time up.
            while (race.TimeLeft > 3.6f && race.Phase == StuntRacePhase.Open)
            {
                Frame();
            }
            Restart(p2);
            while (p2.StartCount.Running)
            {
                Frame();
            }
            Frame();
            Frames(12);
            ClearZone(run2, run2.Zones[0]);
            while (race.Phase == StuntRacePhase.Open)
            {
                Frame();
            }
            ctx.Check(race.Phase == StuntRacePhase.FinalRun && r2.InRun && !r1.InRun,
                $"at time up P2's run is in progress and the race runs on as its final run: {race.Phase}, P2 in a run={r2.InRun}");

            // P1 cannot start another run in the final run.
            int respawns = p1.RespawnCount;
            Restart(p1);
            ctx.Check(p1.RespawnCount == respawns && !p1.StartCount.Running && run1.AllComplete && run1.Elapsed == secondRun,
                $"P1's hold in the final run is refused: {p1.RespawnCount - respawns} respawn(s), count running={p1.StartCount.Running}, run kept at {StuntMission.FormatTime(run1.Elapsed)}");

            // P2 completes its final run faster than P1's best; the race ends on it.
            for (int k = 1; k < run2.TotalCount; k++)
            {
                Frames(2);
                ClearZone(run2, run2.Zones[k]);
            }
            var p2Splits = run2.Zones.Select(z => (float?)z.CompletedAt).ToList();
            ctx.Check(race.Ended && r2.BestTime == run2.Elapsed && run2.Elapsed < secondRun && board.Visible && pause.Halted,
                $"P2's final run counts, {StuntMission.FormatTime(run2.Elapsed)}, and its finish ends the race on the board: ended={race.Ended} board={board.Visible} halted={pause.Halted}");
            ctx.Check(board.Rows.Count == 2 && board.Rows[0].StartsWith("1st  P2", StringComparison.Ordinal)
                    && board.Rows[1].StartsWith("2nd  P1", StringComparison.Ordinal),
                $"the board ranks by best run, P2 first though it finished last: {string.Join(" | ", board.Rows)}");
            ctx.Check(r2.Splits.SequenceEqual(p2Splits) && r1.Splits.SequenceEqual(p1Splits),
                $"…and carries each pilot's best-run splits: P2 {string.Join(",", r2.Splits.Select(s => s?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
        finally
        {
            board.Free();
            var built = rigs.Select(rig => rig.Controller).Where(c => c != null).ToArray();
            roster.ClearMembership();
            foreach (var controller in built)
            {
                controller!.Free();
            }

            pane.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"a two-seat window ranked by best run, with a restart each and a final run");
    }

    [Suite("stunt-race-no-lives",
        "an Instant Action stunt race on two seats built through the session's roster over C1/IA1, "
        + "its end wired by the director over a two-life def: P1 crashes three times and both crash "
        + "together, and every crash returns its pilot after the crash camera with no life spent, "
        + "nobody spectating, the mission never lost and no wrap-up due; the control, the same "
        + "director wiring with no race over P1 alone on one life, loses on its first crash into "
        + "spectate and hands the wrap-up to the menu once after the hold")]
    internal static void StuntRaceSpendsNoLives(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, "C1");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(gamezPath, $"C1 gamez");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");
        var zones = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath));
        if (zones is not { TotalCount: >= 1 })
        {
            throw new SuiteSkippedException($"C1/IA1 loads no danger zone");
        }

        const float RespawnDelay = 0.5f;
        int respawnFrames = Mathf.CeilToInt(RespawnDelay / Dt) + 30;
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var world = new Node3D();
        ctx.Host.AddChild(world);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rigs = new List<PlayerRig>
        {
            new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
            new PlayerRig { Index = 1, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
        };
        var race = new StuntRace(600f, zones.TotalCount);
        var pause = new PauseState();
        var spec = SessionSpec.Parse(new[] { "--no-pads" });
        var roster = new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host,
            SuiteConstants.AircraftResources(ctx, planesGamez, textures, Messages.Load(ctx.MessagesPath), _ => new CamParams()),
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"),
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = pause,
                StuntZones = zones,
                Race = race,
            }, new ApartStarts());

        // The director as the session builds it over an --ia= def, its end wired as GameSession
        // wires it. The Original ending's menu hand-off stands in for the board.
        InstantActionDirector? Director(string tag, int? lives, List<PlayerRig> seats, StuntRace? withRace, List<IaWrapupSnapshot> wrapups)
        {
            InstantActionSuites.EndDef(ctx, tag, "stunt_flying", lives);
            var iaSpec = SessionSpec.Parse(new[] { "--no-pads", $"--ia={Path.Combine(ctx.ScratchDir, $"ia-end-{tag}.json")}" });
            var director = InstantActionDirector.TryCreate(iaSpec);
            if (director == null)
            {
                return null;
            }

            director.SeatRigsForTest(seats);
            director.WireEndConditions(new InstantActionDirector.EndConditionInputs
            {
                StuntZones = zones,
                Race = withRace,
                Projectiles = pool,
                WorldRoot = world,
                SpectatorCameras = new List<SpectatorCamera>(),
                LockCandidates = () => Array.Empty<Node3D>(),
                RespawnDelay = RespawnDelay,
                WrapupToMenu = wrapups.Add,
            });
            return director;
        }

        try
        {
            roster.BuildPlayers(rigs);
            if (rigs[0].Controller is not { Stunt: not null } p1 || rigs[1].Controller is not { Stunt: not null } p2)
            {
                ctx.Check(false, $"both seats were built with a stunt run");
                return;
            }

            foreach (var seat in new[] { p1, p2 })
            {
                seat.UseKeyboard = false;
                seat.PadDevices = Array.Empty<int>();
                seat.HoldActionForTest(InputAction.Respawn, false);
            }

            var raceWrapups = new List<IaWrapupSnapshot>();
            var raceDirector = Director("race-lives", 2, rigs, race, raceWrapups);
            ctx.Check(raceDirector != null, $"the director builds over the race's two-life def");
            if (raceDirector == null)
            {
                return;
            }

            var mission = raceDirector.Runtime;
            race.BeginOpening(0f);

            void Frames(int n, InstantActionDirector director)
            {
                for (int i = 0; i < n; i++)
                {
                    p1.SimStep(Dt);
                    p2.SimStep(Dt);
                    race.Advance(Dt);
                    director.Step(Dt);
                }
            }

            ctx.Check(mission.Def.Lives == 2 && mission.LivesWaived && mission.PilotCount == 2,
                $"the race waives the def's {mission.Def.Lives} lives over both seats: waived={mission.LivesWaived}, {mission.PilotCount} seat(s)");

            // P1 crashes more times than it has lives, each time back after the crash camera.
            bool allBack = true;
            for (int crash = 1; crash <= 3; crash++)
            {
                p1.DebugForceCrash();
                bool down = p1.Crashed;
                Frames(respawnFrames, raceDirector);
                allBack &= down && !p1.Crashed && !p1.Spectating;
            }

            ctx.Check(allBack && mission.LivesLeft(0) == 2 && !mission.IsSpectating(0) && !mission.Ended,
                $"three crashes on two lives each return P1 with none spent: back={allBack}, lives {mission.LivesLeft(0)}, spectating={mission.IsSpectating(0)}, {mission.Outcome}");

            // Both down at once, the case that loses a mission spending lives.
            p1.DebugForceCrash();
            p2.DebugForceCrash();
            bool bothDown = p1.Crashed && p2.Crashed;
            Frames(respawnFrames, raceDirector);
            Frames(Mathf.CeilToInt(InstantActionRuntime.WrapupHoldS / Dt) + 10, raceDirector);
            ctx.Check(bothDown && !p1.Crashed && !p2.Crashed && !p1.Spectating && !p2.Spectating
                    && mission.Outcome == InstantActionOutcome.Running && raceWrapups.Count == 0,
                $"both crashing together lose nothing and end nothing: back={!p1.Crashed}/{!p2.Crashed}, {mission.Outcome}, {raceWrapups.Count} wrap-up(s), lives {mission.LivesLeft(0)}/{mission.LivesLeft(1)}");

            // The control: the same wiring with no race, P1 alone on the default one life.
            p1.Race = null;
            var soloWrapups = new List<IaWrapupSnapshot>();
            var soloDirector = Director("solo-lives", null, new List<PlayerRig> { rigs[0] }, null, soloWrapups);
            ctx.Check(soloDirector != null, $"the director builds over the solo run's one-life def");
            if (soloDirector == null)
            {
                return;
            }

            var solo = soloDirector.Runtime;
            p1.DebugForceCrash();
            Frames(respawnFrames, soloDirector);
            ctx.Check(!solo.LivesWaived && solo.Outcome == InstantActionOutcome.Lost && p1.Spectating && p1.Crashed,
                $"the control's solo run on one life loses on its first crash into spectate: waived={solo.LivesWaived}, {solo.Outcome}, spectating={p1.Spectating}");
            Frames(Mathf.CeilToInt(InstantActionRuntime.WrapupHoldS / Dt) + 10, soloDirector);
            ctx.Check(soloWrapups.Count == 1 && soloWrapups[0] is { Won: false },
                $"and hands its wrap-up to the menu once after the hold: {soloWrapups.Count} wrap-up(s)");
        }
        finally
        {
            var built = rigs.Select(rig => rig.Controller).Where(c => c != null).ToArray();
            roster.ClearMembership();
            foreach (var controller in built)
            {
                controller!.Free();
            }

            world.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"a race's crashes cost time alone; the solo control still loses on its last life");
    }

    // Both gates of a zone crossed green to red on the run itself, each along its own axis, the
    // respawn suites' own crossing. A fresh segment follows, so the next step is not tested from it.
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

    // Two starts well above C1's terrain, clear of every zone and of each other.
    private sealed class ApartStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 200f, 3000f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 1f, 90f);
            }

            return starts;
        }
    }
}
