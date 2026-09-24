using CSVM.Mech3;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The mix arithmetic (<see cref="AudioMix"/>): four 0..100 levels into one gain per category bus,
/// Master multiplying the other three, and the -80 dB floor that keeps a level of 0 a finite
/// volume. The apply half needs an <c>AudioServer</c> and is asserted by the `audio-buses` suite.
/// </summary>
public class AudioMixTests
{
    // Two levels of 1 multiply to exactly the floor, so the floor bites at and below that pair
    // and nowhere above it.
    private const float FloorGain = 0.0001f;

    [Fact]
    public void FullUnderFullIsTheRestingGain()
    {
        Assert.Equal(1f, AudioMix.Gain(AudioMix.MaxLevel, AudioMix.MaxLevel));
        Assert.Equal(0f, AudioMix.VolumeDb(AudioMix.MaxLevel, AudioMix.MaxLevel));
    }

    [Fact]
    public void ZeroIsSilentAndFinite()
    {
        Assert.Equal(0f, AudioMix.Gain(0, AudioMix.MaxLevel));
        float db = AudioMix.VolumeDb(0, AudioMix.MaxLevel);
        Assert.True(float.IsFinite(db), $"a level of 0 must not be negative infinity, got {db}");
        Assert.Equal(Mathf.LinearToDb(FloorGain), db, 3);
        Assert.Equal(-80f, db, 2);
        Assert.Equal(-80f, AudioMix.VolumeDb(AudioMix.MaxLevel, 0), 2);
    }

    [Fact]
    public void MasterHalvesEveryCategory()
    {
        Assert.Equal(0.5f, AudioMix.Gain(100, 50));
        Assert.Equal(0.25f, AudioMix.Gain(50, 50));
        Assert.Equal(0.125f, AudioMix.Gain(25, 50));
        foreach (int level in new[] { 25, 50, 100 })
        {
            float halved = AudioMix.VolumeDb(level, 50) - AudioMix.VolumeDb(level, 100);
            Assert.Equal(-6.0206f, halved, 3);
        }
    }

    [Fact]
    public void TheShippedDefaultsSitSixDbUnderTheRestingGain()
    {
        Assert.Equal(0.5f, AudioMix.Gain(AudioMix.DefaultMusic, AudioMix.DefaultMaster));
        Assert.Equal(-6.0206f, AudioMix.VolumeDb(AudioMix.DefaultMusic, AudioMix.DefaultMaster), 3);
        Assert.Equal(-6.0206f, AudioMix.VolumeDb(AudioMix.DefaultEffects, AudioMix.DefaultMaster), 3);
        Assert.Equal(-6.0206f, AudioMix.VolumeDb(AudioMix.DefaultVoice, AudioMix.DefaultMaster), 3);
    }

    [Fact]
    public void TheFloorBitesOnlyAtTheBottomPair()
    {
        Assert.Equal(FloorGain, AudioMix.Gain(1, 1), 7);
        Assert.Equal(-80f, AudioMix.VolumeDb(1, 1), 2);
        // A level of 1 at full master is the original's own far-left, which its authored MinValue
        // of 1 could reach and which the floor must leave alone.
        Assert.Equal(-40f, AudioMix.VolumeDb(1, AudioMix.MaxLevel), 3);
        Assert.Equal(-60f, AudioMix.VolumeDb(10, 1), 3);
    }

    // ⚠ The two builds convert a slider position on different curves, and the difference is the
    // whole measured asymmetry between their level paths, so it is pinned rather than left to be
    // rediscovered. The original's category level goes through the same ten-decibels-per-doubling
    // conversion its definition volumes use; a bus goes through Godot's 20 log10. Reading them as
    // interchangeable is what makes the remake look quieter than it is (docs/formats/sounds.md).
    [Fact]
    public void ACategoryBusIsNotTheOriginalsCategoryCurve()
    {
        Assert.Equal(-6.0206f, AudioMix.VolumeDb(AudioMix.DefaultEffects, AudioMix.DefaultMaster), 3);
        Assert.Equal(-10f, SoundFalloff.VolumeDb(0.5f), 3);
        float louder = AudioMix.VolumeDb(AudioMix.DefaultEffects, AudioMix.DefaultMaster)
            - SoundFalloff.VolumeDb(0.5f);
        Assert.Equal(3.9794f, louder, 3);
    }

    [Fact]
    public void LevelsClampInsteadOfWrapping()
    {
        Assert.Equal(AudioMix.Gain(0, 100), AudioMix.Gain(-1, 100));
        Assert.Equal(AudioMix.Gain(0, 100), AudioMix.Gain(int.MinValue, 100));
        Assert.Equal(1f, AudioMix.Gain(101, 100));
        Assert.Equal(1f, AudioMix.Gain(int.MaxValue, int.MaxValue));
        Assert.Equal(-80f, AudioMix.VolumeDb(-5, 100), 2);
    }
}
