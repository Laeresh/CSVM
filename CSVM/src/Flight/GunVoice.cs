using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>Everything a mount needs to build a gun voice: the node the emitter hangs under, the
/// archive and definitions its cue is selected from, and the session's listener seam. One of these
/// serves every mount a session builds, so no builder grows four audio parameters of its own.</summary>
/// <param name="Node">Parent for the emitters: a host aircraft, or the world's sound node.</param>
/// <param name="Archive">The session sound archive; null leaves every mount silent.</param>
/// <param name="Defs">The <c>sounds.json</c> definitions a cue name is looked up in.</param>
/// <param name="Listeners">Where the human pilots are, for the cull; null falls back to the camera.</param>
public sealed record GunVoiceHome(Node3D Node, SoundArchive? Archive,
    IReadOnlyDictionary<string, SoundDef>? Defs, Func<IReadOnlyList<Vector3>>? Listeners);

/// <summary>
/// One mounted gun's firing voice: a single <see cref="AudioStreamPlayer3D"/> carrying the mount's
/// own cue, moved to the muzzle each round leaves from and held sounding by a lease that shot
/// renews, so a firing spell is one continuous burst rather than a string of isolated clips. The
/// cue is selected through <see cref="WeaponAudioCues"/>, the seam <see cref="AiWeaponAudio"/> and
/// the pilot's own <see cref="FlightAudio"/> read too, and the cull is the cue's own authored
/// audible distance. ⚠ One voice per mount, never one per owner: the original mints a sound slot
/// per turret, so a zeppelin's rings each hold their own (docs/formats/turrets.md).
/// </summary>
public sealed partial class GunVoice : Node3D
{
    /// <summary>How long one shot keeps the voice sounding, seconds: the turret fire path's own
    /// literal, renewed on every round. A mount whose fire interval is shorter than this never
    /// falls silent between shots (docs/formats/turrets.md, "Projectile cadence and cannon
    /// audio have separate lifetimes").</summary>
    public const float LeaseSeconds = 0.5f;

    private const float SilenceThreshold = 0.002f;

    private readonly Func<IReadOnlyList<Vector3>>? _listeners;
    private readonly AudioStreamPlayer3D _player;
    private readonly string _cue;
    private readonly float _cullSq;
    private readonly string _label;

    private float _leaseLeft;
    // Null until the voice's first shot, so THAT shot always logs its verdict. A turret cue is
    // audible to 200 m and a gun engages further out than that, so a state seeded `culled` would
    // leave the common case with no line at all (INSTR-45).
    private bool? _culled;

    private GunVoice(WeaponSoundCue cue, Func<IReadOnlyList<Vector3>>? listeners, string label)
    {
        _cue = cue.Name;
        _cullSq = cue.RangeMax * cue.RangeMax;
        _listeners = listeners;
        _label = label;
        Name = "GunVoice";
        // RANGE is [full-volume distance, audible distance], mapped onto Godot's inverse-distance
        // curve the way WorldSounds and AiWeaponAudio map every other 3D emitter.
        _player = new AudioStreamPlayer3D
        {
            Stream = cue.Stream,
            UnitSize = cue.RangeMin,
            MaxDistance = cue.RangeMax,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, cue.Volume)),
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            Bus = AudioBuses.Effects,
        };
        AddChild(_player);
    }

    /// <summary>Whether the voice is sounding, read off the live player rather than off a flag this
    /// component keeps, which could agree with itself while the stream is stopped.</summary>
    internal bool Sounding => _player.Playing;

    /// <summary>Builds the voice for a mount whose cue is <paramref name="sndName"/>, or null when
    /// the session has no archive, the mount authors no cue, or the definition is not positional.
    /// <paramref name="label"/> names the mount in this path's log lines. Declining is always said
    /// out loud: no line at all is the one case indistinguishable from never having tried.</summary>
    public static GunVoice? Attach(GunVoiceHome? home, string? sndName, string label)
    {
        if (home == null || string.IsNullOrEmpty(sndName))
        {
            return null;
        }
        if (WeaponAudioCues.GunLoop(home.Archive, home.Defs, sndName) is not { Is3D: true } cue)
        {
            Log.Info("sound", $"gun voice {label}: {sndName} did not resolve as a positional cue, so this mount is silent");
            return null;
        }
        var voice = new GunVoice(cue, home.Listeners, label);
        home.Node.AddChild(voice);
        Log.Info("sound", $"gun voice {label}: loop={cue.Name} cull={cue.RangeMax:0} m lease={LeaseSeconds:0.0} s");
        return voice;
    }

    /// <summary>One round left <paramref name="muzzleWorldPos"/>: the voice moves there and renews
    /// its lease, which starts the loop again if the previous lease had run out. The position is
    /// the firing muzzle's, which is what the original's turret path hands its sound slot.</summary>
    public void Shot(Vector3 muzzleWorldPos)
    {
        _player.GlobalPosition = muzzleWorldPos;
        _leaseLeft = LeaseSeconds;
        Sound();
    }

    /// <summary>Ages the lease by one sim step and silences the voice when it runs out. Called on
    /// every tick of the mount, the ticks its fire gates close included, so a gun that stops
    /// shooting goes quiet within the lease instead of holding the loop for good.</summary>
    public void Tick(float dt)
    {
        if (_leaseLeft <= 0f)
        {
            return;
        }
        _leaseLeft -= dt;
        if (_leaseLeft <= 0f)
        {
            _player.Stop();
            return;
        }
        Sound();
    }

    /// <summary>Drops the lease and silences the voice at once, for a mount whose death
    /// choreography owns everything audible from here on.</summary>
    public void Stop()
    {
        _leaseLeft = 0f;
        _player.Stop();
    }

    /// <summary>The emitter's cue name, world position and authored cull distance. Internal so an
    /// emitter suite reads the pairing this path's log prints rather than inferring it.</summary>
    internal (string Name, Vector3 Position, float RangeMax) Emitter() =>
        (_cue, _player.GlobalPosition, _player.MaxDistance);

    // ⚠ The cull is the cue's OWN audible distance, not EngineAudioCurves.CullDistance: a turret
    // loop is authored audible to 200 m, far inside the engine routine's 2000, so that number could
    // never bite first and reading it here would be a borrowed constant.
    private void Sound()
    {
        float distSq = AudioListeners.NearestDistanceSq(this, _listeners);
        bool culled = distSq > _cullSq;
        SetCulled(culled, distSq);
        if (culled)
        {
            _player.Stop();
            return;
        }
        if (!_player.Playing)
        {
            _player.Play();
        }
    }

    // The first shot and every transition after it, always logged: audio cannot be
    // screenshot-verified (INSTR-45), and this line is what separates "silent because it is past
    // the cull" from "silent because its definition never resolved".
    private void SetCulled(bool culled, float distSq)
    {
        if (culled == _culled)
        {
            return;
        }
        _culled = culled;
        Log.Debug("sound", $"gun voice {_label} {(culled ? "culled" : "audible")} at {Mathf.Sqrt(distSq):0} m (cull {Mathf.Sqrt(_cullSq):0} m)");
    }
}
