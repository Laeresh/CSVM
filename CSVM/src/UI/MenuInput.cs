using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Utils;
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

    /// <summary>Whether the screen showing is taking typed text from this player's keyboard. The
    /// letter aliases below (W/A/S/D, Space, L, P) then read as dead, because otherwise typing a
    /// name would also walk the cursor and confirm the screen. The arrows, Enter and Escape stay
    /// live, and the pad is untouched: it types nothing and so collides with nothing.</summary>
    public bool TextEntry;

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
    /// <summary>The pad's own halves of the two cursor axes, without the keyboard. A screen whose
    /// keyboard is typing text reads these instead: W, A, S and D are letters there, so the
    /// combined axes above would move the cursor on every second character typed.</summary>
    public int PadMove;

    /// <summary>The horizontal twin of <see cref="PadMove"/>, same reason.</summary>
    public int PadMoveX;

    /// <summary>The characters typed this frame, "" for none — keyboard only, edge-detected per
    /// key. Letters arrive upper case while Shift is held. Polled like everything else here rather
    /// than read off an input event, so one screen's text and its navigation share a clock.</summary>
    public string Typed = string.Empty;

    /// <summary>Backspace pressed this frame (edge), the deletion half of <see cref="Typed"/>.</summary>
    public bool Erase;

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

    /// <summary>Open the Instant Action Table of Contents (edge). INVENTED: the original picks a
    /// preset with a mouse on a list that shares its page with the dropdowns, so there is no
    /// decoded button here. X is the last free face button in menu context.</summary>
    public bool Presets;

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

    // The keys a text field takes a character from: the letters, the digit row and the space bar.
    // Nothing else is typeable, because nothing else is a character a profile name may carry.
    private static readonly Key[] TextKeys = BuildTextKeys();

    // One timing rule for both cursor axes, shared with TapHoldButton's hold instead of a pair of
    // hand-rolled timer fields.
    private readonly HoldToRepeat _repeat = new(RepeatInitial, RepeatInterval);
    private readonly HoldToRepeat _repeatX = new(RepeatInitial, RepeatInterval);
    private readonly HoldToRepeat _repeatPad = new(RepeatInitial, RepeatInterval);
    private readonly HoldToRepeat _repeatPadX = new(RepeatInitial, RepeatInterval);

    // Previous state of every text key, in TextKeys order, for the same edge detection the
    // buttons get.
    private readonly bool[] _textPrev = new bool[TextKeys.Length];

    private bool _acceptPrev, _backPrev, _padBackPrev, _startPrev, _loadoutPrev, _presetsPrev;
    private bool _erasePrev;
    private int _dirPrev, _dirXPrev, _dirPadPrev, _dirPadXPrev;

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

    /// <summary>One frame of one cursor axis: a fresh press or a direction flip fires immediately
    /// and arms the initial delay, a held direction repeats on the timer, and letting go releases
    /// it. Public so the d-pad shape unit-tests (the repo takes public members over
    /// InternalsVisibleTo); it reads no device itself.</summary>
    public static int StepAxis(int dir, ref int prev, HoldToRepeat repeat, float dt)
    {
        int move = 0;
        if (dir != prev)
        {
            if (dir != 0)
            {
                move = dir;
                repeat.Press();
            }
            else
            {
                repeat.Release();
            }
        }
        else if (dir != 0 && repeat.Tick(dt))
        {
            move = dir;
        }

        prev = dir;
        return move;
    }


    /// <summary>Reads this player's devices and fills the result fields.</summary>
    public void Poll(float dt)
    {
        Move = StepAxis(RawDir(), ref _dirPrev, _repeat, dt);
        MoveX = StepAxis(RawDirX(), ref _dirXPrev, _repeatX, dt);
        PadMove = StepAxis(RawPadDir(), ref _dirPadPrev, _repeatPad, dt);
        PadMoveX = StepAxis(RawPadDirX(), ref _dirPadXPrev, _repeatPadX, dt);
        PollText();

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

        bool presets = RawPresets();
        Presets = presets && !_presetsPrev;
        _presetsPrev = presets;

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
        _presetsPrev = RawPresets();
        _dirPrev = RawDir();
        if (_dirPrev != 0)
            _repeat.Press();
        else
            _repeat.Release();
        Move = 0;
        _dirXPrev = RawDirX();
        if (_dirXPrev != 0)
            _repeatX.Press();
        else
            _repeatX.Release();
        MoveX = 0;
        PrimePadAxes();
        PrimeText();
        Accept = Back = PadBack = Start = Loadout = Presets = false;
    }

    // The Key enum's letter and digit values ARE their ASCII codes, so the character is the key.
    private static char CharFor(Key key, bool shift)
    {
        if (key == Key.Space)
            return ' ';
        char c = (char)(int)key;
        return key is >= Key.A and <= Key.Z && !shift ? char.ToLowerInvariant(c) : c;
    }

    // The letters, the digit row and the space bar, in that order.
    private static Key[] BuildTextKeys()
    {
        var keys = new List<Key>();
        for (Key k = Key.A; k <= Key.Z; k++)
            keys.Add(k);
        for (Key k = Key.Key0; k <= Key.Key9; k++)
            keys.Add(k);
        keys.Add(Key.Space);
        return keys.ToArray();
    }

    // The pad-only axes' half of Prime, kept together so neither can be seeded and the other not.
    private void PrimePadAxes()
    {
        _dirPadPrev = RawPadDir();
        if (_dirPadPrev != 0)
            _repeatPad.Press();
        else
            _repeatPad.Release();
        _dirPadXPrev = RawPadDirX();
        if (_dirPadXPrev != 0)
            _repeatPadX.Press();
        else
            _repeatPadX.Release();
        PadMove = PadMoveX = 0;
    }

    // A key still held from whatever opened the screen must not type itself into the field.
    private void PrimeText()
    {
        for (int i = 0; i < TextKeys.Length; i++)
            _textPrev[i] = KeyDown(TextKeys[i]);
        _erasePrev = KeyDown(Key.Backspace);
        Typed = string.Empty;
        Erase = false;
    }

    // The letters, digits and space pressed this frame, plus Backspace. Shift decides case, which
    // is what lets a profile name read as the original's own mixed-case roster does.
    private void PollText()
    {
        var typed = new StringBuilder();
        bool shift = KeyDown(Key.Shift);
        for (int i = 0; i < TextKeys.Length; i++)
        {
            bool down = KeyDown(TextKeys[i]);
            if (down && !_textPrev[i])
                typed.Append(CharFor(TextKeys[i], shift));
            _textPrev[i] = down;
        }

        Typed = typed.Length > 0 ? typed.ToString() : string.Empty;
        bool erase = KeyDown(Key.Backspace);
        Erase = erase && !_erasePrev;
        _erasePrev = erase;
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

    // A key that means something here only because nothing else claimed it. A screen taking typed
    // text claims it, so these go quiet there while the dedicated keys carry on.
    private bool AliasDown(Key key) => !TextEntry && KeyDown(key);

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

    private int RawPadDir()
    {
        float stickY = PadAxis(JoyAxis.LeftY);
        bool up = PadButton(JoyButton.DpadUp) || stickY < -StickDeadzone;
        bool down = PadButton(JoyButton.DpadDown) || stickY > StickDeadzone;
        return up ? -1 : down ? 1 : 0;
    }

    private int RawPadDirX()
    {
        float stickX = PadAxis(JoyAxis.LeftX);
        bool left = PadButton(JoyButton.DpadLeft) || stickX < -StickDeadzone;
        bool right = PadButton(JoyButton.DpadRight) || stickX > StickDeadzone;
        return left ? -1 : right ? 1 : 0;
    }

    private int RawDir()
    {
        int pad = RawPadDir();
        bool up = KeyDown(Key.Up) || AliasDown(Key.W) || pad < 0;
        bool down = KeyDown(Key.Down) || AliasDown(Key.S) || pad > 0;
        return up ? -1 : down ? 1 : 0;
    }

    private int RawDirX()
    {
        int pad = RawPadDirX();
        bool left = KeyDown(Key.Left) || AliasDown(Key.A) || pad < 0;
        bool right = KeyDown(Key.Right) || AliasDown(Key.D) || pad > 0;
        return left ? -1 : right ? 1 : 0;
    }

    private bool RawAccept() =>
        KeyDown(Key.Enter) || KeyDown(Key.KpEnter) || AliasDown(Key.Space) || PadButton(JoyButton.A);

    private bool RawBack() => KeyDown(Key.Escape) || RawPadBack();

    private bool RawPadBack() => PadButton(JoyButton.B);

    // Start is the join gesture, so it is pad-only: the keyboard is always player 1,
    // who is joined from the start and has nothing to join.
    private bool RawStart() => PadButton(JoyButton.Start);

    // L on the keyboard beside Y on the pad. Both are otherwise unread in menu context, so this
    // adds a meaning rather than overloading one: W/A/S/D are the stepper axes and Space/Enter,
    // Escape and Start are all spoken for.
    private bool RawLoadout() => AliasDown(Key.L) || PadButton(JoyButton.Y);

    // P on the keyboard beside X on the pad, the last free face button in menu context (A/B/Y and
    // Start are all spoken for above). Same rule as RawLoadout: a new meaning, not an overload.
    private bool RawPresets() => AliasDown(Key.P) || PadButton(JoyButton.X);
}
