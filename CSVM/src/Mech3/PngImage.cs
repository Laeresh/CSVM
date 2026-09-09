using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace CSVM.Mech3;

/// <summary>
/// A decoded PNG as a <see cref="TgaImage"/>, engine-free, so the menu screens' art loads and tests
/// off engine the way the hangar's TGA blueprints already do. Covers exactly what ships under
/// <c>extracted/rimage</c>: 8 bits per channel, non-interlaced, truecolour with (colour type 6) or
/// without (type 2) an alpha channel, which is every file in that extraction. Anything else
/// (palettes, 16-bit channels, Adam7) decodes as null rather than throwing, because menu art is
/// optional by design and a file this decoder does not cover must not take a screen down.
/// </summary>
public static class PngImage
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    /// <summary>The gamma all UI textures are normalized to before their pixels enter a raw RGBA8 path.</summary>
    public const uint UiGamma = 45454;

    /// <summary>Reads and decodes one file, or null when it is absent or outside the coverage
    /// above. The path is the caller's business (absolute, under the session's data root).</summary>
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

    /// <summary>Reads a PNG's <c>gAMA</c> value, or null when it is absent or the file is not a PNG.</summary>
    public static uint? TryReadGamma(string path)
    {
        try
        {
            return File.Exists(path) ? ReadGamma(File.ReadAllBytes(path)) : null;
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

    /// <summary>Decodes one PNG, or null for anything outside this decoder's coverage.</summary>
    public static TgaImage? Decode(byte[] png)
    {
        if (png.Length < Signature.Length + 12)
        {
            return null;
        }

        for (int i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
            {
                return null;
            }
        }

        int width = 0, height = 0, channels = 0;
        uint gamma = 0;
        var idat = new MemoryStream();
        int at = Signature.Length;
        while (at + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            int body = at + 8;
            if (length < 0 || body + length + 4 > png.Length)
            {
                return null;
            }

            if (type == "IHDR")
            {
                if (!ReadHeader(png.AsSpan(body, length), out width, out height, out channels))
                {
                    return null;
                }
            }
            else if (type == "IDAT")
            {
                idat.Write(png, body, length);
            }
            else if (type == "gAMA" && length == 4)
            {
                gamma = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(body, length));
            }
            else if (type == "IEND")
            {
                break;
            }

            at = body + length + 4;
        }

        return channels == 0 ? null : Unfilter(Inflate(idat), width, height, channels, gamma);
    }

    private static uint? ReadGamma(byte[] png)
    {
        if (png.Length < Signature.Length + 12)
        {
            return null;
        }

        for (int i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
            {
                return null;
            }
        }

        for (int at = Signature.Length; at + 12 <= png.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            int body = at + 8;
            if (length < 0 || body + length + 4 > png.Length)
            {
                return null;
            }

            string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (type == "gAMA" && length == 4)
            {
                return BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(body, length));
            }

            at = body + length + 4;
        }

        return null;
    }

    // IHDR: the five bytes after the dimensions are bit depth, colour type, compression, filter
    // and interlace; only the one combination the rimage extraction ships is accepted.
    private static bool ReadHeader(ReadOnlySpan<byte> ihdr, out int width, out int height, out int channels)
    {
        width = 0;
        height = 0;
        channels = 0;
        if (ihdr.Length < 13)
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32BigEndian(ihdr);
        height = BinaryPrimitives.ReadInt32BigEndian(ihdr[4..]);
        if (width <= 0 || height <= 0 || ihdr[8] != 8 || ihdr[10] != 0 || ihdr[11] != 0 || ihdr[12] != 0)
        {
            return false;
        }

        channels = ihdr[9] == 2 ? 3 : ihdr[9] == 6 ? 4 : 0;
        return channels != 0;
    }

    private static byte[]? Inflate(MemoryStream idat)
    {
        try
        {
            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);
            return raw.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    // Each row is one filter byte then its pixels; the five filters all predict a byte from the
    // one before it in the row (a), the one above (b) and the one above-left (c).
    private static TgaImage? Unfilter(byte[]? raw, int width, int height, int channels, uint gamma)
    {
        int stride = width * channels;
        if (raw == null || raw.Length < (long)height * (stride + 1))
        {
            return null;
        }

        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var row = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            int source = (y * (stride + 1)) + 1;
            int filter = raw[source - 1];
            for (int x = 0; x < stride; x++)
            {
                int a = x >= channels ? row[x - channels] : 0;
                int b = previous[x];
                int c = x >= channels ? previous[x - channels] : 0;
                int predicted = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => -1,
                };
                if (predicted < 0)
                {
                    return null;
                }

                row[x] = (byte)(raw[source + x] + predicted);
            }

            for (int x = 0; x < width; x++)
            {
                int o = ((y * width) + x) * 4;
                int p = x * channels;
                rgba[o] = row[p];
                rgba[o + 1] = row[p + 1];
                rgba[o + 2] = row[p + 2];
                rgba[o + 3] = channels == 4 ? row[p + 3] : (byte)255;
            }

            Array.Copy(row, previous, stride);
        }

        NormalizeGamma(rgba, gamma);
        return TgaImage.FromRgba(width, height, rgba);
    }

    // Godot's Image.LoadFromFile drops PNG gAMA. Board textures instead carry the UI's 0.45454
    // transfer curve, so normalize a file's RGB samples before creating an RGBA8 texture.
    private static void NormalizeGamma(byte[] rgba, uint gamma)
    {
        if (gamma == 0 || gamma == UiGamma)
        {
            return;
        }

        double exponent = gamma / (double)UiGamma;
        for (int p = 0; p < rgba.Length; p += 4)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                rgba[p + channel] = (byte)Math.Min(255,
                    (int)Math.Round(Math.Pow(rgba[p + channel] / 255d, exponent) * 255d));
            }
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c);
        return da <= db && da <= dc ? a : db <= dc ? b : c;
    }
}
