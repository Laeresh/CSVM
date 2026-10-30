using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The HD folder's lookup rules. A release zip is read in place under its own top folder and
/// matched without case or extension. A chapter's own entry beats the bare name; later packs win.
/// </summary>
public class HdTexturesTests
{
    [Fact]
    public void ReleaseZipsReplaceUiFilesAndWorldTexturesInPlace()
    {
        var root = TestData.TempDir();
        var hdDir = Path.Combine(root, "HD");
        Directory.CreateDirectory(hdDir);
        Zip(Path.Combine(hdDir, "crimson_hd_chosen_ui_textures.zip"),
            ("ui_textures/rimage/c1m1.png", "c1m1"),
            ("ui_textures/rof/ASSETS/GRAPHICS/SCRAPBOOK/SB_BG_1.png", "scrap"));
        Zip(Path.Combine(hdDir, "crimson_hd_chosen_world_textures.zip"),
            ("world_textures/cloud1.png", "flat"),
            ("world_textures/C3/rtexture12/cloud1.png", "c3"),
            ("world_textures/readme.txt", "not art"));
        using var hd = HdTextures.Open(root, "HD");

        Assert.Equal("c1m1", Text(hd.ForRimage("C1M1")));
        // The UI archive's paths are upper case and its scrapbook backdrops are .JPG.
        Assert.Equal("scrap", Text(hd.ForRof("ASSETS/GRAPHICS/SCRAPBOOK/SB_BG_1.JPG")));
        Assert.Null(hd.ForRimage("c1m2"));
        Assert.Null(hd.ForRof("ASSETS/GRAPHICS/SB_BG_1.JPG"));

        // A chapter's own entry beats the bare name, which every other archive takes.
        Assert.Equal("c3", Text(hd.ForWorld("c3/rtexture12", "cloud1")));
        Assert.Equal("c3", Text(hd.ForWorld("C3/rtexture12", "Cloud1")));
        Assert.Equal("flat", Text(hd.ForWorld("c5/rtexture14", "cloud1")));
        Assert.Equal("flat", Text(hd.ForWorld("", "cloud1")));
        Assert.Null(hd.ForWorld("c5/rtexture14", "readme"));
        Assert.Equal(4, hd.Count);
    }

    [Fact]
    public void ALaterPackOverridesAnEarlierOneAndALooseFolderCounts()
    {
        var root = TestData.TempDir();
        var hdDir = Path.Combine(root, "HD");
        Directory.CreateDirectory(Path.Combine(hdDir, "b_fixes", "rimage"));
        File.WriteAllText(Path.Combine(hdDir, "b_fixes", "rimage", "arrow.png"), "fixed");
        Zip(Path.Combine(hdDir, "a_pack.zip"), ("pack/rimage/arrow.png", "old"), ("pack/rimage/darrow.png", "kept"));

        using var hd = HdTextures.Open(root, "HD");

        Assert.Equal("fixed", Text(hd.ForRimage("arrow")));
        Assert.Equal("kept", Text(hd.ForRimage("darrow")));
    }

    [Fact]
    public void APackShippingBothFormatsOfOneNameAnswersEachOriginalWithItsOwn()
    {
        // The scrapbook draws SB_03_01_BASE.PNG on the page and SB_03_01_BASE.JPG in the zoom (#255).
        var root = TestData.TempDir();
        var hdDir = Path.Combine(root, "HD");
        Directory.CreateDirectory(hdDir);
        Zip(Path.Combine(hdDir, "pack.zip"),
            ("pack/rof/ASSETS/GRAPHICS/SCRAPBOOK/SB_03_01_BASE.jpg", "zoom"),
            ("pack/rof/ASSETS/GRAPHICS/SCRAPBOOK/SB_03_01_BASE.png", "thumb"));

        using var hd = HdTextures.Open(root, "HD");

        const string Scrapbook = "ASSETS/GRAPHICS/SCRAPBOOK/";
        Assert.Equal("thumb", Text(hd.ForRof(Scrapbook + "SB_03_01_BASE.PNG")));
        Assert.Equal("zoom", Text(hd.ForRof(Scrapbook + "SB_03_01_BASE.JPG")));
        // An original whose format the pack does not ship takes the name's last entry.
        Assert.Equal("thumb", Text(hd.ForRof(Scrapbook + "SB_03_01_BASE.TIF")));
        Assert.Equal(1, hd.Count);
    }

    [Fact]
    public void NoFolderOrAnEmptyNameReadsNoPack()
    {
        var root = TestData.TempDir();
        Assert.Equal(0, HdTextures.Open(root, "HD").Count);
        Assert.Equal(0, HdTextures.Open(root, "").Count);
    }

    private static string? Text(byte[]? bytes) => bytes == null ? null : Encoding.UTF8.GetString(bytes);

    private static void Zip(string path, params (string Name, string Body)[] entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, body) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(body);
        }
    }
}
