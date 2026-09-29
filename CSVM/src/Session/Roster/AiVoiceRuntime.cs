using System;
using System.Collections.Generic;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.Roster;

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

    /// <summary>How near a hostile player flown elsewhere must be to taunt a local player. Decoded:
    /// <c>FUN_00470750</c>, the 1695.0 at <c>0x00607e78</c> (docs/formats/combat-voice.md).</summary>
    public const float HumanTauntRange = 1695f;

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
    private readonly Dictionary<int, float> _nextCallOut = new();
    private readonly Dictionary<int, int> _accentTurns = new();
    private readonly HashSet<int> _remoteHumans = new();
    private readonly HashSet<int> _tauntInRange = new();
    private readonly Dictionary<int, float> _lastHull = new();
    private int? _localTeam;
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

    /// <summary>Every raise about an AI that reads what only the flying end knows, before the gate.
    /// It carries the AI, the trigger id, and the broadcast team or null for an addressed line. A
    /// host relays these to its guests, and a guest raises them again from
    /// <see cref="TakeRaise"/>.</summary>
    internal event Action<FlightController, int, int?>? Raised;

    /// <summary>The dispatcher, exposed for tests/probes (registration order, cooldown stamps).</summary>
    public AiVoiceDispatcher Dispatcher => _dispatcher;

    /// <summary>The mission clock the dispatch runs on, advanced by <see cref="Step"/>.</summary>
    public float Now => _now;

    // The team the local player flies for, which the original's per-peer voice arms compare against.
    private int LocalTeam => _localTeam ?? AimAssist.PlayerTeam;

    /// <summary>Advances the mission clock one sim step. The 2 s mute window and every cooldown run
    /// on it, so a halted clock halts the chatter too. It then raises the attack pair for every
    /// pursuer holding a human, and the taunts of every hostile player flown elsewhere. Called by
    /// SessionSimulation.</summary>
    public void Step(float dt)
    {
        _now += dt;
        RaiseAttackCallOuts();
        RaiseHumanTaunts();
    }

    /// <summary>Takes an AI aircraft, voiced or not. ⚠ Hand over EVERY AI the session builds: the
    /// mode machine is watched either way, because the bearing call-out the commit raises is
    /// broadcast and is spoken by a different aircraft than the one whose mode changed. A null
    /// accent, or one resolving to no voiced pilot, registers no speaker and is silent, not an
    /// error.</summary>
    public void RegisterAi(FlightController ai, int? accentId, float talkerChance,
        float constitutionChance)
    {
        WatchModes(ai);
        IndexAndWatchKills(ai);
        WatchFriendlyFire(ai);
        if (accentId is not { } accent)
        {
            return;
        }
        // ⚠ Deal the pilot by registration turn, never from _rng; each network end's stream is its own.
        // A host and its guests register the same AI in the same order, so they deal the same pilot.
        int turn = _accentTurns.GetValueOrDefault(accent);
        _accentTurns[accent] = turn + 1;
        int? voId = _voice.PilotFor(accent, turn);
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
            if (damaged.Damage is { } dmg && !damaged.RemoteOwned)
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

    /// <summary>Registers a human rig flown here as an event source. Its health crossing 30 %
    /// broadcasts <c>WA-HighDmg</c> to the flight. A kill of its own addresses the gloat (id 24) to
    /// the rig itself and broadcasts <c>PR-EnemyDwn</c> (id 16). The gloat speaks only for a rig
    /// given a pilot <paramref name="voId"/>, a network player's chosen voice. A stunt run the rig
    /// already carries is watched for its completed zones (id 15). The first rig names the local side.</summary>
    public void RegisterPlayer(FlightController rig, int? voId = null, float talkerChance = 0f,
        float constitutionChance = 0f)
    {
        _humans.Add(rig.PlayerIndex);
        _localTeam ??= rig.Team;
        IndexAndWatchKills(rig);
        RegisterHumanVoice(rig, voId, talkerChance, constitutionChance);
        _lastPlayerFraction[rig] = 1f;
        if (rig.Stunt is { } stunt)
        {
            stunt.ZoneCompleted += _ => DangerZoneCompleted(rig);
        }
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

    /// <summary>Registers a network player's aircraft flown on another machine, speaking as pilot
    /// <paramref name="voId"/> when its player chose a voice. This end derives its lines as each of
    /// the original's peers does. They are the taunt pair, the DI tiers on the local side, the gloat
    /// and the death cry (docs/formats/combat-voice.md, "A player's own voice").</summary>
    public void RegisterRemotePlayer(FlightController rig, int? voId, float talkerChance,
        float constitutionChance)
    {
        _remoteHumans.Add(rig.PlayerIndex);
        IndexAndWatchKills(rig);
        RegisterHumanVoice(rig, voId, talkerChance, constitutionChance);
    }

    /// <summary>Wires the <c>WA-Turret</c> site (id 0): every gunner in the session, carried or
    /// emplaced, reports its first clear sight of a human player through the shared pool, and the
    /// flight broadcasts on that player's team through the ordinary gate. Called once per
    /// session, since the pool is what both turret families are built against.</summary>
    public void WatchTurrets(ProjectilePool projectiles) =>
        projectiles.TurretAcquiredPlayer += OnTurretAcquired;

    /// <summary>The <c>PR-DngrZn</c> site (id 15): a Danger Zone the player has just flown, praised
    /// by one of that player's own flight. The original raises it inside the zone's completion
    /// routine, where both gates read crossed (<c>FUN_00446990</c>, <c>0x004469ff</c>). That
    /// routine is reached for the local player's vehicle alone (<c>FUN_0048e580</c>,
    /// <c>0x0048ea1f</c>), so no AI completion speaks it.</summary>
    public void DangerZoneCompleted(FlightController player) =>
        Play(_dispatcher.Broadcast(AiVoiceDispatcher.PrDngrZn, player.Team, _now));

    /// <summary>A raise the host's copy of <paramref name="ai"/> made, run through this end's own
    /// gate as the original's peers each run theirs. A <paramref name="team"/> is a broadcast
    /// elected among this end's speakers on that team.</summary>
    internal void TakeRaise(FlightController ai, int trigger, int? team)
    {
        Play(team is { } side
            ? _dispatcher.Broadcast(trigger, side, _now)
            : _dispatcher.Dispatch(ai.PlayerIndex, trigger, _now));
        Raised?.Invoke(ai, trigger, team);
    }

    /// <summary>The DI distress for a replicated AI, derived from the hull fraction the host sends.
    /// The original derives it the same way, from the state message's health byte.</summary>
    internal void TakeHull(FlightController ai, float fraction) =>
        Play(_dispatcher.NotifyDamage(ai.PlayerIndex, fraction, _now));

    /// <summary>The DI distress of a player flown elsewhere, off the hull fraction its owner sends.
    /// The original raises it only for a remote on the local player's side, and only as the health
    /// falls (<c>FUN_00498170</c>, <c>0x004985cf</c>).</summary>
    internal void TakeRemotePlayerHull(FlightController rig, float fraction)
    {
        int id = rig.PlayerIndex;
        float last = _lastHull.GetValueOrDefault(id, 1f);
        _lastHull[id] = fraction;
        if (!_remoteHumans.Contains(id) || fraction >= last || AimAssist.Hostile(rig.Team, LocalTeam))
        {
            return;
        }

        SyncAlive(rig);
        Play(_dispatcher.NotifyDamage(id, fraction, _now));
    }

    /// <summary>The taunt pair of every hostile player flown elsewhere within range of a local
    /// player, addressed to that player's aircraft. The original's per-remote update raises it every
    /// frame (<c>FUN_00470750</c>, <c>0x00470822</c>, <c>0x00470849</c>). 25 waits one frame in
    /// range, the <c>+0x1082</c> latch. The bearing broadcast after it is not raised. It elects from
    /// the local flight's AI, and no network match of the remake puts AI beside a hostile player.</summary>
    internal void RaiseHumanTaunts()
    {
        foreach (int id in _remoteHumans)
        {
            // An airframe swap frees the aircraft a seat was registered with.
            if (!_byIndex.TryGetValue(id, out var remote) || !IsInstanceValid(remote)
                || _dispatcher.Find(id) is not { } speaker)
            {
                continue;
            }
            var quarry = remote.InPlay ? NearestLocalHostile(remote) : null;
            bool wasInRange = _tauntInRange.Contains(id);
            if (quarry == null)
            {
                _tauntInRange.Remove(id);
                continue;
            }
            _tauntInRange.Add(id);
            if (AiVoiceDispatcher.TauntTriggerFor(remote.WorldPosition, remote.NoseDirection,
                    quarry.WorldPosition) is not { } taunt
                || (taunt == AiVoiceDispatcher.TaFailTail && !wasInRange)
                || _now < speaker.NextAllowedAt(taunt))
            {
                continue;
            }
            SayAsPlayer(id, taunt);
        }
    }

    // One raise of the pair: the pursuer's own WA-Attack and the flight's bearing call-out. The
    // bearing is computed in the warned player's frame and broadcast on the player's side. The
    // taunt pair rides the same raise, addressed to the pursuer off its own nose against the quarry.
    // ⚠ The mute window is read here as well as in the gate. A raise the window refuses must not
    // consume the interval. Otherwise a pursuer that commits on the mission's first frame would
    // stay silent for the next fifteen seconds.
    internal void RaiseAttackCallOut(FlightController ai, FlightController quarry)
    {
        if (_now < AiVoiceDispatcher.MuteWindowS)
        {
            return;
        }
        _nextCallOut[ai.PlayerIndex] = _now + AiVoiceDispatcher.SlotCooldownS;
        // The decoded block refuses the taunt while the pursuer is itself in an evade reaction,
        // and a pilot with no mode machine is not evading.
        if (ai.Pilot?.Machine is not { Evading: true }
            && AiVoiceDispatcher.TauntTriggerFor(ai.WorldPosition, ai.NoseDirection,
                quarry.WorldPosition) is { } taunt)
        {
            Say(ai, taunt);
        }
        Say(ai, AiVoiceDispatcher.WaAttack);
        int bearing = AiVoiceDispatcher.BearingTriggerFor(
            quarry.WorldPosition, quarry.NoseDirection, ai.WorldPosition);
        Play(_dispatcher.Broadcast(bearing, quarry.Team, _now));
        Raised?.Invoke(ai, bearing, quarry.Team);
    }

    // The human a pursuer attacks, or null. ⚠ Keep the InPlay and Hostile gates: a downed pursuer's
    // gunner still holds its target, and its wreck would raise "six low" every 15 s. They are the
    // beeper paint gate's own pair, and the original's combat driver never runs for a downed aircraft.
    // A replicated AI's target is the host's to know, and its raises arrive from there.
    private static FlightController? HumanQuarryOf(FlightController ai) =>
        ai.InPlay && !ai.RemoteOwned && ai.Pilot?.Gunner?.AircraftTarget is { IsHumanPiloted: true } quarry
            && AimAssist.Hostile(ai.Team, quarry.Team)
            ? quarry
            : null;

    // The turret warning is a broadcast, so the gunner is not the speaker: one of the warned
    // player's own flight says it, elected on that player's team (decoded, id 0 broadcasts).
    private void OnTurretAcquired(TurretController turret, FlightController player) =>
        Play(_dispatcher.Broadcast(AiVoiceDispatcher.WaTurret, player.Team, _now));

    // ⚠ Subscribed for every AI, not only for the registered speakers. The commit below raises a
    // broadcast bearing call-out that ANOTHER aircraft speaks, and the shipped rosters leave
    // nearly every enemy on accentID -1, so watching only the voiced ones silences the player's
    // own flight. Idempotent per aircraft, since a spawn can be handed over more than once.
    private void WatchModes(FlightController ai)
    {
        if (ai.Pilot?.Machine is not { } machine || !_watched.Add(ai.PlayerIndex))
        {
            return;
        }
        machine.ModeChanged += (from, to, _) => OnModeChanged(ai, machine, from, to);
    }

    // ⚠ A replicated AI's copy of the machine must never voice anything. Its rolls diverge from the
    // host's, so it would speak transitions the host's AI never made.
    private void OnModeChanged(FlightController ai, AiModeMachine machine, AiMode from, AiMode to)
    {
        if (ai.RemoteOwned)
        {
            return;
        }

        // The commit itself, for the case where it falls after the mute window. ⚠ Only out of
        // patrol: a re-entry from avoid crash happens every few seconds near terrain and is the
        // same engagement, which the raise interval would absorb anyway.
        if (to == AiMode.Pursue && from == AiMode.Patrol && HumanQuarryOf(ai) is { } quarry)
        {
            RaiseAttackCallOut(ai, quarry);
        }

        // The shake taunt at the evade episode's end, and only with the flag already cleared: the
        // original raises it where the flag drops and nowhere else. An episode left with the flag
        // still up says nothing here, its pursuer speaks the pair off its own geometry instead.
        if (from is AiMode.Evade or AiMode.EvasiveManeuver
            && to is not (AiMode.Evade or AiMode.EvasiveManeuver)
            && !machine.Evading)
        {
            Say(ai, AiVoiceDispatcher.TaSucShk);
        }
    }

    // An addressed raise the flying end alone can make, played here and offered to the relay.
    private void Say(FlightController ai, int trigger)
    {
        Play(_dispatcher.Dispatch(ai.PlayerIndex, trigger, _now));
        Raised?.Invoke(ai, trigger, null);
    }

    // The original raises the attack pair every frame its pursuer targets the local player. The
    // 15 s slot cooldown sets the rate (docs/formats/combat-voice.md, "The pursue path").
    // ⚠ Do not raise every tick here: the broadcast election walks and resolves every speaker.
    // No slot can speak twice inside that cooldown, so the raise runs at the cooldown's interval.
    // Losing the human, or leaving play, clears the stamp, so a fresh engagement raises at once.
    private void RaiseAttackCallOuts()
    {
        foreach (var ai in _byIndex.Values)
        {
            if (HumanQuarryOf(ai) is not { } quarry)
            {
                _nextCallOut.Remove(ai.PlayerIndex);
                continue;
            }
            if (_nextCallOut.TryGetValue(ai.PlayerIndex, out float next) && _now < next)
            {
                continue;
            }
            RaiseAttackCallOut(ai, quarry);
        }
    }

    // ⚠ Subscribed for every aircraft handed over, human rigs included. The gloat's speaker is the
    // KILLER, so the site needs the victim's team and the shooter id the death report carries. A
    // killer with no voice of its own simply stays silent. The same handover puts the aircraft
    // into the index the team lookups and the bearing call-outs read. Idempotent per aircraft.
    private void IndexAndWatchKills(FlightController plane)
    {
        _byIndex[plane.PlayerIndex] = plane;
        if (!_watchedKills.Add(plane.PlayerIndex))
        {
            return;
        }
        plane.Downed += (_, killer) => OnDowned(plane, killer);
    }

    // A shooter id may belong to an aircraft never handed over, a turret or a rig that left. The
    // roster's own team table is the fallback for those.
    private int TeamOf(int pilot) =>
        _byIndex.TryGetValue(pilot, out var node) ? node.Team : AimAssist.TeamOfPilot(pilot);

    // The gloat, decoded polarity (combat-voice.md, "The gloat triggers and trigger 28"). The
    // friendly predicate over shooter and victim comes first, so a friendly kill picks no gloat at
    // all; the same predicate over the victim and the player's team then picks 22 or 23 for the
    // killer to speak. A kill by a human rig takes the player arm instead, 24 and then 16.
    private void OnDowned(FlightController victim, int? killer)
    {
        if (_remoteHumans.Contains(victim.PlayerIndex))
        {
            OnRemotePlayerDowned(victim, killer);
            return;
        }
        if (killer is not { } shooter || shooter == victim.PlayerIndex)
        {
            return;
        }
        if (!AimAssist.Hostile(TeamOf(shooter), victim.Team))
        {
            return;
        }
        if (_remoteHumans.Contains(shooter))
        {
            // The take-hit death branch's arm for a killer that is not the local player, in that
            // player's own voice. It is 22 when the victim is on the local side, else 23.
            SayAsPlayer(shooter, AimAssist.Hostile(victim.Team, LocalTeam)
                ? AiVoiceDispatcher.GlEnemyDwn
                : AiVoiceDispatcher.GlAllyDwn);
            return;
        }
        if (_humans.Contains(shooter))
        {
            // ⚠ Do not make the broadcast the else-branch of the addressed line: the decoded
            // null-slot test jumps INTO it, so 16 runs whether or not the player's own rig
            // resolved a voice set, and it always elects from the local player's team.
            SayAsPlayer(shooter, AiVoiceDispatcher.GlPlyrDwn);
            Play(_dispatcher.Broadcast(AiVoiceDispatcher.PrEnemyDwn, AimAssist.PlayerTeam, _now));
            return;
        }
        int trigger = AimAssist.Hostile(victim.Team, AimAssist.PlayerTeam)
            ? AiVoiceDispatcher.GlEnemyDwn
            : AiVoiceDispatcher.GlAllyDwn;
        if (!_byIndex.TryGetValue(shooter, out var killerAi))
        {
            Play(_dispatcher.Dispatch(shooter, trigger, _now));
        }
        else if (!killerAi.RemoteOwned && !killerAi.IsHumanPiloted)
        {
            // Which AI scored is the host's to know, so a replicated killer's gloat is relayed.
            Say(killerAi, trigger);
        }
    }

    // A player flown elsewhere died, the death handler FUN_00498bf0's voice arm
    // (docs/formats/combat-voice.md, "A player's own voice"). ⚠ Do not voice a kill by a player
    // flown here. That arm returns before the gloat and the cry, so only the victim's machine speaks.
    private void OnRemotePlayerDowned(FlightController victim, int? killer)
    {
        _lastHull.Remove(victim.PlayerIndex);
        if (killer is { } local && _humans.Contains(local))
        {
            return;
        }
        bool victimOurs = !AimAssist.Hostile(victim.Team, LocalTeam);
        if (killer is { } shooter && _remoteHumans.Contains(shooter) && shooter != victim.PlayerIndex
            && AimAssist.Hostile(TeamOf(shooter), victim.Team))
        {
            bool killerOurs = !AimAssist.Hostile(TeamOf(shooter), LocalTeam);
            if (victimOurs != killerOurs)
            {
                SayAsPlayer(shooter, victimOurs ? AiVoiceDispatcher.GlAllyDwn : AiVoiceDispatcher.GlEnemyDwn);
            }
        }
        Play(_dispatcher.DeathCry(victim.PlayerIndex, victimOurs, _now));
    }

    // A player's own line, addressed to its aircraft and derived on this end, never relayed.
    // Aliveness is read off the aircraft each time, since a player comes back from every death.
    private void SayAsPlayer(int id, int trigger)
    {
        if (_byIndex.TryGetValue(id, out var rig))
        {
            SyncAlive(rig);
        }
        Play(_dispatcher.Dispatch(id, trigger, _now));
    }

    private void SyncAlive(FlightController rig)
    {
        if (_dispatcher.Find(rig.PlayerIndex) is { } speaker)
        {
            speaker.Alive = IsInstanceValid(rig) && rig.InPlay;
        }
    }

    // A player's aircraft speaks as the pilot its player chose, or not at all. It is never elected
    // for a broadcast, which walks the AI of the local flight.
    private void RegisterHumanVoice(FlightController rig, int? voId, float talkerChance, float constitutionChance)
    {
        if (voId is not { } vo || _dispatcher.Find(rig.PlayerIndex) != null)
        {
            return;
        }
        _dispatcher.Register(rig.PlayerIndex, vo, rig.Team, isPlayer: true, talkerChance, constitutionChance);
        _bySpeaker[rig.PlayerIndex] = rig;
        Log.Info("sound", $"player voice: {rig.Name}: VO id {vo} (talker {talkerChance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)})");
    }

    // The nearest player flown here that is in play, hostile to the remote, and in taunt range.
    private FlightController? NearestLocalHostile(FlightController remote)
    {
        FlightController? nearest = null;
        float best = HumanTauntRange * HumanTauntRange;
        foreach (int id in _humans)
        {
            if (_byIndex.TryGetValue(id, out var local) && IsInstanceValid(local) && local.InPlay
                && AimAssist.Hostile(remote.Team, local.Team)
                && remote.WorldPosition.DistanceSquaredTo(local.WorldPosition) is var d && d <= best)
            {
                best = d;
                nearest = local;
            }
        }
        return nearest;
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
        if (shooter is not { } id || !_humans.Contains(id) || victim.RemoteOwned)
        {
            return;
        }
        if (AimAssist.Hostile(TeamOf(id), victim.Team))
        {
            return;
        }
        Say(victim, AiVoiceDispatcher.DsAlly);
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
            // Both roll outcomes are logged; the other gate short-circuits (cooling, muted)
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
