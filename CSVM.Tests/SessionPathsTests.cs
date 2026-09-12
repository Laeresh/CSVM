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

    [Fact]
    public void ForceZippedTakesTheZipEvenWhereTheUnpackedFolderExists()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "extracted", "C4");
        Directory.CreateDirectory(Path.Combine(chapter, "gamez"));
        File.WriteAllText(Path.Combine(chapter, "gamez.zip"), "not really a zip");
        try
        {
            SessionPaths.ForceZipped = true;
            Assert.Equal(Path.Combine(chapter, "gamez.zip"), SessionPaths.ChapterGamez(root, "C4"));
        }
        finally
        {
            SessionPaths.ForceZipped = false;
        }
    }

    [Fact]
    public void ForceZippedStillTakesTheFolderWhereNoZipWasEverExtracted()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "extracted", "C4");
        Directory.CreateDirectory(Path.Combine(chapter, "gamez"));
        try
        {
            // Asking for the export's asset shape must not refuse to start a tree that only ever
            // had the folder, the flag narrows the choice, it does not add a requirement.
            SessionPaths.ForceZipped = true;
            Assert.Equal(Path.Combine(chapter, "gamez"), SessionPaths.ChapterGamez(root, "C4"));
        }
        finally
        {
            SessionPaths.ForceZipped = false;
        }
    }

    [Fact]
    public void TheTopRtextureTierBeatsTheBaseTextureArchive()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "extracted", "C1");
        Directory.CreateDirectory(chapter);
        File.WriteAllText(Path.Combine(chapter, "texture.zip"), "x");
        File.WriteAllText(Path.Combine(chapter, "rtexture2.zip"), "x");
        File.WriteAllText(Path.Combine(chapter, "rtexture15.zip"), "x");
        // numeric, not lexicographic: 15 must beat 2 and 8
        File.WriteAllText(Path.Combine(chapter, "rtexture8.zip"), "x");

        Assert.Equal(Path.Combine(chapter, "rtexture15.zip"), SessionPaths.ChapterTextures(root, "C1"));

        // and the tier obeys the same unpacked-folder preference as everything else
        Directory.CreateDirectory(Path.Combine(chapter, "rtexture15"));
        Assert.Equal(Path.Combine(chapter, "rtexture15"), SessionPaths.ChapterTextures(root, "C1"));
    }
}
