using System.Collections.Generic;
using System.Globalization;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The book's results page (scrapbook spread 1): the outcome line, the four rows the original
/// draws and the two tabs, computed from one mission's record
/// (<c>docs/org/debrief.md#the-screen-is-the-scrapbook</c>). Row titles and the outcome text are
/// literal strings rather than read off <c>ui_strings.json</c> at runtime, following
/// <see cref="Flight.IaWrapupBoard"/>'s own precedent. Positions are <c>LAYOUT.CSV</c>'s
/// <c>[@ScrapBook@]</c> <c>SB_T_*</c> rows. Wired into <see cref="CampaignFlow"/> as
/// <see cref="CampaignScreen.Scrapbook"/>'s <see cref="CampaignScrapbookPage"/>, which also draws
/// spread 1's shipped scraps alongside this class's rows and stamps
/// (<see cref="ScrapbookComposition"/>).
/// </summary>
public static class CampaignScrapbookResults
{
    // LAYOUT.CSV [@ScrapBook@]: STATTITLEX/STATX are the title and value columns; SLINE0/SLINE1
    // are the outcome and heading rows; SLINE2/SLINE4/SLINE5/SLINE6 are the four drawn rows.
    // SLINE3, the cut Rockets Expended row, is not among them (A1).
    private const float TitleX = 417f;
    private const float ValueX = 642f;
    private const float OutcomeY = 339f;
    private const float HeadingY = 369f;
    private const float TimeY = 412f;
    private const float HitsY = 436f;
    private const float CashY = 460f;
    private const float PlanesY = 484f;

    // AB14I, the row font every SB_T_* widget in [@ScrapBook@] carries.
    private const float RowFont = 14f;

    // KTEXTW, the SB_KILLTEXT box width (C16).
    private const float StampTextWidth = 15f;

    private const string MissionCompletedText = "Mission Completed"; // langui 1213
    private const string MissionFailedText = "Mission Failed"; // langui 1214
    private const string ResultsHeadingText = "Mission Results"; // langui 1202
    private const string RunTimeTitle = "Run Time"; // langui 1203
    private const string GunHitRatioTitle = "Gun Hit Ratio"; // langui 1205
    private const string CashEarnedTitle = "Cash Earned"; // langui 1206
    private const string PlanesDownedTitle = "Overall Planes Downed"; // langui 1207

    // SB_killMARKERcombined.png: 22 frames of 70x100, the eleven airframes then the same eleven
    // starred (C16).
    private static readonly BoardArt KillMarker = new(BoardArtLibrary.Ui, "SB_KILLMARKERCOMBINED.PNG", 22);

    // SB_KILL0..SB_KILL10's top-left, LAYOUT.CSV [@ScrapBook@]. Not in reading order (C16).
    private static readonly (float X, float Y)[] StampSlots =
    {
        (560f, 109f), (467f, 93f), (604f, 173f), (604f, 50f), (520f, 42f),
        (679f, 153f), (540f, 195f), (682f, 46f), (476f, 184f), (416f, 154f), (417f, 43f),
    };

    // SB_KILLTEXT0..SB_KILLTEXT10, the count drawn over each stamp.
    private static readonly (float X, float Y)[] StampTextSlots =
    {
        (588f, 128f), (494f, 114f), (632f, 195f), (633f, 72f), (549f, 64f),
        (707f, 176f), (568f, 218f), (709f, 69f), (504f, 207f), (445f, 177f), (444f, 66f),
    };

    /// <summary>Best to Date (langui 1159), the merged half at <c>+0x54</c>, or Most Recent
    /// (langui 1160), the attempt half at <c>+0x00</c> (<c>docs/formats/saved-games.md</c>, "The
    /// mission-result array").</summary>
    public static string TabTitle(bool bestToDate) => bestToDate ? "Best to Date" : "Most Recent";

    /// <summary>Whether the outcome line reads Mission Completed. ⚠ The original's
    /// <c>0x0040a7e6</c> does not read the selected half's completed-objective mask for the Best
    /// to Date tab: it reads <c>+0x24</c> inside the half instead, a slot the completion merge
    /// (<c>FUN_00405ce0</c>) never writes, so it always reads 0. The Best to Date tab therefore
    /// always shows Mission Failed in the shipped game, whatever the merged record's own mask
    /// says; only Most Recent reads the real mask. Reproduced here rather than "fixed".</summary>
    public static bool Won(MissionResult result, bool bestToDate) =>
        !bestToDate && (result.Latest.CompletedMask & CampaignProgression.PrimaryObjectiveMask) != 0;

    /// <summary>The sum of both per-airframe kill arrays over their eleven slots, truncated to
    /// sixteen bits the way <c>0x0040a8df</c> does. There is no stored total field, so this always
    /// agrees with whatever the stamps (C16) draw.</summary>
    public static int PlanesDowned(MissionResult result, bool bestToDate)
    {
        var run = bestToDate ? result.Best : result.Latest;
        int total = 0;
        for (int i = 0; i < CampaignProgression.AirframeCount; i++)
        {
            total += run.Kills[i] + run.AceKills[i];
        }

        return total & 0xffff;
    }

    /// <summary>The filled kill-stamp slots, densely from slot 0: the plain tally's airframes in
    /// ascending index order, skipping zeros, then the ace tally's the same way, stopping at
    /// eleven (<c>docs/org/debrief.md#the-stamps-and-the-total</c>). The same airframe can fill
    /// two slots, once plain and once starred.</summary>
    public static IReadOnlyList<KillStamp> Stamps(MissionResult result, bool bestToDate)
    {
        var run = bestToDate ? result.Best : result.Latest;
        var stamps = new List<KillStamp>();
        for (int i = 0; i < CampaignProgression.AirframeCount && stamps.Count < CampaignProgression.AirframeCount; i++)
        {
            if (run.Kills[i] > 0)
            {
                stamps.Add(new KillStamp(stamps.Count, i, run.Kills[i]));
            }
        }

        for (int i = 0; i < CampaignProgression.AirframeCount && stamps.Count < CampaignProgression.AirframeCount; i++)
        {
            if (run.AceKills[i] > 0)
            {
                stamps.Add(new KillStamp(stamps.Count, i + CampaignProgression.AirframeCount, run.AceKills[i]));
            }
        }

        return stamps;
    }

    /// <summary>The stamp art, one <c>BoardPicture</c> per filled slot at its <c>SB_KILL</c>
    /// position. ⚠ The eleven slots are not in reading order (C16): <c>SB_KILL1</c> sits left of
    /// and above <c>SB_KILL0</c>.</summary>
    public static IReadOnlyList<BoardPicture> StampPictures(MissionResult result, bool bestToDate)
    {
        var pictures = new List<BoardPicture>();
        foreach (var stamp in Stamps(result, bestToDate))
        {
            var (x, y) = StampSlots[stamp.Slot];
            pictures.Add(new BoardPicture(KillMarker, x, y, stamp.Frame));
        }

        return pictures;
    }

    /// <summary>The kill count drawn over each filled stamp, at its <c>SB_KILLTEXT</c>
    /// position.</summary>
    public static IReadOnlyList<BoardLine> StampLabels(MissionResult result, bool bestToDate)
    {
        var lines = new List<BoardLine>();
        foreach (var stamp in Stamps(result, bestToDate))
        {
            var (x, y) = StampTextSlots[stamp.Slot];
            lines.Add(new BoardLine(
                stamp.Count.ToString(CultureInfo.InvariantCulture), x, y, StampTextWidth, RowFont, BoardInk.Row));
        }

        return lines;
    }

    /// <summary>The outcome line, the heading and the four drawn rows (title then value), at
    /// their <c>LAYOUT.CSV</c> positions. Time is <c>mm:ss</c> off milliseconds truncated the way
    /// <c>FUN_00419630</c> writes it; the hit ratio is hits over shots as a percentage, truncated
    /// toward zero the way the screen's own <c>ftol</c> call does, not rounded.</summary>
    public static IReadOnlyList<BoardLine> Rows(MissionResult result, bool bestToDate)
    {
        var run = bestToDate ? result.Best : result.Latest;
        int totalSeconds = run.TimeMs / 1000;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int hitRatio = run.Shots <= 0 ? 0 : run.Hits * 100 / run.Shots;

        return new[]
        {
            new BoardLine(
                Won(result, bestToDate) ? MissionCompletedText : MissionFailedText,
                TitleX, OutcomeY, 0, RowFont, BoardInk.Heading),
            new BoardLine(ResultsHeadingText, TitleX, HeadingY, 0, RowFont, BoardInk.Heading),

            new BoardLine(RunTimeTitle, TitleX, TimeY, 0, RowFont, BoardInk.Row),
            new BoardLine($"{minutes:00}:{seconds:00}", ValueX, TimeY, 0, RowFont, BoardInk.Row),

            new BoardLine(GunHitRatioTitle, TitleX, HitsY, 0, RowFont, BoardInk.Row),
            new BoardLine($"{hitRatio}%", ValueX, HitsY, 0, RowFont, BoardInk.Row),

            new BoardLine(CashEarnedTitle, TitleX, CashY, 0, RowFont, BoardInk.Row),
            new BoardLine($"${run.Money}", ValueX, CashY, 0, RowFont, BoardInk.Row),

            new BoardLine(PlanesDownedTitle, TitleX, PlanesY, 0, RowFont, BoardInk.Row),
            new BoardLine(
                PlanesDowned(result, bestToDate).ToString(CultureInfo.InvariantCulture),
                ValueX, PlanesY, 0, RowFont, BoardInk.Row),
        };
    }

    /// <summary>One filled kill-stamp slot: <paramref name="Slot"/> is the <c>SB_KILL</c>/
    /// <c>SB_KILLTEXT</c> ordinal (0-10, not reading order), <paramref name="Frame"/> the strip
    /// frame (the airframe index, or that plus eleven for the starred/ace variant), and
    /// <paramref name="Count"/> the number drawn over it.</summary>
    public readonly record struct KillStamp(int Slot, int Frame, int Count);
}

/// <summary>
/// The scrapbook's finished-missions list (<c>Campaign CAP-41 Previous Mission *.png</c>): one row
/// per mission the profile has completed, ordered by <c>seq</c>, then VIEW SELECTED, REPLAY
/// MISSION and RETURN TO CABIN. The shots show a row as the long mission name over its area and
/// the plane flown; VIEW SELECTED has no screen of its own to open (its whole job is naming which
/// row REPLAY MISSION acts on), so it is a no-op that leaves the detail line already showing.
/// </summary>
public sealed class CampaignPreviousMissionsPage : CampaignPage
{
    // The row a mission-row press marks as the one REPLAY MISSION and VIEW SELECTED act on. -1
    // until the player has picked one; the two buttons then fall back to the first finished
    // mission, so a press before ever selecting still does something sensible.
    private int _selected = -1;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignPreviousMissionsPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.PreviousMissions;

    /// <inheritdoc/>
    public override string Title => "PREVIOUS MISSIONS";

    /// <inheritdoc/>
    public override int RowCount => Seqs().Count + 3;

    /// <summary>The album page's own two text widgets: whose scrapbook it is, then the heading over
    /// the mission list.</summary>
    public override IReadOnlyList<BoardLine> Captions => new[]
    {
        new BoardLine(Flow.Profile?.Name ?? string.Empty, 425, 64, 300, 18, BoardInk.Heading),
        new BoardLine("PREVIOUS MISSIONS", 425, 114, 300, 16, BoardInk.Heading),
    };

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            string name = Flow.Strings.Text(3450 + seqs[row], $"Mission {seqs[row] + 1}");
            return row == _selected ? "✓ " + name : name;
        }

        return (row - seqs.Count) switch
        {
            0 => "VIEW SELECTED",
            1 => "REPLAY MISSION",
            _ => "RETURN TO CABIN",
        };
    }

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row)
    {
        int offset = row - Seqs().Count;
        return offset switch
        {
            0 => new BoardButtonRef(BoardButton.ViewMission),
            1 => new BoardButtonRef(BoardButton.ReplayMission),
            2 => new BoardButtonRef(BoardButton.ReturnToCabin),
            _ => BoardButtonRef.None,
        };
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            return MissionLine(seqs[row]);
        }

        return (row - seqs.Count) switch
        {
            0 or 1 when SelectedSeq(seqs) is { } seq => MissionLine(seq),
            0 or 1 => "Pick a mission first",
            _ => "Back to the cabin",
        };
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            _selected = row;
            return true;
        }

        switch (row - seqs.Count)
        {
            case 0: // VIEW SELECTED: the detail line above already says everything it would.
                return true;
            case 1: // REPLAY MISSION
                if (SelectedSeq(seqs) is not { } seq)
                {
                    Flow.SetMessage("Select a mission first.");
                    return true;
                }

                Flow.SetMission(seq);
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            default: // RETURN TO CABIN
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
        }
    }

    // The finished seqs, in story order, per CampaignProgression.CompletedSeqs. Read fresh every
    // call rather than cached: a replay recorded through the briefing/flight-check screens must
    // show up here the next time this page draws.
    private List<int> Seqs() =>
        Flow.Profile is { } profile ? CampaignProgression.CompletedSeqs(profile) : new List<int>();

    // The row the two buttons act on: the player's own pick, or the first finished mission when
    // nothing has been picked yet.
    private int? SelectedSeq(List<int> seqs)
    {
        if (seqs.Count == 0)
        {
            return null;
        }

        int index = _selected >= 0 && _selected < seqs.Count ? _selected : 0;
        return seqs[index];
    }

    // Long name over area and the plane that flew the best-of record, the CAP-41 layout.
    private string MissionLine(int seq)
    {
        string area = Flow.Strings.Text(1220 + (seq / 5), "");
        string plane = Flow.Profile is { } profile && CampaignProgression.ResultOf(profile, seq) is { } result
            ? result.Best.PlaneName
            : "";
        return string.IsNullOrEmpty(area) && string.IsNullOrEmpty(plane)
            ? string.Empty
            : $"{area}   ·   {plane}";
    }
}
