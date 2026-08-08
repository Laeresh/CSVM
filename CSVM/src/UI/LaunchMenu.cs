using System;
using System.Collections.Generic;
using CSVM.Flight;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The in-game launchscreen: a keyboard/controller-driven menu shown
/// when the viewer is launched with no content-selecting CLI arg (a bare launch, e.g.
/// RunGame.ps1). Three screens in sequence — <b>Mode</b> (Free Flight / Stunt Flying / Dogfight) →
/// <b>Chapter</b> (the eight chapter worlds) → <b>Plane</b> (the player roster, with a couple of
/// stats from <see cref="PlaneStats"/>) — after which <see cref="Launch"/> fires with the chosen
/// chapter, the per-player plane + pad, and the mode; GameSession builds the world through the
/// normal arg-driven pipeline (the menu just fills in the same selections the CLI would).
///
/// <para><b>Dogfight needs a fight.</b> Its Plane screen withholds the launch gesture until at
/// least two players have joined, even once everyone present is locked — see
/// <see cref="CanLaunch"/> and the hint line <see cref="JoinHint"/> shows while it is withheld.
/// Free Flight and Stunt Flying still launch solo exactly as before.</para>
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
/// chapter screen instead, which unlocks everyone). A pad that disconnects drops its player
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
    /// player in player order, the picked mode). The host hides the menu and builds the session.</summary>
    public Action<string, IReadOnlyList<PlayerChoice>, MenuMode>? Launch;

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
    // Reference values at 720p (TUNE).
    private const float StripHeightFrac = 0.12f;
    private const int PanePad = 10;

    // The three flight modes, in MenuMode's ordinal order (Free/Stunt/Versus) so the row index
    // doubles as the enum value with no separate lookup.
    private static readonly Choice[] Modes =
    {
        new("Free Flight", "Explore the map freely — no objectives, no clock."),
        new("Stunt Flying", "Race through every Danger Zone against the clock."),
        new("Dogfight", "Splitscreen free-for-all — first to the kill target wins."),
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

    private string _zrdrPath = "";
    private Screen _screen = Screen.Mode;
    private int _modeIndex, _chapterIndex;
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

    private enum Screen { Mode, Chapter, Plane }

    /// <summary>The chapter roster the picked mode offers — the Chapter screen and everything
    /// downstream (breadcrumb, launch) index into this, never the full list.</summary>
    private (string Name, string Code, bool DangerZones)[] CurrentChapters => ChaptersFor(_mode);

    /// <summary>The single-player cursor position on the current screen (the plane screen reads
    /// player 1's cursor).</summary>
    private int CurrentIndex => _screen switch
    {
        Screen.Mode => _modeIndex,
        Screen.Chapter => _chapterIndex,
        _ => _slots[0].PlaneIndex,
    };

    /// <summary>Builds the (hidden) launchscreen. <paramref name="zrdrPath"/> is the shared zrdr
    /// extraction the plane stats come from. Add it to the tree, wire <see cref="Launch"/> /
    /// <see cref="Quit"/>, then <see cref="ShowMenu"/>.</summary>
    public static LaunchMenu Build(string zrdrPath)
    {
        var menu = new LaunchMenu { _zrdrPath = zrdrPath, Layer = HudLayers.Board, Visible = false };

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
    /// at least two have joined to fight each other. Free Flight and Stunt Flying launch solo
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

    /// <summary>Show the menu (normally from the Mode screen) and prime every input edge so a
    /// button still held from the transition here (the Esc that left a flight, the Start that
    /// joined a player) does not fire immediately. Joined players survive a return from flight;
    /// their plane locks do not. <paramref name="startScreen"/> ("chapter"/"plane") opens on a
    /// later screen — a screenshot/verification aid (--menu=plane).</summary>
    public void ShowMenu(string startScreen = "")
    {
        _screen = startScreen switch
        {
            "chapter" => Screen.Chapter,
            "plane" => Screen.Plane,
            _ => Screen.Mode,
        };
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

    private static (string Name, string Code, bool DangerZones)[] ChaptersFor(MenuMode mode) =>
        mode == MenuMode.Stunt ? Array.FindAll(Chapters, c => c.DangerZones) : Chapters;

    private static int Mph(PlaneStats s) => Mathf.RoundToInt(s.FdSpeed * 2.23694f);

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
                int n = _screen == Screen.Mode ? Modes.Length : CurrentChapters.Length;
                if (_screen == Screen.Mode)
                    _modeIndex = Wrap(_modeIndex + p1.Move, n);
                else
                    _chapterIndex = Wrap(_chapterIndex + p1.Move, n);
                dirty = true;
            }
            if (p1.Accept)
            {
                _error = "";
                _screen = _screen == Screen.Mode ? Screen.Chapter : Screen.Plane;
                if (_screen == Screen.Chapter)
                {
                    _mode = (MenuMode)_modeIndex; // the row order IS the enum order
                    // The roster may have shrunk (Stunt hides the dzone-less maps) — keep the
                    // cursor on a row that exists.
                    _chapterIndex = Wrap(_chapterIndex, CurrentChapters.Length);
                }
                else
                    PrimeJoins(); // joining opens here — a Start held on the way in must not fire
                dirty = true;
            }
            else if (p1.Back)
            {
                if (_screen == Screen.Mode)
                    Quit?.Invoke();
                else
                    _screen = Screen.Mode;
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
                    // Player 1 backing out returns everyone to the chapter screen.
                    _screen = Screen.Chapter;
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
        // Leave our state as-is so a failed build can send us back with ShowMenu.
        Launch?.Invoke(CurrentChapters[_chapterIndex].Code, choices, _mode);
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
            _ => _slots.Count > 1 ? "SELECT AIRCRAFT — ALL PLAYERS" : "SELECT AIRCRAFT",
        };
        _body.AddChild(Label(heading, (int)(HeadingFont * s), HeadingColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(6 * s)));

        int count = CurrentCount();
        for (int i = 0; i < count; i++)
            _body.AddChild(Row(i, s));

        _body.AddChild(Spacer((int)(10 * s)));
        _body.AddChild(DetailBlock(s));

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
        float stripH = size.Y * StripHeightFrac;
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
        float refH =
            font.GetHeight(TitleFont) + font.GetHeight(CrumbFont) + font.GetHeight(FooterFont) +
            font.GetHeight(HeadingFont) + rows * font.GetHeight(RowFont) +
            font.GetHeight(DetailFont) + font.GetHeight(FooterFont) +
            (_error.Length > 0 ? font.GetHeight(ErrorFont) + 4 : 0) +
            8 + 8 + 6 + 10 + 16 +          // the explicit spacers Rebuild adds
            6 * (10 + rows);               // the body VBox's separation between children
        return Mathf.Min(s, viewH / refH);
    }

    private int CurrentCount() => _screen switch
    {
        Screen.Mode => Modes.Length,
        Screen.Chapter => CurrentChapters.Length,
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
            _ => Planes[index].Name,
        };
        bool sel = index == CurrentIndex;
        return Label((sel ? "▶  " : "     ") + text, (int)(RowFont * s),
            sel ? RowFocusColor : RowColor, HorizontalAlignment.Center);
    }

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
        return $"↑↓  Navigate       Enter / A  Select       {back}{who}";
    }

    private string Breadcrumb()
    {
        string mode = Modes[(int)_mode].Label;
        return _screen switch
        {
            Screen.Mode => "Mode  ›  Map  ›  Aircraft",
            Screen.Chapter => $"{mode}  ›  Map  ›  Aircraft",
            _ => $"{mode}  ›  {CurrentChapters[_chapterIndex].Name}  ›  Aircraft",
        };
    }

    private string Detail(int focus) => _screen switch
    {
        Screen.Mode => Modes[focus].Detail,
        Screen.Chapter => $"Region {CurrentChapters[focus].Code}",
        _ => PlaneStat(Planes[focus].Node),
    };

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

    /// <summary>One joined player: their device binding, their cursor in the plane list, and
    /// whether they have locked their pick.</summary>
    private sealed class Slot
    {
        public readonly MenuInput Input = new();
        public int PlaneIndex;
        public bool Locked;
    }
}
