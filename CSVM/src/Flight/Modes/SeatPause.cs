using System;
using CSVM.Utils;

namespace CSVM.Flight.Modes;

/// <summary>One frame of <see cref="SeatPause.Poll"/>. It says whether the clock is halted and
/// whether that changed this frame, since the engine loops hold their sample position through the
/// halt. It also says whether a board came down, since the press that dismissed it must not also
/// fly.</summary>
public readonly record struct PauseFrame(bool Halted, bool HaltChanged, bool BoardCleared);

/// <summary>One flight seat's pause key and the halt it mirrors into the shared clock. It holds
/// the key's edge and the two screens that silence it, photo mode and the pause's options leaf. It
/// also holds the network sheet that stands over a running flight. It is polled once per rendered
/// frame, never from the sim step, since a halted sim takes no steps and could never resume itself.
/// With a shared <see cref="PauseState"/> only the player who paused may resume.
/// <see cref="Poll"/> reports the two edges its host performs: the audio's hold and the re-entry
/// latch.</summary>
public sealed class SeatPause
{
    private readonly Func<bool> _pauseHeldNow;
    private bool _pausePrev;
    private bool _haltPrev;
    private bool _boardPrev;

    /// <summary>Builds the key over <paramref name="pauseHeldNow"/>, the bare pause control as the
    /// hands hold it at the moment of the call, past every gate. A leaving screen seeds the edge from
    /// it.</summary>
    public SeatPause(Func<bool> pauseHeldNow)
    {
        ArgumentNullException.ThrowIfNull(pauseHeldNow);
        _pauseHeldNow = pauseHeldNow;
    }

    /// <summary>This pane is in photo mode: the session owns its camera and its HUD is hidden. The
    /// pause key is silent, so Escape means "leave photo mode" and nothing else. Driven by the
    /// session through <see cref="BeginPhotoMode"/> and <see cref="EndPhotoMode"/>.</summary>
    public bool InPhotoMode { get; private set; }

    /// <summary>The options leaf stands over this session's pause. The pause key is silent for the
    /// duration, so Escape means "leave the page" and nothing else. Driven by the session through
    /// <see cref="BeginPauseLeaf"/> and <see cref="EndPauseLeaf"/>.</summary>
    public bool InPauseLeaf { get; private set; }

    /// <summary>A network pause's sheet is up over a flight that keeps running. The seat is then
    /// wholly held and the look controls are muted, so the sheet's menu keys fly nothing.</summary>
    public bool SheetOverFlight { get; private set; }

    /// <summary>Whether a screen over the flight owns the keyboard, which silences every key read
    /// beside this one.</summary>
    public bool Silenced => InPhotoMode || InPauseLeaf;

    /// <summary>Enter photo mode: the pause key goes silent for the duration.</summary>
    public void BeginPhotoMode() => InPhotoMode = true;

    /// <summary>Leave photo mode, seeding the key's edge from the control as it is now. ⚠ Do not
    /// clear the flag alone. The edge read false for the whole mode. The Escape still under the
    /// player's finger would then read as a fresh press and unpause the session behind it.</summary>
    public void EndPhotoMode()
    {
        InPhotoMode = false;
        _pausePrev = _pauseHeldNow();
    }

    /// <summary>Enter the options leaf over the pause: the pause key goes silent for the
    /// duration.</summary>
    public void BeginPauseLeaf() => InPauseLeaf = true;

    /// <summary>Leave the options leaf, seeding the key's edge for the reason
    /// <see cref="EndPhotoMode"/> does. The page is left with Escape, and that same Escape would
    /// otherwise resume the mission the leaf was opened from.</summary>
    public void EndPauseLeaf()
    {
        InPauseLeaf = false;
        _pausePrev = _pauseHeldNow();
    }

    /// <summary>One frame of the key, P, Esc or pad Start, one toggle per press. Esc opens the pause
    /// board rather than leaving the flight. <paramref name="allowPause"/> is the seat's own gate,
    /// false on an AI or a bare suite rig. A <paramref name="remoteOwned"/> seat never stands under
    /// the sheet, since its owner's machine holds it instead.</summary>
    public PauseFrame Poll(bool allowPause, bool pauseHeld, PauseState? shared, int playerIndex,
        GameClock? clock, bool remoteOwned)
    {
        bool pressed = allowPause && !Silenced && pauseHeld;
        if (pressed && !_pausePrev)
        {
            if (shared != null)
                shared.TryToggle(playerIndex);
            else if (clock != null)
                clock.Halted = !clock.Halted;
        }
        _pausePrev = pressed;
        bool boardUp = shared?.Halted ?? (clock?.Halted ?? false);
        bool halted = shared?.ClockHeld ?? boardUp;
        if (clock != null)
            clock.Halted = halted;
        SheetOverFlight = boardUp && !halted && !remoteOwned;
        bool haltChanged = halted != _haltPrev;
        _haltPrev = halted;
        // Clearing is the pause board's own re-entry point: the B or Enter that dismissed the
        // sheet can still be down on this very frame.
        bool boardCleared = boardUp != _boardPrev && !boardUp;
        _boardPrev = boardUp;
        return new PauseFrame(halted, haltChanged, boardCleared);
    }
}
