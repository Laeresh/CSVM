using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>One sound definition from sounds.json (name → WAV + playback flags).</summary>
public sealed class SoundDef
{
    public string Name = "";
    public string WavName = "";
    public bool Looped;          // LOOPED flag: plays as a forward loop
    public bool Is3D;            // 3D flag: positional
    public bool Frequency;       // FREQUENCY flag: the engine pitch-shifts this sound
    public float RangeMin = 130f, RangeMax = 1020f; // RANGE [full-volume dist, audible dist], m
    public float Volume = 1f;    // VOLUME base gain
}

/// <summary>
/// Parser for the zrdr sounds.json reader file: the SETS block maps snd_* names to
/// WAV files with playback flags. Entry shape: [snd_name, wav, FLAG..., KEY, [vals], ...]
/// where LOOPED/3D/FREQUENCY/SFX/PURGEABLE/OPTIONAL are bare flags and RANGE/VOLUME/QUEUE
/// carry a value list.
/// </summary>
public static class SoundDefs
{
    /// <summary>Loads all SETS entries from sounds.json in a zrdr extraction (zip or dir).</summary>
    public static Dictionary<string, SoundDef> Load(string zrdrPath)
    {
        var defs = new Dictionary<string, SoundDef>(StringComparer.OrdinalIgnoreCase);
        var root = Zrdr.LoadFile(zrdrPath, "sounds.json")[0] as List<object?>
            ?? throw new InvalidOperationException("sounds.json: unexpected root shape");
        var sets = ZrdrDict.FromAlternating(root).List("SETS")
            ?? throw new InvalidOperationException("sounds.json: no SETS block");

        // SETS alternates: set-name, [entry, entry, ...]
        for (int i = 0; i + 1 < sets.Count; i += 2)
        {
            if (sets[i + 1] is not List<object?> entries)
                continue;
            foreach (var e in entries)
                if (e is List<object?> { Count: >= 2 } entry
                    && entry[0] is string name && entry[1] is string wav)
                {
                    var def = new SoundDef { Name = name, WavName = wav };
                    for (int j = 2; j < entry.Count; j++)
                    {
                        if (entry[j] is not string flag)
                            continue; // value list — consumed by the key before it
                        List<object?>? Val() => j + 1 < entry.Count ? entry[j + 1] as List<object?> : null;
                        switch (flag)
                        {
                            case "LOOPED": def.Looped = true; break;
                            case "3D": def.Is3D = true; break;
                            case "FREQUENCY": def.Frequency = true; break;
                            case "RANGE" when Val() is { Count: >= 2 } r
                                && r[0] is float min && r[1] is float max:
                                def.RangeMin = min;
                                def.RangeMax = max;
                                break;
                            case "VOLUME" when Val() is { Count: >= 1 } v && v[0] is float vol:
                                def.Volume = vol;
                                break;
                        }
                    }
                    defs[name] = def;
                }
        }
        return defs;
    }
}
