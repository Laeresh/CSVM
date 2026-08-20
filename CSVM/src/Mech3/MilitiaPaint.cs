using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// A militia's paint pattern, which is all a militia decides on the Instant Action path: the
/// original builds every wingman, ace and wave member from the plain AI def of its aircraft and
/// writes the setup screen's pattern, decals and colours over it, so no militia ever selects a
/// vehicle def there (docs/formats/instant-action.md). The pattern is read off whichever def of that
/// militia names one, since every def of a militia wears the same pattern.
/// </summary>
public static class MilitiaPaint
{
    private static readonly char[] Space = { ' ' };

    /// <summary>Each militia's pattern, keyed by the militia half of a vehicle's display name
    /// ("Sacred Trust Hellhound" keys "Sacred Trust" to <c>sactrust</c>). A militia/aircraft pair the
    /// install ships no def for is reached all the same, which is what paints a Sacred Trust
    /// Warhawk.</summary>
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
    /// none: a shipped <c>ia.json</c>'s <c>MSG_*</c> key, or Broadway Bomber, whose masks ship under
    /// no def at all and so carry no colours.</summary>
    public static string? PatternForWave(IReadOnlyDictionary<string, string> patternByMilitia,
        string enemyName)
    {
        int cut = enemyName.LastIndexOfAny(Space);
        return cut > 0 && patternByMilitia.TryGetValue(Key(enemyName[..cut]), out var pattern)
            ? pattern : null;
    }

    // The militia half, normalised: the menu says "Hollywood Knight" and the message table
    // "Hollywood Knights", so the trailing plural comes off both before either is compared.
    private static string Key(string militia) => militia.TrimEnd('s', 'S');
}
