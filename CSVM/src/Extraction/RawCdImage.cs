using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CSVM.Extraction;

/// <summary>Where a raw image's data track lies: the image file and the track's first and end
/// byte, null for the file's end. Each stored sector is <c>SectorSize</c> bytes, its 2,048 bytes
/// of user data at <c>DataOffset</c>.</summary>
internal readonly record struct RawDataTrack(string File, long Start, long? End, int SectorSize, int DataOffset)
{
    /// <summary>The whole sectors the track holds in a file of <paramref name="fileLength"/>
    /// bytes. Throws <see cref="InvalidDataException"/> when the track starts past its end.</summary>
    public long Sectors(long fileLength, string sheet)
    {
        long end = End is { } stop && stop < fileLength ? stop : fileLength;
        if (Start >= end)
        {
            throw new InvalidDataException($"{sheet}: the data track starts at byte {Start} of '{File}', which holds {fileLength}");
        }

        return (end - Start) / SectorSize;
    }
}

/// <summary>A raw CD image read in place as an <see cref="IsoImage"/>. That is a <c>.cue</c>
/// sheet with the file it names, or a CloneCD <c>.ccd</c> with its <c>.img</c>. The first track
/// must be data; its sectors' 2,048-byte user data is the ISO 9660 volume
/// (layout: docs/formats/disc-image.md). A bare <c>.bin</c> or <c>.img</c> is taken only with a
/// sheet of the same name beside it.</summary>
public static class RawCdImage
{
    /// <summary>The raw sector size of a CloneCD image and of a <c>/2352</c> track.</summary>
    internal const int RawSector = 2352;

    // A sheet is a few lines; a larger file named .cue or .ccd is not one, and is not read whole.
    private const int MaxSheetBytes = 64 * 1024;

    private const string CueExtension = ".cue";
    private const string CloneCdExtension = ".ccd";

    /// <summary>The sheet <paramref name="path"/> stands for, or null. An existing <c>.cue</c> or
    /// <c>.ccd</c> is its own sheet. An existing <c>.bin</c> or <c>.img</c> has the same-named
    /// <c>.cue</c> (then <c>.ccd</c>) beside it, matched without regard to case.</summary>
    public static string? Descriptor(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string extension = Path.GetExtension(path);
        if (extension.Equals(CueExtension, StringComparison.OrdinalIgnoreCase) || extension.Equals(CloneCdExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(path);
        }

        if (!extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".img", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string stem = Path.GetFileNameWithoutExtension(path);
        return InstallLocator.ResolveFile(folder, stem + CueExtension) ?? InstallLocator.ResolveFile(folder, stem + CloneCdExtension);
    }

    /// <summary>The ISO 9660 volume in the first track of the raw image <paramref name="path"/>
    /// (any path <see cref="Descriptor"/> accepts), rooted as an <c>.iso</c> is. Throws
    /// <see cref="InvalidDataException"/> naming the reason for a sheet or volume that does not
    /// read, and <see cref="FileNotFoundException"/> when the image file is not beside it.</summary>
    public static IsoImage Read(string path) => Read(path, atGame: true);

    /// <summary>As <see cref="Read(string)"/>; <paramref name="atGame"/> false roots the source at
    /// the disc root, which lists every file on it.</summary>
    internal static IsoImage Read(string path, bool atGame)
    {
        string sheet = Descriptor(path)
            ?? throw new InvalidDataException($"{path}: not a .cue or .ccd sheet, nor a .bin or .img with one beside it");
        var track = sheet.EndsWith(CloneCdExtension, StringComparison.OrdinalIgnoreCase) ? CloneCdTrack(sheet) : CueSheet.DataTrack(sheet);
        long sectors = track.Sectors(new FileInfo(track.File).Length, sheet);
        return IsoImage.Read(sheet, () => new SectorStream(track, sectors), atGame);
    }

    /// <summary>The text of the sheet <paramref name="sheet"/>, refused past the size a sheet
    /// can have.</summary>
    internal static string[] SheetLines(string sheet)
    {
        if (new FileInfo(sheet).Length > MaxSheetBytes)
        {
            throw new InvalidDataException($"{sheet}: {new FileInfo(sheet).Length} bytes is too large for a sheet");
        }

        return File.ReadAllLines(sheet);
    }

    /// <summary>The file <paramref name="name"/> beside <paramref name="sheet"/>, matched without
    /// regard to case. Its relative path is tried first, then its bare name, so a path from the
    /// dumping machine still finds it. Throws <see cref="FileNotFoundException"/>.</summary>
    internal static string Beside(string sheet, string name)
    {
        string folder = Path.GetDirectoryName(sheet)!;
        string leaf = name.Split('/', '\\').Last();
        return InstallLocator.ResolveFile(folder, name) ?? InstallLocator.ResolveFile(folder, leaf)
            ?? throw new FileNotFoundException($"{sheet} names the image '{name}', which is not beside it", name);
    }

    // CloneCD's sheet is an INI file. Each [TRACK n] has MODE (0 audio, 1, 2) and INDEX 1, its
    // first sector in the .img. The .img holds 2,352-byte sectors from the disc's sector 0.
    private static RawDataTrack CloneCdTrack(string ccd)
    {
        var sections = Sections(ccd);
        if (sections.TryGetValue("Disc", out var disc) && Number(disc, "DataTracksScrambled", ccd) is > 0)
        {
            throw new InvalidDataException($"{ccd}: the data tracks are stored scrambled, which CSVM does not unscramble");
        }

        var tracks = sections
            .Where(s => s.Key.StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase))
            .Select(s => (Number: ParseNumber(s.Key.Substring(6), ccd), Mode: Number(s.Value, "MODE", ccd), Index0: Number(s.Value, "INDEX 0", ccd), Index1: Number(s.Value, "INDEX 1", ccd)))
            .OrderBy(t => t.Number)
            .ToList();
        if (tracks.Count == 0)
        {
            throw new InvalidDataException($"{ccd}: no [TRACK n] section");
        }

        var first = tracks[0];
        if (first.Mode is not (1 or 2))
        {
            throw first.Mode == 0
                ? new InvalidDataException(CueSheet.AudioFirst(ccd, first.Number, tracks.Where(t => t.Mode is 1 or 2).Select(t => t.Number)))
                : new InvalidDataException($"{ccd}: track {first.Number} has MODE={first.Mode}, expected 0, 1 or 2");
        }

        long start = first.Index1 ?? throw new InvalidDataException($"{ccd}: track {first.Number} has no INDEX 1");
        long? end = tracks.Count > 1 ? tracks[1].Index0 ?? tracks[1].Index1 : null;
        string image = Beside(ccd, Path.GetFileNameWithoutExtension(ccd) + ".img");
        return new RawDataTrack(image, start * RawSector, end * RawSector, RawSector, first.Mode == 1 ? 16 : 24);
    }

    private static Dictionary<string, Dictionary<string, string>> Sections(string ccd)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (string raw in SheetLines(ccd))
        {
            string line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[line[1..^1].Trim()] = current;
            }
            else if (current != null && line.IndexOf('=') is > 0 and int equals)
            {
                current[line[..equals].Trim()] = line[(equals + 1)..].Trim();
            }
        }

        return sections;
    }

    private static long? Number(Dictionary<string, string> section, string key, string ccd) =>
        section.TryGetValue(key, out string? value) ? ParseNumber(value, ccd) : null;

    private static long ParseNumber(string value, string ccd)
    {
        bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        return long.TryParse(hex ? value[2..] : value, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out long number)
            ? number
            : throw new InvalidDataException($"{ccd}: '{value}' is not a number");
    }

    // A data track as the 2,048-byte sectors an ISO 9660 reader expects: read-only, seekable, each
    // stored sector's header and error-correction bytes skipped. Owns its handle on the image file.
    private sealed class SectorStream : Stream
    {
        private const int UserData = 2048;

        // How many stored sectors one read fetches at most. A long copy costs one file read per
        // 64 KB of user data; a small read fetches only the sectors it touches.
        private const int MaxSectorsPerRead = 32;

        private readonly FileStream _file;
        private readonly RawDataTrack _track;
        private byte[] _raw = Array.Empty<byte>();
        private long _position;

        public SectorStream(RawDataTrack track, long sectors)
        {
            _track = track;
            Length = sectors * UserData;
            _file = new FileStream(track.File, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
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
            if (left <= 0 || buffer.Length == 0)
            {
                return 0;
            }

            long sector = _position / UserData;
            int within = (int)(_position % UserData);
            int want = (int)Math.Min(Math.Min(buffer.Length, left), (MaxSectorsPerRead * UserData) - within);
            int sectors = (within + want + UserData - 1) / UserData;
            int size = _track.SectorSize;
            if (_raw.Length < sectors * size)
            {
                _raw = new byte[sectors * size];
            }

            _file.Position = _track.Start + (sector * size);
            _file.ReadExactly(_raw, 0, sectors * size);
            int done = 0;
            for (int i = 0; done < want; i++)
            {
                int from = i == 0 ? within : 0;
                int count = Math.Min(UserData - from, want - done);
                _raw.AsSpan((i * size) + _track.DataOffset + from, count).CopyTo(buffer.Slice(done));
                done += count;
            }

            _position += want;
            return want;
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
                throw new IOException("A seek before the start of the track.");
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
                _file.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
