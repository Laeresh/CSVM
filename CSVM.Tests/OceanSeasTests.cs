using System.IO;
using System.Linq;
using CSVM.Effects;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shipped per-chapter seas (<see cref="OceanSeas"/>). A missing entry or field takes the default,
/// and a mistake is a warning rather than a crash. The lab's Save writes one chapter's differing fields
/// and nothing else, in a stable order, so the file round-trips.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class OceanSeasTests
{
    private static string ShippedPath => Path.Combine(TestData.RepoRoot, "CSVM", "data", "ocean_seas.json");

    /// <summary>The shipped file names every sea chapter, saves nothing and reads clean, so every
    /// chapter draws the ocean's tune.</summary>
    [Fact]
    public void TheShippedFileDrawsEveryChapterAtTheDefaults()
    {
        var seas = OceanSeas.Parse(File.ReadAllText(ShippedPath));
        Assert.Empty(seas.Warnings);
        Assert.All(OceanSeas.Chapters, c => Assert.Equal(SeaState.Default, seas.For(c)));
        Assert.Equal(SeaState.Default, seas.For("C4"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"_about\": \"prose\" }")]
    [InlineData("{ \"C1B\": {} }")]
    public void AnEmptyFileOrEntryTakesTheDefaults(string json)
    {
        var seas = OceanSeas.Parse(json);
        Assert.Empty(seas.Warnings);
        Assert.Equal(SeaState.Default, seas.For("C1B"));
    }

    [Fact]
    public void AnUnreadableFileWarnsAndTakesTheDefaults()
    {
        var seas = OceanSeas.Parse("{ not json");
        Assert.Single(seas.Warnings);
        Assert.Equal(SeaState.Default, seas.For("C1"));
    }

    /// <summary>A partial entry sets its own fields and leaves the rest, and the other chapters, alone.</summary>
    [Fact]
    public void APartialEntrySetsOnlyItsFields()
    {
        var seas = OceanSeas.Parse("{ \"c1b\": { \"height\": 1.4, \"foam_strength\": 0.2 } }");
        Assert.Empty(seas.Warnings);
        Assert.Equal(SeaState.Default with { Height = 1.4f, FoamStrength = 0.2f }, seas.For("C1B"));
        Assert.Equal(SeaState.Default, seas.For("C1"));
    }

    /// <summary>An unknown field, an unknown chapter and a value that is not a number are each one
    /// warning; the entry's good fields still apply.</summary>
    [Fact]
    public void AMistakeIsAWarningNotACrash()
    {
        var seas = OceanSeas.Parse("{ \"C2\": { \"hieght\": 2, \"length\": \"long\", \"wind\": 45 }, \"C4\": {}, \"C3\": 5 }");
        Assert.Equal(4, seas.Warnings.Count);
        Assert.Contains(seas.Warnings, w => w.Contains("'hieght' is not a sea field"));
        Assert.Contains(seas.Warnings, w => w.Contains("'length' is not a number"));
        Assert.Contains(seas.Warnings, w => w.Contains("'C4' is not a sea chapter"));
        Assert.Contains(seas.Warnings, w => w.Contains("C3 is not an object"));
        Assert.Equal(SeaState.Default with { WindDeg = 45f }, seas.For("C2"));
    }

    /// <summary>A value out of range is held at its edge, and the file is told so.</summary>
    [Fact]
    public void AnOutOfRangeValueIsClampedAndReported()
    {
        var seas = OceanSeas.Parse("{ \"C5\": { \"length\": 3, \"swell_full\": 500 } }");
        Assert.Equal((2f, 160f), (seas.For("C5").Length, seas.For("C5").SwellFull));
        Assert.Equal(2, seas.Warnings.Count);
    }

    /// <summary>Save writes the chapter's differing fields alone, in the fields' order. Every other key
    /// stays where it was, and the file reads back as the sea it wrote.</summary>
    [Fact]
    public void SaveWritesOnlyTheDifferingFieldsAndRoundTrips()
    {
        string shipped = File.ReadAllText(ShippedPath);
        var sea = SeaState.Default with { FoamStrength = 0.2f, Height = 1.4f, Length = 1.5f };
        string written = OceanSeas.WithEntry(shipped, "c1b", sea);

        Assert.Contains("\"C1B\": {\n    \"height\": 1.4,\n    \"length\": 1.5,\n    \"foam_strength\": 0.2\n  }", Lf(written));
        var keys = System.Text.Json.Nodes.JsonNode.Parse(written)!.AsObject().Select(p => p.Key).ToArray();
        Assert.Equal(new[] { "_about", "C1", "C1B", "C1C", "C2", "C2B", "C3", "C5" }, keys);
        var back = OceanSeas.Parse(written);
        Assert.Empty(back.Warnings);
        Assert.Equal(sea, back.For("C1B"));
        Assert.Equal(SeaState.Default, back.For("C2"));
    }

    /// <summary>Saving the defaults over the shipped file writes the shipped file again, so a Save
    /// changes only what the lab changed.</summary>
    [Fact]
    public void SavingTheDefaultsLeavesTheShippedFileAsItIs()
    {
        string shipped = File.ReadAllText(ShippedPath);
        Assert.Equal(Lf(shipped), Lf(OceanSeas.WithEntry(shipped, "C3", SeaState.Default)));
    }

    /// <summary>Save on disk: a file it creates, then a second chapter added beside the first.</summary>
    [Fact]
    public void SaveOnDiskCreatesThenUpdates()
    {
        string path = Path.Combine(TestData.TempDir(), "ocean_seas.json");
        Assert.Equal(1, OceanSeas.Save(path, "C2", SeaState.Default with { WindDeg = 90f }));
        Assert.Equal(2, OceanSeas.Save(path, "C3", SeaState.Default with { Length = 1.2f, FoamCover = 0.2f }));
        var seas = OceanSeas.Parse(File.ReadAllText(path));
        Assert.Equal(90f, seas.For("C2").WindDeg);
        Assert.Equal((1.2f, 0.2f), (seas.For("C3").Length, seas.For("C3").FoamCover));
    }

    /// <summary>A comment added by hand is read past on Save as on load, so every other chapter and the
    /// prose key survive the write.</summary>
    [Fact]
    public void SaveReadsPastAComment()
    {
        string commented = "// tuned by hand\n" + File.ReadAllText(ShippedPath).Replace("\"C2\":", "/* the next sea */ \"C2\":");
        Assert.Empty(OceanSeas.Parse(commented).Warnings);
        string written = OceanSeas.WithEntry(commented, "C5", SeaState.Default with { WindDeg = 90f });

        var keys = System.Text.Json.Nodes.JsonNode.Parse(written)!.AsObject().Select(p => p.Key).ToArray();
        Assert.Equal(new[] { "_about", "C1", "C1B", "C1C", "C2", "C2B", "C3", "C5" }, keys);
        Assert.Equal(Lf(File.ReadAllText(ShippedPath)), Lf(OceanSeas.WithEntry(written, "C5", SeaState.Default)));
    }

    /// <summary>A file Save cannot read is left as it was rather than replaced by the one chapter saved.</summary>
    [Theory]
    [InlineData("{ \"C1\": {}, ")]
    [InlineData("[ 1, 2 ]")]
    public void SaveRefusesAFileItCannotRead(string text)
    {
        string path = Path.Combine(TestData.TempDir(), "ocean_seas.json");
        File.WriteAllText(path, text);
        Assert.Throws<InvalidDataException>(() => OceanSeas.Save(path, "C2", SeaState.Default with { WindDeg = 90f }));
        Assert.Equal(text, File.ReadAllText(path));
    }

    /// <summary>A height clamped to the fold limit is written at a precision that stays inside it. The
    /// saved file then loads with no clamp warning, as the sea the lab held.</summary>
    [Theory]
    [InlineData(0.55f)]
    [InlineData(0.6f)]
    [InlineData(0.7f)]
    [InlineData(0.9f)]
    [InlineData(0.77f)]
    public void AHeightAtTheFoldLimitRoundTripsWithoutAWarning(float sharpness)
    {
        var sea = (SeaState.Default with { Sharpness = sharpness, Height = 2f }).Clamped();
        Assert.Equal(SeaState.FoldLimit / sharpness, sea.Height);
        var back = OceanSeas.Parse(OceanSeas.WithEntry(null, "C1", sea));
        Assert.Empty(back.Warnings);
        Assert.Equal(sea.Written(), back.For("C1"));
        Assert.True(back.For("C1").Height * sharpness <= SeaState.FoldLimit);
        Assert.Equal(sea.Height, back.For("C1").Height, 3);
    }

    /// <summary>A coast ramp held at its narrowest reads back without a clamp warning as well.</summary>
    [Fact]
    public void ARampAtItsNarrowestRoundTripsWithoutAWarning()
    {
        var sea = (SeaState.Default with { SwellFull = 100.12345f, SwellFrom = 150f, LookFull = 70.00007f, LookFrom = 80f }).Clamped();
        var back = OceanSeas.Parse(OceanSeas.WithEntry(null, "C3", sea));
        Assert.Empty(back.Warnings);
        Assert.Equal(sea.Written(), back.For("C3"));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n");
}
