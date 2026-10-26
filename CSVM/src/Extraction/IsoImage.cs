using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSVM.Extraction;

/// <summary>An ISO 9660 image read in place as a game-file source. Its root is the disc's
/// <c>GAME</c> folder, or the image root when it has none. Names are the primary volume's, minus
/// the <c>;1</c> version and a trailing period, matched without regard to case. The directory
/// tree is walked once, on <see cref="Read"/>. Each <see cref="Open"/> is a window onto the image
/// over its own handle, so no file is loaded whole and reads may run on any thread. The chapter
/// <c>.CAB</c> files come out as plain files (layout: docs/formats/disc-image.md).</summary>
public sealed class IsoImage : IGameFileSource
{
    private const int SectorSize = 2048;
    private const int FirstDescriptor = 16;

    // How many volume descriptors are read looking for the primary one before the image is refused.
    private const int MaxDescriptors = 32;
    private const byte PrimaryDescriptor = 1;
    private const byte TerminatorDescriptor = 255;

    // A directory record's fixed part is 33 bytes, and every name is at least one byte.
    private const int MinRecordSize = 34;

    // Far above any directory of a game disc; a corrupt length past it is refused, not allocated.
    private const int MaxDirectorySize = 1 << 20;
    private const byte DirectoryFlag = 0x02;
    private const byte AssociatedFlag = 0x04;
    private const byte MultiExtentFlag = 0x80;
    private const string GameFolder = "GAME/";

    private readonly Dictionary<string, Extent> _files;
    private readonly Func<Stream> _open;

    private IsoImage(string image, Dictionary<string, Extent> files, Func<Stream> open)
    {
        Location = image;
        _files = files;
        _open = open;
    }

    /// <summary>The image file.</summary>
    public string Location { get; }

    /// <summary>Every file below the source's root, <c>/</c>-separated in the disc's case.</summary>
    public IReadOnlyCollection<string> Files => _files.Keys;

    /// <summary>Reads the directory tree of the image file <paramref name="image"/>. Throws
    /// <see cref="InvalidDataException"/> when it is not an ISO 9660 image or a record points
    /// outside it, and <see cref="IOException"/> when the file does not read.</summary>
    public static IsoImage Read(string image)
    {
        string full = Path.GetFullPath(image);
        return Read(full, () => new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    /// <inheritdoc/>
    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    /// <inheritdoc/>
    public Stream? Open(string path) =>
        _files.TryGetValue(Normalize(path), out var extent) ? new ExtentStream(_open(), extent) : null;

    /// <summary>The image whose 2,048-byte sectors <paramref name="open"/> hands out as a fresh
    /// seekable stream per call, named <paramref name="location"/>. A raw CD image's data track
    /// comes in this way. <paramref name="atGame"/> false roots it at the image root even when a
    /// <c>GAME</c> folder exists.</summary>
    internal static IsoImage Read(string location, Func<Stream> open, bool atGame = true)
    {
        Dictionary<string, Extent> files;
        using (var stream = open())
        {
            files = Walk(stream, location);
        }

        if (atGame && files.Keys.Any(path => path.StartsWith(GameFolder, StringComparison.OrdinalIgnoreCase)))
        {
            files = files.Where(f => f.Key.StartsWith(GameFolder, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(f => f.Key.Substring(GameFolder.Length), f => f.Value, StringComparer.OrdinalIgnoreCase);
        }

        return new IsoImage(location, files, open);
    }

    /// <summary>Whether some file lies below the folder <paramref name="path"/>, the image's
    /// counterpart of a folder lookup.</summary>
    internal bool HasFolder(string path)
    {
        string prefix = Normalize(path) + "/";
        return _files.Keys.Any(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // Split on either slash and rejoined, so a path only ever names a stored key. A ".." segment
    // matches no record, so it never leaves the image.
    private static string Normalize(string path) =>
        string.Join('/', path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));

    private static Dictionary<string, Extent> Walk(Stream image, string name)
    {
        byte[] primary = FindPrimary(image, name);
        int block = BinaryPrimitives.ReadUInt16LittleEndian(primary.AsSpan(128));
        if (block is not (512 or 1024 or 2048))
        {
            throw new InvalidDataException($"{name}: logical block size {block}, expected 512, 1024 or 2048");
        }

        var files = new Dictionary<string, Extent>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<(string Prefix, Extent Extent)>();
        pending.Push((string.Empty, ExtentOf(primary.AsSpan(156, MinRecordSize), block, image.Length, name)));
        var visited = new HashSet<long>();
        while (pending.TryPop(out var dir))
        {
            // A corrupt image could point a directory back at an ancestor.
            if (!visited.Add(dir.Extent.Offset))
            {
                continue;
            }

            if (dir.Extent.Length > MaxDirectorySize)
            {
                throw new InvalidDataException($"{name}: directory '{dir.Prefix}' claims {dir.Extent.Length} bytes");
            }

            var records = new byte[dir.Extent.Length];
            image.Seek(dir.Extent.Offset, SeekOrigin.Begin);
            image.ReadExactly(records);
            int at = 0;
            while (at < records.Length)
            {
                int size = records[at];
                if (size == 0)
                {
                    // Records never straddle a sector, so a zero length pads to the next one.
                    at = ((at / SectorSize) + 1) * SectorSize;
                    continue;
                }

                if (size < MinRecordSize || at + size > records.Length || 33 + records[at + 32] > size)
                {
                    throw new InvalidDataException($"{name}: bad directory record at byte {at} of '{dir.Prefix}'");
                }

                var record = records.AsSpan(at, size);
                at += size;
                var raw = record.Slice(33, record[32]);
                byte flags = record[25];
                if ((raw.Length == 1 && raw[0] <= 1) || (flags & AssociatedFlag) != 0)
                {
                    continue;
                }

                string path = dir.Prefix + FileName(raw);
                if ((flags & MultiExtentFlag) != 0)
                {
                    throw new InvalidDataException($"{name}: '{path}' spans several extents, which this reader does not join");
                }

                var extent = ExtentOf(record, block, image.Length, name);
                if ((flags & DirectoryFlag) != 0)
                {
                    pending.Push((path + "/", extent));
                }
                else
                {
                    files.TryAdd(path, extent);
                }
            }
        }

        return files;
    }

    private static byte[] FindPrimary(Stream image, string name)
    {
        var sector = new byte[SectorSize];
        for (int i = 0; i < MaxDescriptors; i++)
        {
            long at = (long)(FirstDescriptor + i) * SectorSize;
            if (at + SectorSize > image.Length)
            {
                break;
            }

            image.Seek(at, SeekOrigin.Begin);
            image.ReadExactly(sector);
            if (!sector.AsSpan(1, 5).SequenceEqual("CD001"u8) || sector[0] == TerminatorDescriptor)
            {
                break;
            }

            if (sector[0] == PrimaryDescriptor)
            {
                return sector;
            }
        }

        throw new InvalidDataException($"{name}: not an ISO 9660 image, no primary volume descriptor from sector {FirstDescriptor}");
    }

    // Data begins after the extended attribute record, whose length in blocks is the record's byte 1.
    private static Extent ExtentOf(ReadOnlySpan<byte> record, int block, long imageLength, string name)
    {
        long offset = (BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(2)) + (long)record[1]) * block;
        long length = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(10));
        if (offset + length > imageLength)
        {
            throw new InvalidDataException($"{name}: an extent at byte {offset} of {length} bytes runs past the image's {imageLength}");
        }

        return new Extent(offset, length);
    }

    private static string FileName(ReadOnlySpan<byte> raw)
    {
        string stored = Encoding.ASCII.GetString(raw);
        int version = stored.IndexOf(';');
        return (version < 0 ? stored : stored.Substring(0, version)).TrimEnd('.');
    }

    private readonly record struct Extent(long Offset, long Length);

    // A read-only window onto one file's bytes in the image, owning its own handle.
    private sealed class ExtentStream : Stream
    {
        private readonly Stream _image;
        private readonly long _start;
        private long _position;

        public ExtentStream(Stream image, Extent extent)
        {
            _image = image;
            _start = extent.Offset;
            Length = extent.Length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            long left = Length - _position;
            if (left <= 0)
            {
                return 0;
            }

            if (buffer.Length > left)
            {
                buffer = buffer.Slice(0, (int)left);
            }

            _image.Position = _start + _position;
            int read = _image.Read(buffer);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (target < 0)
            {
                throw new IOException("A seek before the start of the file.");
            }

            _position = target;
            return target;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
