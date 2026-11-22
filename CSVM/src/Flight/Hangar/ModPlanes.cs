using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Utils;

namespace CSVM.Flight.Hangar;

/// <summary>A mod skipped at load: the id it would have had, its folder or zip, and why.</summary>
public readonly record struct SkippedMod(string Id, string Source, string Reason);

/// <summary><c>plane.json</c>'s <c>tint</c>: the mask and detail PNGs' paths inside the mod (slashes,
/// lower case) and the material names the livery tints through them.</summary>
public sealed record ModPlaneTint(string Mask, string Detail, IReadOnlyList<string> Materials);

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

    /// <summary>The livery tint, or null for a plane whose own surfaces keep their paint under every livery.</summary>
    public ModPlaneTint? Tint { get; init; }

    /// <summary>The mod's folder or zip on disk.</summary>
    public string Source { get; }

    /// <summary>SHA-256 over every file's path and bytes, lower-case hex. A folder and a zip of the
    /// same files hash alike.</summary>
    public string Hash { get; }

    /// <summary><c>loadout.guns</c>: the four gun slots' default picks, W1 first, in place of the
    /// parent's stock guns; null keeps the parent's.</summary>
    public IReadOnlyList<GunChoice>? DefaultGuns { get; internal init; }

    /// <summary><c>loadout.hardpoints_per_wing</c>: the pylons hung on each wing by default, 0-4;
    /// null keeps the parent's.</summary>
    public int? DefaultHardpointsPerWing { get; internal init; }

    /// <summary><c>hangar_capacity</c>: the hangar's weight capacity in lb, in place of the parent's
    /// stat-table row; null keeps the parent's.</summary>
    public int? HangarCapacity { get; internal init; }

    /// <summary>The plane's stats: the parent's, as <see cref="PlaneStats.Load"/> or, for a bot,
    /// <see cref="PlaneStats.LoadForAi"/> with the parent's AI def loads them, then the overrides and
    /// <see cref="ClearsTurrets"/>. An override name no field takes is logged and ignored.
    /// ⚠ <c>DefName</c> and <c>NodeName</c> stay the parent's: the stock fit and the AI def key on them.</summary>
    public PlaneStats Stats(ZrdrSet zrdr, bool forAi = false, string? aiDefName = null)
    {
        var stats = forAi ? PlaneStats.LoadForAi(zrdr, KindOf, aiDefName) : PlaneStats.Load(zrdr, KindOf);
        foreach (var (name, value) in Overrides)
        {
            if (!stats.OverrideDefField(name, (float)value))
            {
                Log.Warn("flight", $"mod plane {Id}: override {name} is no def field CSVM reads, ignored");
            }
        }

        if (ClearsTurrets)
        {
            stats.TurretMounts.Clear();
        }

        return stats;
    }

    /// <summary>The plane's stock fit: the parent's, or null when it has none. With
    /// <see cref="ClearsTurrets"/> each turret slot is a forward gun slot on the firepoints it names,
    /// so a turret plane's W4 fires ahead from firepoint1 and firepoint2. The plane.json loadout
    /// then replaces the guns and the pylons, as a hangar build's picks do.</summary>
    public LoadoutDef? StockLoadout(StockLoadouts stock)
    {
        var fit = ParentFit(stock);
        if (fit == null || (DefaultGuns == null && DefaultHardpointsPerWing == null))
        {
            return fit;
        }

        var picks = new CustomPlaneDef { LeftHardpoints = DefaultHardpointsPerWing ?? 0, RightHardpoints = DefaultHardpointsPerWing ?? 0 };
        for (int slot = 0; slot < CustomPlaneDef.GunSlots && DefaultGuns != null; slot++)
        {
            picks.Guns[slot] = DefaultGuns[slot];
        }

        var built = CustomPlaneBuild.LoadoutFor(picks, fit);
        built.Guns = DefaultGuns == null ? fit.Guns : built.Guns;
        built.Hardpoints = DefaultHardpointsPerWing == null ? fit.Hardpoints : built.Hardpoints;
        return built;
    }

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

    private LoadoutDef? ParentFit(StockLoadouts stock)
    {
        if (stock.ForModel(KindOf) is not { } fit || !ClearsTurrets)
        {
            return stock.ForModel(KindOf);
        }

        return new LoadoutDef
        {
            Def = fit.Def,
            Model = fit.Model,
            Display = fit.Display,
            Hardpoints = fit.Hardpoints,
            Guns = fit.Guns.Select(g => !g.Turret ? g : new GunSpec
            {
                Slot = g.Slot,
                Mount = g.Mount,
                Caliber = g.Caliber,
                Ammo = g.Ammo,
                Markers = g.Markers,
                Single = g.Single,
                WeaponId = g.WeaponId,
                Rounds = g.Rounds,
            }).ToList(),
        };
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

    /// <summary>The mods folder under the data root, the one <c>--mods=</c> replaces.</summary>
    public const string DefaultFolder = "Mods";

    private const string TurretsKey = "turrets";

    // About six times the stat table's heaviest row, the Balmoral's 15,760 lb. A typo of an extra
    // digit or two still fails.
    private const int MaxHangarCapacity = 100_000;

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

    /// <summary>The folder under the data root, or under the mods folder <paramref name="mods"/>
    /// (relative to the data root unless rooted), that plane mods are read from.</summary>
    public static string FolderIn(string dataRoot, string mods = DefaultFolder) => Path.Combine(dataRoot, mods, "Planes");

    /// <summary>The mods under <paramref name="dataRoot"/>, or under the mods folder <paramref name="mods"/>
    /// (<c>--mods=</c>); empty when the folder is absent.</summary>
    public static ModPlanes Open(string dataRoot, string mods = DefaultFolder)
    {
        var planes = new List<ModPlane>();
        var skipped = new List<SkippedMod>();
        string dir = FolderIn(dataRoot, mods);
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

    /// <summary>The stock fit <paramref name="node"/> flies: a loaded mod's <see cref="ModPlane.StockLoadout"/>,
    /// else the stock table's fit for the node.</summary>
    public LoadoutDef? StockFitFor(StockLoadouts stock, string node) =>
        Find(node) is { } mod ? mod.StockLoadout(stock) : stock.ForModel(node);

    /// <summary>These mods with <paramref name="plane"/> left out and listed in <see cref="Skipped"/>, for
    /// a mod that loaded but failed later (its model, its registration). Logs it as a load skip does.</summary>
    public ModPlanes Skip(ModPlane plane, string reason)
    {
        Log.Warn("flight", $"mod plane {plane.Id} skipped: {reason} ({plane.Source})");
        return new(Planes.Where(p => !ReferenceEquals(p, plane)).ToList(),
            Skipped.Append(new SkippedMod(plane.Id, plane.Source, reason)).ToList());
    }

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
        ModPlaneTint? tint = null;
        GunChoice[]? guns = null;
        int? hardpoints = null;
        int? capacity = null;
        foreach (var field in doc.RootElement.EnumerateObject())
        {
            switch (field.Name)
            {
                case "loadout":
                    (guns, hardpoints) = DefaultLoadout(field.Value);
                    break;
                case "hangar_capacity":
                    capacity = WholeNumber(field.Value, 1, MaxHangarCapacity) ?? throw new InvalidDataException(
                        $"hangar_capacity is not a whole number of pounds from 1 to {MaxHangarCapacity}");
                    break;
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
                case "tint":
                    tint = Tint(field.Value, files);
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
            source, zip, files, Hash(source, zip, files))
        { Tint = tint, DefaultGuns = guns, DefaultHardpointsPerWing = hardpoints, HangarCapacity = capacity };
    }

    // The tint block: a mask and a detail PNG inside the mod, and the non-empty list of materials they tint.
    private static ModPlaneTint Tint(JsonElement block, Dictionary<string, string> files)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("tint is not an object");
        }

        string? mask = null;
        string? detail = null;
        List<string>? materials = null;
        foreach (var field in block.EnumerateObject())
        {
            switch (field.Name)
            {
                case "mask":
                    mask = Png(field, files);
                    break;
                case "detail":
                    detail = Png(field, files);
                    break;
                case "materials":
                    var names = field.Value.ValueKind == JsonValueKind.Array
                        ? field.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList()
                        : null;
                    if (names is not { Count: > 0 } || names.Any(string.IsNullOrEmpty))
                    {
                        throw new InvalidDataException("tint.materials is not a non-empty list of material names");
                    }

                    materials = names.ConvertAll(n => n!);
                    break;
                default:
                    if (!field.Name.StartsWith('_'))
                    {
                        throw new InvalidDataException($"unknown key tint.{field.Name}");
                    }

                    break;
            }
        }

        return new ModPlaneTint(
            mask ?? throw new InvalidDataException("tint.mask is missing"),
            detail ?? throw new InvalidDataException("tint.detail is missing"),
            materials ?? throw new InvalidDataException("tint.materials is missing"));
    }

    // A tint image's path: a .png that is in the mod.
    private static string Png(JsonProperty field, Dictionary<string, string> files)
    {
        string path = PathKey(Text(field));
        if (!path.EndsWith(".png", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"tint.{field.Name} {path} is not a .png");
        }

        return files.ContainsKey(path) ? path : throw new InvalidDataException($"tint.{field.Name} {path} is not in the mod");
    }

    // The loadout block: four gun slots and a per-wing pylon count, each optional. A slot is null
    // (empty) or a calibre 30-70, twin unless it says not. A value outside the hangar's ranges fails.
    private static (GunChoice[]? Guns, int? HardpointsPerWing) DefaultLoadout(JsonElement block)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("loadout is not an object");
        }

        GunChoice[]? guns = null;
        int? hardpoints = null;
        foreach (var field in block.EnumerateObject())
        {
            switch (field.Name)
            {
                case "guns":
                    if (field.Value.ValueKind != JsonValueKind.Array || field.Value.GetArrayLength() != CustomPlaneDef.GunSlots)
                    {
                        throw new InvalidDataException($"loadout.guns is not an array of {CustomPlaneDef.GunSlots} slots");
                    }

                    guns = field.Value.EnumerateArray().Select((gun, i) => DefaultGun(gun, i + 1)).ToArray();
                    break;
                case "hardpoints_per_wing":
                    hardpoints = WholeNumber(field.Value, 0, CustomPlaneDef.MaxHardpointsPerWing) ?? throw new InvalidDataException(
                        $"loadout.hardpoints_per_wing is not a whole number from 0 to {CustomPlaneDef.MaxHardpointsPerWing}");
                    break;
                default:
                    if (!field.Name.StartsWith('_'))
                    {
                        throw new InvalidDataException($"unknown key loadout.{field.Name}");
                    }

                    break;
            }
        }

        return (guns, hardpoints);
    }

    private static GunChoice DefaultGun(JsonElement gun, int slot)
    {
        if (gun.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        string at = $"loadout.guns slot {slot}";
        if (gun.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{at} is neither null nor an object");
        }

        int? row = null;
        bool twin = true;
        foreach (var field in gun.EnumerateObject())
        {
            switch (field.Name)
            {
                case "calibre":
                    // The calibre in hundredths of an inch, 30 (.30 Zephyr) to 70 (.70 Goliath), is dropdown row 0-4.
                    row = WholeNumber(field.Value, 30, 30 + (10 * CustomPlaneDef.MaxCalibre)) is { } c && c % 10 == 0 ? (c - 30) / 10
                        : throw new InvalidDataException($"{at}: calibre is not one of 30, 40, 50, 60, 70");
                    break;
                case "twin":
                    twin = field.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? field.Value.GetBoolean()
                        : throw new InvalidDataException($"{at}: twin is not true or false");
                    break;
                default:
                    throw new InvalidDataException($"{at}: unknown key {field.Name}");
            }
        }

        return row is { } calibre ? new GunChoice(calibre, twin) : throw new InvalidDataException($"{at}: calibre is missing");
    }

    private static int? WholeNumber(JsonElement value, int min, int max) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n) && n >= min && n <= max ? n : null;

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
