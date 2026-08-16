using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The in-game launchscreen: a keyboard/controller-driven menu shown
/// when the viewer is launched with no content-selecting CLI arg (a bare launch, e.g.
/// RunGame.ps1). <b>Mode</b> (Free Flight / Instant Action / Dogfight) branches two ways
/// two ways. Free Flight and Dogfight go straight to <b>Chapter</b>
/// (the eight chapter worlds, unchanged) → <b>Plane</b>. Instant Action instead opens its own
/// five-step wizard: <b>Environment</b> (the seven decoded Instant Action environments,
/// each naming one chapter) → <b>MissionType</b> (the four mission types that environment's
/// `disallow_missions` allows, with the lives stepper beside them) → <b>Waves</b> (up to four,
/// starting empty — a deliberate presentation divergence from the original's always-four
/// dropdowns, decision 1) → <b>Wingmen</b> (0-5, aircraft hidden at 0) → <b>Plane</b>, shared with
/// the other two modes. Dogfighting an Ace skips straight from MissionType to Plane — the ace duel
/// takes no wave or wingman configuration, matching the original's own screen ("dogfighting an
/// ace takes no wave configuration"). <see cref="Launch"/> fires with the chosen chapter, the
/// per-player plane + pad, the picked <see cref="MenuMode"/>, and — Instant Action only — the
/// wizard's own built <c>InstantActionDef</c> (<see cref="FireLaunch"/>,
/// <see cref="Mech3.InstantAction.BuildFromWizard"/>): GameSession builds one
/// <c>InstantActionRuntime</c> from that value exactly the way <c>--ia=</c> does, which is what
/// "one build path from wizard and CLI" (H16's own goal) means in code.
///
/// <para><b>Dogfight needs a fight.</b> Its Plane screen withholds the launch gesture until at
/// least two players have joined, even once everyone present is locked — see
/// <see cref="CanLaunch"/> and the hint line <see cref="JoinHint"/> shows while it is withheld.
/// Free Flight and Instant Action still launch solo exactly as before.</para>
///
/// <para><b>Join flow.</b> Two phases, in this order. First player 1 — the keyboard plus
/// every pad nobody else holds — picks the mode and the chapter, and the pad it actually steers
/// those screens with is <b>claimed</b> for player 1 (driving with the keyboard claims nothing,
/// which leaves every pad free and is exactly the keyboard-versus-controllers setup). Then, on the
/// <b>Plane</b> screen, any still-free pad joins as its own player by pressing Start, up to
/// <see cref="SplitScreen.MaxPlayers"/>. Ordering it that way is what makes Start unambiguous: it
/// can only ever mean "a new player", never "steal the pad player 1 is holding". The join strip
/// under the breadcrumb shows who is in on every screen. Each player then locks their pick with A
/// — <b>duplicates are allowed</b>, nothing reserves an aircraft — and the flight starts when
/// everyone is locked. B unlocks; B while unlocked leaves the session (player 1 goes back to the
/// screen that fed the Plane screen this launch — Chapter, or Instant Action's MissionType —
/// which unlocks everyone). A pad that disconnects drops its player
/// (player 1 just loses its pad and keeps the keyboard).</para>
///
/// <para><b>The aircraft screen splits</b> once more than one player has joined: instead of one
/// list with several cursors on it, each player gets their own panel — laid out by the very same
/// <see cref="SplitScreen.PaneRect"/> the flight panes use, so you choose in the pane you will
/// then fly in, in your own colour, with your own roster position, stats and lock state. The
/// shared breadcrumb and join hint move to a strip along the bottom. One player keeps the plain
/// centred layout, which is why a single-player launchscreen is pixel-identical to the
/// pre-splitscreen one.</para>
///
/// <para><b>Input</b> is polled per player every frame through <see cref="MenuInput"/> rather
/// than Godot's input map / focus system: it needs no project-settings wiring, behaves identically
/// for keyboard and pad, and — the reason the join flow needs it — reads a <i>named device</i>,
/// which actions cannot. Edge detection + auto-repeat live in MenuInput.</para>
///
/// <para>Re-entrant: the Launcher frees the session and calls <see cref="ShowMenu"/> again on
/// Esc-from-flight, so this resets to the Mode screen, clears the plane locks (joined players
/// stay joined) and re-primes every input edge — a held Esc that returned here must not
/// immediately re-trigger Back, and a held Start must not re-join anyone.</para>
/// </summary>
public sealed partial class LaunchMenu : CanvasLayer
{
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
    // Splitscreen plane select (several players): the bottom strip that keeps the breadcrumb +
    // join hint out of the panes, as a fraction of viewport height, and the pane's inner padding.
    // Reference values at 720p. Confirmed at the controls (BL-126, 2026-08-15): join/lock feel
    // reads right at 2P and 4P, no retune owed.
    private const float StripHeightFrac = 0.12f;
    private const int PanePad = 10;
    // The lives stepper's range (Screen.MissionType, decision 15/18): 0 = unlimited, 1 = the
    // faithful one-life run (default), up to this cap. INVENTED — ia.json carries no such field, so
    // there is no decoded range to match; TUNE.
    private const int MaxLives = 9;

    // The three top-level modes, in MenuMode's ordinal order (Free/Stunt/Versus) so the row index
    // doubles as the enum value with no separate lookup. Row 1 reads "Instant Action":
    // Stunt Flying is no longer offered here on its own — it is one of the
    // four Instant Action mission types (Screen.MissionType, below), reachable only where the
    // picked environment's chapter carries dzones. The MenuMode enum value stays named Stunt
    // (SessionSpec.cs, out of this item's file-contention scope) — only the label changes; picking
    // this row still opens the Environment screen rather than the plain Chapter one.
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

    // The four Instant Action mission types, in the UI dropdown's own order (`IDS_IA_MISSIONTYPE`,
    // 3660) — NOT the internal id order (`docs/formats/instant-action.md` "Mission types have
    // internal ids"). Key is the ia.json `mission_type` string every consumer (InstantActionDef,
    // --scenario=) already uses. Every environment offers all four except that Stunt Flying is
    // hidden where the chapter's own `disallow_missions` bars it (decoded: only C2B among the seven
    // offered here) — CurrentMissionTypes applies that filter; this table is the unfiltered set.
    private static readonly (string Label, string Key)[] MissionTypes =
    {
        ("Dogfighting an Ace", "dogfight_ace"),
        ("Dogfighting a Squadron", "dogfight_squadron"),
        ("Stunt Flying", "stunt_flying"),
        ("Attacking a Zeppelin", "zeppelin_run"),
    };

    // The eight chapter worlds (mirrors RunDev.ps1's roster: display name + extracted folder code).
    // The lettered codes are separate terrain databases, not lighting variants of one map: C1/C1B/C1C
    // all sit in the campaign's Sea Haven region but host different story missions over different
    // ground, with disjoint landmarks and Danger Zones (same for C2/C2B). Names follow the original's
    // instant-action environment menu (crimson.exe maps env 0-6 to c1, c2b, c3, c5, c1b, c4, c2; C1C
    // is not selectable there — campaign/MP only). DangerZones marks the chapters whose ia.json has
    // a dzones list; C1C/C2B have none, so ChaptersFor hides them from Stunt Flying (the original
    // hides "the clouds" there too). A stunt run forced onto them via CLI still falls back to free
    // flight (logged by StuntMission).
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

    // The thirteen Instant Action militias (`IDS_IA_MILITIAS`, 3670) and the aircraft each one
    // actually flies — the wave editor's own Militia/Aircraft fields. Aircraft coverage is
    // the `.BM` pattern reading decision 7 settles on (docs/formats/instant-action.md "The
    // thirteen militias and their aircraft"), NOT vehicle.json's paint_pattern defs — under that
    // reading Fortune Hunter would cover three planes instead of all eleven, and Sacred Trust would
    // lose the Warhawk. Names use ia.json's own singular vocabulary ("Autogyro", never the UI
    // plural list's "Hoplite" — A1's two-vocabulary trap), matching what InstantActionWave.EnemyPlane
    // and PlaneNodeFor both key on. Trap (c): a militia is never filtered out here, Fortune Hunter
    // included, for being the player's own side.
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
    private MenuMode _mode;
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

    private enum Screen { Mode, Chapter, Environment, MissionType, Waves, WaveEdit, Wingmen, Plane }

    /// <summary>The chapter roster the picked mode offers — the Chapter screen and everything
    /// downstream (breadcrumb, launch) index into this, never the full list. Free Flight/Dogfight
    /// only; Instant Action uses <see cref="Environments"/>/<see cref="CurrentMissionTypes"/>
    /// instead (its own environment list is decoded, not this table's alphabetic one).</summary>
    private (string Name, string Code, bool DangerZones)[] CurrentChapters => ChaptersFor(_mode);

    /// <summary>The mission types the picked environment's chapter actually offers: all four,
    /// minus Stunt Flying where that chapter's own `disallow_missions` bars it (decoded: only "the
    /// clouds" among the seven Instant Action environments — <see cref="DangerZonesFor"/>).</summary>
    private (string Label, string Key)[] CurrentMissionTypes => MissionTypeRowsFor(Environments[_environmentIndex].Code);

    /// <summary>The single-player cursor position on the current screen (the plane screen reads
    /// player 1's cursor).</summary>
    private int CurrentIndex => _screen switch
    {
        Screen.Mode => _modeIndex,
        Screen.Chapter => _chapterIndex,
        Screen.Environment => _environmentIndex,
        Screen.MissionType => _missionTypeIndex,
        Screen.Waves => _waveListIndex,
        Screen.WaveEdit => _waveFieldIndex,
        Screen.Wingmen => _wingmenFieldIndex,
        _ => _slots[0].PlaneIndex,
    };

    /// <summary>How many rows the Wingmen screen shows right now: the Aircraft field is hidden at
    /// 0 wingmen, matching the decoded setup screen's own behaviour.</summary>
    private int WingmenRowCount => _numWingmen > 0 ? 2 : 1;

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

    /// <summary>The Instant Action MissionType screen's roster for one environment's chapter, as
    /// `ia.json` `mission_type` keys, in the UI dropdown's own order: every environment offers all
    /// four except that Stunt Flying is dropped where the chapter's `disallow_missions` bars it
    /// (decoded: only C2B, "the clouds", among the seven offered environments — the same rule
    /// <see cref="ChapterCodesFor"/> already applies via <see cref="DangerZonesFor"/>, read once
    /// here instead of duplicated). Static + public so the filter is testable without a menu
    /// instance.</summary>
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

    /// <summary>The wave editor's own build step: one wizard wave slot to the
    /// <see cref="InstantActionWave"/> value <see cref="Mech3.InstantAction.BuildFromWizard"/>
    /// stores — <see cref="InstantAction.EmptyWave"/> when unconfigured (<paramref name="count"/>
    /// 0), so an unconfigured slot and a JSON file's own omitted <c>groupN</c> are byte-identical
    /// regardless of whatever the militia/aircraft/skill cursors happen to be sitting on (they are
    /// not "configured" until a pilot actually raises the count). <c>EnemyName</c> is a plain
    /// "<i>militia</i> <i>aircraft</i>" label, not an attempt at the original's own undecoded
    /// <c>MSG_*</c> construction — nothing downstream reads <c>EnemyName</c> (the field's own doc
    /// comment), so there is nothing to get wrong by not guessing it. Static + public so the wave
    /// editor's own build rule is testable without a menu instance.</summary>
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
    /// ("chapter"/"environment"/"missiontype"/"waves"/"wingmen"/"plane") opens on a later screen —
    /// a screenshot/verification aid (--menu=plane, --menu=missiontype).</summary>
    public void ShowMenu(string startScreen = "")
    {
        _screen = startScreen switch
        {
            "chapter" => Screen.Chapter,
            "environment" => Screen.Environment,
            "missiontype" => Screen.MissionType,
            "waves" => Screen.Waves,
            "wingmen" => Screen.Wingmen,
            "plane" => Screen.Plane,
            _ => Screen.Mode,
        };
        // Environment/MissionType/Waves/Wingmen only exist under Instant Action — force it so a
        // --menu= opening straight onto one of them (a screenshot aid) renders the right
        // roster/filter rather than whatever _mode was last left at.
        if (_screen is Screen.Environment or Screen.MissionType or Screen.Waves or Screen.WaveEdit or Screen.Wingmen)
        {
            _modeIndex = (int)MenuMode.Stunt;
            _mode = MenuMode.Stunt;
        }
        _error = "";
        Visible = true;
        if (_slots.Count == 0)
            _slots.Add(new Slot { Input = { Keyboard = true } });
        foreach (var slot in _slots)
        {
            slot.Locked = false;
            slot.Input.Prime();
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

    /// <summary>Whether a chapter's `ia.json` ships `dzones` — looked up from <see cref="Chapters"/>
    /// by code so the Environment/MissionType screens and the plain Chapter screen cannot read two
    /// different answers for the same chapter. Chapters is the eight-row table; every Environment
    /// row's code is one of the seven that carry a DangerZones entry there (C1C, the omitted
    /// chapter, is the only one that would not be).</summary>
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

    /// <summary>Whether a pad already belongs to a player: one of players 2–4, or the pad player 1
    /// claimed on the Mode/Chapter screens (<see cref="_p1Pad"/>). Before that claim, player 1's
    /// pads are only borrowed — it reads every free device, so any of them can still join.</summary>
    private bool IsClaimed(int pad)
    {
        if (pad == _p1Pad)
            return true;
        for (int i = 1; i < _slots.Count; i++)
            if (_slots[i].Input.Pad == pad)
                return true;
        return false;
    }

    /// <summary>Reconciles the joined players with the live pad roster: drops a player whose pad
    /// disconnected, then hands player 1 <b>every unclaimed pad</b>. That last part is the
    /// important one — player 1 reading the whole leftover roster rather than <c>pads[0]</c> is
    /// what keeps the phantom-device fix alive (see <see cref="MenuInput.Pads"/>), and
    /// it falls out for free that a pad joining as its own player leaves player 1's set and
    /// rejoins it on un-join. Returns true when anything changed (the strip needs redrawing).</summary>
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
        if (free.Count != p1.Pads.Length)
        {
            p1.Pads = free.ToArray();
            p1.Prime(); // a button still held on a pad that just changed hands is not a press
            dirty = true;
        }
        else
        {
            for (int i = 0; i < free.Count; i++)
                if (free[i] != p1.Pads[i])
                {
                    p1.Pads = free.ToArray();
                    p1.Prime();
                    dirty = true;
                    break;
                }
        }
        return dirty;
    }

    /// <summary>Seeds the per-pad join edges from the current state, so a Start held while the
    /// menu appears does not immediately join a player.</summary>
    private void PrimeJoins()
    {
        _joinPrev.Clear();
        foreach (int pad in Pads.Connected())
            _joinPrev[pad] = MenuInput.JoinPressed(pad);
    }

    /// <summary>Start on an unclaimed pad joins a new player (up to the splitscreen rig's
    /// capacity). <b>Only on the Plane screen:</b> player 1 sets the mode and the chapter first —
    /// claiming its own pad in the process (<see cref="ClaimP1Pad"/>) — and everybody else joins
    /// once the aircraft list is up. That ordering is what makes the gesture unambiguous; when
    /// joining was allowed everywhere, Start on the pad player 1 was steering split it off as
    /// player 2 and dumped player 1 back on the keyboard.</summary>
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

    /// <summary>Pins player 1 to whichever pad it is actually steering the Mode/Chapter screens
    /// with ("logging in" that controller). Called only from those screens, so by the time the
    /// aircraft list appears player 1's device is settled and every other pad is unambiguously a
    /// joiner. Player 1 driving with the keyboard claims nothing — then all pads stay free, which
    /// is exactly the keyboard-versus-controllers setup.</summary>
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

    /// <summary>Reads this frame's polled intents and applies them. Mode/Chapter are player 1's
    /// alone (the others can only leave); the Plane screen runs every player's cursor at once and
    /// fires <see cref="Launch"/> when they are all locked. Returns true if the view changed.</summary>
    private bool HandleInput()
    {
        bool dirty = false;
        if (_screen != Screen.Plane)
        {
            var p1 = _slots[0].Input;
            dirty |= ClaimP1Pad();
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
                }
                dirty = true;
            }
            if (p1.MoveX != 0)
            {
                dirty |= HandleMoveX(p1.MoveX);
            }
            if (p1.Accept)
            {
                _error = "";
                HandleAccept();
                dirty = true;
            }
            else if (p1.Back)
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
            if (input.Move != 0 && !slot.Locked)
            {
                slot.PlaneIndex = Wrap(slot.PlaneIndex + input.Move, Planes.Length);
                dirty = true;
            }
            if (input.Accept && !slot.Locked)
            {
                slot.Locked = true;
                _error = "";
                dirty = true;
            }
            else if (input.Back)
            {
                if (slot.Locked)
                {
                    slot.Locked = false;
                }
                else if (i == 0)
                {
                    // Player 1 backing out returns everyone to whichever screen fed the Plane
                    // screen this time: the plain Chapter screen for Free Flight/Dogfight,
                    // MissionType for Instant Action's ace duel (it skipped Waves/Wingmen on the
                    // way in, so it skips them on the way back out too), else Wingmen.
                    _screen = _mode != MenuMode.Stunt ? Screen.Chapter
                        : CurrentMissionTypes[_missionTypeIndex].Key == "dogfight_ace" ? Screen.MissionType
                        : Screen.Wingmen;
                    foreach (var s in _slots)
                        s.Locked = false;
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

    /// <summary>The horizontal axis's effect, screen by screen — always a live-editing stepper on
    /// whichever field the vertical cursor is focused on, never a "select and lock" gesture (that
    /// is what Accept is for). Split out of <see cref="HandleInput"/> because it now has one branch
    /// per wizard screen that carries a stepper: the mission choice's lives, and the wave
    /// editor's four fields plus the wingman count/aircraft.</summary>
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
                    _wingmanPlaneIndex = Wrap(_wingmanPlaneIndex + dir, Planes.Length);
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>The Accept gesture's effect, screen by screen — split out of
    /// <see cref="HandleInput"/> for the same reason <see cref="HandleMoveX"/> was: one branch per
    /// wizard screen now, most of them advancing the wizard rather than picking a row.</summary>
    private void HandleAccept()
    {
        switch (_screen)
        {
            case Screen.Mode:
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
                    _waveListIndex = 0;
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

    /// <summary>The chosen Instant Action environment's own shipped ia.zrd.json, for the fields the
    /// wizard has no control to edit (the ace, the zeppelin node names, disallow_missions) — should
    /// not fail for any of the seven environments Instant Action offers; falls back to the built-in
    /// defaults exactly like a bad --ia= file does rather than leaving the menu unable to proceed.</summary>
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

    private bool AllLocked()
    {
        foreach (var slot in _slots)
            if (!slot.Locked)
                return false;
        return _slots.Count > 0;
    }

    /// <summary>Whether the Plane screen's launch gesture is live right now. A lone Dogfight pilot
    /// stays on this screen with <see cref="JoinHint"/> naming what it is waiting for.</summary>
    private bool CanLaunch() => CanLaunch(_mode, AllLocked(), _slots.Count);

    private void FireLaunch()
    {
        var choices = new List<PlayerChoice>(_slots.Count);
        foreach (var slot in _slots)
            choices.Add(new PlayerChoice(Planes[slot.PlaneIndex].Node, slot.Input.Pads));

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
                // Player 1's own pick — in splitscreen there is no single "the" player plane, and
                // this is the same nominal choice the def's PlayerPlane already means: a
                // sanity-checked label (GameSession warns if it does not resolve), not what any
                // human actually flies (--plane=/the roster owns that regardless of this value).
                Planes[_slots[0].PlaneIndex].Name,
                _numWingmen,
                Planes[_wingmanPlaneIndex].Name,
                waves,
                _lives);
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
            _ => _slots.Count > 1 ? "SELECT AIRCRAFT — ALL PLAYERS" : "SELECT AIRCRAFT",
        };
        _body.AddChild(Label(heading, (int)(HeadingFont * s), HeadingColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(6 * s)));

        int count = CurrentCount();
        for (int i = 0; i < count; i++)
            _body.AddChild(Row(i, s));

        _body.AddChild(Spacer((int)(10 * s)));
        _body.AddChild(DetailBlock(s));

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
        }

        if (_error.Length > 0)
        {
            _body.AddChild(Spacer((int)(4 * s)));
            _body.AddChild(Label(_error, (int)(ErrorFont * s), ErrorColor, HorizontalAlignment.Center));
        }

        _body.AddChild(Spacer((int)(16 * s)));
        _body.AddChild(Label(Footer(), (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
    }

    /// <summary>The splitscreen aircraft select: one panel per player in that player's pane of the
    /// screen (the same <see cref="SplitScreen.PaneRect"/> geometry the flight panes use), plus a
    /// shared bottom strip carrying the breadcrumb, the join strip and the controls line. Each
    /// panel shows the player's tag + device, the full aircraft roster with their own cursor, the
    /// focused plane's stats, and their lock state — the panel border lights up in the player's
    /// colour once locked, which is the at-a-glance "who are we waiting for".</summary>
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
        strip.AddChild(Label("↑↓  Choose       Enter / A  Lock in       Esc / B  Unlock  ·  leave",
            (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
        if (_error.Length > 0)
            strip.AddChild(Label(_error, (int)(ErrorFont * s), ErrorColor, HorizontalAlignment.Center));
        _paneRoot.AddChild(strip);
    }

    /// <summary>One player's panel contents. The roster is the full list — it fits, because the
    /// font scale is derived from the pane's own height rather than the window's (a 4P quarter
    /// pane and a 2P half pane are the same height, so both land on the same size).</summary>
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

        for (int i = 0; i < Planes.Length; i++)
        {
            bool sel = i == slot.PlaneIndex;
            box.AddChild(Label((sel ? "▶  " : "     ") + Planes[i].Name, (int)(RowFont * paneScale),
                sel ? color : RowColor, HorizontalAlignment.Center));
        }

        box.AddChild(Label(PlaneStat(Planes[slot.PlaneIndex].Node), (int)(DetailFont * paneScale),
            DetailColor, HorizontalAlignment.Center));
        box.AddChild(Label(slot.Locked ? "✓  LOCKED IN" : "choosing…", (int)(FooterFont * paneScale),
            slot.Locked ? color : FooterColor, HorizontalAlignment.Center));
        return box;
    }

    /// <summary>How large to draw this screen: the item-4 rule (720p metrics, scaled up on taller
    /// viewports) capped so the screen's actual content still fits the viewport. The cap matters
    /// because the screens are not all the same height — a four-player plane select adds a cursor
    /// row per player to the tallest list there is, and without it the footer fell off a 720p
    /// window. The estimate uses the real font line heights, and rounds generously (the spacers
    /// are absolute pixels but counted as reference units), so it errs toward a small margin
    /// rather than an overflow. Single-player screens fit at the uncapped scale, so their layout
    /// is unchanged.</summary>
    private float LayoutScale()
    {
        // CanvasLayer is a Node (not a CanvasItem), so read the size off the Viewport directly.
        float viewH = GetViewport().GetVisibleRect().Size.Y;
        float s = Mathf.Max(1f, viewH / 720f);
        var font = _body.GetThemeDefaultFont();
        if (font == null)
            return s;
        int rows = CurrentCount();
        // The Plane screen's own optional Instant Action line (decision 8a's flown-wingmen
        // re-clamp) adds a row + spacer Rebuild only draws conditionally — counted here on the
        // same condition, or a wingman-heavy wizard launch overflows 720p with nothing to show it.
        // It is two more VBox children (the spacer and the label itself), which the separation
        // term below must also grow by, not just the row height sum.
        bool wingmenLine = _screen == Screen.Plane && WingmenLine().Length > 0;
        int extraChildren = wingmenLine ? 2 : 0;
        float refH =
            font.GetHeight(TitleFont) + font.GetHeight(CrumbFont) + font.GetHeight(FooterFont) +
            font.GetHeight(HeadingFont) + rows * font.GetHeight(RowFont) +
            font.GetHeight(DetailFont) + font.GetHeight(FooterFont) +
            (wingmenLine ? font.GetHeight(DetailFont) + 4 : 0) +
            (_error.Length > 0 ? font.GetHeight(ErrorFont) + 4 : 0) +
            8 + 8 + 6 + 10 + 16 +          // the explicit spacers Rebuild adds
            6 * (10 + rows + extraChildren); // the body VBox's separation between children
        return Mathf.Min(s, viewH / refH);
    }

    private int CurrentCount() => _screen switch
    {
        Screen.Mode => Modes.Length,
        Screen.Chapter => CurrentChapters.Length,
        Screen.Environment => Environments.Length,
        Screen.MissionType => CurrentMissionTypes.Length,
        Screen.Waves => _waves.Length + 1, // + the trailing "Continue" row
        Screen.WaveEdit => 4, // Enemies / Militia / Aircraft / Skill
        Screen.Wingmen => WingmenRowCount,
        _ => Planes.Length,
    };

    /// <summary>One centred list row with a ▶ cursor — the item-4 layout, used by every screen
    /// the centred body draws. A multi-player aircraft screen never comes through here: it splits
    /// into per-player panes instead (<see cref="RebuildPanes"/>).</summary>
    private Control Row(int index, float s)
    {
        string text = _screen switch
        {
            Screen.Mode => Modes[index].Label,
            Screen.Chapter => CurrentChapters[index].Name,
            Screen.Environment => Environments[index].Name,
            Screen.MissionType => CurrentMissionTypes[index].Label,
            Screen.Waves => WaveListRowText(index),
            Screen.WaveEdit => WaveFieldRowText(index),
            Screen.Wingmen => WingmenFieldRowText(index),
            _ => Planes[index].Name,
        };
        bool sel = index == CurrentIndex;
        return Label((sel ? "▶  " : "     ") + text, (int)(RowFont * s),
            sel ? RowFocusColor : RowColor, HorizontalAlignment.Center);
    }

    /// <summary>One Waves-screen row: an unconfigured slot reads "empty" (decision 1's own "starts
    /// empty" wizard, not the original's always-four dropdowns), a configured one summarises its
    /// count/militia/aircraft/skill, and the trailing row advances to Wingmen.</summary>
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

    /// <summary>One WaveEdit-screen field row: the label plus the field's own current value, since
    /// this screen has no separate detail area — <see cref="HandleMoveX"/> edits whichever one the
    /// cursor sits on.</summary>
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

    /// <summary>One Wingmen-screen field row — the Aircraft row (index 1) only ever draws while
    /// it exists (<see cref="WingmenRowCount"/> is 1 at 0 wingmen), matching the decoded setup
    /// screen's own hidden-at-zero control.</summary>
    private string WingmenFieldRowText(int index) => index switch
    {
        0 => $"Wingmen         {_numWingmen}",
        _ => $"Aircraft        {Planes[_wingmanPlaneIndex].Name}",
    };

    /// <summary>The detail area: one stats line for the focused entry.</summary>
    private Control DetailBlock(float s) =>
        Label(Detail(CurrentIndex), (int)(DetailFont * s), DetailColor, HorizontalAlignment.Center);

    /// <summary>The join strip shown under the breadcrumb on every screen: who is in, on what
    /// device, plus the hint that free pads can join with Start.</summary>
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

    /// <summary>The strip as plain text — compared each frame so a hotplug (or a join) redraws
    /// even when nothing was pressed.</summary>
    private string JoinStripText()
    {
        var parts = new List<string>(_slots.Count + 1);
        for (int i = 0; i < _slots.Count; i++)
            parts.Add($"{SplitScreen.PlayerTag(i)} {_slots[i].Input.DeviceLabel}");
        parts.Add(JoinHint());
        return string.Join(" | ", parts);
    }

    /// <summary>The hint beside the join strip. Joining only happens on the aircraft screen, so
    /// the earlier screens say where it will be rather than inviting a press that does nothing.
    /// Dogfight below 2 players gets its own line — <see cref="CanLaunch"/> is withholding the
    /// launch gesture, so the generic "you may join" hint would undersell what is actually
    /// blocking it.</summary>
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
        string back = _screen == Screen.Mode ? "Esc / B  Quit" : "Esc / B  Back";
        string who = _slots.Count > 1 ? "       (P1 chooses)" : "";
        string nav = _screen switch
        {
            Screen.MissionType => "↑↓  Choose mission       ←→  Lives",
            Screen.WaveEdit or Screen.Wingmen => "↑↓  Choose field       ←→  Change",
            _ => "↑↓  Navigate",
        };
        return $"{nav}       Enter / A  Select       {back}{who}";
    }

    private string Breadcrumb()
    {
        string mode = Modes[(int)_mode].Label;
        return _screen switch
        {
            Screen.Mode => "Mode  ›  Map  ›  Aircraft",
            Screen.Chapter => $"{mode}  ›  Map  ›  Aircraft",
            Screen.Environment => $"{mode}  ›  Environment  ›  Mission  ›  Aircraft",
            Screen.MissionType => $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  Mission  ›  Aircraft",
            Screen.Waves or Screen.WaveEdit =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Waves  ›  Aircraft",
            Screen.Wingmen =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Wingmen  ›  Aircraft",
            _ when _mode == MenuMode.Stunt =>
                $"{mode}  ›  {Environments[_environmentIndex].Name}  ›  {CurrentMissionTypes[_missionTypeIndex].Label}  ›  Aircraft",
            _ => $"{mode}  ›  {CurrentChapters[_chapterIndex].Name}  ›  Aircraft",
        };
    }

    private string Detail(int focus) => _screen switch
    {
        Screen.Mode => Modes[focus].Detail,
        Screen.Chapter => $"Region {CurrentChapters[focus].Code}",
        Screen.Environment => $"Region {Environments[focus].Code}",
        Screen.MissionType => LivesDetail(),
        Screen.Waves => "Enter / A  edit a wave",
        Screen.WaveEdit or Screen.Wingmen => "←→  change",
        _ => PlaneStat(Planes[focus].Node),
    };

    /// <summary>The lives stepper's own line, shown where the other screens show the focused row's
    /// stat/region — it is not per-row, so it does not vary with the mission-type cursor.</summary>
    private string LivesDetail() =>
        _lives == 0 ? "Lives   Unlimited        ◀ ▶  change" : $"Lives   {_lives}        ◀ ▶  change";

    /// <summary>The Plane screen's own Instant Action line: the flown-wingmen re-clamp (decision
    /// 8a, <see cref="InstantActionRuntime.FlownWingmen"/>) against the CURRENT joined-player count
    /// — recomputed every Rebuild, so it tracks a pilot joining live. Empty outside Instant Action
    /// or at 0 configured wingmen, which is what lets the caller skip the row entirely rather than
    /// draw a blank one.</summary>
    private string WingmenLine()
    {
        if (_mode != MenuMode.Stunt || _numWingmen == 0)
            return "";
        int flown = InstantActionRuntime.FlownWingmen(_numWingmen, _slots.Count);
        return flown == _numWingmen
            ? $"Wingmen  {_numWingmen}"
            : $"Wingmen  {flown} of {_numWingmen} configured (flight capped at 6)";
    }

    /// <summary>A couple of stats for the focused plane, loaded lazily from vehicle.json and cached
    /// (null = load failed, shown as unavailable — never blocks the menu). fd_speed → mph is the
    /// validated top-speed figure (see PlaneStats).</summary>
    private string PlaneStat(string node)
    {
        var s = StatsFor(node);
        if (s == null)
            return "(stats unavailable)";
        return $"Top Speed  {Mph(s)} mph        Weight  {s.VehWeight:0}";
    }

    /// <summary>Just the top speed — the compact form used in the per-player pick lines.</summary>
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
    public readonly record struct PlayerChoice(string PlaneNode, int[] Pads);

    private readonly record struct Choice(string Label, string Detail);

    /// <summary>One wizard wave slot's UI state: how many enemies (0 = unconfigured), and the
    /// militia/aircraft/skill picked for it. <c>MilitiaIndex</c> resets <c>AircraftIndex</c> to 0
    /// when it changes (<see cref="HandleMoveX"/>'s WaveEdit case) — the decoded setup screen's own
    /// <c>AV[BA].QG = 0</c> (docs/formats/instant-action.md), since a militia's aircraft list is
    /// somebody else's roster once the militia changes. Mutable (not the record structs above) —
    /// <see cref="HandleMoveX"/> edits a field through a <c>ref</c> into <see cref="_waves"/>.</summary>
    private struct WaveSlot
    {
        public int Count;
        public int MilitiaIndex;
        public int AircraftIndex;
        public int SkillIndex;
    }

    /// <summary>One joined player: their device binding, their cursor in the plane list, and
    /// whether they have locked their pick.</summary>
    private sealed class Slot
    {
        public readonly MenuInput Input = new();
        public int PlaneIndex;
        public bool Locked;
    }
}
