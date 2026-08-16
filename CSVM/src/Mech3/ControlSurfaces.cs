using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Classifies an aircraft's control-surface mesh nodes by name and supplies each kind's hinge
/// axis: the deflecting mesh node (<c>l_aileron1</c>, <c>r_elevator1</c>, <c>l_rudder1</c>, or
/// the Fury's <c>l_rudder_rotate</c>) hangs under a hinge parent group whose transform places
/// and orients the hinge line.
/// ⚠ Parent hinge-group names deliberately do not classify; rotating both parent and child would
/// double the deflection. No zrdr anim defines deflection; angles and rates are TUNE constants
/// in <c>Flight.ControlSurfaceAnimator</c>, not data here.
/// </summary>
public static class ControlSurfaces
{
    // Digits are required on the bare rudder form: "l_rudder"/"r_rudder" without digits are hinge
    // parent groups (e.g. Fury, Peacemaker), not the deflecting mesh node.
    private static readonly Regex AileronRe = new("^([lr])_aileron[0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex ElevatorRe = new("^[lr]_elevator[0-9]*$", RegexOptions.IgnoreCase);
    private static readonly Regex RudderRe = new("^[lr]_rudder([0-9]+|_rotate)$", RegexOptions.IgnoreCase);

    public enum Kind { None, AileronLeft, AileronRight, Elevator, Rudder }

    public static Kind Classify(string name)
    {
        var ail = AileronRe.Match(name);
        if (ail.Success)
            return char.ToLowerInvariant(ail.Groups[1].Value[0]) == 'l'
                ? Kind.AileronLeft : Kind.AileronRight;
        if (ElevatorRe.IsMatch(name)) return Kind.Elevator;
        if (RudderRe.IsMatch(name)) return Kind.Rudder;
        return Kind.None;
    }

    /// <summary>The hinge axis in the surface node's local frame: spanwise X for
    /// ailerons/elevators, vertical Y for rudders. The parent group's rotation
    /// orients this into the true (swept/tilted) hinge line — e.g. the Bloodhawk's
    /// rudder hinge leans 12° aft via its lrudder1 group.</summary>
    public static Vector3 HingeAxis(Kind kind) =>
        kind == Kind.Rudder ? Vector3.Up : Vector3.Right;
}
