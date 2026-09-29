using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Launch;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Zeppelin vs Zeppelin between whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig and the chapter's <c>MP3</c> map. The host and the first
/// guest fly lobby team 2, listed first, and the second guest team 1. Parts are killed on the host,
/// which decides every world hit, in each seat's name. Every rule is read on all three machines.
/// </summary>
internal static class NetZeppelinVersusSuites
{
    private const ulong HostSeed = 0xC0FFEE41UL;

    private const int SettleSteps = 20;

    // A part's death, its health on every guest and each machine's poll of it, with room to spare.
    private const int PartSteps = 10;

    // A death, the crash camera cut to QuickRespawn, the ask and the grant.
    private const int GrantSteps = 120;

    private const float QuickRespawn = 0.5f;

    // Far above what the gas bags score and far below the lost hull's bonus.
    private const int ScoreTarget = 30;

    private const float Overkill = 100000f;

    // Where the waiting pilots loop: well over every hill and kilometres from both hulls' routes.
    private const float ParkX = -2000f;
    private const float ParkZ = -6000f;
    private const float ParkHeight = 1500f;
    private const float ParkSpacing = 600f;

    // Steady loops at full throttle, which keep a waiting pilot at the height it was put.
    private const string TrackedFlight = "--hold=0.3,0,0,1";

    // Team 2 is first in seat order, so it flies hull 0 whatever its number.
    private static readonly int[] Teams = { 2, 2, 1 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-zeppelin-vs-zeppelin",
        "three sessions on the chapter's MP3 map, two seats on lobby team 2 listed first and one on "
        + "team 1: every machine flies multiplayer1zep for team 2 and multiplayer2zep for team 1, "
        + "with each side's parts on its team and each seat opening beside its own hull; every pane "
        + "reads its own hull as Defend and the other as Destroy under the team's name; an enemy "
        + "gas bag scores its killer 10 and its own side -10; a broadside cannon scores its bound bag "
        + "once; a downed seat returns by its hull above the spawn table; and the third bag of "
        + "multiplayer1zep ends the match for team 1 with 100 each that the Score limit never reads")]
    internal static void AMatchOfHullsAcrossThreeMachines(TestContext ctx)
    {
        var spec = Spec(ctx);
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(4101));
        var roster = NetCombatSuites.Roster(3).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
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
            if (!Sides(ctx, peers))
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

            // The opening blocks stand a hundred metres over each hull, where a loop would ram it.
            for (int seat = 0; seat < 3; seat++)
            {
                var high = new Vector3(ParkX - (ParkSpacing * seat), ParkHeight, ParkZ);
                peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
            }

            Lockstep(SettleSteps, peers);
            HullMarkers(ctx, peers);
            GasBags(ctx, peers);
            Returns(ctx, peers);
            HullLost(ctx, peers);
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
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, SessionSpec.ZvzMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{SessionSpec.ZvzMission} zrdr");
        return SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={SessionSpec.ZvzMission}", "--players=1", "--mute",
            "--no-pads", "--zvz", TrackedFlight, $"--vs-kills={ScoreTarget}",
        });
    }

    // Both hulls on every machine, each on its side's team, and every seat beside its own hull.
    private static bool Sides(TestContext ctx, GameSession[] peers)
    {
        ctx.Check(peers.All(p => p.ZvzPlay is { } zvz && zvz.Rules.TeamOfHull(0) == 2 && zvz.Rules.TeamOfHull(1) == 1
                                 && p.ZeppelinHulls?.NodeAt(0) == "multiplayer1zep" && p.ZeppelinHulls.NodeAt(1) == "multiplayer2zep"),
            $"every machine flies multiplayer1zep for team 2, first in seat order, and multiplayer2zep for team 1 ({string.Join(" | ", peers.Select(p => p.ZvzPlay == null ? "none" : $"{p.ZeppelinHulls?.NodeAt(0)}:{p.ZvzPlay.Rules.TeamOfHull(0)} {p.ZeppelinHulls?.NodeAt(1)}:{p.ZvzPlay.Rules.TeamOfHull(1)}"))})");
        if (peers.Any(p => p.ZvzPlay == null))
        {
            return false;
        }

        var parts = peers.Select(p =>
        {
            var into = new List<AimCandidate>();
            p.ZeppelinHulls!.CollectTargetParts(into);
            return into;
        }).ToArray();
        int team2 = AimAssist.LobbyTeam(2)!.Value, team1 = AimAssist.LobbyTeam(1)!.Value;
        ctx.Check(parts.All(list => list.Count(c => c.Team == team2) > 0 && list.Count(c => c.Team == team1) > 0
                                    && list.All(c => c.Team == team1 || c.Team == team2)),
            $"and every part of each hull flies on its side's team, so aim and friend-or-foe follow it ({string.Join(" | ", parts.Select(l => $"{l.Count(c => c.Team == team2)}/{l.Count(c => c.Team == team1)} of {l.Count}"))})");

        var hulls = peers[0].ZeppelinHulls!;
        var near = new List<string>();
        bool beside = true;
        for (int seat = 0; seat < 3; seat++)
        {
            var at = peers[seat].SeatRigs[seat].Controller!.WorldPosition;
            float own = at.DistanceTo(hulls.HullPositionAt(seat < 2 ? 0 : 1)!.Value);
            float other = at.DistanceTo(hulls.HullPositionAt(seat < 2 ? 1 : 0)!.Value);
            beside &= own < other;
            near.Add($"{own:0}/{other:0}");
        }

        ctx.Check(beside, $"and every seat opens nearer its own hull than the other, in the block the map lays round it ({string.Join(", ", near)} m own/other)");
        return true;
    }

    // Each pane reads its own side's hull as Defend and the other as Destroy, under the hull's team
    // name. The map's table calls multiplayer1zep the enemy on every machine. Destroy takes the red.
    private static void HullMarkers(TestContext ctx, GameSession[] peers)
    {
        var got = new List<string>();
        bool right = true;
        for (int machine = 0; machine < peers.Length; machine++)
        {
            var pool = NetTeamSuites.Cycles(peers[machine], machine);
            int ownTeam = peers[machine].SeatRigs[machine].Controller!.Team;
            foreach (var (node, team, name) in new[] { ("multiplayer1zep", 2, "Blue Angels"), ("multiplayer2zep", 1, "Red Squadron") })
            {
                var found = NetTeamSuites.RefOf(pool, s => s is ObjectiveSite site && site.Node.Equals(node, StringComparison.OrdinalIgnoreCase));
                bool mine = Teams[machine] == team;
                bool red = found is { } t && TargetHud.MarkerColor(t, ownTeam) == TargetHud.HudRed;
                right &= found is { } f && f.Category == (mine ? "Defend" : "Destroy") && f.DisplayName == name && red != mine;
                got.Add($"m{machine}:{node} {(found is { } g ? $"{g.Category}|{g.DisplayName}{(red ? " red" : "")}" : "missing")}");
            }
        }

        ctx.Check(right, $"every pane reads its own side's hull as Defend and the other's as Destroy in red, each under its team's name, on every machine ({string.Join(", ", got)})");
    }

    // An enemy bag scores, a cannon scores its bound bag once, and a side's own bag costs.
    private static void GasBags(TestContext ctx, GameSession[] peers)
    {
        Kill(peers, "multiplayer1zep", "gasbag1", seat: 2);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(2) == ZeppelinVersus.GasbagScore && p.Versus!.KillsOf(2) == 0),
            $"team 1's seat downing multiplayer1zep's gasbag1 scores it {ZeppelinVersus.GasbagScore} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => Pool(p, "multiplayer1zep", "gasbag1") is { Status: DestructibleRegistry.State.Destroyed }),
            $"and the bag is destroyed on every machine ({string.Join(", ", peers.Select(p => Pool(p, "multiplayer1zep", "gasbag1")?.Status.ToString() ?? "none"))})");
        ctx.Check(peers[0].ZvzPlay!.Spoken.Contains("snd_Zep_GBlost") && peers[1].ZvzPlay!.Spoken.Contains("snd_Zep_GBlost")
                  && peers[2].ZvzPlay!.Spoken.Contains("snd_Zep_GBdest") && !peers[2].ZvzPlay!.Spoken.Contains("snd_Zep_GBlost"),
            $"team 2's machines hear their gas bag lost and team 1's hears it destroyed ({Spoken(peers)})");

        Kill(peers, "multiplayer2zep", "lbroad1", seat: 0);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(0) == ZeppelinVersus.GasbagScore),
            $"a broadside cannon of multiplayer2zep scores its killer its bound gasbag1 on every machine ({Scores(peers)})");
        Kill(peers, "multiplayer2zep", "gasbag1", seat: 1);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(1) == 0 && Pool(p, "multiplayer2zep", "gasbag1") is { Status: DestructibleRegistry.State.Destroyed }),
            $"ABLE-TO-FAIL CONTROL: the bag itself then dies and scores nobody, its count spent by the cannon ({Scores(peers)})");

        Kill(peers, "multiplayer1zep", "gasbag2", seat: 1);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(1) == ZeppelinVersus.OwnGasbagScore),
            $"a team 2 seat downing its own hull's gasbag2 costs it {-ZeppelinVersus.OwnGasbagScore} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => !p.Versus!.Completed && p.ZeppelinHulls!.SurvivorsOf("multiplayer1zep") == 3),
            $"ABLE-TO-FAIL CONTROL: with three of five bags standing multiplayer1zep flies on and the match runs ({string.Join(", ", peers.Select(p => p.ZeppelinHulls!.SurvivorsOf("multiplayer1zep")))})");
    }

    // A downed seat comes back by its hull, above anything the spawn table holds.
    private static void Returns(TestContext ctx, GameSession[] peers)
    {
        int before = peers[0].SpawnsTaken;
        var owner = peers[2];
        owner.SeatRigs[2].Controller!.DebugForceCrash(owner.SeatRigs[0].Controller!.PlayerIndex);
        for (int step = 0; step < GrantSteps && peers[0].SpawnsTaken == before; step++)
        {
            Lockstep(1, peers);
        }

        Lockstep(PartSteps, peers);
        var at = peers.Select(p => p.SeatRigs[2].Controller!.WorldPosition).ToArray();
        ctx.Check(peers[0].SpawnsTaken > before && at.All(p => p.Y >= ZeppelinVersus.RespawnFloor - 50f) && at.All(p => p.DistanceTo(at[0]) < 50f),
            $"the downed seat returns at the respawn ring's height on every machine, over the table's 800 m top ({string.Join(" | ", at.Select(p => $"({p.X:0},{p.Y:0},{p.Z:0})"))})");
    }

    // The third bag of multiplayer1zep takes it below three standing and ends the match.
    private static void HullLost(TestContext ctx, GameSession[] peers)
    {
        Kill(peers, "multiplayer1zep", "gasbag3", seat: 2);
        ctx.Check(peers.All(p => p.Versus!.ScoreOf(2) == 2 * ZeppelinVersus.GasbagScore),
            $"the third bag scores its killer before the hull goes ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Versus!.Completed && p.MatchEnd == NetMatchEnd.Objective && p.Versus!.ObjectiveWinner == 1),
            $"multiplayer1zep lost ends the match on its objective for team 1 on every machine ({string.Join(", ", peers.Select(p => $"{p.MatchEnd}/{p.Versus!.ObjectiveWinner}"))})");
        ctx.Check(peers.All(p => p.Versus!.TeamTotalOf(1) == p.Versus!.TeamScoreOf(1) + VersusMatch.HullLossBonus
                                 && p.Versus!.TeamTotalOf(2) == p.Versus!.TeamScoreOf(2)),
            $"and team 1 takes the {VersusMatch.HullLossBonus} bonus on every board, the losing side none ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Versus!.TeamScoreOf(1) < ScoreTarget && p.Versus!.TeamTotalOf(1) >= ScoreTarget),
            $"ABLE-TO-FAIL CONTROL: the bonus carries team 1 past the {ScoreTarget}-point limit only on the board, which the limit never reads ({Scores(peers)})");
        var titles = peers.Select(p => VersusBoard.Title(p.Versus!)).ToArray();
        ctx.Check(titles.All(t => t == "RED SQUADRON WINS"),
            $"every machine's board names the side whose hull survived ({string.Join(" | ", titles)})");
        ctx.Check(peers.All(p => p.ZvzPlay!.LinesPosted > 0) && peers[2].ZvzPlay!.Spoken.Contains("snd_Zep_dest")
                  && peers[0].ZvzPlay!.Spoken.Contains("snd_Zep_lost") && peers[1].ZvzPlay!.Spoken.Contains("snd_Zep_lost"),
            $"and every machine posts the ending, the winners hearing a zeppelin destroyed and the losers theirs lost ({Spoken(peers)})");
    }

    // A part killed on the host in one seat's name, as the host's copy of that seat's round would.
    private static void Kill(GameSession[] peers, string hull, string part, int seat)
    {
        var host = peers[0];
        if (Pool(host, hull, part) is { } pool && host.NetWorld?.World is { } world)
        {
            world.DamageAt(pool.Anchor, Overkill, host.SeatRigs[seat].Controller!.PlayerIndex);
        }

        Lockstep(PartSteps, peers);
    }

    private static DestructibleRegistry.Instance? Pool(GameSession peer, string hull, string part) =>
        peer.NetWorld?.World?.Destructibles.All.FirstOrDefault(inst =>
            string.Equals(inst.Owner, hull, StringComparison.OrdinalIgnoreCase)
            && string.Equals(AnimRuntime.NameOf(inst.Anchor), part, StringComparison.OrdinalIgnoreCase));

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

    private static string Spoken(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", p.ZvzPlay!.Spoken)));

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 3).Select(p.Versus!.ScoreOf))
            + $" teams {p.Versus!.TeamScoreOf(1)}({p.Versus!.TeamTotalOf(1)})/{p.Versus!.TeamScoreOf(2)}({p.Versus!.TeamTotalOf(2)})"));
}
