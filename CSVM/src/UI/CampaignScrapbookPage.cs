using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The scrapbook's results page (spread 1), opened on the mission a finished mission just flew
/// (<c>docs/org/debrief.md#the-screen-is-the-scrapbook</c>): the outcome line, the four results
/// rows, the kill stamps and spread 1's own shipped scraps. Every scrap that opens
/// (<see cref="ScrapbookScrap.Opens"/>) gets its own row after REPLAY MISSION and RETURN TO CABIN,
/// stepped into rather than clicked (no pointer hit-testing over freely-positioned art);
/// confirming one opens <see cref="CampaignScreen.ScrapbookZoom"/>. The Best to Date toggle, the
/// book's arrows/bookmark and the story pages beyond spread 1 are not yet wired.
/// </summary>
public sealed class CampaignScrapbookPage : CampaignPage
{
    /// <summary>REPLAY MISSION's row.</summary>
    public const int ReplayRow = 0;

    /// <summary>RETURN TO CABIN's row.</summary>
    public const int CabinRow = 1;

    // Scrap rows start here, one per ScrapRows().Count.
    private const int ScrapRowBase = 2;

    // Mission-end always opens the book on spread 1; stepping to a story page needs the book's
    // page/mission arrows, not yet wired.
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
    public override int RowCount => ScrapRowBase + ScrapRows().Count;

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

    /// <summary>The spread's shipped scraps (<c>SCRAPBOOK.CSV</c>) under the kill stamps, gated on
    /// the mission's merged best-to-date mask and with a capture skipped when the profile carries
    /// no such file (nothing saves one yet).</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            if (Result() is not { } result || Flow.Profile is not { } profile)
            {
                return Array.Empty<BoardPicture>();
            }

            var pictures = new List<BoardPicture>(ScrapbookComposition.Pictures(
                Flow.DataRoot, Flow.MissionSeq + 1, Spread, result.Best.CompletedMask, CaptureExists));
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

    /// <summary>A scrap row draws no list text of its own: its picture already stands at its
    /// authored position, and drawing its name over it too would be the shell inventing a caption
    /// the original never had.</summary>
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
        _ when ScrapAt(row) is { } scrap => ScrapHint(scrap),
        _ => string.Empty,
    };

    /// <summary>REPLAY MISSION opens the briefing without touching <see cref="CampaignFlow.MissionSeq"/>,
    /// which is already the mission this page is showing, not the campaign's current position
    /// (they differ after a win). A scrap row opens its detail view.</summary>
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
                if (ScrapAt(row) is not { } scrap)
                {
                    return false;
                }

                Flow.SetScrapbookZoom(Flow.MissionSeq + 1, Spread, scrap.Item);
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

    private MissionResult? Result() =>
        Flow.Profile is { } profile ? CampaignProgression.ResultOf(profile, Flow.MissionSeq) : null;

    // The spread's openable scraps, in item order -- the row order the cursor steps.
    private IReadOnlyList<ScrapbookScrap> ScrapRows() =>
        Result() is { } result && Flow.Profile != null
            ? ScrapbookComposition.Openable(
                Flow.DataRoot, Flow.MissionSeq + 1, Spread, result.Best.CompletedMask, CaptureExists)
            : Array.Empty<ScrapbookScrap>();

    private ScrapbookScrap? ScrapAt(int row)
    {
        var scraps = ScrapRows();
        int index = row - ScrapRowBase;
        return index >= 0 && index < scraps.Count ? scraps[index] : null;
    }

    private bool CaptureExists(ScrapbookScrap scrap) =>
        Flow.Profile is { } profile
        && File.Exists(Path.Combine(Flow.Store.DirFor(profile.Name), scrap.FileName));
}
