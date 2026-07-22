using Godot;

namespace CSVM.Flight;

/// <summary>
/// The shared results board for a splitscreen stunt race (M2.5 item 7). Where the single-player
/// <see cref="StuntScoreboard"/> shows one pilot's per-zone splits inside their own pane, this
/// ranks the whole field and covers the <b>entire window</b> — it goes on its own CanvasLayer over
/// the splitscreen panes, not into a SubViewport, because the race ends for everybody at once.
///
/// <para>One row per player in finishing order: placing, their colour-coded tag, aircraft, zones
/// cleared and total time (plus the gap to the winner). The winner's row is highlighted in their
/// own identity colour, the same one they carried through the launchscreen's join strip and plane
/// select. R (any player's respawn button) is a rematch — fresh clocks and zones for everyone,
/// same aircraft and spawns; Esc leaves.</para>
///
/// <para>Best times are deliberately not recorded here (<see cref="ScoreStore"/> is
/// single-player-only): race totals aren't comparable across player counts, and each player starts
/// from a different mission spawn point.</para>
/// </summary>
public sealed partial class StuntRaceBoard : Control
{
    private StuntRace _race = null!;
    private string _context = "";
    private string _exitHint = "";

    private CenterContainer _center = null!;
    private PanelContainer? _panel;

    // Base metrics at 720p (scaled by window height). All TUNE — mirrors StuntScoreboard so the
    // solo and race boards read as the same screen.
    private const int TitleFont = 26;
    private const int ContextFont = 15;
    private const int HeaderFont = 14;
    private const int RowFont = 18;
    private const int FooterFont = 15;

    private static readonly Color TitleColor = new(0.93f, 0.96f, 1f);
    private static readonly Color ContextColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color HeaderColor = new(0.50f, 0.62f, 0.80f);
    private static readonly Color RowColor = new(0.86f, 0.89f, 0.94f);
    private static readonly Color FooterColor = new(0.68f, 0.74f, 0.82f);

    /// <summary>Builds the (hidden) board and subscribes to the race's completion. Add it to a
    /// CanvasLayer above the splitscreen panes; it wakes itself on
    /// <see cref="StuntRace.RaceCompleted"/> and retires on a rematch.</summary>
    public static StuntRaceBoard Build(StuntRace race, string context, bool exitsToMenu)
    {
        var board = new StuntRaceBoard
        {
            _race = race,
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

        race.RaceCompleted += board.OnRaceCompleted;
        return board;
    }

    public override void _ExitTree() => _race.RaceCompleted -= OnRaceCompleted;

    public override void _Process(double delta)
    {
        // Track the window (resizable) so the backdrop always covers it.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        // A rematch clears the placings — retire the board until the next race ends.
        if (Visible && !_race.AllFinished)
            Visible = false;
    }

    private void OnRaceCompleted()
    {
        // Log the final order too, so a race is reviewable from a headless run's log.
        GD.Print("stunt race results:");
        foreach (var r in _race.Standings())
            GD.Print($"  {StuntRace.Ordinal(r.Rank)}  {r.Tag}  {r.PlaneDisplay}  " +
                     $"{StuntMission.FormatTime(r.FinishTime)}");
        Populate();
        Visible = true;
    }

    private void Populate()
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
            ContentMarginLeft = 34f * s, ContentMarginRight = 34f * s,
            ContentMarginTop = 24f * s, ContentMarginBottom = 22f * s,
        };
        style.SetBorderWidthAll(Mathf.RoundToInt(2f * s));
        style.SetCornerRadiusAll(Mathf.RoundToInt(6f * s));
        _panel.AddThemeStyleboxOverride("panel", style);
        _center.AddChild(_panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", Mathf.RoundToInt(7f * s));
        _panel.AddChild(body);

        body.AddChild(Centered(Label("STUNT RACE RESULTS", (int)(TitleFont * s), TitleColor)));
        body.AddChild(Centered(Label(_context, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));

        // One row per player: placing | tag | aircraft | zones | total (+ gap to the winner).
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(22f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);

        int rankW = (int)(52f * s), tagW = (int)(46f * s), planeW = (int)(180f * s),
            zonesW = (int)(76f * s), timeW = (int)(120f * s);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, rankW);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, tagW);
        AddCell(grid, "AIRCRAFT", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, planeW);
        AddCell(grid, "ZONES", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, zonesW);
        AddCell(grid, "TOTAL", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, timeW);

        float winnerTime = 0f;
        bool haveWinner = false;
        foreach (var r in _race.Standings())
        {
            bool won = r.Finished && r.Rank == 1;
            if (won)
            {
                winnerTime = r.FinishTime;
                haveWinner = true;
            }
            // The winner's row wears their own identity colour; everyone else stays neutral so the
            // placing reads at a glance.
            var color = won ? r.Color : RowColor;
            int font = (int)(RowFont * s);
            AddCell(grid, r.Finished ? StuntRace.Ordinal(r.Rank) : "—", font, color, HorizontalAlignment.Left, rankW);
            AddCell(grid, r.Tag, font, r.Color, HorizontalAlignment.Left, tagW);
            AddCell(grid, r.PlaneDisplay, font, color, HorizontalAlignment.Left, planeW);
            AddCell(grid, $"{r.Mission.CompletedCount}/{r.Mission.TotalCount}", font, color,
                HorizontalAlignment.Right, zonesW);
            string time = r.Finished
                ? StuntMission.FormatTime(r.FinishTime)
                    + (haveWinner && !won ? $"  (+{StuntMission.FormatTime(r.FinishTime - winnerTime)})" : "")
                : "DNF";
            AddCell(grid, time, font, color, HorizontalAlignment.Right, timeW);
        }

        body.AddChild(Separator(s));
        body.AddChild(Centered(Label($"R — Rematch        {_exitHint}", (int)(FooterFont * s), FooterColor)));
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
}
