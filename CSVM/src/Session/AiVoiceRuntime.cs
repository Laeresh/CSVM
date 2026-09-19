using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Wires the E16 voice dispatch into a running flight session: subscribes the decoded
/// event sources on each registered aircraft, runs them through <see cref="AiVoiceDispatcher"/>,
/// and plays every decision through <c>CombatVoice.PlayableFor</c> only, resolving the
/// name, <c>WorldSounds.HasStream</c> answering availability, and <see cref="MissionRadio.Speak"/>
/// queueing it on the flat Voice channel the objective callouts share, never at the speaker.
/// The wired/unwired dispatch-site table is docs/formats/combat-voice.md "The remake's dispatch
/// sites". Speakers register on their real <see cref="FlightController.Team"/>; a broadcast
/// elects among a caller's own side.
/// ⚠ Free flight and --vs give every pilot a default Team, so a broadcast still only elects
/// within a "teamless" match there, wiring engages once a mission shares an explicit team
/// between AI, or between an AI and the player.</summary>
public sealed partial class AiVoiceRuntime : Node
{
    /// <summary>The player's WA-HighDmg broadcast threshold, decoded (id 13 fires when the
    /// player's health crosses 30 %).</summary>
    public const float PlayerHighDmgFraction = 0.30f;

    private readonly CombatVoice _voice;
    private readonly WorldSounds _sounds;
    private readonly MissionRadio _radio;
    private readonly AiVoiceDispatcher _dispatcher;
    private readonly Random _rng;
    private readonly Dictionary<int, FlightController> _bySpeaker = new();
    private readonly Dictionary<int, FlightController> _byIndex = new();
    private readonly Dictionary<FlightController, float> _lastPlayerFraction = new();
    private readonly HashSet<int> _humans = new();
    private readonly HashSet<int> _watched = new();
    private readonly HashSet<int> _watchedKills = new();
    private readonly HashSet<int> _watchedFriendlyFire = new();
    private readonly HashSet<(int Speaker, string Family)> _noClipLogged = new();
    private float _now;

    public AiVoiceRuntime(CombatVoice voice, WorldSounds sounds, MissionRadio radio, Random rng)
    {
        _voice = voice;
        _sounds = sounds;
        _radio = radio;
        _rng = rng;
        _dispatcher = new AiVoiceDispatcher(rng, ResolvePlayable)
        {
            IsTalking = radio.IsSpeaking,
        };
    }

    /// <summary>Every line handed to the radio: (speaker tag, trigger id, resolved clip), the
    /// observability seam the ai-voice suite counts. A line the busy channel keeps waiting past
    /// its QUEUE tolerance is still dropped there, unheard.</summary>
    public event Action<string, int, string>? LinePlayed;

    /// <summary>The dispatcher, exposed for tests/probes (registration order, cooldown stamps).</summary>
    public AiVoiceDispatcher Dispatcher => _dispatcher;

    /// <summary>The mission clock the dispatch runs on, advanced by <see cref="Step"/>.</summary>
    public float Now => _now;

    /// <summary>Advances the mission clock one sim step (the 2 s mute window and every cooldown
    /// run on this, so a halted clock halts the chatter too). Called by SessionSimulation.</summary>
    public void Step(float dt) => _now += dt;

    /// <summary>Takes an AI aircraft, voiced or not. ⚠ Hand over EVERY AI the session builds: the
    /// mode machine is watched either way, because the bearing call-out and the taunt are spoken
    /// by a different aircraft than the one whose mode changed. A null accent, or one resolving to
    /// no voiced pilot, registers no speaker and is silent, not an error.</summary>
    public void RegisterAi(FlightController ai, int? accentId, float talkerChance,
        float constitutionChance)
    {
        WatchModes(ai);
        WatchKills(ai);
        WatchFriendlyFire(ai);
        if (accentId is not { } accent)
        {
            return;
        }
        int? voId = _voice.PilotFor(accent, _rng);
        if (voId is not { } vo)
        {
            Log.Info("sound", $"ai voice: {ai.Name}: accent {accent} resolves to no voiced pilot, silent");
            return;
        }
        var speaker = _dispatcher.Register(ai.PlayerIndex, vo, ai.Team,
            isPlayer: false, talkerChance, constitutionChance);
        _bySpeaker[ai.PlayerIndex] = ai;
        // An inert aircraft neither speaks nor is elected for a broadcast, mirrored here because
        // the dispatcher cannot see a FlightController's own aliveness.
        speaker.Alive = ai.InPlay;
        ai.InertChanged += plane => speaker.Alive = plane.InPlay;
        Log.Info("sound", $"ai voice: {ai.Name}: accent {accent} -> VO id {vo} (talker {talkerChance:0.00})");

        ai.DamageApplied += (damaged, _) =>
        {
            if (damaged.Damage is { } dmg)
            {
                Play(_dispatcher.NotifyDamage(speaker.Id, dmg.SummaryHealthFraction, _now));
            }
        };
        ai.Downed += (_, _) =>
        {
            speaker.Alive = false;
            Play(_dispatcher.DeathCry(speaker.Id, onPlayersTeam: ai.Team == AimAssist.PlayerTeam, _now));
        };
    }

    /// <summary>Registers a human rig as an event source; the player itself never speaks an AI
    /// line. Its health crossing 30 % broadcasts <c>WA-HighDmg</c> to the flight, and a kill of
    /// its own draws the flight's gloat (id 24).</summary>
    public void RegisterPlayer(FlightController rig)
    {
        _humans.Add(rig.PlayerIndex);
        WatchKills(rig);
        _lastPlayerFraction[rig] = 1f;
        rig.DamageApplied += (damaged, _) =>
        {
            if (damaged.Damage is not { } dmg)
            {
                return;
            }
            float fraction = dmg.SummaryHealthFraction;
            if (fraction < PlayerHighDmgFraction
                && _lastPlayerFraction[damaged] >= PlayerHighDmgFraction)
            {
                Play(_dispatcher.Broadcast(AiVoiceDispatcher.WaHighDmg, damaged.Team, _now));
            }
            _lastPlayerFraction[damaged] = fraction;
        };
    }

    // ⚠ Subscribed for every AI, not only for the registered speakers. Two of the sites below
    // dispatch on ANOTHER aircraft, and the shipped rosters leave nearly every enemy on accentID
    // -1, so watching only the voiced ones silences the player's own flight. Idempotent per
    // aircraft, since a spawn can be handed over more than once.
    private void WatchModes(FlightController ai)
    {
        if (ai.Pilot?.Machine is not { } machine || !_watched.Add(ai.PlayerIndex))
        {
            return;
        }
        machine.ModeChanged += (from, to, _) => OnModeChanged(ai, machine, from, to);
    }

    private void OnModeChanged(FlightController ai, AiModeMachine machine, AiMode from, AiMode to)
    {
        int speakerId = ai.PlayerIndex;
        // Acquisition (our chosen dispatch point, marked in combat-voice.md): committing to an
        // attack on a human, the attacker's WA-Attack, and the flight's computed bearing
        // call-out in the warned player's own frame.
        if (to == AiMode.Pursue && from == AiMode.Patrol
            && ai.Pilot?.Gunner?.AircraftTarget is { IsHumanPiloted: true } quarry)
        {
            Play(_dispatcher.Dispatch(speakerId, AiVoiceDispatcher.WaAttack, _now));
            int bearing = AiVoiceDispatcher.BearingTriggerFor(
                quarry.WorldPosition, quarry.NoseDirection, ai.WorldPosition);
            Play(_dispatcher.Broadcast(bearing, quarry.Team, _now));
        }

        // A pursuer's failed sixth-sense (tail) check stuns it; its AI target taunts.
        if (to == AiMode.Stunned && from is AiMode.Pursue or AiMode.LayOff
            && ai.Pilot?.Gunner?.AircraftTarget is { IsHumanPiloted: false } evader
            && _dispatcher.Find(evader.PlayerIndex) != null)
        {
            Play(_dispatcher.Dispatch(evader.PlayerIndex, AiVoiceDispatcher.TaFailTail, _now));
        }

        // The evade episode's end decides the taunt pair. A flag still standing is the pursuer's
        // nose inside the decoded tail cone (26); a cleared flag is the shake (27). A step
        // between the two evade modes is the same episode and stays silent.
        if (from is AiMode.Evade or AiMode.EvasiveManeuver
            && to is not (AiMode.Evade or AiMode.EvasiveManeuver))
        {
            Play(_dispatcher.Dispatch(speakerId,
                machine.Evading ? AiVoiceDispatcher.TaFailShk : AiVoiceDispatcher.TaSucShk, _now));
        }
    }

    // ⚠ Subscribed for every aircraft handed over, human rigs included. The gloat's speaker is the
    // KILLER, so the site needs the victim's team and the shooter id the death report carries, and
    // a killer with no voice of its own simply stays silent. Idempotent per aircraft.
    private void WatchKills(FlightController plane)
    {
        _byIndex[plane.PlayerIndex] = plane;
        if (!_watchedKills.Add(plane.PlayerIndex))
        {
            return;
        }
        plane.Downed += (_, killer) => OnDowned(plane, killer);
    }

    // The gloat, decoded polarity (combat-voice.md, "The gloat triggers and trigger 28"). The
    // friendly predicate over shooter and victim comes first, so a friendly kill picks no gloat at
    // all; the same predicate over the victim and the player's team then picks 22 or 23 for the
    // killer to speak. A kill by a human rig takes 24 instead and broadcasts on the player's side.
    private void OnDowned(FlightController victim, int? killer)
    {
        if (killer is not { } shooter || shooter == victim.PlayerIndex)
        {
            return;
        }
        int shooterTeam = _byIndex.TryGetValue(shooter, out var node)
            ? node.Team
            : AimAssist.TeamOfPilot(shooter);
        if (!AimAssist.Hostile(shooterTeam, victim.Team))
        {
            return;
        }
        if (_humans.Contains(shooter))
        {
            Play(_dispatcher.Broadcast(AiVoiceDispatcher.GlPlyrDwn, AimAssist.PlayerTeam, _now));
            return;
        }
        int trigger = AimAssist.Hostile(victim.Team, AimAssist.PlayerTeam)
            ? AiVoiceDispatcher.GlEnemyDwn
            : AiVoiceDispatcher.GlAllyDwn;
        Play(_dispatcher.Dispatch(shooter, trigger, _now));
    }

    // ⚠ Subscribed for AI only: the struck aircraft is the speaker here, and a human rig speaks no
    // AI line, so a player hit by a wingman's round stays silent. Idempotent per aircraft.
    private void WatchFriendlyFire(FlightController ai)
    {
        if (!_watchedFriendlyFire.Add(ai.PlayerIndex))
        {
            return;
        }
        ai.DamageApplied += OnFriendlyFire;
    }

    // The ally distress, the decoded first arm (combat-voice.md, "The gloat triggers and trigger
    // 28"): a round from the local player damages an aircraft the team predicate calls friendly,
    // and the STRUCK aircraft speaks. The second arm counts survivors among three undecoded
    // globals and waits on that decode.
    private void OnFriendlyFire(FlightController victim, int? shooter)
    {
        if (shooter is not { } id || !_humans.Contains(id))
        {
            return;
        }
        int shooterTeam = _byIndex.TryGetValue(id, out var node)
            ? node.Team
            : AimAssist.TeamOfPilot(id);
        if (AimAssist.Hostile(shooterTeam, victim.Team))
        {
            return;
        }
        Play(_dispatcher.Dispatch(victim.PlayerIndex, AiVoiceDispatcher.DsAlly, _now));
    }

    // B8's availability contract: the resolved name must have a decoded stream behind it, for a
    // variant group that means a playable member, which the group name itself cannot answer.
    // ⚠ An accent registered outside the roster (CLI, Instant Action) must join the prewarm set
    // (SessionPrewarmNames' extraAccents); unprewarmed, this returns null and the pilot is silent.
    private string? ResolvePlayable(int voId, string family)
    {
        string? name = _voice.PlayableFor(voId, family);
        if (name == null)
        {
            return null;
        }
        foreach (var clip in _voice.ClipsFor(voId, family))
        {
            if (_sounds.HasStream(clip))
            {
                return name;
            }
        }
        return null;
    }

    private void Play(AiVoiceDispatcher.Decision decision)
    {
        if (decision.Speaker is not { } speaker)
        {
            return;
        }
        string tag = _bySpeaker.TryGetValue(speaker.Id, out var node) ? node.Name : $"#{speaker.Id}";
        if (decision.Clip is { } clip)
        {
            // ⚠ Do not place a line at the speaker; the defs are QUEUE radio lines with no 3D flag,
            // and the original plays them flat through the one queue the objective callouts use.
            // The speaker rides along so the gate's already-talking test can read it back.
            string? resolved = _radio.Speak(clip, _rng, speaker.Id);
            Log.Info("sound", $"ai voice: {tag}: trigger #{decision.TriggerId} -> {clip}{(resolved != null && resolved != clip ? $" ({resolved})" : "")} ({decision.Outcome})");
            if (resolved != null)
            {
                LinePlayed?.Invoke(tag, decision.TriggerId, resolved);
            }
        }
        else if (decision.Outcome == AiVoiceDispatcher.NoClipOutcome)
        {
            LogNoClip(speaker, decision.TriggerId, tag);
        }
        else if (decision.Rolled)
        {
            // The engine logs both roll outcomes; the other gate short-circuits (cooling, muted)
            // are silent here, they fire at hit rate.
            Log.Info("sound", $"ai voice: {tag}: trigger #{decision.TriggerId} silent ({decision.Outcome})");
        }
    }

    // Once per speaker and family, since the refusal fires at hit rate. A pilot owning the family's
    // defs with none prewarmed is a missing prewarm accent, so it warns; owning none is data.
    private void LogNoClip(AiVoiceDispatcher.Speaker speaker, int triggerId, string tag)
    {
        string family = triggerId >= 0 && triggerId < CombatVoice.TriggerFamilies.Count
            ? CombatVoice.TriggerFamilies[triggerId]
            : "";
        if (!_noClipLogged.Add((speaker.Id, family)))
        {
            return;
        }
        if (_voice.ClipsFor(speaker.VoId, family).Count > 0)
        {
            Log.Warn("sound", $"ai voice: {tag}: trigger #{triggerId} ({family}) silent, VO id {speaker.VoId} owns the clips but none was prewarmed");
        }
        else
        {
            Log.Info("sound", $"ai voice: {tag}: trigger #{triggerId} ({family}) silent, VO id {speaker.VoId} owns no such clip");
        }
    }
}
