using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>One texture of a texture archive, decoded. <see cref="Pixels"/> is RGBA8 when
/// <see cref="HasAlphaChannel"/> is set and RGB8 otherwise. Those are the two layouts today's
/// extracted PNGs carry, so an image built from it keeps the PNG decode's format.</summary>
/// <param name="Name">The header's name, the extraction manifest's <c>name</c>.</param>
/// <param name="Rename">The collision rename of a repeated name, null for a first occurrence.</param>
/// <param name="RenderFlags">The render-flags word at header <c>0x0E</c> (the manifest's
/// <c>stretch</c>), raw.</param>
public sealed record TextureImage(
    string Name,
    string? Rename,
    int Width,
    int Height,
    TextureArchive.AlphaClass Alpha,
    int RenderFlags,
    bool HasAlphaChannel,
    byte[] Pixels)
{
    /// <summary>The name the texture is looked up by, today's PNG base name.</summary>
    public string Key => Rename ?? Name;
}

/// <summary>
/// Reads <c>texture.zbd</c>, <c>rtextureN.zbd</c> and <c>rimage.zbd</c> into
/// <see cref="TextureImage"/>s in file order, engine-free. Layout, pixel decode, alpha class and
/// rename rule: docs/formats/texture-archive.md.
/// </summary>
public static class TextureZbd
{
    private const int HeaderSize = 24;
    private const int EntrySize = 40;
    private const int NameLength = 32;
    private const int GlobalPaletteSize = 512;

    private const int TwoBytesPerPixel = 0x01;
    private const int HasAlpha = 0x02;
    private const int NoAlpha = 0x04;
    private const int AlphaPlane = 0x08;
    private const int GlobalPalette = 0x10;

    // 5- and 6-bit channels widened by rounding to nearest, as the extraction widened them.
    private static readonly byte[] Widen5 = WidenTable(31);
    private static readonly byte[] Widen6 = WidenTable(63);

    /// <summary>The textures of the archive at <paramref name="path"/> (<c>ZBD/C1/texture.zbd</c>),
    /// or null without a source or without the file.</summary>
    public static IReadOnlyList<TextureImage>? Load(IGameFileSource? game, string path) =>
        game != null && game.Exists(path) ? Read(game.ReadAllBytes(path)) : null;

    /// <summary>Decodes a whole archive. Throws <see cref="InvalidDataException"/> on a file that
    /// is not one, or whose textures do not tile it exactly.</summary>
    public static IReadOnlyList<TextureImage> Read(byte[] data)
    {
        Expect(data.Length >= HeaderSize, "header", 0);
        Expect(I32(data, 0) == 0 && I32(data, 4) == 1 && I32(data, 16) == 0 && I32(data, 20) == 0, "header", 0);
        int paletteCount = I32(data, 8);
        int count = I32(data, 12);
        Expect(paletteCount >= 0 && count > 0, "counts", 8);
        long tableEnd = HeaderSize + ((long)EntrySize * count);
        long palettesEnd = tableEnd + ((long)GlobalPaletteSize * paletteCount);
        Expect(palettesEnd <= data.Length, "texture table", HeaderSize);

        var palettes = new byte[paletteCount][];
        for (int p = 0; p < paletteCount; p++)
        {
            palettes[p] = Widen565(data.AsSpan((int)tableEnd + (p * GlobalPaletteSize), GlobalPaletteSize));
        }

        // Case-sensitive, and a rename is itself taken, as the extraction's collision rule has it.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var textures = new List<TextureImage>(count);
        int pos = (int)palettesEnd;
        for (int i = 0; i < count; i++)
        {
            int entry = HeaderSize + (i * EntrySize);
            string name = Name(data.AsSpan(entry, NameLength), entry);
            Expect(I32(data, entry + 32) == pos, $"start of {name}", entry + 32);
            int palette = I32(data, entry + 36);
            Expect(palette == -1 || (palette >= 0 && palette < paletteCount), $"global palette of {name}", entry + 36);
            textures.Add(ReadTexture(data, ref pos, name, Rename(seen, name), palette < 0 ? null : palettes[palette]));
        }

        Expect(pos == data.Length, "end of file", pos);
        return textures;
    }

    private static TextureImage ReadTexture(byte[] data, ref int pos, string name, string? rename, byte[]? globalPalette)
    {
        int at = pos;
        Take(data, ref pos, 16, name);
        int flags = data[at];
        int width = U16(data, at + 4), height = U16(data, at + 6);
        int paletteCount = U16(data, at + 12);
        int renderFlags = U16(data, at + 14);

        // The class is the storage flags' alone, never the pixels'.
        TextureArchive.AlphaClass alpha;
        if ((flags & NoAlpha) != 0)
        {
            Expect((flags & (HasAlpha | AlphaPlane)) == 0, $"alpha flags of {name}", at);
            alpha = TextureArchive.AlphaClass.None;
        }
        else
        {
            Expect((flags & HasAlpha) != 0, $"alpha flags of {name}", at);
            alpha = (flags & AlphaPlane) != 0 ? TextureArchive.AlphaClass.Full : TextureArchive.AlphaClass.Simple;
        }

        Expect((flags & TwoBytesPerPixel) != 0, $"pixel size of {name}", at);
        Expect(((flags & GlobalPalette) != 0) == (globalPalette != null), $"global palette flag of {name}", at);
        Expect(I32(data, at + 8) == 0, $"field 08 of {name}", at + 8);
        Expect(paletteCount <= 256 && (globalPalette == null || paletteCount >= 1), $"palette count of {name}", at + 12);

        int size = width * height;
        bool full = alpha == TextureArchive.AlphaClass.Full;
        byte[] pixels;
        bool hasAlphaChannel;
        if (paletteCount == 0)
        {
            var colour = Take(data, ref pos, size * 2, name);
            var plane = full ? Take(data, ref pos, size, name) : default;
            hasAlphaChannel = alpha != TextureArchive.AlphaClass.None;
            pixels = new byte[size * (hasAlphaChannel ? 4 : 3)];
            for (int i = 0, o = 0; i < size; i++)
            {
                int c = colour[i * 2] | (colour[(i * 2) + 1] << 8);
                pixels[o++] = Widen5[c >> 11];
                pixels[o++] = Widen6[(c >> 5) & 0x3F];
                pixels[o++] = Widen5[c & 0x1F];
                if (hasAlphaChannel)
                {
                    // Simple alpha: only a texel of 0x0000 is clear.
                    pixels[o++] = full ? plane[i] : c == 0 ? (byte)0 : (byte)255;
                }
            }
        }
        else
        {
            var indices = Take(data, ref pos, size, name);
            var plane = full ? Take(data, ref pos, size, name) : default;
            var palette = globalPalette ?? Widen565(Take(data, ref pos, paletteCount * 2, name));

            // ⚠ A paletted Simple texture gets no alpha channel: its clear index is not encoded.
            hasAlphaChannel = full;
            pixels = new byte[size * (full ? 4 : 3)];
            for (int i = 0, o = 0; i < size; i++)
            {
                int index = indices[i];
                if (index >= paletteCount)
                {
                    Expect(false, $"palette index of {name}", pos);
                }

                pixels[o++] = palette[index * 3];
                pixels[o++] = palette[(index * 3) + 1];
                pixels[o++] = palette[(index * 3) + 2];
                if (full)
                {
                    pixels[o++] = plane[i];
                }
            }
        }

        return new TextureImage(name, rename, width, height, alpha, renderFlags, hasAlphaChannel, pixels);
    }

    // The first occurrence keeps its name; a repeat becomes `stem-N.suffix` (split at the last
    // period) or `name-N`, with the smallest N not yet taken.
    private static string? Rename(HashSet<string> seen, string name)
    {
        if (seen.Add(name))
        {
            return null;
        }

        int dot = name.LastIndexOf('.');
        for (int n = 1; ; n++)
        {
            string candidate = dot < 0 ? $"{name}-{n}" : $"{name[..dot]}-{n}.{name[(dot + 1)..]}";
            if (seen.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Name(ReadOnlySpan<byte> raw, int offset)
    {
        int end = raw.IndexOf((byte)0);
        Expect(end >= 0 && raw[end..].IndexOfAnyExcept((byte)0) < 0, "name padding", offset);
        foreach (byte b in raw[..end])
        {
            Expect(b < 0x80, "name", offset);
        }

        return Encoding.ASCII.GetString(raw[..end]);
    }

    private static byte[] Widen565(ReadOnlySpan<byte> colours)
    {
        var rgb = new byte[colours.Length / 2 * 3];
        for (int i = 0, o = 0; i + 1 < colours.Length; i += 2)
        {
            int c = colours[i] | (colours[i + 1] << 8);
            rgb[o++] = Widen5[c >> 11];
            rgb[o++] = Widen6[(c >> 5) & 0x3F];
            rgb[o++] = Widen5[c & 0x1F];
        }

        return rgb;
    }

    private static byte[] WidenTable(int max)
    {
        var table = new byte[max + 1];
        for (int v = 0; v <= max; v++)
        {
            table[v] = (byte)((v * 255.0 / max) + 0.5);
        }

        return table;
    }

    private static ReadOnlySpan<byte> Take(byte[] data, ref int pos, int length, string name)
    {
        Expect(length <= data.Length - pos, $"data of {name} past the end", pos);
        var span = data.AsSpan(pos, length);
        pos += length;
        return span;
    }

    private static int I32(byte[] data, int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));

    private static int U16(byte[] data, int at) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at));

    private static void Expect(bool ok, string what, long offset)
    {
        if (!ok)
        {
            throw new InvalidDataException($"texture archive: bad {what} at offset {offset}");
        }
    }
}
