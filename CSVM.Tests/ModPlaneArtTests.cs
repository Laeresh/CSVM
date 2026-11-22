using System.IO;
using CSVM.Flight.Hangar;
using CSVM.UI.Hangar;
using Xunit;

namespace CSVM.Tests;

/// <summary>The engine-free half of a plane mod's menu art: the cache key, the portrait choice and
/// the slot fit.</summary>
public class ModPlaneArtTests
{
    [Fact]
    public void TheCacheFileIsKeyedByIdHashAndView()
    {
        string a = ModPlaneArt.CacheFile("cache", "avenger", "0123456789abcdef0123", ModArtView.Plan);
        string b = ModPlaneArt.CacheFile("cache", "avenger", "fedcba98765432100123", ModArtView.Plan);

        Assert.Equal(Path.Combine("cache", "avenger_0123456789abcdef_plan.png"), a);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, ModPlaneArt.CacheFile("cache", "avenger", "0123456789abcdef0123", ModArtView.HeadOn));
    }

    [Fact]
    public void OnlyThePortraitViewComesFromThePortraitImage()
    {
        string root = TestData.TempDir();
        Mod(root, "pictured", """{ "kind_of": "player_brigand", "portrait": "face.png" }""");
        Mod(root, "bare", """{ "kind_of": "player_brigand" }""");
        var mods = ModPlanes.Open(root);
        var pictured = mods.Find("pictured")!;
        var bare = mods.Find("bare")!;

        Assert.True(ModPlaneArt.FromPortrait(pictured, ModArtView.Portrait));
        Assert.False(ModPlaneArt.FromPortrait(pictured, ModArtView.Plan));
        Assert.False(ModPlaneArt.FromPortrait(bare, ModArtView.Portrait));
    }

    [Fact]
    public void APictureFitsItsSlotCentredWithItsAspect()
    {
        Assert.Equal((10f, 30f, 80f, 40f), ModPlaneArt.Fit(200, 100, 10f, 10f, 80f, 80f));
        Assert.Equal((30f, 10f, 40f, 80f), ModPlaneArt.Fit(50, 100, 10f, 10f, 80f, 80f));
    }

    private static void Mod(string root, string id, string json)
    {
        string folder = Path.Combine(ModPlanes.FolderIn(root), id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModPlanes.DefinitionFile), json);
        File.WriteAllText(Path.Combine(folder, ModPlanes.ModelFile), "glb");
        File.WriteAllText(Path.Combine(folder, "face.png"), "png");
    }
}
