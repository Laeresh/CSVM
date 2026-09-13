using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI.Menu.Original;

/// <summary>The colours the Instant Action screen writes in, read off its own rows: the text
/// rows' authored colour and the paper buttons' label tail.</summary>
public sealed record OriginalInstantActionInks(
    MenuLayoutColor Text, MenuLayoutColor LabelNormal, MenuLayoutColor LabelRollover, MenuLayoutColor LabelDepressed);

/// <summary>
/// The Original Instant Action screen and its Weapon Loadout, a standalone module over the shared
/// <see cref="InstantActionFeature"/> and the decoded <c>[@InstantAction@]</c> and
/// <c>[@OrdinanceLayout@]</c> sections: the contents window and its arrows, the dropdowns, the paged
/// enemy rows, the radio pair and the five buttons, then one aeroplane's fit over the campaign's own
/// ammo chrome. Decoded rules bound here: a contents row applies its preset, View Story writes its
/// name, the ace duel blanks every enemy control, the wingman plane blanks at zero wingmen, stunt
/// flying bars the clouds. The Pilot Plane list is the sortie screens' roster; Build opens the
/// hangar wallet-free, Weapon Loadout the loadout screen for the seat the radio names, and the
/// per-seat picker's own door lands there over that seat's fit. ACCEPT LOADOUT keeps the picks,
/// CANCEL LOADOUT and Back restore. Readings: docs/org/menu-inventory.md, docs/formats/instant-action.md.
/// </summary>
public sealed class OriginalInstantActionScreen : IOriginalScreenModule
{
    /// <summary>The layout section the screen is composed from.</summary>
    public const string InstantActionSection = "InstantAction";

    /// <summary>The Table of Contents list; its rows are keyed <c>IA_TL_Contents:&lt;index&gt;</c>.</summary>
    public const string ContentsKey = "IA_TL_Contents";

    /// <summary>The contents list's scroll-up arrow.</summary>
    public const string ContentsUpKey = "IA_TL_Contents:up";

    /// <summary>The contents list's scroll-down arrow.</summary>
    public const string ContentsDownKey = "IA_TL_Contents:down";

    /// <summary>The View Story button.</summary>
    public const string ViewStoryKey = "IA_B_VIEW";

    /// <summary>The Fly Mission button.</summary>
    public const string FlyMissionKey = "IA_B_FLY";

    /// <summary>The Weapon Loadout button.</summary>
    public const string WeaponLoadoutKey = "IA_B_CHANGEWEAPONS";

    /// <summary>The Exit button, back to the main menu.</summary>
    public const string ExitKey = "IA_B_Exit";

    /// <summary>The Build Custom Plane button.</summary>
    public const string BuildKey = "IA_B_BUILD";

    /// <summary>The radio pair: whose loadout the Weapon Loadout button edits.</summary>
    public const string PlayerRadioKey = "IA_B_PLAYER";

    /// <summary>The wingman half of the radio pair.</summary>
    public const string WingmanRadioKey = "IA_B_WINGMAN";

    /// <summary>The enemy rows' page-up button.</summary>
    public const string PageUpKey = "IA_B_UP";

    /// <summary>The enemy rows' page-down button.</summary>
    public const string PageDownKey = "IA_B_DOWN";

    /// <summary>The player's plane dropdown.</summary>
    public const string PlayerPlaneKey = "IA_D_PLANEP";

    /// <summary>The wingman count dropdown.</summary>
    public const string WingmenKey = "IA_D_NWING";

    /// <summary>The wingman plane dropdown, hidden at zero wingmen.</summary>
    public const string WingmanPlaneKey = "IA_D_PLANEW";

    /// <summary>The mission type dropdown.</summary>
    public const string MissionKey = "IA_D_MISSTYPE";

    /// <summary>The environment dropdown.</summary>
    public const string EnvironmentKey = "IA_D_ENVIRONMENT";

    /// <summary>The layout section the loadout screen is composed from.</summary>
    public const string LoadoutSection = "OrdinanceLayout";

    /// <summary>The screen's ACCEPT LOADOUT button, keeping the picks.</summary>
    public const string LoadoutAcceptKey = "OL_B_ACCEPT";

    /// <summary>The screen's CANCEL LOADOUT button, restoring the picks it opened on.</summary>
    public const string LoadoutCancelKey = "OL_B_CANCEL";

    /// <summary>The four ammunition fields, <c>OL_D_AMMO0..3</c>, one per gun slot.</summary>
    public const string LoadoutAmmoPrefix = "OL_D_AMMO";

    /// <summary>The eight rocket fields, <c>OL_D_ROCKETS0..7</c>, one per pylon.</summary>
    public const string LoadoutRocketPrefix = "OL_D_ROCKETS";

    // Text sizes against the authored 18-pixel item height and the text rows' own boxes.
    private const float ItemFont = 13f;
    private const float TitleFont = 20f;
    private const float LabelFont = 14f;
    private const float InstructionFont = 12f;
    private const float StoryFont = 18f;

    // Text sizes against the loadout section's 15-pixel item height and its text rows.
    private const float LoadoutTitleFont = 20f;
    private const float LoadoutHeadingFont = 15f;
    private const float LoadoutCaptionFont = 11f;
    private const float LoadoutItemFont = 12f;

    // The string ids the campaign's ammo page reads: the calibre words, the no-gun marker, and the
    // two description families indexed by option row (docs/formats/campaign-screens.md).
    private const int CalibreLabel = 3320;
    private const int NoGunLabel = 3315;
    private const int AmmoTitleLabel = 3350;
    private const int AmmoBodyLabel = 3370;
    private const int RocketTitleLabel = 3380;
    private const int RocketBodyLabel = 3410;

    // The four colours a list and a box mark with, measured off the film: no layout row authors a
    // dropdown's paper, the band under its picked row, the lighter band under the row the pointer
    // is on, or the cream a closed box's outline is redrawn in while the pointer stands on it.
    private const byte ListPaperR = 216, ListPaperG = 200, ListPaperB = 166;
    private const byte PickedRowR = 200, PickedRowG = 151, PickedRowB = 80;
    private const byte HoveredRowR = 220, HoveredRowG = 181, HoveredRowB = 124;
    private const byte LitBoxR = 246, LitBoxG = 237, LitBoxB = 214;

    // A widget's size when the layout row is missing or its art cannot be measured.
    private const float FallbackItemHeight = 18f;
    private const float FallbackDropWidth = 160f;
    private const float FallbackArrowWidth = 15f;
    private const float FallbackArrowHeight = 14f;
    private const float FallbackButtonWidth = 220f;
    private const float FallbackButtonHeight = 42f;

    private static readonly string[] WaveKeyPrefixes = { "IA_D_NENEMY", "IA_D_EGROUP", "IA_D_DIFFICULTY", "IA_D_PLANEE" };

    private readonly InstantActionFeature _instantAction;
    private readonly PlayerSetupFeature _setup;
    private readonly CustomPlaneStore? _planes;
    private readonly MenuLayout _layout;
    private readonly Func<string, (int Width, int Height)?> _measure;
    private readonly IOriginalScreenHost _host;
    private readonly Func<StockLoadouts?>? _stock;
    private readonly string?[] _loadoutBefore = new string?[LoadoutChoice.MaxGunSlot + LoadoutChoice.MaxPylon];
    private IReadOnlyList<MenuAircraft> _iaPilotRoster = OriginalRosters.Roster(Array.Empty<CustomPlaneDef>());
    private string? _iaPilotBuild;
    private LoadoutChoice? _iaSpareFit;
    private int _iaPage;
    private string? _iaOpen;
    private int _iaListTop;
    private int _iaContentsTop;
    private int _iaRadio;
    private string _iaStoryTitle = string.Empty;
    private LoadoutChoice? _loadoutFit;
    private LoadoutDef? _loadoutDef;
    private LoadoutOptions _loadoutOptions = new();
    private string? _loadoutNode;
    private string _loadoutName = string.Empty;
    // The seat whose own fit the screen is editing, set only by the per-seat picker's door and null
    // for the Instant Action strip's. It is what says the screen belongs to a walk in progress, so
    // the walk survives the trip and the return lands back on the picker rather than on Instant
    // Action.
    private PlayerSeat? _loadoutSeat;

    /// <summary>An Instant Action module over <paramref name="instantAction"/>, the seats
    /// <paramref name="setup"/> holds and the build store its Pilot Plane list reads, composing
    /// <paramref name="layout"/>'s own sections and calling back into <paramref name="host"/> for the
    /// state and the seams every screen family shares. <paramref name="stock"/> answers the stock
    /// weapon table the loadout screen's fields stand over.</summary>
    public OriginalInstantActionScreen(
        InstantActionFeature instantAction, PlayerSetupFeature setup, CustomPlaneStore? planes, MenuLayout layout,
        Func<string, (int Width, int Height)?> measure, IOriginalScreenHost host,
        Func<StockLoadouts?>? stock = null)
    {
        _instantAction = instantAction ?? throw new ArgumentNullException(nameof(instantAction));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _planes = planes;
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _stock = stock;
        Inks = ReadInks(layout);
    }

    /// <summary>The colours the Instant Action screen writes in.</summary>
    public OriginalInstantActionInks Inks { get; }

    /// <summary>The open dropdown's key, or null when none is open.</summary>
    public string? OpenDropdown => _iaOpen;

    /// <summary>The Pilot Plane list: the roster the sortie screens read, the eleven stock
    /// airframes and then the saved builds, as of the last <see cref="RefreshRoster"/>.</summary>
    public IReadOnlyList<MenuAircraft> PilotRoster => _iaPilotRoster;

    /// <summary>The picked Pilot Plane row's index into <see cref="PilotRoster"/>: the picked
    /// build's row while it still flies the feature's airframe, else that airframe's stock row.</summary>
    public int PilotRow
    {
        get
        {
            string node = _instantAction.PlayerPlane.Node;
            int stock = -1;
            for (int i = 0; i < _iaPilotRoster.Count; i++)
            {
                var row = _iaPilotRoster[i];
                if (row.Node != node)
                {
                    continue;
                }

                if (row.IsCustom && row.Name == _iaPilotBuild)
                {
                    return i;
                }

                if (!row.IsCustom && stock < 0)
                {
                    stock = i;
                }
            }

            return stock;
        }
    }

    /// <summary>Which enemy page shows: 0 the pilot fields with the first wave, 1 waves two to four.</summary>
    public int EnemyPage => _iaPage;

    /// <summary>The contents window's first visible preset.</summary>
    public int ContentsTop => _iaContentsTop;

    /// <summary>Whose loadout the Weapon Loadout button targets: 0 the pilot, 1 the wingmen.</summary>
    public int LoadoutTarget => _iaRadio;

    /// <summary>The pilot's fit, seat 0's own so the sortie screens and this one edit one choice; a
    /// shell with no seat joined keeps a spare so the screen still works.</summary>
    public LoadoutChoice PilotFit => Seat0?.Fit ?? (_iaSpareFit ??= new LoadoutChoice());

    /// <summary>The story title View Story last wrote, or "".</summary>
    public string StoryTitle => _iaStoryTitle;

    /// <summary>The fit the loadout screen is editing, or null while it is not showing.</summary>
    public LoadoutChoice? LoadoutFit => _loadoutFit;

    /// <summary>The seat the loadout screen is editing for, as its index, or -1 when it is not
    /// showing or was opened from the Instant Action screen instead.</summary>
    public int LoadoutSeat => _loadoutSeat is { } seat ? SeatIndex(seat) : -1;

    /// <summary>The stock node whose fit the loadout screen is editing, or null while it is not showing.</summary>
    public string? LoadoutNode => _loadoutNode;

    /// <summary>Whether the loadout screen is standing over a seat's own fit, which is what keeps a
    /// seat walk alive across the trip.</summary>
    public bool OnSeatLoadout => _loadoutSeat != null;

    /// <summary>Whether the screen showing is this module's: Instant Action or its loadout.</summary>
    public bool Owns(OriginalScreen screen) =>
        screen is OriginalScreen.InstantAction or OriginalScreen.InstantActionLoadout;

    /// <summary>Opens the Instant Action screen: the environment is confirmed so the launch's base
    /// def is the environment's own, and no list is open.</summary>
    public void OpenInstantAction()
    {
        _instantAction.ConfirmEnvironment();
        if (_instantAction.IsAceDuel)
        {
            _iaPage = 0;
        }

        RefreshRoster();
        _iaOpen = null;
        _host.Open(OriginalScreen.InstantAction);
    }

    /// <summary>Re-reads the saved builds into the Pilot Plane list through the sortie screens'
    /// roster rule. Every entry to the screen calls it; a return from the hangar calls it too, so
    /// a build saved there is offered without leaving the screen.</summary>
    public void RefreshRoster() =>
        _iaPilotRoster = OriginalRosters.Roster(_planes?.List() ?? Array.Empty<CustomPlaneDef>());

    /// <summary>Opens one of the screen's dropdowns as a press on it would, for a scripted pose;
    /// false when the screen is not showing or the key names no dropdown.</summary>
    public bool OpenDropdownOn(string key)
    {
        if (_host.Screen != OriginalScreen.InstantAction || DropdownFor(key) is not { } list)
        {
            return false;
        }

        _iaOpen = key;
        _iaListTop = 0;
        _host.FocusedRow = Math.Max(0, list.Current);
        return true;
    }

    /// <summary>Opens the Weapon Loadout for the seat the radio pair names: the wingmen's airframe
    /// and shared fit, or the picked pilot row (a build edits its airframe's stock fit) and seat
    /// 0's fit. The picks standing on entry are remembered for CANCEL.</summary>
    public void OpenLoadout()
    {
        if (_iaRadio == 1)
        {
            var wingman = _instantAction.WingmanPlane;
            BeginLoadout(wingman.Node, wingman.Name, _instantAction.WingmanFit, null);
        }
        else
        {
            var pilot = PilotPick();
            BeginLoadout(pilot.Node, PilotRowText(pilot), PilotFit, null);
        }
    }

    /// <summary>The per-seat picker's WEAPON LOADOUT: the seat's own fit over the airframe it has
    /// selected, named for the seat so the screen says whose loadout it is. Its own storage and no
    /// other's, so one pilot's picks cannot reach another's aeroplane.</summary>
    public void OpenSeatLoadout(PlayerSeat seat)
    {
        var roster = _setup.Roster;
        if (!seat.Locked || seat.Cursor < 0 || seat.Cursor >= roster.Count)
        {
            return;
        }

        var row = roster[seat.Cursor];
        BeginLoadout(row.Node, $"P{SeatIndex(seat) + 1}  {row.Name}", seat.Fit, seat);
    }

    /// <summary>Forgets what the loadout screen was editing without deciding where to go next, which
    /// is what a walk losing its picking seat mid-edit needs: the fit belongs to a seat that has
    /// left.</summary>
    public void DropLoadout()
    {
        _loadoutFit = null;
        _loadoutDef = null;
        _loadoutNode = null;
        _loadoutSeat = null;
        _iaOpen = null;
    }

    /// <summary>Drops the seat the loadout screen was standing for, which any screen a walk does not
    /// stand on does.</summary>
    public void ClearLoadoutSeat() => _loadoutSeat = null;

    /// <summary>The showing screen's rows, in focus order.</summary>
    public void BuildRows(List<OriginalRow> rows)
    {
        if (_host.Screen == OriginalScreen.InstantActionLoadout)
        {
            BuildLoadoutRows(rows);
        }
        else
        {
            BuildInstantActionRows(rows);
        }
    }

    /// <summary>The showing screen's scrolling lists for the pointer: an open dropdown's list alone
    /// while one stands (on the loadout screen too, whose section has no contents window), else the
    /// contents window.</summary>
    public void Lists(List<OriginalList> lists)
    {
        var screen = _layout.Screen(_host.Screen == OriginalScreen.InstantActionLoadout ? LoadoutSection : InstantActionSection);
        if (screen == null)
        {
            return;
        }

        if (_iaOpen != null)
        {
            if (OpenListWindow(screen) is { } open)
            {
                lists.Add(new OriginalList(_iaOpen, open, ScrollOpenList));
            }

            return;
        }

        if (screen.Widget(ContentsKey) is { } list && ContentsWindow(list) is { } contents)
        {
            lists.Add(new OriginalList(ContentsKey, contents, top => _iaContentsTop = top));
        }
    }

    /// <summary>A sideways step on the focused dropdown picks the next allowed value with wrap; on a
    /// radio it moves the mark. False when the focused row is neither, so the step crosses
    /// columns.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (rows == null || focus < 0 || focus >= rows.Count)
        {
            return false;
        }

        var row = rows[focus];
        if (row.Kind == OriginalRowKind.Radio)
        {
            _iaRadio = row.Key == PlayerRadioKey ? 1 : 0;
            _host.FocusKey(_iaRadio == 0 ? PlayerRadioKey : WingmanRadioKey);
            return true;
        }

        if (row.Kind != OriginalRowKind.Dropdown || DropdownFor(row.Key) is not { } list || list.Items.Count == 0)
        {
            return false;
        }

        int next = list.Current;
        for (int n = 0; n < list.Items.Count; n++)
        {
            next = ((next + direction) % list.Items.Count + list.Items.Count) % list.Items.Count;
            if (list.Allowed(next))
            {
                break;
            }
        }

        if (next != list.Current && list.Allowed(next))
        {
            list.Select(next);
        }

        _host.FocusKey(row.Key);
        return true;
    }

    /// <summary>Closes an open dropdown and puts the focus back on its box; false when none is open.</summary>
    public bool CloseDropdown()
    {
        if (_iaOpen == null)
        {
            return false;
        }

        string key = _iaOpen;
        _iaOpen = null;
        _host.FocusKey(key);
        return true;
    }

    /// <summary>The showing screen's answer to an activated row.</summary>
    public MenuExit? Activate(OriginalRow row) =>
        _host.Screen == OriginalScreen.InstantActionLoadout ? ActivateLoadout(row) : ActivateInstantAction(row);

    /// <summary>Back on either screen: the first one closes an open list and the next leaves, which
    /// on the loadout screen is CANCEL LOADOUT. False leaves Instant Action's own exit to the shell.</summary>
    public bool Back()
    {
        if (CloseDropdown())
        {
            return true;
        }

        if (_host.Screen == OriginalScreen.InstantActionLoadout)
        {
            CloseLoadout(keep: false);
            return true;
        }

        return false;
    }

    /// <summary>The showing screen as drawn. Neither of these two pages writes a note or a stroke,
    /// the prose layer being the hangar's description box alone and the pen the campaign scrapbook's,
    /// so <paramref name="notes"/> and <paramref name="strokes"/> stand unused; both are here
    /// because one signature serves every module's dispatch.</summary>
    public void Compose(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardStroke> strokes, List<BoardLine> lines, List<BoardPlaque> plaques,
        List<BoardNote> notes, List<BoardPanel> overlays)
    {
        if (_host.Screen == OriginalScreen.InstantActionLoadout)
        {
            ComposeLoadout(rows, focus, backdrop, pictures, fills, lines, plaques, overlays);
        }
        else
        {
            ComposeInstantAction(rows, focus, backdrop, pictures, fills, lines, plaques, overlays);
        }
    }

    // The rest of this class stays in the original's own narrative order (a helper beside the entry
    // point it serves, the loadout screen's own half after the setup screen's) rather than hoisted
    // into blocks for the ordering rules' sake, the same trade OriginalHangarScreen.cs makes.
#pragma warning disable SA1201, SA1202, SA1204

    private PlayerSeat? Seat0 => _setup.Seats.Count > 0 ? _setup.Seats[0] : null;

    private int SeatIndex(PlayerSeat seat)
    {
        var seats = _setup.Seats;
        for (int i = 0; i < seats.Count; i++)
        {
            if (ReferenceEquals(seats[i], seat))
            {
                return i;
            }
        }

        return -1;
    }

    private static OriginalInstantActionInks ReadInks(MenuLayout layout)
    {
        var screen = layout.Screen(InstantActionSection);
        var black = new MenuLayoutColor(255, 0, 0, 0);
        var white = new MenuLayoutColor(255, 255, 255, 255);
        var title = screen?.Widget("IA_T_TABLETITLE");
        var fly = screen?.Widget(FlyMissionKey);
        return new OriginalInstantActionInks(
            title != null && title.TryColor("Color", out var text) ? text : black,
            fly != null && fly.TryColor("ColorActive", out var n) ? n : black,
            fly != null && fly.TryColor("ColorRollover", out var r) ? r : black,
            fly != null && fly.TryColor("ColorDepressed", out var d) ? d : white);
    }

    private static bool IsWhite(MenuLayoutWidget widget, string field = "Color") =>
        widget.TryColor(field, out var c) && c.R == 255 && c.G == 255 && c.B == 255;

    private static BoardJustify Justify(MenuLayoutWidget widget) => widget.Int("Justify") switch
    {
        1 => BoardJustify.Center,
        2 => BoardJustify.Right,
        _ => BoardJustify.Left,
    };

    private static string Capitalise(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // A dropdown's box: its authored corner and width, one item high.
    private static (float X, float Y, float Width, float Height) DropBox(MenuLayoutWidget widget) =>
        (widget.Int("X"), widget.Int("Y"), widget.Int("Width", (int)FallbackDropWidth), widget.Int("ItemHeight", (int)FallbackItemHeight));

    private static IReadOnlyList<string> Names<T>(IReadOnlyList<T> rows, Func<T, string> name)
    {
        var names = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            names[i] = name(rows[i]);
        }

        return names;
    }

    private static IReadOnlyList<string> Counts(int max)
    {
        var names = new string[max + 1];
        for (int i = 0; i <= max; i++)
        {
            names[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return names;
    }

    private static int ContentsIndex(string key)
    {
        if (!key.StartsWith(ContentsKey + ":", StringComparison.Ordinal))
        {
            return -1;
        }

        return int.TryParse(key[(ContentsKey.Length + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : -1;
    }

    // The stock airframe a roster row flies as, matched on the node rather than the row's name:
    // a build's row is named for the build, and a name-keyed lookup would land on the wrong def.
    private static InstantActionAirframe? AirframeOf(MenuAircraft row)
    {
        foreach (var airframe in InstantActionFeature.Airframes)
        {
            if (airframe.Node == row.Node)
            {
                return airframe;
            }
        }

        return null;
    }

    private static string PilotRowText(MenuAircraft row)
    {
        string airframe = AirframeOf(row)?.Name ?? row.Name;
        return row.IsCustom ? row.Name + " " + airframe : "Stock " + airframe;
    }

    // The rows: an open list's items alone while one is open, else the screen's widgets.
    private void BuildInstantActionRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(InstantActionSection);
        if (screen == null)
        {
            return;
        }

        if (!AddOpenListRows(screen, rows))
        {
            BuildInstantActionWidgets(screen, rows);
        }
    }

    // The open list's windowed items as the only rows, on the shared drop-list rule; false when no
    // list of this screen is open.
    private bool AddOpenListRows(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        if (OpenInstantActionDrop(screen) is not { } drop)
        {
            return false;
        }

        _iaListTop = OriginalDropLists.Top(drop, _iaListTop, _host.FocusedRow);
        OriginalDropLists.AddRows(drop, _iaListTop, rows, StripSize);
        return true;
    }

    // The open dropdown's list over the screen's own widget, or null while none is open.
    private OpenDropList? OpenInstantActionDrop(MenuLayoutScreen screen)
    {
        if (_iaOpen is not { } key || screen.Widget(key) is not { } open || DropdownFor(key) is not { } list)
        {
            return null;
        }

        return OriginalDropLists.Over(key, open, list.Items, DropBox(open), list.Allowed);
    }

    // The screen's widgets in focus order: the left page (the contents window, its arrows, View
    // Story, Build) as column 0, the right page (the dropdowns of the shown enemy page, the paging
    // button, the radio pair, Weapon Loadout, Fly Mission, Exit) as column 1.
    private void BuildInstantActionWidgets(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        var presets = InstantActionFeature.Presets;
        if (screen.Widget(ContentsKey) is { } list)
        {
            int window = Math.Max(1, list.Int("TotalDisplayed", 14));
            float itemHeight = list.Int("ItemHeight", (int)FallbackItemHeight);
            float x = list.Int("X");
            float y = list.Int("Y");
            float width = list.Int("Width", 277);
            // The list's own arrows stand inside its right edge, in the gutter the page's art
            // paints there, the up arrow at the top of the window and the down arrow at its foot;
            // each is live only while there is more list that way, and the rows keep off the column.
            var up = StripArt(list.Art, 1);
            var down = StripArt(list.Art, 2);
            var upSize = StripSize(up, FallbackArrowWidth, FallbackArrowHeight);
            var downSize = StripSize(down, FallbackArrowWidth, FallbackArrowHeight);
            int top = ContentsTopClamped(window);
            float column = presets.Count > window ? upSize.Width : 0f;
            for (int i = top; i < presets.Count && i < top + window; i++)
            {
                rows.Add(new OriginalRow($"{ContentsKey}:{i}", presets[i].Name, OriginalRowKind.ListRow,
                    x, y + ((i - top) * itemHeight), width - column, itemHeight, true, 0, null));
            }

            rows.Add(new OriginalRow(ContentsUpKey, string.Empty, OriginalRowKind.Button,
                x + width - upSize.Width, y, upSize.Width, upSize.Height, top > 0, 0, up));
            rows.Add(new OriginalRow(ContentsDownKey, string.Empty, OriginalRowKind.Button,
                x + width - downSize.Width, y + (window * itemHeight) - downSize.Height, downSize.Width, downSize.Height,
                top + window < presets.Count, 0, down));
        }

        AddStrip(screen, rows, ViewStoryKey, OriginalRowKind.TextButton, true, 0);
        AddStrip(screen, rows, BuildKey, OriginalRowKind.Button, _host.CanBuildPlane, 0);

        // A box a setting has nothing to say through keeps its place blank and inert rather than
        // leaving the page: the wingman plane at zero wingmen, every enemy box under the ace duel
        // and a wave's militia, skill and aircraft while it carries nobody.
        if (_iaPage == 0)
        {
            AddDropdown(screen, rows, PlayerPlaneKey);
            AddDropdown(screen, rows, WingmenKey);
            AddDropdown(screen, rows, WingmanPlaneKey, live: _instantAction.NumWingmen > 0);
            AddDropdown(screen, rows, MissionKey);
            AddDropdown(screen, rows, EnvironmentKey);
            AddWave(screen, rows, 0);
        }
        else
        {
            AddWave(screen, rows, 1);
            AddWave(screen, rows, 2);
            AddWave(screen, rows, 3);
        }

        // Both paging buttons stand on both pages, the one with nowhere to go in its disabled
        // frame; the ace duel still pages, its later waves blank like the first.
        AddStrip(screen, rows, PageUpKey, OriginalRowKind.Button, _iaPage > 0, 1);
        AddStrip(screen, rows, PageDownKey, OriginalRowKind.Button, _iaPage == 0, 1);

        AddStrip(screen, rows, PlayerRadioKey, OriginalRowKind.Radio, true, 1);
        AddStrip(screen, rows, WingmanRadioKey, OriginalRowKind.Radio, true, 1);
        AddStrip(screen, rows, WeaponLoadoutKey, OriginalRowKind.TextButton, true, 1);
        AddStrip(screen, rows, FlyMissionKey, OriginalRowKind.TextButton, true, 1);
        AddStrip(screen, rows, ExitKey, OriginalRowKind.Button, true, 1);
    }

    // One wave's four boxes. The ace duel takes no wave configuration at all, so even the count
    // goes blank; an empty wave still offers its count and blanks only what a militia would fill.
    private void AddWave(MenuLayoutScreen screen, List<OriginalRow> rows, int wave)
    {
        bool counted = !_instantAction.IsAceDuel;
        bool configured = counted && _instantAction.Waves[wave].Count > 0;
        string n = wave.ToString(CultureInfo.InvariantCulture);
        AddDropdown(screen, rows, WaveKeyPrefixes[0] + n, live: counted);
        for (int i = 1; i < WaveKeyPrefixes.Length; i++)
        {
            AddDropdown(screen, rows, WaveKeyPrefixes[i] + n, live: configured);
        }
    }

    // A dropdown that is not live still stands where the layout puts it, with no value written
    // and its arrow in the disabled frame, which is how the page reads a setting as unavailable.
    private void AddDropdown(MenuLayoutScreen screen, List<OriginalRow> rows, string key, int column = 1, bool live = true)
    {
        if (screen.Widget(key) is not { } widget || DropdownFor(key) is not { } list)
        {
            return;
        }

        var box = DropBox(widget);
        string value = live && list.Current >= 0 && list.Current < list.Items.Count ? list.Items[list.Current] : string.Empty;
        rows.Add(new OriginalRow(key, value, OriginalRowKind.Dropdown, box.X, box.Y, box.Width, box.Height,
            live, column, StripArt(widget.Art, 4)));
    }

    private void AddStrip(MenuLayoutScreen screen, List<OriginalRow> rows, string key, OriginalRowKind kind, bool enabled, int column)
    {
        if (screen.Widget(key) is not { } widget)
        {
            return;
        }

        var art = StripArt(widget.Art, 0, widget.Frames);
        var size = StripSize(art, FallbackButtonWidth, FallbackButtonHeight);
        rows.Add(new OriginalRow(key, widget.Text ?? string.Empty, kind, widget.Int("X"), widget.Int("Y"),
            size.Width, size.Height, enabled, column, art));
    }

    private static BoardArt? StripArt(IReadOnlyList<string> art, int index, int frames = 4) =>
        OriginalDropLists.StripArt(art, index, frames);

    // A strip's one-frame size from the measurer, or the fallback when the file is not there.
    private (float Width, float Height) StripSize(BoardArt? art, float fallbackWidth, float fallbackHeight)
    {
        if (art == null || _measure(art.Name) is not { } size)
        {
            return (fallbackWidth, fallbackHeight);
        }

        return (size.Width, (float)Math.Floor(size.Height / (float)Math.Max(1, art.Frames)));
    }

    private int ContentsTopClamped(int window)
    {
        _iaContentsTop = Math.Clamp(_iaContentsTop, 0, Math.Max(0, InstantActionFeature.Presets.Count - window));
        return _iaContentsTop;
    }

    // The pilot's list over the roster; the wingman's stays the stock table (the original offers
    // the wingmen no builds), so the two dropdowns are built apart on purpose.
    private DropdownList PilotDropdown() =>
        new(Names(_iaPilotRoster, PilotRowText), PilotRow, _ => true, SelectPilotRow);

    private DropdownList WingmanPlaneDropdown() =>
        new(Names(InstantActionFeature.Airframes, a => a.Name), _instantAction.WingmanPlaneIndex, _ => true, _instantAction.SelectWingmanPlane);

    // A pick moves the feature onto the row's stock airframe either way, so the presets, the def's
    // nominal name and the wingman screens keep the stock vocabulary; the build rides as the overlay.
    private void SelectPilotRow(int row)
    {
        if (row < 0 || row >= _iaPilotRoster.Count)
        {
            return;
        }

        var pick = _iaPilotRoster[row];
        var airframes = InstantActionFeature.Airframes;
        string before = _instantAction.PlayerPlane.Node;
        for (int i = 0; i < airframes.Count; i++)
        {
            if (airframes[i].Node == pick.Node)
            {
                _instantAction.SelectPlayerPlane(i);
                break;
            }
        }

        _iaPilotBuild = pick.IsCustom ? pick.Name : null;
        DropPilotFitIfMoved(before);
    }

    // A changed airframe drops the pilot's fit, the wingman rule applied to the pilot: gun slots
    // and pylons are per airframe, so a fit for one has nowhere to live on another.
    private void DropPilotFitIfMoved(string before)
    {
        if (before != _instantAction.PlayerPlane.Node)
        {
            PilotFit.ResetToStock();
        }
    }

    // The picked pilot row, or the feature's stock airframe when the roster has lost it.
    private MenuAircraft PilotPick()
    {
        int row = PilotRow;
        if (row >= 0)
        {
            return _iaPilotRoster[row];
        }

        var stock = _instantAction.PlayerPlane;
        return new MenuAircraft(stock.Name, stock.Node);
    }

    private DropdownList? DropdownFor(string key)
    {
        var ia = _instantAction;
        switch (key)
        {
            case PlayerPlaneKey:
                return PilotDropdown();
            case WingmenKey:
                return new DropdownList(Counts(InstantActionFeature.MaxWingmen), ia.NumWingmen, _ => true, ia.SetWingmen);
            case WingmanPlaneKey:
                return WingmanPlaneDropdown();
            case MissionKey:
                return new DropdownList(Names(ia.MissionTypes, m => m.Label), ia.MissionTypeIndex, _ => true, i =>
                {
                    ia.SelectMissionType(i);
                    if (ia.IsAceDuel)
                    {
                        // The ace duel hides every enemy control, so the enemy page has nothing to show.
                        _iaPage = 0;
                    }
                });
            case EnvironmentKey:
                return new DropdownList(Names(InstantActionFeature.Environments, e => e.Name), ia.EnvironmentIndex,
                    i => InstantActionFeature.EnvironmentAllowed(i, ia.MissionType.Key), i =>
                    {
                        ia.SelectEnvironment(i);
                        ia.ConfirmEnvironment();
                    });
        }

        for (int wave = 0; wave < InstantActionFeature.WaveSlots; wave++)
        {
            string n = wave.ToString(CultureInfo.InvariantCulture);
            var setup = ia.Waves[wave];
            int w = wave;
            if (key == WaveKeyPrefixes[0] + n)
            {
                return new DropdownList(Counts(InstantActionFeature.MaxEnemies), setup.Count, _ => true,
                    i => ia.SetWave(w, ia.Waves[w] with { Count = i }));
            }

            if (key == WaveKeyPrefixes[1] + n)
            {
                return new DropdownList(Names(InstantActionFeature.Militias, m => m.Name), setup.MilitiaIndex, _ => true,
                    i => ia.SelectWaveMilitia(w, i));
            }

            if (key == WaveKeyPrefixes[2] + n)
            {
                return new DropdownList(Names(InstantActionFeature.Skills, s => Capitalise(s)), setup.SkillIndex, _ => true,
                    i => ia.SelectWaveSkill(w, i));
            }

            if (key == WaveKeyPrefixes[3] + n)
            {
                return new DropdownList(Names(ia.WaveAircraft(w), a => a), setup.AircraftIndex, _ => true,
                    i => ia.SelectWaveAircraft(w, i));
            }
        }

        return LoadoutDropdownFor(key);
    }

    private MenuExit? ActivateInstantAction(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0)
        {
            string prefix = row.Key[..colon];
            string suffix = row.Key[(colon + 1)..];
            if (prefix == ContentsKey)
            {
                switch (suffix)
                {
                    case "up":
                        _iaContentsTop--;
                        break;
                    case "down":
                        _iaContentsTop++;
                        break;
                    default:
                        // Selecting a contents row applies its preset over every other control,
                        // the list's own select callback; the environment is re-confirmed so the
                        // launch's base def follows the preset's chapter.
                        string before = _instantAction.PlayerPlane.Node;
                        _instantAction.ApplyPreset(int.Parse(suffix, CultureInfo.InvariantCulture));
                        _instantAction.ConfirmEnvironment();
                        _iaPilotBuild = null;
                        _iaPage = 0;
                        DropPilotFitIfMoved(before);
                        break;
                }

                return null;
            }

            if (DropdownFor(prefix) is { } list)
            {
                switch (suffix)
                {
                    case OriginalDropLists.UpSuffix:
                        ScrollOpenList(_iaListTop - 1);
                        return null;
                    case OriginalDropLists.DownSuffix:
                        ScrollOpenList(_iaListTop + 1);
                        return null;
                }

                int index = int.Parse(suffix, CultureInfo.InvariantCulture);
                if (list.Allowed(index))
                {
                    list.Select(index);
                }

                _iaOpen = null;
                _host.FocusKey(prefix);
            }

            return null;
        }

        switch (row.Key)
        {
            case ViewStoryKey:
                // View Story formats the stored preset's name through IDS_IA_STORYTITLE, whose
                // whole text is the name itself; nothing else changes.
                _iaStoryTitle = _instantAction.PresetIndex >= 0 ? InstantActionFeature.Presets[_instantAction.PresetIndex].Name : string.Empty;
                return null;
            case PageDownKey:
                _iaPage = 1;
                _host.FocusKey(PageUpKey);
                return null;
            case PageUpKey:
                _iaPage = 0;
                _host.FocusKey(PageDownKey);
                return null;
            case PlayerRadioKey:
                _iaRadio = 0;
                return null;
            case WingmanRadioKey:
                _iaRadio = 1;
                return null;
            case BuildKey:
                _host.OpenHangar(null);
                return null;
            case WeaponLoadoutKey:
                OpenLoadout();
                return null;
            case FlyMissionKey:
                if (_setup.Seats.Count > 1)
                {
                    // A second pilot joined here picks on the per-seat screen first; the launch
                    // then carries every seat.
                    return _host.BeginSeatWalk();
                }

                // A build flies its airframe's stock node with the def riding along; the def's
                // own player plane stays the airframe's stock name. An edited fit rides the seat.
                var pilot = PilotPick();
                var fit = PilotFit;
                return _instantAction.BuildExit(new[]
                {
                    new MenuSeatChoice(pilot.Node, Array.Empty<int>(), fit.IsStock ? null : fit, pilot.Custom),
                });
            case ExitKey:
                _host.Open(OriginalScreen.TopLevel);
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } open)
        {
            _iaOpen = row.Key;
            _iaListTop = 0;
            _host.FocusedRow = Math.Max(0, open.Current);
        }

        return null;
    }

    // The contents list's window, its thumb inside the list's right edge on the track between the
    // two arrows, placed by how far the window has scrolled and as long as the share of the list
    // that window shows; null while the presets fit the window.
    private ListWindow? ContentsWindow(MenuLayoutWidget list)
    {
        int window = Math.Max(1, list.Int("TotalDisplayed", 14));
        int count = InstantActionFeature.Presets.Count;
        if (count <= window)
        {
            return null;
        }

        float itemHeight = list.Int("ItemHeight", (int)FallbackItemHeight);
        float x = list.Int("X");
        float y = list.Int("Y");
        float width = list.Int("Width", 277);
        var arrow = StripSize(StripArt(list.Art, 1), FallbackArrowWidth, FallbackArrowHeight);
        var thumb = StripSize(StripArt(list.Art, 0, 1), arrow.Width, 11f);
        float height = window * itemHeight;
        float trackHeight = height - (2f * arrow.Height);
        float thumbHeight = ListWindow.ThumbHeightFor(trackHeight, window, count, thumb.Height);
        int top = ContentsTopClamped(window);
        return new ListWindow(
            x, y, width, height,
            x + width - arrow.Width, ListWindow.ThumbYFor(y + arrow.Height, trackHeight, thumbHeight, top, count - window), thumb.Width, thumbHeight,
            y + arrow.Height, trackHeight, count, window, top);
    }

    // An open dropdown's list window under its box: the authored TotalDisplayed rows, the arrows
    // inside its right edge and the thumb between them; null while the items fit the window.
    private ListWindow? OpenListWindow(MenuLayoutScreen screen) =>
        OpenInstantActionDrop(screen) is { } drop ? OriginalDropLists.Window(drop, _iaListTop, StripSize) : null;

    // Puts an open list's window at top, the focus pulled back inside it.
    private void ScrollOpenList(int top)
    {
        if (_layout.Screen(InstantActionSection) is { } screen && OpenInstantActionDrop(screen) is { } drop)
        {
            int focused = _host.FocusedRow;
            _iaListTop = OriginalDropLists.Scroll(drop, top, ref focused);
            _host.FocusedRow = focused;
        }
    }

    // The screen as drawn: the background under everything, the text rows, the widgets in their
    // states, and an open list as the overlay over the finished page.
    private void ComposeInstantAction(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        var screen = _layout.Screen(InstantActionSection);
        if (screen == null)
        {
            return;
        }

        if (screen.Widget("IA_BackGround") is { Art.Count: > 0 } background)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, background.Art[0], Math.Max(1, background.Frames)),
                background.Int("X"), background.Int("Y")));
        }

        AddText(screen, lines, "IA_T_TABLETITLE", TitleFont);
        AddText(screen, lines, "IA_T_TABLEINSTR", InstructionFont);
        AddText(screen, lines, "IA_T_STORYINSTR", InstructionFont);
        AddText(screen, lines, "IA_T_STORYTITLE", StoryFont, _iaStoryTitle);
        AddText(screen, lines, "IA_T_PLAYERLABEL", InstructionFont);
        AddText(screen, lines, "IA_T_WINGMANLABEL", InstructionFont);
        if (_iaPage == 0)
        {
            AddText(screen, lines, "IA_T_PILOTPLANETITLE", LabelFont);
            AddText(screen, lines, "IA_T_WINGMANTITLE", LabelFont);
            AddText(screen, lines, "IA_T_MISSIONTITLE", LabelFont);
            AddText(screen, lines, "IA_T_ENVIRONMENTTITLE", LabelFont);
            AddText(screen, lines, "IA_T_ENEMY0", LabelFont);
            AddText(screen, lines, "IA_T_CONTINUED", InstructionFont);
        }
        else
        {
            AddText(screen, lines, "IA_T_ENEMY1", LabelFont);
            AddText(screen, lines, "IA_T_ENEMY2", LabelFont);
            AddText(screen, lines, "IA_T_ENEMY3", LabelFont);
            AddText(screen, lines, "IA_T_GOBACK", InstructionFont);
        }

        // With a list open the rows are its items; the page under it is drawn from the closed
        // widgets with the open dropdown focused, and the items become the overlay.
        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        int widgetPressed = _host.PressedRow;
        if (_iaOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildInstantActionWidgets(screen, closed);
            widgets = closed;
            widgetFocus = -1;
            widgetPressed = -1;
            for (int i = 0; i < closed.Count; i++)
            {
                if (closed[i].Key == _iaOpen)
                {
                    widgetFocus = i;
                }
            }
        }

        // The pointer's own row is named rather than indexed, since with a list open the page under
        // it is a rebuilt set of closed widgets whose indices are not the hit-tested rows'; the hit
        // is re-checked, since an activation can swap the row set out from under an earlier index.
        int hover = _host.HoveredRow;
        string? under = hover >= 0 && hover < rows.Count && _host.Pointer is { } at && rows[hover].Contains(at.X, at.Y)
            ? rows[hover].Key
            : null;
        ComposeContentsThumb(screen, pictures);
        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeRow(widgets[i], i == widgetFocus, i == widgetPressed, i, fills, lines, plaques, pictures,
                lit: widgets[i].Key == under);
        }

        if (_iaOpen != null)
        {
            ComposeOpenList(screen, rows, focus, overlays, ItemFont);
        }

        // The seat strip in the same desk-margin band as the campaign boards': the page's own
        // words start at IA_T_TABLETITLE (155, 94), so the top-left corner is clear.
        if (_host.SeatPanel(onPaper: true) is { } strip)
        {
            overlays.Add(strip);
        }
    }

    // An open list as the overlay over the finished page: the visible items on a paper panel with
    // the focused one marked, then the arrows and the thumb once the list outruns its window. The
    // loadout screen's lists come through here too, in their own item font.
    private void ComposeOpenList(MenuLayoutScreen screen, IReadOnlyList<OriginalRow> rows, int focus, List<BoardPanel> overlays, float itemFont)
    {
        var panelFills = new List<BoardFill>();
        var panelLines = new List<BoardLine>();
        var panelPictures = new List<BoardPicture>();
        float top = float.MaxValue, bottom = float.MinValue, left = 0f, width = 0f;
        foreach (var row in rows)
        {
            if (row.Visible && row.Kind == OriginalRowKind.ListRow)
            {
                top = Math.Min(top, row.Y);
                bottom = Math.Max(bottom, row.Y + row.Height);
                left = row.X;
                width = row.Width;
            }
        }

        // The paper runs the authored box's full width, the scroll column included: the rows gave
        // that column up so their bands and their words keep off the chrome, not the panel.
        float panelWidth = _iaOpen != null && screen.Widget(_iaOpen) is { } opened ? DropBox(opened).Width : width;
        if (top < bottom)
        {
            panelFills.Add(new BoardFill(left, top, panelWidth, bottom - top, ListPaperR, ListPaperG, ListPaperB));
            panelFills.Add(new BoardFill(left, top, panelWidth, bottom - top, 0, 0, 0, 1f, Border: true));
        }

        // The list carries two bands at once, the picked value's and the row the cursor is on.
        // The picked one wins where they meet, so a row never wears two and an untouched list
        // still says what is picked; the words stay the page's own ink under either.
        int picked = _iaOpen != null && DropdownFor(_iaOpen) is { } open ? open.Current : -1;
        int pressed = _host.PressedRow;
        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            if (!item.Visible)
            {
                continue;
            }

            if (item.Kind == OriginalRowKind.Button && item.Art != null)
            {
                int frame = item.Enabled ? ComposedBoard.PlaqueFrame(item.Art.Frames, i == focus, i == pressed) : 0;
                panelPictures.Add(new BoardPicture(item.Art, item.X, item.Y, frame));
                continue;
            }

            if (picked >= 0 && OriginalDropLists.IndexOf(item.Key) == picked)
            {
                panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, PickedRowR, PickedRowG, PickedRowB));
            }
            else if (i == focus)
            {
                panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, HoveredRowR, HoveredRowG, HoveredRowB));
            }

            panelLines.Add(new BoardLine(item.Label, item.X + 4f, item.Y + 2f, item.Width - 8f, itemFont,
                item.Enabled ? BoardInk.Row : BoardInk.Detail, i));
        }

        if (OpenInstantActionDrop(screen) is { } drop && OriginalDropLists.Window(drop, _iaListTop, StripSize) is { } window
            && drop.Thumb is { } thumb)
        {
            panelPictures.Add(new BoardPicture(thumb, window.ThumbX, window.ThumbY, Height: window.ThumbHeight));
        }

        overlays.Add(new BoardPanel(panelFills, panelPictures, panelLines));
    }

    // One row of either paper page as drawn. ⚠ Do not move the dropdown's printed box onto the
    // focus alone. These pages draw it on every frame in black, which reads as a printed form
    // field and is the look they want; the Preferences family's painted plates are the pages that
    // take a focus box instead (OriginalShell.ComposePlateRow).
    private void ComposeRow(
        OriginalRow row, bool focused, bool pressed, int index,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures,
        float itemFont = ItemFont, bool lit = false)
    {
        switch (row.Kind)
        {
            case OriginalRowKind.ListRow:
                // The contents list draws its picked and focused rows as rectangles and nothing
                // else, the list widget's own frame and fill.
                if (ContentsIndex(row.Key) == _instantAction.PresetIndex)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.15f));
                }

                if (focused)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 1f, Border: true));
                }

                lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 2f, row.Width - 8f, ItemFont,
                    focused ? BoardInk.RowFocused : BoardInk.Row, index));
                break;
            case OriginalRowKind.Dropdown:
                // Under the pointer the printed black box is redrawn in the page's own cream,
                // which is the whole of the rollover: the fill inside it does not move. The wash
                // stays for a row a keyboard or pad walked onto, so the two never land together.
                if (focused && !lit)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
                }

                fills.Add(lit
                    ? new BoardFill(row.X, row.Y, row.Width, row.Height, LitBoxR, LitBoxG, LitBoxB, 1f, Border: true)
                    : new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 1f, Border: true));

                float arrowWidth = 0f;
                if (row.Art != null)
                {
                    var size = StripSize(row.Art, FallbackArrowWidth, FallbackArrowHeight);
                    arrowWidth = size.Width;
                    int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                    pictures.Add(new BoardPicture(row.Art, row.X + row.Width - size.Width, row.Y + ((row.Height - size.Height) / 2f), frame));
                }

                lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 2f, Math.Max(1f, row.Width - arrowWidth - 6f), itemFont,
                    focused ? BoardInk.RowFocused : BoardInk.Row, index));
                break;
            case OriginalRowKind.Radio when row.Art != null:
                // An eight-state strip: the four button states unmarked, then the same four marked.
                bool checkedRadio = (row.Key == PlayerRadioKey) == (_iaRadio == 0);
                int state = row.Enabled ? (pressed ? 3 : focused ? 2 : 1) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, (checkedRadio ? 4 : 0) + state, string.Empty, BoardInk.LabelNormal));
                break;
            case OriginalRowKind.TextButton when row.Art != null:
                int labelFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                var ink = row.Enabled ? ComposedBoard.PlaqueInk(focused, pressed) : BoardInk.Detail;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, labelFrame, row.Label, ink));
                break;
            case OriginalRowKind.Button when row.Art != null:
                int stripFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, stripFrame, string.Empty, BoardInk.LabelNormal));
                break;
        }
    }

    // The contents list's slider thumb, on the track between its two arrows, placed by how far the
    // window has scrolled and stretched to the length the window gives it, which is what says how
    // much of the list is in view.
    private void ComposeContentsThumb(MenuLayoutScreen screen, List<BoardPicture> pictures)
    {
        if (screen.Widget(ContentsKey) is { } list && StripArt(list.Art, 0, 1) is { } thumb && ContentsWindow(list) is { } window)
        {
            pictures.Add(new BoardPicture(thumb, window.ThumbX, window.ThumbY, Height: window.ThumbHeight));
        }
    }

    private void AddText(MenuLayoutScreen screen, List<BoardLine> lines, string key, float size, string? text = null)
    {
        if (screen.Widget(key) is not { } widget)
        {
            return;
        }

        string words = text ?? widget.Text ?? string.Empty;
        if (words.Length == 0)
        {
            return;
        }

        lines.Add(new BoardLine(words, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size,
            IsWhite(widget) ? BoardInk.Dialog : BoardInk.Heading, -1, false, Justify(widget)));
    }

    private UiStrings LoadoutStrings => _host.MenuStrings;

    private static int OptionIndex(IReadOnlyList<LoadoutOption> options, string id)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // The fill-order entry a physical pylon occupies inside the def's hardpoint count, or -1 when
    // the airframe carries no hardpoint there.
    private static int PylonEntry(LoadoutDef? def, int pylon)
    {
        int count = def?.Hardpoints?.Count ?? 0;
        for (int i = 0; i < count && i < Loadout.PylonFillOrder.Length; i++)
        {
            if (Loadout.PylonFillOrder[i] == pylon)
            {
                return i;
            }
        }

        return -1;
    }

    private static string StockPylon(LoadoutDef def, int entry) =>
        def.Hardpoints != null && entry < def.Hardpoints.Stock.Length ? def.Hardpoints.Stock[entry] : LoadoutChoice.None;

    // The firable gun a slot mounts, or null: a turret slot is built inert, so a pick there would
    // change nothing, and the original's screen shows no field for it.
    private static GunSpec? GunFor(LoadoutDef? def, int slot)
    {
        if (def == null)
        {
            return null;
        }

        foreach (var gun in def.Guns)
        {
            if (gun.Slot == slot && !gun.Turret)
            {
                return gun;
            }
        }

        return null;
    }

    // The screen over one aeroplane's fit, whichever door opened it: the airframe's stock def and
    // the option lists, then the picks standing on entry, which CANCEL and Back restore.
    private void BeginLoadout(string node, string name, LoadoutChoice fit, PlayerSeat? seat)
    {
        _loadoutNode = node;
        _loadoutName = name;
        _loadoutFit = fit;
        _loadoutSeat = seat;
        var stock = _stock?.Invoke();
        _loadoutDef = stock?.ForModel(_loadoutNode);
        _loadoutOptions = stock?.Options ?? new LoadoutOptions();
        for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
        {
            _loadoutBefore[slot - 1] = fit.GunAmmoFor(slot);
        }

        for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
        {
            _loadoutBefore[LoadoutChoice.MaxGunSlot + pylon - 1] = fit.PylonFor(pylon);
        }

        _iaOpen = null;
        _host.Open(OriginalScreen.InstantActionLoadout);
    }

    // Leaves the screen for whichever one opened it: the picks stay on ACCEPT and go back to what
    // stood on entry otherwise, so the shared fit reads as a working copy. The focus lands on the
    // row that opened the screen, so a second visit is one press away.
    private void CloseLoadout(bool keep)
    {
        if (!keep && _loadoutFit is { } fit)
        {
            for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
            {
                fit.SetGunAmmo(slot, _loadoutBefore[slot - 1]);
            }

            for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
            {
                fit.SetPylon(pylon, _loadoutBefore[LoadoutChoice.MaxGunSlot + pylon - 1]);
            }
        }

        bool seated = _loadoutSeat != null;
        DropLoadout();
        _host.Open(seated ? OriginalScreen.SeatPlane : OriginalScreen.InstantAction);
        _host.FocusKey(seated ? nameof(BoardButton.ChangeAmmo) : WeaponLoadoutKey);
    }

    // The rows: an open list's items alone while one is open, else the fields the airframe has
    // and the two buttons, all one column.
    private void BuildLoadoutRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(LoadoutSection);
        if (screen == null || AddOpenListRows(screen, rows))
        {
            return;
        }

        for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
        {
            if (GunFor(_loadoutDef, slot) != null)
            {
                AddDropdown(screen, rows, LoadoutAmmoPrefix + (slot - 1).ToString(CultureInfo.InvariantCulture), 0);
            }
        }

        for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
        {
            if (PylonEntry(_loadoutDef, pylon) >= 0)
            {
                AddDropdown(screen, rows, LoadoutRocketPrefix + (pylon - 1).ToString(CultureInfo.InvariantCulture), 0);
            }
        }

        AddStrip(screen, rows, LoadoutAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, LoadoutCancelKey, OriginalRowKind.Button, true, 0);
    }

    // A field's list over the stock table's options: the standing pick is the fit's, else the
    // def's own stock value; a pick writes the option's id into the fit.
    private DropdownList? LoadoutDropdownFor(string key)
    {
        if (_loadoutFit is not { } fit || _loadoutDef is not { } def)
        {
            return null;
        }

        if (OriginalWidgets.Indexed(key, LoadoutAmmoPrefix) is { } group && GunFor(def, group + 1) is { } gun)
        {
            var options = _loadoutOptions.GunAmmo;
            int slot = group + 1;
            return new DropdownList(Names(options, o => o.Label), OptionIndex(options, fit.GunAmmoFor(slot) ?? gun.Ammo), _ => true,
                i => fit.SetGunAmmo(slot, options[i].Id));
        }

        if (OriginalWidgets.Indexed(key, LoadoutRocketPrefix) is { } cell && PylonEntry(def, cell + 1) is >= 0 and var entry)
        {
            var options = _loadoutOptions.PylonOrdnance;
            int pylon = cell + 1;
            return new DropdownList(Names(options, o => o.Label), OptionIndex(options, fit.PylonFor(pylon) ?? StockPylon(def, entry)), _ => true,
                i => fit.SetPylon(pylon, options[i].Id));
        }

        return null;
    }

    private MenuExit? ActivateLoadout(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0)
        {
            string prefix = row.Key[..colon];
            if (DropdownFor(prefix) is { } list)
            {
                int index = int.Parse(row.Key[(colon + 1)..], CultureInfo.InvariantCulture);
                if (list.Allowed(index))
                {
                    list.Select(index);
                }

                _iaOpen = null;
                _host.FocusKey(prefix);
            }

            return null;
        }

        switch (row.Key)
        {
            case LoadoutAcceptKey:
                CloseLoadout(keep: true);
                return null;
            case LoadoutCancelKey:
                CloseLoadout(keep: false);
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } open)
        {
            _iaOpen = row.Key;
            _host.FocusedRow = Math.Max(0, open.Current);
        }

        return null;
    }

    // The screen as drawn: the section's background, the airframe's two diagram frames, its text
    // rows with the gun captions filled in, the fields and buttons, the focused field's description
    // in its pane, and an open list as the overlay.
    private void ComposeLoadout(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        var screen = _layout.Screen(LoadoutSection);
        if (screen == null)
        {
            return;
        }

        AddPane(screen, backdrop, "OL_BACKGROUND");
        int airframe = _loadoutNode != null ? PlanePickerRoster.AirframeOf(_loadoutNode) ?? 0 : 0;
        foreach (string key in new[] { "OL_P_PLANETOPICON", "OL_P_PLANEFRTICON" })
        {
            if (screen.Widget(key) is { Art.Count: > 0 } diagram)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, diagram.Art[0], Math.Max(1, diagram.Frames)),
                    diagram.Int("X"), diagram.Int("Y"), airframe));
            }
        }

        ComposeLoadoutText(screen, lines);
        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        int widgetPressed = _host.PressedRow;
        if (_iaOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildLoadoutRows(closed);
            widgets = closed;
            widgetFocus = -1;
            widgetPressed = -1;
            for (int i = 0; i < closed.Count; i++)
            {
                if (closed[i].Key == _iaOpen)
                {
                    widgetFocus = i;
                }
            }
        }

        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeRow(widgets[i], i == widgetFocus, i == widgetPressed, i, fills, lines, plaques, pictures, LoadoutItemFont);
        }

        if (widgetFocus >= 0 && widgetFocus < widgets.Count)
        {
            ComposeLoadoutDescription(screen, lines, widgets[widgetFocus].Key);
        }

        if (_iaOpen != null)
        {
            ComposeOpenList(screen, rows, focus, overlays, LoadoutItemFont);
        }
    }

    // The shared pane rule (OriginalWidgets) over this module's own measurer, bound here so no call
    // site has to carry it.
    private (float X, float Y) PaneOrigin(MenuLayoutScreen screen, string key) =>
        OriginalWidgets.PaneOrigin(screen, key, _measure);

    private void AddPane(MenuLayoutScreen screen, List<BoardPicture> pictures, string key) =>
        OriginalWidgets.AddPane(screen, pictures, key, _measure);

    // The section's text rows at their authored places: the title and the panel headings as
    // authored, the plane-info row naming the fitted aircraft, and each gun caption as the slot's
    // calibre or the no-gun marker.
    private void ComposeLoadoutText(MenuLayoutScreen screen, List<BoardLine> lines)
    {
        var strings = LoadoutStrings;
        foreach (var widget in screen.Widgets)
        {
            if (widget.TypeCode != "T")
            {
                continue;
            }

            string text = widget.Text ?? string.Empty;
            float size = LoadoutHeadingFont;
            var ink = BoardInk.Heading;
            if (widget.Key == "OL_T_TITLE")
            {
                size = LoadoutTitleFont;
            }
            else if (widget.Key == "OL_T_PLANEINFO")
            {
                text = _loadoutName;
            }
            else if (OriginalWidgets.Indexed(widget.Key, "OL_T_GunName") is { } group)
            {
                var gun = GunFor(_loadoutDef, group + 1);
                text = gun != null
                    ? strings.Text(CalibreLabel + Math.Clamp((gun.Caliber - 30) / 10, 0, 4), $" .{gun.Caliber}-cal.").Trim()
                    : strings.Text(NoGunLabel, "No Gun");
                size = LoadoutCaptionFont;
                ink = gun != null ? BoardInk.Row : BoardInk.Detail;
            }
            else if (widget.Key.EndsWith("CAPTION", StringComparison.Ordinal))
            {
                size = LoadoutCaptionFont;
                ink = BoardInk.Detail;
            }

            if (text.Length > 0)
            {
                lines.Add(new BoardLine(text, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size, ink, -1, false, Justify(widget)));
            }
        }
    }

    // The focused field's description in the pane beside its panel, the string table's title and
    // body for the standing option, wrapped to the pane's authored width.
    private void ComposeLoadoutDescription(MenuLayoutScreen screen, List<BoardLine> lines, string key)
    {
        if (_loadoutFit is not { } fit || _loadoutDef is not { } def)
        {
            return;
        }

        string pane;
        int index;
        int title;
        int body;
        IReadOnlyList<LoadoutOption> options;
        if (OriginalWidgets.Indexed(key, LoadoutAmmoPrefix) is { } group && GunFor(def, group + 1) is { } gun)
        {
            pane = "OL_S_AMMODESC";
            options = _loadoutOptions.GunAmmo;
            index = OptionIndex(options, fit.GunAmmoFor(group + 1) ?? gun.Ammo);
            title = AmmoTitleLabel;
            body = AmmoBodyLabel;
        }
        else if (OriginalWidgets.Indexed(key, LoadoutRocketPrefix) is { } cell && PylonEntry(def, cell + 1) is >= 0 and var entry)
        {
            pane = "OL_S_ROCKETDESC";
            options = _loadoutOptions.PylonOrdnance;
            index = OptionIndex(options, fit.PylonFor(cell + 1) ?? StockPylon(def, entry));
            title = RocketTitleLabel;
            body = RocketBodyLabel;
        }
        else
        {
            return;
        }

        if (index < 0 || screen.Widget(pane) is not { } box)
        {
            return;
        }

        var strings = LoadoutStrings;
        string words = strings.Text(title + index, options[index].Label);
        string detail = strings.Text(body + index, string.Empty);
        if (detail.Length > 0)
        {
            words = words + " - " + detail;
        }

        lines.Add(new BoardLine(words, box.Int("X") + 4f, box.Int("Y") + 4f, Math.Max(1, box.Int("Width", 172) - 8), LoadoutCaptionFont, BoardInk.Row));
    }

    // One dropdown's list, its picked row, which rows may be picked and what picking one does.
    private sealed record DropdownList(IReadOnlyList<string> Items, int Current, Func<int, bool> Allowed, Action<int> Select);
}
