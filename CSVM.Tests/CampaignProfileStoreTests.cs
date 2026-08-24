using System;
using System.IO;
using System.Linq;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>The persistence contract: a fresh profile starts with the traced $0 + two Devastators,
/// a profile round-trips through its JSON file with every field intact even across a simulated
/// relaunch (a second store instance over the same directory), a missing or malformed file reads
/// as nothing, and deleting a profile never touches anything outside its own directory.</summary>
public class CampaignProfileStoreTests
{
    [Fact]
    public void NewProfile_StartsWithZeroFundsAndTheTwoStarters()
    {
        var def = CampaignProfileDef.NewProfile("Zachary");

        Assert.Equal(0, def.Funds);
        Assert.Equal(0, def.MissionsCompleted);
        Assert.Equal(new[] { "Gypsy Magic", "The Knave" }, def.Planes.Select(p => p.Name).ToArray());
        Assert.All(def.Planes, p => Assert.Equal(5, p.Airframe));
    }

    /// <summary>Create, write, relaunch, read: a second store instance over the same directory
    /// (standing in for a process restart against the same <c>user://</c>) sees exactly what the
    /// first instance wrote: funds, the owned-plane fit and a recorded mission result.</summary>
    [Fact]
    public void RoundTrip_SurvivesASimulatedRelaunch()
    {
        var dir = TestData.TempDir();
        var first = new CampaignProfileStore(dir);
        var def = CampaignProfileDef.NewProfile("Zachary");
        def.Planes[0].Ammo[0] = 2;
        def.Planes[0].Ordnance[0] = 11;
        def.Funds = 900;
        def.MissionsCompleted = 1;
        def.MissionResults.Add(new MissionResult
        {
            Seq = 0,
            CompletedMask = 1,
            TimeMs = 45000,
            Shots = 120,
            Hits = 30,
            Money = 900,
            Airframe = 5,
            PlaneName = "Gypsy Magic",
        });
        first.Save(def);

        var relaunched = new CampaignProfileStore(dir);
        var loaded = relaunched.Load("Zachary");

        Assert.NotNull(loaded);
        Assert.Equal(900, loaded!.Funds);
        Assert.Equal(1, loaded.MissionsCompleted);
        Assert.Equal(2, loaded.Planes[0].Ammo[0]);
        Assert.Equal(11, loaded.Planes[0].Ordnance[0]);
        var result = Assert.Single(loaded.MissionResults);
        Assert.Equal(0, result.Seq);
        Assert.Equal(1, result.CompletedMask);
        Assert.Equal(45000, result.TimeMs);
        Assert.Equal("Gypsy Magic", result.PlaneName);
    }

    [Fact]
    public void Serialize_IsStableAcrossARoundTrip()
    {
        var first = CampaignProfileStore.Serialize(CampaignProfileDef.NewProfile("Zachary"));
        var reread = CampaignProfileStore.Deserialize(first);

        Assert.NotNull(reread);
        Assert.Equal(first, CampaignProfileStore.Serialize(reread!));
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        Assert.Null(store.Load("Nobody"));
    }

    [Fact]
    public void Load_MalformedFile_ReturnsNull()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        var path = store.Save(CampaignProfileDef.NewProfile("Zachary"));
        File.WriteAllText(path, "{ not json at all");

        Assert.Null(store.Load("Zachary"));
    }

    [Fact]
    public void Load_WrongVersion_ReturnsNull()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        var path = store.Save(CampaignProfileDef.NewProfile("Zachary"));
        File.WriteAllText(path, CampaignProfileStore.Serialize(CampaignProfileDef.NewProfile("Zachary"))
            .Replace("\"version\": 1", "\"version\": 99"));

        Assert.Null(store.Load("Zachary"));
    }

    [Fact]
    public void List_SortsByName_AndSkipsTheUnreadable()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        store.Save(CampaignProfileDef.NewProfile("Zephyr"));
        store.Save(CampaignProfileDef.NewProfile("aardvark"));
        Directory.CreateDirectory(store.DirFor("Broken"));
        File.WriteAllText(Path.Combine(store.DirFor("Broken"), "profile.json"), "not even close");

        Assert.Equal(new[] { "aardvark", "Zephyr" }, store.List());
    }

    [Fact]
    public void List_MissingDirectory_IsEmpty()
    {
        var store = new CampaignProfileStore(Path.Combine(TestData.TempDir(), "never-created"));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Save_SameName_Overwrites()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        store.Save(CampaignProfileDef.NewProfile("Zachary"));
        var replacement = CampaignProfileDef.NewProfile("Zachary");
        replacement.Funds = 5000;
        store.Save(replacement);

        Assert.Single(store.List());
        Assert.Equal(5000, store.Load("Zachary")!.Funds);
    }

    /// <summary>Deleting a profile removes only its own directory, proven here by a sentinel file
    /// dropped next to (never inside) the deleted profile's directory, standing in for the global
    /// <c>user://Planes/</c> store a real deletion must never touch.</summary>
    [Fact]
    public void Delete_RemovesOnlyThatProfilesDirectory()
    {
        var dir = TestData.TempDir();
        var store = new CampaignProfileStore(dir);
        store.Save(CampaignProfileDef.NewProfile("Zachary"));
        store.Save(CampaignProfileDef.NewProfile("Keeper"));
        var sentinel = Path.Combine(dir, "not-a-profile.txt");
        File.WriteAllText(sentinel, "hangar planes live outside a profile's directory");

        Assert.True(store.Delete("Zachary"));

        Assert.Null(store.Load("Zachary"));
        Assert.Equal("Keeper", Assert.Single(store.List()));
        Assert.True(File.Exists(sentinel));
    }

    /// <summary>Deleting is sanitised exactly as saving is, so a name a directory cannot carry is
    /// deleted by the same identity it was saved under.</summary>
    [Fact]
    public void Delete_SanitisesTheNameLikeSave()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        store.Save(CampaignProfileDef.NewProfile("Red/Blue: One"));

        Assert.True(store.Delete("Red/Blue: One"));
        Assert.Empty(store.List());
    }

    /// <summary>A name with no directory is a no-op, not an error: a caller may ask twice.</summary>
    [Fact]
    public void Delete_MissingProfile_IsANoOp()
    {
        var store = new CampaignProfileStore(Path.Combine(TestData.TempDir(), "never-created"));

        Assert.False(store.Delete("Nobody"));
        Assert.False(store.Delete("   "));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Save_EmptyName_Throws()
    {
        var store = new CampaignProfileStore(TestData.TempDir());
        Assert.Throws<ArgumentException>(() => store.Save(new CampaignProfileDef { Name = "   " }));
    }

    /// <summary>"." is the store's own root and ".." its parent, so a dot-only name must never
    /// reach the filesystem: Save refuses it, and Delete returns false instead of recursively
    /// removing every profile (or the store's parent directory).</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(" .. ")]
    [InlineData("...")]
    public void DotOnlyNames_AreRejectedEverywhere(string name)
    {
        var dir = TestData.TempDir();
        var store = new CampaignProfileStore(dir);
        store.Save(CampaignProfileDef.NewProfile("Zachary"));

        Assert.Throws<ArgumentException>(() => store.Save(new CampaignProfileDef { Name = name }));
        Assert.False(store.Delete(name));
        Assert.Null(store.Load(name));
        Assert.True(Directory.Exists(Path.Combine(dir, "Zachary")));
    }
}
