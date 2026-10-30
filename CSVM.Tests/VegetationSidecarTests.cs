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

    /// <summary>A pick gives a texture the listed kinds it names, reusing a set with those members, and an
    /// unlisted model never reaches a set. No listed kind removes the pick, and Save keeps picks and mask.</summary>
    [Fact]
    public void APickGivesListedKindsAndSavesWithTheMask()
    {
        const string text = "{\n  \"C2\": {\n    \"kinds\": { \"brush1.flt\": {}, \"brush2.flt\": {}, \"palmtree1.flt\": {} },\n"
            + "    \"sets\": { \"scrub\": [\"brush2.flt\", \"brush1.flt\"] },\n    \"picks\": {}\n  }\n}\n";
        var entry = VegetationSidecar.Parse(text).For("C2");

        var reused = entry.WithPick("grass1", new[] { "BRUSH1.flt", "brush2.flt", "resbuild01.flt" });
        Assert.Equal("scrub", Assert.Single(reused.Picks).Set);
        Assert.Single(reused.Sets);

        var added = reused.WithPick("sand", new[] { "palmtree1.flt", "c_studebaker1.flt" });
        Assert.Equal(new[] { "palmtree1.flt" }, added.SetFor("sand")!.Models);
        Assert.Equal("palmtree1.flt", added.SetFor("sand")!.Name);
        Assert.Equal("scrub", added.SetFor("grass1")!.Name);

        var unpicked = added.WithPick("grass1", new[] { "resbuild01.flt" });
        Assert.Null(unpicked.SetFor("grass1"));
        Assert.NotNull(unpicked.SetFor("sand"));

        var masked = added.WithMask(VegetationMask.PathFor("C2"));
        string saved = VegetationSidecar.WithEntry(text, "C2", masked);
        var back = VegetationSidecar.Parse(saved);
        Assert.Empty(back.Warnings);
        Assert.Equal("vegetation/C2.bin", back.For("C2").Mask);
        Assert.Equal("palmtree1.flt", back.For("C2").SetFor("sand")!.Models[0]);
        Assert.Equal(2, back.For("C2").Picks.Count);
    }

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
            var spec = ClutterTemplateSpec.Load(TestZrdr.Chapter(chapter));
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

    /// <summary>A keep is 1 when absent and held in 0 to 1; the writer leaves out a keep of 1 and writes any other.</summary>
    [Fact]
    public void AKeepReadsClampsAndWritesAroundItsDefaultOfOne()
    {
        var sidecar = VegetationSidecar.Parse("{ \"C2\": { \"kinds\": { \"brush1.flt\": { \"keep\": 0.6 }, \"brush2.flt\": { \"keep\": 1.5 },"
            + " \"brush3.flt\": { \"keep\": -0.2 }, \"brush4.flt\": {}, \"spruce.flt\": { \"keep\": \"most\" } } } }");
        var c2 = sidecar.For("C2");
        Assert.Equal(new[] { 0.6f, 1f, 0f, 1f, 1f }, c2.Kinds.Select(k => k.Keep));
        Assert.Equal(3, sidecar.Warnings.Count);
        Assert.Contains(sidecar.Warnings, w => w.Contains("'keep' is not a number, it takes 1"));

        string written = Lf(VegetationSidecar.WithEntry(Small, "C2", c2));
        Assert.Contains("\"brush1.flt\": {\n        \"keep\": 0.6\n      }", written);
        Assert.Contains("\"brush2.flt\": {}", written);
        Assert.Contains("\"brush3.flt\": {\n        \"keep\": 0\n      }", written);
        Assert.Equal(c2.Kinds, VegetationSidecar.Parse(written).For("C2").Kinds);
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

    /// <summary>A set written as an object carries its density and forest flag, clamped and checked, and the lab's
    /// set edit writes it back that way. A set at its defaults stays a bare list.</summary>
    [Fact]
    public void ASetCarriesItsDensityAndForestFlagThroughSave()
    {
        var sidecar = VegetationSidecar.Parse(
            "{ \"C1\": { \"kinds\": { \"bush1.flt\": {}, \"firtree1.flt\": {} },"
            + " \"sets\": { \"hedge\": [\"bush1.flt\"], \"wood\": { \"kinds\": [\"firtree1.flt\"], \"density\": 12.5, \"mass\": true },"
            + " \"thick\": { \"kinds\": [\"bush1.flt\"], \"density\": 500, \"shade\": 1 }, \"none\": { \"density\": 1 } },"
            + " \"picks\": { \"terpat03\": \"hedge\", \"grass1\": \"wood\" } } }");
        Assert.Equal(3, sidecar.Warnings.Count);
        Assert.Contains(sidecar.Warnings, w => w.Contains("'density' 500 is held at 100"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("'shade' is not a set field"));
        Assert.Contains(sidecar.Warnings, w => w.Contains("set 'none' is neither a list nor an object"));
        var entry = sidecar.For("C1");
        Assert.Equal((VegetationSet.DefaultDensity, false), (entry.SetFor("terpat03")!.Density, entry.SetFor("terpat03")!.Mass));
        Assert.Equal((12.5f, true), (entry.SetFor("grass1")!.Density, entry.SetFor("grass1")!.Mass));
        Assert.Equal(VegetationSet.MaxDensity, entry.Sets.Single(s => s.Name == "thick").Density);

        var edited = entry.WithSet("HEDGE", 3.25f, mass: true).WithSet("wood", -4f, mass: false);
        Assert.Equal((3.25f, true), (edited.SetFor("terpat03")!.Density, edited.SetFor("terpat03")!.Mass));
        Assert.Equal((0f, false), (edited.SetFor("grass1")!.Density, edited.SetFor("grass1")!.Mass));
        Assert.Throws<System.ArgumentException>(() => entry.WithSet("crops", 1f, false));
        Assert.Equal(entry.Picks, edited.Picks);

        string written = Lf(VegetationSidecar.WithEntry(null, "C1", edited.WithSet("thick", VegetationSet.DefaultDensity, false)));
        Assert.Contains("\"hedge\": {\n        \"kinds\": [\n          \"bush1.flt\"\n        ],\n        \"density\": 3.25,\n        \"mass\": true\n      }", written);
        Assert.Contains("\"thick\": [\n        \"bush1.flt\"\n      ]", written);
        var back = VegetationSidecar.Parse(written);
        Assert.Empty(back.Warnings);
        Assert.Equal((3.25f, true), (back.For("C1").SetFor("terpat03")!.Density, back.For("C1").SetFor("terpat03")!.Mass));
        Assert.Equal(0f, back.For("C1").SetFor("grass1")!.Density);
    }

    /// <summary>C3 ships its cliff-top and side transitions picked to its three palms at twice the beach stand's
    /// density, not as forest. The shoreline transition stays unpicked, so no palm stands in the reef water.</summary>
    [Fact]
    public void TheShippedFilePicksC3sTransitionsToItsPalms()
    {
        var c3 = VegetationSidecar.Parse(File.ReadAllText(ShippedPath)).For("C3");
        foreach (string texture in new[] { "cliff1_toptrans.tif", "cliff1_sidetrans.tif" })
        {
            var set = VegetationScatter.Stamped(c3, texture);
            Assert.NotNull(set);
            Assert.Equal(new[] { "palmtree1.flt", "palmtree2.flt", "palmtree3.flt" }, set!.Models);
            Assert.Equal((15.7f, false), (set.Density, set.Mass));
        }
        Assert.All(new[] { "forest1.tif", "cliff1_sandtrans.tif", "cliff1_watertrans2.t" }, t => Assert.Null(VegetationScatter.Stamped(c3, t)));
    }

    /// <summary>C1B's bare rock tops take sparse bushes, not forest. C4's grass-to-rock transitions take a forest
    /// treeline of its templates' three firs, thinner than the templated forest. C2 keeps 60% of its original brush
    /// and scatters brush on the same two plains in place of the rest; its palms keep every stamp.</summary>
    [Fact]
    public void TheShippedFilePicksC1BsRockTopsC4sTransitionsAndC2sPlain()
    {
        var sidecar = VegetationSidecar.Parse(File.ReadAllText(ShippedPath));
        foreach (string texture in new[] { "rocktop.tif", "rocktop128.tif" })
        {
            var set = VegetationScatter.Stamped(sidecar.For("C1B"), texture)!;
            Assert.Equal(new[] { "bush1.flt", "bush2.flt" }, set.Models);
            Assert.Equal((3f, false), (set.Density, set.Mass));
        }
        foreach (string texture in new[] { "cliff01-trans1.tif", "cliff01-trans3.tif" })
        {
            var set = VegetationScatter.Stamped(sidecar.For("C4"), texture)!;
            Assert.Equal(new[] { "spruce.flt", "dougfirtree1.flt", "firtree1.flt" }, set.Models);
            Assert.Equal((3f, true), (set.Density, set.Mass));
        }
        Assert.Null(VegetationScatter.Stamped(sidecar.For("C4"), "terpat01.tif"));
        var c2 = sidecar.For("C2");
        Assert.Equal(new[] { "terpat04.tif", "terpat04-128.tif" }, c2.Picks.Select(p => p.Texture));
        foreach (var pick in c2.Picks)
        {
            var set = VegetationScatter.Stamped(c2, pick.Texture)!;
            Assert.All(set.Models, m => Assert.StartsWith("brush", m));
            Assert.False(set.Mass);
        }
        Assert.All(c2.Kinds, k => Assert.Equal(k.Model.StartsWith("brush") ? 0.6f : 1f, k.Keep));
    }

    /// <summary>The ground cover: C1's fields grow crops and its meadows grass, every texel. C4's grass-to-rock
    /// transitions grow grass on their saturated texels alone. Its fir ground grows grass as cards only, its pale paths
    /// bare under a value ceiling. C1B, C2 and C3 grow none.</summary>
    [Fact]
    public void TheShippedFileGivesC1sFieldsAndC4sTransitionsTheirCover()
    {
        var sidecar = VegetationSidecar.Parse(File.ReadAllText(ShippedPath));
        var c1 = sidecar.For("C1");
        Assert.Equal(
            new[] { ("crop04", GroundCover.Cover.Crop), ("crop05", GroundCover.Cover.Crop), ("terpat03", GroundCover.Cover.Grass),
                ("terpat03b", GroundCover.Cover.Grass), ("grass1", GroundCover.Cover.Grass) },
            c1.Cover.Select(c => (Path.GetFileNameWithoutExtension(c.Texture), c.Grows)));
        Assert.All(c1.Cover, c => Assert.Equal(0f, c.MinSaturation));
        Assert.Equal(GroundCover.Cover.Crop, c1.CoverFor("CROP05")?.Grows);
        Assert.All(new[] { "terpat02.tif", "tarmac01.tif", "terpat03_trans1.tif" }, t => Assert.Null(c1.CoverFor(t)));
        var c4 = sidecar.For("C4");
        Assert.Equal(new[] { "cliff01-trans1.tif", "cliff01-trans3.tif", "terpat01.tif" }, c4.Cover.Select(c => c.Texture));
        Assert.All(c4.Cover, c => Assert.Equal((GroundCover.Cover.Grass, 0.2f), (c.Grows, c.MinSaturation)));
        Assert.All(c4.Cover.Take(2), c => Assert.Equal((1f, false), (c.MaxValue, c.CardsOnly)));
        Assert.Equal((0.55f, true), (c4.CoverFor("terpat01")!.MaxValue, c4.CoverFor("terpat01")!.CardsOnly));
        Assert.All(c1.Cover, c => Assert.Equal((1f, false), (c.MaxValue, c.CardsOnly)));
        Assert.All(new[] { "C1B", "C2", "C3" }, ch => Assert.Empty(sidecar.For(ch).Cover));
    }

    /// <summary>A cover pick reads as a bare word or as an object. The object holds a saturation floor and a value
    /// ceiling, each held to 0..1, and a cards-only choice. A word that is neither grass nor crop, an unknown field and an unknown covers
    /// word warn. Save writes each back in the form it read.</summary>
    [Fact]
    public void ACoverPickReadsBareOrGatedAndRoundTrips()
    {
        const string text = "{\n  \"C4\": {\n    \"kinds\": {},\n    \"cover\": {\n      \"a.tif\": \"GRASS\",\n"
            + "      \"b.tif\": { \"grows\": \"crop\", \"min_saturation\": 0.25 },\n      \"c.tif\": \"reeds\",\n"
            + "      \"d.tif\": { \"grows\": \"grass\", \"min_saturation\": 3, \"height\": 2 },\n"
            + "      \"e.tif\": { \"grows\": \"grass\", \"max_value\": 0.55, \"covers\": \"cards\" },\n"
            + "      \"f.tif\": { \"grows\": \"grass\", \"max_value\": -1, \"covers\": \"shells\" }\n    }\n  }\n}\n";
        var sidecar = VegetationSidecar.Parse(text);
        var c4 = sidecar.For("C4");
        Assert.Equal(5, sidecar.Warnings.Count);
        Assert.Equal(new CoverPick("a.tif", GroundCover.Cover.Grass), c4.CoverFor("a"));
        Assert.Equal(new CoverPick("b.tif", GroundCover.Cover.Crop, 0.25f), c4.CoverFor("b.tif"));
        Assert.Null(c4.CoverFor("c.tif"));
        Assert.Equal(1f, c4.CoverFor("d.tif")!.MinSaturation);
        Assert.Equal(new CoverPick("e.tif", GroundCover.Cover.Grass, 0f, 0.55f, true), c4.CoverFor("e"));
        Assert.Equal(new CoverPick("f.tif", GroundCover.Cover.Grass, 0f, 0f, false), c4.CoverFor("f"));
        string saved = VegetationSidecar.WithEntry(text, "C4", c4.WithSeed(0));
        Assert.Contains("\"a.tif\": \"grass\"", saved);
        Assert.Contains("\"covers\": \"cards\"", saved);
        var back = VegetationSidecar.Parse(saved);
        Assert.Empty(back.Warnings);
        Assert.Equal(c4.Cover, back.For("C4").Cover);
    }

    /// <summary>C1 picks its grassland to a sparse scrub of bushes and the odd fir. Its fields take a hedge set,
    /// which the mask holds to the field edges. Neither is forest, so no clump rises over them.</summary>
    [Fact]
    public void TheShippedFilePicksC1sGrasslandToScrubAndItsFieldsToHedges()
    {
        var c1 = VegetationSidecar.Parse(File.ReadAllText(ShippedPath)).For("C1");
        Assert.Equal(VegetationMask.PathFor("C1"), c1.Mask);
        Assert.All(new[] { "terpat03.tif", "terpat03b.tif", "grass1.tif" }, t => Assert.Equal("scrub", VegetationScatter.Stamped(c1, t)?.Name));
        Assert.All(new[] { "crop04.tif", "crop05.tif" }, t => Assert.Equal("hedge", VegetationScatter.Stamped(c1, t)?.Name));
        Assert.All(c1.Sets, s => Assert.False(s.Mass, s.Name));
        Assert.Null(VegetationScatter.Stamped(c1, "terpat02.tif"));
    }

    /// <summary>The shipped C1 mask thickens the field edges and clears the grassland beside the east-west strips and
    /// on their western approach. Grassland away from the airfield stays neutral.</summary>
    [Fact]
    public void TheShippedC1MaskHedgesTheFieldsAndClearsTheStrips()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(TestData.RepoRoot, "CSVM", "data", VegetationMask.PathFor("C1")));
        var mask = VegetationMask.FromBytes(bytes, -12288f, -12288f, 0f, 0f);
        Assert.Contains((byte)255, bytes);
        Assert.Equal(0f, mask.At(-5000f, -4230f));
        Assert.Equal(0f, mask.At(-6300f, -4230f));
        Assert.Equal(1f, mask.At(-3700f, -4500f));
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
