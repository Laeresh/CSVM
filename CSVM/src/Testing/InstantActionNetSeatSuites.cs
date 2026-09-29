using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.InstantAction;
using CSVM.Session.Launch;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>Where an Instant Action actor's patrol walk is seated, over the real actor build. The
/// original seats every actor's net at its spawn placement and never reseats it on activation. A
/// wave parked at the origin keeps the walk seated there (docs/org/aiPilot.md).</summary>
internal static class InstantActionNetSeatSuites
{
    private const string Chapter = "C1";
    private const string Mission = "IA1";

    [Suite("instant-action-wave-net-seat",
        "the Instant Action actor build over C1/IA1's own data seats every actor's walk on the chapter's "
        + "first net at its spawn: the ace at its spawn point, a wingman beside the player, wave 1 at the "
        + "spawn point it arrives on, and waves 2 to 4 at the origin parking pose facing yaw 0, where "
        + "zeppelin_run parks wave 1 as well; activating a wave-2 member at a spawn point whose own seat "
        + "differs keeps the origin walk, so its first net leg is the parked one")]
    internal static void InstantActionWaveNetSeat(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var built = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var roster = CampaignRosterSuites.Spawner(ctx, planesGamez, textures, live);
            var shipped = InstantAction.Load(missionZrdr);
            var twoAndTwo = shipped.Waves.Select((w, i) => w with { NumEnemies = i < 2 ? 2 : 0 }).ToList();

            var squad = Build(roster, built, ctx, missionZrdr, InstantAction.BuildFromWizard(shipped,
                "dogfight_squadron", shipped.PlayerPlane, numWingmen: 1, shipped.WingmanPlane, twoAndTwo,
                shipped.Lives));
            var net = squad.Enemies.FirstOrDefault()?.Pilot?.Patrol?.Net;
            if (net == null)
            {
                ctx.Check(false, $"{Chapter}'s wave members carry the chapter's first net");
                return;
            }
            ctx.Note($"{Chapter}'s first net '{net.Name}' (#{net.Id}), {net.Nodes.Count} nodes, anchor={net.Trailer?.Name ?? "-"}");
            var trailer = squad.Trailers.For(net);

            // Wave 1 arrives live at its spawn point, where its walk is seated.
            var wave1 = squad.Enemies.Take(2).ToList();
            var wave2 = squad.Enemies.Skip(2).ToList();
            ctx.Check(wave1.Count == 2 && wave1.All(m => !m.Inert) && wave2.Count == 2 && wave2.All(m => m.Inert),
                $"wave 1 is live and wave 2 parked after the build: live={wave1.Count(m => !m.Inert)} parked={wave2.Count(m => m.Inert)}");
            foreach (var m in wave1)
            {
                var want = SeatOf(net, trailer, m.WorldPosition, m.NoseDirection);
                ctx.Check(Leg(m) == want,
                    $"a wave-1 member's walk is seated at its arrival: {Leg(m)} vs {want}");
            }

            // Waves 2 to 4 are seated at the origin parking pose, facing yaw 0.
            var parked = SeatOf(net, trailer, Vector3.Zero, Vector3.Forward);
            foreach (var m in wave2)
            {
                ctx.Check(Leg(m) == parked,
                    $"a parked wave-2 member's walk is seated at the origin: {Leg(m)} vs {parked}");
            }

            // The wingman is seated beside the player, where it spawned.
            if (squad.Wingmen.FirstOrDefault() is { } wingman)
            {
                var want = SeatOf(net, trailer, wingman.WorldPosition, wingman.NoseDirection);
                ctx.Check(Leg(wingman) == want, $"the wingman's walk is seated at its spawn: {Leg(wingman)} vs {want}");
            }
            else
            {
                ctx.Check(false, $"the squadron build spawned its wingman");
            }

            // Activating a parked member keeps the origin walk. The arrival is a real wave spawn point
            // whose own seat differs, so a reseat on activation would be visible.
            var member = wave2[0];
            SpawnPoint? arrival = null;
            foreach (var sp in squad.SpawnList)
            {
                var fwd = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
                if (SeatOf(net, trailer, sp.Position, fwd) != parked)
                {
                    arrival = sp;
                    break;
                }
            }
            if (arrival is not { } at)
            {
                ctx.Check(false, $"one of {squad.SpawnList.Count} wave spawn points seats off the origin leg {parked}");
                return;
            }
            var atFwd = new Basis(Vector3.Up, Mathf.DegToRad(at.HeadingDeg)) * Vector3.Forward;
            var reseated = SeatOf(net, trailer, at.Position, atFwd);
            member.Activate(at.Position, at.Position + atFwd);
            ctx.Check(!member.Inert && member.WorldPosition.DistanceTo(at.Position) < 1f,
                $"the member is live at the spawn point: ({member.WorldPosition.X:0},{member.WorldPosition.Y:0},{member.WorldPosition.Z:0})");
            ctx.Check(Leg(member) == parked,
                $"its first net leg is still the parked one {parked}, not the arrival's {reseated}: {Leg(member)}");

            // The ace is seated at its own spawn point.
            var aceBuild = Build(roster, built, ctx, missionZrdr, InstantAction.BuildFromWizard(shipped,
                "dogfight_ace", shipped.PlayerPlane, numWingmen: 0, shipped.WingmanPlane, shipped.Waves,
                shipped.Lives));
            if (aceBuild.Enemies.FirstOrDefault() is { } ace)
            {
                var want = SeatOf(net, trailer, ace.WorldPosition, ace.NoseDirection);
                ctx.Check(Leg(ace) == want, $"the ace's walk is seated at its spawn: {Leg(ace)} vs {want}");
            }
            else
            {
                ctx.Check(false, $"the dogfight_ace build spawned its ace");
            }

            // zeppelin_run parks wave 1 too: the generator releases it, so it keeps the origin seat.
            var zepBuild = Build(roster, built, ctx, missionZrdr, InstantAction.BuildFromWizard(shipped,
                "zeppelin_run", shipped.PlayerPlane, numWingmen: 0, shipped.WingmanPlane, twoAndTwo,
                shipped.Lives));
            var zepWave1 = zepBuild.Enemies.Take(2).ToList();
            ctx.Check(zepWave1.Count == 2 && zepWave1.All(m => m.Inert && Leg(m) == parked),
                $"zeppelin_run parks wave 1 seated at the origin: {string.Join(", ", zepWave1.Select(m => $"{Leg(m)} inert={m.Inert}"))}");
        }
        finally
        {
            pool?.Free();
            foreach (var fc in built)
            {
                fc.Free();
            }
            textures.Dispose();
        }
    }

    private static (int From, int To) Leg(FlightController rig) =>
        rig.Pilot?.Patrol is { } walk ? (walk.LegStartIndex, walk.CurrentIndex) : (-2, -2);

    private static (int From, int To) SeatOf(AiNet net, Func<Vector3?>? trailer, Vector3 at, Vector3 nose)
    {
        var walk = new AiNetFollower(net, new Random(1), trailerTarget: trailer);
        walk.Seat(at, nose);
        return (walk.LegStartIndex, walk.CurrentIndex);
    }

    // One actor build through the director, the human parked 800 m over the origin. The spawns come
    // back in build order: the ace, the wingmen, then waves 1 to 4.
    private static ActorBuild Build(FlightRoster roster, List<FlightController> built, TestContext ctx,
        string missionZrdr, InstantActionDef def)
    {
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()),
            Chapter, new[] { "player_bhawk" }, MenuMode.Free, def);
        var director = InstantActionDirector.TryCreate(spec)!;
        var leadAt = new Vector3(0f, 800f, 0f);
        var lead = roster.SpawnAi(new AiSpawn("player_bhawk", leadAt, leadAt + Vector3.Forward,
            AiPilot.HoldingCourse(leadAt, leadAt + Vector3.Forward), Team: AimAssist.PlayerTeam));
        built.Add(lead);
        var trailers = new NetTrailerTargets(() => lead.WorldPosition, null);
        var spawnList = SpawnPoints.LoadIa(missionZrdr, def.MissionType);
        var result = new ActorBuild(new List<FlightController>(), new List<FlightController>(), trailers,
            spawnList ?? new List<SpawnPoint>());
        director.BuildActors(new InstantActionDirector.ActorBuildInputs
        {
            Rigs = new List<PlayerRig> { new() { Controller = lead } },
            ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter),
            MissionZrdrPath = missionZrdr,
            ZrdrPath = ctx.ZrdrPath,
            MessagesPath = ctx.MessagesPath,
            SpawnList = spawnList,
            SpawnBase = 0,
            LiveryResolver = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            NetTrailers = trailers,
            Spawn = spawn =>
            {
                var fc = roster.SpawnAi(spawn);
                built.Add(fc);
                (fc.Team == AimAssist.PlayerTeam ? result.Wingmen : result.Enemies).Add(fc);
                return fc;
            },
            RegisterVoice = (_, _, _, _) => { },
        });
        return result;
    }

    private sealed record ActorBuild(List<FlightController> Enemies, List<FlightController> Wingmen,
        NetTrailerTargets Trailers, IReadOnlyList<SpawnPoint> SpawnList);
}
