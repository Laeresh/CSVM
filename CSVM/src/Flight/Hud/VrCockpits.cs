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
/// offset in metres along the mount's Z, positive aft. Both stay on the interior, never on
/// <c>XRServer.WorldScale</c>, which would resize the sky and every other aircraft with it. The offset
/// stays on that one axis because a lateral or vertical one puts parallax between the gunsight and
/// the guns. Engine-free.
/// </summary>
public readonly record struct VrCockpit(float InteriorScaleFactor, float EyeOffsetZ)
{
    /// <summary>The shipped values for a plane nobody has judged at the headset: the flat mount, no
    /// offset.</summary>
    public static readonly VrCockpit Default = new(1f, 0f);

    /// <summary>The interior's mount scale in metres per interior unit.</summary>
    public float InteriorScale => PlaneBuilder.InteriorScale * InteriorScaleFactor;

    /// <summary>The eye offset in the aircraft's frame: along the mount's Z, which the fixed head
    /// pitch tilts.</summary>
    public Vector3 EyeOffsetInAircraft =>
        new Basis(Vector3.Right, PlaneBuilder.HeadPitchOffsetRad) * new Vector3(0f, 0f, EyeOffsetZ);
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
    public static readonly VrCockpits Defaults = new(new Dictionary<string, VrCockpit>());

    // The sane range of an entry. A factor outside it is a typo, not a cockpit: at a tenth the panel
    // is a toy, at four times it is a hangar. An eye a metre off the authored point has left the tub.
    private const float MinScaleFactor = 0.25f;
    private const float MaxScaleFactor = 4f;
    private const float MaxEyeOffsetM = 1f;

    private readonly Dictionary<string, VrCockpit> _planes;

    private VrCockpits(Dictionary<string, VrCockpit> planes) => _planes = planes;

    /// <summary>The committed file's location, read through Godot's file access since an exported
    /// build keeps it in the pck.</summary>
    public static string DefaultPath => "res://data/vr_cockpits.json";

    /// <summary>The planes the file names, as read.</summary>
    public IReadOnlyDictionary<string, VrCockpit> Planes => _planes;

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
        using var doc = JsonDocument.Parse(json);
        var planes = new Dictionary<string, VrCockpit>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("planes", out var list)
            || list.ValueKind != JsonValueKind.Object)
        {
            problems.Add("no \"planes\" object, every plane takes the defaults");
            return new VrCockpits(planes);
        }

        foreach (var plane in list.EnumerateObject())
        {
            if (ReadEntry(plane.Value) is { } entry)
            {
                planes[plane.Name] = entry;
            }
            else
            {
                problems.Add($"\"{plane.Name}\" is malformed or out of range, it takes the defaults");
            }
        }

        return new VrCockpits(planes);
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

        float factor = VrCockpit.Default.InteriorScaleFactor;
        float offset = VrCockpit.Default.EyeOffsetZ;
        if (e.TryGetProperty("interiorScaleFactor", out var f))
        {
            if (f.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            factor = f.GetSingle();
        }

        if (e.TryGetProperty("eyeOffsetZ", out var z))
        {
            if (z.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            offset = z.GetSingle();
        }

        bool inRange = float.IsFinite(factor) && factor >= MinScaleFactor && factor <= MaxScaleFactor
            && float.IsFinite(offset) && Math.Abs(offset) <= MaxEyeOffsetM;
        return inRange ? new VrCockpit(factor, offset) : null;
    }
}
