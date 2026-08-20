using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// Which <c>vehicle.json</c> def a militia's aircraft is: the Black Hat Warhawk is
/// <c>bhatwarhawk</c>. An AI aircraft spawned under one of these flies that def's own armament,
/// damage model, livery and pilot (<c>PlaneStats.LoadForAi</c>) instead of the airframe's base def.
/// The pairing is the game's own: each def's <c>title</c> resolves through the message table to
/// exactly "&lt;militia&gt; &lt;aircraft&gt;" (<c>MSG_VEH_STRUST_HELLHOUND</c> is "Sacred Trust
/// Hellhound"), which is the string the wave editor writes. Nothing here parses def names.
/// </summary>
public static class MilitiaDefs
{
    // The launch menu says "Hollywood Knight", the message table "Hollywood Knights", and they mean
    // the same militia. Matching is loose enough to cover it rather than hard-coding the pair.
    private static readonly char[] Space = { ' ' };

    /// <summary>Every militia aircraft the install names, keyed by its display string. The FIRST def
    /// to claim a name wins: the <c>_2</c>/<c>_3</c>/<c>_5</c> chapter duplicates repeat their base
    /// def's title verbatim. Base and player defs are in here too, under a bare aircraft name
    /// ("Fury"), where no militia string can reach them.</summary>
    public static Dictionary<string, string> ByDisplayName(string zrdrPath, Messages messages)
    {
        var root = Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] as List<object?>
            ?? throw new InvalidOperationException("vehicle.json: unexpected root shape");

        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is not string def || root[i + 1] is not List<object?> props)
                continue;
            var d = ZrdrDict.FromAlternating(props);
            if (d.Str("title") is not { } title)
                continue;
            string display = messages.Get(title);
            if (display.Length > 0 && !byName.ContainsKey(display))
                byName[display] = def;
        }
        return byName;
    }

    /// <summary>The def behind a wave's authored <c>enemy_name</c>, which the wizard writes as
    /// "&lt;militia&gt; &lt;aircraft&gt;". Null when no def carries that name: a shipped
    /// <c>ia.json</c>'s <c>MSG_*</c> key, the player militia (which has no AI defs), or a menu pair
    /// the install ships no def for. The aircraft's base def is flown then.</summary>
    public static string? ForWave(IReadOnlyDictionary<string, string> byDisplayName, string enemyName)
    {
        if (byDisplayName.TryGetValue(enemyName, out var def))
            return def;
        // Singular/plural on the militia half only ("Hollywood Knight" against "Hollywood Knights"),
        // never on the aircraft half, which both sides spell the same way.
        foreach (var pair in byDisplayName)
            if (SameAircraft(pair.Key, enemyName) && SameMilitia(pair.Key, enemyName))
                return pair.Value;
        return null;
    }

    private static bool SameAircraft(string a, string b)
    {
        int i = a.LastIndexOfAny(Space), j = b.LastIndexOfAny(Space);
        return i > 0 && j > 0
            && string.Equals(a[(i + 1)..], b[(j + 1)..], StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameMilitia(string a, string b)
    {
        int i = a.LastIndexOfAny(Space), j = b.LastIndexOfAny(Space);
        if (i <= 0 || j <= 0)
            return false;
        string x = a[..i].TrimEnd('s', 'S'), y = b[..j].TrimEnd('s', 'S');
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
