using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>The cabinet reader, the LZX decoder and the cabinet layer over self-made cabinets
/// written by Windows' <c>makecab</c> (CI-safe). The LZX one holds <see cref="Program"/> as
/// <c>GAME\ZBD\C2B\gamez.zbd</c> over two frames, five aliased texture tiers and two readers.</summary>
public class CabinetTests
{
    // makecab, CompressionType=LZX, CompressionMemory=21; one folder of two data blocks.
    private const string LzxCabinet =
        "TVNDRgAAAAC3AwAAAAAAACwAAAAAAAAAAwEBAAgAAAAYOwAAdQEAAAIAAxUgywAAAAAAAAAAWl2JmyAAR0FNRVxaQkRcQzJC"
        + "XGdhbWV6LnpiZAAGAAAAIMsAAAAAWl2JmyAAR0FNRVxaQkRcQzJCXFJURVhUVX4xLlpCRAAGAAAAJssAAAAAWl2JmyAAR0FN"
        + "RVxaQkRcQzJCXFJURVhUVX4yLlpCRAAGAAAALMsAAAAAWl2JmyAAR0FNRVxaQkRcQzJCXFJURVhUVX4zLlpCRAAGAAAAMssA"
        + "AAAAWl2JmyAAR0FNRVxaQkRcQzJCXFJURVhUVX40LlpCRAAGAAAAOMsAAAAAWl2JmyAAR0FNRVxaQkRcQzJCXFJUNjBBQ34x"
        + "LlpCRAAOAAAAPssAAAAAWl2JmyAAR0FNRVxaQkRcQzJCXHpyZHIuemJkAA4AAABMywAAAABaXYmbIABHQU1FXFpCRFxDMkJc"
        + "SUExXHpyZHIuemJkAAX52GzmAQCAW4CAjQwgpLVACAABAABFMwAANgBBaAQJEKQ4QdSi/xG934HrwW9qXJOSloS4u9OaYVKp"
        + "SS5xxKWXurUTFy4AEAEAApoCqBAC5nraF9AfVtxOSHIi+RGREXrx/Yj1+wBBAAAAABkAGaJzAGZy9St+pP74N2/J50ejTyfu"
        + "xX/5Tr8/XXoe9bKZ/Mvr89OzZ98vz+dfbtGn7J7/vHtO8fJZ/cvw9cPH5/8u33dGp85devb2cpn9y+r22a/nVi/2rOhlnt2/"
        + "3AZfv3u31OHij4urbV1HFqdarGzxt8XsFvFbxXC2x6PX065Wi3gtYraL2y12uPjjYmzb19HFaRarW7xtMbvF/BYxXL9x7OjM"
        + "zMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzM"
        + "zMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzMzN"
        + "zNmOmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmfmSiCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB"
        + "AEC65Y7ZkUwAWkt+8V3dAEAAAAAAAAD+dwCKAAAAAAAABB8AUAAAAAAAAAAAAAAfAMvR5uXV5Bv9NztwduPsfvdPHkc/8/N8"
        + "eX+f/vC8PPz8b778ywCA";

    // makecab, Compress=off: reader.txt, "chapter reader".
    private const string StoredCabinet =
        "TVNDRgAAAABdAAAAAAAAACwAAAAAAAAAAwEBAAEAAAABBAAARwAAAAEAAAAOAAAAAAAAAAAAWl2JmyAAcmVhZGVyLnR4dAAZDXw0DgAOAGNoYXB0ZXIgcmVhZGVy";

    // makecab, CompressionType=MSZIP: the same file.
    private const string MszipCabinet =
        "TVNDRgAAAABhAAAAAAAAACwAAAAAAAAAAwEBAAEAAAABBAAARwAAAAEAAQAOAAAAAAAAAAAAWl2JmyAAcmVhZGVyLnR4dAB5ewzgEgAOAENLS85ILChJLVIoSk1MSS0CAA==";

    [Fact]
    public void AnLzxFolderUnpacksToItsFiles()
    {
        using var stream = new MemoryStream(Convert.FromBase64String(LzxCabinet));
        var cabinet = Cabinet.Read(stream, "C2B.CAB");

        Assert.Equal(1, cabinet.FolderCount);
        Assert.Equal(3, cabinet.CompressionOf(0));
        Assert.Equal(@"GAME\ZBD\C2B\gamez.zbd", cabinet.Files[0].Name);
        Assert.Equal(@"GAME\ZBD\C2B\IA1\zrdr.zbd", cabinet.Files[^1].Name);
        var data = cabinet.Unpack(stream, 0);
        var gamez = cabinet.Files[0];
        Assert.Equal(Program(), data.AsSpan((int)gamez.Offset, (int)gamez.Length).ToArray());
        var last = cabinet.Files[^1];
        Assert.Equal("mission reader", Encoding.ASCII.GetString(data, (int)last.Offset, (int)last.Length));
    }

    [Fact]
    public void AStoredFolderUnpacksAndMszipIsRefused()
    {
        using var stored = new MemoryStream(Convert.FromBase64String(StoredCabinet));
        var cabinet = Cabinet.Read(stored, "OFF.CAB");
        Assert.Equal("reader.txt", cabinet.Files.Single().Name);
        Assert.Equal("chapter reader", Encoding.ASCII.GetString(cabinet.Unpack(stored, 0)));

        using var mszip = new MemoryStream(Convert.FromBase64String(MszipCabinet));
        var refused = Assert.Throws<InvalidDataException>(() => Cabinet.Read(mszip, "MSZIP.CAB").Unpack(mszip, 0));
        Assert.Contains("compression type 1", refused.Message);
    }

    [Fact]
    public void AMalformedCabinetIsRefused()
    {
        var bytes = Convert.FromBase64String(LzxCabinet);
        Assert.Throws<InvalidDataException>(() => Cabinet.Read(new MemoryStream(new byte[64]), "zero"));

        var split = (byte[])bytes.Clone();
        split[30] = 2;
        Assert.Throws<InvalidDataException>(() => Cabinet.Read(new MemoryStream(split), "split"));

        // The first compressed data block cut to half its length.
        var cut = (byte[])bytes.Clone();
        cut[0x175 + 4] /= 2;
        using var stream = new MemoryStream(cut);
        Assert.Throws<InvalidDataException>(() => Cabinet.Read(stream, "cut").Unpack(stream, 0));
    }

    [Fact]
    public void UncompressedLzxBlocksCarryTheirBytesAndPadOddLengths()
    {
        var bits = new BitWriter();
        bits.Write(0, 1);
        foreach (string text in new[] { "fives", "abc" })
        {
            bits.Write(3, 3);
            bits.Write(text.Length, 24);
            bits.AlignWithPadding();
            bits.Raw(new byte[12]);
            bits.Raw(Encoding.ASCII.GetBytes(text));
            bits.Raw(new byte[text.Length % 2]);
        }

        Assert.Equal("fivesabc", Encoding.ASCII.GetString(LzxDecoder.Decode(bits.ToArray(), 15, 8)));
        Assert.Throws<InvalidDataException>(() => LzxDecoder.Decode(bits.ToArray(), 15, 9));
        Assert.Throws<InvalidDataException>(() => LzxDecoder.Decode(bits.ToArray(), 22, 8));
    }

    [Fact]
    public void ADiscFolderReadsItsChapterFromTheCabinetUnderInstallNames()
    {
        string disc = TestData.TempDir();
        string game = Path.Combine(disc, "GAME");
        Directory.CreateDirectory(Path.Combine(game, "GOSDATA", "ASSETS"));
        Directory.CreateDirectory(Path.Combine(game, "ZBD"));
        File.WriteAllBytes(Path.Combine(game, "ZBD", "C2B.CAB"), Convert.FromBase64String(LzxCabinet));
        File.WriteAllText(Path.Combine(game, "ZBD", "ZRDR.ZBD"), "root reader");

        var source = Assert.IsType<CabinetSource>(GameFileSource.Open(disc));
        Assert.Equal(Path.GetFullPath(game), source.Location);
        Assert.Equal(0, source.UnpackedBytes);
        Assert.True(source.Exists("ZBD/C2B/rtexture9.zbd"));
        Assert.Equal(0, source.UnpackedBytes);
        foreach (int tier in new[] { 2, 4, 6, 8, 9 })
        {
            Assert.Equal($"tier {tier}", Encoding.ASCII.GetString(source.ReadAllBytes($"zbd/c2b/RTEXTURE{tier}.ZBD")));
        }

        Assert.True(source.UnpackedBytes > 0);
        Assert.Equal(Program(), source.ReadAllBytes(@"ZBD\C2B\gamez.zbd"));
        Assert.Equal("mission reader", Encoding.ASCII.GetString(source.ReadAllBytes("ZBD/C2B/ia1/ZRDR.zbd")));
        Assert.Equal("chapter reader", Encoding.ASCII.GetString(source.ReadAllBytes("ZBD/C2B/zrdr.zbd")));
        Assert.Equal("root reader", Encoding.ASCII.GetString(source.ReadAllBytes("ZBD/zrdr.zbd")));
        Assert.False(source.Exists("ZBD/C2B/RTEXTU~1.ZBD"));
        Assert.Null(source.Open("ZBD/C2B/none.zbd"));
        Assert.False(source.Exists("ZBD/C1/zrdr.zbd"));
        Assert.True(source.Exists("ZBD/C2B.CAB"));
        using var stream = source.Open("ZBD/C2B/gamez.zbd")!;
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);

        // The GAME folder picked directly is the same disc.
        Assert.Equal(source.Location, Assert.IsType<CabinetSource>(GameFileSource.Open(game)).Location);
    }

    // Text rows each holding an x86 call (0xE8 and a 4-byte relative operand) to one of 16
    // targets. The encoder's E8 translation turns each operand absolute, so the decoder must
    // turn it back.
    private static byte[] Program()
    {
        var bytes = new List<byte>();
        for (int k = 0; k < 4000; k++)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes($"row {k % 50:D2} "));
            int call = bytes.Count;
            bytes.Add(0xE8);
            bytes.AddRange(BitConverter.GetBytes(((k % 16) * 1000) - call));
            bytes.Add((byte)'\n');
        }

        return bytes.ToArray();
    }

    // LZX's bit order: 16-bit little-endian words, each filled from its high bit.
    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = new();
        private int _word;
        private int _count;

        public void Write(int value, int bits)
        {
            for (int i = bits - 1; i >= 0; i--)
            {
                _word = (_word << 1) | ((value >> i) & 1);
                if (++_count == 16)
                {
                    Flush();
                }
            }
        }

        // An uncompressed block pads 1 to 16 bits, so an aligned stream takes a whole word.
        public void AlignWithPadding() => Write(0, 16 - _count);

        public void Raw(byte[] bytes) => _bytes.AddRange(bytes);

        public byte[] ToArray() => _bytes.ToArray();

        private void Flush()
        {
            _bytes.Add((byte)_word);
            _bytes.Add((byte)(_word >> 8));
            _word = 0;
            _count = 0;
        }
    }
}
