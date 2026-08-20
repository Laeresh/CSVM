using System.Linq;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The shared results board for splitscreen "Dogfight" — mirrors
/// <see cref="StuntRaceBoard"/> almost exactly: the match ends for everybody at once, so this
/// covers the WHOLE window on its own CanvasLayer over the splitscreen panes, not a per-pane
/// overlay. Winner (or "DRAW" on a tie) on top, then one ranked row per player — tag, kills,
/// deaths — in their own identity colour; the winner's row is highlighted the same way the race
/// board highlights first place. The world halts underneath it, and the board's own menu offers
/// the rematch (every score and the clock reset, every plane respawns) and the way out. R and pad
/// Y still reach the rematch directly, for the muscle memory and for the hold-test harness.
/// </summary>
public sealed partial class VersusBoard : Control
{
    // Base metrics at 720p (scaled by window height). All TUNE — mirrors StuntRaceBoard so the
    // two shared boards read as the same screen.
    private const int TitleFont = 30;
    private const int ContextFont = 15;
    private const int HeaderFont = 14;
    private const int RowFont = 20;

    private static readonly Color TitleColor = new(0.93f, 0.96f, 1f);
    private static readonly Color ContextColor = new(0.60f, 0.75f, 0.95f);
    private static readonly Color HeaderColor = new(0.50f, 0.62f, 0.80f);
    private static readonly Color RowColor = new(0.86f, 0.89f, 0.94f);

    private VersusMatch _match = null!;
    private string _context = "";
    private string _exitLabel = "";
    private PauseState _state = null!;
    private System.Func<int, MenuInput> _inputFor = null!;

    private CenterContainer _center = null!;
    private PanelContainer? _panel;
    private BoardMenuHost? _host;

    /// <summary>Rerun the match in place, chosen from the menu.</summary>
    public System.Action? Restart { get; set; }

    /// <summary>Leave the session, chosen from the menu.</summary>
    public System.Action? Exit { get; set; }

    /// <summary>Hand player 1's pane to a free camera over the frozen world, chosen from the menu.
    /// The match's halt is never dropped and no standing is spent, so this is the one row that
    /// leaves the board's own state exactly as it found it.</summary>
    public System.Action? PhotoMode { get; set; }

    /// <summary>Builds the (hidden) board and subscribes to the match's completion. Add it to a
    /// CanvasLayer above the splitscreen panes; it wakes itself on
    /// <see cref="VersusMatch.MatchCompleted"/> and retires on a rematch.</summary>
    public static VersusBoard Build(VersusMatch match, string context, bool exitsToMenu,
        PauseState state, System.Func<int, MenuInput> inputFor)
    {
        var board = new VersusBoard
        {
            _match = match,
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

        match.MatchCompleted += board.OnMatchCompleted;
        return board;
    }

    public override void _ExitTree() => _match.MatchCompleted -= OnMatchCompleted;

    public override void _Process(double delta)
    {
        // Track the window (resizable) so the backdrop always covers it.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        // A rematch clears Completed — retire the board and release the clock until the next
        // match ends. R and pad Y reach the rematch without going through the menu, so the release
        // belongs here rather than only on the menu's own Restart.
        if (Visible && !_match.Completed)
        {
            Visible = false;
            _host = null;
            _state.Clear(HaltReason.Ended);
            return;
        }
        if (Visible)
            _host?.Poll((float)delta);
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

    private void OnActivated(BoardMenuItem item)
    {
        if (item == BoardMenuItem.Photo)
        {
            // ⚠ Ahead of the Exit test and returning: the tail of this method is the Restart path.
            PhotoMode?.Invoke();
            return;
        }
        if (item == BoardMenuItem.Exit)
        {
            Exit?.Invoke();
            return;
        }
        // The rematch clears Completed, which _Process turns into the hide and the release.
        Restart?.Invoke();
    }

    private void OnMatchCompleted()
    {
        // Log the final standings too, so a match is reviewable from a headless run's log.
        Log.Info("flight", $"dogfight results:");
        foreach (var st in _match.Standings())
            Log.Info("flight",
                $"  #{st.Rank}  {SplitScreen.PlayerTag(st.PlayerIndex)}  {st.Kills}K/{st.Deaths}D");
        Populate();
        Visible = true;
        // The match stops the world now. It used to keep running underneath, which left the losers
        // flying around a scoreboard that had already counted them.
        _state.Raise(HaltReason.Ended);
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

        // Captured once here so a later Restart() zeroing the live match never rebuilds these
        // already-drawn labels.
        var standings = _match.Standings().ToList();
        var winners = standings.Where(st => st.Rank == 1).ToList();
        string title = winners.Count == 1 ? $"{SplitScreen.PlayerTag(winners[0].PlayerIndex)} WINS" : "DRAW";
        var titleColor = winners.Count == 1 ? SplitScreen.PlayerColor(winners[0].PlayerIndex) : TitleColor;

        body.AddChild(Centered(Label(title, (int)(TitleFont * s), titleColor)));
        body.AddChild(Centered(Label(_context, (int)(ContextFont * s), ContextColor)));
        body.AddChild(Separator(s));

        // One row per player: placing | tag | kills | deaths.
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(26f * s));
        grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(6f * s));
        body.AddChild(grid);

        int rankW = (int)(52f * s), tagW = (int)(56f * s), killsW = (int)(80f * s), deathsW = (int)(80f * s);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, rankW);
        AddCell(grid, "", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Left, tagW);
        AddCell(grid, "KILLS", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, killsW);
        AddCell(grid, "DEATHS", (int)(HeaderFont * s), HeaderColor, HorizontalAlignment.Right, deathsW);

        foreach (var st in standings)
        {
            bool won = st.Rank == 1 && winners.Count == 1;
            // The winner's row wears their own identity colour; everyone else stays neutral so
            // the placing reads at a glance — same rule StuntRaceBoard's winner row follows.
            var color = won ? SplitScreen.PlayerColor(st.PlayerIndex) : RowColor;
            int font = (int)(RowFont * s);
            AddCell(grid, $"#{st.Rank}", font, color, HorizontalAlignment.Left, rankW);
            AddCell(grid, SplitScreen.PlayerTag(st.PlayerIndex), font, SplitScreen.PlayerColor(st.PlayerIndex),
                HorizontalAlignment.Left, tagW);
            AddCell(grid, st.Kills.ToString(), font, color, HorizontalAlignment.Right, killsW);
            AddCell(grid, st.Deaths.ToString(), font, color, HorizontalAlignment.Right, deathsW);
        }

        body.AddChild(Separator(s));

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
