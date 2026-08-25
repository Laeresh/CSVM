using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The one door menu art is loaded through: the extension picks the decoder, and anything
/// with no decoder comes back null rather than as a stand-in picture.</summary>
public class ArtImageTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "csvm-artimage-" + Guid.NewGuid().ToString("N"));

    /// <summary>Makes the scratch directory the cases below write their files into.</summary>
    public ArtImageTests() => Directory.CreateDirectory(_dir);

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ItDecodesPngThroughTheSameDoorAsTga()
    {
        string path = Write("art.PNG", Png(1, 1, new byte[] { 0, 10, 20, 30 }));

        var image = ArtImage.TryLoad(path);

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, image!.Rgba);
    }

    [Fact]
    public void ItDecodesTgaThroughTheSameDoor()
    {
        string path = Write("art.tga", Tga(20, 30, 40));

        var image = ArtImage.TryLoad(path);

        Assert.NotNull(image);
        Assert.Equal(new byte[] { 20, 30, 40, 255 }, image!.Rgba);
    }

    // A JPEG is a real file with real pixels and no decoder here, which is exactly the case that
    // must not produce a stand-in: a wrong picture on a fidelity screen reads as a verdict.
    [Fact]
    public void ItRefusesAFormatWithNoDecoderRatherThanInventingOne()
    {
        string path = Write("photo.JPG", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16 });

        Assert.Null(ArtImage.TryLoad(path));
    }

    [Fact]
    public void ItAnswersNullForAnAbsentFile() =>
        Assert.Null(ArtImage.TryLoad(Path.Combine(_dir, "missing.png")));

    private static byte[] Tga(byte r, byte g, byte b)
    {
        // An 18-byte header (type 2 truecolour, 1x1, 24-bit, top-down) then one BGR pixel.
        var tga = new byte[18 + 3];
        tga[2] = 2;
        tga[12] = 1;
        tga[14] = 1;
        tga[16] = 24;
        tga[17] = 0x20;
        tga[18] = b;
        tga[19] = g;
        tga[20] = r;
        return tga;
    }

    // A minimal truecolour PNG, the shape PngImageTests builds: signature, IHDR, one deflated
    // IDAT of filtered rows, IEND, with zero CRCs the decoder skips.
    private static byte[] Png(int width, int height, byte[] rows)
    {
        var png = new List<byte> { 137, 80, 78, 71, 13, 10, 26, 10 };
        var ihdr = new List<byte>();
        ihdr.AddRange(BigEndian(width));
        ihdr.AddRange(BigEndian(height));
        ihdr.AddRange(new byte[] { 8, 2, 0, 0, 0 });
        png.AddRange(Chunk("IHDR", ihdr.ToArray()));
        png.AddRange(Chunk("IDAT", Deflate(rows)));
        png.AddRange(Chunk("IEND", Array.Empty<byte>()));
        return png.ToArray();
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(raw, 0, raw.Length);
        }

        return buffer.ToArray();
    }

    private static byte[] Chunk(string type, byte[] body)
    {
        var chunk = new List<byte>();
        chunk.AddRange(BigEndian(body.Length));
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        chunk.AddRange(body);
        chunk.AddRange(new byte[4]);
        return chunk.ToArray();
    }

    private static byte[] BigEndian(int value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value,
    };

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
