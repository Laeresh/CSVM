using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Weapons;
using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Enhanced Sound and Hit Confirm below the engine. Original Sound's engine curve is the decoded one
/// bit for bit, and Enhanced Sound's is the shipped tuning's. The tuning file reads and overlays.
/// The two options resolve, persist and stage as their rows say. The live mix is the
/// <c>enhanced-sound-cues</c> suite's.
/// </summary>
public class EnhancedSoundTests
{
    private static readonly string ShippedPath = Path.Combine(TestData.RepoRoot, "CSVM", "data", "enhanced_sound.json");

    // The drives the curve checks walk: idle, part and full throttle, a hard pull, a climb, a dive
    // and the boost. The decoded terms riding the curves are covered as well as the lever.
    private static readonly EngineDrive[] Drives =
    {
        new(0f, 0f, 0f), new(0.1f, 0f, 0f), new(0.55f, 0f, 0f), new(1f, 0f, 0f),
        new(0.7f, 1.2f, 0f), new(0.7f, 0f, -0.5f), new(0.7f, 0f, 0.6f), new(0.4f, 0f, 0f, Boosting: true),
    };

    [Fact]
    public void OriginalSoundIsTheDecodedEngineCurve()
    {
        var stats = new PlaneStats();
        var idle = EngineAudioCurves.Engine(stats, new EngineDrive(0f, 0f, 0f), 1f, true);
        var full = EngineAudioCurves.Engine(stats, new EngineDrive(1f, 0f, 0f), 1f, true);
        Assert.Equal((0.6f, 1f), idle);
        Assert.Equal((1f, 1f), full);
    }

    [Fact]
    public void TheNeutralTuningComputesExactlyWhatOriginalSoundDoes()
    {
        var stats = new PlaneStats();
        foreach (var drive in Drives)
        {
            foreach (bool pitchable in new[] { true, false })
            {
                Assert.Equal(
                    EngineAudioCurves.Engine(stats, drive, 1.1f, pitchable),
                    EngineAudioCurves.Engine(stats, drive, 1.1f, pitchable, EnhancedSoundTuning.Neutral));
            }
        }
    }

    [Fact]
    public void EnhancedSoundWidensThePitchSpanAndRaisesTheGainWithThrottle()
    {
        var stats = new PlaneStats();
        var tuning = Shipped();
        var idle = EngineAudioCurves.Engine(stats, new EngineDrive(0f, 0f, 0f), 1f, true, tuning);
        var full = EngineAudioCurves.Engine(stats, new EngineDrive(1f, 0f, 0f), 1f, true, tuning);
        Assert.Equal(tuning.EnginePitchIdle, idle.Pitch);
        Assert.Equal(tuning.EnginePitchFull, full.Pitch);
        Assert.Equal(tuning.EngineGainIdle, idle.Volume);
        Assert.Equal(tuning.EngineGainFull, full.Volume);
        Assert.True(full.Pitch - idle.Pitch > 1f - 0.6f, $"span {idle.Pitch}..{full.Pitch} is wider than the decoded 0.6..1");
        Assert.True(idle.Volume < full.Volume, $"gain {idle.Volume}..{full.Volume} rises with throttle");

        // Half way between the decoded throttle points sits half way along each curve.
        float mid = (stats.EnginePitch.MinX + stats.EnginePitch.MaxX) / 2f;
        var half = EngineAudioCurves.Engine(stats, new EngineDrive(mid, 0f, 0f), 1f, true, tuning);
        Assert.Equal((tuning.EnginePitchIdle + tuning.EnginePitchFull) / 2f, half.Pitch, 4);
        Assert.Equal((tuning.EngineGainIdle + tuning.EngineGainFull) / 2f, half.Volume, 4);
    }

    [Fact]
    public void TheDecodedTermsRideTheEnhancedCurves()
    {
        var stats = new PlaneStats();
        var tuning = Shipped();
        var level = EngineAudioCurves.Engine(stats, new EngineDrive(0.5f, 0f, 0f), 1f, true, tuning);
        var pull = EngineAudioCurves.Engine(stats, new EngineDrive(0.5f, 1f, 0f), 1f, true, tuning);
        var boost = EngineAudioCurves.Engine(stats, new EngineDrive(0f, 0f, 0f, Boosting: true), 1f, true, tuning);
        Assert.True(pull.Pitch > level.Pitch && pull.Volume > level.Volume, "a hard pull lifts both");
        Assert.True(boost.Pitch > tuning.EnginePitchFull && boost.Volume > tuning.EngineGainFull,
            $"the boost pins past full ({boost.Pitch}, {boost.Volume})");
        var flat = EngineAudioCurves.Engine(stats, new EngineDrive(1f, 0f, 0f), 1f, false, tuning);
        Assert.Equal(1f, flat.Pitch);
    }

    [Fact]
    public void TheShippedFileNamesEveryAmountAndMakesEachHitLouder()
    {
        var tuning = Shipped();
        var text = File.ReadAllText(ShippedPath);
        foreach (var field in new[]
                 {
                     "enginePitchIdle", "enginePitchFull", "engineGainIdle", "engineGainFull", "bulletHitGain",
                     "canopyHitGain", "hitConfirmSound", "hitConfirmGain", "hitConfirmPitch", "hitConfirmInterval",
                 })
        {
            Assert.Contains($"\"{field}\"", text);
        }

        Assert.True(tuning.BulletHitGain > 1f && tuning.CanopyHitGain > 1f);
        Assert.True(tuning.HitConfirmInterval > 0f);
        Assert.Equal("bullet_hit_sg", tuning.HitConfirmSound);
    }

    [Fact]
    public void AFieldLeftOutOrOutOfRangeKeepsTheBaseline()
    {
        var problems = new List<string>();
        var baseline = EnhancedSoundTuning.Neutral with { BulletHitGain = 2f, HitConfirmPitch = 1.5f };
        var read = EnhancedSoundTuning.Parse(
            Encoding.UTF8.GetBytes("{ \"enginePitchFull\": 1.3, \"canopyHitGain\": 99, \"hitConfirmSound\": 4 }"),
            problems, baseline);
        Assert.Equal(1.3f, read.EnginePitchFull);
        Assert.Equal(2f, read.BulletHitGain);
        Assert.Equal(1.5f, read.HitConfirmPitch);
        Assert.Equal(1f, read.CanopyHitGain);
        Assert.Equal("bullet_hit_sg", read.HitConfirmSound);
        Assert.Equal(2, problems.Count);

        problems.Clear();
        Assert.Equal(EnhancedSoundTuning.Neutral, EnhancedSoundTuning.Parse(Encoding.UTF8.GetBytes("[]"), problems));
        Assert.Single(problems);
    }

    [Fact]
    public void LoadReadsAFileOverItsBaselineAndAMissingFileIsTheBaseline()
    {
        string dir = Path.Combine(Path.GetTempPath(), "csvm-enhanced-sound-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "override.json");
            File.WriteAllText(file, "{ \"hitConfirmGain\": 0.5 }");
            var shipped = Shipped();
            var overlaid = EnhancedSoundTuning.Load(file, shipped);
            Assert.Equal(shipped with { HitConfirmGain = 0.5f }, overlaid);
            Assert.Same(shipped, EnhancedSoundTuning.Load(Path.Combine(dir, "absent.json"), shipped));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TheSoundModeReadsEnhancedUntilSetToOriginal()
    {
        Assert.True(SoundModeSetting.IsEnhanced(null));
        Assert.True(SoundModeSetting.IsEnhanced(SoundModeSetting.Enhanced));
        Assert.False(SoundModeSetting.IsEnhanced(SoundModeSetting.Original));
        Assert.True(SoundModeSetting.IsEnhanced("louder"));
        Assert.Equal(SoundModeSetting.Enhanced, SoundModeSetting.Default);
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void HitConfirmNeverSetFollowsTheSoundMode(bool? saved, bool enhanced, bool expected)
    {
        Assert.Equal(expected, SoundModeSetting.HitConfirmOf(saved, enhanced));
    }

    [Fact]
    public void ApplySetsBothFlagsAndADeterministicRunHearsOriginalSound()
    {
        bool wasEnhanced = SoundModeSetting.EnhancedOn, wasConfirm = SoundModeSetting.HitConfirmOn;
        try
        {
            SoundModeSetting.Apply(null, null);
            Assert.True(SoundModeSetting.EnhancedOn && SoundModeSetting.HitConfirmOn);
            SoundModeSetting.Apply(SoundModeSetting.Original, null);
            Assert.False(SoundModeSetting.EnhancedOn || SoundModeSetting.HitConfirmOn);
            SoundModeSetting.Apply(SoundModeSetting.Original, true);
            Assert.True(!SoundModeSetting.EnhancedOn && SoundModeSetting.HitConfirmOn);
            SoundModeSetting.ApplySaved(det: true);
            Assert.False(SoundModeSetting.EnhancedOn || SoundModeSetting.HitConfirmOn);
        }
        finally
        {
            SoundModeSetting.EnhancedOn = wasEnhanced;
            SoundModeSetting.HitConfirmOn = wasConfirm;
        }
    }

    [Fact]
    public void AHitConfirmIsEarnedByAHumanPilotsGunRoundOnAHostileAircraftAlone()
    {
        int own = AimAssist.TeamOfPilot(0), enemy = own + 1;
        Assert.True(HitConfirmCue.Earns(true, true, own, enemy));
        Assert.False(HitConfirmCue.Earns(true, true, own, own));
        Assert.False(HitConfirmCue.Earns(true, true, own, AimAssist.NeutralTeam));
        Assert.False(HitConfirmCue.Earns(false, true, own, enemy));
        Assert.False(HitConfirmCue.Earns(true, false, own, enemy));
    }

    [Fact]
    public void TheHitConfirmSoundsAtMostOncePerInterval()
    {
        var cue = new HitConfirmCue();
        Assert.True(cue.TryFire(0.0, 0.1f));
        Assert.False(cue.TryFire(0.0, 0.1f));
        Assert.False(cue.TryFire(0.05, 0.1f));
        Assert.True(cue.TryFire(0.15, 0.1f));
        Assert.False(cue.TryFire(0.2, 0.1f));
        Assert.True(cue.TryFire(0.5, 0.1f));
    }

    [Fact]
    public void BothOptionsPersistAndAnUnknownModeReadsAsNeverSet()
    {
        var def = new OptionsDef { SoundMode = SoundModeSetting.Original, HitConfirm = true };
        var back = OptionsStore.Deserialize(OptionsStore.Serialize(def))!;
        Assert.Equal(SoundModeSetting.Original, back.SoundMode);
        Assert.True(back.HitConfirm);

        var never = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef()))!;
        Assert.Null(never.SoundMode);
        Assert.Null(never.HitConfirm);

        var odd = OptionsStore.Deserialize("{ \"version\": 1, \"soundMode\": \"loud\", \"hitConfirm\": \"yes\" }")!;
        Assert.Null(odd.SoundMode);
        Assert.Null(odd.HitConfirm);
    }

    [Fact]
    public void TheStagedRowsReadTheirDefaultsAndRideTheApply()
    {
        var choices = new OptionsChoices();
        choices.Load(null);
        Assert.True(choices.SoundEnhancedOn && choices.HitConfirmOn);
        choices.SoundMode = SoundModeSetting.Original;
        Assert.False(choices.SoundEnhancedOn || choices.HitConfirmOn);
        choices.HitConfirm = true;
        var exit = choices.ToExit();
        Assert.Equal(SoundModeSetting.Original, exit.SoundMode);
        Assert.True(exit.HitConfirm);

        choices.Load(new OptionsDef { SoundMode = SoundModeSetting.Enhanced, HitConfirm = false });
        Assert.True(choices.SoundEnhancedOn);
        Assert.False(choices.HitConfirmOn);
    }

    private static EnhancedSoundTuning Shipped()
    {
        var problems = new List<string>();
        var tuning = EnhancedSoundTuning.Parse(File.ReadAllBytes(ShippedPath), problems);
        Assert.Empty(problems);
        return tuning;
    }
}
