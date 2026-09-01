using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>The Original presentation's screens. The top level is decoded; the other two are
/// remake-only screens composed in the decoded chrome's conventions.</summary>
public enum OriginalScreen
{
    /// <summary>The main menu: the decoded <c>[@MainMenu@]</c> rows plus the Free Flight door.</summary>
    TopLevel,

    /// <summary>The remake-only Free Flight screen: a chapter list, an airframe list, BACK and FLY.</summary>
    FreeFlight,

    /// <summary>The remake-only minimal Options screen: the presentation chooser.</summary>
    Options,
}

/// <summary>How a row draws and reacts.</summary>
public enum OriginalRowKind
{
    /// <summary>A decoded button strip: the words are painted in, the state is the frame drawn.</summary>
    Button,

    /// <summary>A plaque with a label written over it in the four state colours.</summary>
    TextButton,

    /// <summary>One entry of a text list.</summary>
    ListRow,
}

/// <summary>One interactive element of a screen in authored 800x600 pixels: what it is, where it
/// is, whether it reacts, and which column it belongs to for the seat's cursor.</summary>
public sealed record OriginalRow(
    string Key, string Label, OriginalRowKind Kind, float X, float Y, float Width, float Height,
    bool Enabled, int Column, BoardArt? Art)
{
    /// <summary>Whether an authored point lies on the row.</summary>
    public bool Contains(float x, float y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>What one frame of input did: the cues to play, the exit to take if any, and whether
/// the picture changed.</summary>
public sealed record OriginalStep(IReadOnlyList<string> Cues, MenuExit? Exit, bool Changed);

/// <summary>The colours the shell writes in, read off the layout: the file-wide four state
/// colours and the paper plaque's own label tail.</summary>
public sealed record OriginalInks(
    MenuLayoutColor Disabled, MenuLayoutColor Active, MenuLayoutColor Rollover, MenuLayoutColor Depressed,
    MenuLayoutColor LabelNormal, MenuLayoutColor LabelRollover, MenuLayoutColor LabelDepressed);

/// <summary>
/// The Original presentation's screen graph, engine-free: the decoded top level with the
/// remake-only Free Flight door, the Free Flight screen and the minimal Options screen, driven by
/// one seat's semantic commands and composed into a <see cref="ComposedBoard"/> in the authored
/// 800x600 space. The pointer arrives already mapped into that space; hovering a live row moves
/// the focus onto it, so keyboard, pad and pointer share one cursor. Every rectangle and art name
/// comes from the layout; the art's pixel size, which the layout does not carry, comes from the
/// measurer the presentation injects.
/// </summary>
public sealed class OriginalShell
{
    /// <summary>The Free Flight door's key on the top level.</summary>
    public const string FreeFlightKey = "FREEFLIGHT";

    /// <summary>The Free Flight screen's leave button.</summary>
    public const string BackKey = "BACK";

    /// <summary>The Free Flight screen's launch button.</summary>
    public const string FlyKey = "FLY";

    /// <summary>The Options screen's presentation toggle.</summary>
    public const string PresentationKey = "PRESENTATION";

    /// <summary>The Options screen's apply button.</summary>
    public const string ApplyKey = "APPLY";

    // The Free Flight door beside the button frame, level with the frame's first row. The frame
    // column is full, so the door stands in the clear left margin at the row pitch's height.
    private const float DoorX = 42f;
    private const float DoorY = 293f;

    // The remake-only screens' list geometry: two columns under the logo, one authored text
    // height (STDTEXTH, 16) plus air per row, and the two plaques on the bottom margin.
    private const float ListTop = 286f;
    private const float RowPitch = 22f;
    private const float RowHeight = 20f;
    private const float ListWidth = 300f;
    private const float LeftColumnX = 60f;
    private const float RightColumnX = 440f;
    private const float PlaqueY = 540f;
    private const float RowFont = 16f;
    private const float HeadingFont = 20f;
    private const float FooterY = 578f;
    private const float FooterFont = 12f;
    private const float OptionsX = 319f;
    private const float OptionsTop = 300f;
    private const float OptionsPitch = 40f;

    // A plaque's size when its art cannot be measured, so the row still has a rectangle.
    private const float FallbackPlaqueWidth = 162f;
    private const float FallbackPlaqueHeight = 28f;
    private const float FallbackButtonWidth = 220f;
    private const float FallbackButtonHeight = 42f;

    private static readonly string[] TopLevelButtons =
    {
        "MM_B_CAMPAIGN", "MM_B_INSTANTACTION", "MM_B_MULTIPLAYER", "MM_B_PREFERENCES", "MM_B_CREDITS", "MM_B_QUIT",
    };

    private readonly MenuLayout _layout;
    private readonly FreeFlightFeature _free;
    private readonly Func<string, (int Width, int Height)?> _measure;
    private readonly IReadOnlyList<OriginalChapter> _chapters;
    private readonly IReadOnlyList<OriginalAirframe> _airframes;
    private readonly BoardArt? _plaque;
    private readonly BoardArt _activePointer;
    private readonly BoardArt _passivePointer;
    private readonly int[] _focus = new int[3];
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);

    private OriginalScreen _screen;
    private int _hover = -1;
    private int _pressed = -1;
    private (float X, float Y)? _pointer;
    private int _pickedChapter = -1;
    private int _pickedAirframe = -1;
    private string _choice = PresentationId.Original.Value;

    /// <summary>A shell over <paramref name="layout"/> and the shared Free Flight feature.
    /// <paramref name="measure"/> answers an art name with its strip's pixel size, or null when
    /// the file is not there; the rosters default to <see cref="OriginalRosters"/>.</summary>
    public OriginalShell(
        MenuLayout layout,
        FreeFlightFeature free,
        Func<string, (int Width, int Height)?> measure,
        IReadOnlyList<OriginalChapter>? chapters = null,
        IReadOnlyList<OriginalAirframe>? airframes = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _free = free ?? throw new ArgumentNullException(nameof(free));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _chapters = chapters ?? OriginalRosters.Chapters;
        _airframes = airframes ?? OriginalRosters.Airframes;
        var plaqueRow = layout.Screen("FlightCheck")?.Widget("FC_B_CHANGEPLANE");
        _plaque = plaqueRow is { Art.Count: > 0 } ? new BoardArt(BoardArtLibrary.Ui, plaqueRow.Art[0], plaqueRow.Frames) : null;
        Inks = ReadInks(layout, plaqueRow);
        _activePointer = new BoardArt(BoardArtLibrary.Ui, PointerArt(layout, "activepointerz.png"));
        _passivePointer = new BoardArt(BoardArtLibrary.Ui, PointerArt(layout, "passivepointerz.png"));
        for (int i = 0; i < _focus.Length; i++)
        {
            _focus[i] = -1;
        }
    }

    /// <summary>The screen showing.</summary>
    public OriginalScreen Screen => _screen;

    /// <summary>The colours the shell writes in.</summary>
    public OriginalInks Inks { get; }

    /// <summary>The current screen's rows, in focus order.</summary>
    public IReadOnlyList<OriginalRow> Rows => BuildRows();

    /// <summary>The focused row's index into <see cref="Rows"/>, or -1 when nothing can take focus.</summary>
    public int Focus => EnsureFocus(Rows);

    /// <summary>The focused row's key, or "".</summary>
    public string FocusedKey
    {
        get
        {
            var rows = Rows;
            int focus = EnsureFocus(rows);
            return focus >= 0 ? rows[focus].Key : string.Empty;
        }
    }

    /// <summary>The row under the pointer, or -1.</summary>
    public int Hover => _hover;

    /// <summary>The picked chapter's code, or null.</summary>
    public string? PickedChapter => _pickedChapter >= 0 ? _chapters[_pickedChapter].Code : null;

    /// <summary>The picked airframe's node, or null.</summary>
    public string? PickedAirframe => _pickedAirframe >= 0 ? _airframes[_pickedAirframe].Node : null;

    /// <summary>The presentation the Options screen would apply.</summary>
    public string PresentationChoice => _choice;

    /// <summary>The pointer's last authored position, or null when the seat has none.</summary>
    public (float X, float Y)? Pointer => _pointer;

    /// <summary>Stands the shell on its top level, the landing point of every return and of a
    /// cold start: the list cursors stay where they were, the airframe pick is dropped so a
    /// return from flight cannot fly again on a stale pick.</summary>
    public void ReturnToTopLevel()
    {
        _pickedAirframe = -1;
        Open(OriginalScreen.TopLevel);
    }

    /// <summary>Opens a screen directly, the screenshot aids' door.</summary>
    public void Open(OriginalScreen screen)
    {
        _screen = screen;
        _hover = -1;
        _pressed = -1;
    }

    /// <summary>Applies one frame of one seat's commands. The pointer, when present, is in
    /// authored pixels.</summary>
    public OriginalStep Step(MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var cues = new List<string>();
        MenuExit? exit = null;
        bool changed = false;
        var rows = Rows;
        int focus = EnsureFocus(rows);

        if (commands.Pointer is { } pointer)
        {
            changed |= _pointer != (pointer.X, pointer.Y);
            _pointer = (pointer.X, pointer.Y);
            int over = HitTest(rows, pointer.X, pointer.Y);
            if (over != _hover)
            {
                _hover = over;
                changed = true;
                if (over >= 0 && rows[over].Enabled)
                {
                    focus = over;
                    if (rows[over].Kind != OriginalRowKind.ListRow)
                    {
                        cues.Add(OriginalCues.Rollover);
                    }
                }
            }

            int pressed = pointer.Pressed && over >= 0 && rows[over].Enabled ? over : -1;
            changed |= pressed != _pressed;
            _pressed = pressed;
            if (pointer.Clicked && over >= 0 && rows[over].Enabled)
            {
                focus = over;
                _focus[(int)_screen] = focus;
                exit = Activate(rows[over], cues);
                changed = true;
                rows = Rows;
                focus = EnsureFocus(rows);
            }
        }

        if (commands.MoveY != 0)
        {
            focus = StepWithinColumn(rows, focus, commands.MoveY);
            changed = true;
        }

        if (commands.MoveX != 0)
        {
            focus = StepColumn(rows, focus, commands.MoveX);
            changed = true;
        }

        _focus[(int)_screen] = focus;
        if (exit == null && commands.Accept && focus >= 0 && rows[focus].Enabled)
        {
            exit = Activate(rows[focus], cues);
            changed = true;
        }
        else if (exit == null && commands.Back)
        {
            exit = Back();
            changed = true;
        }

        return new OriginalStep(cues, exit, changed);
    }

    /// <summary>The screen as a composed board in the authored space, the pointer drawn last.</summary>
    public ComposedBoard Compose()
    {
        var rows = Rows;
        int focus = EnsureFocus(rows);
        var pictures = new List<BoardPicture>();
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        var plaques = new List<BoardPlaque>();
        var main = _layout.Screen(OriginalAvailability.MainMenuSection);
        if (main?.Widget("MM_LOGO") is { Art.Count: > 0 } logo)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], logo.Frames), logo.Int("X"), logo.Int("Y")));
        }

        if (_screen == OriginalScreen.TopLevel && main?.Widget("BFRAME") is { Art.Count: > 0 } structure)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, structure.Art[0], structure.Frames), structure.Int("X"), structure.Int("Y")));
        }

        switch (_screen)
        {
            case OriginalScreen.FreeFlight:
                lines.Add(new BoardLine("FREE FLIGHT", LeftColumnX, ListTop - 44f, 0f, HeadingFont, BoardInk.Heading));
                lines.Add(new BoardLine("MAP", LeftColumnX, ListTop - 20f, 0f, RowFont, BoardInk.Detail));
                lines.Add(new BoardLine("AIRCRAFT", RightColumnX, ListTop - 20f, 0f, RowFont, BoardInk.Detail));
                lines.Add(new BoardLine(
                    "Up / Down  Choose       Left / Right  Column       Enter / A / Click  Pick       Esc / B  Back",
                    0f, FooterY, BoardFit.AuthoredWidth, FooterFont, BoardInk.Detail, -1, false, BoardJustify.Center));
                break;
            case OriginalScreen.Options:
                lines.Add(new BoardLine("OPTIONS", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
                lines.Add(new BoardLine(
                    "The menu restarts at the chosen presentation's top level; unfinished setup is discarded.",
                    0f, FooterY, BoardFit.AuthoredWidth, FooterFont, BoardInk.Detail, -1, false, BoardJustify.Center));
                break;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool focused = i == focus;
            bool pressed = i == _pressed;
            switch (row.Kind)
            {
                case OriginalRowKind.Button when row.Art != null:
                    int stripFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                    plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i, stripFrame, string.Empty, BoardInk.LabelNormal));
                    break;
                case OriginalRowKind.TextButton:
                    var ink = row.Enabled ? ComposedBoard.PlaqueInk(focused, pressed) : BoardInk.Detail;
                    if (row.Art != null)
                    {
                        int plaqueFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                        plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i, plaqueFrame, row.Label, ink));
                    }
                    else
                    {
                        fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 255, 255, 255, 0.6f, Border: true));
                        lines.Add(new BoardLine(row.Label, row.X, row.Y + 4f, row.Width, RowFont, ink, i, false, BoardJustify.Center));
                    }

                    break;
                default:
                    if (IsPicked(row))
                    {
                        fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 255, 255, 255, 0.18f));
                    }

                    lines.Add(new BoardLine(row.Label, row.X + 6f, row.Y + 1f, row.Width - 12f, RowFont,
                        focused ? BoardInk.RowFocused : BoardInk.Row, i));
                    break;
            }
        }

        var overlays = new List<BoardPanel>();
        if (_pointer is { } at)
        {
            bool live = _hover >= 0 && rows[_hover].Enabled;
            overlays.Add(new BoardPanel(
                Array.Empty<BoardFill>(),
                new[] { new BoardPicture(live ? _activePointer : _passivePointer, at.X, at.Y) },
                Array.Empty<BoardLine>()));
        }

        return new ComposedBoard(pictures, Array.Empty<BoardStroke>(), lines, plaques,
            fills: fills, overlays: overlays);
    }

    private static OriginalInks ReadInks(MenuLayout layout, MenuLayoutWidget? plaque)
    {
        var disabled = layout.GlobalColor("DISABLED") ?? new MenuLayoutColor(255, 188, 188, 188);
        var active = layout.GlobalColor("ACTIVE") ?? new MenuLayoutColor(255, 255, 255, 255);
        var rollover = layout.GlobalColor("ROLLOVER") ?? active;
        var depressed = layout.GlobalColor("DEPRESSED") ?? new MenuLayoutColor(255, 0, 0, 0);
        var labelNormal = plaque != null && plaque.TryColor("ColorActive", out var n) ? n : active;
        var labelRollover = plaque != null && plaque.TryColor("ColorRollover", out var r) ? r : rollover;
        var labelDepressed = plaque != null && plaque.TryColor("ColorDepressed", out var d) ? d : depressed;
        return new OriginalInks(disabled, active, rollover, depressed, labelNormal, labelRollover, labelDepressed);
    }

    // The pointer bitmaps are named by the globals script, not by any layout row, so they are
    // read off the script-named asset list; the bare file name is the fallback.
    private static string PointerArt(MenuLayout layout, string fileName)
    {
        foreach (var asset in layout.ExternalAssets)
        {
            if (asset.Kind == "file" && asset.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                int slash = asset.Path.LastIndexOf('/');
                return slash >= 0 ? asset.Path[(slash + 1)..] : asset.Path;
            }
        }

        return fileName;
    }

    private static int HitTest(IReadOnlyList<OriginalRow> rows, float x, float y)
    {
        // Later rows draw over earlier ones, so the last hit wins.
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    private static int StepWithinColumn(IReadOnlyList<OriginalRow> rows, int focus, int dir)
    {
        if (focus < 0)
        {
            return focus;
        }

        int column = rows[focus].Column;
        int i = focus;
        for (int n = 0; n < rows.Count; n++)
        {
            i = (i + dir + rows.Count) % rows.Count;
            if (rows[i].Column == column && rows[i].Enabled)
            {
                return i;
            }
        }

        return focus;
    }

    private static int StepColumn(IReadOnlyList<OriginalRow> rows, int focus, int dir)
    {
        if (focus < 0)
        {
            return focus;
        }

        int columns = 0;
        foreach (var row in rows)
        {
            columns = Math.Max(columns, row.Column + 1);
        }

        if (columns < 2)
        {
            return focus;
        }

        int from = rows[focus].Column;
        int ordinal = OrdinalInColumn(rows, focus);
        int target = (from + dir + columns) % columns;
        int best = -1;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Column != target || !rows[i].Enabled)
            {
                continue;
            }

            int distance = Math.Abs(OrdinalInColumn(rows, i) - ordinal);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best >= 0 ? best : focus;
    }

    private static int OrdinalInColumn(IReadOnlyList<OriginalRow> rows, int index)
    {
        int ordinal = 0;
        for (int i = 0; i < index; i++)
        {
            if (rows[i].Column == rows[index].Column)
            {
                ordinal++;
            }
        }

        return ordinal;
    }

    private int EnsureFocus(IReadOnlyList<OriginalRow> rows)
    {
        int focus = _focus[(int)_screen];
        if (focus >= 0 && focus < rows.Count && rows[focus].Enabled)
        {
            return focus;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Enabled)
            {
                _focus[(int)_screen] = i;
                return i;
            }
        }

        _focus[(int)_screen] = -1;
        return -1;
    }

    private bool IsPicked(OriginalRow row) =>
        (row.Column == 0 && _pickedChapter >= 0 && row.Key == _chapters[_pickedChapter].Code)
        || (row.Column == 1 && _pickedAirframe >= 0 && row.Key == _airframes[_pickedAirframe].Node);

    private MenuExit? Activate(OriginalRow row, List<string> cues)
    {
        if (row.Kind != OriginalRowKind.ListRow)
        {
            cues.Add(OriginalCues.Click);
        }

        switch (_screen)
        {
            case OriginalScreen.TopLevel:
                switch (row.Key)
                {
                    case FreeFlightKey:
                        Open(OriginalScreen.FreeFlight);
                        break;
                    case "MM_B_PREFERENCES":
                        Open(OriginalScreen.Options);
                        break;
                    case "MM_B_QUIT":
                        return new QuitExit();
                }

                break;
            case OriginalScreen.FreeFlight:
                return ActivateFreeFlight(row);
            case OriginalScreen.Options:
                switch (row.Key)
                {
                    case PresentationKey:
                        _choice = _choice == PresentationId.Original.Value
                            ? PresentationId.BuiltIn.Value
                            : PresentationId.Original.Value;
                        break;
                    case ApplyKey:
                        return new PresentationSwitchExit(new PresentationId(_choice));
                    case BackKey:
                        Open(OriginalScreen.TopLevel);
                        break;
                }

                break;
        }

        return null;
    }

    private MenuExit? ActivateFreeFlight(OriginalRow row)
    {
        switch (row.Key)
        {
            case BackKey:
                Open(OriginalScreen.TopLevel);
                return null;
            case FlyKey:
                if (_pickedChapter < 0 || _pickedAirframe < 0)
                {
                    return null;
                }

                _free.SelectChapter(_chapters[_pickedChapter].Code);
                return _free.BuildExit(new[]
                {
                    new MenuSeatChoice(_airframes[_pickedAirframe].Node, Array.Empty<int>()),
                });
        }

        if (row.Column == 0)
        {
            for (int i = 0; i < _chapters.Count; i++)
            {
                if (_chapters[i].Code == row.Key)
                {
                    _pickedChapter = i;
                    _free.SelectChapter(row.Key);
                }
            }
        }
        else
        {
            for (int i = 0; i < _airframes.Count; i++)
            {
                if (_airframes[i].Node == row.Key)
                {
                    _pickedAirframe = i;
                }
            }
        }

        return null;
    }

    private MenuExit? Back()
    {
        if (_screen == OriginalScreen.TopLevel)
        {
            return new QuitExit();
        }

        Open(OriginalScreen.TopLevel);
        return null;
    }

    private IReadOnlyList<OriginalRow> BuildRows()
    {
        var rows = new List<OriginalRow>();
        switch (_screen)
        {
            case OriginalScreen.TopLevel:
                rows.Add(TextButton(FreeFlightKey, "FREE FLIGHT", DoorX, DoorY, true, 0));
                var main = _layout.Screen(OriginalAvailability.MainMenuSection);
                foreach (string key in TopLevelButtons)
                {
                    if (main?.Widget(key) is { } widget)
                    {
                        rows.Add(Button(widget, key is "MM_B_QUIT" or "MM_B_PREFERENCES"));
                    }
                }

                break;
            case OriginalScreen.FreeFlight:
                for (int i = 0; i < _chapters.Count; i++)
                {
                    rows.Add(new OriginalRow(_chapters[i].Code, _chapters[i].Label, OriginalRowKind.ListRow,
                        LeftColumnX, ListTop + (i * RowPitch), ListWidth, RowHeight, true, 0, null));
                }

                rows.Add(TextButton(BackKey, "BACK", LeftColumnX, PlaqueY, true, 0));
                for (int i = 0; i < _airframes.Count; i++)
                {
                    rows.Add(new OriginalRow(_airframes[i].Node, _airframes[i].Name, OriginalRowKind.ListRow,
                        RightColumnX, ListTop + (i * RowPitch), ListWidth, RowHeight, true, 1, null));
                }

                var flySize = PlaqueSize();
                rows.Add(TextButton(FlyKey, "FLY", RightColumnX + ListWidth - flySize.Width, PlaqueY,
                    _pickedChapter >= 0 && _pickedAirframe >= 0, 1));
                break;
            case OriginalScreen.Options:
                string choice = _choice == PresentationId.Original.Value ? "MENU: ORIGINAL" : "MENU: BUILT-IN";
                rows.Add(TextButton(PresentationKey, choice, OptionsX, OptionsTop, true, 0));
                rows.Add(TextButton(ApplyKey, "APPLY", OptionsX, OptionsTop + OptionsPitch, true, 0));
                rows.Add(TextButton(BackKey, "BACK", OptionsX, OptionsTop + (2 * OptionsPitch), true, 0));
                break;
        }

        return rows;
    }

    private OriginalRow Button(MenuLayoutWidget widget, bool enabled)
    {
        string art = widget.Art.Count > 0 ? widget.Art[0] : string.Empty;
        int frames = Math.Max(1, widget.Frames);
        var size = Measure(art);
        float width = size?.Width ?? FallbackButtonWidth;
        float height = size != null ? (float)Math.Floor(size.Value.Height / (float)frames) : FallbackButtonHeight;
        return new OriginalRow(widget.Key, string.Empty, OriginalRowKind.Button, widget.Int("X"), widget.Int("Y"),
            width, height, enabled, 0, art.Length > 0 ? new BoardArt(BoardArtLibrary.Ui, art, frames) : null);
    }

    private OriginalRow TextButton(string key, string label, float x, float y, bool enabled, int column)
    {
        var size = PlaqueSize();
        return new OriginalRow(key, label, OriginalRowKind.TextButton, x, y, size.Width, size.Height, enabled, column, _plaque);
    }

    private (float Width, float Height) PlaqueSize()
    {
        if (_plaque == null || Measure(_plaque.Name) is not { } size)
        {
            return (FallbackPlaqueWidth, FallbackPlaqueHeight);
        }

        return (size.Width, (float)Math.Floor(size.Height / (float)Math.Max(1, _plaque.Frames)));
    }

    private (int Width, int Height)? Measure(string art)
    {
        if (art.Length == 0)
        {
            return null;
        }

        if (!_sizes.TryGetValue(art, out var size))
        {
            size = _measure(art);
            _sizes[art] = size;
        }

        return size;
    }
}
