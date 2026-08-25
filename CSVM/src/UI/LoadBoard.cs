using System.Collections.Generic;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The load screen drawn over the whole window while a session builds: the original's own composed
/// artwork (<c>docs/org/loading-screen.md</c>) through the campaign boards' authored-pixel surface.
/// A build is one synchronous block that stalls the frame loop for a second or two, so nothing can
/// be drawn DURING it; the Launcher shows this, lets one frame render, and builds on the next tick.
/// ⚠ Deliberately has no progress fill and no turning propeller: both need the build decoupled
/// from the draw, which is a different item, so the bar draws its unlit strip and the propeller one
/// still frame. Nothing here reports a fraction the build has not measured.
/// </summary>
public sealed partial class LoadBoard : Control
{
    // Where the two lines sit on each screen's own writing surface: the campaign sheet's white
    // paper, the blackboard's ruled right half. Not decoded; the decode carries no text position.
    private const float CampaignTextX = 60f;
    private const float BoardTextX = 360f;

    private static readonly BoardArt Sheet = new(BoardArtLibrary.Rimage, "loadframe");
    private static readonly BoardArt Blackboard = new(BoardArtLibrary.Rimage, "loadframempt2");

    private ComposedBoard _board = Empty();
    private BoardPalette _palette = BoardPalette.Paper;
    private string _dataRoot = string.Empty;

    /// <summary>Builds the board for one launch. <paramref name="campaign"/> picks the paper sheet
    /// over the blackboard, the split the original makes; free flight and dogfight are ours rather
    /// than the original's and take the non-campaign screen. <paramref name="subject"/> names what
    /// is loading, or is empty for no second line.</summary>
    public static LoadBoard Build(string dataRoot, bool campaign, string subject)
    {
        var board = new LoadBoard
        {
            _dataRoot = dataRoot,
            _board = campaign ? CampaignSheet(subject) : Blackboards(subject),
            _palette = campaign ? BoardPalette.Paper : BoardPalette.Chalk,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        board.SetAnchorsPreset(LayoutPreset.FullRect);
        return board;
    }

    /// <summary>Populates on entry rather than in <see cref="Build"/>: the view sizes itself off
    /// the viewport, which a node outside the tree cannot read.</summary>
    public override void _Ready()
    {
        var view = ComposedBoardView.Build(_dataRoot);
        AddChild(view);
        view.Show(_board, _palette, string.Empty, string.Empty);
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        // Track the window (resizable) so the artwork always covers it, the whole-window rule every
        // shared board follows.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    // The campaign screen: a chart sheet in its frame, with the unlit bar under it at the position
    // the repaint measures its fill from. No pictures, which is what the campaign dialogs carry.
    private static ComposedBoard CampaignSheet(string subject) =>
        new(
            new[]
            {
                new BoardPicture(Sheet, 0, 0, 0, false, 1f, 0f, BoardFit.AuthoredWidth, BoardFit.AuthoredHeight),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prog_blkload"), 90, 548),
            },
            System.Array.Empty<BoardStroke>(),
            Lines(CampaignTextX, subject, 460f),
            System.Array.Empty<BoardPlaque>());

    // The Instant Action screen: the blackboard, its three authored photographs each centred on
    // its own coordinate, the unlit lamp bar and one still propeller frame beside it.
    private static ComposedBoard Blackboards(string subject) =>
        new(
            new[]
            {
                new BoardPicture(Blackboard, 0, 0),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "MP-shotdown"), 197, 157, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "MP-crash"), 197, 307, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "mp-dangerzone2"), 197, 457, 0, true),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prp0"), 506, 549),
                new BoardPicture(new BoardArt(BoardArtLibrary.Rimage, "prog_blk"), 564, 546),
            },
            System.Array.Empty<BoardStroke>(),
            Lines(BoardTextX, subject, 380f),
            System.Array.Empty<BoardPlaque>());

    private static IReadOnlyList<BoardLine> Lines(float x, string subject, float width)
    {
        var lines = new List<BoardLine>(2)
        {
            new("LOADING", x, 60f, width, 26f, BoardInk.Heading),
        };
        if (subject.Length > 0)
        {
            lines.Add(new BoardLine(subject, x, 100f, width, 15f, BoardInk.Row));
        }

        return lines;
    }

    private static ComposedBoard Empty() =>
        new(
            System.Array.Empty<BoardPicture>(),
            System.Array.Empty<BoardStroke>(),
            System.Array.Empty<BoardLine>(),
            System.Array.Empty<BoardPlaque>());
}
