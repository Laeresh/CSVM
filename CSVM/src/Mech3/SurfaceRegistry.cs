using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// The original's global surface-name registry: <c>id → name</c>, for
/// <c>"player_crash_" + name</c> / <c>"touchdown_" + name</c>, keyed by a struck material's
/// <c>GameZMaterial.SoilId</c>. Decode: docs/formats/gamez.md.
/// ⚠ This is Crimson Skies' own id space, not mech3ax's <c>Soil</c> enum ordinal.
/// <c>GameZ.SoilLabelToId</c> converts before the id reaches <c>SoilId</c>; never index
/// <see cref="Names"/> with a raw mech3ax <c>Soil</c> value.
/// ⚠ Every slot is kept, including ones no shipped material carries, dropping one renumbers
/// every id below it. Baked in rather than read at load: no JSON entry point exists for this
/// table, and the six compiled-in names need hardcoding regardless.
/// </summary>
public static class SurfaceRegistry
{
    /// <summary>The ids this codebase names outright, so a lookup reads as the registry slot it is
    /// rather than as a bare number. Only the slots something branches on are named here; every
    /// other id is reached through <see cref="Names"/> like any data-driven value.</summary>
    public const int Default = 0;
    public const int Water = 1;
    public const int Player = 6;
    public const int Enemy = 7;
    public const int Buildings = 11;

    /// <summary>Registry name for every id, in slot order. Index == surface id.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "default", "water", "seafloor", "quicksand", "lava", "fire", // 0-5: compiled into crimson.exe
        "player", "enemy", "airstrip", "opensesame", "death", "buildings", "dzone", "dirt", // 6-13: ZBD/zrdr.zbd @ 0xe631c
    };

    private static readonly Dictionary<string, int> Ids = BuildIds();

    /// <summary>The registry name for <paramref name="id"/>, or null if <paramref name="id"/> is
    /// negative or at/beyond <see cref="Names"/>'s length, the same bounds test
    /// <c>FUN_0048b920</c> makes before indexing the crash-def vector.</summary>
    public static string? NameForId(int id) =>
        id >= 0 && id < Names.Count ? Names[id] : null;

    /// <summary>The id a registry name occupies, or null when the name is not in the registry,
    /// the parse-time direction, which is how the original matches an authored block name against
    /// the registry before writing into its id-indexed array (<c>FUN_005ad630</c>
    /// <c>0x005ae1ea</c>–<c>0x005ae24e</c>). A name that misses is discarded, not defaulted.
    /// Case-insensitive, matching the original's name compare; every shipped name is lowercase.</summary>
    public static int? IdForName(string? name) =>
        name != null && Ids.TryGetValue(name, out var id) ? id : null;

    private static Dictionary<string, int> BuildIds()
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int id = 0; id < Names.Count; id++)
        {
            ids[Names[id]] = id;
        }
        return ids;
    }
}
