using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// The stunt run's splits section, shared by <see cref="StuntScoreboard"/> and
/// <see cref="IaWrapupBoard"/>. It draws the zone table in the order flown, the total, and the
/// ★ NEW BEST / BEST comparison line. A zone row carries its name, split and cumulative time, and a
/// zone never reached gets dash rows. The same table as flat text is
/// <see cref="StuntSummary.Lines"/>.
/// </summary>
public static class StuntSplits
{
    // Section sizes, chrome type scale rungs at the boards' 720p reference (scaled by the board's
    // own scale).
    private static readonly float HeaderFont = ChromeType.InReference(ChromeSize.Note, ResultsBoard.ReferenceHeight);
    private static readonly float RowFont = ChromeType.InReference(ChromeSize.Text, ResultsBoard.ReferenceHeight);
    private static readonly float TotalFont = ChromeType.InReference(ChromeSize.Lead, ResultsBoard.ReferenceHeight);
    private static readonly float BestFont = ChromeType.InReference(ChromeSize.Caption, ResultsBoard.ReferenceHeight);

    private static readonly Color TotalColor = new(0.96f, 0.98f, 1f);
    private static readonly Color BestColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color NewBestColor = new(1f, 0.82f, 0.28f);

    /// <summary>Appends the section to <paramref name="body"/> at scale <paramref name="s"/>.
    /// <paramref name="separatorBeforeTotal"/> keeps the two boards' shipped layouts: the
    /// scoreboard rules off its total, the wrap-up board runs the table straight into it.</summary>
    public static void Add(VBoxContainer body, StuntSummary run, float s, bool separatorBeforeTotal)
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(5f * s));
        body.AddChild(grid);

        int nameW = (int)(280f * s), timeW = (int)(96f * s);
        int header = (int)(HeaderFont * s), font = (int)(RowFont * s);
        ResultsBoard.AddCell(grid, "ZONE", header, ResultsBoard.HeaderColor, HorizontalAlignment.Left, nameW);
        ResultsBoard.AddCell(grid, "SPLIT", header, ResultsBoard.HeaderColor, HorizontalAlignment.Right, timeW);
        ResultsBoard.AddCell(grid, "TIME", header, ResultsBoard.HeaderColor, HorizontalAlignment.Right, timeW);

        float prev = 0f;
        int n = 1;
        foreach (var z in run.Mission.InCompletionOrder())
        {
            string name = $"{n}.  " + (z.Description.Length > 0 ? z.Description
                : z.MarkerText().Length > 0 ? z.MarkerText() : z.DzName);
            ResultsBoard.AddCell(grid, name, font, ResultsBoard.RowColor, HorizontalAlignment.Left, nameW);
            if (z.Completed)
            {
                ResultsBoard.AddCell(grid, StuntMission.FormatTime(z.CompletedAt - prev), font,
                    ResultsBoard.RowColor, HorizontalAlignment.Right, timeW);
                ResultsBoard.AddCell(grid, StuntMission.FormatTime(z.CompletedAt), font,
                    ResultsBoard.RowColor, HorizontalAlignment.Right, timeW);
                prev = z.CompletedAt;
            }
            else
            {
                ResultsBoard.AddCell(grid, "-", font, ResultsBoard.HeaderColor, HorizontalAlignment.Right, timeW);
                ResultsBoard.AddCell(grid, "-", font, ResultsBoard.HeaderColor, HorizontalAlignment.Right, timeW);
            }
            n++;
        }

        if (separatorBeforeTotal)
            body.AddChild(ResultsBoard.Separator(s));
        body.AddChild(ResultsBoard.Centered(ResultsBoard.Label(
            $"TOTAL   {StuntMission.FormatTime(run.Total)}", (int)(TotalFont * s), TotalColor)));

        if (run.NewBest)
        {
            string best = run.PrevBest is { } was
                ? $"★  NEW BEST   (was {StuntMission.FormatTime(was)})"
                : "★  NEW BEST";
            body.AddChild(ResultsBoard.Centered(ResultsBoard.Label(best, (int)(BestFont * s), NewBestColor)));
        }
        else if (run.PrevBest is { } stored)
        {
            body.AddChild(ResultsBoard.Centered(ResultsBoard.Label(
                $"BEST   {StuntMission.FormatTime(stored)}", (int)(BestFont * s), BestColor)));
        }
    }
}
