using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A body cockpit's airframe twins. Each drawn body mesh is paired with a hidden copy under the cockpit
/// panel, which the first-person draw shows in the original's place. The flat cockpit pass's twins share
/// the original's mesh on copies of its materials (<see cref="Flat"/>). The stereo interior's are built
/// through its shader (<see cref="Stereo"/>). Both leave out <c>cockpit*</c>, the rig's hide list and
/// any mesh worn under an override material (a flare). Both draws run the same per-frame pose and
/// visibility copy, <see cref="Mirror"/>.
/// </summary>
public static class BodyTwins
{
    /// <summary>The flat pass's twins of <paramref name="built"/>'s body, each hidden under <paramref name="panel"/>.
    /// A twin shares its original's mesh and <c>node_bias</c>. It wears copies of its materials, since the pass
    /// writes <c>light_origin</c> on them, one per source material in <paramref name="copies"/>.</summary>
    public static List<BodyTwin> Flat(Node3D built, Node3D panel, IEnumerable<string> hide,
        Dictionary<ShaderMaterial, ShaderMaterial> copies)
    {
        var twins = new List<BodyTwin>();
        foreach (var (mi, _, _) in Drawn(built, HideSet(hide)))
        {
            if (mi.Mesh is not { } mesh)
            {
                continue;
            }

            var twin = new MeshInstance3D { Name = $"twin{twins.Count}", Mesh = mesh, CastShadow = mi.CastShadow, Visible = false };
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is ShaderMaterial m)
                {
                    twin.SetSurfaceOverrideMaterial(s, copies.TryGetValue(m, out var copy) ? copy : copies[m] = ShaderTwins.Copy(m));
                }
            }

            twin.SetInstanceShaderParameter(CockpitTransplant.NodeBias, mi.GetInstanceShaderParameter(CockpitTransplant.NodeBias));
            panel.AddChild(twin);
            twins.Add(new BodyTwin(mi, twin));
        }

        return twins;
    }

    /// <summary>The stereo interior's twins of <paramref name="built"/>'s body under <paramref name="holder"/>, at
    /// each original's plane-frame pose. Each takes the mesh of the same gamez node and ordinal in <paramref name="copy"/>,
    /// the airframe built again by <paramref name="scene"/>. A mesh <paramref name="scene"/> did not place in
    /// the stereo interior (<see cref="SceneBuilder.PlacesInInterior"/>) has no twin.</summary>
    public static List<BodyTwin> Stereo(Node3D built, Node3D copy, SceneBuilder scene, IEnumerable<string> hide, Node3D holder)
    {
        var meshes = new Dictionary<int, List<Mesh>>();
        Meshes(copy, meshes);
        var twins = new List<BodyTwin>();
        foreach (var (mi, node, ordinal) in Drawn(built, HideSet(hide)))
        {
            if (meshes.GetValueOrDefault(node) is not { } list || ordinal >= list.Count || !Lifts(list[ordinal], scene))
            {
                continue;
            }

            var twin = new MeshInstance3D
            {
                Name = $"twin{twins.Count}",
                Mesh = list[ordinal],
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Transform = NodeFrame.Under(mi, built),
            };
            twin.SetInstanceShaderParameter(CockpitTransplant.NodeBias, mi.GetInstanceShaderParameter(CockpitTransplant.NodeBias));
            holder.AddChild(twin);
            twins.Add(new BodyTwin(mi, twin));
        }

        return twins;
    }

    /// <summary>Gives each twin its original's visibility and its pose on <paramref name="airframe"/>, read in
    /// the frame of a panel mounted there at <paramref name="mount"/>. A twin whose original went, or left the
    /// airframe, hides.</summary>
    public static void Mirror(IEnumerable<BodyTwin> twins, Node3D airframe, in Transform3D mount)
    {
        var fromAirframe = mount.AffineInverse();
        foreach (var (original, twin) in twins)
        {
            if (!GodotObject.IsInstanceValid(twin))
            {
                continue;
            }

            twin.Visible = GodotObject.IsInstanceValid(original) && original.IsVisibleInTree() && airframe.IsAncestorOf(original);
            if (twin.Visible)
            {
                twin.Transform = fromAirframe * NodeFrame.Under(original, airframe);
            }
        }
    }

    private static HashSet<string> HideSet(IEnumerable<string> hide) => new(hide, StringComparer.OrdinalIgnoreCase);

    // Every drawn body mesh outside cockpit* and the hide list, not under an override material. Each
    // comes with its gamez node's index and its ordinal among that node's meshes.
    private static IEnumerable<(MeshInstance3D Instance, int Node, int Ordinal)> Drawn(Node node, HashSet<string> hide)
    {
        int ordinal = 0;
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Name.ToString() == "mesh")
            {
                if (node.HasMeta(AnimRuntime.IndexMeta) && mi.MaterialOverride == null)
                {
                    yield return (mi, (int)node.GetMeta(AnimRuntime.IndexMeta), ordinal);
                }

                ordinal++;
            }
            else if (child is Node3D n3d && !hide.Contains(AnimRuntime.NameOf(n3d))
                && !AnimRuntime.NameOf(n3d).StartsWith("cockpit", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var drawn in Drawn(n3d, hide))
                {
                    yield return drawn;
                }
            }
        }
    }

    // Each gamez node's drawn meshes, in child order, keyed by the node's index.
    private static void Meshes(Node node, Dictionary<int, List<Mesh>> into)
    {
        if (node.HasMeta(AnimRuntime.IndexMeta))
        {
            var list = new List<Mesh>();
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D { Mesh: { } mesh } mi && mi.Name.ToString() == "mesh")
                {
                    list.Add(mesh);
                }
            }

            into[(int)node.GetMeta(AnimRuntime.IndexMeta)] = list;
        }

        foreach (var child in node.GetChildren())
        {
            Meshes(child, into);
        }
    }

    // Whether every surface of a twin mesh wears the stereo interior's placement.
    private static bool Lifts(Mesh mesh, SceneBuilder scene)
    {
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            if (!scene.PlacesInInterior(mesh.SurfaceGetMaterial(s)))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>One drawn body mesh and the twin a body cockpit's first-person draw shows in its place.</summary>
public sealed record BodyTwin(MeshInstance3D Original, MeshInstance3D Twin);

/// <summary>A flat body cockpit as the cockpit pass draws it: the eye on the model root (the
/// <c>cockpit_camera</c> marker) and the body's twins (<see cref="BodyTwins.Flat"/>).</summary>
public sealed record FlatBodyTwins(Vector3 Eye, IReadOnlyList<BodyTwin> Twins);
