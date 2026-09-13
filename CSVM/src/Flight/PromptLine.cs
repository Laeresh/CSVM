using Godot;

namespace CSVM.Flight;

/// <summary>A control prompt's own centred line three tenths of the way down the pane, in the
/// landings rig's pale yellow: the original's auto-dock offer, and the port's own respawn prompt on
/// the same footing. A text object of its own in the original rather than a line of the flight
/// readout block, so the anchor is a fraction of the pane and static, which lets a suite assert the
/// decode with no <see cref="Control"/> in the process. The wording is
/// <see cref="FlightHud.ComposeAutoLandPrompt"/>'s and <see cref="FlightHud.ComposeRespawnPrompt"/>'s;
/// this owns only where it sits. The respawn prompt has no decode of its own to follow, the original
/// has no such action, so it borrows this placement and the two read as one thing. Decode:
/// docs/formats/anim-definitions/cutscenes.md "The prompt's own placement".</summary>
public sealed partial class PromptLine : Control
{
    // The placement, from FUN_0045e120: x is 0.5 of the viewport width (0x006032e0) with the
    // centring flag set (the text object's +0x1044, written 1 at 0x0045d9f2), y is 0.3 of its
    // height (0x006034ac). The `autoland` font def (extracted/zrdr/fonts.zrd.json: Arial, height
    // -16, weight 700) is a display pixel at the original's 480-line mode, so its 16 px cell is
    // carried onto HudMetrics' 1440p reference by the same factor of three the message stack takes.
    private const float XFraction = 0.5f;
    private const float YFraction = 0.3f;
    private const int RefFontSize = 48;

    // Both of the text object's gradient colours are COLORREF 0x0040ffff (0x0045da03 and
    // 0x0045da08), R 255 G 255 B 64, so the line is a flat pale yellow rather than a graded one.
    private static readonly Color LineColor = new(1f, 1f, 64f / 255f);

    private string _line = string.Empty;

    /// <summary>The line to draw, or empty for none, which is what a pane being offered nothing and
    /// a seat with no binding for the prompt's action both read as.</summary>
    public string Line
    {
        get => _line;

        set
        {
            string next = value ?? string.Empty;
            if (_line == next)
            {
                return;
            }
            _line = next;
            QueueRedraw();
        }
    }

    /// <summary>Where the line is anchored in a pane of <paramref name="paneSize"/>: the horizontal
    /// centre, three tenths of the way down, the line centred on that x.
    /// ⚠ A fraction of the PANE, never of the reading box the rest of the HUD anchors to, so each
    /// splitscreen pane centres its own prompt in its own viewport.</summary>
    public static Vector2 LineAnchor(Vector2 paneSize) =>
        new(paneSize.X * XFraction, paneSize.Y * YFraction);

    public override void _Process(double delta)
    {
        // Track the pane (resizable window / splitscreen layout), the same per-frame resize
        // HudMessages does, since both anchor off the pane's own size.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    public override void _Draw()
    {
        // Same zero-size guard as the other pane HUDs: a draw can land before _Process has sized
        // this pane.
        float s = _line.Length == 0 || Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
        {
            return;
        }
        var font = GetThemeDefaultFont();
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(RefFontSize * s * HudMetrics.StatusTextScale));
        var at = LineAnchor(Size);
        float width = font.GetStringSize(_line, HorizontalAlignment.Left, -1f, fontSize).X;
        // No drop shadow, unlike every marker and the message stack: the `autoland` font def sets
        // shadow false, and the text object is drawn once (FUN_005c7f70).
        DrawString(font, new Vector2(at.X - (width / 2f), at.Y + font.GetAscent(fontSize)), _line,
            HorizontalAlignment.Left, -1f, fontSize, LineColor);
    }
}
