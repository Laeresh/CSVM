namespace CSVM.Bindings;

/// <summary>How a seat flies and watches the aeroplane, a remake-only choice each player makes on
/// the CONTROLS page (<see cref="BindingProfile.ControlScheme"/>). The scheme also picks the chase
/// camera, because the assist law and the level chase only fly well together. An enum rather than a
/// switch so a later scheme has a place to go. docs/controls.md, "Assisted controls".</summary>
public enum ControlScheme
{
    /// <summary>The original's controls and the original's chase rig, banking with the aeroplane.</summary>
    Original,

    /// <summary>The stick commands a turn and a climb, and an assist works the control surfaces. The
    /// chase rig keeps its pitch with the bank taken out; the cockpit, nose and VR views ignore that.
    /// </summary>
    Assisted,
}
