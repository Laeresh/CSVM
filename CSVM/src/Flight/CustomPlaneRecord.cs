using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Flight;

/// <summary>
/// Import-only reader for the original's saved custom plane files: the 204-byte records under an
/// install's <c>Planes\</c> directory, one verbatim in-memory record per plane
/// (docs/formats/paint.md "Saved custom planes"). A read is one way, original file to
/// <see cref="CustomPlaneDef"/>; CSVM's own planes persist through <see cref="CustomPlaneStore"/>
/// and never in this format. Like the store, a file this reader cannot make sense of reads as
/// null rather than throwing: an import must never break on one corrupt save.
/// </summary>
public static class CustomPlaneRecord
{
    /// <summary>The record's exact size, the original's <c>fwrite(record, 0xcc, 1)</c>.</summary>
    public const int Length = 0xcc;

    // The name field occupies +0x04 up to the next field at +0x28.
    private const int NameOffset = 0x04;
    private const int NameCapacity = 0x28 - NameOffset;

    // The record stores each armour zone premultiplied by 5 (the armour screen divides on read);
    // the raw ceiling is therefore MaxArmourUnits * 5.
    private const int ArmourScale = 5;

    // Gun id 5 is the dropdown's explicit empty row, one past the last calibre.
    private const int GunEmpty = 5;

    /// <summary>The def a 204-byte record describes, or null when the bytes are too short, carry
    /// no name, or claim a value outside the decoded ranges (airframe, engine, hardpoints,
    /// pattern, armour, gun ids). Derived fields (+0x28, +0x3c, +0x98.., +0xa8..) and the
    /// undecoded dwords (+0x00, +0x44..+0x58, +0xc8) are ignored; the +0x5c/+0x60 composite picks
    /// and the +0x64 dword are carried opaquely, never interpreted.</summary>
    public static CustomPlaneDef? Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length)
        {
            return null;
        }

        var name = ReadName(bytes.Slice(NameOffset, NameCapacity));
        if (name.Length == 0)
        {
            return null;
        }

        int airframe = Dword(bytes, 0x2c);
        int engine = Dword(bytes, 0x30);
        int leftHardpoints = Dword(bytes, 0x34);
        int rightHardpoints = Dword(bytes, 0x38);
        int pattern = Dword(bytes, 0x40);
        int twinBits = Dword(bytes, 0x84);
        Span<int> armour = [Dword(bytes, 0x74), Dword(bytes, 0x78), Dword(bytes, 0x7c), Dword(bytes, 0x80)];
        Span<int> gunIds = [Dword(bytes, 0x88), Dword(bytes, 0x8c), Dword(bytes, 0x90), Dword(bytes, 0x94)];

        bool inRange =
            InRange(airframe, CustomPlaneDef.MaxAirframe)
            && InRange(engine, CustomPlaneDef.EngineNone)
            && InRange(leftHardpoints, CustomPlaneDef.MaxHardpointsPerWing)
            && InRange(rightHardpoints, CustomPlaneDef.MaxHardpointsPerWing)
            && InRange(pattern, CustomPlaneDef.MaxPaintPattern);
        foreach (int raw in armour)
        {
            inRange &= InRange(raw, CustomPlaneDef.MaxArmourUnits * ArmourScale);
        }

        foreach (int id in gunIds)
        {
            inRange &= InRange(id, GunEmpty);
        }

        if (!inRange)
        {
            return null;
        }

        var def = new CustomPlaneDef
        {
            Name = name,
            Airframe = airframe,
            Engine = engine,
            LeftHardpoints = leftHardpoints,
            RightHardpoints = rightHardpoints,
            ArmourNose = armour[0] / ArmourScale,
            ArmourTail = armour[1] / ArmourScale,
            ArmourLeftWing = armour[2] / ArmourScale,
            ArmourRightWing = armour[3] / ArmourScale,
            PaintPattern = pattern,
            PaintPick1 = Dword(bytes, 0x5c),
            PaintPick2 = Dword(bytes, 0x60),
            PaintPick3 = Dword(bytes, 0x64),
            Colour1 = Colour(bytes, 0x68),
            Colour2 = Colour(bytes, 0x6c),
            Colour3 = Colour(bytes, 0x70),
        };
        for (int slot = 0; slot < def.Guns.Length; slot++)
        {
            int? calibre = gunIds[slot] == GunEmpty ? null : gunIds[slot];
            def.Guns[slot] = new GunChoice(calibre, (twinBits & (1 << slot)) != 0);
        }

        return def;
    }

    /// <summary>The def stored in <paramref name="path"/> (absolute), or null when the file is
    /// absent, unreadable, or not a valid record.</summary>
    public static CustomPlaneDef? ReadFile(string path)
    {
        RequireAbsolute(path);
        try
        {
            return File.Exists(path) ? Read(File.ReadAllBytes(path)) : null;
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

    /// <summary>Every readable saved plane in an install's <c>Planes</c> directory (absolute
    /// path), sorted by name. The directory is only ever read. The original's saves carry no
    /// extension, so every file is tried (its own scan is <c>Planes\*.*</c>); a missing directory
    /// or an unreadable file contributes nothing.</summary>
    public static IReadOnlyList<CustomPlaneDef> ImportDirectory(string directory)
    {
        RequireAbsolute(directory);
        var planes = new List<CustomPlaneDef>();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                var def = ReadFile(file);
                if (def != null)
                {
                    planes.Add(def);
                }
            }
        }

        planes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return planes;
    }

    // The name is NUL-terminated within its field; the original writes it with sprintf, so bytes
    // past the terminator are whatever was there before. Latin-1 mirrors the original's ANSI
    // filenames byte for byte.
    private static string ReadName(ReadOnlySpan<byte> field)
    {
        int end = field.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? field : field[..end]).Trim();
    }

    private static int Dword(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));

    // Colours are stored as RGBA byte quads; the fourth byte is padding and is ignored.
    private static PaintColour Colour(ReadOnlySpan<byte> bytes, int offset) =>
        new(bytes[offset], bytes[offset + 1], bytes[offset + 2]);

    private static bool InRange(int value, int max) => value >= 0 && value <= max;

    private static void RequireAbsolute(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException($"custom plane import needs an absolute path, got '{path}'");
        }
    }
}
