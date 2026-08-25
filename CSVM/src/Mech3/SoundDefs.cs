using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

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
        static float? Num(object? v) => v switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => null,
        };

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
                            case "QUEUE" when Val() is { Count: >= 1 } q && Num(q[0]) is { } secs:
                                def.QueueSeconds = secs;
                                break;
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

    /// <summary>
    /// Loads the <c>SOUND_GROUPS</c> block: the weighted random sound groups a one-shot SOUND event
    /// resolves through. See <c>docs/formats/sounds.md</c> for the entry shape. A dialogue-chain
    /// member is kept as <see cref="SoundGroup.Chains"/> rather than a weighted member. A group
    /// with neither members nor chains is not registered.
    /// </summary>
    public static Dictionary<string, SoundGroup> LoadGroups(string zrdrPath)
    {
        var groups = new Dictionary<string, SoundGroup>(StringComparer.OrdinalIgnoreCase);
        var root = Zrdr.LoadFile(zrdrPath, "sounds.json")[0] as List<object?>
            ?? throw new InvalidOperationException("sounds.json: unexpected root shape");
        var list = ZrdrDict.FromAlternating(root).List("SOUND_GROUPS");
        if (list == null)
        {
            return groups;
        }

        static float? AsFloat(object? v) => v switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            _ => null,
        };

        foreach (var e in list)
        {
            if (e is not List<object?> { Count: >= 2 } entry || entry[0] is not string name)
            {
                continue;
            }
            var group = new SoundGroup { Name = name };
            for (int j = 1; j < entry.Count; j++)
            {
                if (entry[j] is string token)
                {
                    // "MUSIC" is a category marker (its members still follow); "DYNAMIC_WEIGHTS" is
                    // followed by the bare recency factor.
                    if (token == "DYNAMIC_WEIGHTS" && j + 1 < entry.Count && AsFloat(entry[j + 1]) is { } factor)
                    {
                        group.RecencyFactor = factor;
                        j++;
                    }
                    continue;
                }
                if (entry[j] is List<object?> { Count: >= 1 } member && member[0] is string memberName)
                {
                    if (member.Count == 1)
                    {
                        group.Members.Add((memberName, 1f));
                    }
                    else if (member.Count >= 3 && member[1] as string == "WEIGHT" && AsFloat(member[2]) is { } w)
                    {
                        group.Members.Add((memberName, w));
                    }
                    else if (member[1] is List<object?>)
                    {
                        // A dialogue chain: the first line bare, each further line in its own list.
                        var chain = new List<string> { memberName };
                        for (int k = 1; k < member.Count; k++)
                        {
                            if (member[k] is List<object?> { Count: >= 1 } line && line[0] is string lineName)
                            {
                                chain.Add(lineName);
                            }
                        }
                        group.Chains.Add(chain);
                    }
                }
            }
            if (group.Members.Count > 0 || group.Chains.Count > 0)
            {
                groups[name] = group;
            }
        }
        return groups;
    }
}

/// <summary>One sound definition from sounds.json (name → WAV + playback flags). The data sorts
/// every definition into one playback class and the flags are the sorter: <c>3D</c> (always with
/// <c>RANGE</c>) is the positional opt-in, <c>QUEUE</c> marks a queued radio line, <c>MUSIC</c>
/// the streaming channel, and a definition carrying none of them is cockpit or UI audio. See
/// docs/formats/sounds.md.</summary>
public sealed class SoundDef
{
    public string Name = "";
    public string WavName = "";
    public bool Looped;          // LOOPED flag: plays as a forward loop
    public bool Is3D;            // 3D flag: positional
    public bool Frequency;       // FREQUENCY flag: the engine pitch-shifts this sound
    public float RangeMin = 130f, RangeMax = 1020f; // RANGE [full-volume dist, audible dist], m
    public float Volume = 1f;    // VOLUME base gain

    /// <summary>QUEUE's value: how long this line may wait in the mission radio queue before it is
    /// dropped as stale, in seconds. Null when the definition is not a queued line. No definition
    /// in the shipped data carries both this and <see cref="Is3D"/>.</summary>
    public float? QueueSeconds;

    /// <summary>Whether this definition is a radio line: it plays on the non-positional mission
    /// radio queue rather than from a point in the world.</summary>
    public bool Queued => QueueSeconds != null;
}

/// <summary>
/// One <c>SOUND_GROUPS</c> entry: a set of member sounds an event picks one of at random when it
/// names the group instead of a plain <c>snd_*</c> definition. The combat/destruction one-shots go
/// through these — <c>air_mixed_exp_sg</c>, <c>ground_mixed_exp_sg</c>, <c>plane_destroy_sg</c>,
/// <c>bullet_hit_sg</c>, <c>bullet_warning_sg</c>, <c>window_hit_sg</c>.
///
/// <para><c>DYNAMIC_WEIGHTS</c> groups carry a recency factor (0.5 everywhere it appears): the
/// member returned last has its weight scaled by it on the next pick, so the same clip is less
/// likely to repeat back-to-back — that is what the bare <c>0.5</c> after the token decodes to.
/// Explicit-<c>WEIGHT</c> groups (<c>snd_plane_die</c>/<c>snd_plane_dmg</c>, whose <c>snd_nothing</c>
/// carries 0.7 — a 70% chance of silence) give each member its own weight and no recency decay.</para>
/// </summary>
public sealed class SoundGroup
{
    public readonly List<(string Name, float Weight)> Members = new();

    /// <summary>The group's dialogue chains: each an ordered list of snd names (the mission VO
    /// sequences; <c>snd_HI1Start</c> plays five lines in order). Not weighted members and never
    /// returned by <see cref="Pick"/>; kept so the comms/mission layer can consume them, and so
    /// <c>WorldSounds.Prewarm</c> can decode the lines. Playback wiring is that layer's, not
    /// this parser's.</summary>
    public readonly List<IReadOnlyList<string>> Chains = new();
    public string Name = "";
    public float RecencyFactor = 1f;   // DYNAMIC_WEIGHTS scalar on the last-picked member; 1 = none
    private int _last = -1;             // index Pick last returned (the recency memory)

    /// <summary>Picks a member by weight through the caller's RNG (the runtime's seedable
    /// <c>_rng</c>, so a lab replay is deterministic), scaling the last pick down by
    /// <see cref="RecencyFactor"/>. Null only when the group is empty.</summary>
    public string? Pick(Random rng)
    {
        if (Members.Count == 0)
        {
            return null;
        }
        if (Members.Count == 1)
        {
            return Members[0].Name;
        }
        float Weight(int i) => Members[i].Weight * (i == _last ? RecencyFactor : 1f);
        float total = 0f;
        for (int i = 0; i < Members.Count; i++)
        {
            total += Weight(i);
        }
        double roll = rng.NextDouble() * total;
        int chosen = Members.Count - 1;
        for (int i = 0; i < Members.Count; i++)
        {
            roll -= Weight(i);
            if (roll <= 0.0)
            {
                chosen = i;
                break;
            }
        }
        _last = chosen;
        return Members[chosen].Name;
    }

    /// <summary>Forgets which member was picked last. The recency memory lives outside the RNG, so
    /// re-seeding a runtime alone would not replay a pick sequence — whoever re-seeds calls this
    /// too (<see cref="AnimRuntime.Reseed"/> via <c>WorldSounds</c>).</summary>
    public void ResetRecency() => _last = -1;
}
