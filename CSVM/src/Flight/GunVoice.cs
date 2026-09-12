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
/// own cue, moved to where it fires from and held sounding by a lease each renewal resets, so a
/// firing spell is one continuous burst rather than a string of isolated clips. The cue is selected
/// through <see cref="WeaponAudioCues"/>, the seam <see cref="AiWeaponAudio"/> and the pilot's own
/// <see cref="FlightAudio"/> read too, and the cull is the cue's own authored audible distance.
/// ⚠ One voice per mount, never one per owner: the original mints a sound slot per turret, so a
/// zeppelin's rings each hold their own (docs/formats/turrets.md).
/// </summary>
public sealed partial class GunVoice : Node3D
{
    private const float SilenceThreshold = 0.002f;

    private readonly Func<IReadOnlyList<Vector3>>? _listeners;
    private readonly AudioStreamPlayer3D _player;
    private readonly string _cue;
    private readonly float _cullSq;
    private readonly string _label;
    private readonly float _lease;

    private float _leaseLeft;
    private bool _leased;
    // The one step of grace every lease gets, which is what the original's own ordering gives it:
    // the renewal stamps an expiry of now + lease and the release pass drops the voice only once
    // the sound clock is PAST it, so a zero lease survives the frame it was renewed in.
    private bool _renewedThisStep;
    // Null until the voice's first shot, so THAT shot always logs its verdict. A turret cue is
    // audible to 200 m and a gun engages further out than that, so a state seeded `culled` would
    // leave the common case with no line at all (INSTR-45).
    private bool? _culled;

    private GunVoice(WeaponSoundCue cue, Func<IReadOnlyList<Vector3>>? listeners, string label,
        float lease)
    {
        _cue = cue.Name;
        _cullSq = cue.RangeMax * cue.RangeMax;
        _listeners = listeners;
        _label = label;
        _lease = lease;
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

    /// <summary>How long one renewal keeps this voice sounding, seconds. ⚠ Per mount, not one
    /// constant: the two fire paths carry different literals, and each owns its own
    /// (docs/formats/turrets.md, docs/org/weaponFire.md).</summary>
    public float LeaseSeconds => _lease;

    /// <summary>Whether the voice is sounding, read off the live player rather than off a flag this
    /// component keeps, which could agree with itself while the stream is stopped.</summary>
    internal bool Sounding => _player.Playing;

    /// <summary>Builds the voice for a mount whose cue is <paramref name="sndName"/>, or null when
    /// the session has no archive, the mount authors no cue, or the definition is not positional.
    /// <paramref name="label"/> names the mount in this path's log lines and
    /// <paramref name="leaseSeconds"/> is its own fire path's lease. Declining is always said out
    /// loud: no line at all is the one case indistinguishable from never having tried.</summary>
    public static GunVoice? Attach(GunVoiceHome? home, string? sndName, string label,
        float leaseSeconds)
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
        var voice = new GunVoice(cue, home.Listeners, label, leaseSeconds);
        home.Node.AddChild(voice);
        Log.Info("sound", $"gun voice {label}: loop={cue.Name} cull={cue.RangeMax:0} m lease={leaseSeconds:0.00} s");
        return voice;
    }

    /// <summary>The gun is firing, from <paramref name="worldPos"/>: the voice moves there and
    /// renews its lease, which starts the loop again if the previous lease had run out. What
    /// renews it is the caller's own decoded event, a round leaving for a turret and a pass of the
    /// fire decision for a hull.</summary>
    public void Renew(Vector3 worldPos)
    {
        // ⚠ The NODE moves, not the player under it: the cull reads this node's own world position
        // (see Sound), so moving only the player would measure every world gun's distance from the
        // world sound node it hangs under instead of from the gun.
        GlobalPosition = worldPos;
        _leaseLeft = _lease;
        _leased = true;
        _renewedThisStep = true;
        Sound();
    }

    /// <summary>Ages the lease by one sim step and silences the voice when it runs out. Called on
    /// every tick of the mount, the ticks its fire gates close included, so a gun that stops
    /// shooting goes quiet within the lease instead of holding the loop for good.</summary>
    public void Tick(float dt)
    {
        if (!_leased)
        {
            return;
        }
        if (_renewedThisStep)
        {
            _renewedThisStep = false;
            Sound();
            return;
        }
        _leaseLeft -= dt;
        if (_leaseLeft <= 0f)
        {
            _leased = false;
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
        _leased = false;
        _renewedThisStep = false;
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
