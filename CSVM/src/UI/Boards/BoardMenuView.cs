using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// Draws a <see cref="BoardMenu"/>'s rows in the launchscreen's cursor idiom (dim rows, the
/// highlighted one gold behind a ▶) inside the board style every board already uses. One renderer
/// for all five boards, so the cursor reads the same wherever it appears and a layout fix lands
/// once. Drop it in a board's body VBox and call <see cref="Refresh"/> when the menu reports the
/// highlight moved. A results board's footer is the seat's own control hints, which follow that
/// seat's device; a pause board draws none, as the original's pause sheet carries none.
/// </summary>
public sealed partial class BoardMenuView : VBoxContainer
{
    // The legend's gap between hints at the boards' 720p reference. TUNE.
    private const float LegendGap = 28f;

    // The rows' and the legend's sizes, chrome type scale rungs at the same reference.
    private static readonly float RowFont = ChromeType.InReference(ChromeSize.Body, ResultsBoard.ReferenceHeight);
    private static readonly float LegendFont = ChromeType.InReference(ChromeSize.Caption, ResultsBoard.ReferenceHeight);

    private static readonly Color RowColor = new(0.55f, 0.62f, 0.72f);
    private static readonly Color RowFocusColor = new(1f, 0.86f, 0.38f);
    private static readonly Color LegendColor = new(0.68f, 0.74f, 0.82f);

    private BoardMenu _menu = null!;
    private CursorRow[] _rows = System.Array.Empty<CursorRow>();
    private ControlHintBar? _legend;
    private float _scale = 1f;

    /// <summary>Whether a row carries the highlight. False while the board's cursor stands on
    /// something else above the rows (a photograph), which <see cref="Refresh"/> then draws.</summary>
    public bool ShowsCursor { get; set; } = true;

    /// <summary>Builds the rows for <paramref name="menu"/>, scaled by the board's own
    /// <paramref name="s"/> so a menu matches the panel it sits in, and under them the button
    /// legend where <paramref name="legend"/> asks for one. <paramref name="input"/> is the seat
    /// driving the cursor, whose own bindings and device the legend names.</summary>
    public static BoardMenuView Build(BoardMenu menu, float s, MenuInput input, bool legend)
    {
        var view = new BoardMenuView
        {
            _menu = menu,
            _scale = s,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        view.AddThemeConstantOverride("separation", Mathf.RoundToInt(4f * s));

        var rows = new CursorRow[menu.Items.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = CursorRow.Build(menu.Items[i].Label, (int)(RowFont * s), RowColor, selected: false);
            view.AddChild(rows[i]);
        }
        view._rows = rows;

        if (legend)
        {
            view._legend = ControlHintBar.Build(
                Legend(input), (int)(LegendFont * s), LegendColor, LegendGap * s);
            view._legend.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var spacer = new Control { CustomMinimumSize = new Vector2(0f, 8f * s) };
            view.AddChild(spacer);
            view.AddChild(view._legend);
        }

        view.Refresh();
        return view;
    }

    /// <summary>Rewrites the footer for <paramref name="input"/>'s device, the seat's cue when a key
    /// or a pad press hands the hints over mid-board. A view built without a legend ignores it.</summary>
    public void Relegend(MenuInput input) =>
        _legend?.Show(Legend(input), (int)(LegendFont * _scale), LegendColor, LegendGap * _scale);

    /// <summary>Recolours the rows from the menu's current highlight. Cheap enough to call every
    /// time the cursor moves, since it touches only the label overrides.</summary>
    public void Refresh()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            bool selected = ShowsCursor && i == _menu.Index;
            _rows[i].Set(_menu.Items[i].Label, selected ? RowFocusColor : RowColor, selected);
        }
    }

    /// <summary>The row whose drawn line holds the point, in canvas pixels, or -1 for none: the hit
    /// test a <see cref="BoardMenuPointer"/> reads this view through.</summary>
    public int RowAt(float x, float y)
    {
        var at = new Vector2(x, y);
        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i].GetGlobalRect().HasPoint(at))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A row's drawn line in canvas pixels, for a suite that points at it.</summary>
    public Rect2 RowRect(int row) =>
        row >= 0 && row < _rows.Length ? _rows[row].GetGlobalRect() : default;

    // Over the seat's OWN bindings rather than the shipped defaults, since nothing else on a board
    // names the cursor's controls. Separate hints and not one sentence: a seat names one device at
    // a time, and each item is composed through that gate. No way back is advertised, since only a
    // results board draws a legend and none of them can be dismissed.
    private static IReadOnlyList<ControlLine> Legend(MenuInput input) => new[]
    {
        input.Hint("%1  Select", InputAction.MenuDown),
        input.Hint("%1  Confirm", InputAction.MenuAccept),
    };
}
