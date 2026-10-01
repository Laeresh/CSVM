using CSVM.Flight.Hud;
using CSVM.UI.Boards;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class HudMetricsTests
{
    [Fact]
    public void TextScalesDefaultToOneAndStayWithinTheAccessibleRange()
    {
        Assert.Equal(1f, HudMetrics.StatusTextScale, 3);
        Assert.Equal(1f, HudMetrics.MarkerTextScale, 3);
        Assert.Equal(0.5f, HudMetrics.ClampTextScale(0f), 3);
        Assert.Equal(0.5f, HudMetrics.ClampTextScale(0.49f), 3);
        Assert.Equal(1.25f, HudMetrics.ClampTextScale(1.25f), 3);
        Assert.Equal(2f, HudMetrics.ClampTextScale(2.01f), 3);
    }

    [Fact]
    public void TheReadingBoxIsThePaneUntilThePaneIsWiderThanTheReferenceFrame()
    {
        // 16:9 and anything narrower is its own box, so no ordinary screen and no 4P pane moves.
        Check(new Vector2(1280f, 720f), 0f, 1280f, 720f);
        Check(new Vector2(640f, 360f), 0f, 640f, 360f);
        Check(new Vector2(1920f, 1440f), 0f, 1920f, 1440f);

        // The 4P grid's own pane at 720p, a fraction over 16:9 because of the 2 px gutters: still
        // its own box, so the dials do not move by half a pixel on an ordinary splitscreen.
        Check(new Vector2(639f, 359f), 0f, 639f, 359f);

        // 32:9: half the width, centred, so the columns stand a 16:9 reading width apart.
        Check(new Vector2(5120f, 1440f), 1280f, 2560f, 1440f);

        // A full-screen 32:9 view at any height takes the same centred half.
        Check(new Vector2(1280f, 360f), 320f, 640f, 360f);

        // A pane the tree has not sized yet: no divide, no NaN placement.
        Check(Vector2.Zero, 0f, 0f, 0f);
    }

    [Fact]
    public void ASplitPaneIsItsOwnReadingBoxAndAFullScreenViewKeepsTheCentredFrame()
    {
        var window169 = new Vector2(1280f, 720f);

        // One player on 16:9: the pane is the window and the box is the pane, as before.
        Check(window169, window169, 0f, 1280f, 720f);

        // Two stacked on 16:9 (SplitScreen.PaneRect, 2 px gutter): each 1280x359 pane is its own
        // box. The dial columns stand at its borders, not a 16:9 reading width apart.
        var stacked = SplitScreen.PaneRect(0, 2, window169, sideBySide: false).Size;
        Check(stacked, window169, 0f, 1280f, 359f);

        // Side by side on 16:9: a pane narrower than the reference frame, its own box either way.
        var sideBySide = SplitScreen.PaneRect(0, 2, window169, sideBySide: true).Size;
        Check(sideBySide, window169, 0f, 639f, 720f);

        // 4:3 full screen and a 4:3 stacked pane: both their own box.
        var window43 = new Vector2(1024f, 768f);
        Check(window43, window43, 0f, 1024f, 768f);
        Check(SplitScreen.PaneRect(1, 2, window43, sideBySide: false).Size, window43, 0f, 1024f, 383f);

        // A 32:9 window: full screen keeps the centred frame, and its side-by-side 16:9 panes
        // are their own box, so neither reading changes.
        var window329 = new Vector2(5120f, 1440f);
        Check(window329, window329, 1280f, 2560f, 1440f);
        Check(SplitScreen.PaneRect(0, 2, window329, sideBySide: true).Size, window329, 0f, 2559f, 1440f);

        // A window the tree has not sized yet keeps the full-screen rule rather than guessing a split.
        Check(window329, Vector2.Zero, 1280f, 2560f, 1440f);
    }

    private static void Check(Vector2 paneSize, float x, float width, float height) =>
        Check(paneSize, paneSize, x, width, height);

    private static void Check(Vector2 paneSize, Vector2 windowSize, float x, float width, float height)
    {
        var box = HudMetrics.ReadingBox(paneSize, windowSize);
        Assert.Equal(x, box.Position.X, 3);
        Assert.Equal(0f, box.Position.Y, 3);
        Assert.Equal(width, box.Size.X, 3);
        Assert.Equal(height, box.Size.Y, 3);
    }
}
