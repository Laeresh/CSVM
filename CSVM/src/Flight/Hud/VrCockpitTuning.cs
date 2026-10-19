using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CSVM.Flight.Hud;

/// <summary>What one tuning input moves on the flown plane's <see cref="VrCockpit"/>.</summary>
public enum VrTuneKnob
{
    /// <summary><see cref="VrCockpit.InteriorScaleFactor"/>.</summary>
    Scale,

    /// <summary><see cref="VrCockpit.EyeOffsetZ"/>, positive aft.</summary>
    Aft,

    /// <summary><see cref="VrCockpit.EyeOffsetY"/>, positive up.</summary>
    Up,

    /// <summary>Which plane is flown: a step swaps the airframe.</summary>
    Plane,

    /// <summary><see cref="VrCockpit.StoreOffsetZ"/>, positive aft. The keyboard's row cycle stops
    /// before it.</summary>
    StoreAft,

    /// <summary><see cref="VrCockpit.StoreOffsetY"/>, positive up.</summary>
    StoreUp,

    /// <summary><see cref="VrCockpit.StoreSpread"/>.</summary>
    StoreSpread,
}

/// <summary>
/// The arithmetic and the file writer of the <c>--xr-tune</c> lab (<see cref="XrCockpitTuner"/>),
/// engine-free so both unit-test. A step moves one field by a fixed amount, clamped to the file's
/// range (<see cref="VrCockpits"/>). It is rounded to the file's grid, so a saved value reads back
/// exactly. <see cref="TryRewrite"/> edits one plane's numbers in the file's own text. Every other
/// byte stays as it was: the other entries, the notes, the layout.
/// </summary>
public static class VrCockpitTuning
{
    /// <summary>One step of <see cref="VrCockpit.InteriorScaleFactor"/>, fine enough that the
    /// measured alternative factor is a whole number of steps (docs/architecture/Flight.md).</summary>
    public const float ScaleStep = 0.025f;

    /// <summary>One step of either eye offset, in metres.</summary>
    public const float OffsetStepM = 0.01f;

    /// <summary>One step of <see cref="VrCockpit.StoreSpread"/>.</summary>
    public const float SpreadStep = 0.05f;

    // The file's grid: three decimals carries both steps exactly.
    private const int Decimals = 3;

    /// <summary><paramref name="at"/> moved <paramref name="steps"/> steps along
    /// <paramref name="knob"/>, clamped to the file's range. <see cref="VrTuneKnob.Plane"/> moves
    /// nothing here.</summary>
    public static VrCockpit Step(VrCockpit at, VrTuneKnob knob, int steps) => knob switch
    {
        VrTuneKnob.Scale => at with
        {
            InteriorScaleFactor = Snap(Math.Clamp(at.InteriorScaleFactor + (steps * ScaleStep), VrCockpits.MinScaleFactor, VrCockpits.MaxScaleFactor)),
        },
        VrTuneKnob.Aft => at with { EyeOffsetZ = Offset(at.EyeOffsetZ, steps) },
        VrTuneKnob.Up => at with { EyeOffsetY = Offset(at.EyeOffsetY, steps) },
        VrTuneKnob.StoreAft => at with { StoreOffsetZ = Offset(at.StoreOffsetZ, steps, VrCockpits.MaxStoreOffsetM) },
        VrTuneKnob.StoreUp => at with { StoreOffsetY = Offset(at.StoreOffsetY, steps, VrCockpits.MaxStoreOffsetM) },
        VrTuneKnob.StoreSpread => at with
        {
            StoreSpread = Snap(Math.Clamp(at.StoreSpread + (steps * SpreadStep), VrCockpits.MinStoreSpread, VrCockpits.MaxStoreSpread)),
        },
        _ => at,
    };

    /// <summary>The index in <paramref name="names"/> one step of <paramref name="direction"/> from
    /// <paramref name="current"/>, wrapping; a plane the list lacks steps to the first entry. Null
    /// for an empty list.</summary>
    public static int? NextPlane(IReadOnlyList<string> names, string? current, int direction)
    {
        if (names.Count == 0)
        {
            return null;
        }

        int at = -1;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], current, StringComparison.OrdinalIgnoreCase))
            {
                at = i;
                break;
            }
        }

        if (at < 0)
        {
            return 0;
        }

        int step = Math.Sign(direction);
        return ((at + step) % names.Count + names.Count) % names.Count;
    }

    /// <summary>A number as the file writes it: three decimals at most, one at least.</summary>
    public static string Format(float value)
    {
        double rounded = Math.Round((double)value, Decimals, MidpointRounding.AwayFromZero);
        return (rounded == 0d ? 0d : rounded).ToString("0.0##", CultureInfo.InvariantCulture);
    }

    /// <summary>The headset readout: the plane, each field with its step and its control, what the
    /// file holds, and <paramref name="status"/>. <paramref name="selected"/> marks the keyboard's
    /// knob.</summary>
    public static string Readout(string plane, int index, int count, VrCockpit now, VrCockpit saved,
        VrTuneKnob selected, string status)
    {
        var inv = CultureInfo.InvariantCulture;
        string Mark(VrTuneKnob knob) => knob == selected ? "> " : "  ";
        var text = new StringBuilder();
        text.Append(inv, $"{Mark(VrTuneKnob.Plane)}VR cockpit tune: {plane} ({index} of {count}){(now == saved ? string.Empty : ", unsaved")}\n");
        text.Append(inv, $"{Mark(VrTuneKnob.Scale)}scale {Format(now.InteriorScaleFactor)} ({(now.InteriorScale * 1000f).ToString("0.0", inv)} mm per unit), step {Format(ScaleStep)}\n");
        text.Append(inv, $"{Mark(VrTuneKnob.Aft)}eye aft {Format(now.EyeOffsetZ)} m, step {Format(OffsetStepM)}\n");
        text.Append(inv, $"{Mark(VrTuneKnob.Up)}eye up {Format(now.EyeOffsetY)} m, step {Format(OffsetStepM)}\n");
        text.Append(inv, $"  stores aft {Format(now.StoreOffsetZ)} m, up {Format(now.StoreOffsetY)} m, spread {Format(now.StoreSpread)}, step {Format(SpreadStep)}\n");
        text.Append(inv, $"  file: scale {Format(saved.InteriorScaleFactor)}, aft {Format(saved.EyeOffsetZ)}, up {Format(saved.EyeOffsetY)}, stores aft {Format(saved.StoreOffsetZ)}, up {Format(saved.StoreOffsetY)}, spread {Format(saved.StoreSpread)}\n");
        text.Append("  right grip + right stick: left/right scale, forward/back eye; B/A eye up/down\n");
        text.Append("  left grip + right stick: left/right spread, forward/back stores; B/A stores up/down\n");
        text.Append("  either grip + trigger saves, + right stick click resets, + left stick left/right plane\n");
        text.Append("  keys: F22/F23 step the marked row, F24 next row, Shift+F24 save, Ctrl+F24 reset");
        if (status.Length > 0)
        {
            text.Append('\n').Append(status);
        }

        return text.ToString();
    }

    /// <summary>The readout while no control is held: one line, so the cockpit is judged without the
    /// full card over it, and <paramref name="status"/> under it.</summary>
    public static string Summary(string plane, int index, int count, VrCockpit now, VrCockpit saved, string status)
    {
        string line = string.Create(CultureInfo.InvariantCulture, $"tune {plane} ({index} of {count}): scale {Format(now.InteriorScaleFactor)}, aft {Format(now.EyeOffsetZ)}, up {Format(now.EyeOffsetY)}, stores aft {Format(now.StoreOffsetZ)}, up {Format(now.StoreOffsetY)}, spread {Format(now.StoreSpread)}{(now == saved ? string.Empty : ", unsaved")}  [right grip] [left grip: stores]");
        return status.Length > 0 ? line + "\n" + status : line;
    }

    /// <summary>One line for the log: the plane and its six fields.</summary>
    public static string Describe(string plane, VrCockpit entry) =>
        $"{plane} scale {Format(entry.InteriorScaleFactor)} aft {Format(entry.EyeOffsetZ)} m up {Format(entry.EyeOffsetY)} m"
        + $" stores aft {Format(entry.StoreOffsetZ)} m up {Format(entry.StoreOffsetY)} m spread {Format(entry.StoreSpread)}";

    /// <summary>Writes <paramref name="entry"/> as <paramref name="plane"/>'s values into the file's
    /// bytes <paramref name="file"/>. It replaces the numbers in place and adds a missing field or
    /// entry. Every other byte stays, line endings and byte order mark included. It refuses a file
    /// that is not JSON, has no <c>planes</c>, or would not read back, naming <paramref name="why"/>.
    /// </summary>
    public static bool TryRewrite(byte[] file, string plane, VrCockpit entry, out byte[] rewritten, out string why)
    {
        rewritten = file;
        bool bom = file.Length >= 3 && file[0] == 0xEF && file[1] == 0xBB && file[2] == 0xBF;
        var body = bom ? file.AsSpan(3).ToArray() : file;
        List<(int Start, int End, string Text)> edits;
        try
        {
            if (!Plan(body, plane, entry, out edits, out why))
            {
                return false;
            }
        }
        catch (JsonException ex)
        {
            why = $"not JSON ({ex.Message})";
            return false;
        }

        var result = new List<byte>(body.Length + 128);
        if (bom)
        {
            result.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
        }

        int at = 0;
        edits.Sort((a, b) => a.Start.CompareTo(b.Start));
        foreach (var edit in edits)
        {
            result.AddRange(body.AsSpan(at, edit.Start - at).ToArray());
            result.AddRange(Encoding.UTF8.GetBytes(edit.Text));
            at = edit.End;
        }

        result.AddRange(body.AsSpan(at).ToArray());
        byte[] written = result.ToArray();
        if (!ReadsBack(file, written, plane, entry, out why))
        {
            return false;
        }

        rewritten = written;
        return true;
    }

    private static float Offset(float metres, int steps, float max = VrCockpits.MaxEyeOffsetM) =>
        Snap(Math.Clamp(metres + (steps * OffsetStepM), -max, max));

    private static float Snap(float value) => (float)Math.Round((double)value, Decimals, MidpointRounding.AwayFromZero);

    // The edits that write the entry. Each matching entry's numbers are replaced and a missing field
    // follows the last one present. With no match, a new entry follows the last plane.
    private static bool Plan(byte[] body, string plane, VrCockpit entry, out List<(int Start, int End, string Text)> edits, out string why)
    {
        edits = new List<(int, int, string)>();
        string nl = Encoding.UTF8.GetString(body).Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        // An optional field at its default is written only where the entry already has it.
        var fields = new (string Name, float Value, bool Write)[]
        {
            (VrCockpits.ScaleField, entry.InteriorScaleFactor, true),
            (VrCockpits.EyeZField, entry.EyeOffsetZ, true),
            (VrCockpits.EyeYField, entry.EyeOffsetY, entry.EyeOffsetY != 0f),
            (VrCockpits.StoreZField, entry.StoreOffsetZ, entry.StoreOffsetZ != 0f),
            (VrCockpits.StoreYField, entry.StoreOffsetY, entry.StoreOffsetY != 0f),
            (VrCockpits.SpreadField, entry.StoreSpread, entry.StoreSpread != 1f),
        };
        var reader = new Utf8JsonReader(body, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        int planesStart = -1, planesEnd = -1, lastPlaneName = -1, lastPlaneEnd = -1;
        bool matched = false, inEntry = false;
        Scan? scan = null;
        string? property = null;
        while (reader.Read())
        {
            int start = (int)reader.TokenStartIndex;
            int end = (int)reader.BytesConsumed;
            int depth = reader.CurrentDepth;
            if (planesStart < 0)
            {
                if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
                {
                    property = reader.GetString();
                }
                else if (depth == 1 && reader.TokenType == JsonTokenType.StartObject && property == "planes")
                {
                    planesStart = start;
                }

                continue;
            }

            if (planesEnd >= 0)
            {
                continue;
            }

            if (depth == 1 && reader.TokenType == JsonTokenType.EndObject)
            {
                planesEnd = start;
            }
            else if (depth == 2 && reader.TokenType == JsonTokenType.PropertyName)
            {
                lastPlaneName = start;
                scan = string.Equals(reader.GetString(), plane, StringComparison.OrdinalIgnoreCase) ? new Scan() : null;
            }
            else if (depth == 2 && reader.TokenType == JsonTokenType.StartObject && scan != null)
            {
                scan.Start = start;
                inEntry = true;
            }
            else if (depth == 2 && reader.TokenType == JsonTokenType.EndObject && inEntry && scan != null)
            {
                inEntry = false;
                matched = true;
                AddEntryEdits(body, scan, start, fields, nl, edits);
                scan = null;
                lastPlaneEnd = end;
            }
            else if (depth == 3 && inEntry && scan != null)
            {
                scan.Read(ref reader, start, end);
            }
            else if (depth == 2 && reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.PropertyName))
            {
                lastPlaneEnd = end;
            }
        }

        if (planesStart < 0 || planesEnd < 0)
        {
            why = "no \"planes\" object";
            return false;
        }

        if (!matched)
        {
            string fresh = $"\"{plane}\": {{ {Members(fields)} }}";
            bool multiLine = Contains(body, planesStart, planesEnd, (byte)'\n');
            edits.Add(lastPlaneEnd < 0
                ? (planesStart + 1, planesStart + 1, multiLine ? $"{nl}    {fresh}{nl}  " : $" {fresh} ")
                : (lastPlaneEnd, lastPlaneEnd, multiLine ? $",{nl}{Indent(body, lastPlaneName)}{fresh}" : $", {fresh}"));
        }

        why = string.Empty;
        return true;
    }

    private static void AddEntryEdits(byte[] body, Scan scan, int entryEnd, (string Name, float Value, bool Write)[] fields,
        string nl, List<(int Start, int End, string Text)> edits)
    {
        var missing = new List<(string Name, float Value, bool Write)>();
        foreach (var field in fields)
        {
            if (scan.Numbers.TryGetValue(field.Name, out var span))
            {
                edits.Add((span.Start, span.End, Format(field.Value)));
            }
            else if (field.Write)
            {
                missing.Add(field);
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        if (scan.Anchor is { } anchor)
        {
            // A field per line in an entry laid out a field per line, else on the anchor's line.
            string separator = Contains(body, scan.Start, entryEnd, (byte)'\n') ? "," + nl + Indent(body, anchor.NameStart) : ", ";
            edits.Add((anchor.End, anchor.End, separator + Members(missing, separator)));
        }
        else
        {
            string added = Members(missing, ", ");
            edits.Add((scan.Start + 1, scan.Start + 1, scan.HasMembers ? $" {added}," : $" {added} "));
        }
    }

    private static string Members(IEnumerable<(string Name, float Value, bool Write)> fields, string separator = ", ")
    {
        var parts = new List<string>();
        foreach (var (name, value, write) in fields)
        {
            if (write)
            {
                parts.Add($"\"{name}\": {Format(value)}");
            }
        }

        return string.Join(separator, parts);
    }

    // The whitespace a line starts with up to the token at start, or nothing when that token does not
    // open its line.
    private static string Indent(byte[] body, int start)
    {
        int lineStart = start;
        while (lineStart > 0 && body[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        for (int i = lineStart; i < start; i++)
        {
            if (body[i] is not ((byte)' ' or (byte)'\t'))
            {
                return string.Empty;
            }
        }

        return Encoding.UTF8.GetString(body, lineStart, start - lineStart);
    }

    private static bool Contains(byte[] body, int from, int to, byte value) =>
        Array.IndexOf(body, value, from, Math.Max(0, to - from)) >= 0;

    // The rewrite must read back the entry as written, and every other plane as the file had it.
    private static bool ReadsBack(byte[] before, byte[] after, string plane, VrCockpit entry, out string why)
    {
        var problemsBefore = new List<string>();
        var problemsAfter = new List<string>();
        var old = VrCockpits.Parse(before, problemsBefore);
        VrCockpits now;
        try
        {
            now = VrCockpits.Parse(after, problemsAfter);
        }
        catch (JsonException ex)
        {
            why = $"the rewrite is not JSON ({ex.Message})";
            return false;
        }

        if (now.For(plane) != entry || !now.Planes.ContainsKey(plane))
        {
            why = $"the rewrite reads {Describe(plane, now.For(plane))}, not what was written";
            return false;
        }

        foreach (var name in old.Names)
        {
            if (!string.Equals(name, plane, StringComparison.OrdinalIgnoreCase)
                && (now.For(name) != old.For(name) || now.Planes.ContainsKey(name) != old.Planes.ContainsKey(name)))
            {
                why = $"the rewrite changed {name}";
                return false;
            }
        }

        bool added = !ContainsName(old.Names, plane);
        if (now.Names.Count != old.Names.Count + (added ? 1 : 0) || problemsAfter.Count > problemsBefore.Count)
        {
            why = "the rewrite changed the file's other entries";
            return false;
        }

        why = string.Empty;
        return true;
    }

    private static bool ContainsName(IReadOnlyList<string> names, string plane)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, plane, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // One matching entry as read: its opening brace and the byte span of each numeric field. The last
    // of those is where a missing field goes.
    private sealed class Scan
    {
        private string? _property;
        private int _propertyStart;

        public int Start { get; set; }

        public bool HasMembers { get; private set; }

        public Dictionary<string, (int Start, int End)> Numbers { get; } = new(StringComparer.Ordinal);

        public (int NameStart, int End)? Anchor { get; private set; }

        public void Read(ref Utf8JsonReader reader, int start, int end)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                HasMembers = true;
                _property = reader.GetString();
                _propertyStart = start;
                return;
            }

            if (reader.TokenType == JsonTokenType.Number && _property is VrCockpits.ScaleField or VrCockpits.EyeZField or VrCockpits.EyeYField
                or VrCockpits.StoreZField or VrCockpits.StoreYField or VrCockpits.SpreadField)
            {
                Numbers[_property] = (start, end);
                Anchor = (_propertyStart, end);
            }

            _property = null;
        }
    }
}

/// <summary>
/// Steps from one held input: a step as it is pushed past <see cref="Engage"/>. Then, after
/// <see cref="FirstDelayS"/>, one every <see cref="RepeatS"/> while it stays past
/// <see cref="Release"/>, the way a held key repeats. Without repeat it steps once per push, the
/// plane swap's rule. Engine-free.
/// </summary>
public sealed class VrTuneRepeat
{
    /// <summary>The deflection a push must pass to step.</summary>
    public const float Engage = 0.6f;

    /// <summary>The deflection under which a push ends.</summary>
    public const float Release = 0.35f;

    /// <summary>Seconds a push is held before it repeats.</summary>
    public const float FirstDelayS = 0.45f;

    /// <summary>Seconds between repeats.</summary>
    public const float RepeatS = 0.12f;

    private readonly bool _repeats;
    private int _direction;
    private float _held;
    private float _next;

    /// <summary>A stepper that repeats while held, or with <paramref name="repeats"/> false one that
    /// steps once per push.</summary>
    public VrTuneRepeat(bool repeats = true) => _repeats = repeats;

    /// <summary>The steps this frame for a reading <paramref name="value"/> in [-1, 1] held over
    /// <paramref name="dt"/> seconds: -1, 0 or +1.</summary>
    public int Step(float value, float dt)
    {
        int want = value >= Engage ? 1 : value <= -Engage ? -1 : 0;
        if (want == 0 && _direction != 0 && Math.Sign(value) == _direction && Math.Abs(value) > Release)
        {
            want = _direction;
        }

        if (want == 0)
        {
            _direction = 0;
            return 0;
        }

        if (want != _direction)
        {
            _direction = want;
            _held = 0f;
            _next = FirstDelayS;
            return want;
        }

        _held += dt;
        if (!_repeats || _held < _next)
        {
            return 0;
        }

        _next += RepeatS;
        return want;
    }

    /// <summary>Ends the push, as a released input does.</summary>
    public void Clear() => _direction = 0;
}
