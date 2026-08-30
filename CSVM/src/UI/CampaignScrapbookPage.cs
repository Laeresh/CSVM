using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The scrapbook's results page, opened on the mission a finished mission just flew
/// (<c>docs/org/debrief.md#the-screen-is-the-scrapbook</c>): the shown spread's results block, kill
/// stamps and shipped scraps, the page/mission arrows and the Current Mission bookmark for browsing
/// the rest of the book, and every openable scrap
/// (<see cref="ScrapbookScrap.Opens"/>) as its own row opening
/// <see cref="CampaignScreen.ScrapbookZoom"/>. REPLAY MISSION and RETURN TO CABIN act on whichever
/// mission is browsed. The Best to Date toggle and the page title (mission name and area) are not
/// yet wired.
/// </summary>
public sealed class CampaignScrapbookPage : CampaignPage
{
    /// <summary>REPLAY MISSION's row.</summary>
    public const int ReplayRow = 0;

    /// <summary>RETURN TO CABIN's row.</summary>
    public const int CabinRow = 1;

    // Where the fixed rows end and the arrows/bookmark begin; ScrapRowBase (below) follows those.
    private const int NavRowBase = 2;

    // The browsed position: the SCRAPBOOK.CSV mission slot (1-based) and spread. Reset to the
    // flown mission's spread 1 whenever CampaignFlow.MissionSeq changes underneath a reused page
    // instance, so a fresh mission end always reopens the book where it flew rather than wherever
    // browsing last left it.
    private int _viewMission = -1;
    private int _viewSpread = 1;
    private int _openedOnMissionSeq = int.MinValue;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignScrapbookPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Scrapbook;

    /// <inheritdoc/>
    public override string Title => "SCRAPBOOK";

    /// <inheritdoc/>
    public override int RowCount => ScrapRowBase() + ScrapRows().Count;

    /// <summary>The results block's rows and the kill stamps' counts, off the Most Recent half, on
    /// spread 1 only; a spread-1 view of a mission with no recorded attempt shows the "Not yet
    /// flown" placeholder instead.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            var (_, spread) = Position();
            if (spread != 1)
            {
                return Array.Empty<BoardLine>();
            }

            if (Result() is not { } result)
            {
                return new[] { CampaignScrapbookResults.NotYetFlown() };
            }

            var lines = new List<BoardLine>(CampaignScrapbookResults.Rows(result, bestToDate: false));
            lines.AddRange(CampaignScrapbookResults.StampLabels(result, bestToDate: false));
            return lines;
        }
    }

    /// <summary>The shown spread's shipped scraps (<c>SCRAPBOOK.CSV</c>), gated on the mission's
    /// merged best-to-date mask (0 for a mission with no recorded attempt, so only the
    /// always-visible rows draw) and with a capture skipped when the profile carries no such file;
    /// spread 1 also carries the Most Recent kill stamps.</summary>
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
            int bestMask = result?.Best.CompletedMask ?? 0;
            var pictures = new List<BoardPicture>(ScrapbookComposition.Pictures(
                Flow.DataRoot, mission, spread, bestMask, CaptureExists));
            if (spread == 1 && result is { } r)
            {
                pictures.AddRange(CampaignScrapbookResults.StampPictures(r, bestToDate: false));
            }

            return pictures;
        }
    }

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row)
    {
        if (row == ReplayRow)
        {
            return new BoardButtonRef(BoardButton.ReplayMission);
        }

        if (row == CabinRow)
        {
            return new BoardButtonRef(BoardButton.ReturnToCabin);
        }

        var nav = NavRows();
        int navIndex = row - NavRowBase;
        return navIndex >= 0 && navIndex < nav.Count
            ? new BoardButtonRef(nav[navIndex])
            : BoardButtonRef.None;
    }

    /// <summary>A scrap row draws no list text of its own: its picture already stands at its
    /// authored position, and drawing its name over it too would be the shell inventing a caption
    /// the original never had. The page arrows bake their own words into their strip, so only the
    /// labelled Current Mission bookmark needs text here.</summary>
    public override string RowText(int row)
    {
        if (row == ReplayRow)
        {
            return "REPLAY MISSION";
        }

        if (row == CabinRow)
        {
            return "RETURN TO CABIN";
        }

        var nav = NavRows();
        int navIndex = row - NavRowBase;
        if (navIndex >= 0 && navIndex < nav.Count)
        {
            return nav[navIndex] == BoardButton.CurrentMission ? "CURRENT MISSION" : string.Empty;
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (row == ReplayRow)
        {
            return "Flies this mission again";
        }

        if (row == CabinRow)
        {
            return "Back to the cabin";
        }

        var nav = NavRows();
        int navIndex = row - NavRowBase;
        if (navIndex >= 0 && navIndex < nav.Count)
        {
            return nav[navIndex] switch
            {
                BoardButton.ScrapbookPrev => "Back a page",
                BoardButton.ScrapbookNext => "Forward a page",
                _ => "Jump to the current mission",
            };
        }

        return ScrapAt(row) is { } scrap ? ScrapHint(scrap) : string.Empty;
    }

    /// <summary>REPLAY MISSION and the arrows all act on the browsed mission
    /// (<c>uiData</c> 2405's "the mission the open page shows"), not necessarily
    /// <see cref="CampaignFlow.MissionSeq"/>: they differ once the player has stepped away from the
    /// page a mission just ended on. A scrap row opens its detail view.</summary>
    public override bool Accept(int row)
    {
        var (mission, spread) = Position();
        if (row == ReplayRow)
        {
            Flow.SetMission(mission - 1);
            Flow.GoTo(CampaignScreen.Briefing);
            return true;
        }

        if (row == CabinRow)
        {
            Flow.GoTo(CampaignScreen.Cabin);
            return true;
        }

        var nav = NavRows();
        int navIndex = row - NavRowBase;
        if (navIndex >= 0 && navIndex < nav.Count)
        {
            switch (nav[navIndex])
            {
                case BoardButton.ScrapbookPrev:
                    if (Previous(mission, spread) is { } prev)
                    {
                        (_viewMission, _viewSpread) = prev;
                    }

                    return true;
                case BoardButton.ScrapbookNext:
                    if (Next(mission, spread) is { } next)
                    {
                        (_viewMission, _viewSpread) = next;
                    }

                    return true;
                default: // CurrentMission
                    _viewMission = Flow.MissionSeq + 1;
                    _viewSpread = 1;
                    return true;
            }
        }

        if (ScrapAt(row) is not { } scrap)
        {
            return false;
        }

        Flow.SetScrapbookZoom(mission, spread, scrap.Item);
        Flow.GoTo(CampaignScreen.ScrapbookZoom);
        return true;
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

    // The browsed (mission, spread), reset to the flown mission's spread 1 whenever
    // Flow.MissionSeq no longer matches what it was when last reset -- covers both a page seen for
    // the first time and a cached page instance reopened on a different mission.
    private (int Mission, int Spread) Position()
    {
        if (_viewMission < 0 || _openedOnMissionSeq != Flow.MissionSeq)
        {
            _viewMission = Flow.MissionSeq + 1;
            _viewSpread = 1;
            _openedOnMissionSeq = Flow.MissionSeq;
        }

        return (_viewMission, _viewSpread);
    }

    // The previous spread: one back within the mission, or the previous mission's own last spread
    // once its front is reached. Null at the front of the book (mission 1, spread 1); this shell
    // has no table of contents screen to fall into there yet.
    private (int Mission, int Spread)? Previous(int mission, int spread)
    {
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
    private (int Mission, int Spread)? Next(int mission, int spread)
    {
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

    // Which of the page/mission arrows and the Current Mission bookmark are offered right now, in
    // the order they occupy rows from NavRowBase: only Prev/Next that actually lead somewhere, and
    // the bookmark only while the browsed mission is not the campaign's current one -- mirroring
    // uiData 2401's own activate/deactivate flags rather than drawing a dead button.
    private List<BoardButton> NavRows()
    {
        var (mission, spread) = Position();
        var rows = new List<BoardButton>();
        if (Previous(mission, spread) != null)
        {
            rows.Add(BoardButton.ScrapbookPrev);
        }

        if (Next(mission, spread) != null)
        {
            rows.Add(BoardButton.ScrapbookNext);
        }

        if (Flow.Profile != null && mission != Flow.MissionSeq + 1)
        {
            rows.Add(BoardButton.CurrentMission);
        }

        return rows;
    }

    private int ScrapRowBase() => NavRowBase + NavRows().Count;

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
        return ScrapbookComposition.Openable(Flow.DataRoot, mission, spread, bestMask, CaptureExists);
    }

    private ScrapbookScrap? ScrapAt(int row)
    {
        var scraps = ScrapRows();
        int index = row - ScrapRowBase();
        return index >= 0 && index < scraps.Count ? scraps[index] : null;
    }

    private bool CaptureExists(ScrapbookScrap scrap) =>
        Flow.Profile is { } profile
        && File.Exists(Path.Combine(Flow.Store.DirFor(profile.Name), scrap.FileName));
}
