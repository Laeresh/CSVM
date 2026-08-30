using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// One scrap's detail view, opened from a <see cref="CampaignScrapbookPage"/> row and closing back
/// to it: the zoom family's own background (<c>SB_BG_&lt;letter&gt;.jpg</c>), the scrap's inset
/// image, up to three text lines at the family's own boxes
/// (<see cref="ScrapbookComposition.ZoomFamily"/>), and EXPORT TO DESKTOP. <c>TitleKey</c>,
/// <c>CaptionKey</c> and <c>TextKey</c> are the shipped <c>SCRAPBOOK.CSV</c> row's own langui
/// symbols (<c>IDS_SB_...</c>), resolved to their text through the ids <c>RESRC1.H</c> assigns
/// them (<see cref="ScrapbookComposition.StringId"/>). A symbol neither file carries is shown as
/// itself rather than as invented English, the same degrade <see cref="CampaignBriefingPage"/> and
/// <c>BriefingObjectives</c> use for an unresolved key.
/// </summary>
public sealed class CampaignScrapbookZoomPage : CampaignPage
{
    /// <summary>CLOSE's row.</summary>
    public const int CloseRow = 0;

    /// <summary>EXPORT TO DESKTOP's row, offered only for a scrap with a file to copy.</summary>
    public const int ExportRow = 1;

    // TUNE: no font-size decode exists for these boxes (a BoardLine carries no colour of its own
    // either, so LAYOUT.CSV's colour column is moot regardless of its two typo'd rows). Picked to
    // read as a heading over body text, the way every other campaign screen's own faces do.
    private const float TitleFont = 16f;
    private const float BodyFont = 12f;

    // SBZ_GRIME, the torn frame a player capture is mounted in, and where the inset then sits:
    // the script places it at the frame's own position plus ten and eight, ignoring the row's
    // authored ZoomX/ZoomY.
    private const float GrimeX = 40f;
    private const float GrimeY = 16f;
    private const float GrimeInsetX = GrimeX + 10f;
    private const float GrimeInsetY = GrimeY + 8f;

    private static readonly BoardArt GrimeFrame =
        new(BoardArtLibrary.Ui, "DZ_ZOOMgrimeframe.png");

    private readonly string? _exportFolder;

    /// <summary>Binds the page to its flow. <paramref name="exportFolder"/> lets a test send EXPORT
    /// TO DESKTOP somewhere other than a real desktop; the <see cref="CampaignFlow"/> registry line
    /// passes none.</summary>
    public CampaignScrapbookZoomPage(CampaignFlow flow, string? exportFolder = null)
        : base(flow) => _exportFolder = exportFolder;

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.ScrapbookZoom;

    /// <inheritdoc/>
    public override string Title => "SCRAPBOOK";

    /// <inheritdoc/>
    public override int RowCount => Source() == null ? 1 : 2;

    /// <summary>The family background, then the inset image: a shipped scrap at its own
    /// <c>ZoomX</c>/<c>ZoomY</c>, a player capture inside the torn frame instead, at the position
    /// that frame dictates.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            if (Scrap() is not { } scrap)
            {
                return Array.Empty<BoardPicture>();
            }

            var pictures = new List<BoardPicture>
            {
                new(new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/SB_BG_{scrap.Zoom}.jpg"), 0, 0),
            };

            if (scrap.IsCapture)
            {
                if (Flow.CapturePath(scrap) is { } path)
                {
                    pictures.Add(new BoardPicture(GrimeFrame, GrimeX, GrimeY));
                    pictures.Add(new BoardPicture(
                        new BoardArt(BoardArtLibrary.Loose, path), GrimeInsetX, GrimeInsetY));
                }

                return pictures;
            }

            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/{scrap.ZoomFileName}"),
                scrap.ZoomX, scrap.ZoomY));
            return pictures;
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
            AddIfPresent(lines, Words(scrap.TitleKey), family.TitleX, family.TitleY, family.TitleWidth, TitleFont, BoardInk.Heading);
            AddIfPresent(lines, Words(scrap.CaptionKey), family.CaptionX, family.CaptionY, family.CaptionWidth, BodyFont, BoardInk.Row);
            AddIfPresent(lines, Words(scrap.TextKey), family.TextX, family.TextY, family.TextWidth, BodyFont, BoardInk.Row);
            return lines;
        }
    }

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) => row switch
    {
        CloseRow => new BoardButtonRef(BoardButton.CloseZoom),
        ExportRow => new BoardButtonRef(BoardButton.ExportScrap),
        _ => BoardButtonRef.None,
    };

    /// <inheritdoc/>
    public override string RowText(int row) => row switch
    {
        CloseRow => "RETURN",
        ExportRow => "EXPORT TO DESKTOP",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override string Detail(int row) => row switch
    {
        CloseRow => "Back to the page",
        ExportRow => "Saves this image to your desktop",
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (row == CloseRow)
        {
            Flow.GoTo(CampaignScreen.Scrapbook);
            return true;
        }

        if (row != ExportRow || Source() is not { } source)
        {
            return false;
        }

        var (saved, detail) = ScrapbookExport.ToDesktop(source, _exportFolder);
        Flow.SetMessage(saved
            ? Message(705, $"This image has been saved to your desktop as {detail}.", detail)
            : Message(706, $"This asset could not be saved to your desktop.  Reason:  {detail}", detail));
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

    // A scrap's own words: the langui text its symbol names, or the symbol itself when RESRC1.H or
    // the table does not carry it, the same degrade an unresolved briefing key takes.
    private string Words(string key) =>
        ScrapbookComposition.StringId(Flow.DataRoot, key) is { } id
            ? Flow.Strings.Text(id, key)
            : key;

    private string Message(int id, string fallback, string argument) =>
        Flow.Strings.Has(id) ? Flow.Strings.Format(id, argument) : fallback;

    // The file EXPORT TO DESKTOP copies: a capture out of the profile directory, a shipped scrap
    // out of the extraction. Null when neither resolves, which is what deactivates the button.
    private string? Source()
    {
        if (Scrap() is not { } scrap)
        {
            return null;
        }

        if (scrap.IsCapture)
        {
            return Flow.CapturePath(scrap);
        }

        if (Flow.DataRoot is not { } root)
        {
            return null;
        }

        string path = Path.Combine(
            root, "extracted", "rof", "ASSETS", "GRAPHICS", "SCRAPBOOK", scrap.ZoomFileName);
        return File.Exists(path) ? path : null;
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
