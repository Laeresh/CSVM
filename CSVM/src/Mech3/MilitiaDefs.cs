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

    /// <summary>Every militia aircraft the install names, keyed by its display string. Several defs
    /// can claim one name: <c>bhatbrigand</c>, <c>bhatbrigand_2</c> and <c>bhatbrigand_5</c> are all
    /// "Black Hat Brigand", and they differ in pilot ratings and sometimes in weapon, so the
    /// UNSUFFIXED def wins and the <c>_N</c> variants are left to the missions that name them
    /// outright. Base and player defs are in here too, under a bare aircraft name ("Fury"), where no
    /// militia string can reach them.</summary>
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
            if (display.Length == 0)
                continue;
            if (!byName.TryGetValue(display, out var held) || (IsVariant(held) && !IsVariant(def)))
                byName[display] = def;
        }
        return byName;
    }

    /// <summary>Each militia's paint pattern, keyed by the militia half of a display name. Read off
    /// whichever of its defs names one, since every def of a militia wears the same pattern. This is
    /// what paints a wave member: the original takes a wave's livery from the setup screen, so a
    /// militia/aircraft pair with no def of its own (Sacred Trust's Warhawk) is painted all the
    /// same, and only its armament, damage model and pilot fall back to the base def.</summary>
    public static Dictionary<string, string> PatternByMilitia(string zrdrPath, Messages messages)
    {
        var root = Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] as List<object?>
            ?? throw new InvalidOperationException("vehicle.json: unexpected root shape");

        var byMilitia = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is not string || root[i + 1] is not List<object?> props)
                continue;
            var d = ZrdrDict.FromAlternating(props);
            if (d.Str("title") is not { } title || d.Str("paint_pattern") is not { } pattern)
                continue;
            string display = messages.Get(title);
            int cut = display.LastIndexOfAny(Space);
            if (cut <= 0)
                continue; // a bare aircraft name: a base or player def, no militia to key on
            byMilitia.TryAdd(Key(display[..cut]), pattern);
        }
        return byMilitia;
    }

    /// <summary>The pattern a wave's <c>enemy_name</c> is painted in, or null when its militia names
    /// none (a shipped <c>MSG_*</c> key, or Broadway Bomber, whose masks ship under no def at
    /// all).</summary>
    public static string? PatternForWave(IReadOnlyDictionary<string, string> patternByMilitia,
        string enemyName)
    {
        int cut = enemyName.LastIndexOfAny(Space);
        return cut > 0 && patternByMilitia.TryGetValue(Key(enemyName[..cut]), out var pattern)
            ? pattern : null;
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

    // The militia half, normalised: the menu says "Hollywood Knight" and the message table
    // "Hollywood Knights", so the trailing plural comes off both before either is compared.
    private static string Key(string militia) => militia.TrimEnd('s', 'S');

    // A "<name>_<digits>" variant of another def, e.g. bhatbrigand_5. Missions name these directly;
    // nothing resolves one from a display name, since every variant shares the plain def's title.
    private static bool IsVariant(string def)
    {
        int i = def.LastIndexOf('_');
        if (i <= 0 || i == def.Length - 1)
            return false;
        for (int k = i + 1; k < def.Length; k++)
            if (!char.IsDigit(def[k]))
                return false;
        return true;
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
