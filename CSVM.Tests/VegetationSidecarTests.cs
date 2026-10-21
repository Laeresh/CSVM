using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shipped per-chapter vegetation sidecar (<see cref="VegetationSidecar"/>). An unlisted model is
/// not vegetation, and a mistake is a warning rather than a crash. The lab's Save writes one chapter's
/// entry and nothing else, so the file round-trips.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VegetationSidecarTests
{
    private const string Small =
        "{\n  \"_about\": \"prose\",\n  \"C1\": {\n    \"kinds\": {\n      \"bush1.flt\": {}\n    }\n  },\n"
        + "  \"C3\": {\n    \"kinds\": {}\n  }\n}\n";

    private static string ShippedPath => Path.Combine(TestData.RepoRoot, "CSVM", "data", "vegetation.json");

    /// <summary>The shipped file reads clean and has an entry for every chapter with ground.</summary>
    [Fact]
    public void TheShippedFileReadsCleanAndNamesEveryChapterWithGround()
    {
        string shipped = File.ReadAllText(ShippedPath);
        var sidecar = VegetationSidecar.Parse(shipped);
        Assert.Empty(sidecar.Warnings);
        var keys = System.Text.Json.Nodes.JsonNode.Parse(shipped)!.AsObject().Select(p => p.Key).Where(k => !k.StartsWith('_'));
        Assert.Equal(VegetationSidecar.Chapters, keys);
        Assert.All(VegetationSidecar.Chapters, c => Assert.NotEmpty(sidecar.For(c).Kinds));
    }

    /// <summary>Every listed model is a decoration model the chapter's <c>templates.zrd</c> describes, so
    /// no name in the file is a guess.</summary>
    [ExtractedDataFact]
    public void EveryListedModelIsInItsChaptersTemplates()
    {
        var sidecar = VegetationSidecar.Parse(File.ReadAllText(ShippedPath));
        foreach (var chapter in VegetationSidecar.Chapters)
        {
            var spec = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));
            Assert.NotNull(spec);
            Assert.All(sidecar.For(chapter).Kinds, k => Assert.True(spec!.Find(k.Model) != null, $"{chapter} {k.Model}"));
        }
    }

    /// <summary>C2's buildings and parked cars are solid decorations, never listed.</summary>
    [Fact]
    public void NoSolidDecorationIsListed()
    {
        var c2 = VegetationSidecar.Parse(File.ReadAllText(ShippedPath)).For("C2");
        Assert.DoesNotContain(c2.Kinds, k => k.Model.StartsWith("filmbuild") || k.Model.StartsWith("resbuild") || k.Model.StartsWith("c_studebaker"));
    }

    /// <summary>A chapter the file lacks, or a model it does not list, keeps the faithful stamp.</summary>
    [Fact]
    public void AnUnlistedModelOrChapterIsNotVegetation()
    {
        var sidecar = VegetationSidecar.Parse(Small);
        Assert.Empty(sidecar.Warnings);
        Assert.Equal(new VegetationKind("bush1.flt"), sidecar.For("c1").Find("BUSH1.FLT"));
        Assert.Null(sidecar.For("C1").Find("firtree1.flt"));
        Assert.Same(VegetationEntry.Empty, sidecar.For("C4"));
        Assert.Same(VegetationEntry.Empty, sidecar.For("C5"));
    }

    [Fact]
    public void AnUnreadableFileWarnsAndListsNothing()
    {
        var sidecar = VegetationSidecar.Parse("{ not json");
        Assert.Single(sidecar.Warnings);
        Assert.Empty(sidecar.For("C1").Kinds);
    }

    /// <summary>C5 is city only, so an entry for it is refused rather than letting a block be listed.
    /// Each other mistake is one warning, and the entry's good values still apply.</summary>
    [Fact]
    public void AMistakeIsAWarningNotACrash()
    {
        var sidecar = VegetationSidecar.Parse(
            "{ \"C5\": { \"kinds\": { \"cb01.flt\": {} } }, \"C2\": { \"seed\": 1.5, \"colour\": 1, \"kinds\": {"
            + " \"spruce.flt\": { \"tint\": \"green\", \"jiter_uv\": 1, \"brightness\": 0.2 }, \"SPRUCE.FLT\": {} } } }");
        Assert.Equal(6, sidecar.Warnings.Count);
        Assert.Contains(sidecar.Warnings, w => w.Contains("'C5' is not a vegetation chapter"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'seed' is not an integer"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'colour' is not a vegetation field"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'tint' is not a number"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'jiter_uv' is not a kind field"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'SPRUCE.FLT' is listed twice"));
        Assert.Equal(new VegetationKind("spruce.flt", Brightness: 0.2f), Assert.Single(sidecar.For("C2").Kinds));
    }

    /// <summary>A value out of range is held at its edge, and the file is told so.</summary>
    [Fact]
    public void AnOutOfRangeValueIsClampedAndReported()
    {
        var sidecar = VegetationSidecar.Parse("{ \"C4\": { \"kinds\": { \"spruce.flt\": { \"jitter_uv\": 2, \"yaw_deg\": -10 } } } }");
        Assert.Equal(new VegetationKind("spruce.flt", JitterUv: 0.5f), sidecar.For("C4").Find("spruce.flt"));
        Assert.Equal(2, sidecar.Warnings.Count);
    }

    /// <summary>A set keeps only listed kinds and a pick needs a set, so the paint tool cannot reach a
    /// model that is not vegetation.</summary>
    [Fact]
    public void SetsHoldOnlyListedKindsAndPicksNeedASet()
    {
        var sidecar = VegetationSidecar.Parse(
            "{ \"C1\": { \"kinds\": { \"bush1.flt\": {}, \"bush2.flt\": {} },"
            + " \"sets\": { \"hedge\": [\"Bush1.flt\", \"resbuild01.flt\", 3] },"
            + " \"picks\": { \"terpat03\": \"hedge\", \"crop04\": \"crops\" } } }");
        Assert.Equal(3, sidecar.Warnings.Count);
        var entry = sidecar.For("C1");
        Assert.Equal(new[] { "bush1.flt" }, Assert.Single(entry.Sets).Models);
        Assert.Equal("hedge", entry.SetFor("TERPAT03")!.Name);
        Assert.Null(entry.SetFor("crop04"));
    }

    /// <summary>Save writes the chapter's entry with only nonzero values, in a fixed order. Every other key
    /// stays where it was, and the file reads back as the entry it wrote.</summary>
    [Fact]
    public void SaveWritesTheEntryAndRoundTrips()
    {
        var entry = new VegetationEntry
        {
            Seed = 7,
            Mask = "vegetation/C3.png",
            Kinds = new[] { new VegetationKind("palmtree1.flt", 0.1f, 0f, 0.05f, 0.123456f), new VegetationKind("palmtree2.flt") },
            Sets = new[] { new VegetationSet("palms", new[] { "palmtree1.flt", "palmtree2.flt" }) },
            Picks = new[] { new TexturePick("cliff1_toptrans", "palms") },
        };
        string written = VegetationSidecar.WithEntry(Small, "c3", entry);

        Assert.Contains(
            "\"C3\": {\n    \"seed\": 7,\n    \"mask\": \"vegetation/C3.png\",\n    \"kinds\": {\n      \"palmtree1.flt\": {\n"
            + "        \"jitter_uv\": 0.1,\n        \"tint\": 0.05,\n        \"brightness\": 0.1235\n      },\n      \"palmtree2.flt\": {}\n    },\n"
            + "    \"sets\": {\n      \"palms\": [\n        \"palmtree1.flt\",\n        \"palmtree2.flt\"\n      ]\n    },\n"
            + "    \"picks\": {\n      \"cliff1_toptrans\": \"palms\"\n    }\n  }",
            Lf(written));
        Assert.Equal(new[] { "_about", "C1", "C3" }, System.Text.Json.Nodes.JsonNode.Parse(written)!.AsObject().Select(p => p.Key));
        var back = VegetationSidecar.Parse(written).For("C3");
        Assert.Equal((7, "vegetation/C3.png"), (back.Seed, back.Mask));
        Assert.Equal(new VegetationKind("palmtree1.flt", 0.1f, 0f, 0.05f, 0.1235f), back.Find("palmtree1.flt"));
        Assert.Equal("palms", back.SetFor("cliff1_toptrans")!.Name);
    }

    /// <summary>Saving each chapter's loaded entry over the shipped file writes the shipped file again, so a
    /// Save changes only what the lab changed.</summary>
    [Fact]
    public void SavingWhatWasLoadedLeavesTheFileAsItIs()
    {
        string shipped = File.ReadAllText(ShippedPath);
        var loaded = VegetationSidecar.Parse(shipped);
        Assert.All(VegetationSidecar.Chapters, c => Assert.Equal(Lf(shipped), Lf(VegetationSidecar.WithEntry(shipped, c, loaded.For(c)))));
    }

    /// <summary>Save on disk: a file it creates, then a second chapter added beside the first.</summary>
    [Fact]
    public void SaveOnDiskCreatesThenUpdates()
    {
        string path = Path.Combine(TestData.TempDir(), "vegetation.json");
        VegetationSidecar.Save(path, "C2", new VegetationEntry { Kinds = new[] { new VegetationKind("spruce.flt", YawDeg: 30f) } });
        VegetationSidecar.Save(path, "C4", new VegetationEntry { Seed = 2 });
        var sidecar = VegetationSidecar.Parse(File.ReadAllText(path));
        Assert.Empty(sidecar.Warnings);
        Assert.Equal(30f, sidecar.For("C2").Find("spruce.flt")!.YawDeg);
        Assert.Equal(2, sidecar.For("C4").Seed);
    }

    /// <summary>A file Save cannot read is left as it was rather than replaced by the one chapter saved.</summary>
    [Theory]
    [InlineData("{ \"C1\": {}, ")]
    [InlineData("[ 1, 2 ]")]
    public void SaveRefusesAFileItCannotRead(string text)
    {
        string path = Path.Combine(TestData.TempDir(), "vegetation.json");
        File.WriteAllText(path, text);
        Assert.Throws<InvalidDataException>(() => VegetationSidecar.Save(path, "C2", VegetationEntry.Empty));
        Assert.Equal(text, File.ReadAllText(path));
    }

    /// <summary>A lab slider moves one field of one kind, held in the field's range, and leaves the rest
    /// of the entry as it was. The seed moves alone too. The entry Save then writes reads back as the
    /// edit, and the other chapters stay as shipped.</summary>
    [Fact]
    public void ALabEditChangesOneValueAndSavesAsEdited()
    {
        string shipped = File.ReadAllText(ShippedPath);
        var loaded = VegetationSidecar.Parse(shipped);
        var c1 = loaded.For("C1");
        var tint = VegetationKind.Fields.Single(f => f.Key == "tint");
        var yaw = VegetationKind.Fields.Single(f => f.Key == "yaw_deg");

        var firBefore = c1.Find("firtree1.flt");
        var edit = c1.With("FIRTREE1.FLT", tint, 0.3f).With("bush1.flt", yaw, 400f).WithSeed(42);
        Assert.Same(firBefore, c1.Find("firtree1.flt"));
        Assert.Equal(0.3f, edit.Find("firtree1.flt")!.Tint);
        Assert.Equal(180f, edit.Find("bush1.flt")!.YawDeg);
        Assert.Equal(42, edit.Seed);
        Assert.Equal(c1.Kinds.Select(k => k.Model), edit.Kinds.Select(k => k.Model));
        Assert.Equal(c1.Find("firtree2.flt"), edit.Find("firtree2.flt"));
        Assert.Equal(c1.Find("firtree1.flt")! with { Tint = 0.3f }, edit.Find("firtree1.flt"));
        Assert.Throws<System.ArgumentException>(() => c1.With("resbuild01.flt", tint, 0.1f));

        string written = VegetationSidecar.WithEntry(shipped, "C1", edit);
        var back = VegetationSidecar.Parse(written);
        Assert.Empty(back.Warnings);
        Assert.Equal(42, back.For("C1").Seed);
        Assert.Equal(edit.Kinds, back.For("C1").Kinds);
        Assert.All(VegetationSidecar.Chapters.Where(c => c != "C1"), c => Assert.Equal(loaded.For(c).Kinds, back.For(c).Kinds));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n");
}
