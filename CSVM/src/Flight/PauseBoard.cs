using System;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The shared pause board and its menu — the same WHOLE-window CanvasLayer shape as
/// <see cref="VersusBoard"/>/<see cref="StuntRaceBoard"/>, since pausing stops the game for
/// everybody at once. Names the pausing player (their own <see cref="SplitScreen.PlayerColor"/>)
/// and hands them the cursor: <see cref="PauseState"/> lets only that player resume, so only that
/// player's pad drives the menu. Everyone else's input is ignored while it is up rather than
/// fighting over a cursor whose Restart and Exit decide the whole session.
/// </summary>
public sealed partial class PauseBoard : Control
{
    // Base metrics at 720p (scaled by window height) — mirrors VersusBoard so a shared board reads
    // as the same screen whichever one is up. All TUNE.
    private const int TitleFont = 30;
    private const int ContextFont = 18;

    private static readonly Color TitleColor = new(0.93f, 0.96f, 1f);

    private PauseState _state = null!;
    private string _exitLabel = "";
    private Func<int, MenuInput> _inputFor = null!;
    private CenterContainer _center = null!;
    private PanelContainer? _panel;
    private BoardMenuHost? _host;

    /// <summary>Rerun the running mode in place, chosen from the menu.</summary>
    public Action? Restart { get; set; }

    /// <summary>Leave the session, chosen from the menu.</summary>
    public Action? Exit { get; set; }

    /// <summary>Builds the (hidden) board and subscribes to the shared pause state. Add it to a
    /// CanvasLayer above the splitscreen panes; it wakes on <see cref="PauseState.Changed"/> and
    /// hides itself the same way. <paramref name="inputFor"/> answers with a player's own menu
    /// reader, which is what binds the cursor to the pauser.</summary>
    public static PauseBoard Build(PauseState state, bool exitsToMenu, Func<int, MenuInput> inputFor)
    {
        var board = new PauseBoard
        {
            _state = state,
            _exitLabel = exitsToMenu ? "Exit to Menu" : "Quit Game",
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

        state.Changed += board.OnChanged;
        return board;
    }

    public override void _ExitTree() => _state.Changed -= OnChanged;

    public override void _Process(double delta)
    {
        // Track the window (resizable) so the backdrop always covers it — VersusBoard's same rule.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;

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
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        cc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cc.AddChild(c);
        return cc;
    }

    private void OnChanged()
    {
        if (_state.Paused)
            Populate();
        Visible = _state.Paused;
    }

    private void OnActivated(BoardMenuItem item)
    {
        switch (item)
        {
            case BoardMenuItem.Resume:
                _state.ForceResume();
                break;
            case BoardMenuItem.Restart:
                _state.ForceResume();   // the rerun runs against a live clock, not a held one
                Restart?.Invoke();
                break;
            case BoardMenuItem.Exit:
                Exit?.Invoke();
                break;
        }
    }

    private void Populate()
    {
        // Whole window, not a pane — scales on window height alone, same reason VersusBoard does.
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

        // OwnerPlayerIndex is captured once here (Populate runs only from a fresh pause), so the
        // board keeps naming the player who paused even if ownership were ever cleared underneath it.
        int owner = _state.OwnerPlayerIndex;
        string ownerTag = SplitScreen.PlayerTag(owner);
        var ownerColor = SplitScreen.PlayerColor(owner);

        body.AddChild(Centered(Label("PAUSED", (int)(TitleFont * s), TitleColor)));
        body.AddChild(Centered(Label($"{ownerTag} paused", (int)(ContextFont * s), ownerColor)));

        // A fresh menu each pause: the cursor starts on Resume, so a stray confirm on a board that
        // just appeared cannot restart or leave the session. The owner's own reader drives it.
        var menu = new BoardMenu(
            dismissable: true,
            (BoardMenuItem.Resume, "Resume"),
            (BoardMenuItem.Restart, "Restart"),
            (BoardMenuItem.Exit, _exitLabel));
        menu.Activated += OnActivated;
        menu.Dismissed += () => _state.ForceResume();
        _host = BoardMenuHost.Build(menu, _inputFor(owner), s);
        body.AddChild(_host.View);
    }
}
