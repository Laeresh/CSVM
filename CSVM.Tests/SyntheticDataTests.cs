using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using CSVM.Session.Launch;
using CSVM.Tooling;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The synthetic data root <c>--synthetic-data</c> writes, built from the fixtures the engine reads.
/// Its stamp passes the boot check as a current tree and marks it synthetic. Its chapter texture
/// archive opens through the path every session resolves, with one PNG per manifest entry. Decoding
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
    public void ARebuildReplacesWhatAnEarlierRunLeft()
    {
        string root = Built();
        string stray = Path.Combine(root, "extracted", "C1", "texture", "probe_stray.png");
        File.WriteAllBytes(stray, Array.Empty<byte>());

        SyntheticData.Build(TestData.Fixture(), root);

        Assert.False(File.Exists(stray));
    }

    [Fact]
    public void ATreeNotStampedSyntheticIsNeverReplaced()
    {
        string root = TestData.TempDir();
        string stamp = Path.Combine(root, "extracted", "VERSION.json");
        Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
        File.WriteAllText(stamp, $"{{ \"schema\": {ExtractionStamp.Schema} }}");

        Assert.Throws<IOException>(() => SyntheticData.Build(TestData.Fixture(), root));
        Assert.True(File.Exists(stamp));
        Assert.False(SyntheticData.Marks(root));
    }

    [Fact]
    public void AMissingFixturesFolderFailsTheBuildRatherThanLeavingAnEmptyTree()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        Assert.Throws<DirectoryNotFoundException>(
            () => SyntheticData.Build(Path.Combine(TestData.TempDir(), "absent"), root));
        Assert.False(SyntheticData.Marks(root));
    }

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root);
        return root;
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
