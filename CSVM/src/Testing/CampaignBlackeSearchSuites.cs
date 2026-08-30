using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// C4/M02's search for Blacke, the mission whose only door out of its four search locations is a
/// <c>TRAVELERS</c> proximity read against a roster aircraft. The suite pins the authored shape,
/// then drives it over the mission's own BUILT world with a real roster: the warp places him, the
/// spot check resolves him, and a miss reads FALSE rather than unanswerable.
/// </summary>
internal static class CampaignBlackeSearchSuites
{
    private const string Chapter = "C4";

    private const string Mission = "M02";

    // Blacke's autogyro: an aiv roster block, and the reference every spot check names.
    private const string Gyro = "bhatgyro_1";

    // The objective that hides him, five seconds in.
    private const int WarpObjective = 23;

    // The one objective the four spot checks wake, and the mission's PRIMARY 1.
    private const int FoundObjective = 24;

    private const float SpotRadiusM = 500f;

    // What each search location naps its "he is not here" radio line by, and so the whole width of
    // the window a spot check has to read true in.
    private const float SilenceNapS = 2f;

    private const float StepDt = 1f / 60f;

    // How close to an authored waypoint the warped aircraft has to land. It is placed exactly, so
    // this is float slop and nothing else.
    private const float PlacedToleranceM = 1f;

    // The seed the world phase draws the waypoint with, so the suite knows which of the four the
    // mission picked and can assert the placement rather than describe it.
    private const int WarpSeed = 1;

    // The four search locations, and for each the spot check it wakes and the radio line that
    // kills that spot check two seconds later (targets.zrd names them Jimmy's Bar, McCoy's Diner,
    // the Shangri-La dancehall and the C9 brothel).
    private static readonly (int Zone, int Spot, int Silence)[] Locations =
    {
        (2, 31, 35),
        (3, 32, 36),
        (4, 33, 37),
        (6, 34, 38),
    };

    internal static void CampaignBlackeSearch(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        if (MissionAt(ctx, Chapter, Mission) is not { } mission)
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} is not in cm_sequence");
        }

        var script = ObjectiveScript.Load(missionZrdr);
        var blocks = AiSkills.LoadRoster(missionZrdr);
        var report = new StringBuilder();
        var points = CheckAuthored(ctx, script, report);

        var skills = AiSkills.Load(ctx.ZrdrPath);
        var director = CampaignDirector.Create(script, mission,
            CampaignProfileDef.NewProfile("Zachary"), null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
            Drive(ctx, world, director, blocks, skills, points, missionZrdr, chapterZrdr,
                texturesPath, report));

        ctx.WriteArtifact($"test-campaign-blacke-search-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: Blacke is warped, spotted at 500 m, and a miss reads false");
    }

    // The authored shape this suite consumes, read off the shipped file rather than restated. The
    // claim that matters is the last one: nothing but a spot check wakes the mission's PRIMARY 1,
    // so a spot check that cannot resolve its reference is a mission that cannot be finished.
    private static IReadOnlyList<WarpPoint> CheckAuthored(TestContext ctx, ObjectiveScript script,
        StringBuilder report)
    {
        var warp = Numbered(script, WarpObjective);
        ctx.Check(warp?.Warp is { } w && w.Vehicle.Equals(Gyro, StringComparison.OrdinalIgnoreCase),
            $"OBJECTIVE{WarpObjective} warps '{Gyro}'");
        var points = warp?.Warp?.Points ?? new List<WarpPoint>();
        ctx.Same(Locations.Length, points.Count,
            $"…to one of as many authored waypoints as there are search locations");
        foreach (var p in points)
        {
            report.AppendLine($"waypoint ({p.X:0},{p.Y:0},{p.Z:0}) heading {p.Heading:0} deg"
                + (p.PointName is { } name ? $" on path '{name}'" : ""));
        }

        int wakes = 0;
        foreach (var (zone, spot, silence) in Locations)
        {
            var spotter = Numbered(script, spot);
            bool named = spotter?.Travelers is { } t && t.Approaching && t.Group == null
                && string.Equals(t.WhereNode, Gyro, StringComparison.OrdinalIgnoreCase)
                && Mathf.IsEqualApprox(t.Radius, SpotRadiusM);
            ctx.Check(named,
                $"OBJECTIVE{spot} spots '{Gyro}' inside {SpotRadiusM:0} m");
            ctx.Check(Numbered(script, zone)?.WakeWhenComplete.Contains(spot) == true,
                $"…and search location OBJECTIVE{zone} is what wakes it");
            ctx.Check(Numbered(script, zone)?.NapWhenComplete is { } nap
                    && nap.Target == silence && Mathf.IsEqualApprox(nap.Seconds, SilenceNapS),
                $"…which also naps OBJECTIVE{silence} awake {SilenceNapS:0} s later");
            ctx.Check(Numbered(script, silence)?.KillWhenComplete.Contains(spot) == true,
                $"…and OBJECTIVE{silence} kills OBJECTIVE{spot} when it does");
            wakes += spotter?.WakeWhenComplete.Contains(FoundObjective) == true ? 1 : 0;
        }

        ctx.Same(Locations.Length, wakes,
            $"every spot check wakes OBJECTIVE{FoundObjective}, the mission's PRIMARY 1");
        ctx.Same(Locations.Length, WakersOf(script, FoundObjective),
            $"…and NOTHING ELSE does, so a spot check is the mission's only door");
        return points;
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director,
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, AiSkills skills,
        IReadOnlyList<WarpPoint> points, string missionZrdr, string chapterZrdr,
        string texturesPath, StringBuilder report)
    {
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? player = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            roster = Spawner(ctx, planesGamez, textures, live);

            var pose = PlayerPose(blocks);
            player = HumanRig(ctx, planesGamez, textures, live, pose.Position,
                pose.Position + pose.Forward);
            var human = player;
            var listener = pose.Position;

            director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = skills.MinAiActiveDist,
                Player = () => human,
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) => roster!.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });

            if (!director.Roster.TryGetValue(Gyro, out var gyro))
            {
                ctx.Check(false, $"the roster spawned '{Gyro}'");
                return;
            }

            ctx.Check(gyro.Inert, $"'{Gyro}' ships deactivated, and is warped while he still is");

            // ⚠ The reason the node-only resolve failed: the aiv block's name reaches the resolver
            // only through a gamez library root of the SAME name, and this chapter's is 'bhatgyro'.
            ctx.Same(0, world.Runtime.FindNodes(Gyro).Count,
                $"'{Gyro}' is NOT a world node, so only the roster can answer where he is");

            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                Projectiles = live,
                Gamez = world.Gamez,
                ListenerPosition = () => listener,
                PlayerAircraft = () => human,
                Rng = new Random(WarpSeed),
            });

            var graph = director.Graph;
            ctx.Check(graph != null, $"the world phase armed the objective graph");
            if (graph == null)
            {
                return;
            }

            var spawnedAt = gyro.WorldPosition;
            Advance(director, 6f);
            ctx.Check(graph.CompletedOf(WarpObjective),
                $"OBJECTIVE{WarpObjective} ran at its BEGIN_DORMANT 5 s");

            var hidAt = gyro.WorldPosition;
            var drawn = points[new Random(WarpSeed).Next(points.Count)];
            report.AppendLine($"spawned at ({spawnedAt.X:0},{spawnedAt.Y:0},{spawnedAt.Z:0}), "
                + $"hid at ({hidAt.X:0},{hidAt.Y:0},{hidAt.Z:0})");
            if (drawn.PointName is { Length: > 0 } path)
            {
                ctx.Check(director.Paths?.IsFrozen(Gyro) == false,
                    $"the drawn waypoint names path '{path}', which releases him onto it moving");
            }
            else
            {
                var want = new Vector3(drawn.X, drawn.Y, drawn.Z);
                ctx.Check(hidAt.DistanceTo(want) <= PlacedToleranceM,
                    $"WARP_VEHICLE put him on the drawn waypoint ({want.X:0},{want.Y:0},{want.Z:0})");
                ctx.Check(gyro.Inert, $"…and the placement left his INERT bit alone");
            }

            // The spot check reads the human field, so moving the listener alone leaves the
            // aeroplane the condition actually asks about back at its start point.
            Spot(ctx, director, graph, gyro, at =>
            {
                human.PlaceHeld(at, at + pose.Forward);
                listener = at;
            }, report);
        }
        finally
        {
            player?.Free();
            var members = new List<FlightController>(
                roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var rig in members)
            {
                rig.Free();
            }
            pool?.Free();
            textures.Dispose();
        }
    }

    // The regression itself. A miss must read FALSE, not "cannot answer": an unresolved reference
    // is counted rather than decided, and four of those in a row is the mission that cannot end.
    private static void Spot(TestContext ctx, CampaignDirector director, ObjectiveGraph graph,
        FlightController gyro, Action<Vector3> putPlayer, StringBuilder report)
    {
        int unresolvedBefore = graph.UnresolvedConditions;
        var (_, missSpot, _) = Locations[0];
        putPlayer(gyro.WorldPosition + new Vector3(0f, 0f, SpotRadiusM * 4f));
        graph.Wake(missSpot);
        Advance(director, StepDt * 4f);
        ctx.Check(!graph.CompletedOf(missSpot),
            $"a spot check {SpotRadiusM * 4f:0} m away does not complete");
        ctx.Same(unresolvedBefore, graph.UnresolvedConditions,
            $"…and it read FALSE rather than going unanswered, which is the whole bug");
        ctx.Check(graph.StateOf(FoundObjective) == ObjectiveState.Dormant,
            $"so OBJECTIVE{FoundObjective} is still dormant");

        var (_, hitSpot, _) = Locations[1];
        putPlayer(gyro.WorldPosition + new Vector3(0f, 0f, SpotRadiusM * 0.5f));
        graph.Wake(hitSpot);
        Advance(director, StepDt * 4f);
        ctx.Check(graph.CompletedOf(hitSpot),
            $"a spot check inside {SpotRadiusM:0} m of the warped aircraft completes");
        ctx.Check(graph.StateOf(FoundObjective) != ObjectiveState.Dormant,
            $"…which is what wakes OBJECTIVE{FoundObjective}, the mission's PRIMARY 1");
        ctx.Same(unresolvedBefore, graph.UnresolvedConditions,
            $"…with no condition left unanswered on either read");
        report.AppendLine($"spot: OBJECTIVE{missSpot} missed, OBJECTIVE{hitSpot} found, "
            + $"OBJECTIVE{FoundObjective} {graph.StateOf(FoundObjective)}, "
            + $"unresolved {graph.UnresolvedConditions}");
    }

    private static int WakersOf(ObjectiveScript script, int target)
    {
        int wakers = 0;
        foreach (var def in script.Objectives)
        {
            wakers += def.WakeWhenComplete.Contains(target) ? 1 : 0;
        }
        return wakers;
    }

    private static ObjectiveDef? Numbered(ObjectiveScript script, int number)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Number == number)
            {
                return def;
            }
        }
        return null;
    }

    private static CampaignMission? MissionAt(TestContext ctx, string chapter, string mission)
    {
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(mission, StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }
        return null;
    }

    private static void Advance(CampaignDirector director, float seconds)
    {
        for (float t = 0f; t < seconds; t += StepDt)
        {
            director.Step(StepDt);
        }
    }

    private static List<object?>? Fields(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks, string name)
    {
        foreach (var (block, fields) in blocks)
        {
            if (block.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return fields;
            }
        }
        return null;
    }

    private static (Vector3 Position, Vector3 Forward) PlayerPose(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks)
    {
        if (Fields(blocks, CampaignRosterPlan.PlayerBlock) is { } fields
            && AiSkills.RosterSpawnPose(fields) is { } pose)
        {
            return (pose.Position, new Basis(Vector3.Up, Mathf.DegToRad(pose.YawDeg)) * Vector3.Forward);
        }
        return (new Vector3(0f, 800f, 0f), Vector3.Forward);
    }

    // The human rig this suite flies nothing with: it exists so the leader pass has a player.
    private static FlightController HumanRig(TestContext ctx, GameZ planesGamez,
        TextureArchive textures, ProjectilePool live, Vector3 pos, Vector3 lookAt)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                ? PlaneDamage.For(stats) : null,
            PlayerIndex = FlightRoster.ShooterIdBase - 1,
            IsHumanPiloted = true,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = AimAssist.PlayerTeam,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, lookAt);
        rig.Name = "player1";
        ctx.Host.AddChild(rig);
        return rig;
    }

    // The session's own AI spawner with no world effects, the shape CampaignSquadWakeSuites uses.
    private static FlightRoster Spawner(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        ProjectilePool live)
    {
        var spec = SessionSpec.Parse(Array.Empty<string>());
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            null!, ctx.Host, resources,
            new FlightWorldBindings { Projectiles = live, Gamez = planesGamez },
            new HumanRosterBindings());
    }
}
