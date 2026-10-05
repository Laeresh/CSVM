using System;
using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original presentation's end-of-race board, in the Built-in <c>StuntRaceBoard</c>'s place over
/// the whole window. It wakes on <see cref="StuntRace.RaceCompleted"/> and halts the sim, and it
/// retires once a new window clears <see cref="StuntRace.Ended"/>. It draws
/// <see cref="OriginalRaceResults"/>' composition through <see cref="ComposedBoardView"/>, frozen at
/// the race's end. Player 1's reader steps Photo Mode, Restart and the exit with any arrow, and
/// player 1's pointer shares the cursor on <see cref="BoardMenuPointer"/>'s rule. A network guest's
/// board withholds Restart.
/// Module entry: docs/architecture/UI.md on src/UI/Menu/Original/OriginalRaceBoard.cs.
/// </summary>
public sealed partial class OriginalRaceBoard : Control
{
    private readonly BoardMenuPointer _pointer = new() { SettlesFirstSight = true };
    private readonly List<string> _rows = new();

    private StuntRace _race = null!;
    private IReadOnlyList<string> _zoneNames = Array.Empty<string>();
    private string _context = "";
    private string _exitLabel = "";
    private PauseState _state = null!;
    private Func<int, MenuInput> _inputFor = null!;
    private string _dataRoot = "";
    private UiStrings _strings = UiStrings.Empty;
    private ComposedBoardView? _view;
    private RaceResultsSheet? _sheet;
    private BoardMenu? _menu;
    private MenuInput? _input;

    /// <summary>A new window in place, chosen from Restart (R and pad Y reach it without the board).
    /// </summary>
    public Action? Restart { get; set; }

    /// <summary>Leave the session, chosen from the exit row.</summary>
    public Action? Exit { get; set; }

    /// <summary>Hand player 1's pane to a free camera over the frozen world, chosen from Photo Mode.
    /// </summary>
    public Action? PhotoMode { get; set; }

    /// <summary>A network guest's line in place of the Restart plaque, a new window being the host's
    /// to call; null where Restart stands. Read at the race's end.</summary>
    public string? RestartWithheld { get; set; }

    /// <summary>The pointer in the board's authored 800x600 space and whether its button is down, or
    /// null for none this frame. Defaults to player 1's mouse; a suite replaces it.</summary>
    public Func<(float X, float Y, bool Pressed)?> PointerSource { get; set; } = () => null;

    /// <summary>The ranked rows as text, one line per pilot, for the suites and the log.</summary>
    internal IReadOnlyList<string> Rows => _rows;

    /// <summary>What the board froze at the race's end, null before it.</summary>
    internal RaceResultsSheet? Sheet => _sheet;

    /// <summary>The screen as last composed, for a suite that reads what is drawn.</summary>
    internal ComposedBoard? Shown => _view?.Board;

    /// <summary>The menu while the board is up, the suites' way to drive it without a device.</summary>
    internal BoardMenu? Menu => _menu;

    /// <summary>Builds the (hidden) board and subscribes to the race's end. Add it to a CanvasLayer
    /// above the panes. <paramref name="zoneNames"/> names the course's zones in course order.
    /// <paramref name="exitLabel"/> is the exit row's words.</summary>
    public static OriginalRaceBoard Build(StuntRace race, IReadOnlyList<string> zoneNames, string context,
        string exitLabel, PauseState state, Func<int, MenuInput> inputFor, string dataRoot, UiStrings strings)
    {
        ArgumentNullException.ThrowIfNull(race);
        var board = new OriginalRaceBoard
        {
            _race = race,
            _zoneNames = zoneNames,
            _context = context,
            _exitLabel = exitLabel,
            _state = state,
            _inputFor = inputFor,
            _dataRoot = dataRoot,
            _strings = strings,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Visible = false,
        };
        board.PointerSource = board.SeatPointer;
        board.SetAnchorsPreset(LayoutPreset.FullRect);
        race.RaceCompleted += board.OnRaceCompleted;
        return board;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        _view = ComposedBoardView.Build(_dataRoot);
        AddChild(_view);
        if (_sheet != null && Visible)
        {
            Compose();
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree() => _race.RaceCompleted -= OnRaceCompleted;

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;

        // A new window clears the race's end, which retires the board and releases the clock. R and
        // pad Y reach the rerun without the menu this way.
        if (Visible && !_race.Ended)
        {
            Retire();
            return;
        }

        if (!Visible || _menu == null || _input == null)
        {
            return;
        }

        _input.Poll((float)delta);
        bool changed = _menu.Handle(Math.Sign(_input.Move + _input.MoveX), _input.Accept, false);
        if (Visible)
        {
            changed |= _pointer.Step(_menu, PointerSource(), (x, y) => _sheet is { } sheet ? OriginalRaceResults.MenuRowAt(sheet, x, y) : -1);
        }

        if (changed && Visible)
        {
            Compose();
        }
    }

    private void OnRaceCompleted()
    {
        _sheet = RaceResultsSheet.Of(_race, _zoneNames, _context, _exitLabel, RestartWithheld);
        _rows.Clear();
        foreach (var row in _sheet.Standings)
        {
            _rows.Add($"{row.Pilot}  {row.Aircraft}  {row.Best}  {row.Gap}  {row.Runs}");
        }

        Log.Info("flight", $"stunt race results (original board):");
        foreach (var row in _rows)
        {
            Log.Info("flight", $"  {row}");
        }

        // A fresh menu each end, so the cursor starts on Photo Mode. A stray confirm on a board that
        // just appeared then neither restarts nor leaves.
        var menu = RestartWithheld != null
            ? new BoardMenu(
                dismissable: false,
                (BoardMenuItem.Photo, OriginalRaceResults.PhotoLabel),
                (BoardMenuItem.Exit, _exitLabel))
            : new BoardMenu(
                dismissable: false,
                (BoardMenuItem.Photo, OriginalRaceResults.PhotoLabel),
                (BoardMenuItem.Restart, OriginalRaceResults.RestartLabel),
                (BoardMenuItem.Exit, _exitLabel));
        menu.Activated += OnActivated;
        _menu = menu;
        _input = _inputFor(0);
        _input.Prime();
        _pointer.Prime(PointerSource()?.Pressed ?? false);
        Visible = true;
        _state.Raise(HaltReason.Ended);
        Compose();
    }

    private void OnActivated(BoardMenuItem item)
    {
        switch (item)
        {
            case BoardMenuItem.Photo:
                PhotoMode?.Invoke();
                break;
            case BoardMenuItem.Restart:
                Restart?.Invoke();
                break;
            case BoardMenuItem.Exit:
                Exit?.Invoke();
                break;
        }
    }

    private void Retire()
    {
        Visible = false;
        _menu = null;
        _input = null;
        _state.Clear(HaltReason.Ended);
    }

    // Player 1's mouse in authored pixels, only where player 1 reads the keyboard (MenuInput's rule
    // for seat 0). A pad player's board is driven by the pad alone.
    private (float X, float Y, bool Pressed)? SeatPointer()
    {
        if (_input is not { Keyboard: true } || !IsInsideTree())
        {
            return null;
        }

        var size = GetViewportRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        var at = GetViewport().GetMousePosition();
        return ((at.X - fit.OriginX) / fit.Scale, (at.Y - fit.OriginY) / fit.Scale, Input.IsMouseButtonPressed(MouseButton.Left));
    }

    private void Compose()
    {
        if (_sheet != null)
        {
            _view?.Show(OriginalRaceResults.Compose(_sheet, _strings, _menu?.Index ?? 0, _pointer.Held),
                BoardPalette.Paper, string.Empty, string.Empty);
        }
    }
}
