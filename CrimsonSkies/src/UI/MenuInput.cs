using System;
using Godot;

namespace CrimsonSkies.UI;

/// <summary>
/// One launchscreen player's input source (M2.5 item 6): the keyboard (player 1 only) and/or that
/// player's gamepads, polled every frame with edge detection + auto-repeat. Splitting this out
/// of <see cref="LaunchMenu"/> is what makes the join flow possible at all — before the split,
/// every menu read was an any-pad OR across the whole roster (correct for one player, useless
/// once two people need separate cursors).
///
/// <para>Polling rather than Godot's input map / focus system is deliberate and carried over from
/// item 4: it needs no project-settings wiring, works identically for keyboard and pad, and — the
/// reason it matters here — reads a <b>named device</b>, which the action system cannot do.</para>
///
/// <para><see cref="Prime"/> seeds the edge flags from the current raw state, so a button still
/// held from whatever brought us here (the Start press that joined this player, the Esc that left
/// a flight) is not read as a fresh press on the next frame.</para>
/// </summary>
public sealed class MenuInput
{
    // Auto-repeat while a direction is held (TUNE; carried over from item 4's LaunchMenu).
    private const float RepeatInitial = 0.42f;   // s before the first repeat
    private const float RepeatInterval = 0.12f;  // s between repeats after that
    private const float StickDeadzone = 0.5f;    // |LeftY| past this counts as a d-pad press

    /// <summary>Whether this player also flies the keyboard (player 1 only).</summary>
    public bool Keyboard;

    /// <summary>The gamepad devices this player reads. A joined player has exactly one (the pad
    /// they pressed Start on); <b>player 1 holds every pad nobody has claimed</b>, which is what
    /// preserves the 2026-07-19 any-pad fix: phantom joypad devices (a wireless dongle enumerating
    /// with the pad asleep, a non-pad HID exposing a joypad interface) can occupy the early slots,
    /// so binding player 1 to <c>pads[0]</c> would leave a real controller dead in the menu. Idle
    /// devices read as zero, so reading several is safe.</summary>
    public int[] Pads = Array.Empty<int>();

    /// <summary>The single pad this player is bound to, or −1 when it has none or several
    /// (player 1's unclaimed set) — for logging and the join bookkeeping.</summary>
    public int Pad => Pads.Length == 1 ? Pads[0] : -1;

    // Results of the last Poll, valid until the next one.
    public int Move;        // −1 up, +1 down, 0 none (auto-repeat already applied)
    public bool Accept;     // pressed this frame (edge)
    public bool Back;       // pressed this frame (edge)
    public bool Start;      // pressed this frame (edge)

    /// <summary>The last of this player's pads seen actually doing something (a menu button or the
    /// stick past the deadzone) — −1 until one does. The launchscreen uses it to <i>claim</i> the
    /// pad player 1 drives the Mode/Chapter screens with, so that pad is player 1's for good and
    /// only the remaining ones can join. Start is excluded on purpose: it is the join gesture, not
    /// evidence that this player owns the pad.</summary>
    public int LastActivePad = -1;

    private bool _acceptPrev, _backPrev, _startPrev;
    private int _dirPrev;
    private float _repeatTimer;

    /// <summary>A short description of what drives this player, for the menu's join strip.</summary>
    public string DeviceLabel
    {
        get
        {
            string pads = Pads.Length == 0 ? "" : Pads.Length == 1 ? $"pad {Pads[0]}" : $"{Pads.Length} pads";
            if (Keyboard)
                return pads.Length == 0 ? "keyboard" : $"keyboard + {pads}";
            return pads.Length == 0 ? "no device" : pads;
        }
    }

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

        bool accept = RawAccept();
        Accept = accept && !_acceptPrev;
        _acceptPrev = accept;

        bool back = RawBack();
        Back = back && !_backPrev;
        _backPrev = back;

        bool start = RawStart();
        Start = start && !_startPrev;
        _startPrev = start;

        int active = ScanActivePad();
        if (active >= 0)
            LastActivePad = active;
    }

    /// <summary>The first of this player's pads currently producing menu input (excluding Start).
    /// Phantom devices never register — they read idle — so a pad found here is demonstrably a
    /// real one somebody is holding.</summary>
    private int ScanActivePad()
    {
        foreach (int pad in Pads)
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

    /// <summary>Seeds the edge flags from the current state (no press is reported for anything
    /// already held) and clears the last results.</summary>
    public void Prime()
    {
        _acceptPrev = RawAccept();
        _backPrev = RawBack();
        _startPrev = RawStart();
        _dirPrev = RawDir();
        _repeatTimer = RepeatInitial;
        Move = 0;
        Accept = Back = Start = false;
    }

    private bool KeyDown(Key key) => Keyboard && Input.IsKeyPressed(key);

    /// <summary>Button pressed on ANY of this player's pads (a set of one for a joined player,
    /// every unclaimed device for player 1).</summary>
    private bool PadButton(JoyButton button)
    {
        foreach (int pad in Pads)
            if (Input.IsJoyButtonPressed(pad, button))
                return true;
        return false;
    }

    /// <summary>The largest-magnitude value of the axis across this player's pads — idle phantom
    /// devices read ~0 and never mask a real stick.</summary>
    private float PadAxis(JoyAxis axis)
    {
        float v = 0f;
        foreach (int pad in Pads)
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

    private bool RawAccept() =>
        KeyDown(Key.Enter) || KeyDown(Key.KpEnter) || KeyDown(Key.Space) || PadButton(JoyButton.A);

    private bool RawBack() => KeyDown(Key.Escape) || PadButton(JoyButton.B);

    /// <summary>Start is the join gesture, so it is pad-only: the keyboard is always player 1,
    /// who is joined from the start and has nothing to join.</summary>
    private bool RawStart() => PadButton(JoyButton.Start);

    /// <summary>Whether an unbound pad is pressing Start — the join gesture. Static because the
    /// pad has no player (and therefore no <see cref="MenuInput"/>) until it joins; the caller
    /// edge-detects per device.</summary>
    public static bool JoinPressed(int pad) => Input.IsJoyButtonPressed(pad, JoyButton.Start);
}
