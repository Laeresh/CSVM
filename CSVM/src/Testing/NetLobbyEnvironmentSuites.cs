using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Launch;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The lobby's Above the Clouds row flown as the lobby launches it, on
/// <see cref="NetCombatSuites"/>'s rig. The row's chapter is not the one Instant Action flies for
/// its clouds, and nothing else launches a match on it.</summary>
internal static class NetLobbyEnvironmentSuites
{
    private const ulong HostSeed = 0xC0FFEE91UL;

    private const int SettleSteps = 20;

    // How far a hull may stand from its authored start after the settle, well under the kilometres
    // between the two hulls.
    private const float HullDrift = 150f;

    private const string TrackedFlight = "--hold=0.3,0,0,1";

    // Team 2 is first in seat order, so it flies hull 0.
    private static readonly int[] Teams = { 2, 1 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-lobby-above-the-clouds",
        "the lobby's Above the Clouds row launched as Zeppelin vs Zeppelin through the menu's own "
        + "spec: it flies C1C's MP3, both sessions build, every machine flies multiplayer1zep for "
        + "team 2 and multiplayer2zep for team 1, each hull stands where C1C's zeppelins.zrd starts "
        + "it, both machines list zep_rearm_node_1 and _2, each seat opens nearer its own hull, and "
        + "the load screen names loading_m3z")]
    internal static void AboveTheCloudsFliesC1C(TestContext ctx)
    {
        string chapter = DogfightLobby.ChapterOf(0);
        var cli = SessionSpec.Parse(new[] { "--mute", "--no-pads", TrackedFlight });
        var spec = SessionSpec.FromMenu(cli, chapter, new[] { "player_pfighter" }, MenuMode.Versus, zeppelinVsZeppelin: true);
        ctx.Check(spec is { Chapter: "C1C", Mission: SessionSpec.ZvzMission, Versus: true, ZeppelinVsZeppelin: true },
            $"the row launches C1C's MP3 as Zeppelin vs Zeppelin ({spec.Chapter}/{spec.Mission}, zvz {spec.ZeppelinVsZeppelin})");
        ctx.Check(LoadScreens.MultiplayerKey(spec.Chapter, false, true, true) == "loading_m3z",
            $"and its load screen names the row's own dialog ({LoadScreens.MultiplayerKey(spec.Chapter, false, true, true)})");

        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, spec.Chapter, spec.Mission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, spec.Chapter), $"{spec.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{spec.Chapter}/{spec.Mission} zrdr");
        var starts = Zeppelins.Load(missionZrdr).ToDictionary(z => z.Node, z => z.Position, StringComparer.OrdinalIgnoreCase);

        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(9101));
        var roster = NetCombatSuites.Roster(2, spec).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            ctx.Check(ends.All(e => e.Built), $"two sessions build C1C's MP3 in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            for (int step = 0; step < SettleSteps; step++)
            {
                foreach (var session in peers)
                {
                    session._PhysicsProcess(GameClock.FixedDt);
                }
            }

            ctx.Check(peers.All(p => p.ZvzPlay is { } zvz && zvz.Rules.TeamOfHull(0) == 2 && zvz.Rules.TeamOfHull(1) == 1
                                     && p.ZeppelinHulls?.NodeAt(0) == "multiplayer1zep" && p.ZeppelinHulls.NodeAt(1) == "multiplayer2zep"),
                $"every machine flies multiplayer1zep for team 2 and multiplayer2zep for team 1 ({string.Join(" | ", peers.Select(p => p.ZvzPlay == null ? "none" : $"{p.ZeppelinHulls?.NodeAt(0)} {p.ZeppelinHulls?.NodeAt(1)}"))})");
            if (peers.Any(p => p.ZvzPlay == null || p.ZeppelinHulls == null))
            {
                return;
            }

            var hulls = peers[0].ZeppelinHulls!;
            var drift = new List<string>();
            bool placed = true;
            for (int hull = 0; hull < 2; hull++)
            {
                string node = hulls.NodeAt(hull)!;
                var at = hulls.HullPositionAt(hull);
                float off = at is { } p && starts.TryGetValue(node, out var start) ? p.DistanceTo(start) : float.MaxValue;
                placed &= off < HullDrift && !hulls.IsDead(node);
                drift.Add($"{node} {off:0} m");
            }

            ctx.Check(placed, $"each hull stands alive where C1C's zeppelins.zrd starts it ({string.Join(", ", drift)})");

            ctx.Check(peers.All(p => p.RearmPlay is { BaseCount: 2 }),
                $"both machines list zep_rearm_node_1 and _2 ({string.Join(" | ", peers.Select(p => p.RearmPlay?.BaseCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"))})");

            var near = new List<string>();
            bool beside = true;
            for (int seat = 0; seat < 2; seat++)
            {
                var at = peers[seat].SeatRigs[seat].Controller!.WorldPosition;
                float own = at.DistanceTo(hulls.HullPositionAt(seat)!.Value);
                float other = at.DistanceTo(hulls.HullPositionAt(1 - seat)!.Value);
                beside &= own < other;
                near.Add($"{own:0}/{other:0}");
            }

            ctx.Check(beside, $"and each seat opens nearer its own hull, in C1C's net.zrd block ({string.Join(", ", near)} m own/other)");
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
}
