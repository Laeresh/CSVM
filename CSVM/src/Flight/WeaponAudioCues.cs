using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>One resolved weapon sound: the stream a player takes, the definition's own unscaled
/// <c>VOLUME</c>, its authored <c>RANGE</c> pair, and whether it carries <c>3D</c>. The range and the
/// flag travel with the cue so a positional player's <c>UnitSize</c>, <c>MaxDistance</c> and cull
/// threshold cannot disagree with the definition they came from.</summary>
/// <param name="Name">The <c>sounds.json</c> definition name, for the log line.</param>
/// <param name="Stream">The decoded WAV, already forward-looped for a firing loop.</param>
/// <param name="Volume">The definition's <c>VOLUME</c>, unscaled: no mix gain, no distance term.</param>
/// <param name="RangeMin"><c>RANGE</c>'s full-volume distance, m.</param>
/// <param name="RangeMax"><c>RANGE</c>'s audible distance, m.</param>
/// <param name="Is3D">The positional opt-in; a definition without it has no distance model at all.</param>
public readonly record struct WeaponSoundCue(
    string Name, AudioStreamWav Stream, float Volume, float RangeMin, float RangeMax, bool Is3D);

/// <summary>
/// The weapon-sound selection every aircraft shares: a definition name to a resolved
/// <see cref="WeaponSoundCue"/>. The pilot's own <see cref="FlightAudio"/> and the positional
/// <see cref="AiWeaponAudio"/> read their cues from here rather than each keeping a copy, the way
/// both engine paths read <see cref="EngineAudioCurves"/>. It selects and nothing else: which player
/// a cue lands on, what gain it takes and whether it is culled are the two paths' own business,
/// which is what keeps own-ship concepts (splitscreen mix gain, the pilot's near-miss cue) out of the
/// world. Definitions: docs/formats/sounds.md.
/// </summary>
public static class WeaponAudioCues
{
    /// <summary>The dry-trigger cue, <c>weapons.json</c>'s <c>NO_AMMO_WARNING</c> value in the
    /// shipped data (<see cref="WeaponDefs.EmptyClipSound"/> is the read of record). Named once here
    /// because both audio paths play it and a second literal would let them drift.</summary>
    public const string EmptyClipDef = "snd_emptyclip";

    /// <summary>The sustained-fire loop for a caliber's <c>LOOPED_SOUND_NAME</c>, or null when the
    /// name is empty, unknown to <c>sounds.json</c>, or its WAV will not decode.</summary>
    public static WeaponSoundCue? GunLoop(SoundArchive? archive,
        IReadOnlyDictionary<string, SoundDef>? defs, string? sndName) =>
        Resolve(archive, defs, sndName, looped: true);

    /// <summary>A weapon one-shot (the dry-trigger cue), same resolution as
    /// <see cref="GunLoop"/> without the forced loop.</summary>
    public static WeaponSoundCue? OneShot(SoundArchive? archive,
        IReadOnlyDictionary<string, SoundDef>? defs, string? sndName) =>
        Resolve(archive, defs, sndName, looped: false);

    // ⚠ A firing loop is decoded LOOPED whatever its definition says, because it is a sustained-fire
    // cue held while the trigger is; SoundArchive caches per (wav, looped), so the flag is also the
    // prewarm key (WeaponDefs.SoundCues). Everything else takes the definition's own flag.
    private static WeaponSoundCue? Resolve(SoundArchive? archive,
        IReadOnlyDictionary<string, SoundDef>? defs, string? sndName, bool looped)
    {
        if (string.IsNullOrEmpty(sndName) || archive == null || defs == null
            || !defs.TryGetValue(sndName!, out var def))
        {
            return null;
        }
        var stream = archive.Find(def.WavName, looped || def.Looped);
        return stream == null
            ? null
            : new WeaponSoundCue(def.Name, stream, def.Volume, def.RangeMin, def.RangeMax, def.Is3D);
    }
}
