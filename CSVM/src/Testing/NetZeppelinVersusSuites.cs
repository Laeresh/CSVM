using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Spec;
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

    // Above team 1's two enemy gas bags and within them plus a lost hull's term, whether player.zrd's
    // 10 or the executable's 100.
    private const int ScoreTarget = 30;

    // Enough of the broadside's rounds, each scaled up, to take a pilot's hull through any shield.
    private const int BroadsideShots = 8;
    private const float BroadsideScale = 1000f;

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
        + "reads its own hull as Defend and the other as Destroy under the team's name; both hulls' "
        + "broadsides are engaged on every machine; an enemy gas bag scores its killer "
        + "score_gas_kill and its own side score_my_gas_kill; a broadside cannon scores its bound bag "
        + "once; a downed seat returns by its hull above the spawn table; a pilot a hull's broadside "
        + "downs costs it no score and sets that hull's side's term to score_zep_kill; and the third "
        + "bag of multiplayer1zep ends the match for team 1 with player.zrd's score_zep that the "
        + "Score limit never reads; the board's Restart then takes every machine, guest and host, to "
        + "the lobby rather than rerunning on the burnt hull, and the lobby's next launch flies both "
        + "hulls whole on every machine, every part at full health, both broadsides engaged, the "
        + "markers and both rearm bases back and every score and term at zero")]
    internal static void AMatchOfHullsAcrossThreeMachines(TestContext ctx)
    {
        var spec = Spec(ctx);
        var roster = NetCombatSuites.Roster(3, spec).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        var exits = new int[3];
        try
        {
            if (Open(ctx, spec, roster, ends, exits, 4101) is not { } peers)
            {
                return;
            }

            Park(peers);
            Lockstep(SettleSteps, peers);
            HullMarkers(ctx, peers);
            Broadsides(ctx, peers);
            GasBags(ctx, peers);
            Returns(ctx, peers);
            HullKill(ctx, peers);
            HullLost(ctx, peers);
            ToTheLobby(ctx, peers, exits);

            // The lobby's next launch builds every machine's session afresh. Here each takes a new
            // carrier, since a suite's loopback end binds one listener for its life.
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ends.Clear();
            if (Open(ctx, spec, roster, ends, exits, 4102) is not { } again)
            {
                return;
            }

            // Read before a step, so no broadside of the new match can have touched a part yet.
            WholeAgain(ctx, again);
            Park(again);
            Lockstep(SettleSteps, again);
            HullMarkers(ctx, again);
            Broadsides(ctx, again);
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

    // Three sessions on a fresh loopback mesh, each counting its own exits, with both hulls seated
    // on every machine. Null when a build or the sides fail.
    private static GameSession[]? Open(TestContext ctx, SessionSpec spec, NetSeat[] roster,
        List<NetCombatSuites.Ends> ends, int[] exits, int meshSeed)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(meshSeed));
        for (int i = 0; i < 3; i++)
        {
            int machine = i;
            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                i == 0 ? roster : null, teamNames: TeamNames, exitSession: () => exits[machine]++));
        }

        ctx.Check(ends.All(e => e.Built), $"three sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
        if (!ends.All(e => e.Built))
        {
            return null;
        }

        var peers = ends.Select(e => e.Session).ToArray();
        return Sides(ctx, peers) ? peers : null;
    }

    // Quick returns, and every seat moved off its opening block, which stands a hundred metres over
    // its hull where a loop would ram it.
    private static void Park(GameSession[] peers)
    {
        foreach (var rig in peers.SelectMany(p => p.SeatRigs))
        {
            if (rig.Controller is { } pilot)
            {
                pilot.AutoRespawnAfter = QuickRespawn;
            }
        }

        for (int seat = 0; seat < 3; seat++)
        {
            var high = new Vector3(ParkX - (ParkSpacing * seat), ParkHeight, ParkZ);
            peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
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
        ctx.Check(peers.All(p => p.Dogfight?.ZvzPlay is { } zvz && zvz.Rules.TeamOfHull(0) == 2 && zvz.Rules.TeamOfHull(1) == 1
                                 && p.ZeppelinHulls?.NodeAt(0) == "multiplayer1zep" && p.ZeppelinHulls.NodeAt(1) == "multiplayer2zep"),
            $"every machine flies multiplayer1zep for team 2, first in seat order, and multiplayer2zep for team 1 ({string.Join(" | ", peers.Select(p => p.Dogfight?.ZvzPlay == null ? "none" : $"{p.ZeppelinHulls?.NodeAt(0)}:{p.Dogfight?.ZvzPlay.Rules.TeamOfHull(0)} {p.ZeppelinHulls?.NodeAt(1)}:{p.Dogfight?.ZvzPlay.Rules.TeamOfHull(1)}"))})");
        if (peers.Any(p => p.Dogfight?.ZvzPlay == null))
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

    // FUN_00496490 writes both hulls' engage byte, so every machine's broadsides are live, and
    // their rounds carry the hull that fired them.
    private static void Broadsides(TestContext ctx, GameSession[] peers)
    {
        var got = peers.Select(p => string.Join(",", new[] { "multiplayer1zep", "multiplayer2zep" }
            .Select(n => p.ZeppelinHulls!.BroadsideOf(n) is { } b ? $"{n}:{b.CannonsEngaged}/{b.Cannons.Count}" : $"{n}:none"))).ToArray();
        ctx.Check(peers.All(p => p.ZeppelinHulls!.NamesBroadsideRounds
                                 && p.ZeppelinHulls.BroadsideOf("multiplayer1zep") is { CannonsEngaged: true, Cannons.Count: 6 }
                                 && p.ZeppelinHulls.BroadsideOf("multiplayer2zep") is { CannonsEngaged: true, Cannons.Count: 6 }),
            $"both hulls' six broadsides are engaged on every machine, their rounds named for the hull ({string.Join(" | ", got)})");
    }

    // An enemy bag scores, a cannon scores its bound bag once, and a side's own bag costs.
    private static void GasBags(TestContext ctx, GameSession[] peers)
    {
        var scores = peers[0].Dogfight!.Match.Scores;
        Kill(peers, "multiplayer1zep", "gasbag1", seat: 2);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(2) == scores.GasbagKill && p.Dogfight!.Match.KillsOf(2) == 0),
            $"team 1's seat downing multiplayer1zep's gasbag1 scores it {scores.GasbagKill} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => Pool(p, "multiplayer1zep", "gasbag1") is { Status: DestructibleRegistry.State.Destroyed }),
            $"and the bag is destroyed on every machine ({string.Join(", ", peers.Select(p => Pool(p, "multiplayer1zep", "gasbag1")?.Status.ToString() ?? "none"))})");
        ctx.Check(peers[0].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_GBlost") && peers[1].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_GBlost")
                  && peers[2].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_GBdest") && !peers[2].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_GBlost"),
            $"team 2's machines hear their gas bag lost and team 1's hears it destroyed ({Spoken(peers)})");

        Kill(peers, "multiplayer2zep", "lbroad1", seat: 0);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(0) == scores.GasbagKill),
            $"a broadside cannon of multiplayer2zep scores its killer its bound gasbag1 on every machine ({Scores(peers)})");
        Kill(peers, "multiplayer2zep", "gasbag1", seat: 1);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(1) == 0 && Pool(p, "multiplayer2zep", "gasbag1") is { Status: DestructibleRegistry.State.Destroyed }),
            $"ABLE-TO-FAIL CONTROL: the bag itself then dies and scores nobody, its count spent by the cannon ({Scores(peers)})");

        Kill(peers, "multiplayer1zep", "gasbag2", seat: 1);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(1) == scores.OwnGasbagKill),
            $"a team 2 seat downing its own hull's gasbag2 costs it {-scores.OwnGasbagKill} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => !p.Dogfight!.Match.Completed && p.ZeppelinHulls!.SurvivorsOf("multiplayer1zep") == 3),
            $"ABLE-TO-FAIL CONTROL: with three of five bags standing multiplayer1zep flies on and the match runs ({string.Join(", ", peers.Select(p => p.ZeppelinHulls!.SurvivorsOf("multiplayer1zep")))})");
    }

    // A downed seat comes back by its hull, above anything the spawn table holds.
    private static void Returns(TestContext ctx, GameSession[] peers)
    {
        int before = peers[0].Dogfight!.SpawnsTaken;
        var owner = peers[2];
        owner.SeatRigs[2].Controller!.DebugForceCrash(owner.SeatRigs[0].Controller!.PlayerIndex);
        for (int step = 0; step < GrantSteps && peers[0].Dogfight!.SpawnsTaken == before; step++)
        {
            Lockstep(1, peers);
        }

        Lockstep(PartSteps, peers);
        var at = peers.Select(p => p.SeatRigs[2].Controller!.WorldPosition).ToArray();
        ctx.Check(peers[0].Dogfight!.SpawnsTaken > before && at.All(p => p.Y >= ZeppelinVersus.RespawnFloor - 50f) && at.All(p => p.DistanceTo(at[0]) < 50f),
            $"the downed seat returns at the respawn ring's height on every machine, over the table's 800 m top ({string.Join(" | ", at.Select(p => $"({p.X:0},{p.Y:0},{p.Z:0})"))})");
    }

    // Seat 2 of team 1, downed on the host by multiplayer1zep's broadside and reported by its own
    // machine as cause 3. Event 9 sets team 2's term and charges the victim nothing.
    private static void HullKill(TestContext ctx, GameSession[] peers)
    {
        var scores = peers[0].Dogfight!.Match.Scores;
        var round = WeaponDefs.Load(ctx.ZrdrPath).Get(ZeppelinRuntime.BroadsideWeaponId);
        if (round == null)
        {
            ctx.Check(false, $"the weapon catalogue holds the broadside's {ZeppelinRuntime.BroadsideWeaponId}");
            return;
        }

        int scoreBefore = peers[0].Dogfight!.Match.ScoreOf(2);
        int deathsBefore = peers[0].Dogfight!.Match.DeathsOf(2);
        var copy = peers[0].SeatRigs[2].Controller!;
        var owned = peers[2].SeatRigs[2].Controller!;
        for (int shot = 0; shot < BroadsideShots && !owned.Crashed; shot++)
        {
            copy.Body!.TakeProjectileHit(round, copy.WorldPosition, 0, ZeppelinVersus.BroadsideShooter(0), BroadsideScale);
            Lockstep(2, peers);
        }

        Lockstep(PartSteps, peers);
        ctx.Check(peers.All(p => p.Dogfight!.Match.DeathsOf(2) == deathsBefore + 1 && p.Dogfight!.Match.ScoreOf(2) == scoreBefore),
            $"a pilot downed by multiplayer1zep's broadside takes a death and no score on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Dogfight!.Match.TeamTermOf(2) == scores.ZeppelinKill && p.Dogfight!.Match.TeamScoreOf(2) == p.Dogfight!.Match.TeamTotalOf(2) - scores.ZeppelinKill),
            $"and team 2, whose hull fired, has its term set to {scores.ZeppelinKill} on every board, never in the Score limit's total ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Dogfight!.Match.TeamTermOf(1) == 0),
            $"ABLE-TO-FAIL CONTROL: the victim's own side's term stays 0 ({Scores(peers)})");

        // Back in the air before the last bag, so the ending finds every seat flying.
        for (int step = 0; step < GrantSteps && owned.Crashed; step++)
        {
            Lockstep(1, peers);
        }

        Lockstep(PartSteps, peers);
    }

    // The third bag of multiplayer1zep takes it below three standing and ends the match.
    private static void HullLost(TestContext ctx, GameSession[] peers)
    {
        var scores = peers[0].Dogfight!.Match.Scores;
        Kill(peers, "multiplayer1zep", "gasbag3", seat: 2);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(2) == 2 * scores.GasbagKill),
            $"the third bag scores its killer before the hull goes ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Dogfight!.Match.Completed && p.Dogfight!.End == NetMatchEnd.Objective && p.Dogfight!.Match.ObjectiveWinner == 1),
            $"multiplayer1zep lost ends the match on its objective for team 1 on every machine ({string.Join(", ", peers.Select(p => $"{p.Dogfight!.End}/{p.Dogfight!.Match.ObjectiveWinner}"))})");
        ctx.Check(peers.All(p => p.Dogfight!.Match.TeamTotalOf(1) == p.Dogfight!.Match.TeamScoreOf(1) + scores.HullLoss
                                 && p.Dogfight!.Match.TeamTotalOf(2) == p.Dogfight!.Match.TeamScoreOf(2) + scores.ZeppelinKill),
            $"and team 1 takes the lost hull's {scores.HullLoss} on every board, the losing side only its own term ({Scores(peers)})");
        ctx.Check(peers.All(p => p.Dogfight!.Match.TeamScoreOf(1) < ScoreTarget && p.Dogfight!.Match.TeamTotalOf(1) >= ScoreTarget),
            $"ABLE-TO-FAIL CONTROL: the bonus carries team 1 past the {ScoreTarget}-point limit only on the board, which the limit never reads ({Scores(peers)})");
        var titles = peers.Select(p => VersusBoard.Title(p.Dogfight!.Match)).ToArray();
        ctx.Check(titles.All(t => t == "RED SQUADRON WINS"),
            $"every machine's board names the side whose hull survived ({string.Join(" | ", titles)})");
        ctx.Check(peers.All(p => p.Dogfight!.ZvzPlay!.LinesPosted > 0) && peers[2].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_dest")
                  && peers[0].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_lost") && peers[1].Dogfight!.ZvzPlay!.Spoken.Contains("snd_Zep_lost"),
            $"and every machine posts the ending, the winners hearing a zeppelin destroyed and the losers theirs lost ({Spoken(peers)})");
    }

    // The board's Restart on a guest and then the host, each on a finished match. The original's end
    // takes every machine to the lobby, and a rerun in place would fly on the burnt hull.
    private static void ToTheLobby(TestContext ctx, GameSession[] peers, int[] exits)
    {
        peers[2].SeatRigs[2].Controller!.RestartMatch!();
        peers[0].SeatRigs[0].Controller!.RestartMatch!();
        Lockstep(SettleSteps, peers);
        ctx.Check(exits[2] == 1 && exits[0] == 1 && exits[1] == 0,
            $"the Restart of a guest and of the host each takes that machine to the lobby, and no other ({string.Join(",", exits)} exits)");
        ctx.Check(peers.All(p => p.Dogfight!.Match.Completed && p.ZeppelinHulls!.IsDead("multiplayer1zep")),
            $"ABLE-TO-FAIL CONTROL: nothing reruns in place, every machine's match still ended on its lost hull ({string.Join(", ", peers.Select(p => $"{p.Dogfight!.Match.Completed}/{p.ZeppelinHulls!.IsDead("multiplayer1zep")}"))})");
    }

    // The lobby's next launch, a fresh session on every machine. Both hulls fly whole with every
    // part at full health, both rearm bases stand, and every score and term is zero.
    private static void WholeAgain(TestContext ctx, GameSession[] peers)
    {
        var hulls = new[] { "multiplayer1zep", "multiplayer2zep" };
        var parts = peers.Select(p => p.Wire.World?.World?.Destructibles.All
            .Where(inst => hulls.Any(h => string.Equals(inst.Owner, h, StringComparison.OrdinalIgnoreCase))).ToList()
            ?? new List<DestructibleRegistry.Instance>()).ToArray();
        ctx.Check(peers.All(p => hulls.All(h => !p.ZeppelinHulls!.IsDead(h) && p.ZeppelinHulls.SurvivorsOf(h) == 5)),
            $"both hulls fly again with all five gas bags standing on every machine ({string.Join(" | ", peers.Select(p => string.Join(",", hulls.Select(h => $"{p.ZeppelinHulls!.IsDead(h)}:{p.ZeppelinHulls.SurvivorsOf(h)}"))))})");
        ctx.Check(parts.All(list => list.Count > 0 && list.All(inst => inst.Status == DestructibleRegistry.State.Healthy && inst.Health >= inst.MaxHealth)),
            $"and every part of both, the burnt bags and the downed cannon among them, stands at full health ({string.Join(" | ", parts.Select(l => $"{l.Count(i => i.Status == DestructibleRegistry.State.Healthy && i.Health >= i.MaxHealth)}/{l.Count}"))})");
        ctx.Check(peers.All(p => Pool(p, "multiplayer1zep", "gasbag3") is { Status: DestructibleRegistry.State.Healthy }
                                 && Pool(p, "multiplayer2zep", "lbroad1") is { Status: DestructibleRegistry.State.Healthy }),
            $"ABLE-TO-FAIL CONTROL: the bag that lost the last match and the cannon downed in it are found and whole ({string.Join(", ", peers.Select(p => $"{Pool(p, "multiplayer1zep", "gasbag3")?.Status.ToString() ?? "none"}/{Pool(p, "multiplayer2zep", "lbroad1")?.Status.ToString() ?? "none"}"))})");
        ctx.Check(peers.All(p => p.Dogfight?.RearmPlay is { BaseCount: 2 } r && r.BaseAt(0)?.Team == 2 && r.BaseAt(1)?.Team == 1),
            $"both hulls' rearm bases stand again, each serving its side ({string.Join(" | ", peers.Select(p => p.Dogfight?.RearmPlay is { } r ? $"{r.BaseAt(0)?.Team}/{r.BaseAt(1)?.Team}" : "none"))})");
        ctx.Check(peers.All(p => !p.Dogfight!.Match.Completed && p.Dogfight!.End == NetMatchEnd.Running && p.Dogfight!.Match.ObjectiveWinner == 0
                                 && Enumerable.Range(0, 3).All(s => p.Dogfight!.Match.ScoreOf(s) == 0)
                                 && new[] { 1, 2 }.All(t => p.Dogfight!.Match.TeamTermOf(t) == 0 && p.Dogfight!.Match.TeamTotalOf(t) == 0)),
            $"and the match runs from zero on every machine, every seat's score and both sides' terms ({Scores(peers)})");
    }

    // A part killed on the host in one seat's name, as the host's copy of that seat's round would.
    private static void Kill(GameSession[] peers, string hull, string part, int seat)
    {
        var host = peers[0];
        if (Pool(host, hull, part) is { } pool && host.Wire.World?.World is { } world)
        {
            world.DamageAt(pool.Anchor, Overkill, host.SeatRigs[seat].Controller!.PlayerIndex);
        }

        Lockstep(PartSteps, peers);
    }

    private static DestructibleRegistry.Instance? Pool(GameSession peer, string hull, string part) =>
        peer.Wire.World?.World?.Destructibles.All.FirstOrDefault(inst =>
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
        string.Join(" | ", peers.Select(p => string.Join(",", p.Dogfight!.ZvzPlay!.Spoken)));

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 3).Select(p.Dogfight!.Match.ScoreOf))
            + $" teams {p.Dogfight!.Match.TeamScoreOf(1)}({p.Dogfight!.Match.TeamTotalOf(1)})/{p.Dogfight!.Match.TeamScoreOf(2)}({p.Dogfight!.Match.TeamTotalOf(2)})"));
}
