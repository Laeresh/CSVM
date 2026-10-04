using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Lobby launches flown as the lobby makes them, on <see cref="NetCombatSuites"/>'s rig.
/// The Above the Clouds row flies a chapter that is not Instant Action's clouds. A Stunt Race flies a
/// chapter's Instant Action course rather than a multiplayer map.</summary>
internal static class NetLobbyEnvironmentSuites
{
    private const ulong HostSeed = 0xC0FFEE91UL;

    private const int SettleSteps = 20;

    // The race window the host types into the Time box, in minutes, unlike the type's default.
    private const int RaceMinutes = 7;

    // The race's environment row, NW Boeing Field, whose chapter ships a Danger Zone course.
    private const string RaceChapter = "C1";

    // Generous against the opening count's five seconds at the fixed step and the start gate.
    private const int OpeningStepLimit = 900;

    // How far a seat may stand from the spawn on its GO step: one step's flight at race speed.
    private const float SpawnReach = 5f;

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
        var spec = SessionSpec.FromMenu(cli, chapter, new[] { "player_pfighter" }, MenuMode.Versus, missionType: DogfightMissionType.ZeppelinVsZeppelin);
        ctx.Check(spec is { Chapter: "C1C", Mission: SessionSpec.ZvzMission, Versus: true, MissionType: DogfightMissionType.ZeppelinVsZeppelin },
            $"the row launches C1C's MP3 as Zeppelin vs Zeppelin ({spec.Chapter}/{spec.Mission}, type {spec.MissionType})");
        ctx.Check(LoadScreens.MultiplayerKey(spec.Chapter, DogfightMissionType.ZeppelinVsZeppelin, true) == "loading_m3z",
            $"and its load screen names the row's own dialog ({LoadScreens.MultiplayerKey(spec.Chapter, DogfightMissionType.ZeppelinVsZeppelin, true)})");

        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, spec.Chapter, spec.Mission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, spec.Chapter), $"{spec.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{spec.Chapter}/{spec.Mission} zrdr");
        var starts = Zeppelins.Load(missionZrdr).ToDictionary(z => z.Node, z => z.Position, StringComparer.OrdinalIgnoreCase);

        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(9101));
        var roster = NetCombatSuites.Roster(2).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
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

            ctx.Check(peers.All(p => p.Dogfight?.ZvzPlay is { } zvz && zvz.Rules.TeamOfHull(0) == 2 && zvz.Rules.TeamOfHull(1) == 1
                                     && p.ZeppelinHulls?.NodeAt(0) == "multiplayer1zep" && p.ZeppelinHulls.NodeAt(1) == "multiplayer2zep"),
                $"every machine flies multiplayer1zep for team 2 and multiplayer2zep for team 1 ({string.Join(" | ", peers.Select(p => p.Dogfight?.ZvzPlay == null ? "none" : $"{p.ZeppelinHulls?.NodeAt(0)} {p.ZeppelinHulls?.NodeAt(1)}"))})");
            if (peers.Any(p => p.Dogfight?.ZvzPlay == null || p.ZeppelinHulls == null))
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

            ctx.Check(peers.All(p => p.Dogfight?.RearmPlay is { BaseCount: 2 }),
                $"both machines list zep_rearm_node_1 and _2 ({string.Join(" | ", peers.Select(p => p.Dogfight?.RearmPlay?.BaseCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"))})");

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

    [Suite("net-lobby-stunt-race",
        "a two-seat Stunt Race launched through the lobby's own options and the menu's own spec on NW "
        + "Boeing Field with a 7 minute Time box and Limited Lives left ticked: both machines fly C1's IA1 "
        + "with no lives limit, no Instant Action mission, no AI and no Dogfight match, load the same Danger "
        + "Zone course, put both seats on the one spawn (each local seat on the opening count's rails "
        + "behind it), and build a race for their own pilot alone with a 420 s window under the seat's "
        + "callsign. No aircraft on either machine carries a loadout and every one races. Each machine stamps its own "
        + "seat's ghost with that seat's first-person layer and the remote seat's with the layer every "
        + "camera draws, and both windows open after the opening count on one step with each local seat "
        + "rolled onto the spawn")]
    internal static void AStuntRaceFliesTheChaptersCourse(TestContext ctx)
    {
        var options = new DogfightOptionsMessage(1, (byte)DogfightLobby.EnvironmentOf(RaceChapter), (byte)DogfightMissionType.StuntRace,
            DogfightVictory.Time, RaceMinutes, DogfightLobby.DefaultScore, true, DogfightLobby.DefaultLives, false);
        var rules = DogfightLobby.RulesOf(options);
        var specs = new SessionSpec[2];
        for (int i = 0; i < specs.Length; i++)
        {
            specs[i] = SessionSpec.FromMenu(SessionSpec.Parse(new[] { "--mute", "--no-pads" }), DogfightLobby.ChapterOf(options.Environment),
                new[] { "player_pfighter" }, DogfightLobby.LaunchMode(options), vsTimeMinutes: rules.TimeLimitMinutes,
                vsLives: rules.Lives, vsAutoRespawn: rules.AutoRespawn, missionType: rules.MissionType);
        }

        ctx.Check(specs.All(s => s is
        {
            Chapter: RaceChapter, Mission: SessionSpec.StuntRaceMission, Stunt: true, Versus: false,
            MissionType: DogfightMissionType.StuntRace, StuntRaceMinutes: RaceMinutes, IaDef: null, VsLives: 0
        }),
            $"the lobby's options launch C1's IA1 as a {RaceMinutes} minute race on both machines ({specs[0].Chapter}/{specs[0].Mission}, {specs[0].MissionType}, {specs[0].StuntRaceMinutes} min)");

        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RaceChapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, RaceChapter), $"{RaceChapter} gamez");
        ctx.RequireData(missionZrdr, $"{RaceChapter}/{SessionSpec.StuntRaceMission} zrdr");

        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(9102));
        var roster = NetCombatSuites.Roster(2);
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, specs[i], mesh[i], isHost: i == 0, HostSeed + 2 + (ulong)i, i == 0 ? roster : null));
            }

            ctx.Check(ends.All(e => e.Built), $"two sessions build C1's IA1 in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            ctx.Check(peers.All(p => p.InstantAction == null && p.AiAircraftCount == 0 && p.Dogfight == null && p.Generators == null),
                $"neither machine flies an Instant Action mission, an AI aircraft or a Dogfight match ({string.Join(" | ", peers.Select(p => $"{p.InstantAction != null} {p.AiAircraftCount} {p.Dogfight != null}"))})");

            var courses = new List<string>();
            for (int machine = 0; machine < 2; machine++)
            {
                var own = peers[machine].SeatRigs[machine].Controller;
                courses.Add(own?.Stunt is { } run ? string.Join("/", run.Zones.Select(z => z.Description)) : "none");
            }

            int zones = peers[0].SeatRigs[0].Controller?.Stunt?.TotalCount ?? 0;
            ctx.Check(zones > 0 && courses[0] == courses[1] && peers[1].SeatRigs[1].Controller?.Stunt?.TotalCount == zones,
                $"both machines load the same {zones}-zone course ({courses[0]} | {courses[1]})");
            ctx.Check(peers.All(p => p.SeatRigs.Count == 2) && peers[0].SeatRigs[1].Controller?.Stunt == null && peers[1].SeatRigs[0].Controller?.Stunt == null,
                $"and neither runs the course for the seat its other machine flies");

            // A seat flown elsewhere stands on the spawn. This machine's own seat stands back along
            // the opening count's rails from it, the same rails on both machines.
            var spawn = peers[0].SeatRigs[1].Controller!.WorldPosition;
            var railed = peers[0].SeatRigs[0].Controller!.WorldPosition;
            ctx.Check(peers[1].SeatRigs[0].Controller!.WorldPosition.DistanceTo(spawn) < 0.01f
                      && peers[1].SeatRigs[1].Controller!.WorldPosition.DistanceTo(railed) < 0.01f,
                $"both machines put both seats on the one spawn, {spawn.DistanceTo(railed):0} m of rails ahead of each local seat ({spawn:F1}, {railed:F1})");

            Raced(ctx, peers, roster);
            Unarmed(ctx, peers);
            Ghosts(ctx, peers);
            Opened(ctx, peers, spawn);
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

    // Both machines step together until each window opens: on one step, with each local seat rolled
    // off its rails onto the spawn.
    private static void Opened(TestContext ctx, GameSession[] peers, Vector3 spawn)
    {
        var opened = new int[peers.Length];
        var at = new Vector3[peers.Length];
        Array.Fill(opened, -1);
        for (int step = 0; step < OpeningStepLimit && opened.Any(s => s < 0); step++)
        {
            foreach (var session in peers)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }

            for (int machine = 0; machine < peers.Length; machine++)
            {
                var own = peers[machine].SeatRigs[machine].Controller!;
                if (opened[machine] < 0 && own.Race!.MayStartRun)
                {
                    opened[machine] = step;
                    at[machine] = own.WorldPosition;
                }
            }
        }

        var races = peers.Select((p, i) => p.SeatRigs[i].Controller!.Race!).ToArray();
        ctx.Check(opened.All(s => s >= 0 && s == opened[0]) && races.All(r => r.TimeLeft > (RaceMinutes * 60f) - 1f),
            $"both windows open after the opening count on one step ({string.Join(", ", opened)}; {string.Join(", ", races.Select(r => $"{r.Phase} {r.TimeLeft:0.0}s"))})");
        ctx.Check(at.All(p => p.DistanceTo(spawn) < SpawnReach),
            $"and each local seat stands on the spawn at its GO ({string.Join(", ", at.Select(p => $"{p.DistanceTo(spawn):0.0} m"))})");
    }

    // Each machine's race times its own seat alone, under that seat's callsign, with the lobby's window.
    private static void Raced(TestContext ctx, GameSession[] peers, IReadOnlyList<NetSeat> roster)
    {
        var read = new List<string>();
        bool own = true;
        for (int machine = 0; machine < peers.Length; machine++)
        {
            var race = peers[machine].SeatRigs[machine].Controller?.Race;
            var other = peers[machine].SeatRigs[1 - machine].Controller;
            own &= race is { Racers.Count: 1 } && race.WindowSeconds == RaceMinutes * 60f
                && race.Racers[0].Index == machine && race.Racers[0].Callsign == roster[machine].Callsign && other?.Race == null;
            read.Add(race == null ? "none" : $"{race.WindowSeconds:0}s {string.Join("+", race.Racers.Select(r => $"{r.Index}:{r.Callsign}"))}");
        }

        ctx.Check(own, $"each machine races its own seat alone with a {RaceMinutes * 60} s window under its callsign ({string.Join(" | ", read)})");
    }

    // Every aircraft on both machines races, and none carries a loadout or a carried gunner.
    private static void Unarmed(TestContext ctx, GameSession[] peers)
    {
        var all = peers.SelectMany(p => p.SeatRigs.Select(r => r.Controller!)).ToArray();
        ctx.Check(all.All(c => c.Racing && c.Loadout == null && (c.Turrets == null || c.Turrets.Length == 0)),
            $"no aircraft on either machine carries a weapon, and every one races ({string.Join(", ", all.Select(c => $"{c.Racing}/{c.Loadout != null}"))})");
    }

    // The ghost stamp on each machine. Its own seat names that seat's first-person layer. The seat
    // flown elsewhere names the layer every camera draws, so no camera there takes it for its own.
    private static void Ghosts(TestContext ctx, GameSession[] peers)
    {
        var read = new List<string>();
        bool stamped = true;
        for (int machine = 0; machine < peers.Length; machine++)
        {
            for (int seat = 0; seat < 2; seat++)
            {
                uint want = seat == machine ? SplitScreen.FirstPersonLayer(seat) : SplitScreen.EveryCameraLayer;
                var (right, wrong) = Stamps(peers[machine].SeatRigs[seat].Controller!.PlaneModel!, want);
                stamped &= right > 0 && wrong == 0;
                read.Add($"m{machine}s{seat} {right}/{wrong}");
            }
        }

        ctx.Check(stamped, $"each machine stamps its own seat with that seat's layer and the remote seat with the every-camera layer (right/wrong: {string.Join(", ", read)})");
    }

    // How many mesh instances under a model carry an armed ghost naming the layer, and how many another.
    private static (int Right, int Wrong) Stamps(Node model, uint layer)
    {
        int right = 0, wrong = 0;
        var pending = new Stack<Node>();
        pending.Push(model);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            foreach (var child in node.GetChildren())
            {
                pending.Push(child);
            }

            if (node is MeshInstance3D mi && mi.GetInstanceShaderParameter(RaceGhost.Param) is { VariantType: Variant.Type.Vector4 } stamp
                && stamp.AsVector4() is { W: 1f } armed)
            {
                if ((uint)armed.X == layer)
                {
                    right++;
                }
                else
                {
                    wrong++;
                }
            }
        }

        return (right, wrong);
    }
}
