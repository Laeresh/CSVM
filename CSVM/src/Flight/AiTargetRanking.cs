using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>One target-selection candidate, as a snapshot. Built per acquisition by
/// whoever holds the live lists; the ranker reads nothing else, so the formula unit-tests
/// without a scene tree (the <see cref="AiModeMachine"/> pattern).</summary>
public struct RankedTargetCandidate
{
    /// <summary>World position.</summary>
    public Vector3 Position;

    /// <summary>The candidate's nose axis (the target-facing term). Zero = no facing
    /// information, which reads as the unfavourable (closing) arm.</summary>
    public Vector3 Forward;

    /// <summary>Whether this candidate is a human-piloted aircraft — the 0.7 weight case.</summary>
    public bool IsPlayer;

    /// <summary>The objectiveBias term, already in rank units
    /// (<see cref="AiTargetRanking.ObjectiveBiasFor"/>).</summary>
    public float ObjectiveBias;

    /// <summary>How many ALLIES already hold this candidate as their standing target — the
    /// deconfliction input. Zero whenever every pilot sits on its own default team (free flight,
    /// <c>--vs</c>); it counts real allies once a mission puts two AI on the same
    /// <see cref="FlightController.Team"/>.</summary>
    public int AlliedAttackers;
}

/// <summary>One candidate's rank and the inputs that made it — the observability shape the
/// first-acquisition breadcrumb logs.</summary>
public readonly struct TargetScore
{
    public TargetScore(float weight, float distance, float bias, float rank)
    {
        Weight = weight;
        Distance = distance;
        Bias = bias;
        Rank = rank;
    }

    /// <summary>The final weight after the ±0.2 terms (0.7/1.0 base).</summary>
    public float Weight { get; }

    /// <summary>Straight-line distance, metres.</summary>
    public float Distance { get; }

    /// <summary>The objectiveBias term, rank units.</summary>
    public float Bias { get; }

    /// <summary>weight × 1200 + distance + objectiveBias — minimised; <see
    /// cref="AiTargetRanking.NotRanked"/> beyond the activation radius.</summary>
    public float Rank { get; }
}

/// <summary>The decoded target-ranking formula, recovered from the engine's own debug
/// readout (docs/formats/ai-rosters.md "AI modes, engine-side"):
/// <c>rank = weight × 1200 + distance + objectiveBias</c>, MINIMISED, where the weight starts at
/// 1.0 for any target except the player, which starts at 0.7, then takes ±0.2 terms for bearing,
/// altitude sign and target facing. A target beyond the activation radius scores the engine's
/// own 1e21 and is never picked.
///
/// <para><b>Decoded:</b> the formula shape, the 1200 scale, the 0.7/1.0 base weights, the ±0.2
/// term magnitudes, and the 1e21 activation cutoff. ⚠ Under minimisation the player's 0.7 base
/// ranks the player AHEAD of an equal-distance AI target by 360 m of distance equivalence —
/// that is the literal decoded arithmetic, implemented exactly (the plan's "rank the player
/// last" gloss does not follow from it and is recorded as such in the landing notes).</para>
///
/// <para><b>Assumed, named as such:</b> each ±0.2 term's sign convention follows the design's
/// target-ranking section (front 120° arc, targets below, targets facing away are the
/// favourable, −0.2 arms); <see cref="BiasScale"/> maps an authored bias of −1.0 onto one full
/// weight unit (raw metres would leave every shipped value inert); a bias list's FIRST matching
/// entry wins (authored order). The readout's +0.4 dynamics-class and −0.5 structure terms are
/// not modelled — no non-aircraft candidate reaches this path yet.</para>
///
/// <para><b>Deconfliction (invented minimum):</b> the design says a target already held by a
/// peer is dropped from the pool and selection re-runs, falling back to the full pool when it
/// empties. <see cref="SelectBest"/> implements exactly that: the best-ranked candidate with
/// zero <see cref="RankedTargetCandidate.AlliedAttackers"/> wins; when every ranked candidate
/// is held, the overall best is taken. What counts as an "allied attacker" is the caller's
/// (invented: a same-team gunner whose standing target is the candidate).</para></summary>
public static class AiTargetRanking
{
    /// <summary>The decoded weight scale: one weight unit is worth 1200 m of distance.</summary>
    public const float WeightScale = 1200f;

    /// <summary>The player's base weight — the decoded hard constant.</summary>
    public const float PlayerWeight = 0.7f;

    /// <summary>Every other target's base weight.</summary>
    public const float BaseWeight = 1f;

    /// <summary>Magnitude of the bearing / altitude-sign / target-facing weight terms.</summary>
    public const float TermWeight = 0.2f;

    /// <summary>The engine's own out-of-activation score: never picked.</summary>
    public const float NotRanked = 1e21f;

    /// <summary>cos 60° — the design's 120° front arc, the favourable bearing (the three-way
    /// front/rear/beam split the design describes is collapsed to the readout's binary ± term;
    /// the arc width is the design's, the collapse is an assumption).</summary>
    public const float FrontArcCos = 0.5f;

    /// <summary>ASSUMPTION: an authored rating bias scales by the weight scale, so the shipped
    /// −1.0 entries offset one full weight unit. Raw metres would make every shipped value
    /// inert, which cannot be the authored intent.</summary>
    public const float BiasScale = WeightScale;

    /// <summary>One candidate's rank and its inputs. <paramref name="ownForward"/> must be
    /// unit-length (a basis column).</summary>
    public static TargetScore Score(Vector3 ownPos, Vector3 ownForward, float activationRange,
        in RankedTargetCandidate c)
    {
        var to = c.Position - ownPos;
        float dist = to.Length();
        if (dist > activationRange)
            return new TargetScore(0f, dist, c.ObjectiveBias, NotRanked);
        float weight = c.IsPlayer ? PlayerWeight : BaseWeight;
        var toDir = dist > 1e-3f ? to / dist : ownForward;
        // Bearing: inside the front arc is the favourable arm (design: front arc highest).
        weight += ownForward.Dot(toDir) >= FrontArcCos ? -TermWeight : TermWeight;
        // Altitude sign: below is the favourable arm (design: targets below above targets above).
        weight += c.Position.Y < ownPos.Y ? -TermWeight : TermWeight;
        // Target facing: facing away is the favourable arm (design: moving away above closing).
        weight += c.Forward.LengthSquared() > 1e-6f && c.Forward.Dot(to) > 0f
            ? -TermWeight
            : TermWeight;
        return new TargetScore(weight, dist, c.ObjectiveBias,
            weight * WeightScale + dist + c.ObjectiveBias);
    }

    /// <summary>The pick: the minimal-rank candidate no ally already holds; when every ranked
    /// candidate is held, the minimal-rank candidate outright (the design's exhausted-pool
    /// fallback). Returns the candidate's index and its score, or −1 when nothing ranks (all
    /// beyond activation, or the list is empty).</summary>
    public static int SelectBest(Vector3 ownPos, Vector3 ownForward, float activationRange,
        IReadOnlyList<RankedTargetCandidate> candidates, out TargetScore best)
    {
        int bestAny = -1, bestFree = -1;
        TargetScore scoreAny = default, scoreFree = default;
        for (int i = 0; i < candidates.Count; i++)
        {
            var s = Score(ownPos, ownForward, activationRange, candidates[i]);
            if (s.Rank >= NotRanked)
                continue;
            if (bestAny < 0 || s.Rank < scoreAny.Rank)
            {
                bestAny = i;
                scoreAny = s;
            }

            if (candidates[i].AlliedAttackers == 0 && (bestFree < 0 || s.Rank < scoreFree.Rank))
            {
                bestFree = i;
                scoreFree = s;
            }
        }

        if (bestFree >= 0)
        {
            best = scoreFree;
            return bestFree;
        }

        best = scoreAny;
        return bestAny;
    }

    /// <summary>The objectiveBias term for a named candidate: the FIRST matching
    /// <c>rating_biases</c> entry's bias × <see cref="BiasScale"/>, 0 with no list or no match.
    /// AI spawned without a roster block (<c>--ai</c>, egen) carry no biases — the documented
    /// gap until mission spawns attach roster identities.</summary>
    public static float ObjectiveBiasFor(string name, IReadOnlyList<AiRatingBias>? biases)
    {
        if (biases == null)
            return 0f;
        foreach (var b in biases)
        {
            if (b.Matches(name))
                return b.Bias * BiasScale;
        }

        return 0f;
    }
}
