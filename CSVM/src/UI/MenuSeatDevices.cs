using System;
using System.Collections.Generic;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The pad side of the shared player setup, for any presentation: which pad seat 0 claimed by
/// steering with it, the join gesture (Start on an unclaimed pad, edge-detected per device),
/// hotplug (a vanished pad unjoins its seat, seat 0's poller is handed every pad nobody holds)
/// and the flight binding a seat's source carries. A joined pad becomes a
/// <see cref="BuiltInSeat"/> over a poller bound to that one pad. The feature never sees a pad
/// index; this is where a source's own detail is read. ⚠ Seat 0 reads every unclaimed pad until
/// it claims one, never <c>pads[0]</c>: phantom devices occupy the early slots.
/// </summary>
public sealed class MenuSeatDevices
{
    private readonly MenuInput _player1;
    private readonly PlayerSetupFeature _setup;
    // Previous-frame Start state of every connected pad, for edge-detecting the join gesture on
    // pads that have no seat (and therefore no poller) yet.
    private readonly Dictionary<int, bool> _joinPrev = new();

    /// <summary>Over seat 0's poller (<paramref name="player1"/>, the keyboard plus the pads it
    /// borrows) and the feature the seats live in.</summary>
    public MenuSeatDevices(MenuInput player1, PlayerSetupFeature setup)
    {
        _player1 = player1 ?? throw new ArgumentNullException(nameof(player1));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    /// <summary>The pad seat 0 claimed by driving a screen with it, or -1 while it is on the
    /// keyboard and every connected pad is still free to join.</summary>
    public int P1Pad { get; private set; } = -1;

    /// <summary>The single pad a seat's source is bound to, or -1: a pad seat is a
    /// <see cref="BuiltInSeat"/> over a one-pad poller, and any other source has no pad of its own.</summary>
    public static int PadOf(IMenuInputSource source) => source is BuiltInSeat seat ? seat.Input.Pad : -1;

    /// <summary>The poller behind a seat's source, or null for a source that has none (a device-less
    /// seat, or a seat wearing a source of another kind). It is what a screen reading the seat's own
    /// devices needs, and the same detail read in the same one place as the pad above.</summary>
    public static MenuInput? PollerOf(IMenuInputSource source) => (source as BuiltInSeat)?.Input;

    /// <summary>The pads a seat flies with, the binding a launch carries: seat 0's poller's set,
    /// a joined pad's one device, nothing for a device-less seat.</summary>
    public IReadOnlyList<int> FlightPads(PlayerSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        if (_setup.Seats.Count > 0 && ReferenceEquals(_setup.Seats[0], seat))
        {
            return _player1.Pads ?? Array.Empty<int>();
        }

        int pad = PadOf(seat.Source);
        return pad >= 0 ? new[] { pad } : Array.Empty<int>();
    }

    /// <summary>Whether a pad already belongs to a seat: seat 0's claimed pad or a joined pad.
    /// Before the claim, seat 0 only borrows its pads, so any of them can still join.</summary>
    public bool IsClaimed(int pad)
    {
        if (pad == P1Pad)
        {
            return true;
        }

        var seats = _setup.Seats;
        for (int i = 1; i < seats.Count; i++)
        {
            if (PadOf(seats[i].Source) == pad)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reconciles the seats with the live pad roster: a seat whose pad disconnected
    /// leaves, a claimed pad that vanished frees seat 0, and seat 0's poller is bound to its
    /// claimed pad or to every unclaimed one. Returns whether anything changed.</summary>
    public bool Sync()
    {
        var connected = Pads.Connected();
        bool dirty = false;
        var seats = _setup.Seats;
        for (int i = seats.Count - 1; i >= 1; i--)
        {
            int pad = PadOf(seats[i].Source);
            if (pad >= 0 && !connected.Contains(pad))
            {
                GD.Print($"launchscreen: P{i + 1}'s pad {pad} disconnected — player left");
                _setup.Unjoin(seats[i]);
                dirty = true;
            }
        }

        if (P1Pad >= 0 && !connected.Contains(P1Pad))
        {
            GD.Print($"launchscreen: P1's pad {P1Pad} disconnected — back to keyboard + any free pad");
            P1Pad = -1;
            dirty = true;
        }

        var free = new List<int>(connected.Count);
        if (P1Pad >= 0)
        {
            free.Add(P1Pad);
        }
        else
        {
            foreach (int pad in connected)
            {
                if (!IsClaimed(pad))
                {
                    free.Add(pad);
                }
            }
        }

        // Seat 0 always binds an explicit set here; null (every connected pad) is the in-session
        // reading, which the menu never uses. A pad that just changed hands is primed, not read.
        var bound = _player1.Pads ?? Array.Empty<int>();
        if (!SameSet(free, bound))
        {
            _player1.Pads = free.ToArray();
            _player1.Prime();
            dirty = true;
        }

        return dirty;
    }

    /// <summary>Seeds the per-pad join edges from the current state, so a Start held while a
    /// screen with joining appears does not join a seat at once.</summary>
    public void PrimeJoins()
    {
        _joinPrev.Clear();
        foreach (int pad in Pads.Connected())
        {
            _joinPrev[pad] = MenuInput.JoinPressed(pad);
        }
    }

    /// <summary>Start on an unclaimed pad joins a seat over that pad, primed, while a seat is
    /// free. Returns whether anyone joined. The caller decides on which screens joining is open.</summary>
    public bool ScanJoins()
    {
        bool dirty = false;
        foreach (int pad in Pads.Connected())
        {
            bool pressed = MenuInput.JoinPressed(pad);
            _joinPrev.TryGetValue(pad, out bool prev);
            _joinPrev[pad] = pressed;
            if (!pressed || prev || IsClaimed(pad) || _setup.Seats.Count >= PlayerSetupFeature.MaxSeats)
            {
                continue;
            }

            var input = new MenuInput { Pads = new[] { pad } };
            input.Prime();
            if (_setup.Join(new BuiltInSeat(input)) != null)
            {
                // After the join, which is what decides the seat's player number and therefore
                // which saved keymap this pad navigates on.
                input.LoadSavedKeymap(_setup.Seats.Count);
                GD.Print($"launchscreen: P{_setup.Seats.Count} joined on pad {pad} \"{Input.GetJoyName(pad)}\"");
                dirty = true;
            }
        }

        return dirty;
    }

    /// <summary>Pins seat 0 to whichever pad it is steering with, once, so by the time joining
    /// opens every other pad is unambiguously a joiner. Steering with the keyboard claims nothing.
    /// Returns whether a claim was made.</summary>
    public bool ClaimP1Pad()
    {
        int pad = _player1.LastActivePad;
        if (P1Pad >= 0 || pad < 0)
        {
            return false;
        }

        P1Pad = pad;
        GD.Print($"launchscreen: P1 claimed pad {pad} \"{Input.GetJoyName(pad)}\" (other pads join at aircraft select)");
        return true;
    }

    private static bool SameSet(List<int> free, int[] bound)
    {
        if (free.Count != bound.Length)
        {
            return false;
        }

        for (int i = 0; i < free.Count; i++)
        {
            if (free[i] != bound[i])
            {
                return false;
            }
        }

        return true;
    }
}
