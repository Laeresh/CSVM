using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A mod plane in the campaign. The profile keeps its id beside the parent's stock id, and a stock
/// profile's file is unchanged. The launch flies the mod while it is installed and the parent while
/// it is not. The hangar offers it by plane.json's <c>unlock</c>. Mods are built in a temp data root
/// and handed in explicitly, so no test reads or sets <see cref="ModPlanes.Current"/>.
/// </summary>
public class ModPlaneCampaignTests
{
    private const string Pilot = "Zachary";
    private const int Brigand = 4;

    // Four missions over two world folders: c1 (campaign 1) then c1b (campaign 2).
    private static readonly CampaignMission[] Missions =
    {
        new(0, "a", 1, 1, "HAWAII", false),
        new(1, "b", 1, 2, "HAWAII", false),
        new(2, "c", 2, 1, "HAWAII", false),
        new(3, "d", 2, 2, "HAWAII", false),
    };

    [Fact]
    public void AStockProfileWritesNoModFieldAndAModPlaneRoundTripsItsId()
    {
        var stock = CampaignProfileDef.NewProfile(Pilot);
        string stockJson = CampaignProfileStore.Serialize(stock);
        Assert.DoesNotContain("modAirframe", stockJson);
        Assert.Equal(stockJson, CampaignProfileStore.Serialize(CampaignProfileStore.Deserialize(stockJson)!));
        Assert.All(CampaignProfileStore.Deserialize(stockJson)!.Planes, p => Assert.Null(p.ModAirframe));

        var modded = CampaignProfileDef.NewProfile(Pilot);
        modded.Planes.Add(new OwnedPlane { Name = "Blue", Airframe = Brigand, ModAirframe = "avenger" });
        string modJson = CampaignProfileStore.Serialize(modded);
        Assert.Contains("\"airframe\": 4,", modJson);
        Assert.Contains("\"modAirframe\": \"avenger\",", modJson);
        var read = CampaignProfileStore.Deserialize(modJson)!;
        Assert.Equal(("avenger", Brigand), (read.Planes[2].ModAirframe, read.Planes[2].Airframe));
        Assert.Equal(modJson, CampaignProfileStore.Serialize(read));
    }

    [Fact]
    public void AnInstalledModFliesAsItsOwnNodeExceptOnANetworkedLaunch()
    {
        var mods = Mods(("avenger", """{ "name": "E-1C Avenger", "kind_of": "player_brigand" }"""));
        var (feature, _) = Seated(mods, "avenger");

        Assert.Equal("avenger", feature.Profile!.Planes[2].FlownNode(mods));
        Assert.Equal("avenger", feature.BuildExit(new IReadOnlyList<int>[] { new[] { 0 } })!.Seats[0].PlaneNode);
        Assert.Equal("node4", feature.BuildExit(new IReadOnlyList<int>[] { new[] { 0 } }, networked: true)!.Seats[0].PlaneNode);
    }

    [Fact]
    public void AMissingModFliesAsTheStoredParentWithOneWarningAndTheFileKeepsItsId()
    {
        string id = "gone_" + Guid.NewGuid().ToString("N");
        var (feature, store) = Seated(Mods(), id);
        string before = File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json"));
        var lines = new List<string>();

        CampaignMissionExit? exit;
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            exit = feature.BuildExit(new IReadOnlyList<int>[] { new[] { 0 } });
            Assert.Equal("player_brigand", feature.Profile!.Planes[2].FlownNode(feature.ModSet));
        }

        Assert.Equal("node4", exit!.Seats[0].PlaneNode);
        Assert.Single(lines, l => l.Contains($"campaign plane Blue: mod airframe {id} is not installed, it flies as player_brigand"));
        Assert.Equal(before, File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")));
        Assert.Equal(id, store.Load(Pilot)!.Planes[2].ModAirframe);
    }

    [Fact]
    public void APurchaseRecordsTheModIdAndAStockRebuildClearsIt()
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        var profile = CampaignProfileDef.NewProfile(Pilot);
        store.Save(profile);
        var wallet = new CampaignWallet(store, profile, new CustomPlaneStore(TestData.TempDir()));

        wallet.Purchase("Blue", Brigand, 0, "avenger");
        Assert.Equal(("avenger", Brigand), (store.Load(Pilot)!.Planes[2].ModAirframe, store.Load(Pilot)!.Planes[2].Airframe));

        wallet.Purchase("Blue", 7, 0);
        Assert.Equal((null, 7), (store.Load(Pilot)!.Planes[2].ModAirframe, store.Load(Pilot)!.Planes[2].Airframe));
    }

    [Fact]
    public void TheUnlockFieldSetsWhenTheHangarOffersTheMod()
    {
        var mods = Mods(
            ("first", """{ "kind_of": "player_brigand", "unlock": "start" }"""),
            ("parent", """{ "kind_of": "player_brigand" }"""),
            ("mission", """{ "kind_of": "player_brigand", "unlock": "C1B/m02" }"""),
            ("chapter", """{ "kind_of": "player_brigand", "unlock": "c1b" }"""),
            ("unknown", """{ "kind_of": "player_brigand", "unlock": "c9/m01" }"""));
        int parent = HangarEconomy.Airframes[Brigand].Availability;
        Assert.Equal(parent, mods.Find("mission")!.CampaignAvailability);

        var lines = new List<string>();
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            foreach (var mod in mods.Planes)
            {
                mod.BindCampaign(Missions);
            }
        }

        Assert.Equal(1, mods.Find("first")!.CampaignAvailability);
        Assert.Equal(parent, mods.Find("parent")!.CampaignAvailability);
        Assert.Equal(4, mods.Find("mission")!.CampaignAvailability);
        Assert.Equal(3, mods.Find("chapter")!.CampaignAvailability);
        Assert.Equal(parent, mods.Find("unknown")!.CampaignAvailability);
        Assert.Single(lines, l => l.Contains("mod plane unknown: unlock c9/m01 names no campaign mission or chapter, it unlocks with its parent player_brigand"));

        var profile = CampaignProfileDef.NewProfile(Pilot);
        var wallet = new CampaignWallet(new CampaignProfileStore(TestData.TempDir()), profile, new CustomPlaneStore(TestData.TempDir()));
        Assert.True(wallet.IsAirframeAvailable(Brigand, mods.Find("first")));
        Assert.False(wallet.IsAirframeAvailable(Brigand, mods.Find("mission")));
        profile.MissionsCompleted = 2;
        Assert.False(wallet.IsAirframeAvailable(Brigand, mods.Find("mission")));
        Assert.True(wallet.IsAirframeAvailable(Brigand, mods.Find("chapter")));
        profile.MissionsCompleted = 3;
        Assert.True(wallet.IsAirframeAvailable(Brigand, mods.Find("mission")));
        Assert.Equal(wallet.IsAirframeAvailable(Brigand), wallet.IsAirframeAvailable(Brigand, mods.Find("parent")));
    }

    // A seated profile owning a third plane, "Blue", on the Brigand with mod id `mod` beside it, selected.
    private static (CampaignFeature Feature, CampaignProfileStore Store) Seated(ModPlanes mods, string mod)
    {
        var store = new CampaignProfileStore(Path.Combine(TestData.TempDir(), "Profiles"));
        var feature = new CampaignFeature(UiStrings.Empty, airframe => $"node{airframe}") { Mods = mods };
        feature.Open(store, null);
        feature.ContinuePlayer(Pilot);
        feature.Profile!.Planes.Add(new OwnedPlane { Name = "Blue", Airframe = Brigand, ModAirframe = mod });
        feature.Profile.SelectedPlane = 2;
        store.Save(feature.Profile);
        return (feature, store);
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
