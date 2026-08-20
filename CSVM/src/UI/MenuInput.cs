using System;
using Godot;

namespace CSVM.UI;

/// <summary>
/// One launchscreen player's input source: the keyboard (player 1 only) and that player's own
/// gamepads, polled every frame with edge detection and auto-repeat. Per-player rather than an
/// any-pad OR across the roster, which is what makes the join flow possible at all.
/// ⚠ Poll raw device state; do not move this to Godot's input map or focus system. Only raw
/// polling can read a NAMED device, and the join flow needs to know which pad pressed.
/// <see cref="Prime"/> seeds the edge flags from the current raw state, so a button still held
/// from whatever brought us here is not read as a fresh press on the next frame.
/// </summary>
public sealed class MenuInput
{
    /// <summary>Whether this player also flies the keyboard (player 1 only).</summary>
    public bool Keyboard;

    /// <summary>The gamepad devices this player reads, or null for every connected pad — the same
    /// binding <see cref="CSVM.Flight.FlightController"/> takes. A joined player has exactly one
    /// (the pad they pressed Start on); <b>player 1 holds every pad nobody has claimed</b>, which
    /// preserves the any-pad fix: phantom joypad devices can occupy the early slots, so binding
    /// player 1 to <c>pads[0]</c> would leave a real controller dead. Idle devices read as zero,
    /// so reading several is safe.</summary>
    public int[]? Pads = Array.Empty<int>();

    // Results of the last Poll, valid until the next one.
    public int Move;        // −1 up, +1 down, 0 none (auto-repeat already applied)
    /// <summary>−1 left, +1 right, 0 none (auto-repeat already applied) — a second, independent
    /// axis from <see cref="Move"/> so a screen can carry a list cursor (vertical) and a numeric
    /// stepper (horizontal) at once, the way the Instant Action wizard's mission-type screen reads
    /// mission choice and lives together.</summary>
    public int MoveX;
    public bool Accept;     // pressed this frame (edge)
    public bool Back;       // pressed this frame (edge)
    /// <summary>Back on the pad alone, without Escape — for a reader whose Escape is already spoken
    /// for elsewhere. A board menu's is: Escape toggles the pause that owns the board, so reading
    /// it here as well would toggle twice on one press.</summary>
    public bool PadBack;
    public bool Start;      // pressed this frame (edge)

    /// <summary>Open the loadout for whatever this screen is about (edge). Y is free in menu
    /// context — nothing else here reads it, and its only other use is in flight — so it can mean
    /// one thing everywhere the menu offers a fit to edit.</summary>
    public bool Loadout;

    /// <summary>The last of this player's pads seen actually doing something (a menu button or the
    /// stick past the deadzone) — −1 until one does. The launchscreen uses it to <i>claim</i> the
    /// pad player 1 drives the Mode/Chapter screens with, so that pad is player 1's for good and
    /// only the remaining ones can join. Start is excluded on purpose: it is the join gesture, not
    /// evidence that this player owns the pad.</summary>
    public int LastActivePad = -1;

    // Auto-repeat while a direction is held (carried over from the original LaunchMenu). Confirmed
    // at the controls: join/lock feel reads right at 2P and 4P, no retune owed.
    private const float RepeatInitial = 0.42f;   // s before the first repeat
    private const float RepeatInterval = 0.12f;  // s between repeats after that
    private const float StickDeadzone = 0.5f;    // |LeftY| past this counts as a d-pad press

    private bool _acceptPrev, _backPrev, _padBackPrev, _startPrev, _loadoutPrev;
    private int _dirPrev, _dirXPrev;
    private float _repeatTimer, _repeatTimerX;

    /// <summary>The single pad this player is bound to, or −1 when it has none or several
    /// (player 1's unclaimed set) — for logging and the join bookkeeping.</summary>
    public int Pad => Pads is { Length: 1 } ? Pads[0] : -1;

    /// <summary>A short description of what drives this player, for the menu's join strip.</summary>
    public string DeviceLabel
    {
        get
        {
            string pads = Pads == null ? "any pad"
                : Pads.Length == 0 ? ""
                : Pads.Length == 1 ? $"pad {Pads[0]}" : $"{Pads.Length} pads";
            if (Keyboard)
                return pads.Length == 0 ? "keyboard" : $"keyboard + {pads}";
            return pads.Length == 0 ? "no device" : pads;
        }
    }

    /// <summary>Whether an unbound pad is pressing Start — the join gesture. Static because the
    /// pad has no player (and therefore no <see cref="MenuInput"/>) until it joins; the caller
    /// edge-detects per device. Gated like every other pad read, so nobody joins while the
    /// window is in the background.</summary>
    public static bool JoinPressed(int pad) =>
        !CSVM.Pads.InputBlocked && Input.IsJoyButtonPressed(pad, JoyButton.Start);

    /// <summary>Reads this player's devices and fills the result fields.</summary>
    public void Poll(float dt)
    {
        int dir = RawDir();
        Move = 0;
        if (dir != 0)
        {
            if (dir != _dirPrev)
            {
                Move = dir;
                _repeatTimer = RepeatInitial;
            }
            else if ((_repeatTimer -= dt) <= 0f)
            {
                Move = dir;
                _repeatTimer = RepeatInterval;
            }
        }
        _dirPrev = dir;

        int dirX = RawDirX();
        MoveX = 0;
        if (dirX != 0)
        {
            if (dirX != _dirXPrev)
            {
                MoveX = dirX;
                _repeatTimerX = RepeatInitial;
            }
            else if ((_repeatTimerX -= dt) <= 0f)
            {
                MoveX = dirX;
                _repeatTimerX = RepeatInterval;
            }
        }
        _dirXPrev = dirX;

        bool accept = RawAccept();
        Accept = accept && !_acceptPrev;
        _acceptPrev = accept;

        bool back = RawBack();
        Back = back && !_backPrev;
        _backPrev = back;

        bool padBack = RawPadBack();
        PadBack = padBack && !_padBackPrev;
        _padBackPrev = padBack;

        bool start = RawStart();
        Start = start && !_startPrev;
        _startPrev = start;

        bool loadout = RawLoadout();
        Loadout = loadout && !_loadoutPrev;
        _loadoutPrev = loadout;

        int active = ScanActivePad();
        if (active >= 0)
            LastActivePad = active;
    }

    /// <summary>Seeds the edge flags from the current state (no press is reported for anything
    /// already held) and clears the last results.</summary>
    public void Prime()
    {
        _acceptPrev = RawAccept();
        _backPrev = RawBack();
        _padBackPrev = RawPadBack();
        _startPrev = RawStart();
        _loadoutPrev = RawLoadout();
        _dirPrev = RawDir();
        _repeatTimer = RepeatInitial;
        Move = 0;
        _dirXPrev = RawDirX();
        _repeatTimerX = RepeatInitial;
        MoveX = 0;
        Accept = Back = PadBack = Start = Loadout = false;
    }

    // The first of this player's pads currently producing menu input (excluding Start).
    // Phantom devices never register — they read idle — so a pad found here is demonstrably a
    // real one somebody is holding.
    private int ScanActivePad()
    {
        foreach (int pad in CSVM.Pads.For(Pads))
        {
            if (Input.IsJoyButtonPressed(pad, JoyButton.A) ||
                Input.IsJoyButtonPressed(pad, JoyButton.B) ||
                Input.IsJoyButtonPressed(pad, JoyButton.DpadUp) ||
                Input.IsJoyButtonPressed(pad, JoyButton.DpadDown) ||
                Mathf.Abs(Input.GetJoyAxis(pad, JoyAxis.LeftY)) > StickDeadzone)
                return pad;
        }
        return -1;
    }

    private bool KeyDown(Key key) => Keyboard && Input.IsKeyPressed(key);

    // Button pressed on ANY of this player's pads (a set of one for a joined player,
    // every unclaimed device for player 1). Through `CSVM.Pads.For` rather than the
    // Pads field directly, so the read is gated on window focus and on
    // `--no-pads` — the field stays the player's binding, which
    // the join bookkeeping still needs while unfocused.
    private bool PadButton(JoyButton button)
    {
        foreach (int pad in CSVM.Pads.For(Pads))
            if (Input.IsJoyButtonPressed(pad, button))
                return true;
        return false;
    }

    // The largest-magnitude value of the axis across this player's pads — idle phantom
    // devices read ~0 and never mask a real stick.
    private float PadAxis(JoyAxis axis)
    {
        float v = 0f;
        foreach (int pad in CSVM.Pads.For(Pads))
        {
            float a = Input.GetJoyAxis(pad, axis);
            if (Mathf.Abs(a) > Mathf.Abs(v))
                v = a;
        }
        return v;
    }

    private int RawDir()
    {
        float stickY = PadAxis(JoyAxis.LeftY);
        bool up = KeyDown(Key.Up) || KeyDown(Key.W) || PadButton(JoyButton.DpadUp) || stickY < -StickDeadzone;
        bool down = KeyDown(Key.Down) || KeyDown(Key.S) || PadButton(JoyButton.DpadDown) || stickY > StickDeadzone;
        return up ? -1 : down ? 1 : 0;
    }

    private int RawDirX()
    {
        float stickX = PadAxis(JoyAxis.LeftX);
        bool left = KeyDown(Key.Left) || KeyDown(Key.A) || PadButton(JoyButton.DpadLeft) || stickX < -StickDeadzone;
        bool right = KeyDown(Key.Right) || KeyDown(Key.D) || PadButton(JoyButton.DpadRight) || stickX > StickDeadzone;
        return left ? -1 : right ? 1 : 0;
    }

    private bool RawAccept() =>
        KeyDown(Key.Enter) || KeyDown(Key.KpEnter) || KeyDown(Key.Space) || PadButton(JoyButton.A);

    private bool RawBack() => KeyDown(Key.Escape) || RawPadBack();

    private bool RawPadBack() => PadButton(JoyButton.B);

    // Start is the join gesture, so it is pad-only: the keyboard is always player 1,
    // who is joined from the start and has nothing to join.
    private bool RawStart() => PadButton(JoyButton.Start);

    // L on the keyboard beside Y on the pad. Both are otherwise unread in menu context, so this
    // adds a meaning rather than overloading one: W/A/S/D are the stepper axes and Space/Enter,
    // Escape and Start are all spoken for.
    private bool RawLoadout() => KeyDown(Key.L) || PadButton(JoyButton.Y);
}
