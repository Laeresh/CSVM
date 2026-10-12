using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Stunt races with bots, on <see cref="NetCombatSuites"/>'s rig. Every bot flies the course
/// itself, so each zone it clears is the run's own gate test and each time the race's own record. The
/// course is the run's chapter's IA1, C1 unless <c>--chapter=</c> names another, so one suite flies
/// any shipped course.</summary>
internal static class StuntRaceBotSuites
{
    private const ulong Seed = 0x57B07B07UL;

    private const int PaneSeat = 0;
    private const int BotSeat = 1;

    private const string Callsign = "Racer";

    // A window long enough for the longest course, minutes: the race may not end before the bot does.
    private const int WindowMinutes = 15;

    // How long a return or a rerun is given, seconds: the crash camera or the count, with room to spare.
    private const float WaitSeconds = 10f;

    // How far the return may stand from the last zone's exit pose, metres: a few steps of flight.
    private const float ReturnReach = 30f;

    // How often the world's moved colliders reach the physics server, in steps.
    private const int SyncSteps = 30;

    [Suite("stunt-race-bot",
        "a local stunt race of one pane and one ace bot over the run chapter's IA1 course: the launch seats "
        + "the bot, which carries no weapons and races under its callsign; the bot flies every Danger Zone "
        + "through its own gate test, steering between them and running each on its ribbon; crashed "
        + "once after its second zone it returns unattended to that zone's exit with its zones and clock "
        + "kept; it completes the run inside the window and the race board shows its time under its "
        + "callsign; once done it starts another run while the window is open; and the ABLE-TO-FAIL "
        + "CONTROL, the same bot with its objective off, clears no zone and completes no run in the steps "
        + "the first took")]
    internal static void ABotFliesTheCourseAndPostsATime(TestContext ctx)
    {
        string chapter = Course(ctx, ctx.Chapter);
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), chapter, new[] { "player_pfighter" },
            MenuMode.Stunt, vsTimeMinutes: WindowMinutes, missionType: DogfightMissionType.StuntRace,
            bots: new[] { new VsBotEntry("player_fury", NetBotSkill.Ace, 0, Callsign) });
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            int steps = Fly(ctx, spec, objective: true, limit: null);
            Fly(ctx, spec, objective: false, steps);
        }
        finally
        {
            ambient.Restore();
        }
    }

    [Suite("stunt-race-bot-menu",
        "a menu launch of Instant Action Stunt Flying on C1 by one seat, with one bot row on the join board: "
        + "the exit carries the row, which makes the run a race; the session seats the bot after the pane "
        + "under the row's callsign, racing on its own run of the course, and it clears a Danger Zone; "
        + "ABLE-TO-FAIL CONTROL: a squadron mission from the same setup carries no row")]
    internal static void AMenuStuntRunTakesTheJoinBoardsBots(TestContext ctx)
    {
        const string cell = "menu";
        string chapter = Course(ctx, "C1");
        var setup = new PlayerSetupFeature();
        setup.Bots.CallsignPool = Session.Roster.BotSeats.CallsignPool(Mech3.Messages.Load(ctx.MessagesPath));
        setup.AddBot();
        int row = setup.Bots.Rows[0].Id;
        setup.RenameBot(row, Callsign);
        setup.Bots.SetSkill(row, NetBotSkill.Ace);
        var ia = InstantActionFeature.ForDataRoot(ctx.DataRoot);
        ia.SelectEnvironment(InstantActionFeature.Environments.ToList().FindIndex(e => e.Code == chapter));
        ia.ConfirmEnvironment();
        var seats = new[] { new MenuSeatChoice("player_pfighter", Array.Empty<int>()) };
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == "dogfight_squadron"));
        ctx.Check(ia.BuildExit(seats, bots: setup.Bots.LaunchEntries()).Bots == null,
            $"ABLE-TO-FAIL CONTROL: [{cell}] a squadron mission carries no bot row");
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == InstantActionFeature.StuntKey));
        var exit = ia.BuildExit(seats, bots: setup.Bots.LaunchEntries());
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), exit.Chapter,
            exit.Seats.Select(s => s.PlaneNode).ToArray(), exit.Mode, exit.InstantAction, bots: exit.Bots);
        ctx.Check(exit.Bots is { Count: 1 } && spec is { Stunt: true, VsBots.Count: 1 },
            $"[{cell}] the Stunt Flying exit carries the join board's row into the spec ({exit.Bots?.Count ?? 0} row(s), {spec.VsBots.Count} bot(s))");
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        ctx.Check(roster is { Length: 2 } && roster[BotSeat] is { IsBot: true, Callsign: Callsign },
            $"[{cell}] the launch seats the pane and then the row's bot '{Callsign}'");
        if (roster == null)
        {
            return;
        }

        var ambient = NetCombatSuites.Ambient.Save();
        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            var session = end.Session;
            var bot = end.Built && session.SeatRigs.Count > BotSeat ? session.SeatRigs[BotSeat].Controller : null;
            ctx.Check(end.Built && session.Race?.Of(BotSeat)?.Callsign == Callsign && session.RaceBots != null && bot is { Stunt: not null },
                $"[{cell}] the Instant Action session races the bot under its callsign on its own run");
            if (session.Race?.Of(BotSeat) is not { } racer || session.RaceBots == null)
            {
                return;
            }

            int limit = Steps(session.Race.WindowSeconds);
            int steps = 0;
            while (racer.MostZones == 0 && steps < limit)
            {
                Step(session, ++steps);
            }

            ctx.Check(racer.MostZones > 0, $"[{cell}] the bot clears a Danger Zone ({racer.MostZones} zone(s), {steps} steps)");
        }
        finally
        {
            end.Close();
            ambient.Restore();
        }
    }

    [Suite("net-stunt-race-bot",
        "a two-machine Stunt Race from the lobby's options over a clean loopback, the host's roster "
        + "carrying an ace bot after the guest: the host flies the bot and the guest flies none; the bot "
        + "completes a run of the course on the host, and the guest's race board shows the same time "
        + "under the bot's callsign, carried by the host's racer line")]
    internal static void ANetworkBotPostsItsTimeOnEveryBoard(TestContext ctx)
    {
        const string cell = "network";
        string chapter = Course(ctx, "C1");
        var options = new DogfightOptionsMessage(1, (byte)DogfightLobby.EnvironmentOf(chapter), (byte)DogfightMissionType.StuntRace,
            DogfightVictory.Time, 5, DogfightLobby.DefaultScore, false, DogfightLobby.DefaultLives, false);
        var rules = DogfightLobby.RulesOf(options);
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), DogfightLobby.ChapterOf(options.Environment),
            new[] { "player_pfighter" }, DogfightLobby.LaunchMode(options), vsTimeMinutes: rules.TimeLimitMinutes, missionType: rules.MissionType);
        var roster = NetCombatSuites.Roster(2, spec).Append(NetSeats.Bot(0, 2, Callsign, "player_fury", NetBotSkill.Ace)).ToArray();
        NetSeats.Validate(roster, hostPeer: 0);
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(9571));
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, Seed + (ulong)i, i == 0 ? roster : null));
            }

            var peers = ends.Select(e => e.Session).ToArray();
            ctx.Check(ends.All(e => e.Built) && peers.All(p => p.Race != null),
                $"[{cell}] both machines build {chapter}'s IA1 race ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built) || peers[0].Race is not { } hostRace || peers[1].Race is not { } guestRace)
            {
                return;
            }

            var flown = peers[0].SeatRigs.Count > 2 ? peers[0].SeatRigs[2].Controller : null;
            var copy = peers[1].SeatRigs.Count > 2 ? peers[1].SeatRigs[2].Controller : null;
            ctx.Check(peers[0].RaceBots != null && flown is { Pilot: not null, Stunt: not null },
                $"[{cell}] the host flies the bot on its own run of the course");
            ctx.Check(peers[1].RaceBots == null && copy is { Pilot: null, Stunt: null, RemoteOwned: true },
                $"ABLE-TO-FAIL CONTROL: [{cell}] the guest flies no bot and times no run for it");
            if (hostRace.Of(2) is not { } hostRacer || guestRace.Of(2) is not { } guestRacer)
            {
                ctx.Check(false, $"[{cell}] both races hold the bot's racer");
                return;
            }

            int limit = Steps(hostRace.WindowSeconds + StuntRace.FinalRunCap);
            int steps = 0;
            while (guestRacer.BestTime == null && steps < limit)
            {
                steps++;
                foreach (var peer in peers)
                {
                    Step(peer, steps);
                }
            }

            ctx.Check(hostRacer.BestTime is > 0f && guestRacer.BestTime == hostRacer.BestTime,
                $"[{cell}] the bot's run on the host reaches the guest's board (host {Time(hostRacer)}, guest {Time(guestRacer)}, {steps} steps)");
            var row = RaceRows.Of(guestRace.Standings(), guestRace.ZoneCount).FirstOrDefault(r => r.Racer.Index == 2);
            ctx.Check(row != null && row.Name == Callsign && row.Best == Time(hostRacer),
                $"[{cell}] the guest's race board shows it under the bot's callsign ({row?.Place} {row?.Name} {row?.Best})");
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ambient.Restore();
        }
    }

    // The course's data gates, and its chapter.
    private static string Course(TestContext ctx, string chapter)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, chapter), $"{chapter} gamez");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, SessionSpec.StuntRaceMission), $"{chapter}/{SessionSpec.StuntRaceMission} zrdr");
        return chapter;
    }

    // One launch flown until the bot completes a run, or for the steps given; the race's window and
    // its final run when none are. Answers the steps taken.
    private static int Fly(TestContext ctx, SessionSpec spec, bool objective, int? limit)
    {
        string cell = objective ? "objective" : "objective off";
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        ctx.Check(roster is { Length: 2 } && roster[PaneSeat].HasPane && roster[BotSeat] is { IsBot: true, Callsign: Callsign },
            $"[{cell}] the local launch seats a pane and the bot '{Callsign}'");
        if (roster == null)
        {
            return 0;
        }

        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            var session = end.Session;
            var bot = end.Built && session.SeatRigs.Count > BotSeat ? session.SeatRigs[BotSeat].Controller : null;
            ctx.Check(end.Built && session.Race != null && session.RaceBots != null && bot is { Stunt: not null, Race: not null, Pilot: not null },
                $"[{cell}] the session builds a race whose bot flies its own run of the course");
            if (session.Race is not { } race || session.RaceBots is not { } bots || bot is not { Stunt: { } run, Pilot: { } pilot })
            {
                return 0;
            }

            var racer = race.Of(BotSeat);
            ctx.Check(racer?.Callsign == Callsign && bot.Loadout == null && pilot.Gunner == null && pilot.Rocketeer == null,
                $"[{cell}] the bot races as '{racer?.Callsign}' and carries no weapons (loadout {bot.Loadout != null}, gunner {pilot.Gunner != null})");
            bots.BotObjective = objective;
            int steps = limit ?? Steps(race.WindowSeconds + StuntRace.FinalRunCap);
            return objective ? Completes(ctx, session, race, bot, steps) : Idles(ctx, race, run, session, steps);
        }
        finally
        {
            end.Close();
        }
    }

    // The bot flies each zone through its gates, crashed once after its second and returned to that
    // zone's exit. Its completed run reaches the board under its callsign, then it reruns.
    private static int Completes(TestContext ctx, GameSession session, StuntRace race, FlightController bot, int limit)
    {
        const string cell = "objective";
        var run = bot.Stunt!;
        var racer = race.Of(BotSeat)!;
        var cleared = new List<string>();
        run.ZoneCompleted += zone => cleared.Add($"{zone.DzName}@{zone.CompletedAt:0.0}");
        int steps = 0;
        bool crashed = false;
        while (racer.RunsFinished == 0 && steps < limit)
        {
            Step(session, ++steps);
            if (!crashed && run.CompletedCount == 2 && !bot.Crashed)
            {
                crashed = true;
                steps += CrashAndReturn(ctx, session, bot, run);
            }
        }

        ctx.Note($"[{cell}] {session.Spec.Chapter}: zones in the order cleared {string.Join(", ", cleared)}, best {Time(racer)} ({steps} steps)");
        ctx.Check(racer.RunsFinished == 1 && racer.BestTime is > 0f && cleared.Count >= run.TotalCount,
            $"[{cell}] the bot completes a run of all {run.TotalCount} zones (best {Time(racer)}, {racer.MostZones} zones at most, {steps} steps)");
        var row = RaceRows.Of(race.Standings(), race.ZoneCount).FirstOrDefault(r => r.Racer.Index == BotSeat);
        ctx.Check(row != null && row.Name == Callsign && row.Best == Time(racer),
            $"[{cell}] the race board shows its time under its callsign ({row?.Place} {row?.Name} {row?.Best} {row?.Runs})");

        int rerun = 0;
        while (racer.RunsStarted < 2 && race.MayStartRun && rerun < Steps(WaitSeconds))
        {
            Step(session, ++rerun);
        }

        ctx.Check(racer.RunsStarted == 2,
            $"[{cell}] once done it starts another run while the window is open ({racer.RunsStarted} started, window open {race.MayStartRun}, {rerun} steps)");
        return steps;
    }

    // An injected crash, the debug key's, then the unattended return. The bot stands at the last
    // zone's exit pose, its zones and clock kept. Answers the steps it took.
    private static int CrashAndReturn(TestContext ctx, GameSession session, FlightController bot, StuntMission run)
    {
        const string cell = "crash";
        var exit = run.ReturnPose();
        int zones = run.CompletedCount;
        float clock = run.Elapsed;
        bot.DebugForceCrash();
        int steps = 0;
        while (bot.Crashed && steps < Steps(WaitSeconds))
        {
            Step(session, ++steps);
        }

        float off = exit is { } pose ? bot.WorldPosition.DistanceTo(pose.Position) : float.PositiveInfinity;
        ctx.Note($"[{cell}] returned {off:0.0} m from the exit pose after {steps} steps, {run.CompletedCount} zones kept");
        ctx.Check(!bot.Crashed && off < ReturnReach && run.CompletedCount == zones && run.Elapsed > clock,
            $"[{cell}] crashed after {zones} zones, the bot returns unattended to the last zone's exit ({off:0.0} m off, {steps} steps, zones {run.CompletedCount}, clock {StuntMission.FormatTime(clock)} on to {StuntMission.FormatTime(run.Elapsed)})");
        return steps;
    }

    // ABLE-TO-FAIL CONTROL: the same bot from the same start with its objective off clears nothing.
    private static int Idles(TestContext ctx, StuntRace race, StuntMission run, GameSession session, int limit)
    {
        var racer = race.Of(BotSeat)!;
        for (int i = 1; i <= limit; i++)
        {
            Step(session, i);
        }

        ctx.Check(racer.RunsFinished == 0 && racer.MostZones == 0 && run.CompletedCount == 0,
            $"ABLE-TO-FAIL CONTROL: with its objective off the bot clears no zone and completes no run in {limit} steps ({racer.MostZones} zones, {racer.RunsFinished} runs)");
        return limit;
    }

    private static int Steps(float seconds) => Mathf.RoundToInt(seconds / GameClock.FixedDt);

    private static string Time(Racer racer) => racer.BestTime is { } best ? StuntMission.FormatTime(best) : "none";

    // The world's animation advances too, which opens the hangar doors the courses fly through. A
    // suite runs in one engine frame, so the runtime's own process never does.
    private static void Step(GameSession session, int step)
    {
        session._PhysicsProcess(GameClock.FixedDt);
        session.WorldRuntime?.Advance(GameClock.FixedDt);
        if (step % SyncSteps == 0)
        {
            SyncWorld(session);
        }
    }

    // The world's static colliders moved by its animation, pushed to the physics server, which a
    // suite in one engine frame never flushes (docs/verification.md INSTR-13).
    private static void SyncWorld(GameSession session)
    {
        foreach (var node in session.FindChildren("*", "StaticBody3D", recursive: true, owned: false))
        {
            if (node is StaticBody3D body && body.IsInsideTree())
            {
                PhysicsServer3D.BodySetState(body.GetRid(), PhysicsServer3D.BodyState.Transform, body.GlobalTransform);
            }
        }
    }
}
