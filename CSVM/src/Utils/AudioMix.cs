using System;
using Godot;

namespace CSVM.Utils;

/// <summary>The four saved levels as one value, each null where the options file has never carried
/// it. <see cref="AudioMix.Apply"/> takes them straight through, since it falls back to the shipped
/// default per category, so nothing between the file and the mixer has to decide what a null
/// means.</summary>
public readonly record struct AudioLevels(int? Master, int? Music, int? Effects, int? Voice);

/// <summary>
/// The player's mix: four 0..100 levels turned into one gain per category bus and written there.
/// Master multiplies the other three rather than being a level of its own, so the three child buses
/// carry the whole mix. ⚠ Bus 0 is never written here; it carries the developer <c>--volume=</c>
/// gain and the focus mute alone (<c>Session/Launcher.cs</c>), and a level written there could
/// un-silence a scripted run. A level of 0 lands on the same -80 dB floor that file uses, because
/// <c>LinearToDb(0)</c> is negative infinity. The arithmetic is a pure function of the levels;
/// <see cref="Apply"/> is the only member that touches the mixer.
/// </summary>
public static class AudioMix
{
    /// <summary>The lowest level a slider takes, and real silence: the authored <c>MinValue</c> is
    /// 1, whose far-left is about -40 dB and still audible in a quiet room.</summary>
    public const int MinLevel = 0;

    /// <summary>The highest level, at which a category sits at its bus's resting gain.</summary>
    public const int MaxLevel = 100;

    /// <summary>The Master level a fresh install opens on: full, so each category below it stands
    /// at its own default.</summary>
    public const int DefaultMaster = 100;

    /// <summary>The Music level a fresh install opens on, the authored <c>CurrentValue</c> of the
    /// Music Volume row.</summary>
    public const int DefaultMusic = 50;

    /// <summary>The Effects level a fresh install opens on, the authored <c>CurrentValue</c> of the
    /// Effects Volume row.</summary>
    public const int DefaultEffects = 50;

    /// <summary>The Voice level a fresh install opens on, the authored <c>CurrentValue</c> of the
    /// Voice Volume row.</summary>
    public const int DefaultVoice = 50;

    // Gain floor for the dB conversion, the value Session/Launcher.cs floors the developer volume
    // at: LinearToDb(0) is negative infinity, and -80 dB is inaudible.
    private const float Floor = 0.0001f;

    /// <summary>The linear gain a category level runs at under a master level, both clamped to
    /// <see cref="MinLevel"/>..<see cref="MaxLevel"/>. 100 under 100 is exactly 1, which is the
    /// bus's resting gain.</summary>
    public static float Gain(int category, int master) =>
        Clamp(category) / (float)MaxLevel * (Clamp(master) / (float)MaxLevel);

    /// <summary>That gain as a bus volume in dB, floored so a level of 0 reads -80 dB rather than
    /// negative infinity.</summary>
    public static float VolumeDb(int category, int master) =>
        Mathf.LinearToDb(Math.Max(Gain(category, master), Floor));

    /// <summary>The saved levels a launch applies, or none at all under <paramref name="det"/>,
    /// which then leaves every category on its shipped default.
    /// ⚠ A deterministic run reads no saved option: <c>options.json</c> is one machine's state, and
    /// a level saved at the controls reaching a scripted run would make its mix a function of who
    /// ran it. This is the levels' one reader, so the drop is one place rather than one per
    /// caller.</summary>
    public static AudioLevels SavedLevels(bool det)
    {
        if (det)
        {
            return default;
        }

        var saved = OptionsStore.UserOptions().Load();
        return new AudioLevels(saved.AudioMaster, saved.AudioMusic, saved.AudioEffects, saved.AudioVoice);
    }

    /// <summary>Writes the three category buses, taking the shipped default for any level the
    /// caller has not saved. Safe to call at any time: this is both the startup apply and the live
    /// one, and a caller that reapplies the same levels writes the same gains.</summary>
    public static void Apply(int? master = null, int? music = null, int? effects = null, int? voice = null)
    {
        int m = Clamp(master ?? DefaultMaster);
        int mu = Clamp(music ?? DefaultMusic);
        int ef = Clamp(effects ?? DefaultEffects);
        int vo = Clamp(voice ?? DefaultVoice);
        SetBus(AudioBuses.Music, mu, m);
        SetBus(AudioBuses.Effects, ef, m);
        SetBus(AudioBuses.Voice, vo, m);
        Log.Info("sound", $"mix master={m} music={mu} effects={ef} voice={vo} (Master bus untouched)");
    }

    private static int Clamp(int level) => Math.Clamp(level, MinLevel, MaxLevel);

    // ⚠ Refuses index 0 as well as an unresolved name, so a renamed or missing bus cannot make this
    // write the developer gain instead. Godot answers -1 for a name no bus carries.
    private static void SetBus(string bus, int category, int master)
    {
        int index = AudioServer.GetBusIndex(bus);
        if (index <= 0)
        {
            Log.Warn("sound", $"mix: no child bus named {bus}; its level does not apply");
            return;
        }

        AudioServer.SetBusVolumeDb(index, VolumeDb(category, master));
    }
}
