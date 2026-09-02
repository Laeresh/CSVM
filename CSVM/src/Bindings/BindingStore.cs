using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace CSVM.Bindings;

/// <summary>JSON persistence for one seat's <see cref="BindingProfile"/>, one file per player under
/// <c>user://</c>. Named and versioned on purpose: the original writes 2400 unversioned raw bytes to
/// the registry and points its live array at the loaded buffer (`docs/org/input.md`), so a change to
/// its record layout reinterprets an old save as the new one.
/// ⚠ A row this reader cannot read costs that action its saved bindings and nothing more: the
/// action keeps its shipped default and the rest of the file still loads. That is what makes a
/// future format change survivable, so never widen a parse failure into rejecting the file.
/// ⚠ The seat, not the file, decides whether a player reads the keyboard. Persisting that would let
/// a saved file give a pad-only splitscreen seat the keyboard back.</summary>
public sealed class BindingStore
{
    /// <summary>The schema version written into every file. It says how the tokens below are
    /// encoded, not which actions exist: an added action is already handled, since a file that does
    /// not name one leaves it at its default. Bump it when a token's shape changes.</summary>
    public const int Version = 1;

    private const string KeyboardToken = "keyboard";
    private const string MouseToken = "mouse";
    private const string PadPrefix = "pad:";

    // The relaxed encoder, because the default one turns the '+' an axis token carries its sign in
    // into a unicode escape: the file is a keymap a player reads and corrects, and an escaped row
    // is not readable. Nothing renders this text as HTML, which is the case the strict encoder
    // exists for.
    private static readonly JsonWriterOptions WriterOptions =
        new() { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _dir;

    /// <summary>A store over <paramref name="directory"/>, which must be absolute (a relative path
    /// would resolve against whatever the process's working directory happens to be).</summary>
    public BindingStore(string directory)
    {
        if (!Path.IsPathRooted(directory))
        {
            throw new ArgumentException($"binding store needs an absolute directory, got '{directory}'");
        }

        _dir = directory;
    }

    /// <summary>Where the store reads and writes instead of <c>user://</c> while set, so a suite
    /// never loads or overwrites the keymap saved at this machine's controls.</summary>
    public static string? DirectoryOverride { get; set; }

    /// <summary>The production store, <c>user://</c> resolved to its OS path, or the
    /// <see cref="DirectoryOverride"/> while one is set.</summary>
    public static BindingStore UserBindings() =>
        new(DirectoryOverride ?? ProjectSettings.GlobalizePath("user://"));

    /// <summary>The file one player's keymap lives in. Per player, because two players at one
    /// machine hold two different maps and a single file would make them fight over it.</summary>
    public static string FileNameFor(int player) =>
        $"bindings_p{(player >= 1 ? player : 1).ToString(CultureInfo.InvariantCulture)}.json";

    /// <summary>The canonical JSON text for that profile. Every action of every context is written,
    /// including the ones bound to nothing, so a player who unbinds an action gets it back unbound
    /// rather than back at its default.</summary>
    public static string Serialize(int player, BindingProfile profile)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            w.WriteNumber("player", player);
            w.WriteStartObject("contexts");
            foreach (var context in Enum.GetValues<InputContext>())
            {
                w.WriteStartObject(NameOf(context));
                var map = profile.Map(context);
                foreach (var action in DefaultBindings.ActionsIn(context))
                {
                    w.WriteStartArray(action.ToString());
                    foreach (var binding in map.Bindings(action))
                    {
                        w.WriteStringValue(Encode(binding));
                    }

                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The profile a JSON text describes, starting from the shipped defaults for
    /// <paramref name="pad"/> and replacing every action the text gives a readable row for. Text
    /// that is not valid JSON, an unknown context or action name, and a binding token this build
    /// cannot read all leave the action at its default.</summary>
    public static BindingProfile Deserialize(string json, DeviceId pad, bool readsKeyboard)
    {
        var profile = BindingProfile.Defaults(pad, readsKeyboard);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("contexts", out var contexts)
                || contexts.ValueKind != JsonValueKind.Object)
            {
                return profile;
            }

            foreach (var entry in contexts.EnumerateObject())
            {
                if (TryParseContext(entry.Name, out var context) && entry.Value.ValueKind == JsonValueKind.Object)
                {
                    ReadContext(profile.Map(context), context, entry.Value, pad);
                }
            }
        }
        catch (JsonException)
        {
            return BindingProfile.Defaults(pad, readsKeyboard);
        }

        return profile;
    }

    /// <summary>One binding as its stored token, <c>device/control</c>. The device is a name and the
    /// control is a name wherever the engine has one, so the file reads as a keymap and a player can
    /// correct one row in a text editor.</summary>
    public static string Encode(Binding binding)
    {
        string device = binding.Device.Kind switch
        {
            DeviceKind.Keyboard => KeyboardToken,
            DeviceKind.Mouse => MouseToken,
            _ => PadPrefix + binding.Device.Id,
        };
        var c = binding.Control;
        return c.Kind switch
        {
            ControlKind.Key => $"{device}/key:{EnumName<Key>(c.Index)}",
            ControlKind.Button => $"{device}/button:{EnumName<JoyButton>(c.Index)}",
            ControlKind.Axis => string.Format(
                CultureInfo.InvariantCulture,
                "{0}/axis:{1}{2}@{3:0.###}",
                device,
                EnumName<JoyAxis>(c.Index),
                c.Sign < 0 ? "-" : "+",
                c.Deadzone),
            ControlKind.Mouse => $"{device}/mouse:{EnumName<MouseButton>(c.Index)}",
            _ => $"{device}/hat:{c.Index}:{c.Direction}",
        };
    }

    /// <summary>The binding a stored token names, or null when this build cannot read it. A hat
    /// token is deliberately unreadable: Godot reports a d-pad as buttons, so a hat row could only
    /// alias one of them and let two actions hold one direction (`DefaultBindings`).</summary>
    public static Binding? Decode(string token)
    {
        int split = token.LastIndexOf('/');
        if (split <= 0 || split == token.Length - 1)
        {
            return null;
        }

        if (!TryDevice(token[..split], out var device) || !TryControl(token[(split + 1)..], out var control))
        {
            return null;
        }

        return new Binding(device, control);
    }

    /// <summary>That player's stored keymap, or the shipped defaults when the file is absent,
    /// unreadable or malformed. A read failure is never the caller's problem to handle.</summary>
    public BindingProfile Load(int player, DeviceId pad, bool readsKeyboard)
    {
        try
        {
            var path = Path.Combine(_dir, FileNameFor(player));
            return File.Exists(path)
                ? Deserialize(File.ReadAllText(path), pad, readsKeyboard)
                : BindingProfile.Defaults(pad, readsKeyboard);
        }
        catch (IOException)
        {
            return BindingProfile.Defaults(pad, readsKeyboard);
        }
        catch (UnauthorizedAccessException)
        {
            return BindingProfile.Defaults(pad, readsKeyboard);
        }
    }

    /// <summary>Writes that player's keymap atomically: the JSON lands in a sibling temp file and a
    /// rename replaces the real one, so a run that dies mid-write leaves the previous save rather
    /// than a half-written keymap.</summary>
    public void Save(int player, BindingProfile profile)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, FileNameFor(player));
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(player, profile), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private static void ReadContext(ActionMap map, InputContext context, JsonElement element, DeviceId pad)
    {
        foreach (var entry in element.EnumerateObject())
        {
            if (!Enum.TryParse(entry.Name, ignoreCase: true, out InputAction action)
                || DefaultBindings.ContextOf(action) != context
                || entry.Value.ValueKind != JsonValueKind.Array
                || !TryRow(entry.Value, pad, out var bindings))
            {
                continue;
            }

            map.Clear(action);
            foreach (var binding in bindings)
            {
                map.Add(action, binding);
            }
        }
    }

    // The whole row or none of it: one token this build cannot read leaves the action on its
    // default rather than on a keymap the player never chose. An empty array reads as a row of no
    // bindings, which is the deliberate unbind.
    private static bool TryRow(JsonElement array, DeviceId pad, out List<Binding> bindings)
    {
        bindings = new List<Binding>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || Decode(item.GetString()!) is not { } binding)
            {
                return false;
            }

            bindings.Add(DefaultBindings.Retarget(binding, pad));
        }

        return true;
    }

    private static bool TryDevice(string text, out DeviceId device)
    {
        if (text == KeyboardToken)
        {
            device = DeviceId.Keyboard;
            return true;
        }

        if (text == MouseToken)
        {
            device = DeviceId.Mouse;
            return true;
        }

        if (text.StartsWith(PadPrefix, StringComparison.Ordinal) && text.Length > PadPrefix.Length)
        {
            device = DeviceId.Joypad(text[PadPrefix.Length..]);
            return true;
        }

        device = default;
        return false;
    }

    private static bool TryControl(string text, out BindingControl control)
    {
        control = default;
        int colon = text.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        string kind = text[..colon];
        string rest = text[(colon + 1)..];
        switch (kind)
        {
            case "key":
                return TryIndex<Key>(rest, out int keyCode) && Made(BindingControl.Key(keyCode), out control);
            case "button":
                return TryIndex<JoyButton>(rest, out int button) && Made(BindingControl.Button(button), out control);
            case "axis":
                return TryAxis(rest, out control);
            case "mouse":
                return TryIndex<MouseButton>(rest, out int mouseButton) && Made(BindingControl.Mouse(mouseButton), out control);
            default:
                return false;
        }
    }

    private static bool TryAxis(string text, out BindingControl control)
    {
        control = default;
        int at = text.IndexOf('@');
        if (at <= 1 || !float.TryParse(
                text[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out float deadzone))
        {
            return false;
        }

        char signChar = text[at - 1];
        if (signChar != '+' && signChar != '-')
        {
            return false;
        }

        if (!TryIndex<JoyAxis>(text[..(at - 1)], out int axis) || deadzone < 0f || deadzone >= 1f)
        {
            return false;
        }

        control = BindingControl.Axis(axis, signChar == '-' ? -1 : 1, deadzone);
        return true;
    }

    private static bool Made(BindingControl made, out BindingControl control)
    {
        control = made;
        return true;
    }

    // A name the engine enum defines, or the raw number behind a '#' for a code it does not name.
    // Numbers are accepted on the way back in either form, so a file hand-edited to a bare code
    // still loads.
    private static string EnumName<T>(int index)
        where T : struct, Enum
    {
        object value = Enum.ToObject(typeof(T), index);
        return Enum.IsDefined(typeof(T), value)
            ? Enum.GetName(typeof(T), value)!
            : "#" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryIndex<T>(string text, out int index)
        where T : struct, Enum
    {
        string body = text.StartsWith('#') ? text[1..] : text;
        if (int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
        {
            return index >= 0;
        }

        if (Enum.TryParse(body, ignoreCase: false, out T parsed))
        {
            index = Convert.ToInt32(parsed, CultureInfo.InvariantCulture);
            return index >= 0;
        }

        return false;
    }

    private static bool TryParseContext(string text, out InputContext context) =>
        Enum.TryParse(text, ignoreCase: true, out context);

    private static string NameOf(InputContext context) => context.ToString().ToLowerInvariant();
}
