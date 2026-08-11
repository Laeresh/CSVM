using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// The original's global surface-name registry: <c>id → name</c>, so a caller can build
/// <c>"player_crash_" + name</c> or <c>"touchdown_" + name</c> and resolve the def the original
/// resolves for a struck material's numeric surface id (<c>GameZMaterial.SoilId</c>).
///
/// <para><b>The index here is Crimson Skies' id, not mech3ax's <c>Soil</c> enum ordinal</b> — the
/// two disagree at slot 13: this repo's <c>dirt</c> is mech3ax's label <c>NoSlip</c>, while
/// mech3ax's own enum member named <c>Dirt</c> is a MechWarrior 3 leftover at ordinal 6, which
/// Crimson Skies actually uses for <c>player</c>. <c>GameZ.SoilLabelToId</c> already converts
/// mech3ax's string label to this table's id before it ever reaches <c>SoilId</c>, so
/// <c>NameForId</c> below never sees a raw mech3ax ordinal — but do not shortcut that conversion
/// by indexing <see cref="Names"/> with a mech3ax <c>Soil</c> enum value directly.</para>
///
/// <para>Ids 0–5 are compiled into <c>crimson.exe</c> (registry count at <c>0x00637b10</c>,
/// <c>char*</c> array at <c>0x00637b14</c>). Ids 6–13 are appended at load time by
/// <c>FUN_0055b7b0</c>, whose sole caller is the script command <c>LoadSoils &lt;filename&gt;</c>
/// (<c>FUN_005b80a0</c>, call at <c>0x005ba4ae</c>); the filename is script data, not an exe
/// literal, so the list is read from the shipped install: <c>ZBD/zrdr.zbd</c> at offset
/// <c>0xe631c</c>, eight contiguous names in file order — <c>player enemy airstrip opensesame
/// death buildings dzone dirt</c>. <c>analysis/surface-classification/soils_list.py</c> reproduces
/// the derivation from the raw archive.</para>
///
/// <para><b>Baked in, not read at load.</b> Unlike this repo's other Mech3 readers, this list has
/// no structured JSON entry point in mech3ax's zrdr extraction — it was located by an anchor byte
/// scan against the raw <c>zrdr.zbd</c> blob, not through the reader-family conventions
/// (<see cref="Zrdr"/>) every other config list here goes through. Re-running that scan at load
/// would duplicate the reverse-engineering script inside the engine for a table this small and
/// fixed; baking it in with this provenance is cheaper and no less faithful — the six compiled-in
/// names already have to be hardcoded the same way, since <c>crimson.exe</c> itself is never read
/// at runtime.</para>
///
/// <para><b>Every slot is kept, including the ones no shipped material carries</b>
/// (<c>seafloor</c>/<c>quicksand</c>/<c>lava</c> at 2/3/4, <c>player</c>/<c>enemy</c>/
/// <c>opensesame</c>/<c>death</c> at 6/7/9/10) — dropping any of them would renumber every id below
/// it and silently corrupt the whole table.</para>
///
/// <para><b>The append rule's digit-dedup is not implemented.</b> The original appends a soils-list
/// name only if it is not already present, matching case-insensitively over the registry name's
/// length and accepting a trailing NUL <b>or digit</b> — so a hypothetical <c>water2</c> would
/// collapse onto <c>water</c> (<c>0x0055b874</c>). No shipped name hits that case (all fourteen are
/// pairwise distinct under the rule), so this table is simply the fourteen names in slot order with
/// no dedup pass applied.</para>
/// </summary>
public static class SurfaceRegistry
{
    /// <summary>Registry name for every id, in slot order. Index == surface id.</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "default", "water", "seafloor", "quicksand", "lava", "fire", // 0-5: compiled into crimson.exe
        "player", "enemy", "airstrip", "opensesame", "death", "buildings", "dzone", "dirt", // 6-13: ZBD/zrdr.zbd @ 0xe631c
    };

    /// <summary>The registry name for <paramref name="id"/>, or null if <paramref name="id"/> is
    /// negative or at/beyond <see cref="Names"/>'s length — the same bounds test
    /// <c>FUN_0048b920</c> makes before indexing the crash-def vector.</summary>
    public static string? NameForId(int id) =>
        id >= 0 && id < Names.Count ? Names[id] : null;
}
