using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using Godot;

namespace CSVM.Testing;

/// <summary>A deactivated roster block on a player-anchored net, woken through the real objective
/// graph. The original's activation carries the vehicle's position by the net's trailer and keeps the
/// walk seated at its spawn (docs/org/aiPilot.md "Activation keeps the walk").</summary>
internal static class CampaignWakeCarrySuites
{
    private const string Chapter = "C1";
    private const string Mission = "M04";

    // Deactivated in the shipped roster, flying M4ReinfAce, whose trailer is the player.
    private const string Block = "blakebloodhawk_8";
    private const string AnchoredNet = "M4ReinfAce";

    // Where the human stands from the net's anchor node, far enough that a carry cannot hide.
    private static readonly Vector3 HumanFromAnchor = new(3000f, 0f, -2000f);

    [Suite("campaign-wake-trailer-carry",
        "C1/M04's deactivated blakebloodhawk_8 flies M4ReinfAce, anchored to the player: its walk is "
        + "seated at its spawn while it sleeps, and the objective whose WAKEUP_ENEMIES names it puts it "
        + "in the world at its spawn carried by the trailer offset (the human's X/Z less the anchor "
        + "node's, the height kept), still flying the leg it was seated on")]
    internal static void CampaignWakeTrailerCarry(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(Chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(Mission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} is not in cm_sequence");
        }

        var script = ObjectiveScript.Load(missionZrdr);
        int wakeObjective = -1;
        for (int i = 0; i < script.Objectives.Count && wakeObjective < 0; i++)
        {
            foreach (var name in script.Objectives[i].WakeupEnemies)
            {
                if (name.Equals(Block, StringComparison.OrdinalIgnoreCase))
                {
                    wakeObjective = i + 1;
                }
            }
        }
        var net = AiNets.ByName(AiNets.Load(chapterZrdr), AnchoredNet);
        if (wakeObjective < 0 || net?.Trailer is not { NodeIndex: >= 0 } trailer
            || !string.Equals(trailer.Name, NetTrailerTargets.PlayerName, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Check(false, $"{Chapter}/{Mission} wakes '{Block}' by objective ({wakeObjective}) and '{AnchoredNet}' is anchored to the player");
            return;
        }

        var report = new StringBuilder();
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            ProjectilePool? pool = null;
            FlightRoster? roster = null;
            FlightController? human = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                roster = CampaignRosterSuites.Spawner(ctx, planesGamez, textures, live);
                var anchor = net.Nodes[trailer.NodeIndex].Position;
                var standAt = anchor + HumanFromAnchor;
                var player = CampaignPlayerDeathSuites.HumanRig(ctx, planesGamez, textures, live, standAt, Vector3.Forward);
                human = player;

                director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = chapterZrdr,
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = AiSkills.Load(ctx.ZrdrPath).MinAiActiveDist,
                    Player = () => player,
                    NetTrailers = new NetTrailerTargets(
                        () => player.WorldPosition,
                        name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                    FindNodes = name => world.Runtime.FindNodes(name),
                    Spawn = (plan, pos, look, pilot) => roster!.SpawnAi(
                        CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                    Rng = new Random(1),
                });

                if (!director.Roster.TryGetValue(Block, out var rig) || rig.Pilot?.Patrol is not { } walk)
                {
                    ctx.Check(false, $"'{Block}' spawns with a patrol walk");
                    return;
                }
                ctx.Check(rig.Inert && walk.Net.Name.Equals(AnchoredNet, StringComparison.OrdinalIgnoreCase),
                    $"'{Block}' spawns deactivated on '{AnchoredNet}': inert={rig.Inert} net='{walk.Net.Name}'");
                var spawnedAt = rig.WorldPosition;
                var seat = (walk.LegStartIndex, walk.CurrentIndex);
                ctx.Check(seat.LegStartIndex >= 0 && seat.CurrentIndex >= 0,
                    $"its walk is seated at its spawn while it sleeps: {seat.LegStartIndex} -> {seat.CurrentIndex}");

                var offset = AiNetFollower.TrailerOffset(net, player.WorldPosition);
                var carried = spawnedAt + offset;
                report.AppendLine($"spawn {spawnedAt}, anchor node {trailer.NodeIndex} at {anchor}, human {player.WorldPosition}, offset {offset}");

                director.Attach(new CampaignDirector.WorldInputs
                {
                    Runtime = world.Runtime,
                    Sounds = world.Runtime.Sounds,
                    Projectiles = live,
                    ListenerPosition = () => player.WorldPosition,
                    PlayerAircraft = () => player,
                    Rng = new Random(1),
                });
                if (director.Graph is not { } graph)
                {
                    ctx.Check(false, $"the world phase armed the objective graph");
                    return;
                }
                graph.Wake(wakeObjective);
                report.AppendLine($"woken by OBJECTIVE{wakeObjective} at {rig.WorldPosition}, walk {walk.LegStartIndex} -> {walk.CurrentIndex}");

                ctx.Check(rig.InPlay, $"OBJECTIVE{wakeObjective} puts '{Block}' in the world");
                ctx.Check(rig.WorldPosition.DistanceTo(carried) < 1f,
                    $"it wakes at its spawn carried by the trailer: {rig.WorldPosition} vs {carried}");
                ctx.Check(rig.WorldPosition.DistanceTo(spawnedAt) > 1000f,
                    $"…which is well clear of the spawn itself: {rig.WorldPosition.DistanceTo(spawnedAt):0} m");
                ctx.Check(Mathf.Abs(rig.WorldPosition.Y - spawnedAt.Y) < 0.5f,
                    $"…at the height it spawned at: {rig.WorldPosition.Y:0.0} vs {spawnedAt.Y:0.0}");
                ctx.Check(ReferenceEquals(rig.Pilot?.Patrol, walk) && (walk.LegStartIndex, walk.CurrentIndex) == seat,
                    $"…flying the leg its spawn seated: {walk.LegStartIndex} -> {walk.CurrentIndex} vs {seat.LegStartIndex} -> {seat.CurrentIndex}");
            }
            finally
            {
                human?.Free();
                var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
                roster?.ClearMembership();
                foreach (var r in members)
                {
                    r.Free();
                }
                pool?.Free();
                textures.Dispose();
            }
        });
        ctx.WriteArtifact($"test-campaign-wake-trailer-carry-{Chapter}-{Mission}.txt", report.ToString());
    }
}
