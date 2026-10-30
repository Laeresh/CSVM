using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary><see cref="RofFiles"/> over a fixture install: where each file the extraction wrote now
/// answers, the derived string table and layout, and the cinemas. One check keeps
/// <c>CSVM.Extraction</c> free of engine types. The install facts read the player's install, and
/// one compares the whole view with the extraction it replaces.</summary>
public class RofFilesTests
{
    [Fact]
    public void TheViewHoldsEveryFileWhereTheExtractionPutIt()
    {
        var rof = RofFiles.Open(FixtureInstall());

        Assert.True(rof.Exists("ASSETS/GRAPHICS/FORTUNE/KES_WING.BM"));
        Assert.True(rof.Exists("ASSETS/GRAPHICS/FORTUNE/KES_WING.PNG"));
        Assert.True(rof.Exists("ASSETS/GRAPHICS/FORTUNE/KES_WING_MASK.PNG"));
        Assert.True(rof.Exists("_crimptch/ASSETS/SCRIPTS/AIRFRAME.SCRIPT"));
        Assert.True(rof.Exists("ASSETS/GRAPHICS/MPG/CRIMFLAG.MPG"));
        Assert.True(rof.Exists(RofFiles.MenuLayoutFile));
        Assert.False(rof.Exists("ASSETS/GRAPHICS/MPG/CHAP0.MPG"));

        var strings = UiStrings.Parse(rof.Text(RofFiles.UiStringsFile)!);
        Assert.Equal("Kestrel", strings.Text(16));
        Assert.Equal("IMP36", strings.Face(16));
    }

    [Fact]
    public void TheShadingPngHoldsTheBmShadingPlane()
    {
        var rof = RofFiles.Open(FixtureInstall());

        var png = PngImage.Decode(rof.Read("ASSETS/GRAPHICS/FORTUNE/KES_WING.PNG")!)!;
        var bm = BmTexture.TryDecode(rof.Read("ASSETS/GRAPHICS/FORTUNE/KES_WING.BM"))!;
        Assert.Equal(bm.Width, png.Width);
        Assert.Equal(bm.Shading[3], png.Rgba[4]);
    }

    [Fact]
    public void RowsJoinTheirSymbolAndLanguiWinsTheMenuTextJoin()
    {
        var rof = RofFiles.Open(FixtureInstall());

        using var doc = JsonDocument.Parse(rof.Read(RofFiles.UiStringsFile)!);
        var rows = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { "langui", "langui", "language" }, rows.Select(r => r.GetProperty("dll").GetString()));
        Assert.Equal("IDS_KESTREL", rows[0].GetProperty("symbol").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("symbol").ValueKind);

        string menu = rof.Text(RofFiles.MenuLayoutFile)!;
        Assert.Contains("Kestrel", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("shadowed", menu, StringComparison.Ordinal);
        Assert.Contains("AIRFRAME.SCRIPT", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInstallWithoutTheUiFilesHoldsNothingRatherThanThrowing()
    {
        string root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "GOSDATA", "ASSETS"));

        var rof = RofFiles.Open(new InstallFolderSource(root));

        Assert.True(rof.IsEmpty);
        Assert.Null(rof.Read(RofFiles.UiStringsFile));
        Assert.Same(RofFiles.Empty, RofFiles.Open(null));
    }

    [Fact]
    public void AnArchiveThatDoesNotReadIsReadAsAbsent()
    {
        string root = TestData.TempDir();
        string assets = Directory.CreateDirectory(Path.Combine(root, "GOSDATA", "ASSETS")).FullName;
        File.WriteAllBytes(Path.Combine(assets, "crimson.rof"), new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0 });

        Assert.True(RofFiles.Open(new InstallFolderSource(root)).IsEmpty);
    }

    [Fact]
    public void ADirectoryPointingBackAtItsAncestorIsRejectedRatherThanWalkedForever()
    {
        byte[] rof = ExtractionFixtures.Rof(new RofDir().Dir("LOOP", new RofDir().File("A.TXT", new byte[] { 1 })));

        // The root node's one entry sits at byte 8 and starts with its target offset; 0 is the root.
        BitConverter.GetBytes(0).CopyTo(rof, 8);

        Assert.Throws<InvalidDataException>(() => RofArchive.Walk(rof).ToList());
    }

    [Fact]
    public void ADirectoryNodeSharedBySiblingsIsWalkedUnderEach()
    {
        byte[] rof = ExtractionFixtures.Rof(new RofDir()
            .Dir("A", new RofDir().File("X.TXT", new byte[] { 1 }))
            .Dir("B", new RofDir()));

        // Point B's target (the second entry, 24 bytes on) at A's node.
        Array.Copy(rof, 8, rof, 8 + 24, 4);

        var paths = RofArchive.Walk(rof).Select(e => e.Path).ToList();
        Assert.Equal(new[] { "A", "A/X.TXT", "B", "B/X.TXT" }, paths);
    }

    [Fact]
    public void AHeadInflatesNoFurtherThanItWasAskedAndAWholeReadMatchesIt()
    {
        byte[] text = Encoding.ASCII.GetBytes(new string('x', 5000) + "tail");
        byte[] archive = ExtractionFixtures.Rof(new RofDir().File("BIG.TXT", text));
        var entry = RofArchive.Walk(archive).Single();

        Assert.Equal(text[..24], RofArchive.ReadHead(archive, entry, 24));
        Assert.Equal(text, RofArchive.ReadMember(archive, entry));
        Assert.Equal(text, RofArchive.ReadHead(archive, entry, int.MaxValue));
    }

    [Fact]
    public void ACinemaNameResolvesWithoutRegardToCaseOrExtension()
    {
        var rof = RofFiles.Open(FixtureInstall());

        Assert.Equal("ASSETS/GRAPHICS/MPG/CRIMFLAG.MPG", RofFiles.Cinema("CrimFlag"));
        Assert.Equal(new byte[] { 7, 7, 7, 7 }, rof.Read(RofFiles.Cinema("crimflag.MPG")));
        Assert.Equal(new byte[] { 7, 7 }, rof.Head(RofFiles.Cinema("crimflag"), 2));
    }

    [Fact]
    public void EveryPathIsHeldUpperCaseButTheTwoDerivedFiles()
    {
        var rof = RofFiles.Open(FixtureInstall());

        Assert.Contains("ASSETS/GRAPHICS/AP_BACKGROUND.PNG", rof.Files);
        Assert.True(rof.Exists("assets/graphics/AP_BackGround.png"));
        Assert.All(rof.Files, n => Assert.Equal(RofTree.Canonical(n), n));
    }

    [Fact]
    public void AViewIsFoundByTheDataRootItWasBoundTo()
    {
        string root = TestData.TempDir();
        var rof = RofFiles.Of(new Dictionary<string, byte[]> { ["assets/x.txt"] = new byte[] { 1 } });

        RofFiles.Bind(root, rof);

        Assert.Same(rof, RofFiles.For(root + Path.DirectorySeparatorChar));
        Assert.Same(RofFiles.Empty, RofFiles.For(Path.Combine(root, "elsewhere")));
        Assert.Same(RofFiles.Empty, RofFiles.For(null));
    }

    [Fact]
    public void ExtractionTypesReferenceNoEngineType()
    {
        var violations = AssemblyDependencyScan.Violations(
            Path.Combine(AppContext.BaseDirectory, "CSVM.dll"),
            ns => ns == "CSVM.Extraction",
            name => name.StartsWith("Godot.", StringComparison.Ordinal));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>The shipped archive's census, which docs/formats/rof.md records: every member
    /// inflates to its declared size and every <c>.BM</c> decodes.</summary>
    [InstallFact]
    public void TheInstalledArchiveInflatesWholeAndEveryBmDecodes()
    {
        byte[] rof = File.ReadAllBytes(Path.Combine(InstallFactAttribute.Assets!, "crimson.rof"));

        var entries = RofArchive.Walk(rof).ToList();
        var files = entries.Where(e => !e.IsDirectory).ToList();
        int textures = 0;
        foreach (var file in files)
        {
            byte[] data = RofArchive.ReadMember(rof, file);
            if (file.Path.EndsWith(".BM", StringComparison.OrdinalIgnoreCase) && BmTexture.TryDecode(data) != null)
            {
                textures++;
            }
        }

        Assert.Equal(21, entries.Count - files.Count);
        Assert.Equal(846, files.Count);
        Assert.Equal(184, textures);
    }

    [InstallFact]
    public void TheInstalledStringTablesHoldTheirDocumentedCounts()
    {
        string binaries = Path.Combine(InstallFactAttribute.Assets!, "BINARIES");

        Assert.Equal(1247, PeStringTable.Read(File.ReadAllBytes(Path.Combine(binaries, "langui.dll"))).Count);
        Assert.Equal(36, PeStringTable.Read(File.ReadAllBytes(Path.Combine(binaries, "language.dll"))).Count);
    }

    /// <summary>Every file the engine reads out of the UI half, straight from the install. Each
    /// member inflates, each derived PNG encodes and both derived files decode. All ten cinemas are
    /// there, and each opens as a movie and decodes its first picture.</summary>
    [GameFilesFact]
    public void EveryFileTheEngineReadsDecodesFromTheInstall()
    {
        var rof = RofFiles.Open(GameFilesData.Source);
        var movies = rof.Files.Where(f => f.StartsWith(RofFiles.CinemaFolder + "/", StringComparison.Ordinal)).ToList();
        foreach (string file in rof.Files.Except(movies))
        {
            Assert.True(rof.Read(file) is { Length: > 0 }, file);
        }

        // The patch archive adds one member; a pressed disc carries no patch.
        int patched = GameFilesData.Source!.Exists(RofFiles.PatchPath) ? 1 : 0;
        Assert.Equal(1216 + patched, rof.Files.Count() - movies.Count);
        Assert.Equal(10, movies.Count);
        Assert.All(movies, m => Assert.True(rof.Head(m, 4) is [0x00, 0x00, 0x01, 0xBA], m));
        Assert.All(movies, m => Assert.True(CSVM.Video.MpegMovie.FromBytes(rof.Read(m)!).NextFrame() != null, m));
        Assert.True(UiStrings.Parse(rof.Text(RofFiles.UiStringsFile)!).Has(16));
        Assert.NotNull(CSVM.UI.Menu.MenuLayout.Parse(rof.Text(RofFiles.MenuLayoutFile)!).Screen("MainMenu"));
    }

    /// <summary>The view against the extraction it replaces, file for file. The paths match, and so
    /// do the bytes of every member, derived PNG, derived file and cinema.</summary>
    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("rof")]
    public void TheViewMatchesTheExtractionByteForByte(string folder)
    {
        string extracted = Path.Combine(TestData.ExtractedRoot!, folder);
        var rof = RofFiles.Open(GameFilesData.Source);
        var onDisk = Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(extracted, f).Replace('\\', '/'))
            .ToDictionary(RofTree.Canonical, f => f);

        Assert.Equal(onDisk.Keys.OrderBy(k => k, StringComparer.Ordinal), rof.Files.OrderBy(k => k, StringComparer.Ordinal));
        var differ = new List<string>();
        foreach (var (key, file) in onDisk)
        {
            byte[] expected = File.ReadAllBytes(Path.Combine(extracted, file));
            byte[] actual = rof.Read(key)!;
            bool same = key.EndsWith(".JSON", StringComparison.Ordinal) ? Normalized(expected) == Normalized(actual)
                : expected.AsSpan().SequenceEqual(actual) || (Derived(rof, key) && SamePixels(expected, actual));
            if (!same)
            {
                differ.Add(key);
            }
        }

        Assert.True(differ.Count == 0, string.Join(", ", differ));
    }

    // A .BM's PNG pair, which an older extractor encoded with another compressor: by pixel.
    private static bool Derived(RofFiles rof, string key) =>
        (key.EndsWith("_MASK.PNG", StringComparison.Ordinal) && rof.Exists(key[..^"_MASK.PNG".Length] + ".BM"))
        || (key.EndsWith(".PNG", StringComparison.Ordinal) && rof.Exists(key[..^".PNG".Length] + ".BM"));

    private static bool SamePixels(byte[] a, byte[] b) =>
        PngImage.Decode(a) is { } x && PngImage.Decode(b) is { } y && x.Width == y.Width && x.Rgba.AsSpan().SequenceEqual(y.Rgba);

    // A JSON file by value: an older extractor wrote the two derived files with other whitespace,
    // escaping and a byte order mark.
    private static string Normalized(byte[] json) =>
        System.Text.Json.Nodes.JsonNode.Parse(RofFiles.Of(new Dictionary<string, byte[]> { ["f"] = json }).Text("f")!)!.ToJsonString();

    // A base archive holding one member in mixed case, which the shipped archive never has, so
    // the view's own mapping is what upper-cases it. Laid out as an install's GOSDATA/ASSETS.
    private static IGameFileSource FixtureInstall()
    {
        string root = TestData.TempDir();
        string assets = Directory.CreateDirectory(Path.Combine(root, "GOSDATA", "ASSETS")).FullName;
        string layout = "[@ProbeMain@]\r\nPM_T_TITLE=T,IDS_KESTREL,120,44,0,200,20,0xFF102030,1\r\n";
        File.WriteAllBytes(Path.Combine(assets, "crimson.rof"), ExtractionFixtures.Rof(new RofDir().Dir("ASSETS", new RofDir()
            .File("LAYOUT.CSV", Encoding.ASCII.GetBytes(layout))
            .Dir("SCRIPTS", new RofDir()
                .File("RESOURCE.H", Encoding.ASCII.GetBytes("#define IDS_KESTREL 16\r\n")))
            .Dir("GRAPHICS", new RofDir()
                .File("AP_BackGround.png", new byte[] { 1 })
                .Dir("MPG", new RofDir())
                .Dir("FORTUNE", new RofDir().File("KES_WING.BM", ExtractionFixtures.Bm(4, 2, i => (byte)i)))))));
        File.WriteAllBytes(Path.Combine(assets, "crimptch.rof"), ExtractionFixtures.Rof(new RofDir().Dir("ASSETS", new RofDir()
            .Dir("SCRIPTS", new RofDir().File("AIRFRAME.SCRIPT", Encoding.ASCII.GetBytes("gui_create"))))));
        string movies = Directory.CreateDirectory(Path.Combine(assets, "GRAPHICS", "MPG")).FullName;
        File.WriteAllBytes(Path.Combine(movies, "CrimFlag.MPG"), new byte[] { 7, 7, 7, 7 });
        string binaries = Directory.CreateDirectory(Path.Combine(assets, "BINARIES")).FullName;
        File.WriteAllBytes(Path.Combine(binaries, "langui.dll"), ExtractionFixtures.StringTablePe(2, new[] { "[IMP36]Kestrel", "Second" }));
        File.WriteAllBytes(Path.Combine(binaries, "language.dll"), ExtractionFixtures.StringTablePe(2, new[] { "shadowed" }));
        return new InstallFolderSource(root);
    }
}

/// <summary>A fact that needs the player's install (its <c>GOSDATA/ASSETS</c> folder under a data
/// root) and, when <see cref="OptInVariable"/> is set, that variable too. Skipped with the reason
/// otherwise, so an absent install never reads as a pass.</summary>
public sealed class InstallFactAttribute : FactAttribute
{
    private string? _optIn;

    public InstallFactAttribute()
    {
        if (Assets == null)
        {
            Skip = "no retail install: set CSVM_DATA_ROOT to a checkout holding CrimsonSkiesGame/";
        }
    }

    /// <summary>The install's <c>GOSDATA/ASSETS</c> folder, or null when none is reachable.</summary>
    public static string? Assets { get; } = FindAssets();

    /// <summary>An environment variable the test also needs; unset, the test is skipped.</summary>
    public string? OptInVariable
    {
        get => _optIn;
        set
        {
            _optIn = value;
            if (Skip == null && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(value!)))
            {
                Skip = "opt-in: set " + value + " to run this";
            }
        }
    }

    private static string? FindAssets()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("CSVM_DATA_ROOT"), TestData.RepoRoot })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string assets = Path.Combine(root, "CrimsonSkiesGame", "GOSDATA", "ASSETS");
            if (File.Exists(Path.Combine(assets, "crimson.rof")))
            {
                return assets;
            }
        }

        return null;
    }
}
