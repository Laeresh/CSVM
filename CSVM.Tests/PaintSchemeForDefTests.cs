using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The livery half of the identity split: a vehicle def's own authored scheme, resolved through
/// <c>kind_of</c> the way the original's spawn resolves it field by field (docs/org/paint.md).
/// This is what an AI aircraft wears when nothing overrides it.
/// </summary>
public class PaintSchemeForDefTests
{
    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    // Black Hat's Warhawk against Studio Security's Fury: two militias, two schemes, neither of them
    // the player's Fortune Hunters red.
    [ExtractedDataFact]
    public void AMilitiaDefWearsItsOwnPatternRatherThanTheDefault()
    {
        var blackHat = PaintScheme.ForDef(ZrdrPath, "bhatwarhawk");
        var studio = PaintScheme.ForDef(ZrdrPath, "secfury");

        Assert.NotNull(blackHat);
        Assert.NotNull(studio);
        Assert.NotEqual("player_fortune", blackHat!.Pattern);
        Assert.NotEqual(blackHat.Pattern, studio!.Pattern);
    }

    // The two ends of the same lookup: a scheme the catalog already knows must come back identical
    // when read off the def that authors it, or the AI would fly a different livery from the one the
    // paint UI offers under that name.
    [ExtractedDataFact]
    public void ADefsSchemeMatchesTheCatalogEntryForItsPattern()
    {
        var scheme = PaintScheme.ForDef(ZrdrPath, "bhatwarhawk");
        Assert.NotNull(scheme);

        var catalog = PaintScheme.LoadCatalog(ZrdrPath);
        var known = catalog.Find(s => s.Pattern == scheme!.Pattern);
        Assert.NotNull(known);
        Assert.Equal(known!.Color1, scheme!.Color1);
        Assert.Equal(known.Color2, scheme.Color2);
        Assert.Equal(known.Color3, scheme.Color3);
    }

    // A def with no paint keys anywhere in its chain has no scheme of its own, which is a fallback
    // to the livery picker rather than a parse failure.
    [ExtractedDataFact]
    public void ADefWithNoAuthoredPatternResolvesToNothing()
    {
        Assert.Null(PaintScheme.ForDef(ZrdrPath, "basic_airplane"));
    }
}
