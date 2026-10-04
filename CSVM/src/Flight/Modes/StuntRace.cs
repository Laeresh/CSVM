using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Where a time-attack stunt race stands.</summary>
public enum StuntRacePhase
{
    /// <summary>The opening count runs, and no run may start.</summary>
    Opening,

    /// <summary>The window clock runs, and any pilot may start or restart a run.</summary>
    Open,

    /// <summary>Time is up. A run in progress may still finish inside the cap; none may start.</summary>
    FinalRun,

    /// <summary>The race is over and its board is due.</summary>
    Ended,
}

/// <summary>One pilot of a time-attack stunt race: their best completed run, their furthest run,
/// and the run they are flying now. Every field is fed through <see cref="StuntRace"/>'s event
/// entry points, never read off an aircraft, so a network peer's pilot is the same record.</summary>
public sealed class Racer
{
    private readonly float?[] _current;
    private readonly float?[] _best;
    private readonly float?[] _furthest;

    internal Racer(int index, int zoneCount)
    {
        Index = index;
        _current = new float?[zoneCount];
        _best = new float?[zoneCount];
        _furthest = new float?[zoneCount];
        Callsign = Tag;
    }

    /// <summary>0-based player index (player 1 = 0), also their pane, colour and tag.</summary>
    public int Index { get; }

    /// <summary>Readable aircraft name for the boards ("Bloodhawk").</summary>
    public string PlaneDisplay { get; set; } = "";

    /// <summary>The name the boards show. The player tag unless a network seat names the pilot.</summary>
    public string Callsign { get; set; }

    /// <summary>The <see cref="ScoreStore"/> key this pilot's best records under, the solo run's
    /// <c>chapter/mission/plane</c>. Empty records nothing.</summary>
    public string ScoreKey { get; set; } = "";

    /// <summary>The fastest completed run's time, or null before the first completed run.</summary>
    public float? BestTime { get; private set; }

    /// <summary>The most zones any one run of this pilot cleared, the current run included.</summary>
    public int MostZones { get; private set; }

    /// <summary>The lowest run time at which a run reached <see cref="MostZones"/>.</summary>
    public float TimeToMostZones { get; private set; }

    /// <summary>Runs started inside the window.</summary>
    public int RunsStarted { get; private set; }

    /// <summary>Runs completed, every zone cleared, that the race counted.</summary>
    public int RunsFinished { get; private set; }

    /// <summary>Whether a counted run is in progress: started in the window, not yet completed
    /// or thrown away.</summary>
    public bool InRun { get; private set; }

    /// <summary>Zones the run in progress has cleared.</summary>
    public int CurrentZones { get; private set; }

    public bool Finished => BestTime != null;

    /// <summary>The run time each zone was cleared at, by course index, in the run that ranks this
    /// pilot: the best completed run, else the furthest. Null where that run never cleared it.</summary>
    public IReadOnlyList<float?> Splits => Finished ? _best : _furthest;

    /// <summary>The player's identity colour, shared with the launchscreen's join strip and plane
    /// select so a player recognises "their" colour from menu to results.</summary>
    public Color Color => UI.Boards.SplitScreen.PlayerColor(Index);

    public string Tag => UI.Boards.SplitScreen.PlayerTag(Index);

    internal void Start()
    {
        InRun = true;
        RunsStarted++;
        CurrentZones = 0;
        Array.Clear(_current);
    }

    internal void Stop() => InRun = false;

    // A first clearing of a course zone in the run in progress; the furthest run follows it.
    internal bool ClearZone(int zone, float runTime)
    {
        if (!InRun || zone < 0 || zone >= _current.Length || _current[zone] != null)
            return false;
        _current[zone] = runTime;
        CurrentZones++;
        if (CurrentZones > MostZones || (CurrentZones == MostZones && runTime < TimeToMostZones))
        {
            MostZones = CurrentZones;
            TimeToMostZones = runTime;
            Array.Copy(_current, _furthest, _current.Length);
        }
        return true;
    }

    // The run in progress completed; answers whether it is the new best.
    internal bool Finish(float runTime)
    {
        InRun = false;
        RunsFinished++;
        if (BestTime is { } best && best <= runTime)
            return false;
        BestTime = runTime;
        Array.Copy(_current, _best, _current.Length);
        return true;
    }

    internal void Clear()
    {
        BestTime = null;
        MostZones = 0;
        TimeToMostZones = 0f;
        RunsStarted = 0;
        RunsFinished = 0;
        InRun = false;
        CurrentZones = 0;
        Array.Clear(_current);
        Array.Clear(_best);
        Array.Clear(_furthest);
    }
}

/// <summary>
/// The time-attack stunt race: one shared window over one course, every pilot flying as many runs
/// as it allows, ranked by their best completed run. Engine-free bookkeeping fed by explicit
/// events: the opening's length, time advanced, a run started, a zone cleared, a run finished or
/// thrown away. A network host can feed the same calls from its peers' reports, and
/// <see cref="Follow"/> is the local wiring off a <see cref="StuntMission"/>. The window clock
/// starts at the opening count's GO; at time up a run in progress may finish, inside
/// <see cref="FinalRunCap"/>, and no run starts. Not a Node: it is freed with the session.
/// </summary>
public sealed class StuntRace
{
    /// <summary>How long after time up a run in progress may still finish, seconds.</summary>
    public const float FinalRunCap = 120f;

    // The start count's own boundary rule. A window opening on the seats' steps then opens on
    // the step of their GO.
    private const float OpeningTolerance = 1e-4f;

    private readonly List<Racer> _racers = new();
    private float _openingSeconds;
    private float _openingElapsed;
    private double _windowElapsed;

    /// <param name="windowSeconds">The window's length from the opening GO to time up.</param>
    /// <param name="zoneCount">How many Danger Zones the course has.</param>
    public StuntRace(float windowSeconds, int zoneCount)
    {
        WindowSeconds = Mathf.Max(0f, windowSeconds);
        ZoneCount = zoneCount;
    }

    /// <summary>Fired once when the race ends (the results board wakes on it).</summary>
    public event Action? RaceCompleted;

    /// <summary>Fired when a completed run is its pilot's fastest in this race so far.</summary>
    public event Action<Racer>? BestImproved;

    public float WindowSeconds { get; }

    public int ZoneCount { get; }

    public StuntRacePhase Phase { get; private set; } = StuntRacePhase.Opening;

    /// <summary>Seconds since the opening GO; zero during the opening.</summary>
    public float WindowElapsed => (float)_windowElapsed;

    /// <summary>Seconds of the window left: all of it during the opening, none after time up.</summary>
    public float TimeLeft => Phase switch
    {
        StuntRacePhase.Opening => WindowSeconds,
        StuntRacePhase.Open => Mathf.Max(0f, (float)(WindowSeconds - _windowElapsed)),
        _ => 0f,
    };

    /// <summary>Seconds of the final-run stretch left while it runs, zero otherwise.</summary>
    public float FinalRunLeft => Phase == StuntRacePhase.FinalRun
        ? Mathf.Max(0f, (float)(WindowSeconds + FinalRunCap - _windowElapsed))
        : 0f;

    /// <summary>Whether a run may start or restart now: only while the window is open.</summary>
    public bool MayStartRun => Phase == StuntRacePhase.Open;

    public bool Ended => Phase == StuntRacePhase.Ended;

    /// <summary>The racers in player order (index 0 = player 1).</summary>
    public IReadOnlyList<Racer> Racers => _racers;

    /// <summary>"1st" / "2nd" / "3rd" / "4th", placings, shared by the run HUD and the boards.</summary>
    public static string Ordinal(int rank) => rank switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{rank}th",
    };

    /// <summary>A window clock as "m:ss", rounded up so it reads 0:00 only at time up.</summary>
    public static string FormatClock(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", total / 60, total % 60);
    }

    /// <summary>A signed gap between two run times: "+1.3", or "+1:02.5" past a minute.</summary>
    public static string FormatGap(float seconds)
    {
        string sign = seconds < 0f ? "-" : "+";
        float magnitude = Mathf.Abs(seconds);
        return magnitude < 60f
            ? sign + magnitude.ToString("0.0", CultureInfo.InvariantCulture)
            : sign + StuntMission.FormatTime(magnitude);
    }

    /// <summary>A board's best column for one pilot: the best time, or the furthest run's zones out
    /// of <paramref name="zoneCount"/> for a pilot with no completed run.</summary>
    public static string BestText(Racer racer, int zoneCount) =>
        racer.BestTime is { } best ? StuntMission.FormatTime(best) : $"{racer.MostZones}/{zoneCount} ZONES";

    /// <summary>A board's gap column for one pilot: behind <paramref name="winner"/>'s best, and
    /// blank for the winner. A pilot with no completed run shows the time to the furthest zones.
    /// </summary>
    public static string GapText(Racer racer, float? winner) =>
        racer.BestTime is { } best ? (winner is { } w && best > w ? FormatGap(best - w) : "")
        : racer.MostZones > 0 ? $"at {StuntMission.FormatTime(racer.TimeToMostZones)}" : "";

    /// <summary>Enters a player. Call in player order at session build.</summary>
    public Racer Add(int index, string planeDisplay, string scoreKey = "")
    {
        var racer = new Racer(index, ZoneCount) { PlaneDisplay = planeDisplay, ScoreKey = scoreKey };
        _racers.Add(racer);
        return racer;
    }

    /// <summary>The racer flying as player <paramref name="index"/>, or null.</summary>
    public Racer? Of(int index)
    {
        foreach (var r in _racers)
            if (r.Index == index)
                return r;
        return null;
    }

    /// <summary>Feeds this race from player <paramref name="index"/>'s own run: its clock
    /// starting, each zone, its completion and a restart throwing it away.</summary>
    public void Follow(int index, StuntMission run)
    {
        run.RunStarted += () => RunStarted(index);
        run.RunReset += () => RunAbandoned(index);
        run.ZoneCompleted += zone =>
        {
            for (int i = 0; i < run.Zones.Count; i++)
                if (run.Zones[i] == zone)
                    ZoneCleared(index, i, zone.CompletedAt);
        };
        run.RunCompleted += () => RunFinished(index, run.Elapsed);
    }

    /// <summary>The opening count has begun on every seat: the window opens after
    /// <paramref name="seconds"/> of <see cref="Advance"/>, at once for none.</summary>
    public void BeginOpening(float seconds)
    {
        Phase = StuntRacePhase.Opening;
        _openingSeconds = seconds;
        _openingElapsed = 0f;
        _windowElapsed = 0;
        if (seconds <= 0f)
            OpenWindow();
    }

    /// <summary>One step of the shared clock: the opening count, the window and the final-run
    /// stretch, in turn. Call once per sim step after every seat has stepped.</summary>
    public void Advance(float dt)
    {
        switch (Phase)
        {
            case StuntRacePhase.Opening:
                _openingElapsed += dt;
                if (_openingSeconds - _openingElapsed <= OpeningTolerance)
                    OpenWindow();
                break;
            case StuntRacePhase.Open:
                _windowElapsed += dt;
                if (_windowElapsed >= WindowSeconds)
                    TimeUp();
                break;
            case StuntRacePhase.FinalRun:
                _windowElapsed += dt;
                if (_windowElapsed >= WindowSeconds + FinalRunCap)
                    End();
                break;
        }
    }

    /// <summary>Player <paramref name="index"/>'s run clock started. Counted only while the window
    /// is open; answers whether it was.</summary>
    public bool RunStarted(int index)
    {
        if (Of(index) is not { } racer)
            return false;
        if (!MayStartRun)
        {
            racer.Stop();
            EndIfNoRunLeft();
            return false;
        }
        racer.Start();
        return true;
    }

    /// <summary>Player <paramref name="index"/> threw the run in progress away.</summary>
    public void RunAbandoned(int index)
    {
        if (Of(index) is not { InRun: true } racer)
            return;
        racer.Stop();
        EndIfNoRunLeft();
    }

    /// <summary>The run in progress cleared course zone <paramref name="zone"/> at run time
    /// <paramref name="runTime"/>. A zone already cleared in this run, or no run, counts nothing.</summary>
    public void ZoneCleared(int index, int zone, float runTime)
    {
        if (Phase is StuntRacePhase.Open or StuntRacePhase.FinalRun)
            Of(index)?.ClearZone(zone, runTime);
    }

    /// <summary>The run in progress completed in <paramref name="runTime"/>. A completion with
    /// no counted run, or after the race ended, counts nothing.</summary>
    public void RunFinished(int index, float runTime)
    {
        if (Phase is not (StuntRacePhase.Open or StuntRacePhase.FinalRun)
            || Of(index) is not { InRun: true } racer)
            return;
        bool best = racer.Finish(runTime);
        Log.Info("flight",
            $"stunt race: {racer.Tag} run {racer.RunsFinished} in {StuntMission.FormatTime(runTime)}{(best ? ", best so far" : "")} ({racer.PlaneDisplay})");
        if (best)
            BestImproved?.Invoke(racer);
        EndIfNoRunLeft();
    }

    /// <summary>A new window over the same field: every pilot's runs cleared and the race back
    /// before its opening, which <see cref="BeginOpening"/> starts again.</summary>
    public void Restart()
    {
        foreach (var r in _racers)
            r.Clear();
        Phase = StuntRacePhase.Opening;
        _openingSeconds = 0f;
        _openingElapsed = 0f;
        _windowElapsed = 0;
    }

    /// <summary>The field in race order: completed runs by best time first. Pilots with none
    /// follow by most zones in any run, then the lower time to them, then player order.</summary>
    public List<Racer> Standings()
    {
        var ordered = new List<Racer>(_racers);
        ordered.Sort(Compare);
        return ordered;
    }

    /// <summary>Player <paramref name="index"/>'s place in <see cref="Standings"/>, 1-based, or 0.</summary>
    public int PlaceOf(int index) => Standings().FindIndex(r => r.Index == index) + 1;

    /// <summary>Player <paramref name="index"/>'s live leaderboard line: the window clock, or FINAL
    /// RUN after time up, then their place, the leader's best and their gap to it. The leader's
    /// gap is their lead over second.</summary>
    public string LeaderboardLine(int index)
    {
        string clock = Phase switch
        {
            StuntRacePhase.Opening or StuntRacePhase.Open => $"TIME {FormatClock(TimeLeft)}",
            StuntRacePhase.FinalRun => $"FINAL RUN {FormatClock(FinalRunLeft)}",
            _ => "TIME UP",
        };
        var order = Standings();
        int place = order.FindIndex(r => r.Index == index) + 1;
        if (place == 0)
            return clock;
        var me = order[place - 1];
        var leader = order[0];
        string line = $"{clock}   {Ordinal(place)}/{order.Count}   LEADER "
            + (leader.BestTime is { } lead ? $"{leader.Callsign} {StuntMission.FormatTime(lead)}" : "--");
        if (me.BestTime is not { } mine)
            return line + "   NO TIME";
        if (me != leader)
            return line + "   " + FormatGap(mine - leader.BestTime!.Value);
        return order.Count > 1 && order[1].BestTime is { } second ? line + "   " + FormatGap(mine - second) : line;
    }

    internal void Remove(int index)
    {
        int at = _racers.FindIndex(racer => racer.Index == index);
        if (at >= 0)
            _racers.RemoveAt(at);
    }

    private static int Compare(Racer a, Racer b)
    {
        if (a.BestTime is { } ta && b.BestTime is { } tb)
        {
            int byBest = ta.CompareTo(tb);
            return byBest != 0 ? byBest : a.Index.CompareTo(b.Index);
        }
        if (a.Finished != b.Finished)
            return a.Finished ? -1 : 1;
        int zones = b.MostZones.CompareTo(a.MostZones);
        if (zones != 0)
            return zones;
        int time = a.MostZones > 0 ? a.TimeToMostZones.CompareTo(b.TimeToMostZones) : 0;
        return time != 0 ? time : a.Index.CompareTo(b.Index);
    }

    private void OpenWindow()
    {
        Phase = StuntRacePhase.Open;
        _windowElapsed = 0;
        Log.Info("flight", $"stunt race: GO, a {FormatClock(WindowSeconds)} window for {_racers.Count} pilots");
    }

    private void TimeUp()
    {
        Phase = StuntRacePhase.FinalRun;
        int running = _racers.FindAll(r => r.InRun).Count;
        Log.Info("flight", $"stunt race: time up, {running} run(s) in progress may finish inside {FinalRunCap:0} s");
        EndIfNoRunLeft();
    }

    private void EndIfNoRunLeft()
    {
        if (Phase == StuntRacePhase.FinalRun && !_racers.Exists(r => r.InRun))
            End();
    }

    private void End()
    {
        foreach (var r in _racers)
            r.Stop();
        Phase = StuntRacePhase.Ended;
        Log.Info("flight", $"stunt race: RACE COMPLETE after {StuntMission.FormatTime(WindowElapsed)}");
        RaceCompleted?.Invoke();
    }
}
