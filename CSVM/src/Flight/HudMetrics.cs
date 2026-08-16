using Godot;

namespace CSVM.Flight;

/// <summary>The one place the flight HUD decides how big it draws (docs/architecture.md). The
/// window height sets the base scale against a 1440p reference; a splitscreen pane's share of
/// that window is damped through a square root, confirmed at the controls (<c>BL-126</c>). A
/// full-screen single-player view has <see cref="PaneFactor"/> exactly 1, so <see cref="Scale"/>
/// returns the plain height ratio unchanged. Damped sizes stay on screen only if positions anchor
/// to a pane EDGE, not the top-left.</summary>
public static class HudMetrics
{
    /// <summary>The reference viewport height every HUD metric was measured at (HUD.png).</summary>
    public const float ReferenceHeight = 1440f;

    /// <summary>How much this control's viewport is shrunk by splitscreen: 1.0 for a full-screen
    /// single-player view (the pane IS the window), sqrt(½) ≈ 0.71 in a 2P pane, ½ in a 4P pane.
    /// Multiply any element that should shrink with the pane — but not vanish — by this.</summary>
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
}
