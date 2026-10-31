using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Bots in Capture the Flag, between a host and a guest in one process on the chapter's
/// <c>MP2</c> map and <see cref="NetCombatSuites"/>'s rig. The host and the guest fly lobby team 2,
/// a bot alone flies team 1, and the host flies the bot and decides every flag.</summary>
internal static class NetCtfBotSuites
{
    private const ulong HostSeed = 0xC7FB0700UL;

    private const int HostSeat = 0;
    private const int GuestSeat = 1;
    private const int BotSeat = 2;

    private const int SettleSteps = 20;

    // The longest the bot is given to take the flag, then to bring it home: three minutes of sim each.
    private const int MaxLegSteps = 10800;

    // How far from the enemy flag the bot starts, facing away, and how high above it.
    private const float StartRange = 1500f;
    private const float StartAbove = 200f;

    // An ask, the host's decision and its table on the guest, with room to spare.
    private const int AskSteps = 10;

    // How high over a flag the guest is put to take it, inside the reach.
    private const float Over = 15f;

    private const float QuickRespawn = 0.5f;

    // How far off the bot's nose the guest is put as the competing enemy. Then how long the bot's
    // ranking is given to take it, and how long the carrier is watched held against it.
    private const float RivalAheadM = 400f;
    private const int RankSteps = 300;
    private const int KeepSteps = 300;

    // The people loop high over the field and far outside the bot's attack radius, as in
    // net-capture-the-flag.
    private const float ParkHeight = 1500f;
    private const float ParkOut = 4000f;

    private const string TrackedFlight = "--hold=0.3,0,0,1";

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-ctf-bot",
        "a host and a guest on the chapter's MP2 map, both on team 2, against a bot on team 1: the bot, started "
        + "past the enemy flag facing away, flies to it, takes it through the host's own flag decision "
        + "and carries it to its own base, where the host scores the capture to the bot's seat on both "
        + "machines; once the host's pilot takes the bot's team's flag, kilometres outside the bot's "
        + "attack radius, the bot takes that carrier as its target over the guest, the enemy its ranking had "
        + "taken 400 m off its nose, and keeps it while the guest stays nearer; and the "
        + "ABLE-TO-FAIL CONTROL, the bot from the same start with the objective off, takes no flag and "
        + "scores nothing in the steps the objective needed for its capture")]
    internal static void ABotTakesTheEnemyFlagHome(TestContext ctx)
    {
        var spec = Spec(ctx);
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            if (Open(ctx, spec, ends) is not { } peers)
            {
                return;
            }

            Park(peers);
            Lockstep(SettleSteps, peers);
            int steps = Capture(ctx, peers);
            Chase(ctx, peers);
            Control(ctx, peers, steps);
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

    private static SessionSpec Spec(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        return SessionSpec.Parse(new[] { "--vs" }
            .Concat(NetCombatSuites.Arena(ctx, mission: SessionSpec.CtfMission))
            .Concat(new[] { "--players=1", "--mute", "--no-pads", "--ctf", TrackedFlight, "--vs-kills=1000" })
            .ToArray());
    }

    // Both sessions on a clean loopback, the host carrying the roster with the bot. Null when a
    // build fails or a machine builds no flags.
    private static GameSession[]? Open(TestContext ctx, SessionSpec spec, List<NetCombatSuites.Ends> ends)
    {
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = "player_pfighter", TeamId = 2 },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = "player_fbrand", TeamId = 2 },
            NetSeats.Bot(0, BotSeat, "flagger", "player_pfighter", team: 1),
        };
        NetSeats.Validate(roster, hostPeer: 0);
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4155));
        ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, teamNames: TeamNames));
        ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, teamNames: TeamNames));
        ctx.Check(ends.All(e => e.Built), $"both sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
        if (!ends.All(e => e.Built))
        {
            return null;
        }

        var peers = ends.Select(e => e.Session).ToArray();
        ctx.Check(peers.All(p => p.Dogfight?.Flags?.Flags.Rows is { Count: 2 }), $"both machines build a flag for each team");
        var bot = peers[0].SeatRigs[BotSeat].Controller;
        ctx.Check(bot is { IsBotSeat: true, Pilot.Gunner: not null }, $"the host flies the bot seat on an armed AI pilot");
        return peers.All(p => p.Dogfight?.Flags != null) && bot?.Pilot?.Gunner != null ? peers : null;
    }

    // Quick returns, and both people looping high over the field, kilometres from both flags.
    private static void Park(GameSession[] peers)
    {
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.Rule = pilot.Rule with { AutoRespawnAfter = QuickRespawn };
            }
        }

        for (int seat = 0; seat < 2; seat++)
        {
            ParkSeat(peers, seat);
        }
    }

    private static void ParkSeat(GameSession[] peers, int seat)
    {
        var flags = Flags(peers[0]);
        var mid = (flags.HomeOf(1) + flags.HomeOf(2)) / 2f;
        var across = (flags.HomeOf(2) - flags.HomeOf(1)).Cross(Vector3.Up).Normalized();
        var high = mid + (across * (ParkOut + (seat * 600f))) + (Vector3.Up * ParkHeight);
        peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
    }

    // The bot takes team 2's flag through the host's decision and brings it to team 1's base. There
    // the host scores the capture to its seat. The steps the whole run took, for the control.
    private static int Capture(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[BotSeat].Controller!;
        var scores = host.Dogfight!.Match.Scores;
        Start(host, bot);
        Lockstep(1, peers);
        var run = bot.Pilot!.ObjectiveRun;
        var enemy = Flags(host).HomeOf(2);
        ctx.Check(run is { Flying: true } && run.Base.DistanceTo(enemy) < FlagMatch.Reach,
            $"the bot {bot.WorldPosition.DistanceTo(enemy):0} m from team 2's flag, facing away, flies a run to it ({(run == null ? "none" : $"{run.Leg} to {run.Base}")} against {enemy})");

        int taken = 0;
        for (; taken < MaxLegSteps && Flags(host).RowOf(2) is not { State: FlagState.Held }; taken++)
        {
            Lockstep(1, peers);
        }

        Lockstep(AskSteps, peers);
        ctx.Check(peers.All(p => Flags(p).RowOf(2) is { State: FlagState.Held, Holder: BotSeat }),
            $"and takes it through the host's decision in {taken} step(s), held by the bot on both machines ({Rows(peers)})");

        int home = 0;
        for (; home < MaxLegSteps && Flags(host).RowOf(2) is { State: FlagState.Held }; home++)
        {
            Lockstep(1, peers);
        }

        Lockstep(AskSteps, peers);
        ctx.Check(peers.All(p => Flags(p).RowOf(2) is { State: FlagState.Home } && p.Dogfight!.Match.ScoreOf(BotSeat) == scores.FlagCapture
                                 && p.Dogfight!.Match.TeamScoreOf(1) == scores.FlagCapture),
            $"and carries it to team 1's base in {home} more step(s), the host scoring the capture, {scores.FlagCapture}, to the bot's seat on both machines ({Scores(peers)})");
        ctx.Note($"the bot took the flag in {taken} step(s) and brought it home in {home} more, in from bearing {AiPilot.HeadingDegOf(run?.Approach ?? Vector3.Zero):0}");
        return taken + home + (2 * AskSteps);
    }

    // The guest, an enemy of the bot's, is put off the bot's nose, and the bot's ranking takes it.
    // The host's own pilot then takes team 1's flag kilometres away, outside the ranking's reach.
    // The bot takes the carrier in the guest's place and keeps it while the guest stays nearer.
    private static void Chase(TestContext ctx, GameSession[] peers)
    {
        var host = peers[0];
        var bot = host.SeatRigs[BotSeat].Controller!;
        var gunner = bot.Pilot!.Gunner!;
        var raider = host.SeatRigs[HostSeat].Controller!;
        var rival = host.SeatRigs[GuestSeat].Controller!;
        Start(host, bot);
        Lockstep(1, peers);
        var near = bot.WorldPosition + (bot.NoseDirection * RivalAheadM);
        peers[GuestSeat].SeatRigs[GuestSeat].Controller!.RespawnAt(near, near + (bot.NoseDirection * 100f));
        int ranked = 0;
        for (; ranked < RankSteps && !ReferenceEquals(gunner.Target, rival); ranked++)
        {
            Lockstep(1, peers);
        }

        ctx.Check(ReferenceEquals(gunner.Target, rival),
            $"ABLE-TO-FAIL CONTROL: with no flag taken the bot's ranking takes the guest {bot.WorldPosition.DistanceTo(rival.WorldPosition):0} m off its nose, in {ranked} step(s) ({TargetPool.TargetLabel(gunner.Target)})");

        var over = Flags(host).HomeOf(1) + (Vector3.Up * Over);
        raider.RespawnAt(over, over + (Vector3.Right * 100f));
        Lockstep(AskSteps, peers);

        // Parked high with the flag, clear of its base's walls, so the carrier lives while watched.
        ParkSeat(peers, HostSeat);
        bool kept = true;
        bool nearer = true;
        for (int step = 0; step < KeepSteps; step++)
        {
            Lockstep(1, peers);
            kept &= ReferenceEquals(gunner.Target, raider);
            nearer &= bot.WorldPosition.DistanceTo(rival.WorldPosition) < bot.WorldPosition.DistanceTo(raider.WorldPosition);
        }

        float range = bot.WorldPosition.DistanceTo(raider.WorldPosition);
        ctx.Check(peers.All(p => Flags(p).RowOf(1) is { State: FlagState.Held, Holder: HostSeat }) && kept && nearer
                  && range > (bot.Pilot.Machine?.AttackRange ?? 0f),
            $"once the host's pilot takes team 1's flag {range:0} m away, past the {bot.Pilot.Machine?.AttackRange ?? 0f:0} m attack radius, the bot takes the carrier as its target over the guest {bot.WorldPosition.DistanceTo(rival.WorldPosition):0} m away and keeps it for {KeepSteps} step(s) (kept {kept}, guest nearer throughout {nearer}; {TargetPool.TargetLabel(gunner.Target)}; {Rows(peers)})");

        // The carrier goes down, its flag floats, and the throw runs out with nobody near it.
        raider.DebugForceCrash(null);
        Lockstep((int)(FlagMatch.ThrowSeconds / GameClock.FixedDt) + (4 * AskSteps), peers);
        ParkSeat(peers, HostSeat);
        ParkSeat(peers, GuestSeat);
        Lockstep(AskSteps, peers);
    }

    // The ABLE-TO-FAIL CONTROL: the same bot from the same start with the objective off, for as many
    // steps as the objective needed for its capture. It takes no flag and scores nothing.
    private static void Control(TestContext ctx, GameSession[] peers, int steps)
    {
        var host = peers[0];
        var flags = host.Dogfight!.Flags!;
        var bot = host.SeatRigs[BotSeat].Controller!;
        int score = host.Dogfight!.Match.ScoreOf(BotSeat);
        flags.BotObjective = false;
        try
        {
            Start(host, bot);
            bool held = false;
            for (int step = 0; step < steps; step++)
            {
                Lockstep(1, peers);
                held |= flags.Flags.Carried(BotSeat) != 0;
            }

            ctx.Check(!held && host.Dogfight!.Match.ScoreOf(BotSeat) == score && bot.Pilot!.ObjectiveRun is not { Flying: true },
                $"ABLE-TO-FAIL CONTROL: with the objective off the same bot from the same start takes no flag and scores nothing in {steps} step(s) (held {held}, score {score} to {host.Dogfight!.Match.ScoreOf(BotSeat)})");
        }
        finally
        {
            flags.BotObjective = true;
        }
    }

    // The bot set down past team 2's flag on the line from team 1's, above it, facing away from both.
    private static void Start(GameSession host, FlightController bot)
    {
        var flags = Flags(host);
        var enemy = flags.HomeOf(2);
        var own = flags.HomeOf(1);
        var away = new Vector3(enemy.X - own.X, 0f, enemy.Z - own.Z).Normalized();
        var at = enemy + (away * StartRange) + (Vector3.Up * StartAbove);
        bot.Pilot!.Gunner!.Target = null;
        bot.RespawnAt(at, at + (away * 100f));
    }

    private static FlagMatch Flags(GameSession session) => session.Dogfight!.Flags!.Flags;

    private static void Lockstep(int steps, params GameSession[] sessions)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in sessions)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }

    private static string Rows(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(" ", Flags(p).Rows.Select(r => $"{r.Team}:{r.State}/{r.Holder}"))));

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 3).Select(p.Dogfight!.Match.ScoreOf))
            + $" teams {p.Dogfight!.Match.TeamScoreOf(1)}/{p.Dogfight!.Match.TeamScoreOf(2)}"));
}
