using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Extraction;

/// <summary>Reads the named-entry archive of <c>zrdr.zbd</c>, <c>soundsh.zbd</c> and
/// <c>soundsl.zbd</c>. Payloads lie back to back, then a table of 148-byte entries, then a
/// version and count. The layout is in docs/formats/zbd-archive.md. Opening reads only the table;
/// each payload is read on demand, so a 128 MB sound archive is never held whole. The format has no
/// checksum, so the table is checked instead: entries must tile the payload region from offset 0.
/// The archive owns its stream, and <see cref="ReadEntry"/> may be called from any thread.</summary>
public sealed class ZbdArchive : IDisposable
{
    // The only version an archive of the original carries.
    private const uint Version = 1;
    private const int FooterSize = 8;
    private const int EntrySize = 148;
    private const int NameSize = 64;

    private readonly Stream _stream;
    private readonly object _read = new();

    private ZbdArchive(Stream stream, IReadOnlyList<ZbdEntry> entries)
    {
        _stream = stream;
        Entries = entries;
    }

    /// <summary>Every entry in table order, which is also payload order. Names are as stored, so
    /// two entries may share one.</summary>
    public IReadOnlyList<ZbdEntry> Entries { get; }

    /// <summary>Opens the archive <paramref name="path"/> names in <paramref name="source"/>.
    /// Throws <see cref="FileNotFoundException"/> when it is absent and
    /// <see cref="InvalidDataException"/> when its table does not read.</summary>
    public static ZbdArchive Open(IGameFileSource source, string path)
    {
        var stream = source.Open(path)
            ?? throw new FileNotFoundException($"'{path}' is not in the game files at '{source.Location}'", path);
        try
        {
            return Read(stream, path);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Reads the table of the archive in the seekable <paramref name="stream"/>, which the
    /// result then owns. <paramref name="name"/> labels error messages. Throws
    /// <see cref="InvalidDataException"/> on a wrong version or a table that does not tile.</summary>
    public static ZbdArchive Read(Stream stream, string name)
    {
        long length = stream.Length;
        if (length < FooterSize)
        {
            throw new InvalidDataException($"{name}: {length} bytes is too short for an archive footer");
        }

        var footer = new byte[FooterSize];
        stream.Seek(length - FooterSize, SeekOrigin.Begin);
        stream.ReadExactly(footer);
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(footer);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(footer.AsSpan(4));
        if (version != Version)
        {
            throw new InvalidDataException($"{name}: archive version {version}, expected {Version}");
        }

        long tableStart = length - FooterSize - ((long)count * EntrySize);
        if (tableStart < 0)
        {
            throw new InvalidDataException($"{name}: {count} entries do not fit in {length} bytes");
        }

        var table = new byte[count * EntrySize];
        stream.Seek(tableStart, SeekOrigin.Begin);
        stream.ReadExactly(table);

        var entries = new List<ZbdEntry>((int)count);
        long expected = 0;
        for (int i = 0; i < count; i++)
        {
            var e = table.AsSpan(i * EntrySize, EntrySize);
            uint start = BinaryPrimitives.ReadUInt32LittleEndian(e);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(4));
            var nameField = e.Slice(8, NameSize);
            int nul = nameField.IndexOf((byte)0);
            string entryName = Encoding.ASCII.GetString(nul < 0 ? nameField : nameField.Slice(0, nul));
            if (start != expected || start + (long)size > tableStart)
            {
                throw new InvalidDataException(
                    $"{name}: entry {i} '{entryName}' spans {start}+{size}, expected to start at {expected} and end by {tableStart}");
            }

            entries.Add(new ZbdEntry(entryName, start, (int)size));
            expected = start + (long)size;
        }

        return new ZbdArchive(stream, entries);
    }

    /// <summary>Today's extraction's collision rule, which every loader keying a repeated name
    /// reproduces. The first <paramref name="name"/> keeps it (null). A repeat becomes
    /// <c>stem-N.ext</c> (split at the last period) or <c>name-N</c>. N is the smallest from 1 not
    /// in <paramref name="seen"/>. Case-sensitive, so <paramref name="seen"/> must compare
    /// ordinally; the name or rename handed out is added to it. Rule:
    /// docs/formats/zbd-archive.md.</summary>
    public static string? Rename(HashSet<string> seen, string name)
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

    /// <summary>The payload of <paramref name="entry"/>, one of <see cref="Entries"/>.</summary>
    public byte[] ReadEntry(ZbdEntry entry)
    {
        var data = new byte[entry.Length];
        lock (_read)
        {
            _stream.Seek(entry.Offset, SeekOrigin.Begin);
            _stream.ReadExactly(data);
        }

        return data;
    }

    /// <summary>Closes the archive's stream.</summary>
    public void Dispose() => _stream.Dispose();
}

/// <summary>One entry of a <see cref="ZbdArchive"/>: its stored name (<c>aiv.zrd</c>) and where its payload sits from the start of the file.</summary>
public sealed record ZbdEntry(string Name, long Offset, int Length);
