using System.IO;
using CSVM;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Extraction-path arithmetic: the per-chapter and per-mission layout, and the
/// unpacked-folder-beats-zip preference.
/// </summary>
public class SessionPathsTests
{
    [Fact]
    public void ChapterAndMissionPathsFollowTheExtractionLayout()
    {
        var root = TestData.TempDir();
        Assert.Equal(Path.Combine(root, "extracted", "C4", "texture.zip"),
            SessionPaths.ChapterTextures(root, "C4"));
        Assert.Equal(Path.Combine(root, "extracted", "C4", "gamez.zip"),
            SessionPaths.ChapterGamez(root, "C4"));
        Assert.Equal(Path.Combine(root, "extracted", "C4", "zrdr.zip"),
            SessionPaths.ChapterZrdr(root, "C4"));
        Assert.Equal(Path.Combine(root, "extracted", "C4", "IA1", "zrdr.zip"),
            SessionPaths.MissionZrdr(root, "C4", "IA1"));
    }

    [Fact]
    public void AnUnpackedSiblingFolderWinsOverItsZip()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "extracted", "C4");
        Directory.CreateDirectory(Path.Combine(chapter, "gamez"));
        File.WriteAllText(Path.Combine(chapter, "gamez.zip"), "not really a zip");

        Assert.Equal(Path.Combine(chapter, "gamez"), SessionPaths.ChapterGamez(root, "C4"));
        // No folder for textures, so its zip path passes through verbatim.
        Assert.Equal(Path.Combine(chapter, "texture.zip"), SessionPaths.ChapterTextures(root, "C4"));
    }

    [Fact]
    public void PreferUnzippedIsPurePathArithmeticWhenNothingExists()
    {
        var missing = Path.Combine(TestData.TempDir(), "nowhere", "zrdr.zip");
        Assert.Equal(missing, SessionPaths.PreferUnzipped(missing));
    }
}
