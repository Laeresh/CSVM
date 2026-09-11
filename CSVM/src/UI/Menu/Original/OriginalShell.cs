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
    /// <summary>The main menu: the decoded <c>[@MainMenu@]</c> rows plus the Free Flight and Dogfight doors.</summary>
    TopLevel,

    /// <summary>The remake-only Free Flight screen: a chapter list, the aircraft list, the seats, BACK and FLY.</summary>
    FreeFlight,

    /// <summary>The remake-only Dogfight screen: the Free Flight screen's shape over the Dogfight gate.</summary>
    Dogfight,

    /// <summary>The remake-only per-seat aircraft screen: the campaign plane-selection board's
    /// shape over the sortie roster, one joined seat picking at a time.</summary>
    SeatPlane,

    /// <summary>The Options screen: the decoded <c>[@Preferences@]</c> page, its GAME OPTIONS,
    /// AUDIO and VIDEO doors live and its CONTROLS door drawn disabled.</summary>
    Options,

    /// <summary>The decoded <c>[@GameOptions@]</c> page: the shared options in its authored row
    /// shape, with ACCEPT CHANGES and CANCEL CHANGES under them.</summary>
    GameOptions,

    /// <summary>The decoded <c>[@Audio@]</c> page: the four volume levels as sliders in its
    /// authored row shape, with ACCEPT CHANGES and CANCEL CHANGES under them.</summary>
    Audio,

    /// <summary>The decoded <c>[@Video@]</c> page: the display settings in its authored row shape,
    /// with ACCEPT CHANGES and CANCEL CHANGES beside them.</summary>
    Video,

    /// <summary>The decoded <c>[@Credits@]</c> screen: the background pane the credit names are
    /// painted into, ABOUT drawn disabled and the DONE plaque.</summary>
    Credits,

    /// <summary>The decoded <c>[@InstantAction@]</c> setup screen: the Table of Contents, the
    /// dropdowns, the paged enemy rows, the radio pair and its buttons.</summary>
    InstantAction,

    /// <summary>The Instant Action screen's Weapon Loadout, the decoded <c>[@OrdinanceLayout@]</c>
    /// chrome over the fit of the seat the radio pair names.</summary>
    InstantActionLoadout,

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

    /// <summary>A decoded slider: a thumb held and moved along a slot, or stepped sideways, over
    /// the whole numbers its track spans. The one continuous control the shell has.</summary>
    Slider,
}

/// <summary>One slider carried by its row, for the pointer's hold-and-move and the sideways step:
/// the track the thumb runs on, the value it stands at, the write that puts it somewhere else, and
/// the slot art under it (the row's own art being the thumb, the one piece that moves). A page
/// declares one of these and the shell needs to know nothing else about the setting behind it,
/// which is the same bargain <see cref="OriginalList"/> strikes for a scrolled list.</summary>
public sealed record OriginalSlider(SliderTrack Track, int Value, Action<int> SetValue, BoardArt? Slot);

/// <summary>One interactive element of a screen in authored 800x600 pixels: what it is, where it
/// is, whether it reacts, which column it belongs to for the seat's cursor, whether it is on
/// screen (a list row outside its window keeps its place for the keyboard, unseen and unhit), and
/// the slider it carries when it is one.</summary>
public sealed record OriginalRow(
    string Key, string Label, OriginalRowKind Kind, float X, float Y, float Width, float Height,
    bool Enabled, int Column, BoardArt? Art, bool Visible = true, OriginalSlider? Slider = null)
{
    /// <summary>Whether an authored point lies on the row.</summary>
    public bool Contains(float x, float y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>What one frame of input did: the cues to play, the exit to take if any, and whether
/// the picture changed.</summary>
public sealed record OriginalStep(IReadOnlyList<string> Cues, MenuExit? Exit, bool Changed);

/// <summary>One scrolling list on the screen showing, for the pointer's wheel and thumb drag:
/// its window as the list widget describes it and the write that puts the window's first row
/// somewhere else, which also pulls the focus inside the window when it stood on a row the move
/// would hide. The arrows and the keyboard never go through this.</summary>
public sealed record OriginalList(string Key, ListWindow Window, Action<int> ScrollTo);

/// <summary>The colours the shell writes in, read off the layout: the file-wide four state
/// colours and the paper plaque's own label tail.</summary>
public sealed record OriginalInks(
    MenuLayoutColor Disabled, MenuLayoutColor Active, MenuLayoutColor Rollover, MenuLayoutColor Depressed,
    MenuLayoutColor LabelNormal, MenuLayoutColor LabelRollover, MenuLayoutColor LabelDepressed);

/// <summary>The colours the Options screen and the pages behind its doors write in, read off
/// <c>[@Preferences@]</c>: its description rows' authored text colour and its title's, which
/// <c>[@GameOptions@]</c> and <c>[@Video@]</c> repeat row for row.</summary>
public sealed record OriginalPreferencesInks(MenuLayoutColor Text, MenuLayoutColor Title);

/// <summary>
/// The Original presentation's screen graph, engine-free: the decoded top level with the
/// remake-only Free Flight and Dogfight doors, the sortie screens, the Options screen over the
/// decoded Preferences chrome with the Game Options, AUDIO and VIDEO pages behind its three live doors, and the decoded
/// Instant Action, loadout, campaign and hangar screens over their shared features (each family
/// its own partial file), driven by each seat's semantic commands and composed into a
/// <see cref="ComposedBoard"/> in the authored 800x600 space. Seat
/// 0's pointer arrives already mapped into that space; hovering a live row moves the focus onto
/// it, so keyboard, pad and pointer share one cursor. A dialog (the original's messagebox) may
/// stand over any screen, and while one does its answers are the only rows. Every rectangle and art
/// name comes from the layout, the art's pixel size from the measurer the presentation injects.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The Free Flight door's key on the top level.</summary>
    public const string FreeFlightKey = "FREEFLIGHT";

    /// <summary>The Free Flight screen's leave button.</summary>
    public const string BackKey = "BACK";

    /// <summary>The Free Flight screen's launch button.</summary>
    public const string FlyKey = "FLY";

    /// <summary>The Game Options page's difficulty dropdown, the page's first row.</summary>
    public const string DifficultyKey = "DIFFICULTY";

    /// <summary>The Game Options page's menu-presentation dropdown.</summary>
    public const string PresentationKey = "PRESENTATION";

    /// <summary>The VIDEO page's enhanced-graphics checkbox.</summary>
    public const string GraphicsKey = "GRAPHICS";

    /// <summary>The AUDIO page's Master slider, the page's first row.</summary>
    public const string AudioMasterKey = "AUDIOMASTER";

    /// <summary>The AUDIO page's Music Volume slider.</summary>
    public const string AudioMusicKey = "AUDIOMUSIC";

    /// <summary>The AUDIO page's Effects Volume slider.</summary>
    public const string AudioEffectsKey = "AUDIOEFFECTS";

    /// <summary>The AUDIO page's Voice Volume slider.</summary>
    public const string AudioVoiceKey = "AUDIOVOICE";

    /// <summary>The VIDEO page's monitor dropdown, the page's first row.</summary>
    public const string MonitorKey = "MONITOR";

    /// <summary>The VIDEO page's resolution dropdown.</summary>
    public const string ResolutionKey = "RESOLUTION";

    /// <summary>The VIDEO page's display-mode dropdown.</summary>
    public const string DisplayModeKey = "DISPLAYMODE";

    /// <summary>The VIDEO page's V-Sync dropdown.</summary>
    public const string VSyncKey = "VSYNC";

    /// <summary>The Options screen's section in the layout, whose chrome it is composed over.</summary>
    public const string PreferencesSection = "Preferences";

    /// <summary>The Options screen's way back, <c>[@Preferences@]</c>'s own RETURN TO MAIN MENU.</summary>
    public const string OptionsBackKey = "PF_B_MAINMENU";

    /// <summary>The Preferences page's four page doors, in their authored order. The first three
    /// open the Game Options, AUDIO and VIDEO pages; the fourth draws disabled, no shared controls
    /// option standing behind it.</summary>
    public static readonly string[] PreferencesPageKeys = { GameOptionsDoorKey, AudioDoorKey, VideoDoorKey, "PF_B_CONTROLS" };

    private const float PreferencesTitleFont = 20f;
    private const float PreferencesTextFont = 14f;

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
    // region grows: three pixels of slot become twenty-three, which is what makes the whole thumb
    // pressable. Every shipped slider row authors these four.
    private const int SliderInsetLeft = 0;
    private const int SliderInsetTop = -10;
    private const int SliderInsetRight = 1;
    private const int SliderInsetBottom = -10;

    // The layout key every screen that authors a background movie spells it under, [@FinalCinema@]
    // and [@CampaignIntro@] excepted; those two are cinemas rather than screens with one behind them.
    private const string MovieKey = "MOVIE";

    // The screens whose section authors a background movie Original composes. Save and Load author
    // the same row and take an entry here whenever Original composes them, needing nothing else.
    private static readonly IReadOnlyDictionary<OriginalScreen, string> MovieSections =
        new Dictionary<OriginalScreen, string>
        {
            [OriginalScreen.TopLevel] = OriginalAvailability.MainMenuSection,
            [OriginalScreen.Options] = PreferencesSection,
        };

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
    private readonly Func<CSVM.Utils.OptionsDef>? _options;
    private readonly Func<IReadOnlyList<string>>? _screenSizes;
    private readonly Func<CSVM.Utils.ScreenList>? _screens;
    private readonly SliderControl _slider = new();
    private readonly int[] _focus = new int[Enum.GetValues<OriginalScreen>().Length];
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);

    private OriginalScreen _screen;
    private int _hover = -1;
    private int _pressed = -1;
    // The key of the row a press landed on. A press activates nothing: the row draws its held
    // frame while the button is down and fires only when the button comes up still on it, so a
    // press released anywhere else fires nothing.
    private string? _armed;
    // Which bitmap the pointer wears. It answers an enter or a leave and is recomputed only when
    // the pointer moves, so a screen drawn under a still pointer keeps the bitmap it arrived with.
    private bool _pointerLive;
    private (float X, float Y)? _pointer;
    // A thumb drag in progress: which list, where the pointer took hold and where the window stood.
    private (string Key, float StartY, int StartTop)? _drag;
    private int _pickedChapter = -1;
    private string _choice = PresentationId.Original.Value;
    private string _graphics = CSVM.Utils.GraphicsMode.Default;
    private int _difficulty = CSVM.Flight.Difficulty.Normal;
    // The four display settings as they were saved. A page that shows a setting still has to hand
    // back the ones it does not, or the one writer's save would clear them; carrying them on the
    // shell is what lets either page's apply do that.
    private string? _monitorIndex;
    private string? _resolution;
    private string? _displayMode;
    private string? _vsync;
    // The four saved volume levels, carried for the same reason: the AUDIO page shows them and the
    // other option pages do not, and every page's apply hands back the settings it does not show.
    private int? _audioMaster;
    private int? _audioMusic;
    private int? _audioEffects;
    private int? _audioVoice;

    /// <summary>A shell over <paramref name="layout"/> and the shared features. <paramref name="measure"/>
    /// answers an art name with its strip's pixel size (null when the file is not there),
    /// <paramref name="flightDevices"/> a seat with its launch's devices; the chapters default to
    /// <see cref="OriginalRosters"/>, a missing Instant Action feature to a private one. Instant
    /// Action's Build Custom Plane stands only over <paramref name="hangar"/> and <paramref name="planes"/>
    /// together, the Campaign row only over <paramref name="campaign"/> and <paramref name="profiles"/> together.</summary>
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
        string? dataRoot = null,
        // Reads the saved options the Options screen shows back; null opens it on the defaults,
        // which is what an engine-free test wants. The shell never writes them.
        Func<CSVM.Utils.OptionsDef>? options = null,
        // Reads the sizes the window's own screen can hold, the resolution row's words; null offers
        // every candidate size, there being no screen to ask without an engine.
        Func<IReadOnlyList<string>>? screenSizes = null,
        // Reads the screens the machine has, the monitor row's words and the screen a saved index
        // that names none falls back to; null offers the one screen an engine-free caller can.
        Func<CSVM.Utils.ScreenList>? screens = null)
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
        _options = options;
        _screenSizes = screenSizes;
        _screens = screens;
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

    /// <summary>The colours the Options screen and the Game Options page write in.</summary>
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

    /// <summary>Whether the pointer wears the active bitmap. Set on an enter or a leave and on
    /// nothing else, so it can disagree with <see cref="Hover"/> on a screen the pointer did not
    /// move onto.</summary>
    public bool PointerLive => _pointerLive;

    /// <summary>The key of the row a press is holding, or "". It fires when the button comes up
    /// still on it and never otherwise.</summary>
    public string ArmedKey => _armed ?? string.Empty;

    /// <summary>The screen's scrolling lists as the pointer sees them, the topmost first: none
    /// under a dialog, an open dropdown's list alone while one stands, else the screen's own.</summary>
    public IReadOnlyList<OriginalList> Lists
    {
        get
        {
            var lists = new List<OriginalList>();
            if (_dialog != null)
            {
                return lists;
            }

            switch (_screen)
            {
                case OriginalScreen.FreeFlight:
                case OriginalScreen.Dogfight:
                    SortieLists(lists);
                    break;
                case OriginalScreen.InstantAction:
                case OriginalScreen.InstantActionLoadout:
                    InstantActionLists(lists);
                    break;
                case OriginalScreen.SeatPlane:
                case var _ when IsCampaignScreen:
                    CampaignLists(lists);
                    break;
                case var _ when IsHangarScreen:
                    HangarLists(lists);
                    break;
            }

            return lists;
        }
    }

    /// <summary>The list whose thumb the pointer is dragging, or null.</summary>
    public string? Dragging => _drag?.Key;

    /// <summary>The slider row the pointer is dragging, or null. Kept apart from
    /// <see cref="Dragging"/> so a drag can never change which of the two it belongs to.</summary>
    public string? DraggingSlider => _slider.Held;

    /// <summary>The picked chapter's code, or null.</summary>
    public string? PickedChapter => _pickedChapter >= 0 ? _chapters[_pickedChapter].Code : null;

    /// <summary>The presentation the Game Options page would apply.</summary>
    public string PresentationChoice => _choice;

    /// <summary>The graphics mode word the VIDEO page would apply.</summary>
    public string GraphicsChoice => _graphics;

    /// <summary>The screen index (<see cref="CSVM.Utils.MonitorSetting.Word"/>'s spelling) the
    /// VIDEO page would apply, or null while nothing has been saved and no row has been
    /// touched.</summary>
    public string? MonitorChoice => _monitorIndex;

    /// <summary>The window size (<see cref="CSVM.Utils.OptionsStore.FormatResolution"/>'s spelling)
    /// the VIDEO page would apply, or null while nothing has been saved and no row has been
    /// touched.</summary>
    public string? ResolutionChoice => _resolution;

    /// <summary>The display-mode word (<see cref="CSVM.Utils.DisplayWords.DisplayModes"/>) the
    /// VIDEO page would apply, or null while nothing has been saved and no row has been
    /// touched.</summary>
    public string? DisplayModeChoice => _displayMode;

    /// <summary>The V-Sync word (<see cref="CSVM.Utils.DisplayWords.VSyncChoices"/>) the VIDEO page
    /// would apply, or null while nothing has been saved and no row has been touched.</summary>
    public string? VSyncChoice => _vsync;

    /// <summary>The Master level (<see cref="CSVM.Utils.AudioMix"/>'s 0..100) the AUDIO page would
    /// apply, or null while nothing has been saved and no row has been touched.</summary>
    public int? AudioMasterChoice => _audioMaster;

    /// <summary>The Music level the AUDIO page would apply, or null while never set.</summary>
    public int? AudioMusicChoice => _audioMusic;

    /// <summary>The Effects level the AUDIO page would apply, or null while never set.</summary>
    public int? AudioEffectsChoice => _audioEffects;

    /// <summary>The Voice level the AUDIO page would apply, or null while never set.</summary>
    public int? AudioVoiceChoice => _audioVoice;

    /// <summary>The difficulty tier (<see cref="CSVM.Flight.Difficulty"/>) the Game Options page
    /// would apply.</summary>
    public int DifficultyChoice => _difficulty;

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

    /// <summary>Opens a screen directly, the screenshot aids' door. Any screen the walk does not
    /// stand on (<see cref="OnSeatWalk"/>) ends a seat walk in progress.</summary>
    public void Open(OriginalScreen screen)
    {
        _screen = screen;
        _hover = -1;
        _pressed = -1;
        _armed = null;
        if (!OnSeatWalk)
        {
            _pickingSeat = null;
            _seatPage = null;
            _loadoutSeat = null;
        }

        _drag = null;
        _slider.LetGo();
        if (screen is OriginalScreen.GameOptions or OriginalScreen.Audio or OriginalScreen.Video)
        {
            ReadSavedOptions();
        }
    }

    /// <summary>Applies one frame of seat 0's commands, under the rule <see cref="StepSeat"/>
    /// applies to it: on a screen standing for another seat, the per-seat aircraft screen picking
    /// for one and the campaign check's screens on a guest's, only its pointer counts. The pointer,
    /// when present, is in authored pixels.</summary>
    public OriginalStep Step(MenuCommands commands) => StepSeat(0, commands);

    // One seat's frame applied to the screen showing, the seat's own right to drive it already
    // settled by StepSeat.
    private OriginalStep ApplyFrame(MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var cues = new List<string>();
        MenuExit? exit = null;
        SyncCampaignField();
        bool changed = TypeName(commands, cues);
        if (OnSeatWalk && _pickingSeat is not { Joined: true })
        {
            // The seat this screen was picking for has gone: the walk moves on or ends, and a
            // Weapon Loadout it had open on that seat's own fit goes with it.
            DropLoadout();
            exit = AdvanceSeatWalk();
            changed = true;
        }

        var rows = Rows;
        int focus = EnsureFocus(rows);

        if (commands.Pointer is { } pointer)
        {
            bool pointerMoved = _pointer != (pointer.X, pointer.Y);
            changed |= pointerMoved;
            _pointer = (pointer.X, pointer.Y);
            // The thumb, the slider and the wheel come before the rows: a held one owns the
            // pointer until it is let go, and a wheel step moves the rows the hit test then reads.
            // The thumb has first refusal and stands down while a slider holds, so neither crosses.
            bool dragging = _slider.Held == null && DragThumb(pointer, ref changed);
            if (!dragging)
            {
                dragging = _slider.Drive(rows, pointer, out bool moved);
                changed |= moved;
            }

            if (!dragging && pointer.Wheel != 0)
            {
                changed |= WheelList(pointer);
            }

            rows = Rows;
            focus = EnsureFocus(rows);
            int over = dragging ? -1 : HitTest(rows, pointer.X, pointer.Y);
            if (pointerMoved)
            {
                bool live = over >= 0 && rows[over].Enabled;
                changed |= live != _pointerLive;
                _pointerLive = live;
            }

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

            if (pointer.Clicked && !dragging && over >= 0 && rows[over].Enabled)
            {
                _armed = rows[over].Key;
            }

            bool onArmed = _armed != null && over >= 0 && rows[over].Enabled && rows[over].Key == _armed;
            int pressed = onArmed ? over : -1;
            changed |= pressed != _pressed;
            _pressed = pressed;
            bool fires = onArmed && !pointer.Pressed;
            if (!pointer.Pressed)
            {
                _armed = null;
            }

            if (dragging)
            {
                // A drag's click was spent on the thumb or the slider it took hold of; nothing
                // under the pointer is activated.
            }
            else if (fires)
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
            else if (pointer.Clicked && over < 0
                && (CloseInstantActionDropdown() || CloseHangarDropdown() || CloseCampaignCombo() || CloseGameOptionsDropdown()))
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
            // A sideways step changes a value where the cursor stands on one (a slider, first
            // because it belongs to no one screen, then an Instant Action or loadout dropdown, a
            // radio, an option row, a hangar tab, a campaign field); else it crosses columns.
            if (SliderControl.StepValue(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (IsInstantActionFamily && StepInstantActionValue(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (_screen == OriginalScreen.GameOptions && StepGameOptionValue(rows, focus, commands.MoveX))
            {
                rows = Rows;
                focus = EnsureFocus(rows);
            }
            else if (_screen == OriginalScreen.Video && StepVideoValue(rows, focus, commands.MoveX))
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

        // With seat 0's aircraft picked, a joined seat still to confirm gets its own screen.
        if (exit == null && IsSortie && BeginSeatWalkIfDue())
        {
            changed = true;
        }

        return new OriginalStep(cues, exit, changed);
    }

#pragma warning disable SA1202 // ApplyFrame above stays beside the Step that delegates to it.
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
        ComposeMovie(backdrop);
        var main = _layout.Screen(OriginalAvailability.MainMenuSection);
        bool ownPage = _screen is OriginalScreen.InstantAction or OriginalScreen.InstantActionLoadout
            or OriginalScreen.Options or OriginalScreen.GameOptions or OriginalScreen.Audio or OriginalScreen.Video
            or OriginalScreen.SeatPlane or OriginalScreen.Credits
            || IsHangarScreen || IsCampaignScreen;
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
            case OriginalScreen.InstantActionLoadout:
                ComposeLoadout(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case var _ when IsCampaignScreen:
                ComposeCampaign(rows, focus, backdrop, pictures, fills, strokes, lines, plaques, notes, overlays);
                break;
            case OriginalScreen.SeatPlane:
                ComposeSeatPlane(focus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case var _ when IsHangarScreen:
                ComposeHangar(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case OriginalScreen.FreeFlight:
            case OriginalScreen.Dogfight:
                ComposeSortie(rows, lines, overlays);
                break;
            case OriginalScreen.Options:
                ComposeOptions(pictures, lines);
                break;
            case OriginalScreen.Credits:
                ComposeCredits(pictures);
                break;
            case OriginalScreen.GameOptions:
                ComposeGameOptions(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
            case OriginalScreen.Audio:
                ComposeAudio(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques);
                break;
            case OriginalScreen.Video:
                ComposeVideo(screenRows, screenFocus, backdrop, pictures, fills, lines, plaques, overlays);
                break;
        }

        if (!ownPage || _screen is OriginalScreen.Options or OriginalScreen.Credits)
        {
            ComposeRows(screenRows, screenFocus, fills, lines, plaques);
        }

        if (_dialog != null && !IsCampaignScreen)
        {
            overlays.Add(ComposeDialog(rows, focus));
        }

        if (_pointer is { } at)
        {
            overlays.Add(new BoardPanel(
                Array.Empty<BoardFill>(),
                new[] { new BoardPicture(_pointerLive ? _activePointer : _passivePointer, at.X, at.Y) },
                Array.Empty<BoardLine>()));
        }

        return new ComposedBoard(pictures, strokes, lines, plaques, notes,
            backdrop: backdrop, fills: fills, overlays: overlays);
    }
#pragma warning restore SA1202

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

    // The n-th art a slider row names, falling back to the shipped file name so the control still
    // has a name to draw where the section is absent. Neither art is a strip.
    private static BoardArt SliderArt(MenuLayoutWidget? widget, int index, string fallback)
    {
        var art = widget?.Art;
        string name = art != null && index < art.Count && art[index].Length > 0 ? art[index] : fallback;
        return new BoardArt(BoardArtLibrary.Ui, name, 1);
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

    // A thumb drag: a click on a list's thumb takes hold of it, and while the button stays down
    // the window follows the pointer down the track; letting go ends it. True while one holds.
    private bool DragThumb(MenuPointer pointer, ref bool changed)
    {
        if (_drag is { } drag)
        {
            if (!pointer.Pressed)
            {
                _drag = null;
                changed = true;
                return false;
            }

            foreach (var list in Lists)
            {
                if (list.Key != drag.Key)
                {
                    continue;
                }

                int top = list.Window.TopAfterDrag(drag.StartTop, pointer.Y - drag.StartY);
                if (top != list.Window.Top)
                {
                    list.ScrollTo(top);
                    changed = true;
                }

                return true;
            }

            // The list the drag began on is gone with its screen.
            _drag = null;
            return false;
        }

        if (!pointer.Clicked)
        {
            return false;
        }

        foreach (var list in Lists)
        {
            if (list.Window.OnThumb(pointer.X, pointer.Y))
            {
                _drag = (list.Key, pointer.Y, list.Window.Top);
                _hover = -1;
                changed = true;
                return true;
            }
        }

        return false;
    }

    // A wheel step over a list moves its window by that many rows; the first list containing the
    // pointer takes it, and a list that fits its window ignores it.
    private bool WheelList(MenuPointer pointer)
    {
        foreach (var list in Lists)
        {
            if (!list.Window.Contains(pointer.X, pointer.Y))
            {
                continue;
            }

            int top = list.Window.TopAfterWheel(pointer.Wheel);
            if (top == list.Window.Top)
            {
                return false;
            }

            list.ScrollTo(top);
            return true;
        }

        return false;
    }

    // The rows of a screen that has no page of its own (the top level, the sortie screens, the
    // Options screen): a decoded strip in its state frame, a paper plaque with its label (an
    // outlined label where the plaque art is missing), and list text. Nothing is focused or
    // pressed while a dialog stands over the screen. A row outside its list's window draws
    // nothing, since the window is what the pointer scrolls.
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

    // The screen's background movie, under everything else it draws. The row places it at its own
    // corner and scales the picture by a percentage of the picture's own size, which the measurer
    // answers and no number here does; a movie that does not measure composes nothing, which is
    // this screen with its background missing and the rest of it intact.
    private void ComposeMovie(List<BoardPicture> backdrop)
    {
        if (!MovieSections.TryGetValue(_screen, out string? section)
            || _layout.Screen(section)?.Widget(MovieKey) is not { Art.Count: > 0 } row
            || Measure(row.Art[0]) is not { } size)
        {
            return;
        }

        backdrop.Add(new BoardPicture(
            new BoardArt(BoardArtLibrary.Movie, row.Art[0]), row.Int("X"), row.Int("Y"),
            Width: size.Width * row.Int("ScaleX", 100) / 100f,
            Height: size.Height * row.Int("ScaleY", 100) / 100f));
    }

    // The Options screen's chrome, [@Preferences@]'s own: its logo and background panes, its title
    // and the description beside each page door. The rows themselves (the four doors and RETURN TO
    // MAIN MENU) are drawn by the row loop. With no section the page's own door stands alone over
    // the top level's logo.
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

        foreach (string key in new[] { "PF_T_GODESC", "PF_T_APDESC", "PF_T_VPDESC", "PF_T_CPDESC" })
        {
            if (screen.Widget(key) is { } description)
            {
                lines.Add(new BoardLine(description.Text ?? string.Empty, description.Int("X"), description.Int("Y"),
                    description.Int("Width"), PreferencesTextFont, BoardInk.Row));
            }
        }
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
                    case CampaignKey:
                        OpenCampaign();
                        break;
                    case "MM_B_INSTANTACTION":
                        OpenInstantAction();
                        break;
                    case "MM_B_PREFERENCES":
                        Open(OriginalScreen.Options);
                        break;
                    case CreditsDoorKey:
                        Open(OriginalScreen.Credits);
                        break;
                    case "MM_B_QUIT":
                        return new QuitExit();
                }

                break;
            case OriginalScreen.FreeFlight:
            case OriginalScreen.Dogfight:
                return ActivateSortie(row);
            case OriginalScreen.Credits:
                return ActivateCredits(row);
            case OriginalScreen.InstantAction:
                return ActivateInstantAction(row);
            case OriginalScreen.InstantActionLoadout:
                return ActivateLoadout(row);
            case OriginalScreen.SeatPlane:
                return ActivateSeatPlane(row);
            case var _ when IsCampaignScreen:
                return ActivateCampaign(row);
            case var _ when IsHangarScreen:
                return ActivateHangar(row);
            case OriginalScreen.Options:
                switch (row.Key)
                {
                    case GameOptionsDoorKey:
                        OpenGameOptions();
                        break;
                    case AudioDoorKey:
                        OpenAudio();
                        break;
                    case VideoDoorKey:
                        OpenVideo();
                        break;
                    case BackKey:
                    case OptionsBackKey:
                        Open(OriginalScreen.TopLevel);
                        break;
                }

                break;
            case OriginalScreen.GameOptions:
                return ActivateGameOptions(row);
            case OriginalScreen.Audio:
                return ActivateAudio(row);
            case OriginalScreen.Video:
                return ActivateVideo(row);
        }

        return null;
    }

    // Back with a dialog standing takes its declining answer, the messagebox script's own Escape.
    // On a sortie screen it undoes seat 0's pick a stage at a time, then leaves; the per-seat
    // screen has its own, whose meaning depends on who pressed it. On Instant Action, its loadout
    // and Game Options the first Back closes an open list and the next leaves (CANCEL LOADOUT,
    // CANCEL CHANGES); the VIDEO page has no list, so Back is its CANCEL CHANGES. The campaign and
    // the hangar walk their graphs back, and the top level quits as MAINMENU.SCRIPT's Quit does.
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

        if (_screen == OriginalScreen.SeatPlane)
        {
            return BackSeatPlane();
        }

        if (IsSortie && Seat0 is { } seat && _setup.Back(seat) != SeatBack.Browsing)
        {
            return null;
        }

        if (IsInstantActionFamily && CloseInstantActionDropdown())
        {
            return null;
        }

        if (_screen == OriginalScreen.InstantActionLoadout)
        {
            CloseLoadout(keep: false);
            return null;
        }

        if (_screen == OriginalScreen.GameOptions)
        {
            BackGameOptions();
            return null;
        }

        if (_screen == OriginalScreen.Audio)
        {
            // The AUDIO page carries no list to close first, so Back is its CANCEL CHANGES.
            BackToPreferences();
            return null;
        }

        if (_screen == OriginalScreen.Video)
        {
            BackVideo();
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
                var main = _layout.Screen(OriginalAvailability.MainMenuSection);
                foreach (string key in TopLevelButtons)
                {
                    if (main?.Widget(key) is { } widget)
                    {
                        bool enabled = key is "MM_B_QUIT" or "MM_B_PREFERENCES" or "MM_B_INSTANTACTION" or CreditsDoorKey
                            || (key == CampaignKey && _campaign != null && _profiles != null);
                        rows.Add(Button(widget, enabled));
                    }
                }

                break;
            case OriginalScreen.Credits:
                BuildCreditsRows(rows);
                break;
            case OriginalScreen.InstantAction:
                BuildInstantActionRows(rows);
                break;
            case OriginalScreen.InstantActionLoadout:
                BuildLoadoutRows(rows);
                break;
            case OriginalScreen.SeatPlane:
                if (_seatPage is { } seatPage)
                {
                    BuildPageRows(seatPage, rows);
                }

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
            case OriginalScreen.GameOptions:
                BuildGameOptionsRows(rows);
                break;
            case OriginalScreen.Audio:
                BuildAudioRows(rows);
                break;
            case OriginalScreen.Video:
                BuildVideoRows(rows);
                break;
        }

        return rows;
    }

    // The Options screen over [@Preferences@]: the four page doors at their authored corners, the
    // GAME OPTIONS, AUDIO and VIDEO doors live and CONTROLS disabled since no shared controls
    // option stands behind it, and the section's own RETURN TO MAIN MENU. Without the section the
    // three live doors stand alone with a BACK plaque, so the screen is still navigable.
    private void BuildOptionsRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(PreferencesSection);
        if (screen == null)
        {
            rows.Add(TextButton(GameOptionsDoorKey, "GAME OPTIONS", OptionsX, OptionsTop, true, 0));
            rows.Add(TextButton(AudioDoorKey, "AUDIO", OptionsX, OptionsTop + OptionsPitch, true, 0));
            rows.Add(TextButton(VideoDoorKey, "VIDEO", OptionsX, OptionsTop + (2f * OptionsPitch), true, 0));
            rows.Add(TextButton(BackKey, "BACK", OptionsX, OptionsTop + (3f * OptionsPitch), true, 0));
            return;
        }

        foreach (string key in PreferencesPageKeys)
        {
            if (screen.Widget(key) is { } door)
            {
                rows.Add(Button(door, key is GameOptionsDoorKey or AudioDoorKey or VideoDoorKey));
            }
        }

        if (screen.Widget(OptionsBackKey) is { } back)
        {
            rows.Add(Button(back, true));
        }
        else
        {
            rows.Add(TextButton(BackKey, "BACK", OptionsX, OptionsTop, true, 0));
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

    // A slider row from its authored widget: the slot at the widget's corner in its own art's
    // measured size, the thumb measured from its own art, and the row's rectangle the region the
    // widget insets the slot into, which is what the pointer has to hit. A page supplies the range
    // its setting spans, the level it stands at and where a new level goes.
    private OriginalRow SliderRow(
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
        float left = x + (widget?.Int("Left", SliderInsetLeft) ?? SliderInsetLeft);
        float top = y + (widget?.Int("Top", SliderInsetTop) ?? SliderInsetTop);
        float right = x + slotSize.Width - (widget?.Int("Right", SliderInsetRight) ?? SliderInsetRight);
        float bottom = y + slotSize.Height - (widget?.Int("Bottom", SliderInsetBottom) ?? SliderInsetBottom);
        return new OriginalRow(key, string.Empty, OriginalRowKind.Slider, left, top,
            Math.Max(1f, right - left), Math.Max(1f, bottom - top), enabled, column, thumb,
            Slider: new OriginalSlider(track, track.Clamp(value), setValue, slot));
    }

    // The box that marks a focused row on the pages composed over a painted plate, shared by the
    // slider row and by the dropdown rows that take boxOnFocus, so one outline covers every marked
    // row on those pages. It is the layout's own DISABLED grey rather than the dropdown's authored
    // black, which on dark paint is a dark line nobody sees, and it is a mark rather than standing
    // chrome, so only the row the cursor is on ever carries it.
    private BoardFill FocusBox(OriginalRow row)
    {
        var mark = Inks.Disabled;
        return new BoardFill(row.X, row.Y, row.Width, row.Height, mark.R, mark.G, mark.B, 0.75f, Border: true);
    }

    // A slider as drawn: the slot, then the thumb at the value's own place on it. The thumb is one
    // frame with no focused or pressed state, so focus is the focus box and the wash under it.
    // ⚠ Do not drop either half of that pair, nor the unmeasured-art rectangles. The box is the
    // readable half, the wash is the region it encloses, and the rectangles are how the level
    // still shows. docs/menu-presentations.md and docs/org/menu-inventory.md hold the readings.
    private void ComposeSlider(OriginalRow row, bool focused, List<BoardFill> fills, List<BoardPicture> pictures)
    {
        if (row.Slider is not { } slider)
        {
            return;
        }

        var track = slider.Track;
        if (focused)
        {
            fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
            fills.Add(FocusBox(row));
        }

        float thumbX = track.ThumbX(slider.Value);
        if (slider.Slot != null && row.Art != null && Measure(slider.Slot.Name) != null && Measure(row.Art.Name) != null)
        {
            pictures.Add(new BoardPicture(slider.Slot, track.X, track.Y));
            pictures.Add(new BoardPicture(row.Art, thumbX, track.ThumbY));
            return;
        }

        fills.Add(new BoardFill(track.X, track.Y, track.Width, track.Height, 255, 255, 255, 0.6f, Border: true));
        fills.Add(new BoardFill(thumbX, track.ThumbY, track.ThumbWidth, track.ThumbHeight, 255, 255, 255, 0.6f));
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
