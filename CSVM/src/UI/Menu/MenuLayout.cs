using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CSVM.UI.Menu;

/// <summary>An ARGB colour as the layout authors it (<c>0xAARRGGBB</c>).</summary>
public readonly record struct MenuLayoutColor(byte A, byte R, byte G, byte B)
{
    /// <summary>Parses the authored hex token. The four byte-rotated slips the shipped file
    /// carries parse as authored; the reader does not second-guess them.</summary>
    public static bool TryParse(string? token, out MenuLayoutColor color)
    {
        color = default;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
        {
            return false;
        }

        color = new MenuLayoutColor((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }
}

/// <summary>One field of a widget type: its name, the value kind the artifact declares for it
/// (<c>int</c>, <c>color</c>, <c>art</c>, <c>resid</c>, <c>script</c>, <c>bool</c>, <c>text</c>)
/// and whether a row may omit it.</summary>
public sealed record MenuLayoutField(string Name, string Kind, bool Optional);

/// <summary>One widget type's field table, the artifact's own description of how to read a row
/// of that type. A reader consults <see cref="KindOf"/> rather than carrying a field table.</summary>
public sealed class MenuLayoutWidgetType
{
    private readonly Dictionary<string, MenuLayoutField> _byName = new(StringComparer.Ordinal);

    internal MenuLayoutWidgetType(string type, string widget, string scriptClass, IReadOnlyList<MenuLayoutField> fields)
    {
        Type = type;
        Widget = widget;
        ScriptClass = scriptClass;
        Fields = fields;
        foreach (var field in fields)
        {
            _byName[field.Name] = field;
        }
    }

    /// <summary>The one-letter type code a row starts with.</summary>
    public string Type { get; }

    /// <summary>The widget's name in words (button, pane, text, ...).</summary>
    public string Widget { get; }

    /// <summary>The script control class the type binds to.</summary>
    public string ScriptClass { get; }

    /// <summary>The fields in row order.</summary>
    public IReadOnlyList<MenuLayoutField> Fields { get; }

    /// <summary>The declared kind of a field, or null for a name the type does not carry.</summary>
    public string? KindOf(string field) => _byName.TryGetValue(field, out var f) ? f.Kind : null;
}

/// <summary>One widget row as decoded: its key, its type, every field's resolved value, the
/// raw macro tokens where substitution changed a value, and the decoder's derived readings
/// (art names, string join, navigation target, strip frame count). Typed accessors consult the
/// type table's kinds and refuse a field whose kind does not match the ask.</summary>
public sealed class MenuLayoutWidget
{
    private readonly IReadOnlyDictionary<string, string> _fields;
    private readonly IReadOnlyDictionary<string, string> _authored;

    internal MenuLayoutWidget(
        string key,
        MenuLayoutWidgetType? type,
        string typeCode,
        string widget,
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyDictionary<string, string> authored,
        IReadOnlyList<string> art,
        string? resIdSymbol,
        int? resId,
        string? text,
        string textSource,
        string? navigateTo,
        int frames)
    {
        Key = key;
        Type = type;
        TypeCode = typeCode;
        Widget = widget;
        _fields = fields;
        _authored = authored;
        Art = art;
        ResIdSymbol = resIdSymbol;
        ResId = resId;
        Text = text;
        TextSource = textSource;
        NavigateTo = navigateTo;
        Frames = frames;
    }

    /// <summary>The row's key, as authored (<c>MM_B_CAMPAIGN</c>).</summary>
    public string Key { get; }

    /// <summary>The type table entry, or null when the artifact declares no such type.</summary>
    public MenuLayoutWidgetType? Type { get; }

    /// <summary>The one-letter type code.</summary>
    public string TypeCode { get; }

    /// <summary>The widget's name in words.</summary>
    public string Widget { get; }

    /// <summary>The art files the row names, in field order.</summary>
    public IReadOnlyList<string> Art { get; }

    /// <summary>The string symbol, kept beside the text so a missing string is diagnosable.</summary>
    public string? ResIdSymbol { get; }

    /// <summary>The numeric string id, or null when the row carries none.</summary>
    public int? ResId { get; }

    /// <summary>The resolved string, or null.</summary>
    public string? Text { get; }

    /// <summary>Where <see cref="Text"/> came from: <c>resource</c>, <c>runtime</c>, <c>none</c>,
    /// <c>unresolved-symbol</c> or <c>unresolved-string</c>.</summary>
    public string TextSource { get; }

    /// <summary>The screen a button navigates to, or null.</summary>
    public string? NavigateTo { get; }

    /// <summary>How many stacked frames the row's art divides into (1 for a plain picture).</summary>
    public int Frames { get; }

    /// <summary>Every field's resolved value, by name.</summary>
    public IReadOnlyDictionary<string, string> Fields => _fields;

    /// <summary>The field's resolved value, or "" when the row lacks it.</summary>
    public string Field(string name) => _fields.TryGetValue(name, out var v) ? v : string.Empty;

    /// <summary>The raw <c>&lt;MACRO&gt;</c> token a field was authored with, or null when the
    /// value was written out literally.</summary>
    public string? Authored(string name) => _authored.TryGetValue(name, out var v) ? v : null;

    /// <summary>An <c>int</c>-kind field as a number. False for a missing field, an empty value,
    /// a value that is not a number or a field of another kind.</summary>
    public bool TryInt(string name, out int value)
    {
        value = 0;
        return KindIs(name, "int")
            && int.TryParse(Field(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>An <c>int</c>-kind field, or <paramref name="fallback"/> when it does not read.</summary>
    public int Int(string name, int fallback = 0) => TryInt(name, out int v) ? v : fallback;

    /// <summary>A <c>bool</c>-kind field: <c>1</c> is true, anything else false. False for a
    /// field of another kind.</summary>
    public bool Bool(string name) => KindIs(name, "bool") && Field(name) == "1";

    /// <summary>A <c>color</c>-kind field as ARGB. False for a missing or unparsable value or a
    /// field of another kind.</summary>
    public bool TryColor(string name, out MenuLayoutColor color)
    {
        color = default;
        return KindIs(name, "color") && MenuLayoutColor.TryParse(Field(name), out color);
    }

    private bool KindIs(string name, string kind) => Type?.KindOf(name) == kind;
}

/// <summary>One macro definition, file-wide or per screen.</summary>
public sealed record MenuLayoutMacro(string Name, string Value);

/// <summary>One screen: its section, the script it binds to and its widget rows by key.</summary>
public sealed class MenuLayoutScreen
{
    private readonly Dictionary<string, MenuLayoutWidget> _byKey = new(StringComparer.OrdinalIgnoreCase);

    internal MenuLayoutScreen(string section, string script, IReadOnlyList<MenuLayoutMacro> macros,
        IReadOnlyList<MenuLayoutWidget> widgets, IReadOnlyList<string> keysWithoutLayoutRow)
    {
        Section = section;
        Script = script;
        Macros = macros;
        Widgets = widgets;
        KeysWithoutLayoutRow = keysWithoutLayoutRow;
        foreach (var widget in widgets)
        {
            _byKey[widget.Key] = widget;
        }
    }

    /// <summary>The section name, which is the script name (<c>MainMenu</c>).</summary>
    public string Section { get; }

    /// <summary>The archive path of the script of the same name, or "" when there is none.</summary>
    public string Script { get; }

    /// <summary>The section's own macros, in file order.</summary>
    public IReadOnlyList<MenuLayoutMacro> Macros { get; }

    /// <summary>The widget rows, in file order.</summary>
    public IReadOnlyList<MenuLayoutWidget> Widgets { get; }

    /// <summary>Keys the script creates that have no layout row, so the widget exists with no
    /// authored geometry.</summary>
    public IReadOnlyList<string> KeysWithoutLayoutRow { get; }

    /// <summary>The widget under a key, matched case-insensitively since scripts lowercase the
    /// keys the layout writes in capitals; null when the section has no such row.</summary>
    public MenuLayoutWidget? Widget(string key) => _byKey.TryGetValue(key, out var w) ? w : null;
}

/// <summary>One navigation edge the layout states: a button on one screen naming another.</summary>
public sealed record MenuLayoutEdge(string From, string Widget, string To, string Priority, bool EndScript);

/// <summary>One asset a script names outside the layout: a complete path or a fragment a runtime
/// name is assembled from, and whether the extraction has it.</summary>
public sealed record MenuLayoutAsset(string Path, string Kind, string Script, bool Present);

/// <summary>
/// The runtime reader of <c>extracted/rof/menu_layout.json</c>, the decoded menu layout the
/// extractor emits: screens, widgets, navigation, script-named assets and the type table that
/// says how each field reads. It parses the artifact and nothing else; the format analysis
/// stays in the extractor, and a tree extracted before the decode existed is caught by the
/// extraction stamp rather than read as an empty layout.
/// </summary>
public sealed class MenuLayout
{
    private readonly Dictionary<string, MenuLayoutScreen> _screens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _globals = new(StringComparer.Ordinal);

    private MenuLayout(int schema, IReadOnlyList<MenuLayoutWidgetType> widgetTypes,
        IReadOnlyList<MenuLayoutMacro> globals, IReadOnlyList<MenuLayoutScreen> screens,
        IReadOnlyList<MenuLayoutEdge> navigation, IReadOnlyList<MenuLayoutAsset> externalAssets,
        IReadOnlyList<string> missingArt)
    {
        Schema = schema;
        WidgetTypes = widgetTypes;
        Globals = globals;
        Screens = screens;
        Navigation = navigation;
        ExternalAssets = externalAssets;
        MissingArt = missingArt;
        foreach (var screen in screens)
        {
            _screens[screen.Section] = screen;
        }

        foreach (var macro in globals)
        {
            _globals[macro.Name] = macro.Value;
        }
    }

    /// <summary>The artifact's own schema number.</summary>
    public int Schema { get; }

    /// <summary>The type table: one entry per widget type the artifact declares.</summary>
    public IReadOnlyList<MenuLayoutWidgetType> WidgetTypes { get; }

    /// <summary>The file-wide macros, in file order.</summary>
    public IReadOnlyList<MenuLayoutMacro> Globals { get; }

    /// <summary>Every screen, in file order.</summary>
    public IReadOnlyList<MenuLayoutScreen> Screens { get; }

    /// <summary>The navigation edges the layout states, flattened.</summary>
    public IReadOnlyList<MenuLayoutEdge> Navigation { get; }

    /// <summary>The assets the scripts name outside the layout.</summary>
    public IReadOnlyList<MenuLayoutAsset> ExternalAssets { get; }

    /// <summary>Art the layout names that the extraction does not carry.</summary>
    public IReadOnlyList<string> MissingArt { get; }

    /// <summary>Where the artifact sits under a data root.</summary>
    public static string PathUnder(string dataRoot) =>
        Path.Combine(dataRoot, "extracted", "rof", "menu_layout.json");

    /// <summary>Reads the artifact at <paramref name="path"/>. Null, with a reason, when the file
    /// is absent or does not parse; a caller falls back on the reason rather than on an empty
    /// layout.</summary>
    public static MenuLayout? TryLoad(string path, out string? reason)
    {
        reason = null;
        if (!File.Exists(path))
        {
            reason = $"no decoded menu layout at {path}";
            return null;
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException or FormatException)
        {
            reason = $"menu layout unreadable at {path}: {e.GetType().Name}: {e.Message}";
            return null;
        }
    }

    /// <summary>Parses the artifact's JSON text. Throws <see cref="JsonException"/> on malformed
    /// JSON and <see cref="InvalidDataException"/> when the document is not the artifact.</summary>
    public static MenuLayout Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("screens", out _))
        {
            throw new InvalidDataException("not a menu layout artifact: no 'screens' array");
        }

        int schema = root.TryGetProperty("schema", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
        var types = new List<MenuLayoutWidgetType>();
        var typesByCode = new Dictionary<string, MenuLayoutWidgetType>(StringComparer.Ordinal);
        foreach (var t in Array(root, "widgetTypes"))
        {
            var fields = new List<MenuLayoutField>();
            foreach (var f in Array(t, "fields"))
            {
                fields.Add(new MenuLayoutField(Str(f, "name"), Str(f, "kind"), Flag(f, "optional")));
            }

            var type = new MenuLayoutWidgetType(Str(t, "type"), Str(t, "widget"), Str(t, "scriptClass"), fields);
            types.Add(type);
            typesByCode[type.Type] = type;
        }

        var screens = new List<MenuLayoutScreen>();
        foreach (var sc in Array(root, "screens"))
        {
            var widgets = new List<MenuLayoutWidget>();
            foreach (var w in Array(sc, "widgets"))
            {
                string code = Str(w, "type");
                typesByCode.TryGetValue(code, out var type);
                widgets.Add(new MenuLayoutWidget(
                    Str(w, "key"), type, code, Str(w, "widget"),
                    Map(w, "fields"), Map(w, "authored"), Strings(w, "art"),
                    OptStr(w, "resIdSymbol"), OptInt(w, "resId"), OptStr(w, "text"),
                    OptStr(w, "textSource") ?? "none", OptStr(w, "navigateTo"),
                    Math.Max(1, OptInt(w, "frames") ?? 1)));
            }

            screens.Add(new MenuLayoutScreen(Str(sc, "section"), Str(sc, "script"),
                Macros(sc, "macros"), widgets, Strings(sc, "keysWithoutLayoutRow")));
        }

        var navigation = new List<MenuLayoutEdge>();
        foreach (var e in Array(root, "navigation"))
        {
            navigation.Add(new MenuLayoutEdge(Str(e, "from"), Str(e, "widget"), Str(e, "to"),
                Str(e, "priority"), Str(e, "endScript") == "1"));
        }

        var assets = new List<MenuLayoutAsset>();
        foreach (var a in Array(root, "externalAssets"))
        {
            assets.Add(new MenuLayoutAsset(Str(a, "path"), Str(a, "kind"), Str(a, "script"), Flag(a, "present")));
        }

        return new MenuLayout(schema, types, Macros(root, "globals"), screens, navigation, assets,
            Strings(root, "missingArt"));
    }

    /// <summary>The screen under a section name, or null.</summary>
    public MenuLayoutScreen? Screen(string section) => _screens.TryGetValue(section, out var s) ? s : null;

    /// <summary>A file-wide macro's value, or null when <c>[GLOBALVARS]</c> lacks it.</summary>
    public string? Global(string name) => _globals.TryGetValue(name, out var v) ? v : null;

    /// <summary>A file-wide colour macro (<c>ACTIVE</c>, <c>ROLLOVER</c>, ...), or null.</summary>
    public MenuLayoutColor? GlobalColor(string name) =>
        MenuLayoutColor.TryParse(Global(name), out var c) ? c : null;

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static string Str(JsonElement e, string name) => OptStr(e, name) ?? string.Empty;

    private static string? OptStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? OptInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True
            || (v.ValueKind == JsonValueKind.String && v.GetString() == "1"));

    private static IReadOnlyList<string> Strings(JsonElement e, string name)
    {
        var list = new List<string>();
        foreach (var item in Array(e, name))
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }

    private static IReadOnlyDictionary<string, string> Map(JsonElement e, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (e.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in obj.EnumerateObject())
            {
                map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.ToString();
            }
        }

        return map;
    }

    private static IReadOnlyList<MenuLayoutMacro> Macros(JsonElement e, string name)
    {
        var list = new List<MenuLayoutMacro>();
        foreach (var m in Array(e, name))
        {
            list.Add(new MenuLayoutMacro(Str(m, "name"), Str(m, "value")));
        }

        return list;
    }
}
