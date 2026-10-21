using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Classifies an aircraft's control-surface mesh nodes by name and supplies each kind's hinge
/// axis: the deflecting mesh node (<c>l_aileron1</c>, <c>r_elevator1</c>, <c>l_rudder1</c>, or
/// the Fury's <c>l_rudder_rotate</c>) hangs under a hinge parent group whose transform places
/// and orients the hinge line.
/// ⚠ Parent hinge-group names deliberately do not classify; rotating both parent and child would
/// double the deflection. No zrdr anim defines deflection: the angles, the channel mixing and the
/// smoothing are decoded from the executable and live in <c>Flight.Airframe.ControlSurfaceMix</c>.
/// </summary>
public static class ControlSurfaces
{
    /// <summary>The meta a plane rig stamps on a surface it names: its <see cref="Kind"/> as an int.
    /// A node carrying it stands outside <see cref="InNumbering"/> (<see cref="PlaneRigPatch"/>).</summary>
    public const string KindMeta = "rig_surface_kind";

    /// <summary>The rig's hinge axis on that node, node-local and unit length; absent keeps <see cref="HingeAxis(Kind)"/>.</summary>
    public const string HingeAxisMeta = "rig_hinge_axis";

    /// <summary>The rig's hinge point on that node, node-local; absent keeps the node's origin.</summary>
    public const string HingePivotMeta = "rig_hinge_pivot";

    // Digits are required on the bare rudder form: "l_rudder"/"r_rudder" without digits are hinge
    // parent groups (e.g. Fury, Peacemaker), not the deflecting mesh node.
    private static readonly Regex AileronRe = new("^([lr])_aileron[0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex ElevatorRe = new("^([lr])_elevator[0-9]*$", RegexOptions.IgnoreCase);
    private static readonly Regex RudderRe = new("^[lr]_rudder([0-9]+|_rotate)$", RegexOptions.IgnoreCase);
    private static readonly Regex IndexRe = new("^(.*?)([0-9]+)$");

    // Side matters on the elevators too: the original mixes a differential roll term into them,
    // so left and right settle at different angles for the same stick.
    public enum Kind { None, AileronLeft, AileronRight, ElevatorLeft, ElevatorRight, Rudder }

    /// <summary>A built node's kind: the rig's when one is stamped on it, else its Godot name's.</summary>
    public static Kind Classify(Node3D node) =>
        node.HasMeta(KindMeta) ? (Kind)node.GetMeta(KindMeta).AsInt32() : Classify(node.Name);

    /// <summary>A built node's node-local hinge axis: the rig's when stamped, else its kind's.</summary>
    public static Vector3 HingeAxis(Node3D node, Kind kind) =>
        node.HasMeta(HingeAxisMeta) ? node.GetMeta(HingeAxisMeta).AsVector3() : HingeAxis(kind);

    public static Kind Classify(string name)
    {
        var ail = AileronRe.Match(name);
        if (ail.Success)
            return IsLeft(ail) ? Kind.AileronLeft : Kind.AileronRight;
        var elev = ElevatorRe.Match(name);
        if (elev.Success)
            return IsLeft(elev) ? Kind.ElevatorLeft : Kind.ElevatorRight;
        if (RudderRe.IsMatch(name)) return Kind.Rudder;
        return Kind.None;
    }

    /// <summary>Whether the original's list reaches this surface, counting up from 1 to its first
    /// missing index. The set is every surface name in the plane, since outside and <c>cockpit1</c>
    /// share one numbering. The remake-only names (digitless elevators, <c>_rotate</c> rudders)
    /// stand outside it, always driven and never filling a gap. Decode: docs/org/flightModel.md.
    /// </summary>
    public static bool InNumbering(string name, IReadOnlySet<string> planeNames)
    {
        var m = IndexRe.Match(name);
        if (!m.Success)
            return true;
        int index = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        if (index < 1)
            return false;
        for (int k = 1; k < index; k++)
            if (!planeNames.Contains(m.Groups[1].Value + k.ToString(CultureInfo.InvariantCulture)))
                return false;
        return true;
    }

    /// <summary>The hinge axis in the surface node's local frame: spanwise X for
    /// ailerons/elevators, vertical Y for rudders. The parent group's rotation
    /// orients this into the true (swept/tilted) hinge line, e.g. the Bloodhawk's
    /// rudder hinge leans 12° aft via its lrudder1 group.</summary>
    public static Vector3 HingeAxis(Kind kind) =>
        kind == Kind.Rudder ? Vector3.Up : Vector3.Right;

    private static bool IsLeft(Match m) => char.ToLowerInvariant(m.Groups[1].Value[0]) == 'l';
}
