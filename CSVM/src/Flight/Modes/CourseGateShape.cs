using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// Every dimension a course gate is drawn with, in the gate's own frame (<see cref="CourseMotion.Rest"/>).
/// It covers the course pylons' places and taper, a ring's tube, and its balloons and tethers. The meshes
/// (<see cref="CourseVisuals"/>) read these, and so must anything that tests a touch against them, so the
/// two cannot drift apart. Engine-free. Every value is a remake-only look, tuned at the controls.
/// </summary>
public static class CourseGateShape
{
    /// <summary>A course pylon's base and top radius as fractions of its height.</summary>
    public const float PylonBaseShare = 0.09f;

    public const float PylonTopShare = 0.025f;

    /// <summary>The ripple's amplitude at a course pylon's top, metres; the base stands still.</summary>
    public const float RippleM = 0.25f;

    /// <summary>A ring's tube radius as a fraction of its diameter, at least <see cref="MinTubeM"/>.
    /// The tube lies outside the diameter, which is the clear opening.</summary>
    public const float TubeShare = 0.03f;

    public const float MinTubeM = 0.5f;

    /// <summary>A balloon's radius and its tether's length as fractions of the ring's diameter.</summary>
    public const float BalloonShare = 0.14f;

    public const float TetherShare = 0.45f;

    /// <summary>A balloon's height over its width; it is a sphere stretched upright.</summary>
    public const float BalloonStretch = 1.15f;

    public const float TetherRadiusM = 0.08f;

    /// <summary>Each balloon's tether angle from straight up, degrees, in the ring's plane; one balloon each.</summary>
    public static readonly float[] BalloonAnglesDeg = { -40f, 0f, 40f };

    /// <summary>The base centre of course pylon <paramref name="side"/> (0 left, 1 right): local x is
    /// minus or plus half the width, on the gate's base.</summary>
    public static Vector3 PylonBase(CourseGate gate, int side) => new((side == 0 ? -0.5f : 0.5f) * gate.Width, 0f, 0f);

    /// <summary>The middle of the gate's opening: a pylon gate's half height over its base, a ring's centre.</summary>
    public static Vector3 OpeningCentre(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, 0.5f * gate.Height, 0f) : Vector3.Zero;

    public static float PylonBaseRadius(CourseGate gate) => PylonBaseShare * gate.Height;

    public static float PylonTopRadius(CourseGate gate) => PylonTopShare * gate.Height;

    public static float TubeRadius(CourseGate gate) => Mathf.Max(TubeShare * gate.Diameter, MinTubeM);

    /// <summary>The radius of the tube's centreline about the ring's centre.</summary>
    public static float TubeCentreline(CourseGate gate) => (0.5f * gate.Diameter) + TubeRadius(gate);

    public static float BalloonRadius(CourseGate gate) => BalloonShare * gate.Diameter;

    public static float TetherLength(CourseGate gate) => TetherShare * gate.Diameter;

    /// <summary>The unit way from the ring's centre to balloon <paramref name="i"/>, in the ring's plane.</summary>
    public static Vector3 BalloonWay(int i)
    {
        float angle = Mathf.DegToRad(BalloonAnglesDeg[i]);
        return new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
    }

    /// <summary>Where tether <paramref name="i"/> leaves the ring: the tube's outer edge.</summary>
    public static Vector3 TetherAnchor(CourseGate gate, int i) => BalloonWay(i) * ((0.5f * gate.Diameter) + (2f * TubeRadius(gate)));

    /// <summary>Balloon <paramref name="i"/>'s centre, at the tether's end plus its own half height.</summary>
    public static Vector3 BalloonCentre(CourseGate gate, int i) =>
        TetherAnchor(gate, i) + (BalloonWay(i) * (TetherLength(gate) + (BalloonStretch * BalloonRadius(gate))));
}
