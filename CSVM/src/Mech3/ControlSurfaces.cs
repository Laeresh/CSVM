using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Classifies an aircraft's control-surface mesh nodes by name and supplies each
/// kind's hinge axis. Every player plane names its movable surfaces the same way in
/// planes.zbd: the deflecting mesh node (<c>l_aileron1</c>, <c>r_elevator1</c>,
/// <c>l_rudder1</c>, or the Fury's <c>l_rudder_rotate</c>) hangs under a hinge
/// parent group (<c>lailer1</c>, <c>rt_elev</c>, <c>lrudder1</c>, <c>l_rudder</c>, …)
/// whose transform places and orients the hinge line; the mesh node itself is
/// usually identity with hinge-relative vertices (the surface extends aft of the
/// local origin). The parent-group names deliberately do NOT classify — rotating
/// both parent and child would double the deflection.
///
/// Hinge axes, corroborated against the mesh geometry (like PropParts): aileron and
/// elevator meshes are spanwise slabs (wide local X, thin Y, chord extending aft +Z)
/// — they hinge about local X; rudder meshes are vertical fins (flat in local X,
/// extending aft +Z) — they hinge about local Y. No zrdr anim or reader defines
/// deflection: the original engine drives these procedurally, so angles and rates
/// are TUNE constants in <c>Flight.ControlSurfaceAnimator</c>, not data here.
/// </summary>
public static class ControlSurfaces
{
    // Mesh-node names across the fleet: l/r_aileron1..3, l/r_elevator1/2 (a bare
    // l_elevator exists only on the never-built anim_bloodhawk — digits optional to
    // be safe), l/r_rudder1/2 and l/r_rudder_rotate (Fury). Digits are REQUIRED on
    // the bare rudder form: "l_rudder"/"r_rudder" without digits are hinge parent
    // groups (Fury, Peacemaker, Avenger), as are lailerN/railerN, l/r_elev,
    // lft/rt_elev, lrudderN/rrudderN (no underscore) and the Peacemaker's
    // r_rudder1b group (the trailing letter keeps it out of the rudder match).
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
