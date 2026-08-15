using System;

namespace CSVM.Flight;

/// <summary>
/// Splitscreen pause bookkeeping (PLAN-splitscreen-polish E43, `BL-373`): one instance shared by
/// every human rig in the session (assigned to <see cref="FlightController.PauseState"/> the same
/// way <see cref="VersusMatch"/> is), so any player's Start/P freezes the shared
/// <c>GameClock</c> for everybody, but only the player who paused may resume it — a second
/// player's press while paused is a no-op, not a steal.
/// </summary>
public sealed class PauseState
{
    /// <summary>Fires on every successful pause or resume — never on a rejected unpause attempt.</summary>
    public event Action? Changed;

    public bool Paused { get; private set; }

    /// <summary>The player who paused, or -1 when not paused.</summary>
    public int OwnerPlayerIndex { get; private set; } = -1;

    /// <summary>Not paused: pauses and claims ownership for <paramref name="playerIndex"/>. Paused:
    /// resumes only if <paramref name="playerIndex"/> is the owner. Returns whether the state
    /// changed.</summary>
    public bool TryToggle(int playerIndex)
    {
        if (!Paused)
        {
            Paused = true;
            OwnerPlayerIndex = playerIndex;
            Changed?.Invoke();
            return true;
        }
        if (playerIndex != OwnerPlayerIndex)
            return false;
        Paused = false;
        OwnerPlayerIndex = -1;
        Changed?.Invoke();
        return true;
    }
}
