using System;
using System.IO;
using System.Linq;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>B11's persistence contract: a plane round-trips through its JSON file with every
/// chosen field intact, a missing or malformed file reads as nothing, the list is every loadable
/// plane, and the name is the identity (same name overwrites).</summary>
public class CustomPlaneStoreTests
{
    [Fact]
    public void RoundTrip_PreservesEveryField()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(FullDef());

        var loaded = store.Load("Blue Streak");

        Assert.NotNull(loaded);
        var expected = FullDef();
        Assert.Equal(expected.Name, loaded!.Name);
        Assert.Equal(expected.Airframe, loaded.Airframe);
        Assert.Equal(expected.Engine, loaded.Engine);
        Assert.Equal(expected.ArmourNose, loaded.ArmourNose);
        Assert.Equal(expected.ArmourTail, loaded.ArmourTail);
        Assert.Equal(expected.ArmourLeftWing, loaded.ArmourLeftWing);
        Assert.Equal(expected.ArmourRightWing, loaded.ArmourRightWing);
        Assert.Equal(expected.Guns, loaded.Guns);
        Assert.Equal(expected.LeftHardpoints, loaded.LeftHardpoints);
        Assert.Equal(expected.RightHardpoints, loaded.RightHardpoints);
        Assert.Equal(expected.PaintPattern, loaded.PaintPattern);
        Assert.Equal(expected.PaintPick1, loaded.PaintPick1);
        Assert.Equal(expected.PaintPick2, loaded.PaintPick2);
        Assert.Equal(expected.PaintPick3, loaded.PaintPick3);
        Assert.Equal(expected.Colour1, loaded.Colour1);
        Assert.Equal(expected.Colour2, loaded.Colour2);
        Assert.Equal(expected.Colour3, loaded.Colour3);
    }

    [Fact]
    public void Serialize_IsStableAcrossARoundTrip()
    {
        var first = CustomPlaneStore.Serialize(FullDef());
        var reread = CustomPlaneStore.Deserialize(first);

        Assert.NotNull(reread);
        Assert.Equal(first, CustomPlaneStore.Serialize(reread!));
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        Assert.Null(store.Load("Never Built"));
    }

    [Fact]
    public void Load_MalformedFile_ReturnsNull()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        var garbled = store.Save(FullDef());
        File.WriteAllText(garbled, "{ not json at all");

        Assert.Null(store.Load("Blue Streak"));
    }

    [Fact]
    public void Load_WrongVersion_ReturnsNull()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        var path = store.Save(FullDef());
        File.WriteAllText(path, CustomPlaneStore.Serialize(FullDef()).Replace("\"version\": 1", "\"version\": 99"));

        Assert.Null(store.Load("Blue Streak"));
    }

    [Fact]
    public void List_SortsByName_AndSkipsTheUnreadable()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(new CustomPlaneDef { Name = "Zephyr" });
        store.Save(new CustomPlaneDef { Name = "aardvark" });
        store.Save(new CustomPlaneDef { Name = "Jumping Jane" });
        File.WriteAllText(store.PathFor("Broken"), "not even close");

        var names = store.List();

        Assert.Equal(new[] { "aardvark", "Jumping Jane", "Zephyr" }, names.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void List_MissingDirectory_IsEmpty()
    {
        var store = new CustomPlaneStore(Path.Combine(TestData.TempDir(), "never-created"));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Save_SameName_Overwrites()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(FullDef());
        var replacement = FullDef();
        replacement.Engine = 5;
        store.Save(replacement);

        Assert.Single(store.List());
        Assert.Equal(5, store.Load("Blue Streak")!.Engine);
    }

    [Fact]
    public void Save_EmptyName_Throws()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        Assert.Throws<ArgumentException>(() => store.Save(new CustomPlaneDef { Name = "   " }));
    }

    [Fact]
    public void Deserialize_ClampsOutOfRangeValues()
    {
        var json = CustomPlaneStore.Serialize(FullDef())
            .Replace("\"airframe\": 7", "\"airframe\": 99")
            .Replace("\"pattern\": 13", "\"pattern\": -2");

        var def = CustomPlaneStore.Deserialize(json);

        Assert.NotNull(def);
        Assert.Equal(CustomPlaneDef.MaxAirframe, def!.Airframe);
        Assert.Equal(0, def.PaintPattern);
    }

    [Fact]
    public void Constructor_RelativeDirectory_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CustomPlaneStore("Planes"));
    }

    private static CustomPlaneDef FullDef() => new()
    {
        Name = "Blue Streak",
        Airframe = 7,
        Engine = 3,
        ArmourNose = 4,
        ArmourTail = 1,
        ArmourLeftWing = 12,
        ArmourRightWing = 0,
        LeftHardpoints = 2,
        RightHardpoints = 4,
        PaintPattern = 13,
        PaintPick1 = 12,
        PaintPick2 = 7,
        PaintPick3 = 42,
        Colour1 = new PaintColour(223, 0, 41),
        Colour2 = new PaintColour(25, 25, 25),
        Colour3 = new PaintColour(255, 255, 255),
        Guns =
        {
            [0] = new GunChoice(2, Twin: true),
            [1] = new GunChoice(0, Twin: false),
            [2] = new GunChoice(null, Twin: false),
            [3] = new GunChoice(4, Twin: true),
        },
    };
}
