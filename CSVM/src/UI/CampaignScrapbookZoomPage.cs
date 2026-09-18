using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// One scrap's detail view, opened from a <see cref="CampaignScrapbookPage"/> row and closing back
/// to it: the zoom family's own background (<c>SB_BG_&lt;letter&gt;.jpg</c>), the scrap's inset
/// image where it names one, up to three text lines at the family's own boxes in their own faces
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

    // The sizes a scrap's words take only where their langui row names no face it can read, which
    // no shipped scrap does: the row's [FONTID] carries the size (LanguiFace.Pixels).
    private const float TitleFont = 16f;
    private const float BodyFont = 12f;

    // SBZ_GRIME, the torn frame a player capture is mounted in, and where the inset then sits:
    // the script places it at the frame's own position plus ten and eight, ignoring the row's
    // authored ZoomX/ZoomY.
    private const float GrimeX = 40f;
    private const float GrimeY = 16f;
    private const float GrimeInsetDx = 10f;
    private const float GrimeInsetDy = 8f;

    // A langui row's inline bold and italic runs, <B>...<b> and <I>...<i>.
    private static readonly Regex InlineStyle = new("<[BbIi]>", RegexOptions.Compiled);

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
                    var (grimeX, grimeY) = Flow.Layout.At(CampaignLayout.ZoomSection, "SBZ_GRIME", GrimeX, GrimeY);
                    pictures.Add(new BoardPicture(
                        Flow.Layout.Art(CampaignLayout.ZoomSection, "SBZ_GRIME", GrimeFrame), grimeX, grimeY));
                    pictures.Add(new BoardPicture(
                        new BoardArt(BoardArtLibrary.Loose, path), grimeX + GrimeInsetDx, grimeY + GrimeInsetDy));
                }

                return pictures;
            }

            if (scrap.HasZoomInset)
            {
                pictures.Add(new BoardPicture(
                    new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/{scrap.ZoomFileName}"),
                    scrap.ZoomX, scrap.ZoomY));
            }

            return pictures;
        }
    }

    /// <summary>The scrap's words at the zoom family's three boxes, the decoded layout's
    /// <c>SBZ_T_*</c> rows first and <c>LAYOUT.CSV</c>'s own read of the same rows where the
    /// layout is absent.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            if (Scrap() is not { } scrap)
            {
                return Array.Empty<BoardLine>();
            }

            var family = Flow.Layout.ZoomFamily(scrap.Zoom) ?? ScrapbookComposition.ZoomFamily(Flow.DataRoot, scrap.Zoom);
            if (family is not { } boxes)
            {
                return Array.Empty<BoardLine>();
            }

            var lines = new List<BoardLine>();
            AddIfPresent(lines, scrap.TitleKey, $"SBZ_T_TITLE{scrap.Zoom}",
                (boxes.TitleX, boxes.TitleY, boxes.TitleWidth), TitleFont, BoardInk.Heading);
            AddIfPresent(lines, scrap.CaptionKey, $"SBZ_T_CAPTION{scrap.Zoom}",
                (boxes.CaptionX, boxes.CaptionY, boxes.CaptionWidth), BodyFont, BoardInk.Row);
            AddIfPresent(lines, scrap.TextKey, $"SBZ_T_TEXT{scrap.Zoom}",
                (boxes.TextX, boxes.TextY, boxes.TextWidth), BodyFont, BoardInk.Row);
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
    // key is anything else. The words take the face their langui row names, one line per face
    // height as the original pitches them, and the box row's own colour and justification.
    private void AddIfPresent(
        List<BoardLine> lines, string key, string box, (float X, float Y, float Width) at, float size, BoardInk ink)
    {
        if (key.Length == 0 || key == "0")
        {
            return;
        }

        int? id = ScrapbookComposition.StringId(Flow.DataRoot, key);
        var face = id is { } row ? LanguiFace.Parse(Flow.Strings.Face(row)) : null;
        BoardTint? colour = Flow.Layout.Widget(CampaignLayout.ZoomSection, box) is { } widget
            && widget.TryColor("Color", out var c)
                ? new BoardTint(c.R, c.G, c.B)
                : null;
        lines.Add(new BoardLine(
            Words(key, id), at.X, at.Y, at.Width, face?.Pixels ?? size, ink,
            Justify: Flow.Layout.Justify(CampaignLayout.ZoomSection, box, BoardJustify.Left),
            Leading: face?.Pixels ?? 0f, Face: face, Colour: colour));
    }

    // A scrap's own words: the langui text its symbol names, or the symbol itself when RESRC1.H or
    // the table does not carry it, the same degrade an unresolved briefing key takes. The inline
    // <B>/<I> runs are markup the board has no mixed-style draw for, so they are dropped.
    private string Words(string key, int? id) =>
        id is { } row ? InlineStyle.Replace(Flow.Strings.Text(row, key), string.Empty) : key;

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

        if (!scrap.HasZoomInset || Flow.DataRoot is not { } root)
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
