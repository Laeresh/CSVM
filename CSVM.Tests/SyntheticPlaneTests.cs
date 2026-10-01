using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
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
        Assert.Equal(2, weapons.All.Count);
        var gun = Assert.Single(weapons.All, w => w.IsGun);
        var rocket = Assert.Single(weapons.All, w => w.IsRocket);
        Assert.Equal("Probe Gun", gun.DisplayName);
        Assert.Equal("Probe Rocket", rocket.DisplayName);
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

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root);
        return root;
    }
}
