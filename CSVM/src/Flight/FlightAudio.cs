using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Own-plane sound, all from the player's own game data: the plane's engine loop (per-plane WAV via
/// vehicle.json 'engine_sound', throttle-driven pitch and volume, swapped for 'damaged_engine_sound'
/// while the airframe is hurt and for 'cockpit_engine_sound' while the pilot's SELECTED view is
/// Cockpit or Nose — <see cref="EngineAudioCurves.EngineDefFor"/> is the one precedence rule both
/// swaps share), the overspeed whine ('prop_sound', which no shipped def names), the
/// airframe rattle (player.json 'rattle' block), plus the prop start/stop one-shots
/// (snd_propstart/snd_propstop). Non-positional players: these are what the pilot hears, and
/// <see cref="AiEngineAudio"/> is the positional twin every other aircraft carries (own-ship only —
/// an AI rig has no selected view, so it never reads 'cockpit_engine_sound').
/// </summary>
public partial class FlightAudio : Node
{
    /// <summary>Overall gain for this plane's own-ship mix. 1 for single player;
    /// splitscreen sets 1/√N so N simultaneous engine stacks don't sum to a wall of noise
    /// (equal-power, so 2P ≈ −3 dB each, 4P ≈ −6 dB). TUNE — pending a real 4P listen.</summary>
    public float MixGain = 1f;

    private const float SilenceThreshold = 0.002f;
    // s for the loop to fade to full behind snd_propstart. Sourced from startprops' authored prop
    // cross-fade (plane_props.zrd.json, OBJECT_OPACITY_FROM_TO RUN_TIME 2.0 on staticpropN→propN)
    // — the nearest authored duration. TUNE pending a listen A/B.
    private const float EngineStartRamp = 2.0f;

    private readonly List<(string name, AudioStreamWav stream, float volume)> _crashSounds = new();

    private PlaneStats _stats = null!;
    private AudioStreamPlayer? _engine, _whine, _rattle;
    private float _engineVol = 1f, _whineVol = 1f, _rattleVol = 1f; // sounds.json VOLUME base gain

    // The engine slot's three candidate streams, all resolved at Setup so a mid-flight swap is an
    // assignment rather than an archive read: the session's SoundArchive outlives the build, but a
    // decode at the moment the airframe is hit or the view changes is a hitch nobody needs.
    private AudioStreamWav? _engineStream, _damagedStream, _cockpitStream;
    private float _damagedVol = 1f, _cockpitVol = 1f;
    private bool _engineDamaged;              // which of the three the slot currently holds
    private bool _engineFirstPerson;
    private float _enginePitchMul = 1f;       // the damaged swap's one-off pitch draw
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
        _engineStream = _engine?.Stream as AudioStreamWav;
        // Not built as a player of its own: this stream replaces the engine's on the one slot, so a
        // second player would be the blend the decode refuted.
        if (stats.DamagedEngineSound is { } damagedName)
            _damagedStream = LoadStream(archive, defs, damagedName, out _damagedVol);
        // Same non-player pattern: a swap candidate, not a player of its own (D31).
        if (stats.CockpitEngineSound is { } cockpitName)
            _cockpitStream = LoadStream(archive, defs, cockpitName, out _cockpitVol);
        if (stats.WhineSound is { } whineName)
            _whine = MakeLoop(archive, defs, whineName, out _whineVol);
        _rattle = MakeLoop(archive, defs, stats.RattleSound, out _rattleVol);

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

        // The ground/water crash booms, layered over plane_destroy_sg (docs/architecture.md).
        _groundExp = MakeOneShot(archive, defs, "snd_exp_ground_a", out _groundExpVol);
        _waterExp = MakeOneShot(archive, defs, "snd_exp_water_a", out _waterExpVol);

        // The graze reaction's authored sounds (touchdown.zrd), the lighter `_b` pair.
        _grazeGround = MakeOneShot(archive, defs, "snd_exp_ground_b", out _grazeGroundVol);
        _grazeWater = MakeOneShot(archive, defs, "snd_exp_water_b", out _grazeWaterVol);

        // The near-miss cue's group (player.json warning_shot_sound), own-ship and non-positional
        // so splitscreen only the nearly-hit pilot hears it.
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
    /// pristine), <paramref name="firstPerson"/> is the pilot's SELECTED view
    /// (<see cref="FlightController.FirstPersonView"/>) — a held numpad key or look-behind is a
    /// per-frame camera pose, not a change of selection, so it does not retrigger this swap (D31).
    /// Not called while crashed, so the loops stay dead until respawn.</summary>
    public void Update(float dt, in EngineDrive drive, float speedFrac, float damageFrac, bool firstPerson = false)
    {
        if (_engineRamp < 1f)
            _engineRamp = Mathf.Min(1f, _engineRamp + dt / EngineStartRamp);
        UpdateEngineSlot(damageFrac > 0f, firstPerson);
        if (_engine != null)
        {
            if (!_engine.Playing)
                StartEngine(); // respawn after a crash: propstart + fresh volume ramp-in
            var (pitch, volume) = EngineAudioCurves.Engine(_stats, drive, _enginePitchMul);
            _engine.PitchScale = pitch;
            float baseVol = _engineDamaged ? _damagedVol : _engineFirstPerson ? _cockpitVol : _engineVol;
            _engine.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold,
                volume * baseVol * _engineRamp * MixGain));
        }
        if (_whine != null)
        {
            var (pitch, volume) = EngineAudioCurves.Whine(_stats, speedFrac);
            UpdateLoop(_whine, volume * _whineVol * MixGain, pitch);
        }
        UpdateLoop(_rattle, _stats.RattleVolume.Eval(speedFrac) * _rattleVol * MixGain, 1f);
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
        if (_whine != null)
        {
            _whine.StreamPaused = paused;
        }
        if (_rattle != null)
        {
            _rattle.StreamPaused = paused;
        }
    }

    /// <summary>Kills the flight loops (dead engine) and fires one of the game's
    /// plane-explosion one-shots.</summary>
    public void OnCrash()
    {
        _engine?.Stop();
        _whine?.Stop();
        _rattle?.Stop();
        if (_crash == null)
            return;
        var (name, stream, volume) = _crashSounds[
            (int)(Rng.Stream(Rng.FlightAudio).Randi() % (uint)_crashSounds.Count)];
        _crash.Stream = stream;
        // Takes MixGain (D32, docs/architecture.md): a splitscreen pile-up fires this
        // once per downed rig in the same instant, so N booms must not sum into a clipped wall.
        float gain = volume * MixGain;
        _crash.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, gain));
        _crash.Play();
        // Which of the four explosions played, plus D32's computed gain — the only
        // trace this pick leaves outside the speakers.
        GD.Print($"crash sound: {name} MixGain={MixGain:0.00} vol={gain:0.000}");
    }

    /// <summary>The `dirt`(13) crash's earth-impact boom (snd_exp_ground_a), layered over the
    /// plane explosion <see cref="OnCrash"/> already fired. Called only when `player_crash_dirt`
    /// is the resolved def (FlightController.PlayCrashBoom), so it does not sound on a sea dive,
    /// the `player_crash_default` fallback, or a future mid-air destruct (which plays no
    /// `player_crash_*` def at all).</summary>
    // D32: layers over OnCrash's boom in the same instant, so it takes MixGain for the
    // same reason — a pile-up stacks this once per downed rig too.
    public void OnGroundExplosion()
    {
        float gain = _groundExpVol * MixGain;
        PlayOneShot(_groundExp, gain);
        GD.Print($"crash sound: snd_exp_ground_a MixGain={MixGain:0.00} vol={gain:0.000}");
    }

    /// <summary>The sea dive's counterpart (snd_exp_water_a — the `_a` pair, not the graze's
    /// lighter `_b`), layered over the plane explosion the same way. Authored one level down, in
    /// the plane_big_splash player_crash_water calls; the crash runtime renders effects only, so
    /// the sound comes from here.</summary>
    // D32: same crash-instant stacking as OnGroundExplosion; MixGain for the same reason.
    public void OnWaterExplosion()
    {
        float gain = _waterExpVol * MixGain;
        PlayOneShot(_waterExp, gain);
        GD.Print($"crash sound: snd_exp_water_a MixGain={MixGain:0.00} vol={gain:0.000}");
    }

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
        _whine?.Stop();
        _rattle?.Stop();
        // D32: fires right after OnCrash's boom, the same crash instant — MixGain for
        // the same pile-up reason, not the "your prop" respawn cue StartEngine plays below.
        float gain = _propStopVol * MixGain;
        PlayOneShot(_propStop, gain);
        GD.Print($"engine stop: snd_propstop MixGain={MixGain:0.00} vol={gain:0.000}");
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

    // A looped definition's stream and its unscaled VOLUME, same convention as WorldSounds' 3D
    // emitters. Split out from MakeLoop so the engine slot's swap candidate can be resolved without
    // building a player nothing would ever start.
    private static AudioStreamWav? LoadStream(SoundArchive archive,
        IReadOnlyDictionary<string, SoundDef> defs, string sndName, out float baseVolume)
    {
        baseVolume = 1f;
        if (!defs.TryGetValue(sndName, out var def))
        {
            GD.PushWarning($"sound def not found in sounds.json: {sndName}");
            return null;
        }
        baseVolume = def.Volume;
        return archive.Find(def.WavName, def.Looped);
    }

    /// <summary>Points the engine slot at <c>damaged_engine_sound</c>, <c>cockpit_engine_sound</c>
    /// or back at <c>engine_sound</c> (<see cref="EngineAudioCurves.EngineDefFor"/> carries the
    /// precedence), drawing the damaged swap's pitch multiplier as it goes. Damaged gates on ANY
    /// damage (the bitmask is undecoded, so any damage swaps and a full repair swaps back); either
    /// input changing re-evaluates the pair.</summary>
    private void UpdateEngineSlot(bool damaged, bool firstPerson)
    {
        damaged &= _damagedStream != null;
        firstPerson &= _cockpitStream != null;
        if (_engine == null || (damaged == _engineDamaged && firstPerson == _engineFirstPerson))
        {
            return;
        }
        _engineDamaged = damaged;
        _engineFirstPerson = firstPerson;
        var (name, pitchMul) = EngineAudioCurves.EngineDefFor(
            _stats, damaged, Rng.Stream(Rng.FlightAudio), firstPerson);
        _enginePitchMul = pitchMul;
        var stream = damaged ? _damagedStream : firstPerson ? _cockpitStream : _engineStream;
        if (stream == null)
        {
            return;
        }
        bool wasPlaying = _engine.Playing;
        _engine.Stop();
        _engine.Stream = stream;
        if (wasPlaying)
            _engine.Play();
        // The headless observable for a swap nobody can screenshot: which def the slot took and
        // what the draw gave it. A hard cut, same as the damaged swap — no crossfade is decoded.
        GD.Print($"engine sound: slot 0 -> {name} pitchMul={_enginePitchMul:0.000}");
    }

    private AudioStreamPlayer? MakeLoop(SoundArchive archive,
        IReadOnlyDictionary<string, SoundDef> defs, string sndName, out float baseVolume)
    {
        var stream = LoadStream(archive, defs, sndName, out baseVolume);
        if (stream == null)
            return null;
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = -60f };
        AddChild(player);
        return player;
    }

    // Loads a non-looped one-shot (crash/prop start/stop) from a sounds.json def.
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

    // Spin the engine loop up from silence behind snd_propstart. Used for the
    // initial spawn (here) and every respawn (via the loop-restart hook in Update).
    private void StartEngine()
    {
        _engineRamp = 0f;
        _engine?.Play();
        // Stays at raw volume, deliberately unlike the crash-boom family — a respawn does not
        // pile up with other rigs' at one instant.
        PlayOneShot(_propStart, _propStartVol);
    }
}
