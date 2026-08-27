using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The mission spawner's roster read: an enemy generator's <c>vehicle.params</c> label
/// resolves to the mission's own disabled roster block, and the aircraft it launches carries that
/// block's fields rather than a CLI airframe's defaults.</summary>
internal static class GeneratorRosterSuites
{
    // The one shipped mission whose generator template authors the nitro slot: C5/M04's dantezep
    // launches parameter 'Miles', the disabled block stihellhound_5_7, which authors slot 34.
    private const string Chapter = "C5";
    private const string Mission = "M04";
    private const string NitroBlock = "stihellhound_5_7";

    internal static void GeneratorRosterParams(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(texturesPath, $"C1 textures");

        var nets = AiNets.Load(chapterZrdr);
        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        var templates = CampaignRosterPlan.GeneratorTemplates(missionZrdr, defs, nets);
        string? parameter = null;
        foreach (var def in EnemyGenerators.Load(missionZrdr))
        {
            parameter ??= def.VehicleParams;
        }
        ctx.Check(parameter != null, $"{Chapter}/{Mission}'s generator names a vehicle.params block");
        if (parameter == null || !templates.TryGetValue(parameter, out var plan))
        {
            throw new SuiteSkippedException(
                $"{Chapter}/{Mission} carries no generator template for '{parameter}'");
        }

        ctx.Check(plan.Name == NitroBlock,
            $"the generator's '{parameter}' resolves to the roster block {NitroBlock}");
        ctx.Check(plan.Nitro, $"the block's slot 34 plans an installed nitro injector");
        var skills = AiSkills.Load(ctx.ZrdrPath);

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        FlightController? launched = null;
        FlightController? fallback = null;
        ctx.Host.AddChild(pool);
        try
        {
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var resources = new AircraftAssemblyResources
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
            };
            var roster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                null, ctx.Host, resources,
                new FlightWorldBindings
                {
                    Projectiles = pool,
                    Gamez = planesGamez,
                    ChapterZrdrPath = chapterZrdr,
                },
                new HumanRosterBindings());

            var drop = new Vector3(0f, 900f, 0f);
            var pilot = AiPilot.HoldingCourse(drop, drop + Vector3.Forward);
            launched = roster.SpawnAi(
                CampaignRosterPlan.SpawnFor(plan, drop, drop + Vector3.Forward, pilot));
            CampaignRosterPlan.ApplyPlan(pilot, plan, skills.MinAiActiveDist);

            ctx.Check(launched.Nitro.Installed,
                $"the generated aircraft carries the block's nitro injector");
            ctx.Check(launched.Name == NitroBlock,
                $"the generated aircraft wears the block's own name");
            ctx.Check(pilot.Machine == null
                      || pilot.Machine.ActivationRange >= skills.MinAiActiveDist,
                $"the plan's volumes reach the machine under the min_ai_active_dist floor");

            // The CLI fallback the spawner takes when no parameter resolves, so the arm above is
            // seen able to fail: nothing outside a roster block installs an injector.
            var spare = new Vector3(0f, 900f, -300f);
            fallback = roster.SpawnAi(new AiSpawn(ctx.PlaneName, spare, spare + Vector3.Forward,
                AiPilot.HoldingCourse(spare, spare + Vector3.Forward), ShippedSkins: true));
            ctx.Check(!fallback.Nitro.Installed,
                $"a spawn with no roster block installs no injector");

            roster.ClearMembership();
        }
        finally
        {
            launched?.Free();
            fallback?.Free();
            pool.Free();
            textures.Dispose();
        }
    }
}
