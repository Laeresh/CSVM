using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Tooling;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The modding folder (<see cref="ModdingFolder"/>, <see cref="GameData.Load"/>): its layout, the
/// launch's choice of folder, and each archive it holds replacing that one game file alone. The
/// edited copies are made in a temp folder from today's extraction. Layout: docs/modding.md.
/// </summary>
public class ModdingFolderTests
{
    [Theory]
    [InlineData("ZBD/c1/m02/zrdr.zbd", "C1/M02/zrdr.zip")]
    [InlineData("ZBD/C1B/gamez.zbd", "C1B/gamez.zip")]
    [InlineData("ZBD/C1/rtexture15.zbd", "C1/rtexture15.zip")]
    [InlineData("ZBD/planes.zbd", "planes.zip")]
    [InlineData("ZBD/soundsh.zbd", "soundsh.zip")]
    [InlineData("ZBD/interp.zbd", "interp.json")]
    [InlineData("strings.dll", "messages.json")]
    public void AnInstallPathMapsToWhereUnzbdWritesItsOutput(string installPath, string output) =>
        Assert.Equal(output, ModdingFolder.OutputOf(installPath));

    [Fact]
    public void AnUnpackedFolderIsReadBeforeItsZipAndAnAbsentArchiveIsNone()
    {
        string root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "C1"));
        File.WriteAllBytes(Path.Combine(root, "C1", "gamez.zip"), Array.Empty<byte>());
        var mods = new ModdingFolder(root);

        Assert.Equal(Path.Combine(root, "C1", "gamez.zip"), mods.Find("ZBD/c1/gamez.zbd"));
        Directory.CreateDirectory(Path.Combine(root, "C1", "gamez"));
        Assert.Equal(Path.Combine(root, "C1", "gamez"), mods.Find("ZBD/C1/gamez.zbd"));
        Assert.Null(mods.Find("ZBD/C2/gamez.zbd"));
        Assert.Null(mods.Find(Messages.GameFile));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("notes.txt", false)]
    [InlineData("C1/readme.txt", false)]
    [InlineData("messages.json", true)]
    [InlineData("zrdr.zip", true)]
    [InlineData("C1/gamez.zip", true)]
    [InlineData("C1/rtexture15.zip", true)]
    [InlineData("C1/M02/mis_anim.zip", true)]
    public void HoldsAnyAnswersWhetherAnyOutputFindKnowsIsThere(string? file, bool holds)
    {
        string root = TestData.TempDir();
        if (file != null)
        {
            string path = Path.Combine(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Array.Empty<byte>());
        }

        Assert.Equal(holds, new ModdingFolder(root).HoldsAny());
        Assert.False(new ModdingFolder(Path.Combine(root, "absent")).HoldsAny());
    }

    [Fact]
    public void TheFlagBeatsTheSavedFolderWhichADeterministicOrSyntheticRunNeverReads()
    {
        string saved = Path.GetFullPath(TestData.TempDir());
        string named = Path.GetFullPath(TestData.TempDir());
        string? Choose(params string[] args) => ModdingFolder.Choose(SessionSpec.Parse(args), () => saved);

        Assert.Equal(saved, Choose("--menu"));
        Assert.Equal(named, Choose("--menu", "--mod-folder=" + named));
        Assert.Null(Choose("--menu", "--mod-folder="));
        Assert.Null(Choose("--menu", "--det"));
        Assert.Equal(named, Choose("--menu", "--det", "--mod-folder=" + named));
        Assert.Null(Choose("--menu", "--synthetic-data", "--mod-folder=" + named));
        Assert.Null(ModdingFolder.Choose(SessionSpec.Parse(new[] { "--menu" }), () => null));
    }

    [Fact]
    public void TheOptionsRowOffersTheLaunchedFolderAndAnOldExtractionThatExist()
    {
        string dataRoot = TestData.TempDir();
        string launched = TestData.TempDir();
        Assert.Equal(new[] { Path.GetFullPath(launched) }, ModdingFolder.Offered(dataRoot, launched));

        Directory.CreateDirectory(Path.Combine(dataRoot, "extracted"));
        Assert.Equal(
            new[] { Path.GetFullPath(launched), Path.GetFullPath(Path.Combine(dataRoot, "extracted")) },
            ModdingFolder.Offered(dataRoot, launched));
        Assert.Empty(ModdingFolder.Offered(null, Path.Combine(launched, "absent")));
    }

    [Fact]
    public void TheSavedFolderRoundTripsThroughTheOptionsFile()
    {
        string folder = Path.GetFullPath(TestData.TempDir());
        var back = Utils.OptionsStore.Deserialize(Utils.OptionsStore.Serialize(new Utils.OptionsDef { ModFolder = folder }))!;
        Assert.Equal(folder, back.ModFolder);
        Assert.Null(Utils.OptionsStore.Deserialize("{\"version\":1,\"modFolder\":\"relative\"}")!.ModFolder);
    }

    // Without game files every slot the folder holds still reads, which shows it is the folder's.
    [Fact]
    public void EveryRootArchiveTheFolderHoldsIsReadFromIt()
    {
        string root = TestData.TempDir();
        File.WriteAllText(Path.Combine(root, "messages.json"), "{\"entries\":[{\"key\":\"MSG_MOD\",\"id\":1,\"value\":\"Modded\"}]}");
        File.WriteAllText(Path.Combine(root, "interp.json"), "[{\"name\":\"support\\\\mod.gw\",\"lines\":[\"MipBias -1\"]}]");
        Directory.CreateDirectory(Path.Combine(root, "zrdr"));
        File.WriteAllText(Path.Combine(root, "zrdr", "camparam.zrd.json"), "[\"mod\",[1.5]]");
        Directory.CreateDirectory(Path.Combine(root, "soundsh"));
        File.WriteAllBytes(Path.Combine(root, "soundsh", "mod.wav"), new byte[] { 1, 2, 3 });

        var data = GameData.Load(null, root);

        Assert.Equal(Path.GetFullPath(root), data.ModFolder);
        Assert.Equal("Modded", data.Messages.Get("MSG_MOD"));
        Assert.Equal(new[] { "MipBias -1" }, data.Interp!.Lines("support\\mod.gw"));
        Assert.Equal("mod", data.Zrdr.LoadFile("camparam.json")[0]);
        Assert.Equal(new byte[] { 1, 2, 3 }, data.Sounds!.ReadWavBytes("mod.wav"));
        Assert.Null(data.Planes);
        Assert.Same(RofFiles.Empty, data.Rof);
    }

    // A broken output falls back to the game file rather than leaving the slot empty.
    [Fact]
    public void OutputThatDoesNotReadLeavesTheSlotToTheGameFiles()
    {
        string root = TestData.TempDir();
        File.WriteAllText(Path.Combine(root, "messages.json"), "not json");
        Assert.Equal(0, GameData.Load(null, root).Messages.Count);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C1")]
    public void OneChaptersGamezFromTheFolderReplacesThatArchiveAlone(string chapter)
    {
        var game = GameFilesData.Source!;
        string root = TestData.TempDir();
        string zip = Path.Combine(root, chapter, "gamez.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(zip)!);
        File.Copy(Path.Combine(TestData.ExtractedRoot!, chapter, "gamez.zip"), zip);
        string original = EditFirst(zip, "models.json", "\"x\": ", "123.5");

        var modded = GameData.Load(game, root);
        var plain = GameData.Load(game);
        var edited = modded.LoadChapter(chapter, "M02").Gamez!;
        var shipped = plain.LoadChapter(chapter, "M02").Gamez!;
        var first = shipped.Meshes.First(m => m.Vertices.Count > 0);
        int at = shipped.Meshes.IndexOf(first);

        Assert.Equal(float.Parse(original, System.Globalization.CultureInfo.InvariantCulture), first.Vertices[0].X);
        Assert.Equal(123.5f, edited.Meshes[at].Vertices[0].X);
        Assert.Equal(shipped.Nodes.Count, edited.Nodes.Count);
        Assert.Equal(first.Vertices[0].Y, edited.Meshes[at].Vertices[0].Y);

        // Every other archive is the game's: another chapter's world, the planes and the readers.
        Assert.Equal(plain.LoadChapter("C1B", "M01").Gamez!.Meshes.Count, modded.LoadChapter("C1B", "M01").Gamez!.Meshes.Count);
        Assert.Equal(plain.Planes!.Nodes.Count, modded.Planes!.Nodes.Count);
        Assert.Equal(GameData.RootZrdrPath, modded.Zrdr.Archive);
        Assert.Equal($"ZBD/{chapter}/zrdr.zbd", modded.LoadChapter(chapter, "M02").ChapterZrdr.Archive);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C1", "M02")]
    public void OneMissionsReadersFromTheFolderReplaceThatScopeAlone(string chapter, string mission)
    {
        string root = TestData.TempDir();
        string folder = Path.Combine(root, chapter, mission, "zrdr");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "pickups.zrd.json"),
            File.ReadAllText(Path.Combine(TestData.ExtractedRoot!, chapter, mission, "zrdr", "pickups.zrd.json")).Replace("ladder_pickup_sensor", "csvm_mod_sensor"));

        var scope = GameData.Load(GameFilesData.Source, root).LoadChapter(chapter, mission);

        Assert.Equal(folder, scope.MissionZrdr.Archive);
        Assert.Contains("csvm_mod_sensor", Flatten(scope.MissionZrdr.LoadFile("pickups.json")));
        Assert.False(scope.MissionZrdr.HasFile("objectives.json"));
        Assert.Equal($"ZBD/{chapter}/zrdr.zbd", scope.ChapterZrdr.Archive);
        Assert.True(scope.ChapterZrdr.Files.Count > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C1")]
    public void OneTextureArchiveFromTheFolderReplacesThatTierAlone(string chapter)
    {
        var game = GameFilesData.Source!;
        string tier = TextureSet.ChapterArchive(game, chapter)!;
        string from = Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ModdingFolder.OutputOf(tier), null));
        string root = TestData.TempDir();
        string folder = Path.Combine(root, Path.ChangeExtension(ModdingFolder.OutputOf(tier), null));
        Directory.CreateDirectory(folder);

        // A one-texture archive whose render flags say Both.
        var shipped = TextureSet.ReadExtracted(from)[0];
        File.Copy(Path.Combine(from, shipped.Key + ".png"), Path.Combine(folder, shipped.Key + ".png"));
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            $"{{\"texture_infos\":[{{\"name\":\"{shipped.Name}\",\"alpha\":\"{shipped.Alpha}\",\"width\":{shipped.Width},\"height\":{shipped.Height},\"stretch\":\"Both\"}}]}}");

        var data = GameData.Load(game, root);
        var textures = data.LoadChapter(chapter, "M02").Textures;

        Assert.Equal(1, textures.Count);
        Assert.Equal(3, textures.Find(shipped.Key)!.RenderFlags);
        Assert.Equal(shipped.Pixels, textures.Find(shipped.Key)!.Pixels);
        Assert.Equal(Path.ChangeExtension(tier[4..], null).ToLowerInvariant(), textures.Archive);
        Assert.Equal(TextureSet.Load(game, GameData.RimagePath).Count, data.Rimage.Count);
    }

    // The data root holds today's extraction; with no folder named, no archive comes from it.
    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("--menu")]
    public void WithNoFolderNamedAnExtractionBesideTheDataRootIsNotRead(string mode)
    {
        var plain = BasePaths.Resolve(TestData.RepoRoot, TestData.DataRoot!, SessionSpec.Parse(new[] { mode, "--det" }), SyntheticGameData.Build);
        Assert.Null(plain.Data.ModFolder);
        Assert.Equal(GameData.RootZrdrPath, plain.Data.Zrdr.Archive);
        Assert.Equal("ZBD/C1/M02/zrdr.zbd", plain.Data.LoadChapter("C1", "M02").MissionZrdr.Archive);

        // Named, the same folder is read.
        var named = BasePaths.Resolve(
            TestData.RepoRoot, TestData.DataRoot!, SessionSpec.Parse(new[] { mode, "--det", "--mod-folder=" + TestData.ExtractedRoot }), SyntheticGameData.Build);
        Assert.Equal(Path.GetFullPath(TestData.ExtractedRoot!), named.Data.ModFolder);
        Assert.StartsWith(Path.GetFullPath(TestData.ExtractedRoot!), named.Data.Zrdr.Archive, StringComparison.OrdinalIgnoreCase);
    }

    [GameFilesFact]
    public void TheGameDataCarriesTheUiHalfReadInPlace()
    {
        var data = GameData.Load(GameFilesData.Source);
        Assert.True(data.Rof.Exists(RofFiles.MenuLayoutFile));
        var view = RofFiles.Of(new System.Collections.Generic.Dictionary<string, byte[]> { ["ASSETS/X.TXT"] = new byte[] { 1 } });
        Assert.Same(view, SyntheticGameData.Build(view).Rof);
    }

    private static string Flatten(object? value) => value switch
    {
        System.Collections.Generic.List<object?> list => string.Join(",", list.Select(Flatten)),
        _ => value?.ToString() ?? string.Empty,
    };

    // Replaces the first value after marker in one entry of the zip, answering the value it had.
    private static string EditFirst(string zip, string entry, string marker, string value)
    {
        using var archive = ZipFile.Open(zip, ZipArchiveMode.Update);
        var found = archive.GetEntry(entry)!;
        string text;
        using (var reader = new StreamReader(found.Open(), Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        int start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = text.IndexOfAny(new[] { ',', '\r', '\n' }, start);
        string original = text[start..end];
        found.Delete();
        using (var writer = new StreamWriter(archive.CreateEntry(entry).Open(), new UTF8Encoding(false)))
        {
            writer.Write(text[..start] + value + text[end..]);
        }

        return original;
    }
}
