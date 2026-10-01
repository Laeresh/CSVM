using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Launch;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A team Deathmatch between whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig. The host and the first guest fly on one lobby team and the
/// second guest on another. Every rule is read on all three machines, since a seat's team is part
/// of the roster every machine builds from.</summary>
internal static class NetTeamSuites
{
    private const string MpMission = "MP1";

    private const ulong HostSeed = 0xC0FFEE84UL;

    private const int SettleSteps = 20;

    // A death, the crash camera cut to QuickRespawn, the ask and the grant, with room to spare.
    private const int GrantSteps = 120;

    private const float QuickRespawn = 0.5f;

    // How close to a table entry a placed aeroplane counts as standing on it, horizontally.
    private const float EntryTolerance = 5f;


    // The scripted climb the net match suites fly, so no seat scores a crash of its own.
    private const string TrackedFlight = "--hold=0.6,0.9,0,1";

    private static readonly int[] Teams = { 1, 1, 2 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-team-deathmatch",
        "three sessions in one process, the host and one guest on lobby team 1 and the other guest on "
        + "team 2: each seat opens on its own team's net.zrd block and comes back inside it, teammates "
        + "are not hostile and neither team flies on a raw lobby number, every pane reads a teammate "
        + "on the Ally cycle in the friendly green and the other team in red, a teammate kill costs the "
        + "killer score_suicide and counts no kill, the host ends the match when a team's total reaches the "
        + "Score target though no pilot reached it alone, every machine's board names the winning "
        + "team, and after the rematch the drop of the other team's only pilot ends it on reason 4")]
    internal static void ATeamMatchAcrossThreeMachines(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, TrackedFlight, $"--vs-kills={TeamTarget(MatchScores.Load(ctx.ZrdrPath))}");
        var table = SpawnPoints.LoadNetTable(SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, MpMission));
        if (table is not { Count: > 2 * SpawnPoints.NetBlock })
        {
            throw new SuiteSkippedException($"{ctx.Chapter}/{MpMission}'s net.zrd authors no second team block");
        }

        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(8401));
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
            Openings(ctx, table, peers);
            Lockstep(SettleSteps, peers);
            Hostility(ctx, peers);
            Markers(ctx, peers);
            Kills(ctx, peers);
            Rematch(ctx, peers);
            LastTeamStanding(ctx, mesh, peers);
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

    /// <summary>What <paramref name="seat"/>'s own pane on <paramref name="peer"/> reads on its target
    /// cycles. Every aeroplane and mission site is filed by the pool its selection rebuilds from.
    /// Shared with the Capture the Flag and Zeppelin vs Zeppelin suites.</summary>
    internal static TargetPool Cycles(GameSession peer, int seat)
    {
        var own = peer.SeatRigs[seat].Controller!;
        var scan = new AimCandidateSet();
        own.Projectiles?.CollectAircraft(scan);
        var sites = new List<AimCandidate>();
        own.TargetObjectives?.Invoke(sites);
        var pool = new TargetPool();
        pool.Rebuild(scan, null, own.Team, own, sites);
        return pool;
    }

    /// <summary>The ref a pool files for a source <paramref name="source"/> accepts, on any cycle,
    /// or null.</summary>
    internal static TargetRef? RefOf(TargetPool pool, Func<object?, bool> source) =>
        pool.Enemy.Concat(pool.Ally).Concat(pool.NonAircraft).Where(t => source(t.Source))
            .Select(t => (TargetRef?)t).FirstOrDefault();

    // The opening placement, read before any step. Each seat stands on its team's block, walked by
    // its place in the team, on the same entry on every machine.
    private static void Openings(TestContext ctx, IReadOnlyList<SpawnPoint> table, GameSession[] peers)
    {
        var want = SpawnPoints.TeamBlocks(table.Count, Teams, 0).Openings;
        string plans = string.Join(" | ", peers.Select(p => p.TeamOpenings is { } o ? string.Join(",", o) : "none"));
        ctx.Check(peers.All(p => p.TeamOpenings is { } o && o.SequenceEqual(want)),
            $"every machine opens seat by seat on entries {string.Join(",", want)}, block 1 twice and block 2 once ({plans})");

        string stands = string.Join(" | ", peers.Select(p => string.Join(",", p.SeatRigs.Select(r => EntryOf(table, r.Controller)))));
        ctx.Check(peers.All(p => p.SeatRigs.Select((r, seat) => OnEntry(table, want[seat], r.Controller)).All(on => on)),
            $"and every aeroplane stands on its seat's entry on every machine ({stands})");

        // ABLE-TO-FAIL CONTROL. The team blocks are other ground than the free-for-all block, so an
        // aeroplane placed by the free-for-all walk could not pass the line above.
        bool apart = want.All(entry => Enumerable.Range(0, SpawnPoints.NetBlock).All(i => !SameGround(table[i], table[entry])));
        ctx.Check(apart, $"ABLE-TO-FAIL CONTROL: no team opening stands on any free-for-all entry's ground");
    }

    // A lobby team is a team id only once banded, so teammates share one and the other team differs.
    private static void Hostility(TestContext ctx, GameSession[] peers)
    {
        foreach (var (peer, index) in peers.Select((p, i) => (p, i)))
        {
            var teams = peer.SeatRigs.Select(r => r.Controller!.Team).ToArray();
            string reading = $"machine {index}: {string.Join(",", teams)}";
            ctx.Check(teams[0] == teams[1] && !AimAssist.Hostile(teams[0], teams[1]),
                $"the two team 1 seats fly on one team id and are not hostile to each other ({reading})");
            ctx.Check(AimAssist.Hostile(teams[0], teams[2]) && AimAssist.Hostile(teams[1], teams[2]),
                $"and both are hostile to the team 2 seat ({reading})");
            ctx.Check(teams.All(t => t > AimAssist.LobbyTeamBand && t != AimAssist.PlayerTeam && t != TurretDef.DefaultTeamId),
                $"and no seat flies on its raw lobby number, the player's side or the emplacements' ({reading})");
        }
    }

    // Each machine's own pane on the other two seats. A teammate files on the Ally cycle, and its box
    // and Dogfight marker take the friendly green. The other team's seat takes neither.
    private static void Markers(TestContext ctx, GameSession[] peers)
    {
        var readings = new List<string>();
        bool right = true, foes = true;
        for (int machine = 0; machine < peers.Length; machine++)
        {
            var own = peers[machine].SeatRigs[machine].Controller!;
            var pool = Cycles(peers[machine], machine);
            for (int seat = 0; seat < peers.Length; seat++)
            {
                if (seat == machine)
                {
                    continue;
                }

                var other = peers[machine].SeatRigs[seat].Controller!;
                bool mate = Teams[seat] == Teams[machine];
                var found = RefOf(pool, s => ReferenceEquals(s, other));
                bool green = found is { } t && TargetHud.MarkerColor(t, own.Team) == TargetHud.HudGreen;
                bool hud = VersusHud.MarkerColor(own.Team, other.Team, seat) == TargetHud.HudGreen;
                bool reads = found is { } f && f.Class == (mate ? TargetClass.Ally : TargetClass.Enemy)
                    && green == mate && hud == mate;
                right &= !mate || reads;
                foes &= mate || reads;
                readings.Add($"m{machine}:s{seat} {found?.Class.ToString() ?? "missing"} box={(green ? "green" : "not green")} hud={(hud ? "green" : "own colour")}");
            }
        }

        ctx.Check(right, $"every machine's pane reads its teammate on the Ally cycle, its box and its Dogfight marker in the friendly green ({string.Join(", ", readings)})");
        ctx.Check(foes, $"ABLE-TO-FAIL CONTROL: and the other team's seat on the Enemy cycle, never green ({string.Join(", ", readings)})");
    }

    // The Score target, four of team 1's kills and one teamkill in the sequence below. No pilot of
    // the two-seat team reaches it alone, so only the team's total can end the match.
    private static int TeamTarget(MatchScores scores) => (4 * scores.Kill) + scores.Suicide;

    // The scoring, killed on the machine that flies each victim and scored by the host alone.
    // The team 1 total reaches the target off two pilots on two kills each, after a teamkill.
    private static void Kills(TestContext ctx, GameSession[] peers)
    {
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.AutoRespawnAfter = QuickRespawn;
            }
        }

        var scores = peers[0].Versus!.Scores;
        int kill = scores.Kill, suicide = scores.Suicide, target = TeamTarget(scores);
        Down(peers, victim: 2, killer: 0);
        Board(ctx, peers, "seat 0 kills the other team's seat", (kill, 1, 0), (0, 0, 0), (0, 0, 1));
        Returned(ctx, peers, seat: 2);

        Down(peers, victim: 0, killer: 1);
        Board(ctx, peers, $"a teammate kill costs the killer {-suicide} and counts no kill", (kill, 1, 1), (suicide, 0, 0), (0, 0, 1));
        ctx.Check(peers.All(p => p.Versus!.TeamScoreOf(1) == kill + suicide),
            $"and the team's total falls with it on every machine ({Totals(peers)})");
        Returned(ctx, peers, seat: 0);

        Down(peers, victim: 2, killer: 1);
        Down(peers, victim: 2, killer: 1);
        ctx.Check(peers.All(p => p.Versus!.TeamScoreOf(1) == target - kill && !p.Versus!.Completed && p.MatchEnd == NetMatchEnd.Running),
            $"ABLE-TO-FAIL CONTROL: one kill short of the target the match runs on every machine ({Totals(peers)})");

        Down(peers, victim: 2, killer: 0);
        ctx.Check(peers.All(p => p.Versus!.Completed && p.MatchEnd == NetMatchEnd.ScoreTarget && p.Pause is { Ended: true }),
            $"team 1's total of {target} ends the match on the Score limit on every machine ({string.Join(", ", peers.Select(p => p.MatchEnd))})");
        ctx.Check(peers.All(p => Enumerable.Range(0, 3).All(seat => p.Versus!.ScoreOf(seat) < target)),
            $"though no pilot reached it alone ({Totals(peers)})");
        var titles = peers.Select(p => VersusBoard.Title(p.Versus!)).ToArray();
        ctx.Check(titles.All(t => t == "RED SQUADRON WINS"),
            $"and every machine's board names the winning team by its lobby name ({string.Join(" | ", titles)})");
    }

    // The host's rematch puts every score back to zero on every machine.
    private static void Rematch(TestContext ctx, GameSession[] peers)
    {
        peers[0].SeatRigs[0].Controller!.RestartMatch!();
        Lockstep(GrantSteps, peers);
        ctx.Check(peers.All(p => !p.Versus!.Completed && p.MatchEnd == NetMatchEnd.Running
                                 && Enumerable.Range(0, 3).All(seat => p.Versus!.ScoreOf(seat) == 0)),
            $"the host's rematch runs the round again from zero on every machine ({Totals(peers)})");
    }

    // Reason 4 by team: the other team's only pilot drops, and two pilots with lives remain on one
    // team. A free-for-all with the same two pilots would fly on.
    private static void LastTeamStanding(TestContext ctx, IReadOnlyList<LoopbackTransport> mesh, GameSession[] peers)
    {
        var staying = new[] { peers[0], peers[1] };
        mesh[0].Disconnect(mesh[2].LocalPeer);
        Lockstep(SettleSteps, staying);
        ctx.Check(staying.All(p => p.SeatRigs[0].Controller is { Crashed: false } && p.SeatRigs[1].Controller is { Crashed: false }),
            $"with both team 1 pilots still flying on both remaining machines");
        ctx.Check(staying.All(p => p.Versus!.Completed && p.MatchEnd == NetMatchEnd.NobodyLeft),
            $"the drop of team 2's only pilot ends the match on reason 4 on both remaining machines ({string.Join(", ", staying.Select(p => p.MatchEnd))})");

        var alone = new VersusMatch(3, TeamTarget(MatchScores.Fallback), 300f);
        alone.Leave(2);
        ctx.Check(!alone.Completed,
            $"ABLE-TO-FAIL CONTROL: the same drop in a free-for-all leaves two opponents and the match runs");
    }

    // One death, crashed on the machine that flies the victim, and the grant that answers it.
    private static void Down(GameSession[] peers, int victim, int killer)
    {
        var owner = peers[victim];
        owner.SeatRigs[victim].Controller!.DebugForceCrash(owner.SeatRigs[killer].Controller!.PlayerIndex);
        Lockstep(GrantSteps, peers);
    }

    // The expected (score, kills, deaths) per seat, on every machine: the host's count and the two
    // guests' copies of it.
    private static void Board(TestContext ctx, GameSession[] peers, string what,
        (int Score, int Kills, int Deaths) seat0, (int Score, int Kills, int Deaths) seat1, (int Score, int Kills, int Deaths) seat2)
    {
        var want = new[] { seat0, seat1, seat2 };
        bool right = peers.All(p => Enumerable.Range(0, 3).All(seat =>
            p.Versus!.ScoreOf(seat) == want[seat].Score && p.Versus!.KillsOf(seat) == want[seat].Kills
            && p.Versus!.DeathsOf(seat) == want[seat].Deaths));
        ctx.Check(right, $"{what}, on every machine ({Lines(peers)})");
    }

    // A downed seat's grant lands inside its own team's block, the same entry on every machine.
    private static void Returned(TestContext ctx, GameSession[] peers, int seat)
    {
        int start = Teams[seat] * SpawnPoints.NetBlock;
        var entries = peers.Select(p => p.SpawnEntries[seat]).ToArray();
        ctx.Check(entries.All(e => e == entries[0]) && entries[0] >= start && entries[0] < start + SpawnPoints.NetBlock,
            $"seat {seat} comes back inside team {Teams[seat]}'s block [{start}, {start + SpawnPoints.NetBlock}) on every machine ({string.Join(",", entries)})");
    }

    // Every session through the same fixed steps, host first, the order a listen server runs in.
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

    private static string Lines(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(" ", Enumerable.Range(0, 3)
            .Select(seat => $"P{seat + 1}:{p.Versus!.ScoreOf(seat)}/{p.Versus!.KillsOf(seat)}K/{p.Versus!.DeathsOf(seat)}D"))));

    private static string Totals(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(" ", p.Versus!.TeamStandings().Select(t => $"{t.Name}:{t.Score}"))
            + " / " + string.Join(",", Enumerable.Range(0, 3).Select(p.Versus!.ScoreOf))));

    private static bool SameGround(SpawnPoint a, SpawnPoint b) =>
        Mathf.Abs(a.Position.X - b.Position.X) < EntryTolerance && Mathf.Abs(a.Position.Z - b.Position.Z) < EntryTolerance;

    private static bool OnEntry(IReadOnlyList<SpawnPoint> table, int entry, Node3D? placed) =>
        placed != null && SameGround(table[entry], new SpawnPoint(placed.GlobalPosition, 0f));

    // The first entry under a placed aeroplane's ground, or -1, for the failure message.
    private static int EntryOf(IReadOnlyList<SpawnPoint> table, Node3D? placed)
    {
        for (int i = 0; placed != null && i < table.Count; i++)
        {
            if (SameGround(table[i], new SpawnPoint(placed.GlobalPosition, 0f)))
            {
                return i;
            }
        }

        return -1;
    }
}
