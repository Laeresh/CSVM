using System;
using System.Collections.Generic;
using System.IO;

namespace CSVM.Mech3;

/// <summary>
/// The combat-voice resolver: the <c>aiv</c> roster's <c>accentID</c> (slot 65) → a row in
/// <c>zrdr/voice.zrd</c> (the ACCENT table, 35 rows, not the trigger table) → a pool of pilot VO
/// ids → that pilot's clip entries in sounds.json. The clips ship as ordinary <c>SETS</c> entries
/// (<c>snd_id&lt;N&gt;_&lt;TYPE&gt;</c> → <c>VO_id&lt;N&gt;_&lt;TYPE&gt;.wav</c>, one set per pilot
/// id), and the data also ships per-family variant groups
/// (<c>snd_&lt;FAMILY&gt;-A_id&lt;N&gt;_random</c>, <c>DYNAMIC_WEIGHTS 0.5</c>) that pick one of
/// the <c>-A/-B/-C</c> takes, so a resolved name feeds <see cref="WorldSounds.PlayOneShot"/>
/// directly, group or plain def alike. See <c>docs/formats/combat-voice.md</c>.
///
/// <para>This is the chain only: trigger dispatch (cooldowns, the talker roll, speaker election)
/// is E16's and sits above this seam. ⚠ A def here is not proof of a playable clip: five pilot ids
/// (13, 15, 17, 35, 36) ship full def sets with no WAVs at all, and id 44 carries the 12 bearing
/// defs without their WAVs. Availability is answered by the prewarm
/// (<see cref="WorldSounds.HasStream"/>), never by def presence.</para>
/// </summary>
public sealed class CombatVoice
{
    /// <summary>
    /// The engine's 29-trigger table (ids 0–28), as family roots for <see cref="ClipsFor"/> /
    /// <see cref="PlayableFor"/>. Ids 1–12 are the computed bearing call-outs
    /// (<c>id = 1 + 3·bearing + altitudeBand</c>, low/level/high within each of 12/3/6/9 o'clock)
    /// and are exact clip tokens with no variant letter. Ids 20/21 resolve to the <c>DA</c>/<c>DE</c>
    /// family root; the <c>Bail</c>/<c>NoBail</c> split below it is the dispatcher's
    /// (constitution) call, so query <c>"DA-Bail"</c> or <c>"DA-NoBail"</c> for a playable name.
    /// </summary>
    public static readonly IReadOnlyList<string> TriggerFamilies = new[]
    {
        "WA-Turret",
        "WA-Enemy-12L", "WA-Enemy-12", "WA-Enemy-12H",
        "WA-Enemy-3L", "WA-Enemy-3", "WA-Enemy-3H",
        "WA-Enemy-6L", "WA-Enemy-6", "WA-Enemy-6H",
        "WA-Enemy-9L", "WA-Enemy-9", "WA-Enemy-9H",
        "WA-HighDmg", "WA-Attack", "PR-DngrZn", "PR-EnemyDwn",
        "DI-LowDmg", "DI-MedDmg", "DI-HighDmg",
        "DA", "DE",
        "GL-AllyDwn", "GL-EnemyDwn", "GL-PlyrDwn",
        "TA-FailTail", "TA-FailShk", "TA-SucShk",
        "DS-Ally",
    };

    private readonly Dictionary<int, int[]> _accents;
    private readonly Dictionary<int, List<(string Type, string DefName)>> _clips = new();
    private readonly HashSet<string> _defNames;
    private readonly IReadOnlyDictionary<string, SoundGroup> _groups;

    public CombatVoice(IReadOnlyDictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup> groups,
        IReadOnlyDictionary<int, int[]> accents)
    {
        _groups = groups;
        _accents = new Dictionary<int, int[]>(accents.Count);
        foreach (var (accent, pool) in accents)
        {
            _accents[accent] = pool;
        }
        _defNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in defs.Values)
        {
            if (!TryParseClipName(def.Name, out int voId, out string type))
            {
                continue;
            }
            _defNames.Add(def.Name);
            if (!_clips.TryGetValue(voId, out var list))
            {
                _clips[voId] = list = new List<(string, string)>();
            }
            list.Add((type, def.Name));
        }
    }

    /// <summary>Every accent id the table carries (35 rows in the retail install).</summary>
    public IReadOnlyCollection<int> AccentIds => _accents.Keys;

    /// <summary>Every pilot VO id that has at least one clip DEF. ⚠ Not "at least one playable
    /// clip"; see the class remark on the def-without-WAV ids.</summary>
    public IReadOnlyCollection<int> PilotIds => _clips.Keys;

    /// <summary>Loads the accent table (<c>zrdr/voice.zrd</c>): rows of
    /// <c>[accentId, voId, …]</c>.</summary>
    public static Dictionary<int, int[]> LoadAccents(string zrdrPath)
    {
        var accents = new Dictionary<int, int[]>();
        foreach (var row in Zrdr.LoadFile(zrdrPath, "voice.json"))
        {
            if (row is not List<object?> { Count: >= 2 } r || r[0] is not float accent)
            {
                continue;
            }
            var pool = new List<int>(r.Count - 1);
            for (int i = 1; i < r.Count; i++)
            {
                if (r[i] is float id)
                {
                    pool.Add((int)id);
                }
            }
            accents[(int)accent] = pool.ToArray();
        }
        return accents;
    }

    /// <summary>
    /// The distinct <c>accentID</c>s (roster slot 65) of one mission's <c>aiv</c> roster, i.e.
    /// which voices this mission's AI can speak with. Blocks are not fixed-width (42/65/66/67/68/81
    /// fields ship); one shorter than 66 simply has no accent and is skipped. A mission without a
    /// roster yields an empty list.
    /// </summary>
    public static IReadOnlyList<int> MissionAccentIds(string missionZrdrPath)
    {
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "aiv.json");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or InvalidDataException)
        {
            return Array.Empty<int>();
        }
        var accents = new SortedSet<int>();
        for (int i = 1; i < root.Count; i++)   // element 0 is the (slotId, label) header
        {
            if (root[i] is List<object?> { Count: >= 2 } block
                && block[1] is List<object?> { Count: >= 66 } fields
                && fields[65] is float accent && accent >= 0f)
            {
                accents.Add((int)accent);
            }
        }
        return new List<int>(accents);
    }

    /// <summary>The session convenience: the voice prewarm set for one mission's roster, or empty
    /// when the mission authors no roster (21 of the 53 mission dirs author no accents at all).
    /// The subset is chosen over prewarm-everything deliberately (see combat-voice.md).
    /// <paramref name="extraAccents"/> joins accents the session assigns outside the roster
    /// (the <c>--ai=…:accent=N</c> spawns) — a clip not in this set never plays (B8).</summary>
    public static IReadOnlyCollection<string> SessionPrewarmNames(string zrdrPath,
        string missionZrdrPath, IReadOnlyDictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup> groups, IEnumerable<int>? extraAccents = null)
    {
        var accents = new SortedSet<int>(MissionAccentIds(missionZrdrPath));
        if (extraAccents != null)
        {
            foreach (int accent in extraAccents)
            {
                accents.Add(accent);
            }
        }
        if (accents.Count == 0)
        {
            return Array.Empty<string>();
        }
        return new CombatVoice(defs, groups, LoadAccents(zrdrPath)).PrewarmNames(accents);
    }

    /// <summary>Splits <c>snd_id&lt;N&gt;_&lt;TYPE&gt;</c> into pilot VO id and TYPE token; false
    /// for any other def-name shape (<c>snd_idle_*</c> does not parse; the digits are required).</summary>
    public static bool TryParseClipName(string defName, out int voId, out string type)
    {
        voId = 0;
        type = "";
        const string prefix = "snd_id";
        if (!defName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        int i = prefix.Length;
        int id = 0;
        int digits = 0;
        while (i < defName.Length && defName[i] >= '0' && defName[i] <= '9')
        {
            id = (id * 10) + (defName[i] - '0');
            i++;
            digits++;
        }
        if (digits == 0 || i >= defName.Length - 1 || defName[i] != '_')
        {
            return false;
        }
        voId = id;
        type = defName.Substring(i + 1);
        return true;
    }

    /// <summary>The accent row's pilot VO id pool (rows 0–10 hold 2–3 ids, 11–34 one), or empty
    /// when the accent id is unknown.</summary>
    public int[] Pool(int accentId) =>
        _accents.TryGetValue(accentId, out var pool) ? pool : Array.Empty<int>();

    /// <summary>
    /// Picks one pilot VO id from the accent's pool, the spawn-time half of the chain. Pool
    /// members without any clip def are skipped, and <paramref name="eligible"/> narrows further
    /// (E16 passes a prewarm-backed availability check). Null when nothing in the pool qualifies.
    /// </summary>
    public int? PilotFor(int accentId, Random rng, Func<int, bool>? eligible = null)
    {
        var candidates = new List<int>();
        foreach (int id in Pool(accentId))
        {
            if (_clips.ContainsKey(id) && (eligible == null || eligible(id)))
            {
                candidates.Add(id);
            }
        }
        if (candidates.Count == 0)
        {
            return null;
        }
        return candidates[rng.Next(candidates.Count)];
    }

    /// <summary>
    /// The one name to hand <see cref="WorldSounds.PlayOneShot"/> for a pilot and a family root:
    /// the shipped <c>snd_&lt;family&gt;-A_id&lt;N&gt;_random</c> variant group when the data
    /// authors one (466 do; recency-weighted <c>-A/-B/-C</c> pick), else the plain def for a
    /// variantless token (the 12 bearing call-outs), else null. Both <c>PR-DngrZn</c> spellings
    /// are accepted for either family.
    /// </summary>
    public string? PlayableFor(int voId, string family)
    {
        foreach (var root in Spellings(family))
        {
            string group = $"snd_{root}-A_id{voId}_random";
            if (_groups.ContainsKey(group))
            {
                return group;
            }
            string def = $"snd_id{voId}_{root}";
            if (_defNames.Contains(def))
            {
                return def;
            }
        }
        return null;
    }

    /// <summary><see cref="PlayableFor"/> keyed by trigger id (0–28); ids 20/21 return null here
    /// because the Bail/NoBail split sits below the trigger; query the sub-family instead.</summary>
    public string? PlayableForTrigger(int voId, int triggerId) =>
        triggerId >= 0 && triggerId < TriggerFamilies.Count
            ? PlayableFor(voId, TriggerFamilies[triggerId])
            : null;

    /// <summary>Every clip def of a pilot under a family root: the token itself or any
    /// <c>root-…</c> extension of it (<c>"DA"</c> → all four Bail/NoBail variants;
    /// <c>"WA-Enemy-3H"</c> → the one bearing clip). Def names, playable individually.</summary>
    public IReadOnlyList<string> ClipsFor(int voId, string family)
    {
        var result = new List<string>();
        if (!_clips.TryGetValue(voId, out var list))
        {
            return result;
        }
        foreach (var (type, defName) in list)
        {
            foreach (var root in Spellings(family))
            {
                if (type.Equals(root, StringComparison.OrdinalIgnoreCase)
                    || type.StartsWith(root + "-", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(defName);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Every clip def name the given accents can reach through their pools, deduplicated:
    /// the mission's voice prewarm set.</summary>
    public IReadOnlyCollection<string> PrewarmNames(IEnumerable<int> accentIds)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (int accent in accentIds)
        {
            foreach (int voId in Pool(accent))
            {
                if (_clips.TryGetValue(voId, out var list))
                {
                    foreach (var (_, defName) in list)
                    {
                        names.Add(defName);
                    }
                }
            }
        }
        return names;
    }

    /// <summary>Every combat-voice clip def in the install (1,414 in retail): the
    /// prewarm-everything set, kept for measurement; the session uses the roster subset.</summary>
    public IReadOnlyCollection<string> AllClipNames() => _defNames;

    // The shipped data spells one concept two ways (PR-DngrZn-A/B/C/D beside PR-DangerZone-A/B);
    // a name-driven lookup accepts either for both.
    private static IEnumerable<string> Spellings(string family)
    {
        yield return family;
        if (family.StartsWith("PR-DngrZn", StringComparison.OrdinalIgnoreCase))
        {
            yield return "PR-DangerZone" + family.Substring("PR-DngrZn".Length);
        }
        else if (family.StartsWith("PR-DangerZone", StringComparison.OrdinalIgnoreCase))
        {
            yield return "PR-DngrZn" + family.Substring("PR-DangerZone".Length);
        }
    }
}
