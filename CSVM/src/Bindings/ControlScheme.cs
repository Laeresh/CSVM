namespace CSVM.Bindings;

/// <summary>How a seat's stick flies the aeroplane, a remake-only choice each player makes on the
/// CONTROLS page (<see cref="BindingProfile.ControlScheme"/>). An enum rather than a switch so a
/// later scheme has a place to go. docs/controls.md, "Assisted controls and the level chase camera".
/// </summary>
public enum ControlScheme
{
    /// <summary>The original's controls: the stick works aileron and elevator directly.</summary>
    Original,

    /// <summary>The stick commands a turn and a climb, and an assist works the control surfaces.</summary>
    Assisted,
}

/// <summary>How a seat's chase view follows the aeroplane, a remake-only choice beside
/// <see cref="ControlScheme"/> (<see cref="BindingProfile.ChaseCamera"/>). The cockpit and nose
/// views and the VR chase ignore it.</summary>
public enum ChaseCamera
{
    /// <summary>The original's chase rig, banking with the aeroplane.</summary>
    Original,

    /// <summary>The chase rig with the bank taken out, keeping its pitch.</summary>
    Level,
}
