using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Own-plane sound, all from the player's own game data: the plane's engine loop
/// (per-plane WAV via vehicle.json 'engine_sound', throttle-driven pitch/volume),
/// the overspeed whine (player.json 'prop_sound' curves — silent until past
/// fd_speed in a dive), the airframe rattle (player.json 'rattle' block), plus the
/// prop start/stop one-shots (snd_propstart/snd_propstop). Non-positional players:
/// these are what the pilot hears; positional 3D emitters are for other aircraft, later.
/// </summary>
public partial class FlightAudio : Node
{
    private PlaneStats _stats = null!;
    private AudioStreamPlayer? _engine, _whine, _rattle;
    private float _engineVol = 1f, _whineVol = 1f, _rattleVol = 1f; // sounds.json VOLUME base gain
    private AudioStreamPlayer? _crash;
    private readonly List<(AudioStreamWav stream, float volume)> _crashSounds = new();
    private AudioStreamPlayer? _groundExp;
    private float _groundExpVol = 1f;
    private AudioStreamPlayer? _propStart, _propStop;
    private float _propStartVol = 1f, _propStopVol = 1f;
    private float _engineRamp = 1f; // 0→1 gain envelope while the engine catches after a start

    // Gun firing (B16): kept references so the looped firing sound + empty-clip cue can be built
    // on demand from any caliber's LOOPED_SOUND_NAME. Own-ship, non-positional (like the engine).
    private SoundArchive? _archive;
    private IReadOnlyDictionary<string, SoundDef>? _defs;
    private AudioStreamPlayer? _gunLoop;
    private string? _gunLoopName;
    private float _gunLoopVol = 1f;
    private AudioStreamPlayer? _emptyClip;
    private float _emptyClipVol = 1f;

    private const float SilenceThreshold = 0.002f;
    private const float EngineStartRamp = 1.8f; // s for the loop to fade to full behind snd_propstart

    // The prop_sound curve caps the whine at volume 0.5, but spectral analysis of the
    // user's reference video (Bloodhawk dive to ~1.27x fd_speed) bounds the original's whine at
    // 0.06-0.14 of the engine's amplitude — the reader volume is evidently not a linear mix gain
    // for this loop. 0.12 puts our saturated whine ~24 dB under the engine, at the bound. TUNE.
    private const float WhineMixGain = 0.12f;

    /// <summary>Overall gain for this plane's own-ship mix. 1 for single player;
    /// splitscreen sets 1/√N so N simultaneous engine stacks don't sum to a wall of noise
    /// (equal-power, so 2P ≈ −3 dB each, 4P ≈ −6 dB). TUNE — pending a real 4P listen.</summary>
    public float MixGain = 1f;

    public void Setup(SoundArchive archive, Dictionary<string, SoundDef> defs, PlaneStats stats)
    {
        _stats = stats;
        _archive = archive;
        _defs = defs;
        // The shared empty-clip cue (weapons.json NO_AMMO_WARNING = snd_emptyclip).
        _emptyClip = MakeOneShot(archive, defs, "snd_emptyclip", out _emptyClipVol);
        _engine = MakeLoop(archive, defs, stats.EngineSound, out _engineVol);
        _whine = MakeLoop(archive, defs, stats.WhineSound, out _whineVol);
        _rattle = MakeLoop(archive, defs, stats.RattleSound, out _rattleVol);

        // Prop start/stop one-shots (both non-looped, no VOLUME field → base gain 1.0):
        // snd_propstart plays as the engine ramps in on (re)spawn; snd_propstop is the
        // wind-down for a future landing/shutdown (see OnEngineStop).
        _propStart = MakeOneShot(archive, defs, "snd_propstart", out _propStartVol);
        _propStop = MakeOneShot(archive, defs, "snd_propstop", out _propStopVol);

        // Crash explosions: the game defines snd_exp_plane1..4 as a set; pick one at
        // random per crash, like the original.
        for (int i = 1; i <= 4; i++)
            if (defs.TryGetValue($"snd_exp_plane{i}", out var def)
                && archive.Find(def.WavName, looped: false) is { } stream)
                _crashSounds.Add((stream, def.Volume));
        if (_crashSounds.Count > 0)
        {
            _crash = new AudioStreamPlayer();
            AddChild(_crash);
        }

        // The ground/dirt crash choreography layers snd_exp_ground_a — the
        // heavy earth-impact boom — over the plane_destroy_sg explosion above; the dirt anim
        // def fires it as its Sound event. Air/water hits use their own sounds, so this stays
        // gated on the surface in FlightController (OnGroundExplosion), not folded into OnCrash.
        _groundExp = MakeOneShot(archive, defs, "snd_exp_ground_a", out _groundExpVol);
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
        baseVolume = def.Volume*0.2f; //Temporary fix for volume
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
        baseVolume = def.Volume * 0.2f; // same temporary volume scale as the loops
        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        return player;
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
                _gunLoopVol = def.Volume * 0.2f;
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

    /// <summary>Spin the engine loop up from silence behind snd_propstart. Used for the
    /// initial spawn (here) and every respawn (via the loop-restart hook in Update).</summary>
    private void StartEngine()
    {
        _engineRamp = 0f;
        _engine?.Play();
        PlayOneShot(_propStart, _propStartVol);
    }

    /// <summary>Per-frame drive: <paramref name="speedFrac"/> is speed / fd_speed.
    /// Not called while crashed, so the loops stay dead until respawn.</summary>
    public void Update(float dt, float throttle, float speedFrac)
    {
        if (_engineRamp < 1f)
            _engineRamp = Mathf.Min(1f, _engineRamp + dt / EngineStartRamp);
        if (_engine != null)
        {
            if (!_engine.Playing)
                StartEngine(); // respawn after a crash: propstart + fresh volume ramp-in
            _engine.PitchScale = Mathf.Max(0.01f, _stats.EnginePitch.Eval(throttle));
            _engine.VolumeDb = Mathf.LinearToDb(Mathf.Max(
                SilenceThreshold, _stats.EngineVolume.Eval(throttle) * _engineVol * _engineRamp * MixGain));
        }
        UpdateLoop(_whine, _stats.WhineVolume.Eval(speedFrac) * _whineVol * WhineMixGain * MixGain,
            _stats.WhinePitch.Eval(speedFrac));
        UpdateLoop(_rattle, _stats.RattleVolume.Eval(speedFrac) * _rattleVol * MixGain, 1f);
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
        var (stream, volume) = _crashSounds[(int)(GD.Randi() % (uint)_crashSounds.Count)];
        _crash.Stream = stream;
        _crash.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, volume));
        _crash.Play();
    }

    /// <summary>The ground/dirt crash's earth-impact boom (snd_exp_ground_a), layered over the
    /// plane explosion <see cref="OnCrash"/> already fired. Called only for a Ground surface
    /// (FlightController.Crash), so it does not sound on a future air or water destruct.</summary>
    public void OnGroundExplosion() => PlayOneShot(_groundExp, _groundExpVol);

    /// <summary>Graceful engine wind-down (future landing/parking): plays snd_propstop and
    /// kills the loops. NOT called on crash — the explosion one-shot already covers that
    /// moment, and a prop wind-down under it would read wrong. A shutdown state using this
    /// must also stop driving <see cref="Update"/> (as the crash freeze does), else the
    /// loop-restart hook there would immediately fire snd_propstart again.</summary>
    public void OnEngineStop()
    {
        _engine?.Stop();
        _whine?.Stop();
        _rattle?.Stop();
        PlayOneShot(_propStop, _propStopVol);
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
}
