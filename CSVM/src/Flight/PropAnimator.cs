using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Spins a flying aircraft's propeller/rotor blur discs each frame. Cheaper and
/// simpler than an AnimationPlayer: a flat list of (node, local axis, rate) that the
/// flight loop advances. Built from a plane model that PlaneBuilder rendered with
/// <c>spinningProps</c> (the blur layers present, the static disc hidden). See
/// <see cref="PropParts"/> for what spins and how fast.
/// </summary>
public sealed class PropAnimator
{
    private readonly List<Spinner> _spinners;

    private PropAnimator(List<Spinner> spinners) => _spinners = spinners;

    public int Count => _spinners.Count;

    /// <summary>Walks the built plane tree and collects every propeller/rotor blur node;
    /// null if the model has none (so callers can skip creating an animator).</summary>
    public static PropAnimator? Build(Node3D planeRoot)
    {
        var spinners = new List<Spinner>();
        Collect(planeRoot, spinners);
        return spinners.Count > 0 ? new PropAnimator(spinners) : null;
    }

    /// <summary>Advances every disc about its own local axis. <paramref name="speedFactor"/>
    /// scales the rate (typically throttle-derived); pass 0 to freeze (e.g. while crashed).</summary>
    public void Advance(double delta, float speedFactor)
    {
        float dt = (float)delta * speedFactor;
        if (dt == 0f)
            return;
        foreach (var s in _spinners)
            s.Node.RotateObjectLocal(s.Axis, s.RadPerSec * dt);
    }

    private static void Collect(Node node, List<Spinner> spinners)
    {
        if (node is Node3D n3d && PropParts.Spin(PropParts.Classify(n3d.Name), out var axis, out var deg))
            spinners.Add(new Spinner(n3d, axis, Mathf.DegToRad(deg)));
        foreach (var child in node.GetChildren())
            Collect(child, spinners);
    }

    private readonly struct Spinner
    {
        public readonly Node3D Node;
        public readonly Vector3 Axis;      // unit, in the node's local frame
        public readonly float RadPerSec;   // base rate; scaled by throttle at runtime
        public Spinner(Node3D node, Vector3 axis, float radPerSec)
        {
            Node = node;
            Axis = axis;
            RadPerSec = radPerSec;
        }
    }
}
