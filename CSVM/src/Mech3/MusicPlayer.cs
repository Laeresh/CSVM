using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>The game states the score is keyed to. The names are the original's own: each maps to
/// one sound-group or definition name its data cues (docs/org/music.md).</summary>
public enum MusicState
{
    Silent,
    Menu,
    Prebattle,
    Battle,
    BattleSuccess,
    MissionSuccess,
    PrimaryObjective,
    SecondaryObjective,
    TertiaryObjective,
}

/// <summary>The state-driven score: one non-positional streaming channel shared by the menus, the
/// cabin and the mission, beside <see cref="WorldSounds"/>'s pooled 3D emitters. Cues arrive as
/// sound-set or sound-group names, exactly as the original's data names them, and one track plays
/// at a time: a new cue hard-cuts the old one and a repeat of the playing track is ignored.
/// The selection rules, the battle timer and the fade rates are decoded in
/// docs/org/music.md.</summary>
public sealed partial class MusicPlayer : Node
{
    /// <summary>Seconds of battle music one combat ping buys. A ping never shortens the hold, and
    /// the hold counts down only while battle music is actually playing (docs/org/music.md).</summary>
    public const float BattleHoldSeconds = 20f;

    /// <summary>Period of the original's proximity scan for the battle trigger, which runs only
    /// while battle music is silent. Its result reaches this player as <see cref="NoteCombat"/>.</summary>
    public const float BattleScanSeconds = 5f;

    /// <summary>Radius of that scan around the player, in metres. The original compares squared
    /// distance against 1e6.</summary>
    public const float BattleScanRadiusM = 1000f;

    /// <summary>How many other vehicles must be inside <see cref="BattleScanRadiusM"/> for the scan
    /// to ping. The original's test is "more than 2".</summary>
    public const int BattleScanMinNearby = 3;

    /// <summary>Gain per second while a tracked cue fades in, so full volume is reached in a
    /// quarter second. Only the battle group is tracked in the shipped data.</summary>
    public const float FadeInPerSecond = 4f;

    /// <summary>Gain per second while battle music fades out after its hold expires: four seconds
    /// to silence, then the channel stops.</summary>
    public const float FadeOutPerSecond = 0.25f;

    /// <summary>How many times the out-of-mission screens play the splash track before falling
    /// silent, from the shared sound object <c>GLOBALS.SCRIPT</c> creates.</summary>
    public const int MenuLoopCount = 5;

    /// <summary>⚠ TUNE, and a stand-in for a control that does not exist yet. The music channel is
    /// mixed this far below the master bus because at full gain it drowns the briefing narration at
    /// the controls. The original mixes music against a user setting; until an options menu exists
    /// there is nothing to read, so the level is fixed here. Remove it, do not re-tune it, once the
    /// options menu can carry a music slider.</summary>
    public const float ChannelLevel = 0.2f;

    /// <summary>Resolves a definition's WAV into a stream, the same seam
    /// <see cref="WorldSounds.Loader"/> uses. The bool is the stream's own forward-loop flag.</summary>
    public Func<SoundDef, bool, AudioStreamWav?>? Loader;

    private readonly IReadOnlyDictionary<string, SoundDef> _defs;
    private readonly IReadOnlyDictionary<string, SoundGroup> _groups;

    // The alternating pick for the three objective stinger families, in MusicState order
    // (primary, secondary, tertiary). The original overrides each group's weighted pick with a
    // per-family counter, so the two takes alternate from the first cue of a process.
    private readonly int[] _stingerTurn = new int[3];

    private readonly AudioStreamPlayer _player = new();
    private string _wav = string.Empty;
    private string _cue = string.Empty;
    private MusicState _state = MusicState.Silent;
    private bool _loopForever;
    private int _loopsLeft;
    private float _gain = 1f;
    private float _target = 1f;
    private float _fadeRate;
    private float _battleHold;

    public MusicPlayer(IReadOnlyDictionary<string, SoundDef> defs,
        IReadOnlyDictionary<string, SoundGroup>? groups = null)
    {
        _defs = defs;
        _groups = groups ?? new Dictionary<string, SoundGroup>();
    }

    /// <summary>The WAV file playing on the channel, or empty when it is silent.</summary>
    public string Current => _wav;

    /// <summary>The state the last accepted cue put the channel in.</summary>
    public MusicState State => _state;

    /// <summary>Whether the playing track restarts itself for ever. True for prebattle and battle,
    /// which the original forces looping regardless of their definitions' flags.</summary>
    public bool Looping => _loopForever;

    /// <summary>Seconds of battle music still owed by the last combat ping.</summary>
    public float BattleHold => _battleHold;

    /// <summary>The channel's current gain, which only the battle fade moves off 1.</summary>
    public float Gain => _gain;

    /// <summary>Whether the original's proximity scan would ping: it counts other vehicles inside
    /// <see cref="BattleScanRadiusM"/> of the player and fires above <see cref="BattleScanMinNearby"/>
    /// minus one.</summary>
    public static bool ScanPings(int nearbyVehicles) => nearbyVehicles >= BattleScanMinNearby;

    /// <summary>Plays the track one game state calls for and returns its WAV name, or null when
    /// nothing changed or nothing resolved. A state whose track is already playing is a no-op, so
    /// re-entering the cabin does not restart the splash track.</summary>
    public string? Enter(MusicState state, Random rng)
    {
        if (state == MusicState.Silent)
        {
            Stop();
            return null;
        }

        string? wav = Cue(CueNameOf(state), rng, LoopsOf(state), ForcedLoopOf(state));
        if (wav != null)
        {
            _state = state;
        }

        return wav;
    }

    /// <summary>Plays a cue named the way the original's data names it: a <c>SOUND_GROUPS</c> group
    /// (<c>music_*_sg</c>) or a plain <c>SETS</c> definition. Returns the WAV now playing, or null
    /// when the name is unknown, its stream is missing, or that track is already playing.
    /// ⚠ Do not add the original's 15 s music refusal here. It guards the objectives runtime's
    /// tracked-cue list, which no mission cue reaches, while every mission cue reaches this method
    /// (docs/org/music.md).</summary>
    public string? Cue(string name, Random rng, int loops = 1, bool forceLoop = false)
    {
        if (ResolveDef(name, rng) is not { } def)
        {
            Log.Warn("sound", $"music cue names no definition cue={name}");
            return null;
        }

        // Re-cueing the playing track never restarts it, so a mission that wakes the same
        // prebattle objective twice hears one continuous track.
        if (string.Equals(_wav, def.WavName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        bool loopForever = forceLoop || (def.Looped && loops <= 0);
        var stream = Loader?.Invoke(def, loopForever);
        if (stream == null)
        {
            Log.Warn("sound", $"music track has no stream cue={name} wav={def.WavName}");
            return null;
        }

        _player.Stream = stream;
        _wav = def.WavName;
        _cue = name;
        _loopForever = loopForever;
        _loopsLeft = loopForever ? 0 : Math.Max(loops, 1);
        _state = MusicState.Silent;
        SetGain(1f);
        _target = 1f;
        _fadeRate = 0f;
        _player.Play();
        Log.Info("sound", $"music play cue={name} wav={def.WavName} loop={(loopForever ? "forever" : _loopsLeft.ToString())}");
        return def.WavName;
    }

    /// <summary>Cuts the channel silent at once. The original's only gradual stop is the battle
    /// hold expiring, which <see cref="Tick"/> drives.</summary>
    public void Stop()
    {
        if (_wav.Length == 0)
        {
            return;
        }

        Log.Info("sound", $"music stop wav={_wav}");
        _player.Stop();
        _wav = string.Empty;
        _cue = string.Empty;
        _state = MusicState.Silent;
        _loopForever = false;
        _loopsLeft = 0;
        _fadeRate = 0f;
        SetGain(1f);
    }

    /// <summary>Reports that the player is in combat, which is what starts and sustains battle
    /// music. A ping refreshes the hold to <see cref="BattleHoldSeconds"/> and never shortens
    /// it.</summary>
    public void NoteCombat()
    {
        if (_battleHold < BattleHoldSeconds)
        {
            _battleHold = BattleHoldSeconds;
        }
    }

    /// <summary>Advances the channel: the battle hold and its fade, and the restart a looping or
    /// multi-play track needs when its stream ends. Call once per frame from the session.</summary>
    public void Tick(float dt, Random rng)
    {
        StepBattle(dt, rng);
        StepFade(dt);
        StepLoop();
    }

    public override void _Ready() => AddChild(_player);

    private static string CueNameOf(MusicState state) => state switch
    {
        MusicState.Menu => "snd_music_splash",
        MusicState.Prebattle => "music_prebattle_sg",
        MusicState.Battle => "music_battle_sg",
        MusicState.BattleSuccess => "music_battlesuccess_sg",
        MusicState.MissionSuccess => "music_missionsuccess_sg",
        MusicState.PrimaryObjective => "music_primaryobj_sg",
        MusicState.SecondaryObjective => "music_secondaryobj_sg",
        MusicState.TertiaryObjective => "music_tertiaryobj_sg",
        _ => string.Empty,
    };

    private static int LoopsOf(MusicState state) => state == MusicState.Menu ? MenuLoopCount : 1;

    // Prebattle and battle loop for ever because the resolver forces it, not because their
    // definitions say so: only battle1-6 carry LOOPED in the data.
    private static bool ForcedLoopOf(MusicState state) =>
        state is MusicState.Prebattle or MusicState.Battle;

    // Index of the stinger family a group name belongs to, or -1. The families are the only
    // groups whose member choice is a counter rather than the group's own weights.
    private static int StingerFamily(string name) => name switch
    {
        "music_primaryobj_sg" => 0,
        "music_secondaryobj_sg" => 1,
        "music_tertiaryobj_sg" => 2,
        _ => -1,
    };

    private SoundDef? ResolveDef(string name, Random rng)
    {
        if (_defs.TryGetValue(name, out var direct))
        {
            return direct;
        }

        if (!_groups.TryGetValue(name, out var group))
        {
            return null;
        }

        string? member = PickMember(name, group, rng);
        return member != null && _defs.TryGetValue(member, out var def) ? def : null;
    }

    private string? PickMember(string name, SoundGroup group, Random rng)
    {
        int family = StingerFamily(name);
        if (family < 0 || group.Members.Count == 0)
        {
            return group.Pick(rng);
        }

        int turn = _stingerTurn[family] % group.Members.Count;
        _stingerTurn[family]++;
        return group.Members[turn].Name;
    }

    private void StepBattle(float dt, Random rng)
    {
        bool battlePlaying = _state == MusicState.Battle && _wav.Length > 0;
        if (_battleHold > 0f)
        {
            if (battlePlaying)
            {
                _battleHold = Math.Max(_battleHold - dt, 0f);
                return;
            }

            if (Enter(MusicState.Battle, rng) != null)
            {
                SetGain(0f);
                _target = 1f;
                _fadeRate = FadeInPerSecond;
            }

            return;
        }

        if (battlePlaying && _fadeRate >= 0f)
        {
            _target = 0f;
            _fadeRate = -FadeOutPerSecond;
        }
    }

    private void StepFade(float dt)
    {
        if (_fadeRate == 0f)
        {
            return;
        }

        SetGain(Mathf.Clamp(_gain + (_fadeRate * dt), 0f, 1f));
        if (_fadeRate > 0f && _gain >= _target)
        {
            _fadeRate = 0f;
            return;
        }

        if (_fadeRate < 0f && _gain <= 0f)
        {
            Stop();
        }
    }

    private void StepLoop()
    {
        if (_wav.Length == 0 || _loopForever || _player.Playing || _loopsLeft <= 1)
        {
            return;
        }

        _loopsLeft--;
        _player.Play();
        Log.Debug("sound", $"music repeat cue={_cue} wav={_wav} left={_loopsLeft}");
    }

    private void SetGain(float gain)
    {
        // Gain stays the fade's own 0..1 value, so every assertion about the ramp reads what the
        // decode describes; ChannelLevel is applied at the mixer alone.
        _gain = gain;
        _player.VolumeDb = Mathf.LinearToDb(Math.Max(gain * ChannelLevel, 0.0001f));
    }
}
