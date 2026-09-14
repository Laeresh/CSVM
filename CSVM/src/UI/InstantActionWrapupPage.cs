using System.Collections.Generic;
using System.Globalization;
using CSVM.Session;
using CSVM.UI.Menu;

namespace CSVM.UI;

/// <summary>
/// The Original presentation's Instant Action wrap-up page, composed from <c>[@IA_WrapUp@]</c>'s
/// own rows (<c>docs/formats/instant-action/wrap-up.md</c>): the magazine background, the four
/// brushstroke panes, the screen title and the four title/value pairs, plus the further lines the
/// built-in board carries and the shipped page has nowhere to put (the outcome headline, the
/// context line and the stunt splits). Row titles are literal strings rather than read off
/// <c>ui_strings.json</c>, following <see cref="Flight.IaWrapupBoard"/>'s own precedent; positions
/// come through the <see cref="CampaignLayout"/> a caller hands in, with the shipped values as the
/// fallback beside every read.
/// </summary>
public static class InstantActionWrapupPage
{
    /// <summary>The layout section the page is composed from.</summary>
    public const string Section = CampaignLayout.WrapupSection;

    /// <summary>The full-page magazine art under everything.</summary>
    public const string BackgroundKey = "IAWU_BACKGROUND";

    /// <summary>The CONTINUE plaque, back to the Instant Action screen.</summary>
    public const string ContinueKey = "IAWU_B_CONTINUE";

    /// <summary>The page's own heading row.</summary>
    public const string TitleKey = "IAWU_T_TITLE";

    /// <summary>The heading's face, against its 40-pixel authored box.</summary>
    public const float TitleFont = 26f;

    /// <summary>A row title's face, against the 20-pixel boxes the four title rows author.</summary>
    public const float RowFont = 13f;

    /// <summary>A value's face, one step up from its title so the number reads as the answer.</summary>
    public const float ValueFont = 15f;

    /// <summary>The further lines' face, the size their band is measured against; the band asks the
    /// renderer to shrink it where a long stunt table would otherwise run past the pad.</summary>
    public const float ExtraFont = 11f;

    // The shipped titles, langui 1133-1137. Literal text for the reason IaWrapupBoard's own copies
    // are: ui_strings.json is a build-time extraction artifact, not one of the archives a session
    // opens, and five lines do not earn a reader of their own here.
    private const string TitleText = "Instant Action";
    private const string TimeTitle = "Time to Complete Mission";
    private const string DestroyedTitle = "Enemies Shot Down";
    private const string ZonesTitle = "Danger Zones Completed";
    private const string ShotsTitle = "Shot %";

    // The headline the shipped page has no row for: the original never lost an Instant Action
    // mission with lives to run out, so this stands in, in the words the built-in board uses.
    private const string CompleteText = "MISSION COMPLETE";
    private const string FailedText = "MISSION FAILED";

    // The authored geometry, [@IA_WrapUp@]'s own, as the fallback beside every layout read: the
    // heading, the title column, the value column, the four brushstroke panes and the plaque.
    private const float TitleX = 467f;
    private const float TitleY = 98f;
    private const float ColumnX = 480f;
    private const float ValueX = 533f;
    private const float StrokeX = 489f;
    private const float ContinueX = 640f;
    private const float ContinueY = 449f;

    // GN_B_Continue.png's one frame, which the engine-free half cannot measure; the band of further
    // lines runs down to the plaque's own foot.
    private const float ContinueFrameHeight = 34f;

    // The gap between the last value row and the first further line, the pitch the value rows
    // themselves leave. The band's right edge stops clear of the plaque by the same gap.
    private const float ExtraGap = 26f;

    // The four rows, top to bottom: the title row's line, its value's line, and the brushstroke
    // pane under the title.
    private static readonly (string TitleKey, string Title, float TitleY, string ValueKey, float ValueY, string StrokeKey, float StrokeY)[] Wired =
    {
        ("IAWU_T_TIMETITLE", TimeTitle, 154f, "IAWU_T_TIME", 185f, "IAWU_LINE0", 173f),
        ("IAWU_T_DESTROYEDTITLE", DestroyedTitle, 210f, "IAWU_T_DESTROYED", 235f, "IAWU_LINE1", 226f),
        ("IAWU_T_ZONESTITLE", ZonesTitle, 280f, "IAWU_T_ZONES", 306f, "IAWU_LINE2", 297f),
        ("IAWU_T_SHOTSTITLE", ShotsTitle, 345f, "IAWU_T_SHOTS", 369f, "IAWU_LINE3", 359f),
    };

    private static readonly BoardArt Background = new(BoardArtLibrary.Ui, "IA_StatScreenBackground.jpg");
    private static readonly BoardArt Brushstroke = new(BoardArtLibrary.Ui, "IA_StatScreen_Brushstroke.png");
    private static readonly BoardArt ContinuePlaque = new(BoardArtLibrary.Ui, "GN_B_Continue.png", 4);

    /// <summary>The heading and the four title/value pairs, each at its own <c>IAWU_T_*</c> row.
    /// The four values are the snapshot's own and are never recomputed here.</summary>
    public static IReadOnlyList<BoardLine> Rows(IaWrapupSnapshot snapshot, CampaignLayout? layout = null)
    {
        System.ArgumentNullException.ThrowIfNull(snapshot);
        layout ??= CampaignLayout.Fallback;
        var (titleX, titleY) = layout.At(Section, TitleKey, TitleX, TitleY);
        var lines = new List<BoardLine>
        {
            new(TitleText, titleX, titleY, 0f, TitleFont, BoardInk.Heading, Italic: true),
        };
        string[] values =
        {
            InstantActionRuntime.FormatElapsed(snapshot.Elapsed),
            snapshot.EnemiesShotDown.ToString(CultureInfo.InvariantCulture),
            snapshot.ZonesCompleted.ToString(CultureInfo.InvariantCulture),
            snapshot.ShotPercent.ToString(CultureInfo.InvariantCulture) + "%",
        };
        for (int i = 0; i < Wired.Length; i++)
        {
            var row = Wired[i];
            var (tx, ty) = layout.At(Section, row.TitleKey, ColumnX, row.TitleY);
            var (vx, vy) = layout.At(Section, row.ValueKey, ValueX, row.ValueY);
            lines.Add(new BoardLine(row.Title, tx, ty, 0f, RowFont, BoardInk.Heading, Italic: true));
            lines.Add(new BoardLine(values[i], vx, vy, 0f, ValueFont, BoardInk.Row, Italic: true));
        }

        return lines;
    }

    /// <summary>The further lines the built-in board carries and the shipped page authors no row
    /// for: the outcome headline, the context line naming the chapter and the mission type, and the
    /// stunt run's splits as the snapshot froze them.</summary>
    public static IReadOnlyList<string> ExtraLines(IaWrapupSnapshot snapshot)
    {
        System.ArgumentNullException.ThrowIfNull(snapshot);
        var lines = new List<string> { snapshot.Won ? CompleteText : FailedText };
        if (snapshot.Context.Length > 0)
        {
            lines.Add(snapshot.Context);
        }

        if (snapshot.StuntLines is { } splits)
        {
            lines.AddRange(splits);
        }

        return lines;
    }

    /// <summary>The page's art: the magazine spread under everything, then a brushstroke under each
    /// of the four row titles.</summary>
    public static IReadOnlyList<BoardPicture> Pictures(CampaignLayout? layout = null)
    {
        layout ??= CampaignLayout.Fallback;
        var (backX, backY) = layout.At(Section, BackgroundKey, 0f, 0f);
        var pictures = new List<BoardPicture>
        {
            new(layout.Art(Section, BackgroundKey, Background), backX, backY),
        };
        foreach (var row in Wired)
        {
            var (x, y) = layout.At(Section, row.StrokeKey, StrokeX, row.StrokeY);
            pictures.Add(new BoardPicture(layout.Art(Section, row.StrokeKey, Brushstroke), x, y));
        }

        return pictures;
    }

    /// <summary>The band the further lines flow inside: from under the last value row down to the
    /// plaque's foot, in the title column and stopping clear of the plaque's left edge. Read off the
    /// page's own rows rather than written down, so a layout that spaces them differently moves the
    /// band with them instead of drawing it over an authored row.</summary>
    public static (float X, float Y, float Width, float Height) ExtraBox(CampaignLayout? layout = null)
    {
        layout ??= CampaignLayout.Fallback;
        var last = Wired[^1];
        var (x, _) = layout.At(Section, last.TitleKey, ColumnX, last.TitleY);
        var (_, valueY) = layout.At(Section, last.ValueKey, ValueX, last.ValueY);
        var (plaqueX, plaqueY) = layout.At(Section, ContinueKey, ContinueX, ContinueY);
        float top = valueY + ExtraGap;
        return (x, top, System.Math.Max(1f, plaqueX - ExtraGap - x),
            System.Math.Max(1f, plaqueY + ContinueFrameHeight - top));
    }

    /// <summary>The CONTINUE plaque's art strip.</summary>
    public static BoardArt ContinueArt(CampaignLayout? layout = null) =>
        (layout ?? CampaignLayout.Fallback).Art(Section, ContinueKey, ContinuePlaque);

    /// <summary>The CONTINUE plaque's authored top-left.</summary>
    public static (float X, float Y) ContinueAt(CampaignLayout? layout = null) =>
        (layout ?? CampaignLayout.Fallback).At(Section, ContinueKey, ContinueX, ContinueY);

    /// <summary>A stand-in run for the screenshot aids and the coverage walk: a stunt flight with
    /// three zones behind it, so the page shows every line family at once.</summary>
    public static IaWrapupSnapshot Sample(bool won) => new(
        won, "C1   ·   Stunt Flying", 186f, 4, 3, 27,
        new[] { "1.  Pier    12.4    12.4", "2.  Bridge    15.1    27.5", "3.  Tower    18.6    46.1", "TOTAL   46.1" });
}
