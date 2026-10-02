using System;
using System.Collections.Generic;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// What every page behind the Options hub stands on, held once so the five page modules share it.
/// It places a section's button strips at their measured size and the ACCEPT CHANGES and CANCEL
/// CHANGES pair in either form. It builds the slider row and the open option list, and draws the
/// plate, the title and the controls in their states. It holds no page's state and reaches the
/// shell only through <see cref="IOriginalScreenHost"/>. The form the pages stand behind is
/// <see cref="OriginalOptionsScreen"/>.
/// </summary>
internal sealed class OriginalOptionsChrome
{
    /// <summary>The words every page's own row titles are written in, the hub's description size.</summary>
    internal const float TitleFont = 14f;

    /// <summary>The words every description, list item and instruction line is written in.</summary>
    internal const float DescriptionFont = 12f;

    /// <summary>A checkbox's size where its strip cannot be measured, so the row has a rectangle.</summary>
    internal const float FallbackCheckSize = 24f;

    // The hub's own title font, which every page writes its own title in.
    private const float PageTitleFont = 20f;

    // A decoded button strip's size where its own art cannot be measured.
    private const float FallbackButtonWidth = 220f;
    private const float FallbackButtonHeight = 42f;

    // The slider's two art files, and their shipped pixel sizes as the fallback when neither can
    // be measured. Neither row carries a frame count, so each is one image with no state to draw.
    // docs/formats/menu-layout.md holds the Z row's decode.
    private const string SliderSlotArt = "PF_B_SliderSlot.png";
    private const string SliderThumbArt = "PF_B_Slider.png";
    private const float FallbackSlotWidth = 171f;
    private const float FallbackSlotHeight = 3f;
    private const float FallbackThumbWidth = 43f;
    private const float FallbackThumbHeight = 21f;

    // The authored insets from the slot to the region a press has to land in, negative where the
    // region grows. Three pixels of slot become twenty-three, which is what makes the whole thumb
    // pressable. Every shipped slider row authors these four.
    private const int SliderInsetLeft = 0;
    private const int SliderInsetTop = -10;
    private const int SliderInsetRight = 1;
    private const int SliderInsetBottom = -10;

    /// <summary>The kit over <paramref name="layout"/>'s sections, calling back into
    /// <paramref name="host"/> for the measurer, the cursor and the shell's row rules.</summary>
    internal OriginalOptionsChrome(MenuLayout layout, IOriginalScreenHost host)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>The decoded layout every page reads its own section off.</summary>
    internal MenuLayout Layout { get; }

    /// <summary>The shell's side of the seam, as every page reaches it.</summary>
    internal IOriginalScreenHost Host { get; }

    /// <summary>The n-th art a row names as a strip, the shared drop-list rule's own reading. The
    /// pages name their arrows, bars and checkbox strips this way.</summary>
    internal static BoardArt? StripArt(IReadOnlyList<string> art, int index, int frames = 4) =>
        OriginalDropLists.StripArt(art, index, frames);

    /// <summary>An authored text row as one line. A leading <c>[FONTID]</c> tag is a renderer
    /// directive the extractor left on the rebinding pages' multi-line rows. The newline inside is
    /// the original's own break, which a wrapped line does not need.</summary>
    internal static string Untagged(string text)
    {
        string line = text.Replace('\n', ' ');
        if (line.Length > 2 && line[0] == '[' && line.IndexOf(']') is var close && close > 1)
        {
            line = line[(close + 1)..];
        }

        return line;
    }

    /// <summary>The shared open-dropdown rule over one option's words and box.</summary>
    internal static OpenDropList DropList(
        string key, MenuLayoutWidget? widget, IReadOnlyList<string> items,
        (float X, float Y, float Width, float Height) box) =>
        OriginalDropLists.Over(key, widget, items, box);

    /// <summary>The open list's first windowed row, pulled to keep the focused item in view.</summary>
    internal static int DropListTop(OpenDropList drop, int top, int focused) =>
        OriginalDropLists.Top(drop, top, focused);

    /// <summary>A strip's one-frame size from the host's measurer, or the fallback when the file is
    /// not there.</summary>
    internal (float Width, float Height) StripSize(BoardArt? art, float fallbackWidth, float fallbackHeight)
    {
        if (art == null || Host.Measure(art.Name) is not { } size)
        {
            return (fallbackWidth, fallbackHeight);
        }

        return (size.Width, (float)Math.Floor(size.Height / (float)Math.Max(1, art.Frames)));
    }

    /// <summary>One of a section's own button strips as a row, at its authored corner in its
    /// measured size. The offset <paramref name="dy"/> moves it down from that corner, which takes
    /// a plaque standing on a grown plate's bottom band down with the plate.</summary>
    internal void AddStrip(
        MenuLayoutScreen screen, List<OriginalRow> rows, string key, OriginalRowKind kind, bool enabled,
        int column, float dy = 0f)
    {
        if (screen.Widget(key) is not { } widget)
        {
            return;
        }

        var art = StripArt(widget.Art, 0, widget.Frames);
        var size = StripSize(art, FallbackButtonWidth, FallbackButtonHeight);
        rows.Add(new OriginalRow(key, widget.Text ?? string.Empty, kind, widget.Int("X"), widget.Int("Y") + dy,
            size.Width, size.Height, enabled, column, art));
    }

    /// <summary>A page's ACCEPT CHANGES and CANCEL CHANGES strips, in that order and one column,
    /// <paramref name="dy"/> below their authored corners.</summary>
    internal void AddPlaques(MenuLayoutScreen screen, List<OriginalRow> rows, string accept, string cancel, float dy = 0f)
    {
        AddStrip(screen, rows, accept, OriginalRowKind.Button, true, 0, dy);
        AddStrip(screen, rows, cancel, OriginalRowKind.Button, true, 0, dy);
    }

    /// <summary>The same pair as the shell's labelled plaques on lines <paramref name="at"/> and
    /// the one under it, which a page drawn without its section stands on.</summary>
    internal void AddFallbackPlaques(List<OriginalRow> rows, string accept, string cancel, int at)
    {
        rows.Add(Host.PlaqueRow(accept, "ACCEPT CHANGES", at, true, 0));
        rows.Add(Host.PlaqueRow(cancel, "CANCEL CHANGES", at + 1, true, 0));
    }

    /// <summary>A slider row from its authored widget. The slot stands at the widget's corner and
    /// the row's rectangle is the region the widget insets it into, which the pointer has to hit.
    /// The page supplies the range, the level and where a new level goes. The shell's own
    /// <see cref="SliderControl"/> drives it and the shell draws it.</summary>
    internal OriginalRow SliderRow(
        MenuLayoutWidget? widget, string key, float fallbackX, float fallbackY,
        int min, int max, int value, Action<int> setValue, bool enabled = true, int column = 0)
    {
        var slot = SliderArt(widget, 0, SliderSlotArt);
        var thumb = SliderArt(widget, 1, SliderThumbArt);
        var slotSize = StripSize(slot, FallbackSlotWidth, FallbackSlotHeight);
        var thumbSize = StripSize(thumb, FallbackThumbWidth, FallbackThumbHeight);
        float x = widget?.Int("X", (int)fallbackX) ?? fallbackX;
        float y = widget?.Int("Y", (int)fallbackY) ?? fallbackY;
        var track = new SliderTrack(x, y, slotSize.Width, slotSize.Height, thumbSize.Width, thumbSize.Height, min, max);
        var region = SliderRegion(widget, x, y, slotSize);
        return new OriginalRow(key, string.Empty, OriginalRowKind.Slider, region.X, region.Y,
            region.Width, region.Height, enabled, column, thumb,
            Slider: new OriginalSlider(track, track.Clamp(value), setValue, slot));
    }

    /// <summary>A slider widget's press region, the slider row's own rectangle, at the corner the
    /// widget authors or the fallback one.</summary>
    internal (float X, float Y, float Width, float Height) SliderBox(MenuLayoutWidget? widget, float fallbackX, float fallbackY)
    {
        float x = widget?.Int("X", (int)fallbackX) ?? fallbackX;
        float y = widget?.Int("Y", (int)fallbackY) ?? fallbackY;
        var slot = StripSize(SliderArt(widget, 0, SliderSlotArt), FallbackSlotWidth, FallbackSlotHeight);
        return SliderRegion(widget, x, y, slot);
    }

    /// <summary>A section's background plate in the backdrop at its authored corner.</summary>
    internal void ComposePlate(MenuLayoutScreen screen, string key, List<BoardPicture> backdrop)
    {
        if (screen.Widget(key) is { Art.Count: > 0 } plate)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, plate.Art[0], Math.Max(1, plate.Frames)),
                plate.Int("X"), plate.Int("Y")));
        }
    }

    /// <summary>A section's own title row in the hub's title size.</summary>
    internal void ComposePageTitle(MenuLayoutScreen screen, string key, string fallback, List<BoardLine> lines)
    {
        if (screen.Widget(key) is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? fallback, title.Int("X"), title.Int("Y"), title.Int("Width"),
                PageTitleFont, BoardInk.Heading, -1, false,
                title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }
    }

    /// <summary>The open list's rows, the ones outside its window hidden.</summary>
    internal void AddDropListRows(OpenDropList drop, int top, List<OriginalRow> rows) =>
        OriginalDropLists.AddRows(drop, top, rows, StripSize);

    /// <summary>The open list's window for the pointer, null while it fits.</summary>
    internal ListWindow? DropListWindow(OpenDropList drop, int top) =>
        OriginalDropLists.Window(drop, top, StripSize);

    /// <summary>The open list scrolled to <paramref name="top"/>. The focus is per screen, so the
    /// pull the rule applies to a focused item lands on the showing page's own cursor.</summary>
    internal int ScrollDropList(OpenDropList drop, int top)
    {
        int focused = Host.FocusedRow;
        int first = OriginalDropLists.Scroll(drop, top, ref focused);
        Host.FocusedRow = focused;
        return first;
    }

    /// <summary>An open option list as the overlay: the panel over the rows the window shows, with
    /// each item's words on it. The arrows and the thumb join it once the list outruns the window.
    /// The panel runs the authored box's full width, the scroll column included.</summary>
    internal BoardPanel ComposeOptionList(OpenDropList drop, int top, IReadOnlyList<OriginalRow> rows, int focus)
    {
        var panelFills = new List<BoardFill>();
        var panelLines = new List<BoardLine>();
        var panelPictures = new List<BoardPicture>();
        float head = float.MaxValue, foot = float.MinValue;
        foreach (var row in rows)
        {
            if (row.Visible && row.Kind == OriginalRowKind.ListRow)
            {
                head = Math.Min(head, row.Y);
                foot = Math.Max(foot, row.Y + row.Height);
            }
        }

        // A dark panel in the plate's own key, not the white one the paper pages open. These pages
        // write in the section's pale text colour, which no white ground would carry.
        if (head < foot)
        {
            panelFills.Add(new BoardFill(drop.X, head, drop.Width, foot - head, 16, 14, 12, 0.94f));
            panelFills.Add(new BoardFill(drop.X, head, drop.Width, foot - head, 200, 190, 170, 1f, Border: true));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            if (!item.Visible)
            {
                continue;
            }

            if (item.Kind == OriginalRowKind.Button && item.Art != null)
            {
                int frame = item.Enabled ? ComposedBoard.PlaqueFrame(item.Art.Frames, i == focus, i == Host.PressedRow) : 0;
                panelPictures.Add(new BoardPicture(item.Art, item.X, item.Y, frame));
                continue;
            }

            if (i == focus)
            {
                panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 255, 255, 255, 0.18f));
            }

            panelLines.Add(new BoardLine(item.Label, item.X + 4f, item.Y + 2f, item.Width - 8f, DescriptionFont,
                i == focus ? BoardInk.RowFocused : BoardInk.Row, i));
        }

        if (DropListWindow(drop, top) is { } window && drop.Thumb is { } thumb)
        {
            panelPictures.Add(new BoardPicture(thumb, window.ThumbX, window.ThumbY, Height: window.ThumbHeight));
        }

        return new BoardPanel(panelFills, panelPictures, panelLines);
    }

    /// <summary>A table page's controls in their states. A checkbox draws from its eight-state strip,
    /// ticked where <paramref name="ticked"/> says so. Every other row takes the shell's plate-row
    /// rule.</summary>
    internal void ComposeControls(
        IReadOnlyList<OriginalRow> controls, int focus, int pressed, Func<string, bool> ticked, BoardLayers layers)
    {
        for (int i = 0; i < controls.Count; i++)
        {
            var control = controls[i];
            if (control.Kind == OriginalRowKind.Radio && control.Art != null)
            {
                // An eight-state strip: the four button states unchecked, then the same four checked.
                int state = control.Enabled ? (i == pressed ? 3 : i == focus ? 2 : 1) : 0;
                int frame = (ticked(control.Key) ? 4 : 0) + state;
                layers.Plaques.Add(new BoardPlaque(control.Art, control.X, control.Y, i, frame, string.Empty, BoardInk.LabelNormal));
                continue;
            }

            Host.ComposeGenericRow(control, i == focus, i == pressed, i, layers);
        }
    }

    // The rectangle a slider row authors is its slot art's own at the row's corner. The four insets
    // the row carries move it in, and out where one is negative. That is the region a press has to
    // land in. A Z row states no width and no height of its own.
    private static (float X, float Y, float Width, float Height) SliderRegion(
        MenuLayoutWidget? widget, float x, float y, (float Width, float Height) slot)
    {
        float left = x + (widget?.Int("Left", SliderInsetLeft) ?? SliderInsetLeft);
        float top = y + (widget?.Int("Top", SliderInsetTop) ?? SliderInsetTop);
        float right = x + slot.Width - (widget?.Int("Right", SliderInsetRight) ?? SliderInsetRight);
        float bottom = y + slot.Height - (widget?.Int("Bottom", SliderInsetBottom) ?? SliderInsetBottom);
        return (left, top, Math.Max(1f, right - left), Math.Max(1f, bottom - top));
    }

    // The n-th art a slider row names, falling back to the shipped file name. The control then
    // still has a name to draw where the section is absent. Neither art is a strip.
    private static BoardArt SliderArt(MenuLayoutWidget? widget, int index, string fallback)
    {
        var art = widget?.Art;
        string name = art != null && index < art.Count && art[index].Length > 0 ? art[index] : fallback;
        return new BoardArt(BoardArtLibrary.Ui, name, 1);
    }
}
