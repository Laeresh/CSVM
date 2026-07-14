using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Own-plane sound, all from the player's own game data: the plane's engine loop
/// (per-plane WAV via vehicle.json 'engine_sound', throttle-driven pitch/volume),
/// the overspeed whine (player.json 'prop_sound' curves — silent until past
/// fd_speed in a dive), and the airframe rattle (player.json 'rattle' block).
/// Non-positional players: these are what the pilot hears; positional 3D emitters
/// are for other aircraft, later.
/// </summary>
public partial class FlightAudio : Node
{
    private PlaneStats _stats = null!;
    private AudioStreamPlayer? _engine, _whine, _rattle;
    private float _engineVol = 1f, _whineVol = 1f, _rattleVol = 1f; // sounds.json VOLUME base gain
    private AudioStreamPlayer? _crash;
    private readonly List<(AudioStreamWav stream, float volume)> _crashSounds = new();

    private const float SilenceThreshold = 0.002f;

    public void Setup(SoundArchive archive, Dictionary<string, SoundDef> defs, PlaneStats stats)
    {
        _stats = stats;
        _engine = MakeLoop(archive, defs, stats.EngineSound, out _engineVol);
        _whine = MakeLoop(archive, defs, stats.WhineSound, out _whineVol);
        _rattle = MakeLoop(archive, defs, stats.RattleSound, out _rattleVol);

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
        baseVolume = def.Volume;
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = -60f };
        AddChild(player);
        return player;
    }

    public override void _Ready() => _engine?.Play(); // whine/rattle start on demand

    /// <summary>Per-frame drive: <paramref name="speedFrac"/> is speed / fd_speed.
    /// Not called while crashed, so the loops stay dead until respawn.</summary>
    public void Update(float throttle, float speedFrac)
    {
        if (_engine != null)
        {
            if (!_engine.Playing)
                _engine.Play(); // restart after a crash respawn
            _engine.PitchScale = Mathf.Max(0.01f, _stats.EnginePitch.Eval(throttle));
            _engine.VolumeDb = Mathf.LinearToDb(
                Mathf.Max(SilenceThreshold, _stats.EngineVolume.Eval(throttle) * _engineVol));
        }
        UpdateLoop(_whine, _stats.WhineVolume.Eval(speedFrac) * _whineVol, _stats.WhinePitch.Eval(speedFrac));
        UpdateLoop(_rattle, _stats.RattleVolume.Eval(speedFrac) * _rattleVol, 1f);
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
