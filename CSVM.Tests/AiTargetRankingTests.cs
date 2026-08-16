using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The target-ranking formula (docs/formats/ai-rosters.md "AI modes, engine-side"):
/// rank = weight × 1200 + distance + objectiveBias, minimised, player base weight 0.7, ±0.2
/// bearing/altitude/facing terms, 1e21 beyond the activation radius. The term sign conventions
/// are still documented assumptions; these tests pin the decoded arithmetic and the assumptions
/// both, so a re-decode that moves either fails loudly. Plus the roster accessors for slots 6
/// (primary_target) and 33 (rating_biases), with install-wide goldens.
/// </summary>
public class AiTargetRankingTests
{
    private const float Activation = 2000f;

    private static readonly Vector3 OwnPos = new(0f, 500f, 0f);
    private static readonly Vector3 OwnFwd = new(0f, 0f, -1f);

    // ---- the formula -------------------------------------------------------------------------

    [Fact]
    public void PlayerRanksAtPointSevenWorkedComparison()
    {
        // Equal geometry, 800 m each: the player's 0.7 base weight is worth 0.3 × 1200 = 360
        // rank units, so the player out-ranks (rank is minimised) the AI target exactly by it.
        var player = Ahead(800f, isPlayer: true, xOffset: 100f);
        var ai = Ahead(800f, xOffset: -100f);
        var sPlayer = AiTargetRanking.Score(OwnPos, OwnFwd, Activation, player);
        var sAi = AiTargetRanking.Score(OwnPos, OwnFwd, Activation, ai);
        Assert.Equal(0.7f - 0.2f + 0.2f - 0.2f, sPlayer.Weight, 3); // front −, above +, away −
        Assert.Equal(1.0f - 0.2f + 0.2f - 0.2f, sAi.Weight, 3);
        Assert.Equal(360f, sAi.Rank - sPlayer.Rank, 1);

        int pick = AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { ai, player }, out var best);
        Assert.Equal(1, pick);
        Assert.Equal(sPlayer.Rank, best.Rank, 2);
    }

    [Fact]
    public void TheWeightScaleDominatesDistanceAt1200()
    {
        // The 0.3 player-weight delta equals 360 m: an AI target 500 m closer wins, one only
        // 300 m closer still loses.
        var player = Ahead(900f, isPlayer: true, xOffset: 100f);
        Assert.Equal(0, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { Ahead(400f, xOffset: -100f), player }, out _));
        Assert.Equal(1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { Ahead(600f, xOffset: -100f), player }, out _));
        // One ±0.2 term flip is 480 rank units: a target astern loses to one 400 m farther
        // ahead (bearing is the only differing term — the astern nose flips too so facing
        // stays on the away arm).
        var behind = Ahead(400f);
        behind.Position = OwnPos + new Vector3(0f, 0f, 400f);
        behind.Forward = new Vector3(0f, 0f, 1f);
        Assert.Equal(1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { behind, Ahead(800f) }, out _));
    }

    [Fact]
    public void DistanceBreaksTies()
    {
        var near = Ahead(500f);
        var far = Ahead(900f);
        Assert.Equal(400f, RankOf(far) - RankOf(near), 1);
        Assert.Equal(0, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { near, far }, out _));
    }

    [Fact]
    public void BeyondActivationScoresTheEnginesOwn1e21AndIsNeverPicked()
    {
        var outside = Ahead(Activation + 1f);
        Assert.Equal(AiTargetRanking.NotRanked, RankOf(outside));
        // Even alone, an out-of-activation target is never selected.
        Assert.Equal(-1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { outside }, out _));
        // At the radius it still ranks (the cutoff is strictly beyond).
        Assert.True(RankOf(Ahead(Activation)) < AiTargetRanking.NotRanked);
    }

    [Fact]
    public void TheThreeTermsMoveTheWeightByPointTwoEach()
    {
        var baseline = Ahead(500f);
        float b = RankOf(baseline);

        var behind = baseline;
        behind.Position = OwnPos + new Vector3(0f, 0f, 500f); // astern: bearing flips…
        behind.Forward = new Vector3(0f, 0f, 1f);             // …and only bearing
        Assert.Equal(0.4f * AiTargetRanking.WeightScale, RankOf(behind) - b, 1);

        var below = baseline;
        below.Position = baseline.Position + Vector3.Down * 100f; // below: altitude flips
        float distBelow = OwnPos.DistanceTo(below.Position);
        Assert.Equal(-0.4f * AiTargetRanking.WeightScale + (distBelow - 500f),
            RankOf(below) - b, 1);

        var closing = baseline;
        closing.Forward = new Vector3(0f, 0f, 1f); // nose at the shooter: facing flips
        Assert.Equal(0.4f * AiTargetRanking.WeightScale, RankOf(closing) - b, 1);

        // No facing information reads as the unfavourable (closing) arm.
        var noFacing = baseline;
        noFacing.Forward = Vector3.Zero;
        Assert.Equal(RankOf(closing), RankOf(noFacing), 1);
    }

    // ---- rating_biases -----------------------------------------------------------------------

    [Fact]
    public void BiasWildcardsMatchAsAuthored()
    {
        var star = new AiRatingBias("fuel_truck*", -1f, 0f);
        Assert.True(star.Matches("fuel_truck"));
        Assert.True(star.Matches("fuel_truck_02"));
        Assert.True(star.Matches("FUEL_TRUCK_02")); // case-insensitive
        Assert.False(star.Matches("truck"));
        Assert.False(star.Matches("a_fuel_truck")); // no implicit leading wildcard

        var doubleStar = new AiRatingBias("aagun**", -1f, null);
        Assert.True(doubleStar.Matches("aagun03"));

        var exact = new AiRatingBias("bloodhawk_2", -1f, null);
        Assert.True(exact.Matches("bloodhawk_2"));
        Assert.False(exact.Matches("bloodhawk_21"));

        var infix = new AiRatingBias("hk_*p", -1f, null);
        Assert.True(infix.Matches("hk_zep"));
        Assert.False(infix.Matches("hk_zeps"));
    }

    [Fact]
    public void ObjectiveBiasSaturatesAtBothEndsAndFirstMatchWins()
    {
        var biases = new List<AiRatingBias>
        {
            new("bloodhawk_*", -1f, 0f),
            new("bloodhawk_2", -0.5f, 0f), // shadowed: first match wins (authored order)
        };
        // −1.0 is the exclusion, not a penalty — the dominant shipped value.
        Assert.Equal(AiTargetRanking.NotRanked,
            AiTargetRanking.ObjectiveBiasFor("bloodhawk_2", biases), 1);
        Assert.Equal(0f, AiTargetRanking.ObjectiveBiasFor("piratezep", biases), 1);
        Assert.Equal(0f, AiTargetRanking.ObjectiveBiasFor("bloodhawk_2", null), 1);

        // Between the ends it scales to rank units directly and negated, so a positive bias
        // attracts: 0.4 is worth 300 rank units in the candidate's favour.
        Assert.Equal(-300f,
            AiTargetRanking.ObjectiveBiasFor("x", new List<AiRatingBias> { new("x", 0.4f, null) }), 1);
        // And 1.0 or more is always-target.
        Assert.Equal(AiTargetRanking.AlwaysTarget,
            AiTargetRanking.ObjectiveBiasFor("x", new List<AiRatingBias> { new("x", 1f, null) }), 1);
    }

    [Fact]
    public void AnExcludedTargetIsNeverPickedEvenWhenItIsTheOnlyCandidate()
    {
        // The whole point of the exclusion: a −1.0 target is not merely deprioritised.
        var excluded = Ahead(500f, bias: AiTargetRanking.NotRanked);
        Assert.Equal(-1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { excluded }, out _));

        // …and a plain target beats it however much farther away it is, inside the radius.
        var plain = Ahead(1900f, xOffset: 50f);
        Assert.Equal(1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { excluded, plain }, out _));
    }

    // ---- deconfliction -----------------------------------------------------------------------

    [Fact]
    public void AHeldTargetIsDroppedForTheBestFreeOne()
    {
        // The better-ranked candidate is already held by an ally: the free peer wins.
        var held = Ahead(500f, attackers: 1);
        var free = Ahead(900f, xOffset: 50f);
        Assert.Equal(1, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { held, free }, out var best));
        Assert.Equal(900f, best.Distance, 1);
    }

    [Fact]
    public void AnExhaustedPoolFallsBackToTheBestOverall()
    {
        // Every candidate is held: the design's "exhausting the pool returns to the top" —
        // the best-ranked candidate is taken anyway.
        var a = Ahead(500f, attackers: 1);
        var b = Ahead(900f, attackers: 2, xOffset: 50f);
        Assert.Equal(0, AiTargetRanking.SelectBest(OwnPos, OwnFwd, Activation,
            new[] { a, b }, out var best));
        Assert.Equal(500f, best.Distance, 1);
    }

    // ---- the roster accessors ------------------------------------------------------------------

    [Fact]
    public void RosterPrimaryTargetReadsSlotSix()
    {
        var fields = new List<object?>();
        for (int i = 0; i < 10; i++)
            fields.Add(0f);
        fields[6] = "piratezep";
        Assert.Equal("piratezep", AiSkills.RosterPrimaryTarget(fields));
        fields[6] = string.Empty; // the 346 unset blocks author ""
        Assert.Null(AiSkills.RosterPrimaryTarget(fields));
        Assert.Null(AiSkills.RosterPrimaryTarget(new List<object?> { 0f, 0f }));
    }

    [Fact]
    public void RosterRatingBiasesParseTriplesAndPreserveTheThirdElement()
    {
        var fields = new List<object?>();
        for (int i = 0; i < 34; i++)
            fields.Add(0f);
        fields[33] = new List<object?>
        {
            new List<object?> { "tcargun*", -1f, 0f },     // triple: the exe comment's shape
            new List<object?> { "bloodhawk_2", -1f },      // pair: what this install authors
            new List<object?> { 42f, -1f, 0f },            // malformed: skipped
        };
        var biases = AiSkills.RosterRatingBiases(fields);
        Assert.Equal(2, biases.Count);
        Assert.Equal("tcargun*", biases[0].Pattern);
        Assert.Equal(-1f, biases[0].Bias, 3);
        Assert.Equal(0f, biases[0].Third);
        Assert.Null(biases[1].Third);

        fields[33] = null; // the 93 null blocks
        Assert.Empty(AiSkills.RosterRatingBiases(fields));
        Assert.Empty(AiSkills.RosterRatingBiases(new List<object?> { 0f }));
    }

    // ---- goldens against the shipped extraction ------------------------------------------------

    [ExtractedDataFact]
    public void TheShippedSlotSixAndThirtyThreeCensusesHold()
    {
        // The measured counts: 414 blocks;
        // primary_target authored on 68 (27 of them "player"); rating_biases a list on 321.
        int blocks = 0, primaries = 0, playerPrimaries = 0, biased = 0, thirds = 0;
        int entries = 0, minusOnes = 0;
        foreach (var roster in AllRosters())
        {
            foreach (var (_, fields) in roster)
            {
                blocks++;
                if (AiSkills.RosterPrimaryTarget(fields) is { } primary)
                {
                    primaries++;
                    if (primary == "player")
                        playerPrimaries++;
                }

                var biases = AiSkills.RosterRatingBiases(fields);
                if (biases.Count > 0)
                    biased++;
                entries += biases.Count;
                minusOnes += biases.Count(b => b.Bias == -1f);
                thirds += biases.Count(b => b.Third != null);
            }
        }

        Assert.Equal(414, blocks);
        Assert.Equal(68, primaries);
        Assert.Equal(27, playerPrimaries);
        Assert.Equal(321, biased);
        Assert.Equal(697, entries);
        Assert.Equal(389, minusOnes); // biases run -1.0…1.0, both signs, -1.0 dominant
        // Measured 2026-08-13: every shipped entry is a [pattern, bias] PAIR — the exe
        // comment's third element is never authored in this install (0 triples across all 53
        // files). The reader still preserves one defensively when present.
        Assert.Equal(0, thirds);
    }

    // A candidate ahead of the shooter with every ±0.2 term on the same arm as its
    // peers: in the front arc, above the shooter (level counts as above), nose pointing away.
    private static RankedTargetCandidate Ahead(float distance, bool isPlayer = false,
        float bias = 0f, int attackers = 0, float xOffset = 0f) => new()
        {
            Position = OwnPos + new Vector3(xOffset, 0f, -Mathf.Sqrt(distance * distance - xOffset * xOffset)),
            Forward = new Vector3(0f, 0f, -1f),
            IsPlayer = isPlayer,
            ObjectiveBias = bias,
            AlliedAttackers = attackers,
        };

    private static float RankOf(in RankedTargetCandidate c) =>
        AiTargetRanking.Score(OwnPos, OwnFwd, Activation, c).Rank;

    private static IEnumerable<List<(string Name, List<object?> Fields)>> AllRosters()
    {
        string root = TestData.DataRoot!;
        foreach (string chapter in new[] { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" })
        {
            string dir = Path.Combine(root, "extracted", chapter);
            if (!Directory.Exists(dir))
                continue;
            foreach (string missionDir in Directory.EnumerateDirectories(dir))
            {
                // Chapter-scope dirs (anim/texture/zrdr/…) are not missions and have no
                // <dir>/zrdr scope; a mission dir without an aiv also just contributes nothing.
                string zrdr = SessionPaths.MissionZrdr(root, chapter, Path.GetFileName(missionDir));
                List<(string Name, List<object?> Fields)> roster;
                try
                {
                    roster = AiSkills.LoadRoster(zrdr);
                }
                catch (IOException)
                {
                    continue;
                }

                if (roster.Count > 0)
                    yield return roster;
            }
        }
    }
}
