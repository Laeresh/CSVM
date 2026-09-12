namespace CSVM.Flight;

/// <summary>The rocket trigger's consumed-press latch. A cutscene skip or a pause-menu
/// Resume can hand flight input straight back on the same frame the button that confirmed it (F /
/// gamepad A) is still physically down; without this, <see cref="FlightController"/>'s edge-free
/// button read sees that still-down press as a fresh pull and launches a rocket nobody meant to
/// fire. <see cref="Arm"/> is called at the moment input comes back; <see cref="Read"/> is
/// the trigger's own read every frame after, and the first of those reads is what decides whether
/// a press was in progress. Pure state, driven by its own unit tests and, through the
/// re-entry points, by the flight input handoff suite, the way <see cref="WeaponCursor"/> is
/// proven through <c>FireControl</c>. Public (not internal, unlike
/// <see cref="WeaponCursor"/>) so its own unit tests can construct and drive it directly; this
/// project carries no InternalsVisibleTo to <c>CSVM.Tests</c>.</summary>
public sealed class RocketTriggerLatch
{
    private bool _armed;

    /// <summary>Arms the latch, whatever the button reads as input comes back.
    /// ⚠ Do not gate this on a button reading taken here. A re-entry point runs inside an input
    /// handler, whose cached action snapshot was resolved before the press that reached it. Such a
    /// reading answers "up" for the very press this latch exists to swallow. The first
    /// <see cref="Read"/> after the arm follows a fresh poll and disarms on its own when nothing
    /// was down.</summary>
    public void Arm() => _armed = true;

    /// <summary>This frame's trigger reading: released while armed and <paramref name="held"/>
    /// stays true (the consumed press has not let go yet), which also disarms the latch the moment
    /// <paramref name="held"/> goes false, so the next real pull reads through normally.</summary>
    public bool Read(bool held)
    {
        if (!held)
        {
            _armed = false;
            return false;
        }
        return !_armed;
    }
}
