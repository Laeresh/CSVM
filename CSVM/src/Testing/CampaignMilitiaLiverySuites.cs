using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The livery a campaign mission's own enemy set flies in: each block's militia def
/// resolved through the session's own spawner, painted onto the aircraft it builds. The def also
/// carries the damage model, the weapons fit and the pilot vector, so what this watches is the one
/// seam all four ride (docs/org/paint.md).</summary>
internal static class CampaignMilitiaLiverySuites
{
    // CM01's enemy set is three Medusa Kestrels, CM02's is the British bombers and their escort:
    // two militias, three airframes, over two missions of the same chapter.
    private const string Chapter = "C3";
    private const string MedusaMission = "M01";
    private const string BritishMission = "M05";

    [Suite("campaign-militia-livery",
        "the campaign enemy set's livery over CM01 (C3/M01) and CM02 (C3/M05), spawned through "
        + "the session's own roster path: every enemy block resolves the militia def its name "
        + "carries, its aircraft is painted in that def's authored pattern with a painter that "
        + "finds skins for the airframe, the aircraft is still built under the shipped-skins "
        + "reading (so the def's livery outranks it rather than replacing the fork), the "
        + "player's own side keeps the default pattern, and a spawn naming no def at all under "
        + "that same reading stays unpainted")]
    internal static void CampaignMilitiaLivery(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string rof = Path.Combine(ctx.DataRoot, "extracted", "rof");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(rof, $"extracted UI archive (paint patterns)");

        var spec = SessionSpec.Parse(Array.Empty<string>());
        var liveries = new LiveryResolver(spec, rof);
        if (liveries.Patterns.IsEmpty)
        {
            throw new SuiteSkippedException("the extracted UI archive ships no paint patterns");
        }

        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        var nets = AiNets.Load(chapterZrdr);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        var built = new List<FlightController>();
        var report = new StringBuilder();
        ctx.Host.AddChild(pool);
        try
        {
            var roster = Spawner(ctx, spec, liveries, planesGamez, textures, pool);
            foreach (string mission in new[] { MedusaMission, BritishMission })
            {
                string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, mission);
                ctx.RequireData(missionZrdr, $"{Chapter}/{mission} zrdr");
                var plan = CampaignRosterPlan.Build(AiSkills.LoadRoster(missionZrdr), defs, nets);
                report.AppendLine($"-- {Chapter}/{mission}: {plan.Spawns.Count} planned block(s) --");
                CheckMission(ctx, roster, liveries, plan, built, report);
            }

            CheckNoDefControl(ctx, roster, built, report);
            roster.ClearMembership();
        }
        finally
        {
            foreach (var rig in built)
            {
                rig.Free();
            }
            pool.Free();
            textures.Dispose();
        }

        ctx.WriteArtifact($"test-campaign-militia-livery-{Chapter}.txt", report.ToString());
        ctx.Note($"spawned {built.Count} aircraft off {Chapter}/{MedusaMission} and {Chapter}/{BritishMission} and read each one's livery");
    }

    // Every aircraft block of one mission, spawned far enough apart that nothing interacts, with
    // its livery read back off the built controller rather than off the plan.
    private static void CheckMission(TestContext ctx, FlightRoster roster, LiveryResolver liveries,
        CampaignRosterPlan plan, List<FlightController> built, StringBuilder report)
    {
        int enemies = 0, painted = 0, friends = 0;
        foreach (var block in plan.Spawns)
        {
            if (block.Surface)
            {
                continue;
            }

            var at = new Vector3(built.Count * 400f, 900f, 0f);
            var rig = roster.SpawnAi(CampaignRosterPlan.SpawnFor(
                block, at, at + Vector3.Forward, AiPilot.HoldingCourse(at, at + Vector3.Forward)));
            built.Add(rig);
            var authored = block.AiDef is { } def ? liveries.DefScheme(ctx.ZrdrPath, def) : null;
            report.AppendLine($"  {block.Name}: team {block.Team} def '{block.AiDef ?? "-"}' "
                + $"-> {rig.Scheme?.Pattern ?? "(unpainted)"} shipped-skins={rig.ShippedSkins} "
                + $"painter={(rig.Painter == null ? "none" : rig.Painter.PatternMissesAircraft ? "decals only" : "skins")}");

            if (block.Team == AimAssist.PlayerTeam)
            {
                friends++;
                ctx.Check(!rig.ShippedSkins && rig.Scheme != null,
                    $"{block.Name} flies on the player's team and draws a livery ('{rig.Scheme?.Pattern ?? "-"}')");
                continue;
            }

            enemies++;
            // The fork is still crossed: the block is built under the shipped-skins reading, and
            // the def's own livery outranks it. Without both halves the arm below proves nothing.
            ctx.Check(rig.ShippedSkins,
                $"{block.Name} is built under the shipped-skins reading, as every non-player team is");
            if (authored == null)
            {
                ctx.Check(rig.Scheme == null,
                    $"…and '{block.AiDef ?? "-"}' authors no paint_pattern, so it keeps the shipped skins");
                continue;
            }

            painted++;
            ctx.Check(rig.Scheme != null && rig.Scheme.Pattern == authored.Pattern,
                $"…and wears its militia def's authored '{authored.Pattern}' (built '{rig.Scheme?.Pattern ?? "-"}')");
            ctx.Check(rig.Scheme != null && rig.Scheme.Color1 == authored.Color1
                      && rig.Scheme.NoseDecal == authored.NoseDecal,
                $"…with that def's own first colour and nose decal, not another militia's");
            ctx.Check(rig.Painter is { PatternMissesAircraft: false },
                $"…and the pattern ships skins for '{block.PlaneNode}', so the paint reaches the model");
        }

        ctx.Check(enemies > 0 && painted == enemies,
            $"every one of the mission's {enemies} enemy aeroplane(s) is painted by its own def ({painted})");
        ctx.Check(friends > 0, $"and the mission fields {friends} aircraft on the player's own side");
    }

    // The control the arms above need to be seen able to fail: the same reading on a spawn that
    // names no militia def keeps today's answer, the bare shipped skins. That is the CLI airframe
    // an enemy generator with no vehicle.params label launches.
    private static void CheckNoDefControl(TestContext ctx, FlightRoster roster,
        List<FlightController> built, StringBuilder report)
    {
        var at = new Vector3(built.Count * 400f, 900f, 0f);
        var bare = roster.SpawnAi(new AiSpawn(ctx.PlaneName, at, at + Vector3.Forward,
            AiPilot.HoldingCourse(at, at + Vector3.Forward),
            Team: InstantActionRuntime.EnemyTeam, ShippedSkins: true));
        built.Add(bare);
        report.AppendLine($"  (control) {ctx.PlaneName} with no militia def -> {bare.Scheme?.Pattern ?? "(unpainted)"}");
        ctx.Check(bare.Scheme == null && bare.Painter == null,
            $"a spawn naming no militia def under the same reading stays unpainted ('{bare.Scheme?.Pattern ?? "-"}')");
    }

    private static FlightRoster Spawner(TestContext ctx, SessionSpec spec, LiveryResolver liveries,
        GameZ planesGamez, TextureArchive textures, ProjectilePool pool) =>
        new(FlightRosterPolicy.From(spec), liveries, null, ctx.Host,
            new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            },
            new FlightWorldBindings { Projectiles = pool, Gamez = planesGamez },
            new HumanRosterBindings());
}
