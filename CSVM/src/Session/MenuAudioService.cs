using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The menu host's <see cref="IMenuAudio"/> over the process's own playback: the music channel
/// and the sound archive <c>Launcher</c> already keeps, plus the briefing narration player, which
/// used to be the launchscreen's, and a cue player for the menu's own sounds. Narration ducks the
/// music for as long as it is begun and lifts the duck on end, the whole-stay duck the briefing
/// asks for; a missing stream is silence, not a refusal. A cue resolves through
/// <see cref="MenuCueTable"/> to a wav under the extracted rof tree's sounds folder, decoded once
/// and replayed from the start on every ask; a name the table lacks or a file the tree lacks is
/// logged once and stays silent.
/// </summary>
public sealed partial class MenuAudioService : Node, IMenuAudio
{
    private readonly MusicPlayer? _music;
    private readonly Func<string, AudioStreamWav?> _sounds;
    private readonly string? _cueDir;
    private readonly Dictionary<string, AudioStreamWav?> _cues = new(StringComparer.Ordinal);
    private AudioStreamPlayer _narration = null!;
    private AudioStreamPlayer _cuePlayer = null!;
    // How many narrations have begun since the last end, so the log reads 1 on entry and 2 on
    // REPLAY BRIEFING, as the launchscreen's own counter did.
    private int _starts;

    /// <summary>A service over <paramref name="music"/> (null for a silent install) resolving
    /// narration WAVs through <paramref name="sounds"/> and menu cues under <paramref name="cueDir"/>
    /// (the extracted rof tree's <c>ASSETS/SOUNDS</c>; null leaves every cue silent).</summary>
    public MenuAudioService(MusicPlayer? music, Func<string, AudioStreamWav?> sounds, string? cueDir = null)
    {
        _music = music;
        _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        _cueDir = cueDir;
    }

    public override void _Ready()
    {
        // On the Master bus by default, which is where --volume=/audio.volume already applies.
        _narration = new AudioStreamPlayer { Name = "briefing_narration" };
        AddChild(_narration);
        _cuePlayer = new AudioStreamPlayer { Name = "menu_cue" };
        AddChild(_cuePlayer);
    }

    public void Cue(MenuCue cue)
    {
        if (!_cues.TryGetValue(cue.Name, out var stream))
        {
            stream = LoadCue(cue.Name);
            _cues[cue.Name] = stream;
        }

        if (stream == null)
        {
            return;
        }

        _cuePlayer.Stop();
        _cuePlayer.Stream = stream;
        _cuePlayer.Play();
        Log.Debug("sound", $"menu cue name={cue.Name} file={MenuCueTable.FileFor(cue.Name)}");
    }

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

    // The one decode per cue name. Every failure is a debug line and a null, cached like a hit,
    // so a menu cannot log once per frame about a sound it does not have.
    private AudioStreamWav? LoadCue(string name)
    {
        string? file = MenuCueTable.FileFor(name);
        if (file == null)
        {
            Log.Debug("sound", $"menu cue name={name} unresolved: not in the cue table");
            return null;
        }

        if (_cueDir == null)
        {
            Log.Debug("sound", $"menu cue name={name} file={file} silent: no cue directory");
            return null;
        }

        string path = Path.Combine(_cueDir, file);
        if (!File.Exists(path))
        {
            Log.Debug("sound", $"menu cue name={name} file={file} silent: not at {path}");
            return null;
        }

        try
        {
            var wav = WavFile.Parse(File.ReadAllBytes(path));
            var data = new byte[wav.Samples.Length * 2];
            Buffer.BlockCopy(wav.Samples, 0, data, 0, data.Length);
            return new AudioStreamWav
            {
                Data = data,
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = wav.SampleRate,
                Stereo = wav.Channels == 2,
                LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            Log.Warn("sound", $"menu cue name={name} file={file} failed to decode: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }
}
