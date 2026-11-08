using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSVM.Net;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>What a course gate is. A pylon gate is two course pylons standing a width apart; a
/// balloon ring is a vertical ring held up by balloons.</summary>
public enum CourseGateKind
{
    PylonGate,
    BalloonRing,
}

/// <summary>Which way a sequence entry flies through its gate: along the gate's heading, or against it.</summary>
public enum GateDirection
{
    Forward,
    Reverse,
}

/// <summary>One step of a course's sequence: the pool gate it names, and the way it is flown.</summary>
public readonly record struct SequenceEntry(string GateId, GateDirection Direction);

/// <summary>One gate of a course's gate pool, in absolute world coordinates. A pylon gate's
/// <see cref="Position"/> is its base midway between the course pylons. A ring's is its centre, so its Y is
/// the ring's height. <see cref="Heading"/> is yaw in degrees, as <see cref="SpawnPoint"/> has it.
/// A pylon gate sizes by <see cref="Width"/> and <see cref="Height"/>, a ring by
/// <see cref="Diameter"/>, and an unused size is 0.</summary>
public sealed record CourseGate(
    string Id, CourseGateKind Kind, Vector3 Position, float Heading, float Width, float Height, float Diameter)
{
    /// <summary>The horizontal way a <see cref="GateDirection.Forward"/> crossing travels.</summary>
    public Vector3 Forward => new Basis(Vector3.Up, Mathf.DegToRad(Heading)) * Vector3.Forward;

    /// <summary>The horizontal way an entry flown <paramref name="direction"/> crosses this gate.</summary>
    public Vector3 FlownWay(GateDirection direction) => direction == GateDirection.Forward ? Forward : -Forward;
}

/// <summary>
/// One authored air race course: a gate pool, the sequence that flies it, and its start point.
/// A gate may appear in the sequence more than once. <see cref="Chapter"/> and <see cref="Name"/>
/// come from the file's path, <c>&lt;chapter&gt;/&lt;name&gt;.json</c>, never from its text.
/// A course is thus shared by copying that one file, which <see cref="CourseLibrary"/> finds.
/// The JSON shape is <see cref="ToJson"/>'s; <see cref="Parse"/> rejects a file it cannot use whole,
/// with the reason, rather than guess at a gate.
/// </summary>
public sealed record Course(
    string Chapter, string Name, SpawnPoint Start, IReadOnlyList<CourseGate> Gates, IReadOnlyList<SequenceEntry> Sequence)
{
    /// <summary>The format version written into every file. A file naming a later one is rejected.</summary>
    public const int Version = 1;

    private static readonly JsonDocumentOptions ReaderOptions =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // The eleven stock nodes a planes list names, in airframe-id order, as Flight.Hangar.StockAirframes
    // keeps them. Read from Mech3, since nothing in Flight outside Hangar may name Hangar.
    private static readonly string[] Nodes = Mech3.InstantAction.Airframes.Select(a => a.Node).ToArray();

    /// <summary>The file's optional <c>title</c>, the name a player sees; empty where it has none.
    /// ⚠ Not in <see cref="Hash"/> nor the score key: retitling a course keeps its bests.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The airframes the file's optional <c>planes</c> admits, by id
    /// (the stock order) in file order, null where it names none and every plane races.
    /// ⚠ Never put it in <see cref="Hash"/> or the score key: the board times the gates. A guest reads
    /// the list from the host's own JSON, so both ends hold the same one.</summary>
    public IReadOnlyList<int>? Planes { get; init; }

    /// <summary>What every screen calls the course: <see cref="Title"/>, or <see cref="Name"/> where it is blank.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Name : Title;

    /// <summary>A stable hex digest of the gate pool (in file order, ids left out), the sequence and
    /// the start point. The name, title, planes, chapter and gate ids are not in it. The score key adds the
    /// name and chapter (<see cref="ScoreStore.KeyFor"/>), and any moved gate starts a fresh board.</summary>
    public string Hash
    {
        get
        {
            using var bytes = new MemoryStream();
            using (var w = new BinaryWriter(bytes))
            {
                WritePose(w, Start.Position, Start.HeadingDeg);
                w.Write(Gates.Count);
                foreach (var gate in Gates)
                {
                    w.Write((int)gate.Kind);
                    WritePose(w, gate.Position, gate.Heading);
                    WriteFloats(w, gate.Width, gate.Height, gate.Diameter);
                }

                w.Write(Sequence.Count);
                foreach (var entry in Sequence)
                {
                    w.Write(Gates.Select(g => g.Id).ToList().IndexOf(entry.GateId));
                    w.Write((int)entry.Direction);
                }
            }

            return Convert.ToHexString(SHA256.HashData(bytes.ToArray()), 0, 8).ToLowerInvariant();
        }
    }

    /// <summary>Whether the course admits airframe <paramref name="airframe"/>, a stock pick or a
    /// custom build of it.</summary>
    public bool Admits(int airframe) => Planes == null || Planes.Contains(airframe);

    /// <summary><paramref name="rules"/> with the airframes this course leaves out barred, the
    /// outlaw list left as it stands (<see cref="NetPlaneRules.CourseBarred"/>).</summary>
    public NetPlaneRules Limits(NetPlaneRules rules)
    {
        int barred = 0;
        for (int airframe = 0; airframe < Nodes.Length; airframe++)
        {
            barred |= Admits(airframe) ? 0 : 1 << airframe;
        }

        return rules with { CourseBarred = (ushort)barred };
    }

    /// <summary>Why the course refuses a launch seating <paramref name="planes"/>, stock nodes, or
    /// null where it admits each. A node outside the eleven passes, as a bot's does.</summary>
    public string? Refuses(IEnumerable<string> planes)
    {
        foreach (string plane in planes)
        {
            if (IdOf(plane) is { } airframe && !Admits(airframe))
            {
                return $"it admits only {string.Join(", ", Planes!.Select(a => Nodes[a]))}, not {plane}";
            }
        }

        return null;
    }

    /// <summary>The course <paramref name="json"/> describes, or null with the reason in
    /// <paramref name="error"/>. Rejected: not JSON, a missing or mistyped field, an unknown token.
    /// Also rejected: a size not above 0, a duplicate gate id, a sequence entry naming no gate. So is
    /// an empty <c>planes</c> list, or one naming a plane twice or outside the eleven.</summary>
    public static Course? Parse(string chapter, string name, string json, out string error)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, ReaderOptions);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("not a JSON object");
            }

            if (root.TryGetProperty("version", out var version) && version.GetInt32() > Version)
            {
                throw new FormatException($"version {version.GetInt32()} is newer than this build reads ({Version})");
            }

            string title = string.Empty;
            if (root.TryGetProperty("title", out var titled))
            {
                title = titled.ValueKind == JsonValueKind.String ? titled.GetString()! : throw new FormatException("course \"title\" is not a string");
            }

            var planes = root.TryGetProperty("planes", out var listed) ? ReadPlanes(listed) : null;
            var start = Field(root, "start", "course");
            var gates = new List<CourseGate>();
            foreach (var g in Field(root, "gates", "course").EnumerateArray())
            {
                var gate = ReadGate(g, gates.Count);
                if (gates.Exists(other => other.Id == gate.Id))
                {
                    throw new FormatException($"gate id \"{gate.Id}\" is used twice");
                }

                gates.Add(gate);
            }

            var sequence = new List<SequenceEntry>();
            foreach (var s in Field(root, "sequence", "course").EnumerateArray())
            {
                string where = $"sequence entry {sequence.Count}";
                string id = Field(s, "gate", where).GetString() ?? string.Empty;
                if (!gates.Exists(g => g.Id == id))
                {
                    throw new FormatException($"{where} names unknown gate \"{id}\"");
                }

                sequence.Add(new SequenceEntry(id, Token(s, "dir", where, "forward", "reverse") == 0
                    ? GateDirection.Forward : GateDirection.Reverse));
            }

            error = string.Empty;
            return new Course(
                chapter, name, new SpawnPoint(Vec(Field(start, "pos", "start")), Number(start, "heading", "start")), gates, sequence)
            {
                Title = title,
                Planes = planes,
            };
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        {
            error = e.Message;
            return null;
        }
    }

    /// <summary>The course a host sent as <paramref name="json"/> under <paramref name="chapter"/> and
    /// <paramref name="name"/>, or null with the reason. The JSON must parse, and its <see cref="Hash"/>
    /// must be the <paramref name="hash"/> it was sent with.</summary>
    public static Course? FromWire(string chapter, string name, string hash, string json, out string error)
    {
        var course = Parse(chapter, name, json, out error);
        if (course != null && !string.Equals(course.Hash, hash, StringComparison.OrdinalIgnoreCase))
        {
            error = $"its gates hash to {course.Hash}, not the {hash} the host sent";
            return null;
        }

        return course;
    }

    /// <summary>The course a host's lobby <paramref name="word"/> carries, or null with the one refusal a
    /// lobby and a launch both show. Refused: no word, a word naming a course other than
    /// <paramref name="wanted"/> (a lobby passes none), and JSON that does not parse or hash as sent.</summary>
    public static Course? FromWord(Net.LobbyCourseMessage? word, (string Chapter, string Name)? wanted, out string refusal)
    {
        if (word is not { } sent)
        {
            refusal = "the host's course has not arrived";
            return null;
        }

        if (wanted is { } launch && (!string.Equals(sent.Chapter, launch.Chapter, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sent.Name, launch.Name, StringComparison.OrdinalIgnoreCase)))
        {
            refusal = $"the host's course is {sent.Chapter}/{sent.Name}";
            return null;
        }

        var course = FromWire(sent.Chapter, sent.Name, sent.Hash, sent.Json, out string error);
        refusal = course == null ? $"the host's course {sent.Chapter}/{sent.Name} is refused: {error}" : "";
        return course;
    }

    /// <summary>The lobby word that carries <paramref name="course"/> under round <paramref name="epoch"/>.</summary>
    public static Net.LobbyCourseMessage WordFor(byte epoch, Course course) =>
        new(epoch, course.Chapter, course.Name, course.Hash, course.ToJson());

    /// <summary>Whether <paramref name="course"/> goes on the wire in one lobby message and its run in
    /// one racer line. An oversize course is left out of the lobby's course list.</summary>
    public static bool FitsWire(Course course) =>
        Net.RaceStandingMessage.Carries(course.Sequence.Count) && WordFor(0, course).Fits;

    /// <summary>The course as indented JSON, the shape <see cref="Parse"/> reads. The top level holds
    /// <c>version</c>, <c>title</c> and <c>planes</c> (stock nodes) where set, <c>start</c>
    /// {<c>pos</c>, <c>heading</c>}, <c>gates</c> and <c>sequence</c>.
    /// A gate is {<c>id</c>, <c>kind</c> <c>pylons</c>|<c>ring</c>, <c>pos</c>, <c>heading</c>} plus
    /// <c>width</c> and <c>height</c>, or <c>diameter</c>. An entry is {<c>gate</c>, <c>dir</c>
    /// <c>forward</c>|<c>reverse</c>}, and a position is [x, y, z].</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            if (!string.IsNullOrWhiteSpace(Title))
            {
                w.WriteString("title", Title);
            }

            if (Planes != null)
            {
                w.WriteStartArray("planes");
                foreach (int airframe in Planes)
                {
                    w.WriteStringValue(Nodes[airframe]);
                }

                w.WriteEndArray();
            }

            w.WriteStartObject("start");
            WriteVec(w, "pos", Start.Position);
            w.WriteNumber("heading", Start.HeadingDeg);
            w.WriteEndObject();
            w.WriteStartArray("gates");
            foreach (var gate in Gates)
            {
                w.WriteStartObject();
                w.WriteString("id", gate.Id);
                w.WriteString("kind", gate.Kind == CourseGateKind.PylonGate ? "pylons" : "ring");
                WriteVec(w, "pos", gate.Position);
                w.WriteNumber("heading", gate.Heading);
                if (gate.Kind == CourseGateKind.PylonGate)
                {
                    w.WriteNumber("width", gate.Width);
                    w.WriteNumber("height", gate.Height);
                }
                else
                {
                    w.WriteNumber("diameter", gate.Diameter);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("sequence");
            foreach (var entry in Sequence)
            {
                w.WriteStartObject();
                w.WriteString("gate", entry.GateId);
                w.WriteString("dir", entry.Direction == GateDirection.Forward ? "forward" : "reverse");
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static CourseGate ReadGate(JsonElement g, int index)
    {
        string where = $"gate {index}";
        string id = Field(g, "id", where).GetString() ?? string.Empty;
        where = $"gate \"{id}\"";
        if (id.Length == 0)
        {
            throw new FormatException($"gate {index} has an empty id");
        }

        bool pylons = Token(g, "kind", where, "pylons", "ring") == 0;
        var position = Vec(Field(g, "pos", where));
        float heading = Number(g, "heading", where);
        var gate = pylons
            ? new CourseGate(id, CourseGateKind.PylonGate, position, heading, Size(g, "width", where), Size(g, "height", where), 0f)
            : new CourseGate(id, CourseGateKind.BalloonRing, position, heading, 0f, 0f, Size(g, "diameter", where));
        return gate;
    }

    private static int? IdOf(string node)
    {
        int at = Array.FindIndex(Nodes, n => string.Equals(n, node, StringComparison.OrdinalIgnoreCase));
        return at < 0 ? null : at;
    }

    private static List<int> ReadPlanes(JsonElement listed)
    {
        if (listed.ValueKind != JsonValueKind.Array || listed.GetArrayLength() == 0)
        {
            throw new FormatException("course \"planes\" is not a list of one or more stock plane nodes");
        }

        var planes = new List<int>();
        foreach (var plane in listed.EnumerateArray())
        {
            string? node = plane.ValueKind == JsonValueKind.String ? plane.GetString() : plane.GetRawText();
            int airframe = IdOf(node ?? "")
                ?? throw new FormatException($"course \"planes\" names unknown plane \"{node}\", expected one of {string.Join(", ", Nodes)}");
            if (planes.Contains(airframe))
            {
                throw new FormatException($"course \"planes\" names \"{node}\" twice");
            }

            planes.Add(airframe);
        }

        return planes;
    }

    private static JsonElement Field(JsonElement owner, string name, string where) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value)
            ? value
            : throw new FormatException($"{where} has no \"{name}\"");

    private static float Number(JsonElement owner, string name, string where) =>
        Field(owner, name, where) is { ValueKind: JsonValueKind.Number } value
            ? value.GetSingle()
            : throw new FormatException($"{where} \"{name}\" is not a number");

    private static float Size(JsonElement owner, string name, string where)
    {
        float size = Number(owner, name, where);
        return size > 0f ? size : throw new FormatException($"{where} \"{name}\" must be above 0, got {size}");
    }

    // The index of the token among the allowed values; anything else is a format error naming them.
    private static int Token(JsonElement owner, string name, string where, params string[] allowed)
    {
        string? token = Field(owner, name, where).GetString();
        int index = Array.IndexOf(allowed, token);
        return index >= 0 ? index : throw new FormatException($"{where} \"{name}\" is \"{token}\", expected {string.Join(" or ", allowed)}");
    }

    private static Vector3 Vec(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 3
            ? new Vector3(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle())
            : throw new FormatException($"position {value.GetRawText()} is not [x, y, z]");

    private static void WriteVec(Utf8JsonWriter w, string name, Vector3 v)
    {
        w.WriteStartArray(name);
        w.WriteNumberValue(v.X);
        w.WriteNumberValue(v.Y);
        w.WriteNumberValue(v.Z);
        w.WriteEndArray();
    }

    private static void WritePose(BinaryWriter w, Vector3 position, float heading) =>
        WriteFloats(w, position.X, position.Y, position.Z, heading);

    // Adding 0 turns -0 into +0, so a heading an editor computed as -0 hashes as the 0 a file reads back.
    private static void WriteFloats(BinaryWriter w, params float[] values)
    {
        foreach (float v in values)
        {
            w.Write(v + 0f);
        }
    }
}
