using System;

namespace CSVM.Bindings;

/// <summary>A control held through a handover reads released until it reads up. An armed action
/// disarms the moment it reads up, so the next real press reads through. Flight's re-entry latch
/// arms its discrete commands on a cutscene skip or a pause dismiss
/// (<c>Flight/Airframe/FlightReentryLatch.cs</c>). Each reader arms every action but the absolute
/// lever as the load gate opens (<see cref="PlayerActions"/>). Pure state, public for the unit tests.</summary>
public sealed class HandoverLatch
{
    private readonly bool[] _armed = new bool[Enum.GetValues<InputAction>().Length];
    private int _count;

    /// <summary>Whether any action is still armed, so a caller can skip its reads.</summary>
    public bool Any => _count > 0;

    /// <summary>Arms one action, whatever the controls read now.</summary>
    public void Arm(InputAction action)
    {
        if (!_armed[(int)action])
        {
            _armed[(int)action] = true;
            _count++;
        }
    }

    /// <summary>This frame's reading for one action: released while it is armed and
    /// <paramref name="held"/> stays true. A false <paramref name="held"/> disarms it.</summary>
    public bool Read(InputAction action, bool held)
    {
        if (!held)
        {
            if (_armed[(int)action])
            {
                _armed[(int)action] = false;
                _count--;
            }

            return false;
        }

        return !_armed[(int)action];
    }
}
