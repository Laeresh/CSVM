using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.Utils;

namespace CSVM.Session.World;

/// <summary>
/// A stunt race over the wire, one per network race session. Each machine times its own seats'
/// runs and the host keeps the window, the leaderboard and the ending. A guest reports its seats'
/// run starts, splits, finishes and restarts to the host as <see cref="RaceRunMessage"/>s. The host
/// feeds them to its race beside its own seats' runs, and sends every changed racer's line and its
/// clock to every guest. A guest's race is a replica that takes both, and its opening count
/// catches up to the host's. The window's end crosses as <see cref="RaceCallMessage"/>s: the host's
/// restart and return to the lobby, a guest's leaving (<c>docs/org/multiplayer-messages.md</c>,
/// "Stunt race").
/// </summary>
internal sealed class NetRaceLink
{
    private readonly NetSession _net;
    private readonly StuntRace _race;
    private readonly Func<double> _clock;
    private readonly NetClockSlew? _slew;
    private readonly NetClockPing? _ping;
    private readonly float _stepSeconds;
    private readonly MatchStateCadence? _cadence;
    // The host's newest run number from each remote seat's owner, under the round it was heard in.
    private readonly Dictionary<int, ushort> _ownerRuns = new();
    private readonly Dictionary<int, int> _sentRevisions = new();
    // A guest's own seats' run numbers in the current window.
    private readonly Dictionary<int, ushort> _ownRuns = new();
    private int _ownerRunsRound;
    private int _ownRunsRound;
    private StuntRacePhase? _sentPhase;
    private int _sentRound = -1;
    private bool _askedClock;
    private bool _windowMismatchLogged;

    private NetRaceLink(NetSession net, StuntRace race, Func<double> clock, NetClockSlew? slew,
        NetClockPing? ping, float stepSeconds)
    {
        _net = net;
        _race = race;
        _clock = clock;
        _slew = slew;
        _ping = ping;
        _stepSeconds = stepSeconds;
        _cadence = net.IsHost ? new MatchStateCadence() : null;
    }

    /// <summary>On the host, a guest machine that left the race, by its peer.</summary>
    internal event Action<int>? GuestLeft;

    /// <summary>On a guest, the host's new window to open here. Every local seat goes back to the
    /// start behind a fresh opening count, as the host's own restart does.</summary>
    internal Action? Restarted { get; set; }

    /// <summary>On a guest, whether the host took the race back to the lobby. The launcher's upkeep
    /// follows it there.</summary>
    internal bool LobbyCalled { get; private set; }

    /// <summary>Calls taken from the other end: restarts and the lobby on a guest, leaves on the host.</summary>
    internal int CallsTaken { get; private set; }

    /// <summary>On a guest, what each local seat's running count must skip when the opening
    /// catches up to the host's, in seconds. The race has already skipped the same.</summary>
    internal Action<float>? CatchUp { get; set; }

    /// <summary>Whether this end is the race's host.</summary>
    internal bool IsHost => _net.IsHost;

    /// <summary>On the host, reports that passed the owner, round and run checks.</summary>
    internal int ReportsTaken { get; private set; }

    /// <summary>On the host, reports dropped: a spoofed seat, another window, a repeat or a stale run.</summary>
    internal int ReportsRefused { get; private set; }

    /// <summary>On the host, racer lines sent; on a guest, lines taken.</summary>
    internal int Lines { get; private set; }

    /// <summary>On the host, clock readings sent; on a guest, readings taken.</summary>
    internal int States { get; private set; }

    /// <summary>On a guest, the seconds its opening caught up to the host's in all.</summary>
    internal float CaughtUp { get; private set; }

    /// <summary>Wires <paramref name="race"/> to <paramref name="net"/>. A guest's race becomes a
    /// replica. <paramref name="slew"/> and <paramref name="ping"/> are a guest's shared clock and
    /// its round trip; <paramref name="stepSeconds"/> is the fixed sim step a catch-up moves in.</summary>
    internal static NetRaceLink Open(NetSession net, StuntRace race, Func<double> clock, NetClockSlew? slew,
        NetClockPing? ping, float stepSeconds)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(clock);
        var link = new NetRaceLink(net, race, clock, slew, ping, stepSeconds);
        if (net.IsHost)
        {
            net.On<RaceRunMessage>(link.TakeReport);
        }
        else
        {
            race.Replicate();
            net.On<RaceStandingMessage>((_, line) => link.TakeLine(line));
            net.On<RaceStateMessage>((_, state) => link.TakeState(state));
        }

        net.On<RaceCallMessage>(link.TakeCall);
        return link;
    }

    /// <summary>The host's new window, told to every guest. Sent at the restart itself, ahead of the
    /// new window's lines and clock on the same channel.</summary>
    internal void CallRestart()
    {
        if (_net.IsHost)
        {
            _net.Broadcast(new RaceCallMessage(NetRaceCall.Restart, (byte)_race.Round), NetChannels.Events);
        }
    }

    /// <summary>This machine leaves the race. A guest tells the host, which marks its seats left. The
    /// host tells its guests only when it takes an ended race to the lobby (<paramref name="toLobby"/>).
    /// A host leaving otherwise closes its door, which ends every guest's flight.</summary>
    internal void Leave(bool toLobby)
    {
        if (!_net.IsHost)
        {
            _net.Send(_net.HostPeer, new RaceCallMessage(NetRaceCall.Leave, (byte)_race.Round), NetChannels.Events);
        }
        else if (toLobby && _race.Ended)
        {
            _net.Broadcast(new RaceCallMessage(NetRaceCall.Lobby, (byte)_race.Round), NetChannels.Events);
        }
    }

    /// <summary>A seat whose pilot left the mission, on every machine. Its record stays, marked; the
    /// host sends the marked line at once, since a halted race takes no step to send it.</summary>
    internal void SeatLeft(int seat)
    {
        if (_race.MarkLeft(seat) && _cadence != null)
        {
            Flush(false);
        }
    }

    /// <summary>The owner's feed off seat <paramref name="seat"/>'s own run, flown here. The host's
    /// race follows it directly; a guest reports each event to the host.</summary>
    internal void Feed(int seat, StuntMission run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (_net.IsHost)
        {
            _race.Follow(seat, run);
            return;
        }

        run.RunStarted += () => Report(seat, NetRaceRun.Started);
        run.RunReset += () => Report(seat, NetRaceRun.Abandoned);
        run.ZoneCompleted += zone =>
        {
            for (int i = 0; i < run.Zones.Count; i++)
            {
                if (run.Zones[i] == zone)
                {
                    Report(seat, NetRaceRun.Zone, i, zone.CompletedAt);
                }
            }
        };
        run.RunCompleted += () => Report(seat, NetRaceRun.Finished, runTime: run.Elapsed);
    }

    /// <summary>A guest's report of one event of its own seat's run. A start numbers a new run;
    /// every other event names the run in progress.</summary>
    internal void Report(int seat, NetRaceRun kind, int zone = RaceRunMessage.NoZone, float runTime = 0f)
    {
        if (_net.IsHost)
        {
            return;
        }

        if (_ownRunsRound != _race.Round)
        {
            _ownRuns.Clear();
            _ownRunsRound = _race.Round;
        }

        ushort run = _ownRuns.GetValueOrDefault(seat);
        if (kind == NetRaceRun.Started)
        {
            _ownRuns[seat] = ++run;
        }
        else if (run == 0)
        {
            // No run of this window to name, as when a new window resets the seat's run.
            return;
        }

        _net.Send(_net.HostPeer,
            new RaceRunMessage((byte)seat, kind, (byte)Math.Clamp(zone, 0, RaceRunMessage.NoZone), (byte)_race.Round, run, runTime),
            NetChannels.Events);
    }

    /// <summary>Once per sim step, after the race has advanced. The host sends each changed racer's
    /// line, then its clock once a second and on every change of phase. A guest's first step asks
    /// the host's clock again, now that both clocks run.</summary>
    internal void Step()
    {
        if (_cadence is { } cadence)
        {
            Flush(cadence.StepSends());
        }
        else if (!_askedClock)
        {
            _askedClock = true;
            _ping?.AskSoon();
        }
    }

    // The lines go first: a guest's board wakes on the ending, so every line the host decided
    // before it must already stand there. One ordered channel carries both.
    private void Flush(bool tick)
    {
        foreach (var racer in _race.Racers)
        {
            if (_sentRevisions.GetValueOrDefault(racer.Index) != racer.Revision)
            {
                _sentRevisions[racer.Index] = racer.Revision;
                _net.Broadcast(LineOf(racer), NetChannels.Events);
                Lines++;
            }
        }

        if (tick || _sentPhase != _race.Phase || _sentRound != _race.Round)
        {
            _sentPhase = _race.Phase;
            _sentRound = _race.Round;
            float elapsed = _race.Phase == StuntRacePhase.Opening ? _race.OpeningElapsed : _race.WindowElapsed;
            _net.Broadcast(new RaceStateMessage((NetRacePhase)_race.Phase, (byte)_race.Round, elapsed,
                _race.WindowSeconds, (float)_clock()), NetChannels.Events);
            States++;
        }
    }

    private RaceStandingMessage LineOf(Racer racer)
    {
        var line = racer.Line();
        var splits = new float[line.Splits.Count];
        for (int i = 0; i < splits.Length; i++)
        {
            splits[i] = line.Splits[i] ?? RaceStandingMessage.NoSplit;
        }

        return new RaceStandingMessage((byte)racer.Index, (byte)_race.Round, line.InRun, line.BestTime != null,
            (ushort)Math.Min(line.RunsStarted, ushort.MaxValue), (ushort)Math.Min(line.RunsFinished, ushort.MaxValue),
            line.BestTime ?? 0f, line.TimeToMostZones, (byte)Math.Min(line.MostZones, byte.MaxValue),
            (byte)Math.Min(line.CurrentZones, byte.MaxValue), splits, line.Left);
    }

    // A call is taken only from the end that may make it: a restart or the lobby from the host, a
    // leave from a guest. A restart must name the window after this guest's own, which it opens.
    private void TakeCall(int peer, RaceCallMessage call)
    {
        if (_net.IsHost)
        {
            if (call.Call == NetRaceCall.Leave && peer != _net.LocalPeer)
            {
                CallsTaken++;
                Log.Info("flight", $"net race: peer {peer} left the race");
                GuestLeft?.Invoke(peer);
            }

            return;
        }

        if (peer != _net.HostPeer)
        {
            return;
        }

        if (call.Call == NetRaceCall.Restart && call.Round == (byte)(_race.Round + 1))
        {
            CallsTaken++;
            Log.Info("flight", $"net race: the host opened window {call.Round}");
            Restarted?.Invoke();
        }
        else if (call.Call == NetRaceCall.Lobby && call.Round == (byte)_race.Round)
        {
            CallsTaken++;
            LobbyCalled = true;
            Log.Info("flight", $"net race: the host took the race back to the lobby");
        }
        else
        {
            Log.Info("flight", $"net race: dropped the host's {call.Call} call for window {call.Round} in window {_race.Round}");
        }
    }

    // The host's intake. Only the machine flying a seat reports it, under the current window; a
    // repeated start and an event of a run already superseded are dropped. The race then applies
    // its own rules: no start outside the open window, nothing after the end.
    private void TakeReport(int peer, RaceRunMessage report)
    {
        if (peer == _net.LocalPeer || _net.PeerOfSeat(report.Seat) != peer)
        {
            Refuse(report, "from a machine that does not fly the seat");
            return;
        }

        if (report.Round != (byte)_race.Round)
        {
            Refuse(report, "of another window");
            return;
        }

        if (report.Kind is NetRaceRun.Zone or NetRaceRun.Finished && !(float.IsFinite(report.RunTime) && report.RunTime >= 0f))
        {
            Refuse(report, "with no run time");
            return;
        }

        if (_ownerRunsRound != _race.Round)
        {
            _ownerRuns.Clear();
            _ownerRunsRound = _race.Round;
        }

        ushort newest = _ownerRuns.GetValueOrDefault(report.Seat);
        bool fresh = report.Kind == NetRaceRun.Started ? report.Run > newest : newest != 0 && report.Run == newest;
        if (!fresh)
        {
            Refuse(report, report.Kind == NetRaceRun.Started ? "a repeated start" : "of a run already superseded");
            return;
        }

        ReportsTaken++;
        switch (report.Kind)
        {
            case NetRaceRun.Started:
                _ownerRuns[report.Seat] = report.Run;
                if (!_race.RunStarted(report.Seat))
                {
                    Log.Info("flight", $"net race: seat {report.Seat}'s run {report.Run} started outside the open window or after its pilot left, not counted");
                }

                break;
            case NetRaceRun.Zone:
                _race.ZoneCleared(report.Seat, report.Zone, report.RunTime);
                break;
            case NetRaceRun.Finished:
                _race.RunFinished(report.Seat, report.RunTime);
                break;
            case NetRaceRun.Abandoned:
                _race.RunAbandoned(report.Seat);
                break;
            default:
                ReportsTaken--;
                Refuse(report, "of an unknown kind");
                return;
        }

        // At once, not on the next step: a finish that ends the race halts the host's simulation.
        Flush(false);
    }

    private void Refuse(in RaceRunMessage report, string why)
    {
        ReportsRefused++;
        Log.Info("flight", $"net race: dropped seat {report.Seat}'s {report.Kind} report (run {report.Run}, round {report.Round}), {why}");
    }

    // A guest takes a line only under its own window. The host's restart call reaches it ahead of
    // the next window's lines, so a line under another round is an old window's.
    private void TakeLine(in RaceStandingMessage line)
    {
        if (line.Round != (byte)_race.Round)
        {
            return;
        }

        var splits = new float?[line.Splits.Count];
        for (int i = 0; i < splits.Length; i++)
        {
            splits[i] = line.Splits[i] >= 0f ? line.Splits[i] : null;
        }

        _race.TakeLine(line.Seat, new RacerLine(line.InRun, line.RunsStarted, line.RunsFinished,
            line.Completed ? line.BestTime : null, line.MostZones, line.TimeToMostZones, line.CurrentZones, splits, line.Left));
        Lines++;
    }

    // The host's clock, read forward by how long it spent on the link. The lateness takes the
    // newest clock reading, not the offset still walking to it: a catch-up is applied once.
    private void TakeState(in RaceStateMessage state)
    {
        if (state.Round != (byte)_race.Round || state.Phase > NetRacePhase.Ended
            || !float.IsFinite(state.Elapsed) || !float.IsFinite(state.HostClock))
        {
            return;
        }

        States++;
        if (!_windowMismatchLogged && state.WindowSeconds != _race.WindowSeconds)
        {
            _windowMismatchLogged = true;
            Log.Warn("flight", $"net race: the host's window is {state.WindowSeconds:0} s and this machine's {_race.WindowSeconds:0} s; the host's clock decides");
        }

        double lateness = _slew is { } slew ? Math.Max(0.0, _clock() + slew.Target - state.HostClock) : 0.0;
        float caught = _race.TakeHostClock((StuntRacePhase)state.Phase, state.Elapsed + lateness, _stepSeconds);
        if (caught > 0f)
        {
            CaughtUp += caught;
            CatchUp?.Invoke(caught);
            Log.Info("flight", $"net race: the opening count caught up {caught:0.000} s to the host's ({lateness:0.000} s on the link)");
        }
    }
}
