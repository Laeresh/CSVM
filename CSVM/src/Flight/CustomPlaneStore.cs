using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CSVM.Flight;

/// <summary>
/// JSON persistence for <see cref="CustomPlaneDef"/>: one file per plane under
/// <c>user://Planes/</c>. The store works over a plain absolute directory through System.IO so
/// it unit-tests without an engine; <see cref="UserPlanes"/> is the one Godot touch, resolving
/// the <c>user://</c> scheme. Like the stunt-score store, a missing or malformed file reads as
/// nothing rather than throwing: a corrupt save must never break a plane picker.
///
/// <para>Duplicate names: the name IS the identity, as in the original (its writer runs
/// <c>sprintf("Planes\%s", name)</c> and overwrites). Saving over an existing name replaces its
/// file; two names that sanitise to the same filename are the same stored plane.</para>
/// </summary>
public sealed class CustomPlaneStore
{
    /// <summary>The schema version written into every file; a file claiming any other version is
    /// treated as malformed rather than half-read.</summary>
    public const int Version = 1;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly string _dir;

    /// <summary>A store over <paramref name="directory"/>, which must be absolute (a relative
    /// path would resolve against whatever the process's working directory happens to be).</summary>
    public CustomPlaneStore(string directory)
    {
        if (!Path.IsPathRooted(directory))
        {
            throw new ArgumentException($"custom plane store needs an absolute directory, got '{directory}'");
        }

        _dir = directory;
    }

    /// <summary>The production store, <c>user://Planes/</c> resolved to its OS path.</summary>
    public static CustomPlaneStore UserPlanes() =>
        new(Path.Combine(Godot.ProjectSettings.GlobalizePath("user://"), "Planes"));

    /// <summary>The canonical JSON text for <paramref name="def"/>. Field order and formatting
    /// are fixed so that load then save reproduces a file byte for byte; the def is clamped
    /// first, so a file on disk is always in range.</summary>
    public static string Serialize(CustomPlaneDef def)
    {
        def.Clamp();
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            w.WriteString("name", def.Name);
            w.WriteNumber("airframe", def.Airframe);
            w.WriteNumber("engine", def.Engine);
            w.WriteStartObject("armour");
            w.WriteNumber("nose", def.ArmourNose);
            w.WriteNumber("tail", def.ArmourTail);
            w.WriteNumber("leftWing", def.ArmourLeftWing);
            w.WriteNumber("rightWing", def.ArmourRightWing);
            w.WriteEndObject();
            w.WriteStartArray("guns");
            foreach (var gun in def.Guns)
            {
                w.WriteStartObject();
                if (gun.Calibre is { } calibre)
                {
                    w.WriteNumber("calibre", calibre);
                }
                else
                {
                    w.WriteNull("calibre");
                }

                w.WriteBoolean("twin", gun.Twin);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartObject("hardpoints");
            w.WriteNumber("leftWing", def.LeftHardpoints);
            w.WriteNumber("rightWing", def.RightHardpoints);
            w.WriteEndObject();
            w.WriteStartObject("paint");
            w.WriteNumber("pattern", def.PaintPattern);
            w.WriteNumber("pick1", def.PaintPick1);
            w.WriteNumber("pick2", def.PaintPick2);
            w.WriteNumber("pick3", def.PaintPick3);
            WriteColour(w, "colour1", def.Colour1);
            WriteColour(w, "colour2", def.Colour2);
            WriteColour(w, "colour3", def.Colour3);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The def a JSON text describes, clamped into range, or null when the text is not
    /// valid JSON, not an object, or not <see cref="Version"/>. A field the file omits keeps the
    /// model's default rather than failing the whole plane.</summary>
    public static CustomPlaneDef? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadInt(root, "version", -1) != Version)
            {
                return null;
            }

            var def = new CustomPlaneDef
            {
                Name = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString() ?? string.Empty
                    : string.Empty,
                Airframe = ReadInt(root, "airframe", 0),
                Engine = ReadInt(root, "engine", CustomPlaneDef.EngineNone),
            };
            if (root.TryGetProperty("armour", out var armour) && armour.ValueKind == JsonValueKind.Object)
            {
                def.ArmourNose = ReadInt(armour, "nose", 0);
                def.ArmourTail = ReadInt(armour, "tail", 0);
                def.ArmourLeftWing = ReadInt(armour, "leftWing", 0);
                def.ArmourRightWing = ReadInt(armour, "rightWing", 0);
            }

            if (root.TryGetProperty("guns", out var guns) && guns.ValueKind == JsonValueKind.Array)
            {
                int slot = 0;
                foreach (var gun in guns.EnumerateArray())
                {
                    if (slot >= def.Guns.Length)
                    {
                        break;
                    }

                    if (gun.ValueKind == JsonValueKind.Object)
                    {
                        int? calibre =
                            gun.TryGetProperty("calibre", out var c)
                            && c.ValueKind == JsonValueKind.Number
                            && c.TryGetInt32(out int cal)
                                ? cal
                                : null;
                        bool twin = gun.TryGetProperty("twin", out var t) && t.ValueKind == JsonValueKind.True;
                        def.Guns[slot] = new GunChoice(calibre, twin);
                    }

                    slot++;
                }
            }

            if (root.TryGetProperty("hardpoints", out var hp) && hp.ValueKind == JsonValueKind.Object)
            {
                def.LeftHardpoints = ReadInt(hp, "leftWing", 0);
                def.RightHardpoints = ReadInt(hp, "rightWing", 0);
            }

            if (root.TryGetProperty("paint", out var paint) && paint.ValueKind == JsonValueKind.Object)
            {
                def.PaintPattern = ReadInt(paint, "pattern", 0);
                def.PaintPick1 = ReadInt(paint, "pick1", 0);
                def.PaintPick2 = ReadInt(paint, "pick2", 0);
                def.PaintPick3 = ReadInt(paint, "pick3", 0);
                def.Colour1 = ReadColour(paint, "colour1");
                def.Colour2 = ReadColour(paint, "colour2");
                def.Colour3 = ReadColour(paint, "colour3");
            }

            return def.Clamp();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Every loadable plane in the directory, sorted by name. A missing directory is an
    /// empty list (a first run has built nothing); an unreadable or malformed file is skipped,
    /// never a crash.</summary>
    public IReadOnlyList<CustomPlaneDef> List()
    {
        var planes = new List<CustomPlaneDef>();
        if (Directory.Exists(_dir))
        {
            foreach (var file in Directory.GetFiles(_dir, "*.json"))
            {
                var def = TryRead(file);
                if (def != null)
                {
                    planes.Add(def);
                }
            }
        }

        planes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return planes;
    }

    /// <summary>The stored plane of that name, or null when its file is absent or malformed.</summary>
    public CustomPlaneDef? Load(string name) => TryRead(PathFor(name));

    /// <summary>Writes <paramref name="def"/> to its name's file, creating the directory on first
    /// save and replacing any existing plane of the same name (the duplicate-name policy above).
    /// Returns the file's absolute path. A nameless plane is refused: the name screen guarantees
    /// one, so reaching here without it is a caller bug, not a user state.</summary>
    public string Save(CustomPlaneDef def)
    {
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException("a custom plane cannot be saved without a name");
        }

        Directory.CreateDirectory(_dir);
        var path = PathFor(def.Name);
        File.WriteAllText(path, Serialize(def), new UTF8Encoding(false));
        return path;
    }

    /// <summary>The file this name persists to. Characters a filename cannot carry become '_';
    /// the original writes the raw name and simply cannot save such a plane, ours can.</summary>
    public string PathFor(string name)
    {
        var safe = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return Path.Combine(_dir, safe + ".json");
    }

    private static CustomPlaneDef? TryRead(string path)
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

    private static void WriteColour(Utf8JsonWriter w, string key, PaintColour colour)
    {
        w.WriteStartArray(key);
        w.WriteNumberValue(colour.R);
        w.WriteNumberValue(colour.G);
        w.WriteNumberValue(colour.B);
        w.WriteEndArray();
    }

    private static int ReadInt(JsonElement obj, string key, int fallback) =>
        obj.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number
        && v.TryGetInt32(out int value)
            ? value
            : fallback;

    private static PaintColour ReadColour(JsonElement obj, string key)
    {
        if (obj.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() == 3)
        {
            byte[] rgb = new byte[3];
            int i = 0;
            foreach (var v in arr.EnumerateArray())
            {
                rgb[i++] =
                    v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int channel)
                        ? (byte)Math.Clamp(channel, 0, 255)
                        : (byte)0;
            }

            return new PaintColour(rgb[0], rgb[1], rgb[2]);
        }

        return default;
    }
}
