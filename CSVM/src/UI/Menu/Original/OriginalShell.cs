using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>The Original presentation's screens. The top level, the Instant Action screen, the
/// campaign's screens and the hangar's screens are decoded; the others are remake-only screens
/// composed in the decoded chrome's conventions. The campaign's members run from
/// <see cref="CampaignRoster"/> to <see cref="CampaignScrapbookZoom"/> and the hangar's sit last,
/// from <see cref="PlaneName"/> on, which is what the shell reads its two branches off.</summary>
public enum OriginalScreen
{
    /// <summary>The main menu: the decoded <c>[@MainMenu@]</c> rows plus the Free Flight, Dogfight and hangar doors.</summary>
    TopLevel,

    /// <summary>The remake-only Free Flight screen: a chapter list, the aircraft list, the seats, BACK and FLY.</summary>
    FreeFlight,

    /// <summary>The remake-only Dogfight screen: the Free Flight screen's shape over the Dogfight gate.</summary>
    Dogfight,

    /// <summary>The Options screen: the decoded <c>[@Preferences@]</c> chrome with the
    /// presentation chooser as its content.</summary>
    Options,

    /// <summary>The decoded <c>[@InstantAction@]</c> setup screen: the Table of Contents, the
    /// dropdowns, the paged enemy rows, the radio pair and its buttons.</summary>
    InstantAction,

    /// <summary>The decoded <c>[@Campaign@]</c> player profile screen: the name box, the roster,
    /// CONTINUE, DELETE PLAYER and CANCEL.</summary>
    CampaignRoster,

    /// <summary>The decoded <c>[@PassengerCabin@]</c> hub.</summary>
    CampaignCabin,

    /// <summary>The decoded <c>[@ScrapBook_TOC@]</c> table of contents, PREVIOUS MISSIONS' destination.</summary>
    CampaignPreviousMissions,

    /// <summary>The briefing dialog, <c>Briefing.zrd</c>'s own chrome over the mission's reveal.</summary>
    CampaignBriefing,

    /// <summary>The decoded <c>[@FlightCheck@]</c> screen, one check per joined human.</summary>
    CampaignFlightCheck,

    /// <summary>The decoded <c>[@OrdinanceLayout@]</c> ammo selection.</summary>
    CampaignAmmo,

    /// <summary>The decoded <c>[@PlaneSelection@]</c> screen.</summary>
    CampaignPlaneSelection,

    /// <summary>The decoded <c>[@ScrapBook@]</c> spread, the mission end's destination.</summary>
    CampaignScrapbook,

    /// <summary>The decoded <c>[@ScrapbookZoom@]</c> detail view of one scrap.</summary>
    CampaignScrapbookZoom,

    /// <summary>The decoded <c>[@PlaneName@]</c> screen: the edit box, the defaults box, OK and Cancel.</summary>
    PlaneName,

    /// <summary>The Plane Construction hub with the <c>[@AirFrame@]</c> tab on its page.</summary>
    HangarAirframe,

    /// <summary>The hub with the <c>[@Engine@]</c> tab.</summary>
    HangarEngine,

    /// <summary>The hub with the <c>[@Armor@]</c> tab.</summary>
    HangarArmor,

    /// <summary>The hub with the <c>[@Guns@]</c> tab.</summary>
    HangarGuns,

    /// <summary>The hub with the <c>[@HardPoints@]</c> tab.</summary>
    HangarHardpoints,

    /// <summary>The hub with the <c>[@Paint@]</c> tab.</summary>
    HangarPaint,

    /// <summary>The hub with the <c>[@Purchase@]</c> totals page, READY TO PURCHASE's destination.</summary>
    HangarPurchase,

    /// <summary>The <c>[@Hangar@]</c> inventory, SELL PLANES' destination.</summary>
    HangarInventory,
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

    /// <summary>A decoded dropdown: its box shows the picked value, Accept opens its list, a
    /// sideways step picks the next value.</summary>
    Dropdown,

    /// <summary>One button of a decoded radio pair, or a checkbox, drawn from an eight-state strip.</summary>
    Radio,

    /// <summary>A decoded edit box: its label is the text typed so far, and the seat's typed
    /// characters feed it while its screen shows.</summary>
    TextField,
}

/// <summary>One interactive element of a screen in authored 800x600 pixels: what it is, where it
/// is, whether it reacts, which column it belongs to for the seat's cursor, and whether it is on
/// screen (a list row outside its window keeps its place for the keyboard, unseen and unhit).</summary>
public sealed record OriginalRow(
    string Key, string Label, OriginalRowKind Kind, float X, float Y, float Width, float Height,
    bool Enabled, int Column, BoardArt? Art, bool Visible = true)
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

/// <summary>The colours the Options screen writes in, read off <c>[@Preferences@]</c>: its
/// description rows' authored text colour and its title's.</summary>
public sealed record OriginalPreferencesInks(MenuLayoutColor Text, MenuLayoutColor Title);

/// <summary>
/// The Original presentation's screen graph, engine-free: the decoded top level with the
/// remake-only Free Flight, Dogfight and hangar doors, the sortie screens, the Options screen over
/// the decoded Preferences chrome, and the decoded Instant Action, campaign and hangar screens over
/// their shared features (each family its own partial file), driven by each seat's semantic
/// commands and composed into a <see cref="ComposedBoard"/> in the authored 800x600 space. Seat
/// 0's pointer arrives already mapped into that space; hovering a live row moves the focus onto
/// it, so keyboard, pad and pointer share one cursor. A dialog (the original's messagebox) may
/// stand over any screen, and while one does its answers are the only rows. Every rectangle and
/// art name comes from the layout; the art's pixel size, which the layout does not carry, comes
/// from the measurer the presentation injects.
/// </summary>
public sealed partial class OriginalShell
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

    /// <summary>The Options screen's section in the layout, whose chrome it is composed over.</summary>
    public const string PreferencesSection = "Preferences";

    /// <summary>The Options screen's way back, <c>[@Preferences@]</c>'s own RETURN TO MAIN MENU.</summary>
    public const string OptionsBackKey = "PF_B_MAINMENU";

    /// <summary>The Preferences pages' four doors, drawn disabled: no shared option stands behind them.</summary>
    public static readonly string[] PreferencesPageKeys = { "PF_B_GAMEOPTIONS", "PF_B_AUDIO", "PF_B_VIDEO", "PF_B_CONTROLS" };

    // The chooser's own words beside the decoded description rows, one line so they clear the
    // RETURN TO MAIN MENU strip under them.
    private const string ChooserDescription = "Menu presentation. APPLY restarts the menu.";
    private const float ChooserGap = 8f;
    private const float PreferencesTitleFont = 20f;
    private const float PreferencesTextFont = 14f;
    private const float ChooserFont = 12f;

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
    private readonly PlayerSetupFeature _setup;
    private readonly Func<string, (int Width, int Height)?> _measure;
    private readonly Func<PlayerSeat, IReadOnlyList<int>> _flightDevices;
    private readonly IReadOnlyList<OriginalChapter> _chapters;
    private readonly BoardArt? _plaque;
    private readonly BoardArt _activePointer;
    private readonly BoardArt _passivePointer;
    private readonly int[] _focus = new int[Enum.GetValues<OriginalScreen>().Length];
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);

    private OriginalScreen _screen;
    private int _hover = -1;
    private int _pressed = -1;
    private (float X, float Y)? _pointer;
    private int _pickedChapter = -1;
    private string _choice = PresentationId.Original.Value;

    /// <summary>A shell over <paramref name="layout"/> and the shared features. <paramref name="measure"/>
    /// answers an art name with its strip's pixel size (null when the file is not there),
    /// <paramref name="flightDevices"/> a seat with its launch's devices; the chapters default to
    /// <see cref="OriginalRosters"/>, a missing Instant Action feature to a private one. The hangar
    /// door stands only over <paramref name="hangar"/> and <paramref name="planes"/> together, the
    /// Campaign row only over <paramref name="campaign"/> and <paramref name="profiles"/> together.</summary>
    public OriginalShell(
        MenuLayout layout,
        FreeFlightFeature free,
        PlayerSetupFeature setup,
        Func<string, (int Width, int Height)?> measure,
        Func<PlayerSeat, IReadOnlyList<int>>? flightDevices = null,
        IReadOnlyList<OriginalChapter>? chapters = null,
        InstantActionFeature? instantAction = null,
        HangarFeature? hangar = null,
        CSVM.Flight.CustomPlaneStore? planes = null,
        CampaignFeature? campaign = null,
        Func<CSVM.Session.CampaignProfileStore>? profiles = null,
        Func<CSVM.Flight.StockLoadouts?>? stock = null,
        string? dataRoot = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _free = free ?? throw new ArgumentNullException(nameof(free));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _flightDevices = flightDevices ?? (_ => Array.Empty<int>());
        _chapters = chapters ?? OriginalRosters.Chapters;
        _instantAction = instantAction ?? new InstantActionFeature(_ => Mech3.InstantAction.Defaults());
        _hangar = hangar;
        _planes = planes;
        _campaign = campaign;
        _profiles = profiles;
        _stock = stock;
        _dataRoot = dataRoot;
        _campaignLayout = CampaignLayout.Over(layout);
        var plaqueRow = layout.Screen("FlightCheck")?.Widget("FC_B_CHANGEPLANE");
        _plaque = plaqueRow is { Art.Count: > 0 } ? new BoardArt(BoardArtLibrary.Ui, plaqueRow.Art[0], plaqueRow.Frames) : null;
        Inks = ReadInks(layout, plaqueRow);
        PreferencesInks = ReadPreferencesInks(layout, Inks);
        InstantActionInks = ReadInstantActionInks(layout);
        HangarInks = ReadHangarInks(layout);
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

    /// <summary>The colours the Options screen writes in.</summary>
    public OriginalPreferencesInks PreferencesInks { get; }

    /// <summary>The current screen's rows, in focus order: a standing dialog's answers alone,
    /// else the screen's own.</summary>
    public IReadOnlyList<OriginalRow> Rows => _dialog != null ? DialogRows() : BuildRows();

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

    /// <summary>The presentation the Options screen would apply.</summary>
    public string PresentationChoice => _choice;

    /// <summary>The pointer's last authored position, or null when the seat has none.</summary>
    public (float X, float Y)? Pointer => _pointer;

    /// <summary>Stands the shell on its top level, the landing point of every return and of a
    /// cold start: the list cursors stay where they were, every seat's pick goes back to browsing
    /// so a return from flight cannot fly again on a stale pick, and an open campaign is dropped,
    /// since the two flight returns reopen it on the profile the mission wrote.</summary>
    public void ReturnToTopLevel()
    {
        _setup.ResetPicks(fits: true);
        CloseCampaign();
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
        SyncCampaignField();
        bool changed = TypeName(commands, cues);
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
                    // An open campaign list's entry takes the highlight, not the focus, which
                    // stays on the field the list hangs from.
                    if (HoverOnly(rows[over]))
                    {
                        HighlightComboEntry(rows[over]);
                    }
                    else
                    {
                        focus = over;
                    }

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
                if (!HoverOnly(rows[over]))
                {
                    focus = over;
                    _focus[(int)_screen] = focus;
                }

                exit = Activate(rows[over], cues);
                changed = true;
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (pointer.Clicked && over < 0 && (CloseInstantActionDropdown() || CloseHangarDropdown() || CloseCampaignCombo()))
            {
                // A click off an open list closes it and picks nothing.
                changed = true;
                rows = Rows;
                focus = EnsureFocus(rows);
            }
        }

        if (commands.MoveY != 0)
        {
            // Inside an open campaign list the axis walks the list's entries.
            if (!MoveCampaignCombo(rows, focus, commands.MoveY))
            {
                focus = StepWithinColumn(rows, focus, commands.MoveY);
            }

            changed = true;
        }

        if (commands.MoveX != 0)
        {
            // On the Instant Action screen a sideways step on a dropdown or a radio changes its
            // value, in the hangar it steps a dropdown or walks the tab bar, on a campaign screen
            // it steps a closed field's pick; anywhere else it crosses columns.
            if (_screen == OriginalScreen.InstantAction && StepInstantActionValue(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (IsHangarScreen && StepHangarSideways(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (IsCampaignScreen && StepCampaignSideways(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else
            {
                focus = StepColumn(rows, focus, commands.MoveX);
            }

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

    /// <summary>The screen as a composed board in the authored space, a standing dialog over it
    /// and the pointer drawn last.</summary>
    public ComposedBoard Compose()
    {
        var rows = Rows;
        int focus = EnsureFocus(rows);
        // Under a dialog the screen is drawn from its own rows with nothing focused; the dialog's
        // answers are the rows the pointer and the cursor see.
        var screenRows = _dialog == null ? rows : BuildRows();
        int screenFocus = _dialog == null ? focus : -1;
        var backdrop = new List<BoardPicture>();
        var pictures = new List<BoardPicture>();
        var fills = new List<BoardFill>();
        var strokes = new List<BoardStroke>();
        var lines = new List<BoardLine>();
        var plaques = new List<BoardPlaque>();
        var notes = new List<BoardNote>();
        var overlays = new List<BoardPanel>();
        var main = _layout.Screen(OriginalAvailability.MainMenuSection);
        bool ownPage = _screen is OriginalScreen.InstantAction or OriginalScreen.Options || IsHangarScreen || IsCampaignScreen;
        if (!ownPage && main?.Widget("MM_LOGO") is { Art.Count: > 0 } logo)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], logo.Frames), logo.Int("X"), logo.Int("Y")));
        }

        if (_screen == OriginalScreen.TopLevel && main?.Widget("BFRAME") is { Art.Count: > 0 } structure)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, structure.Art[0], structure.Frames), structure.Int("X"), structure.Int("Y")));
        }

        switch (_screen)
        {
            case OriginalScreen.InstantAction:
                ComposeInstantAction(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case var _ when IsCampaignScreen:
                ComposeCampaign(rows, focus, backdrop, pictures, fills, strokes, lines, plaques, notes, overlays);
                break;
            case var _ when IsHangarScreen:
                ComposeHangar(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case OriginalScreen.FreeFlight:
            case OriginalScreen.Dogfight:
                ComposeSortie(rows, lines);
                break;
            case OriginalScreen.Options:
                ComposeOptions(pictures, lines);
                break;
        }

        if (!ownPage || _screen == OriginalScreen.Options)
        {
            ComposeRows(screenRows, screenFocus, fills, lines, plaques);
        }

        if (_dialog != null && !IsCampaignScreen)
        {
            overlays.Add(ComposeDialog(rows, focus));
        }

        if (_pointer is { } at)
        {
            bool live = _hover >= 0 && _hover < rows.Count && rows[_hover].Enabled;
            overlays.Add(new BoardPanel(
                Array.Empty<BoardFill>(),
                new[] { new BoardPicture(live ? _activePointer : _passivePointer, at.X, at.Y) },
                Array.Empty<BoardLine>()));
        }

        return new ComposedBoard(pictures, strokes, lines, plaques, notes,
            backdrop: backdrop, fills: fills, overlays: overlays);
    }

    // Where the chooser stands on the Preferences page: the slot under the last page door, at
    // the doors' own pitch, which the rows author and the panel has room for.
    private static (float X, float Y) ChooserCorner(MenuLayoutScreen screen)
    {
        var first = screen.Widget(PreferencesPageKeys[0]);
        var last = screen.Widget(PreferencesPageKeys[^1]);
        var beforeLast = screen.Widget(PreferencesPageKeys[^2]);
        if (first == null || last == null)
        {
            return (OptionsX, OptionsTop);
        }

        float pitch = beforeLast != null ? last.Int("Y") - beforeLast.Int("Y") : OptionsPitch;
        return (first.Int("X"), last.Int("Y") + Math.Max(OptionsPitch, pitch));
    }

    private static OriginalPreferencesInks ReadPreferencesInks(MenuLayout layout, OriginalInks inks)
    {
        var screen = layout.Screen(PreferencesSection);
        var description = screen?.Widget("PF_T_GODESC");
        var title = screen?.Widget("PF_T_TITLE");
        return new OriginalPreferencesInks(
            description != null && description.TryColor("Color", out var text) ? text : inks.Disabled,
            title != null && title.TryColor("Color", out var heading) ? heading : inks.Active);
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
            if (rows[i].Visible && rows[i].Contains(x, y))
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

    // The rows of a screen that has no page of its own (the top level, the sortie screens, the
    // Options screen): a decoded strip in its state frame, a paper plaque with its label (an
    // outlined label where the plaque art is missing), and list text. Nothing is focused or
    // pressed while a dialog stands over the screen.
    private void ComposeRows(IReadOnlyList<OriginalRow> rows, int focus, List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.Visible)
            {
                continue;
            }

            bool focused = i == focus;
            bool pressed = focus >= 0 && i == _pressed;
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
    }

    // The Options screen's chrome, [@Preferences@]'s own: its logo and background panes, its title
    // and the description beside each page door, plus the chooser's description in the same column
    // and colour. The rows themselves (the four disabled doors, the chooser, APPLY and RETURN TO MAIN
    // MENU) are drawn by the row loop. With no section the chooser stands alone over the top level's logo.
    private void ComposeOptions(List<BoardPicture> pictures, List<BoardLine> lines)
    {
        var screen = _layout.Screen(PreferencesSection);
        if (screen == null)
        {
            if (_layout.Screen(OriginalAvailability.MainMenuSection)?.Widget("MM_LOGO") is { Art.Count: > 0 } logo)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], logo.Frames), logo.Int("X"), logo.Int("Y")));
            }

            lines.Add(new BoardLine("OPTIONS", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            lines.Add(new BoardLine(ChooserDescription, 0f, FooterY, BoardFit.AuthoredWidth, FooterFont, BoardInk.Detail, -1, false, BoardJustify.Center));
            return;
        }

        foreach (string key in new[] { "PF_LOGO", "PF_BACKGROUND" })
        {
            if (screen.Widget(key) is { Art.Count: > 0 } pane)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, pane.Art[0], Math.Max(1, pane.Frames)), pane.Int("X"), pane.Int("Y")));
            }
        }

        if (screen.Widget("PF_T_TITLE") is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? "PREFERENCES", title.Int("X"), title.Int("Y"), title.Int("Width"), PreferencesTitleFont,
                BoardInk.Heading, -1, false, title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }

        MenuLayoutWidget? lastDescription = null;
        foreach (string key in new[] { "PF_T_GODESC", "PF_T_APDESC", "PF_T_VPDESC", "PF_T_CPDESC" })
        {
            if (screen.Widget(key) is { } description)
            {
                lastDescription = description;
                lines.Add(new BoardLine(description.Text ?? string.Empty, description.Int("X"), description.Int("Y"),
                    description.Int("Width"), PreferencesTextFont, BoardInk.Row));
            }
        }

        var (_, chooserY) = ChooserCorner(screen);
        float descriptionX = lastDescription?.Int("X") ?? OptionsX;
        float descriptionWidth = lastDescription?.Int("Width", 310) ?? 310;
        lines.Add(new BoardLine(ChooserDescription, descriptionX, chooserY + 6f, descriptionWidth, ChooserFont, BoardInk.Row));
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

    private MenuExit? Activate(OriginalRow row, List<string> cues)
    {
        if (row.Kind != OriginalRowKind.ListRow)
        {
            cues.Add(OriginalCues.Click);
        }

        // A standing dialog takes the answer whatever screen it stands over.
        if (_dialog != null)
        {
            AnswerDialog(row.Key);
            return null;
        }

        switch (_screen)
        {
            case OriginalScreen.TopLevel:
                switch (row.Key)
                {
                    case FreeFlightKey:
                        Open(OriginalScreen.FreeFlight);
                        break;
                    case DogfightKey:
                        Open(OriginalScreen.Dogfight);
                        break;
                    case HangarKey:
                        OpenHangar();
                        break;
                    case CampaignKey:
                        OpenCampaign();
                        break;
                    case "MM_B_INSTANTACTION":
                        OpenInstantAction();
                        break;
                    case "MM_B_PREFERENCES":
                        Open(OriginalScreen.Options);
                        break;
                    case "MM_B_QUIT":
                        return new QuitExit();
                }

                break;
            case OriginalScreen.FreeFlight:
            case OriginalScreen.Dogfight:
                return ActivateSortie(row);
            case OriginalScreen.InstantAction:
                return ActivateInstantAction(row);
            case var _ when IsCampaignScreen:
                return ActivateCampaign(row);
            case var _ when IsHangarScreen:
                return ActivateHangar(row);
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
                    case OptionsBackKey:
                        Open(OriginalScreen.TopLevel);
                        break;
                }

                break;
        }

        return null;
    }

    // Back with a dialog standing takes its declining answer, the messagebox script's own Escape.
    // On a sortie screen it first undoes seat 0's own pick, a stage at a time; browsing, it leaves
    // the screen. On the Instant Action screen the first Back closes an open list; the next one
    // leaves. The campaign and the hangar have their own graphs to walk back through. The top
    // level quits outright, as MAINMENU.SCRIPT's Quit terminates with no confirm.
    private MenuExit? Back()
    {
        if (_dialog is { } dialog)
        {
            AnswerDialog(dialog.Answers[dialog.Answers.Count - 1].Key);
            return null;
        }

        if (_screen == OriginalScreen.TopLevel)
        {
            return new QuitExit();
        }

        if (IsSortie && Seat0 is { } seat && _setup.Back(seat) != SeatBack.Browsing)
        {
            return null;
        }

        if (_screen == OriginalScreen.InstantAction && CloseInstantActionDropdown())
        {
            return null;
        }

        if (IsCampaignScreen)
        {
            BackCampaign();
            return null;
        }

        if (IsHangarScreen)
        {
            return BackHangar();
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
                rows.Add(TextButton(DogfightKey, "DOGFIGHT", DoorX, DogfightDoorY, true, 0));
                rows.Add(TextButton(HangarKey, "BUILD PLANE", DoorX, HangarDoorY, _hangar != null && _planes != null, 0));
                var main = _layout.Screen(OriginalAvailability.MainMenuSection);
                foreach (string key in TopLevelButtons)
                {
                    if (main?.Widget(key) is { } widget)
                    {
                        bool enabled = key is "MM_B_QUIT" or "MM_B_PREFERENCES" or "MM_B_INSTANTACTION"
                            || (key == CampaignKey && _campaign != null && _profiles != null);
                        rows.Add(Button(widget, enabled));
                    }
                }

                break;
            case OriginalScreen.InstantAction:
                BuildInstantActionRows(rows);
                break;
            case var _ when IsCampaignScreen:
                BuildCampaignRows(rows);
                break;
            case var _ when IsHangarScreen:
                BuildHangarRows(rows);
                break;
            case OriginalScreen.FreeFlight:
            case OriginalScreen.Dogfight:
                SortieRows(rows);
                break;
            case OriginalScreen.Options:
                BuildOptionsRows(rows);
                break;
        }

        return rows;
    }

    // The Options screen over [@Preferences@]: the four page doors at their authored corners,
    // disabled since no shared option stands behind them; the chooser and APPLY as paper plaques
    // in the slot under them; and the section's own RETURN TO MAIN MENU. Without the section the
    // chooser stands alone with a BACK plaque.
    private void BuildOptionsRows(List<OriginalRow> rows)
    {
        string choice = _choice == PresentationId.Original.Value ? "ORIGINAL" : "BUILT-IN";
        var screen = _layout.Screen(PreferencesSection);
        if (screen == null)
        {
            rows.Add(TextButton(PresentationKey, choice, OptionsX, OptionsTop, true, 0));
            rows.Add(TextButton(ApplyKey, "APPLY", OptionsX, OptionsTop + OptionsPitch, true, 0));
            rows.Add(TextButton(BackKey, "BACK", OptionsX, OptionsTop + (2 * OptionsPitch), true, 0));
            return;
        }

        foreach (string key in PreferencesPageKeys)
        {
            if (screen.Widget(key) is { } door)
            {
                rows.Add(Button(door, false));
            }
        }

        var (x, y) = ChooserCorner(screen);
        var plaque = PlaqueSize();
        rows.Add(TextButton(PresentationKey, choice, x, y, true, 0));
        rows.Add(TextButton(ApplyKey, "APPLY", x + plaque.Width + ChooserGap, y, true, 0));
        if (screen.Widget(OptionsBackKey) is { } back)
        {
            rows.Add(Button(back, true));
        }
        else
        {
            rows.Add(TextButton(BackKey, "BACK", x, y + OptionsPitch, true, 0));
        }
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
