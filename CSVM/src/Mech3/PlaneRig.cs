using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

public enum RigSurfaceKind { Aileron, Elevator, Rudder }

public enum RigSide { Left, Right }

/// <summary>
/// One airframe's parsed <c>rig.json</c>: marker positions, prop, surface and flare overrides, and
/// the cockpit section.
/// Any section or optional field may be absent, and absent means the original's value. Absence
/// therefore reads as null or an empty collection, never as a default. Vectors are metres in the
/// marker frame, nose -Z, right +X (docs/formats/markers.md).
/// ⚠ Do not add a gun, caliber, ammo or pylon-count field: the rig says where, never what, and
/// those stay in <c>stock_loadouts.json</c> and the save. Node names are not checked here, since
/// that needs the airframe's tree (<see cref="PlaneRigPatch"/>).
/// </summary>
public sealed class PlaneRig
{
    /// <summary>The original's pylon count, <c>pylon1</c> to <c>pylon8</c> on every airframe
    /// (docs/formats/markers.md, Pylons). A rig can move a pylon, never add one.</summary>
    public const int MaxPylon = 8;

    private static readonly Regex PylonRe = new("^pylon([0-9]+)$", RegexOptions.IgnoreCase);

    private static readonly HashSet<string> SectionKeys = new(StringComparer.Ordinal)
    {
        "markers", "props", "surfaces", "flares", "cockpit",
    };

    private static readonly HashSet<string> CockpitKeys = new(StringComparer.Ordinal) { "from", "hide", "gauges" };

    private static readonly HashSet<string> PoseKeys = new(StringComparer.Ordinal) { "position", "rotation", "scale" };

    private static readonly HashSet<string> PropKeys = new(StringComparer.Ordinal)
    {
        "node", "axis", "deg_per_sec", "blur",
    };

    private static readonly HashSet<string> SurfaceKeys = new(StringComparer.Ordinal)
    {
        "node", "kind", "side", "hinge",
    };

    private static readonly HashSet<string> HingeKeys = new(StringComparer.Ordinal) { "origin", "axis" };

    private static readonly HashSet<string> FlareKeys = new(StringComparer.Ordinal) { "left", "right" };

    private PlaneRig(
        string sourceName,
        Dictionary<string, Vector3> markers,
        List<RigProp> props,
        List<RigSurface> surfaces,
        string? leftFlare,
        string? rightFlare,
        RigCockpit? cockpit)
    {
        SourceName = sourceName;
        Markers = markers;
        Props = props;
        Surfaces = surfaces;
        LeftFlare = leftFlare;
        RightFlare = rightFlare;
        Cockpit = cockpit;
    }

    /// <summary>The file or label every validation message names.</summary>
    public string SourceName { get; }

    /// <summary>Marker name to its plane-frame position; a marker not listed keeps the original's.</summary>
    public IReadOnlyDictionary<string, Vector3> Markers { get; }

    public IReadOnlyList<RigProp> Props { get; }

    public IReadOnlyList<RigSurface> Surfaces { get; }

    /// <summary>The node to treat as the port wingtip flare, or null for the original's.</summary>
    public string? LeftFlare { get; }

    /// <summary>The node to treat as the starboard wingtip flare, or null for the original's.</summary>
    public string? RightFlare { get; }

    /// <summary>The <c>cockpit</c> section, or null where the airframe keeps <c>cockpit1</c> as built.</summary>
    public RigCockpit? Cockpit { get; }

    /// <summary>Reads and validates <paramref name="path"/>; the path names every error.</summary>
    public static PlaneRig Load(string path) => Parse(File.ReadAllText(path), path);

    /// <summary>Parses and validates one rig. Throws <see cref="InvalidDataException"/> naming
    /// <paramref name="sourceName"/> and the entry for any key, value or pylon side it rejects.</summary>
    public static PlaneRig Parse(string json, string sourceName)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"plane rig '{sourceName}': not valid JSON: {e.Message}", e);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Fail(sourceName, "root", "not a JSON object");
            }

            CheckKeys(root, SectionKeys, sourceName, "root");
            var markers = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            var props = new List<RigProp>();
            var surfaces = new List<RigSurface>();
            string? left = null;
            string? right = null;
            if (root.TryGetProperty("markers", out var m))
            {
                ReadMarkers(m, sourceName, markers);
            }

            if (root.TryGetProperty("props", out var p))
            {
                foreach (var (entry, label) in Entries(p, sourceName, "props"))
                {
                    props.Add(ReadProp(entry, sourceName, label));
                }

                CheckUniqueNodes(props.ConvertAll(x => x.Node), sourceName, "props");
            }

            if (root.TryGetProperty("surfaces", out var s))
            {
                foreach (var (entry, label) in Entries(s, sourceName, "surfaces"))
                {
                    surfaces.Add(ReadSurface(entry, sourceName, label));
                }

                CheckUniqueNodes(surfaces.ConvertAll(x => x.Node), sourceName, "surfaces");
            }

            if (root.TryGetProperty("flares", out var f))
            {
                if (f.ValueKind != JsonValueKind.Object)
                {
                    throw Fail(sourceName, "flares", "not a JSON object");
                }

                CheckKeys(f, FlareKeys, sourceName, "flares");
                left = OptionalString(f, "left", sourceName, "flares");
                right = OptionalString(f, "right", sourceName, "flares");
            }

            RigCockpit? cockpit = root.TryGetProperty("cockpit", out var c) ? ReadCockpit(c, sourceName) : null;
            return new PlaneRig(sourceName, markers, props, surfaces, left, right, cockpit);
        }
    }

    // A hide list needs body mode: without it the pilot sits in cockpit1, which draws none of the
    // airframe's nodes. Names repeat without case, as PlaneRigPatch matches them.
    private static RigCockpit ReadCockpit(JsonElement section, string source)
    {
        if (section.ValueKind != JsonValueKind.Object)
        {
            throw Fail(source, "cockpit", "not a JSON object");
        }

        CheckKeys(section, CockpitKeys, source, "cockpit");
        bool fromBody = OptionalString(section, "from", source, "cockpit") switch
        {
            null => false,
            "body" => true,
            var other => throw Fail(source, "cockpit.from", $"unknown source '{other}' (body, or no 'from' to keep cockpit1)"),
        };
        var hide = new List<string>();
        if (section.TryGetProperty("hide", out var h))
        {
            if (!fromBody)
            {
                throw Fail(source, "cockpit.hide", "needs \"from\": \"body\", since cockpit1 draws no airframe node");
            }

            if (h.ValueKind != JsonValueKind.Array)
            {
                throw Fail(source, "cockpit.hide", "not a JSON array");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in h.EnumerateArray())
            {
                string label = $"cockpit.hide[{hide.Count.ToString(CultureInfo.InvariantCulture)}]";
                if (entry.ValueKind != JsonValueKind.String || entry.GetString() is not { Length: > 0 } node)
                {
                    throw Fail(source, label, "not a non-empty string");
                }

                if (!seen.Add(node))
                {
                    throw Fail(source, $"{label} ({node})", "node listed twice");
                }

                hide.Add(node);
            }
        }

        var gauges = new Dictionary<string, RigGaugePose>(StringComparer.OrdinalIgnoreCase);
        if (section.TryGetProperty("gauges", out var g))
        {
            if (g.ValueKind != JsonValueKind.Object)
            {
                throw Fail(source, "cockpit.gauges", "not a JSON object");
            }

            foreach (var assembly in g.EnumerateObject())
            {
                string label = "cockpit.gauges." + assembly.Name;
                if (gauges.ContainsKey(assembly.Name))
                {
                    throw Fail(source, label, "defined twice");
                }

                gauges[assembly.Name] = ReadPose(assembly.Value, source, label);
            }
        }

        return new RigCockpit(fromBody, hide, gauges);
    }

    private static RigGaugePose ReadPose(JsonElement e, string source, string label)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw Fail(source, label, "not a JSON object");
        }

        CheckKeys(e, PoseKeys, source, label);
        var position = e.TryGetProperty("position", out var p)
            ? Vec3(p, source, label + ".position")
            : throw Fail(source, label, "missing 'position'");
        Vector3? rotation = e.TryGetProperty("rotation", out var r) ? Vec3(r, source, label + ".rotation") : null;
        float? scale = null;
        if (e.TryGetProperty("scale", out var s))
        {
            float value = Number(s, source, label + ".scale");
            scale = value > 0f ? value : throw Fail(source, label + ".scale", "not a positive number");
        }

        return new RigGaugePose(position, rotation, scale);
    }

    // A pylon keeps its side: odd numbers are port (x < 0), even are starboard (x > 0), and the
    // centreline x == 0 is neither, so it is rejected (docs/formats/markers.md, Pylons).
    private static void ReadMarkers(JsonElement section, string source, Dictionary<string, Vector3> into)
    {
        if (section.ValueKind != JsonValueKind.Object)
        {
            throw Fail(source, "markers", "not a JSON object");
        }

        foreach (var marker in section.EnumerateObject())
        {
            string label = "markers." + marker.Name;
            if (into.ContainsKey(marker.Name))
            {
                throw Fail(source, label, "defined twice");
            }

            var pos = Vec3(marker.Value, source, label);
            var pylon = PylonRe.Match(marker.Name);
            if (pylon.Success)
            {
                bool parsed = int.TryParse(pylon.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n);
                if (!parsed || n < 1 || n > MaxPylon)
                {
                    throw Fail(source, label, "pylon number outside 1..8");
                }

                bool port = n % 2 == 1;
                if (port ? !(pos.X < 0f) : !(pos.X > 0f))
                {
                    throw Fail(source, label, port
                        ? "an odd pylon is port and needs x < 0"
                        : "an even pylon is starboard and needs x > 0");
                }
            }

            into[marker.Name] = pos;
        }
    }

    private static RigProp ReadProp(JsonElement e, string source, string label)
    {
        CheckKeys(e, PropKeys, source, label);
        string node = RequiredString(e, "node", source, label);
        label += $" ({node})";
        Vector3? axis = e.TryGetProperty("axis", out var a) ? Axis(a, source, label + ".axis") : null;
        float? rate = e.TryGetProperty("deg_per_sec", out var r) ? Number(r, source, label + ".deg_per_sec") : null;
        bool? blur = null;
        if (e.TryGetProperty("blur", out var b))
        {
            if (b.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Fail(source, label + ".blur", "not true or false");
            }

            blur = b.ValueKind == JsonValueKind.True;
        }

        return new RigProp(node, axis, rate, blur);
    }

    // Ailerons and elevators need a side, since the classifier splits them by side. The rudder
    // is one sideless kind there, so its side is optional and changes nothing.
    private static RigSurface ReadSurface(JsonElement e, string source, string label)
    {
        CheckKeys(e, SurfaceKeys, source, label);
        string node = RequiredString(e, "node", source, label);
        label += $" ({node})";
        string kindText = RequiredString(e, "kind", source, label);
        RigSurfaceKind kind = kindText switch
        {
            "aileron" => RigSurfaceKind.Aileron,
            "elevator" => RigSurfaceKind.Elevator,
            "rudder" => RigSurfaceKind.Rudder,
            _ => throw Fail(source, label + ".kind", $"unknown kind '{kindText}' (aileron, elevator, rudder)"),
        };
        RigSide? side = OptionalString(e, "side", source, label) switch
        {
            null => null,
            "left" => RigSide.Left,
            "right" => RigSide.Right,
            var other => throw Fail(source, label + ".side", $"unknown side '{other}' (left, right)"),
        };
        if (side == null && kind != RigSurfaceKind.Rudder)
        {
            throw Fail(source, label, "an aileron or elevator needs a side");
        }

        Vector3? origin = null;
        Vector3? axis = null;
        if (e.TryGetProperty("hinge", out var h))
        {
            if (h.ValueKind != JsonValueKind.Object)
            {
                throw Fail(source, label + ".hinge", "not a JSON object");
            }

            CheckKeys(h, HingeKeys, source, label + ".hinge");
            origin = h.TryGetProperty("origin", out var o) ? Vec3(o, source, label + ".hinge.origin") : null;
            axis = h.TryGetProperty("axis", out var a) ? Axis(a, source, label + ".hinge.axis") : null;
        }

        return new RigSurface(node, kind, side, origin, axis);
    }

    private static IEnumerable<(JsonElement Entry, string Label)> Entries(JsonElement section, string source, string name)
    {
        if (section.ValueKind != JsonValueKind.Array)
        {
            throw Fail(source, name, "not a JSON array");
        }

        int i = 0;
        foreach (var entry in section.EnumerateArray())
        {
            string label = $"{name}[{i.ToString(CultureInfo.InvariantCulture)}]";
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw Fail(source, label, "not a JSON object");
            }

            yield return (entry, label);
            i++;
        }
    }

    private static void CheckKeys(JsonElement e, HashSet<string> allowed, string source, string label)
    {
        foreach (var prop in e.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
            {
                throw Fail(source, label, $"unknown key '{prop.Name}' (allowed: {string.Join(", ", allowed)})");
            }
        }
    }

    private static void CheckUniqueNodes(List<string> nodes, string source, string section)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string node in nodes)
        {
            if (!seen.Add(node))
            {
                throw Fail(source, $"{section} ({node})", "node listed twice");
            }
        }
    }

    private static string RequiredString(JsonElement e, string key, string source, string label) =>
        OptionalString(e, key, source, label) ?? throw Fail(source, label, $"missing '{key}'");

    private static string? OptionalString(JsonElement e, string key, string source, string label)
    {
        if (!e.TryGetProperty(key, out var v))
        {
            return null;
        }

        if (v.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(v.GetString()))
        {
            throw Fail(source, $"{label}.{key}", "not a non-empty string");
        }

        return v.GetString();
    }

    private static Vector3 Axis(JsonElement e, string source, string label)
    {
        var axis = Vec3(e, source, label);
        if (axis.LengthSquared() == 0f)
        {
            throw Fail(source, label, "zero-length axis");
        }

        return axis;
    }

    private static Vector3 Vec3(JsonElement e, string source, string label)
    {
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 3)
        {
            throw Fail(source, label, "not an array of 3 numbers");
        }

        return new Vector3(Number(e[0], source, label), Number(e[1], source, label), Number(e[2], source, label));
    }

    // A literal past float range parses as a finite double but stores as infinity, so the check
    // runs on the narrowed value.
    private static float Number(JsonElement e, string source, string label)
    {
        if (e.ValueKind != JsonValueKind.Number)
        {
            throw Fail(source, label, "not a number");
        }

        float value = e.TryGetDouble(out double d) ? (float)d : float.NaN;
        if (!float.IsFinite(value))
        {
            throw Fail(source, label, "not a finite number");
        }

        return value;
    }

    private static InvalidDataException Fail(string source, string label, string problem) =>
        new($"plane rig '{source}': {label}: {problem}");
}

/// <summary>One prop override. A null field keeps the default of the node's name class. That is the
/// axis and rate of <see cref="PropParts.Spin"/>, or the blur disc of a <c>propNb</c>/<c>rotorNb</c>
/// name (<see cref="PropParts.Kind.PropGhost"/>).</summary>
public sealed record RigProp(string Node, Vector3? Axis, float? DegPerSec, bool? Blur);

/// <summary>The <c>cockpit</c> section. In body mode the pilot sits in the replaced airframe, otherwise
/// in the original interior. The hide list names airframe nodes the pilot's pane drops, body mode only.
/// The gauges table maps an assembly to its pose, keys matched without case. An assembly is a child of
/// the interior's <c>gauges</c> node, or the glass key. The names are checked in <see cref="PlaneRigPatch"/>.</summary>
public sealed record RigCockpit(bool FromBody, IReadOnlyList<string> Hide, IReadOnlyDictionary<string, RigGaugePose> Gauges)
{
    /// <summary>The sight glass's key: a polygon wearing <c>cockpit6</c> or <c>crosshair</c>
    /// (<see cref="GunsightReticle.IsGlass"/>), not a node, so it has no name of its own.</summary>
    public const string GlassKey = "glass";
}

/// <summary>One assembly's pose: plane-frame metres in body mode, the interior's own frame and units
/// otherwise. The rotation is Euler degrees in Godot's YXZ order, basis Ry * Rx * Rz.
/// That is how <c>Node3D.RotationDegrees</c> reads them. A null field keeps the assembly's own value in
/// that frame.</summary>
public sealed record RigGaugePose(Vector3 Position, Vector3? RotationDeg, float? Scale);

/// <summary>One control-surface override. <see cref="Side"/> is null only on a rudder; the hinge
/// fields are null where the original's hinge parent group and node-local axis stand.</summary>
public sealed record RigSurface(string Node, RigSurfaceKind Kind, RigSide? Side, Vector3? HingeOrigin, Vector3? HingeAxis)
{
    /// <summary>The <see cref="ControlSurfaces.Kind"/> the original's name match would give.</summary>
    public ControlSurfaces.Kind ClassifierKind => (Kind, Side) switch
    {
        (RigSurfaceKind.Aileron, RigSide.Left) => ControlSurfaces.Kind.AileronLeft,
        (RigSurfaceKind.Aileron, _) => ControlSurfaces.Kind.AileronRight,
        (RigSurfaceKind.Elevator, RigSide.Left) => ControlSurfaces.Kind.ElevatorLeft,
        (RigSurfaceKind.Elevator, _) => ControlSurfaces.Kind.ElevatorRight,
        _ => ControlSurfaces.Kind.Rudder,
    };
}
