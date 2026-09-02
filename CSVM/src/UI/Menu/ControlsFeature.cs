using System;
using System.Collections.Generic;
using CSVM.Bindings;

namespace CSVM.UI.Menu;

/// <summary>A rebind waiting for the player's word, because the control it wants already belongs to
/// somebody. <see cref="Losers"/> is every action that would lose it, in enum order, and is never
/// empty: a control that is free is bound without asking.</summary>
public sealed record RebindSteal(InputAction Action, Binding Binding, IReadOnlyList<InputAction> Losers);

/// <summary>The shared rebinding screen: which seat is being edited, which context and action the
/// cursor is on, the capture in progress, and the steal it is about to perform. Engine-free like
/// every other feature, so a presentation supplies the frame's raw device state and draws whatever
/// this reports.
/// ⚠ A steal is never silent. A capture that lands on a held control raises <see cref="Pending"/>
/// naming every action that would lose it, and nothing moves until <see cref="ConfirmSteal"/>.
/// ⚠ Every edit is scoped to <see cref="Player"/>'s own profile. Two seats hold two maps, so a
/// rebind for player 2 cannot reach player 1, and the screen never edits "the" keymap.</summary>
public sealed class ControlsFeature : IMenuFeature
{
    /// <summary>How many of an action's bindings a row prints before it says how many it is
    /// hiding. Four, because that is the longest row the shipped set holds, so nothing ships
    /// hidden.</summary>
    public const int RowBindings = 4;

    private readonly Dictionary<int, SeatState> _seats = new();
    private readonly List<int> _players = new();
    private readonly HashSet<int> _dirty = new();
    private readonly Action<int, BindingProfile>? _save;

    private InputContext _context = InputContext.Flight;
    private ControlCapture? _capture;
    private int _player;
    private int _row;
    private int _slot;

    /// <summary>A feature whose saves go through <paramref name="save"/>, or nowhere when that is
    /// null. The write is injected rather than reached for, so the feature stays engine-free and a
    /// test never touches the player's real keymap file.</summary>
    public ControlsFeature(Action<int, BindingProfile>? save = null) => _save = save;

    /// <summary>The seats this screen can edit, in the order they were registered.</summary>
    public IReadOnlyList<int> Players => _players;

    /// <summary>Whichever seat's keymap the screen is showing.</summary>
    public int Player
    {
        get => _player;
        set
        {
            if (!_seats.ContainsKey(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "No seat is registered for that player.");
            _player = value;
            ResetCursor();
        }
    }

    /// <summary>Which of the three keymaps a seat holds is being edited. The steal rule runs inside
    /// a context (<see cref="InputContext"/>), so this is also the scope of every conflict.</summary>
    public InputContext Context
    {
        get => _context;
        set
        {
            _context = value;
            ResetCursor();
        }
    }

    /// <summary>The rows the screen draws: every action this context owns, bound or not.</summary>
    public IReadOnlyList<InputAction> Actions => DefaultBindings.ActionsIn(_context);

    /// <summary>The highlighted row.</summary>
    public int Row => _row;

    /// <summary>The action the highlighted row names.</summary>
    public InputAction Focused => Actions[_row];

    /// <summary>Which of the focused action's bindings is highlighted. Equal to the binding count
    /// when the cursor is on the empty slot past the end, which is how a control is added rather
    /// than replaced.</summary>
    public int Slot => _slot;

    /// <summary>Whether the screen is waiting for the player to press a control.</summary>
    public bool Capturing { get; private set; }

    /// <summary>The steal awaiting the player's word, or null when nothing is pending.</summary>
    public RebindSteal? Pending { get; private set; }

    /// <summary>The line the screen prints under the list: what just happened, or what is being
    /// asked. Empty when there is nothing to say.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Whether any seat's keymap has changed since it was last saved. Tracked per seat
    /// rather than per screen, because stepping to another player must not lose the first player's
    /// edits.</summary>
    public bool Dirty => _dirty.Count > 0;

    /// <summary>The focused action's bindings, in the order the row prints them.</summary>
    public IReadOnlyList<Binding> FocusedBindings => Map.Bindings(Focused);

    /// <summary>Whether the seat being edited reads the keyboard at all. False for a pad-only
    /// splitscreen seat, which cannot capture a key or a mouse button.</summary>
    public bool ReadsKeyboard => _seats[_player].ReadsKeyboard;

    private ActionMap Map => _seats[_player].Profile.Map(_context);

    /// <summary>Registers one seat's live keymap. The profile is the one its polling sites read, not
    /// a copy, so a committed rebind reaches the seat without a reload. <paramref name="padOf"/>
    /// gives the identity that context's pad bindings sit on, per context rather than per seat,
    /// because the three polling sites do not share one placeholder.</summary>
    public void AddSeat(int player, BindingProfile profile, Func<InputContext, DeviceId> padOf, bool readsKeyboard)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(padOf);
        if (!_seats.ContainsKey(player))
            _players.Add(player);
        _seats[player] = new SeatState(profile, padOf, readsKeyboard);
        if (_players.Count == 1)
            _player = player;
    }

    /// <summary>That action's bindings on the seat being edited.</summary>
    public IReadOnlyList<Binding> Bindings(InputAction action) => Map.Bindings(action);

    /// <summary>One row's controls as text, hiding nothing without saying so.</summary>
    public string RowText(InputAction action) => BindingLabels.Row(Map.Bindings(action), RowBindings);

    /// <summary>Points the row cursor at one of <see cref="Actions"/>, clamped, and puts the slot
    /// cursor back on that row's first control. The screen owns the wrap, because its own list
    /// carries the seat and context steppers above these rows.</summary>
    public void Focus(int row)
    {
        int count = Actions.Count;
        _row = row < 0 ? 0 : row >= count ? count - 1 : row;
        _slot = 0;
        CancelCapture();
    }

    /// <summary>Moves the slot cursor along the focused action's controls, clamped, with one place
    /// past the last for adding a control rather than replacing one.</summary>
    public void MoveSlot(int step)
    {
        int max = FocusedBindings.Count;
        int next = _slot + step;
        _slot = next < 0 ? 0 : next > max ? max : next;
        CancelCapture();
    }

    /// <summary>Starts listening for a control. Everything already held is masked, so the button
    /// that opened the capture is not read as the answer to it.</summary>
    public void BeginCapture(IDeviceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var seat = _seats[_player];
        Pending = null;
        Capturing = true;
        _capture = new ControlCapture(seat.PadOf(_context), seat.ReadsKeyboard);
        _capture.Arm(state);
        Status = $"Press a control for {BindingLabels.Name(Focused)}.";
    }

    /// <summary>Stops listening without binding anything.</summary>
    public void CancelCapture()
    {
        if (Capturing)
            Status = string.Empty;
        Capturing = false;
        _capture = null;
    }

    /// <summary>One frame of a capture in progress: returns true when something the screen draws
    /// changed. Does nothing while no capture is running.</summary>
    public bool Poll(IDeviceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Capturing || _capture is not { } capture)
            return false;

        if (capture.Cancelled(state))
        {
            Capturing = false;
            _capture = null;
            Status = "Cancelled.";
            return true;
        }

        if (capture.Poll(state) is not { } binding)
            return false;

        Capturing = false;
        _capture = null;
        return Offer(binding);
    }

    /// <summary>Proposes a control for the focused slot, as a capture does. Public because it is
    /// the whole rule and a test drives it without a device.</summary>
    public bool Offer(Binding binding)
    {
        var losers = new List<InputAction>();
        foreach (var owner in Map.OwnersOf(binding))
        {
            if (owner != Focused)
                losers.Add(owner);
        }

        if (losers.Count == 0)
        {
            Commit(binding);
            return true;
        }

        Pending = new RebindSteal(Focused, binding, losers);
        Status = $"{BindingLabels.Describe(binding)} is bound to {BindingLabels.Clause(losers)}. "
            + "Accept takes it; Back leaves it alone.";
        return true;
    }

    /// <summary>Performs the pending steal, which is the only path that takes a control off another
    /// action.</summary>
    public void ConfirmSteal()
    {
        if (Pending is not { } pending)
            return;
        Pending = null;
        Commit(pending.Binding);
    }

    /// <summary>Drops the pending steal and leaves every action's controls where they were.
    /// </summary>
    public void DiscardSteal()
    {
        if (Pending is null)
            return;
        Pending = null;
        Status = "Left alone.";
    }

    /// <summary>Drops the highlighted control from the focused action, touching no other action.
    /// </summary>
    public void UnbindSlot()
    {
        var bindings = FocusedBindings;
        if (_slot >= bindings.Count)
            return;

        var dropped = bindings[_slot];
        Map.Unassign(Focused, dropped);
        _slot = Math.Min(_slot, FocusedBindings.Count);
        MarkDirty();
        Status = $"{BindingLabels.Name(Focused)} lost {BindingLabels.Describe(dropped)}.";
    }

    /// <summary>Puts this seat's whole context back to the shipped defaults, in place, so the maps
    /// its polling sites already hold are the ones that change.</summary>
    public void ResetContext()
    {
        var seat = _seats[_player];
        var defaults = DefaultBindings.MapFor(_context, seat.PadOf(_context));
        var map = Map;
        map.Clear();
        foreach (var action in defaults.BoundActions)
        {
            foreach (var binding in defaults.Bindings(action))
                map.Add(action, binding);
        }

        _slot = 0;
        MarkDirty();
        Status = "Defaults restored.";
    }

    /// <summary>Writes every changed seat's keymap through whatever save the host supplied. Every
    /// seat rather than the one on screen, since a player who edits two seats and leaves once
    /// expects both written.</summary>
    public void Save()
    {
        foreach (int player in _players)
        {
            if (_dirty.Contains(player))
                _save?.Invoke(player, _seats[player].Profile);
        }

        _dirty.Clear();
    }

    /// <summary>Drops the capture, the pending steal and the status line. The keymaps themselves are
    /// persisted data and survive a presentation switch.</summary>
    public void Discard()
    {
        Capturing = false;
        Pending = null;
        Status = string.Empty;
        ResetCursor();
    }

    private void Commit(Binding binding)
    {
        var bindings = FocusedBindings;
        if (_slot < bindings.Count)
            Map.Unassign(Focused, bindings[_slot]);

        var stolen = Map.Assign(Focused, binding);
        _slot = Math.Max(0, Map.Bindings(Focused).Count - 1);
        MarkDirty();
        Status = stolen.Count == 0
            ? $"{BindingLabels.Name(Focused)} is now {BindingLabels.Describe(binding)}."
            : $"{BindingLabels.Name(Focused)} is now {BindingLabels.Describe(binding)}. "
                + $"{BindingLabels.Clause(stolen)} lost it.";
    }

    private void MarkDirty() => _dirty.Add(_player);

    private void ResetCursor()
    {
        _row = 0;
        _slot = 0;
        Capturing = false;
        _capture = null;
        Pending = null;
    }

    private sealed record SeatState(BindingProfile Profile, Func<InputContext, DeviceId> PadOf, bool ReadsKeyboard);
}
