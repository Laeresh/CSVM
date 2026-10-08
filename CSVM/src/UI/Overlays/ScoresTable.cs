using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.UI.Screens;

namespace CSVM.UI.Overlays;

/// <summary>One line of a held scores table, its cells in column order. <c>Seat</c> is whose identity
/// colour its name cell wears, -1 for a team's line.</summary>
public sealed record ScoresRow(IReadOnlyList<string> Cells, int Seat, bool Team);

/// <summary>
/// The standings a held Display Scores shows under the Built-in presentation, in the mode's results
/// board columns. A race shows place, pilot, aircraft, best, gap and runs. A Dogfight shows place,
/// pilot, score, kills and deaths, a team match's lines first. Engine-free;
/// <see cref="ScoresOverlay"/> draws it. Module entry: docs/architecture/UI.md.
/// </summary>
public sealed class ScoresTable
{
    private ScoresTable(IReadOnlyList<string> headers, IReadOnlyList<bool> rightAligned, IReadOnlyList<ScoresRow> rows)
    {
        Headers = headers;
        RightAligned = rightAligned;
        Rows = rows;
    }

    /// <summary>The column headers, blank over the place.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Per column, whether its cells stand against the right edge (the figures).</summary>
    public IReadOnlyList<bool> RightAligned { get; }

    /// <summary>The lines in standing order.</summary>
    public IReadOnlyList<ScoresRow> Rows { get; }

    /// <summary>A race's standings, ranked by best run, in <c>StuntRaceBoard</c>'s words, a pilot who
    /// left marked so.</summary>
    public static ScoresTable Race(StuntRace race)
    {
        ArgumentNullException.ThrowIfNull(race);
        var rows = new List<ScoresRow>();
        foreach (var row in RaceRows.Of(race.Standings(), race.ZoneCount))
        {
            rows.Add(new ScoresRow(new[] { row.Place, row.Name, row.Aircraft, row.Best, row.Gap, row.Runs }, row.Racer.Index, false));
        }

        return new ScoresTable(
            new[] { "", "PILOT", "AIRCRAFT", "BEST", "GAP", "RUNS" },
            new[] { false, false, false, true, true, true }, rows);
    }

    /// <summary>A Dogfight's standings by score, in the Dogfight board's columns, each seat named by
    /// <paramref name="name"/>; a team match's lines lead.</summary>
    public static ScoresTable Dogfight(VersusMatch match, Func<int, string> name)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(name);
        var rows = new List<ScoresRow>();
        foreach (var team in match.TeamStandings())
            rows.Add(new ScoresRow(Figures(team.Rank, team.Name, team.Score, team.Kills, team.Deaths), -1, true));
        foreach (var st in match.Standings())
            rows.Add(new ScoresRow(Figures(st.Rank, name(st.PlayerIndex), st.Score, st.Kills, st.Deaths), st.PlayerIndex, false));
        return new ScoresTable(
            new[] { "", "PILOT", "SCORE", "KILLS", "DEATHS" },
            new[] { false, false, true, true, true }, rows);
    }

    private static string[] Figures(int rank, string who, int score, int kills, int deaths) => new[]
    {
        "#" + rank.ToString(CultureInfo.InvariantCulture), who,
        score.ToString(CultureInfo.InvariantCulture), kills.ToString(CultureInfo.InvariantCulture),
        deaths.ToString(CultureInfo.InvariantCulture),
    };
}

/// <summary>What one session's held Display Scores reads: its race or its Dogfight, the seats' names
/// and flags, and the original's header words. A source with neither mode shows nothing, which is
/// every solo and campaign flight.</summary>
public sealed class ScoresSource
{
    /// <summary>The race, or null outside one.</summary>
    public StuntRace? Race { get; init; }

    /// <summary>The Dogfight, or null outside one.</summary>
    public VersusMatch? Match { get; init; }

    /// <summary>A seat's name: the network callsign, else the splitscreen tag.</summary>
    public Func<int, string> Name { get; init; } = SplitScreen.PlayerTag;

    /// <summary>The team whose flag a seat carries, 0 for none; null outside Capture the Flag.</summary>
    public Func<int, int>? Carried { get; init; }

    /// <summary>The original's header words.</summary>
    public OriginalScoresWords Words { get; init; } = OriginalScoresWords.Fallback;

    /// <summary>Whether a mode with scores stands behind this source.</summary>
    public bool HasScores => Race != null || Match != null;

    /// <summary>The Built-in table, null with no mode.</summary>
    public ScoresTable? Table() =>
        Race != null ? ScoresTable.Race(Race) : Match != null ? ScoresTable.Dogfight(Match, Name) : null;

    /// <summary>The Original text, empty with no mode.</summary>
    public IReadOnlyList<OriginalScoresLine> OriginalLines() =>
        Race != null ? OriginalScoresText.Race(Race, Words)
        : Match != null ? OriginalScoresText.Dogfight(Match, Name, Carried, Words)
        : Array.Empty<OriginalScoresLine>();
}
