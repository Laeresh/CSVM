using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Session;

/// <summary>One owned plane's campaign fit: the ownership record itself, plus the per-gun
/// ammunition and per-pylon ordnance picks <c>docs/formats/saved-games.md</c> finds in the
/// original's 204-byte plane record. This is NOT a hangar build (paint, armour, hardpoint count):
/// those stay in <see cref="Flight.CustomPlaneStore"/>'s global <c>user://Planes/</c>, named here
/// by <see cref="Name"/> only, so deleting a profile can never orphan or delete one.</summary>
public sealed class OwnedPlane
{
    public string Name { get; set; } = string.Empty;
    public int Airframe { get; set; }

    /// <summary>Per-gun-slot ammunition index, four slots. The original's no-gun marker (4) and
    /// the ammunition vocabulary (slug/dumdum/ap/magnesium) are <c>loadouts.md</c>'s; a slot with
    /// no gun mounted keeps the value at rest, since nothing here decides what guns a plane has.</summary>
    public int[] Ammo { get; set; } = new int[4];

    /// <summary>Per-pylon ordnance id, eight cells (four per wing). The id-to-ordnance mapping is
    /// not decoded (<c>saved-games.md</c>, "Where the ammunition and ordnance picks live"), so a
    /// value here is only ever round-tripped, never interpreted.</summary>
    public int[] Ordnance { get; set; } = new int[8];
}

/// <summary>One completed or attempted mission's recorded result, best-of merged the way
/// <c>saved-games.md</c>'s mission-result array is (<c>FUN_00405ce0</c>): the fields this decode
/// closed. The two twelve-byte counter arrays it left undecoded are not carried.</summary>
public sealed class MissionResult
{
    /// <summary>The <c>cm_sequence.zrd</c> flat index, 0..23, the campaign's own mission id.</summary>
    public int Seq { get; set; }
    public int CompletedMask { get; set; }
    public int TimeMs { get; set; }
    public int Shots { get; set; }
    public int Hits { get; set; }
    public int Money { get; set; }
    public int Airframe { get; set; }
    public string PlaneName { get; set; } = string.Empty;
}

/// <summary>One campaign profile's persisted state: wallet, owned planes with their campaign fit,
/// recorded mission results and how far through <c>cm_sequence.zrd</c> the player is. Fields and
/// their defaults come from <c>docs/formats/saved-games.md</c> (structure) and
/// <c>docs/org/hangar.md</c> "The campaign wallet" (starting funds and planes); see
/// <see cref="NewProfile"/>.</summary>
public sealed class CampaignProfileDef
{
    public string Name { get; set; } = string.Empty;
    public int Funds { get; set; }
    public List<OwnedPlane> Planes { get; } = new();
    public int SelectedPlane { get; set; }

    /// <summary>The count of completed missions: both the campaign's tree position (the next
    /// mission is <c>cm_sequence</c> <c>seq == MissionsCompleted</c>) and the save's own field,
    /// <c>UIData +0x338</c>.</summary>
    public int MissionsCompleted { get; set; }
    public List<MissionResult> MissionResults { get; } = new();

    /// <summary>A fresh profile per the traced reset (<c>FUN_004113b0</c>, <c>docs/org/hangar.md</c>
    /// "The campaign wallet"): zero funds, two prebuilt Devastators (<c>langui</c> 511 "Gypsy
    /// Magic", 512 "The Knave"), nothing flown. Ammo/ordnance picks start at the hangar's own
    /// "untouched" values (index 0, the stock fit); the campaign's Ammo Selection screen is what
    /// changes them.</summary>
    public static CampaignProfileDef NewProfile(string name)
    {
        var def = new CampaignProfileDef { Name = name, Funds = 0, SelectedPlane = 0 };
        def.Planes.Add(new OwnedPlane { Name = "Gypsy Magic", Airframe = 5 });
        def.Planes.Add(new OwnedPlane { Name = "The Knave", Airframe = 5 });
        return def;
    }
}

/// <summary>
/// JSON persistence for <see cref="CampaignProfileDef"/>: one directory per profile under
/// <c>user://Profiles/&lt;name&gt;/profile.json</c>, following <see cref="Flight.ScoreStore"/> and
/// <see cref="Flight.CustomPlaneStore"/>'s precedent: plain System.IO so it unit-tests without an
/// engine, a missing/malformed file reads as nothing rather than throwing.
///
/// <para>A profile name is user text entry and becomes a directory name, so it is sanitised the
/// same way <see cref="Flight.CustomPlaneStore"/> sanitises a plane name. Deleting a profile
/// removes only its own directory: hangar planes stay in the global
/// <c>user://Planes/</c> store, referenced here by name only (see <see cref="OwnedPlane"/>), so a
/// deletion can never orphan or delete one.</para>
/// </summary>
public sealed class CampaignProfileStore
{
    /// <summary>The schema version written into every file. A file claiming a version this reader
    /// does not know is treated as malformed rather than half-read.</summary>
    public const int Version = 1;

    private const string FileName = "profile.json";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly string _dir;

    /// <summary>A store over <paramref name="directory"/>, which must be absolute (a relative path
    /// would resolve against whatever the process's working directory happens to be).</summary>
    public CampaignProfileStore(string directory)
    {
        if (!Path.IsPathRooted(directory))
        {
            throw new ArgumentException($"campaign profile store needs an absolute directory, got '{directory}'");
        }

        _dir = directory;
    }

    /// <summary>The production store, <c>user://Profiles/</c> resolved to its OS path.</summary>
    public static CampaignProfileStore UserProfiles() =>
        new(Path.Combine(Godot.ProjectSettings.GlobalizePath("user://"), "Profiles"));

    /// <summary>The canonical JSON text for <paramref name="def"/>.</summary>
    public static string Serialize(CampaignProfileDef def)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            w.WriteString("name", def.Name);
            w.WriteNumber("funds", def.Funds);
            w.WriteNumber("selectedPlane", def.SelectedPlane);
            w.WriteNumber("missionsCompleted", def.MissionsCompleted);
            w.WriteStartArray("planes");
            foreach (var plane in def.Planes)
            {
                w.WriteStartObject();
                w.WriteString("name", plane.Name);
                w.WriteNumber("airframe", plane.Airframe);
                WriteInts(w, "ammo", plane.Ammo);
                WriteInts(w, "ordnance", plane.Ordnance);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("missionResults");
            foreach (var result in def.MissionResults)
            {
                w.WriteStartObject();
                w.WriteNumber("seq", result.Seq);
                w.WriteNumber("completedMask", result.CompletedMask);
                w.WriteNumber("timeMs", result.TimeMs);
                w.WriteNumber("shots", result.Shots);
                w.WriteNumber("hits", result.Hits);
                w.WriteNumber("money", result.Money);
                w.WriteNumber("airframe", result.Airframe);
                w.WriteString("planeName", result.PlaneName);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The def a JSON text describes, or null when the text is not valid JSON, not an
    /// object, or not <see cref="Version"/>. A field the file omits keeps the model's default
    /// rather than failing the whole profile.</summary>
    public static CampaignProfileDef? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int version = ReadInt(root, "version", -1);
            if (root.ValueKind != JsonValueKind.Object || version != Version)
            {
                return null;
            }

            var def = new CampaignProfileDef
            {
                Name = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString() ?? string.Empty
                    : string.Empty,
                Funds = ReadInt(root, "funds", 0),
                SelectedPlane = ReadInt(root, "selectedPlane", 0),
                MissionsCompleted = ReadInt(root, "missionsCompleted", 0),
            };

            if (root.TryGetProperty("planes", out var planes) && planes.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in planes.EnumerateArray())
                {
                    if (p.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var plane = new OwnedPlane
                    {
                        Name = p.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String
                            ? pn.GetString() ?? string.Empty
                            : string.Empty,
                        Airframe = ReadInt(p, "airframe", 0),
                    };
                    ReadInts(p, "ammo", plane.Ammo);
                    ReadInts(p, "ordnance", plane.Ordnance);
                    def.Planes.Add(plane);
                }
            }

            if (root.TryGetProperty("missionResults", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in results.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    def.MissionResults.Add(new MissionResult
                    {
                        Seq = ReadInt(r, "seq", 0),
                        CompletedMask = ReadInt(r, "completedMask", 0),
                        TimeMs = ReadInt(r, "timeMs", 0),
                        Shots = ReadInt(r, "shots", 0),
                        Hits = ReadInt(r, "hits", 0),
                        Money = ReadInt(r, "money", 0),
                        Airframe = ReadInt(r, "airframe", 0),
                        PlaneName = r.TryGetProperty("planeName", out var rn) && rn.ValueKind == JsonValueKind.String
                            ? rn.GetString() ?? string.Empty
                            : string.Empty,
                    });
                }
            }

            return def;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Every stored profile's name, sorted. A missing directory is an empty list (a first
    /// run has created nothing); a profile subdirectory with no readable <c>profile.json</c> is
    /// skipped, never a crash.</summary>
    public IReadOnlyList<string> List()
    {
        var names = new List<string>();
        if (Directory.Exists(_dir))
        {
            foreach (var sub in Directory.GetDirectories(_dir))
            {
                if (TryRead(Path.Combine(sub, FileName)) is { } def)
                {
                    names.Add(def.Name);
                }
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>The stored profile of that name, or null when its file is absent or malformed, or
    /// the name itself cannot be a profile directory.</summary>
    public CampaignProfileDef? Load(string name)
    {
        try
        {
            return TryRead(Path.Combine(DirFor(name), FileName));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Writes <paramref name="def"/> to its name's directory, creating it on first save
    /// and overwriting any existing profile of the same name (the name IS the identity, same as
    /// <see cref="Flight.CustomPlaneStore"/>). Returns the file's absolute path.</summary>
    public string Save(CampaignProfileDef def)
    {
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException("a campaign profile cannot be saved without a name");
        }

        var dir = DirFor(def.Name);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, FileName);
        File.WriteAllText(path, Serialize(def), new UTF8Encoding(false));
        return path;
    }

    /// <summary>Removes the stored profile's whole directory, returning whether one existed. Only
    /// this profile's own directory is touched, never <c>user://Planes/</c>, per the ownership
    /// rule above. A name with no directory is a no-op rather than an error.</summary>
    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            var dir = DirFor(name);
            if (!Directory.Exists(dir))
            {
                return false;
            }

            Directory.Delete(dir, recursive: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The directory this name persists to. Characters a directory name cannot carry
    /// become '_', same treatment <see cref="Flight.CustomPlaneStore.PathFor"/> gives a plane
    /// name. A name that sanitises to nothing but dots is rejected: "." is this store's own root
    /// and ".." its parent, so accepting either would let Save or Delete act on the whole store
    /// (or on all of <c>user://</c>) instead of on one profile.</summary>
    public string DirFor(string name)
    {
        var safe = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        if (safe.Trim('.', ' ').Length == 0)
        {
            throw new ArgumentException($"'{name}' does not sanitise to a usable profile directory name");
        }

        return Path.Combine(_dir, safe);
    }

    private static CampaignProfileDef? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null;
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

    private static void WriteInts(Utf8JsonWriter w, string key, IReadOnlyList<int> values)
    {
        w.WriteStartArray(key);
        foreach (int value in values)
        {
            w.WriteNumberValue(value);
        }

        w.WriteEndArray();
    }

    private static void ReadInts(JsonElement obj, string key, int[] into)
    {
        if (!obj.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int i = 0;
        foreach (var value in arr.EnumerateArray())
        {
            if (i >= into.Length)
            {
                break;
            }

            into[i++] = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int v) ? v : 0;
        }
    }

    private static int ReadInt(JsonElement obj, string key, int fallback) =>
        obj.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number
        && v.TryGetInt32(out int value)
            ? value
            : fallback;
}
