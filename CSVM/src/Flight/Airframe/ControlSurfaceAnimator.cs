using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>Poses the flying aircraft's control surfaces from <see cref="ControlSurfaceMix"/>,
/// PropAnimator-style: a flat list of hinged nodes the flight loop advances each frame. Deflection
/// is an absolute pose, not an incremental spin: each surface stores its build-time local basis and
/// gets <c>Basis = base · Rot(hingeAxis, angle)</c>, so it tracks the input without accumulating.
/// The original drives this procedurally with no zrdr anim, over six node lists collected by name
/// (<c>l_aileronN</c>, <c>r_aileronN</c>, <c>l_elevatorN</c>, <c>r_elevatorN</c>, <c>l_rudderN</c>,
/// <c>r_rudderN</c>), one angle slot each. This class owns the node side of that: which slot a node
/// takes and which way its hinge faces. The angles themselves are the mix's.</summary>
public sealed class ControlSurfaceAnimator
{
    private const float CanardMaxZ = -1f;     // hinge this far toward the nose (−Z) = canard
                                              // (Bloodhawk elevators at z −4.2; every
                                              // conventional tail surface sits at z ≥ 0.19)

    // The fraction of a surface's chord behind its forward-most vertex that still counts as its
    // leading edge. A slightly raked edge still yields one hinge line.
    private const float LeadingEdgeBand = 0.02f;

    private readonly List<Surface> _surfaces;
    private ControlSurfaceMix _mix;

    private ControlSurfaceAnimator(List<Surface> surfaces) => _surfaces = surfaces;

    public int Count => _surfaces.Count;

    /// <summary>Walks the built plane tree and collects every control-surface node;
    /// null if the model has none (the autogyro has only its two ailerons, absence
    /// of any kind is normal and per-surface).</summary>
    public static ControlSurfaceAnimator? Build(Node3D planeRoot)
    {
        var surfaces = new List<Surface>();
        foreach (var child in planeRoot.GetChildren())
            Collect(child, Transform3D.Identity, surfaces, interior: false);
        return surfaces.Count > 0 ? new ControlSurfaceAnimator(surfaces) : null;
    }

    /// <summary>Where a cockpit-interior surface turns, in its own frame: the centroid of its
    /// mesh's leading edge (the forward-most vertices). Remake-only, in both presentations (#149).
    /// The interior's surface origins sit off their hinges, up to 0.67 m on the Warhawk's ailerons.
    /// Turning about the origin swings the whole surface out of the wing. Zero when the node
    /// carries no mesh.</summary>
    public static Vector3 LeadingEdge(Node3D surface)
    {
        var verts = new List<Vector3>();
        foreach (var mi in surface.GetChildren().OfType<MeshInstance3D>())
        {
            if (mi.Mesh is not ArrayMesh mesh)
                continue;
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                foreach (var v in mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    verts.Add(mi.Transform * v);
        }
        if (verts.Count == 0)
            return Vector3.Zero;
        float front = verts.Min(v => v.Z), chord = verts.Max(v => v.Z) - front;
        var edge = verts.Where(v => v.Z <= front + (LeadingEdgeBand * chord)).ToList();
        return edge.Aggregate(Vector3.Zero, (a, b) => a + b) / edge.Count;
    }

    /// <summary>Advances the six angle slots and poses every surface. Skip while crashed/paused
    /// (the pose then just holds).</summary>
    /// <param name="animate">The original's player-only guard, widened to every human pilot: an
    /// AI aircraft's slots are never written, so its surfaces stay where they are.</param>
    /// <param name="reverseAuthority">The reverse-authority factor at this airspeed
    /// (<see cref="FlightModel.ReverseAuthorityAt"/>), scaling the RUDDER target only.</param>
    public void Advance(double delta, FlightInput input, bool animate, float reverseAuthority = 1f)
    {
        _mix.Advance((float)delta, input, reverseAuthority, animate);
        Apply();
    }

    /// <summary>Snap every surface back to neutral (respawn).</summary>
    public void Reset()
    {
        _mix.Reset();
        Apply();
    }

    private static void Collect(Node node, Transform3D parentAcc, List<Surface> surfaces, bool interior)
    {
        var acc = parentAcc;
        if (node is Node3D n3d)
        {
            acc = parentAcc * n3d.Transform; // pose in the plane model's frame
            interior |= AnimRuntime.NameOf(n3d).StartsWith("cockpit", System.StringComparison.OrdinalIgnoreCase);
            var kind = ControlSurfaces.Classify(n3d.Name);
            if (kind != ControlSurfaces.Kind.None)
            {
                var axis = ControlSurfaces.HingeAxis(kind);
                // The slot angle already carries the original's sign. Two frame flips remain: a
                // rotated mount (canard groups carry yaw π), and a nose-mounted elevator, which
                // raises the nose the other way. Account for both before touching any sign.
                float d = (acc.Basis * axis).Dot(axis);
                float scale = d < 0f ? -1f : 1f;
                SurfaceSlot slot;
                switch (kind)
                {
                    case ControlSurfaces.Kind.AileronLeft:
                        slot = SurfaceSlot.AileronLeft;
                        break;
                    case ControlSurfaces.Kind.AileronRight:
                        slot = SurfaceSlot.AileronRight;
                        break;
                    case ControlSurfaces.Kind.ElevatorLeft:
                    case ControlSurfaces.Kind.ElevatorRight:
                        slot = kind == ControlSurfaces.Kind.ElevatorLeft
                            ? SurfaceSlot.ElevatorLeft : SurfaceSlot.ElevatorRight;
                        if (acc.Origin.Z < CanardMaxZ)
                            scale = -scale;
                        break;
                    default:
                        slot = SurfaceSlot.Rudder;
                        break;
                }

                // ⚠ The outside surfaces turn about their origin, as the original turns them.
                surfaces.Add(new Surface(n3d, axis, slot, scale, interior ? LeadingEdge(n3d) : Vector3.Zero));
            }
        }

        foreach (var child in node.GetChildren())
            Collect(child, acc, surfaces, interior);
    }

    private void Apply()
    {
        foreach (var s in _surfaces)
        {
            var turn = new Basis(s.Axis, _mix[s.Slot] * s.Scale);
            s.Node.Basis = s.BaseBasis * turn;
            // Turning about the pivot instead of the origin moves the origin by the difference.
            s.Node.Position = s.BasePosition + (s.BaseBasis * (s.Pivot - (turn * s.Pivot)));
        }
    }

    private readonly struct Surface
    {
        public readonly Node3D Node;
        public readonly Basis BaseBasis;  // build-time local basis; deflection composes on top
        public readonly Vector3 Axis;     // hinge axis in the node's local frame
        public readonly SurfaceSlot Slot;
        public readonly float Scale;      // the mount's frame flips, ±1
        public readonly Vector3 BasePosition; // build-time local position
        public readonly Vector3 Pivot;    // the hinge point in the node's own frame, zero = origin

        public Surface(Node3D node, Vector3 axis, SurfaceSlot slot, float scale, Vector3 pivot)
        {
            Node = node;
            BaseBasis = node.Basis;
            Axis = axis;
            Slot = slot;
            Scale = scale;
            BasePosition = node.Position;
            Pivot = pivot;
        }
    }
}
