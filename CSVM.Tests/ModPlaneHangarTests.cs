using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A mod airframe in the hangar. The cases cover the default loadout and capacity fields, the
/// economy over the mod's row, and the store's mod id with the missing-mod fallback.
/// The last one has the builder offer the mod after the stock rows. Mods are built in a temp data
/// root and handed in explicitly, so no test reads or sets <see cref="ModPlanes.Current"/>.
/// </summary>
public class ModPlaneHangarTests
{
    private const int Brigand = 4;

    // The Avenger's default, Decision 30: 2 x .60 on W1 and 6 x .30 on W2-W4, with the turret cleared.
    private const string AvengerJson = """
        {
          "name": "E-1C Avenger",
          "kind_of": "player_brigand",
          "hangar_capacity": 12500,
          "loadout": {
            "_note": "W1 .60 pair, W2-W4 .30 pairs",
            "guns": [ { "calibre": 60 }, { "calibre": 30 }, { "calibre": 30, "twin": true }, { "calibre": 30 } ]
          },
          "overrides": { "turrets": [] }
        }
        """;

    private static readonly StockLoadouts Stock =
        StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));

    [Fact]
    public void TheLoadoutAndCapacityFieldsParse()
    {
        var mods = Mods(("avenger", AvengerJson),
            ("singles", """{ "kind_of": "player_fury", "loadout": { "guns": [null, { "calibre": 70, "twin": false }, null, null], "hardpoints_per_wing": 0 } }"""),
            ("plain", """{ "kind_of": "player_fury" }"""));

        var avenger = mods.Find("avenger")!;
        Assert.Equal(new[] { new GunChoice(3, true), new GunChoice(0, true), new GunChoice(0, true), new GunChoice(0, true) }, avenger.DefaultGuns);
        Assert.Null(avenger.DefaultHardpointsPerWing);
        Assert.Equal(12500, avenger.HangarCapacity);

        var singles = mods.Find("singles")!;
        Assert.Equal(new[] { default, new GunChoice(4, false), default, default(GunChoice) }, singles.DefaultGuns);
        Assert.Equal(0, singles.DefaultHardpointsPerWing);
        Assert.Null(singles.HangarCapacity);

        var plain = mods.Find("plain")!;
        Assert.Null(plain.DefaultGuns);
        Assert.Null(plain.DefaultHardpointsPerWing);
        Assert.Null(plain.HangarCapacity);
    }

    [Theory]
    [InlineData("\"hangar_capacity\": 0", "hangar_capacity is not a whole number of pounds")]
    [InlineData("\"hangar_capacity\": 12500.5", "hangar_capacity is not a whole number of pounds")]
    [InlineData("\"hangar_capacity\": \"12500\"", "hangar_capacity is not a whole number of pounds")]
    [InlineData("\"hangar_capacity\": 100001", "hangar_capacity is not a whole number of pounds")]
    [InlineData("\"loadout\": []", "loadout is not an object")]
    [InlineData("\"loadout\": { \"rockets\": 2 }", "unknown key loadout.rockets")]
    [InlineData("\"loadout\": { \"guns\": [null, null, null] }", "loadout.guns is not an array of 4 slots")]
    [InlineData("\"loadout\": { \"guns\": [5, null, null, null] }", "slot 1 is neither null nor an object")]
    [InlineData("\"loadout\": { \"guns\": [null, {}, null, null] }", "slot 2: calibre is missing")]
    [InlineData("\"loadout\": { \"guns\": [null, null, { \"calibre\": 35 }, null] }", "slot 3: calibre is not one of")]
    [InlineData("\"loadout\": { \"guns\": [null, null, { \"calibre\": 80 }, null] }", "slot 3: calibre is not one of")]
    [InlineData("\"loadout\": { \"guns\": [null, null, { \"calibre\": 20 }, null] }", "slot 3: calibre is not one of")]
    [InlineData("\"loadout\": { \"guns\": [null, null, null, { \"calibre\": 30, \"twin\": \"yes\" }] }", "slot 4: twin is not true or false")]
    [InlineData("\"loadout\": { \"guns\": [null, null, null, { \"calibre\": 30, \"ammo\": \"ap\" }] }", "slot 4: unknown key ammo")]
    [InlineData("\"loadout\": { \"hardpoints_per_wing\": 5 }", "loadout.hardpoints_per_wing is not a whole number from 0 to 4")]
    [InlineData("\"loadout\": { \"hardpoints_per_wing\": -1 }", "loadout.hardpoints_per_wing is not a whole number from 0 to 4")]
    public void AnOutOfRangeLoadoutOrCapacitySkipsTheMod(string field, string reason)
    {
        var mods = Mods(("broken", $$"""{ "kind_of": "player_brigand", {{field}} }"""));

        Assert.Empty(mods.Planes);
        Assert.Contains(reason, Assert.Single(mods.Skipped).Reason);
    }

    // W3 is empty on the stock Brigand, so the Avenger's .30 pair there binds firepoint3/4 by the
    // slot rule. The converted turret slot fires forward from firepoint1/2.
    [Fact]
    public void TheAvengerDefaultIsSixThirtiesAndTwoSixtiesForward()
    {
        var mods = Mods(("avenger", AvengerJson),
            ("pylons", """{ "kind_of": "player_brigand", "loadout": { "hardpoints_per_wing": 4 } }"""));
        var brigand = Stock.ForModel("player_brigand")!;

        var fit = mods.Find("avenger")!.StockLoadout(Stock)!;

        Assert.Equal(new[] { 1, 2, 3, 4 }, fit.Guns.Select(g => g.Slot));
        Assert.All(fit.Guns, g => Assert.False(g.Turret));
        Assert.Equal(2, fit.Guns.Where(g => g.Caliber == 60).Sum(g => g.Markers.Count));
        Assert.Equal(6, fit.Guns.Where(g => g.Caliber == 30).Sum(g => g.Markers.Count));
        Assert.Equal(new[] { "firepoint3", "firepoint4" }, fit.Guns.Single(g => g.Slot == 3).Markers);
        Assert.Equal(new[] { "firepoint1", "firepoint2" }, fit.Guns.Single(g => g.Slot == 4).Markers);
        Assert.Same(brigand.Hardpoints, fit.Hardpoints);

        var pylons = mods.Find("pylons")!.StockLoadout(Stock)!;
        Assert.Same(brigand.Guns, pylons.Guns);
        Assert.Equal(8, pylons.Hardpoints!.Stock.Count(s => s != LoadoutChoice.None));
        Assert.Equal((4, 4), HangarFeature.StockWingCounts(pylons.Hardpoints));
    }

    // 3885 airframe + 2000 engine + 1160 (.60 pair) + 3 x 560 (.30 pairs) + 4 x 480 hardpoints = 10645 lb.
    // On the parent's row W4 is a turret (+480 lb) and the Brigand's 11015 lb is exceeded.
    [Fact]
    public void TheModsRowPricesTheTurretSlotAsAWingGunAndJudgesWeightOnItsCapacity()
    {
        var mods = Mods(("avenger", AvengerJson), ("light", """{ "kind_of": "player_brigand", "hangar_capacity": 10000, "overrides": { "turrets": [] } }"""));
        var build = new CustomPlaneDef { Airframe = Brigand, ModAirframe = "avenger", Engine = 1, LeftHardpoints = 2, RightHardpoints = 2 };
        build.Guns[0] = new GunChoice(3, true);
        for (int slot = 1; slot < CustomPlaneDef.GunSlots; slot++)
        {
            build.Guns[slot] = new GunChoice(0, true);
        }

        var onMod = HangarEconomy.Price(build, mods.Find("avenger"));
        var onParent = HangarEconomy.Price(build, null);

        Assert.Equal(10645, onMod.Total.Weight);
        Assert.Equal(12500, onMod.Capacity);
        Assert.Equal(PurchaseVerdict.Ok, onMod.Verdict);
        Assert.Equal(new CostWeight(480, 560), onMod.Guns[3]);
        Assert.Equal(11125, onParent.Total.Weight);
        Assert.Equal(11015, onParent.Capacity);
        Assert.Equal(PurchaseVerdict.Overweight, onParent.Verdict);
        Assert.Equal(PurchaseVerdict.Overweight, HangarEconomy.Price(build, mods.Find("light")).Verdict);
        Assert.Equal(0, HangarEconomy.StatsFor(Brigand, mods.Find("avenger")).Turrets);
        Assert.Equal(HangarEconomy.Airframes[Brigand] with { Capacity = 12500, TurretMask = 0 }, HangarEconomy.StatsFor(Brigand, mods.Find("avenger")));
    }

    [Fact]
    public void TheStoreKeepsAModIdBesideItsParentAndAStockRecordAsBefore()
    {
        var stock = new CustomPlaneDef { Name = "Stocky", Airframe = Brigand, Engine = 1 };
        var mod = new CustomPlaneDef { Name = "Blue", Airframe = Brigand, ModAirframe = "avenger", Engine = 1 };

        string stockJson = CustomPlaneStore.Serialize(stock);
        string modJson = CustomPlaneStore.Serialize(mod);

        Assert.DoesNotContain("modAirframe", stockJson);
        Assert.Equal(stockJson, CustomPlaneStore.Serialize(CustomPlaneStore.Deserialize(stockJson)!));
        Assert.Null(CustomPlaneStore.Deserialize(stockJson)!.ModAirframe);
        Assert.Contains("\"airframe\": 4,", modJson);
        Assert.Contains("\"modAirframe\": \"avenger\",", modJson);
        var read = CustomPlaneStore.Deserialize(modJson)!;
        Assert.Equal(("avenger", Brigand), (read.ModAirframe, read.Airframe));
        Assert.Equal(modJson, CustomPlaneStore.Serialize(read));
    }

    [Fact]
    public void AMissingModFliesAsTheStoredParentWithOneWarningAndKeepsItsId()
    {
        string id = "gone_" + Guid.NewGuid().ToString("N");
        var def = new CustomPlaneDef { Name = "Orphan", Airframe = Brigand, ModAirframe = id, Engine = 1 };
        var none = Mods();
        var lines = new List<string>();

        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            Assert.Null(def.ModIn(none));
            Assert.Equal("player_brigand", def.FlownNode(none));
            Assert.Equal("player_brigand", PlanePickerRoster.Build(Array.Empty<(string, string)>(), new[] { def }, none).Single().Node);
        }

        Assert.Single(lines, l => l.Contains($"mod airframe {id} is not installed, it flies as player_brigand"));
        Assert.Equal(HangarEconomy.Airframes[Brigand].Capacity, HangarEconomy.Price(def, def.ModIn(none)).Capacity);

        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(def);
        Assert.Equal(id, store.Load("Orphan")!.ModAirframe);
    }

    [Fact]
    public void TheBuilderOffersTheModAfterTheStockRowsAndBuildsOnItsDefaults()
    {
        var mods = Mods(("avenger", AvengerJson));
        var store = new CustomPlaneStore(TestData.TempDir());
        var hangar = new HangarFeature(UiStrings.Empty, StockAirframes.Node, () => Stock) { Mods = mods };
        hangar.Open(store);
        int row = HangarEconomy.Airframes.Length;

        Assert.Equal(row + 1, hangar.AirframeRows);
        Assert.Equal("E-1C Avenger", hangar.AirframeName(row));
        Assert.Equal(Brigand, hangar.StockIdOfRow(row));

        hangar.StartDefaultPlane(row);
        Assert.Equal((Brigand, "avenger"), (hangar.Scratch.Airframe, hangar.Scratch.ModAirframe));
        Assert.Equal(row, hangar.AirframeRowOf(hangar.Scratch));
        Assert.Equal(new[] { new GunChoice(3, true), new GunChoice(0, true), new GunChoice(0, true), new GunChoice(0, true) }, hangar.Scratch.Guns);
        Assert.Equal((2, 2), (hangar.Scratch.LeftHardpoints, hangar.Scratch.RightHardpoints));
        Assert.Equal(12500, hangar.Bill.Capacity);
        Assert.Equal(12500, hangar.BillWithAirframe(row).Capacity);
        Assert.Equal(11015, hangar.BillWithAirframe(Brigand).Capacity);

        hangar.Scratch.Name = "Blue";
        Assert.True(hangar.Commit(), hangar.Message);
        var saved = store.Load("Blue")!;
        Assert.Equal("avenger", saved.ModAirframe);
        Assert.Equal("avenger", PlanePickerRoster.Build(Array.Empty<(string, string)>(), new[] { saved }, mods).Single().Node);

        hangar.Open(store);
        hangar.StartFromSaved(saved);
        Assert.Equal(row, hangar.AirframeRowOf(hangar.Scratch));
        Assert.False(hangar.PickAirframe(row));
        Assert.True(hangar.PickAirframe(Brigand));
        Assert.Null(hangar.Scratch.ModAirframe);
    }

    private static ModPlanes Mods(params (string Id, string Json)[] mods)
    {
        string root = TestData.TempDir();
        foreach (var (id, json) in mods)
        {
            string folder = Path.Combine(ModPlanes.FolderIn(root), id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile), json);
            File.WriteAllText(Path.Combine(folder, ModPlanes.ModelFile), "glb");
        }

        return ModPlanes.Open(root);
    }
}
