using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A network stunt race on the wire, on <see cref="NetCombatSuites"/>'s rig. Two sessions
/// launch from the lobby's options over a loopback with latency, once clean and once lossy. The
/// guest times and reports its own runs. The host keeps the window, the board and the end, which
/// the guest's board replicates.</summary>
internal static class NetStuntRaceSuites
{
    private const ulong HostSeed = 0xC0FFEE95UL;

    private const string RaceChapter = "C1";

    // One way on the link, six steps at the fixed step. A guest released by the host's start word
    // then opens its window visibly late unless it catches up.
    private const double Latency = 0.1;

    // Generous against the start gate, the opening's five seconds and any retransmission.
    private const int StepLimit = 1200;

    // Steps between two zones of the guest's first run and of the host's run. The guest's first run
    // is the slower, and its final run, flown fast, the fastest of all.
    private const int GuestFirstGap = 40;
    private const int HostGap = 30;
    private const int FinalGap = 2;

    // How much of the window a pilot restarts into: its 3, 2, 1 then ends with a run in progress at time up.
    private const float LateRestartSeconds = 4.5f;

    // How far a seat may stand from the spawn on its GO step: one step's flight at race speed.
    private const float SpawnReach = 5f;

    private static readonly int HoldFrames = Mathf.RoundToInt(TapHoldButton.PadHoldSeconds / GameClock.FixedDt) + 2;

    [Suite("net-stunt-race-wire",
        "a two-machine Stunt Race from the lobby's options over a 100 ms loopback, then the same over one "
        + "with jitter and loss: the guest is released late but its opening count catches up and both "
        + "windows open on the host's step; both boards hold both pilots; the guest's own run reaches the "
        + "host's board and comes back to the guest's with its splits, and the host's run reaches the "
        + "guest's; both boards agree on the order and the best splits before and after a final run; a "
        + "guest run finished after time up counts while the guest's restart there is refused; the "
        + "guest's race does not end at its own cap but on the host's, after the host's race has ended")]
    internal static void ANetworkRaceKeepsOneBoard(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RaceChapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, RaceChapter), $"{RaceChapter} gamez");
        ctx.RequireData(missionZrdr, $"{RaceChapter}/{SessionSpec.StuntRaceMission} zrdr");

        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            Race(ctx, "clean", new LoopbackConditions(Latency, 0.0, 0.0), 9501, opensWithin: 1);
            Race(ctx, "lossy", new LoopbackConditions(Latency, 0.02, 0.25), 9502, opensWithin: 2);
        }
        finally
        {
            ambient.Restore();
        }
    }

    [Suite("net-stunt-race-end",
        "a two-machine Stunt Race's end over a 100 ms loopback: at the window's end the host's board offers "
        + "Restart and Lobby, the guest's says it waits for the host and offers Leave, and the guest's own "
        + "restart is refused; the host's Restart opens the next window on both machines, both "
        + "openings on one step with each local seat back on the spawn, an old window's line dropped; a guest "
        + "leaving mid-window keeps its best, ranked and marked left on the host's board, and its aeroplane "
        + "goes from the host's world, while the host's race runs on alone to the window's end; on a second "
        + "pair, the host's Lobby takes the guest to the lobby with the same race table on both machines")]
    internal static void ANetworkRaceEndsOnTheHostsWord(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RaceChapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, RaceChapter), $"{RaceChapter} gamez");
        ctx.RequireData(missionZrdr, $"{RaceChapter}/{SessionSpec.StuntRaceMission} zrdr");

        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            EndWindow(ctx);
            EndLobby(ctx);
        }
        finally
        {
            ambient.Restore();
        }
    }

    /// <summary>A lobby race's launch arguments. A race spec is not <c>--det</c>, so without a named
    /// store a guest's best would land in the player's own stunt scores.</summary>
    internal static string[] RaceArgs(TestContext ctx) =>
        new[] { "--mute", "--no-pads", $"--scores={System.IO.Path.Combine(ctx.ScratchDir, "net_race_scores.json")}" };

    private static void Race(TestContext ctx, string cell, LoopbackConditions link, int seed, int opensWithin)
    {
        var options = new DogfightOptionsMessage(1, (byte)DogfightLobby.EnvironmentOf(RaceChapter), (byte)DogfightMissionType.StuntRace,
            DogfightVictory.Time, 5, DogfightLobby.DefaultScore, false, DogfightLobby.DefaultLives, false);
        var rules = DogfightLobby.RulesOf(options);
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(RaceArgs(ctx)), DogfightLobby.ChapterOf(options.Environment),
            new[] { "player_pfighter" }, DogfightLobby.LaunchMode(options), vsTimeMinutes: rules.TimeLimitMinutes,
            vsLives: rules.Lives, vsAutoRespawn: rules.AutoRespawn, missionType: rules.MissionType);

        var mesh = LoopbackTransport.Mesh(2, link, new Random(seed));
        var roster = NetCombatSuites.Roster(2);
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i, i == 0 ? roster : null));
            }

            ctx.Check(ends.All(e => e.Built), $"[{cell}] two sessions build C1's IA1 race ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            var host = peers[0].SeatRigs[0].Controller!;
            var guest = peers[1].SeatRigs[1].Controller!;
            if (host.Race is not { } hostRace || guest.Race is not { } guestRace || host.Stunt is not { } hostRun || guest.Stunt is not { } guestRun)
            {
                ctx.Check(false, $"[{cell}] each machine's own seat races with a course");
                return;
            }

            foreach (var seat in new[] { host, guest })
            {
                seat.UseKeyboard = false;
                seat.PadDevices = Array.Empty<int>();
                seat.HoldActionForTest(InputAction.Respawn, false);
            }

            ctx.Check(!hostRace.Replicated && guestRace.Replicated
                      && new[] { hostRace, guestRace }.All(r => r.Racers.Select(x => (x.Index, x.Callsign)).SequenceEqual(roster.Select(s => (s.SeatIndex, s.Callsign)))),
                $"[{cell}] both boards hold both pilots in seat order under their callsigns, the guest's a replica ({Names(hostRace)} | {Names(guestRace)})");

            Opening(ctx, cell, peers, hostRace, guestRace, opensWithin);

            // Both pilots' first runs, flown together from GO: the host's the faster.
            for (int step = 1; step < StepLimit && (hostRun.CompletedCount < hostRun.TotalCount || guestRun.CompletedCount < guestRun.TotalCount); step++)
            {
                Step(peers);
                if (step % HostGap == 0 && hostRun.CompletedCount < hostRun.TotalCount)
                {
                    StuntRaceSuites.ClearZone(hostRun, hostRun.Zones[hostRun.CompletedCount]);
                }

                if (step % GuestFirstGap == 0 && guestRun.CompletedCount < guestRun.TotalCount)
                {
                    StuntRaceSuites.ClearZone(guestRun, guestRun.Zones[guestRun.CompletedCount]);
                }
            }

            float guestFirst = guestRun.Elapsed;
            float hostFirst = hostRun.Elapsed;
            var guestFirstSplits = Splits(guestRun);
            bool reached = Until(peers, () => hostRace.Of(1)!.RunsFinished == 1 && guestRace.Of(1)!.RunsFinished == 1
                                              && guestRace.Of(0)!.RunsFinished == 1);
            ctx.Check(reached && hostRace.Of(1)!.BestTime == guestFirst && hostRace.Of(1)!.Splits.SequenceEqual(guestFirstSplits)
                      && guestRace.Of(1)!.BestTime == guestFirst && guestRace.Of(1)!.Splits.SequenceEqual(guestFirstSplits),
                $"[{cell}] the guest's own run, {StuntMission.FormatTime(guestFirst)}, reaches the host's board and comes back to the guest's with its splits (host {hostRace.Of(1)!.BestTime}, guest {guestRace.Of(1)!.BestTime})");
            ctx.Check(reached && guestRace.Of(0)!.BestTime == hostFirst && guestRace.Of(0)!.Splits.SequenceEqual(Splits(hostRun)),
                $"[{cell}] and the host's own run, {StuntMission.FormatTime(hostFirst)}, reaches the guest's board with its splits ({guestRace.Of(0)!.BestTime})");
            Agree(ctx, cell, "after a run each", hostRace, guestRace, firstIndex: 0);

            FinalRun(ctx, cell, peers, host, guest, hostRace, guestRace, hostRun, guestRun);
            Cap(ctx, cell, peers, hostRace, guestRace);
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // The window's end, the host's Restart and a guest leaving mid-window, on one pair.
    private static void EndWindow(TestContext ctx)
    {
        const string cell = "window";
        var exits = new int[2];
        var ends = OpenEnds(ctx, 9503, exits);
        try
        {
            if (Seats(ctx, cell, ends) is not { } s)
            {
                return;
            }

            var spawn = s.Peers[0].SeatRigs[1].Controller!.WorldPosition;
            Opening(ctx, cell, s.Peers, s.HostRace, s.GuestRace, 1);
            FlyBoth(s, hostGap: HostGap, guestGap: GuestFirstGap);
            Until(s.Peers, () => s.GuestRace.Of(0)!.RunsFinished == 1 && s.GuestRace.Of(1)!.RunsFinished == 1);
            CloseWindow(s);
            bool ended = Until(s.Peers, () => s.HostRace.Ended && s.GuestRace.Ended);
            ctx.Check(ended, $"[{cell}] the window's end ends both races ({s.HostRace.Phase}/{s.GuestRace.Phase})");

            var hostBoard = s.Peers[0].Boards?.RaceBoard as UI.Screens.StuntRaceBoard;
            var guestBoard = s.Peers[1].Boards?.RaceBoard as UI.Screens.StuntRaceBoard;
            ctx.Check(MenuRows(hostBoard) == "Photo Mode|Restart|Lobby" && hostBoard!.WithheldLine == null,
                $"[{cell}] the host's board offers Restart and Lobby ({MenuRows(hostBoard)})");
            ctx.Check(MenuRows(guestBoard) == "Photo Mode|Leave" && guestBoard!.WithheldLine == UI.Screens.StuntRaceBoard.WaitingForHost,
                $"[{cell}] the guest's board says it waits for the host and offers Leave ({MenuRows(guestBoard)}, \"{guestBoard?.WithheldLine}\")");

            s.Guest.RerunRace!();
            Steps(s.Peers, 10);
            ctx.Check(s.GuestRace.Ended && s.HostRace.Ended && s.GuestRace.Window == 0 && s.HostRace.Window == 0,
                $"[{cell}] the guest's own restart opens no window anywhere ({s.HostRace.Phase} {s.HostRace.Window}/{s.GuestRace.Phase} {s.GuestRace.Window})");

            Reopen(ctx, cell, s, spawn, hostBoard, guestBoard);
            Leaving(ctx, cell, s, exits, hostBoard);
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // The host's Restart reaches the guest as a call ahead of the new window's lines. Both races go
    // to window 1 and both boards retire. Both openings end on one step, each local seat on the spawn.
    private static void Reopen(TestContext ctx, string cell, RaceSeats s, Vector3 spawn,
        UI.Screens.StuntRaceBoard? hostBoard, UI.Screens.StuntRaceBoard? guestBoard)
    {
        s.Host.RerunRace!();
        var opened = new[] { -1, -1 };
        var at = new Vector3[2];
        var races = new[] { s.HostRace, s.GuestRace };
        var seats = new[] { s.Host, s.Guest };
        int staleAt = -1;
        string stale = "never read";
        for (int step = 0; step < StepLimit && opened.Any(o => o < 0); step++)
        {
            Step(s.Peers);

            // A finished run of the old window, sent once the guest is in the new one. No line of the
            // new window follows it until a run starts, so a taken one would still stand a link later.
            if (staleAt < 0 && s.GuestRace.Window == 1)
            {
                staleAt = step;
                s.Peers[0].Wire.Link!.Broadcast(new RaceStandingMessage(1, 0, false, true, 1, 1, 1f, 1f, 5, 0, new float[5]), NetChannels.Events);
            }
            else if (staleAt >= 0 && step == staleAt + 12)
            {
                var read = s.GuestRace.Of(1)!;
                stale = read.BestTime == null && read.RunsFinished == 0 ? "dropped" : $"taken (best {read.BestTime}, {read.RunsFinished} finished)";
            }

            for (int machine = 0; machine < 2; machine++)
            {
                if (opened[machine] < 0 && races[machine].Window == 1 && races[machine].MayStartRun)
                {
                    opened[machine] = step;
                    at[machine] = seats[machine].WorldPosition;
                }
            }
        }

        ctx.Check(s.HostRace.Window == 1 && s.GuestRace.Window == 1 && s.Peers[1].Wire.Race?.CallsTaken == 1,
            $"[{cell}] the host's Restart opens window 1 on both machines, the guest's on the host's call ({s.HostRace.Window}/{s.GuestRace.Window})");
        ctx.Check(opened.All(o => o >= 0) && opened[1] >= opened[0] && opened[1] - opened[0] <= 1,
            $"[{cell}] both new openings end on the host's step or the guest's one after, never before ({opened[0]}, {opened[1]})");
        ctx.Check(at.All(p => p.DistanceTo(spawn) < SpawnReach),
            $"[{cell}] each local seat stands on the shared spawn at its GO ({string.Join(", ", at.Select(p => $"{p.DistanceTo(spawn):0.0} m"))})");
        ctx.Check(s.HostRace.Racers.All(r => r.RunsStarted == 0 && r.BestTime == null) && s.GuestRace.Racers.All(r => r.RunsStarted == 0 && r.BestTime == null),
            $"[{cell}] and both boards start the new window empty");

        hostBoard?._Process(GameClock.FixedDt);
        guestBoard?._Process(GameClock.FixedDt);
        ctx.Check(hostBoard is { Visible: false } && guestBoard is { Visible: false },
            $"[{cell}] both boards retire on the new window ({hostBoard?.Visible}/{guestBoard?.Visible})");

        ctx.Note($"[{cell}] the new openings ended on steps {opened[0]} and {opened[1]} after the host's Restart");
        ctx.Check(stale == "dropped", $"[{cell}] a line of the old window arriving in the new one is dropped ({stale})");
    }

    // The guest flies a fast run in the new window and walks out of it. Its row stays, ranked and
    // marked left, its aeroplane goes from the host's world, and the host's race runs on alone.
    private static void Leaving(TestContext ctx, string cell, RaceSeats s, int[] exits, UI.Screens.StuntRaceBoard? hostBoard)
    {
        FlyBoth(s, hostGap: HostGap, guestGap: FinalGap * 5);
        float guestBest = s.GuestRun.Elapsed;
        Until(s.Peers, () => s.HostRace.Of(0)!.RunsFinished == 1 && s.HostRace.Of(1)!.RunsFinished == 1);

        s.Peers[1].LeaveFlight();
        bool marked = Until(s.Peers, () => s.HostRace.Of(1)!.Left);
        var copy = s.Peers[0].SeatRigs[1].Controller!;
        var left = s.HostRace.Of(1)!;
        ctx.Check(exits[1] == 1 && marked && left.BestTime == guestBest && s.HostRace.Standings()[0] == left,
            $"[{cell}] the guest's Leave keeps its best, {StuntMission.FormatTime(guestBest)}, first and marked left on the host's board (left={left.Left}, best {left.BestTime})");
        ctx.Check(copy.Inert && !copy.InPlay && copy.ShakePivot is { Visible: false },
            $"[{cell}] and its aeroplane, ghost and label go from the host's world (inert={copy.Inert}, drawn={copy.ShakePivot?.Visible})");

        var host = new[] { s.Peers[0] };
        Steps(host, 120);
        ctx.Check(s.HostRace.Phase == StuntRacePhase.Open && s.HostRace.TimeLeft > 1f,
            $"[{cell}] the host's race runs on with one pilot left ({s.HostRace.Phase}, {s.HostRace.TimeLeft:0} s left)");
        s.HostRace.Advance(s.HostRace.TimeLeft - (3f * GameClock.FixedDt));
        bool over = Until(host, () => s.HostRace.Ended);
        ctx.Check(over && s.HostRace.WindowElapsed - s.HostRace.WindowSeconds < 2f * GameClock.FixedDt,
            $"[{cell}] and ends at the window's end ({s.HostRace.WindowElapsed:0.000} of {s.HostRace.WindowSeconds:0} s)");
        string first = hostBoard?.Rows.FirstOrDefault() ?? "";
        ctx.Check(first.StartsWith($"1st  {left.Callsign}{StuntRace.LeftSuffix}", StringComparison.Ordinal),
            $"[{cell}] the host's board ranks the guest first, marked left (\"{first}\")");
    }

    // The host's Lobby from an ended race: the guest's link hears the call, and both machines land
    // the same race table.
    private static void EndLobby(TestContext ctx)
    {
        const string cell = "lobby";
        var exits = new int[2];
        var ends = OpenEnds(ctx, 9504, exits);
        try
        {
            if (Seats(ctx, cell, ends) is not { } s)
            {
                return;
            }

            Opening(ctx, cell, s.Peers, s.HostRace, s.GuestRace, 1);
            FlyBoth(s, hostGap: HostGap, guestGap: GuestFirstGap);
            Until(s.Peers, () => s.GuestRace.Of(0)!.RunsFinished == 1 && s.GuestRace.Of(1)!.RunsFinished == 1);
            ctx.Check(Launcher.RaceLanding(s.HostRace) == null, $"ABLE-TO-FAIL CONTROL: [{cell}] a race still running lands no table");
            CloseWindow(s);
            Until(s.Peers, () => s.HostRace.Ended && s.GuestRace.Ended);
            var guestLink = s.Peers[1].Wire.Race!;
            ctx.Check(!guestLink.LobbyCalled && exits.All(e => e == 0),
                $"ABLE-TO-FAIL CONTROL: [{cell}] nobody is called to the lobby before the host leaves the board");

            s.Peers[0].LeaveFlight();
            bool called = Until(s.Peers, () => guestLink.LobbyCalled);
            var here = Launcher.RaceLanding(s.HostRace);
            var there = Launcher.RaceLanding(s.GuestRace);
            ctx.Check(exits[0] == 1 && called && s.Peers[0].Wire.Race!.CallsTaken == 0,
                $"[{cell}] the host's Lobby leaves its flight and calls the guest to the lobby ({exits[0]} exit, called={called})");
            ctx.Check(here is { Count: 2 } && there != null && here.SequenceEqual(there),
                $"[{cell}] and both machines land the same table ({Table(here)} | {Table(there)})");
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // Two sessions from the lobby's options over a clean 100 ms loopback, each exit counted.
    private static List<NetCombatSuites.Ends> OpenEnds(TestContext ctx, int seed, int[] exits)
    {
        var options = new DogfightOptionsMessage(1, (byte)DogfightLobby.EnvironmentOf(RaceChapter), (byte)DogfightMissionType.StuntRace,
            DogfightVictory.Time, 5, DogfightLobby.DefaultScore, false, DogfightLobby.DefaultLives, false);
        var rules = DogfightLobby.RulesOf(options);
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(RaceArgs(ctx)), DogfightLobby.ChapterOf(options.Environment),
            new[] { "player_pfighter" }, DogfightLobby.LaunchMode(options), vsTimeMinutes: rules.TimeLimitMinutes,
            vsLives: rules.Lives, vsAutoRespawn: rules.AutoRespawn, missionType: rules.MissionType);
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(Latency, 0.0, 0.0), new Random(seed));
        var roster = NetCombatSuites.Roster(2);
        var ends = new List<NetCombatSuites.Ends>();
        for (int i = 0; i < 2; i++)
        {
            int machine = i;
            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i, i == 0 ? roster : null,
                exitSession: () => exits[machine]++));
        }

        return ends;
    }

    // Each machine's own seat, its race and its course, with the seats off every device.
    private static RaceSeats? Seats(TestContext ctx, string cell, List<NetCombatSuites.Ends> ends)
    {
        ctx.Check(ends.All(e => e.Built), $"[{cell}] two sessions build C1's IA1 race ({string.Join(", ", ends.Select(e => e.Built))})");
        if (!ends.All(e => e.Built))
        {
            return null;
        }

        var peers = ends.Select(e => e.Session).ToArray();
        var host = peers[0].SeatRigs[0].Controller!;
        var guest = peers[1].SeatRigs[1].Controller!;
        if (host.Race is not { } hostRace || guest.Race is not { } guestRace || host.Stunt is not { } hostRun || guest.Stunt is not { } guestRun)
        {
            ctx.Check(false, $"[{cell}] each machine's own seat races with a course");
            return null;
        }

        foreach (var seat in new[] { host, guest })
        {
            seat.UseKeyboard = false;
            seat.PadDevices = Array.Empty<int>();
            seat.HoldActionForTest(InputAction.Respawn, false);
        }

        return new RaceSeats(peers, host, guest, hostRace, guestRace, hostRun, guestRun);
    }

    // A run each, flown together from the window's opening, every zone in course order.
    private static void FlyBoth(RaceSeats s, int hostGap, int guestGap)
    {
        for (int step = 1; step < StepLimit && (s.HostRun.CompletedCount < s.HostRun.TotalCount || s.GuestRun.CompletedCount < s.GuestRun.TotalCount); step++)
        {
            Step(s.Peers);
            if (step % hostGap == 0 && s.HostRun.CompletedCount < s.HostRun.TotalCount)
            {
                StuntRaceSuites.ClearZone(s.HostRun, s.HostRun.Zones[s.HostRun.CompletedCount]);
            }

            if (step % guestGap == 0 && s.GuestRun.CompletedCount < s.GuestRun.TotalCount)
            {
                StuntRaceSuites.ClearZone(s.GuestRun, s.GuestRun.Zones[s.GuestRun.CompletedCount]);
            }
        }
    }

    // Both machines' clocks to time up with nobody in a run, so the host's next step ends its race.
    private static void CloseWindow(RaceSeats s)
    {
        s.GuestRace.Advance(s.GuestRace.TimeLeft);
        s.HostRace.Advance(s.HostRace.TimeLeft);
    }

    private static string MenuRows(UI.Screens.ResultsBoard? board) =>
        board?.StandardMenu is { } menu ? string.Join("|", menu.Items.Select(i => i.Label)) : "(no menu)";

    private static string Table(IReadOnlyList<RaceTableRow>? rows) =>
        rows == null ? "(none)" : string.Join(" ", rows.Select(r => $"{r.Pilot}/{r.Best}{(r.Left ? "/left" : "")}"));

    // Steps both machines until each has released its start and opened its window. The guest hears
    // the host's start word a link later, so only its catch-up puts its window on the host's step.
    private static void Opening(TestContext ctx, string cell, GameSession[] peers, StuntRace hostRace, StuntRace guestRace, int within)
    {
        var released = new[] { -1, -1 };
        var opened = new[] { -1, -1 };
        var races = new[] { hostRace, guestRace };
        for (int step = 0; step < StepLimit && opened.Any(s => s < 0); step++)
        {
            Step(peers);
            for (int machine = 0; machine < 2; machine++)
            {
                if (released[machine] < 0 && !peers[machine].StartHeld)
                {
                    released[machine] = step;
                }

                if (opened[machine] < 0 && races[machine].MayStartRun)
                {
                    opened[machine] = step;
                }
            }
        }

        int lag = released[1] - released[0];
        float caught = peers[1].Wire.Race?.CaughtUp ?? 0f;
        int caughtSteps = Mathf.RoundToInt(caught / GameClock.FixedDt);
        ctx.Check(lag >= 3, $"[{cell}] the guest is released {lag} steps after the host, the link's latency (released at {released[0]}, {released[1]})");
        ctx.Check(opened.All(s => s >= 0) && opened[1] >= opened[0] && opened[1] - opened[0] <= within,
            $"[{cell}] yet the guest's window opens on the host's step or at most {within} after, never before ({opened[0]}, {opened[1]}), its opening having caught up {caughtSteps} steps ({caught:0.000} s)");
        ctx.Check(caughtSteps > 0 && Math.Abs(caughtSteps - lag) <= within,
            $"[{cell}] and the catch-up matches the lag the start word took ({caughtSteps} against {lag})");
    }

    // A late restart by both pilots puts both in a run at time up. The guest's run finished in the
    // final run counts, its restart there is refused, and the host's pilot is still in a run.
    private static void FinalRun(TestContext ctx, string cell, GameSession[] peers, FlightController host, FlightController guest,
        StuntRace hostRace, StuntRace guestRace, StuntMission hostRun, StuntMission guestRun)
    {
        float jump = hostRace.TimeLeft - LateRestartSeconds;
        hostRace.Advance(jump);
        guestRace.Advance(jump);
        foreach (var seat in new[] { host, guest })
        {
            seat.HoldActionForTest(InputAction.Respawn, true);
        }

        Steps(peers, HoldFrames);
        foreach (var seat in new[] { host, guest })
        {
            seat.HoldActionForTest(InputAction.Respawn, false);
        }

        bool counted = Until(peers, () => !host.StartCount.Running && !guest.StartCount.Running);
        Steps(peers, 6);
        StuntRaceSuites.ClearZone(hostRun, hostRun.Zones[0]);
        StuntRaceSuites.ClearZone(guestRun, guestRun.Zones[0]);
        bool up = Until(peers, () => hostRace.Phase == StuntRacePhase.FinalRun && guestRace.Phase == StuntRacePhase.FinalRun
                                     && guestRace.Of(0)!.CurrentZones == 1 && guestRace.Of(1)!.CurrentZones == 1);
        ctx.Check(counted && up && hostRace.Racers.All(r => r.InRun) && guestRace.Racers.All(r => r.InRun),
            $"[{cell}] both pilots restart late and are in a run at time up, on both boards ({hostRace.Phase}/{guestRace.Phase}, in a run {string.Join("", hostRace.Racers.Select(r => r.InRun ? 1 : 0))}/{string.Join("", guestRace.Racers.Select(r => r.InRun ? 1 : 0))})");

        int respawns = guest.RespawnCount;
        guest.HoldActionForTest(InputAction.Respawn, true);
        Steps(peers, HoldFrames);
        guest.HoldActionForTest(InputAction.Respawn, false);
        Step(peers);
        ctx.Check(guest.RespawnCount == respawns && !guest.StartCount.Running,
            $"[{cell}] the guest's restart in the final run is refused by the host's window ({guest.RespawnCount - respawns} respawn(s))");

        for (int k = 1; k < guestRun.TotalCount; k++)
        {
            Steps(peers, FinalGap);
            StuntRaceSuites.ClearZone(guestRun, guestRun.Zones[k]);
        }

        float guestFinal = guestRun.Elapsed;
        var finalSplits = Splits(guestRun);
        bool reached = Until(peers, () => hostRace.Of(1)!.RunsFinished == 2 && guestRace.Of(1)!.RunsFinished == 2);
        ctx.Check(reached && hostRace.Phase == StuntRacePhase.FinalRun && hostRace.Of(1)!.BestTime == guestFinal
                  && guestRace.Of(1)!.BestTime == guestFinal && guestRace.Of(1)!.Splits.SequenceEqual(finalSplits),
            $"[{cell}] the guest's run finished after time up, {StuntMission.FormatTime(guestFinal)}, counts as its best on both boards while the host's pilot flies on ({hostRace.Phase})");
        Agree(ctx, cell, "after the final run", hostRace, guestRace, firstIndex: 1);
    }

    // The guest's clock passes its cap and its race waits; the host's reaching the cap ends both.
    private static void Cap(TestContext ctx, string cell, GameSession[] peers, StuntRace hostRace, StuntRace guestRace)
    {
        guestRace.Advance(StuntRace.FinalRunCap + 10f);
        Steps(peers, 10);
        bool waited = guestRace.Phase == StuntRacePhase.FinalRun;

        // Three steps short of the host's cap, so its own step ends it and says so in the same step.
        hostRace.Advance(hostRace.FinalRunLeft - (3f * GameClock.FixedDt));
        int hostEnded = -1;
        int guestEnded = -1;
        for (int step = 0; step < StepLimit && guestEnded < 0; step++)
        {
            Step(peers);
            hostEnded = hostEnded < 0 && hostRace.Ended ? step : hostEnded;
            guestEnded = guestEnded < 0 && guestRace.Ended ? step : guestEnded;
        }

        var cut = hostRace.Of(0)!;
        ctx.Check(waited && hostEnded >= 0 && guestEnded >= hostEnded && !cut.InRun && cut.RunsFinished == 1,
            $"[{cell}] the guest's race runs past its own cap and ends on the host's, which cut the host pilot's run at the cap (guest waited {waited}, ended at steps {hostEnded}/{guestEnded}, host pilot finished {cut.RunsFinished})");
        Agree(ctx, cell, "at the end", hostRace, guestRace, firstIndex: 1);
    }

    // Both boards list the same order and every pilot's same best and splits.
    private static void Agree(TestContext ctx, string cell, string when, StuntRace hostRace, StuntRace guestRace, int firstIndex)
    {
        var a = hostRace.Standings();
        var b = guestRace.Standings();
        bool same = a.Select(r => r.Index).SequenceEqual(b.Select(r => r.Index)) && a[0].Index == firstIndex
            && a.Zip(b).All(p => p.First.BestTime == p.Second.BestTime && p.First.Splits.SequenceEqual(p.Second.Splits)
                                 && p.First.RunsFinished == p.Second.RunsFinished && p.First.RunsStarted == p.Second.RunsStarted);
        ctx.Check(same, $"[{cell}] {when}, both boards rank {string.Join(" ", a.Select(r => r.Callsign))} | {string.Join(" ", b.Select(r => r.Callsign))} with the same bests and splits");
    }

    private static List<float?> Splits(StuntMission run) => run.Zones.Select(z => (float?)z.CompletedAt).ToList();

    private static string Names(StuntRace race) => string.Join(" ", race.Racers.Select(r => $"{r.Index}:{r.Callsign}"));

    // Steps until the condition holds, as a lossy link reshuffles which words land together.
    private static bool Until(GameSession[] peers, Func<bool> condition)
    {
        for (int step = 0; step < StepLimit && !condition(); step++)
        {
            Step(peers);
        }

        return condition();
    }

    private static void Steps(GameSession[] peers, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            Step(peers);
        }
    }

    // Each session's own clock is framed first, as its rendered frame does. A held start frames no
    // time, and the clock readings the guest's catch-up takes stay as real as in play.
    private static void Step(GameSession[] peers)
    {
        foreach (var session in peers)
        {
            session.SimClock?.BeginFrame(GameClock.FixedDt);
            session._PhysicsProcess(GameClock.FixedDt);
        }
    }

    // One pair's own seats, races and courses.
    private sealed record RaceSeats(GameSession[] Peers, FlightController Host, FlightController Guest, StuntRace HostRace,
        StuntRace GuestRace, StuntMission HostRun, StuntMission GuestRun);
}
