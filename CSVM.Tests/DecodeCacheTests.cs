using System.IO;
using CSVM;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The identity contract behind <see cref="DecodeCache"/>: a repeat lookup returns the very object
/// the first one decoded, and every input that changes what would be decoded changes the key. The
/// second half is what stops a cache handing one chapter's world the wrong mission's program, so it
/// is asserted input by input rather than as one happy case.
/// </summary>
public class DecodeCacheTests
{
    [Fact]
    public void RepeatingOneAnimKeyReturnsTheSameProgramAndCountsAHit()
    {
        var cache = new DecodeCache();
        var key = Key("/nowhere", "C1", "IA1");

        var first = Anim(cache, key);
        var second = Anim(cache, key);

        Assert.Same(first, second);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    [Theory]
    [InlineData("/elsewhere", "C1", "IA1")]   // a different data root
    [InlineData("/nowhere", "C3", "IA1")]     // a different chapter
    [InlineData("/nowhere", "C1", "M04")]     // a different mission
    public void ChangingAnyPartOfTheAnimKeyMisses(string root, string chapter, string mission)
    {
        var cache = new DecodeCache();
        var baseline = Anim(cache, Key("/nowhere", "C1", "IA1"));

        var other = Anim(cache, Key(root, chapter, mission));

        Assert.NotSame(baseline, other);
        Assert.Equal(2, cache.Misses);
        Assert.Equal(0, cache.Hits);
    }

    [ExtractedDataFact]
    public void RepeatingOneGamezPathReturnsTheSameDocumentAndADifferentChapterMisses()
    {
        var cache = new DecodeCache();
        string root = TestData.DataRoot!;
        string c1 = SessionPaths.ChapterGamez(root, "C1");
        string c3 = SessionPaths.ChapterGamez(root, "C3");

        var first = cache.Gamez(c1);
        var second = cache.Gamez(c1);
        var other = cache.Gamez(c3);

        Assert.Same(first, second);
        Assert.NotSame(first, other);
        Assert.NotEmpty(first.Nodes);
        Assert.Equal(2, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    // AnimProgram.Load tolerates every one of its five paths being absent (a user who has not
    // re-extracted gets reader-only behaviour), so the key can be exercised on names alone, no
    // install, and no decode cost, which is what lets this cover data root as well as chapter.
    private static string[] Key(string root, string chapter, string mission) => new[]
    {
        Path.Combine(root, "zrdr.zip"),
        Path.Combine(root, chapter, "zrdr.zip"),
        Path.Combine(root, chapter, mission, "zrdr.zip"),
        Path.Combine(root, chapter, "cam_anim.zip"),
        Path.Combine(root, chapter, mission, "mis_anim.zip"),
    };

    private static AnimProgram Anim(DecodeCache cache, string[] k) =>
        cache.Anim(k[0], k[1], k[2], k[3], k[4]);
}
