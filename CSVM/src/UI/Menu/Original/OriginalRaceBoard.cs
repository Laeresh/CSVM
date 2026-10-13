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
/// the race's end. Player 1's reader steps Photo Mode, Restart, the exit and any scroll arrows, the
/// pointer sharing that cursor on <see cref="BoardMenuPointer"/>'s rule. A network guest's board withholds Restart.
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
    private int _scoresTop;
    private int _splitsTop;

    // A held thumb: which list, where the pointer took it and the window's top then.
    private (bool Splits, float StartY, int StartTop)? _drag;
    private bool _wasPressed;

    // Wheel steps turned since the board last read them, positive toward a list's foot. The wheel is
    // an event and never a held state, so it is counted rather than polled.
    private int _wheel;

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

    /// <summary>The wheel steps turned since the last frame, positive toward a list's foot. Defaults
    /// to the board's own count of the mouse wheel; a suite replaces it.</summary>
    public Func<int> WheelSource { get; set; } = () => 0;

    /// <summary>The first row each list shows, the standings' and the splits', for the suites.</summary>
    internal (int Scores, int Splits) Tops => (_scoresTop, _splitsTop);

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
        board.WheelSource = board.TakeWheel;
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
    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventMouseButton { Pressed: true } button)
        {
            _wheel += button.ButtonIndex == MouseButton.WheelDown ? 1
                : button.ButtonIndex == MouseButton.WheelUp ? -1 : 0;
        }
    }

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
            var pointer = PointerSource();
            bool dragging = ScrollByPointer(pointer, WheelSource(), ref changed);
            changed |= _pointer.Step(_menu, pointer,
                (x, y) => !dragging && _sheet is { } sheet ? OriginalRaceResults.MenuRowAt(sheet, x, y) : -1);
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
        // just appeared then neither restarts nor leaves. A scrolling list's arrows follow the plaques.
        var items = new List<(BoardMenuItem, string)>();
        foreach (int slot in OriginalRaceResults.Slots(_sheet))
        {
            items.Add(slot switch
            {
                OriginalRaceResults.PhotoRow => (BoardMenuItem.Photo, OriginalRaceResults.PhotoLabel),
                OriginalRaceResults.RestartRow => (BoardMenuItem.Restart, OriginalRaceResults.RestartLabel),
                OriginalRaceResults.ExitRow => (BoardMenuItem.Exit, _exitLabel),
                _ => (BoardMenuItem.Scroll, string.Empty),
            });
        }

        _scoresTop = 0;
        _splitsTop = 0;
        _drag = null;
        _wheel = 0;
        var menu = new BoardMenu(dismissable: false, items.ToArray());
        menu.Activated += OnActivated;
        _menu = menu;
        _input = _inputFor(0);
        _input.Prime();
        _wasPressed = PointerSource()?.Pressed ?? false;
        _pointer.Prime(_wasPressed);
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
            case BoardMenuItem.Scroll when _sheet != null && _menu != null:
                (_scoresTop, _splitsTop) = OriginalRaceResults.Scrolled(_sheet,
                    OriginalRaceResults.Slots(_sheet)[_menu.Index], _scoresTop, _splitsTop);
                Compose();
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

    // The thumb and the wheel, the Original shell's rule on each list's ListWindow. A click on a
    // thumb holds it while the button stays down, the window following the pointer down the track.
    // A wheel step over a list moves it by rows. True while a thumb holds.
    private bool ScrollByPointer((float X, float Y, bool Pressed)? pointer, int wheel, ref bool changed)
    {
        bool clicked = pointer is { Pressed: true } && !_wasPressed;
        _wasPressed = pointer is { Pressed: true };
        if (pointer is not { } at || _sheet == null)
        {
            _drag = null;
            return false;
        }

        if (_drag is { } drag)
        {
            if (!at.Pressed)
            {
                _drag = null;
                return false;
            }

            var held = OriginalRaceResults.Window(_sheet, drag.Splits, drag.Splits ? _splitsTop : _scoresTop);
            changed |= ScrollTo(drag.Splits, held.TopAfterDrag(drag.StartTop, at.Y - drag.StartY));
            return true;
        }

        foreach (bool splits in new[] { false, true })
        {
            var window = OriginalRaceResults.Window(_sheet, splits, splits ? _splitsTop : _scoresTop);
            if (clicked && window.OnThumb(at.X, at.Y))
            {
                _drag = (splits, at.Y, window.Top);
                return true;
            }

            if (wheel != 0 && window.Contains(at.X, at.Y))
            {
                changed |= ScrollTo(splits, window.TopAfterWheel(wheel));
            }
        }

        return false;
    }

    private bool ScrollTo(bool splits, int top)
    {
        ref int at = ref splits ? ref _splitsTop : ref _scoresTop;
        if (at == top)
        {
            return false;
        }

        at = top;
        return true;
    }

    private int TakeWheel()
    {
        int steps = _wheel;
        _wheel = 0;
        return steps;
    }

    // Player 1's mouse in authored pixels.
    private (float X, float Y, bool Pressed)? SeatPointer() => AuthoredPointer.Of(this, _input);

    private void Compose()
    {
        if (_sheet != null)
        {
            _view?.Show(OriginalRaceResults.Compose(_sheet, _strings, _menu?.Index ?? 0, _pointer.Held, _scoresTop, _splitsTop),
                BoardPalette.Paper, string.Empty, string.Empty);
        }
    }
}
