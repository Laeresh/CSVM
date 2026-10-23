using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The model folder's lookup rules: the three keys and their precedence, the <c>.flt</c> rule, and
/// a <c>.gltf</c> read only from a loose folder.
/// </summary>
public class HdModelsTests
{
    [Fact]
    public void EveryKeyFormResolvesAndTheMoreSpecificOneWins()
    {
        var root = TestData.TempDir();
        var dir = Path.Combine(root, "HDModels");
        Zip(Path.Combine(dir, "pack.zip"),
            ("pack/tower.glb", "bare"),
            ("pack/C3/tower.glb", "c3"),
            ("pack/prop1.glb", "bare prop"),
            ("pack/planes/BHawk/prop1.glb", "bhawk"),
            ("pack/C3/prop1.glb", "c3 prop"));

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal(5, hd.Count);
        Assert.Equal("c3", Text(hd.For("TOWER", chapter: "C3")));
        Assert.Equal("bare", Text(hd.For("tower", chapter: "C5")));
        Assert.Equal("bare", Text(hd.For("tower")));
        Assert.Equal("bhawk", Text(hd.For("prop1", airframe: "bhawk")));
        Assert.Equal("bhawk", Text(hd.For("prop1", chapter: "c3", airframe: "bhawk")));
        Assert.Equal("c3 prop", Text(hd.For("prop1", chapter: "c3", airframe: "fury")));
        Assert.Equal("bare prop", Text(hd.For("prop1", airframe: "fury")));
        Assert.Null(hd.For("hangar", chapter: "C3"));
        Assert.Null(hd.For(""));
        Assert.Equal("c3/tower", hd.KeyFor("TOWER", chapter: "C3"));
        Assert.Equal("planes/bhawk/prop1", hd.KeyFor("prop1.flt", chapter: "c3", airframe: "BHawk"));
        Assert.Equal("tower", hd.KeyFor("tower", chapter: "C5"));
        Assert.Null(hd.KeyFor("hangar", chapter: "C3"));

        var model = hd.For("tower")!.Value;
        Assert.True(model.IsGlb);
        Assert.Null(model.GltfPath);
    }

    /// <summary>A clump tile's files are read under their whole path and count as no replacement. No node
    /// resolves to one, not even a node named like the file, and any other <c>.png</c> is ignored.</summary>
    [Fact]
    public void AClumpTileKeyReachesNoNode()
    {
        var root = TestData.TempDir();
        Zip(Path.Combine(root, "HDModels", "pack.zip"),
            ("pack/C1/firtree1.clump.png", "png"),
            ("pack/C1/firtree1.clump.json", "json"),
            ("pack/C1/firtree1.clump.side.png", "side"),
            ("pack/C1/firtree1.png", "loose png"),
            ("pack/C1/tower.glb", "tower"));

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal(1, hd.Count);
        Assert.Equal(3, hd.ClumpCount);
        Assert.Equal("png", Text(hd.Clump("C1/FirTree1.clump.png")!.Value.Bytes));
        Assert.Equal("json", Text(hd.Clump("c1/firtree1.clump.json")!.Value.Bytes));
        Assert.Equal("side", Text(hd.Clump("C1/firtree1.clump.side.png")!.Value.Bytes));
        Assert.EndsWith("pack/C1/firtree1.clump.png", hd.Clump("C1/firtree1.clump.png")!.Value.Source);
        Assert.Null(hd.Clump("C1/firtree1.png"));
        Assert.Null(hd.Clump("C1/tower.glb"));
        Assert.Null(hd.Clump("C4/firtree1.clump.png"));
        foreach (string node in new[] { "firtree1", "firtree1.clump", "firtree1.clump.png", "FIRTREE1.CLUMP.JSON", "firtree1.clump.side.png" })
        {
            Assert.Null(hd.KeyFor(node, chapter: "C1"));
            Assert.Null(hd.KeyFor(node));
        }
        Assert.Equal("c1/tower", hd.KeyFor("tower", chapter: "C1"));
    }

    [Fact]
    public void ATrailingFltIsDroppedFromTheNodeAndTheFileName()
    {
        var root = TestData.TempDir();
        var dir = Path.Combine(root, "HDModels", "loose");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "he_rocket.glb"), "rocket");
        File.WriteAllText(Path.Combine(dir, "gunshell.FLT.glb"), "shell");
        File.WriteAllText(Path.Combine(dir, "a.b.glb"), "dotted");

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal("rocket", Text(hd.For("he_rocket.flt")));
        Assert.Equal("rocket", Text(hd.For("HE_ROCKET")));
        Assert.Equal("shell", Text(hd.For("gunshell")));
        Assert.Equal("shell", Text(hd.For("gunshell.flt")));
        Assert.Equal("dotted", Text(hd.For("a.b")));
        Assert.Null(hd.For("a"));
        Assert.Equal("he_rocket", HdModels.NodeKey("He_Rocket.FLT"));
        Assert.Equal("flt_mount", HdModels.NodeKey("flt_mount"));
    }

    [Fact]
    public void AGltfIsReadFromALooseFolderWithItsPathButRefusedInAZip()
    {
        var root = TestData.TempDir();
        var dir = Path.Combine(root, "HDModels");
        Directory.CreateDirectory(Path.Combine(dir, "b_loose", "C2"));
        var gltf = Path.Combine(dir, "b_loose", "C2", "barn.gltf");
        File.WriteAllText(gltf, "{}");
        File.WriteAllText(Path.Combine(dir, "b_loose", "C2", "barn.bin"), "buffer");
        Zip(Path.Combine(dir, "a.zip"), ("a/silo.gltf", "{}"), ("a/silo.glb", "silo"));
        Zip(Path.Combine(dir, "c.zip"), ("c/C2/barn.gltf", "{}"));

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal(2, hd.Count);
        var barn = hd.For("barn", chapter: "C2")!.Value;
        Assert.False(barn.IsGlb);
        Assert.Equal(gltf, barn.GltfPath);
        Assert.Equal("{}", Text(barn.Bytes));
        // A refused .gltf neither counts nor displaces an earlier file under its key.
        Assert.Equal("silo", Text(hd.For("silo")));
        Assert.True(hd.For("silo")!.Value.IsGlb);
    }

    [Fact]
    public void AnAirframeRigIsReadOnlyAsPlanesAirframeRigJsonAndNamesItsSource()
    {
        var root = TestData.TempDir();
        var dir = Path.Combine(root, "HDModels");
        Zip(Path.Combine(dir, "a.zip"),
            ("a/planes/Player_BHawk/RIG.json", "zip rig"),
            ("a/planes/player_bhawk/rig.glb", "rig mesh"),
            ("a/planes/player_bhawk/notes.json", "stray"),
            ("a/planes/rig.json", "stray"),
            ("a/planes/player_fury/extra/rig.json", "stray"),
            ("a/rig.json", "stray"));
        Directory.CreateDirectory(Path.Combine(dir, "b_loose", "planes", "player_fury"));
        var loose = Path.Combine(dir, "b_loose", "planes", "player_fury", "rig.json");
        File.WriteAllText(loose, "loose rig");

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal(1, hd.Count);
        Assert.Equal(2, hd.RigCount);
        var zipped = hd.RigFor("PLAYER_BHAWK")!.Value;
        Assert.Equal("zip rig", Text(zipped.Bytes));
        Assert.Equal(Path.Combine(dir, "a.zip") + ":a/planes/Player_BHawk/RIG.json", zipped.Source);
        Assert.Equal("loose rig", Text(hd.RigFor("player_fury")!.Value.Bytes));
        Assert.Equal(loose, hd.RigFor("player_fury")!.Value.Source);
        Assert.Null(hd.RigFor("player_kestrel"));
        Assert.Null(hd.RigFor(""));

        // A model node named rig is a model, never the rig, and a rig is never a model.
        Assert.Equal("rig mesh", Text(hd.For("rig", airframe: "player_bhawk")));
        Assert.Null(hd.For("rig", airframe: "player_fury"));
        Assert.Null(hd.For("notes", airframe: "player_bhawk"));
    }

    [Fact]
    public void ALaterPackWinsAnAirframesRig()
    {
        var root = TestData.TempDir();
        var dir = Path.Combine(root, "HDModels");
        Zip(Path.Combine(dir, "a.zip"), ("a/planes/player_bhawk/rig.json", "first"));
        Zip(Path.Combine(dir, "b.zip"), ("b/planes/player_bhawk/rig.json", "second"));

        using var hd = HdModels.Open(root, "HDModels");

        Assert.Equal(1, hd.RigCount);
        Assert.Equal("second", Text(hd.RigFor("player_bhawk")!.Value.Bytes));
        Assert.Null(HdModels.Open(root, "").RigFor("player_bhawk"));
    }

    [Fact]
    public void NoFolderOrAnEmptyNameReadsNoPack()
    {
        var root = TestData.TempDir();
        Assert.Equal(0, HdModels.Open(root, "HDModels").Count);
        Assert.Equal(0, HdModels.Open(root, "").Count);
        Assert.Null(HdModels.Open(root, "").For("tower"));
    }

    private static string? Text(HdModel? model) => model == null ? null : Text(model.Value.Bytes);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void Zip(string path, params (string Name, string Body)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, body) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(body);
        }
    }
}
