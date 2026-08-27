namespace CSVM.Flight;

/// <summary>The rocket trigger's consumed-press latch. A cutscene skip or a pause-menu
/// Resume can hand flight input straight back on the same frame the button that confirmed it (F /
/// gamepad A) is still physically down; without this, <see cref="FlightController"/>'s edge-free
/// button read sees that still-down press as a fresh pull and launches a rocket nobody meant to
/// fire. <see cref="ArmIfHeld"/> is called at the moment input comes back; <see cref="Read"/> is
/// the trigger's own read every frame after. Pure state, nothing else calls it: proven directly by
/// its own unit tests rather than through <c>FlightController</c>, the way
/// <see cref="WeaponCursor"/> is proven through <c>FireControl</c>. Public (not internal, unlike
/// <see cref="WeaponCursor"/>) so its own unit tests can construct and drive it directly; this
/// project carries no InternalsVisibleTo to <c>CSVM.Tests</c>.</summary>
public sealed class RocketTriggerLatch
{
    private bool _armed;

    /// <summary>Arms the latch when <paramref name="held"/> is true (the button reads down this
    /// frame); a no-op when it is not, since there is nothing that press could still be.</summary>
    public void ArmIfHeld(bool held)
    {
        if (held)
            _armed = true;
    }

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
