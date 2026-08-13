using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Golden invariants for the combat-voice chain over the retail extraction (skipped without it):
/// the accent table, the per-pilot clip-def sets sounds.json ships, the <c>_random</c> variant
/// groups, the dialogue chains, and one mission's roster-accent scan. Numbers only, never content
/// (see <see cref="ExtractedGoldenTests"/> for the convention).
/// </summary>
public class CombatVoiceGoldenTests
{
    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void TheAccentTableHasThirtyFiveRowsPoolsThenSingles()
    {
        var accents = CombatVoice.LoadAccents(SharedZrdr);
        Assert.Equal(35, accents.Count);
        for (int row = 0; row < 35; row++)
        {
            Assert.True(accents.ContainsKey(row), $"accent row {row} missing");
            int size = accents[row].Length;
            // Rows 0-10 are 2-3-id pools; 11-34 map one accent to one pilot VO id.
            Assert.True(row <= 10 ? size is 2 or 3 : size == 1, $"row {row} pool size {size}");
        }
    }

    [ExtractedDataFact]
    public void SoundsJsonShipsThePilotClipDefSets()
    {
        var voice = Voice(out _, out _);
        // 35 per-pilot id sets in SETS, 1,414 snd_id<N>_* clip defs, no duplicates.
        Assert.Equal(35, voice.PilotIds.Count);
        Assert.Equal(1414, voice.AllClipNames().Count);
    }

    [ExtractedDataFact]
    public void EightPilotIdsCarryTheTwelveClipBearingSet()
    {
        // Def-side the full WA-Enemy set sits on 8 ids; on disk id 44's twelve WAVs are absent,
        // which is the 7-of-31 figure the clip survey reports. Def presence is not availability.
        var voice = Voice(out _, out _);
        var owners = new List<int>();
        foreach (int id in voice.PilotIds.OrderBy(i => i))
        {
            if (voice.ClipsFor(id, "WA-Enemy").Count == 12)
            {
                owners.Add(id);
            }
        }
        Assert.Equal(new[] { 2, 7, 24, 26, 29, 31, 44, 48 }, owners);
    }

    [ExtractedDataFact]
    public void TheVariantGroupsAndDialogueChainsAreAllRegistered()
    {
        var groups = SoundDefs.LoadGroups(SharedZrdr);
        // 710 SOUND_GROUPS entries: 222 single-chain dialogue groups + 488 weighted groups,
        // 466 of which are the per-pilot per-family snd_<FAMILY>-A_id<N>_random variant picks.
        Assert.Equal(710, groups.Count);
        Assert.Equal(222, groups.Values.Count(g => g.Chains.Count > 0));
        foreach (var g in groups.Values.Where(g => g.Chains.Count > 0))
        {
            Assert.Empty(g.Members);            // chains never mix with weighted members
            Assert.Single(g.Chains);            // and every chain group holds exactly one
            Assert.True(g.Chains[0].Count >= 2, $"{g.Name} chain of {g.Chains[0].Count}");
        }
        Assert.Equal(466, groups.Keys.Count(k =>
            k.StartsWith("snd_") && k.EndsWith("_random") && k.Contains("_id")));
    }

    [ExtractedDataFact]
    public void EveryAccentPoolResolvesAndTheDefOnlyIdsAreKnown()
    {
        var voice = Voice(out _, out _);
        var accents = CombatVoice.LoadAccents(SharedZrdr);
        var clipless = new SortedSet<int>();
        foreach (var pool in accents.Values)
        {
            foreach (int id in pool)
            {
                if (!voice.PilotIds.Contains(id))
                {
                    clipless.Add(id);
                }
            }
        }
        // Ids 5 and 40 are mapped by the accent table but ship neither defs nor WAVs; the
        // def-without-WAV ids (13, 15, 17, 35, 36) DO have defs and are not in this list.
        Assert.Equal(new[] { 5, 40 }, clipless);
    }

    [ExtractedDataFact]
    public void C1M04sRosterNamesItsSevenAccentsAndTheirPrewarmSet()
    {
        string mission = SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "M04");
        var accents = CombatVoice.MissionAccentIds(mission);
        Assert.Equal(new[] { 11, 12, 13, 14, 15, 16, 24 }, accents);
        var names = CombatVoice.SessionPrewarmNames(SharedZrdr, mission,
            SoundDefs.Load(SharedZrdr), SoundDefs.LoadGroups(SharedZrdr));
        Assert.Equal(283, names.Count);
    }

    [ExtractedDataFact]
    public void TheWorkedExampleChainResolvesEndToEnd()
    {
        var voice = Voice(out _, out var groups);
        // accent 12 (Jack/Ilsa per the survey) → the single-id pool {2} → id2's clips.
        Assert.Equal(new[] { 2 }, voice.Pool(12));
        Assert.Equal(2, voice.PilotFor(12, new System.Random(1)));
        string? playable = voice.PlayableFor(2, "DI-LowDmg");
        Assert.Equal("snd_DI-LowDmg-A_id2_random", playable);
        Assert.True(groups.ContainsKey(playable!));
        Assert.Equal("snd_id2_WA-Enemy-3H", voice.PlayableForTrigger(2, 6));
        // DA/DE resolve per sub-family; the root alone is deliberately not playable.
        Assert.Null(voice.PlayableForTrigger(2, 20));
        Assert.Equal("snd_DA-Bail-A_id2_random", voice.PlayableFor(2, "DA-Bail"));
    }

    private static CombatVoice Voice(out Dictionary<string, SoundDef> defs,
        out Dictionary<string, SoundGroup> groups)
    {
        defs = SoundDefs.Load(SharedZrdr);
        groups = SoundDefs.LoadGroups(SharedZrdr);
        return new CombatVoice(defs, groups, CombatVoice.LoadAccents(SharedZrdr));
    }
}
