using System;
using System.IO;

namespace CSVM.Mech3;

/// <summary>
/// A decoded TGA as raw top-down RGBA8 bytes, engine-free. Covers exactly what ships under
/// <c>extracted/rof/ASSETS/GRAPHICS</c>: uncompressed (type 2) and RLE (type 10) truecolour at 24
/// or 32 bits, both row orders (TGA rows are bottom-up unless descriptor bit 5 says otherwise).
/// Anything else — colour-mapped, greyscale, right-to-left, truncated — decodes as null rather
/// than throwing: hangar art is optional by design and a bad file must not take a menu down.
/// </summary>
public sealed class TgaImage
{
    private TgaImage(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    /// <summary>Pixel width.</summary>
    public int Width { get; }

    /// <summary>Pixel height.</summary>
    public int Height { get; }

    /// <summary>Top-down rows of RGBA8, <see cref="Width"/> x <see cref="Height"/> x 4 bytes.
    /// A 24-bit source gets an opaque alpha.</summary>
    public byte[] Rgba { get; }

    /// <summary>Reads and decodes one file, or null when it is absent or not a TGA this decoder
    /// covers. The path is the caller's business (absolute, under the session's data root).</summary>
    public static TgaImage? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Decodes one TGA, or null for anything outside this decoder's coverage.</summary>
    public static TgaImage? Decode(ReadOnlySpan<byte> tga)
    {
        if (tga.Length < 18)
        {
            return null;
        }

        int idLength = tga[0];
        int colourMapType = tga[1];
        int type = tga[2];
        int width = tga[12] | (tga[13] << 8);
        int height = tga[14] | (tga[15] << 8);
        int bpp = tga[16];
        int descriptor = tga[17];
        if (colourMapType != 0 || (type != 2 && type != 10) || (bpp != 24 && bpp != 32)
            || width <= 0 || height <= 0 || (descriptor & 0x10) != 0)
        {
            return null;
        }

        int start = 18 + idLength;
        if (start > tga.Length)
        {
            return null;
        }

        var rgba = new byte[width * height * 4];
        int perPixel = bpp / 8;
        bool ok = type == 2
            ? ReadRaw(tga[start..], rgba, perPixel)
            : ReadRle(tga[start..], rgba, perPixel);
        if (!ok)
        {
            return null;
        }

        if ((descriptor & 0x20) == 0)
        {
            FlipRows(rgba, width, height);
        }

        return new TgaImage(width, height, rgba);
    }

    // Type 2: pixels in file order, BGR(A) to RGBA.
    private static bool ReadRaw(ReadOnlySpan<byte> data, byte[] rgba, int perPixel)
    {
        int pixels = rgba.Length / 4;
        if (data.Length < pixels * perPixel)
        {
            return false;
        }

        for (int p = 0; p < pixels; p++)
        {
            WritePixel(data[(p * perPixel)..], rgba, p, perPixel);
        }

        return true;
    }

    // Type 10: packets of a header byte then pixel data; high bit set is a run of one repeated
    // pixel, clear is that many literal pixels, count in the low bits plus one either way. A
    // packet claiming more pixels than remain is malformed, not clipped.
    private static bool ReadRle(ReadOnlySpan<byte> data, byte[] rgba, int perPixel)
    {
        int pixels = rgba.Length / 4;
        int at = 0;
        for (int p = 0; p < pixels;)
        {
            if (at >= data.Length)
            {
                return false;
            }

            int header = data[at++];
            int count = (header & 0x7f) + 1;
            if (p + count > pixels)
            {
                return false;
            }

            if ((header & 0x80) != 0)
            {
                if (at + perPixel > data.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    WritePixel(data[at..], rgba, p + i, perPixel);
                }

                at += perPixel;
            }
            else
            {
                if (at + (count * perPixel) > data.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    WritePixel(data[(at + (i * perPixel))..], rgba, p + i, perPixel);
                }

                at += count * perPixel;
            }

            p += count;
        }

        return true;
    }

    private static void WritePixel(ReadOnlySpan<byte> source, byte[] rgba, int pixel, int perPixel)
    {
        int o = pixel * 4;
        rgba[o] = source[2];
        rgba[o + 1] = source[1];
        rgba[o + 2] = source[0];
        rgba[o + 3] = perPixel == 4 ? source[3] : (byte)255;
    }

    private static void FlipRows(byte[] rgba, int width, int height)
    {
        int stride = width * 4;
        var row = new byte[stride];
        for (int top = 0, bottom = height - 1; top < bottom; top++, bottom--)
        {
            Array.Copy(rgba, top * stride, row, 0, stride);
            Array.Copy(rgba, bottom * stride, rgba, top * stride, stride);
            Array.Copy(row, 0, rgba, bottom * stride, stride);
        }
    }
}
