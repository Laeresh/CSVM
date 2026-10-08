using System.Collections.Generic;

namespace CSVM.Flight.Modes;

/// <summary>One stunt run's numbers for a split table: the run itself, its total and how it
/// compares to the stored best. Read at run end, when the run is over and the clock is
/// halted.</summary>
public readonly record struct StuntSummary(StuntMission Mission, float Total, float? PrevBest, bool NewBest)
{
    /// <summary>The word that opens the total's line in <see cref="Lines"/>, so a page that already
    /// shows the mission clock can leave that line out.</summary>
    public const string TotalLabel = "TOTAL";

    /// <summary>The split table as flat text, one line per zone in the order flown and then the
    /// total and the best comparison. What a run hands a menu page: the summary holds a live
    /// mission object, which cannot outlive the session, and these strings can.</summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>();
        float prev = 0f;
        int n = 1;
        foreach (var z in Mission.InCompletionOrder())
        {
            string name = z.Description.Length > 0 ? z.Description
                : z.MarkerText().Length > 0 ? z.MarkerText() : z.DzName;
            lines.Add(z.Completed
                ? $"{n}.  {name}   {StuntMission.FormatTime(z.CompletedAt - prev)}   {StuntMission.FormatTime(z.CompletedAt)}"
                : $"{n}.  {name}   -   -");
            if (z.Completed)
            {
                prev = z.CompletedAt;
            }

            n++;
        }

        lines.Add($"{TotalLabel}   {StuntMission.FormatTime(Total)}");
        if (NewBest)
        {
            lines.Add(PrevBest is { } was
                ? $"NEW BEST   (was {StuntMission.FormatTime(was)})"
                : "NEW BEST");
        }
        else if (PrevBest is { } stored)
        {
            lines.Add($"BEST   {StuntMission.FormatTime(stored)}");
        }

        return lines;
    }
}
