using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// Which <c>vehicle.json</c> def a militia's aircraft is: the Black Hat Warhawk is
/// <c>bhatwarhawk</c>. An AI aircraft spawned under one of these flies that def's own armament,
/// damage model, livery and pilot (<c>PlaneStats.LoadForAi</c>) instead of the airframe's base def.
/// ⚠ The table is read off each def's own authored <c>paint_pattern</c>, never off its name: the
/// prefixes are not a system (<c>britpeace</c>, <c>secgyro</c>), and <c>stihellhound</c> is Sacred
/// Trust's only because it wears <c>sactrust</c>. Which pairs have no def, and why a shipped
/// <c>ia.json</c> wave resolves none, is in docs/formats/instant-action.md.
/// </summary>
public static class MilitiaDefs
{
    // Keyed "<militia>|<aircraft>", both in the launch menu's own display vocabulary.
    private static readonly Dictionary<string, string> Defs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Black Hat|Warhawk"] = "bhatwarhawk",
        ["Black Hat|Brigand"] = "bhatbrigand",
        ["Black Hat|Autogyro"] = "bhatgyro",
        ["Black Swan|Fury"] = "bsfury",
        ["Blake Aviation|Bloodhawk"] = "blakebloodhawk",
        ["Blake Aviation|Peacemaker"] = "blakepeace",
        ["British|Peacemaker"] = "britpeace",
        ["British|Balmoral"] = "britbalmoral",
        ["Hollywood Knight|Firebrand"] = "hkfirebrand",
        ["Hughes Aviation|Bloodhawk"] = "habloodhawk",
        ["Hughes Aviation|Kestrel"] = "hakestrel",
        ["Hughes Aviation|Fury"] = "hafury",
        ["Medusa|Kestrel"] = "medkestrel",
        ["Medusa|Brigand"] = "medbrigand",
        ["Russian|Devastator"] = "rusdevastator",
        ["Sacred Trust|Hellhound"] = "stihellhound",
        ["German|Hellhound"] = "germanhellhound",
        ["Studio Security|Fury"] = "secfury",
        ["Studio Security|Autogyro"] = "secgyro",
    };

    // Longest first, so "Black Hat" cannot claim a name a longer militia also prefixes.
    private static readonly string[] MilitiaNames =
    {
        "Hollywood Knight", "Studio Security", "Blake Aviation", "Hughes Aviation",
        "Broadway Bomber", "Sacred Trust", "Fortune Hunter", "Black Swan", "Black Hat",
        "Russian", "British", "German", "Medusa",
    };

    /// <summary>The def for a militia and one of its aircraft, both as the launch menu names them;
    /// null when the pair has no def of its own.</summary>
    public static string? For(string militia, string aircraft) =>
        Defs.TryGetValue($"{militia}|{aircraft}", out var def) ? def : null;

    /// <summary>The def behind a wave's authored <c>enemy_name</c>, which the wizard writes as
    /// "&lt;militia&gt; &lt;aircraft&gt;". Null when the name names no militia (a shipped
    /// <c>MSG_*</c> key, a hand-authored label) or when that militia's aircraft has no def, and the
    /// aircraft's base def is flown instead.</summary>
    public static string? ForWave(string enemyName, string aircraft)
    {
        foreach (string militia in MilitiaNames)
            if (enemyName.StartsWith(militia, StringComparison.OrdinalIgnoreCase))
                return For(militia, aircraft);
        return null;
    }
}
