using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="ZrdrZbd"/> over readers and archives encoded in code (CI-safe), and over every
/// <c>zrdr.zbd</c> of the install against today's zips read by <see cref="Zrdr.LoadFileOrEmpty"/>.
/// Value encoding and lookup rule: docs/formats/zrdr.md.
/// </summary>
public class ZrdrZbdTests
{
    private readonly ITestOutputHelper _output;

    public ZrdrZbdTests(ITestOutputHelper output) => _output = output;

    /// <summary>The 62 reader archives: the root, each chapter's and each mission's.</summary>
    public static IEnumerable<object[]> ReaderArchives() =>
        ZbdManifest.Expected.Where(IsReaderArchive).Select(path => new object[] { path });

    [Fact]
    public void ThereAreSixtyTwoReaderArchives()
    {
        Assert.Equal(62, ZbdManifest.Expected.Count(IsReaderArchive));
    }

    [Fact]
    public void ValuesDecodeIntoTheLoaderShapeWithTheirFloatBits()
    {
        float subnormal = BitConverter.UInt32BitsToSingle(0x00000001);
        byte[] payload = List(Str("speed"), List(Float(-0f), Float(subnormal), Int(-3)), Str("LOOPED"), List());

        var root = ZrdrZbd.ReadReader(payload, "probe.zrd");

        Assert.Equal(4, root.Count);
        Assert.Equal("speed", root[0]);
        var values = Assert.IsType<List<object?>>(root[1]);
        Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits((float)values[0]!));
        Assert.Equal(0x00000001u, BitConverter.SingleToUInt32Bits((float)values[1]!));
        Assert.Equal(-3f, Assert.IsType<float>(values[2]));
        Assert.Equal("LOOPED", root[2]);
        Assert.Null(root[3]);
    }

    [Fact]
    public void AnEmptyReaderIsAnEmptyRoot()
    {
        Assert.Empty(ZrdrZbd.ReadReader(List(), "templates.zrd"));
    }

    [Theory]
    [InlineData("root tag")]
    [InlineData("value tag")]
    [InlineData("count zero")]
    [InlineData("count overrun")]
    [InlineData("string overrun")]
    [InlineData("non-ascii")]
    [InlineData("trailing")]
    [InlineData("truncated")]
    public void ACorruptReaderThrows(string corruption)
    {
        byte[] payload = List(Str("ab"), Int(1));
        switch (corruption)
        {
            case "root tag":
                payload[0] = 3;
                break;
            case "value tag":
                payload[8] = 9;
                break;
            case "count zero":
                payload[4] = 0;
                break;
            case "count overrun":
                payload[4] = 200;
                break;
            case "string overrun":
                payload[12] = 200;
                break;
            case "non-ascii":
                payload[16] = 0xE9;
                break;
            case "trailing":
                payload = payload.Concat(new byte[] { 0 }).ToArray();
                break;
            case "truncated":
                payload = payload[..^2];
                break;
        }

        Assert.Throws<InvalidDataException>(() => ZrdrZbd.ReadReader(payload, "probe.zrd"));
    }

    [Fact]
    public void ARepeatedNameIsRenamedAndTheFirstOccurrenceIsFound()
    {
        byte[] archive = Archive(("player.zrd", List(Str("first"))), ("Briefing.zrd", List()), ("player.zrd", List(Str("second"))));

        var files = ZrdrZbd.Read(archive);

        Assert.Equal(new[] { "player.zrd", "Briefing.zrd", "player-1.zrd" }, files.Select(f => f.Key));
        Assert.Equal("first", ZrdrZbd.Find(files, "player.json")!.Root[0]);
        Assert.Equal("first", ZrdrZbd.Find(files, "player.zrd.json")!.Root[0]);
        Assert.Equal("second", ZrdrZbd.Find(files, "player-1.zrd")!.Root[0]);
        Assert.Equal("second", ZrdrZbd.Find(files, "player-1.json")!.Root[0]);
        Assert.Same(files[1], ZrdrZbd.Find(files, "briefing.json"));
        Assert.Null(ZrdrZbd.Find(files, "vehicle.json"));
    }

    [Fact]
    public void NoSourceReadsAsNoReaders()
    {
        Assert.Null(ZrdrZbd.Load(null, "ZBD/zrdr.zbd"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(ReaderArchives))]
    public void TheReaderBuildsTheFilesTheExtractionBuilds(string path)
    {
        var bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        var clock = Stopwatch.StartNew();
        var files = ZrdrZbd.Read(bytes);
        double decodeMs = clock.Elapsed.TotalMilliseconds;

        int compared = ModelDiff.AssertEqual(FromExtraction(path), ByZipName(files));
        _output.WriteLine($"{path}: {files.Count} readers, {files.Count(f => f.Rename != null)} renamed, "
                          + $"{files.Count(f => f.Root.Count == 0)} empty, {bytes.Length} bytes, decoded in {decodeMs:F2} ms; "
                          + $"{compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(ReaderArchives))]
    public void TheEngineScopeOfTheArchiveAnswersAsTheExtractedScopeDoes(string path)
    {
        string zip = Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ZbdTree.Canonical(path), ".zip"));
        var extracted = ZrdrSet.FromExtracted(zip);
        var archive = ZrdrSet.Load(GameFilesData.Source, "ZBD/" + path);

        int compared = ModelDiff.AssertEqual(
            extracted.Files.ToDictionary(f => f.Key, f => f.Root), archive.Files.ToDictionary(f => f.Key, f => f.Root));
        foreach (var file in archive.Files)
        {
            string name = file.Key + ".json";
            Assert.True(extracted.HasFile(name), name);
            ModelDiff.AssertEqual(extracted.LoadFileOrEmpty(name), archive.LoadFileOrEmpty(name));
        }

        Assert.Equal(extracted.LoadMatchingFiles("ANIMATION_DEFINITIONS").Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal),
            archive.LoadMatchingFiles("ANIMATION_DEFINITIONS").Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
        _output.WriteLine($"{path}: {archive.Files.Count} readers, {compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("zrdr.zbd", "camparam.zrd")]
    public void OneFlippedFloatBitInTheArchiveIsFoundUnderItsReader(string path, string reader)
    {
        var expected = FromExtraction(path);
        byte[] bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        int at = FirstStoredFloat(bytes, reader, expected[reader + ".json"]);
        bytes[at] ^= 1;

        string? difference = ModelDiff.FirstDifference(expected, ByZipName(ZrdrZbd.Read(bytes)));

        Assert.NotNull(difference);
        Assert.Contains(reader + ".json", difference);
        _output.WriteLine(difference);
    }

    private static bool IsReaderArchive(string path) => Path.GetFileName(path) == "zrdr.zbd";

    private static Dictionary<string, List<object?>> ByZipName(IReadOnlyList<ZrdrFile> files) =>
        files.ToDictionary(f => f.Key + ".json", f => f.Root);

    private static Dictionary<string, List<object?>> FromExtraction(string path)
    {
        string zip = Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ZbdTree.Canonical(path), ".zip"));
        using var archive = ZipFile.OpenRead(zip);
        return archive.Entries.Select(e => e.FullName).Where(n => n != "manifest.json").ToList()
            .ToDictionary(n => n, n => Zrdr.LoadFileOrEmpty(zip, n));
    }

    // The low byte of the first of the reader's floats stored with a float tag. Integer-tagged values
    // read as floats too, so a candidate may be absent.
    private static int FirstStoredFloat(byte[] bytes, string reader, List<object?> root)
    {
        using var archive = ZbdArchive.Read(new MemoryStream(bytes), "zrdr.zbd");
        var entry = archive.Entries.First(e => e.Name == reader);
        var payload = bytes.AsSpan((int)entry.Offset, entry.Length);
        foreach (float f in Floats(root))
        {
            int at = payload.IndexOf(Float(f));
            if (at >= 0)
            {
                return (int)entry.Offset + at + 4;
            }
        }

        throw new InvalidOperationException("no stored float found");
    }

    private static IEnumerable<float> Floats(List<object?> list) =>
        list.SelectMany(v => v switch
        {
            float f => new[] { f },
            List<object?> inner => Floats(inner),
            _ => Enumerable.Empty<float>(),
        });

    private static byte[] Int(int value) => Tagged(1, BitConverter.GetBytes(value));

    private static byte[] Float(float value) => Tagged(2, BitConverter.GetBytes(value));

    private static byte[] Str(string text) =>
        Tagged(3, BitConverter.GetBytes(text.Length).Concat(Encoding.ASCII.GetBytes(text)).ToArray());

    private static byte[] List(params byte[][] items) =>
        Tagged(4, BitConverter.GetBytes(items.Length + 1).Concat(items.SelectMany(i => i)).ToArray());

    private static byte[] Tagged(uint tag, byte[] body) => BitConverter.GetBytes(tag).Concat(body).ToArray();

    // The container of docs/formats/zbd-archive.md: payloads, 148-byte entries, version and count.
    private static byte[] Archive(params (string Name, byte[] Data)[] entries)
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
            var field = new byte[140];
            Encoding.ASCII.GetBytes(name).CopyTo(field, 0);
            writer.Write(field);
            offset += (uint)data.Length;
        }

        writer.Write(1u);
        writer.Write((uint)entries.Length);
        return stream.ToArray();
    }
}
