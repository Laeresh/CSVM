using System;
using CSVM.Utils;

namespace CSVM.Flight.Audio;

/// <summary>
/// The engine duck under a voice line: one gain that every engine slot's written volume carries,
/// the pilot's own <see cref="FlightAudio"/> and every <see cref="Ai.AiEngineAudio"/> alike. It falls
/// while a queued radio line is on air and an engine's level stands above the authored limit. Once
/// the channel is quiet it climbs back to 1. It is the original's one global: each
/// in-range aircraft's engine update steps it again, so company moves it faster, and a spawn never
/// resets it. One instance serves a whole session, every splitscreen pane included.
/// Decode: docs/formats/vehicle.md, "A voice line ducks every engine".
/// </summary>
public sealed class EngineVoiceDuck
{
    /// <summary>The recovery rate as a fraction of the fall rate, the float at <c>0x00608b94</c>.
    /// </summary>
    public const float RecoveryFraction = 0.1667f;

    private readonly Func<bool> _voiceOnAir;
    private readonly Func<float> _sfxLevel;
    private bool _wasOnAir;

    /// <summary><paramref name="voiceOnAir"/> answers whether the radio has a line on air.
    /// <paramref name="sfxLevel"/> is the effects option's linear level, the original's
    /// <c>SfxVolume</c>'s counterpart, which the fall's compare carries.</summary>
    public EngineVoiceDuck(Func<bool> voiceOnAir, Func<float> sfxLevel)
    {
        _voiceOnAir = voiceOnAir;
        _sfxLevel = sfxLevel;
    }

    /// <summary>The gain an engine slot multiplies into its written volume this frame, 1 when no
    /// line has been heard.</summary>
    public float Gain { get; private set; } = 1f;

    /// <summary>One gain step with nothing held: the fall while <paramref name="voiceOnAir"/> and a
    /// slot's written level stands above <paramref name="limit"/>, floored at it, else the recovery
    /// toward 1. A level is the effects level times the gain times the slot's curve volume.</summary>
    public static float StepGain(float gain, float dt, float limit, bool voiceOnAir,
        float slot0Level, float slot1Level)
    {
        if (!voiceOnAir)
        {
            return gain < 1f ? Math.Min(1f, gain + ((1f - limit) * RecoveryFraction * dt)) : gain;
        }

        return slot0Level > limit || slot1Level > limit
            ? Math.Max(limit, gain - ((1f - limit) * dt))
            : gain;
    }

    /// <summary>One aircraft's step, taken after it has written its slots at <see cref="Gain"/>.
    /// The curve volumes are the throttle and whine curves' own outputs. The original's compare
    /// carries no definition VOLUME and no splitscreen mix gain, so neither is in them. A slot that is
    /// not sounding passes 0. An aircraft past the cull or with its engine out does not step.</summary>
    public void Step(float dt, float limit, float engineCurve, float whineCurve)
    {
        bool onAir = _voiceOnAir();
        float sfx = _sfxLevel();
        Gain = StepGain(Gain, dt, limit, onAir, sfx * Gain * engineCurve, sfx * Gain * whineCurve);
        if (onAir != _wasOnAir)
        {
            _wasOnAir = onAir;
            // The headless observable for a change nobody can screenshot: the edge, and where the
            // gain stood when it came.
            Log.Debug("sound", $"engine duck: voice {(onAir ? "on air" : "quiet")} gain={Gain:0.000} limit={limit:0.00} sfx={sfx:0.00}");
        }
    }
}
