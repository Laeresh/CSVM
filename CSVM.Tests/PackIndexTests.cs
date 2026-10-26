using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The pack folder enumeration both HD lookups share. Zips and subfolders load in name order with
/// their top folder dropped, filtered by extension, and a later file wins its key.
/// </summary>
public class PackIndexTests
{
    private static readonly IReadOnlySet<string> Glb = new HashSet<string> { ".glb", ".gltf" };

    [Fact]
    public void AZipAndAFolderAreReadUnderTheirTopFolderAndFiltered()
    {
        var root = TestData.TempDir();
        var packs = Path.Combine(root, "Packs");
        Directory.CreateDirectory(Path.Combine(packs, "loose", "C3"));
        File.WriteAllText(Path.Combine(packs, "loose", "C3", "Tower.GLB"), "tower");
        File.WriteAllText(Path.Combine(packs, "loose", "C3", "tower.png"), "not a model");
        File.WriteAllText(Path.Combine(packs, "stray.glb"), "a loose file at the root is no pack");
        Zip(Path.Combine(packs, "a_pack.zip"), ("top/rocket.glb", "rocket"), ("top/readme.txt", "no"));

        using var index = PackIndex.Open(root, "Packs", Glb, f => PackIndex.KeyOf(f.RelativePath));

        Assert.Equal(packs, index.Root);
        Assert.Equal(2, index.Count);
        Assert.Equal("rocket", Text(index.Read("rocket")));
        Assert.Equal("tower", Text(index.Read("c3/tower")));
        Assert.Equal(new PackEntry(".glb", null), index.Find("rocket"));
        Assert.Equal(new PackEntry(".glb", Path.Combine(packs, "loose", "C3", "Tower.GLB")), index.Find("c3/tower"));
        Assert.Null(index.Find("stray"));
        Assert.Null(index.Read("readme"));
    }

    [Fact]
    public void ALaterPackWinsAndWithinAPackTheLaterNameWins()
    {
        var root = TestData.TempDir();
        var packs = Path.Combine(root, "Packs");
        Directory.CreateDirectory(Path.Combine(packs, "b_fix"));
        File.WriteAllText(Path.Combine(packs, "b_fix", "prop.glb"), "fix");
        Directory.CreateDirectory(Path.Combine(packs, "c_both"));
        File.WriteAllText(Path.Combine(packs, "c_both", "foo.gltf"), "text");
        File.WriteAllText(Path.Combine(packs, "c_both", "foo.glb"), "binary");
        Zip(Path.Combine(packs, "a_main.zip"), ("main/prop.glb", "main"), ("main/BAR.gltf", "upper"), ("main/bar.glb", "lower"));

        using var index = PackIndex.Open(root, "Packs", Glb, f => PackIndex.KeyOf(f.RelativePath));

        Assert.Equal("fix", Text(index.Read("prop")));
        // foo.gltf sorts after foo.glb, whatever order the disk or the zip lists them in.
        Assert.Equal("text", Text(index.Read("foo")));
        Assert.Equal(".gltf", index.Find("foo")!.Value.Extension);
        Assert.Equal("upper", Text(index.Read("bar")));
    }

    [Fact]
    public void TheKeyFunctionSeesTheSourceAndCanRefuseAFile()
    {
        var root = TestData.TempDir();
        var packs = Path.Combine(root, "Packs");
        Zip(Path.Combine(packs, "a.zip"), ("top/keep.glb", "keep"), ("top/drop.gltf", "drop"));
        var seen = new List<PackFile>();

        using var index = PackIndex.Open(root, "Packs", Glb, f =>
        {
            seen.Add(f);
            return f.InZip && f.RelativePath.EndsWith(".gltf") ? null : PackIndex.KeyOf(f.RelativePath);
        });

        Assert.Equal(1, index.Count);
        Assert.Contains(new PackFile(Path.Combine(packs, "a.zip"), "keep.glb", true), seen);
        Assert.Null(index.Read("drop"));
    }

    [Fact]
    public void NoFolderOrAnEmptyNameIsEmptyWithNoRoot()
    {
        var root = TestData.TempDir();
        Assert.Null(PackIndex.Open(root, "Packs", Glb, f => f.RelativePath).Root);
        Assert.Equal(0, PackIndex.Open(root, "", Glb, f => f.RelativePath).Count);
    }

    [Fact]
    public void AZipEntryNamedWithBackslashesDropsItsTopFolder()
    {
        var root = TestData.TempDir();
        Zip(Path.Combine(root, "Packs", "a.zip"), ("mypack\\planes\\player_bhawk\\body.glb", "body"));

        using var index = PackIndex.Open(root, "Packs", Glb, f => PackIndex.KeyOf(f.RelativePath));

        Assert.Equal("body", Text(index.Read("planes/player_bhawk/body")));
    }

    [Fact]
    public void ADamagedZipEntryReadsAsNullAndTheRestStillReads()
    {
        var root = TestData.TempDir();
        string zip = Path.Combine(root, "Packs", "a.zip");
        Zip(zip, ("top/broken.glb", new string('x', 4096)), ("top/fine.glb", "fine"));
        // The first entry's deflate data follows its local header, name and extra field. 0xFF opens
        // a block of the reserved type, which the inflater refuses.
        var bytes = File.ReadAllBytes(zip);
        int data = 30 + BitConverter.ToUInt16(bytes, 26) + BitConverter.ToUInt16(bytes, 28);
        Array.Fill(bytes, (byte)0xFF, data, 8);
        File.WriteAllBytes(zip, bytes);

        using var index = PackIndex.Open(root, "Packs", Glb, f => PackIndex.KeyOf(f.RelativePath));

        Assert.Null(index.Read("broken"));
        Assert.Equal("fine", Text(index.Read("fine")));
    }

    private static string? Text(byte[]? bytes) => bytes == null ? null : Encoding.UTF8.GetString(bytes);

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
