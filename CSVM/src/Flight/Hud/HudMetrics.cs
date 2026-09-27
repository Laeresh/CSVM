using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>The one place the flight HUD decides how big it draws (docs/architecture.md). The
/// window height against a 1440p reference sets the base scale. A splitscreen pane's share of
/// that window is damped through a square root (<see cref="PaneFactor"/>, exactly 1 full-screen),
/// confirmed at the controls. Damped sizes stay on screen only if positions anchor to a pane
/// EDGE, not the top-left. A horizontal edge is <see cref="ReadingBox(Vector2, Vector2)"/>'s,
/// which keeps a wide full-screen view's columns from spreading a screen apart. On a NARROWER
/// pane a column would hold a 16:9 margin from a nearer border, which
/// <see cref="ColumnOutdent"/> hands back.</summary>
public static class HudMetrics
{
    /// <summary>The reference viewport height every HUD metric was measured at (HUD.png).</summary>
    public const float ReferenceHeight = 1440f;

    /// <summary>The aspect the reference frame was measured at: HUD.png is 2556x1440, 16:9 to
    /// within its own bezel scans, and every edge-anchored constant is an offset from its edges.</summary>
    public const float ReferenceAspect = 16f / 9f;

    /// <summary>The border an edge-anchored gauge column keeps on a pane narrower than the
    /// reference frame, in reference pixels. The reference placement's own margins are a 16:9
    /// frame's worth of room that a 4:3 pane does not have, so there the column gives all but this
    /// back and sits at the border.</summary>
    public const float MinimumColumnMargin = 16f;

    /// <summary>The lowest allowed HUD text multiplier.</summary>
    public const float MinimumTextScale = 0.5f;

    /// <summary>The highest allowed HUD text multiplier.</summary>
    public const float MaximumTextScale = 2f;

    /// <summary>The user-selected multiplier for flight-status text.</summary>
    public static float StatusTextScale => TextScale("hud.statusTextScale");

    /// <summary>The user-selected multiplier for flight marker labels.</summary>
    public static float MarkerTextScale => TextScale("hud.markerTextScale");

    /// <summary>How much this control's viewport is shrunk by splitscreen: 1.0 for a full-screen
    /// single-player view (the pane IS the window), sqrt(½) ≈ 0.71 in a 2P pane, ½ in a 4P pane.
    /// Multiply any element that should shrink with the pane, but not vanish, by this.</summary>
    public static float PaneFactor(Control control)
    {
        float paneH = control.GetViewportRect().Size.Y;
        // The root Window's height is the real screen height even for a control living inside a
        // SubViewport (the panes are children of the same window). No stretch/content scaling is
        // configured in project.godot, so window pixels and viewport pixels are the same unit.
        float windowH = control.GetTree()?.Root?.Size.Y ?? 0f;
        if (windowH < 1f || paneH < 1f)
            return 1f;
        return Mathf.Sqrt(Mathf.Clamp(paneH / windowH, 0.01f, 1f));
    }

    /// <summary>The scale factor a HUD element should apply to its reference-space metrics:
    /// the window's height against the calibration reference, damped by the pane share.
    /// Identical to plain <c>viewportHeight / reference</c> whenever there is one full view.</summary>
    public static float Scale(Control control, float reference = ReferenceHeight)
    {
        float windowH = control.GetTree()?.Root?.Size.Y ?? 0f;
        if (windowH < 1f)
            windowH = control.GetViewportRect().Size.Y;
        return windowH / reference * PaneFactor(control);
    }

    /// <summary>The region a full-screen view's edge-anchored HUD element measures from: the
    /// reference frame's aspect at the pane's full height, centred. A view 16:9 or narrower, to
    /// within a pixel, IS the box, so an ordinary screen sees nothing move. A wider one keeps the
    /// two columns a reading width apart rather than a screen width. ⚠ Not for an off-screen
    /// marker, which belongs to the true pane edge: that is the edge it left by.</summary>
    public static Rect2 ReadingBox(Vector2 paneSize) => ReadingBox(paneSize, paneSize);

    /// <summary>The reading box of a pane inside a window. A pane that is the whole window takes
    /// <see cref="ReadingBox(Vector2)"/>'s centred frame; a splitscreen pane is its own box. A split
    /// pane is a share of a screen read edge to edge, so its columns belong at its borders.</summary>
    public static Rect2 ReadingBox(Vector2 paneSize, Vector2 windowSize)
    {
        // ⚠ A split pane is one a pixel or more short of the window on either axis. An unsized
        // window counts as full-screen, which keeps the reference placement rather than guessing.
        bool split = windowSize.X >= 1f && windowSize.Y >= 1f
            && (windowSize.X - paneSize.X >= 1f || windowSize.Y - paneSize.Y >= 1f);
        if (split)
            return new Rect2(Vector2.Zero, paneSize);

        // ⚠ A view within a pixel of the reference aspect IS the box. Shaving an odd window's
        // fraction off each side would move every dial half a pixel for nothing anyone can see.
        float width = paneSize.Y * ReferenceAspect;
        if (paneSize.X - width < 1f)
            return new Rect2(Vector2.Zero, paneSize);
        return new Rect2((paneSize.X - width) / 2f, 0f, width, paneSize.Y);
    }

    /// <summary>This control's reading box, in its own local space.</summary>
    public static Rect2 ReadingBox(Control control) =>
        ReadingBox(control.GetViewportRect().Size, control.IsInsideTree() ? control.GetTree().Root.Size : Vector2.Zero);

    /// <summary>How far outward, in reference pixels, an edge-anchored column slides on a box this
    /// wide. <paramref name="referenceMargin"/> is the column's outer edge measured from the
    /// reference frame's own edge; on a box narrower than that frame it collapses to
    /// <see cref="MinimumColumnMargin"/>, which is what puts a column at the border of a 4:3
    /// screen. Zero on a box at the reference aspect, which is every pane 16:9 and wider, so no
    /// ordinary screen and no splitscreen pane moves.</summary>
    public static float ColumnOutdent(Vector2 boxSize, float referenceMargin)
    {
        // ⚠ The same one-pixel allowance ReadingBox makes, for the same reason: a 4P grid's
        // nominally 16:9 pane is a fraction narrow, and that must not read as a narrow screen.
        float width = boxSize.Y * ReferenceAspect;
        return width - boxSize.X >= 1f ? Mathf.Max(0f, referenceMargin - MinimumColumnMargin) : 0f;
    }

    /// <summary>Constrain a user-selected HUD text multiplier to its supported range.</summary>
    public static float ClampTextScale(float scale) => Mathf.Clamp(scale, MinimumTextScale, MaximumTextScale);

    private static float TextScale(string key) => ClampTextScale(Config.GetFloat(key, 1f));
}
