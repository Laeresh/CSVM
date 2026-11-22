using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Extraction;

namespace CSVM.Flight.Hangar;

/// <summary>What one <see cref="CustomPlaneRecord.ImportInto"/> did: the names stored, those of
/// them suffixed past a stored plane, the planes already stored identically, and the file names
/// skipped.</summary>
public sealed record CustomPlaneImport
{
    /// <summary>Gets every plane written, under the name it was stored as.</summary>
    public List<string> Imported { get; } = new();

    /// <summary>Gets the imported names that took a suffix to keep a stored plane.</summary>
    public List<string> Renamed { get; } = new();

    /// <summary>Gets the planes the store already held identically, which were not written.</summary>
    public List<string> AlreadyThere { get; } = new();

    /// <summary>Gets the file names that did not read or could not be written.</summary>
    public List<string> Skipped { get; } = new();
}

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

    // No swatch row ships more than ten shades, so a variant past nine names no colour.
    private const int MaxShade = 9;

    /// <summary>The def a 204-byte record describes, or null when the bytes are too short, carry
    /// no name, or claim a value outside the decoded ranges (airframe, engine, hardpoints,
    /// pattern, colour, shade, decal, armour, gun ids). Derived fields (+0x28, +0x3c, +0x98..,
    /// +0xa8..) are ignored, and so is the +0x68 RGBA: it is the engine's own cache of what the
    /// colour and shade indices resolve to, which the def recomputes. <see cref="StoredColour"/>
    /// reads it for the cross-check.</summary>
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
        Span<int> colours = [Dword(bytes, 0x44), Dword(bytes, 0x48), Dword(bytes, 0x4c)];
        Span<int> shades = [Dword(bytes, 0x50), Dword(bytes, 0x54), Dword(bytes, 0x58)];
        Span<int> decals = [Dword(bytes, 0x5c), Dword(bytes, 0x60), Dword(bytes, 0x64)];

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

        foreach (int colour in colours)
        {
            inRange &= InRange(colour, CustomPlaneDef.MaxSwatch);
        }

        foreach (int shade in shades)
        {
            inRange &= InRange(shade, MaxShade);
        }

        foreach (int decal in decals)
        {
            inRange &= InRange(decal, CustomPlaneDef.MaxDecal);
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
            NoseDecal = decals[0],
            TailDecal = decals[1],
            WingDecal = decals[2],
        };
        for (int slot = 0; slot < HangarPaintTables.Slots; slot++)
        {
            def.PaintColours[slot] = colours[slot];
            def.PaintShades[slot] = shades[slot];
        }

        for (int slot = 0; slot < def.Guns.Length; slot++)
        {
            int? calibre = gunIds[slot] == GunEmpty ? null : gunIds[slot];
            def.Guns[slot] = new GunChoice(calibre, (twinBits & (1 << slot)) != 0);
        }

        return def;
    }

    /// <summary>A paint slot's RGB as the record itself caches it at +0x68 (RGBA quads, the fourth
    /// byte padding). Not read into the def: the def resolves the same colour from its index pair,
    /// and this is what that resolution is checked against.</summary>
    public static PaintColour StoredColour(ReadOnlySpan<byte> bytes, int slot) =>
        new(bytes[0x68 + (slot * 4)], bytes[0x69 + (slot * 4)], bytes[0x6a + (slot * 4)]);

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

    /// <summary>Every save folder of the original on this machine, one group per install (see the
    /// overload). <paramref name="known"/> are the installs CSVM was told of.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> FindSaves(IEnumerable<string?> known) =>
        FindSaves(known, InstallSearchRoots.ForThisMachine(),
            OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : null);

    /// <summary>The existing save folders of <paramref name="known"/>, then of every install
    /// <see cref="InstallLocator.Candidates"/> finds under <paramref name="roots"/>, then of the
    /// installer's default folder under each Program Files. An absent install still counts: an
    /// uninstall leaves its VirtualStore saves behind. Each group is <see cref="SaveFolders"/>' order.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> FindSaves(
        IEnumerable<string?> known, InstallSearchRoots roots, string? localAppData)
    {
        var installs = new List<string>();
        foreach (var place in known)
        {
            if (!string.IsNullOrWhiteSpace(place) && Path.IsPathRooted(place))
            {
                installs.Add(place);
            }
        }

        installs.AddRange(InstallLocator.Candidates(roots, null));
        if (roots.Windows)
        {
            foreach (var programFiles in roots.ProgramFiles)
            {
                installs.Add(Path.Combine(programFiles, "Microsoft Games", "Crimson Skies"));
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<IReadOnlyList<string>>();
        foreach (var install in installs)
        {
            var folders = new List<string>();
            foreach (var folder in SaveFolders(install, localAppData))
            {
                if (Directory.Exists(folder) && seen.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder))))
                {
                    folders.Add(folder);
                }
            }

            if (folders.Count > 0)
            {
                found.Add(folders);
            }
        }

        return found;
    }

    /// <summary>Where the original install at <paramref name="install"/> (absolute) keeps its saved
    /// planes, in the order the game sees them. The VirtualStore copy under
    /// <paramref name="localAppData"/> (null where none exists) comes first, then <c>Planes</c>.
    /// Windows redirects the game's <c>Planes\%s</c> write under Program Files there.</summary>
    public static IReadOnlyList<string> SaveFolders(string install, string? localAppData)
    {
        RequireAbsolute(install);
        var folders = new List<string>();
        if (!string.IsNullOrEmpty(localAppData) && Path.GetPathRoot(install) is { Length: > 0 } root)
        {
            folders.Add(Path.Combine(localAppData, "VirtualStore", install[root.Length..], "Planes"));
        }

        folders.Add(InstallLocator.ResolveDirectory(install, "Planes") ?? Path.Combine(install, "Planes"));
        return folders;
    }

    /// <summary>Copies every file of each install's folders (read only, the original's own
    /// <c>Planes\*.*</c>) into <paramref name="store"/>. Within one install a name an earlier
    /// folder held is passed over. A file that does not read or write is skipped and named.
    /// ⚠ Never overwrite a stored name: it is the player's own plane, so the import becomes
    /// "name (2)" and on. A stored identical twin counts as already there.</summary>
    public static CustomPlaneImport ImportInto(CustomPlaneStore store, IEnumerable<IReadOnlyList<string>> installs)
    {
        var result = new CustomPlaneImport();
        foreach (var folders in installs)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
            {
                foreach (var file in FilesIn(folder))
                {
                    string fileName = Path.GetFileName(file);
                    if (!seen.Add(fileName))
                    {
                        continue;
                    }

                    if (ReadFile(file) is not { } def || !TrySaveBeside(store, def, result))
                    {
                        result.Skipped.Add(fileName);
                    }
                }
            }
        }

        return result;
    }

    // Walks the name and its suffixes to the first free file or an identical twin. A suffix trims
    // the name so the whole fits the name cap. False when the store cannot be written.
    private static bool TrySaveBeside(CustomPlaneStore store, CustomPlaneDef def, CustomPlaneImport result)
    {
        string name = def.Name;
        try
        {
            for (int n = 1; ; n++)
            {
                string suffix = string.Create(CultureInfo.InvariantCulture, $" ({n})");
                def.Name = n == 1 ? name
                    : name[..Math.Min(name.Length, Math.Max(0, CustomPlaneDef.MaxNameLength - suffix.Length))].TrimEnd() + suffix;
                if (!File.Exists(store.PathFor(def.Name)))
                {
                    store.Save(def);
                    result.Imported.Add(def.Name);
                    if (n > 1)
                    {
                        result.Renamed.Add(def.Name);
                    }

                    return true;
                }

                if (store.Load(def.Name) is { } stored && CustomPlaneStore.Serialize(stored) == CustomPlaneStore.Serialize(def))
                {
                    result.AlreadyThere.Add(def.Name);
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string[] FilesIn(string folder)
    {
        try
        {
            var files = Directory.Exists(folder) ? Directory.GetFiles(folder) : Array.Empty<string>();
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
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

    private static bool InRange(int value, int max) => value >= 0 && value <= max;

    private static void RequireAbsolute(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException($"custom plane import needs an absolute path, got '{path}'");
        }
    }
}
