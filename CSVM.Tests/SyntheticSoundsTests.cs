using System;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The synthetic tree's sound archive and the WAV encoder behind it. Every definition the stand-in's
/// records name resolves. Every WAV decodes through the engine's reader as the MS ADPCM shape
/// <c>docs/formats/sounds.md</c> describes. The encoder round-trips a signal through that reader.
/// Playback needs Godot, so the in-engine run covers that half.
/// </summary>
public class SyntheticSoundsTests
{
    [Fact]
    public void EveryDefinitionDecodesAsMonoAdpcmAtTheArchiveRate()
    {
        string extracted = Path.Combine(Built(), "extracted");
        var defs = SoundDefs.Load(Path.Combine(extracted, "zrdr"));
        using var archive = new SoundArchive(Path.Combine(extracted, "soundsh"));

        Assert.NotEmpty(defs);
        foreach (var def in defs.Values)
        {
            byte[]? bytes = archive.ReadWavBytes(def.WavName);
            Assert.NotNull(bytes);
            Assert.Equal(2, BitConverter.ToUInt16(bytes, 20));
            var wav = WavFile.Parse(bytes);
            Assert.Equal(1, wav.Channels);
            Assert.Equal(SyntheticSounds.Rate, wav.SampleRate);
            Assert.True(wav.Frames > SyntheticSounds.Rate / 20, $"{def.WavName} decodes {wav.Frames} frames");
        }
    }

    [Fact]
    public void EveryGroupMemberIsADefinition()
    {
        string zrdr = Path.Combine(Built(), "extracted", "zrdr");
        var defs = SoundDefs.Load(zrdr);
        var groups = SoundDefs.LoadGroups(zrdr);

        Assert.NotEmpty(groups);
        Assert.All(groups.Values, g => Assert.All(g.Members, m => Assert.True(defs.ContainsKey(m.Name), m.Name)));
    }

    [Fact]
    public void ThePlaneAndItsWeaponsNameSoundsTheArchiveHolds()
    {
        string zrdr = Path.Combine(Built(), "extracted", "zrdr");
        var defs = SoundDefs.Load(zrdr);
        var groups = SoundDefs.LoadGroups(zrdr);
        var stats = PlaneStats.Load(zrdr, SyntheticPlane.Plane);

        foreach (string? name in new[] { stats.EngineSound, stats.CockpitEngineSound, stats.DamagedEngineSound, stats.RattleSound })
        {
            Assert.NotNull(name);
            Assert.True(defs.ContainsKey(name), name);
        }

        // The engine loop is pitched and the cockpit and damaged loops are not, the split the
        // format page decodes for the shipped definitions.
        Assert.True(defs[stats.EngineSound].Frequency);
        Assert.False(defs[stats.CockpitEngineSound!].Frequency);
        Assert.False(defs[stats.DamagedEngineSound!].Frequency);
        Assert.Contains(stats.WarningShotSound, groups.Keys);
        Assert.Contains(stats.BulletHitSound, groups.Keys);

        var weapons = WeaponDefs.Load(zrdr, null);
        Assert.Contains(weapons.EmptyClipSound, defs.Keys);
        var cues = weapons.SoundCues();
        Assert.Contains(cues, c => c.Looped);
        Assert.All(cues, c => Assert.True(defs.ContainsKey(c.Name), c.Name));
    }

    [Fact]
    public void EveryAccentDealsAPilotWhoseWholeSetResolves()
    {
        string zrdr = Path.Combine(Built(), "extracted", "zrdr");
        var defs = SoundDefs.Load(zrdr);
        var voice = new CombatVoice(defs, SoundDefs.LoadGroups(zrdr), CombatVoice.LoadAccents(zrdr));

        Assert.NotEmpty(voice.AccentIds);
        var pilots = voice.AccentIds.SelectMany(voice.Pool).Distinct().ToList();

        // The Player Information voices the network suites seat are dealt by an accent too.
        Assert.Contains(PilotVoices.SpeakerFor(PilotVoices.Wire(1))!.Value, pilots);
        Assert.Contains(PilotVoices.SpeakerFor(PilotVoices.Wire(5))!.Value, pilots);
        foreach (int pilot in pilots)
        {
            // The bearings are a per-pilot capability, and DA/DE resolve below the family root.
            foreach (string family in CombatVoice.TriggerFamilies.Where(f => !f.StartsWith("WA-Enemy-") && f is not ("DA" or "DE")))
            {
                Assert.True(voice.PlayableFor(pilot, family) != null, $"VO id {pilot} speaks {family}");
            }

            Assert.Equal(4, voice.ClipsFor(pilot, "DA").Count + voice.ClipsFor(pilot, "DE").Count);
            Assert.EndsWith("_random", voice.PlayableFor(pilot, "DI-LowDmg"));
        }

        Assert.Contains(pilots, p => voice.PlayableForTrigger(p, 6) != null);
    }

    [Fact]
    public void AManifestMissingAWavTheDefinitionsNameFailsTheBuild()
    {
        string fixtures = TestData.TempDir();
        string sounds = Path.Combine(fixtures, "synthetic", "zrdr", "sounds.json");
        string manifest = Path.Combine(fixtures, "synthetic", "soundsh", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sounds)!);
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.Copy(TestData.Fixture("synthetic", "zrdr", "sounds.json"), sounds);
        File.WriteAllText(manifest, "{ \"wavs\": [ { \"name\": \"probe_engine.wav\", \"seconds\": 0.1, \"hz\": 110 } ] }");

        var tree = new SyntheticTree(fixtures, Path.Combine(TestData.TempDir(), "root"));
        var thrown = Assert.Throws<InvalidDataException>(() => SyntheticSounds.WriteArchive(tree));
        Assert.Contains("probe_gunloop.wav", thrown.Message);
    }

    [Fact]
    public void AdpcmRoundTripsATonePcmRoundTripsExactly()
    {
        short[] tone = SyntheticSounds.Signal("probe_round_trip", 0.25, 440);

        var adpcm = WavFile.Parse(WavWriter.MsAdpcm(tone, SyntheticSounds.Rate));
        Assert.Equal(tone.Length, adpcm.Frames);
        double signal = tone.Sum(s => (double)s * s);
        double noise = tone.Select((s, i) => (double)(s - adpcm.Samples[i]) * (s - adpcm.Samples[i])).Sum();

        // Four bits a sample on a smooth tone keeps the error some 20 dB under the signal.
        Assert.True(10 * Math.Log10(signal / noise) > 20, $"SNR {10 * Math.Log10(signal / noise):0.0} dB");

        var pcm = WavFile.Parse(WavWriter.Pcm16(tone, SyntheticSounds.Rate));
        Assert.Equal(tone, pcm.Samples);
    }

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies);
        return root;
    }
}
