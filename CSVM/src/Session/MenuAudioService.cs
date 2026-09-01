using System;
using CSVM.Mech3;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The menu host's <see cref="IMenuAudio"/> over the process's own playback: the music channel
/// and the sound archive <c>Launcher</c> already keeps, plus the briefing narration player, which
/// used to be the launchscreen's. Narration ducks the music for as long as it is begun and lifts
/// the duck on end, the whole-stay duck the briefing asks for; a missing stream is silence, not a
/// refusal. Built-in requests no cues today, so <see cref="Cue"/> only records the ask.
/// </summary>
public sealed partial class MenuAudioService : Node, IMenuAudio
{
    private readonly MusicPlayer? _music;
    private readonly Func<string, AudioStreamWav?> _sounds;
    private AudioStreamPlayer _narration = null!;
    // How many narrations have begun since the last end, so the log reads 1 on entry and 2 on
    // REPLAY BRIEFING, as the launchscreen's own counter did.
    private int _starts;

    /// <summary>A service over <paramref name="music"/> (null for a silent install) resolving
    /// narration WAVs through <paramref name="sounds"/>.</summary>
    public MenuAudioService(MusicPlayer? music, Func<string, AudioStreamWav?> sounds)
    {
        _music = music;
        _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
    }

    public override void _Ready()
    {
        // On the Master bus by default, which is where --volume=/audio.volume already applies.
        _narration = new AudioStreamPlayer { Name = "briefing_narration" };
        AddChild(_narration);
    }

    public void Cue(MenuCue cue) =>
        Log.Debug("sound", $"menu cue name={cue.Name} unresolved: no menu cue table yet");

    public void BeginNarration(string wavName)
    {
        ArgumentNullException.ThrowIfNull(wavName);
        if (_music != null)
        {
            _music.Ducked = true;
        }

        _narration.Stop();
        _narration.Stream = wavName.Length > 0 ? _sounds(wavName) : null;
        if (_narration.Stream != null)
        {
            _narration.Play();
        }

        _starts++;
        // Through the sink, not GD.Print: a scripted run at --volume=0 has no sound to hear, and
        // this line is what says the narration started at all.
        string got = _narration.Stream != null ? "yes" : "no";
        Log.Info("sound", $"briefing narration start={_starts} wav={wavName} stream={got}");
    }

    public void EndNarration()
    {
        _starts = 0;
        if (_music != null)
        {
            _music.Ducked = false;
        }

        if (_narration.Playing)
        {
            _narration.Stop();
        }

        _narration.Stream = null;
    }
}
