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
}
