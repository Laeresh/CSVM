using System;
using CSVM.Session;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>One stunt run's numbers for the wrap-up board's split table: the run itself, its total
/// and how it compares to the stored best. Read at mission end, when the run is over and the clock
/// is halted.</summary>
public readonly record struct StuntSummary(StuntMission Mission, float Total, float? PrevBest, bool NewBest);

/// <summary>
/// Instant Action's wrap-up board: the shipped screen's four rows — Time to Complete Mission,
/// Enemies Shot Down, Danger Zones Completed, Shot % — the ones <c>IA_WRAPUP.SCRIPT</c> and
/// <c>LAYOUT.CSV</c> actually wire. On a <c>stunt_flying</c> mission it also carries the run's
/// per-zone splits and its best-time row, so one board covers the mission rather than stacking
/// with <see cref="StuntScoreboard"/>. Decode: docs/formats/instant-action/wrap-up.md. Shares
/// <see cref="VersusBoard"/>'s construction, the whole window on its own <c>CanvasLayer</c>, since
/// the mission ends for every human at once. Every value is handed in by the caller at
/// <see cref="Present"/> time rather than read live off any source, the way
/// <see cref="VersusBoard"/> draws off a <see cref="VersusMatch.Standings"/> snapshot.
/// </summary>
public sealed partial class IaWrapupBoard : Control
{
    // Base metrics at 720p (scaled by window height). All TUNE — mirrors VersusBoard/StuntRaceBoard
    // so every shared results board reads as the same screen.
    private const int TitleFont = 30;
    private const int ContextFont = 15;
    private const int RowFont = 20;

    // The stunt split section's own metrics, matching StuntScoreboard's so the merged table reads
    // as the board it came from. All TUNE.
    private const int SplitHeaderFont = 14;
    private const int SplitRowFont = 17;
    private const int TotalFont = 23;
    private const int BestFont = 16;

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
    private static readonly Color SplitHeaderColor = new(0.50f, 0.62f, 0.80f);
    private static readonly Color TotalColor = new(0.96f, 0.98f, 1f);
    private static readonly Color BestColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color NewBestColor = new(1f, 0.82f, 0.28f);

    private string _context = "";
    private string _exitLabel = "";
    private PauseState _state = null!;
    private Func<int, MenuInput> _inputFor = null!;

    private CenterContainer _center = null!;
    private PanelContainer? _panel;
    private BoardMenuHost? _host;

    /// <summary>Rerun the mission in place, chosen from the menu.</summary>
    public Action? Restart { get; set; }

    /// <summary>Leave the session, chosen from the menu.</summary>
    public Action? Exit { get; set; }

    /// <summary>Hand player 1's pane to a free camera over the frozen world, chosen from the menu.
    /// The mission's halt is never dropped and no result is spent, so this is the one row that
    /// leaves the board's own state exactly as it found it.</summary>
    public Action? PhotoMode { get; set; }

    /// <summary>Builds the (hidden) board. Add it to a <c>CanvasLayer</c> above the splitscreen
    /// panes; the caller calls <see cref="Present"/> once, from
    /// <see cref="InstantActionRuntime.MissionEnded"/>.</summary>
    public static IaWrapupBoard Build(string context, bool exitsToMenu, PauseState state,
        Func<int, MenuInput> inputFor)
    {
        var board = new IaWrapupBoard
        {
            _context = context,
            _exitLabel = exitsToMenu ? "Exit to Menu" : "Quit Game",
            _state = state,
            _inputFor = inputFor,
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
        // Track the window (resizable) so the backdrop always covers it. Only the menu's own
        // Restart retires this board; a mission that has ended stays ended until then.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;

        if (Visible)
            _host?.Poll((float)delta);
    }

    /// <summary>Shows the board with the mission's four final counters
    /// (docs/formats/instant-action.md "What the four numbers count"). Called exactly once, from
    /// <see cref="InstantActionRuntime.MissionEnded"/> — every value here is that instant's
    /// snapshot, the same discipline <see cref="VersusBoard"/> takes from
    /// <see cref="VersusMatch.Standings"/>.</summary>
    public void Present(bool won, float elapsedSeconds, int enemiesShotDown, int zonesCompleted,
        int shotPercent, StuntSummary? stunt = null)
    {
        Populate(won, elapsedSeconds, enemiesShotDown, zonesCompleted, shotPercent, stunt);
        Visible = true;
        // The world stops under the board rather than flying on beneath a screen that has already
        // counted the mission. The pause key is refused while this reason is set.
        _state.Raise(HaltReason.Ended);
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

    // The stunt run's own section, in StuntScoreboard's layout: name | split | cumulative in the
    // order flown, then the total and the best-time comparison. Player 1's run — the board is
    // shared, and splitscreen pilots each fly their own copy of the zone set.
    private static void AddSplits(VBoxContainer body, StuntSummary run, float s)
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(5f * s));
        body.AddChild(grid);

        int nameW = (int)(280f * s), timeW = (int)(96f * s);
        int header = (int)(SplitHeaderFont * s), font = (int)(SplitRowFont * s);
        AddCell(grid, "ZONE", header, SplitHeaderColor, HorizontalAlignment.Left, nameW);
        AddCell(grid, "SPLIT", header, SplitHeaderColor, HorizontalAlignment.Right, timeW);
        AddCell(grid, "TIME", header, SplitHeaderColor, HorizontalAlignment.Right, timeW);

        float prev = 0f;
        int n = 1;
        foreach (var z in run.Mission.InCompletionOrder())
        {
            string name = $"{n}.  " + (z.Description.Length > 0 ? z.Description
                : z.MarkerText().Length > 0 ? z.MarkerText() : z.DzName);
            AddCell(grid, name, font, RowColor, HorizontalAlignment.Left, nameW);
            if (z.Completed)
            {
                AddCell(grid, StuntMission.FormatTime(z.CompletedAt - prev), font, RowColor,
                    HorizontalAlignment.Right, timeW);
                AddCell(grid, StuntMission.FormatTime(z.CompletedAt), font, RowColor,
                    HorizontalAlignment.Right, timeW);
                prev = z.CompletedAt;
            }
            else
            {
                AddCell(grid, "—", font, SplitHeaderColor, HorizontalAlignment.Right, timeW);
                AddCell(grid, "—", font, SplitHeaderColor, HorizontalAlignment.Right, timeW);
            }
            n++;
        }

        body.AddChild(Centered(Label($"TOTAL   {StuntMission.FormatTime(run.Total)}",
            (int)(TotalFont * s), TotalColor)));
        if (run.NewBest)
        {
            string best = run.PrevBest is { } was
                ? $"★  NEW BEST   (was {StuntMission.FormatTime(was)})"
                : "★  NEW BEST";
            body.AddChild(Centered(Label(best, (int)(BestFont * s), NewBestColor)));
        }
        else if (run.PrevBest is { } stored)
        {
            body.AddChild(Centered(Label($"BEST   {StuntMission.FormatTime(stored)}",
                (int)(BestFont * s), BestColor)));
        }
    }

    private static HSeparator Separator(float s)
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", Mathf.RoundToInt(8f * s));
        return sep;
    }

    // Nothing else retires this board: a mission that has ended stays ended, so unlike the race
    // and dogfight boards the hide and the clock release happen here rather than on a live flag.
    private void OnActivated(BoardMenuItem item)
    {
        if (item == BoardMenuItem.Photo)
        {
            // ⚠ Before the Exit test and returning: everything below this point is the Restart
            // path, which spends the halt and reruns the mission.
            PhotoMode?.Invoke();
            return;
        }
        if (item == BoardMenuItem.Exit)
        {
            Exit?.Invoke();
            return;
        }
        Visible = false;
        _host = null;
        _state.Clear(HaltReason.Ended);
        Restart?.Invoke();
    }

    private void Populate(bool won, float elapsedSeconds, int enemiesShotDown, int zonesCompleted,
        int shotPercent, StuntSummary? stunt)
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

        // The shipped screen has no outcome text; the original never lost a mission with lives
        // to run out. This stands in as the headline, in VersusBoard's shape.
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

        if (stunt is { } run)
        {
            body.AddChild(Separator(s));
            AddSplits(body, run, s);
        }

        body.AddChild(Separator(s));

        // No Resume: the mission is over and there is nothing to resume to. Player 1 drives it —
        // Restart and Exit are session-wide decisions, and no player raised this board.
        var menu = new BoardMenu(
            dismissable: false,
            (BoardMenuItem.Photo, "Photo Mode"),
            (BoardMenuItem.Restart, "Restart"),
            (BoardMenuItem.Exit, _exitLabel));
        menu.Activated += OnActivated;
        _host = BoardMenuHost.Build(menu, _inputFor(0), s);
        body.AddChild(_host.View);
    }
}
