using System.Collections.Generic;
using CSVM.Bindings;
using Godot;

namespace CSVM.UI;

/// <summary>
/// Draws a <see cref="BoardMenu"/>'s rows in the launchscreen's cursor idiom (dim rows, the
/// highlighted one gold behind a ▶) inside the board style every board already uses. One renderer
/// for all five boards, so the cursor reads the same wherever it appears and a layout fix lands
/// once. Drop it in a board's body VBox and call <see cref="Refresh"/> when the menu reports the
/// highlight moved. Its footer is the seat's own control hints, which follow that seat's device.
/// </summary>
public sealed partial class BoardMenuView : VBoxContainer
{
    // Base metrics at 720p, matching the boards' own row/footer sizes. All TUNE.
    private const int RowFont = 20;
    private const int LegendFont = 15;
    private const float LegendGap = 28f;

    private static readonly Color RowColor = new(0.55f, 0.62f, 0.72f);
    private static readonly Color RowFocusColor = new(1f, 0.86f, 0.38f);
    private static readonly Color LegendColor = new(0.68f, 0.74f, 0.82f);

    private BoardMenu _menu = null!;
    private CursorRow[] _rows = System.Array.Empty<CursorRow>();
    private ControlHintBar? _legend;
    private float _scale = 1f;

    /// <summary>Builds the rows and the button legend for <paramref name="menu"/>, scaled by the
    /// board's own <paramref name="s"/> so a menu matches the panel it sits in.
    /// <paramref name="input"/> is the seat driving the cursor, whose own bindings and device the
    /// legend names.</summary>
    public static BoardMenuView Build(BoardMenu menu, float s, MenuInput input)
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

        view._legend = ControlHintBar.Build(
            Legend(menu.Dismissable, input), (int)(LegendFont * s), LegendColor, LegendGap * s);
        view._legend.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var spacer = new Control { CustomMinimumSize = new Vector2(0f, 8f * s) };
        view.AddChild(spacer);
        view.AddChild(view._legend);

        view.Refresh();
        return view;
    }

    /// <summary>What a board's footer teaches, over the seat's OWN bindings rather than the shipped
    /// defaults, since nothing else on a board names the cursor's controls. Three separate hints and
    /// not one sentence: a seat names one device at a time, and each item is composed through that
    /// gate. A board with no way back advertises none, which is every results board. Public so the
    /// Original sheet, which draws its own footer, says the same thing this one does.</summary>
    public static IReadOnlyList<ControlLine> Legend(bool dismissable, MenuInput input)
    {
        var items = new List<ControlLine>(3)
        {
            input.Hint("%1  Select", InputAction.MenuDown),
            input.Hint("%1  Confirm", InputAction.MenuAccept),
        };
        if (dismissable)
        {
            items.Add(input.Hint("%1  Resume", InputAction.MenuBack));
        }

        return items;
    }

    /// <summary>Rewrites the footer for <paramref name="input"/>'s device, the seat's cue when a key
    /// or a pad press hands the hints over mid-board.</summary>
    public void Relegend(MenuInput input) =>
        _legend?.Show(Legend(_menu.Dismissable, input), (int)(LegendFont * _scale), LegendColor,
            LegendGap * _scale);

    /// <summary>Recolours the rows from the menu's current highlight. Cheap enough to call every
    /// time the cursor moves, since it touches only the label overrides.</summary>
    public void Refresh()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            bool selected = i == _menu.Index;
            _rows[i].Set(_menu.Items[i].Label, selected ? RowFocusColor : RowColor, selected);
        }
    }

}
