namespace CSVM.Flight;

/// <summary>The one seam through which a sim step reads this frame's pilot intent: a scripted hold
/// sequence, an AI pilot, or the keyboard/pad. Resolved once, at or before
/// <see cref="FlightController.Bind"/>, because which arm flies a given aircraft cannot change
/// afterward: <see cref="FlightController.Pilot"/> arrives only through the build DTO and
/// <see cref="FlightController.HoldSegments"/> is set once by the caller, both before the first
/// sim step. Ground-blow probing and the AI ground-blow write stay on the node after
/// <see cref="Read"/> returns: they need the live world, which a source does not have.</summary>
public interface IFlightInputSource
{
    /// <summary>This frame's stick/throttle read, before ground-blow probing is applied.</summary>
    FlightInput Read(float dt);
}

/// <summary>Plays back <see cref="FlightController.HoldSegments"/> — the scripted sequence a
/// playtest capture or an authored demo flies instead of a live stick.</summary>
public sealed class HoldInputSource : IFlightInputSource
{
    private readonly FlightController _owner;

    public HoldInputSource(FlightController owner) => _owner = owner;

    public FlightInput Read(float dt) => _owner.NextHoldInput(dt);
}

/// <summary>Reads <see cref="FlightController.Pilot"/>'s AI decision for this frame.</summary>
public sealed class PilotInputSource : IFlightInputSource
{
    private readonly FlightController _owner;

    public PilotInputSource(FlightController owner) => _owner = owner;

    public FlightInput Read(float dt) => _owner.NextPilotInput(dt);
}

/// <summary>Reads this pane's keyboard and gamepad.</summary>
public sealed class KeyboardInputSource : IFlightInputSource
{
    private readonly FlightController _owner;

    public KeyboardInputSource(FlightController owner) => _owner = owner;

    public FlightInput Read(float dt) => _owner.ReadKeyboard(dt);
}
