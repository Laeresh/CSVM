using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Original's Game Options page, composed from the decoded <c>[@GameOptions@]</c> rows: the page's
/// background and title, the shared options in the section's own row shape (a title in the title
/// column, a control in the control column, a description in the description column, at the
/// authored row pitch) and ACCEPT CHANGES and CANCEL CHANGES under them. The options are a table,
/// so a further one is an entry plus the store field it reads. The first row is the original's own
/// Difficulty dropdown at its authored place; remake-only is the Menu row under it, its words and
/// the control it takes. The display settings stand on the VIDEO page instead
/// (<see cref="VideoSection"/>). The decode and the readings are in <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the page is composed from.</summary>
    public const string GameOptionsSection = "GameOptions";

    /// <summary>The Preferences page's door onto it.</summary>
    public const string GameOptionsDoorKey = "PF_B_GAMEOPTIONS";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string GameOptionsAcceptKey = "GO_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to Preferences with the choices dropped.</summary>
    public const string GameOptionsCancelKey = "GO_B_CANCELCHANGES";

    // The page's authored row shape, used where a layout does not carry the section: the title
    // column and its box, the first row's line and the 62-pixel pitch, the dropdown box, the
    // checkbox's offset from its own row and the description column.
    // docs/org/menu-inventory.md holds the decode these come from.
    private const float GameOptionTitleX = 138f;
    private const float GameOptionTitleWidth = 170f;
    private const float GameOptionCheckTitleWidth = 112f;
    private const float GameOptionFirstY = 283f;
    private const float GameOptionPitch = 62f;
    private const float GameOptionDropX = 143f;
    private const float GameOptionDropDy = 16f;
    private const float GameOptionDropWidth = 144f;
    private const float GameOptionItemHeight = 17f;
    private const float GameOptionCheckDx = 122f;
    private const float GameOptionCheckDy = 3f;
    private const float GameOptionDescX = 346f;
    private const float GameOptionDescDy = 9f;
    private const float GameOptionDescWidth = 310f;

    private const float GameOptionTitleFont = 14f;
    private const float GameOptionDescFont = 12f;
    private const float FallbackCheckSize = 24f;

    // The three IDS_DIFFICULTY rows as the campaign selector labels them, the two shipped
    // presentation tokens as the dropdown's items.
    private static readonly string[] DifficultyWords =
    {
        CSVM.Flight.Difficulty.Label(CSVM.Flight.Difficulty.Normal),
        CSVM.Flight.Difficulty.Label(CSVM.Flight.Difficulty.Hard),
        CSVM.Flight.Difficulty.Label(CSVM.Flight.Difficulty.Hardest),
    };

    private static readonly string[] PresentationWords = { "ORIGINAL", "BUILT-IN" };

    // The page's options in their authored row order, each a title, a control, a description and
    // the words of the store field it reads and writes. A screen never saves: the apply exit
    // carries every choice and Launcher.ApplyOptions is the options file's one writer. The
    // difficulty row's title and description are IDS_GO_DIFFICULTY_TITLE and _DESC as authored;
    // the setting scales enemy armour and health at spawn and nothing about how the enemy flies.
    private static readonly GameOption[] GameOptions =
    {
        new(DifficultyKey, "Difficulty", _ => "Select the difficulty level for a solo campaign.",
            OriginalRowKind.Dropdown, DifficultyWords,
            s => CSVM.Flight.Difficulty.Clamp(s._difficulty),
            (s, i) => s._difficulty = CSVM.Flight.Difficulty.Clamp(i)),
        new(PresentationKey, "Menu", _ => "Select the menu presentation.", OriginalRowKind.Dropdown, PresentationWords,
            s => s._choice == PresentationId.BuiltIn.Value ? 1 : 0,
            (s, i) => s._choice = i == 1 ? PresentationId.BuiltIn.Value : PresentationId.Original.Value),
    };

    private string? _goOpen;

    /// <summary>The open option list's key, or null when none is open.</summary>
    public string? OpenGameOption => _goOpen;

    /// <summary>Opens the Game Options page on the saved options with its first row focused, which
    /// is what the Preferences page's GAME OPTIONS door and the screenshot aid both go through.
    /// The page is a form, not a list, so it opens on its first option rather than on wherever the
    /// cursor stood when it was last left.</summary>
    public void OpenGameOptions()
    {
        _goOpen = null;
        _focus[(int)OriginalScreen.GameOptions] = -1;
        Open(OriginalScreen.GameOptions);
    }

    // The saved options either option page shows back: what was asked for, not what this process
    // resolved, since a flag or the config key can have decided either and the page still owes the
    // player the words their own ACCEPT CHANGES saved. Both pages read every setting, since each
    // one's apply carries the settings it does not show unchanged. A page with no reader opens on
    // the shipped defaults, which is what an engine-free test wants.
    private void ReadSavedOptions()
    {
        var saved = _options?.Invoke();
        _choice = saved?.MenuPresentation ?? PresentationId.Original.Value;
        _graphics = saved?.GraphicsMode ?? CSVM.Utils.GraphicsMode.Default;
        _difficulty = CSVM.Flight.Difficulty.Parse(saved?.Difficulty) ?? CSVM.Flight.Difficulty.Normal;
        _monitorIndex = saved?.MonitorIndex;
        _resolution = saved?.Resolution;
        _displayMode = saved?.DisplayMode;
        _vsync = saved?.VSync;
    }

    // The apply exit both option pages leave through, carrying every setting the store holds: a
    // page writes the ones it shows and hands the rest back as ReadSavedOptions read them, which
    // is what keeps Launcher.ApplyOptions the options file's one writer.
    private OptionsApplyExit AppliedOptions() =>
        new(new PresentationId(_choice), _graphics, CSVM.Flight.Difficulty.Word(_difficulty),
            _monitorIndex, _resolution, _displayMode, _vsync);

    // The rows: an open list's items alone while one is open, else the option controls at their
    // authored rows and the two plaques under them, all one column. Without the section the
    // controls stand as text buttons so the page is still walkable.
    private void BuildGameOptionsRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(GameOptionsSection);
        if (screen == null)
        {
            for (int i = 0; i < GameOptions.Length; i++)
            {
                var option = GameOptions[i];
                rows.Add(TextButton(option.Key, option.Words[option.Read(this)], OptionsX, OptionsTop + (i * OptionsPitch), true, 0));
            }

            rows.Add(TextButton(GameOptionsAcceptKey, "ACCEPT CHANGES", OptionsX, OptionsTop + (GameOptions.Length * OptionsPitch), true, 0));
            rows.Add(TextButton(GameOptionsCancelKey, "CANCEL CHANGES", OptionsX, OptionsTop + ((GameOptions.Length + 1) * OptionsPitch), true, 0));
            return;
        }

        var page = ReadGameOptionsPage(screen);
        if (_goOpen != null && GameOptionFor(_goOpen) is { } open)
        {
            var box = page.DropBoxFor(IndexOfGameOption(_goOpen));
            for (int i = 0; i < open.Words.Count; i++)
            {
                rows.Add(new OriginalRow($"{_goOpen}:{i}", open.Words[i], OriginalRowKind.ListRow,
                    box.X, box.Y + (box.Height * (i + 1)), box.Width, box.Height, true, 0, null));
            }

            return;
        }

        BuildGameOptionsControls(screen, page, rows);
    }

    private void BuildGameOptionsControls(MenuLayoutScreen screen, GameOptionsPage page, List<OriginalRow> rows)
    {
        for (int i = 0; i < GameOptions.Length; i++)
        {
            var option = GameOptions[i];
            if (option.Kind == OriginalRowKind.Dropdown)
            {
                var box = page.DropBoxFor(i);
                rows.Add(new OriginalRow(option.Key, option.Words[option.Read(this)], OriginalRowKind.Dropdown,
                    box.X, box.Y, box.Width, box.Height, true, 0, page.Arrow));
                continue;
            }

            var size = StripSize(page.Box, FallbackCheckSize, FallbackCheckSize);
            rows.Add(new OriginalRow(option.Key, string.Empty, OriginalRowKind.Radio,
                page.TitleX + page.CheckDx, page.RowY(i) + page.CheckDy, size.Width, size.Height, true, 0, page.Box));
        }

        AddStrip(screen, rows, GameOptionsAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, GameOptionsCancelKey, OriginalRowKind.Button, true, 0);
    }

    // The page's row shape off the section's own widgets, each falling back to the authored number
    // when the row is not there: the title column, the first row's line and the pitch between the
    // authored rows, the dropdown box, the checkbox's offset from its row, the description column.
    private GameOptionsPage ReadGameOptionsPage(MenuLayoutScreen screen)
    {
        var first = screen.Widget("GO_T_DIFFTITLE");
        var second = screen.Widget("GO_T_VIEWTITLE");
        var third = screen.Widget("GO_T_HEADTITLE");
        var drop = screen.Widget("GO_D_DIFFICULTY");
        var box = screen.Widget("GO_B_HEADTURN");
        var description = screen.Widget("GO_T_DIFFDESC");
        float titleX = first?.Int("X", (int)GameOptionTitleX) ?? GameOptionTitleX;
        float firstY = first?.Int("Y", (int)GameOptionFirstY) ?? GameOptionFirstY;
        float pitch = first != null && second != null ? second.Int("Y") - first.Int("Y") : GameOptionPitch;
        return new GameOptionsPage(
            titleX,
            first?.Int("Width", (int)GameOptionTitleWidth) ?? GameOptionTitleWidth,
            third?.Int("Width", (int)GameOptionCheckTitleWidth) ?? GameOptionCheckTitleWidth,
            firstY,
            pitch > 0f ? pitch : GameOptionPitch,
            drop?.Int("X", (int)GameOptionDropX) ?? GameOptionDropX,
            drop != null ? drop.Int("Y") - firstY : GameOptionDropDy,
            drop?.Int("Width", (int)GameOptionDropWidth) ?? GameOptionDropWidth,
            drop?.Int("ItemHeight", (int)GameOptionItemHeight) ?? GameOptionItemHeight,
            box != null ? box.Int("X") - titleX : GameOptionCheckDx,
            box != null && third != null ? box.Int("Y") - third.Int("Y") : GameOptionCheckDy,
            description?.Int("X", (int)GameOptionDescX) ?? GameOptionDescX,
            description != null ? description.Int("Y") - firstY : GameOptionDescDy,
            description?.Int("Width", (int)GameOptionDescWidth) ?? GameOptionDescWidth,
            StripArt(drop?.Art ?? Array.Empty<string>(), 4),
            box != null ? StripArt(box.Art, 0, box.Frames) : null);
    }

    private int IndexOfGameOption(string key)
    {
        for (int i = 0; i < GameOptions.Length; i++)
        {
            if (GameOptions[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private GameOption? GameOptionFor(string key)
    {
        int at = IndexOfGameOption(key);
        return at >= 0 ? GameOptions[at] : null;
    }

    // A press: a list item picks and closes, a dropdown opens its list, a checkbox flips, ACCEPT
    // CHANGES leaves as the apply exit and CANCEL CHANGES drops the edits and goes back.
    private MenuExit? ActivateGameOptions(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0 && GameOptionFor(row.Key[..colon]) is { } picked)
        {
            picked.Write(this, int.Parse(row.Key[(colon + 1)..], CultureInfo.InvariantCulture));
            _goOpen = null;
            FocusKey(picked.Key);
            return null;
        }

        switch (row.Key)
        {
            case GameOptionsAcceptKey:
                return AppliedOptions();
            case GameOptionsCancelKey:
                BackToPreferences();
                return null;
        }

        if (GameOptionFor(row.Key) is not { } option)
        {
            return null;
        }

        if (option.Kind == OriginalRowKind.Dropdown && _layout.Screen(GameOptionsSection) != null)
        {
            _goOpen = option.Key;
            _focus[(int)_screen] = Math.Max(0, option.Read(this));
            return null;
        }

        option.Write(this, (option.Read(this) + 1) % option.Words.Count);
        return null;
    }

    // A sideways step on a focused option picks the next value with wrap, as the hangar's and the
    // Instant Action screen's dropdowns do. False on anything else, so the step crosses columns.
    private bool StepGameOptionValue(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || GameOptionFor(rows[focus].Key) is not { } option)
        {
            return false;
        }

        int count = option.Words.Count;
        option.Write(this, ((option.Read(this) + direction) % count + count) % count);
        FocusKey(option.Key);
        return true;
    }

    private bool CloseGameOptionsDropdown()
    {
        if (_goOpen == null)
        {
            return false;
        }

        string key = _goOpen;
        _goOpen = null;
        FocusKey(key);
        return true;
    }

    // Back from the page: an open list closes first, then the page leaves the way CANCEL CHANGES
    // does, since GO_B_CANCELCHANGES is the declining answer the layout gives the page.
    private void BackGameOptions()
    {
        if (!CloseGameOptionsDropdown())
        {
            BackToPreferences();
        }
    }

    private void BackToPreferences()
    {
        ReadSavedOptions();
        Open(OriginalScreen.Options);
    }

    // The page as drawn: the Preferences page's logo (this section authors none and the original
    // keeps it standing), the page's background, its title, then each option's title and
    // description at their authored columns, the controls, and an open list as the overlay.
    private void ComposeGameOptions(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        if (_layout.Screen(PreferencesSection)?.Widget("PF_LOGO") is { Art.Count: > 0 } logo)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], Math.Max(1, logo.Frames)),
                logo.Int("X"), logo.Int("Y")));
        }

        var screen = _layout.Screen(GameOptionsSection);
        if (screen == null)
        {
            lines.Add(new BoardLine("GAME OPTIONS", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            ComposeRows(rows, focus, fills, lines, plaques);
            return;
        }

        if (screen.Widget("GO_BACKGROUND") is { Art.Count: > 0 } background)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, background.Art[0], Math.Max(1, background.Frames)),
                background.Int("X"), background.Int("Y")));
        }

        if (screen.Widget("GO_T_TITLE") is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? "GAME OPTIONS", title.Int("X"), title.Int("Y"), title.Int("Width"),
                PreferencesTitleFont, BoardInk.Heading, -1, false,
                title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }

        var page = ReadGameOptionsPage(screen);
        for (int i = 0; i < GameOptions.Length; i++)
        {
            var option = GameOptions[i];
            lines.Add(new BoardLine(option.Title, page.TitleX, page.RowY(i), page.TitleWidthFor(option.Kind),
                GameOptionTitleFont, BoardInk.Row, -1, false,
                option.Kind == OriginalRowKind.Radio ? BoardJustify.Center : BoardJustify.Left));
            lines.Add(new BoardLine(option.Description(this), page.DescX, page.RowY(i) + page.DescDy, page.DescWidth,
                GameOptionDescFont, BoardInk.Row));
        }

        ComposeGameOptionsControls(screen, page, rows, focus, fills, lines, plaques, pictures, overlays);
    }

    // The controls in their states. With a list open the page under it is drawn from the closed
    // controls with the open one focused, and the items become the overlay, as the Instant Action
    // screen's own list does.
    private void ComposeGameOptionsControls(
        MenuLayoutScreen screen, GameOptionsPage page, IReadOnlyList<OriginalRow> rows, int focus,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures,
        List<BoardPanel> overlays)
    {
        var controls = rows;
        int controlFocus = focus;
        int controlPressed = _pressed;
        if (_goOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildGameOptionsControls(screen, page, closed);
            controls = closed;
            controlFocus = IndexOfGameOption(_goOpen);
            controlPressed = -1;
        }

        for (int i = 0; i < controls.Count; i++)
        {
            var control = controls[i];
            if (control.Kind == OriginalRowKind.Radio && control.Art != null)
            {
                // An eight-state strip: the four button states unchecked, then the same four checked.
                int state = control.Enabled ? (i == controlPressed ? 3 : i == controlFocus ? 2 : 1) : 0;
                int frame = (GameOptionFor(control.Key)?.Read(this) == 1 ? 4 : 0) + state;
                plaques.Add(new BoardPlaque(control.Art, control.X, control.Y, i, frame, string.Empty, BoardInk.LabelNormal));
                continue;
            }

            ComposeInstantActionRow(control, i == controlFocus, i == controlPressed, i, fills, lines, plaques, pictures);
        }

        if (_goOpen != null && rows.Count > 0)
        {
            overlays.Add(ComposeOptionList(rows, focus));
        }
    }

    // An open option list as the overlay, drawn by both option pages: the items are already rows,
    // so this is their panel and nothing more.
    private BoardPanel ComposeOptionList(IReadOnlyList<OriginalRow> rows, int focus)
    {
        var panelFills = new List<BoardFill>();
        var panelLines = new List<BoardLine>();
        var first = rows[0];
        var last = rows[rows.Count - 1];
        float height = last.Y + last.Height - first.Y;
        // A dark panel in the plate's own key, not the white one the paper pages open: this page
        // writes in the section's pale text colour, which no white ground would carry.
        panelFills.Add(new BoardFill(first.X, first.Y, first.Width, height, 16, 14, 12, 0.94f));
        panelFills.Add(new BoardFill(first.X, first.Y, first.Width, height, 200, 190, 170, 1f, Border: true));
        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            if (i == focus)
            {
                panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 255, 255, 255, 0.18f));
            }

            panelLines.Add(new BoardLine(item.Label, item.X + 4f, item.Y + 2f, item.Width - 8f, GameOptionDescFont,
                i == focus ? BoardInk.RowFocused : BoardInk.Row, i));
        }

        return new BoardPanel(panelFills, Array.Empty<BoardPicture>(), panelLines);
    }

    // One option of the page: its title, its description (read off the shell, since a row can say
    // something about its saved state), the control it takes, the words of the store field it
    // shows, and how that field is read and written.
    private sealed record GameOption(
        string Key, string Title, Func<OriginalShell, string> Description, OriginalRowKind Kind, IReadOnlyList<string> Words,
        Func<OriginalShell, int> Read, Action<OriginalShell, int> Write);

    // The page's row shape in authored pixels, every number off the section's own widgets: the
    // title column, the first row's line and the pitch between rows, the dropdown box, the
    // checkbox's offset from its row, the description column, and the two controls' strips.
    private sealed record GameOptionsPage(
        float TitleX, float TitleWidth, float CheckTitleWidth, float FirstY, float Pitch,
        float DropX, float DropDy, float DropWidth, float ItemHeight,
        float CheckDx, float CheckDy, float DescX, float DescDy, float DescWidth,
        BoardArt? Arrow, BoardArt? Box)
    {
        public float RowY(int row) => FirstY + (row * Pitch);

        // A checkbox row takes the head-turn row's own narrower title box, which is what leaves the
        // box beside it clear of the words; a dropdown row takes the wide one.
        public float TitleWidthFor(OriginalRowKind kind) =>
            kind == OriginalRowKind.Radio ? CheckTitleWidth : TitleWidth;

        public (float X, float Y, float Width, float Height) DropBoxFor(int row) =>
            (DropX, RowY(Math.Max(0, row)) + DropDy, DropWidth, ItemHeight);
    }
}
