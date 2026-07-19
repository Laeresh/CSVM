using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The one place the flight HUD decides how big it draws (M2.5 item 6). Every HUD element
/// (compass tape, gauge cluster, marker HUD, results scoreboard, the text block) is calibrated
/// against a 1440p reference — <c>OriginalScreenshots/HUD.png</c> — and used to scale by a plain
/// <c>viewportHeight / 1440</c>. That breaks in splitscreen: a HUD element in a quarter-height 4P
/// pane would draw at a quarter size while the pane is still half the screen WIDE, so the dials
/// shrink to unreadable dots in a lot of empty space.
///
/// <para>The rule here separates the two factors: the <b>window</b> height still sets the base
/// scale (so a 4K screen gets a big HUD and a 720p one a small one, exactly as before), and the
/// pane's share of that window is damped through a square root — a half-height 2P pane draws at
/// ~71% instead of 50%, a quarter-height 4P pane at 50% instead of 25%. Console splitscreen does
/// the same thing for the same reason. The damping exponent is TUNE; what is NOT tunable is the
/// single-player identity: with one full-screen view the pane fraction is exactly 1, so
/// <see cref="PaneFactor"/> is 1 and <see cref="Scale"/> returns the old
/// <c>viewportHeight / reference</c> unchanged — that is why enabling this could not move a
/// single-player pixel.</para>
///
/// <para>Note that damped sizes only stay on screen if positions are anchored to a pane EDGE
/// rather than scaled from the top-left: see GaugeCluster's bottom-anchored dial placement.</para>
/// </summary>
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
    /// Identical to the old <c>viewportHeight / reference</c> whenever there is one full view.</summary>
    public static float Scale(Control control, float reference = ReferenceHeight)
    {
        float windowH = control.GetTree()?.Root?.Size.Y ?? 0f;
        if (windowH < 1f)
            windowH = control.GetViewportRect().Size.Y;
        return windowH / reference * PaneFactor(control);
    }
}
