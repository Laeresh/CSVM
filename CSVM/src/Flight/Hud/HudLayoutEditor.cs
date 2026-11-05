using System;
using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The HUD layout editor: the halted rendered view with the HUD over it. You drag each outlined
/// element anywhere on the pane, out to its real edges. Opened over one seat's pane by
/// <see cref="Bindings.InputAction.EditHudLayout"/>; every pane previews the draft through
/// <see cref="Preview"/>. Enter saves the draft as the Custom layout, Escape drops it. Its keys are
/// fixed, as photo mode's are, since it is modal and every seat's keys are silenced while it stands.
/// It holds the clock with <see cref="HaltReason.Editing"/>. docs/architecture/Flight.md.
/// </summary>
public sealed partial class HudLayoutEditor : Control
{
    /// <summary>How far one arrow press moves the selected element, in reference pixels.</summary>
    public const float NudgeStep = 8f;

    /// <summary>How far one arrow press under Shift moves it, in reference pixels.</summary>
    public const float FineNudgeStep = 1f;

    // The help line's size in the 1440p reference, and the outline's width.
    private const float RefHelpFont = 26f;
    private const float OutlineWidth = 2f;

    // The help line's smallest font size and its plate's side padding in pixels. Its baseline stands
    // this many line heights above the pane's bottom, on a plate of this opacity.
    private const int MinHelpFont = 12;
    private const float HelpPadding = 8f;
    private const float HelpLift = 1.5f;
    private const float HelpPlateAlpha = 0.6f;

    private const string HelpText =
        "HUD LAYOUT   Drag an instrument, or Tab and the arrows   S Sides   O Original   Enter save   Esc cancel";

    private static readonly Color Outline = new(1f, 1f, 1f, 0.7f);
    private static readonly Color Chosen = new(1f, 0.86f, 0.38f);
    private static readonly Color ChosenFill = new(1f, 0.86f, 0.38f, 0.15f);

    private static readonly Key[] PolledKeys =
    {
        Key.Escape, Key.Enter, Key.KpEnter, Key.Tab, Key.Left, Key.Right, Key.Up, Key.Down, Key.S, Key.O,
    };

    private readonly FlightHud _hud;
    private readonly IReadOnlyList<FlightController> _seats;
    private readonly PauseState _pause;
    private readonly OptionsStore? _store;
    private readonly Dictionary<Key, bool> _keyPrev = new();
    private HudLayout _draft;
    private HudElement _selected = HudElement.LeftDials;
    private bool _dragging;
    private bool _mousePrev;
    private Vector2 _grab;

    private HudLayoutEditor(FlightHud hud, IReadOnlyList<FlightController> seats, PauseState pause,
        OptionsStore? store, HudLayout start)
    {
        _hud = hud;
        _seats = seats;
        _pause = pause;
        _store = store;
        _draft = start;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        Name = "hud_layout_editor";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Fires once when the editor closes: true on a save, false on a cancel.</summary>
    public event Action<bool>? Closed;

    /// <summary>The draft the open editor shows on every pane, or null while no editor is open.</summary>
    public static HudLayout? Preview { get; private set; }

    /// <summary>The placement the editor would save.</summary>
    public HudLayout Draft => _draft;

    /// <summary>The element the arrows move, the last one grabbed or tabbed to.</summary>
    public HudElement Selected => _selected;

    /// <summary>Whether the editor still stands over its pane.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>Opens the editor over <paramref name="pilot"/>'s pane, starting from the layout the
    /// panes draw now. Null, with a log line, when the session cannot take it. That is no shared
    /// pause, a halt already up, a network session, a headset, or a pane whose HUD is off. A null <paramref name="store"/> (a <c>--det</c> run) saves to no file.</summary>
    public static HudLayoutEditor? Open(FlightController pilot, IReadOnlyList<FlightController> seats,
        PauseState? pause, OptionsStore? store)
    {
        string? refused = pause == null ? "no shared pause"
            : pause.Halted ? "the session is halted"
            : pause.Overlay ? "a network session's clock cannot stop"
            : pilot.Headset != null ? "a headset flies this seat"
            : !pilot.PilotHud.CanHostLayoutEditor ? "the pane's HUD is off"
            : null;
        if (refused != null)
        {
            Log.Info("flight", $"hud layout editor: not opened, {refused}");
            return null;
        }

        var start = Active() ?? HudLayoutPlacement.OriginalLayout;
        var editor = new HudLayoutEditor(pilot.PilotHud, seats, pause!, store, start);
        pause!.Raise(HaltReason.Editing);
        foreach (var seat in seats)
        {
            seat.BeginHudEditor();
        }

        pilot.PilotHud.OpenLayoutEditor(editor);
        editor.Load(start);
        editor.PrimeKeys();
        Log.Info("flight", $"hud layout editor: open, from {editor.Draft.Format()}");
        return editor;
    }

    /// <summary>The layout the running process draws by: the open editor's draft, else the setting's.</summary>
    public static HudLayout? Active() =>
        Preview ?? HudLayoutPlacement.Resolve(HudLayoutSetting.Current, HudLayoutSetting.Custom);

    /// <summary>Picks up the element under <paramref name="at"/>, a pane point, the topmost first.
    /// False where none is.</summary>
    public bool Grab(Vector2 at)
    {
        var (box, pane, scale) = Frame();
        for (int i = HudLayout.Elements.Count - 1; i >= 0; i--)
        {
            var element = HudLayout.Elements[i];
            var rect = HudLayoutPlacement.Place(element, _draft, box, pane, scale);
            if (rect.HasPoint(at))
            {
                _selected = element;
                _grab = at - rect.Position;
                _dragging = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>Moves the grabbed element so the point it was held by stands at <paramref name="at"/>,
    /// kept inside the pane. Nothing is grabbed, nothing moves.</summary>
    public void DragTo(Vector2 at)
    {
        if (!_dragging)
        {
            return;
        }

        var (box, pane, scale) = Frame();
        var size = HudLayoutPlacement.Place(_selected, _draft, box, pane, scale).Size;
        MoveSelected(new Rect2(at - _grab, size), box, pane, scale);
    }

    /// <summary>Lets the grabbed element go where it stands.</summary>
    public void Drop() => _dragging = false;

    /// <summary>Moves the selected element by <paramref name="referencePixels"/>, kept inside the pane.</summary>
    public void Nudge(Vector2 referencePixels)
    {
        var (box, pane, scale) = Frame();
        var rect = HudLayoutPlacement.Place(_selected, _draft, box, pane, scale);
        MoveSelected(new Rect2(rect.Position + (referencePixels * scale), rect.Size), box, pane, scale);
    }

    /// <summary>Selects the next element, or the previous one when <paramref name="backward"/>.</summary>
    public void SelectNext(bool backward = false)
    {
        int count = HudLayout.Elements.Count;
        int at = ((int)_selected + (backward ? count - 1 : 1)) % count;
        _selected = HudLayout.Elements[at];
    }

    /// <summary>Replaces the draft with <paramref name="layout"/> whole, the S and O keys. A layout
    /// measured from the reading box is re-measured from the pane's edges where it stands now.</summary>
    public void Load(HudLayout layout)
    {
        var (box, pane, scale) = Frame();
        SetDraft(HudLayoutPlacement.ToPane(layout, box, pane, scale));
    }

    /// <summary>Closes the editor. A save makes the draft the Custom layout, writes it to the store and
    /// posts the line in every pane. A cancel leaves the layout as it was. Either way the halt goes,
    /// every seat's keys come back with the pause edge seeded, and the panes take the layout at once.</summary>
    public void Close(bool save)
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _dragging = false;
        Preview = null;
        if (save)
        {
            HudLayoutSetting.SaveCustom(_draft, _store);
        }

        _hud.CloseLayoutEditor(this);
        foreach (var seat in _seats)
        {
            if (!IsInstanceValid(seat))
            {
                continue;
            }

            seat.EndHudEditor();
            seat.PilotHud.SetHudLayout(Active());
            if (save)
            {
                seat.PilotHud.PostHudLayout(HudLayoutKind.Custom);
            }
        }

        _pause.Clear(HaltReason.Editing);
        Log.Info("flight", $"hud layout editor: {(save ? "saved" : "cancelled")}");
        Closed?.Invoke(save);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!IsOpen)
        {
            return;
        }

        PollMouse();
        PollKeys();
        QueueRedraw();
    }

    // ⚠ Do not leave the preview behind. A session torn down under an open editor would hand its
    // draft to every pane of the next one.
    public override void _ExitTree()
    {
        if (IsOpen)
        {
            Preview = null;
        }
    }

    public override void _Draw()
    {
        var (box, pane, scale) = Frame();
        var font = ThemeDB.FallbackFont;
        int size = Mathf.Max(MinHelpFont, Mathf.RoundToInt(RefHelpFont * scale));
        foreach (var element in HudLayout.Elements)
        {
            var rect = HudLayoutPlacement.Place(element, _draft, box, pane, scale);
            bool chosen = element == _selected;
            if (chosen)
            {
                DrawRect(rect, ChosenFill);
            }

            DrawRect(rect, chosen ? Chosen : Outline, filled: false, width: OutlineWidth);
        }

        var help = font.GetStringSize(HelpText, HorizontalAlignment.Left, -1, size);
        var at = new Vector2((pane.X - help.X) / 2f, pane.Y - (help.Y * HelpLift));
        var plate = new Rect2(at - new Vector2(HelpPadding, help.Y), help + new Vector2(2f * HelpPadding, help.Y * (HelpLift - 1f)));
        DrawRect(plate, new Color(0f, 0f, 0f, HelpPlateAlpha));
        DrawString(font, at, HelpText, HorizontalAlignment.Left, -1, size, Chosen);
    }

    // The pane's reading box, size and HUD scale, the frame every placement is measured in.
    private (Rect2 Box, Vector2 Pane, float Scale) Frame() =>
        (HudMetrics.ReadingBox(this), GetViewportRect().Size, HudMetrics.Scale(this));

    private void MoveSelected(Rect2 rect, Rect2 box, Vector2 pane, float scale)
    {
        var inside = HudLayoutPlacement.Inside(rect, pane);
        SetDraft(_draft.With(_selected, HudLayoutPlacement.PlacementFor(inside, HudLayoutPlacement.FrameOf(_draft, box, pane), scale)));
    }

    private void SetDraft(HudLayout layout)
    {
        _draft = layout;
        Preview = layout;
    }

    // The window's pointer in this pane's own pixels. A splitscreen pane is a SubViewport drawn by a
    // container, so the pointer is moved by the container's corner and scaled by its stretch.
    private Vector2 PaneMouse()
    {
        if (GetViewport() is SubViewport view && view.GetParent() is SubViewportContainer container)
        {
            var rect = container.GetGlobalRect();
            var stretch = rect.Size.X > 0f && rect.Size.Y > 0f ? (Vector2)view.Size / rect.Size : Vector2.One;
            return (GetTree().Root.GetMousePosition() - rect.Position) * stretch;
        }

        return GetViewport().GetMousePosition();
    }

    // The left button's press grabs, its hold drags and its release drops. Polled rather than taken as
    // GUI input, so the pointer reaches the pane whichever viewport draws it.
    private void PollMouse()
    {
        bool down = Input.IsMouseButtonPressed(MouseButton.Left);
        if (down && !_mousePrev)
        {
            Grab(PaneMouse());
        }
        else if (down && _dragging)
        {
            DragTo(PaneMouse());
        }
        else if (!down && _dragging)
        {
            Drop();
        }

        _mousePrev = down;
    }

    // ⚠ Prime before the first poll. The Shift+F2 that opened the editor is still down, and every key
    // already held reads as no press until it is let go.
    private void PrimeKeys()
    {
        foreach (var key in PolledKeys)
        {
            _keyPrev[key] = Input.IsKeyPressed(key);
        }

        _mousePrev = Input.IsMouseButtonPressed(MouseButton.Left);
    }

    private bool Pressed(Key key)
    {
        bool down = Input.IsKeyPressed(key);
        bool was = _keyPrev.TryGetValue(key, out bool prev) && prev;
        _keyPrev[key] = down;
        return down && !was;
    }

    private void PollKeys()
    {
        bool shift = Input.IsKeyPressed(Key.Shift);
        float step = shift ? FineNudgeStep : NudgeStep;
        var presses = new Dictionary<Key, bool>();
        foreach (var key in PolledKeys)
        {
            presses[key] = Pressed(key);
        }

        if (presses[Key.Escape])
        {
            Close(save: false);
            return;
        }

        if (presses[Key.Enter] || presses[Key.KpEnter])
        {
            Close(save: true);
            return;
        }

        if (presses[Key.Tab])
            SelectNext(backward: shift);
        if (presses[Key.S])
            Load(HudLayoutPlacement.Sides);
        if (presses[Key.O])
            Load(HudLayoutPlacement.OriginalLayout);
        var move = new Vector2(
            (presses[Key.Right] ? step : 0f) - (presses[Key.Left] ? step : 0f),
            (presses[Key.Down] ? step : 0f) - (presses[Key.Up] ? step : 0f));
        if (move != Vector2.Zero)
            Nudge(move);
    }
}
