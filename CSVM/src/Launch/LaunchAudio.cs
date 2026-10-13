using CSVM.Bindings;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The process's sound outside a flight's own. It holds the one music channel and its archive, the
/// cinemas that pause it and the boot sequence. It also holds the master bus's gain and focus mute.
/// Built with the launcher and outliving every session; what it plays goes up under the launcher's
/// node. Module notes: docs/architecture/Launch.md.
/// </summary>
internal sealed class LaunchAudio(Node host)
{
    // Master-bus index. default_bus_layout.tres sends Music, Effects and Voice into Master. A gain or
    // a mute written here reaches every sound and leaves a player's own mix on the child buses alone.
    private const int MasterBus = 0;

    // The score, and the archive it streams from. Both are process-lifetime, unlike the build-scoped
    // SessionArchives.Sounds. One channel has to survive a mission launch, or the cabin track would
    // restart every time the player left a board. See docs/org/music.md.
    private MusicPlayer? _music;
    private SoundArchive? _archive;
    private System.Random _rng = new();
    // The cinema last put up, which StopCinema ends when a co-op host's film ends before the guest's.
    private UI.Screens.CinemaScreen? _cinemaShown;
    // The data root the films are read from, settled by Build.
    private string _dataRoot = string.Empty;
    // The master gain ApplyMasterVolume resolved, which a cinema warns against when it is zero.
    private float _masterVolume = MasterVolume.Unattenuated;

    // Alt-tabbing away silences the game; alt-tabbing back restores it, via an AudioServer
    // master-bus mute. One bus write reaches every player at once, where a factor would have to be
    // threaded through FlightAudio's and WorldSounds' own gain code.
    // ⚠ `--mute` cannot be reused for this: it is load-time and never constructs the audio
    // players, so there is nothing here to toggle.
    private bool _focusMuted;

    /// <summary>The process's music channel, or null when the sound archive or the sound
    /// definitions would not load.</summary>
    public MusicPlayer? Music => _music;

    /// <summary>A sound off the score's archive by its WAV name, or null where none opened.</summary>
    public AudioStreamWav? FindSound(string wav) => _archive?.Find(wav, false, warn: false);

    /// <summary>Writes the master output gain <see cref="MasterVolume"/> resolves, its only caller.
    /// Deliberately not --mute: at volume 0 both audio paths still load, play, count and log, so
    /// the run is silent but not blind. ⚠ Bus 0 alone: <see cref="AudioMix"/> writes the player's
    /// four levels on the three buses under it, so a saved level cannot lift a run this silenced.
    /// </summary>
    public void ApplyMasterVolume(float? asked, bool exported)
    {
        // The exported switch is the one that settles the log directory, so an export's audible
        // default and its logs\ folder are decided together.
        float volume = MasterVolume.Resolve(asked, exported);
        _masterVolume = volume;
        string source = asked.HasValue ? "--volume" : "config";
        // Full volume is the bus's own resting state, so leaving it alone keeps a full-volume
        // launch byte-identical in both output and console log.
        if (Mathf.IsEqualApprox(volume, MasterVolume.Unattenuated))
        {
            return;
        }
        AudioServer.SetBusVolumeDb(MasterBus, MasterVolume.VolumeDb(volume));
        string note = volume <= 0f ? ", sounds still load, play, count and log" : "";
        Log.Info("sound", $"master volume={volume:0.###} via={source}{note}");
    }

    /// <summary>Mutes or unmutes the master bus and gates pad reads, on window focus. Idempotent,
    /// since the notification can arrive more than once. It only ever clears a mute it set itself,
    /// so it cannot stomp on a mute from anywhere else.</summary>
    public void SetFocusMuted(bool muted)
    {
        if (_focusMuted == muted)
        {
            return;
        }
        _focusMuted = muted;
        AudioServer.SetBusMute(MasterBus, muted);
        // Pad reads follow focus (Pads.For), so a stick drifting while alt-tabbed cannot fly
        // the plane. The roster deliberately does not, see Pads.cs's docs/architecture.md
        // entry. Keyboard needs no gate: Godot releases held keys on focus loss.
        Pads.Focused = !muted;
        if (muted)
            Log.Info("sound", $"focus: lost, audio muted, pad reads gated");
        else
            Log.Info("sound", $"focus: regained, audio restored, pad reads live");
    }

    /// <summary>Opens the music channel and its archive under <paramref name="paths"/>, seeded from
    /// the master. Everything here is optional. An install without soundsh or a readable sounds.json
    /// leaves the game silent, which is what a missing extraction has always meant.
    /// </summary>
    public void Build(BasePaths paths, ulong masterSeed)
    {
        _dataRoot = paths.DataRoot;
        try
        {
            _archive = new SoundArchive(paths.SoundsPath);
            _music = new MusicPlayer(SoundDefs.Load(paths.ZrdrPath), SoundDefs.LoadGroups(paths.ZrdrPath))
            {
                Loader = (def, looped) => _archive.Find(def.WavName, looped, warn: false),
            };
            host.AddChild(_music);
            _rng = new System.Random((int)(masterSeed & 0x7fffffff));
        }
        catch (System.Exception e)
        {
            _music = null;
            _archive = null;
            Log.Warn("sound", $"music: no channel this process ({e.GetType().Name}: {e.Message})");
        }
    }

    /// <summary>Advances the score's fades by <paramref name="delta"/> wall seconds.</summary>
    public void Tick(float delta) => _music?.Tick(delta, _rng);

    /// <summary>Stops the menu score as the session leaves the boards for a mission.</summary>
    public void StopMusic() => _music?.Stop();

    /// <summary>Cues the menu track. A cue for the track already playing is a no-op, which is
    /// exactly what the original's own mail(11004) does (docs/org/music.md).</summary>
    public void CueMenu() => _music?.Enter(MusicState.Menu, _rng);

    /// <summary>Plays one cinema over the whole window and runs <paramref name="then"/> on the
    /// frame it stops, whether it played out or was skipped. A name that resolves to no readable
    /// file runs the continuation straight away. A flow then costs a screen rather than stalling on
    /// a cinema this install does not carry. This is the seam every movie sequence goes
    /// through.</summary>
    public void PlayCinema(string name, System.Action then, Video.CinemaSkip skip = UI.Screens.CinemaScreen.BootKeys)
    {
        if (UI.Screens.CinemaScreen.Open(_dataRoot, name, skip) is not { } cinema)
        {
            Log.Warn("ui", $"cinema {name} not played; looked under {Extraction.SessionPaths.CinemaFolder(_dataRoot)}");
            then();
            return;
        }

        // A repo run's developer gain defaults to zero. A cinema exists for its sound track, so there
        // silence reads as a defect rather than as a quiet run.
        if (_masterVolume <= 0f)
        {
            Log.Warn("sound", $"cinema {name} is playing at master volume 0, pass --volume=1.0 to hear it");
        }

        Log.Info("ui", $"cinema {name} playing skip={skip}");

        // Every film carries its own sound track, so the score must not play under it. The track
        // waits where it stopped and resumes under the screen the film hands to.
        if (_music != null)
        {
            _music.Paused = true;
        }

        cinema.Ended = () =>
        {
            if (_music != null)
            {
                _music.Paused = false;
            }

            then();
        };
        _cinemaShown = cinema;
        host.AddChild(cinema);
    }

    /// <summary>Ends the cinema <see cref="PlayCinema"/> last put up, as a skip does, when it is
    /// still showing. A co-op guest's film stops this way when its host's does.</summary>
    public void StopCinema()
    {
        if (_cinemaShown is { } cinema && GodotObject.IsInstanceValid(cinema) && !cinema.Finished)
        {
            cinema.Stop();
        }

        _cinemaShown = null;
    }

    /// <summary>The boot sequence in fmv.zrd's own order, its card and waits and fade included;
    /// the reader's eight actions live in <c>BootSequence</c>. A press ends the action it lands in
    /// and the next begins. Whether the original abandoned the rest of the block instead is not
    /// decoded (docs/formats/cinemas.md).</summary>
    public void PlayBoot(System.Action then) =>
        UI.Screens.BootCard.Play(host, _dataRoot, PlayCinema, then);

    /// <summary>Closes the score's archive on an ordinary quit.</summary>
    public void Exit()
    {
        _archive?.Dispose();
        _archive = null;
    }
}
