using Godot;

namespace CSVM.UI;

/// <summary>
/// The load screen drawn over the whole window while a session builds: the composition
/// <see cref="LoadScreens"/> makes, through the campaign boards' authored-pixel surface.
/// A build is one synchronous block that stalls the frame loop for a second or two, so nothing can
/// be drawn DURING it; the Launcher shows this, lets one frame render, and builds on the next tick.
/// ⚠ Deliberately has no progress fill and no turning propeller: both need the build decoupled
/// from the draw, which is a different item, so the bar draws its unlit strip and the propeller one
/// still frame. Nothing here reports a fraction the build has not measured.
/// </summary>
public sealed partial class LoadBoard : Control
{
    private ComposedBoard _board = LoadScreens.Empty;
    private BoardPalette _palette = BoardPalette.Paper;
    private string _dataRoot = string.Empty;

    /// <summary>Builds the board for one launch. <paramref name="campaign"/> picks the paper sheet
    /// over the blackboard, and <paramref name="missionType"/> the Instant Action dialog whose four
    /// texts the blackboard writes, null for a mode of ours. <paramref name="subject"/> is the
    /// sheet's second line, and the heading a mode of ours takes in place of a dialog.</summary>
    public static LoadBoard Build(
        string dataRoot, string zrdrPath, string messagesPath, bool campaign, string subject,
        string? missionType)
    {
        var board = new LoadBoard
        {
            _dataRoot = dataRoot,
            _board = LoadScreens.For(campaign, subject, missionType, zrdrPath, messagesPath),
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
}
