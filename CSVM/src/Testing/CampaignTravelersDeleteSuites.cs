using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary><c>TRAVELERS ... DELETE_ON_SUCCESS</c> over the shipped missions that author it: the
/// counted aircraft leaves the world as its clause completes, and never while it is dormant. The
/// three forms in use are CM21's literal point, C5/M02's <c>player</c> reference and C2/M05's AI
/// group. The directors run on their missions' own scripts and aiv rosters with no chapter world.
/// Each clause reads positions alone, so the suites place the aircraft rather than fly them.
/// </summary>
internal static class CampaignTravelersDeleteSuites
{
    private const float StepDt = 1f / 60f;

    // The graph completes at most one objective per tick from a rotating scan. Each leg runs well
    // past every armed objective of the mission.
    private const float ScanWindow = 5f;

    // CM21: OBJECTIVE20 is TRAVELERS autogyro_1 APPROACHING [-344, 250, -4347.5] 1000
    // DELETE_ON_SUCCESS, awake from the start. OBJECTIVE28 wakes the Cabbie and completes at once,
    // waking OBJECTIVE56, DEDG [2, 0] over the Cabbie's own group.
    private const string CabbieChapter = "C5";
    private const string CabbieMission = "M01";
    private const string Cabbie = "autogyro_1";
    private const int DeliveredObjective = 20;
    private const int WakeObjective = 28;
    private const int GroupGoneObjective = 56;

    // C5/M02: OBJECTIVE29 is TRAVELERS bhatbrigand_5_1 LEAVING player 2000 DELETE_ON_SUCCESS.
    private const string EscortChapter = "C5";
    private const string EscortMission = "M02";
    private const string Escort = "bhatbrigand_5_1";
    private const int EscortObjective = 29;

    // C2/M05: OBJECTIVE35 is TRAVELERS 2 LEAVING cargozep2 2000 DELETE_ON_SUCCESS.
    private const string GroupChapter = "C2";
    private const string GroupMission = "M05";
    private const int GroupObjective = 35;

    // How far outside or inside a radius each leg places the subject.
    private const float Margin = 500f;

    [Suite("campaign-cabbie-delivered",
        "CM21's Cabbie (autogyro_1) under C5/M01's own OBJECTIVE20, TRAVELERS APPROACHING "
        + "[-344, 250, -4347.5] 1000 DELETE_ON_SUCCESS: parked on the point while still dormant he "
        + "completes nothing, woken 1500 m out he completes nothing, and once within 1000 m the "
        + "objective completes and he leaves the world for good, deactivated and out of his group, "
        + "so the mission's own DEDG [2, 0] completes after him")]
    internal static void CampaignCabbieDelivered(TestContext ctx)
    {
        var run = Open(ctx, CabbieChapter, CabbieMission, Cabbie);
        var clause = Clause(run.Script, DeliveredObjective, CabbieChapter, CabbieMission);
        if (clause.Who != Cabbie || clause.WherePoint is not { Length: 3 } p || !clause.Approaching
            || !clause.DeleteOnSuccess)
        {
            throw new SuiteSkippedException(
                $"{CabbieChapter}/{CabbieMission} OBJECTIVE{DeliveredObjective} no longer authors the Cabbie's delivery");
        }

        var point = new Vector3(p[0], p[1], p[2]);
        var report = new StringBuilder();
        Fly(ctx, run, (director, cabbie, player) =>
        {
            var graph = director.Graph!;
            ctx.Check(cabbie.Inert && cabbie.WorldPosition.DistanceTo(point) > clause.Radius,
                $"'{Cabbie}' ships deactivated, {cabbie.WorldPosition.DistanceTo(point):0} m from the point");

            // ABLE-TO-FAIL CONTROL: the dormant Cabbie on the point itself.
            cabbie.WarpTo(point, 0f, 0f);
            Step(director, ScanWindow);
            ctx.Check(!graph.CompletedOf(DeliveredObjective) && cabbie.Inert && director.Removed.Count == 0,
                $"parked on the point while still dormant, he completes nothing and is not removed");

            var outside = point + (Vector3.Right * (clause.Radius + Margin));
            cabbie.WarpTo(outside, 0f, 0f);
            graph.Wake(WakeObjective);
            Step(director, ScanWindow);
            ctx.Check(cabbie.InPlay && graph.CompletedOf(WakeObjective),
                $"OBJECTIVE{WakeObjective} wakes him where he stands, {cabbie.WorldPosition.DistanceTo(point):0} m out");
            ctx.Check(!graph.CompletedOf(DeliveredObjective) && !graph.CompletedOf(GroupGoneObjective),
                $"ABLE-TO-FAIL CONTROL: {clause.Radius + Margin:0} m out neither OBJECTIVE{DeliveredObjective} nor his group's DEDG completes");

            cabbie.WarpTo(point + (Vector3.Right * (clause.Radius - Margin)), 0f, 0f);
            float delivered = StepUntil(director, () => graph.CompletedOf(DeliveredObjective));
            ctx.Check(graph.CompletedOf(DeliveredObjective),
                $"within {clause.Radius:0} m OBJECTIVE{DeliveredObjective} completes ({delivered:0.00} s)");
            ctx.Check(cabbie.Deactivated && !cabbie.InPlay && director.Removed.Contains(Cabbie),
                $"and '{Cabbie}' is out of the world, deactivated rather than parked");
            float gone = StepUntil(director, () => graph.CompletedOf(GroupGoneObjective));
            ctx.Check(graph.CompletedOf(GroupGoneObjective),
                $"his group empties, so OBJECTIVE{GroupGoneObjective}'s DEDG [2, 0] completes ({gone:0.00} s later)");
            Step(director, ScanWindow);
            ctx.Check(cabbie.Deactivated && player.InPlay,
                $"he stays out while the mission runs on, and the player is untouched");
            report.AppendLine(Log.Format($"delivered after {delivered:0.00} s, group gone {gone:0.00} s later; removed: {string.Join(",", director.Removed)}"));
        });

        ctx.WriteArtifact($"test-campaign-cabbie-delivered-{CabbieChapter}-{CabbieMission}.txt", report.ToString());
    }

    [Suite("campaign-travelers-delete-forms",
        "the other two DELETE_ON_SUCCESS forms the shipped missions author, each over its own "
        + "mission's script and aiv roster: C5/M02's OBJECTIVE29 (bhatbrigand_5_1 LEAVING player "
        + "2000) holds while the human is 1500 m off and removes the Black Hat once the human is "
        + "2500 m off; C2/M05's OBJECTIVE35 (group 2 LEAVING 2000, its zeppelin reference stood in "
        + "for by a point) holds with the group inside and removes the counted member once outside")]
    internal static void CampaignTravelersDeleteForms(TestContext ctx)
    {
        var report = new StringBuilder();
        var escort = Open(ctx, EscortChapter, EscortMission, Escort);
        var leaving = Clause(escort.Script, EscortObjective, EscortChapter, EscortMission);
        if (leaving.Who != Escort || leaving.Approaching || leaving.WhereNode != "player" || !leaving.DeleteOnSuccess)
        {
            throw new SuiteSkippedException(
                $"{EscortChapter}/{EscortMission} OBJECTIVE{EscortObjective} no longer authors an escort leaving the player");
        }

        Fly(ctx, escort, (director, brigand, player) =>
        {
            var graph = director.Graph!;
            Wake(brigand);
            var at = brigand.WorldPosition;
            player.PlaceHeld(at + (Vector3.Right * (leaving.Radius - Margin)), at);
            graph.Wake(EscortObjective);
            Step(director, ScanWindow);
            ctx.Check(!graph.CompletedOf(EscortObjective) && brigand.InPlay,
                $"ABLE-TO-FAIL CONTROL: with the human {leaving.Radius - Margin:0} m off, OBJECTIVE{EscortObjective} holds and '{Escort}' flies on");

            player.PlaceHeld(at + (Vector3.Right * (leaving.Radius + Margin)), at);
            float left = StepUntil(director, () => graph.CompletedOf(EscortObjective));
            ctx.Check(graph.CompletedOf(EscortObjective) && brigand.Deactivated && director.Removed.Contains(Escort),
                $"with the human {leaving.Radius + Margin:0} m off, OBJECTIVE{EscortObjective} completes and '{Escort}' leaves the world ({left:0.00} s)");
            ctx.Check(player.InPlay, $"…and the human it was measured against stays");
            report.AppendLine(Log.Format($"{EscortChapter}/{EscortMission}: escort removed after {left:0.00} s"));
        });

        GroupForm(ctx, report);
        ctx.WriteArtifact("test-campaign-travelers-delete-forms.txt", report.ToString());
    }

    // C2/M05's group form. This run builds no world and so no zeppelin to refer to. A point beside
    // the group's member stands in for it, and the rest of the clause is as authored.
    private static void GroupForm(TestContext ctx, StringBuilder report)
    {
        var probe = Open(ctx, GroupChapter, GroupMission, null);
        var groupClause = Clause(probe.Script, GroupObjective, GroupChapter, GroupMission);
        if (groupClause.Group is not { } group || groupClause.Approaching || groupClause.WhereNode == null
            || !groupClause.DeleteOnSuccess)
        {
            throw new SuiteSkippedException(
                $"{GroupChapter}/{GroupMission} OBJECTIVE{GroupObjective} no longer authors a group leaving a node");
        }

        var run = probe with { Group = group };
        Fly(ctx, run, (director, member, player) =>
        {
            var graph = director.Graph!;
            Wake(member);
            var stand = member.WorldPosition + (Vector3.Right * (groupClause.Radius - Margin));
            groupClause.WhereNode = null;
            groupClause.WherePoint = new[] { stand.X, stand.Y, stand.Z };
            graph.Wake(GroupObjective);
            Step(director, ScanWindow);
            ctx.Check(!graph.CompletedOf(GroupObjective) && member.InPlay,
                $"ABLE-TO-FAIL CONTROL: group {group}'s member {groupClause.Radius - Margin:0} m from the reference, OBJECTIVE{GroupObjective} holds");

            member.WarpTo(stand + (Vector3.Right * (groupClause.Radius + Margin)), 0f, 0f);
            float left = StepUntil(director, () => graph.CompletedOf(GroupObjective));
            ctx.Check(graph.CompletedOf(GroupObjective) && member.Deactivated && director.Removed.Count == 1,
                $"once outside, OBJECTIVE{GroupObjective} completes and the counted member leaves the world ({left:0.00} s; removed {string.Join(",", director.Removed)})");
            ctx.Check(player.InPlay, $"…while the human, who is in no roster group, stays");
            report.AppendLine(Log.Format($"{GroupChapter}/{GroupMission}: group {group} member removed after {left:0.00} s"));
        });
    }

    // An inert roster block put in play where it stands, the way WAKEUP_ENEMIES does.
    private static void Wake(FlightController rig)
    {
        if (rig.Inert)
        {
            rig.Activate(rig.WorldPosition, rig.WorldPosition + rig.NoseDirection);
        }
    }

    private static TravelersSpec Clause(ObjectiveScript script, int number, string chapter, string mission)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Number == number && def.Travelers is { } spec)
            {
                return spec;
            }
        }

        throw new SuiteSkippedException($"{chapter}/{mission} OBJECTIVE{number} carries no TRAVELERS");
    }

    private static void Step(CampaignDirector director, float seconds)
    {
        for (float t = 0f; t < seconds; t += StepDt)
        {
            director.Step(StepDt);
        }
    }

    // Steps until the condition holds or the scan window runs out, answering the seconds taken.
    private static float StepUntil(CampaignDirector director, Func<bool> done)
    {
        float t = 0f;
        while (!done() && t < ScanWindow)
        {
            director.Step(StepDt);
            t += StepDt;
        }

        return t;
    }

    private static Mission Open(TestContext ctx, string chapter, string mission, string? subject)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, mission);
        ctx.RequireData(zrdr, $"{chapter}/{mission} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(mission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }

        if (found is not { } campaign)
        {
            throw new SuiteSkippedException($"{chapter}/{mission} is not in cm_sequence");
        }

        return new Mission(chapter, mission, campaign, ObjectiveScript.Load(zrdr), zrdr, subject, null);
    }

    // The mission's director over one human and the one roster block the leg places: the named
    // subject, or the first member of the named group. Every rig is freed on the way out.
    private static void Fly(TestContext ctx, Mission run,
        Action<CampaignDirector, FlightController, FlightController> legs)
    {
        var blocks = AiSkills.LoadRoster(run.Zrdr);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var director = CampaignDirector.Create(run.Script, run.Campaign,
            CampaignProfileDef.NewProfile("Zachary"), null, run.Zrdr);
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, run.Chapter));
        var rigs = new List<FlightController>();
        ProjectilePool? pool = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var pose = PlayerPose(blocks);
            var player = Rig(ctx, planesGamez, textures, PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName), live,
                ctx.PlaneName, pose.Position, pose.Position + pose.Forward, human: true, pilot: null,
                FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);
            player.Held = true;
            rigs.Add(player);

            FlightController? subject = null;
            director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, run.Chapter),
                MissionZrdrPath = run.Zrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = skills.MinAiActiveDist,
                Player = () => player,
                Spawn = (plan, pos, look, pilot) =>
                {
                    bool wanted = run.Subject != null
                        ? plan.Name.Equals(run.Subject, StringComparison.OrdinalIgnoreCase)
                        : plan.Group == run.Group && subject == null;
                    if (!wanted)
                    {
                        return null;
                    }

                    var stats = StatsFor(ctx, plan);
                    var rig = Rig(ctx, planesGamez, textures, stats, live, plan.PlaneNode, pos, look,
                        human: false, pilot, FlightRoster.ShooterIdBase + 1, plan.Team ?? AimAssist.PlayerTeam);
                    rig.Inert = plan.Inert;
                    rig.Held = true;
                    rigs.Add(rig);
                    subject = rig;
                    return rig;
                },
                Rng = new System.Random(1),
            });
            director.Attach(new CampaignDirector.WorldInputs
            {
                ListenerPosition = () => player.WorldPosition,
                PlayerAircraft = () => player,
                Rng = new System.Random(1),
            });

            if (subject == null || director.Graph == null)
            {
                ctx.Check(false, $"{run.Chapter}/{run.Name}'s roster spawned {run.Subject ?? $"a group {run.Group} member"}");
                return;
            }

            legs(director, subject, player);
        }
        finally
        {
            foreach (var rig in rigs)
            {
                rig.Free();
            }

            pool?.Free();
            textures.Dispose();
        }
    }

    private static (Vector3 Position, Vector3 Forward) PlayerPose(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks)
    {
        foreach (var (name, fields) in blocks)
        {
            if (name.Equals(CampaignRosterPlan.PlayerBlock, StringComparison.OrdinalIgnoreCase)
                && AiSkills.RosterSpawnPose(fields) is { } pose)
            {
                return (pose.Position, new Basis(Vector3.Up, Mathf.DegToRad(pose.YawDeg)) * Vector3.Forward);
            }
        }

        return (new Vector3(0f, 500f, 0f), Vector3.Forward);
    }

    private static PlaneStats StatsFor(TestContext ctx, RosterSpawnPlan plan)
    {
        try
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode, plan.AiDef);
        }
        catch (ArgumentException)
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode);
        }
    }

    // One aircraft on the suite's own stage, with no world to take a collision's damage.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, ProjectilePool live, string planeNode, Vector3 pos, Vector3 lookAt, bool human,
        AiPilot? pilot, int shooterId, int team)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(planeNode);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f ? PlaneDamage.For(stats) : null,
            PlayerIndex = shooterId,
            IsHumanPiloted = human,
            Pilot = pilot,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = team,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats, aiForcePath: !human), null, new CamParams(), pos, lookAt);
        rig.Name = $"{planeNode}_{shooterId}";
        ctx.Host.AddChild(rig);
        return rig;
    }

    // One mission's script and data, with the roster block a leg places: a named subject, or the
    // first member of a group.
    private sealed record Mission(string Chapter, string Name, CampaignMission Campaign, ObjectiveScript Script,
        string Zrdr, string? Subject, int? Group);
}
