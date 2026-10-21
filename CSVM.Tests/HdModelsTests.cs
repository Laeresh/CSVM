using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The model folder's lookup rules: the airframe, chapter and bare keys and their precedence, the
/// <c>.flt</c> rule, and a <c>.gltf</c> read only from a loose folder.
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

        var model = hd.For("tower")!.Value;
        Assert.True(model.IsGlb);
        Assert.Null(model.GltfPath);
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
