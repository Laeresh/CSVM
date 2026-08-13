using System;
using System.Collections.Generic;
using System.IO;

namespace CSVM.Mech3;

/// <summary>One roster block's nine-slot skill vector, in the exe's stat order. Null = unset
/// (<c>-1</c> or omitted): the engine falls back to the airframe def's stat. The two slots the
/// designers left at 5 nearly everywhere are <c>Talker</c> and <c>Constitution</c>.</summary>
public readonly struct AiSkillVector
{
    public int? DareDevil { get; init; }

    public int? NaturalTouch { get; init; }

    public int? SixthSense { get; init; }

    public int? DeadEye { get; init; }

    public int? QuickDraw { get; init; }

    public int? SteadyHand { get; init; }

    public int? StunRecovery { get; init; }

    public int? Talker { get; init; }

    public int? Constitution { get; init; }
}

/// <summary>One roster <c>rating_biases</c> entry (slot 33, docs/formats/ai-rosters.md): a
/// node-name pattern with <c>*</c> wildcards and the bias it applies to target ranking. This
/// install authors <c>[pattern, bias]</c> pairs only (measured: 0 triples across all 53 files);
/// the exe's editor comment names a third element, so one is accepted defensively and preserved
/// raw (<see cref="Third"/> — parse it, act on nothing).</summary>
public sealed class AiRatingBias
{
    public AiRatingBias(string pattern, float bias, object? third)
    {
        Pattern = pattern;
        Bias = bias;
        Third = third;
    }

    /// <summary>The candidate-node-name pattern; <c>*</c> matches any run of characters.</summary>
    public string Pattern { get; }

    /// <summary>The authored bias (shipped values are −1.0); how it scales into the ranking is
    /// <c>AiTargetRanking.BiasScale</c>'s documented assumption.</summary>
    public float Bias { get; }

    /// <summary>The undecoded third element, raw. Preserved, never interpreted.</summary>
    public object? Third { get; }

    /// <summary>Case-insensitive wildcard match of <paramref name="name"/> against
    /// <see cref="Pattern"/>. Only <c>*</c> is special (any run, so <c>**</c> collapses to
    /// <c>*</c>) — the shipped data authors nothing else.</summary>
    public bool Matches(string name)
    {
        string p = Pattern;
        int pi = 0, ni = 0, star = -1, mark = 0;
        while (ni < name.Length)
        {
            if (pi < p.Length && (p[pi] == '*'))
            {
                star = pi++;
                mark = ni;
            }
            else if (pi < p.Length && char.ToLowerInvariant(p[pi]) == char.ToLowerInvariant(name[ni]))
            {
                pi++;
                ni++;
            }
            else if (star >= 0)
            {
                pi = star + 1;
                ni = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (pi < p.Length && p[pi] == '*')
            pi++;
        return pi == p.Length;
    }
}

/// <summary>
/// The AI pilot-skill constants (docs/formats/ai-rosters.md): the shared
/// <c>player.json</c>'s <c>ai_skill_parameters</c> block, one <c>[value@1, value@9]</c> endpoint
/// pair per stat, indexed by the roster's 1–9 skill ratings — plus the accessor that reads a
/// roster block's nine-slot skill vector (slots 22–30) by stat name.
///
/// <para>⚠ Ratings between the endpoints interpolate LINEARLY here. Only the two endpoints are
/// decoded; the curve between them is a documented assumption, not a read fact. Two stats improve
/// downward (<c>dead_eye_angle</c>, <c>steady_hand_chance</c>) — do not normalise the direction
/// away.</para>
///
/// <para><c>natural_touch</c> has no entry BY DESIGN: it compares directly against a maneuver's
/// own 1–9 difficulty, so asking this table for it throws rather than inventing a curve.</para>
/// </summary>
public sealed class AiSkills
{
    // The roster's nine consecutive skill slots (docs/formats/ai-rosters.md "The skill vector").
    private const int SkillSlotFirst = 22;

    // Roster slot 6: primary_target, the assigned target node name ("Primary target: %s").
    private const int PrimaryTargetSlot = 6;

    // Roster slot 33: rating_biases, [pattern, bias, ?] triples feeding target ranking.
    private const int RatingBiasesSlot = 33;

    private readonly Dictionary<string, (float At1, float At9)> _params =
        new(StringComparer.OrdinalIgnoreCase);

    private AiSkills()
    {
    }

    /// <summary>The raw endpoint pairs, keyed by the shipped stat-parameter names
    /// (<c>dead_eye_angle</c>, <c>quick_draw_angle</c>, …).</summary>
    public IReadOnlyDictionary<string, (float At1, float At9)> Parameters => _params;

    /// <summary>player.json's <c>min_ai_active_dist</c> (2000 m shipped) — the AI activation
    /// radius, and the fallback for every roster whose own volume slots are unauthored (all of
    /// them; docs/formats/ai-rosters.md "The three unnamed slots").</summary>
    public float MinAiActiveDist { get; private set; } = 2000f;

    /// <summary>Loads the <c>ai_skill_parameters</c> block from the shared zrdr scope's
    /// <c>player.json</c>. Throws when the block is absent — the shipped install always
    /// carries it, so a miss is a wrong path, not a default to paper over.</summary>
    public static AiSkills Load(string zrdrPath)
    {
        if (Zrdr.LoadFile(zrdrPath, "player.json")[0] is not List<object?> playerList)
            throw new InvalidDataException("player.json: unexpected root shape");
        var player = ZrdrDict.FromAlternating(playerList);
        if (player.List("ai_skill_parameters") is not { } block)
            throw new InvalidDataException("player.json carries no ai_skill_parameters block");
        var skills = new AiSkills();
        for (int i = 0; i + 1 < block.Count; i += 2)
        {
            if (block[i] is string name && block[i + 1] is List<object?> { Count: >= 2 } pair
                && pair[0] is float at1 && pair[1] is float at9)
                skills._params[name] = (at1, at9);
        }
        if (player.TryFloat("min_ai_active_dist", out float activeDist))
            skills.MinAiActiveDist = activeDist;
        return skills;
    }

    /// <summary>Reads a roster block's skill vector: slots 22–30 in the exe's stat order. A slot
    /// authored <c>-1</c>, missing (blocks are not fixed-width) or non-numeric reads null =
    /// UNSET — the engine then falls back to the airframe def's own stat keys
    /// (docs/formats/vehicle.md "AI-combatant tuning"), so null must stay null here rather than
    /// become an invented default.</summary>
    public static AiSkillVector RosterSkills(IReadOnlyList<object?> fields)
    {
        int? Slot(int offset)
        {
            int idx = SkillSlotFirst + offset;
            if (idx >= fields.Count || fields[idx] is not float f)
                return null;
            int v = (int)f;
            return v >= 1 && v <= 9 ? v : null;
        }

        return new AiSkillVector
        {
            DareDevil = Slot(0),
            NaturalTouch = Slot(1),
            SixthSense = Slot(2),
            DeadEye = Slot(3),
            QuickDraw = Slot(4),
            SteadyHand = Slot(5),
            StunRecovery = Slot(6),
            Talker = Slot(7),
            Constitution = Slot(8),
        };
    }

    /// <summary>Reads a roster block's assigned target (slot 6, <c>primary_target</c>): the
    /// target node's name, or null when unset (<c>""</c> on 346 of 414 shipped blocks, or a
    /// short block omitting the slot). <c>"player"</c> names the player's aircraft.</summary>
    public static string? RosterPrimaryTarget(IReadOnlyList<object?> fields) =>
        fields.Count > PrimaryTargetSlot && fields[PrimaryTargetSlot] is string { Length: > 0 } s
            ? s
            : null;

    /// <summary>Reads a roster block's <c>rating_biases</c> (slot 33): the authored
    /// [pattern, bias, ?] entries in order, or an empty list when the slot is null, omitted or
    /// malformed. The third element is undecoded and kept raw on each entry.</summary>
    public static List<AiRatingBias> RosterRatingBiases(IReadOnlyList<object?> fields)
    {
        var result = new List<AiRatingBias>();
        if (fields.Count <= RatingBiasesSlot || fields[RatingBiasesSlot] is not List<object?> list)
            return result;
        foreach (var entry in list)
        {
            if (entry is List<object?> { Count: >= 2 } triple
                && triple[0] is string pattern && triple[1] is float bias)
                result.Add(new AiRatingBias(pattern, bias, triple.Count > 2 ? triple[2] : null));
        }

        return result;
    }

    /// <summary>Loads one mission's roster blocks (<c>aiv.json</c> in the mission zrdr scope):
    /// element 0 is the positional header, elements 1…N are <c>["nodename", [fields…]]</c>.
    /// Blocks are NOT fixed-width (42–81 fields ship); the fields list is returned raw so slot
    /// readers stay defensive.</summary>
    public static List<(string Name, List<object?> Fields)> LoadRoster(string missionZrdrPath)
    {
        var root = Zrdr.LoadFileOrEmpty(missionZrdrPath, "aiv.json");
        var blocks = new List<(string, List<object?>)>();
        for (int i = 1; i < root.Count; i++)
        {
            if (root[i] is List<object?> { Count: >= 2 } entry
                && entry[0] is string name && entry[1] is List<object?> fields)
                blocks.Add((name, fields));
        }
        return blocks;
    }

    /// <summary>The stat parameter at a 1–9 rating: the shipped endpoints at 1 and 9, linear in
    /// between (the documented assumption above). Out-of-range ratings clamp — the shipped data
    /// authors nothing outside 1–9 (<c>ace_stats</c> caps at 9).</summary>
    public float At(string key, float rating)
    {
        if (!_params.TryGetValue(key, out var pair))
            throw new KeyNotFoundException($"ai_skill_parameters has no '{key}' (natural_touch has none by design)");
        float t = (Math.Clamp(rating, 1f, 9f) - 1f) / 8f;
        return pair.At1 + (pair.At9 - pair.At1) * t;
    }

    /// <summary>The dead-eye aim-error cone half-angle, degrees (4.0° at 1 → 1.45° at 9 —
    /// tighter is the better pilot).</summary>
    public float DeadEyeAngleDeg(float rating) => At("dead_eye_angle", rating);

    /// <summary>The quick-draw shot-acceptance cone half-angle off the target's nose/tail,
    /// degrees (50° at 1 → 89° at 9 — a better pilot takes more oblique shots).</summary>
    public float QuickDrawAngleDeg(float rating) => At("quick_draw_angle", rating);
}

