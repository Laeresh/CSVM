using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Menu.Original;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The stand-in aircraft the synthetic tree carries, read through the loaders a flight uses. That
/// covers its box-built model and marker rig, its player and AI defs, and its flight globals. It
/// also covers its one gun and one rocket, the shake sources and the stock fit that arms it. The
/// in-engine run flies it; these pin that every record resolves and the generated mesh faces out.
/// </summary>
public class SyntheticPlaneTests
{
    // The ids a custom plane's build composes: each calibre's slug gun and the pylons' stock ordnance.
    private static readonly string[] CustomPlaneIds = Enumerable.Range(0, CustomPlaneDef.MaxCalibre + 1)
        .Select(c => StockLoadouts.GunWeaponId(30 + (10 * c), "slug")).Append(Loadout.StockOrdnance).ToArray();

    [Fact]
    public void TheModelCarriesTheRootTheMarkerRigAndTheCockpit()
    {
        var gamez = GameZ.Load(Path.Combine(Built(), "extracted", "planes"));

        Assert.NotNull(gamez.FindByName(SyntheticPlane.Plane));
        foreach (string name in new[] { "geometry", "healthy", "markers", "dontmove", "destroyed", "cockpit1",
                     "cockpit_camera", "exhaust1", "exhaust2", "staticprop1", "prop1", "pcdp4", "pcdp6" })
        {
            Assert.NotNull(gamez.FindByName(name));
        }

        var rig = MarkerRig.Extract(gamez, SyntheticPlane.Plane);
        Assert.NotNull(rig);
        Assert.Equal(8, rig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Firepoint));
        Assert.Equal(8, rig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Pylon));
        Assert.Single(rig.Markers, m => m.Kind == MarkerRig.MarkerKind.Target);

        // docs/formats/markers.md: odd pylons port, even starboard, |x| falling as the number rises.
        var pylons = rig.Markers.Where(m => m.Kind == MarkerRig.MarkerKind.Pylon).OrderBy(m => m.Ordinal).ToList();
        Assert.All(pylons, p => Assert.True(p.Ordinal % 2 == 1 ? p.Local.X < 0f : p.Local.X > 0f));
        Assert.True(pylons[0].Local.X < pylons[2].Local.X && pylons[1].Local.X > pylons[3].Local.X);
    }

    [Fact]
    public void EveryGeneratedBoxIsClosedAndWoundOutward()
    {
        var gamez = GameZ.Load(Path.Combine(Built(), "extracted", "planes"));

        Assert.NotEmpty(gamez.Meshes);
        foreach (var mesh in gamez.Meshes)
        {
            Assert.Equal(8, mesh.Vertices.Count);
            Assert.Equal(6, mesh.Polygons.Count);
            var center = mesh.Vertices.Aggregate(Vector3.Zero, (a, v) => a + v) / mesh.Vertices.Count;
            foreach (var poly in mesh.Polygons)
            {
                Assert.InRange(poly.MaterialIndex, 0, gamez.Materials.Count - 1);
                var corners = poly.VertexIndices.Select(i => mesh.Vertices[i]).ToList();
                var newell = Vector3.Zero;
                for (int i = 0; i < corners.Count; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % corners.Count];
                    newell += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }

                var faceCenter = corners.Aggregate(Vector3.Zero, (s, v) => s + v) / corners.Count;
                Assert.True(newell.Dot(faceCenter - center) > 0f);
            }
        }
    }

    [Fact]
    public void ThePlayerAndAiDefsResolveWithEveryFlightRecord()
    {
        string zrdr = Path.Combine(Built(), "extracted", "zrdr");

        var stats = PlaneStats.Load(zrdr, SyntheticPlane.Plane);
        Assert.Equal("pprobe", stats.DefName);
        Assert.Equal(0.85f, stats.EnginePower);
        Assert.Equal(16f, stats.Gravity);
        Assert.Equal(4, stats.DestroyableParts.Count);
        Assert.Contains(stats.DestroyableParts, p => p.InjureAnims.Any(a => a.Anim == "pdpanel4"));
        Assert.Equal(6, stats.CollisionProbes.Count);

        var ai = PlaneStats.LoadForAi(zrdr, SyntheticPlane.Plane);
        Assert.Equal("probe", ai.AiDefName);
        Assert.Equal(new[] { "wep_probe_gun", "wep_probe_rocket" }, ai.AiWeapons.Select(w => w.WeaponId));

        Assert.NotEmpty(Maneuvers.Load(zrdr));
        Assert.True(AiSkills.Load(zrdr).QuickDrawChance(5) > 0f);
    }

    [Fact]
    public void TheGunAndTheRocketResolveTheirNamesAndTheStockFitArmsThem()
    {
        string root = Built();
        string zrdr = Path.Combine(root, "extracted", "zrdr");
        var messages = Messages.Load(Path.Combine(root, "extracted", "messages.json"));

        var weapons = WeaponDefs.Load(zrdr, messages);
        var gun = weapons.Get("wep_probe_gun");
        Assert.NotNull(gun);
        Assert.True(gun.IsGun);
        var rocket = weapons.Get("wep_probe_rocket");
        Assert.NotNull(rocket);
        Assert.Equal("Probe Gun", gun.DisplayName);
        Assert.Equal("Probe Rocket", rocket.DisplayName);

        // The ordnance records beside them are invented too, and each names itself through the table.
        // Only the ids a custom plane composes carry the code's names.
        Assert.All(weapons.All, w => Assert.True(w.Id.StartsWith("wep_probe_", System.StringComparison.Ordinal)
                                                 || CustomPlaneIds.Contains(w.Id), w.Id));
        Assert.All(weapons.All, w => Assert.NotEqual(w.DescKey, w.DisplayName));
        // The fused blast rocket: a trigger distance past 8 m and a radius over twice it.
        var blast = weapons.Get("wep_probe_blastrocket");
        Assert.NotNull(blast);
        Assert.True(blast.IsRocket && blast.HighExplosive);
        Assert.Equal("Probe Blast Rocket", blast.DisplayName);
        Assert.True(blast.DetonationDistance > 8f && blast.ImpactProximity >= 2f * blast.DetonationDistance);
        Assert.All(weapons.All, w => Assert.Empty(w.UnhandledKeys));

        var shakes = ShakeDefs.Load(zrdr);
        Assert.Equal(6, shakes.All.Count);
        Assert.All(shakes.All, s => Assert.Empty(s.UnhandledKeys));

        var fit = StockLoadouts.Load(SyntheticPlane.LoadoutsUnder(TestData.Fixture())).For("pprobe");
        Assert.NotNull(fit);
        Assert.Equal(SyntheticPlane.Plane, fit.Model);
        Assert.All(fit.Guns, g => Assert.Equal(gun.Id, g.WeaponId));
        Assert.NotNull(fit.Hardpoints);
        Assert.All(fit.Hardpoints.Stock, id => Assert.Equal(rocket.Id, id));
    }

    [Fact]
    public void TheFighterIsASecondDistinctAirframeWithItsOwnRigDefsAndFit()
    {
        string root = Built();
        var gamez = GameZ.Load(Path.Combine(root, "extracted", "planes"));
        string zrdr = Path.Combine(root, "extracted", "zrdr");

        var probeRig = MarkerRig.Extract(gamez, SyntheticPlane.Plane)!;
        var fighterRig = MarkerRig.Extract(gamez, SyntheticPlane.Fighter);
        Assert.NotNull(fighterRig);
        Assert.Equal(8, fighterRig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Firepoint));
        Assert.Equal(8, fighterRig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Pylon));
        Assert.NotEqual(Marker(probeRig, "pylon1"), Marker(fighterRig, "pylon1"));
        Assert.NotEqual(Marker(probeRig, "firepoint1"), Marker(fighterRig, "firepoint1"));

        // The two airframes fly apart: distinct defs, dynamics, engine row and engine sound.
        var probe = PlaneStats.Load(zrdr, SyntheticPlane.Plane);
        var fighter = PlaneStats.Load(zrdr, SyntheticPlane.Fighter);
        Assert.Equal("pprobefighter", fighter.DefName);
        Assert.NotEqual(probe.RollTorque, fighter.RollTorque);
        Assert.NotEqual(probe.VehWeight, fighter.VehWeight);
        Assert.NotEqual(probe.EnginePower, fighter.EnginePower);
        Assert.NotEqual(probe.EngineSound, fighter.EngineSound);
        Assert.Equal(6, fighter.CollisionProbes.Count);
        Assert.Contains(fighter.DestroyableParts, p => p.InjureAnims.Any(a => a.Anim == "pdpanel4"));
        Assert.Equal("probefighter", PlaneStats.LoadForAi(zrdr, SyntheticPlane.Fighter).AiDefName);

        // Laid over the committed config, the fighter's fit replaces the shipped plane on its model.
        var stock = StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));
        Assert.NotNull(stock.ForModel(SyntheticPlane.Fighter));
        stock.Overlay(SyntheticPlane.LoadoutsUnder(TestData.Fixture()));
        var fit = stock.ForModel(SyntheticPlane.Fighter);
        Assert.NotNull(fit);
        Assert.Equal("pprobefighter", fit.Def);
        Assert.Single(stock.All.Values, d => d.Model == SyntheticPlane.Fighter);
        Assert.All(fit.Guns, g => Assert.Equal("wep_probe_gun", g.WeaponId));
        Assert.Equal(4, fit.Hardpoints!.Count);
        Assert.Equal(12, stock.All.Count);
    }

    [Fact]
    public void TheCustomPlaneAirframesAreDistinctAndArmTheIdsACustomBuildComposes()
    {
        string root = Built();
        var gamez = GameZ.Load(Path.Combine(root, "extracted", "planes"));
        string zrdr = Path.Combine(root, "extracted", "zrdr");
        var fighterRig = MarkerRig.Extract(gamez, SyntheticPlane.Fighter)!;
        var fighter = PlaneStats.Load(zrdr, SyntheticPlane.Fighter);
        var stock = StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));
        stock.Overlay(SyntheticPlane.LoadoutsUnder(TestData.Fixture()));

        foreach (var (node, def, ai) in new[] { (SyntheticPlane.Firebrand, "pprobebrand", "probebrand"),
                     (SyntheticPlane.Avenger, "pprobeavenger", "probeavenger") })
        {
            var rig = MarkerRig.Extract(gamez, node);
            Assert.NotNull(rig);
            Assert.Equal(8, rig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Firepoint));
            Assert.Equal(8, rig.Markers.Count(m => m.Kind == MarkerRig.MarkerKind.Pylon));
            Assert.NotEqual(Marker(fighterRig, "pylon1"), Marker(rig, "pylon1"));

            var stats = PlaneStats.Load(zrdr, node);
            Assert.Equal(def, stats.DefName);
            Assert.NotEqual(fighter.RollTorque, stats.RollTorque);
            Assert.NotEqual(fighter.EngineSound, stats.EngineSound);
            Assert.Equal(ai, PlaneStats.LoadForAi(zrdr, node).AiDefName);

            // The engine pick's registry rows exist, so a custom engine reads its own power.
            int airframe = StockAirframes.IdOf(node)!.Value;
            for (int tier = 0; tier < 3; tier++)
            {
                Assert.NotNull(CustomPlaneBuild.EnginePowerFor(zrdr, new CustomPlaneDef { Airframe = airframe, Engine = tier }));
            }

            var fit = stock.ForModel(node);
            Assert.Equal(def, fit?.Def);
            Assert.All(fit!.Guns, g => Assert.Equal("wep_probe_gun", g.WeaponId));
        }

        Assert.Equal(12, stock.All.Count);
        var weapons = WeaponDefs.Load(zrdr, null);
        Assert.All(CustomPlaneIds, id => Assert.NotNull(weapons.Get(id)));
        Assert.True(weapons.Get(Loadout.StockOrdnance)!.HighExplosive);
    }

    private static Vector3 Marker(MarkerRig rig, string name) => rig.Markers.Single(m => m.Name == name).Local;

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies);
        return root;
    }
}
