using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CSVM.Flight.Hangar;
using CSVM.Utils;

namespace CSVM.Session.Career;

/// <summary>
/// JSON persistence for careers, one file per career under <c>user://Careers/&lt;name&gt;.json</c>.
/// Each save first copies the previous file to <c>&lt;name&gt;.json.bak</c>, and a career whose file
/// is unreadable loads from that backup. Plain System.IO, like the campaign profile store, so it
/// unit-tests without the engine. Plane builds are written in <see cref="CustomPlaneStore"/>'s own
/// JSON, nested, so a plane reads the same here as in the hangar.
/// </summary>
public sealed class CareerStore
{
    /// <summary>The schema version. A file claiming another version is treated as unreadable.</summary>
    public const int Version = 1;

    private const string Extension = ".json";
    private const string BackupExtension = ".json.bak";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly string _dir;

    /// <summary>A store over <paramref name="directory"/>, which must be absolute.</summary>
    public CareerStore(string directory)
    {
        if (!Path.IsPathRooted(directory))
        {
            throw new ArgumentException($"career store needs an absolute directory, got '{directory}'");
        }

        _dir = directory;
    }

    /// <summary>The production store, <c>Careers/</c> in <see cref="UserData.Root"/>.</summary>
    public static CareerStore UserCareers() => new(Path.Combine(UserData.Root, "Careers"));

    /// <summary>The canonical JSON text for <paramref name="state"/>.</summary>
    public static string Serialize(CareerState state)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            w.WriteString("name", state.Name);
            w.WriteNumber("seed", state.Seed);
            w.WriteNumber("day", state.Day);
            WriteLedger(w, state.Ledger);
            w.WriteStartArray("planes");
            foreach (var plane in state.Planes)
            {
                WritePlane(w, plane);
            }

            w.WriteEndArray();
            w.WriteStartObject("hold");
            WriteRounds(w, state.Hold.Rounds);
            WriteRockets(w, state.Hold.Rockets);
            w.WriteEndObject();
            w.WriteStartObject("shelf");
            WriteRounds(w, state.Shelf.SpecialistStock);
            WriteRockets(w, state.Shelf.RocketStock);
            w.WriteEndObject();
            w.WriteStartObject("board");
            w.WriteNumber("nextId", state.Board.NextId);
            w.WriteStartArray("offers");
            foreach (var offer in state.Board.Offers)
            {
                WriteContract(w, offer);
            }

            w.WriteEndArray();
            w.WriteEndObject();
            if (state.ActiveContract is { } active)
            {
                w.WritePropertyName("activeContract");
                WriteContract(w, active);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The career a JSON text describes, or null when the text is not this version's
    /// shape. Any malformed part fails the whole career: a half-read ledger is worse than none.</summary>
    public static CareerState? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.GetProperty("version").GetInt32() != Version)
            {
                return null;
            }

            var ledgerJson = root.GetProperty("ledger");
            var ledger = Ledger.Restore(
                ledgerJson.GetProperty("cash").GetInt32(),
                ledgerJson.GetProperty("debt").GetInt32(),
                ledgerJson.GetProperty("entries").EnumerateArray().Select(ReadEntry).ToList());
            var planes = root.GetProperty("planes").EnumerateArray().Select(ReadPlane).ToList();
            if (planes.Any(p => p == null))
            {
                return null;
            }

            Contract? active = root.TryGetProperty("activeContract", out var a) ? ReadContract(a) : null;
            var state = CareerState.Restore(
                root.GetProperty("name").GetString() ?? string.Empty,
                root.GetProperty("seed").GetUInt64(),
                root.GetProperty("day").GetInt32(),
                ledger,
                planes!,
                active);

            var hold = root.GetProperty("hold");
            foreach (var (line, n) in ReadRounds(hold))
            {
                state.Hold.AddRounds(line, n);
            }

            foreach (var (id, n) in ReadRockets(hold))
            {
                state.Hold.AddRockets(id, n);
            }

            var shelf = root.GetProperty("shelf");
            state.Shelf.Restore(ReadRounds(shelf), ReadRockets(shelf));
            var board = root.GetProperty("board");
            state.Board.Restore(
                board.GetProperty("nextId").GetInt32(),
                board.GetProperty("offers").EnumerateArray().Select(ReadContract).ToList());
            return state;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The names of every saved career, sorted.</summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_dir))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_dir, "*" + Extension)
            .Select(p => Load(Path.GetFileNameWithoutExtension(p))?.Name)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The career saved as <paramref name="name"/>, from its backup when the file itself
    /// does not read; null when neither does.</summary>
    public CareerState? Load(string name)
    {
        string path = PathFor(name);
        return Read(path) ?? Read(Path.ChangeExtension(path, null) + BackupExtension);
    }

    /// <summary>Writes <paramref name="state"/>, keeping the previous file as the backup.</summary>
    public void Save(CareerState state)
    {
        Directory.CreateDirectory(_dir);
        string path = PathFor(state.Name);
        if (File.Exists(path) && Read(path) != null)
        {
            File.Copy(path, Path.ChangeExtension(path, null) + BackupExtension, overwrite: true);
        }

        AtomicFile.WriteAllText(path, Serialize(state));
    }

    /// <summary>Deletes the career and its backup.</summary>
    public void Delete(string name)
    {
        string path = PathFor(name);
        File.Delete(path);
        File.Delete(Path.ChangeExtension(path, null) + BackupExtension);
    }

    /// <summary>The file a career named <paramref name="name"/> saves to.</summary>
    public string PathFor(string name) => Path.Combine(_dir, CustomPlaneStore.FileKey(name) + Extension);

    private static CareerState? Read(string path) => AtomicFile.ReadAllText(path) is { } text ? Deserialize(text) : null;

    private static void WriteLedger(Utf8JsonWriter w, Ledger ledger)
    {
        w.WriteStartObject("ledger");
        w.WriteNumber("cash", ledger.Cash);
        w.WriteNumber("debt", ledger.Debt);
        w.WriteStartArray("entries");
        foreach (var e in ledger.Entries)
        {
            w.WriteStartObject();
            w.WriteNumber("day", e.Day);
            w.WriteString("category", e.Category.ToString());
            w.WriteNumber("cash", e.Cash);
            w.WriteNumber("debt", e.Debt);
            w.WriteString("note", e.Note);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static LedgerEntry ReadEntry(JsonElement e) => new(
        e.GetProperty("day").GetInt32(),
        Enum.Parse<LedgerCategory>(e.GetProperty("category").GetString() ?? string.Empty),
        e.GetProperty("cash").GetInt32(),
        e.GetProperty("debt").GetInt32(),
        e.GetProperty("note").GetString() ?? string.Empty);

    private static void WritePlane(Utf8JsonWriter w, PlaneInstance plane)
    {
        w.WriteStartObject();
        w.WritePropertyName("build");
        w.WriteRawValue(CustomPlaneStore.Serialize(plane.Build));
        w.WriteStartArray("zoneDamage");
        foreach (var zone in Enum.GetValues<ArmourZone>())
        {
            w.WriteNumberValue(plane.ZoneDamage(zone));
        }

        w.WriteEndArray();
        w.WriteNumber("damagedParts", (int)plane.DamagedParts);
        w.WriteNumber("sorties", plane.Sorties);
        w.WriteNumber("kills", plane.Kills);
        w.WriteEndObject();
    }

    private static PlaneInstance? ReadPlane(JsonElement p)
    {
        if (CustomPlaneStore.Deserialize(p.GetProperty("build").GetRawText()) is not { } build)
        {
            return null;
        }

        var plane = new PlaneInstance(build)
        {
            DamagedParts = (PlaneParts)p.GetProperty("damagedParts").GetInt32(),
            Sorties = p.GetProperty("sorties").GetInt32(),
            Kills = p.GetProperty("kills").GetInt32(),
        };
        var damage = p.GetProperty("zoneDamage").EnumerateArray().Select(d => d.GetInt32()).ToArray();
        foreach (var zone in Enum.GetValues<ArmourZone>())
        {
            plane.SetZoneDamage(zone, damage[(int)zone]);
        }

        return plane;
    }

    private static void WriteRounds(Utf8JsonWriter w, IEnumerable<KeyValuePair<AmmoLine, int>> rounds)
    {
        w.WriteStartArray("rounds");
        foreach (var (line, n) in rounds)
        {
            w.WriteStartObject();
            w.WriteNumber("calibre", line.Calibre);
            w.WriteString("kind", line.Kind.ToString());
            w.WriteNumber("count", n);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static List<KeyValuePair<AmmoLine, int>> ReadRounds(JsonElement owner) =>
        owner.GetProperty("rounds").EnumerateArray()
            .Select(r => new KeyValuePair<AmmoLine, int>(
                new AmmoLine(r.GetProperty("calibre").GetInt32(), Enum.Parse<AmmoKind>(r.GetProperty("kind").GetString() ?? string.Empty)),
                r.GetProperty("count").GetInt32()))
            .ToList();

    private static void WriteRockets(Utf8JsonWriter w, IEnumerable<KeyValuePair<string, int>> rockets)
    {
        w.WriteStartArray("rockets");
        foreach (var (id, n) in rockets)
        {
            w.WriteStartObject();
            w.WriteString("id", id);
            w.WriteNumber("count", n);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static List<KeyValuePair<string, int>> ReadRockets(JsonElement owner) =>
        owner.GetProperty("rockets").EnumerateArray()
            .Select(r => new KeyValuePair<string, int>(r.GetProperty("id").GetString() ?? string.Empty, r.GetProperty("count").GetInt32()))
            .ToList();

    private static void WriteContract(Utf8JsonWriter w, Contract c)
    {
        w.WriteStartObject();
        w.WriteNumber("id", c.Id);
        w.WriteString("job", c.Job.ToString());
        w.WriteString("employer", c.Employer.ToString());
        w.WriteNumber("threat", c.Threat);
        w.WriteNumber("fee", c.Fee);
        w.WriteNumber("offeredOn", c.OfferedOn);
        w.WriteNumber("expiresOn", c.ExpiresOn);
        w.WriteNumber("seed", c.Seed);
        w.WriteEndObject();
    }

    private static Contract ReadContract(JsonElement c) => new(
        c.GetProperty("id").GetInt32(),
        Enum.Parse<JobType>(c.GetProperty("job").GetString() ?? string.Empty),
        Enum.Parse<EmployerKind>(c.GetProperty("employer").GetString() ?? string.Empty),
        c.GetProperty("threat").GetInt32(),
        c.GetProperty("fee").GetInt32(),
        c.GetProperty("offeredOn").GetInt32(),
        c.GetProperty("expiresOn").GetInt32(),
        c.GetProperty("seed").GetUInt64());
}
