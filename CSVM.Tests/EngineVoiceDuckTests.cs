using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine voice duck's stepping, against the decode in docs/formats/vehicle.md, "A voice line
/// ducks every engine". The fall runs at (1 - L)/s, gated on a written level above L and floored
/// at L. The recovery runs at (1 - L) x 0.1667/s, capped at 1. Several aircraft share one gain.
/// The live radio and engine voices are the engine suite <c>engine-voice-duck</c>.
/// </summary>
public class EngineVoiceDuckTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void FallsAtOneMinusLimitPerSecondWhileALevelStandsAboveTheLimit()
    {
        float next = EngineVoiceDuck.StepGain(1f, 0.1f, 0.4f, voiceOnAir: true, 1f, 0f);
        Assert.Equal(1f - (0.6f * 0.1f), next, 6);
    }

    [Fact]
    public void TheFallIsFlooredAtTheLimit()
    {
        Assert.Equal(0.4f, EngineVoiceDuck.StepGain(0.41f, 0.1f, 0.4f, true, 0.41f, 0f));
    }

    [Fact]
    public void NoFallWhenNeitherSlotIsAboveTheLimit()
    {
        // The compare is strict: a level equal to the limit holds the gain where it is.
        Assert.Equal(0.9f, EngineVoiceDuck.StepGain(0.9f, 0.1f, 0.4f, true, 0.4f, 0.4f));
        Assert.Equal(0.9f, EngineVoiceDuck.StepGain(0.9f, 0.1f, 0.4f, true, 0f, 0f));
    }

    [Fact]
    public void EitherSlotAboveTheLimitIsEnoughToFall()
    {
        Assert.True(EngineVoiceDuck.StepGain(1f, 0.1f, 0.4f, true, 0f, 0.5f) < 1f);
    }

    [Fact]
    public void RecoversAtOneSixthOfTheFallRateAndCapsAtOne()
    {
        float next = EngineVoiceDuck.StepGain(0.4f, 1f, 0.4f, voiceOnAir: false, 0f, 0f);
        Assert.Equal(0.4f + (0.6f * EngineVoiceDuck.RecoveryFraction), next, 6);
        Assert.Equal(1f, EngineVoiceDuck.StepGain(0.99f, 1f, 0.4f, false, 0f, 0f));
        Assert.Equal(1f, EngineVoiceDuck.StepGain(1f, 1f, 0.4f, false, 0f, 0f));
    }

    [Fact]
    public void RecoveryIgnoresTheSlotLevels()
    {
        Assert.Equal(EngineVoiceDuck.StepGain(0.5f, Dt, 0.4f, false, 0f, 0f),
            EngineVoiceDuck.StepGain(0.5f, Dt, 0.4f, false, 1f, 1f));
    }

    [Fact]
    public void FullDuckAndRecoveryTakeTheDecodedTimesForOneAircraft()
    {
        var duck = new EngineVoiceDuck(() => true, () => 1f);
        int down = 0;
        while (duck.Gain > 0.4f && down < 1000)
        {
            duck.Step(Dt, 0.4f, 1f, 0f);
            down++;
        }
        Assert.InRange(down * Dt, 0.99f, 1.02f);

        bool onAir = false;
        var quiet = new EngineVoiceDuck(() => onAir, () => 1f);
        onAir = true;
        for (int i = 0; i < 120; i++)
            quiet.Step(Dt, 0.4f, 1f, 0f);
        onAir = false;
        int up = 0;
        while (quiet.Gain < 1f && up < 10000)
        {
            quiet.Step(Dt, 0.4f, 1f, 0f);
            up++;
        }
        Assert.InRange(up * Dt, 5.98f, 6.02f);
    }

    [Fact]
    public void TheWrittenLevelCarriesTheEffectsLevelAndTheGainItself()
    {
        // At the shipped effects level 0.5 and the flat curve 1.0 the level is 0.5 x gain. The
        // fall stops once the gain is at or below 0.4 / 0.5 = 0.8.
        var duck = new EngineVoiceDuck(() => true, () => 0.5f);
        for (int i = 0; i < 600; i++)
            duck.Step(Dt, 0.4f, 1f, 0f);
        Assert.InRange(duck.Gain, 0.8f - (0.6f * Dt), 0.8f);

        // At or below the limit from the start, nothing moves.
        var low = new EngineVoiceDuck(() => true, () => 0.4f);
        for (int i = 0; i < 600; i++)
            low.Step(Dt, 0.4f, 1f, 0f);
        Assert.Equal(1f, low.Gain);
    }

    [Fact]
    public void EveryAircraftThatStepsMovesTheOneSharedGain()
    {
        var duck = new EngineVoiceDuck(() => true, () => 1f);
        for (int frame = 0; frame < 15; frame++)
        {
            duck.Step(Dt, 0.4f, 1f, 0f);   // the pilot's own engine
            duck.Step(Dt, 0.4f, 1f, 0f);   // one AI engine in range
        }
        Assert.Equal(1f - (2f * 0.6f * 15 * Dt), duck.Gain, 4);
    }

    /// <summary>The install authors the limit at 0.4 in player.zrd, over the executable's compiled
    /// 0.5, and every airframe reads the same global.</summary>
    [ExtractedDataFact]
    public void TheInstallAuthorsTheLimitAtPointFour()
    {
        string zrdr = SessionPaths.PreferUnzipped(System.IO.Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));
        Assert.Equal(0.4f, PlaneStats.Load(zrdr, "player_bhawk").VoiceoverVolumeLimiter, 5);
        Assert.Equal(0.4f, PlaneStats.LoadForAi(zrdr, "player_fury").VoiceoverVolumeLimiter, 5);
    }
}
