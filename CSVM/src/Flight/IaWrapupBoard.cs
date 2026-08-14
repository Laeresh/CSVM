using CSVM.Session;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Instant Action's wrap-up board (PLAN-instant-action.md G14): the shipped screen's four rows —
/// Time to Complete Mission, Enemies Shot Down, Danger Zones Completed, Shot % — the ones
/// <c>IA_WRAPUP.SCRIPT</c> and <c>LAYOUT.CSV</c> actually wire (docs/formats/instant-action.md,
/// "The wrap-up screen"; a fifth title, "Total Kills", is decoded but never wired to a row and is
/// not rendered here). Shares <see cref="VersusBoard"/>'s construction — the WHOLE window on its
/// own <c>CanvasLayer</c>, not a per-pane overlay, since the mission ends for every human at once
/// (decisions 10/14) — rather than <see cref="StuntScoreboard"/>'s per-pane shape, which is for an
/// individual pilot's own solo run.
///
/// <para>Every value is handed in by the caller at <see cref="Present"/> time rather than read
/// live off any of the sources: <c>Session.GameSession</c> owns the mission clock, the
/// kill count and the two shot counters, and this board only draws what it is given — the same
/// division <see cref="VersusBoard"/> draws with <see cref="VersusMatch.Standings"/>' snapshot.
/// Danger Zones Completed and Shot % are both a decoded "the local player" counter generalised to
/// every human for splitscreen (summed pilots' own <c>StuntMission.CompletedCount</c>; every
/// human's shooter id folded into <see cref="ProjectilePool.ScoredShooters"/>) — an extension
/// named as one, in the shape decisions 8/8a/10 already take.</para>
/// </summary>
public sealed partial class IaWrapupBoard : Control
{
    // Base metrics at 720p (scaled by window height). All TUNE — mirrors VersusBoard/StuntRaceBoard
    // so every shared results board reads as the same screen.
    private const int TitleFont = 30;
    private const int ContextFont = 15;
    private const int RowFont = 20;
    private const int FooterFont = 15;

    // The shipped row titles, langui ids 1134-1137 (docs/formats/instant-action.md "The wrap-up
    // screen") — literal text, not read off ui_strings.json at runtime: that table is a build-time
    // extraction artifact of the .rof archive, not one of the five archives a session build opens
    // (Session/SessionArchives.cs), and six lines are stable enough not to earn a reader of their
    // own here.
    private const string TimeTitle = "Time to Complete Mission";
    private const string DestroyedTitle = "Enemies Shot Down";
    private const string ZonesTitle = "Danger Zones Completed";
    private const string ShotsTitle = "Shot %";

    private static readonly Color WonColor = new(0.55f, 0.92f, 0.62f);
    private static readonly Color LostColor = new(0.92f, 0.45f, 0.45f);
    private static readonly Color ContextColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color RowColor = new(0.86f, 0.89f, 0.94f);
    private static readonly Color FooterColor = new(0.68f, 0.74f, 0.82f);

    private string _context = "";
    private string _exitHint = "";

    private CenterContainer _center = null!;
    private PanelContainer? _panel;

    /// <summary>Builds the (hidden) board. Add it to a <c>CanvasLayer</c> above the splitscreen
    /// panes; the caller calls <see cref="Present"/> once, from
    /// <see cref="InstantActionRuntime.MissionEnded"/>.</summary>
    public static IaWrapupBoard Build(string context, bool exitsToMenu)
    {
        var board = new IaWrapupBoard
        {
            _context = context,
            _exitHint = exitsToMenu ? "Esc — Menu" : "Esc — Quit",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Visible = false,
        };
        board.SetAnchorsPreset(LayoutPreset.FullRect);

        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f), MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        board.AddChild(backdrop);

        board._center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        board._center.SetAnchorsPreset(LayoutPreset.FullRect);
        board.AddChild(board._center);

        return board;
    }

    public override void _Process(double delta)
    {
        // Track the window (resizable) so the backdrop always covers it. Unlike VersusBoard/
        // StuntRaceBoard there is no rematch to retire this on — once a mission has ended it stays
        // ended — so nothing here ever re-hides the board.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    /// <summary>Shows the board with the mission's four final counters
    /// (docs/formats/instant-action.md "What the four numbers count"). Called exactly once, from
    /// <see cref="InstantActionRuntime.MissionEnded"/> — every value here is that instant's
    /// snapshot, the same discipline <see cref="VersusBoard"/> takes from
    /// <see cref="VersusMatch.Standings"/>.</summary>
    public void Present(bool won, float elapsedSeconds, int enemiesShotDown, int zonesCompleted, int shotPercent)
    {
        Populate(won, elapsedSeconds, enemiesShotDown, zonesCompleted, shotPercent);
        Visible = true;
    }

    private static Label Label(string text, int fontSize, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        l.AddThemeConstantOverride("shadow_offset_x", 1);
        l.AddThemeConstantOverride("shadow_offset_y", 1);
        return l;
    }

    private static CenterContainer Centered(Control c)
    {
        var cc = new CenterContainer();
        cc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cc.AddChild(c);
        return cc;
    }

    private static void AddCell(GridContainer grid, string text, int fontSize, Color color,
        HorizontalAlignment align, int minWidth)
    {
        var l = Label(text, fontSize, color);
        l.HorizontalAlignment = align;
        l.CustomMinimumSize = new Vector2(minWidth, 0);
        grid.AddChild(l);
    }

    private static HSeparator Separator(float s)
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", Mathf.RoundToInt(8f * s));
        return sep;
    }

    private void Populate(bool won, float elapsedSeconds, int enemiesShotDown, int zonesCompleted, int shotPercent)
    {
        // The board spans the whole window, not a pane — so it scales on the window height alone
        // (no HudMetrics pane damping, which is for HUD elements drawn inside a pane).
        float s = Mathf.Max(0.5f, Size.Y > 0f ? Size.Y / 720f : 1f);

        if (_panel != null)
        {
            _center.RemoveChild(_panel);
            _panel.QueueFree();
        }

        _panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.11f, 0.94f),
            BorderColor = new Color(0.34f, 0.48f, 0.72f),
            ContentMarginLeft = 34f * s,
            ContentMarginRight = 34f * s,
            ContentMarginTop = 24f * s,
            ContentMarginBottom = 22f * s,
        };
        style.SetBorderWidthAll(Mathf.RoundToInt(2f * s));
        style.SetCornerRadiusAll(Mathf.RoundToInt(6f * s));
        _panel.AddThemeStyleboxOverride("panel", style);
        _center.AddChild(_panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", Mathf.RoundToInt(7f * s));
        _panel.AddChild(body);

        // langui 1133 IDS_IAWU_TITLE "Instant Action" is the screen's own heading; the outcome
        // ("MISSION COMPLETE"/"MISSION FAILED") is not on the shipped screen at all — the original
        // never lost a mission with lives to run out (decision 14/15) — so it stands in as this
        // board's headline, in the shape VersusBoard's winner-name headline already takes.
        string title = won ? "MISSION COMPLETE" : "MISSION FAILED";
        var titleColor = won ? WonColor : LostColor;
        body.AddChild(Centered(Label(title, (int)(TitleFont * s), titleColor)));
        body.AddChild(Centered(Label(_context, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);

        int labelW = (int)(280f * s), valueW = (int)(90f * s);
        int font = (int)(RowFont * s);
        AddCell(grid, TimeTitle, font, RowColor, HorizontalAlignment.Left, labelW);
        AddCell(grid, InstantActionRuntime.FormatElapsed(elapsedSeconds), font, RowColor,
            HorizontalAlignment.Right, valueW);
        AddCell(grid, DestroyedTitle, font, RowColor, HorizontalAlignment.Left, labelW);
        AddCell(grid, enemiesShotDown.ToString(), font, RowColor, HorizontalAlignment.Right, valueW);
        AddCell(grid, ZonesTitle, font, RowColor, HorizontalAlignment.Left, labelW);
        AddCell(grid, zonesCompleted.ToString(), font, RowColor, HorizontalAlignment.Right, valueW);
        AddCell(grid, ShotsTitle, font, RowColor, HorizontalAlignment.Left, labelW);
        AddCell(grid, $"{shotPercent}%", font, RowColor, HorizontalAlignment.Right, valueW);

        body.AddChild(Separator(s));
        body.AddChild(Centered(Label(_exitHint, (int)(FooterFont * s), FooterColor)));
    }
}
