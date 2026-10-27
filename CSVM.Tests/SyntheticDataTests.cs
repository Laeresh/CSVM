using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The synthetic data root <c>--synthetic-data</c> writes, built from the fixtures the engine reads.
/// Its stamp marks it synthetic. Its chapter textures
/// are built in memory, as its message table and its Original shell's files are. The shell's files
/// pass its availability check, each picture at its recorded size. Decoding
/// pixels needs Godot, so the in-engine run covers that half.
/// </summary>
public class SyntheticDataTests
{
    [Fact]
    public void TheTreeIsMarkedSynthetic()
    {
        Assert.True(SyntheticData.Marks(Built()));
    }

    /// <summary>A synthetic launch resolves no game source and reads the invented message table and
    /// aircraft out of memory. The tree it runs over carries neither, and its chapters no world.</summary>
    [Fact]
    public void ASyntheticLaunchBuildsItsGameDataInMemoryWithNoSource()
    {
        string root = Built();
        var paths = Spec.BasePaths.Resolve(TestData.RepoRoot, root, Spec.SessionSpec.Parse(new[] { "--synthetic-data" }), Tooling.SyntheticGameData.Build);

        Assert.Null(paths.Game);
        Assert.Equal("Probe Gun", paths.Data.Messages.Get("MSG_PROBE_GUN"));
        Assert.Null(paths.Data.Interp);
        Assert.False(File.Exists(Path.Combine(root, "extracted", "messages.json")));
        Assert.NotNull(paths.Data.Planes?.FindByName(SyntheticPlane.Plane));
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "planes")));
        Assert.NotNull(paths.Data.Sounds?.ReadWavBytes(SyntheticSounds.Wavs[0].Name));
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "soundsh")));
        Assert.Null(paths.Data.LoadChapter("C1", "M01").Gamez);
        Assert.True(paths.Data.Zrdr.HasFile("vehicle.json"));
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "zrdr")));
        var probe = paths.Data.LoadChapter(SyntheticMission.Chapter, SyntheticMission.Mission);
        Assert.True(probe.ChapterZrdr.Exists);
        Assert.Empty(probe.ChapterZrdr.Files);
        Assert.True(probe.MissionZrdr.HasFile("weather.json"));
        Assert.False(paths.Data.LoadChapter("C2", "M01").MissionZrdr.Exists);
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "C1")));
    }

    /// <summary>The run's reader scopes are the root's records, the empty C1 chapter, the two invented
    /// missions and four chapters' <c>ia</c>. No other scope exists.</summary>
    [Fact]
    public void TheReaderScopesHoldTheRecordsTheSuitesRead()
    {
        Assert.Equal(
            new[] { "vehicle.zrd", "engines.zrd", "player.zrd", "maneuvers.zrd", "weapons.zrd", "shakes.zrd", "ai.zrd",
                "probe_world.zrd", "flame_ball.zrd", "pufftrails.zrd", "cm_sequence.zrd", "Briefing.zrd", "sounds.zrd", "voice.zrd" },
            SyntheticZrdr.Root().Files.Select(f => f.Key));
        Assert.All(SyntheticZrdr.Root().Files, f => Assert.NotEmpty(f.Root));
        Assert.Equal(new[] { "objectives.zrd" }, SyntheticZrdr.Mission("C1", SyntheticMission.CampaignMission).Files.Select(f => f.Key));
        foreach (string chapter in new[] { "C1B", "C3", "C4", "C5" })
        {
            Assert.True(SyntheticZrdr.Mission(chapter, "IA1").HasFile("ia.json"));
        }

        Assert.False(SyntheticZrdr.Mission("C1", "IA1").Exists);
        Assert.False(SyntheticZrdr.Chapter("C2").Exists);
    }

    [Fact]
    public void ARealLookingStampIsNotSynthetic()
    {
        string root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "extracted"));
        File.WriteAllText(
            Path.Combine(root, "extracted", "VERSION.json"),
            $"{{ \"schema\": 4, \"assets\": {{ \"script\": \"CSVM\" }} }}");

        Assert.False(SyntheticData.Marks(root));
        Assert.False(SyntheticData.Marks(TestData.TempDir()));
    }

    /// <summary>The synthetic chapter textures are built in memory, one record per entry at its
    /// size, for C1 alone; the tree carries no texture folder.</summary>
    [Fact]
    public void TheChapterTexturesAreBuiltInMemoryOneRecordPerEntry()
    {
        string root = Built();
        var data = SyntheticGameData.Build();
        var lines = new List<string>();
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            using var archive = new TextureArchive(data.LoadChapter(SyntheticTextures.Chapter, "M01").Textures);
            Assert.NotEmpty(SyntheticTextures.Entries);
            Assert.Equal(SyntheticTextures.Entries.Count, archive.ManifestTextureCount);
            foreach (var (name, width, height) in SyntheticTextures.Entries)
            {
                var texture = archive.Lookup(name);
                Assert.NotNull(texture);
                Assert.Equal((width, height), (texture!.Width, texture.Height));
                Assert.Equal(width * height * 3, texture.Pixels.Length);
            }

            Assert.Empty(archive.MissingTextures);
        }

        Assert.Empty(lines);
        Assert.Equal(0, data.LoadChapter("C2", "M01").Textures.Count);
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "C1", "texture")));
    }

    [Fact]
    public void TheOriginalShellOpensOverTheTree()
    {
        string root = Built();

        var layout = OriginalAvailability.Load(root, out string? reason);

        Assert.Null(reason);
        Assert.NotNull(layout);
        Assert.All(layout!.MissingArt, art => Assert.True(OriginalAvailability.IsMovie(art), art));
        Assert.NotNull(layout.Screen(OriginalAvailability.MainMenuSection));
        Assert.Equal("Agreed", layout.Screen("MessageBox")!.Widget("MB_B_CENTER")!.Text);
        Assert.True(UiStrings.TryLoad(root)!.Has(10123));
        Assert.False(Directory.Exists(Path.Combine(root, "extracted", "rof")));
    }

    [Fact]
    public void EveryShellPictureIsWrittenAtItsRecordedSizeInItsOwnFormat()
    {
        string root = Built();
        using var doc = JsonDocument.Parse(File.ReadAllText(TestData.Fixture("synthetic", "rof", "art.json")));
        foreach (var entry in doc.RootElement.GetProperty("art").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString()!;
            byte[] bytes = RofFiles.For(root).Read(OriginalAvailability.RelativeArtPath(name))!;
            var size = Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".png" => (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20))),
                ".tga" => (TgaImage.Decode(bytes)!.Width, TgaImage.Decode(bytes)!.Height),
                _ => JpegFrameSize(bytes),
            };

            Assert.Equal((entry.GetProperty("width").GetInt32(), entry.GetProperty("height").GetInt32()), size);
        }
    }

    [Fact]
    public void TheInventedMissionCarriesTwoBearingsACloudBandAndANetBlock()
    {
        var zrdr = SyntheticZrdr.Mission(SyntheticMission.Chapter, SyntheticMission.Mission);

        // A zone change shows only where the two zones author different bearings. A whiteout
        // shows only where the band's core sits above the ground.
        var weather = WeatherState.Load(zrdr);
        Assert.NotNull(weather);
        Assert.NotEqual(weather!.Zone("zone1").SunOrientation, weather.Zone("zone2").SunOrientation);
        Assert.Equal(1f, weather.WhiteoutAmount(weather.CloudBandCentre));
        Assert.Equal(0f, weather.WhiteoutAmount(0f));

        var block = SpawnPoints.LoadNetFreeForAll(zrdr);
        Assert.Equal(SpawnPoints.NetBlock, block?.Count);
        Assert.Equal(block!.Count, block.Select(s => s.Position).Distinct().Count());
    }

    [Fact]
    public void ARebuildReplacesWhatAnEarlierRunLeft()
    {
        string root = Built();
        string stray = Path.Combine(root, "extracted", "probe_stray.png");
        File.WriteAllBytes(stray, Array.Empty<byte>());

        SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies);

        Assert.False(File.Exists(stray));
    }

    [Fact]
    public void ATreeNotStampedSyntheticIsNeverReplaced()
    {
        string root = TestData.TempDir();
        string stamp = Path.Combine(root, "extracted", "VERSION.json");
        Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
        File.WriteAllText(stamp, "{ \"schema\": 4 }");

        Assert.Throws<IOException>(() => SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies));
        Assert.True(File.Exists(stamp));
        Assert.False(SyntheticData.Marks(root));
    }

    [Fact]
    public void AMissingFixturesFolderFailsTheBuildRatherThanLeavingAnEmptyTree()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        Assert.Throws<DirectoryNotFoundException>(
            () => SyntheticData.Build(Path.Combine(TestData.TempDir(), "absent"), root, SyntheticShell.TreeFamilies));
        Assert.False(SyntheticData.Marks(root));
    }

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies);
        return root;
    }

    // A baseline JPEG's frame header: the size follows the precision byte, height first. Every
    // marker before it is a length-prefixed segment, which is all the synthetic writer emits.
    private static (int Width, int Height) JpegFrameSize(byte[] jpeg)
    {
        Assert.True(jpeg[0] == 0xFF && jpeg[1] == 0xD8, "a JPEG starts with SOI");
        int at = 2;
        while (jpeg[at + 1] != 0xC0)
        {
            at += 2 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 2));
        }

        return (BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 7)), BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at + 5)));
    }
}
