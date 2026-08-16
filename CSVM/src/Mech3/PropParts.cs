using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Classifies an aircraft's propeller/rotor subnodes by name and supplies each spinning kind's
/// local axis and rate: propeller discs turn about local Z, rotors about local Y. See
/// <c>docs/architecture.md</c> for the node-name scheme and <c>docs/formats/anim-definitions.md</c>
/// for the <c>XYZ_ROTATION</c> decode these rates come from.
/// ⚠ <c>nitropropN</c> is classified but never spun; no nitro system yet.
/// </summary>
public static class PropParts
{
    // Settled fact, not a TUNE: the authored spin_rotorN/spin_rotorNb rates from
    // plane_props.json (spinprops) and autogyro.json (agyro_rotors). PropAnimator converts
    // them the same way AnimDefs.Spin converts the ambient world's XYZ_ROTATION spins
    // (docs/formats/anim-definitions.md) — do not re-measure or treat these as tunable.
    private const float PropMainDegPerSec = -220f; // propN   : XYZ_ROTATION [0,0,-220]
    private const float PropGhostDegPerSec = 60f;  // propNb  : XYZ_ROTATION [0,0, 60]
    private const float RotorMainDegPerSec = 165f; // rotor1  : XYZ_ROTATION [0,165,0]
    private const float RotorGhostDegPerSec = -60f;// rotor1b : XYZ_ROTATION [0,-60,0]

    private static readonly Regex StaticRe = new("^static(prop|rotor)[0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex NitroRe = new("^nitroprop[0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex PropGhostRe = new("^prop[0-9]+b$", RegexOptions.IgnoreCase);
    private static readonly Regex PropMainRe = new("^prop[0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex RotorGhostRe = new("^rotor[0-9]+b$", RegexOptions.IgnoreCase);
    private static readonly Regex RotorMainRe = new("^rotor[0-9]+$", RegexOptions.IgnoreCase);

    public enum Kind { None, Static, Nitro, PropMain, PropGhost, RotorMain, RotorGhost }

    public static Kind Classify(string name)
    {
        if (StaticRe.IsMatch(name)) return Kind.Static;
        if (NitroRe.IsMatch(name)) return Kind.Nitro;
        if (PropGhostRe.IsMatch(name)) return Kind.PropGhost; // check "b" before the bare disc
        if (PropMainRe.IsMatch(name)) return Kind.PropMain;
        if (RotorGhostRe.IsMatch(name)) return Kind.RotorGhost;
        if (RotorMainRe.IsMatch(name)) return Kind.RotorMain;
        return Kind.None;
    }

    /// <summary>A blur layer the exterior viewer hides but flight spins (props + rotor);
    /// nitro stays hidden either way (no nitro system yet). Flight instead hides the inverse:
    /// the still <c>Static</c> disc and <c>Nitro</c> (see PlaneBuilder.Skip).</summary>
    public static bool IsDynamic(Kind kind) =>
        kind is Kind.PropMain or Kind.PropGhost or Kind.RotorMain or Kind.RotorGhost or Kind.Nitro;

    /// <summary>For a spinning kind, its local spin axis and rate (deg/s); false otherwise
    /// (Nitro included — it is never spun, just hidden).</summary>
    public static bool Spin(Kind kind, out Vector3 axis, out float degPerSec)
    {
        (axis, degPerSec) = kind switch
        {
            Kind.PropMain => (Vector3.Back, PropMainDegPerSec),   // local Z = nose axis
            Kind.PropGhost => (Vector3.Back, PropGhostDegPerSec),
            Kind.RotorMain => (Vector3.Up, RotorMainDegPerSec),   // local Y = overhead rotor
            Kind.RotorGhost => (Vector3.Up, RotorGhostDegPerSec),
            _ => (Vector3.Zero, 0f),
        };
        return degPerSec != 0f;
    }
}
