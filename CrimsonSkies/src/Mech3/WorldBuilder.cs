using System;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Builds the whole world of a chapter gamez.zbd (terrain chunks, buildings, zeppelins,
/// horizon). World content lives in two places: the World node's children, and ~350
/// top-level subtrees referenced only through the World's spatial partition grid.
/// </summary>
public sealed class WorldBuilder
{
    private readonly GameZ _gamez;
    private readonly SceneBuilder _scene;

    // Non-scenery world content: 'horizon' is the original skydome (17 km sphere that
    // would swallow the scene and shadow it — TODO render it as a real sky later),
    // 'fvol1'..'fvol9' are flight-boundary volumes, 'dzpaths' are colored path ribbons.
    private static bool SkipWorldNode(GameZNode n) =>
        n.Name.Equals("horizon", StringComparison.OrdinalIgnoreCase)
        || n.Name.Equals("dzpaths", StringComparison.OrdinalIgnoreCase)
        || n.Name.StartsWith("fvol", StringComparison.OrdinalIgnoreCase);

    // Rendered but not solid: the plane should fly through cloud/sky geometry, not crash
    // into it. Node names are unreliable for this (cloud layers turn up under generic
    // names like 'g27517'), so classify by texture — anything skinned with a cloud*/sky*
    // texture is non-solid. Terrain, water, buildings, zeppelins, trains stay solid.
    private bool NoCollisionNode(GameZNode n)
    {
        if (n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return false;
        foreach (var poly in _gamez.Meshes[n.MeshIndex].Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            if (tex != null && (tex.StartsWith("cloud", StringComparison.OrdinalIgnoreCase)
                                || tex.StartsWith("sky", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    public int MeshInstanceCount => _scene.MeshInstanceCount;
    public int ColliderCount => _scene.ColliderCount;

    /// <param name="collision">Attach static colliders to solid geometry so the flight
    /// loop can raycast against terrain and buildings. Off for static viewing.</param>
    public WorldBuilder(GameZ gamez, TextureArchive textures, bool collision = false)
    {
        _gamez = gamez;
        _scene = new SceneBuilder(gamez, textures, fullbright: true, generateCollision: collision);
    }

    public Node3D Build(string worldName = "world1")
    {
        GameZNode? world = null;
        foreach (var n in _gamez.Nodes)
            if (n.Kind == "World" && string.Equals(n.Name, worldName, StringComparison.OrdinalIgnoreCase))
            {
                world = n;
                break;
            }
        if (world == null)
            throw new ArgumentException($"world node '{worldName}' not found in GameZ data");

        var root = new Node3D { Name = worldName };

        foreach (var childIndex in world.Children)
            Add(root, childIndex);
        if (world.PartitionNodes != null)
            foreach (var idx in world.PartitionNodes)
                Add(root, idx);
        return root;
    }

    private void Add(Node3D root, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _gamez.Nodes.Count)
            return;
        var built = _scene.BuildSubtree(_gamez.Nodes[nodeIndex], SkipWorldNode, NoCollisionNode);
        if (built != null)
            root.AddChild(built);
    }
}
