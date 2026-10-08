using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Game Options page behind the Options hub, one page module over the decoded
/// <c>[@GameOptions@]</c> section. It is a table of options, each a key, a title, a control and
/// the store field it reads and writes. The plate grows a whole band of its own art per row past
/// the three it is painted with, its plaques moving down with it. It stages its five settings in
/// the form's shared <see cref="OptionsChoices"/> and leaves through the form's apply exit (<see cref="IOriginalOptionsForm"/>).
/// The rows and their readings are in <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed class OriginalGameOptionsPage : IOriginalOptionsPage
{
    /// <summary>The Game Options page's layout section.</summary>
    public const string Section = "GameOptions";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string AcceptKey = "GO_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to the hub with the choices dropped.</summary>
    public const string CancelKey = "GO_B_CANCELCHANGES";

    /// <summary>The page's difficulty dropdown, the page's first row.</summary>
    public const string DifficultyKey = "DIFFICULTY";

    /// <summary>The page's Default View dropdown, the original's own second row.</summary>
    public const string DefaultViewKey = "DEFAULTVIEW";

    /// <summary>The page's Auto Head Turn checkbox, the original's own third row.</summary>
    public const string AutoHeadTurnKey = "AUTOHEADTURN";

    /// <summary>The page's next-target checkbox.</summary>
    public const string NearestAfterKillKey = "NEARESTAFTERKILL";

    /// <summary>The page's controller-rumble checkbox.</summary>
    public const string RumbleKey = "RUMBLE";

    // The one dropdown the Game Options section authors, whose box, window and scroll art every
    // option row of that page takes. The page authors a single D row, and the rows stand at its
    // column.
    private const string DropKey = "GO_D_DIFFICULTY";

    // The page's authored row shape, used where a layout does not carry the section. It is the
    // title column and its box, the first row's line and the 62-pixel pitch, and the dropdown box.
    // The checkbox's offset from its own row and the description column follow.
    // The decode these come from is in docs/org/menu-inventory.md.
    private const float TitleX = 138f;
    private const float TitleWidth = 170f;
    private const float CheckTitleWidth = 112f;
    private const float FirstY = 283f;
    private const float Pitch = 62f;
    private const float DropX = 143f;
    private const float DropDy = 16f;
    private const float DropWidth = 144f;
    private const float ItemHeight = 17f;
    private const float CheckDx = 122f;
    private const float CheckDy = 3f;
    private const float DescX = 346f;
    private const float DescDy = 9f;
    private const float DescWidth = 310f;

    // The plate the section authors and how many rows it holds. GO_BackGround.png draws three
    // raised row panels at the authored pitch over one grey description window, and the section
    // stands three rows on it. A page carrying more grows the plate by whole bands of its own art.
    private const int AuthoredRows = 3;

    // The band a repeat takes, in the plate art's own pixels. Y 116 to 178 is one whole row panel,
    // both seams inside the dark gaps the art leaves between panels. Those gaps are 112 to 119 and
    // 174 to 181. A repeat therefore cuts no rivet, no panel edge and no window border, and the
    // description window grows with the rows. A vertical stretch would smear all three.
    private const int PlateBandY = 116;
    private const int PlateBandHeight = 62;

    // The first authored row's inset from the top of its band, used where no plate is there to
    // measure it from. A band opens one band above the head crop's bottom edge.
    private const float BandInsetFallback = 14f;

    // The three IDS_DIFFICULTY rows as the campaign selector labels them.
    private static readonly string[] DifficultyWords =
    {
        CSVM.Flight.Hangar.Difficulty.Label(CSVM.Flight.Hangar.Difficulty.Normal),
        CSVM.Flight.Hangar.Difficulty.Label(CSVM.Flight.Hangar.Difficulty.Hard),
        CSVM.Flight.Hangar.Difficulty.Label(CSVM.Flight.Hangar.Difficulty.Hardest),
    };

    // The Default View dropdown's own three items, the words and the order the original's list
    // carries. They come from CSVM.Flight.Camera.PilotView.Selectable and .Label, decoded from uiData 2127
    // in crimson.exe (docs/org/menu-inventory.md holds the addresses).
    private static readonly string[] DefaultViewWords =
    {
        CSVM.Flight.Camera.PilotView.Label(CSVM.Flight.Camera.PilotView.Selectable[0]),
        CSVM.Flight.Camera.PilotView.Label(CSVM.Flight.Camera.PilotView.Selectable[1]),
        CSVM.Flight.Camera.PilotView.Label(CSVM.Flight.Camera.PilotView.Selectable[2]),
    };

    // The checkbox's two words, in the order its eight-frame strip reads them: index 0 unchecked,
    // index 1 checked. Only the page with no section draws them, as a text button's label.
    private static readonly string[] SwitchWords = { "OFF", "ON" };

    // The page's options in their authored row order. Each is a title, a control, a description
    // and the words of the store field it reads and writes. A page never saves. The apply exit
    // carries every choice, and Launcher.ApplyOptions is the options file's one writer.
    // The difficulty row's title and description are IDS_GO_DIFFICULTY_TITLE and _DESC as authored.
    // The setting scales enemy armour and health at spawn and nothing about how the enemy flies.
    private static readonly GameOption[] Options =
    {
        new(DifficultyKey, "Difficulty", _ => "Select the difficulty level for a solo campaign.",
            OriginalRowKind.Dropdown, DifficultyWords,
            c => CSVM.Flight.Hangar.Difficulty.Clamp(c.Difficulty),
            (c, i) => c.Difficulty = CSVM.Flight.Hangar.Difficulty.Clamp(i)),
        new(DefaultViewKey, "Default View", _ => "Select your default view.",
            OriginalRowKind.Dropdown, DefaultViewWords,
            c => IndexOfView(c.DefaultViewMode),
            (c, i) => c.DefaultView = CSVM.Flight.Camera.PilotView.Name(
                CSVM.Flight.Camera.PilotView.Selectable[Math.Clamp(i, 0, CSVM.Flight.Camera.PilotView.Selectable.Count - 1)])),
        new(AutoHeadTurnKey, "Auto Head Turn",
            _ => "Select to turn your head automatically as your aircraft turns.",
            OriginalRowKind.Radio, SwitchWords,
            c => c.AutoHeadTurnOn ? 1 : 0,
            (c, i) => c.AutoHeadTurn = i == 1),
        new(NearestAfterKillKey, "Next Target",
            _ => "Take the nearest target after a kill instead of the first of the list.",
            OriginalRowKind.Radio, SwitchWords,
            c => c.NearestAfterKillOn ? 1 : 0,
            (c, i) => c.NearestAfterKill = i == 1),
        new(RumbleKey, "Rumble",
            _ => "Rumble the gamepad for guns, launches, hits, the nitro and a dive past the rated maximum.",
            OriginalRowKind.Radio, SwitchWords,
            c => c.RumbleOn ? 1 : 0,
            (c, i) => c.Rumble = i == 1),
    };

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly IOriginalOptionsForm _form;

    // The form's staged settings, shared with the other two settings pages.
    private readonly OptionsChoices _choices;

    private string? _open;
    private int _listTop;

    internal OriginalGameOptionsPage(OriginalOptionsChrome chrome, IOriginalOptionsForm form, OptionsChoices choices)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _choices = choices ?? throw new ArgumentNullException(nameof(choices));
        _host = chrome.Host;
    }

    /// <summary>The page's open option list's key, or null when none is open.</summary>
    public string? OpenOption => _open;

    /// <summary>The difficulty tier (<see cref="CSVM.Flight.Hangar.Difficulty"/>) the page would
    /// apply.</summary>
    public int DifficultyChoice => _choices.Difficulty;

    /// <summary>The targeting setting the page would apply, or null while nothing has been saved
    /// and no row has been touched.</summary>
    public bool? NearestAfterKillChoice => _choices.NearestAfterKill;

    /// <summary>The haptics setting the page would apply, or null while nothing has been saved and
    /// no row has been touched.</summary>
    public bool? RumbleChoice => _choices.Rumble;

    /// <summary>The opening view the page would apply, a
    /// <see cref="CSVM.Flight.Camera.PilotView.Name"/> word, or null while nothing has been saved and
    /// no row has been touched.</summary>
    public string? DefaultViewChoice => _choices.DefaultView;

    /// <summary>The automatic head turn the page would apply. It is null while nothing has been
    /// saved and no row has been touched, which leaves the config key deciding.</summary>
    public bool? AutoHeadTurnChoice => _choices.AutoHeadTurn;

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.GameOptions;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    /// <summary>Opens the page on the saved options with its first row focused. The hub's GAME
    /// OPTIONS door and the screenshot aid both go through it. The page is a form, not a list. It
    /// opens on its first option rather than where the cursor last stood.</summary>
    public void Open()
    {
        _open = null;
        _listTop = 0;
        _host.Open(OriginalScreen.GameOptions);
        _host.FocusedRow = -1;
    }

    // The rows. An open list's items stand alone while one is open. Otherwise the option controls
    // stand at their authored rows with the two plaques under them, all one column. Without the
    // section the controls stand as text buttons so the page is still walkable.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            for (int i = 0; i < Options.Length; i++)
            {
                var option = Options[i];
                rows.Add(_host.PlaqueRow(option.Key, option.Words[option.Read(_choices)], i, true, 0));
            }

            _chrome.AddFallbackPlaques(rows, AcceptKey, CancelKey, Options.Length);
            return;
        }

        if (OpenDrop() is { } drop)
        {
            _listTop = OriginalOptionsChrome.DropListTop(drop, _listTop, _host.FocusedRow);
            _chrome.AddDropListRows(drop, _listTop, rows);
            return;
        }

        BuildControls(screen, ReadShape(screen), rows);
    }

    // The page's lists for the pointer: an open option list alone, and only once it outruns the
    // authored window.
    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
        if (OpenDrop() is { } drop && _chrome.DropListWindow(drop, _listTop) is { } window)
        {
            lists.Add(new OriginalList(drop.Key, window, top => _listTop = _chrome.ScrollDropList(drop, top)));
        }
    }

    // A sideways step on a focused option picks the next value with wrap, as the hangar's and the
    // Instant Action screen's dropdowns do. False on anything else, so the step crosses columns.
    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || OptionFor(rows[focus].Key) is not { } option)
        {
            return false;
        }

        int count = option.Words.Count;
        option.Write(_choices, ((option.Read(_choices) + direction) % count + count) % count);
        _host.FocusKey(option.Key);
        return true;
    }

    bool IOriginalOptionsPage.CloseDropdown() => CloseDropdown();

    // What a press does. A list item picks and closes, a dropdown opens its list, a checkbox flips.
    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0 && OptionFor(row.Key[..colon]) is { } picked)
        {
            string suffix = row.Key[(colon + 1)..];
            if (suffix is OriginalDropLists.UpSuffix or OriginalDropLists.DownSuffix)
            {
                if (OpenDrop() is { } open)
                {
                    _listTop = _chrome.ScrollDropList(open, _listTop + (suffix == OriginalDropLists.UpSuffix ? -1 : 1));
                }

                return null;
            }

            picked.Write(_choices, int.Parse(suffix, CultureInfo.InvariantCulture));
            _open = null;
            _host.FocusKey(picked.Key);
            return null;
        }

        if (OptionFor(row.Key) is not { } option)
        {
            return null;
        }

        if (option.Kind == OriginalRowKind.Dropdown && _chrome.Layout.Screen(Section) != null)
        {
            _open = option.Key;
            _listTop = 0;
            _host.FocusedRow = Math.Max(0, option.Read(_choices));
            return null;
        }

        option.Write(_choices, (option.Read(_choices) + 1) % option.Words.Count);
        return null;
    }

    MenuExit? IOriginalOptionsPage.Accept() => _form.Apply();

    void IOriginalOptionsPage.Cancel() => _form.Leave();

    // An open list closes first, then the page leaves the way CANCEL CHANGES does.
    // GO_B_CANCELCHANGES is the declining answer the layout gives the page.
    void IOriginalOptionsPage.Back()
    {
        if (!CloseDropdown())
        {
            _form.Leave();
        }
    }

    // The plate, the title, each option's title and description, the controls and an open list as
    // the overlay. ⚠ Keep the plate in the backdrop, never among the pictures. A board draws its
    // fills between the two layers, so a plate there buries every focus mark the rows compose.
    void IOriginalOptionsPage.Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            _host.ComposePlainPage("GAME OPTIONS", rows, focus, layers);
            return;
        }

        var shape = ReadShape(screen);
        ComposePlate(screen, shape.ExtraRows, layers.Backdrop);
        _chrome.ComposePageTitle(screen, "GO_T_TITLE", "GAME OPTIONS", layers.Lines);
        for (int i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            layers.Lines.Add(new BoardLine(option.Title, shape.TitleX, shape.TitleYFor(i, option.Kind),
                shape.TitleWidthFor(option.Kind), OriginalOptionsChrome.TitleFont, BoardInk.Row));
            layers.Lines.Add(new BoardLine(option.Description(this), shape.DescX, shape.DescY(i), shape.DescWidth,
                OriginalOptionsChrome.DescriptionFont, BoardInk.Row));
        }

        // With a list open the page under it is drawn from the closed controls with the open one
        // focused. The items become the overlay, as the Instant Action screen's own list does.
        IReadOnlyList<OriginalRow> controls = rows;
        int controlFocus = focus;
        int controlPressed = _host.PressedRow;
        if (_open != null)
        {
            var closed = new List<OriginalRow>();
            BuildControls(screen, shape, closed);
            controls = closed;
            controlFocus = IndexOf(_open);
            controlPressed = -1;
        }

        _chrome.ComposeControls(controls, controlFocus, controlPressed, key => OptionFor(key)?.Read(_choices) == 1, layers);
        if (OpenDrop() is { } drop && rows.Count > 0)
        {
            layers.Overlays.Add(_chrome.ComposeOptionList(drop, _listTop, rows, focus));
        }
    }

    // Where a view stands in the Default View dropdown's own list.
    private static int IndexOfView(CSVM.Flight.Camera.PilotViewMode mode)
    {
        for (int i = 0; i < CSVM.Flight.Camera.PilotView.Selectable.Count; i++)
        {
            if (CSVM.Flight.Camera.PilotView.Selectable[i] == mode)
            {
                return i;
            }
        }

        return 0;
    }

    // Where each row's own line falls on the plate, or null where the rows do not fit its bands. A
    // dropdown takes a band to itself, its open list standing under the box. The checkbox rows take
    // the bands left over, paired from the top of their run where they outnumber the bands. The
    // bottom band then keeps one row, its description clear of the plaques under it. A pair's upper
    // row stands at its band's own top, inset above the authored line. The lower one stands a
    // checkbox below it, the only way two boxes fit a band drawn for one.
    private static float[]? BandedLines(float firstY, float pitch, float inset, float checkHeight, int bands)
    {
        int drops = 0;
        foreach (var option in Options)
        {
            drops += option.Kind == OriginalRowKind.Dropdown ? 1 : 0;
        }

        int spare = bands - drops;
        if (spare < 1 || Options.Length - drops > 2 * spare)
        {
            return null;
        }

        var lines = new float[Options.Length];
        int pairs = Math.Max(0, Options.Length - drops - spare);
        int band = -1;
        int held = 0;
        int allow = 0;
        for (int i = 0; i < Options.Length; i++)
        {
            bool drop = Options[i].Kind == OriginalRowKind.Dropdown;
            if (drop || held >= allow)
            {
                band++;
                held = 0;
                allow = !drop && pairs > 0 ? 2 : 1;
                pairs -= allow == 2 ? 1 : 0;
            }

            if (band >= bands)
            {
                return null;
            }

            float line = firstY + (band * pitch);
            lines[i] = allow == 1 ? line : line - inset + (held * checkHeight);
            held++;
        }

        return lines;
    }

    // The first authored row's own inset from the top of its band, measured off the plate. The head
    // crop ends one band below that band's own top, so the band opens at the plate's corner plus the
    // difference. Clamped at zero, a layout standing its first row above its own plate stating
    // nothing about where a band opens.
    private static float BandInset(MenuLayoutScreen screen, float firstY) =>
        screen.Widget("GO_BACKGROUND") is { } plate
            ? Math.Max(0f, firstY - plate.Int("Y") - (PlateBandY - PlateBandHeight))
            : BandInsetFallback;

    // The rows one under another at a single pitch, the placement a page falls back to when its rows
    // outrun the plate's bands.
    private static float[] TightLines(float firstY, float pitch)
    {
        var lines = new float[Options.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = firstY + (i * pitch);
        }

        return lines;
    }

    // Whether every row's reach stops above the moved plaque line. ⚠ Never draw a row over the
    // button: the press regions would overlap and one pointer press would land on two rows.
    private static bool LinesClearThePlaques(float[] lines, float below, MenuLayoutWidget? accept, float plaqueDy)
    {
        if (accept == null)
        {
            return true;
        }

        float limit = accept.Int("Y") + plaqueDy;
        foreach (float line in lines)
        {
            if (line + below > limit)
            {
                return false;
            }
        }

        return true;
    }

    // The authored section carries three rows and this page holds more. The plate grows a whole
    // band per extra row and takes the plaques down with it. The authored pitch stands for as many
    // rows as the canvas holds bands. Past that the rows tighten into the space between the first
    // line and the moved button. Here below is how far a row reaches under its own line, plaqueDy
    // how far the growth took the button down.
    private static float FitPitch(float authored, float firstY, float below, MenuLayoutWidget? accept, float plaqueDy)
    {
        if (accept == null || Options.Length < 2)
        {
            return authored;
        }

        // Floored to a whole point: the authored pitches are integers and a fractional one would put
        // every row below the first on a half pixel.
        float fit = MathF.Floor((accept.Int("Y") + plaqueDy - firstY - below) / (Options.Length - 1));
        return fit > 0f && fit < authored ? fit : authored;
    }

    private static int IndexOf(string key)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            if (Options[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private static GameOption? OptionFor(string key)
    {
        int at = IndexOf(key);
        return at >= 0 ? Options[at] : null;
    }

    private bool CloseDropdown()
    {
        if (_open == null)
        {
            return false;
        }

        string key = _open;
        _open = null;
        _host.FocusKey(key);
        return true;
    }

    // The open option's list, or null while none is open. It is that option's words under the
    // authored dropdown's box on its own row, windowed by the TotalDisplayed the section authors.
    private OpenDropList? OpenDrop()
    {
        if (_open is not { } key || _chrome.Layout.Screen(Section) is not { } screen
            || OptionFor(key) is not { } open)
        {
            return null;
        }

        return OriginalOptionsChrome.DropList(key, screen.Widget(DropKey), open.Words,
            ReadShape(screen).DropBoxFor(IndexOf(key)));
    }

    private void BuildControls(MenuLayoutScreen screen, GameOptionsShape shape, List<OriginalRow> rows)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            if (option.Kind == OriginalRowKind.Dropdown)
            {
                var box = shape.DropBoxFor(i);
                rows.Add(new OriginalRow(option.Key, option.Words[option.Read(_choices)], OriginalRowKind.Dropdown,
                    box.X, box.Y, box.Width, box.Height, true, 0, shape.Arrow));
                continue;
            }

            var size = _chrome.StripSize(shape.Box, OriginalOptionsChrome.FallbackCheckSize, OriginalOptionsChrome.FallbackCheckSize);
            rows.Add(new OriginalRow(option.Key, string.Empty, OriginalRowKind.Radio,
                shape.TitleX + shape.CheckDx, shape.RowY(i) + shape.CheckDy, size.Width, size.Height, true, 0, shape.Box));
        }

        _chrome.AddPlaques(screen, rows, AcceptKey, CancelKey, shape.PlaqueDy);
    }

    // The page's row shape off the section's own widgets, each number falling back to the authored
    // one when the row is not there. It is the title column, the first row's line and the pitch
    // between the authored rows. The dropdown box, the checkbox's offset from its row and the
    // description column follow.
    private GameOptionsShape ReadShape(MenuLayoutScreen screen)
    {
        var first = screen.Widget("GO_T_DIFFTITLE");
        var second = screen.Widget("GO_T_VIEWTITLE");
        var third = screen.Widget("GO_T_HEADTITLE");
        var drop = screen.Widget(DropKey);
        var box = screen.Widget("GO_B_HEADTURN");
        var description = screen.Widget("GO_T_DIFFDESC");
        float titleX = first?.Int("X", (int)TitleX) ?? TitleX;
        float firstY = first?.Int("Y", (int)FirstY) ?? FirstY;
        float pitch = first != null && second != null ? second.Int("Y") - first.Int("Y") : Pitch;
        float descDy = description != null ? description.Int("Y") - firstY : DescDy;
        float dropDy = drop != null ? drop.Int("Y") - firstY : DropDy;
        float dropHeight = drop?.Int("ItemHeight", (int)ItemHeight) ?? ItemHeight;
        var checkArt = box != null ? OriginalOptionsChrome.StripArt(box.Art, 0, box.Frames) : null;
        float checkDy = box != null && third != null ? box.Int("Y") - third.Int("Y") : CheckDy;
        float checkHeight = _chrome.StripSize(checkArt, OriginalOptionsChrome.FallbackCheckSize, OriginalOptionsChrome.FallbackCheckSize).Height;
        float below = Math.Max(descDy + OriginalOptionsChrome.DescriptionFont,
            Math.Max(dropDy + dropHeight, checkDy + checkHeight));
        int extraRows = ExtraRows(screen);
        float plaqueDy = extraRows * PlateBandHeight;
        var accept = screen.Widget(AcceptKey);
        pitch = pitch > 0f ? pitch : Pitch;
        var lines = BandedLines(firstY, pitch, BandInset(screen, firstY), checkHeight, AuthoredRows + extraRows);
        if (lines == null || !LinesClearThePlaques(lines, below, accept, plaqueDy))
        {
            lines = TightLines(firstY, FitPitch(pitch, firstY, below, accept, plaqueDy));
        }

        return new GameOptionsShape(
            titleX,
            first?.Int("Width", (int)TitleWidth) ?? TitleWidth,
            third?.Int("Width", (int)CheckTitleWidth) ?? CheckTitleWidth,
            firstY,
            lines,
            drop?.Int("X", (int)DropX) ?? DropX,
            dropDy,
            drop?.Int("Width", (int)DropWidth) ?? DropWidth,
            dropHeight,
            box != null ? box.Int("X") - titleX : CheckDx,
            checkDy,
            checkHeight,
            description?.Int("X", (int)DescX) ?? DescX,
            descDy,
            description?.Int("Width", (int)DescWidth) ?? DescWidth,
            OriginalOptionsChrome.StripArt(drop?.Art ?? Array.Empty<string>(), 4),
            checkArt,
            extraRows);
    }

    // The plate grows by whole bands of its own art rather than stretching. A page with more rows
    // than the section authors still stands on art at its authored scale. The pieces are the head
    // above the band, that band once per row, then the tail below it. An extraRows of 0 draws the
    // one authored picture and nothing else.
    // ⚠ The band is the art's, not the row pitch a layout reads. The seams are chosen against the
    // bitmap's own gaps, so a section authoring another pitch still repeats these 62 pixels.
    private void ComposePlate(MenuLayoutScreen screen, int extraRows, List<BoardPicture> backdrop)
    {
        if (screen.Widget("GO_BACKGROUND") is not { Art.Count: > 0 } plate)
        {
            return;
        }

        var art = new BoardArt(BoardArtLibrary.Ui, plate.Art[0], Math.Max(1, plate.Frames));
        float x = plate.Int("X");
        float y = plate.Int("Y");
        if (extraRows <= 0 || _host.Measure(plate.Art[0]) is not { } size)
        {
            backdrop.Add(new BoardPicture(art, x, y));
            return;
        }

        backdrop.Add(new BoardPicture(art, x, y, Crop: new BoardCrop(0f, 0f, size.Width, PlateBandY)));
        for (int i = 0; i <= extraRows; i++)
        {
            backdrop.Add(new BoardPicture(art, x, y + PlateBandY + (i * PlateBandHeight),
                Crop: new BoardCrop(0f, PlateBandY, size.Width, PlateBandHeight)));
        }

        float below = PlateBandY + PlateBandHeight;
        backdrop.Add(new BoardPicture(art, x, y + below + (extraRows * PlateBandHeight),
            Crop: new BoardCrop(0f, below, size.Width, size.Height - below)));
    }

    // How many whole bands the plate grows by, one per row past the three the section authors. The
    // count is capped at what the authored canvas still holds under the plate's own corner. A grown
    // plate that ran off the canvas would put its bottom band and the two plaques on it out of
    // sight. The rows past the cap tighten instead (FitPitch).
    private int ExtraRows(MenuLayoutScreen screen)
    {
        int wanted = Math.Max(0, Options.Length - AuthoredRows);
        if (wanted == 0 || screen.Widget("GO_BACKGROUND") is not { Art.Count: > 0 } plate
            || _host.Measure(plate.Art[0]) is not { } size)
        {
            return 0;
        }

        int room = (int)Math.Floor(
            (BoardFit.AuthoredHeight - plate.Int("Y") - size.Height) / (float)PlateBandHeight);
        return Math.Clamp(room, 0, wanted);
    }

    // One option of the page. It carries its title and its description, read off the page since a
    // row can say something about its saved state. The control it takes, the words of the store
    // field it shows, and how that field is read and written follow.
    private sealed record GameOption(
        string Key, string Title, Func<OriginalGameOptionsPage, string> Description, OriginalRowKind Kind,
        IReadOnlyList<string> Words,
        Func<OptionsChoices, int> Read, Action<OptionsChoices, int> Write);

    // The page's row shape in authored pixels, every number off the section's own widgets. It is
    // the title column, the first row's line and every row's own line. The dropdown box, the
    // checkbox's offset from its row and its own height, the description column and the two
    // controls' strips follow.
    private sealed record GameOptionsShape(
        float TitleX, float TitleWidth, float CheckTitleWidth, float FirstY, IReadOnlyList<float> Lines,
        float DropX, float DropDy, float DropWidth, float ItemHeight,
        float CheckDx, float CheckDy, float CheckHeight, float DescX, float DescDy, float DescWidth,
        BoardArt? Arrow, BoardArt? Box, int ExtraRows)
    {
        // How far the plate's growth took the two plaques and everything else standing on its
        // bottom band down from their authored line.
        public float PlaqueDy => ExtraRows * PlateBandHeight;

        public float RowY(int row) => Lines[Math.Clamp(row, 0, Lines.Count - 1)];

        // Where a row's description stands in the column beside it. That column is one window rather
        // than a row of panels. The paragraphs are spread evenly from the first row's line to the last
        // row's, not crowded where two rows share a band. Each still stands beside its own control.
        // With every row on a band of its own this is the authored offset again.
        public float DescY(int row)
        {
            float first = RowY(0) + DescDy;
            float last = RowY(Lines.Count - 1) + DescDy;
            return Lines.Count < 2
                ? first
                : MathF.Floor(first + ((last - first) * Math.Clamp(row, 0, Lines.Count - 1) / (Lines.Count - 1)));
        }

        // Where a row's own words stand. A dropdown row's title keeps the row's line, its box
        // opening under it. A checkbox row's title stands on its box's centre line instead, the box
        // being taller than the face and standing beside the words. That is what the VIDEO section
        // authors for the same pair, its clutter title seven pixels under a 29-pixel box against a
        // 14-pixel face. A line is drawn from the top of its own em box, so half the difference
        // between the two centres it.
        public float TitleYFor(int row, OriginalRowKind kind) =>
            kind == OriginalRowKind.Radio
                ? RowY(row) + CheckDy + MathF.Floor((CheckHeight - OriginalOptionsChrome.TitleFont) / 2f)
                : RowY(row);

        // A checkbox row takes the head-turn row's own narrower title box, which leaves the box
        // beside it clear of the words. A dropdown row takes the wide one.
        public float TitleWidthFor(OriginalRowKind kind) =>
            kind == OriginalRowKind.Radio ? CheckTitleWidth : TitleWidth;

        public (float X, float Y, float Width, float Height) DropBoxFor(int row) =>
            (DropX, RowY(Math.Max(0, row)) + DropDy, DropWidth, ItemHeight);
    }
}
