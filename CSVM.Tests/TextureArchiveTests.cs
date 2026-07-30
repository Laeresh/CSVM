using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The parts of the texture archive that are name arithmetic rather than pixels: the decal
/// numbering (<c>docs/formats/paint.md</c>) and the known-absent list. Decoding a texture needs
/// Godot's <c>Image</c>, so alpha classification and the truncated-name resolution stay with the
/// in-engine suites; only the index is exercised here, over a directory of empty files whose
/// names are the whole input.
/// </summary>
public class TextureArchiveTests
{
    [Fact]
    public void DecalIndexFindsTheZeroPaddedTwoDigitName()
    {
        using var archive = new TextureArchive(DecalDir());
        Assert.Equal("21probe_star", archive.FindByDecalIndex(21));
        Assert.Equal("07probe_mark", archive.FindByDecalIndex(7));
        Assert.Equal("00probe_zero", archive.FindByDecalIndex(0));
    }

    [Fact]
    public void AThirdDigitMeansItIsNotADecalAndTheLodTwinIsSkipped()
    {
        using var archive = new TextureArchive(DecalDir());
        // "213probe_notadecal" begins with "21" but its third character is a digit.
        Assert.NotEqual("213probe_notadecal", archive.FindByDecalIndex(21));
        Assert.NotEqual("21probe_star_1", archive.FindByDecalIndex(21));
    }

    [Fact]
    public void AnIndexWithNoDecalOrOutOfRangeGivesNull()
    {
        using var archive = new TextureArchive(DecalDir());
        Assert.Null(archive.FindByDecalIndex(42));
        Assert.Null(archive.FindByDecalIndex(-1));
        Assert.Null(archive.FindByDecalIndex(100));
    }

    [Fact]
    public void TexturesTheRetailDataItselfLacksAreFlaggedAsKnownAbsent()
    {
        // These render neutral instead of debug magenta; anything else missing stays loud.
        Assert.True(TextureArchive.IsKnownAbsent("pir_spinner"));
        Assert.True(TextureArchive.IsKnownAbsent("pir_spinner.t"));
        Assert.True(TextureArchive.IsKnownAbsent("barngrill.tif"));
        Assert.False(TextureArchive.IsKnownAbsent("probe_plain"));
    }

    [Fact]
    public void AnArchiveOverAnEmptyDirectoryHasNoMissesYet()
    {
        using var archive = new TextureArchive(TestData.TempDir());
        Assert.Empty(archive.MissingTextures);
    }

    // The census colour is name arithmetic too — a hash, no engine — and the whole instrument
    // rests on it giving the same answer in every process. These lock that down.

    [Fact]
    public void ACensusColourIsAFunctionOfTheNameAlone()
    {
        // Recorded values, not a recomputation: a self-comparison would pass on any hash and could
        // never catch the map silently changing under a refactor.
        Assert.Equal("a1ffa0", Hex(TextureDropIn.ColorForName("lkzepskin")));
        Assert.Equal("ddffb6", Hex(TextureDropIn.ColorForName("firtree1")));
        Assert.Equal("c9d0ff", Hex(TextureDropIn.ColorForName("grass1")));
        // Case and extension are not part of the identity: the material's stored name is a
        // truncated, arbitrarily-cased version of the PNG's.
        Assert.Equal(TextureDropIn.ColorForName("lkzepskin"), TextureDropIn.ColorForName("LKZepSkin"));
    }

    [Fact]
    public void EveryCensusColourIsFullBrightnessAndFarFromGrey()
    {
        for (int i = 0; i < 1000; i++)
        {
            var c = TextureDropIn.ColorForName($"probe_texture_{i}");
            Assert.True(c.R8 == 255 || c.G8 == 255 || c.B8 == 255, $"probe_texture_{i} is not full brightness: {Hex(c)}");
            // Greyness is a LINEAR-space property, because that is where the classifier measures:
            // one channel must sit at most 0.9 of the brightest, which is 0.1 of chromaticity —
            // twice the whole match tolerance away from white.
            var lin = c.SrgbToLinear();
            float max = System.Math.Max(lin.R, System.Math.Max(lin.G, lin.B));
            float min = System.Math.Min(lin.R, System.Math.Min(lin.G, lin.B));
            Assert.True(min / max <= 0.91f, $"probe_texture_{i} is nearly grey in linear space: {Hex(c)}");
        }
    }

    [Fact]
    public void CollisionsAreRareEnoughToBeReportableRatherThanRoutine()
    {
        // Eight bits per channel leave ~200k reachable colours and the ratios are drawn uniformly
        // in linear space, which uses the bright end of each byte sparsely — so a thousand names
        // collide a handful of times (8 measured, and 4 across all 882 C1 textures). The engine
        // warns per collision and the map counts them; this locks the rate, which a palette shrink
        // would blow straight through.
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 1000; i++)
        {
            seen.Add(Hex(TextureDropIn.ColorForName($"probe_texture_{i}")));
        }
        Assert.True(seen.Count >= 985, $"only {seen.Count} distinct colours for 1000 names");
    }

    private static string DecalDir()
    {
        var dir = TestData.TempDir();
        foreach (var name in new[]
                 {
                     "00probe_zero.png",
                     "07probe_mark.png",
                     "21probe_star.png",
                     "21probe_star_1.png",   // the half-size LOD twin, not the decal
                     "213probe_notadecal.png",
                     "probe_plain.png",
                 })
        {
            File.WriteAllBytes(Path.Combine(dir, name), System.Array.Empty<byte>());
        }
        return dir;
    }

    private static string Hex(Godot.Color c) => $"{c.R8:x2}{c.G8:x2}{c.B8:x2}";
}
