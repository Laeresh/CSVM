using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>The engine side of one Dogfight, split screen or over the wire. It owns the
/// <see cref="VersusMatch"/> and its scoring off every seat's Downed report, the respawn rotation
/// and its grants, and the lives line. It owns the host's match state, ending and rematch, and
/// opens the team modes' runtimes: the flags, the two hulls and the rearm bases. The session
/// builds it ahead of the roster, calls each phase in its build order and steps it in its own
/// simulation order. The wire's combat stays the session's, which hands a death here.
/// Module entry: docs/architecture/Session.md on src/Session/World/VersusDirector.cs.</summary>
public sealed class VersusDirector
{
    /// <summary>Seconds a downed pilot watches the crash cam before auto-respawning; R skips
    /// early. The return is at the pilot's own spawn point, full, with no invulnerability.
    /// UNDECODED: the original's delay is the crash def's RESET_TIME
    /// (docs/org/multiplayer-scoring.md). An Instant Action life spends the same delay.</summary>
    internal const float RespawnDelay = 3f;

    private readonly SessionSpec _spec;
    private readonly Field _field;

    // Built on the host alone in a network match, since two rotations diverge on first blood.
    // A guest holds none and takes every placement off the wire.
    private VersusSpawnRotation? _rotation;
    // The spawn list the session was placed from, kept so a granted spawn resolves its entry
    // index against the same table on every peer.
    private IReadOnlyList<SpawnPoint>? _spawnList;
    private string _spawnListName = "";
    // Which seats have an unanswered spawn ask out, so a due crash timer asks the host once per
    // death rather than once per step.
    private bool[] _spawnAsked = Array.Empty<bool>();
    // The entry each seat was last granted, which is what every peer must agree on.
    private int[] _spawnEntries = Array.Empty<int>();
    // Who downed each seat last, which the rotation weighs heaviest. Filled by every rig's Downed
    // report, a seat flown elsewhere included, since its owner's death report raises that here too.
    private int?[] _lastKiller = Array.Empty<int?>();
    // Each seat's deaths as the lives line last read them, so a death posts its line once.
    private int[] _livesSeen = Array.Empty<int>();
    // When the host repeats the match state, null on a guest and outside a match. A guest never
    // holds one, which is what makes the host the only writer of the clock.
    private Net.MatchStateCadence? _matchCadence;
    // The leave-the-session door a Zeppelin vs Zeppelin rematch takes, null where no menu stands
    // behind the flight.
    private Action? _toLobby;
    // --debug-scoreboard: fires once, on the first sim step.
    private bool _debugKillFired;

    private VersusDirector(SessionSpec spec, Field field, VersusMatch match)
    {
        _spec = spec;
        _field = field;
        Match = match;
    }

    /// <summary>The match's scorekeeping. On a guest it is the mirror of the host's, written from
    /// the score messages rather than counted here.</summary>
    internal VersusMatch Match { get; }

    /// <summary>Capture the Flag's flags, null outside a <c>--ctf</c> network match.</summary>
    internal FlagRuntime? Flags { get; private set; }

    /// <summary>Zeppelin vs Zeppelin's hulls, null outside a <c>--zvz</c> network match.</summary>
    internal ZeppelinVersusRuntime? ZvzPlay { get; private set; }

    /// <summary>The rearm bases, null in a match whose world holds none.</summary>
    internal RearmRuntime? RearmPlay { get; private set; }

    /// <summary>Why the match stopped, as the host named it, <c>Running</c> until one does. The
    /// same value on every machine: the host writes it where it sends the state and a guest where
    /// it applies one.</summary>
    internal Net.NetMatchEnd End { get; private set; }

    /// <summary>How many host grants this machine has placed an aircraft from, the host's own
    /// included. A suite reads it to tell a placement that came off the wire from one the shared
    /// seed walked to. That is the line between the opening spawn and every respawn.</summary>
    internal int SpawnsTaken { get; private set; }

    /// <summary>The spawn table entry each seat was last granted, -1 where it has had none. This
    /// is the placement itself rather than where the aeroplane now stands.</summary>
    internal IReadOnlyList<int> SpawnEntries => _spawnEntries;

    /// <summary>Whether this machine is a network guest, whose rematch is its host's to call.
    /// </summary>
    internal bool RematchIsTheHosts => _field.NetSeats.Count > 0 && _field.Net is not { IsHost: true };

    /// <summary>Each seat's lobby team in a team Dogfight, by seat, or null for any other flight.
    /// Every machine reads the same roster, so every machine puts the same seats on the same teams.
    /// </summary>
    internal static int[]? SeatTeams(SessionSpec spec, IReadOnlyList<Net.NetSeat> seats)
    {
        if (!spec.Versus || !seats.Any(seat => seat.TeamId > 0))
        {
            return null;
        }

        return seats.Select(seat => seat.TeamId).ToArray();
    }

    /// <summary>The spawn table's block each seat walks. Zeppelin vs Zeppelin opens each side in
    /// the block the map lays around its own hull, whatever the side's lobby team number is.
    /// </summary>
    internal static int[]? SpawnTeams(SessionSpec spec, IReadOnlyList<Net.NetSeat> seats) =>
        spec.ZeppelinVsZeppelin && SeatTeams(spec, seats) is { } teams
            ? ZeppelinVersus.SpawnBlocks(teams)
            : SeatTeams(spec, seats);

    /// <summary>The match of a <c>--vs</c> launch, built ahead of the rigs because every pane's
    /// HUD binds this one instance; null for any other launch. The score and respawn plumbing runs
    /// once every rig exists (<see cref="Wire"/>).</summary>
    internal static VersusDirector? TryCreate(SessionSpec spec, Field field, string zrdrPath,
        IReadOnlyDictionary<int, string>? teamNames)
    {
        if (!spec.Versus)
        {
            return null;
        }

        var match = new VersusMatch(field.SeatRigs.Count, spec.VsKills, spec.VsTimeMinutes * 60f, spec.VsLives,
            MatchScores.Load(zrdrPath, why => Log.Warn("flight", $"dogfight: player.zrd unreadable, scoring the executable's fallbacks: {why}")));
        if (SeatTeams(spec, field.NetSeats) is { } seatTeams)
        {
            match.AssignTeams(seatTeams, teamNames);
        }

        return new VersusDirector(spec, field, match);
    }

    /// <summary>The match bookkeeping, fed by every rig's Downed report. A killer inside the
    /// roster scores a kill. Anything else is a death with no killer and costs a point.
    /// ⚠ Do not guard post-completion events here. The match ignores them, and rigs report facts.
    /// </summary>
    internal void Wire(WireInputs inputs)
    {
        var match = Match;
        var seatRigs = _field.SeatRigs;
        _spawnList = inputs.SpawnList;
        _spawnListName = inputs.SpawnListName;
        _toLobby = inputs.ToLobby;
        // Spawn rotation: a downed seat comes back on a point picked against the living field,
        // since a fixed spawn can be camped at. Its Rng comes off the master alone, so no pick
        // here shifts Rng.Spawn. A team match rotates each seat inside its own team's block.
        var rotationRng = new Random(Rng.IntSeedFor(Rng.VersusSpawn));

        // ⚠ Never on a guest: a second rotation diverges on first blood.
        _rotation = _field.NetSeats.Count > 0 && _field.Net is not { IsHost: true }
            ? null
            : inputs.Spawns.SeatEntries is { } openings && inputs.Spawns.SeatBlocks is { } blocks
                ? VersusSpawnRotation.ForBlocks(inputs.SpawnList, openings, blocks, SpawnTeams(_spec, _field.NetSeats)!, rotationRng)
                : VersusSpawnRotation.For(inputs.SpawnList, inputs.SpawnBase, seatRigs.Count, rotationRng);
        // Who downed each seat last, which the rotation weighs heaviest: the Downed report
        // carries it, and the respawn that reads it happens seconds later.
        _lastKiller = new int?[seatRigs.Count];
        _livesSeen = new int[seatRigs.Count];
        var lastKiller = _lastKiller;
        var reportDeath = inputs.ReportDeath;
        foreach (var rig in seatRigs)
            if (rig.Controller is { } pilot)
            {
                int seat = rig.Index;
                // Crash cam, then back in, R skips. With the lobby's Auto Respawn off the same
                // crash cam runs and then waits for Fire Guns, as the original's does. ⚠ Never on a
                // bot seat, which has no Fire Guns to press and would stay down for the match.
                pilot.AutoRespawnAfter = RespawnDelay;
                pilot.RespawnOnFire = !_spec.VsAutoRespawn && !IsBot(seat);
                pilot.Match = match;                  // R-ownership gate: board-up ⇒ rematch
                pilot.RestartMatch = Restart;
                if (_field.NetSeats.Count > 0)
                    pilot.RespawnRequest = () => AskSpawn(seat);
                else if (_rotation != null)
                    pilot.RespawnPlacement = () => Respawn(seat, lastKiller[seat]);
                pilot.Downed += (victim, killer) =>
                {
                    if (victim >= 0 && victim < lastKiller.Length)
                        lastKiller[victim] = killer;
                    // On the wire a death is a report, not a score. The seat's owner sends
                    // it and the host alone counts it.
                    if (_field.NetSeats.Count > 0)
                        reportDeath(victim, killer);
                    else if (killer is int k && k >= 0 && k < match.PlayerCount)
                        match.RegisterKill(k, victim);
                    else
                        match.RegisterDeath(victim);
                };
            }
        match.MatchCompleted += () => Log.Info("flight", $"dogfight: match complete, {string.Join(", ", match.Standings().Select(s => $"P{s.PlayerIndex + 1} {s.Score}pts {s.Kills}K/{s.Deaths}D (#{s.Rank})"))}{string.Concat(match.TeamStandings().Select(t => $", team {t.Team} '{t.Name}' {t.Score}pts (#{t.Rank})"))}");
        Log.Info("flight", $"dogfight: {seatRigs.Count} pilots, {(match.KillTarget > 0 ? $"first to {match.KillTarget} points" : "no kill target")}, {(match.TimeLimit > 0f ? $"{match.TimeLimit / 60f:0.#} min limit" : "no time limit")}{(match.Teamed ? $", teams by seat {string.Join(",", Enumerable.Range(0, match.PlayerCount).Select(match.TeamOf))}" : "")}, {match.Scores}");
    }

    /// <summary>Where a downed seat comes back, over the wire. The rotation stands on the host
    /// alone, so a return is asked of it and granted to the whole field. Every peer then places
    /// the aeroplane through the same call its owner would have made locally. Inert with no seats
    /// on a wire, where a rig keeps its own respawn placement.</summary>
    internal void WireSpawns()
    {
        if (_field.Net is not { } net || _field.NetSeats.Count == 0)
        {
            return;
        }

        var seatRigs = _field.SeatRigs;
        _spawnAsked = new bool[seatRigs.Count];
        _spawnEntries = new int[seatRigs.Count];
        Array.Fill(_spawnEntries, -1);
        net.On<Net.SpawnMessage>((_, spawn) => TakeSpawn(spawn));
        net.On<Net.SpawnAtMessage>((_, spawn) => TakeSpawnAt(spawn));
        if (net.IsHost)
        {
            net.On<Net.SpawnRequestMessage>((_, ask) => GrantSpawn(ask.Seat, Net.NetSpawnKind.Respawn));
        }

        Log.Info("core", $"net spawns: {seatRigs.Count} seats, {(net.IsHost ? $"host (the rotation over {_spawnList?.Count ?? 0} point(s) grants every return)" : "guest (asking the host for its own return, running no rotation)")}");
    }

    /// <summary>The match clock, its limits and its ending over the wire. The host is the only
    /// writer: it runs the clock, arms both limits and decides the end, then says so. A guest hands
    /// its own match over and advances or ends nothing. The scoreboard is not sent at all: every
    /// machine derives it from the scores that already arrive seat by seat.</summary>
    internal void WireMatchState()
    {
        if (_field.Net is not { } net || _field.NetSeats.Count == 0)
        {
            return;
        }

        if (net.IsHost)
        {
            // Change-driven plus a clock tick, and ⚠ nothing is sent from here. The cadence's
            // first step ticks, so the limits reach a guest inside one step of its build. The
            // join then stays the two payloads it is counted as.
            _matchCadence = new Net.MatchStateCadence();
        }
        else
        {
            Match.Replicate();
            net.On<Net.MatchStateMessage>((_, state) => TakeMatchState(state));
        }

        Log.Info("core", $"net match state: {(net.IsHost ? $"host (both limits, the clock every {Net.MatchStateCadence.TickStepInterval} steps, and the ending as it happens)" : "guest (applying the host's clock, limits and ending, advancing none of its own)")}");
    }

    /// <summary>Capture the Flag over a team match on the wire, with the flags the mission lays out
    /// for the lobby's teams. Every seat's death drops its flag on every machine, and so does the
    /// console's ejectflag typed into the chat.</summary>
    internal void WireFlags(FlagInputs inputs)
    {
        if (!_spec.CaptureTheFlag || _field.Net is not { } net || SeatTeams(_spec, _field.NetSeats) is not { } teams)
        {
            return;
        }

        var gamez = inputs.Gamez;
        var scene = inputs.Scene;
        var worldRoot = inputs.WorldRoot;
        Flags = FlagRuntime.Open(new FlagRuntimeInputs
        {
            Net = net,
            SeatRigs = _field.SeatRigs,
            Panes = _field.Panes,
            SeatTeams = teams,
            IsLocal = IsLocal,
            Match = Match,
            SendScore = SendScore,
            FlagHomeToCapture = _spec.FlagHomeToCapture,
            World = inputs.World,
            WorldScene = worldRoot,
            BuildLoose = name => BuildLoose(worldRoot, gamez, scene, name),
            Lights = inputs.Lights,
            Radio = inputs.Radio,
            Strings = _field.Strings,
            GroundAt = inputs.GroundAt,
            CallsignOf = seat => seat >= 0 && seat < _field.NetSeats.Count ? _field.NetSeats[seat].Callsign : "",
        });
        if (Flags is not { } flags)
        {
            return;
        }

        foreach (var rig in _field.SeatRigs)
        {
            int seat = rig.Index;
            if (rig.Controller is { } pilot)
            {
                pilot.Downed += (_, _) => flags.Downed(seat);
            }
        }

        if (inputs.Chat is { } chat)
        {
            chat.EjectFlag = seat => flags.Eject(seat);
        }
    }

    /// <summary>Zeppelin vs Zeppelin over a team match on the wire, the mission's two hulls one per
    /// side. The parts they lose are scored, and the first hull lost ends the match.</summary>
    internal void WireZeppelinVersus(ZeppelinVersusWireInputs inputs)
    {
        if (!_spec.ZeppelinVsZeppelin || _field.Net is not { } net
            || SeatTeams(_spec, _field.NetSeats) is not { } teams || inputs.Zeppelins is not { } zeppelins)
        {
            return;
        }

        var (radius, margin) = ZeppelinVersusRuntime.LoadRespawnRing(inputs.ZrdrPath);
        ZvzPlay = ZeppelinVersusRuntime.Open(new ZeppelinVersusInputs
        {
            Net = net,
            SeatRigs = _field.SeatRigs,
            Panes = _field.Panes,
            SeatTeams = teams,
            IsLocal = IsLocal,
            SeatOfShooter = inputs.SeatOfShooter,
            Match = Match,
            Zeppelins = zeppelins,
            SendScore = SendScore,
            Radio = inputs.Radio,
            Strings = _field.Strings,
            GroundAt = inputs.GroundAt,
            RespawnRadius = radius,
            RespawnMargin = margin,
        });
    }

    /// <summary>The rearm bases of any Dogfight, over the wire or split screen. Zeppelin vs Zeppelin
    /// rearms only at its hulls' own nodes, so a match that could not seat both hulls has no base
    /// at all.</summary>
    internal void WireRearmBases(AnimRuntime? world, string zrdrPath, ZeppelinRuntime? hulls)
    {
        if (world == null || (_spec.ZeppelinVsZeppelin && ZvzPlay == null))
        {
            return;
        }

        RearmPlay = RearmRuntime.Open(new RearmRuntimeInputs
        {
            SeatRigs = _field.SeatRigs,
            IsLocal = seat => _field.NetSeats.Count == 0 || IsLocal(seat),
            SeatTeams = SeatTeams(_spec, _field.NetSeats),
            World = world,
            CaptureTheFlag = _spec.CaptureTheFlag,
            Zeppelins = ZvzPlay,
            Hulls = hulls,
            RadiusSquared = RearmBases.LoadRadiusSquared(zrdrPath, why => Log.Warn("flight", $"rearm: player.zrd unreadable, the radius keeps its initialised value: {why}")),
            Strings = _field.Strings,
        });
    }

    /// <summary>The match's own simulation step. A host advances the clock and ticks the state
    /// out. A guest's Advance is a no-op and its cadence is null, so it applies what arrived and no
    /// more. The ending is sent from here too, which covers the time-out. No match then ends
    /// without saying so, at worst one step late.</summary>
    internal void StepMatch(float dt)
    {
        Match.Advance(dt);
        HoldSpentPilots();
        if (_matchCadence is not { } cadence)
        {
            return;
        }

        bool tick = cadence.StepSends();
        bool unsentEnding = Match.Completed && End == Net.NetMatchEnd.Running;
        if (tick || unsentEnding)
        {
            SendMatchState();
        }
    }

    /// <summary>The one place a network match's numbers move, and it runs on the host alone. Cause
    /// 4 names the turret's owner in the killer field and scores it score_turret_kill. Cause 3 is a
    /// hull's broadside, named by placement index in the source field: event 9 sets the hull's
    /// side's term.</summary>
    internal void ScoreDeath(in Net.DeathMessage death)
    {
        if (_field.Net is not { IsHost: true } net)
        {
            return;
        }

        var match = Match;
        int victim = death.VictimSeat;
        int killer = death.KillerSeat < match.PlayerCount ? death.KillerSeat : -1;
        int hullTeam = death.Cause == Net.NetDeathCause.ZeppelinPart && ZvzPlay is { } zvz && death.SourceId < 2
            ? zvz.Rules.TeamOfHull((int)death.SourceId)
            : 0;
        bool charged = killer >= 0 && death.Cause != Net.NetDeathCause.Suicide;
        if (hullTeam > 0)
        {
            killer = -1;
            charged = false;
            match.RegisterZeppelinKill(victim, hullTeam);
        }
        else if (!charged)
        {
            match.RegisterDeath(victim);
        }
        else
        {
            match.RegisterKill(killer, victim, turret: death.Cause == Net.NetDeathCause.TurretOwner);
        }

        SendScore(victim);
        if (killer >= 0 && killer != victim)
        {
            SendScore(killer);
        }

        // Sent between the scores and the ending on one reliable channel. Every machine then posts
        // the lives line, the kill lines and the ending in the original's order.
        var notice = new Net.DeathNoticeMessage((byte)victim,
            charged ? (byte)killer : Net.NetMessage.NoSeat,
            hullTeam > 0 ? Net.NetDeathCause.ZeppelinPart : charged ? death.Cause : Net.NetDeathCause.Suicide,
            (byte)Math.Clamp(hullTeam, 0, byte.MaxValue));
        net.Broadcast(notice, Net.NetChannels.Events);
        PostDeathNotice(notice.VictimSeat, notice.KillerSeat, notice.Cause, notice.Team);

        // ⚠ The ending goes out AFTER the scores that settled the round, never from the match's
        // completion event, which fires before them. A guest whose match already reads completed
        // drops every score behind it, and its board would then name a different winner.
        if (match.Completed && End == Net.NetMatchEnd.Running)
        {
            SendMatchState();
        }
    }

    /// <summary>A guest's copy of the host's decision. A hull's kill writes its side's term here
    /// too, since no score message carries a team's term. The write is a set, so a repeat changes
    /// nothing.</summary>
    internal void TakeDeathNotice(in Net.DeathNoticeMessage notice)
    {
        if (notice.Cause == Net.NetDeathCause.ZeppelinPart && notice.Team > 0)
        {
            Match.RegisterZeppelinKill(notice.VictimSeat, notice.Team);
        }

        PostDeathNotice(notice.VictimSeat, notice.KillerSeat, notice.Cause, notice.Team);
    }

    /// <summary>One seat's line as the host has it, applied on a guest. Every guest shows this
    /// instead of counting, so a board reads the same everywhere whatever each machine saw.</summary>
    internal void TakeScore(in Net.ScoreMessage score)
    {
        if (_field.Net is not { IsHost: true })
        {
            Match.ApplyScore(score.Seat, score.Score, score.Kills, score.Deaths);
        }
    }

    /// <summary>A seat's death as the HUD lines word it. True when the seat is in the match, whose
    /// Dogfight death lines post on every death, crashes included. On the wire the host's notice
    /// posts them, never this report.</summary>
    internal bool TakeKillLine(int victimId, int? killer)
    {
        var m = Match;
        if (victimId < 0 || victimId >= m.PlayerCount)
        {
            return false;
        }

        if (_field.NetSeats.Count == 0)
        {
            PostSplitScreenDeath(victimId, killer is int k && k >= 0 && k < m.PlayerCount ? k : null);
        }

        return true;
    }

    /// <summary>A guest's seat that left the session mid-match. A drop is the other thing that can
    /// leave a match without an opponent (reason 4). The host's step sends that ending; a guest's
    /// replicated match only marks the seat. A flag the seat carried floats, as a death's does
    /// (FUN_004995a0).</summary>
    internal void SeatLeft(int seat)
    {
        Match.Leave(seat);
        Flags?.Downed(seat);
    }

    /// <summary>The side a team mode labels a mission target with, null for a target neither the
    /// flags nor the hulls name.</summary>
    internal Flight.Weapons.SiteSide? SideOf(string key) => Flags?.SideOf(key) ?? ZvzPlay?.SideOf(key);

    /// <summary>Rematch from the dogfight results board (R): every score and the clock reset, then
    /// every plane back to its own spawn.</summary>
    internal void Restart()
    {
        var match = Match;
        // ⚠ Never rerun in place: that restores no world pool, so it would fly on the last round's
        // burnt gas bags. The original's end takes every machine to the lobby, whose next launch
        // builds the world afresh. Each machine goes there itself, a guest as much as the host.
        if (ZvzPlay != null)
        {
            if (_toLobby is { } toLobby)
            {
                Log.Info("flight", $"dogfight: Zeppelin vs Zeppelin goes again from the lobby, whose next launch rebuilds both hulls");
                toLobby();
            }
            else
            {
                Log.Info("flight", $"dogfight: no rematch in Zeppelin vs Zeppelin outside the lobby, the hulls rebuild only at a launch");
            }

            return;
        }

        // ⚠ On a wire the rematch is the host's alone. A guest restarting here would zero its own
        // board and fly a round nobody else is in. Its R therefore does nothing, its board says
        // so in place of the Restart row, and it waits for the host's running state.
        if (RematchIsTheHosts)
        {
            Log.Info("flight", $"dogfight: rematch is the host's to call, this guest waits for it");
            return;
        }

        Log.Info("flight", $"dogfight: rematch, scores and clock reset for every pilot");
        match.Restart();
        // A rematch is a fresh round, so it opens on the opening spawns rather than on wherever
        // the last round's rotation had left each seat.
        _rotation?.Restart();
        var seatRigs = _field.SeatRigs;
        if (_field.NetSeats.Count > 0)
        {
            // ⚠ The running state goes out BEFORE the zeroed scores. A guest whose match still
            // reads completed drops every score. Both ride the one reliable ordered channel.
            SendMatchState();
            for (int seat = 0; seat < seatRigs.Count; seat++)
                SendScore(seat);
            Flags?.Restart();
            // On a wire the whole field is put back by grant, seat by seat, so a rematch places
            // every aeroplane from the one rotation. A guest grants nothing and waits.
            for (int seat = 0; seat < seatRigs.Count; seat++)
                GrantSpawn(seat, Net.NetSpawnKind.Opening);
            return;
        }

        foreach (var rig in _field.Panes)
            rig.Controller?.Respawn();
    }

    /// <summary>The scoreboard debug force: one scripted, ATTRIBUTED kill on the first sim step,
    /// through the Downed path a real kill takes. A screenshot then has a real K/D and kill banner
    /// without scripting a shot. Single-fire.</summary>
    internal void ForceDebugScoreboard()
    {
        var panes = _field.Panes;
        if (_debugKillFired || panes.Count <= 1)
        {
            return;
        }

        _debugKillFired = true;
        panes[1].Controller?.DebugForceCrash(panes[0].Controller?.PlayerIndex);
    }

    // One library root of the chapter, built hidden and without collision under the world root. The
    // caller parents it where the data's own animation would.
    private static Node3D? BuildLoose(Node3D? worldRoot, GameZ gamez, SceneBuilder? scene, string name)
    {
        if (scene == null || worldRoot == null || gamez.FindByName(name) is not { } node
            || scene.BuildSubtree(node, collisionSkip: _ => true) is not { } built)
        {
            return null;
        }

        built.Transform = Transform3D.Identity;
        built.Visible = false;
        worldRoot.AddChild(built);
        return built;
    }

    private bool IsLocal(int seat) =>
        seat >= 0 && seat < _field.NetSeats.Count && _field.NetSeats[seat].FlownHere;

    private bool HasPane(int seat) =>
        seat >= 0 && seat < _field.NetSeats.Count && _field.NetSeats[seat].HasPane;

    private bool IsBot(int seat) =>
        seat >= 0 && seat < _field.NetSeats.Count && _field.NetSeats[seat].IsBot;

    // A splitscreen match's death in every pane, the seats named by their player tags. The match
    // handler subscribed first, so the death is already counted for the lives line.
    private void PostSplitScreenDeath(int victim, int? killer)
    {
        PostLivesLines();
        foreach (var pane in _field.Panes)
        {
            if (pane.Controller?.MessageStack is { } stack)
            {
                HudMessages.PostMatchKill(stack, _field.Strings,
                    killer != null ? HudMessages.MatchDeath.Killer : HudMessages.MatchDeath.NoKiller,
                    SplitScreen.PlayerTag(victim),
                    killer is int k ? SplitScreen.PlayerTag(k) : null);
            }
        }
    }

    // A match death as the host decided it, posted into every local pane once: the host from its
    // own scoring, a guest from the notice. The dying pilot's lives line goes in first, below. A
    // hull's kill is named for its side's lobby team, as the original's row 7064 is.
    private void PostDeathNotice(int victim, int killer, Net.NetDeathCause cause, int team = 0)
    {
        PostLivesLines();
        var death = cause switch
        {
            Net.NetDeathCause.Suicide => HudMessages.MatchDeath.NoKiller,
            Net.NetDeathCause.ZeppelinPart => HudMessages.MatchDeath.Zeppelin,
            Net.NetDeathCause.TurretOwner => HudMessages.MatchDeath.Turret,
            _ => HudMessages.MatchDeath.Killer,
        };
        var netSeats = _field.NetSeats;
        string? victimName = victim < netSeats.Count ? netSeats[victim].Callsign : null;
        string? killerName = team > 0 ? Match.TeamName(team)
            : killer < netSeats.Count ? netSeats[killer].Callsign : null;
        foreach (var pane in _field.Panes)
        {
            if (pane.Controller?.MessageStack is { } stack)
            {
                HudMessages.PostMatchKill(stack, _field.Strings, death, victimName, killerName);
            }
        }
    }

    // Every seat's lives line still owed, ahead of the kill lines, since the original's handler
    // posts it first. The match step's own pass then finds nothing left to post.
    private void PostLivesLines()
    {
        if (Match is not { Lives: > 0 } match)
        {
            return;
        }

        var seatRigs = _field.SeatRigs;
        for (int seat = 0; seat < seatRigs.Count; seat++)
        {
            if (seatRigs[seat].Controller is { } pilot)
            {
                PostLivesLeft(match, seat, pilot);
            }
        }
    }

    // One seat's line as the host has it. Every guest shows this instead of counting, so a board
    // reads the same everywhere whatever each machine saw.
    private void SendScore(int seat)
    {
        var match = Match;
        if (_field.Net is not { IsHost: true } net || seat < 0 || seat >= match.PlayerCount)
        {
            return;
        }

        net.Broadcast(
            new Net.ScoreMessage((byte)seat, (short)match.ScoreOf(seat), (ushort)match.KillsOf(seat),
                (ushort)match.DeathsOf(seat)),
            Net.NetChannels.Events);
    }

    // The host's match state as it stands now. The clock rides along because the tick is the one
    // message a running match repeats, which makes it the reading a guest's slew can take.
    private void SendMatchState()
    {
        if (_field.Net is not { IsHost: true } net)
        {
            return;
        }

        var match = Match;
        // Which of the original's end reasons this is. The remake arms both limits at once
        // (docs/org/multiplayer-scoring.md). A completed match is therefore reason 4 when the
        // last opponent went, a time-out when the clock ran out, and a score target otherwise.
        var was = End;
        End = !match.Completed ? Net.NetMatchEnd.Running
            : match.ObjectiveWinner > 0 ? Net.NetMatchEnd.Objective
            : match.AllAlone ? Net.NetMatchEnd.NobodyLeft
            : match.TimeLimit > 0f && match.Elapsed >= match.TimeLimit ? Net.NetMatchEnd.TimeLimit
            : Net.NetMatchEnd.ScoreTarget;
        PostAllAlone(was);
        net.Broadcast(
            new Net.MatchStateMessage(match.TimeRemaining, match.TimeLimit, (short)match.KillTarget,
                End, (float)_field.ClockTime(), (byte)Math.Clamp(match.ObjectiveWinner, 0, byte.MaxValue)),
            Net.NetChannels.Events);
    }

    private void TakeMatchState(in Net.MatchStateMessage state)
    {
        if (_field.Net is { IsHost: true })
        {
            return;
        }

        var was = End;
        End = state.End;
        // The objective's winner and bonus land ahead of the ending, which is what the board reads.
        if (state.End == Net.NetMatchEnd.Objective && state.Winner > 0)
        {
            ZvzPlay?.TakeEnding(state.Winner);
        }

        PostAllAlone(was);
        _field.NetClock?.Observe(state.HostClock, _field.ClockTime());
        Match.ApplyState(state.ScoreTarget, state.TimeLimitSeconds, state.RemainingSeconds,
            state.End != Net.NetMatchEnd.Running);
    }

    // Reasons 3 and 4 in words, on every machine the moment its end turns to one. The original's
    // "Game Over:" and "No Enemies Left" lines go into each local pane's stack, and a lost hull
    // posts and speaks its own.
    private void PostAllAlone(Net.NetMatchEnd was)
    {
        if (End == Net.NetMatchEnd.Objective && was != Net.NetMatchEnd.Objective)
        {
            ZvzPlay?.Announce();
        }

        if (was == Net.NetMatchEnd.NobodyLeft || End != Net.NetMatchEnd.NobodyLeft)
        {
            return;
        }

        foreach (var rig in _field.Panes)
        {
            if (rig.Controller?.MessageStack is { } stack)
            {
                HudMessages.PostAllAlone(stack, _field.Strings);
            }
        }
    }

    // The lobby's Limited Lives. A pilot whose deaths reach the limit watches the next aircraft
    // still flying. A rematch's zeroed deaths put it back. Every machine reads the host's death
    // count, so both ends hold the same pilots down, and each death tells its own pilot the lives
    // left.
    private void HoldSpentPilots()
    {
        if (Match is not { Lives: > 0 } match)
        {
            return;
        }

        var seatRigs = _field.SeatRigs;
        var flying = new bool[seatRigs.Count];
        for (int seat = 0; seat < flying.Length; seat++)
        {
            flying[seat] = seatRigs[seat].Controller is { Crashed: false, Inert: false }
                && !match.OutOfLives(seat);
        }

        for (int seat = 0; seat < seatRigs.Count; seat++)
        {
            if (seatRigs[seat].Controller is not { } pilot)
            {
                continue;
            }

            pilot.Spectating = match.OutOfLives(seat);
            int? watched = pilot.Spectating
                ? VersusMatch.NextWatched(seat, flying, SeatOf(pilot.Watching))
                : null;
            pilot.Watching = watched is { } w ? seatRigs[w].Controller : null;
            PostLivesLeft(match, seat, pilot);
        }
    }

    // One seat's lives line, posted once per death into that seat's own pane alone, as the
    // original posts it for the local pilot.
    private void PostLivesLeft(VersusMatch match, int seat, FlightController pilot)
    {
        if (seat >= _livesSeen.Length)
        {
            return;
        }

        int deaths = match.DeathsOf(seat);
        bool died = deaths > _livesSeen[seat];
        _livesSeen[seat] = deaths;
        bool local = _field.NetSeats.Count == 0 || HasPane(seat);
        if (died && local && pilot.MessageStack is { } stack)
        {
            HudMessages.PostLivesLeft(stack, _field.Strings, match.Lives - deaths);
        }
    }

    private int? SeatOf(FlightController? pilot)
    {
        var seatRigs = _field.SeatRigs;
        for (int seat = 0; pilot != null && seat < seatRigs.Count; seat++)
        {
            if (ReferenceEquals(seatRigs[seat].Controller, pilot))
            {
                return seat;
            }
        }

        return null;
    }

    // A seat flown here is down and its timer is up. The host answers itself; a guest asks, once
    // per death, because the ask is reliable and the aeroplane stays down until the answer lands.
    private void AskSpawn(int seat)
    {
        if (_field.Net is not { } net || !IsLocal(seat))
        {
            return;
        }

        if (net.IsHost)
        {
            GrantSpawn(seat, Net.NetSpawnKind.Respawn);
            return;
        }

        if (seat >= _spawnAsked.Length || _spawnAsked[seat])
        {
            return;
        }

        _spawnAsked[seat] = true;
        net.Send(net.HostPeer, new Net.SpawnRequestMessage((byte)seat), Net.NetChannels.Events);
    }

    // The host's answer, and the one place a match's rotation is run. A return is granted only to
    // a seat that is down. The ask is reliable, so a repeat would otherwise walk the rotation a
    // second time and move a flying aeroplane. A rematch's opening grant has no such guard: there
    // the whole field is being put back, whether it was flying or not.
    private void GrantSpawn(int seat, Net.NetSpawnKind kind)
    {
        var seatRigs = _field.SeatRigs;
        if (_field.Net is not { IsHost: true } net || seat < 0 || seat >= seatRigs.Count
            || seatRigs[seat].Controller is not { } rig
            || (kind == Net.NetSpawnKind.Respawn && (!rig.Crashed || Match.OutOfLives(seat))))
        {
            return;
        }

        // Zeppelin vs Zeppelin brings a seat back by its hull rather than off the table.
        if (kind == Net.NetSpawnKind.Respawn && ZvzPlay?.RespawnPoint(seat) is { } point)
        {
            var at = new Net.SpawnAtMessage((byte)seat, point.Position, point.HeadingDeg);
            net.Broadcast(at, Net.NetChannels.Events);
            TakeSpawnAt(at);
            return;
        }

        var spawn = new Net.SpawnMessage((byte)seat, kind, RotatedEntry(seat));
        net.Broadcast(spawn, Net.NetChannels.Events);
        TakeSpawn(spawn);
    }

    // A computed return, applied on every peer as TakeSpawn applies a table entry.
    private void TakeSpawnAt(in Net.SpawnAtMessage spawn)
    {
        var seatRigs = _field.SeatRigs;
        if (spawn.Seat >= seatRigs.Count || seatRigs[spawn.Seat].Controller is not { } rig)
        {
            return;
        }

        if (spawn.Seat < _spawnAsked.Length)
        {
            _spawnAsked[spawn.Seat] = false;
        }

        if (spawn.Seat < _spawnEntries.Length)
        {
            _spawnEntries[spawn.Seat] = -1;
        }

        SpawnsTaken++;
        var point = new SpawnPoint(spawn.Position, spawn.HeadingDeg);
        rig.RespawnAt(point.Position, point.Position + point.Forward);
        Log.Info("flight", $"net spawns: seat {spawn.Seat} placed at ({spawn.Position.X:0},{spawn.Position.Y:0},{spawn.Position.Z:0}) heading {spawn.HeadingDeg:0.#}°, by its hull");
    }

    // The rotation's pick for one seat, as an index into the spawn list every peer holds. No
    // rotation means no table to index, and the seat comes back on the pose it was given.
    private ushort RotatedEntry(int seat)
    {
        if (_rotation is not { } rotation)
        {
            return Net.NetMessage.NoSpawnEntry;
        }

        rotation.Choose(seat, LivingField(), seat < _lastKiller.Length ? _lastKiller[seat] : null);
        return (ushort)rotation.IndexOf(seat);
    }

    // The grant, applied. Every peer runs this, the host on its own message, so one placement
    // rule serves the aeroplane's owner and every copy of it. ⚠ Through RespawnAt, never
    // Respawn. The host's own seats still carry a rotation, and asking it again here moves the
    // aeroplane off the point the field was told about.
    private void TakeSpawn(in Net.SpawnMessage spawn)
    {
        var seatRigs = _field.SeatRigs;
        if (spawn.Seat >= seatRigs.Count || seatRigs[spawn.Seat].Controller is not { } rig)
        {
            return;
        }

        if (spawn.Seat < _spawnAsked.Length)
        {
            _spawnAsked[spawn.Seat] = false;
        }

        SpawnsTaken++;
        if (spawn.Seat < _spawnEntries.Length)
        {
            _spawnEntries[spawn.Seat] = spawn.EntryIndex == Net.NetMessage.NoSpawnEntry
                ? -1 : spawn.EntryIndex;
        }

        if (_spawnList is { } list && spawn.EntryIndex < list.Count)
        {
            var point = list[spawn.EntryIndex];
            rig.RespawnAt(point.Position, point.Position + point.Forward);
        }
        else
        {
            rig.Respawn();
        }

        Log.Info("flight", $"net spawns: seat {spawn.Seat} placed on entry {spawn.EntryIndex} of {_spawnList?.Count ?? 0} ({spawn.Kind})");
    }

    // The field the rotation weighs, one entry per seat and null where that seat is not in the
    // fight. ⚠ Over the whole seat list, not the panes. A host flying one pane still rotates
    // around the guests' aeroplanes, and reading the panes here hides every one of them.
    private Vector3?[] LivingField()
    {
        var seatRigs = _field.SeatRigs;
        var field = new Vector3?[seatRigs.Count];
        for (int i = 0; i < seatRigs.Count; i++)
            field[i] = seatRigs[i].Controller is { Crashed: false, Inert: false } flying
                ? flying.GlobalPosition : null;
        return field;
    }

    // Where a downed seat comes back, the rotation's pick against the field as it stands at the
    // respawn. A seat still on its own crash camera neither holds a point nor pulls one away. Null
    // with no rotation built, which leaves the seat on the pose it was given.
    private (Vector3 Pos, Vector3 LookAt)? Respawn(int seat, int? killer)
    {
        if (_rotation is not { } rotation)
            return null;
        var point = rotation.Choose(seat, LivingField(), killer);
        Log.Info("flight", $"dogfight: P{seat + 1} respawns on {_spawnListName} #{rotation.IndexOf(seat)} of {rotation.PointCount}{(killer is { } k ? $", downed by P{k + 1}" : "")}");
        return (point.Position, point.Position + point.Forward);
    }

    /// <summary>The seats a match is flown over, settled after the network join.</summary>
    internal sealed class Field
    {
        // This session's end of the wire, null outside a network match.
        public Net.NetSession? Net;
        public IReadOnlyList<Net.NetSeat> NetSeats = Array.Empty<Net.NetSeat>();
        // One rig per seat, a seat flown elsewhere included, and the panes drawn here.
        public IReadOnlyList<PlayerRig> SeatRigs = null!;
        public IReadOnlyList<PlayerRig> Panes = null!;
        // The string table the in-flight lines are worded from.
        public Messages? Strings;
        // The session clock's time, which the host's state carries and a guest's slew reads.
        public Func<double> ClockTime = null!;
        public Net.NetClockSlew? NetClock;
    }

    /// <summary>What the match's score and respawn wiring reads once every rig exists.</summary>
    internal sealed class WireInputs
    {
        public SpawnPicker Spawns = null!;
        public List<SpawnPoint>? SpawnList;
        public int SpawnBase;
        // The name a respawn line gives the spawn list it walks.
        public string SpawnListName = "";
        // On the wire a death is reported to its owner's host rather than scored here.
        public Action<int, int?> ReportDeath = null!;
        // Leave the flight for the lobby, null where no menu stands behind it.
        public Action? ToLobby;
    }

    /// <summary>What Capture the Flag lays its flags out with.</summary>
    internal sealed class FlagInputs
    {
        public AnimRuntime? World;
        public GameZ Gamez = null!;
        public SceneBuilder? Scene;
        public Node3D? WorldRoot;
        public WorldLights? Lights;
        public MissionRadio? Radio;
        public Func<Vector3, float?> GroundAt = null!;
        public NetChatLink? Chat;
    }

    /// <summary>What Zeppelin vs Zeppelin seats its two hulls with.</summary>
    internal sealed class ZeppelinVersusWireInputs
    {
        public string ZrdrPath = "";
        public ZeppelinRuntime? Zeppelins;
        public Func<int, int> SeatOfShooter = null!;
        public MissionRadio? Radio;
        public Func<Vector3, float?> GroundAt = null!;
    }
}
