using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSVM.Session.World;

/// <summary>
/// The shipped list of moonlit missions, <c>CSVM/data/moonlit_missions.json</c>: worlds lit as night
/// under Enhanced though their authored light is day-level. Each entry is one world spelled
/// <c>&lt;chapter&gt;/&lt;mission folder&gt;</c>. <c>WeatherRig</c> reads it once per world and marks a
/// listed world's zones, which <c>WeatherRig.IsNightZone</c> then counts as night. A malformed entry
/// is a warning, never fatal. Shipped data, so a network peer and a <c>--det</c> capture agree.
/// </summary>
public sealed class MoonlitMissions
{
    /// <summary>The committed file, read through Godot in an exported build, where it lives in the pck.</summary>
    public const string DefaultPath = "res://data/moonlit_missions.json";

    /// <summary>The list with no world in it, what a missing or unreadable file gives.</summary>
    public static readonly MoonlitMissions None = new();

    private const string ListKey = "missions";

    private readonly HashSet<string> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = new();

    /// <summary>What the file got wrong, one line each.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>The listed worlds, each as <see cref="Key"/> spells it.</summary>
    public IReadOnlyCollection<string> Worlds => _worlds;

    /// <summary>One world's entry: chapter and mission folder joined by a slash.</summary>
    public static string Key(string chapter, string mission) => $"{chapter}/{mission}";

    /// <summary>Reads the shipped list, logging each warning. A missing file lists nothing.</summary>
    public static MoonlitMissions Load()
    {
        if (!Godot.FileAccess.FileExists(DefaultPath))
        {
            Utils.Log.Warn("world", $"moonlit missions: {DefaultPath} not found, no world is lit as night by the list");
            return None;
        }

        var list = Parse(Encoding.UTF8.GetString(Godot.FileAccess.GetFileAsBytes(DefaultPath)));
        foreach (var w in list.Warnings)
            Utils.Log.Warn("world", $"moonlit missions: {w}");
        return list;
    }

    /// <summary>Parses the file's text with no file IO and no engine call, so the rules unit-test.</summary>
    public static MoonlitMissions Parse(string json)
    {
        var list = new MoonlitMissions();
        JsonNode? root;
        try
        {
            root = Mech3.VegetationSidecar.ParseWhole(json);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            list._warnings.Add($"not readable JSON ({ex.Message}), no world is listed");
            return list;
        }

        if (root is not JsonObject top || top[ListKey] is not JsonArray entries)
        {
            list._warnings.Add($"the file is not an object holding a '{ListKey}' array, no world is listed");
            return list;
        }

        foreach (var entry in entries)
        {
            string? text = entry is JsonValue v && v.TryGetValue(out string? s) ? s : null;
            string[] parts = text?.Split('/') ?? Array.Empty<string>();
            if (parts.Length != 2 || parts[0].Trim().Length == 0 || parts[1].Trim().Length == 0)
            {
                list._warnings.Add($"'{entry?.ToJsonString()}' is not '<chapter>/<mission folder>', ignored");
                continue;
            }
            list._worlds.Add(Key(parts[0].Trim(), parts[1].Trim()));
        }

        return list;
    }

    /// <summary>Whether the file lists the world flown in <paramref name="chapter"/>'s
    /// <paramref name="mission"/> folder. Case does not matter.</summary>
    public bool Lists(string chapter, string mission) => _worlds.Contains(Key(chapter, mission));
}
