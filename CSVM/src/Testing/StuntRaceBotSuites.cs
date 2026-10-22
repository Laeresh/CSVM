using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.World;
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
    private const string Rival = "Rival";

    // A window long enough for the longest course, minutes: the race may not end before the bot does.
    private const int WindowMinutes = 16;

    // How long a return or a rerun is given, seconds: the crash camera or the count, with room to spare.
    private const float WaitSeconds = 10f;

    // How far the return may stand from the last zone's exit pose, metres: a few steps of flight.
    private const float ReturnReach = 30f;

    // How often the world's moved colliders reach the physics server, in steps.
    private const int SyncSteps = 30;

    // How far out along a shielded end's approach the gate cell starts the bot, metres. Then how long
    // it is given to clear that zone, seconds.
    private const float ShieldedStartM = 400f;
    private const float ShieldedSeconds = 120f;

    // How long the field suite flies, seconds, and the turn either way over one leg of steering for a
    // zone it fails, degrees. Then the sum of a leg's turns both ways that it fails, three and a half
    // U-turns. An S of alternating U-turns adds 180 a turn, so three pass and four fail.
    private const float FieldSeconds = 360f;
    private const float FieldTurnDeg = 300f;
    private const float FieldAbsTurnDeg = 630f;

    // The bearing about an entry inside which two seats' handovers count as one path, and the share of
    // seat pairs that may.
    private const float OnePathDeg = 2f;
    private const float OnePathShare = 0.15f;

    // The seeds the field flies with line steering on, each a different field of approaches and fluffs.
    private static readonly ulong[] FieldSeeds = { Seed, 0x5EED2440UL };
    [Suite("stunt-race-bot",
        "a local stunt race of one pane and one ace bot over the run chapter's IA1 course: the launch seats "
        + "the bot, which carries guns but no ordnance and takes no target of its own, and races under its "
        + "callsign; it shoots open every weapon-hit structure blocking a zone, and C2's optional Ramses wall; the bot flies every Danger Zone "
        + "through its own gate test, steering between them and running each on its ribbon, locking on no "
        + "faster than the rail check's top lock speed; crashed "
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

    [Suite("stunt-race-bot-gate",
        "a local C2 stunt race of one pane and one ace bot, started on the far side of the Seaplane Hangar's "
        + "closed gate (kkgate) and heading in: a cast along the zone's route from that end strikes the gate "
        + "leaf before the propane tank, and from the other end the tank first; the bot comes round to the "
        + "open end, its own rounds destroy the tank, and it clears the hangar without crashing; ABLE-TO-FAIL "
        + "CONTROL: with no end shut, the same start crashes or clears it with the tank never shot")]
    internal static void ABotComesRoundToShootAShieldedGate(TestContext ctx)
    {
        string chapter = Course(ctx, "C2");
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), chapter, new[] { "player_pfighter" },
            MenuMode.Stunt, vsTimeMinutes: WindowMinutes, missionType: DogfightMissionType.StuntRace,
            bots: new[] { new VsBotEntry("player_fury", NetBotSkill.Ace, 0, Callsign) });
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            Shielded(ctx, spec, shut: true);
            Shielded(ctx, spec, shut: false);
        }
        finally
        {
            ambient.Restore();
        }
    }

    [Suite("stunt-race-bot-menu",
        "a menu launch of Instant Action Stunt Flying on C1 by one seat, with one bot row left on the join board: "
        + "with the race toggle on, the exit carries the row, which makes the run a race; the session seats "
        + "the bot after the pane under the row's callsign, racing on its own run of the course, and it clears "
        + "a Danger Zone; ABLE-TO-FAIL CONTROL: with the toggle off, its default, the same exit carries no row "
        + "and its spec seats no bot, a solo run, and a squadron mission carries no row with the toggle on")]
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
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == InstantActionFeature.StuntKey));
        var solo = ia.BuildExit(seats, bots: setup.Bots.LaunchEntries());
        var soloSpec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), solo.Chapter,
            solo.Seats.Select(s => s.PlaneNode).ToArray(), solo.Mode, solo.InstantAction, bots: solo.Bots);
        ctx.Check(!ia.RaceBots && solo.Bots == null && soloSpec is { Stunt: true, VsBots.Count: 0 },
            $"ABLE-TO-FAIL CONTROL: [{cell}] with the race toggle off the Stunt Flying exit leaves the row on the board, a solo run ({solo.Bots?.Count ?? 0} row(s), {soloSpec.VsBots.Count} bot(s))");
        ia.RaceBots = true;
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == "dogfight_squadron"));
        ctx.Check(ia.BuildExit(seats, bots: setup.Bots.LaunchEntries()).Bots == null,
            $"ABLE-TO-FAIL CONTROL: [{cell}] a squadron mission carries no bot row, the toggle on");
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == InstantActionFeature.StuntKey));
        var exit = ia.BuildExit(seats, bots: setup.Bots.LaunchEntries());
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), exit.Chapter,
            exit.Seats.Select(s => s.PlaneNode).ToArray(), exit.Mode, exit.InstantAction, bots: exit.Bots);
        ctx.Check(exit.Bots is { Count: 1 } && spec is { Stunt: true, VsBots.Count: 1 },
            $"[{cell}] with the race toggle on the Stunt Flying exit carries the join board's row into the spec ({exit.Bots?.Count ?? 0} row(s), {spec.VsBots.Count} bot(s))");
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

    [Suite("stunt-race-bot-final-run",
        "a local C1 stunt race of one pane and two ace bots, its window run down to time up after the opening: "
        + "the pane, crashed with half its crash camera's time left in the window, is still down with the "
        + "prompt offered when time up arrives (the control), and on the next step the prompt is withdrawn and "
        + "its run stops, and a tap returns nothing; the first bot, crashed in the final run, stays down past "
        + "its crash camera's time with its run stopped and is handed no view, while the pane, past its own "
        + "crash camera's time, spectates the second bot, the only racer its target key offers; the second "
        + "bot's crash stops the last run and ends the race, every seat down and nobody respawned")]
    internal static void AFinalRunKeepsEveryCrashDown(TestContext ctx)
    {
        const string cell = "final run";
        string chapter = Course(ctx, "C1");
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), chapter, new[] { "player_pfighter" },
            MenuMode.Stunt, vsTimeMinutes: WindowMinutes, missionType: DogfightMissionType.StuntRace,
            bots: new[] { new VsBotEntry("player_fury", NetBotSkill.Ace, 0, Callsign), new VsBotEntry("player_fury", NetBotSkill.Ace, 0, Rival) });
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        if (roster is not { Length: 3 })
        {
            ctx.Check(false, $"[{cell}] the local launch seats a pane and two bots ({roster?.Length ?? 0} seat(s))");
            return;
        }

        var ambient = NetCombatSuites.Ambient.Save();
        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            var session = end.Session;
            var seats = end.Built ? session.SeatRigs.Select(r => r.Controller).ToArray() : Array.Empty<FlightController?>();
            if (session.Race is not { } race || seats is not [{ Stunt: { } paneRun } pane, { Stunt: { } firstRun, IsBotSeat: true } first, { Stunt: not null, IsBotSeat: true } second])
            {
                ctx.Check(false, $"[{cell}] the session builds a race of a pane and two bots, each on its own run");
                return;
            }

            pane.UseKeyboard = false;
            pane.PadDevices = Array.Empty<int>();
            pane.HoldActionForTest(InputAction.Respawn, false);
            int steps = 0;
            void Run(int n)
            {
                for (int i = 0; i < n && !race.Ended; i++)
                {
                    Step(session, ++steps);
                }
            }

            while (!(race.Phase == StuntRacePhase.Open && race.Racers.All(r => r.InRun)) && steps < Steps(WaitSeconds))
            {
                Run(1);
            }

            // Half the crash camera's time short of time up, so the prompt still stands when it arrives.
            race.Advance(race.TimeLeft - (pane.CrashCameraSeconds / 2f));
            bool flying = !pane.Crashed;
            pane.DebugForceCrash();
            var respawns = seats.Select(s => s!.RespawnCount).ToArray();
            bool offered = true;
            while (race.Phase == StuntRacePhase.Open && steps < Steps(WaitSeconds * 2f))
            {
                offered &= pane.Crashed && pane.RespawnOffered;
                Run(1);
            }

            ctx.Check(flying && offered && race.Phase == StuntRacePhase.FinalRun && race.Racers.All(r => r.InRun),
                $"ABLE-TO-FAIL CONTROL: [{cell}] the pane, crashed short of time up, is down with the prompt offered until time up arrives, every racer in a run ({race.Phase}, flying before={flying}, offered throughout={offered})");

            Run(1);
            bool withdrawn = pane.Crashed && !pane.RespawnOffered && paneRun.Stopped && !race.Of(PaneSeat)!.InRun;
            pane.HoldActionForTest(InputAction.Respawn, true);
            Run(2);
            pane.HoldActionForTest(InputAction.Respawn, false);
            Run(1);
            ctx.Check(withdrawn && pane.Crashed && pane.RespawnCount == respawns[PaneSeat] && !race.Ended,
                $"[{cell}] on the step after time up the prompt is withdrawn and the pane's run stops, and a tap returns nothing (withdrawn={withdrawn}, stopped={paneRun.Stopped}, crashed={pane.Crashed}, {pane.RespawnCount - respawns[PaneSeat]} respawn(s))");

            first.DebugForceCrash();
            Run(Steps(Mathf.Max(pane.CrashCameraSeconds, first.CrashCameraSeconds)) + 10);
            ctx.Check(first.Crashed && first.RespawnCount == respawns[BotSeat] && firstRun.Stopped && !race.Of(BotSeat)!.InRun
                      && race.Of(BotSeat + 1)!.InRun && !race.Ended,
                $"[{cell}] the first bot's crash in the final run stays down past its crash camera's time and stops its run, the race running on ({first.RespawnCount - respawns[BotSeat]} respawn(s), stopped={firstRun.Stopped}, ended={race.Ended})");
            var watcher = session.SpectatorCameras.Count == 1 ? session.SpectatorCameras[0] : null;
            var candidates = watcher?.LockCandidates?.Invoke().ToList();
            ctx.Check(!first.Spectating && !first.CameraOwned && pane.Spectating && ReferenceEquals(watcher?.Follow, second)
                      && candidates is { Count: 1 } && ReferenceEquals(candidates[0], second),
                $"[{cell}] the bot is handed no view, while the pane spectates the second bot, the only racer its target key offers ({session.SpectatorCameras.Count} spectator(s), bot spectating={first.Spectating}, pane spectating={pane.Spectating}, {candidates?.Count} offered)");

            second.DebugForceCrash();
            Run(Steps(WaitSeconds));
            ctx.Check(race.Ended && seats.All(s => s!.Crashed) && seats.Select(s => s!.RespawnCount).SequenceEqual(respawns)
                      && race.Racers.All(r => !r.InRun),
                $"[{cell}] the second bot's crash stops the last run and ends the race, every seat down and nobody respawned (ended={race.Ended}, crashed {string.Join("/", seats.Select(s => s!.Crashed))}, in a run {string.Join("/", race.Racers.Select(r => r.InRun))})");
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

    [Suite("stunt-race-bot-field",
        "a local C1 stunt race of one pane and eight veteran bots in mixed planes, all starting from the race's one point, "
        + "flown six minutes on each of two seeds: no bot circles or weaves on its way into a zone, its track turning under 300 "
        + "degrees either way and under 630 both ways added up over any leg of steering for one zone; and the bots' approaches "
        + "differ, at most 15 percent of the seat pairs that steer into one zone handing it to their pilots within 2 degrees of "
        + "each other about its entry; ABLE-TO-FAIL CONTROLS: an S of four alternating U-turns stays under 300 either way and "
        + "fails 630 both ways, where three pass; the first seed's field with its line steering off, each bot steering straight for the zone's one "
        + "lead-in point, circles there and hands its zones over from bunched approaches")]
    internal static void ABotFieldReachesEachApproachWithoutCircling(TestContext ctx)
    {
        string chapter = Course(ctx, "C1");
        string[] planes = { "player_bhawk", "player_fury", "player_pfighter", "player_peacemaker", "player_fbrand", "player_kestrel", "player_warhawk", "player_brigand" };
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), chapter, new[] { "player_pfighter" },
            MenuMode.Stunt, vsTimeMinutes: WindowMinutes, missionType: DogfightMissionType.StuntRace,
            bots: planes.Select((plane, i) => new VsBotEntry(plane, NetBotSkill.Veteran, 0, $"Field{i + 1}")).ToArray());
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            // ABLE-TO-FAIL CONTROL of the measure: an S of alternating U-turns nets no turn either way.
            SteerLeg S(int uTurns)
            {
                var s = new SteerLeg(0, 0, false, "S", 0, 0f);
                for (int step = 1; step <= uTurns * 180; step++)
                {
                    s.Step(Mathf.DegToRad(Mathf.Min(step % 360, 360 - (step % 360))), StuntBotObjective.SteersIn, step);
                }

                return s;
            }

            var (three, four) = (S(3), S(4));
            ctx.Check(four is { PeakTurnDeg: < FieldTurnDeg, AbsTurnDeg: >= FieldAbsTurnDeg } && three.AbsTurnDeg < FieldAbsTurnDeg,
                $"ABLE-TO-FAIL CONTROL: [S] four alternating U-turns stay under {FieldTurnDeg:0} degrees either way and fail {FieldAbsTurnDeg:0} both ways, and three pass ({four}; {three})");
            foreach (ulong seed in FieldSeeds)
            {
                if (Field(ctx, spec, planes, lineSteering: true, seed) is { } on)
                {
                    ctx.Check(on.Worst is { PeakTurnDeg: < FieldTurnDeg } && on.WorstBoth is { AbsTurnDeg: < FieldAbsTurnDeg },
                        $"[field {seed:X}] no bot's track turns {FieldTurnDeg:0} degrees either way, or {FieldAbsTurnDeg:0} both ways, over a leg of steering for one zone ({on.Legs} legs, worst {on.Worst}, worst both ways {on.WorstBoth})");
                    ctx.Check(on.Pairs > 0 && on.OnePath <= on.Pairs * OnePathShare,
                        $"[field {seed:X}] the bots' approaches differ: {on.OnePath} of {on.Pairs} seat pairs hand one zone over within {OnePathDeg:0} degrees of each other, at most {OnePathShare * 100f:0} percent");
                }
            }

            if (Field(ctx, spec, planes, lineSteering: false, Seed) is { } off)
            {
                ctx.Check(off.Worst is { PeakTurnDeg: >= 360f } && off.OnePath > off.Pairs * OnePathShare,
                    $"ABLE-TO-FAIL CONTROL: [point] with line steering off a bot circles a lead-in point (worst {off.Worst}) and the approaches bunch ({off.OnePath} of {off.Pairs} seat pairs within {OnePathDeg:0} degrees)");
            }
        }
        finally
        {
            ambient.Restore();
        }
    }

    // The live structures any zone's bots shoot: those its route runs into, and any optional target.
    private static List<DestructibleRegistry.Instance> Targets(StuntMission run, FlightController bot) =>
        bot.Destructibles is { } structures
            ? run.Zones.SelectMany(z => StuntRaceBots.Targets(z, structures, bot.WorldStruckAlong)).Distinct().ToList()
            : new List<DestructibleRegistry.Instance>();

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
            ctx.Check(racer?.Callsign == Callsign && bot.Loadout != null && pilot.Gunner is { AutoTarget: false, Target: null } && pilot.Rocketeer == null,
                $"[{cell}] the bot races as '{racer?.Callsign}' with guns and no target of its own (loadout {bot.Loadout != null}, gunner auto {pilot.Gunner?.AutoTarget}, rocketeer {pilot.Rocketeer != null})");
            ctx.Check(bots.RunsSawWorld,
                $"ABLE-TO-FAIL CONTROL: [{cell}] the run table's world ray, at fill time, strikes the built world below {run.Zones[0].PathName}'s entry");
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
        LeadInsClear(ctx, run, bot);
        var targets = Targets(run, bot);
        var cleared = new List<string>();
        run.ZoneCompleted += zone => cleared.Add($"{zone.DzName}@{zone.CompletedAt:0.0}");
        int steps = 0;
        bool crashed = false;
        bool approaching = false;
        float lastMps = 0f;
        float fastestLockMps = 0f;
        int locks = 0;
        while (racer.RunsFinished == 0 && steps < limit)
        {
            Step(session, ++steps);

            // The approach locks on at the speed the step before it left.
            var mode = bot.Pilot!.Machine?.Mode;
            if (approaching && mode == AiMode.NavigatingDangerZone)
            {
                locks++;
                fastestLockMps = Mathf.Max(fastestLockMps, lastMps);
            }

            approaching = mode == AiMode.ApproachingDangerZone;
            lastMps = bot.WorldVelocity.Length();
            if (!crashed && run.CompletedCount == 2 && !bot.Crashed)
            {
                crashed = true;
                steps += CrashAndReturn(ctx, session, bot, run);
                approaching = false;
            }
        }

        ctx.Note($"[{cell}] {session.Spec.Chapter}: zones in the order cleared {string.Join(", ", cleared)}, best {Time(racer)} ({steps} steps), fastest of {locks} locks {fastestLockMps:0.0} m/s");
        ctx.Check(racer.RunsFinished == 1 && racer.BestTime is > 0f && cleared.Count >= run.TotalCount,
            $"[{cell}] the bot completes a run of all {run.TotalCount} zones (best {Time(racer)}, {racer.MostZones} zones at most, {steps} steps)");
        ctx.Check(locks > 0 && fastestLockMps <= StuntBotObjective.TopLockSpeedMps,
            $"[{cell}] the bot locks onto each ribbon no faster than the rail check's top lock speed ({locks} locks, fastest {fastestLockMps:0.0} m/s, top {StuntBotObjective.TopLockSpeedMps:0} m/s)");
        // A ram destroys a structure too, with no shooter, so only the bot's own id proves it shot.
        var shot = targets.Where(s => s.Status == DestructibleRegistry.State.Destroyed && s.LastShooter == bot.PlayerIndex).ToList();
        ctx.Check(shot.Count == targets.Count,
            $"[{cell}] the bot's own rounds destroy every weapon-hit structure a zone's route runs into, and every optional target: {shot.Count} of {targets.Count} ({string.Join(", ", targets.Select(s => $"{s.Def.Name} {s.Status} by {s.LastShooter}"))})");
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

    // Every lead-in the course's zones would take from either end, probed along its straight through
    // the built world. Each zone must keep an end the bot can fly.
    private static void LeadInsClear(TestContext ctx, StuntMission run, FlightController bot)
    {
        const string cell = "lead-in";
        var entry = run.Zones[0].Route!.End(false);
        ctx.Check(bot.WorldBlocksLine(entry, entry + (Vector3.Down * 3000f)),
            $"ABLE-TO-FAIL CONTROL: [{cell}] the probe sees the built world: a ray 3 km down from {run.Zones[0].PathName}'s entry strikes it");
        var probed = new List<string>();
        int stranded = 0;
        foreach (var zone in run.Zones)
        {
            bool flyable = false;
            foreach (bool far in new[] { false, true })
            {
                if (StuntBotObjective.LeadInFor(zone, far, blocked: null) is not { } metres)
                {
                    continue;
                }

                bool blocked = false;
                if (metres > 0f)
                {
                    var from = zone.Route!.WithLeadIn(far, metres).End(far);
                    var to = zone.Route.End(far);
                    blocked = bot.WorldBlocksLine(from, to);
                    probed.Add($"{zone.PathName}{(far ? " far" : "")} {from.DistanceTo(to):0} m {(blocked ? "BLOCKED" : "clear")}");
                }

                flyable |= !blocked;
            }

            stranded += flyable ? 0 : 1;
        }

        ctx.Note($"[{cell}] {bot.Stunt?.TotalCount} zones, straights probed: {(probed.Count == 0 ? "none needed" : string.Join(", ", probed))}");
        ctx.Check(stranded == 0, $"[{cell}] every zone has an end the rail carries through whose lead-in, if any, the world leaves clear ({stranded} without)");
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

    // The field flown on `seed` for FieldSeconds or until every bot finishes a run. It keeps each seat's
    // steering legs, and its handovers about each entry where the bot steered into the zone.
    private static FieldRun? Field(TestContext ctx, SessionSpec spec, string[] planes, bool lineSteering, ulong seed)
    {
        string cell = $"{(lineSteering ? "field" : "point")} {seed:X}";
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        var end = roster?.Length == planes.Length + 1 ? NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, seed, roster) : null;
        try
        {
            if (end is not { Built: true, Session: { Race: { } race, RaceBots: { } bots } session })
            {
                ctx.Check(false, $"[{cell}] the session builds a race of a pane and {planes.Length} bots ({roster?.Length ?? 0} seat(s))");
                return null;
            }

            bots.LineSteering = lineSteering;
            var seats = Enumerable.Range(1, planes.Length).ToArray();
            var open = new Dictionary<int, SteerLeg>();
            var legs = new List<SteerLeg>();
            var prior = new Dictionary<int, StuntBotOrder>();
            var fluffs = new Dictionary<int, int>();
            var handovers = new Dictionary<string, Dictionary<int, float>>();
            int limit = Steps(FieldSeconds);
            int steps = 0;
            while (steps < limit && !seats.All(s => race.Of(s)!.RunsFinished > 0))
            {
                Step(session, ++steps);
                foreach (int seat in seats)
                {
                    var bot = session.SeatRigs[seat].Controller!;
                    var order = bots.Orders.GetValueOrDefault(seat);
                    var before = prior.GetValueOrDefault(seat);
                    prior[seat] = order;
                    if (order.Leg == StuntBotLeg.Fluff && before.Leg != StuntBotLeg.Fluff)
                    {
                        fluffs[seat] = fluffs.GetValueOrDefault(seat) + 1;
                    }

                    bool steering = !bot.Crashed && order.Leg == StuntBotLeg.Steer;
                    if (open.TryGetValue(seat, out var leg) && !(steering && leg.Zone == order.Zone && leg.Far == order.FarEnd))
                    {
                        legs.Add(leg);
                        open.Remove(seat);
                    }

                    if (order.Zone < 0 || bot.Stunt!.Zones[order.Zone].Route is not { } route)
                    {
                        continue;
                    }

                    if (order.StartsRun && before.Leg == StuntBotLeg.Steer && before.Zone == order.Zone)
                    {
                        // Its bearing about the entry, off the ribbon's line.
                        var into = route.EndTangentInto(order.FarEnd);
                        var to = route.End(order.FarEnd) - bot.WorldPosition;
                        to.Y = 0f;
                        into = new Vector3(into.X, 0f, into.Z).Normalized();
                        string zone = route.Name + (order.FarEnd ? " far" : "");
                        handovers.TryAdd(zone, new Dictionary<int, float>());
                        handovers[zone].TryAdd(seat, Mathf.RadToDeg(Mathf.Atan2(to.Cross(into).Y, to.Dot(into))));
                    }

                    if (steering)
                    {
                        var v = bot.WorldVelocity;
                        float heading = Mathf.Atan2(v.X, v.Z);
                        open.TryAdd(seat, new SteerLeg(seat, order.Zone, order.FarEnd, route.Name, steps, heading));
                        open[seat].Step(heading, order.Why, steps);
                    }
                }
            }

            legs.AddRange(open.Values);
            var gaps = handovers.Values.SelectMany(h => h.Values.SelectMany((a, i) => h.Values.Skip(i + 1).Select(b => Mathf.Abs(a - b)))).ToList();
            var run = new FieldRun(legs.MaxBy(l => l.PeakTurnDeg), legs.MaxBy(l => l.AbsTurnDeg), legs.Count, gaps.Count(g => g < OnePathDeg), gaps.Count, legs.Sum(l => l.Misses));
            ctx.Note($"[{cell}] steering legs (seat, zone, seconds, largest turn either way, turn both ways, misses sent round): {string.Join("; ", legs)}");
            ctx.Note($"[{cell}] after {steps * GameClock.FixedDt:0} s, best runs and zones: {string.Join(", ", seats.Select(s => $"{planes[s - 1]} {Time(race.Of(s)!)} {race.Of(s)!.MostZones}"))}");
            ctx.Note($"[{cell}] handover bearings about each entry, degrees: {string.Join("; ", handovers.Select(h => $"{h.Key} {string.Join(" ", h.Value.Values.Select(b => Mathf.RoundToInt(b)))}"))}");
            ctx.Note($"[{cell}] fluffs per seat: {string.Join(", ", seats.Select(s => $"s{s} {fluffs.GetValueOrDefault(s)}"))}");
            ctx.Note($"[{cell}] {run.Misses} misses of the cone sent round; worst leg {run.Worst}, both ways {run.WorstBoth}; {run.OnePath} of {run.Pairs} seat pairs within {OnePathDeg:0} degrees");
            return run;
        }
        finally
        {
            end?.Close();
        }
    }

    // The bot started out along the approach to a zone's shielded end, flown until it clears that
    // zone or first crashes.
    private static void Shielded(TestContext ctx, SessionSpec spec, bool shut)
    {
        string cell = shut ? "gate" : "gate unshut";
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        var end = roster == null ? null : NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            var session = end?.Session;
            var bot = end is { Built: true } && session!.SeatRigs.Count > BotSeat ? session.SeatRigs[BotSeat].Controller : null;
            var structures = bot?.Destructibles;
            var zone = bot?.Stunt?.Zones.FirstOrDefault(z => z.Route is { } r && structures != null
                && StuntRaceBots.Unseen(r, true, structures, bot.WorldStruckAlong) != null);
            if (session?.RaceBots is not { } bots || structures == null || zone?.Route is not { } route
                || StuntRaceBots.Unseen(route, true, structures, bot!.WorldStruckAlong) is not { } gate)
            {
                ctx.Check(false, $"[{cell}] the race builds, and a cast along a zone's route from its far end strikes a structure off its damage node first");
                return;
            }

            if (shut)
            {
                ctx.Check(StuntRaceBots.Unseen(route, false, structures, bot.WorldStruckAlong) == null,
                    $"[{cell}] a cast along {zone.PathName} from its far end strikes '{gate.Def.Name}' off its damage node first, and from its near end on it");
            }

            // Every other zone counts as cleared, so this one is the bot's only pick.
            foreach (var other in bot.Stunt!.Zones)
            {
                other.Completed = other != zone;
            }

            bots.EndsShut = shut;
            var into = route.EndTangentInto(true);
            into = new Vector3(into.X, 0f, into.Z).Normalized();
            bot.RespawnAt(route.End(true) - (into * ShieldedStartM), route.End(true));
            int steps = 0;
            bool crashed = false;
            while (!zone.Completed && !crashed && steps < Steps(ShieldedSeconds))
            {
                Step(session, ++steps);
                crashed = bot.Crashed;
            }

            bool shot = gate.Status == DestructibleRegistry.State.Destroyed && gate.LastShooter == bot.PlayerIndex;
            string seen = $"{zone.PathName} {(zone.Completed ? "cleared" : "open")}, {(crashed ? "crashed" : "no crash")}, '{gate.Def.Name}' {gate.Status} by {gate.LastShooter}, {steps} steps";
            if (shut)
            {
                ctx.Check(zone.Completed && shot && !crashed, $"[{cell}] the bot comes round to the open end, shoots the tank and clears the zone ({seen})");
            }
            else
            {
                ctx.Check(!shot, $"ABLE-TO-FAIL CONTROL: [{cell}] with no end shut the bot never shoots the tank ({seen})");
            }
        }
        finally
        {
            end?.Close();
        }
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
    // One field run: its worst steering leg either way and both ways, and its leg count. Then its handover
    // seat pairs within OnePathDeg and in all. Misses counts its bots' go-rounds after a missed cone.
    private sealed record FieldRun(SteerLeg? Worst, SteerLeg? WorstBoth, int Legs, int OnePath, int Pairs, int Misses);

    // One leg of a bot steering for one zone end. PeakTurnDeg is the largest its signed track turn
    // reached either way since the leg began, degrees, so a full circle is 360. AbsTurnDeg adds its turns
    // up both ways, so an S counts too. Misses counts the times it was sent round after steering in.
    private sealed class SteerLeg(int seat, int zone, bool far, string name, int from, float heading)
    {
        private readonly int _from = from;
        private float _lastHeading = heading;
        private float _turnRad;
        private float _absRad;
        private string _why = "";
        private int _to = from;

        public int Zone => zone;

        public bool Far => far;

        public float PeakTurnDeg { get; private set; }

        public float AbsTurnDeg => Mathf.RadToDeg(_absRad);

        public int Misses { get; private set; }

        public void Step(float heading, string why, int step)
        {
            float turn = Mathf.Wrap(heading - _lastHeading, -Mathf.Pi, Mathf.Pi);
            _turnRad += turn;
            _absRad += Mathf.Abs(turn);
            _lastHeading = heading;
            PeakTurnDeg = Mathf.Max(PeakTurnDeg, Mathf.Abs(Mathf.RadToDeg(_turnRad)));
            Misses += _why == StuntBotObjective.SteersIn && why != StuntBotObjective.SteersIn ? 1 : 0;
            _why = why;
            _to = step;
        }

        public override string ToString() =>
            $"s{seat} {name}{(far ? " far" : "")} {(_to - _from) * GameClock.FixedDt:0} s {PeakTurnDeg:0} deg {AbsTurnDeg:0} deg {Misses}";
    }
}
