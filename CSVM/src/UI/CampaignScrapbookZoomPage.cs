using System;
using System.Collections.Generic;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// One scrap's detail view, opened from a <see cref="CampaignScrapbookPage"/> row and closing
/// back to it: the zoom family's own background (<c>SB_BG_&lt;letter&gt;.jpg</c>), the scrap's inset
/// image at its own <c>ZoomX</c>/<c>ZoomY</c>, and up to three text lines (title, caption, body) at
/// the family's own boxes (<see cref="ScrapbookComposition.ZoomFamily"/>). <c>TitleKey</c>,
/// <c>CaptionKey</c> and <c>TextKey</c> are the shipped <c>SCRAPBOOK.CSV</c> row's own langui
/// symbols (<c>IDS_SB_...</c>): the numeric ids <c>RESRC1.H</c> assigns them belong to
/// <c>ScrapBook.Rc</c>, a resource script never extracted, so no string table anywhere resolves
/// them. Shown as the raw symbol rather than invented English, the same degrade
/// <see cref="CampaignBriefingPage"/> and <c>BriefingObjectives</c> use for an unresolved key.
/// </summary>
public sealed class CampaignScrapbookZoomPage : CampaignPage
{
    /// <summary>CLOSE's row.</summary>
    public const int CloseRow = 0;

    // TUNE: no font-size decode exists for these boxes (a BoardLine carries no colour of its own
    // either, so LAYOUT.CSV's colour column is moot regardless of its two typo'd rows). Picked to
    // read as a heading over body text, the way every other campaign screen's own faces do.
    private const float TitleFont = 16f;
    private const float BodyFont = 12f;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignScrapbookZoomPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.ScrapbookZoom;

    /// <inheritdoc/>
    public override string Title => "SCRAPBOOK";

    /// <inheritdoc/>
    public override int RowCount => 1;

    /// <inheritdoc/>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            if (Scrap() is not { } scrap)
            {
                return Array.Empty<BoardPicture>();
            }

            return new List<BoardPicture>
            {
                new(new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/SB_BG_{scrap.Zoom}.jpg"), 0, 0),
                new(new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/{scrap.ZoomFileName}"), scrap.ZoomX, scrap.ZoomY),
            };
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            if (Scrap() is not { } scrap || ScrapbookComposition.ZoomFamily(Flow.DataRoot, scrap.Zoom) is not { } family)
            {
                return Array.Empty<BoardLine>();
            }

            var lines = new List<BoardLine>();
            AddIfPresent(lines, scrap.TitleKey, family.TitleX, family.TitleY, family.TitleWidth, TitleFont, BoardInk.Heading);
            AddIfPresent(lines, scrap.CaptionKey, family.CaptionX, family.CaptionY, family.CaptionWidth, BodyFont, BoardInk.Row);
            AddIfPresent(lines, scrap.TextKey, family.TextX, family.TextY, family.TextWidth, BodyFont, BoardInk.Row);
            return lines;
        }
    }

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) =>
        row == CloseRow ? new BoardButtonRef(BoardButton.CloseZoom) : BoardButtonRef.None;

    /// <inheritdoc/>
    public override string RowText(int row) => row == CloseRow ? "RETURN" : string.Empty;

    /// <inheritdoc/>
    public override string Detail(int row) => row == CloseRow ? "Back to the page" : string.Empty;

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (row != CloseRow)
        {
            return false;
        }

        Flow.GoTo(CampaignScreen.Scrapbook);
        return true;
    }

    // "0" is the CSV's own absent-text sentinel (docs/formats/campaign-screens.md), so a genuine
    // key is anything else.
    private static void AddIfPresent(
        List<BoardLine> lines, string key, float x, float y, float width, float size, BoardInk ink)
    {
        if (key.Length > 0 && key != "0")
        {
            lines.Add(new BoardLine(key, x, y, width, size, ink));
        }
    }

    private ScrapbookScrap? Scrap()
    {
        if (Flow.ZoomTarget is not { } target)
        {
            return null;
        }

        var items = ScrapbookComposition.Items(Flow.DataRoot, target.Mission, target.Spread);
        return target.Item >= 1 && target.Item <= items.Count ? items[target.Item - 1] : null;
    }
}
