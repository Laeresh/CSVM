using Godot;

namespace CSVM.Flight;

/// <summary>
/// The end-of-run results overlay for Stunt Flying. When the run's last
/// Danger Zone is cleared (<see cref="StuntMission.RunCompleted"/>) this shows a centred panel:
/// the zones in the order they were flown with per-zone split + cumulative times, the total, the
/// plane + chapter, and a best-time comparison (NEW BEST when the total beats the stored record).
/// Best times persist per <c>chapter/mission/plane</c> in <see cref="ScoreStore"/>.
///
/// It is a plain Godot-UI overlay (panel + containers + labels), distinct from the in-flight
/// HUD's hand-drawn marker/dials — a modal results screen, not a flight instrument. R starts a
/// fresh run (FlightController resets the mission + respawns), Esc quits (Launcher's global
/// handler; a menu-launched flight returns to the launchscreen instead). The panel hides itself the moment
/// the run is no longer complete (i.e. after a restart), so the next run's board is clean.
/// </summary>
public sealed partial class StuntScoreboard : Control
{
    private StuntMission _mission = null!;
    private ScoreStore _store = null!;
    private string _scoreKey = "";
    private string _planeDisplay = "";
    private string _context = "";

    private CenterContainer _center = null!;
    private PanelContainer? _panel;

    // Base metrics at 720p (the default window); scaled up on taller viewports so the board reads
    // at 1080p/1440p/4K without ballooning. All TUNE.
    private const int TitleFont = 26;
    private const int ContextFont = 15;
    private const int HeaderFont = 14;
    private const int RowFont = 17;
    private const int TotalFont = 23;
    private const int BestFont = 16;
    private const int FooterFont = 15;

    private static readonly Color TitleColor = new(0.93f, 0.96f, 1f);
    private static readonly Color ContextColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color HeaderColor = new(0.50f, 0.62f, 0.80f);
    private static readonly Color RowColor = new(0.86f, 0.89f, 0.94f);
    private static readonly Color TotalColor = new(0.96f, 0.98f, 1f);
    private static readonly Color BestColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color NewBestColor = new(1f, 0.82f, 0.28f);
    private static readonly Color FooterColor = new(0.68f, 0.74f, 0.82f);

    /// <summary>Builds the (hidden) overlay and subscribes to the run's completion. Add it to the
    /// HUD canvas last so it draws over the marker/dials; feed nothing per-frame — it wakes itself
    /// on <see cref="StuntMission.RunCompleted"/>.</summary>
    public static StuntScoreboard Build(StuntMission mission, string planeDisplay, string context,
        ScoreStore store, string scoreKey)
    {
        var board = new StuntScoreboard
        {
            _mission = mission,
            _store = store,
            _scoreKey = scoreKey,
            _planeDisplay = planeDisplay,
            _context = context,
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

        mission.RunCompleted += board.OnRunCompleted;
        return board;
    }

    public override void _ExitTree() => _mission.RunCompleted -= OnRunCompleted;

    public override void _Process(double delta)
    {
        // Track the viewport so the backdrop covers a resized window.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        // A restart (StuntMission.Reset) clears AllComplete — retire the board until the next run.
        if (Visible && !_mission.AllComplete)
            Visible = false;
    }

    private void OnRunCompleted()
    {
        float total = _mission.Elapsed;
        float? prevBest = _store.GetBest(_scoreKey);
        bool newBest = _store.RecordIfBest(_scoreKey, total);
        GD.Print($"stunt: run complete {StuntMission.FormatTime(total)}"
            + (newBest ? " — NEW BEST" : prevBest.HasValue ? $" (best {StuntMission.FormatTime(prevBest.Value)})" : ""));
        // Log the split table too (the splits are otherwise only visible on the rendered board —
        // this makes a run's scoring reviewable from the headless log).
        float prev = 0f;
        int n = 1;
        foreach (var z in _mission.InCompletionOrder())
        {
            string name = z.Description.Length > 0 ? z.Description : z.DzName;
            GD.Print($"  split {n}. {name}: +{StuntMission.FormatTime(z.CompletedAt - prev)}"
                + $" (@ {StuntMission.FormatTime(z.CompletedAt)})");
            prev = z.CompletedAt;
            n++;
        }
        Populate(total, prevBest, newBest);
        Visible = true;
    }

    private void Populate(float total, float? prevBest, bool newBest)
    {
        // 720p-referenced metrics, damped by the pane share so the panel still fits inside a
        // splitscreen pane (HudMetrics; identical to the old Max(1, h/720) at any full-screen
        // view 720p or taller, which is every real window).
        float s = Mathf.Max(0.5f, HudMetrics.Scale(this, 720f));

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

        body.AddChild(Centered(Label("STUNT FLYING COMPLETE", (int)(TitleFont * s), TitleColor)));
        string ctx = _context.Length > 0 ? $"{_context}   ·   {_planeDisplay}" : _planeDisplay;
        body.AddChild(Centered(Label(ctx, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));

        // Zone table: name | split | cumulative, in the order the zones were flown.
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(5f * s));
        body.AddChild(grid);

        int nameW = (int)(280f * s), timeW = (int)(96f * s);
        AddCell(grid, "ZONE", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, nameW);
        AddCell(grid, "SPLIT", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, timeW);
        AddCell(grid, "TIME", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, timeW);

        float prev = 0f;
        int n = 1;
        foreach (var z in _mission.InCompletionOrder())
        {
            string name = $"{n}.  " + (z.Description.Length > 0 ? z.Description
                : z.MarkerText().Length > 0 ? z.MarkerText() : z.DzName);
            AddCell(grid, name, (int)(RowFont * s), RowColor, HorizontalAlignment.Left, nameW);
            if (z.Completed)
            {
                AddCell(grid, StuntMission.FormatTime(z.CompletedAt - prev), (int)(RowFont * s), RowColor,
                    HorizontalAlignment.Right, timeW);
                AddCell(grid, StuntMission.FormatTime(z.CompletedAt), (int)(RowFont * s), RowColor,
                    HorizontalAlignment.Right, timeW);
                prev = z.CompletedAt;
            }
            else
            {
                AddCell(grid, "—", (int)(RowFont * s), HeaderColor, HorizontalAlignment.Right, timeW);
                AddCell(grid, "—", (int)(RowFont * s), HeaderColor, HorizontalAlignment.Right, timeW);
            }
            n++;
        }

        body.AddChild(Separator(s));
        body.AddChild(Centered(Label($"TOTAL   {StuntMission.FormatTime(total)}", (int)(TotalFont * s), TotalColor)));

        if (newBest)
        {
            string best = prevBest.HasValue
                ? $"★  NEW BEST   (was {StuntMission.FormatTime(prevBest.Value)})"
                : "★  NEW BEST";
            body.AddChild(Centered(Label(best, (int)(BestFont * s), NewBestColor)));
        }
        else if (prevBest.HasValue)
        {
            body.AddChild(Centered(Label($"BEST   {StuntMission.FormatTime(prevBest.Value)}", (int)(BestFont * s), BestColor)));
        }

        body.AddChild(Separator(s));
        body.AddChild(Centered(Label("R — New Run        Esc — Quit", (int)(FooterFont * s), FooterColor)));
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

    /// <summary>Wraps a label in a CenterContainer so it centres in the VBox's full width.</summary>
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
