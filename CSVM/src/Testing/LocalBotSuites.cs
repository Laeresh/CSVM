using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Overlays;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.BotSuiteHelper;

namespace CSVM.Testing;

/// <summary>A local Dogfight with bots: one pane and its bots on a seat roster with no wire. The
/// roster is the one a command-line <c>--vs --vs-bots=</c> launch, or a menu launch carrying the
/// join board's bots, builds through <see cref="SeatFields.LocalVersusField"/>. This machine is the authority over every seat, as a
/// network host is over its bots. It scores each death off the Downed report, places each return
/// from its own rotation and runs the rematch. The session rig is <see cref="NetCombatSuites"/>'s,
/// opened with no transport.</summary>
internal static class LocalBotSuites
{
    private const ulong Seed = 0xB07B07C2UL;

    private const int PaneSeat = 0;
    private const int BotSeat = 1;
    private const int SecondBotSeat = 2;

    // How long a check that something does NOT happen keeps watching, in sim steps.
    private const int HeldSteps = 60;

    // The most steps any one wait is given past the crash camera time.
    private const int WaitSteps = 240;

    // A lethal claim's damage share, the debug kill key's own: far past any hull's pools.
    private const float LethalScale = 1e6f;

    [Suite("versus-local-bot",
        "a local Dogfight of one pane and its bots on a seat roster with no wire: the pane and the "
        + "bot kill each other and both score, each death posts its line naming the bot by its "
        + "callsign, both come back on the local rotation, a rematch places both on their opening "
        + "entries; with Auto Respawn off the bot still returns after the crash camera while the "
        + "pane waits for Fire Guns; with one life a living bot keeps the match running and the "
        + "last bot's death ends it on nobody left to fight; two panes with no bots keep the "
        + "rosterless path, scoring, naming and returning as before; and a menu launch of one seat "
        + "and two join board bots flies them in board order, the Random one on a stock plane")]
    internal static void LocalBotsScoreRespawnAndRematch(TestContext ctx)
    {
        var auto = NetCombatSuites.MatchSpec(ctx, out var table, "--vs-bots=1");
        var pressed = NetCombatSuites.MatchSpec(ctx, out _, "--vs-bots=1", "--vs-no-respawn");
        var limited = NetCombatSuites.MatchSpec(ctx, out _, "--vs-bots=2", "--vs-lives=1");
        var panes = NetCombatSuites.MatchSpec(ctx, out _, "--players=2");
        var (menu, board) = MenuLaunch(ctx, NetCombatSuites.MatchSpec(ctx, out _));
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            Fly(ctx, auto, "auto respawn", session => KillsReturnsAndRematch(ctx, session, table));
            Fly(ctx, pressed, "no auto respawn", session => ReturnsUnasked(ctx, session));
            Fly(ctx, limited, "one life", session => SpendsItsLife(ctx, session));
            PanesAlone(ctx, panes, table);
            Fly(ctx, menu, "menu launch", session => FliesTheBoardsBots(ctx, session, board));
        }
        finally
        {
            ambient.Restore();
        }
    }

    // One local launch, built as the launcher builds it, flown through the readings and freed.
    private static void Fly(TestContext ctx, SessionSpec spec, string cell, Action<GameSession> readings)
    {
        var roster = SeatFields.LocalVersusField(spec, ctx.MessagesPath);
        ctx.Check(roster is { Length: > 1 } && roster[0].HasPane
                  && roster.Skip(1).All(s => s is { IsBot: true, FlownHere: true } && s.PeerId == roster[0].PeerId),
            $"[{cell}] the local launch seats its pane first and then {spec.VsBots.Count} bot(s) on the pane's own peer ({Seats(roster)})");
        if (roster == null)
        {
            return;
        }

        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            ctx.Check(end.Built, $"[{cell}] the session builds");
            if (!end.Built || !Seated(ctx, cell, end.Session, roster.Length))
            {
                return;
            }

            readings(end.Session);
        }
        finally
        {
            end.Close();
        }
    }

    // A pane rig and a pilot-flown bot rig per roster seat, and nothing on any wire. The bot's
    // return is placed here, never asked of a host.
    private static bool Seated(TestContext ctx, string cell, GameSession session, int seats)
    {
        var bot = session.SeatRigs.Count > BotSeat ? session.SeatRigs[BotSeat].Controller : null;
        var pane = session.Rigs.Count > 0 ? session.Rigs[0].Controller : null;
        ctx.Check(session.Wire.Link == null && session.NetSeats.Count == seats && session.SeatRigs.Count == seats
                  && session.Rigs.Count == 1,
            $"[{cell}] one pane and {seats} seats with no wire (link {session.Wire.Link != null}, {session.SeatRigs.Count} seat rig(s), {session.Rigs.Count} pane(s))");
        if (bot == null || pane == null)
        {
            ctx.Check(false, $"[{cell}] both the pane and the bot have an aeroplane");
            return false;
        }

        ctx.Check(!bot.IsHumanPiloted && !bot.RemoteOwned && bot.Pilot is { Gunner.PlayersPreferred: false, Machine: not null }
                  && bot.PlayerIndex == BotSeat,
            $"[{cell}] the bot flies on an armed AI pilot under its seat index (human {bot.IsHumanPiloted}, shooter {bot.PlayerIndex})");
        ctx.Check(bot.Pilot?.Rocketeer is { WingmanRule: true, FiresOnFailedRoll: true },
            $"[{cell}] and its ordnance takes the wingman rule with a failed roll launching, as a network bot's does");
        ctx.Check(bot.RespawnRequest == null && bot.RespawnPlacement != null
                  && pane.RespawnRequest == null && pane.RespawnPlacement != null,
            $"[{cell}] every seat's return is placed by this machine's rotation, none asked of a host (bot asks {bot.RespawnRequest != null}, pane asks {pane.RespawnRequest != null})");

        // ABLE-TO-FAIL CONTROL. The pane on the same roster is a person's, so the reading above
        // is the bot seat's, not a build that made every rig alike.
        ctx.Check(pane is { IsHumanPiloted: true, Pilot: null, RemoteOwned: false } && pane.MessageStack != null,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the pane is a person's, with its own message stack");

        foreach (var rig in session.SeatRigs)
        {
            if (rig.Controller?.Pilot?.Gunner is { } gunner)
            {
                gunner.AutoTarget = false;
                gunner.Target = null;
            }
        }

        return true;
    }

    // Both directions of a kill, each scored here off the Downed report. Both returns are placed
    // off the local rotation. Then the rematch zeroes the board and places both seats.
    private static void KillsReturnsAndRematch(TestContext ctx, GameSession session, IReadOnlyList<SpawnPoint> table)
    {
        const string cell = "auto respawn";
        var match = session.Dogfight!.Match;
        int kill = match.Scores.Kill;
        var pane = session.SeatRigs[PaneSeat].Controller!;
        var bot = session.SeatRigs[BotSeat].Controller!;
        string callsign = session.NetSeats[BotSeat].Callsign;
        int paneOpening = EntryAt(table, pane);
        int botOpening = EntryAt(table, bot);
        ctx.Note($"[{cell}] openings: pane on entry {paneOpening}, bot '{callsign}' on entry {botOpening}");
        Lift(pane);
        Lift(bot);

        // The bot kills the pane.
        var before = Board(session);
        pane.Body!.TakeProjectileHit(DebugKillTarget.LethalWeapon(bot)!, pane.WorldPosition, 0, bot.PlayerIndex, LethalScale);
        var after = Expect(before, killer: BotSeat, victim: PaneSeat, kill);
        int steps = StepUntil(() => pane.Crashed && Board(session).SequenceEqual(after), session);
        ctx.Check(pane.Crashed && Board(session).SequenceEqual(after),
            $"[{cell}] the bot's kill of the pane scores the bot ({Scoreboard(session)}, {steps} step(s))");
        ctx.Check(PaneLines(pane).Contains(callsign, StringComparison.Ordinal),
            $"[{cell}] and the pane's death line names the bot by its callsign '{callsign}' ({PaneLines(pane)})");
        steps = UntilBack(pane, session);
        int entry = EntryAt(table, pane);
        ctx.Check(!pane.Crashed && entry >= 0,
            $"[{cell}] the pane comes back on its own after the crash camera, on rotation entry {entry} ({steps} step(s))");

        // The pane kills the bot, whose pilot holds a quarry it must not keep through the return.
        Lift(pane);
        Lift(bot);
        var gunner = bot.Pilot!.Gunner!;
        var pilot = bot.Pilot;
        gunner.TakeTarget(pane, default, 0d);
        before = Board(session);
        bot.Body!.TakeProjectileHit(DebugKillTarget.LethalWeapon(pane)!, bot.WorldPosition, 0, pane.PlayerIndex, LethalScale);
        after = Expect(before, killer: PaneSeat, victim: BotSeat, kill);
        steps = StepUntil(() => bot.Crashed && Board(session).SequenceEqual(after), session);
        ctx.Check(bot.Crashed && Board(session).SequenceEqual(after),
            $"[{cell}] the pane's kill of the bot scores the pane ({Scoreboard(session)}, {steps} step(s))");
        ctx.Check(PaneLines(pane).Contains(callsign, StringComparison.Ordinal),
            $"[{cell}] and the pane reads the bot's death by its callsign ({PaneLines(pane)})");
        steps = UntilBack(bot, session);
        entry = EntryAt(table, bot);
        ctx.Check(!bot.Crashed && entry >= 0 && ReferenceEquals(bot.Pilot, pilot) && gunner.Target == null,
            $"[{cell}] the bot comes back on rotation entry {entry} on the same pilot with no quarry ({FlightController.TargetLabel(gunner.Target)}, {steps} step(s))");

        // The rematch: the bot down and the pane far off its opening point, then both put back.
        Lift(pane);
        bot.DebugForceCrash();
        StepUntil(() => bot.Crashed, session);
        session.Dogfight.Restart();
        ctx.Check(Board(session).All(row => row == (0, 0, 0)) && !match.Completed,
            $"[{cell}] the rematch zeroes the board ({Scoreboard(session)})");
        ctx.Check(!pane.Crashed && !bot.Crashed && EntryAt(table, pane) == paneOpening && EntryAt(table, bot) == botOpening,
            $"[{cell}] and places both on their opening entries (pane {EntryAt(table, pane)} against {paneOpening}, bot {EntryAt(table, bot)} against {botOpening}, bot down {bot.Crashed})");
    }

    // Auto Respawn off. The bot and the pane go down on one step, each to the other. The bot is back
    // after the crash camera time; the pane waits for its pilot's Fire Guns.
    private static void ReturnsUnasked(TestContext ctx, GameSession session)
    {
        const string cell = "no auto respawn";
        var pane = session.SeatRigs[PaneSeat].Controller!;
        var bot = session.SeatRigs[BotSeat].Controller!;
        ctx.Check(!bot.RespawnOnFire && bot.AutoRespawnAfter == VersusDirector.RespawnDelay,
            $"[{cell}] the bot respawns on the crash camera's {bot.AutoRespawnAfter} s without waiting for Fire Guns (waits {bot.RespawnOnFire})");
        ctx.Check(pane.RespawnOnFire && pane.AutoRespawnAfter == bot.AutoRespawnAfter,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the pane on the same launch waits for Fire Guns after the same {pane.AutoRespawnAfter} s (waits {pane.RespawnOnFire})");

        Lift(pane);
        Lift(bot);
        int kill = session.Dogfight!.Match.Scores.Kill;
        var before = Board(session);
        bot.DebugForceCrash(pane.PlayerIndex);
        pane.DebugForceCrash(bot.PlayerIndex);
        var after = Expect(Expect(before, killer: PaneSeat, victim: BotSeat, kill), killer: BotSeat, victim: PaneSeat, kill);
        ctx.Check(Board(session).SequenceEqual(after),
            $"[{cell}] each kill scores its killer ({Scoreboard(session)})");

        int due = Mathf.RoundToInt(VersusDirector.RespawnDelay / GameClock.FixedDt);
        int down = UntilBack(bot, session);
        ctx.Check(!bot.Crashed && down >= due - 1 && down <= due + 2,
            $"[{cell}] the downed bot returns on its own after the crash camera time ({down} step(s) against {due})");
        Step(HeldSteps, session);
        ctx.Check(pane.Crashed && !bot.Crashed,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the pane, downed on the same step, still waits ({Downs(session)})");
    }

    // One life each and two bots. The first bot's death spends its seat, which stays down with the
    // match still running, so the living bot is the pane's opponent. The second bot's death leaves
    // the pane alone, which ends the match on nobody left to fight.
    private static void SpendsItsLife(TestContext ctx, GameSession session)
    {
        const string cell = "one life";
        var match = session.Dogfight!.Match;
        var bot = session.SeatRigs[BotSeat].Controller!;
        var other = session.SeatRigs[SecondBotSeat].Controller!;
        ctx.Check(match.Lives == 1 && match.PlayerCount == 3, $"[{cell}] three pilots on one life ({match.PlayerCount}, {match.Lives})");
        foreach (var rig in session.SeatRigs)
        {
            Lift(rig.Controller!);
        }

        // Unattributed deaths, so no pilot's score reaches the kill target first.
        bot.DebugForceCrash();
        int steps = StepUntil(() => match.OutOfLives(BotSeat), session);
        Step(HeldSteps + Mathf.RoundToInt(VersusDirector.RespawnDelay / GameClock.FixedDt), session);
        ctx.Check(bot is { Crashed: true, Spectating: true },
            $"[{cell}] the downed bot stays down past the crash camera, out of lives ({Downs(session)}, {steps} step(s))");
        ctx.Check(!match.Completed,
            $"ABLE-TO-FAIL CONTROL: [{cell}] with the second bot alive the match runs ({Scoreboard(session)})");

        other.DebugForceCrash();
        steps = StepUntil(() => match.Completed, session);
        ctx.Check(match is { Completed: true, AllAlone: true },
            $"[{cell}] the last bot's death leaves the pane alone and ends the match on nobody left to fight (complete {match.Completed}, alone {match.AllAlone}, {steps} step(s))");
    }

    // Two panes and no bots keep the path a local match always took: no roster, the panes as seats.
    // The scoring, the death line and the return match the roster path's.
    private static void PanesAlone(TestContext ctx, SessionSpec spec, IReadOnlyList<SpawnPoint> table)
    {
        const string cell = "panes alone";
        ctx.Check(SeatFields.LocalVersusField(spec, ctx.MessagesPath) == null,
            $"[{cell}] a local launch with no bots builds no roster");
        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster: null);
        try
        {
            ctx.Check(end.Built, $"[{cell}] the session builds");
            if (!end.Built)
            {
                return;
            }

            var session = end.Session;
            ctx.Check(session.NetSeats.Count == 0 && session.Rigs.Count == 2 && session.SeatRigs.SequenceEqual(session.Rigs),
                $"[{cell}] its seats are its two panes ({session.NetSeats.Count} roster seat(s), {session.SeatRigs.Count} seat rig(s), {session.Rigs.Count} pane(s))");
            var victim = session.Rigs[0].Controller!;
            var killer = session.Rigs[1].Controller!;
            Lift(victim);
            Lift(killer);
            var after = Expect(Board(session), killer: 1, victim: 0, session.Dogfight!.Match.Scores.Kill);
            victim.Body!.TakeProjectileHit(DebugKillTarget.LethalWeapon(killer)!, victim.WorldPosition, 0, killer.PlayerIndex, LethalScale);
            int steps = StepUntil(() => victim.Crashed && Board(session).SequenceEqual(after), session);
            string tag = UI.Boards.SplitScreen.PlayerTag(1);
            ctx.Check(victim.Crashed && Board(session).SequenceEqual(after) && PaneLines(victim).Contains(tag, StringComparison.Ordinal),
                $"[{cell}] P2's kill of P1 scores P2 and P1's pane names its killer {tag} ({Scoreboard(session)}; {PaneLines(victim)}, {steps} step(s))");
            steps = UntilBack(victim, session);
            ctx.Check(!victim.Crashed && EntryAt(table, victim) >= 0,
                $"[{cell}] and P1 comes back on rotation entry {EntryAt(table, victim)} ({steps} step(s))");
        }
        finally
        {
            end.Close();
        }
    }

    // A one-seat Dogfight exit off the player setup the join board writes, with two bot rows: a stock
    // Fury at ace, then a Random one. The spec is the launcher's own FromMenu over the suite's line.
    private static (SessionSpec Spec, IReadOnlyList<VsBotEntry> Bots) MenuLaunch(TestContext ctx, SessionSpec cli)
    {
        var setup = new UI.Menu.PlayerSetupFeature();
        setup.SetRoster(UI.Menu.Original.OriginalRosters.Roster(Array.Empty<Flight.Hangar.CustomPlaneDef>()));
        var seat = setup.Join(new UI.Menu.MenuIdleSource())!;
        setup.Select(seat);
        setup.Confirm(seat);
        setup.Bots.CallsignPool = Session.Roster.BotSeats.CallsignPool(Mech3.Messages.Load(ctx.MessagesPath));
        setup.AddBot();
        setup.AddBot();
        int ace = setup.Bots.Rows[0].Id;
        setup.Bots.SetAirframe(ace, Flight.Hangar.StockAirframes.IdOf("player_fury") ?? 0);
        setup.Bots.SetSkill(ace, NetBotSkill.Ace);
        var exit = setup.BuildExit(ctx.Chapter, MenuMode.Versus, _ => Array.Empty<int>());
        var spec = SessionSpec.FromMenu(cli, exit.Chapter, exit.Seats.Select(s => s.PlaneNode).ToArray(), exit.Mode,
            vsKills: exit.Match?.KillTarget, vsTimeMinutes: exit.Match?.TimeLimitMinutes, bots: exit.Bots);
        return (spec, exit.Bots ?? Array.Empty<VsBotEntry>());
    }

    // The launch's roster is the board's: its pane, then each bot row by callsign. The named plane and
    // tier are kept and the Random plane is drawn from the stock eleven. Each bot flies an armed pilot.
    private static void FliesTheBoardsBots(TestContext ctx, GameSession session, IReadOnlyList<VsBotEntry> board)
    {
        const string cell = "menu launch";
        var seats = session.NetSeats;
        ctx.Check(board.Count == 2 && seats.Count == 3 && seats[0].HasPane
                  && seats.Skip(1).Select(s => s.Callsign).SequenceEqual(board.Select(b => b.Callsign)),
            $"[{cell}] the session seats the pane and then the board's bots by callsign ({Seats(seats)})");
        ctx.Check(seats.Count == 3 && seats[1] is { PlaneNode: "player_fury", Skill: NetBotSkill.Ace }
                  && seats[2].Skill == NetBotSkill.Veteran && Flight.Hangar.StockAirframes.Nodes.Contains(seats[2].PlaneNode),
            $"[{cell}] the edited bot keeps its Fury at ace and the Random one flies a stock plane ({string.Join(", ", seats.Select(s => $"{s.PlaneNode}:{s.Skill}"))})");
        ctx.Check(session.SeatRigs.Count == 3
                  && session.SeatRigs.Skip(1).All(r => r.Controller is { IsHumanPiloted: false, Pilot: not null }),
            $"[{cell}] both bots fly on an armed AI pilot ({session.SeatRigs.Count} seat rig(s))");
    }

    // Steps until the aeroplane flies again, and answers how many steps that took.
    private static int UntilBack(FlightController plane, GameSession session)
    {
        int due = Mathf.RoundToInt(VersusDirector.RespawnDelay / GameClock.FixedDt);
        int steps = 0;
        while (plane.Crashed && steps < due + WaitSteps)
        {
            Step(1, session);
            steps++;
        }

        return steps;
    }

    private static (int Score, int Kills, int Deaths)[] Board(GameSession session)
    {
        var match = session.Dogfight!.Match;
        return Enumerable.Range(0, match.PlayerCount)
            .Select(s => (match.ScoreOf(s), match.KillsOf(s), match.DeathsOf(s))).ToArray();
    }

    // The board one charged kill moves to. The killer takes a kill's score and a kill, and the
    // victim a death, as player.zrd's values have it.
    private static (int Score, int Kills, int Deaths)[] Expect((int Score, int Kills, int Deaths)[] before,
        int killer, int victim, int kill)
    {
        var after = ((int Score, int Kills, int Deaths)[])before.Clone();
        after[killer] = (after[killer].Score + kill, after[killer].Kills + 1, after[killer].Deaths);
        after[victim] = (after[victim].Score, after[victim].Kills, after[victim].Deaths + 1);
        return after;
    }

    private static string Downs(GameSession session) =>
        string.Join(",", session.SeatRigs.Select(r => r.Controller is { } c ? (c.Crashed ? "down" : "up") : "-"));

    private static string Seats(IReadOnlyList<NetSeat>? roster) =>
        roster == null ? "no roster" : string.Join(", ", roster.Select(s => $"{s.SeatIndex}:{s.Callsign}{(s.IsBot ? " bot" : "")}"));

    private static int StepUntil(Func<bool> done, GameSession session)
    {
        for (int step = 1; step <= WaitSteps; step++)
        {
            Step(1, session);
            if (done())
            {
                return step;
            }
        }

        return WaitSteps;
    }

    private static void Step(int steps, GameSession session)
    {
        for (int i = 0; i < steps; i++)
        {
            session._PhysicsProcess(GameClock.FixedDt);
        }
    }
}
