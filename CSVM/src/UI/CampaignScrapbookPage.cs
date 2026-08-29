using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The scrapbook's results page (spread 1), opened on the mission a finished mission just flew
/// (<c>docs/org/debrief.md#the-screen-is-the-scrapbook</c>): the outcome line, the four results
/// rows and the per-airframe kill stamps <see cref="CampaignScrapbookResults"/> (C15/C16)
/// computes off <see cref="CampaignFlow.MissionSeq"/>, plus spread 1's own shipped scraps (D18):
/// "the results page is a story page with the card laid over its right half"
/// (<c>docs/formats/campaign-screens.md</c>, "The scrapbook"). Then REPLAY MISSION and RETURN TO
/// CABIN. The tab is always Most Recent here, the original's own reset on every entry; the Best
/// to Date toggle, the book's page/mission arrows, the Current Mission bookmark and the story
/// pages beyond spread 1 are Wave D's remaining items.
/// </summary>
public sealed class CampaignScrapbookPage : CampaignPage
{
    /// <summary>REPLAY MISSION's row.</summary>
    public const int ReplayRow = 0;

    /// <summary>RETURN TO CABIN's row.</summary>
    public const int CabinRow = 1;

    private const int RowTotal = 2;

    // Mission-end always opens the book on spread 1 (A3, C17); stepping to a story page is D20's
    // arrows, not yet wired.
    private const int Spread = 1;

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
    public override int RowCount => RowTotal;

    /// <summary>The results block's rows and the kill stamps' counts, off the Most Recent
    /// half.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            if (Result() is not { } result)
            {
                return Array.Empty<BoardLine>();
            }

            var lines = new List<BoardLine>(CampaignScrapbookResults.Rows(result, bestToDate: false));
            lines.AddRange(CampaignScrapbookResults.StampLabels(result, bestToDate: false));
            return lines;
        }
    }

    /// <summary>The spread's shipped scraps (D18, <c>SCRAPBOOK.CSV</c>) under the kill stamps
    /// (C16), gated on the mission's merged best-to-date mask and with a capture skipped when the
    /// profile carries no such file (nothing saves one yet, per D21).</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            if (Result() is not { } result || Flow.Profile is not { } profile)
            {
                return Array.Empty<BoardPicture>();
            }

            string profileDir = Flow.Store.DirFor(profile.Name);
            var pictures = new List<BoardPicture>(ScrapbookComposition.Pictures(
                Flow.DataRoot, Flow.MissionSeq + 1, Spread, result.Best.CompletedMask,
                scrap => File.Exists(Path.Combine(profileDir, scrap.FileName))));
            pictures.AddRange(CampaignScrapbookResults.StampPictures(result, bestToDate: false));
            return pictures;
        }
    }

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) => row switch
    {
        ReplayRow => new BoardButtonRef(BoardButton.ReplayMission),
        CabinRow => new BoardButtonRef(BoardButton.ReturnToCabin),
        _ => BoardButtonRef.None,
    };

    /// <inheritdoc/>
    public override string RowText(int row) => row switch
    {
        ReplayRow => "REPLAY MISSION",
        CabinRow => "RETURN TO CABIN",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override string Detail(int row) => row switch
    {
        ReplayRow => "Flies this mission again",
        CabinRow => "Back to the cabin",
        _ => string.Empty,
    };

    /// <summary>REPLAY MISSION opens the briefing without touching <see cref="CampaignFlow.MissionSeq"/>,
    /// which is already the mission this page is showing, not the campaign's current position
    /// (they differ after a win, C17's own trap).</summary>
    public override bool Accept(int row)
    {
        switch (row)
        {
            case ReplayRow:
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            case CabinRow:
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
            default:
                return false;
        }
    }

    private MissionResult? Result() =>
        Flow.Profile is { } profile ? CampaignProgression.ResultOf(profile, Flow.MissionSeq) : null;
}
