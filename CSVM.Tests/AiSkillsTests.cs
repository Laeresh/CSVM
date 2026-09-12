using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The <c>ai_skill_parameters</c> reader and the roster skill-vector accessor
/// (docs/formats/ai-rosters.md). Mechanics on the hand-authored
/// <c>fixtures/zrdr-skills/player.json</c>; the shipped endpoint constants and the roster's
/// worked cases (the cabbie, the dead-eye-1 mooks) as goldens against the real extraction.
/// The between-endpoint curve is LINEAR, and, decoded as of E42, its origin is rating 0, not
/// rating 1: <c>value = lo + (hi-lo) · rating/9</c> (docs/org/aiControlLaw.md "The skill scalar").
/// </summary>
public class AiSkillsTests
{
    private static AiSkills Fixture => AiSkills.Load(TestData.Fixture("zrdr-skills"));

    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [Fact]
    public void ReadsTheEndpointPairs()
    {
        var s = Fixture;
        Assert.Equal(3, s.Parameters.Count);
        // Rating 1 is NOT the pair's lo endpoint: the engine's own origin is rating 0
        // (lo + (hi-lo)/9 at rating 1), decoded in docs/org/aiControlLaw.md.
        Assert.Equal(8.0f + (2.0f - 8.0f) / 9f, s.At("dead_eye_angle", 1f), 3);
        Assert.Equal(2.0f, s.At("dead_eye_angle", 9f), 3);
        Assert.Equal(40.0f + (88.0f - 40.0f) / 9f, s.At("quick_draw_angle", 1f), 3);
        Assert.Equal(88.0f, s.At("quick_draw_angle", 9f), 3);
    }

    [Fact]
    public void InterpolatesLinearlyFromARatingZeroOrigin()
    {
        var s = Fixture;
        // value = lo + (hi-lo) * rating/9: rating 5 is 5/9 of the way, rating 3 is 3/9.
        Assert.Equal(8.0f + (2.0f - 8.0f) * (5f / 9f), s.At("dead_eye_angle", 5f), 3);
        Assert.Equal(8.0f + (2.0f - 8.0f) * (3f / 9f), s.At("dead_eye_angle", 3f), 3);
        Assert.Equal(40.0f + (88.0f - 40.0f) * (5f / 9f), s.At("quick_draw_angle", 5f), 3);
    }

    [Fact]
    public void DownwardImprovingStatsKeepTheirDirection()
    {
        var s = Fixture;
        // dead_eye_angle and steady_hand_chance IMPROVE downward; the reader must not
        // normalise the direction away.
        Assert.True(s.At("dead_eye_angle", 9f) < s.At("dead_eye_angle", 1f));
        Assert.True(s.At("steady_hand_chance", 9f) < s.At("steady_hand_chance", 1f));
    }

    [Fact]
    public void OutOfRangeRatingsClamp()
    {
        var s = Fixture;
        // The floor is rating 0, which the difficulty offset reaches and the engine clamps to,
        // so a 0 is the pair's own lo and not the rating-1 reading.
        Assert.Equal(8.0f, s.At("dead_eye_angle", 0f), 3);
        Assert.Equal(s.At("dead_eye_angle", 0f), s.At("dead_eye_angle", -3f), 3);
        Assert.NotEqual(s.At("dead_eye_angle", 1f), s.At("dead_eye_angle", 0f), 3);
        Assert.Equal(s.At("dead_eye_angle", 9f), s.At("dead_eye_angle", 42f), 3);
    }

    [Fact]
    public void AnUnknownStatThrowsRatherThanInventing()
    {
        // natural_touch has no entry BY DESIGN (it compares against maneuver difficulty
        // directly); asking must be an error, never a silent default.
        Assert.Throws<KeyNotFoundException>(() => Fixture.At("natural_touch", 5f));
    }

    [Fact]
    public void RosterSkillsReadTheNineSlotsByName()
    {
        // A hand-authored 31-field block: slots 22-30 carry 9 6 6 3 3 6 9 7 8 (the shape of
        // the documented cabbie case), everything else is filler.
        var fields = new List<object?>();
        for (int i = 0; i < 22; i++)
            fields.Add(0f);
        foreach (float v in new[] { 9f, 6f, 6f, 3f, 3f, 6f, 9f, 7f, 8f })
            fields.Add(v);
        var v22 = AiSkills.RosterSkills(fields);
        Assert.Equal(9, v22.DareDevil);
        Assert.Equal(6, v22.NaturalTouch);
        Assert.Equal(6, v22.SixthSense);
        Assert.Equal(3, v22.DeadEye);
        Assert.Equal(3, v22.QuickDraw);
        Assert.Equal(6, v22.SteadyHand);
        Assert.Equal(9, v22.StunRecovery);
        Assert.Equal(7, v22.Talker);
        Assert.Equal(8, v22.Constitution);
    }

    [Fact]
    public void RosterNitroReadsSlot34AsAFlag()
    {
        var fields = new List<object?>();
        for (int i = 0; i < 34; i++)
            fields.Add(0f);
        fields.Add(1f);
        Assert.True(AiSkills.RosterNitro(fields));
        fields[34] = -1f;
        Assert.False(AiSkills.RosterNitro(fields));
        Assert.False(AiSkills.RosterNitro(new List<object?> { 0f, 0f }));
    }

    [Fact]
    public void UnsetAndMissingSlotsReadNull()
    {
        // -1 is the authored unset marker; a short block (they ship at 42-81 fields) simply
        // omits the slots. Both read null, the engine falls back to the airframe def's own
        // stat keys, so no default may be invented here.
        var unset = new List<object?>();
        for (int i = 0; i < 22; i++)
            unset.Add(0f);
        foreach (float f in new[] { -1f, -1f, -1f, 1f, -1f, -1f, -1f, -1f, -1f })
            unset.Add(f);
        var v = AiSkills.RosterSkills(unset);
        Assert.Null(v.DareDevil);
        Assert.Equal(1, v.DeadEye);
        Assert.Null(v.Constitution);

        var shortBlock = new List<object?> { 0f, 0f, 0f };
        var s = AiSkills.RosterSkills(shortBlock);
        Assert.Null(s.DeadEye);
        Assert.Null(s.Talker);
    }

    // ---- goldens against the shipped extraction --------------------------------------------

    [ExtractedDataFact]
    public void TheShippedEndpointConstantsParse()
    {
        var s = AiSkills.Load(SharedZrdr);
        // The decoded table (docs/formats/ai-rosters.md): ten keys, no natural_touch.
        Assert.Equal(10, s.Parameters.Count);
        Assert.False(s.Parameters.ContainsKey("natural_touch"));
        // Rating 1 is lo + (hi-lo)/9, not the raw lo endpoint (the engine's origin is rating 0).
        Assert.Equal(4.0f + (1.45f - 4.0f) / 9f, s.DeadEyeAngleDeg(1), 2);
        Assert.Equal(1.45f, s.DeadEyeAngleDeg(9), 2);
        Assert.Equal(50f + (89f - 50f) / 9f, s.QuickDrawAngleDeg(1), 1);
        Assert.Equal(89f, s.QuickDrawAngleDeg(9), 1);
        Assert.Equal(0.5f + (0.08f - 0.5f) / 9f, s.At("steady_hand_chance", 1f), 2);
        Assert.Equal(0.08f, s.At("steady_hand_chance", 9f), 2);
        Assert.Equal(4.8f + (0.6f - 4.8f) / 9f, s.At("stun_recovery_interval", 1f), 2);
        Assert.Equal(0.6f, s.At("stun_recovery_interval", 9f), 2);
        Assert.Equal(0.35f + (0.95f - 0.35f) / 9f, s.At("constitution_chance", 1f), 2);
        Assert.Equal(0.95f, s.At("constitution_chance", 9f), 2);
        Assert.Equal(0.35f + (0.99f - 0.35f) / 9f, s.At("daredevil_chance", 1f), 2);
        Assert.Equal(0.99f, s.At("daredevil_chance", 9f), 2);
    }

    [ExtractedDataFact]
    public void TheCabbieAndTheMooksReadAsDocumented()
    {
        // C5/M01's cabbie autogyro: the worked skill-vector case (9 6 6 3 3 6 9 7 8, the two
        // lowest values on dead_eye/quick_draw, a cabbie who cannot shoot).
        var roster = AiSkills.LoadRoster(SessionPaths.MissionZrdr(TestData.DataRoot!, "C5", "M01"));
        var cabbie = roster.First(b => b.Name.StartsWith("autogyro", System.StringComparison.OrdinalIgnoreCase));
        var v = AiSkills.RosterSkills(cabbie.Fields);
        Assert.Equal(3, v.DeadEye);
        Assert.Equal(3, v.QuickDraw);
        Assert.Equal(9, v.DareDevil);
        Assert.Equal(7, v.Talker);
        Assert.Equal(8, v.Constitution);

        // C1/M04 carries generic mooks: -1 in eight slots, a lone 1 on dead_eye.
        var m04 = AiSkills.LoadRoster(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "M04"));
        Assert.Contains(m04, b =>
        {
            var s = AiSkills.RosterSkills(b.Fields);
            return s.DeadEye == 1 && s.QuickDraw == null && s.Talker == null;
        });
    }
}
