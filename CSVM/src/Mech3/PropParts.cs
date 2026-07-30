using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Classifies an aircraft's propeller/rotor subnodes by name and supplies the spin
/// axis + rate the original engine drives them at. A plane model carries several
/// propeller representations under <c>dontmove</c>: a still disc (<c>staticpropN</c>),
/// the in-flight blur disc (<c>propN</c>) plus a slower counter-rotating ghost layer
/// (<c>propNb</c>), and a boost visual (<c>nitropropN</c>); the autogyro adds an
/// overhead rotor (<c>rotor1</c>/<c>rotor1b</c>, still <c>staticrotor1</c>).
///
/// The exterior viewer shows the static disc; free flight hides it and spins the
/// blur layers. The rates are the <c>XYZ_ROTATION</c> values from the original's
/// prop animations — <c>plane_props.json</c> ("warhawk" <c>spinprops</c>, shared by
/// every plane) and <c>autogyro.json</c> (<c>agyro_rotors</c>): propeller discs turn
/// about local Z (the nose axis), the autogyro's overhead rotor about local Y. Each
/// engine layers a fast disc (propN/rotorN) and a slower counter-rotating ghost
/// (propNb/rotorNb). The source units are undecoded; we treat them as degrees/second
/// (a visual TUNE — a blur disc reads as spinning at any smooth rate).
/// </summary>
public static class PropParts
{
    public enum Kind { None, Static, Nitro, PropMain, PropGhost, RotorMain, RotorGhost }

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
