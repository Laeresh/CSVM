using System;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The Original presentation's pause screen: the mission's chart filling the window with its flags
/// and icons, the objectives parchment, the profile's memento and the four authored button strips,
/// composed from <c>escape.zrd</c> the way the load and briefing screens are composed from their
/// own dialogs. Follows <see cref="PauseState.Changed"/> and drives its cursor from the pausing
/// player's reader alone, which is <see cref="PauseBoard"/>'s contract unchanged; the Built-in
/// presentation keeps that board. Decode: docs/org/pause-screen.md.
/// </summary>
public sealed partial class OriginalPauseBoard : Control
{
    private PauseState _state = null!;
    private Func<int, MenuInput> _inputFor = null!;
    private Func<PauseReadout> _readout = null!;
    private PauseSheet _sheet = null!;
    private string _dataRoot = "";
    private ComposedBoardView? _view;
    private BoardMenu? _menu;
    private MenuInput? _input;

    /// <summary>Rerun the running mission in place, chosen from RESTART.</summary>
    public Action? Restart { get; set; }

    /// <summary>Leave the session, chosen from QUIT.</summary>
    public Action? Exit { get; set; }

    /// <summary>Open the options, chosen from PREFERENCES. ⚠ Leave it null while no in-flight
    /// options leaf exists; the strip is authored and drawn either way, and a null action makes the
    /// press a no-op rather than a crash.</summary>
    public Action? Preferences { get; set; }

    /// <summary>The screen as last composed, for a suite that reads what is drawn rather than a
    /// screenshot of it.</summary>
    public ComposedBoard? Shown => _view?.Board;

    /// <summary>The strip the cursor stands on, or -1 before the first pause.</summary>
    public int FocusedRow => _menu?.Index ?? -1;

    /// <summary>Builds the (hidden) board over a loaded sheet and subscribes to the shared pause
    /// state. <paramref name="readout"/> is read afresh on each pause, since the objectives, the
    /// memento and the icons all follow the running mission.</summary>
    public static OriginalPauseBoard Build(
        PauseState state,
        Func<int, MenuInput> inputFor,
        string dataRoot,
        PauseSheet sheet,
        Func<PauseReadout> readout)
    {
        var board = new OriginalPauseBoard
        {
            _state = state,
            _inputFor = inputFor,
            _dataRoot = dataRoot,
            _sheet = sheet,
            _readout = readout,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Visible = false,
        };
        board.SetAnchorsPreset(LayoutPreset.FullRect);
        state.Changed += board.OnChanged;
        return board;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // The view sizes itself off the viewport, so it cannot be populated before it is in the
        // tree; the first pause is what composes anything.
        _view = ComposedBoardView.Build(_dataRoot);
        AddChild(_view);
    }

    /// <inheritdoc/>
    public override void _ExitTree() => _state.Changed -= OnChanged;

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (!Visible || _menu == null || _input == null)
        {
            return;
        }

        _input.Poll((float)delta);

        // PadBack only: Escape and Start already reach the pause toggle through FlightController,
        // so reading the combined back here would act twice.
        if (_menu.Handle(_input.Move, _input.Accept, _input.PadBack) && Visible)
        {
            Compose();
        }
    }

    private void OnChanged()
    {
        if (_state.Paused)
        {
            Populate();
        }

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
            case BoardMenuItem.Preferences:
                Preferences?.Invoke();
                break;
            case BoardMenuItem.Exit:
                Exit?.Invoke();
                break;
        }
    }

    // A fresh menu each pause, so the cursor starts on RESUME and a stray confirm on a board that
    // just appeared cannot restart or leave the session. The owner's own reader drives it.
    private void Populate()
    {
        var menu = new BoardMenu(
            dismissable: true,
            (BoardMenuItem.Resume, _sheet.ButtonLabels[PauseScreens.ResumeRow]),
            (BoardMenuItem.Restart, _sheet.ButtonLabels[PauseScreens.RestartRow]),
            (BoardMenuItem.Preferences, _sheet.ButtonLabels[PauseScreens.PreferencesRow]),
            (BoardMenuItem.Exit, _sheet.ButtonLabels[PauseScreens.QuitRow]));
        menu.Activated += OnActivated;
        menu.Dismissed += () => _state.ForceResume();
        _menu = menu;
        _input = _inputFor(_state.OwnerPlayerIndex);
        _input.Prime();
        Compose();
    }

    // The held frame is never composed from here: a confirm is an edge and the action it runs takes
    // the board away in the same frame, so the cursor's row wears the rollover strip instead.
    private void Compose() =>
        _view?.Show(
            PauseScreens.For(_sheet, _readout(), _menu?.Index ?? 0, pressed: false),
            BoardPalette.Escape,
            string.Empty,
            string.Empty);
}
