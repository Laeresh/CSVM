using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Deflects the flying aircraft's control surfaces (ailerons, elevators, rudders)
/// with stick input — PropAnimator-style: a flat list of hinged nodes the flight
/// loop advances each frame. The original engine drives these procedurally (no zrdr
/// anim defines deflection), so the max angles and slew rate are TUNE, validated
/// visually against the original.
///
/// Unlike the props' incremental spin, deflection is an absolute pose: each surface
/// stores its build-time local basis and gets <c>Basis = base · Rot(hingeAxis,
/// angle)</c>, so it tracks the input without accumulating. The per-surface sign
/// bakes three factors: the stick convention (ailerons opposite per side; elevator
/// and rudder trailing edges deflect against the commanded rotation, as the real
/// linkages do), a canard flip (the Bloodhawk's elevators sit on the NOSE — pull
/// deflects them trailing-edge DOWN, the lift-raising direction, opposite a
/// conventional tail), and a frame flip for hinge groups mounted rotated (the same
/// Bloodhawk canard groups have yaw π, so their local X points left) — detected
/// from the accumulated hinge-axis direction in the plane model's frame.
/// </summary>
public sealed class ControlSurfaceAnimator
{
    private readonly struct Surface
    {
        public readonly Node3D Node;
        public readonly Basis BaseBasis;  // build-time local basis; deflection composes on top
        public readonly Vector3 Axis;     // hinge axis in the node's local frame
        public readonly int Channel;      // 0 pitch, 1 roll, 2 yaw
        public readonly float SignedMaxRad; // input [-1,1] → hinge angle, all sign factors baked in
        public Surface(Node3D node, Vector3 axis, int channel, float signedMaxRad)
        {
            Node = node;
            BaseBasis = node.Basis;
            Axis = axis;
            Channel = channel;
            SignedMaxRad = signedMaxRad;
        }
    }

    private const float MaxAileronDeg = 20f;  // TUNE: full-stick deflection
    private const float MaxElevatorDeg = 20f; // TUNE
    private const float MaxRudderDeg = 20f;   // TUNE
    private const float SlewPerSec = 3f;      // TUNE: normalized deflection units/s
                                              // (center → full stop in ~1/3 s)
    private const float CanardMaxZ = -1f;     // hinge this far toward the nose (−Z) = canard
                                              // (Bloodhawk elevators at z −4.2; every
                                              // conventional tail surface sits at z ≥ 0.19)

    private readonly List<Surface> _surfaces;
    private Vector3 _defl; // slewed deflection (pitch, roll, yaw), each in [-1,1]

    public int Count => _surfaces.Count;

    private ControlSurfaceAnimator(List<Surface> surfaces) => _surfaces = surfaces;

    /// <summary>Walks the built plane tree and collects every control-surface node;
    /// null if the model has none (the autogyro has only its two ailerons — absence
    /// of any kind is normal and per-surface).</summary>
    public static ControlSurfaceAnimator? Build(Node3D planeRoot)
    {
        var surfaces = new List<Surface>();
        foreach (var child in planeRoot.GetChildren())
            Collect(child, Transform3D.Identity, surfaces);
        return surfaces.Count > 0 ? new ControlSurfaceAnimator(surfaces) : null;
    }

    private static void Collect(Node node, Transform3D parentAcc, List<Surface> surfaces)
    {
        var acc = parentAcc;
        if (node is Node3D n3d)
        {
            acc = parentAcc * n3d.Transform; // pose in the plane model's frame
            var kind = ControlSurfaces.Classify(n3d.Name);
            if (kind != ControlSurfaces.Kind.None)
            {
                var axis = ControlSurfaces.HingeAxis(kind);
                // Hinge groups can be mounted rotated (the Bloodhawk's canard groups
                // carry yaw π): if the hinge axis ends up pointing against its
                // canonical plane-frame direction, flip so the world-space deflection
                // sign is uniform across the fleet.
                float d = (acc.Basis * axis).Dot(axis);
                float flip = d < 0f ? -1f : 1f;
                float sign; float maxDeg; int channel;
                switch (kind)
                {
                    // Roll + = bank left: left aileron trailing edge up (−, TE rises
                    // for a negative hinge angle about +X), right TE down (+).
                    case ControlSurfaces.Kind.AileronLeft:
                        (channel, sign, maxDeg) = (1, -1f, MaxAileronDeg);
                        break;
                    case ControlSurfaces.Kind.AileronRight:
                        (channel, sign, maxDeg) = (1, 1f, MaxAileronDeg);
                        break;
                    // Pitch + = pull: conventional tail TE up (−); a canard raises
                    // the nose the other way — TE down (+).
                    case ControlSurfaces.Kind.Elevator:
                        bool canard = acc.Origin.Z < CanardMaxZ;
                        (channel, sign, maxDeg) = (0, canard ? 1f : -1f, MaxElevatorDeg);
                        break;
                    // Yaw + = nose left: rudder TE left (− about +Y).
                    default:
                        (channel, sign, maxDeg) = (2, -1f, MaxRudderDeg);
                        break;
                }
                surfaces.Add(new Surface(n3d, axis, channel, sign * flip * Mathf.DegToRad(maxDeg)));
            }
        }
        foreach (var child in node.GetChildren())
            Collect(child, acc, surfaces);
    }

    /// <summary>Slews the deflection toward this frame's stick input and poses every
    /// surface. Skip while crashed/paused (the pose then just holds).</summary>
    public void Advance(double delta, FlightInput input)
    {
        float step = SlewPerSec * (float)delta;
        _defl.X = Mathf.MoveToward(_defl.X, Mathf.Clamp(input.Pitch, -1f, 1f), step);
        _defl.Y = Mathf.MoveToward(_defl.Y, Mathf.Clamp(input.Roll, -1f, 1f), step);
        _defl.Z = Mathf.MoveToward(_defl.Z, Mathf.Clamp(input.Yaw, -1f, 1f), step);
        Apply();
    }

    /// <summary>Snap every surface back to neutral (respawn).</summary>
    public void Reset()
    {
        _defl = Vector3.Zero;
        Apply();
    }

    private void Apply()
    {
        foreach (var s in _surfaces)
            s.Node.Basis = s.BaseBasis * new Basis(s.Axis, _defl[s.Channel] * s.SignedMaxRad);
    }
}
