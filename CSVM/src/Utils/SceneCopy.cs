using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace CSVM.Utils;

/// <summary>
/// A copy of a node subtree, standing in for <see cref="Node.Duplicate"/>. Duplicating a
/// <see cref="GeometryInstance3D"/> builds its property list, which asks the rendering server for
/// its instance uniforms. Under the separate render thread that call is queued holding a pointer
/// into the asking frame. The render thread later writes into memory the frame has left. This
/// copy reads a geometry node's properties from its class, then its metadata, per-surface
/// materials and blend shapes; other nodes copy through their own list.
/// ⚠ Never <c>Duplicate()</c> a subtree holding geometry while the game runs; copy it here.
/// </summary>
public static class SceneCopy
{
    // Stored properties Duplicate itself does not carry across, or that this copy sets itself.
    private static readonly HashSet<string> Skipped = new() { "script", "owner", "name" };

    /// <summary>A copy of <paramref name="source"/> and every non-internal child under it, sharing
    /// its resources and outside the tree. Instance uniforms stay behind, as they do under
    /// <c>Duplicate</c>.</summary>
    public static T Of<T>(T source)
        where T : Node => (T)Copy(source);

    private static Node Copy(Node source)
    {
        var copy = New(source);
        var properties = source is GeometryInstance3D
            ? ClassDB.ClassGetPropertyList(source.GetClass())
            : source.GetPropertyList();
        foreach (Dictionary property in properties)
        {
            string name = property["name"].AsString();
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            if ((usage & PropertyUsageFlags.Storage) != 0 && !Skipped.Contains(name))
            {
                copy.Set(name, source.Get(name));
            }
        }

        copy.Name = source.Name;
        if (source is GeometryInstance3D)
        {
            CopyGeometryExtras(source, copy);
        }

        foreach (var group in source.GetGroups())
        {
            copy.AddToGroup(group);
        }

        for (int i = 0, count = source.GetChildCount(); i < count; i++)
        {
            copy.AddChild(Copy(source.GetChild(i)));
        }

        return copy;
    }

    // A scripted node is made from its script, so the copy runs the same code.
    private static Node New(Node source) =>
        source.GetScript().Obj is CSharpScript script
            ? script.New().As<Node>()
            : ClassDB.Instantiate(source.GetClass()).As<Node>();

    // What a geometry node's own property list adds to its class's.
    private static void CopyGeometryExtras(Node source, Node copy)
    {
        foreach (var meta in source.GetMetaList())
        {
            copy.SetMeta(meta, source.GetMeta(meta));
        }

        if (source is MeshInstance3D mesh && copy is MeshInstance3D meshCopy)
        {
            for (int i = 0, count = mesh.GetSurfaceOverrideMaterialCount(); i < count; i++)
            {
                meshCopy.SetSurfaceOverrideMaterial(i, mesh.GetSurfaceOverrideMaterial(i));
            }

            for (int i = 0, count = mesh.GetBlendShapeCount(); i < count; i++)
            {
                meshCopy.SetBlendShapeValue(i, mesh.GetBlendShapeValue(i));
            }
        }
    }
}
