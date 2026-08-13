using System;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The combat-voice resolver chain (<c>docs/formats/combat-voice.md</c>): accentID →
/// <c>voice.zrd</c> pool → pilot VO id → clip defs / the shipped <c>_random</c> variant groups.
/// Input is <c>fixtures/voice/</c> (a two-pilot sounds.json, a four-row accent table, and a
/// mission dir whose aiv roster has one full-width, one short and one unset-accent block).
/// </summary>
public class CombatVoiceTests
{
    private static string FixtureDir => TestData.Fixture("voice");

    private static string MissionDir => TestData.Fixture("voice", "mission");

    [Fact]
    public void TheAccentTableLoadsPoolsAndSingles()
    {
        var accents = CombatVoice.LoadAccents(FixtureDir);
        Assert.Equal(4, accents.Count);
        Assert.Equal(new[] { 1, 2, 99 }, accents[0]);
        Assert.Equal(new[] { 2 }, accents[12]);
    }

    [Fact]
    public void OnlyIdShapedDefsCountAsClips()
    {
        var voice = Voice();
        Assert.Equal(2, voice.PilotIds.Count);   // snd_not_voice is no pilot clip
        Assert.Contains(1, voice.PilotIds);
        Assert.Contains(2, voice.PilotIds);
    }

    [Fact]
    public void PilotForSkipsPoolIdsWithoutClips()
    {
        var voice = Voice();
        // Accent 0's pool is {1, 2, 99}; 99 has no clip defs and is never picked.
        var rng = new Random(1);
        for (int i = 0; i < 20; i++)
        {
            int? id = voice.PilotFor(0, rng);
            Assert.True(id is 1 or 2, $"picked {id}");
        }
        // Accent 1's pool is only the clipless 99.
        Assert.Null(voice.PilotFor(1, rng));
        // An eligibility filter narrows further.
        Assert.Equal(2, voice.PilotFor(0, rng, id => id == 2));
    }

    [Fact]
    public void PlayableForPrefersTheShippedRandomGroup()
    {
        var voice = Voice();
        Assert.Equal("snd_DI-LowDmg-A_id1_random", voice.PlayableFor(1, "DI-LowDmg"));
        Assert.Equal("snd_DA-Bail-A_id1_random", voice.PlayableFor(1, "DA-Bail"));
    }

    [Fact]
    public void PlayableForFallsBackToTheBareDefForVariantlessTokens()
    {
        var voice = Voice();
        // The bearing call-outs have no -A variant and no group; trigger id 6 is WA-Enemy-3H.
        Assert.Equal("snd_id2_WA-Enemy-3H", voice.PlayableForTrigger(2, 6));
        Assert.Equal("snd_id2_WA-Enemy-3", voice.PlayableFor(2, "WA-Enemy-3"));
        Assert.Null(voice.PlayableFor(1, "WA-Enemy-3H"));     // id1 has no bearing clips
        Assert.Null(voice.PlayableFor(2, "TA-SucShk"));       // nobody has taunts here
    }

    [Fact]
    public void TheTwoDangerZoneSpellingsResolveEitherWay()
    {
        var voice = Voice();
        // id2 authors only the long spelling; id1 only the short one.
        Assert.Equal("snd_PR-DangerZone-A_id2_random", voice.PlayableFor(2, "PR-DngrZn"));
        Assert.Equal("snd_id1_PR-DngrZn-A", voice.ClipsFor(1, "PR-DangerZone")[0]);
    }

    [Fact]
    public void ClipsForMatchesTheFamilyRootAndItsVariants()
    {
        var voice = Voice();
        Assert.Equal(3, voice.ClipsFor(1, "DA").Count);          // Bail-A/B + NoBail-A
        Assert.Equal(2, voice.ClipsFor(1, "DA-Bail").Count);
        Assert.Single(voice.ClipsFor(1, "DA-NoBail"));
        Assert.Empty(voice.ClipsFor(1, "DA-Bail-A-X"));
        Assert.Single(voice.ClipsFor(2, "WA-Enemy-3"));          // exact token, not -3H too
    }

    [Fact]
    public void MissionAccentIdsReadsSlot65AndToleratesShortBlocks()
    {
        // One 68-field block (accent 12), one 43-field block (no slot 65), one accent -1.
        Assert.Equal(new[] { 12 }, CombatVoice.MissionAccentIds(MissionDir));
    }

    [Fact]
    public void AMissingRosterYieldsNoAccentsAndNoPrewarm()
    {
        Assert.Empty(CombatVoice.MissionAccentIds(TestData.Fixture("voice", "no-such-mission")));
        Assert.Empty(CombatVoice.SessionPrewarmNames(FixtureDir,
            TestData.Fixture("voice", "no-such-mission"),
            SoundDefs.Load(FixtureDir), SoundDefs.LoadGroups(FixtureDir)));
    }

    [Fact]
    public void PrewarmNamesIsTheDedupedUnionOfThePoolsClipDefs()
    {
        var voice = Voice();
        var names = voice.PrewarmNames(new[] { 0, 0, 12 });   // pools {1,2,99} and {2}, overlapping
        Assert.Equal(10, names.Count);                        // id1's 6 defs + id2's 4, once each
        Assert.Contains("snd_id1_DA-NoBail-A", names);
        Assert.Contains("snd_id2_PR-DangerZone-B", names);
        Assert.DoesNotContain("snd_not_voice", names);
    }

    [Fact]
    public void SessionPrewarmNamesResolvesTheMissionRoster()
    {
        // The mission's one authored accent is 12 → VO id 2 → its 4 clip defs.
        var names = CombatVoice.SessionPrewarmNames(FixtureDir, MissionDir,
            SoundDefs.Load(FixtureDir), SoundDefs.LoadGroups(FixtureDir));
        Assert.Equal(4, names.Count);
        Assert.Contains("snd_id2_WA-Enemy-3H", names);
    }

    [Fact]
    public void TheTriggerTableHasTwentyNineFamiliesWithComputedBearings()
    {
        Assert.Equal(29, CombatVoice.TriggerFamilies.Count);
        // id = 1 + 3*bearing + altitudeBand, ordered L / level / H within 12, 3, 6, 9 o'clock.
        Assert.Equal("WA-Enemy-12L", CombatVoice.TriggerFamilies[1]);
        Assert.Equal("WA-Enemy-3H", CombatVoice.TriggerFamilies[6]);
        Assert.Equal("WA-Enemy-9", CombatVoice.TriggerFamilies[11]);
        Assert.Equal("DA", CombatVoice.TriggerFamilies[20]);
        Assert.Equal("DS-Ally", CombatVoice.TriggerFamilies[28]);
    }

    [Fact]
    public void ClipNameParsingRejectsNonVoiceShapes()
    {
        Assert.True(CombatVoice.TryParseClipName("snd_id12_TA-FailShk-A", out int id, out string type));
        Assert.Equal(12, id);
        Assert.Equal("TA-FailShk-A", type);
        Assert.False(CombatVoice.TryParseClipName("snd_idle_engine", out _, out _));
        Assert.False(CombatVoice.TryParseClipName("snd_id12", out _, out _));
        Assert.False(CombatVoice.TryParseClipName("snd_exp_hit1", out _, out _));
    }

    private static CombatVoice Voice() => new(
        SoundDefs.Load(FixtureDir),
        SoundDefs.LoadGroups(FixtureDir),
        CombatVoice.LoadAccents(FixtureDir));
}
