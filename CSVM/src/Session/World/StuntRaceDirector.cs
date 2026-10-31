using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>The engine side of one stunt race, split screen or over the wire. It owns the
/// <see cref="StuntRace"/>, the bots this machine flies in it, its spectators, its best times and
/// its reruns. It drives the network race link SessionNet opens. The session builds
/// it ahead of the roster, wires it once every rig exists and steps it around the seats.
/// Module entry: docs/architecture/Session.md on src/Session/World/StuntRaceDirector.cs.</summary>
internal sealed class StuntRaceDirector
{
    /// <summary>How long a race's opening count stands on READY before its 3, 2, 1, at the launch
    /// and on every rerun. TUNE at the controls.</summary>
    internal const float ReadySeconds = 2f;

    private readonly SessionSpec _spec;
    private readonly Field _field;
    private RaceSpectate? _spectate;

    private StuntRaceDirector(SessionSpec spec, Field field, StuntRace race, NetRaceLink? link)
    {
        _spec = spec;
        _field = field;
        Race = race;
        Link = link;
    }

    /// <summary>The race's board, window and racers. On a network guest it is the host's replica.
    /// </summary>
    internal StuntRace Race { get; }

    /// <summary>The race over the wire, null outside a network race.</summary>
    internal NetRaceLink? Link { get; }

    /// <summary>The race's bots this machine flies, null with none. Set by <see cref="Wire"/>.</summary>
    internal StuntRaceBots? Bots { get; private set; }

    /// <summary>The race of a stunt run over <paramref name="zoneCount"/> danger zones, or null when
    /// the run flies alone. <paramref name="openLink"/> wires it to the network before the roster.
    /// The roster feeds each local seat's run through that link.</summary>
    internal static StuntRaceDirector? TryCreate(SessionSpec spec, int zoneCount, Field field,
        Func<StuntRace, NetRaceLink?> openLink)
    {
        // A stunt run races once it has a second seat, a network guest's included. A lobby Stunt
        // Race races even a host left alone.
        if (field.SeatRigs.Count < 2 && spec.MissionType != DogfightMissionType.StuntRace)
        {
            return null;
        }

        // A --debug-scoreboard race closes its window at once: its staggered forced finishes then
        // run as the final run, and the last wakes the board.
        var race = new StuntRace(spec.DebugScoreboard ? 0f : spec.StuntRaceMinutes * 60f, zoneCount);
        var director = new StuntRaceDirector(spec, field, race, openLink(race));
        if (director.Link is { } link)
        {
            // A guest's opening catches up to the host's on every local seat's count at once.
            link.CatchUp = seconds =>
            {
                foreach (var pane in field.Panes)
                {
                    pane.Controller?.CatchUpStartCount(seconds);
                }
            };
        }

        return director;
    }

    /// <summary>The bots, the spectators, the board and the best times, once every rig exists. The
    /// seats' opening counts began in this same build, so the window opens on GO. A network guest's
    /// board waits for the host's window, which opens here on its call.</summary>
    internal void Wire(WireInputs inputs)
    {
        var race = Race;
        Bots = StuntRaceBots.For(race, _field.SeatRigs, _field.NetSeats);
        _spectate = new RaceSpectate(race, panes: _field.Panes, seats: _field.SeatRigs, inputs.WorldRoot, inputs.SpectatorCameras);
        if (Link is { IsHost: false } guestLink)
        {
            guestLink.Rerun = RerunWindow;
        }

        // --debug-race-end poses a split screen race's board as a network one's, P2 left.
        string? posed = Link == null ? _spec.DebugRaceEnd : null;
        if (posed != null)
        {
            race.BestImproved += racer => _ = racer.Index == 1 && race.MarkLeft(1);
        }

        bool guest = Link is { IsHost: false } || posed == "guest";
        inputs.BuildBoard(new BoardRole(Link != null || posed != null, posed != null, guest));
        foreach (var rig in _field.Panes)
        {
            if (rig.Controller != null)
            {
                rig.Controller.RerunRace = inputs.Rerun;
            }
        }

        var bests = ScoreStore.ForSession(_spec.ScoresPath, _spec.ScoresThrowaway);
        race.BestImproved += racer => RecordBest(bests, racer);
        race.BeginOpening(OpeningSeconds(inputs.FirstCount));
        Log.Info("flight", $"stunt race: {_field.SeatRigs.Count} pilots over {race.ZoneCount} danger zones, a {StuntRace.FormatClock(race.WindowSeconds)} time attack, best run ranks{(race.Replicated ? ", the host's board and window replicated here" : "")}");
    }

    /// <summary>One sim step's bots, before the seats step.</summary>
    internal void StepBots() => Bots?.Step();

    /// <summary>One sim step's race, after every seat: a run started this step was started inside
    /// the window it saw. The spectators step after the host's word, so a guest hands no pane over
    /// in a race already ended.</summary>
    internal void Step(float dt)
    {
        Race.Advance(dt);
        Link?.Step();
        _spectate?.Step(dt);
    }

    /// <summary>A board menu's Restart: a new window in place. A network guest's is the host's to
    /// call, and opens through the link.</summary>
    internal void Rerun()
    {
        if (Race.Replicated)
        {
            // ⚠ Never on a guest: a window opened here would be one nobody else flies.
            Log.Info("flight", $"stunt race: a new window is the host's to call");
            return;
        }

        RerunWindow();
    }

    /// <summary>This machine leaves the flight. A network race says so first (<see cref="NetRaceLink.Leave"/>).
    /// </summary>
    internal void Leave(bool toLobby) => Link?.Leave(toLobby);

    // How long a seat's first count runs before GO, the window's opening; none opens it at once.
    private static float OpeningSeconds(IReadOnlyList<StartCountPhase>? phases) =>
        phases?.Sum(phase => phase.Seconds) ?? 0f;

    // A race run that is its pilot's best so far records under the solo run's key, the same store
    // and the same never-worsen rule. A pilot with no key records nothing.
    private static void RecordBest(ScoreStore store, Racer racer)
    {
        if (racer.ScoreKey.Length > 0 && racer.BestTime is { } best && store.RecordIfBest(racer.ScoreKey, best))
        {
            Log.Info("flight", $"stunt race: {racer.Tag} set a new best {StuntMission.FormatTime(best)} for '{racer.ScoreKey}'");
        }
    }

    // A new window from the shared race board (R), or on a network guest from the host's call.
    // Every pilot's runs are cleared, every plane here goes back to the start, and every seat opens
    // on the count again in one step. A host's call goes out first, ahead of the window's lines.
    private void RerunWindow()
    {
        Log.Info("flight", $"stunt race: a new window, every pilot's runs cleared");
        _spectate?.HandBack();
        Race.Rerun();
        Link?.CallRerun();
        IReadOnlyList<StartCountPhase>? opening = null;
        // Every seat flown here, the bots among them; a seat flown elsewhere is its own machine's.
        foreach (var rig in _field.SeatRigs)
        {
            if (rig.Controller is not { RemoteOwned: false } seat)
            {
                continue;
            }

            seat.Stunt?.Reset();
            seat.StuntShots?.Reset();
            seat.Respawn();
            // ⚠ Only a seat that carries a rerun count opens on one: under --det none does.
            if (seat.RerunCount != null)
            {
                opening = StartCount.Opening(ReadySeconds);
                seat.BeginStartCount(opening);
            }
        }

        Race.BeginOpening(OpeningSeconds(opening));
    }

    /// <summary>How this machine's race board reads. <paramref name="Networked"/> gives it a network
    /// exit, and <paramref name="Posed"/> is a <c>--debug-race-end</c> pose of one. A
    /// <paramref name="Guest"/> withholds the restart, which is the host's to call.</summary>
    internal readonly record struct BoardRole(bool Networked, bool Posed, bool Guest);

    /// <summary>The session's seats and panes, the lists the roster fills.</summary>
    internal sealed class Field
    {
        /// <summary>Every seat in the race, those flown elsewhere included.</summary>
        public IReadOnlyList<PlayerRig> SeatRigs = null!;

        /// <summary>This machine's panes.</summary>
        public IReadOnlyList<PlayerRig> Panes = null!;

        /// <summary>The network roster, empty outside a network race.</summary>
        public IReadOnlyList<NetSeat> NetSeats = Array.Empty<NetSeat>();
    }

    /// <summary>What <see cref="Wire"/> needs from the built session.</summary>
    internal sealed class WireInputs
    {
        /// <summary>The seats' first count, whose length the window's opening waits.</summary>
        public IReadOnlyList<StartCountPhase>? FirstCount;

        /// <summary>The board menu's Restart and each pane's R, the session's own rerun.</summary>
        public Action Rerun = null!;

        /// <summary>Builds the race board as <see cref="BoardRole"/> says it reads.</summary>
        public Action<BoardRole> BuildBoard = null!;

        public Node3D WorldRoot = null!;
        public List<SpectatorCamera> SpectatorCameras = null!;
    }
}
