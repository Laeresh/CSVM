using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>One reader file of a <c>zrdr.zbd</c>, decoded into the list shape
/// <see cref="Zrdr.LoadFile"/> returns: strings, <see cref="float"/>s, nested lists, and null for an
/// empty nested list. An empty reader is an empty <see cref="Root"/>.</summary>
/// <param name="Name">The archive table's name, <c>vehicle.zrd</c>.</param>
/// <param name="Rename">The collision rename of a repeated name, null for a first occurrence.</param>
public sealed record ZrdrFile(string Name, string? Rename, List<object?> Root)
{
    /// <summary>The name the file is looked up by: today's zip entry name without <c>.json</c>.</summary>
    public string Key => Rename ?? Name;
}

/// <summary>
/// Reads a <c>zrdr.zbd</c> (root, chapter or mission) into its <see cref="ZrdrFile"/>s in table
/// order, engine-free. The value encoding and the lookup rule are in docs/formats/zrdr.md.
/// </summary>
public static class ZrdrZbd
{
    private const uint IntTag = 1;
    private const uint FloatTag = 2;
    private const uint StringTag = 3;
    private const uint ListTag = 4;

    /// <summary>The reader files of the archive at <paramref name="path"/>
    /// (<c>ZBD/C1/M02/zrdr.zbd</c>), or null without a source or without the file.</summary>
    public static IReadOnlyList<ZrdrFile>? Load(IGameFileSource? game, string path) =>
        game != null && game.Exists(path) ? Read(game.ReadAllBytes(path)) : null;

    /// <summary>Decodes a whole archive. Throws <see cref="InvalidDataException"/> when the
    /// container or any reader in it does not read.</summary>
    public static IReadOnlyList<ZrdrFile> Read(byte[] data)
    {
        using var archive = ZbdArchive.Read(new MemoryStream(data, writable: false), "zrdr.zbd");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var files = new List<ZrdrFile>(archive.Entries.Count);
        foreach (var entry in archive.Entries)
        {
            files.Add(new ZrdrFile(entry.Name, ZbdArchive.Rename(seen, entry.Name), ReadReader(archive.ReadEntry(entry), entry.Name)));
        }

        return files;
    }

    /// <summary>Decodes one reader payload, which must be a single list that fills it exactly.
    /// <paramref name="name"/> labels error messages.</summary>
    public static List<object?> ReadReader(ReadOnlySpan<byte> payload, string name)
    {
        int pos = 0;
        Expect(U32(payload, ref pos, name) == ListTag, name, "root tag", 0);
        var root = ReadList(payload, ref pos, name) ?? new List<object?>();
        Expect(pos == payload.Length, name, "end of reader", pos);
        return root;
    }

    /// <summary>The <see cref="ZrdrFile"/> whose <see cref="ZrdrFile.Key"/> is <paramref name="name"/>
    /// or its zip spelling (<c>vehicle.json</c>, <c>vehicle.zrd.json</c>). Case is ignored. Null when
    /// absent.</summary>
    public static ZrdrFile? Find(IReadOnlyList<ZrdrFile> files, string name)
    {
        string key = name;
        if (key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^".json".Length];
            if (!key.EndsWith(".zrd", StringComparison.OrdinalIgnoreCase))
            {
                key += ".zrd";
            }
        }

        foreach (var file in files)
        {
            if (string.Equals(file.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    // A count of 1 is an empty list, read as null as the JSON detour wrote it.
    private static List<object?>? ReadList(ReadOnlySpan<byte> data, ref int pos, string name)
    {
        int at = pos;
        uint count = U32(data, ref pos, name);
        Expect(count >= 1 && count - 1 <= (uint)(data.Length - pos) / 4, name, "list count", at);
        if (count == 1)
        {
            return null;
        }

        var list = new List<object?>((int)count - 1);
        for (uint i = 1; i < count; i++)
        {
            list.Add(ReadValue(data, ref pos, name));
        }

        return list;
    }

    private static object? ReadValue(ReadOnlySpan<byte> data, ref int pos, string name)
    {
        int at = pos;
        switch (U32(data, ref pos, name))
        {
            case IntTag:
                // Every number reaches the model as a float, the integers included.
                return (float)(int)U32(data, ref pos, name);
            case FloatTag:
                return BitConverter.UInt32BitsToSingle(U32(data, ref pos, name));
            case StringTag:
                int length = (int)U32(data, ref pos, name);
                Expect(length >= 0 && length <= data.Length - pos, name, "string length", pos - 4);
                var text = data.Slice(pos, length);
                Expect(Ascii.IsValid(text), name, "string", pos);
                pos += length;
                return Encoding.ASCII.GetString(text);
            case ListTag:
                return ReadList(data, ref pos, name);
            default:
                Expect(false, name, "value tag", at);
                return null;
        }
    }

    private static uint U32(ReadOnlySpan<byte> data, ref int pos, string name)
    {
        Expect(data.Length - pos >= 4, name, "data past the end", pos);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(data[pos..]);
        pos += 4;
        return value;
    }

    private static void Expect(bool ok, string name, string what, int offset)
    {
        if (!ok)
        {
            throw new InvalidDataException($"reader {name}: bad {what} at offset {offset}");
        }
    }
}
