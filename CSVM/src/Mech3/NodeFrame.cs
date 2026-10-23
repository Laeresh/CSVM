using Godot;

namespace CSVM.Mech3;

/// <summary>A built node's transform in an ancestor's frame, from local transforms alone. It reads the
/// same inside a scene tree or out of one. Below an airframe's model root it is the plane
/// frame the rig, the transplant and the cockpit twins work in.</summary>
public static class NodeFrame
{
    /// <summary>The node's transform in <paramref name="ancestor"/>'s frame: every local below it, the
    /// node's own included. The walk stops at the first parent that is not a <see cref="Node3D"/>.</summary>
    public static Transform3D Under(Node3D node, Node3D ancestor)
    {
        var acc = Transform3D.Identity;
        for (Node3D? n = node; n != null && n != ancestor; n = n.GetParent() as Node3D)
        {
            acc = n.Transform * acc;
        }

        return acc;
    }
}
