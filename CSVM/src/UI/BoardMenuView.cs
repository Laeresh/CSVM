using Godot;

namespace CSVM.UI;

/// <summary>
/// Draws a <see cref="BoardMenu"/>'s rows in the launchscreen's cursor idiom (dim rows, the
/// highlighted one gold behind a ▶) inside the board style every board already uses. One renderer
/// for all five boards, so the cursor reads the same wherever it appears and a layout fix lands
/// once. Drop it in a board's body VBox and call <see cref="Refresh"/> when the menu reports the
/// highlight moved.
/// </summary>
public sealed partial class BoardMenuView : VBoxContainer
{
    // Base metrics at 720p, matching the boards' own row/footer sizes. All TUNE.
    private const int RowFont = 20;
    private const int LegendFont = 15;

    private static readonly Color RowColor = new(0.55f, 0.62f, 0.72f);
    private static readonly Color RowFocusColor = new(1f, 0.86f, 0.38f);
    private static readonly Color LegendColor = new(0.68f, 0.74f, 0.82f);

    private BoardMenu _menu = null!;
    private Label[] _rows = System.Array.Empty<Label>();

    /// <summary>Builds the rows and the button legend for <paramref name="menu"/>, scaled by the
    /// board's own <paramref name="s"/> so a menu matches the panel it sits in.</summary>
    public static BoardMenuView Build(BoardMenu menu, float s)
    {
        var view = new BoardMenuView { _menu = menu, MouseFilter = MouseFilterEnum.Ignore };
        view.AddThemeConstantOverride("separation", Mathf.RoundToInt(4f * s));

        var rows = new Label[menu.Items.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = Row(menu.Items[i].Label, (int)(RowFont * s));
            view.AddChild(Centered(rows[i]));
        }
        view._rows = rows;

        var legend = Row(Legend(menu.Dismissable), (int)(LegendFont * s));
        legend.AddThemeColorOverride("font_color", LegendColor);
        var spacer = new Control { CustomMinimumSize = new Vector2(0f, 8f * s) };
        view.AddChild(spacer);
        view.AddChild(Centered(legend));

        view.Refresh();
        return view;
    }

    /// <summary>Recolours the rows from the menu's current highlight. Cheap enough to call every
    /// time the cursor moves, since it touches only the label overrides.</summary>
    public void Refresh()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            bool selected = i == _menu.Index;
            _rows[i].Text = (selected ? "▶  " : "     ") + _menu.Items[i].Label;
            _rows[i].AddThemeColorOverride("font_color", selected ? RowFocusColor : RowColor);
        }
    }

    // What the player can press, since nothing else on a board teaches the cursor. A results board
    // has no way back, so it advertises none.
    private static string Legend(bool dismissable) =>
        dismissable
            ? "↕ Select        A / Enter — Confirm        B / Esc — Resume"
            : "↕ Select        A / Enter — Confirm";

    private static Label Row(string text, int fontSize)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", RowColor);
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
}
