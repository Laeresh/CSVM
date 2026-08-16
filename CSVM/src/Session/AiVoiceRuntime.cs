using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>Wires the E16 voice dispatch into a running flight session: subscribes the decoded
/// event sources on each registered aircraft, runs them through <see cref="AiVoiceDispatcher"/>,
/// and plays every decision through B8's seam only — <c>CombatVoice.PlayableFor</c> resolving the
/// name, <c>WorldSounds.HasStream</c> answering availability, and the source-following
/// <c>WorldSounds.PlayOneShot(name, Node3D, rng)</c> playing it from the speaker's own aircraft.
///
/// <para>The wired sites (the full wired/unwired table is
/// <c>docs/formats/combat-voice.md</c> "The remake's dispatch sites"): the DI distress tiers off
/// <see cref="FlightController.DamageApplied"/> (the projectile hit path's whole-vehicle summary,
/// 70/50/30 % most-severe-first); the death cry off <see cref="FlightController.Downed"/> — id 20
/// <c>DA</c> when the dying aircraft's <see cref="FlightController.Team"/> is
/// <see cref="AimAssist.PlayerTeam"/>, id 21 <c>DE</c> otherwise (every
/// free-flight/<c>--vs</c> AI still lands on its own default team, so <c>DA</c> stays dormant
/// there; a mission wingman on the player's team makes it reachable) — dispatched with force;
/// <c>WA-Attack</c> + the computed <c>WA-Enemy</c> bearing broadcast on the mode machine's
/// patrol→pursue transition against a human target (our chosen stand-in for the original's
/// undecoded "enemy spotted" event, marked as such); <c>TA-FailTail</c> spoken by the evading AI
/// target when its pursuer's sixth-sense stun lands; <c>TA-SucShk</c> when an AI's own
/// evade/evasive-maneuver reaction completes ("fires as the reaction flag clears"). The player's
/// aircraft registers only as a damage source: its health crossing 30 % broadcasts
/// <c>WA-HighDmg</c> to its own <see cref="FlightController.Team"/>.</para>
///
/// <para>Speakers register on their real <see cref="FlightController.Team"/> (B7 removed the
/// TEAMLESS stand-in this used before a team model existed): a broadcast now actually elects
/// among a caller's own side, live the moment a mission puts two AI on one team; free
/// flight/<c>--vs</c>, where every pilot still gets its own default team, is unaffected.</para></summary>
public sealed partial class AiVoiceRuntime : Node
{
    /// <summary>The player's WA-HighDmg broadcast threshold — decoded (id 13 fires when the
    /// player's health crosses 30 %).</summary>
    public const float PlayerHighDmgFraction = 0.30f;

    private readonly CombatVoice _voice;
    private readonly WorldSounds _sounds;
    private readonly AiVoiceDispatcher _dispatcher;
    private readonly Random _rng;
    private readonly Dictionary<int, FlightController> _bySpeaker = new();
    private readonly Dictionary<FlightController, float> _lastPlayerFraction = new();
    private float _now;

    public AiVoiceRuntime(CombatVoice voice, WorldSounds sounds, Random rng)
    {
        _voice = voice;
        _sounds = sounds;
        _rng = rng;
        _dispatcher = new AiVoiceDispatcher(rng, ResolvePlayable);
    }

    /// <summary>Every line actually played: (speaker node name, trigger id, resolved clip) —
    /// the observability seam the ai-voice suite counts.</summary>
    public event Action<string, int, string>? LinePlayed;

    /// <summary>The dispatcher, exposed for tests/probes (registration order, cooldown stamps).</summary>
    public AiVoiceDispatcher Dispatcher => _dispatcher;

    /// <summary>The mission clock the dispatch runs on, advanced by <see cref="Step"/>.</summary>
    public float Now => _now;

    /// <summary>Advances the mission clock one sim step (the 2 s mute window and every cooldown
    /// run on this, so a halted clock halts the chatter too). Called by the session on a
    /// parent-driven clock; <see cref="_PhysicsProcess"/> is the realtime path, same split as
    /// every other sim consumer.</summary>
    public void Step(float dt) => _now += dt;

    public override void _PhysicsProcess(double delta)
    {
        float dt = Utils.GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt > 0f)
        {
            Step(dt);
        }
    }

    /// <summary>Registers an AI aircraft as a voiced speaker: resolves the accent chain to one
    /// pilot VO id (a seeded pick over the accent's pool), then subscribes the wired event
    /// sources above. An accent that resolves to no voiced pilot logs once and registers
    /// nothing — a silent pilot, not an error.</summary>
    public void RegisterAi(FlightController ai, int accentId, float talkerChance,
        float constitutionChance)
    {
        int? voId = _voice.PilotFor(accentId, _rng);
        if (voId is not { } vo)
        {
            GD.Print($"ai voice: {ai.Name}: accent {accentId} resolves to no voiced pilot — silent");
            return;
        }
        var speaker = _dispatcher.Register(ai.PlayerIndex, vo, ai.Team,
            isPlayer: false, talkerChance, constitutionChance);
        _bySpeaker[ai.PlayerIndex] = ai;
        // An INERT aircraft (E10) is not in the session yet, so it neither speaks nor is elected
        // for a broadcast: the dispatcher's own aliveness gate carries it, mirrored here because
        // nothing in the dispatcher can see a FlightController. Registration order is unchanged —
        // the speaker is listed from the start, just not eligible until its wave launches.
        speaker.Alive = ai.InPlay;
        ai.InertChanged += plane => speaker.Alive = plane.InPlay;
        GD.Print($"ai voice: {ai.Name}: accent {accentId} -> VO id {vo} " +
                 $"(talker {talkerChance:0.00})");

        ai.DamageApplied += damaged =>
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
        if (ai.Pilot?.Machine is { } machine)
        {
            machine.ModeChanged += (from, to, why) => OnModeChanged(ai, speaker.Id, from, to, why);
        }
    }

    /// <summary>Registers a human rig as a damage source only (the player never speaks AI
    /// lines): its health crossing 30 % broadcasts <c>WA-HighDmg</c> to the flight.</summary>
    public void RegisterPlayer(FlightController rig)
    {
        _lastPlayerFraction[rig] = 1f;
        rig.DamageApplied += damaged =>
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

    private void OnModeChanged(FlightController ai, int speakerId, AiMode from, AiMode to,
        string why)
    {
        // Acquisition (our chosen dispatch point, marked in combat-voice.md): committing to an
        // attack on a human — the attacker's WA-Attack, and the flight's computed bearing
        // call-out in the warned player's own frame.
        if (to == AiMode.Pursue && from == AiMode.Patrol
            && ai.Pilot?.Gunner?.Target is { IsHumanPiloted: true } quarry)
        {
            Play(_dispatcher.Dispatch(speakerId, AiVoiceDispatcher.WaAttack, _now));
            int bearing = AiVoiceDispatcher.BearingTriggerFor(
                quarry.WorldPosition, quarry.NoseDirection, ai.WorldPosition);
            Play(_dispatcher.Broadcast(bearing, quarry.Team, _now));
        }

        // A pursuer's failed sixth-sense (tail) check stuns it; its AI target taunts.
        if (to == AiMode.Stunned && from is AiMode.Pursue or AiMode.LayOff
            && ai.Pilot?.Gunner?.Target is { IsHumanPiloted: false } evader
            && _dispatcher.Find(evader.PlayerIndex) != null)
        {
            Play(_dispatcher.Dispatch(evader.PlayerIndex, AiVoiceDispatcher.TaFailTail, _now));
        }

        // The reaction flag clearing: the decoded TA-SucShk dispatch point.
        if (from is AiMode.Evade or AiMode.EvasiveManeuver && why == "reaction complete")
        {
            Play(_dispatcher.Dispatch(speakerId, AiVoiceDispatcher.TaSucShk, _now));
        }
    }

    // B8's availability contract: the resolved name must have a decoded stream behind it — for a
    // variant group that means a playable member, which the group name itself cannot answer.
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
        if (decision.Clip is { } clip && node != null)
        {
            string? resolved = _sounds.PlayOneShot(clip, node, _rng);
            GD.Print($"ai voice: {tag}: trigger #{decision.TriggerId} -> {clip}" +
                     (resolved != null && resolved != clip ? $" ({resolved})" : "") +
                     $" ({decision.Outcome})");
            if (resolved != null)
            {
                LinePlayed?.Invoke(tag, decision.TriggerId, resolved);
            }
        }
        else if (decision.Rolled)
        {
            // The engine logs both roll outcomes; gate short-circuits (cooling, no clip) are
            // silent here — they fire at hit rate.
            GD.Print($"ai voice: {tag}: trigger #{decision.TriggerId} silent ({decision.Outcome})");
        }
    }
}
