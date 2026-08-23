using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The in-game launchscreen shown on a bare launch. Free Flight and Dogfight go straight to
/// Chapter then Plane; Instant Action opens its own five-step wizard (Environment, MissionType,
/// Waves, Wingmen, Plane). Dogfight withholds the launch gesture until two players have joined;
/// see <see cref="CanLaunch"/>. Input is polled per player through <see cref="MenuInput"/> rather
/// than Godot's input map, since the join flow needs a named device. More than one player splits
/// the Plane screen into <see cref="SplitScreen.PaneRect"/> panes. Re-entrant on return from
/// flight; see <see cref="ShowMenu"/>. Module map: docs/architecture.md. Wizard decode:
/// docs/formats/instant-action.md.
/// </summary>
public sealed partial class LaunchMenu : CanvasLayer
{
    /// <summary>The row that opens the hangar, on the Mode screen and on the Instant Action plane
    /// pick. The original has no button string of its own for it; this is the phrase its own help
    /// text uses (langui 10524).</summary>
    public const string HangarRow = "Build Custom Plane";

    /// <summary>Fired when every joined player has locked a plane: (chapter code, one choice per
    /// player in player order, the picked mode, and — Instant Action only, else null — the
    /// wizard's own built <c>InstantActionDef</c>). The host hides the menu and builds the
    /// session.</summary>
    public Action<string, IReadOnlyList<PlayerChoice>, MenuMode, InstantActionDef?>? Launch;

    /// <summary>Fired when the player backs out of the Mode screen — the host quits.</summary>
    public Action? Quit;

    // Base metrics at 720p, scaled up on taller viewports (like StuntScoreboard). All TUNE.
    private const int TitleFont = 40;
    private const int HeadingFont = 20;
    private const int CrumbFont = 15;
    private const int RowFont = 22;
    private const int DetailFont = 16;
    private const int FooterFont = 15;
    private const int ErrorFont = 15;

    // The hangar art block's 720p height (C22's seam); the 358x335 TGAs letterbox into it.
    private const int HangarArtHeight = 140;
    // Splitscreen plane select (several players): the bottom strip that keeps the breadcrumb +
    // join hint out of the panes, as a fraction of viewport height, and the pane's inner padding.
    // Reference values at 720p. Confirmed at the controls: join/lock feel
    // reads right at 2P and 4P, no retune owed.
    private const float StripHeightFrac = 0.12f;
    private const int PanePad = 10;
    // The lives stepper's range (Screen.MissionType, decision 15/18): 0 = unlimited, 1 = the
    // faithful one-life run (default), up to this cap. INVENTED — ia.json carries no such field, so
    // there is no decoded range to match; TUNE.
    private const int MaxLives = 9;

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

    // The eight chapter worlds (mirrors RunDev.ps1's roster). The lettered codes are separate
    // terrain databases, not lighting variants — docs/formats/spawns.md. DangerZones marks the
    // chapters whose ia.json has a dzones list; ChaptersFor hides the others from Stunt Flying,
    // and a stunt run forced onto them via CLI falls back to free flight (StuntMission).
    private static readonly (string Name, string Code, bool DangerZones)[] Chapters =
    {
        ("Sea Haven (night) — IA: an airfield", "C1", true),
        ("The ocean — Sea Haven variant", "C1B", true),
        ("Sea Haven variant C — no IA, campaign/MP only", "C1C", false),
        ("Hollywood — IA: a movie studio", "C2", true),
        ("The clouds — Hollywood variant", "C2B", false),
        ("Hawaii (islands)", "C3", true),
        ("Rocky Mountains — IA: Sky Haven", "C4", true),
        ("New York — IA: Manhattan", "C5", true),
    };

    // The player-flyable roster (mirrors RunDev.ps1, the curated game order + display names — note
    // Devastator = player_pfighter and Hellhound = player_avenger). Node = the planes.zbd root node
    // passed on to the build; stats are loaded lazily from vehicle.json for the focused plane.
    private static readonly (string Name, string Node)[] Planes =
    {
        ("Devastator", "player_pfighter"),
        ("Bloodhawk", "player_bhawk"),
        ("Firebrand", "player_fbrand"),
        ("Brigand", "player_brigand"),
        ("Fury", "player_fury"),
        ("Autogyro", "player_autogyro"),
        ("Hellhound", "player_avenger"),
        ("Kestrel", "player_kestrel"),
        ("Peacemaker", "player_peacemaker"),
        ("Balmoral", "player_balmoral"),
        ("Warhawk", "player_warhawk"),
    };

    // The thirteen Instant Action militias and the aircraft each one flies, per the `.BM` pattern
    // reading (docs/formats/instant-action.md), not vehicle.json's paint_pattern defs. Names use
    // ia.json's singular vocabulary ("Autogyro"), matching InstantActionWave.EnemyPlane and
    // PlaneNodeFor. A militia is never filtered out here for being the player's own side.
    private static readonly (string Name, string[] Aircraft)[] Militias =
    {
        ("Black Hat", new[] { "Warhawk", "Brigand", "Autogyro" }),
        ("Black Swan", new[] { "Fury" }),
        ("Blake Aviation", new[] { "Bloodhawk", "Peacemaker" }),
        ("British", new[] { "Peacemaker", "Balmoral" }),
        ("Fortune Hunter", PlaneNames()),
        ("Hollywood Knight", new[] { "Firebrand" }),
        ("Hughes Aviation", new[] { "Bloodhawk", "Kestrel", "Fury" }),
        ("Medusa", new[] { "Kestrel", "Brigand" }),
        ("Russian", new[] { "Devastator" }),
        ("Sacred Trust", new[] { "Warhawk", "Hellhound" }),
        ("German", new[] { "Hellhound" }),
        ("Studio Security", new[] { "Fury", "Autogyro" }),
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

    private string _zrdrPath = "";
    private string _dataRoot = "";
    private Screen _screen = Screen.Mode;
    private int _modeIndex, _chapterIndex;
    // Instant Action wizard state, steps 1-2: the picked environment
    // row, the picked mission type row within CurrentMissionTypes, and the lives stepper beside it
    // (decision 18).
    private int _environmentIndex, _missionTypeIndex;
    private int _lives = 1;
    // Steps 3-4: the wingman count + aircraft, and the cursors WaveEdit/Waves/Wingmen each
    // read (_waves itself is above, with the other readonly fields).
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
    // The hangar page's art (C22's seam), as the one texture the shell owns: rebuilt only when
    // the page hands over a different decoded image, since Rebuild runs on every keypress.
    private TgaImage? _hangarArtSource;
    private ImageTexture? _hangarArtTexture;
    // The langui table, loaded on first hangar entry (a session that never opens it never reads
    // the file). Null until then; a failed load leaves UiStrings.Empty here.
    private UiStrings? _uiStrings;
    private string _error = "";
    // The pad player 1 claimed by driving the Mode/Chapter screens with it (−1 = none yet, i.e.
    // player 1 is on the keyboard and every connected pad is still free to join).
    private int _p1Pad = -1;
    // The join strip as last drawn — _Process redraws when the live roster changes (hotplug).
    private string _stripText = "";
    private VBoxContainer _body = null!;
    private CenterContainer _center = null!;
    // The splitscreen plane-select root (one panel per player + a shared bottom strip). Shown
    // instead of _center on the Plane screen once more than one player has joined.
    private Control _paneRoot = null!;

    private enum Screen { Mode, Chapter, Environment, MissionType, Waves, WaveEdit, Wingmen, Plane, WingmanLoadout, Hangar }

    // What a fit row edits. The reset row carries no slot of its own and is the only one Accept
    // does anything on, since every other row is a live stepper.
    private enum FitRowKind { Gun, Pylon, Reset }

    /// <summary>The name of the last plane the hangar built this session, or "". ⚠ This is D31's
    /// seam: it lists saved customs after the eleven stock airframes and auto-selects this one in
    /// the picker the hangar was entered from (the original's index-11 contract). Nothing reads it
    /// yet, so a build today returns to an unchanged picker.</summary>
    public string LastBuiltPlane { get; private set; } = "";

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
        Screen.Environment => _environmentIndex,
        Screen.MissionType => _missionTypeIndex,
        Screen.Waves => _waveListIndex,
        Screen.WaveEdit => _waveFieldIndex,
        Screen.Wingmen => _wingmenFieldIndex,
        Screen.WingmanLoadout => _wingmanFitRow,
        Screen.Hangar => _hangar?.Row ?? 0,
        _ => _slots.Count == 1 && _slots[0].InLoadout ? _slots[0].FitRow : _slots[0].PlaneIndex,
    };

    // The stock-fit table, loaded on first use. A failed load leaves the rosters empty, which
    // shows as a loadout list of nothing but its reset row rather than a crash on the way to
    // flying: the fit is optional and a launch must survive without it.
    private StockLoadouts Fits => _stockFits ??= StockLoadouts.Load();

    // How many rows the Wingmen screen shows right now: the Aircraft field is hidden at
    // 0 wingmen, matching the decoded setup screen's own behaviour.
    private int WingmenRowCount => _numWingmen > 0 ? 2 : 1;

    // Whether the plane pick offers the hangar row. Decision 6 puts the Build entry on the
    // Instant Action pick; a splitscreen pane never draws it, so the roster there stays the
    // eleven stock airframes and PlaneIndex cannot point past them.
    private bool HangarRowOnPlaneScreen => _mode == MenuMode.Stunt && _slots.Count == 1;

    // The plane pick's row count, the eleven airframes plus the hangar row where it is offered.
    private int PlaneRowCount => Planes.Length + (HangarRowOnPlaneScreen ? 1 : 0);

    /// <summary>Builds the (hidden) launchscreen. <paramref name="zrdrPath"/> is the shared zrdr
    /// extraction the plane stats come from; <paramref name="dataRoot"/> is where <c>extracted/</c>
    /// lives, needed to load an Instant Action environment's own <c>ia.zrd.json</c> once one is
    /// confirmed. Add it to the tree, wire <see cref="Launch"/> / <see cref="Quit"/>, then
    /// <see cref="ShowMenu"/>.</summary>
    public static LaunchMenu Build(string zrdrPath, string dataRoot)
    {
        var menu = new LaunchMenu { _zrdrPath = zrdrPath, _dataRoot = dataRoot, Layer = HudLayers.Board, Visible = false };

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        menu.AddChild(root);

        // Fully opaque backdrop so the empty 3D scene (procedural sky) never shows through.
        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f), MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        menu._center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        menu._center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(menu._center);

        menu._body = new VBoxContainer();
        menu._body.AddThemeConstantOverride("separation", 6);
        menu._center.AddChild(menu._body);

        // The splitscreen plane select lives alongside the centred layout; exactly one is visible.
        menu._paneRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        menu._paneRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(menu._paneRoot);

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
    /// their plane locks do not. <paramref name="startScreen"/>
    /// ("chapter"/"environment"/"missiontype"/"waves"/"wingmen"/"plane"/"loadout"/"wingmanloadout")
    /// opens on a later screen — a screenshot/verification aid (--menu=plane, --menu=loadout).</summary>
    public void ShowMenu(string startScreen = "")
    {
        _screen = startScreen switch
        {
            "chapter" => Screen.Chapter,
            "environment" => Screen.Environment,
            "missiontype" => Screen.MissionType,
            "waves" => Screen.Waves,
            "wingmen" => Screen.Wingmen,
            "plane" or "loadout" or "selected" => Screen.Plane,
            "wingmanloadout" => Screen.WingmanLoadout,
            _ => Screen.Mode,
        };
        // Environment/MissionType/Waves/Wingmen only exist under Instant Action — force it so a
        // --menu= opening straight onto one of them (a screenshot aid) renders the right
        // roster/filter rather than whatever _mode was last left at.
        if (_screen is Screen.Environment or Screen.MissionType or Screen.Waves or Screen.WaveEdit
            or Screen.Wingmen or Screen.WingmanLoadout)
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
        _error = "";
        // A flow never survives a trip through flight: it holds an unsaved scratch plane, and
        // resuming one after a session would be editing something nobody remembers starting.
        _hangar = null;
        Visible = true;
        if (_slots.Count == 0)
            _slots.Add(new Slot { Input = { Keyboard = true } });
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

        foreach (var slot in _slots)
            slot.Input.Poll((float)delta);
        dirty |= HandleInput();

        // Live hotplug: redraw when the join strip's text changes even if nothing was pressed.
        if (dirty || JoinStripText() != _stripText)
        {
            if (Visible) // a launch during HandleInput hides us; don't rebuild a dead menu
                Rebuild();
        }
    }

    // --- players / devices ---

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    // The eleven Planes display names, standing alone for Militias' Fortune Hunter row's "all
    // eleven" coverage — read off Planes rather than duplicated, so the two rosters cannot drift
    // apart. Callable from anywhere in the class regardless of where it sits textually (unlike a
    // field initializer, a method body only needs Planes assigned by the time it RUNS, and
    // Militias' own initializer — which calls this — runs after Planes' because Planes is
    // declared first).
    private static string[] PlaneNames()
    {
        var names = new string[Planes.Length];
        for (int i = 0; i < Planes.Length; i++)
            names[i] = Planes[i].Name;
        return names;
    }

    private static (string Name, string Code, bool DangerZones)[] ChaptersFor(MenuMode mode) =>
        mode == MenuMode.Stunt ? Array.FindAll(Chapters, c => c.DangerZones) : Chapters;

    private static (string Label, string Key)[] MissionTypeRowsFor(string chapterCode) =>
        DangerZonesFor(chapterCode) ? MissionTypes : Array.FindAll(MissionTypes, m => m.Key != "stunt_flying");

    // Whether a chapter's `ia.json` ships `dzones` — looked up from Chapters
    // by code so the Environment/MissionType screens and the plain Chapter screen cannot read two
    // different answers for the same chapter. Chapters is the eight-row table; every Environment
    // row's code is one of the seven that carry a DangerZones entry there (C1C, the omitted
    // chapter, is the only one that would not be).
    private static bool DangerZonesFor(string chapterCode)
    {
        foreach (var c in Chapters)
            if (c.Code == chapterCode)
                return c.DangerZones;
        return false;
    }

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

    // Start on an unclaimed pad joins a new player (up to the splitscreen rig's
    // capacity). Only on the Plane screen: player 1 sets the mode and the chapter first —
    // claiming its own pad in the process (ClaimP1Pad) — and everybody else joins
    // once the aircraft list is up. That ordering is what makes the gesture unambiguous; when
    // joining was allowed everywhere, Start on the pad player 1 was steering split it off as
    // player 2 and dumped player 1 back on the keyboard.
    private bool ScanJoins()
    {
        bool dirty = false;
        if (_screen != Screen.Plane)
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

            if (p1.Move != 0)
            {
                int n = CurrentCount();
                switch (_screen)
                {
                    case Screen.Mode: _modeIndex = Wrap(_modeIndex + p1.Move, n); break;
                    case Screen.Chapter: _chapterIndex = Wrap(_chapterIndex + p1.Move, n); break;
                    case Screen.Environment: _environmentIndex = Wrap(_environmentIndex + p1.Move, n); break;
                    case Screen.MissionType: _missionTypeIndex = Wrap(_missionTypeIndex + p1.Move, n); break;
                    case Screen.Waves: _waveListIndex = Wrap(_waveListIndex + p1.Move, n); break;
                    case Screen.WaveEdit: _waveFieldIndex = Wrap(_waveFieldIndex + p1.Move, n); break;
                    case Screen.Wingmen: _wingmenFieldIndex = Wrap(_wingmenFieldIndex + p1.Move, n); break;
                    case Screen.WingmanLoadout: _wingmanFitRow = Wrap(_wingmanFitRow + p1.Move, n); break;
                }
                dirty = true;
            }
            if (p1.MoveX != 0)
            {
                dirty |= HandleMoveX(p1.MoveX);
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
                    Quit?.Invoke();
                else
                    _screen = _screen switch
                    {
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
                slot.PlaneIndex = Wrap(slot.PlaneIndex + input.Move, i == 0 ? PlaneRowCount : Planes.Length);
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
            if (input.Accept && i == 0 && slot.PlaneIndex >= Planes.Length)
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
            return false; // the host has hidden us and is building
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
    // font, which is the same guard LayoutScale uses.
    private Vector2 FitColumns(List<FitRow> rows, int fontSize)
    {
        var font = _body.GetThemeDefaultFont();
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
            case Screen.Mode:
                // The trailing row is the hangar's top-level door, past the three modes.
                if (_modeIndex >= Modes.Length)
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

    // Opens the Build Custom Plane flow, remembering the screen to land back on. Both doors
    // (the Mode screen's trailing row and the Instant Action plane pick) come through here, so
    // there is one entry, one exit and one place the scratch plane lives.
    private void OpenHangar(Screen returnTo)
    {
        _hangarReturn = returnTo;
        _hangar = new HangarFlow(CustomPlaneStore.UserPlanes(), HangarStrings(), _dataRoot);
        _screen = Screen.Hangar;
        _error = "";
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
    // wrote nothing, so there is nothing to undo.
    private void CloseHangar(HangarFlow flow)
    {
        _screen = _hangarReturn;
        _hangar = null;
        _error = "";
        if (flow.Exit == HangarExit.Built && flow.BuiltPlaneName is { } name)
        {
            LastBuiltPlane = name;
            GD.Print($"launchscreen: hangar built \"{name}\" (the picker's auto-select is D31's)");
        }
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
    private bool AllConfirmed()
    {
        foreach (var slot in _slots)
            if (!slot.Confirmed)
                return false;
        return _slots.Count > 0;
    }

    // Whether the Plane screen's launch gesture is live right now. A lone Dogfight pilot
    // stays on this screen with JoinHint naming what it is waiting for.
    private bool CanLaunch() => CanLaunch(_mode, AllConfirmed(), _slots.Count);

    private void FireLaunch()
    {
        var choices = new List<PlayerChoice>(_slots.Count);
        foreach (var slot in _slots)
            choices.Add(new PlayerChoice(Planes[slot.PlaneIndex].Node,
                slot.Input.Pads ?? Array.Empty<int>(),
                slot.Fit.IsStock ? null : slot.Fit));

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
                // decides what any human actually flies.
                Planes[_slots[0].PlaneIndex].Name,
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
        // Leave our state as-is so a failed build can send us back with ShowMenu.
        Launch?.Invoke(chapter, choices, _mode, iaDef);
    }

    // --- rendering ---

    private void Rebuild()
    {
        // Several players choosing aircraft get a real split screen — one panel each, laid out by
        // SplitScreen.PaneRect, so you pick in the pane you will then fly in. Everything else (and
        // every single-player screen) keeps the centred layout untouched.
        bool split = _screen == Screen.Plane && _slots.Count > 1;
        _center.Visible = !split;
        _paneRoot.Visible = split;
        if (split)
        {
            // The hangar row exists only in the lone-pilot layout, so a pilot joining while
            // player 1 sits on it must not leave a cursor past the roster's end.
            foreach (var slot in _slots)
                slot.PlaneIndex = Math.Min(slot.PlaneIndex, Planes.Length - 1);
            RebuildPanes();
            return;
        }

        foreach (var c in _body.GetChildren())
            c.QueueFree();

        float s = LayoutScale();
        _body.CustomMinimumSize = new Vector2(560f * s, 0f);

        _body.AddChild(Label("CRIMSON SKIES", (int)(TitleFont * s), TitleColor, HorizontalAlignment.Center));
        _body.AddChild(Label(Breadcrumb(), (int)(CrumbFont * s), CrumbColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(8 * s)));
        _body.AddChild(JoinStrip(s));
        _body.AddChild(Spacer((int)(8 * s)));

        string heading = _screen switch
        {
            Screen.Mode => "SELECT MODE",
            Screen.Chapter => "SELECT MAP",
            Screen.Environment => "SELECT ENVIRONMENT",
            Screen.MissionType => "SELECT MISSION",
            Screen.Waves => "CONFIGURE WAVES",
            Screen.WaveEdit => $"WAVE {_waveEditIndex + 1}",
            Screen.Wingmen => "WINGMEN",
            Screen.WingmanLoadout => $"WINGMEN — AMMO SELECTION  ({Planes[_wingmanPlaneIndex].Name})",
            Screen.Hangar => _hangar?.Page.Title ?? HangarRow,
            _ when _slots.Count == 1 && _slots[0].InLoadout =>
                $"AMMO SELECTION  ({Planes[_slots[0].PlaneIndex].Name})",
            _ when _slots.Count == 1 && _slots[0].Locked => "AIRCRAFT SELECTED",
            _ => _slots.Count > 1 ? "SELECT AIRCRAFT — ALL PLAYERS" : "SELECT AIRCRAFT",
        };
        _body.AddChild(Label(heading, (int)(HeadingFont * s), HeadingColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(6 * s)));

        // C26's totals seam (PLAN-hangar Decision 10): every hangar screen carries the
        // persistent price/weight line off HangarFlow.TotalsLine, error-coloured when over.
        // This pair and its LayoutScale term are the whole rendering, like C22's art block.
        if (_screen == Screen.Hangar && _hangar is { } hangarFlow)
        {
            _body.AddChild(Label(hangarFlow.TotalsLine, (int)(DetailFont * s),
                hangarFlow.TotalsOverweight ? ErrorColor : DetailColor, HorizontalAlignment.Center));
            _body.AddChild(Spacer((int)(4 * s)));
        }

        int count = CurrentCount();
        for (int i = 0; i < count; i++)
            _body.AddChild(Row(i, s));

        _body.AddChild(Spacer((int)(10 * s)));
        _body.AddChild(DetailBlock(s));

        // C22's art seam: a hangar page may hand the shell one decoded TGA with a caption
        // (blueprint, icon, paint preview). This block and HangarArtControl are the whole
        // rendering; nothing else in the layout moves.
        if (_screen == Screen.Hangar && _hangar?.Page.Art is { } art)
        {
            _body.AddChild(Spacer((int)(6 * s)));
            _body.AddChild(HangarArtControl(art, s));
        }

        // The flown-wingmen re-clamp (decision 8a) only matters once players can actually join —
        // the Plane screen — and only under Instant Action with wingmen configured at all.
        if (_screen == Screen.Plane)
        {
            string wingmen = WingmenLine();
            if (wingmen.Length > 0)
            {
                _body.AddChild(Spacer((int)(4 * s)));
                _body.AddChild(Label(wingmen, (int)(DetailFont * s), DetailColor, HorizontalAlignment.Center));
            }

            // The lock is otherwise invisible in the centred layout, and an unacknowledged press
            // on a screen that used to launch on it reads as a freeze rather than as a stage.
            if (_slots.Count == 1 && _slots[0].Locked)
            {
                _body.AddChild(Spacer((int)(4 * s)));
                _body.AddChild(Label($"✓  {Planes[_slots[0].PlaneIndex].Name} selected",
                    (int)(DetailFont * s), RowLockedColor, HorizontalAlignment.Center));
            }
        }

        if (_error.Length > 0)
        {
            _body.AddChild(Spacer((int)(4 * s)));
            _body.AddChild(Label(_error, (int)(ErrorFont * s), ErrorColor, HorizontalAlignment.Center));
        }

        _body.AddChild(Spacer((int)(16 * s)));
        _body.AddChild(Label(Footer(), (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
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
        var font = _body.GetThemeDefaultFont();

        // Fit the roster + header + stats + status into the pane's height.
        float paneScale = s;
        if (font != null)
        {
            float refH = font.GetHeight(CrumbFont)                       // the player/device header
                       + Planes.Length * font.GetHeight(RowFont)         // the roster
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

            box.AddChild(Label(Planes[slot.PlaneIndex].Name, (int)(DetailFont * paneScale),
                DetailColor, HorizontalAlignment.Center));
            box.AddChild(Label("←→ change    B done", (int)(FooterFont * paneScale),
                FooterColor, HorizontalAlignment.Center));
            return box;
        }

        for (int i = 0; i < Planes.Length; i++)
        {
            bool sel = i == slot.PlaneIndex;
            box.AddChild(CursorRow.Build(Planes[i].Name, (int)(RowFont * paneScale),
                sel ? color : RowColor, sel));
        }

        box.AddChild(Label(PlaneStat(Planes[slot.PlaneIndex].Node), (int)(DetailFont * paneScale),
            DetailColor, HorizontalAlignment.Center));
        box.AddChild(Label(
            slot.Confirmed ? "✓  READY" : slot.Locked ? "A again to fly    Y weapons" : "choosing…",
            (int)(FooterFont * paneScale),
            slot.Locked ? color : FooterColor, HorizontalAlignment.Center));
        return box;
    }

    // 720p metrics scaled up on taller viewports, then capped so the tallest screen (a
    // four-player plane select) still fits the viewport instead of losing its footer. The
    // estimate rounds generously, so single-player screens are unaffected.
    private float LayoutScale()
    {
        // CanvasLayer is a Node (not a CanvasItem), so read the size off the Viewport directly.
        float viewH = GetViewport().GetVisibleRect().Size.Y;
        float s = Mathf.Max(1f, viewH / 720f);
        var font = _body.GetThemeDefaultFont();
        if (font == null)
            return s;
        int rows = CurrentCount();
        // Match Rebuild's conditional Instant Action row, or a wingman-heavy launch overflows 720p.
        // It is two more VBox children (the spacer and the label itself), which the separation
        // term below must also grow by, not just the row height sum.
        bool wingmenLine = _screen == Screen.Plane && WingmenLine().Length > 0;
        // The selected-aircraft line is a second conditional pair on the same screen, so it is
        // counted the same way — a wingman-heavy locked launch adds both at once.
        bool lockedLine = _screen == Screen.Plane && _slots.Count == 1 && _slots[0].Locked;
        // The hangar art block (C22's seam) is a third conditional pair, counted the same way.
        bool artBlock = _screen == Screen.Hangar && _hangar?.Page.Art != null;
        // The hangar totals line (C26's seam, Decision 10) is a fourth, on every hangar screen.
        bool totalsLine = _screen == Screen.Hangar && _hangar != null;
        int extraChildren = (wingmenLine ? 2 : 0) + (lockedLine ? 2 : 0) + (artBlock ? 2 : 0) +
            (totalsLine ? 2 : 0);
        float refH =
            font.GetHeight(TitleFont) + font.GetHeight(CrumbFont) + font.GetHeight(FooterFont) +
            font.GetHeight(HeadingFont) + rows * font.GetHeight(RowFont) +
            font.GetHeight(DetailFont) + font.GetHeight(FooterFont) +
            (wingmenLine ? font.GetHeight(DetailFont) + 4 : 0) +
            (lockedLine ? font.GetHeight(DetailFont) + 4 : 0) +
            (artBlock ? HangarArtHeight + font.GetHeight(FooterFont) + 6 : 0) +
            (totalsLine ? font.GetHeight(DetailFont) + 4 : 0) +
            (_error.Length > 0 ? font.GetHeight(ErrorFont) + 4 : 0) +
            8 + 8 + 6 + 10 + 16 +          // the explicit spacers Rebuild adds
            6 * (10 + rows + extraChildren); // the body VBox's separation between children
        return Mathf.Min(s, viewH / refH);
    }

    // The stock fit behind a roster row, or null when the table has no def flying that model.
    private LoadoutDef? StockFitFor(int planeIndex) => Fits.ForModel(Planes[planeIndex].Node);

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
        Screen.Mode => Modes.Length + 1, // + the trailing hangar row
        Screen.Hangar => _hangar?.Page.RowCount ?? 1,
        Screen.Chapter => CurrentChapters.Length,
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

        string text = _screen switch
        {
            Screen.Mode => index < Modes.Length ? Modes[index].Label : HangarRow,
            Screen.Hangar => _hangar?.Page.RowText(index) ?? "",
            Screen.Chapter => CurrentChapters[index].Name,
            Screen.Environment => Environments[index].Name,
            Screen.MissionType => CurrentMissionTypes[index].Label,
            Screen.Waves => WaveListRowText(index),
            Screen.WaveEdit => WaveFieldRowText(index),
            Screen.Wingmen => WingmenFieldRowText(index),
            _ => index < Planes.Length ? Planes[index].Name : HangarRow,
        };
        bool sel = index == CurrentIndex;
        // A locked single-player pick recolours its row, because the centred layout has no
        // per-pane status line to carry the state the way the splitscreen panes do.
        bool locked = _screen == Screen.Plane && _slots.Count == 1 && _slots[0].Locked;
        var colour = sel ? locked ? RowLockedColor : RowFocusColor : RowColor;
        return CursorRow.Build(text, (int)(RowFont * s), colour, sel);
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

    // The detail area: one stats line for the focused entry.
    private Control DetailBlock(float s) =>
        Label(Detail(CurrentIndex), (int)(DetailFont * s), DetailColor, HorizontalAlignment.Center);

    // The one art block a hangar page may request (C22's seam): the page's decoded RGBA as a
    // texture, letterboxed to a fixed height, its caption under it. The texture is rebuilt only
    // when the page hands over a different image (_hangarArtSource).
    private Control HangarArtControl(HangarArt art, float s)
    {
        if (!ReferenceEquals(_hangarArtSource, art.Image))
        {
            var image = Image.CreateFromData(art.Image.Width, art.Image.Height, false,
                Image.Format.Rgba8, art.Image.Rgba);
            _hangarArtTexture = ImageTexture.CreateFromImage(image);
            _hangarArtSource = art.Image;
        }

        var box = new VBoxContainer();
        var rect = new TextureRect
        {
            Texture = _hangarArtTexture,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(0, HangarArtHeight * s),
        };
        rect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(rect);
        box.AddChild(Label(art.Caption, (int)(FooterFont * s), DetailColor, HorizontalAlignment.Center));
        return box;
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

        if (_screen == Screen.Hangar)
        {
            return "↑↓  Choose       ←→  Change       Enter / A  Continue       Esc / B  Back";
        }

        string back = _screen == Screen.Mode ? "Esc / B  Quit" : "Esc / B  Back";
        string who = _slots.Count > 1 ? "       (P1 chooses)" : "";
        string nav = _screen switch
        {
            Screen.MissionType => "↑↓  Choose mission       ←→  Lives",
            Screen.WaveEdit or Screen.Wingmen => "↑↓  Choose field       ←→  Change",
            _ => "↑↓  Navigate",
        };
        // The loadout is an unbound face button, so it is invisible unless the footer says so.
        // Named only where it does something: a lone pilot at aircraft select, and the wingmen
        // step once there are wingmen to arm.
        string fit = _screen == Screen.Plane || (_screen == Screen.Wingmen && _numWingmen > 0)
            ? "       L / Y  Weapons"
            : "";
        // Name the press that is actually next. Before the lock that is "select"; after it, "fly"
        // — a footer still offering "select" on an already-selected plane is why the second press
        // was not obvious in the first place.
        string select = _screen != Screen.Plane ? "Enter / A  Select"
            : _slots.Count == 1 && _slots[0].Locked ? "Enter / A  FLY"
            : "Enter / A  Select";
        return $"{nav}       {select}{fit}       {back}{who}";
    }

    private string Breadcrumb()
    {
        string mode = Modes[(int)_mode].Label;
        return _screen switch
        {
            Screen.Mode => "Mode  ›  Map  ›  Aircraft",
            Screen.Hangar => $"{HangarRow}  ›  {_hangar?.Page.Title}",
            Screen.Chapter => $"{mode}  ›  Map  ›  Aircraft",
            Screen.Environment => $"{mode}  ›  Environment  ›  Mission  ›  Aircraft",
            Screen.MissionType => $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  Mission  ›  Aircraft",
            Screen.Waves or Screen.WaveEdit =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Waves  ›  Aircraft",
            Screen.Wingmen or Screen.WingmanLoadout =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Wingmen  ›  Aircraft",
            _ when _mode == MenuMode.Stunt =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Aircraft",
            _ => $"{mode}  ›  {CurrentChapters[_chapterIndex].Name}  ›  Aircraft",
        };
    }

    private string Detail(int focus) => _screen switch
    {
        Screen.Mode => focus < Modes.Length ? Modes[focus].Detail : "Build a plane in the hangar and fly it.",
        Screen.Hangar => _hangar?.Page.Detail(focus) ?? "",
        Screen.Chapter => $"Region {CurrentChapters[focus].Code}",
        Screen.Environment => $"Region {Environments[focus].Code}",
        Screen.MissionType => LivesDetail(),
        Screen.Waves => focus == _waves.Length ? "Enter / A  on to the wingmen" : "Enter / A  edit a wave",
        Screen.WaveEdit or Screen.Wingmen => "←→  change",
        // Blank: the footer already names the steppers, and a second copy of "←→ change" directly
        // over it reads as two different controls rather than one.
        _ when CentredFitRows() != null => "",
        _ => focus < Planes.Length
            ? PlaneStat(Planes[focus].Node)
            : "Build a plane in the hangar and fly it.",
    };

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

    /// <summary>One player's confirmed selection: the plane node to build and the gamepad(s) that
    /// fly it. A joined player has exactly one; player 1 (who also has the keyboard) carries every
    /// pad nobody claimed, so a lone controller still flies it and phantom devices stay harmless.</summary>
    /// <summary><paramref name="Fit"/> is this pane's own Ammo Selection edits, or null for the
    /// airframe's stock fit. Per pane, because the roster above it is.</summary>
    public readonly record struct PlayerChoice(string PlaneNode, int[] Pads, LoadoutChoice? Fit = null);

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
    // whether they have locked their pick.
    private sealed class Slot
    {
        public readonly MenuInput Input = new();
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
    }
}
