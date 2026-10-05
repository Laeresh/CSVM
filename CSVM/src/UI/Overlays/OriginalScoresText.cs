using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;

namespace CSVM.UI.Overlays;

/// <summary>One line of the original's in-flight scores text. <c>Flag</c> is the team whose flag
/// the pilot on it carries, 0 for none; the original marks it with an "F" ahead of the line.</summary>
public readonly record struct OriginalScoresLine(string Text, int Flag);

/// <summary>The header words the original's scores text opens with: the message table's
/// <c>MSG_MPHUD_PLAYER</c>, <c>MSG_MPHUD_PLAYER_TEAM</c> and <c>MSG_MPHUD_SCORE</c>.</summary>
public sealed record OriginalScoresWords(string Player, string PlayerTeam, string Score)
{
    /// <summary>What a table read without the install's messages says, plain English in the
    /// columns the shipped strings fill.</summary>
    public static readonly OriginalScoresWords Fallback = new("  Player", "  Player/Team", "score");

    /// <summary>The three headers out of <paramref name="messages"/>, each the fallback's where the
    /// table lacks it.</summary>
    public static OriginalScoresWords From(Messages? messages) => new(
        Word(messages, "MSG_MPHUD_PLAYER", Fallback.Player),
        Word(messages, "MSG_MPHUD_PLAYER_TEAM", Fallback.PlayerTeam),
        Word(messages, "MSG_MPHUD_SCORE", Fallback.Score));

    private static string Word(Messages? messages, string key, string fallback) =>
        messages?.Get(key) is { } text && text != key ? text : fallback;
}

/// <summary>
/// The original's in-flight scores, the text Display Scores raises in the HUD, as monospaced lines.
/// A Dogfight's lines are the original's own: a header, then one pilot per line. The name stands in a
/// 21-character column and the score in a 7-character one, then the remake's kills and deaths. A
/// team match puts each team's line over its pilots, indented by one. A race borrows that grid for columns of its own, and every table
/// holds at most <see cref="MaxLines"/> lines, header included. Engine-free, drawn by
/// <see cref="ScoresOverlay"/>; decode in docs/org/multiplayer-scoring.md "The in-flight scores".
/// </summary>
public static class OriginalScoresText
{
    /// <summary>The name column's width in characters, <c>FUN_0046e310</c>'s first argument.</summary>
    public const int NameWidth = 21;

    /// <summary>The score column's width in characters, its second argument.</summary>
    public const int ScoreWidth = 7;

    /// <summary>The lines the HUD holds, <c>FUN_004565d0</c>'s rows from 30 to 200 every 10.</summary>
    public const int MaxLines = 18;

    // The remake's columns after the original's, its text in the original's lowercase header style:
    // a Dogfight's kills, and a race's aircraft, best and gap.
    private const int KillsWidth = 6;
    private const int AircraftWidth = 12;
    private const int BestWidth = 10;
    private const int GapWidth = 9;

    /// <summary>A Dogfight's lines in the original's order: by score, a team's line over its pilots.
    /// The <paramref name="name"/> function names a seat. The <paramref name="carried"/> function
    /// says whose flag a seat carries, read only in a team match as the original does.</summary>
    public static IReadOnlyList<OriginalScoresLine> Dogfight(VersusMatch match, Func<int, string> name,
        Func<int, int>? carried, OriginalScoresWords words)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(words);
        var lines = new List<OriginalScoresLine>
        {
            new(Cell(match.Teamed ? words.PlayerTeam : words.Player, NameWidth) + " " + Cell(words.Score, ScoreWidth)
                + " " + Cell("kills", KillsWidth) + " deaths", 0),
        };
        var standings = match.Standings().ToList();
        if (!match.Teamed)
        {
            foreach (var st in standings)
                lines.Add(new(Pilot(name(st.PlayerIndex), st), 0));
            return Capped(lines);
        }

        foreach (var team in match.TeamStandings())
        {
            lines.Add(new(string.Format(CultureInfo.InvariantCulture, "{0} (Team Score: {1})", team.Name, team.Score), 0));
            foreach (var st in standings.Where(s => match.TeamOf(s.PlayerIndex) == team.Team))
                lines.Add(new(Pilot(" " + name(st.PlayerIndex), st), carried?.Invoke(st.PlayerIndex) ?? 0));
        }

        // A seat on no lobby team has no team line to stand under; it follows the teams.
        foreach (var st in standings.Where(s => match.TeamOf(s.PlayerIndex) == 0))
            lines.Add(new(Pilot(name(st.PlayerIndex), st), 0));
        return Capped(lines);
    }

    /// <summary>A race's lines in the original's grid. Place and callsign fill the name column, then
    /// come the aircraft, best, gap and runs in the race boards' words.</summary>
    public static IReadOnlyList<OriginalScoresLine> Race(StuntRace race, OriginalScoresWords words)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(words);
        var lines = new List<OriginalScoresLine>
        {
            new(Cell(words.Player, NameWidth) + " " + Cell("aircraft", AircraftWidth) + " "
                + Cell("best", BestWidth) + " " + Cell("gap", GapWidth) + " runs", 0),
        };
        var standings = race.Standings();
        float? winner = standings.Count > 0 ? standings[0].BestTime : null;
        for (int i = 0; i < standings.Count; i++)
        {
            var r = standings[i];
            lines.Add(new(
                Cell(StuntRace.Ordinal(i + 1) + " " + StuntRace.NameText(r), NameWidth) + " "
                    + Cell(r.PlaneDisplay, AircraftWidth) + " "
                    + Cell(StuntRace.BestText(r, race.ZoneCount), BestWidth) + " "
                    + Cell(StuntRace.GapText(r, winner), GapWidth) + " "
                    + string.Format(CultureInfo.InvariantCulture, "{0}/{1}", r.RunsFinished, r.RunsStarted),
                0));
        }

        return Capped(lines);
    }

    /// <summary>Text padded with spaces and cut to <paramref name="width"/>, the original's
    /// <c>Left</c> of a value formatted with trailing spaces.</summary>
    public static string Cell(string text, int width)
    {
        string padded = (text ?? string.Empty).PadRight(width);
        return padded[..width];
    }

    // One pilot's line: the original's name and score columns, then the remake's kills and deaths.
    private static string Pilot(string name, VersusStanding st) =>
        Cell(name, NameWidth) + " " + Cell(st.Score.ToString(CultureInfo.InvariantCulture), ScoreWidth) + " "
        + Cell(st.Kills.ToString(CultureInfo.InvariantCulture), KillsWidth) + " " + st.Deaths.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<OriginalScoresLine> Capped(List<OriginalScoresLine> lines) =>
        lines.Count > MaxLines ? lines.GetRange(0, MaxLines) : lines;
}
