using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The scrapbook itself (<c>SCRAPBOOK.SCRIPT</c>), opened on the mission a finished mission just
/// flew: the page title, the shown spread's shipped scraps, and on spread 1 the results card with
/// its Best to Date / Most Recent tabs, the results block and the kill stamps. Every openable scrap
/// (<see cref="ScrapbookScrap.Opens"/>) is a row opening <see cref="CampaignScreen.ScrapbookZoom"/>.
/// The page and mission arrows and the Current Mission bookmark browse the rest of the book, VIEW
/// ALL MISSIONS jumps to the mission overview, and REPLAY MISSION acts on whichever mission is
/// browsed, offered only where the original offers it.
/// </summary>
public sealed class CampaignScrapbookPage : CampaignPage
{
    // SB_T_NAMEANDAREA, the page title's own widget. The layout row names no face, so 19 is the
    // cap height measured off Campaign Mission End screen CM01.png.
    private const float TitleX = 58f;
    private const float TitleY = 53f;
    private const float TitleFont = 19f;

    // SB_STATCARD, the results card the block and its tabs sit on: two frames, 0 drawn under the
    // Best to Date tab and 1 under Most Recent.
    private const float CardX = 403f;
    private const float CardY = 297f;

    // SB_B_Statcardtab.png's own frame width, for centring the unselected tab's label the way
    // ComposedBoardView.DrawPlaque centres a plaque's own.
    private const float TabWidth = 128f;
    private const float TabLabelY = 7f;
    private const float TabLabelFont = 13f;

    private static readonly BoardArt StatCard = new(BoardArtLibrary.Ui, "SB_P_Card.png", 2);

    // The browsed position: the SCRAPBOOK.CSV mission slot (1-based) and spread. Reset to the
    // opened mission's spread 1 whenever the flow opens the book again or CampaignFlow.MissionSeq
    // changes underneath a reused page instance, so a fresh entry always lands where it was opened
    // rather than wherever browsing last left it.
    private int _viewMission = -1;
    private int _viewSpread = 1;
    private int _openedOnMissionSeq = int.MinValue;
    private int _openedOnEntry = int.MinValue;

    // Which half of the record the results block and the stamps read. Most Recent on every entry,
    // the tab the original's own SB_STATCARD frame 1 default puts in front.
    private bool _bestToDate;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignScrapbookPage(CampaignFlow flow)
        : base(flow)
    {
    }

    // Which control a row is. The order here is the order rows are offered, and a row is present
    // only where the original activates its widget.
    private enum RowKind
    {
        Replay,
        BestTab,
        MostTab,
        PrevPage,
        NextPage,
        CurrentMission,
        ViewAllMissions,
        ReturnToCabin,
        Scrap,
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Scrapbook;

    /// <inheritdoc/>
    public override string Title => "SCRAPBOOK";

    /// <inheritdoc/>
    public override int RowCount => Rows().Count;

    /// <summary>The page title, the unselected tab's label, and on spread 1 the results block's
    /// rows and the kill stamps' counts off the selected half; a spread-1 view of a mission with no
    /// recorded attempt shows the "Not yet flown" placeholder instead of a block.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            var lines = new List<BoardLine> { PageTitle() };
            var (_, spread) = Position();
            if (spread != 1)
            {
                return lines;
            }

            if (UnselectedTab() is { } tab)
            {
                lines.Add(new BoardLine(
                    TabLabel(_bestToDate ? RowKind.MostTab : RowKind.BestTab),
                    tab.X, tab.Y + TabLabelY, TabWidth, TabLabelFont, BoardInk.LabelNormal,
                    Justify: BoardJustify.Center));
            }

            if (Result() is not { } result)
            {
                lines.Add(CampaignScrapbookResults.NotYetFlown());
                return lines;
            }

            lines.AddRange(CampaignScrapbookResults.Rows(result, _bestToDate));
            lines.AddRange(CampaignScrapbookResults.StampLabels(result, _bestToDate));
            return lines;
        }
    }

    /// <summary>The shown spread's shipped scraps (<c>SCRAPBOOK.CSV</c>), gated on the mission's
    /// merged best-to-date mask and with a capture skipped when the profile carries no such file;
    /// spread 1 then lays the unselected tab, the results card and the selected half's kill stamps
    /// over them.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            if (Flow.Profile == null)
            {
                return Array.Empty<BoardPicture>();
            }

            var (mission, spread) = Position();
            var result = Result();
            var pictures = new List<BoardPicture>(ScrapbookComposition.Pictures(
                Flow.DataRoot, mission, spread, result?.Best.CompletedMask ?? 0, Flow.CapturePath));
            if (spread != 1)
            {
                return pictures;
            }

            if (UnselectedTab() is { } tab)
            {
                bool focused = Flow.Row == RowOf(_bestToDate ? RowKind.MostTab : RowKind.BestTab);
                pictures.Add(new BoardPicture(
                    tab.Art, tab.X, tab.Y, ComposedBoard.PlaqueFrame(tab.Art.Frames, focused, false)));
            }

            pictures.Add(new BoardPicture(StatCard, CardX, CardY, _bestToDate ? 0 : 1));
            if (result is { } r)
            {
                pictures.AddRange(CampaignScrapbookResults.StampPictures(r, _bestToDate));
            }

            return pictures;
        }
    }

    /// <summary>Which authored button a row presses. The unselected results tab is not among them:
    /// it draws as a picture under the card, since a plaque always draws over one.</summary>
    public override BoardButtonRef Button(int row) => KindAt(row) switch
    {
        RowKind.Replay => new BoardButtonRef(BoardButton.ReplayMission),
        RowKind.BestTab when _bestToDate => new BoardButtonRef(BoardButton.BestTab),
        RowKind.MostTab when !_bestToDate => new BoardButtonRef(BoardButton.MostTab),
        RowKind.PrevPage => new BoardButtonRef(BoardButton.ScrapbookPrev),
        RowKind.NextPage => new BoardButtonRef(BoardButton.ScrapbookNext),
        RowKind.CurrentMission => new BoardButtonRef(BoardButton.CurrentMission),
        RowKind.ViewAllMissions => new BoardButtonRef(BoardButton.ViewAllMissions),
        RowKind.ReturnToCabin => new BoardButtonRef(BoardButton.ReturnToCabin),
        _ => BoardButtonRef.None,
    };

    /// <summary>A scrap row and the unselected tab both draw no list text: the scrap's picture
    /// already stands at its authored position, and the tab's label is placed over its own art in
    /// <see cref="Captions"/>. The arrows and the two paper buttons bake their words into their
    /// strips, so only the labelled plaques need text here.</summary>
    public override string RowText(int row) => KindAt(row) switch
    {
        RowKind.Replay => "REPLAY MISSION",
        RowKind.BestTab when _bestToDate => TabLabel(RowKind.BestTab),
        RowKind.MostTab when !_bestToDate => TabLabel(RowKind.MostTab),
        RowKind.CurrentMission => Flow.Strings.Text(1200, "Current Mission"),
        RowKind.ViewAllMissions => "VIEW ALL MISSIONS",
        RowKind.ReturnToCabin => "RETURN TO CABIN",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override string Detail(int row) => KindAt(row) switch
    {
        RowKind.Replay => "Flies this mission again",
        RowKind.BestTab => "The best run of this mission so far",
        RowKind.MostTab => "The last run of this mission",
        RowKind.PrevPage => Previous() != null ? "Back a page" : "Back to the mission overview",
        RowKind.NextPage => "Forward a page",
        RowKind.CurrentMission => "Jump to the current mission",
        RowKind.ViewAllMissions => "The mission overview",
        RowKind.ReturnToCabin => "Back to the cabin",
        _ => ScrapAt(row) is { } scrap ? ScrapHint(scrap) : string.Empty,
    };

    /// <summary>REPLAY MISSION and the arrows all act on the browsed mission
    /// (<c>uiData</c> 2405's "the mission the open page shows"), not necessarily
    /// <see cref="CampaignFlow.MissionSeq"/>: they differ once the player has stepped away from the
    /// page a mission just ended on. The back arrow at the front of the book falls into the mission
    /// overview, which is where the original's own <c>sb_b_prev</c> goes.</summary>
    public override bool Accept(int row)
    {
        var (mission, spread) = Position();
        var kind = KindAt(row);
        switch (kind)
        {
            case RowKind.Replay:
                Flow.SetMission(mission - 1);
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            case RowKind.BestTab:
                _bestToDate = true;
                return true;
            case RowKind.MostTab:
                _bestToDate = false;
                return true;
            case RowKind.PrevPage:
                if (Previous() is { } prev)
                {
                    (_viewMission, _viewSpread) = prev;
                    Refocus(kind);
                }
                else
                {
                    Flow.GoTo(CampaignScreen.PreviousMissions);
                }

                return true;
            case RowKind.NextPage:
                if (Next() is { } next)
                {
                    (_viewMission, _viewSpread) = next;
                    Refocus(kind);
                }

                return true;
            case RowKind.CurrentMission:
                _viewMission = Flow.MissionSeq + 1;
                _viewSpread = 1;
                Refocus(kind);
                return true;
            case RowKind.ViewAllMissions:
                Flow.GoTo(CampaignScreen.PreviousMissions);
                return true;
            case RowKind.ReturnToCabin:
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
            default:
                if (ScrapAt(row) is not { } scrap)
                {
                    return false;
                }

                Flow.SetScrapbookZoom(mission, spread, scrap.Item);
                Flow.GoTo(CampaignScreen.ScrapbookZoom);
                return true;
        }
    }

    // The first of title, caption, then the image's own name, so the hint line never reads empty
    // for a scrap this page actually offers to open.
    private static string ScrapHint(ScrapbookScrap scrap)
    {
        foreach (var key in new[] { scrap.TitleKey, scrap.CaptionKey, scrap.TextKey })
        {
            if (key.Length > 0 && key != "0")
            {
                return key;
            }
        }

        return scrap.ImageName;
    }

    // The rows this page offers right now, in the order the cursor steps them. Only the widgets
    // the original activates are here: the two tabs and the results card belong to spread 1,
    // Replay to a spread-1 mission whose record holds a time, and Next and the bookmark to a book
    // position that has somewhere to go.
    private List<RowKind> Rows()
    {
        var (mission, spread) = Position();
        var rows = new List<RowKind>();
        if (spread == 1)
        {
            if (ReplayOffered())
            {
                rows.Add(RowKind.Replay);
            }

            rows.Add(RowKind.BestTab);
            rows.Add(RowKind.MostTab);
        }

        rows.Add(RowKind.PrevPage);
        if (Next() != null)
        {
            rows.Add(RowKind.NextPage);
        }

        if (Flow.Profile != null && mission != Flow.MissionSeq + 1)
        {
            rows.Add(RowKind.CurrentMission);
        }

        rows.Add(RowKind.ViewAllMissions);
        rows.Add(RowKind.ReturnToCabin);
        for (int i = 0; i < ScrapRows().Count; i++)
        {
            rows.Add(RowKind.Scrap);
        }

        return rows;
    }

    private RowKind KindAt(int row)
    {
        var rows = Rows();
        return row >= 0 && row < rows.Count ? rows[row] : RowKind.Scrap;
    }

    private int RowOf(RowKind kind) => Rows().IndexOf(kind);

    // Puts the cursor back on the control just pressed, at whatever row the new position gives it.
    // Row lists are rebuilt per position and a page turn off spread 1 drops three rows above the
    // arrows, so a cursor left on its old index slides down onto RETURN TO CABIN instead. A control
    // the new position no longer offers hands the cursor to the back arrow, which every one has.
    private void Refocus(RowKind kind)
    {
        int at = RowOf(kind);
        Flow.FocusRow(at >= 0 ? at : RowOf(RowKind.PrevPage));
    }

    // uiData 2411: Replay Mission is offered once either half of the mission's record holds a
    // time, which a lost attempt also does, so completion bits are not the gate. The script adds
    // the spread-1 gate by only activating the button on the results page.
    private bool ReplayOffered()
    {
        var (_, spread) = Position();
        return spread == 1 && Result() is { } result
            && (result.Latest.TimeMs != 0 || result.Best.TimeMs != 0);
    }

    // Where the tab that is NOT selected draws, or null when the layout carries no such slot.
    private (BoardArt Art, float X, float Y)? UnselectedTab() =>
        CampaignBoards.SlotOf(
            CampaignScreen.Scrapbook, _bestToDate ? BoardButton.MostTab : BoardButton.BestTab);

    private string TabLabel(RowKind kind) => kind == RowKind.BestTab
        ? Flow.Strings.Text(1159, CampaignScrapbookResults.TabTitle(bestToDate: true))
        : Flow.Strings.Text(1160, CampaignScrapbookResults.TabTitle(bestToDate: false));

    // SB_T_NAMEANDAREA's own text, langui 1215 over the player's name and the mission's short
    // name: "Zachary - The Lost Treasure".
    private BoardLine PageTitle()
    {
        var (mission, _) = Position();
        string name = Flow.Profile?.Name ?? string.Empty;
        string title = Flow.Strings.Text(3480 + mission - 1, $"Mission {mission}");
        string text = Flow.Strings.Has(1215) ? Flow.Strings.Format(1215, name, title) : $"{name} - {title}";
        return new BoardLine(text, TitleX, TitleY, 0f, TitleFont, BoardInk.Heading, Italic: true);
    }

    // The browsed (mission, spread), reset to the opened mission's spread 1 whenever the flow has
    // opened the book again or Flow.MissionSeq no longer matches what it was when last reset.
    private (int Mission, int Spread) Position()
    {
        if (_viewMission < 0 || _openedOnMissionSeq != Flow.MissionSeq
            || _openedOnEntry != Flow.ScrapbookEntry)
        {
            _viewMission = Flow.MissionSeq + 1;
            _viewSpread = 1;
            _openedOnMissionSeq = Flow.MissionSeq;
            _openedOnEntry = Flow.ScrapbookEntry;
            _bestToDate = false;
        }

        return (_viewMission, _viewSpread);
    }

    // The previous spread: one back within the mission, or the previous mission's own last spread
    // once its front is reached. Null at the front of the book (mission 1, spread 1), where the
    // back arrow opens the mission overview instead.
    private (int Mission, int Spread)? Previous()
    {
        var (mission, spread) = Position();
        if (spread > 1)
        {
            return (mission, spread - 1);
        }

        if (mission <= 1)
        {
            return null;
        }

        int last = LastSpreadOf(mission - 1);
        return last == 0 ? null : (mission - 1, last);
    }

    // The next spread: one forward within the mission, or the next mission's spread 1 once the
    // current one runs out -- probing SCRAPBOOK.CSV for the neighbouring item 1 the way
    // FUN_00406170 does, rather than storing a page count. Null past the last spread of the last
    // mission the file carries.
    private (int Mission, int Spread)? Next()
    {
        var (mission, spread) = Position();
        if (ScrapbookComposition.Items(Flow.DataRoot, mission, spread + 1).Count > 0)
        {
            return (mission, spread + 1);
        }

        return mission < CampaignSequence.MissionCount
            && ScrapbookComposition.Items(Flow.DataRoot, mission + 1, 1).Count > 0
                ? (mission + 1, 1)
                : null;
    }

    private int LastSpreadOf(int mission)
    {
        int last = 0;
        for (int spread = 1; ScrapbookComposition.Items(Flow.DataRoot, mission, spread).Count > 0; spread++)
        {
            last = spread;
        }

        return last;
    }

    private MissionResult? Result()
    {
        var (mission, _) = Position();
        return Flow.Profile is { } profile ? CampaignProgression.ResultOf(profile, mission - 1) : null;
    }

    // The shown spread's openable scraps, in item order -- the row order the cursor steps.
    private IReadOnlyList<ScrapbookScrap> ScrapRows()
    {
        if (Flow.Profile == null)
        {
            return Array.Empty<ScrapbookScrap>();
        }

        var (mission, spread) = Position();
        int bestMask = Result()?.Best.CompletedMask ?? 0;
        return ScrapbookComposition.Openable(Flow.DataRoot, mission, spread, bestMask, Flow.CapturePath);
    }

    private ScrapbookScrap? ScrapAt(int row)
    {
        var rows = Rows();
        int first = rows.IndexOf(RowKind.Scrap);
        var scraps = ScrapRows();
        int index = first < 0 ? -1 : row - first;
        return index >= 0 && index < scraps.Count ? scraps[index] : null;
    }
}
