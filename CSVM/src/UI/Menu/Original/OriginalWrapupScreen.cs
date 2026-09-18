using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original presentation's Instant Action wrap-up page, a standalone module over the decoded
/// <c>[@IA_WrapUp@]</c> section: the magazine spread, the notepad's heading, the four decoded rows
/// off the frozen snapshot, the further lines on post-its, the outcome's tick box, and a CONTINUE
/// plaque back to the Instant Action screen. The page stands only while a snapshot is held, which the session
/// hands over when the wrap-up hold ends; the built-in presentation keeps its own board instead.
/// Readings: docs/formats/instant-action/wrap-up.md.
/// </summary>
public sealed class OriginalWrapupScreen : IOriginalScreenModule
{
    /// <summary>The layout section the page is composed from.</summary>
    public const string WrapupSection = InstantActionWrapupPage.Section;

    /// <summary>The CONTINUE plaque, back to the Instant Action screen.</summary>
    public const string ContinueKey = InstantActionWrapupPage.ContinueKey;

    // The plaque's size where the file cannot be measured: GN_B_Continue.png's own frame.
    private const float FallbackPlaqueWidth = 112f;
    private const float FallbackPlaqueHeight = 34f;

    private readonly CampaignLayout _layout;
    private readonly Func<string, (int Width, int Height)?> _measure;
    private readonly IOriginalScreenHost _host;
    private readonly Action _openInstantAction;

    // The run the page is showing, frozen at the mission's ending. Null while no ended mission has
    // been handed over, which is every other moment the shell is alive.
    private IaWrapupSnapshot? _snapshot;

    /// <summary>A wrap-up module over <paramref name="layout"/>'s own section, measuring its plaque
    /// through <paramref name="measure"/> and calling back into <paramref name="host"/> for the
    /// shell state every module shares. <paramref name="openInstantAction"/> is the door CONTINUE
    /// takes, the Instant Action screen's own opener rather than a bare screen switch, so the
    /// sortie's roster and environment are re-read on the way.</summary>
    public OriginalWrapupScreen(
        CampaignLayout layout, Func<string, (int Width, int Height)?> measure, IOriginalScreenHost host,
        Action openInstantAction)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _openInstantAction = openInstantAction ?? throw new ArgumentNullException(nameof(openInstantAction));
    }

    /// <summary>The run the page is showing, or null while none has been handed over.</summary>
    public IaWrapupSnapshot? Snapshot => _snapshot;

    /// <summary>The further lines as the page writes them, for a caller reading the page back.</summary>
    public IReadOnlyList<string> ExtraLines =>
        _snapshot is { } shown ? InstantActionWrapupPage.ExtraLines(shown) : Array.Empty<string>();

    /// <summary>The four decoded rows and the heading as the page writes them, or nothing while no
    /// run is showing.</summary>
    public IReadOnlyList<BoardLine> PageRows =>
        _snapshot is { } shown ? InstantActionWrapupPage.Rows(shown, _layout) : Array.Empty<BoardLine>();

    /// <summary>Whether a screen is this module's.</summary>
    public bool Owns(OriginalScreen screen) => screen == OriginalScreen.InstantActionWrapup;

    /// <summary>Stands the page on one ended mission's frozen numbers and opens it.</summary>
    public void ShowWrapup(IaWrapupSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _host.Open(OriginalScreen.InstantActionWrapup);
    }

    /// <summary>The page's one row, the CONTINUE plaque at its authored corner.</summary>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (_layout.Widget(WrapupSection, ContinueKey) == null)
        {
            rows.Add(_host.PlaqueRow(ContinueKey, "CONTINUE", 0, true, 0));
            return;
        }

        var art = InstantActionWrapupPage.ContinueArt(_layout);
        var (x, y) = InstantActionWrapupPage.ContinueAt(_layout);
        var size = OriginalWidgets.StripSize(art, _measure, FallbackPlaqueWidth, FallbackPlaqueHeight);
        rows.Add(new OriginalRow(ContinueKey, string.Empty, OriginalRowKind.Button, x, y, size.Width, size.Height, true, 0, art));
    }

    /// <summary>The page carries no scrolling list.</summary>
    public void Lists(List<OriginalList> lists)
    {
    }

    /// <summary>Nothing on the page changes a value, so a sideways step crosses columns instead.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    /// <summary>The page opens no list.</summary>
    public bool CloseDropdown() => false;

    /// <summary>CONTINUE, the page's one door: back to the Instant Action screen the sortie was set
    /// up on, the run forgotten on the way out.</summary>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Key == ContinueKey)
        {
            Leave();
        }

        return null;
    }

    /// <summary>Back leaves the page the way CONTINUE does: the mission is over either way and
    /// there is nowhere else to stand.</summary>
    public bool Back()
    {
        Leave();
        return true;
    }

    /// <summary>The page as drawn: the magazine spread behind everything, the four brushstrokes,
    /// the heading and the eight row lines, the post-its as fills with their further lines as one
    /// flowed note each, the tick box as strokes, and the plaque.</summary>
    public void Compose(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardStroke> strokes, List<BoardLine> lines, List<BoardPlaque> plaques,
        List<BoardNote> notes, List<BoardPanel> overlays)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(backdrop);
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(fills);
        ArgumentNullException.ThrowIfNull(strokes);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(plaques);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(overlays);
        if (_snapshot is not { } shown || _layout.Widget(WrapupSection, ContinueKey) == null)
        {
            _host.ComposePlainPage("INSTANT ACTION", rows, focus, fills, lines, plaques);
            return;
        }

        var art = InstantActionWrapupPage.Pictures(_layout);
        backdrop.Add(art[0]);
        for (int i = 1; i < art.Count; i++)
        {
            pictures.Add(art[i]);
        }

        lines.AddRange(InstantActionWrapupPage.Rows(shown, _layout));
        foreach (var postIt in InstantActionWrapupPage.PostIts(shown, _layout))
        {
            fills.AddRange(InstantActionWrapupPage.PostItPaper(postIt));
            notes.Add(InstantActionWrapupPage.PostItNote(postIt));
        }

        strokes.AddRange(InstantActionWrapupPage.TickStrokes(shown.Won, _layout));

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Art is { } plaque)
            {
                plaques.Add(new BoardPlaque(
                    plaque, row.X, row.Y, i, ComposedBoard.PlaqueFrame(plaque.Frames, i == focus, i == _host.PressedRow),
                    string.Empty, BoardInk.LabelNormal));
            }
        }

        if (_host.SeatPanel(onPaper: true) is { } strip)
        {
            overlays.Add(strip);
        }
    }

    // The way off the page, shared by CONTINUE and Back: the run is dropped so a later entry to the
    // shell cannot show a stale one, and the Instant Action screen's own door reopens the setup.
    private void Leave()
    {
        _snapshot = null;
        _openInstantAction();
    }
}
