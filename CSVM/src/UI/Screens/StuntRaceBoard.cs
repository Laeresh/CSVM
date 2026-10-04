using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// The shared results board for a time-attack stunt race, <see cref="ResultsBoard"/>'s shell. It
/// covers the whole window over the splitscreen panes, because the race ends for everybody at once.
/// One row per pilot in race order: placing, callsign, aircraft, best time, gap and runs flown.
/// A pilot with no completed run shows their furthest run's zones and the time to them.
/// Under the rows stand each pilot's best-run splits, one row per Danger Zone. R and pad Y reach
/// Restart directly; detail is in this module's docs/architecture/UI.md entry.</summary>
public sealed partial class StuntRaceBoard : ResultsBoard
{
    // The board's sizes, chrome type scale rungs at the boards' 720p reference, scaled by
    // window height. The heading's are StuntScoreboard's, so the solo and race boards match.
    private static readonly float TitleFont = ChromeType.InReference(ChromeSize.Title, ReferenceHeight);
    private static readonly float ContextFont = ChromeType.InReference(ChromeSize.Caption, ReferenceHeight);
    private static readonly float HeaderFont = ChromeType.InReference(ChromeSize.Note, ReferenceHeight);
    private static readonly float RowFont = ChromeType.InReference(ChromeSize.Text, ReferenceHeight);
    private static readonly float SplitFont = ChromeType.InReference(ChromeSize.Caption, ReferenceHeight);

    private readonly List<string> _rows = new();
    private StuntRace _race = null!;
    private IReadOnlyList<string> _zoneNames = System.Array.Empty<string>();
    private string _context = "";

    /// <summary>The ranked rows as drawn, one line per pilot, for the suites and the log.</summary>
    internal IReadOnlyList<string> Rows => _rows;

    // A new window puts the race back before its opening, which the shell turns into the retire.
    protected override bool StillEnded => _race.Ended;

    /// <summary>Builds the (hidden) board and subscribes to the race's end. Add it to a CanvasLayer
    /// above the splitscreen panes; it wakes itself on <see cref="StuntRace.RaceCompleted"/> and
    /// retires on a restart. <paramref name="zoneNames"/> names the course's zones in course order.</summary>
    public static StuntRaceBoard Build(StuntRace race, IReadOnlyList<string> zoneNames, string context,
        bool exitsToMenu, PauseState state, System.Func<int, MenuInput> inputFor)
    {
        var board = new StuntRaceBoard { _race = race, _zoneNames = zoneNames, _context = context };
        board.InitShell(state, exitsToMenu, inputFor);
        race.RaceCompleted += board.OnRaceCompleted;
        return board;
    }

    public override void _ExitTree() => _race.RaceCompleted -= OnRaceCompleted;

    // The best column of one pilot: the best time, or the furthest run's zones.
    private static string BestText(Racer r, int zoneCount) =>
        r.BestTime is { } best ? StuntMission.FormatTime(best) : $"{r.MostZones}/{zoneCount} ZONES";

    private void OnRaceCompleted()
    {
        var standings = _race.Standings();
        _rows.Clear();
        float? winner = standings.Count > 0 ? standings[0].BestTime : null;
        for (int i = 0; i < standings.Count; i++)
        {
            var r = standings[i];
            _rows.Add($"{StuntRace.Ordinal(i + 1)}  {r.Callsign}  {r.PlaneDisplay}  {BestText(r, _race.ZoneCount)}  {GapText(r, winner)}  {r.RunsFinished}/{r.RunsStarted}");
        }

        // Log the final order too, so a race is reviewable from a headless run's log.
        Log.Info("flight", $"stunt race results:");
        foreach (var row in _rows)
            Log.Info("flight", $"  {row}");
        Populate(standings, winner);
        Wake();
    }

    // The gap column: behind the winner, or the time to the furthest run's zones.
    private string GapText(Racer r, float? winner) =>
        r.BestTime is { } best ? (winner is { } w && best > w ? StuntRace.FormatGap(best - w) : "")
        : r.MostZones > 0 ? $"at {StuntMission.FormatTime(r.TimeToMostZones)}" : "";

    private void Populate(List<Racer> standings, float? winner)
    {
        float s = BoardScale();
        var body = BeginPanel(s);

        body.AddChild(Centered(Label("STUNT RACE RESULTS", (int)(TitleFont * s), TitleColor)));
        body.AddChild(Centered(Label(_context, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));

        // One row per pilot: placing | callsign | aircraft | best | gap | runs.
        var grid = new GridContainer { Columns = 6 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(22f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);

        int rankW = (int)(52f * s), nameW = (int)(70f * s), planeW = (int)(170f * s),
            bestW = (int)(110f * s), gapW = (int)(90f * s), runsW = (int)(60f * s);
        int header = (int)(HeaderFont * s);
        AddCell(grid, "", header, HeaderColor, HorizontalAlignment.Left, rankW);
        AddCell(grid, "PILOT", header, HeaderColor, HorizontalAlignment.Left, nameW);
        AddCell(grid, "AIRCRAFT", header, HeaderColor, HorizontalAlignment.Left, planeW);
        AddCell(grid, "BEST", header, HeaderColor, HorizontalAlignment.Right, bestW);
        AddCell(grid, "", header, HeaderColor, HorizontalAlignment.Right, gapW);
        AddCell(grid, "RUNS", header, HeaderColor, HorizontalAlignment.Right, runsW);

        int font = (int)(RowFont * s);
        for (int i = 0; i < standings.Count; i++)
        {
            var r = standings[i];
            // The winner's row wears their own identity colour; everyone else stays neutral so the
            // placing reads at a glance.
            var color = i == 0 && r.Finished ? r.Color : RowColor;
            AddCell(grid, StuntRace.Ordinal(i + 1), font, color, HorizontalAlignment.Left, rankW);
            AddCell(grid, r.Callsign, font, r.Color, HorizontalAlignment.Left, nameW);
            AddCell(grid, r.PlaneDisplay, font, color, HorizontalAlignment.Left, planeW);
            AddCell(grid, BestText(r, _race.ZoneCount), font, color, HorizontalAlignment.Right, bestW);
            AddCell(grid, GapText(r, winner), font, color, HorizontalAlignment.Right, gapW);
            AddCell(grid, $"{r.RunsFinished}/{r.RunsStarted}", font, color, HorizontalAlignment.Right, runsW);
        }

        body.AddChild(Separator(s));
        AddSplits(body, standings, s);
        body.AddChild(Separator(s));
        AddStandardMenu(body, s);
    }

    // Each pilot's best-run splits: a row per zone in course order, a column per pilot in race
    // order. A cell is the run time the zone was cleared at. The course is order-free, so the
    // times themselves show each pilot's own order.
    private void AddSplits(VBoxContainer body, List<Racer> standings, float s)
    {
        int font = (int)(SplitFont * s);
        body.AddChild(Centered(Label("BEST-RUN SPLITS", (int)(HeaderFont * s), HeaderColor)));
        var grid = new GridContainer { Columns = standings.Count + 1 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(18f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(2f * s));
        body.AddChild(grid);

        int zoneW = (int)(190f * s), cellW = (int)(70f * s);
        AddCell(grid, "", font, HeaderColor, HorizontalAlignment.Left, zoneW);
        foreach (var r in standings)
            AddCell(grid, r.Callsign, font, r.Color, HorizontalAlignment.Right, cellW);
        for (int zone = 0; zone < _race.ZoneCount; zone++)
        {
            string name = zone < _zoneNames.Count && _zoneNames[zone].Length > 0 ? _zoneNames[zone] : $"Zone {zone + 1}";
            AddCell(grid, name, font, RowColor, HorizontalAlignment.Left, zoneW);
            foreach (var r in standings)
            {
                string cell = r.Splits[zone] is { } at ? StuntMission.FormatTime(at) : "-";
                AddCell(grid, cell, font, RowColor, HorizontalAlignment.Right, cellW);
            }
        }
    }
}
