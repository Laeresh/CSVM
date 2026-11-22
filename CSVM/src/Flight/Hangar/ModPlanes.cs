using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSVM.Utils;

namespace CSVM.Flight.Hangar;

/// <summary>A mod skipped at load: the id it would have had, its folder or zip, and why.</summary>
public readonly record struct SkippedMod(string Id, string Source, string Reason);

/// <summary>
/// One plane mod, read and checked. It holds its id (the folder or zip name), the stock airframe it
/// inherits from and its literal stat overrides. It also holds the optional cockpit, sound and
/// portrait, and a content hash two machines compare. <see cref="Read"/> reads one of its files.
/// Format: docs/formats/mod-planes.md.
/// </summary>
public sealed class ModPlane
{
    /// <summary>The <see cref="Unlock"/> value that puts the plane in the campaign hangar from the start.</summary>
    public const string UnlockStart = "start";

    /// <summary>The default <see cref="Unlock"/>: the plane enters the hangar with its parent.</summary>
    public const string UnlockWithParent = "with_parent";

    private readonly IReadOnlyDictionary<string, string> _files;
    private readonly bool _zip;

    internal ModPlane(string id, string name, string kindOf, IReadOnlyDictionary<string, double> overrides, bool clearsTurrets,
        string unlock, string? cockpit, string? sound, string? portrait, string source, bool zip, IReadOnlyDictionary<string, string> files, string hash)
    {
        Id = id;
        Name = name;
        KindOf = kindOf;
        Overrides = overrides;
        ClearsTurrets = clearsTurrets;
        Unlock = unlock;
        Cockpit = cockpit;
        Sound = sound;
        Portrait = portrait;
        Source = source;
        _zip = zip;
        _files = files;
        Hash = hash;
    }

    /// <summary>The folder or zip name without <c>.zip</c>, as spelt on disk. Ids match without case.</summary>
    public string Id { get; }

    /// <summary>The name a menu shows: <c>name</c>, else the id.</summary>
    public string Name { get; }

    /// <summary>The stock parent's <c>planes.zbd</c> node, in <see cref="StockAirframes.Nodes"/>' spelling.</summary>
    public string KindOf { get; }

    /// <summary>Def field name to literal value, finite numbers only, applied over the parent's def.</summary>
    public IReadOnlyDictionary<string, double> Overrides { get; }

    /// <summary>Whether <c>"turrets": []</c> clears the parent's turret mounts.</summary>
    public bool ClearsTurrets { get; }

    /// <summary><see cref="UnlockStart"/>, <see cref="UnlockWithParent"/> or a mission or chapter id.</summary>
    public string Unlock { get; }

    /// <summary>The stock node whose cockpit interior the plane borrows, or null for its own or the parent's.</summary>
    public string? Cockpit { get; }

    /// <summary>The stock node whose engine sound the plane borrows, or null for the parent's.</summary>
    public string? Sound { get; }

    /// <summary>The portrait image's path inside the mod (slashes, lower case), or null.</summary>
    public string? Portrait { get; }

    /// <summary>The mod's folder or zip on disk.</summary>
    public string Source { get; }

    /// <summary>SHA-256 over every file's path and bytes, lower-case hex. A folder and a zip of the
    /// same files hash alike.</summary>
    public string Hash { get; }

    /// <summary>The bytes of the file at <paramref name="path"/> inside the mod (<see cref="ModPlanes.ModelFile"/>,
    /// the portrait), matched without case, or null when absent or unreadable.</summary>
    public byte[]? Read(string path)
    {
        if (!_files.TryGetValue(ModPlanes.PathKey(path), out string? at))
        {
            return null;
        }

        try
        {
            return ModPlanes.ReadAt(Source, _zip, at);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Warn("flight", $"mod plane {Id}: {path} unreadable ({e.Message})");
            return null;
        }
    }
}

/// <summary>
/// The plane mods under <c>&lt;dataRoot&gt;/Mods/Planes</c>: each subfolder and each top-level
/// <c>.zip</c> is one mod, read in name order. A mod that fails a check is left out with a warning
/// and listed in <see cref="Skipped"/> for a menu to show; nothing it holds is used.
/// Engine-free. Format: docs/formats/mod-planes.md.
/// </summary>
public sealed class ModPlanes
{
    /// <summary>The mod's definition, at its top.</summary>
    public const string DefinitionFile = "plane.json";

    /// <summary>The mod's model, at its top. A mod without it is skipped.</summary>
    public const string ModelFile = "plane.glb";

    private const string TurretsKey = "turrets";

    private ModPlanes(IReadOnlyList<ModPlane> planes, IReadOnlyList<SkippedMod> skipped)
    {
        Planes = planes;
        Skipped = skipped;
    }

    /// <summary>The mods the session reads, empty until the launcher opens the folder.</summary>
    public static ModPlanes Current { get; set; } = new(Array.Empty<ModPlane>(), Array.Empty<SkippedMod>());

    /// <summary>The mods that passed every check, in name order.</summary>
    public IReadOnlyList<ModPlane> Planes { get; }

    /// <summary>The mods left out, in name order, each with its reason.</summary>
    public IReadOnlyList<SkippedMod> Skipped { get; }

    /// <summary>The folder under the data root that mods are read from.</summary>
    public static string FolderIn(string dataRoot) => Path.Combine(dataRoot, "Mods", "Planes");

    /// <summary>The mods under <paramref name="dataRoot"/>; empty when the folder is absent.</summary>
    public static ModPlanes Open(string dataRoot)
    {
        var planes = new List<ModPlane>();
        var skipped = new List<SkippedMod>();
        string dir = FolderIn(dataRoot);
        if (!Directory.Exists(dir))
        {
            return new(planes, skipped);
        }

        var sources = Directory.EnumerateFileSystemEntries(dir)
            .Where(s => Directory.Exists(s) || s.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        foreach (string source in sources)
        {
            bool zip = !Directory.Exists(source);
            string id = zip ? Path.GetFileNameWithoutExtension(source) : Path.GetFileName(source);
            try
            {
                var plane = Load(id, source, zip);
                if (planes.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException($"id {id} is taken by an earlier mod");
                }

                planes.Add(plane);
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
            {
                skipped.Add(new SkippedMod(id, source, e.Message));
                Log.Warn("flight", $"mod plane {id} skipped: {e.Message} ({source})");
            }
        }

        Log.Info("flight", $"mod planes: {planes.Count} loaded, {skipped.Count} skipped from {dir}");
        return new(planes, skipped);
    }

    /// <summary>The mod whose id is <paramref name="id"/>, matched without case, or null.</summary>
    public ModPlane? Find(string id) =>
        Planes.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    // A path inside a mod as its files are keyed: slashes, lower case.
    internal static string PathKey(string path) => path.Replace('\\', '/').ToLowerInvariant();

    internal static byte[] ReadAt(string source, bool zip, string at)
    {
        if (!zip)
        {
            return File.ReadAllBytes(at);
        }

        using var archive = ZipFile.OpenRead(source);
        using var stream = archive.GetEntry(at)!.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ModPlane Load(string id, string source, bool zip)
    {
        if (id.Length == 0)
        {
            throw new InvalidDataException("the id is empty");
        }

        if (StockAirframes.IdOf(id) != null)
        {
            throw new InvalidDataException($"id {id} is a stock airframe's node name");
        }

        var files = zip ? ZipFiles(source) : FolderFiles(source);
        if (!files.TryGetValue(DefinitionFile, out string? definition))
        {
            throw new InvalidDataException($"no {DefinitionFile} at its top");
        }

        if (!files.ContainsKey(ModelFile))
        {
            throw new InvalidDataException($"no {ModelFile} at its top");
        }

        // Through a stream, which skips a UTF-8 byte order mark; the byte overload refuses one.
        using var doc = JsonDocument.Parse(new MemoryStream(ReadAt(source, zip, definition)));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{DefinitionFile} is not an object");
        }

        string? name = null;
        string? kindOf = null;
        string? cockpit = null;
        string? sound = null;
        string? portrait = null;
        string unlock = ModPlane.UnlockWithParent;
        var overrides = new Dictionary<string, double>(StringComparer.Ordinal);
        bool clearsTurrets = false;
        foreach (var field in doc.RootElement.EnumerateObject())
        {
            switch (field.Name)
            {
                case "name":
                    name = Text(field);
                    break;
                case "kind_of":
                    kindOf = Stock(field);
                    break;
                case "unlock":
                    unlock = Text(field);
                    break;
                case "cockpit":
                    cockpit = Stock(field);
                    break;
                case "sound":
                    sound = Stock(field);
                    break;
                case "portrait":
                    portrait = PathKey(Text(field));
                    if (!files.ContainsKey(portrait))
                    {
                        throw new InvalidDataException($"portrait {portrait} is not in the mod");
                    }

                    break;
                case "overrides":
                    clearsTurrets = Overrides(field.Value, overrides);
                    break;
                default:
                    // A key starting with _ is a note to the reader.
                    if (!field.Name.StartsWith('_'))
                    {
                        throw new InvalidDataException($"unknown key {field.Name} in {DefinitionFile}");
                    }

                    break;
            }
        }

        if (kindOf == null)
        {
            throw new InvalidDataException("kind_of is missing");
        }

        return new ModPlane(id, name ?? id, kindOf, overrides, clearsTurrets, unlock, cockpit, sound, portrait,
            source, zip, files, Hash(source, zip, files));
    }

    // Fills the def overrides and answers whether "turrets": [] clears the parent's mounts.
    private static bool Overrides(JsonElement block, Dictionary<string, double> into)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("overrides is not an object");
        }

        bool clearsTurrets = false;
        foreach (var field in block.EnumerateObject())
        {
            if (field.Name == TurretsKey)
            {
                if (field.Value.ValueKind != JsonValueKind.Array || field.Value.GetArrayLength() != 0)
                {
                    throw new InvalidDataException("overrides.turrets may only be []");
                }

                clearsTurrets = true;
            }
            else if (field.Value.ValueKind == JsonValueKind.Number && double.IsFinite(field.Value.GetDouble()))
            {
                into[field.Name] = field.Value.GetDouble();
            }
            else
            {
                throw new InvalidDataException($"overrides.{field.Name} is not a finite number");
            }
        }

        return clearsTurrets;
    }

    private static string Text(JsonProperty field) =>
        field.Value.ValueKind == JsonValueKind.String && field.Value.GetString() is { Length: > 0 } s
            ? s
            : throw new InvalidDataException($"{field.Name} is not a non-empty string");

    private static string Stock(JsonProperty field) =>
        StockAirframes.IdOf(Text(field)) is { } stock
            ? StockAirframes.Nodes[stock]
            : throw new InvalidDataException($"{field.Name} {Text(field)} is not a stock airframe node ({string.Join(", ", StockAirframes.Nodes)})");

    private static Dictionary<string, string> FolderFiles(string source)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            AddFile(files, Path.GetRelativePath(source, file), file);
        }

        return files;
    }

    // plane.json at the zip's root, or under one top folder, sets the prefix the mod's own files sit under.
    private static Dictionary<string, string> ZipFiles(string source)
    {
        using var archive = ZipFile.OpenRead(source);
        var names = archive.Entries.Select(e => e.FullName).Where(n => !n.EndsWith('/') && !n.EndsWith('\\')).ToList();
        var prefixes = names.Select(PathKey)
            .Where(n => n == DefinitionFile || (n.EndsWith("/" + DefinitionFile, StringComparison.Ordinal) && n.Split('/').Length == 2))
            .Select(n => n[..^DefinitionFile.Length])
            .ToList();
        if (!prefixes.Contains(string.Empty) && prefixes.Count > 1)
        {
            throw new InvalidDataException($"{DefinitionFile} under several top folders ({string.Join(", ", prefixes)})");
        }

        string prefix = prefixes.Contains(string.Empty) ? string.Empty : prefixes.FirstOrDefault() ?? string.Empty;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (PathKey(name).StartsWith(prefix, StringComparison.Ordinal))
            {
                AddFile(files, name[prefix.Length..], name);
            }
        }

        return files;
    }

    // ponytail: dot files (.DS_Store, .git) are left out so a copy's hidden files never split the hash; other OS litter still does.
    private static void AddFile(Dictionary<string, string> files, string relative, string at)
    {
        string key = PathKey(relative);
        if (("/" + key).Contains("/.", StringComparison.Ordinal))
        {
            return;
        }

        if (!files.TryAdd(key, at))
        {
            throw new InvalidDataException($"two files are named {key}, without case");
        }
    }

    // Path order, never enumeration order, so every machine and every copy hashes alike.
    private static string Hash(string source, bool zip, Dictionary<string, string> files)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[8];
        foreach (string key in files.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            byte[] bytes = ReadAt(source, zip, files[key]);
            sha.AppendData(Encoding.UTF8.GetBytes(key + "\0"));
            BinaryPrimitives.WriteInt64LittleEndian(length, bytes.LongLength);
            sha.AppendData(length);
            sha.AppendData(bytes);
        }

        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }
}
