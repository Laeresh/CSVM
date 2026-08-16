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

/// <summary>The decoded target-ranking formula (docs/architecture.md, docs/formats/ai-rosters.md):
/// <c>rank = weight × 1200 + distance + objectiveBias</c>, MINIMISED, player base weight 0.7,
/// others 1.0, ±0.2 terms for bearing/altitude sign/facing, <c>1e21</c> beyond the activation
/// radius. <see cref="SelectBest"/> prefers a candidate no ally already holds
/// (<see cref="RankedTargetCandidate.AlliedAttackers"/>), falling back to the overall best when
/// the pool is exhausted — the design's deconfliction, a minimum reading.</summary>
public static class AiTargetRanking
{
    /// <summary>The decoded weight scale: one weight unit is worth 1200 m of distance.</summary>
    public const float WeightScale = 1200f;

    /// <summary>The player's base weight — the decoded hard constant.
    /// ⚠ Under minimisation this ranks the player AHEAD of an equal-distance AI target, by about
    /// 360 m of distance equivalence. That is the decoded arithmetic; do not "fix" it to rank the
    /// player last.</summary>
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

    /// <summary>An authored <c>rating_biases</c> weight resolves to rank units directly and
    /// NEGATED, so a positive bias attracts under minimisation (docs/formats/ai-rosters.md).
    /// ⚠ Not the weight scale: a bias is not in metres and is not multiplied by 1200.</summary>
    public const float BiasScale = -750f;

    /// <summary>The rank a bias of 1.0 or more collapses to: always target.</summary>
    public const float AlwaysTarget = -100000f;

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

    /// <summary>The objectiveBias term for a named candidate, from the FIRST matching
    /// <c>rating_biases</c> entry; 0 with no list or no match. The ends saturate rather than scale:
    /// 1.0 or more is always-target, −1.0 or less a hard exclusion.
    /// ⚠ An authored −1.0 means NEVER target, not a penalty. It is the dominant shipped value, so
    /// reading it as a penalty inverts the intent across most of the install.
    /// ⚠ The turret's flat term is not implemented; no caller distinguishes one.</summary>
    public static float ObjectiveBiasFor(string name, IReadOnlyList<AiRatingBias>? biases)
    {
        if (biases == null)
            return 0f;
        foreach (var b in biases)
        {
            if (!b.Matches(name))
                continue;
            if (b.Bias >= 1f)
                return AlwaysTarget;
            if (b.Bias <= -1f)
                return NotRanked;
            return b.Bias * BiasScale;
        }

        return 0f;
    }
}
