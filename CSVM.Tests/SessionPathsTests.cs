using System.IO;
using System.Linq;
using CSVM;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The unzbd output layout under a folder: the per-chapter and per-mission paths, and the
/// unpacked-folder-beats-zip preference.
/// </summary>
public class SessionPathsTests
{
    [Fact]
    public void ChapterAndMissionPathsFollowTheOutputLayout()
    {
        var root = TestData.TempDir();
        Assert.Equal(Path.Combine(root, "C4", "texture.zip"),
            SessionPaths.ChapterTextures(root, "C4"));
        Assert.Equal(Path.Combine(root, "C4", "gamez.zip"),
            SessionPaths.ChapterGamez(root, "C4"));
        Assert.Equal(Path.Combine(root, "C4", "zrdr.zip"),
            SessionPaths.ChapterZrdr(root, "C4"));
        Assert.Equal(Path.Combine(root, "C4", "IA1", "zrdr.zip"),
            SessionPaths.MissionZrdr(root, "C4", "IA1"));
    }

    /// <summary>A copy of a disc without its chapters holds the root archives and no chapter world.
    /// One chapter's GameZ is enough; no game files hold none.</summary>
    [Fact]
    public void AChapterWorldIsFoundInTheGameFilesAndNeverAtTheRoot()
    {
        var root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "ZBD"));
        File.WriteAllText(Path.Combine(root, "ZBD", "planes.zbd"), "x");
        var game = new InstallFolderSource(root);
        Assert.False(SessionPaths.HasAnyChapterWorld(game));
        Assert.False(SessionPaths.HasAnyChapterWorld(null));

        Directory.CreateDirectory(Path.Combine(root, "ZBD", "c3"));
        File.WriteAllText(Path.Combine(root, "ZBD", "c3", "GAMEZ.ZBD"), "x");
        Assert.True(SessionPaths.HasAnyChapterWorld(game));
    }

    /// <summary>The campaign sequence names its folders <c>c3</c>/<c>m01</c>; unzbd's tree spells
    /// them <c>C3/M01</c>. Compared as strings, since a Windows disk would find either.</summary>
    [Fact]
    public void ALowerCaseChapterAndMissionMapToTheCaseTheTreeSpells()
    {
        var root = TestData.TempDir();
        Assert.Equal(Path.Combine(root, "C3", "M01", "zrdr.zip"),
            SessionPaths.MissionZrdr(root, "c3", "m01"));
        Assert.Equal(Path.Combine(root, "C1B", "gamez.zip"),
            SessionPaths.ChapterGamez(root, "c1b"));
        Assert.Equal(Path.Combine(root, "C2B", "zrdr.zip"),
            SessionPaths.ChapterZrdr(root, "c2b"));
        Assert.Equal(Path.Combine(root, "C5", "texture.zip"),
            SessionPaths.ChapterTextures(root, "c5"));
        Assert.Equal(Path.Combine(root, "C3", "cam_anim.zip"), SessionPaths.ChapterAnim(root, "c3"));
        Assert.Equal(Path.Combine(root, "C3", "M01", "mis_anim.zip"), SessionPaths.MissionAnim(root, "c3", "m01"));
        Assert.Equal("C3/M01/zrdr.zip", ZbdTree.Canonical(@"c3\m01\ZRDR.zip"));
    }

    /// <summary>Every campaign mission's own folder names resolve to the path the tree holds for
    /// that mission's archive, whatever case the sequence spells it in.</summary>
    [Fact]
    public void EveryCampaignMissionResolvesToItsFolderInTheTree()
    {
        var root = TestData.TempDir();
        for (int campaign = 1; campaign <= 8; campaign++)
        {
            for (int mission = 1; mission <= 5; mission++)
            {
                var named = new Mech3.CampaignMission(0, "", campaign, mission, "", false);
                Assert.Equal(
                    Path.Combine(root, named.ChapterFolder.ToUpperInvariant(), named.MissionFolder.ToUpperInvariant(), "zrdr.zip"),
                    SessionPaths.MissionZrdr(root, named.ChapterFolder, named.MissionFolder));
            }
        }
    }

    [Fact]
    public void TheManifestHoldsEveryChaptersCoreAndMissionArchivesOnce()
    {
        Assert.Equal(184, ZbdManifest.Expected.Count);
        Assert.Equal(ZbdManifest.Expected.Count, ZbdManifest.Expected.Distinct().Count());
        Assert.Contains("C2B/rtexture9.zbd", ZbdManifest.Expected);
        Assert.Contains("C1/M05/mis_anim.zbd", ZbdManifest.Expected);
        Assert.All(ZbdManifest.Expected, p => Assert.Equal(ZbdTree.Canonical(p), p));
    }

    [Fact]
    public void AnUnpackedSiblingFolderWinsOverItsZip()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "C4");
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
    public void TheTopRtextureTierBeatsTheBaseTextureArchive()
    {
        var root = TestData.TempDir();
        var chapter = Path.Combine(root, "C1");
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
