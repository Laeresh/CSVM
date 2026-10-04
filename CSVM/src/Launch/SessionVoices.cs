using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>A flight's mission radio and combat voice, one step of the session build. It also
/// holds the ai_skill_parameters table that the voice and the AI activation floor both read.
/// Its <see cref="Build"/> raises both over the world's prewarmed sounds and registers every human.
/// Each AI spawn then registers through <see cref="RegisterAi"/>. The session ticks the radio and
/// the voice in its own step order.
/// Module entry: docs/architecture/Launch.md on src/Launch/SessionVoices.cs.</summary>
internal sealed class SessionVoices
{
    private readonly SessionSpec _spec;
    private readonly string _zrdrPath;
    private readonly Node3D _worldRoot;
    private readonly IReadOnlyList<PlayerRig> _rigs;
    private readonly IReadOnlyList<PlayerRig> _seatRigs;
    private readonly IReadOnlyList<Net.NetSeat> _netSeats;
    // Whether the match is on a wire, whose people speak as their lobby pilots. A local match's
    // roster carries no voice, so its panes stay voiceless as without one.
    private readonly bool _onWire;
    // The roster the AI fly from, whose skills table is the first choice. Set by Build.
    private FlightRoster? _roster;
    // ai_skill_parameters, loaded once on the first need: the roster's own copy when it has read
    // one, the file otherwise.
    private AiSkills? _aiSkills;

    /// <summary>The voices over one flight build: its panes, its seats and the match's seat roster.
    /// The roster is empty in a local match without bots. <paramref name="onWire"/> says whether
    /// it is a network match's.</summary>
    public SessionVoices(SessionSpec spec, string zrdrPath, Node3D worldRoot,
        IReadOnlyList<PlayerRig> rigs, IReadOnlyList<PlayerRig> seatRigs,
        IReadOnlyList<Net.NetSeat> netSeats, bool onWire)
    {
        _spec = spec;
        _zrdrPath = zrdrPath;
        _worldRoot = worldRoot;
        _rigs = rigs;
        _seatRigs = seatRigs;
        _netSeats = netSeats;
        _onWire = onWire;
    }

    /// <summary>The mission radio queue the campaign's objective callouts speak on, null in a
    /// soundless or world-less session.</summary>
    public MissionRadio? Radio { get; private set; }

    /// <summary>The E16 voice dispatch, null in a soundless or world-less session, chatter
    /// simply off.</summary>
    public AiVoiceRuntime? Voice { get; private set; }

    /// <summary>Builds the radio and the voice runtime over the world's WorldSounds and the sound
    /// defs, before any AI spawn so each can register. The players register as event sources, and
    /// a network seat as its chosen pilot too.</summary>
    public void Build(BuildState state, ProjectilePool projectiles, FlightRoster roster)
    {
        _roster = roster;
        if (state.WorldRuntime?.Sounds is not { } worldSounds
            || state.SoundDefs is not { } vDefs || state.SoundGroups is not { } vGroups)
        {
            return;
        }

        // The radio plays the streams the world's prewarm already decoded, so a callout survives
        // the sound archive's build scope closing exactly as a one-shot does.
        var radio = Radio = new MissionRadio(vDefs, vGroups, worldSounds.StreamFor);
        worldSounds.Radio = radio;
        _worldRoot.AddChild(radio);
        var combatVoice = new CombatVoice(vDefs, vGroups, CombatVoice.LoadAccents(state.ZrdrPath));
        var voice = Voice = new AiVoiceRuntime(combatVoice, worldSounds, radio, Rng.NewSystemRandom(Rng.Ai));
        _worldRoot.AddChild(voice); // its realtime tick; freed with the world subtree
        // WA-Turret: subscribed to the pool, not to a turret list, so the emplacements built
        // further down and every carried gunner report through one seam.
        voice.WatchTurrets(projectiles);
        RegisterPlayers(voice);
        if (_spec.CaptureTheFlag)
        {
            worldSounds.Prewarm(FlagRuntime.VoiceLines);
        }

        if (_spec.ZeppelinVsZeppelin)
        {
            worldSounds.Prewarm(ZeppelinVersusRuntime.VoiceLines);
        }
    }

    /// <summary>player.json's activation floor, on the lazily loaded skills table the spawner
    /// reads. The shipped default stands in when the table cannot be read, so a roster still
    /// spawns.</summary>
    public float MinAiActiveDist()
    {
        try
        {
            _aiSkills ??= AiSkills.Load(_zrdrPath);
            return _aiSkills.MinAiActiveDist;
        }
        catch (Exception e)
        {
            GD.PushWarning($"campaign: cannot load ai_skill_parameters: {e.Message}");
            return 2000f;
        }
    }

    /// <summary>Gives a spawned AI aircraft its voice. Each chance comes from ai_skill_parameters
    /// at its own override when given, else the session's skill rating, so talker and
    /// constitution read two independent curves. A missing accent or skills table means a silent
    /// pilot whose mode machine is still watched; no voice runtime means nothing, never an error.
    /// </summary>
    public void RegisterAi(FlightController? ai, int? accentId, int? talkerOverride = null,
        int? constitutionOverride = null)
    {
        if (ai == null || Voice == null)
        {
            if (accentId != null && Voice == null)
            {
                Log.Info("sound", $"ai voice: accent {accentId} ignored, no voice runtime in this session");
            }
            return;
        }
        _aiSkills ??= _roster?.AiSkills;
        // ⚠ Hand over every AI, accent or none. The runtime watches an accentless aircraft's mode
        // machine, and the shipped rosters leave nearly every enemy on accentID -1. Dropping those
        // here silences the call-outs the player's own flight speaks about them.
        if (_aiSkills is not { } skills)
        {
            Voice.RegisterAi(ai, null, 0f, 0f);
            return;
        }
        int talkerRating = talkerOverride ?? _spec.AiAttackSkill ?? 5;
        int constitutionRating = constitutionOverride ?? _spec.AiAttackSkill ?? 5;
        Voice.RegisterAi(ai, accentId, skills.At("talker_chance", talkerRating),
            skills.At("constitution_chance", constitutionRating));
    }

    // Every human aircraft joins the voice runtime. Outside a network match that is each pane's,
    // voiceless. In one, every human seat speaks on every machine as the pilot its roster voice
    // names, rolling the session's talker rating. A bot seat is silent and joins nothing.
    private void RegisterPlayers(AiVoiceRuntime voice)
    {
        var seats = _netSeats;
        if (!_onWire || seats.Count == 0)
        {
            foreach (var rig in _rigs)
            {
                if (rig.Controller is { } human)
                {
                    voice.RegisterPlayer(human);
                }
            }
            return;
        }

        // A match may fly no AI, so the table the spawner loads on its first AI may not be read yet.
        try
        {
            _aiSkills ??= _roster?.AiSkills ?? AiSkills.Load(_zrdrPath);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            Log.Warn("sound", $"player voice: cannot load ai_skill_parameters, so no player speaks: {e.Message}");
        }

        int rating = _spec.AiAttackSkill ?? 5;
        float talker = _aiSkills?.At("talker_chance", rating) ?? 0f;
        float constitution = _aiSkills?.At("constitution_chance", rating) ?? 0f;
        for (int seat = 0; seat < seats.Count && seat < _seatRigs.Count; seat++)
        {
            if (_seatRigs[seat].Controller is not { } human || seats[seat].IsBot)
            {
                continue;
            }
            int? voId = UI.Menu.PilotVoices.SpeakerFor(seats[seat].Voice);
            if (seats[seat].HasPane)
            {
                voice.RegisterPlayer(human, voId, talker, constitution);
            }
            else
            {
                voice.RegisterRemotePlayer(human, voId, talker, constitution);
            }
        }
    }
}
