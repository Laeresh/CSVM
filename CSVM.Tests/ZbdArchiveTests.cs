using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

public class ZbdArchiveTests
{
    private readonly ITestOutputHelper _output;

    public ZbdArchiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Every manifest archive, as a path under <c>ZBD</c>.</summary>
    public static IEnumerable<object[]> ManifestArchives() => ZbdManifest.Expected.Select(path => new object[] { path });

    /// <summary>The manifest archives in the named-entry layout: every <c>zrdr.zbd</c> and the two
    /// sound archives. The rest are formats of their own.</summary>
    public static IEnumerable<object[]> EntryArchives() =>
        ZbdManifest.Expected.Where(IsEntryArchive).Select(path => new object[] { path });

    [Fact]
    public void ReadsEntriesInTableOrderWithTheirPayloads()
    {
        byte[] bytes = Build(("aiv.zrd", new byte[] { 1, 2, 3 }), ("egen.zrd", new byte[] { 9 }), ("aiv.zrd", new byte[] { 7, 7 }));
        using var archive = ZbdArchive.Read(new MemoryStream(bytes), "fixture");

        Assert.Equal(new[] { "aiv.zrd", "egen.zrd", "aiv.zrd" }, archive.Entries.Select(e => e.Name));
        Assert.Equal(new long[] { 0, 3, 4 }, archive.Entries.Select(e => e.Offset));
        Assert.Equal(new byte[] { 1, 2, 3 }, archive.ReadEntry(archive.Entries[0]));
        Assert.Equal(new byte[] { 9 }, archive.ReadEntry(archive.Entries[1]));
        Assert.Equal(new byte[] { 7, 7 }, archive.ReadEntry(archive.Entries[2]));
    }

    [Fact]
    public void AnEmptyArchiveHasNoEntries()
    {
        using var archive = ZbdArchive.Read(new MemoryStream(Build()), "fixture");
        Assert.Empty(archive.Entries);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("count")]
    [InlineData("gap")]
    [InlineData("overrun")]
    [InlineData("short")]
    public void ACorruptTableThrows(string corruption)
    {
        byte[] bytes = Build(("a.zrd", new byte[] { 1, 2 }), ("b.zrd", new byte[] { 3 }));
        int table = bytes.Length - 8 - (2 * 148);
        switch (corruption)
        {
            case "version":
                bytes[^8] = 2;
                break;
            case "count":
                bytes[^4] = 200;
                break;
            case "gap":
                bytes[table + 148] = 3;
                break;
            case "overrun":
                bytes[table + 148 + 4] = 200;
                break;
            case "short":
                bytes = new byte[] { 1, 0, 0 };
                break;
        }

        Assert.Throws<InvalidDataException>(() => ZbdArchive.Read(new MemoryStream(bytes), "fixture"));
    }

    [Fact]
    public void OpensThroughAGameFileSource()
    {
        string install = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS"));
        Directory.CreateDirectory(Path.Combine(install, "zbd", "C1", "M02"));
        File.WriteAllBytes(Path.Combine(install, "zbd", "C1", "M02", "ZRDR.ZBD"), Build(("aiv.zrd", new byte[] { 5 })));
        var source = GameFileSource.Open(install)!;

        using (var archive = ZbdArchive.Open(source, "ZBD/c1/m02/zrdr.zbd"))
        {
            Assert.Equal(new byte[] { 5 }, archive.ReadEntry(Assert.Single(archive.Entries)));
        }

        Assert.Throws<FileNotFoundException>(() => ZbdArchive.Open(source, "ZBD/c1/zrdr.zbd"));
    }

    [GameFilesTheory]
    [MemberData(nameof(ManifestArchives))]
    public void EveryManifestArchiveOpensFromTheInstall(string path)
    {
        var source = GameFilesData.Source!;
        Assert.True(source.Exists("ZBD/" + path), path);
        using var stream = source.Open("ZBD/" + path)!;
        Assert.True(stream.CanSeek);
        Assert.True(stream.Length > 0, path);
    }

    [GameFilesTheory]
    [MemberData(nameof(EntryArchives))]
    public void EveryEntryArchiveTilesItsPayloadRegion(string path)
    {
        using var archive = ZbdArchive.Open(GameFilesData.Source!, "ZBD/" + path);
        using var stream = GameFilesData.Source!.Open("ZBD/" + path)!;
        long tableStart = stream.Length - 8 - (archive.Entries.Count * 148L);
        var last = archive.Entries[^1];

        Assert.Equal(tableStart, last.Offset + last.Length);
        _output.WriteLine($"{path}: {archive.Entries.Count} entries, {archive.Entries.Count(e => e.Length == 0)} empty");
    }

    // Today's extraction stores each payload under its stored name, a repeat renamed stem-N.ext, a
    // reader entry with .json appended. Matching every name and every payload's CRC32 against the
    // zip proves the reader finds the same bytes unzbd did.
    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(EntryArchives))]
    public void EveryEntryMatchesTodaysExtraction(string path)
    {
        string zipPath = Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ZbdTree.Canonical(path), ".zip"));
        using var zip = ZipFile.OpenRead(zipPath);
        var stored = zip.Entries.Where(e => e.FullName != "manifest.json").ToDictionary(e => e.FullName);
        using var archive = ZbdArchive.Open(GameFilesData.Source!, "ZBD/" + path);
        bool reader = Path.GetFileName(path) == "zrdr.zbd";
        var seen = new HashSet<string>();
        int renamed = 0;

        foreach (var entry in archive.Entries)
        {
            string name = Rename(entry.Name, seen);
            renamed += name == entry.Name ? 0 : 1;
            Assert.True(stored.Remove(reader ? name + ".json" : name, out var zipped), $"{path}: no zip entry for {name}");
            if (!reader)
            {
                Assert.Equal(zipped!.Crc32, PngWriter.Crc32(archive.ReadEntry(entry)));
            }
        }

        Assert.Empty(stored.Keys);
        _output.WriteLine($"{path}: {archive.Entries.Count} entries match, {renamed} renamed");
    }

    private static bool IsEntryArchive(string path) =>
        Path.GetFileName(path) is "zrdr.zbd" or "soundsh.zbd" or "soundsl.zbd";

    private static string Rename(string name, HashSet<string> seen)
    {
        if (seen.Add(name))
        {
            return name;
        }

        int dot = name.LastIndexOf('.');
        for (int index = 1; ; index++)
        {
            string candidate = dot < 0 ? $"{name}-{index}" : $"{name[..dot]}-{index}{name[dot..]}";
            if (seen.Add(candidate))
            {
                return candidate;
            }
        }
    }

    // The layout of docs/formats/zbd-archive.md, with the comment and filetime fields filled the
    // way the original's archives fill them.
    private static byte[] Build(params (string Name, byte[] Data)[] entries)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        foreach (var (_, data) in entries)
        {
            writer.Write(data);
        }

        uint offset = 0;
        foreach (var (name, data) in entries)
        {
            writer.Write(offset);
            writer.Write((uint)data.Length);
            writer.Write(Padded(name));
            writer.Write(2u);
            writer.Write(Padded(name));
            writer.Write(126117504560000000UL);
            offset += (uint)data.Length;
        }

        writer.Write(1u);
        writer.Write((uint)entries.Length);
        return stream.ToArray();
    }

    private static byte[] Padded(string text)
    {
        var field = new byte[64];
        Encoding.ASCII.GetBytes(text).CopyTo(field, 0);
        return field;
    }
}
