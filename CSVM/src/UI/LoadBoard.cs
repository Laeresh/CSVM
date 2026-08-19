using Godot;

namespace CSVM.UI;

/// <summary>
/// The load screen drawn over the whole window while a session builds — the same board style as
/// the pause and results boards, with no menu on it. A build is one synchronous block that stalls
/// the frame loop for a second or two, so nothing can be drawn DURING it; the Launcher shows this,
/// lets one frame render, and builds on the next tick.
/// ⚠ Deliberately has no progress: the build reports its phases only after the fact
/// (<c>StartupProfile</c>), so a bar here would be a fiction. The original's own load screen,
/// its lamp bar included, is artwork in <c>extracted/rimage/</c> and is not matched yet; see
/// the backlog.
/// </summary>
public sealed partial class LoadBoard : Control
{
    // Base metrics at 720p (scaled by window height), mirroring PauseBoard so the load screen
    // reads as the same family of screen. All TUNE.
    private const int TitleFont = 30;
    private const int SubjectFont = 18;

    private static readonly Color TitleColor = new(0.93f, 0.96f, 1f);
    private static readonly Color SubjectColor = new(0.66f, 0.74f, 0.88f);

    private string _subject = "";

    /// <summary>Builds the board naming what is being loaded (a chapter and mode line, or empty
    /// for no second line). Opaque rather than translucent: the outgoing session is still in the
    /// tree for this one frame, and a half-seen dead world is worse than a plain screen.</summary>
    public static LoadBoard Build(string subject)
    {
        var board = new LoadBoard
        {
            _subject = subject,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        board.SetAnchorsPreset(LayoutPreset.FullRect);
        return board;
    }

    /// <summary>Populates on entry rather than in <see cref="Build"/>: the text is sized off the
    /// viewport, which a node outside the tree cannot read.</summary>
    public override void _Ready()
    {
        var backdrop = new ColorRect
        {
            Color = new Color(0.03f, 0.04f, 0.07f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        // Sized off the window height alone, the whole-window rule every shared board follows.
        float height = GetViewportRect().Size.Y;
        float s = Mathf.Max(0.5f, height > 0f ? height / 720f : 1f);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", Mathf.RoundToInt(7f * s));
        center.AddChild(body);

        body.AddChild(Centered(Label("LOADING", (int)(TitleFont * s), TitleColor)));
        if (_subject.Length > 0)
        {
            body.AddChild(Centered(Label(_subject, (int)(SubjectFont * s), SubjectColor)));
        }
    }

    public override void _Process(double delta)
    {
        // Track the window (resizable) so the backdrop always covers it — PauseBoard's same rule.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    private static Label Label(string text, int fontSize, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static CenterContainer Centered(Control c)
    {
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        cc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cc.AddChild(c);
        return cc;
    }
}
