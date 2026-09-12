using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The chapter's authored mip LOD bias (<see cref="TextureArchive.MipBias"/>): the original's
/// <c>MipBias</c> script command, which lands as one device render state for the whole chapter.
/// Decode: docs/org/textures.md. The grammar half runs on hand-authored script lines for forms no
/// chapter ships; the data half pins all eight chapters, because "only C5 authors one" is a claim
/// only a whole-install sweep can make.
/// </summary>
public class MipBiasTests
{
    /// <summary>Per chapter, the bias a retail run applies. C5 is the only chapter whose
    /// <c>adjust.gw</c> carries the command; every other chapter runs at the device default.</summary>
    public static TheoryData<string, float> ChapterBias => new()
    {
        { "C1", 0f },
        { "C1B", 0f },
        { "C1C", 0f },
        { "C2", 0f },
        { "C2B", 0f },
        { "C3", 0f },
        { "C4", 0f },
        { "C5", -0.8f },
    };

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterBias))]
    public void EveryChaptersBiasIsTheOneItsAdjustScriptAuthors(string chapter, float expected)
    {
        Assert.Equal((double)expected, TextureArchive.MipBias(InterpPath(), chapter), 4);
    }

    [Fact]
    public void AChapterWithNoCommandKeepsTheDeviceDefault()
    {
        Assert.Equal(0f, TextureArchive.MipBias(Interp("c9", "AddClutterTemplates cblock1"), "C9"));
    }

    [Fact]
    public void TheLastCommandInTheScriptWins()
    {
        Assert.Equal(-0.5, TextureArchive.MipBias(Interp("c9", "MipBias -0.8", "MipBias -0.5"), "C9"), 4);
    }

    /// <summary>The original clamps the argument into [-1, 1] before it reaches the device, so a
    /// script asking for more gets the bound, not the number it wrote.</summary>
    [Fact]
    public void AnOutOfRangeArgumentIsClampedTheWayTheCommandClampsIt()
    {
        Assert.Equal(-1f, TextureArchive.MipBias(Interp("c9", "MipBias -4.0"), "C9"));
        Assert.Equal(1f, TextureArchive.MipBias(Interp("c9", "MipBias 4.0"), "C9"));
    }

    /// <summary>⚠ <c>load.gw</c> is the data-compile path and must stay unread: it sources the
    /// terrain <c>.flt</c> a retail install does not ship, so its command never runs.</summary>
    [Fact]
    public void TheCompileOnlyLoadScriptIsNotRead()
    {
        string dir = TestData.TempDir();
        string path = Path.Combine(dir, "interp.json");
        File.WriteAllText(path,
            "[{\"name\":\"support\\\\c9\\\\load.gw\",\"lines\":[\"MipBias -1.0\"]},"
            + "{\"name\":\"support\\\\c9\\\\adjust.gw\",\"lines\":[\"AddClutterTemplates x\"]}]");
        Assert.Equal(0f, TextureArchive.MipBias(path, "C9"));
    }

    [Fact]
    public void AnAbsentExtractionReadsAsNoBiasRatherThanThrowing()
    {
        Assert.Equal(0f, TextureArchive.MipBias(Path.Combine(TestData.TempDir(), "gone.json"), "C5"));
    }

    private static string InterpPath() =>
        Path.Combine(TestData.DataRoot ?? TestData.RepoRoot, "extracted", "interp.json");

    // One adjust.gw for the named chapter, holding exactly the lines the case is about.
    private static string Interp(string chapter, params string[] lines)
    {
        string path = Path.Combine(TestData.TempDir(), "interp.json");
        File.WriteAllText(path,
            $"[{{\"name\":\"support\\\\{chapter}\\\\adjust.gw\",\"lines\":[\""
            + string.Join("\",\"", lines) + "\"]}]");
        return path;
    }
}
