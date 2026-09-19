using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Which `.BM` a model texture is painted from. The original pairs the two in a static
/// per-airframe table rather than deriving one name from the other, so a pair is free to differ,
/// and one of them does: the Devastator's fuselage sides (<c>docs/formats/paint.md</c>). These
/// cases pin that one alias and the literal match everywhere else, because a lookup that silently
/// misses leaves the part wearing its shipped skin and paints nothing.
/// </summary>
public class PaintSkinAliasTests
{
    [Fact]
    public void TheDevastatorsFuselageSidesNameTheNumberedBitmap()
    {
        Assert.Equal("dev_fusalage1", PlanePainter.SkinNameFor("dev_fusalage"));
    }

    // Every other pair in the eleven tables is name-identical, which is why a literal lookup
    // worked everywhere but the one aircraft.
    [Fact]
    public void EveryOtherPartKeepsItsOwnName()
    {
        Assert.Equal("dev_wing", PlanePainter.SkinNameFor("dev_wing"));
        Assert.Equal("blo_wing", PlanePainter.SkinNameFor("blo_wing"));
        Assert.Equal("fur_fusalage1", PlanePainter.SkinNameFor("fur_fusalage1"));
        Assert.Equal("war_fusalage1", PlanePainter.SkinNameFor("war_fusalage1"));
        Assert.Equal("kes_fuselage", PlanePainter.SkinNameFor("kes_fuselage"));
    }

    // The fuselage top is a separate 64x64 texture whose UVs tile past U = 4, and no pattern
    // ships a `.BM` for it, so it keeps its shipped ZBD skin.
    [Fact]
    public void TheFuselageTopIsNotAliasedOntoTheSideBitmap()
    {
        Assert.Equal("dev_fusalagetop", PlanePainter.SkinNameFor("dev_fusalagetop"));
    }

    [ExtractedDataFact]
    public void TheAliasResolvesInBothPatternsTheDevastatorCarries()
    {
        var library = Library();
        if (library == null)
        {
            return;
        }

        foreach (string pattern in new[] { "FORTUNE", "CCCP" })
        {
            Assert.Null(library.Skin(pattern, "dev_fusalage"));
            Assert.NotNull(library.Skin(pattern, PlanePainter.SkinNameFor("dev_fusalage")));
            Assert.Null(library.Skin(pattern, PlanePainter.SkinNameFor("dev_fusalagetop")));
            Assert.NotNull(library.Skin(pattern, PlanePainter.SkinNameFor("dev_wing")));
        }
    }

    // Another airframe's lookup goes through the same call and must be untouched by the alias.
    [ExtractedDataFact]
    public void AnotherAircraftStillResolvesLiterally()
    {
        var library = Library();
        if (library == null)
        {
            return;
        }

        Assert.NotNull(library.Skin("FORTUNE", PlanePainter.SkinNameFor("blo_wing")));
        Assert.NotNull(library.Skin("FORTUNE", PlanePainter.SkinNameFor("fur_fusalage1")));
    }

    // Null rather than an empty library when the UI archive has not been extracted, so a partial
    // extraction reads as "not checked" at the call site instead of as a pass.
    private static PatternLibrary? Library()
    {
        string rof = Path.Combine(TestData.ExtractedRoot!, "rof");
        if (!Directory.Exists(Path.Combine(rof, "ASSETS", "GRAPHICS", "CCCP")))
        {
            return null;
        }
        var library = PatternLibrary.Load(rof);
        return library.IsEmpty ? null : library;
    }
}
