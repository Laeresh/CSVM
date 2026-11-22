using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Tooling;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A mod plane's stats and stock fit: the parent's, with the mod's literal overrides and, with
/// <c>"turrets": []</c>, no turret. Mods are built in a temp data root and read through
/// <see cref="ModPlanes.Open"/>; the defs are the synthetic tree's, so no extraction is needed.
/// </summary>
public class ModPlaneStatsTests
{
    // The synthetic Kestrel carries a turret on both its player and its AI def, and four zones.
    private const string Parent = "player_kestrel";

    [Fact]
    public void AnOverrideLandsOnItsFieldAndEverythingElseIsTheParents()
    {
        var zrdr = SyntheticZrdr.Root();
        var mod = Mod("""{ "fd_speed": 140, "fuel": 250, "nose_armor": 50, "tail_hp": 40 }""");

        var stats = mod.Stats(zrdr);
        var parent = PlaneStats.Load(zrdr, Parent);

        Assert.Equal(140f, stats.FdSpeed);
        Assert.Equal(250f, stats.FuelCapacity);
        Assert.Equal(50f, stats.DestroyableParts.Single(p => p.Name == "nose").MaxArmor);
        Assert.Equal(40f, stats.DestroyableParts.Single(p => p.Name == "tail").MaxHp);
        Assert.NotEqual(parent.FdSpeed, stats.FdSpeed);

        var overridden = new HashSet<string> { nameof(PlaneStats.FdSpeed), nameof(PlaneStats.FuelCapacity) };
        foreach (var field in typeof(PlaneStats).GetFields().Where(f => !f.IsStatic && !overridden.Contains(f.Name)
                     && (f.FieldType.IsValueType || f.FieldType == typeof(string))))
        {
            Assert.True(Equals(field.GetValue(parent), field.GetValue(stats)), field.Name);
        }

        Assert.Equal(parent.TurretMounts.Count, stats.TurretMounts.Count);
        Assert.Equal(parent.CollisionProbes, stats.CollisionProbes);
        foreach (var (a, b) in parent.DestroyableParts.Zip(stats.DestroyableParts))
        {
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(b.Name == "tail" ? 40f : a.MaxHp, b.MaxHp);
            Assert.Equal(b.Name == "nose" ? 50f : a.MaxArmor, b.MaxArmor);
        }
    }

    [Fact]
    public void AnUnknownNameWarnsAndIsIgnored()
    {
        var zrdr = SyntheticZrdr.Root();
        var mod = Mod("""{ "fd_sped": 140, "rec_moments_inertia": 1, "wing_hp": 9, "nose_shield": 5 }""");
        var lines = new List<string>();

        PlaneStats stats;
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            stats = mod.Stats(zrdr);
        }

        foreach (string name in new[] { "fd_sped", "rec_moments_inertia", "wing_hp", "nose_shield" })
        {
            Assert.Contains(lines, l => l.Contains($"override {name} is no def field"));
        }

        var parent = PlaneStats.Load(zrdr, Parent);
        Assert.Equal(parent.FdSpeed, stats.FdSpeed);
        Assert.Equal(parent.RecInertia, stats.RecInertia);
        Assert.Equal(parent.DestroyableParts.Select(p => (p.MaxHp, p.MaxArmor)), stats.DestroyableParts.Select(p => (p.MaxHp, p.MaxArmor)));
    }

    [Fact]
    public void TurretsClearOnTheFlownAndTheBotLoadAndTheBotKeepsTheParentsAiDef()
    {
        var zrdr = SyntheticZrdr.Root();
        var mod = Mod("""{ "fd_speed": 140, "turrets": [] }""");

        Assert.NotEmpty(PlaneStats.Load(zrdr, Parent).TurretMounts);
        Assert.NotEmpty(PlaneStats.LoadForAi(zrdr, Parent).TurretMounts);
        Assert.Empty(mod.Stats(zrdr).TurretMounts);

        var bot = mod.Stats(zrdr, forAi: true);
        var parentBot = PlaneStats.LoadForAi(zrdr, Parent);
        Assert.Empty(bot.TurretMounts);
        Assert.Equal(parentBot.AiDefName, bot.AiDefName);
        Assert.Equal(parentBot.VehicleArmor, bot.VehicleArmor);
        Assert.Equal(parentBot.AiWeapons.Select(w => w.WeaponId), bot.AiWeapons.Select(w => w.WeaponId));
        Assert.Equal(140f, bot.FdSpeed);
        Assert.Equal(PlaneStats.Load(zrdr, Parent).DefName, mod.Stats(zrdr).DefName);
    }

    // The Brigand's W4 Rear Turret becomes a forward gun on firepoint1 and firepoint2; the stock
    // Brigand's own fit keeps its turret.
    [Fact]
    public void ClearedTurretsMakeTheTurretSlotAForwardGunForTheModOnly()
    {
        var stock = StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));
        var brigand = stock.ForModel("player_brigand")!;

        var fit = Mod("""{ "turrets": [] }""", "player_brigand").StockLoadout(stock)!;

        var w4 = fit.Guns.Single(g => g.Slot == 4);
        Assert.False(w4.Turret);
        Assert.Equal(new[] { "firepoint1", "firepoint2" }, w4.Markers);
        Assert.Equal(brigand.Guns.Single(g => g.Slot == 4).Caliber, w4.Caliber);
        Assert.All(fit.Guns.Where(g => g.Slot != 4), g => Assert.Contains(g, brigand.Guns));
        Assert.Same(brigand.Hardpoints, fit.Hardpoints);
        Assert.True(stock.ForModel("player_brigand")!.Guns.Single(g => g.Slot == 4).Turret);

        Assert.Same(brigand, Mod("""{ "fd_speed": 140 }""", "player_brigand").StockLoadout(stock));
    }

    private static ModPlane Mod(string overrides, string kindOf = Parent)
    {
        string root = TestData.TempDir();
        string mod = Path.Combine(ModPlanes.FolderIn(root), "testmod");
        Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(mod, "plane.json"), $$"""{ "kind_of": "{{kindOf}}", "overrides": {{overrides}} }""");
        File.WriteAllText(Path.Combine(mod, "plane.glb"), "glb");
        return Assert.Single(ModPlanes.Open(root).Planes);
    }
}
