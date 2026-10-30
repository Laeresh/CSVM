using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Extraction;

/// <summary>One file of a <see cref="Cabinet"/>: its stored name (<c>\</c>-separated), its
/// folder, and where its bytes lie in that folder's unpacked output.</summary>
public readonly record struct CabinetFile(string Name, int Folder, long Offset, long Length);

/// <summary>A Microsoft cabinet's file table, read without unpacking anything, and its folders
/// unpacked on request (docs/formats/cabinet.md). A folder stored uncompressed or with LZX is
/// unpacked; MSZIP and Quantum, which the game's cabinets do not use, are refused. A cabinet that
/// continues into another, a file in a continued folder, or a malformed header throws
/// <see cref="InvalidDataException"/>. Engine-free.</summary>
public sealed class Cabinet
{
    private const ushort PreviousCabinet = 0x0001;
    private const ushort NextCabinet = 0x0002;
    private const ushort ReservePresent = 0x0004;
    private const ushort NameIsUtf8 = 0x0080;
    private const int Stored = 0;
    private const int Lzx = 3;

    private readonly Folder[] _folders;
    private readonly int _dataReserve;

    private Cabinet(string name, Folder[] folders, CabinetFile[] files, int dataReserve)
    {
        Name = name;
        _folders = folders;
        Files = files;
        _dataReserve = dataReserve;
    }

    /// <summary>The cabinet's name, for error messages.</summary>
    public string Name { get; }

    /// <summary>Every file, in the cabinet's order.</summary>
    public IReadOnlyList<CabinetFile> Files { get; }

    /// <summary>The number of folders, each one compressed stream.</summary>
    public int FolderCount => _folders.Length;

    /// <summary>Reads the header, folder and file tables of the cabinet <paramref name="stream"/>,
    /// which must be seekable. <paramref name="name"/> labels errors.</summary>
    public static Cabinet Read(Stream stream, string name)
    {
        using var reader = new BinaryReader(stream, Encoding.Latin1, leaveOpen: true);
        stream.Seek(0, SeekOrigin.Begin);
        if (reader.ReadUInt32() != 0x4643534D)
        {
            throw new InvalidDataException($"{name}: not a cabinet, no MSCF signature");
        }

        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();
        uint filesAt = reader.ReadUInt32();
        reader.ReadUInt32();
        byte minor = reader.ReadByte();
        byte major = reader.ReadByte();
        int folderCount = reader.ReadUInt16();
        int fileCount = reader.ReadUInt16();
        ushort flags = reader.ReadUInt16();
        reader.ReadUInt32();
        if (major != 1)
        {
            throw new InvalidDataException($"{name}: cabinet version {major}.{minor}, expected 1.x");
        }

        if ((flags & (PreviousCabinet | NextCabinet)) != 0)
        {
            throw new InvalidDataException($"{name}: a cabinet split across several files, which this reader does not join");
        }

        int headerReserve = 0;
        int folderReserve = 0;
        int dataReserve = 0;
        if ((flags & ReservePresent) != 0)
        {
            headerReserve = reader.ReadUInt16();
            folderReserve = reader.ReadByte();
            dataReserve = reader.ReadByte();
        }

        stream.Seek(headerReserve, SeekOrigin.Current);
        var folders = new Folder[folderCount];
        for (int i = 0; i < folderCount; i++)
        {
            folders[i] = new Folder(reader.ReadUInt32(), reader.ReadUInt16(), reader.ReadUInt16());
            stream.Seek(folderReserve, SeekOrigin.Current);
        }

        stream.Seek(filesAt, SeekOrigin.Begin);
        var files = new CabinetFile[fileCount];
        for (int i = 0; i < fileCount; i++)
        {
            uint length = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            int folder = reader.ReadUInt16();
            reader.ReadUInt32();
            ushort attributes = reader.ReadUInt16();
            string file = ReadName(reader, (attributes & NameIsUtf8) != 0, name);
            if (folder >= folderCount)
            {
                throw new InvalidDataException($"{name}: '{file}' is in folder {folder} of {folderCount}, or continues across cabinets");
            }

            files[i] = new CabinetFile(file, folder, offset, length);
        }

        return new Cabinet(name, folders, files, dataReserve);
    }

    /// <summary>The compression type of folder <paramref name="folder"/>: 0 none, 1 MSZIP,
    /// 2 Quantum, 3 LZX, the low four bits of the stored field.</summary>
    public int CompressionOf(int folder) => _folders[folder].Compression & 0x0F;

    /// <summary>The whole unpacked output of folder <paramref name="folder"/>, read from
    /// <paramref name="stream"/>, the same cabinet <see cref="Read"/> was given. A file's bytes
    /// are the <see cref="CabinetFile.Length"/> from its <see cref="CabinetFile.Offset"/>.</summary>
    public byte[] Unpack(Stream stream, int folder)
    {
        var f = _folders[folder];
        using var reader = new BinaryReader(stream, Encoding.Latin1, leaveOpen: true);
        stream.Seek(f.DataAt, SeekOrigin.Begin);
        var input = new MemoryStream();
        long output = 0;
        for (int i = 0; i < f.Blocks; i++)
        {
            reader.ReadUInt32();
            int packed = reader.ReadUInt16();
            int unpacked = reader.ReadUInt16();
            stream.Seek(_dataReserve, SeekOrigin.Current);
            if (unpacked != LzxDecoder.FrameSize && i != f.Blocks - 1)
            {
                throw new InvalidDataException($"{Name}: data block {i} of folder {folder} unpacks to {unpacked} bytes, expected {LzxDecoder.FrameSize}");
            }

            var block = reader.ReadBytes(packed);
            if (block.Length != packed)
            {
                throw new InvalidDataException($"{Name}: data block {i} of folder {folder} runs past the end of the cabinet");
            }

            input.Write(block);
            output += unpacked;
        }

        int type = f.Compression & 0x0F;
        if (type == Stored)
        {
            return input.Length == output ? input.ToArray() : throw new InvalidDataException($"{Name}: stored folder {folder} holds {input.Length} bytes, its blocks claim {output}");
        }

        if (type != Lzx)
        {
            throw new InvalidDataException($"{Name}: folder {folder} uses compression type {type}; only stored and LZX folders are read");
        }

        try
        {
            return LzxDecoder.Decode(input.ToArray(), (f.Compression >> 8) & 0x1F, (int)output);
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException($"{Name}: folder {folder}: {e.Message}", e);
        }
    }

    private static string ReadName(BinaryReader reader, bool utf8, string cabinet)
    {
        var bytes = new List<byte>();
        for (byte b = reader.ReadByte(); b != 0; b = reader.ReadByte())
        {
            if (bytes.Count == 256)
            {
                throw new InvalidDataException($"{cabinet}: a file name longer than 256 bytes");
            }

            bytes.Add(b);
        }

        return (utf8 ? Encoding.UTF8 : Encoding.Latin1).GetString(bytes.ToArray());
    }

    private readonly record struct Folder(uint DataAt, int Blocks, int Compression);
}
