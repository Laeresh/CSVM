using System;
using System.Collections.Generic;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's two encoders besides <c>Extraction/PngWriter.cs</c>: a baseline greyscale
/// JPEG and an uncompressed TGA. They exist for the menu art a layout or a script names with a
/// <c>.jpg</c> or <c>.tga</c> extension. A loader picks its decoder by extension, so a PNG under a
/// JPEG name is a decode error rather than a picture. Each writes a checker drawn from the name,
/// so a capture shows which file drew where. Engine-free, like the tree.
/// </summary>
public static class SyntheticImages
{
    // The checker square's side, one JPEG block, so every block is flat and carries a DC term only.
    private const int Square = 8;

    // The standard luminance DC table's code lengths and symbols, categories 0 to 11.
    private static readonly byte[] DcCounts = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] DcSymbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    // One two-bit code, symbol 0x00, end of block.
    private static readonly byte[] AcCounts = { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private static readonly (int Code, int Length)[] DcCodes = Canonical(DcCounts, DcSymbols.Length);

    /// <summary>A baseline JPEG of the name's grey checker, one component over a quantisation table
    /// of ones. Every 8x8 block is flat, so each one codes as a DC difference and an end of block.
    /// The DC table is the JPEG standard's luminance table; the AC table holds end-of-block alone.</summary>
    public static byte[] Jpeg(string name, int width, int height)
    {
        if (width <= 0 || height <= 0 || width > ushort.MaxValue || height > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"{name}: {width}x{height} is not a JPEG size");
        }

        var (lit, unlit) = Shades(name);
        var bytes = new List<byte> { 0xFF, 0xD8 };
        Segment(bytes, 0xE0, new byte[] { (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 });
        var quant = new byte[65];
        Array.Fill(quant, (byte)1);
        quant[0] = 0x00;
        Segment(bytes, 0xDB, quant);
        Segment(bytes, 0xC0, new byte[] { 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0 });
        Segment(bytes, 0xC4, Concat(new byte[] { 0x00 }, DcCounts, DcSymbols));
        Segment(bytes, 0xC4, Concat(new byte[] { 0x10 }, AcCounts, new byte[] { 0x00 }));
        Segment(bytes, 0xDA, new byte[] { 1, 1, 0x00, 0, 63, 0 });

        var bits = new BitSink(bytes);
        int previous = 0;
        for (int by = 0; by < (height + Square - 1) / Square; by++)
        {
            for (int bx = 0; bx < (width + Square - 1) / Square; bx++)
            {
                int shade = (bx + by) % 2 == 0 ? lit : unlit;
                int dc = 8 * (shade - 128);
                int diff = dc - previous;
                previous = dc;
                int category = Category(diff);
                bits.Write(DcCodes[category].Code, DcCodes[category].Length);
                if (category > 0)
                {
                    bits.Write(diff < 0 ? diff + (1 << category) - 1 : diff, category);
                }

                bits.Write(0b00, 2);
            }
        }

        bits.Flush();
        bytes.Add(0xFF);
        bytes.Add(0xD9);
        return bytes.ToArray();
    }

    /// <summary>An uncompressed 24-bit TGA of the name's checker, stored top row first, the type
    /// and origin <c>Mech3/TgaImage.cs</c> and the engine's loader both read.</summary>
    public static byte[] Tga(string name, int width, int height)
    {
        if (width <= 0 || height <= 0 || width > ushort.MaxValue || height > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"{name}: {width}x{height} is not a TGA size");
        }

        var tga = new byte[18 + (width * height * 3)];
        tga[2] = 2;
        tga[12] = (byte)width;
        tga[13] = (byte)(width >> 8);
        tga[14] = (byte)height;
        tga[15] = (byte)(height >> 8);
        tga[16] = 24;
        tga[17] = 0x20;
        byte[] rgb = SyntheticTextures.Checker(name, width, height);
        for (int p = 0; p < width * height; p++)
        {
            tga[18 + (p * 3)] = rgb[(p * 3) + 2];
            tga[18 + (p * 3) + 1] = rgb[(p * 3) + 1];
            tga[18 + (p * 3) + 2] = rgb[p * 3];
        }

        return tga;
    }

    // The two greys of a name's checker, apart enough to read and drawn from the name's hash.
    private static (int Lit, int Unlit) Shades(string name)
    {
        uint hash = 2166136261;
        foreach (char c in name)
        {
            hash = (hash ^ c) * 16777619;
        }

        int lit = 96 + (int)(hash & 0x7F);
        return (lit, lit - 64);
    }

    private static int Category(int value)
    {
        int magnitude = Math.Abs(value);
        int category = 0;
        while (magnitude > 0)
        {
            category++;
            magnitude >>= 1;
        }

        return category;
    }

    // The canonical Huffman codes for a count table, in symbol order.
    private static (int Code, int Length)[] Canonical(byte[] counts, int symbols)
    {
        var codes = new (int, int)[symbols];
        int code = 0;
        int at = 0;
        for (int length = 1; length <= counts.Length; length++)
        {
            for (int i = 0; i < counts[length - 1]; i++)
            {
                codes[at++] = (code++, length);
            }

            code <<= 1;
        }

        return codes;
    }

    private static void Segment(List<byte> bytes, byte marker, byte[] body)
    {
        bytes.Add(0xFF);
        bytes.Add(marker);
        bytes.Add((byte)((body.Length + 2) >> 8));
        bytes.Add((byte)(body.Length + 2));
        bytes.AddRange(body);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var all = new List<byte>();
        foreach (var part in parts)
        {
            all.AddRange(part);
        }

        return all.ToArray();
    }

    // The entropy-coded segment's bit writer: most significant bit first, a 0x00 stuffed after
    // every 0xFF, and the last byte padded with ones.
    private sealed class BitSink
    {
        private readonly List<byte> _bytes;
        private int _buffer;
        private int _count;

        public BitSink(List<byte> bytes) => _bytes = bytes;

        public void Write(int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                _buffer = (_buffer << 1) | ((value >> i) & 1);
                if (++_count == 8)
                {
                    Emit();
                }
            }
        }

        public void Flush()
        {
            while (_count != 0)
            {
                Write(1, 1);
            }
        }

        private void Emit()
        {
            _bytes.Add((byte)_buffer);
            if (_buffer == 0xFF)
            {
                _bytes.Add(0x00);
            }

            _buffer = 0;
            _count = 0;
        }
    }
}
