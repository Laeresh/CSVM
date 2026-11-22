using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CSVM.Flight.Hangar;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The plane mod folder's discovery, the <c>plane.json</c> checks and the content hash, over mods
/// built in a temp data root.
/// </summary>
public class ModPlanesTests
{
    private const string Avenger = """
        {
          "_note": "a test mod",
          "name": "E-1C Avenger",
          "kind_of": "Player_Brigand",
          "unlock": "start",
          "cockpit": "player_warhawk",
          "sound": "player_fury",
          "portrait": "art/Portrait.png",
          "overrides": { "fd_speed": 123.5, "turrets": [] }
        }
        """;

    [Fact]
    public void AFolderModLoadsEveryField()
    {
        string root = TestData.TempDir();
        string mod = Folder(root, "avenger", Avenger);
        Write(mod, "art/Portrait.png", "png");

        var plane = Assert.Single(ModPlanes.Open(root).Planes);

        Assert.Equal("avenger", plane.Id);
        Assert.Equal("E-1C Avenger", plane.Name);
        Assert.Equal("player_brigand", plane.KindOf);
        Assert.Equal(ModPlane.UnlockStart, plane.Unlock);
        Assert.Equal("player_warhawk", plane.Cockpit);
        Assert.Equal("player_fury", plane.Sound);
        Assert.Equal("art/portrait.png", plane.Portrait);
        Assert.Equal(new Dictionary<string, double> { ["fd_speed"] = 123.5 }, plane.Overrides);
        Assert.True(plane.ClearsTurrets);
        Assert.Equal("glb", System.Text.Encoding.UTF8.GetString(plane.Read("PLANE.GLB")!));
        Assert.Equal("png", System.Text.Encoding.UTF8.GetString(plane.Read(plane.Portrait!)!));
        Assert.Null(plane.Read("missing.png"));
        Assert.Equal(64, plane.Hash.Length);
    }

    [Fact]
    public void TheTintNamesItsImagesAndMaterials()
    {
        string root = TestData.TempDir();
        string mod = Folder(root, "tinted", """
            { "kind_of": "player_brigand",
              "tint": { "_note": "R primary", "mask": "Tex\\Mask.PNG", "detail": "tex/detail.png", "materials": ["avenger", "Nacelle"] } }
            """);
        Write(mod, "tex/mask.png", "png");
        Write(mod, "tex/detail.png", "png");

        var plane = Assert.Single(ModPlanes.Open(root).Planes);

        Assert.Equal("tex/mask.png", plane.Tint!.Mask);
        Assert.Equal("tex/detail.png", plane.Tint.Detail);
        Assert.Equal(new[] { "avenger", "Nacelle" }, plane.Tint.Materials);
    }

    [Fact]
    public void DefaultsAreTheIdAndTheParentsUnlock()
    {
        string root = TestData.TempDir();
        string mod = Folder(root, "minimal", string.Empty);
        File.WriteAllText(Path.Combine(mod, "plane.json"), """{ "kind_of": "player_bhawk" }""", new System.Text.UTF8Encoding(true));

        var plane = ModPlanes.Open(root).Find("MINIMAL")!;

        Assert.Equal("minimal", plane.Name);
        Assert.Equal(ModPlane.UnlockWithParent, plane.Unlock);
        Assert.Null(plane.Cockpit);
        Assert.Null(plane.Sound);
        Assert.Null(plane.Portrait);
        Assert.Null(plane.Tint);
        Assert.Empty(plane.Overrides);
        Assert.False(plane.ClearsTurrets);
    }

    [Fact]
    public void AZipLoadsWithOrWithoutATopFolderAndHashesLikeTheFolder()
    {
        string root = TestData.TempDir();
        string mod = Folder(root, "folder", Avenger);
        Write(mod, "art/Portrait.png", "png");
        Zip(root, "nested", ("nested/plane.json", Avenger), ("nested/plane.glb", "glb"), ("nested\\art\\Portrait.png", "png"), ("outside.txt", "x"));
        Zip(root, "twotops", ("a/plane.json", Avenger), ("b/plane.json", Avenger));
        Zip(root, "flat", ("plane.json", Avenger), ("plane.glb", "glb"), ("art/Portrait.png", "png"), ("art/", string.Empty));

        var mods = ModPlanes.Open(root);

        Assert.Contains("several top folders", Assert.Single(mods.Skipped).Reason);
        Assert.Equal(new[] { "flat", "folder", "nested" }, mods.Planes.Select(p => p.Id));
        Assert.Single(mods.Planes.Select(p => p.Hash).Distinct());
        Assert.Equal("png", System.Text.Encoding.UTF8.GetString(mods.Find("nested")!.Read("art/portrait.png")!));
    }

    [Fact]
    public void TheHashFollowsContentNotNamesOnDiskOrHiddenFiles()
    {
        string root = TestData.TempDir();
        Folder(root, "a", """{ "kind_of": "player_fury" }""");
        string b = Folder(root, "b", """{ "kind_of": "player_fury" }""");
        Write(b, ".DS_Store", "finder");
        string c = Folder(root, "c", """{ "kind_of": "player_fury" }""");
        Write(c, "readme.txt", "notes");
        string d = Folder(root, "d", """{ "kind_of": "player_fury" }""");
        Write(d, "plane.glb", "glb2");

        var hashes = ModPlanes.Open(root).Planes.ToDictionary(p => p.Id, p => p.Hash);

        Assert.Equal(hashes["a"], hashes["b"]);
        Assert.NotEqual(hashes["a"], hashes["c"]);
        Assert.NotEqual(hashes["a"], hashes["d"]);
    }

    [Theory]
    [InlineData("""{ "kind_of": "player_brigand", """, "bad json")]
    [InlineData("""[1]""", "is not an object")]
    [InlineData("""{ "name": "x" }""", "kind_of is missing")]
    [InlineData("""{ "kind_of": "pbrigand" }""", "is not a stock airframe node")]
    [InlineData("""{ "kind_of": "player_brigand", "cockpit": "player_jet" }""", "cockpit player_jet is not a stock")]
    [InlineData("""{ "kind_of": "player_brigand", "speed": 3 }""", "unknown key speed")]
    [InlineData("""{ "kind_of": "player_brigand", "unlock": "" }""", "unlock is not a non-empty string")]
    [InlineData("""{ "kind_of": "player_brigand", "overrides": { "fd_speed": "fast" } }""", "overrides.fd_speed is not a finite number")]
    [InlineData("""{ "kind_of": "player_brigand", "overrides": { "turrets": [1] } }""", "overrides.turrets may only be []")]
    [InlineData("""{ "kind_of": "player_brigand", "overrides": [] }""", "overrides is not an object")]
    [InlineData("""{ "kind_of": "player_brigand", "portrait": "p.png" }""", "portrait p.png is not in the mod")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": [] }""", "tint is not an object")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "detail": "d.png", "materials": ["m"] } }""", "tint.mask is missing")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "materials": ["m"] } }""", "tint.detail is missing")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "detail": "d.png" } }""", "tint.materials is missing")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "detail": "d.png", "materials": [] } }""", "tint.materials is not a non-empty list")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "detail": "d.png", "materials": ["m", 3] } }""", "tint.materials is not a non-empty list")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "x.png", "detail": "d.png", "materials": ["m"] } }""", "tint.mask x.png is not in the mod")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "detail": "plane.glb", "materials": ["m"] } }""", "tint.detail plane.glb is not a .png")]
    [InlineData("""{ "kind_of": "player_brigand", "tint": { "mask": "m.png", "detail": "d.png", "materials": ["m"], "gloss": 1 } }""", "unknown key tint.gloss")]
    public void AnInvalidPlaneJsonSkipsTheModWithItsReason(string json, string reason)
    {
        string root = TestData.TempDir();
        string broken = Folder(root, "broken", json);
        Write(broken, "m.png", "png");
        Write(broken, "d.png", "png");
        Folder(root, "fine", """{ "kind_of": "player_brigand" }""");

        var mods = ModPlanes.Open(root);

        Assert.Equal("fine", Assert.Single(mods.Planes).Id);
        var skipped = Assert.Single(mods.Skipped);
        Assert.Equal("broken", skipped.Id);
        if (reason != "bad json")
        {
            Assert.Contains(reason, skipped.Reason);
        }
    }

    [Fact]
    public void ACollidingIdOrAMissingFileSkipsTheMod()
    {
        string root = TestData.TempDir();
        Folder(root, "player_avenger", """{ "kind_of": "player_brigand" }""");
        Folder(root, "dup", """{ "kind_of": "player_brigand" }""");
        Zip(root, "DUP", ("plane.json", """{ "kind_of": "player_brigand" }"""), ("plane.glb", "glb"));
        File.Delete(Path.Combine(Folder(root, "nomodel", """{ "kind_of": "player_brigand" }"""), "plane.glb"));
        Directory.CreateDirectory(Path.Combine(ModPlanes.FolderIn(root), "empty"));
        File.WriteAllText(Path.Combine(ModPlanes.FolderIn(root), "junk.zip"), "not a zip");
        File.WriteAllText(Path.Combine(ModPlanes.FolderIn(root), "loose.json"), "{}");

        var mods = ModPlanes.Open(root);

        Assert.Equal("dup", Assert.Single(mods.Planes).Id);
        var reasons = mods.Skipped.ToDictionary(s => s.Id, s => s.Reason);
        Assert.Equal(new[] { "DUP", "empty", "junk", "nomodel", "player_avenger" }, reasons.Keys.OrderBy(k => k, System.StringComparer.Ordinal));
        Assert.Contains("stock airframe", reasons["player_avenger"]);
        Assert.Contains("taken by an earlier mod", reasons["DUP"]);
        Assert.Contains("no plane.glb", reasons["nomodel"]);
        Assert.Contains("no plane.json", reasons["empty"]);
    }

    [Fact]
    public void NoFolderIsNoMods()
    {
        var mods = ModPlanes.Open(TestData.TempDir());

        Assert.Empty(mods.Planes);
        Assert.Empty(mods.Skipped);
    }

    private static string Folder(string root, string id, string json)
    {
        string mod = Path.Combine(ModPlanes.FolderIn(root), id);
        Write(mod, "plane.json", json);
        Write(mod, "plane.glb", "glb");
        return mod;
    }

    private static void Write(string mod, string relative, string text)
    {
        string path = Path.Combine(mod, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Zip(string root, string id, params (string Name, string Text)[] entries)
    {
        Directory.CreateDirectory(ModPlanes.FolderIn(root));
        using var zip = ZipFile.Open(Path.Combine(ModPlanes.FolderIn(root), id + ".zip"), ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(text);
        }
    }
}
