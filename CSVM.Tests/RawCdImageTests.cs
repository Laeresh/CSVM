using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

public class RawCdImageTests
{
    private const int Sector = 2048;
    private const int Raw = 2352;

    [Theory]
    [InlineData("MODE1/2352")]
    [InlineData("MODE2/2352")]
    [InlineData("MODE2/2336")]
    [InlineData("MODE1/2048")]
    public void ACueTrackReadsAsTheIsoItHolds(string mode)
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "disc.bin"), Track(iso, mode));
        string cue = WriteCue(folder, $"FILE \"disc.bin\" BINARY\n  TRACK 01 {mode}\n    INDEX 01 00:00:00\n");

        AssertSameAsIso(iso, RawCdImage.Read(cue));
    }

    [Fact]
    public void AReadSpanningSectorsSkipsEachSectorsHeader()
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "disc.bin"), Track(iso, "MODE1/2352"));
        var image = RawCdImage.Read(WriteCue(folder, "FILE \"disc.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n"));
        var big = new IsoSource(iso).Read("ZBD/BIG.ZBD");
        using var stream = image.Open("zbd/big.zbd")!;

        stream.Seek(2040, SeekOrigin.Begin);
        var across = new byte[2100];
        stream.ReadExactly(across);
        Assert.Equal(big.Skip(2040).Take(2100), across);
        stream.Seek(-3, SeekOrigin.End);
        Assert.Equal(3, stream.Read(new byte[10], 0, 10));
        Assert.Equal(0, stream.Read(new byte[10], 0, 10));
    }

    [Theory]
    [InlineData(1, "MODE1/2352")]
    [InlineData(2, "MODE2/2352")]
    public void ACloneCdSheetReadsTheImgBesideIt(int mode, string layout)
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "DISC.IMG"), Track(iso, layout));
        string ccd = Path.Combine(folder, "disc.ccd");
        File.WriteAllText(ccd, CloneCd(mode));

        AssertSameAsIso(iso, RawCdImage.Read(ccd));
        AssertSameAsIso(iso, RawCdImage.Read(Path.Combine(folder, "DISC.IMG")));
    }

    [Fact]
    public void TheCuesFileIsFoundBesideItWithoutRegardToCase()
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "crimson.img"), Track(iso, "MODE1/2352"));
        Directory.CreateDirectory(Path.Combine(folder, "tracks"));
        File.WriteAllBytes(Path.Combine(folder, "tracks", "Data.Bin"), Track(iso, "MODE1/2352"));

        AssertSameAsIso(iso, RawCdImage.Read(WriteCue(folder, "FILE \"CRIMSON.IMG\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n")));
        AssertSameAsIso(iso, RawCdImage.Read(WriteCue(folder, "file \"D:\\dumps\\Crimson.img\" binary\ntrack 01 mode1/2352\nindex 01 00:00:00\n")));
        AssertSameAsIso(iso, RawCdImage.Read(WriteCue(folder, "FILE \"TRACKS/DATA.BIN\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n")));
        var missing = Assert.Throws<FileNotFoundException>(() => RawCdImage.Read(WriteCue(folder, "FILE \"absent.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n")));
        Assert.Contains("absent.bin", missing.Message);
    }

    [Fact]
    public void AMixedModeSheetEndsTheDataTrackAtTheAudio()
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        int sectors = iso.Length / Sector;
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "mixed.bin"), Track(iso, "MODE1/2352").Concat(Audio(300)).ToArray());
        string audioAt = Msf(sectors + 2);
        string cue = WriteCue(folder, $"FILE \"mixed.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\nTRACK 02 AUDIO\nINDEX 00 {Msf(sectors)}\nINDEX 01 {audioAt}\n");

        var track = CueSheet.DataTrack(cue);
        Assert.Equal((long)sectors * Raw, track.End);
        AssertSameAsIso(iso, RawCdImage.Read(cue));

        // A per-track dump names each track's own file.
        File.WriteAllBytes(Path.Combine(folder, "track01.bin"), Track(iso, "MODE1/2352"));
        File.WriteAllBytes(Path.Combine(folder, "track02.bin"), Audio(300));
        string split = WriteCue(folder, "FILE \"track01.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\nFILE \"track02.bin\" BINARY\nTRACK 02 AUDIO\nINDEX 00 00:00:00\nINDEX 01 00:02:00\n");
        Assert.Null(CueSheet.DataTrack(split).End);
        AssertSameAsIso(iso, RawCdImage.Read(split));
    }

    [Fact]
    public void AudioFirstOrAudioOnlyIsRefused()
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "extra.bin"), Audio(300).Concat(Track(iso, "MODE1/2352")).ToArray());

        var extra = Assert.Throws<InvalidDataException>(() => RawCdImage.Read(WriteCue(folder, "FILE \"extra.bin\" BINARY\nTRACK 01 AUDIO\nINDEX 01 00:00:00\nTRACK 02 MODE1/2352\nINDEX 01 00:04:00\n")));
        Assert.Contains("track 2", extra.Message);
        var audio = Assert.Throws<InvalidDataException>(() => RawCdImage.Read(WriteCue(folder, "FILE \"extra.bin\" BINARY\nTRACK 01 AUDIO\nINDEX 01 00:00:00\n")));
        Assert.Contains("no data track", audio.Message);
        File.WriteAllBytes(Path.Combine(folder, "music.img"), Audio(300));
        File.WriteAllText(Path.Combine(folder, "music.ccd"), CloneCd(0));
        Assert.Contains("no data track", Assert.Throws<InvalidDataException>(() => RawCdImage.Read(Path.Combine(folder, "music.ccd"))).Message);
    }

    [Theory]
    [InlineData("FILE \"disc.bin BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n", "quote")]
    [InlineData("TRACK 01 MODE1/2352\nINDEX 01 00:00:00\n", "before any FILE")]
    [InlineData("FILE \"disc.bin\" BINARY\nINDEX 01 00:00:00\n", "before any TRACK")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:60:00\n", "mm:ss:ff")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 0:0\n", "mm:ss:ff")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 00 00:00:00\n", "no INDEX 01")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE3/2352\nINDEX 01 00:00:00\n", "MODE3/2352")]
    [InlineData("FILE \"disc.bin\" WAVE\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n", "BINARY")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK xx MODE1/2352\nINDEX 01 00:00:00\n", "not a number")]
    [InlineData("FILE \"disc.bin\"\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n", "arguments")]
    [InlineData("REM nothing here\n", "no TRACK")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 99:00:00\n", "starts at byte")]
    [InlineData("FILE \"disc.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n", "no primary volume descriptor")]
    public void AMalformedCueIsRefusedNamingWhy(string sheet, string reason)
    {
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "disc.bin"), Track(IsoImageTests.Build(IsoImageTests.DiscLayout()), "MODE1/2352"));
        string cue = WriteCue(folder, sheet);

        var refused = Assert.Throws<InvalidDataException>(() => RawCdImage.Read(cue));
        Assert.Contains(reason, refused.Message);
        Assert.Null(GameFileSource.Open(cue));
    }

    [Fact]
    public void AMalformedCloneCdSheetIsRefused()
    {
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "disc.img"), Track(IsoImageTests.Build(IsoImageTests.DiscLayout()), "MODE1/2352"));
        string ccd = Path.Combine(folder, "disc.ccd");

        File.WriteAllText(ccd, CloneCd(1).Replace("DataTracksScrambled=0", "DataTracksScrambled=1", StringComparison.Ordinal));
        Assert.Contains("scrambled", Assert.Throws<InvalidDataException>(() => RawCdImage.Read(ccd)).Message);
        File.WriteAllText(ccd, "[CloneCD]\nVersion=3\n");
        Assert.Contains("no [TRACK n]", Assert.Throws<InvalidDataException>(() => RawCdImage.Read(ccd)).Message);
        File.WriteAllText(ccd, CloneCd(1).Replace("INDEX 1=0", "INDEX 1=zz", StringComparison.Ordinal));
        Assert.Contains("not a number", Assert.Throws<InvalidDataException>(() => RawCdImage.Read(ccd)).Message);
        File.WriteAllText(ccd, CloneCd(4));
        Assert.Contains("MODE=4", Assert.Throws<InvalidDataException>(() => RawCdImage.Read(ccd)).Message);
        File.Delete(Path.Combine(folder, "disc.img"));
        File.WriteAllText(ccd, CloneCd(1));
        Assert.Throws<FileNotFoundException>(() => RawCdImage.Read(ccd));
    }

    [Fact]
    public void GameFileSourceOpensASheetOrAnImageWithOneBeside()
    {
        var iso = IsoImageTests.Build(IsoImageTests.DiscLayout());
        string folder = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(folder, "Crimson.img"), Track(iso, "MODE1/2352"));
        string cue = WriteCue(folder, "FILE \"Crimson.img\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n", "CRIMSON.cue");
        File.WriteAllBytes(Path.Combine(folder, "lonely.bin"), Track(iso, "MODE1/2352"));

        var disc = Assert.IsType<IsoImage>(Assert.IsType<CabinetSource>(GameFileSource.Open(cue)).Inner);
        Assert.Equal(cue, disc.Location);
        Assert.True(disc.Exists("zbd/c1.cab"));
        Assert.Equal(cue, RawCdImage.Descriptor(Path.Combine(folder, "Crimson.img")));
        Assert.IsType<IsoImage>(Assert.IsType<CabinetSource>(GameFileSource.Open(Path.Combine(folder, "Crimson.img"))).Inner);
        Assert.Null(RawCdImage.Descriptor(Path.Combine(folder, "lonely.bin")));
        Assert.Null(GameFileSource.Open(Path.Combine(folder, "lonely.bin")));
        Assert.Null(GameFileSource.Open(Path.Combine(folder, "absent.cue")));

        // A volume without the install layout is passed over, as an .iso is.
        File.WriteAllBytes(Path.Combine(folder, "other.bin"), Track(IsoImageTests.Build(new[] { ("GAME/ZBD/ZRDR.ZBD;1", new byte[] { 1 }) }), "MODE1/2352"));
        Assert.Null(GameFileSource.Open(WriteCue(folder, "FILE \"other.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n")));
    }

    private static void AssertSameAsIso(byte[] iso, IsoImage raw)
    {
        var plain = new IsoSource(iso);
        Assert.Equal(plain.Image.Files.OrderBy(f => f, StringComparer.Ordinal), raw.Files.OrderBy(f => f, StringComparer.Ordinal));
        foreach (string file in plain.Image.Files)
        {
            Assert.Equal(plain.Read(file), raw.ReadAllBytes(file));
        }
    }

    private static string WriteCue(string folder, string text, string name = "disc.cue")
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, text.Replace("\n", "\r\n", StringComparison.Ordinal));
        return path;
    }

    private static string Msf(int sectors) =>
        string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", sectors / 75 / 60, sectors / 75 % 60, sectors % 75);

    // Each 2,048-byte sector of the ISO stored as mode says. The header and error-correction bytes
    // hold noise, which a reader that does not skip them would return.
    private static byte[] Track(byte[] iso, string mode)
    {
        (int size, int offset, byte modeByte) = mode switch
        {
            "MODE1/2352" => (Raw, 16, (byte)1),
            "MODE2/2352" => (Raw, 24, (byte)2),
            "MODE2/2336" => (2336, 8, (byte)2),
            _ => (Sector, 0, (byte)0),
        };
        int sectors = iso.Length / Sector;
        var track = new byte[sectors * size];
        new Random(sectors).NextBytes(track);
        for (int s = 0; s < sectors; s++)
        {
            int at = s * size;
            if (size == Raw)
            {
                track[at] = 0;
                track.AsSpan(at + 1, 10).Fill(0xFF);
                track[at + 11] = 0;
                track[at + 15] = modeByte;
            }

            iso.AsSpan(s * Sector, Sector).CopyTo(track.AsSpan(at + offset));
        }

        return track;
    }

    private static byte[] Audio(int sectors)
    {
        var audio = new byte[sectors * Raw];
        new Random(7).NextBytes(audio);
        return audio;
    }

    // A CloneCD sheet of one track in the given MODE, starting at the image's first sector.
    private static string CloneCd(int mode) => string.Join("\r\n", new[]
    {
        "[CloneCD]", "Version=3", "[Disc]", "TocEntries=4", "Sessions=1", "DataTracksScrambled=0", "CDTextLength=0",
        "[Session 1]", "PreGapMode=1", "PreGapSubC=0",
        "[Entry 3]", "Session=1", "Point=0x01", "ADR=0x01", $"Control=0x0{(mode == 0 ? 0 : 4)}", "PLBA=0",
        "[TRACK 1]", $"MODE={mode}", "INDEX 1=0", string.Empty,
    });

    // The plain image the raw ones were built from, read as D32 reads an .iso.
    private sealed class IsoSource
    {
        public IsoSource(byte[] iso)
        {
            string path = Path.Combine(TestData.TempDir(), "plain.iso");
            File.WriteAllBytes(path, iso);
            Image = IsoImage.Read(path);
        }

        public IsoImage Image { get; }

        public byte[] Read(string file) => Image.ReadAllBytes(file);
    }
}

/// <summary>Reads the raw disc image <see cref="Variable"/> names and checks every file on it
/// against the file copy <see cref="CopyVariable"/> names. Skips without an image.</summary>
public class RawCdImageDataTests
{
    /// <summary>The environment variable naming a Crimson Skies <c>.cue</c> or <c>.ccd</c>.</summary>
    public const string Variable = "CSVM_TEST_CUE";

    /// <summary>The environment variable naming a folder holding a file copy of the same disc.</summary>
    public const string CopyVariable = "CSVM_TEST_CUE_COPY";

    private static readonly uint[] EdcTable = Enumerable.Range(0, 256).Select(i =>
    {
        uint value = (uint)i;
        for (int k = 0; k < 8; k++)
        {
            value = (value >> 1) ^ ((value & 1) != 0 ? 0xD8018001u : 0u);
        }

        return value;
    }).ToArray();

    private readonly ITestOutputHelper _output;

    public RawCdImageDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [RealCueFact]
    public void EveryFileOnTheRawImageMatchesTheDiscsFileCopy()
    {
        string sheet = Environment.GetEnvironmentVariable(Variable)!;
        var disc = Assert.IsType<IsoImage>(Assert.IsType<CabinetSource>(GameFileSource.Open(sheet)).Inner);
        Assert.True(disc.Exists("ZBD/ZRDR.ZBD"));
        var whole = RawCdImage.Read(sheet, atGame: false);
        _output.WriteLine($"{whole.Files.Count} files on the disc, {disc.Files.Count} below GAME");
        ReportSectors(sheet);
        if (Environment.GetEnvironmentVariable(CopyVariable) is not { Length: > 0 } copy || !Directory.Exists(copy))
        {
            _output.WriteLine($"no file copy: set {CopyVariable}, so no byte comparison");
            return;
        }

        var clock = Stopwatch.StartNew();
        var differ = new List<string>();
        long bytes = 0;
        int same = 0;
        foreach (string file in whole.Files.OrderBy(f => f, StringComparer.Ordinal))
        {
            string? twin = InstallLocator.ResolveFile(copy, file);
            if (twin == null)
            {
                differ.Add($"{file}: not in the copy");
                continue;
            }

            using var fromImage = whole.Open(file)!;
            using var fromCopy = File.OpenRead(twin);
            if (FirstDifference(fromImage, fromCopy) is { } at)
            {
                differ.Add($"{file}: differs from byte {at} (image {fromImage.Length} bytes, copy {fromCopy.Length})");
            }
            else
            {
                same++;
            }

            bytes += fromImage.Length;
        }

        int copied = Directory.EnumerateFiles(copy, "*", SearchOption.AllDirectories).Count();
        _output.WriteLine($"{whole.Files.Count} files, {bytes} bytes read and compared with {copy} ({copied} files there) in {clock.ElapsedMilliseconds} ms; {same} identical");
        differ.ForEach(_output.WriteLine);
        Assert.Equal(copied, whole.Files.Count);
        Assert.Empty(differ);
    }

    // Both streams are read to the end even past a difference, so the timing is a whole read.
    private static long? FirstDifference(Stream a, Stream b)
    {
        var left = new byte[1 << 16];
        var right = new byte[1 << 16];
        long at = 0;
        long? first = null;
        while (true)
        {
            int n = a.ReadAtLeast(left, left.Length, throwOnEndOfStream: false);
            int m = b.ReadAtLeast(right, right.Length, throwOnEndOfStream: false);
            int same = left.AsSpan(0, Math.Min(n, m)).CommonPrefixLength(right.AsSpan(0, Math.Min(n, m)));
            if (first == null && (same < Math.Min(n, m) || n != m))
            {
                first = at + same;
            }

            if (n == 0 && m == 0)
            {
                return first;
            }

            at += Math.Min(n, m);
        }
    }

    // The CD-ROM EDC: a CRC-32 over the sector's first 2,064 bytes, reflected polynomial
    // 0xD8018001, zero initial value, stored little-endian.
    private static uint Edc(ReadOnlySpan<byte> data)
    {
        uint edc = 0;
        foreach (byte b in data)
        {
            edc = (edc >> 8) ^ EdcTable[(edc ^ b) & 0xFF];
        }

        return edc;
    }

    // A MODE1/2352 track's sectors whose sync, mode byte or EDC is wrong. Those mark a sector the
    // drive could not read, or one the protection made bad on purpose. Reported, not required.
    private void ReportSectors(string sheet)
    {
        var track = sheet.EndsWith(".ccd", StringComparison.OrdinalIgnoreCase) ? (RawDataTrack?)null : CueSheet.DataTrack(sheet);
        if (track is not { SectorSize: 2352, DataOffset: 16 } mode1)
        {
            return;
        }

        var bad = new List<long>();
        var sector = new byte[2352];
        using var file = File.OpenRead(mode1.File);
        file.Position = mode1.Start;
        long count = mode1.Sectors(file.Length, sheet);
        for (long lba = 0; lba < count; lba++)
        {
            file.ReadExactly(sector);
            bool sync = sector[0] == 0 && sector[11] == 0 && sector.AsSpan(1, 10).IndexOfAnyExcept((byte)0xFF) < 0;
            if (!sync || sector[15] != 1 || Edc(sector.AsSpan(0, 2064)) != BitConverter.ToUInt32(sector, 2064))
            {
                bad.Add(lba);
            }
        }

        var runs = new StringBuilder();
        for (int i = 0; i < bad.Count; i++)
        {
            int j = i;
            while (j + 1 < bad.Count && bad[j + 1] == bad[j] + 1)
            {
                j++;
            }

            runs.Append(CultureInfo.InvariantCulture, $" {bad[i]}..{bad[j]}");
            i = j;
        }

        _output.WriteLine($"{count} sectors, {bad.Count} with a bad sync, mode or EDC:{runs}");
    }
}

/// <summary>A fact that runs only when <see cref="RawCdImageDataTests.Variable"/> names an existing file.</summary>
public sealed class RealCueFactAttribute : FactAttribute
{
    public RealCueFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(RawCdImageDataTests.Variable)))
        {
            Skip = $"no raw disc image: set {RawCdImageDataTests.Variable} to a Crimson Skies .cue or .ccd";
        }
    }
}
