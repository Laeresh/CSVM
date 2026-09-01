using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The in-game launchscreen shown on a bare launch. Free Flight and Dogfight go straight to
/// Chapter then Plane; Instant Action opens its own five-step wizard (Environment, MissionType,
/// Waves, Wingmen, Plane). Dogfight withholds the launch gesture until two players have joined;
/// see <see cref="CanLaunch"/>. Input is polled per player through <see cref="MenuInput"/> rather
/// than Godot's input map, since the join flow needs a named device. More than one player splits
/// the Plane screen into <see cref="SplitScreen.PaneRect"/> panes. Re-entrant on return from
/// flight; see <see cref="ShowMenu"/>. The Built-in presentation's screen graph: player 1's
/// commands arrive through the host's first seat, every launch and the quit leave through
/// <see cref="IMenuHost.Exit"/>, and narration plays through the host's audio service. Module
/// map: docs/architecture.md. Wizard decode: docs/formats/instant-action.md.
/// </summary>
public sealed partial class LaunchMenu : CanvasLayer
{
    /// <summary>The row that opens the hangar, on the Mode screen and on the Instant Action plane
    /// pick. The original has no button string of its own for it; this is the phrase its own help
    /// text uses (langui 10524).</summary>
    public const string HangarRow = "Build Custom Plane";

    /// <summary>The row that opens the campaign, past the three modes and before the hangar's own
    /// door. The campaign is not a <see cref="MenuMode"/>: it owns its screens through
    /// <see cref="CampaignFlow"/> and only launches a mission from inside them.</summary>
    public const string CampaignRow = "Campaign";

    /// <summary>The row that opens the Options screen, the last on the Mode screen. Options hold
    /// the one process-wide choice so far, the menu presentation, and every presentation exposes
    /// them so a player can always get back to Built-in.</summary>
    public const string OptionsRow = "Options";

    // Base metrics at 720p, scaled up on taller viewports (like StuntScoreboard). All TUNE.
    private const int TitleFont = 40;
    private const int HeadingFont = 20;
    private const int CrumbFont = 15;
    private const int RowFont = 22;
    private const int DetailFont = 16;
    private const int FooterFont = 15;
    private const int ErrorFont = 15;

    // The centred layout's content column at 720p: the width every band's text is centred in and
    // the description wraps at, and the vertical padding the header keeps above the title and the
    // footer below the controls line.
    private const int ContentWidth = 560;
    private const int ZonePad = 18;
    // How many description lines the footer reserves. The band's height must not depend on the
    // focused row, so the slot is this tall whatever the row says; a longer description than this
    // wraps into the space above the controls line rather than moving them.
    private const int DetailReserveLines = 2;
    // The separation every band's content column carries between its children.
    private const int ZoneSeparation = 6;

    // The hangar art block's 720p height; the 358x335 TGAs letterbox into it. It
    // stands beside the rows, so it is as tall as the column has room for rather than
    // as short as a block over them had to be.
    private const int HangarArtHeight = 200;
    // Its column's 720p width, to the left of the rows (E47b), and the focused row's own smaller
    // picture under it (E48's 66x66 decal tile).
    private const int HangarArtWidth = 220;

    // How long a pressed plaque holds its depressed frame. Short enough not to lag a press, long
    // enough to be seen at any frame rate the boards run at.
    private const int PressFrames = 6;
    private const int HangarRowArtHeight = 66;
    // Splitscreen plane select (several players): the bottom strip that keeps the breadcrumb +
    // join hint out of the panes, as a fraction of viewport height, and the pane's inner padding.
    // Reference values at 720p. Confirmed at the controls: join/lock feel
    // reads right at 2P and 4P, no retune owed.
    private const float StripHeightFrac = 0.12f;
    private const int PanePad = 10;
    // How many Table of Contents rows the list shows at once. `ia_tl_contents` is one of only two
    // LAYOUT.CSV rows whose last column is a visible-row WINDOW rather than an item count: 14 rows
    // onto the 19 presets (docs/formats/instant-action.md, "Screen controls"). Decoded, not a fit
    // to our own layout — do not "tidy" it to the item count.
    private const int PresetWindow = 14;
    // How many missions the campaign screenshot aids' seeded profile has flown. Three gives the
    // previous-missions list a body and leaves the cabin's Next Mission somewhere other than the
    // campaign's first entry. Raising it walks campaign-briefing onto a longer narration than
    // campaign-briefing-repaint's own 90-second window covers.
    private const int AidMissionsFlown = 3;

    // The completed-objective mask those runs record. Bit 0 alone would leave every scrapbook page
    // blank, since the story scraps are gated on the objectives that unlock them, so the aid
    // records a clean run: bits 0 to 12, the range the shipped rows' own gates use.
    private const int AidCompletedMask = 0x1fff;

    // The lives stepper's range (Screen.MissionType, decision 15/18): 0 = unlimited, 1 = the
    // faithful one-life run (default), up to this cap. INVENTED — ia.json carries no such field, so
    // there is no decoded range to match; TUNE.
    private const int MaxLives = 9;

    // The chip strip's own font size and corner inset, in authored board points — scaled through
    // the same BoardFit the board itself draws at, so the chips read like part of that screen.
    private const float ChipFont = 16f;
    private const float ChipInset = 14f;

    // The three top-level modes, in MenuMode's ordinal order so the row index doubles as the
    // enum value. The enum member stays named Stunt (SessionSpec.cs) though this row reads
    // "Instant Action"; picking it opens the Environment wizard, not the plain Chapter screen.
    // See docs/architecture.md.
    private static readonly Choice[] Modes =
    {
        new("Free Flight", "Explore the map freely — no objectives, no clock."),
        new("Instant Action", "Pick an environment and a mission — ace, squadron, stunt or zeppelin."),
        new("Dogfight", "Splitscreen free-for-all — first to the kill target wins."),
    };

    // The seven Instant Action environments, in the decoded dropdown order (`FUN_004174d0`;
    // docs/formats/instant-action.md "Environment → chapter") — NOT the alphabetic order `Chapters`
    // below uses for Free Flight/Dogfight's plain Chapter screen. C1C is not offered here (the
    // chapter Instant Action omits); its DangerZones flag is looked up from `Chapters` by code
    // rather than duplicated, so the two tables cannot drift apart on that value.
    private static readonly (string Name, string Code)[] Environments =
    {
        ("an airfield", "C1"),
        ("the clouds", "C2B"),
        ("Hawaii", "C3"),
        ("Manhattan", "C5"),
        ("the ocean", "C1B"),
        ("Sky Haven", "C4"),
        ("a movie studio", "C2"),
    };

    // The four Instant Action mission types in the UI dropdown's own order (`IDS_IA_MISSIONTYPE`,
    // 3660) — NOT the internal id order (`docs/formats/instant-action.md` "Mission types have
    // internal ids"). Key is the ia.json `mission_type` string every consumer already uses; the
    // LABELS come from InstantAction.MissionTypeLabel, so this screen, the load screen and the
    // wrap-up board cannot name one mission three ways. Unfiltered: CurrentMissionTypes drops
    // Stunt Flying where the chapter's `disallow_missions` bars it (decoded: only C2B of the seven).
    private static readonly (string Label, string Key)[] MissionTypes =
    {
        (Mech3.InstantAction.MissionTypeLabel("dogfight_ace"), "dogfight_ace"),
        (Mech3.InstantAction.MissionTypeLabel("dogfight_squadron"), "dogfight_squadron"),
        (Mech3.InstantAction.MissionTypeLabel("stunt_flying"), "stunt_flying"),
        (Mech3.InstantAction.MissionTypeLabel("zeppelin_run"), "zeppelin_run"),
    };

    // The eight chapter worlds: this screen's row text over the shared roster (MenuChapters owns
    // the codes, their order and the Danger Zones flag). ChaptersFor hides the flag-less chapters
    // from Stunt Flying, and a stunt run forced onto them via CLI falls back to free flight
    // (StuntMission).
    private static readonly (string Name, string Code, bool DangerZones)[] Chapters = BuildChapters();

    // The player-flyable roster in the langui 3700 dropdown order (docs/formats/instant-action.md
    // "Option strings"), which the original stores an aircraft as an index INTO — so this order is
    // decoded, not cosmetic, and the preset table's aircraft resolve through it. Display names are
    // ia.json's singular vocabulary ("Autogyro", not 3700's plural "Hoplite"); note Devastator =
    // player_pfighter and Hellhound = player_avenger. Node = the planes.zbd root node passed on to
    // the build; stats are loaded lazily from vehicle.json for the focused plane.
    private static readonly (string Name, string Node)[] Planes =
    {
        ("Autogyro", "player_autogyro"),
        ("Hellhound", "player_avenger"),
        ("Balmoral", "player_balmoral"),
        ("Bloodhawk", "player_bhawk"),
        ("Brigand", "player_brigand"),
        ("Devastator", "player_pfighter"),
        ("Firebrand", "player_fbrand"),
        ("Fury", "player_fury"),
        ("Kestrel", "player_kestrel"),
        ("Peacemaker", "player_peacemaker"),
        ("Warhawk", "player_warhawk"),
    };

    // The thirteen Instant Action militias and the aircraft each one flies, per the `.BM` pattern
    // reading (docs/formats/instant-action.md), not vehicle.json's paint_pattern defs. Each roster
    // is Planes's own langui 3700 order filtered to the militia's allowed flags, which is what
    // FUN_00410420's 11-byte mask yields — the dropdown never reorders per militia. Names use
    // ia.json's singular vocabulary ("Autogyro"), matching InstantActionWave.EnemyPlane and
    // PlaneNodeFor. A militia is never filtered out here for being the player's own side.
    private static readonly (string Name, string[] Aircraft)[] Militias =
    {
        ("Black Hat", new[] { "Autogyro", "Brigand", "Warhawk" }),
        ("Black Swan", new[] { "Fury" }),
        ("Blake Aviation", new[] { "Bloodhawk", "Peacemaker" }),
        ("British", new[] { "Balmoral", "Peacemaker" }),
        ("Fortune Hunter", PlaneNames()),
        ("Hollywood Knight", new[] { "Firebrand" }),
        ("Hughes Aviation", new[] { "Bloodhawk", "Fury", "Kestrel" }),
        ("Medusa", new[] { "Brigand", "Kestrel" }),
        ("Russian", new[] { "Devastator" }),
        ("Sacred Trust", new[] { "Hellhound", "Warhawk" }),
        ("German", new[] { "Hellhound" }),
        ("Studio Security", new[] { "Autogyro", "Fury" }),
        ("Broadway Bomber", new[] { "Peacemaker" }),
    };

    // The three Instant Action skills (`IDS_IA_SKILLS`, 3695) — the wave editor's Skill field.
    // Internal keys, matching InstantActionWave.EnemySkill's own vocabulary; display capitalises.
    private static readonly string[] Skills = { "novice", "veteran", "ace" };

    private static readonly Color TitleColor = new(0.96f, 0.80f, 0.35f);
    private static readonly Color CrumbColor = new(0.55f, 0.68f, 0.86f);
    private static readonly Color HeadingColor = new(0.80f, 0.88f, 0.98f);
    private static readonly Color RowColor = new(0.55f, 0.62f, 0.72f);
    private static readonly Color RowFocusColor = new(1f, 0.86f, 0.38f);
    private static readonly Color DetailColor = new(0.66f, 0.78f, 0.92f);
    private static readonly Color FooterColor = new(0.52f, 0.60f, 0.70f);
    private static readonly Color ErrorColor = new(1f, 0.55f, 0.45f);

    // A selected-but-not-yet-flying aircraft. Distinct from RowFocusColor on purpose: the two
    // stages of the pick are the one thing on this screen a pilot must be able to tell apart at
    // a glance, and "the cursor is here" and "this is chosen" would otherwise look identical.
    private static readonly Color RowLockedColor = new(0.55f, 0.95f, 0.62f);

    private readonly Dictionary<string, PlaneStats?> _stats = new();
    // The joined players, player 1 first. Never empty once ShowMenu has run.
    private readonly List<Slot> _slots = new();
    // Previous-frame Start state of every connected pad, for edge-detecting the join gesture on
    // pads that have no player (and therefore no MenuInput) yet.
    private readonly Dictionary<int, bool> _joinPrev = new();
    // Steps 3-4's wizard wave slots: 0 enemies = unconfigured — the wizard's own "starts
    // empty" divergence from the original's always-four dropdowns, decision 1.
    private readonly WaveSlot[] _waves = new WaveSlot[4];
    // The menu host: its first seat is player 1's commands, its feature set holds Free Flight's
    // state and launch rule, its audio service plays the narration, and every exit goes to it.
    private IMenuHost _host = null!;
    private FreeFlightFeature _free = null!;
    // The raw poller behind the host's first seat, player 1's for the pad bookkeeping (claiming,
    // hotplug) that still lives here; the commands themselves are read through the seat.
    private MenuInput _player1 = null!;

    private string _zrdrPath = "";
    private string _dataRoot = "";
    private Screen _screen = Screen.Mode;
    private int _modeIndex, _chapterIndex;
    // The Options screen's cursor and the presentation its first row would apply, seeded from the
    // saved request when the screen opens so it shows back what was asked for, not what is active.
    private int _optionsIndex;
    private string _presentationChoice = PresentationId.BuiltIn.Value;
    // Instant Action wizard state, steps 1-2: the picked environment
    // row, the picked mission type row within CurrentMissionTypes, and the lives stepper beside it
    // (decision 18).
    private int _environmentIndex, _missionTypeIndex;
    private int _lives = 1;
    // The Table of Contents: which preset is applied (-1 = none, the custom path), the list cursor
    // and the first visible row of its 14-row window. Applying one writes the wizard's own fields
    // and nothing else, so _presetIndex is a LABEL for the breadcrumb — the mission that flies is
    // whatever the fields say, byte-identical to the same configuration entered by hand.
    private int _presetIndex = -1;
    private int _presetCursor, _presetTop;
    // Steps 3-4: the wingman count + aircraft, and the cursors WaveEdit/Waves/Wingmen each
    // read (_waves itself is above, with the other readonly fields). _wingmanPlaneIndex starts at
    // Planes's index 0, which the 3700 order makes the Autogyro — the value gui_continue selects
    // when the player has no saved custom planes, not an arbitrary first row.
    private int _waveListIndex, _waveEditIndex, _waveFieldIndex;
    private int _numWingmen, _wingmanPlaneIndex, _wingmenFieldIndex;
    // The chosen environment's own shipped ia.zrd.json, loaded once when Environment is
    // confirmed and reused as FireLaunch's base: the ace, the zeppelin node names and
    // disallow_missions are chapter-level facts the wizard has no control to edit, so they carry
    // over from here unedited rather than defaulting to the built-in generic ace/zeppelin. Null
    // until an environment has actually been confirmed once (a --menu= screenshot never launches).
    private InstantActionDef? _iaBaseDef;
    // The stock-fit table and the Ammo Selection rosters, loaded once on first use: the loadout
    // rows are built from an airframe's own gun slots and pylon count, which only this file knows.
    private StockLoadouts? _stockFits;
    // Every human plane picker's roster: the eleven stock airframes then the store's saved
    // customs (PlanePickerRoster, Decision 6). Refreshed by ShowMenu and CloseHangar
    // (RefreshRoster), so a new save appears without a menu restart. Starts stock-only: reading
    // user:// needs the engine, which a bare construction (tests, --menu= screenshots before
    // ShowMenu) may not have.
    private IReadOnlyList<PickerPlane> _roster = PlanePickerRoster.Build(Planes, Array.Empty<CustomPlaneDef>());
    // The one wingman fit (the original's Player/Wingman radio is not per wingman) and its row
    // cursor. Cleared when the wingman aircraft changes, since pylon count and gun slots are
    // per-airframe and a fit for one has nowhere to live on another.
    private LoadoutChoice _wingmanFit = new();
    private int _wingmanFitRow;
    private MenuMode _mode;
    // The Build Custom Plane flow while it is open, and the screen it was opened from. Both
    // doors (the Mode screen's trailing row, the Instant Action plane pick) come through
    // OpenHangar, so cancelling always lands back where the pilot pressed.
    private HangarFlow? _hangar;
    private Screen _hangarReturn = Screen.Mode;
    // The page's narration count as last acted on: the host's audio service is asked to begin the
    // narration whenever it moves and to end it the moment the briefing stops showing.
    private int _narrationStarts;
    // Whether the briefing's reveal was running on the previous frame, which is what buys the one
    // repaint after it finishes: the frame that lands the last tween is the frame that stops
    // being a running reveal.
    private bool _briefingRunning;
    // The campaign's out-of-mission flow while it is open. One door (the Mode screen's Campaign
    // row); its own screens are the flow's pages, so a new one needs no change here.
    private CampaignFlow? _campaign;
    // A character reached a plane's name since the last frame, so the menu owes a redraw that no
    // polled input asked for.
    private bool _typed;
    // The hangar page's art, as the one texture the shell owns: rebuilt only when
    // the page hands over a different decoded image, since Rebuild runs on every keypress.
    private TgaImage? _hangarArtSource;
    private ImageTexture? _hangarArtTexture;
    // The same pair again for the focused row's own picture (E48's decal tile), which changes on a
    // different beat from the page's art and so cannot share the one slot.
    private TgaImage? _hangarRowArtSource;
    private ImageTexture? _hangarRowArtTexture;
    // The langui table, loaded on first hangar entry (a session that never opens it never reads
    // the file). Null until then; a failed load leaves UiStrings.Empty here.
    private UiStrings? _uiStrings;
    private string _error = "";
    // The pad player 1 claimed by driving the Mode/Chapter screens with it (−1 = none yet, i.e.
    // player 1 is on the keyboard and every connected pad is still free to join).
    private int _p1Pad = -1;
    // The join strip as last drawn — _Process redraws when the live roster changes (hotplug).
    private string _stripText = "";
    // --menu=campaign-guestcheck: which guest's flight check the aid asked for, applied by
    // DebugJoin, since the aid runs inside ShowMenu and the players arrive right after it.
    private int _aidGuest;
    // The viewport as the layout was last built for. Every metric here is derived from it, so a
    // resize or a resolution change owes a repaint that no keypress asked for.
    private Vector2 _viewSize;
    // The centred layout's three bands, top to bottom: header (title, breadcrumb, join strip),
    // middle (heading, rows, the art column beside them), footer (the focused row's description,
    // the error slot, the controls line). Each holds one centred content column rebuilt per frame;
    // the bands themselves are built once and keep the fixed heights MenuZones gives them.
    private VBoxContainer _zones = null!;
    private HBoxContainer _header = null!;
    private HBoxContainer _middle = null!;
    private HBoxContainer _footer = null!;
    // The splitscreen plane-select root (one panel per player + a shared bottom strip). Shown
    // instead of _zones on the Plane screen once more than one player has joined.
    private Control _paneRoot = null!;

    // The campaign's composed-board surface, drawn instead of _zones on every campaign screen.
    private ComposedBoardView _boardRoot = null!;

    // The campaign chip strip: `P1 P2 P3 P4` in `SplitScreen.PlayerColor`, top-right, shown while a
    // campaign board is up AND more than one has joined — a solo campaign board looks exactly as
    // it does today. A composed board draws no full join strip by design (RebuildBoard's own
    // comment), so this is a deliberate exception drawn as a shell overlay rather than a page
    // contribution: CampaignBoards' authored geometry has nowhere to put a live, per-frame roster.
    private HBoxContainer _chipStrip = null!;

    // Frames left to draw the pressed plaque depressed. The original's own button art carries that
    // frame, and a confirm that changes nothing on screen reads as a dead button on a pad.
    private int _pressFrames;

    private enum Screen { Mode, Chapter, Presets, Environment, MissionType, Waves, WaveEdit, Wingmen, Plane, WingmanLoadout, Hangar, Campaign, Options }

    // What a fit row edits. The reset row carries no slot of its own and is the only one Accept
    // does anything on, since every other row is a live stepper.
    private enum FitRowKind { Gun, Pylon, Reset }

    /// <summary>The name of the last plane the hangar built this session, or "".
    /// <see cref="CloseHangar"/> reads it back out of the refreshed roster to auto-select the
    /// just-built plane (the original's index-11 contract, by name).</summary>
    public string LastBuiltPlane { get; private set; } = "";

    /// <summary>The campaign flow while one is open, or null. Read-only: the flow's own screens
    /// are driven through it, not around it.</summary>
    public CampaignFlow? Campaign => _campaign;

    /// <summary>The hangar flow while one is open, or null. Read-only, for the same reason
    /// <see cref="Campaign"/> is: its screens are driven through it, not around it.</summary>
    public HangarFlow? Hangar => _hangar;

    /// <summary>The composed board currently on screen, or null when no campaign screen is up.
    /// This is what the pilot is looking at, so a check that the screen keeps up with a running
    /// briefing reveal compares it against a board freshly composed from the page.</summary>
    public ComposedBoard? ShownBoard => _boardRoot?.Board;

    /// <summary>How the centred layout divides the window right now. A check that the screen does
    /// not jump reads this as the cursor moves: the two fixed bands must not move with it.</summary>
    public MenuZones ShownZones => Zones();

    /// <summary>The header band, the fixed one at the top. Public so a check can read the height
    /// the layout is actually holding it at rather than the one it was told to.</summary>
    public Control HeaderBand => _header;

    /// <summary>The footer band, the fixed one at the bottom.</summary>
    public Control FooterBand => _footer;

    /// <summary>The screen showing, by its own name (Mode, Chapter, Plane, ...). A journey check
    /// reads where a press landed without knowing how the screen is drawn.</summary>
    public string ShownScreen => _screen.ToString();

    /// <summary>The middle band's heading as the screen draws it right now.</summary>
    public string ShownHeading => Heading();

    /// <summary>The header band's breadcrumb as the screen draws it right now.</summary>
    public string ShownBreadcrumb => Breadcrumb();

    /// <summary>The footer's controls line as the screen draws it right now.</summary>
    public string ShownFooter => Footer();

    /// <summary>The focused row's description, or the refusal standing in its slot.</summary>
    public string ShownDetail => _error.Length > 0 ? _error : Detail(CurrentIndex);

    /// <summary>The hint beside the join strip, which names where joining opens.</summary>
    public string ShownJoinHint => JoinHint();

    /// <summary>Player 1's cursor row on the screen showing.</summary>
    public int ShownRow => CurrentIndex;

    /// <summary>How many rows the screen showing has.</summary>
    public int ShownRowCount => CurrentCount();

    /// <summary>The text drawn on player 1's cursor row.</summary>
    public string ShownRowText => RowText(CurrentIndex);

    // The chapter roster the picked mode offers — the Chapter screen and everything
    // downstream (breadcrumb, launch) index into this, never the full list. Free Flight/Dogfight
    // only; Instant Action uses Environments/CurrentMissionTypes
    // instead (its own environment list is decoded, not this table's alphabetic one).
    private (string Name, string Code, bool DangerZones)[] CurrentChapters => ChaptersFor(_mode);

    // The mission types the picked environment's chapter actually offers: all four,
    // minus Stunt Flying where that chapter's own `disallow_missions` bars it (decoded: only "the
    // clouds" among the seven Instant Action environments — DangerZonesFor).
    private (string Label, string Key)[] CurrentMissionTypes => MissionTypeRowsFor(Environments[_environmentIndex].Code);

    // The single-player cursor position on the current screen (the plane screen reads
    // player 1's cursor).
    private int CurrentIndex => _screen switch
    {
        Screen.Mode => _modeIndex,
        Screen.Chapter => _chapterIndex,
        Screen.Presets => _presetCursor,
        Screen.Environment => _environmentIndex,
        Screen.MissionType => _missionTypeIndex,
        Screen.Waves => _waveListIndex,
        Screen.WaveEdit => _waveFieldIndex,
        Screen.Wingmen => _wingmenFieldIndex,
        Screen.WingmanLoadout => _wingmanFitRow,
        Screen.Hangar => _hangar?.Row ?? 0,
        Screen.Campaign => _campaign?.Row ?? 0,
        Screen.Options => _optionsIndex,
        _ => _slots.Count == 1 && _slots[0].InLoadout ? _slots[0].FitRow : _slots[0].PlaneIndex,
    };

    // The font the bands and the fit columns are measured in, or null before the theme has one.
    private Font? MenuFont => _zones.GetThemeDefaultFont();

    // The stock-fit table, loaded on first use. A failed load leaves the rosters empty, which
    // shows as a loadout list of nothing but its reset row rather than a crash on the way to
    // flying: the fit is optional and a launch must survive without it.
    private StockLoadouts Fits => _stockFits ??= StockLoadouts.Load();

    // How many rows the Wingmen screen shows right now: the Aircraft field is hidden at
    // 0 wingmen, matching the decoded setup screen's own behaviour.
    private int WingmenRowCount => _numWingmen > 0 ? 2 : 1;

    // Whether the plane pick offers the hangar DOOR row. Decision 6 puts the Build entry on the
    // Instant Action pick alone; a splitscreen pane never draws it, so no pane's PlaneIndex can
    // point past the roster; the roster itself (stock + customs) is every seat's alike.
    private bool HangarRowOnPlaneScreen => _mode == MenuMode.Stunt && _slots.Count == 1;

    // The plane pick's row count: the roster (stock + customs) plus the hangar door where it is
    // offered. The door sits AFTER the customs, so the C21 clamp reasoning holds with the roster
    // grown: it is always the single trailing row, never locked, never launched.
    private int PlaneRowCount => _roster.Count + (HangarRowOnPlaneScreen ? 1 : 0);

    /// <summary>Builds the (hidden) launchscreen. <paramref name="zrdrPath"/> is the shared zrdr
    /// extraction the plane stats come from; <paramref name="dataRoot"/> is where <c>extracted/</c>
    /// lives, needed to load an Instant Action environment's own <c>ia.zrd.json</c> once one is
    /// confirmed. <paramref name="host"/> must already hold a <see cref="FreeFlightFeature"/> and,
    /// before the first frame, a first seat; <paramref name="player1"/> is the poller behind that
    /// seat. Add it to the tree, then <see cref="ShowMenu"/>.</summary>
    public static LaunchMenu Build(string zrdrPath, string dataRoot, IMenuHost host, MenuInput player1)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(player1);
        var menu = new LaunchMenu
        {
            _zrdrPath = zrdrPath,
            _dataRoot = dataRoot,
            _host = host,
            _free = host.Features.Get<FreeFlightFeature>(),
            _player1 = player1,
            Layer = HudLayers.Board,
            Visible = false,
        };

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        menu.AddChild(root);

        // Fully opaque backdrop so the empty 3D scene (procedural sky) never shows through.
        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f), MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        // The three bands. The middle is the only one that expands, so the header sits on the top
        // edge and the footer on the bottom edge whatever the middle holds.
        menu._zones = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        menu._zones.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        menu._zones.AddThemeConstantOverride("separation", 0);
        root.AddChild(menu._zones);
        menu._header = Band(menu._zones);
        menu._middle = Band(menu._zones);
        menu._middle.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        menu._footer = Band(menu._zones);

        // The splitscreen plane select lives alongside the centred layout; exactly one is visible.
        menu._paneRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        menu._paneRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(menu._paneRoot);

        // The campaign's screens are composed boards rather than row lists, so they draw through
        // their own full-window surface; exactly one of the three layouts is ever visible.
        menu._boardRoot = ComposedBoardView.Build(dataRoot);
        menu._boardRoot.Visible = false;
        root.AddChild(menu._boardRoot);

        // The chip strip is a zero-size box pinned to the top-right corner; GrowHorizontal.Begin
        // spending its minimum width leftward from there (PerfHud's own PlaceTopRight pattern), so
        // the right edge stays on the inset however many chips are joined.
        menu._chipStrip = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        menu._chipStrip.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        menu._chipStrip.GrowHorizontal = Control.GrowDirection.Begin;
        root.AddChild(menu._chipStrip);

        return menu;
    }

    /// <summary>The launch-gate RULE, pure and public so it is reachable from <c>CSVM.Tests</c>
    /// with no menu instance behind it: everyone joined has locked a plane, AND — Dogfight only —
    /// at least two have joined to fight each other. Free Flight and Instant Action launch solo
    /// exactly as before.</summary>
    public static bool CanLaunch(MenuMode mode, bool allLocked, int joinedCount) =>
        allLocked && (mode != MenuMode.Versus || joinedCount >= 2);

    /// <summary>The chapter list a mode actually offers, as codes: Stunt Flying only the maps with
    /// Danger Zones (a stunt run elsewhere would be an empty free flight); every other mode all
    /// eight. Static + public so the rule is testable without a menu instance.</summary>
    public static string[] ChapterCodesFor(MenuMode mode)
    {
        var list = ChaptersFor(mode);
        var codes = new string[list.Length];
        for (int i = 0; i < list.Length; i++)
            codes[i] = list[i].Code;
        return codes;
    }

    /// <summary>The Instant Action Environment screen's roster, as chapter codes, in the decoded
    /// dropdown order — C1, C2B, C3, C5, C1B, C4, C2, C1C never among them. Static + public so
    /// the decoded order is testable without a menu instance.</summary>
    public static string[] EnvironmentCodes()
    {
        var codes = new string[Environments.Length];
        for (int i = 0; i < Environments.Length; i++)
            codes[i] = Environments[i].Code;
        return codes;
    }

    /// <summary>The Instant Action Environment screen's roster as the display names the preset
    /// table names an environment by, in the same decoded dropdown order
    /// <see cref="EnvironmentCodes"/> returns codes in. Static + public so a preset resolves
    /// against the roster rather than against a second copy of it.</summary>
    public static string[] EnvironmentNames()
    {
        var names = new string[Environments.Length];
        for (int i = 0; i < Environments.Length; i++)
            names[i] = Environments[i].Name;
        return names;
    }

    /// <summary>The MissionType screen's roster for one chapter, as `ia.json` `mission_type` keys
    /// in the UI dropdown order, with Stunt Flying dropped where `disallow_missions` bars it — the
    /// same rule <see cref="ChapterCodesFor"/> applies via <see cref="DangerZonesFor"/>. Static
    /// and public so the filter is testable without a menu instance.</summary>
    public static string[] MissionTypeKeysFor(string chapterCode)
    {
        var rows = MissionTypeRowsFor(chapterCode);
        var keys = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
            keys[i] = rows[i].Key;
        return keys;
    }

    /// <summary>The eleven airframe display names in the langui 3700 order, standing alone for
    /// Militias' Fortune Hunter row's "all eleven" coverage — read off <c>Planes</c> rather than
    /// duplicated, so the two rosters cannot drift apart. Callable from anywhere in the class
    /// regardless of where it sits textually: a method body only needs <c>Planes</c> assigned by
    /// the time it RUNS, and Militias' initializer runs after Planes' own. Static + public so the
    /// decoded order is testable without a menu instance.</summary>
    public static string[] PlaneNames()
    {
        var names = new string[Planes.Length];
        for (int i = 0; i < Planes.Length; i++)
            names[i] = Planes[i].Name;
        return names;
    }

    /// <summary>The wave editor's Militia field roster, in the langui dropdown order (3670).
    /// Static + public so the decoded roster is testable without a menu instance.</summary>
    public static string[] MilitiaNames()
    {
        var names = new string[Militias.Length];
        for (int i = 0; i < Militias.Length; i++)
            names[i] = Militias[i].Name;
        return names;
    }

    /// <summary>The wave editor's Aircraft field roster for one militia (by <see cref="MilitiaNames"/>'s
    /// own name, case-sensitive) — the `.BM` pattern coverage table
    /// (docs/formats/instant-action.md), never <c>vehicle.json</c>'s narrower <c>paint_pattern</c>
    /// reading. Throws <see cref="ArgumentException"/> on an unrecognised name, the same
    /// fail-loud policy a typo in wizard-only data deserves. Static + public so the coverage is
    /// testable without a menu instance.</summary>
    public static string[] AircraftFor(string militiaName)
    {
        foreach (var m in Militias)
            if (m.Name == militiaName)
                return m.Aircraft;
        throw new ArgumentException($"'{militiaName}' is not an Instant Action militia", nameof(militiaName));
    }

    /// <summary>The wave editor's Skill field roster, internal keys in the langui dropdown order
    /// (3695) — the same vocabulary <c>InstantActionWave.EnemySkill</c> stores. Static + public so
    /// the roster is testable without a menu instance.</summary>
    public static string[] SkillKeys() => (string[])Skills.Clone();

    /// <summary>Builds one wizard wave slot into the <see cref="InstantActionWave"/>
    /// <see cref="Mech3.InstantAction.BuildFromWizard"/> stores, returning
    /// <see cref="InstantAction.EmptyWave"/> when <paramref name="count"/> is 0 so an unconfigured
    /// slot matches a JSON file's omitted `groupN` byte-identically. `EnemyName` is a plain label,
    /// not the original's undecoded `MSG_*` construction. Static and public so the build rule is
    /// testable without a menu instance.</summary>
    public static InstantActionWave WaveFor(int count, int militiaIndex, int aircraftIndex, int skillIndex)
    {
        if (count <= 0)
            return InstantAction.EmptyWave;
        string militia = Militias[militiaIndex].Name;
        string aircraft = Militias[militiaIndex].Aircraft[aircraftIndex];
        return new InstantActionWave(count, $"{militia} {aircraft}", aircraft, Skills[skillIndex], -1);
    }

    /// <summary>Show the menu (normally from the Mode screen) and prime every input edge so a
    /// button still held from the transition here (the Esc that left a flight, the Start that
    /// joined a player) does not fire immediately. Joined players survive a return from flight;
    /// their plane locks do not. <paramref name="startScreen"/> opens on a later screen, a
    /// screenshot aid: docs/cli.md's <c>--menu=</c> list, with the hangar's own values handled by
    /// <see cref="OpenHangarAid"/> and the campaign's by <see cref="OpenCampaignAid"/>.</summary>
    public void ShowMenu(string startScreen = "")
    {
        _screen = startScreen switch
        {
            "chapter" => Screen.Chapter,
            "presets" => Screen.Presets,
            "environment" => Screen.Environment,
            "missiontype" => Screen.MissionType,
            "waves" => Screen.Waves,
            "wingmen" => Screen.Wingmen,
            "plane" or "loadout" or "selected" => Screen.Plane,
            "wingmanloadout" => Screen.WingmanLoadout,
            "options" => Screen.Options,
            _ => Screen.Mode,
        };
        if (_screen == Screen.Options)
        {
            OpenOptions();
        }

        // Environment/MissionType/Waves/Wingmen only exist under Instant Action — force it so a
        // --menu= opening straight onto one of them (a screenshot aid) renders the right
        // roster/filter rather than whatever _mode was last left at.
        if (_screen is Screen.Presets or Screen.Environment or Screen.MissionType or Screen.Waves
            or Screen.WaveEdit or Screen.Wingmen or Screen.WingmanLoadout)
        {
            _modeIndex = (int)MenuMode.Stunt;
            _mode = MenuMode.Stunt;
        }

        // The wingman list needs a flight to arm and a mission that HAS wingmen, so the aid
        // configures both — the ace duel forces the count to 0, and opening onto a state no
        // player can reach is worse than not having the aid.
        if (_screen == Screen.WingmanLoadout)
        {
            _missionTypeIndex = Math.Max(0, Array.FindIndex(CurrentMissionTypes,
                m => m.Key != "dogfight_ace"));
            if (_numWingmen == 0)
            {
                _numWingmen = 2;
            }
        }
        // Opening straight onto Waves skips the accept that normally parks the cursor, so put it
        // where a player would find it — otherwise the aid screenshots a state nobody sees.
        if (_screen == Screen.Waves)
        {
            _waveListIndex = _waves.Length;
        }
        // Opening straight onto the aircraft screen skips the Chapter Accept that hands Free
        // Flight its pick, so the cursor's chapter is handed over here instead.
        if (_screen == Screen.Plane && _mode == MenuMode.Free)
        {
            _free.SelectChapter(CurrentChapters[_chapterIndex].Code);
        }
        _error = "";
        // A flow never survives a trip through flight: it holds an unsaved scratch plane, and
        // resuming one after a session would be editing something nobody remembers starting.
        _hangar = null;
        // Same rule for the campaign: its flow holds a selected profile and a screen stack, and
        // resuming one after a session would be continuing something nobody remembers starting.
        _campaign = null;
        _aidGuest = 0;
        RefreshRoster();
        OpenHangarAid(startScreen);
        OpenCampaignAid(startScreen);
        Visible = true;
        if (_slots.Count == 0)
            _slots.Add(new Slot(_player1));
        foreach (var slot in _slots)
        {
            // Every stage of the pick, not just the lock: a slot left Confirmed would satisfy the
            // launch gate on the first frame back from flight and fly again without a press.
            slot.Locked = false;
            slot.Confirmed = false;
            slot.InLoadout = false;
            slot.Fit.ResetToStock();
            slot.Input.Prime();
        }
        // A pane's own fit only exists once that slot has selected an airframe, so the aid makes
        // that press for the reader — after the reset loop above, which would undo it.
        if (startScreen is "loadout" or "selected")
        {
            _slots[0].Locked = true;
            _slots[0].InLoadout = startScreen == "loadout";
            _slots[0].FitRow = 0;
        }
        SyncDevices();
        PrimeJoins();
        Rebuild();
    }

    /// <summary>Hide the menu (the host is about to build a session).</summary>
    public void HideMenu() => Visible = false;

    /// <summary>Applies one frame of player 1's semantic commands in place of a device poll, then
    /// redraws if anything changed. The scripted journey suites drive the real screens through
    /// this, and the frame shape is the one a menu input source hands a presentation. Needs
    /// <see cref="ShowMenu"/> to have run, so there is a player 1 to drive.</summary>
    public bool Drive(MenuCommands frame)
    {
        Apply(frame);
        bool dirty;
        try
        {
            dirty = HandleInput();
        }
        finally
        {
            // Edges, so the next real poll starts from an idle frame rather than a press.
            Apply(MenuCommands.None);
        }

        if (dirty && Visible)
            Rebuild();
        return dirty;
    }

    /// <summary>Opens the campaign on the named profile's cabin, the screen a flown mission
    /// returns to. The profile is re-read from the store, so what the mission just recorded (a
    /// completed objective, an advanced position, a paid reward) is what the cabin shows. An
    /// unreadable profile leaves the flow on its roster rather than refusing.</summary>
    public void OpenCampaignCabin(string profileName)
    {
        OpenCampaign();
        if (_campaign is { } flow && flow.Store.Load(profileName) is { } profile)
        {
            flow.SelectProfile(profile);
        }

        Rebuild();
    }

    /// <summary>Opens the campaign on the named profile's scrapbook, at the mission a finished
    /// mission just flew, cabin on its far side after <see cref="Session.Launcher"/>'s deferred
    /// hop. The profile is re-read from the store, the same discipline as
    /// <see cref="OpenCampaignCabin"/>, so the shown record is what the mission just wrote.</summary>
    public void OpenCampaignScrapbook(string profileName, int seq)
    {
        OpenCampaign();
        if (_campaign is { } flow && flow.Store.Load(profileName) is { } profile)
        {
            flow.SelectProfile(profile);
            flow.OpenScrapbook(seq);
        }

        Rebuild();
    }

    /// <summary>Show an error line on the current screen (e.g. a failed build sent us back here).</summary>
    public void ShowError(string message)
    {
        _error = message;
        if (Visible)
            Rebuild();
    }

    /// <summary>Debug/verification aid (--debug-join=N): add N device-less players so the
    /// multi-cursor plane screen can be screenshot on a machine with one controller. They can
    /// never act (no keyboard, no pad), so the shot is deterministic.</summary>
    public void DebugJoin(int extraPlayers)
    {
        for (int i = 0; i < extraPlayers && _slots.Count < SplitScreen.MaxPlayers; i++)
            _slots.Add(new Slot
            {
                PlaneIndex = (i + 1) % Planes.Length,
                // Lock the last one so a screenshot shows both panel states (locked border lit
                // vs still choosing) side by side.
                Locked = i == extraPlayers - 1,
            });
        GD.Print($"launchscreen: --debug-join → {_slots.Count} players (the added ones have no device)");
        // --menu=campaign-guestcheck's own walk, which needs the players this call just added: the
        // aid ran inside ShowMenu, before anybody had joined.
        if (_aidGuest > 0 && _campaign is { } flow)
        {
            flow.SetPlayers(_slots.Count);
            int walked = 0;
            while (walked < _aidGuest && flow.Field.Advance())
            {
                walked++;
            }
        }

        _aidGuest = 0;

        if (Visible)
            Rebuild();
    }

    /// <summary>Debug/verification aid (--debug-waves=N): pre-configure the first N (clamped 0-4)
    /// Instant Action wizard wave slots with a distinct, non-empty load, so the Waves screen's
    /// "N waves configured" states are screenshot-able with nobody at the controls. Forces Instant
    /// Action mode, since waves exist nowhere else.</summary>
    public void DebugWaves(int count)
    {
        _modeIndex = (int)MenuMode.Stunt;
        _mode = MenuMode.Stunt;
        int n = Math.Clamp(count, 0, _waves.Length);
        for (int i = 0; i < n; i++)
        {
            _waves[i] = new WaveSlot
            {
                Count = 4,
                MilitiaIndex = i % Militias.Length,
                AircraftIndex = 0,
                SkillIndex = i % Skills.Length,
            };
        }
        GD.Print($"launchscreen: --debug-waves → {n} wave(s) pre-configured");
        if (Visible)
            Rebuild();
    }

    /// <summary>Debug/verification aid (--debug-preset=N): apply Table of Contents preset N and
    /// open on the wizard's step 1, so the FILLED wizard is screenshot-able with nobody at the
    /// controls. This is the aid for the failure mode units cannot see — a preset that applies the
    /// wrong aircraft or the wrong militia looks entirely plausible on screen. Forces Instant
    /// Action mode, since presets exist nowhere else.</summary>
    public void DebugPreset(int index)
    {
        _modeIndex = (int)MenuMode.Stunt;
        _mode = MenuMode.Stunt;
        int n = Math.Clamp(index, 0, InstantActionPresets.All.Count - 1);
        ApplyPreset(n);
        _presetCursor = n;
        ScrollPresetsToCursor();
        // The base def is normally loaded by Environment's own Accept, which this aid skips.
        _iaBaseDef = LoadEnvironmentDef(Environments[_environmentIndex].Code);
        GD.Print($"launchscreen: --debug-preset → '{InstantActionPresets.All[n].Name}' applied");
        if (Visible)
            Rebuild();
    }

    /// <summary>Debug/verification aid (--debug-wingmen=N): pre-configure the Instant Action
    /// wizard's own wingman count (clamped 0-5) and a non-default aircraft, so the plane screen's
    /// flown-wingmen re-clamp (decision 8a) is screenshot-able alongside --debug-join=. Forces
    /// Instant Action mode, since wingmen exist nowhere else.</summary>
    public void DebugWingmen(int count)
    {
        _modeIndex = (int)MenuMode.Stunt;
        _mode = MenuMode.Stunt;
        _numWingmen = Math.Clamp(count, 0, 5);
        _wingmanPlaneIndex = 1;
        GD.Print($"launchscreen: --debug-wingmen → {_numWingmen} wingman/wingmen pre-configured");
        if (Visible)
            Rebuild();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;

        // Device bookkeeping first: a pad that vanished must not still be driving a cursor, and a
        // pad that appeared should be joinable (or become P1's, if P1 has none).
        bool dirty = SyncDevices();
        dirty |= ScanJoins();

        // Every metric on every one of the three layouts is a function of the window, and a resize
        // or a resolution change arrives as no input at all, so the size itself is watched.
        dirty |= GetViewport().GetVisibleRect().Size != _viewSize;

        // Player 1's commands come through the host's first seat; the other seats are polled here
        // until the shared player setup owns them. Text capture is set before the poll: the
        // PLANENAME screen's letter aliases must be dead for the frame that reads them.
        var seat = _host.Seats[0];
        seat.CapturingText = NamePage() != null;
        Apply(seat.Poll((float)delta));
        for (int i = 1; i < _slots.Count; i++)
            _slots[i].Input.Poll((float)delta);
        dirty |= HandleInput();
        // After the input, so a press that opened or left the briefing is already reflected: the
        // reveal is a clock the page cannot own, and the narration is a node the page cannot hold.
        dirty |= TickCampaignAudio(delta);
        if (_pressFrames > 0)
        {
            _pressFrames--;
            dirty = true;
        }

        dirty |= _typed;
        _typed = false;

        // Live hotplug: redraw when the join strip's text changes even if nothing was pressed.
        if (dirty || JoinStripText() != _stripText)
        {
            if (Visible) // a launch during HandleInput hides us; don't rebuild a dead menu
                Rebuild();
        }
    }

    /// <summary>The screenshot key, and typed characters for the PLANENAME screen. Typing is taken
    /// as an event rather than by the raw polling everything else here uses, because only the event
    /// carries the character the pilot's own keyboard layout produced, and held keys arrive as
    /// echoes, which is where typing gets its auto-repeat.
    /// <para>⚠ The screenshot key is handled here rather than left to the launcher, so the menu
    /// screens own their binding (BL-489); marking it handled keeps one press to one file.</para></summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            Testing.CaptureDirector.SaveScreenshot(GetViewport());
            GetViewport().SetInputAsHandled();
            return;
        }

        if (NamePage() is not { } page || @event is not InputEventKey { Pressed: true } key)
        {
            return;
        }

        bool typed = key.Keycode == Key.Backspace
            ? page.Backspace()
            : key.Unicode > 0 && page.Type((char)key.Unicode);
        if (typed)
        {
            // Redrawn on the next frame rather than here: Rebuild replaces the very controls this
            // event is being dispatched through.
            _typed = true;
            GetViewport().SetInputAsHandled();
        }
    }

    // --- players / devices ---

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    private static (string Name, string Code, bool DangerZones)[] ChaptersFor(MenuMode mode) =>
        mode == MenuMode.Stunt ? Array.FindAll(Chapters, c => c.DangerZones) : Chapters;

    private static (string Label, string Key)[] MissionTypeRowsFor(string chapterCode) =>
        DangerZonesFor(chapterCode) ? MissionTypes : Array.FindAll(MissionTypes, m => m.Key != "stunt_flying");

    // Whether a chapter's `ia.json` ships `dzones`, read off the shared roster so the
    // Environment/MissionType screens and the plain Chapter screen cannot read two different
    // answers for the same chapter.
    private static bool DangerZonesFor(string chapterCode) => MenuChapters.DangerZonesFor(chapterCode);

    // This screen's row text per chapter code, zipped over the shared roster so the codes, their
    // order and the Danger Zones flags have exactly one home.
    private static (string Name, string Code, bool DangerZones)[] BuildChapters()
    {
        var rows = new (string Name, string Code, bool DangerZones)[MenuChapters.All.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            var chapter = MenuChapters.All[i];
            rows[i] = (ChapterRowText(chapter.Code), chapter.Code, chapter.DangerZones);
        }

        return rows;
    }

    private static string ChapterRowText(string code) => code switch
    {
        "C1" => "Sea Haven (night) — IA: an airfield",
        "C1B" => "The ocean — Sea Haven variant",
        "C1C" => "Sea Haven variant C — no IA, campaign/MP only",
        "C2" => "Hollywood — IA: a movie studio",
        "C2B" => "The clouds — Hollywood variant",
        "C3" => "Hawaii (islands)",
        "C4" => "Rocky Mountains — IA: Sky Haven",
        "C5" => "New York — IA: Manhattan",
        _ => code,
    };

    private static int Mph(PlaneStats s) => Mathf.RoundToInt(s.FdSpeed * 2.23694f);

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static Label Label(string text, int fontSize, Color color, HorizontalAlignment align)
    {
        var l = new Label { Text = text, HorizontalAlignment = align };
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        l.AddThemeConstantOverride("shadow_offset_x", 1);
        l.AddThemeConstantOverride("shadow_offset_y", 1);
        return l;
    }

    private static Control Spacer(int height) => new() { CustomMinimumSize = new Vector2(0, height) };

    // One of the three bands: a full-width row whose single content column is centred in it.
    private static HBoxContainer Band(Node parent)
    {
        var band = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(band);
        return band;
    }

    // A band's content column. Nothing in a band expands horizontally, so the column is exactly
    // this wide unless a line of text is wider, which is what the description wraps against.
    // ⚠ The separation scales with the rest; a fixed one is height the band metrics do not budget.
    private static VBoxContainer Column(float width, float s)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0f) };
        column.AddThemeConstantOverride("separation", (int)(ZoneSeparation * s));
        return column;
    }

    // Empties a band before its column is rebuilt. Removed as well as freed: a queued-free child
    // is still a child for the rest of the frame, and would count twice towards the band's height.
    private static void Clear(Node band)
    {
        foreach (var child in band.GetChildren())
        {
            band.RemoveChild(child);
            child.QueueFree();
        }
    }

    // Player 1's frame of semantic commands onto the poller HandleInput reads. Join is not
    // applied: joining is a per-pad scan (ScanJoins), not a seat's command, until the shared
    // player setup owns the seats.
    private void Apply(MenuCommands frame)
    {
        var p1 = _slots[0].Input;
        p1.Move = frame.MoveY;
        p1.MoveX = frame.MoveX;
        p1.Accept = frame.Accept;
        p1.Back = frame.Back;
        p1.Loadout = frame.Loadout;
        p1.Presets = frame.Contents;
    }

    // Whether a pad already belongs to a player: one of players 2–4, or the pad player 1
    // claimed on the Mode/Chapter screens (_p1Pad). Before that claim, player 1's
    // pads are only borrowed — it reads every free device, so any of them can still join.
    private bool IsClaimed(int pad)
    {
        if (pad == _p1Pad)
            return true;
        for (int i = 1; i < _slots.Count; i++)
            if (_slots[i].Input.Pad == pad)
                return true;
        return false;
    }

    // Reconciles the joined players with the live pad roster: drops a player whose pad
    // disconnected, then hands player 1 every unclaimed pad. That last part is the
    // important one — player 1 reading the whole leftover roster rather than `pads[0]` is
    // what keeps the phantom-device fix alive (see MenuInput.Pads), and
    // it falls out for free that a pad joining as its own player leaves player 1's set and
    // rejoins it on un-join. Returns true when anything changed (the strip needs redrawing).
    private bool SyncDevices()
    {
        var connected = Pads.Connected();
        bool dirty = false;
        for (int i = _slots.Count - 1; i >= 1; i--)
        {
            int pad = _slots[i].Input.Pad;
            if (pad >= 0 && !connected.Contains(pad))
            {
                GD.Print($"launchscreen: P{i + 1}'s pad {pad} disconnected — player left");
                _slots.RemoveAt(i);
                dirty = true;
            }
        }
        if (_p1Pad >= 0 && !connected.Contains(_p1Pad))
        {
            GD.Print($"launchscreen: P1's pad {_p1Pad} disconnected — back to keyboard + any free pad");
            _p1Pad = -1;
            dirty = true;
        }
        // Once player 1 has claimed a pad it reads only that one; until then it borrows every
        // device nobody else has, which is what keeps the any-pad phantom-device policy.
        var free = new List<int>(connected.Count);
        if (_p1Pad >= 0)
        {
            free.Add(_p1Pad);
        }
        else
        {
            foreach (int pad in connected)
                if (!IsClaimed(pad))
                    free.Add(pad);
        }
        var p1 = _slots[0].Input;
        // The launchscreen always binds an explicit set here; null (every connected pad) is the
        // in-session reading, which this screen never uses.
        var bound = p1.Pads ?? Array.Empty<int>();
        if (free.Count != bound.Length)
        {
            p1.Pads = free.ToArray();
            p1.Prime(); // a button still held on a pad that just changed hands is not a press
            dirty = true;
        }
        else
        {
            for (int i = 0; i < free.Count; i++)
                if (free[i] != bound[i])
                {
                    p1.Pads = free.ToArray();
                    p1.Prime();
                    dirty = true;
                    break;
                }
        }
        return dirty;
    }

    // Seeds the per-pad join edges from the current state, so a Start held while the
    // menu appears does not immediately join a player.
    private void PrimeJoins()
    {
        _joinPrev.Clear();
        foreach (int pad in Pads.Connected())
            _joinPrev[pad] = MenuInput.JoinPressed(pad);
    }

    // Start on an unclaimed pad joins a new player on the Plane or Campaign screen.
    // The same ordering rule, player 1 claiming a pad first, keeps the gesture unambiguous on both.
    // A campaign join closes at the seated player's FLY MISSION.
    private bool ScanJoins()
    {
        bool dirty = false;
        if (_screen != Screen.Plane && _screen != Screen.Campaign)
            return false;
        // The seated player's FLY MISSION opens the first guest's check instead of leaving, so
        // the field's own lock closes joining.
        if (_campaign is { Field.Locked: true })
            return false;
        foreach (int pad in Pads.Connected())
        {
            bool pressed = MenuInput.JoinPressed(pad);
            _joinPrev.TryGetValue(pad, out bool prev);
            _joinPrev[pad] = pressed;
            if (!pressed || prev || IsClaimed(pad) || _slots.Count >= SplitScreen.MaxPlayers)
                continue;
            var slot = new Slot();
            slot.Input.Pads = new[] { pad };
            slot.Input.Prime();
            _slots.Add(slot);
            GD.Print($"launchscreen: P{_slots.Count} joined on pad {pad} \"{Input.GetJoyName(pad)}\"");
            dirty = true;
        }
        return dirty;
    }

    // Pins player 1 to whichever pad it is actually steering the Mode/Chapter screens
    // with ("logging in" that controller). Called only from those screens, so by the time the
    // aircraft list appears player 1's device is settled and every other pad is unambiguously a
    // joiner. Player 1 driving with the keyboard claims nothing — then all pads stay free, which
    // is exactly the keyboard-versus-controllers setup.
    private bool ClaimP1Pad()
    {
        int pad = _slots[0].Input.LastActivePad;
        if (_p1Pad >= 0 || pad < 0)
            return false;
        _p1Pad = pad;
        GD.Print($"launchscreen: P1 claimed pad {pad} \"{Input.GetJoyName(pad)}\" " +
                 "(other pads join at aircraft select)");
        return true;
    }

    private void Unjoin(int index)
    {
        GD.Print($"launchscreen: P{index + 1} left (pad {_slots[index].Input.Pad})");
        _slots.RemoveAt(index);
    }

    // --- navigation ---

    // Reads this frame's polled intents and applies them. Mode/Chapter are player 1's
    // alone (the others can only leave); the Plane screen runs every player's cursor at once and
    // fires Launch when they are all locked. Returns true if the view changed.
    private bool HandleInput()
    {
        bool dirty = false;
        if (_screen != Screen.Plane)
        {
            var p1 = _slots[0].Input;
            dirty |= ClaimP1Pad();
            if (_screen == Screen.Hangar)
            {
                return HandleHangarInput(p1) || dirty;
            }

            if (_screen == Screen.Campaign)
            {
                return HandleCampaignInput(p1) || dirty;
            }

            if (p1.Move != 0)
            {
                int n = CurrentCount();
                switch (_screen)
                {
                    case Screen.Mode: _modeIndex = Wrap(_modeIndex + p1.Move, n); break;
                    case Screen.Chapter: _chapterIndex = Wrap(_chapterIndex + p1.Move, n); break;
                    case Screen.Presets:
                        _presetCursor = Wrap(_presetCursor + p1.Move, n);
                        ScrollPresetsToCursor();
                        break;
                    case Screen.Environment: _environmentIndex = Wrap(_environmentIndex + p1.Move, n); break;
                    case Screen.MissionType: _missionTypeIndex = Wrap(_missionTypeIndex + p1.Move, n); break;
                    case Screen.Waves: _waveListIndex = Wrap(_waveListIndex + p1.Move, n); break;
                    case Screen.WaveEdit: _waveFieldIndex = Wrap(_waveFieldIndex + p1.Move, n); break;
                    case Screen.Wingmen: _wingmenFieldIndex = Wrap(_wingmenFieldIndex + p1.Move, n); break;
                    case Screen.WingmanLoadout: _wingmanFitRow = Wrap(_wingmanFitRow + p1.Move, n); break;
                    case Screen.Options: _optionsIndex = Wrap(_optionsIndex + p1.Move, n); break;
                }
                dirty = true;
            }
            if (p1.MoveX != 0)
            {
                dirty |= HandleMoveX(p1.MoveX);
            }
            // X on the wizard's step 1 opens the Table of Contents. Step 1 only: the presets are
            // the mode's front door, and a jump back to step 1 from deeper in the wizard would have
            // to explain why picking a scenario threw the pilot three screens backwards.
            if (p1.Presets && _screen == Screen.Environment)
            {
                _screen = Screen.Presets;
                // Re-open on the applied preset, so backing in and out does not lose the place.
                _presetCursor = _presetIndex >= 0 ? _presetIndex : 0;
                ScrollPresetsToCursor();
                return true;
            }
            // Y on the Wingmen step opens the flight's one fit — the same meaning Y carries on a
            // plane pane. Gated on there being wingmen to arm, like the Aircraft row above it.
            if (p1.Loadout && _screen == Screen.Wingmen && _numWingmen > 0)
            {
                _screen = Screen.WingmanLoadout;
                _wingmanFitRow = 0;
                return true;
            }
            if (p1.Accept)
            {
                _error = "";
                HandleAccept();
                dirty = true;
            }
            else if (p1.Back || (p1.Loadout && _screen == Screen.WingmanLoadout))
            {
                if (_screen == Screen.Mode)
                    _host.Exit(new QuitExit());
                else
                    _screen = _screen switch
                    {
                        // Backing out of the contents list leaves the wizard's fields as they were:
                        // a preset is applied on Accept, never on the cursor passing over it.
                        Screen.Presets => Screen.Environment,
                        Screen.Environment => Screen.Mode,
                        Screen.MissionType => Screen.Environment,
                        Screen.Waves => Screen.MissionType,
                        Screen.WaveEdit => Screen.Waves,
                        Screen.Wingmen => Screen.Waves,
                        Screen.WingmanLoadout => Screen.Wingmen,
                        _ => Screen.Mode, // Chapter
                    };
                dirty = true;
            }
            // Everyone else can only drop out from here.
            for (int i = _slots.Count - 1; i >= 1; i--)
            {
                if (!_slots[i].Input.Back)
                    continue;
                Unjoin(i);
                dirty = true;
            }
            return dirty;
        }

        // Plane screen: all joined players pick simultaneously, each with their own cursor.
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            var slot = _slots[i];
            var input = slot.Input;

            // A pane inside its loadout reads nothing else, so one player arming cannot pull
            // anybody else out of browsing and cannot launch while somebody is still in there.
            if (slot.InLoadout)
            {
                dirty |= HandleFitInput(slot);
                continue;
            }

            if (input.Move != 0 && !slot.Locked)
            {
                int was = slot.PlaneIndex;
                slot.PlaneIndex = Wrap(slot.PlaneIndex + input.Move, i == 0 ? PlaneRowCount : _roster.Count);
                if (slot.PlaneIndex != was)
                {
                    // Pylon count and gun slots are per-airframe, so a fit built for one has
                    // nowhere to live on another. Keyed to the airframe changing, never to
                    // unlocking: backing out to re-read the stats line must not cost the fit.
                    slot.Fit.ResetToStock();
                }
                dirty = true;
            }
            // The hangar row is a door, not an aircraft: it never locks, so nothing downstream
            // ever indexes the roster with it.
            if (input.Accept && i == 0 && slot.PlaneIndex >= _roster.Count)
            {
                OpenHangar(Screen.Plane);
                return true;
            }

            if (input.Loadout && slot.Locked && !slot.Confirmed)
            {
                slot.InLoadout = true;
                slot.FitRow = 0;
                dirty = true;
            }
            else if (input.Accept && !slot.Confirmed)
            {
                // Two stages: A selects the airframe, A again confirms it and launches once
                // everybody has. Unconditional — a pilot with no interest in weapons taps twice
                // and flies the stock fit, which is the old behaviour plus one press.
                if (slot.Locked)
                {
                    slot.Confirmed = true;
                }
                else
                {
                    slot.Locked = true;
                }
                _error = "";
                dirty = true;
            }
            else if (input.Back)
            {
                if (slot.Confirmed)
                {
                    slot.Confirmed = false;
                }
                else if (slot.Locked)
                {
                    slot.Locked = false;
                }
                else if (i == 0)
                {
                    // Player 1 backing out returns everyone to whichever screen fed the Plane
                    // screen this time, mirroring the forward skip of Waves/Wingmen for
                    // Instant Action's ace duel.
                    _screen = _mode != MenuMode.Stunt ? Screen.Chapter
                        : CurrentMissionTypes[_missionTypeIndex].Key == "dogfight_ace" ? Screen.MissionType
                        : Screen.Wingmen;
                    foreach (var s in _slots)
                    {
                        s.Locked = false;
                        s.Confirmed = false;
                        s.InLoadout = false;
                    }
                    return true;
                }
                else
                {
                    Unjoin(i);
                }
                dirty = true;
            }
        }

        if (CanLaunch())
        {
            FireLaunch();
            return false; // the host took the exit, hid us and is building
        }
        return dirty;
    }

    // A pane showing its loadout list. Live-editing steppers like every other wizard screen, and
    // B returns to the airframe keeping the fit rather than discarding it. ⚠ That last part is
    // INVENTED: the original pairs ACCEPT LOADOUT with CANCEL LOADOUT, but its Cancel is a button
    // you click, while ours would sit on the pad's navigation key — a discard there would throw
    // away a fit somebody was only stepping back from. Reset to stock is the revert we keep.
    private bool HandleFitInput(Slot slot)
    {
        var input = slot.Input;
        var def = StockFitFor(slot.PlaneIndex);
        var rows = FitRowsFor(def, slot.Fit);
        bool dirty = false;
        if (input.Move != 0)
        {
            slot.FitRow = Wrap(slot.FitRow + input.Move, rows.Count);
            dirty = true;
        }

        slot.FitRow = Math.Clamp(slot.FitRow, 0, rows.Count - 1);
        var row = rows[slot.FitRow];
        if (input.MoveX != 0)
        {
            StepFit(def, slot.Fit, row, input.MoveX);
            dirty = true;
        }

        if (input.Accept && row.Kind == FitRowKind.Reset)
        {
            slot.Fit.ResetToStock();
            dirty = true;
        }

        if (input.Back || input.Loadout)
        {
            slot.InLoadout = false;
            dirty = true;
        }
        return dirty;
    }

    // The wingman list's rows — the one fit the whole flight carries, so it hangs off the
    // Wingmen step rather than any pane.
    private List<FitRow> WingmanFitRows() =>
        FitRowsFor(StockFitFor(_wingmanPlaneIndex), _wingmanFit);

    // The fit list the centred body is showing, or null when it is showing something else. A
    // lone pilot's plane screen keeps the centred layout, so its list draws through the same
    // path the wingman one does; a pane's list is drawn by RebuildPanes instead.
    private List<FitRow>? CentredFitRows() =>
        _screen == Screen.WingmanLoadout ? WingmanFitRows()
        : _screen == Screen.Plane && _slots.Count == 1 && _slots[0].InLoadout
            ? FitRowsFor(StockFitFor(_slots[0].PlaneIndex), _slots[0].Fit)
            : null;

    // The list's two column widths, measured rather than guessed: the label column takes the
    // widest mount name present, the value column the widest entry EITHER roster can produce, so
    // a row keeps its width whatever it is stepped to. Falls back to em estimates with no theme
    // font, which is the same guard the band metrics use.
    private Vector2 FitColumns(List<FitRow> rows, int fontSize)
    {
        var font = MenuFont;
        if (font == null)
        {
            return new Vector2(fontSize * 7f, fontSize * 8f);
        }

        float label = 0f;
        foreach (var row in rows)
        {
            label = Mathf.Max(label, font.GetStringSize(row.Label, HorizontalAlignment.Left, -1, fontSize).X);
        }

        float value = 0f;
        foreach (var option in Fits.Options.GunAmmo)
        {
            value = Mathf.Max(value, font.GetStringSize(option.Label, HorizontalAlignment.Left, -1, fontSize).X);
        }

        foreach (var option in Fits.Options.PylonOrdnance)
        {
            value = Mathf.Max(value, font.GetStringSize(option.Label, HorizontalAlignment.Left, -1, fontSize).X);
        }

        return new Vector2(label + (fontSize * 1.2f), value);
    }

    // One loadout row as a control: mount in the left column, what is fitted there in the right.
    private Control FitRowControl(List<FitRow> rows, int index, int fontSize, Color color, bool selected)
    {
        var columns = FitColumns(rows, fontSize);
        var row = rows[index];
        return CursorRow.BuildColumns(row.Label, row.Value, columns.X, columns.Y,
            fontSize, color, selected);
    }

    // The horizontal axis's effect, screen by screen — always a live-editing stepper on
    // whichever field the vertical cursor is focused on, never a "select and lock" gesture (that
    // is what Accept is for). Split out of HandleInput because it now has one branch
    // per wizard screen that carries a stepper: the mission choice's lives, and the wave
    // editor's four fields plus the wingman count/aircraft.
    private bool HandleMoveX(int dir)
    {
        switch (_screen)
        {
            case Screen.Options:
                // The presentation row is a two-way stepper; the apply row has nothing to step.
                if (_optionsIndex == 0)
                {
                    TogglePresentationChoice();
                    return true;
                }

                return false;
            case Screen.MissionType:
                // The lives stepper rides the same screen as the mission choice (decision 18),
                // so it never competes with the vertical list cursor above.
                _lives = Math.Clamp(_lives + dir, 0, MaxLives);
                return true;
            case Screen.WaveEdit:
                {
                    ref var slot = ref _waves[_waveEditIndex];
                    switch (_waveFieldIndex)
                    {
                        case 0: slot.Count = Math.Clamp(slot.Count + dir, 0, 6); break;
                        case 1:
                            slot.MilitiaIndex = Wrap(slot.MilitiaIndex + dir, Militias.Length);
                            // The decoded setup screen's own AV[BA].QG = 0: a militia's aircraft list
                            // is somebody else's roster once the militia changes.
                            slot.AircraftIndex = 0;
                            break;
                        case 2:
                            slot.AircraftIndex = Wrap(slot.AircraftIndex + dir, Militias[slot.MilitiaIndex].Aircraft.Length);
                            break;
                        case 3: slot.SkillIndex = Wrap(slot.SkillIndex + dir, Skills.Length); break;
                    }
                    return true;
                }
            case Screen.Wingmen:
                if (_wingmenFieldIndex == 0)
                {
                    _numWingmen = Math.Clamp(_numWingmen + dir, 0, 5);
                    // The Aircraft row disappears at 0 wingmen — keep the cursor on a row that
                    // still exists rather than pointing at a field nothing draws.
                    _wingmenFieldIndex = Math.Min(_wingmenFieldIndex, WingmenRowCount - 1);
                }
                else
                {
                    int was = _wingmanPlaneIndex;
                    _wingmanPlaneIndex = Wrap(_wingmanPlaneIndex + dir, Planes.Length);
                    if (_wingmanPlaneIndex != was)
                    {
                        _wingmanFit.ResetToStock();
                    }
                }
                return true;
            case Screen.WingmanLoadout:
                {
                    var rows = WingmanFitRows();
                    _wingmanFitRow = Math.Clamp(_wingmanFitRow, 0, rows.Count - 1);
                    StepFit(StockFitFor(_wingmanPlaneIndex), _wingmanFit, rows[_wingmanFitRow], dir);
                }
                return true;
            default:
                return false;
        }
    }

    // The Accept gesture's effect, screen by screen — split out of
    // HandleInput for the same reason HandleMoveX was: one branch per
    // wizard screen now, most of them advancing the wizard rather than picking a row.
    private void HandleAccept()
    {
        switch (_screen)
        {
            case Screen.WingmanLoadout:
                {
                    // Reset is the only row Accept does anything on: the rest are live steppers,
                    // and B is what leaves, so Accept has nothing else to mean here.
                    var rows = WingmanFitRows();
                    _wingmanFitRow = Math.Clamp(_wingmanFitRow, 0, rows.Count - 1);
                    if (rows[_wingmanFitRow].Kind == FitRowKind.Reset)
                    {
                        _wingmanFit.ResetToStock();
                    }
                }
                break;
            case Screen.Options:
                if (_optionsIndex == 0)
                {
                    TogglePresentationChoice();
                }
                else
                {
                    // The launcher persists the request and restarts the menu; the screen stays
                    // standing for the host to hide.
                    _host.Exit(new PresentationSwitchExit(new PresentationId(_presentationChoice)));
                }

                break;
            case Screen.Mode:
                // The three trailing rows are the campaign's, the hangar's and Options' top-level
                // doors, past the three modes.
                if (_modeIndex == Modes.Length)
                {
                    OpenCampaign();
                    break;
                }

                if (_modeIndex == Modes.Length + 2)
                {
                    _screen = Screen.Options;
                    OpenOptions();
                    break;
                }

                if (_modeIndex > Modes.Length)
                {
                    OpenHangar(Screen.Mode);
                    break;
                }

                _mode = (MenuMode)_modeIndex; // the row order IS the enum order
                if (_mode == MenuMode.Stunt) // "Instant Action" — the wizard's step 1
                {
                    _screen = Screen.Environment;
                }
                else
                {
                    _screen = Screen.Chapter;
                    // The roster may have shrunk (Stunt hid the dzone-less maps last time
                    // around) — keep the cursor on a row that exists.
                    _chapterIndex = Wrap(_chapterIndex, CurrentChapters.Length);
                }
                break;
            case Screen.Presets:
                ApplyPreset(_presetCursor);
                // The original's own page order: the contents list is page 1 and View Story opens
                // page 2, the configuration screen under the preset's name. Our wizard is that
                // second page unrolled into five screens, so Accept lands on the first of them.
                _screen = Screen.Environment;
                break;
            case Screen.Environment:
                _screen = Screen.MissionType;
                // The mission-type roster depends on the environment just picked (Stunt Flying
                // hidden on "the clouds") — keep the cursor on a row that exists.
                _missionTypeIndex = Wrap(_missionTypeIndex, CurrentMissionTypes.Length);
                // The environment's own ia.zrd.json is the wizard's ace/zeppelin/disallow_missions
                // base from here on (FireLaunch) — loaded once here rather than at every Rebuild.
                _iaBaseDef = LoadEnvironmentDef(Environments[_environmentIndex].Code);
                break;
            case Screen.MissionType:
                if (CurrentMissionTypes[_missionTypeIndex].Key == "dogfight_ace")
                {
                    // Dogfighting an Ace takes no wave or wingman configuration — the decoded
                    // setup screen's own behaviour (mission type 0 hides every enemy control).
                    _screen = Screen.Plane;
                    PrimeJoins();
                }
                else
                {
                    _screen = Screen.Waves;
                    // Opens on the trailing Continue row, not wave 1: every slot starts empty
                    // (decision 1), so configuring none is the common path and the harmless row is
                    // the one under the cursor. Coming BACK from a wave keeps that wave's row.
                    _waveListIndex = _waves.Length;
                }
                break;
            case Screen.Chapter:
                if (_mode == MenuMode.Free)
                {
                    _free.SelectChapter(CurrentChapters[_chapterIndex].Code);
                }

                _screen = Screen.Plane;
                PrimeJoins(); // joining opens here — a Start held on the way in must not fire
                break;
            case Screen.Waves:
                if (_waveListIndex < _waves.Length)
                {
                    _waveEditIndex = _waveListIndex;
                    _waveFieldIndex = 0;
                    _screen = Screen.WaveEdit;
                }
                else // the trailing "Continue" row
                {
                    _screen = Screen.Wingmen;
                    _wingmenFieldIndex = 0;
                }
                break;
            case Screen.WaveEdit:
                _screen = Screen.Waves; // live-edited already; Accept just means "done"
                break;
            case Screen.Wingmen:
                _screen = Screen.Plane;
                PrimeJoins();
                break;
        }
    }

    // --- the hangar ---

    // The PLANENAME page, when it is the one showing. Every typed-character path asks through here,
    // so nothing reaches a plane's name from another screen.
    private HangarNamePage? NamePage() =>
        _screen == Screen.Hangar && _hangar?.Page is HangarNamePage page ? page : null;

    // Opens the Build Custom Plane flow, remembering the screen to land back on. Both doors
    // (the Mode screen's trailing row and the Instant Action plane pick) come through here, so
    // there is one entry, one exit and one place the scratch plane lives.
    private void OpenHangar(Screen returnTo)
    {
        _hangarReturn = returnTo;
        _hangar = new HangarFlow(CustomPlaneStore.UserPlanes(), HangarStrings(), _dataRoot, Fits, _zrdrPath,
            nameRng: Rng.NewSystemRandom(Rng.PlaneName));
        _screen = Screen.Hangar;
        _error = "";
    }

    // The hangar's screens sit behind a flow rather than behind the screen enum, so --menu= opens
    // one and walks it: "hangar" the plane list, "airframe" the airframe list, "defaults" its
    // defaults ask, "paint" the preview on a Fury in Fortune Hunters colours. An aid only.
    private void OpenHangarAid(string startScreen)
    {
        if (startScreen is not ("hangar" or "airframe" or "defaults" or "paint" or "name"))
        {
            return;
        }

        OpenHangar(Screen.Mode);
        if (_hangar is not { } flow || startScreen == "hangar")
        {
            return;
        }

        flow.Accept(); // New Plane, on to the airframe screen
        if (startScreen == "airframe")
        {
            return;
        }

        flow.Accept(); // pick the focused airframe, which is what raises the defaults ask (E49)
        if (startScreen == "defaults")
        {
            return;
        }

        flow.AnswerDefaultsAsk(false);
        var stopAt = startScreen == "name" ? HangarScreen.Name : HangarScreen.Paint;
        for (int guard = 0; flow.Screen != stopAt && guard < HangarFlow.Order.Length; guard++)
        {
            flow.Accept();
        }

        if (startScreen == "name")
        {
            return;
        }

        flow.Scratch.Airframe = 7;
        flow.Scratch.LoadPatternDefaults(4);
        flow.Scratch.NoseDecal = 40;
        flow.FocusRow(HangarPaintPage.NoseDecalRow); // so the shot shows the decal tile too
    }

    // One frame of player 1's input on a hangar screen. Nobody else steers it: the flow edits one
    // scratch plane, and a second cursor in it would have nothing of its own to move.
    private bool HandleHangarInput(MenuInput p1)
    {
        if (_hangar is not { } flow)
        {
            _screen = _hangarReturn;
            return true;
        }

        bool dirty = false;
        if (p1.Move != 0)
        {
            dirty |= flow.Move(p1.Move);
        }

        if (p1.MoveX != 0)
        {
            dirty |= flow.Step(p1.MoveX);
        }

        if (p1.Accept)
        {
            dirty |= flow.Accept();
        }
        else if (p1.Back)
        {
            dirty |= flow.Back();
        }

        // The gate's refusal (overweight, no engine, no name) rides the screen's own error line.
        _error = flow.Message;
        if (flow.Exit != HangarExit.None)
        {
            CloseHangar(flow);
            return true;
        }

        return dirty;
    }

    // Leaves the flow, built or cancelled, for the screen it was opened from. A cancelled flow
    // wrote nothing, so there is nothing to undo. Every close re-reads the store, so a build
    // shows up in the pickers without a menu restart; a build then puts player 1's cursor on the
    // new plane, the original's index-11 contract (select the first custom slot after a build),
    // done by name because our customs sort rather than filling slots.
    private void CloseHangar(HangarFlow flow)
    {
        _screen = _hangarReturn;
        _hangar = null;
        _error = "";
        RefreshRoster();
        // Built or cancelled alike: the campaign flow behind the door stood still while the hangar
        // ran, and resuming it re-reads the profile the wallet was spent from.
        if (_hangarReturn == Screen.Campaign)
        {
            _campaign?.Resume();
        }

        if (flow.Exit == HangarExit.Built && flow.BuiltPlaneName is { } name)
        {
            LastBuiltPlane = name;
            int at = PlanePickerRoster.IndexOf(_roster, name);
            if (at >= 0)
            {
                // Selected in the picker the hangar was entered from: from the plane pick this
                // is the visible cursor; from the Mode door it is where the pick opens later.
                _slots[0].PlaneIndex = at;
            }

            GD.Print($"launchscreen: hangar built \"{name}\", selected in the plane picker");
        }
    }

    // --- the campaign ---

    // Opens the campaign's out-of-mission flow on its first screen, the profile roster. There is
    // one door and one exit; every screen inside it is a page of the flow's own.
    private void OpenCampaign()
    {
        _campaign = NewCampaignFlow(CampaignProfileStore.UserProfiles());
        _screen = Screen.Campaign;
        _error = "";
        PrimeJoins(); // joining opens here too, so a held Start must not fire on entry
    }

    // A campaign flow over one store, with the two stores its later screens resolve fits through:
    // the hangar's build store for a player-built aircraft, the stock table for everything else
    // (the two profile-seeded starters and the reward aircraft, neither of which is hangar-built).
    // Without them the flight check and ammo screens read every plane as fit-less.
    private CampaignFlow NewCampaignFlow(CampaignProfileStore store) =>
        new(store, HangarStrings(), _dataRoot, CustomPlaneStore.UserPlanes(), Fits);

    // The campaign's screens sit behind a flow rather than behind the screen enum, so --menu=
    // reaches them the way it reaches the hangar's. Every scripted value runs over a scratch
    // profile directory instead of user://Profiles, so the shot is the same on every machine and no
    // aid can write into a real campaign. The briefing takes a seconds argument
    // ("campaign-briefing:20") because its screen is a two-minute reveal and every stage of it is
    // a different picture.
    private void OpenCampaignAid(string startScreen)
    {
        int colon = startScreen.IndexOf(':');
        string value = colon < 0 ? startScreen : startScreen[..colon];
        if (value is not ("campaign" or "campaign-empty" or "campaign-roster" or "campaign-entry"
            or "campaign-cabin" or "campaign-previous" or "campaign-scrapbook" or "campaign-briefing"
            or "campaign-flightcheck" or "campaign-guestcheck" or "campaign-ammo"
            or "campaign-planeselection" or "campaign-hangar" or "campaign-fly"))
        {
            return;
        }

        if (value == "campaign")
        {
            OpenCampaign();
            return;
        }

        // The one aid that runs over the REAL profile store, because it is the one that launches a
        // mission: the session's own director reads user://Profiles, so a scratch profile would
        // not exist by the time the world builds.
        if (value == "campaign-fly")
        {
            OpenCampaign();
            FlyFirstRealProfile();
            return;
        }

        // Two of the aids need a roster to pick from and five need a profile part-way through the
        // campaign; the seeded store carries both, since a second profile changes no later screen.
        bool seeded = value != "campaign-empty" && value != "campaign-entry";
        _campaign = NewCampaignFlow(AidProfileStore(seeded, progressed: value != "campaign-roster"));
        _screen = Screen.Campaign;
        _error = "";
        PrimeJoins(); // this entry point needs the same held-Start guard as OpenCampaign
        if (_campaign is not { } flow)
        {
            return;
        }

        if (value == "campaign-entry")
        {
            flow.Accept();          // arm the name field
            flow.Type("Zachary");   // a name mid-entry, caret and all
            return;
        }

        float argument = 0f;
        if (colon >= 0)
        {
            float.TryParse(startScreen[(colon + 1)..], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out argument);
        }

        WalkCampaignAid(flow, value, argument);

        // On every screen but the briefing the argument is a cursor step count instead, so a shot
        // can show focus on a plaque other than the opening one. Each step is one pad press.
        // campaign-guestcheck reads it as a player number instead, which WalkCampaignAid took.
        for (int i = 0; value is not ("campaign-briefing" or "campaign-guestcheck") && i < (int)argument; i++)
        {
            flow.Move(1);
        }
    }

    // Walks the first stored profile to its next mission's flight check and presses FLY MISSION,
    // the end-to-end aid: it exercises the launch the cabin performs, not a screen. A store with
    // no profile leaves the flow on its roster, which is what a player with none would see.
    private void FlyFirstRealProfile()
    {
        if (_campaign is not { } flow || flow.Roster.Count == 0
            || flow.Store.Load(flow.Roster[0]) is not { } profile)
        {
            return;
        }

        flow.SelectProfile(profile);
        flow.SetMission(CampaignProgression.NextMissionSeq(profile));
        flow.GoTo(CampaignScreen.FlightCheck);
        flow.FocusRow(flow.Page.RowCount - 1); // FLY MISSION is the screen's last row
        flow.Accept();
    }

    // Walks a seeded flow to the screen an aid names. Every step is a call a player's own presses
    // would make, so no aid can reach a state the campaign itself cannot.
    private void WalkCampaignAid(CampaignFlow flow, string value, float seconds)
    {
        if (flow.Store.Load("Zachary") is not { } profile)
        {
            return;
        }

        flow.SelectProfile(profile);
        switch (value)
        {
            case "campaign-previous":
                flow.GoTo(CampaignScreen.PreviousMissions);
                return;
            case "campaign-scrapbook":
                // The book as a finished mission leaves it: opened on the last mission this
                // profile flew, which is the one door the mission end itself takes.
                flow.GoTo(CampaignScreen.PreviousMissions);
                flow.OpenScrapbook(Math.Max(0, CampaignProgression.NextMissionSeq(profile) - 1));
                return;
            case "campaign-briefing":
                flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                flow.GoTo(CampaignScreen.Briefing);
                AdvanceBriefing(flow, seconds);
                return;
            case "campaign-flightcheck":
                flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                flow.GoTo(CampaignScreen.FlightCheck);
                return;
            case "campaign-guestcheck":
                // The guest-check aid reuses the same screen. The argument names which guest
                // one (default P2), and --debug-join= is what puts them on the field.
                flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                flow.GoTo(CampaignScreen.FlightCheck);
                _aidGuest = Math.Max(1, (int)seconds);
                return;
            case "campaign-ammo":
                flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                flow.SetAmmoSlot(0);
                flow.GoTo(CampaignScreen.Ammo);
                return;
            case "campaign-planeselection":
                // The pilot's CHANGE PLANE press, which is what names the slot the cursor opens on.
                flow.SetMission(CampaignProgression.NextMissionSeq(profile));
                flow.SetPlaneSlot(0);
                flow.GoTo(CampaignScreen.PlaneSelection);
                return;
            case "campaign-hangar":
                // The cabin's own PLANE CONSTRUCTION press, so the shot is the hangar standing
                // over this profile's wallet rather than the wallet-free door.
                flow.FocusRow(CampaignCabinPage.PlaneConstructionRow);
                flow.Accept();
                return;
            default:
                return; // campaign-cabin: SelectProfile already landed there
        }
    }

    // Runs the reveal forward to a stage worth photographing. ⚠ Stepped in frame-sized slices, not
    // one jump: the script blocks on authored waits and measured cue points, so a single huge
    // delta would leave it standing at the first of them.
    private void AdvanceBriefing(CampaignFlow flow, float seconds)
    {
        if (flow.Page is not CampaignBriefingPage page || seconds <= 0f)
        {
            return;
        }

        const float slice = 1f / 60f;
        for (float t = 0f; t < seconds; t += slice)
        {
            page.Advance(slice);
        }
    }

    // The scratch store the campaign screenshot aids read: emptied on every open, and seeded with
    // two profiles for the filled-roster shot. A progressed store additionally flies the first
    // three missions, which is what puts rows on the previous-missions list and moves the cabin's
    // Next Mission off the campaign's first entry.
    private CampaignProfileStore AidProfileStore(bool seeded, bool progressed = false)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CSVM", "menu-aid-profiles");
        try
        {
            if (System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }
        catch (System.IO.IOException)
        {
            // A leftover the aid cannot clear is not worth failing a screenshot over.
        }

        var store = new CampaignProfileStore(dir);
        if (!seeded)
        {
            return store;
        }

        var first = CampaignProfileDef.NewProfile("Zachary");
        if (progressed)
        {
            for (int seq = 0; seq < AidMissionsFlown; seq++)
            {
                CampaignProgression.Record(first, new MissionAttempt(
                    seq, AidCompletedMask, 300_000 + (seq * 20_000), 400, 120,
                    first.Planes[0].Airframe, first.Planes[0].Name));
            }
        }

        store.Save(first);
        store.Save(CampaignProfileDef.NewProfile("Nathan"));
        return store;
    }

    // One frame of player 1's input on a campaign screen. While a page's text field is armed the
    // keyboard's letters are text, so the cursor axes come from the pad alone.
    private bool HandleCampaignInput(MenuInput p1)
    {
        if (_campaign is not { } flow)
        {
            _screen = Screen.Mode;
            return true;
        }

        flow.SetPlayers(_slots.Count);
        var driver = CampaignDriver(flow, p1);
        bool typing = flow.CapturesText;
        int move = typing ? driver.PadMove : driver.Move;
        int step = typing ? driver.PadMoveX : driver.MoveX;
        bool dirty = false;
        if (move != 0)
        {
            dirty |= flow.Move(move);
        }

        if (step != 0)
        {
            dirty |= flow.Step(step);
        }

        if (typing)
        {
            dirty |= flow.Type(driver.Typed);
            dirty |= driver.Erase && flow.Backspace();
        }

        // X is the screen's own shortcut, not a press of the focused plaque, so it lights no button
        // and is read before the confirm. While a name field is armed the keyboard's own X alias is
        // a letter being typed, which must not fire it.
        if (!typing && driver.Presets)
        {
            dirty |= flow.Secondary();
        }

        if (driver.Accept)
        {
            _pressFrames = PressFrames;
            dirty = true;
            flow.Accept();
        }
        else if (driver.Back)
        {
            dirty |= flow.Back();
        }

        // Every guest can only drop out from here; the driving player's Back above is the
        // flow's own navigation. The field closes guest exits once the sequence runs;
        // settled, and every Back on screen is the walk back through the checks.
        for (int i = _slots.Count - 1; i >= 1 && !flow.Field.Locked; i--)
        {
            if (!_slots[i].Input.Back)
                continue;
            Unjoin(i);
            dirty = true;
        }

        // A refusal (an empty name, a name the original's own rule rejects, a full roster) rides
        // the screen's error line, the same place the hangar's gate reports.
        _error = flow.Message;
        switch (flow.Exit)
        {
            case CampaignExit.None:
                return dirty;
            case CampaignExit.OpenHangar:
                OpenCampaignHangar(flow);
                return true;
            case CampaignExit.FlyMission:
                FlyCampaignMission(flow);
                return true;
            default:
                StopNarration();
                _screen = Screen.Mode;
                _campaign = null;
                _error = "";
                return true;
        }
    }

    // Whose presses steer the campaign board. Player 1's everywhere, except a guest's own flight
    // check, which is that guest's screen to fill in. A driver with no device — --debug-join's
    // deviceless players — hands back to player 1, or a screenshot aid could never walk the
    // sequence at all.
    private MenuInput CampaignDriver(CampaignFlow flow, MenuInput p1)
    {
        int at = flow.Field.Current;
        if (at <= 0 || at >= _slots.Count)
        {
            return p1;
        }

        var input = _slots[at].Input;
        return input.Keyboard || input.Pads is { Length: > 0 } ? input : p1;
    }

    // PLANE CONSTRUCTION: the hangar over the profile's own wallet (B13's HangarCampaignContext),
    // with the campaign flow left standing behind it. CloseHangar resumes the flow, which re-reads
    // the profile, so a purchase or a sale shows on the cabin the moment the hangar closes.
    private void OpenCampaignHangar(CampaignFlow flow)
    {
        if (flow.Profile is not { } profile)
        {
            flow.Resume();
            return;
        }

        var planes = CustomPlaneStore.UserPlanes();
        _hangarReturn = Screen.Campaign;
        _hangar = new HangarFlow(planes, HangarStrings(), _dataRoot, Fits, _zrdrPath,
            new HangarCampaignContext(flow.Store, profile, planes), Rng.NewSystemRandom(Rng.PlaneName));
        _screen = Screen.Hangar;
        _error = "";
    }

    // FLY MISSION: the profile is saved and the host is handed a campaign mission exit for this
    // profile and story position. Every joined human's aircraft goes with it as a seat choice:
    // its stock node, the pads it joined on, the ammunition and ordnance the ammo screen (or a
    // guest's own flight check) stored, and its hangar build where it has one. The wingman's own
    // binding is resolved by CampaignDirector, which has the profile open anyway.
    private void FlyCampaignMission(CampaignFlow flow)
    {
        if (flow.Profile is not { } profile)
        {
            flow.Resume();
            return;
        }

        flow.Store.Save(profile);
        int players = _slots.Count;
        var seats = new List<MenuSeatChoice>(players);
        string seatedName = "";
        for (int player = 0; player < players; player++)
        {
            var plane = flow.Field.Plane(player) ?? new OwnedPlane();
            // A reward aircraft with no file in the build store falls back to its own award
            // template: the grant writes one, and this is what carries a profile granted before it
            // did. Per entry, since a guest may pick a granted aircraft from the seated profile too.
            var custom = CustomPlaneStore.UserPlanes().Load(plane.Name)
                         ?? CampaignProgression.BuildForOwned(plane);
            // The pads are the device this seat joined on: the cabin's join flow is the only place
            // that binding exists, and the consumer cannot re-derive it from the connected roster.
            seats.Add(new MenuSeatChoice(
                PlanePickerRoster.AirframeNode(plane.Airframe),
                _slots[player].Input.Pads ?? Array.Empty<int>(),
                CampaignLoadout.For(plane, Fits),
                custom));
            if (player == 0)
            {
                seatedName = plane.Name;
            }
        }

        StopNarration();
        _campaign = null;
        _screen = Screen.Mode;
        _error = "";
        GD.Print($"launchscreen: campaign '{profile.Name}' flying mission seq {flow.MissionSeq} in \"{seatedName}\"");
        for (int player = 1; player < players; player++)
        {
            GD.Print($"launchscreen: campaign P{player + 1} flying \"{flow.Field.Plane(player)?.Name}\"");
        }

        _host.Exit(new CampaignMissionExit(profile.Name, flow.MissionSeq, seats));
    }

    // The briefing's clock and its narration, the two things its page cannot own: a page holds no
    // Godot node and has no frame to advance on. Returns whether the board needs redrawing, which
    // is whenever the reveal uncovered a line or changed the map it is drawing.
    private bool TickCampaignAudio(double delta)
    {
        if (_screen != Screen.Campaign || _campaign?.Page is not CampaignBriefingPage page)
        {
            StopNarration();
            return false;
        }

        page.Advance(delta);
        if (page.NarrationStarts != _narrationStarts)
        {
            _narrationStarts = page.NarrationStarts;
            // The service ducks the music from here until EndNarration, the whole stay: the gaps
            // between lines are short, and a duck that lifted in them would pump the splash track.
            _host.Audio.BeginNarration(page.NarrationWav);
        }

        // ⚠ Do not turn this back into a did-it-change test over page properties. A reveal fades,
        // spins and moves its elements continuously, so such a list is only the cases someone
        // remembered; while it runs, the board repaints on the frame clock instead.
        bool running = page.Reveal is { Complete: false };
        bool repaint = running || _briefingRunning;
        _briefingRunning = running;
        return repaint;
    }

    // Ends the narration, which lifts the music duck too: every door out of the briefing comes
    // through here, including the launch, which hides the board and so stops TickCampaignAudio
    // from running again.
    private void StopNarration()
    {
        _narrationStarts = 0;
        _briefingRunning = false;
        _host.Audio.EndNarration();
    }

    // Re-reads the saved-plane store into the picker roster and keeps every cursor inside the
    // possibly-shrunk list (a deleted file, a rename). The door row, when offered, sits at
    // _roster.Count, which PlaneRowCount - 1 still admits.
    private void RefreshRoster()
    {
        _roster = PlanePickerRoster.Build(Planes, CustomPlaneStore.UserPlanes().List());
        foreach (var slot in _slots)
            slot.PlaneIndex = Math.Min(slot.PlaneIndex, PlaneRowCount - 1);
    }

    // The stock display name behind a roster row: the row's own name for a stock pick, the
    // airframe's stock aircraft for a custom, for consumers that speak ia.json's vocabulary.
    private string NominalPlaneName(int rosterIndex)
    {
        var pick = _roster[rosterIndex];
        if (!pick.IsCustom)
        {
            return pick.Name;
        }

        foreach (var (name, node) in Planes)
            if (node == pick.Node)
                return name;
        return pick.Name;
    }

    // The langui table, read once per session on first hangar entry. A missing extraction is a
    // warning, not a refusal: every hangar label carries its own fallback text.
    private UiStrings HangarStrings()
    {
        if (_uiStrings == null)
        {
            _uiStrings = UiStrings.TryLoad(_dataRoot);
            if (_uiStrings == null)
            {
                GD.PushWarning("launchscreen: no extracted/rof/—— hangar labels fall back");
                _uiStrings = UiStrings.Empty;
            }
        }

        return _uiStrings;
    }

    /// <summary>Writes one Table of Contents preset over the wizard's own fields. Deliberately
    /// partial: the lives stepper is INVENTED and has no preset value, other players' plane cursors
    /// are ours and not the original's, and `_iaBaseDef` is left to Environment's own Accept — so
    /// what flies is exactly what these fields say, reachable by hand as well as by preset.</summary>
    private void ApplyPreset(int index)
    {
        var applied = InstantActionPresets.Resolve(index, _waves.Length);
        _presetIndex = index;
        _environmentIndex = applied.EnvironmentIndex;
        _missionTypeIndex = applied.MissionTypeIndex;
        _numWingmen = applied.NumWingmen;
        // Only where the preset flies wingmen: at 0 the decode reports no aircraft and the Wingmen
        // screen hides the row, so moving that cursor would be inventing a value.
        if (applied.WingmanPlaneIndex is { } wingman)
        {
            _wingmanPlaneIndex = wingman;
            _wingmanFit.ResetToStock();
        }

        // Player 1 only. The original has one pilot and one aircraft dropdown; players 2-4 are our
        // own divergence and the preset has nothing to say about them. It is a cursor position, not
        // a lock — presets are picked back at step 1, so no slot has selected anything yet.
        _slots[0].PlaneIndex = applied.PlayerPlaneIndex;
        _slots[0].Fit.ResetToStock();

        for (int i = 0; i < _waves.Length; i++)
        {
            var wave = applied.Waves[i];
            _waves[i] = new WaveSlot
            {
                Count = wave.Count,
                MilitiaIndex = wave.MilitiaIndex,
                AircraftIndex = wave.AircraftIndex,
                SkillIndex = wave.SkillIndex,
            };
        }
    }

    // Keeps the 14-row window over the cursor, and never scrolls past the end of the list. The
    // cursor wraps (Wrap, like every other screen), so top-to-bottom jumps both ways are normal
    // here rather than an edge case.
    private void ScrollPresetsToCursor()
    {
        int last = Math.Max(0, InstantActionPresets.All.Count - PresetWindow);
        int top = Math.Clamp(_presetTop, 0, last);
        if (_presetCursor < top)
            top = _presetCursor;
        else if (_presetCursor >= top + PresetWindow)
            top = _presetCursor - PresetWindow + 1;
        _presetTop = Math.Clamp(top, 0, last);
    }

    // The chosen Instant Action environment's own shipped ia.zrd.json, for the fields the
    // wizard has no control to edit (the ace, the zeppelin node names, disallow_missions) — should
    // not fail for any of the seven environments Instant Action offers; falls back to the built-in
    // defaults exactly like a bad --ia= file does rather than leaving the menu unable to proceed.
    private InstantActionDef LoadEnvironmentDef(string chapterCode)
    {
        try
        {
            return InstantAction.Load(SessionPaths.MissionZrdr(_dataRoot, chapterCode, "IA1"));
        }
        catch (Exception e)
        {
            GD.PushWarning($"launchscreen: '{chapterCode}/IA1' ia.zrd.json failed to load " +
                            $"({e.Message}) — the wizard's ace/zeppelin fields use the built-in defaults");
            return InstantAction.Defaults();
        }
    }

    // The launch gate reads CONFIRMED, the second stage, not the lock. That is what leaves a
    // window between selecting an airframe and flying it for the loadout to be opened in;
    // locking used to launch on the same frame the last slot locked.
    private bool AllConfirmed() => _slots.Count > 0 && ConfirmedCount() == _slots.Count;

    private int ConfirmedCount()
    {
        int confirmed = 0;
        foreach (var slot in _slots)
            if (slot.Confirmed)
                confirmed++;
        return confirmed;
    }

    // Whether the Plane screen's launch gesture is live right now. A lone Dogfight pilot
    // stays on this screen with JoinHint naming what it is waiting for. Free Flight's gate is
    // its feature's; the other modes keep the static rule until their own features exist.
    private bool CanLaunch() => _mode == MenuMode.Free
        ? _free.CanLaunch(_slots.Count, ConfirmedCount())
        : CanLaunch(_mode, AllConfirmed(), _slots.Count);

    // Every non-campaign launch leaves as one LaunchExit through the host. Free Flight's is the
    // feature's own; Instant Action and Dogfight build theirs here until their features exist.
    // Our state is left as-is either way, so a failed build can send us back with ShowMenu.
    private void FireLaunch()
    {
        var seats = SeatChoices();
        if (_mode == MenuMode.Free)
        {
            _host.Exit(_free.BuildExit(seats));
            return;
        }

        string chapter;
        InstantActionDef? iaDef = null;
        if (_mode == MenuMode.Stunt) // Instant Action
        {
            chapter = Environments[_environmentIndex].Code;
            var waves = new List<InstantActionWave>(_waves.Length);
            foreach (var w in _waves)
                waves.Add(WaveFor(w.Count, w.MilitiaIndex, w.AircraftIndex, w.SkillIndex));
            iaDef = InstantAction.BuildFromWizard(
                _iaBaseDef ?? InstantAction.Defaults(),
                CurrentMissionTypes[_missionTypeIndex].Key,
                // Player 1's own pick — a nominal label only; the roster, not this value,
                // decides what any human actually flies. A custom pick names its airframe's
                // stock aircraft: the def's consumers speak ia.json's stock vocabulary.
                NominalPlaneName(_slots[0].PlaneIndex),
                _numWingmen,
                Planes[_wingmanPlaneIndex].Name,
                waves,
                _lives,
                _wingmanFit.IsStock ? null : _wingmanFit);
        }
        else
        {
            chapter = CurrentChapters[_chapterIndex].Code;
        }

        _host.Exit(new LaunchExit(chapter, seats, _mode, iaDef));
    }

    // Every joined seat's pick as the typed seat choice: the roster row's node, the pads the seat
    // joined on, its fit edits (null for stock) and, for a custom row, the store's def. ⚠ A custom
    // pick launches as its airframe's stock node; the def rides along for the session build.
    private List<MenuSeatChoice> SeatChoices()
    {
        var seats = new List<MenuSeatChoice>(_slots.Count);
        CustomPlaneStore? store = null;
        foreach (var slot in _slots)
        {
            var pick = _roster[slot.PlaneIndex];
            CustomPlaneDef? custom = null;
            if (pick.CustomName is { } name)
            {
                store ??= CustomPlaneStore.UserPlanes();
                custom = store.Load(name);
                if (custom == null)
                {
                    GD.PushWarning($"custom plane '{name}' could not be loaded, flying the stock {pick.Node}");
                }
            }

            seats.Add(new MenuSeatChoice(pick.Node, slot.Input.Pads ?? Array.Empty<int>(),
                slot.Fit.IsStock ? null : slot.Fit, custom));
        }

        return seats;
    }

    // --- rendering ---

    private void Rebuild()
    {
        _viewSize = GetViewport().GetVisibleRect().Size;

        // A campaign screen is a composed board at authored pixel positions, not a row list, so it
        // takes the whole window and neither of the other two layouts draws behind it.
        bool board = _screen == Screen.Campaign && _campaign != null;

        // Several players choosing aircraft get a real split screen — one panel each, laid out by
        // SplitScreen.PaneRect, so you pick in the pane you will then fly in. Everything else (and
        // every single-player screen) keeps the centred layout untouched.
        bool split = !board && _screen == Screen.Plane && _slots.Count > 1;
        _zones.Visible = !split && !board;
        _paneRoot.Visible = split;
        _boardRoot.Visible = board;
        _chipStrip.Visible = board && _slots.Count > 1;
        if (board)
        {
            RebuildBoard();
            return;
        }

        if (split)
        {
            // The hangar DOOR exists only in the lone-pilot layout, so a pilot joining while
            // player 1 sits on it must not leave a cursor past the roster's end. Only the door
            // is clamped away: a custom row stays a valid pick in every pane (Decision 6).
            foreach (var slot in _slots)
                slot.PlaneIndex = Math.Min(slot.PlaneIndex, _roster.Count - 1);
            RebuildPanes();
            return;
        }

        // The header and the footer keep the heights their own contents need and nothing else, so
        // the rows in between are the only thing a screen can move.
        var zones = Zones();
        _header.CustomMinimumSize = new Vector2(0f, zones.Header);
        _footer.CustomMinimumSize = new Vector2(0f, zones.Footer);
        Clear(_header);
        Clear(_middle);
        Clear(_footer);
        _header.AddChild(HeaderColumn(zones.Scale));
        _middle.AddChild(MiddleColumn(zones.Scale));
        _footer.AddChild(FooterColumn(zones.Scale));
    }

    // The header band: the title, where in the menu the pilot is, and who is holding what. The same
    // three lines on every centred screen, which is what makes the band's height a constant.
    private Control HeaderColumn(float s)
    {
        var column = Column(ContentWidth * s, s);
        column.AddChild(Spacer((int)(ZonePad * s)));
        column.AddChild(Label("CRIMSON SKIES", (int)(TitleFont * s), TitleColor, HorizontalAlignment.Center));
        column.AddChild(Label(Breadcrumb(), (int)(CrumbFont * s), CrumbColor, HorizontalAlignment.Center));
        column.AddChild(JoinStrip(s));
        return column;
    }

    // The middle band: what this screen is, the hangar's running total, the rows themselves with
    // the art column beside them, and the two status slots the aircraft screen writes into.
    private Control MiddleColumn(float s)
    {
        // The hangar's art sits in a column of its own to the LEFT of the rows (E47b, the layout
        // the original's paint screen uses), so the band is that much wider when it shows.
        var hangarArt = PageArt();
        var column = Column((ContentWidth + (hangarArt != null ? HangarArtWidth : 0)) * s, s);
        column.AddChild(Spacer((int)(ZonePad * s)));
        column.AddChild(Label(Heading(), (int)(HeadingFont * s), HeadingColor, HorizontalAlignment.Center));

        // The persistent price/weight line comes from HangarFlow.TotalsLine, error-coloured when
        // over and empty where the focused row has no plane to price. Its slot is reserved on every
        // screen, so gaining or losing a total never moves the rows under it.
        var totals = _screen == Screen.Hangar ? _hangar : null;
        column.AddChild(Reserved(totals?.TotalsLine ?? "", (int)(DetailFont * s),
            totals is { TotalsOverweight: true } ? ErrorColor : DetailColor));

        // The rows in a column of their own so the hangar's art can stand beside them.
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)(ZoneSeparation * s));
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        // Every screen but the contents list draws its whole roster; that one is a 14-row window
        // onto 19, so it draws a slice and Row keeps taking the ABSOLUTE index (which is what the
        // cursor comparison and the row text both read).
        int count = CurrentCount();
        int first = _screen == Screen.Presets ? _presetTop : 0;
        int last = _screen == Screen.Presets ? Math.Min(count, _presetTop + PresetWindow) : count;
        for (int i = first; i < last; i++)
            content.AddChild(Row(i, s));

        // C22's art seam: a hangar page may hand the shell one decoded TGA with a caption
        // (blueprint, icon, paint preview), plus a second one for the focused row (E48's decal
        // tile). This block and HangarArtColumn are the whole rendering.
        if (hangarArt != null)
        {
            var beside = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
            beside.AddThemeConstantOverride("separation", (int)(14 * s));
            beside.AddChild(HangarArtColumn(hangarArt, s));
            beside.AddChild(content);
            column.AddChild(beside);
        }
        else
        {
            column.AddChild(content);
        }

        // Both status slots are reserved rather than added when they have something to say: a
        // wingman count or a lock that appears mid-screen would otherwise shift the band.
        column.AddChild(Reserved(_screen == Screen.Plane ? WingmenLine() : "",
            (int)(DetailFont * s), DetailColor));
        column.AddChild(Reserved(SelectedLine(), (int)(DetailFont * s), RowLockedColor));
        return column;
    }

    // The footer band: what the focused row is (or why the last press did nothing) and the presses
    // that do something here. Its height is fixed, so the controls line stays on the same pixel.
    private Control FooterColumn(float s)
    {
        var column = Column(ContentWidth * s, s);
        column.AddChild(DetailBlock(s));
        column.AddChild(Label(Footer(), (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
        column.AddChild(Spacer((int)(ZonePad * s)));
        return column;
    }

    // The lock line the aircraft screen writes into its second status slot, or "". A lone pilot's
    // lock is invisible in the centred layout otherwise, and an unacknowledged press on a screen
    // that used to launch on it reads as a freeze rather than as a stage.
    private string SelectedLine() =>
        _screen == Screen.Plane && _slots.Count == 1 && _slots[0].Locked
            ? $"✓  {_roster[_slots[0].PlaneIndex].Name} selected"
            : "";

    // Opens the Options screen on the saved request, so the stepper shows back what the player
    // asked for even when availability made Built-in the active presentation.
    private void OpenOptions()
    {
        _optionsIndex = 0;
        _presentationChoice = OptionsStore.UserOptions().Load().MenuPresentation ?? PresentationId.BuiltIn.Value;
    }

    private void TogglePresentationChoice() =>
        _presentationChoice = _presentationChoice == PresentationId.Original.Value
            ? PresentationId.BuiltIn.Value
            : PresentationId.Original.Value;

    private string PresentationChoiceLabel() =>
        _presentationChoice == PresentationId.Original.Value ? "Original" : "Built-in";

    // What this screen is, the middle band's first line.
    private string Heading()
    {
        return _screen switch
        {
            Screen.Mode => "SELECT MODE",
            Screen.Chapter => "SELECT MAP",
            // The window shows 14 of 19, so the position has to be on screen somewhere or the
            // list looks like it ends where the window does.
            Screen.Presets => $"TABLE OF CONTENTS  ({_presetCursor + 1}/{CurrentCount()})",
            Screen.Environment => "SELECT ENVIRONMENT",
            Screen.MissionType => "SELECT MISSION",
            Screen.Waves => "CONFIGURE WAVES",
            Screen.WaveEdit => $"WAVE {_waveEditIndex + 1}",
            Screen.Wingmen => "WINGMEN",
            Screen.WingmanLoadout => $"WINGMEN — AMMO SELECTION  ({Planes[_wingmanPlaneIndex].Name})",
            Screen.Hangar => _hangar?.Page.Title ?? HangarRow,
            Screen.Campaign => _campaign?.Page.Title ?? CampaignRow,
            Screen.Options => "OPTIONS",
            _ when _slots.Count == 1 && _slots[0].InLoadout =>
                $"AMMO SELECTION  ({_roster[_slots[0].PlaneIndex].Name})",
            _ when _slots.Count == 1 && _slots[0].Locked => "AIRCRAFT SELECTED",
            _ => _slots.Count > 1 ? "SELECT AIRCRAFT — ALL PLAYERS" : "SELECT AIRCRAFT",
        };
    }

    // One campaign screen as its composed board. The error line rides the detail slot, which is
    // where a refusal has to appear on a screen with no error row of its own.
    private void RebuildBoard()
    {
        if (_campaign is not { } flow)
        {
            return;
        }

        // A board draws no join strip, so the strip text is recorded here rather than left at
        // whatever the last centred screen wrote. Otherwise _Process's own strip comparison never
        // settles on a campaign screen, and a board repaints for a reason that is not its own.
        _stripText = JoinStripText();
        RebuildChipStrip();
        var page = flow.Page;
        int row = flow.Row;
        string detail = _error.Length > 0 ? _error : page.Detail(row);
        // A block of text is the screen's own to place; the one-line hint band takes only what a
        // screen has nowhere else to put, which is every short description and every refusal.
        bool banded = CampaignBoards.DetailSlot(page.Screen) == null && !detail.Contains('\n');
        _boardRoot.Show(
            CampaignBoards.For(page, row, _pressFrames > 0, detail, flow.Modal),
            BoardPalette.For(page.Screen),
            banded ? detail : string.Empty,
            page.Footer);
    }

    // The campaign chip strip content is built only while the board is up; visibility
    // itself is Rebuild's, off the same slot count, so a Back that drops the last guest hides the
    // strip on the same frame instead of leaving one stale chip behind.
    private void RebuildChipStrip()
    {
        foreach (var c in _chipStrip.GetChildren())
            c.QueueFree();
        if (_slots.Count <= 1)
        {
            return;
        }

        var size = GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        float inset = fit.Length(ChipInset);
        _chipStrip.OffsetRight = -inset;
        _chipStrip.OffsetLeft = _chipStrip.OffsetRight;
        _chipStrip.OffsetTop = inset;
        _chipStrip.OffsetBottom = _chipStrip.OffsetTop;
        _chipStrip.AddThemeConstantOverride("separation", Mathf.RoundToInt(fit.Length(10f)));
        for (int i = 0; i < _slots.Count; i++)
        {
            _chipStrip.AddChild(Label(SplitScreen.PlayerTag(i), Mathf.RoundToInt(fit.Length(ChipFont)),
                SplitScreen.PlayerColor(i), HorizontalAlignment.Center));
        }
    }

    // The splitscreen aircraft select: one panel per player in that player's pane of the
    // screen (the same SplitScreen.PaneRect geometry the flight panes use), plus a
    // shared bottom strip carrying the breadcrumb, the join strip and the controls line. Each
    // panel shows the player's tag + device, the full aircraft roster with their own cursor, the
    // focused plane's stats, and their lock state — the panel border lights up in the player's
    // colour once locked, which is the at-a-glance "who are we waiting for".
    private void RebuildPanes()
    {
        foreach (var c in _paneRoot.GetChildren())
            c.QueueFree();

        var size = GetViewport().GetVisibleRect().Size;
        float s = Mathf.Max(1f, size.Y / 720f);
        // The strip's own Instant Action line (decision 8a's flown-wingmen re-clamp) is one more
        // row than StripHeightFrac was tuned for — grow the fixed band by a line's worth so it
        // does not push the controls line off the bottom of a centred, unclipped VBoxContainer.
        bool wingmenLine = WingmenLine().Length > 0;
        float stripH = size.Y * StripHeightFrac + (wingmenLine ? (FooterFont + 6) * s : 0f);
        var paneArea = new Vector2(size.X, Mathf.Max(1f, size.Y - stripH));

        for (int i = 0; i < _slots.Count; i++)
        {
            var rect = SplitScreen.PaneRect(i, _slots.Count, paneArea);
            var panel = new PanelContainer
            {
                Position = rect.Position,
                Size = rect.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var color = SplitScreen.PlayerColor(i);
            bool locked = _slots[i].Locked;
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = locked ? new Color(color, 0.10f) : new Color(0.06f, 0.07f, 0.10f, 0.92f),
                BorderColor = locked ? color : new Color(color, 0.45f),
                BorderWidthLeft = (int)(2 * s),
                BorderWidthRight = (int)(2 * s),
                BorderWidthTop = (int)(2 * s),
                BorderWidthBottom = (int)(2 * s),
                ContentMarginLeft = PanePad * s,
                ContentMarginRight = PanePad * s,
                ContentMarginTop = PanePad * s,
                ContentMarginBottom = PanePad * s,
            });
            panel.AddChild(PaneBody(i, rect.Size - Vector2.One * (2f * PanePad * s), s));
            _paneRoot.AddChild(panel);
        }

        // The shared strip: what everyone already chose, who is in, and the controls.
        var strip = new VBoxContainer
        {
            Position = new Vector2(0f, size.Y - stripH),
            Size = new Vector2(size.X, stripH),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        strip.AddChild(Label(Breadcrumb(), (int)(CrumbFont * s), CrumbColor, HorizontalAlignment.Center));
        strip.AddChild(JoinStrip(s));
        // RebuildPanes only ever draws the Plane screen (Rebuild's own split check), so the
        // flown-wingmen re-clamp (decision 8a) applies unconditionally here, same as the centred
        // layout's own Plane-screen-only check.
        if (wingmenLine)
            strip.AddChild(Label(WingmenLine(), (int)(FooterFont * s), DetailColor, HorizontalAlignment.Center));
        strip.AddChild(Label(
            "↑↓  Choose       Enter / A  Select, again to fly       L / Y  Weapons       Esc / B  Back",
            (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
        if (_error.Length > 0)
            strip.AddChild(Label(_error, (int)(ErrorFont * s), ErrorColor, HorizontalAlignment.Center));
        _paneRoot.AddChild(strip);
    }

    // One player's panel contents. The roster is the full list — it fits, because the
    // font scale is derived from the pane's own height rather than the window's (a 4P quarter
    // pane and a 2P half pane are the same height, so both land on the same size).
    private Control PaneBody(int player, Vector2 inner, float s)
    {
        var slot = _slots[player];
        var color = SplitScreen.PlayerColor(player);
        var font = MenuFont;

        // Fit the roster + header + stats + status into the pane's height.
        float paneScale = s;
        if (font != null)
        {
            float refH = font.GetHeight(CrumbFont)                       // the player/device header
                       + _roster.Count * font.GetHeight(RowFont)         // the roster
                       + font.GetHeight(DetailFont)                      // stats
                       + font.GetHeight(FooterFont)                      // lock status
                       + 4 * 4;                                          // separations
            paneScale = Mathf.Min(s, inner.Y / refH);
        }

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", (int)(2 * paneScale));

        box.AddChild(Label($"{SplitScreen.PlayerTag(player)}   {slot.Input.DeviceLabel}",
            (int)(CrumbFont * paneScale), color, HorizontalAlignment.Center));

        // This pane swaps to its own loadout list in place, so the players beside it keep
        // browsing untouched — and nobody can launch while somebody is still in here.
        if (slot.InLoadout)
        {
            var fitRows = FitRowsFor(StockFitFor(slot.PlaneIndex), slot.Fit);
            for (int i = 0; i < fitRows.Count; i++)
            {
                bool selected = i == slot.FitRow;
                box.AddChild(FitRowControl(fitRows, i, (int)(RowFont * paneScale),
                    selected ? color : RowColor, selected));
            }

            box.AddChild(Label(_roster[slot.PlaneIndex].Name, (int)(DetailFont * paneScale),
                DetailColor, HorizontalAlignment.Center));
            box.AddChild(Label("←→ change    B done", (int)(FooterFont * paneScale),
                FooterColor, HorizontalAlignment.Center));
            return box;
        }

        for (int i = 0; i < _roster.Count; i++)
        {
            bool sel = i == slot.PlaneIndex;
            box.AddChild(CursorRow.Build(_roster[i].Name, (int)(RowFont * paneScale),
                sel ? color : RowColor, sel));
        }

        box.AddChild(Label(PlaneStat(_roster[slot.PlaneIndex].Node), (int)(DetailFont * paneScale),
            DetailColor, HorizontalAlignment.Center));
        box.AddChild(Label(
            slot.Confirmed ? "✓  READY" : slot.Locked ? "A again to fly    Y weapons" : "choosing…",
            (int)(FooterFont * paneScale),
            slot.Locked ? color : FooterColor, HorizontalAlignment.Center));
        return box;
    }

    // The three bands for the window being drawn into, measured at the 720p metrics and scaled
    // together. Only the middle term depends on the screen: every slot in the other two is drawn
    // whether or not it has anything to say, so the bands keep their height as the pilot moves.
    private MenuZones Zones()
    {
        // CanvasLayer is a Node (not a CanvasItem), so read the size off the Viewport directly.
        float viewH = GetViewport().GetVisibleRect().Size.Y;
        var font = MenuFont;
        if (font == null)
        {
            return MenuZones.For(viewH, 0f, 0f, 0f);
        }

        float rowsH = DrawnRowCount() * (font.GetHeight(RowFont) + ZoneSeparation);
        // The hangar art column stands beside the rows, so it only adds height where it is taller
        // than the rows it sits next to, not on top of them.
        float artH = PageArt() != null
            ? Mathf.Max(0f, HangarArtHeight + HangarRowArtHeight + (2 * font.GetHeight(FooterFont)) +
                12 - rowsH)
            : 0f;
        float header = ZonePad + font.GetHeight(TitleFont) + font.GetHeight(CrumbFont) +
            font.GetHeight(FooterFont) + (3 * ZoneSeparation);
        float middle = ZonePad + font.GetHeight(HeadingFont) + (3 * font.GetHeight(DetailFont)) +
            rowsH + artH + (5 * ZoneSeparation);
        float footer = (DetailReserveLines * font.GetHeight(DetailFont)) +
            font.GetHeight(FooterFont) + ZonePad + (2 * ZoneSeparation);
        return MenuZones.For(viewH, header, middle, footer);
    }

    // How many rows the middle band actually draws. Only the contents list differs from the item
    // count: it is a 14-row window onto 19, and budgeting for all 19 shrinks it for nothing.
    private int DrawnRowCount() =>
        _screen == Screen.Presets ? Math.Min(CurrentCount(), PresetWindow) : CurrentCount();

    // One line at a font size, the height a reserved slot keeps whatever it holds.
    private float LineHeight(int fontSize) => MenuFont?.GetHeight(fontSize) ?? 0f;

    // A label whose slot is drawn whether or not it has anything to say, so what appears in it
    // never moves the rows above or the controls below.
    private Control Reserved(string text, int fontSize, Color color)
    {
        var label = Label(text, fontSize, color, HorizontalAlignment.Center);
        label.CustomMinimumSize = new Vector2(0f, LineHeight(fontSize));
        return label;
    }

    // The stock fit behind a roster row, or null when the table has no def flying that model.
    // A custom row resolves through its airframe's stock node, so its Ammo Selection list is the
    // airframe's until custom loadouts are supported. Wingman indices land here too: wingmen
    // are stock-only, and the roster's first eleven rows ARE the stock table in its order.
    private LoadoutDef? StockFitFor(int planeIndex) => Fits.ForModel(_roster[planeIndex].Node);

    // One airframe's loadout list: a row per firable gun slot, a row per pylon, then reset.
    // Turret slots are left out while they are built inert — an ammo pick there would change
    // nothing that can be fired. Pylons list in fill order under their PHYSICAL number, so the
    // screen agrees with the weapon gauge's belt lights rather than renumbering them 1..N.
    private List<FitRow> FitRowsFor(LoadoutDef? def, LoadoutChoice fit)
    {
        var rows = new List<FitRow>();
        if (def == null)
        {
            rows.Add(new FitRow(FitRowKind.Reset, 0, "Reset to stock", ""));
            return rows;
        }

        foreach (var gun in def.Guns)
        {
            if (gun.Turret)
            {
                continue;
            }
            string id = fit.GunAmmoFor(gun.Slot) ?? gun.Ammo;
            rows.Add(new FitRow(FitRowKind.Gun, gun.Slot, $".{gun.Caliber}-cal.", LabelFor(Fits.Options.GunAmmo, id)));
        }

        var hp = def.Hardpoints;
        for (int i = 0; hp != null && i < hp.Count && i < Loadout.PylonFillOrder.Length; i++)
        {
            int pylon = Loadout.PylonFillOrder[i];
            string id = fit.PylonFor(pylon) ?? (i < hp.Stock.Length ? hp.Stock[i] : LoadoutChoice.None);
            rows.Add(new FitRow(FitRowKind.Pylon, pylon, $"Pylon {pylon}", LabelFor(Fits.Options.PylonOrdnance, id)));
        }

        rows.Add(new FitRow(FitRowKind.Reset, 0, "Reset to stock", ""));
        return rows;
    }

    // An id's dropdown label, falling back to the id so an unoffered stock value (a def naming
    // something the screen does not list) is visible rather than blank.
    private string LabelFor(IReadOnlyList<LoadoutOption> options, string id)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return option.Label;
            }
        }
        return id;
    }

    // The stepper on one fit row: walk the row's own roster from wherever it sits now. A value
    // the roster does not carry starts the walk at the first entry rather than refusing to move.
    private void StepFit(LoadoutDef? def, LoadoutChoice fit, FitRow row, int dir)
    {
        var options = row.Kind == FitRowKind.Gun ? Fits.Options.GunAmmo : Fits.Options.PylonOrdnance;
        if (def == null || options.Count == 0 || row.Kind == FitRowKind.Reset)
        {
            return;
        }

        string current = row.Kind == FitRowKind.Gun
            ? fit.GunAmmoFor(row.Key) ?? StockGunAmmo(def, row.Key)
            : fit.PylonFor(row.Key) ?? StockPylon(def, row.Key);
        int at = 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Id, current, StringComparison.OrdinalIgnoreCase))
            {
                at = i;
                break;
            }
        }

        string picked = options[Wrap(at + dir, options.Count)].Id;
        if (row.Kind == FitRowKind.Gun)
        {
            fit.SetGunAmmo(row.Key, picked);
        }
        else
        {
            fit.SetPylon(row.Key, picked);
        }
    }

    private string StockGunAmmo(LoadoutDef def, int slot)
    {
        foreach (var gun in def.Guns)
        {
            if (gun.Slot == slot)
            {
                return gun.Ammo;
            }
        }
        return "slug";
    }

    private string StockPylon(LoadoutDef def, int pylon)
    {
        var hp = def.Hardpoints;
        for (int i = 0; hp != null && i < hp.Count && i < Loadout.PylonFillOrder.Length; i++)
        {
            if (Loadout.PylonFillOrder[i] == pylon)
            {
                return i < hp.Stock.Length ? hp.Stock[i] : LoadoutChoice.None;
            }
        }
        return LoadoutChoice.None;
    }

    private int CurrentCount() => _screen switch
    {
        Screen.Mode => Modes.Length + 3, // + the trailing campaign, hangar and options rows
        Screen.Hangar => _hangar?.Page.RowCount ?? 1,
        Screen.Campaign => _campaign?.Page.RowCount ?? 1,
        Screen.Options => 2, // the presentation stepper and the apply row
        Screen.Chapter => CurrentChapters.Length,
        Screen.Presets => InstantActionPresets.All.Count,
        Screen.Environment => Environments.Length,
        Screen.MissionType => CurrentMissionTypes.Length,
        Screen.Waves => _waves.Length + 1, // + the trailing "Continue" row
        Screen.WaveEdit => 4, // Enemies / Militia / Aircraft / Skill
        Screen.Wingmen => WingmenRowCount,
        _ => CentredFitRows()?.Count ?? PlaneRowCount,
    };

    // One centred list row with a ▶ cursor — the item-4 layout, used by every screen
    // the centred body draws. A multi-player aircraft screen never comes through here: it splits
    // into per-player panes instead (RebuildPanes).
    private Control Row(int index, float s)
    {
        if (CentredFitRows() is { } fitRows)
        {
            bool focused = index == CurrentIndex;
            return FitRowControl(fitRows, index, (int)(RowFont * s),
                focused ? RowFocusColor : RowColor, focused);
        }

        string text = RowText(index);
        bool sel = index == CurrentIndex;
        // A locked single-player pick recolours its row, because the centred layout has no
        // per-pane status line to carry the state the way the splitscreen panes do.
        bool locked = _screen == Screen.Plane && _slots.Count == 1 && _slots[0].Locked;
        var colour = sel ? locked ? RowLockedColor : RowFocusColor : RowColor;
        return CursorRow.Build(text, (int)(RowFont * s), colour, sel);
    }

    // The text on one row of the screen showing; a fit list's row reads as its mount label.
    private string RowText(int index)
    {
        if (CentredFitRows() is { } fitRows)
        {
            return fitRows[index].Label;
        }

        return _screen switch
        {
            Screen.Mode => index < Modes.Length ? Modes[index].Label
                : index == Modes.Length ? CampaignRow
                : index == Modes.Length + 1 ? HangarRow : OptionsRow,
            Screen.Hangar => _hangar?.Page.RowText(index) ?? "",
            Screen.Campaign => _campaign?.Page.RowText(index) ?? "",
            Screen.Options => index == 0
                ? $"Menu presentation: {PresentationChoiceLabel()}"
                : "Apply and restart the menu",
            Screen.Chapter => CurrentChapters[index].Name,
            Screen.Presets => InstantActionPresets.All[index].Name,
            Screen.Environment => Environments[index].Name,
            Screen.MissionType => CurrentMissionTypes[index].Label,
            Screen.Waves => WaveListRowText(index),
            Screen.WaveEdit => WaveFieldRowText(index),
            Screen.Wingmen => WingmenFieldRowText(index),
            _ => index < _roster.Count ? _roster[index].Name : HangarRow,
        };
    }

    // One Waves-screen row: an unconfigured slot reads "empty" (decision 1's own "starts
    // empty" wizard, not the original's always-four dropdowns), a configured one summarises its
    // count/militia/aircraft/skill, and the trailing row advances to Wingmen.
    private string WaveListRowText(int index)
    {
        if (index == _waves.Length)
            return "Continue → Wingmen";
        var w = _waves[index];
        string summary = w.Count == 0
            ? "empty"
            : $"{w.Count}x {Militias[w.MilitiaIndex].Name} {Militias[w.MilitiaIndex].Aircraft[w.AircraftIndex]} ({Cap(Skills[w.SkillIndex])})";
        return $"Wave {index + 1} — {summary}";
    }

    // One WaveEdit-screen field row: the label plus the field's own current value, since
    // this screen has no separate detail area — HandleMoveX edits whichever one the
    // cursor sits on.
    private string WaveFieldRowText(int index)
    {
        var w = _waves[_waveEditIndex];
        return index switch
        {
            0 => $"Enemies         {w.Count}",
            1 => $"Militia         {Militias[w.MilitiaIndex].Name}",
            2 => $"Aircraft        {Militias[w.MilitiaIndex].Aircraft[w.AircraftIndex]}",
            _ => $"Skill           {Cap(Skills[w.SkillIndex])}",
        };
    }

    // One Wingmen-screen field row — the Aircraft row (index 1) only ever draws while
    // it exists (WingmenRowCount is 1 at 0 wingmen), matching the decoded setup
    // screen's own hidden-at-zero control.
    private string WingmenFieldRowText(int index) => index switch
    {
        0 => $"Wingmen         {_numWingmen}",
        _ => $"Aircraft        {Planes[_wingmanPlaneIndex].Name}",
    };

    // The detail area: one stats line for the focused entry, autowrapped. The hangar's
    // airframe-defaults ask (string 206) is a two-sentence question, and an unwrapped label makes
    // the centred body as wide as the sentence, which runs off a 16:9 screen (E45). A Label reports
    // a minimum width of 1 once autowrap is on, so it takes the column's width rather than setting
    // it, and every other screen's one-liner is unaffected.
    private Control DetailBlock(float s)
    {
        int size = (int)(DetailFont * s);
        // A refusal takes the description's own slot, the way a campaign board's hint band takes
        // it: it is the one line on the screen that says why the last press did nothing.
        bool refused = _error.Length > 0;
        var label = Label(refused ? _error : Detail(CurrentIndex), size,
            refused ? ErrorColor : DetailColor, HorizontalAlignment.Center);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // Top-aligned in a slot of its own: a one-line description starts on the same pixel as a
        // two-line one, and a longer one grows up into the band rather than moving the controls.
        label.VerticalAlignment = VerticalAlignment.Top;
        label.CustomMinimumSize = new Vector2(0f, DetailReserveLines * LineHeight(size));
        return label;
    }

    // The picture the current page asks the shell to draw beside its rows, or null. Hangar and
    // campaign pages share the one art column, so a page needs no change here to use it.
    private HangarArt? PageArt() => _screen switch
    {
        Screen.Hangar => _hangar?.Page.Art,
        Screen.Campaign => _campaign?.Page.Art,
        _ => null,
    };

    // The focused row's own smaller picture, same seam.
    private HangarArt? PageRowArt() => _screen switch
    {
        Screen.Hangar => _hangar is { } hangar ? hangar.Page.RowArt(hangar.Row) : null,
        Screen.Campaign => _campaign is { } campaign ? campaign.Page.RowArt(campaign.Row) : null,
        _ => null,
    };

    // The art column a hangar page may request: the page's picture over the focused
    // row's own, both captioned. Each texture is rebuilt only when the page hands over a different
    // decoded image, since Rebuild runs on every keypress.
    private Control HangarArtColumn(HangarArt art, float s)
    {
        var box = new VBoxContainer();
        box.CustomMinimumSize = new Vector2(HangarArtWidth * s, 0f);
        box.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        AddArt(box, art, HangarArtHeight * s, ref _hangarArtSource, ref _hangarArtTexture, s);
        if (PageRowArt() is { } rowArt)
        {
            box.AddChild(Spacer((int)(6 * s)));
            AddArt(box, rowArt, HangarRowArtHeight * s, ref _hangarRowArtSource,
                ref _hangarRowArtTexture, s);
        }

        return box;
    }

    // One captioned picture into a column, reusing the caller's cached texture when the page hands
    // over the same decoded image again.
    private void AddArt(Control box, HangarArt art, float height, ref TgaImage? source,
        ref ImageTexture? texture, float s)
    {
        if (!ReferenceEquals(source, art.Image))
        {
            var image = Image.CreateFromData(art.Image.Width, art.Image.Height, false,
                Image.Format.Rgba8, art.Image.Rgba);
            texture = ImageTexture.CreateFromImage(image);
            source = art.Image;
        }

        var rect = new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(0, height),
        };
        rect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(rect);
        var caption = Label(art.Caption, (int)(FooterFont * s), DetailColor, HorizontalAlignment.Center);
        caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(caption);
    }

    // The join strip shown under the breadcrumb on every screen: who is in, on what
    // device, plus the hint that free pads can join with Start.
    private Control JoinStrip(float s)
    {
        _stripText = JoinStripText();
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", (int)(14 * s));
        for (int i = 0; i < _slots.Count; i++)
        {
            var label = Label($"{SplitScreen.PlayerTag(i)}  {_slots[i].Input.DeviceLabel}",
                (int)(FooterFont * s), SplitScreen.PlayerColor(i), HorizontalAlignment.Center);
            label.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            row.AddChild(label);
        }
        var hint = Label(JoinHint(), (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center);
        hint.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        row.AddChild(hint);
        return row;
    }

    // The strip as plain text — compared each frame so a hotplug (or a join) redraws
    // even when nothing was pressed.
    private string JoinStripText()
    {
        var parts = new List<string>(_slots.Count + 1);
        for (int i = 0; i < _slots.Count; i++)
            parts.Add($"{SplitScreen.PlayerTag(i)} {_slots[i].Input.DeviceLabel}");
        parts.Add(JoinHint());
        return string.Join(" | ", parts);
    }

    // The hint beside the join strip. Joining only happens on the aircraft screen, so
    // the earlier screens say where it will be rather than inviting a press that does nothing.
    // Dogfight below 2 players gets its own line — CanLaunch is withholding the
    // launch gesture, so the generic "you may join" hint would undersell what is actually
    // blocking it.
    private string JoinHint()
    {
        if (_slots.Count >= SplitScreen.MaxPlayers)
            return $"({SplitScreen.MaxPlayers}-player maximum)";
        if (_screen != Screen.Plane)
            return "(other players join at aircraft select)";
        if (_mode == MenuMode.Versus && _slots.Count < 2)
            return $"(Dogfight needs a fight — {SplitScreen.PlayerTag(_slots.Count)}: press START to join)";
        return Pads.Connected().Count > 0
            ? "(press START on a free pad to join)"
            : "(connect a pad and press START to join)";
    }

    private string Footer()
    {
        if (CentredFitRows() != null)
        {
            return "↑↓  Choose mount       ←→  Change       L / Y or Esc / B  Done";
        }

        if (_screen == Screen.Campaign)
        {
            // The campaign's pages name their own presses: a screen with an armed text field has a
            // different control set from the same screen with the cursor on its list.
            return _campaign?.Page.Footer ?? "Esc / B  Back";
        }

        if (_screen == Screen.Hangar)
        {
            // Name the presses this hangar screen actually has: the two pick screens select on
            // Enter and have no stepper at all (E49), and the defaults ask is two answers.
            if (_hangar?.DefaultsAsk != null)
            {
                return "↑↓  Choose       Enter / A  Answer       Esc / B  Back";
            }

            if (_hangar?.Screen == HangarScreen.PlaneSelection)
            {
                return "↑↓  Choose       Enter / A  Select       Esc / B  Back";
            }

            if (_hangar?.Screen == HangarScreen.Name)
            {
                // W/A/S/D are dead here (MenuInput.TextEntry), so the arrows are named alone.
                return "↑↓  Choose       ←→  Change word       Type / Backspace  Rename"
                    + "       Enter / A  Continue       Esc / B  Back";
            }

            return _hangar?.Screen is HangarScreen.Airframe or HangarScreen.Engine
                ? "↑↓  Choose       Enter / A  Select, again to continue       Esc / B  Back"
                : "↑↓  Choose       ←→  Change       Enter / A  Continue       Esc / B  Back";
        }

        string back = _screen == Screen.Mode ? "Esc / B  Quit" : "Esc / B  Back";
        string who = _slots.Count > 1 ? "       (P1 chooses)" : "";
        string nav = _screen switch
        {
            Screen.MissionType => "↑↓  Choose mission       ←→  Lives",
            Screen.WaveEdit or Screen.Wingmen or Screen.Options => "↑↓  Choose field       ←→  Change",
            _ => "↑↓  Navigate",
        };
        // The loadout is an unbound face button, so it is invisible unless the footer says so.
        // Named only where it does something: a lone pilot at aircraft select, and the wingmen
        // step once there are wingmen to arm.
        string fit = _screen == Screen.Plane || (_screen == Screen.Wingmen && _numWingmen > 0)
            ? "       L / Y  Weapons"
            : "";
        // Same rule for the contents list, and the same reason: an unbound face button nobody can
        // guess at. Named on step 1, the one screen it opens from.
        string presets = _screen == Screen.Environment ? "       P / X  Scenarios" : "";
        // Name the press that is actually next. Before the lock that is "select"; after it, "fly"
        // — a footer still offering "select" on an already-selected plane is why the second press
        // was not obvious in the first place.
        string select = _screen != Screen.Plane ? "Enter / A  Select"
            : _slots.Count == 1 && _slots[0].Locked ? "Enter / A  FLY"
            : "Enter / A  Select";
        return $"{nav}       {select}{fit}{presets}       {back}{who}";
    }

    private string Breadcrumb()
    {
        string mode = Modes[(int)_mode].Label;
        return _screen switch
        {
            Screen.Mode => "Mode  ›  Map  ›  Aircraft",
            Screen.Hangar => $"{HangarRow}  ›  {_hangar?.Page.Title}",
            Screen.Campaign => $"{CampaignRow}  ›  {_campaign?.Page.Title}",
            Screen.Options => OptionsRow,
            Screen.Chapter => $"{mode}  ›  Map  ›  Aircraft",
            Screen.Presets => $"{mode}  ›  Table of Contents",
            Screen.Environment => $"{mode}{PresetCrumb()}  ›  Environment  ›  Mission  ›  Aircraft",
            Screen.MissionType => $"{mode}{PresetCrumb()}  ›  {Environments[_environmentIndex].Name}  ›  Mission  ›  Aircraft",
            Screen.Waves or Screen.WaveEdit =>
                $"{mode}{PresetCrumb()}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Waves  ›  Aircraft",
            Screen.Wingmen or Screen.WingmanLoadout =>
                $"{mode}{PresetCrumb()}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Wingmen  ›  Aircraft",
            _ when _mode == MenuMode.Stunt =>
                $"{mode}{PresetCrumb()}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Aircraft",
            _ => $"{mode}  ›  {CurrentChapters[_chapterIndex].Name}  ›  Aircraft",
        };
    }

    // The applied preset's name, as the configuration page's own heading crumb — the original's
    // View Story formats it through IDS_IA_STORYTITLE, whose whole text is `%1!s!`, and nothing
    // clears it when a dropdown is then changed by hand. Empty on the custom path.
    private string PresetCrumb() =>
        _presetIndex >= 0 ? $"  ›  {InstantActionPresets.All[_presetIndex].Name}" : "";

    private string Detail(int focus) => _screen switch
    {
        Screen.Mode => focus < Modes.Length ? Modes[focus].Detail
            : focus == Modes.Length ? "Fly the story: pick a player, then the cabin."
            : focus == Modes.Length + 1 ? "Build a plane in the hangar and fly it."
            : "Choose which menu presentation draws the menus.",
        Screen.Hangar => _hangar?.Page.Detail(focus) ?? "",
        Screen.Campaign => _campaign?.Page.Detail(focus) ?? "",
        Screen.Options => focus == 0
            ? "Built-in needs no extracted menu art; Original draws the original's own screens from it."
            : "Saves the choice and restarts the menu at its top level; unfinished setup is discarded.",
        Screen.Presets => PresetDetail(focus),
        Screen.Chapter => $"Region {CurrentChapters[focus].Code}",
        Screen.Environment => $"Region {Environments[focus].Code}",
        Screen.MissionType => LivesDetail(),
        Screen.Waves => focus == _waves.Length ? "Enter / A  on to the wingmen" : "Enter / A  edit a wave",
        Screen.WaveEdit or Screen.Wingmen => "←→  change",
        // Blank: the footer already names the steppers, and a second copy of "←→ change" directly
        // over it reads as two different controls rather than one.
        _ when CentredFitRows() != null => "",
        _ => focus < _roster.Count
            ? PlaneStat(_roster[focus].Node)
            : "Build a plane in the hangar and fly it.",
    };

    // The focused preset's own line: what picking it would fill the wizard with. The enemy total
    // is the sum of its waves, which is 0 for the five ace presets — a duel, not an empty mission.
    private string PresetDetail(int focus)
    {
        var preset = InstantActionPresets.All[focus];
        int enemies = 0;
        foreach (var wave in preset.Waves)
            enemies += wave.Count;
        string label = Mech3.InstantAction.MissionTypeLabel(preset.MissionType);
        string wingmen = preset.NumWingmen == 0 ? "alone" : $"{preset.NumWingmen} wingmen";
        return $"{label} over {preset.Environment}   ·   {preset.PlayerPlane}, {wingmen}   ·   {enemies} enemies";
    }

    // The lives stepper's own line, shown where the other screens show the focused row's
    // stat/region — it is not per-row, so it does not vary with the mission-type cursor.
    private string LivesDetail() =>
        _lives == 0 ? "Lives   Unlimited        ◀ ▶  change" : $"Lives   {_lives}        ◀ ▶  change";

    // The Plane screen's own Instant Action line: the flown-wingmen re-clamp (decision
    // 8a, InstantActionRuntime.FlownWingmen) against the CURRENT joined-player count
    // — recomputed every Rebuild, so it tracks a pilot joining live. Empty outside Instant Action
    // or at 0 configured wingmen, which is what lets the caller skip the row entirely rather than
    // draw a blank one.
    private string WingmenLine()
    {
        if (_mode != MenuMode.Stunt || _numWingmen == 0)
            return "";
        int flown = InstantActionRuntime.FlownWingmen(_numWingmen, _slots.Count);
        return flown == _numWingmen
            ? $"Wingmen  {_numWingmen}"
            : $"Wingmen  {flown} of {_numWingmen} configured (flight capped at 6)";
    }

    // A couple of stats for the focused plane, loaded lazily from vehicle.json and cached
    // (null = load failed, shown as unavailable — never blocks the menu). fd_speed → mph is the
    // validated top-speed figure (see PlaneStats).
    private string PlaneStat(string node)
    {
        var s = StatsFor(node);
        if (s == null)
            return "(stats unavailable)";
        return $"Top Speed  {Mph(s)} mph        Weight  {s.VehWeight:0}";
    }

    // Just the top speed — the compact form used in the per-player pick lines.
    private string PlaneSpeed(string node)
    {
        var s = StatsFor(node);
        return s == null ? "stats n/a" : $"{Mph(s)} mph";
    }

    private PlaneStats? StatsFor(string node)
    {
        if (!_stats.TryGetValue(node, out var s))
        {
            try { s = PlaneStats.Load(_zrdrPath, node); }
            catch (Exception e) { GD.Print($"launchscreen: no stats for {node}: {e.Message}"); s = null; }
            _stats[node] = s;
        }
        return s;
    }

    // One editable line of a loadout list. Key is the gun slot (1-4) or the physical pylon
    // number (1-8) — slot identity, the same key LoadoutChoice uses, never a row index.
    private readonly record struct FitRow(FitRowKind Kind, int Key, string Label, string Value);

    private readonly record struct Choice(string Label, string Detail);

    // One wizard wave slot's UI state: how many enemies (0 = unconfigured), and the
    // militia/aircraft/skill picked for it. `MilitiaIndex` resets `AircraftIndex` to 0
    // when it changes (HandleMoveX's WaveEdit case) — the decoded setup screen's own
    // `AV[BA].QG = 0` (docs/formats/instant-action.md), since a militia's aircraft list is
    // somebody else's roster once the militia changes. Mutable (not the record structs above) —
    // HandleMoveX edits a field through a `ref` into _waves.
    private struct WaveSlot
    {
        public int Count;
        public int MilitiaIndex;
        public int AircraftIndex;
        public int SkillIndex;
    }

    // One joined player: their device binding, their cursor in the plane list, and
    // whether they have locked their pick. Player 1's poller is the host's first seat's.
    private sealed class Slot
    {
        public readonly MenuInput Input;

        // Starts at Planes's index 0 — the Autogyro under the 3700 order, matching gui_continue's
        // own no-custom-planes selection rather than being positional by accident.
        public int PlaneIndex;

        // Browsing → Locked → Confirmed. The second stage exists so there IS a moment to open the
        // loadout from: locking used to launch on the same frame the last slot locked.
        public bool Locked;
        public bool Confirmed;

        // This pane is showing its loadout list instead of the roster. Per slot, so one player
        // arming cannot pull anybody else out of browsing.
        public bool InLoadout;
        public int FitRow;
        public LoadoutChoice Fit = new();

        public Slot()
            : this(new MenuInput())
        {
        }

        public Slot(MenuInput input)
        {
            Input = input;
        }
    }
}
