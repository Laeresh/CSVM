using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The synthetic data root <c>--synthetic-data</c> writes, built from the fixtures the engine reads.
/// Its stamp passes the boot check as a current tree and marks it synthetic. Its chapter texture
/// archive opens through the path every session resolves, with one PNG per manifest entry. The
/// Original shell's files pass its availability check, each picture at its recorded size. Decoding
/// pixels needs Godot, so the in-engine run covers that half.
/// </summary>
public class SyntheticDataTests
{
    [Fact]
    public void TheTreeIsStampedCurrentAndMarkedSynthetic()
    {
        string root = Built();

        Assert.Equal(StampStanding.Current, ExtractionStamp.Standing(root, out int? found));
        Assert.Equal(ExtractionStamp.Schema, found);
        Assert.True(SyntheticData.Marks(root));

        var lines = new List<string>();
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            ExtractionStamp.Check(root);
        }

        Assert.Empty(lines);
    }

    [Fact]
    public void ARealLookingStampIsNotSynthetic()
    {
        string root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "extracted"));
        File.WriteAllText(
            Path.Combine(root, "extracted", "VERSION.json"),
            $"{{ \"schema\": {ExtractionStamp.Schema}, \"assets\": {{ \"script\": \"CSVM\" }} }}");

        Assert.False(SyntheticData.Marks(root));
        Assert.False(SyntheticData.Marks(TestData.TempDir()));
    }

    [Fact]
    public void TheChapterTextureArchiveOpensWithOnePngPerManifestEntry()
    {
        string root = Built();
        var lines = new List<string>();
        using (CSVM.Utils.Log.PushConsoleSink(lines.Add))
        {
            using var archive = new TextureArchive(SessionPaths.ChapterTextures(root, SyntheticTextures.Chapter));
            var entries = ManifestEntries();
            Assert.NotEmpty(entries);
            Assert.Equal(entries.Count, archive.ManifestTextureCount);
            foreach (var (name, width, height) in entries)
            {
                byte[]? png = archive.ReadPngBytes(name);
                Assert.NotNull(png);
                Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
                Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
            }

            Assert.Empty(archive.MissingTextures);
        }

        Assert.Empty(lines);
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
    }

    [Fact]
    public void EveryShellPictureIsWrittenAtItsRecordedSizeInItsOwnFormat()
    {
        string root = Built();
        using var doc = JsonDocument.Parse(File.ReadAllText(TestData.Fixture("synthetic", "rof", "art.json")));
        foreach (var entry in doc.RootElement.GetProperty("art").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString()!;
            byte[] bytes = File.ReadAllBytes(OriginalAvailability.ArtPath(root, name));
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
    public void ARebuildReplacesWhatAnEarlierRunLeft()
    {
        string root = Built();
        string stray = Path.Combine(root, "extracted", "C1", "texture", "probe_stray.png");
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
        File.WriteAllText(stamp, $"{{ \"schema\": {ExtractionStamp.Schema} }}");

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

    private static List<(string Name, int Width, int Height)> ManifestEntries()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(TestData.Fixture("synthetic", "C1", "texture", "manifest.json")));
        var entries = new List<(string, int, int)>();
        foreach (var info in doc.RootElement.GetProperty("texture_infos").EnumerateArray())
        {
            entries.Add((info.GetProperty("name").GetString()!, info.GetProperty("width").GetInt32(), info.GetProperty("height").GetInt32()));
        }

        return entries;
    }
}
