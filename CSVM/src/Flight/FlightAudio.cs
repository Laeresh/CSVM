using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Own-plane sound, all from the player's own game data: the plane's engine loop
/// (per-plane WAV via vehicle.json 'engine_sound', throttle-driven pitch/volume, played as a
/// detuned dual voice), the overspeed whine (player.json 'prop_sound' curves —
/// silent until past fd_speed in a dive), the airframe rattle (player.json 'rattle' block),
/// plus the prop start/stop one-shots (snd_propstart/snd_propstop). Non-positional players:
/// these are what the pilot hears; positional 3D emitters are for other aircraft, later.
/// </summary>
public partial class FlightAudio : Node
{
    /// <summary>Overall gain for this plane's own-ship mix. 1 for single player;
    /// splitscreen sets 1/√N so N simultaneous engine stacks don't sum to a wall of noise
    /// (equal-power, so 2P ≈ −3 dB each, 4P ≈ −6 dB). TUNE — pending a real 4P listen.</summary>
    public float MixGain = 1f;

    // The prop_sound curve caps the whine at volume 0.5, but spectral analysis of the
    // user's reference video (Bloodhawk dive to ~1.27x fd_speed) bounds the original's whine at
    // 0.06-0.14 of the engine's amplitude — the reader volume is evidently not a linear mix gain
    // for this loop. 0.12 puts our saturated whine ~24 dB under the engine, at the bound. TUNE.
    internal const float WhineMixGain = 0.12f;

    // No reference recording exists for the damaged-engine loop the way the whine had a spectral
    // dive to measure against — 1.0 (the def's own sounds.json volume, unattenuated) is the
    // starting point until a real capture says otherwise. TUNE, Config-wired so it can move
    // without a rebuild.
    internal const float DamagedEngineMixGain = 1f;

    // The original plays its engine loop as a detuned dual stack: spectral analysis of the
    // reference dive recording found combs consistent with ~5% separation between two voices
    // (a Doppler reading of those same combs was refuted; the detune reading was not).
    // No exact ratio was measured, only "~5%", so
    // this is a TUNE seeded from that figure. Config-wired so it can move without a rebuild.
    internal const float EngineDetuneRatio = 0.05f;

    // Splitting one engine voice into two changes nothing about total loudness only if each
    // voice is attenuated to keep summed *power* (not amplitude) constant — the two voices are
    // near-identical waveforms a few percent apart in pitch, so they add closer to
    // uncorrelated (power) than correlated (amplitude) once they drift out of phase. 1/sqrt(2)
    // per voice keeps the pair at the same RMS power as today's single full-gain loop, the same
    // equal-power convention MixGain already uses for splitscreen.
    private const float EngineVoiceGain = 0.70710678f;

    private const float SilenceThreshold = 0.002f;
    // s for the loop to fade to full behind snd_propstart. Sourced from startprops' authored prop
    // cross-fade (plane_props.zrd.json, OBJECT_OPACITY_FROM_TO RUN_TIME 2.0 on staticpropN→propN)
    // — the nearest authored duration. TUNE pending a listen A/B.
    private const float EngineStartRamp = 2.0f;

    private readonly List<(string name, AudioStreamWav stream, float volume)> _crashSounds = new();

    private PlaneStats _stats = null!;
    private AudioStreamPlayer? _engine, _engine2, _whine, _rattle, _damagedEngine;
    private float _engineVol = 1f, _whineVol = 1f, _rattleVol = 1f, _damagedEngineVol = 1f; // sounds.json VOLUME base gain
    private AudioStreamPlayer? _crash;
    private AudioStreamPlayer? _groundExp, _waterExp;
    private float _groundExpVol = 1f, _waterExpVol = 1f;
    private AudioStreamPlayer? _grazeGround, _grazeWater;
    private float _grazeGroundVol = 1f, _grazeWaterVol = 1f;
    private AudioStreamPlayer? _propStart, _propStop;
    private float _propStartVol = 1f, _propStopVol = 1f;
    private float _engineRamp = 1f; // 0→1 gain envelope while the engine catches after a start

    // Gun firing: kept references so the looped firing sound + empty-clip cue can be built
    // on demand from any caliber's LOOPED_SOUND_NAME. Own-ship, non-positional (like the engine).
    private SoundArchive? _archive;
    private IReadOnlyDictionary<string, SoundDef>? _defs;
    private AudioStreamPlayer? _gunLoop;
    private string? _gunLoopName;
    private float _gunLoopVol = 1f;
    private AudioStreamPlayer? _emptyClip;
    private float _emptyClipVol = 1f;

    // The near-miss cue: warning_shot_sound names a SOUND_GROUPS entry
    // (bullet_warning_sg → snd_bulletpass1-3), so the variant is picked per pass through the
    // group's own weighted-recency draw rather than fixed at Setup. One player, restreamed —
    // two passes closer together than the wav is long is exactly what the interval prevents.
    private SoundGroup? _warningShotGroup;
    private AudioStreamPlayer? _warningShot;
    private System.Random? _warningShotRng;

    public void Setup(SoundArchive archive, Dictionary<string, SoundDef> defs, PlaneStats stats,
        IReadOnlyDictionary<string, SoundGroup>? groups = null)
    {
        _stats = stats;
        _archive = archive;
        _defs = defs;
        // The shared empty-clip cue (weapons.json NO_AMMO_WARNING = snd_emptyclip).
        _emptyClip = MakeOneShot(archive, defs, "snd_emptyclip", out _emptyClipVol);
        _engine = MakeLoop(archive, defs, stats.EngineSound, out _engineVol);
        // A second voice of the same loop, detuned a few percent off the first in Update,
        // reproduces the original's dual-stack chorus. Same def/stream, its own player so the two
        // voices run independent playback positions.
        _engine2 = MakeLoop(archive, defs, stats.EngineSound, out _);
        _whine = MakeLoop(archive, defs, stats.WhineSound, out _whineVol);
        _rattle = MakeLoop(archive, defs, stats.RattleSound, out _rattleVol);
        // damaged_engine_sound: a second engine loop blended in as the airframe takes damage.
        if (stats.DamagedEngineSound is { } damagedEngineSound)
            _damagedEngine = MakeLoop(archive, defs, damagedEngineSound, out _damagedEngineVol);

        // Prop start/stop one-shots (both non-looped, no VOLUME field → base gain 1.0):
        // snd_propstart plays as the engine ramps in on (re)spawn; snd_propstop is the
        // wind-down for a future landing/shutdown (see OnEngineStop).
        _propStart = MakeOneShot(archive, defs, "snd_propstart", out _propStartVol);
        _propStop = MakeOneShot(archive, defs, "snd_propstop", out _propStopVol);

        // Crash explosions: the game defines snd_exp_plane1..4 as a set; pick one at
        // random per crash, like the original.
        for (int i = 1; i <= 4; i++)
        {
            if (defs.TryGetValue($"snd_exp_plane{i}", out var def)
                && archive.Find(def.WavName, looped: false) is { } stream)
            {
                _crashSounds.Add((def.Name, stream, def.Volume));
            }
        }
        if (_crashSounds.Count > 0)
        {
            _crash = new AudioStreamPlayer();
            AddChild(_crash);
        }

        // The ground/dirt crash choreography layers snd_exp_ground_a — the
        // heavy earth-impact boom — over the plane_destroy_sg explosion above; the dirt anim
        // def fires it as its Sound event. The sea dive's counterpart is snd_exp_water_a, which
        // the water def does not carry directly: it sits in the plane_big_splash that
        // player_crash_water's destroy_crash calls. Both stay gated on the surface in
        // FlightController, not folded into OnCrash.
        _groundExp = MakeOneShot(archive, defs, "snd_exp_ground_a", out _groundExpVol);
        _waterExp = MakeOneShot(archive, defs, "snd_exp_water_a", out _waterExpVol);

        // The graze reaction's authored sounds (touchdown.zrd): the touchdown_default/_dirt
        // sequences Sound snd_exp_ground_b, touchdown_water snd_exp_water_b — the lighter `_b`
        // pair, not the crash's `_a`.
        _grazeGround = MakeOneShot(archive, defs, "snd_exp_ground_b", out _grazeGroundVol);
        _grazeWater = MakeOneShot(archive, defs, "snd_exp_water_b", out _grazeWaterVol);

        // The near-miss cue's group (player.json warning_shot_sound). Own-ship and non-positional
        // like everything else here: the def is 3D with RANGE [20,200], but a pass close enough to
        // trigger is well inside that inner radius, i.e. full volume — and in splitscreen only the
        // pilot who was nearly hit may hear it, which a world emitter on one shared listener cannot
        // do. A silent group (no such name, or no WAVs) simply leaves the cue unbuilt.
        if (groups != null && groups.TryGetValue(stats.WarningShotSound, out var warningGroup))
        {
            _warningShotGroup = warningGroup;
            _warningShotRng = Rng.NewSystemRandom(Rng.Weapons);
            _warningShot = new AudioStreamPlayer();
            AddChild(_warningShot);
        }
        else
        {
            GD.PushWarning($"sound group not found in sounds.json: {stats.WarningShotSound}");
        }
    }

    /// <summary>Start (or keep playing) the gun firing loop for the given <c>LOOPED_SOUND_NAME</c>.
    /// Rebuilds the player only when the sound changes (a different caliber group starts firing).</summary>
    public void StartGunLoop(string? sndName)
    {
        if (string.IsNullOrEmpty(sndName) || _archive == null || _defs == null)
        {
            return;
        }
        if (_gunLoopName != sndName)
        {
            _gunLoop?.Stop();
            _gunLoop = null;
            // Forward-loop the firing sound while the trigger is held, regardless of the def's own
            // LOOPED flag (it is a sustained-fire cue).
            if (_defs.TryGetValue(sndName, out var def) && _archive.Find(def.WavName, looped: true) is { } stream)
            {
                _gunLoop = new AudioStreamPlayer { Stream = stream };
                AddChild(_gunLoop);
                _gunLoopName = sndName;
                _gunLoopVol = def.Volume;
            }
        }
        if (_gunLoop is { Playing: false })
        {
            _gunLoop.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, _gunLoopVol * MixGain));
            _gunLoop.Play();
        }
    }

    public void StopGunLoop() => _gunLoop?.Stop();

    /// <summary>The empty-clip cue — one shot when a dry gun's trigger is pulled.</summary>
    public void PlayEmptyClip() => PlayOneShot(_emptyClip, _emptyClipVol * MixGain);

    public override void _Ready() => StartEngine(); // whine/rattle start on demand

    /// <summary>Per-frame drive: <paramref name="speedFrac"/> is speed / fd_speed,
    /// <paramref name="damageFrac"/> is accumulated damage (1 - PlaneDamage.WorstFraction, 0 when
    /// pristine). Not called while crashed, so the loops stay dead until respawn.</summary>
    public void Update(float dt, float throttle, float speedFrac, float damageFrac)
    {
        if (_engineRamp < 1f)
            _engineRamp = Mathf.Min(1f, _engineRamp + dt / EngineStartRamp);
        if (_engine != null)
        {
            if (!_engine.Playing)
                StartEngine(); // respawn after a crash: propstart + fresh volume ramp-in
            float pitch = Mathf.Max(0.01f, _stats.EnginePitch.Eval(throttle));
            float halfDetune = Config.GetFloat("flightAudio.engineDetuneRatio", EngineDetuneRatio) * 0.5f;
            float voiceGain = Mathf.Max(SilenceThreshold,
                _stats.EngineVolume.Eval(throttle) * _engineVol * _engineRamp * MixGain) * EngineVoiceGain;
            _engine.PitchScale = pitch * (1f - halfDetune);
            _engine.VolumeDb = Mathf.LinearToDb(voiceGain);
            if (_engine2 != null)
            {
                if (!_engine2.Playing)
                    _engine2.Play();
                _engine2.PitchScale = pitch * (1f + halfDetune);
                _engine2.VolumeDb = Mathf.LinearToDb(voiceGain);
            }
        }
        UpdateLoop(_whine, _stats.WhineVolume.Eval(speedFrac) * _whineVol
            * Config.GetFloat("flightAudio.whineMixGain", WhineMixGain) * MixGain,
            _stats.WhinePitch.Eval(speedFrac));
        UpdateLoop(_rattle, _stats.RattleVolume.Eval(speedFrac) * _rattleVol * MixGain, 1f);
        UpdateLoop(_damagedEngine, _stats.DamagedEngineGain.Eval(damageFrac) * _damagedEngineVol
            * Config.GetFloat("flightAudio.damagedEngineMixGain", DamagedEngineMixGain) * MixGain, 1f);
    }

    /// <summary>Holds (or releases) the own-plane loops where they are, for the sim-clock halt:
    /// the engine/whine/rattle keep their sample position and volume instead of droning through a
    /// frozen frame. One-shots already in flight are deliberately left to play out — they are
    /// short and stopping them mid-sample is the louder artefact.</summary>
    public void SetPaused(bool paused)
    {
        if (_engine != null)
        {
            _engine.StreamPaused = paused;
        }
        if (_engine2 != null)
        {
            _engine2.StreamPaused = paused;
        }
        if (_whine != null)
        {
            _whine.StreamPaused = paused;
        }
        if (_rattle != null)
        {
            _rattle.StreamPaused = paused;
        }
        if (_damagedEngine != null)
        {
            _damagedEngine.StreamPaused = paused;
        }
    }

    /// <summary>Kills the flight loops (dead engine) and fires one of the game's
    /// plane-explosion one-shots.</summary>
    public void OnCrash()
    {
        _engine?.Stop();
        _engine2?.Stop();
        _whine?.Stop();
        _rattle?.Stop();
        _damagedEngine?.Stop();
        if (_crash == null)
            return;
        var (name, stream, volume) = _crashSounds[
            (int)(Rng.Stream(Rng.FlightAudio).Randi() % (uint)_crashSounds.Count)];
        _crash.Stream = stream;
        _crash.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, volume));
        _crash.Play();
        // Which of the four explosions played — the only trace this pick leaves outside the speakers.
        GD.Print($"crash sound: {name}");
    }

    /// <summary>The ground/dirt crash's earth-impact boom (snd_exp_ground_a), layered over the
    /// plane explosion <see cref="OnCrash"/> already fired. Called only for a Ground surface
    /// (FlightController.Crash), so it does not sound on a sea dive or a future air destruct.</summary>
    public void OnGroundExplosion() => PlayOneShot(_groundExp, _groundExpVol);

    /// <summary>The sea dive's counterpart (snd_exp_water_a — the `_a` pair, not the graze's
    /// lighter `_b`), layered over the plane explosion the same way. Authored one level down, in
    /// the plane_big_splash player_crash_water calls; the crash runtime renders effects only, so
    /// the sound comes from here.</summary>
    public void OnWaterExplosion() => PlayOneShot(_waterExp, _waterExpVol);

    /// <summary>The survivable scrape's authored bark, alongside the <c>touchdown_*</c> effect the
    /// world-effects runtime renders: snd_exp_water_b off water, snd_exp_ground_b off everything
    /// else. Rate-limited by the caller (FlightController), not here — a long scrape would
    /// otherwise re-fire it every physics frame.</summary>
    public void OnGraze(bool water) => PlayOneShot(water ? _grazeWater : _grazeGround,
        (water ? _grazeWaterVol : _grazeGroundVol) * MixGain);

    /// <summary>A round passed close enough to hear: one draw from the warning-shot group,
    /// already rate-limited by <see cref="WarningShotCue"/> in FlightController — same split as
    /// <see cref="OnGraze"/>. Returns the variant that played, or null when the cue is unbuilt.</summary>
    public string? OnWarningShot()
    {
        if (_warningShot == null || _warningShotGroup == null || _archive == null || _defs == null)
            return null;
        string? name = _warningShotGroup.Pick(_warningShotRng!);
        if (name == null || !_defs.TryGetValue(name, out var def))
            return null;
        var stream = _archive.Find(def.WavName, looped: false);
        if (stream == null)
            return null;
        _warningShot.Stream = stream;
        PlayOneShot(_warningShot, def.Volume * MixGain);
        return name;
    }

    /// <summary>Engine wind-down: plays snd_propstop and kills the loops. Layers over the crash
    /// explosion one-shot (<see cref="OnCrash"/>) rather than replacing it — FlightController
    /// calls both from the same crash/destruction moment, snd_propstop right after the boom, so
    /// the loops end on the authored cue instead of a cut. The loop-restart hook in
    /// <see cref="Update"/> is what fires snd_propstart again on the next respawn; nothing here
    /// needs to prevent that.</summary>
    public void OnEngineStop()
    {
        _engine?.Stop();
        _engine2?.Stop();
        _whine?.Stop();
        _rattle?.Stop();
        _damagedEngine?.Stop();
        PlayOneShot(_propStop, _propStopVol);
        GD.Print("engine stop: snd_propstop");
    }

    private static void PlayOneShot(AudioStreamPlayer? player, float volume)
    {
        if (player == null)
            return;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, volume));
        player.Play();
    }

    private static void UpdateLoop(AudioStreamPlayer? player, float volume, float pitch)
    {
        if (player == null)
            return;
        if (volume <= SilenceThreshold)
        {
            if (player.Playing)
                player.Stop();
            return;
        }
        if (!player.Playing)
            player.Play();
        player.PitchScale = Mathf.Max(0.01f, pitch);
        player.VolumeDb = Mathf.LinearToDb(volume);
    }

    private AudioStreamPlayer? MakeLoop(SoundArchive archive,
        IReadOnlyDictionary<string, SoundDef> defs, string sndName, out float baseVolume)
    {
        baseVolume = 1f;
        if (!defs.TryGetValue(sndName, out var def))
        {
            GD.PushWarning($"sound def not found in sounds.json: {sndName}");
            return null;
        }
        var stream = archive.Find(def.WavName, def.Looped);
        if (stream == null)
            return null;
        // sounds.json's authored VOLUME plays unscaled here, same as WorldSounds' 3D
        // emitters and OnCrash's plane-explosion pick — the only other own-ship path that ever
        // read this field, and it never carried a blanket factor. MixGain/WhineMixGain/
        // DamagedEngineMixGain are the deliberate, named attenuations layered on top per loop.
        baseVolume = def.Volume;
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = -60f };
        AddChild(player);
        return player;
    }

    /// <summary>Loads a non-looped one-shot (crash/prop start/stop) from a sounds.json def.</summary>
    private AudioStreamPlayer? MakeOneShot(SoundArchive archive,
        IReadOnlyDictionary<string, SoundDef> defs, string sndName, out float baseVolume)
    {
        baseVolume = 1f;
        if (!defs.TryGetValue(sndName, out var def))
        {
            GD.PushWarning($"sound def not found in sounds.json: {sndName}");
            return null;
        }
        var stream = archive.Find(def.WavName, looped: false);
        if (stream == null)
            return null;
        baseVolume = def.Volume; // unscaled, same convention as MakeLoop above
        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        return player;
    }

    /// <summary>Spin the engine loop up from silence behind snd_propstart. Used for the
    /// initial spawn (here) and every respawn (via the loop-restart hook in Update).</summary>
    private void StartEngine()
    {
        _engineRamp = 0f;
        _engine?.Play();
        _engine2?.Play();
        PlayOneShot(_propStart, _propStartVol);
    }
}
