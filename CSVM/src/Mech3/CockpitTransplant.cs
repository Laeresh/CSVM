using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A body cockpit's instruments: the original's gauge assemblies and sight glass. They leave the built
/// <c>cockpit1</c> for the airframe, at the rig's poses (<see cref="RigCockpit.Gauges"/>).
/// Each named assembly is re-parented as built, so <c>Flight.Hud.CockpitGauges</c> binds the same
/// needles, lamps, readouts and materials. The glass, a polygon inside a larger mesh, is copied out
/// as a mesh of its own. An assembly the table does not name stays in the hidden interior. The pose
/// arithmetic is engine-free: <see cref="Posed"/> over <see cref="PlaneBuilder.InteriorToPlane"/>.
/// </summary>
public static class CockpitTransplant
{
    /// <summary>The panel node's name. ⚠ Keep the <c>cockpit</c> prefix: the walks that skip the
    /// interior by it must skip the panel. Its assemblies carry their own <c>markers</c>.</summary>
    public const string PanelName = "cockpit_panel";

    /// <summary>The panel's child holding the cut-loose sight glass, beside the <c>gauges</c> node.</summary>
    public const string GlassName = "glass";

    // The cross-node draw-order tie-break every built instance carries (SceneBuilder.NodeBiasOf).
    private const string NodeBias = "node_bias";

    /// <summary>An assembly's plane-frame transform under a rig pose: the pose's position, and its
    /// rotation (YXZ Euler degrees) and uniform scale where given. An absent field keeps
    /// <paramref name="original"/>'s, the assembly's apparent pose in <c>cockpit1</c>.</summary>
    public static Transform3D Posed(Transform3D original, RigGaugePose pose)
    {
        var rotation = pose.RotationDeg is { } deg
            ? Basis.FromEuler(deg * (Mathf.Pi / 180f), EulerOrder.Yxz)
            : original.Basis.Orthonormalized();
        var scale = pose.Scale is { } s ? Vector3.One * s : original.Basis.Scale;
        return new Transform3D(rotation * Basis.FromScale(scale), pose.Position);
    }

    /// <summary>The sight glass's own frame in the interior: unrotated, at the mean of its polygons'
    /// corners (<see cref="GunsightReticle.Find"/>). A rig's glass position is that centre. Null when
    /// the interior holds no glass.</summary>
    public static Transform3D? GlassFrame(IReadOnlyList<GunsightReticle.Glass> glasses)
    {
        var sum = Vector3.Zero;
        int corners = 0;
        foreach (var glass in glasses)
        {
            foreach (var corner in glass.Corners)
            {
                sum += corner;
                corners++;
            }
        }

        return corners == 0 ? null : new Transform3D(Basis.Identity, sum / corners);
    }

    /// <summary>Moves the assemblies <paramref name="cockpit"/> names out of the built
    /// <paramref name="interior"/> into a new hidden panel, and copies the glass out when named. Each
    /// takes its rig pose in the frame <paramref name="mount"/> puts the interior in. The panel holds
    /// a <c>gauges</c> node only when an assembly moved.</summary>
    /// <param name="glassFrame"><see cref="GlassFrame"/> of the same interior.</param>
    /// <param name="materials">The interior's textured materials, which name the glass's surfaces.</param>
    public static Node3D Build(Node3D interior, RigCockpit cockpit, Transform3D mount, Transform3D? glassFrame,
        IReadOnlyList<(ShaderMaterial Material, string TextureName)> materials)
    {
        var panel = new Node3D { Name = PanelName, Visible = false };
        int glassSurfaces = 0;
        if (cockpit.Gauges.TryGetValue(RigCockpit.GlassKey, out var glassPose) && glassFrame is { } frame)
        {
            var glass = new Node3D { Name = GlassName, Transform = Posed(mount * frame, glassPose) };
            glassSurfaces = CutGlass(interior, interior, frame, GlassMaterials(materials), glass);
            panel.AddChild(glass);
        }

        var gauges = FindNamed(interior, "gauges");
        Node3D? group = null;
        int moved = 0;
        foreach (var (name, pose) in cockpit.Gauges)
        {
            if (name.Equals(RigCockpit.GlassKey, StringComparison.OrdinalIgnoreCase)
                || gauges == null || Child(gauges, name) is not { } assembly)
            {
                continue;
            }

            var at = Posed(mount * PlaneRigPatch.PlaneFrame(assembly, interior), pose);
            if (group == null)
            {
                group = new Node3D { Name = "gauges" };
                panel.AddChild(group);
            }

            gauges.RemoveChild(assembly);
            group.AddChild(assembly);
            assembly.Transform = at;
            moved++;
        }

        Log.Info("world", $"cockpit: {moved} gauge assembly(ies) and {glassSurfaces} sight glass surface(s) moved onto the airframe");
        return panel;
    }

    private static HashSet<ulong> GlassMaterials(IReadOnlyList<(ShaderMaterial Material, string TextureName)> materials)
    {
        var ids = new HashSet<ulong>();
        foreach (var (material, texture) in materials)
        {
            if (GunsightReticle.IsGlass(texture))
            {
                ids.Add(material.GetInstanceId());
            }
        }

        return ids;
    }

    // Each surface wearing a glass material becomes a mesh of its own, placed in the glass's frame.
    // Its new instance carries the source's layers, shadow setting and node_bias.
    // ⚠ Never Duplicate() the source instance: under the separate render thread the process crashes.
    private static int CutGlass(Node node, Node3D interior, Transform3D frame, HashSet<ulong> glassIds, Node3D into)
    {
        int cut = 0;
        if (node is MeshInstance3D { Mesh: ArrayMesh mesh } instance)
        {
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (instance.GetActiveMaterial(s) is not { } material || !glassIds.Contains(material.GetInstanceId()))
                {
                    continue;
                }

                var own = new ArrayMesh();
                own.AddSurfaceFromArrays(mesh.SurfaceGetPrimitiveType(s), mesh.SurfaceGetArrays(s));
                own.SurfaceSetMaterial(0, material);
                var copy = new MeshInstance3D
                {
                    Name = instance.Name,
                    Mesh = own,
                    Layers = instance.Layers,
                    CastShadow = instance.CastShadow,
                    Transform = frame.AffineInverse() * PlaneRigPatch.PlaneFrame(instance, interior),
                };
                copy.SetInstanceShaderParameter(NodeBias, instance.GetInstanceShaderParameter(NodeBias));
                into.AddChild(copy);
                cut++;
            }
        }

        foreach (var child in node.GetChildren())
        {
            cut += CutGlass(child, interior, frame, glassIds, into);
        }

        return cut;
    }

    private static Node3D? Child(Node3D parent, string name)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Node3D n3d && AnimRuntime.NameOf(n3d).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }
        }

        return null;
    }

    // Depth-first, as PlaneRigPatch.CheckCockpit finds the gauges node on the gamez tree.
    private static Node3D? FindNamed(Node3D root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Node3D n3d)
            {
                continue;
            }

            if (AnimRuntime.NameOf(n3d).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }

            if (FindNamed(n3d, name) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }
}
