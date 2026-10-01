using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Launch;
using CSVM.Session.World;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Capture the Flag between whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig and the chapter's <c>MP2</c> map. The host and the first
/// guest fly lobby team 1 and the second guest team 2. Every flag rule is read on all three
/// machines. The host decides, and each guest applies its table or floats a flag on its own.
/// </summary>
internal static class NetFlagSuites
{
    private const ulong HostSeed = 0xC0FFEE40UL;

    private const int SettleSteps = 20;

    // An ask, the host's decision and its table on every machine, with room to spare.
    private const int AskSteps = 10;

    // A death, the crash camera cut to QuickRespawn, the ask and the grant.
    private const int GrantSteps = 120;

    private const float QuickRespawn = 0.5f;

    // How high over a flag a pilot is put, inside the reach.
    private const float Over = 15f;

    // How high over the field a waiting pilot is parked. It is far above the throw's 80 m climb
    // and every hill, with room for the loops it flies there.
    private const float ParkHeight = 800f;

    // Steady loops at full throttle. A loop comes back to the height it started at, so a parked
    // pilot never descends into the hills.
    private const string TrackedFlight = "--hold=0.3,0,0,1";

    private static readonly int[] Teams = { 1, 1, 2 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    // The rows every flag marker reads, by the keys FlagMarkers names. The side words come first,
    // then each marker's line for the flag's own side and for the other.
    private static readonly string[] MarkerRows =
    {
        FlagMarkers.YourKey, FlagMarkers.EnemyKey, FlagMarkers.CapturedByKey, FlagMarkers.HoldsFlagKey,
        "MSG_MP_YOUR_BASE", "MSG_MP_ENEMY_BASE", "MSG_MP_YOUR_FLAG_BASE", "MSG_MP_ENEMY_FLAG_BASE",
        "MSG_YOUR_FLAG_FLOAT", "MSG_ENEMY_FLAG_FLOAT",
    };

    // Steps that outlast a cooldown, so the next ask of the same seat is not refused by it.
    private static int CooledSteps => (int)(FlagMatch.HomeTakeCooldown / GameClock.FixedDt) + 10;

    [Suite("net-capture-the-flag",
        "three sessions on the chapter's MP2 map, two seats on lobby team 1 and one on team 2: every "
        + "machine builds a flag per team at its cs_flag_n, its base and flag markers reading Your or "
        + "Enemy by the pane's side; a guest takes the enemy flag at its base, the flag hangs on its "
        + "aeroplane on every machine, the base flag hides and the away marker and the carrier's tag "
        + "name the carrier; bringing it home "
        + "scores score_enemy_flag; a carrier's death floats the flag on every machine; a pilot catches its own "
        + "floating flag and returning it scores score_return_flag; a guest carrier's ejectflag typed into the chat "
        + "floats its flag on every machine through the host and sends no chat line; the uncaught flag goes home "
        + "when its 15 s throw runs out, scoring nobody; and a second capture ends the match on the Score limit by team")]
    internal static void AMatchOfFlagsAcrossThreeMachines(TestContext ctx)
    {
        var spec = Spec(ctx);
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(4001));
        var roster = NetCombatSuites.Roster(3, spec).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            ctx.Check(ends.All(e => e.Built), $"three sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            if (!Built(ctx, peers))
            {
                return;
            }

            foreach (var rig in peers.SelectMany(p => p.SeatRigs))
            {
                if (rig.Controller is { } pilot)
                {
                    pilot.AutoRespawnAfter = QuickRespawn;
                }
            }

            Lockstep(SettleSteps, peers);
            for (int seat = 0; seat < 3; seat++)
            {
                Park(peers, seat);
            }

            var rows = Messages.Load(ctx.MessagesPath);
            Worded(ctx, rows);
            HomeMarkers(ctx, peers, rows);
            Capture(ctx, peers, rows);
            Lockstep(CooledSteps, peers);
            CatchAndReturn(ctx, peers, rows);
            Lockstep(CooledSteps, peers);
            ThrowRunsOut(ctx, peers);
            Lockstep(CooledSteps, peers);
            SecondCapture(ctx, peers);
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

    // Two captures end it and nothing short of them can: team 2 scores only by its flags.
    private static int ScoreTarget(TestContext ctx) => 2 * MatchScores.Load(ctx.ZrdrPath).FlagCapture;

    private static SessionSpec Spec(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        return SessionSpec.Parse(new[] { "--vs" }
            .Concat(NetCombatSuites.Arena(ctx, mission: SessionSpec.CtfMission))
            .Concat(new[] { "--players=1", "--mute", "--no-pads", "--ctf", TrackedFlight, $"--vs-kills={ScoreTarget(ctx)}" })
            .ToArray());
    }

    // Two flags on every machine, at the same homes, each at its base with its carried twin hidden.
    private static bool Built(TestContext ctx, GameSession[] peers)
    {
        var rows = peers.Select(p => p.Flags?.Flags.Rows).ToArray();
        ctx.Check(rows.All(r => r is { Count: 2 } && r.All(f => f.State == FlagState.Home && f.Holder == FlagMatch.NoHolder)),
            $"every machine builds two flags at home from cs_flag_1 and cs_flag_2 ({string.Join(" | ", rows.Select(Describe))})");
        if (!rows.All(r => r is { Count: 2 }))
        {
            return false;
        }

        var homes = peers.Select(p => p.Flags!.Flags.HomeOf(1)).ToArray();
        ctx.Check(homes.All(h => h.DistanceTo(homes[0]) < 0.01f) && homes[0].DistanceTo(peers[0].Flags!.Flags.HomeOf(2)) > 2f * FlagMatch.Reach,
            $"and every machine stands team 1's flag on the same home, out of reach of team 2's ({string.Join(" | ", homes)})");
        return true;
    }

    // The guest on team 2 takes team 1's flag at its base, and brings it to its own.
    private static void Capture(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "the team 2 guest takes team 1's flag at its base", team: 1, holder: 2);
        HeldMarkers(ctx, peers, rows);
        var spoken = peers.Select(p => string.Join(",", p.Flags!.Spoken)).ToArray();
        ctx.Check(peers[0].Flags!.Spoken.Contains("snd_CTFlost") && peers[1].Flags!.Spoken.Contains("snd_CTFlost")
                  && peers[2].Flags!.Spoken.Contains("snd_CTFstolen") && !peers[2].Flags!.Spoken.Contains("snd_CTFlost"),
            $"team 1's machines hear their flag lost and team 2's hears it stolen ({string.Join(" | ", spoken)})");
        ctx.Check(peers.All(p => p.Flags!.LinesPosted > 0),
            $"and every machine posts the flag line ({string.Join(",", peers.Select(p => p.Flags!.LinesPosted))})");

        // ABLE-TO-FAIL CONTROL: inside the take's cooldown the carrier at its own base asks nothing.
        int before = peers[0].Versus!.ScoreOf(2);
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "ABLE-TO-FAIL CONTROL: at its own base inside the take's 5 s cooldown the flag stays held", team: 1, holder: 2);
        Park(peers, seat: 2);
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        Home(ctx, peers, "carried to team 2's base it goes home on every machine", team: 1);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(2) == before + p.Versus!.Scores.FlagCapture && p.Versus!.KillsOf(2) == 0),
            $"and the capture scores its carrier {peers[0].Versus!.Scores.FlagCapture} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => !p.Versus!.Completed),
            $"ABLE-TO-FAIL CONTROL: one capture is short of the {ScoreTarget(ctx)}-point target and the match runs ({Scores(peers)})");
        Park(peers, seat: 2);
    }

    // The carrier goes down with the flag and it floats everywhere. The host's own pilot on team 1
    // catches it and returns it.
    private static void CatchAndReturn(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken again", team: 1, holder: 2);
        Down(peers, victim: 2, killer: 0);
        ctx.Check(peers.All(p => p.Flags!.Flags.RowOf(1) is { State: FlagState.Floating, Holder: FlagMatch.NoHolder }),
            $"the carrier's death floats the flag on every machine ({Rows(peers)})");
        FloatingMarkers(ctx, peers, rows);

        // Over the flag rather than on it: a flag at rest lies against a base's buildings.
        int before = peers[0].Versus!.ScoreOf(0);
        if (peers[0].Flags!.Flags.FloatingAt(1) is { } floating)
        {
            var over = floating + (Vector3.Up * Over);
            peers[0].SeatRigs[0].Controller!.RespawnAt(over, over + (Vector3.Right * 100f));
        }

        Lockstep(1, peers);
        Park(peers, seat: 0);
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "the host's team 1 pilot catches its own floating flag", team: 1, holder: 0);
        ctx.Check(peers[0].Flags!.Spoken.Contains("snd_CTFscoreCapt") && !peers[2].Flags!.Spoken.Contains("snd_CTFscoreCapt"),
            $"and its own team hears it caught ({string.Join(" | ", peers.Select(p => string.Join(",", p.Flags!.Spoken)))})");
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 0, peers[0].Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Home(ctx, peers, "returned to its own base", team: 1);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(0) == before + p.Versus!.Scores.FlagReturn),
            $"and the return scores its carrier {peers[0].Versus!.Scores.FlagReturn} on every machine ({Scores(peers)})");
        Park(peers, seat: 0);
    }

    // The guest carrier types the console's ejectflag into its chat. The host floats the flag and
    // relays it, so it floats everywhere while the carrier flies on. Nobody catches it, and it goes
    // home at the end of its throw, on the host's word.
    private static void ThrowRunsOut(TestContext ctx, GameSession[] peers)
    {
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken a third time", team: 1, holder: 2);
        var posted = peers.Select(p => p.NetChat!.Chat.Posted).ToArray();
        var sent = peers.Select(p => p.NetChat!.LinesSent).ToArray();
        Type(peers, seat: 1, NetChatLink.EjectFlagCommand);
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "ABLE-TO-FAIL CONTROL: the console's ejectflag from a pilot carrying nothing leaves the flag held", team: 1, holder: 2);

        // Parked only once the relay is back, so the flag floats over team 1's base everywhere. The
        // take's cooldown stops the carrier catching it meanwhile.
        Type(peers, seat: 2, NetChatLink.EjectFlagCommand);
        Lockstep(AskSteps, peers);
        Park(peers, seat: 2);
        ctx.Check(peers.All(p => p.Flags!.Flags.RowOf(1) is { State: FlagState.Floating, Holder: FlagMatch.NoHolder }),
            $"the carrier's ejectflag floats the flag on every machine ({Rows(peers)})");
        ctx.Check(peers[2].SeatRigs[2].Controller is { Crashed: false, Destroyed: false } && peers.All(p => p.SeatRigs[2].Controller!.MarkerName == null),
            $"and the carrier flies on untagged");
        ctx.Check(peers.Select((p, i) => p.NetChat!.Chat.Posted == posted[i] && p.NetChat!.LinesSent == sent[i]).All(same => same),
            $"and neither console line was posted or sent as chat ({string.Join(",", peers.Select(p => $"{p.NetChat!.Chat.Posted}/{p.NetChat!.LinesSent}"))})");
        var scores = peers.Select(p => Enumerable.Range(0, 3).Select(p.Versus!.ScoreOf).ToArray()).ToArray();
        int flown = AskSteps;
        Lockstep((int)(FlagMatch.ThrowSeconds / GameClock.FixedDt) - flown - AskSteps, peers);
        ctx.Check(peers.All(p => p.Flags!.Flags.RowOf(1) is { State: FlagState.Floating }),
            $"ABLE-TO-FAIL CONTROL: short of its 15 s throw the flag still floats on every machine ({Rows(peers)})");
        Lockstep(4 * AskSteps, peers);
        Home(ctx, peers, "once the throw runs out", team: 1);
        ctx.Check(peers.Select((p, i) => Enumerable.Range(0, 3).Select(p.Versus!.ScoreOf).SequenceEqual(scores[i])).All(same => same),
            $"and a floating flag's return scores nobody ({Scores(peers)})");
    }

    // The second capture reaches the target on team 2's total and ends the match everywhere.
    private static void SecondCapture(TestContext ctx, GameSession[] peers)
    {
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken for the second capture", team: 1, holder: 2);
        Park(peers, seat: 2);
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 2, peers[2].Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        ctx.Check(peers.All(p => p.Versus!.TeamScoreOf(2) == ScoreTarget(ctx) && p.Versus!.Completed && p.MatchEnd == NetMatchEnd.ScoreTarget),
            $"the second capture ends the match on the Score limit by team on every machine ({Scores(peers)}; {string.Join(", ", peers.Select(p => p.MatchEnd))})");
        var titles = peers.Select(p => VersusBoard.Title(p.Versus!)).ToArray();
        ctx.Check(titles.All(t => t == "BLUE ANGELS WINS"),
            $"and every machine's board names the capturing team ({string.Join(" | ", titles)})");
    }

    // Every marker line below is read off the run's own table, so a missing row would compare
    // the key against itself. Each side's two lines must also differ, or no read could tell the
    // sides apart.
    private static void Worded(TestContext ctx, Messages rows)
    {
        var missing = MarkerRows.Where(key => rows.Get(key) == key).ToArray();
        var pairs = new[] { ("MSG_MP_YOUR_BASE", "MSG_MP_ENEMY_BASE"), ("MSG_MP_YOUR_FLAG_BASE", "MSG_MP_ENEMY_FLAG_BASE"),
            ("MSG_YOUR_FLAG_FLOAT", "MSG_ENEMY_FLAG_FLOAT"), (FlagMarkers.YourKey, FlagMarkers.EnemyKey) };
        var alike = pairs.Where(p => rows.Get(p.Item1) == rows.Get(p.Item2)).Select(p => p.Item1).ToArray();
        ctx.Check(missing.Length == 0 && alike.Length == 0,
            $"the message table words every flag marker line, each side apart (missing {string.Join(",", missing)}; alike {string.Join(",", alike)})");
    }

    // Both flags home. Each base and each flag at its base reads "Your" to its own team's pane and
    // "Enemy" to the other's, under its team's name. No away marker stands.
    private static void HomeMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Reads(ctx, peers, "every pane reads team 1's base as Your Base on team 1 and Enemy Base on team 2, "
            + "where the map's own table labels it Team 1 on every machine", "ctf_1",
            m => $"{Side(m, 1, rows.Get("MSG_MP_YOUR_BASE"), rows.Get("MSG_MP_ENEMY_BASE"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Base", "Enemy Base")}|Red Squadron");
        Reads(ctx, peers, "and team 2's base the other way round", "ctf_2",
            m => $"{Side(m, 2, rows.Get("MSG_MP_YOUR_BASE"), rows.Get("MSG_MP_ENEMY_BASE"))}|Blue Angels",
            m => $"{Side(m, 2, "Your Base", "Enemy Base")}|Blue Angels");
        Reads(ctx, peers, "every pane reads team 1's flag at its base by side", "cs_flag_1",
            m => $"{Side(m, 1, rows.Get("MSG_MP_YOUR_FLAG_BASE"), rows.Get("MSG_MP_ENEMY_FLAG_BASE"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Flag At Base", "Enemy Flag At Base")}|Red Squadron");
        Reads(ctx, peers, "and no pane has an away marker for a flag at home", "cs_flg_light1", _ => "off");
    }

    // Team 1's flag held by seat 2. Its at-base marker is off and its away marker names the carrier
    // by side. The carrier's own name line is its tag.
    private static void HeldMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        string carrier = peers[0].NetSeats[2].Callsign;
        string your = rows.Get(FlagMarkers.YourKey);
        string enemy = rows.Get(FlagMarkers.EnemyKey);
        Reads(ctx, peers, "with the flag taken no pane marks it at its base", "cs_flag_1", _ => "off");
        Reads(ctx, peers, "and every pane marks it away, captured by its carrier, by side", "cs_flg_light1",
            m => $"{Messages.Fill(rows.Get(FlagMarkers.CapturedByKey), Side(m, 1, your, enemy), carrier)}|Red Squadron",
            m => $"{Side(m, 1, "Your", "Enemy")} Flag Captured by {carrier}|Red Squadron");
        Reads(ctx, peers, "ABLE-TO-FAIL CONTROL: team 2's flag still stands at its base on every pane", "cs_flag_2",
            m => $"{Side(m, 2, rows.Get("MSG_MP_YOUR_FLAG_BASE"), rows.Get("MSG_MP_ENEMY_FLAG_BASE"))}|Blue Angels",
            m => $"{Side(m, 2, "Your Flag At Base", "Enemy Flag At Base")}|Blue Angels");

        var tags = new[] { 0, 1 }.Select(m => NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[m], m),
            s => ReferenceEquals(s, peers[m].SeatRigs[2].Controller))?.DisplayName ?? "missing").ToArray();
        string tag = Messages.Fill(rows.Get(FlagMarkers.HoldsFlagKey), carrier, your);
        ctx.Check(tags.All(t => t == tag),
            $"and team 1's panes read the carrier's own marker as holding their flag, in the message table's line ({string.Join(" | ", tags)})");
        if (!ctx.SyntheticData)
        {
            ctx.Check(tags.All(t => t == $"{carrier}  Holds Your flag"),
                $"and team 1's panes read the carrier's own marker as holding their flag ({string.Join(" | ", tags)})");
        }

        var near = NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[0], 0),
            s => s is ObjectiveSite { Node: "cs_flg_light1" })?.Position.DistanceTo(peers[0].SeatRigs[2].Controller!.WorldPosition);
        ctx.Check(near is < 30f, $"and the away marker stands on the carrier ({near?.ToString("0.0") ?? "none"} m)");
    }

    // The carrier down: the away marker reads floating by side and the carrier's tag is gone.
    private static void FloatingMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Reads(ctx, peers, "every pane marks the floating flag by side", "cs_flg_light1",
            m => $"{Side(m, 1, rows.Get("MSG_YOUR_FLAG_FLOAT"), rows.Get("MSG_ENEMY_FLAG_FLOAT"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Flag Floating", "Enemy Flag Floating")}|Red Squadron");
        ctx.Check(peers.All(p => p.SeatRigs[2].Controller!.MarkerName == null),
            $"and no machine still tags the downed carrier");
    }

    // The line a pane on machine `machine` should read for a marker of `team`'s flag.
    private static string Side(int machine, int team, string your, string enemy) =>
        Teams[machine] == team ? your : enemy;

    // A marker read on every pane against the lines the run's table words. On a real extraction it
    // is also read against the shipped wording, `shipped`, which an invented table does not carry.
    private static void Reads(TestContext ctx, GameSession[] peers, string what, string key, Func<int, string> want,
        Func<int, string>? shipped = null)
    {
        var got = peers.Select((p, m) => NetTeamSuites.RefOf(NetTeamSuites.Cycles(p, m),
                s => s is ObjectiveSite site && site.Node.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } t
            ? $"{t.Category}|{t.DisplayName}"
            : "off").ToArray();
        string reading = string.Join(" | ", got);
        if (shipped == null)
        {
            ctx.Check(got.Select((g, m) => g == want(m)).All(ok => ok), $"{what} ({reading})");
            return;
        }

        ctx.Check(got.Select((g, m) => g == want(m)).All(ok => ok), $"{what}, in the message table's lines ({reading})");
        if (!ctx.SyntheticData)
        {
            ctx.Check(got.Select((g, m) => g == shipped(m)).All(ok => ok), $"{what} ({reading})");
        }
    }

    private static void Held(TestContext ctx, GameSession[] peers, string what, int team, int holder)
    {
        ctx.Check(peers.All(p => p.Flags!.Flags.RowOf(team) is { State: FlagState.Held } row && row.Holder == holder),
            $"{what}: seat {holder} holds team {team}'s flag on every machine ({Rows(peers)})");
        ctx.Check(peers.All(p => p.SeatRigs[holder].Controller is { } pilot && p.Flags!.CarriedFlag(team) is { Visible: true } flag && pilot.IsAncestorOf(flag)),
            $"and the flag hangs on seat {holder}'s aeroplane on every machine ({Hung(peers, team)})");
    }

    private static void Home(TestContext ctx, GameSession[] peers, string what, int team)
    {
        ctx.Check(peers.All(p => p.Flags!.Flags.RowOf(team) is { State: FlagState.Home, Holder: FlagMatch.NoHolder }),
            $"{what}: team {team}'s flag stands at home on every machine ({Rows(peers)})");
        ctx.Check(peers.All(p => p.Flags!.CarriedFlag(team) is not { Visible: true }),
            $"and no machine still draws it away from its base ({Hung(peers, team)})");
    }

    // Where each machine's carried flag hangs and whether it draws, for a failure message.
    private static string Hung(GameSession[] peers, int team) =>
        string.Join(" | ", peers.Select(p => p.Flags!.CarriedFlag(team) is { } flag
            ? $"{flag.GetParent()?.Name ?? "-"}/{flag.GetParent()?.GetParent()?.Name ?? "-"} visible={flag.Visible}"
            : "no node"));

    // A seat put over a point on the machine that flies it. Every other machine sees it by its pose.
    private static void Put(GameSession[] peers, int seat, Vector3 at)
    {
        var over = at + (Vector3.Up * Over);
        peers[seat].SeatRigs[seat].Controller!.RespawnAt(over, over + (Vector3.Right * 100f));
    }

    // A seat put high over the field while the suite waits out a cooldown or a throw. There it is
    // out of every flag's reach and clear of the bases' buildings.
    private static void Park(GameSession[] peers, int seat)
    {
        var home = peers[seat].Flags!.Flags.HomeOf(1);
        var high = home + (Vector3.Up * ParkHeight) + ((Vector3.Left + Vector3.Forward) * (ParkHeight * seat));
        peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
    }

    // A line typed into the chat on the machine that flies the seat, and sent.
    private static void Type(GameSession[] peers, int seat, string text)
    {
        var link = peers[seat].NetChat!;
        link.Open(seat, team: false);
        foreach (char c in text)
        {
            link.Chat.Type(c);
        }

        link.Submit();
    }

    private static void Down(GameSession[] peers, int victim, int killer)
    {
        var owner = peers[victim];
        owner.SeatRigs[victim].Controller!.DebugForceCrash(owner.SeatRigs[killer].Controller!.PlayerIndex);
        Lockstep(GrantSteps, peers);
        Park(peers, victim);
    }

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

    private static string Describe(IReadOnlyList<FlagRow>? rows) =>
        rows == null ? "none" : string.Join(" ", rows.Select(r => $"{r.Team}:{r.State}/{r.Holder}"));

    private static string Rows(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => Describe(p.Flags?.Flags.Rows)));

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 3).Select(p.Versus!.ScoreOf))
            + $" teams {p.Versus!.TeamScoreOf(1)}/{p.Versus!.TeamScoreOf(2)}"));
}
