using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// One AI aircraft's engine audio, positional: the same two slots the pilot's own
/// <see cref="FlightAudio"/> drives (engine, and the whine no shipped airframe names), on
/// <see cref="AudioStreamPlayer3D"/>s riding this node, plus the distance cull that silences an
/// aircraft the player is too far from. Every number comes from
/// <see cref="EngineAudioCurves"/>, which the own-ship path reads too.
/// ⚠ Own-ship concepts stay out: no splitscreen mix gain (the panes' listeners already decide who
/// hears this), no start ramp, no prop-start cue, no crash or graze one-shots — an AI kill is
/// audible from the crash animation's own authored sound events.
/// </summary>
public sealed partial class AiEngineAudio : Node3D
{
    /// <summary>Where the session's listeners are, one per splitscreen pane. The cull measures to
    /// the nearest of them, since that is the pane whose volume wins the mix. Left null, the cull
    /// falls back to this node's own viewport camera.</summary>
    public Func<IReadOnlyList<Vector3>>? Listeners;

    private PlaneStats _stats = null!;
    private AudioStreamPlayer3D? _engine, _whine;
    private AudioStreamWav? _engineStream, _damagedStream;
    private float _engineVol = 1f, _damagedVol = 1f, _whineVol = 1f;
    private bool _engineDamaged;
    private float _enginePitchMul = 1f;
    private bool _culled = true;   // starts culled so the first in-range frame logs its start

    /// <summary>Builds this aircraft's engine audio and hangs it under <paramref name="controller"/>,
    /// or returns null when the session found no sound archive. The spawner's whole share of the
    /// job; call it after the controller has joined the tree, since the cull reads a world position.
    /// <paramref name="listeners"/> left null falls back to the node's own viewport camera.</summary>
    public static AiEngineAudio? Attach(FlightController controller, SoundArchive? archive,
        IReadOnlyDictionary<string, SoundDef>? defs, PlaneStats stats,
        Func<IReadOnlyList<Vector3>>? listeners = null)
    {
        if (archive == null || defs == null)
        {
            return null;
        }
        var audio = new AiEngineAudio { Name = "EngineAudio", Listeners = listeners };
        controller.AddChild(audio);
        audio.Setup(archive, defs, stats);
        return audio;
    }

    /// <summary>Builds the slots this airframe's data names. The engine slot is resolved with both
    /// its candidate streams up front, so the damaged swap is an assignment; the whine slot is built
    /// only when a definition names it, which no shipped airframe does.</summary>
    public void Setup(SoundArchive archive, IReadOnlyDictionary<string, SoundDef> defs, PlaneStats stats)
    {
        _stats = stats;
        _engine = MakeLoop(archive, defs, stats.EngineSound, out _engineVol);
        _engineStream = _engine?.Stream as AudioStreamWav;
        // No player of its own: this stream replaces the engine's on the one slot.
        if (stats.DamagedEngineSound is { } damagedName)
            _damagedStream = LoadStream(archive, defs, damagedName, out _damagedVol);
        if (stats.WhineSound is { } whineName)
        {
            _whine = MakeLoop(archive, defs, whineName, out _whineVol);
        }
    }

    /// <summary>Per-frame drive, same arguments as <see cref="FlightAudio.Update"/> and called from
    /// the same place, so both paths run off the sim clock. Beyond the cull both slots stop; back
    /// inside, they start again.</summary>
    public void Update(float dt, float throttle, float speedFrac, float damageFrac)
    {
        _ = dt;
        SetEngineDamaged(damageFrac > 0f);
        float distSq = NearestListenerDistanceSq();
        bool culled = distSq > EngineAudioCurves.CullDistanceSq;
        if (culled != _culled)
        {
            _culled = culled;
            // Transitions only, and always logged: audio cannot be screenshot-verified, and this
            // line is what separates "silent because it is past the cull" from "silent because its
            // definition never resolved".
            Log.Debug("sound", $"ai engine {GetParent()?.Name} {(culled ? "culled" : "audible")} at {Mathf.Sqrt(distSq):0} m");
        }
        if (culled)
        {
            _engine?.Stop();
            _whine?.Stop();
            return;
        }
        if (_engine != null)
        {
            var (pitch, volume) = EngineAudioCurves.Engine(_stats, throttle, _enginePitchMul);
            UpdateLoop(_engine, volume * (_engineDamaged ? _damagedVol : _engineVol), pitch);
        }
        if (_whine != null)
        {
            var (pitch, volume) = EngineAudioCurves.Whine(_stats, speedFrac);
            UpdateLoop(_whine, volume * _whineVol, pitch);
        }
    }

    /// <summary>Kills both slots for good — the aircraft is down, and its crash animation owns
    /// everything audible from here on.</summary>
    public void Stop()
    {
        _engine?.Stop();
        _whine?.Stop();
    }

    private static void UpdateLoop(AudioStreamPlayer3D player, float volume, float pitch)
    {
        if (volume <= 0.002f)
        {
            if (player.Playing)
                player.Stop();
            return;
        }
        if (!player.Playing)
            player.Play();
        player.PitchScale = pitch;
        player.VolumeDb = Mathf.LinearToDb(volume);
    }

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

    // Squared range to the nearest listener, or 0 when there is none to measure from: an aircraft
    // with no listener is not evidence that it should be silent, and culling it would hide a
    // missing-listener bug behind a working-looking silence.
    private float NearestListenerDistanceSq()
    {
        var here = GlobalPosition;
        var ears = Listeners?.Invoke();
        if (ears is { Count: > 0 })
        {
            float best = float.MaxValue;
            foreach (var ear in ears)
            {
                best = Mathf.Min(best, here.DistanceSquaredTo(ear));
            }
            return best;
        }
        return GetViewport()?.GetCamera3D() is { } camera
            ? here.DistanceSquaredTo(camera.GlobalPosition)
            : 0f;
    }

    // The engine slot's def swap, decided by the same helper the own-ship path uses so the two
    // cannot drift; see FlightAudio.SetEngineDamaged for why any damage at all trips it.
    private void SetEngineDamaged(bool damaged)
    {
        damaged &= _damagedStream != null;
        if (damaged == _engineDamaged || _engine == null)
        {
            return;
        }
        _engineDamaged = damaged;
        var (name, pitchMul) = EngineAudioCurves.EngineDefFor(
            _stats, damaged, Rng.Stream(Rng.FlightAudio));
        _enginePitchMul = pitchMul;
        var stream = damaged ? _damagedStream : _engineStream;
        if (stream == null)
        {
            return;
        }
        bool wasPlaying = _engine.Playing;
        _engine.Stop();
        _engine.Stream = stream;
        if (wasPlaying)
            _engine.Play();
        Log.Debug("sound", $"ai engine {GetParent()?.Name} slot 0 ->{name} pitchMul={_enginePitchMul:0.000}");
    }

    // RANGE is [full-volume distance, audible distance], mapped onto Godot's inverse-distance curve
    // the way WorldSounds maps every other 3D emitter — an approximation of the original's roll-off,
    // and TUNE for the same reason it is there.
    private AudioStreamPlayer3D? MakeLoop(SoundArchive archive,
        IReadOnlyDictionary<string, SoundDef> defs, string sndName, out float baseVolume)
    {
        var stream = LoadStream(archive, defs, sndName, out baseVolume);
        if (stream == null || !defs.TryGetValue(sndName, out var def))
        {
            return null;
        }
        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            UnitSize = def.RangeMin,
            MaxDistance = def.RangeMax,
            VolumeDb = -60f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        };
        AddChild(player);
        return player;
    }
}
