using System.Globalization;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// The shared results board for splitscreen "Dogfight", <see cref="ResultsBoard"/>'s shell: the
/// match ends for everybody at once, so this covers the WHOLE window on its own CanvasLayer over
/// the splitscreen panes, not a per-pane overlay. Winner (or "DRAW" on a tie) on top, then one
/// ranked row per player, tag, score, kills, deaths, in their own identity colour; the winner's row is
/// highlighted the same way the race board highlights first place. R and pad Y still reach the
/// rematch directly, for the muscle memory and for the hold-test harness.
/// </summary>
public sealed partial class VersusBoard : ResultsBoard
{
    /// <summary>The line a network guest's board draws in place of its Restart row
    /// (<see cref="ResultsBoard.RestartWithheld"/>): the host restarts the round for every machine.</summary>
    public const string HostCallsTheRematch = "The host calls the rematch";

    /// <summary>The tag after a computer pilot's callsign on its row, the same word the lobby and
    /// Game Scores tag a bot with. The remake's own: the original has no bots.</summary>
    public const string BotTag = "BOT";

    // The board's sizes, chrome type scale rungs at the boards' 720p reference, scaled by
    // window height.
    private static readonly float TitleFont = ChromeType.InReference(ChromeSize.Heading, ReferenceHeight);
    private static readonly float ContextFont = ChromeType.InReference(ChromeSize.Caption, ReferenceHeight);
    private static readonly float HeaderFont = ChromeType.InReference(ChromeSize.Note, ReferenceHeight);
    private static readonly float RowFont = ChromeType.InReference(ChromeSize.Body, ReferenceHeight);

    private VersusMatch _match = null!;
    private string _context = "";
    private System.Func<int, string?>? _botName;

    // A rematch clears Completed, which the shell's _Process turns into the hide and the release.
    protected override bool StillEnded => _match.Completed;

    /// <summary>Builds the (hidden) board and subscribes to the match's completion. Add it to a
    /// CanvasLayer above the splitscreen panes. It wakes itself on
    /// <see cref="VersusMatch.MatchCompleted"/> and retires on a rematch. <paramref name="botName"/>
    /// gives a computer pilot's callsign by <c>PlayerIndex</c>, null for a person's seat.</summary>
    public static VersusBoard Build(VersusMatch match, string context, bool exitsToMenu,
        PauseState state, System.Func<int, MenuInput> inputFor, System.Func<int, string?>? botName = null)
    {
        var board = new VersusBoard { _match = match, _context = context, _botName = botName };
        board.InitShell(state, exitsToMenu, inputFor);
        match.MatchCompleted += board.OnMatchCompleted;
        return board;
    }

    /// <summary>What a row's tag cell reads. A person keeps the seat's player tag. A bot reads its
    /// callsign from <paramref name="botName"/> and then <see cref="BotTag"/>.</summary>
    public static string RowTag(int playerIndex, System.Func<int, string?>? botName) =>
        botName?.Invoke(playerIndex) is { } callsign ? $"{callsign} {BotTag}" : SplitScreen.PlayerTag(playerIndex);

    /// <summary>The board's headline for a finished match: the one leader's name and WINS, or DRAW
    /// on a tie at the top. A team match names the leading team by its lobby name. A match won on its
    /// objective names the team that won it, whatever the totals. A bot leader is named by its
    /// callsign from <paramref name="botName"/>, a person by player tag.</summary>
    public static string Title(VersusMatch match, System.Func<int, string?>? botName = null)
    {
        if (match.ObjectiveWinner > 0)
        {
            return $"{match.TeamName(match.ObjectiveWinner).ToUpperInvariant()} WINS";
        }

        if (match.Teamed)
        {
            var teams = match.TeamStandings().Where(t => t.Rank == 1).ToList();
            return teams.Count == 1 ? $"{teams[0].Name.ToUpperInvariant()} WINS" : "DRAW";
        }

        var winners = match.Standings().Where(st => st.Rank == 1).ToList();
        if (winners.Count != 1)
        {
            return "DRAW";
        }

        int seat = winners[0].PlayerIndex;
        string name = botName?.Invoke(seat) is { } callsign ? callsign.ToUpperInvariant() : SplitScreen.PlayerTag(seat);
        return $"{name} WINS";
    }

    public override void _ExitTree() => _match.MatchCompleted -= OnMatchCompleted;

    private void OnMatchCompleted()
    {
        // Log the final standings too, so a match is reviewable from a headless run's log.
        Log.Info("flight", $"dogfight results: {Title(_match, _botName)}");
        foreach (var team in _match.TeamStandings())
            Log.Info("flight", $"  #{team.Rank}  {team.Name}  {team.Score} pts  {team.Kills}K/{team.Deaths}D");
        foreach (var st in _match.Standings())
            Log.Info("flight",
                $"  #{st.Rank}  {RowTag(st.PlayerIndex, _botName)}  {st.Score} pts  {st.Kills}K/{st.Deaths}D");
        Populate();
        Wake();
    }

    private void Populate()
    {
        float s = BoardScale();
        var body = BeginPanel(s);

        // Captured once here so a later Restart() zeroing the live match never rebuilds these
        // already-drawn labels.
        var standings = _match.Standings().ToList();
        var teams = _match.TeamStandings().ToList();
        var winners = standings.Where(st => st.Rank == 1).ToList();
        string title = Title(_match, _botName);
        var titleColor = !_match.Teamed && winners.Count == 1 ? SplitScreen.PlayerColor(winners[0].PlayerIndex) : TitleColor;

        body.AddChild(Centered(Label(title, (int)(TitleFont * s), titleColor)));
        body.AddChild(Centered(Label(_context, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));
        if (teams.Count > 0)
        {
            AddTeams(body, teams, s);
            body.AddChild(Separator(s));
        }

        // One row per player: placing | tag | score | kills | deaths. Score is the ranked number
        // and kills alone do not explain it, a death with no killer costs a point.
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);

        int rankW = (int)(52f * s), tagW = (int)(56f * s), scoreW = (int)(80f * s);
        int killsW = (int)(80f * s), deathsW = (int)(80f * s);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, rankW);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, tagW);
        AddCell(grid, "SCORE", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, scoreW);
        AddCell(grid, "KILLS", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, killsW);
        AddCell(grid, "DEATHS", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, deathsW);

        foreach (var st in standings)
        {
            bool won = !_match.Teamed && st.Rank == 1 && winners.Count == 1;
            // The winner's row wears their own identity colour; everyone else stays neutral so
            // the placing reads at a glance, same rule StuntRaceBoard's winner row follows.
            var color = won ? SplitScreen.PlayerColor(st.PlayerIndex) : RowColor;
            int font = (int)(RowFont * s);
            AddCell(grid, $"#{st.Rank}", font, color, HorizontalAlignment.Left, rankW);
            AddTag(grid, st.PlayerIndex, font, (int)(HeaderFont * s), tagW);
            AddCell(grid, st.Score.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, scoreW);
            AddCell(grid, st.Kills.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, killsW);
            AddCell(grid, st.Deaths.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, deathsW);
        }

        body.AddChild(Separator(s));
        AddStandardMenu(body, s);
    }

    // A row's tag cell in the seat's colour: a person's player tag, or a bot's callsign. The bot
    // tag follows a callsign in the headers' smaller ink, so it reads as a note on the name.
    private void AddTag(GridContainer grid, int seat, int font, int noteFont, int minWidth)
    {
        if (_botName?.Invoke(seat) is not { } callsign)
        {
            AddCell(grid, SplitScreen.PlayerTag(seat), font, SplitScreen.PlayerColor(seat), HorizontalAlignment.Left, minWidth);
            return;
        }

        var cell = new HBoxContainer { CustomMinimumSize = new Vector2(minWidth, 0) };
        cell.AddThemeConstantOverride("separation", Mathf.RoundToInt(font * 0.4f));
        cell.AddChild(Label(callsign, font, SplitScreen.PlayerColor(seat)));
        var tag = Label(BotTag, noteFont, HeaderColor);
        tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        cell.AddChild(tag);
        grid.AddChild(cell);
    }

    // A team match's own rows over the pilots': placing | team name | score | kills | deaths. Each
    // row is the total its members' lines add up to.
    private void AddTeams(Control body, System.Collections.Generic.List<VersusTeamStanding> teams, float s)
    {
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);
        int rankW = (int)(52f * s), nameW = (int)(180f * s), numberW = (int)(80f * s);
        int font = (int)(RowFont * s);
        bool sole = teams.Count(t => t.Rank == 1) == 1;
        foreach (var team in teams)
        {
            var color = sole && team.Rank == 1 ? TitleColor : RowColor;
            AddCell(grid, $"#{team.Rank}", font, color, HorizontalAlignment.Left, rankW);
            AddCell(grid, team.Name, font, color, HorizontalAlignment.Left, nameW);
            AddCell(grid, team.Score.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, numberW);
            AddCell(grid, team.Kills.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, numberW);
            AddCell(grid, team.Deaths.ToString(CultureInfo.InvariantCulture), font, color, HorizontalAlignment.Right, numberW);
        }
    }
}
