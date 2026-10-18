using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// One plane's VR cockpit data: a factor on the <c>cockpit1</c> mount's scale, and the seated eye's
/// offset in metres. The offset runs along the mount's Z (positive aft) and its Y (positive up). All
/// of it stays on the interior, never on <c>XRServer.WorldScale</c>, which would resize the sky and
/// every other aircraft with it. No lateral offset exists: the authored eye, the
/// <c>cockpit_camera</c> marker read in interior units, centres every tub. The collimated reticle (<c>Mech3/GunsightReticle.cs</c>) stays on the guns from any eye, and a
/// vertical offset only moves where the glass frames it; engine-free.
/// </summary>
public readonly record struct VrCockpit(float InteriorScaleFactor, float EyeOffsetZ, float EyeOffsetY = 0f)
{
    /// <summary>The shipped values for a plane nobody has judged at the headset: the flat mount, no
    /// offset.</summary>
    public static readonly VrCockpit Default = new(1f, 0f);

    /// <summary>The interior's mount scale in metres per interior unit.</summary>
    public float InteriorScale => PlaneBuilder.InteriorScale * InteriorScaleFactor;

    /// <summary>The eye offset in the aircraft's frame: along the stereo mount's Y and Z, which are
    /// the aircraft's own (<see cref="PlaneBuilder.StereoMount"/>).</summary>
    public Vector3 EyeOffsetInAircraft => new(0f, EyeOffsetY, EyeOffsetZ);
}

/// <summary>
/// The parsed <c>CSVM/data/vr_cockpits.json</c>: per plane node name, a <see cref="VrCockpit"/>.
/// Hand-authored engine config, tuned by the author at the headset; a plane the file leaves out takes
/// <see cref="VrCockpit.Default"/>. Read once per session, and only when the stereo path draws the
/// interior, so a flat launch never opens it. A missing or malformed file, or a malformed entry, logs
/// a warning and falls back to the defaults. The file's fields: docs/architecture/Flight.md.
/// </summary>
public sealed class VrCockpits
{
    /// <summary>Every plane at <see cref="VrCockpit.Default"/>.</summary>
    public static readonly VrCockpits Defaults = new(new Dictionary<string, VrCockpit>(), new List<string>());

    // The sane range of an entry. A factor outside it is a typo, not a cockpit: at a tenth the panel
    // is a toy, at four times it is a hangar. An eye a metre off the authored point has left the tub.
    // The tuning lab clamps to the same range, so every value it saves reads back.
    internal const float MinScaleFactor = 0.25f;
    internal const float MaxScaleFactor = 4f;
    internal const float MaxEyeOffsetM = 1f;

    /// <summary>The scale factor's field name, which the tuning lab's writer also uses.</summary>
    internal const string ScaleField = "interiorScaleFactor";

    /// <summary>The fore and aft offset's field name.</summary>
    internal const string EyeZField = "eyeOffsetZ";

    /// <summary>The up and down offset's field name.</summary>
    internal const string EyeYField = "eyeOffsetY";

    private readonly Dictionary<string, VrCockpit> _planes;
    private readonly List<string> _names;

    private VrCockpits(Dictionary<string, VrCockpit> planes, List<string> names)
    {
        _planes = planes;
        _names = names;
    }

    /// <summary>The committed file's location, read through Godot's file access since an exported
    /// build keeps it in the pck.</summary>
    public static string DefaultPath => "res://data/vr_cockpits.json";

    /// <summary>The planes the file names, as read.</summary>
    public IReadOnlyDictionary<string, VrCockpit> Planes => _planes;

    /// <summary>Every plane name the file lists, malformed entries included, in the file's order:
    /// the order the tuning lab steps through.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>Loads the table (defaults to <see cref="DefaultPath"/>). Never throws: a missing or
    /// unreadable file warns and yields <see cref="Defaults"/>.</summary>
    public static VrCockpits Load(string? path = null)
    {
        path ??= DefaultPath;
        bool viaGodot = path.StartsWith("res://", StringComparison.Ordinal);
        if (viaGodot ? !Godot.FileAccess.FileExists(path) : !File.Exists(path))
        {
            Log.Warn("flight", $"vr cockpits: {path} not found, every plane takes the defaults");
            return Defaults;
        }

        try
        {
            var problems = new List<string>();
            var table = Parse(viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path), problems);
            foreach (var problem in problems)
            {
                Log.Warn("flight", $"vr cockpits: {path}: {problem}");
            }

            Log.Info("flight", $"vr cockpits: {table._planes.Count} plane(s) read from {path}");
            return table;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn("flight", $"vr cockpits: {path} could not be read ({ex.Message}), every plane takes the defaults");
            return Defaults;
        }
    }

    /// <summary>Parses the file's bytes with no file access and no engine calls. Throws
    /// <see cref="JsonException"/> on bytes that are not JSON. A malformed entry is left out, so that
    /// plane takes the default, and named in <paramref name="problems"/>.</summary>
    public static VrCockpits Parse(byte[] json, List<string> problems)
    {
        // An editor may save the file with a byte order mark, which the JSON reader refuses.
        int bom = json.Length >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF ? 3 : 0;
        using var doc = JsonDocument.Parse(json.AsMemory(bom));
        var planes = new Dictionary<string, VrCockpit>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("planes", out var list)
            || list.ValueKind != JsonValueKind.Object)
        {
            problems.Add("no \"planes\" object, every plane takes the defaults");
            return new VrCockpits(planes, names);
        }

        foreach (var plane in list.EnumerateObject())
        {
            names.Add(plane.Name);
            if (ReadEntry(plane.Value) is { } entry)
            {
                planes[plane.Name] = entry;
            }
            else
            {
                problems.Add($"\"{plane.Name}\" is malformed or out of range, it takes the defaults");
            }
        }

        return new VrCockpits(planes, names);
    }

    /// <summary>The plane's entry, or the default for a plane the file leaves out.</summary>
    public VrCockpit For(string planeName) =>
        _planes.TryGetValue(planeName, out var entry) ? entry : VrCockpit.Default;

    // A field the entry leaves out keeps its default; one present must be a number in range.
    private static VrCockpit? ReadEntry(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryNumber(e, ScaleField, VrCockpit.Default.InteriorScaleFactor, out float factor)
            || !TryNumber(e, EyeZField, VrCockpit.Default.EyeOffsetZ, out float z)
            || !TryNumber(e, EyeYField, VrCockpit.Default.EyeOffsetY, out float y))
        {
            return null;
        }

        bool inRange = float.IsFinite(factor) && factor >= MinScaleFactor && factor <= MaxScaleFactor
            && InOffsetRange(z) && InOffsetRange(y);
        return inRange ? new VrCockpit(factor, z, y) : null;
    }

    private static bool InOffsetRange(float metres) => float.IsFinite(metres) && Math.Abs(metres) <= MaxEyeOffsetM;

    // A field left out reads the fallback; one present must be a number.
    private static bool TryNumber(JsonElement entry, string field, float fallback, out float value)
    {
        value = fallback;
        if (!entry.TryGetProperty(field, out var element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        value = element.GetSingle();
        return true;
    }
}
