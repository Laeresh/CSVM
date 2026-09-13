using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// One AI aircraft's weapon audio, positional: the sustained-fire gun loop and the dry-trigger cue
/// the pilot's own <see cref="FlightAudio"/> plays flat, on <see cref="AudioStreamPlayer3D"/>s riding
/// this node, plus the cull that silences an aircraft firing further off than its cue is audible.
/// Both definitions are selected through <see cref="WeaponAudioCues"/>, which the own-ship path reads
/// too. The sibling of <see cref="AiEngineAudio"/>, and separate from it because that component's
/// contract is the two engine slots and nothing else.
/// ⚠ Own-ship concepts stay out: no splitscreen mix gain (the panes' listeners already decide who
/// hears this), no incoming-fire cue (those are the pilot being shot at), and no pitch term of any
/// kind (see <see cref="StartGunLoop"/>).
/// </summary>
public sealed partial class AiWeaponAudio : Node3D
{
    /// <summary>Where the session's human pilots are, the same nearest-human seam
    /// <c>ProjectilePool</c> measures its weapon one-shots against. The cull takes the nearest of
    /// them. Left null, it falls back to this node's own viewport camera.</summary>
    public Func<IReadOnlyList<Vector3>>? Listeners;

    private const float SilenceThreshold = 0.002f;

    private SoundArchive? _archive;
    private IReadOnlyDictionary<string, SoundDef>? _defs;
    private AudioStreamPlayer3D? _gunLoop;
    private string? _gunLoopName;
    private float _gunLoopVol = 1f;
    private float _gunLoopCullSq;             // the live loop's own RANGE audible distance, squared
    private AudioStreamPlayer3D? _emptyClip;
    private string? _emptyClipName;
    private float _emptyClipVol = 1f;
    // Null until the loop's first frame, so THAT frame always logs its verdict. A gun's authored
    // audible distance is 150-550 m and traffic engages further out than that, so a first frame
    // seeded `culled` would leave the common case with no line at all (INSTR-45).
    private bool? _culled;

    /// <summary>Whether the gun loop is sounding, read off the live player rather than off a flag
    /// this component keeps, which could agree with itself while the voice is stopped. Internal for
    /// the emitter suite's cull half.</summary>
    internal bool LoopSounding => _gunLoop is { Playing: true };

    /// <summary>Builds this aircraft's weapon audio and hangs it under <paramref name="controller"/>,
    /// or returns null when the session found no sound archive. The spawner's whole share of the job;
    /// call it after the controller has joined the tree, since the cull reads a world position.
    /// <paramref name="weapons"/> carries the dry cue's <c>NO_AMMO_WARNING</c> name;
    /// <paramref name="listeners"/> left null falls back to the node's own viewport camera.</summary>
    public static AiWeaponAudio? Attach(FlightController controller, SoundArchive? archive,
        IReadOnlyDictionary<string, SoundDef>? defs, WeaponDefs? weapons,
        Func<IReadOnlyList<Vector3>>? listeners = null)
    {
        if (archive == null || defs == null)
        {
            // Said out loud for the reason AiEngineAudio says it: no archive means no line from Setup
            // below, and no line at all is the one case indistinguishable from never having tried.
            Log.Info("sound", $"ai weapons {controller.Name}: no sound archive, so this aircraft's guns are silent");
            return null;
        }
        var audio = new AiWeaponAudio { Name = "WeaponAudio", Listeners = listeners };
        controller.AddChild(audio);
        audio.Setup(archive, defs, weapons);
        return audio;
    }

    /// <summary>Keeps the archive and definitions for the gun loop, which is built on the first frame
    /// a caliber fires rather than up front (an airframe's loadout can hold several, and a plane that
    /// never shoots should decode none), and resolves the dry-trigger cue now, since there is exactly
    /// one of it.</summary>
    public void Setup(SoundArchive archive, IReadOnlyDictionary<string, SoundDef> defs,
        WeaponDefs? weapons)
    {
        _archive = archive;
        _defs = defs;
        _emptyClipName = WeaponAudioCues.EmptyClipName(weapons);
        var empty = WeaponAudioCues.EmptyClip(archive, defs, weapons);
        _emptyClip = MakePlayer(empty);
        _emptyClipVol = empty?.Volume ?? 1f;
        // The build half of the observable INSTR-45 asks for, WorldSounds.Debug's precedent: a cue
        // logged `unresolved` here never had a stream, one logged `ok` and later `culled` is silent by
        // distance alone. The gun loop's own line comes when a caliber first fires.
        Log.Info("sound", $"ai weapons {Aircraft()}: dry={SlotState(_emptyClipName, empty)} loop=on first burst");
    }

    /// <summary>Starts (or keeps playing) the gun firing loop for the given
    /// <c>LOOPED_SOUND_NAME</c>, from this aircraft's position. Called every frame the loop is wanted,
    /// so the distance cull rides here rather than on a per-frame call of its own; the loop exists
    /// only while the trigger is held.
    /// ⚠ Do not add a pitch term. `CAP-09` measured no Doppler on the original's world emitters, and
    /// Instant Action traffic is unmeasured, so any pitch here would be an invention.</summary>
    public void StartGunLoop(string? sndName)
    {
        if (_gunLoopName != sndName)
        {
            var cue = WeaponAudioCues.GunLoop(_archive, _defs, sndName);
            RebuildGunLoop(sndName, cue);
        }
        if (_gunLoop == null)
        {
            return;
        }
        float distSq = AudioListeners.NearestDistanceSq(this, Listeners);
        bool culled = distSq > _gunLoopCullSq;
        SetCulled(culled, distSq);
        if (culled)
        {
            _gunLoop.Stop();
            return;
        }
        if (!_gunLoop.Playing)
        {
            _gunLoop.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, _gunLoopVol));
            _gunLoop.Play();
        }
    }

    /// <summary>The trigger came off, or the aircraft respawned.</summary>
    public void StopGunLoop() => _gunLoop?.Stop();

    /// <summary>The dry-trigger cue, one shot when this aircraft's guns or pylons come up empty.
    /// Positional by decision: the original plays every aircraft's flat and at full volume from any
    /// distance (docs/org/weaponFire.md), and this port keeps the world emitter instead. Not
    /// culled by hand: the player's own <c>MaxDistance</c> already clips it silent past the
    /// definition's audible distance, and a one-shot holds a voice for a fraction of a second.</summary>
    public void PlayEmptyClip()
    {
        if (_emptyClip == null)
        {
            return;
        }
        _emptyClip.VolumeDb = Mathf.LinearToDb(Mathf.Max(SilenceThreshold, _emptyClipVol));
        _emptyClip.Play();
    }

    /// <summary>Kills the loop for good, because the aircraft is down and its crash animation owns
    /// everything audible from here on.</summary>
    public void Stop() => _gunLoop?.Stop();

    /// <summary>How many positional players this aircraft's weapon voice holds, and where they are.
    /// Internal so the emitter-count suite can read the pairing the log prints.</summary>
    internal IReadOnlyList<(string Name, Vector3 Position, float RangeMax)> Emitters()
    {
        var live = new List<(string, Vector3, float)>();
        if (_gunLoop != null)
        {
            live.Add((_gunLoopName ?? "?", _gunLoop.GlobalPosition, _gunLoop.MaxDistance));
        }
        if (_emptyClip != null)
        {
            live.Add((_emptyClipName ?? "?", _emptyClip.GlobalPosition, _emptyClip.MaxDistance));
        }
        return live;
    }

    // One cue's word in the build log: the definition asked for, and whether a positional stream came
    // back. `flat` is a definition carrying no 3D flag, which by the data's own sorting has no
    // distance model at all and so gets no world player here (docs/formats/sounds.md).
    private static string SlotState(string? sndName, WeaponSoundCue? cue) => cue switch
    {
        null => $"{sndName ?? "none"}(unresolved)",
        { Is3D: false } c => $"{c.Name}(flat)",
        { } c => $"{c.Name}(ok)",
    };

    // RANGE is [full-volume distance, audible distance], mapped onto Godot's inverse-distance curve
    // the way WorldSounds and AiEngineAudio map every other 3D emitter. A definition without the 3D
    // flag gets no player: the data says it has no distance model, so placing it in the world would
    // invent one. ⚠ The player hangs on this node and takes no muzzle offset: the original's fire
    // tick hands its firing loop the vehicle's own position (docs/org/weaponFire.md).
    private AudioStreamPlayer3D? MakePlayer(WeaponSoundCue? cue)
    {
        if (cue is not { Is3D: true } resolved)
        {
            return null;
        }
        var player = new AudioStreamPlayer3D
        {
            Stream = resolved.Stream,
            UnitSize = resolved.RangeMin,
            MaxDistance = resolved.RangeMax,
            VolumeDb = -60f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            Bus = AudioBuses.Effects,
        };
        AddChild(player);
        return player;
    }

    // The loop slot takes a new caliber: drop the old player rather than restream it, since the two
    // carry different RANGE pairs and a reused player would keep the first one's.
    private void RebuildGunLoop(string? sndName, WeaponSoundCue? cue)
    {
        if (_gunLoop != null)
        {
            _gunLoop.Stop();
            _gunLoop.QueueFree();
            _gunLoop = null;
        }
        _gunLoopName = null;
        _gunLoop = MakePlayer(cue);
        if (_gunLoop == null || cue is not { } resolved)
        {
            return;
        }
        _gunLoopName = sndName;
        _gunLoopVol = resolved.Volume;
        // The cull is the cue's OWN audible distance, not EngineAudioCurves.CullDistance: a gun loop
        // is authored audible to 150-550 m, far inside the engine routine's 2000, so that number
        // could never bite first and reading it here would be a borrowed constant.
        _gunLoopCullSq = resolved.RangeMax * resolved.RangeMax;
        _culled = null;   // re-armed with the slot, so the first frame of this caliber logs its verdict
        Log.Info("sound", $"ai weapons {Aircraft()}: loop={SlotState(sndName, cue)} cull={resolved.RangeMax:0} m");
    }

    // The first frame and every transition after it, always logged: audio cannot be
    // screenshot-verified (INSTR-45), and this line is what separates "silent because it is past the
    // cull" from "silent because its definition never resolved".
    private void SetCulled(bool culled, float distSq)
    {
        if (culled == _culled)
        {
            return;
        }
        _culled = culled;
        Log.Debug("sound", $"ai weapons {Aircraft()} {(culled ? "culled" : "audible")} at {Mathf.Sqrt(distSq):0} m (cull {Mathf.Sqrt(_gunLoopCullSq):0} m)");
    }

    // Which aircraft every line here is about: the controller this component hangs under, whose name
    // the spawner already logged. Never this node's own name, which is "WeaponAudio" on all of them.
    private string Aircraft() => GetParent()?.Name.ToString() ?? Name.ToString();
}
