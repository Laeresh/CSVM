using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CSVM.Extraction;

/// <summary>
/// Encodes 8-bit RGB or RGBA pixels as a PNG, managed and dependency-free. <c>System.Drawing</c> is
/// Windows-only and Godot's image type needs a running engine, which a unit test lacks. Every row
/// is stored unfiltered and the whole image deflated once. That suffices for the extraction's
/// textures and <c>--dump</c>'s, which no player reads.
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>A whole PNG file for <paramref name="rgb"/>, three bytes per pixel, rows top to
    /// bottom with no padding between them.</summary>
    public static byte[] EncodeRgb(int width, int height, ReadOnlySpan<byte> rgb) => Encode(width, height, rgb, 3, 2);

    /// <summary>A whole PNG file for <paramref name="rgba"/>, four bytes per pixel with straight
    /// alpha, rows top to bottom with no padding between them.</summary>
    public static byte[] EncodeRgba(int width, int height, ReadOnlySpan<byte> rgba) => Encode(width, height, rgba, 4, 6);

    /// <summary>Writes <see cref="EncodeRgb"/>'s output to <paramref name="path"/>, replacing
    /// any file there.</summary>
    public static void WriteRgb(string path, int width, int height, ReadOnlySpan<byte> rgb) =>
        File.WriteAllBytes(path, EncodeRgb(width, height, rgb));

    /// <summary>The PNG chunk CRC (ISO 3309, as zlib computes it) over <paramref name="data"/>,
    /// continuing from <paramref name="crc"/>. Public so a test can check a chunk it reads back.
    /// </summary>
    public static uint Crc32(ReadOnlySpan<byte> data, uint crc = 0)
    {
        uint c = ~crc;
        foreach (byte b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return ~c;
    }

    private static byte[] Encode(int width, int height, ReadOnlySpan<byte> pixels, int channels, byte colourType)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "a PNG needs at least one pixel");
        }

        int stride = width * channels;
        if (pixels.Length != stride * height)
        {
            throw new ArgumentException($"pixel buffer is not width * height * {channels} bytes", nameof(pixels));
        }

        using var file = new MemoryStream();
        file.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // bits per channel
        header[9] = colourType; // 2 RGB, 6 RGBA
        WriteChunk(file, "IHDR", header);

        using (var raw = new MemoryStream())
        {
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0); // filter type 0: the row as it stands
                    z.Write(pixels.Slice(y * stride, stride));
                }
            }

            WriteChunk(file, "IDAT", raw.GetBuffer().AsSpan(0, (int)raw.Length));
        }

        WriteChunk(file, "IEND", ReadOnlySpan<byte>.Empty);
        return file.ToArray();
    }

    private static void WriteChunk(Stream file, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        file.Write(word);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        file.Write(typeBytes);
        file.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(data, Crc32(typeBytes)));
        file.Write(word);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
