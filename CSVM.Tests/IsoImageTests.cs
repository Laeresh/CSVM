using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CSVM.Extraction;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

public class IsoImageTests
{
    private const int Sector = 2048;
    private const int RootLba = 18;

    private static readonly byte[] Big = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();

    [Fact]
    public void NamesLoseTheirVersionAndMatchWithoutRegardToCase()
    {
        var iso = IsoImage.Read(WriteImage(DiscLayout()));

        Assert.Equal(
            new[] { "GOSDATA/ASSETS/CRIMSON.ROF", "MixedCase.Txt", "STRINGS.DLL", "ZBD/BIG.ZBD", "ZBD/C1.CAB", "ZBD/NOEXT", "ZBD/ZRDR.ZBD" },
            iso.Files.OrderBy(f => f, StringComparer.Ordinal));
        Assert.True(iso.Exists("zbd/zrdr.zbd"));
        Assert.True(iso.Exists(@"ZBD\Zrdr.Zbd"));
        Assert.True(iso.Exists("mixedcase.TXT"));
        Assert.True(iso.Exists("ZBD/noext"));
        Assert.True(iso.Exists("/ZBD//C1.CAB"));
        Assert.Equal(Encoding.ASCII.GetBytes("zrdr"), iso.ReadAllBytes("ZBD/ZRDR.ZBD"));
    }

    [Fact]
    public void AFileIsASeekableWindowOntoTheImage()
    {
        var iso = IsoImage.Read(WriteImage(DiscLayout()));
        using var first = iso.Open("ZBD/BIG.ZBD")!;
        using var second = iso.Open("ZBD/BIG.ZBD")!;

        Assert.True(first.CanSeek);
        Assert.Equal(Big.Length, first.Length);
        first.Seek(3000, SeekOrigin.Begin);
        var slice = new byte[100];
        first.ReadExactly(slice);
        Assert.Equal(Big.Skip(3000).Take(100), slice);
        Assert.Equal(Big.Take(10), Read(second, 10));
        first.Seek(-4, SeekOrigin.End);
        Assert.Equal(Big.TakeLast(4), Read(first, 100));
        Assert.Equal(0, first.Read(new byte[8], 0, 8));
        Assert.Equal(Big, iso.ReadAllBytes("zbd/big.zbd"));
    }

    [Fact]
    public void AMissingFolderOrEscapingPathIsNotFound()
    {
        string image = WriteImage(DiscLayout());
        var iso = IsoImage.Read(image);

        Assert.False(iso.Exists("ZBD/none.zbd"));
        Assert.Null(iso.Open("ZBD/none.zbd"));
        Assert.False(iso.Exists("ZBD"));
        Assert.False(iso.Exists("README.TXT"));
        Assert.False(iso.Exists("../README.TXT"));
        Assert.False(iso.Exists("ZBD/../STRINGS.DLL"));
        Assert.False(iso.Exists("./STRINGS.DLL"));
        var missing = Assert.Throws<FileNotFoundException>(() => iso.ReadAllBytes("ZBD/none.zbd"));
        Assert.Contains(Path.GetFullPath(image), missing.Message);
    }

    [Fact]
    public void GameFileSourceOpensAnIsoHeldToTheInstallRule()
    {
        var disc = GameFileSource.Open(WriteImage(DiscLayout()));
        Assert.IsType<IsoImage>(disc);
        Assert.True(disc!.Exists("ZBD/C1.CAB"));

        // An image of an install has no GAME folder and is rooted at the image root.
        var install = GameFileSource.Open(WriteImage(("ZBD/ZRDR.ZBD;1", new byte[] { 1 }), ("GOSDATA/ASSETS/CRIMSON.ROF;1", new byte[] { 2 })));
        Assert.Equal(new byte[] { 1 }, install!.ReadAllBytes("zbd/zrdr.zbd"));

        Assert.Null(GameFileSource.Open(WriteImage(("GAME/ZBD/ZRDR.ZBD;1", new byte[] { 1 }))));
        Assert.Null(GameFileSource.Open(WriteBytes(new byte[40 * Sector])));
        Assert.Null(GameFileSource.Open(Path.Combine(TestData.TempDir(), "absent.iso")));
    }

    [Fact]
    public void AMalformedImageIsRefused()
    {
        var good = Build(DiscLayout());

        Assert.Throws<InvalidDataException>(() => IsoImage.Read(WriteBytes(new byte[100])));
        Assert.Throws<InvalidDataException>(() => IsoImage.Read(WriteBytes(new byte[40 * Sector])));
        Assert.Throws<InvalidDataException>(() => IsoImage.Read(WriteBytes(Patched(good, (16 * Sector) + 158, 1_000_000))));
        var shortRecord = (byte[])good.Clone();
        shortRecord[RootLba * Sector] = 20;
        Assert.Throws<InvalidDataException>(() => IsoImage.Read(WriteBytes(shortRecord)));
        int zbd = RecordAt(good, ReadLba(good, RecordAt(good, RootLba, "GAME")), "ZBD");
        int bigFile = RecordAt(good, ReadLba(good, zbd), "BIG.ZBD;1");
        Assert.Throws<InvalidDataException>(() => IsoImage.Read(WriteBytes(Patched(good, bigFile + 10, int.MaxValue))));
    }

    [Fact]
    public void ADirectoryPointingAtAnAncestorEnds()
    {
        var image = Build(DiscLayout());
        int game = RecordAt(image, RootLba, "GAME");
        int zbd = RecordAt(image, ReadLba(image, game), "ZBD");

        var iso = IsoImage.Read(WriteBytes(Patched(image, zbd + 2, RootLba)));
        Assert.True(iso.Exists("STRINGS.DLL"));
        Assert.False(iso.Exists("ZBD/ZRDR.ZBD"));
    }

    private static (string Path, byte[] Data)[] DiscLayout() => new[]
    {
        ("README.TXT;1", Encoding.ASCII.GetBytes("readme")),
        ("GAME/STRINGS.DLL;1", Encoding.ASCII.GetBytes("strings")),
        ("GAME/MixedCase.Txt", Encoding.ASCII.GetBytes("mixed")),
        ("GAME/ZBD/ZRDR.ZBD;1", Encoding.ASCII.GetBytes("zrdr")),
        ("GAME/ZBD/C1.CAB;1", Encoding.ASCII.GetBytes("cabinet")),
        ("GAME/ZBD/NOEXT.;1", Array.Empty<byte>()),
        ("GAME/ZBD/BIG.ZBD;1", Big),
        ("GAME/GOSDATA/ASSETS/CRIMSON.ROF;1", Encoding.ASCII.GetBytes("rof")),
    };

    private static byte[] Read(Stream stream, int count)
    {
        var buffer = new byte[count];
        int read = stream.Read(buffer, 0, count);
        return buffer.Take(read).ToArray();
    }

    private static string WriteImage(params (string Path, byte[] Data)[] files) => WriteBytes(Build(files));

    private static string WriteBytes(byte[] image)
    {
        string path = Path.Combine(TestData.TempDir(), "disc.iso");
        File.WriteAllBytes(path, image);
        return path;
    }

    private static byte[] Patched(byte[] image, int at, uint value)
    {
        var copy = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(at), value);
        return copy;
    }

    private static uint ReadLba(byte[] image, int record) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(record + 2));

    // The byte offset in the image of the record named name in the directory at sector lba.
    private static int RecordAt(byte[] image, uint lba, string name)
    {
        for (int at = (int)lba * Sector; image[at] != 0; at += image[at])
        {
            if (Encoding.ASCII.GetString(image, at + 33, image[at + 32]) == name)
            {
                return at;
            }
        }

        throw new InvalidOperationException($"no record '{name}' in sector {lba}");
    }

    // A minimal ISO 9660 image. The primary descriptor sits at sector 16 and the terminator at 17.
    // One sector per directory follows from 18, root first, then each file's data. Paths hold
    // the stored names, version suffix included.
    private static byte[] Build((string Path, byte[] Data)[] files)
    {
        var dirs = new SortedSet<string>(StringComparer.Ordinal) { string.Empty };
        foreach (var (path, _) in files)
        {
            for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
            {
                dirs.Add(path[..slash]);
            }
        }

        var dirLba = dirs.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => (uint)(RootLba + x.i));
        uint next = RootLba + (uint)dirs.Count;
        var fileLba = new Dictionary<string, uint>();
        foreach (var (path, data) in files)
        {
            fileLba[path] = next;
            next += (uint)Math.Max(1, (data.Length + Sector - 1) / Sector);
        }

        var image = new byte[next * Sector];
        int pvd = 16 * Sector;
        image[pvd] = 1;
        "CD001"u8.CopyTo(image.AsSpan(pvd + 1));
        image[pvd + 6] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(pvd + 128), Sector);
        Record(new byte[] { 0 }, RootLba, Sector, true).CopyTo(image, pvd + 156);
        image[17 * Sector] = 255;
        "CD001"u8.CopyTo(image.AsSpan((17 * Sector) + 1));

        foreach (string dir in dirs)
        {
            string parent = dir.Contains('/') ? dir[..dir.LastIndexOf('/')] : string.Empty;
            var records = new List<byte[]> { Record(new byte[] { 0 }, dirLba[dir], Sector, true), Record(new byte[] { 1 }, dirLba[parent], Sector, true) };
            string prefix = dir.Length == 0 ? string.Empty : dir + "/";
            records.AddRange(dirs.Where(d => d.Length > prefix.Length && d.StartsWith(prefix, StringComparison.Ordinal) && !d[prefix.Length..].Contains('/'))
                .Select(d => Record(Encoding.ASCII.GetBytes(d[prefix.Length..]), dirLba[d], Sector, true)));
            records.AddRange(files.Where(f => f.Path.StartsWith(prefix, StringComparison.Ordinal) && !f.Path[prefix.Length..].Contains('/'))
                .Select(f => Record(Encoding.ASCII.GetBytes(f.Path[prefix.Length..]), fileLba[f.Path], (uint)f.Data.Length, false)));
            int at = (int)dirLba[dir] * Sector;
            foreach (var record in records)
            {
                record.CopyTo(image, at);
                at += record.Length;
            }
        }

        foreach (var (path, data) in files)
        {
            data.CopyTo(image, fileLba[path] * Sector);
        }

        return image;
    }

    private static byte[] Record(byte[] name, uint lba, uint size, bool directory)
    {
        var record = new byte[33 + name.Length + ((name.Length + 1) % 2)];
        record[0] = (byte)record.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(2), lba);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), size);
        record[25] = directory ? (byte)2 : (byte)0;
        record[32] = (byte)name.Length;
        name.CopyTo(record, 33);
        return record;
    }
}

/// <summary>Reads the real disc image the <see cref="Variable"/> environment variable names, and
/// skips without one.</summary>
public class IsoImageDataTests
{
    /// <summary>The environment variable naming a Crimson Skies <c>.iso</c> for the real-image check.</summary>
    public const string Variable = "CSVM_TEST_ISO";

    private readonly ITestOutputHelper _output;

    public IsoImageDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [RealIsoFact]
    public void TheRealImageHoldsTheRootArchivesAndMatchesTheInstall()
    {
        var iso = Assert.IsType<IsoImage>(GameFileSource.Open(Environment.GetEnvironmentVariable(Variable)!));
        foreach (string root in ZbdManifest.Expected.Where(path => !path.Contains('/')))
        {
            Assert.True(iso.Exists("ZBD/" + root), $"ZBD/{root} is not in the image");
        }

        Assert.Contains(iso.Files, f => f.StartsWith("ZBD/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".CAB", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"{iso.Files.Count} files in the image");
        if (GameFilesData.Source is not { } install)
        {
            _output.WriteLine(GameFilesData.NoSourceReason + ", so no byte comparison");
            return;
        }

        var differ = new List<string>();
        int compared = 0;
        foreach (string file in iso.Files.Where(f => !f.EndsWith(".CAB", StringComparison.OrdinalIgnoreCase) && install.Exists(f)))
        {
            using var fromIso = iso.Open(file)!;
            using var fromInstall = install.Open(file)!;
            if (!SHA256.HashData(fromIso).AsSpan().SequenceEqual(SHA256.HashData(fromInstall)))
            {
                differ.Add(file);
            }

            compared++;
        }

        _output.WriteLine($"{compared} files compared with the install at {install.Location}");
        Assert.True(compared > 0);
        Assert.Empty(differ);
    }
}

/// <summary>A fact that runs only when <see cref="IsoImageDataTests.Variable"/> names an existing file.</summary>
public sealed class RealIsoFactAttribute : FactAttribute
{
    public RealIsoFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(IsoImageDataTests.Variable)))
        {
            Skip = $"no disc image: set {IsoImageDataTests.Variable} to a Crimson Skies .iso";
        }
    }
}
