using System;
using System.Collections.Generic;
using CrimsonSkies.Flight;
using Godot;

namespace CrimsonSkies.UI;

/// <summary>
/// The in-game launchscreen (Milestone 2.5 item 4): a keyboard/controller-driven menu shown when
/// the viewer is launched with no content-selecting CLI arg (a bare launch, e.g. RunGame.ps1).
/// Three screens in sequence — <b>Mode</b> (Free Flight / Stunt Flying) → <b>Chapter</b> (the eight
/// chapter worlds) → <b>Plane</b> (the player roster, with a couple of stats from
/// <see cref="PlaneStats"/>) — after which <see cref="Launch"/> fires with the chosen
/// chapter/plane/mode and PlaneViewer builds the world through the normal arg-driven pipeline (the
/// menu just fills in the same selections the CLI would).
///
/// Navigation is polled every frame (uniform across keyboard and EVERY connected gamepad's
/// d-pad/left stick — any-pad, never pads[0], so hot-plugged pads and machines with phantom
/// joypad devices work, and the footer shows the live roster) with edge detection + auto-repeat,
/// so no Godot input map / focus wiring is needed: ↑↓ / W,S / d-pad / stick move the highlight,
/// Enter/Space/A accept, Esc/B go back (Back on the Mode screen quits via <see cref="Quit"/>).
/// It is a plain Godot-UI overlay (opaque panel + labels) on its own high CanvasLayer, distinct
/// from the hand-drawn flight HUD.
///
/// Re-entrant: PlaneViewer tears the world down and calls <see cref="ShowMenu"/> again on
/// Esc-from-flight, so this always resets to the Mode screen and re-primes its input edges (a held
/// Esc that returned here must not immediately re-trigger Back).
/// </summary>
public sealed partial class LaunchMenu : CanvasLayer
{
    /// <summary>Fired when the player confirms a plane: (chapter code, plane node name, stunt mode).
    /// The host hides the menu and builds the session.</summary>
    public Action<string, string, bool>? Launch;

    /// <summary>Fired when the player backs out of the Mode screen — the host quits.</summary>
    public Action? Quit;

    private enum Screen { Mode, Chapter, Plane }

    private readonly record struct Choice(string Label, string Detail);

    // The two flight modes. Index 1 (Stunt Flying) sets stunt mode.
    private static readonly Choice[] Modes =
    {
        new("Free Flight", "Explore the map freely — no objectives, no clock."),
        new("Stunt Flying", "Race through every Danger Zone against the clock."),
    };

    // The eight chapter worlds (mirrors RunDev.ps1's roster: display name + extracted folder code;
    // C1/C1B/C1C are day/night/weather variants of Sea Haven). Not every chapter has Danger Zones —
    // C1C/C2B have none, so Stunt Flying there falls back to free flight (logged by StuntMission).
    private static readonly (string Name, string Code)[] Chapters =
    {
        ("Sea Haven (night)", "C1"),
        ("Sea Haven — variant B", "C1B"),
        ("Sea Haven — variant C", "C1C"),
        ("Hollywood", "C2"),
        ("Hollywood — variant B", "C2B"),
        ("Hawaii (islands)", "C3"),
        ("Rocky Mountains", "C4"),
        ("New York", "C5"),
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

    // Input timing (TUNE): auto-repeat while a direction is held.
    private const float RepeatInitial = 0.42f;   // s before the first repeat
    private const float RepeatInterval = 0.12f;  // s between repeats after that
    private const float StickDeadzone = 0.5f;    // |LeftY| past this counts as a d-pad press

    // Base metrics at 720p, scaled up on taller viewports (like StuntScoreboard). All TUNE.
    private const int TitleFont = 40;
    private const int HeadingFont = 20;
    private const int CrumbFont = 15;
    private const int RowFont = 22;
    private const int DetailFont = 16;
    private const int FooterFont = 15;
    private const int ErrorFont = 15;

    private static readonly Color TitleColor = new(0.96f, 0.80f, 0.35f);
    private static readonly Color CrumbColor = new(0.55f, 0.68f, 0.86f);
    private static readonly Color HeadingColor = new(0.80f, 0.88f, 0.98f);
    private static readonly Color RowColor = new(0.55f, 0.62f, 0.72f);
    private static readonly Color RowFocusColor = new(1f, 0.86f, 0.38f);
    private static readonly Color DetailColor = new(0.66f, 0.78f, 0.92f);
    private static readonly Color FooterColor = new(0.52f, 0.60f, 0.70f);
    private static readonly Color ErrorColor = new(1f, 0.55f, 0.45f);

    private string _zrdrPath = "";
    private readonly Dictionary<string, PlaneStats?> _stats = new();

    private Screen _screen = Screen.Mode;
    private int _modeIndex, _chapterIndex, _planeIndex;
    private bool _stunt;
    private string _error = "";

    // Input edge/repeat state.
    private bool _acceptPrev, _backPrev;
    private int _vDirPrev;
    private float _repeatTimer;

    // Footer gamepad line as last drawn — _Process redraws when the live roster changes (hotplug).
    private string _padStatus = "";

    private VBoxContainer _body = null!;

    /// <summary>Builds the (hidden) launchscreen. <paramref name="zrdrPath"/> is the shared zrdr
    /// extraction the plane stats come from. Add it to the tree, wire <see cref="Launch"/> /
    /// <see cref="Quit"/>, then <see cref="ShowMenu"/>.</summary>
    public static LaunchMenu Build(string zrdrPath)
    {
        var menu = new LaunchMenu { _zrdrPath = zrdrPath, Layer = 10, Visible = false };

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        menu.AddChild(root);

        // Fully opaque backdrop so the empty 3D scene (procedural sky) never shows through.
        var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.08f), MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        menu._body = new VBoxContainer();
        menu._body.AddThemeConstantOverride("separation", 6);
        center.AddChild(menu._body);

        return menu;
    }

    /// <summary>Show the menu (normally from the Mode screen) and prime the input edges so a key
    /// still held from the transition here (e.g. the Esc that left a flight) does not fire
    /// immediately. <paramref name="startScreen"/> ("chapter"/"plane") opens on a later screen —
    /// a screenshot/verification aid (--menu=plane); anything else starts at Mode.</summary>
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
        PrimeInput();
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

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        float dt = (float)delta;

        // Live hotplug: redraw when the pad roster changes so the footer line stays truthful.
        if (GamepadStatus() != _padStatus)
            Rebuild();

        int vDir = RawVDir();
        if (vDir != 0)
        {
            if (vDir != _vDirPrev)
            {
                Move(vDir);
                _repeatTimer = RepeatInitial;
            }
            else if ((_repeatTimer -= dt) <= 0f)
            {
                Move(vDir);
                _repeatTimer = RepeatInterval;
            }
        }
        _vDirPrev = vDir;

        bool accept = RawAccept();
        if (accept && !_acceptPrev)
            OnAccept();
        _acceptPrev = accept;

        bool back = RawBack();
        if (back && !_backPrev)
            OnBack();
        _backPrev = back;
    }

    // --- input reads (shared by _Process and PrimeInput) ---
    // Every read spans ALL connected gamepads, never pads[0]: phantom joypad devices (wireless
    // dongles enumerating with the pad asleep, non-pad HID) can occupy the early slots, and a pad
    // connected after launch lands in a later one. Idle devices read as zero, so any-pad is safe.

    private static bool AnyPadPressed(JoyButton button)
    {
        foreach (int pad in Input.GetConnectedJoypads())
            if (Input.IsJoyButtonPressed(pad, button))
                return true;
        return false;
    }

    /// <summary>The largest-magnitude value of the axis across all connected gamepads (0 when none).</summary>
    private static float AnyPadAxis(JoyAxis axis)
    {
        float v = 0f;
        foreach (int pad in Input.GetConnectedJoypads())
        {
            float a = Input.GetJoyAxis(pad, axis);
            if (Mathf.Abs(a) > Mathf.Abs(v))
                v = a;
        }
        return v;
    }

    private static int RawVDir()
    {
        float stickY = AnyPadAxis(JoyAxis.LeftY);
        bool up = Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)
            || AnyPadPressed(JoyButton.DpadUp) || stickY < -StickDeadzone;
        bool down = Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)
            || AnyPadPressed(JoyButton.DpadDown) || stickY > StickDeadzone;
        return up ? -1 : down ? 1 : 0;
    }

    private static bool RawAccept() =>
        Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.KpEnter) || Input.IsKeyPressed(Key.Space)
        || AnyPadPressed(JoyButton.A);

    private static bool RawBack() =>
        Input.IsKeyPressed(Key.Escape) || AnyPadPressed(JoyButton.B);

    private void PrimeInput()
    {
        _acceptPrev = RawAccept();
        _backPrev = RawBack();
        _vDirPrev = RawVDir();
        _repeatTimer = RepeatInitial;
    }

    // --- navigation ---

    private int CurrentCount() => _screen switch
    {
        Screen.Mode => Modes.Length,
        Screen.Chapter => Chapters.Length,
        _ => Planes.Length,
    };

    private int CurrentIndex => _screen switch
    {
        Screen.Mode => _modeIndex,
        Screen.Chapter => _chapterIndex,
        _ => _planeIndex,
    };

    private void Move(int dir)
    {
        int n = CurrentCount();
        int next = ((CurrentIndex + dir) % n + n) % n;
        switch (_screen)
        {
            case Screen.Mode: _modeIndex = next; break;
            case Screen.Chapter: _chapterIndex = next; break;
            default: _planeIndex = next; break;
        }
        Rebuild();
    }

    private void OnAccept()
    {
        _error = "";
        switch (_screen)
        {
            case Screen.Mode:
                _stunt = _modeIndex == 1;
                _screen = Screen.Chapter;
                Rebuild();
                break;
            case Screen.Chapter:
                _screen = Screen.Plane;
                Rebuild();
                break;
            case Screen.Plane:
                // The host hides the menu and builds; leave our state as-is so a failed build can
                // send us back with ShowMenu (which resets to Mode).
                Launch?.Invoke(Chapters[_chapterIndex].Code, Planes[_planeIndex].Node, _stunt);
                break;
        }
    }

    private void OnBack()
    {
        switch (_screen)
        {
            case Screen.Mode:
                Quit?.Invoke();
                break;
            case Screen.Chapter:
                _screen = Screen.Mode;
                Rebuild();
                break;
            case Screen.Plane:
                _screen = Screen.Chapter;
                Rebuild();
                break;
        }
    }

    // --- rendering ---

    private void Rebuild()
    {
        foreach (var c in _body.GetChildren())
            c.QueueFree();

        // CanvasLayer is a Node (not a CanvasItem), so read the size off the Viewport directly.
        float s = Mathf.Max(1f, GetViewport().GetVisibleRect().Size.Y / 720f);
        _body.CustomMinimumSize = new Vector2(560f * s, 0f);

        _body.AddChild(Label("CRIMSON SKIES", (int)(TitleFont * s), TitleColor, HorizontalAlignment.Center));
        _body.AddChild(Label(Breadcrumb(), (int)(CrumbFont * s), CrumbColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(10 * s)));

        string heading = _screen switch
        {
            Screen.Mode => "SELECT MODE",
            Screen.Chapter => "SELECT MAP",
            _ => "SELECT AIRCRAFT",
        };
        _body.AddChild(Label(heading, (int)(HeadingFont * s), HeadingColor, HorizontalAlignment.Center));
        _body.AddChild(Spacer((int)(6 * s)));

        int count = CurrentCount();
        int focus = CurrentIndex;
        for (int i = 0; i < count; i++)
        {
            string text = _screen switch
            {
                Screen.Mode => Modes[i].Label,
                Screen.Chapter => Chapters[i].Name,
                _ => Planes[i].Name,
            };
            bool sel = i == focus;
            _body.AddChild(Label((sel ? "▶  " : "     ") + text, (int)(RowFont * s),
                sel ? RowFocusColor : RowColor, HorizontalAlignment.Center));
        }

        _body.AddChild(Spacer((int)(10 * s)));
        _body.AddChild(Label(Detail(focus), (int)(DetailFont * s), DetailColor, HorizontalAlignment.Center));

        if (_error.Length > 0)
        {
            _body.AddChild(Spacer((int)(4 * s)));
            _body.AddChild(Label(_error, (int)(ErrorFont * s), ErrorColor, HorizontalAlignment.Center));
        }

        _body.AddChild(Spacer((int)(16 * s)));
        string back = _screen == Screen.Mode ? "Esc / B  Quit" : "Esc / B  Back";
        _body.AddChild(Label($"↑↓  Navigate       Enter / A  Select       {back}",
            (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));

        _padStatus = GamepadStatus();
        _body.AddChild(Spacer((int)(4 * s)));
        _body.AddChild(Label(_padStatus, (int)(FooterFont * s), FooterColor, HorizontalAlignment.Center));
    }

    private static string GamepadStatus()
    {
        var pads = Input.GetConnectedJoypads();
        return pads.Count == 0 ? "No gamepad — keyboard controls"
            : pads.Count == 1 ? $"Gamepad: {Input.GetJoyName(pads[0])}"
            : $"Gamepads: {pads.Count} connected — all active";
    }

    private string Breadcrumb()
    {
        string mode = _stunt ? "Stunt Flying" : "Free Flight";
        return _screen switch
        {
            Screen.Mode => "Mode  ›  Map  ›  Aircraft",
            Screen.Chapter => $"{mode}  ›  Map  ›  Aircraft",
            _ => $"{mode}  ›  {Chapters[_chapterIndex].Name}  ›  Aircraft",
        };
    }

    private string Detail(int focus) => _screen switch
    {
        Screen.Mode => Modes[focus].Detail,
        Screen.Chapter => $"Region {Chapters[focus].Code}",
        _ => PlaneStat(Planes[focus].Node),
    };

    /// <summary>A couple of stats for the focused plane, loaded lazily from vehicle.json and cached
    /// (null = load failed, shown as unavailable — never blocks the menu). fd_speed → mph is the
    /// validated top-speed figure (see PlaneStats).</summary>
    private string PlaneStat(string node)
    {
        if (!_stats.TryGetValue(node, out var s))
        {
            try { s = PlaneStats.Load(_zrdrPath, node); }
            catch (Exception e) { GD.Print($"launchscreen: no stats for {node}: {e.Message}"); s = null; }
            _stats[node] = s;
        }
        if (s == null)
            return "(stats unavailable)";
        int mph = Mathf.RoundToInt(s.FdSpeed * 2.23694f);
        return $"Top Speed  {mph} mph        Weight  {s.VehWeight:0}";
    }

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
}
