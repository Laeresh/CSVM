using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The sounds.json front-end (<c>docs/formats/sounds.md</c>): the <c>SETS</c> flag/value grammar
/// and the <c>SOUND_GROUPS</c> weighted picker, including its <c>DYNAMIC_WEIGHTS</c> recency
/// memory. Input is <c>fixtures/zrdr/sounds.json</c>.
/// </summary>
public class SoundDefsTests
{
    private static string FixtureDir => TestData.Fixture("zrdr");

    [Fact]
    public void EverySetsBlockContributesItsEntries()
    {
        var defs = Defs();
        Assert.Equal(4, defs.Count);
        Assert.Contains("snd_probe_second", defs.Keys); // from the second SETS block
    }

    [Fact]
    public void BareFlagsAndValueKeysAreReadFromOneEntry()
    {
        var def = Defs()["snd_probe_loop"];
        Assert.Equal("probe_loop.wav", def.WavName);
        Assert.True(def.Looped);
        Assert.True(def.Is3D);
        Assert.False(def.Frequency);
        Assert.Equal(50f, def.RangeMin);
        Assert.Equal(400f, def.RangeMax);
        Assert.Equal(0.75f, def.Volume);
    }

    [Fact]
    public void AnEntryWithNoFlagsKeepsTheDefaults()
    {
        var def = Defs()["snd_probe_oneshot"];
        Assert.False(def.Looped);
        Assert.False(def.Is3D);
        Assert.Equal(130f, def.RangeMin);
        Assert.Equal(1020f, def.RangeMax);
        Assert.Equal(1f, def.Volume);
    }

    [Fact]
    public void UnmappedFlagsAreIgnoredWithoutDisturbingTheOnesAfterThem()
    {
        // SFX / PURGEABLE carry no behaviour here; FREQUENCY sits before them.
        var def = Defs()["snd_probe_pitched"];
        Assert.True(def.Frequency);
        Assert.False(def.Looped);
    }

    [Fact]
    public void LookupIsCaseInsensitive()
    {
        Assert.True(Defs().ContainsKey("SND_PROBE_LOOP"));
    }

    [Fact]
    public void GroupMembersDefaultToWeightOneAndExplicitWeightsAreRead()
    {
        var groups = Groups();
        Assert.Equal(new[] { 1f, 1f }, Weights(groups["probe_plain_sg"]));
        Assert.Equal(new[] { 0f, 1f }, Weights(groups["probe_weighted_sg"]));
    }

    [Fact]
    public void ACategoryTokenIsNotMistakenForAMember()
    {
        // "MUSIC" is a marker; its members still follow.
        var group = Groups()["probe_music_sg"];
        Assert.Single(group.Members);
        Assert.Equal("snd_probe_oneshot", group.Members[0].Name);
    }

    [Fact]
    public void ADialogueChainIsKeptAsAnOrderedLineListWithNoWeightedMember()
    {
        var group = Groups()["probe_dialogue_sg"];
        Assert.Empty(group.Members);
        var chain = Assert.Single(group.Chains);
        Assert.Equal(new[] { "snd_probe_oneshot", "snd_probe_second", "snd_probe_loop" }, chain);
    }

    [Fact]
    public void AGroupWithNeitherMembersNorChainsIsNotRegistered()
    {
        Assert.DoesNotContain("probe_empty_sg", Groups().Keys);
    }

    [Fact]
    public void AZeroWeightMemberIsNeverPicked()
    {
        var group = Groups()["probe_weighted_sg"];
        var rng = new Random(1);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal("snd_probe_second", group.Pick(rng));
        }
    }

    [Fact]
    public void DynamicWeightsScalesDownTheMemberPickedLast()
    {
        // Equal weights, recency 0.5. The same roll (0.6 of the total) picks member 1 first
        // — total 2.0, roll 1.2 falls past member 0 — and then member 0, because member 1's
        // weight has halved: total 1.5, roll 0.9 no longer clears member 0's full 1.0.
        var dynamic = Groups()["probe_dynamic_sg"];
        Assert.Equal(0.5f, dynamic.RecencyFactor);
        Assert.Equal("snd_probe_second", dynamic.Pick(new ConstantRandom(0.6)));
        Assert.Equal("snd_probe_oneshot", dynamic.Pick(new ConstantRandom(0.6)));

        // The control: without DYNAMIC_WEIGHTS the identical rolls repeat the same member.
        var plain = Groups()["probe_plain_sg"];
        Assert.Equal(1f, plain.RecencyFactor);
        Assert.Equal("snd_probe_second", plain.Pick(new ConstantRandom(0.6)));
        Assert.Equal("snd_probe_second", plain.Pick(new ConstantRandom(0.6)));
    }

    private static Dictionary<string, SoundDef> Defs() => SoundDefs.Load(FixtureDir);

    private static Dictionary<string, SoundGroup> Groups() => SoundDefs.LoadGroups(FixtureDir);

    private static float[] Weights(SoundGroup group)
    {
        var weights = new float[group.Members.Count];
        for (int i = 0; i < group.Members.Count; i++)
        {
            weights[i] = group.Members[i].Weight;
        }
        return weights;
    }

    /// <summary>A die that always shows the same face, so a pick is a statement about the
    /// weights rather than about the RNG.</summary>
    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value) => _value = value;

        public override double NextDouble() => _value;
    }
}
