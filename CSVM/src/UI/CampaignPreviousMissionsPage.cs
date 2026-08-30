using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Mech3;
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
    // SLINE3, the cut Rockets Expended row, is not among them.
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

    // KTEXTW, the SB_KILLTEXT box width.
    private const float StampTextWidth = 15f;

    private const string MissionCompletedText = "Mission Completed"; // langui 1213
    private const string MissionFailedText = "Mission Failed"; // langui 1214
    private const string NotYetFlownText = "Not yet flown"; // langui 1219
    private const string ResultsHeadingText = "Mission Results"; // langui 1202
    private const string RunTimeTitle = "Run Time"; // langui 1203
    private const string GunHitRatioTitle = "Gun Hit Ratio"; // langui 1205
    private const string CashEarnedTitle = "Cash Earned"; // langui 1206
    private const string PlanesDownedTitle = "Overall Planes Downed"; // langui 1207

    // SB_killMARKERcombined.png: 22 frames of 70x100, the eleven airframes then the same eleven
    // starred.
    private static readonly BoardArt KillMarker = new(BoardArtLibrary.Ui, "SB_KILLMARKERCOMBINED.PNG", 22);

    // SB_KILL0..SB_KILL10's top-left, LAYOUT.CSV [@ScrapBook@]. Not in reading order.
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
    /// agrees with whatever the stamps draw.</summary>
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
    /// position. ⚠ The eleven slots are not in reading order: <c>SB_KILL1</c> sits left of
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

    /// <summary>The results card's own placeholder for a page whose mission has no recorded
    /// attempt yet: langui 1219, at the outcome line's own position.</summary>
    public static BoardLine NotYetFlown() =>
        new(NotYetFlownText, TitleX, OutcomeY, 0, RowFont, BoardInk.Heading, Italic: true);

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
                TitleX, OutcomeY, 0, RowFont, BoardInk.Heading, Italic: true),
            new BoardLine(ResultsHeadingText, TitleX, HeadingY, 0, RowFont, BoardInk.Heading, Italic: true),

            new BoardLine(RunTimeTitle, TitleX, TimeY, 0, RowFont, BoardInk.Row, Italic: true),
            new BoardLine($"{minutes:00}:{seconds:00}", ValueX, TimeY, 0, RowFont, BoardInk.Row, Italic: true),

            new BoardLine(GunHitRatioTitle, TitleX, HitsY, 0, RowFont, BoardInk.Row, Italic: true),
            new BoardLine($"{hitRatio}%", ValueX, HitsY, 0, RowFont, BoardInk.Row, Italic: true),

            new BoardLine(CashEarnedTitle, TitleX, CashY, 0, RowFont, BoardInk.Row, Italic: true),
            new BoardLine($"${run.Money}", ValueX, CashY, 0, RowFont, BoardInk.Row, Italic: true),

            new BoardLine(PlanesDownedTitle, TitleX, PlanesY, 0, RowFont, BoardInk.Row, Italic: true),
            new BoardLine(
                PlanesDowned(result, bestToDate).ToString(CultureInfo.InvariantCulture),
                ValueX, PlanesY, 0, RowFont, BoardInk.Row, Italic: true),
        };
    }

    /// <summary>One filled kill-stamp slot: <paramref name="Slot"/> is the <c>SB_KILL</c>/
    /// <c>SB_KILLTEXT</c> ordinal (0-10, not reading order), <paramref name="Frame"/> the strip
    /// frame (the airframe index, or that plus eleven for the starred/ace variant), and
    /// <paramref name="Count"/> the number drawn over it.</summary>
    public readonly record struct KillStamp(int Slot, int Frame, int Count);
}

/// <summary>
/// The scrapbook's table of contents (<c>SCRAPBOOK_TOC.SCRIPT</c>, <c>Campaign CAP-41 Previous
/// Mission 1.png</c>): one 80-pixel row per mission the profile has completed, each an aircraft
/// silhouette beside the mission's short name, the area it was flown over and the plane that flew
/// it, in a four-row window with the listbox's own scrollbar beside it, then VIEW SELECTED, REPLAY
/// MISSION and RETURN TO CABIN. A confirm on a row picks it and a second confirm on the row already
/// picked is REPLAY MISSION's own press. VIEW SELECTED has no screen of its own to open (its whole
/// job is naming which row the two buttons act on), so it is a no-op.
/// </summary>
public sealed class CampaignPreviousMissionsPage : CampaignPage
{
    // SBTOC_L_TOCList, the listbox row of LAYOUT.CSV's [@ScrapBook_TOC@]: X, Y, wrap width, the
    // height of ONE row (not of the widget) and how many of them are on screen at once.
    private const float ListX = 420f;
    private const float ListY = 140f;
    private const float ListWidth = 325f;
    private const float RowHeight = 80f;
    private const int VisibleRows = 4;

    // The row sub-script's own columns: the icon pane sits two pixels in, and its text column
    // starts a further 20 past the pane's width, at the three rows +10, +30 and +50 down the row.
    private const float IconX = ListX + 2f;
    private const float IconWidth = 80f;
    private const float TextX = ListX + IconWidth + 20f;
    private const float FirstLineY = 10f;
    private const float LinePitch = 20f;

    // The row face. Every row is drawn in @globals@gfont3d, whose size is in no layout row, so
    // this is measured off the reference shot; docs/org/campaign-board.md carries the measurement.
    private const float RowFont = 17f;

    // SBTOC_T_CHARACTER and SBTOC_T_MISSIONS: both 300 wide from x 425, justify 2, the first at
    // y 64 and the second at 114. Neither names a face either, so both sizes are measured too.
    private const float HeaderX = 425f;
    private const float HeaderWidth = 300f;
    private const float NameY = 64f;
    private const float NameFont = 14f;
    private const float HeadingY = 114f;
    private const float HeadingFont = 11f;

    // The picked row's wash and its outline, the sub-script's own setpencolor arguments
    // 0x80f2e7b7 and 0xffdd9017. The focused row takes the same outline over the half-strength
    // wash the original draws under a mouse pointer.
    private const byte WashRed = 0xf2;
    private const byte WashGreen = 0xe7;
    private const byte WashBlue = 0xb7;
    private const byte EdgeRed = 0xdd;
    private const byte EdgeGreen = 0x90;
    private const byte EdgeBlue = 0x17;
    private const float PickedWash = 0x80 / 255f;
    private const float FocusWash = 0x40 / 255f;

    // The scrollbar column, measured off Campaign CAP-41 Previous Mission 2.png: the strip stands
    // in the list's own last 16 pixels with an 11-pixel arrow at each end of the window. Its
    // track is the list's KF colour, 0xff282418.
    private const float ScrollX = 730f;
    private const float ScrollWidth = 16f;
    private const float ScrollButton = 11f;
    private const byte TrackRed = 0x28;
    private const byte TrackGreen = 0x24;
    private const byte TrackBlue = 0x18;

    // fc_planeicons.png as the row sub-script mounts it: 12 frames of 80x80, the eleven airframes
    // in id order and then the card fan the not-yet-started career row takes.
    private static readonly BoardArt PlaneIcons = new(BoardArtLibrary.Ui, "FC_PlaneIcons.png", 12);

    // The listbox's <SLIDER>, <UP> and <DOWN>: a 16x11 thumb and two four-frame 16x11 strips.
    private static readonly BoardArt ScrollThumb = new(BoardArtLibrary.Ui, "CM_B_ScrollBar.png");
    private static readonly BoardArt ScrollUp = new(BoardArtLibrary.Ui, "CM_B_ScrollUp.png", 4);
    private static readonly BoardArt ScrollDown = new(BoardArtLibrary.Ui, "CM_B_ScrollDown.png", 4);

    // The row a mission-row press marks as the one REPLAY MISSION and VIEW SELECTED act on. -1
    // until the player has picked one; the two buttons then fall back to the first finished
    // mission, so a press before ever selecting still does something sensible.
    private int _selected = -1;

    // The first row of the four the window shows, kept across visits so paging away from the list
    // and back does not jump it to the top.
    private int _top;

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
    public override int RowCount => Seqs().Count + Buttons().Count;

    /// <summary>The two header widgets, then the three lines of every row the window shows.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            var seqs = Seqs();
            int top = Window(seqs.Count);
            var lines = new List<BoardLine>
            {
                new(Flow.Profile?.Name ?? string.Empty, HeaderX, NameY, HeaderWidth, NameFont,
                    BoardInk.Heading, Justify: BoardJustify.Right),
                new(Flow.Strings.Text(1131, "Previous Missions"), HeaderX, HeadingY, HeaderWidth,
                    HeadingFont, BoardInk.Heading, Justify: BoardJustify.Right),
            };

            for (int i = 0; i < VisibleRows && top + i < seqs.Count; i++)
            {
                float y = ListY + (i * RowHeight) + FirstLineY;
                foreach (string text in RowLines(seqs[top + i]))
                {
                    lines.Add(new BoardLine(text, TextX, y, 0f, RowFont, BoardInk.Row, Italic: true));
                    y += LinePitch;
                }
            }

            return lines;
        }
    }

    /// <summary>Each shown row's aircraft silhouette, then the scrollbar's two arrows and its thumb
    /// once the list is longer than its window.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            var seqs = Seqs();
            int top = Window(seqs.Count);
            var pictures = new List<BoardPicture>();
            for (int i = 0; i < VisibleRows && top + i < seqs.Count; i++)
            {
                pictures.Add(new BoardPicture(
                    PlaneIcons, IconX, ListY + (i * RowHeight), Airframe(seqs[top + i])));
            }

            if (seqs.Count > VisibleRows)
            {
                var (thumbY, thumbHeight) = Thumb(seqs.Count, top);
                pictures.Add(new BoardPicture(ScrollUp, ScrollX, ListY, Frame: 1));
                pictures.Add(new BoardPicture(
                    ScrollDown, ScrollX, ListY + WindowHeight - ScrollButton, Frame: 1));
                pictures.Add(new BoardPicture(
                    ScrollThumb, ScrollX, thumbY, Width: ScrollWidth, Height: thumbHeight));
            }

            return pictures;
        }
    }

    /// <summary>The picked row's wash and outline, the focused row's fainter pair, and the
    /// scrollbar's track behind its thumb.</summary>
    public override IReadOnlyList<BoardFill> Fills
    {
        get
        {
            var seqs = Seqs();
            int top = Window(seqs.Count);
            var fills = new List<BoardFill>();
            for (int i = 0; i < VisibleRows && top + i < seqs.Count; i++)
            {
                if (Wash(top + i) is not { } wash)
                {
                    continue;
                }

                float y = ListY + (i * RowHeight);
                fills.Add(new BoardFill(
                    ListX, y, ListWidth, RowHeight, WashRed, WashGreen, WashBlue, wash));
                fills.Add(new BoardFill(
                    ListX, y, ListWidth, RowHeight, EdgeRed, EdgeGreen, EdgeBlue, Border: true));
            }

            if (seqs.Count > VisibleRows)
            {
                fills.Add(new BoardFill(
                    ScrollX, ListY + ScrollButton, ScrollWidth, WindowHeight - (2f * ScrollButton),
                    TrackRed, TrackGreen, TrackBlue));
            }

            return fills;
        }
    }

    // How tall the four-row window is, which is where the scrollbar's lower arrow sits.
    private static float WindowHeight => VisibleRows * RowHeight;

    /// <summary>A mission row draws no list text of its own: its three lines already stand at their
    /// authored positions inside the row.</summary>
    public override string RowText(int row) => ButtonAt(row) switch
    {
        BoardButton.ViewMission => "VIEW SELECTED",
        BoardButton.ReplayMission => "REPLAY MISSION",
        BoardButton.CurrentMission => Flow.Strings.Text(1200, "Current Mission"),
        BoardButton.ReturnToCabin => "RETURN TO CABIN",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) =>
        ButtonAt(row) is var button && button != BoardButton.None
            ? new BoardButtonRef(button)
            : BoardButtonRef.None;

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            return row == _selected
                ? "Selected. Confirm again to replay it"
                : Flow.Strings.Text(3450 + seqs[row], $"Mission {seqs[row] + 1}");
        }

        return ButtonAt(row) switch
        {
            BoardButton.ViewMission => "Opens the scrapbook at this mission",
            BoardButton.ReplayMission when SelectedSeq(seqs) is { } seq =>
                Flow.Strings.Text(3450 + seq, $"Mission {seq + 1}"),
            BoardButton.CurrentMission => "Opens the scrapbook at the current mission",
            _ => "Back to the cabin",
        };
    }

    /// <summary>A mission row's first confirm picks it and its second replays it, which is the
    /// original's own double-click on a row folded onto a pad's single button. VIEW SELECTED and
    /// the bookmark both open the book (<c>uiData</c> 2405 mode 1) on the picked mission and on
    /// the campaign's own current one.</summary>
    public override bool Accept(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            if (row == _selected)
            {
                return Replay(seqs[row]);
            }

            _selected = row;
            return true;
        }

        switch (ButtonAt(row))
        {
            case BoardButton.ViewMission:
                if (SelectedSeq(seqs) is { } viewing)
                {
                    Flow.OpenScrapbook(viewing);
                }

                return true;
            case BoardButton.ReplayMission:
                return SelectedSeq(seqs) is { } replaying && Replay(replaying);
            case BoardButton.CurrentMission:
                Flow.OpenScrapbook(CurrentSeq());
                return true;
            default:
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
        }
    }

    // The finished seqs, in story order, per CampaignProgression.CompletedSeqs. Read fresh every
    // call rather than cached: a replay recorded through the briefing/flight-check screens must
    // show up here the next time this page draws.
    private List<int> Seqs() =>
        Flow.Profile is { } profile ? CampaignProgression.CompletedSeqs(profile) : new List<int>();

    // The buttons under the list, in the order they take rows. REPLAY MISSION is offered only
    // where uiData 2411 offers it, on a picked mission whose record holds a time; the other three
    // are created active and stay so.
    private List<BoardButton> Buttons()
    {
        var buttons = new List<BoardButton> { BoardButton.ViewMission };
        if (SelectedSeq(Seqs()) is { } seq && Flow.Profile is { } profile
            && CampaignProgression.ResultOf(profile, seq) is { } result
            && (result.Latest.TimeMs != 0 || result.Best.TimeMs != 0))
        {
            buttons.Add(BoardButton.ReplayMission);
        }

        buttons.Add(BoardButton.CurrentMission);
        buttons.Add(BoardButton.ReturnToCabin);
        return buttons;
    }

    private BoardButton ButtonAt(int row)
    {
        var buttons = Buttons();
        int offset = row - Seqs().Count;
        return offset >= 0 && offset < buttons.Count ? buttons[offset] : BoardButton.None;
    }

    // The mission the campaign is on, which the bookmark opens the book at: the next unflown one,
    // or the last of the twenty-four once the campaign is finished.
    private int CurrentSeq()
    {
        if (Flow.Profile is not { } profile)
        {
            return 0;
        }

        return Math.Clamp(
            CampaignProgression.NextMissionSeq(profile), 0, CampaignSequence.MissionCount - 1);
    }

    // How strongly a row's wash draws, or null for a row that is neither picked nor focused.
    private float? Wash(int row) =>
        row == _selected ? PickedWash : row == Flow.Row ? FocusWash : null;

    private bool Replay(int seq)
    {
        Flow.SetMission(seq);
        Flow.GoTo(CampaignScreen.Briefing);
        return true;
    }

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

    // The first row of the shown window, moved only as far as it must to keep the cursor's own row
    // on screen. A cursor parked on one of the three buttons leaves it where the list last stood.
    private int Window(int count)
    {
        int last = Math.Max(0, count - VisibleRows);
        int top = Math.Clamp(_top, 0, last);
        if (Flow.Row < count)
        {
            top = Math.Clamp(Math.Clamp(top, Flow.Row - VisibleRows + 1, Flow.Row), 0, last);
        }

        _top = top;
        return top;
    }

    // The thumb's run down the track: as tall a fraction of it as the window is of the list, never
    // shorter than an arrow, and stepped so the last row scrolled to lands it flush at the bottom.
    private (float Y, float Height) Thumb(int count, int top)
    {
        float track = WindowHeight - (2f * ScrollButton);
        float height = Math.Max(ScrollButton, track * VisibleRows / count);
        int last = Math.Max(1, count - VisibleRows);
        return (ListY + ScrollButton + ((track - height) * top / last), height);
    }

    // The three lines uiData 2409 hands the row sub-script: the mission's short name, the area of
    // the chapter it belongs to, and the plane whose best-of run stands in the record.
    private string[] RowLines(int seq) => new[]
    {
        Flow.Strings.Text(3480 + seq, $"Mission {seq + 1}"),
        Flow.Strings.Text(1220 + (seq / 5), string.Empty),
        Run(seq)?.PlaneName ?? string.Empty,
    };

    // The icon strip's frame for a row: the airframe its best-of run flew, clamped inside the
    // eleven the strip carries before the card fan.
    private int Airframe(int seq) =>
        Math.Clamp(Run(seq)?.Airframe ?? 0, 0, CampaignProgression.AirframeCount - 1);

    private MissionRun? Run(int seq) =>
        Flow.Profile is { } profile && CampaignProgression.ResultOf(profile, seq) is { } result
            ? result.Best
            : null;
}

