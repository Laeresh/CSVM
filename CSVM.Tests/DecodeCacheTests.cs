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

        var first = Anim(cache, "C1", "IA1");
        var second = Anim(cache, "c1", "ia1");

        Assert.Same(first, second);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    [Theory]
    [InlineData("C3", "IA1")]     // a different chapter
    [InlineData("C1", "M04")]     // a different mission
    [InlineData("C1I", "A1")]     // the same letters split elsewhere
    public void ChangingAnyPartOfTheAnimKeyMisses(string chapter, string mission)
    {
        var cache = new DecodeCache();
        var baseline = Anim(cache, "C1", "IA1");

        var other = Anim(cache, chapter, mission);

        Assert.NotSame(baseline, other);
        Assert.Equal(2, cache.Misses);
        Assert.Equal(0, cache.Hits);
    }

    [Fact]
    public void RepeatingOneGamezChapterReturnsTheSameDocumentAndADifferentChapterMisses()
    {
        var cache = new DecodeCache();

        var first = cache.Gamez("C1", () => new GameZ());
        var second = cache.Gamez("c1", () => new GameZ());
        var other = cache.Gamez("C3", () => new GameZ());

        Assert.Same(first, second);
        Assert.NotSame(first, other);
        Assert.Equal(2, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    // An empty program per miss: the key alone is under test, so no install and no decode.
    private static AnimProgram Anim(DecodeCache cache, string chapter, string mission) =>
        cache.Anim(chapter, mission, () => AnimProgram.FromDefinitions(System.Array.Empty<AnimDefinition>()));
}
